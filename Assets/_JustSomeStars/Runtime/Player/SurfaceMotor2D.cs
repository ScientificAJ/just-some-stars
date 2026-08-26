using System;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Input;
using UnityEngine;

namespace JustSomeStars.Runtime.Player
{
    [DisallowMultipleComponent]
    public sealed class SurfaceMotor2D : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Collider2D bodyCollider;
        [SerializeField] private SurfaceMotor2DConfig config;
        [SerializeField] private LayerMask groundMask = ~0;

        private Vector2 moveInput;
        private Vector2 movingSurfaceVelocity;
        private Vector2 appliedSurfaceVelocity;
        private Vector2 pendingExternalVelocity;
        private Vector2 groundNormal = Vector2.up;
        private Vector2 recoveryAnchor;
        private float remainingJetSeconds;
        private bool jumpRequested;
        private bool jetHeld;
        private InputRouter inputRouter;
        private SettingsService settingsService;

        public bool IsGrounded { get; private set; }
        public float RemainingJetSeconds => remainingJetSeconds;

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
            movingSurfaceVelocity = velocity;
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
            pendingExternalVelocity = Vector2.zero;
            jumpRequested = false;
            jetHeld = false;
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
            var velocity = body.linearVelocity - appliedSurfaceVelocity;
            var targetDirection = IsGrounded
                ? ProjectInputAlongGround(moveInput, groundNormal)
                : new Vector2(moveInput.x, 0f);
            var targetX = targetDirection.x * config.MoveSpeed;
            var acceleration = Mathf.Abs(moveInput.x) > 0.001f
                ? (IsGrounded ? config.GroundAcceleration : config.AirAcceleration)
                : config.GroundDeceleration;
            velocity.x = Mathf.MoveTowards(
                velocity.x,
                targetX,
                acceleration * deltaTime);

            if (IsGrounded && Mathf.Abs(moveInput.x) > 0.001f)
            {
                velocity.y = targetDirection.y * config.MoveSpeed;
            }

            if (jumpRequested && IsGrounded)
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

            velocity.y = Mathf.Max(velocity.y, -config.MaxFallSpeed);
            velocity += pendingExternalVelocity;
            pendingExternalVelocity = Vector2.zero;
            appliedSurfaceVelocity = movingSurfaceVelocity;
            body.linearVelocity = velocity + appliedSurfaceVelocity;
            jumpRequested = false;
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
            foreach (var hit in hits)
            {
                if (hit.collider == null || hit.collider == bodyCollider)
                {
                    continue;
                }

                IsGrounded = true;
                groundNormal = hit.normal;
                break;
            }
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
