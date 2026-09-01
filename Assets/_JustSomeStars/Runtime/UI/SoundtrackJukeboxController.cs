using System;
using System.Collections.Generic;
using JustSomeStars.Runtime.Cosmetics;
using UnityEngine;

namespace JustSomeStars.Runtime.UI
{
    public sealed class SoundtrackTrack
    {
        public SoundtrackTrack(string id, string title, string cueId)
        {
            Id = id;
            Title = title;
            CueId = cueId;
        }

        public string Id { get; }
        public string Title { get; }
        public string CueId { get; }
    }

    public interface ISoundtrackPlayer
    {
        bool Play(SoundtrackTrack track);
        void Stop();
    }

    public sealed class UnitySoundtrackPlayer : MonoBehaviour, ISoundtrackPlayer
    {
        [SerializeField] private AudioSource output;

        public bool Play(SoundtrackTrack track)
        {
            if (track == null || output == null)
            {
                return false;
            }

            var clip = Resources.Load<AudioClip>(track.CueId);
            if (clip == null)
            {
                return false;
            }

            output.clip = clip;
            output.loop = true;
            output.Play();
            return true;
        }

        public void Stop()
        {
            if (output == null)
            {
                return;
            }

            output.Stop();
            output.clip = null;
        }
    }

    public sealed class SoundtrackJukeboxController
    {
        private static readonly IReadOnlyDictionary<string, SoundtrackTrack> Tracks =
            new Dictionary<string, SoundtrackTrack>(StringComparer.Ordinal)
            {
                ["music.clubhouse-before-dinner"] = new(
                    "music.clubhouse-before-dinner", "Before Dinner", "cue.clubhouse.before-dinner"),
                ["music.mirra-warm-cold-horizon"] = new(
                    "music.mirra-warm-cold-horizon", "Warm / Cold Horizon", "cue.mirra.horizon"),
                ["music.koro-vesper-orbit"] = new(
                    "music.koro-vesper-orbit", "Koro and Vesper", "cue.koro-vesper.orbit"),
                ["music.aster-veil-signal"] = new(
                    "music.aster-veil-signal", "Signal Through the Veil", "cue.aster-veil.signal"),
                ["music.dinner-homecoming"] = new(
                    "music.dinner-homecoming", "Home Before the Stars", "cue.dinner.homecoming"),
            };

        private readonly EditionFeatureService m_Editions;
        private readonly ISoundtrackPlayer m_Player;

        public SoundtrackJukeboxController(
            EditionFeatureService editions,
            ISoundtrackPlayer player)
        {
            m_Editions = editions ?? throw new ArgumentNullException(nameof(editions));
            m_Player = player ?? throw new ArgumentNullException(nameof(player));
        }

        public string SelectedTrackId { get; private set; } = string.Empty;
        public IEnumerable<SoundtrackTrack> AvailableTracks => Tracks.Values;

        public bool Select(string trackId)
        {
            if (!m_Editions.IsAvailable(EditionFeature.SoundtrackJukebox) ||
                string.IsNullOrWhiteSpace(trackId) ||
                !Tracks.TryGetValue(trackId, out var track) ||
                !m_Player.Play(track))
            {
                return false;
            }

            SelectedTrackId = trackId;
            return true;
        }

        public void Stop()
        {
            m_Player.Stop();
            SelectedTrackId = string.Empty;
        }
    }
}
