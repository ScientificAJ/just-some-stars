using System;
using System.IO;
using UnityEngine;

namespace JustSomeStars.Editor.Build
{
    internal interface ICommerceBuildConfigurationLease : IDisposable
    {
        void CleanupAndVerify();
    }

    internal interface ICommerceBuildConfigurationLeaseFactory
    {
        ICommerceBuildConfigurationLease Acquire(
            BuildConfiguration configuration);
    }

    internal static class RevenueCatBuildEnvironment
    {
        public const string TestStoreApiKeyVariable =
            "JSS_REVENUECAT_TEST_STORE_API_KEY";
        public const string GoogleApiKeyVariable =
            "JSS_REVENUECAT_GOOGLE_API_KEY";
    }

    internal sealed class RevenueCatBuildConfigurationLeaseFactory :
        ICommerceBuildConfigurationLeaseFactory
    {
        public const string GeneratedAssetDirectory =
            "Assets/_JustSomeStars/GeneratedCommerce";
        public const string ConfigurationAssetPath =
            GeneratedAssetDirectory +
            "/Resources/JssRevenueCatConfiguration.json";

        private readonly string m_ProjectRoot;
        private readonly Func<string, string> m_ReadVariable;
        private readonly Action m_RefreshAssets;

        public RevenueCatBuildConfigurationLeaseFactory(
            string projectRoot,
            Func<string, string> readVariable,
            Action refreshAssets)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new ArgumentException(
                    "A project root is required.",
                    nameof(projectRoot));
            }

