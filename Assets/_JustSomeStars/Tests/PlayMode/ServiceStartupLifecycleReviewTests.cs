using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Core;
using NUnit.Framework;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class ServiceStartupLifecycleReviewTests
    {
        public enum UnsuccessfulInitializationMode
        {
            SynchronousThrow,
            AsynchronousFault,
            UnavailableResult,
            FailedResult,
            InternalCancellation,
        }

        [TestCase(GameServiceRole.Settings)]
        [TestCase(GameServiceRole.LocalSave)]
        [TestCase(GameServiceRole.Input)]
        [TestCase(GameServiceRole.ContentCatalogue)]
        [TestCase(GameServiceRole.ModeController)]
        public async Task Startup_MissingOneRequiredRoleFailsPreflightInEnumOrder(
            GameServiceRole missingRole)
        {
            var events = new List<string>();
            var transition = new RecordingSceneTransition(events);
            var registrations = BootstrapTestComposition.RequiredRoles
                .Where(role => role != missingRole)
                .Reverse()
                .Select(role => new GameServiceRegistration(
                    role,
                    RecordingGameService.Available(role.ToString(), events)))
                .ToArray();
            var coordinator = new ServiceStartupCoordinator();

            try
            {
                var report = await coordinator.StartupAsync(
                    new GameBootstrapComposition(registrations, transition),
                    CancellationToken.None);

                Assert.That(report.IsSuccessful, Is.False);
                Assert.That(report.IsCancelled, Is.False);
                Assert.That(report.RoutedToFrontend, Is.False);
                Assert.That(
                    report.PrimaryFailure,
                    Is.InstanceOf<InvalidOperationException>());
                Assert.That(
                    report.PrimaryFailure?.Message,
                    Is.EqualTo($"Missing required service roles: {missingRole}."));
                Assert.That(report.Services, Is.Empty);
                Assert.That(events, Is.Empty);
                Assert.That(transition.Destinations, Is.Empty);
            }
            finally
            {
                await ShutdownQuietlyAsync(coordinator);
            }
        }

        [Test]
        public async Task Startup_MissingSeveralRequiredRolesReportsEveryRoleInEnumOrder()
        {
            var events = new List<string>();
            var transition = new RecordingSceneTransition(events);
            var coordinator = new ServiceStartupCoordinator();
            var registrations = new[]
            {
                new GameServiceRegistration(
                    GameServiceRole.ContentCatalogue,
                    RecordingGameService.Available("ContentCatalogue", events)),
                new GameServiceRegistration(
                    GameServiceRole.Settings,
                    RecordingGameService.Available("Settings", events)),
                new GameServiceRegistration(
                    GameServiceRole.Growth,
                    RecordingGameService.Available("Growth", events)),
            };

            try
            {
                var report = await coordinator.StartupAsync(
                    new GameBootstrapComposition(registrations, transition),
                    CancellationToken.None);

                Assert.That(report.IsSuccessful, Is.False);
                Assert.That(report.RoutedToFrontend, Is.False);
                Assert.That(
                    report.PrimaryFailure?.Message,
                    Is.EqualTo(
                        "Missing required service roles: LocalSave, Input, ModeController."));
                Assert.That(report.Services, Is.Empty);
                Assert.That(events, Is.Empty);
                Assert.That(transition.Destinations, Is.Empty);
            }
            finally
            {
                await ShutdownQuietlyAsync(coordinator);
            }
        }

        [Test]
        public async Task Startup_EmptyCompositionFailsGracefullyWithAllMissingRoles()
        {
            var events = new List<string>();
            var transition = new RecordingSceneTransition(events);
            var coordinator = new ServiceStartupCoordinator();

            try
            {
                var report = await coordinator.StartupAsync(
                    new GameBootstrapComposition(
                        Array.Empty<GameServiceRegistration>(),
                        transition),
                    CancellationToken.None);

                Assert.That(report.IsSuccessful, Is.False);
                Assert.That(report.IsCancelled, Is.False);
                Assert.That(report.RoutedToFrontend, Is.False);
                Assert.That(
                    report.PrimaryFailure?.Message,
                    Is.EqualTo(
                        "Missing required service roles: Settings, LocalSave, Input, " +
                        "ContentCatalogue, ModeController."));
                Assert.That(report.Services, Is.Empty);
                Assert.That(events, Is.Empty);
                Assert.That(transition.Destinations, Is.Empty);
            }
            finally
            {
                await ShutdownQuietlyAsync(coordinator);
            }
        }

        [Test]
        public void Composition_RejectsSameServiceInstanceAcrossDistinctRoles()
        {
            var events = new List<string>();
            var sharedService = RecordingGameService.Available(
                "SharedSettingsAndSave",
                events);
            var transition = new RecordingSceneTransition(events);
            var registrations = new[]
            {
                new GameServiceRegistration(GameServiceRole.Settings, sharedService),
                new GameServiceRegistration(GameServiceRole.LocalSave, sharedService),
                new GameServiceRegistration(
                    GameServiceRole.Input,
                    RecordingGameService.Available("Input", events)),
                new GameServiceRegistration(
                    GameServiceRole.ContentCatalogue,
                    RecordingGameService.Available("ContentCatalogue", events)),
                new GameServiceRegistration(
                    GameServiceRole.ModeController,
                    RecordingGameService.Available("ModeController", events)),
            };

            var exception = Assert.Throws<InvalidOperationException>(() =>
                new GameBootstrapComposition(registrations, transition));

            StringAssert.Contains("Settings", exception?.Message);
            StringAssert.Contains("LocalSave", exception?.Message);
            Assert.That(sharedService.InitializeCount, Is.EqualTo(0));
            Assert.That(sharedService.ShutdownCount, Is.EqualTo(0));
            Assert.That(events, Is.Empty);
            Assert.That(transition.Destinations, Is.Empty);
        }

        [TestCase(UnsuccessfulInitializationMode.SynchronousThrow)]
        [TestCase(UnsuccessfulInitializationMode.AsynchronousFault)]
        [TestCase(UnsuccessfulInitializationMode.UnavailableResult)]
        [TestCase(UnsuccessfulInitializationMode.FailedResult)]
        [TestCase(UnsuccessfulInitializationMode.InternalCancellation)]
        public async Task RequiredUnsuccessfulInitialization_CleansCurrentThenPriorServices(
            UnsuccessfulInitializationMode failureMode)
        {
            var events = new List<string>();
            using var callerLifetime = new CancellationTokenSource();
            Exception primaryFailure = failureMode ==
                UnsuccessfulInitializationMode.InternalCancellation
                    ? null
                    : new InvalidOperationException($"{failureMode} failed");
            var receivedToken = default(CancellationToken);
            var settings = RecordingGameService.Available("Settings", events);
            var localSave = RecordingGameService.Available("LocalSave", events);
            var input = new RecordingGameService(
                "Input",
                events,
                token =>
                {
                    receivedToken = token;
                    if (failureMode ==
                        UnsuccessfulInitializationMode.InternalCancellation)
                    {
                        primaryFailure = new OperationCanceledException(
                            "service cancelled its own operation",
                            token);
                        return new ValueTask<StartupResult>(
                            Task.FromException<StartupResult>(primaryFailure));
                    }

                    return CreateFailure(failureMode, primaryFailure)(token);
                });
            var contentCatalogue = RecordingGameService.Available(
                "ContentCatalogue",
                events);
            var modeController = RecordingGameService.Available(
                "ModeController",
                events);
            var overrides = new Dictionary<GameServiceRole, IGameService>
            {
                [GameServiceRole.Settings] = settings,
                [GameServiceRole.LocalSave] = localSave,
                [GameServiceRole.Input] = input,
                [GameServiceRole.ContentCatalogue] = contentCatalogue,
                [GameServiceRole.ModeController] = modeController,
            };
            var transition = new RecordingSceneTransition(events);
            var coordinator = new ServiceStartupCoordinator();

            try
            {
                var report = await coordinator.StartupAsync(
                    new GameBootstrapComposition(
                        BootstrapTestComposition.CompleteRequired(events, overrides),
                        transition),
                    callerLifetime.Token);
                var inputReport = report.Services.Single(
                    service => service.Role == GameServiceRole.Input);
                var receivedCancelableToken = receivedToken.CanBeCanceled;
                var receivedTokenWasCancelled = receivedToken.IsCancellationRequested;

                await coordinator.ShutdownAsync();
                await coordinator.ShutdownAsync();

                Assert.That(report.PrimaryFailure, Is.SameAs(primaryFailure));
                Assert.That(report.IsCancelled, Is.False);
                Assert.That(report.RoutedToFrontend, Is.False);
                Assert.That(inputReport.State, Is.EqualTo(ServiceStartupState.Failed));
                Assert.That(inputReport.Failure, Is.SameAs(primaryFailure));
                Assert.That(receivedCancelableToken, Is.True);
                Assert.That(receivedTokenWasCancelled, Is.False);
                Assert.That(settings.ShutdownCount, Is.EqualTo(1));
                Assert.That(localSave.ShutdownCount, Is.EqualTo(1));
                Assert.That(input.ShutdownCount, Is.EqualTo(1));
                Assert.That(contentCatalogue.InitializeCount, Is.EqualTo(0));
                Assert.That(contentCatalogue.ShutdownCount, Is.EqualTo(0));
                Assert.That(modeController.InitializeCount, Is.EqualTo(0));
                Assert.That(modeController.ShutdownCount, Is.EqualTo(0));
                Assert.That(transition.Destinations, Is.Empty);
                Assert.That(events, Is.EqualTo(new[]
                {
                    "initialize:Settings",
                    "initialize:LocalSave",
                    "initialize:Input",
                    "shutdown:Input",
                    "shutdown:LocalSave",
                    "shutdown:Settings",
                }));
            }
            finally
            {
                await ShutdownQuietlyAsync(coordinator);
            }
        }

        [Test]
        public async Task RequiredFailure_PreservesPrimaryAndEveryCleanupFailureInAttemptOrder()
        {
            var events = new List<string>();
            var primaryFailure = new InvalidOperationException("input startup failed");
            var inputCleanupFailure = new InvalidOperationException("input cleanup failed");
            var saveCleanupFailure = new InvalidOperationException("save cleanup failed");
            var settingsCleanupFailure = new InvalidOperationException("settings cleanup failed");
            var settings = RecordingGameService.Available(
                "Settings",
                events,
                () => new ValueTask(Task.FromException(settingsCleanupFailure)));
            var localSave = RecordingGameService.Available(
                "LocalSave",
                events,
                () => new ValueTask(Task.FromException(saveCleanupFailure)));
            var input = new RecordingGameService(
                "Input",
                events,
                _ => throw primaryFailure,
                () => new ValueTask(Task.FromException(inputCleanupFailure)));
            var overrides = new Dictionary<GameServiceRole, IGameService>
            {
                [GameServiceRole.Settings] = settings,
                [GameServiceRole.LocalSave] = localSave,
                [GameServiceRole.Input] = input,
            };
            var transition = new RecordingSceneTransition(events);
            var coordinator = new ServiceStartupCoordinator();

            try
            {
                var report = await coordinator.StartupAsync(
                    new GameBootstrapComposition(
                        BootstrapTestComposition.CompleteRequired(events, overrides),
                        transition),
                    CancellationToken.None);

                await coordinator.ShutdownAsync();
                await coordinator.ShutdownAsync();

                Assert.That(report.PrimaryFailure, Is.SameAs(primaryFailure));
                Assert.That(report.CleanupFailures, Is.EqualTo(new[]
                {
                    inputCleanupFailure,
                    saveCleanupFailure,
                    settingsCleanupFailure,
                }));
                Assert.That(events, Is.EqualTo(new[]
                {
                    "initialize:Settings",
                    "initialize:LocalSave",
                    "initialize:Input",
                    "shutdown:Input",
                    "shutdown:LocalSave",
                    "shutdown:Settings",
                }));
                Assert.That(transition.Destinations, Is.Empty);
                Assert.That(input.ShutdownCount, Is.EqualTo(1));
                Assert.That(localSave.ShutdownCount, Is.EqualTo(1));
                Assert.That(settings.ShutdownCount, Is.EqualTo(1));
            }
            finally
            {
                await ShutdownQuietlyAsync(coordinator);
            }
        }

        [Test]
        public async Task RequiredConcreteFaultAfterCallerCancellation_PreservesConcreteFailure()
        {
            var events = new List<string>();
            using var callerCancellation = new CancellationTokenSource();
            var concreteFailure = new InvalidOperationException(
                "local save faulted while cancellation became concurrent");
            var settings = RecordingGameService.Available("Settings", events);
            var localSave = new RecordingGameService(
                "LocalSave",
                events,
                _ =>
                {
                    callerCancellation.Cancel();
                    throw concreteFailure;
                });
            var nextService = RecordingGameService.Available("Input", events);
            var overrides = new Dictionary<GameServiceRole, IGameService>
            {
                [GameServiceRole.Settings] = settings,
                [GameServiceRole.LocalSave] = localSave,
                [GameServiceRole.Input] = nextService,
            };
            var transition = new RecordingSceneTransition(events);
            var coordinator = new ServiceStartupCoordinator();

            try
            {
                var report = await coordinator.StartupAsync(
                    new GameBootstrapComposition(
                        BootstrapTestComposition.CompleteRequired(events, overrides),
                        transition),
                    callerCancellation.Token);
                await coordinator.ShutdownAsync();
                await coordinator.ShutdownAsync();

                var localSaveReport = report.Services.Single(
                    service => service.Role == GameServiceRole.LocalSave);
                Assert.That(report.IsSuccessful, Is.False);
                Assert.That(report.IsCancelled, Is.False);
                Assert.That(report.PrimaryFailure, Is.SameAs(concreteFailure));
                Assert.That(localSaveReport.State, Is.EqualTo(ServiceStartupState.Failed));
                Assert.That(localSaveReport.Failure, Is.SameAs(concreteFailure));
                Assert.That(localSave.ShutdownCount, Is.EqualTo(1));
                Assert.That(settings.ShutdownCount, Is.EqualTo(1));
                Assert.That(nextService.InitializeCount, Is.EqualTo(0));
                Assert.That(nextService.ShutdownCount, Is.EqualTo(0));
                Assert.That(transition.Destinations, Is.Empty);
                Assert.That(events, Is.EqualTo(new[]
                {
                    "initialize:Settings",
                    "initialize:LocalSave",
                    "shutdown:LocalSave",
                    "shutdown:Settings",
                }));
            }
            finally
            {
                await ShutdownQuietlyAsync(coordinator);
            }
        }

        [Test]
        public async Task RequiredFailedResultAfterCallerCancellation_PreservesConcreteFailure()
        {
            var events = new List<string>();
            using var callerCancellation = new CancellationTokenSource();
            var concreteFailure = new InvalidOperationException(
                "local save returned a concrete failure during cancellation");
            var settings = RecordingGameService.Available("Settings", events);
            var localSave = new RecordingGameService(
                "LocalSave",
                events,
                _ =>
                {
                    callerCancellation.Cancel();
                    return new ValueTask<StartupResult>(
                        StartupResult.Failed(
                            "Local save failed during cancellation.",
                            concreteFailure));
                });
            var nextService = RecordingGameService.Available("Input", events);
            var overrides = new Dictionary<GameServiceRole, IGameService>
            {
                [GameServiceRole.Settings] = settings,
                [GameServiceRole.LocalSave] = localSave,
                [GameServiceRole.Input] = nextService,
            };
            var transition = new RecordingSceneTransition(events);
            var coordinator = new ServiceStartupCoordinator();

            try
            {
                var report = await coordinator.StartupAsync(
                    new GameBootstrapComposition(
                        BootstrapTestComposition.CompleteRequired(events, overrides),
                        transition),
                    callerCancellation.Token);
                await coordinator.ShutdownAsync();
                await coordinator.ShutdownAsync();

                var localSaveReport = report.Services.Single(
                    service => service.Role == GameServiceRole.LocalSave);
                Assert.That(report.IsSuccessful, Is.False);
                Assert.That(report.IsCancelled, Is.False);
                Assert.That(report.PrimaryFailure, Is.SameAs(concreteFailure));
                Assert.That(localSaveReport.State, Is.EqualTo(ServiceStartupState.Failed));
                Assert.That(localSaveReport.Failure, Is.SameAs(concreteFailure));
                Assert.That(localSave.ShutdownCount, Is.EqualTo(1));
                Assert.That(settings.ShutdownCount, Is.EqualTo(1));
                Assert.That(nextService.InitializeCount, Is.EqualTo(0));
                Assert.That(nextService.ShutdownCount, Is.EqualTo(0));
                Assert.That(transition.Destinations, Is.Empty);
                Assert.That(events, Is.EqualTo(new[]
                {
                    "initialize:Settings",
                    "initialize:LocalSave",
                    "shutdown:LocalSave",
                    "shutdown:Settings",
                }));
            }
            finally
            {
                await ShutdownQuietlyAsync(coordinator);
            }
        }

        [Test]
        public async Task CleanupFailures_PropertyReturnsStableSnapshotsAcrossShutdown()
        {
            var events = new List<string>();
            var cleanupFailure = new InvalidOperationException(
                "mode controller shutdown failed");
            var modeController = RecordingGameService.Available(
                "ModeController",
                events,
                () => new ValueTask(Task.FromException(cleanupFailure)));
            var overrides = new Dictionary<GameServiceRole, IGameService>
            {
                [GameServiceRole.ModeController] = modeController,
            };
            var coordinator = new ServiceStartupCoordinator();

            try
            {
                var report = await coordinator.StartupAsync(
                    new GameBootstrapComposition(
                        BootstrapTestComposition.CompleteRequired(events, overrides),
                        new RecordingSceneTransition(events)),
                    CancellationToken.None);
                var capturedBeforeShutdown = report.CleanupFailures;

                await coordinator.ShutdownAsync();

                Assert.That(capturedBeforeShutdown, Is.Empty);
                Assert.That(
                    report.CleanupFailures,
                    Is.EqualTo(new[] { cleanupFailure }));
                Assert.That(
                    report.CleanupFailures,
                    Is.Not.SameAs(capturedBeforeShutdown));
                Assert.That(modeController.ShutdownCount, Is.EqualTo(1));
            }
            finally
            {
                await ShutdownQuietlyAsync(coordinator);
            }
        }

        [TestCase(UnsuccessfulInitializationMode.SynchronousThrow)]
        [TestCase(UnsuccessfulInitializationMode.AsynchronousFault)]
        [TestCase(UnsuccessfulInitializationMode.UnavailableResult)]
        [TestCase(UnsuccessfulInitializationMode.FailedResult)]
        [TestCase(UnsuccessfulInitializationMode.InternalCancellation)]
        public async Task OptionalUnsuccessfulInitialization_CleansOnceAndContinues(
            UnsuccessfulInitializationMode failureMode)
        {
            var events = new List<string>();
            using var callerLifetime = new CancellationTokenSource();
            Exception optionalFailure = failureMode ==
                UnsuccessfulInitializationMode.InternalCancellation
                    ? null
                    : new InvalidOperationException($"optional {failureMode} failed");
            var cleanupFailure = failureMode ==
                UnsuccessfulInitializationMode.UnavailableResult
                    ? new InvalidOperationException("cloud cleanup failed")
                    : null;
            var receivedToken = default(CancellationToken);
            var cloud = new RecordingGameService(
                "Cloud",
                events,
                token =>
                {
                    receivedToken = token;
                    if (failureMode ==
                        UnsuccessfulInitializationMode.InternalCancellation)
                    {
                        optionalFailure = new OperationCanceledException(
                            "optional service cancelled its own operation",
                            token);
                        return new ValueTask<StartupResult>(
                            Task.FromException<StartupResult>(optionalFailure));
                    }

                    return CreateFailure(failureMode, optionalFailure)(token);
                },
                cleanupFailure == null
                    ? null
                    : () => new ValueTask(Task.FromException(cleanupFailure)));
            var settings = RecordingGameService.Available("Settings", events);
            var localSave = RecordingGameService.Available("LocalSave", events);
            var input = RecordingGameService.Available("Input", events);
            var contentCatalogue = RecordingGameService.Available(
                "ContentCatalogue",
                events);
            var modeController = RecordingGameService.Available(
                "ModeController",
                events);
            var requiredOverrides = new Dictionary<GameServiceRole, IGameService>
            {
                [GameServiceRole.Settings] = settings,
                [GameServiceRole.LocalSave] = localSave,
                [GameServiceRole.Input] = input,
                [GameServiceRole.ContentCatalogue] = contentCatalogue,
                [GameServiceRole.ModeController] = modeController,
            };
            var commerce = RecordingGameService.Available("Commerce", events);
            var transition = new RecordingSceneTransition(events);
            var coordinator = new ServiceStartupCoordinator();

            try
            {
                var report = await coordinator.StartupAsync(
                    new GameBootstrapComposition(
                        BootstrapTestComposition.CompleteWithOptional(
                            events,
                            new[]
                            {
                                new GameServiceRegistration(GameServiceRole.Cloud, cloud),
                                new GameServiceRegistration(GameServiceRole.Commerce, commerce),
                            },
                            requiredOverrides),
                        transition),
                    callerLifetime.Token);
                var cloudReport = report.Services.Single(
                    service => service.Role == GameServiceRole.Cloud);
                var receivedCancelableToken = receivedToken.CanBeCanceled;
                var receivedTokenWasCancelled = receivedToken.IsCancellationRequested;

                await coordinator.ShutdownAsync();
                await coordinator.ShutdownAsync();

                Assert.That(report.IsSuccessful, Is.True);
                Assert.That(report.IsCancelled, Is.False);
                Assert.That(report.PrimaryFailure, Is.Null);
                if (cleanupFailure == null)
                {
                    Assert.That(report.CleanupFailures, Is.Empty);
                }
                else
                {
                    Assert.That(
                        report.CleanupFailures,
                        Is.EqualTo(new[] { cleanupFailure }));
                }

                Assert.That(cloudReport.State, Is.EqualTo(ServiceStartupState.Unavailable));
                Assert.That(cloudReport.Failure, Is.SameAs(optionalFailure));
                Assert.That(receivedCancelableToken, Is.True);
                Assert.That(receivedTokenWasCancelled, Is.False);
                Assert.That(cloud.ShutdownCount, Is.EqualTo(1));
                Assert.That(commerce.ShutdownCount, Is.EqualTo(1));
                Assert.That(modeController.ShutdownCount, Is.EqualTo(1));
                Assert.That(contentCatalogue.ShutdownCount, Is.EqualTo(1));
                Assert.That(input.ShutdownCount, Is.EqualTo(1));
                Assert.That(localSave.ShutdownCount, Is.EqualTo(1));
                Assert.That(settings.ShutdownCount, Is.EqualTo(1));
                Assert.That(transition.Destinations, Is.EqualTo(new[] { "Frontend" }));
                Assert.That(events, Is.EqualTo(new[]
                {
                    "initialize:Settings",
                    "initialize:LocalSave",
                    "initialize:Input",
                    "initialize:ContentCatalogue",
                    "initialize:ModeController",
                    "initialize:Cloud",
                    "shutdown:Cloud",
                    "initialize:Commerce",
                    "route:Frontend",
                    "shutdown:Commerce",
                    "shutdown:ModeController",
                    "shutdown:ContentCatalogue",
                    "shutdown:Input",
                    "shutdown:LocalSave",
                    "shutdown:Settings",
                }));
            }
            finally
            {
                await ShutdownQuietlyAsync(coordinator);
            }
        }

        [Test]
        public async Task ShutdownDuringPendingInitialization_CancelsStartupCleansAndNeverRoutes()
        {
            var events = new List<string>();
            var initializeEntered = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var initializeRelease = new TaskCompletionSource<StartupResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var cancellationObserved = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var cancellationRegistration = default(CancellationTokenRegistration);
            var pendingSettings = new RecordingGameService(
                "Settings",
                events,
                token =>
                {
                    cancellationRegistration = token.Register(() =>
                    {
                        cancellationObserved.TrySetResult(null);
                        initializeRelease.TrySetCanceled(token);
                    });
                    initializeEntered.TrySetResult(null);
                    return new ValueTask<StartupResult>(initializeRelease.Task);
                });
            var overrides = new Dictionary<GameServiceRole, IGameService>
            {
                [GameServiceRole.Settings] = pendingSettings,
            };
            var transition = new RecordingSceneTransition(events);
            var coordinator = new ServiceStartupCoordinator();
            Task<StartupReport> startupTask = null;
            Task shutdownTask = null;

            try
            {
                startupTask = coordinator.StartupAsync(
                    new GameBootstrapComposition(
                        BootstrapTestComposition.CompleteRequired(events, overrides),
                        transition),
                    CancellationToken.None).AsTask();
                await BoundedTestTask.Complete(
                    initializeEntered.Task,
                    "pending service initialization entry");

                shutdownTask = coordinator.ShutdownAsync().AsTask();

                await BoundedTestTask.Complete(
                    cancellationObserved.Task,
                    "coordinator shutdown cancellation");
                var report = await BoundedTestTask.Complete(
                    startupTask,
                    "cancelled startup");
                await BoundedTestTask.Complete(shutdownTask, "shutdown completion");
                await coordinator.ShutdownAsync();

                Assert.That(report.IsCancelled, Is.True);
                Assert.That(report.RoutedToFrontend, Is.False);
                Assert.That(
                    report.PrimaryFailure,
                    Is.InstanceOf<OperationCanceledException>());
                Assert.That(pendingSettings.ShutdownCount, Is.EqualTo(1));
                Assert.That(transition.Destinations, Is.Empty);
                Assert.That(events, Is.EqualTo(new[]
                {
                    "initialize:Settings",
                    "shutdown:Settings",
                }));
                Assert.That(pendingSettings.ShutdownCount, Is.EqualTo(1));
            }
            finally
            {
                initializeRelease.TrySetResult(StartupResult.Available());
                cancellationRegistration.Dispose();
                await DrainQuietlyAsync(startupTask);
                await DrainQuietlyAsync(shutdownTask);
                await ShutdownQuietlyAsync(coordinator);
            }
        }

        [Test]
        public async Task ShutdownDuringNonCooperativeInitialization_CleansReturnedServiceAndStops()
        {
            var events = new List<string>();
            var initializeEntered = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var initializeRelease = new TaskCompletionSource<StartupResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var cancellationObserved = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var cancellationRegistration = default(CancellationTokenRegistration);
            var pendingSettings = new RecordingGameService(
                "Settings",
                events,
                token =>
                {
                    cancellationRegistration = token.Register(
                        () => cancellationObserved.TrySetResult(null));
                    initializeEntered.TrySetResult(null);
                    return new ValueTask<StartupResult>(initializeRelease.Task);
                });
            var nextService = RecordingGameService.Available("LocalSave", events);
            var overrides = new Dictionary<GameServiceRole, IGameService>
            {
                [GameServiceRole.Settings] = pendingSettings,
                [GameServiceRole.LocalSave] = nextService,
            };
            var transition = new RecordingSceneTransition(events);
            var coordinator = new ServiceStartupCoordinator();
            Task<StartupReport> startupTask = null;
            Task firstShutdown = null;
            Task secondShutdown = null;

            try
            {
                startupTask = coordinator.StartupAsync(
                    new GameBootstrapComposition(
                        BootstrapTestComposition.CompleteRequired(events, overrides),
                        transition),
                    CancellationToken.None).AsTask();
                await BoundedTestTask.Complete(
                    initializeEntered.Task,
                    "non-cooperative service initialization entry");

                firstShutdown = coordinator.ShutdownAsync().AsTask();
                secondShutdown = coordinator.ShutdownAsync().AsTask();
                await BoundedTestTask.Complete(
                    cancellationObserved.Task,
                    "non-cooperative service cancellation observation");
                Assert.That(
                    pendingSettings.LastInitializeToken.CanBeCanceled,
                    Is.True);
                Assert.That(
                    pendingSettings.LastInitializeToken.IsCancellationRequested,
                    Is.True);

                initializeRelease.TrySetResult(StartupResult.Available());
                var report = await BoundedTestTask.Complete(
                    startupTask,
                    "non-cooperative cancelled startup");
                await BoundedTestTask.Complete(
                    firstShutdown,
                    "first non-cooperative shutdown");
                await BoundedTestTask.Complete(
                    secondShutdown,
                    "second non-cooperative shutdown");

                Assert.That(report.IsCancelled, Is.True);
                Assert.That(report.RoutedToFrontend, Is.False);
                Assert.That(
                    report.PrimaryFailure,
                    Is.InstanceOf<OperationCanceledException>());
                Assert.That(pendingSettings.InitializeCount, Is.EqualTo(1));
                Assert.That(pendingSettings.ShutdownCount, Is.EqualTo(1));
                Assert.That(nextService.InitializeCount, Is.EqualTo(0));
                Assert.That(nextService.ShutdownCount, Is.EqualTo(0));
                Assert.That(transition.Destinations, Is.Empty);
                Assert.That(events, Is.EqualTo(new[]
                {
                    "initialize:Settings",
                    "shutdown:Settings",
                }));
            }
            finally
            {
                initializeRelease.TrySetResult(StartupResult.Available());
                cancellationRegistration.Dispose();
                await DrainQuietlyAsync(startupTask);
                await DrainQuietlyAsync(firstShutdown);
                await DrainQuietlyAsync(secondShutdown);
                await ShutdownQuietlyAsync(coordinator);
            }
        }

        [Test]
        public async Task FinalRequiredServiceCancelsCallerBeforeReturn_TransitionIsNeverIssued()
        {
            var events = new List<string>();
            using var callerCancellation = new CancellationTokenSource();
            var settings = RecordingGameService.Available("Settings", events);
            var localSave = RecordingGameService.Available("LocalSave", events);
            var input = RecordingGameService.Available("Input", events);
            var contentCatalogue = RecordingGameService.Available(
                "ContentCatalogue",
                events);
            var finalServiceReceivedCancelableToken = false;
            var finalServiceTokenWasCancelled = true;
            var modeController = new RecordingGameService(
                "ModeController",
                events,
                token =>
                {
                    finalServiceReceivedCancelableToken = token.CanBeCanceled;
                    finalServiceTokenWasCancelled = token.IsCancellationRequested;
                    callerCancellation.Cancel();
                    return new ValueTask<StartupResult>(StartupResult.Available());
                });
            var overrides = new Dictionary<GameServiceRole, IGameService>
            {
                [GameServiceRole.Settings] = settings,
                [GameServiceRole.LocalSave] = localSave,
                [GameServiceRole.Input] = input,
                [GameServiceRole.ContentCatalogue] = contentCatalogue,
                [GameServiceRole.ModeController] = modeController,
            };
            var transition = new RecordingSceneTransition(events);
            var coordinator = new ServiceStartupCoordinator();

            try
            {
                var report = await coordinator.StartupAsync(
                    new GameBootstrapComposition(
                        BootstrapTestComposition.CompleteRequired(events, overrides),
                        transition),
                    callerCancellation.Token);

                await coordinator.ShutdownAsync();
                await coordinator.ShutdownAsync();

                Assert.That(report.IsCancelled, Is.True);
                Assert.That(report.RoutedToFrontend, Is.False);
                Assert.That(finalServiceReceivedCancelableToken, Is.True);
                Assert.That(finalServiceTokenWasCancelled, Is.False);
                Assert.That(transition.Destinations, Is.Empty);
                Assert.That(modeController.ShutdownCount, Is.EqualTo(1));
                Assert.That(contentCatalogue.ShutdownCount, Is.EqualTo(1));
                Assert.That(input.ShutdownCount, Is.EqualTo(1));
                Assert.That(localSave.ShutdownCount, Is.EqualTo(1));
                Assert.That(settings.ShutdownCount, Is.EqualTo(1));
                Assert.That(events, Is.EqualTo(new[]
                {
                    "initialize:Settings",
                    "initialize:LocalSave",
                    "initialize:Input",
                    "initialize:ContentCatalogue",
                    "initialize:ModeController",
                    "shutdown:ModeController",
                    "shutdown:ContentCatalogue",
                    "shutdown:Input",
                    "shutdown:LocalSave",
                    "shutdown:Settings",
                }));
            }
            finally
            {
                await ShutdownQuietlyAsync(coordinator);
            }
        }

        [Test]
        public async Task RoutePointOfNoReturn_UsesNoCancellationAndReportsCompletedRouteTruthfully()
        {
            var events = new List<string>();
            var routeEntered = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var routeRelease = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var transition = new RecordingSceneTransition(events, AwaitRouteAsync);
            var coordinator = new ServiceStartupCoordinator();
            using var callerCancellation = new CancellationTokenSource();
            Task<StartupReport> startupTask = null;
            Task shutdownTask = null;

            try
            {
                startupTask = coordinator.StartupAsync(
                    new GameBootstrapComposition(
                        BootstrapTestComposition.CompleteRequired(events),
                        transition),
                    callerCancellation.Token).AsTask();
                await BoundedTestTask.Complete(
                    routeEntered.Task,
                    "route point-of-no-return entry");

                shutdownTask = coordinator.ShutdownAsync().AsTask();
                callerCancellation.Cancel();
                routeRelease.TrySetResult(null);

                var report = await BoundedTestTask.Complete(
                    startupTask,
                    "route completion after cancellation");
                await BoundedTestTask.Complete(
                    shutdownTask,
                    "shutdown after route completion");

                Assert.That(transition.Tokens, Has.Count.EqualTo(1));
                Assert.That(transition.Tokens[0].CanBeCanceled, Is.False);
                Assert.That(report.IsCancelled, Is.False);
                Assert.That(report.PrimaryFailure, Is.Null);
                Assert.That(report.IsSuccessful, Is.True);
                Assert.That(report.RoutedToFrontend, Is.True);
                Assert.That(report.RequestedDestination, Is.EqualTo("Frontend"));
                Assert.That(transition.Destinations, Is.EqualTo(new[] { "Frontend" }));
                Assert.That(events.Take(6), Is.EqualTo(new[]
                {
                    "initialize:Settings",
                    "initialize:LocalSave",
                    "initialize:Input",
                    "initialize:ContentCatalogue",
                    "initialize:ModeController",
                    "route:Frontend",
                }));
                Assert.That(events.Skip(6), Is.EqualTo(new[]
                {
                    "shutdown:ModeController",
                    "shutdown:ContentCatalogue",
                    "shutdown:Input",
                    "shutdown:LocalSave",
                    "shutdown:Settings",
                }));
            }
            finally
            {
                routeRelease.TrySetResult(null);
                await DrainQuietlyAsync(startupTask);
                await DrainQuietlyAsync(shutdownTask);
                await ShutdownQuietlyAsync(coordinator);
            }

            async ValueTask AwaitRouteAsync(
                string _,
                CancellationToken cancellationToken)
            {
                routeEntered.TrySetResult(null);
                await routeRelease.Task;
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        private static Func<CancellationToken, ValueTask<StartupResult>>
            CreateFailure(
                UnsuccessfulInitializationMode failureMode,
                Exception failure)
        {
            return failureMode switch
            {
                UnsuccessfulInitializationMode.SynchronousThrow => _ => throw failure,
                UnsuccessfulInitializationMode.AsynchronousFault => _ =>
                    new ValueTask<StartupResult>(
                        Task.FromException<StartupResult>(failure)),
                UnsuccessfulInitializationMode.UnavailableResult => _ =>
                    new ValueTask<StartupResult>(
                        StartupResult.Unavailable("Input is unavailable.", failure)),
                UnsuccessfulInitializationMode.FailedResult => _ =>
                    new ValueTask<StartupResult>(
                        StartupResult.Failed("Input failed.", failure)),
                UnsuccessfulInitializationMode.InternalCancellation => _ =>
                    new ValueTask<StartupResult>(
                        Task.FromException<StartupResult>(failure)),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(failureMode),
                    failureMode,
                    null),
            };
        }

        private static async Task ShutdownQuietlyAsync(
            ServiceStartupCoordinator coordinator)
        {
            if (coordinator == null)
            {
                return;
            }

            await coordinator.ShutdownAsync();
        }

        private static async Task DrainQuietlyAsync(Task task)
        {
            if (task == null)
            {
                return;
            }

            var completed = await Task.WhenAny(task, Task.Delay(3000));
            if (completed != task)
            {
                return;
            }

            try
            {
                await task;
            }
            catch
            {
                // The assertions report lifecycle failures; teardown only drains tasks.
            }
        }
    }
}
