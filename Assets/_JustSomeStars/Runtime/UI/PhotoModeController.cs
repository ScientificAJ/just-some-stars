using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Commerce;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Cosmetics;
using JustSomeStars.Runtime.Animation2D;
using JustSomeStars.Runtime.Flight;
using JustSomeStars.Runtime.Input;
using JustSomeStars.Runtime.Player;
using JustSomeStars.Runtime.Saving;
using JustSomeStars.Runtime.Cinematics;
using UnityEngine;
using UnityEngine.UI;

namespace JustSomeStars.Runtime.UI
{
    public sealed class PhotoModeRuntimeDependencies
    {
        public PhotoModeRuntimeDependencies(
            GameModeController modes,
            Camera camera,
            IStoreService store,
            Bounds panBounds,
            CanvasGroup[] hudGroups,
            InputRouter input = null,
            ISaveService saves = null,
            CosmeticCatalog catalog = null)
        {
            Modes = modes ?? throw new ArgumentNullException(nameof(modes));
            Camera = camera ?? throw new ArgumentNullException(nameof(camera));
            Store = store ?? throw new ArgumentNullException(nameof(store));
            if (!camera.orthographic || panBounds.size.x <= 0f ||
                panBounds.size.y <= 0f)
            {
                throw new ArgumentException(
                    "Photo Mode requires an orthographic camera and positive 2D pan bounds.",
                    nameof(panBounds));
            }
            if (hudGroups == null || Array.Exists(hudGroups, item => item == null))
            {
                throw new ArgumentException(
                    "Photo Mode HUD groups cannot contain null entries.",
                    nameof(hudGroups));
            }
            PanBounds = panBounds;
            HudGroups = (CanvasGroup[])hudGroups.Clone();
            Input = input;
            Saves = saves;
            Catalog = catalog;
        }

        public GameModeController Modes { get; }
        public Camera Camera { get; }
        public IStoreService Store { get; }
        public Bounds PanBounds { get; }
        public CanvasGroup[] HudGroups { get; }
        public InputRouter Input { get; }
        public ISaveService Saves { get; }
        public CosmeticCatalog Catalog { get; }
    }

    [DisallowMultipleComponent]
    public sealed class PhotoModeController :
        MonoBehaviour,
        ISurfaceGameplayExtension,
        IFlightGameplayExtension,
        IChapterOneSequenceExtension
    {
        private const float MinimumZoom = 2.5f;
        private const float MaximumZoom = 7f;
        private const float MinimumExposure = -1.5f;
        private const float MaximumExposure = 1.5f;

        [SerializeField] private Camera photoCamera;
        [SerializeField] private CosmeticCatalog catalog;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private GameObject explorerControlsRoot;
        [SerializeField] private CanvasGroup[] hudGroups = Array.Empty<CanvasGroup>();
        [SerializeField] private SpriteRenderer[] exposureTargets =
            Array.Empty<SpriteRenderer>();
        [SerializeField] private SpriteRenderer[] depthLayers =
            Array.Empty<SpriteRenderer>();
        [SerializeField] private Image earnedFrameImage;
        [SerializeField] private Sprite[] earnedFrames = Array.Empty<Sprite>();
        [SerializeField] private string[] earnedFrameIds = Array.Empty<string>();
        [SerializeField] private LayeredCharacterRenderer[] poseActors =
            Array.Empty<LayeredCharacterRenderer>();
        [SerializeField] private Bounds panBounds =
            new Bounds(Vector3.zero, new Vector3(16f, 8f, 1f));

        private readonly Dictionary<SpriteRenderer, Color> m_OriginalColors =
            new Dictionary<SpriteRenderer, Color>();
        private PhotoModeRuntimeDependencies m_Dependencies;
        private HudState[] m_HudState = Array.Empty<HudState>();
        private Vector3 m_OriginalPosition;
        private Quaternion m_OriginalRotation;
        private float m_OriginalZoom;
        private float m_OriginalTimeScale;
        private int m_DepthIndex;
        private int[] m_OwnedFrameIndices = Array.Empty<int>();
        private string[] m_OriginalActorMotions = Array.Empty<string>();
        private int m_SelectedFrameCursor = -1;
        private int m_SelectedLens;
        private bool m_IsBound;
        private bool m_IsTransitioning;
        private bool m_CleanHud;

        public bool IsOpen { get; private set; }
        public bool AllowsFreeOrbit => false;
        public bool AdvancedControlsAvailable { get; private set; }
        public float Exposure { get; private set; }
        public int SelectedDepthLayer => m_DepthIndex;

        public void Configure(PhotoModeRuntimeDependencies dependencies)
        {
            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }
            if (m_Dependencies != null && !ReferenceEquals(m_Dependencies, dependencies))
            {
                throw new InvalidOperationException(
                    "PhotoModeController cannot be rebound without release.");
            }

            m_Dependencies = dependencies;
            photoCamera = dependencies.Camera;
            panBounds = dependencies.PanBounds;
            hudGroups = dependencies.HudGroups;
            AdvancedControlsAvailable = IsExplorerVerified(dependencies.Store);
            if (explorerControlsRoot != null)
            {
                explorerControlsRoot.SetActive(AdvancedControlsAvailable);
            }
            panelRoot?.SetActive(false);
            CaptureColorBaselines();
            BindInput(dependencies.Input);
        }

