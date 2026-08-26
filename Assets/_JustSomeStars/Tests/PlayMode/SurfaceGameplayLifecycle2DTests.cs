using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Input;
using JustSomeStars.Runtime.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class SurfaceGameplayLifecycle2DTests
    {
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
                    modes);
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
