using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Saving;
using UnityEngine;

namespace JustSomeStars.Runtime.Missions
{
    public sealed class KoroVesperProgressionService : IChapterProgression
    {
        public const string ResourceName = "Task25KoroVesperChapter";
        public const string MissionId = "mission.koro-vesper.chapter-one";
        public const string FlightSceneName = "Task25VesperFlight";
        public const string SurfaceSceneName = "KoroVesper";
        public const string FragmentIdValue = "fragment.signal.koro.002";

        private static readonly string[] s_Checkpoints =
        {
            "mission.koro-vesper.approach",
            "mission.koro-vesper.landed",
            "mission.koro-vesper.traversal",
            "mission.koro-vesper.spectra",
            "mission.koro-vesper.rhythm",
            "mission.koro-vesper.fragment",
        };

        private readonly GameEventBus m_Events;
        private readonly ISaveService m_Saves;
        private readonly SettingsService m_Settings;
        private readonly List<IDisposable> m_Subscriptions = new();
        private readonly HashSet<string> m_UniqueEvents = new(StringComparer.Ordinal);
        private readonly List<string> m_EventHistory = new();
        private readonly object m_FlushGate = new();

        private KoroVesperChapterContent m_Content;
        private GameSave m_Working;
        private GameSave m_Durable;
        private Task m_ActiveFlush = Task.CompletedTask;
        private bool m_Dirty;
        private bool m_Initialized;
        private bool m_Shutdown;
        private bool m_NaturalObserved;
        private bool m_SignalObserved;
        private bool m_ComparisonAccepted;

