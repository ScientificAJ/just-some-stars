using System;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Core;
using UnityEngine;

namespace JustSomeStars.Runtime.Player
{
    [Serializable]
    public sealed class CompositionCameraProfile
    {
        [SerializeField] private GameCameraPolicy policy = GameCameraPolicy.Surface;
        [SerializeField] private Vector2 deadZone = new Vector2(2f, 1f);
        [SerializeField, Min(0f)] private float lookAheadDistance = 1.2f;
        [SerializeField, Min(0f)] private float smoothingSeconds = 0.12f;
        [SerializeField] private Vector2 zoomRange = new Vector2(3f, 6f);
        [SerializeField, Min(0.01f)] private float defaultZoom = 4f;
        [SerializeField] private Bounds centerRails;
        [SerializeField] private Bounds contentSafeBounds;
        [SerializeField] private Transform primaryTarget;
        [SerializeField] private Transform[] compositionTargets =
            Array.Empty<Transform>();

        public GameCameraPolicy Policy { get => policy; set => policy = value; }
        public Vector2 DeadZone { get => deadZone; set => deadZone = value; }
        public float LookAheadDistance { get => lookAheadDistance; set => lookAheadDistance = Mathf.Max(0f, value); }
        public float SmoothingSeconds { get => smoothingSeconds; set => smoothingSeconds = Mathf.Max(0f, value); }
        public Vector2 ZoomRange { get => zoomRange; set => zoomRange = value; }
        public float DefaultZoom { get => defaultZoom; set => defaultZoom = Mathf.Max(0.01f, value); }
        public Bounds CenterRails { get => centerRails; set => centerRails = value; }
        public Bounds ContentSafeBounds { get => contentSafeBounds; set => contentSafeBounds = value; }
        public Transform PrimaryTarget { get => primaryTarget; set => primaryTarget = value; }
        public Transform[] CompositionTargets
        {
            get => compositionTargets;
            set => compositionTargets = value ?? Array.Empty<Transform>();
        }

        public void ValidateOrThrow()
        {
            if (!Enum.IsDefined(typeof(GameCameraPolicy), policy) ||
                primaryTarget == null || deadZone.x < 0f || deadZone.y < 0f ||
                smoothingSeconds < 0f || lookAheadDistance < 0f ||
                zoomRange.x <= 0f || zoomRange.y <= 0f ||
                defaultZoom < Mathf.Min(zoomRange.x, zoomRange.y) ||
                defaultZoom > Mathf.Max(zoomRange.x, zoomRange.y) ||
                centerRails.size.x <= 0f || centerRails.size.y <= 0f ||
                contentSafeBounds.size.x <= 0f || contentSafeBounds.size.y <= 0f)
            {
                throw new InvalidOperationException(
                    "Composition camera profiles require a target, positive " +
                    "rails/content bounds and a bounded default zoom.");
            }
            if (Array.Exists(compositionTargets, item => item == null))
            {
                throw new InvalidOperationException(
                    "Optional composition targets cannot contain null entries.");
            }
        }
    }

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
        [SerializeField] private CompositionCameraProfile[] profiles =
            Array.Empty<CompositionCameraProfile>();

        private Vector2 targetVelocity;
        private Vector2 smoothVelocity;
        private float requestedZoom = 4f;
        private bool reducedMotion;
        private CompositionCameraProfile activeProfile;

