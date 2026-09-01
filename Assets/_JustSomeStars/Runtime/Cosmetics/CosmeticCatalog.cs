using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace JustSomeStars.Runtime.Cosmetics
{
    public enum CosmeticCategory
    {
        Captain = 0,
        Ori = 1,
        Ship = 2,
        Lens = 3,
        Clubhouse = 4,
        Photo = 5,
        Crew = 6,
    }

    public enum CosmeticOwnershipSource
    {
        Free = 0,
        Earned = 1,
        Birthday = 2,
        Edition = 3,
        IndividualPurchase = 4,
    }

    public enum CosmeticRarity
    {
        Field = 0,
        Discovery = 1,
        Signal = 2,
        Constellation = 3,
    }

    [Serializable]
    public sealed class CosmeticDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private CosmeticCategory category;
        [SerializeField] private CosmeticOwnershipSource ownershipSource;
        [SerializeField] private CosmeticRarity rarity;
        [SerializeField] private string packId;
        [SerializeField] private string productId;
        [SerializeField] private string[] entitlementIds = Array.Empty<string>();
        [SerializeField] private CaptainBodyFamily[] bodyFits =
            Array.Empty<CaptainBodyFamily>();
        [SerializeField] private Sprite icon;
        [SerializeField] private Sprite presentationSprite;
        [SerializeField] private string presentationAssetPath;
        [SerializeField] private string attachmentAssetPath;
        [SerializeField] private string paletteMaskPath;
        [SerializeField] private string presentationLayerId;
        [SerializeField] private string presentationEffectId;
        [SerializeField] private string primaryColor;
        [SerializeField] private string accentColor;
        [SerializeField] private bool canBeEarned;
        [SerializeField] private bool silhouetteChanging;
        [SerializeField] private string[] compatibleClipIds = Array.Empty<string>();
        [SerializeField] private string[] compatibleFrameEvents = Array.Empty<string>();

        public string Id => id;
        public string DisplayName => displayName;
        public CosmeticCategory Category => category;
        public CosmeticOwnershipSource OwnershipSource => ownershipSource;
        public CosmeticRarity Rarity => rarity;
        public string PackId => packId;
        public string ProductId => productId;
        public IReadOnlyList<string> EntitlementIds => entitlementIds;
        public IReadOnlyList<CaptainBodyFamily> BodyFits => bodyFits;
        public Sprite Icon => icon;
        public Sprite PresentationSprite => presentationSprite;
        public string PresentationAssetPath => presentationAssetPath;
        public string AttachmentAssetPath => attachmentAssetPath;
        public string PaletteMaskPath => paletteMaskPath;
        public string PresentationLayerId => presentationLayerId;
        public string PresentationEffectId => presentationEffectId;
        public string PrimaryColor => primaryColor;
        public string AccentColor => accentColor;
        public bool CanBeEarned => canBeEarned;
        public bool SilhouetteChanging => silhouetteChanging;
        public IReadOnlyList<string> CompatibleClipIds => compatibleClipIds;
        public IReadOnlyList<string> CompatibleFrameEvents => compatibleFrameEvents;

#if UNITY_EDITOR
        public static CosmeticDefinition CreateForEditor(
            string itemId,
            string itemDisplayName,
            CosmeticCategory itemCategory,
            CosmeticOwnershipSource source,
            CosmeticRarity itemRarity,
            string itemPackId,
            string itemProductId,
            string[] itemEntitlementIds,
            CaptainBodyFamily[] itemBodyFits,
            Sprite itemIcon,
            Sprite itemPresentationSprite,
            string assetPath,
            string fittedAttachmentPath,
            string maskPath,
            string layerId,
            string effectId,
            string itemPrimaryColor,
            string itemAccentColor,
            bool itemCanBeEarned,
            bool changesSilhouette,
            string[] clipIds,
            string[] frameEvents)
        {
            return new CosmeticDefinition
            {
                id = itemId,
                displayName = itemDisplayName,
                category = itemCategory,
                ownershipSource = source,
                rarity = itemRarity,
                packId = itemPackId,
                productId = itemProductId,
                entitlementIds = itemEntitlementIds,
                bodyFits = itemBodyFits,
                icon = itemIcon,
                presentationSprite = itemPresentationSprite,
                presentationAssetPath = assetPath,
                attachmentAssetPath = fittedAttachmentPath,
                paletteMaskPath = maskPath,
                presentationLayerId = layerId,
                presentationEffectId = effectId,
                primaryColor = itemPrimaryColor,
                accentColor = itemAccentColor,
                canBeEarned = itemCanBeEarned,
                silhouetteChanging = changesSilhouette,
                compatibleClipIds = clipIds,
                compatibleFrameEvents = frameEvents,
            };
        }
#endif
    }

    [CreateAssetMenu(
        fileName = "CosmeticCatalog",
        menuName = "Just Some Stars/Cosmetics/Cosmetic Catalog")]
    public sealed class CosmeticCatalog : ScriptableObject
    {
        public const int CurrentSchemaVersion = 1;
        public const int MinimumLaunchItemCount = 100;

        private static readonly Regex CanonicalId = new Regex(
            "^[a-z0-9]+(?:[._-][a-z0-9]+)*$",
            RegexOptions.CultureInvariant);

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private CosmeticDefinition[] items =
            Array.Empty<CosmeticDefinition>();

        private Dictionary<string, CosmeticDefinition> m_ById;

        public int SchemaVersion => schemaVersion;

        public IReadOnlyList<CosmeticDefinition> Items => items;

        public CosmeticDefinition Find(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return null;
            }

            EnsureIndex();
            return m_ById.TryGetValue(itemId, out var definition)
                ? definition
                : null;
        }

        public IReadOnlyList<CosmeticDefinition> Category(
            CosmeticCategory category)
        {
            return new ReadOnlyCollection<CosmeticDefinition>(items
                .Where(item => item.Category == category)
                .ToArray());
        }

        public void ValidateOrThrow()
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    $"Cosmetic catalogue schema must be {CurrentSchemaVersion}.");
            }

            if (items == null || items.Length < MinimumLaunchItemCount)
            {
                throw new InvalidOperationException(
                    $"Launch catalogue requires at least {MinimumLaunchItemCount} entries.");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var categories = new HashSet<CosmeticCategory>();
            foreach (var item in items)
            {
                ValidateItem(item, ids);
                categories.Add(item.Category);
            }

            var expectedCategories = Enum.GetValues(typeof(CosmeticCategory))
                .Cast<CosmeticCategory>();
            if (!expectedCategories.All(categories.Contains))
            {
                throw new InvalidOperationException(
                    "Every launch cosmetic category requires at least one entry.");
            }

            m_ById = items.ToDictionary(
                item => item.Id,
                StringComparer.Ordinal);
        }

        private static void ValidateItem(
            CosmeticDefinition item,
            ISet<string> ids)
        {
            if (item == null ||
                !CanonicalId.IsMatch(item.Id ?? string.Empty) ||
                !ids.Add(item.Id))
            {
                throw new InvalidOperationException(
                    "Cosmetic IDs must be unique canonical identifiers.");
            }

            if (string.IsNullOrWhiteSpace(item.DisplayName) ||
                !Enum.IsDefined(typeof(CosmeticCategory), item.Category) ||
                !Enum.IsDefined(typeof(CosmeticOwnershipSource), item.OwnershipSource) ||
                !Enum.IsDefined(typeof(CosmeticRarity), item.Rarity) ||
                string.IsNullOrWhiteSpace(item.PackId) ||
                item.Icon == null ||
                item.PresentationSprite == null ||
                string.IsNullOrWhiteSpace(item.PresentationAssetPath) ||
                string.IsNullOrWhiteSpace(item.AttachmentAssetPath) ||
                string.IsNullOrWhiteSpace(item.PresentationLayerId) ||
                !CanonicalId.IsMatch(item.PresentationEffectId ?? string.Empty) ||
                item.CompatibleClipIds == null ||
                item.CompatibleClipIds.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Cosmetic '{item?.Id}' has incomplete presentation metadata.");
            }

            var entitlements = item.EntitlementIds ?? Array.Empty<string>();
            if (entitlements.Any(value => !CanonicalId.IsMatch(value ?? string.Empty)) ||
                entitlements.Distinct(StringComparer.Ordinal).Count() !=
                entitlements.Count)
            {
                throw new InvalidOperationException(
                    $"Cosmetic '{item.Id}' has invalid entitlement metadata.");
            }

            if (item.OwnershipSource == CosmeticOwnershipSource.IndividualPurchase &&
                (!CanonicalId.IsMatch(item.ProductId ?? string.Empty) ||
                 entitlements.Count == 0))
            {
                throw new InvalidOperationException(
                    $"Purchased cosmetic '{item.Id}' requires product and entitlement IDs.");
            }

            if (item.Category == CosmeticCategory.Captain && item.SilhouetteChanging)
            {
                var expectedFits = Enum.GetValues(typeof(CaptainBodyFamily))
                    .Cast<CaptainBodyFamily>();
                if (!expectedFits.All(item.BodyFits.Contains) ||
                    item.BodyFits.Distinct().Count() != 3)
                {
                    throw new InvalidOperationException(
                        $"Silhouette cosmetic '{item.Id}' must fit all three Captain families.");
                }
            }

            if (item.Category != CosmeticCategory.Captain && item.BodyFits.Count != 0)
            {
                throw new InvalidOperationException(
                    $"Non-Captain cosmetic '{item.Id}' cannot claim Captain body fits.");
            }
        }

        private void EnsureIndex()
        {
            if (m_ById == null || m_ById.Count != items.Length)
            {
                ValidateOrThrow();
            }
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(CosmeticDefinition[] definitions)
        {
            schemaVersion = CurrentSchemaVersion;
            items = definitions ?? Array.Empty<CosmeticDefinition>();
            m_ById = null;
        }
#endif
    }

}
