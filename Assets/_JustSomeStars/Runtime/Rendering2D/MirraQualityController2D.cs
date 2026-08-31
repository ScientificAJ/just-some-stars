using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Player;
using UnityEngine;

namespace JustSomeStars.Runtime.Rendering2D
{
    [DisallowMultipleComponent]
    public sealed class MirraQualityController2D : MonoBehaviour,
        ISurfaceGameplayExtension
    {
        [SerializeField] private MirraQualityProfile[] profiles =
            Array.Empty<MirraQualityProfile>();
        [SerializeField] private Camera qualityCamera;
        [SerializeField] private GameObject[] qualityLights =
            Array.Empty<GameObject>();
        [SerializeField] private ParticleSystem[] qualityParticles =
            Array.Empty<ParticleSystem>();
        [SerializeField] private MonoBehaviour postProcessingVolume;
        [SerializeField] private ParallaxLayer2D[] parallaxLayers =
            Array.Empty<ParallaxLayer2D>();

        private readonly Dictionary<GameObject, bool> m_LightStates = new();
        private readonly Dictionary<GameObject, float> m_LightIntensities = new();
        private readonly Dictionary<ParticleSystem, float> m_EmissionRates = new();
        private readonly Dictionary<ParticleSystem, int> m_MaxParticles = new();
        private readonly Dictionary<ParallaxLayer2D, float> m_ParallaxFactors = new();

        private SettingsService m_Settings;
        private int m_OriginalTargetFrameRate;
        private float m_OriginalWidthScale = 1f;
        private float m_OriginalHeightScale = 1f;
        private bool m_OriginalDynamicResolution;
        private float m_OriginalVolumeWeight;
        private bool m_HasSnapshot;

        public MirraQualityProfile[] Profiles => profiles.ToArray();
        public string ActiveProfileId { get; private set; } = string.Empty;
        public int ActiveTargetFrameRate { get; private set; }
        public float ActiveRenderScale { get; private set; } = 1f;
        public int ActiveLightCount { get; private set; }
        public float ActiveParticleMultiplier { get; private set; }
        public float ActiveVolumeWeight { get; private set; }
        public bool ActiveUsesScalableBufferPath { get; private set; }
        public bool IsBound => m_Settings != null;

        public void Configure(SurfaceGameplayDependencies dependencies)
        {
            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }

            if (m_Settings != null)
            {
                if (ReferenceEquals(m_Settings, dependencies.Settings))
                {
                    return;
                }

                throw new InvalidOperationException(
                    "Mirra quality is already bound to another composition.");
            }

            ValidateOrThrow();
            if (!dependencies.Settings.IsInitialized)
            {
                throw new InvalidOperationException(
                    "Mirra quality requires initialized device-local settings.");
            }

            try
            {
                CaptureGlobalState();
                m_Settings = dependencies.Settings;
                m_Settings.SettingsChanged += OnSettingsChanged;
                ApplyQuality(m_Settings.Current.PresentationQuality);
            }
            catch
            {
                if (m_Settings != null)
                {
                    m_Settings.SettingsChanged -= OnSettingsChanged;
                    m_Settings = null;
                }

                RestoreGlobalState();
                throw;
            }
        }

        public void Release(SurfaceGameplayDependencies dependencies)
        {
            if (m_Settings == null)
            {
                return;
            }

            if (dependencies == null ||
                !ReferenceEquals(m_Settings, dependencies.Settings))
            {
                throw new InvalidOperationException(
                    "Mirra quality can only release its owning composition.");
            }

            m_Settings.SettingsChanged -= OnSettingsChanged;
            m_Settings = null;
            RestoreGlobalState();
        }

