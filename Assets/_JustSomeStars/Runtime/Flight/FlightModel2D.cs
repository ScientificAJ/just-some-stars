using System;
using System.Collections.Generic;
using JustSomeStars.Runtime.Accessibility;
using UnityEngine;

namespace JustSomeStars.Runtime.Flight
{
    [Serializable]
    public readonly struct FlightSimulationConfig
    {
        public FlightSimulationConfig(
            Rect routeEnvelope,
            float acceleration,
            float boostMultiplier,
            float passiveDrag,
            float brakeDeceleration,
            float steeringAcceleration,
            float driftSteeringMultiplier,
            float maximumSpeed,
            float failureRecoverySeconds)
        {
            RouteEnvelope = routeEnvelope;
            Acceleration = acceleration;
            BoostMultiplier = boostMultiplier;
            PassiveDrag = passiveDrag;
            BrakeDeceleration = brakeDeceleration;
            SteeringAcceleration = steeringAcceleration;
            DriftSteeringMultiplier = driftSteeringMultiplier;
            MaximumSpeed = maximumSpeed;
            FailureRecoverySeconds = failureRecoverySeconds;
        }

        public Rect RouteEnvelope { get; }
        public float Acceleration { get; }
        public float BoostMultiplier { get; }
        public float PassiveDrag { get; }
        public float BrakeDeceleration { get; }
        public float SteeringAcceleration { get; }
        public float DriftSteeringMultiplier { get; }
        public float MaximumSpeed { get; }
        public float FailureRecoverySeconds { get; }

        public static FlightSimulationConfig Default => new FlightSimulationConfig(
            new Rect(-12f, -5f, 24f, 10f),
            4.5f,
            1.8f,
            0.08f,
            5.5f,
            3.6f,
            0.42f,
            9f,
            1.25f);
    }

    public readonly struct FlightInputFrame
    {
        public FlightInputFrame(
            Vector2 steering,
            bool primaryHeld,
            bool secondaryHeld,
            int laneDelta = 0)
        {
            Steering = steering;
            PrimaryHeld = primaryHeld;
            SecondaryHeld = secondaryHeld;
            LaneDelta = laneDelta;
        }

        public Vector2 Steering { get; }
        public bool PrimaryHeld { get; }
        public bool SecondaryHeld { get; }
        public int LaneDelta { get; }
    }

    public readonly struct FlightLaneTransition
    {
        public FlightLaneTransition(int fromLane, int toLane)
            : this(fromLane, toLane, 0f)
        {
        }

        public FlightLaneTransition(
            int fromLane,
            int toLane,
            float durationSeconds)
        {
            FromLane = fromLane;
            ToLane = toLane;
            DurationSeconds = durationSeconds;
        }

        public int FromLane { get; }
        public int ToLane { get; }
        public float DurationSeconds { get; }
    }

    public readonly struct GravityAssistSample
    {
        public GravityAssistSample(
            Vector2 center,
            float radius,
            float radialAcceleration,
            float tangentialAcceleration,
            int lane)
        {
            Center = center;
            Radius = radius;
            RadialAcceleration = radialAcceleration;
            TangentialAcceleration = tangentialAcceleration;
            Lane = lane;
        }

        public Vector2 Center { get; }
        public float Radius { get; }
        public float RadialAcceleration { get; }
        public float TangentialAcceleration { get; }
        public int Lane { get; }
    }

    public readonly struct FlightState
    {
        public FlightState(
            Vector2 position,
            Vector2 velocity,
            int lane,
            bool failurePending = false,
            bool landingLocked = false,
            float elapsedSeconds = 0f,
            int laneTransitionTarget = -1,
            float laneTransitionProgress = 0f)
        {
            Position = position;
            Velocity = velocity;
            Lane = lane;
            FailurePending = failurePending;
            LandingLocked = landingLocked;
            ElapsedSeconds = elapsedSeconds;
            LaneTransitionTarget = laneTransitionTarget;
            LaneTransitionProgress = laneTransitionProgress;
        }

        public Vector2 Position { get; }
        public Vector2 Velocity { get; }
        public int Lane { get; }
        public bool FailurePending { get; }
        public bool LandingLocked { get; }
        public float ElapsedSeconds { get; }
        public int LaneTransitionTarget { get; }
        public float LaneTransitionProgress { get; }
    }

