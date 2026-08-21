using System;
using System.IO;
using System.Runtime.InteropServices;
using JustSomeStars.Editor.Build;
using NUnit.Framework;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class BuildSceneLeaseTests
    {
        private const string GeneratedFolder =
            "Assets/_JustSomeStars/GeneratedBuild";
        private const string PreparationFolder =
            "Temp/JssBuildSceneLease";
        private const string OwnershipMarker = ".jss-build-scene-owner";
        private const string OwnershipMarkerContents =
            "JustSomeStars.BuildSceneLease:v1\n";

        [Test]
        public void RecoverCrashResidue_KnownOwnedGeneratedContent_IsRemoved()
        {
            WithTemporaryProject(projectRoot =>
            {
                var folder = CreateOwnedResidue(projectRoot);
                File.WriteAllText(
                    Path.Combine(folder, "Task3EmptyBuildScene.unity"),
                    "fake test scene");
                File.WriteAllText(
                    Path.Combine(folder, "Task3EmptyBuildScene.unity.meta"),
                    "fake test meta");
                File.WriteAllText(folder + ".meta", "fake folder meta");

                BuildSceneLease.RecoverCrashResidue(projectRoot);

                Assert.That(Directory.Exists(folder), Is.False);
                Assert.That(File.Exists(folder + ".meta"), Is.False);
            });
        }

        [Test]
        public void RecoverCrashResidue_OwnedPartialCreation_IsRemoved()
        {
            WithTemporaryProject(projectRoot =>
            {
                var folder = CreateOwnedResidue(projectRoot);

                BuildSceneLease.RecoverCrashResidue(projectRoot);

                Assert.That(Directory.Exists(folder), Is.False);
            });
        }

        [Test]
        public void RecoverCrashResidue_UnknownFile_ThrowsWithoutDeletingFolder()
        {
            WithTemporaryProject(projectRoot =>
            {
                var folder = CreateOwnedResidue(projectRoot);
                var unknownPath = Path.Combine(folder, "do-not-delete.txt");
                File.WriteAllText(unknownPath, "user content");

                var exception = Assert.Throws<InvalidOperationException>(() =>
                    BuildSceneLease.RecoverCrashResidue(projectRoot));

                Assert.That(exception.Message, Does.Contain("unknown"));
                Assert.That(File.ReadAllText(unknownPath), Is.EqualTo("user content"));
            });
        }

        [Test]
        public void RecoverCrashResidue_MissingOwnershipMarker_ThrowsWithoutDeletingFolder()
        {
            WithTemporaryProject(projectRoot =>
            {
                var folder = Path.Combine(projectRoot, GeneratedFolder);
                Directory.CreateDirectory(folder);
                var scenePath = Path.Combine(folder, "Task3EmptyBuildScene.unity");
                File.WriteAllText(scenePath, "unknown owner");

                Assert.Throws<InvalidOperationException>(() =>
                    BuildSceneLease.RecoverCrashResidue(projectRoot));

                Assert.That(File.Exists(scenePath), Is.True);
            });
        }

        [Test]
        public void RecoverCrashResidue_InvalidOwnershipMarker_ThrowsWithoutDeletingFolder()
        {
            WithTemporaryProject(projectRoot =>
            {
                var folder = Path.Combine(projectRoot, GeneratedFolder);
                Directory.CreateDirectory(folder);
                var markerPath = Path.Combine(folder, OwnershipMarker);
                File.WriteAllText(markerPath, "someone-else:v1\n");

                Assert.Throws<InvalidOperationException>(() =>
                    BuildSceneLease.RecoverCrashResidue(projectRoot));

                Assert.That(File.ReadAllText(markerPath), Is.EqualTo("someone-else:v1\n"));
            });
        }

        [Test]
        public void RecoverCrashResidue_NoGeneratedFolder_IsNoOp()
        {
            WithTemporaryProject(projectRoot =>
            {
                Assert.DoesNotThrow(() =>
                    BuildSceneLease.RecoverCrashResidue(projectRoot));
            });
        }

        [Test]
        public void RecoverCrashResidue_ExactEmptyPreparationFolder_IsRemoved()
        {
            WithTemporaryProject(projectRoot =>
            {
                var preparationFolder = Path.Combine(projectRoot, PreparationFolder);
                Directory.CreateDirectory(preparationFolder);

                BuildSceneLease.RecoverCrashResidue(projectRoot);

                Assert.That(Directory.Exists(preparationFolder), Is.False);
            });
        }

        [Test]
        public void RecoverCrashResidue_PartialKnownPreparationMarker_IsRemoved()
        {
            WithTemporaryProject(projectRoot =>
            {
                var preparationFolder = Path.Combine(projectRoot, PreparationFolder);
                Directory.CreateDirectory(preparationFolder);
                File.WriteAllText(
                    Path.Combine(preparationFolder, OwnershipMarker + ".tmp"),
                    "JustSomeStars.BuildScene");

                BuildSceneLease.RecoverCrashResidue(projectRoot);

                Assert.That(Directory.Exists(preparationFolder), Is.False);
            });
        }

        [Test]
        public void RecoverCrashResidue_UnknownPreparationContent_ThrowsWithoutDeleting()
        {
            WithTemporaryProject(projectRoot =>
            {
                var preparationFolder = Path.Combine(projectRoot, PreparationFolder);
                Directory.CreateDirectory(preparationFolder);
                var unknownPath = Path.Combine(preparationFolder, "do-not-delete.txt");
                File.WriteAllText(unknownPath, "user content");

                Assert.Throws<InvalidOperationException>(() =>
                    BuildSceneLease.RecoverCrashResidue(projectRoot));

                Assert.That(File.ReadAllText(unknownPath), Is.EqualTo("user content"));
            });
        }

        [Test]
        public void RecoverCrashResidue_ReparsePreparationMarker_IsRejectedAsFilesystemLink()
        {
            if (Environment.OSVersion.Platform != PlatformID.Unix)
            {
                return;
            }

            WithTemporaryProject(projectRoot =>
            {
                var preparationFolder = Path.Combine(projectRoot, PreparationFolder);
                Directory.CreateDirectory(preparationFolder);
                var markerPath = Path.Combine(preparationFolder, OwnershipMarker);
                var externalTargetPath = Path.Combine(
                    projectRoot,
                    "external-preparation-marker.txt");
                File.WriteAllText(externalTargetPath, OwnershipMarkerContents);
                Assert.That(
                    CreateSymbolicLink(externalTargetPath, markerPath),
                    Is.EqualTo(0),
                    "The Linux symlink test fixture could not be created. errno=" +
                    Marshal.GetLastWin32Error());

                using (new FileStream(
                           externalTargetPath,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.None))
                {
                    var exception = Assert.Throws<InvalidOperationException>(() =>
                        BuildSceneLease.RecoverCrashResidue(projectRoot));

                    Assert.That(exception.Message, Does.Contain("filesystem link"));
                }

                Assert.That(
                    File.ReadAllText(externalTargetPath),
                    Is.EqualTo(OwnershipMarkerContents));
                Assert.That(
                    File.GetAttributes(markerPath) & FileAttributes.ReparsePoint,
                    Is.Not.EqualTo(0));
            });
        }

        [Test]
        public void RecoverCrashResidue_ReparseOwnershipMarker_IsRejectedAsFilesystemLink()
        {
            if (Environment.OSVersion.Platform != PlatformID.Unix)
            {
                return;
            }

            WithTemporaryProject(projectRoot =>
            {
                var folder = Path.Combine(projectRoot, GeneratedFolder);
                Directory.CreateDirectory(folder);
                var markerPath = Path.Combine(folder, OwnershipMarker);
                var externalTargetPath = Path.Combine(
                    projectRoot,
                    "external-ownership-marker.txt");
                File.WriteAllText(externalTargetPath, OwnershipMarkerContents);
                Assert.That(
                    CreateSymbolicLink(externalTargetPath, markerPath),
                    Is.EqualTo(0),
                    "The Linux symlink test fixture could not be created. errno=" +
                    Marshal.GetLastWin32Error());

                using (new FileStream(
                           externalTargetPath,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.None))
                {
                    var exception = Assert.Throws<InvalidOperationException>(() =>
                        BuildSceneLease.RecoverCrashResidue(projectRoot));

                    Assert.That(exception.Message, Does.Contain("filesystem link"));
                }

                Assert.That(
                    File.ReadAllText(externalTargetPath),
                    Is.EqualTo(OwnershipMarkerContents));
                Assert.That(
                    File.GetAttributes(markerPath) & FileAttributes.ReparsePoint,
                    Is.Not.EqualTo(0));
            });
        }

        private static string CreateOwnedResidue(string projectRoot)
        {
            var folder = Path.Combine(projectRoot, GeneratedFolder);
            Directory.CreateDirectory(folder);
            File.WriteAllText(
                Path.Combine(folder, OwnershipMarker),
                OwnershipMarkerContents);
            return folder;
        }

        private static void WithTemporaryProject(Action<string> action)
        {
            var projectRoot = Path.Combine(
                Path.GetTempPath(),
                "jss-scene-lease-tests-" + Guid.NewGuid().ToString("N"));
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

        [DllImport("libc", EntryPoint = "symlink", SetLastError = true)]
        private static extern int CreateSymbolicLink(
            string target,
            string linkPath);
    }
}
