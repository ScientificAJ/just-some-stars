using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JustSomeStars.Editor.Importers;
using JustSomeStars.Editor.Validation;
using JustSomeStars.Runtime.Animation2D;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace JustSomeStars.Editor.Build
{
    public static class CrewSpriteAssetBuilder
    {
        private const string CharacterRoot =
            "Assets/_JustSomeStars/Art/2D/Characters";
        private const string ContentRoot =
            "Assets/_JustSomeStars/Content/Characters";
        private const string GroupName = "Characters-Crew";
        private static readonly (string Name, string Id)[] Characters =
        {
            ("Mira", "mira"),
            ("Juno", "juno"),
            ("Kai", "kai"),
            ("Bea", "bea"),
            ("Ori", "ori"),
        };

        public static void Apply()
        {
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                EnsureFolder(ContentRoot);
                var addressables = AddressableAssetSettingsDefaultObject.Settings ??
                    throw new InvalidOperationException(
                        "Committed Addressables settings are missing.");
                var group = GetOrCreateGroup(addressables);

                foreach (var character in Characters)
                {
                    BuildCharacter(character.Name, character.Id, addressables, group);
                }

                addressables.SetDirty(
                    AddressableAssetSettings.ModificationEvent.BatchModification,
                    null,
                    postEvent: true,
                    settingsModified: true);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("[JSS Task 12 Stage 4] Crew sprite assets built and validated.");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                else
                {
                    throw;
                }
            }
        }

        private static void BuildCharacter(
            string displayName,
            string characterId,
            AddressableAssetSettings addressables,
            AddressableAssetGroup group)
        {
            var manifests = new List<CharacterSpriteManifest>();
            var clips = new List<SpriteAnimationClipDefinition>();
            var tracks = new List<SpriteClipAnchorTrack>();
            foreach (var facing in new[] { "right", "left" })
            {
                var atlasPath =
                    $"{CharacterRoot}/{displayName}/Atlases/{facing}/" +
                    $"{characterId}-{facing}.png";
                var manifest = CharacterSpritePostprocessor.LoadValidatedManifest(
                    atlasPath,
                    Path.GetDirectoryName(Application.dataPath));
                manifests.Add(manifest);
                var sprites = AssetDatabase.LoadAllAssetsAtPath(atlasPath)
                    .OfType<Sprite>()
                    .ToDictionary(sprite => sprite.name, StringComparer.Ordinal);

                foreach (var manifestClip in manifest.clips)
                {
                    var clip = ScriptableObject.CreateInstance<SpriteAnimationClipDefinition>();
                    clip.name = manifestClip.id;
                    clip.Configure(
                        manifestClip.id,
                        Enum.Parse<SpriteFacing>(manifestClip.facing),
                        Enum.Parse<SpriteAnimationLoopMode>(manifestClip.loopMode),
                        manifestClip.frames.Select(frame =>
                            sprites.TryGetValue(frame.spriteName, out var sprite)
                                ? sprite
                                : throw new InvalidDataException(
                                    $"Imported sprite {frame.spriteName} is missing."))
                            .ToArray(),
                        manifestClip.frames.Select(frame => frame.durationSeconds).ToArray(),
                        manifestClip.frames.SelectMany((frame, frameIndex) =>
                            frame.events.Select(frameEvent => new SpriteFrameEvent(
                                frameIndex,
                                Enum.Parse<SpriteFrameEventKind>(frameEvent.kind),
                                frameEvent.id)))
                            .ToArray(),
                        manifestClip.frames.SelectMany((frame, frameIndex) =>
                            frame.contacts.Select(contact =>
                                new SpriteFrameContact(frameIndex, contact)))
                            .ToArray());
                    clips.Add(clip);
                    tracks.Add(new SpriteClipAnchorTrack(
                        manifestClip.id,
                        manifestClip.frames.Select(frame =>
                            new SpriteFrameAnchorRow(frame.anchors.Select(anchor =>
                                new SpriteFrameAnchor(
                                    anchor.id,
                                    new Vector2(
                                        anchor.runtimePixels[0],
                                        anchor.runtimePixels[1]),
                                    anchor.isAuthoredVisible))
                                .ToArray()))
                            .ToArray()));
                }
            }

            var assetPath = $"{ContentRoot}/{displayName}SpriteSet.asset";
            if (AssetDatabase.LoadAssetAtPath<CharacterSpriteSet>(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
            var spriteSet = ScriptableObject.CreateInstance<CharacterSpriteSet>();
            spriteSet.name = $"{displayName}SpriteSet";
            spriteSet.Configure(characterId, clips.ToArray(), tracks.ToArray());
            AssetDatabase.CreateAsset(spriteSet, assetPath);
            foreach (var clip in clips)
            {
                AssetDatabase.AddObjectToAsset(clip, spriteSet);
            }
            EditorUtility.SetDirty(spriteSet);
            AssetDatabase.SaveAssets();

            CrewSpriteSetValidator.ValidateOrThrow(spriteSet, manifests);
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                throw new InvalidDataException($"No GUID for {assetPath}.");
            }
            var entry = addressables.CreateOrMoveEntry(
                guid,
                group,
                readOnly: false,
                postEvent: false);
            entry.address = $"Characters/Crew/{characterId}";
            entry.SetLabel("Characters-Crew", true, true, false);
        }

        private static AddressableAssetGroup GetOrCreateGroup(
            AddressableAssetSettings settings)
        {
            var group = settings.FindGroup(GroupName);
            if (group != null)
            {
                return group;
            }
            return settings.CreateGroup(
                GroupName,
                setAsDefaultGroup: false,
                readOnly: false,
                postEvent: false,
                schemasToCopy: null,
                typeof(BundledAssetGroupSchema),
                typeof(ContentUpdateGroupSchema));
        }

        private static void EnsureFolder(string path)
        {
            var segments = path.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = $"{current}/{segments[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }
                current = next;
            }
        }
    }
}
