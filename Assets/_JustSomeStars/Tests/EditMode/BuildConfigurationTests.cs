using System;
using System.Linq;
using JustSomeStars.Editor.Build;
using NUnit.Framework;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class BuildConfigurationTests
    {
        private static readonly string[] VariantSymbols =
        {
            "JSS_DEVELOPMENT",
            "JSS_GOOGLE_PLAY",
            "JSS_GALAXY",
        };

        [TestCase(BuildTargetKind.GooglePlay, "com.scientificaj.justsomestars", "JSS_GOOGLE_PLAY")]
        [TestCase(BuildTargetKind.Galaxy, "com.scientificaj.justsomestars.galaxy", "JSS_GALAXY")]
        public void Resolve_ReturnsStoreSpecificIdentity(
            BuildTargetKind kind, string packageId, string requiredSymbol)
        {
            var result = BuildConfiguration.Resolve(kind, buildNumber: 42);

            Assert.That(result.PackageId, Is.EqualTo(packageId));
            Assert.That(result.DefineSymbols, Does.Contain(requiredSymbol));
            Assert.That(result.VersionCode, Is.EqualTo(42));
        }

        [TestCase(
            BuildTargetKind.AndroidInternal,
            "com.scientificaj.justsomestars",
            "JSS_DEVELOPMENT",
            "Builds/AndroidInternal/JustSomeStars-internal.apk",
            false,
            true,
            true,
            false)]
        [TestCase(
            BuildTargetKind.GooglePlay,
            "com.scientificaj.justsomestars",
            "JSS_GOOGLE_PLAY",
            "Builds/GooglePlay/JustSomeStars-google-play.aab",
            true,
            false,
            false,
            true)]
        [TestCase(
            BuildTargetKind.Galaxy,
            "com.scientificaj.justsomestars.galaxy",
            "JSS_GALAXY",
            "Builds/Galaxy/JustSomeStars-galaxy.aab",
            true,
            false,
            false,
            true)]
        public void Resolve_ReturnsCompleteVariantContract(
            BuildTargetKind kind,
            string packageId,
            string variantSymbol,
            string outputPath,
            bool buildAppBundle,
            bool isDevelopmentBuild,
            bool allowDebugging,
            bool useCustomKeystore)
        {
            var result = BuildConfiguration.Resolve(kind, buildNumber: 73);

            Assert.That(result.Kind, Is.EqualTo(kind));
            Assert.That(result.PackageId, Is.EqualTo(packageId));
            Assert.That(result.VariantSymbol, Is.EqualTo(variantSymbol));
            Assert.That(result.OutputPath, Is.EqualTo(outputPath));
            Assert.That(result.BuildAppBundle, Is.EqualTo(buildAppBundle));
            Assert.That(result.IsDevelopmentBuild, Is.EqualTo(isDevelopmentBuild));
            Assert.That(result.AllowDebugging, Is.EqualTo(allowDebugging));
            Assert.That(result.UseCustomKeystore, Is.EqualTo(useCustomKeystore));
            Assert.That(result.VersionCode, Is.EqualTo(73));
            Assert.That(result.DefineSymbols.Intersect(VariantSymbols),
                Is.EqualTo(new[] { variantSymbol }));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(2_100_000_001)]
        public void Resolve_InvalidAndroidVersionCode_Throws(int buildNumber)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                BuildConfiguration.Resolve(BuildTargetKind.AndroidInternal, buildNumber));
        }

        [TestCase(1)]
        [TestCase(2_100_000_000)]
        public void Resolve_BoundaryAndroidVersionCode_IsAccepted(int buildNumber)
        {
            var result = BuildConfiguration.Resolve(BuildTargetKind.AndroidInternal, buildNumber);

            Assert.That(result.VersionCode, Is.EqualTo(buildNumber));
        }

        [TestCase(BuildTargetKind.AndroidInternal, "JSS_DEVELOPMENT")]
        [TestCase(BuildTargetKind.GooglePlay, "JSS_GOOGLE_PLAY")]
        [TestCase(BuildTargetKind.Galaxy, "JSS_GALAXY")]
        public void Resolve_ProducesOnlyTheSelectedEphemeralVariantSymbol(
            BuildTargetKind kind, string expectedVariantSymbol)
        {
            var result = BuildConfiguration.Resolve(kind, 11);

            Assert.That(result.DefineSymbols,
                Is.EqualTo(new[] { expectedVariantSymbol }));
        }

        [TestCase("JSS_DEVELOPMENT", "JSS_GOOGLE_PLAY")]
        [TestCase("JSS_DEVELOPMENT", "JSS_GALAXY")]
        [TestCase("JSS_GOOGLE_PLAY", "JSS_GALAXY")]
        public void ValidateDefineSymbols_MultipleVariants_Throws(
            string firstVariant, string secondVariant)
        {
            Assert.Throws<InvalidOperationException>(() =>
                BuildConfiguration.ValidateDefineSymbols(new[]
                {
                    "FEATURE_ALPHA",
                    firstVariant,
                    secondVariant,
                }));
        }

        [Test]
        public void ValidateDefineSymbols_NoVariant_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                BuildConfiguration.ValidateDefineSymbols(new[] { "FEATURE_ALPHA" }));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void ValidateDefineSymbols_NullEmptyOrWhitespaceToken_Throws(
            string invalidToken)
        {
            Assert.Throws<InvalidOperationException>(() =>
                BuildConfiguration.ValidateDefineSymbols(new[]
                {
                    "JSS_DEVELOPMENT",
                    invalidToken,
                }));
        }

        [TestCase("JSS_DEVELOPMENT")]
        [TestCase(" JSS_DEVELOPMENT ")]
        public void ValidateDefineSymbols_DuplicateNormalizedVariantToken_Throws(
            string duplicateVariant)
        {
            Assert.Throws<InvalidOperationException>(() =>
                BuildConfiguration.ValidateDefineSymbols(new[]
                {
                    "JSS_DEVELOPMENT",
                    duplicateVariant,
                }));
        }

        [TestCase("FEATURE_ALPHA")]
        [TestCase(" FEATURE_ALPHA ")]
        public void ValidateDefineSymbols_DuplicateNormalizedUnrelatedToken_Throws(
            string duplicateSymbol)
        {
            Assert.Throws<InvalidOperationException>(() =>
                BuildConfiguration.ValidateDefineSymbols(new[]
                {
                    "JSS_DEVELOPMENT",
                    "FEATURE_ALPHA",
                    duplicateSymbol,
                }));
        }

        [TestCase("FEATURE_ALPHA;FEATURE_BETA")]
        [TestCase("JSS_GOOGLE_PLAY;FEATURE_ALPHA")]
        [TestCase("FEATURE_ALPHA;")]
        public void ValidateDefineSymbols_SemicolonPackedToken_Throws(
            string packedToken)
        {
            Assert.Throws<InvalidOperationException>(() =>
                BuildConfiguration.ValidateDefineSymbols(new[]
                {
                    "JSS_DEVELOPMENT",
                    packedToken,
                }));
        }

        [Test]
        public void ValidateDefineSymbols_UniqueNormalizedTokensWithOneVariant_AreAccepted()
        {
            Assert.DoesNotThrow(() =>
                BuildConfiguration.ValidateDefineSymbols(new[]
                {
                    " FEATURE_ALPHA ",
                    " JSS_GOOGLE_PLAY ",
                    "FEATURE_BETA",
                }));
        }

        [Test]
        public void Resolve_UnknownTargetKind_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                BuildConfiguration.Resolve((BuildTargetKind)999, 1));
        }

        [Test]
        public void BuildScenePlan_NoEnabledScenes_UsesTemporaryGeneratedScene()
        {
            const string temporaryScenePath =
                "Assets/_JustSomeStars/GeneratedBuild/Task3EmptyBuildScene.unity";

            var result = BuildScenePlan.Resolve(
                Array.Empty<string>(),
                temporaryScenePath);

            Assert.That(result.RequiresTemporaryScene, Is.True);
            Assert.That(result.ScenePaths, Is.EqualTo(new[] { temporaryScenePath }));
        }

        [Test]
        public void BuildScenePlan_EnabledScenes_PreservesUniquePathsInOrder()
        {
            var result = BuildScenePlan.Resolve(
                new[]
                {
                    "Assets/_JustSomeStars/Scenes/Core/Boot.unity",
                    "",
                    "Assets/_JustSomeStars/Scenes/Core/Frontend.unity",
                    "Assets/_JustSomeStars/Scenes/Core/Boot.unity",
                },
                "Assets/_JustSomeStars/GeneratedBuild/Task3EmptyBuildScene.unity");

            Assert.That(result.RequiresTemporaryScene, Is.False);
            Assert.That(result.ScenePaths, Is.EqualTo(new[]
            {
                "Assets/_JustSomeStars/Scenes/Core/Boot.unity",
                "Assets/_JustSomeStars/Scenes/Core/Frontend.unity",
            }));
        }
    }
}
