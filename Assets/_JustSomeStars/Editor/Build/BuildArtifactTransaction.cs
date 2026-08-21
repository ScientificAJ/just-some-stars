using System;
using System.Collections.Generic;
using System.IO;

namespace JustSomeStars.Editor.Build
{
    internal interface IBuildArtifactFileSystem
    {
        bool Exists(string path);

        long GetLength(string path);

        void CreateDirectory(string path);

        void DeleteFile(string path);

        void MoveFile(string sourcePath, string destinationPath);
    }

    internal sealed class BuildArtifactTransaction : IDisposable
    {
        private readonly string m_ProjectRoot;
        private readonly IBuildArtifactFileSystem m_FileSystem;
        private bool m_Published;
        private bool m_Disposed;

        private BuildArtifactTransaction(
            string projectRoot,
            string finalPath,
            string stagingPath,
            IBuildArtifactFileSystem fileSystem)
        {
            m_ProjectRoot = projectRoot;
            FinalPath = finalPath;
            StagingPath = stagingPath;
            m_FileSystem = fileSystem;
        }

        public string FinalPath { get; }

        public string StagingPath { get; }

        public static BuildArtifactTransaction Begin(
            string projectRoot,
            string relativeFinalPath)
        {
            return Begin(
                projectRoot,
                relativeFinalPath,
                new SystemBuildArtifactFileSystem());
        }

        internal static BuildArtifactTransaction Begin(
            string projectRoot,
            string relativeFinalPath,
            IBuildArtifactFileSystem fileSystem)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new ArgumentException(
                    "A Unity project root is required.",
                    nameof(projectRoot));
            }

            if (string.IsNullOrWhiteSpace(relativeFinalPath) ||
                Path.IsPathRooted(relativeFinalPath))
            {
                throw new InvalidOperationException(
                    "The configured Android artifact must be project-relative.");
            }

            if (fileSystem == null)
            {
                throw new ArgumentNullException(nameof(fileSystem));
            }

            var absoluteProjectRoot = Path.GetFullPath(projectRoot);
            var buildsRoot = Path.GetFullPath(
                Path.Combine(absoluteProjectRoot, "Builds"));
            var finalPath = Path.GetFullPath(
                Path.Combine(absoluteProjectRoot, relativeFinalPath));
            var buildsPrefix = buildsRoot.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!finalPath.StartsWith(buildsPrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Refusing to write an Android artifact outside the project Builds directory.");
            }

            var extension = Path.GetExtension(finalPath);
            if (!string.Equals(extension, ".apk", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".aab", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Android build outputs must end in .apk or .aab.");
            }

            var outputDirectory = Path.GetDirectoryName(finalPath);
            if (string.IsNullOrEmpty(outputDirectory))
            {
                throw new InvalidOperationException(
                    "The Android build output has no parent directory.");
            }

            var stagingPath = Path.Combine(
                outputDirectory,
                Path.GetFileNameWithoutExtension(finalPath) +
                ".jss-staging" +
                extension);
            BuildFilesystemSafety.EnsureNoFilesystemLinks(
                absoluteProjectRoot,
                outputDirectory,
                "the Android build output directory");
            BuildFilesystemSafety.EnsureNoFilesystemLinks(
                absoluteProjectRoot,
                finalPath,
                "the canonical Android artifact");
            BuildFilesystemSafety.EnsureNoFilesystemLinks(
                absoluteProjectRoot,
                stagingPath,
                "the staged Android artifact");
            fileSystem.CreateDirectory(outputDirectory);
            BuildFilesystemSafety.EnsureNoFilesystemLinks(
                absoluteProjectRoot,
                outputDirectory,
                "the Android build output directory");
            var transaction = new BuildArtifactTransaction(
                absoluteProjectRoot,
                finalPath,
                stagingPath,
                fileSystem);
            transaction.InvalidateArtifacts();
            return transaction;
        }

        public void Publish()
        {
            ThrowIfDisposed();
            BuildFilesystemSafety.EnsureNoFilesystemLinks(
                m_ProjectRoot,
                Path.GetDirectoryName(FinalPath),
                "the Android build output directory");
            BuildFilesystemSafety.EnsureNoFilesystemLinks(
                m_ProjectRoot,
                FinalPath,
                "the canonical Android artifact");
            BuildFilesystemSafety.EnsureNoFilesystemLinks(
                m_ProjectRoot,
                StagingPath,
                "the staged Android artifact");
            if (m_Published)
            {
                throw new InvalidOperationException(
                    "The Android artifact transaction has already been published.");
            }

            if (!m_FileSystem.Exists(StagingPath) ||
                m_FileSystem.GetLength(StagingPath) <= 0)
            {
                throw new InvalidOperationException(
                    "The staged Android artifact is missing or empty.");
            }

            if (m_FileSystem.Exists(FinalPath))
            {
                throw new InvalidOperationException(
                    "The canonical Android artifact reappeared before publication.");
            }

            if (!string.Equals(
                    Path.GetDirectoryName(StagingPath),
                    Path.GetDirectoryName(FinalPath),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Staging and canonical Android artifacts must share one directory.");
            }

            m_FileSystem.MoveFile(StagingPath, FinalPath);
            if (m_FileSystem.Exists(StagingPath) ||
                !m_FileSystem.Exists(FinalPath) ||
                m_FileSystem.GetLength(FinalPath) <= 0)
            {
                throw new InvalidOperationException(
                    "The staged Android artifact could not be atomically published and verified.");
            }

            m_Published = true;
        }

        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }

            m_Disposed = true;
            if (!m_Published)
            {
                DeleteBothAndVerify();
            }
        }

        private void InvalidateArtifacts()
        {
            DeleteBothAndVerify();
        }

        private void DeleteBothAndVerify()
        {
            var failures = new List<Exception>();
            DeleteAndVerify(FinalPath, failures);
            DeleteAndVerify(StagingPath, failures);
            if (failures.Count > 0)
            {
                throw new AggregateException(
                    "Android artifact cleanup could not be completed.",
                    failures);
            }
        }

        private void DeleteAndVerify(string path, ICollection<Exception> failures)
        {
            try
            {
                BuildFilesystemSafety.EnsureNoFilesystemLinks(
                    m_ProjectRoot,
                    path,
                    "an Android artifact transaction path");
                if (m_FileSystem.Exists(path))
                {
                    m_FileSystem.DeleteFile(path);
                }

                if (m_FileSystem.Exists(path))
                {
                    throw new InvalidOperationException(
                        "An Android artifact remained after cleanup: " +
                        Path.GetFileName(path) + ".");
                }
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        private void ThrowIfDisposed()
        {
            if (m_Disposed)
            {
                throw new ObjectDisposedException(nameof(BuildArtifactTransaction));
            }
        }
    }

    internal sealed class SystemBuildArtifactFileSystem : IBuildArtifactFileSystem
    {
        public bool Exists(string path)
        {
            return File.Exists(path);
        }

        public long GetLength(string path)
        {
            return new FileInfo(path).Length;
        }

        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        public void DeleteFile(string path)
        {
            File.Delete(path);
        }

        public void MoveFile(string sourcePath, string destinationPath)
        {
            File.Move(sourcePath, destinationPath);
        }
    }
}
