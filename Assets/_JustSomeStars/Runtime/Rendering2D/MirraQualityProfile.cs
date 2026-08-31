using System;
using JustSomeStars.Runtime.Accessibility;
using UnityEngine;

namespace JustSomeStars.Runtime.Rendering2D
{
    [CreateAssetMenu(
        fileName = "MirraQualityProfile",
        menuName = "Just Some Stars/Rendering/Mirra Quality Profile")]
    public sealed class MirraQualityProfile : ScriptableObject
    {
        [SerializeField] private string stableId;
        [SerializeField] private PresentationQuality quality;
        [SerializeField] private int targetFrameRate = 30;
        [SerializeField, Range(0.75f, 1f)] private float renderScale = 1f;
        [SerializeField] private bool usesDynamicResolution;
        [SerializeField, Range(1, 4)] private int activeLightCount = 3;
        [SerializeField, Range(0.5f, 1.2f)] private float lightIntensityMultiplier = 1f;
        [SerializeField, Range(0f, 1.25f)] private float particleMultiplier = 1f;
        [SerializeField, Range(0f, 1f)] private float volumeWeight = 1f;
        [SerializeField, Range(0.5f, 1f)] private float parallaxMultiplier = 1f;

        public string StableId => stableId;
        public PresentationQuality Quality => quality;
        public int TargetFrameRate => targetFrameRate;
        public float RenderScale => renderScale;
        public bool UsesDynamicResolution => usesDynamicResolution;
        public int ActiveLightCount => activeLightCount;
        public float LightIntensityMultiplier => lightIntensityMultiplier;
        public float ParticleMultiplier => particleMultiplier;
        public float VolumeWeight => volumeWeight;
        public float ParallaxMultiplier => parallaxMultiplier;

        public void ValidateOrThrow()
        {
            if (string.IsNullOrWhiteSpace(stableId) ||
                !Enum.IsDefined(typeof(PresentationQuality), quality) ||
                (targetFrameRate != 30 && targetFrameRate != 60) ||
                renderScale < 0.75f || renderScale > 1f ||
                activeLightCount < 1 || activeLightCount > 4 ||
                lightIntensityMultiplier < 0.5f ||
                lightIntensityMultiplier > 1.2f ||
                particleMultiplier < 0f || particleMultiplier > 1.25f ||
                volumeWeight < 0f || volumeWeight > 1f ||
                parallaxMultiplier < 0.5f || parallaxMultiplier > 1f ||
                (quality == PresentationQuality.HighFrameRate) !=
                    usesDynamicResolution ||
                (quality == PresentationQuality.HighFrameRate) !=
                    (targetFrameRate == 60))
            {
                throw new InvalidOperationException(
                    $"Mirra quality profile '{name}' is outside the mobile contract.");
            }
        }
    }
}
