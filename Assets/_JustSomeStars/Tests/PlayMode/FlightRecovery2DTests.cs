using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Flight;
using JustSomeStars.Runtime.Input;
using JustSomeStars.Runtime.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class FlightRecovery2DTests
    {
        private readonly List<GameObject> m_Objects = new List<GameObject>();
        private string m_InputRoot;
        private SettingsService m_InputSettings;
        private InputActionAsset m_InputActions;
        private InputRouter m_Input;
        private GameModeController m_InputModes;
        private UnitySceneTransition m_InputTransition;
        private Touchscreen m_Touchscreen;
        private InputSettings.BackgroundBehavior m_PreviousBackground;
        private InputSettings.EditorInputBehaviorInPlayMode m_PreviousEditor;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var value in m_Objects)
            {
                if (value != null)
                {
                    UnityEngine.Object.DestroyImmediate(value);
                }
            }

            m_Objects.Clear();
            if (m_InputSettings == null)
            {
                yield break;
            }

            m_InputTransition?.ReleaseBindings();
            if (m_Touchscreen != null && m_Touchscreen.added)
            {
                InputSystem.RemoveDevice(m_Touchscreen);
            }
            InputSystem.settings.backgroundBehavior = m_PreviousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = m_PreviousEditor;

            var recovery = SceneManager.CreateScene(
                "Task17FlightRecovery_" + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(recovery);
            foreach (var scene in Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Where(scene => scene.IsValid() && scene.isLoaded && scene != recovery)
                .ToArray())
            {
                var unload = SceneManager.UnloadSceneAsync(scene);
                if (unload != null)
                {
                    yield return unload;
                }
            }

            yield return WaitForTask(m_InputModes.ShutdownAsync().AsTask());
            yield return WaitForTask(m_Input.ShutdownAsync().AsTask());
            yield return WaitForTask(m_InputSettings.ShutdownAsync().AsTask());
            UnityEngine.Object.DestroyImmediate(m_InputActions);
            if (Directory.Exists(m_InputRoot))
            {
                Directory.Delete(m_InputRoot, true);
            }

            m_InputTransition = null;
            m_Touchscreen = null;
            m_InputModes = null;
            m_Input = null;
            m_InputActions = null;
            m_InputSettings = null;
            m_InputRoot = null;
        }

        [UnityTest]
        public IEnumerator ProductionRoute_ContainsBoundFlightCameraLanesPredictionAndTouchHud()
        {
            var operation = SceneManager.LoadSceneAsync(
                "Task17FlightGraybox",
                LoadSceneMode.Single);
            Assert.That(operation, Is.Not.Null);
            yield return operation;
            yield return null;

            var lifecycle = UnityEngine.Object.FindFirstObjectByType<
                FlightGameplayLifecycle2D>(FindObjectsInactive.Include);
            Assert.That(lifecycle, Is.Not.Null);
            Assert.That(lifecycle.NominalRouteSeconds, Is.EqualTo(90f).Within(0.01f));
            Assert.That(lifecycle.Motor, Is.Not.Null);
            var configuredMaximumSpeed = typeof(FlightMotor2D).GetProperty(
                "ConfiguredMaximumSpeed");
            Assert.That(
                configuredMaximumSpeed,
                Is.Not.Null,
                "The production route needs an authored speed so its 90-second " +
                "contract is measurable rather than a label.");
            var route = UnityEngine.Object.FindFirstObjectByType<
                Task17FlightRoute2D>(FindObjectsInactive.Include);
            Assert.That(route, Is.Not.Null);
            var maximumSpeed = (float)configuredMaximumSpeed.GetValue(lifecycle.Motor);
            var measuredNominalSeconds = Vector2.Distance(
                route.RouteStart,
                route.RouteFinish) / maximumSpeed;
            Assert.That(
                measuredNominalSeconds,
                Is.EqualTo(lifecycle.NominalRouteSeconds).Within(1f));
            Assert.That(lifecycle.Landing, Is.Not.Null);
            Assert.That(lifecycle.PredictionArc, Is.Not.Null);
            Assert.That(lifecycle.PredictionArc.PointCount, Is.GreaterThanOrEqualTo(12));
            Assert.That(lifecycle.CompositionCamera, Is.Not.Null);
            Assert.That(
                lifecycle.CompositionCamera.CurrentPolicy,
                Is.EqualTo(GameCameraPolicy.Flight));
            Assert.That(UnityEngine.Object.FindObjectsByType<FlightDepthLane>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None), Has.Length.GreaterThanOrEqualTo(3));
            Assert.That(UnityEngine.Object.FindObjectsByType<GravityAssistVolume2D>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None), Has.Length.GreaterThanOrEqualTo(1));
            Assert.That(UnityEngine.Object.FindObjectsByType<FlightCheckpoint>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None), Has.Length.GreaterThanOrEqualTo(1));

            var presentation = UnityEngine.Object.FindFirstObjectByType<
                PlayerShipPresentation2D>(FindObjectsInactive.Include);
            Assert.That(presentation, Is.Not.Null);
            var engineField = typeof(PlayerShipPresentation2D).GetField(
                "engine",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var engine = (SpriteRenderer)engineField.GetValue(presentation);
            var initialEngineFrame = engine.sprite;
            presentation.SetMotion(1f, reduceMotion: false);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(
                engine.sprite,
                Is.Not.SameAs(initialEngineFrame),
                "The production engine must animate authored atlas frames, not " +
                "only pulse one static sprite.");

            var landingField = typeof(PlayerShipPresentation2D).GetField(
                "landingGear",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var doorField = typeof(PlayerShipPresentation2D).GetField(
                "door",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var landingRenderer = (SpriteRenderer)landingField.GetValue(presentation);
            var doorRenderer = (SpriteRenderer)doorField.GetValue(presentation);
            var landingPivot = landingRenderer.transform.parent;
            var doorPivot = doorRenderer.transform.parent;
            var landingPosition = landingPivot.localPosition;
            var doorRotation = doorPivot.localRotation;
            presentation.SetLandingProgress(0f);
            var stowedFrame = landingRenderer.sprite;
            presentation.SetLandingProgress(0.5f);
            Assert.That(landingRenderer.sprite, Is.Not.SameAs(stowedFrame));
            presentation.SetLandingProgress(1f);
            Assert.That(landingRenderer.sprite, Is.Not.SameAs(stowedFrame));
            Assert.That(landingPivot.localPosition, Is.EqualTo(landingPosition),
                "The landing atlas already authors gear travel; its pivot must " +
                "not apply that motion twice.");
            presentation.SetDoorOpen(false);
            var closedFrame = doorRenderer.sprite;
            presentation.SetDoorOpen(true);
            Assert.That(doorRenderer.sprite, Is.Not.SameAs(closedFrame));
            Assert.That(doorPivot.localRotation, Is.EqualTo(doorRotation),
                "The door atlas already authors rotation; its pivot must not " +
                "apply that motion twice.");

            var landing = lifecycle.Landing;
            var presentationField = typeof(LandingSequence).GetField(
                "presentation",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(presentationField, Is.Not.Null);
            Assert.That(presentationField.GetValue(landing), Is.SameAs(presentation),
                "The live landing route must drive the production presentation.");

            var motorField = typeof(LandingSequence).GetField(
                "motor",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(motorField, Is.Not.Null);
            Assert.That(motorField.GetValue(landing), Is.SameAs(lifecycle.Motor));
            presentation.SetLandingProgress(0f);
            presentation.SetDoorOpen(false);
            var originalFlightState = lifecycle.Motor.State;
            var failedTransition = new RecordingSceneTransition
            {
                Failure = new IOException("landing route rejected"),
            };
            landing.Configure(failedTransition, new GameEventBus());
            var failedLanding = landing.TryLandAsync(true, CancellationToken.None);
            Assert.That(lifecycle.Motor.State.LandingLocked, Is.True);
            Assert.That(lifecycle.Motor.State.Velocity, Is.EqualTo(Vector2.zero));
            while (failedTransition.CallCount == 0)
            {
                yield return null;
            }

            Assert.That(landingRenderer.sprite, Is.Not.SameAs(stowedFrame));
            Assert.That(doorRenderer.sprite, Is.Not.SameAs(closedFrame));
            failedTransition.Complete();
            yield return WaitForTask(failedLanding, allowFault: true);
            Assert.That(failedLanding.IsFaulted, Is.True);
            AssertState(lifecycle.Motor.State, originalFlightState);
            Assert.That(landingRenderer.sprite, Is.SameAs(stowedFrame));
            Assert.That(doorRenderer.sprite, Is.SameAs(closedFrame));
        }

        [UnityTest]
        public IEnumerator CheckpointRecovery_RestoresEveryCriticalFieldExactly()
        {
            var motor = CreateMotor();
            var checkpoint = CreateComponent<FlightCheckpoint>("Checkpoint");
            var expected = new FlightState(
                new Vector2(2f, -1f),
                new Vector2(3f, 0.5f),
                2,
                failurePending: false,
                landingLocked: false,
                elapsedSeconds: 41f);
            motor.SetStateForTests(expected);
            motor.CaptureCheckpoint(checkpoint);
            motor.SetStateForTests(new FlightState(
                new Vector2(200f, -300f),
                new Vector2(-8f, 4f),
                0,
                failurePending: true,
                landingLocked: false,
                elapsedSeconds: 54f));

            Assert.That(motor.RecoverLatestCheckpoint(), Is.True);
            AssertState(motor.State, expected);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RepeatedFailureRecovery_DoesNotDriftOrDuplicateCheckpointState()
        {
            var motor = CreateMotor();
            var checkpoint = CreateComponent<FlightCheckpoint>("Checkpoint");
            var expected = new FlightState(
                new Vector2(-4f, 1f),
                new Vector2(2f, -0.25f),
                1,
                elapsedSeconds: 23f);
            motor.SetStateForTests(expected);
            motor.CaptureCheckpoint(checkpoint);

            for (var iteration = 0; iteration < 5; iteration++)
            {
                motor.SetStateForTests(new FlightState(
                    Vector2.one * (100f + iteration),
                    Vector2.one * -20f,
                    2,
                    failurePending: true,
                    landingLocked: true,
                    elapsedSeconds: 80f));
                Assert.That(motor.RecoverLatestCheckpoint(), Is.True);
                AssertState(motor.State, expected);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Landing_InvalidApproachAndCancellationPublishNothing()
        {
            var transition = new RecordingSceneTransition();
            var events = new GameEventBus();
            var published = 0;
            using var subscription = events.Subscribe<LandingCompleted>(_ => published++);
            var landing = CreateComponent<LandingSequence>("Landing");
            landing.Configure(transition, events);

            var invalid = landing.TryLandAsync(false, CancellationToken.None);
            yield return WaitForTask(invalid);
            Assert.That(invalid.Result, Is.False);
            Assert.That(transition.CallCount, Is.Zero);
            Assert.That(published, Is.Zero);

            landing.Cancel();
            Assert.That(landing.State, Is.EqualTo(LandingSequenceState.Flight));
            Assert.That(published, Is.Zero);
        }

        [UnityTest]
        public IEnumerator Landing_SuccessPublishesExactlyOnceAfterTransitionCompletes()
        {
            var transition = new RecordingSceneTransition();
            var events = new GameEventBus();
            var published = 0;
            ContentId destination = default;
            using var subscription = events.Subscribe<LandingCompleted>(value =>
            {
                published++;
                destination = value.DestinationId;
            });
            var landing = CreateComponent<LandingSequence>("Landing");
            landing.Configure(transition, events);

            var task = landing.TryLandAsync(true, CancellationToken.None);
            Assert.That(published, Is.Zero);
            transition.Complete();
            yield return WaitForTask(task);

            Assert.That(task.Result, Is.True);
            Assert.That(transition.CallCount, Is.EqualTo(1));
            Assert.That(published, Is.EqualTo(1));
            Assert.That(destination.Value, Is.EqualTo("destination.mirra.surface"));
            Assert.That(landing.State, Is.EqualTo(LandingSequenceState.Completed));

            var repeated = landing.TryLandAsync(true, CancellationToken.None);
            yield return WaitForTask(repeated);
            Assert.That(repeated.Result, Is.False);
            Assert.That(transition.CallCount, Is.EqualTo(1));
            Assert.That(published, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Landing_TransitionFailureOrCancellationReturnsToRecoverableFlight()
        {
            foreach (var cancellation in new[] { false, true })
            {
                var transition = new RecordingSceneTransition
                {
                    Failure = cancellation
                        ? new OperationCanceledException()
                        : new IOException("route failed"),
                };
                var events = new GameEventBus();
                var published = 0;
                using var subscription = events.Subscribe<LandingCompleted>(_ => published++);
                var landing = CreateComponent<LandingSequence>(
                    cancellation ? "CanceledLanding" : "FailedLanding");
                landing.Configure(transition, events);

                var task = landing.TryLandAsync(true, CancellationToken.None);
                transition.Complete();
                yield return WaitForTask(task, allowFault: true);

                Assert.That(task.IsFaulted || task.IsCanceled, Is.True);
                Assert.That(landing.State, Is.EqualTo(LandingSequenceState.Flight));
                Assert.That(published, Is.Zero);
            }
        }

        [UnityTest]
        public IEnumerator Landing_ProductionReleaseDuringRouteStillPublishesExactlyOnce()
        {
            var transition = new ReleasingSceneTransition();
            var events = new GameEventBus();
            var published = 0;
            using var subscription = events.Subscribe<LandingCompleted>(_ => published++);
            var landing = CreateComponent<LandingSequence>("ReleasedDuringRouteLanding");
            landing.Configure(transition, events);
            transition.ReleaseDuringRoute = landing.Release;

            var task = landing.TryLandAsync(true, CancellationToken.None);
            yield return WaitForTask(task, allowFault: true);

            Assert.That(task.IsFaulted || task.IsCanceled, Is.False);
            Assert.That(task.Result, Is.True);
            Assert.That(published, Is.EqualTo(1));
            Assert.That(transition.CallCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Landing_CancelThenRetryCannotOverlapOrCorruptTheNewAttempt()
        {
            var transition = new CancelThenSucceedSceneTransition();
            var events = new GameEventBus();
            var published = 0;
            using var subscription = events.Subscribe<LandingCompleted>(_ => published++);
            var landing = CreateComponent<LandingSequence>("CancelRetryLanding");
            landing.Configure(transition, events);

            var first = landing.TryLandAsync(true, CancellationToken.None);
            landing.Cancel();
            var overlapping = landing.TryLandAsync(true, CancellationToken.None);
            yield return WaitForTask(overlapping, allowFault: true);
            Assert.That(overlapping.IsFaulted || overlapping.IsCanceled, Is.False);
            Assert.That(overlapping.Result, Is.False);
            Assert.That(published, Is.Zero);

            yield return WaitForTask(first, allowFault: true);
            Assert.That(first.IsCanceled || first.IsFaulted, Is.True);
            Assert.That(landing.State, Is.EqualTo(LandingSequenceState.Flight));

            var retry = landing.TryLandAsync(true, CancellationToken.None);
            yield return WaitForTask(retry);
            Assert.That(retry.Result, Is.True);
            Assert.That(published, Is.EqualTo(1));
            Assert.That(transition.CallCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator CanonicalFlightInput_SupportsSimultaneousSteerBoostAndBrake()
        {
            m_InputRoot = Path.Combine(
                Path.GetTempPath(),
                "JssTask17FlightInput",
                Guid.NewGuid().ToString("N"));
            m_InputSettings = new SettingsService(Path.Combine(
                m_InputRoot,
                "settings.json"));
            m_InputActions = UnityEngine.Object.Instantiate(InputSystem.actions);
            m_Input = new InputRouter(m_InputActions, m_InputSettings);
            m_InputModes = GameModeController.CreateForTests(
                GameMode.Flight,
                new InputRouterGameModeRuntimeHooks(m_Input));
            var events = new GameEventBus();
            m_InputTransition = new UnitySceneTransition();
            m_InputTransition.ConfigureFlightDependencies(
                new FlightGameplayDependencies(
                m_InputSettings,
                m_Input,
                m_InputModes,
                events,
                m_InputTransition));
            m_PreviousBackground = InputSystem.settings.backgroundBehavior;
            m_PreviousEditor =
                InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior =
                InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode
                    .AllDeviceInputAlwaysGoesToGameView;

            var settingsTask = m_InputSettings.InitializeAsync(
                CancellationToken.None).AsTask();
            yield return WaitForTask(settingsTask);
            var inputTask = m_Input.InitializeAsync(
                CancellationToken.None).AsTask();
            yield return WaitForTask(inputTask);
            var modeTask = m_InputModes.InitializeAsync(
                CancellationToken.None).AsTask();
            yield return WaitForTask(modeTask);
            Assert.That(m_Input.ActiveGameplayMode,
                Is.EqualTo(GameplayInputMode.Flight));

            var load = SceneManager.LoadSceneAsync(
                "Task17FlightGraybox",
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;
            m_InputTransition.BindActiveScene();

            var lifecycle = UnityEngine.Object.FindFirstObjectByType<
                FlightGameplayLifecycle2D>(FindObjectsInactive.Include);
            Assert.That(lifecycle, Is.Not.Null);
            Assert.That(lifecycle.IsConfigured, Is.True);
            var inputModule = EventSystem.current.currentInputModule as
                InputSystemUIInputModule;
            Assert.That(inputModule, Is.Not.Null);
            var sticks = UnityEngine.Object.FindObjectsByType<OnScreenStick>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            var steering = sticks.Single(candidate =>
                candidate.controlPath == "<Gamepad>/leftStick");
            var buttons = UnityEngine.Object.FindObjectsByType<OnScreenButton>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            var boost = buttons.Single(candidate =>
                candidate.controlPath == "<Gamepad>/buttonSouth");
            var brake = buttons.Single(candidate =>
                candidate.controlPath == "<Gamepad>/buttonWest");
            var lane = sticks.Single(candidate =>
                candidate.controlPath == "<Gamepad>/rightStick");
            var leftHanded = m_InputSettings.Current;
            leftHanded.LeftHandedControls = true;
            Assert.That(m_InputSettings.Apply(leftHanded), Is.True);
            yield return null;
            Assert.That(((RectTransform)steering.transform).anchorMin.x,
                Is.GreaterThan(0.5f));
            Assert.That(((RectTransform)lane.transform).anchorMax.x,
                Is.LessThan(0.5f));
            Assert.That(((RectTransform)boost.transform).anchorMax.x,
                Is.LessThan(0.5f));
            Assert.That(((RectTransform)brake.transform).anchorMax.x,
                Is.LessThan(0.5f));
            Assert.That(m_Input.MovementScreenSide, Is.EqualTo(ControlScreenSide.Right));
            Assert.That(m_Input.ActionScreenSide, Is.EqualTo(ControlScreenSide.Left));
            m_Touchscreen = InputSystem.AddDevice<Touchscreen>();
            Canvas.ForceUpdateCanvases();

            var steerCenter = CenterOf(steering.transform);
            var steerRight = steerCenter + Vector2.right *
                steering.movementRange;
            QueueTouch(
                inputModule,
                m_Touchscreen,
                1,
                UnityEngine.InputSystem.TouchPhase.Began,
                steerCenter);
            QueueTouch(
                inputModule,
                m_Touchscreen,
                1,
                UnityEngine.InputSystem.TouchPhase.Moved,
                steerRight,
                steerRight - steerCenter);
            QueueTouch(
                inputModule,
                m_Touchscreen,
                2,
                UnityEngine.InputSystem.TouchPhase.Began,
                CenterOf(boost.transform));
            QueueTouch(
                inputModule,
                m_Touchscreen,
                3,
                UnityEngine.InputSystem.TouchPhase.Began,
                CenterOf(brake.transform));

            Assert.That(m_Input.ReadMove().x, Is.GreaterThan(0.75f));
            Assert.That(
                m_Input.IsCommandPressed(SemanticGameplayCommand.Primary),
                Is.True);
            Assert.That(
                m_Input.IsCommandPressed(SemanticGameplayCommand.Secondary),
                Is.True);

            yield return new WaitForFixedUpdate();
            Assert.That(lifecycle.Motor.State.Velocity.x, Is.GreaterThan(0f));

            var checkpoint = UnityEngine.Object.FindFirstObjectByType<
                FlightCheckpoint>(FindObjectsInactive.Include);
            Assert.That(checkpoint, Is.Not.Null);
            lifecycle.Motor.CaptureCheckpoint(checkpoint);
            var checkpointBeforeFailure = lifecycle.Motor.LatestCheckpoint;
            var failedProductionRoute = m_InputTransition.RouteAsync(
                "Mirra2DProof",
                CancellationToken.None).AsTask();
            yield return WaitForTask(failedProductionRoute, allowFault: true);
            Assert.That(failedProductionRoute.IsFaulted, Is.True,
                "The fixture intentionally omits surface dependencies.");
            Assert.That(
                SceneManager.GetActiveScene().name,
                Is.EqualTo("Task17FlightGraybox"));
            Assert.That(lifecycle.IsConfigured, Is.True);
            Assert.That(lifecycle.Motor.IsBound, Is.True);
            Assert.That(
                lifecycle.Motor.LatestCheckpoint,
                Is.EqualTo(checkpointBeforeFailure));
        }

        private FlightMotor2D CreateMotor()
        {
            var gameObject = new GameObject("FlightMotor");
            m_Objects.Add(gameObject);
            gameObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            return gameObject.AddComponent<FlightMotor2D>();
        }

        private T CreateComponent<T>(string name) where T : Component
        {
            var gameObject = new GameObject(name);
            m_Objects.Add(gameObject);
            return gameObject.AddComponent<T>();
        }

        private static void AssertState(FlightState actual, FlightState expected)
        {
            Assert.That(actual.Position, Is.EqualTo(expected.Position));
            Assert.That(actual.Velocity, Is.EqualTo(expected.Velocity));
            Assert.That(actual.Lane, Is.EqualTo(expected.Lane));
            Assert.That(actual.FailurePending, Is.EqualTo(expected.FailurePending));
            Assert.That(actual.LandingLocked, Is.EqualTo(expected.LandingLocked));
            Assert.That(actual.ElapsedSeconds, Is.EqualTo(expected.ElapsedSeconds));
        }

        private static IEnumerator WaitForTask(Task task, bool allowFault = false)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (!allowFault && task.IsFaulted)
            {
                throw task.Exception?.InnerException ?? task.Exception;
            }
        }

        private static Vector2 CenterOf(Transform transform)
        {
            var rect = (RectTransform)transform;
            var canvas = rect.GetComponentInParent<Canvas>();
            return RectTransformUtility.WorldToScreenPoint(
                canvas.worldCamera,
                rect.TransformPoint(rect.rect.center));
        }

        private static void QueueTouch(
            InputSystemUIInputModule inputModule,
            Touchscreen touchscreen,
            int touchId,
            UnityEngine.InputSystem.TouchPhase phase,
            Vector2 position,
            Vector2 delta = default)
        {
            InputSystem.QueueStateEvent(touchscreen, new TouchState
            {
                touchId = touchId,
                phase = phase,
                position = position,
                delta = delta,
                pressure = phase == UnityEngine.InputSystem.TouchPhase.Ended
                    ? 0f
                    : 1f,
            });
            InputSystem.Update();
            inputModule.Process();
            InputSystem.Update();
        }

        private sealed class RecordingSceneTransition : ISceneTransition
        {
            private readonly TaskCompletionSource<bool> m_Completion =
                new TaskCompletionSource<bool>();

            public int CallCount { get; private set; }
            public Exception Failure { get; set; }

            public async ValueTask RouteAsync(
                string destination,
                CancellationToken cancellationToken)
            {
                CallCount++;
                await m_Completion.Task;
                cancellationToken.ThrowIfCancellationRequested();
                if (Failure != null)
                {
                    throw Failure;
                }
            }

            public void Complete()
            {
                m_Completion.TrySetResult(true);
            }
        }

        private sealed class ReleasingSceneTransition : ISceneTransition
        {
            public Action ReleaseDuringRoute { get; set; }
            public int CallCount { get; private set; }

            public ValueTask RouteAsync(
                string destination,
                CancellationToken cancellationToken)
            {
                CallCount++;
                ReleaseDuringRoute?.Invoke();
                return default;
            }
        }

        private sealed class CancelThenSucceedSceneTransition : ISceneTransition
        {
            public int CallCount { get; private set; }

            public async ValueTask RouteAsync(
                string destination,
                CancellationToken cancellationToken)
            {
                CallCount++;
                if (CallCount == 1)
                {
                    // Keep the first attempt live until after its caller has
                    // exercised the immediate retry boundary. Cancellation of
                    // Task.Delay can otherwise resume this async method inline.
                    await Task.Yield();
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
        }
    }

    internal static class Task17TaskEnumerator
    {
        public static IEnumerator AsIEnumerator(this Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                throw task.Exception?.InnerException ?? task.Exception;
            }
        }
    }
}
