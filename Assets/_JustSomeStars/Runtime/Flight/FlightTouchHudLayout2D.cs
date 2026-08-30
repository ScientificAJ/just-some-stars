using System;
using JustSomeStars.Runtime.Accessibility;
using UnityEngine;

namespace JustSomeStars.Runtime.Flight
{
    [DisallowMultipleComponent]
    public sealed class FlightTouchHudLayout2D : MonoBehaviour
    {
        [SerializeField] private RectTransform steering;
        [SerializeField] private RectTransform lane;
        [SerializeField] private RectTransform boost;
        [SerializeField] private RectTransform brakeDrift;

        private AnchorRange[] authoredLayout;

        public void Apply(GameSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            EnsureCaptured();
            for (var index = 0; index < authoredLayout.Length; index++)
            {
                var target = TargetAt(index);
                var authored = authoredLayout[index];
                target.anchorMin = settings.LeftHandedControls
                    ? new Vector2(1f - authored.Maximum.x, authored.Minimum.y)
                    : authored.Minimum;
                target.anchorMax = settings.LeftHandedControls
                    ? new Vector2(1f - authored.Minimum.x, authored.Maximum.y)
                    : authored.Maximum;
            }
        }

        private void Awake()
        {
            EnsureCaptured();
        }

        private void EnsureCaptured()
        {
            if (authoredLayout != null)
            {
                return;
            }

            for (var index = 0; index < 4; index++)
            {
                if (TargetAt(index) == null)
                {
                    throw new InvalidOperationException(
                        "FlightTouchHudLayout2D requires all four controls.");
                }
            }

            authoredLayout = new AnchorRange[4];
            for (var index = 0; index < authoredLayout.Length; index++)
            {
                var target = TargetAt(index);
                authoredLayout[index] = new AnchorRange(
                    target.anchorMin,
                    target.anchorMax);
            }
        }

        private RectTransform TargetAt(int index)
        {
            return index switch
            {
                0 => steering,
                1 => lane,
                2 => boost,
                3 => brakeDrift,
                _ => throw new ArgumentOutOfRangeException(nameof(index)),
            };
        }

        private readonly struct AnchorRange
        {
            public AnchorRange(Vector2 minimum, Vector2 maximum)
            {
                Minimum = minimum;
                Maximum = maximum;
            }

            public Vector2 Minimum { get; }
            public Vector2 Maximum { get; }
        }
    }
}