    public sealed class FlightModel2D
    {
        private const float MinimumVectorSqrMagnitude = 0.000001f;

        private readonly FlightLaneTransition[] m_Transitions;
        private readonly GravityAssistSample[] m_GravityAssists;
        private readonly FlightAssistProfile m_AssistProfile;

        public FlightModel2D(
            FlightSimulationConfig config,
            AssistLevel assist,
            IReadOnlyList<FlightLaneTransition> transitions,
            IReadOnlyList<GravityAssistSample> gravityAssists)
        {
            Validate(config, assist, transitions, gravityAssists);
            Config = config;
            Assist = assist;
            m_AssistProfile = FlightAssist.For(assist);
            m_Transitions = Copy(transitions);
            m_GravityAssists = Copy(gravityAssists);
        }

        public FlightSimulationConfig Config { get; }

        public AssistLevel Assist { get; }

        public FlightState Step(
            FlightState state,
            FlightInputFrame input,
            float fixedDeltaTime)
        {
            RequireFinite(fixedDeltaTime, nameof(fixedDeltaTime));
            if (fixedDeltaTime <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(fixedDeltaTime));
            }

            RequireFinite(state.Position, nameof(state));
            RequireFinite(state.Velocity, nameof(state));
            RequireFinite(input.Steering, nameof(input));

            var steering = Vector2.ClampMagnitude(input.Steering, 1f);
            var lane = state.Lane;
            var transitionTarget = state.LaneTransitionTarget;
            var transitionProgress = state.LaneTransitionProgress;
            if (transitionTarget < 0 && Mathf.Abs(input.LaneDelta) == 1)
            {
                var requestedLane = lane + Math.Sign(input.LaneDelta);
                var declaration = FindTransition(lane, requestedLane);
                if (declaration.HasValue)
                {
                    if (declaration.Value.DurationSeconds <= 0f)
                    {
                        lane = requestedLane;
                    }
                    else
                    {
                        transitionTarget = requestedLane;
                        transitionProgress = 0f;
                    }
                }
            }

            if (transitionTarget >= 0)
            {
                var declaration = FindTransition(lane, transitionTarget);
                if (!declaration.HasValue || declaration.Value.DurationSeconds <= 0f)
                {
                    lane = transitionTarget;
                    transitionTarget = -1;
                    transitionProgress = 0f;
                }
                else
                {
                    transitionProgress += fixedDeltaTime /
                        declaration.Value.DurationSeconds;
                    if (transitionProgress >= 1f)
                    {
                        lane = transitionTarget;
                        transitionTarget = -1;
                        transitionProgress = 0f;
                    }
                }
            }

            var velocity = state.Velocity;
            var speed = velocity.magnitude;
            var drifting = input.SecondaryHeld &&
                steering.sqrMagnitude > 0.04f;
            var braking = input.SecondaryHeld && !drifting;

            if (steering.sqrMagnitude > MinimumVectorSqrMagnitude)
            {
                var accelerationScale = input.PrimaryHeld
                    ? Config.BoostMultiplier
                    : 1f;
                velocity += steering *
                    (Config.Acceleration * accelerationScale * fixedDeltaTime);

                if (speed > 0.01f)
                {
                    var desiredVelocity = steering.normalized * speed;
                    var response = m_AssistProfile.SteeringCorrection *
                        (drifting ? Config.DriftSteeringMultiplier : 1f);
                    velocity = Vector2.MoveTowards(
                        velocity,
                        desiredVelocity,
                        response * fixedDeltaTime);
                }
            }
            else if (input.PrimaryHeld)
            {
                var direction = speed > 0.01f
                    ? velocity.normalized
                    : Vector2.right;
                velocity += direction *
                    (Config.Acceleration * Config.BoostMultiplier * fixedDeltaTime);
            }

