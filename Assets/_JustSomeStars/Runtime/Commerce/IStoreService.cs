using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Core;

namespace JustSomeStars.Runtime.Commerce
{
    public enum StoreAvailability
    {
        Checking = 0,
        Available = 1,
        Offline = 2,
        UnavailableConfiguration = 3,
        UnavailablePlatform = 4,
        UnavailableDependency = 5,
        Failed = 6,
    }

    public enum PurchaseStatus
    {
        Purchased = 0,
        Cancelled = 1,
        Pending = 2,
        Failed = 3,
        Unavailable = 4,
        ProductUnavailable = 5,
        AlreadyOwned = 6,
    }

    public sealed class PurchaseResult
    {
        public PurchaseResult(
            PurchaseStatus status,
            EntitlementSnapshot entitlements,
            string message)
        {
            Status = status;
            Entitlements = entitlements?.Copy() ??
                EntitlementSnapshot.Empty;
            Message = message ?? string.Empty;
        }

        public PurchaseStatus Status { get; }

        public EntitlementSnapshot Entitlements { get; }

        public string Message { get; }
    }

    public interface IStoreService : IGameService
    {
        StoreAvailability Availability { get; }

        IReadOnlyList<StoreProduct> Products { get; }

        EntitlementSnapshot CurrentEntitlements { get; }

        string StatusMessage { get; }

        event Action StateChanged;

        ValueTask<IReadOnlyList<StoreProduct>> GetProductsAsync(
            CancellationToken cancellationToken);

        ValueTask<PurchaseResult> PurchaseAsync(
            ContentId productId,
            CancellationToken cancellationToken);

        ValueTask<EntitlementSnapshot> RestoreAsync(
            CancellationToken cancellationToken);

        ValueTask<EntitlementSnapshot> RefreshEntitlementsAsync(
            CancellationToken cancellationToken);

        ValueTask ResumeAsync(CancellationToken cancellationToken);
    }

    public sealed class UnavailableStoreService : IStoreService
    {
        private static readonly IReadOnlyList<StoreProduct> NoProducts =
            Array.Empty<StoreProduct>();

        public StoreAvailability Availability =>
            StoreAvailability.UnavailableConfiguration;

        public IReadOnlyList<StoreProduct> Products => NoProducts;

        public EntitlementSnapshot CurrentEntitlements =>
            EntitlementSnapshot.Empty;

        public string StatusMessage =>
            "Optional purchases are not configured for this build. " +
            "The complete story remains available.";

        public event Action StateChanged
        {
            add { }
            remove { }
        }

        public ValueTask<StartupResult> InitializeAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<StartupResult>(StartupResult.Unavailable(
                "Optional purchases are not configured for this build. " +
                "The complete story remains available."));
        }

        public ValueTask ShutdownAsync() => default;

        public ValueTask<IReadOnlyList<StoreProduct>> GetProductsAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<IReadOnlyList<StoreProduct>>(NoProducts);
        }

        public ValueTask<PurchaseResult> PurchaseAsync(
            ContentId productId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<PurchaseResult>(new PurchaseResult(
                PurchaseStatus.Unavailable,
                EntitlementSnapshot.Empty,
                "Optional purchases are unavailable. The story is still free."));
        }

        public ValueTask<EntitlementSnapshot> RestoreAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<EntitlementSnapshot>(EntitlementSnapshot.Empty);
        }

        public ValueTask<EntitlementSnapshot> RefreshEntitlementsAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<EntitlementSnapshot>(EntitlementSnapshot.Empty);
        }

        public ValueTask ResumeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return default;
        }
    }
}
