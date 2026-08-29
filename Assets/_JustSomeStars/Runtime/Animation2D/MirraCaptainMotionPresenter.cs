using JustSomeStars.Runtime.Cosmetics;
using UnityEngine;

namespace JustSomeStars.Runtime.Animation2D
{
    [DisallowMultipleComponent]
    public sealed class MirraCaptainMotionPresenter : MonoBehaviour
    {
        [SerializeField] private LayeredCharacterRenderer characterRenderer;
        [SerializeField] private Rigidbody2D motionSource;

        private CaptainSpriteLoadout loadout;
        private string currentMotion = "idle";
        private SpriteFacing currentFacing = SpriteFacing.Right;

        private void Start()
        {
            if (characterRenderer == null || motionSource == null)
            {
                return;
            }
            loadout = CaptainSpriteLoadout.CreateLaunchLook(
                characterRenderer.CurrentFamily,
                characterRenderer.ActiveLayerCount);
        }

        private void Update()
        {
            if (characterRenderer == null || motionSource == null ||
                loadout == null)
            {
                return;
            }
            var desiredFacing = motionSource.linearVelocity.x < -0.05f
                ? SpriteFacing.Left
                : motionSource.linearVelocity.x > 0.05f
                    ? SpriteFacing.Right
                    : currentFacing;
            var desiredMotion = Mathf.Abs(motionSource.linearVelocity.y) > 0.45f
                ? "jump"
                : Mathf.Abs(motionSource.linearVelocity.x) > 0.2f
                    ? "run"
                    : "idle";
            if (desiredFacing == currentFacing &&
                desiredMotion == currentMotion)
            {
                return;
            }
            characterRenderer.ApplyLoadout(loadout, desiredFacing, desiredMotion);
            currentFacing = desiredFacing;
            currentMotion = desiredMotion;
        }
    }
}
