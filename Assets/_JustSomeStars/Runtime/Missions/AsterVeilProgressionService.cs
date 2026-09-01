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
    public sealed class AsterVeilProgressionService : IChapterProgression
    {
        public const string ResourceName = "Task26AsterVeilChapter";
        public const string MissionId = "mission.aster-veil.chapter-one";
        public const string FlightSceneName = "AsterVeil";
        public const string ReconstructionSceneName = "SignalReassembly";
        public const string ClubhouseSceneName = "Clubhouse";
        public const string DinnerSceneName = "DinnerEnding";
        public const string FragmentIdValue = "fragment.signal.aster.003";

        private static readonly string[] s_Checkpoints =
        {
            "mission.aster-veil.approach",
            "mission.aster-veil.route",
            "mission.aster-veil.relative-motion",
            "mission.aster-veil.debris",
            "mission.aster-veil.fragment",
            "mission.aster-veil.reassembly",
            "mission.aster-veil.escape",
            "mission.aster-veil.return",
            "mission.aster-veil.dinner",
        };

        private readonly GameEventBus m_Events;
        private readonly ISaveService m_Saves;
        private readonly SettingsService m_Settings;
        private readonly List<IDisposable> m_Subscriptions = new();
        private readonly HashSet<string> m_UniqueEvents = new(StringComparer.Ordinal);
        private readonly object m_StateGate = new();
        private readonly object m_FlushGate = new();

        private AsterVeilChapterContent m_Content;
        private GameSave m_Working;
        private GameSave m_Durable;
        private Task m_ActiveFlush = Task.CompletedTask;
        private long m_MutationVersion;
        private bool m_Dirty;
        private bool m_Initialized;
        private bool m_Shutdown;

        public AsterVeilProgressionService(
            GameEventBus gameEvents,
            ISaveService saves,
            SettingsService settings)
        {
            m_Events = gameEvents ?? throw new ArgumentNullException(nameof(gameEvents));
            m_Saves = saves ?? throw new ArgumentNullException(nameof(saves));
            m_Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public AsterVeilChapterContent Content => m_Content;
        public ContentId ChapterId => new(MissionId);
        public ContentId ApproachId => new("approach.aster.gravity-route");
        public ContentId FragmentId => new(FragmentIdValue);
        public int CheckpointOrdinal => m_Working?.Mission.CheckpointOrdinal ?? 0;
        public bool HasPendingDeparture => false;
        public bool IsMissionComplete => m_Working?.ChapterOne.CreditsUnlocked == true;
        public bool CreditsUnlocked => IsMissionComplete;
        public int DuplicateEventCount { get; private set; }

        public string ResumeSceneName => CheckpointOrdinal switch
        {
            <= 4 => FlightSceneName,
            5 => ReconstructionSceneName,
            6 => FlightSceneName,
            7 => ClubhouseSceneName,
            _ => DinnerSceneName,
        };

        public GameMode ResumeMode => CheckpointOrdinal switch
        {
            <= 6 => GameMode.Flight,
            _ => GameMode.Clubhouse,
        };

        public async ValueTask<StartupResult> InitializeAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (m_Initialized) return StartupResult.Available();
            if (m_Shutdown)
            {
                throw new InvalidOperationException(
                    "Aster Veil progression cannot initialize after shutdown.");
            }

            m_Content = Resources.Load<AsterVeilChapterContent>(ResourceName);
            if (m_Content == null)
            {
                return StartupResult.Failed(
                    $"Aster Veil chapter resource '{ResourceName}' is missing.");
            }
            m_Content.ValidateOrThrow();

            var loaded = await m_Saves.LoadAsync(cancellationToken);
            m_Working = loaded.HasSave
                ? loaded.Save
                : GameSave.CreateNew("save.aster.local", DateTime.UtcNow.Ticks);
            if (!string.Equals(
                    m_Working.Mission.MissionId,
                    MissionId,
                    StringComparison.Ordinal))
            {
                if (!IsKoroComplete(m_Working))
                {
                    return StartupResult.Failed(
                        "Aster Veil requires the two earlier Signal fragments.");
                }
                PreservePriorChapters(m_Working);
                SetChapterPhase(m_Working, ChapterOnePhase.KoroComplete);
                SetCheckpoint(m_Working, 0);
            }
            else
            {
                ValidateLoadedProgress(m_Working);
            }

            m_Durable = m_Working.Copy();
            BindEvents();
            m_Initialized = true;
            return StartupResult.Available();
        }

        public async ValueTask ShutdownAsync()
        {
            if (m_Shutdown) return;
            m_Shutdown = true;
            await FlushPendingAsync(CancellationToken.None);
            foreach (var subscription in m_Subscriptions)
            {
                subscription.Dispose();
            }
            m_Subscriptions.Clear();
            m_Initialized = false;
        }

        public bool IsActiveNode(string nodeId) =>
            m_Initialized && !IsMissionComplete &&
            string.Equals(CurrentObjectiveId(), nodeId, StringComparison.Ordinal);

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
                if (m_ActiveFlush.IsCompleted)
                {
                    m_ActiveFlush = FlushLoopAsync(cancellationToken);
                }
                return m_ActiveFlush;
            }
        }

        public async Task CompleteFinalPulseAndUnlockCreditsAsync(
            Action presentCredits,
            CancellationToken cancellationToken)
        {
            if (presentCredits == null)
            {
                throw new ArgumentNullException(nameof(presentCredits));
            }
            RequireInitialized();

            lock (m_StateGate)
            {
                if (CheckpointOrdinal < 9)
                {
                    throw new InvalidOperationException(
                        "Dinner must finish before the final Signal pulse.");
                }
                RequireThreeFragments(m_Working);
                SetChapterPhase(m_Working, ChapterOnePhase.DinnerComplete);
                m_Working.ChapterOne.FinalPulseSeen = true;
                AddDiscovery(m_Working, "chapter.one.complete");
                AddDiscovery(m_Working, "chapter.two.signal-pulse");
                MarkDirty();
            }

            await FlushPendingAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            presentCredits();
        }

        private async Task FlushLoopAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                GameSave candidate;
                long version;
                lock (m_StateGate)
                {
                    if (!m_Dirty) return;
                    version = m_MutationVersion;
                    candidate = m_Working.Copy();
                    candidate.Metadata.Revision = checked(
                        candidate.Metadata.Revision + 1);
                    candidate.Metadata.UpdatedUtcTicks = Math.Max(
                        candidate.Metadata.UpdatedUtcTicks + 1,
                        DateTime.UtcNow.Ticks);
                }

                cancellationToken.ThrowIfCancellationRequested();
                await m_Saves.SaveCheckpointAsync(candidate, cancellationToken);
                lock (m_StateGate)
                {
                    m_Durable = candidate.Copy();
                    m_Working.Metadata = candidate.Metadata.Copy();
                    if (version == m_MutationVersion)
                    {
                        m_Dirty = false;
                        return;
                    }
                }
            }
        }

        private void BindEvents()
        {
            m_Subscriptions.Add(m_Events.Subscribe<ApproachCompleted>(item =>
                Accept(nameof(ApproachCompleted), item.ApproachId, () =>
                    CheckpointOrdinal == 0 && item.ApproachId == ApproachId,
                    1)));
            m_Subscriptions.Add(m_Events.Subscribe<InteractionCompleted>(item =>
                Accept(nameof(InteractionCompleted), item.InteractionId, () =>
                {
                    if (CheckpointOrdinal == 1 && item.InteractionId.Value ==
                        "interaction.aster.route-committed")
                    {
                        SetCheckpoint(m_Working, 2);
                        return true;
                    }
                    if (CheckpointOrdinal == 5 && item.InteractionId.Value ==
                        "interaction.signal.reassemble")
                    {
                        RequireThreeFragments(m_Working);
                        SetChapterPhase(
                            m_Working,
                            ChapterOnePhase.SignalReassembled);
                        m_Working.ChapterOne.StarMapRevealed = true;
                        AddDiscovery(m_Working, "map.signal.beyond-aurelia");
                        AddDiscovery(m_Working, "signal.pulse.recent");
                        AddAtlas(m_Working, "atlas.signal.map-beyond-aurelia");
                        SetCheckpoint(m_Working, 6);
                        return true;
                    }
                    return false;
                })));
            m_Subscriptions.Add(m_Events.Subscribe<PhenomenonObserved>(item =>
                Accept(nameof(PhenomenonObserved), item.PhenomenonId, () =>
                    CheckpointOrdinal == 2 && item.PhenomenonId.Value ==
                        "phenomenon.aster.relative-motion", 3)));
            m_Subscriptions.Add(m_Events.Subscribe<TraversalMilestoneReached>(item =>
                Accept(nameof(TraversalMilestoneReached), item.MilestoneId, () =>
                    CheckpointOrdinal == 3 && item.MilestoneId.Value ==
                        "route.aster.debris-lane-cleared", 4)));
            m_Subscriptions.Add(m_Events.Subscribe<SignalFragmentRecovered>(item =>
                Accept(nameof(SignalFragmentRecovered), item.FragmentId, () =>
                {
                    if (CheckpointOrdinal != 4 || item.FragmentId != FragmentId)
                    {
                        return false;
                    }
                    AddDiscovery(m_Working, FragmentIdValue);
                    SetChapterPhase(
                        m_Working,
                        ChapterOnePhase.AsterFragmentRecovered);
                    SetCheckpoint(m_Working, 5);
                    return true;
                })));
            m_Subscriptions.Add(m_Events.Subscribe<DepartureCompleted>(item =>
                Accept(nameof(DepartureCompleted), item.DepartureId, () =>
                    CheckpointOrdinal == 6 && item.DepartureId.Value ==
                        "departure.aster.escape", 7)));
            m_Subscriptions.Add(m_Events.Subscribe<LandingCompleted>(item =>
                Accept(nameof(LandingCompleted), item.DestinationId, () =>
                {
                    if (CheckpointOrdinal != 7 || item.DestinationId.Value !=
                        "destination.clubhouse.return")
                    {
                        return false;
                    }
                    SetChapterPhase(m_Working, ChapterOnePhase.ReturnedHome);
                    SetCheckpoint(m_Working, 8);
                    return true;
                })));
            m_Subscriptions.Add(m_Events.Subscribe<ConversationCompleted>(item =>
                Accept(nameof(ConversationCompleted), item.ConversationId, () =>
                    CheckpointOrdinal == 8 && item.ConversationId.Value ==
                        "conversation.dinner.just-some-stars", 9)));
        }

        private void Accept(
            string kind,
            ContentId id,
            Func<bool> mutation,
            int checkpoint = -1)
        {
            if (!m_Initialized || m_Shutdown) return;
            var key = kind + ":" + id.Value;
            lock (m_StateGate)
            {
                if (m_UniqueEvents.Contains(key))
                {
                    DuplicateEventCount++;
                    return;
                }
                var changed = mutation();
                if (changed && checkpoint >= 0)
                {
                    SetCheckpoint(m_Working, checkpoint);
                }
                if (!changed) return;
                m_UniqueEvents.Add(key);
                MarkDirty();
            }
            _ = FlushPendingAsync(CancellationToken.None);
        }

        private void MarkDirty()
        {
            m_Dirty = true;
            m_MutationVersion = checked(m_MutationVersion + 1);
        }

        private string CurrentObjectiveId()
        {
            var ordinal = CheckpointOrdinal;
            return ordinal >= s_Checkpoints.Length
                ? s_Checkpoints[^1]
                : s_Checkpoints[ordinal];
        }

        private static void SetCheckpoint(GameSave save, int ordinal)
        {
            if (ordinal < 0 || ordinal > s_Checkpoints.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(ordinal));
            }
            save.Mission = new MissionProgress
            {
                MissionId = MissionId,
                CheckpointNodeId = ordinal == 0
                    ? s_Checkpoints[0]
                    : s_Checkpoints[ordinal - 1],
                CheckpointOrdinal = ordinal,
                CompletedNodeIds = s_Checkpoints.Take(ordinal).ToArray(),
                ActiveNodeIds = ordinal < s_Checkpoints.Length
                    ? new[] { s_Checkpoints[ordinal] }
                    : Array.Empty<string>(),
            };
            save.Story.CheckpointId = save.Mission.CheckpointNodeId;
            save.Story.CheckpointOrdinal = 13 + ordinal;
        }

        private static void SetChapterPhase(GameSave save, ChapterOnePhase phase)
        {
            if (phase < save.ChapterOne.Phase) return;
            save.ChapterOne.Phase = phase;
            if (phase >= ChapterOnePhase.SignalReassembled)
            {
                save.ChapterOne.StarMapRevealed = true;
            }
            if (phase == ChapterOnePhase.DinnerComplete)
            {
                save.ChapterOne.FinalPulseSeen = true;
            }
        }

        private static bool IsKoroComplete(GameSave save) =>
            save.DiscoveryIds.Contains(
                KoroVesperProgressionService.FragmentIdValue,
                StringComparer.Ordinal) ||
            (string.Equals(
                 save.Mission.MissionId,
                 KoroVesperProgressionService.MissionId,
                 StringComparison.Ordinal) &&
             save.Mission.CheckpointOrdinal >= 6);

        private static void PreservePriorChapters(GameSave save)
        {
            AddDiscovery(save, "chapter.mirra.complete");
            AddDiscovery(save, "fragment.signal.mirra.001");
            AddDiscovery(save, "chapter.koro-vesper.complete");
            AddDiscovery(save, KoroVesperProgressionService.FragmentIdValue);
        }

        private static void RequireThreeFragments(GameSave save)
        {
            var required = new[]
            {
                "fragment.signal.mirra.001",
                KoroVesperProgressionService.FragmentIdValue,
                FragmentIdValue,
            };
            if (required.Any(id => !save.DiscoveryIds.Contains(
                    id, StringComparer.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Signal reconstruction requires all three unique fragments.");
            }
        }

        private static void AddDiscovery(GameSave save, string id)
        {
            save.DiscoveryIds = save.DiscoveryIds
                .Append(id)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static void AddAtlas(GameSave save, string id)
        {
            save.AtlasEntryIds = save.AtlasEntryIds
                .Append(id)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static void ValidateLoadedProgress(GameSave save)
        {
            var mission = save.Mission;
            if (mission.CheckpointOrdinal < 0 ||
                mission.CheckpointOrdinal > s_Checkpoints.Length ||
                mission.CompletedNodeIds == null || mission.ActiveNodeIds == null ||
                mission.CompletedNodeIds.Any(id => !s_Checkpoints.Contains(
                    id, StringComparer.Ordinal)) ||
                mission.ActiveNodeIds.Any(id => !s_Checkpoints.Contains(
                    id, StringComparer.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Saved Aster Veil progress is outside the authored route.");
            }
            if (mission.CheckpointOrdinal >= 5)
            {
                RequireThreeFragments(save);
            }
        }

        private void RequireInitialized()
        {
            if (!m_Initialized)
            {
                throw new InvalidOperationException(
                    "Aster Veil progression must initialize first.");
            }
        }
    }
}
