using System;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Missions;
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
        [SerializeField, Min(0.05f)] private float minimumPresentationSeconds = 0.2f;

        public int PresentationCount { get; private set; }
        public string CurrentDialogueId { get; private set; } = string.Empty;

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

            CurrentDialogueId = entry.StableId.Value;
            speakerLabel.text = entry.SpeakerId.Value
                .Replace("crew.", string.Empty)
                .Replace("robot.", string.Empty)
                .ToUpperInvariant();
            bodyLabel.text = localizedText;
            panel.SetActive(true);
            PresentationCount++;
            var elapsed = 0f;
            try
            {
                while (elapsed < minimumPresentationSeconds)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                    elapsed += Time.unscaledDeltaTime;
                }
            }
            finally
            {
                if (panel != null)
                {
                    panel.SetActive(false);
                }

                CurrentDialogueId = string.Empty;
            }
        }
    }
}
