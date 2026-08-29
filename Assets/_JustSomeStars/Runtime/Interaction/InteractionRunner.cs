using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Animation2D;
using JustSomeStars.Runtime.Core;
using UnityEngine;

namespace JustSomeStars.Runtime.Interaction
{
    public interface IInteractionParticipant2D
    {
        ContentId ActorId { get; }
        InteractionActorKind ActorKind { get; }
        Vector2 Position { get; }
        InteractionFacing Facing { get; }
        InteractionDepthBand DepthBand { get; }
        int AllowedPhysicsLayers { get; }
        GameMode Mode { get; }
        IReadOnlyCollection<ContentId> Tools { get; }

        ValueTask MoveToAsync(
            Vector2 destination,
            CancellationToken cancellationToken);

        ValueTask PlayAsync(
            SpriteAnimationClipDefinition clip,
            CancellationToken cancellationToken);

        void Recover(Vector2 recoveryPosition);
    }

    public readonly struct InteractionAssignment
    {
        public InteractionAssignment(ContentId actorId, ContentId anchorId)
        {
            ActorId = actorId;
            AnchorId = anchorId;
        }

        public ContentId ActorId { get; }
        public ContentId AnchorId { get; }
    }

    public sealed class InteractionRunResult
    {
        internal InteractionRunResult(InteractionAssignment[] assignments)
        {
            Assignments = assignments ??
                throw new ArgumentNullException(nameof(assignments));
        }

        public IReadOnlyList<InteractionAssignment> Assignments { get; }
    }

    public sealed class InteractionRunner
    {
        private const float FacingEpsilon = 0.001f;
        private readonly InteractionReservationService m_Reservations;
        private readonly GameEventBus m_EventBus;

