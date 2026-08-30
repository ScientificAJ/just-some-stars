using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Dialogue;
using JustSomeStars.Runtime.Saving;

namespace JustSomeStars.Runtime.Missions
{
    public interface IProgressionStore
    {
        bool TryUnlock(ContentId discoveryId, ContentId atlasEntryId);
    }

    public sealed class MissionDirector : IDisposable, IProgressionStore
    {
        private readonly MissionDefinition m_Definition;
        private readonly GameEventBus m_Events;
        private readonly ISaveService m_Saves;
        private readonly Func<long> m_UtcTicks;
        private readonly IDialogueQueue m_DialogueQueue;
        private readonly Dictionary<ContentId, DialogueEntry> m_DialogueCatalog;
        private readonly List<IDisposable> m_Subscriptions = new List<IDisposable>();
        private readonly object m_SaveGate = new object();
        private readonly Dictionary<string, HashSet<MissionRequirement>> m_Satisfied =
            new Dictionary<string, HashSet<MissionRequirement>>(StringComparer.Ordinal);
        private readonly HashSet<string> m_DialogueRequestedNodes =
            new HashSet<string>(StringComparer.Ordinal);

        private GameSave m_Durable;
        private GameSave m_Working;
        private bool m_Started;
        private bool m_Disposed;
        private bool m_CheckpointPending;
        private Task<bool> m_ActiveFlush;
        private long m_WorkingGeneration;

        public MissionDirector(
            MissionDefinition definition,
            GameEventBus gameEvents,
            ISaveService saves,
            GameSave initialSave,
            Func<long> utcTicks,
            IDialogueQueue dialogueQueue = null,
            IEnumerable<DialogueEntry> dialogueCatalog = null)
        {
            m_Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            m_Events = gameEvents ?? throw new ArgumentNullException(nameof(gameEvents));
            m_Saves = saves ?? throw new ArgumentNullException(nameof(saves));
            m_Durable = (initialSave ?? throw new ArgumentNullException(nameof(initialSave))).Copy();
            m_Working = m_Durable.Copy();
            m_UtcTicks = utcTicks ?? throw new ArgumentNullException(nameof(utcTicks));
            m_Definition.ValidateOrThrow();
            m_DialogueQueue = dialogueQueue;
            var entries = dialogueCatalog?.ToArray() ?? Array.Empty<DialogueEntry>();
            if (entries.Any(entry => entry == null))
            {
                throw new ArgumentException(
                    "Mission dialogue catalog cannot contain null entries.",
                    nameof(dialogueCatalog));
            }

            m_DialogueCatalog = entries
                .GroupBy(entry => entry.StableId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Count() == 1
                        ? group.Single()
                        : throw new ArgumentException(
                            $"Mission dialogue ID '{group.Key}' is duplicated.",
                            nameof(dialogueCatalog)));
            var requiredDialogue = m_Definition.Nodes
                .SelectMany(node => node.DialogueIds)
                .Select(id => new ContentId(id))
                .Distinct()
                .ToArray();
            if (requiredDialogue.Length > 0 && m_DialogueQueue == null)
            {
                throw new ArgumentNullException(
                    nameof(dialogueQueue),
                    "A mission with authored dialogue requires a dialogue queue.");
            }

            foreach (var id in requiredDialogue)
            {
                if (!m_DialogueCatalog.ContainsKey(id))
                {
                    throw new ArgumentException(
                        $"Mission dialogue '{id}' is missing from the injected catalog.",
                        nameof(dialogueCatalog));
                }
            }
        }

        public GameSave WorkingSave => m_Working.Copy();
        public GameSave DurableSave => m_Durable.Copy();
        public IReadOnlyList<string> CompletedNodeIds =>
            m_Working.Mission.CompletedNodeIds.ToArray();
        public IReadOnlyList<string> ActiveNodeIds =>
            m_Working.Mission.ActiveNodeIds.ToArray();
        public bool HasPendingCheckpoint => m_CheckpointPending;

