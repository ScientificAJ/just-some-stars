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
            GameBootstrap.CompositionFactory = null;
            foreach (var bootstrap in UnityEngine.Object.FindObjectsByType<GameBootstrap>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                UnityEngine.Object.Destroy(bootstrap.gameObject);
            }

            yield return null;
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

            var load = SceneManager.LoadSceneAsync("Boot", LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return WaitForBootstrapStartup();

            var bootstraps = UnityEngine.Object.FindObjectsByType<GameBootstrap>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(bootstraps, Has.Length.EqualTo(1));
            Assert.That(bootstraps[0].LastStartupReport, Is.Not.Null);
            Assert.That(bootstraps[0].LastStartupReport.RoutedToFrontend, Is.True);
            Assert.That(transition.Destinations, Is.EqualTo(new[] { "Frontend" }));

            var bootstrap = bootstraps[0];
            var nextScene = SceneManager.CreateScene("Task4AfterBoot");
            Assert.That(SceneManager.SetActiveScene(nextScene), Is.True);
            var bootScene = SceneManager.GetSceneByName("Boot");
            var unload = SceneManager.UnloadSceneAsync(bootScene);
            Assert.That(unload, Is.Not.Null);
            yield return unload;

            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));
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
            const int maximumFrames = 120;
            for (var frame = 0; frame < maximumFrames; frame++)
            {
                var bootstrap = UnityEngine.Object.FindFirstObjectByType<GameBootstrap>(
                    FindObjectsInactive.Include);
                if (bootstrap != null && bootstrap.LastStartupReport != null)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("GameBootstrap did not finish startup within 120 frames.");
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
