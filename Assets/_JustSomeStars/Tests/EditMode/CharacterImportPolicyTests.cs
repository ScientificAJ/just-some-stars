using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JustSomeStars.Editor.Importers;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class CharacterImportPolicyTests
    {
        private const string FixtureFbxPath =
            "Assets/_JustSomeStars/Art/Characters/Export/Fixtures/" +
            "task11-primitive.fbx";
        private const string FixtureReportPath =
            "Assets/_JustSomeStars/Art/Characters/Export/Fixtures/" +
            "task11-primitive.jss-character.json";

        [Test]
        public void Scope_IsLimitedToCanonicalCharacterExports()
        {
            Assert.That(
                CharacterModelPostprocessor.IsCharacterExportPath(FixtureFbxPath),
                Is.True);
            Assert.That(
                CharacterModelPostprocessor.IsCharacterExportPath(
                    "Assets/_JustSomeStars/Art/Characters/Export/captain.FBX"),
                Is.True);
            Assert.That(
                CharacterModelPostprocessor.IsCharacterExportPath(
                    "Assets/_JustSomeStars/Art/Characters/Source/captain.fbx"),
                Is.False);
            Assert.That(
                CharacterModelPostprocessor.IsCharacterExportPath(
                    "Assets/ThirdParty/character.fbx"),
                Is.False);
            Assert.That(
                CharacterModelPostprocessor.IsCharacterExportPath(
                    "Assets/_JustSomeStars/Art/Characters/Export/readme.txt"),
                Is.False);
        }

        [Test]
        public void DeclaredRigKind_DrivesExplicitUnityRigPolicy()
        {
            Assert.That(
                CharacterModelPostprocessor.AnimationTypeFor("Generic"),
                Is.EqualTo(ModelImporterAnimationType.Generic));
            Assert.That(
                CharacterModelPostprocessor.AnimationTypeFor("Humanoid"),
                Is.EqualTo(ModelImporterAnimationType.Human));
            Assert.Throws<InvalidDataException>(() =>
                CharacterModelPostprocessor.AnimationTypeFor("Legacy"));
        }

        [Test]
        public void Metadata_MissingMalformedAndStale_FailsClosed()
        {
            var root = Path.Combine(
                Path.GetFullPath("Builds/Task11"),
                "CharacterImportPolicyTests",
                Guid.NewGuid().ToString("N"));
            const string testAsset =
                "Assets/_JustSomeStars/Art/Characters/Export/" +
                "Fixture/test-character.fbx";
            var absoluteFbx = Path.Combine(root, testAsset);
            var absoluteReport = Path.ChangeExtension(
                absoluteFbx,
                null) + CharacterModelPostprocessor.ReportSuffix;
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteFbx));
            File.WriteAllBytes(absoluteFbx, new byte[] { 1, 2, 3, 4 });
            try
            {
                Assert.Throws<FileNotFoundException>(() =>
                    CharacterModelPostprocessor.LoadValidatedReport(
                        testAsset,
                        root));

                File.WriteAllText(absoluteReport, "{not-json");
                Assert.Throws<InvalidDataException>(() =>
                    CharacterModelPostprocessor.LoadValidatedReport(
                        testAsset,
                        root));

                var canonicalJson = File.ReadAllText(FixtureReportPath);
                File.WriteAllText(absoluteReport, canonicalJson);
                Assert.Throws<InvalidDataException>(() =>
                    CharacterModelPostprocessor.LoadValidatedReport(
                        testAsset,
                        root));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void CanonicalFixture_ImporterSettingsAreExplicitAndStable()
        {
            var importer = AssetImporter.GetAtPath(FixtureFbxPath) as ModelImporter;
            Assert.That(importer, Is.Not.Null, FixtureFbxPath);
            Assert.That(importer.globalScale, Is.EqualTo(1f));
            Assert.That(importer.useFileScale, Is.False);
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Generic));
            Assert.That(importer.avatarSetup, Is.EqualTo(ModelImporterAvatarSetup.CreateFromThisModel));
            Assert.That(importer.materialImportMode, Is.EqualTo(ModelImporterMaterialImportMode.None));
            Assert.That(importer.importAnimation, Is.True);
            Assert.That(importer.animationCompression, Is.EqualTo(ModelImporterAnimationCompression.Optimal));
            Assert.That(importer.importCameras, Is.False);
            Assert.That(importer.importLights, Is.False);
            Assert.That(importer.preserveHierarchy, Is.True);
            Assert.That(importer.resampleCurves, Is.False);
            Assert.That(importer.bakeAxisConversion, Is.False);
            Assert.That(importer.motionNodeName, Is.EqualTo("Root"));
        }

        [Test]
        public void PrimitiveRoundTrip_ReconcilesScaleAxesRigRootMotionAndLods()
        {
            var report = CharacterModelPostprocessor.LoadValidatedReport(
                FixtureFbxPath,
                Path.GetFullPath("."));
            Assert.That(report.schemaVersion, Is.EqualTo(1));
            Assert.That(report.rigKind, Is.EqualTo("Generic"));
            Assert.That(report.axes.forward, Is.EqualTo("-Z"));
            Assert.That(report.axes.up, Is.EqualTo("Y"));
            Assert.That(report.axes.unityForward, Is.EqualTo("+Z"));
            Assert.That(report.materials, Is.EqualTo(new[] { "MAT_Task11Canvas" }));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FixtureFbxPath);
            Assert.That(prefab, Is.Not.Null, FixtureFbxPath);
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var lod0 = FindDescendant(instance.transform, "CHR_Task11Primitive_LOD0");
                Assert.That(lod0, Is.Not.Null);
                var renderer = lod0.GetComponent<Renderer>();
                Assert.That(renderer, Is.Not.Null);
                var size = renderer.bounds.size;
                Assert.That(size.x, Is.EqualTo(report.dimensionsMeters.x).Within(0.01f));
                Assert.That(size.y, Is.EqualTo(report.dimensionsMeters.z).Within(0.01f));
                Assert.That(size.z, Is.EqualTo(report.dimensionsMeters.y).Within(0.01f));

                var forward = FindDescendant(instance.transform, report.forwardMarker);
                Assert.That(forward, Is.Not.Null);
                Assert.That(forward.position.z, Is.GreaterThan(0.45f));

                var importedBoneNames = instance.GetComponentsInChildren<Transform>(true)
                    .Select(transform => transform.name)
                    .Where(report.bones.Contains)
                    .OrderBy(name => name)
                    .ToArray();
                Assert.That(importedBoneNames, Is.EqualTo(report.bones.OrderBy(name => name)));

                var lodGroups = instance.GetComponentsInChildren<LODGroup>(true);
                Assert.That(lodGroups, Has.Length.EqualTo(1));
                var lods = lodGroups[0].GetLODs();
                Assert.That(lods, Has.Length.EqualTo(3));
                string[] sharedSkinBones = null;
                for (var index = 0; index < lods.Length; index++)
                {
                    Assert.That(lods[index].renderers, Has.Length.EqualTo(1));
                    Assert.That(
                        lods[index].renderers[0].name,
                        Does.EndWith($"_LOD{index}"));
                    var skin = lods[index].renderers[0] as SkinnedMeshRenderer;
                    Assert.That(skin, Is.Not.Null, $"LOD{index} must share the rig.");
                    Assert.That(skin.rootBone, Is.Not.Null);
                    var skinBones = skin.bones
                        .Select(bone => bone.name)
                        .OrderBy(name => name)
                        .ToArray();
                    Assert.That(skinBones, Is.Not.Empty);
                    if (sharedSkinBones == null)
                    {
                        sharedSkinBones = skinBones;
                    }
                    else
                    {
                        Assert.That(skinBones, Is.EqualTo(sharedSkinBones));
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            var rootMotionCurves = AssetDatabase.LoadAllAssetsAtPath(FixtureFbxPath)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                .SelectMany(clip => AnimationUtility.GetCurveBindings(clip)
                    .Where(binding => binding.path.EndsWith("Root", StringComparison.Ordinal) &&
                                      binding.propertyName.EndsWith("m_LocalPosition.z", StringComparison.Ordinal))
                    .Select(binding => AnimationUtility.GetEditorCurve(clip, binding)))
                .Where(curve => curve != null && curve.length >= 2)
                .ToArray();
            Assert.That(rootMotionCurves, Is.Not.Empty);
            Assert.That(
                rootMotionCurves.Max(curve =>
                    Math.Abs(curve.keys[^1].value - curve.keys[0].value)),
                Is.EqualTo(report.rootMotion.distanceMeters).Within(0.01f));
        }

        [Test]
        public void ForcedReimport_RemainsIdempotent()
        {
            AssetDatabase.ImportAsset(
                FixtureFbxPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(
                FixtureFbxPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FixtureFbxPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<LODGroup>(true), Has.Length.EqualTo(1));
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .SingleOrDefault(transform => transform.name == name);
        }
    }
}
