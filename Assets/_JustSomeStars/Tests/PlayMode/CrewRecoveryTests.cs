using System;
using System.Linq;
using System.Threading;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Crew;
using JustSomeStars.Runtime.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class CrewRecoveryTests
    {
        [Test]
        public void OffCameraBlockedActor_WarpsToAuthoredRecoveryPoint()
        {
            var recovery = new CrewRecovery(
                blockedSecondsBeforeRecovery: 1.5f,
                maximumRouteDistance: 12f);
            var context = new CrewRecoveryContext(
                actorPosition: new Vector2(2f, 1f),
                recoveryPosition: new Vector2(8f, 2f),
                cameraVisible: false,
                recoveryPositionVisible: false,
                blockedSeconds: 2f,
                remainingRouteDistance: 4f,
                routeAvailable: true);

            var decision = recovery.Evaluate(context);

            Assert.That(decision.Kind, Is.EqualTo(CrewRecoveryKind.HiddenWarp));
            Assert.That(decision.Position, Is.EqualTo(new Vector2(8f, 2f)));
            Assert.That(decision.NextState, Is.EqualTo(CrewActionState.Recover));
        }

        [Test]
        public void VisibleActor_NeverTeleportsAndRequestsRepathInstead()
        {
            var recovery = new CrewRecovery(1f, 10f);
            var decision = recovery.Evaluate(new CrewRecoveryContext(
                Vector2.zero,
                new Vector2(9f, 0f),
                cameraVisible: true,
                recoveryPositionVisible: false,
                blockedSeconds: 8f,
                remainingRouteDistance: 5f,
                routeAvailable: false));

            Assert.That(decision.Kind, Is.EqualTo(CrewRecoveryKind.Repath));
            Assert.That(decision.AllowsTeleport, Is.False);
        }

        [Test]
        public void ExcessiveOffCameraRoute_RecoversInsteadOfRemainingStuck()
        {
            var recovery = new CrewRecovery(2f, 6f);
            var decision = recovery.Evaluate(new CrewRecoveryContext(
                Vector2.zero,
                new Vector2(1f, 1f),
                cameraVisible: false,
                recoveryPositionVisible: false,
                blockedSeconds: 0f,
                remainingRouteDistance: 20f,
                routeAvailable: true));

            Assert.That(decision.Kind, Is.EqualTo(CrewRecoveryKind.HiddenWarp));
            Assert.That(decision.AllowsTeleport, Is.True);
        }

        [Test]
        public void VisibleRecoveryDestination_NeverAllowsHiddenWarp()
        {
            var recovery = new CrewRecovery(1f, 10f);
            var decision = recovery.Evaluate(new CrewRecoveryContext(
                Vector2.zero,
                new Vector2(2f, 0f),
                cameraVisible: false,
                recoveryPositionVisible: true,
                blockedSeconds: 3f,
                remainingRouteDistance: 4f,
                routeAvailable: false));

            Assert.That(decision.Kind, Is.EqualTo(CrewRecoveryKind.Repath));
            Assert.That(decision.AllowsTeleport, Is.False);
        }

        [Test]
        public void DirectorTick_OwnsDialogueAndInteractionReservations()
        {
            var owned = new System.Collections.Generic.List<UnityEngine.Object>();
            try
            {
                var mira = CreateBrain("crew.mira", CrewRole.Mira,
                    CrewAttention.AtmosphereAndEvidence, owned);
                var juno = CreateBrain("crew.juno", CrewRole.Juno,
                    CrewAttention.MachineryAndTools, owned);
                var ori = CreateBrain("crew.ori", CrewRole.Ori,
                    CrewAttention.HazardsAndSignal, owned);
                var team = new[] { mira, juno, ori };
                var reservations = new InteractionReservationService();
                var dialogue = new DialogueTokenArbiter();
                var director = new CrewDirector(dialogue, reservations, 0.2f);
                var candidates = new System.Collections.Generic.Dictionary<
                    ContentId,
                    System.Collections.Generic.IReadOnlyList<CrewActionCandidate>>
                {
                    [mira.ActorId] = new[] { Action("action.mira.speak",
                        CrewActionState.Speak, "", requiresDialogue: true) },
                    [juno.ActorId] = new[] { Action("action.juno.speak",
                        CrewActionState.Speak, "", requiresDialogue: true) },
                    [ori.ActorId] = new[] { Action("action.ori.interact",
                        CrewActionState.Interact, "anchor.ori.scan",
                        requiresDialogue: false) },
                };

                using var batch = new DecisionBatch(
                    director.Tick(team, candidates, nowSeconds: 0d));

                Assert.That(batch.Decisions.Count(decision =>
                    decision.DialogueToken?.IsActive == true), Is.EqualTo(1));
                Assert.That(batch.Decisions.Count(decision =>
                    decision.Action.State == CrewActionState.Wait), Is.EqualTo(1));
                Assert.That(batch.Decisions.Single(decision =>
                    decision.ActorId == ori.ActorId).InteractionLease.IsActive,
                    Is.True);
                Assert.That(dialogue.ActiveTokenCount, Is.EqualTo(1));
                Assert.That(reservations.ActiveLeaseCount, Is.EqualTo(1));
            }
            finally
            {
                foreach (var item in owned)
                {
                    UnityEngine.Object.DestroyImmediate(item);
                }
            }
        }

        [Test]
        public void DialogueContention_AllowsExactlyOneAuthoredLineAtATime()
        {
            var arbiter = new DialogueTokenArbiter();
            Assert.That(arbiter.TryAcquire(
                new ContentId("crew.mira"),
                priority: 10,
                interruptible: false,
                out var mira), Is.True);
            Assert.That(arbiter.TryAcquire(
                new ContentId("crew.juno"),
                priority: 100,
                interruptible: true,
                out var juno), Is.False);
            Assert.That(juno, Is.Null);
            Assert.That(arbiter.Owner, Is.EqualTo(new ContentId("crew.mira")));

            mira.Dispose();
            Assert.That(arbiter.TryAcquire(
                new ContentId("crew.juno"),
                priority: 10,
                interruptible: true,
                out juno), Is.True);
            Assert.That(arbiter.ActiveTokenCount, Is.EqualTo(1));
            juno.Dispose();
            Assert.That(arbiter.ActiveTokenCount, Is.Zero);
        }

        [Test]
        public void BrainInteractionLease_CancellationReleasesExclusiveAnchor()
        {
            var personality = ScriptableObject.CreateInstance<CrewPersonality>();
            try
            {
                personality.Configure(
                    "crew.juno",
                    "Juno",
                    CrewRole.Juno,
                    CrewAttention.MachineryAndTools,
                    new[]
                    {
                        new CrewAttentionWeight(
                            CrewAttention.MachineryAndTools,
                            1f),
                    });
                var brain = new CrewBrain(personality);
                var reservations = new InteractionReservationService();
                using var cancellation = new CancellationTokenSource();

                Assert.That(brain.TryReserveInteraction(
                    reservations,
                    new ContentId("anchor.juno-repair"),
                    exclusive: true,
                    TimeSpan.FromSeconds(2),
                    cancellation.Token,
                    out var lease), Is.True);
                Assert.That(reservations.ActiveLeaseCount, Is.EqualTo(1));

                cancellation.Cancel();

                Assert.That(lease.IsActive, Is.False);
                Assert.That(reservations.ActiveLeaseCount, Is.Zero);
                lease.Dispose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(personality);
            }
        }

        [Test]
        public void Brain_ExecutesEveryDeclaredStateThroughConcrete2DRuntime()
        {
            var owned = new System.Collections.Generic.List<UnityEngine.Object>();
            var graph = ScriptableObject.CreateInstance<TraversalGraph2D>();
            owned.Add(graph);
            graph.Configure(
                new[]
                {
                    new TraversalNode2D("node.a", Vector2.zero,
                        InteractionDepthBand.Gameplay, "node.b"),
                    new TraversalNode2D("node.b", Vector2.right,
                        InteractionDepthBand.Gameplay),
                },
                Array.Empty<TraversalDepthTransition>());
            var brain = CreateBrain("crew.juno", CrewRole.Juno,
                CrewAttention.MachineryAndTools, owned);
            var runtime = new RecordingCrewRuntime();
            var reservations = new InteractionReservationService();
            var dialogue = new DialogueTokenArbiter();
            try
            {
                foreach (CrewActionState state in Enum.GetValues(
                    typeof(CrewActionState)))
                {
                    DialogueToken token = null;
                    InteractionReservationLease lease = null;
                    var anchor = state == CrewActionState.Interact
                        ? "anchor.juno"
                        : "";
                    if (state == CrewActionState.Speak ||
                        state == CrewActionState.Conversation)
                    {
                        Assert.That(dialogue.TryAcquire(
                            brain.ActorId, 1, true, out token), Is.True);
                    }

                    if (state == CrewActionState.Interact)
                    {
                        Assert.That(reservations.TryReserve(
                            new ContentId(anchor), brain.ActorId, true,
                            TimeSpan.FromSeconds(2), default, out lease), Is.True);
                    }

                    var action = new CrewActionCandidate(
                        $"action.juno.{state.ToString().ToLowerInvariant()}",
                        state,
                        CrewActionPriority.Personality,
                        CrewAttention.MachineryAndTools,
                        1f,
                        Vector2.right,
                        InteractionDepthBand.Gameplay,
                        state == CrewActionState.Speak ||
                            state == CrewActionState.Conversation,
                        interactionAnchorId: anchor,
                        targetTraversalNodeId: state == CrewActionState.Traverse
                            ? "node.b"
                            : "",
                        targetCameraVisible: false);
                    using var decision = new CrewDecision(
                        brain.ActorId, action, token, lease);

                    brain.Execute(decision, runtime, graph);

                    Assert.That(runtime.LastState, Is.EqualTo(state), state.ToString());
                }
            }
            finally
            {
                foreach (var item in owned)
                {
                    UnityEngine.Object.DestroyImmediate(item);
                }
            }
        }


        private static CrewBrain CreateBrain(
            string id,
            CrewRole role,
            CrewAttention primary,
            System.Collections.Generic.ICollection<UnityEngine.Object> owned)
        {
            var personality = ScriptableObject.CreateInstance<CrewPersonality>();
            owned.Add(personality);
            personality.Configure(id, role.ToString(), role, primary,
                new[] { new CrewAttentionWeight(primary, 1f) });
            return new CrewBrain(personality);
        }

        private static CrewActionCandidate Action(
            string id,
            CrewActionState state,
            string anchorId,
            bool requiresDialogue)
        {
            return new CrewActionCandidate(
                id, state, CrewActionPriority.Personality,
                CrewAttention.HazardsAndSignal, 1f, Vector2.zero,
                InteractionDepthBand.Gameplay, requiresDialogue,
                interactionAnchorId: anchorId);
        }

        private sealed class RecordingCrewRuntime : ICrewActionRuntime2D
        {
            public ContentId CurrentTraversalNodeId => new ContentId("node.a");
            public bool CameraVisible => false;
            public CrewActionState LastState { get; private set; }

            public void Join(Vector2 position) => LastState = CrewActionState.Join;
            public void Follow(Vector2 position) => LastState = CrewActionState.Follow;
            public void Position(Vector2 position) => LastState = CrewActionState.Position;
            public void Traverse(
                System.Collections.Generic.IReadOnlyList<TraversalNode2D> path)
            {
                Assert.That(path.Select(node => node.Id.Value), Is.EqualTo(
                    new[] { "node.a", "node.b" }));
                LastState = CrewActionState.Traverse;
            }
            public void Investigate(Vector2 position) =>
                LastState = CrewActionState.Investigate;
            public void Interact(Vector2 position) =>
                LastState = CrewActionState.Interact;
            public void React(Vector2 position) => LastState = CrewActionState.React;
            public void Speak() => LastState = CrewActionState.Speak;
            public void Converse() => LastState = CrewActionState.Conversation;
            public void EnterCinematic() => LastState = CrewActionState.Cinematic;
            public void Wait() => LastState = CrewActionState.Wait;
            public void Recover(Vector2 position) => LastState = CrewActionState.Recover;
        }
    }
}
