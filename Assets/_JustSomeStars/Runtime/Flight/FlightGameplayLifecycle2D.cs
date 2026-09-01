using System;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Player;
using UnityEngine;

namespace JustSomeStars.Runtime.Flight
{
    public interface IFlightGameplayExtension
    {
        void Configure(FlightGameplayDependencies dependencies);

        void Release(FlightGameplayDependencies dependencies);
    }

    [DisallowMultipleComponent]
    public sealed class FlightGameplayLifecycle2D : MonoBehaviour
    {
        [SerializeField] private FlightMotor2D motor;
        [SerializeField] private LandingSequence landing;
        [SerializeField] private CompositionCamera2D compositionCamera;
        [SerializeField] private Rigidbody2D targetBody;
        [SerializeField] private FlightPredictionArc2D predictionArc;
        [SerializeField] private FlightTouchHudLayout2D touchHud;
        [SerializeField] private float nominalRouteSeconds = 90f;

        private IFlightGameplayExtension[] m_Extensions =
            Array.Empty<IFlightGameplayExtension>();

        public FlightGameplayDependencies Dependencies { get; private set; }
        public bool IsConfigured => Dependencies != null;
        public float NominalRouteSeconds => nominalRouteSeconds;
        public FlightMotor2D Motor => motor;
        public LandingSequence Landing => landing;
        public CompositionCamera2D CompositionCamera => compositionCamera;
        public FlightPredictionArc2D PredictionArc => predictionArc;

        public void Configure(FlightGameplayDependencies dependencies)
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
                    "Flight gameplay is already bound to another composition.");
            }

            if (motor == null || landing == null || compositionCamera == null ||
                targetBody == null || predictionArc == null || touchHud == null)
            {
                throw new InvalidOperationException(
                    "FlightGameplayLifecycle2D requires all production bindings.");
            }

            try
            {
                motor.BindInput(dependencies.Input, dependencies.Settings);
                landing.Configure(
                    dependencies.Scenes,
                    dependencies.Events,
                    dependencies.Modes,
                    dependencies.Progression);
                compositionCamera.ApplySettings(dependencies.Settings.Current);
                touchHud.Apply(dependencies.Settings.Current);
                compositionCamera.SetPolicy(GameCameraPolicy.Flight);
                compositionCamera.SetTargetVelocity(targetBody.linearVelocity);
                dependencies.Settings.SettingsChanged += OnSettingsChanged;
                dependencies.Modes.StateChanged += OnModeStateChanged;
                m_Extensions = GetComponentsInChildren<IFlightGameplayExtension>(true);
                foreach (var extension in m_Extensions)
                {
                    extension.Configure(dependencies);
                }
                ApplyPolicy(dependencies.Modes.CurrentPolicy);
                Dependencies = dependencies;
                if (dependencies.Progression?.HasPendingDeparture == true)
                {
                    _ = ConfirmPendingDepartureAsync(dependencies.Progression);
                }
            }
            catch
            {
                for (var index = m_Extensions.Length - 1; index >= 0; index--)
                {
                    try
                    {
                        m_Extensions[index].Release(dependencies);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception, this);
                    }
                }
                m_Extensions = Array.Empty<IFlightGameplayExtension>();
                dependencies.Settings.SettingsChanged -= OnSettingsChanged;
                dependencies.Modes.StateChanged -= OnModeStateChanged;
                landing.Release();
                motor.ReleaseInput(dependencies.Input);
                throw;
            }
        }

        private async System.Threading.Tasks.Task ConfirmPendingDepartureAsync(
            JustSomeStars.Runtime.Missions.IChapterProgression progression)
        {
            try
            {
                await progression.ConfirmDepartureAsync(destroyCancellationToken);
            }
            catch (OperationCanceledException) when (destroyCancellationToken.IsCancellationRequested)
            {
                // Scene teardown owns this cancellation.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        public void Release(FlightGameplayDependencies dependencies)
        {
            if (Dependencies == null)
            {
                return;
            }

            if (!ReferenceEquals(Dependencies, dependencies))
            {
                throw new InvalidOperationException(
                    "Flight gameplay can only release its owning composition.");
            }

            Dependencies = null;
            for (var index = m_Extensions.Length - 1; index >= 0; index--)
            {
                m_Extensions[index].Release(dependencies);
            }
            m_Extensions = Array.Empty<IFlightGameplayExtension>();
            dependencies.Settings.SettingsChanged -= OnSettingsChanged;
            dependencies.Modes.StateChanged -= OnModeStateChanged;
            landing.Release();
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
            touchHud.Apply(settings);
        }

        private void OnModeStateChanged(GameModeRuntimePolicy policy)
        {
            ApplyPolicy(policy);
        }

        private void ApplyPolicy(GameModeRuntimePolicy policy)
        {
            compositionCamera.SetPolicy(policy.CameraPolicy);
            motor.SetInputSuppressed(
                policy.Mode != GameMode.Flight || policy.Overlay != GameOverlay.None);
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
