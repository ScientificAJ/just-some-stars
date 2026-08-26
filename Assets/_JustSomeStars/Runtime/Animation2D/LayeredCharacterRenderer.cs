using System;
using System.Collections.Generic;
using JustSomeStars.Runtime.Cosmetics;
using UnityEngine;

namespace JustSomeStars.Runtime.Animation2D
{
    [DisallowMultipleComponent]
    public sealed class LayeredCharacterRenderer : MonoBehaviour
    {
        [SerializeField] private CaptainSpriteSet spriteSet;
        [SerializeField] private CaptainBodyFamily family = CaptainBodyFamily.Average;
        [SerializeField] private SpriteFacing facing = SpriteFacing.Right;
        [SerializeField] private SpriteRenderer[] layerRenderers =
            Array.Empty<SpriteRenderer>();

        private CaptainSpriteLoadout loadout;
        private SpriteAtlasAnimator[] animators = Array.Empty<SpriteAtlasAnimator>();
        private Material[] ownedMaterials = Array.Empty<Material>();
        private Texture2D transparentModuleTexture;
        private string currentMotion = "idle";

        public event Action<SpriteFrameEvent> FrameEventEmitted;

        public CaptainSpriteSet SpriteSet => spriteSet;
        public IReadOnlyList<SpriteRenderer> LayerRenderers => layerRenderers;
        public CaptainBodyFamily CurrentFamily => family;
        public SpriteFacing CurrentFacing => facing;
        public int ActiveLayerCount => animators.Length;
        public int CurrentFrameIndex => animators.Length == 0
            ? 0
            : animators[0].CurrentFrameIndex;

