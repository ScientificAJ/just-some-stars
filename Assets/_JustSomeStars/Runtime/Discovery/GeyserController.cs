using System;
using JustSomeStars.Runtime.Player;
using UnityEngine;

namespace JustSomeStars.Runtime.Discovery
{
    public readonly struct GeyserCycleSample
    {
        public GeyserCycleSample(
            float cycleTime,
            bool warningActive,
            bool hazardActive,
            float ballisticHeight,
            float visualIntensity,
            bool signalSource)
        {
            CycleTime = cycleTime;
            WarningActive = warningActive;
            HazardActive = hazardActive;
            BallisticHeight = ballisticHeight;
            VisualIntensity = visualIntensity;
            SignalSource = signalSource;
        }

        public float CycleTime { get; }
        public bool WarningActive { get; }
        public bool HazardActive { get; }
        public float BallisticHeight { get; }
        public float VisualIntensity { get; }
        public bool SignalSource { get; }
    }

    public sealed class GeyserCycleModel
    {
        private readonly float m_CycleSeconds;
        private readonly float m_WarningSeconds;
        private readonly float m_EruptionSeconds;
        private readonly float m_OffsetSeconds;
        private readonly bool m_SignalSource;
        private float m_Time;

        public GeyserCycleModel(
            float cycleSeconds,
            float warningSeconds,
            float eruptionSeconds,
            float offsetSeconds,
            bool signalSource)
        {
            if (!IsFinite(cycleSeconds) || !IsFinite(warningSeconds) ||
                !IsFinite(eruptionSeconds) || !IsFinite(offsetSeconds) ||
                cycleSeconds <= 0f || warningSeconds <= 0f ||
                eruptionSeconds <= 0f ||
                warningSeconds + eruptionSeconds >= cycleSeconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cycleSeconds),
                    "Geyser timing needs a finite telegraph, eruption and recovery.");
            }

