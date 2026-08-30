using System;
using UnityEngine;

namespace JustSomeStars.Runtime.Flight
{
    public readonly struct FlightCheckpointSnapshot
    {
        public FlightCheckpointSnapshot(string stableId, int order, FlightState state)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                throw new ArgumentException("A checkpoint ID is required.", nameof(stableId));
            }

            StableId = stableId;
            Order = order;
            State = state;
        }

        public string StableId { get; }
        public int Order { get; }
        public FlightState State { get; }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class FlightCheckpoint : MonoBehaviour
    {
        [SerializeField] private string stableId = "flight.checkpoint";
        [SerializeField] private int order;

        public string StableId => stableId;
        public int Order => order;

        public FlightCheckpointSnapshot Capture(FlightState state)
        {
            return new FlightCheckpointSnapshot(stableId, order, state);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            other.GetComponentInParent<FlightMotor2D>()?.CaptureCheckpoint(this);
        }
    }
}
