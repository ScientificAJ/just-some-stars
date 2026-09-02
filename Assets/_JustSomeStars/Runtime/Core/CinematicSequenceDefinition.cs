using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Video;

namespace JustSomeStars.Runtime.Core
{
    [CreateAssetMenu(
        fileName = "CinematicSequence",
        menuName = "Just Some Stars/Cinematics/Sequence")]
    public sealed class CinematicSequenceDefinition : ScriptableObject
    {
        [SerializeField] private string stableId;
        [SerializeField] private VideoClip optionalVideo;
        [SerializeField] private Sprite fallbackStill;
        [SerializeField] private CinematicBeatDefinition[] beats =
            Array.Empty<CinematicBeatDefinition>();

        public string StableId => stableId;
        public VideoClip OptionalVideo => optionalVideo;
        public Sprite FallbackStill => fallbackStill;
        public IReadOnlyList<CinematicBeatDefinition> Beats => beats;
        public float TotalDuration => beats == null || beats.Length == 0
            ? 0f
            : beats.Max(beat => beat.StartSeconds + beat.DurationSeconds);

        public void Configure(
            string id,
            VideoClip video,
            Sprite still,
            CinematicBeatDefinition[] authoredBeats)
        {
            stableId = id;
            optionalVideo = video;
            fallbackStill = still;
            beats = authoredBeats != null
                ? authoredBeats.OrderBy(beat => beat?.StartSeconds ?? float.MaxValue)
                    .ThenBy(beat => BeatOrder(
                        beat?.Kind ?? CinematicBeatKind.InteractionRelease))
                    .ThenBy(beat => beat?.ActorId, StringComparer.Ordinal)
                    .ThenBy(beat => beat?.Value, StringComparer.Ordinal)
                    .ToArray()
                : null;
            ValidateOrThrow();
        }

        public void ValidateOrThrow()
        {
            if (string.IsNullOrWhiteSpace(stableId) ||
                !string.Equals(stableId, stableId.Trim(), StringComparison.Ordinal) ||
                fallbackStill == null || beats == null || beats.Length == 0 ||
                beats.Any(beat => beat == null))
            {
                throw new InvalidOperationException(
                    "Cinematic sequence requires an id, fallback still and authored beats.");
            }
            foreach (var beat in beats) beat.ValidateOrThrow();
            if (beats.GroupBy(beat => (
                    beat.StartSeconds,
                    beat.Kind,
                    beat.ActorId,
                    beat.Value)).Any(group => group.Count() > 1))
            {
                throw new InvalidOperationException(
                    $"Cinematic sequence '{stableId}' has duplicate beats.");
            }
            var bodyDriven = beats.Any(beat =>
                beat.Kind == CinematicBeatKind.BodyClip);
            var directReleaseCount = beats.Count(beat =>
                beat.Kind == CinematicBeatKind.InteractionRelease);
            if ((!bodyDriven && directReleaseCount != 1) ||
                (bodyDriven && directReleaseCount > 1))
            {
                throw new InvalidOperationException(
                    $"Cinematic sequence '{stableId}' requires either one direct " +
                    "release or a body performance that emits its release event.");
            }
        }

        private static int BeatOrder(CinematicBeatKind kind) => kind switch
        {
            CinematicBeatKind.BodyClip => 0,
            CinematicBeatKind.Expression => 1,
            CinematicBeatKind.Viseme => 2,
            CinematicBeatKind.Audio => 3,
            CinematicBeatKind.Vfx => 4,
            CinematicBeatKind.Caption => 5,
            CinematicBeatKind.InteractionRelease => 6,
            _ => int.MaxValue,
        };
    }
}
