using System;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Input;
using UnityEngine;

namespace JustSomeStars.Runtime.Player
{
    public readonly struct SurfaceMotorState
    {
        public SurfaceMotorState(
            bool isGrounded,
            bool isJetActive,
            float remainingJetSeconds,
            Vector2 groundNormal,
            Vector2 relativeVelocity,
            Vector2 activeSurfaceVelocity,
            Vector2 externalAcceleration)
        {
            IsGrounded = isGrounded;
            IsJetActive = isJetActive;
            RemainingJetSeconds = remainingJetSeconds;
            GroundNormal = groundNormal;
            RelativeVelocity = relativeVelocity;
            ActiveSurfaceVelocity = activeSurfaceVelocity;
            ExternalAcceleration = externalAcceleration;
        }

        public bool IsGrounded { get; }
        public bool IsJetActive { get; }
        public float RemainingJetSeconds { get; }
        public Vector2 GroundNormal { get; }
        public Vector2 RelativeVelocity { get; }
        public Vector2 ActiveSurfaceVelocity { get; }
        public Vector2 ExternalAcceleration { get; }
    }

    [DisallowMultipleComponent]
    public sealed class SurfaceMotor2D : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Collider2D bodyCollider;
        [SerializeField] private SurfaceMotor2DConfig config;
        [SerializeField] private LayerMask groundMask = ~0;

        private Vector2 moveInput;
        private Vector2 manualSurfaceVelocity;
        private Vector2 detectedSurfaceVelocity;
        private Vector2 appliedSurfaceVelocity;
        private Vector2 pendingExternalVelocity;
        private Vector2 externalAcceleration;
        private Vector2 groundNormal = Vector2.up;
        private Vector2 recoveryAnchor;
        private float remainingJetSeconds;
        private bool jumpRequested;
        private bool jetHeld;
        private InputRouter inputRouter;
        private SettingsService settingsService;

        public bool IsGrounded { get; private set; }
        public float RemainingJetSeconds => remainingJetSeconds;
        public SurfaceMotorState State => new SurfaceMotorState(
            IsGrounded,
            jetHeld && !IsGrounded && remainingJetSeconds > 0f,
            remainingJetSeconds,
            groundNormal,
            body != null ? body.linearVelocity - appliedSurfaceVelocity : Vector2.zero,
            detectedSurfaceVelocity + manualSurfaceVelocity,
            externalAcceleration);

        public void Configure(
            Rigidbody2D targetBody,
            Collider2D targetCollider,
            SurfaceMotor2DConfig motorConfig)
        {
            body = targetBody != null
                ? targetBody
                : throw new ArgumentNullException(nameof(targetBody));
            bodyCollider = targetCollider != null
                ? targetCollider
                : throw new ArgumentNullException(nameof(targetCollider));
            config = motorConfig != null
                ? motorConfig
                : throw new ArgumentNullException(nameof(motorConfig));
            remainingJetSeconds = config.JetDuration;
            recoveryAnchor = body.position;
        }

        public void BindInput(InputRouter router, SettingsService settings)
        {
            if (inputRouter != null)
            {
                inputRouter.GameplayCommandPerformed -= OnGameplayCommand;
            }

            inputRouter = router ?? throw new ArgumentNullException(nameof(router));
            settingsService = settings ?? throw new ArgumentNullException(nameof(settings));
            inputRouter.GameplayCommandPerformed += OnGameplayCommand;
        }

        public void ReleaseInput(InputRouter router)
        {
            if (router == null)
            {
                throw new ArgumentNullException(nameof(router));
            }

            if (inputRouter == null)
            {
                return;
            }

            if (!ReferenceEquals(inputRouter, router))
            {
                throw new InvalidOperationException(
                    "SurfaceMotor2D can only release its bound InputRouter.");
            }

            inputRouter.GameplayCommandPerformed -= OnGameplayCommand;
            inputRouter = null;
            settingsService = null;
            moveInput = Vector2.zero;
            jumpRequested = false;
            jetHeld = false;
        }

        public void SetMoveInput(Vector2 input)
        {
            moveInput = Vector2.ClampMagnitude(input, 1f);
        }

        public void RequestJump()
        {
            jumpRequested = true;
        }

        public void SetJetHeld(bool held)
        {
            jetHeld = held;
        }

