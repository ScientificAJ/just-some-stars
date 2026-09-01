using System;
using UnityEngine;

namespace JustSomeStars.Runtime.Flight
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class DebrisHazard2D : MonoBehaviour
    {
        [SerializeField] private DebrisFieldController owner;
        [SerializeField] private int stableId;
        [SerializeField] private int lane;

        private bool m_Registered;

        public void Configure(DebrisFieldController field, int id, int depthLane)
        {
            owner = field ?? throw new ArgumentNullException(nameof(field));
            if (id < 0 || id >= DebrisFieldSimulation.BodyCount)
            {
                throw new ArgumentOutOfRangeException(nameof(id));
            }
            if (depthLane < 0 || depthLane > 2)
            {
                throw new ArgumentOutOfRangeException(nameof(depthLane));
            }
            stableId = id;
            lane = depthLane;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var motor = other.GetComponentInParent<FlightMotor2D>();
            if (motor == null ||
                !FlightModel2D.IsHazardActiveForLane(motor.State.Lane, lane))
            {
                return;
            }
            owner.RegisterCollision(stableId);
            m_Registered = true;
            motor.TriggerSoftFailure();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!m_Registered || other.GetComponentInParent<FlightMotor2D>() == null)
            {
                return;
            }
            owner.ReleaseCollision(stableId);
            m_Registered = false;
        }

        private void OnDisable()
        {
            if (!m_Registered) return;
            owner?.ReleaseCollision(stableId);
            m_Registered = false;
        }
    }
}