        public KoroVesperProgressionService(
            GameEventBus gameEvents,
            ISaveService saves,
            SettingsService settings)
        {
            m_Events = gameEvents ?? throw new ArgumentNullException(nameof(gameEvents));
            m_Saves = saves ?? throw new ArgumentNullException(nameof(saves));
            m_Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public KoroVesperChapterContent Content => m_Content;
        public ContentId ChapterId => new ContentId(MissionId);
        public ContentId ApproachId => new ContentId("approach.vesper.gravity-route");
        public ContentId FragmentId => new ContentId(FragmentIdValue);
        public int CheckpointOrdinal => m_Working?.Mission.CheckpointOrdinal ?? 0;
        public string CurrentObjectiveId => CheckpointOrdinal >= s_Checkpoints.Length
            ? s_Checkpoints[^1]
            : s_Checkpoints[CheckpointOrdinal];
        public bool IsMissionComplete => CheckpointOrdinal >= 6 &&
            m_Working.DiscoveryIds.Contains(FragmentIdValue, StringComparer.Ordinal);
        public int DuplicateEventCount { get; private set; }
        public IReadOnlyList<string> EventHistory => m_EventHistory.ToArray();
        public bool HasDiscovery(string id)
        {
            RequireInitialized();
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(
                    "Discovery identifiers must be canonical.", nameof(id));
            }

            return m_Working.DiscoveryIds.Contains(id, StringComparer.Ordinal);
        }
        public string ResumeSceneName => CheckpointOrdinal <= 1
            ? FlightSceneName
            : SurfaceSceneName;
        public GameMode ResumeMode => CheckpointOrdinal <= 1
            ? GameMode.Flight
            : GameMode.Surface;
        public bool HasPendingDeparture => false;

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
                    "Koro/Vesper progression cannot initialize after shutdown.");
            }

            m_Content = Resources.Load<KoroVesperChapterContent>(ResourceName);
            if (m_Content == null)
            {
                return StartupResult.Failed(
                    $"Koro/Vesper chapter resource '{ResourceName}' is missing.");
            }
            m_Content.ValidateOrThrow();

            var loaded = await m_Saves.LoadAsync(cancellationToken);
            m_Working = loaded.HasSave
                ? loaded.Save
                : GameSave.CreateNew("save.koro.local", DateTime.UtcNow.Ticks);
            if (!string.Equals(
                    m_Working.Mission.MissionId,
                    MissionId,
                    StringComparison.Ordinal))
            {
                PreservePriorChapterCompletion(m_Working);
                SetCheckpoint(m_Working, 0);
            }
            else
            {
                ValidateLoadedProgress(m_Working.Mission);
            }

            m_Durable = m_Working.Copy();
            RestorePartialSamples();
            BindEvents();
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
            foreach (var subscription in m_Subscriptions)
            {
                subscription.Dispose();
            }
            m_Subscriptions.Clear();
            m_Initialized = false;
        }

        public bool IsActiveNode(string nodeId) =>
            m_Initialized && string.Equals(
                CurrentObjectiveId,
                nodeId,
                StringComparison.Ordinal) && !IsMissionComplete;

        public Task ConfirmDepartureAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
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

        public async Task WaitForQuiescenceAsync(CancellationToken cancellationToken)
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

        private async Task FlushAfterPreviousAsync(
            Task previous,
            CancellationToken cancellationToken)
        {
            await previous;
            cancellationToken.ThrowIfCancellationRequested();
            if (!m_Dirty)
            {
                return;
            }

            var candidate = m_Working.Copy();
            candidate.Metadata.Revision = checked(candidate.Metadata.Revision + 1);
            candidate.Metadata.UpdatedUtcTicks = Math.Max(
                candidate.Metadata.UpdatedUtcTicks + 1,
                DateTime.UtcNow.Ticks);
            await m_Saves.SaveCheckpointAsync(candidate, cancellationToken);
            m_Durable = candidate.Copy();
            m_Working.Metadata = candidate.Metadata.Copy();
            m_Dirty = false;
        }

        private void BindEvents()
        {
            m_Subscriptions.Add(m_Events.Subscribe<ApproachCompleted>(item =>
                Accept(nameof(ApproachCompleted), item.ApproachId, () =>
                {
                    if (CheckpointOrdinal == 0 && item.ApproachId == ApproachId)
                    {
                        SetCheckpoint(m_Working, 1);
                        return true;
                    }
                    return false;
                })));
            m_Subscriptions.Add(m_Events.Subscribe<LandingCompleted>(item =>
                Accept(nameof(LandingCompleted), item.DestinationId, () =>
                {
                    if (CheckpointOrdinal == 1 && item.DestinationId.Value ==
                        "destination.koro.surface")
                    {
                        SetCheckpoint(m_Working, 2);
                        return true;
                    }
                    return false;
                })));
            m_Subscriptions.Add(m_Events.Subscribe<TraversalMilestoneReached>(item =>
                Accept(nameof(TraversalMilestoneReached), item.MilestoneId, () =>
                {
                    if (CheckpointOrdinal == 2 && item.MilestoneId.Value ==
                        "route.koro.low-gravity")
                    {
                        SetCheckpoint(m_Working, 3);
                        return true;
                    }
                    return false;
                })));
            m_Subscriptions.Add(m_Events.Subscribe<PhenomenonObserved>(item =>
                Accept(nameof(PhenomenonObserved), item.PhenomenonId, () =>
                {
                    if (CheckpointOrdinal != 3)
                    {
                        return false;
                    }
                    if (item.PhenomenonId.Value == "phenomenon.koro.geyser-natural")
                    {
                        m_NaturalObserved = true;
                        AddDiscovery("sample.koro.geyser-natural");
                    }
                    else if (item.PhenomenonId.Value ==
                             "phenomenon.koro.geyser-signal")
                    {
                        m_SignalObserved = true;
                        AddDiscovery("sample.koro.geyser-signal");
                    }
                    else
                    {
                        return false;
                    }
                    TryCompleteSpectra();
                    return true;
                })));
            m_Subscriptions.Add(m_Events.Subscribe<EvidenceAccepted>(item =>
                Accept(nameof(EvidenceAccepted), item.EvidenceId, () =>
                {
                    if (CheckpointOrdinal != 3 || item.EvidenceId.Value !=
                        "evidence.koro.spectrum-comparison" ||
                        item.PredictionId.Value !=
                        "prediction.koro.water-related-material")
                    {
                        return false;
                    }
                    m_ComparisonAccepted = true;
                    AddDiscovery("evidence.koro.spectrum-comparison");
                    AddAtlas("atlas.koro.geyser-spectra");
                    TryCompleteSpectra();
                    return true;
                })));
            m_Subscriptions.Add(m_Events.Subscribe<InteractionCompleted>(item =>
                Accept(nameof(InteractionCompleted), item.InteractionId, () =>
                {
                    if (CheckpointOrdinal == 4 && item.InteractionId.Value ==
                        "interaction.koro.geyser-rhythm")
                    {
                        SetCheckpoint(m_Working, 5);
                        return true;
                    }
                    return false;
                })));
            m_Subscriptions.Add(m_Events.Subscribe<SignalFragmentRecovered>(item =>
                Accept(nameof(SignalFragmentRecovered), item.FragmentId, () =>
                {
                    if (CheckpointOrdinal == 5 && item.FragmentId == FragmentId)
                    {
                        AddDiscovery(FragmentIdValue);
                        AddDiscovery("chapter.koro-vesper.complete");
                        SetCheckpoint(m_Working, 6);
                        return true;
                    }
                    return false;
                })));
        }

        private void Accept(string kind, ContentId id, Func<bool> mutation)
        {
            if (!m_Initialized || m_Shutdown)
            {
                return;
            }

            var key = kind + ":" + id.Value;
            if (m_UniqueEvents.Contains(key))
            {
                DuplicateEventCount++;
                return;
            }
            if (!mutation())
            {
                return;
            }
            m_UniqueEvents.Add(key);
            m_EventHistory.Add(key);
            m_Dirty = true;
            _ = FlushPendingAsync(CancellationToken.None);
        }

        private void TryCompleteSpectra()
        {
            if (m_NaturalObserved && m_SignalObserved && m_ComparisonAccepted)
            {
                SetCheckpoint(m_Working, 4);
            }
        }

        private void RestorePartialSamples()
        {
            m_NaturalObserved = m_Working.DiscoveryIds.Contains(
                "sample.koro.geyser-natural",
                StringComparer.Ordinal);
            m_SignalObserved = m_Working.DiscoveryIds.Contains(
                "sample.koro.geyser-signal",
                StringComparer.Ordinal);
            m_ComparisonAccepted = m_Working.DiscoveryIds.Contains(
                "evidence.koro.spectrum-comparison",
                StringComparer.Ordinal);
        }

        private void AddDiscovery(string id)
        {
            m_Working.DiscoveryIds = m_Working.DiscoveryIds
                .Append(id)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private void AddAtlas(string id)
        {
            m_Working.AtlasEntryIds = m_Working.AtlasEntryIds
                .Append(id)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static void PreservePriorChapterCompletion(GameSave save)
        {
            if (string.Equals(
                    save.Mission.MissionId,
                    "mission.mirra.chapter-one",
                    StringComparison.Ordinal) &&
                (save.Mission.CompletedNodeIds.Contains(
                     "mission.mirra.complete",
                     StringComparer.Ordinal) || save.Story.CheckpointOrdinal >= 7))
            {
                save.DiscoveryIds = save.DiscoveryIds
                    .Append("chapter.mirra.complete")
                    .Append("fragment.signal.mirra.001")
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
            }
        }

        private static void SetCheckpoint(GameSave save, int ordinal)
        {
            if (ordinal < 0 || ordinal > s_Checkpoints.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(ordinal));
            }

            var completed = s_Checkpoints.Take(ordinal).ToArray();
            save.Mission = new MissionProgress
            {
                MissionId = MissionId,
                CheckpointNodeId = ordinal == 0
                    ? s_Checkpoints[0]
                    : s_Checkpoints[ordinal - 1],
                CheckpointOrdinal = ordinal,
                CompletedNodeIds = completed,
                ActiveNodeIds = ordinal < s_Checkpoints.Length
                    ? new[] { s_Checkpoints[ordinal] }
                    : Array.Empty<string>(),
            };
            save.Story.CheckpointId = save.Mission.CheckpointNodeId;
            save.Story.CheckpointOrdinal = 7 + ordinal;
        }

        private static void ValidateLoadedProgress(MissionProgress mission)
        {
            if (mission.CheckpointOrdinal < 0 ||
                mission.CheckpointOrdinal > s_Checkpoints.Length ||
                mission.CompletedNodeIds == null || mission.ActiveNodeIds == null ||
                mission.CompletedNodeIds.Any(id => !s_Checkpoints.Contains(
                    id, StringComparer.Ordinal)) ||
                mission.ActiveNodeIds.Any(id => !s_Checkpoints.Contains(
                    id, StringComparer.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Saved Koro/Vesper progress is outside the authored route.");
            }
        }

        private void RequireInitialized()
        {
            if (!m_Initialized)
            {
                throw new InvalidOperationException(
                    "Koro/Vesper progression must initialize first.");
            }
        }
    }
}
