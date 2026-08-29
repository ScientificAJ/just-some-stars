using System;
using System.Collections.Generic;
using System.Linq;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Interaction;
using UnityEngine;

namespace JustSomeStars.Runtime.Crew
{
    public readonly struct CrewFormationPosition
    {
        public CrewFormationPosition(ContentId actorId, Vector2 position)
        {
            ActorId = actorId;
            Position = position;
        }

        public ContentId ActorId { get; }
        public Vector2 Position { get; }
    }

    public sealed class CrewDirector
    {
        private readonly DialogueTokenArbiter m_Dialogue;
        private readonly InteractionReservationService m_Reservations;
        private readonly double m_DecisionIntervalSeconds;
        private double m_NextDecisionSeconds;

        public CrewDirector(
            DialogueTokenArbiter dialogue,
            InteractionReservationService reservations,
            float decisionIntervalSeconds)
        {
            m_Dialogue = dialogue ?? throw new ArgumentNullException(nameof(dialogue));
            m_Reservations = reservations ??
                throw new ArgumentNullException(nameof(reservations));
            if (decisionIntervalSeconds <= 0f ||
                float.IsNaN(decisionIntervalSeconds) ||
                float.IsInfinity(decisionIntervalSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(decisionIntervalSeconds));
            }

            m_DecisionIntervalSeconds = decisionIntervalSeconds;
        }

        public bool CinematicControl { get; private set; }
        public DialogueTokenArbiter Dialogue => m_Dialogue;

        public IReadOnlyList<CrewBrain> SelectExpeditionTeam(
            IEnumerable<CrewBrain> availableBrains,
            IReadOnlyDictionary<ContentId, float> destinationFit)
        {
            if (availableBrains == null || destinationFit == null)
            {
                throw new ArgumentNullException(
                    availableBrains == null
                        ? nameof(availableBrains)
                        : nameof(destinationFit));
            }

            var brains = availableBrains.ToArray();
            if (brains.Any(brain => brain == null) ||
                brains.Select(brain => brain.ActorId).Distinct().Count() !=
                    brains.Length)
            {
                throw new InvalidOperationException(
                    "Crew selection requires unique non-null brains.");
            }

            var ori = brains.Where(brain => brain.IsOri).ToArray();
            var humans = brains.Where(brain => !brain.IsOri).ToArray();
            if (ori.Length != 1 || humans.Length < 2)
            {
                throw new InvalidOperationException(
                    "A destination team requires two human companions and Ori.");
            }

            var selectedHumans = humans
                .OrderByDescending(brain => destinationFit.TryGetValue(
                    brain.ActorId,
                    out var fit) ? fit : 0f)
                .ThenBy(brain => brain.ActorId.Value, StringComparer.Ordinal)
                .Take(2);
            return selectedHumans.Concat(ori).ToArray();
        }

        public IReadOnlyList<CrewFormationPosition> BuildFormation(
            Vector2 captainPosition,
            bool facingRight,
            IReadOnlyList<CrewBrain> activeTeam)
        {
            if (activeTeam == null || activeTeam.Count != 3 ||
                activeTeam.Count(brain => brain.IsOri) != 1)
            {
                throw new InvalidOperationException(
                    "Formation requires exactly two companions plus Ori.");
            }

            var direction = facingRight ? -1f : 1f;
            var offsets = new[]
            {
                new Vector2(1.25f * direction, 0f),
                new Vector2(2.15f * direction, 0f),
                new Vector2(0.7f * direction, -0.05f),
            };
            return activeTeam.Select((brain, index) =>
                new CrewFormationPosition(
                    brain.ActorId,
                    captainPosition + offsets[index])).ToArray();
        }

        public IReadOnlyList<CrewDecision> Tick(
            IReadOnlyList<CrewBrain> activeTeam,
            IReadOnlyDictionary<ContentId, IReadOnlyList<CrewActionCandidate>>
                candidatesByActor,
            double nowSeconds)
        {
            if (activeTeam == null || candidatesByActor == null)
            {
                throw new ArgumentNullException(
                    activeTeam == null
                        ? nameof(activeTeam)
                        : nameof(candidatesByActor));
            }

            ValidateActiveTeam(activeTeam);
            if (double.IsNaN(nowSeconds) || double.IsInfinity(nowSeconds) ||
                nowSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(nowSeconds));
            }

