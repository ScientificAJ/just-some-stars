using System;
using JustSomeStars.Editor.Build;
using NUnit.Framework;
using UnityEditor;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class BuildInvocationTests
    {
        [Test]
        public void AndroidTargetGuard_UnsupportedTarget_Throws()
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                AndroidBuildTargetGuard.Validate(
                    androidSupportInstalled: false,
                    activeBuildTarget: BuildTarget.Android));

            Assert.That(exception.Message, Does.Contain("Android Build Support"));
        }

        [Test]
        public void AndroidTargetGuard_NonAndroidTarget_RequiresCliBuildTarget()
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                AndroidBuildTargetGuard.Validate(
                    androidSupportInstalled: true,
                    activeBuildTarget: BuildTarget.StandaloneLinux64));

            Assert.That(exception.Message, Does.Contain("-buildTarget Android"));
        }

        [Test]
        public void AndroidTargetGuard_ActiveAndroidTarget_IsAccepted()
        {
            Assert.DoesNotThrow(() =>
                AndroidBuildTargetGuard.Validate(
                    androidSupportInstalled: true,
                    activeBuildTarget: BuildTarget.Android));
        }

        [TestCase(BuildTargetKind.AndroidInternal, "JSS_DEVELOPMENT")]
        [TestCase(BuildTargetKind.GooglePlay, "JSS_GOOGLE_PLAY")]
        [TestCase(BuildTargetKind.Galaxy, "JSS_GALAXY")]
        public void CreatePlayerOptions_UsesOneEphemeralVariantDefine(
            BuildTargetKind kind,
            string expectedVariant)
        {
            var configuration = BuildConfiguration.Resolve(kind, 42);
            var persistentSymbols = new[] { "FEATURE_ALPHA", "FEATURE_BETA" };
            var originalSymbols = (string[])persistentSymbols.Clone();

            var options = BuildPlayerOptionsFactory.Create(
                configuration,
                "/project/Builds/variant/JustSomeStars.jss-staging.apk",
                new[] { "Assets/Scene.unity" },
                persistentSymbols);

            Assert.That(options.target, Is.EqualTo(BuildTarget.Android));
            Assert.That(options.targetGroup, Is.EqualTo(BuildTargetGroup.Android));
            Assert.That(options.extraScriptingDefines, Is.EqualTo(new[] { expectedVariant }));
            Assert.That(options.scenes, Is.EqualTo(new[] { "Assets/Scene.unity" }));
            Assert.That(persistentSymbols, Is.EqualTo(originalSymbols));
        }

        [TestCase("JSS_DEVELOPMENT")]
        [TestCase(" JSS_GOOGLE_PLAY ")]
        [TestCase("JSS_GALAXY")]
        public void CreatePlayerOptions_PersistentJssVariant_Throws(string persistentVariant)
        {
            var configuration = BuildConfiguration.Resolve(
                BuildTargetKind.AndroidInternal,
                42);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                BuildPlayerOptionsFactory.Create(
                    configuration,
                    "/project/Builds/AndroidInternal/JustSomeStars-internal.jss-staging.apk",
                    new[] { "Assets/Scene.unity" },
                    new[] { "FEATURE_ALPHA", persistentVariant }));

            Assert.That(exception.Message, Does.Contain(persistentVariant.Trim()));
        }

        [Test]
        public void CreatePlayerOptions_InternalBuild_UsesDevelopmentAndDebugging()
        {
            var configuration = BuildConfiguration.Resolve(
                BuildTargetKind.AndroidInternal,
                42);

            var options = BuildPlayerOptionsFactory.Create(
                configuration,
                "/project/Builds/AndroidInternal/JustSomeStars-internal.jss-staging.apk",
                new[] { "Assets/Scene.unity" },
                Array.Empty<string>());

            Assert.That(options.options & BuildOptions.Development, Is.Not.Zero);
            Assert.That(options.options & BuildOptions.AllowDebugging, Is.Not.Zero);
            Assert.That(options.options & BuildOptions.DetailedBuildReport, Is.Not.Zero);
        }

        [TestCase(BuildTargetKind.GooglePlay)]
        [TestCase(BuildTargetKind.Galaxy)]
        public void CreatePlayerOptions_ReleaseBuild_IsNotDevelopmentOrDebugging(
            BuildTargetKind kind)
        {
            var configuration = BuildConfiguration.Resolve(kind, 42);

            var options = BuildPlayerOptionsFactory.Create(
                configuration,
                "/project/Builds/release/JustSomeStars.jss-staging.aab",
                new[] { "Assets/Scene.unity" },
                Array.Empty<string>());

            Assert.That(
                options.options & BuildOptions.Development,
                Is.EqualTo(BuildOptions.None));
            Assert.That(
                options.options & BuildOptions.AllowDebugging,
                Is.EqualTo(BuildOptions.None));
            Assert.That(options.options & BuildOptions.DetailedBuildReport, Is.Not.Zero);
        }
    }
}
