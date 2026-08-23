using System;
using System.Collections.Generic;
using System.Linq;
using JustSomeStars.Runtime.Core;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Build;
using UnityEngine;

namespace JustSomeStars.Editor.Validation
{
    public static class ProjectContentValidator
    {
        private static readonly ContentBodyFamily[] s_RequiredBodyFamilies =
            (ContentBodyFamily[])Enum.GetValues(typeof(ContentBodyFamily));

        public static ValidationReport Validate(ProjectContentIndex index)
        {
            if (index == null)
            {
                throw new ArgumentNullException(nameof(index));
            }

            var issues = new List<ValidationIssue>(index.SeedIssues);
            var definitions = BuildDefinitionIndex(index.Definitions, issues);
            ValidateRequiredBindings(index, definitions, issues);
            ValidateReferences(index.References, definitions, issues);
            ValidateScienceSources(index.ScienceSources, definitions, issues);
            ValidateAddressables(index, definitions, issues);
            ValidateCosmeticFits(index.CosmeticFits, definitions, issues);
            ValidateStoreEntitlements(
                index.StoreEntitlements,
                definitions,
                issues);
            return new ValidationReport(issues);
        }

        public static ValidationReport ValidateProject()
        {
            var builder = new ProjectContentIndexBuilder();
            var contributorTypes =
                TypeCache.GetTypesDerivedFrom<IProjectContentValidationContributor>()
                    .Where(type => !type.IsAbstract && !type.IsInterface)
                    .OrderBy(type => type.FullName, StringComparer.Ordinal)
                    .ToArray();
            foreach (var contributorType in contributorTypes)
            {
                try
                {
                    var contributor =
                        (IProjectContentValidationContributor)Activator.CreateInstance(
                            contributorType,
                            nonPublic: true);
                    contributor.Contribute(builder);
                }
                catch (Exception exception)
                {
                    builder.AddIssue(new ValidationIssue(
                        ValidationIssueCode.ContributorFailure,
                        $"Validation contributor '{contributorType.FullName}' failed: " +
                        exception.Message,
                        contributorType.FullName));
                }
            }

            return Validate(builder.Build());
        }

        public static void ValidateForCi()
        {
            var report = ValidateProject();
            if (!report.IsValid)
            {
                foreach (var issue in report.Issues)
                {
                    Debug.LogError(
                        $"[JSS Content Validation] {issue.Code}: {issue.Message} " +
                        $"({issue.AssetPath})");
                }

                throw new BuildFailedException(
                    $"Project content validation failed with " +
                    $"{report.Issues.Count} error(s).");
            }

            Debug.Log("[JSS Content Validation] Project content is valid.");
        }

        private static Dictionary<ContentId, List<ContentDefinition>>
            BuildDefinitionIndex(
                IReadOnlyList<ContentDefinition> definitions,
                ICollection<ValidationIssue> issues)
        {
            var byId = new Dictionary<ContentId, List<ContentDefinition>>();
            foreach (var definition in definitions)
            {
                if (!byId.TryGetValue(definition.Id, out var matches))
                {
                    matches = new List<ContentDefinition>();
                    byId.Add(definition.Id, matches);
                }

                matches.Add(definition);
            }

            foreach (var entry in byId.OrderBy(
                         pair => pair.Key.Value,
                         StringComparer.Ordinal))
            {
                if (entry.Value.Count <= 1)
                {
                    continue;
                }

                var paths = string.Join(
                    ", ",
                    entry.Value.Select(definition => definition.AssetPath));
                issues.Add(new ValidationIssue(
                    ValidationIssueCode.DuplicateContentId,
                    $"Content ID '{entry.Key}' is defined {entry.Value.Count} " +
                    $"times: {paths}.",
                    entry.Value[0].AssetPath));
            }

            return byId;
        }

