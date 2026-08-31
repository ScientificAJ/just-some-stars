using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accounts;
using JustSomeStars.Runtime.Core;

namespace JustSomeStars.Runtime.Commerce
{
    public enum SamsungIapOperationMode
    {
        Production = 0,
        Test = 1,
        TestFailure = 2,
    }

    public enum GalaxyNativeStatus
    {
        Succeeded = 0,
        Cancelled = 1,
        Pending = 2,
        Failed = 3,
        Unavailable = 4,
    }

    public sealed class GalaxyNativeProduct
    {
        public GalaxyNativeProduct(
            string itemId,
            string title,
            string description,
            string formattedPrice,
            string currencyCode)
        {
            ItemId = itemId ?? string.Empty;
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            FormattedPrice = formattedPrice ?? string.Empty;
            CurrencyCode = currencyCode ?? string.Empty;
        }

        public string ItemId { get; }
        public string Title { get; }
        public string Description { get; }
        public string FormattedPrice { get; }
        public string CurrencyCode { get; }
    }

    public sealed class GalaxyNativePurchase
    {
        public GalaxyNativePurchase(
            GalaxyNativeStatus status,
            string purchaseId,
            string itemId,
            string obfuscatedAccountId,
            string obfuscatedProfileId)
        {
            Status = status;
            PurchaseId = purchaseId ?? string.Empty;
            ItemId = itemId ?? string.Empty;
            ObfuscatedAccountId = obfuscatedAccountId ?? string.Empty;
            ObfuscatedProfileId = obfuscatedProfileId ?? string.Empty;
        }

        public GalaxyNativeStatus Status { get; }
        public string PurchaseId { get; }
        public string ItemId { get; }
        public string ObfuscatedAccountId { get; }
        public string ObfuscatedProfileId { get; }
    }

    public sealed class GalaxyVerifiedAuthority
    {
        public GalaxyVerifiedAuthority(
            bool verified,
            string purchaseId,
            string itemId,
            string packageId,
            string mode,
            string obfuscatedAccountId,
            string obfuscatedProfileId,
            string signedAuthority,
            DateTime verifiedAtUtc)
        {
            Verified = verified;
            PurchaseId = purchaseId ?? string.Empty;
            ItemId = itemId ?? string.Empty;
            PackageId = packageId ?? string.Empty;
            Mode = mode ?? string.Empty;
            ObfuscatedAccountId = obfuscatedAccountId ?? string.Empty;
            ObfuscatedProfileId = obfuscatedProfileId ?? string.Empty;
            SignedAuthority = signedAuthority ?? string.Empty;
            VerifiedAtUtc = verifiedAtUtc;
        }

        public bool Verified { get; }
        public string PurchaseId { get; }
        public string ItemId { get; }
        public string PackageId { get; }
        public string Mode { get; }
        public string ObfuscatedAccountId { get; }
        public string ObfuscatedProfileId { get; }
        public string SignedAuthority { get; }
        public DateTime VerifiedAtUtc { get; }
    }

    public interface IGalaxyIapGateway : IGameService
    {
        bool IsSupported { get; }

        ValueTask<IReadOnlyList<GalaxyNativeProduct>> GetProductsDetailsAsync(
            IReadOnlyList<string> itemIds,
            CancellationToken cancellationToken);

        ValueTask<IReadOnlyList<GalaxyNativePurchase>> GetOwnedListAsync(
            CancellationToken cancellationToken);

        ValueTask<GalaxyNativePurchase> StartPaymentAsync(
            string itemId,
            string obfuscatedAccountId,
            string obfuscatedProfileId,
            CancellationToken cancellationToken);

        ValueTask<bool> AcknowledgePurchasesAsync(
            string purchaseId,
            CancellationToken cancellationToken);
    }

    public interface IGalaxyReceiptVerifier
    {
        bool IsConfigured { get; }
        string Revision { get; }

        ValueTask<GalaxyVerifiedAuthority> VerifyAsync(
            string purchaseId,
            CancellationToken cancellationToken);

        ValueTask<bool> ValidateCachedAuthorityAsync(
            GalaxyVerifiedAuthority authority,
            CancellationToken cancellationToken);
    }

    public interface IGalaxyEntitlementLedger
    {
        bool IsKnownPurchase(string identity, string purchaseId);

        bool IsPendingItem(string identity, string itemId);

        bool IsReplayedPurchase(string purchaseId, string identity);

        ValueTask PersistPendingAsync(
            string identity,
            string itemId,
            long identityGeneration,
            CancellationToken cancellationToken);

