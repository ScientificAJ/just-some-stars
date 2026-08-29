using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Discovery;
using JustSomeStars.Runtime.Input;
using JustSomeStars.Runtime.Player;
using JustSomeStars.Runtime.Rendering2D;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class DiscoveryLensTests
    {
        private readonly List<UnityEngine.Object> m_OwnedObjects =
            new List<UnityEngine.Object>();
        private readonly List<IDisposable> m_OwnedDisposables =
            new List<IDisposable>();
        private string m_TestRoot;
        private SettingsService m_Settings;
        private InputActionAsset m_Actions;
        private InputRouter m_Input;
        private GameModeController m_Modes;
        private Keyboard m_Keyboard;
        private InputSettings.BackgroundBehavior m_PreviousBackgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode m_PreviousEditorBehavior;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            m_TestRoot = Path.Combine(
                Path.GetTempPath(),
                "JssTask16DiscoveryLens",
                Guid.NewGuid().ToString("N"));
            m_PreviousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            m_PreviousEditorBehavior =
                InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior =
                InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode
                    .AllDeviceInputAlwaysGoesToGameView;
            m_Keyboard = InputSystem.AddDevice<Keyboard>();
            m_Settings = new SettingsService(Path.Combine(m_TestRoot, "settings.json"));
            m_Actions = UnityEngine.Object.Instantiate(InputSystem.actions);
            m_Input = new InputRouter(m_Actions, m_Settings);
            m_Modes = GameModeController.CreateForTests(
                GameMode.Surface,
                new InputRouterGameModeRuntimeHooks(m_Input));

            var settingsTask = m_Settings.InitializeAsync(CancellationToken.None)
                .AsTask();
            yield return WaitForTask(settingsTask);
            Assert.That(settingsTask.Result.IsAvailable, Is.True);
            var inputTask = m_Input.InitializeAsync(CancellationToken.None).AsTask();
            yield return WaitForTask(inputTask);
            Assert.That(inputTask.Result.IsAvailable, Is.True);
            var modesTask = m_Modes.InitializeAsync(CancellationToken.None).AsTask();
            yield return WaitForTask(modesTask);
            Assert.That(modesTask.Result.IsAvailable, Is.True);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var ownedDisposable in m_OwnedDisposables)
            {
                ownedDisposable.Dispose();
            }
            m_OwnedDisposables.Clear();

            var modeShutdown = m_Modes.ShutdownAsync().AsTask();
            yield return WaitForTask(modeShutdown);
            var inputShutdown = m_Input.ShutdownAsync().AsTask();
            yield return WaitForTask(inputShutdown);
            var settingsShutdown = m_Settings.ShutdownAsync().AsTask();
            yield return WaitForTask(settingsShutdown);

            foreach (var ownedObject in m_OwnedObjects)
            {
                if (ownedObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(ownedObject);
                }
            }

            UnityEngine.Object.DestroyImmediate(m_Actions);
            if (m_Keyboard != null && m_Keyboard.added)
            {
                InputSystem.RemoveDevice(m_Keyboard);
            }
            InputSystem.settings.editorInputBehaviorInPlayMode =
                m_PreviousEditorBehavior;
            InputSystem.settings.backgroundBehavior =
                m_PreviousBackgroundBehavior;
            if (Directory.Exists(m_TestRoot))
            {
                Directory.Delete(m_TestRoot, recursive: true);
            }
            m_OwnedObjects.Clear();
        }

        [UnityTest]
        public IEnumerator LensCommand_OwnsSurfaceLensTransitionsAndTeardown()
        {
            var fixture = CreateFixture();
            fixture.Controller.Bind();
            Assert.That(m_Modes.CurrentMode, Is.EqualTo(GameMode.Surface));
            Assert.That(m_Input.ActiveGameplayMode,
                Is.EqualTo(GameplayInputMode.Surface));
            var routedCommands = new List<(GameplayInputMode Mode,
                SemanticGameplayCommand Command)>();
            m_Input.GameplayCommandPerformed += (mode, command) =>
                routedCommands.Add((mode, command));

            PressKeyboard(Key.L);
            Assert.That(routedCommands, Does.Contain((
                GameplayInputMode.Surface,
                SemanticGameplayCommand.Lens)));
            yield return WaitForTask(fixture.Controller.ActiveTransition);
            Assert.That(fixture.Controller.LastTransitionFailure, Is.Null);
            Assert.That(m_Modes.CurrentMode, Is.EqualTo(GameMode.Lens));
            Assert.That(m_Input.ActiveGameplayMode,
                Is.EqualTo(GameplayInputMode.Lens));
            Assert.That(fixture.Controller.IsActive, Is.True);

            ReleaseKeyboard();
            PressKeyboard(Key.L);
            yield return WaitForTask(fixture.Controller.ActiveTransition);
            Assert.That(m_Modes.CurrentMode, Is.EqualTo(GameMode.Surface));
            ReleaseKeyboard();
            PressKeyboard(Key.L);
            fixture.Controller.Dispose();
            yield return WaitForTask(fixture.Controller.ActiveTransition);
            Assert.That(m_Modes.CurrentMode, Is.EqualTo(GameMode.Surface),
                "Disposal must quiesce an in-flight Surface-to-Lens transition.");
            Assert.That(m_Input.ActiveGameplayMode,
                Is.EqualTo(GameplayInputMode.Surface));
            ReleaseKeyboard();
            PressKeyboard(Key.L);
            yield return null;
            Assert.That(m_Modes.CurrentMode, Is.EqualTo(GameMode.Surface));
        }

        [UnityTest]
        public IEnumerator FocusBehaviors_UseRegionBoundsAndTrackRetention()
        {
            var track = CreateTarget(
                "phenomenon.track",
                LayerBand.Gameplay,
                Vector2.zero,
                LensFocusBehavior.Track);
            var point = CreateTarget(
                "phenomenon.point",
                LayerBand.Gameplay,
                new Vector2(0.7f, 0f),
                LensFocusBehavior.Point);
            var region = CreateTarget(
                "phenomenon.region",
                LayerBand.Gameplay,
                new Vector2(2f, 0f),
                LensFocusBehavior.Region,
                visualWidth: 2f);
            var fixture = CreateFixture(track, point, region);
            fixture.Controller.Bind();
            yield return WaitForTask(EnterLens());
            fixture.Controller.SelectMode(LensMode.Temperature);

            fixture.Controller.Advance(0f, Vector2.zero, scanHeld: false);
            Assert.That(fixture.Controller.FocusedTarget, Is.SameAs(track));
            fixture.Controller.Advance(0.25f, Vector2.right, scanHeld: false);
            Assert.That(fixture.Controller.FocusedTarget, Is.SameAs(track),
                "Track focus must retain a moving target outside initial radius.");

            track.gameObject.SetActive(false);
            fixture.Controller.SetSelectedBand(LayerBand.Midground);
            fixture.Controller.SetSelectedBand(LayerBand.Gameplay);
            fixture.Controller.Advance(0f, Vector2.zero, scanHeld: false);
            Assert.That(fixture.Controller.FocusedTarget, Is.SameAs(region),
                "Region focus must use authored visible bounds, not only center.");
        }

        [UnityTest]
        public IEnumerator OverlappingTargets_SelectDeterministicallyByDepthThenId()
        {
            var nearB = CreateTarget(
                "phenomenon.b",
                LayerBand.Midground,
                Vector2.zero);
            var nearA = CreateTarget(
                "phenomenon.a",
                LayerBand.Midground,
                Vector2.zero);
            var gameplay = CreateTarget(
                "phenomenon.gameplay",
                LayerBand.Gameplay,
                Vector2.zero);
            var fixture = CreateFixture(nearB, gameplay, nearA);
            fixture.Controller.Bind();
            yield return WaitForTask(EnterLens());
            fixture.Controller.SelectMode(LensMode.Temperature);
            fixture.Controller.SetSelectedBand(LayerBand.Midground);

            fixture.Controller.Advance(
                0f,
                Vector2.zero,
                scanHeld: false);

            Assert.That(
                fixture.Controller.FocusedTarget.Phenomenon.StableId.Value,
                Is.EqualTo("phenomenon.a"));
            Assert.That(gameplay.IsFocused, Is.False);
            Assert.That(nearA.IsFocused, Is.True);
            Assert.That(nearB.IsFocused, Is.False);
        }

        [UnityTest]
        public IEnumerator ScanProgress_IsGatedByFocusCompatibilityAndHeldPrimary()
        {
            var target = CreateTarget(
                "phenomenon.scan",
                LayerBand.Gameplay,
                Vector2.zero);
            var fixture = CreateFixture(target);
            fixture.Controller.Bind();
            yield return WaitForTask(EnterLens());
            fixture.Controller.SelectMode(LensMode.Temperature);
            fixture.Controller.SetSelectedBand(LayerBand.Gameplay);
            fixture.Controller.SelectInstrument(fixture.ThermalInstrument);
            fixture.Controller.SelectPrediction(new Prediction(
                "prediction.scan",
                target.Phenomenon.StableId.Value,
                target.Phenomenon.CorrectHypothesisId.Value));

            fixture.Controller.Advance(
                0.25f,
                Vector2.zero,
                scanHeld: false);
            Assert.That(fixture.Controller.ScanProgress, Is.Zero);
            fixture.Controller.Advance(
                0.25f,
                Vector2.zero,
                scanHeld: true);
            Assert.That(fixture.Controller.ScanProgress, Is.EqualTo(0.5f)
                .Within(0.001f));

            fixture.Controller.SetSelectedBand(LayerBand.Midground);
            fixture.Controller.Advance(
                0.25f,
                Vector2.zero,
                scanHeld: true);
            Assert.That(fixture.Controller.ScanProgress, Is.Zero);
            Assert.That(fixture.Recorder.Records, Is.Empty);
        }

        [UnityTest]
        public IEnumerator CompletedIncorrectScan_PublishesOnceAndDoesNotRepeatWhileHeld()
        {
            var target = CreateTarget(
                "phenomenon.wrong-answer",
                LayerBand.Gameplay,
                Vector2.zero);
            var fixture = CreateFixture(target);
            fixture.Controller.Bind();
            yield return WaitForTask(EnterLens());
            fixture.Controller.SelectMode(LensMode.Temperature);
            fixture.Controller.SetSelectedBand(LayerBand.Gameplay);
            fixture.Controller.SelectInstrument(fixture.ThermalInstrument);
            fixture.Controller.SelectPrediction(new Prediction(
                "prediction.wrong-answer",
                target.Phenomenon.StableId.Value,
                "hypothesis.wrong"));

            fixture.Controller.Advance(
                0.5f,
                Vector2.zero,
                scanHeld: true);
            fixture.Controller.Advance(
                0.5f,
                Vector2.zero,
                scanHeld: true);

            Assert.That(fixture.Recorder.Records, Has.Count.EqualTo(1));
            Assert.That(fixture.Recorder.Records[0].PredictionWasCorrect, Is.False);
            Assert.That(fixture.Recorder.Records[0].MissionMayContinue, Is.True);
            Assert.That(fixture.Controller.ReticleState,
                Is.EqualTo(LensReticleState.Complete));
        }

        [UnityTest]
        public IEnumerator CyclingModes_IsExactAndNeverMovesTheOrthographicCamera()
        {
            var fixture = CreateFixture();
            fixture.Controller.Bind();
            yield return WaitForTask(EnterLens());
            var cameraTransform = fixture.Camera.transform;
            var originalPosition = cameraTransform.position;
            var originalRotation = cameraTransform.rotation;
            var observed = new List<LensMode> { fixture.Controller.SelectedMode };

            for (var index = 0; index < 6; index++)
            {
                fixture.Controller.CycleMode();
                observed.Add(fixture.Controller.SelectedMode);
            }

            Assert.That(observed, Is.EqualTo(new[]
            {
                LensMode.Imaging,
                LensMode.Spectrum,
                LensMode.Temperature,
                LensMode.Atmosphere,
                LensMode.Motion,
                LensMode.Signal,
                LensMode.Imaging,
            }));
            Assert.That(fixture.Camera.orthographic, Is.True);
            Assert.That(cameraTransform.position, Is.EqualTo(originalPosition));
            Assert.That(cameraTransform.rotation, Is.EqualTo(originalRotation));
        }

        private LensFixture CreateFixture(params DiscoveryLensTarget2D[] targets)
        {
            var cameraObject = new GameObject("LensCamera");
            m_OwnedObjects.Add(cameraObject);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            var events = new GameEventBus();
            var recorder = new EvidenceRecorder(events);
            var thermal = CreateInstrument(
                "instrument.thermal-imager",
                LensMode.Temperature);
            var controller = new DiscoveryLensController(
                m_Input,
                m_Modes,
                m_Settings,
                recorder,
                camera,
                () => Vector2.zero,
                targets ?? Array.Empty<DiscoveryLensTarget2D>(),
                new[] { thermal });
            m_OwnedDisposables.Add(controller);
            return new LensFixture(controller, recorder, thermal, camera);
        }

        private DiscoveryLensTarget2D CreateTarget(
            string id,
            LayerBand band,
            Vector2 position,
            LensFocusBehavior behavior = LensFocusBehavior.Point,
            float visualWidth = 0f)
        {
            var phenomenon = ScriptableObject.CreateInstance<PhenomenonDefinition>();
            m_OwnedObjects.Add(phenomenon);
            phenomenon.Configure(
                id,
                "science-source." + id,
                band,
                behavior,
                new[] { LensMode.Temperature },
                "hypothesis." + id,
                "hint." + id,
                "detail." + id,
                0.75f);
            var root = new GameObject(id);
            m_OwnedObjects.Add(root);
            root.transform.position = position;
            var target = root.AddComponent<DiscoveryLensTarget2D>();
            target.Configure(phenomenon);
            if (visualWidth > 0f)
            {
                var texture = new Texture2D(4, 4);
                var sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, 4f, 4f),
                    new Vector2(0.5f, 0.5f),
                    4f);
                m_OwnedObjects.Add(texture);
                m_OwnedObjects.Add(sprite);
                var renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.transform.localScale = new Vector3(
                    visualWidth,
                    1f,
                    1f);
            }
            return target;
        }

        private InstrumentDefinition CreateInstrument(
            string id,
            params LensMode[] modes)
        {
            var definition = ScriptableObject.CreateInstance<InstrumentDefinition>();
            m_OwnedObjects.Add(definition);
            definition.Configure(id, modes, 0.5f);
            return definition;
        }

        private void PressKeyboard(Key key)
        {
            InputSystem.QueueStateEvent(m_Keyboard, new KeyboardState(key));
            InputSystem.Update();
        }

        private Task EnterLens()
        {
            return m_Modes.EnterAsync(GameMode.Lens, CancellationToken.None)
                .AsTask();
        }

        private static void ReleaseKeyboard()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            InputSystem.Update();
        }

        private static IEnumerator WaitForTask(Task task)
        {
            while (task != null && !task.IsCompleted)
            {
                yield return null;
            }

            if (task != null && task.IsFaulted)
            {
                throw task.Exception;
            }
        }

        private sealed class LensFixture
        {
            public LensFixture(
                DiscoveryLensController controller,
                EvidenceRecorder recorder,
                InstrumentDefinition thermalInstrument,
                Camera camera)
            {
                Controller = controller;
                Recorder = recorder;
                ThermalInstrument = thermalInstrument;
                Camera = camera;
            }

            public DiscoveryLensController Controller { get; }
            public EvidenceRecorder Recorder { get; }
            public InstrumentDefinition ThermalInstrument { get; }
            public Camera Camera { get; }
        }
    }
}
