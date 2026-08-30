using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Crew;
using JustSomeStars.Runtime.Dialogue;
using JustSomeStars.Runtime.Discovery;
using JustSomeStars.Runtime.Input;
using JustSomeStars.Runtime.Interaction;
using JustSomeStars.Runtime.Player;
using TMPro;
using UnityEngine;

namespace JustSomeStars.Runtime.Missions
{
    [DisallowMultipleComponent]
    public sealed class MirraMissionController2D : MonoBehaviour,
        ISurfaceGameplayExtension
    {
        [SerializeField] private MirraClimateField climateField;
        [SerializeField] private MirraDialoguePresenter2D dialoguePresenter;
        [SerializeField] private MirraCrewRuntime2D crewRuntime;
        [SerializeField] private MirraInteractionParticipant2D[] repairParticipants =
            Array.Empty<MirraInteractionParticipant2D>();
        [SerializeField] private InteractionAnchor2D[] repairAnchors =
            Array.Empty<InteractionAnchor2D>();
        [SerializeField] private Rigidbody2D captainBody;
        [SerializeField] private Transform probePoint;
        [SerializeField] private Transform fragmentPoint;
        [SerializeField] private Transform departurePoint;
        [SerializeField] private GameObject fragmentVisual;
        [SerializeField] private TMP_Text objectiveLabel;
        [SerializeField, Min(0.25f)] private float interactionDistance = 1.5f;
        [SerializeField] private string miraActorId = "crew.mira";
        [SerializeField] private string junoActorId = "crew.juno";
        [SerializeField] private string oriActorId = "robot.ori";
        [SerializeField] private string returnFlightScene = "Task17FlightGraybox";

        private readonly CancellationTokenSource m_Lifetime = new();
        private InteractionReservationService m_Reservations;
        private InteractionRunner m_InteractionRunner;
        private SurfaceGameplayDependencies m_Dependencies;
        private IDisposable m_LandingSubscription;
        private bool m_OperationInFlight;

        public string[] ActiveHumanCompanionIds => new[] { miraActorId, junoActorId };
        public ContentId OriId => new ContentId(oriActorId);
        public int ActiveLeaseCount => m_Reservations?.ActiveLeaseCount ?? 0;
        public int HintPresentationCount =>
            m_Dependencies?.Progression?.HintPresentationCount ?? 0;
        public string CurrentObjectiveId =>
            m_Dependencies?.Progression?.CurrentObjectiveId ?? string.Empty;
        public bool IsConfigured => m_Dependencies != null;
        public Task ActiveOperation { get; private set; } = Task.CompletedTask;

        public void Configure(SurfaceGameplayDependencies dependencies)
        {
            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }

            if (m_Dependencies != null)
            {
                if (ReferenceEquals(m_Dependencies, dependencies))
                {
                    return;
                }

                throw new InvalidOperationException(
                    "Mirra mission controller is already composition-owned.");
            }

            ValidateOrThrow(dependencies);
            m_Dependencies = dependencies;
            m_Reservations = new InteractionReservationService();
            m_InteractionRunner = new InteractionRunner(
                m_Reservations,
                dependencies.Events);
            crewRuntime.Configure(m_Reservations);
            dependencies.Progression.BindDialoguePresenter(dialoguePresenter);
            dependencies.Progression.ObjectiveChanged += OnObjectiveChanged;
            climateField.Bind(dependencies.Events, dependencies.Settings);
            m_LandingSubscription = dependencies.Events.Subscribe<LandingCompleted>(
                _ => climateField.BeginObservation());
            dependencies.Input.GameplayCommandPerformed += OnGameplayCommand;
            if (dependencies.Progression.CheckpointOrdinal >= 2)
            {
                climateField.BeginObservation();
            }
            SynchronizePresentation();
        }

        public void Release(SurfaceGameplayDependencies dependencies)
        {
            if (m_Dependencies == null)
            {
                return;
            }

            if (!ReferenceEquals(m_Dependencies, dependencies))
            {
                throw new InvalidOperationException(
                    "Mirra mission controller can only release its owner.");
            }

            dependencies.Input.GameplayCommandPerformed -= OnGameplayCommand;
            dependencies.Progression.ObjectiveChanged -= OnObjectiveChanged;
            m_LandingSubscription?.Dispose();
            m_LandingSubscription = null;
            climateField.Release(dependencies.Events);
            crewRuntime.Release(m_Reservations);
            dependencies.Progression.ReleaseDialoguePresenter(dialoguePresenter);
            m_Dependencies = null;
            m_InteractionRunner = null;
            m_Reservations = null;
            m_OperationInFlight = false;
        }

        public async Task<bool> TryRepairAsync(CancellationToken cancellationToken)
        {
            RequireConfigured();
            if (!m_Dependencies.Progression.IsActiveNode("mission.mirra.repaired"))
            {
                m_Dependencies.Events.Publish(new PlayerBehaviorObserved(
                    m_Dependencies.Progression.Content.RepairInteractionId,
                    PlayerBehaviorOutcome.RecoveryRequested));
                await Task.Yield();
                return false;
            }

            crewRuntime.SetCinematicControl(true);
            try
            {
                await m_InteractionRunner.RunAsync(
                    m_Dependencies.Progression.Content.ProbeRepair,
                    repairParticipants,
                    repairAnchors,
                    cancellationToken);
                await m_Dependencies.Progression.FlushPendingAsync(cancellationToken);
                SynchronizePresentation();
                return true;
            }
            finally
            {
                crewRuntime.SetCinematicControl(false);
            }
        }