        private static void ValidateRequiredBindings(
            ProjectContentIndex index,
            IReadOnlyDictionary<ContentId, List<ContentDefinition>> definitions,
            ICollection<ValidationIssue> issues)
        {
            var scienceOwners = new HashSet<ContentId>(
                index.ScienceSources.Select(binding => binding.OwnerId));
            var addressableOwners = new HashSet<ContentId>(
                index.Addressables.Select(binding => binding.OwnerId));
            var cosmeticOwners = new HashSet<ContentId>(
                index.CosmeticFits.Select(binding => binding.CosmeticId));
            var storeProducts = new HashSet<ContentId>(
                index.StoreEntitlements.Select(binding => binding.ProductId));

            foreach (var entry in definitions.OrderBy(
                         pair => pair.Key.Value,
                         StringComparer.Ordinal))
            {
                foreach (var definition in entry.Value
                             .GroupBy(candidate => candidate.Kind)
                             .Select(group => group.First()))
                {
                    switch (definition.Kind)
                    {
                        case ContentKind.Phenomenon
                            when !scienceOwners.Contains(definition.Id):
                            issues.Add(new ValidationIssue(
                                ValidationIssueCode.MissingScienceSource,
                                $"Phenomenon '{definition.Id}' has no science " +
                                "source binding.",
                                definition.AssetPath));
                            break;
                        case ContentKind.Destination
                            when !addressableOwners.Contains(definition.Id):
                            issues.Add(new ValidationIssue(
                                ValidationIssueCode.MissingAddressableKey,
                                $"Destination '{definition.Id}' has no " +
                                "Addressables binding.",
                                definition.AssetPath));
                            break;
                        case ContentKind.Cosmetic
                            when !cosmeticOwners.Contains(definition.Id):
                            issues.Add(new ValidationIssue(
                                ValidationIssueCode.MissingCosmeticFit,
                                $"Cosmetic '{definition.Id}' has no body-family " +
                                "fit declaration.",
                                definition.AssetPath));
                            break;
                        case ContentKind.StoreProduct
                            when !storeProducts.Contains(definition.Id):
                            issues.Add(new ValidationIssue(
                                ValidationIssueCode.MissingStoreEntitlement,
                                $"Store product '{definition.Id}' has no " +
                                "entitlement mapping.",
                                definition.AssetPath));
                            break;
                    }
                }
            }
        }

        private static void ValidateReferences(
            IReadOnlyList<ContentReference> references,
            IReadOnlyDictionary<ContentId, List<ContentDefinition>> definitions,
            ICollection<ValidationIssue> issues)
        {
            foreach (var reference in references)
            {
                if (!HasDefinition(definitions, reference.OwnerId, null))
                {
                    AddMissingOwner(reference.OwnerId, reference.AssetPath, issues);
                }

                var expectedKind = reference.Kind ==
                    ContentReferenceKind.MissionLink
                        ? ContentKind.Mission
                        : ContentKind.Dialogue;
                if (HasDefinition(definitions, reference.TargetId, expectedKind))
                {
                    continue;
                }

                var code = reference.Kind == ContentReferenceKind.MissionLink
                    ? ValidationIssueCode.MissingMissionLink
                    : ValidationIssueCode.MissingDialogueReference;
                issues.Add(new ValidationIssue(
                    code,
                    $"Content '{reference.OwnerId}' references missing " +
                    $"{expectedKind} '{reference.TargetId}'.",
                    reference.AssetPath));
            }
        }

        private static void ValidateScienceSources(
            IReadOnlyList<ScienceSourceBinding> bindings,
            IReadOnlyDictionary<ContentId, List<ContentDefinition>> definitions,
            ICollection<ValidationIssue> issues)
        {
            foreach (var binding in bindings)
            {
                if (!HasDefinition(definitions, binding.OwnerId, null))
                {
                    AddMissingOwner(binding.OwnerId, binding.AssetPath, issues);
                }

                if (!HasDefinition(
                        definitions,
                        binding.SourceId,
                        ContentKind.ScienceSource))
                {
                    issues.Add(new ValidationIssue(
                        ValidationIssueCode.MissingScienceSource,
                        $"Content '{binding.OwnerId}' references missing science " +
                        $"source '{binding.SourceId}'.",
                        binding.AssetPath));
                }
            }
        }

        private static void ValidateAddressables(
            ProjectContentIndex index,
            IReadOnlyDictionary<ContentId, List<ContentDefinition>> definitions,
            ICollection<ValidationIssue> issues)
        {
            var knownKeys = new HashSet<string>(
                index.KnownAddressableKeys,
                StringComparer.Ordinal);
            foreach (var binding in index.Addressables)
            {
                if (!HasDefinition(definitions, binding.OwnerId, null))
                {
                    AddMissingOwner(binding.OwnerId, binding.AssetPath, issues);
                }

                if (!knownKeys.Contains(binding.Key))
                {
                    issues.Add(new ValidationIssue(
                        ValidationIssueCode.MissingAddressableKey,
                        $"Content '{binding.OwnerId}' references missing " +
                        $"Addressables key '{binding.Key}'.",
                        binding.AssetPath));
                }
            }
        }

