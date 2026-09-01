using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Atlas;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Crew;
using JustSomeStars.Runtime.Dialogue;
using JustSomeStars.Runtime.Saving;
using UnityEngine;

namespace JustSomeStars.Runtime.Missions
{
    public interface IMirraDialoguePresenter
    {
        Task PresentAsync(
            DialogueEntry entry,
            string localizedText,
            CancellationToken cancellationToken);
    }

    public sealed class MirraProgressionService : IChapterProgression
    {
        public const string ResourceName = "Task19MirraChapter";
        public const string FlightSceneName = "Task17FlightGraybox";
        public const string SurfaceSceneName = "Mirra";

        private readonly GameEventBus m_Events;
        private readonly ISaveService m_Saves;
        private readonly SettingsService m_Settings;
        private readonly List<IDisposable> m_Subscriptions = new();
        private readonly List<string> m_EventHistory = new();
        private readonly HashSet<string> m_UniqueEvents = new(StringComparer.Ordinal);
        private readonly object m_FlushGate = new();

        private MirraChapterContent m_Content;
        private MissionDirector m_Mission;
        private DialogueDirector m_Dialogue;
        private HintDirector m_Hints;
        private AtlasService m_Atlas;
        private DialogueRouter m_DialogueRouter;
        private Task m_ActiveFlush = Task.CompletedTask;
        private bool m_Initialized;
        private bool m_Shutdown;

        public MirraProgressionService(
            GameEventBus gameEvents,
            ISaveService saves,
            SettingsService settings)
        {
            m_Events = gameEvents ?? throw new ArgumentNullException(nameof(gameEvents));
            m_Saves = saves ?? throw new ArgumentNullException(nameof(saves));
            m_Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public MirraChapterContent Content => m_Content;
        public ContentId ChapterId => m_Content != null
            ? m_Content.StableId
            : new ContentId("mission.mirra.chapter-one");
        public ContentId ApproachId => m_Content != null
            ? m_Content.ApproachId
            : new ContentId("approach.mirra.safe");
        public GameSave DurableSave => m_Mission?.DurableSave;
        public ContentId FragmentId => m_Content != null ? m_Content.FragmentId : default;
        public IReadOnlyList<string> EventHistory => m_EventHistory.ToArray();
        public int DuplicateEventCount { get; private set; }
        public int HintPresentationCount => m_DialogueRouter?.HintPresentationCount ?? 0;
        public bool IsInitialized => m_Initialized;
        public bool IsMissionComplete => m_Mission != null &&
            m_Mission.CompletedNodeIds.Contains(
                "mission.mirra.complete",
                StringComparer.Ordinal);
        public int CheckpointOrdinal => m_Mission?.DurableSave.Mission.CheckpointOrdinal ?? 0;
        public string CurrentObjectiveId => m_Mission?.ActiveNodeIds
            .OrderBy(id => id, StringComparer.Ordinal)
            .FirstOrDefault() ?? string.Empty;
        public string ResumeSceneName => CheckpointOrdinal == 0 ||
            CheckpointOrdinal == 1 || CheckpointOrdinal >= 6
                ? FlightSceneName
                : SurfaceSceneName;
        public GameMode ResumeMode => string.Equals(
                ResumeSceneName,
                FlightSceneName,
                StringComparison.Ordinal)
            ? GameMode.Flight
            : GameMode.Surface;
        public bool HasPendingDeparture => IsActiveNode("mission.mirra.departed");

        public event Action<string> ObjectiveChanged;

        public async ValueTask<StartupResult> InitializeAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (m_Initialized)
            {
                return StartupResult.Available();
            }

            if (m_Shutdown)
            {
                throw new InvalidOperationException(
                    "Mirra progression cannot initialize after shutdown.");
            }

            m_Content = Resources.Load<MirraChapterContent>(ResourceName);
            if (m_Content == null)
            {
                return StartupResult.Failed(
                    $"Mirra chapter resource '{ResourceName}' is missing.");
            }

            m_Content.ValidateOrThrow();
            var loaded = await m_Saves.LoadAsync(cancellationToken);
            var save = loaded.HasSave
                ? loaded.Save
                : GameSave.CreateNew("save.mirra.local", DateTime.UtcNow.Ticks);
            m_DialogueRouter = new DialogueRouter(m_Content.ProgressionContent.English);
            m_Dialogue = new DialogueDirector(
                m_Events,
                new DialogueTokenArbiter(),
                m_DialogueRouter,
                new RuntimeClock(),
                m_Content.ProgressionContent.Dialogue);
            m_Mission = new MissionDirector(
                m_Content.Mission,
                m_Events,
                m_Saves,
                save,
                () => DateTime.UtcNow.Ticks,
                m_Dialogue,
                m_Content.ProgressionContent.Dialogue);
            m_Mission.Start();
            m_Hints = new HintDirector(
                m_Events,
                m_Dialogue,
                m_Settings.Current.ExplorationAssist,
                new[]
                {
                    new HintRule(
                        "mission.mirra.repair",
                        m_Content.RepairInteractionId.Value,
                        m_Content.ProgressionContent.RequireDialogue(
                            "dialogue.mirra.hint"),
                        2),
                });
            m_Atlas = new AtlasService(
                m_Events,
                m_Mission,
                m_Content.ProgressionContent.AtlasEntries,
                m_Content.ProgressionContent.ScienceSources,
                m_Content.ProgressionContent.English);
            BindEventHistory();
            SynchronizeHintObjective();
            m_Initialized = true;
            return StartupResult.Available();
        }

