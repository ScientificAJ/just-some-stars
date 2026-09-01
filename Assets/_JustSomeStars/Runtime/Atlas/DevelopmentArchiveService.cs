using System;
using System.Collections.Generic;
using JustSomeStars.Runtime.Cosmetics;

namespace JustSomeStars.Runtime.Atlas
{
    public sealed class DevelopmentArchiveEntry
    {
        public DevelopmentArchiveEntry(
            string id,
            string title,
            string missionId,
            string scienceNote,
            string artProcessNote)
        {
            Id = id;
            Title = title;
            MissionId = missionId;
            ScienceNote = scienceNote;
            ArtProcessNote = artProcessNote;
        }

        public string Id { get; }
        public string Title { get; }
        public string MissionId { get; }
        public string ScienceNote { get; }
        public string ArtProcessNote { get; }
    }

    public sealed class DevelopmentArchiveService
    {
        private static readonly DevelopmentArchiveEntry[] Entries =
        {
            new("archive.mirra-lighting-study", "A Horizon in Two Temperatures",
                "mission.mirra.chapter-one",
                "Mirra's sunrise and night field teach that light colour records both source temperature and atmosphere.",
                "The scene was separated into warm and cool painted bands so parallax could preserve the horizon split."),
            new("archive.koro-spectra-notes", "Reading a Star by Its Missing Colours",
                "mission.koro-vesper.chapter-one",
                "Absorption lines reveal the elements above a star's bright photosphere.",
                "The Lens spectrum was simplified into broad, readable bands before the fine line overlay was authored."),
            new("archive.vesper-geyser-timing", "Vesper's Returning Plumes",
                "mission.koro-vesper.chapter-one",
                "A repeating plume becomes a clock when the moon's orbit and tidal stresses remain stable.",
                "The geyser loop was timed from gameplay contacts first, then painted effects were fitted to those events."),
            new("archive.aster-debris-simulation", "Moving Together Through the Veil",
                "mission.aster-veil.chapter-one",
                "Relative motion explains why nearby debris can appear still while both objects travel quickly.",
                "The debris lanes were blocked as safe readable bands before the foreground silhouettes were painted."),
            new("archive.signal-motif-sketches", "Three Fragments, One Signal",
                "mission.aster-veil.chapter-one",
                "Repeated signal shapes let the crew compare observations from different worlds.",
                "The circular cyan motif was kept consistent across Lens, ship, fragment and clubhouse compositions."),
            new("archive.ori-prototype-journal", "Ori Learns to Listen",
                "mission.mirra.chapter-one",
                "A sensor is useful because it turns an invisible change into a record the crew can compare.",
                "Ori's eye, antenna and wheel silhouettes were iterated as separate readable animation layers."),
        };

        private readonly EditionFeatureService m_Editions;

        public DevelopmentArchiveService(EditionFeatureService editions)
        {
            m_Editions = editions ?? throw new ArgumentNullException(nameof(editions));
        }

        public IReadOnlyList<DevelopmentArchiveEntry> AvailableEntries =>
            m_Editions.IsAvailable(EditionFeature.DevelopmentArchive)
                ? Entries
                : Array.Empty<DevelopmentArchiveEntry>();
    }
}
