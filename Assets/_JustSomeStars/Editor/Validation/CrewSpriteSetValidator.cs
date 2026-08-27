using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JustSomeStars.Editor.Importers;
using JustSomeStars.Runtime.Animation2D;

namespace JustSomeStars.Editor.Validation
{
    public static class CrewSpriteSetValidator
    {
        internal static void ValidateOrThrow(
            CharacterSpriteSet spriteSet,
            IReadOnlyList<CharacterSpriteManifest> manifests)
        {
            if (spriteSet == null)
            {
                throw new ArgumentNullException(nameof(spriteSet));
            }
            if (manifests == null || manifests.Count != 2 ||
                manifests.Any(manifest => manifest == null))
            {
                throw new InvalidDataException(
                    "Crew validation requires right and left manifests.");
            }

            var manifestClips = manifests.SelectMany(manifest => manifest.clips).ToArray();
            if (spriteSet.Clips.Count != 16 ||
                spriteSet.Clips.Count != manifestClips.Length ||
                spriteSet.AnchorTracks.Count != manifestClips.Length)
            {
                throw new InvalidDataException(
                    $"Crew sprite set {spriteSet.CharacterId} is not clip complete.");
            }

            foreach (var expected in manifestClips)
            {
                var clip = spriteSet.FindClip(expected.id);
                var isClimb = expected.id.Contains(".climb.", StringComparison.Ordinal);
                var isExternalInteraction =
                    expected.id.Contains(".interact.", StringComparison.Ordinal) &&
                    string.Equals(
                        expected.sceneGeometryPolicy,
                        "external-only",
                        StringComparison.Ordinal);
                var track = spriteSet.AnchorTracks.SingleOrDefault(candidate =>
                    string.Equals(candidate.ClipId, expected.id, StringComparison.Ordinal));
                if (track == null || clip.Frames.Count != expected.frames.Length ||
                    track.Frames.Count != expected.frames.Length ||
                    !string.Equals(
                        clip.LoopMode.ToString(),
                        expected.loopMode,
                        StringComparison.Ordinal) ||
                    ((isClimb || isExternalInteraction) &&
                     (!string.Equals(expected.sceneGeometryPolicy, "external-only",
                         StringComparison.Ordinal) ||
                      expected.frames.Any(frame =>
                          !string.Equals(
                              frame.authoredPoseRole,
                              isClimb
                                  ? "actor-only"
                                  : "actor-with-approved-equipment",
                              StringComparison.Ordinal) ||
                          !string.Equals(frame.contactAuthority,
                              "external-scene-resolver",
                              StringComparison.Ordinal)))))
                {
                    throw new InvalidDataException(
                        $"Crew clip {expected.id} has stale frames or anchors.");
                }
                for (var frameIndex = 0; frameIndex < expected.frames.Length; frameIndex++)
                {
                    var expectedFrame = expected.frames[frameIndex];
                    var actualAnchors = track.Frames[frameIndex].Anchors;
                    if (actualAnchors.Count != 14 ||
                        actualAnchors.Select(anchor => anchor.Id)
                            .Distinct(StringComparer.Ordinal).Count() != 14 ||
                        !actualAnchors.Select(anchor => anchor.Id).SequenceEqual(
                            expectedFrame.anchors.Select(anchor => anchor.id),
                            StringComparer.Ordinal))
                    {
                        throw new InvalidDataException(
                            $"Crew clip {expected.id} frame {frameIndex} anchors are stale.");
                    }
                    for (var anchorIndex = 0; anchorIndex < actualAnchors.Count; anchorIndex++)
                    {
                        var actual = actualAnchors[anchorIndex].RuntimePixels;
                        var anchorManifest = expectedFrame.anchors[anchorIndex];
                        var expectedPixels = anchorManifest.runtimePixels;
                        var sourcePixels = anchorManifest.sourcePixels;
                        var region = anchorManifest.semanticRegionNormalized;
                        var hasSourcePixels = sourcePixels != null &&
                            sourcePixels.Length == 2;
                        var sourceX = hasSourcePixels ? sourcePixels[0] / 256f : -1f;
                        var sourceY = hasSourcePixels
                            ? (384f - sourcePixels[1]) / 384f
                            : -1f;
                        if (expectedPixels == null || expectedPixels.Length != 2 ||
                            sourcePixels == null || sourcePixels.Length != 2 ||
                            anchorManifest.semanticBasis != "authored-frame-v1" ||
                            region == null || region.Length != 4 ||
                            sourceX < region[0] || sourceX > region[2] ||
                            sourceY < region[1] || sourceY > region[3] ||
                            actualAnchors[anchorIndex].IsAuthoredVisible !=
                                anchorManifest.isAuthoredVisible ||
                            (expected.id.Contains(".scan.", StringComparison.Ordinal) &&
                             string.Equals(
                                 actualAnchors[anchorIndex].Id,
                                 "ActiveTool",
                                 StringComparison.Ordinal) &&
                             !anchorManifest.isAuthoredVisible) ||
                            Math.Abs(actual.x - expectedPixels[0]) > 0.0001f ||
                            Math.Abs(actual.y - expectedPixels[1]) > 0.0001f ||
                            actual.x < 0f || actual.x > 128f ||
                            actual.y < 0f || actual.y > 192f)
                        {
                            throw new InvalidDataException(
                                $"Crew clip {expected.id} frame {frameIndex} anchor " +
                                $"{actualAnchors[anchorIndex].Id} is invalid.");
                        }
                    }
                }
            }
        }
    }
}
