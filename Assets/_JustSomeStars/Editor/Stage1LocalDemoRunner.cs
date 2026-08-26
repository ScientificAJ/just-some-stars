using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Input;
using JustSomeStars.Runtime.Player;
using JustSomeStars.Runtime.Rendering2D;
using UnityEngine;
using UnityEngine.InputSystem;
using EditorApplication = UnityEditor.EditorApplication;

namespace JustSomeStars.Editor
{
    public sealed class Stage1LocalDemoRunner
    {
        private const int RuntimeCaptureWidth = 1280;
        private const int RuntimeCaptureHeight = 720;
        private const string ExitAfterCaptureEnvironmentVariable =
            "JSS_STAGE1_RUNTIME_EXIT_AFTER_CAPTURE";
        private const string CaptureCameraXEnvironmentVariable =
            "JSS_STAGE1_RUNTIME_CAPTURE_CAMERA_X";

        private SettingsService m_Settings;
        private InputRouter m_Input;
        private GameModeController m_Modes;
        private SurfaceGameplayLifecycle2D m_Lifecycle;
        private SurfaceGameplayDependencies m_Dependencies;
        private Task m_InitializeTask;
        private Task m_ShutdownTask;

        internal bool IsReady { get; private set; }

        internal Task InitializeAsync()
        {
            if (m_InitializeTask == null)
            {
                m_InitializeTask = InitializeCoreAsync();
            }

            return m_InitializeTask;
        }

        internal Task ShutdownAsync()
        {
            if (m_ShutdownTask == null)
            {
                m_ShutdownTask = ShutdownCoreAsync();
            }

            return m_ShutdownTask;
        }

        private async Task InitializeCoreAsync()
        {
            if (m_ShutdownTask != null)
            {
                throw new InvalidOperationException(
                    "The Stage 1 local demo cannot restart after shutdown.");
            }

            try
            {
                var projectRoot = Path.GetFullPath(
                    Path.Combine(Application.dataPath, ".."));
                var settingsPath = Path.Combine(
                    projectRoot,
                    "Library",
                    "JustSomeStars",
                    "EditorDemo",
                    "jss-settings-v1.json");
                var actions = InputSystem.actions;
                if (actions == null)
                {
                    throw new InvalidOperationException(
                        "Unity project-wide JssInputActions is not configured.");
                }

                m_Settings = new SettingsService(settingsPath);
                m_Input = new InputRouter(actions, m_Settings);
                var runtimeHooks = new InputRouterGameModeRuntimeHooks(m_Input);
                m_Modes = GameModeController.CreateForTests(
                    GameMode.Surface,
                    runtimeHooks);

                RequireAvailable(
                    await m_Settings.InitializeAsync(CancellationToken.None),
                    "SettingsService");
                RequireAvailable(
                    await m_Input.InitializeAsync(CancellationToken.None),
                    "InputRouter");
                RequireAvailable(
                    await m_Modes.InitializeAsync(CancellationToken.None),
                    "GameModeController");

                var lifecycles = UnityEngine.Object.FindObjectsByType<
                    SurfaceGameplayLifecycle2D>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                if (lifecycles.Length != 1)
                {
                    throw new InvalidOperationException(
                        "The Stage 1 proof requires exactly one " +
                        "SurfaceGameplayLifecycle2D; found " +
                        lifecycles.Length + ".");
                }

                m_Lifecycle = lifecycles[0];
                m_Dependencies = new SurfaceGameplayDependencies(
                    m_Settings,
                    m_Input,
                    m_Modes);
                m_Lifecycle.Configure(m_Dependencies);
                IsReady = true;
                Debug.Log(
                    "[JSS Stage 1 Demo] Ready. Move: A/D or touch stick. " +
                    "Jump/jet: Space, Left Shift, or JUMP. " +
                    "Interact: E or INTERACT.");
                await CaptureRequestedRuntimeFrameAsync();
            }
            catch
            {
                await ShutdownAsync();
                throw;
            }
        }

        private async Task ShutdownCoreAsync()
        {
            IsReady = false;
            if (m_Lifecycle != null &&
                m_Dependencies != null &&
                m_Lifecycle.IsConfigured)
            {
                m_Lifecycle.Release(m_Dependencies);
            }

            if (m_Modes != null)
            {
                await m_Modes.ShutdownAsync();
            }

            if (m_Input != null)
            {
                await m_Input.ShutdownAsync();
            }

            if (m_Settings != null)
            {
                await m_Settings.ShutdownAsync();
            }

            m_Dependencies = null;
            m_Lifecycle = null;
        }

        private static void RequireAvailable(
            StartupResult result,
            string serviceName)
        {
            if (!result.IsAvailable)
            {
                throw new InvalidOperationException(
                    serviceName + " could not start: " + result.Message,
                    result.Failure);
            }
        }

