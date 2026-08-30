using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Flight;
using JustSomeStars.Runtime.Player;
using JustSomeStars.Runtime.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JustSomeStars.Runtime.Core
{
    public interface ISceneTransition
    {
        ValueTask RouteAsync(
            string destination,
            CancellationToken cancellationToken);
    }

    internal interface ISceneBindingLifecycle
    {
        void BindActiveScene();

        void ReleaseBindings();
    }

    public sealed class UnitySceneTransition :
        ISceneTransition,
        ISceneBindingLifecycle
    {
        private readonly FrontendDependencies m_FrontendDependencies;
        private readonly SurfaceGameplayDependencies m_SurfaceDependencies;
        private FlightGameplayDependencies m_FlightDependencies;

        private FrontendController m_Controller;
        private UnityFrontendLifecycle m_Lifecycle;
        private FrontendSettingsPanel m_SettingsPanel;
        private SurfaceGameplayLifecycle2D m_SurfaceLifecycle;
        private FlightGameplayLifecycle2D m_FlightLifecycle;

        public UnitySceneTransition()
        {
        }

        public UnitySceneTransition(FrontendDependencies frontendDependencies)
        {
            m_FrontendDependencies = frontendDependencies ??
                throw new ArgumentNullException(nameof(frontendDependencies));
        }

        public UnitySceneTransition(
            FrontendDependencies frontendDependencies,
            SurfaceGameplayDependencies surfaceDependencies)
        {
            m_FrontendDependencies = frontendDependencies ??
                throw new ArgumentNullException(nameof(frontendDependencies));
            m_SurfaceDependencies = surfaceDependencies ??
                throw new ArgumentNullException(nameof(surfaceDependencies));
        }

        public async ValueTask RouteAsync(
            string destination,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(destination))
            {
                throw new ArgumentException(
                    "A scene destination is required.",
                    nameof(destination));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var sourceScene = SceneManager.GetActiveScene();
            if (string.Equals(
                    sourceScene.name,
                    destination,
                    StringComparison.Ordinal))
            {
                if (!HasBindingsForScene(sourceScene))
                {
                    ReleaseBindings();
                    BindActiveScene();
                }

                return;
            }

            if (IsDisposableBootstrapScene(sourceScene))
            {
                await ReplaceBootstrapSceneAsync(destination);
                return;
            }

            var existingSceneHandles = new HashSet<int>();
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                existingSceneHandles.Add(SceneManager.GetSceneAt(index).handle);
            }

            var destinationScene = default(Scene);
            var sourceBindingsReleased = false;
            try
            {
                // Keep the bound source scene alive until the destination has
                // loaded. This makes a load or injection failure recoverable.
                var operation = SceneManager.LoadSceneAsync(
                    destination,
                    LoadSceneMode.Additive);
                if (operation == null)
                {
                    throw new InvalidOperationException(
                        $"Unity did not create a load operation for scene " +
                        $"'{destination}'.");
                }

                // Unity cannot cancel after accepting the request. From here,
                // report actual completion rather than a cancellation that
                // cannot stop activation.
                await AwaitOperation(operation);
                destinationScene = FindNewScene(
                    destination,
                    existingSceneHandles);
                if (!destinationScene.IsValid() || !destinationScene.isLoaded)
                {
                    throw new InvalidOperationException(
                        $"Unity loaded no new scene for '{destination}'.");
                }

                if (!SceneManager.SetActiveScene(destinationScene))
                {
                    throw new InvalidOperationException(
                        $"Unity could not activate scene '{destination}'.");
                }

                sourceBindingsReleased = true;
                ReleaseBindings();
                BindActiveScene();

                var unload = SceneManager.UnloadSceneAsync(sourceScene);
                if (unload == null)
                {
                    throw new InvalidOperationException(
                        $"Unity could not unload source scene " +
                        $"'{sourceScene.name}'.");
                }

                await AwaitOperation(unload);
            }
            catch (Exception transitionFailure)
            {
                var recoveryFailures = new List<Exception>();
                if (sourceScene.IsValid() && sourceScene.isLoaded)
                {
                    if (sourceBindingsReleased)
                    {
                        TryRelease(ReleaseBindings, recoveryFailures);
                    }

                    if (!SceneManager.SetActiveScene(sourceScene))
                    {
                        recoveryFailures.Add(new InvalidOperationException(
                            $"Unity could not reactivate source scene " +
                            $"'{sourceScene.name}'."));
                    }
                    else if (sourceBindingsReleased)
                    {
                        TryRelease(BindActiveScene, recoveryFailures);
                    }
                }
                else
                {
                    recoveryFailures.Add(new InvalidOperationException(
                        "The source scene was unavailable during route rollback."));
                }

                if (destinationScene.IsValid() && destinationScene.isLoaded)
                {
                    try
                    {
                        var unload = SceneManager.UnloadSceneAsync(destinationScene);
                        if (unload == null)
                        {
                            throw new InvalidOperationException(
                                "Unity could not unload the failed destination.");
                        }

                        await AwaitOperation(unload);
                    }
                    catch (Exception cleanupFailure)
                    {
                        recoveryFailures.Add(cleanupFailure);
                    }
                }

                if (recoveryFailures.Count > 0)
                {
                    recoveryFailures.Insert(0, transitionFailure);
                    throw new AggregateException(
                        "Scene transition and rollback both failed.",
                        recoveryFailures);
                }

                throw;
            }
        }

        private async ValueTask ReplaceBootstrapSceneAsync(string destination)
        {
            // Boot owns no player state. Replacing it avoids Unity's costly
            // additive lighting finalization for the Frontend while gameplay
            // routes retain the recoverable additive transaction below.
            ReleaseBindings();
            var operation = SceneManager.LoadSceneAsync(
                destination,
                LoadSceneMode.Single);
            if (operation == null)
            {
                throw new InvalidOperationException(
                    $"Unity did not create a bootstrap route operation for " +
                    $"scene '{destination}'.");
            }

            while (!operation.isDone)
            {
                await Task.Yield();
            }

            BindActiveScene();
        }

        private static bool IsDisposableBootstrapScene(Scene scene)
        {
            return string.Equals(scene.name, "Boot", StringComparison.Ordinal);
        }

        private bool HasBindingsForScene(Scene scene)
        {
            return IsBoundToScene(m_Controller, scene) ||
                IsBoundToScene(m_Lifecycle, scene) ||
                IsBoundToScene(m_SettingsPanel, scene) ||
                IsBoundToScene(m_SurfaceLifecycle, scene) ||
                IsBoundToScene(m_FlightLifecycle, scene);
        }

        private static bool IsBoundToScene(Component component, Scene scene)
        {
            return component != null && component.gameObject.scene == scene;
        }

        private static async ValueTask AwaitOperation(AsyncOperation operation)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (operation.isDone)
            {
                return;
            }

            var completion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            void Complete(AsyncOperation _)
            {
                completion.TrySetResult(true);
            }

            operation.completed += Complete;
            try
            {
                // The operation can complete between the initial state check and
                // event subscription. Recheck after subscribing so that window
                // cannot strand the route task.
                if (operation.isDone)
                {
                    completion.TrySetResult(true);
                }

                await completion.Task;
            }
            finally
            {
                operation.completed -= Complete;
            }
        }

        private static Scene FindNewScene(
            string name,
            ISet<int> existingSceneHandles)
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var candidate = SceneManager.GetSceneAt(index);
                if (candidate.isLoaded &&
                    string.Equals(candidate.name, name, StringComparison.Ordinal) &&
                    !existingSceneHandles.Contains(candidate.handle))
                {
                    return candidate;
                }
            }

            return default;
        }

        public void BindActiveScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (string.Equals(
                    activeScene.name,
                    "Frontend",
                    StringComparison.Ordinal))
            {
                ConfigureFrontend();
                return;
            }

            var flightCandidates = UnityEngine.Object.FindObjectsByType<
                FlightGameplayLifecycle2D>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            FlightGameplayLifecycle2D flightMatch = null;
            foreach (var candidate in flightCandidates)
            {
                if (candidate.gameObject.scene != activeScene)
                {
                    continue;
                }

                if (flightMatch != null)
                {
                    throw new InvalidOperationException(
                        "A routed flight scene must contain exactly one " +
                        "FlightGameplayLifecycle2D.");
                }

                flightMatch = candidate;
            }

            if (flightMatch != null)
            {
                if (m_FlightDependencies == null)
                {
                    throw new InvalidOperationException(
                        "Flight routing requires composition-owned dependencies.");
                }

                m_FlightLifecycle = flightMatch;
                try
                {
                    m_FlightLifecycle.Configure(m_FlightDependencies);
                }
                catch
                {
                    m_FlightLifecycle = null;
                    throw;
                }

                return;
            }

            var candidates = UnityEngine.Object.FindObjectsByType<
                SurfaceGameplayLifecycle2D>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            SurfaceGameplayLifecycle2D match = null;
            foreach (var candidate in candidates)
            {
                if (candidate.gameObject.scene != activeScene)
                {
                    continue;
                }

                if (match != null)
                {
                    throw new InvalidOperationException(
                        "A routed gameplay scene must contain at most one " +
                        "SurfaceGameplayLifecycle2D.");
                }

                match = candidate;
            }

            if (match == null)
            {
                return;
            }

            if (m_SurfaceDependencies == null)
            {
                throw new InvalidOperationException(
                    "Surface routing requires composition-owned dependencies.");
            }

            m_SurfaceLifecycle = match;
            try
            {
                m_SurfaceLifecycle.Configure(m_SurfaceDependencies);
            }
            catch
            {
                m_SurfaceLifecycle = null;
                throw;
            }
        }

        private void ConfigureFrontend()
        {
            if (m_FrontendDependencies == null)
            {
                throw new InvalidOperationException(
                    "Frontend routing requires composition-owned dependencies.");
            }

            var settingsPanels = FindInActiveScene<FrontendSettingsPanel>();
            var views = FindInActiveScene<FrontendView>();
            var lifecycles = FindInActiveScene<UnityFrontendLifecycle>();
            var controllers = FindInActiveScene<FrontendController>();
            if (settingsPanels.Length != 1 || views.Length != 1 ||
                lifecycles.Length != 1 || controllers.Length != 1)
            {
                throw new InvalidOperationException(
                    "Frontend scene must contain exactly one settings panel, " +
                    "view, lifecycle and controller for dependency injection.");
            }

            m_SettingsPanel = settingsPanels[0];
            m_Lifecycle = lifecycles[0];
            m_Controller = controllers[0];
            try
            {
                m_SettingsPanel.Configure(m_FrontendDependencies);
                m_Lifecycle.Configure(m_FrontendDependencies);
                m_Controller.Configure(m_FrontendDependencies);
            }
            catch
            {
                ReleaseBindings();
                throw;
            }
        }

        private static T[] FindInActiveScene<T>() where T : Component
        {
            var activeScene = SceneManager.GetActiveScene();
            var matches = new List<T>();
            foreach (var candidate in UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
            {
                if (candidate.gameObject.scene == activeScene)
                {
                    matches.Add(candidate);
                }
            }

            return matches.ToArray();
        }

        public void ReleaseBindings()
        {
            var controller = m_Controller;
            var lifecycle = m_Lifecycle;
            var settingsPanel = m_SettingsPanel;
            var surfaceLifecycle = m_SurfaceLifecycle;
            var flightLifecycle = m_FlightLifecycle;
            m_Controller = null;
            m_Lifecycle = null;
            m_SettingsPanel = null;
            m_SurfaceLifecycle = null;
            m_FlightLifecycle = null;

            var failures = new List<Exception>();
            TryRelease(
                () =>
                {
                    if (controller != null)
                    {
                        controller.Release(m_FrontendDependencies);
                    }
                },
                failures);
            TryRelease(
                () =>
                {
                    if (flightLifecycle != null &&
                        m_FlightDependencies != null)
                    {
                        flightLifecycle.Release(m_FlightDependencies);
                    }
                },
                failures);
            TryRelease(
                () =>
                {
                    if (surfaceLifecycle != null &&
                        m_SurfaceDependencies != null)
                    {
                        surfaceLifecycle.Release(m_SurfaceDependencies);
                    }
                },
                failures);
            TryRelease(
                () =>
                {
                    if (lifecycle != null)
                    {
                        lifecycle.Release(m_FrontendDependencies);
                    }
                },
                failures);
            TryRelease(
                () =>
                {
                    if (settingsPanel != null)
                    {
                        settingsPanel.Release(m_FrontendDependencies);
                    }
                },
                failures);

            if (failures.Count > 0)
            {
                throw new AggregateException(
                    "Frontend bindings could not be released cleanly.",
                    failures);
            }
        }

        public void ConfigureFlightDependencies(
            FlightGameplayDependencies dependencies)
        {
            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }

            if (m_FlightDependencies != null &&
                !ReferenceEquals(m_FlightDependencies, dependencies))
            {
                throw new InvalidOperationException(
                    "Flight dependencies can only be assigned once by composition.");
            }

            m_FlightDependencies = dependencies;
        }

        private static void TryRelease(
            Action release,
            ICollection<Exception> failures)
        {
            try
            {
                release();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }
    }
}
