using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JustSomeStars.Runtime.Core;

namespace JustSomeStars.Runtime.Commerce
{
    public enum StoreEnvironment
    {
        Unavailable = 0,
        RevenueCatTestStore = 1,
        GooglePlay = 2,
        Galaxy = 3,
    }

    public enum EntitlementVerification
    {
        NotRequested = 0,
        Verified = 1,
        VerifiedOnDevice = 2,
        Failed = 3,
    }

    public enum EntitlementSource
    {
        None = 0,
        CustomerInfo = 1,
        OfflineVerifiedCache = 2,
    }

    public sealed class EntitlementSnapshot
    {
        public static readonly EntitlementSnapshot Empty = new EntitlementSnapshot(
            string.Empty,
            string.Empty,
            StoreEnvironment.Unavailable,
            string.Empty,
            EntitlementVerification.NotRequested,
            EntitlementSource.None,
            DateTime.UnixEpoch,
            Array.Empty<ContentId>());

        private readonly ReadOnlyCollection<ContentId> m_ActiveEntitlements;
        private readonly HashSet<ContentId> m_ActiveSet;

        public EntitlementSnapshot(
            string appUserId,
            string appFingerprint,
            StoreEnvironment environment,
            string packageId,
            EntitlementVerification verification,
            EntitlementSource source,
            DateTime verifiedAtUtc,
            IEnumerable<ContentId> activeEntitlements)
        {
            if (verifiedAtUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "Entitlement timestamps must be UTC.",
                    nameof(verifiedAtUtc));
            }

            AppUserId = appUserId ?? string.Empty;
            AppFingerprint = appFingerprint ?? string.Empty;
            Environment = environment;
            PackageId = packageId ?? string.Empty;
            Verification = verification;
            Source = source;
            VerifiedAtUtc = verifiedAtUtc;
            var values = (activeEntitlements ?? Array.Empty<ContentId>())
                .ToArray();
            if (values.Any(value => !value.IsValid) ||
                values.Distinct().Count() != values.Length)
            {
                throw new ArgumentException(
                    "Active entitlements must contain unique canonical IDs.",
                    nameof(activeEntitlements));
            }

            Array.Sort(values, (left, right) => StringComparer.Ordinal.Compare(
                left.Value,
                right.Value));
            m_ActiveEntitlements = new ReadOnlyCollection<ContentId>(values);
            m_ActiveSet = new HashSet<ContentId>(values);
        }

        public string AppUserId { get; }

        public string AppFingerprint { get; }

        public StoreEnvironment Environment { get; }

        public string PackageId { get; }

        public EntitlementVerification Verification { get; }

        public EntitlementSource Source { get; }

        public DateTime VerifiedAtUtc { get; }

        public IReadOnlyCollection<ContentId> ActiveEntitlements =>
            m_ActiveEntitlements;

        public bool IsVerified =>
            Verification == EntitlementVerification.Verified ||
            Verification == EntitlementVerification.VerifiedOnDevice;

        public bool Owns(ContentId entitlementId) =>
            entitlementId.IsValid && m_ActiveSet.Contains(entitlementId);

        public EntitlementSnapshot Copy() => new EntitlementSnapshot(
            AppUserId,
            AppFingerprint,
            Environment,
            PackageId,
            Verification,
            Source,
            VerifiedAtUtc,
            m_ActiveEntitlements);

        internal EntitlementSnapshot AsOfflineCache() => new EntitlementSnapshot(
            AppUserId,
            AppFingerprint,
            Environment,
            PackageId,
            Verification,
            EntitlementSource.OfflineVerifiedCache,
            VerifiedAtUtc,
            m_ActiveEntitlements);
    }
}