        public void SetMovingSurfaceVelocity(Vector2 velocity)
        {
            manualSurfaceVelocity = velocity;
        }

        public void SetExternalAcceleration(Vector2 acceleration)
        {
            externalAcceleration = acceleration;
        }

        public void AddExternalVelocity(Vector2 velocity)
        {
            pendingExternalVelocity += velocity;
        }

        public void SetRecoveryAnchor(Vector2 anchor)
        {
            recoveryAnchor = anchor;
        }

        public void Recover()
        {
            RequireConfiguration();
            body.position = recoveryAnchor;
            body.linearVelocity = Vector2.zero;
            appliedSurfaceVelocity = Vector2.zero;
            manualSurfaceVelocity = Vector2.zero;
            detectedSurfaceVelocity = Vector2.zero;
            pendingExternalVelocity = Vector2.zero;
            externalAcceleration = Vector2.zero;
            jumpRequested = false;
            jetHeld = false;
            IsGrounded = false;
            groundNormal = Vector2.up;
            remainingJetSeconds = config.JetDuration;
        }

        public void Simulate(float deltaTime)
        {
            RequireConfiguration();
            if (deltaTime <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }

            SampleGround();
            TryResolveStep();
            var velocity = body.linearVelocity - appliedSurfaceVelocity;
            var targetDirection = IsGrounded
                ? ProjectInputAlongGround(moveInput, groundNormal)
                : new Vector2(moveInput.x, 0f);
            var targetX = targetDirection.x * config.MoveSpeed;
            var acceleration = Mathf.Abs(moveInput.x) > 0.001f
                ? (IsGrounded ? config.GroundAcceleration : config.AirAcceleration)
                : IsGrounded
                    ? config.GroundDeceleration
                    : 0f;
            if (acceleration > 0f)
            {
                velocity.x = Mathf.MoveTowards(
                    velocity.x,
                    targetX,
                    acceleration * deltaTime);
            }

            if (IsGrounded && Mathf.Abs(moveInput.x) > 0.001f)
            {
                velocity.y = targetDirection.y * config.MoveSpeed;
            }

            var jumpedThisStep = jumpRequested && IsGrounded;
            if (jumpedThisStep)
            {
                velocity.y = config.JumpVelocity;
                remainingJetSeconds = config.JetDuration;
            }

            if (jetHeld && !IsGrounded && remainingJetSeconds > 0f)
            {
                var used = Mathf.Min(deltaTime, remainingJetSeconds);
                velocity.y += config.JetAcceleration * used;
                remainingJetSeconds -= used;
            }

            velocity += externalAcceleration * deltaTime;
            velocity += pendingExternalVelocity;
            pendingExternalVelocity = Vector2.zero;
            velocity.y = Mathf.Max(velocity.y, -config.MaxFallSpeed);
            var currentSurfaceVelocity = detectedSurfaceVelocity +
                manualSurfaceVelocity;
            body.linearVelocity = velocity + currentSurfaceVelocity;
            appliedSurfaceVelocity = jumpedThisStep
                ? Vector2.zero
                : currentSurfaceVelocity;
            jumpRequested = false;
        }

        public bool IsSurfaceWalkable(Vector2 surfaceNormal)
        {
            RequireConfiguration();
            return surfaceNormal.sqrMagnitude > 0.0001f &&
                Vector2.Angle(surfaceNormal, Vector2.up) <=
                config.MaximumSlopeAngle + 0.0001f;
        }

        public bool CanTraverseStep(float height)
        {
            RequireConfiguration();
            return height >= 0f && height <= config.MaximumStepHeight + 0.0001f;
        }

        public static Vector2 ProjectInputAlongGround(
            Vector2 input,
            Vector2 surfaceNormal)
        {
            if (surfaceNormal.sqrMagnitude < 0.0001f)
            {
                return input;
            }

            var normalized = surfaceNormal.normalized;
            return input - normalized * Vector2.Dot(input, normalized);
        }

