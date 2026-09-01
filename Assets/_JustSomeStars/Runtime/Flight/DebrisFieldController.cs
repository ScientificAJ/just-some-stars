using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Missions;
using UnityEngine;

namespace JustSomeStars.Runtime.Flight
{
    public readonly struct DebrisBodyState : IEquatable<DebrisBodyState>
    {
        public DebrisBodyState(
            int stableId,
            int lane,
            Vector2 position,
            Vector2 velocity,
            float rotationDegrees,
            float angularVelocity)
        {
            StableId = stableId;
            Lane = lane;
            Position = position;
            Velocity = velocity;
            RotationDegrees = rotationDegrees;
            AngularVelocity = angularVelocity;
        }

        public int StableId { get; }
        public int Lane { get; }
        public Vector2 Position { get; }
        public Vector2 Velocity { get; }
        public float RotationDegrees { get; }
        public float AngularVelocity { get; }

        public bool Equals(DebrisBodyState other) =>
            StableId == other.StableId &&
            Lane == other.Lane &&
            Position == other.Position &&
            Velocity == other.Velocity &&
            RotationDegrees.Equals(other.RotationDegrees) &&
            AngularVelocity.Equals(other.AngularVelocity);
    }

    public sealed class DebrisFieldCheckpoint
    {
        internal DebrisFieldCheckpoint(
            int tick,
            IReadOnlyList<DebrisBodyState> bodies)
        {
            Tick = tick;
            Bodies = bodies.ToArray();
        }

        public int Tick { get; }
        public IReadOnlyList<DebrisBodyState> Bodies { get; }
    }

    public sealed class DebrisFieldSimulation
    {
        public const int BodyCount = 18;
        public const float FixedDeltaSeconds = 1f / 30f;

        private readonly int m_Seed;
        private readonly List<DebrisBodyState> m_Bodies;
        private readonly HashSet<int> m_ActiveCollisions = new();

        public DebrisFieldSimulation(int seed)
        {
            if (seed <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seed));
            }