        public float EffectiveLookAhead => reducedMotion
            ? 0f
            : activeProfile != null
                ? activeProfile.LookAheadDistance
                : lookAheadDistance;
        public GameCameraPolicy CurrentPolicy => currentPolicy;
        public bool AllowsFreeOrbit => false;
        public bool VelocityZoomEnabled => !reducedMotion;
        public CompositionCameraProfile[] Profiles =>
            (CompositionCameraProfile[])profiles.Clone();
        public CompositionCameraProfile ActiveProfile => activeProfile;
        public Camera ControlledCamera => controlledCamera != null
            ? controlledCamera
            : GetComponent<Camera>();

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
            if (profiles != null && profiles.Length > 0)
            {
                ValidateProfiles();
                ApplyProfile(currentPolicy, false);
            }
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
            profiles = Array.Empty<CompositionCameraProfile>();
            activeProfile = null;
        }

        public void ConfigureProfiles(
            Camera camera,
            CompositionCameraProfile[] authoredProfiles,
            GameCameraPolicy initialPolicy)
        {
            controlledCamera = camera != null
                ? camera
                : throw new ArgumentNullException(nameof(camera));
            profiles = authoredProfiles != null
                ? (CompositionCameraProfile[])authoredProfiles.Clone()
                : throw new ArgumentNullException(nameof(authoredProfiles));
            ValidateProfiles();
            controlledCamera.orthographic = true;
            controlledCamera.transform.rotation = Quaternion.identity;
            smoothVelocity = Vector2.zero;
            ApplyProfile(initialPolicy, true);
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
            if (profiles != null && profiles.Length > 0)
            {
                ApplyProfile(policy, true);
            }
        }

        public void Sample(float deltaTime)
        {
            if (controlledCamera == null || target == null)
            {
                throw new InvalidOperationException(
                    "CompositionCamera2D must be configured before sampling.");
            }

            var current = (Vector2)controlledCamera.transform.position;
            var activeTarget = activeProfile != null
                ? activeProfile.PrimaryTarget
                : target;
            if (activeTarget == null)
            {
                throw new InvalidOperationException(
                    "The active composition profile has no primary target.");
            }
            var lookDirection = new Vector2(
                Mathf.Clamp(targetVelocity.x, -1f, 1f),
                Mathf.Clamp(targetVelocity.y, -1f, 1f));
            var focus = ResolveCompositionFocus(activeTarget) +
                lookDirection * EffectiveLookAhead;
            var desired = current;
            var activeDeadZone = activeProfile != null
                ? activeProfile.DeadZone
                : deadZone;
            var halfDeadZone = activeDeadZone * 0.5f;

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

            desired = ClampToAuthoredBounds(desired);
            var activeSmoothing = activeProfile != null
                ? activeProfile.SmoothingSeconds
                : smoothingSeconds;
            var position = activeSmoothing <= 0f
                ? desired
                : Vector2.SmoothDamp(
                    current,
                    desired,
                    ref smoothVelocity,
                    activeSmoothing,
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

        private Vector2 ResolveCompositionFocus(Transform primary)
        {
            var sum = (Vector2)primary.position;
            var count = 1;
            if (activeProfile != null)
            {
                foreach (var optionalTarget in activeProfile.CompositionTargets)
                {
                    if (optionalTarget == null)
                    {
                        continue;
                    }
                    sum += (Vector2)optionalTarget.position;
                    count++;
                }
            }
            return sum / count;
        }

        private Vector2 ClampToAuthoredBounds(Vector2 desired)
        {
            var centerBounds = activeProfile != null
                ? activeProfile.CenterRails
                : movementBounds;
            var minX = centerBounds.min.x;
            var maxX = centerBounds.max.x;
            var minY = centerBounds.min.y;
            var maxY = centerBounds.max.y;

            if (activeProfile != null)
            {
                var safe = activeProfile.ContentSafeBounds;
                var halfHeight = requestedZoom;
                var halfWidth = halfHeight * Mathf.Max(0.01f, controlledCamera.aspect);
                minX = Mathf.Max(minX, safe.min.x + halfWidth);
                maxX = Mathf.Min(maxX, safe.max.x - halfWidth);
                minY = Mathf.Max(minY, safe.min.y + halfHeight);
                maxY = Mathf.Min(maxY, safe.max.y - halfHeight);
                if (minX > maxX)
                {
                    minX = maxX = safe.center.x;
                }
                if (minY > maxY)
                {
                    minY = maxY = safe.center.y;
                }
            }

            return new Vector2(
                Mathf.Clamp(desired.x, minX, maxX),
                Mathf.Clamp(desired.y, minY, maxY));
        }

        private void ValidateProfiles()
        {
            var policies = (GameCameraPolicy[])Enum.GetValues(
                typeof(GameCameraPolicy));
            if (profiles == null || profiles.Length != policies.Length ||
                Array.Exists(profiles, item => item == null))
            {
                throw new InvalidOperationException(
                    "CompositionCamera2D requires one profile per camera policy.");
            }
            foreach (var profile in profiles)
            {
                profile.ValidateOrThrow();
            }
            foreach (var policy in policies)
            {
                if (Array.FindAll(profiles, item => item.Policy == policy).Length != 1)
                {
                    throw new InvalidOperationException(
                        "CompositionCamera2D requires exactly one " + policy +
                        " profile.");
                }
            }
        }

        private void ApplyProfile(GameCameraPolicy policy, bool resetSmoothing)
        {
            var profile = Array.Find(profiles, item => item.Policy == policy);
            if (profile == null)
            {
                throw new InvalidOperationException(
                    "No composition profile exists for " + policy + ".");
            }
            activeProfile = profile;
            currentPolicy = policy;
            target = profile.PrimaryTarget;
            var orderedZoom = new Vector2(
                Mathf.Min(profile.ZoomRange.x, profile.ZoomRange.y),
                Mathf.Max(profile.ZoomRange.x, profile.ZoomRange.y));
            zoomRange = orderedZoom;
            requestedZoom = Mathf.Clamp(
                profile.DefaultZoom,
                orderedZoom.x,
                orderedZoom.y);
            if (resetSmoothing)
            {
                smoothVelocity = Vector2.zero;
            }
        }
    }
}
