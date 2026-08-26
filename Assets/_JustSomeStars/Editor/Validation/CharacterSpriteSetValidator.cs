using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JustSomeStars.Editor.Importers;
using JustSomeStars.Runtime.Animation2D;
using UnityEngine;

namespace JustSomeStars.Editor.Validation
{
    public static class CharacterSpriteSetValidator
    {
        internal static void ValidateOrThrow(
            CharacterSpriteSet spriteSet,
            CharacterSpriteManifest manifest)
        {
            if (spriteSet == null)
            {
                throw new ArgumentNullException(nameof(spriteSet));
            }
            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }
            if (!string.Equals(
                    spriteSet.CharacterId,
                    manifest.characterId,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "CharacterSpriteSet identity does not match its manifest.");
            }
            if (spriteSet.Clips.Count != manifest.clips.Length)
            {
                throw new InvalidDataException(
                    "CharacterSpriteSet clip count does not match its manifest.");
            }

            for (var clipIndex = 0; clipIndex < manifest.clips.Length; clipIndex++)
            {
                var clip = spriteSet.Clips[clipIndex];
                var expected = manifest.clips[clipIndex];
                clip.ValidateOrThrow();
                if (!string.Equals(clip.StableId, expected.id, StringComparison.Ordinal) ||
                    clip.Frames.Count != expected.frames.Length ||
                    clip.Facing.ToString() != expected.facing ||
                    clip.LoopMode.ToString() != expected.loopMode)
                {
                    throw new InvalidDataException(
                        $"CharacterSpriteSet clip {clipIndex} does not match its manifest.");
                }

                var expectedContacts = new List<SpriteFrameContact>();
                var expectedEvents = new List<SpriteFrameEvent>();
                for (var frameIndex = 0; frameIndex < expected.frames.Length; frameIndex++)
                {
                    var expectedFrame = expected.frames[frameIndex];
                    var sprite = clip.Frames[frameIndex];
                    if (sprite == null || sprite.name != expectedFrame.spriteName)
                    {
                        throw new InvalidDataException(
                            $"Clip {clip.StableId} frame {frameIndex} sprite is stale.");
                    }
                    var normalizedPivot = new Vector2(
                        sprite.pivot.x / sprite.rect.width,
                        sprite.pivot.y / sprite.rect.height);
                    if (Vector2.Distance(
                            normalizedPivot,
                            new Vector2(
                                expectedFrame.pivotNormalized[0],
                                expectedFrame.pivotNormalized[1])) > 0.0001f ||
                        Mathf.Abs(
                            clip.FrameDurations[frameIndex] -
                            expectedFrame.durationSeconds) > 0.000001f)
                    {
                        throw new InvalidDataException(
                            $"Clip {clip.StableId} frame {frameIndex} timing or pivot is stale.");
                    }
                    expectedContacts.AddRange(expectedFrame.contacts.Select(contact =>
                        new SpriteFrameContact(frameIndex, contact)));
                    expectedEvents.AddRange(expectedFrame.events.Select(frameEvent =>
                    {
                        if (!Enum.TryParse<SpriteFrameEventKind>(
                                frameEvent.kind,
                                ignoreCase: false,
                                out var kind))
                        {
                            throw new InvalidDataException(
                                $"Unsupported frame-event kind {frameEvent.kind}.");
                        }
                        return new SpriteFrameEvent(frameIndex, kind, frameEvent.id);
                    }));
                }

                AssertContacts(clip.FrameContacts, expectedContacts, clip.StableId);
                AssertEvents(clip.FrameEvents, expectedEvents, clip.StableId);
            }
        }

        private static void AssertContacts(
            IReadOnlyList<SpriteFrameContact> actual,
            IReadOnlyList<SpriteFrameContact> expected,
            string clipId)
        {
            var actualRows = actual.Select(contact =>
                $"{contact.FrameIndex}:{contact.Id}").ToArray();
            var expectedRows = expected.Select(contact =>
                $"{contact.FrameIndex}:{contact.Id}").ToArray();
            if (!actualRows.SequenceEqual(expectedRows, StringComparer.Ordinal))
            {
                throw new InvalidDataException(
                    $"Clip {clipId} contacts do not match its manifest.");
            }
        }

        private static void AssertEvents(
            IReadOnlyList<SpriteFrameEvent> actual,
            IReadOnlyList<SpriteFrameEvent> expected,
            string clipId)
        {
            var actualRows = actual.Select(frameEvent =>
                $"{frameEvent.FrameIndex}:{frameEvent.Kind}:{frameEvent.Id}").ToArray();
            var expectedRows = expected.Select(frameEvent =>
                $"{frameEvent.FrameIndex}:{frameEvent.Kind}:{frameEvent.Id}").ToArray();
            if (!actualRows.SequenceEqual(expectedRows, StringComparer.Ordinal))
            {
                throw new InvalidDataException(
                    $"Clip {clipId} events do not match its manifest.");
            }
        }
    }
}