        public InteractionRunner(
            InteractionReservationService reservations,
            GameEventBus eventBus)
        {
            m_Reservations = reservations ??
                throw new ArgumentNullException(nameof(reservations));
            m_EventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        public async Task<InteractionRunResult> RunAsync(
            InteractionDefinition definition,
            IReadOnlyList<IInteractionParticipant2D> participants,
            IReadOnlyList<InteractionAnchor2D> anchors,
            CancellationToken cancellationToken)
        {
            ValidateRequest(definition, participants, anchors);
            var reservations = new List<ReservedParticipant>(participants.Count);
            var participantFailures = new ParticipantFailureState();
            using var executionCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            executionCancellation.CancelAfter(definition.ReservationTimeout);
            var executionToken = executionCancellation.Token;
            try
            {
                foreach (var participant in participants)
                {
                    executionToken.ThrowIfCancellationRequested();
                    var candidates = SelectEligibleAnchors(
                        definition,
                        participant,
                        anchors);
                    ReservedParticipant reserved = null;
                    foreach (var anchor in candidates)
                    {
                        if (m_Reservations.TryReserve(
                                anchor.StableId,
                                participant.ActorId,
                                anchor.IsExclusive,
                                definition.ReservationTimeout,
                                executionToken,
                                out var lease))
                        {
                            reserved = new ReservedParticipant(
                                participant,
                                anchor,
                                lease,
                                definition.GetClip(participant.ActorKind));
                            break;
                        }
                    }

                    if (reserved == null)
                    {
                        throw new InvalidOperationException(
                            $"Interaction '{definition.StableId}' has no available " +
                            $"anchor for actor '{participant.ActorId}'.");
                    }

                    reservations.Add(reserved);
                }

                await Task.WhenAll(reservations.Select(
                    reservation => RunParticipantAndCancelPeersAsync(
                        reservation,
                        executionCancellation,
                        participantFailures)));
                executionToken.ThrowIfCancellationRequested();
                if (reservations.Any(reservation => !reservation.Lease.IsActive))
                {
                    throw new TimeoutException(
                        $"Interaction '{definition.StableId}' exceeded an anchor lease.");
                }

                definition.PublishEvents(m_EventBus);
                return new InteractionRunResult(
                    reservations
                        .Select(reservation => new InteractionAssignment(
                            reservation.Participant.ActorId,
                            reservation.Anchor.StableId))
                        .ToArray());
            }
            catch (Exception exception)
            {
                foreach (var reservation in reservations)
                {
                    reservation.Participant.Recover(
                        reservation.Anchor.RecoveryPosition);
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (participantFailures.FirstFailure is Exception firstFailure)
                {
                    ExceptionDispatchInfo.Capture(firstFailure).Throw();
                }

                if (executionCancellation.IsCancellationRequested)
                {
                    throw new TimeoutException(
                        $"Interaction '{definition.StableId}' exceeded its " +
                        $"{definition.ReservationTimeout.TotalSeconds:0.###}-second " +
                        "execution timeout.",
                        exception);
                }

                throw;
            }
            finally
            {
                foreach (var reservation in reservations)
                {
                    reservation.Lease.Dispose();
                }
            }
        }

        public static IReadOnlyList<InteractionAnchor2D> SelectEligibleAnchors(
            InteractionDefinition definition,
            IInteractionParticipant2D participant,
            IReadOnlyList<InteractionAnchor2D> anchors)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (participant == null)
            {
                throw new ArgumentNullException(nameof(participant));
            }

            if (anchors == null)
            {
                throw new ArgumentNullException(nameof(anchors));
            }

            definition.ValidateOrThrow();
            if (!participant.ActorId.IsValid)
            {
                throw new InvalidOperationException(
                    "Interaction participant requires a valid actor ID.");
            }

            if (!definition.AllowsMode(participant.Mode) ||
                !definition.HasRequiredTool(participant.Tools))
            {
                return Array.Empty<InteractionAnchor2D>();
            }

            var maxDistanceSquared = definition.MaxDistance * definition.MaxDistance;
            var candidates = new List<(InteractionAnchor2D Anchor, float Distance)>();
            foreach (var anchor in anchors)
            {
                if (anchor == null)
                {
                    throw new InvalidOperationException(
                        "Interaction anchor collection contains a null entry.");
                }

                anchor.ValidateOrThrow();
                if (anchor.ActorKind != participant.ActorKind ||
                    anchor.DepthBand != participant.DepthBand ||
                    (participant.AllowedPhysicsLayers &
                        (1 << anchor.PhysicsLayer)) == 0 ||
                    !FacingMatches(anchor, participant))
                {
                    continue;
                }

                var distanceSquared =
                    (anchor.Position - participant.Position).sqrMagnitude;
                if (distanceSquared <= maxDistanceSquared)
                {
                    candidates.Add((anchor, distanceSquared));
                }
            }

            return candidates
                .OrderBy(candidate => candidate.Distance)
                .ThenBy(candidate => candidate.Anchor.StableId.Value,
                    StringComparer.Ordinal)
                .Select(candidate => candidate.Anchor)
                .ToArray();
        }

        private static bool FacingMatches(
            InteractionAnchor2D anchor,
            IInteractionParticipant2D participant)
        {
            if (anchor.RequiredFacing != InteractionFacing.Any &&
                anchor.RequiredFacing != participant.Facing)
            {
                return false;
            }

            if (!anchor.RequireApproachFacing)
            {
                return true;
            }

            var horizontalDelta = anchor.Position.x - participant.Position.x;
            if (Mathf.Abs(horizontalDelta) <= FacingEpsilon)
            {
                return true;
            }

            var required = horizontalDelta > 0f
                ? InteractionFacing.Right
                : InteractionFacing.Left;
            return participant.Facing == required;
        }

        private static async Task RunParticipantAsync(
            ReservedParticipant reservation,
            CancellationToken cancellationToken)
        {
            await reservation.Participant.MoveToAsync(
                reservation.Anchor.Position,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await reservation.Participant.PlayAsync(
                reservation.Clip,
                cancellationToken);
        }

        private static async Task RunParticipantAndCancelPeersAsync(
            ReservedParticipant reservation,
            CancellationTokenSource executionCancellation,
            ParticipantFailureState failures)
        {
            try
            {
                await RunParticipantAsync(
                    reservation,
                    executionCancellation.Token);
            }
            catch (Exception exception)
            {
                if (!executionCancellation.IsCancellationRequested)
                {
                    failures.TryRecord(exception);
                    executionCancellation.Cancel();
                }

                throw;
            }
        }

        private static void ValidateRequest(
            InteractionDefinition definition,
            IReadOnlyList<IInteractionParticipant2D> participants,
            IReadOnlyList<InteractionAnchor2D> anchors)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            definition.ValidateOrThrow();
            if (participants == null || participants.Count == 0)
            {
                throw new ArgumentException(
                    "Interaction requires at least one participant.",
                    nameof(participants));
            }

            if (anchors == null || anchors.Count == 0)
            {
                throw new ArgumentException(
                    "Interaction requires at least one anchor.",
                    nameof(anchors));
            }

            var actorIds = new HashSet<ContentId>();
            foreach (var participant in participants)
            {
                if (participant == null || !participant.ActorId.IsValid)
                {
                    throw new InvalidOperationException(
                        "Interaction participants require valid actor IDs.");
                }

                if (!actorIds.Add(participant.ActorId))
                {
                    throw new InvalidOperationException(
                        $"Interaction actor '{participant.ActorId}' is duplicated.");
                }
            }
        }

        private sealed class ReservedParticipant
        {
            public ReservedParticipant(
                IInteractionParticipant2D participant,
                InteractionAnchor2D anchor,
                InteractionReservationLease lease,
                SpriteAnimationClipDefinition clip)
            {
                Participant = participant;
                Anchor = anchor;
                Lease = lease;
                Clip = clip;
            }

            public IInteractionParticipant2D Participant { get; }
            public InteractionAnchor2D Anchor { get; }
            public InteractionReservationLease Lease { get; }
            public SpriteAnimationClipDefinition Clip { get; }
        }

        private sealed class ParticipantFailureState
        {
            private readonly object m_Gate = new object();
            private Exception m_FirstFailure;

            public Exception FirstFailure
            {
                get
                {
                    lock (m_Gate)
                    {
                        return m_FirstFailure;
                    }
                }
            }

            public void TryRecord(Exception exception)
            {
                lock (m_Gate)
                {
                    m_FirstFailure ??= exception;
                }
            }
        }
    }
}
