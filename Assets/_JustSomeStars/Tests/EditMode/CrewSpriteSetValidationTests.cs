using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JustSomeStars.Editor.Importers;
using JustSomeStars.Runtime.Animation2D;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class CrewSpriteSetValidationTests
    {
        private static readonly string[] Names = { "Mira", "Juno", "Kai", "Bea", "Ori" };
        private static readonly string[] Ids = { "mira", "juno", "kai", "bea", "ori" };
        private static readonly string[] Clips =
        {
            "idle", "run", "turn", "jump", "land", "climb", "scan", "interact",
        };
        private static readonly int[] FrameCounts = { 4, 8, 4, 6, 4, 8, 8, 6 };

        [Test]
        public void CrewAssets_AreBespokeClipCompleteAndManifestReconciled()
        {
            for (var characterIndex = 0; characterIndex < Names.Length; characterIndex++)
            {
                var name = Names[characterIndex];
                var id = Ids[characterIndex];
                var assetPath = $"Assets/_JustSomeStars/Content/Characters/{name}SpriteSet.asset";
                var spriteSet = AssetDatabase.LoadAssetAtPath<CharacterSpriteSet>(assetPath);
                Assert.That(spriteSet, Is.Not.Null, assetPath);
                Assert.That(spriteSet.CharacterId, Is.EqualTo(id));
                Assert.That(spriteSet.Clips.Count, Is.EqualTo(16));

                foreach (var facing in new[] { "right", "left" })
                {
                    var atlasPath =
                        $"Assets/_JustSomeStars/Art/2D/Characters/{name}/Atlases/" +
                        $"{facing}/{id}-{facing}.png";
                    var manifest = CharacterSpritePostprocessor.LoadValidatedManifest(
                        atlasPath,
                        Path.GetFullPath("."));
                    var expectedClips = spriteSet.Clips
                        .Where(clip => clip.StableId.EndsWith($".{facing}", StringComparison.Ordinal))
                        .ToArray();
                    Assert.That(expectedClips, Has.Length.EqualTo(8));
                    for (var clipIndex = 0; clipIndex < Clips.Length; clipIndex++)
                    {
                        var clip = spriteSet.FindClip($"{id}.{Clips[clipIndex]}.{facing}");
                        Assert.That(
                            clip.Frames.Count,
                            Is.EqualTo(FrameCounts[clipIndex]));
                        Assert.That(clip.FrameDurations.All(duration =>
                            Mathf.Abs(duration - 1f / 12f) < 0.00001f), Is.True);
                        if (Clips[clipIndex] == "climb")
                        {
                            Assert.That(
                                clip.LoopMode,
                                Is.EqualTo(SpriteAnimationLoopMode.Once));
                        }
                        if (Clips[clipIndex] == "jump")
                        {
                            Assert.That(
                                clip.FrameContacts.All(contact =>
                                    contact.FrameIndex == 0),
                                Is.True,
                                $"{clip.StableId} declares airborne support.");
                        }
                    }

                    Assert.That(manifest.clips, Has.Length.EqualTo(8));
                    Assert.That(
                        manifest.clips.Select(clip => clip.id),
                        Is.EqualTo(Clips.Select(clip => $"{id}.{clip}.{facing}")));
                    AssertEveryFrameHasExactAnchors(name, manifest);
                    var scanTrack = spriteSet.AnchorTracks.Single(track =>
                        track.ClipId == $"{id}.scan.{facing}");
                    Assert.That(
                        scanTrack.Frames.All(frame =>
                            frame.Anchors.Single(anchor =>
                                anchor.Id == "ActiveTool").IsAuthoredVisible),
                        Is.True);
                }

                AssertNoCaptainLayerRendererDependency(spriteSet);
            }
        }

        [Test]
        public void CrewPackages_PinApprovedIdentityHeightAndRoleEquipment()
        {
            var expected = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Mira"] = "7d7ebbbe494ba1899e5d0f4549e5f363ac58a00e75fadf8c25524989524787a5",
                ["Juno"] = "b46e416d4ac0901efdf9dbcd5eff244e8f930f64e117fd5a8c753053930da722",
                ["Kai"] = "d53aa52b5cdefaa63988394a94f92e4a3471fd0d48c66f69aa4eeb2227ee8396",
                ["Bea"] = "28927ab81d62f07deb7fc7a327d19e4fe60f4fd054f98a1a6cccc481eb4ba0e0",
                ["Ori"] = "16e0b936ff7b93b3ef28a1f4bab862c7a78981e2a3c765cb6ea09e05527b22ce",
            };
            var roleTokens = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["Mira"] = new[] { "spectrum-viewer", "atmosphere-sampler", "violet" },
                ["Juno"] = new[] { "wrench", "diagnostic-driver", "repair-pack" },
                ["Kai"] = new[] { "route-controller", "heading-display", "pilot-pack" },
                ["Bea"] = new[] { "optical-camera", "atlas-tablet", "archive-pack" },
                ["Ori"] = new[] { "secondary-scanner", "arm-bay", "four-wheel" },
            };

            foreach (var name in Names)
            {
                var packagePath =
                    $"Assets/_JustSomeStars/Art/2D/Characters/{name}/" +
                    $"{name.ToLowerInvariant()}-sprite-package.json";
                Assert.That(File.Exists(packagePath), Is.True, packagePath);
                var json = File.ReadAllText(packagePath);
                Assert.That(json, Does.Contain(expected[name]));
                foreach (var token in roleTokens[name])
                {
                    Assert.That(json, Does.Contain(token), $"{name} is missing {token}.");
                }
                Assert.That(json, Does.Not.Contain("CaptainSpriteSet"));
                Assert.That(json, Does.Not.Contain("LayeredCharacterRenderer"));
            }
        }

        [Test]
        public void NeutralFaceSpeechAtlases_CoverWrittenExpressionSemantics()
        {
            var expressions = new[]
            {
                "neutral", "happy", "curious", "worried", "afraid",
                "surprised", "determined", "sad", "blink", "speaking",
            };
            foreach (var pair in Names.Zip(Ids, (name, id) => (name, id)))
            {
                var manifestPath =
                    $"Assets/_JustSomeStars/Art/2D/Characters/{pair.name}/Atlases/neutral/" +
                    $"{pair.id}-face-speech.sprite-manifest.json";
                Assert.That(File.Exists(manifestPath), Is.True, manifestPath);
                var json = File.ReadAllText(manifestPath);
                foreach (var expression in expressions)
                {
                    Assert.That(json, Does.Contain($"{pair.id}.expression.{expression}"));
                }
                for (var index = 0; index < 6; index++)
                {
                    Assert.That(json, Does.Contain($"{pair.id}.speech.{index}"));
                }
            }
        }

        private static void AssertEveryFrameHasExactAnchors(
            string name,
            CharacterSpriteManifest manifest)
        {
            var expected = name == "Ori"
                ? new[]
                {
                    "Root", "LeftWheelContact", "RightWheelContact", "HeadRotationRing",
                    "OpticalEye", "SignalAntenna", "LeftArmBay", "RightArmBay",
                    "LeftGripper", "RightGripper", "SecondaryScanner", "ServicePanel",
                    "ActiveTool", "StowedTool",
                }
                : new[]
                {
                    "Root", "LeftFoot", "RightFoot", "LeftHand", "RightHand",
                    "HelmetRing", "BackpackSocket", "Belt", "LeftWrist", "RightWrist",
                    "LeftBootTop", "RightBootTop", "ActiveTool", "StowedTool",
                };
            foreach (var frame in manifest.clips.SelectMany(clip => clip.frames))
            {
                Assert.That(
                    frame.anchors.Select(anchor => anchor.id),
                    Is.EquivalentTo(expected));
                Assert.That(frame.anchors.All(anchor =>
                    anchor.runtimePixels[0] >= 0f && anchor.runtimePixels[0] <= 128f &&
                    anchor.runtimePixels[1] >= 0f && anchor.runtimePixels[1] <= 192f &&
                    anchor.semanticBasis == "authored-frame-v1" &&
                    anchor.semanticRegionNormalized != null &&
                    anchor.semanticRegionNormalized.Length == 4),
                    Is.True);
            }
        }

        private static void AssertNoCaptainLayerRendererDependency(CharacterSpriteSet spriteSet)
        {
            Assert.That(spriteSet.GetType().FullName, Is.EqualTo(
                "JustSomeStars.Runtime.Animation2D.CharacterSpriteSet"));
            Assert.That(
                spriteSet.GetType().GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Any(field => field.FieldType.Name.Contains("Captain", StringComparison.Ordinal) ||
                                  field.FieldType.Name.Contains("LayeredCharacterRenderer", StringComparison.Ordinal)),
                Is.False);
        }
    }
}
