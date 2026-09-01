using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace JustSomeStars.Runtime.Saving
{
    [Serializable]
    public sealed class StoryProgress : IEquatable<StoryProgress>
    {
        [SerializeField] private string checkpointId = "story.prologue.start";
        [SerializeField] private int checkpointOrdinal;

        public string CheckpointId
        {
            get => checkpointId;
            set => checkpointId = value;
        }

        public int CheckpointOrdinal
        {
            get => checkpointOrdinal;
            set => checkpointOrdinal = value;
        }

        public StoryProgress Copy()
        {
            return new StoryProgress
            {
                checkpointId = checkpointId,
                checkpointOrdinal = checkpointOrdinal,
            };
        }

        public bool Equals(StoryProgress other)
        {
            return other != null &&
                string.Equals(checkpointId, other.checkpointId, StringComparison.Ordinal) &&
                checkpointOrdinal == other.checkpointOrdinal;
        }

        public override bool Equals(object obj) => Equals(obj as StoryProgress);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((checkpointId != null ? checkpointId.GetHashCode() : 0) * 397) ^
                    checkpointOrdinal;
            }
        }
    }

    public enum ChapterOnePhase
    {
        NotStarted = 0,
        OpeningComplete = 1,
        MirraComplete = 2,
        KoroComplete = 3,
        AsterFragmentRecovered = 4,
        SignalReassembled = 5,
        ReturnedHome = 6,
        DinnerComplete = 7,
    }

    [Serializable]
    public sealed class ChapterOneProgress : IEquatable<ChapterOneProgress>
    {
        [SerializeField] private ChapterOnePhase phase;
        [SerializeField] private bool starMapRevealed;
        [SerializeField] private bool finalPulseSeen;

        public ChapterOnePhase Phase
        {
            get => phase;
            set => phase = value;
        }

        public bool StarMapRevealed
        {
            get => starMapRevealed;
            set => starMapRevealed = value;
        }

        public bool FinalPulseSeen
        {
            get => finalPulseSeen;
            set => finalPulseSeen = value;
        }

        public bool CreditsUnlocked =>
            phase == ChapterOnePhase.DinnerComplete &&
            starMapRevealed &&
            finalPulseSeen;

        public ChapterOneProgress Copy()
        {
            return new ChapterOneProgress
            {
                phase = phase,
                starMapRevealed = starMapRevealed,
                finalPulseSeen = finalPulseSeen,
            };
        }

        public bool Equals(ChapterOneProgress other)
        {
            return other != null &&
                phase == other.phase &&
                starMapRevealed == other.starMapRevealed &&
                finalPulseSeen == other.finalPulseSeen;
        }

        public override bool Equals(object obj) => Equals(obj as ChapterOneProgress);

        public override int GetHashCode()
        {
            unchecked
            {
                return (((int)phase * 397) ^ starMapRevealed.GetHashCode()) * 397 ^
                    finalPulseSeen.GetHashCode();
            }
        }

        internal void ThrowIfInvalid(string parameterName)
        {
            if (!Enum.IsDefined(typeof(ChapterOnePhase), phase))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Chapter One phase is not authored.");
            }

            if (phase < ChapterOnePhase.SignalReassembled && starMapRevealed)
            {
                throw new ArgumentException(
                    "The star map cannot be revealed before Signal reconstruction.",
                    parameterName);
            }

            if (phase >= ChapterOnePhase.SignalReassembled && !starMapRevealed)
            {
                throw new ArgumentException(
                    "Signal reconstruction must reveal the star map.",
                    parameterName);
            }

            if (phase != ChapterOnePhase.DinnerComplete && finalPulseSeen)
            {
                throw new ArgumentException(
                    "The final pulse cannot be recorded before dinner completes.",
                    parameterName);
            }

            if (phase == ChapterOnePhase.DinnerComplete && !finalPulseSeen)
            {
                throw new ArgumentException(
                    "Dinner completion requires the final Ori/fragment pulse.",
                    parameterName);
            }
        }
    }

    [Serializable]
    public sealed class CaptainState : IEquatable<CaptainState>
    {
        [SerializeField] private string bodyFamilyId = "captain.family.a";
        [SerializeField] private string appearancePresetId = "captain.face.01";
        [SerializeField] private string suitCosmeticId = "suit.clubhouse";
        [SerializeField] private long lastCustomizedUtcTicks;

        public string BodyFamilyId
        {
            get => bodyFamilyId;
            set => bodyFamilyId = value;
        }

        public string AppearancePresetId
        {
            get => appearancePresetId;
            set => appearancePresetId = value;
        }

        public string SuitCosmeticId
        {
            get => suitCosmeticId;
            set => suitCosmeticId = value;
        }

        public long LastCustomizedUtcTicks
        {
            get => lastCustomizedUtcTicks;
            set => lastCustomizedUtcTicks = value;
        }

        public CaptainState Copy()
        {
            return new CaptainState
            {
                bodyFamilyId = bodyFamilyId,
                appearancePresetId = appearancePresetId,
                suitCosmeticId = suitCosmeticId,
                lastCustomizedUtcTicks = lastCustomizedUtcTicks,
            };
        }

        public bool Equals(CaptainState other)
        {
            return other != null &&
                string.Equals(bodyFamilyId, other.bodyFamilyId, StringComparison.Ordinal) &&
                string.Equals(
                    appearancePresetId,
                    other.appearancePresetId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    suitCosmeticId,
                    other.suitCosmeticId,
                    StringComparison.Ordinal) &&
                lastCustomizedUtcTicks == other.lastCustomizedUtcTicks;
        }

        public override bool Equals(object obj) => Equals(obj as CaptainState);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = bodyFamilyId != null ? bodyFamilyId.GetHashCode() : 0;
                hash = (hash * 397) ^
                    (appearancePresetId != null ? appearancePresetId.GetHashCode() : 0);
                hash = (hash * 397) ^
                    (suitCosmeticId != null ? suitCosmeticId.GetHashCode() : 0);
                return (hash * 397) ^ lastCustomizedUtcTicks.GetHashCode();
            }
        }
    }

    [Serializable]
    public sealed class PhotoMetadata : IEquatable<PhotoMetadata>
    {
        [SerializeField] private string photoId;
        [SerializeField] private string relativePath;
        [SerializeField] private long capturedUtcTicks;

        public string PhotoId
        {
            get => photoId;
            set => photoId = value;
        }

        public string RelativePath
        {
            get => relativePath;
            set => relativePath = value;
        }

        public long CapturedUtcTicks
        {
            get => capturedUtcTicks;
            set => capturedUtcTicks = value;
        }

        public PhotoMetadata Copy()
        {
            return new PhotoMetadata
            {
                photoId = photoId,
                relativePath = relativePath,
                capturedUtcTicks = capturedUtcTicks,
            };
        }

        public bool Equals(PhotoMetadata other)
        {
            return other != null &&
                string.Equals(photoId, other.photoId, StringComparison.Ordinal) &&
                string.Equals(relativePath, other.relativePath, StringComparison.Ordinal) &&
                capturedUtcTicks == other.capturedUtcTicks;
        }

        public override bool Equals(object obj) => Equals(obj as PhotoMetadata);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = photoId != null ? photoId.GetHashCode() : 0;
                hash = (hash * 397) ^
                    (relativePath != null ? relativePath.GetHashCode() : 0);
                return (hash * 397) ^ capturedUtcTicks.GetHashCode();
            }
        }
    }

    [Serializable]
    public sealed class BirthdayState : IEquatable<BirthdayState>
    {
        [SerializeField] private bool hasValue;
        [SerializeField] private int day;
        [SerializeField] private int month;
        [SerializeField] private int year;
        [SerializeField] private int correctionCount;
        [SerializeField] private int lastBirthdayGiftYear;

        public bool HasValue
        {
            get => hasValue;
            set => hasValue = value;
        }

        public int Day
        {
            get => day;
            set => day = value;
        }

        public int Month
        {
            get => month;
            set => month = value;
        }

        public int Year
        {
            get => year;
            set => year = value;
        }

        public int CorrectionCount
        {
            get => correctionCount;
            set => correctionCount = value;
        }

        public int LastBirthdayGiftYear
        {
            get => lastBirthdayGiftYear;
            set => lastBirthdayGiftYear = value;
        }

        public BirthdayState Copy()
        {
            return new BirthdayState
            {
                hasValue = hasValue,
                day = day,
                month = month,
                year = year,
                correctionCount = correctionCount,
                lastBirthdayGiftYear = lastBirthdayGiftYear,
            };
        }

        public bool Equals(BirthdayState other)
        {
            return other != null &&
                hasValue == other.hasValue &&
                day == other.day &&
                month == other.month &&
                year == other.year &&
                correctionCount == other.correctionCount &&
                lastBirthdayGiftYear == other.lastBirthdayGiftYear;
        }

        public bool HasSameDate(BirthdayState other)
        {
            return other != null &&
                hasValue == other.hasValue &&
                (!hasValue ||
                    (day == other.day && month == other.month && year == other.year));
        }

        public override bool Equals(object obj) => Equals(obj as BirthdayState);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = hasValue.GetHashCode();
                hash = (hash * 397) ^ day;
                hash = (hash * 397) ^ month;
                hash = (hash * 397) ^ year;
                hash = (hash * 397) ^ correctionCount;
                return (hash * 397) ^ lastBirthdayGiftYear;
            }
        }
    }

    [Serializable]
    public sealed class SaveMetadata : IEquatable<SaveMetadata>
    {
        [SerializeField] private string saveId;
        [SerializeField] private long revision;
        [SerializeField] private long createdUtcTicks;
        [SerializeField] private long updatedUtcTicks;

        public string SaveId
        {
            get => saveId;
            set => saveId = value;
        }

        public long Revision
        {
            get => revision;
            set => revision = value;
        }

        public long CreatedUtcTicks
        {
            get => createdUtcTicks;
            set => createdUtcTicks = value;
        }

        public long UpdatedUtcTicks
        {
            get => updatedUtcTicks;
            set => updatedUtcTicks = value;
        }

        public SaveMetadata Copy()
        {
            return new SaveMetadata
            {
                saveId = saveId,
                revision = revision,
                createdUtcTicks = createdUtcTicks,
                updatedUtcTicks = updatedUtcTicks,
            };
        }

        public bool Equals(SaveMetadata other)
        {
            return other != null &&
                string.Equals(saveId, other.saveId, StringComparison.Ordinal) &&
                revision == other.revision &&
                createdUtcTicks == other.createdUtcTicks &&
                updatedUtcTicks == other.updatedUtcTicks;
        }

        public override bool Equals(object obj) => Equals(obj as SaveMetadata);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = saveId != null ? saveId.GetHashCode() : 0;
                hash = (hash * 397) ^ revision.GetHashCode();
                hash = (hash * 397) ^ createdUtcTicks.GetHashCode();
                return (hash * 397) ^ updatedUtcTicks.GetHashCode();
            }
        }
    }

    [Serializable]
    public sealed class MissionProgress : IEquatable<MissionProgress>
    {
        [SerializeField] private string missionId = string.Empty;
        [SerializeField] private string checkpointNodeId = string.Empty;
        [SerializeField] private int checkpointOrdinal;
        [SerializeField] private string[] completedNodeIds = Array.Empty<string>();
        [SerializeField] private string[] activeNodeIds = Array.Empty<string>();

        public string MissionId
        {
            get => missionId;
            set => missionId = value;
        }

        public string CheckpointNodeId
        {
            get => checkpointNodeId;
            set => checkpointNodeId = value;
        }

        public int CheckpointOrdinal
        {
            get => checkpointOrdinal;
            set => checkpointOrdinal = value;
        }

        public string[] CompletedNodeIds
        {
            get => completedNodeIds;
            set => completedNodeIds = value;
        }

        public string[] ActiveNodeIds
        {
            get => activeNodeIds;
            set => activeNodeIds = value;
        }

        public bool HasMission => !string.IsNullOrEmpty(missionId);

        public static MissionProgress Empty()
        {
            return new MissionProgress
            {
                missionId = string.Empty,
                checkpointNodeId = string.Empty,
                checkpointOrdinal = 0,
                completedNodeIds = Array.Empty<string>(),
                activeNodeIds = Array.Empty<string>(),
            };
        }

        public MissionProgress Copy()
        {
            return new MissionProgress
            {
                missionId = missionId,
                checkpointNodeId = checkpointNodeId,
                checkpointOrdinal = checkpointOrdinal,
                completedNodeIds = completedNodeIds?.ToArray(),
                activeNodeIds = activeNodeIds?.ToArray(),
            };
        }

        public bool Equals(MissionProgress other)
        {
            return other != null &&
                string.Equals(missionId, other.missionId, StringComparison.Ordinal) &&
                string.Equals(
                    checkpointNodeId,
                    other.checkpointNodeId,
                    StringComparison.Ordinal) &&
                checkpointOrdinal == other.checkpointOrdinal &&
                SequenceEqual(completedNodeIds, other.completedNodeIds) &&
                SequenceEqual(activeNodeIds, other.activeNodeIds);
        }

        public override bool Equals(object obj) => Equals(obj as MissionProgress);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = missionId != null ? missionId.GetHashCode() : 0;
                hash = (hash * 397) ^
                    (checkpointNodeId != null ? checkpointNodeId.GetHashCode() : 0);
                return (hash * 397) ^ checkpointOrdinal;
            }
        }

        internal void ThrowIfInvalid(string parameterName)
        {
            if (completedNodeIds == null || activeNodeIds == null)
            {
                throw new ArgumentException(
                    "Mission progress arrays cannot be missing.",
                    parameterName);
            }

            if (!HasMission)
            {
                if (!string.IsNullOrEmpty(checkpointNodeId) ||
                    checkpointOrdinal != 0 ||
                    completedNodeIds.Length != 0 ||
                    activeNodeIds.Length != 0)
                {
                    throw new ArgumentException(
                        "Empty mission progress cannot carry graph state.",
                        parameterName);
                }

                return;
            }

            RequireCanonicalId(missionId, parameterName);
            RequireCanonicalId(checkpointNodeId, parameterName);
            if (checkpointOrdinal < 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Mission checkpoint ordinal cannot be negative.");
            }

            RequireUniqueIds(completedNodeIds, parameterName);
            RequireUniqueIds(activeNodeIds, parameterName);
            if (completedNodeIds.Intersect(activeNodeIds, StringComparer.Ordinal).Any())
            {
                throw new ArgumentException(
                    "Mission nodes cannot be completed and active together.",
                    parameterName);
            }
        }

        private static void RequireUniqueIds(string[] values, string parameterName)
        {
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                RequireCanonicalId(value, parameterName);
                if (!unique.Add(value))
                {
                    throw new ArgumentException(
                        "Mission progress identifiers must be unique.",
                        parameterName);
                }
            }
        }

        private static void RequireCanonicalId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Mission progress requires canonical identifiers.",
                    parameterName);
            }
        }

        private static bool SequenceEqual(string[] first, string[] second) =>
            first != null && second != null && first.SequenceEqual(second);
    }

    [Serializable]
    public sealed class GameSave : IEquatable<GameSave>
    {
        public const int CurrentSchemaVersion = 4;

        internal static readonly string[] RequiredJsonFields =
        {
            "schemaVersion",
            "story",
            "chapterOne",
            "mission",
            "captain",
            "discoveryIds",
            "earnedCosmeticIds",
            "atlasEntryIds",
            "photographs",
            "birthday",
            "metadata",
        };

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private StoryProgress story = new StoryProgress();
        [SerializeField] private ChapterOneProgress chapterOne =
            new ChapterOneProgress();
        [SerializeField] private MissionProgress mission = MissionProgress.Empty();
        [SerializeField] private CaptainState captain = new CaptainState();
        [SerializeField] private string[] discoveryIds = Array.Empty<string>();
        [SerializeField] private string[] earnedCosmeticIds = Array.Empty<string>();
        [SerializeField] private string[] atlasEntryIds = Array.Empty<string>();
        [SerializeField] private PhotoMetadata[] photographs = Array.Empty<PhotoMetadata>();
        [SerializeField] private BirthdayState birthday = new BirthdayState();
        [SerializeField] private SaveMetadata metadata = new SaveMetadata();

        public int SchemaVersion => schemaVersion;

        internal void SetSchemaVersionForMigration(int version)
        {
            if (version < 1 || version > CurrentSchemaVersion)
            {
                throw new ArgumentOutOfRangeException(nameof(version));
            }

            schemaVersion = version;
        }

        public StoryProgress Story
        {
            get => story;
            set => story = value;
        }

        public ChapterOneProgress ChapterOne
        {
            get => chapterOne;
            set => chapterOne = value;
        }

        public MissionProgress Mission
        {
            get => mission;
            set => mission = value;
        }

        public CaptainState Captain
        {
            get => captain;
            set => captain = value;
        }

        public string[] DiscoveryIds
        {
            get => discoveryIds;
            set => discoveryIds = value;
        }

        public string[] EarnedCosmeticIds
        {
            get => earnedCosmeticIds;
            set => earnedCosmeticIds = value;
        }

        public string[] AtlasEntryIds
        {
            get => atlasEntryIds;
            set => atlasEntryIds = value;
        }

        public PhotoMetadata[] Photographs
        {
            get => photographs;
            set => photographs = value;
        }

        public BirthdayState Birthday
        {
            get => birthday;
            set => birthday = value;
        }

        public SaveMetadata Metadata
        {
            get => metadata;
            set => metadata = value;
        }

        public static GameSave CreateNew(string saveId, long createdUtcTicks)
        {
            if (string.IsNullOrWhiteSpace(saveId))
            {
                throw new ArgumentException("A stable save identifier is required.", nameof(saveId));
            }

            if (createdUtcTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(createdUtcTicks));
            }

            return new GameSave
            {
                schemaVersion = CurrentSchemaVersion,
                story = new StoryProgress(),
                chapterOne = new ChapterOneProgress(),
                mission = MissionProgress.Empty(),
                captain = new CaptainState
                {
                    LastCustomizedUtcTicks = createdUtcTicks,
                },
                discoveryIds = Array.Empty<string>(),
                earnedCosmeticIds = Array.Empty<string>(),
                atlasEntryIds = Array.Empty<string>(),
                photographs = Array.Empty<PhotoMetadata>(),
                birthday = new BirthdayState(),
                metadata = new SaveMetadata
                {
                    SaveId = saveId,
                    Revision = 0,
                    CreatedUtcTicks = createdUtcTicks,
                    UpdatedUtcTicks = createdUtcTicks,
                },
            };
        }

        public GameSave Copy()
        {
            return new GameSave
            {
                schemaVersion = schemaVersion,
                story = story?.Copy(),
                chapterOne = chapterOne?.Copy(),
                mission = mission?.Copy(),
                captain = captain?.Copy(),
                discoveryIds = discoveryIds?.ToArray(),
                earnedCosmeticIds = earnedCosmeticIds?.ToArray(),
                atlasEntryIds = atlasEntryIds?.ToArray(),
                photographs = photographs?.Select(photo => photo?.Copy()).ToArray(),
                birthday = birthday?.Copy(),
                metadata = metadata?.Copy(),
            };
        }

        public bool Equals(GameSave other)
        {
            return other != null &&
                schemaVersion == other.schemaVersion &&
                Equals(story, other.story) &&
                Equals(chapterOne, other.chapterOne) &&
                Equals(mission, other.mission) &&
                Equals(captain, other.captain) &&
                SequenceEqual(discoveryIds, other.discoveryIds) &&
                SequenceEqual(earnedCosmeticIds, other.earnedCosmeticIds) &&
                SequenceEqual(atlasEntryIds, other.atlasEntryIds) &&
                SequenceEqual(photographs, other.photographs) &&
                Equals(birthday, other.birthday) &&
                Equals(metadata, other.metadata);
        }

        public override bool Equals(object obj) => Equals(obj as GameSave);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = schemaVersion;
                hash = (hash * 397) ^ (story?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (chapterOne?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (mission?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (captain?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (birthday?.GetHashCode() ?? 0);
                return (hash * 397) ^ (metadata?.GetHashCode() ?? 0);
            }
        }

        internal void ThrowIfInvalid(string parameterName)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                throw new ArgumentException(
                    $"Save schema must be version {CurrentSchemaVersion}.",
                    parameterName);
            }

            if (story == null || chapterOne == null || mission == null || captain == null ||
                birthday == null || metadata == null)
            {
                throw new ArgumentException("Save domains cannot be missing.", parameterName);
            }

            RequireId(story.CheckpointId, nameof(StoryProgress.CheckpointId), parameterName);
            if (story.CheckpointOrdinal < 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Story checkpoint ordinal cannot be negative.");
            }

            chapterOne.ThrowIfInvalid(parameterName);

            mission.ThrowIfInvalid(parameterName);

            RequireId(captain.BodyFamilyId, nameof(CaptainState.BodyFamilyId), parameterName);
            RequireId(
                captain.AppearancePresetId,
                nameof(CaptainState.AppearancePresetId),
                parameterName);
            RequireId(
                captain.SuitCosmeticId,
                nameof(CaptainState.SuitCosmeticId),
                parameterName);
            if (captain.LastCustomizedUtcTicks < 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Captain customization timestamp cannot be negative.");
            }

            RequireUniqueIds(discoveryIds, nameof(DiscoveryIds), parameterName);
            RequireUniqueIds(earnedCosmeticIds, nameof(EarnedCosmeticIds), parameterName);
            RequireUniqueIds(atlasEntryIds, nameof(AtlasEntryIds), parameterName);
            RequirePhotos(photographs, parameterName);
            RequireBirthday(birthday, parameterName);
            RequireMetadata(metadata, parameterName);
        }

        private static void RequireBirthday(BirthdayState value, string parameterName)
        {
            if (!value.HasValue)
            {
                if (value.Day != 0 || value.Month != 0 || value.Year != 0 ||
                    value.CorrectionCount != 0 ||
                    value.LastBirthdayGiftYear != 0)
                {
                    throw new ArgumentException(
                        "An unset birthday cannot carry date or gift state.",
                        parameterName);
                }

                return;
            }

            try
            {
                _ = new DateTime(value.Year, value.Month, value.Day);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                throw new ArgumentException("Birthday must be a real calendar date.", parameterName, exception);
            }

            if (value.CorrectionCount < 0 || value.LastBirthdayGiftYear < 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Birthday correction count and gift year cannot be negative.");
            }
        }

        private static void RequireMetadata(SaveMetadata value, string parameterName)
        {
            RequireId(value.SaveId, nameof(SaveMetadata.SaveId), parameterName);
            if (value.Revision < 0 || value.CreatedUtcTicks < 0 ||
                value.UpdatedUtcTicks < value.CreatedUtcTicks)
            {
                throw new ArgumentException("Save metadata is not monotonic.", parameterName);
            }
        }

        private static void RequirePhotos(PhotoMetadata[] values, string parameterName)
        {
            if (values == null)
            {
                throw new ArgumentException("Photograph metadata cannot be missing.", parameterName);
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var photo in values)
            {
                if (photo == null)
                {
                    throw new ArgumentException("Photograph metadata cannot contain null entries.", parameterName);
                }

                RequireId(photo.PhotoId, nameof(PhotoMetadata.PhotoId), parameterName);
                if (!ids.Add(photo.PhotoId))
                {
                    throw new ArgumentException("Photograph identifiers must be unique.", parameterName);
                }

                if (string.IsNullOrWhiteSpace(photo.RelativePath) ||
                    Path.IsPathRooted(photo.RelativePath) ||
                    photo.RelativePath.Split('/', '\\').Any(segment => segment == ".."))
                {
                    throw new ArgumentException(
                        "Photograph paths must be safe relative paths.",
                        parameterName);
                }

                if (photo.CapturedUtcTicks < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        parameterName,
                        "Photograph timestamps cannot be negative.");
                }
            }
        }

        private static void RequireUniqueIds(
            string[] values,
            string fieldName,
            string parameterName)
        {
            if (values == null)
            {
                throw new ArgumentException($"{fieldName} cannot be missing.", parameterName);
            }

            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                RequireId(value, fieldName, parameterName);
                if (!unique.Add(value))
                {
                    throw new ArgumentException($"{fieldName} cannot contain duplicates.", parameterName);
                }
            }
        }

        private static void RequireId(string value, string fieldName, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"{fieldName} is required.", parameterName);
            }
        }

        private static bool SequenceEqual<T>(T[] first, T[] second)
        {
            return first != null && second != null && first.SequenceEqual(second);
        }
    }
}
