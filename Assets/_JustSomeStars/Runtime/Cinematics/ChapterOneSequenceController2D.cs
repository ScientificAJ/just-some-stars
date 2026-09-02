using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Animation2D;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Accounts;
using JustSomeStars.Runtime.Atlas;
using JustSomeStars.Runtime.Commerce;
using JustSomeStars.Runtime.Cosmetics;
using JustSomeStars.Runtime.Input;
using JustSomeStars.Runtime.Missions;
using JustSomeStars.Runtime.Saving;
using JustSomeStars.Runtime.UI;
using TMPro;
using UnityEngine;

namespace JustSomeStars.Runtime.Cinematics
{
    public interface IChapterOneSequenceExtension
    {
        void Configure(ChapterOneSequenceDependencies dependencies);

        void Release(ChapterOneSequenceDependencies dependencies);
    }

    public interface IChapterOnePresentationGate
    {
        bool InteractionIsReleased { get; }

        void BeginAfterSceneStateApplied();
    }

    public enum ChapterOneSequenceKind
    {
        Opening = 0,
        SignalReassembly = 1,
        Clubhouse = 2,
        DinnerEnding = 3,
    }

    public sealed class ChapterOneSequenceDependencies
    {
        public ChapterOneSequenceDependencies(
            ISaveService saves,
            InputRouter input,
            GameModeController modes,
            GameEventBus events,
            ISceneTransition scenes,
            IChapterProgression progression,
            SettingsService settings = null,
            IAccountService account = null,
            IStoreService store = null)
        {
            Saves = saves ?? throw new ArgumentNullException(nameof(saves));
            Input = input ?? throw new ArgumentNullException(nameof(input));
            Modes = modes ?? throw new ArgumentNullException(nameof(modes));
            Events = events ?? throw new ArgumentNullException(nameof(events));
            Scenes = scenes ?? throw new ArgumentNullException(nameof(scenes));
            Progression = progression ?? throw new ArgumentNullException(
                nameof(progression));
            Settings = settings;
            Account = account;
            Store = store;
        }

        public ISaveService Saves { get; }
        public InputRouter Input { get; }
        public GameModeController Modes { get; }
        public GameEventBus Events { get; }
        public ISceneTransition Scenes { get; }
        public IChapterProgression Progression { get; }
        public SettingsService Settings { get; }
        public IAccountService Account { get; }
        public IStoreService Store { get; }
    }

    [DisallowMultipleComponent]
    public sealed class ChapterOneSequenceController2D : MonoBehaviour
    {
        public const string OpeningPromise =
            "We’re going exploring! We’ll be back before dinner!";
        public const string ParentQuestion = "So, did you discover anything?";
        public const string DinnerAnswer = "Just some stars.";
        public const string BirthdayDeliveryId = "birthday.ori.delivery.2026";
        public const string BirthdayDecorationsId =
            "birthday.decorations.homemade.2026";

        [SerializeField] private ChapterOneSequenceKind sequenceKind;
        [SerializeField] private LayeredCharacterRenderer captainRenderer;
        [SerializeField] private TMP_Text chapterTitle;
        [SerializeField] private TMP_Text storyCopy;
        [SerializeField] private GameObject creditsRoot;
        [SerializeField] private GameObject oriPulse;
        [SerializeField] private GameObject fragmentPulse;
        [SerializeField] private SpriteRenderer[] parallaxBands =
            Array.Empty<SpriteRenderer>();
        [SerializeField] private Transform[] crewAnchors = Array.Empty<Transform>();
        [SerializeField] private SpriteAtlasAnimator[] crewAnimators =
            Array.Empty<SpriteAtlasAnimator>();
        [SerializeField] private SpriteAnimationClipDefinition[] crewIdleClips =
            Array.Empty<SpriteAnimationClipDefinition>();
        [SerializeField] private SpriteAnimationClipDefinition[] crewActionClips =
            Array.Empty<SpriteAnimationClipDefinition>();
        [SerializeField] private AudioSource signalCue;
        [SerializeField] private Transform scoutShip;
        [SerializeField] private Transform signalHologram;
        [SerializeField] private LocalizedEnglishCatalog english;
        [SerializeField] private MonoBehaviour[] playerUiExtensions =
            Array.Empty<MonoBehaviour>();

