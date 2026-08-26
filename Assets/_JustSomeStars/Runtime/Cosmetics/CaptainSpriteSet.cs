using System;
using System.Collections.Generic;
using System.Linq;
using JustSomeStars.Runtime.Animation2D;
using UnityEngine;

namespace JustSomeStars.Runtime.Cosmetics
{
    [Serializable]
    public sealed class CaptainSpriteSetEntry
    {
        [SerializeField] private CaptainBodyFamily family;
        [SerializeField] private SpriteFacing facing;
        [SerializeField] private CaptainSpriteLayer layer;
        [SerializeField] private CharacterSpriteSet spriteSet;

        public CaptainSpriteSetEntry(
            CaptainBodyFamily family,
            SpriteFacing facing,
            CaptainSpriteLayer layer,
            CharacterSpriteSet spriteSet)
        {
            this.family = family;
            this.facing = facing;
            this.layer = layer;
            this.spriteSet = spriteSet;
        }

        public CaptainBodyFamily Family => family;
        public SpriteFacing Facing => facing;
        public CaptainSpriteLayer Layer => layer;
        public CharacterSpriteSet SpriteSet => spriteSet;
    }

    [Serializable]
    public sealed class CaptainPaletteMaskEntry
    {
        [SerializeField] private CaptainBodyFamily family;
        [SerializeField] private SpriteFacing facing;
        [SerializeField] private CaptainSpriteLayer layer;
        [SerializeField] private Texture2D texture;

        public CaptainPaletteMaskEntry(
            CaptainBodyFamily family,
            SpriteFacing facing,
            CaptainSpriteLayer layer,
            Texture2D texture)
        {
            this.family = family;
            this.facing = facing;
            this.layer = layer;
            this.texture = texture;
        }

        public CaptainBodyFamily Family => family;
        public SpriteFacing Facing => facing;
        public CaptainSpriteLayer Layer => layer;
        public Texture2D Texture => texture;
    }

    [Serializable]
    public sealed class CaptainModuleTextureEntry
    {
        [SerializeField] private CaptainBodyFamily family;
        [SerializeField] private SpriteFacing facing;
        [SerializeField] private CaptainCustomizationCategory category;
        [SerializeField] private CaptainSpriteLayer targetLayer;
        [SerializeField] private string[] optionIds = Array.Empty<string>();
        [SerializeField] private Texture2D texture;

        public CaptainModuleTextureEntry(
            CaptainBodyFamily family,
            SpriteFacing facing,
            CaptainCustomizationCategory category,
            CaptainSpriteLayer targetLayer,
            string[] optionIds,
            Texture2D texture)
        {
            this.family = family;
            this.facing = facing;
            this.category = category;
            this.targetLayer = targetLayer;
            this.optionIds = optionIds == null
                ? Array.Empty<string>()
                : (string[])optionIds.Clone();
            this.texture = texture;
        }

        public CaptainBodyFamily Family => family;
        public SpriteFacing Facing => facing;
        public CaptainCustomizationCategory Category => category;
        public CaptainSpriteLayer TargetLayer => targetLayer;
        public IReadOnlyList<string> OptionIds => optionIds;
        public Texture2D Texture => texture;

        public Vector4 UvScaleOffset(string optionId)
        {
            var index = Array.IndexOf(optionIds, optionId);
            if (index < 0 || index >= 8)
            {
                throw new KeyNotFoundException(
                    $"Captain module {category} has no option {optionId}.");
            }
            var column = index % 4;
            var row = index / 4;
            return new Vector4(0.25f, 0.5f, column * 0.25f, row == 0 ? 0.5f : 0f);
        }
    }

    [Serializable]
    public sealed class CaptainAnchorPoint
    {
        [SerializeField] private string id;
        [SerializeField] private Vector2 runtimePixels;

        public CaptainAnchorPoint(string id, Vector2 runtimePixels)
        {
            this.id = id;
            this.runtimePixels = runtimePixels;
        }

        public string Id => id;
        public Vector2 RuntimePixels => runtimePixels;
    }

    [Serializable]
    public sealed class CaptainFrameAnchors
    {
        [SerializeField] private CaptainBodyFamily family;
        [SerializeField] private SpriteFacing facing;
        [SerializeField] private string motionId;
        [SerializeField] private int frameIndex;
        [SerializeField] private CaptainAnchorPoint[] points =
            Array.Empty<CaptainAnchorPoint>();

