using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JustSomeStars.Runtime.Animation2D
{
    [CreateAssetMenu(
        fileName = "CharacterSpriteSet",
        menuName = "Just Some Stars/Animation 2D/Character Sprite Set")]
    public sealed class CharacterSpriteSet : ScriptableObject
    {
        [SerializeField] private string characterId;
        [SerializeField] private SpriteAnimationClipDefinition[] clips =
            Array.Empty<SpriteAnimationClipDefinition>();
        [SerializeField] private SpriteClipAnchorTrack[] anchorTracks =
            Array.Empty<SpriteClipAnchorTrack>();

        public string CharacterId => characterId;
        public IReadOnlyList<SpriteAnimationClipDefinition> Clips => clips;
        public IReadOnlyList<SpriteClipAnchorTrack> AnchorTracks => anchorTracks;

        public void Configure(
            string id,
            SpriteAnimationClipDefinition[] definitions,
            SpriteClipAnchorTrack[] tracks = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new InvalidOperationException(
                    "Character sprite set requires a stable character id.");
            }
            if (definitions == null || definitions.Length == 0 ||
                definitions.Any(clip => clip == null))
            {
                throw new InvalidOperationException(
                    $"Character sprite set {id} requires at least one clip.");
            }

            foreach (var clip in definitions)
            {
                clip.ValidateOrThrow();
            }
            if (definitions.Select(clip => clip.StableId)
                .Distinct(StringComparer.Ordinal).Count() != definitions.Length)
            {
                throw new InvalidOperationException(
                    $"Character sprite set {id} contains duplicate clip ids.");
            }

            characterId = id;
            clips = (SpriteAnimationClipDefinition[])definitions.Clone();
            anchorTracks = tracks != null
                ? (SpriteClipAnchorTrack[])tracks.Clone()
                : Array.Empty<SpriteClipAnchorTrack>();
            ValidateAnchorTracks();
        }

        public SpriteAnimationClipDefinition FindClip(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                throw new ArgumentException("Clip id is required.", nameof(stableId));
            }
            var match = clips?.SingleOrDefault(clip =>
                clip != null && string.Equals(
                    clip.StableId,
                    stableId,
                    StringComparison.Ordinal));
            return match ?? throw new KeyNotFoundException(
                $"Character sprite set {characterId} has no clip {stableId}.");
        }

        public IReadOnlyList<SpriteFrameAnchor> ResolveFrameAnchors(
            string stableClipId,
            int frameIndex)
        {
            var track = anchorTracks?.SingleOrDefault(candidate =>
                candidate != null && string.Equals(
                    candidate.ClipId,
                    stableClipId,
                    StringComparison.Ordinal));
            if (track == null)
            {
                throw new KeyNotFoundException(
                    $"Character sprite set {characterId} has no anchor track " +
                    $"for {stableClipId}.");
            }
            if (frameIndex < 0 || frameIndex >= track.Frames.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(frameIndex));
            }
            return track.Frames[frameIndex].Anchors;
        }

        private void ValidateAnchorTracks()
        {
            if (anchorTracks == null || anchorTracks.Length == 0)
            {
                anchorTracks = Array.Empty<SpriteClipAnchorTrack>();
                return;
            }
            if (anchorTracks.Any(track => track == null) ||
                anchorTracks.Select(track => track.ClipId)
                    .Distinct(StringComparer.Ordinal).Count() != anchorTracks.Length ||
                anchorTracks.Length != clips.Length)
            {
                throw new InvalidOperationException(
                    $"Character sprite set {characterId} has invalid anchor tracks.");
            }
            foreach (var clip in clips)
            {
                var track = anchorTracks.SingleOrDefault(candidate =>
                    string.Equals(candidate.ClipId, clip.StableId, StringComparison.Ordinal));
                if (track == null || track.Frames.Count != clip.Frames.Count)
                {
                    throw new InvalidOperationException(
                        $"Character sprite set {characterId} anchor track does not " +
                        $"match {clip.StableId}.");
                }
            }
        }
    }
}
