using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Core;
using UnityEngine;

namespace JustSomeStars.Runtime.Accounts
{
    public sealed class GuestAccountService : IGameService
    {
        private const int IdentitySchemaVersion = 1;
        private readonly string m_Path;
        private readonly string m_BackupPath;
        private readonly string m_TemporaryPath;

        public GuestAccountService()
            : this(GetDefaultPath())
        {
        }

        public GuestAccountService(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Guest identity path is required.", nameof(path));
            }

            m_Path = Path.GetFullPath(path);
            m_BackupPath = m_Path + ".backup";
            m_TemporaryPath = m_Path + ".tmp";
            Current = CreateState(string.Empty, durable: false);
        }

        public AccountState Current { get; private set; }

        public bool IsDurable { get; private set; }

        public ValueTask<StartupResult> InitializeAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryRead(m_Path, out var guestId) ||
                TryRead(m_BackupPath, out guestId))
            {
                Current = CreateState(guestId, durable: true);
                IsDurable = true;
                return new ValueTask<StartupResult>(StartupResult.Available());
            }

            guestId = "guest." + CreateOpaqueId();
            try
            {
                WriteAtomically(guestId);
                Current = CreateState(guestId, durable: true);
                IsDurable = true;
            }
            catch (IOException)
            {
                Current = CreateState(guestId, durable: false);
                IsDurable = false;
            }
            catch (UnauthorizedAccessException)
            {
                Current = CreateState(guestId, durable: false);
                IsDurable = false;
            }

            return new ValueTask<StartupResult>(StartupResult.Available());
        }

        public ValueTask ShutdownAsync()
        {
            return default;
        }

        private void WriteAtomically(string guestId)
        {
            var directory = Path.GetDirectoryName(m_Path);
            if (string.IsNullOrEmpty(directory))
            {
                throw new InvalidOperationException(
                    "Guest identity path must have a parent directory.");
            }

            Directory.CreateDirectory(directory);
            DeleteOwnedTemporary();
            try
            {
                var document = JsonUtility.ToJson(new IdentityDocument
                {
                    schemaVersion = IdentitySchemaVersion,
                    guestId = guestId,
                });
                using (var stream = new FileStream(
                    m_TemporaryPath,
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

                if (!TryRead(m_TemporaryPath, out var verified) ||
                    !string.Equals(verified, guestId, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "Durably written guest identity failed validation.");
                }

                if (!File.Exists(m_Path))
                {
                    File.Move(m_TemporaryPath, m_Path);
                }
                else
                {
                    File.Replace(m_TemporaryPath, m_Path, m_BackupPath);
                }
            }
            finally
            {
                DeleteOwnedTemporary();
            }
        }

        private static bool TryRead(string path, out string guestId)
        {
            guestId = null;
            try
            {
                if (!File.Exists(path))
                {
                    return false;
                }

                var document = JsonUtility.FromJson<IdentityDocument>(
                    File.ReadAllText(path));
                if (document == null ||
                    document.schemaVersion != IdentitySchemaVersion ||
                    !IsValidGuestId(document.guestId))
                {
                    return false;
                }

                guestId = document.guestId;
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

        private static bool IsValidGuestId(string value)
        {
            if (value == null || value.Length != 38 ||
                !value.StartsWith("guest.", StringComparison.Ordinal))
            {
                return false;
            }

            for (var index = 6; index < value.Length; index++)
            {
                var character = value[index];
                var isHex = character >= '0' && character <= '9' ||
                    character >= 'a' && character <= 'f';
                if (!isHex)
                {
                    return false;
                }
            }

            return true;
        }

        private static string CreateOpaqueId()
        {
            var bytes = new byte[16];
            using (var generator = RandomNumberGenerator.Create())
            {
                generator.GetBytes(bytes);
            }

            var builder = new StringBuilder(32);
            foreach (var value in bytes)
            {
                builder.Append(value.ToString("x2"));
            }

            return builder.ToString();
        }

        private static AccountState CreateState(string guestId, bool durable)
        {
            return new AccountState(
                AccountConnection.OfflineGuest,
                AccountCapability.Offline,
                AccountSyncState.LocalOnly,
                AccountOperation.None,
                guestId,
                string.Empty,
                durable
                    ? "Playing offline. Progress stays on this device."
                    : "Playing offline. Guest identity could not be saved yet.");
        }

        private void DeleteOwnedTemporary()
        {
            if (File.Exists(m_TemporaryPath))
            {
                File.Delete(m_TemporaryPath);
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
                "jss-guest-account-v1.json"));
#else
            return Path.Combine(
                Application.persistentDataPath,
                "jss-guest-account-v1.json");
#endif
        }

        [Serializable]
        private sealed class IdentityDocument
        {
            public int schemaVersion;
            public string guestId;
        }
    }
}
