using System;
using System.Collections.Generic;
using JustSomeStars.Runtime.Saving;
using UnityEngine;

namespace JustSomeStars.Runtime.Cosmetics
{
    public sealed class CosmeticPresentationBinding
    {
        public CosmeticPresentationBinding(CosmeticDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Sprite = definition.PresentationSprite ?? throw new InvalidOperationException(
                $"Cosmetic '{definition.Id}' has no presentation sprite.");
        }

        public CosmeticDefinition Definition { get; }
        public Sprite Sprite { get; }
        public string AttachmentAssetPath => Definition.AttachmentAssetPath;
        public string PaletteMaskPath => Definition.PaletteMaskPath;
        public string LayerId => Definition.PresentationLayerId;
        public string EffectId => Definition.PresentationEffectId;
    }

    public interface ICosmeticPresentationTarget
    {
        CosmeticCategory Category { get; }
        void Apply(CosmeticPresentationBinding binding);
    }

    public sealed class CosmeticPresentationService
    {
        private readonly CosmeticCatalog m_Catalog;
        private readonly Dictionary<CosmeticCategory, ICosmeticPresentationTarget> m_Targets =
            new();

        public CosmeticPresentationService(CosmeticCatalog catalog)
        {
            m_Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            m_Catalog.ValidateOrThrow();
        }

        public void Register(ICosmeticPresentationTarget target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            m_Targets[target.Category] = target;
        }

        public void Apply(string itemId)
        {
            var definition = m_Catalog.Find(itemId) ?? throw new ArgumentException(
                $"Cosmetic '{itemId}' is not in the launch catalogue.",
                nameof(itemId));
            if (!m_Targets.TryGetValue(definition.Category, out var target))
            {
                throw new InvalidOperationException(
                    $"No presentation target is registered for {definition.Category}.");
            }

            target.Apply(new CosmeticPresentationBinding(definition));
        }

        public void ApplyLoadout(CosmeticLoadoutState loadout)
        {
            if (loadout == null)
            {
                throw new ArgumentNullException(nameof(loadout));
            }

            foreach (CosmeticCategory category in Enum.GetValues(typeof(CosmeticCategory)))
            {
                Apply(loadout.Selected(category));
            }
        }
    }

    public sealed class CosmeticPresentationTarget2D :
        MonoBehaviour,
        ICosmeticPresentationTarget
    {
        private static readonly int PrimaryColorId =
            Shader.PropertyToID("_JssCosmeticPrimary");
        private static readonly int AccentColorId =
            Shader.PropertyToID("_JssCosmeticAccent");
        private static readonly int EffectSeedId =
            Shader.PropertyToID("_JssCosmeticEffectSeed");

        [SerializeField] private CosmeticCategory category;
        [SerializeField] private SpriteRenderer presentationRenderer;

        public CosmeticCategory Category => category;

        public void Apply(CosmeticPresentationBinding binding)
        {
            if (binding == null)
            {
                throw new ArgumentNullException(nameof(binding));
            }
            if (binding.Definition.Category != category)
            {
                throw new InvalidOperationException(
                    $"A {binding.Definition.Category} cosmetic cannot bind to {category}.");
            }
            if (presentationRenderer == null)
            {
                throw new InvalidOperationException(
                    $"The {category} cosmetic presentation renderer is missing.");
            }

            presentationRenderer.sprite = binding.Sprite;
            presentationRenderer.color = Color.white;
            presentationRenderer.enabled = true;

            var properties = new MaterialPropertyBlock();
            presentationRenderer.GetPropertyBlock(properties);
            if (ColorUtility.TryParseHtmlString(
                    binding.Definition.PrimaryColor,
                    out var primary))
            {
                properties.SetColor(PrimaryColorId, primary);
            }
            if (ColorUtility.TryParseHtmlString(
                    binding.Definition.AccentColor,
                    out var accent))
            {
                properties.SetColor(AccentColorId, accent);
            }
            properties.SetFloat(
                EffectSeedId,
                StableEffectSeed(binding.EffectId));
            presentationRenderer.SetPropertyBlock(properties);
        }

        private static float StableEffectSeed(string effectId)
        {
            unchecked
            {
                var hash = 17;
                foreach (var value in effectId ?? string.Empty)
                {
                    hash = (hash * 31) + value;
                }
                return (uint)hash / (float)uint.MaxValue;
            }
        }
    }
}
