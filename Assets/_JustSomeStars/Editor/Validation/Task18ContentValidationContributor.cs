using System;
using System.Collections.Generic;
using System.Linq;
using JustSomeStars.Runtime.Atlas;
using JustSomeStars.Runtime.Dialogue;
using JustSomeStars.Runtime.Discovery;
using JustSomeStars.Runtime.Missions;
using UnityEditor;

namespace JustSomeStars.Editor.Validation
{
    internal sealed class Task18ContentValidationContributor :
        IProjectContentValidationContributor
    {
        public void Contribute(ProjectContentIndexBuilder builder)
        {
            var localization = LoadAssets<LocalizedEnglishCatalog>();
            foreach (var catalog in localization)
            {
                TryContribute(
                    builder,
                    catalog.Path,
                    () => catalog.Asset.ValidateOrThrow());
            }

            var localizationKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var catalog in localization)
            {
                foreach (var key in CollectLocalizationKeys(catalog.Asset))
                {
                    localizationKeys.Add(key);
                }
            }

            foreach (var source in LoadAssets<ScienceSourceDefinition>())
            {
                TryContribute(builder, source.Path, () =>
                {
                    source.Asset.ValidateOrThrow();
                    builder.AddDefinition(
                        source.Asset.StableId,
                        ContentKind.ScienceSource,
                        source.Path);
                });
            }

            foreach (var phenomenon in LoadAssets<PhenomenonDefinition>())
            {
                TryContribute(builder, phenomenon.Path, () =>
                {
                    phenomenon.Asset.ValidateOrThrow();
                    builder
                        .AddDefinition(
                            phenomenon.Asset.StableId,
                            ContentKind.Phenomenon,
                            phenomenon.Path)
                        .AddScienceSource(
                            phenomenon.Asset.StableId,
                            phenomenon.Asset.ScienceSourceId,
                            phenomenon.Path);
                });
            }

            var phenomenonIds = new HashSet<string>(
                LoadAssets<PhenomenonDefinition>()
                    .Select(record => record.Asset.StableId.Value),
                StringComparer.Ordinal);
            var instrumentIds = new HashSet<string>(
                LoadAssets<InstrumentDefinition>()
                    .Select(record => record.Asset.StableId.Value),
                StringComparer.Ordinal);
            var predictionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var content in LoadAssets<Task18ProgressionContent>())
            {
                TryContribute(builder, content.Path, () =>
                {
                    content.Asset.ValidateOrThrow();
                    foreach (var id in content.Asset.PredictionIds)
                    {
                        predictionIds.Add(id.Value);
                    }
                });
            }

            var dialogueIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var dialogue in LoadAssets<DialogueEntry>())
            {
                TryContribute(builder, dialogue.Path, () =>
                {
                    dialogue.Asset.ValidateOrThrow();
                    dialogueIds.Add(dialogue.Asset.StableId.Value);
                    builder.AddDefinition(
                        dialogue.Asset.StableId,
                        ContentKind.Dialogue,
                        dialogue.Path);
                    RequireLocalization(
                        builder,
                        localizationKeys,
                        dialogue.Asset.LocalizationKey,
                        dialogue.Path);
                });
            }

            foreach (var mission in LoadAssets<MissionDefinition>())
            {
                TryContribute(builder, mission.Path, () =>
                {
                    mission.Asset.ValidateOrThrow();
                    builder.AddDefinition(
                        mission.Asset.StableId,
                        ContentKind.Mission,
                        mission.Path);
                    foreach (var dialogueId in mission.Asset.Nodes
                                 .SelectMany(node => node.DialogueIds)
                                 .Distinct(StringComparer.Ordinal))
                    {
                        builder.AddReference(
                            mission.Asset.StableId,
                            new JustSomeStars.Runtime.Core.ContentId(dialogueId),
                            ContentReferenceKind.DialogueReference,
                            mission.Path);
                    }

                    foreach (var requirement in mission.Asset.Nodes
                                 .SelectMany(node => node.Requirements))
                    {
                        var known = requirement.EventKind switch
                        {
                            MissionEventKind.PhenomenonObserved =>
                                phenomenonIds.Contains(requirement.PayloadId.Value),
                            MissionEventKind.InstrumentUsed =>
                                instrumentIds.Contains(requirement.PayloadId.Value),
                            MissionEventKind.PredictionRecorded =>
                                predictionIds.Contains(requirement.PayloadId.Value),
                            MissionEventKind.ConversationCompleted =>
                                dialogueIds.Contains(requirement.PayloadId.Value),
                            _ => true,
                        };
                        if (!known)
                        {
                            builder.AddIssue(new ValidationIssue(
                                ValidationIssueCode.InvalidContentAsset,
                                $"Mission '{mission.Asset.StableId}' has an unresolved " +
                                $"{requirement.EventKind} payload '{requirement.PayloadId}'.",
                                mission.Path));
                        }
                    }
                });
            }

            foreach (var atlas in LoadAssets<AtlasEntry>())
            {
                TryContribute(builder, atlas.Path, () =>
                {
                    atlas.Asset.ValidateOrThrow();
                    builder
                        .AddDefinition(
                            atlas.Asset.StableId,
                            ContentKind.AtlasEntry,
                            atlas.Path)
                        .AddScienceSource(
                            atlas.Asset.StableId,
                            atlas.Asset.ScienceSourceId,
                            atlas.Path);
                    RequireLocalization(builder, localizationKeys, atlas.Asset.ShortTextKey, atlas.Path);
                    RequireLocalization(builder, localizationKeys, atlas.Asset.BalancedTextKey, atlas.Path);
                    RequireLocalization(builder, localizationKeys, atlas.Asset.DeepTextKey, atlas.Path);
                });
            }
        }

        private static IEnumerable<string> CollectLocalizationKeys(
            LocalizedEnglishCatalog catalog)
        {
            var serialized = new SerializedObject(catalog);
            var entries = serialized.FindProperty("entries");
            for (var index = 0; index < entries.arraySize; index++)
            {
                yield return entries.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("key").stringValue;
            }
        }

        private static void RequireLocalization(
            ProjectContentIndexBuilder builder,
            ISet<string> keys,
            string key,
            string path)
        {
            if (!keys.Contains(key))
            {
                builder.AddIssue(new ValidationIssue(
                    ValidationIssueCode.MissingLocalization,
                    $"Player-facing localization key '{key}' has no English value.",
                    path));
            }
        }

        private static AssetRecord<T>[] LoadAssets<T>() where T : UnityEngine.Object
        {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { "Assets/_JustSomeStars/Content" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => new AssetRecord<T>(
                    path,
                    AssetDatabase.LoadAssetAtPath<T>(path)))
                .Where(record => record.Asset != null)
                .ToArray();
        }

        private static void TryContribute(
            ProjectContentIndexBuilder builder,
            string path,
            Action contribution)
        {
            try
            {
                contribution();
            }
            catch (Exception exception)
            {
                builder.AddIssue(new ValidationIssue(
                    ValidationIssueCode.InvalidContentAsset,
                    exception.Message,
                    path));
            }
        }

        private readonly struct AssetRecord<T> where T : UnityEngine.Object
        {
            public AssetRecord(string path, T asset)
            {
                Path = path;
                Asset = asset;
            }

            public string Path { get; }
            public T Asset { get; }
        }
    }
}
