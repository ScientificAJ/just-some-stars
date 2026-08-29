using System;
using System.Collections.Generic;
using System.Linq;
using JustSomeStars.Runtime.Core;
using UnityEngine;

namespace JustSomeStars.Runtime.Crew
{
    public enum CrewRole
    {
        Mira = 0,
        Juno = 1,
        Kai = 2,
        Bea = 3,
        Ori = 4,
    }

    public enum CrewAttention
    {
        None = 0,
        AtmosphereAndEvidence = 1,
        MachineryAndTools = 2,
        TraversalAndDanger = 3,
        MemoryAndWellbeing = 4,
        HazardsAndSignal = 5,
    }

    [Serializable]
    public struct CrewAttentionWeight
    {
        [SerializeField] private CrewAttention attention;
        [SerializeField, Range(0f, 1f)] private float weight;

        public CrewAttentionWeight(CrewAttention attention, float weight)
        {
            this.attention = attention;
            this.weight = weight;
        }

        public CrewAttention Attention => attention;
        public float Weight => weight;
    }

    [CreateAssetMenu(
        fileName = "CrewPersonality",
        menuName = "Just Some Stars/Crew/Personality")]
    public sealed class CrewPersonality : ScriptableObject
    {
        [SerializeField] private string stableId;
        [SerializeField] private string displayName;
        [SerializeField] private CrewRole role;
        [SerializeField] private CrewAttention primaryAttention;
        [SerializeField] private CrewAttentionWeight[] attentionWeights =
            Array.Empty<CrewAttentionWeight>();
        [NonSerialized] private bool m_IsValidated;

        public ContentId StableId => new ContentId(stableId);
        public string DisplayName => displayName;
        public CrewRole Role => role;
        public CrewAttention PrimaryAttention => primaryAttention;
        public bool IsOri => role == CrewRole.Ori;

        public void Configure(
            string id,
            string authoredDisplayName,
            CrewRole authoredRole,
            CrewAttention authoredPrimaryAttention,
            CrewAttentionWeight[] authoredWeights)
        {
            stableId = id;
            displayName = authoredDisplayName;
            role = authoredRole;
            primaryAttention = authoredPrimaryAttention;
            attentionWeights = authoredWeights?.ToArray() ??
                Array.Empty<CrewAttentionWeight>();
            m_IsValidated = false;
            ValidateOrThrow();
        }

        public float GetAttentionWeight(CrewAttention attention)
        {
            if (attention == CrewAttention.None)
            {
                return 0f;
            }

            foreach (var entry in attentionWeights)
            {
                if (entry.Attention == attention)
                {
                    return entry.Weight;
                }
            }

            return 0f;
        }

        public void ValidateOrThrow()
        {
            if (m_IsValidated)
            {
                return;
            }

            _ = StableId;
            if (string.IsNullOrWhiteSpace(displayName) ||
                !string.Equals(displayName, displayName.Trim(),
                    StringComparison.Ordinal) ||
                !Enum.IsDefined(typeof(CrewRole), role) ||
                primaryAttention == CrewAttention.None ||
                !Enum.IsDefined(typeof(CrewAttention), primaryAttention))
            {
                throw new InvalidOperationException(
                    $"Crew personality '{stableId}' has invalid identity data.");
            }

            var seen = new HashSet<CrewAttention>();
            foreach (var entry in attentionWeights ??
                Array.Empty<CrewAttentionWeight>())
            {
                if (entry.Attention == CrewAttention.None ||
                    !Enum.IsDefined(typeof(CrewAttention), entry.Attention) ||
                    !seen.Add(entry.Attention) ||
                    entry.Weight < 0f || entry.Weight > 1f ||
                    float.IsNaN(entry.Weight) || float.IsInfinity(entry.Weight))
                {
                    throw new InvalidOperationException(
                        $"Crew personality '{stableId}' has invalid attention weights.");
                }
            }

            if (Mathf.Abs(GetAttentionWeight(primaryAttention) - 1f) > 0.0001f)
            {
                throw new InvalidOperationException(
                    $"Crew personality '{stableId}' must weight its primary " +
                    "attention exactly 1.");
            }

            m_IsValidated = true;
        }

        private void OnValidate()
        {
            m_IsValidated = false;
        }
    }
}
