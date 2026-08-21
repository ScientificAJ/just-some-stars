using System;
using System.IO;
using JustSomeStars.Editor.Build;
using NUnit.Framework;
using UnityEditor.Build.Reporting;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class BuildResultValidationTests
    {
        [Test]
        public void ValidateBuildReport_NullReport_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                BuildReportValidator.Validate((BuildReport)null));
        }

        [TestCase(BuildResult.Cancelled)]
        [TestCase(BuildResult.Failed)]
        [TestCase(BuildResult.Unknown)]
        public void ValidateBuildReport_NonSuccessResult_Throws(BuildResult result)
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                BuildReportValidator.Validate(result));

            Assert.That(exception.Message, Does.Contain(result.ToString()));
        }

        [Test]
        public void ValidateBuildReport_SucceededResult_IsAccepted()
        {
            Assert.DoesNotThrow(() =>
                BuildReportValidator.Validate(BuildResult.Succeeded));
        }

        [Test]
        public void ValidateArtifact_MissingFile_Throws()
        {
            WithTemporaryDirectory(directory =>
            {
                Assert.Throws<InvalidOperationException>(() =>
                    BuildArtifactValidator.Validate(
                        Path.Combine(directory, "missing.apk"),
                        ".apk"));
            });
        }

        [Test]
        public void ValidateArtifact_EmptyFile_Throws()
        {
            WithTemporaryDirectory(directory =>
            {
                var path = Path.Combine(directory, "empty.apk");
                File.WriteAllBytes(path, Array.Empty<byte>());

                Assert.Throws<InvalidOperationException>(() =>
                    BuildArtifactValidator.Validate(path, ".apk"));
            });
        }

        [Test]
        public void ValidateArtifact_WrongExtension_Throws()
        {
            WithTemporaryDirectory(directory =>
            {
                var path = Path.Combine(directory, "artifact.tmp");
                File.WriteAllBytes(path, new byte[] { 1 });

                Assert.Throws<InvalidOperationException>(() =>
                    BuildArtifactValidator.Validate(path, ".apk"));
            });
        }

        [TestCase("artifact.apk", ".apk")]
        [TestCase("artifact.aab", ".aab")]
        public void ValidateArtifact_NonEmptyCorrectExtension_IsAccepted(
            string fileName,
            string extension)
        {
            WithTemporaryDirectory(directory =>
            {
                var path = Path.Combine(directory, fileName);
                File.WriteAllBytes(path, new byte[] { 1, 2, 3 });

                Assert.DoesNotThrow(() =>
                    BuildArtifactValidator.Validate(path, extension));
            });
        }

        private static void WithTemporaryDirectory(Action<string> action)
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "jss-build-validation-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                action(directory);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }
    }
}
