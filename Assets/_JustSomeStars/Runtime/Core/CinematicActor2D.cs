using System;
using System.Collections.Generic;
using System.Linq;
using JustSomeStars.Runtime.Animation2D;
using UnityEngine;

namespace JustSomeStars.Runtime.Core
{
    [DisallowMultipleComponent]
    public sealed class CinematicActor2D : MonoBehaviour
    {
        [SerializeField] private string actorId;
        [SerializeField] private LayeredCharacterRenderer captainRenderer;
        [SerializeField] private SpriteAtlasAnimator spriteAnimator;
        [SerializeField] private CharacterSpriteSet spriteSet;
        [SerializeField] private CharacterSpriteSet cinematicSpriteSet;

        private SpriteAnimationClipDefinition m_CaptainPerformance;
        private int m_LastCaptainFrame = -1;

        public string ActorId => actorId;

        public event Action<CinematicActor2D, SpriteFrameEvent> FrameEventEmitted;

        public void Configure(
            string id,
            LayeredCharacterRenderer captain,
            SpriteAtlasAnimator animator,
            CharacterSpriteSet authoredSpriteSet,
            CharacterSpriteSet authoredCinematicSpriteSet = null)
        {
            if (string.IsNullOrWhiteSpace(id) ||
                (captain == null && (animator == null || authoredSpriteSet == null)) ||
                (captain != null && (animator != null || authoredSpriteSet != null)))
            {
                throw new InvalidOperationException(
                    "Cinematic actor requires one canonical sprite performance route.");
            }
            UnbindSources();
            actorId = id.Trim().ToLowerInvariant();
            captainRenderer = captain;
            spriteAnimator = animator;
            spriteSet = authoredSpriteSet;
            cinematicSpriteSet = authoredCinematicSpriteSet;
            ValidateOrThrow();
            if (isActiveAndEnabled) BindSources();
        }

