using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Core;
using UnityEngine;

namespace JustSomeStars.Runtime.Accessibility
{
    internal interface ISettingsStorage
    {
        bool TryRead(string path, out string document);

        void WriteAtomically(string path, string document);
    }

    internal sealed class FileSettingsStorage : ISettingsStorage
    {
        public bool TryRead(string path, out string document)
        {
            if (!File.Exists(path))
            {
                document = null;
                return false;
            }

            document = File.ReadAllText(path);
            return true;
        }

        public void WriteAtomically(string path, string document)
        {
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException(
                    "Settings path must have a parent directory.",
                    nameof(path));
            }

            Directory.CreateDirectory(directory);
            var temporaryPath = fullPath + ".tmp";
            try
            {
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None))
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(document);
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                }

                if (File.Exists(fullPath))
                {
                    File.Replace(temporaryPath, fullPath, null);
                }
                else
                {
                    File.Move(temporaryPath, fullPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
    }

    public sealed class SettingsService : IGameService
    {
        private readonly object m_Gate = new object();
        private readonly string m_Path;
        private readonly ISettingsStorage m_Storage;

        private GameSettings m_Current = GameSettings.CreateDefaults();
        private bool m_IsInitialized;

        public SettingsService()
            : this(GetDefaultPath(), new FileSettingsStorage())
        {
        }

        public SettingsService(string path)
            : this(path, new FileSettingsStorage())
        {
        }

        internal SettingsService(string path, ISettingsStorage storage)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException(
                    "Settings path is required.",
                    nameof(path));
            }

            m_Path = Path.GetFullPath(path);
            m_Storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public event Action<GameSettings> SettingsChanged;

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

        public GameSettings Current
        {
            get
            {
                lock (m_Gate)
                {
                    return m_Current.Copy();
                }
            }
        }

        public ValueTask<StartupResult> InitializeAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (m_Gate)
            {
                if (m_IsInitialized)
                {
                    return new ValueTask<StartupResult>(StartupResult.Available());
                }

                var loaded = GameSettings.CreateDefaults();
                try
                {
                    if (m_Storage.TryRead(m_Path, out var document) &&
                        GameSettings.TryFromJson(document, out var persisted))
                    {
                        loaded = persisted;
                    }
                }
                catch (IOException)
                {
                    // Settings are optional device-local preferences. A read
                    // failure must not prevent a safe default launch.
                }
                catch (UnauthorizedAccessException)
                {
                    // Keep the complete safe defaults without claiming a write.
                }

                cancellationToken.ThrowIfCancellationRequested();
                m_Current = loaded.Copy();
                m_IsInitialized = true;
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

        public bool Apply(GameSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var candidate = settings.Copy();
            candidate.ThrowIfInvalid(nameof(settings));
            Delegate[] listeners;

            lock (m_Gate)
            {
                if (!m_IsInitialized)
                {
                    throw new InvalidOperationException(
                        "Settings must be initialized before applying changes.");
                }

                if (candidate.Equals(m_Current))
                {
                    return false;
                }

                var document = candidate.ToJson();
                m_Storage.WriteAtomically(m_Path, document);
                m_Current = candidate.Copy();
                listeners = SettingsChanged?.GetInvocationList() ?? Array.Empty<Delegate>();
            }

            foreach (var listener in listeners)
            {
                ((Action<GameSettings>)listener)(candidate.Copy());
            }

            return true;
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
                "jss-settings-v1.json"));
#else
            return Path.Combine(
                Application.persistentDataPath,
                "jss-settings-v1.json");
#endif
        }
    }
}
