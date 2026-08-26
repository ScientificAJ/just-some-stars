using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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

        private FrontendController m_Controller;
        private UnityFrontendLifecycle m_Lifecycle;
        private FrontendSettingsPanel m_SettingsPanel;
        private SurfaceGameplayLifecycle2D m_SurfaceLifecycle;

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

            ReleaseBindings();

            // LoadSceneAsync cannot be cancelled after Unity accepts the request.
            // From this call onward, report the actual completion rather than a
            // cancellation that cannot stop scene activation.
            var operation = SceneManager.LoadSceneAsync(
                destination,
                LoadSceneMode.Single);
            if (operation == null)
            {
                throw new InvalidOperationException(
                    $"Unity did not create a load operation for scene '{destination}'.");
            }

            while (!operation.isDone)
            {
                await Task.Yield();
            }

            BindActiveScene();
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

            var settingsPanels = UnityEngine.Object.FindObjectsByType<
                FrontendSettingsPanel>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            var views = UnityEngine.Object.FindObjectsByType<FrontendView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            var lifecycles = UnityEngine.Object.FindObjectsByType<
                UnityFrontendLifecycle>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            var controllers = UnityEngine.Object.FindObjectsByType<
                FrontendController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
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

        public void ReleaseBindings()
        {
            var controller = m_Controller;
            var lifecycle = m_Lifecycle;
            var settingsPanel = m_SettingsPanel;
            var surfaceLifecycle = m_SurfaceLifecycle;
            m_Controller = null;
            m_Lifecycle = null;
            m_SettingsPanel = null;
            m_SurfaceLifecycle = null;

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
