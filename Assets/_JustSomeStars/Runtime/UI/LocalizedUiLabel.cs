using System;
using JustSomeStars.Runtime.Atlas;
using TMPro;
using UnityEngine;

namespace JustSomeStars.Runtime.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedUiLabel : MonoBehaviour
    {
        [SerializeField] private LocalizedEnglishCatalog english;
        [SerializeField] private string key;
        [SerializeField] private string[] formatArguments = Array.Empty<string>();

        private void OnEnable()
        {
            Apply();
        }

        public void Configure(
            LocalizedEnglishCatalog catalog,
            string localizationKey,
            params string[] arguments)
        {
            english = catalog ?? throw new ArgumentNullException(nameof(catalog));
            key = string.IsNullOrWhiteSpace(localizationKey)
                ? throw new ArgumentException(
                    "A localization key is required.",
                    nameof(localizationKey))
                : localizationKey;
            formatArguments = arguments != null
                ? (string[])arguments.Clone()
                : Array.Empty<string>();
            Apply();
        }

        public void Apply()
        {
            if (english == null || string.IsNullOrWhiteSpace(key))
            {
                return;
            }
            var text = GetComponent<TMP_Text>();
            text.text = formatArguments == null || formatArguments.Length == 0
                ? english.Resolve(key)
                : Task28English.Format(english, key, formatArguments);
        }
    }
}
