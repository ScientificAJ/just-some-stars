using UnityEngine;

namespace JustSomeStars.Runtime.Player
{
    [CreateAssetMenu(
        fileName = "SurfaceMotor2DConfig",
        menuName = "Just Some Stars/Player/Surface Motor 2D Config")]
    public sealed class SurfaceMotor2DConfig : ScriptableObject
    {
        [SerializeField, Min(0f)] private float moveSpeed = 5f;
        [SerializeField, Min(0f)] private float groundAcceleration = 28f;
        [SerializeField, Min(0f)] private float airAcceleration = 14f;
        [SerializeField, Min(0f)] private float groundDeceleration = 32f;
        [SerializeField, Min(0f)] private float jumpVelocity = 7f;
        [SerializeField, Min(0f)] private float jetAcceleration = 12f;
        [SerializeField, Min(0f)] private float jetDuration = 0.35f;
        [SerializeField, Min(0f)] private float groundProbeDistance = 0.12f;
        [SerializeField, Range(0f, 89f)] private float maximumSlopeAngle = 45f;
        [SerializeField, Min(0f)] private float maximumStepHeight = 0.30f;
        [SerializeField, Min(0f)] private float stepProbeDistance = 0.14f;
        [SerializeField, Min(0f)] private float maxFallSpeed = 18f;

        public float MoveSpeed { get => moveSpeed; set => moveSpeed = Mathf.Max(0f, value); }
        public float GroundAcceleration { get => groundAcceleration; set => groundAcceleration = Mathf.Max(0f, value); }
        public float AirAcceleration { get => airAcceleration; set => airAcceleration = Mathf.Max(0f, value); }
        public float GroundDeceleration { get => groundDeceleration; set => groundDeceleration = Mathf.Max(0f, value); }
        public float JumpVelocity { get => jumpVelocity; set => jumpVelocity = Mathf.Max(0f, value); }
        public float JetAcceleration { get => jetAcceleration; set => jetAcceleration = Mathf.Max(0f, value); }
        public float JetDuration { get => jetDuration; set => jetDuration = Mathf.Max(0f, value); }
        public float GroundProbeDistance { get => groundProbeDistance; set => groundProbeDistance = Mathf.Max(0f, value); }
        public float MaximumSlopeAngle { get => maximumSlopeAngle; set => maximumSlopeAngle = Mathf.Clamp(value, 0f, 89f); }
        public float MaximumStepHeight { get => maximumStepHeight; set => maximumStepHeight = Mathf.Max(0f, value); }
        public float StepProbeDistance { get => stepProbeDistance; set => stepProbeDistance = Mathf.Max(0f, value); }
        public float MaxFallSpeed { get => maxFallSpeed; set => maxFallSpeed = Mathf.Max(0f, value); }
    }
}
