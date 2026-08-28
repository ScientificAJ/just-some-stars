using System;
using UnityEngine;

namespace JustSomeStars.Runtime.Player
{
    [DisallowMultipleComponent]
    public sealed class SurfaceRecovery2D : MonoBehaviour
    {
        [SerializeField] private SurfaceMotor2D motor;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Vector2 safeAnchor;
        [SerializeField] private float fallThreshold = -8f;

        public int RecoveryCount { get; private set; }
        public Vector2 SafeAnchor => safeAnchor;
        public float FallThreshold => fallThreshold;

        public void Configure(
            SurfaceMotor2D configuredMotor,
            Rigidbody2D configuredBody,
            Vector2 configuredSafeAnchor,
            float configuredFallThreshold)
        {
            motor = configuredMotor != null
                ? configuredMotor
                : throw new ArgumentNullException(nameof(configuredMotor));
            body = configuredBody != null
                ? configuredBody
                : throw new ArgumentNullException(nameof(configuredBody));
            if (configuredFallThreshold >= configuredSafeAnchor.y)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(configuredFallThreshold),
                    "Fall recovery must be below the safe anchor.");
            }
            safeAnchor = configuredSafeAnchor;
            fallThreshold = configuredFallThreshold;
            motor.SetRecoveryAnchor(safeAnchor);
        }

        public void EvaluateNow()
        {
            if (motor == null || body == null)
            {
                throw new InvalidOperationException(
                    "SurfaceRecovery2D must be configured before evaluation.");
            }
            if (body.position.y >= fallThreshold)
            {
                return;
            }
            motor.SetRecoveryAnchor(safeAnchor);
            motor.Recover();
            RecoveryCount++;
        }

        private void FixedUpdate()
        {
            if (motor != null && body != null)
            {
                EvaluateNow();
            }
        }
    }
}
