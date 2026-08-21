using System;
using System.Collections.Generic;
using JustSomeStars.Editor.Build;
using NUnit.Framework;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class BuildEnvironmentTests
    {
        private const string FakeSecret = "JSS_TEST_SENTINEL_NOT_A_REAL_SECRET";
        private const string GooglePathVariable =
            "JSS_GOOGLE_PLAY_ANDROID_KEYSTORE_PATH";
        private const string GoogleStorePasswordVariable =
            "JSS_GOOGLE_PLAY_ANDROID_KEYSTORE_PASSWORD";
        private const string GoogleAliasVariable =
            "JSS_GOOGLE_PLAY_ANDROID_KEY_ALIAS";
        private const string GoogleAliasPasswordVariable =
            "JSS_GOOGLE_PLAY_ANDROID_KEY_ALIAS_PASSWORD";
        private const string GalaxyPathVariable =
            "JSS_GALAXY_ANDROID_KEYSTORE_PATH";
        private const string GalaxyStorePasswordVariable =
            "JSS_GALAXY_ANDROID_KEYSTORE_PASSWORD";
        private const string GalaxyAliasVariable =
            "JSS_GALAXY_ANDROID_KEY_ALIAS";
        private const string GalaxyAliasPasswordVariable =
            "JSS_GALAXY_ANDROID_KEY_ALIAS_PASSWORD";

        [Test]
        public void ReadBuildNumber_InternalAbsentValue_UsesStableDefault()
        {
            var result = BuildEnvironment.ReadBuildNumber(
                BuildTargetKind.AndroidInternal,
                name => null);

            Assert.That(result, Is.EqualTo(1));
        }

        [TestCase(BuildTargetKind.AndroidInternal, "")]
        [TestCase(BuildTargetKind.AndroidInternal, "   ")]
        [TestCase(BuildTargetKind.GooglePlay, "")]
        [TestCase(BuildTargetKind.GooglePlay, "   ")]
        [TestCase(BuildTargetKind.Galaxy, "")]
        [TestCase(BuildTargetKind.Galaxy, "   ")]
        public void ReadBuildNumber_PresentEmptyOrWhitespaceValue_Throws(
            BuildTargetKind kind,
            string rawValue)
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                BuildEnvironment.ReadBuildNumber(kind, name => rawValue));

            Assert.That(exception.Message, Does.Contain("JSS_BUILD_NUMBER"));
        }

        [TestCase(BuildTargetKind.AndroidInternal)]
        [TestCase(BuildTargetKind.GooglePlay)]
        [TestCase(BuildTargetKind.Galaxy)]
        public void ReadBuildNumber_ValidValue_IsHonored(BuildTargetKind kind)
        {
            var variables = new Dictionary<string, string>
            {
                ["JSS_BUILD_NUMBER"] = "2100000000",
            };

            var result = BuildEnvironment.ReadBuildNumber(
                kind,
                name => Read(variables, name));

            Assert.That(result, Is.EqualTo(2_100_000_000));
        }

        [TestCase(BuildTargetKind.GooglePlay)]
        [TestCase(BuildTargetKind.Galaxy)]
        public void ReadBuildNumber_ReleaseAbsentValue_Throws(BuildTargetKind kind)
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                BuildEnvironment.ReadBuildNumber(kind, name => null));

            Assert.That(exception.Message, Does.Contain("JSS_BUILD_NUMBER"));
        }

        [TestCase(" 42")]
        [TestCase("42 ")]
        [TestCase("+42")]
        [TestCase("-1")]
        [TestCase("0")]
        [TestCase("2100000001")]
        [TestCase("2147483648")]
        [TestCase("forty-two")]
        public void ReadBuildNumber_InvalidValue_Throws(string rawValue)
        {
            var variables = new Dictionary<string, string>
            {
                ["JSS_BUILD_NUMBER"] = rawValue,
            };

            Assert.Throws<InvalidOperationException>(() =>
                BuildEnvironment.ReadBuildNumber(
                    BuildTargetKind.AndroidInternal,
                    name => Read(variables, name)));
        }

        [TestCase(BuildTargetKind.GooglePlay)]
        [TestCase(BuildTargetKind.Galaxy)]
        public void ReadSigning_CompleteStoreSpecificSet_IsReturned(BuildTargetKind kind)
        {
            var variables = CompleteSigningVariables(kind);
            var expectedPath = kind == BuildTargetKind.GooglePlay
                ? "/project/google-release.jks"
                : "/project/galaxy-release.jks";

            var result = BuildEnvironment.ReadSigning(
                kind,
                name => Read(variables, name),
                path => string.Equals(path, expectedPath, StringComparison.Ordinal),
                path => "/project/" + path);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.KeystorePath, Is.EqualTo(expectedPath));
            Assert.That(result.KeystorePassword, Is.EqualTo(FakeSecret + "-store"));
            Assert.That(result.KeyAlias, Is.EqualTo("jss-test-alias"));
            Assert.That(result.KeyAliasPassword, Is.EqualTo(FakeSecret + "-alias"));
            Assert.That(result.ToString(), Does.Not.Contain(FakeSecret));
        }

        [TestCase(BuildTargetKind.GooglePlay, GooglePathVariable)]
        [TestCase(BuildTargetKind.GooglePlay, GoogleStorePasswordVariable)]
        [TestCase(BuildTargetKind.GooglePlay, GoogleAliasVariable)]
        [TestCase(BuildTargetKind.GooglePlay, GoogleAliasPasswordVariable)]
        [TestCase(BuildTargetKind.Galaxy, GalaxyPathVariable)]
        [TestCase(BuildTargetKind.Galaxy, GalaxyStorePasswordVariable)]
        [TestCase(BuildTargetKind.Galaxy, GalaxyAliasVariable)]
        [TestCase(BuildTargetKind.Galaxy, GalaxyAliasPasswordVariable)]
        public void ReadSigning_MissingOrWhitespaceStoreValue_ThrowsWithoutSecret(
            BuildTargetKind kind,
            string missingVariable)
        {
            var variables = CompleteSigningVariables(kind);
            variables[missingVariable] = "   ";

            var exception = Assert.Throws<InvalidOperationException>(() =>
                BuildEnvironment.ReadSigning(
                    kind,
                    name => Read(variables, name),
                    _ => true,
                    path => "/project/" + path));

            Assert.That(exception.Message, Does.Contain(missingVariable));
            Assert.That(exception.Message, Does.Not.Contain(FakeSecret));
        }

        [TestCase(BuildTargetKind.GooglePlay)]
        [TestCase(BuildTargetKind.Galaxy)]
        public void ReadSigning_MissingKeystoreFile_ThrowsWithoutSecret(BuildTargetKind kind)
        {
            var variables = CompleteSigningVariables(kind);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                BuildEnvironment.ReadSigning(
                    kind,
                    name => Read(variables, name),
                    _ => false,
                    path => "/project/" + path));

            Assert.That(exception.Message, Does.Contain("keystore"));
            Assert.That(exception.Message, Does.Not.Contain(FakeSecret));
        }

        [TestCase(BuildTargetKind.GooglePlay, GoogleStorePasswordVariable)]
        [TestCase(BuildTargetKind.GooglePlay, GoogleAliasPasswordVariable)]
        [TestCase(BuildTargetKind.Galaxy, GalaxyStorePasswordVariable)]
        [TestCase(BuildTargetKind.Galaxy, GalaxyAliasPasswordVariable)]
        public void ReadSigning_ShortPassword_ThrowsBeforePathOrFileInspection(
            BuildTargetKind kind,
            string passwordVariable)
        {
            var variables = CompleteSigningVariables(kind);
            variables[passwordVariable] = "too-short";

            var exception = Assert.Throws<InvalidOperationException>(() =>
                BuildEnvironment.ReadSigning(
                    kind,
                    name => Read(variables, name),
                    _ => throw new AssertionException(
                        "Short passwords must fail before filesystem inspection."),
                    _ => throw new AssertionException(
                        "Short passwords must fail before path resolution.")));

            Assert.That(exception.Message, Does.Contain(passwordVariable));
            Assert.That(exception.Message, Does.Contain("12"));
            Assert.That(exception.Message, Does.Not.Contain("too-short"));
        }

        [TestCase(BuildTargetKind.GooglePlay)]
        [TestCase(BuildTargetKind.Galaxy)]
        public void ReadSigning_InvalidKeystorePath_ThrowsWithoutSecretTrace(
            BuildTargetKind kind)
        {
            var variables = CompleteSigningVariables(kind);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                BuildEnvironment.ReadSigning(
                    kind,
                    name => Read(variables, name),
                    _ => true,
                    _ => throw new ArgumentException(
                        "fake path parser failure " + FakeSecret)));

            Assert.That(exception.ToString(), Does.Contain("keystore path"));
            Assert.That(exception.ToString(), Does.Not.Contain(FakeSecret));
        }

        [TestCase(BuildTargetKind.GooglePlay, GalaxyPathVariable)]
        [TestCase(BuildTargetKind.GooglePlay, GalaxyStorePasswordVariable)]
        [TestCase(BuildTargetKind.GooglePlay, GalaxyAliasVariable)]
        [TestCase(BuildTargetKind.GooglePlay, GalaxyAliasPasswordVariable)]
        [TestCase(BuildTargetKind.Galaxy, GooglePathVariable)]
        [TestCase(BuildTargetKind.Galaxy, GoogleStorePasswordVariable)]
        [TestCase(BuildTargetKind.Galaxy, GoogleAliasVariable)]
        [TestCase(BuildTargetKind.Galaxy, GoogleAliasPasswordVariable)]
        public void ReadSigning_OtherStoreVariableIsPopulated_Throws(
            BuildTargetKind kind,
            string otherStoreVariable)
        {
            var variables = CompleteSigningVariables(kind);
            variables[otherStoreVariable] = FakeSecret;

            var exception = Assert.Throws<InvalidOperationException>(() =>
                BuildEnvironment.ReadSigning(
                    kind,
                    name => Read(variables, name),
                    _ => true,
                    path => "/project/" + path));

            Assert.That(exception.Message, Does.Contain(otherStoreVariable));
            Assert.That(exception.Message, Does.Not.Contain(FakeSecret));
        }

        [Test]
        public void ReadSigning_InternalBuild_IgnoresBothStoreSets()
        {
            var variables = CompleteSigningVariables(BuildTargetKind.GooglePlay);
            foreach (var pair in CompleteSigningVariables(BuildTargetKind.Galaxy))
            {
                variables[pair.Key] = pair.Value;
            }

            var result = BuildEnvironment.ReadSigning(
                BuildTargetKind.AndroidInternal,
                name => Read(variables, name),
                _ => throw new AssertionException("Internal signing must not inspect files."),
                _ => throw new AssertionException("Internal signing must not resolve paths."));

            Assert.That(result, Is.Null);
        }

        private static Dictionary<string, string> CompleteSigningVariables(
            BuildTargetKind kind)
        {
            if (kind == BuildTargetKind.GooglePlay)
            {
                return new Dictionary<string, string>
                {
                    [GooglePathVariable] = "google-release.jks",
                    [GoogleStorePasswordVariable] = FakeSecret + "-store",
                    [GoogleAliasVariable] = "jss-test-alias",
                    [GoogleAliasPasswordVariable] = FakeSecret + "-alias",
                };
            }

            return new Dictionary<string, string>
            {
                [GalaxyPathVariable] = "galaxy-release.jks",
                [GalaxyStorePasswordVariable] = FakeSecret + "-store",
                [GalaxyAliasVariable] = "jss-test-alias",
                [GalaxyAliasPasswordVariable] = FakeSecret + "-alias",
            };
        }

        private static string Read(
            IReadOnlyDictionary<string, string> variables,
            string name)
        {
            return variables.TryGetValue(name, out var value) ? value : null;
        }
    }
}
