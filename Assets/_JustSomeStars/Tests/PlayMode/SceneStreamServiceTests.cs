using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class SceneStreamServiceTests
    {
        private readonly List<SceneStreamService> m_Services =
            new List<SceneStreamService>();
        private readonly List<GameModeController> m_Controllers =
            new List<GameModeController>();

        [TearDown]
        public async Task TearDown()
        {
            for (var index = m_Services.Count - 1; index >= 0; index--)
            {
                await m_Services[index].ShutdownAsync();
            }

            for (var index = m_Controllers.Count - 1; index >= 0; index--)
            {
                await m_Controllers[index].ShutdownAsync();
            }

            m_Services.Clear();
            m_Controllers.Clear();
        }

        [Test]
        public async Task LoadAndUnload_ActivatesAdditivelyAndRestoresPriorScene()
        {
            var markerType = Type.GetType(
                "JustSomeStars.Runtime.Core.PerformanceMarkers, JustSomeStars.Runtime");
            Assert.That(markerType, Is.Not.Null);
            markerType.GetMethod("ResetForTests")?.Invoke(null, null);
            var baselineCount = SceneManager.sceneCount;
            var baselineActive = SceneManager.GetActiveScene();
            var fixture = await CreateFixture(GameMode.Flight, autoComplete: true);
            var progress = new List<SceneStreamProgress>();
            fixture.Service.TransitionProgressed += progress.Add;

            var loaded = await fixture.Service.LoadDestinationAsync(
                "mirra",
                CancellationToken.None);

            Assert.That(loaded.Status, Is.EqualTo(SceneStreamStatus.Loaded));
            Assert.That(fixture.Controller.CurrentMode, Is.EqualTo(GameMode.Surface));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(baselineCount + 1));
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("JssTask8_mirra"));
            Assert.That(fixture.Backend.LastHandle.ActivateCount, Is.EqualTo(1));
            Assert.That(fixture.Backend.LastHandle.ReleaseCount, Is.EqualTo(0));
            Assert.That(progress, Is.Not.Empty);
            Assert.That(
                progress.Select(item => item.NormalizedProgress),
                Is.Ordered.Ascending);
            Assert.That(progress.Last().Stage, Is.EqualTo(SceneStreamStage.Completed));
            Assert.That(progress.Last().NormalizedProgress, Is.EqualTo(1f));

            var unloaded = await fixture.Service.UnloadDestinationAsync(
                "mirra",
                CancellationToken.None);

            Assert.That(unloaded.Status, Is.EqualTo(SceneStreamStatus.Unloaded));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(baselineCount));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(baselineActive));
            Assert.That(fixture.Backend.LastHandle.UnloadCount, Is.EqualTo(1));
            Assert.That(fixture.Backend.LastHandle.ReleaseCount, Is.EqualTo(1));
            var streamingSamples = markerType.GetProperty("StreamingSamples");
            Assert.That(streamingSamples, Is.Not.Null);
            Assert.That(Convert.ToInt64(streamingSamples.GetValue(null)),
                Is.GreaterThan(0L));
        }

        [Test]
        public async Task DuplicateOperations_AreIdempotentWithoutDuplicateHandles()
        {
            var fixture = await CreateFixture(GameMode.Flight, autoComplete: true);

            var first = await fixture.Service.LoadDestinationAsync(
                "mirra",
                CancellationToken.None);
            var duplicate = await fixture.Service.LoadDestinationAsync(
                "mirra",
                CancellationToken.None);
            var firstUnload = await fixture.Service.UnloadDestinationAsync(
                "mirra",
                CancellationToken.None);
            var duplicateUnload = await fixture.Service.UnloadDestinationAsync(
                "mirra",
                CancellationToken.None);

            Assert.That(first.Status, Is.EqualTo(SceneStreamStatus.Loaded));
            Assert.That(duplicate.Status, Is.EqualTo(SceneStreamStatus.AlreadyLoaded));
            Assert.That(firstUnload.Status, Is.EqualTo(SceneStreamStatus.Unloaded));
            Assert.That(duplicateUnload.Status, Is.EqualTo(SceneStreamStatus.NothingLoaded));
            Assert.That(fixture.Backend.BeginCount, Is.EqualTo(1));
            Assert.That(fixture.Backend.LastHandle.UnloadCount, Is.EqualTo(1));
            Assert.That(fixture.Backend.LastHandle.ReleaseCount, Is.EqualTo(1));
        }

        [Test]
        public async Task CancellationAfterIssue_AwaitsSettlementCleansExactlyOnceAndDoesNotFallback()
        {
            var baselineCount = SceneManager.sceneCount;
            var fixture = await CreateFixture(GameMode.Flight, autoComplete: false);
            using var cancellation = new CancellationTokenSource();
            var load = fixture.Service.LoadDestinationAsync(
                "mirra",
                cancellation.Token).AsTask();
            await fixture.Backend.WaitForBeginAsync();

            cancellation.Cancel();
            Assert.That(load.IsCompleted, Is.False,
                "Issued Unity work must settle before cancellation completes.");
            fixture.Backend.LastHandle.CompleteLoad();

            await AssertOperationCancelledAsync(load);
            Assert.That(SceneManager.sceneCount, Is.EqualTo(baselineCount));
            Assert.That(fixture.Backend.LastHandle.UnloadCount, Is.EqualTo(1));
            Assert.That(fixture.Backend.LastHandle.ReleaseCount, Is.EqualTo(1));
            Assert.That(fixture.Fallback.Destinations, Is.Empty);
            Assert.That(fixture.Diagnostics, Is.Empty);
            Assert.That(fixture.Controller.CurrentMode, Is.EqualTo(GameMode.Flight));
        }

        [TestCase(FakeFailurePoint.Load)]
        [TestCase(FakeFailurePoint.Activate)]
        public async Task LoadOrActivationFailure_CleansThenRoutesSafeFallbackOnce(
            FakeFailurePoint failurePoint)
        {
            var baselineCount = SceneManager.sceneCount;
            var fixture = await CreateFixture(GameMode.Flight, autoComplete: true);
            fixture.Backend.FailurePoint = failurePoint;

            var result = await fixture.Service.LoadDestinationAsync(
                "mirra",
                CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(SceneStreamStatus.Failed));
            Assert.That(result.Diagnostic, Is.Not.Null.And.Not.Empty);
            Assert.That(SceneManager.sceneCount, Is.EqualTo(baselineCount));
            Assert.That(fixture.Backend.LastHandle.ReleaseCount, Is.EqualTo(1));
            Assert.That(fixture.Backend.LastHandle.UnloadCount,
                Is.EqualTo(failurePoint == FakeFailurePoint.Load ? 0 : 1));
            Assert.That(fixture.Fallback.Destinations, Is.EqualTo(new[] { "Frontend" }));
            Assert.That(fixture.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(fixture.Diagnostics[0].DestinationId, Is.EqualTo("mirra"));
            Assert.That(fixture.Controller.CurrentMode, Is.EqualTo(GameMode.Frontend));
        }

        [Test]
        public async Task OverlayBlockedModeCommit_StillClearsOverlayAndRecoversFallback()
        {
            var fixture = await CreateFixture(GameMode.Flight, autoComplete: true);
            await fixture.Controller.OpenOverlayAsync(
                GameOverlay.Settings,
                CancellationToken.None);

            var result = await fixture.Service.LoadDestinationAsync(
                "mirra",
                CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(SceneStreamStatus.Failed));
            Assert.That(fixture.Fallback.Destinations, Is.EqualTo(new[] { "Frontend" }));
            Assert.That(fixture.Controller.CurrentMode, Is.EqualTo(GameMode.Frontend));
            Assert.That(fixture.Controller.ActiveOverlay, Is.EqualTo(GameOverlay.None));
            Assert.That(fixture.Backend.LastHandle.UnloadCount, Is.EqualTo(1));
            Assert.That(fixture.Backend.LastHandle.ReleaseCount, Is.EqualTo(1));
        }

        [Test]
        public async Task UnloadFailure_RestoresFallbackAndRecordsOneDiagnostic()
        {
            var fixture = await CreateFixture(GameMode.Flight, autoComplete: true);
            fixture.Backend.FailurePoint = FakeFailurePoint.Unload;
            await fixture.Service.LoadDestinationAsync("mirra", CancellationToken.None);

            var result = await fixture.Service.UnloadDestinationAsync(
                "mirra",
                CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(SceneStreamStatus.Failed));
            Assert.That(fixture.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(
                fixture.Diagnostics[0].Stage,
                Is.EqualTo(SceneStreamStage.CleaningUp));
            Assert.That(fixture.Fallback.Destinations, Is.EqualTo(new[] { "Frontend" }));
            Assert.That(fixture.Controller.CurrentMode, Is.EqualTo(GameMode.Frontend));
            Assert.That(fixture.Backend.RestoreCount, Is.EqualTo(1));
            Assert.That(fixture.Backend.LastHandle.ReleaseCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ShutdownUnloadFailure_StillReleasesCatalogAndIsIdempotent()
        {
            var fixture = await CreateFixture(GameMode.Flight, autoComplete: true);
            fixture.Backend.FailurePoint = FakeFailurePoint.Unload;
            await fixture.Service.LoadDestinationAsync("mirra", CancellationToken.None);
            m_Services.Remove(fixture.Service);

            var firstShutdown = fixture.Service.ShutdownAsync().AsTask();
            var secondShutdown = fixture.Service.ShutdownAsync().AsTask();
            Assert.That(secondShutdown, Is.SameAs(firstShutdown));
            var failure = await CaptureFailureAsync(firstShutdown);

            Assert.That(failure, Is.InstanceOf<AggregateException>());
            Assert.That(fixture.Source.ReleaseCount, Is.EqualTo(1));
            Assert.That(fixture.Backend.RestoreCount, Is.EqualTo(1));
            Assert.That(fixture.Backend.LastHandle.UnloadCount, Is.EqualTo(1));
            Assert.That(fixture.Backend.LastHandle.ReleaseCount, Is.EqualTo(1));
            Assert.That(fixture.Service.IsInitialized, Is.False);
        }

        [Test]
        public async Task ConcurrentLoad_FailsClosedWithoutIssuingSecondOperation()
        {
            var fixture = await CreateFixture(GameMode.Flight, autoComplete: false);
            using var cancellation = new CancellationTokenSource();
            var first = fixture.Service.LoadDestinationAsync(
                "mirra",
                cancellation.Token).AsTask();
            await fixture.Backend.WaitForBeginAsync();

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await fixture.Service.LoadDestinationAsync(
                    "koro",
                    CancellationToken.None));
            Assert.That(fixture.Backend.BeginCount, Is.EqualTo(1));

            cancellation.Cancel();
            fixture.Backend.LastHandle.CompleteLoad();
            await AssertOperationCancelledAsync(first);
        }

        [Test]
        public async Task ShutdownDuringLoad_CancelsSettlesAndIsIdempotent()
        {
            var baselineCount = SceneManager.sceneCount;
            var fixture = await CreateFixture(GameMode.Flight, autoComplete: false);
            var load = fixture.Service.LoadDestinationAsync(
                "mirra",
                CancellationToken.None).AsTask();
            await fixture.Backend.WaitForBeginAsync();

            var firstShutdown = fixture.Service.ShutdownAsync().AsTask();
            var secondShutdown = fixture.Service.ShutdownAsync().AsTask();
            Assert.That(secondShutdown, Is.SameAs(firstShutdown));
            Assert.That(firstShutdown.IsCompleted, Is.False);
            fixture.Backend.LastHandle.CompleteLoad();

            await firstShutdown;
            await AssertOperationCancelledAsync(load);
            Assert.That(SceneManager.sceneCount, Is.EqualTo(baselineCount));
            Assert.That(fixture.Backend.LastHandle.UnloadCount, Is.EqualTo(1));
            Assert.That(fixture.Backend.LastHandle.ReleaseCount, Is.EqualTo(1));
            Assert.That(fixture.Source.ReleaseCount, Is.EqualTo(1));
        }

        [Test]
        public async Task RepeatedCycles_LeakNoScenesHandlesOrBootstrapObjects()
        {
            var baselineScenes = SceneManager.sceneCount;
            var baselineBootstraps = UnityEngine.Object.FindObjectsByType<GameBootstrap>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None).Length;
            var fixture = await CreateFixture(GameMode.Flight, autoComplete: true);

            for (var cycle = 0; cycle < 3; cycle++)
            {
                await fixture.Service.LoadDestinationAsync(
                    "mirra",
                    CancellationToken.None);
                await fixture.Service.UnloadDestinationAsync(
                    "mirra",
                    CancellationToken.None);
                await fixture.Controller.RecoverAsync(
                    GameMode.Flight,
                    CancellationToken.None);
            }

            Assert.That(SceneManager.sceneCount, Is.EqualTo(baselineScenes));
            Assert.That(fixture.Backend.Handles, Has.Count.EqualTo(3));
            Assert.That(
                fixture.Backend.Handles.All(handle =>
                    handle.UnloadCount == 1 && handle.ReleaseCount == 1),
                Is.True);
            Assert.That(
                UnityEngine.Object.FindObjectsByType<GameBootstrap>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Length,
                Is.EqualTo(baselineBootstraps));
        }

        [Test]
        public async Task InvalidCatalog_MakesRequiredServiceUnavailableAndReleasesSource()
        {
            var invalid = SceneCatalog.CreateForTests(
                SceneCatalog.CurrentSchemaVersion,
                "Frontend",
                GameMode.Frontend,
                new SceneCatalogEntry("bad", "", GameMode.Surface));
            var source = new StaticCatalogSource(invalid);
            var controller = OwnController(GameMode.Frontend);
            await controller.InitializeAsync(CancellationToken.None);
            var service = OwnService(new SceneStreamService(
                source,
                new FakeSceneBackend(autoComplete: true),
                new RecordingTransition(),
                controller));

            var result = await service.InitializeAsync(CancellationToken.None);

            Assert.That(result.IsAvailable, Is.False);
            Assert.That(result.Message, Does.Contain("catalog"));
            Assert.That(source.ReleaseCount, Is.EqualTo(1));
        }

        private async Task<Fixture> CreateFixture(
            GameMode initialMode,
            bool autoComplete)
        {
            var entries = new[]
            {
                new SceneCatalogEntry("mirra", "scene.mirra", GameMode.Surface),
                new SceneCatalogEntry("koro", "scene.koro", GameMode.Surface),
            };
            var catalog = SceneCatalog.CreateForTests(
                SceneCatalog.CurrentSchemaVersion,
                "Frontend",
                GameMode.Frontend,
                entries);
            var source = new StaticCatalogSource(catalog);
            var hooks = new RecordingModeHooks();
            var controller = OwnController(
                GameModeController.CreateForTests(initialMode, hooks));
            Assert.That(
                (await controller.InitializeAsync(CancellationToken.None)).IsAvailable,
                Is.True);
            var backend = new FakeSceneBackend(autoComplete);
            var fallback = new RecordingTransition();
            var service = OwnService(new SceneStreamService(
                source,
                backend,
                fallback,
                controller));
            var startup = await service.InitializeAsync(CancellationToken.None);
            Assert.That(startup.IsAvailable, Is.True);
            var diagnostics = new List<SceneStreamDiagnostic>();
            service.DiagnosticRecorded += diagnostics.Add;
            return new Fixture(
                service,
                controller,
                source,
                backend,
                fallback,
                diagnostics);
        }

        private static async Task AssertOperationCancelledAsync(Task operation)
        {
            try
            {
                await operation;
            }
            catch (OperationCanceledException)
            {
                return;
            }

            Assert.Fail("Expected the asynchronous operation to be cancelled.");
        }

        private static async Task<Exception> CaptureFailureAsync(Task operation)
        {
            try
            {
                await operation;
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        private GameModeController OwnController(GameMode initialMode)
        {
            return OwnController(GameModeController.CreateForTests(initialMode));
        }

        private GameModeController OwnController(GameModeController controller)
        {
            m_Controllers.Add(controller);
            return controller;
        }

        private SceneStreamService OwnService(SceneStreamService service)
        {
            m_Services.Add(service);
            return service;
        }

        private sealed class Fixture
        {
            public Fixture(
                SceneStreamService service,
                GameModeController controller,
                StaticCatalogSource source,
                FakeSceneBackend backend,
                RecordingTransition fallback,
                List<SceneStreamDiagnostic> diagnostics)
            {
                Service = service;
                Controller = controller;
                Source = source;
                Backend = backend;
                Fallback = fallback;
                Diagnostics = diagnostics;
            }

            public SceneStreamService Service { get; }
            public GameModeController Controller { get; }
            public StaticCatalogSource Source { get; }
            public FakeSceneBackend Backend { get; }
            public RecordingTransition Fallback { get; }
            public List<SceneStreamDiagnostic> Diagnostics { get; }
        }

        private sealed class StaticCatalogSource : ISceneCatalogSource
        {
            private readonly SceneCatalog m_Catalog;

            public StaticCatalogSource(SceneCatalog catalog)
            {
                m_Catalog = catalog;
            }

            public int ReleaseCount { get; private set; }

            public ValueTask<SceneCatalog> LoadAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new ValueTask<SceneCatalog>(m_Catalog);
            }

            public ValueTask ReleaseAsync()
            {
                ReleaseCount++;
                return default;
            }
        }

        private sealed class RecordingModeHooks : IGameModeRuntimeHooks
        {
            public ValueTask ApplyAsync(
                GameModeRuntimePolicy policy,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return default;
            }
        }

        private sealed class RecordingTransition : ISceneTransition
        {
            private readonly List<string> m_Destinations = new List<string>();

            public IReadOnlyList<string> Destinations => m_Destinations;

            public ValueTask RouteAsync(
                string destination,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                m_Destinations.Add(destination);
                return default;
            }
        }

        public enum FakeFailurePoint
        {
            None,
            Load,
            Activate,
            Unload,
        }

        private sealed class FakeSceneBackend : ISceneStreamBackend
        {
            private readonly bool m_AutoComplete;
            private readonly TaskCompletionSource<object> m_Began =
                new TaskCompletionSource<object>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            public FakeSceneBackend(bool autoComplete)
            {
                m_AutoComplete = autoComplete;
            }

            public readonly List<FakeSceneLoadHandle> Handles =
                new List<FakeSceneLoadHandle>();

            public FakeFailurePoint FailurePoint { get; set; }

            public int BeginCount { get; private set; }

            public int RestoreCount { get; private set; }

            public FakeSceneLoadHandle LastHandle => Handles.Last();

            public object CaptureActiveScene()
            {
                return SceneManager.GetActiveScene();
            }

            public ISceneLoadHandle BeginLoad(string address, string destinationId)
            {
                BeginCount++;
                var handle = new FakeSceneLoadHandle(
                    destinationId,
                    FailurePoint);
                Handles.Add(handle);
                m_Began.TrySetResult(null);
                if (m_AutoComplete)
                {
                    handle.CompleteLoad();
                }

                return handle;
            }

            public void RestoreActiveScene(object sceneToken)
            {
                RestoreCount++;
                if (sceneToken is Scene scene && scene.IsValid() && scene.isLoaded)
                {
                    SceneManager.SetActiveScene(scene);
                }
            }

            public Task WaitForBeginAsync()
            {
                return m_Began.Task;
            }
        }

        private sealed class FakeSceneLoadHandle : ISceneLoadHandle
        {
            private readonly string m_DestinationId;
            private readonly FakeFailurePoint m_FailurePoint;
            private readonly TaskCompletionSource<object> m_Loaded =
                new TaskCompletionSource<object>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            private Scene m_Scene;

            public FakeSceneLoadHandle(
                string destinationId,
                FakeFailurePoint failurePoint)
            {
                m_DestinationId = destinationId;
                m_FailurePoint = failurePoint;
            }

            public Task LoadTask => m_Loaded.Task;

            public float PercentComplete => m_Loaded.Task.IsCompleted ? 1f : 0.25f;

            public Exception LoadFailure => m_FailurePoint == FakeFailurePoint.Load
                ? new InvalidOperationException("load failure")
                : null;

            public bool HasLoadedScene => m_Scene.IsValid() && m_Scene.isLoaded;

            public int ActivateCount { get; private set; }
            public int UnloadCount { get; private set; }
            public int ReleaseCount { get; private set; }

            public void CompleteLoad()
            {
                if (m_Loaded.Task.IsCompleted)
                {
                    return;
                }

                if (m_FailurePoint != FakeFailurePoint.Load)
                {
                    m_Scene = SceneManager.CreateScene("JssTask8_" + m_DestinationId);
                }

                m_Loaded.TrySetResult(null);
            }

            public Task ActivateAsync()
            {
                ActivateCount++;
                if (m_FailurePoint == FakeFailurePoint.Activate)
                {
                    throw new InvalidOperationException("activation failure");
                }

                Assert.That(SceneManager.SetActiveScene(m_Scene), Is.True);
                return Task.CompletedTask;
            }

            public async Task UnloadAsync()
            {
                UnloadCount++;
                if (!HasLoadedScene)
                {
                    return;
                }

                var unload = SceneManager.UnloadSceneAsync(m_Scene);
                Assert.That(unload, Is.Not.Null);
                while (!unload.isDone)
                {
                    await Task.Yield();
                }

                if (m_FailurePoint == FakeFailurePoint.Unload)
                {
                    throw new InvalidOperationException("unload failure");
                }
            }

            public void Release()
            {
                ReleaseCount++;
                Assert.That(ReleaseCount, Is.EqualTo(1));
            }
        }
    }
}
