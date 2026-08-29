using System;
using System.Linq;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Discovery;
using JustSomeStars.Runtime.Input;
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
        [SerializeField] private DiscoveryLensTarget2D[] lensTargets =
            Array.Empty<DiscoveryLensTarget2D>();
        [SerializeField] private InstrumentDefinition[] lensInstruments =
            Array.Empty<InstrumentDefinition>();
        [SerializeField] private Prediction[] lensPredictions =
            Array.Empty<Prediction>();
        [SerializeField] private DiscoveryLensPresenter2D lensPresenter;
        [SerializeField] private GameObject lensAimControl;
        [SerializeField] private GameObject[] surfaceOnlyControls =
            Array.Empty<GameObject>();

        private DiscoveryLensController lensController;

        public SurfaceGameplayDependencies Dependencies { get; private set; }

        public bool IsConfigured => Dependencies != null;

        public DiscoveryLensController LensController => lensController;

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
                lensController = new DiscoveryLensController(
                    dependencies.Input,
                    dependencies.Modes,
                    dependencies.Settings,
                    new EvidenceRecorder(dependencies.Events),
                    compositionCamera.ControlledCamera,
                    () => targetBody.position,
                    lensTargets.Where(candidate => candidate != null),
                    lensInstruments.Where(candidate => candidate != null),
                    lensPredictions.Where(candidate => candidate != null));
                lensController.Bind();
                lensPresenter?.Bind(lensController);
                ApplyLensTouchControls(dependencies.Modes.CurrentPolicy);
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
                lensController?.Dispose();
                lensController = null;
                lensPresenter?.Release();
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
            lensPresenter?.Release();
            lensController?.Dispose();
            lensController = null;
            ApplyLensTouchControls(new GameModeRuntimePolicy(
                GameMode.Surface,
                GameOverlay.None,
                GameplayInputMode.Surface,
                GameCameraPolicy.Surface));
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
                lensController?.Tick(Time.unscaledDeltaTime);
            }
        }

        private void OnSettingsChanged(GameSettings settings)
        {
            compositionCamera.ApplySettings(settings);
        }

        private void OnModeStateChanged(GameModeRuntimePolicy policy)
        {
            compositionCamera.SetPolicy(policy.CameraPolicy);
            ApplyLensTouchControls(policy);
        }

        private void ApplyLensTouchControls(GameModeRuntimePolicy policy)
        {
            var lensIsActive = policy.Mode == GameMode.Lens &&
                policy.Overlay == GameOverlay.None;
            if (lensAimControl != null)
            {
                lensAimControl.SetActive(lensIsActive);
            }
            foreach (var control in surfaceOnlyControls.Where(
                candidate => candidate != null))
            {
                control.SetActive(!lensIsActive);
            }
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
