using System;
using JustSomeStars.Runtime.Accounts;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Commerce;
using JustSomeStars.Runtime.Input;
using JustSomeStars.Runtime.Flight;
using JustSomeStars.Runtime.Player;
using JustSomeStars.Runtime.Saving;
using JustSomeStars.Runtime.Missions;
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
            var account = CreateAccountService(localSave);
            var commerce = StoreProviderRegistry.Create(account);
            var saves = new CloudCheckpointSaveService(localSave, account);
            var actions = RequireProjectActions();
            var input = new InputRouter(actions, settings);
            var modeController = new GameModeController(
                InitialExperiencePolicy.CurrentMode,
                new InputRouterGameModeRuntimeHooks(input));
            var gameEvents = new GameEventBus();
            var progression = new DestinationProgressionCoordinator(
                gameEvents,
                saves,
                settings);
            var surfaceDependencies = new SurfaceGameplayDependencies(
                settings,
                input,
                modeController,
                gameEvents,
                saves,
                progression);
            var sceneTransition = new UnitySceneTransition(
                new FrontendDependencies(settings, input, account),
                surfaceDependencies);
            surfaceDependencies.ConfigureSceneTransition(sceneTransition);
            sceneTransition.ConfigureFlightDependencies(
                new FlightGameplayDependencies(
                    settings,
                    input,
                    modeController,
                    gameEvents,
                    sceneTransition,
                    progression));
            return CreateCompositionWithModeController(
                settings,
                saves,
                input,
                modeController,
                account,
                commerce,
                sceneTransition,
                new AddressablesSceneCatalogSource(SceneCatalog.AddressablesKey),
                new AddressablesSceneStreamBackend(),
                progression);
        }

        private static GameBootstrapComposition CreateComposition(
            SettingsService settings,
            LocalSaveService localSave,
            ISceneTransition sceneTransition,
            ISceneCatalogSource catalogSource,
            ISceneStreamBackend sceneBackend)
        {
            var input = new InputRouter(RequireProjectActions(), settings);
            var account = CreateAccountService(localSave);
            return CreateComposition(
                settings,
                new CloudCheckpointSaveService(localSave, account),
                account,
                new UnavailableStoreService(),
                input,
                sceneTransition,
                catalogSource,
                sceneBackend);
        }

        private static GameBootstrapComposition CreateComposition(
            SettingsService settings,
            ISaveService saves,
            IAccountService account,
            IStoreService commerce,
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
                saves,
                input,
                modeController,
                account,
                commerce,
                sceneTransition,
                catalogSource,
                sceneBackend);
        }

        private static GameBootstrapComposition CreateCompositionWithModeController(
            SettingsService settings,
            ISaveService saves,
            InputRouter input,
            GameModeController modeController,
            IAccountService account,
            IStoreService commerce,
            ISceneTransition sceneTransition,
            ISceneCatalogSource catalogSource,
            ISceneStreamBackend sceneBackend,
            IChapterProgression progression = null)
        {
            var sceneStream = new SceneStreamService(
                catalogSource,
                sceneBackend,
                sceneTransition,
                modeController);
            var registrations = new System.Collections.Generic.List<
                GameServiceRegistration>
                {
                    new GameServiceRegistration(GameServiceRole.Settings, settings),
                    new GameServiceRegistration(GameServiceRole.LocalSave, saves),
                    new GameServiceRegistration(GameServiceRole.Input, input),
                    new GameServiceRegistration(
                        GameServiceRole.ContentCatalogue,
                        sceneStream),
                    new GameServiceRegistration(
                        GameServiceRole.ModeController,
                        modeController),
                    new GameServiceRegistration(
                        GameServiceRole.Cloud,
                        account),
                    new GameServiceRegistration(
                        GameServiceRole.Commerce,
                        commerce),
                };
            if (progression != null)
            {
                registrations.Add(new GameServiceRegistration(
                    GameServiceRole.Progression,
                    progression));
            }

            if (progression == null)
            {
                return new GameBootstrapComposition(registrations, sceneTransition);
            }

            return new GameBootstrapComposition(
                registrations,
                sceneTransition,
                () => InitialExperiencePolicy.CurrentMode == GameMode.Flight
                    ? progression.ResumeSceneName
                    : InitialExperiencePolicy.CurrentDestination,
                () => InitialExperiencePolicy.CurrentMode == GameMode.Flight
                    ? progression.ResumeMode
                    : InitialExperiencePolicy.CurrentMode);
        }

        private static FirebaseAccountService CreateAccountService(
            ISaveService localSave)
        {
            return new FirebaseAccountService(
                new GuestAccountService(),
                localSave,
                new FirestoreCloudSaveService(
                    new UnavailableFirestoreDocumentGateway()),
                new UnavailableFirebaseAuthGateway(),
                new UnavailableAccountDeletionGateway());
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
