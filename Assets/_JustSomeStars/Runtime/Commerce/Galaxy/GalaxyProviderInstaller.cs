using System;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accounts;
using UnityEngine;

namespace JustSomeStars.Runtime.Commerce.Galaxy
{
    public static class GalaxyReceiptVerifierRegistry
    {
        public static Func<IGalaxyReceiptVerifier> Factory { get; set; }

        internal static IGalaxyReceiptVerifier Create() =>
            Factory?.Invoke() ?? new UnavailableGalaxyReceiptVerifier();

        internal static void Reset() => Factory = null;
    }

    public sealed class UnavailableGalaxyReceiptVerifier :
        IGalaxyReceiptVerifier
    {
        public bool IsConfigured => false;
        public string Revision => "unavailable";

        public ValueTask<GalaxyVerifiedAuthority> VerifyAsync(
            string purchaseId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<GalaxyVerifiedAuthority>(
                new GalaxyVerifiedAuthority(
                    false,
                    purchaseId,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    DateTime.UtcNow));
        }

        public ValueTask<bool> ValidateCachedAuthorityAsync(
            GalaxyVerifiedAuthority authority,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<bool>(false);
        }
    }

    internal static class GalaxyProviderInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
#if JSS_GALAXY
            StoreProviderRegistry.Factory = Create;
#endif
        }

        private static IStoreService Create(IAccountService account) =>
            new GalaxyStoreService(
                account,
                new GalaxyAndroidJavaGateway(),
                GalaxyReceiptVerifierRegistry.Create(),
                new GalaxyFileEntitlementLedger());
    }
}
