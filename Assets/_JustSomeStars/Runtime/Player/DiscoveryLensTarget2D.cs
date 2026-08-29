using System;
using System.Linq;
using JustSomeStars.Runtime.Discovery;
using UnityEngine;

namespace JustSomeStars.Runtime.Player
{
    [DisallowMultipleComponent]
    public sealed class DiscoveryLensTarget2D : MonoBehaviour
    {
        [SerializeField] private PhenomenonDefinition phenomenon;
        [SerializeField] private SpriteRenderer[] glowRenderers =
            Array.Empty<SpriteRenderer>();
        [SerializeField] private ParticleSystem[] focusParticles =
            Array.Empty<ParticleSystem>();
        [SerializeField] private Color dormantColor =
            new Color(0.30f, 0.70f, 1f, 0.72f);
        [SerializeField] private Color focusedColor =
            new Color(0.74f, 0.35f, 1f, 1f);

        public string TargetId => phenomenon != null
            ? phenomenon.StableId.Value
            : string.Empty;
        public PhenomenonDefinition Phenomenon => phenomenon;
        public bool IsConfigured => phenomenon != null;
        public bool IsFocused { get; private set; }

        public void Configure(PhenomenonDefinition definition)
        {
            phenomenon = definition ?? throw new ArgumentNullException(
                nameof(definition));
            phenomenon.ValidateOrThrow();
            ApplyVisualState();
        }

        public void SetFocused(bool focused)
        {
            IsFocused = focused;
            ApplyVisualState();
        }

        public bool IsVisibleFrom(Camera camera)
        {
            if (camera == null || !isActiveAndEnabled)
            {
                return false;
            }

            var viewport = camera.WorldToViewportPoint(transform.position);
            return viewport.z > 0f &&
                viewport.x >= 0f && viewport.x <= 1f &&
                viewport.y >= 0f && viewport.y <= 1f;
        }

        public float GetFocusDistanceSquared(Vector2 aimWorld)
        {
            if (phenomenon == null ||
                phenomenon.FocusBehavior != LensFocusBehavior.Region)
            {
                return ((Vector2)transform.position - aimWorld).sqrMagnitude;
            }

            var renderers = GetComponentsInChildren<SpriteRenderer>(true)
                .Where(candidate => candidate != null && candidate.sprite != null)
                .ToArray();
            if (renderers.Length == 0)
            {
                return ((Vector2)transform.position - aimWorld).sqrMagnitude;
            }

            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1))
            {
                bounds.Encapsulate(renderer.bounds);
            }

            var closest = bounds.ClosestPoint(new Vector3(
                aimWorld.x,
                aimWorld.y,
                bounds.center.z));
            return ((Vector2)closest - aimWorld).sqrMagnitude;
        }

        public bool CanRetainTrackFocus(Vector2 aimWorld)
        {
            if (phenomenon == null ||
                phenomenon.FocusBehavior != LensFocusBehavior.Track)
            {
                return false;
            }

            var retentionRadius = phenomenon.FocusRadius * 1.5f;
            return ((Vector2)transform.position - aimWorld).sqrMagnitude <=
                retentionRadius * retentionRadius;
        }

        private void ApplyVisualState()
        {
            foreach (var renderer in glowRenderers)
            {
                if (renderer != null)
                {
                    renderer.color = IsFocused ? focusedColor : dormantColor;
                }
            }
            foreach (var particles in focusParticles)
            {
                if (particles == null)
                {
                    continue;
                }
                if (IsFocused)
                {
                    particles.Play(true);
                }
                else
                {
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }
    }
}