        private ChapterOneSequenceDependencies m_Dependencies;
        private CancellationTokenSource m_Lifetime;
        private Task m_Initialization = Task.CompletedTask;
        private bool m_CommandInFlight;
        private float m_MotionTime;
        private Vector3[] m_ParallaxBasePositions = Array.Empty<Vector3>();
        private Vector3[] m_CrewBasePositions = Array.Empty<Vector3>();
        private Vector3 m_ScoutBasePosition;
        private Vector3 m_SignalBaseScale;
        private int m_BeatIndex;
        private bool m_IsSafeHub;
        private bool m_BirthdayCelebration;
        private IChapterOnePresentationGate[] m_PresentationGates =
            Array.Empty<IChapterOnePresentationGate>();

        public ChapterOneSequenceKind SequenceKind => sequenceKind;
        public bool IsConfigured => m_Dependencies != null;
        public bool IsReady => m_Initialization.IsCompletedSuccessfully &&
            m_PresentationGates.All(gate => gate.InteractionIsReleased);
        public Task InitializationTask => m_Initialization;
        public string BirthdayDeliveryStableId => BirthdayDeliveryId;
        public string BirthdayDecorationsStableId => BirthdayDecorationsId;
        public bool IsSafeHub => m_IsSafeHub;
        public CaptainBodyFamily SavedCaptainFamily =>
            captainRenderer != null
                ? captainRenderer.CurrentFamily
                : CaptainBodyFamily.Average;