        public void Start()
        {
            ThrowIfDisposed();
            if (m_Started)
            {
                return;
            }

            if (!m_Working.Mission.HasMission)
            {
                m_Working.Mission = new MissionProgress
                {
                    MissionId = m_Definition.StableId.Value,
                    CheckpointNodeId = m_Definition.EntryNodeId.Value,
                    CheckpointOrdinal = 0,
                    CompletedNodeIds = Array.Empty<string>(),
                    ActiveNodeIds = new[] { m_Definition.EntryNodeId.Value },
                };
                m_Working.Story.CheckpointId = m_Definition.EntryNodeId.Value;
                m_Working.Story.CheckpointOrdinal = 0;
            }
            else
            {
                if (!string.Equals(
                        m_Working.Mission.MissionId,
                        m_Definition.StableId.Value,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The loaded mission progress belongs to another mission.");
                }

                ValidateProgressAgainstDefinition(m_Working.Mission);
            }

            m_Subscriptions.Add(m_Events.Subscribe<LandingCompleted>(item =>
                Satisfy(MissionEventKind.LandingCompleted, item.DestinationId)));
            m_Subscriptions.Add(m_Events.Subscribe<PhenomenonObserved>(item =>
                Satisfy(MissionEventKind.PhenomenonObserved, item.PhenomenonId)));
            m_Subscriptions.Add(m_Events.Subscribe<PredictionRecorded>(item =>
                Satisfy(MissionEventKind.PredictionRecorded, item.PredictionId)));
            m_Subscriptions.Add(m_Events.Subscribe<InstrumentUsed>(item =>
                Satisfy(MissionEventKind.InstrumentUsed, item.InstrumentId)));
            m_Subscriptions.Add(m_Events.Subscribe<SignalFragmentRecovered>(item =>
                Satisfy(MissionEventKind.SignalFragmentRecovered, item.FragmentId)));
            m_Subscriptions.Add(m_Events.Subscribe<ConversationCompleted>(item =>
                Satisfy(MissionEventKind.ConversationCompleted, item.ConversationId)));
            m_Subscriptions.Add(m_Events.Subscribe<ApproachCompleted>(item =>
                Satisfy(MissionEventKind.ApproachCompleted, item.ApproachId)));
            m_Subscriptions.Add(m_Events.Subscribe<TraversalMilestoneReached>(item =>
                Satisfy(
                    MissionEventKind.TraversalMilestoneReached,
                    item.MilestoneId)));
            m_Subscriptions.Add(m_Events.Subscribe<ClimateSampleObserved>(item =>
                Satisfy(MissionEventKind.ClimateSampleObserved, item.ZoneId)));
            m_Subscriptions.Add(m_Events.Subscribe<EvidenceAccepted>(item =>
                Satisfy(MissionEventKind.EvidenceAccepted, item.EvidenceId)));
            m_Subscriptions.Add(m_Events.Subscribe<InteractionCompleted>(item =>
                Satisfy(MissionEventKind.InteractionCompleted, item.InteractionId)));
            m_Subscriptions.Add(m_Events.Subscribe<DepartureRequested>(item =>
                Satisfy(MissionEventKind.DepartureRequested, item.DepartureId)));
            m_Subscriptions.Add(m_Events.Subscribe<DepartureCompleted>(item =>
                Satisfy(MissionEventKind.DepartureCompleted, item.DepartureId)));
            m_Started = true;
            ProcessAutomaticNodes();
            RequestDialogueForActiveNodes();
        }

        public Task<bool> FlushCheckpointAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            lock (m_SaveGate)
            {
                if (m_ActiveFlush != null && !m_ActiveFlush.IsCompleted)
                {
                    return m_ActiveFlush;
                }

                m_ActiveFlush = null;
                if (!m_CheckpointPending)
                {
                    return Task.FromResult(false);
                }

                cancellationToken.ThrowIfCancellationRequested();
                var candidate = m_Working.Copy();
                candidate.Metadata.Revision = Increment(candidate.Metadata.Revision);
                var now = m_UtcTicks();
                if (now < candidate.Metadata.UpdatedUtcTicks)
                {
                    throw new InvalidOperationException(
                        "Checkpoint time cannot move backwards.");
                }

                candidate.Metadata.UpdatedUtcTicks = now;
                m_ActiveFlush = FlushCoreAsync(
                    candidate,
                    m_WorkingGeneration,
                    cancellationToken);
                return m_ActiveFlush;
            }
        }

        public async Task ShutdownAsync()
        {
            Task<bool> active;
            lock (m_SaveGate)
            {
                active = m_ActiveFlush;
            }

            if (active != null)
            {
                await active;
            }

            Dispose();
        }

