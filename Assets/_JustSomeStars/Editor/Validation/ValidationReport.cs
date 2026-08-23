using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JustSomeStars.Runtime.Core;

namespace JustSomeStars.Editor.Validation
{
    public enum ContentKind
    {
        Destination = 0,
        Mission = 1,
        Dialogue = 2,
        Phenomenon = 3,
        ScienceSource = 4,
        Cosmetic = 5,
        StoreProduct = 6,
        Entitlement = 7,
    }

    public enum ContentReferenceKind
    {
        MissionLink = 0,
        DialogueReference = 1,
    }

    public enum ContentBodyFamily
    {
        Compact = 0,
        Average = 1,
        TallBroad = 2,
    }

    public enum ValidationIssueCode
    {
        InvalidContentAsset = 0,
        ContributorFailure = 1,
        DuplicateContentId = 2,
        MissingContentOwner = 3,
        MissingMissionLink = 4,
        MissingDialogueReference = 5,
        MissingScienceSource = 6,
        MissingAddressableKey = 7,
        MissingCosmeticFit = 8,
        MissingStoreEntitlement = 9,
        DuplicateStoreProductMapping = 10,
    }

    public sealed class ValidationIssue
    {
        public ValidationIssue(
            ValidationIssueCode code,
            string message,
            string assetPath)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException(
                    "A validation issue requires a message.",
                    nameof(message));
            }