        ValueTask PersistVerifiedAsync(
            string identity,
            GalaxyVerifiedAuthority authority,
            ContentId entitlementId,
            CancellationToken cancellationToken);

        ValueTask<IReadOnlyList<GalaxyVerifiedAuthority>> LoadAuthoritiesAsync(
            string identity,
            CancellationToken cancellationToken);

        ValueTask<IReadOnlyList<string>> LoadPendingAcknowledgementsAsync(
            string identity,
            CancellationToken cancellationToken);

        ValueTask MarkAcknowledgedAsync(
            string identity,
            string purchaseId,
            CancellationToken cancellationToken);

        ValueTask ClearPendingAsync(
            string identity,
            CancellationToken cancellationToken);
    }

    internal static class GalaxyReceiptPolicy
    {
        internal const string ProductionMode = "PRODUCTION";
        internal const string GalaxyPackage =
            "com.scientificaj.justsomestars.galaxy";

        internal static bool Accept(
            bool verified,
            string purchaseId,
            string itemId,
            string packageId,
            string mode,
            string receiptAccountId,
            string receiptProfileId,
            long expectedGeneration,
            long callbackGeneration,
            bool replayed,
            string signedAuthority) =>
            verified &&
            !string.IsNullOrWhiteSpace(purchaseId) &&
            GalaxyProductMap.TryEntitlement(itemId, out _) &&
            string.Equals(packageId, GalaxyPackage, StringComparison.Ordinal) &&
            string.Equals(mode, ProductionMode, StringComparison.Ordinal) &&
            string.Equals(
                receiptAccountId,
                "account-hash",
                StringComparison.Ordinal) &&
            string.Equals(
                receiptProfileId,
                "profile-hash",
                StringComparison.Ordinal) &&
            expectedGeneration == callbackGeneration &&
            !replayed &&
            !string.IsNullOrWhiteSpace(signedAuthority);

        internal static bool Accept(
            GalaxyVerifiedAuthority authority,
            string expectedAccountId,
            string expectedProfileId,
            long expectedGeneration,
            long callbackGeneration,
            bool replayed) =>
            authority != null &&
            authority.Verified &&
            !string.IsNullOrWhiteSpace(authority.PurchaseId) &&
            GalaxyProductMap.TryEntitlement(authority.ItemId, out _) &&
            string.Equals(
                authority.PackageId,
                GalaxyPackage,
                StringComparison.Ordinal) &&
            string.Equals(
                authority.Mode,
                ProductionMode,
                StringComparison.Ordinal) &&
            string.Equals(
                authority.ObfuscatedAccountId,
                expectedAccountId,
                StringComparison.Ordinal) &&
            string.Equals(
                authority.ObfuscatedProfileId,
                expectedProfileId,
                StringComparison.Ordinal) &&
            expectedGeneration == callbackGeneration &&
            !replayed &&
            !string.IsNullOrWhiteSpace(authority.SignedAuthority) &&
            authority.VerifiedAtUtc.Kind == DateTimeKind.Utc;
    }

    public sealed class GalaxyStoreService : IStoreService
    {
        private readonly IAccountService m_Account;
        private readonly IGalaxyIapGateway m_Gateway;
        private readonly IGalaxyReceiptVerifier m_Verifier;
        private readonly IGalaxyEntitlementLedger m_Ledger;
        private readonly SemaphoreSlim m_Operation = new SemaphoreSlim(1, 1);
        private IReadOnlyList<StoreProduct> m_Products =
            Array.Empty<StoreProduct>();
        private EntitlementSnapshot m_Current = EntitlementSnapshot.Empty;
        private StoreAvailability m_Availability = StoreAvailability.Checking;
        private string m_Message = "Checking optional Galaxy purchases…";
        private long m_IdentityGeneration;
        private bool m_Initialized;

        public GalaxyStoreService(
            IAccountService account,
            IGalaxyIapGateway gateway,
            IGalaxyReceiptVerifier verifier,
            IGalaxyEntitlementLedger ledger)
        {
            m_Account = account ?? throw new ArgumentNullException(nameof(account));
            m_Gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            m_Verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
            m_Ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        }

        public StoreAvailability Availability => m_Availability;
        public IReadOnlyList<StoreProduct> Products => m_Products;
        public EntitlementSnapshot CurrentEntitlements => m_Current.Copy();
        public string StatusMessage => m_Message;
        public long IdentityGeneration => Interlocked.Read(ref m_IdentityGeneration);
        public event Action StateChanged;

        public async ValueTask<StartupResult> InitializeAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (m_Initialized)
            {
                return m_Availability == StoreAvailability.Available
                    ? StartupResult.Available()
                    : StartupResult.Unavailable(m_Message);
            }

