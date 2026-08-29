using System;
using System.Collections.Generic;
using JustSomeStars.Runtime.Cosmetics;
using UnityEngine;

namespace JustSomeStars.Runtime.Player
{
    [Serializable]
    public sealed class BodySpriteCalibrationProfile
    {
        [SerializeField] private CaptainBodyFamily family;
        [SerializeField] private float heightMeters;
        [SerializeField] private Vector3 visualScale = Vector3.one;
        [SerializeField] private Vector2 visualPivot;
        [SerializeField] private Vector2 colliderSize = new Vector2(0.8f, 1.5f);
        [SerializeField] private Vector2 colliderOffset;
        [SerializeField] private Vector2 shadowPosition = new Vector2(0f, -0.75f);
        [SerializeField] private Vector3 shadowScale = Vector3.one;
        [SerializeField] private Vector2 cameraAnchor = new Vector2(0f, 0.4f);

        public CaptainBodyFamily Family { get => family; set => family = value; }
        public float HeightMeters { get => heightMeters; set => heightMeters = Mathf.Max(0f, value); }
        public Vector3 VisualScale { get => visualScale; set => visualScale = value; }
        public Vector2 VisualPivot { get => visualPivot; set => visualPivot = value; }
        public Vector2 ColliderSize { get => colliderSize; set => colliderSize = value; }
        public Vector2 ColliderOffset { get => colliderOffset; set => colliderOffset = value; }
        public Vector2 ShadowPosition { get => shadowPosition; set => shadowPosition = value; }
        public Vector3 ShadowScale { get => shadowScale; set => shadowScale = value; }
        public Vector2 CameraAnchor { get => cameraAnchor; set => cameraAnchor = value; }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CapsuleCollider2D))]
    public sealed class BodySpriteCalibration : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private CapsuleCollider2D bodyCollider;
        [SerializeField] private Transform contactShadow;
        [SerializeField] private Transform cameraAnchor;
        [SerializeField] private CaptainBodyFamily activeFamily =
            CaptainBodyFamily.Average;
        [SerializeField] private BodySpriteCalibrationProfile[] profiles =
            Array.Empty<BodySpriteCalibrationProfile>();

        private readonly Dictionary<CaptainBodyFamily, BodySpriteCalibrationProfile>
            profileLookup =
                new Dictionary<CaptainBodyFamily, BodySpriteCalibrationProfile>();

        public CaptainBodyFamily ActiveFamily => activeFamily;
        public float CalibratedHeight => Resolve(activeFamily).HeightMeters;
        public float BootBaselineWorldY => bodyCollider == null
            ? transform.position.y
            : transform.TransformPoint(
                bodyCollider.offset - Vector2.up * bodyCollider.size.y * 0.5f).y;
        public bool ChangesGameplayCapability => false;
        public bool ChangesAnimationCadence => false;
        public IReadOnlyList<BodySpriteCalibrationProfile> Profiles => profiles;
        public Transform CameraAnchor => cameraAnchor;

        public void Configure(
            Transform configuredVisualRoot,
            CapsuleCollider2D configuredCollider,
            Transform configuredShadow,
            Transform configuredCameraAnchor)
        {
            visualRoot = configuredVisualRoot != null
                ? configuredVisualRoot
                : throw new ArgumentNullException(nameof(configuredVisualRoot));
            bodyCollider = configuredCollider != null
                ? configuredCollider
                : throw new ArgumentNullException(nameof(configuredCollider));
            contactShadow = configuredShadow != null
                ? configuredShadow
                : throw new ArgumentNullException(nameof(configuredShadow));
            cameraAnchor = configuredCameraAnchor != null
                ? configuredCameraAnchor
                : throw new ArgumentNullException(nameof(configuredCameraAnchor));
            EnsureDefaultProfiles();
            RebuildLookup();
            ApplyFamily(activeFamily);
        }

        public void SetProfiles(BodySpriteCalibrationProfile[] authoredProfiles)
        {
            profiles = authoredProfiles != null
                ? (BodySpriteCalibrationProfile[])authoredProfiles.Clone()
                : throw new ArgumentNullException(nameof(authoredProfiles));
            RebuildLookup();
            ApplyFamily(activeFamily);
        }

        public void ApplyFamily(CaptainBodyFamily family)
        {
            RequireBindings();
            RebuildLookup();
            var profile = Resolve(family);
            Validate(profile);

            transform.localScale = Vector3.one;
            visualRoot.localScale = profile.VisualScale;
            visualRoot.localPosition = new Vector3(
                profile.VisualPivot.x,
                profile.VisualPivot.y,
                visualRoot.localPosition.z);
            bodyCollider.size = profile.ColliderSize;
            bodyCollider.offset = profile.ColliderOffset;
            contactShadow.localPosition = new Vector3(
                profile.ShadowPosition.x,
                profile.ShadowPosition.y,
                contactShadow.localPosition.z);
            contactShadow.localScale = profile.ShadowScale;
            cameraAnchor.localPosition = new Vector3(
                profile.CameraAnchor.x,
                profile.CameraAnchor.y,
                cameraAnchor.localPosition.z);
            activeFamily = family;
        }

        private void Awake()
        {
            if (bodyCollider == null)
            {
                bodyCollider = GetComponent<CapsuleCollider2D>();
            }
            if (visualRoot != null && bodyCollider != null &&
                contactShadow != null && cameraAnchor != null)
            {
                EnsureDefaultProfiles();
                RebuildLookup();
                ApplyFamily(activeFamily);
            }
        }

        private void EnsureDefaultProfiles()
        {
            if (profiles != null && profiles.Length > 0)
            {
                return;
            }

            profiles = new[]
            {
                CreateProfile(
                    CaptainBodyFamily.Compact,
                    1.46f,
                    new Vector3(1.90f, 1.90f, 1f),
                    new Vector2(0f, -1.850815f),
                    new Vector2(0.751f, 1.6655f),
                    new Vector2(0f, -0.15005f),
                    new Vector3(0.47f, 0.235f, 1f),
                    new Vector2(0f, 0.36f)),
                CreateProfile(
                    CaptainBodyFamily.Average,
                    1.56f,
                    new Vector3(1.90f, 1.90f, 1f),
                    new Vector2(0f, -1.850815f),
                    new Vector2(0.803704f, 1.77963f),
                    new Vector2(0f, -0.093f),
                    new Vector3(0.496f, 0.248f, 1f),
                    new Vector2(0f, 0.40f)),
                CreateProfile(
                    CaptainBodyFamily.TallBroad,
                    1.66f,
                    new Vector3(1.90f, 1.90f, 1f),
                    new Vector2(0f, -1.850815f),
                    new Vector2(0.854f, 1.8937f),
                    new Vector2(0f, -0.035965f),
                    new Vector3(0.53f, 0.265f, 1f),
                    new Vector2(0f, 0.44f)),
            };
        }

        private static BodySpriteCalibrationProfile CreateProfile(
            CaptainBodyFamily family,
            float height,
            Vector3 visualScale,
            Vector2 visualPivot,
            Vector2 colliderSize,
            Vector2 colliderOffset,
            Vector3 shadowScale,
            Vector2 cameraAnchor)
        {
            return new BodySpriteCalibrationProfile
            {
                Family = family,
                HeightMeters = height,
                VisualScale = visualScale,
                VisualPivot = visualPivot,
                ColliderSize = colliderSize,
                ColliderOffset = colliderOffset,
                ShadowPosition = new Vector2(0f, -0.982815f),
                ShadowScale = shadowScale,
                CameraAnchor = cameraAnchor,
            };
        }

        private void RebuildLookup()
        {
            EnsureDefaultProfiles();
            profileLookup.Clear();
            foreach (var profile in profiles)
            {
                if (profile == null || profileLookup.ContainsKey(profile.Family))
                {
                    throw new InvalidOperationException(
                        "Body calibrations require one unique profile per family.");
                }
                profileLookup.Add(profile.Family, profile);
            }
            if (profileLookup.Count != Enum.GetValues(typeof(CaptainBodyFamily)).Length)
            {
                throw new InvalidOperationException(
                    "Body calibrations require Compact, Average and TallBroad.");
            }
        }

        private BodySpriteCalibrationProfile Resolve(CaptainBodyFamily family)
        {
            EnsureDefaultProfiles();
            if (profileLookup.Count == 0)
            {
                RebuildLookup();
            }
            if (!profileLookup.TryGetValue(family, out var profile))
            {
                throw new InvalidOperationException(
                    "No body calibration exists for " + family + ".");
            }
            return profile;
        }

        private static void Validate(BodySpriteCalibrationProfile profile)
        {
            if (profile.HeightMeters <= 0f ||
                profile.VisualScale.x <= 0f || profile.VisualScale.y <= 0f ||
                profile.ColliderSize.x <= 0f || profile.ColliderSize.y <= 0f ||
                profile.ShadowScale.x <= 0f || profile.ShadowScale.y <= 0f)
            {
                throw new InvalidOperationException(
                    "Body calibration dimensions must be positive.");
            }
        }

        private void RequireBindings()
        {
            if (visualRoot == null || bodyCollider == null ||
                contactShadow == null || cameraAnchor == null)
            {
                throw new InvalidOperationException(
                    "BodySpriteCalibration requires visual, collider, shadow " +
                    "and camera-anchor bindings.");
            }
        }
    }
}
