using System;
using System.Collections.Generic;
using UnityEngine;

namespace JustSomeStars.Runtime.Atlas
{
    [Serializable]
    public sealed class LocalizedEnglishText
    {
        [SerializeField] private string key;
        [SerializeField, TextArea] private string value;

        public LocalizedEnglishText(string key, string value)
        {
            this.key = key;
            this.value = value;
        }

        public string Key => key;
        public string Value => value;
    }

    [CreateAssetMenu(
        fileName = "LocalizedEnglishCatalog",
        menuName = "Just Some Stars/Localization/English Catalog")]
    public sealed class LocalizedEnglishCatalog : ScriptableObject
    {
        [SerializeField] private LocalizedEnglishText[] entries =
            Array.Empty<LocalizedEnglishText>();

        public void Configure(LocalizedEnglishText[] authoredEntries)
        {
            entries = authoredEntries != null
                ? (LocalizedEnglishText[])authoredEntries.Clone()
                : null;
            ValidateOrThrow();
        }

        public string Resolve(string key)
        {
            ValidateOrThrow();
            foreach (var entry in entries)
            {
                if (string.Equals(entry.Key, key, StringComparison.Ordinal))
                {
                    return entry.Value;
                }
            }

            throw new KeyNotFoundException(
                $"English localization key '{key}' is not authored.");
        }

        public void ValidateOrThrow()
        {
            if (entries == null || entries.Length == 0)
            {
                throw new InvalidOperationException(
                    "English localization catalog cannot be empty.");
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key) ||
                    !string.Equals(entry.Key, entry.Key.Trim(), StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(entry.Value) ||
                    !keys.Add(entry.Key))
                {
                    throw new InvalidOperationException(
                        "English localization entries require unique keys and non-empty text.");
                }
            }
        }
    }
}
