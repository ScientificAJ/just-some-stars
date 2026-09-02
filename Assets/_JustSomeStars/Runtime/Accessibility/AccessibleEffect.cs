using JustSomeStars.Runtime.UI;
using UnityEngine;

namespace JustSomeStars.Runtime.Accessibility
{
    public enum AccessibilityEffectKind
    {
        CameraShake = 0,
        Flashing = 1,
        Motion = 2,
        MotionBlur = 3,
    }

    [DisallowMultipleComponent]
    public sealed class AccessibleEffect : MonoBehaviour
    {
        [SerializeField] private AccessibilityEffectKind kind;
        [SerializeField] private Behaviour effect;

        public void Apply(GameSettings settings)
        {
            if (settings == null || effect == null)
            {
                return;
            }
            effect.enabled = kind switch
            {
                AccessibilityEffectKind.CameraShake => !settings.ReducedCameraShake,
                AccessibilityEffectKind.Flashing => !settings.ReducedFlashing,
                AccessibilityEffectKind.Motion => !settings.ReducedMotion,
                AccessibilityEffectKind.MotionBlur => settings.MotionBlurEnabled,
                _ => effect.enabled,
            };
        }
    }
}
