using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class BootSceneTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return ShutdownAndDestroyAllBootstraps();
        }

        [Test]
        public async Task Startup_InitializesRequiredThenOptionalServicesInFixedOrder()
        {
            var events = new List<string>();
            var registrations = CreateRegistrations(
                events,
                GameServiceRole.Growth,
                GameServiceRole.Input,
                GameServiceRole.Cloud,
                GameServiceRole.Settings,
                GameServiceRole.ModeController,
                GameServiceRole.Notifications,
                GameServiceRole.LocalSave,
                GameServiceRole.Attribution,
                GameServiceRole.ContentCatalogue,
                GameServiceRole.Commerce);
            var transition = new RecordingSceneTransition(events);
            var coordinator = new ServiceStartupCoordinator();

            try
            {
                var report = await coordinator.StartupAsync(
                    new GameBootstrapComposition(registrations, transition),
                    CancellationToken.None);

                Assert.That(events, Is.EqualTo(new[]
                {
                    "initialize:Settings",
                    "initialize:LocalSave",
                    "initialize:Input",
                    "initialize:ContentCatalogue",
                    "initialize:ModeController",
                    "initialize:Cloud",
                    "initialize:Commerce",
                    "initialize:Notifications",
                    "initialize:Attribution",
                    "initialize:Growth",
                    "route:Frontend",
                }));
                Assert.That(report.IsSuccessful, Is.True);
                Assert.That(report.RoutedToFrontend, Is.True);
                Assert.That(report.RequestedDestination, Is.EqualTo("Frontend"));
            }
            finally
            {
                await coordinator.ShutdownAsync();
            }
        }

        [Test]
        public async Task OptionalFailure_IsUnavailableAndDoesNotBlockFrontend()
        {
            var events = new List<string>();
            var optionalFailure = new InvalidOperationException("cloud is offline");
            var settings = RecordingService.Available("Settings", events);
            var cloud = new RecordingService(
                "Cloud",
                events,
                _ => throw optionalFailure);
            var growth = RecordingService.Available("Growth", events);
            var transition = new RecordingSceneTransition(events);
            var coordinator = new ServiceStartupCoordinator();

            try
            {
                var report = await coordinator.StartupAsync(
                    new GameBootstrapComposition(
                        new[]
                        {
                            new GameServiceRegistration(GameServiceRole.Growth, growth),
                            new GameServiceRegistration(GameServiceRole.Settings, settings),
                            new GameServiceRegistration(
                                GameServiceRole.LocalSave,
                                RecordingService.Available("LocalSave", events)),
                            new GameServiceRegistration(
                                GameServiceRole.Input,
                                RecordingService.Available("Input", events)),
                            new GameServiceRegistration(
                                GameServiceRole.ContentCatalogue,
                                RecordingService.Available("ContentCatalogue", events)),
                            new GameServiceRegistration(
                                GameServiceRole.ModeController,
                                RecordingService.Available("ModeController", events)),
                            new GameServiceRegistration(GameServiceRole.Cloud, cloud),
                        },
                        transition),
                    CancellationToken.None);

                var cloudResult = report.Services.Single(
                    result => result.Role == GameServiceRole.Cloud);
                Assert.That(cloudResult.Requirement, Is.EqualTo(ServiceRequirement.Optional));
                Assert.That(cloudResult.State, Is.EqualTo(ServiceStartupState.Unavailable));
                Assert.That(cloudResult.Failure, Is.SameAs(optionalFailure));
                Assert.That(report.PrimaryFailure, Is.Null);
                Assert.That(report.IsSuccessful, Is.True);
                Assert.That(events, Is.EqualTo(new[]
                {
                    "initialize:Settings",
                    "initialize:LocalSave",
                    "initialize:Input",
                    "initialize:ContentCatalogue",
                    "initialize:ModeController",
                    "initialize:Cloud",
                    "shutdown:Cloud",
                    "initialize:Growth",
                    "route:Frontend",
                }));
                Assert.That(transition.Destinations, Is.EqualTo(new[] { "Frontend" }));
            }
            finally
            {
                await coordinator.ShutdownAsync();
            }
        }

        [Test]
        public async Task RequiredFailure_StopsStartupAndPreservesCleanupFailure()
        {
            var events = new List<string>();
            var primaryFailure = new InvalidOperationException("local save failed");
            var cleanupFailure = new InvalidOperationException("settings cleanup failed");
            var settings = new RecordingService(
                "Settings",
                events,
                _ => new ValueTask<StartupResult>(StartupResult.Available()),
                () => throw cleanupFailure);
            var localSave = new RecordingService(
                "LocalSave",
                events,
                _ => new ValueTask<StartupResult>(
                    Task.FromException<StartupResult>(primaryFailure)));
            var input = RecordingService.Available("Input", events);
            var transition = new RecordingSceneTransition(events);
            var coordinator = new ServiceStartupCoordinator();

            try
            {
                var report = await coordinator.StartupAsync(
                    new GameBootstrapComposition(
                        new[]
                        {
                            new GameServiceRegistration(GameServiceRole.Input, input),
                            new GameServiceRegistration(GameServiceRole.LocalSave, localSave),
                            new GameServiceRegistration(GameServiceRole.Settings, settings),
                            new GameServiceRegistration(
                                GameServiceRole.ContentCatalogue,
                                RecordingService.Available("ContentCatalogue", events)),
                            new GameServiceRegistration(
                                GameServiceRole.ModeController,
                                RecordingService.Available("ModeController", events)),
                        },
                        transition),
                    CancellationToken.None);

                Assert.That(report.IsSuccessful, Is.False);
                Assert.That(report.RoutedToFrontend, Is.False);
                Assert.That(report.PrimaryFailure, Is.SameAs(primaryFailure));
                Assert.That(report.CleanupFailures, Is.EqualTo(new[] { cleanupFailure }));
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
                await coordinator.ShutdownAsync();
            }
        }

        [Test]
        public async Task RequiredCancellation_StopsRoutingAndShutsDownInitializedServices()
        {
            var events = new List<string>();
            using var cancellation = new CancellationTokenSource();
            var settings = RecordingService.Available("Settings", events);
            var localSave = new RecordingService(
                "LocalSave",
                events,
                token =>
                {
                    cancellation.Cancel();
                    return new ValueTask<StartupResult>(
                        Task.FromCanceled<StartupResult>(token));
                });
            var transition = new RecordingSceneTransition(events);
            var coordinator = new ServiceStartupCoordinator();

            try
            {
                var report = await coordinator.StartupAsync(
                    new GameBootstrapComposition(
                        new[]
                        {
                            new GameServiceRegistration(GameServiceRole.Settings, settings),
                            new GameServiceRegistration(GameServiceRole.LocalSave, localSave),
                            new GameServiceRegistration(
                                GameServiceRole.Input,
                                RecordingService.Available("Input", events)),
                            new GameServiceRegistration(
                                GameServiceRole.ContentCatalogue,
                                RecordingService.Available("ContentCatalogue", events)),
                            new GameServiceRegistration(
                                GameServiceRole.ModeController,
                                RecordingService.Available("ModeController", events)),
                        },
                        transition),
                    cancellation.Token);

                Assert.That(report.IsCancelled, Is.True);
                Assert.That(report.RoutedToFrontend, Is.False);
                Assert.That(report.PrimaryFailure, Is.InstanceOf<OperationCanceledException>());
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
                await coordinator.ShutdownAsync();
            }
        }

        [Test]
        public async Task StartupCalledTwice_SharesOneLifecycleAndRoutesOnce()
        {
            var events = new List<string>();
            var release = new TaskCompletionSource<StartupResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var settings = new RecordingService(
                "Settings",
                events,
                _ => new ValueTask<StartupResult>(release.Task));
            var transition = new RecordingSceneTransition(events);
            var composition = new GameBootstrapComposition(
                new[]
                    {
                        new GameServiceRegistration(GameServiceRole.Settings, settings),
                    }
                    .Concat(CreateRegistrations(
                        events,
                        GameServiceRole.LocalSave,
                        GameServiceRole.Input,
                        GameServiceRole.ContentCatalogue,
                        GameServiceRole.ModeController)),
                transition);
            var coordinator = new ServiceStartupCoordinator();

            try
            {
                var first = coordinator.StartupAsync(
                    composition,
                    CancellationToken.None).AsTask();
                var second = coordinator.StartupAsync(
                    composition,
                    CancellationToken.None).AsTask();
                release.SetResult(StartupResult.Available());

                var reports = await Task.WhenAll(first, second);

                Assert.That(reports[1], Is.SameAs(reports[0]));
                Assert.That(events.Count(entry => entry == "initialize:Settings"), Is.EqualTo(1));
                Assert.That(transition.Destinations, Is.EqualTo(new[] { "Frontend" }));
            }
            finally
            {
                await coordinator.ShutdownAsync();
            }
        }

        [Test]
        public async Task Shutdown_UsesReverseSuccessfulInitializationOrderAndIsIdempotent()
        {
            var events = new List<string>();
            var transition = new RecordingSceneTransition(events);
            var coordinator = new ServiceStartupCoordinator();
            var composition = new GameBootstrapComposition(
                CreateRegistrations(
                    events,
                    GameServiceRole.Settings,
                    GameServiceRole.LocalSave,
                    GameServiceRole.Input,
                    GameServiceRole.ContentCatalogue,
                    GameServiceRole.ModeController,
                    GameServiceRole.Cloud),
                transition);

            try
            {
                await coordinator.StartupAsync(composition, CancellationToken.None);
                events.Clear();

                await coordinator.ShutdownAsync();
                await coordinator.ShutdownAsync();

                Assert.That(events, Is.EqualTo(new[]
                {
                    "shutdown:Cloud",
                    "shutdown:ModeController",
                    "shutdown:ContentCatalogue",
                    "shutdown:Input",
                    "shutdown:LocalSave",
                    "shutdown:Settings",
                }));
            }
            finally
            {
                await coordinator.ShutdownAsync();
            }
        }

        [UnityTest]
        public IEnumerator BootScene_RoutesExactlyOnceAndSurvivesSceneChanges()
        {
            yield return ShutdownAndDestroyAllBootstraps();
            ApplicationBootstrapInstaller.Install();
            Assert.That(GameBootstrap.CompositionFactory, Is.Not.Null);

            var load = SceneManager.LoadSceneAsync("Boot", LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return WaitForFrontendStartup();

            var bootstraps = UnityEngine.Object.FindObjectsByType<GameBootstrap>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(bootstraps, Has.Length.EqualTo(1));
            var bootstrap = bootstraps[0];
            Assert.That(bootstrap.LastStartupReport, Is.Not.Null);
            Assert.That(bootstrap.LastStartupReport.IsSuccessful, Is.True);
            Assert.That(bootstrap.LastStartupReport.RoutedToFrontend, Is.True);
            Assert.That(
                bootstrap.LastStartupReport.RequestedDestination,
                Is.EqualTo("Frontend"));
            Assert.That(
                SceneManager.GetActiveScene().name,
                Is.EqualTo("Frontend"));
            Assert.That(bootstrap != null, Is.True);
            Assert.That(bootstrap.gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));
            Assert.That(
                UnityEngine.Object.FindObjectsByType<GameBootstrap>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None),
                Has.Length.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DuplicateBootstrap_IsDestroyedBeforeItCanRoute()
        {
            var events = new List<string>();
            var transition = new RecordingSceneTransition(events);
            GameBootstrap.CompositionFactory = () =>
                new GameBootstrapComposition(
                    CreateRegistrations(
                        events,
                        GameServiceRole.Settings,
                        GameServiceRole.LocalSave,
                        GameServiceRole.Input,
                        GameServiceRole.ContentCatalogue,
                        GameServiceRole.ModeController),
                    transition);

            new GameObject("PrimaryBootstrap").AddComponent<GameBootstrap>();
            new GameObject("DuplicateBootstrap").AddComponent<GameBootstrap>();

            yield return null;
            yield return WaitForBootstrapStartup();

            Assert.That(
                UnityEngine.Object.FindObjectsByType<GameBootstrap>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None),
                Has.Length.EqualTo(1));
            Assert.That(transition.Destinations, Is.EqualTo(new[] { "Frontend" }));
        }

        private static IEnumerator ShutdownAndDestroyAllBootstraps()
        {
            GameBootstrap.CompositionFactory = null;
            var bootstraps = UnityEngine.Object.FindObjectsByType<GameBootstrap>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            var shutdownTasks = bootstraps
                .Where(bootstrap => bootstrap != null)
                .Select(bootstrap => bootstrap.ShutdownAsync().AsTask())
                .Distinct()
                .ToArray();
            foreach (var shutdownTask in shutdownTasks)
            {
                yield return WaitForTask(
                    shutdownTask,
                    "BootSceneTests bootstrap shutdown");
            }

            foreach (var bootstrap in bootstraps)
            {
                if (bootstrap != null)
                {
                    UnityEngine.Object.Destroy(bootstrap.gameObject);
                }
            }

            const float cleanupTimeoutSeconds = 10f;
            var cleanupDeadline =
                Time.realtimeSinceStartup + cleanupTimeoutSeconds;
            while (Time.realtimeSinceStartup < cleanupDeadline)
            {
                var remaining = UnityEngine.Object.FindObjectsByType<GameBootstrap>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                if (remaining.Length == 0)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.That(
                UnityEngine.Object.FindObjectsByType<GameBootstrap>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None),
                Is.Empty,
                "BootSceneTests must start and finish without a retained bootstrap.");
        }

        private static IEnumerator WaitForTask(Task task, string operation)
        {
            const float timeoutSeconds = 10f;
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(
                task.IsCompleted,
                Is.True,
                $"{operation} did not complete within {timeoutSeconds} seconds.");
            task.GetAwaiter().GetResult();
        }

        private static IReadOnlyList<GameServiceRegistration> CreateRegistrations(
            ICollection<string> events,
            params GameServiceRole[] roles)
        {
            return roles
                .Select(role => new GameServiceRegistration(
                    role,
                    RecordingService.Available(role.ToString(), events)))
                .ToArray();
        }

        private static IEnumerator WaitForBootstrapStartup()
        {
            const float timeoutSeconds = 10f;
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                var bootstrap = UnityEngine.Object.FindFirstObjectByType<GameBootstrap>(
                    FindObjectsInactive.Include);
                if (bootstrap != null && bootstrap.LastStartupReport != null)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail(
                $"GameBootstrap did not finish startup within {timeoutSeconds} seconds.");
        }

        private static IEnumerator WaitForFrontendStartup()
        {
            const float timeoutSeconds = 10f;
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                var bootstraps = UnityEngine.Object.FindObjectsByType<GameBootstrap>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                if (SceneManager.GetActiveScene().name == "Frontend" &&
                    bootstraps.Length == 1 &&
                    bootstraps[0] != null &&
                    bootstraps[0].LastStartupReport != null)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail(
                $"Boot-to-Frontend startup did not complete within " +
                $"{timeoutSeconds} seconds.");
        }

        private sealed class RecordingService : IGameService
        {
            private readonly string m_Name;
            private readonly ICollection<string> m_Events;
            private readonly Func<CancellationToken, ValueTask<StartupResult>> m_Initialize;
            private readonly Func<ValueTask> m_Shutdown;

            public RecordingService(
                string name,
                ICollection<string> events,
                Func<CancellationToken, ValueTask<StartupResult>> initialize,
                Func<ValueTask> shutdown = null)
            {
                m_Name = name;
                m_Events = events;
                m_Initialize = initialize;
                m_Shutdown = shutdown ?? (() => default);
            }

            public static RecordingService Available(
                string name,
                ICollection<string> events)
            {
                return new RecordingService(
                    name,
                    events,
                    _ => new ValueTask<StartupResult>(StartupResult.Available()));
            }

            public ValueTask<StartupResult> InitializeAsync(
                CancellationToken cancellationToken)
            {
                m_Events.Add($"initialize:{m_Name}");
                return m_Initialize(cancellationToken);
            }

            public ValueTask ShutdownAsync()
            {
                m_Events.Add($"shutdown:{m_Name}");
                return m_Shutdown();
            }
        }

        private sealed class RecordingSceneTransition : ISceneTransition
        {
            private readonly ICollection<string> m_Events;
            private readonly List<string> m_Destinations = new List<string>();

            public RecordingSceneTransition(ICollection<string> events)
            {
                m_Events = events;
            }

            public IReadOnlyList<string> Destinations => m_Destinations;

            public ValueTask RouteAsync(
                string destination,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                m_Destinations.Add(destination);
                m_Events.Add($"route:{destination}");
                return default;
            }
        }
    }
}
