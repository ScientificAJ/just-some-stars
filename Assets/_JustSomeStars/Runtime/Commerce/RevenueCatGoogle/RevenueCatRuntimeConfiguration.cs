using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace JustSomeStars.Runtime.Commerce.RevenueCatGoogle
{
    internal sealed class RevenueCatRuntimeConfiguration
    {
        private RevenueCatRuntimeConfiguration(
            string apiKey,
            StoreEnvironment environment,
            string packageId,
            string fingerprint)
        {
            ApiKey = apiKey;
            Environment = environment;
            PackageId = packageId;
            Fingerprint = fingerprint;
        }

        public string ApiKey { get; }

        public StoreEnvironment Environment { get; }

        public string PackageId { get; }

        public string Fingerprint { get; }

        public static bool TryLoad(out RevenueCatRuntimeConfiguration configuration)
        {
            configuration = null;
            var asset = Resources.Load<TextAsset>("JssRevenueCatConfiguration");
            if (asset == null)
            {
                return false;
            }

            try
            {
                var document = JsonUtility.FromJson<ConfigurationDocument>(asset.text);
                if (document == null ||
                    string.IsNullOrWhiteSpace(document.apiKey) ||
                    string.IsNullOrWhiteSpace(document.packageId) ||
                    !Enum.TryParse(
                        document.environment,
                        ignoreCase: false,
                        out StoreEnvironment environment) ||
                    environment == StoreEnvironment.Unavailable ||
                    environment == StoreEnvironment.Galaxy ||
                    !string.Equals(
                        document.packageId,
                        Application.identifier,
                        StringComparison.Ordinal) ||
                    !HasExpectedPrefix(document.apiKey, environment))
                {
                    return false;
                }

                configuration = new RevenueCatRuntimeConfiguration(
                    document.apiKey,
                    environment,
                    document.packageId,
                    ComputeFingerprint(document.apiKey));
                return true;
            }
            finally
            {
                Resources.UnloadAsset(asset);
            }
        }

        private static bool HasExpectedPrefix(
            string apiKey,
            StoreEnvironment environment) =>
            environment == StoreEnvironment.RevenueCatTestStore
                ? apiKey.StartsWith("test_", StringComparison.Ordinal)
                : environment == StoreEnvironment.GooglePlay &&
                  apiKey.StartsWith("goog_", StringComparison.Ordinal);

        private static string ComputeFingerprint(string apiKey)
        {
            using var sha256 = SHA256.Create();
            var digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(apiKey));
            var builder = new StringBuilder("sha256:");
            foreach (var value in digest)
            {
                builder.Append(value.ToString("x2"));
            }

            return builder.ToString();
        }

        [Serializable]
        private sealed class ConfigurationDocument
        {
            public string apiKey;
            public string environment;
            public string packageId;
        }
    }
}