            m_ProjectRoot = Path.GetFullPath(projectRoot);
            m_ReadVariable = readVariable ??
                throw new ArgumentNullException(nameof(readVariable));
            m_RefreshAssets = refreshAssets ??
                throw new ArgumentNullException(nameof(refreshAssets));
            RequireOwnedPath(GeneratedAssetDirectory);
        }

        public ICommerceBuildConfigurationLease Acquire(
            BuildConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var lease = new RevenueCatBuildConfigurationLease(
                m_ProjectRoot,
                m_RefreshAssets);
            try
            {
                lease.CleanupAndVerify();
                if (configuration.Kind == BuildTargetKind.Galaxy)
                {
                    var samsungMode = SamsungIapBuildModePolicy.Resolve(configuration.DefineSymbols);
                    if (samsungMode != SamsungIapBuildMode.Production)
                    {
                        throw new InvalidOperationException(
                            "Galaxy release builds must use Samsung IAP production mode.");
                    }
                }

                var testKey = NormalizeKey(
                    m_ReadVariable(
                        RevenueCatBuildEnvironment.TestStoreApiKeyVariable));
                var googleKey = NormalizeKey(
                    m_ReadVariable(
                        RevenueCatBuildEnvironment.GoogleApiKeyVariable));
                var apiKey = ResolveApiKey(
                    configuration.Kind,
                    testKey,
                    googleKey);
                if (apiKey == null)
                {
                    return lease;
                }

                var path = RequireOwnedPath(ConfigurationAssetPath);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var document = new ConfigurationDocument
                {
                    apiKey = apiKey,
                    environment = configuration.Kind == BuildTargetKind.GooglePlay
                        ? "GooglePlay"
                        : "RevenueCatTestStore",
                    packageId = configuration.PackageId,
                };
                File.WriteAllText(path, JsonUtility.ToJson(document));
                m_RefreshAssets();
                if (!File.Exists(path))
                {
                    throw new InvalidOperationException(
                        "RevenueCat build configuration was not materialized.");
                }

                return lease;
            }
            catch
            {
                lease.CleanupAndVerify();
                throw;
            }
        }

        private static string ResolveApiKey(
            BuildTargetKind kind,
            string testKey,
            string googleKey)
        {
            switch (kind)
            {
                case BuildTargetKind.AndroidInternal:
                    RequireAbsent(
                        googleKey,
                        RevenueCatBuildEnvironment.GoogleApiKeyVariable,
                        "internal Test Store builds");
                    if (testKey == null)
                    {
                        return null;
                    }

                    RequirePrefix(
                        testKey,
                        "test_",
                        RevenueCatBuildEnvironment.TestStoreApiKeyVariable);
                    return testKey;
                case BuildTargetKind.GooglePlay:
                    RequireAbsent(
                        testKey,
                        RevenueCatBuildEnvironment.TestStoreApiKeyVariable,
                        "Google Play builds");
                    if (googleKey == null)
                    {
                        throw new InvalidOperationException(
                            RevenueCatBuildEnvironment.GoogleApiKeyVariable +
                            " is required for Google Play builds.");
                    }

                    RequirePrefix(
                        googleKey,
                        "goog_",
                        RevenueCatBuildEnvironment.GoogleApiKeyVariable);
                    return googleKey;
                case BuildTargetKind.Galaxy:
                    RequireAbsent(
                        testKey,
                        RevenueCatBuildEnvironment.TestStoreApiKeyVariable,
                        "Galaxy builds");
                    RequireAbsent(
                        googleKey,
                        RevenueCatBuildEnvironment.GoogleApiKeyVariable,
                        "Galaxy builds");
                    return null;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        private static string NormalizeKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (!string.Equals(value, value.Trim(), StringComparison.Ordinal) ||
                value.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
            {
                throw new InvalidOperationException(
                    "RevenueCat public SDK keys must not contain surrounding " +
                    "whitespace or control characters.");
            }

            return value;
        }

        private static void RequirePrefix(
            string value,
            string prefix,
            string variable)
        {
            if (!value.StartsWith(prefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    variable + " has the wrong RevenueCat environment prefix.");
            }
        }

        private static void RequireAbsent(
            string value,
            string variable,
            string buildDescription)
        {
            if (value != null)
            {
                throw new InvalidOperationException(
                    variable + " must be unset for " + buildDescription + ".");
            }
        }

        private string RequireOwnedPath(string projectRelativePath)
        {
            var assetsRoot = Path.GetFullPath(Path.Combine(m_ProjectRoot, "Assets"));
            var candidate = Path.GetFullPath(Path.Combine(
                m_ProjectRoot,
                projectRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            var prefix = assetsRoot.TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(prefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Generated commerce path escapes the project Assets directory.");
            }

            return candidate;
        }

        [Serializable]
        private sealed class ConfigurationDocument
        {
            public string apiKey;
            public string environment;
            public string packageId;
        }
    }

    internal sealed class RevenueCatBuildConfigurationLease :
        ICommerceBuildConfigurationLease,
        IDisposable
    {
        private readonly string m_GeneratedRoot;
        private readonly string m_GeneratedRootMeta;
        private readonly Action m_RefreshAssets;

        public RevenueCatBuildConfigurationLease(
            string projectRoot,
            Action refreshAssets)
        {
            m_GeneratedRoot = Path.GetFullPath(Path.Combine(
                projectRoot,
                RevenueCatBuildConfigurationLeaseFactory.GeneratedAssetDirectory
                    .Replace('/', Path.DirectorySeparatorChar)));
            m_GeneratedRootMeta = m_GeneratedRoot + ".meta";
            m_RefreshAssets = refreshAssets ??
                throw new ArgumentNullException(nameof(refreshAssets));
        }

        public void CleanupAndVerify()
        {
            var changed = false;
            if (Directory.Exists(m_GeneratedRoot))
            {
                Directory.Delete(m_GeneratedRoot, recursive: true);
                changed = true;
            }

            if (File.Exists(m_GeneratedRootMeta))
            {
                File.Delete(m_GeneratedRootMeta);
                changed = true;
            }

            if (changed)
            {
                m_RefreshAssets();
            }

            if (Directory.Exists(m_GeneratedRoot) ||
                File.Exists(m_GeneratedRootMeta))
            {
                throw new InvalidOperationException(
                    "Temporary RevenueCat build configuration cleanup failed.");
            }

        }

        public void Dispose() => CleanupAndVerify();
    }
}