            if (CinematicControl)
            {
                foreach (var brain in activeTeam)
                {
                    brain.SetState(CrewActionState.Cinematic);
                }

                return Array.Empty<CrewDecision>();
            }

            if (nowSeconds + 0.0000001d < m_NextDecisionSeconds)
            {
                return Array.Empty<CrewDecision>();
            }

            m_NextDecisionSeconds = nowSeconds + m_DecisionIntervalSeconds;
            var choices = new List<CrewActionCandidate>(activeTeam.Count);
            foreach (var brain in activeTeam)
            {
                if (!candidatesByActor.TryGetValue(
                        brain.ActorId,
                        out var candidates))
                {
                    throw new InvalidOperationException(
                        $"No authored candidates exist for '{brain.ActorId}'.");
                }

                choices.Add(brain.Decide(candidates, cinematicControl: false));
            }

            var decisions = new List<CrewDecision>(activeTeam.Count);
            try
            {
                var dialogueWinner = choices
                    .Select((choice, index) => (Choice: choice, Index: index))
                    .Where(entry => entry.Choice.RequiresDialogueToken)
                    .OrderByDescending(entry => entry.Choice.Priority)
                    .ThenByDescending(entry => entry.Choice.BaseUtility)
                    .ThenBy(entry => activeTeam[entry.Index].ActorId.Value,
                        StringComparer.Ordinal)
                    .Select(entry => entry.Index)
                    .DefaultIfEmpty(-1)
                    .First();
                for (var index = 0; index < activeTeam.Count; index++)
                {
                    var brain = activeTeam[index];
                    var choice = choices[index];
                    DialogueToken dialogueToken = null;
                    InteractionReservationLease interactionLease = null;
                    if (choice.RequiresDialogueToken)
                    {
                        if (index != dialogueWinner || !m_Dialogue.TryAcquire(
                                brain.ActorId,
                                (int)choice.Priority,
                                interruptible: choice.Priority <
                                    CrewActionPriority.MandatoryStory,
                                out dialogueToken))
                        {
                            choice = CreateWait(brain.ActorId);
                            brain.ApplyDecision(choice);
                        }
                    }

                    if (choice.State == CrewActionState.Interact)
                    {
                        if (!choice.HasInteractionAnchor ||
                            !m_Reservations.TryReserve(
                                new ContentId(choice.InteractionAnchorId),
                                brain.ActorId,
                                exclusive: true,
                                TimeSpan.FromSeconds(5),
                                default,
                                out interactionLease))
                        {
                            dialogueToken?.Dispose();
                            dialogueToken = null;
                            choice = CreateWait(brain.ActorId);
                            brain.ApplyDecision(choice);
                        }
                    }

                    decisions.Add(new CrewDecision(
                        brain.ActorId,
                        choice,
                        dialogueToken,
                        interactionLease));
                }

                return decisions;
            }
            catch
            {
                foreach (var decision in decisions)
                {
                    decision.Dispose();
                }

                throw;
            }
        }

        public void SetCinematicControl(bool enabled)
        {
            CinematicControl = enabled;
        }

        private static void ValidateActiveTeam(IReadOnlyList<CrewBrain> activeTeam)
        {
            if (activeTeam.Count != 3 || activeTeam.Any(brain => brain == null) ||
                activeTeam.Count(brain => brain.IsOri) != 1 ||
                activeTeam.Count(brain => !brain.IsOri) != 2 ||
                activeTeam.Select(brain => brain.ActorId).Distinct().Count() != 3)
            {
                throw new InvalidOperationException(
                    "Decision ticks require exactly two unique companions plus Ori.");
            }
        }

        private static CrewActionCandidate CreateWait(ContentId actorId)
        {
            return new CrewActionCandidate(
                $"action.{actorId.Value}.wait",
                CrewActionState.Wait,
                CrewActionPriority.Ambient,
                CrewAttention.None,
                0f,
                Vector2.zero,
                InteractionDepthBand.Gameplay,
                requiresDialogueToken: false);
        }
    }
}
