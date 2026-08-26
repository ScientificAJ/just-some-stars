using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace JustSomeStars.Editor.Importers
{
    public sealed class CharacterSpritePostprocessor : AssetPostprocessor
    {
        internal const string AtlasRoot =
            "Assets/_JustSomeStars/Art/2D/Characters/Atlases/";
        private const string CharacterRoot =
            "Assets/_JustSomeStars/Art/2D/Characters/";
        internal const string ManifestSuffix = ".sprite-manifest.json";
        internal const string ManifestHashSuffix = ".sprite-manifest.sha256";
        private const int SupportedSchemaVersion = 1;

        internal static bool IsCanonicalAtlasPath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   path.StartsWith(CharacterRoot, StringComparison.Ordinal) &&
                   (path.StartsWith(AtlasRoot, StringComparison.Ordinal) ||
                    path.Contains("/Atlases/", StringComparison.Ordinal)) &&
                   path.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsCanonicalCustomizationTexturePath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   path.StartsWith(CharacterRoot, StringComparison.Ordinal) &&
                   path.Contains("/Customization/", StringComparison.Ordinal) &&
                   path.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
        }

        internal static CharacterSpriteManifest LoadValidatedManifest(
            string atlasAssetPath,
            string projectRoot)
        {
            if (!IsCanonicalAtlasPath(atlasAssetPath))
            {
                throw new InvalidDataException(
                    $"Character sprite manifest requested outside {AtlasRoot}: " +
                    atlasAssetPath);
            }

            var absoluteRoot = Path.GetFullPath(projectRoot);
            var absoluteAtlas = Path.GetFullPath(
                Path.Combine(absoluteRoot, atlasAssetPath));
            var manifestAssetPath = atlasAssetPath[..^4] + ManifestSuffix;
            var manifestHashAssetPath = atlasAssetPath[..^4] + ManifestHashSuffix;
            var absoluteManifest = Path.GetFullPath(
                Path.Combine(absoluteRoot, manifestAssetPath));
            var absoluteManifestHash = Path.GetFullPath(
                Path.Combine(absoluteRoot, manifestHashAssetPath));
            if (!IsWithinRoot(absoluteAtlas, absoluteRoot) ||
                !IsWithinRoot(absoluteManifest, absoluteRoot) ||
                !IsWithinRoot(absoluteManifestHash, absoluteRoot))
            {
                throw new InvalidDataException(
                    "Character sprite import path escaped the project root.");
            }
            if (!File.Exists(absoluteAtlas))
            {
                throw new FileNotFoundException(
                    "Character sprite atlas is missing.",
                    absoluteAtlas);
            }
            if (!File.Exists(absoluteManifest))
            {
                throw new FileNotFoundException(
                    "Character sprite manifest is missing.",
                    absoluteManifest);
            }

            CharacterSpriteManifest manifest;
            try
            {
                manifest = JsonUtility.FromJson<CharacterSpriteManifest>(
                    File.ReadAllText(absoluteManifest));
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    $"Character sprite manifest is malformed: {manifestAssetPath}",
                    exception);
            }

            ValidateManifest(manifest, atlasAssetPath, absoluteAtlas);
            if (!File.Exists(absoluteManifestHash))
            {
                throw new FileNotFoundException(
                    "Character sprite manifest hash is missing.",
                    absoluteManifestHash);
            }
            var declaredManifestHash = File.ReadAllText(absoluteManifestHash).Trim();
            if (!string.Equals(
                    declaredManifestHash,
                    Sha256(absoluteManifest),
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Character sprite manifest hash is stale or malformed.");
            }
            return manifest;
        }

        private void OnPreprocessTexture()
        {
            if (IsCanonicalCustomizationTexturePath(assetPath))
            {
                ApplyCustomizationTextureSettings((TextureImporter)assetImporter);
                return;
            }
            if (!IsCanonicalAtlasPath(assetPath))
            {
                return;
            }
            var manifest = LoadValidatedManifest(
                assetPath,
                Path.GetDirectoryName(Application.dataPath));
            ApplyImporterSettings((TextureImporter)assetImporter, manifest);
        }

        internal static void ApplyCustomizationTextureSettings(
            TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.sRGBTexture = !importer.assetPath.Replace('\\', '/').Contains(
                "/Customization/PaletteMasks/",
                StringComparison.Ordinal);
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.compressionQuality = 100;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android",
                overridden = true,
                maxTextureSize = 2048,
                resizeAlgorithm = TextureResizeAlgorithm.Mitchell,
                format = TextureImporterFormat.ASTC_6x6,
                textureCompression = TextureImporterCompression.Compressed,
                compressionQuality = 100,
                crunchedCompression = false,
                allowsAlphaSplitting = false,
            });
        }

        internal static void ApplyImporterSettings(
            TextureImporter importer,
            CharacterSpriteManifest manifest)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = manifest.pixelsPerUnit;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.compressionQuality = 100;

#pragma warning disable CS0618
            importer.spritesheet = manifest.clips
                .SelectMany(clip => clip.frames)
                .Select(frame => new SpriteMetaData
                {
                    name = frame.spriteName,
                    rect = new Rect(
                        frame.rectPixels.x,
                        frame.rectPixels.y,
                        frame.rectPixels.width,
                        frame.rectPixels.height),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = new Vector2(
                        frame.pivotNormalized[0],
                        frame.pivotNormalized[1]),
                    border = Vector4.zero,
                })
                .ToArray();
