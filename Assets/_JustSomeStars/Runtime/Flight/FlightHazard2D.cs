using UnityEngine;

namespace JustSomeStars.Runtime.Flight
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class FlightHazard2D : MonoBehaviour
    {
        [SerializeField] private int lane = 2;

        public int Lane => lane;

        private void OnTriggerEnter2D(Collider2D other)
        {
            var motor = other.GetComponentInParent<FlightMotor2D>();
            if (motor != null &&
                FlightModel2D.IsHazardActiveForLane(motor.State.Lane, lane))
            {
                motor.TriggerSoftFailure();
            }
        }
    }
}
