using System;
using System.Collections.Generic;
using System.Linq;

namespace JustSomeStars.Runtime.Saving
{
    public enum SaveMergeConflictKind
    {
        StoryCheckpoint = 0,
        CaptainCustomization = 1,
        Birthday = 2,
    }

    public sealed class SaveMergeConflictException : InvalidOperationException
    {
        public SaveMergeConflictException(
            SaveMergeConflictKind kind,
            string detail)
            : base(
                $"Save merge needs player choice for incompatible {kind}: {detail}")
        {
            Kind = kind;
        }

        public SaveMergeConflictKind Kind { get; }
    }

    public static class SaveMerge
    {
        public static GameSave Combine(GameSave local, GameSave cloud)
        {
            if (local == null)
            {
                throw new ArgumentNullException(nameof(local));
            }

            if (cloud == null)
            {
                throw new ArgumentNullException(nameof(cloud));
            }

            local.ThrowIfInvalid(nameof(local));
            cloud.ThrowIfInvalid(nameof(cloud));

            var merged = local.Copy();
            merged.Story = MergeStory(local.Story, cloud.Story);
            merged.Captain = MergeCaptain(local.Captain, cloud.Captain);
            merged.DiscoveryIds = Union(local.DiscoveryIds, cloud.DiscoveryIds);
            merged.EarnedCosmeticIds = Union(
                local.EarnedCosmeticIds,
                cloud.EarnedCosmeticIds);
            merged.AtlasEntryIds = Union(local.AtlasEntryIds, cloud.AtlasEntryIds);
            merged.Photographs = local.Photographs
                .Select(photo => photo.Copy())
                .ToArray();
            merged.Birthday = MergeBirthday(local.Birthday, cloud.Birthday);
            merged.Metadata = new SaveMetadata
            {
                SaveId = local.Metadata.SaveId,
                Revision = Increment(Math.Max(
                    local.Metadata.Revision,
                    cloud.Metadata.Revision)),
                CreatedUtcTicks = Math.Min(
                    local.Metadata.CreatedUtcTicks,
                    cloud.Metadata.CreatedUtcTicks),
                UpdatedUtcTicks = Increment(Math.Max(
                    local.Metadata.UpdatedUtcTicks,
                    cloud.Metadata.UpdatedUtcTicks)),
            };
            merged.ThrowIfInvalid(nameof(merged));
            return merged;
        }

        private static StoryProgress MergeStory(
            StoryProgress local,
            StoryProgress cloud)
        {
            if (local.CheckpointOrdinal > cloud.CheckpointOrdinal)
            {
                return local.Copy();
            }

            if (cloud.CheckpointOrdinal > local.CheckpointOrdinal)
            {
                return cloud.Copy();
            }

            if (!string.Equals(
                    local.CheckpointId,
                    cloud.CheckpointId,
                    StringComparison.Ordinal))
            {
                throw new SaveMergeConflictException(
                    SaveMergeConflictKind.StoryCheckpoint,
                    "equal checkpoint ordinals have different identities.");
            }

            return local.Copy();
        }

        private static CaptainState MergeCaptain(
            CaptainState local,
            CaptainState cloud)
        {
            if (local.LastCustomizedUtcTicks > cloud.LastCustomizedUtcTicks)
            {
                return local.Copy();
            }

            if (cloud.LastCustomizedUtcTicks > local.LastCustomizedUtcTicks)
            {
                return cloud.Copy();
            }

            if (!local.Equals(cloud))
            {
                throw new SaveMergeConflictException(
                    SaveMergeConflictKind.CaptainCustomization,
                    "equal edit timestamps describe different appearances.");
            }

            return local.Copy();
        }

        private static BirthdayState MergeBirthday(
            BirthdayState local,
            BirthdayState cloud)
        {
            if (!local.HasValue)
            {
                return cloud.Copy();
            }

            if (!cloud.HasValue)
            {
                return local.Copy();
            }

            if (!local.HasSameDate(cloud))
            {
                throw new SaveMergeConflictException(
                    SaveMergeConflictKind.Birthday,
                    "private birthday dates disagree.");
            }

            var merged = local.Copy();
            merged.LastBirthdayGiftYear = Math.Max(
                local.LastBirthdayGiftYear,
                cloud.LastBirthdayGiftYear);
            return merged;
        }

        private static string[] Union(
            IEnumerable<string> local,
            IEnumerable<string> cloud)
        {
            return local.Concat(cloud)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static long Increment(long value)
        {
            if (value == long.MaxValue)
            {
                throw new InvalidOperationException(
                    "Save metadata cannot advance beyond Int64.MaxValue.");
            }

            return value + 1;
        }
    }
}