        private static async Task CaptureRequestedRuntimeFrameAsync()
        {
            var requestedPath = Environment.GetEnvironmentVariable(
                "JSS_STAGE1_RUNTIME_CAPTURE");
            if (string.IsNullOrWhiteSpace(requestedPath))
            {
                return;
            }

            var absolutePath = Path.GetFullPath(requestedPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            if (File.Exists(absolutePath))
            {
                File.Delete(absolutePath);
            }

            await WaitForEditorFramesAsync(10);
            CaptureRuntimeFrame(absolutePath);
            ValidateCapturedDimensions(absolutePath);
            Debug.Log(
                "[JSS Stage 1 Demo] Runtime capture saved: " +
                absolutePath);
            if (ShouldExitAfterCapture)
            {
                EditorApplication.delayCall +=
                    EditorApplication.ExitPlaymode;
            }
        }

        internal static bool ShouldExitAfterCapture => string.Equals(
            Environment.GetEnvironmentVariable(
                ExitAfterCaptureEnvironmentVariable),
            "1",
            StringComparison.Ordinal);

        private static void ValidateCapturedDimensions(string absolutePath)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(absolutePath), false) ||
                    texture.width != RuntimeCaptureWidth ||
                    texture.height != RuntimeCaptureHeight)
                {
                    throw new InvalidOperationException(
                        "The Stage 1 runtime capture must be exactly " +
                        RuntimeCaptureWidth + "x" + RuntimeCaptureHeight +
                        "; Unity wrote " + texture.width + "x" +
                        texture.height + ".");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void CaptureRuntimeFrame(string absolutePath)
        {
            var cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            Camera camera = null;
            foreach (var candidate in cameras)
            {
                if (candidate.isActiveAndEnabled &&
                    (camera == null || candidate.depth > camera.depth))
                {
                    camera = candidate;
                }
            }

            if (camera == null)
            {
                throw new InvalidOperationException(
                    "The Stage 1 runtime capture requires one active camera.");
            }

            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            var renderModes = new RenderMode[canvases.Length];
            var worldCameras = new Camera[canvases.Length];
            var target = new RenderTexture(
                RuntimeCaptureWidth,
                RuntimeCaptureHeight,
                24,
                RenderTextureFormat.ARGB32)
            {
                name = "JSS_Stage1_RuntimeCapture",
            };
            var priorTarget = camera.targetTexture;
            var priorActive = RenderTexture.active;
            Texture2D result = null;
            try
            {
                ApplyRequestedCaptureCameraPosition(camera);
                for (var index = 0; index < canvases.Length; index++)
                {
                    renderModes[index] = canvases[index].renderMode;
                    worldCameras[index] = canvases[index].worldCamera;
                    if (canvases[index].renderMode ==
                        RenderMode.ScreenSpaceOverlay)
                    {
                        canvases[index].renderMode =
                            RenderMode.ScreenSpaceCamera;
                        canvases[index].worldCamera = camera;
                    }
                }

                target.Create();
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                result = new Texture2D(
                    RuntimeCaptureWidth,
                    RuntimeCaptureHeight,
                    TextureFormat.RGB24,
                    false);
                result.ReadPixels(
                    new Rect(
                        0,
                        0,
                        RuntimeCaptureWidth,
                        RuntimeCaptureHeight),
                    0,
                    0,
                    false);
                result.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                File.WriteAllBytes(absolutePath, result.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = priorTarget;
                RenderTexture.active = priorActive;
                for (var index = 0; index < canvases.Length; index++)
                {
                    canvases[index].renderMode = renderModes[index];
                    canvases[index].worldCamera = worldCameras[index];
                }

                if (result != null)
                {
                    UnityEngine.Object.DestroyImmediate(result);
                }

                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void ApplyRequestedCaptureCameraPosition(Camera camera)
        {
            var requested = Environment.GetEnvironmentVariable(
                CaptureCameraXEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(requested))
            {
                return;
            }

            if (!float.TryParse(
                    requested,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var positionX) ||
                positionX < -2.001f ||
                positionX > 2.001f)
            {
                throw new InvalidOperationException(
                    CaptureCameraXEnvironmentVariable +
                    " must be a number from -2 through 2.");
            }

            camera.transform.position = new Vector3(
                positionX,
                camera.transform.position.y,
                camera.transform.position.z);
            var rig = UnityEngine.Object.FindFirstObjectByType<ParallaxRig2D>(
                FindObjectsInactive.Exclude);
            if (rig == null)
            {
                throw new InvalidOperationException(
                    "The Stage 1 runtime capture requires its ParallaxRig2D.");
            }

            rig.ApplyNow();
            Debug.Log(
                "[JSS Stage 1 Demo] Capture camera x: " +
                positionX.ToString("0.###", CultureInfo.InvariantCulture));
        }

        private static Task WaitForEditorFramesAsync(int frameCount)
        {
            var completion = new TaskCompletionSource<bool>();
            var remaining = Mathf.Max(1, frameCount);
            void OnUpdate()
            {
                remaining--;
                if (remaining > 0)
                {
                    return;
                }

                EditorApplication.update -= OnUpdate;
                completion.TrySetResult(true);
            }

            EditorApplication.update += OnUpdate;
            return completion.Task;
        }

    }
}
