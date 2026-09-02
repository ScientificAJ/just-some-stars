using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Missions;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Animation2D;
using JustSomeStars.Runtime.Atlas;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.UI;
using TMPro;
using UnityEngine;

namespace JustSomeStars.Runtime.Dialogue
{
    [DisallowMultipleComponent]
    public sealed class MirraDialoguePresenter2D : MonoBehaviour,
        IMirraDialoguePresenter
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text speakerLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private AccessibleCaption accessibleCaption;
        [SerializeField] private AccessibilityApplier accessibility;
        [SerializeField] private FacialAtlasController2D facialAtlas;
        [SerializeField] private LocalizedEnglishCatalog english;
        [SerializeField] private CinematicActor2D[] actors =
            Array.Empty<CinematicActor2D>();
        [SerializeField, Min(0.05f)] private float minimumPresentationSeconds = 0.2f;

        private DialogueEntry m_CurrentEntry;
        private CinematicActor2D m_CurrentActor;
        private bool m_InteractionReleased;

        public int PresentationCount { get; private set; }
        public string CurrentDialogueId { get; private set; } = string.Empty;

        public void ConfigureMedia(
            LocalizedEnglishCatalog localization,
            FacialAtlasController2D facialController,
            CinematicActor2D[] authoredActors)
        {
            english = localization != null
                ? localization
                : throw new ArgumentNullException(nameof(localization));
            facialAtlas = facialController != null
                ? facialController
                : throw new ArgumentNullException(nameof(facialController));
            actors = authoredActors != null
                ? (CinematicActor2D[])authoredActors.Clone()
                : throw new ArgumentNullException(nameof(authoredActors));
            ValidateMediaOrThrow();
        }

        public void ValidateMediaOrThrow()
        {
            if (english == null || facialAtlas == null || actors == null ||
                actors.Length == 0 || actors.Any(actor => actor == null) ||
                actors.Select(actor => actor.ActorId)
                    .Distinct(StringComparer.Ordinal).Count() != actors.Length)
            {
                throw new InvalidOperationException(
                    "Mission dialogue requires localization, layered faces and " +
                    "unique frame-event actors.");
            }
            english.ValidateOrThrow();
            foreach (var actor in actors) actor.ValidateOrThrow();
        }

        public static float CalculateReadableDuration(
            string localizedText,
            float dialogueSpeed,
            float minimumSeconds)
        {
            if (string.IsNullOrWhiteSpace(localizedText))
                throw new ArgumentException("Readable dialogue requires text.",
                    nameof(localizedText));
            if (float.IsNaN(dialogueSpeed) || float.IsInfinity(dialogueSpeed) ||
                dialogueSpeed < 0.5f || dialogueSpeed > 2f ||
                float.IsNaN(minimumSeconds) || float.IsInfinity(minimumSeconds) ||
                minimumSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(dialogueSpeed));
            }
            const float baseCharactersPerSecond = 15f;
            return Mathf.Max(
                minimumSeconds,
                0.45f + localizedText.Trim().Length /
                (baseCharactersPerSecond * dialogueSpeed));
        }

        public async Task PresentAsync(
            DialogueEntry entry,
            string localizedText,
            CancellationToken cancellationToken)
        {
            if (entry == null || string.IsNullOrWhiteSpace(localizedText))
            {
                throw new ArgumentException(
                    "Mirra dialogue presentation requires authored content.");
            }

            if (panel == null || speakerLabel == null || bodyLabel == null)
            {
                throw new InvalidOperationException(
                    "Mirra dialogue presenter requires its panel and labels.");
            }

            ValidateMediaOrThrow();

            CurrentDialogueId = entry.StableId.Value;
            var actorId = CanonicalActor(entry.SpeakerId.Value);
            var speaker = english.Resolve("actor." + actorId);
            m_CurrentActor = actors.SingleOrDefault(actor => string.Equals(
                actor.ActorId,
                actorId,
                StringComparison.Ordinal)) ?? throw new InvalidOperationException(
                $"Dialogue actor '{actorId}' has no media binding.");
            var semanticId = "dialogue." + entry.StableId.Value;
            if (!m_CurrentActor.HasPerformance(semanticId))
            {
                throw new InvalidOperationException(
                    $"Dialogue '{entry.StableId.Value}' has no authored gesture " +
                    $"performance for '{entry.Gesture}'.");
            }
            m_CurrentEntry = entry;
            m_InteractionReleased = false;
            m_CurrentActor.FrameEventEmitted += OnActorFrameEvent;
            speakerLabel.text = speaker;
            bodyLabel.text = localizedText;
            panel.SetActive(true);
            if (facialAtlas != null &&
                !facialAtlas.ShowExpression(entry.SpeakerId.Value, entry.Expression))
            {
                facialAtlas.ShowExpression(entry.SpeakerId.Value, "neutral");
            }
            if (accessibleCaption != null)
            {
                accessibleCaption.Present(speaker, localizedText);
                accessibleCaption.Apply(accessibility == null ||
                    accessibility.CaptionsEnabled);
            }
            PresentationCount++;
            var readableSeconds = CalculateReadableDuration(
                localizedText,
                accessibility != null ? accessibility.DialogueSpeed : 1f,
                minimumPresentationSeconds);
            var elapsed = 0f;
            try
            {
                if (!m_CurrentActor.PlayBody(semanticId))
                {
                    throw new InvalidOperationException(
                        $"Dialogue performance '{semanticId}' could not start.");
                }
                while (elapsed < readableSeconds || !m_InteractionReleased)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                    elapsed += Time.unscaledDeltaTime;
                }
            }
            finally
            {
                if (m_CurrentActor != null)
                {
                    m_CurrentActor.FrameEventEmitted -= OnActorFrameEvent;
                    m_CurrentActor.EndPerformance();
                }
                if (panel != null)
                {
                    panel.SetActive(false);
                }

                CurrentDialogueId = string.Empty;
                facialAtlas?.EndSpeech();
                facialAtlas?.ResetNeutral();
                m_CurrentEntry = null;
                m_CurrentActor = null;
                m_InteractionReleased = false;
            }
        }

        private void OnActorFrameEvent(
            CinematicActor2D actor,
            SpriteFrameEvent frameEvent)
        {
            if (actor != m_CurrentActor || m_CurrentEntry == null) return;
            switch (frameEvent.Kind)
            {
                case SpriteFrameEventKind.Expression:
                    facialAtlas.ShowExpression(actor.ActorId, frameEvent.Id);
                    break;
                case SpriteFrameEventKind.Viseme:
                    if (int.TryParse(frameEvent.Id, out var viseme))
                        facialAtlas.ShowViseme(actor.ActorId, viseme);
                    break;
                case SpriteFrameEventKind.Audio:
                    AudioDirector.Instance?.PlayCue(frameEvent.Id);
                    break;
                case SpriteFrameEventKind.Caption:
                    if (!string.Equals(
                            frameEvent.Id,
                            m_CurrentEntry.LocalizationKey,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Dialogue frame event '{frameEvent.Id}' does not match " +
                            $"'{m_CurrentEntry.LocalizationKey}'.");
                    }
                    break;
                case SpriteFrameEventKind.InteractionRelease:
                    m_InteractionReleased = true;
                    break;
            }
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
    }
}
