using UnityEngine;

namespace JustSomeStars.Runtime.Flight
{
    [DisallowMultipleComponent]
    public sealed class Task17FlightRoute2D : MonoBehaviour
    {
        [SerializeField] private float nominalDurationSeconds = 90f;
        [SerializeField] private Vector2 routeStart = new Vector2(-250f, 0f);
        [SerializeField] private Vector2 routeFinish = new Vector2(250f, 0f);
        [SerializeField] private FlightCheckpoint recoveryCheckpoint;
        [SerializeField] private GravityAssistVolume2D gravityOpportunity;
        [SerializeField] private FlightHazard2D recoverableHazard;
        [SerializeField] private LandingSequence landingGate;

        public float NominalDurationSeconds => nominalDurationSeconds;
        public Vector2 RouteStart => routeStart;
        public Vector2 RouteFinish => routeFinish;
        public FlightCheckpoint RecoveryCheckpoint => recoveryCheckpoint;
        public GravityAssistVolume2D GravityOpportunity => gravityOpportunity;
        public FlightHazard2D RecoverableHazard => recoverableHazard;
        public LandingSequence LandingGate => landingGate;
    }
}
