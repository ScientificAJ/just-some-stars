using System;
using UnityEngine;

namespace JustSomeStars.Runtime.UI
{
    [CreateAssetMenu(
        fileName = "HomemadeSignalUiTokens",
        menuName = "Just Some Stars/UI/Homemade Signal Tokens")]
    public sealed class HomemadeSignalUiTokens : ScriptableObject
    {
        [SerializeField] private Color smokedGlass =
            new Color(0.012f, 0.035f, 0.066f, 0.92f);
        [SerializeField] private Color paintedMetal =
            new Color(0.18f, 0.19f, 0.20f, 1f);
        [SerializeField] private Color brass =
            new Color(0.86f, 0.62f, 0.28f, 1f);
        [SerializeField] private Color signalCyan =
            new Color(0.16f, 0.88f, 0.96f, 1f);
        [SerializeField] private Color warmPaper =
            new Color(0.97f, 0.84f, 0.67f, 1f);
        [SerializeField] private Color bodyText =
            new Color(0.86f, 0.90f, 0.96f, 1f);
        [SerializeField, Min(48f)] private float minimumTouchTargetDp = 48f;
        [SerializeField, Min(12f)] private float minimumBodySp = 14f;
        [SerializeField] private Vector2 phoneReferenceResolution =
            new Vector2(1280f, 720f);
        [SerializeField] private Vector2 foldableReferenceResolution =
            new Vector2(1768f, 884f);

        public Color SmokedGlass => smokedGlass;
        public Color PaintedMetal => paintedMetal;
        public Color Brass => brass;
        public Color SignalCyan => signalCyan;
        public Color WarmPaper => warmPaper;
        public Color BodyText => bodyText;
        public float MinimumTouchTargetDp => minimumTouchTargetDp;
        public float MinimumBodySp => minimumBodySp;
        public Vector2 PhoneReferenceResolution => phoneReferenceResolution;
        public Vector2 FoldableReferenceResolution => foldableReferenceResolution;

        public void ValidateOrThrow()
        {
            if (minimumTouchTargetDp < 48f || minimumBodySp < 14f ||
                phoneReferenceResolution.x <= 0f ||
                phoneReferenceResolution.y <= 0f ||
                foldableReferenceResolution.x <= 0f ||
                foldableReferenceResolution.y <= 0f)
            {
                throw new InvalidOperationException(
                    "Homemade/Signal UI tokens require mobile-safe physical floors.");
            }
        }
    }
}
