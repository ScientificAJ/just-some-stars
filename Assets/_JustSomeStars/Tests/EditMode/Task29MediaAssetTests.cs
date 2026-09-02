using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JustSomeStars.Runtime.Animation2D;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Dialogue;
using JustSomeStars.Runtime.Cinematics;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class Task29MediaAssetTests
    {
        private const string AudioLibraryPath =
            "Assets/_JustSomeStars/Content/Resources/Task29AudioCueLibrary.asset";
        private const string CinematicRoot =
            "Assets/_JustSomeStars/Content/Cinematics";
        private const string AnimationRoot =
            "Assets/_JustSomeStars/Art/2D/Characters/Animations";
        private const string MediaRoot = "Assets/_JustSomeStars/Audio";
        private const string RightsLedger = "docs/media/media-rights-ledger.csv";

        private static readonly string[] RequiredMusicCues =
        {
            "cue.clubhouse.before-dinner",
            "cue.mirra.horizon",
            "cue.koro-vesper.orbit",
            "cue.aster-veil.signal",
            "cue.dinner.homecoming",
        };

        private static readonly string[] RequiredMusicStates =
        {
            "music.clubhouse",
            "music.mirra",
            "music.koro-vesper",
            "music.aster-veil",
            "music.dinner",
        };

        private static readonly string[] RequiredSequences =
        {
            "Opening",
            "SignalReassembly",
            "Clubhouse",
            "DinnerEnding",
        };

        private static readonly string[] RequiredScenePaths =
        {
            "Assets/_JustSomeStars/Scenes/Cinematics/Opening.unity",
            "Assets/_JustSomeStars/Scenes/Cinematics/SignalReassembly.unity",
            "Assets/_JustSomeStars/Scenes/Core/Clubhouse.unity",
            "Assets/_JustSomeStars/Scenes/Cinematics/DinnerEnding.unity",
        };

        private static readonly (string Name, string Id)[] RequiredActors =
        {
            ("Captain", "captain"),
            ("Mira", "mira"),
            ("Juno", "juno"),
            ("Kai", "kai"),
            ("Bea", "bea"),
            ("Ori", "ori"),
        };

        private static readonly (string Path, string State, bool HasDialogue)[]
            RequiredDestinations =
            {
                ("Assets/_JustSomeStars/Scenes/Destinations/Mirra.unity",
                    "music.mirra", true),
                ("Assets/_JustSomeStars/Scenes/Destinations/KoroVesper.unity",
                    "music.koro-vesper", true),
                ("Assets/_JustSomeStars/Scenes/Destinations/AsterVeil.unity",
                    "music.aster-veil", false),
            };

        [Test]
        public void AudioLibrary_ResolvesEveryStoryAndJukeboxCueWithAlignedSignalStems()
        {
            var library = AssetDatabase.LoadAssetAtPath<AudioCueLibrary>(
                AudioLibraryPath);
            Assert.That(library, Is.Not.Null, AudioLibraryPath);
            Assert.DoesNotThrow(library.ValidateOrThrow);

            foreach (var cueId in RequiredMusicCues)
            {
                var cue = library.FindCue(cueId);
                Assert.That(cue.Bus, Is.EqualTo(AudioBus.Music), cueId);
                Assert.That(cue.Clip, Is.Not.Null, cueId);
                Assert.That(cue.Loop, Is.True, cueId);
                Assert.That(cue.Clip.length, Is.GreaterThanOrEqualTo(20f), cueId);
                Assert.That(cue.Clip.channels, Is.EqualTo(2), cueId);
                Assert.That(cue.Clip.frequency, Is.EqualTo(44100), cueId);
            }

            foreach (var stateId in RequiredMusicStates)
            {
                var state = library.FindMusicState(stateId);
                var foundation = library.FindCue(state.FoundationCueId).Clip;
                var signal = library.FindCue(state.SignalStemCueId).Clip;
                Assert.That(foundation.samples, Is.EqualTo(signal.samples), stateId);
                Assert.That(foundation.channels, Is.EqualTo(signal.channels), stateId);
                Assert.That(foundation.frequency, Is.EqualTo(signal.frequency), stateId);
                Assert.That(state.SignalLevel, Is.InRange(0.08f, 0.85f), stateId);
            }

            foreach (var guid in AssetDatabase.FindAssets(
                         "t:DialogueEntry",
                         new[] { "Assets/_JustSomeStars/Content/Dialogue" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var entry = AssetDatabase.LoadAssetAtPath<DialogueEntry>(path);
                Assert.That(entry, Is.Not.Null, path);
                Assert.That(
                    library.TryFindCue(entry.VoiceReference, out _) ||
                    library.IsExplicitlyUnvoiced(entry.VoiceReference),
                    Is.True,
                    $"{path} voice reference '{entry.VoiceReference}' must resolve " +
                    "or be explicitly caption-only.");
            }
        }

        [Test]
        public void CinematicDefinitions_AreDeterministicLocalizedAndHaveImmediateFallbacks()
        {
            foreach (var sequenceName in RequiredSequences)
            {
                var path = $"{CinematicRoot}/{sequenceName}.asset";
                var definition = AssetDatabase.LoadAssetAtPath<CinematicSequenceDefinition>(
                    path);
                Assert.That(definition, Is.Not.Null, path);
                Assert.DoesNotThrow(definition.ValidateOrThrow, path);
                Assert.That(definition.FallbackStill, Is.Not.Null, path);
                Assert.That(definition.Beats, Is.Not.Empty, path);
                Assert.That(definition.Beats.All(beat =>
                    beat.Kind == CinematicBeatKind.BodyClip), Is.True,
                    $"{path} must use character frame events as its one media clock.");
            }
        }

        [Test]
        public void EverySpeakingActor_ExposesCompleteFacesAndLayeredSpeechShapes()
        {
            foreach (var actor in RequiredActors)
            {
                var set = AssetDatabase.LoadAssetAtPath<CharacterSpriteSet>(
                    $"Assets/_JustSomeStars/Content/Characters/Faces/" +
                    $"{actor.Name}FaceSpriteSet.asset");
                Assert.That(set, Is.Not.Null, actor.Name);
                foreach (var expression in FacialAtlasController2D.RequiredExpressions)
                {
                    Assert.That(set.FindClip(
                        $"{actor.Id}.expression.{expression}"), Is.Not.Null,
                        $"{actor.Name}/{expression}");
                }
                for (var viseme = 0; viseme < FacialAtlasController2D.VisemeCount; viseme++)
                {
                    Assert.That(set.FindClip(
                        $"{actor.Id}.speech.{viseme}"), Is.Not.Null,
                        $"{actor.Name}/speech.{viseme}");
                }

                var performance = AssetDatabase.LoadAssetAtPath<CharacterSpriteSet>(
                    $"Assets/_JustSomeStars/Content/Characters/" +
                    $"{actor.Name}SpriteSet.asset");
                Assert.That(performance, Is.Not.Null, actor.Name + " performance");
                foreach (var motion in new[]
                         {
                             "idle", "run", "jump", "land", "climb", "interact",
                             "scan", "turn",
                         })
                {
                    if (actor.Id == "captain") break;
                    Assert.That(performance.FindClip(
                        $"{actor.Id}.{motion}.left"), Is.Not.Null);
                    Assert.That(performance.FindClip(
                        $"{actor.Id}.{motion}.right"), Is.Not.Null);
                }
            }
        }

        [Test]
        public void PerformanceSets_UseOneOrderedFrameEventContractForEveryActorAndGesture()
        {
            var requiredKinds = new HashSet<SpriteFrameEventKind>
            {
                SpriteFrameEventKind.Audio,
                SpriteFrameEventKind.Vfx,
                SpriteFrameEventKind.Caption,
                SpriteFrameEventKind.Expression,
                SpriteFrameEventKind.Viseme,
                SpriteFrameEventKind.InteractionRelease,
            };
            var authoredKinds = new HashSet<SpriteFrameEventKind>();
            foreach (var actor in RequiredActors)
            {
                var path = $"{AnimationRoot}/{actor.Name}PerformanceSpriteSet.asset";
                var set = AssetDatabase.LoadAssetAtPath<CharacterSpriteSet>(path);
                Assert.That(set, Is.Not.Null, path);
                Assert.That(set.CharacterId, Is.EqualTo(actor.Id), path);
                foreach (var facing in new[] { "left", "right" })
                {
                    Assert.That(set.FindClip(
                        $"{actor.Id}.reaction.{facing}"), Is.Not.Null, path);
                    Assert.That(set.FindClip(
                        $"{actor.Id}.conversation.{facing}"), Is.Not.Null, path);
                }
                authoredKinds.UnionWith(set.Clips.SelectMany(clip => clip.FrameEvents)
                    .Select(frameEvent => frameEvent.Kind));
                Assert.That(set.Clips.All(clip =>
                    !string.IsNullOrWhiteSpace(clip.PlaybackMotionId)), Is.True, path);
            }
            Assert.That(authoredKinds.IsSupersetOf(requiredKinds), Is.True,
                "The shared frame-event vocabulary must cover audio, VFX, captions, " +
                "faces, speech and release without inventing fake per-actor events.");

            foreach (var guid in AssetDatabase.FindAssets(
                         "t:DialogueEntry",
                         new[] { "Assets/_JustSomeStars/Content/Dialogue" }))
            {
                var entry = AssetDatabase.LoadAssetAtPath<DialogueEntry>(
                    AssetDatabase.GUIDToAssetPath(guid));
                var actorId = entry.SpeakerId.Value
                    .Replace("crew.", string.Empty)
                    .Replace("robot.", string.Empty);
                var actor = RequiredActors.Single(candidate =>
                    candidate.Id == actorId);
                var set = AssetDatabase.LoadAssetAtPath<CharacterSpriteSet>(
                    $"{AnimationRoot}/{actor.Name}PerformanceSpriteSet.asset");
                foreach (var facing in new[] { "left", "right" })
                {
                    var clip = set.FindClip(
                        $"{actorId}.dialogue.{entry.StableId.Value}.{facing}");
                    Assert.That(clip.FrameEvents.Any(frameEvent =>
                        frameEvent.Kind == SpriteFrameEventKind.Expression &&
                        frameEvent.Id == entry.Expression), Is.True, entry.name);
                    Assert.That(clip.FrameEvents.Any(frameEvent =>
                        frameEvent.Kind == SpriteFrameEventKind.Caption &&
                        frameEvent.Id == entry.LocalizationKey), Is.True, entry.name);
                    Assert.That(clip.FrameEvents.Any(frameEvent =>
                        frameEvent.Kind == SpriteFrameEventKind.InteractionRelease),
                        Is.True, entry.name);
                }
            }
        }

        [Test]
        public void ChapterOneScenes_HaveOneBoundCinematicDirectorAndInEngineCaptain()
        {
            foreach (var scenePath in RequiredScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(scenePath);
                var roots = scene.GetRootGameObjects();
                var directors = roots.SelectMany(root =>
                        root.GetComponentsInChildren<CinematicDirector>(true))
                    .ToArray();
                Assert.That(directors, Has.Length.EqualTo(1), scenePath);
                Assert.DoesNotThrow(directors[0].ValidateAuthoredOrThrow, scenePath);
                var sequence = roots.SelectMany(root => root.GetComponentsInChildren<
                        ChapterOneSequenceController2D>(true))
                    .Single();
                var extensions = new SerializedObject(sequence)
                    .FindProperty("playerUiExtensions");
                Assert.That(extensions, Is.Not.Null, scenePath);
                Assert.That(Enumerable.Range(0, extensions.arraySize).Any(index =>
                    extensions.GetArrayElementAtIndex(index).objectReferenceValue ==
                    directors[0]), Is.True, scenePath);
                Assert.That(roots.SelectMany(root =>
                        root.GetComponentsInChildren<CinematicActor2D>(true))
                    .Any(actor => actor.ActorId == "captain"), Is.True, scenePath);
                Assert.That(directors[0].HasFrameEventReleaseRoute, Is.True, scenePath);
            }
        }

        [Test]
        public void DestinationScenes_BindMusicProgressionAndMissionDialogueMedia()
        {
            foreach (var destination in RequiredDestinations)
            {
                var scene = EditorSceneManager.OpenScene(destination.Path);
                var roots = scene.GetRootGameObjects();
                var music = roots.SelectMany(root =>
                        root.GetComponentsInChildren<MusicStatePresenter2D>(true))
                    .ToArray();
                Assert.That(music, Has.Length.EqualTo(1), destination.Path);
                Assert.That(music[0].MusicStateId, Is.EqualTo(destination.State),
                    destination.Path);
                Assert.DoesNotThrow(music[0].ValidateOrThrow, destination.Path);

                var presenters = roots.SelectMany(root =>
                        root.GetComponentsInChildren<MirraDialoguePresenter2D>(true))
                    .ToArray();
                Assert.That(presenters.Length > 0, Is.EqualTo(destination.HasDialogue),
                    destination.Path);
                foreach (var presenter in presenters)
                {
                    var serialized = new SerializedObject(presenter);
                    Assert.That(serialized.FindProperty("english").objectReferenceValue,
                        Is.Not.Null, destination.Path);
                    Assert.That(serialized.FindProperty("facialAtlas").objectReferenceValue,
                        Is.Not.Null, destination.Path);
                    Assert.That(serialized.FindProperty("actors").arraySize,
                        Is.GreaterThanOrEqualTo(1), destination.Path);
                }
            }
        }

        [Test]
        public void RightsLedger_CoversEveryRuntimeMediaFileWithHashAndLicenseStatus()
        {
            var ledgerPath = Path.GetFullPath(RightsLedger);
            Assert.That(File.Exists(ledgerPath), Is.True, RightsLedger);
            var ledger = File.ReadAllText(ledgerPath);
            Assert.That(ledger, Does.StartWith(
                "asset_path,sha256,source,license,generation_tool,edit_status"));

            var mediaFiles = Directory.GetFiles(MediaRoot, "*", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                .Where(path => path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                               path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                               path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
                .Select(path => path.Replace('\\', '/'))
                .ToArray();
            Assert.That(mediaFiles, Is.Not.Empty);
            foreach (var path in mediaFiles)
            {
                Assert.That(ledger, Does.Contain(path + ","), path);
            }
            Assert.That(ledger, Does.Not.Contain(",unknown,"));
            Assert.That(ledger, Does.Not.Contain(",pending,"));
        }
    }
}
