using System;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Input;
using JustSomeStars.Runtime.Player;
using JustSomeStars.Runtime.Saving;
using JustSomeStars.Runtime.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JustSomeStars.Runtime.Core
{
    internal static class ApplicationBootstrapInstaller
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

        internal static GameBootstrapComposition CreateCompositionForTests(
            ISceneTransition sceneTransition,
            ISceneCatalogSource catalogSource,
            ISceneStreamBackend sceneBackend,
            string settingsPath,
            string savePath)
        {
            if (sceneTransition == null)
            {
                throw new ArgumentNullException(nameof(sceneTransition));
            }

            if (catalogSource == null)
            {
                throw new ArgumentNullException(nameof(catalogSource));
            }

            if (sceneBackend == null)
            {
                throw new ArgumentNullException(nameof(sceneBackend));
            }

            return CreateComposition(
                new SettingsService(settingsPath),
                new LocalSaveService(savePath),
                sceneTransition,
                catalogSource,
                sceneBackend);
        }

        private static GameBootstrapComposition CreateComposition()
        {
            var settings = new SettingsService();
            var localSave = new LocalSaveService();
            var actions = RequireProjectActions();
            var input = new InputRouter(actions, settings);
            var modeController = new GameModeController(
                InitialExperiencePolicy.CurrentMode,
                new InputRouterGameModeRuntimeHooks(input));
            var gameEvents = new GameEventBus();
            var sceneTransition = new UnitySceneTransition(
                new FrontendDependencies(settings, input),
                new SurfaceGameplayDependencies(
                    settings,
                    input,
                    modeController,
                    gameEvents));
            return CreateCompositionWithModeController(
                settings,
                localSave,
                input,
                modeController,
                sceneTransition,
                new AddressablesSceneCatalogSource(SceneCatalog.AddressablesKey),
                new AddressablesSceneStreamBackend());
        }

        private static GameBootstrapComposition CreateComposition(
            SettingsService settings,
            LocalSaveService localSave,
            ISceneTransition sceneTransition,
            ISceneCatalogSource catalogSource,
            ISceneStreamBackend sceneBackend)
        {
            var input = new InputRouter(RequireProjectActions(), settings);
            return CreateComposition(
                settings,
                localSave,
                input,
                sceneTransition,
                catalogSource,
                sceneBackend);
        }

        private static GameBootstrapComposition CreateComposition(
            SettingsService settings,
            LocalSaveService localSave,
            InputRouter input,
            ISceneTransition sceneTransition,
            ISceneCatalogSource catalogSource,
            ISceneStreamBackend sceneBackend)
        {
            var modeController = new GameModeController(
                GameMode.Frontend,
                new InputRouterGameModeRuntimeHooks(input));
            return CreateCompositionWithModeController(
                settings,
                localSave,
                input,
                modeController,
                sceneTransition,
                catalogSource,
                sceneBackend);
        }

        private static GameBootstrapComposition CreateCompositionWithModeController(
            SettingsService settings,
            LocalSaveService localSave,
            InputRouter input,
            GameModeController modeController,
            ISceneTransition sceneTransition,
            ISceneCatalogSource catalogSource,
            ISceneStreamBackend sceneBackend)
        {
            var sceneStream = new SceneStreamService(
                catalogSource,
                sceneBackend,
                sceneTransition,
                modeController);
            return new GameBootstrapComposition(
                new[]
                {
                    new GameServiceRegistration(GameServiceRole.Settings, settings),
                    new GameServiceRegistration(GameServiceRole.LocalSave, localSave),
                    new GameServiceRegistration(GameServiceRole.Input, input),
                    new GameServiceRegistration(
                        GameServiceRole.ContentCatalogue,
                        sceneStream),
                    new GameServiceRegistration(
                        GameServiceRole.ModeController,
                        modeController),
                },
                sceneTransition);
        }

        private static InputActionAsset RequireProjectActions()
        {
            var actions = InputSystem.actions;
            if (actions == null)
            {
                throw new InvalidOperationException(
                    "Unity project-wide JssInputActions is not configured.");
            }

            return actions;
        }
    }
}
