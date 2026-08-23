using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace JustSomeStars.Editor.Importers
{
    public sealed class CharacterModelPostprocessor : AssetPostprocessor
    {
        internal const string ExportRoot =
            "Assets/_JustSomeStars/Art/Characters/Export/";
        internal const string ReportSuffix = ".jss-character.json";
        private const int SupportedSchemaVersion = 1;

        private static readonly float[] LodThresholds = { 0.60f, 0.30f, 0.08f };

        internal static bool IsCharacterExportPath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   path.StartsWith(ExportRoot, StringComparison.Ordinal) &&
                   path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
        }

        internal static ModelImporterAnimationType AnimationTypeFor(string rigKind)
        {
            return rigKind switch
            {
                "Generic" => ModelImporterAnimationType.Generic,
                "Humanoid" => ModelImporterAnimationType.Human,
                _ => throw new InvalidDataException(
                    $"Unsupported character rig kind: {rigKind ?? "<null>"}.")
            };
        }

        internal static CharacterImportReport LoadValidatedReport(
            string modelAssetPath,
            string projectRoot)
        {
            if (!IsCharacterExportPath(modelAssetPath))
            {
                throw new InvalidDataException(
                    $"Character report requested outside {ExportRoot}: {modelAssetPath}");
            }

            var absoluteRoot = Path.GetFullPath(projectRoot);
            var absoluteModel = Path.GetFullPath(
                Path.Combine(absoluteRoot, modelAssetPath));
            var reportAssetPath = modelAssetPath[..^4] + ReportSuffix;
            var absoluteReport = Path.GetFullPath(
                Path.Combine(absoluteRoot, reportAssetPath));
            if (!absoluteModel.StartsWith(absoluteRoot + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal) ||
                !absoluteReport.StartsWith(absoluteRoot + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException("Character import path escaped the project root.");
            }

            if (!File.Exists(absoluteModel))
            {
                throw new FileNotFoundException(
                    "Character FBX is missing.",
                    absoluteModel);
            }

            if (!File.Exists(absoluteReport))
            {
                throw new FileNotFoundException(
                    "Character import report is missing.",
                    absoluteReport);
            }

            CharacterImportReport report;
            try
            {
                report = JsonUtility.FromJson<CharacterImportReport>(
                    File.ReadAllText(absoluteReport));
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    $"Character import report is malformed: {reportAssetPath}",
                    exception);
            }

            ValidateReport(report, modelAssetPath, absoluteRoot, absoluteModel);
            return report;
        }

        private void OnPreprocessModel()
        {
            if (!IsCharacterExportPath(assetPath))
            {
                return;
            }

            var report = LoadValidatedReport(
                assetPath,
                Path.GetDirectoryName(Application.dataPath));
            ApplyImporterSettings((ModelImporter)assetImporter, report);
        }

        private void OnPostprocessModel(GameObject model)
        {
            if (!IsCharacterExportPath(assetPath))
            {
                return;
            }

            var report = LoadValidatedReport(
                assetPath,
                Path.GetDirectoryName(Application.dataPath));
            ApplyLodGroup(model, report);
        }

        internal static void ApplyImporterSettings(
            ModelImporter importer,
            CharacterImportReport report)
        {
            importer.globalScale = 1f;
            importer.useFileScale = false;
            importer.animationType = AnimationTypeFor(report.rigKind);
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.importAnimatedCustomProperties = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.preserveHierarchy = true;
            importer.resampleCurves = false;
            importer.bakeAxisConversion = false;
            importer.motionNodeName = report.rootMotion.bone;
            importer.isReadable = false;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
        }

        internal static void ApplyLodGroup(
            GameObject model,
            CharacterImportReport report)
        {
            var existingGroups = model.GetComponentsInChildren<LODGroup>(true);
            if (existingGroups.Length > 1 ||
                (existingGroups.Length == 1 && existingGroups[0].gameObject != model))
            {
                throw new InvalidDataException(
                    "Character export contains an unexpected authored LODGroup.");
            }

            var group = existingGroups.SingleOrDefault() ?? model.AddComponent<LODGroup>();
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            var lods = new LOD[3];
            for (var index = 0; index < lods.Length; index++)
            {
                var suffix = $"_LOD{index}";
                var levelRenderers = renderers
                    .Where(renderer => renderer.name.EndsWith(
                        suffix,
                        StringComparison.Ordinal))
                    .OrderBy(renderer => renderer.name, StringComparer.Ordinal)
                    .ToArray();
                if (levelRenderers.Length == 0)
                {
                    throw new InvalidDataException(
                        $"Character export has no renderer for {suffix}.");
                }

                var expectedMeshes = report.LodFor(index).meshes
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray();
                var importedMeshes = levelRenderers
                    .Select(renderer => renderer.name)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray();
                if (!expectedMeshes.SequenceEqual(importedMeshes))
                {
                    throw new InvalidDataException(
                        $"Imported {suffix} renderers do not match the Blender report.");
                }

                lods[index] = new LOD(LodThresholds[index], levelRenderers);
            }

            group.fadeMode = LODFadeMode.None;
            group.animateCrossFading = false;
            group.SetLODs(lods);
            group.RecalculateBounds();
        }

        private static void ValidateReport(
            CharacterImportReport report,
            string modelAssetPath,
            string projectRoot,
            string absoluteModel)
        {
            if (report == null || report.schemaVersion != SupportedSchemaVersion)
            {
                throw new InvalidDataException("Unsupported character report schema.");
            }

            if (report.validation == null || !report.validation.isValid ||
                report.validation.issues == null || report.validation.issues.Length != 0)
            {
                throw new InvalidDataException("Character report does not contain a clean validation result.");
            }

            if (!string.Equals(report.fbxAsset, modelAssetPath, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Character report points at a different FBX asset.");
            }

            if (!string.Equals(report.fbxSha256, Sha256(absoluteModel), StringComparison.Ordinal))
            {
                throw new InvalidDataException("Character FBX hash does not match its report.");
            }

            if (string.IsNullOrWhiteSpace(report.sourceAsset) ||
                string.IsNullOrWhiteSpace(report.sourceBlendSha256))
            {
                throw new InvalidDataException("Character report has no source provenance.");
            }

            var sourcePath = Path.GetFullPath(Path.Combine(projectRoot, report.sourceAsset));
            if (!sourcePath.StartsWith(projectRoot + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal) ||
                !File.Exists(sourcePath) ||
                !string.Equals(report.sourceBlendSha256, Sha256(sourcePath),
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException("Character source provenance is missing or stale.");
            }

            if (report.units == null || report.units.lengthUnit != "METERS" ||
                Math.Abs(report.units.metersPerUnit - 1f) > 0.000001f ||
                report.axes == null || report.axes.forward != "-Z" ||
                report.axes.up != "Y" || report.axes.unityForward != "+Z")
            {
                throw new InvalidDataException("Character units or axes are not canonical.");
            }

            AnimationTypeFor(report.rigKind);
            if (report.dimensionsMeters == null ||
                report.dimensionsMeters.x <= 0f ||
                report.dimensionsMeters.y <= 0f ||
                report.dimensionsMeters.z <= 0f ||
                report.bones == null || report.bones.Length == 0 ||
                report.bones.Distinct(StringComparer.Ordinal).Count() != report.bones.Length ||
                report.rootMotion == null ||
                string.IsNullOrWhiteSpace(report.rootMotion.bone) ||
                report.rootMotion.distanceMeters <= 0f ||
                report.JSS_LOD0 == null || report.JSS_LOD1 == null || report.JSS_LOD2 == null ||
                report.JSS_LOD0.triangles <= report.JSS_LOD1.triangles ||
                report.JSS_LOD1.triangles <= report.JSS_LOD2.triangles)
            {
                throw new InvalidDataException("Character report is incomplete or internally inconsistent.");
            }
        }

        private static string Sha256(string path)
        {
            using var stream = File.OpenRead(path);
            using var hash = SHA256.Create();
            return string.Concat(hash.ComputeHash(stream)
                .Select(value => value.ToString("x2")));
        }
    }

    [Serializable]
    internal sealed class CharacterImportReport
    {
        public int schemaVersion;
        public string assetName;
        public string sourceAsset;
        public string sourceBlendSha256;
        public string fbxAsset;
        public string fbxSha256;
        public string blenderVersion;
        public string rigKind;
        public CharacterUnits units;
        public CharacterAxes axes;
        public CharacterDimensions dimensionsMeters;
        public string[] bones;
        public CharacterLods lods;
        public string[] materials;
        public string forwardMarker;
        public CharacterRootMotion rootMotion;
        public CharacterValidation validation;

        public CharacterLod LodFor(int index)
        {
            return index switch
            {
                0 => lods.JSS_LOD0,
                1 => lods.JSS_LOD1,
                2 => lods.JSS_LOD2,
                _ => throw new ArgumentOutOfRangeException(nameof(index))
            };
        }

        public CharacterLod JSS_LOD0 => lods?.JSS_LOD0;
        public CharacterLod JSS_LOD1 => lods?.JSS_LOD1;
        public CharacterLod JSS_LOD2 => lods?.JSS_LOD2;
    }

    [Serializable]
    internal sealed class CharacterUnits
    {
        public string lengthUnit;
        public float metersPerUnit;
    }

    [Serializable]
    internal sealed class CharacterAxes
    {
        public string forward;
        public string up;
        public string unityForward;
    }

    [Serializable]
    internal sealed class CharacterDimensions
    {
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    internal sealed class CharacterLods
    {
        public CharacterLod JSS_LOD0;
        public CharacterLod JSS_LOD1;
        public CharacterLod JSS_LOD2;
    }

    [Serializable]
    internal sealed class CharacterLod
    {
        public string[] meshes;
        public int triangles;
    }

    [Serializable]
    internal sealed class CharacterRootMotion
    {
        public string bone;
        public int startFrame;
        public int endFrame;
        public CharacterDimensions deltaMeters;
        public float distanceMeters;
    }

    [Serializable]
    internal sealed class CharacterValidation
    {
        public bool isValid;
        public CharacterValidationIssue[] issues;
    }

    [Serializable]
    internal sealed class CharacterValidationIssue
    {
        public string code;
        public string message;
    }
}
