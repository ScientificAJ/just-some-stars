using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class InputRouterTests
    {
        private static readonly string[] GameplayMapNames =
        {
            "Surface",
            "Flight",
            "Lens",
        };

        private string m_TestRoot;
        private InputSettings.BackgroundBehavior m_PreviousBackgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode m_PreviousEditorBehavior;

        [SetUp]
        public void SetUp()
        {
            m_TestRoot = Path.Combine(
                Path.GetTempPath(),
                "JssTask6InputRouterTests",
                Guid.NewGuid().ToString("N"));
            m_PreviousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            m_PreviousEditorBehavior =
                InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior =
                InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode
                    .AllDeviceInputAlwaysGoesToGameView;
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                InputSystem.settings.editorInputBehaviorInPlayMode =
                    m_PreviousEditorBehavior;
            }
            finally
            {
                InputSystem.settings.backgroundBehavior =
                    m_PreviousBackgroundBehavior;
                if (Directory.Exists(m_TestRoot))
                {
                    Directory.Delete(m_TestRoot, recursive: true);
                }
            }
        }

        [Test]
        public async Task Initialize_EnablesOnlyUiAndKeepsExactAssetIdentity()
        {
            var fixture = await CreateFixture();
            try
            {
                var result = await fixture.Router.InitializeAsync(
                    CancellationToken.None);

                Assert.That(result.IsAvailable, Is.True);
                Assert.That(fixture.Router.IsInitialized, Is.True);
                Assert.That(fixture.Router.Actions, Is.SameAs(fixture.Actions));
                Assert.That(fixture.Actions.FindActionMap("UI").enabled, Is.True);
                Assert.That(fixture.Router.ActiveGameplayMode,
                    Is.EqualTo(GameplayInputMode.None));
                Assert.That(
                    GameplayMapNames.Select(name =>
                        fixture.Actions.FindActionMap(name).enabled),
                    Is.All.False);
                Assert.That(fixture.Router.ReadMove(), Is.EqualTo(Vector2.zero));
                Assert.That(fixture.Router.ReadLook(), Is.EqualTo(Vector2.zero));
            }
            finally
            {
                await fixture.DisposeAsync();
            }
        }

        [Test]
        public async Task GameplayModeSwitch_DisablesPriorMapBeforePublishingNewCommands()
        {
            var fixture = await CreateFixture();
            Keyboard keyboard = null;
            try
            {
                await fixture.Router.InitializeAsync(CancellationToken.None);
                keyboard = InputSystem.AddDevice<Keyboard>();
                var commands = new List<string>();
                fixture.Router.GameplayCommandPerformed += (mode, command) =>
                    commands.Add($"{mode}:{command}");

                fixture.Router.SetGameplayMode(GameplayInputMode.Surface);
                AssertOnlyGameplayMapEnabled(fixture.Actions, "Surface");
                PressAndRelease(keyboard, Key.Space);
                Assert.That(commands, Is.EqualTo(new[] { "Surface:Primary" }));

                fixture.Router.SetGameplayMode(GameplayInputMode.Flight);
                AssertOnlyGameplayMapEnabled(fixture.Actions, "Flight");
                PressAndRelease(keyboard, Key.Space);
                Assert.That(commands, Is.EqualTo(new[]
                {
                    "Surface:Primary",
                    "Flight:Primary",
                }));

                fixture.Router.SetGameplayMode(GameplayInputMode.None);
                AssertOnlyGameplayMapEnabled(fixture.Actions, enabledMap: null);
                PressAndRelease(keyboard, Key.Space);
                Assert.That(commands, Has.Count.EqualTo(2));
            }
            finally
            {
                if (keyboard != null && keyboard.added)
                {
                    InputSystem.RemoveDevice(keyboard);
                }

                await fixture.DisposeAsync();
            }
        }

        [Test]
        public async Task Cancel_PublishesExactlyOnceAcrossShutdownAndReinitialize()
        {
            var fixture = await CreateFixture();
            Keyboard keyboard = null;
            try
            {
                await fixture.Router.InitializeAsync(CancellationToken.None);
                keyboard = InputSystem.AddDevice<Keyboard>();
                var backCount = 0;
                fixture.Router.BackRequested += () => backCount++;

                PressAndRelease(keyboard, Key.Escape);
                Assert.That(backCount, Is.EqualTo(1));

                await fixture.Router.ShutdownAsync();
                Assert.That(fixture.Actions.FindActionMap("UI").enabled, Is.False);
                PressAndRelease(keyboard, Key.Escape);
                Assert.That(backCount, Is.EqualTo(1));

                await fixture.Router.InitializeAsync(CancellationToken.None);
                PressAndRelease(keyboard, Key.Escape);
                Assert.That(backCount, Is.EqualTo(2));
            }
            finally
            {
                if (keyboard != null && keyboard.added)
                {
                    InputSystem.RemoveDevice(keyboard);
                }

                await fixture.DisposeAsync();
            }
        }

        [Test]
        public async Task LeftHandedSetting_SwapsPublishedSidesWithoutRenamingActions()
        {
            var fixture = await CreateFixture();
            try
            {
                await fixture.Router.InitializeAsync(CancellationToken.None);
                var originalActionNames = fixture.Actions.actionMaps
                    .SelectMany(map => map.actions)
                    .Select(action => $"{action.actionMap.name}/{action.name}")
                    .ToArray();
                var changes = 0;
                fixture.Router.ControlLayoutChanged += (_, _) => changes++;

                Assert.That(fixture.Router.MovementScreenSide,
                    Is.EqualTo(ControlScreenSide.Left));
                Assert.That(fixture.Router.ActionScreenSide,
                    Is.EqualTo(ControlScreenSide.Right));

                var settings = fixture.Settings.Current;
                settings.LeftHandedControls = true;
                fixture.Settings.Apply(settings);

                Assert.That(fixture.Router.MovementScreenSide,
                    Is.EqualTo(ControlScreenSide.Right));
                Assert.That(fixture.Router.ActionScreenSide,
                    Is.EqualTo(ControlScreenSide.Left));
                Assert.That(changes, Is.EqualTo(1));
                Assert.That(
                    fixture.Actions.actionMaps
                        .SelectMany(map => map.actions)
                        .Select(action => $"{action.actionMap.name}/{action.name}"),
                    Is.EqualTo(originalActionNames));

                fixture.Settings.Apply(settings.Copy());
                Assert.That(changes, Is.EqualTo(1));
            }
            finally
            {
                await fixture.DisposeAsync();
            }
        }

        [Test]
        public async Task Initialize_WithIncompleteAssetFailsClosedWithoutEnabledMaps()
        {
            var settings = await CreateSettings();
            var incomplete = ScriptableObject.CreateInstance<InputActionAsset>();
            incomplete.AddActionMap("UI").AddAction("Cancel");
            var router = new InputRouter(incomplete, settings);
            try
            {
                var result = await router.InitializeAsync(CancellationToken.None);

                Assert.That(result.IsAvailable, Is.False);
                Assert.That(router.IsInitialized, Is.False);
                Assert.That(incomplete.actionMaps.Select(map => map.enabled),
                    Is.All.False);
            }
            finally
            {
                await router.ShutdownAsync();
                await settings.ShutdownAsync();
                UnityEngine.Object.DestroyImmediate(incomplete);
            }
        }

        [Test]
        public async Task Initialize_PreCancelledLeavesEveryMapDisabled()
        {
            var settings = await CreateSettings();
            var actions = CreateCanonicalActions();
            var router = new InputRouter(actions, settings);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            try
            {
                Exception cancellationFailure = null;
                try
                {
                    await router.InitializeAsync(cancellation.Token);
                }
                catch (Exception exception)
                {
                    cancellationFailure = exception;
                }

                Assert.That(
                    cancellationFailure,
                    Is.InstanceOf<OperationCanceledException>());
                Assert.That(actions.actionMaps.Select(map => map.enabled),
                    Is.All.False);
                Assert.That(router.IsInitialized, Is.False);
            }
            finally
            {
                await router.ShutdownAsync();
                await settings.ShutdownAsync();
                UnityEngine.Object.DestroyImmediate(actions);
            }
        }

        private async Task<RouterFixture> CreateFixture()
        {
            var settings = await CreateSettings();
            var actions = CreateCanonicalActions();
            return new RouterFixture(
                settings,
                actions,
                new InputRouter(actions, settings));
        }

        private async Task<SettingsService> CreateSettings()
        {
            var settings = new SettingsService(Path.Combine(
                m_TestRoot,
                Guid.NewGuid().ToString("N"),
                "jss-settings-v1.json"));
            var result = await settings.InitializeAsync(CancellationToken.None);
            Assert.That(result.IsAvailable, Is.True);
            return settings;
        }

        private static InputActionAsset CreateCanonicalActions()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var ui = asset.AddActionMap("UI");
            ui.AddAction("Navigate", InputActionType.Value, expectedControlLayout: "Vector2");
            ui.AddAction("Submit", InputActionType.Button);
            ui.AddAction("Cancel", InputActionType.Button, "<Keyboard>/escape")
                .AddBinding("<Gamepad>/buttonEast");
            var point = ui.AddAction(
                "Point",
                InputActionType.PassThrough,
                expectedControlLayout: "Vector2");
            point.AddBinding("<Mouse>/position");
            point.AddBinding("<Touchscreen>/primaryTouch/position");
            var click = ui.AddAction(
                "Click",
                InputActionType.PassThrough,
                expectedControlLayout: "Button");
            click.AddBinding("<Mouse>/leftButton");
            click.AddBinding("<Touchscreen>/primaryTouch/press");
            ui.AddAction("RightClick", InputActionType.PassThrough, expectedControlLayout: "Button")
                .AddBinding("<Mouse>/rightButton");
            ui.AddAction("MiddleClick", InputActionType.PassThrough, expectedControlLayout: "Button")
                .AddBinding("<Mouse>/middleButton");
            ui.AddAction("ScrollWheel", InputActionType.PassThrough, expectedControlLayout: "Vector2")
                .AddBinding("<Mouse>/scroll");
            ui.AddAction("TrackedDevicePosition", InputActionType.PassThrough,
                expectedControlLayout: "Vector3");
            ui.AddAction("TrackedDeviceOrientation", InputActionType.PassThrough,
                expectedControlLayout: "Quaternion");

            foreach (var mapName in GameplayMapNames)
            {
                var map = asset.AddActionMap(mapName);
                map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2")
                    .AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/w")
                    .With("Down", "<Keyboard>/s")
                    .With("Left", "<Keyboard>/a")
                    .With("Right", "<Keyboard>/d");
                map.FindAction("Move").AddBinding("<Gamepad>/leftStick");
                var look = map.AddAction(
                    "Look",
                    InputActionType.Value,
                    expectedControlLayout: "Vector2");
                look.AddBinding("<Mouse>/delta");
                look.AddBinding("<Gamepad>/rightStick");
                map.AddAction("Primary", InputActionType.Button, "<Keyboard>/space")
                    .AddBinding("<Gamepad>/buttonSouth");
                map.AddAction("Secondary", InputActionType.Button, "<Keyboard>/leftShift")
                    .AddBinding("<Gamepad>/buttonWest");
                map.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape")
                    .AddBinding("<Gamepad>/start");
                map.AddAction("Lens", InputActionType.Button, "<Keyboard>/l")
                    .AddBinding("<Gamepad>/leftShoulder");
                map.AddAction("PhotoMode", InputActionType.Button, "<Keyboard>/p")
                    .AddBinding("<Gamepad>/rightShoulder");
                map.AddAction("Recenter", InputActionType.Button, "<Keyboard>/r")
                    .AddBinding("<Gamepad>/rightStickPress");
            }

            return asset;
        }

        private static void AssertOnlyGameplayMapEnabled(
            InputActionAsset actions,
            string enabledMap)
        {
            Assert.That(actions.FindActionMap("UI").enabled, Is.True);
            foreach (var mapName in GameplayMapNames)
            {
                Assert.That(
                    actions.FindActionMap(mapName).enabled,
                    Is.EqualTo(string.Equals(
                        mapName,
                        enabledMap,
                        StringComparison.Ordinal)),
                    mapName);
            }
        }

        private static void PressAndRelease(Keyboard keyboard, Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
        }

        private sealed class RouterFixture
        {
            public RouterFixture(
                SettingsService settings,
                InputActionAsset actions,
                InputRouter router)
            {
                Settings = settings;
                Actions = actions;
                Router = router;
            }

            public SettingsService Settings { get; }

            public InputActionAsset Actions { get; }

            public InputRouter Router { get; }

            public async Task DisposeAsync()
            {
                await Router.ShutdownAsync();
                await Settings.ShutdownAsync();
                UnityEngine.Object.DestroyImmediate(Actions);
            }
        }
    }
}
