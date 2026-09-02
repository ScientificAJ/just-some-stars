using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Animation2D;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Crew;
using UnityEngine;

namespace JustSomeStars.Runtime.Interaction
{
    [DisallowMultipleComponent]
    public sealed class MirraInteractionParticipant2D : MonoBehaviour,
        IInteractionParticipant2D
    {
        [SerializeField] private string actorId;
        [SerializeField] private InteractionActorKind actorKind;
        [SerializeField] private InteractionFacing facing = InteractionFacing.Right;
        [SerializeField] private InteractionDepthBand depthBand =
            InteractionDepthBand.Gameplay;
        [SerializeField] private LayerMask allowedPhysicsLayers = ~0;
        [SerializeField] private string[] toolIds = Array.Empty<string>();
        [SerializeField] private LayeredCharacterRenderer layeredRenderer;
        [SerializeField] private SpriteAtlasAnimator atlasAnimator;
        [SerializeField] private CharacterSpriteSet spriteSet;
        [SerializeField] private string idleClipId;
        [SerializeField, Min(0.01f)] private float moveSeconds = 0.12f;
        [SerializeField] private Transform recoveryAnchor;
        [SerializeField] private Camera compositionCamera;
        [SerializeField, Min(0.1f)] private float offCameraDistance = 7f;

        private CrewRecovery m_Recovery;
        private float m_NextRecoveryCheck;

        public ContentId ActorId => new ContentId(actorId);
        public InteractionActorKind ActorKind => actorKind;
        public Vector2 Position => transform.position;
        public InteractionFacing Facing => facing;
        public InteractionDepthBand DepthBand => depthBand;
        public int AllowedPhysicsLayers => allowedPhysicsLayers;
        public GameMode Mode => GameMode.Surface;
        public IReadOnlyCollection<ContentId> Tools => toolIds
            .Select(id => new ContentId(id))
            .ToArray();
        public int RecoveryCount { get; private set; }

        public async ValueTask MoveToAsync(
            Vector2 destination,
            CancellationToken cancellationToken)
        {
            ValidateOrThrow();
            var start = (Vector2)transform.position;
            var elapsed = 0f;
            while (elapsed < moveSeconds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                elapsed += Time.unscaledDeltaTime;
                transform.position = Vector2.Lerp(
                    start,
                    destination,
                    Mathf.Clamp01(elapsed / moveSeconds));
            }

            transform.position = destination;
        }

        public async ValueTask PlayAsync(
            SpriteAnimationClipDefinition clip,
            CancellationToken cancellationToken)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            clip.ValidateOrThrow();
            if (!clip.FrameEvents.Any(frameEvent =>
                    frameEvent.Kind == SpriteFrameEventKind.InteractionRelease ||
                    frameEvent.Kind == SpriteFrameEventKind.Interaction &&
                    string.Equals(
                        frameEvent.Id,
                        "interact-commit",
                        StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Interaction clip '{clip.StableId}' has no authored release event.");
            }

            var released = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            void OnFrameEvent(SpriteFrameEvent frameEvent)
            {
                if (frameEvent.Kind == SpriteFrameEventKind.InteractionRelease ||
                    frameEvent.Kind == SpriteFrameEventKind.Interaction &&
                    string.Equals(
                        frameEvent.Id,
                        "interact-commit",
                        StringComparison.Ordinal))
                {
                    released.TrySetResult(true);
                }
            }

            if (layeredRenderer != null)
                layeredRenderer.FrameEventEmitted += OnFrameEvent;
            else
                atlasAnimator.FrameEventEmitted += OnFrameEvent;
            using var cancellation = cancellationToken.Register(() =>
                released.TrySetCanceled(cancellationToken));
            try
            {
                if (layeredRenderer != null)
                    layeredRenderer.Play("interact");
                else
                    atlasAnimator.Play(clip);
                await released.Task;
            }
            finally
            {
                if (layeredRenderer != null)
                {
                    layeredRenderer.FrameEventEmitted -= OnFrameEvent;
                    layeredRenderer.Play("idle");
                }
                else
                {
                    atlasAnimator.FrameEventEmitted -= OnFrameEvent;
                    if (spriteSet != null && !string.IsNullOrWhiteSpace(idleClipId))
                        atlasAnimator.Play(spriteSet.FindClip(idleClipId));
                }
            }
        }

        public void Recover(Vector2 recoveryPosition)
        {
            transform.position = recoveryPosition;
            RecoveryCount++;
        }

        public void ValidateOrThrow()
        {
            _ = ActorId;
            if (!Enum.IsDefined(typeof(InteractionActorKind), actorKind) ||
                !Enum.IsDefined(typeof(InteractionFacing), facing) ||
                !Enum.IsDefined(typeof(InteractionDepthBand), depthBand) ||
                moveSeconds <= 0f || float.IsNaN(moveSeconds) ||
                float.IsInfinity(moveSeconds) ||
                toolIds == null || toolIds.Select(id => new ContentId(id)).Distinct()
                    .Count() != toolIds.Length ||
                (layeredRenderer == null &&
                    (atlasAnimator == null || spriteSet == null ||
                     string.IsNullOrWhiteSpace(idleClipId))))
            {
                throw new InvalidOperationException(
                    $"Mirra interaction actor '{actorId}' is not production-ready.");
            }
        }

        public CrewRecoveryDecision EvaluateRecoveryNow()
        {
            ValidateOrThrow();
            m_Recovery ??= new CrewRecovery(1.2f, offCameraDistance);
            var recoveryPosition = recoveryAnchor != null
                ? (Vector2)recoveryAnchor.position
                : Position;
            var actorVisible = IsVisible(Position);
            var recoveryVisible = IsVisible(recoveryPosition);
            var decision = m_Recovery.Evaluate(new CrewRecoveryContext(
                Position,
                recoveryPosition,
                actorVisible,
                recoveryVisible,
                actorVisible ? 0f : 2f,
                Vector2.Distance(Position, recoveryPosition),
                routeAvailable: true));
            if (decision.Kind == CrewRecoveryKind.HiddenWarp)
            {
                Recover(decision.Position);
            }

            return decision;
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime < m_NextRecoveryCheck)
            {
                return;
            }

            m_NextRecoveryCheck = Time.unscaledTime + 0.25f;
            if (recoveryAnchor != null && compositionCamera != null)
            {
                EvaluateRecoveryNow();
            }
        }

        private bool IsVisible(Vector2 position)
        {
            if (compositionCamera == null)
            {
                return false;
            }

            var viewport = compositionCamera.WorldToViewportPoint(position);
            return viewport.z >= 0f && viewport.x >= 0f && viewport.x <= 1f &&
                viewport.y >= 0f && viewport.y <= 1f;
        }
    }
}
