using System;
using System.Collections.Generic;
using JustSomeStars.Runtime.Animation2D;
using JustSomeStars.Runtime.Core;
using UnityEngine;

namespace JustSomeStars.Runtime.Crew
{
    [DisallowMultipleComponent]
    public sealed class KoroCrewActorRuntime2D : MonoBehaviour,
        ICrewActionRuntime2D
    {
        [SerializeField] private string actorId;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private SpriteAtlasAnimator atlasAnimator;
        [SerializeField] private CharacterSpriteSet spriteSet;
        [SerializeField] private string idleClipId;
        [SerializeField] private string runClipId;
        [SerializeField] private string interactClipId;
        [SerializeField] private Transform recoveryAnchor;
        [SerializeField] private Camera compositionCamera;
        [SerializeField] private string initialTraversalNodeId = "route.koro.start";
        [SerializeField, Min(0.5f)] private float movementSpeed = 3.6f;
        [SerializeField, Min(0.5f)] private float jumpVelocity = 4.8f;

        private readonly Queue<TraversalNode2D> m_Waypoints = new();
        private Vector2 m_FollowTarget;
        private bool m_HasFollowTarget;
        private string m_CurrentClip;
        private ContentId m_CurrentNode;

        public ContentId ActorId => new ContentId(actorId);
        public ContentId CurrentTraversalNodeId => m_CurrentNode.IsValid
            ? m_CurrentNode
            : new ContentId(initialTraversalNodeId);
        public bool CameraVisible => IsVisible(body.position);
        public Vector2 RecoveryPosition => recoveryAnchor.position;
        public bool RecoveryPositionVisible => IsVisible(recoveryAnchor.position);

        public void ValidateOrThrow()
        {
            _ = ActorId;
            _ = new ContentId(initialTraversalNodeId);
            if (body == null || body.bodyType != RigidbodyType2D.Dynamic ||
                atlasAnimator == null || spriteSet == null || recoveryAnchor == null ||
                compositionCamera == null || string.IsNullOrWhiteSpace(idleClipId) ||
                string.IsNullOrWhiteSpace(runClipId) ||
                string.IsNullOrWhiteSpace(interactClipId) || movementSpeed <= 0f ||
                jumpVelocity <= 0f)
            {
                throw new InvalidOperationException(
                    $"Koro crew actor '{actorId}' needs a physical authored runtime.");
            }
            spriteSet.FindClip(idleClipId);
            spriteSet.FindClip(runClipId);
            spriteSet.FindClip(interactClipId);
        }

        public CrewRecoveryDecision EvaluateRecovery(
            Vector2 desiredPosition,
            float maximumRouteDistance)
        {
            return new CrewRecovery(1.2f, maximumRouteDistance).Evaluate(
                new CrewRecoveryContext(
                    body.position,
                    recoveryAnchor.position,
                    CameraVisible,
                    RecoveryPositionVisible,
                    blockedSeconds: 0f,
                    Vector2.Distance(body.position, desiredPosition),
                    routeAvailable: true));
        }

        public void Join(Vector2 position) => Follow(position);
        public void Position(Vector2 position) => Follow(position);
        public void Investigate(Vector2 position) => Follow(position);

        public void Follow(Vector2 position)
        {
            m_Waypoints.Clear();
            m_FollowTarget = position;
            m_HasFollowTarget = true;
            Play(runClipId);
        }

        public void Traverse(IReadOnlyList<TraversalNode2D> path)
        {
            if (path == null || path.Count == 0)
            {
                throw new ArgumentException(
                    "Koro crew traversal requires an authored route.", nameof(path));
            }
            m_Waypoints.Clear();
            foreach (var node in path)
            {
                m_Waypoints.Enqueue(node);
            }
            m_HasFollowTarget = false;
            Play(runClipId);
        }

        public void Interact(Vector2 position)
        {
            StopMotion();
            Play(interactClipId);
        }

        public void React(Vector2 position) => Play(interactClipId);
        public void Speak() => Play(idleClipId);
        public void Converse() => Play(idleClipId);
        public void EnterCinematic() => StopMotion();
        public void Wait() => StopMotion();

        public void Recover(Vector2 position)
        {
            body.position = position;
            body.linearVelocity = Vector2.zero;
            StopMotion();
        }

        private void Awake()
        {
            if (!string.IsNullOrWhiteSpace(initialTraversalNodeId))
            {
                m_CurrentNode = new ContentId(initialTraversalNodeId);
            }
        }

        private void FixedUpdate()
        {
            if (m_Waypoints.Count > 0)
            {
                var node = m_Waypoints.Peek();
                DriveTo(node.Position);
                if (Vector2.Distance(body.position, node.Position) <= 0.22f)
                {
                    m_CurrentNode = node.Id;
                    m_Waypoints.Dequeue();
                }
                return;
            }
            if (m_HasFollowTarget)
            {
                DriveTo(m_FollowTarget);
                if (Vector2.Distance(body.position, m_FollowTarget) <= 0.2f)
                {
                    m_HasFollowTarget = false;
                    body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                    Play(idleClipId);
                }
            }
        }

        private void DriveTo(Vector2 target)
        {
            var delta = target - body.position;
            body.linearVelocity = new Vector2(
                Mathf.Clamp(delta.x * 3f, -movementSpeed, movementSpeed),
                body.linearVelocity.y);
            if (delta.y > 0.35f && Mathf.Abs(body.linearVelocity.y) < 0.15f)
            {
                body.linearVelocity = new Vector2(body.linearVelocity.x, jumpVelocity);
            }
        }

        private void StopMotion()
        {
            m_Waypoints.Clear();
            m_HasFollowTarget = false;
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            Play(idleClipId);
        }

        private void Play(string clipId)
        {
            if (string.Equals(m_CurrentClip, clipId, StringComparison.Ordinal))
            {
                return;
            }
            atlasAnimator.Play(spriteSet.FindClip(clipId));
            m_CurrentClip = clipId;
        }

        private bool IsVisible(Vector2 position)
        {
            var viewport = compositionCamera.WorldToViewportPoint(position);
            return viewport.z >= 0f && viewport.x >= 0f && viewport.x <= 1f &&
                viewport.y >= 0f && viewport.y <= 1f;
        }
    }
}
