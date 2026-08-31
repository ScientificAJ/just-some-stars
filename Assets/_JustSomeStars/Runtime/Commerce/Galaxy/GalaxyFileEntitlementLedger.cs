using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Core;
using UnityEngine;

namespace JustSomeStars.Runtime.Commerce.Galaxy
{
    public sealed class GalaxyFileEntitlementLedger :
        IGalaxyEntitlementLedger
    {
        private const int SchemaVersion = 1;
        private readonly object m_Gate = new object();
        private readonly string m_Path;
        private readonly string m_BackupPath;
        private readonly string m_TemporaryPath;

        public GalaxyFileEntitlementLedger()
            : this(Path.Combine(
                Application.persistentDataPath,
                "jss-galaxy-entitlements-v1.json"))
        {
        }

        public GalaxyFileEntitlementLedger(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException(
                    "A Galaxy entitlement ledger path is required.",
                    nameof(path));
            }

            m_Path = Path.GetFullPath(path);
            m_BackupPath = m_Path + ".bak";
            m_TemporaryPath = m_Path + ".tmp";
        }

        public bool IsKnownPurchase(string identity, string purchaseId)
        {
            lock (m_Gate)
            {
                return Read().authorities.Any(value =>
                    Matches(value.identity, identity) &&
                    Matches(value.purchaseId, purchaseId));
            }
        }

        public bool IsPendingItem(string identity, string itemId)
        {
            lock (m_Gate)
            {
                return Read().pendingPurchases.Any(value =>
                    Matches(value.identity, identity) &&
                    Matches(value.itemId, itemId));
            }
        }

        public bool IsReplayedPurchase(string purchaseId, string identity)
        {
            lock (m_Gate)
            {
                return Read().authorities.Any(value =>
                    Matches(value.purchaseId, purchaseId));
            }
        }

        public ValueTask PersistPendingAsync(
            string identity,
            string itemId,
            long identityGeneration,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireToken(identity, nameof(identity));
            RequireToken(itemId, nameof(itemId));
            lock (m_Gate)
            {
                var document = Read();
                var pending = document.pendingPurchases
                    .Where(value => !Matches(value.identity, identity))
                    .ToList();
                pending.Add(new PendingDocument
                {
                    identity = identity,
                    itemId = itemId,
                    identityGeneration = identityGeneration,
                });
                document.pendingPurchases = pending.ToArray();
                WriteAtomically(document);
            }

            return default;
        }

        public ValueTask PersistVerifiedAsync(
            string identity,
            GalaxyVerifiedAuthority authority,
            ContentId entitlementId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireToken(identity, nameof(identity));
            if (authority == null ||
                !authority.Verified ||
                string.IsNullOrWhiteSpace(authority.SignedAuthority))
            {
                throw new InvalidDataException(
                    "Only a server-verified signed Galaxy authority may be persisted.");
            }

            lock (m_Gate)
            {
                var document = Read();
                var conflictingOwner = document.authorities.FirstOrDefault(value =>
                    Matches(value.purchaseId, authority.PurchaseId) &&
                    !Matches(value.identity, identity));
                if (conflictingOwner != null)
                {
                    throw new InvalidDataException(
                        "A Galaxy purchase authority is already bound to another identity.");
                }

                var existing = document.authorities.FirstOrDefault(value =>
                    Matches(value.identity, identity) &&
                    Matches(value.purchaseId, authority.PurchaseId));
                var entries = document.authorities
                    .Where(value => existing == null || !ReferenceEquals(value, existing))
                    .ToList();
                entries.Add(ToDocument(
                    identity,
                    authority,
                    entitlementId.Value,
                    existing?.acknowledged ?? false));
                document.authorities = entries.ToArray();
                document.pendingPurchases = document.pendingPurchases
                    .Where(value =>
                        !Matches(value.identity, identity) ||
                        !Matches(value.itemId, authority.ItemId))
                    .ToArray();
                WriteAtomically(document);
            }

            return default;
        }

        public ValueTask<IReadOnlyList<GalaxyVerifiedAuthority>>
            LoadAuthoritiesAsync(
                string identity,
                CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (m_Gate)
            {
                IReadOnlyList<GalaxyVerifiedAuthority> authorities = Read()
                    .authorities
                    .Where(value => Matches(value.identity, identity))
                    .Select(ToAuthority)
                    .ToArray();
                return new ValueTask<IReadOnlyList<GalaxyVerifiedAuthority>>(
                    authorities);
            }
        }

        public ValueTask<IReadOnlyList<string>>
            LoadPendingAcknowledgementsAsync(
                string identity,
                CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (m_Gate)
            {
                IReadOnlyList<string> purchaseIds = Read()
                    .authorities
                    .Where(value =>
                        Matches(value.identity, identity) &&
                        !value.acknowledged)
                    .Select(value => value.purchaseId)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                return new ValueTask<IReadOnlyList<string>>(purchaseIds);
            }
        }

        public ValueTask MarkAcknowledgedAsync(
            string identity,
            string purchaseId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (m_Gate)
            {
                var document = Read();
                var changed = false;
                foreach (var authority in document.authorities)
                {
                    if (Matches(authority.identity, identity) &&
                        Matches(authority.purchaseId, purchaseId))
                    {
                        authority.acknowledged = true;
                        changed = true;
                    }
                }

                if (changed)
                {
                    WriteAtomically(document);
                }
            }

            return default;
        }