        public void ApplyQuality(PresentationQuality quality)
        {
            ValidateOrThrow();
            CaptureGlobalState();
            var profile = profiles.SingleOrDefault(item => item.Quality == quality) ??
                profiles.Single(item =>
                    item.Quality == PresentationQuality.Balanced);
            profile.ValidateOrThrow();

            Application.targetFrameRate = profile.TargetFrameRate;
            ScalableBufferManager.ResizeBuffers(
                profile.RenderScale,
                profile.RenderScale);
            var usesScalableBufferPath =
                profile.UsesDynamicResolution || profile.RenderScale < 1f;
            qualityCamera.allowDynamicResolution = usesScalableBufferPath;

            for (var index = 0; index < qualityLights.Length; index++)
            {
                var light = qualityLights[index];
                light.SetActive(index < profile.ActiveLightCount);
                if (index < profile.ActiveLightCount &&
                    m_LightIntensities.TryGetValue(light, out var intensity))
                {
                    SetFloatProperty(
                        light.GetComponents<MonoBehaviour>().First(component =>
                            component != null && component.GetType().FullName ==
                                "UnityEngine.Rendering.Universal.Light2D"),
                        "intensity",
                        intensity * profile.LightIntensityMultiplier);
                }
            }

            foreach (var particle in qualityParticles)
            {
                var emission = particle.emission;
                var rate = emission.rateOverTime;
                rate.curveMultiplier = m_EmissionRates[particle] *
                    profile.ParticleMultiplier;
                emission.rateOverTime = rate;
                var main = particle.main;
                main.maxParticles = Mathf.Max(1, Mathf.RoundToInt(
                    m_MaxParticles[particle] * profile.ParticleMultiplier));
            }

            SetFloatProperty(
                postProcessingVolume,
                "weight",
                profile.VolumeWeight);
            foreach (var layer in parallaxLayers)
            {
                layer.Configure(
                    m_ParallaxFactors[layer] * profile.ParallaxMultiplier,
                    layer.AxisScale);
            }

            ActiveProfileId = profile.StableId;
            ActiveTargetFrameRate = profile.TargetFrameRate;
            ActiveRenderScale = profile.RenderScale;
            ActiveLightCount = profile.ActiveLightCount;
            ActiveParticleMultiplier = profile.ParticleMultiplier;
            ActiveVolumeWeight = profile.VolumeWeight;
            ActiveUsesScalableBufferPath = usesScalableBufferPath;
            Debug.Log(
                $"[JSS Quality] profile={profile.StableId} " +
                $"renderScale={profile.RenderScale:F3} " +
                $"scalableBufferPath={usesScalableBufferPath} " +
                $"cameraAllowDynamicResolution={qualityCamera.allowDynamicResolution} " +
                $"systemSupportsDynamicResolution={SystemInfo.supportsDynamicResolution}");
        }

        public void RestoreGlobalState()
        {
            if (!m_HasSnapshot)
            {
                ActiveProfileId = string.Empty;
                return;
            }

            Application.targetFrameRate = m_OriginalTargetFrameRate;
            ScalableBufferManager.ResizeBuffers(
                m_OriginalWidthScale,
                m_OriginalHeightScale);
            if (qualityCamera != null)
            {
                qualityCamera.allowDynamicResolution = m_OriginalDynamicResolution;
            }

            foreach (var pair in m_LightStates)
            {
                pair.Key.SetActive(pair.Value);
                if (m_LightIntensities.TryGetValue(pair.Key, out var intensity))
                {
                    var light = pair.Key.GetComponents<MonoBehaviour>()
                        .FirstOrDefault(component => component != null &&
                            component.GetType().FullName ==
                                "UnityEngine.Rendering.Universal.Light2D");
                    if (light != null)
                    {
                        SetFloatProperty(light, "intensity", intensity);
                    }
                }
            }

            foreach (var pair in m_EmissionRates)
            {
                var emission = pair.Key.emission;
                var rate = emission.rateOverTime;
                rate.curveMultiplier = pair.Value;
                emission.rateOverTime = rate;
                var main = pair.Key.main;
                main.maxParticles = m_MaxParticles[pair.Key];
            }

            SetFloatProperty(postProcessingVolume, "weight", m_OriginalVolumeWeight);
            foreach (var pair in m_ParallaxFactors)
            {
                pair.Key.Configure(pair.Value, pair.Key.AxisScale);
            }

            ActiveProfileId = string.Empty;
            ActiveTargetFrameRate = 0;
            ActiveRenderScale = 1f;
            ActiveLightCount = 0;
            ActiveParticleMultiplier = 0f;
            ActiveVolumeWeight = 0f;
            ActiveUsesScalableBufferPath = false;
            m_HasSnapshot = false;
            m_LightStates.Clear();
            m_LightIntensities.Clear();
            m_EmissionRates.Clear();
            m_MaxParticles.Clear();
            m_ParallaxFactors.Clear();
        }

