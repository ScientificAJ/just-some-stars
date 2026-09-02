using System;
using System.Collections;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Atlas;
using JustSomeStars.Runtime.Commerce;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class AccessibilityUiTests
    {
        private readonly List<UnityEngine.Object> m_Owned =
            new List<UnityEngine.Object>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (var index = m_Owned.Count - 1; index >= 0; index--)
            {
                if (m_Owned[index] != null)
                {
                    UnityEngine.Object.Destroy(m_Owned[index]);
                }
            }

            m_Owned.Clear();
            yield return null;
        }

        [Test]
        public void EnglishCatalog_ResolvesEveryOwnedPlayerUiKeyAndRejectsFallbacks()
        {
            var catalog = Own(ScriptableObject.CreateInstance<
                LocalizedEnglishCatalog>());
            catalog.Configure(Task28English.CreateEntries());

            foreach (var key in Task28English.RequiredKeys)
            {
                Assert.That(catalog.Resolve(key), Is.Not.Null.And.Not.Empty, key);
            }

            Assert.That(
                () => catalog.Resolve("ui.missing-contract"),
                Throws.TypeOf<KeyNotFoundException>());
            Assert.That(
                catalog.Resolve(Task28English.FrontendStatus),
                Does.Not.Contain("Development Flight"));
            Assert.That(
                catalog.Resolve(Task28English.FrontendContinueNoSave),
                Does.Not.Contain("not in this flight"));
            var resolved = string.Join("\n", Task28English.RequiredKeys
                .Select(catalog.Resolve));
            Assert.That(resolved, Does.Not.Contain("SIL OPEN FONT LICENSE"));
            Assert.That(resolved, Does.Not.Contain("Apache License\nVersion 2.0"));
        }

        [UnityTest]
        public IEnumerator AccessibilityApplier_MaximumCombinedOptionsReflowAndReduceEffects()
        {
            var root = Own(new GameObject("AccessibilityFixture"));
            var standard = Resources.Load<TMP_FontAsset>(
                "Fonts & Materials/LiberationSans SDF");
            Assert.That(standard, Is.Not.Null);
            var readable = Own(UnityEngine.Object.Instantiate(standard));
            readable.name = "ReadableTypeFixture";

            var labelObject = Own(new GameObject("LongCaption"));
            labelObject.transform.SetParent(root.transform, false);
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.font = standard;
            label.fontSize = 20f;
            label.text = "Mina: A long caption must wrap without clipping.";
            label.enableWordWrapping = false;

            var captionRoot = Own(new GameObject("CaptionRoot"));
            captionRoot.transform.SetParent(root.transform, false);
            var speakerObject = Own(new GameObject(
                "Speaker",
                typeof(RectTransform),
                typeof(TextMeshProUGUI)));
            speakerObject.transform.SetParent(captionRoot.transform, false);
            var bodyObject = Own(new GameObject(
                "CaptionBody",
                typeof(RectTransform),
                typeof(TextMeshProUGUI)));
            bodyObject.transform.SetParent(captionRoot.transform, false);
            var caption = captionRoot.AddComponent<AccessibleCaption>();
            SetField(caption, "root", captionRoot);
            SetField(caption, "speakerLabel", speakerObject.GetComponent<TMP_Text>());
            SetField(caption, "bodyLabel", bodyObject.GetComponent<TMP_Text>());
            caption.Present("Mina", "The Signal repeats every eight seconds.");

            var movementObject = Own(new GameObject(
                "Movement",
                typeof(RectTransform)));
            movementObject.transform.SetParent(root.transform, false);
            var movement = movementObject.GetComponent<RectTransform>();
            movement.anchorMin = new Vector2(0.05f, 0.05f);
            movement.anchorMax = new Vector2(0.2f, 0.25f);
            var actionObject = Own(new GameObject(
                "Actions",
                typeof(RectTransform)));
            actionObject.transform.SetParent(root.transform, false);
            var actions = actionObject.GetComponent<RectTransform>();
            actions.anchorMin = new Vector2(0.8f, 0.05f);
            actions.anchorMax = new Vector2(0.95f, 0.25f);
            var touchLayout = root.AddComponent<AccessibleTouchLayout>();
            SetField(touchLayout, "movementGroup", new[] { movement });
            SetField(touchLayout, "actionGroup", new[] { actions });

            var symbolObject = Own(new GameObject(
                "StatusSymbol",
                typeof(RectTransform),
                typeof(TextMeshProUGUI)));
            symbolObject.transform.SetParent(root.transform, false);
            var symbol = symbolObject.AddComponent<AccessibleStatusSymbol>();
            SetField(symbol, "symbolLabel", symbolObject.GetComponent<TMP_Text>());

            var flashingObject = Own(new GameObject(
                "FlashingEffect",
                typeof(CanvasGroup)));
            flashingObject.transform.SetParent(root.transform, false);
            var flashingPulse = flashingObject.AddComponent<AccessibleSignalPulse>();
            SetField(
                flashingPulse,
                "target",
                flashingObject.GetComponent<CanvasGroup>());
            var flashingEffect = flashingObject.AddComponent<AccessibleEffect>();
            SetField(
                flashingEffect,
                "kind",
                AccessibilityEffectKind.Flashing);
            SetField(flashingEffect, "effect", flashingPulse);

            var blurObject = Own(new GameObject(
                "MotionBlurEffect",
                typeof(CanvasGroup)));
            blurObject.transform.SetParent(root.transform, false);
            var blurProxy = blurObject.AddComponent<AccessibleSignalPulse>();
            SetField(
                blurProxy,
                "target",
                blurObject.GetComponent<CanvasGroup>());
            var blurEffect = blurObject.AddComponent<AccessibleEffect>();
            SetField(
                blurEffect,
                "kind",
                AccessibilityEffectKind.MotionBlur);
            SetField(blurEffect, "effect", blurProxy);

            var particlesObject = Own(new GameObject("Particles"));
            particlesObject.transform.SetParent(root.transform, false);
            var particles = particlesObject.AddComponent<ParticleSystem>();
            var initialEmission = particles.emission;
            initialEmission.rateOverTime = 24f;

            var settings = CreateSettingsService();
            var combined = settings.Current.Copy();
            combined.TextScale = 1.35f;
            combined.DyslexiaFriendlyFontEnabled = true;
            combined.CaptionsEnabled = true;
            combined.ReducedCameraShake = true;
            combined.ReducedFlashing = true;
            combined.ReducedMotion = true;
            combined.MotionBlurEnabled = false;
            combined.ParticleDensity = 0.25f;
            combined.LeftHandedControls = true;
            combined.ColorVisionMode = ColorVisionMode.Protanopia;
            Assert.That(settings.Apply(combined), Is.True);

            var applier = root.AddComponent<AccessibilityApplier>();
            SetField(applier, "m_ScopeRoot", root.transform);
            applier.Configure(settings, standard, readable);
            applier.ApplyNow();
            yield return null;

            Assert.That(label.font, Is.SameAs(readable));
            Assert.That(label.fontSize, Is.EqualTo(27f).Within(0.01f));
            Assert.That(label.enableWordWrapping, Is.True);
            Assert.That(applier.EffectiveTextScale, Is.EqualTo(1.35f));
            Assert.That(applier.ReducedMotionActive, Is.True);
            Assert.That(applier.ReducedFlashingActive, Is.True);
            Assert.That(applier.CaptionsEnabled, Is.True);
            Assert.That(captionRoot.activeSelf, Is.True);
            Assert.That(speakerObject.GetComponent<TMP_Text>().text, Is.EqualTo("Mina"));
            Assert.That(
                bodyObject.GetComponent<TMP_Text>().text,
                Is.EqualTo("The Signal repeats every eight seconds."));
            Assert.That(movement.anchorMin.x, Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(movement.anchorMax.x, Is.EqualTo(0.95f).Within(0.001f));
            Assert.That(actions.anchorMin.x, Is.EqualTo(0.05f).Within(0.001f));
            Assert.That(actions.anchorMax.x, Is.EqualTo(0.2f).Within(0.001f));
            Assert.That(
                symbolObject.GetComponent<TMP_Text>().text,
                Is.EqualTo("◆"));
            Assert.That(
                particles.emission.rateOverTime.constant,
                Is.EqualTo(6f).Within(0.01f));
            Assert.That(flashingPulse.enabled, Is.False);
            Assert.That(blurProxy.enabled, Is.False);

            settings.ShutdownAsync().GetAwaiter().GetResult();
        }

        [UnityTest]
        public IEnumerator PhotoMode_BaseToolsAreBoundedAndRestoreWorldAndHud()
        {
            var root = Own(new GameObject("PhotoModeFixture"));
            var cameraObject = Own(new GameObject("Camera"));
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4f;
            camera.transform.position = new Vector3(2f, 1f, -10f);
            camera.transform.rotation = Quaternion.Euler(0f, 0f, 7f);
            var hudObject = Own(new GameObject("Hud"));
            var hud = hudObject.AddComponent<CanvasGroup>();
            hud.alpha = 1f;
            hud.blocksRaycasts = true;

            var modes = GameModeController.CreateForTests(GameMode.Surface);
            yield return Await(modes.InitializeAsync(CancellationToken.None));
            var controller = root.AddComponent<PhotoModeController>();
            controller.Configure(new PhotoModeRuntimeDependencies(
                modes,
                camera,
                new UnavailableStoreService(),
                new Bounds(Vector3.zero, new Vector3(8f, 4f, 1f)),
                new[] { hud }));

            yield return Await(controller.OpenAsync(CancellationToken.None));
            Assert.That(modes.CurrentPolicy.Overlay, Is.EqualTo(GameOverlay.PhotoMode));
            Assert.That(controller.AllowsFreeOrbit, Is.False);
            Assert.That(controller.AdvancedControlsAvailable, Is.False);

            controller.PanBy(new Vector2(100f, -100f));
            controller.ZoomBy(-100f);
            controller.SetExposure(3f);
            controller.SetCleanHud(true);

            Assert.That(camera.transform.position.x, Is.InRange(-4f, 4f));
            Assert.That(camera.transform.position.y, Is.InRange(-2f, 2f));
            Assert.That(camera.orthographicSize, Is.InRange(2.5f, 7f));
            Assert.That(controller.Exposure, Is.InRange(-1.5f, 1.5f));
            Assert.That(hud.alpha, Is.EqualTo(0f));

            yield return Await(controller.CloseAsync(CancellationToken.None));
            Assert.That(modes.CurrentPolicy.Overlay, Is.EqualTo(GameOverlay.None));
            Assert.That(camera.transform.position, Is.EqualTo(new Vector3(2f, 1f, -10f)));
            Assert.That(
                Quaternion.Angle(camera.transform.rotation, Quaternion.Euler(0f, 0f, 7f)),
                Is.LessThan(0.01f));
            Assert.That(camera.orthographicSize, Is.EqualTo(4f));
            Assert.That(hud.alpha, Is.EqualTo(1f));
            Assert.That(hud.blocksRaycasts, Is.True);
            yield return Await(modes.ShutdownAsync());
        }

        private SettingsService CreateSettingsService()
        {
            var settings = new SettingsService(Path.Combine(
                Path.GetTempPath(),
                "JssTask28AccessibilityUiTests",
                Guid.NewGuid().ToString("N"),
                "jss-settings-v1.json"));
            var startup = settings.InitializeAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            Assert.That(startup.IsAvailable, Is.True);
            return settings;
        }

        private T Own<T>(T value) where T : UnityEngine.Object
        {
            m_Owned.Add(value);
            return value;
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private static IEnumerator Await(ValueTask operation)
        {
            var task = operation.AsTask();
            while (!task.IsCompleted)
            {
                yield return null;
            }
            task.GetAwaiter().GetResult();
        }

        private static IEnumerator Await<T>(ValueTask<T> operation)
        {
            var task = operation.AsTask();
            while (!task.IsCompleted)
            {
                yield return null;
            }
            _ = task.GetAwaiter().GetResult();
        }
    }
}
