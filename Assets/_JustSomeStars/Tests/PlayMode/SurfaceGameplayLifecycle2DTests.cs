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
using JustSomeStars.Runtime.Discovery;
using JustSomeStars.Runtime.Input;
using JustSomeStars.Runtime.Player;
using JustSomeStars.Runtime.UI;
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
    public sealed class SurfaceGameplayLifecycle2DTests
    {
        private string m_OnScreenTestRoot;
        private SettingsService m_OnScreenSettings;
        private InputActionAsset m_OnScreenActions;
        private InputRouter m_OnScreenInput;
        private GameModeController m_OnScreenModes;
        private UnitySceneTransition m_OnScreenTransition;
        private Touchscreen m_OnScreenTouchscreen;
        private InputSettings.BackgroundBehavior m_PreviousBackgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode
            m_PreviousEditorBehavior;

        [UnityTearDown]
        public IEnumerator TearDownOnScreenRoute()
        {
            if (m_OnScreenSettings == null)
            {
                yield break;
            }

            m_OnScreenTransition?.ReleaseBindings();
            if (m_OnScreenTouchscreen != null && m_OnScreenTouchscreen.added)
            {
                InputSystem.RemoveDevice(m_OnScreenTouchscreen);
            }
            InputSystem.settings.editorInputBehaviorInPlayMode =
                m_PreviousEditorBehavior;
            InputSystem.settings.backgroundBehavior =
                m_PreviousBackgroundBehavior;

            var recovery = SceneManager.CreateScene(
                "Task13OnScreenInputRecovery_" + Guid.NewGuid().ToString("N"));
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

            var modeShutdown = m_OnScreenModes.ShutdownAsync().AsTask();
            yield return WaitForTask(modeShutdown);
            var inputShutdown = m_OnScreenInput.ShutdownAsync().AsTask();
            yield return WaitForTask(inputShutdown);
            var settingsShutdown = m_OnScreenSettings.ShutdownAsync().AsTask();
            yield return WaitForTask(settingsShutdown);
            UnityEngine.Object.DestroyImmediate(m_OnScreenActions);
            if (Directory.Exists(m_OnScreenTestRoot))
            {
                Directory.Delete(m_OnScreenTestRoot, recursive: true);
            }

            m_OnScreenTransition = null;
            m_OnScreenTouchscreen = null;
            m_OnScreenModes = null;
            m_OnScreenInput = null;
            m_OnScreenActions = null;
            m_OnScreenSettings = null;
            m_OnScreenTestRoot = null;
        }

        [UnityTest]
        public IEnumerator ProductionMirraScene_OnScreenStickDrivesTheBoundMotor()
        {
            m_OnScreenTestRoot = Path.Combine(
                Path.GetTempPath(),
                "JssTask13OnScreenInput",
                Guid.NewGuid().ToString("N"));
            m_OnScreenSettings = new SettingsService(Path.Combine(
                m_OnScreenTestRoot,
                "settings.json"));
            m_OnScreenActions = UnityEngine.Object.Instantiate(InputSystem.actions);
            m_OnScreenInput = new InputRouter(
                m_OnScreenActions,
                m_OnScreenSettings);
            var hooks = new InputRouterGameModeRuntimeHooks(m_OnScreenInput);
            m_OnScreenModes = GameModeController.CreateForTests(
                GameMode.Surface,
                hooks);
            m_OnScreenTransition = new UnitySceneTransition(
                new FrontendDependencies(m_OnScreenSettings, m_OnScreenInput),
                new SurfaceGameplayDependencies(
                    m_OnScreenSettings,
                    m_OnScreenInput,
                    m_OnScreenModes,
                    new GameEventBus()));
            m_PreviousBackgroundBehavior =
                InputSystem.settings.backgroundBehavior;
            m_PreviousEditorBehavior =
                InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior =
                InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode
                    .AllDeviceInputAlwaysGoesToGameView;

            var settingsTask = m_OnScreenSettings
                .InitializeAsync(CancellationToken.None)
                .AsTask();
            yield return WaitForTask(settingsTask);
            Assert.That(settingsTask.Result.IsAvailable, Is.True);

            var inputTask = m_OnScreenInput
                .InitializeAsync(CancellationToken.None)
                .AsTask();
            yield return WaitForTask(inputTask);
            Assert.That(inputTask.Result.IsAvailable, Is.True);

            var modeTask = m_OnScreenModes
                .InitializeAsync(CancellationToken.None)
                .AsTask();
            yield return WaitForTask(modeTask);
            Assert.That(modeTask.Result.IsAvailable, Is.True);
            Assert.That(
                m_OnScreenInput.ActiveGameplayMode,
                Is.EqualTo(GameplayInputMode.Surface));

            var sceneLoad = SceneManager.LoadSceneAsync(
                "Mirra2DProof",
                LoadSceneMode.Single);
            Assert.That(sceneLoad, Is.Not.Null);
            yield return sceneLoad;
            yield return null;

            m_OnScreenTransition.BindActiveScene();
            var lifecycle = UnityEngine.Object.FindFirstObjectByType<
                JustSomeStars.Runtime.Player.SurfaceGameplayLifecycle2D>(
                FindObjectsInactive.Include);
            Assert.That(lifecycle, Is.Not.Null);
            Assert.That(lifecycle.IsConfigured, Is.True);
            var instruments = ReadField<InstrumentDefinition[]>(
                lifecycle,
                "lensInstruments");
            Assert.That(instruments, Has.Length.EqualTo(1));
            Assert.That(instruments[0], Is.Not.Null,
                "The production Mirra scene must bind its thermal instrument.");
            Assert.That(instruments[0].StableId.Value,
                Is.EqualTo("instrument.mirra.thermal-imager"));
            var predictions = ReadField<Prediction[]>(lifecycle, "lensPredictions");
            Assert.That(predictions, Has.Length.EqualTo(1));
            Assert.That(predictions[0], Is.Not.Null);
            Assert.That(predictions[0].StableId.Value,
                Is.EqualTo("prediction.mirra.day-night-circulation"));
            Assert.That(predictions[0].PhenomenonId.Value,
                Is.EqualTo("phenomenon.mirra.temperature-gradient"));
            Assert.That(predictions[0].HypothesisId.Value,
                Is.EqualTo("hypothesis.mirra.day-night-circulation"));

            var motor = ReadField<Component>(lifecycle, "motor");
            var body = ReadField<Rigidbody2D>(lifecycle, "targetBody");
            var stick = UnityEngine.Object.FindFirstObjectByType<OnScreenStick>(
                FindObjectsInactive.Exclude);
            Assert.That(stick, Is.Not.Null);
            Assert.That(stick.control, Is.Not.Null);
            Assert.That(stick.control.path, Does.EndWith("/leftStick"));
            var inputModule = EventSystem.current.currentInputModule as
                InputSystemUIInputModule;
            Assert.That(
                inputModule,
                Is.Not.Null,
                "The production EventSystem must route actual touch events through " +
                "InputSystemUIInputModule.");
            m_OnScreenTouchscreen = InputSystem.AddDevice<Touchscreen>();

            var rect = (RectTransform)stick.transform;
            Canvas.ForceUpdateCanvases();
            var canvas = rect.GetComponentInParent<Canvas>();
            var center = RectTransformUtility.WorldToScreenPoint(
                canvas.worldCamera,
                rect.TransformPoint(rect.rect.center));
            var raycastResults = new List<RaycastResult>();
            EventSystem.current.RaycastAll(
                new PointerEventData(EventSystem.current) { position = center },
                raycastResults);
            Assert.That(
                raycastResults.Any(result => result.gameObject == stick.gameObject),
                Is.True,
                $"TouchMove must be raycastable at {center}; hits were: " +
                string.Join(", ", raycastResults.Select(result =>
                    result.gameObject.name)));
            var pointerDownReceived = false;
            var dragReceived = false;
            var eventTrigger = stick.gameObject.AddComponent<EventTrigger>();
            eventTrigger.triggers = new List<EventTrigger.Entry>
            {
                CreateTrigger(EventTriggerType.PointerDown, _ =>
                    pointerDownReceived = true),
                CreateTrigger(EventTriggerType.Drag, _ => dragReceived = true),
            };
            QueueTouch(
                inputModule,
                m_OnScreenTouchscreen,
                UnityEngine.InputSystem.TouchPhase.Began,
                center);
            Assert.That(
                m_OnScreenTouchscreen.primaryTouch.press.isPressed,
                Is.True,
                "The queued real touchscreen press must reach InputSystem.");
            Assert.That(
                pointerDownReceived,
                Is.True,
                "The production UI module must dispatch PointerDown to TouchMove.");
            var draggedPosition = center + Vector2.right * stick.movementRange;
            QueueTouch(
                inputModule,
                m_OnScreenTouchscreen,
                UnityEngine.InputSystem.TouchPhase.Moved,
                draggedPosition,
                draggedPosition - center);
            Assert.That(
                dragReceived,
                Is.True,
                "The production UI module must dispatch Drag to TouchMove.");

            Assert.That(
                m_OnScreenInput.ReadMove().x,
                Is.GreaterThan(0.9f),
                "The production on-screen Gamepad must feed the composition-owned " +
                "Surface action map.");
            body.linearVelocity = Vector2.zero;
            InvokeAny(motor, "FixedUpdate");
            Assert.That(
                body.linearVelocity.x,
                Is.GreaterThan(0f),
                "The bound production motor must consume the on-screen stick value.");

            QueueTouch(
                inputModule,
                m_OnScreenTouchscreen,
                UnityEngine.InputSystem.TouchPhase.Ended,
                draggedPosition);

            var buttons = UnityEngine.Object.FindObjectsByType<OnScreenButton>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            Assert.That(buttons, Has.Length.EqualTo(3));
            var jumpButton = buttons.Single(candidate =>
                candidate.controlPath == "<Gamepad>/buttonWest");
            var interactButton = buttons.Single(candidate =>
                candidate.controlPath == "<Gamepad>/buttonSouth");
            var lensButton = buttons.Single(candidate =>
                candidate.controlPath == "<Gamepad>/buttonNorth");

            var jumpCenter = CenterOf(jumpButton.transform);
            Press(inputModule, m_OnScreenTouchscreen, jumpCenter);
            Assert.That(
                ReadField<bool>(motor, "jumpRequested"),
                Is.True,
                "The production Jump control must reach the bound motor.");
            Release(inputModule, m_OnScreenTouchscreen, jumpCenter);

            var interaction = UnityEngine.Object.FindFirstObjectByType<
                SurfaceInteractionProbe2D>(FindObjectsInactive.Exclude);
            Assert.That(interaction, Is.Not.Null);
            var interactionCollider = interaction.GetComponent<Collider2D>();
            body.position = interactionCollider.bounds.center;
            body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Assert.That(
                interaction.IsAvailable,
                Is.True,
                "The real Captain must activate the production interaction trigger.");
            var interactCenter = CenterOf(interactButton.transform);
            Press(inputModule, m_OnScreenTouchscreen, interactCenter);
            Assert.That(interaction.IsActivated, Is.True);
            var interactionLabel = ReadField<TMPro.TMP_Text>(interaction, "label");
            Assert.That(interactionLabel.text, Is.EqualTo("SIGNAL LINKED"));
            Release(inputModule, m_OnScreenTouchscreen, interactCenter);

            var lensTarget = UnityEngine.Object.FindFirstObjectByType<
                DiscoveryLensTarget2D>(FindObjectsInactive.Exclude);
            Assert.That(lensTarget, Is.Not.Null);
            Assert.That(lensTarget.IsFocused, Is.False);
            body.position = lensTarget.transform.position;
            body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            var lensCenter = CenterOf(lensButton.transform);
            Press(inputModule, m_OnScreenTouchscreen, lensCenter);
            yield return WaitForTask(lifecycle.LensController.ActiveTransition);
            Assert.That(m_OnScreenModes.CurrentMode, Is.EqualTo(GameMode.Lens));
            Assert.That(m_OnScreenInput.ActiveGameplayMode,
                Is.EqualTo(GameplayInputMode.Lens));
            Assert.That(lifecycle.LensController.SelectedBand,
                Is.EqualTo(JustSomeStars.Runtime.Rendering2D.LayerBand.Midground),
                "The production Lens must start on the authored target band.");
            lifecycle.LensController.Advance(0f, Vector2.zero, scanHeld: false);
            Assert.That(lensTarget.IsFocused, Is.True,
                "The production Mirra target must be focusable from the real body origin.");
            Release(inputModule, m_OnScreenTouchscreen, lensCenter);

            var lensAim = UnityEngine.Object.FindObjectsByType<OnScreenStick>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Single(candidate =>
                    candidate.controlPath == "<Gamepad>/rightStick");
            Assert.That(lensAim.gameObject.activeInHierarchy, Is.True);
            Assert.That(stick.gameObject.activeInHierarchy, Is.False);
            Assert.That(jumpButton.gameObject.activeInHierarchy, Is.False);
            Assert.That(interactButton.gameObject.activeInHierarchy, Is.False);
            Assert.That(lensButton.gameObject.activeInHierarchy, Is.True);

            var allButtons = UnityEngine.Object.FindObjectsByType<OnScreenButton>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            var lensModeButton = allButtons.Single(candidate =>
                candidate.name == "TouchLensMode");
            var lensScanButton = allButtons.Single(candidate =>
                candidate.name == "TouchLensScan");
            Assert.That(lensModeButton.controlPath,
                Is.EqualTo("<Gamepad>/buttonWest"));
            Assert.That(lensScanButton.controlPath,
                Is.EqualTo("<Gamepad>/buttonSouth"));
            Assert.That(lensModeButton.gameObject.activeInHierarchy, Is.True);
            Assert.That(lensScanButton.gameObject.activeInHierarchy, Is.True);
            var reticle = UnityEngine.Object.FindObjectsByType<Transform>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Single(candidate => candidate.name == "TouchLensReticle");
            var progress = UnityEngine.Object.FindObjectsByType<Transform>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Single(candidate => candidate.name == "TouchLensProgress")
                .GetComponent<UnityEngine.UI.Image>();
            var status = UnityEngine.Object.FindObjectsByType<Transform>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Single(candidate => candidate.name == "TouchLensStatus")
                .GetComponent<TMPro.TMP_Text>();
            InvokeAny(ReadField<Component>(lifecycle, "lensPresenter"), "LateUpdate");
            Assert.That(reticle.gameObject.activeInHierarchy, Is.True);
            Assert.That(progress, Is.Not.Null);
            Assert.That(status, Is.Not.Null);
            Assert.That(status.text, Is.EqualTo("INCOMPATIBLE"));
            var lensModeLabel = lensModeButton.GetComponentInChildren<
                TMPro.TMP_Text>(true);
            var lensScanLabel = lensScanButton.GetComponentInChildren<
                TMPro.TMP_Text>(true);
            Assert.That(lensModeLabel, Is.Not.Null);
            Assert.That(lensScanLabel, Is.Not.Null);
            Assert.That(lensModeLabel.text, Is.EqualTo("MODE"));
            Assert.That(lensScanLabel.text, Is.EqualTo("SCAN"));

            var lensModeCenter = CenterOf(lensModeButton.transform);
            Press(inputModule, m_OnScreenTouchscreen, lensModeCenter);
            Assert.That(lifecycle.LensController.SelectedMode,
                Is.EqualTo(LensMode.Spectrum));
            Release(inputModule, m_OnScreenTouchscreen, lensModeCenter);
            Press(inputModule, m_OnScreenTouchscreen, lensModeCenter);
            Assert.That(lifecycle.LensController.SelectedMode,
                Is.EqualTo(LensMode.Temperature));
            Release(inputModule, m_OnScreenTouchscreen, lensModeCenter);
            lifecycle.LensController.Advance(0f, Vector2.zero, scanHeld: false);
            Assert.That(lifecycle.LensController.ReticleState,
                Is.EqualTo(LensReticleState.Focused));
            InvokeAny(ReadField<Component>(lifecycle, "lensPresenter"), "LateUpdate");
            Assert.That(status.text, Is.EqualTo("FOCUS"));

            var aimCenter = CenterOf(lensAim.transform);
            Press(inputModule, m_OnScreenTouchscreen, aimCenter);
            var aimDraggedPosition = aimCenter +
                Vector2.right * lensAim.movementRange;
            QueueTouch(
                inputModule,
                m_OnScreenTouchscreen,
                UnityEngine.InputSystem.TouchPhase.Moved,
                aimDraggedPosition,
                aimDraggedPosition - aimCenter);
            Assert.That(m_OnScreenInput.ReadLook().x, Is.GreaterThan(0.9f),
                "The production Lens stick must feed semantic Look.");
            Release(inputModule, m_OnScreenTouchscreen, aimDraggedPosition);

            lifecycle.LensController.Advance(0f, Vector2.left, scanHeld: false);
            var scanCenter = CenterOf(lensScanButton.transform);
            Press(inputModule, m_OnScreenTouchscreen, scanCenter);
            Assert.That(m_OnScreenInput.IsCommandPressed(
                SemanticGameplayCommand.Primary), Is.True);
            lifecycle.LensController.Advance(
                instruments[0].ScanDurationSeconds,
                Vector2.zero,
                m_OnScreenInput.IsCommandPressed(SemanticGameplayCommand.Primary));
            InvokeAny(ReadField<Component>(lifecycle, "lensPresenter"), "LateUpdate");
            Assert.That(lifecycle.LensController.LastEvidence, Is.Not.Null,
                "The real Mirra SCAN control must publish evidence.");
            Assert.That(lifecycle.LensController.LastEvidence.PredictionWasCorrect,
                Is.True);
            Assert.That(lifecycle.LensController.ReticleState,
                Is.EqualTo(LensReticleState.Complete));
            Assert.That(progress.fillAmount, Is.EqualTo(1f).Within(0.001f));
            Assert.That(status.text, Is.EqualTo("RECORDED"));
            Release(inputModule, m_OnScreenTouchscreen, scanCenter);

            Press(inputModule, m_OnScreenTouchscreen, lensCenter);
            yield return WaitForTask(lifecycle.LensController.ActiveTransition);
            Assert.That(m_OnScreenModes.CurrentMode, Is.EqualTo(GameMode.Surface));
            Assert.That(m_OnScreenInput.ActiveGameplayMode,
                Is.EqualTo(GameplayInputMode.Surface));
            Release(inputModule, m_OnScreenTouchscreen, lensCenter);
            Assert.That(lensAim.gameObject.activeInHierarchy, Is.False);
            Assert.That(lensModeButton.gameObject.activeInHierarchy, Is.False);
            Assert.That(lensScanButton.gameObject.activeInHierarchy, Is.False);
            Assert.That(reticle.gameObject.activeInHierarchy, Is.False);
            Assert.That(stick.gameObject.activeInHierarchy, Is.True);
            Assert.That(jumpButton.gameObject.activeInHierarchy, Is.True);
            Assert.That(interactButton.gameObject.activeInHierarchy, Is.True);
        }

        [Test]
        public async Task ProductionSceneBinding_DrivesInputSettingsModeAndLookAhead()
        {
            var testRoot = Path.Combine(
                Path.GetTempPath(),
                "JssTask12Stage1SurfaceBindings",
                Guid.NewGuid().ToString("N"));
            var settings = new SettingsService(Path.Combine(
                testRoot,
                "settings.json"));
            var actions = UnityEngine.Object.Instantiate(InputSystem.actions);
            var input = new InputRouter(actions, settings);
            var hooks = new InputRouterGameModeRuntimeHooks(input);
            var modes = GameModeController.CreateForTests(GameMode.Surface, hooks);
            var previousBackgroundBehavior =
                InputSystem.settings.backgroundBehavior;
            var previousEditorBehavior =
                InputSystem.settings.editorInputBehaviorInPlayMode;
            GameObject root = null;
            ScriptableObject config = null;
            Keyboard keyboard = null;
            UnitySceneTransition transition = null;
            try
            {
                InputSystem.settings.backgroundBehavior =
                    InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode =
                    InputSettings.EditorInputBehaviorInPlayMode
                        .AllDeviceInputAlwaysGoesToGameView;
                Assert.That(
                    (await settings.InitializeAsync(CancellationToken.None))
                    .IsAvailable,
                    Is.True);
                Assert.That(
                    (await input.InitializeAsync(CancellationToken.None))
                    .IsAvailable,
                    Is.True);
                Assert.That(
                    (await modes.InitializeAsync(CancellationToken.None))
                    .IsAvailable,
                    Is.True);

                root = new GameObject("SurfaceGameplayLifecycleFixture");
                var body = root.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                body.freezeRotation = true;
                var collider = root.AddComponent<CapsuleCollider2D>();
                config = Stage1RuntimeReflection.CreateConfig(
                    "JustSomeStars.Runtime.Player.SurfaceMotor2DConfig");
                Stage1RuntimeReflection.Set(config, "MoveSpeed", 5f);
                Stage1RuntimeReflection.Set(config, "GroundAcceleration", 20f);
                Stage1RuntimeReflection.Set(config, "AirAcceleration", 10f);
                Stage1RuntimeReflection.Set(config, "GroundDeceleration", 24f);
                Stage1RuntimeReflection.Set(config, "JumpVelocity", 7f);
                Stage1RuntimeReflection.Set(config, "JetAcceleration", 12f);
                Stage1RuntimeReflection.Set(config, "JetDuration", 0.35f);
                Stage1RuntimeReflection.Set(config, "GroundProbeDistance", 0.1f);
                Stage1RuntimeReflection.Set(config, "MaxFallSpeed", 18f);
                var motor = Stage1RuntimeReflection.AddComponent(
                    root,
                    "JustSomeStars.Runtime.Player.SurfaceMotor2D");
                Stage1RuntimeReflection.Invoke(
                    motor,
                    "Configure",
                    body,
                    collider,
                    config);

                var cameraRoot = new GameObject("CompositionCamera");
                cameraRoot.transform.SetParent(root.transform, false);
                var camera = cameraRoot.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 4f;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                var compositionCamera = Stage1RuntimeReflection.AddComponent(
                    cameraRoot,
                    "JustSomeStars.Runtime.Player.CompositionCamera2D");
                Stage1RuntimeReflection.Invoke(
                    compositionCamera,
                    "Configure",
                    camera,
                    root.transform,
                    new Bounds(Vector3.zero, new Vector3(20f, 12f, 1f)),
                    new Vector2(2f, 1f),
                    2f,
                    0f,
                    new Vector2(3f, 6f));

                var lifecycleType = Stage1RuntimeReflection.RequireType(
                    "JustSomeStars.Runtime.Player.SurfaceGameplayLifecycle2D");
                var lifecycle = root.AddComponent(lifecycleType);
                SetField(lifecycle, "motor", motor);
                SetField(lifecycle, "compositionCamera", compositionCamera);
                SetField(lifecycle, "targetBody", body);

                var dependenciesType = Stage1RuntimeReflection.RequireType(
                    "JustSomeStars.Runtime.Player.SurfaceGameplayDependencies");
                var surfaceDependencies = Activator.CreateInstance(
                    dependenciesType,
                    settings,
                    input,
                    modes,
                    new GameEventBus());
                var constructor = typeof(UnitySceneTransition).GetConstructors()
                    .SingleOrDefault(candidate =>
                    {
                        var parameters = candidate.GetParameters();
                        return parameters.Length == 2 &&
                            parameters[0].ParameterType ==
                            typeof(FrontendDependencies) &&
                            parameters[1].ParameterType == dependenciesType;
                    });
                Assert.That(constructor, Is.Not.Null,
                    "UnitySceneTransition must own the production surface binder.");
                transition = (UnitySceneTransition)constructor.Invoke(new[]
                {
                    new FrontendDependencies(settings, input),
                    surfaceDependencies,
                });
                InvokeAny(transition, "BindActiveScene");

                Assert.That(
                    ReadProperty<bool>(lifecycle, "IsConfigured"),
                    Is.True);
                Assert.That(
                    Stage1RuntimeReflection.Read<GameCameraPolicy>(
                        compositionCamera,
                        "CurrentPolicy"),
                    Is.EqualTo(GameCameraPolicy.Surface));

                keyboard = InputSystem.AddDevice<Keyboard>();
                InputSystem.QueueStateEvent(
                    keyboard,
                    new KeyboardState(Key.D, Key.LeftShift));
                InputSystem.Update();
                InvokeAny(motor, "FixedUpdate");
                Assert.That(body.linearVelocity.x, Is.GreaterThan(0f));
                Assert.That(
                    Stage1RuntimeReflection.Read<float>(
                        motor,
                        "RemainingJetSeconds"),
                    Is.LessThan(0.35f),
                    "The held Secondary action must continuously drive jet assist.");

                body.linearVelocity = new Vector2(3f, 1f);
                InvokeAny(lifecycle, "Update");
                Assert.That(
                    ReadField<Vector2>(compositionCamera, "targetVelocity"),
                    Is.EqualTo(body.linearVelocity));

                var changed = settings.Current;
                changed.ReducedMotion = true;
                Assert.That(settings.Apply(changed), Is.True);
                Assert.That(
                    Stage1RuntimeReflection.Read<float>(
                        compositionCamera,
                        "EffectiveLookAhead"),
                    Is.EqualTo(0f));

                await modes.OpenOverlayAsync(
                    GameOverlay.Settings,
                    CancellationToken.None);
                Assert.That(
                    Stage1RuntimeReflection.Read<GameCameraPolicy>(
                        compositionCamera,
                        "CurrentPolicy"),
                    Is.EqualTo(GameCameraPolicy.Settings));
                await modes.CloseOverlayAsync(CancellationToken.None);
                Assert.That(
                    Stage1RuntimeReflection.Read<GameCameraPolicy>(
                        compositionCamera,
                        "CurrentPolicy"),
                    Is.EqualTo(GameCameraPolicy.Surface));

                transition.ReleaseBindings();
                Assert.That(
                    ReadProperty<bool>(lifecycle, "IsConfigured"),
                    Is.False);
            }
            finally
            {
                InputSystem.settings.editorInputBehaviorInPlayMode =
                    previousEditorBehavior;
                InputSystem.settings.backgroundBehavior =
                    previousBackgroundBehavior;
                transition?.ReleaseBindings();
                if (keyboard != null && keyboard.added)
                {
                    InputSystem.RemoveDevice(keyboard);
                }

                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }

                if (config != null)
                {
                    UnityEngine.Object.DestroyImmediate(config);
                }

                await modes.ShutdownAsync();
                await input.ShutdownAsync();
                await settings.ShutdownAsync();
                UnityEngine.Object.DestroyImmediate(actions);
                if (Directory.Exists(testRoot))
                {
                    Directory.Delete(testRoot, recursive: true);
                }
            }
        }

        private static void InvokeAny(object target, string methodName)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, Array.Empty<object>());
        }

        private static IEnumerator WaitForTask(Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                throw task.Exception;
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

        private static EventTrigger.Entry CreateTrigger(
            EventTriggerType eventId,
            UnityEngine.Events.UnityAction<BaseEventData> callback)
        {
            var entry = new EventTrigger.Entry { eventID = eventId };
            entry.callback.AddListener(callback);
            return entry;
        }

        private static void Press(
            InputSystemUIInputModule inputModule,
            Touchscreen touchscreen,
            Vector2 position)
        {
            QueueTouch(
                inputModule,
                touchscreen,
                UnityEngine.InputSystem.TouchPhase.Began,
                position);
        }

        private static void Release(
            InputSystemUIInputModule inputModule,
            Touchscreen touchscreen,
            Vector2 position)
        {
            QueueTouch(
                inputModule,
                touchscreen,
                UnityEngine.InputSystem.TouchPhase.Ended,
                position);
        }

        private static void QueueTouch(
            InputSystemUIInputModule inputModule,
            Touchscreen touchscreen,
            UnityEngine.InputSystem.TouchPhase phase,
            Vector2 position,
            Vector2 delta = default)
        {
            InputSystem.QueueStateEvent(touchscreen, new TouchState
            {
                touchId = 1,
                phase = phase,
                position = position,
                delta = delta,
                pressure = phase == UnityEngine.InputSystem.TouchPhase.Ended
                    ? 0f
                    : 1f,
            });
            InputSystem.Update();
            inputModule.Process();
            // OnScreenControl queues its virtual-Gamepad event while the UI module
            // dispatches the pointer event. Process that queued event before the
            // production router is sampled, matching the following player frame.
            InputSystem.Update();
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static T ReadField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(target);
        }

        private static T ReadProperty<T>(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(target);
        }
    }
}