        public ValueTask ClearPendingAsync(
            string identity,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (m_Gate)
            {
                var document = Read();
                var remaining = document.pendingPurchases
                    .Where(value => !Matches(value.identity, identity))
                    .ToArray();
                if (remaining.Length != document.pendingPurchases.Length)
                {
                    document.pendingPurchases = remaining;
                    WriteAtomically(document);
                }
            }

            return default;
        }

        private LedgerDocument Read()
        {
            if (TryRead(m_Path, out var document))
            {
                return document;
            }

            if (TryRead(m_BackupPath, out document))
            {
                return document;
            }

            return LedgerDocument.Empty();
        }

        private static bool TryRead(string path, out LedgerDocument document)
        {
            document = null;
            try
            {
                if (!File.Exists(path))
                {
                    return false;
                }

                var parsed = JsonUtility.FromJson<LedgerDocument>(
                    File.ReadAllText(path, Encoding.UTF8));
                if (!IsValid(parsed))
                {
                    return false;
                }

                document = parsed;
                return true;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException)
            {
                return false;
            }
        }

        private void WriteAtomically(LedgerDocument document)
        {
            if (!IsValid(document))
            {
                throw new InvalidDataException(
                    "Refusing to write an invalid Galaxy entitlement ledger.");
            }

            var directory = Path.GetDirectoryName(m_Path);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException(
                    "Galaxy entitlement ledger has no parent directory.");
            }

            Directory.CreateDirectory(directory);
            DeleteIfPresent(m_TemporaryPath);
            try
            {
                using (var stream = new FileStream(
                    m_TemporaryPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None))
                using (var writer = new StreamWriter(
                    stream,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
                {
                    writer.Write(JsonUtility.ToJson(document));
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                }

                if (!TryRead(m_TemporaryPath, out _))
                {
                    throw new InvalidDataException(
                        "Written Galaxy entitlement ledger failed validation.");
                }

                if (File.Exists(m_Path))
                {
                    File.Replace(m_TemporaryPath, m_Path, m_BackupPath);
                }
                else
                {
                    File.Move(m_TemporaryPath, m_Path);
                }
            }
            finally
            {
                DeleteIfPresent(m_TemporaryPath);
            }
        }

        private static bool IsValid(LedgerDocument document)
        {
            if (document == null || document.schemaVersion != SchemaVersion)
            {
                return false;
            }

            document.authorities ??= Array.Empty<AuthorityDocument>();
            document.pendingPurchases ??= Array.Empty<PendingDocument>();
            return document.authorities.All(value =>
                       value != null &&
                       !string.IsNullOrWhiteSpace(value.identity) &&
                       !string.IsNullOrWhiteSpace(value.purchaseId) &&
                       !string.IsNullOrWhiteSpace(value.itemId) &&
                       !string.IsNullOrWhiteSpace(value.entitlementId) &&
                       !string.IsNullOrWhiteSpace(value.signedAuthority) &&
                       value.verifiedAtUtcTicks > 0) &&
                   document.pendingPurchases.All(value =>
                       value != null &&
                       !string.IsNullOrWhiteSpace(value.identity) &&
                       !string.IsNullOrWhiteSpace(value.itemId));
        }

        private static AuthorityDocument ToDocument(
            string identity,
            GalaxyVerifiedAuthority authority,
            string entitlementId,
            bool acknowledged) => new AuthorityDocument
        {
            identity = identity,
            purchaseId = authority.PurchaseId,
            itemId = authority.ItemId,
            entitlementId = entitlementId,
            packageId = authority.PackageId,
            mode = authority.Mode,
            obfuscatedAccountId = authority.ObfuscatedAccountId,
            obfuscatedProfileId = authority.ObfuscatedProfileId,
            signedAuthority = authority.SignedAuthority,
            verifiedAtUtcTicks = authority.VerifiedAtUtc.Ticks,
            acknowledged = acknowledged,
        };

        private static GalaxyVerifiedAuthority ToAuthority(
            AuthorityDocument document) => new GalaxyVerifiedAuthority(
                true,
                document.purchaseId,
                document.itemId,
                document.packageId,
                document.mode,
                document.obfuscatedAccountId,
                document.obfuscatedProfileId,
                document.signedAuthority,
                new DateTime(document.verifiedAtUtcTicks, DateTimeKind.Utc));

        private static void RequireToken(string value, string parameter)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
            {
                throw new ArgumentException(
                    "Galaxy ledger identifiers must be nonempty single-line tokens.",
                    parameter);
            }
        }

        private static bool Matches(string left, string right) =>
            string.Equals(left, right, StringComparison.Ordinal);

        private static void DeleteIfPresent(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        [Serializable]
        private sealed class LedgerDocument
        {
            public int schemaVersion;
            public AuthorityDocument[] authorities;
            public PendingDocument[] pendingPurchases;

            public static LedgerDocument Empty() => new LedgerDocument
            {
                schemaVersion = SchemaVersion,
                authorities = Array.Empty<AuthorityDocument>(),
                pendingPurchases = Array.Empty<PendingDocument>(),
            };
        }

        [Serializable]
        private sealed class AuthorityDocument
        {
            public string identity;
            public string purchaseId;
            public string itemId;
            public string entitlementId;
            public string packageId;
            public string mode;
            public string obfuscatedAccountId;
            public string obfuscatedProfileId;
            public string signedAuthority;
            public long verifiedAtUtcTicks;
            public bool acknowledged;
        }

        [Serializable]
        private sealed class PendingDocument
        {
            public string identity;
            public string itemId;
            public long identityGeneration;
        }
    }
}