            m_Account.StateChanged += HandleAccountChanged;
            var native = await m_Gateway.InitializeAsync(cancellationToken);
            m_Initialized = true;
            if (!native.IsAvailable ||
                !m_Gateway.IsSupported ||
                !m_Verifier.IsConfigured ||
                string.IsNullOrWhiteSpace(m_Verifier.Revision))
            {
                SetUnavailable(
                    "Optional Galaxy purchases are not activated. " +
                    "The complete story remains available.");
                return StartupResult.Unavailable(m_Message);
            }

            m_Availability = StoreAvailability.Available;
            m_Message = "Optional Galaxy purchases are ready.";
            await LoadCachedAuthoritiesAsync(cancellationToken);
            await RetryAcknowledgementsAsync(cancellationToken);
            await ReconcileOwnedAsync(adoptUnknown: false, cancellationToken);
            Notify();
            return StartupResult.Available();
        }

        public async ValueTask ShutdownAsync()
        {
            if (!m_Initialized)
            {
                return;
            }

            m_Account.StateChanged -= HandleAccountChanged;
            Interlocked.Increment(ref m_IdentityGeneration);
            await m_Gateway.ShutdownAsync();
            m_Initialized = false;
            m_Products = Array.Empty<StoreProduct>();
            m_Current = EntitlementSnapshot.Empty;
            m_Availability = StoreAvailability.Checking;
        }

        public async ValueTask<IReadOnlyList<StoreProduct>> GetProductsAsync(
            CancellationToken cancellationToken)
        {
            if (!CanOperate())
            {
                return Array.Empty<StoreProduct>();
            }

            var products = await m_Gateway.GetProductsDetailsAsync(
                GalaxyProductMap.ItemIds,
                cancellationToken);
            m_Products = GalaxyProductMap.Project(products);
            Notify();
            return m_Products;
        }

        public async ValueTask<PurchaseResult> PurchaseAsync(
            ContentId productId,
            CancellationToken cancellationToken)
        {
            if (!CanOperate())
            {
                return Result(PurchaseStatus.Unavailable);
            }

            var product = m_Products.FirstOrDefault(value => value.Id == productId);
            if (product == null)
            {
                return Result(PurchaseStatus.ProductUnavailable);
            }

            var generation = IdentityGeneration;
            var identity = ActiveIdentity();
            var accountId = Obfuscated("jss-galaxy-account-v1", identity);
            var profileId = Obfuscated("jss-galaxy-profile-v1", identity);
            await m_Operation.WaitAsync(cancellationToken);
            try
            {
                await m_Ledger.PersistPendingAsync(
                    identity,
                    product.StoreProductId,
                    generation,
                    cancellationToken);
                var native = await m_Gateway.StartPaymentAsync(
                    product.StoreProductId,
                    accountId,
                    profileId,
                    cancellationToken);
                if (native.Status == GalaxyNativeStatus.Cancelled)
                {
                    await m_Ledger.ClearPendingAsync(identity, cancellationToken);
                    return Result(PurchaseStatus.Cancelled);
                }

                if (native.Status != GalaxyNativeStatus.Succeeded)
                {
                    return Result(native.Status == GalaxyNativeStatus.Pending
                        ? PurchaseStatus.Pending
                        : PurchaseStatus.Failed);
                }

                return await CompleteVerifiedPurchaseAsync(
                    native,
                    identity,
                    accountId,
                    profileId,
                    generation,
                    cancellationToken);
            }
            finally
            {
                m_Operation.Release();
            }
        }

        public async ValueTask<EntitlementSnapshot> RestoreAsync(
            CancellationToken cancellationToken)
        {
            await ReconcileOwnedAsync(adoptUnknown: true, cancellationToken);
            return CurrentEntitlements;
        }

        public async ValueTask<EntitlementSnapshot> RefreshEntitlementsAsync(
            CancellationToken cancellationToken)
        {
            await ReconcileOwnedAsync(adoptUnknown: false, cancellationToken);
            return CurrentEntitlements;
        }

        public async ValueTask ResumeAsync(CancellationToken cancellationToken)
        {
            if (!CanOperate())
            {
                return;
            }

            await RetryAcknowledgementsAsync(cancellationToken);
            await ReconcileOwnedAsync(adoptUnknown: false, cancellationToken);
        }

