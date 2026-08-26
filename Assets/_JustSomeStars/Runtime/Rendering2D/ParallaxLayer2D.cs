using UnityEngine;

namespace JustSomeStars.Runtime.Rendering2D
{
    [DisallowMultipleComponent]
    public sealed class ParallaxLayer2D : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float factor;
        [SerializeField] private Vector2 axisScale = Vector2.one;

        internal Vector3 Origin { get; set; }

        public float Factor => factor;
        public Vector2 AxisScale => axisScale;

        public void Configure(float parallaxFactor, Vector2 independentAxisScale)
        {
            factor = Mathf.Clamp01(parallaxFactor);
            axisScale = independentAxisScale;
        }
    }
}
