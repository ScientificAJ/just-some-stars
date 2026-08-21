using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using JustSomeStars.Editor.Build;
using NUnit.Framework;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class BuildArtifactTransactionTests
    {
        [TestCase(
            "Builds/AndroidInternal/JustSomeStars-internal.apk",
            "JustSomeStars-internal.jss-staging.apk")]
        [TestCase(
            "Builds/GooglePlay/JustSomeStars-google-play.aab",
            "JustSomeStars-google-play.jss-staging.aab")]
        [TestCase(
            "Builds/Galaxy/JustSomeStars-galaxy.aab",
            "JustSomeStars-galaxy.jss-staging.aab")]
        public void Begin_UsesDeterministicSameDirectoryStagingWithTrueExtension(
            string relativeFinalPath,
            string expectedStagingName)
        {
            WithTemporaryProject(projectRoot =>
            {
                using (var transaction = BuildArtifactTransaction.Begin(
                           projectRoot,
                           relativeFinalPath))
                {
                    Assert.That(
                        Path.GetDirectoryName(transaction.StagingPath),
                        Is.EqualTo(Path.GetDirectoryName(transaction.FinalPath)));
                    Assert.That(
                        Path.GetFileName(transaction.StagingPath),
                        Is.EqualTo(expectedStagingName));
                    Assert.That(
                        Path.GetExtension(transaction.StagingPath),
                        Is.EqualTo(Path.GetExtension(transaction.FinalPath)));
                }
            });
        }

        [Test]
        public void Begin_InvalidatesStaleCanonicalAndStagingBeforeLaterPreflight()
        {
            WithTemporaryProject(projectRoot =>
            {
                var finalPath = Path.Combine(
                    projectRoot,
                    "Builds/AndroidInternal/JustSomeStars-internal.apk");
                var stagingPath = Path.Combine(
                    projectRoot,
                    "Builds/AndroidInternal/JustSomeStars-internal.jss-staging.apk");
                Directory.CreateDirectory(Path.GetDirectoryName(finalPath));
                File.WriteAllBytes(finalPath, new byte[] { 1 });
                File.WriteAllBytes(stagingPath, new byte[] { 2 });

                using (BuildArtifactTransaction.Begin(
                           projectRoot,
                           "Builds/AndroidInternal/JustSomeStars-internal.apk"))
                {
                    Assert.That(File.Exists(finalPath), Is.False);
                    Assert.That(File.Exists(stagingPath), Is.False);
                }
            });
        }

        [Test]
        public void DisposeWithoutPublish_RemovesCanonicalAndPartialStaging()
        {
            WithTemporaryProject(projectRoot =>
            {
                string finalPath;
                string stagingPath;
                using (var transaction = BuildArtifactTransaction.Begin(
                           projectRoot,
                           "Builds/Galaxy/JustSomeStars-galaxy.aab"))
                {
                    finalPath = transaction.FinalPath;
                    stagingPath = transaction.StagingPath;
                    File.WriteAllBytes(stagingPath, new byte[] { 1, 2, 3 });
                    File.WriteAllBytes(finalPath, new byte[] { 4, 5, 6 });
                }

                Assert.That(File.Exists(finalPath), Is.False);
                Assert.That(File.Exists(stagingPath), Is.False);
            });
        }

        [Test]
        public void Publish_AtomicallyMovesValidatedStagingToCanonical()
        {
            WithTemporaryProject(projectRoot =>
            {
                string finalPath;
                string stagingPath;
                using (var transaction = BuildArtifactTransaction.Begin(
                           projectRoot,
                           "Builds/GooglePlay/JustSomeStars-google-play.aab"))
                {
                    finalPath = transaction.FinalPath;
                    stagingPath = transaction.StagingPath;
                    File.WriteAllBytes(stagingPath, new byte[] { 7, 8, 9 });

                    transaction.Publish();
                }

                Assert.That(File.ReadAllBytes(finalPath), Is.EqualTo(new byte[] { 7, 8, 9 }));
                Assert.That(File.Exists(stagingPath), Is.False);
            });
        }

        [Test]
        public void Publish_EmptyStaging_ThrowsAndLeavesNoArtifact()
        {
            WithTemporaryProject(projectRoot =>
            {
                string finalPath;
                string stagingPath;
                using (var transaction = BuildArtifactTransaction.Begin(
                           projectRoot,
                           "Builds/AndroidInternal/JustSomeStars-internal.apk"))
                {
                    finalPath = transaction.FinalPath;
                    stagingPath = transaction.StagingPath;
                    File.WriteAllBytes(stagingPath, Array.Empty<byte>());

                    Assert.Throws<InvalidOperationException>(() => transaction.Publish());
                }

                Assert.That(File.Exists(finalPath), Is.False);
                Assert.That(File.Exists(stagingPath), Is.False);
            });
        }

        [Test]
        public void Publish_UsesOneSameFilesystemMoveRatherThanCopyDelete()
        {
            var fileSystem = new RecordingArtifactFileSystem();
            const string projectRoot = "/project";
            using (var transaction = BuildArtifactTransaction.Begin(
                       projectRoot,
                       "Builds/GooglePlay/JustSomeStars-google-play.aab",
                       fileSystem))
            {
                fileSystem.AddFile(transaction.StagingPath, length: 3);

                transaction.Publish();

                Assert.That(fileSystem.MoveCalls, Is.EqualTo(new[]
                {
                    (transaction.StagingPath, transaction.FinalPath),
                }));
                Assert.That(
                    Path.GetDirectoryName(transaction.StagingPath),
                    Is.EqualTo(Path.GetDirectoryName(transaction.FinalPath)));
                Assert.That(fileSystem.Exists(transaction.FinalPath), Is.True);
                Assert.That(fileSystem.Exists(transaction.StagingPath), Is.False);
            }
        }

        [TestCase("../outside.apk")]
        [TestCase("Assets/not-a-build.apk")]
        [TestCase("Builds/AndroidInternal/not-an-android-artifact.zip")]
        public void Begin_UnsafeOrUnsupportedOutput_ThrowsWithoutTouchingOutsideFile(
            string relativePath)
        {
            WithTemporaryProject(projectRoot =>
            {
                var outsidePath = Path.Combine(projectRoot, "outside.apk");
                File.WriteAllBytes(outsidePath, new byte[] { 9 });

                Assert.Throws<InvalidOperationException>(() =>
                    BuildArtifactTransaction.Begin(projectRoot, relativePath));

                Assert.That(File.ReadAllBytes(outsidePath), Is.EqualTo(new byte[] { 9 }));
            });
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Begin_SymlinkedOutputComponent_ThrowsWithoutTouchingExternalArtifact(
            bool linkBuildsRoot)
        {
            if (Environment.OSVersion.Platform != PlatformID.Unix)
            {
                return;
            }

            WithTemporaryProject(projectRoot =>
            {
                var externalRoot = Path.Combine(
                    Path.GetTempPath(),
                    "jss-artifact-external-" + Guid.NewGuid().ToString("N"));
                var linkPath = linkBuildsRoot
                    ? Path.Combine(projectRoot, "Builds")
                    : Path.Combine(projectRoot, "Builds/AndroidInternal");
                var linkTarget = linkBuildsRoot
                    ? externalRoot
                    : Path.Combine(externalRoot, "AndroidInternal");
                var externalOutputDirectory = Path.Combine(
                    externalRoot,
                    "AndroidInternal");
                var externalArtifact = Path.Combine(
                    externalOutputDirectory,
                    "JustSomeStars-internal.apk");
                Directory.CreateDirectory(externalOutputDirectory);
                if (!linkBuildsRoot)
                {
                    Directory.CreateDirectory(Path.Combine(projectRoot, "Builds"));
                }

                Assert.That(CreateSymbolicLink(linkTarget, linkPath), Is.EqualTo(0),
                    "The Linux symlink test fixture could not be created. errno=" +
                    Marshal.GetLastWin32Error());
                File.WriteAllBytes(externalArtifact, new byte[] { 9, 8, 7 });
                try
                {
                    Assert.Throws<InvalidOperationException>(() =>
                        BuildArtifactTransaction.Begin(
                            projectRoot,
                            "Builds/AndroidInternal/JustSomeStars-internal.apk"));

                    Assert.That(File.ReadAllBytes(externalArtifact),
                        Is.EqualTo(new byte[] { 9, 8, 7 }));
                }
                finally
                {
                    if (Directory.Exists(linkPath))
                    {
                        Directory.Delete(linkPath);
                    }

                    if (Directory.Exists(externalRoot))
                    {
                        Directory.Delete(externalRoot, true);
                    }
                }
            });
        }

        private static void WithTemporaryProject(Action<string> action)
        {
            var projectRoot = Path.Combine(
                Path.GetTempPath(),
                "jss-artifact-transaction-tests-" + Guid.NewGuid().ToString("N"));
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

        private sealed class RecordingArtifactFileSystem : IBuildArtifactFileSystem
        {
            private readonly Dictionary<string, long> m_Files =
                new Dictionary<string, long>(StringComparer.Ordinal);

            public List<(string Source, string Destination)> MoveCalls { get; } =
                new List<(string Source, string Destination)>();

            public void AddFile(string path, long length)
            {
                m_Files[path] = length;
            }

            public bool Exists(string path)
            {
                return m_Files.ContainsKey(path);
            }

            public long GetLength(string path)
            {
                return m_Files[path];
            }

            public void CreateDirectory(string path)
            {
            }

            public void DeleteFile(string path)
            {
                m_Files.Remove(path);
            }

            public void MoveFile(string sourcePath, string destinationPath)
            {
                var length = m_Files[sourcePath];
                m_Files.Remove(sourcePath);
                m_Files[destinationPath] = length;
                MoveCalls.Add((sourcePath, destinationPath));
            }
        }

        [DllImport("libc", EntryPoint = "symlink", SetLastError = true)]
        private static extern int CreateSymbolicLink(
            string target,
            string linkPath);
    }
}