        private async ValueTask<PurchaseResult> CompleteVerifiedPurchaseAsync(
            GalaxyNativePurchase native,
            string identity,
            string accountId,
            string profileId,
            long generation,
            CancellationToken cancellationToken)
        {
            var authority = await m_Verifier.VerifyAsync(
                native.PurchaseId,
                cancellationToken);
            if (authority == null ||
                !string.Equals(
                    native.PurchaseId,
                    authority.PurchaseId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    native.ItemId,
                    authority.ItemId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    native.ObfuscatedAccountId,
                    accountId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    native.ObfuscatedProfileId,
                    profileId,
                    StringComparison.Ordinal))
            {
                return Result(PurchaseStatus.Failed);
            }

            var replayed = m_Ledger.IsReplayedPurchase(
                native.PurchaseId,
                identity);
            if (!GalaxyReceiptPolicy.Accept(
                    authority,
                    accountId,
                    profileId,
                    generation,
                    IdentityGeneration,
                    replayed) ||
                !GalaxyProductMap.TryEntitlement(
                    authority.ItemId,
                    out var entitlementId))
            {
                return Result(PurchaseStatus.Failed);
            }

            await m_Ledger.PersistVerifiedAsync(
                identity,
                authority,
                entitlementId,
                cancellationToken);
            PublishAuthority(identity, authority, entitlementId);
            var acknowledged = await m_Gateway.AcknowledgePurchasesAsync(
                authority.PurchaseId,
                cancellationToken);
            if (acknowledged)
            {
                await m_Ledger.MarkAcknowledgedAsync(
                    identity,
                    authority.PurchaseId,
                    cancellationToken);
            }

            await m_Ledger.ClearPendingAsync(identity, cancellationToken);
            return Result(PurchaseStatus.Purchased);
        }

        private async ValueTask ReconcileOwnedAsync(
            bool adoptUnknown,
            CancellationToken cancellationToken)
        {
            if (!CanOperate())
            {
                return;
            }

            var identity = ActiveIdentity();
            var generation = IdentityGeneration;
            var accountId = Obfuscated("jss-galaxy-account-v1", identity);
            var profileId = Obfuscated("jss-galaxy-profile-v1", identity);
            var owned = await m_Gateway.GetOwnedListAsync(cancellationToken);
            foreach (var purchase in owned ?? Array.Empty<GalaxyNativePurchase>())
            {
                if (!adoptUnknown &&
                    !m_Ledger.IsKnownPurchase(identity, purchase.PurchaseId) &&
                    !m_Ledger.IsPendingItem(identity, purchase.ItemId))
                {
                    continue;
                }

                await CompleteVerifiedPurchaseAsync(
                    purchase,
                    identity,
                    accountId,
                    profileId,
                    generation,
                    cancellationToken);
            }
        }

