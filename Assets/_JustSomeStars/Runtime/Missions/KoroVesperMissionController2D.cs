using System;
using System.Collections.Generic;
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
    public sealed class KoroVesperMissionController2D : MonoBehaviour,
        ISurfaceGameplayExtension
    {
        [SerializeField] private GeyserController naturalGeyser;
        [SerializeField] private GeyserController signalGeyser;
        [SerializeField] private KoroCrewRuntime2D crewRuntime;
        [SerializeField] private Rigidbody2D captainBody;
        [SerializeField] private Transform fragmentPoint;
        [SerializeField] private GameObject fragmentVisual;
        [SerializeField] private TMP_Text objectiveLabel;
        [SerializeField] private TMP_Text spectrumPanel;
        [SerializeField] private AudioSource comparisonCue;
        [SerializeField] private AudioSource fragmentCue;
        [SerializeField] private CompositionCamera2D compositionCamera;
        [SerializeField] private SpriteRenderer[] framingLayers =
            Array.Empty<SpriteRenderer>();
        [SerializeField, Min(0.01f)] private float authoredOrthographicSize = 5f;
        [SerializeField, Min(0.01f)] private float minimumResponsiveSize = 3.70f;
        [SerializeField, Min(0.25f)] private float interactionDistance = 1.5f;
        [SerializeField] private float traversalMilestoneX = 2.25f;

        private readonly CancellationTokenSource m_Lifetime = new();
        private readonly HashSet<string> m_PresentedDialogue = new(
            StringComparer.Ordinal);
        private SurfaceGameplayDependencies m_Dependencies;
        private KoroVesperProgressionService m_Progression;
        private InteractionReservationService m_Reservations;
        private IDisposable m_PhenomenonSubscription;
        private IDisposable m_InteractionSubscription;
        private KoroSpectrumSample m_NaturalSample;
        private KoroSpectrumSample m_SignalSample;
        private bool m_ComparisonPublished;
        private bool m_NaturalWasActive;
        private bool m_SignalWasActive;
        private int m_RhythmStep;
        private bool m_RhythmPublished;
        private bool m_TraversalPublished;
        private string m_PresentedObjectiveId;
        private MirraDialoguePresenter2D m_DialoguePresenter;
        private Task m_DialogueTail = Task.CompletedTask;
        private float m_LastFramingAspect = -1f;

        public bool IsConfigured => m_Dependencies != null;
        public string[] ActiveHumanCompanionIds =>
            new[] { "crew.mira", "crew.bea" };
        public ContentId OriId => new ContentId("crew.ori");
        public bool HasNaturalSample => m_NaturalSample != null;
        public bool HasSignalSample => m_SignalSample != null;
        public bool HasComparison => m_ComparisonPublished ||
            m_Progression?.CheckpointOrdinal >= 4;

        public void Configure(SurfaceGameplayDependencies dependencies)
        {
            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }
            if (m_Dependencies != null)
            {
                if (ReferenceEquals(m_Dependencies, dependencies)) return;
                throw new InvalidOperationException(
                    "Koro mission controller is already composition-owned.");
            }

            m_Progression = dependencies.ResolveProgression<
                KoroVesperProgressionService>() ?? throw new InvalidOperationException(
                "Koro scene requires the active Koro/Vesper progression.");
            m_DialoguePresenter = GetComponent<MirraDialoguePresenter2D>();
            ValidateOrThrow();
            m_Dependencies = dependencies;
            ApplyResponsiveFraming(true);
            m_Reservations = new InteractionReservationService();
            crewRuntime.Configure(m_Reservations);
            EnsureCue(comparisonCue, "KoroSpectrumComparisonCue", 880f, 0.18f);
            EnsureCue(fragmentCue, "KoroSignalFragmentCue", 660f, 0.28f);
            m_PhenomenonSubscription = dependencies.Events.Subscribe<
                PhenomenonObserved>(OnPhenomenonObserved);
            m_InteractionSubscription = dependencies.Events.Subscribe<
                InteractionCompleted>(_ => SynchronizePresentation());
            dependencies.Input.GameplayCommandPerformed += OnGameplayCommand;
            RestoreSamplesFromSave();
            m_TraversalPublished = m_Progression.CheckpointOrdinal >= 3;
            SynchronizePresentation();
        }

        public void Release(SurfaceGameplayDependencies dependencies)
        {
            if (m_Dependencies == null) return;
            if (!ReferenceEquals(m_Dependencies, dependencies))
            {
                throw new InvalidOperationException(
                    "Koro mission controller can only release its owner.");
            }
            dependencies.Input.GameplayCommandPerformed -= OnGameplayCommand;
            m_PhenomenonSubscription?.Dispose();
            m_InteractionSubscription?.Dispose();
            m_PhenomenonSubscription = null;
            m_InteractionSubscription = null;
            crewRuntime.Release(m_Reservations);
            m_Reservations = null;
            m_Dependencies = null;
            m_Progression = null;
            m_DialoguePresenter = null;
            m_LastFramingAspect = -1f;
            m_TraversalPublished = false;
            m_PresentedObjectiveId = null;
        }

        public async Task<bool> TryRecoverFragmentAsync(
            CancellationToken cancellationToken)
        {
            RequireConfigured();
            if (!m_Progression.IsActiveNode("mission.koro-vesper.fragment") ||
                Vector2.Distance(captainBody.position, fragmentPoint.position) >
                interactionDistance)
            {
                return false;
            }
            m_Dependencies.Events.Publish(new SignalFragmentRecovered(
                m_Progression.FragmentId));
            await m_Progression.FlushPendingAsync(cancellationToken);
            fragmentCue.volume = m_Dependencies.Settings.Current.EffectsVolume;
            fragmentCue.Play();
            SynchronizePresentation();
            if (m_Dependencies.ChapterProgression is
                DestinationProgressionCoordinator coordinator)
            {
                await coordinator.AdvanceToAsterAsync(cancellationToken);
                await m_Dependencies.Modes.EnterAsync(
                    GameMode.Flight,
                    cancellationToken);
                await m_Dependencies.Scenes.RouteAsync(
                    AsterVeilProgressionService.FlightSceneName,
                    cancellationToken);
            }
            return true;
        }

        private void OnPhenomenonObserved(PhenomenonObserved observed)
        {
            if (m_Progression == null || m_Progression.CheckpointOrdinal != 3)
            {
                return;
            }

            if (observed.PhenomenonId.Value == "phenomenon.koro.geyser-natural")
            {
                m_NaturalSample = NaturalSample();
            }
            else if (observed.PhenomenonId.Value ==
                     "phenomenon.koro.geyser-signal")
            {
                m_SignalSample = SignalSample();
            }
            else
            {
                return;
            }

            if (m_NaturalSample != null && m_SignalSample != null &&
                !m_ComparisonPublished)
            {
                var result = KoroSpectrumComparison.Compare(
                    m_NaturalSample, m_SignalSample);
                var scienceCopy = m_Progression.Content.ResolveAtlasEnglish(
                    m_Progression.Content.GeyserAtlas.StableId,
                    m_Dependencies.Settings.Current.ScienceDepth);
                spectrumPanel.text =
                    $"UV FALSE-COLOR · {result.Unit}\n" +
                    $"MATCH {result.MatchScore:P0}\n" + result.Interpretation +
                    "\n\n" + scienceCopy;
                m_Dependencies.Events.Publish(new EvidenceAccepted(
                    new ContentId("evidence.koro.spectrum-comparison"),
                    new ContentId("prediction.koro.water-related-material")));
                m_ComparisonPublished = true;
                comparisonCue.volume = m_Dependencies.Settings.Current.EffectsVolume;
                comparisonCue.Play();
            }
            SynchronizePresentation();
        }

        private void RestoreSamplesFromSave()
        {
            if (m_Progression.HasDiscovery("sample.koro.geyser-natural"))
            {
                m_NaturalSample = NaturalSample();
            }
            if (m_Progression.HasDiscovery("sample.koro.geyser-signal"))
            {
                m_SignalSample = SignalSample();
            }
            if (m_Progression.HasDiscovery("evidence.koro.spectrum-comparison"))
            {
                m_ComparisonPublished = true;
            }
        }

        private static void EnsureCue(
            AudioSource source,
            string name,
            float frequency,
            float seconds)
        {
            if (source.clip != null)
            {
                return;
            }

            const int sampleRate = 22050;
            var sampleCount = Mathf.CeilToInt(sampleRate * seconds);
            var samples = new float[sampleCount];
            for (var index = 0; index < sampleCount; index++)
            {
                var time = index / (float)sampleRate;
                var envelope = Mathf.Sin(Mathf.PI * index / sampleCount);
                samples[index] = Mathf.Sin(2f * Mathf.PI * frequency * time) *
                    envelope * 0.18f;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            source.clip = clip;
            source.playOnAwake = false;
        }

        private static KoroSpectrumSample NaturalSample() => new(
            "spectrum.koro.natural",
            new[] { 121.6f, 130.4f, 135.6f },
            new[] { 1f, 0.42f, 0.68f },
            "nm");

        private static KoroSpectrumSample SignalSample() => new(
            "spectrum.koro.signal",
            new[] { 121.6f, 130.4f, 135.6f },
            new[] { 1f, 0.42f, 0.91f },
            "nm");

        private void SynchronizePresentation()
        {
            if (m_Progression == null) return;
            m_PresentedObjectiveId = m_Progression.CurrentObjectiveId;
            objectiveLabel.text = m_Progression.IsMissionComplete
                ? "CHAPTER COMPLETE · SECOND SIGNAL RECOVERED"
                : m_PresentedObjectiveId switch
            {
                "mission.koro-vesper.landed" => "LAND ON KORO",
                "mission.koro-vesper.traversal" => "CROSS THE LOW-GRAVITY SHELVES",
                "mission.koro-vesper.spectra" => "COMPARE BOTH GEYSER SPECTRA",
                "mission.koro-vesper.rhythm" => "FOLLOW THE REPEATING RHYTHM",
                "mission.koro-vesper.fragment" => "RECOVER THE SECOND FRAGMENT",
                _ => "SECOND SIGNAL · KORO",
            };
            fragmentVisual.SetActive(
                m_Progression.CheckpointOrdinal >= 5 &&
                !m_Progression.IsMissionComplete);
            QueueDialogueForObjective(m_PresentedObjectiveId);
        }

        private void QueueDialogueForObjective(string objectiveId)
        {
            var dialogueId = objectiveId switch
            {
                "mission.koro-vesper.traversal" => "dialogue.koro.bea",
                "mission.koro-vesper.spectra" => "dialogue.koro.mira",
                "mission.koro-vesper.rhythm" => "dialogue.koro.ori",
                _ => string.Empty,
            };
            if (string.IsNullOrEmpty(dialogueId) ||
                !m_PresentedDialogue.Add(dialogueId))
            {
                return;
            }

            var entry = m_Progression.Content.Dialogue.Single(item =>
                item.StableId.Value == dialogueId);
            var localized = m_Progression.Content.English.Resolve(
                entry.LocalizationKey);
            var presenter = m_DialoguePresenter;
            m_DialogueTail = PresentDialogueAfterAsync(
                m_DialogueTail,
                presenter,
                entry,
                localized,
                m_Lifetime.Token);
        }

        private static async Task PresentDialogueAfterAsync(
            Task previous,
            MirraDialoguePresenter2D presenter,
            DialogueEntry entry,
            string localized,
            CancellationToken cancellationToken)
        {
            try
            {
                await previous;
                cancellationToken.ThrowIfCancellationRequested();
                await presenter.PresentAsync(entry, localized, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Scene teardown cancels queued presentation without leaking UI.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void OnGameplayCommand(
            GameplayInputMode inputMode,
            SemanticGameplayCommand command)
        {
            if (inputMode == GameplayInputMode.Surface &&
                command == SemanticGameplayCommand.Primary)
            {
                _ = TryRecoverFragmentAsync(m_Lifetime.Token);
            }
        }

        private void Update()
        {
            ApplyResponsiveFraming(false);
            if (m_Progression == null)
            {
                return;
            }

            if (!string.Equals(
                    m_PresentedObjectiveId,
                    m_Progression.CurrentObjectiveId,
                    StringComparison.Ordinal))
            {
                SynchronizePresentation();
            }

            if (!m_TraversalPublished &&
                m_Progression.CheckpointOrdinal == 2 &&
                captainBody.position.x >= traversalMilestoneX)
            {
                m_TraversalPublished = true;
                m_Dependencies.Events.Publish(new TraversalMilestoneReached(
                    new ContentId("route.koro.low-gravity")));
                _ = m_Progression.FlushPendingAsync(m_Lifetime.Token);
                SynchronizePresentation();
            }

            if (m_RhythmPublished || m_Progression.CheckpointOrdinal != 4)
            {
                return;
            }

            var naturalStarted = naturalGeyser.Current.HazardActive &&
                !m_NaturalWasActive;
            var signalStarted = signalGeyser.Current.HazardActive &&
                !m_SignalWasActive;
            m_NaturalWasActive = naturalGeyser.Current.HazardActive;
            m_SignalWasActive = signalGeyser.Current.HazardActive;

            if ((m_RhythmStep == 0 && naturalStarted) ||
                (m_RhythmStep == 1 && signalStarted) ||
                (m_RhythmStep == 2 && naturalStarted))
            {
                m_RhythmStep++;
            }
            else if (naturalStarted)
            {
                m_RhythmStep = 1;
            }

            if (m_RhythmStep < 3)
            {
                return;
            }

            m_Dependencies.Events.Publish(new InteractionCompleted(
                new ContentId("interaction.koro.geyser-rhythm")));
            _ = m_Progression.FlushPendingAsync(m_Lifetime.Token);
            m_RhythmPublished = true;
            SynchronizePresentation();
        }

        private void ApplyResponsiveFraming(bool force)
        {
            if (compositionCamera == null || framingLayers == null ||
                framingLayers.Length == 0)
            {
                return;
            }

            var camera = compositionCamera.ControlledCamera;
            var aspect = camera.aspect;
            if (float.IsNaN(aspect) || float.IsInfinity(aspect) || aspect <= 0f)
            {
                throw new InvalidOperationException(
                    "Koro responsive framing requires a finite camera aspect.");
            }
            if (!force && Mathf.Abs(aspect - m_LastFramingAspect) < 0.001f)
            {
                return;
            }

            var visualWidth = framingLayers.Min(renderer =>
                renderer.sprite.bounds.size.x *
                Mathf.Abs(renderer.transform.lossyScale.x));
            var responsiveSize = Mathf.Clamp(
                visualWidth / (2f * aspect),
                minimumResponsiveSize,
                authoredOrthographicSize);
            compositionCamera.SetZoom(responsiveSize);
            camera.orthographicSize = responsiveSize;
            m_LastFramingAspect = aspect;
        }

        private void ValidateOrThrow()
        {
            if (naturalGeyser == null || signalGeyser == null ||
                ReferenceEquals(naturalGeyser, signalGeyser) || crewRuntime == null ||
                captainBody == null || fragmentPoint == null ||
                fragmentVisual == null || objectiveLabel == null ||
                spectrumPanel == null || comparisonCue == null ||
                fragmentCue == null || compositionCamera == null ||
                framingLayers == null || framingLayers.Length != 6 ||
                framingLayers.Any(renderer =>
                    renderer == null || renderer.sprite == null) ||
                authoredOrthographicSize <= 0f || minimumResponsiveSize <= 0f ||
                minimumResponsiveSize > authoredOrthographicSize ||
                interactionDistance <= 0f ||
                m_DialoguePresenter == null ||
                float.IsNaN(traversalMilestoneX) ||
                float.IsInfinity(traversalMilestoneX) ||
                traversalMilestoneX <= 0f)
            {
                throw new InvalidOperationException(
                    "Koro mission controller needs both geysers, crew, Lens panel, " +
                    "fragment, objective and audio cues.");
            }
        }

        private void RequireConfigured()
        {
            if (m_Dependencies == null)
            {
                throw new InvalidOperationException(
                    "Koro mission controller must be configured first.");
            }
        }

        private void OnDestroy()
        {
            m_Lifetime.Cancel();
            m_Lifetime.Dispose();
            if (m_Dependencies != null)
            {
                Release(m_Dependencies);
            }
        }
    }
}
