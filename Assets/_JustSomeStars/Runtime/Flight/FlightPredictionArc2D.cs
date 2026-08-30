using System.Collections.Generic;
using UnityEngine;

namespace JustSomeStars.Runtime.Flight
{
    [DisallowMultipleComponent]
    public sealed class FlightPredictionArc2D : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer[] points = System.Array.Empty<SpriteRenderer>();

        public int PointCount => points?.Length ?? 0;

        public void Present(IReadOnlyList<FlightState> prediction, bool reducedMotion)
        {
            if (prediction == null)
            {
                throw new System.ArgumentNullException(nameof(prediction));
            }

            for (var index = 0; index < points.Length; index++)
            {
                var point = points[index];
                if (point == null)
                {
                    continue;
                }

                var sampleIndex = points.Length <= 1
                    ? 0
                    : Mathf.RoundToInt(
                        index * (prediction.Count - 1f) / (points.Length - 1f));
                var visible = prediction.Count > 0 && sampleIndex < prediction.Count;
                point.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                point.transform.position = prediction[sampleIndex].Position;
                var alpha = reducedMotion ? 0.42f : Mathf.Lerp(0.8f, 0.2f,
                    points.Length <= 1 ? 0f : index / (points.Length - 1f));
                var color = point.color;
                color.a = alpha;
                point.color = color;
            }
        }
    }
}
