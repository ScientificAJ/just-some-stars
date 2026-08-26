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

        public string CharacterId => characterId;
        public IReadOnlyList<SpriteAnimationClipDefinition> Clips => clips;

        public void Configure(string id, SpriteAnimationClipDefinition[] definitions)
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
    }
}
