using UnityEngine;

namespace JustSomeStars.Runtime.Accessibility
{
    [DisallowMultipleComponent]
    public sealed class AccessibleSignalPulse : MonoBehaviour
    {
        [SerializeField] private CanvasGroup target;
        [SerializeField] private float minimumAlpha = 0.28f;
        [SerializeField] private float maximumAlpha = 0.72f;
        [SerializeField] private float cyclesPerSecond = 0.55f;

        private void OnEnable()
        {
            if (target == null)
            {
                target = GetComponent<CanvasGroup>();
            }
        }

        private void Update()
        {
            if (target == null)
            {
                return;
            }
            var phase = Mathf.Sin(
                Time.unscaledTime * Mathf.PI * 2f * cyclesPerSecond);
            target.alpha = Mathf.Lerp(
                minimumAlpha,
                maximumAlpha,
                (phase + 1f) * 0.5f);
        }

        private void OnDisable()
        {
            if (target != null)
            {
                target.alpha = maximumAlpha;
            }
        }
    }
}
