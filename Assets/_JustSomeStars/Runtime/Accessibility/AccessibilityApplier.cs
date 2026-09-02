using System;
using System.Collections.Generic;
using JustSomeStars.Runtime.Flight;
using JustSomeStars.Runtime.Player;
using JustSomeStars.Runtime.UI;
using JustSomeStars.Runtime.Cinematics;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JustSomeStars.Runtime.Accessibility
{
    [DisallowMultipleComponent]
    public sealed class AccessibilityApplier :
        MonoBehaviour,
        ISurfaceGameplayExtension,
        IFlightGameplayExtension,
        IChapterOneSequenceExtension
    {
        private readonly Dictionary<int, TextBaseline> m_TextBaselines =
            new Dictionary<int, TextBaseline>();
        private readonly Dictionary<int, float> m_ParticleBaselines =
            new Dictionary<int, float>();

        [SerializeField] private TMP_FontAsset m_StandardFont;
        [SerializeField] private TMP_FontAsset m_ReadableFont;
        [SerializeField] private Transform m_ScopeRoot;

        private SettingsService m_Settings;

        public float EffectiveTextScale { get; private set; } = 1f;
        public bool CaptionsEnabled { get; private set; } = true;
        public bool ReducedMotionActive { get; private set; }
        public bool ReducedFlashingActive { get; private set; }
        public float DialogueSpeed => m_Settings?.Current.DialogueSpeed ?? 1f;

        private void OnDestroy()
        {
            Release();
        }

        public void Configure(
            SettingsService settings,
            TMP_FontAsset standardFont,
            TMP_FontAsset readableFont)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }
            if (!settings.IsInitialized)
            {
                throw new InvalidOperationException(
                    "Accessibility requires initialized settings.");
            }
            if (standardFont == null || readableFont == null)
            {
                throw new ArgumentNullException(
                    standardFont == null ? nameof(standardFont) : nameof(readableFont));
            }
            if (m_Settings != null && !ReferenceEquals(m_Settings, settings))
            {
                throw new InvalidOperationException(
                    "AccessibilityApplier cannot be rebound to another settings service.");
            }

            m_StandardFont = standardFont;
            m_ReadableFont = readableFont;
            if (m_Settings == null)
            {
                m_Settings = settings;
                m_Settings.SettingsChanged += OnSettingsChanged;
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
            ApplyNow();
        }

        public void Configure(SettingsService settings)
        {
            Configure(settings, m_StandardFont, m_ReadableFont);
        }

        public void Configure(SurfaceGameplayDependencies dependencies)
        {
            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }
            Configure(dependencies.Settings);
        }

        public void Release(SurfaceGameplayDependencies dependencies)
        {
            _ = dependencies;
            Release();
        }

        public void Configure(FlightGameplayDependencies dependencies)
        {
            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }
            Configure(dependencies.Settings);
        }

        public void Release(FlightGameplayDependencies dependencies)
        {
            _ = dependencies;
            Release();
        }

        public void Configure(ChapterOneSequenceDependencies dependencies)
        {
            if (dependencies?.Settings == null)
            {
                return;
            }
            Configure(dependencies.Settings);
        }

        public void Release(ChapterOneSequenceDependencies dependencies)
        {
            _ = dependencies;
            Release();
        }

        public void Release()
        {
            if (m_Settings == null)
            {
                return;
            }
            m_Settings.SettingsChanged -= OnSettingsChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            m_Settings = null;
        }

        public void ApplyNow()
        {
            if (m_Settings == null)
            {
                return;
            }
            Apply(m_Settings.Current);
        }

        private void OnSettingsChanged(GameSettings settings) => Apply(settings);

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _ = scene;
            _ = mode;
            ApplyNow();
        }

        private void Apply(GameSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            EffectiveTextScale = Mathf.Clamp(settings.TextScale, 0.85f, 1.35f);
            CaptionsEnabled = settings.CaptionsEnabled;
            ReducedMotionActive = settings.ReducedMotion;
            ReducedFlashingActive = settings.ReducedFlashing;

            foreach (var text in FindScoped<TMP_Text>())
            {
                if (text == null)
                {
                    continue;
                }
                var id = text.GetInstanceID();
                if (!m_TextBaselines.TryGetValue(id, out var baseline))
                {
                    baseline = new TextBaseline(text.fontSize, text.characterSpacing);
                    m_TextBaselines.Add(id, baseline);
                }
                text.font = settings.DyslexiaFriendlyFontEnabled
                    ? m_ReadableFont
                    : m_StandardFont;
                text.fontSize = baseline.FontSize * EffectiveTextScale;
                text.characterSpacing = baseline.CharacterSpacing +
                    (settings.DyslexiaFriendlyFontEnabled ? 2.5f : 0f);
                text.enableWordWrapping = true;
                text.overflowMode = TextOverflowModes.Overflow;
            }

            foreach (var caption in FindScoped<AccessibleCaption>())
            {
                caption.Apply(settings.CaptionsEnabled);
            }
            foreach (var layout in FindScoped<AccessibleTouchLayout>())
            {
                layout.Apply(settings.LeftHandedControls);
            }
            foreach (var symbol in FindScoped<AccessibleStatusSymbol>())
            {
                symbol.Apply(settings.ColorVisionMode);
            }
            foreach (var effect in FindScoped<AccessibleEffect>())
            {
                effect.Apply(settings);
            }
            foreach (var motion in FindScoped<FrontendMotionDirector>())
            {
                motion.MotionScale = settings.ReducedMotion ? 0f : 1f;
            }
            foreach (var camera in FindScoped<CompositionCamera2D>())
            {
                camera.ApplySettings(settings);
            }
            foreach (var particles in FindScoped<ParticleSystem>())
            {
                var id = particles.GetInstanceID();
                var emission = particles.emission;
                if (!m_ParticleBaselines.TryGetValue(id, out var baseline))
                {
                    baseline = emission.rateOverTime.constant;
                    m_ParticleBaselines.Add(id, baseline);
                }
                emission.rateOverTime = baseline *
                    Mathf.Clamp01(settings.ParticleDensity);
            }
        }

        private T[] FindScoped<T>() where T : Component
        {
            if (m_ScopeRoot != null)
            {
                return m_ScopeRoot.GetComponentsInChildren<T>(true);
            }
            return FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        }

        private readonly struct TextBaseline
        {
            public TextBaseline(float fontSize, float characterSpacing)
            {
                FontSize = fontSize;
                CharacterSpacing = characterSpacing;
            }

            public float FontSize { get; }
            public float CharacterSpacing { get; }
        }
    }

}