        public async ValueTask ShutdownAsync()
        {
            if (m_Shutdown)
            {
                return;
            }

            m_Shutdown = true;
            await WaitForQuiescenceAsync(CancellationToken.None);
            if (m_Mission != null)
            {
                await m_Mission.ShutdownAsync();
            }

            m_Atlas?.Dispose();
            m_Hints?.Dispose();
            m_Dialogue?.Dispose();
            foreach (var subscription in m_Subscriptions)
            {
                subscription.Dispose();
            }

            m_Subscriptions.Clear();
            m_Initialized = false;
        }

        public void BindDialoguePresenter(IMirraDialoguePresenter presenter)
        {
            RequireInitialized();
            m_DialogueRouter.Bind(presenter);
        }

        public void ReleaseDialoguePresenter(IMirraDialoguePresenter presenter)
        {
            m_DialogueRouter?.Release(presenter);
        }

        public bool IsActiveNode(string nodeId)
        {
            return m_Mission != null && m_Mission.ActiveNodeIds.Contains(
                nodeId,
                StringComparer.Ordinal);
        }

        public Task FlushPendingAsync(CancellationToken cancellationToken)
        {
            RequireInitialized();
            lock (m_FlushGate)
            {
                m_ActiveFlush = FlushAfterPreviousAsync(
                    m_ActiveFlush,
                    cancellationToken);
                return m_ActiveFlush;
            }
        }

        public async Task WaitForQuiescenceAsync(
            CancellationToken cancellationToken)
        {
            Task active;
            lock (m_FlushGate)
            {
                active = m_ActiveFlush;
            }

            cancellationToken.ThrowIfCancellationRequested();
            await active;
            cancellationToken.ThrowIfCancellationRequested();
        }

        public async Task ConfirmDepartureAsync(CancellationToken cancellationToken)
        {
            RequireInitialized();
            if (!HasPendingDeparture)
            {
                return;
            }

            m_Events.Publish(new DepartureCompleted(m_Content.DepartureId));
            await FlushPendingAsync(cancellationToken);
        }

        private async Task FlushAfterPreviousAsync(
            Task previous,
            CancellationToken cancellationToken)
        {
            await previous;
            cancellationToken.ThrowIfCancellationRequested();
            await m_Mission.FlushCheckpointAsync(cancellationToken);
            SynchronizeHintObjective();
            ObjectiveChanged?.Invoke(CurrentObjectiveId);
        }

