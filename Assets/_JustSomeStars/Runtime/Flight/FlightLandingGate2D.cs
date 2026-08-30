using System;
using UnityEngine;

namespace JustSomeStars.Runtime.Flight
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class FlightLandingGate2D : MonoBehaviour
    {
        [SerializeField] private int requiredLane = 1;
        [SerializeField] private float maximumApproachSpeed = 4f;

        private async void OnTriggerEnter2D(Collider2D other)
        {
            var motor = other.GetComponentInParent<FlightMotor2D>();
            var landing = other.GetComponentInParent<LandingSequence>();
            if (motor == null || landing == null)
            {
                return;
            }

            var valid = motor.State.Lane == requiredLane &&
                motor.State.Velocity.magnitude <= maximumApproachSpeed;
            try
            {
                await landing.TryLandAsync(valid, destroyCancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Scene/object teardown is an expected cancellation boundary.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }
}