        private void CaptureGlobalState()
        {
            if (m_HasSnapshot)
            {
                return;
            }

            m_OriginalTargetFrameRate = Application.targetFrameRate;
            m_OriginalWidthScale = ScalableBufferManager.widthScaleFactor;
            m_OriginalHeightScale = ScalableBufferManager.heightScaleFactor;
            m_OriginalDynamicResolution = qualityCamera.allowDynamicResolution;
            m_OriginalVolumeWeight = GetFloatProperty(postProcessingVolume, "weight");
            foreach (var light in qualityLights)
            {
                m_LightStates.Add(light, light.activeSelf);
                var component = light.GetComponents<MonoBehaviour>()
                    .First(item => item != null && item.GetType().FullName ==
                        "UnityEngine.Rendering.Universal.Light2D");
                m_LightIntensities.Add(
                    light,
                    GetFloatProperty(component, "intensity"));
            }

            foreach (var particle in qualityParticles)
            {
                m_EmissionRates.Add(
                    particle,
                    particle.emission.rateOverTime.curveMultiplier);
                m_MaxParticles.Add(particle, particle.main.maxParticles);
            }

            foreach (var layer in parallaxLayers)
            {
                m_ParallaxFactors.Add(layer, layer.Factor);
            }

            m_HasSnapshot = true;
        }

        private void ValidateOrThrow()
        {
            profiles ??= Array.Empty<MirraQualityProfile>();
            qualityLights ??= Array.Empty<GameObject>();
            qualityParticles ??= Array.Empty<ParticleSystem>();
            parallaxLayers ??= Array.Empty<ParallaxLayer2D>();
            if (profiles.Length != 4 || profiles.Any(profile => profile == null) ||
                profiles.Select(profile => profile.Quality).Distinct().Count() != 4 ||
                profiles.Select(profile => profile.StableId).Distinct().Count() != 4 ||
                qualityCamera == null || qualityLights.Length != 3 ||
                qualityLights.Any(light => light == null) ||
                qualityParticles.Length != 1 || qualityParticles[0] == null ||
                postProcessingVolume == null || parallaxLayers.Length != 6 ||
                parallaxLayers.Any(layer => layer == null))
            {
                throw new InvalidOperationException(
                    "Mirra quality requires four profiles, the three bounded lights, " +
                    "one particle system, one volume, one camera and six parallax bands.");
            }

            foreach (var profile in profiles)
            {
                profile.ValidateOrThrow();
            }
        }

        private void OnSettingsChanged(GameSettings settings)
        {
            ApplyQuality(settings.PresentationQuality);
        }

        private static float GetFloatProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.PropertyType == typeof(float))
            {
                return (float)property.GetValue(target);
            }

            var field = target.GetType().GetField(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            if (field == null || field.FieldType != typeof(float))
            {
                throw new InvalidOperationException(
                    $"{target.GetType().Name} requires float property {propertyName}.");
            }

            return (float)field.GetValue(target);
        }

        private static void SetFloatProperty(
            object target,
            string propertyName,
            float value)
        {
            var property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.CanWrite &&
                property.PropertyType == typeof(float))
            {
                property.SetValue(target, value);
                return;
            }

            var field = target.GetType().GetField(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            if (field == null || field.FieldType != typeof(float))
            {
                throw new InvalidOperationException(
                    $"{target.GetType().Name} requires writable float property " +
                    propertyName + ".");
            }

            field.SetValue(target, value);
        }

        private void OnDestroy()
        {
            if (m_Settings != null)
            {
                m_Settings.SettingsChanged -= OnSettingsChanged;
                m_Settings = null;
            }

            RestoreGlobalState();
        }
    }
}
