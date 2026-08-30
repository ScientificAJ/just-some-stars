using System;
using JustSomeStars.Runtime.Core;
using UnityEngine;

namespace JustSomeStars.Runtime.Atlas
{
    [CreateAssetMenu(
        fileName = "ScienceSourceDefinition",
        menuName = "Just Some Stars/Atlas/Science Source")]
    public sealed class ScienceSourceDefinition : ScriptableObject
    {
        [SerializeField] private string stableId;
        [SerializeField] private string title;
        [SerializeField] private string publisher;
        [SerializeField] private string sourceUrl;
        [SerializeField, TextArea] private string useNote;

        public ContentId StableId => new ContentId(stableId);
        public string Title => title;
        public string Publisher => publisher;
        public string SourceUrl => sourceUrl;
        public string UseNote => useNote;

        public void Configure(
            string id,
            string authoredTitle,
            string authoredPublisher,
            string url,
            string note)
        {
            stableId = id;
            title = authoredTitle;
            publisher = authoredPublisher;
            sourceUrl = url;
            useNote = note;
            ValidateOrThrow();
        }

        public void ValidateOrThrow()
        {
            _ = StableId;
            RequireText(title, nameof(title));
            RequireText(publisher, nameof(publisher));
            RequireText(useNote, nameof(useNote));
            if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) ||
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Science source '{stableId}' requires an HTTPS source URL.");
            }
        }

        private static void RequireText(string value, string role)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Science source {role} must be canonical and non-empty.");
            }
        }
    }
}