        public CaptainFrameAnchors(
            CaptainBodyFamily family,
            SpriteFacing facing,
            string motionId,
            int frameIndex,
            CaptainAnchorPoint[] points)
        {
            this.family = family;
            this.facing = facing;
            this.motionId = motionId;
            this.frameIndex = frameIndex;
            this.points = points == null
                ? Array.Empty<CaptainAnchorPoint>()
                : (CaptainAnchorPoint[])points.Clone();
        }

        public CaptainBodyFamily Family => family;
        public SpriteFacing Facing => facing;
        public string MotionId => motionId;
        public int FrameIndex => frameIndex;
        public IReadOnlyList<CaptainAnchorPoint> Points => points;
    }

    [CreateAssetMenu(
        fileName = "CaptainSpriteSet",
        menuName = "Just Some Stars/Animation 2D/Captain Sprite Set")]
    public sealed class CaptainSpriteSet : ScriptableObject
    {
        private static readonly string[] MotionIds =
        {
            "idle", "run", "turn", "jump", "land", "climb", "scan", "interact",
        };

        public static readonly string[] RequiredAnchors =
        {
            "Root", "LeftFoot", "RightFoot", "LeftHand", "RightHand",
            "HelmetRing", "BackpackSocket", "Belt", "LeftWrist", "RightWrist",
            "LeftBootTop", "RightBootTop", "ActiveTool", "StowedTool",
        };

        [SerializeField] private CaptainSpriteSetEntry[] entries =
            Array.Empty<CaptainSpriteSetEntry>();
        [SerializeField] private CaptainPaletteMaskEntry[] paletteMasks =
            Array.Empty<CaptainPaletteMaskEntry>();
        [SerializeField] private CaptainModuleTextureEntry[] moduleTextures =
            Array.Empty<CaptainModuleTextureEntry>();
        [SerializeField] private CaptainFrameAnchors[] frameAnchors =
            Array.Empty<CaptainFrameAnchors>();
        [SerializeField] private Shader customizationShader;

        [NonSerialized] private bool allowBaseOnlyForTests;

        public IReadOnlyList<CaptainSpriteSetEntry> Entries => entries;
        public IReadOnlyList<CaptainPaletteMaskEntry> PaletteMasks => paletteMasks;
        public IReadOnlyList<CaptainModuleTextureEntry> ModuleTextures =>
            moduleTextures;
        public IReadOnlyList<CaptainFrameAnchors> FrameAnchors => frameAnchors;
        public Shader CustomizationShader => customizationShader;

        public void Configure(CaptainSpriteSetEntry[] configuredEntries)
        {
            ConfigureEntries(configuredEntries);
            allowBaseOnlyForTests = true;
            ValidateOrThrow();
        }

        public void Configure(
            CaptainSpriteSetEntry[] configuredEntries,
            CaptainPaletteMaskEntry[] configuredPaletteMasks,
            CaptainModuleTextureEntry[] configuredModuleTextures,
            CaptainFrameAnchors[] configuredFrameAnchors,
            Shader configuredShader)
        {
            ConfigureEntries(configuredEntries);
            paletteMasks = configuredPaletteMasks == null
                ? Array.Empty<CaptainPaletteMaskEntry>()
                : (CaptainPaletteMaskEntry[])configuredPaletteMasks.Clone();
            moduleTextures = configuredModuleTextures == null
                ? Array.Empty<CaptainModuleTextureEntry>()
                : (CaptainModuleTextureEntry[])configuredModuleTextures.Clone();
            frameAnchors = configuredFrameAnchors == null
                ? Array.Empty<CaptainFrameAnchors>()
                : (CaptainFrameAnchors[])configuredFrameAnchors.Clone();
            customizationShader = configuredShader;
            allowBaseOnlyForTests = false;
            ValidateOrThrow();
        }

        private void ConfigureEntries(CaptainSpriteSetEntry[] configuredEntries)
        {
            if (configuredEntries == null || configuredEntries.Length != 30 ||
                configuredEntries.Any(entry => entry?.SpriteSet == null))
            {
                throw new InvalidOperationException(
                    "CaptainSpriteSet requires exactly 30 family/facing/layer entries.");
            }
            var unique = configuredEntries
                .Select(entry => (entry.Family, entry.Facing, entry.Layer))
                .Distinct()
                .Count();
            if (unique != 30)
            {
                throw new InvalidOperationException(
                    "CaptainSpriteSet contains duplicate compatibility entries.");
            }
            entries = (CaptainSpriteSetEntry[])configuredEntries.Clone();
        }

        public Texture2D FindPaletteMask(
            CaptainBodyFamily family,
            SpriteFacing facing,
            CaptainSpriteLayer layer)
        {
            var matches = paletteMasks.Where(entry =>
                entry != null && entry.Family == family &&
                entry.Facing == facing && entry.Layer == layer).ToArray();
            if (matches.Length != 1 || matches[0].Texture == null)
            {
                throw new KeyNotFoundException(
                    $"Captain palette mask is missing {family}/{facing}/{layer}.");
            }
            return matches[0].Texture;
        }