            m_Seed = seed;
            m_Bodies = CreateBodies(seed);
        }

        public int Tick { get; private set; }
        public IReadOnlyList<DebrisBodyState> Bodies => m_Bodies.ToArray();
        public int ActiveCollisionCount => m_ActiveCollisions.Count;

        public string StateToken
        {
            get
            {
                var builder = new StringBuilder(1024);
                builder.Append(m_Seed).Append(':').Append(Tick);
                foreach (var body in m_Bodies.OrderBy(item => item.StableId))
                {
                    builder.Append('|')
                        .Append(body.StableId).Append(':')
                        .Append(body.Lane).Append(':')
                        .Append(body.Position.x.ToString("R", CultureInfo.InvariantCulture))
                        .Append(',')
                        .Append(body.Position.y.ToString("R", CultureInfo.InvariantCulture))
                        .Append(':')
                        .Append(body.Velocity.x.ToString("R", CultureInfo.InvariantCulture))
                        .Append(',')
                        .Append(body.Velocity.y.ToString("R", CultureInfo.InvariantCulture))
                        .Append(':')
                        .Append(body.RotationDegrees.ToString(
                            "R", CultureInfo.InvariantCulture))
                        .Append(':')
                        .Append(body.AngularVelocity.ToString(
                            "R", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        public void Step(float steeringAxis)
        {
            if (float.IsNaN(steeringAxis) || float.IsInfinity(steeringAxis))
            {
                throw new ArgumentOutOfRangeException(nameof(steeringAxis));
            }

            var boundedSteering = Mathf.Clamp(steeringAxis, -1f, 1f);
            var routePhase = Tick * FixedDeltaSeconds;
            for (var index = 0; index < m_Bodies.Count; index++)
            {
                var body = m_Bodies[index];
                var relativeWave = Mathf.Sin(
                    routePhase * (0.29f + body.Lane * 0.07f) +
                    body.StableId * 0.61f);
                var velocity = body.Velocity + new Vector2(
                    0f,
                    (relativeWave * 0.015f + boundedSteering * 0.001f) *
                    FixedDeltaSeconds);
                var position = body.Position + velocity * FixedDeltaSeconds;
                if (position.x < -18f)
                {
                    position.x += 36f;
                }
                else if (position.x > 18f)
                {
                    position.x -= 36f;
                }

                m_Bodies[index] = new DebrisBodyState(
                    body.StableId,
                    body.Lane,
                    position,
                    velocity,
                    Mathf.Repeat(
                        body.RotationDegrees +
                        body.AngularVelocity * FixedDeltaSeconds,
                        360f),
                    body.AngularVelocity);
            }

            Tick = checked(Tick + 1);
        }

        public DebrisFieldCheckpoint CaptureCheckpoint() =>
            new(Tick, m_Bodies);

        public void RestoreCheckpoint(DebrisFieldCheckpoint checkpoint)
        {
            if (checkpoint == null || checkpoint.Bodies.Count != BodyCount)
            {
                throw new ArgumentException(
                    "A complete debris checkpoint is required.",
                    nameof(checkpoint));
            }

            Tick = checkpoint.Tick;
            m_Bodies.Clear();
            m_Bodies.AddRange(checkpoint.Bodies);
            m_ActiveCollisions.Clear();
        }

        public void RegisterCollision(int stableId)
        {
            if (stableId < 0 || stableId >= BodyCount)
            {
                throw new ArgumentOutOfRangeException(nameof(stableId));
            }
            m_ActiveCollisions.Add(stableId);
        }

        public void ReleaseCollision(int stableId) =>
            m_ActiveCollisions.Remove(stableId);

        private static List<DebrisBodyState> CreateBodies(int seed)
        {
            var random = new MissionRandom((uint)seed);
            var bodies = new List<DebrisBodyState>(BodyCount);
            for (var stableId = 0; stableId < BodyCount; stableId++)
            {
                var lane = stableId % 3;
                var laneY = (lane - 1) * 2.25f;
                bodies.Add(new DebrisBodyState(
                    stableId,
                    lane,
                    new Vector2(
                        -17f + random.Next01() * 34f,
                        laneY + (random.Next01() - 0.5f) * 1.2f),
                    new Vector2(
                        -2.2f - random.Next01() * 3.8f,
                        (random.Next01() - 0.5f) * 0.32f),
                    random.Next01() * 360f,
                    -48f + random.Next01() * 96f));
            }
            return bodies;
        }

        private struct MissionRandom
        {
            private uint m_State;

            public MissionRandom(uint seed)
            {
                m_State = seed == 0 ? 0xA57E2026u : seed;
            }

            public float Next01()
            {
                var value = m_State;
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
                m_State = value;
                return (value & 0x00FFFFFFu) / 16777216f;
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class DebrisFieldController : MonoBehaviour, IFlightGameplayExtension
    {
        public const int AuthoredSeed = 260826;

        [SerializeField] private FlightMotor2D motor;
        [SerializeField] private Rigidbody2D[] debrisBodies = Array.Empty<Rigidbody2D>();
        [SerializeField] private SpriteRenderer[] debrisRenderers =
            Array.Empty<SpriteRenderer>();
        [SerializeField] private int authoredSeed = AuthoredSeed;
        [SerializeField] private Vector2 routeOrigin;
        [SerializeField] private float routeCheckpointX = 0f;
        [SerializeField] private float routeExitX = 5.4f;

        private DebrisFieldSimulation m_Simulation;
        private DebrisFieldCheckpoint m_Checkpoint;
        private FlightState m_ShipCheckpoint;
        private float m_Accumulator;
        private bool m_MilestonePublished;
        private bool m_EscapeTraversalPrepared;
        private bool m_WasFailurePending;
        private FlightGameplayDependencies m_Dependencies;

        public DebrisFieldSimulation Simulation => m_Simulation;
        public int Seed => authoredSeed;
        public bool IsConfigured => m_Dependencies != null;
        public bool RouteMilestonePublished => m_MilestonePublished;
        public bool CanEscape => m_Simulation != null &&
            m_EscapeTraversalPrepared &&
            m_Simulation.ActiveCollisionCount == 0 &&
            ShipRouteX <= -routeExitX;

        private float ShipRouteX => motor.State.Position.x - routeOrigin.x;

        public void Configure(FlightGameplayDependencies dependencies)
        {
            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }
            if (m_Dependencies != null)
            {
                if (ReferenceEquals(m_Dependencies, dependencies)) return;
                throw new InvalidOperationException(
                    "Debris field is already owned by another composition.");
            }
            if (motor == null || debrisBodies == null || debrisRenderers == null ||
                debrisBodies.Length != DebrisFieldSimulation.BodyCount ||
                debrisRenderers.Length != DebrisFieldSimulation.BodyCount ||
                debrisBodies.Any(item => item == null) ||
                debrisRenderers.Any(item => item == null) || authoredSeed <= 0 ||
                !IsFinite(routeOrigin.x) || !IsFinite(routeOrigin.y) ||
                routeExitX <= routeCheckpointX)
            {
                throw new InvalidOperationException(
                    "Aster requires the complete deterministic debris presentation.");
            }

            m_Dependencies = dependencies;
            m_Simulation = new DebrisFieldSimulation(authoredSeed);
            m_Accumulator = 0f;
            m_MilestonePublished = false;
            m_EscapeTraversalPrepared = false;
            m_WasFailurePending = false;
            Present();
        }

        public void PrepareEscapeTraversal()
        {
            if (m_Dependencies == null || m_Simulation == null)
            {
                throw new InvalidOperationException(
                    "The Aster escape can begin only after the debris field is configured.");
            }

            var start = new FlightState(
                routeOrigin + new Vector2(routeExitX, 0f),
                Vector2.zero,
                motor.State.Lane);
            motor.RestoreCheckpointState(start);
            m_Checkpoint = m_Simulation.CaptureCheckpoint();
            m_ShipCheckpoint = motor.State;
            m_MilestonePublished = true;
            m_EscapeTraversalPrepared = true;
            m_WasFailurePending = false;
        }

        public void Release(FlightGameplayDependencies dependencies)
        {
            if (m_Dependencies == null) return;
            if (!ReferenceEquals(m_Dependencies, dependencies))
            {
                throw new InvalidOperationException(
                    "Debris field can only release its composition owner.");
            }
            m_Dependencies = null;
            m_Simulation = null;
            m_Checkpoint = null;
            m_Accumulator = 0f;
            m_EscapeTraversalPrepared = false;
        }

        private void FixedUpdate()
        {
            if (m_Dependencies == null || m_Simulation == null) return;

            m_Accumulator += Time.fixedDeltaTime;
            while (m_Accumulator >= DebrisFieldSimulation.FixedDeltaSeconds)
            {
                m_Accumulator -= DebrisFieldSimulation.FixedDeltaSeconds;
                m_Simulation.Step(0f);
                if (m_Checkpoint == null &&
                    ShipRouteX >= routeCheckpointX)
                {
                    m_Checkpoint = m_Simulation.CaptureCheckpoint();
                    m_ShipCheckpoint = motor.State;
                }
            }

            if (m_Simulation.ActiveCollisionCount > 0 &&
                !motor.State.FailurePending)
            {
                motor.TriggerSoftFailure();
            }

            if (motor.State.FailurePending && !m_WasFailurePending &&
                m_Checkpoint != null)
            {
                m_Simulation.RestoreCheckpoint(m_Checkpoint);
                motor.RestoreCheckpointState(m_ShipCheckpoint);
            }
            m_WasFailurePending = motor.State.FailurePending;

            if (!m_MilestonePublished &&
                m_Checkpoint != null &&
                m_Simulation.ActiveCollisionCount == 0 &&
                ShipRouteX >= routeExitX)
            {
                m_MilestonePublished = true;
                m_Dependencies.Events.Publish(new TraversalMilestoneReached(
                    new ContentId("route.aster.debris-lane-cleared")));
            }

            Present();
        }

        private void Present()
        {
            if (m_Simulation == null) return;
            var states = m_Simulation.Bodies;
            for (var index = 0; index < states.Count; index++)
            {
                debrisBodies[index].position = routeOrigin + states[index].Position;
                debrisBodies[index].rotation = states[index].RotationDegrees;
                debrisRenderers[index].sortingOrder = 320 + states[index].Lane * 10 +
                    index % 5;
            }
        }

        public void RegisterCollision(int stableId)
        {
            if (m_Simulation == null) return;
            m_Simulation.RegisterCollision(stableId);
        }

        public void ReleaseCollision(int stableId)
        {
            m_Simulation?.ReleaseCollision(stableId);
        }

        public bool CanRecoverFragment(Vector2 fragmentPosition, float radius)
        {
            if (radius <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }
            return m_MilestonePublished &&
                m_Simulation != null &&
                m_Simulation.ActiveCollisionCount == 0 &&
                Vector2.Distance(motor.State.Position, fragmentPosition) <= radius;
        }

        private void OnDestroy()
        {
            if (m_Dependencies != null)
            {
                Release(m_Dependencies);
            }
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

}
