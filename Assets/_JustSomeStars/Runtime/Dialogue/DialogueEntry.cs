using System;
using System.Collections.Generic;
using System.Linq;
using JustSomeStars.Runtime.Core;
using UnityEngine;

namespace JustSomeStars.Runtime.Dialogue
{
    public enum DialoguePriority
    {
        Ambient = 100,
        Hint = 200,
        Personality = 300,
        Safety = 400,
        Story = 500,
    }

    [CreateAssetMenu(
        fileName = "DialogueEntry",
        menuName = "Just Some Stars/Dialogue/Dialogue Entry")]
    public sealed class DialogueEntry : ScriptableObject
    {
        [SerializeField] private string stableId;
        [SerializeField] private string localizationKey;
        [SerializeField] private string speakerId;
        [SerializeField] private string voiceReference;
        [SerializeField] private string emotion;
        [SerializeField] private string expression;
        [SerializeField] private string gesture;
        [SerializeField] private string[] conditions = Array.Empty<string>();
        [SerializeField] private DialoguePriority priority;
        [SerializeField] private bool interruptible = true;
        [SerializeField, Min(0f)] private double cooldownSeconds;
        [SerializeField] private string[] followupIds = Array.Empty<string>();

        public ContentId StableId => new ContentId(stableId);
        public string LocalizationKey => localizationKey;
        public ContentId SpeakerId => new ContentId(speakerId);
        public string VoiceReference => voiceReference;
        public string Emotion => emotion;
        public string Expression => expression;
        public string Gesture => gesture;
        public IReadOnlyList<string> Conditions => conditions;
        public DialoguePriority Priority => priority;
        public bool Interruptible => interruptible;
        public double CooldownSeconds => cooldownSeconds;
        public IReadOnlyList<string> FollowupIds => followupIds;

        public void Configure(
            string id,
            string textKey,
            string speaker,
            string voice,
            string authoredEmotion,
            string authoredExpression,
            string authoredGesture,
            string[] authoredConditions,
            DialoguePriority authoredPriority,
            bool canInterrupt,
            double cooldown,
            string[] authoredFollowups)
        {
            stableId = id;
            localizationKey = textKey;
            speakerId = speaker;
            voiceReference = voice;
            emotion = authoredEmotion;
            expression = authoredExpression;
            gesture = authoredGesture;
            conditions = authoredConditions != null
                ? (string[])authoredConditions.Clone()
                : null;
            priority = authoredPriority;
            interruptible = canInterrupt;
            cooldownSeconds = cooldown;
            followupIds = authoredFollowups != null
                ? (string[])authoredFollowups.Clone()
                : null;
            ValidateOrThrow();
        }

        public void ConfigureFollowups(string[] authoredFollowups)
        {
            followupIds = authoredFollowups != null
                ? (string[])authoredFollowups.Clone()
                : null;
            ValidateOrThrow();
        }

        public void ValidateOrThrow()
        {
            _ = StableId;
            _ = SpeakerId;
            RequireCanonical(localizationKey, nameof(localizationKey));
            RequireCanonical(voiceReference, nameof(voiceReference));
            RequireCanonical(emotion, nameof(emotion));
            RequireCanonical(expression, nameof(expression));
            RequireCanonical(gesture, nameof(gesture));
            if (!Enum.IsDefined(typeof(DialoguePriority), priority))
            {
                throw new InvalidOperationException(
                    $"Dialogue '{stableId}' has invalid priority.");
            }

            if (cooldownSeconds < 0 || double.IsNaN(cooldownSeconds) ||
                double.IsInfinity(cooldownSeconds))
            {
                throw new InvalidOperationException(
                    $"Dialogue '{stableId}' has invalid cooldown.");
            }

            RequireCanonicalArray(conditions, "condition");
            RequireCanonicalArray(followupIds, "follow-up");
        }

        private static void RequireCanonical(string value, string role)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Dialogue {role} must be a canonical authored value.");
            }
        }

        private static void RequireCanonicalArray(string[] values, string role)
        {
            if (values == null || values.Any(value =>
                    string.IsNullOrWhiteSpace(value) ||
                    !string.Equals(value, value.Trim(), StringComparison.Ordinal)) ||
                values.Distinct(StringComparer.Ordinal).Count() != values.Length)
            {
                throw new InvalidOperationException(
                    $"Dialogue {role} values must be unique and canonical.");
            }
        }
    }
}
