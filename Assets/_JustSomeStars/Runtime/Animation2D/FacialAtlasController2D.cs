using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace JustSomeStars.Runtime.Animation2D
{
    [DisallowMultipleComponent]
    public sealed class FacialAtlasController2D : MonoBehaviour
    {
        public const int VisemeCount = 6;

        private static readonly string[] Expressions =
        {
            "neutral",
            "happy",
            "curious",
            "worried",
            "afraid",
            "surprised",
            "determined",
            "sad",
            "blink",
            "speaking",
        };

        private static readonly IReadOnlyDictionary<string, string> ExpressionAliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["neutral"] = "neutral",
                ["calm"] = "neutral",
                ["happy"] = "happy",
                ["joy"] = "happy",
                ["bright"] = "happy",
                ["encouraging"] = "happy",
                ["small-smile"] = "happy",
                ["warm"] = "happy",
                ["curious"] = "curious",
                ["wonder"] = "curious",
                ["bright-focus"] = "curious",
                ["observe"] = "curious",
                ["worried"] = "worried",
                ["concerned"] = "worried",
                ["anxious"] = "worried",
                ["afraid"] = "afraid",
                ["fear"] = "afraid",
                ["scared"] = "afraid",
                ["surprised"] = "surprised",
                ["surprise"] = "surprised",
                ["flicker"] = "surprised",
                ["signal-pulse"] = "surprised",
                ["determined"] = "determined",
                ["focused"] = "determined",
                ["alert"] = "determined",
                ["sad"] = "sad",
                ["blink"] = "blink",
                ["speaking"] = "speaking",
            };

        [SerializeField] private CharacterSpriteSet[] faceAtlases =
            Array.Empty<CharacterSpriteSet>();
        [SerializeField] private Image uiTarget;
        [SerializeField] private Image speechUiTarget;
        [SerializeField] private SpriteRenderer worldTarget;
        [SerializeField] private SpriteRenderer speechWorldTarget;

        private string m_BaseExpression = "neutral";

        public static IReadOnlyList<string> RequiredExpressions => Expressions;
        public string CurrentActorId { get; private set; } = string.Empty;
        public string CurrentExpression { get; private set; } = string.Empty;

        public void Configure(CharacterSpriteSet[] atlases, Image target)
        {
            Configure(atlases, target, null);
        }

        public void Configure(
            CharacterSpriteSet[] atlases,
            Image target,
            Image speechTarget)
        {
            faceAtlases = CopyAndValidate(atlases);
            uiTarget = target != null
                ? target
                : throw new ArgumentNullException(nameof(target));
            speechUiTarget = speechTarget;
            worldTarget = null;
            speechWorldTarget = null;
            Hide();
        }

        public void Configure(CharacterSpriteSet[] atlases, SpriteRenderer target)
        {
            Configure(atlases, target, null);
        }

        public void Configure(
            CharacterSpriteSet[] atlases,
            SpriteRenderer target,
            SpriteRenderer speechTarget)
        {
            faceAtlases = CopyAndValidate(atlases);
            worldTarget = target != null
                ? target
                : throw new ArgumentNullException(nameof(target));
            speechWorldTarget = speechTarget;
            uiTarget = null;
            speechUiTarget = null;
            Hide();
        }

        public bool ShowExpression(string actorId, string authoredExpression)
        {
            var canonicalActor = CanonicalActor(actorId);
            if (!ExpressionAliases.TryGetValue(
                    authoredExpression ?? string.Empty,
                    out var expression) ||
                !TryResolve(canonicalActor, $"{canonicalActor}.expression.{expression}",
                    out var sprite))
            {
                return false;
            }

            CurrentActorId = canonicalActor;
            CurrentExpression = expression;
            m_BaseExpression = expression == "blink" || expression == "speaking"
                ? "neutral"
                : expression;
            PresentBase(sprite);
            SetSpeechTarget(null, false);
            return true;
        }

        public bool ShowViseme(string actorId, int visemeIndex)
        {
            var canonicalActor = CanonicalActor(actorId);
            if ((speechUiTarget == null && speechWorldTarget == null) ||
                visemeIndex < 0 || visemeIndex >= VisemeCount ||
                !TryResolve(canonicalActor, $"{canonicalActor}.speech.{visemeIndex}",
                    out var sprite))
            {
                return false;
            }

            if (!string.Equals(CurrentActorId, canonicalActor, StringComparison.Ordinal) &&
                !ShowExpression(canonicalActor, "neutral"))
            {
                return false;
            }
            CurrentActorId = canonicalActor;
            CurrentExpression = "speaking";
            SetSpeechTarget(sprite, true);
            return true;
        }

        public bool AdvanceSpeech(string actorId, float elapsedSeconds)
        {
            if (elapsedSeconds < 0f || float.IsNaN(elapsedSeconds) ||
                float.IsInfinity(elapsedSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            }
            var frame = Mathf.FloorToInt(elapsedSeconds * 12f) % VisemeCount;
            return ShowViseme(actorId, frame);
        }

        public void ResetNeutral()
        {
            if (string.IsNullOrEmpty(CurrentActorId))
            {
                Hide();
                return;
            }
            if (!ShowExpression(CurrentActorId, "neutral")) Hide();
        }

        public void EndSpeech()
        {
            if (string.IsNullOrEmpty(CurrentActorId))
            {
                Hide();
                return;
            }
            SetSpeechTarget(null, false);
            if (!ShowExpression(CurrentActorId, m_BaseExpression) &&
                !ShowExpression(CurrentActorId, "neutral"))
            {
                Hide();
            }
        }

        public void Hide()
        {
            CurrentActorId = string.Empty;
            CurrentExpression = string.Empty;
            SetBaseTarget(null, false);
            SetSpeechTarget(null, false);
        }

        private bool TryResolve(string actorId, string clipId, out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrWhiteSpace(actorId)) return false;
            var atlas = faceAtlases?.SingleOrDefault(candidate => candidate != null &&
                string.Equals(candidate.CharacterId, actorId, StringComparison.Ordinal));
            if (atlas == null) return false;
            try
            {
                var clip = atlas.FindClip(clipId);
                sprite = clip.Frames.Count > 0 ? clip.Frames[0] : null;
                return sprite != null;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }

        private void PresentBase(Sprite sprite) => SetBaseTarget(sprite, true);

        private void SetBaseTarget(Sprite sprite, bool visible)
        {
            if (uiTarget != null)
            {
                uiTarget.sprite = sprite;
                uiTarget.enabled = visible;
                uiTarget.preserveAspect = true;
            }
            if (worldTarget != null)
            {
                worldTarget.sprite = sprite;
                worldTarget.enabled = visible;
            }
        }

        private void SetSpeechTarget(Sprite sprite, bool visible)
        {
            if (speechUiTarget != null)
            {
                speechUiTarget.sprite = sprite;
                speechUiTarget.enabled = visible;
                speechUiTarget.preserveAspect = true;
            }
            if (speechWorldTarget != null)
            {
                speechWorldTarget.sprite = sprite;
                speechWorldTarget.enabled = visible;
            }
        }

        private static CharacterSpriteSet[] CopyAndValidate(
            CharacterSpriteSet[] atlases)
        {
            if (atlases == null || atlases.Length == 0 ||
                atlases.Any(atlas => atlas == null) ||
                atlases.Select(atlas => atlas.CharacterId)
                    .Distinct(StringComparer.Ordinal).Count() != atlases.Length)
            {
                throw new InvalidOperationException(
                    "Facial atlas controller requires unique authored sprite sets.");
            }
            return (CharacterSpriteSet[])atlases.Clone();
        }

        private static string CanonicalActor(string actorId)
        {
            if (string.IsNullOrWhiteSpace(actorId)) return string.Empty;
            var canonical = actorId.Trim().ToLowerInvariant();
            if (canonical.StartsWith("crew.", StringComparison.Ordinal))
                canonical = canonical.Substring("crew.".Length);
            if (canonical.StartsWith("robot.", StringComparison.Ordinal))
                canonical = canonical.Substring("robot.".Length);
            return canonical;
        }
    }
}
