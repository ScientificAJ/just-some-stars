using System;
using System.Linq;
using UnityEngine;

namespace JustSomeStars.Runtime.Rendering2D
{
    [DisallowMultipleComponent]
    public sealed class ParallaxRig2D : MonoBehaviour
    {
        [SerializeField] private Transform cameraAnchor;
        [SerializeField] private ParallaxLayer2D[] layers =
            Array.Empty<ParallaxLayer2D>();
        [SerializeField, Range(0f, 1f)] private float motionScale = 1f;

        private Vector3 cameraOrigin;
        private bool originsCaptured;

        public float MotionScale => motionScale;

        public void Configure(
            Transform anchor,
            ParallaxLayer2D[] parallaxLayers)
        {
            cameraAnchor = anchor;
            layers = parallaxLayers == null
                ? Array.Empty<ParallaxLayer2D>()
                : parallaxLayers.Where(layer => layer != null).ToArray();
            originsCaptured = false;
        }

        public void CaptureOrigins()
        {
            if (cameraAnchor == null)
            {
                throw new InvalidOperationException(
                    "ParallaxRig2D requires a camera anchor.");
            }

            cameraOrigin = cameraAnchor.position;
            foreach (var layer in layers)
            {
                layer.Origin = layer.transform.position;
            }

            originsCaptured = true;
        }

        public void SetMotionScale(float scale)
        {
            motionScale = Mathf.Clamp01(scale);
        }

        public void ApplyNow()
        {
            if (!originsCaptured)
            {
                CaptureOrigins();
            }

            var displacement = cameraAnchor.position - cameraOrigin;
            foreach (var layer in layers)
            {
                var offset = new Vector3(
                    displacement.x * layer.AxisScale.x,
                    displacement.y * layer.AxisScale.y,
                    0f) * (layer.Factor * motionScale);
                layer.transform.position = layer.Origin + offset;
            }
        }

        private void LateUpdate()
        {
            if (cameraAnchor != null)
            {
                ApplyNow();
            }
        }
    }
}
