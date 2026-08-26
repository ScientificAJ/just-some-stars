using System;
using System.Collections.Generic;
using UnityEngine;

namespace JustSomeStars.Runtime.Cosmetics
{
    public enum CaptainBodyFamily
    {
        Compact = 0,
        Average = 1,
        TallBroad = 2,
    }

    public enum CaptainSpriteLayer
    {
        BodyBase = 0,
        HeadHair = 1,
        SilhouetteCostume = 2,
        BackpackEquipment = 3,
        ForegroundHandTool = 4,
    }

    public enum CaptainCustomizationCategory
    {
        FacePresets = 0,
        EyeShapes = 1,
        IrisColors = 2,
        HairShapes = 3,
        SuitComponents = 4,
        Patches = 5,
        Accessories = 6,
        Gloves = 7,
        Boots = 8,
        Helmets = 9,
        Backpacks = 10,
    }

    [Serializable]
    public sealed class CaptainSpriteLoadout
    {
        internal const int MaximumLayerCount = 5;

        [SerializeField] private CaptainBodyFamily family;
        [SerializeField] private int activeLayerCount = MaximumLayerCount;
        [SerializeField] private string facePreset = "face-1";
        [SerializeField] private string skinSwatch = "skin-1";
        [SerializeField] private string eyeShape = "eye-shape-1";
        [SerializeField] private string irisColor = "warm-brown";
        [SerializeField] private string hairShape = "hair-shape-1";
        [SerializeField] private string hairColor = "deep-brown";
        [SerializeField] private string suitComponent = "canvas-oversuit";
        [SerializeField] private string suitColorway = "amber-clay";
        [SerializeField] private string patch = "patch-1";
        [SerializeField] private string accessory = "wrist-device";
        [SerializeField] private string gloves = "wrapped-work";
        [SerializeField] private string boots = "laceup-work";
        [SerializeField] private string helmet = "explorer-lite";
        [SerializeField] private string backpack = "field-pack";
        [SerializeField] private string signalState = "dormant";

        private static readonly IReadOnlyDictionary<string, string[]> Catalog =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [nameof(facePreset)] = Sequence("face", 6),
                [nameof(skinSwatch)] = Sequence("skin", 8),
                [nameof(eyeShape)] = Sequence("eye-shape", 6),
                [nameof(irisColor)] = new[]
                {
                    "warm-brown", "amber", "hazel", "river-blue",
                    "deep-blue", "slate",
                },
                [nameof(hairShape)] = Sequence("hair-shape", 8),
                [nameof(hairColor)] = new[]
                {
                    "black", "deep-brown", "chestnut", "copper", "auburn",
                    "golden-blonde", "ash-blonde", "silver", "blue-black",
                },
                [nameof(suitComponent)] = new[]
                {
                    "base-shirt", "canvas-oversuit", "scarf-neck-layer",
                    "utility-belt",
                },
                [nameof(suitColorway)] = new[]
                {
                    "amber-clay", "deep-teal", "dusk-purple", "river-blue",
                    "moss-green", "sandstone",
                },
                [nameof(patch)] = Sequence("patch", 6),
                [nameof(accessory)] = new[]
                {
                    "goggles", "hair-clips", "headband", "wrist-device",
                    "utility-pouch",
                },
                [nameof(gloves)] = new[]
                {
                    "wrapped-work", "padded-utility", "tactile-grip",
                },
                [nameof(boots)] = new[]
                {
                    "laceup-work", "strap-utility", "pull-on",
                },
                [nameof(helmet)] = new[]
                {
                    "explorer-lite", "surveyor", "field-ready",
                },
                [nameof(backpack)] = new[]
                {
                    "daypack", "expedition-pack", "field-pack",
                },
                [nameof(signalState)] = new[]
                {
                    "dormant", "active-cyan", "resonance-violet",
                },
            };

        public CaptainBodyFamily Family => family;
        public int ActiveLayerCount => activeLayerCount;
        public string FacePreset => facePreset;
        public string SkinSwatch => skinSwatch;
        public string EyeShape => eyeShape;
        public string IrisColor => irisColor;
        public string HairShape => hairShape;
        public string HairColor => hairColor;
        public string SuitComponent => suitComponent;
        public string SuitColorway => suitColorway;
        public string Patch => patch;
        public string Accessory => accessory;
        public string Gloves => gloves;
        public string Boots => boots;
        public string Helmet => helmet;
        public string Backpack => backpack;
        public string SignalState => signalState;

