using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Atlas;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Crew;
using JustSomeStars.Runtime.Dialogue;
using JustSomeStars.Runtime.Discovery;
using JustSomeStars.Runtime.Flight;
using JustSomeStars.Runtime.Missions;
using JustSomeStars.Runtime.Saving;
using NUnit.Framework;
using UnityEngine;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class FlightDiscoveryAtlasMissionTests
    {
        private string m_Root;
        private GameObject m_LandingObject;

        [SetUp]
        public void SetUp()
        {
            m_Root = Path.Combine(
                Path.GetTempPath(),
                "JssTask18Progression",
                Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (m_LandingObject != null)
            {
                UnityEngine.Object.DestroyImmediate(m_LandingObject);
            }

            if (Directory.Exists(m_Root))
            {
                Directory.Delete(m_Root, recursive: true);
            }
        }

        [Test]
        public async Task RealLandingAndMirraEvidence_CompletePersistReloadIdempotently()
        {
            var content = Resources.Load<Task18ProgressionContent>(
                "Task18ProgressionContent");
            Assert.That(content, Is.Not.Null);
            content.ValidateOrThrow();
            var savePath = Path.Combine(m_Root, "progress.json");
            var saves = new LocalSaveService(savePath);
            await saves.InitializeAsync(CancellationToken.None);
            var bus = new GameEventBus();
            var clock = new FixedDialogueClock();
            var dialogueArbiter = new DialogueTokenArbiter();
            var presenter = new RecordingDialoguePresenter();
            using var dialogue = new DialogueDirector(
                bus,
                dialogueArbiter,
                presenter,
                clock,
                content.Dialogue);
            using var hints = new HintDirector(
                bus,
                dialogue,
                AssistLevel.Guided,
                new[]
                {
                    new HintRule(
                        "mission.mirra.instrument",
                        content.MirraPhenomenon.StableId.Value,
                        content.RequireDialogue("dialogue.mirra.hint"),
                        2),
                });
            using var mission = new MissionDirector(
                content.Mission,
                bus,
                saves,
                GameSave.CreateNew("save.task18.e2e", 100),
                () => 200,
                dialogue,
                content.Dialogue);
            mission.Start();
            using var atlas = new AtlasService(
                bus,
                mission,
                content.AtlasEntries,
                content.ScienceSources,
                content.English);

            m_LandingObject = new GameObject("Task18LandingSequence");
            var landing = m_LandingObject.AddComponent<LandingSequence>();
            SetPrivate(landing, "destinationId", "destination.mirra.surface");
            SetPrivate(landing, "destinationScene", "MirraTask18Fixture");
            var transition = new RecordingTransition();
            landing.Configure(transition, bus);

            Assert.That(
                await landing.TryLandAsync(true, CancellationToken.None),
                Is.True);
            Assert.That(transition.Routes, Is.EqualTo(1));
            Assert.That(
                await mission.FlushCheckpointAsync(CancellationToken.None),
                Is.True);

            Assert.That(
                mission.ActiveNodeIds,
                Does.Contain("mission.mirra.instrument"),
                "Landing must activate and complete its authored arrival dialogue without test orchestration.");
            Assert.That(
                presenter.PresentedIds,
                Is.EqualTo(new[] { "dialogue.mirra.arrival" }),
                "Behavior hints must not play merely because their objective became active.");

            hints.SetObjective(new ContentId("mission.mirra.instrument"));
            bus.Publish(new PlayerBehaviorObserved(
                content.MirraPhenomenon.StableId,
                PlayerBehaviorOutcome.IncorrectPrediction));
            Assert.That(
                presenter.PresentedIds,
                Is.EqualTo(new[]
                {
                    "dialogue.mirra.arrival",
                    "dialogue.mirra.hint",
                }));

            var recorder = new EvidenceRecorder(bus);
            var predictionId = content.PredictionIds.Single();
            Assert.That(
                predictionId.Value,
                Is.EqualTo("prediction.mirra.day-night-circulation"));
            var record = recorder.Record(
                new Prediction(
                    predictionId.Value,
                    content.MirraPhenomenon.StableId.Value,
                    content.MirraPhenomenon.CorrectHypothesisId.Value),
                content.MirraPhenomenon,
                content.MirraInstrument,
                LensMode.Temperature);
            Assert.That(record.PredictionWasCorrect, Is.True);
            Assert.That(mission.HasPendingCheckpoint, Is.True);
            Assert.That(mission.WorkingSave.AtlasEntryIds, Is.EqualTo(new[]
            {
                "atlas.mirra.temperature-gradient",
            }));

            Assert.That(
                await mission.FlushCheckpointAsync(CancellationToken.None),
                Is.True);
            var durable = mission.DurableSave;
            Assert.That(durable.Mission.CheckpointNodeId, Is.EqualTo("mission.mirra.observe"));
            Assert.That(durable.Mission.CompletedNodeIds, Does.Contain("mission.mirra.optional-prediction"));
            Assert.That(durable.DiscoveryIds, Is.EqualTo(new[]
            {
                "phenomenon.mirra.temperature-gradient",
            }));
            Assert.That(durable.AtlasEntryIds, Is.EqualTo(new[]
            {
                "atlas.mirra.temperature-gradient",
            }));

            var reopenedService = new LocalSaveService(savePath);
            await reopenedService.InitializeAsync(CancellationToken.None);
            var reopened = reopenedService.LastLoadResult.Save;
            Assert.That(reopened, Is.EqualTo(durable));
            var bytesBeforeReplay = File.ReadAllBytes(savePath);

            Assert.That(
                await landing.TryLandAsync(true, CancellationToken.None),
                Is.False);
            recorder.Record(
                new Prediction(
                    predictionId.Value,
                    content.MirraPhenomenon.StableId.Value,
                    content.MirraPhenomenon.CorrectHypothesisId.Value),
                content.MirraPhenomenon,
                content.MirraInstrument,
                LensMode.Temperature);
            Assert.That(
                await mission.FlushCheckpointAsync(CancellationToken.None),
                Is.False);
            Assert.That(File.ReadAllBytes(savePath), Is.EqualTo(bytesBeforeReplay));
            Assert.That(mission.DurableSave, Is.EqualTo(durable));

            landing.Release();
            await reopenedService.ShutdownAsync();
            await saves.ShutdownAsync();
        }

        private static void SetPrivate<T>(object target, string fieldName, T value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
            field.SetValue(target, value);
        }

        private sealed class FixedDialogueClock : IDialogueClock
        {
            public double NowSeconds => 1;
        }

        private sealed class RecordingDialoguePresenter : IDialoguePresenter
        {
            public System.Collections.Generic.List<string> PresentedIds { get; } = new();

            public Task PresentAsync(
                DialogueEntry entry,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PresentedIds.Add(entry.StableId.Value);
                return Task.CompletedTask;
            }
        }

        private sealed class RecordingTransition : ISceneTransition
        {
            public int Routes { get; private set; }

            public ValueTask RouteAsync(
                string destination,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Assert.That(destination, Is.EqualTo("MirraTask18Fixture"));
                Routes++;
                return default;
            }
        }
    }
}
