using System;
using JustSomeStars.Runtime.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JustSomeStars.Runtime.Discovery
{
    [DisallowMultipleComponent]
    public sealed class DiscoveryLensPresenter2D : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform reticleRoot;
        [SerializeField] private Image reticleImage;
        [SerializeField] private Image progressImage;
        [SerializeField] private TMP_Text statusLabel;

        private DiscoveryLensController controller;

        public void Bind(DiscoveryLensController value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            if (canvas == null || reticleRoot == null || reticleImage == null ||
                progressImage == null || statusLabel == null)
            {
                throw new InvalidOperationException(
                    "Discovery Lens presenter requires complete UI bindings.");
            }

            controller = value;
            Render();
        }

        public void Release()
        {
            controller = null;
            if (reticleRoot != null)
            {
                reticleRoot.gameObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            using var performance = PerformanceMarkers.Lens.Auto();
            Render();
        }

        private void Render()
        {
            if (reticleRoot == null)
            {
                return;
            }

            var active = controller != null && controller.IsActive;
            reticleRoot.gameObject.SetActive(active);
            if (!active)
            {
                return;
            }

            var screen = RectTransformUtility.WorldToScreenPoint(
                controller.CompositionCamera,
                controller.AimWorld);
            if (reticleRoot.parent is RectTransform parent &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent,
                    screen,
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay
                        ? null
                        : canvas.worldCamera,
                    out var localPoint))
            {
                reticleRoot.anchoredPosition = localPoint;
            }

            progressImage.fillAmount = controller.ScanProgress;
            statusLabel.text = StatusFor(controller);
            var color = ColorFor(controller.ReticleState);
            reticleImage.color = color;
            progressImage.color = color;
        }

        private static string StatusFor(DiscoveryLensController value)
        {
            return value.ReticleState switch
            {
                LensReticleState.Searching => "SEARCH",
                LensReticleState.Focused => "FOCUS",
                LensReticleState.Incompatible => "INCOMPATIBLE",
                LensReticleState.Scanning =>
                    $"SCANNING {Mathf.RoundToInt(value.ScanProgress * 100f)}%",
                LensReticleState.Complete => "RECORDED",
                _ => string.Empty,
            };
        }

        private static Color ColorFor(LensReticleState state)
        {
            return state switch
            {
                LensReticleState.Complete => new Color(0.45f, 1f, 0.72f, 1f),
                LensReticleState.Incompatible => new Color(1f, 0.52f, 0.35f, 1f),
                LensReticleState.Scanning => new Color(0.72f, 0.40f, 1f, 1f),
                _ => new Color(0.42f, 0.86f, 1f, 1f),
            };
        }
    }
}
