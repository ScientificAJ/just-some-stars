using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using JustSomeStars.Runtime.Core;
using UnityEngine;

namespace JustSomeStars.Runtime.Commerce
{
    public sealed class OfflineEntitlementCache
    {
        internal const int CurrentSchemaVersion = 1;

        private readonly object m_Gate = new object();
        private readonly string m_Path;
        private readonly string m_BackupPath;
        private readonly string m_TemporaryPath;
        private readonly string m_RefreshPath;
        private readonly string m_RefreshTemporaryPath;

        public OfflineEntitlementCache()
            : this(GetDefaultPath())
        {
        }

        public OfflineEntitlementCache(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException(
                    "Entitlement cache path is required.",
                    nameof(path));
            }

            m_Path = Path.GetFullPath(path);
            m_BackupPath = m_Path + ".backup";
            m_TemporaryPath = m_Path + ".tmp";
            m_RefreshPath = m_Path + ".refresh";
            m_RefreshTemporaryPath = m_RefreshPath + ".tmp";
        }

        public EntitlementSnapshot Load(
            string appUserId,
            string appFingerprint,
            StoreEnvironment environment,
            string packageId)
        {
            lock (m_Gate)
            {
                if (TryLoad(
                    m_Path,
                    appUserId,
                    appFingerprint,
                    environment,
                    packageId,
                    out var snapshot))
                {
                    return snapshot;
                }

                return TryLoad(
                    m_BackupPath,
                    appUserId,
                    appFingerprint,
                    environment,
                    packageId,
                    out snapshot)
                    ? snapshot
                    : null;
            }
        }

        public void ReplaceVerified(EntitlementSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (!snapshot.IsVerified ||
                string.IsNullOrWhiteSpace(snapshot.AppUserId) ||
                string.IsNullOrWhiteSpace(snapshot.AppFingerprint) ||
                snapshot.Environment == StoreEnvironment.Unavailable ||
                string.IsNullOrWhiteSpace(snapshot.PackageId))
            {
                throw new InvalidOperationException(
                    "Only complete verified identity-scoped snapshots may be cached.");
            }

            lock (m_Gate)
            {
                var document = ToDocument(snapshot);
                WriteAtomically(
                    m_Path,
                    m_BackupPath,
                    m_TemporaryPath,
                    JsonUtility.ToJson(document, prettyPrint: true),
                    verify: temporary => TryLoad(
                        temporary,
                        snapshot.AppUserId,
                        snapshot.AppFingerprint,
                        snapshot.Environment,
                        snapshot.PackageId,
                        out var reloaded) &&
                        reloaded.ActiveEntitlements.SequenceEqual(
                            snapshot.ActiveEntitlements));
            }
        }

        public void MarkRefreshRequired(
            string appUserId,
            string appFingerprint,
            StoreEnvironment environment,
            string packageId)
        {
            var marker = RefreshMarker.Create(
                appUserId,
                appFingerprint,
                environment,
                packageId);
            lock (m_Gate)
            {
                WriteAtomically(
                    m_RefreshPath,
                    backupPath: null,
                    temporaryPath: m_RefreshTemporaryPath,
                    document: JsonUtility.ToJson(marker),
                    verify: temporary => TryReadMarker(
                        temporary,
                        appUserId,
                        appFingerprint,
                        environment,
                        packageId));
            }
        }

        public bool IsRefreshRequired(
            string appUserId,
            string appFingerprint,
            StoreEnvironment environment,
            string packageId)
        {
            lock (m_Gate)
            {
                return TryReadMarker(
                    m_RefreshPath,
                    appUserId,
                    appFingerprint,
                    environment,
                    packageId);
            }
        }

        public void ClearRefreshRequired()
        {
            lock (m_Gate)
            {
                DeleteIfPresent(m_RefreshPath);
                DeleteIfPresent(m_RefreshTemporaryPath);
            }
        }

        public void Clear()
        {
            lock (m_Gate)
            {
                DeleteIfPresent(m_Path);
                DeleteIfPresent(m_BackupPath);
                DeleteIfPresent(m_TemporaryPath);
                DeleteIfPresent(m_RefreshPath);
                DeleteIfPresent(m_RefreshTemporaryPath);
            }
        }

        private static CacheDocument ToDocument(EntitlementSnapshot snapshot)
        {
            var document = new CacheDocument
            {
                schemaVersion = CurrentSchemaVersion,
                appUserId = snapshot.AppUserId,
                appFingerprint = snapshot.AppFingerprint,
                environment = (int)snapshot.Environment,
                packageId = snapshot.PackageId,
                verification = (int)snapshot.Verification,
                verifiedAtUtcTicks = snapshot.VerifiedAtUtc.Ticks,
                activeEntitlementIds = snapshot.ActiveEntitlements
                    .Select(value => value.Value)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
            };
            document.integritySha256 = Hash(Canonical(document));
            return document;
        }

