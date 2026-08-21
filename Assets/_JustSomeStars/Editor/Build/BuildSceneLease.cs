using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JustSomeStars.Editor.Build
{
    internal interface IBuildSceneLeaseFactory
    {
        IBuildSceneLease Acquire();
    }

    internal interface IBuildSceneLease
    {
        IReadOnlyList<string> ScenePaths { get; }

        void CleanupAndVerify();
    }

    internal sealed class BuildSceneLeaseFactory : IBuildSceneLeaseFactory
    {
        private readonly string m_ProjectRoot;

        public BuildSceneLeaseFactory(string projectRoot)
        {
            m_ProjectRoot = projectRoot ??
                throw new ArgumentNullException(nameof(projectRoot));
        }

        public IBuildSceneLease Acquire()
        {
            return BuildSceneLease.Acquire(m_ProjectRoot);
        }
    }

    internal sealed class BuildSceneLease : IBuildSceneLease
    {
        private const string GeneratedFolderRelativePath =
            "Assets/_JustSomeStars/GeneratedBuild";
        private const string GeneratedSceneRelativePath =
            GeneratedFolderRelativePath + "/Task3EmptyBuildScene.unity";
        private const string PreparationFolderRelativePath =
            "Temp/JssBuildSceneLease";
        private const string OwnershipMarkerName = ".jss-build-scene-owner";
        private const string OwnershipMarkerTemporaryName =
            OwnershipMarkerName + ".tmp";
        private const string OwnershipMarkerContents =
            "JustSomeStars.BuildSceneLease:v1\n";

        private static readonly HashSet<string> KnownGeneratedFiles =
            new HashSet<string>(
                new[]
                {
                    OwnershipMarkerName,
                    OwnershipMarkerName + ".meta",
                    "Task3EmptyBuildScene.unity",
                    "Task3EmptyBuildScene.unity.meta",
                },
                StringComparer.Ordinal);

        private readonly string m_ProjectRoot;
        private readonly bool m_OwnsGeneratedScene;
        private Scene m_TemporaryScene;
        private bool m_Cleaned;

        private BuildSceneLease(
            string projectRoot,
            IReadOnlyList<string> scenePaths,
            bool ownsGeneratedScene,
            Scene temporaryScene)
        {
            m_ProjectRoot = projectRoot;
            ScenePaths = scenePaths;
            m_OwnsGeneratedScene = ownsGeneratedScene;
            m_TemporaryScene = temporaryScene;
        }

        public IReadOnlyList<string> ScenePaths { get; }

        public static void RecoverCrashResidue(string projectRoot)
        {
            var absoluteProjectRoot = ResolveProjectRoot(projectRoot);
            RecoverPreparationResidue(absoluteProjectRoot);
            var folderPath = Path.Combine(
                absoluteProjectRoot,
                GeneratedFolderRelativePath);
            var folderMetaPath = folderPath + ".meta";
            BuildFilesystemSafety.EnsureNoFilesystemLinks(
                absoluteProjectRoot,
                folderPath,
                "the generated build-scene folder");
            BuildFilesystemSafety.EnsureNoFilesystemLinks(
                absoluteProjectRoot,
                folderMetaPath,
                "the generated build-scene folder metadata");
            if (!Directory.Exists(folderPath))
            {
                if (File.Exists(folderMetaPath))
                {
                    throw new InvalidOperationException(
                        "An unowned generated build-scene folder meta file exists; " +
                        "refusing to delete unknown content.");
                }

                return;
            }

            ValidateOwnedGeneratedFolder(folderPath);
            Directory.Delete(folderPath, recursive: true);
            if (File.Exists(folderMetaPath))
            {
                File.Delete(folderMetaPath);
            }

            if (Directory.Exists(folderPath) || File.Exists(folderMetaPath))
            {
                throw new InvalidOperationException(
                    "Known generated build-scene crash residue could not be removed.");
            }
        }

        internal static BuildSceneLease Acquire(string projectRoot)
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException(
                    "Temporary build-scene generation is CLI-only and requires batch mode.");
            }

            var absoluteProjectRoot = ResolveProjectRoot(projectRoot);
            RecoverCrashResidue(absoluteProjectRoot);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var scenePlan = BuildScenePlan.Resolve(
                EditorBuildSettings.scenes
                    .Where(scene => scene.enabled)
                    .Select(scene => scene.path),
                GeneratedSceneRelativePath);
            if (!scenePlan.RequiresTemporaryScene)
            {
                return new BuildSceneLease(
                    absoluteProjectRoot,
                    scenePlan.ScenePaths,
                    ownsGeneratedScene: false,
                    temporaryScene: default);
            }

            BuildSceneLease lease = null;
            try
            {
                var absoluteFolderPath = Path.Combine(
                    absoluteProjectRoot,
                    GeneratedFolderRelativePath);
                PrepareGeneratedFolder(
                    absoluteProjectRoot,
                    absoluteFolderPath);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                var temporaryScene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);
                lease = new BuildSceneLease(
                    absoluteProjectRoot,
                    scenePlan.ScenePaths,
                    ownsGeneratedScene: true,
                    temporaryScene);
                if (!EditorSceneManager.SaveScene(
                        temporaryScene,
                        GeneratedSceneRelativePath,
                        saveAsCopy: false))
                {
                    throw new InvalidOperationException(
                        "Unity could not save the temporary empty build scene.");
                }

                AssetDatabase.ImportAsset(
                    GeneratedSceneRelativePath,
                    ImportAssetOptions.ForceSynchronousImport);
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GeneratedSceneRelativePath) == null)
                {
                    throw new InvalidOperationException(
                        "Unity did not import the temporary empty build scene.");
                }

                Debug.Log(
                    "[JSS Build] No enabled build-settings scenes; leased a generated empty scene.");
                return lease;
            }
            catch (Exception creationFailure)
            {
                Exception cleanupFailure = null;
                try
                {
                    if (lease != null)
                    {
                        lease.CleanupAndVerify();
                    }
                    else
                    {
                        RecoverCrashResidue(absoluteProjectRoot);
                        AssetDatabase.Refresh(
                            ImportAssetOptions.ForceSynchronousImport);
                    }
                }
                catch (Exception exception)
                {
                    cleanupFailure = exception;
                }

                if (cleanupFailure != null)
                {
                    throw new AggregateException(
                        "Generated build-scene creation and cleanup both failed.",
                        creationFailure,
                        cleanupFailure);
                }

                ExceptionDispatchInfo.Capture(creationFailure).Throw();
                throw new InvalidOperationException(
                    "Unreachable generated scene creation failure path.");
            }
        }

        public void CleanupAndVerify()
        {
            if (m_Cleaned)
            {
                return;
            }

            if (!m_OwnsGeneratedScene)
            {
                m_Cleaned = true;
                return;
            }

            var failures = new List<Exception>();
            try
            {
                if (m_TemporaryScene.IsValid() && m_TemporaryScene.isLoaded)
                {
                    EditorSceneManager.NewScene(
                        NewSceneSetup.EmptyScene,
                        NewSceneMode.Single);
                    m_TemporaryScene = default;
                }
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }

            try
            {
                var absoluteFolderPath = Path.Combine(
                    m_ProjectRoot,
                    GeneratedFolderRelativePath);
                ValidateOwnedGeneratedFolder(absoluteFolderPath);
                if (!AssetDatabase.DeleteAsset(GeneratedFolderRelativePath) &&
                    Directory.Exists(absoluteFolderPath))
                {
                    throw new InvalidOperationException(
                        "Unity could not delete the owned generated build-scene folder.");
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                VerifyNoResidue(absoluteFolderPath);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }

            if (failures.Count > 0)
            {
                throw new AggregateException(
                    "Generated build-scene cleanup failed.",
                    failures);
            }

            m_Cleaned = true;
        }

        private static string ResolveProjectRoot(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new ArgumentException(
                    "A Unity project root is required.",
                    nameof(projectRoot));
            }

            return Path.GetFullPath(projectRoot);
        }

        private static void PrepareGeneratedFolder(
            string projectRoot,
            string finalFolderPath)
        {
            var preparationFolderPath = Path.Combine(
                projectRoot,
                PreparationFolderRelativePath);
            var preparationParentPath = Path.GetDirectoryName(preparationFolderPath);
            if (string.IsNullOrEmpty(preparationParentPath))
            {
                throw new InvalidOperationException(
                    "The generated build-scene preparation path has no parent.");
            }

            BuildFilesystemSafety.EnsureNoFilesystemLinks(
                projectRoot,
                preparationParentPath,
                "the generated build-scene preparation parent");
            BuildFilesystemSafety.EnsureNoFilesystemLinks(
                projectRoot,
                finalFolderPath,
                "the generated build-scene folder");
            Directory.CreateDirectory(preparationParentPath);
            Directory.CreateDirectory(preparationFolderPath);
            BuildFilesystemSafety.EnsureNoFilesystemLinks(
                projectRoot,
                preparationFolderPath,
                "the generated build-scene preparation folder");

            var temporaryMarkerPath = Path.Combine(
                preparationFolderPath,
                OwnershipMarkerTemporaryName);
            var markerPath = Path.Combine(
                preparationFolderPath,
                OwnershipMarkerName);
            var markerBytes = new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false).GetBytes(
                OwnershipMarkerContents);
            using (var stream = new FileStream(
                       temporaryMarkerPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            {
                stream.Write(markerBytes, 0, markerBytes.Length);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryMarkerPath, markerPath);
            Directory.Move(preparationFolderPath, finalFolderPath);
        }

        private static void RecoverPreparationResidue(string projectRoot)
        {
            var preparationFolderPath = Path.Combine(
                projectRoot,
                PreparationFolderRelativePath);
            BuildFilesystemSafety.EnsureNoFilesystemLinks(
                projectRoot,
                preparationFolderPath,
                "the generated build-scene preparation folder");
            if (!Directory.Exists(preparationFolderPath))
            {
                return;
            }

            var subdirectories = Directory.GetDirectories(
                preparationFolderPath,
                "*",
                SearchOption.TopDirectoryOnly);
            var files = Directory.GetFiles(
                preparationFolderPath,
                "*",
                SearchOption.TopDirectoryOnly);
            if (files.Any(path =>
                    (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0))
            {
                throw new InvalidOperationException(
                    "The generated build-scene preparation folder contains a " +
                    "filesystem link; refusing to read or delete it.");
            }

            var knownContent = files.Length == 0 ||
                files.Length == 1 && IsKnownPreparationMarker(files[0]);
            if (subdirectories.Length > 0 || !knownContent)
            {
                throw new InvalidOperationException(
                    "The generated build-scene preparation folder contains " +
                    "unknown content; refusing to delete it.");
            }

            if (files.Length == 1)
            {
                File.Delete(files[0]);
                if (File.Exists(files[0]))
                {
                    throw new InvalidOperationException(
                        "A known generated build-scene preparation marker could " +
                        "not be removed.");
                }
            }

            Directory.Delete(preparationFolderPath, recursive: false);
            if (Directory.Exists(preparationFolderPath))
            {
                throw new InvalidOperationException(
                    "Known generated build-scene preparation residue could not " +
                    "be removed.");
            }
        }

        private static bool IsKnownPreparationMarker(string path)
        {
            var fileName = Path.GetFileName(path);
            var contents = File.ReadAllText(path);
            if (string.Equals(
                    fileName,
                    OwnershipMarkerName,
                    StringComparison.Ordinal))
            {
                return string.Equals(
                    contents,
                    OwnershipMarkerContents,
                    StringComparison.Ordinal);
            }

            return string.Equals(
                       fileName,
                       OwnershipMarkerTemporaryName,
                       StringComparison.Ordinal) &&
                   OwnershipMarkerContents.StartsWith(
                       contents,
                       StringComparison.Ordinal);
        }

        private static void ValidateOwnedGeneratedFolder(string folderPath)
        {
            var markerPath = Path.Combine(folderPath, OwnershipMarkerName);
            if (!File.Exists(markerPath))
            {
                throw new InvalidOperationException(
                    "The generated build-scene folder has no valid ownership marker; " +
                    "refusing to delete unknown content.");
            }

            if ((File.GetAttributes(markerPath) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException(
                    "The generated build-scene ownership marker is a filesystem " +
                    "link; refusing to read or delete it.");
            }

            if (!string.Equals(
                    File.ReadAllText(markerPath),
                    OwnershipMarkerContents,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The generated build-scene folder has no valid ownership marker; " +
                    "refusing to delete unknown content.");
            }

            var subdirectories = Directory.GetDirectories(
                folderPath,
                "*",
                SearchOption.TopDirectoryOnly);
            var generatedFiles = Directory.GetFiles(
                    folderPath,
                    "*",
                    SearchOption.TopDirectoryOnly);
            var containsFilesystemLinks = generatedFiles.Any(path =>
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0);
            var unknownFiles = generatedFiles
                .Select(path => path.Substring(folderPath.Length + 1)
                    .Replace(Path.DirectorySeparatorChar, '/'))
                .Where(path => !KnownGeneratedFiles.Contains(path))
                .ToArray();
            if (subdirectories.Length > 0 ||
                containsFilesystemLinks ||
                unknownFiles.Length > 0)
            {
                throw new InvalidOperationException(
                    "The generated build-scene folder contains unknown content; " +
                    "refusing to delete it.");
            }
        }

        private static void VerifyNoResidue(string absoluteFolderPath)
        {
            if (Directory.Exists(absoluteFolderPath) ||
                File.Exists(absoluteFolderPath + ".meta"))
            {
                throw new InvalidOperationException(
                    "Generated build-scene files remain after cleanup.");
            }
        }
    }
}
