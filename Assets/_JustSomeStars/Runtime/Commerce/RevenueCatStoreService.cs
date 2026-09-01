using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accounts;
using JustSomeStars.Runtime.Core;

namespace JustSomeStars.Runtime.Commerce
{
    public static class StoreProviderRegistry
    {
        public static Func<IAccountService, IStoreService> Factory { get; set; }

        public static IStoreService Create(IAccountService account) =>
            Factory?.Invoke(account) ?? new UnavailableStoreService();

        internal static void ResetForTests() => Factory = null;
    }

    public enum RevenueCatGatewayResultStatus
    {
        Succeeded = 0,
        Cancelled = 1,
        Pending = 2,
        Failed = 3,
        Unavailable = 4,
    }

    public sealed class RevenueCatEntitlement
    {
        public RevenueCatEntitlement(
            string identifier,
            bool isActive,
            EntitlementVerification verification)
        {
            Identifier = identifier ?? string.Empty;
            IsActive = isActive;
            Verification = verification;
        }

        public string Identifier { get; }

        public bool IsActive { get; }

        public EntitlementVerification Verification { get; }
    }

    public sealed class RevenueCatCustomerInfo
    {
        private readonly ReadOnlyCollection<RevenueCatEntitlement> m_Entitlements;

        public RevenueCatCustomerInfo(
            string appUserId,
            EntitlementVerification verification,
            DateTime requestDateUtc,
            IEnumerable<RevenueCatEntitlement> entitlements)
        {
            if (requestDateUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "CustomerInfo request date must be UTC.",
                    nameof(requestDateUtc));
            }

            AppUserId = appUserId ?? string.Empty;
            Verification = verification;
            RequestDateUtc = requestDateUtc;
            m_Entitlements = new ReadOnlyCollection<RevenueCatEntitlement>(
                (entitlements ?? Array.Empty<RevenueCatEntitlement>())
                .Where(value => value != null)
                .ToArray());
        }

        public string AppUserId { get; }

        public EntitlementVerification Verification { get; }

        public DateTime RequestDateUtc { get; }

