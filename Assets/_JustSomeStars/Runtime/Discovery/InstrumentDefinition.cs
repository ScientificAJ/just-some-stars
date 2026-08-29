using System;
using System.Collections.Generic;
using System.Linq;
using JustSomeStars.Runtime.Core;
using UnityEngine;

namespace JustSomeStars.Runtime.Discovery
{
    [CreateAssetMenu(
        fileName = "InstrumentDefinition",
        menuName = "Just Some Stars/Discovery/Instrument Definition")]
    public sealed class InstrumentDefinition : ScriptableObject
    {
        [SerializeField] private string stableId;
        [SerializeField] private LensMode[] supportedModes =
            Array.Empty<LensMode>();
        [SerializeField, Min(0.01f)] private float scanDurationSeconds = 1f;

        public ContentId StableId => new ContentId(stableId);

        public IReadOnlyList<LensMode> SupportedModes => supportedModes;

        public float ScanDurationSeconds => scanDurationSeconds;

        public void Configure(
            string id,
            LensMode[] modes,
            float scanDuration)
        {
            stableId = id;
            supportedModes = modes != null
                ? (LensMode[])modes.Clone()
                : null;
            scanDurationSeconds = scanDuration;
            ValidateOrThrow();
        }

        public bool Supports(LensMode mode)
        {
            return supportedModes != null &&
                Array.IndexOf(supportedModes, mode) >= 0;
        }

        public bool IsCompatibleWith(
            PhenomenonDefinition phenomenon,
            LensMode mode)
        {
            if (phenomenon == null)
            {
                return false;
            }

            return Supports(mode) && phenomenon.Supports(mode);
        }

        public void ValidateOrThrow()
        {
            _ = StableId;
            if (supportedModes == null || supportedModes.Length == 0 ||
                supportedModes.Any(mode => !Enum.IsDefined(
                    typeof(LensMode), mode)) ||
                supportedModes.Distinct().Count() != supportedModes.Length)
            {
                throw new InvalidOperationException(
                    $"Instrument '{stableId}' requires unique valid Lens modes.");
            }

            if (scanDurationSeconds <= 0f ||
                float.IsNaN(scanDurationSeconds) ||
                float.IsInfinity(scanDurationSeconds))
            {
                throw new InvalidOperationException(
                    $"Instrument '{stableId}' requires a positive scan duration.");
            }
        }
    }
}
