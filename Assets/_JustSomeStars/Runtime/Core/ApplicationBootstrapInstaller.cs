using System;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accounts;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Commerce;
using JustSomeStars.Runtime.Cinematics;
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
                progression,
                commerce,
                account);
            var sceneRouter = new SceneRoutingTransition();
            var sceneTransition = new UnitySceneTransition(
                new FrontendDependencies(
                    settings: settings,
                    input: input,
                    account: account,
                    beginChapterOne: null,
                    saves: saves,
                    startNewGame: token => StartNewChapterOneAsync(
                        saves,
                        modeController,
                        sceneRouter,
                        token),
                    continueGame: (save, token) => ContinueChapterOneAsync(
                        save,
                        progression,
                        modeController,
                        sceneRouter,
                        token),
                    canContinue: save => CanContinueChapterOne(save, progression),
                    describeCheckpoint: save => DescribeCheckpoint(save, progression)),
                surfaceDependencies);
            surfaceDependencies.ConfigureSceneTransition(sceneRouter);
            sceneTransition.ConfigureFlightDependencies(
                new FlightGameplayDependencies(
                    settings,
                    input,
                    modeController,
                    gameEvents,
                    sceneRouter,
                    progression,
                    commerce,
                    account,
                    saves));
            sceneTransition.ConfigureChapterOneDependencies(
                new ChapterOneSequenceDependencies(
                    saves,
                    input,
                    modeController,
                    gameEvents,
                    sceneRouter,
                    progression,
                    settings,
                    account,
                    commerce));
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
                progression,
                sceneRouter);
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
            IChapterProgression progression = null,
            SceneRoutingTransition sceneRouter = null)
        {
            var sceneStream = new SceneStreamService(
                catalogSource,
                sceneBackend,
                sceneTransition,
                modeController);
            sceneRouter?.Configure(sceneStream, sceneTransition);
            var routedTransition = (ISceneTransition)sceneRouter ?? sceneTransition;
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
                return new GameBootstrapComposition(registrations, routedTransition);
            }

            return new GameBootstrapComposition(
                registrations,
                routedTransition,
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

        private static async ValueTask StartNewChapterOneAsync(
            ISaveService saves,
            GameModeController modes,
            ISceneTransition scenes,
            CancellationToken cancellationToken)
        {
            var save = GameSave.CreateNew(
                Guid.NewGuid().ToString("N"),
                DateTime.UtcNow.Ticks);
            await saves.SaveCheckpointAsync(save, cancellationToken);
            await EnterChapterModeAsync(modes, GameMode.Clubhouse, cancellationToken);
            await scenes.RouteAsync("Opening", cancellationToken);
        }

        private static async ValueTask ContinueChapterOneAsync(
            GameSave save,
            DestinationProgressionCoordinator progression,
            GameModeController modes,
            ISceneTransition scenes,
            CancellationToken cancellationToken)
        {
            if (!CanContinueChapterOne(save, progression))
            {
                throw new InvalidOperationException(
                    "Continue requires a valid installed Chapter One checkpoint.");
            }
            await EnterChapterModeAsync(
                modes,
                progression.ResumeMode,
                cancellationToken);
            await scenes.RouteAsync(progression.ResumeSceneName, cancellationToken);
        }

        private static bool CanContinueChapterOne(
            GameSave save,
            DestinationProgressionCoordinator progression)
        {
            return save != null && progression != null &&
                save.ChapterOne.Phase >= ChapterOnePhase.OpeningComplete &&
                !string.IsNullOrWhiteSpace(progression.ResumeSceneName);
        }

        private static string DescribeCheckpoint(
            GameSave save,
            DestinationProgressionCoordinator progression)
        {
            return CanContinueChapterOne(save, progression)
                ? progression.ResumeSceneName
                : "Opening";
        }

        private static async ValueTask EnterChapterModeAsync(
            GameModeController modes,
            GameMode destination,
            CancellationToken cancellationToken)
        {
            if (destination != GameMode.Clubhouse &&
                destination != GameMode.Flight &&
                destination != GameMode.Surface)
            {
                throw new InvalidOperationException(
                    $"Chapter routing cannot enter unsupported mode '{destination}'.");
            }

            for (var transition = 0;
                 modes.CurrentMode != destination && transition < 6;
                 transition++)
            {
                var next = modes.CurrentMode switch
                {
                    GameMode.Frontend => GameMode.Customization,
                    GameMode.Customization => GameMode.Clubhouse,
                    GameMode.Clubhouse => GameMode.Flight,
                    GameMode.Flight when destination == GameMode.Surface =>
                        GameMode.Surface,
                    GameMode.Flight => GameMode.Clubhouse,
                    GameMode.Surface => GameMode.Flight,
                    GameMode.Lens or GameMode.Dialogue or GameMode.Cinematic =>
                        GameMode.Surface,
                    _ => throw new InvalidOperationException(
                        $"No Chapter One route exists from '{modes.CurrentMode}'."),
                };
                await modes.EnterAsync(next, cancellationToken);
            }

            if (modes.CurrentMode != destination)
            {
                throw new InvalidOperationException(
                    $"Chapter routing could not reach mode '{destination}'.");
            }
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
