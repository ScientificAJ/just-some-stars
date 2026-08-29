using System;
using System.Collections.Generic;
using System.Linq;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Rendering2D;
using UnityEngine;

namespace JustSomeStars.Runtime.Discovery
{
    [CreateAssetMenu(
        fileName = "PhenomenonDefinition",
        menuName = "Just Some Stars/Discovery/Phenomenon Definition")]
    public sealed class PhenomenonDefinition : ScriptableObject
    {
        [SerializeField] private string stableId;
        [SerializeField] private string scienceSourceId;
        [SerializeField] private LayerBand depthBand = LayerBand.Gameplay;
        [SerializeField] private LensFocusBehavior focusBehavior =
            LensFocusBehavior.Point;
        [SerializeField] private LensMode[] observableModes =
            Array.Empty<LensMode>();
        [SerializeField] private string correctHypothesisId;
        [SerializeField] private string guidedHintKey;
        [SerializeField] private string deepDetailKey;
        [SerializeField, Min(0.01f)] private float focusRadius = 0.75f;

        public ContentId StableId => new ContentId(stableId);

        public ContentId ScienceSourceId => new ContentId(scienceSourceId);

        public LayerBand DepthBand => depthBand;

        public LensFocusBehavior FocusBehavior => focusBehavior;

        public IReadOnlyList<LensMode> ObservableModes => observableModes;

        public ContentId CorrectHypothesisId =>
            new ContentId(correctHypothesisId);

        public float FocusRadius => focusRadius;

        public void Configure(
            string id,
            string sourceId,
            LayerBand band,
            LensFocusBehavior behavior,
            LensMode[] modes,
            string correctHypothesis,
            string guidedHint,
            string deepDetail,
            float authoredFocusRadius)
        {
            stableId = id;
            scienceSourceId = sourceId;
            depthBand = band;
            focusBehavior = behavior;
            observableModes = modes != null
                ? (LensMode[])modes.Clone()
                : null;
            correctHypothesisId = correctHypothesis;
            guidedHintKey = guidedHint;
            deepDetailKey = deepDetail;
            focusRadius = authoredFocusRadius;
            ValidateOrThrow();
        }

        public bool Supports(LensMode mode)
        {
            return observableModes != null &&
                Array.IndexOf(observableModes, mode) >= 0;
        }

        public string GetPresentationKey(ScienceDepth scienceDepth)
        {
            if (!Enum.IsDefined(typeof(ScienceDepth), scienceDepth))
            {
                throw new ArgumentOutOfRangeException(nameof(scienceDepth));
            }

            ValidateOrThrow();
            return scienceDepth == ScienceDepth.Deep
                ? deepDetailKey
                : guidedHintKey;
        }

        public void ValidateOrThrow()
        {
            _ = StableId;
            _ = ScienceSourceId;
            _ = CorrectHypothesisId;
            if (!Enum.IsDefined(typeof(LayerBand), depthBand) ||
                depthBand == LayerBand.Hud)
            {
                throw new InvalidOperationException(
                    $"Phenomenon '{stableId}' requires a world composition band.");
            }

            if (!Enum.IsDefined(typeof(LensFocusBehavior), focusBehavior))
            {
                throw new InvalidOperationException(
                    $"Phenomenon '{stableId}' has an invalid focus behavior.");
            }

            if (observableModes == null || observableModes.Length == 0 ||
                observableModes.Any(mode => !Enum.IsDefined(
                    typeof(LensMode), mode)) ||
                observableModes.Distinct().Count() != observableModes.Length)
            {
                throw new InvalidOperationException(
                    $"Phenomenon '{stableId}' requires unique observable modes.");
            }

            if (string.IsNullOrWhiteSpace(guidedHintKey) ||
                !string.Equals(
                    guidedHintKey,
                    guidedHintKey.Trim(),
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(deepDetailKey) ||
                !string.Equals(
                    deepDetailKey,
                    deepDetailKey.Trim(),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Phenomenon '{stableId}' requires canonical presentation keys.");
            }

            if (focusRadius <= 0f || float.IsNaN(focusRadius) ||
                float.IsInfinity(focusRadius))
            {
                throw new InvalidOperationException(
                    $"Phenomenon '{stableId}' requires a positive focus radius.");
            }
        }
    }
}
