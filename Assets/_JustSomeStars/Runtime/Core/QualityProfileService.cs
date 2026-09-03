using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accessibility;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JustSomeStars.Runtime.Core
{
    public sealed class QualityProfileGameService : IGameService
    {
        private readonly SettingsService m_Settings;
        private QualityProfileService m_Runtime;

        public QualityProfileGameService(SettingsService settings)
        {
            m_Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public ValueTask<StartupResult> InitializeAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            m_Runtime ??= QualityProfileService.EnsureInstalled(m_Settings);
            return new ValueTask<StartupResult>(StartupResult.Available());
        }

        public ValueTask ShutdownAsync()
        {
            var runtime = m_Runtime;
            m_Runtime = null;
            if (runtime == null)
            {
                return default;
            }

            runtime.Release(m_Settings);
            if (runtime.gameObject != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(runtime.gameObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(runtime.gameObject);
                }
            }

            return default;
        }
    }

    public readonly struct QualityProfileEnvelope
    {
        public QualityProfileEnvelope(
            int targetFrameRate,
            float minimumRenderScale,
            float maximumRenderScale,
            bool usesAdaptiveResolution)
        {
            TargetFrameRate = targetFrameRate;
            MinimumRenderScale = minimumRenderScale;
            MaximumRenderScale = maximumRenderScale;
            UsesAdaptiveResolution = usesAdaptiveResolution;
        }

        public int TargetFrameRate { get; }
        public float MinimumRenderScale { get; }
        public float MaximumRenderScale { get; }
        public bool UsesAdaptiveResolution { get; }
    }

    [DisallowMultipleComponent]
    public sealed class QualityProfileService : MonoBehaviour
    {
        private const float SampleWindowSeconds = 0.5f;
        private const float SlowFrameMultiplier = 1.08f;
        private const float FastFrameMultiplier = 0.8f;
        private const float DegradeStep = 0.05f;
        private const float RecoverStep = 0.02f;
        private const float LowMemoryStep = 0.10f;
        private const float LowMemoryHoldSeconds = 10f;

        private SettingsService m_Settings;
        private int m_OriginalTargetFrameRate;
        private float m_OriginalWidthScale = 1f;
        private float m_OriginalHeightScale = 1f;
        private float m_AccumulatedSeconds;
        private int m_AccumulatedFrames;
        private bool m_HasSnapshot;
        private bool m_HasApplied;
        private bool m_LowMemoryPending;
        private float m_LowMemoryHoldRemaining;
        private int m_CameraRefreshFrames;
        private readonly Dictionary<Camera, bool> m_CameraDynamicResolution = new();

        public static QualityProfileService Instance { get; private set; }

        public event Action<QualityProfileService> ProfileApplied;

        public bool IsBound => m_Settings != null;
        public PresentationQuality ActiveQuality { get; private set; } =
            PresentationQuality.Balanced;
        public int ActiveTargetFrameRate { get; private set; }
        public float ActiveRenderScale { get; private set; } = 1f;
        public bool ActiveUsesAdaptiveResolution { get; private set; }
        public int ManagedCameraCount => m_CameraDynamicResolution.Count;

        public static QualityProfileService EnsureInstalled(SettingsService settings)
        {
            if (Instance != null)
            {
                if (!Instance.IsBound)
                {
                    Instance.Configure(settings);
                }
                else if (!ReferenceEquals(Instance.m_Settings, settings))
                {
                    throw new InvalidOperationException(
                        "Quality profile service already belongs to another composition.");
                }

                return Instance;
            }

            var root = new GameObject("JSS Quality Profile Service");
            DontDestroyOnLoad(root);
            var service = root.AddComponent<QualityProfileService>();
            service.Configure(settings);
            return service;
        }

        public static QualityProfileEnvelope GetEnvelope(PresentationQuality quality)
        {
            return quality switch
            {
                PresentationQuality.Performance =>
                    new QualityProfileEnvelope(30, 0.70f, 0.82f, true),
                PresentationQuality.Balanced =>
                    new QualityProfileEnvelope(30, 0.80f, 1f, false),
                PresentationQuality.Cinematic =>
                    new QualityProfileEnvelope(30, 0.80f, 1f, false),
                PresentationQuality.HighFrameRate =>
                    new QualityProfileEnvelope(60, 0.72f, 1f, true),
                _ => throw new ArgumentOutOfRangeException(nameof(quality)),
            };
        }

        public static float EvaluateNextScale(
            PresentationQuality quality,
            float currentScale,
            float averageFrameSeconds,
            bool lowMemory)
        {
            var envelope = GetEnvelope(quality);
            var next = Mathf.Clamp(
                currentScale,
                envelope.MinimumRenderScale,
                envelope.MaximumRenderScale);
            if (lowMemory)
            {
                return Mathf.Clamp(
                    Mathf.Round((next - LowMemoryStep) * 100f) / 100f,
                    envelope.MinimumRenderScale,
                    envelope.MaximumRenderScale);
            }
            if (!envelope.UsesAdaptiveResolution)
            {
                return envelope.MaximumRenderScale;
            }

            var targetSeconds = 1f / envelope.TargetFrameRate;
            if (averageFrameSeconds > targetSeconds * SlowFrameMultiplier)
            {
                next -= DegradeStep;
            }
            else if (averageFrameSeconds < targetSeconds * FastFrameMultiplier)
            {
                next += RecoverStep;
            }

            return Mathf.Clamp(
                Mathf.Round(next * 100f) / 100f,
                envelope.MinimumRenderScale,
                envelope.MaximumRenderScale);
        }

        public void Configure(SettingsService settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }
            if (m_Settings != null)
            {
                if (ReferenceEquals(m_Settings, settings))
                {
                    return;
                }

                throw new InvalidOperationException(
                    "Quality profile service cannot be rebound before release.");
            }

            if (Instance != null && !ReferenceEquals(Instance, this))
            {
                throw new InvalidOperationException(
                    "Only one quality profile service may own player globals.");
            }

            Instance = this;
            CaptureGlobals();
            m_Settings = settings;
            m_Settings.SettingsChanged += OnSettingsChanged;
            Application.lowMemory += OnLowMemory;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (m_Settings.IsInitialized)
            {
                ApplyQuality(m_Settings.Current.PresentationQuality);
            }
        }

        public void Release(SettingsService settings)
        {
            if (m_Settings == null)
            {
                return;
            }
            if (!ReferenceEquals(m_Settings, settings))
            {
                throw new InvalidOperationException(
                    "Only the owning settings composition can release quality globals.");
            }

            m_Settings.SettingsChanged -= OnSettingsChanged;
            Application.lowMemory -= OnLowMemory;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            m_Settings = null;
            RestoreGlobals();
            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }
        }

        public void ApplyCurrentToCamera(Camera camera)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            if (!m_CameraDynamicResolution.ContainsKey(camera))
            {
                m_CameraDynamicResolution.Add(
                    camera,
                    camera.allowDynamicResolution);
            }

            // The per-camera flag opts the camera into ScalableBufferManager.
            // A scale of 1.0 still renders natively; keeping the path enabled lets
            // a low-memory event shed resolution in every quality profile.
            camera.allowDynamicResolution = true;
        }

        public bool ManagesCamera(Camera camera)
        {
            return camera != null && m_CameraDynamicResolution.ContainsKey(camera);
        }

        public void SampleFrameForTests(float averageFrameSeconds, bool lowMemory)
        {
            if (!m_HasApplied)
            {
                throw new InvalidOperationException(
                    "Quality must be applied before adaptive sampling.");
            }
            if (averageFrameSeconds <= 0f || float.IsNaN(averageFrameSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(averageFrameSeconds));
            }

            var next = EvaluateNextScale(
                ActiveQuality,
                ActiveRenderScale,
                averageFrameSeconds,
                lowMemory);
            if (!Mathf.Approximately(next, ActiveRenderScale))
            {
                ActiveRenderScale = next;
                ScalableBufferManager.ResizeBuffers(next, next);
                ApplyToLoadedCameras();
                ProfileApplied?.Invoke(this);
            }
        }

        private void Update()
        {
            if (m_Settings == null)
            {
                return;
            }
            if (!m_HasApplied)
            {
                if (m_Settings.IsInitialized)
                {
                    ApplyQuality(m_Settings.Current.PresentationQuality);
                }
                return;
            }

            m_AccumulatedSeconds += Time.unscaledDeltaTime;
            m_AccumulatedFrames++;
            if (m_AccumulatedSeconds < SampleWindowSeconds)
            {
                return;
            }

            var average = m_AccumulatedSeconds / Mathf.Max(1, m_AccumulatedFrames);
            if (m_LowMemoryPending)
            {
                SampleFrameForTests(average, lowMemory: true);
                m_LowMemoryHoldRemaining = LowMemoryHoldSeconds;
            }
            else if (m_LowMemoryHoldRemaining > 0f)
            {
                m_LowMemoryHoldRemaining = Mathf.Max(
                    0f,
                    m_LowMemoryHoldRemaining - m_AccumulatedSeconds);
            }
            else
            {
                SampleFrameForTests(average, lowMemory: false);
            }
            m_AccumulatedSeconds = 0f;
            m_AccumulatedFrames = 0;
            m_LowMemoryPending = false;
        }

        private void LateUpdate()
        {
            if (m_HasApplied && m_CameraRefreshFrames > 0)
            {
                ApplyToLoadedCameras();
                m_CameraRefreshFrames--;
            }
        }

        private void ApplyQuality(PresentationQuality quality)
        {
            var envelope = GetEnvelope(quality);
            ActiveQuality = quality;
            ActiveTargetFrameRate = envelope.TargetFrameRate;
            ActiveRenderScale = envelope.MaximumRenderScale;
            ActiveUsesAdaptiveResolution = envelope.UsesAdaptiveResolution;
            Application.targetFrameRate = envelope.TargetFrameRate;
            ScalableBufferManager.ResizeBuffers(
                envelope.MaximumRenderScale,
                envelope.MaximumRenderScale);
            m_AccumulatedSeconds = 0f;
            m_AccumulatedFrames = 0;
            m_LowMemoryPending = false;
            m_LowMemoryHoldRemaining = 0f;
            m_HasApplied = true;
            ApplyToLoadedCameras();
            m_CameraRefreshFrames = 2;
            ProfileApplied?.Invoke(this);
            Debug.Log(
                $"[JSS Performance] quality={quality} " +
                $"targetFps={envelope.TargetFrameRate} " +
                $"renderScale={ActiveRenderScale:F2} adaptive={envelope.UsesAdaptiveResolution}");
        }

        private void CaptureGlobals()
        {
            if (m_HasSnapshot)
            {
                return;
            }

            m_OriginalTargetFrameRate = Application.targetFrameRate;
            m_OriginalWidthScale = ScalableBufferManager.widthScaleFactor;
            m_OriginalHeightScale = ScalableBufferManager.heightScaleFactor;
            m_HasSnapshot = true;
        }

        private void RestoreGlobals()
        {
            if (!m_HasSnapshot)
            {
                return;
            }

            Application.targetFrameRate = m_OriginalTargetFrameRate;
            ScalableBufferManager.ResizeBuffers(
                m_OriginalWidthScale,
                m_OriginalHeightScale);
            ActiveTargetFrameRate = 0;
            ActiveRenderScale = 1f;
            ActiveUsesAdaptiveResolution = false;
            foreach (var entry in m_CameraDynamicResolution)
            {
                if (entry.Key != null)
                {
                    entry.Key.allowDynamicResolution = entry.Value;
                }
            }
            m_CameraDynamicResolution.Clear();
            m_CameraRefreshFrames = 0;
            m_HasApplied = false;
            m_HasSnapshot = false;
        }

        private void OnSettingsChanged(GameSettings settings)
        {
            if (!m_HasApplied || settings.PresentationQuality != ActiveQuality)
            {
                ApplyQuality(settings.PresentationQuality);
            }
        }

        private void OnLowMemory()
        {
            m_LowMemoryPending = true;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!m_HasApplied)
            {
                return;
            }

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                {
                    ApplyCurrentToCamera(camera);
                }
            }
            m_CameraRefreshFrames = Math.Max(m_CameraRefreshFrames, 2);
        }

        private void ApplyToLoadedCameras()
        {
            var cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (var camera in cameras)
            {
                if (camera != null && camera.gameObject.scene.IsValid())
                {
                    ApplyCurrentToCamera(camera);
                }
            }
        }

        private void OnDestroy()
        {
            if (m_Settings != null)
            {
                var owner = m_Settings;
                Release(owner);
            }
            else if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }
        }
    }
}
