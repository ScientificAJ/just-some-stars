using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JustSomeStars.Runtime.Cosmetics
{
    [CreateAssetMenu(
        fileName = "CosmeticCollection",
        menuName = "Just Some Stars/Cosmetics/Category Collection")]
    public sealed class CosmeticCategoryCollection : ScriptableObject
    {
        [SerializeField] private CosmeticCategory category;
        [SerializeField] private CosmeticCatalog catalog;
        [SerializeField] private string[] itemIds = Array.Empty<string>();

        public CosmeticCategory Category => category;
        public CosmeticCatalog Catalog => catalog;
        public IReadOnlyList<string> ItemIds => itemIds;

        public void ValidateOrThrow()
        {
            if (catalog == null || itemIds == null || itemIds.Length == 0 ||
                itemIds.Distinct(StringComparer.Ordinal).Count() != itemIds.Length)
            {
                throw new InvalidOperationException(
                    $"{category} cosmetic collection is incomplete.");
            }

            foreach (var itemId in itemIds)
            {
                var definition = catalog.Find(itemId);
                if (definition == null || definition.Category != category)
                {
                    throw new InvalidOperationException(
                        $"{category} collection references invalid item '{itemId}'.");
                }
            }
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(
            CosmeticCategory collectionCategory,
            CosmeticCatalog sourceCatalog,
            string[] collectionItemIds)
        {
            category = collectionCategory;
            catalog = sourceCatalog;
            itemIds = collectionItemIds ?? Array.Empty<string>();
        }
#endif
    }
}