        private async Task<bool> FlushCoreAsync(
            GameSave candidate,
            long candidateGeneration,
            CancellationToken cancellationToken)
        {
            await m_Saves.SaveCheckpointAsync(candidate, cancellationToken);
            lock (m_SaveGate)
            {
                m_Durable = candidate.Copy();
                m_Working.Metadata = candidate.Metadata.Copy();
                m_CheckpointPending = m_WorkingGeneration != candidateGeneration;
            }

            return true;
        }

        public void RecoverToAuthoredNode(ContentId failedNodeId)
        {
            ThrowIfDisposed();
            var failed = m_Definition.RequireNode(failedNodeId);
            if (!failed.HasRecoveryNode)
            {
                throw new InvalidOperationException(
                    $"Mission node '{failedNodeId}' has no authored recovery target.");
            }

            var target = failed.RecoveryNodeId.Value;
            m_Working = m_Durable.Copy();
            m_Working.Mission.CompletedNodeIds = m_Working.Mission.CompletedNodeIds
                .Where(id => !string.Equals(id, target, StringComparison.Ordinal))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            m_Working.Mission.ActiveNodeIds = new[] { target };
            m_Satisfied.Clear();
            m_DialogueRequestedNodes.Clear();
            m_CheckpointPending = false;
            m_WorkingGeneration++;
            RequestDialogueForActiveNodes();
            m_Events.Publish(new PlayerBehaviorObserved(
                failedNodeId,
                PlayerBehaviorOutcome.RecoveryRequested));
        }

        public bool TryUnlock(ContentId discoveryId, ContentId atlasEntryId)
        {
            ThrowIfDisposed();
            if (!discoveryId.IsValid || !atlasEntryId.IsValid)
            {
                throw new ArgumentException("Progression unlocks require valid content IDs.");
            }

            var discoveries = new HashSet<string>(
                m_Working.DiscoveryIds,
                StringComparer.Ordinal);
            var atlas = new HashSet<string>(
                m_Working.AtlasEntryIds,
                StringComparer.Ordinal);
            var addedDiscovery = discoveries.Add(discoveryId.Value);
            var addedAtlas = atlas.Add(atlasEntryId.Value);
            if (!addedDiscovery && !addedAtlas)
            {
                return false;
            }

            m_Working.DiscoveryIds = discoveries
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            m_Working.AtlasEntryIds = atlas
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            m_WorkingGeneration++;
            return true;
        }

        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }

            lock (m_SaveGate)
            {
                if (m_ActiveFlush != null && !m_ActiveFlush.IsCompleted)
                {
                    throw new InvalidOperationException(
                        "Await ShutdownAsync before disposing active progression persistence.");
                }
            }

            foreach (var subscription in m_Subscriptions)
            {
                subscription.Dispose();
            }