        private void FixedUpdate()
        {
            if (body == null || bodyCollider == null || config == null)
            {
                return;
            }

            if (inputRouter != null && inputRouter.IsInitialized)
            {
                var sensitivity = settingsService != null &&
                    settingsService.IsInitialized
                    ? settingsService.Current.TouchSensitivity
                    : 1f;
                SetMoveInput(inputRouter.ReadMove() * sensitivity);
                SetJetHeld(inputRouter.IsCommandPressed(
                    SemanticGameplayCommand.Secondary));
            }

            Simulate(Time.fixedDeltaTime);
        }

        private void SampleGround()
        {
            Physics2D.SyncTransforms();
            var bounds = bodyCollider.bounds;
            var distance = bounds.extents.y + config.GroundProbeDistance;
            var hits = Physics2D.RaycastAll(
                bounds.center,
                Vector2.down,
                distance,
                groundMask);
            IsGrounded = false;
            groundNormal = Vector2.up;
            detectedSurfaceVelocity = Vector2.zero;
            foreach (var hit in hits)
            {
                if (!IsValidGroundHit(hit) || !IsSurfaceWalkable(hit.normal))
                {
                    continue;
                }

                IsGrounded = true;
                groundNormal = hit.normal;
                if (hit.rigidbody != null)
                {
                    detectedSurfaceVelocity = hit.rigidbody.linearVelocity;
                }
                break;
            }
        }

        private void TryResolveStep()
        {
            if (!IsGrounded || Mathf.Abs(moveInput.x) < 0.001f ||
                config.MaximumStepHeight <= 0f || config.StepProbeDistance <= 0f)
            {
                return;
            }

            var bounds = bodyCollider.bounds;
            var direction = Mathf.Sign(moveInput.x);
            var horizontal = Vector2.right * direction;
            var forwardDistance = bounds.extents.x + config.StepProbeDistance;
            var lowerOrigin = new Vector2(
                bounds.center.x,
                bounds.min.y + Mathf.Max(0.02f, config.GroundProbeDistance * 0.25f));
            var lowerHits = Physics2D.RaycastAll(
                lowerOrigin,
                horizontal,
                forwardDistance,
                groundMask);
            var obstacle = Array.Find(lowerHits, IsValidGroundHit);
            if (obstacle.collider == null ||
                obstacle.collider is EdgeCollider2D)
            {
                return;
            }

            var upperOrigin = lowerOrigin + Vector2.up *
                (config.MaximumStepHeight + config.GroundProbeDistance);
            var upperHits = Physics2D.RaycastAll(
                upperOrigin,
                horizontal,
                forwardDistance,
                groundMask);
            if (Array.Exists(upperHits, IsValidGroundHit))
            {
                return;
            }

            var landingOrigin = new Vector2(
                bounds.center.x + direction * forwardDistance,
                bounds.min.y + config.MaximumStepHeight +
                config.GroundProbeDistance);
            var landingHits = Physics2D.RaycastAll(
                landingOrigin,
                Vector2.down,
                config.MaximumStepHeight + config.GroundProbeDistance * 2f,
                groundMask);
            foreach (var hit in landingHits)
            {
                if (!IsValidGroundHit(hit) || !IsSurfaceWalkable(hit.normal))
                {
                    continue;
                }

                var rise = hit.point.y - bounds.min.y;
                if (rise > 0.001f && CanTraverseStep(rise))
                {
                    body.position += Vector2.up * rise;
                    Physics2D.SyncTransforms();
                }
                return;
            }
        }

        private bool IsValidGroundHit(RaycastHit2D hit)
        {
            if (hit.collider == null || hit.collider == bodyCollider ||
                hit.collider.isTrigger)
            {
                return false;
            }

            var otherMotor = hit.collider.GetComponentInParent<SurfaceMotor2D>();
            return otherMotor == null || ReferenceEquals(otherMotor, this);
        }

        private void OnGameplayCommand(
            GameplayInputMode mode,
            SemanticGameplayCommand command)
        {
            if (mode != GameplayInputMode.Surface)
            {
                return;
            }

            if (command == SemanticGameplayCommand.Secondary)
            {
                RequestJump();
                SetJetHeld(true);
            }
        }

        private void OnDestroy()
        {
            if (inputRouter != null)
            {
                ReleaseInput(inputRouter);
            }
        }

        private void RequireConfiguration()
        {
            if (body == null || bodyCollider == null || config == null)
            {
                throw new InvalidOperationException(
                    "SurfaceMotor2D must be configured before simulation.");
            }
        }
    }
}
