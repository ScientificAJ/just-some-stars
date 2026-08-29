using System;
using System.IO;
using System.Linq;
using JustSomeStars.Editor.Validation;
using JustSomeStars.Runtime.Animation2D;
using JustSomeStars.Runtime.Cosmetics;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class CaptainSpriteCompatibilityTests
    {
        private const string SpriteSetPath =
            "Assets/_JustSomeStars/Content/Characters/CaptainSpriteSet.asset";
        private const string PackagePath =
            "Assets/_JustSomeStars/Art/2D/Characters/Captain/" +
            "captain-sprite-package.json";
        private const string ScenePath =
            "Assets/_JustSomeStars/Scenes/Benchmarks/Mirra2DProof.unity";

        [Test]
        public void RequiredLaunchLook_HasExactFamilyFacingLayerAndClipMatrix()
        {
            var spriteSet = AssetDatabase.LoadAssetAtPath<CaptainSpriteSet>(
                SpriteSetPath);
            Assert.That(spriteSet, Is.Not.Null, SpriteSetPath);
            Assert.That(spriteSet.Entries.Count, Is.EqualTo(30));
            Assert.That(spriteSet.PaletteMasks.Count, Is.EqualTo(30));
            Assert.That(spriteSet.ModuleTextures.Count, Is.EqualTo(66));
            Assert.That(spriteSet.FrameAnchors.Count, Is.EqualTo(288));
            Assert.That(spriteSet.CustomizationShader, Is.Not.Null);

            foreach (CaptainBodyFamily family in Enum.GetValues(
                         typeof(CaptainBodyFamily)))
            {
                var loadout = CaptainSpriteLoadout.CreateLaunchLook(family);
                Assert.That(loadout.SkinSwatch, Is.EqualTo("skin-5"));
                Assert.That(loadout.SuitColorway, Is.EqualTo("sandstone"));
                Assert.That(loadout.SignalState, Is.EqualTo("active-cyan"));
                Assert.That(
                    CaptainSpriteCompatibilityValidator.Validate(spriteSet, loadout),
                    Is.Empty,
                    family.ToString());
                foreach (var facing in new[] { SpriteFacing.Right, SpriteFacing.Left })
                {
                    foreach (CaptainSpriteLayer layer in Enum.GetValues(
                                 typeof(CaptainSpriteLayer)))
                    {
                        var layerSet = spriteSet.Find(family, facing, layer);
                        Assert.That(layerSet, Is.Not.Null);
                        Assert.That(layerSet.Clips.Count, Is.EqualTo(8));
                        Assert.That(
                            layerSet.Clips.Select(clip => clip.Frames.Count),
                            Is.EqualTo(new[] { 4, 8, 4, 6, 4, 8, 8, 6 }));
                    }
                }
            }
        }

        [Test]
        public void PackageMetadata_CoversCatalogAnchorsMasksAndBoundedPages()
        {
            var spriteSet = AssetDatabase.LoadAssetAtPath<CaptainSpriteSet>(
                SpriteSetPath);
            Assert.That(spriteSet, Is.Not.Null, SpriteSetPath);
            var package = File.ReadAllText(PackagePath);
            StringAssert.Contains("\"publicationCount\": 30", package);
            StringAssert.Contains("\"proofSourceRowCount\": 240", package);
            StringAssert.Contains("\"paletteMaskRowCount\": 240", package);
            StringAssert.Contains("\"compositeClipCount\": 48", package);
            StringAssert.Contains("\"rawPublicationSheets\":", package);
            StringAssert.Contains("\"customizationMatrix\":", package);
            StringAssert.Contains("\"attachmentMatrix\":", package);
            foreach (var anchor in CaptainSpriteSet.RequiredAnchors)
            {
                StringAssert.Contains($"\"{anchor}\"", package);
            }
            StringAssert.Contains("\"modulePublicationCount\": 66", package);
            StringAssert.Contains("\"moduleOptionFrameCount\": 15264", package);
            foreach (var channel in new[] { "skin", "hair", "suit", "Signal" })
            {
                StringAssert.Contains($"\"{channel}\"", package);
            }

            var atlasPaths = Directory.GetFiles(
                "Assets/_JustSomeStars/Art/2D/Characters/Captain/Atlases",
                "*.png",
                SearchOption.AllDirectories);
            Assert.That(atlasPaths, Has.Length.EqualTo(30));
            foreach (var path in atlasPaths)
            {
                var assetPath = path.Replace('\\', '/');
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                Assert.That(texture, Is.Not.Null, assetPath);
                Assert.That(texture.width, Is.LessThanOrEqualTo(2048));
                Assert.That(texture.height, Is.LessThanOrEqualTo(2048));
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                Assert.That(importer, Is.Not.Null, assetPath);
                Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(100f));
                Assert.That(importer.mipmapEnabled, Is.False);
                Assert.That(importer.isReadable, Is.False);
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(
                    importer.GetPlatformTextureSettings("Android").format,
                    Is.EqualTo(TextureImporterFormat.ASTC_6x6));
            }

            var spriteAtlasPaths = Directory.GetFiles(
                "Assets/_JustSomeStars/Art/2D/Characters/Captain/Atlases",
                "*.spriteatlas",
                SearchOption.AllDirectories);
            Assert.That(spriteAtlasPaths, Has.Length.EqualTo(30));
            foreach (var path in spriteAtlasPaths)
            {
                var assetPath = path.Replace('\\', '/');
                var spriteAtlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(
                    assetPath);
                Assert.That(spriteAtlas, Is.Not.Null, assetPath);
                Assert.That(spriteAtlas.GetPackables(), Has.Length.EqualTo(1));
                Assert.That(
                    AssetDatabase.GetAssetPath(spriteAtlas.GetPackables()[0]),
                    Is.EqualTo(Path.ChangeExtension(assetPath, ".png")));
            }

            foreach (var entry in spriteSet.PaletteMasks)
            {
                AssertCustomizationTexture(entry.Texture);
            }
            foreach (var entry in spriteSet.ModuleTextures)
            {
                AssertCustomizationTexture(entry.Texture);
                Assert.That(entry.OptionIds.Count, Is.InRange(3, 8));
            }
            foreach (var frame in spriteSet.FrameAnchors)
            {
                Assert.That(frame.Points.Count, Is.EqualTo(14));
                Assert.That(
                    frame.Points.Select(point => point.Id),
                    Is.EquivalentTo(CaptainSpriteSet.RequiredAnchors));
            }
            var addressable = AddressableAssetSettingsDefaultObject.Settings
                .FindAssetEntry(AssetDatabase.AssetPathToGUID(SpriteSetPath));
            Assert.That(addressable, Is.Not.Null);
            Assert.That(
                addressable.address,
                Is.EqualTo("characters/captain/sprite-set"));
        }

        [Test]
        public void Scene_UsesRealCaptainSpriteSetAndExactlyFiveVisualLayers()
        {
            var priorSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var captain = GameObject.Find("Captain");
                Assert.That(captain, Is.Not.Null);
                var renderer = captain.GetComponent<LayeredCharacterRenderer>();
                Assert.That(renderer, Is.Not.Null);
                Assert.That(
                    AssetDatabase.GetAssetPath(renderer.SpriteSet),
                    Is.EqualTo(SpriteSetPath));
                Assert.That(renderer.LayerRenderers.Count, Is.EqualTo(5));
                Assert.That(
                    renderer.LayerRenderers.Count(item => item != null),
                    Is.EqualTo(5));
                Assert.That(
                    captain.transform.localScale.x,
                    Is.EqualTo(1f).Within(0.0001f));
                Assert.That(
                    captain.transform.localScale.y,
                    Is.EqualTo(1f).Within(0.0001f));
                var visualRoot = captain.transform.Find("CaptainVisualRoot");
                Assert.That(visualRoot, Is.Not.Null);
                Assert.That(
                    visualRoot.localScale.x,
                    Is.EqualTo(1.90f).Within(0.0001f));
                Assert.That(
                    visualRoot.localScale.y,
                    Is.EqualTo(1.90f).Within(0.0001f));
                Assert.That(captain.GetComponent<SpriteRenderer>(), Is.Null);
                Assert.That(
                    renderer.LayerRenderers.Select(item => item.transform.parent),
                    Has.All.EqualTo(visualRoot));
                var collider = captain.GetComponent<CapsuleCollider2D>();
                Assert.That(collider, Is.Not.Null);
                Assert.That(
                    collider.bounds.size.x,
                    Is.EqualTo(0.8037f).Within(0.01f));
                Assert.That(
                    collider.bounds.size.y,
                    Is.EqualTo(1.7796f).Within(0.01f));
            }
            finally
            {
                if (priorSetup.Length > 0 && priorSetup.Any(item => item.isLoaded))
                {
                    EditorSceneManager.RestoreSceneManagerSetup(priorSetup);
                }
                else
                {
                    EditorSceneManager.NewScene(
                        NewSceneSetup.EmptyScene,
                        NewSceneMode.Single);
                }
            }
        }

        [Test]
        public void Validator_RejectsSixthLayerMissingEntryAndTimingDrift()
        {
            Assert.Throws<InvalidOperationException>(() =>
                CaptainSpriteLoadout.CreateLaunchLook(
                    CaptainBodyFamily.Average,
                    activeLayerCount: 6));

            var spriteSet = ScriptableObject.CreateInstance<CaptainSpriteSet>();
            try
            {
                Assert.Throws<InvalidOperationException>(() =>
                    spriteSet.Configure(Array.Empty<CaptainSpriteSetEntry>()));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(spriteSet);
            }
        }

        private static void AssertCustomizationTexture(Texture2D texture)
        {
            Assert.That(texture, Is.Not.Null);
            var path = AssetDatabase.GetAssetPath(texture);
            StringAssert.Contains("/Customization/", path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Default));
            Assert.That(
                importer.sRGBTexture,
                Is.EqualTo(!path.Contains(
                    "/Customization/PaletteMasks/",
                    StringComparison.Ordinal)),
                path);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.isReadable, Is.False);
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(
                importer.GetPlatformTextureSettings("Android").format,
                Is.EqualTo(TextureImporterFormat.ASTC_6x6));
        }
    }
}