            m_Subscriptions.Clear();
            m_Disposed = true;
        }

        private void Satisfy(MissionEventKind eventKind, ContentId payloadId)
        {
            if (!m_Started || m_Disposed)
            {
                return;
            }

            var active = m_Working.Mission.ActiveNodeIds.ToArray();
            foreach (var id in active.OrderBy(value => value, StringComparer.Ordinal))
            {
                var node = m_Definition.RequireNode(new ContentId(id));
                var matching = node.Requirements
                    .Where(requirement =>
                        requirement.EventKind == eventKind &&
                        requirement.PayloadId == payloadId)
                    .ToArray();
                if (matching.Length == 0)
                {
                    continue;
                }

                if (!m_Satisfied.TryGetValue(id, out var satisfied))
                {
                    satisfied = new HashSet<MissionRequirement>();
                    m_Satisfied.Add(id, satisfied);
                }

                foreach (var requirement in matching)
                {
                    satisfied.Add(requirement);
                }

                if (node.Requirements.All(satisfied.Contains))
                {
                    CompleteNode(node);
                }
            }
        }

        private void CompleteNode(MissionNode node)
        {
            var completed = new HashSet<string>(
                m_Working.Mission.CompletedNodeIds,
                StringComparer.Ordinal);
            if (!completed.Add(node.StableId.Value))
            {
                return;
            }

            var active = new HashSet<string>(
                m_Working.Mission.ActiveNodeIds,
                StringComparer.Ordinal);
            active.Remove(node.StableId.Value);
            foreach (var next in node.NextNodeIds)
            {
                if (!completed.Contains(next))
                {
                    active.Add(next);
                }
            }

            m_Working.Mission.CompletedNodeIds = completed
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            m_Working.Mission.ActiveNodeIds = active
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            m_Satisfied.Remove(node.StableId.Value);
            m_WorkingGeneration++;
            if (node.IsSafeCheckpoint)
            {
                if (node.CheckpointOrdinal > m_Working.Mission.CheckpointOrdinal)
                {
                    m_Working.Mission.CheckpointNodeId = node.StableId.Value;
                    m_Working.Mission.CheckpointOrdinal = node.CheckpointOrdinal;
                    m_Working.Story.CheckpointId = node.StableId.Value;
                    m_Working.Story.CheckpointOrdinal = node.CheckpointOrdinal;
                }

                m_CheckpointPending = true;
            }

            ProcessAutomaticNodes();
            RequestDialogueForActiveNodes();
        }

        private void ProcessAutomaticNodes()
        {
            while (true)
            {
                var automatic = m_Working.Mission.ActiveNodeIds
                    .Select(id => m_Definition.RequireNode(new ContentId(id)))
                    .Where(node => node.Requirements.Count == 0)
                    .OrderBy(node => node.StableId.Value, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (automatic == null)
                {
                    return;
                }

                CompleteNodeWithoutRecursion(automatic);
            }
        }

        private void CompleteNodeWithoutRecursion(MissionNode node)
        {
            var completed = new HashSet<string>(
                m_Working.Mission.CompletedNodeIds,
                StringComparer.Ordinal) { node.StableId.Value };
            var active = new HashSet<string>(
                m_Working.Mission.ActiveNodeIds,
                StringComparer.Ordinal);
            active.Remove(node.StableId.Value);
            foreach (var next in node.NextNodeIds)
            {
                if (!completed.Contains(next))
                {
                    active.Add(next);
                }
            }

            m_Working.Mission.CompletedNodeIds = completed
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            m_Working.Mission.ActiveNodeIds = active
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            m_WorkingGeneration++;
        }

        private void ValidateProgressAgainstDefinition(MissionProgress progress)
        {
            MissionNode checkpoint;
            try
            {
                foreach (var id in progress.CompletedNodeIds.Concat(progress.ActiveNodeIds))
                {
                    _ = m_Definition.RequireNode(new ContentId(id));
                }

                checkpoint = m_Definition.RequireNode(
                    new ContentId(progress.CheckpointNodeId));
            }
            catch (KeyNotFoundException exception)
            {
                throw new InvalidOperationException(
                    "Loaded mission progress references a node outside the authored graph.",
                    exception);
            }

            if (checkpoint.CheckpointOrdinal != progress.CheckpointOrdinal ||
                !progress.CompletedNodeIds.Contains(
                    checkpoint.StableId.Value,
                    StringComparer.Ordinal) ||
                !string.Equals(
                    m_Working.Story.CheckpointId,
                    checkpoint.StableId.Value,
                    StringComparison.Ordinal) ||
                m_Working.Story.CheckpointOrdinal != progress.CheckpointOrdinal)
            {
                throw new InvalidOperationException(
                    "Loaded mission checkpoint is not authentic to the authored graph and story state.");
            }
        }

        private void RequestDialogueForActiveNodes()
        {
            foreach (var node in m_Working.Mission.ActiveNodeIds
                         .Select(id => m_Definition.RequireNode(new ContentId(id)))
                         .OrderBy(node => node.StableId.Value, StringComparer.Ordinal))
            {
                if (node.DialogueIds.Count == 0 ||
                    !m_DialogueRequestedNodes.Add(node.StableId.Value))
                {
                    continue;
                }

                foreach (var dialogueId in node.DialogueIds)
                {
                    m_DialogueQueue.Enqueue(
                        m_DialogueCatalog[new ContentId(dialogueId)]);
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (m_Disposed)
            {
                throw new ObjectDisposedException(nameof(MissionDirector));
            }
        }

        private static long Increment(long value)
        {
            if (value == long.MaxValue)
            {
                throw new InvalidOperationException("Save revision is exhausted.");
            }

            return value + 1;
        }
    }
}
