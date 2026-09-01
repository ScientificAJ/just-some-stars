using System;
using System.Collections.Generic;
using JustSomeStars.Runtime.Commerce;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Saving;

namespace JustSomeStars.Runtime.Cosmetics
{
    public readonly struct CosmeticOwnership
    {
        public CosmeticOwnership(bool owned, CosmeticOwnershipSource source)
        {
            Owned = owned;
            Source = source;
        }

        public bool Owned { get; }
        public CosmeticOwnershipSource Source { get; }
    }

    public sealed class OwnershipResolver
    {
        private readonly CosmeticCatalog m_Catalog;
        private readonly CosmeticPresentationService m_Presentation;

        public OwnershipResolver(
            CosmeticCatalog catalog,
            CosmeticPresentationService presentation = null)
        {
            m_Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            m_Catalog.ValidateOrThrow();
            m_Presentation = presentation;
        }

        public CosmeticOwnership Resolve(
            string itemId,
            GameSave save,
            EntitlementSnapshot entitlements)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            var item = m_Catalog.Find(itemId) ?? throw new ArgumentException(
                $"Cosmetic '{itemId}' is not in the launch catalogue.",
                nameof(itemId));
            var earned = new HashSet<string>(
                save.EarnedCosmeticIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);

            if (item.OwnershipSource == CosmeticOwnershipSource.Free)
            {
                return new CosmeticOwnership(true, CosmeticOwnershipSource.Free);
            }

            if (item.OwnershipSource == CosmeticOwnershipSource.Birthday &&
                earned.Contains(item.Id))
            {
                return new CosmeticOwnership(true, CosmeticOwnershipSource.Birthday);
            }

            if (item.CanBeEarned && earned.Contains(item.Id))
            {
                return new CosmeticOwnership(true, CosmeticOwnershipSource.Earned);
            }

            if (item.OwnershipSource == CosmeticOwnershipSource.Edition &&
                HasVerifiedEntitlement(item, entitlements))
            {
                return new CosmeticOwnership(true, CosmeticOwnershipSource.Edition);
            }

            if (item.OwnershipSource == CosmeticOwnershipSource.IndividualPurchase &&
                HasVerifiedEntitlement(item, entitlements))
            {
                return new CosmeticOwnership(
                    true,
                    CosmeticOwnershipSource.IndividualPurchase);
            }

            return new CosmeticOwnership(false, item.OwnershipSource);
        }

        public void Equip(
            CosmeticCategory category,
            string itemId,
            GameSave save,
            EntitlementSnapshot entitlements,
            long equippedUtcTicks)
        {
            var item = m_Catalog.Find(itemId) ?? throw new ArgumentException(
                $"Cosmetic '{itemId}' is not in the launch catalogue.",
                nameof(itemId));
            if (item.Category != category)
            {
                throw new InvalidOperationException(
                    $"Cosmetic '{itemId}' cannot be equipped in {category}.");
            }
            if (!Resolve(itemId, save, entitlements).Owned)
            {
                throw new InvalidOperationException(
                    $"Cosmetic '{itemId}' is not owned by the active profile.");
            }

            save.CosmeticLoadout.Set(category, itemId, equippedUtcTicks);
            m_Presentation?.Apply(itemId);
        }

        private static bool HasVerifiedEntitlement(
            CosmeticDefinition item,
            EntitlementSnapshot entitlements)
        {
            if (entitlements == null || !entitlements.IsVerified)
            {
                return false;
            }

            foreach (var entitlementId in item.EntitlementIds)
            {
                if (entitlements.Owns(new ContentId(entitlementId)))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