            m_CycleSeconds = cycleSeconds;
            m_WarningSeconds = warningSeconds;
            m_EruptionSeconds = eruptionSeconds;
            m_OffsetSeconds = Mathf.Repeat(offsetSeconds, cycleSeconds);
            m_SignalSource = signalSource;
        }

        public GeyserCycleSample Sample(float absoluteTime, bool reducedMotion)
        {
            if (!IsFinite(absoluteTime))
            {
                throw new ArgumentOutOfRangeException(nameof(absoluteTime));
            }

            var cycleTime = Mathf.Repeat(absoluteTime + m_OffsetSeconds, m_CycleSeconds);
            var warning = cycleTime < m_WarningSeconds;
            var eruptionTime = cycleTime - m_WarningSeconds;
            var hazard = eruptionTime >= 0f && eruptionTime < m_EruptionSeconds;
            var normalized = hazard
                ? Mathf.Clamp01(eruptionTime / m_EruptionSeconds)
                : 0f;
            var ballistic = hazard
                ? Mathf.Sin(normalized * Mathf.PI) * 4.2f
                : 0f;
            var fullVisual = warning
                ? 0.32f + (0.52f * cycleTime / m_WarningSeconds)
                : hazard ? 0.75f + (0.25f * Mathf.Sin(normalized * Mathf.PI)) : 0f;
            return new GeyserCycleSample(
                cycleTime,
                warning,
                hazard,
                ballistic,
                reducedMotion ? fullVisual * 0.35f : fullVisual,
                m_SignalSource);
        }

        public GeyserCycleSample Advance(
            float deltaSeconds,
            bool paused,
            bool reducedMotion)
        {
            if (!IsFinite(deltaSeconds) || deltaSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            }
            if (!paused)
            {
                m_Time += deltaSeconds;
            }
            return Sample(m_Time, reducedMotion);
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [DisallowMultipleComponent]
    public sealed class GeyserController : MonoBehaviour, ISurfaceGameplayExtension
    {
        [SerializeField, Min(4f)] private float cycleSeconds = 8f;
        [SerializeField, Min(0.25f)] private float warningSeconds = 1.5f;
        [SerializeField, Min(0.25f)] private float eruptionSeconds = 2.25f;
        [SerializeField, Min(0f)] private float offsetSeconds;
        [SerializeField] private bool signalSource;
        [SerializeField] private Collider2D hazard;
        [SerializeField] private Transform plumeVisual;
        [SerializeField] private SpriteRenderer warningRenderer;
        [SerializeField] private AudioSource cueAudio;

        private GeyserCycleModel m_Model;
        private SurfaceGameplayDependencies m_Dependencies;
        private bool m_PreviousHazard;

        public GeyserCycleSample Current { get; private set; }
        public bool IsConfigured => m_Dependencies != null;

        public void Configure(SurfaceGameplayDependencies dependencies)
        {
            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }
            if (m_Dependencies != null)
            {
                if (ReferenceEquals(m_Dependencies, dependencies))
                {
                    return;
                }
                throw new InvalidOperationException(
                    "Geyser controller is already composition-owned.");
            }
            if (hazard == null || plumeVisual == null || warningRenderer == null)
            {
                throw new InvalidOperationException(
                    "Geyser controller requires hazard, plume and warning bindings.");
            }
            m_Model = new GeyserCycleModel(
                cycleSeconds,
                warningSeconds,
                eruptionSeconds,
                offsetSeconds,
                signalSource);
            EnsureCue();
            m_Dependencies = dependencies;
            Apply(m_Model.Sample(0f, dependencies.Settings.Current.ReducedMotion));
        }

        public void Release(SurfaceGameplayDependencies dependencies)
        {
            if (m_Dependencies == null)
            {
                return;
            }
            if (!ReferenceEquals(m_Dependencies, dependencies))
            {
                throw new InvalidOperationException(
                    "Geyser controller can only release its owning composition.");
            }
            hazard.enabled = false;
            m_Dependencies = null;
            m_Model = null;
            m_PreviousHazard = false;
        }

        private void Update()
        {
            if (m_Dependencies == null)
            {
                return;
            }
            Apply(m_Model.Advance(
                Time.deltaTime,
                Time.timeScale <= 0f,
                m_Dependencies.Settings.Current.ReducedMotion));
        }

        private void Apply(GeyserCycleSample sample)
        {
            Current = sample;
            hazard.enabled = sample.HazardActive;
            plumeVisual.localScale = new Vector3(
                0.72f + sample.VisualIntensity * 0.28f,
                0.12f + sample.BallisticHeight * 0.22f,
                1f);
            warningRenderer.enabled = sample.WarningActive;
            warningRenderer.color = signalSource
                ? new Color(0.65f, 0.28f, 1f, sample.VisualIntensity)
                : new Color(0.35f, 0.92f, 1f, sample.VisualIntensity);
            if (sample.HazardActive && !m_PreviousHazard && cueAudio != null)
            {
                cueAudio.volume = m_Dependencies.Settings.Current.EffectsVolume;
                cueAudio.Play();
            }
            m_PreviousHazard = sample.HazardActive;
        }

        private void OnDestroy()
        {
            if (m_Dependencies != null)
            {
                Release(m_Dependencies);
            }
        }

        private void EnsureCue()
        {
            if (cueAudio == null || cueAudio.clip != null)
            {
                return;
            }

            const int sampleRate = 22050;
            const float seconds = 0.32f;
            var sampleCount = Mathf.CeilToInt(sampleRate * seconds);
            var samples = new float[sampleCount];
            var frequency = signalSource ? 520f : 340f;
            for (var index = 0; index < sampleCount; index++)
            {
                var time = index / (float)sampleRate;
                var envelope = 1f - index / (float)sampleCount;
                samples[index] = Mathf.Sin(2f * Mathf.PI * frequency * time) *
                    envelope * 0.12f;
            }

            var clip = AudioClip.Create(
                signalSource ? "SignalGeyserCue" : "NaturalGeyserCue",
                sampleCount,
                1,
                sampleRate,
                false);
            clip.SetData(samples, 0);
            cueAudio.clip = clip;
            cueAudio.playOnAwake = false;
        }
    }
}