        public static CaptainSpriteLoadout CreateLaunchLook(
            CaptainBodyFamily family,
            int activeLayerCount = MaximumLayerCount)
        {
            if (!Enum.IsDefined(typeof(CaptainBodyFamily), family))
            {
                throw new ArgumentOutOfRangeException(nameof(family));
            }
            if (activeLayerCount < 1 || activeLayerCount > MaximumLayerCount)
            {
                throw new InvalidOperationException(
                    "Captain sprite loadouts support one through five visual layers.");
            }

            var result = new CaptainSpriteLoadout
            {
                family = family,
                activeLayerCount = activeLayerCount,
            };
            result.ValidateOrThrow();
            return result;
        }

        public CaptainSpriteLoadout WithOption(
            CaptainCustomizationCategory category,
            string optionId)
        {
            var result = (CaptainSpriteLoadout)MemberwiseClone();
            switch (category)
            {
                case CaptainCustomizationCategory.FacePresets:
                    result.facePreset = optionId;
                    break;
                case CaptainCustomizationCategory.EyeShapes:
                    result.eyeShape = optionId;
                    break;
                case CaptainCustomizationCategory.IrisColors:
                    result.irisColor = optionId;
                    break;
                case CaptainCustomizationCategory.HairShapes:
                    result.hairShape = optionId;
                    break;
                case CaptainCustomizationCategory.SuitComponents:
                    result.suitComponent = optionId;
                    break;
                case CaptainCustomizationCategory.Patches:
                    result.patch = optionId;
                    break;
                case CaptainCustomizationCategory.Accessories:
                    result.accessory = optionId;
                    break;
                case CaptainCustomizationCategory.Gloves:
                    result.gloves = optionId;
                    break;
                case CaptainCustomizationCategory.Boots:
                    result.boots = optionId;
                    break;
                case CaptainCustomizationCategory.Helmets:
                    result.helmet = optionId;
                    break;
                case CaptainCustomizationCategory.Backpacks:
                    result.backpack = optionId;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(category));
            }
            result.ValidateOrThrow();
            return result;
        }

        public CaptainSpriteLoadout WithPaletteSelections(
            string selectedSkin,
            string selectedHair,
            string selectedSuit,
            string selectedSignal)
        {
            var result = (CaptainSpriteLoadout)MemberwiseClone();
            result.skinSwatch = selectedSkin;
            result.hairColor = selectedHair;
            result.suitColorway = selectedSuit;
            result.signalState = selectedSignal;
            result.ValidateOrThrow();
            return result;
        }

        public string SelectedOption(CaptainCustomizationCategory category)
        {
            return category switch
            {
                CaptainCustomizationCategory.FacePresets => facePreset,
                CaptainCustomizationCategory.EyeShapes => eyeShape,
                CaptainCustomizationCategory.IrisColors => irisColor,
                CaptainCustomizationCategory.HairShapes => hairShape,
                CaptainCustomizationCategory.SuitComponents => suitComponent,
                CaptainCustomizationCategory.Patches => patch,
                CaptainCustomizationCategory.Accessories => accessory,
                CaptainCustomizationCategory.Gloves => gloves,
                CaptainCustomizationCategory.Boots => boots,
                CaptainCustomizationCategory.Helmets => helmet,
                CaptainCustomizationCategory.Backpacks => backpack,
                _ => throw new ArgumentOutOfRangeException(nameof(category)),
            };
        }

        public void ValidateOrThrow()
        {
            if (!Enum.IsDefined(typeof(CaptainBodyFamily), family) ||
                activeLayerCount < 1 || activeLayerCount > MaximumLayerCount)
            {
                throw new InvalidOperationException(
                    "Captain sprite loadout has an invalid family or layer count.");
            }
            RequireCatalogValue(nameof(facePreset), facePreset);
            RequireCatalogValue(nameof(skinSwatch), skinSwatch);
            RequireCatalogValue(nameof(eyeShape), eyeShape);
            RequireCatalogValue(nameof(irisColor), irisColor);
            RequireCatalogValue(nameof(hairShape), hairShape);
            RequireCatalogValue(nameof(hairColor), hairColor);
            RequireCatalogValue(nameof(suitComponent), suitComponent);
            RequireCatalogValue(nameof(suitColorway), suitColorway);
            RequireCatalogValue(nameof(patch), patch);
            RequireCatalogValue(nameof(accessory), accessory);
            RequireCatalogValue(nameof(gloves), gloves);
            RequireCatalogValue(nameof(boots), boots);
            RequireCatalogValue(nameof(helmet), helmet);
            RequireCatalogValue(nameof(backpack), backpack);
            RequireCatalogValue(nameof(signalState), signalState);
        }

        private static void RequireCatalogValue(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                Array.IndexOf(Catalog[key], value) < 0)
            {
                throw new InvalidOperationException(
                    $"Captain loadout option {key}={value} is not approved.");
            }
        }

        private static string[] Sequence(string prefix, int count)
        {
            var result = new string[count];
            for (var index = 0; index < count; index++)
            {
                result[index] = $"{prefix}-{index + 1}";
            }
            return result;
        }
    }
}
