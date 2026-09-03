using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class PerformanceBudgetValidatorTests
    {
        private const string ValidatorTypeName =
            "JustSomeStars.Editor.Validation.PerformanceBudgetValidator, JustSomeStars.Editor";

        [Test]
        public void MobileBudgets_AreExplicitBoundedAndProjectContentPassesThem()
        {
            var type = Type.GetType(ValidatorTypeName, throwOnError: false);
            Assert.That(type, Is.Not.Null,
                "Task 30 requires one fail-closed project performance-budget validator.");

            AssertConstant(type, "MaxEnvironmentLayers", 8);
            AssertConstant(type, "MaxActiveCharacters", 6);
            AssertConstant(type, "MaxTwoDLights", 4);
            AssertConstant(type, "MaxParticleSystems", 3);
            AssertConstant(type, "MaxAndroidTextureDimension", 2048);
            AssertConstant(type, "MaxDestinationAtlases", 24);
            AssertConstant(type, "MaxTransparentLayerPeak", 8);
            AssertConstant(type, "PerformanceProcessMemoryBudgetMb", 896);
            AssertConstant(
                type,
                "MaxDestinationTextureResidencyBytes",
                256L * 1024L * 1024L);

            var method = type.GetMethod(
                "CollectProjectFindings",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            var findings = method.Invoke(null, null) as IEnumerable;
            Assert.That(findings, Is.Not.Null);
            var rows = 0;
            foreach (var finding in findings)
            {
                rows++;
                TestContext.WriteLine(finding);
            }

            Assert.That(rows, Is.Zero,
                "The source-frozen player content exceeds its declared mobile budget.");

            var collectSamples = type.GetMethod(
                "CollectProjectSamples",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(collectSamples, Is.Not.Null,
                "The validator must expose the measurements that drive its findings.");
            var samples = ToObjects(collectSamples.Invoke(null, null) as IEnumerable);
            var expectedScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .Where(path => path.StartsWith(
                    "Assets/_JustSomeStars/Scenes/",
                    StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            Assert.That(
                samples.Select(sample => Read<string>(sample, "Owner")),
                Is.EqualTo(expectedScenes),
                "Every enabled player scene must have a real budget sample.");
            foreach (var sample in samples)
            {
                TestContext.WriteLine(
                    $"{Read<string>(sample, "Owner")}: " +
                    $"residency={Read<long>(sample, "TextureResidencyBytes")} " +
                    $"atlases={Read<int>(sample, "AtlasCount")} " +
                    $"transparentPeak={Read<int>(sample, "TransparentLayerPeak")} " +
                    $"actors={Read<int>(sample, "ActiveCharacters")}");
            }

            var authoredCameraSamples = samples
                .Where(sample => Read<int>(sample, "AuthoredCameraCount") > 0)
                .ToArray();
            Assert.That(authoredCameraSamples, Is.Not.Empty,
                "The player build must contain authored scene cameras.");
            foreach (var sample in authoredCameraSamples)
            {
                Assert.That(
                    Read<int>(sample, "DynamicResolutionCameraCount"),
                    Is.EqualTo(Read<int>(sample, "AuthoredCameraCount")),
                    $"Every authored camera in {Read<string>(sample, "Owner")} " +
                    "must opt into Unity's scalable-buffer path.");
            }

            var mirra = samples.Single(sample => string.Equals(
                Read<string>(sample, "Owner"),
                "Assets/_JustSomeStars/Scenes/Destinations/Mirra.unity",
                StringComparison.Ordinal));
            Assert.That(Read<long>(mirra, "TextureResidencyBytes"),
                Is.GreaterThan(0L));
            Assert.That(Read<int>(mirra, "AtlasCount"),
                Is.GreaterThan(0),
                "Mirra ships PNG sprite atlases even though it uses no SpriteAtlas wrapper.");
            Assert.That(Read<int>(mirra, "TransparentLayerPeak"),
                Is.GreaterThanOrEqualTo(2),
                "The overdraw proxy must measure overlapping visible renderers.");
            Assert.That(Read<int>(mirra, "ActiveCharacters"),
                Is.GreaterThanOrEqualTo(4),
                "The Captain, two companions and Ori must all count as active actors.");

            var mirraProof = samples.Single(sample => string.Equals(
                Read<string>(sample, "Owner"),
                "Assets/_JustSomeStars/Scenes/Benchmarks/Mirra2DProof.unity",
                StringComparison.Ordinal));
            Assert.That(Read<int>(mirraProof, "ActiveCharacters"),
                Is.GreaterThanOrEqualTo(3),
                "Animator-only Mira and Ori must count alongside the Captain.");
        }

        [TestCase("TextureResidencyBytes", 268435457L, "texture residency")]
        [TestCase("AtlasCount", 25L, "atlas count")]
        [TestCase("TransparentLayerPeak", 9L, "transparent layer peak")]
        [TestCase("ActiveCharacters", 7L, "active characters")]
        [TestCase("TwoDLights", 5L, "2D lights")]
        [TestCase("ParticleSystems", 4L, "particle systems")]
        [TestCase("ProcessMemoryMb", 897L, "process memory")]
        public void EveryBudgetRule_FailsClosedWithOwnerMeasuredValueAndThreshold(
            string propertyName,
            long overBudgetValue,
            string expectedMetric)
        {
            var validator = Type.GetType(ValidatorTypeName, throwOnError: false);
            var sampleType = Type.GetType(
                "JustSomeStars.Editor.Validation.PerformanceBudgetSample, JustSomeStars.Editor",
                throwOnError: false);
            Assert.That(validator, Is.Not.Null);
            Assert.That(sampleType, Is.Not.Null);

            var sample = Activator.CreateInstance(sampleType);
            Set(sample, "Owner", "fixture://task30/over-budget");
            Set(sample, "TextureResidencyBytes", 128L * 1024L * 1024L);
            Set(sample, "AtlasCount", 12);
            Set(sample, "TransparentLayerPeak", 6);
            Set(sample, "ActiveCharacters", 5);
            Set(sample, "TwoDLights", 3);
            Set(sample, "ParticleSystems", 2);
            Set(sample, "ProcessMemoryMb", 800);
            Set(sample, propertyName, Convert.ChangeType(
                overBudgetValue,
                sampleType.GetProperty(propertyName).PropertyType));

            var validate = validator.GetMethod(
                "CollectFindings",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(validate, Is.Not.Null);
            var findings = validate.Invoke(null, new[] { sample }) as IEnumerable;
            Assert.That(findings, Is.Not.Null);
            var message = string.Join("\n", ToStrings(findings));
            Assert.That(message, Does.Contain("fixture://task30/over-budget"));
            Assert.That(message, Does.Contain(expectedMetric).IgnoreCase);
            Assert.That(message, Does.Contain(overBudgetValue.ToString()));
            Assert.That(message, Does.Contain("limit"));
        }

        [Test]
        public void RuntimeMarkers_AreNamedAndUsedByEveryOwnedSystem()
        {
            var root = Directory.GetCurrentDirectory();
            var markerPath = Path.Combine(
                root,
                "Assets/_JustSomeStars/Runtime/Core/PerformanceMarkers.cs");
            Assert.That(File.Exists(markerPath), Is.True,
                "Task 30 requires the shared runtime marker vocabulary.");

            var markerSource = File.ReadAllText(markerPath);
            var expected = new[]
            {
                "JSS.Player",
                "JSS.Crew",
                "JSS.Flight",
                "JSS.Lens",
                "JSS.UI",
                "JSS.Streaming",
            };
            foreach (var marker in expected)
            {
                StringAssert.Contains(marker, markerSource);
            }

            AssertConsumer(
                root,
                "Assets/_JustSomeStars/Runtime/Player/SurfaceGameplayLifecycle2D.cs",
                "PerformanceMarkers.Player");
            AssertConsumer(
                root,
                "Assets/_JustSomeStars/Runtime/Crew/MirraCrewRuntime2D.cs",
                "PerformanceMarkers.Crew");
            AssertConsumer(
                root,
                "Assets/_JustSomeStars/Runtime/Flight/FlightGameplayLifecycle2D.cs",
                "PerformanceMarkers.Flight");
            AssertConsumer(
                root,
                "Assets/_JustSomeStars/Runtime/Discovery/DiscoveryLensPresenter2D.cs",
                "PerformanceMarkers.Lens");
            AssertConsumer(
                root,
                "Assets/_JustSomeStars/Runtime/UI/FrontendMotionDirector.cs",
                "PerformanceMarkers.UI");
            AssertConsumer(
                root,
                "Assets/_JustSomeStars/Runtime/Core/SceneStreamService.cs",
                "PerformanceMarkers.Streaming");
        }

        private static void AssertConstant(Type type, string fieldName, int expected)
        {
            var field = type.GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, fieldName);
            Assert.That(field.IsLiteral, Is.True, fieldName);
            Assert.That(field.GetRawConstantValue(), Is.EqualTo(expected), fieldName);
        }

        private static void AssertConstant(Type type, string fieldName, long expected)
        {
            var field = type.GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, fieldName);
            Assert.That(field.IsLiteral, Is.True, fieldName);
            Assert.That(Convert.ToInt64(field.GetRawConstantValue()),
                Is.EqualTo(expected), fieldName);
        }

        private static string[] ToStrings(IEnumerable values)
        {
            var rows = new System.Collections.Generic.List<string>();
            foreach (var value in values)
            {
                rows.Add(value?.ToString() ?? "<null>");
            }

            return rows.ToArray();
        }

        private static object[] ToObjects(IEnumerable values)
        {
            Assert.That(values, Is.Not.Null);
            var rows = new System.Collections.Generic.List<object>();
            foreach (var value in values)
            {
                rows.Add(value);
            }

            return rows.ToArray();
        }

        private static T Read<T>(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(target);
        }

        private static void Set(object target, string propertyName, object value)
        {
            var property = target.GetType().GetProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            property.SetValue(target, value);
        }

        private static void AssertConsumer(string root, string relativePath, string marker)
        {
            var path = Path.Combine(root, relativePath);
            Assert.That(File.Exists(path), Is.True, relativePath);
            StringAssert.Contains(marker, File.ReadAllText(path), relativePath);
        }
    }
}
