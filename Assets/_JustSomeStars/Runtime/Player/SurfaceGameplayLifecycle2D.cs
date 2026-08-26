using System;
using System.Linq;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Core;
using UnityEngine;

namespace JustSomeStars.Runtime.Player
{
    [DisallowMultipleComponent]
    public sealed class SurfaceGameplayLifecycle2D : MonoBehaviour
    {
        [SerializeField] private SurfaceMotor2D motor;
        [SerializeField] private CompositionCamera2D compositionCamera;
        [SerializeField] private Rigidbody2D targetBody;
        [SerializeField] private SurfaceInteractionProbe2D[] interactionProbes =
            Array.Empty<SurfaceInteractionProbe2D>();

        public SurfaceGameplayDependencies Dependencies { get; private set; }

        public bool IsConfigured => Dependencies != null;

        public void Configure(SurfaceGameplayDependencies dependencies)
        {
            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }

            if (Dependencies != null)
            {
                if (ReferenceEquals(Dependencies, dependencies))
                {
                    return;
                }

                throw new InvalidOperationException(
                    "Surface gameplay is already bound to another composition.");
            }

            if (motor == null || compositionCamera == null || targetBody == null)
            {
                throw new InvalidOperationException(
                    "SurfaceGameplayLifecycle2D requires its motor, camera and " +
                    "target body bindings.");
            }

            try
            {
                motor.BindInput(dependencies.Input, dependencies.Settings);
                foreach (var interaction in interactionProbes.Where(
                    candidate => candidate != null))
                {
                    interaction.BindInput(dependencies.Input);
                }
                compositionCamera.ApplySettings(dependencies.Settings.Current);
                compositionCamera.SetPolicy(
                    dependencies.Modes.CurrentPolicy.CameraPolicy);
                compositionCamera.SetTargetVelocity(targetBody.linearVelocity);
                dependencies.Settings.SettingsChanged += OnSettingsChanged;
                dependencies.Modes.StateChanged += OnModeStateChanged;
                Dependencies = dependencies;
            }
            catch
            {
                dependencies.Settings.SettingsChanged -= OnSettingsChanged;
                dependencies.Modes.StateChanged -= OnModeStateChanged;
                foreach (var interaction in interactionProbes.Where(
                    candidate => candidate != null))
                {
                    interaction.ReleaseInput(dependencies.Input);
                }
                motor.ReleaseInput(dependencies.Input);
                throw;
            }
        }

        public void Release(SurfaceGameplayDependencies dependencies)
        {
            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }

            if (Dependencies == null)
            {
                return;
            }

            if (!ReferenceEquals(Dependencies, dependencies))
            {
                throw new InvalidOperationException(
                    "Surface gameplay can only release its owning composition.");
            }

            Dependencies = null;
            dependencies.Settings.SettingsChanged -= OnSettingsChanged;
            dependencies.Modes.StateChanged -= OnModeStateChanged;
            foreach (var interaction in interactionProbes.Where(
                candidate => candidate != null))
            {
                interaction.ReleaseInput(dependencies.Input);
            }
            motor.ReleaseInput(dependencies.Input);
            compositionCamera.SetTargetVelocity(Vector2.zero);
        }

        private void Update()
        {
            if (Dependencies != null)
            {
                compositionCamera.SetTargetVelocity(targetBody.linearVelocity);
            }
        }

        private void OnSettingsChanged(GameSettings settings)
        {
            compositionCamera.ApplySettings(settings);
        }

        private void OnModeStateChanged(GameModeRuntimePolicy policy)
        {
            compositionCamera.SetPolicy(policy.CameraPolicy);
        }

        private void OnDestroy()
        {
            if (Dependencies != null)
            {
                Release(Dependencies);
            }
        }
    }
}
