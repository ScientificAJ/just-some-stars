using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Input;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class SurfaceInteractionProbe2DTests
    {
        [Test]
        public async Task PrimaryTogglesTheNearbyTargetAndSecondaryDoesNot()
        {
            var testRoot = Path.Combine(
                Path.GetTempPath(),
                "JssTask12Stage1Interaction",
                Guid.NewGuid().ToString("N"));
            var settings = new SettingsService(Path.Combine(testRoot, "settings.json"));
            var actions = UnityEngine.Object.Instantiate(InputSystem.actions);
            var input = new InputRouter(actions, settings);
            var target = new GameObject("InteractionTarget");
            var player = new GameObject("CaptainProxy");
            var previousBackgroundBehavior =
                InputSystem.settings.backgroundBehavior;
            var previousEditorBehavior =
                InputSystem.settings.editorInputBehaviorInPlayMode;
            Keyboard keyboard = null;
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
                input.SetGameplayMode(GameplayInputMode.Surface);

                var labelObject = new GameObject("InteractionLabel");
                labelObject.transform.SetParent(target.transform, false);
                var label = labelObject.AddComponent<TextMeshPro>();
                var probe = Stage1RuntimeReflection.AddComponent(
                    target,
                    "JustSomeStars.Runtime.Player.SurfaceInteractionProbe2D");
                Stage1RuntimeReflection.Invoke(
                    probe,
                    "Configure",
                    label,
                    "INTERACT",
                    "SIGNAL LINKED");
                Stage1RuntimeReflection.Invoke(probe, "BindInput", input);

                var playerCollider = player.AddComponent<CapsuleCollider2D>();
                Stage1RuntimeReflection.AddComponent(
                    player,
                    "JustSomeStars.Runtime.Player.SurfaceMotor2D");
                InvokeNonPublic(probe, "OnTriggerEnter2D", playerCollider);
                Assert.That(
                    Stage1RuntimeReflection.Read<bool>(probe, "IsAvailable"),
                    Is.True);

                keyboard = InputSystem.AddDevice<Keyboard>();
                PressAndRelease(keyboard, Key.Space);
                Assert.That(
                    Stage1RuntimeReflection.Read<bool>(probe, "IsActivated"),
                    Is.False,
                    "Secondary jump/jet input cannot activate interactions.");

                PressAndRelease(keyboard, Key.E);
                Assert.That(
                    Stage1RuntimeReflection.Read<bool>(probe, "IsActivated"),
                    Is.True);
                Assert.That(label.text, Is.EqualTo("SIGNAL LINKED"));
            }
            finally
            {
                InputSystem.settings.editorInputBehaviorInPlayMode =
                    previousEditorBehavior;
                InputSystem.settings.backgroundBehavior =
                    previousBackgroundBehavior;
                if (keyboard != null && keyboard.added)
                {
                    InputSystem.RemoveDevice(keyboard);
                }

                await input.ShutdownAsync();
                await settings.ShutdownAsync();
                UnityEngine.Object.DestroyImmediate(actions);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(player);
                if (Directory.Exists(testRoot))
                {
                    Directory.Delete(testRoot, recursive: true);
                }
            }
        }

        private static void InvokeNonPublic(
            object target,
            string methodName,
            params object[] args)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, args);
        }

        private static void PressAndRelease(Keyboard keyboard, Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
        }
    }
}
