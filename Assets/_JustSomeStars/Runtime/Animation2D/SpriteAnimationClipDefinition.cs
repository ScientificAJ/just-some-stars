using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JustSomeStars.Runtime.Animation2D
{
    [CreateAssetMenu(
        fileName = "SpriteAnimationClip",
        menuName = "Just Some Stars/Animation 2D/Sprite Animation Clip")]
    public sealed class SpriteAnimationClipDefinition : ScriptableObject
    {
        [SerializeField] private string stableId;
        [SerializeField] private SpriteFacing facing;
        [SerializeField] private SpriteAnimationLoopMode loopMode;
        [SerializeField] private Sprite[] frames = Array.Empty<Sprite>();
        [SerializeField] private float[] frameDurations = Array.Empty<float>();
        [SerializeField] private SpriteFrameContact[] frameContacts =
            Array.Empty<SpriteFrameContact>();
        [SerializeField] private SpriteFrameEvent[] frameEvents =
            Array.Empty<SpriteFrameEvent>();

        public string StableId => stableId;
        public SpriteFacing Facing => facing;
        public SpriteAnimationLoopMode LoopMode => loopMode;
        public IReadOnlyList<Sprite> Frames => frames;
        public IReadOnlyList<float> FrameDurations => frameDurations;
        public IReadOnlyList<SpriteFrameContact> FrameContacts => frameContacts;
        public IReadOnlyList<SpriteFrameEvent> FrameEvents => frameEvents;
        public float TotalDuration => frameDurations?.Sum() ?? 0f;

        public void Configure(
            string id,
            SpriteFacing declaredFacing,
            SpriteAnimationLoopMode declaredLoopMode,
            Sprite[] sprites,
            float[] durations,
            SpriteFrameEvent[] events,
            SpriteFrameContact[] contacts = null)
        {
            stableId = id;
            facing = declaredFacing;
            loopMode = declaredLoopMode;
            frames = sprites != null ? (Sprite[])sprites.Clone() : null;
            frameDurations = durations != null ? (float[])durations.Clone() : null;
            frameEvents = events != null
                ? (SpriteFrameEvent[])events.Clone()
                : Array.Empty<SpriteFrameEvent>();
            frameContacts = contacts != null
                ? (SpriteFrameContact[])contacts.Clone()
                : Array.Empty<SpriteFrameContact>();
            ValidateOrThrow();
        }

        public void ValidateOrThrow()
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                throw new InvalidOperationException(
                    "Sprite animation clip requires a stable id.");
            }

            if (frames == null || frames.Length == 0 || frames.Any(frame => frame == null))
            {
                throw new InvalidOperationException(
                    $"Sprite animation clip {stableId} has missing frames.");
            }

            if (frameDurations == null || frameDurations.Length != frames.Length ||
                frameDurations.Any(duration =>
                    duration <= 0f || float.IsNaN(duration) || float.IsInfinity(duration)))
            {
                throw new InvalidOperationException(
                    $"Sprite animation clip {stableId} has invalid frame durations.");
            }

            frameEvents ??= Array.Empty<SpriteFrameEvent>();
            frameContacts ??= Array.Empty<SpriteFrameContact>();
            if (frameEvents.Any(frameEvent =>
                    frameEvent.FrameIndex < 0 || frameEvent.FrameIndex >= frames.Length ||
                    string.IsNullOrWhiteSpace(frameEvent.Id)) ||
                frameContacts.Any(contact =>
                    contact.FrameIndex < 0 || contact.FrameIndex >= frames.Length ||
                    string.IsNullOrWhiteSpace(contact.Id)))
            {
                throw new InvalidOperationException(
                    $"Sprite animation clip {stableId} has invalid frame metadata.");
            }

            var duplicateEvent = frameEvents
                .GroupBy(frameEvent => (frameEvent.FrameIndex, frameEvent.Id))
                .Any(group => group.Count() > 1);
            var duplicateContact = frameContacts
                .GroupBy(contact => (contact.FrameIndex, contact.Id))
                .Any(group => group.Count() > 1);
            if (duplicateEvent || duplicateContact)
            {
                throw new InvalidOperationException(
                    $"Sprite animation clip {stableId} has duplicate frame metadata.");
            }
        }
    }
}