#pragma warning restore CS0618

            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android",
                overridden = true,
                maxTextureSize = 2048,
                resizeAlgorithm = TextureResizeAlgorithm.Mitchell,
                format = TextureImporterFormat.ASTC_6x6,
                textureCompression = TextureImporterCompression.Compressed,
                compressionQuality = 100,
                crunchedCompression = false,
                allowsAlphaSplitting = false,
            });
        }

        private static void ValidateManifest(
            CharacterSpriteManifest manifest,
            string atlasAssetPath,
            string absoluteAtlas)
        {
            if (manifest == null || manifest.schemaVersion != SupportedSchemaVersion)
            {
                throw new InvalidDataException(
                    "Unsupported character sprite manifest schema.");
            }
            if (string.IsNullOrWhiteSpace(manifest.characterId) ||
                manifest.pixelsPerUnit <= 0 || manifest.atlas == null ||
                manifest.atlas.format != "PNG" ||
                manifest.atlas.width <= 0 || manifest.atlas.height <= 0 ||
                !string.Equals(
                    manifest.atlas.path,
                    Path.GetFileName(atlasAssetPath),
                    StringComparison.Ordinal) ||
                !string.Equals(
                    manifest.atlas.sha256,
                    Sha256(absoluteAtlas),
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Character sprite atlas identity does not match its manifest.");
            }
            if (manifest.validation == null || !manifest.validation.isValid ||
                manifest.validation.issues == null ||
                manifest.validation.issues.Length != 0)
            {
                throw new InvalidDataException(
                    "Character sprite manifest has no clean validation result.");
            }
            if (manifest.clips == null || manifest.clips.Length == 0 ||
                manifest.clips.Any(clip => clip == null ||
                    string.IsNullOrWhiteSpace(clip.id) ||
                    clip.frames == null || clip.frames.Length == 0) ||
                manifest.clips.Select(clip => clip.id)
                    .Distinct(StringComparer.Ordinal).Count() != manifest.clips.Length)
            {
                throw new InvalidDataException(
                    "Character sprite manifest has invalid clip rows.");
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var clip in manifest.clips)
            {
                if (!Enum.TryParse<JustSomeStars.Runtime.Animation2D.SpriteFacing>(
                        clip.facing,
                        ignoreCase: false,
                        out _) ||
                    !Enum.TryParse<JustSomeStars.Runtime.Animation2D.SpriteAnimationLoopMode>(
                        clip.loopMode,
                        ignoreCase: false,
                        out _))
                {
                    throw new InvalidDataException(
                        $"Clip {clip.id} has unsupported facing or loop mode.");
                }
                for (var index = 0; index < clip.frames.Length; index++)
                {
                    var frame = clip.frames[index];
                    if (frame == null || frame.index != index ||
                        string.IsNullOrWhiteSpace(frame.spriteName) ||
                        !names.Add(frame.spriteName) ||
                        frame.rectPixels == null ||
                        frame.rectPixels.width <= 0 || frame.rectPixels.height <= 0 ||
                        frame.rectPixels.x < 0 || frame.rectPixels.y < 0 ||
                        frame.rectPixels.x + frame.rectPixels.width > manifest.atlas.width ||
                        frame.rectPixels.y + frame.rectPixels.height > manifest.atlas.height ||
                        frame.pivotNormalized == null ||
                        frame.pivotNormalized.Length != 2 ||
                        frame.pivotNormalized.Any(value => value < 0f || value > 1f) ||
                        frame.durationSeconds <= 0f ||
                        frame.contacts == null || frame.events == null)
                    {
                        throw new InvalidDataException(
                            $"Clip {clip.id} frame {index} is invalid.");
                    }
                }
            }
        }

        private static bool IsWithinRoot(string path, string root)
        {
            return path.StartsWith(
                root + Path.DirectorySeparatorChar,
                StringComparison.Ordinal);
        }

        internal static string Sha256(string path)
        {
            using var stream = File.OpenRead(path);
            using var hash = SHA256.Create();
            return string.Concat(hash.ComputeHash(stream)
                .Select(value => value.ToString("x2")));
        }
    }

    [Serializable]
    internal sealed class CharacterSpriteManifest
    {
        public int schemaVersion;
        public string characterId;
        public int pixelsPerUnit;
        public string sourceRequestSha256;
        public SpriteAtlasManifest atlas;
        public SpriteClipManifest[] clips;
        public SpriteValidationManifest validation;
    }

    [Serializable]
    internal sealed class SpriteAtlasManifest
    {
        public string path;
        public string format;
        public int width;
        public int height;
        public string sha256;
    }

    [Serializable]
    internal sealed class SpriteClipManifest
    {
        public string id;
        public string facing;
        public string loopMode;
        public int cadenceFps;
        public string sourceStrip;
        public string sourceStripSha256;
        public SpriteFrameManifest[] frames;
    }

    [Serializable]
    internal sealed class SpriteFrameManifest
    {
        public int index;
        public string spriteName;
        public SpriteRectManifest rectPixels;
        public float[] pivotNormalized;
        public float durationSeconds;
        public string[] contacts;
        public SpriteEventManifest[] events;
        public SpriteAnchorManifest[] anchors;
        public int sourceBaselinePixels;
        public int registrationOffsetPixels;
        public int registeredBaselinePixels;
        public int[] alphaBoundsPixels;
        public int interiorAlphaHolePixels;
    }

    [Serializable]
    internal sealed class SpriteRectManifest
    {
        public int x;
        public int y;
        public int width;
        public int height;
    }

    [Serializable]
    internal sealed class SpriteEventManifest
    {
        public string id;
        public string kind;
    }

    [Serializable]
    internal sealed class SpriteAnchorManifest
    {
        public string id;
        public float[] sourcePixels;
        public float[] runtimePixels;
    }

    [Serializable]
    internal sealed class SpriteValidationManifest
    {
        public bool isValid;
        public string[] issues;
    }
}
