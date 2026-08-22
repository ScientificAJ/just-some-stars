using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JustSomeStars.Runtime.Saving
{
    internal interface ISaveMigration
    {
        int FromVersion { get; }

        int ToVersion { get; }

        string Migrate(string document);
    }

    internal sealed class SaveMigrator
    {
        private readonly IReadOnlyDictionary<int, ISaveMigration> m_Steps;

        internal SaveMigrator(
            int targetVersion,
            IEnumerable<ISaveMigration> steps)
        {
            if (targetVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(targetVersion));
            }

            TargetVersion = targetVersion;
            var candidates = (steps ?? throw new ArgumentNullException(nameof(steps)))
                .ToArray();
            if (candidates.Any(step =>
                    step == null ||
                    step.FromVersion < 1 ||
                    step.ToVersion != step.FromVersion + 1 ||
                    step.ToVersion > targetVersion))
            {
                throw new ArgumentException(
                    "Save migrations must be non-null forward one schema at a time.",
                    nameof(steps));
            }

            var byVersion = new Dictionary<int, ISaveMigration>();
            foreach (var step in candidates)
            {
                if (!byVersion.TryAdd(step.FromVersion, step))
                {
                    throw new ArgumentException(
                        "Only one migration may start from each schema version.",
                        nameof(steps));
                }
            }

            for (var version = 1; version < targetVersion; version++)
            {
                if (!byVersion.ContainsKey(version))
                {
                    throw new ArgumentException(
                        "Migration registry must be contiguous through its target version.",
                        nameof(steps));
                }
            }

            if (byVersion.Count != Math.Max(0, targetVersion - 1))
            {
                throw new ArgumentException(
                    "Migration registry contains steps outside its target chain.",
                    nameof(steps));
            }

            m_Steps = byVersion;
        }

        internal int TargetVersion { get; }

        internal int RegisteredStepCount => m_Steps.Count;

        internal static SaveMigrator CreateCurrent()
        {
            return new SaveMigrator(
                GameSave.CurrentSchemaVersion,
                Array.Empty<ISaveMigration>());
        }

        internal bool TryMigrate(string document, out string migratedDocument)
        {
            migratedDocument = null;
            if (!TryReadSchemaVersion(document, out var version) ||
                version < 1 ||
                version > TargetVersion)
            {
                return false;
            }

            var candidate = document;
            while (version < TargetVersion)
            {
                if (!m_Steps.TryGetValue(version, out var step))
                {
                    return false;
                }

                try
                {
                    candidate = step.Migrate(candidate);
                }
                catch (Exception exception) when (
                    exception is ArgumentException ||
                    exception is InvalidOperationException ||
                    exception is FormatException)
                {
                    return false;
                }

                if (!TryReadSchemaVersion(candidate, out var outputVersion) ||
                    outputVersion != step.ToVersion)
                {
                    return false;
                }

                version = outputVersion;
            }

            migratedDocument = candidate;
            return true;
        }

        private static bool TryReadSchemaVersion(string document, out int version)
        {
            version = 0;
            if (string.IsNullOrWhiteSpace(document) ||
                document.IndexOf("\"schemaVersion\"", StringComparison.Ordinal) < 0)
            {
                return false;
            }

            try
            {
                var envelope = JsonUtility.FromJson<SchemaEnvelope>(document);
                if (envelope == null)
                {
                    return false;
                }

                version = envelope.schemaVersion;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        [Serializable]
        private sealed class SchemaEnvelope
        {
            public int schemaVersion;
        }
    }
}