        private void BindEventHistory()
        {
            m_Subscriptions.Add(m_Events.Subscribe<ApproachCompleted>(item =>
                RecordEvent(nameof(ApproachCompleted), item.ApproachId)));
            m_Subscriptions.Add(m_Events.Subscribe<LandingCompleted>(item =>
                RecordEvent(nameof(LandingCompleted), item.DestinationId)));
            m_Subscriptions.Add(m_Events.Subscribe<TraversalMilestoneReached>(item =>
                RecordEvent(nameof(TraversalMilestoneReached), item.MilestoneId)));
            m_Subscriptions.Add(m_Events.Subscribe<ClimateSampleObserved>(item =>
                RecordEvent(nameof(ClimateSampleObserved), item.ZoneId)));
            m_Subscriptions.Add(m_Events.Subscribe<EvidenceAccepted>(item =>
                RecordEvent(nameof(EvidenceAccepted), item.EvidenceId)));
            m_Subscriptions.Add(m_Events.Subscribe<InteractionCompleted>(item =>
                RecordEvent(nameof(InteractionCompleted), item.InteractionId)));
            m_Subscriptions.Add(m_Events.Subscribe<SignalFragmentRecovered>(item =>
                RecordEvent(nameof(SignalFragmentRecovered), item.FragmentId)));
            m_Subscriptions.Add(m_Events.Subscribe<DepartureRequested>(item =>
                RecordEvent(nameof(DepartureRequested), item.DepartureId)));
            m_Subscriptions.Add(m_Events.Subscribe<DepartureCompleted>(item =>
                RecordEvent(nameof(DepartureCompleted), item.DepartureId)));
        }

        private void RecordEvent(string kind, ContentId id)
        {
            var value = kind + ":" + id.Value;
            if (!m_UniqueEvents.Add(value))
            {
                DuplicateEventCount++;
                return;
            }

            m_EventHistory.Add(value);
            _ = FlushPendingAsync(CancellationToken.None);
        }

        private void SynchronizeHintObjective()
        {
            if (m_Hints == null)
            {
                return;
            }

            var repair = new ContentId("mission.mirra.repair");
            if (IsActiveNode("mission.mirra.traversal") ||
                IsActiveNode("mission.mirra.evidence") ||
                IsActiveNode("mission.mirra.repaired"))
            {
                m_Hints.SetObjective(repair);
            }
            else
            {
                m_Hints.CompleteObjective(repair);
            }
        }

        private void RequireInitialized()
        {
            if (!m_Initialized)
            {
                throw new InvalidOperationException(
                    "Mirra progression must be initialized first.");
            }
        }

        private sealed class RuntimeClock : IDialogueClock
        {
            public double NowSeconds => Time.realtimeSinceStartupAsDouble;
        }

        private sealed class DialogueRouter : IDialoguePresenter
        {
            private readonly LocalizedEnglishCatalog m_English;
            private IMirraDialoguePresenter m_Presenter;

            public DialogueRouter(LocalizedEnglishCatalog english)
            {
                m_English = english ?? throw new ArgumentNullException(nameof(english));
            }

            public int HintPresentationCount { get; private set; }

            public void Bind(IMirraDialoguePresenter presenter)
            {
                if (presenter == null)
                {
                    throw new ArgumentNullException(nameof(presenter));
                }

                if (m_Presenter != null && !ReferenceEquals(m_Presenter, presenter))
                {
                    throw new InvalidOperationException(
                        "Only one active Mirra dialogue presenter is allowed.");
                }

                m_Presenter = presenter;
            }

            public void Release(IMirraDialoguePresenter presenter)
            {
                if (ReferenceEquals(m_Presenter, presenter))
                {
                    m_Presenter = null;
                }
            }

            public async Task PresentAsync(
                DialogueEntry entry,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry.Priority == DialoguePriority.Hint)
                {
                    HintPresentationCount++;
                }

                var presenter = m_Presenter;
                if (presenter != null)
                {
                    await presenter.PresentAsync(
                        entry,
                        m_English.Resolve(entry.LocalizationKey),
                        cancellationToken);
                }
            }
        }
    }
}
