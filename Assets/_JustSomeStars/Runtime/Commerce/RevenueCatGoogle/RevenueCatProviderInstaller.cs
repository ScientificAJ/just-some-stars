using JustSomeStars.Runtime.Accounts;
using UnityEngine;

namespace JustSomeStars.Runtime.Commerce.RevenueCatGoogle
{
    internal static class RevenueCatProviderInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            StoreProviderRegistry.Factory = Create;
        }

        private static IStoreService Create(IAccountService account)
        {
            if (!RevenueCatRuntimeConfiguration.TryLoad(out var configuration))
            {
                return new UnavailableStoreService();
            }

            return new RevenueCatStoreService(
                account,
                new RevenueCatUnityGateway(configuration),
                new OfflineEntitlementCache());
        }
    }
}
