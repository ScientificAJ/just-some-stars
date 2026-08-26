using System;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Core;
using UnityEngine;

namespace JustSomeStars.Runtime.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CompositionCamera2D : MonoBehaviour
    {
        [SerializeField] private Camera controlledCamera;
        [SerializeField] private Transform target;
        [SerializeField] private Bounds movementBounds;
        [SerializeField] private Vector2 deadZone = new Vector2(2f, 1f);
        [SerializeField, Min(0f)] private float lookAheadDistance = 2f;
        [SerializeField, Min(0f)] private float smoothingSeconds = 0.12f;
        [SerializeField] private Vector2 zoomRange = new Vector2(3f, 6f);
        [SerializeField] private GameCameraPolicy currentPolicy =
            GameCameraPolicy.Surface;

        private Vector2 targetVelocity;
        private Vector2 smoothVelocity;
        private float requestedZoom = 4f;
        private bool reducedMotion;

        public float EffectiveLookAhead => reducedMotion ? 0f : lookAheadDistance;
        public GameCameraPolicy CurrentPolicy => currentPolicy;
        public bool AllowsFreeOrbit => false;

        private void OnEnable()
        {
            if (controlledCamera == null)
            {
                controlledCamera = GetComponent<Camera>();
            }

            if (controlledCamera == null)
            {
                return;
            }

            zoomRange = new Vector2(
                Mathf.Min(zoomRange.x, zoomRange.y),
                Mathf.Max(zoomRange.x, zoomRange.y));
            requestedZoom = Mathf.Clamp(
                controlledCamera.orthographicSize,
                zoomRange.x,
                zoomRange.y);
            controlledCamera.orthographic = true;
            controlledCamera.transform.rotation = Quaternion.identity;
        }

        public void Configure(
            Camera camera,
            Transform followTarget,
            Bounds authoredMovementBounds,
            Vector2 authoredDeadZone,
            float authoredLookAhead,
            float authoredSmoothing,
            Vector2 authoredZoomRange)
        {
            controlledCamera = camera != null
                ? camera
                : throw new ArgumentNullException(nameof(camera));
            target = followTarget != null
                ? followTarget
                : throw new ArgumentNullException(nameof(followTarget));
            movementBounds = authoredMovementBounds;
            deadZone = new Vector2(
                Mathf.Max(0f, authoredDeadZone.x),
                Mathf.Max(0f, authoredDeadZone.y));
            lookAheadDistance = Mathf.Max(0f, authoredLookAhead);
            smoothingSeconds = Mathf.Max(0f, authoredSmoothing);
            zoomRange = new Vector2(
                Mathf.Min(authoredZoomRange.x, authoredZoomRange.y),
                Mathf.Max(authoredZoomRange.x, authoredZoomRange.y));
            requestedZoom = Mathf.Clamp(
                controlledCamera.orthographicSize,
                zoomRange.x,
                zoomRange.y);
            controlledCamera.orthographic = true;
            controlledCamera.transform.rotation = Quaternion.identity;
        }

        public void SetTargetVelocity(Vector2 velocity)
        {
            targetVelocity = velocity;
        }

        public void ApplySettings(GameSettings settings)
        {
            reducedMotion = settings != null && settings.ReducedMotion;
        }

        public void SetZoom(float orthographicSize)
        {
            requestedZoom = Mathf.Clamp(
                orthographicSize,
                zoomRange.x,
                zoomRange.y);
        }

        public void SetPolicy(GameCameraPolicy policy)
        {
            if (!Enum.IsDefined(typeof(GameCameraPolicy), policy))
            {
                throw new ArgumentOutOfRangeException(nameof(policy));
            }

            currentPolicy = policy;
        }

        public void Sample(float deltaTime)
        {
            if (controlledCamera == null || target == null)
            {
                throw new InvalidOperationException(
                    "CompositionCamera2D must be configured before sampling.");
            }

            var current = (Vector2)controlledCamera.transform.position;
            var lookDirection = new Vector2(
                Mathf.Clamp(targetVelocity.x, -1f, 1f),
                Mathf.Clamp(targetVelocity.y, -1f, 1f));
            var focus = (Vector2)target.position +
                lookDirection * EffectiveLookAhead;
            var desired = current;
            var halfDeadZone = deadZone * 0.5f;

            if (focus.x > current.x + halfDeadZone.x)
            {
                desired.x = focus.x - halfDeadZone.x;
            }
            else if (focus.x < current.x - halfDeadZone.x)
            {
                desired.x = focus.x + halfDeadZone.x;
            }

            if (focus.y > current.y + halfDeadZone.y)
            {
                desired.y = focus.y - halfDeadZone.y;
            }
            else if (focus.y < current.y - halfDeadZone.y)
            {
                desired.y = focus.y + halfDeadZone.y;
            }

            desired.x = Mathf.Clamp(
                desired.x,
                movementBounds.min.x,
                movementBounds.max.x);
            desired.y = Mathf.Clamp(
                desired.y,
                movementBounds.min.y,
                movementBounds.max.y);
            var position = smoothingSeconds <= 0f
                ? desired
                : Vector2.SmoothDamp(
                    current,
                    desired,
                    ref smoothVelocity,
                    smoothingSeconds,
                    Mathf.Infinity,
                    Mathf.Max(0f, deltaTime));
            controlledCamera.transform.position = new Vector3(
                position.x,
                position.y,
                controlledCamera.transform.position.z);
            controlledCamera.transform.rotation = Quaternion.identity;
            controlledCamera.orthographicSize = requestedZoom;
        }

        private void LateUpdate()
        {
            if (controlledCamera != null && target != null)
            {
                Sample(Time.unscaledDeltaTime);
            }
        }
    }
}
