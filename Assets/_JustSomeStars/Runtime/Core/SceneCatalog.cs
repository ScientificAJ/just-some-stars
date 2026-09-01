using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace JustSomeStars.Runtime.Core
{
    [Serializable]
    public sealed class SceneCatalogEntry
    {
        [SerializeField]
        private string m_DestinationId;

        [SerializeField]
        private string m_Address;

        [SerializeField]
        private GameMode m_TargetMode;

        public SceneCatalogEntry(
            string destinationId,
            string address,
            GameMode targetMode)
        {
            m_DestinationId = destinationId;
            m_Address = address;
            m_TargetMode = targetMode;
        }

        public string DestinationId => m_DestinationId;

        public string Address => m_Address;

        public GameMode TargetMode => m_TargetMode;
    }

    [CreateAssetMenu(
        fileName = "SceneCatalog",
        menuName = "Just Some Stars/Scene Catalog")]
    public sealed class SceneCatalog : ScriptableObject
    {
        public const int CurrentSchemaVersion = 1;
        public const string AddressablesKey = "jss.scene-catalog";

        [SerializeField]
        private int m_SchemaVersion = CurrentSchemaVersion;

        [SerializeField]
        private string m_FallbackSceneName = "Frontend";

        [SerializeField]
        private GameMode m_FallbackMode = GameMode.Frontend;

        [SerializeField]
        private SceneCatalogEntry[] m_Entries = Array.Empty<SceneCatalogEntry>();

        public int SchemaVersion => m_SchemaVersion;

        public string FallbackSceneName => m_FallbackSceneName;

        public GameMode FallbackMode => m_FallbackMode;

        public IReadOnlyList<SceneCatalogEntry> Entries =>
            new ReadOnlyCollection<SceneCatalogEntry>(
                m_Entries ?? Array.Empty<SceneCatalogEntry>());

        internal static SceneCatalog CreateForTests(
            int schemaVersion,
            string fallbackSceneName,
            GameMode fallbackMode,
            params SceneCatalogEntry[] entries)
        {
            var catalog = CreateInstance<SceneCatalog>();
            catalog.ConfigureForTests(
                schemaVersion,
                fallbackSceneName,
                fallbackMode,
                entries);
            return catalog;
        }

        internal void ConfigureForTests(
            int schemaVersion,
            string fallbackSceneName,
            GameMode fallbackMode,
            SceneCatalogEntry[] entries)
        {
            m_SchemaVersion = schemaVersion;
            m_FallbackSceneName = fallbackSceneName;
            m_FallbackMode = fallbackMode;
            m_Entries = entries ?? Array.Empty<SceneCatalogEntry>();
        }

        public void Validate()
        {
            if (m_SchemaVersion != CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    $"Scene catalog schema '{m_SchemaVersion}' is unsupported; " +
                    $"expected '{CurrentSchemaVersion}'.");
            }

            RequireTrimmedValue(m_FallbackSceneName, "fallback scene name");
            if (!Enum.IsDefined(typeof(GameMode), m_FallbackMode))
            {
                throw new InvalidOperationException(
                    $"Scene catalog fallback mode '{m_FallbackMode}' is invalid.");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var addresses = new HashSet<string>(StringComparer.Ordinal);
            var entries = m_Entries ?? Array.Empty<SceneCatalogEntry>();
            for (var index = 0; index < entries.Length; index++)
            {
                var entry = entries[index] ?? throw new InvalidOperationException(
                    $"Scene catalog entry {index} is null.");
                RequireTrimmedValue(
                    entry.DestinationId,
                    $"entry {index} destination ID");
                RequireTrimmedValue(
                    entry.Address,
                    $"entry {index} Addressables address");
                if (!Enum.IsDefined(typeof(GameMode), entry.TargetMode))
                {
                    throw new InvalidOperationException(
                        $"Scene catalog entry '{entry.DestinationId}' has invalid " +
                        $"target mode '{entry.TargetMode}'.");
                }

                if (!ids.Add(entry.DestinationId))
                {
                    throw new InvalidOperationException(
                        $"Scene catalog destination ID '{entry.DestinationId}' " +
                        "is duplicated.");
                }

                if (!addresses.Add(entry.Address))
                {
                    throw new InvalidOperationException(
                        $"Scene catalog Addressables address '{entry.Address}' " +
                        "is duplicated.");
                }
            }
        }

        public bool TryGetEntry(
            string destinationId,
            out SceneCatalogEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(destinationId) ||
                !string.Equals(
                    destinationId,
                    destinationId.Trim(),
                    StringComparison.Ordinal))
            {
                return false;
            }

            var entries = m_Entries ?? Array.Empty<SceneCatalogEntry>();
            foreach (var candidate in entries)
            {
                if (candidate != null &&
                    string.Equals(
                        candidate.DestinationId,
                        destinationId,
                        StringComparison.Ordinal))
                {
                    entry = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool TryResolveEntry(
            string destinationIdOrAddress,
            out SceneCatalogEntry entry)
        {
            if (TryGetEntry(destinationIdOrAddress, out entry))
            {
                return true;
            }
            if (string.IsNullOrWhiteSpace(destinationIdOrAddress) ||
                !string.Equals(
                    destinationIdOrAddress,
                    destinationIdOrAddress.Trim(),
                    StringComparison.Ordinal))
            {
                entry = null;
                return false;
            }

            foreach (var candidate in m_Entries ?? Array.Empty<SceneCatalogEntry>())
            {
                if (candidate != null && string.Equals(
                        candidate.Address,
                        destinationIdOrAddress,
                        StringComparison.Ordinal))
                {
                    entry = candidate;
                    return true;
                }
            }

            entry = null;
            return false;
        }

        private static void RequireTrimmedValue(string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Scene catalog {label} must be non-empty and already trimmed.");
            }
        }
    }
}
