using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Missions;
using JustSomeStars.Runtime.Saving;
using NUnit.Framework;
using UnityEngine;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class MissionGraphTests
    {
        private MissionDefinition m_Mission;

        [TearDown]
        public void TearDown()
        {
            if (m_Mission != null)
            {
                UnityEngine.Object.DestroyImmediate(m_Mission);
            }
        }

        [Test]
        public async Task TypedEvents_AdvanceOnlyExactActiveRequirementsAndReplayIsIdempotent()
        {
            m_Mission = CreateMission(optionalIsCheckpoint: true);
            var save = GameSave.CreateNew("save.mission", 10);
            var storage = new RecordingSaveService();
            var bus = new GameEventBus();
            using var director = new MissionDirector(
                m_Mission,
                bus,
                storage,
                save,
                () => 20);

            director.Start();
            bus.Publish(new LandingCompleted(new ContentId("destination.wrong")));
            bus.Publish(new PhenomenonObserved(new ContentId("phenomenon.mirra.temperature-gradient")));
            Assert.That(director.CompletedNodeIds, Does.Not.Contain("mission.mirra.observe"));

            bus.Publish(new LandingCompleted(new ContentId("destination.mirra")));
            bus.Publish(new LandingCompleted(new ContentId("destination.mirra")));
            bus.Publish(new PhenomenonObserved(new ContentId("phenomenon.mirra.temperature-gradient")));
            bus.Publish(new PhenomenonObserved(new ContentId("phenomenon.mirra.temperature-gradient")));

            Assert.That(director.CompletedNodeIds.Count(id => id == "mission.mirra.land"), Is.EqualTo(1));
            Assert.That(director.CompletedNodeIds.Count(id => id == "mission.mirra.observe"), Is.EqualTo(1));
            Assert.That(director.HasPendingCheckpoint, Is.True);
            Assert.That(await director.FlushCheckpointAsync(CancellationToken.None), Is.True);
            Assert.That(storage.Writes, Is.EqualTo(1));
            Assert.That(director.DurableSave.Mission.CompletedNodeIds, Does.Contain("mission.mirra.observe"));
            Assert.That(director.DurableSave.Metadata.Revision, Is.EqualTo(1));
        }

        [Test]
        public async Task OptionalFork_BeforeOrAfterRequiredPathConvergesToSameDurableState()
        {
            var first = await RunOptionalOrderAsync(optionalFirst: true);
            var second = await RunOptionalOrderAsync(optionalFirst: false);

            Assert.That(first.Mission.CompletedNodeIds, Is.EqualTo(second.Mission.CompletedNodeIds));
            Assert.That(first.Mission.ActiveNodeIds, Is.EqualTo(second.Mission.ActiveNodeIds));
            Assert.That(first.Story.CheckpointId, Is.EqualTo(second.Story.CheckpointId));
            Assert.That(first.Mission.CompletedNodeIds, Does.Contain("mission.mirra.optional-prediction"));
            Assert.That(first.Mission.CompletedNodeIds, Does.Contain("mission.mirra.observe"));
        }

        [Test]
        public async Task RestartRecoveryAndFailedCheckpoint_PreserveExactDurableProgressAndRetry()
        {
            m_Mission = CreateMission(optionalIsCheckpoint: true);
            var storage = new RecordingSaveService { FailNextWrite = true };
            var bus = new GameEventBus();
            using var director = new MissionDirector(
                m_Mission,
                bus,
                storage,
                GameSave.CreateNew("save.recovery", 10),
                () => 30);
            director.Start();
            bus.Publish(new LandingCompleted(new ContentId("destination.mirra")));
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await director.FlushCheckpointAsync(CancellationToken.None));
            Assert.That(director.DurableSave.Metadata.Revision, Is.Zero);
            Assert.That(director.HasPendingCheckpoint, Is.True);

            Assert.That(await director.FlushCheckpointAsync(CancellationToken.None), Is.True);
            bus.Publish(new PredictionRecorded(new ContentId("prediction.mirra")));
            Assert.That(director.WorkingSave.Mission.CompletedNodeIds, Does.Contain("mission.mirra.optional-prediction"));
            Assert.That(await director.FlushCheckpointAsync(CancellationToken.None), Is.True);
            var durable = director.DurableSave;
            Assert.That(durable.Mission.CompletedNodeIds, Does.Contain("mission.mirra.land"));
            Assert.That(durable.Mission.CompletedNodeIds, Does.Contain("mission.mirra.optional-prediction"));

            director.RecoverToAuthoredNode(new ContentId("mission.mirra.observe"));
            Assert.That(
                director.WorkingSave.Mission.CompletedNodeIds,
                Is.EqualTo(durable.Mission.CompletedNodeIds
                    .Where(id => id != "mission.mirra.land")
                    .ToArray()));
            Assert.That(
                director.WorkingSave.Mission.CompletedNodeIds,
                Does.Contain("mission.mirra.optional-prediction"));
            Assert.That(director.WorkingSave.Mission.ActiveNodeIds, Is.EqualTo(new[] { "mission.mirra.land" }));
            Assert.That(director.WorkingSave.DiscoveryIds, Is.EqualTo(durable.DiscoveryIds));

            using var reopened = new MissionDirector(
                m_Mission,
                new GameEventBus(),
                storage,
                durable,
                () => 40);
            reopened.Start();
            Assert.That(reopened.WorkingSave.Mission, Is.EqualTo(durable.Mission));
        }

        [Test]
        public void GraphValidation_RejectsBrokenLinksAmbiguityDeadEndsAndRecoveryCycles()
        {
            var duplicate = ScriptableObject.CreateInstance<MissionDefinition>();
            try
            {
                duplicate.Configure(
                    "mission.broken",
                    "node.entry",
                    new[]
                    {
                        Node("node.entry", MissionNodeKind.Entry, null, new[] { "node.missing" }),
                        Node("node.entry", MissionNodeKind.Terminal, null, Array.Empty<string>()),
                    });
                Assert.Throws<InvalidOperationException>(duplicate.ValidateOrThrow);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(duplicate);
            }
        }

        [Test]
        public async Task CheckpointDrain_SerializesOverlappingRequestsAndPersistsLateAtomicProgress()
        {
            m_Mission = CreateMission(optionalIsCheckpoint: true);
            var storage = new DeferredSaveService();
            var bus = new GameEventBus();
            using var director = new MissionDirector(
                m_Mission,
                bus,
                storage,
                GameSave.CreateNew("save.serial", 10),
                () => 30);
            director.Start();
            bus.Publish(new LandingCompleted(new ContentId("destination.mirra")));

            var first = director.FlushCheckpointAsync(CancellationToken.None);
            bus.Publish(new PredictionRecorded(new ContentId("prediction.mirra")));
            var joined = director.FlushCheckpointAsync(CancellationToken.None);
            Assert.That(joined, Is.SameAs(first));
            Assert.That(storage.MaximumConcurrentWrites, Is.EqualTo(1));
            storage.CompleteNext();
            Assert.That(await first, Is.True);
            Assert.That(director.HasPendingCheckpoint, Is.True);

            var second = director.FlushCheckpointAsync(CancellationToken.None);
            Assert.That(storage.MaximumConcurrentWrites, Is.EqualTo(1));
            storage.CompleteNext();
            Assert.That(await second, Is.True);
            Assert.That(director.DurableSave.Mission.CompletedNodeIds, Does.Contain(
                "mission.mirra.optional-prediction"));
            Assert.That(storage.Writes, Is.EqualTo(2));
        }

        [Test]
        public void PartialLegacyAtlasState_BackfillsMissingPairAtomically()
        {
            m_Mission = CreateMission(optionalIsCheckpoint: true);
            var save = GameSave.CreateNew("save.partial-atlas", 10);
            save.DiscoveryIds = new[] { "phenomenon.mirra.temperature-gradient" };
            using var director = new MissionDirector(
                m_Mission,
                new GameEventBus(),
                new RecordingSaveService(),
                save,
                () => 20);
            director.Start();

            Assert.That(
                director.TryUnlock(
                    new ContentId("phenomenon.mirra.temperature-gradient"),
                    new ContentId("atlas.mirra.temperature-gradient")),
                Is.True);
            Assert.That(director.WorkingSave.DiscoveryIds, Is.EqualTo(new[]
            {
                "phenomenon.mirra.temperature-gradient",
            }));
            Assert.That(director.WorkingSave.AtlasEntryIds, Is.EqualTo(new[]
            {
                "atlas.mirra.temperature-gradient",
            }));
            Assert.That(
                director.TryUnlock(
                    new ContentId("phenomenon.mirra.temperature-gradient"),
                    new ContentId("atlas.mirra.temperature-gradient")),
                Is.False);
        }

        [Test]
        public void Restart_RejectsCheckpointThatIsNotAuthenticToMissionGraphAndStory()
        {
            m_Mission = CreateMission(optionalIsCheckpoint: true);
            var invalid = new[]
            {
                ProgressSave(
                    checkpointId: "mission.mirra.missing",
                    checkpointOrdinal: 1,
                    completed: new[] { "mission.mirra.entry", "mission.mirra.land" },
                    storyId: "mission.mirra.missing",
                    storyOrdinal: 1),
                ProgressSave(
                    checkpointId: "mission.mirra.land",
                    checkpointOrdinal: 2,
                    completed: new[] { "mission.mirra.entry", "mission.mirra.land" },
                    storyId: "mission.mirra.land",
                    storyOrdinal: 2),
                ProgressSave(
                    checkpointId: "mission.mirra.land",
                    checkpointOrdinal: 1,
                    completed: new[] { "mission.mirra.entry" },
                    storyId: "mission.mirra.land",
                    storyOrdinal: 1),
                ProgressSave(
                    checkpointId: "mission.mirra.land",
                    checkpointOrdinal: 1,
                    completed: new[] { "mission.mirra.entry", "mission.mirra.land" },
                    storyId: "story.fabricated",
                    storyOrdinal: 1),
            };

            foreach (var save in invalid)
            {
                using var director = new MissionDirector(
                    m_Mission,
                    new GameEventBus(),
                    new RecordingSaveService(),
                    save,
                    () => 20);
                Assert.Throws<InvalidOperationException>(director.Start);
            }
        }

        private async Task<GameSave> RunOptionalOrderAsync(bool optionalFirst)
        {
            m_Mission = CreateMission(optionalIsCheckpoint: true);
            var bus = new GameEventBus();
            var storage = new RecordingSaveService();
            using var director = new MissionDirector(
                m_Mission,
                bus,
                storage,
                GameSave.CreateNew("save.order", 10),
                () => 50);
            director.Start();
            bus.Publish(new LandingCompleted(new ContentId("destination.mirra")));
            await director.FlushCheckpointAsync(CancellationToken.None);
            if (optionalFirst)
            {
                bus.Publish(new PredictionRecorded(new ContentId("prediction.mirra")));
                bus.Publish(new PhenomenonObserved(new ContentId("phenomenon.mirra.temperature-gradient")));
            }
            else
            {
                bus.Publish(new PhenomenonObserved(new ContentId("phenomenon.mirra.temperature-gradient")));
                bus.Publish(new PredictionRecorded(new ContentId("prediction.mirra")));
            }

            await director.FlushCheckpointAsync(CancellationToken.None);
            return director.DurableSave;
        }

        internal static MissionDefinition CreateMission(bool optionalIsCheckpoint)
        {
            var mission = ScriptableObject.CreateInstance<MissionDefinition>();
            mission.Configure(
                "mission.mirra.task18",
                "mission.mirra.entry",
                new[]
                {
                    Node("mission.mirra.entry", MissionNodeKind.Entry, null, new[] { "mission.mirra.land" }),
                    Node(
                        "mission.mirra.land",
                        MissionNodeKind.Checkpoint,
                        new MissionRequirement(MissionEventKind.LandingCompleted, "destination.mirra"),
                        new[] { "mission.mirra.optional-prediction", "mission.mirra.observe" },
                        checkpointOrdinal: 1),
                    Node(
                        "mission.mirra.optional-prediction",
                        MissionNodeKind.Optional,
                        new MissionRequirement(MissionEventKind.PredictionRecorded, "prediction.mirra"),
                        Array.Empty<string>(),
                        checkpointOrdinal: optionalIsCheckpoint ? 2 : 0),
                    Node(
                        "mission.mirra.observe",
                        MissionNodeKind.Checkpoint,
                        new MissionRequirement(MissionEventKind.PhenomenonObserved, "phenomenon.mirra.temperature-gradient"),
                        new[] { "mission.mirra.terminal" },
                        checkpointOrdinal: 3,
                        recoveryNodeId: "mission.mirra.land"),
                    Node("mission.mirra.terminal", MissionNodeKind.Terminal, null, Array.Empty<string>()),
                });
            mission.ValidateOrThrow();
            return mission;
        }

        private static GameSave ProgressSave(
            string checkpointId,
            int checkpointOrdinal,
            string[] completed,
            string storyId,
            int storyOrdinal)
        {
            var save = GameSave.CreateNew("save.fabricated", 10);
            save.Mission = new MissionProgress
            {
                MissionId = "mission.mirra.task18",
                CheckpointNodeId = checkpointId,
                CheckpointOrdinal = checkpointOrdinal,
                CompletedNodeIds = completed,
                ActiveNodeIds = new[] { "mission.mirra.observe" },
            };
            save.Story.CheckpointId = storyId;
            save.Story.CheckpointOrdinal = storyOrdinal;
            return save;
        }

        private static MissionNode Node(
            string id,
            MissionNodeKind kind,
            MissionRequirement? requirement,
            string[] next,
            int checkpointOrdinal = 0,
            string recoveryNodeId = null)
        {
            return new MissionNode(
                id,
                kind,
                requirement.HasValue ? new[] { requirement.Value } : Array.Empty<MissionRequirement>(),
                next,
                Array.Empty<string>(),
                recoveryNodeId,
                checkpointOrdinal);
        }

        private sealed class RecordingSaveService : ISaveService
        {
            public bool FailNextWrite { get; set; }
            public int Writes { get; private set; }
            public bool IsInitialized => true;
            public ValueTask<StartupResult> InitializeAsync(CancellationToken cancellationToken) =>
                new ValueTask<StartupResult>(StartupResult.Available());
            public ValueTask ShutdownAsync() => default;
            public ValueTask<LoadSaveResult> LoadAsync(CancellationToken cancellationToken) => default;
            public ValueTask<LoadSaveResult> RecoverAsync(CancellationToken cancellationToken) => default;
            public GameSave Merge(GameSave local, GameSave cloud) => SaveMerge.Combine(local, cloud);
            public ValueTask SaveCheckpointAsync(GameSave save, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (FailNextWrite)
                {
                    FailNextWrite = false;
                    throw new InvalidOperationException("fixture write failed");
                }

                Writes++;
                return default;
            }
        }

        private sealed class DeferredSaveService : ISaveService
        {
            private readonly Queue<TaskCompletionSource<bool>> m_Gates = new();
            private int m_Concurrent;
            public int Writes { get; private set; }
            public int MaximumConcurrentWrites { get; private set; }
            public bool IsInitialized => true;
            public ValueTask<StartupResult> InitializeAsync(CancellationToken cancellationToken) =>
                new ValueTask<StartupResult>(StartupResult.Available());
            public ValueTask ShutdownAsync() => default;
            public ValueTask<LoadSaveResult> LoadAsync(CancellationToken cancellationToken) => default;
            public ValueTask<LoadSaveResult> RecoverAsync(CancellationToken cancellationToken) => default;
            public GameSave Merge(GameSave local, GameSave cloud) => SaveMerge.Combine(local, cloud);
            public ValueTask SaveCheckpointAsync(GameSave save, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Writes++;
                m_Concurrent++;
                MaximumConcurrentWrites = Math.Max(MaximumConcurrentWrites, m_Concurrent);
                var gate = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                m_Gates.Enqueue(gate);
                return new ValueTask(WaitAsync(gate.Task));
            }

            public void CompleteNext()
            {
                Assert.That(m_Gates.Count, Is.GreaterThan(0));
                m_Gates.Dequeue().TrySetResult(true);
            }

            private async Task WaitAsync(Task gate)
            {
                try
                {
                    await gate;
                }
                finally
                {
                    m_Concurrent--;
                }
            }
        }
    }
}
