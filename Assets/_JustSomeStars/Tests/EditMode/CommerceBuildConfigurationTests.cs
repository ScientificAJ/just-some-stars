using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using JustSomeStars.Editor.Build;
using NUnit.Framework;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class CommerceBuildConfigurationTests
    {
        private string m_ProjectRoot;

        [SetUp]
        public void SetUp()
        {
            m_ProjectRoot = Path.Combine(
                Path.GetTempPath(),
                "JssTask23CommerceBuild",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_ProjectRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(m_ProjectRoot))
            {
                Directory.Delete(m_ProjectRoot, recursive: true);
            }
        }

        [Test]
        public void InternalBuild_WithoutTestStoreKeyPublishesNoConfiguration()
        {
            var refreshCount = 0;
            var factory = Factory(new Dictionary<string, string>(),
                () => refreshCount++);

            using var lease = factory.Acquire(
                BuildConfiguration.Resolve(BuildTargetKind.AndroidInternal, 1));

            Assert.That(File.Exists(ConfigurationPath()), Is.False);
            lease.CleanupAndVerify();
            Assert.That(Directory.Exists(GeneratedRoot()), Is.False);
            Assert.That(refreshCount, Is.EqualTo(0));
        }

        [Test]
        public void TestStoreBuild_UsesTemporaryExactConfigurationAndCleansEveryByte()
        {
            const string key = "test_task23_public_sdk_key";
            var refreshCount = 0;
            var factory = Factory(new Dictionary<string, string>
            {
                [RevenueCatBuildEnvironment.TestStoreApiKeyVariable] = key,
            }, () => refreshCount++);

            using var lease = factory.Acquire(
                BuildConfiguration.Resolve(BuildTargetKind.AndroidInternal, 1));

            Assert.That(File.Exists(ConfigurationPath()), Is.True);
            var document = File.ReadAllText(ConfigurationPath());
            StringAssert.Contains("\"environment\":\"RevenueCatTestStore\"", document);
            StringAssert.Contains("\"packageId\":\"com.scientificaj.justsomestars\"", document);
            StringAssert.Contains("\"apiKey\":\"" + key + "\"", document);
            Assert.That(refreshCount, Is.EqualTo(1));

            lease.CleanupAndVerify();

            Assert.That(Directory.Exists(GeneratedRoot()), Is.False);
            Assert.That(File.Exists(GeneratedRoot() + ".meta"), Is.False);
            Assert.That(refreshCount, Is.EqualTo(2));
        }

        [Test]
        public void StoreVariants_RejectMissingWrongOrCrossStoreCredentials()
        {
            var google = BuildConfiguration.Resolve(BuildTargetKind.GooglePlay, 1);
            var galaxy = BuildConfiguration.Resolve(BuildTargetKind.Galaxy, 1);

            Directory.CreateDirectory(Path.GetDirectoryName(ConfigurationPath()));
            File.WriteAllText(ConfigurationPath(), "stale-public-sdk-key");
            File.WriteAllText(GeneratedRoot() + ".meta", "stale-folder-meta");
            Assert.Throws<InvalidOperationException>(() =>
                Factory(new Dictionary<string, string>(), () => { }).Acquire(google));
            Assert.That(Directory.Exists(GeneratedRoot()), Is.False,
                "Validation failures must remove stale generated key material.");
            Assert.That(File.Exists(GeneratedRoot() + ".meta"), Is.False);
            Assert.Throws<InvalidOperationException>(() =>
                Factory(new Dictionary<string, string>
                {
                    [RevenueCatBuildEnvironment.GoogleApiKeyVariable] =
                        "test_wrong_environment",
                }, () => { }).Acquire(google));
            Assert.Throws<InvalidOperationException>(() =>
                Factory(new Dictionary<string, string>
                {
                    [RevenueCatBuildEnvironment.TestStoreApiKeyVariable] =
                        "test_cross_store",
                    [RevenueCatBuildEnvironment.GoogleApiKeyVariable] =
                        "goog_release_key",
                }, () => { }).Acquire(google));
            Assert.Throws<InvalidOperationException>(() =>
                Factory(new Dictionary<string, string>
                {
                    [RevenueCatBuildEnvironment.TestStoreApiKeyVariable] =
                        "test_forbidden_on_galaxy",
                }, () => { }).Acquire(galaxy));
            Assert.Throws<InvalidOperationException>(() =>
                Factory(new Dictionary<string, string>
                {
                    [RevenueCatBuildEnvironment.GoogleApiKeyVariable] =
                        "goog_cross_store",
                }, () => { }).Acquire(galaxy));

            using var valid = Factory(new Dictionary<string, string>
            {
                [RevenueCatBuildEnvironment.GoogleApiKeyVariable] =
                    "goog_release_key",
            }, () => { }).Acquire(google);
            StringAssert.Contains(
                "\"environment\":\"GooglePlay\"",
                File.ReadAllText(ConfigurationPath()));
            valid.CleanupAndVerify();

            using var validGalaxy = Factory(
                new Dictionary<string, string>(),
                () => { }).Acquire(galaxy);
            Assert.That(File.Exists(ConfigurationPath()), Is.False,
                "The Samsung fallback must not materialize RevenueCat " +
                "configuration or invent a Galaxy RevenueCat key.");
            validGalaxy.CleanupAndVerify();
        }

        [Test]
        public void GeneratedAndroidProject_UsesSafeLaunchModeAndIsolatesGalaxyBilling()
        {
            var processorType = typeof(BuildCli).Assembly.GetType(
                "JustSomeStars.Editor.Build.RevenueCatAndroidBuildProcessor");
            Assert.That(processorType, Is.Not.Null,
                "Task 23 requires a generated-project policy, not only an asmdef " +
                "constraint, because EDM4U resolves native dependencies globally.");
            var resolveGalaxy = processorType.GetMethod(
                "ResolveGalaxyVariant",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(resolveGalaxy, Is.Not.Null,
                "The real generated-project callback must resolve the store from " +
                "the invocation-applied package ID, not persistent symbols.");
            Assert.That(resolveGalaxy.Invoke(null, new object[]
            {
                BuildConfiguration.PrimaryAndroidPackageId,
            }), Is.False);
            Assert.That(resolveGalaxy.Invoke(null, new object[]
            {
                BuildConfiguration.GalaxyAndroidPackageId,
            }), Is.True);
            var unknownPackage = Assert.Throws<TargetInvocationException>(() =>
                resolveGalaxy.Invoke(null, new object[]
                {
                    "com.scientificaj.justsomestars.unknown",
                }));
            StringAssert.Contains(
                "does not identify a supported JSS store variant",
                unknownPackage.InnerException?.Message);
            var patch = processorType.GetMethod(
                "PatchGeneratedAndroidProject",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(patch, Is.Not.Null);

            var unityLibrary = Path.Combine(m_ProjectRoot, "unityLibrary");
            var manifestPath = Path.Combine(
                unityLibrary,
                "src",
                "main",
                "AndroidManifest.xml");
            var gradlePath = Path.Combine(unityLibrary, "build.gradle");
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath));
            File.WriteAllText(
                manifestPath,
                "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\">" +
                "<application><activity " +
                "android:name=\"com.unity3d.player.UnityPlayerGameActivity\" " +
                "android:launchMode=\"singleTask\" /></application></manifest>");
            File.WriteAllText(
                gradlePath,
                "dependencies {\n" +
                "    implementation 'com.revenuecat.purchases:" +
                "purchases-hybrid-common:[18.33.1]'\n" +
                "    implementation 'com.android.billingclient:billing:8.0.0'\n" +
                "}\n");
            File.WriteAllText(
                Path.Combine(m_ProjectRoot, "settings.gradle"),
                "include ':launcher', ':unityLibrary'\n");

            patch.Invoke(null, new object[] { unityLibrary, true });
            patch.Invoke(null, new object[] { unityLibrary, true });

            var manifest = File.ReadAllText(manifestPath);
            StringAssert.Contains("android:launchMode=\"singleTop\"", manifest);
            StringAssert.DoesNotContain("singleTask", manifest);
            var gradle = File.ReadAllText(gradlePath);
            StringAssert.Contains(
                "JSS_TASK24_GALAXY_SAMSUNG_IAP_ISOLATION",
                gradle);
            StringAssert.Contains(
                "implementation project(':jssGalaxyBilling')",
                gradle);
            StringAssert.DoesNotContain("purchases-hybrid-common", gradle);
            StringAssert.DoesNotContain("com.android.billingclient", gradle);
            Assert.That(
                Directory.Exists(Path.Combine(
                    m_ProjectRoot,
                    "jssGalaxyBilling")),
                Is.True);
            Assert.That(
                gradle.Split(new[]
                {
                    "JSS_TASK24_GALAXY_SAMSUNG_IAP_ISOLATION",
                }, StringSplitOptions.None),
                Has.Length.EqualTo(2),
                "The generated-project policy must be idempotent.");
        }

        private RevenueCatBuildConfigurationLeaseFactory Factory(
            IReadOnlyDictionary<string, string> values,
            Action refresh) => new RevenueCatBuildConfigurationLeaseFactory(
                m_ProjectRoot,
                name => values.TryGetValue(name, out var value) ? value : null,
                refresh);

        private string GeneratedRoot() => Path.Combine(
            m_ProjectRoot,
            RevenueCatBuildConfigurationLeaseFactory.GeneratedAssetDirectory
                .Replace('/', Path.DirectorySeparatorChar));

        private string ConfigurationPath() => Path.Combine(
            m_ProjectRoot,
            RevenueCatBuildConfigurationLeaseFactory.ConfigurationAssetPath
                .Replace('/', Path.DirectorySeparatorChar));
    }
}
