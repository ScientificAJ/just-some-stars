using UnityEngine;

namespace JustSomeStars.Runtime.Flight
{
    [DisallowMultipleComponent]
    public sealed class GravityAssistVolume2D : MonoBehaviour
    {
        [SerializeField] private float radius = 3f;
        [SerializeField] private float radialAcceleration = 2.4f;
        [SerializeField] private float tangentialAcceleration = 1.8f;
        [SerializeField] private int lane = 1;

        public GravityAssistSample Sample => new GravityAssistSample(
            transform.position,
            radius,
            radialAcceleration,
            tangentialAcceleration,
            lane);
    }
}