        private async ValueTask LoadCachedAuthoritiesAsync(
            CancellationToken cancellationToken)
        {
            var identity = ActiveIdentity();
            var generation = IdentityGeneration;
            var accountId = Obfuscated("jss-galaxy-account-v1", identity);
            var profileId = Obfuscated("jss-galaxy-profile-v1", identity);
            var cached = await m_Ledger.LoadAuthoritiesAsync(
                identity,
                cancellationToken);
            foreach (var authority in cached ??
                Array.Empty<GalaxyVerifiedAuthority>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!await m_Verifier.ValidateCachedAuthorityAsync(
                        authority,
                        cancellationToken) ||
                    !GalaxyReceiptPolicy.Accept(
                        authority,
                        accountId,
                        profileId,
                        generation,
                        IdentityGeneration,
                        replayed: false) ||
                    !GalaxyProductMap.TryEntitlement(
                        authority.ItemId,
                        out var entitlementId))
                {
                    continue;
                }

                PublishAuthority(identity, authority, entitlementId);
            }
        }

        private async ValueTask RetryAcknowledgementsAsync(
            CancellationToken cancellationToken)
        {
            var identity = ActiveIdentity();
            var pending = await m_Ledger.LoadPendingAcknowledgementsAsync(
                identity,
                cancellationToken);
            foreach (var purchaseId in pending ?? Array.Empty<string>())
            {
                if (await m_Gateway.AcknowledgePurchasesAsync(
                        purchaseId,
                        cancellationToken))
                {
                    await m_Ledger.MarkAcknowledgedAsync(
                        identity,
                        purchaseId,
                        cancellationToken);
                }
            }
        }

        private void PublishAuthority(
            string identity,
            GalaxyVerifiedAuthority authority,
            ContentId entitlementId)
        {
            var active = m_Current.ActiveEntitlements
                .Concat(new[] { entitlementId })
                .Distinct()
                .ToArray();
            m_Current = new EntitlementSnapshot(
                Obfuscated("jss-galaxy-user-v1", identity),
                Obfuscated(
                    "jss-galaxy-authority-v1",
                    m_Verifier.Revision + ":PRODUCTION"),
                StoreEnvironment.Galaxy,
                GalaxyReceiptPolicy.GalaxyPackage,
                EntitlementVerification.Verified,
                EntitlementSource.SamsungVerifiedAuthority,
                authority.VerifiedAtUtc,
                active);
            Notify();
        }

        private void HandleAccountChanged(AccountState state)
        {
            Interlocked.Increment(ref m_IdentityGeneration);
            m_Current = EntitlementSnapshot.Empty;
            m_Products = Array.Empty<StoreProduct>();
            Notify();
        }

        private string ActiveIdentity()
        {
            var state = m_Account.Current;
            var value = !string.IsNullOrWhiteSpace(state?.FirebaseUserId)
                ? "firebase:" + state.FirebaseUserId
                : "guest:" + (state?.GuestId ?? string.Empty);
            if (value.EndsWith(":", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "A stable game profile is required for Galaxy purchases.");
            }

            return value;
        }

        private bool CanOperate() =>
            m_Initialized &&
            m_Availability == StoreAvailability.Available &&
            m_Verifier.IsConfigured;

        private PurchaseResult Result(PurchaseStatus status) =>
            new PurchaseResult(status, CurrentEntitlements, status.ToString());

        private void SetUnavailable(string message)
        {
            m_Availability = StoreAvailability.UnavailableConfiguration;
            m_Current = EntitlementSnapshot.Empty;
            m_Message = message;
            Notify();
        }

        private void Notify() => StateChanged?.Invoke();

        private static string Obfuscated(string domain, string value)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(
                domain + "\n" + (value ?? string.Empty)));
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }

    internal static class GalaxyProductMap
    {
        private static readonly Mapping[] Mappings =
        {
            new Mapping("store.explorer-edition", "jss.edition.explorer", "explorer_edition"),
            new Mapping("store.founders-constellation", "jss.pack.founders_constellation", "founders_constellation"),
            new Mapping("store.complete-launch-collection", "jss.pack.complete_launch_collection", "complete_launch_collection"),
            new Mapping("store.mirra-collection", "jss.collection.mirra", "mirra_collection"),
            new Mapping("store.koro-vesper-collection", "jss.collection.koro_vesper", "koro_vesper_collection"),
            new Mapping("store.aster-veil-collection", "jss.collection.aster_veil", "aster_veil_collection"),
        };

        internal static IReadOnlyList<string> ItemIds { get; } =
            new ReadOnlyCollection<string>(Mappings.Select(value => value.ItemId).ToArray());

        internal static bool IsAllowedItem(string itemId) =>
            Mappings.Any(value => string.Equals(
                value.ItemId,
                itemId,
                StringComparison.Ordinal));

        internal static bool TryEntitlement(
            string itemId,
            out ContentId entitlementId)
        {
            var mapping = Mappings.FirstOrDefault(value => string.Equals(
                value.ItemId,
                itemId,
                StringComparison.Ordinal));
            if (mapping == null)
            {
                entitlementId = default;
                return false;
            }

            entitlementId = new ContentId(mapping.EntitlementId);
            return true;
        }

        internal static IReadOnlyList<StoreProduct> Project(
            IReadOnlyList<GalaxyNativeProduct> products)
        {
            var result = new List<StoreProduct>();
            foreach (var product in products ?? Array.Empty<GalaxyNativeProduct>())
            {
                var mapping = Mappings.FirstOrDefault(value => string.Equals(
                    value.ItemId,
                    product.ItemId,
                    StringComparison.Ordinal));
                if (mapping == null ||
                    string.IsNullOrWhiteSpace(product.Title) ||
                    string.IsNullOrWhiteSpace(product.Description) ||
                    string.IsNullOrWhiteSpace(product.FormattedPrice))
                {
                    continue;
                }

                result.Add(new StoreProduct(
                    new ContentId(mapping.ContentId),
                    mapping.ItemId,
                    "samsung-galaxy",
                    GalaxyReceiptPolicy.GalaxyPackage,
                    new ContentId(mapping.EntitlementId),
                    product.Title.Trim(),
                    product.Description.Trim(),
                    product.FormattedPrice.Trim(),
                    product.CurrencyCode?.Trim()));
            }

            return new ReadOnlyCollection<StoreProduct>(result);
        }

        private sealed class Mapping
        {
            public Mapping(string contentId, string itemId, string entitlementId)
            {
                ContentId = contentId;
                ItemId = itemId;
                EntitlementId = entitlementId;
            }

            public string ContentId { get; }
            public string ItemId { get; }
            public string EntitlementId { get; }
        }
    }

}
