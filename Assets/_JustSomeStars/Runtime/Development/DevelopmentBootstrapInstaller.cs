using System;
using JustSomeStars.Runtime.Core;
using UnityEngine;

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

            return new GameBootstrapComposition(
                new[]
                {
                    new GameServiceRegistration(
                        GameServiceRole.Settings,
                        new DevelopmentSettingsService(lifecycleObserver)),
                    new GameServiceRegistration(
                        GameServiceRole.LocalSave,
                        new DevelopmentLocalSaveService(lifecycleObserver)),
                    new GameServiceRegistration(
                        GameServiceRole.Input,
                        new DevelopmentInputService(lifecycleObserver)),
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
            return CreateComposition(
                new UnitySceneTransition(),
                NoOpDevelopmentServiceLifecycleObserver.Instance);
        }
    }
}