        public CaptainModuleTextureEntry FindModule(
            CaptainBodyFamily family,
            SpriteFacing facing,
            CaptainCustomizationCategory category)
        {
            var matches = moduleTextures.Where(entry =>
                entry != null && entry.Family == family &&
                entry.Facing == facing && entry.Category == category).ToArray();
            if (matches.Length != 1 || matches[0].Texture == null)
            {
                throw new KeyNotFoundException(
                    $"Captain module is missing {family}/{facing}/{category}.");
            }
            return matches[0];
        }

        public Vector2 ResolveAnchorLocal(
            CaptainBodyFamily family,
            SpriteFacing facing,
            string motionId,
            int frameIndex,
            string anchorId)
        {
            var frame = frameAnchors.SingleOrDefault(entry =>
                entry != null && entry.Family == family &&
                entry.Facing == facing && entry.MotionId == motionId &&
                entry.FrameIndex == frameIndex);
            var point = frame?.Points.SingleOrDefault(entry =>
                entry != null && entry.Id == anchorId);
            if (point == null)
            {
                throw new KeyNotFoundException(
                    $"Captain anchor is missing {family}/{facing}/{motionId}/" +
                    $"{frameIndex}/{anchorId}.");
            }
            return new Vector2(
                (point.RuntimePixels.x - 64f) / 100f,
                (point.RuntimePixels.y - 18f) / 100f);
        }

        public CharacterSpriteSet Find(
            CaptainBodyFamily family,
            SpriteFacing facing,
            CaptainSpriteLayer layer)
        {
            var matches = entries.Where(entry =>
                entry != null && entry.Family == family &&
                entry.Facing == facing && entry.Layer == layer).ToArray();
            if (matches.Length != 1)
            {
                throw new KeyNotFoundException(
                    $"Captain sprites are missing {family}/{facing}/{layer}.");
            }
            return matches[0].SpriteSet;
        }

        public SpriteAnimationClipDefinition FindClip(
            CaptainBodyFamily family,
            SpriteFacing facing,
            CaptainSpriteLayer layer,
            string motionId)
        {
            if (Array.IndexOf(MotionIds, motionId) < 0)
            {
                throw new KeyNotFoundException($"Unsupported Captain motion {motionId}.");
            }
            return Find(family, facing, layer).FindClip(
                ClipId(family, facing, layer, motionId));
        }

        public void ValidateOrThrow()
        {
            if (entries == null || entries.Length != 30)
            {
                throw new InvalidOperationException(
                    "CaptainSpriteSet does not contain the complete matrix.");
            }
            foreach (CaptainBodyFamily family in Enum.GetValues(
                         typeof(CaptainBodyFamily)))
            {
                foreach (var facing in new[] { SpriteFacing.Right, SpriteFacing.Left })
                {
                    SpriteAnimationClipDefinition[] canonical = null;
                    foreach (CaptainSpriteLayer layer in Enum.GetValues(
                                 typeof(CaptainSpriteLayer)))
                    {
                        var layerSet = Find(family, facing, layer);
                        if (layerSet.Clips.Count != MotionIds.Length)
                        {
                            throw new InvalidOperationException(
                                $"Captain {family}/{facing}/{layer} is not clip complete.");
                        }
                        var clips = MotionIds.Select(motion =>
                            layerSet.FindClip(ClipId(family, facing, layer, motion)))
                            .ToArray();
                        foreach (var clip in clips)
                        {
                            clip.ValidateOrThrow();
                        }
                        if (canonical == null)
                        {
                            canonical = clips;
                        }
                        else
                        {
                            RequireSynchronization(canonical, clips, family, facing, layer);
                        }
                    }
                }
            }
            if (allowBaseOnlyForTests)
            {
                return;
            }
            if (customizationShader == null || paletteMasks == null ||
                paletteMasks.Length != 30 || moduleTextures == null ||
                moduleTextures.Length != 66 || frameAnchors == null ||
                frameAnchors.Length != 288)
            {
                throw new InvalidOperationException(
                    "Captain customization requires one shader, 30 palette " +
                    "pages, 66 module pages, and 288 anchored frames.");
            }
            var expectedAnchorIds = new HashSet<string>(
                RequiredAnchors,
                StringComparer.Ordinal);
            foreach (CaptainBodyFamily family in Enum.GetValues(
                         typeof(CaptainBodyFamily)))
            {
                foreach (var facing in new[]
                         {
                             SpriteFacing.Right,
                             SpriteFacing.Left,
                         })
                {
                    foreach (CaptainSpriteLayer layer in Enum.GetValues(
                                 typeof(CaptainSpriteLayer)))
                    {
                        FindPaletteMask(family, facing, layer);
                    }
                    foreach (CaptainCustomizationCategory category in Enum.GetValues(
                                 typeof(CaptainCustomizationCategory)))
                    {
                        var module = FindModule(family, facing, category);
                        if (module.OptionIds.Count < 3 ||
                            module.OptionIds.Count > 8 ||
                            module.OptionIds.Distinct(StringComparer.Ordinal).Count() !=
                            module.OptionIds.Count)
                        {
                            throw new InvalidOperationException(
                                $"Captain module {family}/{facing}/{category} " +
                                "has an invalid option catalog.");
                        }
                    }
                    foreach (var motionId in MotionIds)
                    {
                        var expectedFrames = FindClip(
                            family,
                            facing,
                            CaptainSpriteLayer.BodyBase,
                            motionId).Frames.Count;
                        for (var frameIndex = 0;
                             frameIndex < expectedFrames;
                             frameIndex++)
                        {
                            var frame = frameAnchors.SingleOrDefault(entry =>
                                entry != null && entry.Family == family &&
                                entry.Facing == facing &&
                                entry.MotionId == motionId &&
                                entry.FrameIndex == frameIndex);
                            if (frame == null || frame.Points.Count !=
                                RequiredAnchors.Length ||
                                !new HashSet<string>(
                                    frame.Points.Select(point => point.Id),
                                    StringComparer.Ordinal).SetEquals(
                                    expectedAnchorIds))
                            {
                                throw new InvalidOperationException(
                                    $"Captain anchor frame is incomplete at " +
                                    $"{family}/{facing}/{motionId}/{frameIndex}.");
                            }
                        }
                    }
                }
            }
        }