            Code = code;
            Message = message.Trim();
            AssetPath = assetPath?.Trim() ?? string.Empty;
        }

        public ValidationIssueCode Code { get; }

        public string Message { get; }

        public string AssetPath { get; }
    }

    public sealed class ValidationReport
    {
        private readonly IReadOnlyList<ValidationIssue> m_Issues;

        internal ValidationReport(IEnumerable<ValidationIssue> issues)
        {
            var snapshot = issues == null
                ? Array.Empty<ValidationIssue>()
                : new List<ValidationIssue>(issues).ToArray();
            m_Issues = new ReadOnlyCollection<ValidationIssue>(snapshot);
        }

        public IReadOnlyList<ValidationIssue> Issues => m_Issues;

        public bool IsValid => m_Issues.Count == 0;
    }

    public interface IProjectContentValidationContributor
    {
        void Contribute(ProjectContentIndexBuilder builder);
    }

    public sealed class ProjectContentIndexBuilder
    {
        private readonly List<ContentDefinition> m_Definitions =
            new List<ContentDefinition>();
        private readonly List<ContentReference> m_References =
            new List<ContentReference>();
        private readonly List<ScienceSourceBinding> m_ScienceSources =
            new List<ScienceSourceBinding>();
        private readonly List<AddressableBinding> m_Addressables =
            new List<AddressableBinding>();
        private readonly List<string> m_KnownAddressableKeys =
            new List<string>();
        private readonly List<CosmeticFitBinding> m_CosmeticFits =
            new List<CosmeticFitBinding>();
        private readonly List<StoreEntitlementBinding> m_StoreEntitlements =
            new List<StoreEntitlementBinding>();
        private readonly List<ValidationIssue> m_SeedIssues =
            new List<ValidationIssue>();

        public ProjectContentIndexBuilder AddDefinition(
            ContentId id,
            ContentKind kind,
            string assetPath)
        {
            RequireContentId(id, nameof(id));
            RequireDefinedEnum(kind, nameof(kind));
            m_Definitions.Add(new ContentDefinition(
                id,
                kind,
                RequirePath(assetPath, nameof(assetPath))));
            return this;
        }

        public ProjectContentIndexBuilder AddReference(
            ContentId ownerId,
            ContentId targetId,
            ContentReferenceKind kind,
            string assetPath)
        {
            RequireContentId(ownerId, nameof(ownerId));
            RequireContentId(targetId, nameof(targetId));
            RequireDefinedEnum(kind, nameof(kind));
            m_References.Add(new ContentReference(
                ownerId,
                targetId,
                kind,
                RequirePath(assetPath, nameof(assetPath))));
            return this;
        }

        public ProjectContentIndexBuilder AddScienceSource(
            ContentId ownerId,
            ContentId sourceId,
            string assetPath)
        {
            RequireContentId(ownerId, nameof(ownerId));
            RequireContentId(sourceId, nameof(sourceId));
            m_ScienceSources.Add(new ScienceSourceBinding(
                ownerId,
                sourceId,
                RequirePath(assetPath, nameof(assetPath))));
            return this;
        }

        public ProjectContentIndexBuilder AddAddressable(
            ContentId ownerId,
            string key,
            string assetPath)
        {
            RequireContentId(ownerId, nameof(ownerId));
            m_Addressables.Add(new AddressableBinding(
                ownerId,
                RequireCanonicalValue(key, nameof(key)),
                RequirePath(assetPath, nameof(assetPath))));
            return this;
        }

        public ProjectContentIndexBuilder AddKnownAddressableKey(string key)
        {
            m_KnownAddressableKeys.Add(
                RequireCanonicalValue(key, nameof(key)));
            return this;
        }

        public ProjectContentIndexBuilder AddCosmeticFits(
            ContentId cosmeticId,
            string assetPath,
            params ContentBodyFamily[] families)
        {
            RequireContentId(cosmeticId, nameof(cosmeticId));
            if (families == null)
            {
                throw new ArgumentNullException(nameof(families));
            }

            var copy = (ContentBodyFamily[])families.Clone();
            foreach (var family in copy)
            {
                RequireDefinedEnum(family, nameof(families));
            }

            m_CosmeticFits.Add(new CosmeticFitBinding(
                cosmeticId,
                RequirePath(assetPath, nameof(assetPath)),
                copy));
            return this;
        }

        public ProjectContentIndexBuilder AddStoreEntitlement(
            ContentId productId,
            ContentId entitlementId,
            string assetPath)
        {
            RequireContentId(productId, nameof(productId));
            RequireContentId(entitlementId, nameof(entitlementId));
            m_StoreEntitlements.Add(new StoreEntitlementBinding(
                productId,
                entitlementId,
                RequirePath(assetPath, nameof(assetPath))));
            return this;
        }

        public ProjectContentIndexBuilder AddIssue(ValidationIssue issue)
        {
            m_SeedIssues.Add(issue ?? throw new ArgumentNullException(nameof(issue)));
            return this;
        }

        public ProjectContentIndex Build()
        {
            return new ProjectContentIndex(
                m_Definitions.ToArray(),
                m_References.ToArray(),
                m_ScienceSources.ToArray(),
                m_Addressables.ToArray(),
                m_KnownAddressableKeys.ToArray(),
                m_CosmeticFits.ToArray(),
                m_StoreEntitlements.ToArray(),
                m_SeedIssues.ToArray());
        }

        private static void RequireContentId(ContentId id, string parameterName)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException(
                    "A validation contribution requires a valid content ID.",
                    parameterName);
            }
        }

        private static void RequireDefinedEnum<TEnum>(
            TEnum value,
            string parameterName)
            where TEnum : struct
        {
            if (!Enum.IsDefined(typeof(TEnum), value))
            {
                throw new ArgumentOutOfRangeException(parameterName, value, null);
            }
        }

        private static string RequirePath(string path, string parameterName)
        {
            return RequireCanonicalValue(path, parameterName);
        }

        private static string RequireCanonicalValue(
            string value,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "The value must be non-empty and already trimmed.",
                    parameterName);
            }

            return value;
        }
    }

    public sealed class ProjectContentIndex
    {
        internal ProjectContentIndex(
            ContentDefinition[] definitions,
            ContentReference[] references,
            ScienceSourceBinding[] scienceSources,
            AddressableBinding[] addressables,
            string[] knownAddressableKeys,
            CosmeticFitBinding[] cosmeticFits,
            StoreEntitlementBinding[] storeEntitlements,
            ValidationIssue[] seedIssues)
        {
            Definitions = definitions;
            References = references;
            ScienceSources = scienceSources;
            Addressables = addressables;
            KnownAddressableKeys = knownAddressableKeys;
            CosmeticFits = cosmeticFits;
            StoreEntitlements = storeEntitlements;
            SeedIssues = seedIssues;
        }

        internal IReadOnlyList<ContentDefinition> Definitions { get; }

        internal IReadOnlyList<ContentReference> References { get; }

        internal IReadOnlyList<ScienceSourceBinding> ScienceSources { get; }

        internal IReadOnlyList<AddressableBinding> Addressables { get; }

        internal IReadOnlyList<string> KnownAddressableKeys { get; }

        internal IReadOnlyList<CosmeticFitBinding> CosmeticFits { get; }

        internal IReadOnlyList<StoreEntitlementBinding> StoreEntitlements { get; }

        internal IReadOnlyList<ValidationIssue> SeedIssues { get; }
    }

    internal sealed class ContentDefinition
    {
        public ContentDefinition(ContentId id, ContentKind kind, string assetPath)
        {
            Id = id;
            Kind = kind;
            AssetPath = assetPath;
        }

        public ContentId Id { get; }

        public ContentKind Kind { get; }

        public string AssetPath { get; }
    }

    internal sealed class ContentReference
    {
        public ContentReference(
            ContentId ownerId,
            ContentId targetId,
            ContentReferenceKind kind,
            string assetPath)
        {
            OwnerId = ownerId;
            TargetId = targetId;
            Kind = kind;
            AssetPath = assetPath;
        }

        public ContentId OwnerId { get; }

        public ContentId TargetId { get; }

        public ContentReferenceKind Kind { get; }

        public string AssetPath { get; }
    }

    internal sealed class ScienceSourceBinding
    {
        public ScienceSourceBinding(
            ContentId ownerId,
            ContentId sourceId,
            string assetPath)
        {
            OwnerId = ownerId;
            SourceId = sourceId;
            AssetPath = assetPath;
        }

        public ContentId OwnerId { get; }

        public ContentId SourceId { get; }

        public string AssetPath { get; }
    }

    internal sealed class AddressableBinding
    {
        public AddressableBinding(
            ContentId ownerId,
            string key,
            string assetPath)
        {
            OwnerId = ownerId;
            Key = key;
            AssetPath = assetPath;
        }

        public ContentId OwnerId { get; }

        public string Key { get; }

        public string AssetPath { get; }
    }

    internal sealed class CosmeticFitBinding
    {
        public CosmeticFitBinding(
            ContentId cosmeticId,
            string assetPath,
            ContentBodyFamily[] families)
        {
            CosmeticId = cosmeticId;
            AssetPath = assetPath;
            Families = families;
        }

        public ContentId CosmeticId { get; }

        public string AssetPath { get; }

        public IReadOnlyList<ContentBodyFamily> Families { get; }
    }

    internal sealed class StoreEntitlementBinding
    {
        public StoreEntitlementBinding(
            ContentId productId,
            ContentId entitlementId,
            string assetPath)
        {
            ProductId = productId;
            EntitlementId = entitlementId;
            AssetPath = assetPath;
        }

        public ContentId ProductId { get; }

        public ContentId EntitlementId { get; }

        public string AssetPath { get; }
    }
}
