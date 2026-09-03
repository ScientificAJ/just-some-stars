using System;
using System.Collections.Generic;
using System.Linq;
using JustSomeStars.Runtime.Animation2D;
using JustSomeStars.Runtime.Core;
using UnityEngine;

namespace JustSomeStars.Runtime.Crew
{
    [DisallowMultipleComponent]
    public sealed class MirraCrewActorRuntime2D : MonoBehaviour,
        ICrewActionRuntime2D
    {
        [SerializeField] private string actorId;
        [SerializeField] private SpriteAtlasAnimator atlasAnimator;
        [SerializeField] private CharacterSpriteSet spriteSet;
        [SerializeField] private string idleClipId;
        [SerializeField] private string runClipId;
        [SerializeField] private string interactClipId;
        [SerializeField] private Transform recoveryAnchor;
        [SerializeField] private Camera compositionCamera;
        [SerializeField] private string initialTraversalNodeId =
            "route.mirra.twilight";
        [SerializeField, Min(0.1f)] private float movementSpeed = 4f;

        private Vector2 m_TargetPosition;
        private bool m_HasMovementTarget;
        private string m_CurrentClipId;
        private ContentId m_CurrentTraversalNodeId;

        public ContentId ActorId => new ContentId(actorId);
        public ContentId CurrentTraversalNodeId =>
            m_CurrentTraversalNodeId.IsValid
                ? m_CurrentTraversalNodeId
                : new ContentId(initialTraversalNodeId);
        public bool CameraVisible => IsVisible(transform.position);
        public Vector2 RecoveryPosition => recoveryAnchor.position;
        public bool RecoveryPositionVisible => IsVisible(recoveryAnchor.position);

        public void ValidateOrThrow()
        {
            _ = ActorId;
            _ = new ContentId(initialTraversalNodeId);
            if (atlasAnimator == null || spriteSet == null ||
                recoveryAnchor == null || compositionCamera == null ||
                string.IsNullOrWhiteSpace(idleClipId) ||
                string.IsNullOrWhiteSpace(runClipId) ||
                string.IsNullOrWhiteSpace(interactClipId) ||
                movementSpeed <= 0f || float.IsNaN(movementSpeed) ||
                float.IsInfinity(movementSpeed))
            {
                throw new InvalidOperationException(
                    $"Crew actor runtime '{actorId}' is not production-ready.");
            }

            spriteSet.FindClip(idleClipId);
            spriteSet.FindClip(runClipId);
            spriteSet.FindClip(interactClipId);
        }

        public CrewRecoveryDecision EvaluateRecovery(
            Vector2 desiredPosition,
            float maximumRouteDistance)
        {
            var recovery = new CrewRecovery(1.2f, maximumRouteDistance);
            return recovery.Evaluate(new CrewRecoveryContext(
                transform.position,
                recoveryAnchor != null
                    ? (Vector2)recoveryAnchor.position
                    : desiredPosition,
                CameraVisible,
                RecoveryPositionVisible,
                blockedSeconds: 0f,
                Vector2.Distance(transform.position, desiredPosition),
                routeAvailable: true));
        }

        public void Join(Vector2 position) => MoveTo(position);

        public void Follow(Vector2 position) => MoveTo(position);

        public void Position(Vector2 position) => MoveTo(position);

        public void Traverse(IReadOnlyList<TraversalNode2D> path)
        {
            if (path == null || path.Count == 0)
            {
                throw new ArgumentException(
                    "Crew traversal requires an authored non-empty path.",
                    nameof(path));
            }

            var destination = path[path.Count - 1];
            m_CurrentTraversalNodeId = destination.Id;
            MoveTo(destination.Position);
        }

        public void Investigate(Vector2 position) => MoveTo(position);

        public void Interact(Vector2 position)
        {
            transform.position = position;
            m_HasMovementTarget = false;
            Play(interactClipId);
        }

        public void React(Vector2 position)
        {
            m_TargetPosition = position;
            Play(interactClipId);
        }

        public void Speak() => Play(idleClipId);

        public void Converse() => Play(idleClipId);

        public void EnterCinematic()
        {
            m_HasMovementTarget = false;
            Play(idleClipId);
        }

        public void Wait()
        {
            m_HasMovementTarget = false;
            Play(idleClipId);
        }

        public void Recover(Vector2 position)
        {
            transform.position = position;
            m_HasMovementTarget = false;
            Play(idleClipId);
        }

        private void Awake()
        {
            if (!string.IsNullOrWhiteSpace(initialTraversalNodeId))
            {
                m_CurrentTraversalNodeId = new ContentId(initialTraversalNodeId);
            }
        }

        private void Update()
        {
            if (HasExternalClipOwnership())
            {
                m_HasMovementTarget = false;
                return;
            }
            if (!m_HasMovementTarget)
            {
                return;
            }

            transform.position = Vector2.MoveTowards(
                transform.position,
                m_TargetPosition,
                movementSpeed * Time.deltaTime);
            if (Vector2.SqrMagnitude(
                    (Vector2)transform.position - m_TargetPosition) <= 0.0001f)
            {
                transform.position = m_TargetPosition;
                m_HasMovementTarget = false;
                Play(idleClipId);
            }
        }

        private void MoveTo(Vector2 position)
        {
            m_TargetPosition = position;
            m_HasMovementTarget = true;
            Play(runClipId);
        }

        private void Play(string clipId)
        {
            if (HasExternalClipOwnership())
            {
                return;
            }
            if (string.Equals(m_CurrentClipId, clipId, StringComparison.Ordinal))
            {
                return;
            }

            atlasAnimator.Play(spriteSet.FindClip(clipId));
            m_CurrentClipId = clipId;
        }

        private bool HasExternalClipOwnership()
        {
            var current = atlasAnimator != null
                ? atlasAnimator.CurrentClip
                : null;
            return current != null && spriteSet != null &&
                !spriteSet.Clips.Contains(current);
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