        private static bool TryLoad(
            string path,
            string appUserId,
            string appFingerprint,
            StoreEnvironment environment,
            string packageId,
            out EntitlementSnapshot snapshot)
        {
            snapshot = null;
            try
            {
                if (!File.Exists(path))
                {
                    return false;
                }

                var document = JsonUtility.FromJson<CacheDocument>(
                    File.ReadAllText(path));
                if (document == null ||
                    document.schemaVersion != CurrentSchemaVersion ||
                    !FixedEquals(document.integritySha256, Hash(Canonical(document))) ||
                    !string.Equals(
                        document.appUserId,
                        appUserId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        document.appFingerprint,
                        appFingerprint,
                        StringComparison.Ordinal) ||
                    document.environment != (int)environment ||
                    !string.Equals(
                        document.packageId,
                        packageId,
                        StringComparison.Ordinal) ||
                    document.verifiedAtUtcTicks <= 0 ||
                    document.activeEntitlementIds == null)
                {
                    return false;
                }

                var verification = (EntitlementVerification)document.verification;
                if (verification != EntitlementVerification.Verified &&
                    verification != EntitlementVerification.VerifiedOnDevice)
                {
                    return false;
                }

                var entitlements = document.activeEntitlementIds
                    .Select(value => new ContentId(value))
                    .ToArray();
                if (entitlements.Distinct().Count() != entitlements.Length)
                {
                    return false;
                }

                snapshot = new EntitlementSnapshot(
                    document.appUserId,
                    document.appFingerprint,
                    environment,
                    document.packageId,
                    verification,
                    EntitlementSource.OfflineVerifiedCache,
                    new DateTime(
                        document.verifiedAtUtcTicks,
                        DateTimeKind.Utc),
                    entitlements);
                return true;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is CryptographicException)
            {
                return false;
            }
        }

        private static void WriteAtomically(
            string path,
            string backupPath,
            string temporaryPath,
            string document,
            Func<string, bool> verify)
        {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory))
            {
                throw new InvalidOperationException(
                    "Entitlement cache path must have a parent directory.");
            }

            Directory.CreateDirectory(directory);
            DeleteIfPresent(temporaryPath);
            try
            {
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None))
                using (var writer = new StreamWriter(
                    stream,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
                {
                    writer.Write(document);
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                }

                if (verify != null && !verify(temporaryPath))
                {
                    throw new InvalidDataException(
                        "The written entitlement cache failed validation.");
                }

                if (!File.Exists(path))
                {
                    File.Move(temporaryPath, path);
                }
                else if (string.IsNullOrEmpty(backupPath))
                {
                    File.Delete(path);
                    File.Move(temporaryPath, path);
                }
                else
                {
                    File.Replace(temporaryPath, path, backupPath);
                }
            }
            finally
            {
                DeleteIfPresent(temporaryPath);
            }
        }

        private static bool TryReadMarker(
            string path,
            string appUserId,
            string appFingerprint,
            StoreEnvironment environment,
            string packageId)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return false;
                }

                var marker = JsonUtility.FromJson<RefreshMarker>(
                    File.ReadAllText(path));
                return marker != null &&
                    marker.schemaVersion == CurrentSchemaVersion &&
                    string.Equals(marker.appUserId, appUserId, StringComparison.Ordinal) &&
                    string.Equals(
                        marker.appFingerprint,
                        appFingerprint,
                        StringComparison.Ordinal) &&
                    marker.environment == (int)environment &&
                    string.Equals(marker.packageId, packageId, StringComparison.Ordinal) &&
                    FixedEquals(marker.integritySha256, Hash(Canonical(marker)));
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException)
            {
                return false;
            }
        }

        private static string Canonical(CacheDocument document) => string.Join(
            "\n",
            document.schemaVersion,
            document.appUserId ?? string.Empty,
            document.appFingerprint ?? string.Empty,
            document.environment,
            document.packageId ?? string.Empty,
            document.verification,
            document.verifiedAtUtcTicks,
            string.Join("\n", document.activeEntitlementIds ?? Array.Empty<string>()));

        private static string Canonical(RefreshMarker marker) => string.Join(
            "\n",
            marker.schemaVersion,
            marker.appUserId ?? string.Empty,
            marker.appFingerprint ?? string.Empty,
            marker.environment,
            marker.packageId ?? string.Empty);

        private static string Hash(string value)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var item in bytes)
            {
                builder.Append(item.ToString("x2"));
            }

            return builder.ToString();
        }

        private static bool FixedEquals(string left, string right)
        {
            var leftBytes = Encoding.ASCII.GetBytes(left ?? string.Empty);
            var rightBytes = Encoding.ASCII.GetBytes(right ?? string.Empty);
            return leftBytes.Length == rightBytes.Length &&
                CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
        }

        private static void DeleteIfPresent(string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static string GetDefaultPath()
        {
#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "Library",
                "JustSomeStars",
                "Local",
                "jss-verified-entitlements-v1.json"));
#else
            return Path.Combine(
                Application.persistentDataPath,
                "jss-verified-entitlements-v1.json");
#endif
        }

        [Serializable]
        private sealed class CacheDocument
        {
            public int schemaVersion;
            public string appUserId;
            public string appFingerprint;
            public int environment;
            public string packageId;
            public int verification;
            public long verifiedAtUtcTicks;
            public string[] activeEntitlementIds;
            public string integritySha256;
        }

        [Serializable]
        private sealed class RefreshMarker
        {
            public int schemaVersion;
            public string appUserId;
            public string appFingerprint;
            public int environment;
            public string packageId;
            public string integritySha256;

            public static RefreshMarker Create(
                string appUserId,
                string appFingerprint,
                StoreEnvironment environment,
                string packageId)
            {
                if (string.IsNullOrWhiteSpace(appUserId) ||
                    string.IsNullOrWhiteSpace(appFingerprint) ||
                    environment == StoreEnvironment.Unavailable ||
                    string.IsNullOrWhiteSpace(packageId))
                {
                    throw new ArgumentException(
                        "A complete store identity is required for a refresh marker.");
                }

                var marker = new RefreshMarker
                {
                    schemaVersion = CurrentSchemaVersion,
                    appUserId = appUserId,
                    appFingerprint = appFingerprint,
                    environment = (int)environment,
                    packageId = packageId,
                };
                marker.integritySha256 = Hash(Canonical(marker));
                return marker;
            }
        }
    }
}