        public void Configure(ChapterOneSequenceDependencies dependencies)
        {
            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }
            if (m_Dependencies != null)
            {
                if (ReferenceEquals(m_Dependencies, dependencies)) return;
                throw new InvalidOperationException(
                    "Chapter One sequence is already composition-owned.");
            }
            ValidateOrThrow();
            m_ParallaxBasePositions = new Vector3[parallaxBands.Length];
            for (var index = 0; index < parallaxBands.Length; index++)
            {
                m_ParallaxBasePositions[index] =
                    parallaxBands[index].transform.localPosition;
            }
            m_CrewBasePositions = crewAnchors
                .Select(anchor => anchor.localPosition)
                .ToArray();
            if (scoutShip != null)
            {
                m_ScoutBasePosition = scoutShip.localPosition;
            }
            m_SignalBaseScale = signalHologram.localScale;
            var extensions = RequirePlayerUiExtensions();
            m_PresentationGates = extensions
                .OfType<IChapterOnePresentationGate>()
                .ToArray();
            var configuredExtensionCount = 0;
            var inputBound = false;
            m_Dependencies = dependencies;
            m_Lifetime = new CancellationTokenSource();
            try
            {
                dependencies.Input.GameplayCommandPerformed += OnGameplayCommand;
                inputBound = true;
                foreach (var extension in extensions)
                {
                    extension.Configure(dependencies);
                    configuredExtensionCount++;
                }
                m_Initialization = InitializePresentationAsync(m_Lifetime.Token);
            }
            catch
            {
                if (inputBound)
                {
                    dependencies.Input.GameplayCommandPerformed -=
                        OnGameplayCommand;
                }
                for (var index = configuredExtensionCount - 1; index >= 0; index--)
                {
                    try
                    {
                        extensions[index].Release(dependencies);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception, this);
                    }
                }
                m_Lifetime.Cancel();
                m_Lifetime.Dispose();
                m_Lifetime = null;
                m_Dependencies = null;
                m_PresentationGates = Array.Empty<IChapterOnePresentationGate>();
                m_Initialization = Task.CompletedTask;
                throw;
            }
        }

        public void Release(ChapterOneSequenceDependencies dependencies)
        {
            if (m_Dependencies == null) return;
            if (!ReferenceEquals(m_Dependencies, dependencies))
            {
                throw new InvalidOperationException(
                    "Chapter One sequence can only release its owner.");
            }
            dependencies.Input.GameplayCommandPerformed -= OnGameplayCommand;
            var extensions = RequirePlayerUiExtensions();
            for (var index = extensions.Length - 1; index >= 0; index--)
            {
                extensions[index].Release(dependencies);
            }
            m_Lifetime.Cancel();
            m_Lifetime.Dispose();
            m_Lifetime = null;
            m_Dependencies = null;
            m_PresentationGates = Array.Empty<IChapterOnePresentationGate>();
            m_CommandInFlight = false;
        }

        private IChapterOneSequenceExtension[] RequirePlayerUiExtensions()
        {
            playerUiExtensions ??= Array.Empty<MonoBehaviour>();
            if (playerUiExtensions.Any(extension =>
                    extension == null ||
                    extension is not IChapterOneSequenceExtension) ||
                playerUiExtensions.Distinct().Count() != playerUiExtensions.Length)
            {
                throw new InvalidOperationException(
                    "Chapter One UI extensions must be unique typed components.");
            }
            return playerUiExtensions
                .Cast<IChapterOneSequenceExtension>()
                .ToArray();
        }

        public async Task CompleteOpeningAsync(CancellationToken cancellationToken)
        {
            RequireKind(ChapterOneSequenceKind.Opening);
            await m_Initialization;
            var loaded = await m_Dependencies.Saves.LoadAsync(cancellationToken);
            var save = loaded.HasSave
                ? loaded.Save
                : GameSave.CreateNew("save.chapter-one", DateTime.UtcNow.Ticks);
            if (save.ChapterOne.Phase < ChapterOnePhase.OpeningComplete)
            {
                save.ChapterOne.Phase = ChapterOnePhase.OpeningComplete;
                AdvanceMetadata(save);
                await m_Dependencies.Saves.SaveCheckpointAsync(
                    save, cancellationToken);
            }
            m_Dependencies.Events.Publish(new DepartureCompleted(
                new ContentId("departure.clubhouse.first-flight")));
            await m_Dependencies.Modes.EnterAsync(GameMode.Flight, cancellationToken);
            await m_Dependencies.Scenes.RouteAsync(
                MirraProgressionService.FlightSceneName,
                cancellationToken);
        }

        public async Task CompleteReassemblyAsync(
            CancellationToken cancellationToken)
        {
            RequireKind(ChapterOneSequenceKind.SignalReassembly);
            await m_Initialization;
            var progression = RequireAster();
            m_Dependencies.Events.Publish(new InteractionCompleted(
                new ContentId("interaction.signal.reassemble")));
            await progression.FlushPendingAsync(cancellationToken);
            PlaySignalCue();
            await m_Dependencies.Scenes.RouteAsync(
                AsterVeilProgressionService.FlightSceneName,
                cancellationToken);
        }

        public async Task CompleteReturnAsync(CancellationToken cancellationToken)
        {
            RequireKind(ChapterOneSequenceKind.Clubhouse);
            await m_Initialization;
            var loaded = await m_Dependencies.Saves.LoadAsync(cancellationToken);
            if (!loaded.HasSave ||
                !string.Equals(
                    loaded.Save.Mission.MissionId,
                    AsterVeilProgressionService.MissionId,
                    StringComparison.Ordinal) ||
                loaded.Save.Mission.CheckpointOrdinal != 7)
            {
                m_IsSafeHub = true;
                ApplyAuthoredCopy(loaded.HasSave ? loaded.Save : null);
                return;
            }
            var progression = RequireAster();
            if (progression.CheckpointOrdinal == 7)
            {
                m_Dependencies.Events.Publish(new LandingCompleted(
                    new ContentId("destination.clubhouse.return")));
                await progression.FlushPendingAsync(cancellationToken);
            }
            if (m_Dependencies.Modes.CurrentMode == GameMode.Flight)
            {
                await m_Dependencies.Modes.EnterAsync(
                    GameMode.Clubhouse,
                    cancellationToken);
            }
            await m_Dependencies.Scenes.RouteAsync(
                AsterVeilProgressionService.DinnerSceneName,
                cancellationToken);
        }

        public async Task CompleteDinnerAsync(CancellationToken cancellationToken)
        {
            RequireKind(ChapterOneSequenceKind.DinnerEnding);
            await m_Initialization;
            var progression = RequireAster();
            if (progression.CheckpointOrdinal == 8)
            {
                m_Dependencies.Events.Publish(new ConversationCompleted(
                    new ContentId("conversation.dinner.just-some-stars")));
                await progression.FlushPendingAsync(cancellationToken);
            }
            oriPulse.SetActive(true);
            fragmentPulse.SetActive(true);
            PlaySignalCue();
            await progression.CompleteFinalPulseAndUnlockCreditsAsync(
                () => creditsRoot.SetActive(true),
                cancellationToken);
        }

        private async Task InitializePresentationAsync(
            CancellationToken cancellationToken)
        {
            var loaded = await m_Dependencies.Saves.LoadAsync(cancellationToken);
            var save = loaded.HasSave
                ? loaded.Save
                : GameSave.CreateNew("save.chapter-one", DateTime.UtcNow.Ticks);
            m_IsSafeHub = sequenceKind == ChapterOneSequenceKind.Clubhouse &&
                (!string.Equals(
                     save.Mission.MissionId,
                     AsterVeilProgressionService.MissionId,
                     StringComparison.Ordinal) ||
                 save.Mission.CheckpointOrdinal != 7);
            m_BirthdayCelebration = save.Birthday != null &&
                save.Birthday.LastBirthdayGiftYear > 0;
            if (captainRenderer != null)
            {
                var loadout = CaptainSpriteLoadout.FromCaptainState(
                    save.Captain,
                    captainRenderer.ActiveLayerCount > 0
                        ? captainRenderer.ActiveLayerCount
                        : 5);
                captainRenderer.ApplyLoadout(
                    loadout,
                    SpriteFacing.Right,
                    ResolveCaptainMotion());
            }
            PlayCrewMotion(m_IsSafeHub ? crewIdleClips : crewActionClips);
            creditsRoot?.SetActive(save.ChapterOne.CreditsUnlocked);
            oriPulse?.SetActive(save.ChapterOne.FinalPulseSeen);
            fragmentPulse?.SetActive(save.ChapterOne.FinalPulseSeen);
            ApplyAuthoredCopy(save);
            foreach (var gate in m_PresentationGates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                gate.BeginAfterSceneStateApplied();
            }
        }

        private void ApplyAuthoredCopy(GameSave save)
        {
            if (chapterTitle == null || storyCopy == null) return;
            switch (sequenceKind)
            {
                case ChapterOneSequenceKind.Opening:
                    chapterTitle.text = ResolveUi("chapter.opening.title");
                    storyCopy.text = ResolveUi("chapter.opening.copy");
                    break;
                case ChapterOneSequenceKind.SignalReassembly:
                    chapterTitle.text = ResolveUi("chapter.reassembly.title");
                    storyCopy.text = ResolveUi("chapter.reassembly.copy");
                    break;
                case ChapterOneSequenceKind.Clubhouse:
                    chapterTitle.text = m_IsSafeHub
                        ? ResolveUi("chapter.clubhouse.safe.title")
                        : ResolveUi("chapter.clubhouse.return.title");
                    storyCopy.text = m_IsSafeHub
                        ? ResolveUi("chapter.clubhouse.safe.copy")
                        : ResolveUi("chapter.clubhouse.return.copy") +
                          (m_BirthdayCelebration
                              ? ResolveUi("chapter.clubhouse.birthday")
                              : string.Empty);
                    break;
                case ChapterOneSequenceKind.DinnerEnding:
                    chapterTitle.text = ResolveUi("chapter.dinner.title");
                    storyCopy.text = ResolveUi("chapter.dinner.copy");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void Update()
        {
            if (m_Dependencies == null) return;
            m_MotionTime += Time.deltaTime;
            for (var index = 0; index < parallaxBands.Length; index++)
            {
                var offset = Mathf.Sin(m_MotionTime * (0.08f + index * 0.025f)) *
                    (0.002f + index * 0.001f);
                parallaxBands[index].transform.localPosition =
                    m_ParallaxBasePositions[index] + new Vector3(offset, 0f, 0f);
            }
            AnimateStoryPresentation();
            if (oriPulse != null && oriPulse.activeSelf)
            {
                var pulse = 0.92f + Mathf.Sin(m_MotionTime * 4.2f) * 0.08f;
                oriPulse.transform.localScale = Vector3.one * pulse;
            }
            if (sequenceKind == ChapterOneSequenceKind.SignalReassembly)
            {
                var shimmer = 1f + Mathf.Sin(m_MotionTime * 2.3f) * 0.025f;
                signalHologram.localScale = m_SignalBaseScale * shimmer;
                signalHologram.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Sin(m_MotionTime * 0.72f) * 0.65f);
            }
        }

        private void OnGameplayCommand(
            GameplayInputMode inputMode,
            SemanticGameplayCommand command)
        {
            if (command != SemanticGameplayCommand.Primary ||
                m_CommandInFlight || !IsReady)
            {
                return;
            }
            m_CommandInFlight = true;
            _ = RunPrimaryAsync();
        }

        public void AdvanceFromUi()
        {
            if (m_CommandInFlight || !IsReady)
            {
                return;
            }
            m_CommandInFlight = true;
            _ = RunPrimaryAsync();
        }

        private async Task RunPrimaryAsync()
        {
            try
            {
                if (sequenceKind == ChapterOneSequenceKind.Clubhouse && m_IsSafeHub)
                {
                    await ResumeFromSafeHubAsync(m_Lifetime.Token);
                    return;
                }
                if (TryAdvanceStoryBeat()) return;
                switch (sequenceKind)
                {
                    case ChapterOneSequenceKind.Opening:
                        await CompleteOpeningAsync(m_Lifetime.Token);
                        break;
                    case ChapterOneSequenceKind.SignalReassembly:
                        await CompleteReassemblyAsync(m_Lifetime.Token);
                        break;
                    case ChapterOneSequenceKind.Clubhouse:
                        await CompleteReturnAsync(m_Lifetime.Token);
                        break;
                    case ChapterOneSequenceKind.DinnerEnding:
                        await CompleteDinnerAsync(m_Lifetime.Token);
                        break;
                }
            }
            catch (OperationCanceledException) when (
                m_Lifetime == null || m_Lifetime.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                m_CommandInFlight = false;
            }
        }

        private bool TryAdvanceStoryBeat()
        {
            var authoredBeats = sequenceKind switch
            {
                ChapterOneSequenceKind.Opening => 1,
                ChapterOneSequenceKind.SignalReassembly => 1,
                ChapterOneSequenceKind.Clubhouse => 2,
                ChapterOneSequenceKind.DinnerEnding => 2,
                _ => 0,
            };
            if (m_BeatIndex >= authoredBeats) return false;
            m_BeatIndex++;
            PlayCrewMotion(crewActionClips);
            captainRenderer?.Play(ResolveCaptainMotion());
            storyCopy.text = sequenceKind switch
            {
                ChapterOneSequenceKind.Opening =>
                    ResolveUi("chapter.opening.beat"),
                ChapterOneSequenceKind.SignalReassembly =>
                    ResolveUi("chapter.reassembly.beat"),
                ChapterOneSequenceKind.Clubhouse when m_BeatIndex == 1 =>
                    ResolveUi("chapter.clubhouse.beat1"),
                ChapterOneSequenceKind.Clubhouse =>
                    ResolveUi("chapter.clubhouse.beat2"),
                ChapterOneSequenceKind.DinnerEnding when m_BeatIndex == 1 =>
                    ResolveUi("chapter.dinner.question"),
                ChapterOneSequenceKind.DinnerEnding =>
                    ResolveUi("chapter.dinner.beat2"),
                _ => storyCopy.text,
            };
            return true;
        }

        private async Task ResumeFromSafeHubAsync(
            CancellationToken cancellationToken)
        {
            var destinationMode = m_Dependencies.Progression.ResumeMode;
            if (destinationMode == GameMode.Flight)
            {
                await m_Dependencies.Modes.EnterAsync(
                    GameMode.Flight,
                    cancellationToken);
            }
            else if (destinationMode == GameMode.Surface)
            {
                await m_Dependencies.Modes.EnterAsync(
                    GameMode.Flight,
                    cancellationToken);
                await m_Dependencies.Modes.EnterAsync(
                    GameMode.Surface,
                    cancellationToken);
            }
            else if (destinationMode != GameMode.Clubhouse)
            {
                throw new InvalidOperationException(
                    $"The Clubhouse cannot resume mode '{destinationMode}'.");
            }

            await m_Dependencies.Scenes.RouteAsync(
                m_Dependencies.Progression.ResumeSceneName,
                cancellationToken);
        }

        private void AnimateStoryPresentation()
        {
            if (crewAnchors.Length != m_CrewBasePositions.Length) return;
            for (var index = 0; index < crewAnchors.Length; index++)
            {
                var basePosition = m_CrewBasePositions[index];
                var breathing = Mathf.Sin(m_MotionTime * 1.6f + index * 0.7f) *
                    0.018f;
                var storyOffset = Vector3.zero;
                if (sequenceKind == ChapterOneSequenceKind.Clubhouse && !m_IsSafeHub)
                {
                    var run = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(
                        (m_MotionTime - 0.4f - index * 0.08f) / 2.4f));
                    storyOffset = new Vector3(run * 2.1f, 0f, 0f);
                }
                else if (sequenceKind == ChapterOneSequenceKind.SignalReassembly)
                {
                    var gather = 0.18f * Mathf.Sin(m_MotionTime * 0.9f + index);
                    storyOffset = new Vector3(
                        Mathf.Sign(-basePosition.x) * Mathf.Abs(gather),
                        Mathf.Abs(gather) * 0.12f,
                        0f);
                }
                else if (sequenceKind == ChapterOneSequenceKind.DinnerEnding)
                {
                    storyOffset = new Vector3(
                        Mathf.Sin(m_MotionTime * 0.72f + index) * 0.035f,
                        Mathf.Sin(m_MotionTime * 1.3f + index * 0.8f) * 0.026f,
                        0f);
                }
                crewAnchors[index].localPosition = basePosition + storyOffset +
                    new Vector3(0f, breathing, 0f);
            }

            if (scoutShip != null && sequenceKind == ChapterOneSequenceKind.Clubhouse &&
                !m_IsSafeHub)
            {
                var crash = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(m_MotionTime / 1.2f));
                scoutShip.localPosition = m_ScoutBasePosition +
                    new Vector3(crash * 0.65f, -crash * 0.18f, 0f);
                scoutShip.localRotation = Quaternion.Euler(0f, 0f, -11f * crash);
            }
            else if (scoutShip != null &&
                     sequenceKind == ChapterOneSequenceKind.Opening)
            {
                var launch = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01((m_MotionTime - 0.55f) / 3.1f));
                scoutShip.localPosition = m_ScoutBasePosition +
                    new Vector3(launch * 2.6f, launch * 1.15f, 0f);
                scoutShip.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Lerp(0f, 7f, launch));
            }
        }

        private string ResolveCaptainMotion() => sequenceKind switch
        {
            ChapterOneSequenceKind.Clubhouse when m_IsSafeHub => "idle",
            ChapterOneSequenceKind.Clubhouse => "run",
            ChapterOneSequenceKind.Opening or
            ChapterOneSequenceKind.SignalReassembly or
            ChapterOneSequenceKind.DinnerEnding => "interact",
            _ => "idle",
        };

        private void PlayCrewMotion(SpriteAnimationClipDefinition[] clips)
        {
            if (clips == null || clips.Length != crewAnimators.Length)
            {
                throw new InvalidOperationException(
                    "Chapter One crew choreography is incomplete.");
            }
            for (var index = 0; index < crewAnimators.Length; index++)
            {
                crewAnimators[index].Play(clips[index]);
            }
        }

        private AsterVeilProgressionService RequireAster()
        {
            if (m_Dependencies.Progression is DestinationProgressionCoordinator coordinator)
            {
                return coordinator.RequireActive<AsterVeilProgressionService>();
            }
            if (m_Dependencies.Progression is AsterVeilProgressionService direct)
            {
                return direct;
            }
            throw new InvalidOperationException(
                "This sequence requires active Aster Veil progression.");
        }

        private void PlaySignalCue()
        {
            if (signalCue == null) return;
            signalCue.Play();
        }

        private string ResolveUi(string key) => english != null
            ? english.Resolve(key)
            : Task28English.ResolveDefault(key);

        private void ValidateOrThrow()
        {
            if (!Enum.IsDefined(typeof(ChapterOneSequenceKind), sequenceKind) ||
                chapterTitle == null || storyCopy == null || creditsRoot == null ||
                oriPulse == null || fragmentPulse == null || signalCue == null ||
                parallaxBands == null || parallaxBands.Length < 4 ||
                Array.Exists(parallaxBands, item => item == null) ||
                crewAnchors == null || crewAnchors.Length != 6 ||
                Array.Exists(crewAnchors, item => item == null) ||
                crewAnimators == null || crewAnimators.Length != 5 ||
                Array.Exists(crewAnimators, item => item == null) ||
                crewIdleClips == null || crewIdleClips.Length != 5 ||
                Array.Exists(crewIdleClips, item => item == null) ||
                crewActionClips == null || crewActionClips.Length != 5 ||
                Array.Exists(crewActionClips, item => item == null) ||
                scoutShip == null ||
                signalHologram == null ||
                ((sequenceKind == ChapterOneSequenceKind.Opening ||
                  sequenceKind == ChapterOneSequenceKind.DinnerEnding) &&
                 captainRenderer == null))
            {
                throw new InvalidOperationException(
                    "Chapter One sequences require complete authored 2.5D bindings.");
            }
        }

        private void RequireKind(ChapterOneSequenceKind required)
        {
            if (sequenceKind != required || m_Dependencies == null)
            {
                throw new InvalidOperationException(
                    $"Sequence operation requires configured {required} staging.");
            }
        }

        private static void AdvanceMetadata(GameSave save)
        {
            save.Metadata.Revision = checked(save.Metadata.Revision + 1);
            save.Metadata.UpdatedUtcTicks = Math.Max(
                save.Metadata.UpdatedUtcTicks + 1,
                DateTime.UtcNow.Ticks);
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
