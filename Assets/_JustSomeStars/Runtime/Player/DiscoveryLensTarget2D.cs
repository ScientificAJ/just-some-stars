using System;
using JustSomeStars.Runtime.Input;
using UnityEngine;

namespace JustSomeStars.Runtime.Player
{
    [DisallowMultipleComponent]
    public sealed class DiscoveryLensTarget2D : MonoBehaviour
    {
        [SerializeField] private string targetId;
        [SerializeField] private SpriteRenderer[] glowRenderers =
            Array.Empty<SpriteRenderer>();
        [SerializeField] private ParticleSystem[] focusParticles =
            Array.Empty<ParticleSystem>();
        [SerializeField] private Color dormantColor =
            new Color(0.30f, 0.70f, 1f, 0.72f);
        [SerializeField] private Color focusedColor =
            new Color(0.74f, 0.35f, 1f, 1f);

        private InputRouter inputRouter;

        public string TargetId => targetId;
        public bool IsFocused { get; private set; }

        public void Configure(string stableTargetId)
        {
            targetId = !string.IsNullOrWhiteSpace(stableTargetId)
                ? stableTargetId
                : throw new ArgumentException(
                    "Lens target id is required.",
                    nameof(stableTargetId));
            ApplyVisualState();
        }

        public void BindInput(InputRouter router)
        {
            if (router == null)
            {
                throw new ArgumentNullException(nameof(router));
            }
            if (inputRouter != null)
            {
                if (ReferenceEquals(inputRouter, router))
                {
                    return;
                }
                throw new InvalidOperationException(
                    "DiscoveryLensTarget2D is already bound.");
            }
            inputRouter = router;
            inputRouter.GameplayCommandPerformed += OnGameplayCommand;
        }

        public void ReleaseInput(InputRouter router)
        {
            if (router == null)
            {
                throw new ArgumentNullException(nameof(router));
            }
            if (inputRouter == null)
            {
                return;
            }
            if (!ReferenceEquals(inputRouter, router))
            {
                throw new InvalidOperationException(
                    "DiscoveryLensTarget2D can only release its owner.");
            }
            inputRouter.GameplayCommandPerformed -= OnGameplayCommand;
            inputRouter = null;
        }

        public void SetFocused(bool focused)
        {
            IsFocused = focused;
            ApplyVisualState();
        }

        private void OnGameplayCommand(
            GameplayInputMode mode,
            SemanticGameplayCommand command)
        {
            if ((mode == GameplayInputMode.Surface ||
                 mode == GameplayInputMode.Lens) &&
                command == SemanticGameplayCommand.Lens)
            {
                SetFocused(!IsFocused);
            }
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

        private void OnDestroy()
        {
            if (inputRouter != null)
            {
                ReleaseInput(inputRouter);
            }
        }
    }
}
