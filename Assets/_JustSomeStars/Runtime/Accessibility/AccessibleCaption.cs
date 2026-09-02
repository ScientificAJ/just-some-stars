using System;
using TMPro;
using UnityEngine;

namespace JustSomeStars.Runtime.Accessibility
{
    [DisallowMultipleComponent]
    public sealed class AccessibleCaption : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text speakerLabel;
        [SerializeField] private TMP_Text bodyLabel;

        public void Present(string speaker, string body)
        {
            if (speakerLabel != null && bodyLabel != null)
            {
                speakerLabel.text = speaker ?? string.Empty;
                bodyLabel.text = body ?? string.Empty;
                return;
            }
            if (label == null)
            {
                throw new InvalidOperationException(
                    "Accessible captions require a text label.");
            }
            label.text = string.IsNullOrWhiteSpace(speaker)
                ? body ?? string.Empty
                : $"{speaker}: {body ?? string.Empty}";
        }

        public void Apply(bool enabled)
        {
            (root != null ? root : gameObject).SetActive(enabled);
        }
    }
}
