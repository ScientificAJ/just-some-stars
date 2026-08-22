using System;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Input;
using JustSomeStars.Runtime.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JustSomeStars.Runtime.Development
{
    internal static class DevelopmentBootstrapInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        internal static void Install()
        {
            if (GameBootstrap.CompositionFactory != null)
            {
                return;
            }

            GameBootstrap.CompositionFactory = CreateComposition;
        }

        internal static GameBootstrapComposition CreateComposition(
            ISceneTransition sceneTransition,
            IDevelopmentServiceLifecycleObserver lifecycleObserver)
        {
            if (sceneTransition == null)
            {
                throw new ArgumentNullException(nameof(sceneTransition));
            }

            if (lifecycleObserver == null)
            {
                throw new ArgumentNullException(nameof(lifecycleObserver));
            }

            var settings = new SettingsService();
            var actions = InputSystem.actions;
            if (actions == null)
            {
                throw new InvalidOperationException(
                    "Unity project-wide JssInputActions is not configured.");
            }

            var input = new InputRouter(actions, settings);
            return new GameBootstrapComposition(
                new[]
                {
                    new GameServiceRegistration(
                        GameServiceRole.Settings,
                        settings),
                    new GameServiceRegistration(
                        GameServiceRole.LocalSave,
                        new DevelopmentLocalSaveService(lifecycleObserver)),
                    new GameServiceRegistration(
                        GameServiceRole.Input,
                        input),
                    new GameServiceRegistration(
                        GameServiceRole.ContentCatalogue,
                        new DevelopmentContentCatalogueService(lifecycleObserver)),
                    new GameServiceRegistration(
                        GameServiceRole.ModeController,
                        new DevelopmentModeControllerService(lifecycleObserver)),
                },
                sceneTransition);
        }

        private static GameBootstrapComposition CreateComposition()
        {
            var settings = new SettingsService();
            var actions = InputSystem.actions;
            if (actions == null)
            {
                throw new InvalidOperationException(
                    "Unity project-wide JssInputActions is not configured.");
            }

            var input = new InputRouter(actions, settings);
            var dependencies = new FrontendDependencies(settings, input);
            return new GameBootstrapComposition(
                new[]
                {
                    new GameServiceRegistration(
                        GameServiceRole.Settings,
                        settings),
                    new GameServiceRegistration(
                        GameServiceRole.LocalSave,
                        new DevelopmentLocalSaveService(
                            NoOpDevelopmentServiceLifecycleObserver.Instance)),
                    new GameServiceRegistration(
                        GameServiceRole.Input,
                        input),
                    new GameServiceRegistration(
                        GameServiceRole.ContentCatalogue,
                        new DevelopmentContentCatalogueService(
                            NoOpDevelopmentServiceLifecycleObserver.Instance)),
                    new GameServiceRegistration(
                        GameServiceRole.ModeController,
                        new DevelopmentModeControllerService(
                            NoOpDevelopmentServiceLifecycleObserver.Instance)),
                },
                new UnitySceneTransition(dependencies));
        }
    }
}
