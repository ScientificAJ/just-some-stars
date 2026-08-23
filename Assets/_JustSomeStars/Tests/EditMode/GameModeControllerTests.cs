using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Input;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class GameModeControllerTests
    {
        private static readonly IReadOnlyDictionary<GameMode, GameMode[]>
            s_AllowedTransitions =
                new Dictionary<GameMode, GameMode[]>
                {
                    [GameMode.Frontend] = new[] { GameMode.Customization },
                    [GameMode.Customization] = new[]
                    {
                        GameMode.Frontend,
                        GameMode.Clubhouse,
                    },
                    [GameMode.Clubhouse] = new[]
                    {
                        GameMode.Customization,
                        GameMode.Flight,
                    },
                    [GameMode.Flight] = new[]
                    {
                        GameMode.Clubhouse,
                        GameMode.Surface,
                    },
                    [GameMode.Surface] = new[]
                    {
                        GameMode.Flight,
                        GameMode.Lens,
                        GameMode.Dialogue,
                        GameMode.Cinematic,
                    },
                    [GameMode.Lens] = new[] { GameMode.Surface },
                    [GameMode.Dialogue] = new[] { GameMode.Surface },
                    [GameMode.Cinematic] = new[] { GameMode.Surface },
                };

        [Test]
        public void TransitionMatrix_IsCompleteAndRejectsEveryUnlistedEdge()
        {
            foreach (GameMode source in Enum.GetValues(typeof(GameMode)))
            {
                var controller = GameModeController.CreateForTests(source);
                foreach (GameMode destination in Enum.GetValues(typeof(GameMode)))
                {
                    var expected = source == destination ||
                        s_AllowedTransitions[source].Contains(destination);
                    Assert.That(
                        controller.CanEnter(destination),
                        Is.EqualTo(expected),
                        $"Unexpected {source} -> {destination} policy.");
                }
            }
        }

        [Test]
        public async Task SameMode_IsIdempotentWithoutCallingRuntimeHook()
        {
            var hooks = new RecordingRuntimeHooks();
            var controller = GameModeController.CreateForTests(
                GameMode.Surface,
                hooks);
            await controller.InitializeAsync(CancellationToken.None);
            hooks.Policies.Clear();

            var result = await controller.EnterAsync(
                GameMode.Surface,
                CancellationToken.None);

            Assert.That(result, Is.EqualTo(GameModeTransitionResult.Unchanged));
            Assert.That(hooks.Policies, Is.Empty);
            Assert.That(controller.CurrentMode, Is.EqualTo(GameMode.Surface));
        }

        [Test]
        public async Task InputAndCameraPolicyMapping_IsExactForEveryBaseMode()
        {
            var expected = new Dictionary<GameMode, GameplayInputMode>
            {
                [GameMode.Frontend] = GameplayInputMode.None,
                [GameMode.Customization] = GameplayInputMode.None,
                [GameMode.Clubhouse] = GameplayInputMode.None,
                [GameMode.Flight] = GameplayInputMode.Flight,
                [GameMode.Surface] = GameplayInputMode.Surface,
                [GameMode.Lens] = GameplayInputMode.Lens,
                [GameMode.Dialogue] = GameplayInputMode.None,
                [GameMode.Cinematic] = GameplayInputMode.None,
            };

            foreach (var entry in expected)
            {
                var hooks = new RecordingRuntimeHooks();
                var controller = GameModeController.CreateForTests(
                    entry.Key,
                    hooks);

                var startup = await controller.InitializeAsync(
                    CancellationToken.None);

                Assert.That(startup.IsAvailable, Is.True);
                Assert.That(hooks.Policies, Has.Count.EqualTo(1));
                Assert.That(hooks.Policies[0].Mode, Is.EqualTo(entry.Key));
                Assert.That(hooks.Policies[0].Overlay, Is.EqualTo(GameOverlay.None));
                Assert.That(hooks.Policies[0].InputMode, Is.EqualTo(entry.Value));
                Assert.That(
                    hooks.Policies[0].CameraPolicy.ToString(),
                    Is.EqualTo(entry.Key.ToString()));
            }
        }

        [Test]
        public async Task CancelledOrFailedHook_RestoresExactPriorPolicyAndState()
        {
            var cancellationHooks = new RecordingRuntimeHooks
            {
                ThrowCancellationFor = GameMode.Surface,
            };
            var cancelled = GameModeController.CreateForTests(
                GameMode.Flight,
                cancellationHooks);
            await cancelled.InitializeAsync(CancellationToken.None);

            Exception cancellationFailure = null;
            try
            {
                await cancelled.EnterAsync(
                    GameMode.Surface,
                    CancellationToken.None);
            }
            catch (Exception exception)
            {
                cancellationFailure = exception;
            }

            Assert.That(
                cancellationFailure,
                Is.InstanceOf<OperationCanceledException>());
            Assert.That(cancelled.CurrentMode, Is.EqualTo(GameMode.Flight));
            Assert.That(cancelled.ActiveOverlay, Is.EqualTo(GameOverlay.None));
            Assert.That(cancellationHooks.Policies.Last().Mode, Is.EqualTo(GameMode.Flight));
            Assert.That(
                cancellationHooks.Policies.Last().InputMode,
                Is.EqualTo(GameplayInputMode.Flight));

            var failureHooks = new RecordingRuntimeHooks
            {
                ThrowFailureFor = GameMode.Surface,
            };
            var failed = GameModeController.CreateForTests(
                GameMode.Flight,
                failureHooks);
            await failed.InitializeAsync(CancellationToken.None);

            var transitionFailure = await CaptureFailureAsync(
                failed.EnterAsync(
                    GameMode.Surface,
                    CancellationToken.None).AsTask());
            Assert.That(
                transitionFailure,
                Is.TypeOf<InvalidOperationException>());
            Assert.That(failed.CurrentMode, Is.EqualTo(GameMode.Flight));
            Assert.That(failureHooks.Policies.Last().Mode, Is.EqualTo(GameMode.Flight));
        }

        [Test]
        public async Task ConcurrentAndReentrantRequests_FailWithoutChangingCommittedState()
        {
            var hooks = new RecordingRuntimeHooks();
            var gate = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            hooks.BlockFor = GameMode.Surface;
            hooks.BlockGate = gate.Task;
            var controller = GameModeController.CreateForTests(GameMode.Flight, hooks);
            await controller.InitializeAsync(CancellationToken.None);

            var first = controller.EnterAsync(
                GameMode.Surface,
                CancellationToken.None).AsTask();
            await hooks.WaitUntilBlocked();
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await controller.EnterAsync(
                    GameMode.Clubhouse,
                    CancellationToken.None));
            Assert.That(controller.CurrentMode, Is.EqualTo(GameMode.Flight));

            gate.TrySetResult(null);
            await first;
            Exception reentrantFailure = null;
            controller.StateChanged += _ =>
            {
                try
                {
                    controller.EnterAsync(
                            GameMode.Flight,
                            CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();
                }
                catch (Exception exception)
                {
                    reentrantFailure = exception;
                }
            };

            await controller.EnterAsync(GameMode.Lens, CancellationToken.None);

            Assert.That(reentrantFailure, Is.TypeOf<InvalidOperationException>());
            Assert.That(controller.CurrentMode, Is.EqualTo(GameMode.Lens));
        }

        [Test]
        public async Task ShutdownDuringBlockedHook_CancelsSettlesAndIsIdempotent()
        {
            var hooks = new RecordingRuntimeHooks();
            hooks.BlockFor = GameMode.Surface;
            hooks.BlockGate = hooks.ReleaseGate.Task;
            var controller = GameModeController.CreateForTests(GameMode.Flight, hooks);
            await controller.InitializeAsync(CancellationToken.None);

            var transition = controller.EnterAsync(
                GameMode.Surface,
                CancellationToken.None).AsTask();
            await hooks.WaitUntilBlocked();
            var firstShutdown = controller.ShutdownAsync().AsTask();
            var secondShutdown = controller.ShutdownAsync().AsTask();
            Assert.That(secondShutdown, Is.SameAs(firstShutdown));

            var shutdownFailure = await CaptureFailureAsync(firstShutdown);
            hooks.ReleaseGate.TrySetResult(null);
            var transitionFailure = await CaptureFailureAsync(transition);

            Assert.That(shutdownFailure, Is.Null);
            Assert.That(
                transitionFailure,
                Is.InstanceOf<OperationCanceledException>());
            Assert.That(controller.CurrentMode, Is.EqualTo(GameMode.Frontend));
            Assert.That(controller.ActiveOverlay, Is.EqualTo(GameOverlay.None));
            Assert.That(controller.IsInitialized, Is.False);
            Assert.That(hooks.Policies.Last().Mode, Is.EqualTo(GameMode.Frontend));
        }

        [Test]
        public async Task OverlayRules_AreExactAndCloseRestoresUnderlyingMode()
        {
            var allowed = new Dictionary<GameOverlay, GameMode[]>
            {
                [GameOverlay.Settings] = Enum.GetValues(typeof(GameMode))
                    .Cast<GameMode>()
                    .ToArray(),
                [GameOverlay.Pause] = new[]
                {
                    GameMode.Clubhouse,
                    GameMode.Flight,
                    GameMode.Surface,
                    GameMode.Lens,
                },
                [GameOverlay.PhotoMode] = new[]
                {
                    GameMode.Clubhouse,
                    GameMode.Flight,
                    GameMode.Surface,
                },
            };

            foreach (GameMode mode in Enum.GetValues(typeof(GameMode)))
            {
                var controller = GameModeController.CreateForTests(mode);
                foreach (var entry in allowed)
                {
                    Assert.That(
                        controller.CanOpenOverlay(entry.Key),
                        Is.EqualTo(entry.Value.Contains(mode)),
                        $"Unexpected {entry.Key} policy for {mode}.");
                }
            }

            var hooks = new RecordingRuntimeHooks();
            var surface = GameModeController.CreateForTests(GameMode.Surface, hooks);
            await surface.InitializeAsync(CancellationToken.None);
            Assert.That(
                await surface.OpenOverlayAsync(
                    GameOverlay.PhotoMode,
                    CancellationToken.None),
                Is.EqualTo(GameModeTransitionResult.Changed));
            Assert.That(surface.CurrentMode, Is.EqualTo(GameMode.Surface));
            Assert.That(surface.ActiveOverlay, Is.EqualTo(GameOverlay.PhotoMode));
            Assert.That(hooks.Policies.Last().InputMode, Is.EqualTo(GameplayInputMode.None));
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await surface.OpenOverlayAsync(
                    GameOverlay.Settings,
                    CancellationToken.None));
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await surface.EnterAsync(GameMode.Flight, CancellationToken.None));

            Assert.That(
                await surface.CloseOverlayAsync(CancellationToken.None),
                Is.EqualTo(GameModeTransitionResult.Changed));
            Assert.That(surface.CurrentMode, Is.EqualTo(GameMode.Surface));
            Assert.That(surface.ActiveOverlay, Is.EqualTo(GameOverlay.None));
            Assert.That(hooks.Policies.Last().Mode, Is.EqualTo(GameMode.Surface));
            Assert.That(
                hooks.Policies.Last().InputMode,
                Is.EqualTo(GameplayInputMode.Surface));
        }

        private sealed class RecordingRuntimeHooks : IGameModeRuntimeHooks
        {
            private readonly TaskCompletionSource<object> m_Blocked =
                new TaskCompletionSource<object>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            public readonly List<GameModeRuntimePolicy> Policies =
                new List<GameModeRuntimePolicy>();

            public GameMode? ThrowCancellationFor { get; set; }

            public GameMode? ThrowFailureFor { get; set; }

            public GameMode? BlockFor { get; set; }

            public Task BlockGate { get; set; }

            public TaskCompletionSource<object> ReleaseGate { get; } =
                new TaskCompletionSource<object>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            public async ValueTask ApplyAsync(
                GameModeRuntimePolicy policy,
                CancellationToken cancellationToken)
            {
                Policies.Add(policy);
                if (BlockFor == policy.Mode && BlockGate != null)
                {
                    m_Blocked.TrySetResult(null);
                    var cancellation = Task.Delay(
                        Timeout.Infinite,
                        cancellationToken);
                    var completed = await Task.WhenAny(BlockGate, cancellation);
                    if (ReferenceEquals(completed, cancellation))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    await BlockGate;
                }

                if (ThrowCancellationFor == policy.Mode)
                {
                    ThrowCancellationFor = null;
                    throw new OperationCanceledException(cancellationToken);
                }

                if (ThrowFailureFor == policy.Mode)
                {
                    ThrowFailureFor = null;
                    throw new InvalidOperationException("mode hook failure");
                }
            }

            public Task WaitUntilBlocked()
            {
                return m_Blocked.Task;
            }
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
    }

    public sealed class SceneCatalogTests
    {
        [Test]
        public void VersionOneCatalog_RejectsMalformedOrDuplicateEntries()
        {
            AssertCatalogRejected(
                new SceneCatalogEntry("", "scene.surface", GameMode.Surface));
            AssertCatalogRejected(
                new SceneCatalogEntry("mirra", "", GameMode.Surface));
            AssertCatalogRejected(
                new SceneCatalogEntry(" mirra ", "scene.surface", GameMode.Surface));
            AssertCatalogRejected(
                new SceneCatalogEntry("mirra", " scene.surface ", GameMode.Surface));
            AssertCatalogRejected(
                new SceneCatalogEntry(
                    "mirra",
                    "scene.surface",
                    (GameMode)999));

            var duplicateId = SceneCatalog.CreateForTests(
                SceneCatalog.CurrentSchemaVersion,
                "Frontend",
                GameMode.Frontend,
                new SceneCatalogEntry("mirra", "scene.a", GameMode.Surface),
                new SceneCatalogEntry("mirra", "scene.b", GameMode.Surface));
            Assert.Throws<InvalidOperationException>(() => duplicateId.Validate());

            var duplicateAddress = SceneCatalog.CreateForTests(
                SceneCatalog.CurrentSchemaVersion,
                "Frontend",
                GameMode.Frontend,
                new SceneCatalogEntry("mirra", "scene.same", GameMode.Surface),
                new SceneCatalogEntry("koro", "scene.same", GameMode.Flight));
            Assert.Throws<InvalidOperationException>(() => duplicateAddress.Validate());

            var future = SceneCatalog.CreateForTests(
                SceneCatalog.CurrentSchemaVersion + 1,
                "Frontend",
                GameMode.Frontend);
            Assert.Throws<InvalidOperationException>(() => future.Validate());
        }

        [Test]
        public void CommittedCatalog_IsVersionOneEmptyAndAddressableAtCanonicalKey()
        {
            const string path = "Assets/_JustSomeStars/Content/SceneCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<SceneCatalog>(path);

            Assert.That(catalog, Is.Not.Null);
            Assert.DoesNotThrow(() => catalog.Validate());
            Assert.That(catalog.SchemaVersion, Is.EqualTo(SceneCatalog.CurrentSchemaVersion));
            Assert.That(catalog.FallbackSceneName, Is.EqualTo("Frontend"));
            Assert.That(catalog.FallbackMode, Is.EqualTo(GameMode.Frontend));
            Assert.That(catalog.Entries, Is.Empty);

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            Assert.That(settings, Is.Not.Null);
            var guid = AssetDatabase.AssetPathToGUID(path);
            var entry = settings.FindAssetEntry(guid);
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.address, Is.EqualTo(SceneCatalog.AddressablesKey));
            Assert.That(entry.parentGroup, Is.SameAs(settings.DefaultGroup));
        }

        private static void AssertCatalogRejected(SceneCatalogEntry entry)
        {
            var catalog = SceneCatalog.CreateForTests(
                SceneCatalog.CurrentSchemaVersion,
                "Frontend",
                GameMode.Frontend,
                entry);
            Assert.Throws<InvalidOperationException>(() => catalog.Validate());
        }
    }
}
