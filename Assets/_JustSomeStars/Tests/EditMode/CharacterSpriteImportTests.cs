using System;
using System.IO;
using System.Linq;
using JustSomeStars.Editor.Importers;
using JustSomeStars.Editor.Validation;
using JustSomeStars.Runtime.Animation2D;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class CharacterSpriteImportTests
    {
        private const string AtlasPath =
            "Assets/_JustSomeStars/Art/2D/Characters/Atlases/Fixtures/" +
            "primitive-stage2.png";
        private const string SpriteAtlasPath =
            "Assets/_JustSomeStars/Art/2D/Characters/Atlases/Fixtures/" +
            "primitive-stage2.spriteatlas";
        private const string SpriteSetPath =
            "Assets/_JustSomeStars/Art/2D/Characters/Definitions/Fixtures/" +
            "PrimitiveStage2SpriteSet.asset";

        [Test]
        public void Scope_IsLimitedToCanonicalCharacterAtlasPngs()
        {
            Assert.That(CharacterSpritePostprocessor.IsCanonicalAtlasPath(AtlasPath), Is.True);
            Assert.That(
                CharacterSpritePostprocessor.IsCanonicalAtlasPath(
                    "Assets/_JustSomeStars/Art/2D/Characters/Atlases/captain.PNG"),
                Is.True);
            Assert.That(
                CharacterSpritePostprocessor.IsCanonicalAtlasPath(
                    "Assets/_JustSomeStars/Art/2D/Characters/Source/captain.png"),
                Is.False);
            Assert.That(
                CharacterSpritePostprocessor.IsCanonicalAtlasPath(
                    "Assets/ThirdParty/captain.png"),
                Is.False);
            Assert.That(
                CharacterSpritePostprocessor.IsCanonicalAtlasPath(
                    "Assets/_JustSomeStars/Art/2D/Characters/Atlases/captain.webp"),
                Is.False);
        }

        [Test]
        public void MissingMalformedAndStaleManifest_FailsClosed()
        {
            var root = Path.Combine(
                Path.GetFullPath("Builds/Task12Stage2"),
                "CharacterSpriteImportTests",
                Guid.NewGuid().ToString("N"));
            const string assetPath =
                "Assets/_JustSomeStars/Art/2D/Characters/Atlases/Fixtures/bad.png";
            var absoluteAtlas = Path.Combine(root, assetPath);
            var absoluteManifest = Path.ChangeExtension(
                absoluteAtlas,
                null) + CharacterSpritePostprocessor.ManifestSuffix;
            var absoluteManifestHash = Path.ChangeExtension(
                absoluteAtlas,
                null) + CharacterSpritePostprocessor.ManifestHashSuffix;
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteAtlas));
            File.WriteAllBytes(absoluteAtlas, new byte[] { 1, 2, 3, 4 });
            try
            {
                Assert.Throws<FileNotFoundException>(() =>
                    CharacterSpritePostprocessor.LoadValidatedManifest(assetPath, root));

                File.WriteAllText(absoluteManifest, "{not-json");
                Assert.Throws<InvalidDataException>(() =>
                    CharacterSpritePostprocessor.LoadValidatedManifest(assetPath, root));

                File.WriteAllText(
                    absoluteManifest,
                    "{\"schemaVersion\":1,\"characterId\":\"bad\"," +
                    "\"pixelsPerUnit\":64,\"atlas\":{" +
                    "\"path\":\"bad.png\",\"format\":\"PNG\"," +
                    "\"width\":64,\"height\":96," +
                    "\"sha256\":\"stale\"},\"clips\":[]," +
                    "\"validation\":{\"isValid\":true,\"issues\":[]}}");
                Assert.Throws<InvalidDataException>(() =>
                    CharacterSpritePostprocessor.LoadValidatedManifest(assetPath, root));

                var canonicalManifestPath = AtlasPath[..^4] +
                    CharacterSpritePostprocessor.ManifestSuffix;
                var canonicalAtlasHash = CharacterSpritePostprocessor.Sha256(AtlasPath);
                var fixtureAtlasHash = CharacterSpritePostprocessor.Sha256(absoluteAtlas);
                var validForFixture = File.ReadAllText(canonicalManifestPath)
                    .Replace("primitive-stage2.png", "bad.png")
                    .Replace(canonicalAtlasHash, fixtureAtlasHash);
                File.WriteAllText(absoluteManifest, validForFixture);
                Assert.Throws<FileNotFoundException>(() =>
                    CharacterSpritePostprocessor.LoadValidatedManifest(assetPath, root));

                File.WriteAllText(absoluteManifestHash, new string('0', 64) + "\n");
                Assert.Throws<InvalidDataException>(() =>
                    CharacterSpritePostprocessor.LoadValidatedManifest(assetPath, root));

                File.WriteAllText(
                    absoluteManifestHash,
                    CharacterSpritePostprocessor.Sha256(absoluteManifest) + "\n");
                Assert.DoesNotThrow(() =>
                    CharacterSpritePostprocessor.LoadValidatedManifest(assetPath, root));
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
        public void CanonicalAtlas_ImportsEveryManifestFrameWithExplicitMobilePolicy()
        {
            var manifest = CharacterSpritePostprocessor.LoadValidatedManifest(
                AtlasPath,
                Path.GetFullPath("."));
            var importer = AssetImporter.GetAtPath(AtlasPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null, AtlasPath);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple));
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(128f));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.isReadable, Is.False);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Compressed));
            var android = importer.GetPlatformTextureSettings("Android");
            Assert.That(android.overridden, Is.True);
            Assert.That(android.format, Is.EqualTo(TextureImporterFormat.ASTC_6x6));
            Assert.That(android.maxTextureSize, Is.EqualTo(2048));

            var expectedNames = manifest.clips
                .SelectMany(clip => clip.frames)
                .Select(frame => frame.spriteName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            var importedNames = AssetDatabase.LoadAllAssetsAtPath(AtlasPath)
                .OfType<Sprite>()
                .Select(sprite => sprite.name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            Assert.That(importedNames, Is.EqualTo(expectedNames));
            Assert.That(importedNames, Has.Length.EqualTo(12));
        }

        [Test]
        public void CanonicalAtlas_HasDeterministicSpriteAtlasOwnership()
        {
            var spriteAtlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(SpriteAtlasPath);
            Assert.That(spriteAtlas, Is.Not.Null, SpriteAtlasPath);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            Assert.That(texture, Is.Not.Null, AtlasPath);
            Assert.That(
                spriteAtlas.GetPackables().Select(AssetDatabase.GetAssetPath),
                Does.Contain(AtlasPath));
            Assert.That(spriteAtlas.GetPackables(), Has.Length.EqualTo(1));
        }

        [Test]
        public void CanonicalSpriteSet_ReconcilesManifestPivotsContactsAndEvents()
        {
            var manifest = CharacterSpritePostprocessor.LoadValidatedManifest(
                AtlasPath,
                Path.GetFullPath("."));
            var spriteSet = AssetDatabase.LoadAssetAtPath<CharacterSpriteSet>(SpriteSetPath);
            Assert.That(spriteSet, Is.Not.Null, SpriteSetPath);
            CharacterSpriteSetValidator.ValidateOrThrow(spriteSet, manifest);

            Assert.That(spriteSet.CharacterId, Is.EqualTo("primitive-stage2"));
            Assert.That(
                spriteSet.Clips.Select(clip => clip.StableId),
                Is.EqualTo(new[] { "primitive.idle.right", "primitive.run.right" }));
            Assert.That(spriteSet.FindClip("primitive.run.right").Frames, Has.Length.EqualTo(8));
            Assert.That(
                spriteSet.FindClip("primitive.run.right").FrameEvents
                    .Count(frameEvent => frameEvent.Kind == SpriteFrameEventKind.FootContact),
                Is.EqualTo(4));
        }

        [Test]
        public void ForcedReimport_RemainsIdempotent()
        {
            AssetDatabase.ImportAsset(
                AtlasPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            var first = AssetDatabase.LoadAllAssetsAtPath(AtlasPath)
                .OfType<Sprite>()
                .Select(sprite => sprite.name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            AssetDatabase.ImportAsset(
                AtlasPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            var second = AssetDatabase.LoadAllAssetsAtPath(AtlasPath)
                .OfType<Sprite>()
                .Select(sprite => sprite.name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            Assert.That(second, Is.EqualTo(first));
            Assert.That(second, Has.Length.EqualTo(12));
        }
    }
}
