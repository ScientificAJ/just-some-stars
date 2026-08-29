using System;
using System.Collections.Generic;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Interaction;
using UnityEngine;

namespace JustSomeStars.Runtime.Crew
{
    public enum CrewActionState
    {
        Join = 0,
        Follow = 1,
        Position = 2,
        Traverse = 3,
        Investigate = 4,
        Interact = 5,
        React = 6,
        Speak = 7,
        Conversation = 8,
        Cinematic = 9,
        Wait = 10,
        Recover = 11,
    }

    public enum CrewActionPriority
    {
        Ambient = 0,
        Personality = 100,
        SafetyRecovery = 200,
        MandatoryStory = 300,
    }

    public sealed class CrewActionCandidate
    {
        public CrewActionCandidate(
            string id,
            CrewActionState state,
            CrewActionPriority priority,
            CrewAttention attention,
            float baseUtility,
            Vector2 targetPosition,
            InteractionDepthBand targetDepthBand,
            bool requiresDialogueToken,
            string interactionAnchorId = "",
            string targetTraversalNodeId = "",
            bool targetCameraVisible = false)
        {
            Id = new ContentId(id);
            State = state;
            Priority = priority;
            Attention = attention;
            BaseUtility = baseUtility;
            TargetPosition = targetPosition;
            TargetDepthBand = targetDepthBand;
            RequiresDialogueToken = requiresDialogueToken;
            InteractionAnchorId = interactionAnchorId;
            TargetTraversalNodeId = targetTraversalNodeId;
            TargetCameraVisible = targetCameraVisible;
            ValidateOrThrow();
        }

        public ContentId Id { get; }
        public CrewActionState State { get; }
        public CrewActionPriority Priority { get; }
        public CrewAttention Attention { get; }
        public float BaseUtility { get; }
        public Vector2 TargetPosition { get; }
        public InteractionDepthBand TargetDepthBand { get; }
        public bool RequiresDialogueToken { get; }
        public string InteractionAnchorId { get; }
        public string TargetTraversalNodeId { get; }
        public bool TargetCameraVisible { get; }
        public bool HasInteractionAnchor =>
            !string.IsNullOrWhiteSpace(InteractionAnchorId);

        public void ValidateOrThrow()
        {
            if (!Enum.IsDefined(typeof(CrewActionState), State) ||
                !Enum.IsDefined(typeof(CrewActionPriority), Priority) ||
                !Enum.IsDefined(typeof(CrewAttention), Attention) ||
                !Enum.IsDefined(typeof(InteractionDepthBand), TargetDepthBand) ||
                !IsFinite(BaseUtility) ||
                !IsFinite(TargetPosition.x) ||
                !IsFinite(TargetPosition.y) ||
                (!string.IsNullOrEmpty(InteractionAnchorId) &&
                    !string.Equals(InteractionAnchorId, InteractionAnchorId.Trim(),
                        StringComparison.Ordinal)) ||
                (!string.IsNullOrEmpty(TargetTraversalNodeId) &&
                    !string.Equals(TargetTraversalNodeId,
                        TargetTraversalNodeId.Trim(), StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Crew action '{Id}' has invalid authored data.");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public static class CrewUtility
    {
        public static CrewActionCandidate Select(
            IEnumerable<CrewActionCandidate> candidates,
            CrewPersonality personality)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (personality == null)
            {
                throw new ArgumentNullException(nameof(personality));
            }

            personality.ValidateOrThrow();
            CrewActionCandidate best = null;
            var bestScoreRank = double.NegativeInfinity;
            foreach (var candidate in candidates)
            {
                if (candidate == null)
                {
                    throw new InvalidOperationException(
                        "Crew utility selection contains a null candidate.");
                }

                candidate.ValidateOrThrow();
                var score = (double)candidate.BaseUtility +
                    personality.GetAttentionWeight(candidate.Attention);
                const double scoreStep = 0.000001d;
                var scoreRank = Math.Round(
                    score / scoreStep,
                    MidpointRounding.AwayFromZero);
                if (best == null || candidate.Priority > best.Priority ||
                    (candidate.Priority == best.Priority &&
                        scoreRank > bestScoreRank) ||
                    (candidate.Priority == best.Priority &&
                        scoreRank == bestScoreRank &&
                        string.CompareOrdinal(
                            candidate.Id.Value,
                            best.Id.Value) < 0))
                {
                    best = candidate;
                    bestScoreRank = scoreRank;
                }
            }

            return best ?? throw new InvalidOperationException(
                "Crew utility selection requires at least one candidate.");
        }
    }

    public sealed class CrewDecision : IDisposable
    {
        public CrewDecision(
            ContentId actorId,
            CrewActionCandidate action,
            DialogueToken dialogueToken = null,
            InteractionReservationLease interactionLease = null)
        {
            if (!actorId.IsValid)
            {
                throw new ArgumentException("Crew decision requires an actor ID.");
            }

            ActorId = actorId;
            Action = action ?? throw new ArgumentNullException(nameof(action));
            DialogueToken = dialogueToken;
            InteractionLease = interactionLease;
        }

        public ContentId ActorId { get; }
        public CrewActionCandidate Action { get; }
        public DialogueToken DialogueToken { get; }
        public InteractionReservationLease InteractionLease { get; }

        public void Dispose()
        {
            DialogueToken?.Dispose();
            InteractionLease?.Dispose();
        }
    }

    public sealed class DecisionBatch : IDisposable
    {
        private readonly IReadOnlyList<CrewDecision> m_Decisions;

        public DecisionBatch(IReadOnlyList<CrewDecision> decisions)
        {
            m_Decisions = decisions ?? throw new ArgumentNullException(nameof(decisions));
        }

        public IReadOnlyList<CrewDecision> Decisions => m_Decisions;

        public void Dispose()
        {
            foreach (var decision in m_Decisions)
            {
                decision?.Dispose();
            }
        }
    }
}