        public void Configure(SurfaceGameplayDependencies dependencies)
        {
            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }
            var camera = photoCamera != null
                ? photoCamera
                : GetComponentInParent<SurfaceGameplayLifecycle2D>()?
                    .GetComponentInChildren<CompositionCamera2D>(true)?
                    .ControlledCamera;
            Configure(new PhotoModeRuntimeDependencies(
                dependencies.Modes,
                camera,
                dependencies.Store ?? new UnavailableStoreService(),
                panBounds,
                ResolveHudGroups(),
                dependencies.Input,
                dependencies.Saves,
                catalog));
        }

        public void Release(SurfaceGameplayDependencies dependencies)
        {
            _ = dependencies;
            ReleaseRuntime();
        }

        public void Configure(FlightGameplayDependencies dependencies)
        {
            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }
            var camera = photoCamera != null
                ? photoCamera
                : GetComponentInParent<FlightGameplayLifecycle2D>()?
                    .CompositionCamera.ControlledCamera;
            Configure(new PhotoModeRuntimeDependencies(
                dependencies.Modes,
                camera,
                dependencies.Store ?? new UnavailableStoreService(),
                panBounds,
                ResolveHudGroups(),
                dependencies.Input,
                dependencies.Saves,
                catalog));
        }

        public void Release(FlightGameplayDependencies dependencies)
        {
            _ = dependencies;
            ReleaseRuntime();
        }

        public void Configure(ChapterOneSequenceDependencies dependencies)
        {
            if (dependencies == null || dependencies.Settings == null)
            {
                throw new InvalidOperationException(
                    "Clubhouse Photo Mode requires settings dependencies.");
            }
            Configure(new PhotoModeRuntimeDependencies(
                dependencies.Modes,
                photoCamera,
                dependencies.Store ?? new UnavailableStoreService(),
                panBounds,
                ResolveHudGroups(),
                dependencies.Input,
                dependencies.Saves,
                catalog));
        }

        public void Release(ChapterOneSequenceDependencies dependencies)
        {
            _ = dependencies;
            ReleaseRuntime();
        }

        public async ValueTask OpenAsync(CancellationToken cancellationToken)
        {
            RequireConfigured();
            if (IsOpen || m_IsTransitioning)
            {
                return;
            }
            m_IsTransitioning = true;
            try
            {
                await m_Dependencies.Modes.OpenOverlayAsync(
                    GameOverlay.PhotoMode,
                    cancellationToken);
                await RefreshOwnedFramesAsync(cancellationToken);
                m_OriginalPosition = photoCamera.transform.position;
                m_OriginalRotation = photoCamera.transform.rotation;
                m_OriginalZoom = photoCamera.orthographicSize;
                m_OriginalTimeScale = Time.timeScale;
                m_HudState = CaptureHudState();
                m_OriginalActorMotions = (poseActors ??
                        Array.Empty<LayeredCharacterRenderer>())
                    .Select(actor => actor != null ? actor.CurrentMotion : string.Empty)
                    .ToArray();
                Time.timeScale = 0f;
                IsOpen = true;
                Exposure = 0f;
                m_DepthIndex = 0;
                SetCleanHud(false);
                ApplyDepthFocus();
                panelRoot?.SetActive(true);
            }
            finally
            {
                m_IsTransitioning = false;
            }
        }

        public async ValueTask CloseAsync(CancellationToken cancellationToken)
        {
            if (!IsOpen || m_IsTransitioning)
            {
                return;
            }
            m_IsTransitioning = true;
            try
            {
                await m_Dependencies.Modes.CloseOverlayAsync(cancellationToken);
            }
            finally
            {
                RestoreVisualState();
                m_IsTransitioning = false;
            }
        }

        public void PanBy(Vector2 delta)
        {
            RequireOpen();
            var current = photoCamera.transform.position;
            current.x = Mathf.Clamp(current.x + delta.x, panBounds.min.x, panBounds.max.x);
            current.y = Mathf.Clamp(current.y + delta.y, panBounds.min.y, panBounds.max.y);
            photoCamera.transform.position = current;
        }

        public void ZoomBy(float delta)
        {
            RequireOpen();
            photoCamera.orthographicSize = Mathf.Clamp(
                photoCamera.orthographicSize + delta,
                MinimumZoom,
                MaximumZoom);
        }

        public void CycleDepthFocus(int direction)
        {
            RequireOpen();
            if (depthLayers == null || depthLayers.Length == 0)
            {
                return;
            }
            m_DepthIndex = (m_DepthIndex + Math.Sign(direction) +
                depthLayers.Length) % depthLayers.Length;
            ApplyDepthFocus();
        }

        public void SetExposure(float exposure)
        {
            RequireOpen();
            Exposure = Mathf.Clamp(exposure, MinimumExposure, MaximumExposure);
            var multiplier = Mathf.Pow(2f, Exposure * 0.35f);
            foreach (var pair in m_OriginalColors)
            {
                if (pair.Key == null)
                {
                    continue;
                }
                var linear = pair.Value.linear;
                linear.r = Mathf.Clamp01(linear.r * multiplier);
                linear.g = Mathf.Clamp01(linear.g * multiplier);
                linear.b = Mathf.Clamp01(linear.b * multiplier);
                linear.a = pair.Value.a;
                pair.Key.color = linear.gamma;
            }
            ApplyDepthFocus();
        }

        public void SetCleanHud(bool clean)
        {
            RequireOpen();
            for (var index = 0; index < hudGroups.Length; index++)
            {
                hudGroups[index].alpha = clean ? 0f : m_HudState[index].Alpha;
                hudGroups[index].blocksRaycasts = !clean &&
                    m_HudState[index].BlocksRaycasts;
                hudGroups[index].interactable = !clean &&
                    m_HudState[index].Interactable;
            }
            m_CleanHud = clean;
        }

        public async void ToggleFromUi()
        {
            try
            {
                if (IsOpen)
                {
                    await CloseAsync(destroyCancellationToken);
                }
                else
                {
                    await OpenAsync(destroyCancellationToken);
                }
            }
            catch (OperationCanceledException) when (
                destroyCancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        public void PanLeft() => PanBy(Vector2.left * 0.4f);
        public void PanRight() => PanBy(Vector2.right * 0.4f);
        public void PanUp() => PanBy(Vector2.up * 0.3f);
        public void PanDown() => PanBy(Vector2.down * 0.3f);
        public void ZoomIn() => ZoomBy(-0.35f);
        public void ZoomOut() => ZoomBy(0.35f);
        public void NextDepthLayer() => CycleDepthFocus(1);
        public void IncreaseExposure() => SetExposure(Exposure + 0.25f);
        public void DecreaseExposure() => SetExposure(Exposure - 0.25f);
        public void ToggleCleanHud() => SetCleanHud(!m_CleanHud);

        public void CaptureToGallery()
        {
            var fileName = "just-some-stars-" +
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".png";
            CaptureCleanScreenshot(Path.Combine(
                Application.persistentDataPath,
                "JustSomeStars",
                "photos",
                fileName));
        }

        public void SelectEarnedFrame(int index)
        {
            RequireOpen();
            if (earnedFrameImage == null || index < 0 ||
                index >= m_OwnedFrameIndices.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            var assetIndex = m_OwnedFrameIndices[index];
            earnedFrameImage.sprite = earnedFrames[assetIndex];
            earnedFrameImage.enabled = true;
            m_SelectedFrameCursor = index;
        }

        public void NextEarnedFrame()
        {
            RequireOpen();
            if (m_OwnedFrameIndices.Length == 0)
            {
                earnedFrameImage.enabled = false;
                return;
            }
            SelectEarnedFrame((m_SelectedFrameCursor + 1) %
                m_OwnedFrameIndices.Length);
        }

        public void SelectCinematicLens(int index)
        {
            RequireAdvanced();
            var lenses = new[] { 3.4f, 4f, 5.2f };
            if (index < 0 || index >= lenses.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            photoCamera.orthographicSize = lenses[index];
            m_SelectedLens = index;
        }

        public void NextCinematicLens() =>
            SelectCinematicLens((m_SelectedLens + 1) % 3);

        public void SelectExpandedPose(int index)
        {
            RequireAdvanced();
            var motions = new[] { "idle", "interact", "scan" };
            if (index < 0 || index >= motions.Length || poseActors == null ||
                poseActors.Length == 0 || Array.Exists(poseActors, actor => actor == null))
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            foreach (var actor in poseActors)
            {
                actor.Play(motions[index]);
            }
        }

        public void NextExpandedPose()
        {
            RequireAdvanced();
            var current = poseActors != null && poseActors.Length > 0
                ? poseActors[0].CurrentMotion
                : string.Empty;
            var next = current == "idle" ? 1 : current == "interact" ? 2 : 0;
            SelectExpandedPose(next);
        }

        public void SaveExplorerPresetZero() => SaveExplorerPreset(0);
        public void LoadExplorerPresetZero() => LoadExplorerPreset(0);

        public void SaveExplorerPreset(int slot)
        {
            RequireAdvanced();
            if (slot < 0 || slot > 2)
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }
            PhotoModePresetStore.Save(slot, new PhotoModePreset
            {
                x = photoCamera.transform.position.x,
                y = photoCamera.transform.position.y,
                zoom = photoCamera.orthographicSize,
                exposure = Exposure,
                depth = m_DepthIndex,
            });
        }

        public void LoadExplorerPreset(int slot)
        {
            RequireAdvanced();
            var preset = PhotoModePresetStore.Load(slot);
            if (preset == null)
            {
                throw new InvalidOperationException(
                    $"Explorer Photo Mode preset {slot} does not exist.");
            }
            var position = photoCamera.transform.position;
            position.x = Mathf.Clamp(preset.x, panBounds.min.x, panBounds.max.x);
            position.y = Mathf.Clamp(preset.y, panBounds.min.y, panBounds.max.y);
            photoCamera.transform.position = position;
            photoCamera.orthographicSize = Mathf.Clamp(
                preset.zoom,
                MinimumZoom,
                MaximumZoom);
            m_DepthIndex = depthLayers == null || depthLayers.Length == 0
                ? 0
                : Mathf.Clamp(preset.depth, 0, depthLayers.Length - 1);
            SetExposure(preset.exposure);
            ApplyDepthFocus();
        }

        public void CaptureCleanScreenshot(string absolutePath)
        {
            RequireOpen();
            if (string.IsNullOrWhiteSpace(absolutePath) ||
                !Path.IsPathRooted(absolutePath))
            {
                throw new ArgumentException(
                    "Photo captures require an absolute path.",
                    nameof(absolutePath));
            }
            StartCoroutine(CaptureRoutine(Path.GetFullPath(absolutePath)));
        }

        private IEnumerator CaptureRoutine(string path)
        {
            var panelWasActive = panelRoot != null && panelRoot.activeSelf;
            var cleanWasActive = m_CleanHud;
            panelRoot?.SetActive(false);
            SetCleanHud(true);
            yield return new WaitForEndOfFrame();
            Directory.CreateDirectory(Path.GetDirectoryName(path) ??
                Application.persistentDataPath);
            ScreenCapture.CaptureScreenshot(path);
            if (panelRoot != null)
            {
                panelRoot.SetActive(panelWasActive);
            }
            SetCleanHud(cleanWasActive);
        }

        private async void HandleCommand(
            GameplayInputMode mode,
            SemanticGameplayCommand command)
        {
            _ = mode;
            if (command != SemanticGameplayCommand.PhotoMode || m_IsTransitioning)
            {
                return;
            }
            try
            {
                if (IsOpen)
                {
                    await CloseAsync(destroyCancellationToken);
                }
                else
                {
                    await OpenAsync(destroyCancellationToken);
                }
            }
            catch (OperationCanceledException) when (
                destroyCancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private void BindInput(InputRouter input)
        {
            if (m_IsBound || input == null)
            {
                return;
            }
            input.GameplayCommandPerformed += HandleCommand;
            m_IsBound = true;
        }

        private void ReleaseRuntime()
        {
            if (m_Dependencies?.Input != null && m_IsBound)
            {
                m_Dependencies.Input.GameplayCommandPerformed -= HandleCommand;
            }
            m_IsBound = false;
            if (IsOpen)
            {
                RestoreVisualState();
            }
            m_Dependencies = null;
        }

        private void OnDestroy()
        {
            ReleaseRuntime();
        }

        private CanvasGroup[] ResolveHudGroups()
        {
            if (hudGroups != null && hudGroups.Length > 0)
            {
                return hudGroups;
            }
            return GetComponentsInParent<CanvasGroup>(true);
        }

        private void CaptureColorBaselines()
        {
            m_OriginalColors.Clear();
            foreach (var renderer in exposureTargets ?? Array.Empty<SpriteRenderer>())
            {
                if (renderer != null && !m_OriginalColors.ContainsKey(renderer))
                {
                    m_OriginalColors.Add(renderer, renderer.color);
                }
            }
            foreach (var renderer in depthLayers ?? Array.Empty<SpriteRenderer>())
            {
                if (renderer != null && !m_OriginalColors.ContainsKey(renderer))
                {
                    m_OriginalColors.Add(renderer, renderer.color);
                }
            }
        }

        private async ValueTask RefreshOwnedFramesAsync(
            CancellationToken cancellationToken)
        {
            m_OwnedFrameIndices = Array.Empty<int>();
            m_SelectedFrameCursor = -1;
            if (earnedFrames == null || earnedFrameIds == null ||
                earnedFrames.Length != earnedFrameIds.Length ||
                earnedFrames.Length == 0)
            {
                return;
            }
            if (Array.Exists(earnedFrames, frame => frame == null) ||
                Array.Exists(earnedFrameIds, string.IsNullOrWhiteSpace))
            {
                throw new InvalidOperationException(
                    "Photo Mode earned-frame assets are incomplete.");
            }
            if (m_Dependencies.Catalog == null)
            {
                m_OwnedFrameIndices = Enumerable.Range(0, earnedFrames.Length)
                    .Take(1)
                    .ToArray();
                return;
            }
            var loaded = m_Dependencies.Saves == null
                ? null
                : await m_Dependencies.Saves.LoadAsync(cancellationToken);
            var save = loaded?.HasSave == true
                ? loaded.Save
                : GameSave.CreateNew("photo-mode-preview", DateTime.UtcNow.Ticks);
            var ownership = new OwnershipResolver(m_Dependencies.Catalog);
            m_OwnedFrameIndices = earnedFrameIds
                .Select((id, index) => new { id, index })
                .Where(item => ownership.Resolve(
                    item.id,
                    save,
                    m_Dependencies.Store.CurrentEntitlements).Owned)
                .Select(item => item.index)
                .ToArray();
        }

        private HudState[] CaptureHudState()
        {
            var states = new HudState[hudGroups.Length];
            for (var index = 0; index < hudGroups.Length; index++)
            {
                states[index] = new HudState(
                    hudGroups[index].alpha,
                    hudGroups[index].interactable,
                    hudGroups[index].blocksRaycasts);
            }
            return states;
        }

        private void ApplyDepthFocus()
        {
            if (depthLayers == null || depthLayers.Length == 0)
            {
                return;
            }
            for (var index = 0; index < depthLayers.Length; index++)
            {
                var renderer = depthLayers[index];
                if (renderer == null || !m_OriginalColors.TryGetValue(renderer, out var original))
                {
                    continue;
                }
                var current = renderer.color;
                current.a = original.a * (index == m_DepthIndex ? 1f : 0.68f);
                renderer.color = current;
            }
        }

        private void RestoreVisualState()
        {
            Time.timeScale = m_OriginalTimeScale;
            photoCamera.transform.position = m_OriginalPosition;
            photoCamera.transform.rotation = m_OriginalRotation;
            photoCamera.orthographicSize = m_OriginalZoom;
            for (var index = 0; index < hudGroups.Length && index < m_HudState.Length; index++)
            {
                hudGroups[index].alpha = m_HudState[index].Alpha;
                hudGroups[index].interactable = m_HudState[index].Interactable;
                hudGroups[index].blocksRaycasts = m_HudState[index].BlocksRaycasts;
            }
            foreach (var pair in m_OriginalColors)
            {
                if (pair.Key != null)
                {
                    pair.Key.color = pair.Value;
                }
            }
            for (var index = 0;
                 index < poseActors.Length && index < m_OriginalActorMotions.Length;
                 index++)
            {
                if (poseActors[index] != null &&
                    !string.IsNullOrWhiteSpace(m_OriginalActorMotions[index]))
                {
                    poseActors[index].Play(m_OriginalActorMotions[index]);
                }
            }
            panelRoot?.SetActive(false);
            Exposure = 0f;
            m_CleanHud = false;
            IsOpen = false;
        }

        private void RequireConfigured()
        {
            if (m_Dependencies == null)
            {
                throw new InvalidOperationException(
                    "Photo Mode must be configured before use.");
            }
        }

        private void RequireOpen()
        {
            RequireConfigured();
            if (!IsOpen)
            {
                throw new InvalidOperationException("Photo Mode is not open.");
            }
        }

        private void RequireAdvanced()
        {
            RequireOpen();
            if (!AdvancedControlsAvailable)
            {
                throw new InvalidOperationException(
                    "Explorer Photo Mode tools require a verified entitlement.");
            }
        }

        private static bool IsExplorerVerified(IStoreService store)
        {
            var snapshot = store?.CurrentEntitlements;
            return snapshot != null && snapshot.IsVerified && snapshot.Owns(
                new ContentId(EditionFeatureService.ExplorerEntitlementId));
        }

        private readonly struct HudState
        {
            public HudState(float alpha, bool interactable, bool blocksRaycasts)
            {
                Alpha = alpha;
                Interactable = interactable;
                BlocksRaycasts = blocksRaycasts;
            }

            public float Alpha { get; }
            public bool Interactable { get; }
            public bool BlocksRaycasts { get; }
        }

        [Serializable]
        private sealed class PhotoModePreset
        {
            public float x;
            public float y;
            public float zoom;
            public float exposure;
            public int depth;
        }

        private static class PhotoModePresetStore
        {
            public static void Save(int slot, PhotoModePreset preset)
            {
                var directory = Path.Combine(
                    Application.persistentDataPath,
                    "JustSomeStars",
                    "photo-mode");
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, $"explorer-preset-{slot}.json");
                var temporary = path + ".tmp";
                File.WriteAllText(
                    temporary,
                    JsonUtility.ToJson(preset, prettyPrint: true),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                if (File.Exists(path))
                {
                    File.Replace(temporary, path, null);
                }
                else
                {
                    File.Move(temporary, path);
                }
            }

            public static PhotoModePreset Load(int slot)
            {
                if (slot < 0 || slot > 2)
                {
                    throw new ArgumentOutOfRangeException(nameof(slot));
                }
                var path = Path.Combine(
                    Application.persistentDataPath,
                    "JustSomeStars",
                    "photo-mode",
                    $"explorer-preset-{slot}.json");
                if (!File.Exists(path))
                {
                    return null;
                }
                return JsonUtility.FromJson<PhotoModePreset>(
                    File.ReadAllText(path, Encoding.UTF8));
            }
        }
    }
}