        public IReadOnlyList<RevenueCatEntitlement> Entitlements =>
            m_Entitlements;
    }

    public sealed class RevenueCatGatewayProduct
    {
        public RevenueCatGatewayProduct(
            string storeProductId,
            string offeringId,
            string packageId,
            string title,
            string description,
            string formattedPrice,
            string currencyCode)
        {
            StoreProductId = storeProductId ?? string.Empty;
            OfferingId = offeringId ?? string.Empty;
            PackageId = packageId ?? string.Empty;
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            FormattedPrice = formattedPrice ?? string.Empty;
            CurrencyCode = currencyCode ?? string.Empty;
        }

        public string StoreProductId { get; }

        public string OfferingId { get; }

        public string PackageId { get; }

        public string Title { get; }

        public string Description { get; }

        public string FormattedPrice { get; }

        public string CurrencyCode { get; }
    }

    public sealed class RevenueCatGatewayResult
    {
        public RevenueCatGatewayResult(
            RevenueCatGatewayResultStatus status,
            RevenueCatCustomerInfo customerInfo,
            string message)
        {
            Status = status;
            CustomerInfo = customerInfo;
            Message = message ?? string.Empty;
        }

        public RevenueCatGatewayResultStatus Status { get; }

        public RevenueCatCustomerInfo CustomerInfo { get; }

        public string Message { get; }
    }

    public interface IRevenueCatGateway : IGameService
    {
        bool IsConfigured { get; }

        string AppFingerprint { get; }

        StoreEnvironment Environment { get; }

        string AndroidPackageId { get; }

        string CurrentAppUserId { get; }

        event Action<RevenueCatCustomerInfo> CustomerInfoUpdated;

        ValueTask<IReadOnlyList<RevenueCatGatewayProduct>> GetProductsAsync(
            CancellationToken cancellationToken);

        ValueTask<RevenueCatGatewayResult> PurchaseAsync(
            string storeProductId,
            CancellationToken cancellationToken);

        ValueTask<RevenueCatGatewayResult> RestoreAsync(
            CancellationToken cancellationToken);

        ValueTask<RevenueCatGatewayResult> RefreshAsync(
            CancellationToken cancellationToken);

        ValueTask<RevenueCatGatewayResult> LogInAsync(
            string firebaseUserId,
            CancellationToken cancellationToken);

        ValueTask<RevenueCatGatewayResult> LogOutAsync(
            CancellationToken cancellationToken);
    }

    public sealed class RevenueCatStoreService : IStoreService
    {
        private static readonly Regex SafeFirebaseUserId = new Regex(
            "^[A-Za-z0-9._:-]{1,100}$",
            RegexOptions.CultureInvariant);

        private readonly object m_StateGate = new object();
        private readonly IAccountService m_Account;
        private readonly IRevenueCatGateway m_Gateway;
        private readonly OfflineEntitlementCache m_Cache;
        private readonly SemaphoreSlim m_Operation = new SemaphoreSlim(1, 1);

        private IReadOnlyList<StoreProduct> m_Products =
            Array.Empty<StoreProduct>();
        private EntitlementSnapshot m_Current = EntitlementSnapshot.Empty;
        private StoreAvailability m_Availability = StoreAvailability.Checking;
        private string m_StatusMessage = "Checking optional purchases…";
        private string m_FirebaseUserId = string.Empty;
        private CancellationTokenSource m_Lifetime;
        private Task m_IdentityTransition = Task.CompletedTask;
        private long m_Generation;
        private int m_PurchaseActive;
        private int m_ResumeRefreshActive;
        private bool m_Initialized;

        public RevenueCatStoreService(
            IAccountService account,
            IRevenueCatGateway gateway,
            OfflineEntitlementCache cache)
        {
            m_Account = account ?? throw new ArgumentNullException(nameof(account));
            m_Gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            m_Cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public StoreAvailability Availability
        {
            get
            {
                lock (m_StateGate)
                {
                    return m_Availability;
                }
            }
        }

        public IReadOnlyList<StoreProduct> Products
        {
            get
            {
                lock (m_StateGate)
                {
                    return m_Products;
                }
            }
        }

        public EntitlementSnapshot CurrentEntitlements
        {
            get
            {
                lock (m_StateGate)
                {
                    return m_Current.Copy();
                }
            }
        }

        public string StatusMessage
        {
            get
            {
                lock (m_StateGate)
                {
                    return m_StatusMessage;
                }
            }
        }

        public event Action StateChanged;

        public async ValueTask<StartupResult> InitializeAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (m_Initialized)
            {
                return Availability == StoreAvailability.Available
                    ? StartupResult.Available()
                    : StartupResult.Unavailable(StatusMessage);
            }

            m_Lifetime = new CancellationTokenSource();
            m_Account.StateChanged += HandleAccountStateChanged;
            m_Gateway.CustomerInfoUpdated += HandleCustomerInfoUpdated;
            var startup = await m_Gateway.InitializeAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            m_Initialized = true;
            if (!m_Gateway.IsConfigured || !startup.IsAvailable)
            {
                SetUnavailable(
                    StoreAvailability.UnavailableConfiguration,
                    "Optional purchases are unavailable in this build. " +
                    "The complete story remains free.");
                return StartupResult.Unavailable(StatusMessage);
            }

            if (!IsCompleteGatewayIdentity())
            {
                SetUnavailable(
                    StoreAvailability.UnavailableDependency,
                    "The optional store could not establish a safe local identity.");
                return StartupResult.Unavailable(StatusMessage);
            }

            var targetUid = FirebaseUid(m_Account.Current);
            await ApplyIdentityTransitionAsync(
                targetUid,
                Interlocked.Increment(ref m_Generation),
                cancellationToken);
            return Availability == StoreAvailability.Available
                ? StartupResult.Available()
                : StartupResult.Unavailable(StatusMessage);
        }

        public async ValueTask ShutdownAsync()
        {
            if (!m_Initialized)
            {
                return;
            }

            m_Account.StateChanged -= HandleAccountStateChanged;
            m_Gateway.CustomerInfoUpdated -= HandleCustomerInfoUpdated;
            Interlocked.Increment(ref m_Generation);
            m_Lifetime?.Cancel();
            try
            {
                await m_IdentityTransition;
            }
            catch (OperationCanceledException)
            {
            }

            await m_Gateway.ShutdownAsync();
            m_Lifetime?.Dispose();
            m_Lifetime = null;
            m_Initialized = false;
            lock (m_StateGate)
            {
                m_Products = Array.Empty<StoreProduct>();
                m_Current = EntitlementSnapshot.Empty;
                m_Availability = StoreAvailability.Checking;
                m_StatusMessage = "Optional store stopped.";
                m_FirebaseUserId = string.Empty;
            }
        }

        public async ValueTask<IReadOnlyList<StoreProduct>> GetProductsAsync(
            CancellationToken cancellationToken)
        {
            if (!CanOperate())
            {
                return Array.Empty<StoreProduct>();
            }

            var generation = Interlocked.Read(ref m_Generation);
            await m_Operation.WaitAsync(cancellationToken);
            try
            {
                if (!IsCurrent(generation))
                {
                    return Array.Empty<StoreProduct>();
                }

                var projections = await m_Gateway.GetProductsAsync(cancellationToken);
                var products = CommerceProductMap.Project(projections);
                if (!IsCurrent(generation))
                {
                    return Array.Empty<StoreProduct>();
                }

                lock (m_StateGate)
                {
                    m_Products = products;
                    m_StatusMessage = products.Count == 0
                        ? "Optional cosmetics are not available from the store yet."
                        : "Optional cosmetics are ready. Prices come from the store.";
                }

                NotifyChanged();
                return products;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                SetTransientFailure(
                    "The store could not load products. The story is unaffected.");
                return Array.Empty<StoreProduct>();
            }
            finally
            {
                m_Operation.Release();
            }
        }

        public async ValueTask<PurchaseResult> PurchaseAsync(
            ContentId productId,
            CancellationToken cancellationToken)
        {
            if (!productId.IsValid || !CanOperate())
            {
                return Result(PurchaseStatus.Unavailable, StatusMessage);
            }

            if (Interlocked.CompareExchange(ref m_PurchaseActive, 1, 0) != 0)
            {
                return Result(
                    PurchaseStatus.Pending,
                    "Another purchase is already waiting for the store.");
            }

            var nativePurchaseStarted = false;
            var refreshWasAlreadyRequired = false;
            try
            {
                var product = Products.FirstOrDefault(value => value.Id == productId);
                if (product == null)
                {
                    return Result(
                        PurchaseStatus.ProductUnavailable,
                        "That optional item is not available from the store.");
                }

                if (CurrentEntitlements.Owns(product.EntitlementId))
                {
                    return Result(
                        PurchaseStatus.AlreadyOwned,
                        "This optional item is already owned.");
                }

                var generation = Interlocked.Read(ref m_Generation);
                await m_Operation.WaitAsync(cancellationToken);
                try
                {
                    if (!IsCurrent(generation))
                    {
                        return Result(
                            PurchaseStatus.Unavailable,
                            "The game profile changed before the purchase began.");
                    }

                    SetBusy("Waiting for the store…");
                    cancellationToken.ThrowIfCancellationRequested();
                    refreshWasAlreadyRequired = RefreshRequired();
                    MarkRefreshRequired();
                    nativePurchaseStarted = true;
                    var response = await m_Gateway.PurchaseAsync(
                        product.StoreProductId,
                        cancellationToken);
                    if (!IsCurrent(generation))
                    {
                        return Result(
                            PurchaseStatus.Unavailable,
                            "The game profile changed while the store was open.");
                    }

                    switch (response.Status)
                    {
                        case RevenueCatGatewayResultStatus.Cancelled:
                            if (!refreshWasAlreadyRequired)
                            {
                                m_Cache.ClearRefreshRequired();
                            }

                            SetAvailable("Purchase cancelled in the store.");
                            return Result(PurchaseStatus.Cancelled, StatusMessage);
                        case RevenueCatGatewayResultStatus.Pending:
                            MarkRefreshRequired();
                            SetAvailable(
                                "The store is still processing this purchase. " +
                                "Ownership will refresh automatically.");
                            return Result(PurchaseStatus.Pending, StatusMessage);
                        case RevenueCatGatewayResultStatus.Unavailable:
                            SetTransientFailure(
                                "Purchases need a store connection. The story is unaffected.");
                            return Result(PurchaseStatus.Unavailable, StatusMessage);
                        case RevenueCatGatewayResultStatus.Failed:
                            MarkRefreshRequired();
                            SetTransientFailure(
                                "The store did not confirm this purchase. " +
                                "No item was granted; ownership will be checked again.");
                            return Result(PurchaseStatus.Failed, StatusMessage);
                        case RevenueCatGatewayResultStatus.Succeeded:
                            break;
                        default:
                            throw new InvalidOperationException(
                                "The store returned an unknown purchase state.");
                    }

                    if (!TryPublishVerified(response.CustomerInfo, generation) ||
                        !CurrentEntitlements.Owns(product.EntitlementId))
                    {
                        MarkRefreshRequired();
                        SetTransientFailure(
                            "The store returned without a verified entitlement. " +
                            "No item was granted.");
                        return Result(PurchaseStatus.Failed, StatusMessage);
                    }

                    SetAvailable("Purchase verified. This optional item is now owned.");
                    return Result(PurchaseStatus.Purchased, StatusMessage);
                }
                finally
                {
                    m_Operation.Release();
                }
            }
            catch (OperationCanceledException)
            {
                if (nativePurchaseStarted)
                {
                    MarkRefreshRequired();
                    SetAvailable(
                        "Store confirmation closed. Ownership will be checked " +
                        "when the game resumes.");
                }
                else
                {
                    SetAvailable(
                        "Purchase confirmation closed before the store opened.");
                }

                throw;
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                MarkRefreshRequired();
                SetTransientFailure(
                    "The store response was interrupted. No item was guessed or granted.");
                return Result(PurchaseStatus.Failed, StatusMessage);
            }
            finally
            {
                Interlocked.Exchange(ref m_PurchaseActive, 0);
            }
        }

        public async ValueTask<EntitlementSnapshot> RestoreAsync(
            CancellationToken cancellationToken)
        {
            return await CompleteSnapshotOperationAsync(
                "Checking purchases with the store…",
                token => m_Gateway.RestoreAsync(token),
                markForRetry: false,
                cancellationToken);
        }

        public async ValueTask<EntitlementSnapshot> RefreshEntitlementsAsync(
            CancellationToken cancellationToken)
        {
            return await CompleteSnapshotOperationAsync(
                "Refreshing optional ownership…",
                token => m_Gateway.RefreshAsync(token),
                markForRetry: true,
                cancellationToken);
        }

        public async ValueTask ResumeAsync(CancellationToken cancellationToken)
        {
            if (!CanOperate() ||
                Interlocked.CompareExchange(ref m_ResumeRefreshActive, 1, 0) != 0)
            {
                return;
            }

            try
            {
                if (!RefreshRequired())
                {
                    return;
                }

                await RefreshEntitlementsAsync(cancellationToken);
            }
            finally
            {
                Interlocked.Exchange(ref m_ResumeRefreshActive, 0);
            }
        }

        public ValueTask WaitForIdentityIdleAsync() =>
            new ValueTask(m_IdentityTransition);

        private async ValueTask<EntitlementSnapshot> CompleteSnapshotOperationAsync(
            string busyMessage,
            Func<CancellationToken, ValueTask<RevenueCatGatewayResult>> operation,
            bool markForRetry,
            CancellationToken cancellationToken)
        {
            if (!CanOperate())
            {
                return CurrentEntitlements;
            }

            var generation = Interlocked.Read(ref m_Generation);
            await m_Operation.WaitAsync(cancellationToken);
            try
            {
                if (!IsCurrent(generation))
                {
                    return CurrentEntitlements;
                }

                SetBusy(busyMessage);
                var response = await operation(cancellationToken);
                if (!IsCurrent(generation))
                {
                    return CurrentEntitlements;
                }

                if (response.Status == RevenueCatGatewayResultStatus.Succeeded &&
                    TryPublishVerified(response.CustomerInfo, generation))
                {
                    SetAvailable(CurrentEntitlements.ActiveEntitlements.Count == 0
                        ? "No previous optional purchases were found."
                        : "Optional purchases were verified for this game profile.");
                    return CurrentEntitlements;
                }

                if (markForRetry ||
                    response.Status == RevenueCatGatewayResultStatus.Pending)
                {
                    MarkRefreshRequired();
                }

                SetTransientFailure(response.Status == RevenueCatGatewayResultStatus.Cancelled
                    ? "Restore cancelled. Existing verified ownership is unchanged."
                    : "The store could not verify ownership. Existing verified " +
                      "offline items are unchanged.");
                return CurrentEntitlements;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                if (markForRetry)
                {
                    MarkRefreshRequired();
                }

                SetTransientFailure(
                    "The store connection was interrupted. Existing verified " +
                    "offline items are unchanged.");
                return CurrentEntitlements;
            }
            finally
            {
                m_Operation.Release();
            }
        }

        private void HandleAccountStateChanged(AccountState state)
        {
            if (!m_Initialized || m_Lifetime == null)
            {
                return;
            }

            var targetUid = FirebaseUid(state);
            long generation;
            lock (m_StateGate)
            {
                if (string.Equals(
                    targetUid,
                    m_FirebaseUserId,
                    StringComparison.Ordinal))
                {
                    return;
                }

                generation = Interlocked.Increment(ref m_Generation);
                m_Current = EntitlementSnapshot.Empty;
                m_Availability = StoreAvailability.Checking;
                m_StatusMessage =
                    "Changing game profile. Purchases are temporarily locked.";
            }

            NotifyChanged();
            m_IdentityTransition = ApplyIdentityTransitionSafelyAsync(
                targetUid,
                generation,
                m_Lifetime.Token);
        }

        private async Task ApplyIdentityTransitionSafelyAsync(
            string targetUid,
            long generation,
            CancellationToken cancellationToken)
        {
            try
            {
                await ApplyIdentityTransitionAsync(
                    targetUid,
                    generation,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                if (IsCurrent(generation))
                {
                    SetUnavailable(
                        StoreAvailability.Failed,
                        "The optional store could not safely change game profile. " +
                        "Purchases remain locked.");
                }
            }
        }

        private async Task ApplyIdentityTransitionAsync(
            string targetUid,
            long generation,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(targetUid) && !SafeFirebaseUserId.IsMatch(targetUid))
            {
                if (IsCurrent(generation))
                {
                    SetUnavailable(
                        StoreAvailability.UnavailableConfiguration,
                        "The linked game profile cannot be used safely by the store.");
                }

                return;
            }

            await m_Operation.WaitAsync(cancellationToken);
            try
            {
                if (!IsCurrent(generation))
                {
                    return;
                }

                RevenueCatGatewayResult response = null;
                var previousGatewayUserId = m_Gateway.CurrentAppUserId;
                if (!string.Equals(
                    targetUid,
                    m_FirebaseUserId,
                    StringComparison.Ordinal))
                {
                    response = string.IsNullOrEmpty(targetUid)
                        ? await m_Gateway.LogOutAsync(cancellationToken)
                        : await m_Gateway.LogInAsync(targetUid, cancellationToken);
                }

                if (!IsCurrent(generation))
                {
                    return;
                }

                if (response != null &&
                    (response.Status != RevenueCatGatewayResultStatus.Succeeded ||
                        !GatewayIdentityMatchesTarget(
                            targetUid,
                            previousGatewayUserId)))
                {
                    SetUnavailable(
                        StoreAvailability.Failed,
                        "The optional store could not safely change game profile. " +
                        "Purchases remain locked.");
                    return;
                }

                if (!IsCompleteGatewayIdentity())
                {
                    SetUnavailable(
                        StoreAvailability.Failed,
                        "The optional store returned an incomplete game profile. " +
                        "Purchases remain locked.");
                    return;
                }

                lock (m_StateGate)
                {
                    m_FirebaseUserId = targetUid;
                    m_Current = m_Cache.Load(
                        m_Gateway.CurrentAppUserId,
                        m_Gateway.AppFingerprint,
                        m_Gateway.Environment,
                        m_Gateway.AndroidPackageId) ?? EntitlementSnapshot.Empty;
                    m_Availability = StoreAvailability.Available;
                    m_StatusMessage = m_Current.ActiveEntitlements.Count > 0
                        ? "Previously verified optional items are available offline."
                        : "Optional purchases are available.";
                }

                if (response != null &&
                    response.Status == RevenueCatGatewayResultStatus.Succeeded)
                {
                    TryPublishVerified(response.CustomerInfo, generation);
                }
            }
            finally
            {
                m_Operation.Release();
            }

            NotifyChanged();
        }

        private void HandleCustomerInfoUpdated(RevenueCatCustomerInfo info)
        {
            var generation = Interlocked.Read(ref m_Generation);
            TryPublishVerified(info, generation);
        }

        private bool TryPublishVerified(
            RevenueCatCustomerInfo info,
            long generation)
        {
            if (info == null || !IsCurrent(generation) ||
                !IsAcceptableVerification(info.Verification) ||
                !string.Equals(
                    info.AppUserId,
                    m_Gateway.CurrentAppUserId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            var verified = info.Entitlements
                .Where(value => value.IsActive &&
                    IsAcceptableVerification(value.Verification) &&
                    CommerceProductMap.IsAllowedEntitlement(value.Identifier))
                .Select(value => new ContentId(value.Identifier))
                .Distinct()
                .ToArray();
            var snapshot = new EntitlementSnapshot(
                info.AppUserId,
                m_Gateway.AppFingerprint,
                m_Gateway.Environment,
                m_Gateway.AndroidPackageId,
                info.Verification,
                EntitlementSource.CustomerInfo,
                info.RequestDateUtc,
                verified);
            m_Cache.ReplaceVerified(snapshot);
            m_Cache.ClearRefreshRequired();
            lock (m_StateGate)
            {
                if (!IsCurrent(generation))
                {
                    return false;
                }

                m_Current = snapshot;
            }

            NotifyChanged();
            return true;
        }

        private bool IsCompleteGatewayIdentity() =>
            m_Gateway.IsConfigured &&
            !string.IsNullOrWhiteSpace(m_Gateway.AppFingerprint) &&
            m_Gateway.Environment != StoreEnvironment.Unavailable &&
            !string.IsNullOrWhiteSpace(m_Gateway.AndroidPackageId) &&
            !string.IsNullOrWhiteSpace(m_Gateway.CurrentAppUserId);

        private bool GatewayIdentityMatchesTarget(
            string targetUid,
            string previousGatewayUserId)
        {
            var gatewayUserId = m_Gateway.CurrentAppUserId;
            if (!string.IsNullOrEmpty(targetUid))
            {
                return string.Equals(
                    gatewayUserId,
                    targetUid,
                    StringComparison.Ordinal);
            }

            return !string.IsNullOrWhiteSpace(gatewayUserId) &&
                !string.Equals(
                    gatewayUserId,
                    previousGatewayUserId,
                    StringComparison.Ordinal) &&
                gatewayUserId.StartsWith(
                    "$RCAnonymousID:",
                    StringComparison.Ordinal);
        }

        private bool CanOperate() =>
            m_Initialized &&
            (Availability == StoreAvailability.Available ||
                Availability == StoreAvailability.Offline) &&
            IsCompleteGatewayIdentity();

        private bool IsCurrent(long generation) =>
            m_Initialized && generation == Interlocked.Read(ref m_Generation);

        private void MarkRefreshRequired()
        {
            if (IsCompleteGatewayIdentity())
            {
                m_Cache.MarkRefreshRequired(
                    m_Gateway.CurrentAppUserId,
                    m_Gateway.AppFingerprint,
                    m_Gateway.Environment,
                    m_Gateway.AndroidPackageId);
            }
        }

        private bool RefreshRequired() => IsCompleteGatewayIdentity() &&
            m_Cache.IsRefreshRequired(
                m_Gateway.CurrentAppUserId,
                m_Gateway.AppFingerprint,
                m_Gateway.Environment,
                m_Gateway.AndroidPackageId);

        private PurchaseResult Result(PurchaseStatus status, string message) =>
            new PurchaseResult(status, CurrentEntitlements, message);

        private void SetBusy(string message)
        {
            lock (m_StateGate)
            {
                m_StatusMessage = message;
            }

            NotifyChanged();
        }

        private void SetAvailable(string message)
        {
            lock (m_StateGate)
            {
                m_Availability = StoreAvailability.Available;
                m_StatusMessage = message;
            }

            NotifyChanged();
        }

        private void SetTransientFailure(string message)
        {
            lock (m_StateGate)
            {
                m_Availability = StoreAvailability.Offline;
                m_StatusMessage = message;
            }

            NotifyChanged();
        }

        private void SetUnavailable(StoreAvailability availability, string message)
        {
            lock (m_StateGate)
            {
                m_Availability = availability;
                m_Current = EntitlementSnapshot.Empty;
                m_StatusMessage = message;
            }

            NotifyChanged();
        }

        private void NotifyChanged() => StateChanged?.Invoke();

        private static bool IsAcceptableVerification(
            EntitlementVerification verification) =>
            verification == EntitlementVerification.Verified ||
            verification == EntitlementVerification.VerifiedOnDevice;

        private static string FirebaseUid(AccountState state) =>
            state == null || string.IsNullOrWhiteSpace(state.FirebaseUserId)
                ? string.Empty
                : state.FirebaseUserId;

        private static bool IsRecoverable(Exception exception) =>
            !(exception is OutOfMemoryException) &&
            !(exception is StackOverflowException) &&
            !(exception is AccessViolationException);
    }

    internal static class CommerceProductMap
    {
        internal const string OfferingId = "launch_cosmetics";

        private static readonly ProductMapping[] Mappings =
        {
            new ProductMapping(
                "store.explorer-edition",
                "jss.edition.explorer",
                "explorer_edition_package",
                "explorer_edition"),
            new ProductMapping(
                "store.founders-constellation",
                "jss.pack.founders_constellation",
                "founders_constellation_package",
                "founders_constellation"),
            new ProductMapping(
                "store.complete-launch-collection",
                "jss.pack.complete_launch_collection",
                "complete_launch_collection_package",
                "complete_launch_collection"),
            new ProductMapping(
                "store.mirra-collection",
                "jss.collection.mirra",
                "mirra_collection_package",
                "mirra_collection"),
            new ProductMapping(
                "store.koro-vesper-collection",
                "jss.collection.koro_vesper",
                "koro_vesper_collection_package",
                "koro_vesper_collection"),
            new ProductMapping(
                "store.aster-veil-collection",
                "jss.collection.aster_veil",
                "aster_veil_collection_package",
                "aster_veil_collection"),
            new ProductMapping("cosmetic.captain.launch-navigator", "jss.cosmetic.captain.launch-navigator", "captain_launch_navigator_package", "jss.cosmetic.captain.launch-navigator"),
            new ProductMapping("cosmetic.captain.launch-planetary", "jss.cosmetic.captain.launch-planetary", "captain_launch_planetary_package", "jss.cosmetic.captain.launch-planetary"),
            new ProductMapping("cosmetic.captain.launch-starlight", "jss.cosmetic.captain.launch-starlight", "captain_launch_starlight_package", "jss.cosmetic.captain.launch-starlight"),
            new ProductMapping("cosmetic.captain.star-charm", "jss.cosmetic.captain.star-charm", "captain_star_charm_package", "jss.cosmetic.captain.star-charm"),
            new ProductMapping("cosmetic.captain.birthday-charm", "jss.cosmetic.captain.birthday-charm", "captain_birthday_charm_package", "jss.cosmetic.captain.birthday-charm"),
            new ProductMapping("cosmetic.captain.ori-wristlink", "jss.cosmetic.captain.ori-wristlink", "captain_ori_wristlink_package", "jss.cosmetic.captain.ori-wristlink"),
            new ProductMapping("cosmetic.ori.festival-canopy", "jss.cosmetic.ori.festival-canopy", "ori_festival_canopy_package", "jss.cosmetic.ori.festival-canopy"),
            new ProductMapping("cosmetic.ori.moon-chimes", "jss.cosmetic.ori.moon-chimes", "ori_moon_chimes_package", "jss.cosmetic.ori.moon-chimes"),
            new ProductMapping("cosmetic.ori.comet-trail", "jss.cosmetic.ori.comet-trail", "ori_comet_trail_package", "jss.cosmetic.ori.comet-trail"),
            new ProductMapping("cosmetic.ship.builder-rig", "jss.cosmetic.ship.builder-rig", "ship_builder_rig_package", "jss.cosmetic.ship.builder-rig"),
            new ProductMapping("cosmetic.ship.signal-tower", "jss.cosmetic.ship.signal-tower", "ship_signal_tower_package", "jss.cosmetic.ship.signal-tower"),
            new ProductMapping("cosmetic.ship.comet-launch", "jss.cosmetic.ship.comet-launch", "ship_comet_launch_package", "jss.cosmetic.ship.comet-launch"),
            new ProductMapping("cosmetic.lens.rocket-window", "jss.cosmetic.lens.rocket-window", "lens_rocket_window_package", "jss.cosmetic.lens.rocket-window"),
            new ProductMapping("cosmetic.lens.starlight-compass", "jss.cosmetic.lens.starlight-compass", "lens_starlight_compass_package", "jss.cosmetic.lens.starlight-compass"),
            new ProductMapping("cosmetic.clubhouse.moon-chair", "jss.cosmetic.clubhouse.moon-chair", "clubhouse_moon_chair_package", "jss.cosmetic.clubhouse.moon-chair"),
            new ProductMapping("cosmetic.clubhouse.ori-radio", "jss.cosmetic.clubhouse.ori-radio", "clubhouse_ori_radio_package", "jss.cosmetic.clubhouse.ori-radio"),
            new ProductMapping("cosmetic.photo.captain-pose", "jss.cosmetic.photo.captain-pose", "photo_captain_pose_package", "jss.cosmetic.photo.captain-pose"),
            new ProductMapping("cosmetic.photo.stargazer-pose", "jss.cosmetic.photo.stargazer-pose", "photo_stargazer_pose_package", "jss.cosmetic.photo.stargazer-pose"),
            new ProductMapping("cosmetic.crew.launch-homecoming", "jss.cosmetic.crew.launch-homecoming", "crew_launch_homecoming_package", "jss.cosmetic.crew.launch-homecoming"),
            new ProductMapping("cosmetic.crew.birthday-expedition", "jss.cosmetic.crew.birthday-expedition", "crew_birthday_expedition_package", "jss.cosmetic.crew.birthday-expedition"),
        };

        internal static IReadOnlyList<StoreProduct> Project(
            IReadOnlyList<RevenueCatGatewayProduct> products)
        {
            var projected = new List<StoreProduct>();
            foreach (var source in products ?? Array.Empty<RevenueCatGatewayProduct>())
            {
                var mapping = Mappings.FirstOrDefault(value =>
                    string.Equals(
                        value.StoreProductId,
                        source.StoreProductId,
                        StringComparison.Ordinal) &&
                    string.Equals(value.PackageId, source.PackageId, StringComparison.Ordinal) &&
                    string.Equals(OfferingId, source.OfferingId, StringComparison.Ordinal));
                if (mapping == null ||
                    string.IsNullOrWhiteSpace(source.Title) ||
                    string.IsNullOrWhiteSpace(source.Description) ||
                    string.IsNullOrWhiteSpace(source.FormattedPrice))
                {
                    continue;
                }

                projected.Add(new StoreProduct(
                    new ContentId(mapping.ContentId),
                    source.StoreProductId,
                    source.OfferingId,
                    source.PackageId,
                    new ContentId(mapping.EntitlementId),
                    source.Title.Trim(),
                    source.Description.Trim(),
                    source.FormattedPrice.Trim(),
                    source.CurrencyCode?.Trim()));
            }

            return new ReadOnlyCollection<StoreProduct>(projected);
        }

        internal static bool IsAllowedEntitlement(string identifier) =>
            Mappings.Any(value => string.Equals(
                value.EntitlementId,
                identifier,
                StringComparison.Ordinal));

        private sealed class ProductMapping
        {
            public ProductMapping(
                string contentId,
                string storeProductId,
                string packageId,
                string entitlementId)
            {
                ContentId = contentId;
                StoreProductId = storeProductId;
                PackageId = packageId;
                EntitlementId = entitlementId;
            }

            public string ContentId { get; }

            public string StoreProductId { get; }

            public string PackageId { get; }

            public string EntitlementId { get; }
        }
    }
}
