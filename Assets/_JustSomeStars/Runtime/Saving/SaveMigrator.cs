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
                new ISaveMigration[]
                {
                    new SaveMigrationV1ToV2(),
                    new SaveMigrationV2ToV3(),
                });
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

        private sealed class SaveMigrationV1ToV2 : ISaveMigration
        {
            public int FromVersion => 1;
            public int ToVersion => 2;

            public string Migrate(string document)
            {
                var legacy = JsonUtility.FromJson<LegacySaveV1>(document);
                if (legacy == null || legacy.story == null || legacy.captain == null ||
                    legacy.birthday == null || legacy.metadata == null)
                {
                    throw new FormatException("Schema v1 save is incomplete.");
                }

                var migrated = GameSave.CreateNew(
                    legacy.metadata.SaveId,
                    legacy.metadata.CreatedUtcTicks);
                migrated.Story = legacy.story;
                migrated.Mission = MissionProgress.Empty();
                migrated.Captain = legacy.captain;
                migrated.DiscoveryIds = legacy.discoveryIds;
                migrated.EarnedCosmeticIds = legacy.earnedCosmeticIds;
                migrated.AtlasEntryIds = legacy.atlasEntryIds;
                migrated.Photographs = legacy.photographs;
                migrated.Birthday = legacy.birthday;
                migrated.Metadata = legacy.metadata;
                migrated.ThrowIfInvalid(nameof(document));
                migrated.SetSchemaVersionForMigration(ToVersion);
                return JsonUtility.ToJson(migrated, prettyPrint: true);
            }

            [Serializable]
            private sealed class LegacySaveV1
            {
                public StoryProgress story;
                public CaptainState captain;
                public string[] discoveryIds;
                public string[] earnedCosmeticIds;
                public string[] atlasEntryIds;
                public PhotoMetadata[] photographs;
                public BirthdayState birthday;
                public SaveMetadata metadata;
            }
        }

        private sealed class SaveMigrationV2ToV3 : ISaveMigration
        {
            public int FromVersion => 2;

            public int ToVersion => 3;

            public string Migrate(string document)
            {
                var legacy = JsonUtility.FromJson<LegacySaveV2>(document);
                if (legacy == null || legacy.story == null || legacy.mission == null ||
                    legacy.captain == null || legacy.birthday == null ||
                    legacy.metadata == null)
                {
                    throw new FormatException("Schema v2 save is incomplete.");
                }

                var migrated = GameSave.CreateNew(
                    legacy.metadata.SaveId,
                    legacy.metadata.CreatedUtcTicks);
                migrated.Story = legacy.story;
                migrated.Mission = legacy.mission;
                migrated.Captain = legacy.captain;
                migrated.DiscoveryIds = legacy.discoveryIds;
                migrated.EarnedCosmeticIds = legacy.earnedCosmeticIds;
                migrated.AtlasEntryIds = legacy.atlasEntryIds;
                migrated.Photographs = legacy.photographs;
                migrated.Birthday = new BirthdayState
                {
                    HasValue = legacy.birthday.HasValue,
                    Day = legacy.birthday.Day,
                    Month = legacy.birthday.Month,
                    Year = legacy.birthday.Year,
                    CorrectionCount = 0,
                    LastBirthdayGiftYear = legacy.birthday.LastBirthdayGiftYear,
                };
                migrated.Metadata = legacy.metadata;
                migrated.ThrowIfInvalid(nameof(document));
                return JsonUtility.ToJson(migrated, prettyPrint: true);
            }

            [Serializable]
            private sealed class LegacySaveV2
            {
                public StoryProgress story;
                public MissionProgress mission;
                public CaptainState captain;
                public string[] discoveryIds;
                public string[] earnedCosmeticIds;
                public string[] atlasEntryIds;
                public PhotoMetadata[] photographs;
                public BirthdayState birthday;
                public SaveMetadata metadata;
            }
        }
    }
}
