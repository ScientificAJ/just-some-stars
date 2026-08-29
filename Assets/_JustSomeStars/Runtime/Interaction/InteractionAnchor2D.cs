using System;
using JustSomeStars.Runtime.Core;
using UnityEngine;

namespace JustSomeStars.Runtime.Interaction
{
    [DisallowMultipleComponent]
    public sealed class InteractionAnchor2D : MonoBehaviour
    {
        [SerializeField] private string stableId;
        [SerializeField] private InteractionActorKind actorKind;
        [SerializeField] private InteractionFacing requiredFacing =
            InteractionFacing.Any;
        [SerializeField] private InteractionDepthBand depthBand =
            InteractionDepthBand.Gameplay;
        [SerializeField] private bool exclusive = true;
        [SerializeField] private bool requireApproachFacing = true;
        [SerializeField] private Vector2 recoveryOffset = new Vector2(0f, 0.25f);

        public ContentId StableId => new ContentId(stableId);
        public InteractionActorKind ActorKind => actorKind;
        public InteractionFacing RequiredFacing => requiredFacing;
        public InteractionDepthBand DepthBand => depthBand;
        public bool IsExclusive => exclusive;
        public bool RequireApproachFacing => requireApproachFacing;
        public int PhysicsLayer => gameObject.layer;
        public Vector2 Position => transform.position;
        public Vector2 RecoveryPosition =>
            transform.TransformPoint(recoveryOffset);

        public void Configure(
            string id,
            InteractionActorKind requiredActorKind,
            InteractionFacing facing,
            InteractionDepthBand declaredDepthBand,
            bool exclusive,
            bool requireApproachFacing,
            Vector2 recoveryOffset)
        {
            stableId = id;
            actorKind = requiredActorKind;
            requiredFacing = facing;
            depthBand = declaredDepthBand;
            this.exclusive = exclusive;
            this.requireApproachFacing = requireApproachFacing;
            this.recoveryOffset = recoveryOffset;
            ValidateOrThrow();
        }

        public void ValidateOrThrow()
        {
            _ = StableId;
            if (!Enum.IsDefined(typeof(InteractionActorKind), actorKind))
            {
                throw new InvalidOperationException(
                    $"Anchor '{stableId}' has an invalid actor kind.");
            }

            if (!Enum.IsDefined(typeof(InteractionFacing), requiredFacing))
            {
                throw new InvalidOperationException(
                    $"Anchor '{stableId}' has an invalid facing.");
            }

            if (!Enum.IsDefined(typeof(InteractionDepthBand), depthBand))
            {
                throw new InvalidOperationException(
                    $"Anchor '{stableId}' has an invalid depth band.");
            }

            if (!IsFinite(recoveryOffset.x) || !IsFinite(recoveryOffset.y))
            {
                throw new InvalidOperationException(
                    $"Anchor '{stableId}' has an invalid recovery offset.");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
