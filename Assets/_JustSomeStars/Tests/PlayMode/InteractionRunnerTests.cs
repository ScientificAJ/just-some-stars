using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Animation2D;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class InteractionRunnerTests
    {
        private readonly List<UnityEngine.Object> m_OwnedObjects =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (var index = m_OwnedObjects.Count - 1; index >= 0; index--)
            {
                UnityEngine.Object.DestroyImmediate(m_OwnedObjects[index]);
            }

            m_OwnedObjects.Clear();
        }

        [Test]
        public async Task ProbeRepair_CaptainJunoOriUseDistinctAnchorsAndRelease()
        {
            var definition = CreateDefinition();
            var anchors = CreateProbeAnchors();
            var participants = CreateParticipants(blockPlayback: true);
            var reservations = new InteractionReservationService();
            var events = new GameEventBus();
            var instruments = new List<ContentId>();
            var fragments = new List<ContentId>();
            using var instrumentSubscription = events.Subscribe<InstrumentUsed>(
                gameEvent => instruments.Add(gameEvent.InstrumentId));
            using var fragmentSubscription = events.Subscribe<SignalFragmentRecovered>(
                gameEvent => fragments.Add(gameEvent.FragmentId));
            var runner = new InteractionRunner(reservations, events);

            var run = runner.RunAsync(
                definition,
                participants,
                anchors,
                CancellationToken.None);
            await Task.WhenAll(participants.Select(actor => actor.PlayEntered));

            Assert.That(reservations.ActiveLeaseCount, Is.EqualTo(3));
            Assert.That(
                participants
                    .Select(actor => actor.LastDestination)
                    .Distinct()
                    .ToArray(),
                Has.Length.EqualTo(3));
            foreach (var participant in participants)
            {
                participant.ReleasePlayback();
            }

            var result = await run;

            Assert.That(
                result.Assignments
                    .Select(assignment => assignment.AnchorId)
                    .Distinct()
                    .ToArray(),
                Has.Length.EqualTo(3));
            Assert.That(
                participants.Select(actor => actor.PlayedClip.StableId),
                Is.EquivalentTo(new[]
                {
                    "clip.probe.captain",
                    "clip.probe.juno",
                    "clip.probe.ori",
                }));
            Assert.That(instruments.Single().Value, Is.EqualTo("tool.probe-wrench"));
            Assert.That(fragments.Single().Value, Is.EqualTo("fragment.probe"));
            Assert.That(reservations.ActiveLeaseCount, Is.Zero);
            Assert.That(participants.All(actor => !actor.WasRecovered), Is.True);
        }

        [Test]
        public async Task Cancellation_RecoversEveryActorAndReleasesEveryLease()
        {
            var definition = CreateDefinition();
            var anchors = CreateProbeAnchors();
            var participants = CreateParticipants(blockPlayback: true);
            var reservations = new InteractionReservationService();
            var events = new GameEventBus();
            var published = 0;
            using var subscription = events.Subscribe<InstrumentUsed>(_ => published++);
            var runner = new InteractionRunner(reservations, events);
            using var cancellation = new CancellationTokenSource();

            var run = runner.RunAsync(
                definition,
                participants,
                anchors,
                cancellation.Token);
            await Task.WhenAll(participants.Select(actor => actor.PlayEntered));
            Assert.That(reservations.ActiveLeaseCount, Is.EqualTo(3));

            cancellation.Cancel();

            var failure = await CaptureFailureAsync(run);
            Assert.That(failure, Is.InstanceOf<OperationCanceledException>());
            Assert.That(reservations.ActiveLeaseCount, Is.Zero);
            Assert.That(published, Is.Zero);
            Assert.That(participants.All(actor => actor.WasRecovered), Is.True);
            foreach (var participant in participants)
            {
                var anchor = anchors.Single(candidate =>
                    candidate.ActorKind == participant.ActorKind);
                Assert.That(
                    participant.Position,
                    Is.EqualTo(anchor.RecoveryPosition).Using(Vector2Comparer.Instance));
            }
        }

        [Test]
        public async Task MissingEligibleAnchor_FailsClosedWithoutMovingOrPublishing()
        {
            var definition = CreateDefinition();
            var anchors = CreateProbeAnchors()
                .Where(anchor => anchor.ActorKind != InteractionActorKind.Ori)
                .ToArray();
            var participants = CreateParticipants(blockPlayback: false);
            var reservations = new InteractionReservationService();
            var events = new GameEventBus();
            var published = 0;
            using var subscription = events.Subscribe<InstrumentUsed>(_ => published++);
            var runner = new InteractionRunner(reservations, events);

            var failure = await CaptureFailureAsync(runner.RunAsync(
                definition,
                participants,
                anchors,
                CancellationToken.None));

            Assert.That(failure, Is.TypeOf<InvalidOperationException>());
            Assert.That(participants.All(actor => actor.PlayedClip == null), Is.True);
            Assert.That(reservations.ActiveLeaseCount, Is.Zero);
            Assert.That(published, Is.Zero);
        }

        [Test]
        public async Task ParticipantFault_CancelsBlockedPeersAndRecoversEveryone()
        {
            var definition = CreateDefinition();
            var anchors = CreateProbeAnchors();
            var participants = CreateParticipants(blockPlayback: true);
            participants[2].SetPlaybackFailure(
                new InvalidOperationException("Ori action failed."));
            var reservations = new InteractionReservationService();
            var events = new GameEventBus();
            var published = 0;
            using var subscription = events.Subscribe<InstrumentUsed>(_ => published++);
            var runner = new InteractionRunner(reservations, events);
            using var fallbackCancellation = new CancellationTokenSource();

            var run = runner.RunAsync(
                definition,
                participants,
                anchors,
                fallbackCancellation.Token);
            await Task.WhenAll(participants.Select(actor => actor.PlayEntered));
            var completed = await Task.WhenAny(run, Task.Delay(250));
            var completedWithoutFallback = ReferenceEquals(completed, run);
            if (!completedWithoutFallback)
            {
                fallbackCancellation.Cancel();
            }

            var failure = await CaptureFailureAsync(run);

            Assert.That(
                completedWithoutFallback,
                Is.True,
                "The first participant fault must cancel blocked peers immediately.");
            Assert.That(failure, Is.TypeOf<InvalidOperationException>());
            Assert.That(reservations.ActiveLeaseCount, Is.Zero);
            Assert.That(participants.All(actor => actor.WasRecovered), Is.True);
            Assert.That(published, Is.Zero);
        }

        [Test]
        public async Task LeaseTimeout_CancelsRecoversAndReleasesWithoutEvents()
        {
            var definition = CreateDefinition(reservationTimeoutSeconds: 0.05f);
            var anchors = CreateProbeAnchors();
            var participants = CreateParticipants(blockPlayback: true);
            var reservations = new InteractionReservationService();
            var events = new GameEventBus();
            var published = 0;
            using var subscription = events.Subscribe<InstrumentUsed>(_ => published++);
            var runner = new InteractionRunner(reservations, events);
            using var fallbackCancellation = new CancellationTokenSource(
                TimeSpan.FromSeconds(1));

            var failure = await CaptureFailureAsync(runner.RunAsync(
                definition,
                participants,
                anchors,
                fallbackCancellation.Token));

            Assert.That(failure, Is.TypeOf<TimeoutException>());
            Assert.That(reservations.ActiveLeaseCount, Is.Zero);
            Assert.That(participants.All(actor => actor.WasRecovered), Is.True);
            Assert.That(published, Is.Zero);
        }

        private InteractionDefinition CreateDefinition(
            float reservationTimeoutSeconds = 5f)
        {
            var definition = ScriptableObject.CreateInstance<InteractionDefinition>();
            m_OwnedObjects.Add(definition);
            definition.Configure(
                "interaction.probe-repair",
                "tool.probe-wrench",
                CreateClip("clip.probe.captain"),
                CreateClip("clip.probe.juno"),
                CreateClip("clip.probe.ori"),
                new[] { GameMode.Surface },
                new[]
                {
                    new InteractionEventBinding(
                        InteractionEventKind.InstrumentUsed,
                        "tool.probe-wrench"),
                    new InteractionEventBinding(
                        InteractionEventKind.SignalFragmentRecovered,
                        "fragment.probe"),
                },
                maxDistance: 8f,
                reservationTimeoutSeconds);
            return definition;
        }

        private SpriteAnimationClipDefinition CreateClip(string id)
        {
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 2f, 2f),
                new Vector2(0.5f, 0.5f));
            var clip = ScriptableObject.CreateInstance<SpriteAnimationClipDefinition>();
            m_OwnedObjects.Add(clip);
            m_OwnedObjects.Add(sprite);
            m_OwnedObjects.Add(texture);
            clip.Configure(
                id,
                SpriteFacing.Right,
                SpriteAnimationLoopMode.Once,
                new[] { sprite },
                new[] { 0.1f },
                Array.Empty<SpriteFrameEvent>());
            return clip;
        }

        private InteractionAnchor2D[] CreateProbeAnchors()
        {
            return new[]
            {
                CreateAnchor(
                    "anchor.probe.captain",
                    InteractionActorKind.Player,
                    new Vector2(1f, 0f)),
                CreateAnchor(
                    "anchor.probe.juno",
                    InteractionActorKind.Crew,
                    new Vector2(2f, 0f)),
                CreateAnchor(
                    "anchor.probe.ori",
                    InteractionActorKind.Ori,
                    new Vector2(3f, 0f)),
            };
        }

        private InteractionAnchor2D CreateAnchor(
            string id,
            InteractionActorKind actorKind,
            Vector2 position)
        {
            var gameObject = new GameObject(id);
            m_OwnedObjects.Add(gameObject);
            gameObject.layer = 9;
            gameObject.transform.position = position;
            var anchor = gameObject.AddComponent<InteractionAnchor2D>();
            anchor.Configure(
                id,
                actorKind,
                InteractionFacing.Right,
                InteractionDepthBand.Gameplay,
                exclusive: true,
                requireApproachFacing: true,
                recoveryOffset: new Vector2(0f, 0.4f));
            return anchor;
        }

        private RecordingParticipant[] CreateParticipants(bool blockPlayback)
        {
            var tool = new[] { new ContentId("tool.probe-wrench") };
            return new[]
            {
                new RecordingParticipant(
                    "actor.captain",
                    InteractionActorKind.Player,
                    new Vector2(0f, 0f),
                    tool,
                    blockPlayback),
                new RecordingParticipant(
                    "actor.juno",
                    InteractionActorKind.Crew,
                    new Vector2(0.2f, 0f),
                    tool,
                    blockPlayback),
                new RecordingParticipant(
                    "actor.ori",
                    InteractionActorKind.Ori,
                    new Vector2(0.4f, 0f),
                    tool,
                    blockPlayback),
            };
        }

        private static async Task<Exception> CaptureFailureAsync(Task task)
        {
            try
            {
                await task;
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        private sealed class RecordingParticipant : IInteractionParticipant2D
        {
            private readonly bool m_BlockPlayback;
            private readonly TaskCompletionSource<object> m_PlayEntered =
                new TaskCompletionSource<object>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<object> m_PlayRelease =
                new TaskCompletionSource<object>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            private Exception m_PlaybackFailure;

            public RecordingParticipant(
                string id,
                InteractionActorKind actorKind,
                Vector2 position,
                IReadOnlyCollection<ContentId> tools,
                bool blockPlayback)
            {
                ActorId = new ContentId(id);
                ActorKind = actorKind;
                Position = position;
                Tools = tools;
                m_BlockPlayback = blockPlayback;
            }

            public ContentId ActorId { get; }
            public InteractionActorKind ActorKind { get; }
            public Vector2 Position { get; private set; }
            public InteractionFacing Facing => InteractionFacing.Right;
            public InteractionDepthBand DepthBand =>
                InteractionDepthBand.Gameplay;
            public int AllowedPhysicsLayers => 1 << 9;
            public GameMode Mode => GameMode.Surface;
            public IReadOnlyCollection<ContentId> Tools { get; }
            public Vector2 LastDestination { get; private set; }
            public SpriteAnimationClipDefinition PlayedClip { get; private set; }
            public bool WasRecovered { get; private set; }
            public Task PlayEntered => m_PlayEntered.Task;

            public ValueTask MoveToAsync(
                Vector2 destination,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Position = destination;
                LastDestination = destination;
                return default;
            }

            public ValueTask PlayAsync(
                SpriteAnimationClipDefinition clip,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PlayedClip = clip;
                m_PlayEntered.TrySetResult(null);
                if (m_PlaybackFailure != null)
                {
                    return new ValueTask(Task.FromException(m_PlaybackFailure));
                }

                if (!m_BlockPlayback)
                {
                    return default;
                }

                return new ValueTask(WaitForReleaseAsync(cancellationToken));
            }

            public void Recover(Vector2 recoveryPosition)
            {
                WasRecovered = true;
                Position = recoveryPosition;
            }

            public void ReleasePlayback()
            {
                m_PlayRelease.TrySetResult(null);
            }

            public void SetPlaybackFailure(Exception failure)
            {
                m_PlaybackFailure = failure ??
                    throw new ArgumentNullException(nameof(failure));
            }

            private async Task WaitForReleaseAsync(
                CancellationToken cancellationToken)
            {
                using var registration = cancellationToken.Register(
                    () => m_PlayRelease.TrySetCanceled(cancellationToken));
                await m_PlayRelease.Task;
            }
        }

        private sealed class Vector2Comparer : IEqualityComparer<Vector2>
        {
            public static readonly Vector2Comparer Instance =
                new Vector2Comparer();

            public bool Equals(Vector2 left, Vector2 right)
            {
                return Vector2.Distance(left, right) <= 0.0001f;
            }

            public int GetHashCode(Vector2 value)
            {
                return value.GetHashCode();
            }
        }
    }
}
