using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JustSomeStars.Runtime.Core
{
    [CreateAssetMenu(
        fileName = "AudioCueLibrary",
        menuName = "Just Some Stars/Audio/Cue Library")]
    public sealed class AudioCueLibrary : ScriptableObject
    {
        [SerializeField] private AudioCueDefinition[] cues =
            Array.Empty<AudioCueDefinition>();
        [SerializeField] private MusicStateDefinition[] musicStates =
            Array.Empty<MusicStateDefinition>();
        [SerializeField] private string[] explicitlyUnvoiced = Array.Empty<string>();

        public IReadOnlyList<AudioCueDefinition> Cues => cues;
        public IReadOnlyList<MusicStateDefinition> MusicStates => musicStates;
        public IReadOnlyList<string> ExplicitlyUnvoiced => explicitlyUnvoiced;

        public void Configure(
            AudioCueDefinition[] authoredCues,
            MusicStateDefinition[] authoredMusicStates,
            string[] unvoicedReferences = null)
        {
            cues = authoredCues != null
                ? (AudioCueDefinition[])authoredCues.Clone()
                : null;
            musicStates = authoredMusicStates != null
                ? (MusicStateDefinition[])authoredMusicStates.Clone()
                : null;
            explicitlyUnvoiced = unvoicedReferences != null
                ? (string[])unvoicedReferences.Clone()
                : Array.Empty<string>();
            ValidateOrThrow();
        }

        public AudioCueDefinition FindCue(string id)
        {
            if (!TryFindCue(id, out var cue))
                throw new KeyNotFoundException($"Audio cue '{id}' is not authored.");
            return cue;
        }

        public bool TryFindCue(string id, out AudioCueDefinition cue)
        {
            cue = cues?.SingleOrDefault(candidate => candidate != null &&
                string.Equals(candidate.StableId, id, StringComparison.Ordinal));
            return cue != null;
        }

        public MusicStateDefinition FindMusicState(string id)
        {
            var state = musicStates?.SingleOrDefault(candidate => candidate != null &&
                string.Equals(candidate.StableId, id, StringComparison.Ordinal));
            return state ?? throw new KeyNotFoundException(
                $"Music state '{id}' is not authored.");
        }

        public bool IsExplicitlyUnvoiced(string voiceReference) =>
            explicitlyUnvoiced != null && explicitlyUnvoiced.Contains(
                voiceReference,
                StringComparer.Ordinal);

        public void ValidateOrThrow()
        {
            if (cues == null || cues.Length == 0 || cues.Any(cue => cue == null) ||
                musicStates == null || musicStates.Length == 0 ||
                musicStates.Any(state => state == null) || explicitlyUnvoiced == null)
            {
                throw new InvalidOperationException(
                    "Audio cue library requires authored cues and music states.");
            }

            foreach (var cue in cues) cue.ValidateOrThrow();
            foreach (var state in musicStates) state.ValidateOrThrow();
            if (cues.Select(cue => cue.StableId).Distinct(StringComparer.Ordinal).Count() !=
                cues.Length ||
                musicStates.Select(state => state.StableId)
                    .Distinct(StringComparer.Ordinal).Count() != musicStates.Length ||
                explicitlyUnvoiced.Any(value => string.IsNullOrWhiteSpace(value) ||
                    !string.Equals(value, value.Trim(), StringComparison.Ordinal)) ||
                explicitlyUnvoiced.Distinct(StringComparer.Ordinal).Count() !=
                    explicitlyUnvoiced.Length ||
                explicitlyUnvoiced.Any(value => cues.Any(cue => string.Equals(
                    cue.StableId,
                    value,
                    StringComparison.Ordinal))))
            {
                throw new InvalidOperationException(
                    "Audio cue and music-state ids must be unique.");
            }

            foreach (var state in musicStates)
            {
                var foundation = FindCue(state.FoundationCueId);
                var signal = FindCue(state.SignalStemCueId);
                if (foundation.Bus != AudioBus.Music || signal.Bus != AudioBus.Music ||
                    !foundation.Loop || !signal.Loop ||
                    foundation.Clip.samples != signal.Clip.samples ||
                    foundation.Clip.channels != signal.Clip.channels ||
                    foundation.Clip.frequency != signal.Clip.frequency)
                {
                    throw new InvalidOperationException(
                        $"Music state '{state.StableId}' requires aligned looping stems.");
                }
            }
        }
    }
}
