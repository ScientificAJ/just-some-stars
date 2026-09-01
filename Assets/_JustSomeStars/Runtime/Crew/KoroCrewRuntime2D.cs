using System;
using System.Collections.Generic;
using System.Linq;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Dialogue;
using JustSomeStars.Runtime.Interaction;
using UnityEngine;

namespace JustSomeStars.Runtime.Crew
{
    [DisallowMultipleComponent]
    public sealed class KoroCrewRuntime2D : MonoBehaviour
    {
        [SerializeField] private CrewPersonality[] personalities =
            Array.Empty<CrewPersonality>();
        [SerializeField] private KoroCrewActorRuntime2D[] actors =
            Array.Empty<KoroCrewActorRuntime2D>();
        [SerializeField] private TraversalGraph2D traversalGraph;
        [SerializeField] private Rigidbody2D captainBody;
        [SerializeField, Min(0.05f)] private float decisionIntervalSeconds = 0.2f;
        [SerializeField, Min(1f)] private float maximumRouteDistance = 7f;

        private CrewDirector m_Director;
        private IReadOnlyList<CrewBrain> m_Team = Array.Empty<CrewBrain>();
        private Dictionary<ContentId, CrewBrain> m_Brains;
        private Dictionary<ContentId, KoroCrewActorRuntime2D> m_Actors;
        private InteractionReservationService m_Reservations;
        private bool m_FacingRight = true;

        public bool IsConfigured => m_Director != null;
        public int DecisionTickCount { get; private set; }
        public IReadOnlyList<ContentId> ActiveActorIds =>
            m_Team.Select(item => item.ActorId).ToArray();

        public void Configure(InteractionReservationService reservations)
        {
            if (reservations == null)
            {
                throw new ArgumentNullException(nameof(reservations));
            }
            if (m_Director != null)
            {
                if (ReferenceEquals(m_Reservations, reservations)) return;
                throw new InvalidOperationException(
                    "Koro crew runtime already belongs to another composition.");
            }
            ValidateOrThrow();
            m_Reservations = reservations;
            m_Director = new CrewDirector(
                new DialogueTokenArbiter(), reservations, decisionIntervalSeconds);
            m_Team = personalities.Select(item => new CrewBrain(item)).ToArray();
            m_Brains = m_Team.ToDictionary(item => item.ActorId);
            m_Actors = actors.ToDictionary(item => item.ActorId);
        }

        public void Release(InteractionReservationService reservations)
        {
            if (m_Director == null) return;
            if (!ReferenceEquals(m_Reservations, reservations))
            {
                throw new InvalidOperationException(
                    "Koro crew runtime can only release its owner.");
            }
            m_Director = null;
            m_Team = Array.Empty<CrewBrain>();
            m_Brains = null;
            m_Actors = null;
            m_Reservations = null;
        }

        public void ValidateOrThrow()
        {
            var expected = new[]
            {
                new ContentId("crew.mira"),
                new ContentId("crew.bea"),
                new ContentId("crew.ori"),
            };
            if (personalities == null || actors == null ||
                personalities.Length != 3 || actors.Length != 3 ||
                personalities.Any(item => item == null) ||
                actors.Any(item => item == null) || traversalGraph == null ||
                captainBody == null ||
                !personalities.Select(item => item.StableId).SequenceEqual(expected) ||
                !actors.Select(item => item.ActorId).SequenceEqual(expected))
            {
                throw new InvalidOperationException(
                    "Koro requires Mira, Bea and Ori with one authored traversal graph.");
            }
            foreach (var personality in personalities) personality.ValidateOrThrow();
            foreach (var actor in actors) actor.ValidateOrThrow();
            traversalGraph.FindPath(
                new ContentId("route.koro.start"),
                new ContentId("route.koro.signal"));
        }

        private void Update()
        {
            if (m_Director == null) return;
            if (Mathf.Abs(captainBody.linearVelocity.x) > 0.05f)
            {
                m_FacingRight = captainBody.linearVelocity.x > 0f;
            }
            var formation = m_Director.BuildFormation(
                captainBody.position, m_FacingRight, m_Team)
                .ToDictionary(item => item.ActorId);
            var desired = DesiredNode(captainBody.position.x);
            var candidates = new Dictionary<ContentId,
                IReadOnlyList<CrewActionCandidate>>();
            foreach (var brain in m_Team)
            {
                var actor = m_Actors[brain.ActorId];
                var target = formation[brain.ActorId].Position;
                var recovery = actor.EvaluateRecovery(target, maximumRouteDistance);
                var candidate = recovery.Kind == CrewRecoveryKind.HiddenWarp
                    ? new CrewActionCandidate(
                        $"action.{brain.ActorId.Value}.recover",
                        CrewActionState.Recover,
                        CrewActionPriority.SafetyRecovery,
                        CrewAttention.None,
                        1f,
                        recovery.Position,
                        InteractionDepthBand.Gameplay,
                        false,
                        targetCameraVisible: false)
                    : actor.CurrentTraversalNodeId != desired
                        ? new CrewActionCandidate(
                            $"action.{brain.ActorId.Value}.traverse",
                            CrewActionState.Traverse,
                            CrewActionPriority.SafetyRecovery,
                            CrewAttention.TraversalAndDanger,
                            1f,
                            target,
                            InteractionDepthBand.Gameplay,
                            false,
                            targetTraversalNodeId: desired.Value)
                        : new CrewActionCandidate(
                            $"action.{brain.ActorId.Value}.follow",
                            CrewActionState.Follow,
                            CrewActionPriority.Ambient,
                            CrewAttention.None,
                            0f,
                            target,
                            InteractionDepthBand.Gameplay,
                            false);
                candidates.Add(brain.ActorId, new[] { candidate });
            }
            var decisions = m_Director.Tick(
                m_Team, candidates, Time.unscaledTimeAsDouble);
            if (decisions.Count == 0) return;
            DecisionTickCount++;
            using var batch = new DecisionBatch(decisions);
            foreach (var decision in decisions)
            {
                m_Brains[decision.ActorId].Execute(
                    decision, m_Actors[decision.ActorId], traversalGraph);
            }
        }

        private static ContentId DesiredNode(float x) => x < -2f
            ? new ContentId("route.koro.start")
            : x > 2.4f
                ? new ContentId("route.koro.signal")
                : new ContentId("route.koro.geysers");
    }
}
