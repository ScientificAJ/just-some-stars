using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JustSomeStars.Editor.Build;
using NUnit.Framework;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class SigningSecretScrubberTests
    {
        private const string FakeSecret = "JSS_TEST_SENTINEL_NOT_A_REAL_SECRET";
        private const string FakeEscapableSecret =
            "JSS_TEST_SENTINEL_QUOTE\"_SLASH\\_COLON:=#!";
        private const string JsonEscapedSecret =
            "JSS_TEST_SENTINEL_QUOTE\\\"_SLASH\\\\_COLON:=#!";
        private const string GradleEscapedSecret =
            "JSS_TEST_SENTINEL_QUOTE\"_SLASH\\\\_COLON\\:\\=\\#\\!";
        private const string FakeComplexSecret =
            " JSS quote\" slash/ back\\ inner space\tline\né中:=#!";
        private const string JsonMinimalComplexSecret =
            " JSS quote\\\" slash/ back\\\\ inner space\\tline\\né中:=#!";
        private const string JsonFullyEscapedLowerComplexSecret =
            " JSS quote\\\" slash\\/ back\\\\ inner space\\tline\\n" +
            "\\u00e9\\u4e2d:=#!";
        private const string JsonFullyEscapedUpperComplexSecret =
            " JSS quote\\\" slash\\/ back\\\\ inner space\\tline\\n" +
            "\\u00E9\\u4E2D:=#!";
        private const string JavaPropertiesLowerComplexSecret =
            "\\ JSS quote\" slash/ back\\\\ inner space\\tline\\n" +
            "\\u00e9\\u4e2d\\:\\=\\#\\!";
        private const string JavaPropertiesUpperComplexSecret =
            "\\ JSS quote\" slash/ back\\\\ inner space\\tline\\n" +
            "\\u00E9\\u4E2D\\:\\=\\#\\!";

        [TestCase("Library/Bee/Android/Prj/gradle.properties")]
        [TestCase("Library/BuildPlayerData/fake-signing-cache.bin")]
        [TestCase("Library/Il2cppBuildCache/fake-signing-cache.bin")]
        [TestCase("Library/PlayerDataCache/fake-signing-cache.bin")]
        [TestCase("Temp/StagingArea/gradle.properties")]
        public void ScrubAndVerify_FakeSecretInBoundedVolatileCache_DeletesContainingFile(
            string relativeCachePath)
        {
            WithTemporaryProject(projectRoot =>
            {
                var cachePath = Path.Combine(projectRoot, relativeCachePath);
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
                File.WriteAllText(
                    cachePath,
                    "unrelated-prefix=" + FakeSecret + "-store");

                new SigningSecretScrubber().ScrubAndVerify(
                    projectRoot,
                    CreateCredentials());

                Assert.That(File.Exists(cachePath), Is.False);
            });
        }

        [Test]
        public void ScrubAndVerify_NextInvocationWithSameCredentials_RemovesCrashResidueAgain()
        {
            WithTemporaryProject(projectRoot =>
            {
                var cachePath = Path.Combine(
                    projectRoot,
                    "Library/Bee/Android/Prj/gradle.properties");
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
                var scrubber = new SigningSecretScrubber();
                var credentials = CreateCredentials();

                File.WriteAllText(cachePath, FakeSecret + "-alias");
                scrubber.ScrubAndVerify(projectRoot, credentials);
                File.WriteAllText(cachePath, FakeSecret + "-alias");
                scrubber.ScrubAndVerify(projectRoot, credentials);

                Assert.That(File.Exists(cachePath), Is.False);
            });
        }

        [Test]
        public void ScrubAndVerify_SecretOutsideBoundedCaches_IsNotRewritten()
        {
            WithTemporaryProject(projectRoot =>
            {
                var sourcePath = Path.Combine(projectRoot, "Assets/do-not-rewrite.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(sourcePath));
                File.WriteAllText(sourcePath, FakeSecret + "-store");

                new SigningSecretScrubber().ScrubAndVerify(
                    projectRoot,
                    CreateCredentials());

                Assert.That(File.ReadAllText(sourcePath),
                    Is.EqualTo(FakeSecret + "-store"));
            });
        }

        [TestCase(JsonEscapedSecret)]
        [TestCase(GradleEscapedSecret)]
        public void ScrubAndVerify_SerializedSecretFormInVolatileCache_DeletesFile(
            string serializedSecret)
        {
            WithTemporaryProject(projectRoot =>
            {
                var cachePath = Path.Combine(
                    projectRoot,
                    "Library/Bee/Android/Prj/serialized-signing-cache.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
                File.WriteAllText(cachePath, "value=" + serializedSecret);
                var credentials = new ReleaseSigningCredentials(
                    "/project/fake-release.jks",
                    FakeEscapableSecret,
                    "jss-test-alias",
                    FakeSecret + "-alias");

                new SigningSecretScrubber().ScrubAndVerify(
                    projectRoot,
                    credentials);

                Assert.That(File.Exists(cachePath), Is.False);
            });
        }

        [TestCase(JsonMinimalComplexSecret)]
        [TestCase(JsonFullyEscapedLowerComplexSecret)]
        [TestCase(JsonFullyEscapedUpperComplexSecret)]
        [TestCase(JavaPropertiesLowerComplexSecret)]
        [TestCase(JavaPropertiesUpperComplexSecret)]
        public void ScrubAndVerify_JsonOrJavaPropertiesComplexSecret_DeletesFile(
            string serializedSecret)
        {
            WithTemporaryProject(projectRoot =>
            {
                var cachePath = Path.Combine(
                    projectRoot,
                    "Library/Bee/Android/Prj/complex-serialized-signing-cache.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
                File.WriteAllText(cachePath, "value=" + serializedSecret);
                var credentials = new ReleaseSigningCredentials(
                    "/project/fake-release.jks",
                    FakeComplexSecret,
                    "jss-test-alias",
                    FakeSecret + "-alias");

                new SigningSecretScrubber().ScrubAndVerify(
                    projectRoot,
                    credentials);

                Assert.That(File.Exists(cachePath), Is.False);
            });
        }

        [Test]
        public void ScrubAndVerify_UnableToProveRemoval_ThrowsWithoutSecretValue()
        {
            var scrubber = new SigningSecretScrubber(
                new UndeletableSecretFileAccess());

            var exception = Assert.Throws<InvalidOperationException>(() =>
                scrubber.ScrubAndVerify("/project", CreateCredentials()));

            Assert.That(exception.Message, Does.Contain("residue"));
            Assert.That(exception.Message, Does.Not.Contain(FakeSecret));
        }

        [Test]
        public void ScrubAndVerify_NoResidue_InspectsEachFileOnlyOnce()
        {
            var fileAccess = new CleanCountingSecretFileAccess();
            var scrubber = new SigningSecretScrubber(fileAccess);

            scrubber.ScrubAndVerify("/project", CreateCredentials());

            Assert.That(fileAccess.InspectionCount, Is.EqualTo(1));
            Assert.That(fileAccess.DeleteCount, Is.Zero);
            Assert.That(fileAccess.PatternCount, Is.EqualTo(4));
        }

        [Test]
        public void SystemFileAccess_SecretAcrossStreamingBoundary_IsDetected()
        {
            WithTemporaryProject(projectRoot =>
            {
                var cachePath = Path.Combine(projectRoot, "streaming-cache.bin");
                var prefix = new string('x', 64 * 1024 - 3);
                File.WriteAllText(cachePath, prefix + FakeSecret + "-store");
                var patterns = new[]
                {
                    Encoding.UTF8.GetBytes(FakeSecret + "-store"),
                };

                var containsSecret = new SystemSigningSecretFileAccess()
                    .ContainsAny(cachePath, patterns);

                Assert.That(containsSecret, Is.True);
            });
        }

        private static ReleaseSigningCredentials CreateCredentials()
        {
            return new ReleaseSigningCredentials(
                "/project/fake-release.jks",
                FakeSecret + "-store",
                "jss-test-alias",
                FakeSecret + "-alias");
        }

        private static void WithTemporaryProject(Action<string> action)
        {
            var projectRoot = Path.Combine(
                Path.GetTempPath(),
                "jss-signing-scrubber-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectRoot);
            try
            {
                action(projectRoot);
            }
            finally
            {
                if (Directory.Exists(projectRoot))
                {
                    Directory.Delete(projectRoot, true);
                }
            }
        }

        private sealed class UndeletableSecretFileAccess : ISigningSecretFileAccess
        {
            private static readonly string SecretPath =
                "/project/Library/Bee/stuck-secret.bin";

            public IEnumerable<string> EnumerateFiles(string rootPath)
            {
                return rootPath.EndsWith(
                    Path.Combine("Library", "Bee"),
                    StringComparison.Ordinal)
                    ? new[] { SecretPath }
                    : Array.Empty<string>();
            }

            public bool ContainsAny(
                string path,
                IReadOnlyList<byte[]> patterns)
            {
                return true;
            }

            public void DeleteFile(string path)
            {
                // Deliberately leave the fake file visible to the verification pass.
            }
        }

        private sealed class CleanCountingSecretFileAccess : ISigningSecretFileAccess
        {
            private const string CleanPath =
                "/project/Library/Bee/clean-cache.bin";

            public int InspectionCount { get; private set; }

            public int DeleteCount { get; private set; }

            public int PatternCount { get; private set; }

            public IEnumerable<string> EnumerateFiles(string rootPath)
            {
                return rootPath.EndsWith(
                    Path.Combine("Library", "Bee"),
                    StringComparison.Ordinal)
                    ? new[] { CleanPath }
                    : Array.Empty<string>();
            }

            public bool ContainsAny(
                string path,
                IReadOnlyList<byte[]> patterns)
            {
                InspectionCount++;
                PatternCount = patterns.Count;
                return false;
            }

            public void DeleteFile(string path)
            {
                DeleteCount++;
            }
        }
    }
}
