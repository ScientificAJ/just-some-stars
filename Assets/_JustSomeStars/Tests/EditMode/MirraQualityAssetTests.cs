using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class MirraQualityAssetTests
    {
        private const string ScenePath =
            "Assets/_JustSomeStars/Scenes/Destinations/Mirra.unity";
        private const string ProfileRoot =
            "Assets/_JustSomeStars/Content/QualityProfiles";
        private const string SharedMaterialRoot =
            "Assets/_JustSomeStars/Art/2D/Materials/Shared";
        private const string MirraMaterialRoot =
            "Assets/_JustSomeStars/Art/2D/Materials/Mirra";
        private const string MirraVfxRoot =
            "Assets/_JustSomeStars/Art/2D/VFX/Mirra";
        private const string MaterialMapRoot =
            "Assets/_JustSomeStars/Art/2D/Materials/Maps";
        private const string QualityTypeName =
            "JustSomeStars.Runtime.Rendering2D.MirraQualityController2D";

        private static readonly string[] ExpectedQualities =
        {
            "Performance",
            "Balanced",
            "Cinematic",
            "HighFrameRate",
        };

        private static readonly string[] ExpectedProfiles =
        {
            "MirraPerformance.asset",
            "MirraBalanced.asset",
            "MirraCinematic.asset",
            "MirraHighFrameRate.asset",
        };

        [Test]
        public void QualityProfiles_AreExactlyFourReachableBoundedAndUnique()
        {
            Assert.That(Enum.GetNames(typeof(PresentationQuality)),
                Is.EqualTo(ExpectedQualities));

            var profiles = ExpectedProfiles.Select(file =>
                    AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                        $"{ProfileRoot}/{file}"))
                .ToArray();
            Assert.That(profiles, Has.All.Not.Null);
            Assert.That(profiles.Select(profile => Read<string>(profile, "StableId")),
                Is.EquivalentTo(new[]
                {
                    "mirra.performance",
                    "mirra.balanced",
                    "mirra.cinematic",
                    "mirra.high-frame-rate",
                }));
            Assert.That(profiles.Select(profile => Read<object>(profile, "Quality")
                    .ToString()),
                Is.EquivalentTo(ExpectedQualities));
            Assert.That(profiles.Select(profile => Read<int>(profile, "TargetFrameRate")),
                Is.EqualTo(new[] { 30, 30, 30, 60 }));
            Assert.That(profiles.Select(profile => Read<float>(profile, "RenderScale")),
                Has.All.InRange(0.75f, 1f));
            Assert.That(profiles.Select(profile => Read<int>(profile, "ActiveLightCount")),
                Has.All.InRange(1, 4));
            Assert.That(profiles.Select(profile => Read<float>(profile, "ParticleMultiplier")),
                Has.All.InRange(0f, 1.25f));
            Assert.That(profiles.Select(profile => Read<float>(profile, "VolumeWeight")),
                Has.All.InRange(0f, 1f));
            Assert.That(Read<bool>(profiles[3], "UsesDynamicResolution"), Is.True);
            Assert.That(profiles.Take(3).Select(profile =>
                    Read<bool>(profile, "UsesDynamicResolution")),
                Has.All.False);
        }

        [Test]
        public void ShaderGraphsAndMaterialLibrary_AreLiveDistinctAndSceneBound()
        {
            var surfaceGraph = $"{SharedMaterialRoot}/JSSSpriteSurface2D.shadergraph";
            var emissionGraph = $"{SharedMaterialRoot}/JSSSpriteEmission2D.shadergraph";
            Assert.That(AssetDatabase.LoadMainAssetAtPath(surfaceGraph), Is.Not.Null);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(emissionGraph), Is.Not.Null);

            var expectedMaterials = new Dictionary<string, string>
            {
                ["JSS_Fabric.mat"] = SharedMaterialRoot,
                ["JSS_Metal.mat"] = SharedMaterialRoot,
                ["JSS_Rock.mat"] = MirraMaterialRoot,
                ["JSS_Ice.mat"] = MirraMaterialRoot,
                ["JSS_VisorGlass.mat"] = MirraMaterialRoot,
                ["JSS_SignalHologram.mat"] = MirraVfxRoot,
                ["JSS_Atmosphere.mat"] = MirraMaterialRoot,
            };
            var materials = expectedMaterials.Select(pair =>
                    AssetDatabase.LoadAssetAtPath<Material>(
                        $"{pair.Value}/{pair.Key}"))
                .ToArray();
            Assert.That(materials, Has.All.Not.Null);
            Assert.That(materials.Select(material => material.shader),
                Has.All.Not.Null);
            Assert.That(materials.Select(material => material.shader.name),
                Has.None.EqualTo("Hidden/InternalErrorShader"));
            Assert.That(materials.Select(material => material.renderQueue),
                Has.All.GreaterThanOrEqualTo(3000),
                "Every scene-bound sprite material must preserve source alpha; " +
                "an opaque material turns each parallax band into a full-screen occluder.");

            var surfaceMaterials = materials.Take(5).ToArray();
            Assert.That(surfaceMaterials.Select(material =>
                    AssetDatabase.GetAssetPath(material.shader)).Distinct().ToArray(),
                Is.EqualTo(new[] {surfaceGraph}),
                "Fabric, metal, rock, ice and visor must use the live surface graph.");
            Assert.That(materials.Skip(5).Select(material =>
                    AssetDatabase.GetAssetPath(material.shader)).Distinct().ToArray(),
                Is.EqualTo(new[] {emissionGraph}),
                "Signal and atmosphere must use the alpha-preserving emission graph.");

            var expectedMapNames = new[]
            {
                "Fabric",
                "Metal",
                "Rock",
                "Ice",
                "VisorGlass",
            };
            for (var index = 0; index < surfaceMaterials.Length; index++)
            {
                var material = surfaceMaterials[index];
                Assert.That(material.HasProperty("_Normal_Map"), Is.True);
                Assert.That(material.HasProperty("_MaskMap"), Is.True);
                Assert.That(material.HasProperty("_PaletteMask"), Is.True);
                Assert.That(material.HasProperty("_PaletteColor"), Is.True);

                var normal = material.GetTexture("_Normal_Map");
                var surfaceMask = material.GetTexture("_MaskMap");
                var paletteMask = material.GetTexture("_PaletteMask");
                Assert.That(normal, Is.Not.Null, material.name + " normal");
                Assert.That(surfaceMask, Is.Not.Null, material.name + " mask");
                Assert.That(paletteMask, Is.Not.Null, material.name + " palette");
                Assert.That(AssetDatabase.GetAssetPath(normal),
                    Is.EqualTo($"{MaterialMapRoot}/JSS_{expectedMapNames[index]}Normal.png"));
                Assert.That(AssetDatabase.GetAssetPath(surfaceMask),
                    Is.EqualTo($"{MaterialMapRoot}/JSS_{expectedMapNames[index]}SurfaceMask.png"));
                Assert.That(AssetDatabase.GetAssetPath(paletteMask),
                    Is.EqualTo($"{MaterialMapRoot}/JSS_{expectedMapNames[index]}PaletteMask.png"));
                Assert.That(normal.width, Is.GreaterThanOrEqualTo(64));
                Assert.That(normal.height, Is.GreaterThanOrEqualTo(64));
                Assert.That((AssetImporter.GetAtPath(
                        AssetDatabase.GetAssetPath(normal)) as TextureImporter)?.textureType,
                    Is.EqualTo(TextureImporterType.NormalMap));
            }

            var litResponses = surfaceMaterials
                .Select(material => material.GetFloat("_Smoothness"))
                .Distinct()
                .ToArray();
            Assert.That(litResponses, Has.Length.EqualTo(5),
                "All five surface families need distinct live graph responses.");
            Assert.That(File.ReadAllText(surfaceGraph),
                Does.Contain("Normal Map")
                    .And.Contain("Mask Map")
                    .And.Contain("Palette Mask")
                    .And.Contain("Palette Color")
                    .And.Contain("Palette Response"));

            var paletteShader =
                "Assets/_JustSomeStars/Runtime/Animation2D/CaptainLayeredSprite.shader";
            Assert.That(File.ReadAllText(paletteShader),
                Does.Contain("_PaletteMask").And.Contain("_Signal"));

            var dependencies = AssetDatabase.GetDependencies(ScenePath, true);
            Assert.That(dependencies, Does.Contain(surfaceGraph));
            Assert.That(dependencies, Does.Contain(emissionGraph));
            Assert.That(dependencies, Does.Contain(paletteShader));
            foreach (var pair in expectedMaterials)
            {
                Assert.That(dependencies, Does.Contain($"{pair.Value}/{pair.Key}"));
            }
            Assert.That(dependencies.Count(path =>
                    path.StartsWith(MaterialMapRoot, StringComparison.Ordinal)),
                Is.GreaterThanOrEqualTo(15),
                "The production scene must bind all normal/surface/palette maps.");
        }

        [Test]
        public void ProductionMirra_StagesQualityActorsAndOwnedShipWithinBudgets()
        {
            var priorSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var roots = scene.GetRootGameObjects();
                var all = roots.SelectMany(root =>
                        root.GetComponentsInChildren<Transform>(true))
                    .ToArray();
                var task28Ui = all.Single(item => item.name == "Task28PlayerUi");
                foreach (var name in new[]
                {
                    "Captain",
                    "Mira",
                    "Juno",
                    "Ori",
                    "OwnedPlayerShipPresentation",
                })
                {
                    Assert.That(
                        all.Count(item => item.name == name &&
                            !item.IsChildOf(task28Ui)),
                        Is.EqualTo(1),
                        name);
                }

                var ownedShip = all.Single(item =>
                    item.name == "OwnedPlayerShipPresentation");
                var shipRenderers = ownedShip.GetComponentsInChildren<SpriteRenderer>(true);
                Assert.That(shipRenderers, Is.Not.Empty);
                Assert.That(shipRenderers.Select(renderer =>
                        AssetDatabase.GetAssetPath(renderer.sprite)),
                    Has.All.StartsWith(
                        "Assets/_JustSomeStars/Art/2D/Ship/PlayerShip/"));

                var qualityType = FindType(QualityTypeName);
                Assert.That(qualityType, Is.Not.Null);
                var quality = roots.SelectMany(root =>
                        root.GetComponentsInChildren(qualityType, true))
                    .Cast<Component>()
                    .Single();
                Assert.That(quality, Is.InstanceOf<ISurfaceGameplayExtension>());
                var profiles = Read<Array>(quality, "Profiles").Cast<object>().ToArray();
                Assert.That(profiles, Has.Length.EqualTo(4));
                Assert.That(profiles, Has.All.Not.Null);

                var lifecycle = roots.SelectMany(root =>
                        root.GetComponentsInChildren<SurfaceGameplayLifecycle2D>(true))
                    .Single();
                var extensions = ReadField<MonoBehaviour[]>(lifecycle,
                    "gameplayExtensions");
                Assert.That(extensions, Does.Contain(quality));

                Assert.That(roots.Sum(root => CountByType(root,
                        "UnityEngine.Rendering.Universal.Light2D")),
                    Is.InRange(1, 4));
                Assert.That(roots.Sum(root =>
                        root.GetComponentsInChildren<ParticleSystem>(true).Length),
                    Is.InRange(1, 3));
                var volumeType = FindType("UnityEngine.Rendering.Volume");
                var volumes = roots.SelectMany(root =>
                        root.GetComponentsInChildren(volumeType, true))
                    .Cast<Behaviour>()
                    .ToArray();
                Assert.That(volumes, Has.Length.EqualTo(2));
                Assert.That(
                    volumes.Count(volume => volume.isActiveAndEnabled),
                    Is.EqualTo(1));

                var bandRoot = all.Single(item => item.name == "Bands");
                Assert.That(bandRoot.childCount, Is.EqualTo(8));
                Assert.That(all.Count(item => item.name == "FinalLightingAndGrading"),
                    Is.EqualTo(1));
            }
            finally
            {
                if (priorSetup.Any(item => item.isLoaded &&
                    !string.IsNullOrEmpty(item.path)))
                {
                    EditorSceneManager.RestoreSceneManagerSetup(priorSetup);
                }
            }
        }

        private static int CountByType(GameObject root, string fullName)
        {
            return root.GetComponentsInChildren<Component>(true)
                .Count(component => component != null &&
                    component.GetType().FullName == fullName);
        }

        private static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(type => type != null);
        }

        private static T Read<T>(object target, string property)
        {
            return (T)target.GetType().GetProperty(
                property,
                BindingFlags.Public | BindingFlags.Instance)?.GetValue(target);
        }

        private static T ReadField<T>(object target, string field)
        {
            return (T)target.GetType().GetField(
                field,
                BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(target);
        }
    }
}