            if (braking)
            {
                velocity = Vector2.ClampMagnitude(
                    velocity,
                    Mathf.Max(0f, velocity.magnitude -
                        Config.BrakeDeceleration * fixedDeltaTime));
            }
            else
            {
                velocity = Vector2.ClampMagnitude(
                    velocity,
                    Mathf.Max(0f, velocity.magnitude -
                        Config.PassiveDrag * fixedDeltaTime));
            }

            velocity += CalculateGravityAcceleration(
                state.Position,
                lane) * fixedDeltaTime;
            velocity += CalculateRouteCorrection(state.Position) * fixedDeltaTime;
            velocity = Vector2.ClampMagnitude(velocity, Config.MaximumSpeed);

            var position = state.Position + velocity * fixedDeltaTime;
            ConstrainToEnvelope(ref position, ref velocity);
            return new FlightState(
                position,
                velocity,
                lane,
                state.FailurePending,
                state.LandingLocked,
                state.ElapsedSeconds + fixedDeltaTime,
                transitionTarget,
                transitionProgress);
        }

        public IReadOnlyList<FlightState> Predict(
            FlightState state,
            FlightInputFrame input,
            float fixedDeltaTime,
            int steps)
        {
            if (steps < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(steps));
            }

            var prediction = new FlightState[steps];
            var current = state;
            for (var index = 0; index < steps; index++)
            {
                current = Step(current, input, fixedDeltaTime);
                prediction[index] = current;
            }

