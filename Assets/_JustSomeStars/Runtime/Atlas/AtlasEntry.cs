using System;
using JustSomeStars.Runtime.Core;
using UnityEngine;

namespace JustSomeStars.Runtime.Atlas
{
    [CreateAssetMenu(
        fileName = "AtlasEntry",
        menuName = "Just Some Stars/Atlas/Atlas Entry")]
    public sealed class AtlasEntry : ScriptableObject
    {
        [SerializeField] private string stableId;
        [SerializeField] private string phenomenonId;
        [SerializeField] private string scienceSourceId;
        [SerializeField] private string shortTextKey;
        [SerializeField] private string balancedTextKey;
        [SerializeField] private string deepTextKey;

        public ContentId StableId => new ContentId(stableId);
        public ContentId PhenomenonId => new ContentId(phenomenonId);
        public ContentId ScienceSourceId => new ContentId(scienceSourceId);
        public string ShortTextKey => shortTextKey;
        public string BalancedTextKey => balancedTextKey;
        public string DeepTextKey => deepTextKey;

        public void Configure(
            string id,
            string phenomenon,
            string source,
            string shortKey,
            string balancedKey,
            string deepKey)
        {
            stableId = id;
            phenomenonId = phenomenon;
            scienceSourceId = source;
            shortTextKey = shortKey;
            balancedTextKey = balancedKey;
            deepTextKey = deepKey;
            ValidateOrThrow();
        }

        public void ValidateOrThrow()
        {
            _ = StableId;
            _ = PhenomenonId;
            _ = ScienceSourceId;
            RequireKey(shortTextKey, nameof(shortTextKey));
            RequireKey(balancedTextKey, nameof(balancedTextKey));
            RequireKey(deepTextKey, nameof(deepTextKey));
            if (string.Equals(shortTextKey, balancedTextKey, StringComparison.Ordinal) ||
                string.Equals(shortTextKey, deepTextKey, StringComparison.Ordinal) ||
                string.Equals(balancedTextKey, deepTextKey, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Atlas entry '{stableId}' requires three distinct depth keys.");
            }
        }

        private static void RequireKey(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Atlas localization key '{parameterName}' must be canonical.");
            }
        }
    }

}
