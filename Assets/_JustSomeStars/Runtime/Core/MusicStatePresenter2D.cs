using System;
using UnityEngine;

namespace JustSomeStars.Runtime.Core
{
    [DisallowMultipleComponent]
    public sealed class MusicStatePresenter2D : MonoBehaviour
    {
        [SerializeField] private string musicStateId;

        public string MusicStateId => musicStateId;

        public void Configure(string stateId)
        {
            musicStateId = stateId;
            ValidateOrThrow();
            if (isActiveAndEnabled) Apply(AudioDirector.Instance);
        }

        public void ValidateOrThrow()
        {
            if (string.IsNullOrWhiteSpace(musicStateId) ||
                !string.Equals(
                    musicStateId,
                    musicStateId.Trim(),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Destination music requires a canonical authored state id.");
            }
        }

        private void OnEnable()
        {
            AudioDirector.Installed -= Apply;
            AudioDirector.Installed += Apply;
            Apply(AudioDirector.Instance);
        }

        private void OnDisable()
        {
            AudioDirector.Installed -= Apply;
        }

        private void Apply(AudioDirector director)
        {
            if (director == null || string.IsNullOrWhiteSpace(musicStateId)) return;
            if (!director.SetMusicState(musicStateId))
            {
                Debug.LogWarning(
                    $"Music state '{musicStateId}' is unavailable.",
                    this);
            }
        }
    }
}
