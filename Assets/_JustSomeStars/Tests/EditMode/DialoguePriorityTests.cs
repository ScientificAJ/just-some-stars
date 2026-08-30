using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Crew;
using JustSomeStars.Runtime.Dialogue;
using JustSomeStars.Runtime.Discovery;
using JustSomeStars.Runtime.Rendering2D;
using NUnit.Framework;
using UnityEngine;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class DialoguePriorityTests
    {
        private readonly List<UnityEngine.Object> m_Owned = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in m_Owned)
            {
                UnityEngine.Object.DestroyImmediate(item);
            }

            m_Owned.Clear();
        }

        [Test]
        public async Task SharedCrewArbiter_PreemptsInterruptibleLineCancelsPresenterAndCompletesOnlyReplacement()
        {
            var bus = new GameEventBus();
            var arbiter = new DialogueTokenArbiter();
            var presenter = new ControlledPresenter();
            var clock = new FakeClock();
            var ambient = Entry("dialogue.ambient", DialoguePriority.Ambient, true, 10);
            var story = Entry("dialogue.story", DialoguePriority.Story, false, 10);
            var completed = new List<string>();
            using var subscription = bus.Subscribe<ConversationCompleted>(item =>
                completed.Add(item.ConversationId.Value));
            using var director = new DialogueDirector(
                bus,
                arbiter,
                presenter,
                clock,
                new[] { ambient, story });

            var ambientRun = director.RequestAsync(ambient, CancellationToken.None);
            await presenter.WaitUntilStartedAsync("dialogue.ambient");
            var storyRun = director.RequestAsync(story, CancellationToken.None);
            await presenter.WaitUntilCancelledAsync("dialogue.ambient");
            presenter.Complete("dialogue.story");

            Assert.That(await ambientRun, Is.EqualTo(DialogueOutcome.Interrupted));
            Assert.That(await storyRun, Is.EqualTo(DialogueOutcome.Completed));
            Assert.That(completed, Is.EqualTo(new[] { "dialogue.story" }));
            Assert.That(arbiter.ActiveTokenCount, Is.Zero);
        }

        [Test]
        public async Task CooldownStartsAtPresentationStartAndFaultsReleaseTokenWithoutFollowup()
        {
            var entry = Entry("dialogue.cooldown", DialoguePriority.Personality, true, 5);
            var followup = Entry("dialogue.followup", DialoguePriority.Ambient, true, 0);
            entry.ConfigureFollowups(new[] { followup.StableId.Value });
            var clock = new FakeClock();
            var presenter = new ImmediatePresenter();
            var arbiter = new DialogueTokenArbiter();
            using var director = new DialogueDirector(
                new GameEventBus(),
                arbiter,
                presenter,
                clock,
                new[] { entry, followup });

            Assert.That(await director.RequestAsync(entry, CancellationToken.None), Is.EqualTo(DialogueOutcome.Completed));
            Assert.That(await director.RequestAsync(entry, CancellationToken.None), Is.EqualTo(DialogueOutcome.Cooldown));
            clock.Now = 5;
            presenter.ThrowNext = true;
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await director.RequestAsync(entry, CancellationToken.None));
            Assert.That(arbiter.ActiveTokenCount, Is.Zero);
        }

        [Test]
        public async Task FollowupChain_HoldsOneConversationOpenAndCompletesOnlyAfterTerminalLine()
        {
            var bus = new GameEventBus();
            var root = Entry("dialogue.chain.root", DialoguePriority.Story, false, 0);
            var terminal = Entry("dialogue.chain.terminal", DialoguePriority.Story, false, 0);
            root.ConfigureFollowups(new[] { terminal.StableId.Value });
            var presenter = new ControlledPresenter();
            var completed = new List<string>();
            using var subscription = bus.Subscribe<ConversationCompleted>(item =>
                completed.Add(item.ConversationId.Value));
            using var director = new DialogueDirector(
                bus,
                new DialogueTokenArbiter(),
                presenter,
                new FakeClock(),
                new[] { root, terminal });

            var run = director.RequestAsync(root, CancellationToken.None);
            await presenter.WaitUntilStartedAsync(root.StableId.Value);
            presenter.Complete(root.StableId.Value);
            await Task.Yield();

            Assert.That(run.IsCompleted, Is.False,
                "A conversation cannot complete while its authored follow-up is pending.");
            Assert.That(completed, Is.Empty);
            await presenter.WaitUntilStartedAsync(terminal.StableId.Value);
            presenter.Complete(terminal.StableId.Value);

            Assert.That(await run, Is.EqualTo(DialogueOutcome.Completed));
            Assert.That(completed, Is.EqualTo(new[] { root.StableId.Value }));
        }

        [TestCase(AssistLevel.Guided, 1)]
        [TestCase(AssistLevel.Balanced, 2)]
        [TestCase(AssistLevel.Ace, int.MaxValue)]
        public void Hints_UseMeaningfulObjectiveScopedAttemptsNeverElapsedTime(
            AssistLevel assist,
            int threshold)
        {
            var bus = new GameEventBus();
            var queue = new RecordingDialogueQueue();
            var hint = Entry("dialogue.hint.mirra", DialoguePriority.Hint, true, 0);
            using var director = new HintDirector(
                bus,
                queue,
                assist,
                new[] { new HintRule("mission.mirra.observe", hint, threshold) });
            director.SetObjective(new ContentId("mission.mirra.observe"));

            director.TickElapsedSeconds(9999);
            Assert.That(queue.Entries, Is.Empty);
            if (threshold != int.MaxValue)
            {
                for (var attempt = 0; attempt < threshold; attempt++)
                {
                    bus.Publish(new PlayerBehaviorObserved(
                        new ContentId("mission.mirra.observe"),
                        PlayerBehaviorOutcome.IncorrectPrediction));
                }

                Assert.That(queue.Entries, Is.EqualTo(new[] { hint }));
            }

            director.CompleteObjective(new ContentId("mission.mirra.observe"));
            bus.Publish(new PlayerBehaviorObserved(
                new ContentId("mission.mirra.observe"),
                PlayerBehaviorOutcome.RecoveryRequested));
            Assert.That(queue.Entries.Count, Is.EqualTo(threshold == int.MaxValue ? 0 : 1));
        }

        [Test]
        public void RealIncorrectEvidenceOutcome_TriggersMappedGuidedHintWithoutAdvancingStory()
        {
            var bus = new GameEventBus();
            var queue = new RecordingDialogueQueue();
            var hint = Entry("dialogue.hint.evidence", DialoguePriority.Hint, true, 0);
            var phenomenon = ScriptableObject.CreateInstance<PhenomenonDefinition>();
            phenomenon.Configure(
                "phenomenon.hint.temperature",
                "science-source.hint",
                LayerBand.Gameplay,
                LensFocusBehavior.Point,
                new[] { LensMode.Temperature },
                "hypothesis.hint.correct",
                "hint.guided",
                "hint.deep",
                0.5f);
            var instrument = ScriptableObject.CreateInstance<InstrumentDefinition>();
            instrument.Configure(
                "instrument.hint.thermal",
                new[] { LensMode.Temperature },
                0.25f);
            m_Owned.Add(phenomenon);
            m_Owned.Add(instrument);
            using var hints = new HintDirector(
                bus,
                queue,
                AssistLevel.Guided,
                new[]
                {
                    new HintRule(
                        "mission.hint.observe",
                        phenomenon.StableId.Value,
                        hint,
                        1),
                });
            hints.SetObjective(new ContentId("mission.hint.observe"));
            var missionCompletions = 0;
            using var completion = bus.Subscribe<ConversationCompleted>(_ => missionCompletions++);
            var recorder = new EvidenceRecorder(bus);

            recorder.Record(
                new Prediction(
                    "prediction.hint.wrong",
                    phenomenon.StableId.Value,
                    "hypothesis.hint.wrong"),
                phenomenon,
                instrument,
                LensMode.Temperature);

            Assert.That(queue.Entries, Is.EqualTo(new[] { hint }));
            Assert.That(missionCompletions, Is.Zero);
        }

        private DialogueEntry Entry(
            string id,
            DialoguePriority priority,
            bool interruptible,
            double cooldown)
        {
            var entry = ScriptableObject.CreateInstance<DialogueEntry>();
            entry.Configure(
                id,
                "loc." + id,
                "crew.mira",
                "voice." + id,
                "curious",
                "focused",
                "gesture.small",
                Array.Empty<string>(),
                priority,
                interruptible,
                cooldown,
                Array.Empty<string>());
            m_Owned.Add(entry);
            return entry;
        }

        private sealed class FakeClock : IDialogueClock
        {
            public double Now { get; set; }
            public double NowSeconds => Now;
        }

        private sealed class ImmediatePresenter : IDialoguePresenter
        {
            public bool ThrowNext { get; set; }
            public Task PresentAsync(DialogueEntry entry, CancellationToken cancellationToken)
            {
                if (ThrowNext)
                {
                    ThrowNext = false;
                    throw new InvalidOperationException("fixture presenter fault");
                }

                return Task.CompletedTask;
            }
        }

        private sealed class ControlledPresenter : IDialoguePresenter
        {
            private readonly Dictionary<string, TaskCompletionSource<bool>> m_Started = new();
            private readonly Dictionary<string, TaskCompletionSource<bool>> m_Completed = new();
            private readonly Dictionary<string, TaskCompletionSource<bool>> m_Cancelled = new();

            public async Task PresentAsync(DialogueEntry entry, CancellationToken cancellationToken)
            {
                Source(m_Started, entry.StableId.Value).TrySetResult(true);
                using var registration = cancellationToken.Register(() =>
                {
                    Source(m_Cancelled, entry.StableId.Value).TrySetResult(true);
                    Source(m_Completed, entry.StableId.Value).TrySetCanceled(cancellationToken);
                });
                await Source(m_Completed, entry.StableId.Value).Task;
            }

            public Task WaitUntilStartedAsync(string id) => Source(m_Started, id).Task;
            public Task WaitUntilCancelledAsync(string id) => Source(m_Cancelled, id).Task;
            public void Complete(string id) => Source(m_Completed, id).TrySetResult(true);

            private static TaskCompletionSource<bool> Source(
                IDictionary<string, TaskCompletionSource<bool>> sources,
                string id)
            {
                if (!sources.TryGetValue(id, out var source))
                {
                    source = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    sources.Add(id, source);
                }

                return source;
            }
        }

        private sealed class RecordingDialogueQueue : IDialogueQueue
        {
            public List<DialogueEntry> Entries { get; } = new List<DialogueEntry>();
            public void Enqueue(DialogueEntry entry) => Entries.Add(entry);
        }
    }
}
