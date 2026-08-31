using System;
using System.Collections.Generic;
using System.Linq;

namespace JustSomeStars.Editor.Build
{
    internal enum SamsungIapBuildMode
    {
        Production = 0,
        Test = 1,
        TestFailure = 2,
    }

    internal static class SamsungIapBuildModePolicy
    {
        internal const string TestSymbol = "JSS_GALAXY_IAP_TEST";
        internal const string TestFailureSymbol =
            "JSS_GALAXY_IAP_TEST_FAILURE";

        internal static SamsungIapBuildMode Resolve(
            IEnumerable<string> defineSymbols)
        {
            var symbols = new HashSet<string>(
                defineSymbols ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            if (!symbols.Contains(BuildConfiguration.GalaxySymbol))
            {
                throw new InvalidOperationException(
                    "Samsung IAP mode may only be resolved for Galaxy builds.");
            }

            if (symbols.Contains(TestSymbol) ||
                symbols.Contains(TestFailureSymbol))
            {
                throw new InvalidOperationException(
                    "BuildGalaxyRelease is production-only. Samsung test modes " +
                    "require an explicit non-release evidence invocation.");
            }

            return SamsungIapBuildMode.Production;
        }
    }
}
