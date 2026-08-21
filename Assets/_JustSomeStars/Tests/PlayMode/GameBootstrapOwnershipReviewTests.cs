using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class GameBootstrapOwnershipReviewTests
    {
        private readonly List<Action> m_EmergencyReleases = new List<Action>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameBootstrap.CompositionFactory = null;
            DestroyAllBootstraps();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var release in m_EmergencyReleases)
            {
                release();
            }

            m_EmergencyReleases.Clear();
            GameBootstrap.CompositionFactory = null;
            DestroyAllBootstraps();

            const int cleanupFrames = 5;
            for (var frame = 0; frame < cleanupFrames; frame++)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ActiveDestroy_ShutsDownSuccessfulLifecycleInReverseOrder()
        {
            var events = new List<string>();
            var transition = new RecordingSceneTransition(events);
            var services = CreateNamedRequiredServices(events);
            var serviceOverrides = services.ToDictionary(
                pair => pair.Key,
                pair => (IGameService)pair.Value);
            GameBootstrap.CompositionFactory = () =>
                new GameBootstrapComposition(
                    BootstrapTestComposition.CompleteRequired(
                        events,
                        serviceOverrides),
                    transition);

            var bootstrap = new GameObject("ActiveTeardownBootstrap")
                .AddComponent<GameBootstrap>();
            yield return WaitForReport(bootstrap, "active bootstrap startup");

            Assert.That(bootstrap.LastStartupReport.IsSuccessful, Is.True);
            var lifetimeToken = services[GameServiceRole.Settings].LastInitializeToken;
            Assert.That(lifetimeToken.CanBeCanceled, Is.True);
            Assert.That(lifetimeToken.IsCancellationRequested, Is.False);
            events.Clear();
            UnityEngine.Object.Destroy(bootstrap.gameObject);

            yield return WaitForCondition(
                () => services[GameServiceRole.Settings].ShutdownCount == 1,
                "active bootstrap teardown");

            Assert.That(events, Is.EqualTo(new[]
            {
                "shutdown:ModeController",
                "shutdown:ContentCatalogue",
                "shutdown:Input",
                "shutdown:LocalSave",
                "shutdown:Settings",
            }));
            foreach (var service in services.Values)
            {
                Assert.That(service.ShutdownCount, Is.EqualTo(1));
            }
        }

        [UnityTest]
        public IEnumerator CurrentPrimaryManualStartup_SharesAutomaticLifecycleAndRoutesOnce()
        {
            var initializeEntered = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var initializeRelease = new TaskCompletionSource<StartupResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            m_EmergencyReleases.Add(
                () => initializeRelease.TrySetResult(StartupResult.Available()));

            var events = new List<string>();
            var services = CreateNamedRequiredServices(events);
            services[GameServiceRole.Settings] = new RecordingGameService(
                "Settings",
                events,
                _ =>
                {
                    initializeEntered.TrySetResult(null);
                    return new ValueTask<StartupResult>(initializeRelease.Task);
                });
            var overrides = services.ToDictionary(
                pair => pair.Key,
                pair => (IGameService)pair.Value);
            var transition = new RecordingSceneTransition(events);
            var composition = new GameBootstrapComposition(
                BootstrapTestComposition.CompleteRequired(events, overrides),
                transition);
            GameBootstrap.CompositionFactory = () => composition;
            var primary = new GameObject("CurrentPrimaryManualStartup")
                .AddComponent<GameBootstrap>();

            var first = primary.StartupAsync(composition, default).AsTask();
            var second = primary.StartupAsync(composition, default).AsTask();
            yield return WaitForTask(
                initializeEntered.Task,
                "current-primary manual initialization entry");
            Assert.That(services[GameServiceRole.Settings].InitializeCount, Is.EqualTo(1));

            initializeRelease.TrySetResult(StartupResult.Available());
            yield return WaitForTask(first, "first current-primary startup call");
            yield return WaitForTask(second, "second current-primary startup call");
            yield return WaitForReport(primary, "automatic current-primary startup");

            Assert.That(second.Result, Is.SameAs(first.Result));
            Assert.That(primary.LastStartupReport, Is.SameAs(first.Result));
            Assert.That(first.Result.IsSuccessful, Is.True);
            Assert.That(transition.Destinations, Is.EqualTo(new[] { "Frontend" }));
            foreach (var service in services.Values)
            {
                Assert.That(service.InitializeCount, Is.EqualTo(1));
            }

            var shutdown = primary.ShutdownAsync().AsTask();
            yield return WaitForTask(shutdown, "current-primary explicit shutdown");
            UnityEngine.Object.Destroy(primary.gameObject);
            yield return WaitForCondition(
                () => primary == null,
                "current-primary destruction after shutdown");
            foreach (var service in services.Values)
            {
                Assert.That(service.ShutdownCount, Is.EqualTo(1));
            }
        }

        [UnityTest]
        public IEnumerator DuplicateManualStartup_IsRejectedWithoutCoordinatorWork()
        {
            var primaryEvents = new List<string>();
            GameBootstrap.CompositionFactory = () =>
                new GameBootstrapComposition(
                    BootstrapTestComposition.CompleteRequired(primaryEvents),
                    new RecordingSceneTransition(primaryEvents));
            var primary = new GameObject("ManualDuplicatePrimary")
                .AddComponent<GameBootstrap>();
            yield return WaitForReport(primary, "manual-duplicate primary startup");

            var duplicateEvents = new List<string>();
            var duplicateFailure = new InvalidOperationException(
                "a duplicate must never initialize this service");
            var duplicateSettings = new RecordingGameService(
                "DuplicateSettings",
                duplicateEvents,
                _ => throw duplicateFailure);
            var duplicateOverrides = new Dictionary<GameServiceRole, IGameService>
            {
                [GameServiceRole.Settings] = duplicateSettings,
            };
            var duplicateTransition = new RecordingSceneTransition(duplicateEvents);
            var duplicateComposition = new GameBootstrapComposition(
                BootstrapTestComposition.CompleteRequired(
                    duplicateEvents,
                    duplicateOverrides),
                duplicateTransition);
            var duplicate = new GameObject("ManualDuplicatePendingDestroy")
                .AddComponent<GameBootstrap>();
            Exception rejection = null;
            Task<StartupReport> unexpectedStartup = null;

            try
            {
                unexpectedStartup = duplicate.StartupAsync(
                    duplicateComposition,
                    default).AsTask();
            }
            catch (Exception exception)
            {
                rejection = exception;
            }

            if (unexpectedStartup != null)
            {
                yield return WaitForTask(
                    unexpectedStartup,
                    "unexpected duplicate manual startup");
                if (unexpectedStartup.IsFaulted)
                {
                    rejection = unexpectedStartup.Exception?.GetBaseException();
                }
            }

            yield return WaitForCondition(
                () => duplicate == null,
                "deferred duplicate destruction");

            Assert.That(
                new[]
                {
                    rejection?.GetType().Name ?? "<no rejection>",
                    duplicateSettings.InitializeCount.ToString(),
                    duplicateSettings.ShutdownCount.ToString(),
                    duplicateTransition.Destinations.Count.ToString(),
                },
                Is.EqualTo(new[]
                {
                    nameof(InvalidOperationException),
                    "0",
                    "0",
                    "0",
                }));
            StringAssert.Contains("current primary", rejection?.Message);
            Assert.That(duplicateEvents, Is.Empty);
        }

        [UnityTest]
        public IEnumerator SubsystemResetWithLivePrimary_PreservesOwnershipAndDestroyBarrier()
        {
            var cleanupEntered = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var cleanupRelease = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            m_EmergencyReleases.Add(() => cleanupRelease.TrySetResult(null));

            var firstEvents = new List<string>();
            var firstServices = CreateNamedRequiredServices(firstEvents);
            firstServices[GameServiceRole.ModeController] =
                RecordingGameService.Available(
                    "ModeController",
                    firstEvents,
                    () =>
                    {
                        cleanupEntered.TrySetResult(null);
                        return new ValueTask(cleanupRelease.Task);
                    });
            var firstOverrides = firstServices.ToDictionary(
                pair => pair.Key,
                pair => (IGameService)pair.Value);
            GameBootstrap.CompositionFactory = () =>
                new GameBootstrapComposition(
                    BootstrapTestComposition.CompleteRequired(
                        firstEvents,
                        firstOverrides),
                    new RecordingSceneTransition(firstEvents));
            var first = new GameObject("LiveResetPrimary")
                .AddComponent<GameBootstrap>();
            yield return WaitForReport(first, "live pre-reset primary startup");
            firstEvents.Clear();

            InvokeSubsystemRegistrationReset();

            var probeEvents = new List<string>();
            var probeSettings = RecordingGameService.Available(
                "Settings",
                probeEvents);
            var probeOverrides = new Dictionary<GameServiceRole, IGameService>
            {
                [GameServiceRole.Settings] = probeSettings,
            };
            var probeTransition = new RecordingSceneTransition(probeEvents);
            GameBootstrap.CompositionFactory = () =>
                new GameBootstrapComposition(
                    BootstrapTestComposition.CompleteRequired(
                        probeEvents,
                        probeOverrides),
                    probeTransition);
            var probe = new GameObject("LiveResetOwnershipProbe")
                .AddComponent<GameBootstrap>();
            yield return null;
            yield return null;

            Assert.That(
                new[]
                {
                    (probe == null).ToString(),
                    probeSettings.InitializeCount.ToString(),
                    probeTransition.Destinations.Count.ToString(),
                },
                Is.EqualTo(new[] { "True", "0", "0" }));
            Assert.That(probeEvents, Is.Empty);

            UnityEngine.Object.Destroy(first.gameObject);
            yield return WaitForCondition(
                () => first == null,
                "live pre-reset primary destruction");

            var replacementEvents = new List<string>();
            var replacementSettings = RecordingGameService.Available(
                "Settings",
                replacementEvents);
            var replacementOverrides = new Dictionary<GameServiceRole, IGameService>
            {
                [GameServiceRole.Settings] = replacementSettings,
            };
            var replacementTransition = new RecordingSceneTransition(
                replacementEvents);
            GameBootstrap.CompositionFactory = () =>
                new GameBootstrapComposition(
                    BootstrapTestComposition.CompleteRequired(
                        replacementEvents,
                        replacementOverrides),
                    replacementTransition);
            var replacement = new GameObject("LiveResetReplacement")
                .AddComponent<GameBootstrap>();

            try
            {
                yield return WaitForTask(
                    cleanupEntered.Task,
                    "live-reset primary cleanup entry");

                Assert.That(
                    new[]
                    {
                        replacementSettings.InitializeCount.ToString(),
                        replacementTransition.Destinations.Count.ToString(),
                    },
                    Is.EqualTo(new[] { "0", "0" }));

                cleanupRelease.TrySetResult(null);
                yield return WaitForReport(
                    replacement,
                    "live-reset replacement startup after cleanup");

                Assert.That(firstEvents, Is.EqualTo(new[]
                {
                    "shutdown:ModeController",
                    "shutdown:ContentCatalogue",
                    "shutdown:Input",
                    "shutdown:LocalSave",
                    "shutdown:Settings",
                }));
                foreach (var service in firstServices.Values)
                {
                    Assert.That(service.ShutdownCount, Is.EqualTo(1));
                }

                Assert.That(replacementSettings.InitializeCount, Is.EqualTo(1));
                Assert.That(replacement.LastStartupReport.IsSuccessful, Is.True);
                Assert.That(
                    replacementTransition.Destinations,
                    Is.EqualTo(new[] { "Frontend" }));
            }
            finally
            {
                cleanupRelease.TrySetResult(null);
            }
        }

        [UnityTest]
        public IEnumerator NonCooperativeInitializer_PoisonsBarrierUntilItSettles()
        {
            var initializeEntered = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var initializeRelease = new TaskCompletionSource<StartupResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var cancellationObserved = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var cleanupEntered = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var cleanupRelease = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var cleanupCompleted = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var cancellationRegistration = default(CancellationTokenRegistration);
            m_EmergencyReleases.Add(
                () => initializeRelease.TrySetResult(StartupResult.Available()));
            m_EmergencyReleases.Add(() => cleanupRelease.TrySetResult(null));

            var timeline = new List<string>();
            var pendingSettings = new RecordingGameService(
                "OldSettings",
                timeline,
                token =>
                {
                    cancellationRegistration = token.Register(
                        () => cancellationObserved.TrySetResult(null));
                    initializeEntered.TrySetResult(null);
                    return new ValueTask<StartupResult>(initializeRelease.Task);
                },
                () => new ValueTask(CompleteOldCleanupAsync()));
            var firstOverrides = new Dictionary<GameServiceRole, IGameService>
            {
                [GameServiceRole.Settings] = pendingSettings,
            };
            var firstTransition = new RecordingSceneTransition(timeline);
            GameBootstrap.CompositionFactory = () =>
                new GameBootstrapComposition(
                    BootstrapTestComposition.CompleteRequired(
                        timeline,
                        firstOverrides),
                    firstTransition);
            var first = new GameObject("NonCooperativeBarrierPrimary")
                .AddComponent<GameBootstrap>();
            yield return WaitForTask(
                initializeEntered.Task,
                "non-cooperative initializer entry");

            UnityEngine.Object.Destroy(first.gameObject);
            yield return WaitForCondition(
                () => first == null,
                "non-cooperative primary destruction");
            yield return WaitForTask(
                cancellationObserved.Task,
                "non-cooperative cancellation observation");

            var replacementSettings = RecordingGameService.Available(
                "ReplacementSettings",
                timeline);
            var replacementOverrides = new Dictionary<GameServiceRole, IGameService>
            {
                [GameServiceRole.Settings] = replacementSettings,
            };
            var replacementTransition = new RecordingSceneTransition(
                timeline);
            GameBootstrap.CompositionFactory = () =>
                new GameBootstrapComposition(
                    BootstrapTestComposition.CompleteRequired(
                        timeline,
                        replacementOverrides),
                    replacementTransition);
            var replacement = new GameObject("NonCooperativeBarrierReplacement")
                .AddComponent<GameBootstrap>();

            try
            {
                yield return null;
                yield return null;

                Assert.That(pendingSettings.ShutdownCount, Is.EqualTo(0));
                Assert.That(replacementSettings.InitializeCount, Is.EqualTo(0));
                Assert.That(firstTransition.Destinations, Is.Empty);
                Assert.That(replacementTransition.Destinations, Is.Empty);

                initializeRelease.TrySetResult(StartupResult.Available());
                yield return WaitForTask(
                    cleanupEntered.Task,
                    "old current-service cleanup entry");
                Assert.That(cleanupCompleted.Task.IsCompleted, Is.False);
                Assert.That(replacementSettings.InitializeCount, Is.EqualTo(0));

                cleanupRelease.TrySetResult(null);
                yield return WaitForTask(
                    cleanupCompleted.Task,
                    "old current-service cleanup completion");
                yield return WaitForReport(
                    replacement,
                    "replacement after non-cooperative settlement");

                Assert.That(pendingSettings.InitializeCount, Is.EqualTo(1));
                Assert.That(pendingSettings.ShutdownCount, Is.EqualTo(1));
                Assert.That(firstTransition.Destinations, Is.Empty);
                Assert.That(replacementSettings.InitializeCount, Is.EqualTo(1));
                Assert.That(replacement.LastStartupReport.IsSuccessful, Is.True);
                Assert.That(
                    replacementTransition.Destinations,
                    Is.EqualTo(new[] { "Frontend" }));
                Assert.That(
                    timeline.IndexOf("cleanup-completed:OldSettings"),
                    Is.LessThan(timeline.IndexOf("initialize:ReplacementSettings")));
            }
            finally
            {
                initializeRelease.TrySetResult(StartupResult.Available());
                cleanupRelease.TrySetResult(null);
                cancellationRegistration.Dispose();
            }

            async Task CompleteOldCleanupAsync()
            {
                cleanupEntered.TrySetResult(null);
                await cleanupRelease.Task;
                timeline.Add("cleanup-completed:OldSettings");
                cleanupCompleted.TrySetResult(null);
            }
        }

        [UnityTest]
        public IEnumerator DefaultComposition_ReportsAllMissingRolesWithoutRouting()
        {
            GameBootstrap.CompositionFactory = null;
            var bootstrap = new GameObject("DefaultCompositionBootstrap")
                .AddComponent<GameBootstrap>();

            yield return WaitForReport(bootstrap, "default composition startup");

            Assert.That(bootstrap.LastStartupReport.IsSuccessful, Is.False);
            Assert.That(bootstrap.LastStartupReport.IsCancelled, Is.False);
            Assert.That(bootstrap.LastStartupReport.RoutedToFrontend, Is.False);
            Assert.That(
                bootstrap.LastStartupReport.PrimaryFailure?.Message,
                Is.EqualTo(
                    "Missing required service roles: Settings, LocalSave, Input, " +
                    "ContentCatalogue, ModeController."));
        }

        [UnityTest]
        public IEnumerator DestroyRecreateChain_WaitsForEveryPredecessorShutdown()
        {
            var cleanupEntered = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var cleanupRelease = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            m_EmergencyReleases.Add(() => cleanupRelease.TrySetResult(null));

            var firstEvents = new List<string>();
            var firstModeController = RecordingGameService.Available(
                "ModeController",
                firstEvents,
                () =>
                {
                    cleanupEntered.TrySetResult(null);
                    return new ValueTask(cleanupRelease.Task);
                });
            var firstOverrides = new Dictionary<GameServiceRole, IGameService>
            {
                [GameServiceRole.ModeController] = firstModeController,
            };
            var firstComposition = new GameBootstrapComposition(
                BootstrapTestComposition.CompleteRequired(
                    firstEvents,
                    firstOverrides),
                new RecordingSceneTransition(firstEvents));
            GameBootstrap.CompositionFactory = () => firstComposition;
            var first = new GameObject("BarrierBootstrapA")
                .AddComponent<GameBootstrap>();
            yield return WaitForReport(first, "first bootstrap startup");

            UnityEngine.Object.Destroy(first.gameObject);
            yield return WaitForTask(cleanupEntered.Task, "first bootstrap cleanup entry");

            var secondEvents = new List<string>();
            var secondSettings = RecordingGameService.Available(
                "Settings",
                secondEvents);
            var secondOverrides = new Dictionary<GameServiceRole, IGameService>
            {
                [GameServiceRole.Settings] = secondSettings,
            };
            GameBootstrap.CompositionFactory = () =>
                new GameBootstrapComposition(
                    BootstrapTestComposition.CompleteRequired(
                        secondEvents,
                        secondOverrides),
                    new RecordingSceneTransition(secondEvents));
            var second = new GameObject("BarrierBootstrapB")
                .AddComponent<GameBootstrap>();
            yield return null;
            UnityEngine.Object.Destroy(second.gameObject);
            yield return WaitForCondition(
                () => second == null,
                "deferred destruction of second bootstrap");

            var thirdEvents = new List<string>();
            var thirdSettings = RecordingGameService.Available(
                "Settings",
                thirdEvents);
            var thirdOverrides = new Dictionary<GameServiceRole, IGameService>
            {
                [GameServiceRole.Settings] = thirdSettings,
            };
            GameBootstrap.CompositionFactory = () =>
                new GameBootstrapComposition(
                    BootstrapTestComposition.CompleteRequired(
                        thirdEvents,
                        thirdOverrides),
                    new RecordingSceneTransition(thirdEvents));
            var third = new GameObject("BarrierBootstrapC")
                .AddComponent<GameBootstrap>();

            try
            {
                yield return null;
                yield return null;

                Assert.That(
                    secondSettings.InitializeCount,
                    Is.EqualTo(0),
                    "A replacement destroyed behind its predecessor must never start.");
                Assert.That(
                    thirdSettings.InitializeCount,
                    Is.EqualTo(0),
                    "A chained replacement must wait for the entire predecessor barrier.");

                cleanupRelease.TrySetResult(null);
                yield return WaitForReport(third, "third bootstrap startup after cleanup");

                Assert.That(firstModeController.ShutdownCount, Is.EqualTo(1));
                Assert.That(secondSettings.InitializeCount, Is.EqualTo(0));
                Assert.That(thirdSettings.InitializeCount, Is.EqualTo(1));
                Assert.That(third.LastStartupReport.IsSuccessful, Is.True);
            }
            finally
            {
                cleanupRelease.TrySetResult(null);
            }
        }

        [UnityTest]
        public IEnumerator ReplacementManualStartup_WaitsForPredecessorCleanup()
        {
            var cleanupEntered = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var cleanupRelease = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            m_EmergencyReleases.Add(() => cleanupRelease.TrySetResult(null));

            var firstEvents = new List<string>();
            var firstModeController = RecordingGameService.Available(
                "ModeController",
                firstEvents,
                () =>
                {
                    cleanupEntered.TrySetResult(null);
                    return new ValueTask(cleanupRelease.Task);
                });
            var firstOverrides = new Dictionary<GameServiceRole, IGameService>
            {
                [GameServiceRole.ModeController] = firstModeController,
            };
            GameBootstrap.CompositionFactory = () =>
                new GameBootstrapComposition(
                    BootstrapTestComposition.CompleteRequired(
                        firstEvents,
                        firstOverrides),
                    new RecordingSceneTransition(firstEvents));
            var first = new GameObject("ManualBarrierBootstrapA")
                .AddComponent<GameBootstrap>();
            yield return WaitForReport(first, "manual-barrier predecessor startup");

            UnityEngine.Object.Destroy(first.gameObject);
            yield return WaitForTask(
                cleanupEntered.Task,
                "manual-barrier predecessor cleanup entry");

            var replacementEvents = new List<string>();
            var replacementServices = CreateNamedRequiredServices(replacementEvents);
            var replacementOverrides = replacementServices.ToDictionary(
                pair => pair.Key,
                pair => (IGameService)pair.Value);
            var replacementTransition = new RecordingSceneTransition(
                replacementEvents);
            var replacementComposition = new GameBootstrapComposition(
                BootstrapTestComposition.CompleteRequired(
                    replacementEvents,
                    replacementOverrides),
                replacementTransition);
            GameBootstrap.CompositionFactory = () => replacementComposition;
            var replacement = new GameObject("ManualBarrierBootstrapB")
                .AddComponent<GameBootstrap>();
            var manualStartup = replacement.StartupAsync(
                replacementComposition,
                default).AsTask();

            try
            {
                yield return null;
                yield return null;
                Assert.That(
                    new[]
                    {
                        manualStartup.IsCompleted.ToString(),
                        replacementServices[GameServiceRole.Settings]
                            .InitializeCount.ToString(),
                        replacementTransition.Destinations.Count.ToString(),
                    },
                    Is.EqualTo(new[] { "False", "0", "0" }));

                cleanupRelease.TrySetResult(null);
                yield return WaitForTask(
                    manualStartup,
                    "manual replacement startup after predecessor cleanup");
                yield return WaitForReport(
                    replacement,
                    "automatic replacement startup after predecessor cleanup");

                Assert.That(
                    replacement.LastStartupReport,
                    Is.SameAs(manualStartup.Result));
                Assert.That(manualStartup.Result.IsSuccessful, Is.True);
                Assert.That(
                    replacementTransition.Destinations,
                    Is.EqualTo(new[] { "Frontend" }));
                foreach (var service in replacementServices.Values)
                {
                    Assert.That(service.InitializeCount, Is.EqualTo(1));
                }

                var shutdown = replacement.ShutdownAsync().AsTask();
                yield return WaitForTask(
                    shutdown,
                    "manual replacement explicit shutdown");
                UnityEngine.Object.Destroy(replacement.gameObject);
                yield return WaitForCondition(
                    () => replacement == null,
                    "manual replacement destruction");
                foreach (var service in replacementServices.Values)
                {
                    Assert.That(service.ShutdownCount, Is.EqualTo(1));
                }
            }
            finally
            {
                cleanupRelease.TrySetResult(null);
            }
        }

        [UnityTest]
        public IEnumerator ShutdownWaitingReplacement_TerminatesOwnedStartWithoutRouting()
        {
            var cleanupEntered = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var cleanupRelease = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            m_EmergencyReleases.Add(() => cleanupRelease.TrySetResult(null));

            var firstEvents = new List<string>();
            var firstModeController = RecordingGameService.Available(
                "ModeController",
                firstEvents,
                () =>
                {
                    cleanupEntered.TrySetResult(null);
                    return new ValueTask(cleanupRelease.Task);
                });
            var firstOverrides = new Dictionary<GameServiceRole, IGameService>
            {
                [GameServiceRole.ModeController] = firstModeController,
            };
            GameBootstrap.CompositionFactory = () =>
                new GameBootstrapComposition(
                    BootstrapTestComposition.CompleteRequired(
                        firstEvents,
                        firstOverrides),
                    new RecordingSceneTransition(firstEvents));
            var first = new GameObject("ShutdownBarrierBootstrapA")
                .AddComponent<GameBootstrap>();
            yield return WaitForReport(first, "shutdown-barrier predecessor startup");

            UnityEngine.Object.Destroy(first.gameObject);
            yield return WaitForTask(
                cleanupEntered.Task,
                "shutdown-barrier predecessor cleanup entry");

            var replacementEvents = new List<string>();
            var replacementServices = CreateNamedRequiredServices(replacementEvents);
            var replacementOverrides = replacementServices.ToDictionary(
                pair => pair.Key,
                pair => (IGameService)pair.Value);
            var replacementTransition = new RecordingSceneTransition(
                replacementEvents);
            GameBootstrap.CompositionFactory = () =>
                new GameBootstrapComposition(
                    BootstrapTestComposition.CompleteRequired(
                        replacementEvents,
                        replacementOverrides),
                    replacementTransition);
            var replacement = new GameObject("ShutdownBarrierBootstrapB")
                .AddComponent<GameBootstrap>();
            Task ownedStart = null;
            yield return WaitForCondition(
                () => (ownedStart = GetOwnedStartTask(replacement)) != null,
                "waiting replacement owned Start task publication");

            var firstShutdown = replacement.ShutdownAsync().AsTask();
            var secondShutdown = replacement.ShutdownAsync().AsTask();
            yield return WaitForTask(firstShutdown, "first waiting replacement shutdown");
            yield return WaitForTask(secondShutdown, "second waiting replacement shutdown");

            try
            {
                cleanupRelease.TrySetResult(null);
                yield return WaitForTask(
                    ownedStart,
                    "waiting replacement owned Start termination");

                Assert.That(firstShutdown, Is.SameAs(secondShutdown));
                Assert.That(replacement.LastStartupReport, Is.Null);
                Assert.That(replacementTransition.Destinations, Is.Empty);
                Assert.That(replacementEvents, Is.Empty);
                foreach (var service in replacementServices.Values)
                {
                    Assert.That(service.InitializeCount, Is.EqualTo(0));
                    Assert.That(service.ShutdownCount, Is.EqualTo(0));
                }

                LogAssert.NoUnexpectedReceived();
                UnityEngine.Object.Destroy(replacement.gameObject);
                yield return WaitForCondition(
                    () => replacement == null,
                    "shutdown waiting replacement destruction");
            }
            finally
            {
                cleanupRelease.TrySetResult(null);
            }
        }

        [UnityTest]
        public IEnumerator DestroyedFormerOwner_RetainedReferenceCannotRestartServices()
        {
            var cleanupEntered = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var cleanupRelease = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            m_EmergencyReleases.Add(() => cleanupRelease.TrySetResult(null));

            var predecessorEvents = new List<string>();
            var predecessorMode = RecordingGameService.Available(
                "ModeController",
                predecessorEvents,
                () =>
                {
                    cleanupEntered.TrySetResult(null);
                    return new ValueTask(cleanupRelease.Task);
                });
            var predecessorOverrides = new Dictionary<GameServiceRole, IGameService>
            {
                [GameServiceRole.ModeController] = predecessorMode,
            };
            GameBootstrap.CompositionFactory = () =>
                new GameBootstrapComposition(
                    BootstrapTestComposition.CompleteRequired(
                        predecessorEvents,
                        predecessorOverrides),
                    new RecordingSceneTransition(predecessorEvents));
            var predecessor = new GameObject("RetainedOwnerPredecessor")
                .AddComponent<GameBootstrap>();
            yield return WaitForReport(
                predecessor,
                "retained-owner predecessor startup");

            UnityEngine.Object.Destroy(predecessor.gameObject);
            yield return WaitForTask(
                cleanupEntered.Task,
                "retained-owner predecessor cleanup entry");

            var waitingEvents = new List<string>();
            GameBootstrap.CompositionFactory = () =>
                new GameBootstrapComposition(
                    BootstrapTestComposition.CompleteRequired(waitingEvents),
                    new RecordingSceneTransition(waitingEvents));
            var waitingOwner = new GameObject("RetainedDestroyedOwner")
                .AddComponent<GameBootstrap>();
            yield return WaitForCondition(
                () => GetOwnedStartTask(waitingOwner) != null,
                "retained owner waiting Start task publication");

            var retainedOwner = waitingOwner;
            UnityEngine.Object.Destroy(waitingOwner.gameObject);
            yield return WaitForCondition(
                () => waitingOwner == null,
                "retained owner deferred destruction");
            Assert.That(ReferenceEquals(retainedOwner, null), Is.False);
            Assert.That(retainedOwner == null, Is.True);

            var rejectedEvents = new List<string>();
            var rejectedFailure = new InvalidOperationException(
                "a destroyed owner must never initialize this service");
            var rejectedSettings = new RecordingGameService(
                "RejectedSettings",
                rejectedEvents,
                _ => throw rejectedFailure);
            var rejectedOverrides = new Dictionary<GameServiceRole, IGameService>
            {
                [GameServiceRole.Settings] = rejectedSettings,
            };
            var rejectedTransition = new RecordingSceneTransition(rejectedEvents);
            var rejectedComposition = new GameBootstrapComposition(
                BootstrapTestComposition.CompleteRequired(
                    rejectedEvents,
                    rejectedOverrides),
                rejectedTransition);
            Exception rejection = null;
            Task<StartupReport> unexpectedStartup = null;

            try
            {
                unexpectedStartup = retainedOwner.StartupAsync(
                    rejectedComposition,
                    default).AsTask();
            }
            catch (Exception exception)
            {
                rejection = exception;
            }

            cleanupRelease.TrySetResult(null);
            if (unexpectedStartup != null)
            {
                yield return WaitForTask(
                    unexpectedStartup,
                    "retained destroyed owner rejected startup");
                if (unexpectedStartup.IsFaulted)
                {
                    rejection = unexpectedStartup.Exception?.GetBaseException();
                }
                else if (unexpectedStartup.IsCanceled)
                {
                    rejection = new TaskCanceledException(unexpectedStartup);
                }
            }

            Assert.That(rejection, Is.InstanceOf<InvalidOperationException>());
            StringAssert.Contains("current primary", rejection?.Message);
            Assert.That(rejectedSettings.InitializeCount, Is.EqualTo(0));
            Assert.That(rejectedSettings.ShutdownCount, Is.EqualTo(0));
            Assert.That(rejectedTransition.Destinations, Is.Empty);
            Assert.That(rejectedEvents, Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator StaticResetDuringCleanup_PreservesUnfinishedPredecessorBarrier()
        {
            var cleanupEntered = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var cleanupRelease = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            m_EmergencyReleases.Add(() => cleanupRelease.TrySetResult(null));

            var firstEvents = new List<string>();
            var firstModeController = RecordingGameService.Available(
                "ModeController",
                firstEvents,
                () =>
                {
                    cleanupEntered.TrySetResult(null);
                    return new ValueTask(cleanupRelease.Task);
                });
            var firstOverrides = new Dictionary<GameServiceRole, IGameService>
            {
                [GameServiceRole.ModeController] = firstModeController,
            };
            GameBootstrap.CompositionFactory = () =>
                new GameBootstrapComposition(
                    BootstrapTestComposition.CompleteRequired(
                        firstEvents,
                        firstOverrides),
                    new RecordingSceneTransition(firstEvents));
            var first = new GameObject("StaticResetBootstrapA")
                .AddComponent<GameBootstrap>();
            yield return WaitForReport(first, "pre-reset bootstrap startup");

            UnityEngine.Object.Destroy(first.gameObject);
            yield return WaitForTask(cleanupEntered.Task, "pre-reset cleanup entry");

            InvokeSubsystemRegistrationReset();

            var replacementEvents = new List<string>();
            var replacementSettings = RecordingGameService.Available(
                "Settings",
                replacementEvents);
            var replacementOverrides = new Dictionary<GameServiceRole, IGameService>
            {
                [GameServiceRole.Settings] = replacementSettings,
            };
            GameBootstrap.CompositionFactory = () =>
                new GameBootstrapComposition(
                    BootstrapTestComposition.CompleteRequired(
                        replacementEvents,
                        replacementOverrides),
                    new RecordingSceneTransition(replacementEvents));
            var replacement = new GameObject("StaticResetBootstrapB")
                .AddComponent<GameBootstrap>();

            try
            {
                yield return null;
                yield return null;
                Assert.That(
                    replacementSettings.InitializeCount,
                    Is.EqualTo(0),
                    "Subsystem reset must retain an unfinished predecessor shutdown.");

                cleanupRelease.TrySetResult(null);
                yield return WaitForReport(
                    replacement,
                    "post-reset replacement startup");

                Assert.That(firstModeController.ShutdownCount, Is.EqualTo(1));
                Assert.That(replacementSettings.InitializeCount, Is.EqualTo(1));
                Assert.That(replacement.LastStartupReport.IsSuccessful, Is.True);
            }
            finally
            {
                cleanupRelease.TrySetResult(null);
            }
        }

        private static Dictionary<GameServiceRole, RecordingGameService>
            CreateNamedRequiredServices(ICollection<string> events)
        {
            var services = new Dictionary<GameServiceRole, RecordingGameService>();
            foreach (var role in BootstrapTestComposition.RequiredRoles)
            {
                services.Add(
                    role,
                    RecordingGameService.Available(role.ToString(), events));
            }

            return services;
        }

        private static void InvokeSubsystemRegistrationReset()
        {
            var reset = typeof(GameBootstrap)
                .GetMethods(
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Static)
                .SingleOrDefault(method => method
                    .GetCustomAttributes(
                        typeof(RuntimeInitializeOnLoadMethodAttribute),
                        inherit: false)
                    .Cast<RuntimeInitializeOnLoadMethodAttribute>()
                    .Any(attribute =>
                        attribute.loadType ==
                        RuntimeInitializeLoadType.SubsystemRegistration));
            Assert.That(reset, Is.Not.Null);
            reset.Invoke(null, null);
        }

        private static Task GetOwnedStartTask(GameBootstrap bootstrap)
        {
            return (Task)typeof(GameBootstrap)
                .GetField(
                    "m_StartTask",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(bootstrap);
        }

        private static IEnumerator WaitForReport(
            GameBootstrap bootstrap,
            string operation)
        {
            return WaitForCondition(
                () => bootstrap != null && bootstrap.LastStartupReport != null,
                operation);
        }

        private static IEnumerator WaitForTask(Task task, string operation)
        {
            return WaitForCondition(() => task.IsCompleted, operation);
        }

        private static IEnumerator WaitForCondition(
            Func<bool> predicate,
            string operation)
        {
            const int maximumFrames = 120;
            for (var frame = 0; frame < maximumFrames; frame++)
            {
                if (predicate())
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail($"{operation} did not complete within {maximumFrames} frames.");
        }

        private static void DestroyAllBootstraps()
        {
            foreach (var bootstrap in UnityEngine.Object.FindObjectsByType<GameBootstrap>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (bootstrap != null)
                {
                    UnityEngine.Object.Destroy(bootstrap.gameObject);
                }
            }
        }
    }
}
