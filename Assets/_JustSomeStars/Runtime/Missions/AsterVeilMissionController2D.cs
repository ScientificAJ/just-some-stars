using System;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Flight;
using JustSomeStars.Runtime.Input;
using TMPro;
using JustSomeStars.Runtime.UI;
using UnityEngine;

namespace JustSomeStars.Runtime.Missions
{
    [DisallowMultipleComponent]
    public sealed class AsterVeilMissionController2D : MonoBehaviour,
        IFlightGameplayExtension
    {
        [SerializeField] private DebrisFieldController debrisField;
        [SerializeField] private TMP_Text objectiveLabel;
        [SerializeField] private TMP_Text crewTrustLabel;
        [SerializeField] private GameObject fragmentVisual;
        [SerializeField] private GameObject routeHologram;
        [SerializeField] private AudioSource routeCue;
        [SerializeField] private AudioSource fragmentCue;

        private FlightGameplayDependencies m_Dependencies;
        private AsterVeilProgressionService m_Progression;
        private bool m_CommandInFlight;

        public bool IsConfigured => m_Dependencies != null;
        public bool CrewTrustHandedToCaptain =>
            m_Progression?.CheckpointOrdinal >= 1;

        public void Configure(FlightGameplayDependencies dependencies)
        {
            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }
            if (m_Dependencies != null)
            {
                if (ReferenceEquals(m_Dependencies, dependencies)) return;
                throw new InvalidOperationException(
                    "Aster mission controller is already composition-owned.");
            }
            if (debrisField == null || objectiveLabel == null ||
                crewTrustLabel == null || fragmentVisual == null ||
                routeHologram == null || routeCue == null || fragmentCue == null)
            {
                throw new InvalidOperationException(
                    "Aster mission requires its complete route presentation.");
            }
            m_Progression = dependencies.Progression switch
            {
                DestinationProgressionCoordinator coordinator =>
                    coordinator.RequireActive<AsterVeilProgressionService>(),
                AsterVeilProgressionService direct => direct,
                _ => throw new InvalidOperationException(
                    "Aster scene requires active Aster Veil progression."),
            };
            m_Dependencies = dependencies;
            dependencies.Input.GameplayCommandPerformed += OnGameplayCommand;
            if (m_Progression.CheckpointOrdinal == 6)
            {
                debrisField.PrepareEscapeTraversal();
            }
            SynchronizePresentation();
        }

        public void Release(FlightGameplayDependencies dependencies)
        {
            if (m_Dependencies == null) return;
            if (!ReferenceEquals(m_Dependencies, dependencies))
            {
                throw new InvalidOperationException(
                    "Aster mission can only release its owner.");
            }
            dependencies.Input.GameplayCommandPerformed -= OnGameplayCommand;
            m_Dependencies = null;
            m_Progression = null;
            m_CommandInFlight = false;
        }

        private void OnGameplayCommand(
            GameplayInputMode inputMode,
            SemanticGameplayCommand command)
        {
            if (inputMode != GameplayInputMode.Flight ||
                command != SemanticGameplayCommand.Primary || m_CommandInFlight)
            {
                return;
            }
            m_CommandInFlight = true;
            _ = AdvancePlayerDecisionAsync(destroyCancellationToken);
        }

        public async Task AdvancePlayerDecisionAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                if (m_Progression == null) return;
                switch (m_Progression.CheckpointOrdinal)
                {
                    case 0:
                        crewTrustLabel.text =
                            Task28English.ResolveDefault("aster.trust.pick");
                        m_Dependencies.Events.Publish(new ApproachCompleted(
                            m_Progression.ApproachId));
                        routeCue.Play();
                        break;
                    case 1:
                        m_Dependencies.Events.Publish(new InteractionCompleted(
                            new ContentId("interaction.aster.route-committed")));
                        routeHologram.SetActive(true);
                        routeCue.Play();
                        break;
                    case 2:
                        m_Dependencies.Events.Publish(new PhenomenonObserved(
                            new ContentId("phenomenon.aster.relative-motion")));
                        break;
                    case 4:
                        if (!debrisField.CanRecoverFragment(
                                fragmentVisual.transform.position,
                                1.35f))
                        {
                            objectiveLabel.text =
                                Task28English.ResolveDefault(
                                    "aster.fragment.safeLane");
                            return;
                        }
                        m_Dependencies.Events.Publish(new SignalFragmentRecovered(
                            m_Progression.FragmentId));
                        fragmentVisual.SetActive(false);
                        fragmentCue.Play();
                        break;
                    case 5:
                        await m_Dependencies.Scenes.RouteAsync(
                            AsterVeilProgressionService.ReconstructionSceneName,
                            cancellationToken);
                        return;
                    case 6:
                        if (!debrisField.CanEscape)
                        {
                            objectiveLabel.text =
                                Task28English.ResolveDefault(
                                    "aster.escape.openLine");
                            return;
                        }
                        m_Dependencies.Events.Publish(new DepartureCompleted(
                            new ContentId("departure.aster.escape")));
                        await m_Progression.FlushPendingAsync(cancellationToken);
                        await m_Dependencies.Modes.EnterAsync(
                            GameMode.Clubhouse,
                            cancellationToken);
                        await m_Dependencies.Scenes.RouteAsync(
                            AsterVeilProgressionService.ClubhouseSceneName,
                            cancellationToken);
                        return;
                }
                await m_Progression.FlushPendingAsync(cancellationToken);
                SynchronizePresentation();
            }
            finally
            {
                m_CommandInFlight = false;
            }
        }

        private void SynchronizePresentation()
        {
            if (m_Progression == null) return;
            objectiveLabel.text = m_Progression.CheckpointOrdinal switch
            {
                0 => Task28English.ResolveDefault("aster.objective.0"),
                1 => Task28English.ResolveDefault("aster.objective.1"),
                2 => Task28English.ResolveDefault("aster.objective.2"),
                3 => Task28English.ResolveDefault("aster.objective.3"),
                4 => Task28English.ResolveDefault("aster.objective.4"),
                5 => Task28English.ResolveDefault("aster.objective.5"),
                6 => Task28English.ResolveDefault("aster.objective.6"),
                _ => Task28English.ResolveDefault("aster.objective.complete"),
            };
            fragmentVisual.SetActive(m_Progression.CheckpointOrdinal == 4);
            routeHologram.SetActive(m_Progression.CheckpointOrdinal >= 2);
            if (m_Progression.CheckpointOrdinal >= 1)
            {
                crewTrustLabel.text =
                    Task28English.ResolveDefault("aster.trust.withYou");
            }
        }

        private void OnDestroy()
        {
            if (m_Dependencies != null)
            {
                Release(m_Dependencies);
            }
        }
    }
}
