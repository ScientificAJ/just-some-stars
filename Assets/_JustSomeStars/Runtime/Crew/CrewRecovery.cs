using System;
using UnityEngine;

namespace JustSomeStars.Runtime.Crew
{
    public enum CrewRecoveryKind
    {
        None = 0,
        Repath = 1,
        HiddenWarp = 2,
    }

    public readonly struct CrewRecoveryContext
    {
        public CrewRecoveryContext(
            Vector2 actorPosition,
            Vector2 recoveryPosition,
            bool cameraVisible,
            bool recoveryPositionVisible,
            float blockedSeconds,
            float remainingRouteDistance,
            bool routeAvailable)
        {
            ActorPosition = actorPosition;
            RecoveryPosition = recoveryPosition;
            CameraVisible = cameraVisible;
            RecoveryPositionVisible = recoveryPositionVisible;
            BlockedSeconds = blockedSeconds;
            RemainingRouteDistance = remainingRouteDistance;
            RouteAvailable = routeAvailable;
        }

        public Vector2 ActorPosition { get; }
        public Vector2 RecoveryPosition { get; }
        public bool CameraVisible { get; }
        public bool RecoveryPositionVisible { get; }
        public float BlockedSeconds { get; }
        public float RemainingRouteDistance { get; }
        public bool RouteAvailable { get; }
    }

    public readonly struct CrewRecoveryDecision
    {
        public CrewRecoveryDecision(
            CrewRecoveryKind kind,
            Vector2 position,
            CrewActionState nextState)
        {
            Kind = kind;
            Position = position;
            NextState = nextState;
        }

        public CrewRecoveryKind Kind { get; }
        public Vector2 Position { get; }
        public CrewActionState NextState { get; }
        public bool AllowsTeleport => Kind == CrewRecoveryKind.HiddenWarp;
    }

    public sealed class CrewRecovery
    {
        private readonly float m_BlockedSecondsBeforeRecovery;
        private readonly float m_MaximumRouteDistance;

        public CrewRecovery(
            float blockedSecondsBeforeRecovery,
            float maximumRouteDistance)
        {
            if (!IsPositiveFinite(blockedSecondsBeforeRecovery) ||
                !IsPositiveFinite(maximumRouteDistance))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(blockedSecondsBeforeRecovery),
                    "Crew recovery thresholds must be positive and finite.");
            }

            m_BlockedSecondsBeforeRecovery = blockedSecondsBeforeRecovery;
            m_MaximumRouteDistance = maximumRouteDistance;
        }

        public CrewRecoveryDecision Evaluate(CrewRecoveryContext context)
        {
            Validate(context);
            var needsRecovery = !context.RouteAvailable ||
                context.BlockedSeconds >= m_BlockedSecondsBeforeRecovery ||
                context.RemainingRouteDistance > m_MaximumRouteDistance;
            if (!needsRecovery)
            {
                return new CrewRecoveryDecision(
                    CrewRecoveryKind.None,
                    context.ActorPosition,
                    CrewActionState.Follow);
            }

            if (context.CameraVisible || context.RecoveryPositionVisible)
            {
                return new CrewRecoveryDecision(
                    CrewRecoveryKind.Repath,
                    context.ActorPosition,
                    CrewActionState.Traverse);
            }

            return new CrewRecoveryDecision(
                CrewRecoveryKind.HiddenWarp,
                context.RecoveryPosition,
                CrewActionState.Recover);
        }

        private static void Validate(CrewRecoveryContext context)
        {
            if (!IsFinite(context.ActorPosition.x) ||
                !IsFinite(context.ActorPosition.y) ||
                !IsFinite(context.RecoveryPosition.x) ||
                !IsFinite(context.RecoveryPosition.y) ||
                context.BlockedSeconds < 0f ||
                context.RemainingRouteDistance < 0f ||
                !IsFinite(context.BlockedSeconds) ||
                !IsFinite(context.RemainingRouteDistance))
            {
                throw new InvalidOperationException(
                    "Crew recovery context has invalid measured values.");
            }
        }

        private static bool IsPositiveFinite(float value)
        {
            return value > 0f && IsFinite(value);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
