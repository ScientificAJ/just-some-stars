using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Core;
using UnityEngine;

namespace JustSomeStars.Runtime.Saving
{
    internal interface ISaveSerializer
    {
        string Serialize(GameSave save);

        bool TryDeserialize(string document, out GameSave save);
    }

    internal sealed class JsonSaveSerializer : ISaveSerializer
    {
        private readonly SaveMigrator m_Migrator;

        internal JsonSaveSerializer(SaveMigrator migrator)
        {
            m_Migrator = migrator ?? throw new ArgumentNullException(nameof(migrator));
        }

        public string Serialize(GameSave save)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            save.ThrowIfInvalid(nameof(save));
            return JsonUtility.ToJson(save, prettyPrint: true);
        }

        public bool TryDeserialize(string document, out GameSave save)
        {
            save = null;
            if (!m_Migrator.TryMigrate(document, out var migrated))
            {
                return false;
            }

            foreach (var field in GameSave.RequiredJsonFields)
            {
                if (migrated.IndexOf($"\"{field}\"", StringComparison.Ordinal) < 0)
                {
                    return false;
                }
            }

            try
            {
                var candidate = JsonUtility.FromJson<GameSave>(migrated);
                if (candidate == null)
                {
                    return false;
                }

                candidate.ThrowIfInvalid(nameof(document));
                save = candidate;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }

    internal interface ISaveStorage
    {
        bool Exists(string path);

        string ReadAllText(string path);

        void WriteDurably(string path, string document);

        void Move(string sourcePath, string destinationPath);

        void Replace(
            string sourcePath,
            string destinationPath,
            string backupPath);

        void Delete(string path);
    }

    internal sealed class FileSaveStorage : ISaveStorage
    {
        public bool Exists(string path) => File.Exists(path);

        public string ReadAllText(string path) => File.ReadAllText(path);

        public void WriteDurably(string path, string document)
        {
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException(
                    "Save path must have a parent directory.",
                    nameof(path));
            }

            Directory.CreateDirectory(directory);
            using var stream = new FileStream(
                fullPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None);
            using var writer = new StreamWriter(
                stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.Write(document);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }

        public void Move(string sourcePath, string destinationPath)
        {
            File.Move(sourcePath, destinationPath);
        }

        public void Replace(
            string sourcePath,
            string destinationPath,
            string backupPath)
        {
            File.Replace(sourcePath, destinationPath, backupPath);
        }

        public void Delete(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    public sealed class LocalSaveService : ISaveService
    {
        private const string MissingMessage = "No local save exists yet.";
        private const string LoadedMessage = "Local progress loaded.";
        private const string RecoveredMessage =
            "The latest save could not be read, so the last complete checkpoint was restored.";
        private const string UnreadableMessage =
            "Local progress could not be read. The damaged files were kept so recovery remains possible.";
        private const string StorageUnavailableMessage =
            "Local progress is temporarily unavailable. You can keep playing offline and try again later.";

        private readonly object m_Gate = new object();
        private readonly string m_Path;
        private readonly string m_BackupPath;
        private readonly string m_TemporaryPath;
        private readonly ISaveSerializer m_Serializer;
        private readonly ISaveStorage m_Storage;

        private bool m_IsInitialized;
        private LoadSaveResult m_LastLoadResult = Missing();

        public LocalSaveService()
            : this(GetDefaultPath())
        {
        }

        public LocalSaveService(string path)
            : this(
                path,
                new JsonSaveSerializer(SaveMigrator.CreateCurrent()),
                new FileSaveStorage())
        {
        }

        internal LocalSaveService(
            string path,
            ISaveSerializer serializer,
            ISaveStorage storage)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Save path is required.", nameof(path));
            }

            m_Path = Path.GetFullPath(path);
            m_BackupPath = m_Path + ".backup";
            m_TemporaryPath = m_Path + ".tmp";
            m_Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            m_Storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public bool IsInitialized
        {
            get
            {
                lock (m_Gate)
                {
                    return m_IsInitialized;
                }
            }
        }

        public LoadSaveResult LastLoadResult
        {
            get
            {
                lock (m_Gate)
                {
                    return m_LastLoadResult.Copy();
                }
            }
        }

        public ValueTask<StartupResult> InitializeAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (m_Gate)
            {
                if (!m_IsInitialized)
                {
                    m_LastLoadResult = LoadCore();
                    cancellationToken.ThrowIfCancellationRequested();
                    m_IsInitialized = true;
                }

                return new ValueTask<StartupResult>(StartupResult.Available());
            }
        }

        public ValueTask ShutdownAsync()
        {
            lock (m_Gate)
            {
                m_IsInitialized = false;
            }

            return default;
        }

        public ValueTask<LoadSaveResult> LoadAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (m_Gate)
            {
                m_LastLoadResult = LoadCore();
                cancellationToken.ThrowIfCancellationRequested();
                return new ValueTask<LoadSaveResult>(m_LastLoadResult.Copy());
            }
        }

        public ValueTask<LoadSaveResult> RecoverAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (m_Gate)
            {
                m_LastLoadResult = RecoverCore();
                cancellationToken.ThrowIfCancellationRequested();
                return new ValueTask<LoadSaveResult>(m_LastLoadResult.Copy());
            }
        }