        public async Task<bool> TryRecoverFragmentAsync(
            CancellationToken cancellationToken)
        {
            RequireConfigured();
            if (!m_Dependencies.Progression.IsActiveNode("mission.mirra.fragment"))
            {
                return false;
            }

            m_Dependencies.Events.Publish(new SignalFragmentRecovered(
                m_Dependencies.Progression.Content.FragmentId));
            await m_Dependencies.Progression.FlushPendingAsync(cancellationToken);
            SynchronizePresentation();
            return true;
        }

        public async Task<bool> TryDepartAsync(CancellationToken cancellationToken)
        {
            RequireConfigured();
            var dependencies = m_Dependencies;
            if (!dependencies.Progression.IsActiveNode(
                    "mission.mirra.departure-requested"))
            {
                return false;
            }

            await dependencies.Modes.EnterAsync(GameMode.Flight, cancellationToken);
            try
            {
                await dependencies.Scenes.RouteAsync(
                    returnFlightScene,
                    cancellationToken);
                if (dependencies.Modes.CurrentMode != GameMode.Flight)
                {
                    await dependencies.Modes.EnterAsync(
                        GameMode.Flight,
                        cancellationToken);
                }
            }
            catch
            {
                await dependencies.Modes.EnterAsync(
                    GameMode.Surface,
                    CancellationToken.None);
                throw;
            }

            dependencies.Events.Publish(new DepartureRequested(
                dependencies.Progression.Content.DepartureId));
            await dependencies.Progression.FlushPendingAsync(CancellationToken.None);
            await dependencies.Progression.ConfirmDepartureAsync(
                CancellationToken.None);

            return true;
        }

        public void EvaluateProgressNow()
        {
            RequireConfigured();
            climateField.EvaluateNow();
            SynchronizePresentation();
        }

        private void OnGameplayCommand(
            GameplayInputMode inputMode,
            SemanticGameplayCommand command)
        {
            if (m_Dependencies == null || m_OperationInFlight ||
                inputMode != GameplayInputMode.Surface ||
                command != SemanticGameplayCommand.Primary)
            {
                return;
            }

            if (Within(probePoint) &&
                m_Dependencies.Progression.IsActiveNode("mission.mirra.repaired"))
            {
                Queue(TryRepairAsync(m_Lifetime.Token));
            }
            else if (Within(fragmentPoint) &&
                m_Dependencies.Progression.IsActiveNode("mission.mirra.fragment"))
            {
                Queue(TryRecoverFragmentAsync(m_Lifetime.Token));
            }
            else if (Within(departurePoint) &&
                m_Dependencies.Progression.IsActiveNode(
                    "mission.mirra.departure-requested"))
            {
                Queue(TryDepartAsync(m_Lifetime.Token));
            }
        }

        private void Queue(Task operation)
        {
            m_OperationInFlight = true;
            ActiveOperation = ObserveAsync(operation);
        }

        private async Task ObserveAsync(Task operation)
        {
            try
            {
                await operation;
            }
            catch (OperationCanceledException) when (m_Lifetime.IsCancellationRequested)
            {
                // Scene teardown cancels the owned interaction.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                m_OperationInFlight = false;
            }
        }

        private bool Within(Transform target)
        {
            return target != null && captainBody != null &&
                Vector2.Distance(captainBody.position, target.position) <=
                    interactionDistance;
        }

        private void SynchronizePresentation()
        {
            if (m_Dependencies == null)
            {
                return;
            }

            fragmentVisual.SetActive(
                m_Dependencies.Progression.IsActiveNode("mission.mirra.fragment"));
            if (objectiveLabel != null)
            {
                objectiveLabel.text = m_Dependencies.Progression.Content
                    .ResolveObjective(m_Dependencies.Progression.CurrentObjectiveId);
            }
        }

        private void OnObjectiveChanged(string _)
        {
            SynchronizePresentation();
        }

        private void ValidateOrThrow(SurfaceGameplayDependencies dependencies)
        {
            if (dependencies.Progression == null || dependencies.Saves == null ||
                dependencies.Scenes == null || climateField == null ||
                dialoguePresenter == null || crewRuntime == null ||
                captainBody == null ||
                probePoint == null || fragmentPoint == null || departurePoint == null ||
                fragmentVisual == null || objectiveLabel == null ||
                repairParticipants == null || repairParticipants.Length != 3 ||
                repairParticipants.Any(item => item == null) ||
                repairAnchors == null || repairAnchors.Length != 3 ||
                repairAnchors.Any(item => item == null) ||
                repairParticipants.Select(item => item.ActorKind).Distinct().Count() != 3 ||
                repairAnchors.Select(item => item.ActorKind).Distinct().Count() != 3 ||
                !ActiveHumanCompanionIds.SequenceEqual(
                    new[] { "crew.mira", "crew.juno" }) ||
                OriId.Value != "robot.ori" ||
                string.IsNullOrWhiteSpace(returnFlightScene))
            {
                throw new InvalidOperationException(
                    "Mirra mission controller requires the canonical party, " +
                    "interaction anchors, UI, climate, save, and route bindings.");
            }

            foreach (var participant in repairParticipants)
            {
                participant.ValidateOrThrow();
            }

            foreach (var anchor in repairAnchors)
            {
                anchor.ValidateOrThrow();
            }
        }

        private void RequireConfigured()
        {
            if (m_Dependencies == null)
            {
                throw new InvalidOperationException(
                    "Mirra mission controller must be composition-configured first.");
            }
        }

        private void OnDestroy()
        {
            m_Lifetime.Cancel();
            if (m_Dependencies != null)
            {
                Release(m_Dependencies);
            }

            m_Lifetime.Dispose();
        }
    }
}
