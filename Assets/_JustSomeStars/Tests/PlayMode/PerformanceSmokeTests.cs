using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Discovery;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class PerformanceSmokeTests
    {
        private const string ServiceTypeName =
            "JustSomeStars.Runtime.Core.QualityProfileService, JustSomeStars.Runtime";

        [TestCase(PresentationQuality.Performance, 30, 0.70f, 0.82f, true)]
        [TestCase(PresentationQuality.Balanced, 30, 0.80f, 1.00f, false)]
        [TestCase(PresentationQuality.Cinematic, 30, 0.80f, 1.00f, false)]
        [TestCase(PresentationQuality.HighFrameRate, 60, 0.72f, 1.00f, true)]
        public void QualityProfiles_ExposeTheLockedMobileEnvelope(
            PresentationQuality quality,
            int targetFrameRate,
            float minimumScale,
            float maximumScale,
            bool adaptive)
        {
            var service = RequireServiceType();
            var envelope = InvokeStatic(service, "GetEnvelope", quality);
            Assert.That(Read<int>(envelope, "TargetFrameRate"), Is.EqualTo(targetFrameRate));
            Assert.That(Read<float>(envelope, "MinimumRenderScale"),
                Is.EqualTo(minimumScale).Within(0.0001f));
            Assert.That(Read<float>(envelope, "MaximumRenderScale"),
                Is.EqualTo(maximumScale).Within(0.0001f));
            Assert.That(Read<bool>(envelope, "UsesAdaptiveResolution"), Is.EqualTo(adaptive));
        }

        [Test]
        public void AdaptiveProfiles_DegradeQuicklyRecoverSlowlyAndHonorLowMemory()
        {
            var service = RequireServiceType();

            var overloaded = Evaluate(
                service,
                PresentationQuality.Performance,
                0.82f,
                0.040f,
                false);
            Assert.That(overloaded, Is.EqualTo(0.77f).Within(0.0001f));

            var recovered = Evaluate(
                service,
                PresentationQuality.Performance,
                overloaded,
                0.020f,
                false);
            Assert.That(recovered, Is.EqualTo(0.79f).Within(0.0001f));

            var lowMemory = Evaluate(
                service,
                PresentationQuality.HighFrameRate,
                1f,
                0.010f,
                true);
            Assert.That(lowMemory, Is.EqualTo(0.90f).Within(0.0001f));

            var fixedScale = Evaluate(
                service,
                PresentationQuality.Balanced,
                1f,
                0.050f,
                true);
            Assert.That(fixedScale, Is.EqualTo(0.90f).Within(0.0001f),
                "Low-memory pressure must shed render residency even for the " +
                "normally fixed-scale Balanced profile.");
        }

        [Test]
        public void LiveService_ObservesSettingsAdaptsAndRestoresOwnedGlobals()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "JssTask30Quality",
                Guid.NewGuid().ToString("N"));
            var settings = new SettingsService(Path.Combine(root, "settings.json"));
            var serviceRoot = new GameObject("Task30QualityService");
            var type = RequireServiceType();
            var component = serviceRoot.AddComponent(type);
            var originalTarget = Application.targetFrameRate;
            var originalWidth = ScalableBufferManager.widthScaleFactor;
            var originalHeight = ScalableBufferManager.heightScaleFactor;
            try
            {
                var startup = settings.InitializeAsync(CancellationToken.None).Result;
                Assert.That(startup.IsAvailable, Is.True);
                Invoke(component, "Configure", settings);
                Assert.That(Read<bool>(component, "IsBound"), Is.True);

                var changed = settings.Current;
                changed.PresentationQuality = PresentationQuality.Performance;
                Assert.That(settings.Apply(changed), Is.True);
                Assert.That(Read<int>(component, "ActiveTargetFrameRate"), Is.EqualTo(30));
                Assert.That(Read<float>(component, "ActiveRenderScale"),
                    Is.EqualTo(0.82f).Within(0.0001f));

                Invoke(component, "SampleFrameForTests", 0.040f, false);
                Assert.That(Read<float>(component, "ActiveRenderScale"),
                    Is.EqualTo(0.77f).Within(0.0001f));

                Invoke(component, "Release", settings);
                Assert.That(Read<bool>(component, "IsBound"), Is.False);
                Assert.That(Application.targetFrameRate, Is.EqualTo(originalTarget));
                Assert.That(ScalableBufferManager.widthScaleFactor,
                    Is.EqualTo(originalWidth).Within(0.0001f));
                Assert.That(ScalableBufferManager.heightScaleFactor,
                    Is.EqualTo(originalHeight).Within(0.0001f));
            }
            finally
            {
                if (Read<bool>(component, "IsBound"))
                {
                    Invoke(component, "Release", settings);
                }
                settings.ShutdownAsync().GetAwaiter().GetResult();
                UnityEngine.Object.DestroyImmediate(serviceRoot);
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [UnityTest]
        public IEnumerator RepresentativeProductionScenes_RunHotPathsAndPublishSamples()
        {
            var markers = Type.GetType(
                "JustSomeStars.Runtime.Core.PerformanceMarkers, JustSomeStars.Runtime",
                throwOnError: false);
            Assert.That(markers, Is.Not.Null);
            InvokeStatic(markers, "ResetForTests");

            var settingsRoot = Path.Combine(
                Path.GetTempPath(),
                "JssTask30SceneQuality",
                Guid.NewGuid().ToString("N"));
            var settings = new SettingsService(Path.Combine(
                settingsRoot,
                "settings.json"));
            var serviceRoot = new GameObject("Task30SceneQualityService");
            var service = serviceRoot.AddComponent(RequireServiceType());
            var startup = settings.InitializeAsync(CancellationToken.None).Result;
            Assert.That(startup.IsAvailable, Is.True);
            Invoke(service, "Configure", settings);
            var performance = settings.Current;
            performance.PresentationQuality = PresentationQuality.Performance;
            Assert.That(settings.Apply(performance), Is.True);

            var baseline = SceneManager.GetActiveScene();
            var mirra = Addressables.LoadSceneAsync(
                "Mirra",
                LoadSceneMode.Additive,
                activateOnLoad: true);
            yield return mirra;
            Assert.That(mirra.Status, Is.EqualTo(AsyncOperationStatus.Succeeded));
            yield return null;
            yield return null;
            AssertSceneCamerasUseDynamicResolution(mirra.Result.Scene, service);
            var lensPresenter = UnityEngine.Object.FindFirstObjectByType<
                DiscoveryLensPresenter2D>(FindObjectsInactive.Include);
            Assert.That(lensPresenter, Is.Not.Null,
                "Mirra must retain its real Discovery Lens presenter.");
            var lensWasActive = lensPresenter.gameObject.activeSelf;
            lensPresenter.gameObject.SetActive(true);
            yield return null;
            if (!lensWasActive)
            {
                lensPresenter.gameObject.SetActive(false);
            }
            yield return Addressables.UnloadSceneAsync(mirra, autoReleaseHandle: true);

            var flight = Addressables.LoadSceneAsync(
                "Task25VesperFlight",
                LoadSceneMode.Additive,
                activateOnLoad: true);
            yield return flight;
            Assert.That(flight.Status, Is.EqualTo(AsyncOperationStatus.Succeeded));
            yield return null;
            yield return null;
            AssertSceneCamerasUseDynamicResolution(flight.Result.Scene, service);
            yield return Addressables.UnloadSceneAsync(flight, autoReleaseHandle: true);

            var aster = Addressables.LoadSceneAsync(
                "AsterVeil",
                LoadSceneMode.Additive,
                activateOnLoad: true);
            yield return aster;
            Assert.That(aster.Status, Is.EqualTo(AsyncOperationStatus.Succeeded));
            yield return null;
            yield return null;
            AssertSceneCamerasUseDynamicResolution(aster.Result.Scene, service);
            yield return Addressables.UnloadSceneAsync(aster, autoReleaseHandle: true);

            var frontend = SceneManager.LoadSceneAsync("Frontend", LoadSceneMode.Additive);
            Assert.That(frontend, Is.Not.Null);
            yield return frontend;
            yield return null;
            yield return null;
            var frontendScene = SceneManager.GetSceneByName("Frontend");
            Assert.That(frontendScene.IsValid() && frontendScene.isLoaded, Is.True);
            AssertSceneCamerasUseDynamicResolution(
                frontendScene,
                service,
                allowCameraLessOverlay: true);
            yield return SceneManager.UnloadSceneAsync(frontendScene);

            if (baseline.IsValid() && baseline.isLoaded)
            {
                SceneManager.SetActiveScene(baseline);
            }

            AssertSample(markers, "PlayerSamples");
            AssertSample(markers, "CrewSamples");
            AssertSample(markers, "FlightSamples");
            AssertSample(markers, "LensSamples");
            AssertSample(markers, "UISamples");

            Invoke(service, "Release", settings);
            settings.ShutdownAsync().GetAwaiter().GetResult();
            UnityEngine.Object.DestroyImmediate(serviceRoot);
            if (Directory.Exists(settingsRoot))
            {
                Directory.Delete(settingsRoot, recursive: true);
            }
        }

        private static void AssertSceneCamerasUseDynamicResolution(
            Scene scene,
            object service,
            bool allowCameraLessOverlay = false)
        {
            var cameras = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .Where(camera => camera.isActiveAndEnabled)
                .ToArray();
            if (allowCameraLessOverlay && cameras.Length == 0)
            {
                TestContext.WriteLine(
                    $"{scene.path}: camera-less screen-space overlay; render-scale " +
                    "ownership remains with the underlying player scene camera.");
                return;
            }
            Assert.That(cameras, Is.Not.Empty, scene.path);
            foreach (var camera in cameras)
            {
                Assert.That(
                    (bool)Invoke(service, "ManagesCamera", camera),
                    Is.True,
                    $"The live quality service did not configure {camera.name} " +
                    $"in {scene.path}.");
            }
            if (SystemInfo.supportsDynamicResolution)
            {
                Assert.That(cameras.All(camera => camera.allowDynamicResolution),
                    Is.True,
                    $"Every active camera in {scene.path} must consume the live " +
                    "Performance render-scale envelope.");
            }
            else
            {
                TestContext.WriteLine(
                    $"{scene.path}: native dynamic resolution is unsupported on " +
                    $"{SystemInfo.operatingSystem}; service ownership verified and " +
                    "Android camera opt-in is covered by EditMode serialization.");
            }
        }

        private static Type RequireServiceType()
        {
            var type = Type.GetType(ServiceTypeName, throwOnError: false);
            Assert.That(type, Is.Not.Null,
                "Task 30 requires the global quality-profile service.");
            return type;
        }

        private static object InvokeStatic(Type type, string methodName, object argument)
        {
            var method = type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(null, new[] { argument });
        }

        private static object InvokeStatic(Type type, string methodName)
        {
            var method = type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(null, null);
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            var methods = target.GetType().GetMethods(
                BindingFlags.Public | BindingFlags.Instance);
            foreach (var method in methods)
            {
                if (method.Name == methodName &&
                    method.GetParameters().Length == arguments.Length)
                {
                    return method.Invoke(target, arguments);
                }
            }

            Assert.Fail($"Missing method {target.GetType().FullName}.{methodName}");
            return null;
        }

        private static void AssertSample(Type markerType, string propertyName)
        {
            var property = markerType.GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(property, Is.Not.Null, propertyName);
            Assert.That(Convert.ToInt64(property.GetValue(null)),
                Is.GreaterThan(0L), propertyName);
        }

        private static float Evaluate(
            Type type,
            PresentationQuality quality,
            float scale,
            float averageFrameSeconds,
            bool lowMemory)
        {
            var method = type.GetMethod(
                "EvaluateNextScale",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return (float)method.Invoke(
                null,
                new object[] { quality, scale, averageFrameSeconds, lowMemory });
        }

        private static T Read<T>(object target, string propertyName)
        {
            Assert.That(target, Is.Not.Null, propertyName);
            var property = target.GetType().GetProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(target);
        }
    }
}