            return prediction;
        }

        public bool IsLaneTransitionDeclared(int fromLane, int toLane)
        {
            return FindTransition(fromLane, toLane).HasValue;
        }

        public static bool IsHazardActiveForLane(int shipLane, int hazardLane)
        {
            return shipLane == hazardLane;
        }

        private Vector2 CalculateGravityAcceleration(Vector2 position, int lane)
        {
            var acceleration = Vector2.zero;
            foreach (var gravity in m_GravityAssists)
            {
                if (gravity.Lane != lane)
                {
                    continue;
                }

                var offset = gravity.Center - position;
                var distance = offset.magnitude;
                if (distance > gravity.Radius || distance <= 0.0001f)
                {
                    continue;
                }

                var falloff = 1f - distance / gravity.Radius;
                var radial = offset / distance;
                var tangent = new Vector2(-radial.y, radial.x);
                acceleration += (radial * gravity.RadialAcceleration +
                    tangent * gravity.TangentialAcceleration) * falloff;
            }

            return acceleration;
        }

        private Vector2 CalculateRouteCorrection(Vector2 position)
        {
            var bounds = Config.RouteEnvelope;
            var correction = Vector2.zero;
            var margin = Mathf.Min(
                m_AssistProfile.SafeMargin,
                Mathf.Min(bounds.width, bounds.height) * 0.49f);
            if (margin <= 0f)
            {
                return correction;
            }

            if (position.x > bounds.xMax - margin)
            {
                correction.x -= Mathf.InverseLerp(
                    bounds.xMax - margin,
                    bounds.xMax,
                    position.x) * m_AssistProfile.RouteCorrection;
            }
            else if (position.x < bounds.xMin + margin)
            {
                correction.x += Mathf.InverseLerp(
                    bounds.xMin + margin,
                    bounds.xMin,
                    position.x) * m_AssistProfile.RouteCorrection;
            }

            if (position.y > bounds.yMax - margin)
            {
                correction.y -= Mathf.InverseLerp(
                    bounds.yMax - margin,
                    bounds.yMax,
                    position.y) * m_AssistProfile.RouteCorrection;
            }
            else if (position.y < bounds.yMin + margin)
            {
                correction.y += Mathf.InverseLerp(
                    bounds.yMin + margin,
                    bounds.yMin,
                    position.y) * m_AssistProfile.RouteCorrection;
            }

            return correction;
        }

        private void ConstrainToEnvelope(ref Vector2 position, ref Vector2 velocity)
        {
            var bounds = Config.RouteEnvelope;
            if (position.x < bounds.xMin)
            {
                position.x = bounds.xMin;
                velocity.x = Mathf.Max(0f, velocity.x);
            }
            else if (position.x > bounds.xMax)
            {
                position.x = bounds.xMax;
                velocity.x = Mathf.Min(0f, velocity.x);
            }

            if (position.y < bounds.yMin)
            {
                position.y = bounds.yMin;
                velocity.y = Mathf.Max(0f, velocity.y);
            }
            else if (position.y > bounds.yMax)
            {
                position.y = bounds.yMax;
                velocity.y = Mathf.Min(0f, velocity.y);
            }
        }

        private FlightLaneTransition? FindTransition(int fromLane, int toLane)
        {
            foreach (var transition in m_Transitions)
            {
                if (transition.FromLane == fromLane &&
                    transition.ToLane == toLane)
                {
                    return transition;
                }
            }

            return null;
        }

        private static T[] Copy<T>(IReadOnlyList<T> source)
        {
            var result = new T[source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                result[index] = source[index];
            }

            return result;
        }

        private static void Validate(
            FlightSimulationConfig config,
            AssistLevel assist,
            IReadOnlyList<FlightLaneTransition> transitions,
            IReadOnlyList<GravityAssistSample> gravityAssists)
        {
            if (!Enum.IsDefined(typeof(AssistLevel), assist))
            {
                throw new ArgumentOutOfRangeException(nameof(assist));
            }

            if (transitions == null)
            {
                throw new ArgumentNullException(nameof(transitions));
            }

            if (gravityAssists == null)
            {
                throw new ArgumentNullException(nameof(gravityAssists));
            }

            RequireFinite(config.RouteEnvelope.xMin, nameof(config));
            RequireFinite(config.RouteEnvelope.yMin, nameof(config));
            RequirePositive(config.RouteEnvelope.width, nameof(config));
            RequirePositive(config.RouteEnvelope.height, nameof(config));
            RequirePositive(config.Acceleration, nameof(config));
            RequirePositive(config.BoostMultiplier, nameof(config));
            RequireFinite(config.PassiveDrag, nameof(config));
            RequirePositive(config.BrakeDeceleration, nameof(config));
            RequirePositive(config.SteeringAcceleration, nameof(config));
            RequirePositive(config.DriftSteeringMultiplier, nameof(config));
            RequirePositive(config.MaximumSpeed, nameof(config));
            RequirePositive(config.FailureRecoverySeconds, nameof(config));

            foreach (var transition in transitions)
            {
                if (transition.FromLane < 0 || transition.ToLane < 0 ||
                    transition.FromLane == transition.ToLane ||
                    !float.IsFinite(transition.DurationSeconds) ||
                    transition.DurationSeconds < 0f)
                {
                    throw new ArgumentOutOfRangeException(nameof(transitions));
                }
            }

            foreach (var gravity in gravityAssists)
            {
                RequireFinite(gravity.Center, nameof(gravityAssists));
                RequirePositive(gravity.Radius, nameof(gravityAssists));
                RequireFinite(gravity.RadialAcceleration, nameof(gravityAssists));
                RequireFinite(gravity.TangentialAcceleration, nameof(gravityAssists));
                if (gravity.Lane < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(gravityAssists));
                }
            }
        }

        private static void RequireFinite(Vector2 value, string parameterName)
        {
            RequireFinite(value.x, parameterName);
            RequireFinite(value.y, parameterName);
        }

        private static void RequirePositive(float value, string parameterName)
        {
            RequireFinite(value, parameterName);
            if (value <= 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static void RequireFinite(float value, string parameterName)
        {
            if (!float.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}
