using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JustSomeStars.Editor.Importers;
using JustSomeStars.Runtime.Animation2D;
using JustSomeStars.Runtime.Atlas;
using JustSomeStars.Runtime.Cinematics;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Cosmetics;
using JustSomeStars.Runtime.Dialogue;
using JustSomeStars.Runtime.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace JustSomeStars.Editor.Build
{
    public static class Task29MediaBuilder
    {
        private const string AudioManifest =
            "Assets/_JustSomeStars/Audio/task29-audio-manifest.json";
        private const string AudioLibrary =
            "Assets/_JustSomeStars/Content/Resources/Task29AudioCueLibrary.asset";
        private const string FaceRoot =
            "Assets/_JustSomeStars/Content/Characters/Faces";
        private const string AnimationRoot =
            "Assets/_JustSomeStars/Art/2D/Characters/Animations";
        private const string CinematicRoot =
            "Assets/_JustSomeStars/Content/Cinematics";
        private const string EnglishAsset =
            "Assets/_JustSomeStars/Content/Localization/English/Task28English.asset";
        private const string StandardFont =
            "Assets/TextMesh Pro/Resources/Fonts & Materials/" +
            "LiberationSans SDF.asset";

        private static readonly CharacterSpec[] Characters =
        {
            new("Captain", "captain"),
            new("Mira", "mira"),
            new("Juno", "juno"),
            new("Kai", "kai"),
            new("Bea", "bea"),
            new("Ori", "ori"),
        };

        private static readonly SceneMediaSpec[] Scenes =
        {
            new(
                "Opening",
                "Assets/_JustSomeStars/Scenes/Cinematics/Opening.unity",
                "cinematic.opening",
                "music.clubhouse",
                "mira",
                Task28English.CinematicOpening,
                "curious"),
            new(
                "SignalReassembly",
                "Assets/_JustSomeStars/Scenes/Cinematics/SignalReassembly.unity",
                "cinematic.signal-reassembly",
                "music.aster-veil",
                "ori",
                Task28English.CinematicSignal,
                "surprised"),
            new(
                "Clubhouse",
                "Assets/_JustSomeStars/Scenes/Core/Clubhouse.unity",
                "cinematic.clubhouse",
                "music.clubhouse",
                "juno",
                Task28English.CinematicClubhouse,
                "happy"),
            new(
                "DinnerEnding",
                "Assets/_JustSomeStars/Scenes/Cinematics/DinnerEnding.unity",
                "cinematic.dinner-ending",
                "music.dinner",
                "mira",
                Task28English.CinematicDinner,
                "happy"),
        };

        private static readonly DestinationMediaSpec[] Destinations =
        {
            new(
                "Assets/_JustSomeStars/Scenes/Destinations/Mirra.unity",
                "music.mirra",
                true),
            new(
                "Assets/_JustSomeStars/Scenes/Destinations/KoroVesper.unity",
                "music.koro-vesper",
                true),
            new(
                "Assets/_JustSomeStars/Scenes/Destinations/AsterVeil.unity",
                "music.aster-veil",
                false),
        };

        public static void Apply()
        {
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                EnsureFolder(Path.GetDirectoryName(AudioLibrary)?.Replace('\\', '/'));
                EnsureFolder(FaceRoot);
                EnsureFolder(AnimationRoot);
                EnsureFolder(CinematicRoot);
                ConfigureAudioImporters();
                var audio = BuildAudioLibrary();
                BuildFaceSets();
                BuildPerformanceSets(audio);
                var english = Require<LocalizedEnglishCatalog>(EnglishAsset);
                english.Configure(Task28English.CreateEntries());
                EditorUtility.SetDirty(english);
                AssetDatabase.SaveAssets();

                foreach (var spec in Scenes) PatchScene(spec);
                foreach (var destination in Destinations) PatchDestination(destination);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Require<AudioCueLibrary>(AudioLibrary).ValidateOrThrow();
                ValidatePersisted();
                Debug.Log(
                    "[JSS Task 29] Frame-event performances, layered faces, " +
                    "audio states and cinematic media materialized and validated.");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void ConfigureAudioImporters()
        {
            foreach (var path in AssetDatabase.FindAssets(
                         "t:AudioClip",
                         new[] { "Assets/_JustSomeStars/Audio" })
                     .Select(AssetDatabase.GUIDToAssetPath))
            {
                if (AssetImporter.GetAtPath(path) is not AudioImporter importer)
                    throw new InvalidOperationException($"No AudioImporter for {path}.");
                var music = path.Contains("/Music/", StringComparison.Ordinal);
                var settings = importer.defaultSampleSettings;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = music ? 0.74f : 0.82f;
                settings.loadType = music
                    ? AudioClipLoadType.Streaming
                    : AudioClipLoadType.DecompressOnLoad;
                settings.preloadAudioData = !music;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = false;
                importer.loadInBackground = music;
                importer.SaveAndReimport();
            }
        }

        private static AudioCueLibrary BuildAudioLibrary()
        {
            var manifest = JsonUtility.FromJson<AudioPackageManifest>(
                File.ReadAllText(Absolute(AudioManifest))) ??
                throw new InvalidDataException("Task 29 audio manifest is malformed.");
            if (manifest.schemaVersion != 1 || manifest.files == null ||
                manifest.files.Length != 18 || manifest.musicStates == null ||
                manifest.musicStates.Length != 5)
            {
                throw new InvalidDataException("Task 29 audio manifest is incomplete.");
            }

            var cues = manifest.files.Select(record =>
            {
                var clip = Require<AudioClip>(record.path);
                var bus = Enum.Parse<AudioBus>(record.bus);
                return new AudioCueDefinition(
                    record.cueId,
                    bus,
                    clip,
                    record.loop,
                    bus == AudioBus.Music ? 0.82f : 0.86f);
            }).ToList();
            AddAlias(cues, "jump-audio", "cue.sfx.ui.positive");
            AddAlias(cues, "land-audio", "cue.sfx.footstep.soil");
            AddAlias(cues, "scan-audio", "cue.sfx.lens.focus");

            var states = manifest.musicStates.Select(state =>
                new MusicStateDefinition(
                    state.stateId,
                    state.foundationCueId,
                    state.signalStemCueId,
                    state.signalLevel,
                    state.crossfadeSeconds)).ToArray();
            var library = AssetDatabase.LoadAssetAtPath<AudioCueLibrary>(AudioLibrary);
            if (library == null)
            {
                if (File.Exists(Absolute(AudioLibrary)))
                    AssetDatabase.DeleteAsset(AudioLibrary);
                library = ScriptableObject.CreateInstance<AudioCueLibrary>();
                library.name = "Task29AudioCueLibrary";
                AssetDatabase.CreateAsset(library, AudioLibrary);
            }
            library.Configure(cues.ToArray(), states, ExplicitlyUnvoiced());
            EditorUtility.SetDirty(library);
            return library;
        }

        private static void AddAlias(
            ICollection<AudioCueDefinition> cues,
            string alias,
            string target)
        {
            var resolved = cues.Single(cue => string.Equals(
                cue.StableId, target, StringComparison.Ordinal));
            cues.Add(new AudioCueDefinition(
                alias,
                resolved.Bus,
                resolved.Clip,
                resolved.Loop,
                resolved.Gain));
        }

        private static string[] ExplicitlyUnvoiced() => new[]
        {
            "voice.aster.bea.fragment",
            "voice.aster.juno.motion",
            "voice.aster.kai.trust",
            "voice.aster.mira.route",
            "voice.aster.ori.pulse",
            "voice.juno.mirra-hint",
            "voice.juno.mirra-repair",
            "voice.mira.mirra-arrival",
            "voice.ori.mirra-fragment",
            "voice.task25.unvoiced",
        };

        private static void BuildFaceSets()
        {
            foreach (var character in Characters)
            {
                var atlasPath =
                    $"Assets/_JustSomeStars/Art/2D/Characters/{character.Name}/" +
                    $"Atlases/neutral/{character.Id}-face-speech.png";
                var manifest = CharacterSpritePostprocessor.LoadValidatedManifest(
                    atlasPath,
                    Path.GetDirectoryName(Application.dataPath));
                var sprites = AssetDatabase.LoadAllAssetsAtPath(atlasPath)
                    .OfType<Sprite>()
                    .ToDictionary(sprite => sprite.name, StringComparer.Ordinal);
                var clips = manifest.clips.Select(authored =>
                {
                    var clip = ScriptableObject.CreateInstance<
                        SpriteAnimationClipDefinition>();
                    clip.name = authored.id;
                    clip.Configure(
                        authored.id,
                        Enum.Parse<SpriteFacing>(authored.facing),
                        Enum.Parse<SpriteAnimationLoopMode>(authored.loopMode),
                        authored.frames.Select(frame =>
                            sprites.TryGetValue(frame.spriteName, out var sprite)
                                ? sprite
                                : throw new InvalidDataException(
                                    $"Missing face sprite {frame.spriteName}.")).ToArray(),
                        authored.frames.Select(frame => frame.durationSeconds).ToArray(),
                        Array.Empty<SpriteFrameEvent>());
                    return clip;
                }).ToArray();

                var path = $"{FaceRoot}/{character.Name}FaceSpriteSet.asset";
                var spriteSet = GetOrCreateSpriteSet(path, $"{character.Name}FaceSpriteSet");
                DestroyOwnedClips(path);
                spriteSet.Configure(character.Id, clips);
                foreach (var clip in clips) AssetDatabase.AddObjectToAsset(clip, spriteSet);
                EditorUtility.SetDirty(spriteSet);
            }
            AssetDatabase.SaveAssets();
        }

        private static void BuildPerformanceSets(AudioCueLibrary audio)
        {
            var dialogue = AssetDatabase.FindAssets(
                    "t:DialogueEntry",
                    new[] { "Assets/_JustSomeStars/Content/Dialogue" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(Require<DialogueEntry>)
                .OrderBy(entry => entry.StableId.Value, StringComparer.Ordinal)
                .ToArray();

            foreach (var character in Characters)
            {
                var clips = new List<SpriteAnimationClipDefinition>();
                foreach (var facing in new[] { SpriteFacing.Left, SpriteFacing.Right })
                {
                    clips.Add(CreatePerformanceClip(
                        character,
                        "reaction",
                        facing,
                        "turn",
                        new[]
                        {
                            new SpriteFrameEvent(
                                0,
                                SpriteFrameEventKind.Expression,
                                "curious"),
                        }));
                    var conversationSource = SourceClip(character, "interact", facing);
                    clips.Add(CreatePerformanceClip(
                        character,
                        "conversation",
                        facing,
                        "interact",
                        new[]
                        {
                            new SpriteFrameEvent(
                                0,
                                SpriteFrameEventKind.Expression,
                                "neutral"),
                            new SpriteFrameEvent(
                                conversationSource.Frames.Count - 1,
                                SpriteFrameEventKind.InteractionRelease,
                                "conversation-complete"),
                        }));

                    foreach (var spec in Scenes.Where(spec =>
                                 character.Id == "captain" ||
                                 character.Id == spec.Speaker))
                    {
                        clips.Add(CreateCinematicPerformance(character, spec, facing));
                    }

                    foreach (var entry in dialogue.Where(entry => string.Equals(
                                 CanonicalActor(entry.SpeakerId.Value),
                                 character.Id,
                                 StringComparison.Ordinal)))
                    {
                        clips.Add(CreateDialoguePerformance(
                            character,
                            entry,
                            facing,
                            audio));
                    }
                }

                var path = $"{AnimationRoot}/{character.Name}PerformanceSpriteSet.asset";
                var set = GetOrCreateSpriteSet(
                    path,
                    $"{character.Name}PerformanceSpriteSet");
                DestroyOwnedClips(path);
                set.Configure(character.Id, clips.ToArray());
                foreach (var clip in clips) AssetDatabase.AddObjectToAsset(clip, set);
                EditorUtility.SetDirty(set);
            }
            AssetDatabase.SaveAssets();
        }

        private static SpriteAnimationClipDefinition CreateCinematicPerformance(
            CharacterSpec character,
            SceneMediaSpec spec,
            SpriteFacing facing)
        {
            var motion = character.Id == "captain"
                ? spec.Name == "Clubhouse" ? "idle" : "interact"
                : spec.Speaker == "ori" ? "scan" : "interact";
            var source = SourceClip(character, motion, facing);
            var events = new List<SpriteFrameEvent>();
            if (character.Id == spec.Speaker)
            {
                events.Add(new SpriteFrameEvent(
                    0,
                    SpriteFrameEventKind.Audio,
                    "state:" + spec.MusicState));
                events.Add(new SpriteFrameEvent(
                    0,
                    SpriteFrameEventKind.Expression,
                    spec.Expression));
                events.Add(new SpriteFrameEvent(
                    Math.Min(1, source.Frames.Count - 1),
                    SpriteFrameEventKind.Caption,
                    spec.CaptionKey));
                AddSpeechEvents(events, source.Frames.Count);
                events.Add(new SpriteFrameEvent(
                    Math.Max(0, source.Frames.Count - 2),
                    SpriteFrameEventKind.Vfx,
                    "signal-pulse"));
                events.Add(new SpriteFrameEvent(
                    source.Frames.Count - 1,
                    SpriteFrameEventKind.InteractionRelease,
                    "continue"));
            }
            return CreatePerformanceClip(
                character,
                spec.StableId,
                facing,
                motion,
                events);
        }

        private static SpriteAnimationClipDefinition CreateDialoguePerformance(
            CharacterSpec character,
            DialogueEntry entry,
            SpriteFacing facing,
            AudioCueLibrary audio)
        {
            var motion = MotionForGesture(entry.Gesture);
            var source = SourceClip(character, motion, facing);
            var events = new List<SpriteFrameEvent>
            {
                new(0, SpriteFrameEventKind.Expression, entry.Expression),
                new(
                    Math.Min(1, source.Frames.Count - 1),
                    SpriteFrameEventKind.Caption,
                    entry.LocalizationKey),
            };
            if (audio.TryFindCue(entry.VoiceReference, out _))
            {
                events.Add(new SpriteFrameEvent(
                    0,
                    SpriteFrameEventKind.Audio,
                    entry.VoiceReference));
            }
            if (motion == "scan")
            {
                events.Add(new SpriteFrameEvent(
                    Math.Max(0, source.Frames.Count - 2),
                    SpriteFrameEventKind.Vfx,
                    "signal-pulse"));
            }
            AddSpeechEvents(events, source.Frames.Count);
            events.Add(new SpriteFrameEvent(
                source.Frames.Count - 1,
                SpriteFrameEventKind.InteractionRelease,
                "dialogue-complete"));
            return CreatePerformanceClip(
                character,
                "dialogue." + entry.StableId.Value,
                facing,
                motion,
                events);
        }

        private static SpriteAnimationClipDefinition CreatePerformanceClip(
            CharacterSpec character,
            string semanticId,
            SpriteFacing facing,
            string motion,
            IEnumerable<SpriteFrameEvent> authoredEvents)
        {
            var source = SourceClip(character, motion, facing);
            var events = source.FrameEvents
                .Concat(authoredEvents ?? Array.Empty<SpriteFrameEvent>())
                .GroupBy(frameEvent => (
                    frameEvent.FrameIndex,
                    frameEvent.Kind,
                    frameEvent.Id))
                .Select(group => group.First())
                .OrderBy(frameEvent => frameEvent.FrameIndex)
                .ThenBy(frameEvent => (int)frameEvent.Kind)
                .ThenBy(frameEvent => frameEvent.Id, StringComparer.Ordinal)
                .ToArray();
            var clip = ScriptableObject.CreateInstance<
                SpriteAnimationClipDefinition>();
            clip.name = $"{character.Id}.{semanticId}.{FacingId(facing)}";
            clip.Configure(
                clip.name,
                facing,
                SpriteAnimationLoopMode.Once,
                source.Frames.ToArray(),
                source.FrameDurations.ToArray(),
                events,
                source.FrameContacts.ToArray(),
                motion);
            return clip;
        }

        private static SpriteAnimationClipDefinition SourceClip(
            CharacterSpec character,
            string motion,
            SpriteFacing facing)
        {
            if (character.Id == "captain")
            {
                return Require<CaptainSpriteSet>(
                    "Assets/_JustSomeStars/Content/Characters/CaptainSpriteSet.asset")
                    .FindClip(
                        CaptainBodyFamily.Average,
                        facing,
                        CaptainSpriteLayer.BodyBase,
                        motion);
            }
            return Require<CharacterSpriteSet>(
                    $"Assets/_JustSomeStars/Content/Characters/" +
                    $"{character.Name}SpriteSet.asset")
                .FindClip($"{character.Id}.{motion}.{FacingId(facing)}");
        }

        private static string MotionForGesture(string gesture)
        {
            var canonical = gesture?.Trim().ToLowerInvariant() ?? string.Empty;
            if (canonical.Contains("scan", StringComparison.Ordinal) ||
                canonical.Contains("instrument", StringComparison.Ordinal) ||
                canonical.Contains("probe", StringComparison.Ordinal) ||
                canonical.Contains("pulse", StringComparison.Ordinal))
            {
                return "scan";
            }
            return canonical == "nod" ? "turn" : "interact";
        }

        private static void AddSpeechEvents(
            ICollection<SpriteFrameEvent> events,
            int frameCount)
        {
            if (frameCount <= 0) return;
            var indices = new[]
            {
                Math.Min(1, frameCount - 1),
                Math.Max(0, frameCount / 2),
                Math.Max(0, frameCount - 2),
            };
            for (var index = 0; index < indices.Length; index++)
            {
                events.Add(new SpriteFrameEvent(
                    indices[index],
                    SpriteFrameEventKind.Viseme,
                    (index * 2).ToString()));
            }
        }

        private static void PatchScene(SceneMediaSpec spec)
        {
            var scene = EditorSceneManager.OpenScene(spec.ScenePath, OpenSceneMode.Single);
            var english = Require<LocalizedEnglishCatalog>(EnglishAsset);
            var font = Require<TMP_FontAsset>(StandardFont);
            var sequence = FindInScene<ChapterOneSequenceController2D>(scene)
                .SingleOrDefault() ?? throw new InvalidOperationException(
                    $"{spec.ScenePath} is missing ChapterOneSequence.");
            var canvas = FindInScene<Canvas>(scene).FirstOrDefault() ??
                throw new InvalidOperationException($"{spec.ScenePath} has no Canvas.");
            var fallback = FindInScene<SpriteRenderer>(scene).FirstOrDefault(renderer =>
                renderer.name == "Sky" && renderer.sprite != null) ??
                FindInScene<SpriteRenderer>(scene).FirstOrDefault(renderer =>
                    renderer.sprite != null) ?? throw new InvalidOperationException(
                    $"{spec.ScenePath} has no authored fallback still.");
            var definition = BuildSequence(spec, fallback.sprite);

            DestroyNamed(scene, "Task29CinematicPresentation");
            var presentation = NewUi("Task29CinematicPresentation", canvas.transform);
            Stretch(presentation.GetComponent<RectTransform>());
            var captionPanel = NewUi(
                "CinematicCaption",
                presentation.transform,
                typeof(Image));
            SetRect(
                captionPanel.GetComponent<RectTransform>(),
                new Vector2(0.18f, 0.035f),
                new Vector2(0.82f, 0.255f));
            captionPanel.GetComponent<Image>().color =
                new Color(0.025f, 0.04f, 0.09f, 0.9f);
            var portrait = CreatePortrait(
                captionPanel.transform,
                "Portrait",
                new Vector2(0.015f, 0.08f),
                new Vector2(0.205f, 0.92f));
            var speech = CreatePortrait(
                captionPanel.transform,
                "SpeechOverlay",
                new Vector2(0.015f, 0.08f),
                new Vector2(0.205f, 0.92f));
            var speaker = NewText(
                "Speaker",
                captionPanel.transform,
                font,
                24f,
                new Vector2(0.23f, 0.58f),
                new Vector2(0.97f, 0.93f),
                new Color(0.42f, 0.9f, 1f, 1f));
            var caption = NewText(
                "Caption",
                captionPanel.transform,
                font,
                30f,
                new Vector2(0.23f, 0.12f),
                new Vector2(0.97f, 0.62f),
                Color.white);
            var facial = presentation.AddComponent<FacialAtlasController2D>();
            facial.Configure(LoadFaceSets(), portrait, speech);

            var actors = BuildActors(presentation.transform, scene);
            var director = presentation.AddComponent<CinematicDirector>();
            director.ConfigureAuthored(
                definition,
                english,
                fallback,
                captionPanel,
                speaker,
                caption,
                facial,
                actors);
            captionPanel.SetActive(false);
            SetChapterExtensions(sequence, director);
            SaveScene(scene);
        }

        private static void PatchDestination(DestinationMediaSpec spec)
        {
            var scene = EditorSceneManager.OpenScene(spec.ScenePath, OpenSceneMode.Single);
            DestroyNamed(scene, "Task29MusicState");
            var musicObject = new GameObject("Task29MusicState");
            SceneManager.MoveGameObjectToScene(musicObject, scene);
            musicObject.AddComponent<MusicStatePresenter2D>().Configure(spec.MusicState);

            var presenters = FindInScene<MirraDialoguePresenter2D>(scene);
            if (spec.HasDialogue != (presenters.Length > 0))
            {
                throw new InvalidOperationException(
                    $"{spec.ScenePath} dialogue route changed unexpectedly.");
            }
            foreach (var presenter in presenters)
            {
                var serialized = new SerializedObject(presenter);
                var panel = serialized.FindProperty("panel")?.objectReferenceValue
                    as GameObject ?? throw new InvalidOperationException(
                    $"{spec.ScenePath} dialogue panel is missing.");
                var body = serialized.FindProperty("bodyLabel")?.objectReferenceValue
                    as TMP_Text ?? throw new InvalidOperationException(
                    $"{spec.ScenePath} dialogue body is missing.");
                var speaker = serialized.FindProperty("speakerLabel")?.objectReferenceValue
                    as TMP_Text ?? throw new InvalidOperationException(
                    $"{spec.ScenePath} dialogue speaker is missing.");
                foreach (Transform child in panel.transform)
                {
                    if (child.name == "Task29DialogueMedia")
                        UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
                var media = NewUi("Task29DialogueMedia", panel.transform);
                Stretch(media.GetComponent<RectTransform>());
                media.transform.SetAsFirstSibling();
                var portrait = CreatePortrait(
                    media.transform,
                    "Portrait",
                    new Vector2(0.015f, 0.08f),
                    new Vector2(0.205f, 0.92f));
                var speech = CreatePortrait(
                    media.transform,
                    "SpeechOverlay",
                    new Vector2(0.015f, 0.08f),
                    new Vector2(0.205f, 0.92f));
                var facial = media.AddComponent<FacialAtlasController2D>();
                facial.Configure(LoadFaceSets(), portrait, speech);
                var actors = BuildActors(media.transform, scene);
                presenter.ConfigureMedia(
                    Require<LocalizedEnglishCatalog>(EnglishAsset),
                    facial,
                    actors);
                ShiftTextForPortrait(speaker.rectTransform, 0.23f);
                ShiftTextForPortrait(body.rectTransform, 0.23f);
            }
            SaveScene(scene);
        }

        private static CinematicActor2D[] BuildActors(Transform root, Scene scene)
        {
            var result = new List<CinematicActor2D>();
            var captains = FindInScene<LayeredCharacterRenderer>(scene);
            if (captains.Length > 1)
                throw new InvalidOperationException($"{scene.path} has multiple Captains.");
            if (captains.Length == 1)
            {
                var binding = new GameObject("Actor-captain")
                    .AddComponent<CinematicActor2D>();
                binding.transform.SetParent(root, false);
                binding.Configure(
                    "captain",
                    captains[0],
                    null,
                    null,
                    PerformanceSet("Captain"));
                result.Add(binding);
            }

            foreach (var character in Characters.Where(item => item.Id != "captain"))
            {
                var anchor = scene.GetRootGameObjects()
                    .SelectMany(item => item.GetComponentsInChildren<Transform>(true))
                    .FirstOrDefault(item => item.name == character.Name &&
                        item.GetComponentInChildren<SpriteAtlasAnimator>(true) != null);
                var animator = anchor?.GetComponentInChildren<SpriteAtlasAnimator>(true);
                if (animator == null) continue;
                var bindingObject = new GameObject($"Actor-{character.Id}");
                bindingObject.transform.SetParent(root, false);
                var binding = bindingObject.AddComponent<CinematicActor2D>();
                binding.Configure(
                    character.Id,
                    null,
                    animator,
                    Require<CharacterSpriteSet>(
                        $"Assets/_JustSomeStars/Content/Characters/" +
                        $"{character.Name}SpriteSet.asset"),
                    PerformanceSet(character.Name));
                result.Add(binding);
            }
            return result.ToArray();
        }

        private static CinematicSequenceDefinition BuildSequence(
            SceneMediaSpec spec,
            Sprite fallback)
        {
            var path = $"{CinematicRoot}/{spec.Name}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<CinematicSequenceDefinition>(path);
            if (definition == null)
            {
                if (File.Exists(Absolute(path))) AssetDatabase.DeleteAsset(path);
                definition = ScriptableObject.CreateInstance<CinematicSequenceDefinition>();
                definition.name = spec.Name;
                AssetDatabase.CreateAsset(definition, path);
            }
            definition.Configure(
                spec.StableId,
                null,
                fallback,
                new[]
                {
                    new CinematicBeatDefinition(
                        0f,
                        CinematicBeatKind.BodyClip,
                        "captain",
                        spec.StableId,
                        0f),
                    new CinematicBeatDefinition(
                        0f,
                        CinematicBeatKind.BodyClip,
                        spec.Speaker,
                        spec.StableId,
                        0f),
                });
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void SetChapterExtensions(
            ChapterOneSequenceController2D sequence,
            CinematicDirector director)
        {
            var serialized = new SerializedObject(sequence);
            var property = serialized.FindProperty("playerUiExtensions") ??
                throw new InvalidOperationException(
                    "Chapter One extension field changed.");
            var retained = new List<UnityEngine.Object>();
            for (var index = 0; index < property.arraySize; index++)
            {
                var value = property.GetArrayElementAtIndex(index).objectReferenceValue;
                if (value != null && value is not CinematicDirector) retained.Add(value);
            }
            retained.Add(director);
            property.arraySize = retained.Count;
            for (var index = 0; index < retained.Count; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = retained[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sequence);
        }

        private static void ValidatePersisted()
        {
            foreach (var set in LoadFaceSets())
            {
                foreach (var expression in FacialAtlasController2D.RequiredExpressions)
                    _ = set.FindClip($"{set.CharacterId}.expression.{expression}");
                for (var index = 0; index < FacialAtlasController2D.VisemeCount; index++)
                    _ = set.FindClip($"{set.CharacterId}.speech.{index}");
            }
            foreach (var character in Characters)
            {
                var set = PerformanceSet(character.Name);
                _ = set.FindClip($"{character.Id}.reaction.left");
                _ = set.FindClip($"{character.Id}.reaction.right");
                _ = set.FindClip($"{character.Id}.conversation.left");
                _ = set.FindClip($"{character.Id}.conversation.right");
                foreach (var clip in set.Clips) clip.ValidateOrThrow();
            }
            foreach (var spec in Scenes)
            {
                var scene = EditorSceneManager.OpenScene(spec.ScenePath, OpenSceneMode.Single);
                var directors = FindInScene<CinematicDirector>(scene);
                if (directors.Length != 1 || directors[0].SequenceId != spec.StableId ||
                    !directors[0].HasFrameEventReleaseRoute)
                {
                    throw new InvalidOperationException(
                        $"{spec.ScenePath} did not persist one frame-driven director.");
                }
                directors[0].ValidateAuthoredOrThrow();
            }
            foreach (var spec in Destinations)
            {
                var scene = EditorSceneManager.OpenScene(spec.ScenePath, OpenSceneMode.Single);
                var music = FindInScene<MusicStatePresenter2D>(scene);
                if (music.Length != 1 || music[0].MusicStateId != spec.MusicState)
                {
                    throw new InvalidOperationException(
                        $"{spec.ScenePath} did not persist its music state.");
                }
                music[0].ValidateOrThrow();
                foreach (var presenter in FindInScene<MirraDialoguePresenter2D>(scene))
                    presenter.ValidateMediaOrThrow();
            }
        }

        private static CharacterSpriteSet PerformanceSet(string name) =>
            Require<CharacterSpriteSet>(
                $"{AnimationRoot}/{name}PerformanceSpriteSet.asset");

        private static CharacterSpriteSet[] LoadFaceSets() => Characters
            .Select(character => Require<CharacterSpriteSet>(
                $"{FaceRoot}/{character.Name}FaceSpriteSet.asset"))
            .ToArray();

        private static CharacterSpriteSet GetOrCreateSpriteSet(
            string path,
            string name)
        {
            var set = AssetDatabase.LoadAssetAtPath<CharacterSpriteSet>(path);
            if (set != null) return set;
            if (File.Exists(Absolute(path))) AssetDatabase.DeleteAsset(path);
            set = ScriptableObject.CreateInstance<CharacterSpriteSet>();
            set.name = name;
            AssetDatabase.CreateAsset(set, path);
            return set;
        }

        private static void DestroyOwnedClips(string path)
        {
            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path)
                         .OfType<SpriteAnimationClipDefinition>())
            {
                UnityEngine.Object.DestroyImmediate(clip, true);
            }
        }

        private static Image CreatePortrait(
            Transform parent,
            string name,
            Vector2 min,
            Vector2 max)
        {
            var root = NewUi(name, parent, typeof(Image));
            SetRect(root.GetComponent<RectTransform>(), min, max);
            var image = root.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        private static TMP_Text NewText(
            string name,
            Transform parent,
            TMP_FontAsset font,
            float size,
            Vector2 min,
            Vector2 max,
            Color color)
        {
            var root = NewUi(name, parent, typeof(TextMeshProUGUI));
            SetRect(root.GetComponent<RectTransform>(), min, max);
            var text = root.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        private static GameObject NewUi(
            string name,
            Transform parent,
            params Type[] components)
        {
            var all = new List<Type> { typeof(RectTransform) };
            all.AddRange(components.Where(type => type != typeof(RectTransform)));
            var root = new GameObject(name, all.ToArray());
            root.transform.SetParent(parent, false);
            return root;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Stretch(RectTransform rect) =>
            SetRect(rect, Vector2.zero, Vector2.one);

        private static void ShiftTextForPortrait(RectTransform rect, float minimumX)
        {
            if (rect == null) return;
            var min = rect.anchorMin;
            min.x = Mathf.Max(min.x, minimumX);
            rect.anchorMin = min;
        }

        private static void DestroyNamed(Scene scene, string name)
        {
            foreach (var target in scene.GetRootGameObjects()
                         .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                         .Where(item => item.name == name)
                         .ToArray())
            {
                UnityEngine.Object.DestroyImmediate(target.gameObject);
            }
        }

        private static void SaveScene(Scene scene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException($"Failed to save {scene.path}.");
        }

        private static string CanonicalActor(string actorId)
        {
            var canonical = actorId.Trim().ToLowerInvariant();
            if (canonical.StartsWith("crew.", StringComparison.Ordinal))
                canonical = canonical.Substring("crew.".Length);
            if (canonical.StartsWith("robot.", StringComparison.Ordinal))
                canonical = canonical.Substring("robot.".Length);
            return canonical;
        }

        private static string FacingId(SpriteFacing facing) => facing switch
        {
            SpriteFacing.Left => "left",
            SpriteFacing.Right => "right",
            _ => throw new ArgumentOutOfRangeException(nameof(facing)),
        };

        private static T Require<T>(string path) where T : UnityEngine.Object
        {
            var value = AssetDatabase.LoadAssetAtPath<T>(path);
            return value != null
                ? value
                : throw new InvalidOperationException(
                    $"Task 29 requires {typeof(T).Name} at {path}.");
        }

        private static string Absolute(string assetPath) => Path.Combine(
            Path.GetDirectoryName(Application.dataPath) ??
                throw new InvalidOperationException("Project root is unavailable."),
            assetPath);

        private static T[] FindInScene<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Folder path is required.", nameof(path));
            var segments = path.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = $"{current}/{segments[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }

        [Serializable]
        private sealed class AudioPackageManifest
        {
            public int schemaVersion;
            public AudioFileRecord[] files;
            public MusicStateRecord[] musicStates;
        }

        [Serializable]
        private sealed class AudioFileRecord
        {
            public string path;
            public string cueId;
            public string bus;
            public bool loop;
        }

        [Serializable]
        private sealed class MusicStateRecord
        {
            public string stateId;
            public string foundationCueId;
            public string signalStemCueId;
            public float signalLevel;
            public float crossfadeSeconds;
        }

        private sealed class CharacterSpec
        {
            public CharacterSpec(string name, string id)
            {
                Name = name;
                Id = id;
            }

            public string Name { get; }
            public string Id { get; }
        }

        private sealed class SceneMediaSpec
        {
            public SceneMediaSpec(
                string name,
                string scenePath,
                string stableId,
                string musicState,
                string speaker,
                string captionKey,
                string expression)
            {
                Name = name;
                ScenePath = scenePath;
                StableId = stableId;
                MusicState = musicState;
                Speaker = speaker;
                CaptionKey = captionKey;
                Expression = expression;
            }

            public string Name { get; }
            public string ScenePath { get; }
            public string StableId { get; }
            public string MusicState { get; }
            public string Speaker { get; }
            public string CaptionKey { get; }
            public string Expression { get; }
        }

        private sealed class DestinationMediaSpec
        {
            public DestinationMediaSpec(
                string scenePath,
                string musicState,
                bool hasDialogue)
            {
                ScenePath = scenePath;
                MusicState = musicState;
                HasDialogue = hasDialogue;
            }

            public string ScenePath { get; }
            public string MusicState { get; }
            public bool HasDialogue { get; }
        }
    }
}