        private static void ValidateCosmeticFits(
            IReadOnlyList<CosmeticFitBinding> bindings,
            IReadOnlyDictionary<ContentId, List<ContentDefinition>> definitions,
            ICollection<ValidationIssue> issues)
        {
            foreach (var binding in bindings)
            {
                if (!HasDefinition(
                        definitions,
                        binding.CosmeticId,
                        ContentKind.Cosmetic))
                {
                    AddMissingOwner(binding.CosmeticId, binding.AssetPath, issues);
                }

                var families = new HashSet<ContentBodyFamily>(binding.Families);
                foreach (var requiredFamily in s_RequiredBodyFamilies)
                {
                    if (!families.Contains(requiredFamily))
                    {
                        issues.Add(new ValidationIssue(
                            ValidationIssueCode.MissingCosmeticFit,
                            $"Cosmetic '{binding.CosmeticId}' has no " +
                            $"'{requiredFamily}' body-family fit.",
                            binding.AssetPath));
                    }
                }
            }
        }

        private static void ValidateStoreEntitlements(
            IReadOnlyList<StoreEntitlementBinding> bindings,
            IReadOnlyDictionary<ContentId, List<ContentDefinition>> definitions,
            ICollection<ValidationIssue> issues)
        {
            var mappedProducts = new HashSet<ContentId>();
            foreach (var binding in bindings)
            {
                if (!HasDefinition(
                        definitions,
                        binding.ProductId,
                        ContentKind.StoreProduct))
                {
                    AddMissingOwner(binding.ProductId, binding.AssetPath, issues);
                }

                if (!mappedProducts.Add(binding.ProductId))
                {
                    issues.Add(new ValidationIssue(
                        ValidationIssueCode.DuplicateStoreProductMapping,
                        $"Store product '{binding.ProductId}' has more than one " +
                        "entitlement mapping.",
                        binding.AssetPath));
                }

                if (!HasDefinition(
                        definitions,
                        binding.EntitlementId,
                        ContentKind.Entitlement))
                {
                    issues.Add(new ValidationIssue(
                        ValidationIssueCode.MissingStoreEntitlement,
                        $"Store product '{binding.ProductId}' references missing " +
                        $"entitlement '{binding.EntitlementId}'.",
                        binding.AssetPath));
                }
            }
        }

        private static bool HasDefinition(
            IReadOnlyDictionary<ContentId, List<ContentDefinition>> definitions,
            ContentId id,
            ContentKind? expectedKind)
        {
            return definitions.TryGetValue(id, out var matches) &&
                (expectedKind == null ||
                 matches.Any(definition => definition.Kind == expectedKind.Value));
        }

        private static void AddMissingOwner(
            ContentId ownerId,
            string assetPath,
            ICollection<ValidationIssue> issues)
        {
            issues.Add(new ValidationIssue(
                ValidationIssueCode.MissingContentOwner,
                $"Validation contribution owner '{ownerId}' is not defined.",
                assetPath));
        }
    }

    internal sealed class SceneCatalogValidationContributor :
        IProjectContentValidationContributor
    {
        public void Contribute(ProjectContentIndexBuilder builder)
        {
            AddKnownAddressableKeys(builder);
            var catalogGuids = AssetDatabase.FindAssets(
                "t:SceneCatalog",
                new[] { "Assets" });
            Array.Sort(catalogGuids, StringComparer.Ordinal);
            foreach (var guid in catalogGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var catalog = AssetDatabase.LoadAssetAtPath<SceneCatalog>(path);
                if (catalog == null)
                {
                    builder.AddIssue(new ValidationIssue(
                        ValidationIssueCode.InvalidContentAsset,
                        "A SceneCatalog search result could not be loaded.",
                        path));
                    continue;
                }

                try
                {
                    catalog.Validate();
                }
                catch (Exception exception)
                {
                    builder.AddIssue(new ValidationIssue(
                        ValidationIssueCode.InvalidContentAsset,
                        $"SceneCatalog is invalid: {exception.Message}",
                        path));
                    continue;
                }

                foreach (var entry in catalog.Entries)
                {
                    var id = new ContentId(entry.DestinationId);
                    builder
                        .AddDefinition(id, ContentKind.Destination, path)
                        .AddAddressable(id, entry.Address, path);
                }
            }
        }

        private static void AddKnownAddressableKeys(
            ProjectContentIndexBuilder builder)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                builder.AddIssue(new ValidationIssue(
                    ValidationIssueCode.InvalidContentAsset,
                    "The project has no AddressableAssetSettings.",
                    "Assets/AddressableAssetsData"));
                return;
            }

            foreach (var group in settings.groups.Where(group => group != null))
            {
                foreach (var entry in group.entries.Where(entry => entry != null))
                {
                    if (!string.IsNullOrWhiteSpace(entry.address) &&
                        string.Equals(
                            entry.address,
                            entry.address.Trim(),
                            StringComparison.Ordinal))
                    {
                        builder.AddKnownAddressableKey(entry.address);
                    }
                }
            }
        }
    }
}