        public static string LayerId(CaptainSpriteLayer layer)
        {
            return layer switch
            {
                CaptainSpriteLayer.BodyBase => "body-base",
                CaptainSpriteLayer.HeadHair => "head-hair",
                CaptainSpriteLayer.SilhouetteCostume => "silhouette-costume",
                CaptainSpriteLayer.BackpackEquipment => "backpack-equipment",
                CaptainSpriteLayer.ForegroundHandTool => "foreground-hand-tool",
                _ => throw new ArgumentOutOfRangeException(nameof(layer)),
            };
        }

        private static string ClipId(
            CaptainBodyFamily family,
            SpriteFacing facing,
            CaptainSpriteLayer layer,
            string motionId)
        {
            return $"captain.{family.ToString().ToLowerInvariant()}." +
                   $"{LayerId(layer)}.{motionId}." +
                   facing.ToString().ToLowerInvariant();
        }

        private static void RequireSynchronization(
            IReadOnlyList<SpriteAnimationClipDefinition> canonical,
            IReadOnlyList<SpriteAnimationClipDefinition> candidate,
            CaptainBodyFamily family,
            SpriteFacing facing,
            CaptainSpriteLayer layer)
        {
            for (var index = 0; index < canonical.Count; index++)
            {
                var expected = canonical[index];
                var actual = candidate[index];
                if (actual.Frames.Count != expected.Frames.Count ||
                    actual.LoopMode != expected.LoopMode ||
                    actual.Facing != expected.Facing ||
                    !actual.FrameDurations.SequenceEqual(expected.FrameDurations) ||
                    !MetadataEqual(actual.FrameContacts, expected.FrameContacts) ||
                    !MetadataEqual(actual.FrameEvents, expected.FrameEvents))
                {
                    throw new InvalidOperationException(
                        $"Captain {family}/{facing}/{layer} timeline drifted at " +
                        MotionIds[index] + ".");
                }
            }
        }

        private static bool MetadataEqual(
            IReadOnlyList<SpriteFrameContact> left,
            IReadOnlyList<SpriteFrameContact> right)
        {
            return left.Select(value => $"{value.FrameIndex}:{value.Id}")
                .SequenceEqual(right.Select(value => $"{value.FrameIndex}:{value.Id}"));
        }

        private static bool MetadataEqual(
            IReadOnlyList<SpriteFrameEvent> left,
            IReadOnlyList<SpriteFrameEvent> right)
        {
            return left.Select(value => $"{value.FrameIndex}:{value.Kind}:{value.Id}")
                .SequenceEqual(right.Select(value =>
                    $"{value.FrameIndex}:{value.Kind}:{value.Id}"));
        }
    }
}
