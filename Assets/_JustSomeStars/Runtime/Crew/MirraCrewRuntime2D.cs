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
    public sealed class MirraCrewRuntime2D : MonoBehaviour
    {
        [SerializeField] private CrewPersonality[] personalities =
            Array.Empty<CrewPersonality>();
        [SerializeField] private MirraCrewActorRuntime2D[] actors =
            Array.Empty<MirraCrewActorRuntime2D>();
        [SerializeField] private TraversalGraph2D traversalGraph;
        [SerializeField] private Rigidbody2D captainBody;
        [SerializeField, Min(0.05f)] private float decisionIntervalSeconds = 0.2f;
        [SerializeField, Min(1f)] private float maximumRouteDistance = 7f;

        private CrewDirector m_Director;
        private IReadOnlyList<CrewBrain> m_ActiveTeam = Array.Empty<CrewBrain>();
        private Dictionary<ContentId, CrewBrain> m_BrainsById;
        private Dictionary<ContentId, MirraCrewActorRuntime2D> m_ActorsById;
        private InteractionReservationService m_Reservations;
        private bool m_FacingRight = true;

        public bool IsConfigured => m_Director != null;
        public int DecisionTickCount { get; private set; }
        public IReadOnlyList<ContentId> AuthoredActorIds => (personalities ??
                Array.Empty<CrewPersonality>())
            .Where(item => item != null)
            .Select(item => item.StableId)
            .ToArray();
        public IReadOnlyList<ContentId> ActiveActorIds => m_ActiveTeam
            .Select(item => item.ActorId)
            .ToArray();

        public void Configure(InteractionReservationService reservations)
        {
            if (reservations == null)
            {
                throw new ArgumentNullException(nameof(reservations));
            }

            if (m_Director != null)
            {
                if (ReferenceEquals(m_Reservations, reservations))
                {
                    return;
                }

                throw new InvalidOperationException(
                    "Mirra crew runtime already belongs to another composition.");
            }

            ValidateOrThrow();
            m_Reservations = reservations;
            m_Director = new CrewDirector(
                new DialogueTokenArbiter(),
                reservations,
                decisionIntervalSeconds);
            var brains = personalities.Select(item => new CrewBrain(item)).ToArray();
            m_BrainsById = brains.ToDictionary(item => item.ActorId);
            m_ActorsById = actors.ToDictionary(item => item.ActorId);
            var destinationFit = new Dictionary<ContentId, float>
            {
                [new ContentId("crew.mira")] = 1f,
                [new ContentId("crew.juno")] = 0.9f,
                [new ContentId("crew.ori")] = 1f,
            };
            m_ActiveTeam = m_Director.SelectExpeditionTeam(
                brains,
                destinationFit);
        }

        public void Release(InteractionReservationService reservations)
        {
            if (m_Director == null)
            {
                return;
            }

            if (!ReferenceEquals(m_Reservations, reservations))
            {
                throw new InvalidOperationException(
                    "Mirra crew runtime can only release its owning composition.");
            }

            m_Director = null;
            m_ActiveTeam = Array.Empty<CrewBrain>();
            m_BrainsById = null;
            m_ActorsById = null;
            m_Reservations = null;
        }

        public void SetCinematicControl(bool enabled)
        {
            m_Director?.SetCinematicControl(enabled);
        }

        public void ValidateOrThrow()
        {
            if (personalities == null || personalities.Length != 3 ||
                personalities.Any(item => item == null) ||
                actors == null || actors.Length != 3 ||
                actors.Any(item => item == null) || traversalGraph == null ||
                captainBody == null || decisionIntervalSeconds <= 0f ||
                float.IsNaN(decisionIntervalSeconds) ||
                float.IsInfinity(decisionIntervalSeconds) ||
                maximumRouteDistance <= 0f || float.IsNaN(maximumRouteDistance) ||
                float.IsInfinity(maximumRouteDistance))
            {
                throw new InvalidOperationException(
                    "Mirra crew runtime requires three authored actors, " +
                    "personalities, traversal, and Captain bindings.");
            }

            foreach (var personality in personalities)
            {
                personality.ValidateOrThrow();
            }

            foreach (var actor in actors)
            {
                actor.ValidateOrThrow();
            }

            var personalityIds = personalities.Select(item => item.StableId).ToArray();
            var actorIds = actors.Select(item => item.ActorId).ToArray();
            var expected = new[]
            {
                new ContentId("crew.mira"),
                new ContentId("crew.juno"),
                new ContentId("crew.ori"),
            };
            if (!personalityIds.SequenceEqual(expected) ||
                !actorIds.SequenceEqual(expected))
            {
                throw new InvalidOperationException(
                    "Mirra's production team must be Mira, Juno and Ori in " +
                    "canonical order.");
            }

            traversalGraph.FindPath(
                new ContentId("route.mirra.hot"),
                new ContentId("route.mirra.cold"));
        }

        private void Update()
        {
            if (m_Director == null)
            {
                return;
            }

            if (Mathf.Abs(captainBody.linearVelocity.x) > 0.05f)
            {
                m_FacingRight = captainBody.linearVelocity.x > 0f;
            }

            var formation = m_Director.BuildFormation(
                captainBody.position,
                m_FacingRight,
                m_ActiveTeam).ToDictionary(item => item.ActorId);
            var desiredNode = DesiredTraversalNode(captainBody.position.x);
            var candidates = new Dictionary<ContentId,
                IReadOnlyList<CrewActionCandidate>>();
            foreach (var brain in m_ActiveTeam)
            {
                var actor = m_ActorsById[brain.ActorId];
                var formationPosition = formation[brain.ActorId].Position;
                var recovery = actor.EvaluateRecovery(
                    formationPosition,
                    maximumRouteDistance);
                CrewActionCandidate candidate;
                if (recovery.Kind == CrewRecoveryKind.HiddenWarp)
                {
                    candidate = new CrewActionCandidate(
                        $"action.{brain.ActorId.Value}.recover",
                        CrewActionState.Recover,
                        CrewActionPriority.SafetyRecovery,
                        CrewAttention.None,
                        1f,
                        recovery.Position,
                        InteractionDepthBand.Gameplay,
                        requiresDialogueToken: false,
                        targetCameraVisible: false);
                }
                else if (actor.CurrentTraversalNodeId != desiredNode)
                {
                    candidate = new CrewActionCandidate(
                        $"action.{brain.ActorId.Value}.traverse",
                        CrewActionState.Traverse,
                        CrewActionPriority.SafetyRecovery,
                        CrewAttention.TraversalAndDanger,
                        1f,
                        formationPosition,
                        InteractionDepthBand.Gameplay,
                        requiresDialogueToken: false,
                        targetTraversalNodeId: desiredNode.Value);
                }
                else
                {
                    candidate = new CrewActionCandidate(
                        $"action.{brain.ActorId.Value}.follow",
                        CrewActionState.Follow,
                        CrewActionPriority.Ambient,
                        CrewAttention.None,
                        0f,
                        formationPosition,
                        InteractionDepthBand.Gameplay,
                        requiresDialogueToken: false);
                }

                candidates.Add(brain.ActorId, new[] { candidate });
            }

            var decisions = m_Director.Tick(
                m_ActiveTeam,
                candidates,
                Time.unscaledTimeAsDouble);
            if (decisions.Count == 0)
            {
                return;
            }

            DecisionTickCount++;
            using var batch = new DecisionBatch(decisions);
            foreach (var decision in decisions)
            {
                m_BrainsById[decision.ActorId].Execute(
                    decision,
                    m_ActorsById[decision.ActorId],
                    traversalGraph);
            }
        }

        private static ContentId DesiredTraversalNode(float captainX)
        {
            if (captainX < -2f)
            {
                return new ContentId("route.mirra.hot");
            }

            return captainX > 2f
                ? new ContentId("route.mirra.cold")
                : new ContentId("route.mirra.twilight");
        }
    }
}
