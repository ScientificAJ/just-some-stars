using System;
using UnityEngine;

namespace JustSomeStars.Runtime.Animation2D
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteAtlasAnimator))]
    public sealed class MirraProofActorPresenter : MonoBehaviour
    {
        [SerializeField] private SpriteAtlasAnimator animator;
        [SerializeField] private CharacterSpriteSet spriteSet;
        [SerializeField] private string idleClipId;
        [SerializeField] private Rigidbody2D motionSource;

        private string currentClipId;

        public CharacterSpriteSet SpriteSet => spriteSet;
        public string CurrentClipId => currentClipId;

        public void Configure(
            SpriteAtlasAnimator configuredAnimator,
            CharacterSpriteSet configuredSpriteSet,
            string configuredIdleClipId)
        {
            animator = configuredAnimator != null
                ? configuredAnimator
                : throw new ArgumentNullException(nameof(configuredAnimator));
            spriteSet = configuredSpriteSet != null
                ? configuredSpriteSet
                : throw new ArgumentNullException(nameof(configuredSpriteSet));
            idleClipId = !string.IsNullOrWhiteSpace(configuredIdleClipId)
                ? configuredIdleClipId
                : throw new ArgumentException(
                    "Idle clip id is required.",
                    nameof(configuredIdleClipId));
            Play(idleClipId);
        }

        public void SetMotionSource(Rigidbody2D source)
        {
            motionSource = source;
        }

        private void Awake()
        {
            animator ??= GetComponent<SpriteAtlasAnimator>();
            if (spriteSet != null && !string.IsNullOrWhiteSpace(idleClipId))
            {
                Play(idleClipId);
            }
        }

        private void Update()
        {
            if (motionSource == null || spriteSet == null)
            {
                return;
            }
            var facing = motionSource.linearVelocity.x < -0.05f
                ? "left"
                : "right";
            var motion = Mathf.Abs(motionSource.linearVelocity.x) > 0.2f
                ? "run"
                : "idle";
            var separator = idleClipId.IndexOf('.', StringComparison.Ordinal);
            var actorId = separator > 0
                ? idleClipId.Substring(0, separator)
                : spriteSet.CharacterId;
            var desired = actorId + "." + motion + "." + facing;
            if (!string.Equals(currentClipId, desired, StringComparison.Ordinal))
            {
                Play(desired);
            }
        }

        private void Play(string stableClipId)
        {
            var clip = spriteSet.FindClip(stableClipId);
            animator.Play(clip);
            currentClipId = stableClipId;
        }
    }
}
