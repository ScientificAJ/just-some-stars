using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Interaction;
using UnityEngine;

namespace JustSomeStars.Runtime.Crew
{
    public interface ICrewActionRuntime2D
    {
        ContentId CurrentTraversalNodeId { get; }
        bool CameraVisible { get; }
        void Join(Vector2 position);
        void Follow(Vector2 position);
        void Position(Vector2 position);
        void Traverse(IReadOnlyList<TraversalNode2D> path);
        void Investigate(Vector2 position);
        void Interact(Vector2 position);
        void React(Vector2 position);
        void Speak();
        void Converse();
        void EnterCinematic();
        void Wait();
        void Recover(Vector2 position);
    }

    public sealed class CrewBrain
    {
        private readonly CrewPersonality m_Personality;

        public CrewBrain(CrewPersonality personality)
        {
            m_Personality = personality ??
                throw new ArgumentNullException(nameof(personality));
            m_Personality.ValidateOrThrow();
            CurrentState = CrewActionState.Join;
        }

        public ContentId ActorId => m_Personality.StableId;
        public CrewRole Role => m_Personality.Role;
        public bool IsOri => m_Personality.IsOri;
        public CrewActionState CurrentState { get; private set; }
        public CrewActionCandidate CurrentAction { get; private set; }

        public CrewActionCandidate Decide(
            IEnumerable<CrewActionCandidate> candidates,
            bool cinematicControl)
        {
            if (cinematicControl)
            {
                CurrentState = CrewActionState.Cinematic;
                CurrentAction = null;
                return null;
            }

            CurrentAction = CrewUtility.Select(candidates, m_Personality);
            CurrentState = CurrentAction.State;
            return CurrentAction;
        }

        public CrewActionCandidate DecideFromPerceptions(
            IEnumerable<CrewPerception> perceptions,
            bool cinematicControl)
        {
            if (perceptions == null)
            {
                throw new ArgumentNullException(nameof(perceptions));
            }

            return Decide(
                perceptions.Select(perception => perception?.ToCandidate() ??
                    throw new InvalidOperationException(
                        "Crew perception collection contains null.")),
                cinematicControl);
        }

        public bool TryReserveInteraction(
            InteractionReservationService reservations,
            ContentId anchorId,
            bool exclusive,
            TimeSpan timeout,
            CancellationToken cancellationToken,
            out InteractionReservationLease lease)
        {
            if (reservations == null)
            {
                throw new ArgumentNullException(nameof(reservations));
            }

            return reservations.TryReserve(
                anchorId,
                ActorId,
                exclusive,
                timeout,
                cancellationToken,
                out lease);
        }

        public void Execute(
            CrewDecision decision,
            ICrewActionRuntime2D runtime,
            TraversalGraph2D traversalGraph = null)
        {
            if (decision == null || runtime == null)
            {
                throw new ArgumentNullException(
                    decision == null ? nameof(decision) : nameof(runtime));
            }

            if (decision.ActorId != ActorId)
            {
                throw new InvalidOperationException(
                    "Crew brain cannot execute another actor's decision.");
            }

            var action = decision.Action;
            switch (action.State)
            {
                case CrewActionState.Join:
                    runtime.Join(action.TargetPosition);
                    break;
                case CrewActionState.Follow:
                    runtime.Follow(action.TargetPosition);
                    break;
                case CrewActionState.Position:
                    runtime.Position(action.TargetPosition);
                    break;
                case CrewActionState.Traverse:
                    if (traversalGraph == null ||
                        string.IsNullOrWhiteSpace(action.TargetTraversalNodeId))
                    {
                        throw new InvalidOperationException(
                            "Traverse requires an authored graph and target node.");
                    }

                    var path = traversalGraph.FindPath(
                        runtime.CurrentTraversalNodeId,
                        new ContentId(action.TargetTraversalNodeId));
                    if (path.Count == 0)
                    {
                        throw new InvalidOperationException(
                            "No authored 2D traversal route reaches the target.");
                    }

                    runtime.Traverse(path);
                    break;
                case CrewActionState.Investigate:
                    runtime.Investigate(action.TargetPosition);
                    break;
                case CrewActionState.Interact:
                    if (decision.InteractionLease?.IsActive != true)
                    {
                        throw new InvalidOperationException(
                            "Interact requires an active reserved anchor lease.");
                    }

                    runtime.Interact(action.TargetPosition);
                    break;
                case CrewActionState.React:
                    runtime.React(action.TargetPosition);
                    break;
                case CrewActionState.Speak:
                    RequireDialogue(decision);
                    runtime.Speak();
                    break;
                case CrewActionState.Conversation:
                    RequireDialogue(decision);
                    runtime.Converse();
                    break;
                case CrewActionState.Cinematic:
                    runtime.EnterCinematic();
                    break;
                case CrewActionState.Wait:
                    runtime.Wait();
                    break;
                case CrewActionState.Recover:
                    if (runtime.CameraVisible || action.TargetCameraVisible)
                    {
                        throw new InvalidOperationException(
                            "Recovery warp requires hidden actor and destination.");
                    }

                    runtime.Recover(action.TargetPosition);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action.State));
            }

            ApplyDecision(action);
        }

        public void SetState(CrewActionState state)
        {
            if (!Enum.IsDefined(typeof(CrewActionState), state))
            {
                throw new ArgumentOutOfRangeException(nameof(state));
            }

            CurrentState = state;
            if (state == CrewActionState.Recover ||
                state == CrewActionState.Wait ||
                state == CrewActionState.Follow ||
                state == CrewActionState.Cinematic)
            {
                CurrentAction = null;
            }
        }

        internal void ApplyDecision(CrewActionCandidate action)
        {
            CurrentAction = action ?? throw new ArgumentNullException(nameof(action));
            CurrentState = action.State;
        }

        private static void RequireDialogue(CrewDecision decision)
        {
            if (decision.DialogueToken?.IsActive != true)
            {
                throw new InvalidOperationException(
                    "Speaking actions require the Director's active dialogue token.");
            }
        }
    }
}
