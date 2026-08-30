using System;
using UnityEngine;

namespace JustSomeStars.Runtime.Flight
{
    [DisallowMultipleComponent]
    public sealed class PlayerShipPresentation2D : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer engine;
        [SerializeField] private Transform landingGearPivot;
        [SerializeField] private SpriteRenderer landingGear;
        [SerializeField] private Transform doorPivot;
        [SerializeField] private SpriteRenderer door;
        [SerializeField] private Sprite[] engineFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] landingFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] doorFrames = Array.Empty<Sprite>();
        [SerializeField] private SpriteRenderer cockpitSeat;
        [SerializeField] private SpriteRenderer damageOverlay;
        [SerializeField] private SpriteRenderer cosmeticAttachment;

        private Vector3 landingDeployedPosition;
        private Color engineBaseColor = Color.white;
        private bool reducedMotion;
        private float engineDemand;
        private float engineAnimationStartedAt;

        public bool HasAllSemanticLayers => engine != null &&
            landingGearPivot != null && landingGear != null &&
            doorPivot != null && door != null &&
            engineFrames != null && engineFrames.Length == 4 &&
            landingFrames != null && landingFrames.Length == 3 &&
            doorFrames != null && doorFrames.Length == 3 &&
            cockpitSeat != null && damageOverlay != null &&
            cosmeticAttachment != null;

        private void Awake()
        {
            if (landingGearPivot != null)
            {
                landingDeployedPosition = landingGearPivot.localPosition;
            }

            if (engine != null)
            {
                engineBaseColor = engine.color;
                ApplyFrame(engine, engineFrames, 0);
            }

            ApplyFrame(landingGear, landingFrames, 0);
            ApplyFrame(door, doorFrames, 0);
        }

        public void SetMotion(float demand, bool reduceMotion)
        {
            if (engineDemand <= 0.01f && demand > 0.01f)
            {
                engineAnimationStartedAt = Time.unscaledTime;
            }

            engineDemand = Mathf.Clamp01(demand);
            reducedMotion = reduceMotion;
            ApplyEngine(Time.unscaledTime);
        }

        public void SetLandingProgress(float progress)
        {
            if (landingGearPivot == null)
            {
                throw new InvalidOperationException("Landing gear pivot is missing.");
            }

            landingGearPivot.localPosition = landingDeployedPosition;
            ApplyFrame(
                landingGear,
                landingFrames,
                Mathf.RoundToInt(Mathf.Clamp01(progress) *
                    (landingFrames.Length - 1)));
        }

        public void SetDoorOpen(bool open)
        {
            if (doorPivot == null)
            {
                throw new InvalidOperationException("Door pivot is missing.");
            }

            doorPivot.localRotation = Quaternion.identity;
            ApplyFrame(door, doorFrames, open ? doorFrames.Length - 1 : 0);
        }

        public void SetOccupied(bool occupied)
        {
            if (cockpitSeat != null)
            {
                cockpitSeat.enabled = occupied;
            }
        }

        public void SetDamaged(bool damaged)
        {
            if (damageOverlay != null)
            {
                damageOverlay.enabled = damaged;
            }
        }

        public void SetCosmeticVisible(bool visible)
        {
            if (cosmeticAttachment != null)
            {
                cosmeticAttachment.enabled = visible;
            }
        }

        private void Update()
        {
            ApplyEngine(Time.unscaledTime);
        }

        private void ApplyEngine(float time)
        {
            if (engine == null)
            {
                return;
            }

            var frame = engineDemand <= 0.01f
                ? 0
                : reducedMotion
                    ? engineFrames.Length - 1
                    : Mathf.FloorToInt(
                        (time - engineAnimationStartedAt) *
                        Mathf.Lerp(7f, 12f, engineDemand));
            ApplyFrame(engine, engineFrames, frame);

            var pulse = reducedMotion
                ? 1f
                : 0.88f + Mathf.Sin(time * 11f) * 0.12f;
            var color = engineBaseColor;
            color.a = Mathf.Lerp(0.58f, 1f, engineDemand) * pulse;
            engine.color = color;
        }

        private static void ApplyFrame(
            SpriteRenderer renderer,
            Sprite[] frames,
            int frame)
        {
            if (renderer == null || frames == null || frames.Length == 0)
            {
                return;
            }

            renderer.sprite = frames[Mathf.Abs(frame) % frames.Length];
        }
    }
}
