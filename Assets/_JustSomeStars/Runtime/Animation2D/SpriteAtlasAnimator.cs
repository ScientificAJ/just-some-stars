using System;
using UnityEngine;

namespace JustSomeStars.Runtime.Animation2D
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteAtlasAnimator : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer targetRenderer;

        private SpriteAnimationClipDefinition currentClip;
        private float elapsedInFrame;

        public event Action<SpriteFrameEvent> FrameEventEmitted;

        public SpriteAnimationClipDefinition CurrentClip => currentClip;
        public int CurrentFrameIndex { get; private set; }
        public bool IsPlaying { get; private set; }

        public void Configure(SpriteRenderer renderer)
        {
            targetRenderer = renderer != null
                ? renderer
                : throw new ArgumentNullException(nameof(renderer));
        }

        public void Play(SpriteAnimationClipDefinition clip)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }
            clip.ValidateOrThrow();
            RequireRenderer();

            currentClip = clip;
            CurrentFrameIndex = 0;
            elapsedInFrame = 0f;
            IsPlaying = true;
            ApplyCurrentFrame(emitEvents: true);
        }

        public void Stop()
        {
            IsPlaying = false;
            elapsedInFrame = 0f;
        }

        public void Advance(float deltaSeconds)
        {
            if (deltaSeconds < 0f || float.IsNaN(deltaSeconds) ||
                float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            }
            if (!IsPlaying || currentClip == null || deltaSeconds == 0f)
            {
                return;
            }

            elapsedInFrame += deltaSeconds;
            while (IsPlaying &&
                   elapsedInFrame >= currentClip.FrameDurations[CurrentFrameIndex])
            {
                elapsedInFrame -= currentClip.FrameDurations[CurrentFrameIndex];
                var next = CurrentFrameIndex + 1;
                if (next >= currentClip.Frames.Count)
                {
                    if (currentClip.LoopMode == SpriteAnimationLoopMode.Loop)
                    {
                        CurrentFrameIndex = 0;
                        ApplyCurrentFrame(emitEvents: true);
                        continue;
                    }

                    CurrentFrameIndex = currentClip.Frames.Count - 1;
                    elapsedInFrame = 0f;
                    IsPlaying = false;
                    ApplyCurrentFrame(emitEvents: false);
                    return;
                }

                CurrentFrameIndex = next;
                ApplyCurrentFrame(emitEvents: true);
            }
        }

        private void Awake()
        {
            targetRenderer ??= GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            if (IsPlaying)
            {
                Advance(Time.deltaTime);
            }
        }

        private void ApplyCurrentFrame(bool emitEvents)
        {
            targetRenderer.sprite = currentClip.Frames[CurrentFrameIndex];
            if (!emitEvents)
            {
                return;
            }
            foreach (var frameEvent in currentClip.FrameEvents)
            {
                if (frameEvent.FrameIndex == CurrentFrameIndex)
                {
                    FrameEventEmitted?.Invoke(frameEvent);
                }
            }
        }

        private void RequireRenderer()
        {
            targetRenderer ??= GetComponent<SpriteRenderer>();
            if (targetRenderer == null)
            {
                throw new InvalidOperationException(
                    "SpriteAtlasAnimator requires a configured SpriteRenderer.");
            }
        }
    }
}