        public void Configure(
            CaptainSpriteSet configuredSpriteSet,
            CaptainSpriteLoadout configuredLoadout,
            SpriteFacing configuredFacing,
            SpriteRenderer[] configuredRenderers)
        {
            if (configuredSpriteSet == null)
            {
                throw new ArgumentNullException(nameof(configuredSpriteSet));
            }
            if (configuredLoadout == null)
            {
                throw new ArgumentNullException(nameof(configuredLoadout));
            }
            configuredLoadout.ValidateOrThrow();
            configuredSpriteSet.ValidateOrThrow();
            if (configuredRenderers == null ||
                configuredRenderers.Length != configuredLoadout.ActiveLayerCount ||
                configuredRenderers.Length < 1 || configuredRenderers.Length > 5 ||
                Array.Exists(configuredRenderers, item => item == null))
            {
                throw new InvalidOperationException(
                    "Layered Captain rendering requires one through five renderers.");
            }

            ReleaseAnimators();
            spriteSet = configuredSpriteSet;
            loadout = configuredLoadout;
            family = configuredLoadout.Family;
            facing = configuredFacing;
            layerRenderers = (SpriteRenderer[])configuredRenderers.Clone();
            animators = new SpriteAtlasAnimator[layerRenderers.Length];
            ownedMaterials = new Material[layerRenderers.Length];
            if (spriteSet.CustomizationShader != null)
            {
                transparentModuleTexture = new Texture2D(
                    1,
                    1,
                    TextureFormat.RGBA32,
                    false)
                {
                    name = "CaptainTransparentModule",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                transparentModuleTexture.SetPixel(0, 0, Color.clear);
                transparentModuleTexture.Apply(false, true);
            }
            for (var index = 0; index < layerRenderers.Length; index++)
            {
                var renderer = layerRenderers[index];
                renderer.sortingOrder = 520 + index;
                if (spriteSet.CustomizationShader != null)
                {
                    var material = new Material(spriteSet.CustomizationShader)
                    {
                        name = $"CaptainLayer-{index}",
                        hideFlags = HideFlags.HideAndDontSave,
                    };
                    renderer.sharedMaterial = material;
                    ownedMaterials[index] = material;
                }
                var animator = renderer.GetComponent<SpriteAtlasAnimator>();
                if (animator == null)
                {
                    animator = renderer.gameObject.AddComponent<SpriteAtlasAnimator>();
                }
                animator.Configure(renderer);
                animator.enabled = false;
                animators[index] = animator;
            }
            animators[0].FrameEventEmitted += OnAuthoritativeFrameEvent;
            ApplyVisualSelection();
        }

        public void Play(string motionId)
        {
            RequireConfigured();
            currentMotion = motionId;
            var expectedFrames = -1;
            for (var index = 0; index < animators.Length; index++)
            {
                var layer = (CaptainSpriteLayer)index;
                var clip = spriteSet.FindClip(family, facing, layer, motionId);
                if (expectedFrames < 0)
                {
                    expectedFrames = clip.Frames.Count;
                }
                else if (clip.Frames.Count != expectedFrames)
                {
                    throw new InvalidOperationException(
                        "Captain visual layers cannot switch to a mixed timeline.");
                }
                animators[index].Play(clip);
            }
            RequireSynchronizedFrame();
        }

        public void ApplyLoadout(
            CaptainSpriteLoadout nextLoadout,
            SpriteFacing nextFacing,
            string motionId)
        {
            if (nextLoadout == null)
            {
                throw new ArgumentNullException(nameof(nextLoadout));
            }
            if (nextLoadout.ActiveLayerCount != layerRenderers.Length)
            {
                throw new InvalidOperationException(
                    "Atomic loadout changes must preserve the configured renderer count.");
            }
            nextLoadout.ValidateOrThrow();
            loadout = nextLoadout;
            family = nextLoadout.Family;
            facing = nextFacing;
            ApplyVisualSelection();
            Play(motionId);
        }

        public Vector2 ResolveAnchorLocal(string anchorId)
        {
            RequireConfigured();
            return spriteSet.ResolveAnchorLocal(
                family,
                facing,
                currentMotion,
                CurrentFrameIndex,
                anchorId);
        }

        public void Advance(float deltaSeconds)
        {
            RequireConfigured();
            foreach (var animator in animators)
            {
                animator.Advance(deltaSeconds);
            }
            RequireSynchronizedFrame();
        }

        private void Awake()
        {
            if (spriteSet != null && layerRenderers != null &&
                layerRenderers.Length > 0)
            {
                Configure(
                    spriteSet,
                    CaptainSpriteLoadout.CreateLaunchLook(
                        family,
                        layerRenderers.Length),
                    facing,
                    layerRenderers);
                Play(currentMotion);
            }
        }

        private void Update()
        {
            if (animators.Length > 0)
            {
                Advance(Time.deltaTime);
            }
        }

        private void OnDestroy()
        {
            ReleaseAnimators();
        }

        private void OnAuthoritativeFrameEvent(SpriteFrameEvent frameEvent)
        {
            FrameEventEmitted?.Invoke(frameEvent);
        }

        private void ReleaseAnimators()
        {
            if (animators.Length > 0 && animators[0] != null)
            {
                animators[0].FrameEventEmitted -= OnAuthoritativeFrameEvent;
            }
            animators = Array.Empty<SpriteAtlasAnimator>();
            foreach (var material in ownedMaterials)
            {
                DestroyOwned(material);
            }
            ownedMaterials = Array.Empty<Material>();
            DestroyOwned(transparentModuleTexture);
            transparentModuleTexture = null;
        }

        private void ApplyVisualSelection()
        {
            if (spriteSet.CustomizationShader == null)
            {
                return;
            }
            for (var layerIndex = 0;
                 layerIndex < layerRenderers.Length;
                 layerIndex++)
            {
                var layer = (CaptainSpriteLayer)layerIndex;
                var block = new MaterialPropertyBlock();
                layerRenderers[layerIndex].GetPropertyBlock(block);
                block.SetTexture(
                    "_PaletteMask",
                    spriteSet.FindPaletteMask(family, facing, layer));
                block.SetColor("_SkinColor", SkinColor(loadout.SkinSwatch));
                block.SetColor("_HairColor", HairColor(loadout.HairColor));
                block.SetColor("_SuitColor", SuitColor(loadout.SuitColorway));
                block.SetColor("_SignalColor", SignalColor(loadout.SignalState));
                foreach (CaptainCustomizationCategory category in Enum.GetValues(
                             typeof(CaptainCustomizationCategory)))
                {
                    var module = spriteSet.FindModule(family, facing, category);
                    var propertyName = "_" + category + "Module";
                    var uvPropertyName = "_" + category + "Uv";
                    if (module.TargetLayer == layer)
                    {
                        block.SetTexture(propertyName, module.Texture);
                        block.SetVector(
                            uvPropertyName,
                            module.UvScaleOffset(loadout.SelectedOption(category)));
                    }
                    else
                    {
                        block.SetTexture(propertyName, transparentModuleTexture);
                        block.SetVector(uvPropertyName, Vector4.zero);
                    }
                }
                layerRenderers[layerIndex].SetPropertyBlock(block);
            }
        }

        private static Color SkinColor(string id)
        {
            return IndexedColor(
                id,
                "skin-",
                new[]
                {
                    new Color(0.30f, 0.14f, 0.07f),
                    new Color(0.40f, 0.20f, 0.10f),
                    new Color(0.52f, 0.29f, 0.16f),
                    new Color(0.62f, 0.38f, 0.23f),
                    new Color(0.72f, 0.48f, 0.31f),
                    new Color(0.80f, 0.58f, 0.40f),
                    new Color(0.88f, 0.68f, 0.50f),
                    new Color(0.94f, 0.78f, 0.63f),
                });
        }

        private static Color HairColor(string id)
        {
            return NamedColor(id, new Dictionary<string, Color>(StringComparer.Ordinal)
            {
                ["black"] = new(0.035f, 0.03f, 0.04f),
                ["deep-brown"] = new(0.11f, 0.055f, 0.03f),
                ["chestnut"] = new(0.26f, 0.095f, 0.04f),
                ["copper"] = new(0.55f, 0.18f, 0.055f),
                ["auburn"] = new(0.38f, 0.07f, 0.035f),
                ["golden-blonde"] = new(0.78f, 0.49f, 0.16f),
                ["ash-blonde"] = new(0.60f, 0.51f, 0.39f),
                ["silver"] = new(0.66f, 0.69f, 0.74f),
                ["blue-black"] = new(0.035f, 0.07f, 0.13f),
            });
        }

        private static Color SuitColor(string id)
        {
            return NamedColor(id, new Dictionary<string, Color>(StringComparer.Ordinal)
            {
                ["amber-clay"] = new(0.62f, 0.25f, 0.08f),
                ["deep-teal"] = new(0.035f, 0.29f, 0.31f),
                ["dusk-purple"] = new(0.30f, 0.15f, 0.43f),
                ["river-blue"] = new(0.08f, 0.29f, 0.57f),
                ["moss-green"] = new(0.20f, 0.35f, 0.14f),
                ["sandstone"] = new(0.66f, 0.48f, 0.28f),
            });
        }

        private static Color SignalColor(string id)
        {
            return NamedColor(id, new Dictionary<string, Color>(StringComparer.Ordinal)
            {
                ["dormant"] = new(0.08f, 0.16f, 0.20f),
                ["active-cyan"] = new(0.16f, 0.86f, 1.0f),
                ["resonance-violet"] = new(0.67f, 0.28f, 1.0f),
            });
        }

        private static Color IndexedColor(
            string id,
            string prefix,
            IReadOnlyList<Color> colors)
        {
            if (!id.StartsWith(prefix, StringComparison.Ordinal) ||
                !int.TryParse(id.Substring(prefix.Length), out var value) ||
                value < 1 || value > colors.Count)
            {
                throw new InvalidOperationException("Unknown Captain color " + id + ".");
            }
            return colors[value - 1];
        }

        private static Color NamedColor(
            string id,
            IReadOnlyDictionary<string, Color> colors)
        {
            if (!colors.TryGetValue(id, out var result))
            {
                throw new InvalidOperationException("Unknown Captain color " + id + ".");
            }
            return result;
        }

        private static void DestroyOwned(UnityEngine.Object value)
        {
            if (value == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Destroy(value);
            }
            else
            {
                DestroyImmediate(value);
            }
        }

        private void RequireConfigured()
        {
            if (spriteSet == null || loadout == null || animators.Length == 0)
            {
                throw new InvalidOperationException(
                    "LayeredCharacterRenderer is not configured.");
            }
        }

        private void RequireSynchronizedFrame()
        {
            var frameIndex = animators[0].CurrentFrameIndex;
            for (var index = 1; index < animators.Length; index++)
            {
                if (animators[index].CurrentFrameIndex != frameIndex)
                {
                    throw new InvalidOperationException(
                        "Captain visual layers advanced to mixed frame indices.");
                }
            }
        }
    }
}
