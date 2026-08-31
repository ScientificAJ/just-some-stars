using System;
using JustSomeStars.Runtime.Core;

namespace JustSomeStars.Runtime.Commerce
{
    public sealed class StoreProduct
    {
        public StoreProduct(
            ContentId id,
            string storeProductId,
            string offeringId,
            string packageId,
            ContentId entitlementId,
            string title,
            string description,
            string formattedPrice,
            string currencyCode)
        {
            RequireCanonical(id, nameof(id));
            RequireCanonical(entitlementId, nameof(entitlementId));
            Id = id;
            StoreProductId = RequireText(storeProductId, nameof(storeProductId));
            OfferingId = RequireText(offeringId, nameof(offeringId));
            PackageId = RequireText(packageId, nameof(packageId));
            EntitlementId = entitlementId;
            Title = RequireText(title, nameof(title));
            Description = RequireText(description, nameof(description));
            FormattedPrice = RequireText(formattedPrice, nameof(formattedPrice));
            CurrencyCode = currencyCode?.Trim() ?? string.Empty;
        }

        public ContentId Id { get; }

        public string StoreProductId { get; }

        public string OfferingId { get; }

        public string PackageId { get; }

        public ContentId EntitlementId { get; }

        public string Title { get; }

        public string Description { get; }

        public string FormattedPrice { get; }

        public string CurrencyCode { get; }

        public bool IsOneTimeNonConsumable => true;

        private static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Store metadata must be non-empty and already trimmed.",
                    parameterName);
            }

            return value;
        }

        private static void RequireCanonical(ContentId value, string parameterName)
        {
            if (!value.IsValid)
            {
                throw new ArgumentException(
                    "A canonical content identifier is required.",
                    parameterName);
            }
        }
    }
}