        public bool PlayBody(string clipOrMotionId)
        {
            if (string.IsNullOrWhiteSpace(clipOrMotionId)) return false;
            if (TryResolvePerformance(clipOrMotionId, out var performance))
            {
                if (captainRenderer != null)
                {
                    captainRenderer.Play(performance.PlaybackMotionId);
                    m_CaptainPerformance = performance;
                    m_LastCaptainFrame = 0;
                    EmitPerformanceFrame(0);
                    return true;
                }

                spriteAnimator.Play(performance);
                return true;
            }
            if (captainRenderer != null)
            {
                m_CaptainPerformance = null;
                m_LastCaptainFrame = -1;
                captainRenderer.Play(clipOrMotionId);
                return true;
            }
            if (spriteAnimator == null || spriteSet == null) return false;
            try
            {
                spriteAnimator.Play(spriteSet.FindClip(clipOrMotionId));
                return true;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }

        public bool HasPerformanceEvent(
            string semanticId,
            SpriteFrameEventKind kind)
        {
            return TryResolvePerformance(semanticId, out var performance) &&
                performance.FrameEvents.Any(frameEvent => frameEvent.Kind == kind);
        }

        public bool HasPerformance(string semanticId) =>
            TryResolvePerformance(semanticId, out _);

        public void EndPerformance()
        {
            m_CaptainPerformance = null;
            m_LastCaptainFrame = -1;
            if (captainRenderer != null)
            {
                captainRenderer.Play("idle");
                return;
            }

            if (spriteAnimator == null || spriteSet == null) return;
            var facing = CurrentFacing();
            try
            {
                spriteAnimator.Play(spriteSet.FindClip(
                    $"{actorId}.idle.{FacingId(facing)}"));
            }
            catch (KeyNotFoundException)
            {
                spriteAnimator.Stop();
            }
        }

        public void ValidateOrThrow()
        {
            if (string.IsNullOrWhiteSpace(actorId) ||
                !string.Equals(actorId, actorId.Trim(), StringComparison.Ordinal) ||
                (captainRenderer == null &&
                    (spriteAnimator == null || spriteSet == null)) ||
                (captainRenderer != null &&
                    (spriteAnimator != null || spriteSet != null)) ||
                cinematicSpriteSet == null ||
                !string.Equals(
                    cinematicSpriteSet.CharacterId,
                    actorId,
                    StringComparison.Ordinal) ||
                cinematicSpriteSet.Clips.Count == 0)
            {
                throw new InvalidOperationException(
                    "Cinematic actors require one body route and a matching " +
                    "frame-event performance set.");
            }

            foreach (var clip in cinematicSpriteSet.Clips)
            {
                clip.ValidateOrThrow();
                if (string.IsNullOrWhiteSpace(clip.PlaybackMotionId))
                {
                    throw new InvalidOperationException(
                        $"Cinematic performance '{clip.StableId}' has no body motion.");
                }
            }
        }

        private void OnEnable() => BindSources();

        private void OnDisable() => UnbindSources();

        private void LateUpdate()
        {
            if (captainRenderer == null || m_CaptainPerformance == null) return;
            if (!string.Equals(
                    captainRenderer.CurrentMotion,
                    m_CaptainPerformance.PlaybackMotionId,
                    StringComparison.Ordinal))
            {
                m_CaptainPerformance = null;
                m_LastCaptainFrame = -1;
                return;
            }

            var current = captainRenderer.CurrentFrameIndex;
            if (current == m_LastCaptainFrame) return;
            if (current > m_LastCaptainFrame)
            {
                for (var frame = m_LastCaptainFrame + 1; frame <= current; frame++)
                    EmitPerformanceFrame(frame);
            }
            else if (m_CaptainPerformance.LoopMode == SpriteAnimationLoopMode.Loop)
            {
                for (var frame = m_LastCaptainFrame + 1;
                     frame < m_CaptainPerformance.Frames.Count;
                     frame++)
                {
                    EmitPerformanceFrame(frame);
                }
                for (var frame = 0; frame <= current; frame++)
                    EmitPerformanceFrame(frame);
            }
            m_LastCaptainFrame = current;
        }

        private void BindSources()
        {
            if (captainRenderer != null)
            {
                captainRenderer.FrameEventEmitted -= OnFrameEvent;
                captainRenderer.FrameEventEmitted += OnFrameEvent;
            }
            if (spriteAnimator != null)
            {
                spriteAnimator.FrameEventEmitted -= OnFrameEvent;
                spriteAnimator.FrameEventEmitted += OnFrameEvent;
            }
        }

        private void UnbindSources()
        {
            if (captainRenderer != null)
                captainRenderer.FrameEventEmitted -= OnFrameEvent;
            if (spriteAnimator != null)
                spriteAnimator.FrameEventEmitted -= OnFrameEvent;
        }

        private void OnFrameEvent(SpriteFrameEvent frameEvent)
        {
            if (captainRenderer != null && m_CaptainPerformance != null) return;
            FrameEventEmitted?.Invoke(this, frameEvent);
        }

        private void EmitPerformanceFrame(int frameIndex)
        {
            if (m_CaptainPerformance == null || frameIndex < 0 ||
                frameIndex >= m_CaptainPerformance.Frames.Count)
            {
                return;
            }
            foreach (var frameEvent in m_CaptainPerformance.FrameEvents)
            {
                if (frameEvent.FrameIndex == frameIndex)
                    FrameEventEmitted?.Invoke(this, frameEvent);
            }
        }

        private bool TryResolvePerformance(
            string semanticId,
            out SpriteAnimationClipDefinition performance)
        {
            performance = null;
            if (cinematicSpriteSet == null || string.IsNullOrWhiteSpace(semanticId))
                return false;
            var canonical = semanticId.Trim().ToLowerInvariant();
            var candidates = new[]
            {
                $"{actorId}.{canonical}.{FacingId(CurrentFacing())}",
                canonical,
            };
            foreach (var candidate in candidates)
            {
                try
                {
                    performance = cinematicSpriteSet.FindClip(candidate);
                    return true;
                }
                catch (KeyNotFoundException)
                {
                }
            }
            return false;
        }

        private SpriteFacing CurrentFacing()
        {
            if (captainRenderer != null) return captainRenderer.CurrentFacing;
            return spriteAnimator?.CurrentClip?.Facing ?? SpriteFacing.Right;
        }

        private static string FacingId(SpriteFacing facing) => facing switch
        {
            SpriteFacing.Left => "left",
            SpriteFacing.Right => "right",
            _ => "right",
        };
    }
}