        public ValueTask SaveCheckpointAsync(
            GameSave save,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            var candidate = save.Copy();
            candidate.ThrowIfInvalid(nameof(save));
            lock (m_Gate)
            {
                if (!m_IsInitialized)
                {
                    throw new InvalidOperationException(
                        "Local save service must be initialized before writing checkpoints.");
                }

                cancellationToken.ThrowIfCancellationRequested();
                WriteCheckpoint(candidate);
                m_LastLoadResult = Loaded(candidate);
                return default;
            }
        }

        public GameSave Merge(GameSave local, GameSave cloud)
        {
            return SaveMerge.Combine(local, cloud);
        }

        private void WriteCheckpoint(GameSave candidate)
        {
            var document = m_Serializer.Serialize(candidate);
            if (!m_Serializer.TryDeserialize(document, out var serialized) ||
                !serialized.Equals(candidate))
            {
                throw new InvalidDataException(
                    "Save serializer did not round-trip the complete checkpoint.");
            }

            m_Storage.Delete(m_TemporaryPath);
            try
            {
                m_Storage.WriteDurably(m_TemporaryPath, document);
                var temporaryDocument = m_Storage.ReadAllText(m_TemporaryPath);
                if (!m_Serializer.TryDeserialize(temporaryDocument, out var temporarySave) ||
                    !temporarySave.Equals(candidate))
                {
                    throw new InvalidDataException(
                        "Durably written temporary save failed validation.");
                }

                if (!m_Storage.Exists(m_Path))
                {
                    m_Storage.Move(m_TemporaryPath, m_Path);
                    return;
                }

                var primaryDocument = m_Storage.ReadAllText(m_Path);
                var primaryIsReadable = m_Serializer.TryDeserialize(
                    primaryDocument,
                    out _);
                m_Storage.Replace(
                    m_TemporaryPath,
                    m_Path,
                    primaryIsReadable ? m_BackupPath : null);
            }
            finally
            {
                m_Storage.Delete(m_TemporaryPath);
            }
        }

        private LoadSaveResult LoadCore()
        {
            var primaryExists = false;
            var backupExists = false;
            var storageFailure = false;

            try
            {
                primaryExists = m_Storage.Exists(m_Path);
                if (primaryExists &&
                    m_Serializer.TryDeserialize(
                        m_Storage.ReadAllText(m_Path),
                        out var primary))
                {
                    return Loaded(primary);
                }
            }
            catch (IOException)
            {
                storageFailure = true;
            }
            catch (UnauthorizedAccessException)
            {
                storageFailure = true;
            }

            try
            {
                backupExists = m_Storage.Exists(m_BackupPath);
                if (backupExists &&
                    m_Serializer.TryDeserialize(
                        m_Storage.ReadAllText(m_BackupPath),
                        out var backup))
                {
                    return Recovered(backup);
                }
            }
            catch (IOException)
            {
                storageFailure = true;
            }
            catch (UnauthorizedAccessException)
            {
                storageFailure = true;
            }

            if (storageFailure)
            {
                return StorageUnavailable();
            }

            return primaryExists || backupExists ? Unreadable() : Missing();
        }

        private LoadSaveResult RecoverCore()
        {
            try
            {
                if (!m_Storage.Exists(m_BackupPath))
                {
                    return Missing();
                }

                return m_Serializer.TryDeserialize(
                    m_Storage.ReadAllText(m_BackupPath),
                    out var backup)
                    ? Recovered(backup)
                    : Unreadable();
            }
            catch (IOException)
            {
                return StorageUnavailable();
            }
            catch (UnauthorizedAccessException)
            {
                return StorageUnavailable();
            }
        }

        private static LoadSaveResult Missing() => new LoadSaveResult(
            LoadSaveStatus.Missing,
            null,
            MissingMessage);

        private static LoadSaveResult Loaded(GameSave save) => new LoadSaveResult(
            LoadSaveStatus.LoadedPrimary,
            save,
            LoadedMessage);

        private static LoadSaveResult Recovered(GameSave save) => new LoadSaveResult(
            LoadSaveStatus.RecoveredBackup,
            save,
            RecoveredMessage);

        private static LoadSaveResult Unreadable() => new LoadSaveResult(
            LoadSaveStatus.Unreadable,
            null,
            UnreadableMessage);

        private static LoadSaveResult StorageUnavailable() => new LoadSaveResult(
            LoadSaveStatus.StorageUnavailable,
            null,
            StorageUnavailableMessage);

        private static string GetDefaultPath()
        {
#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "Library",
                "JustSomeStars",
                "Local",
                "jss-save.json"));
#else
            return Path.Combine(Application.persistentDataPath, "jss-save.json");
#endif
        }
    }
}
