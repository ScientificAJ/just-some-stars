using System;
using System.IO;
using System.Linq;
using JustSomeStars.Runtime.Animation2D;
using JustSomeStars.Runtime.Cosmetics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JustSomeStars.Editor
{
    public static class Task26ChapterOneEvidenceCapture
    {
        private const string EvidenceRoot =
            "/mnt/unity-data/JustSomeStars/Builds/Task26ChapterOne/Evidence";

        private static readonly (string scene, string output)[] Captures =
        {
            ("Assets/_JustSomeStars/Scenes/Destinations/AsterVeil.unity",
                "aster-veil-runtime.png"),
            ("Assets/_JustSomeStars/Scenes/Cinematics/SignalReassembly.unity",
                "signal-reassembly-runtime.png"),
            ("Assets/_JustSomeStars/Scenes/Cinematics/Opening.unity",
                "clubhouse-opening-runtime.png"),
            ("Assets/_JustSomeStars/Scenes/Core/Clubhouse.unity",
                "clubhouse-return-runtime.png"),
            ("Assets/_JustSomeStars/Scenes/Cinematics/DinnerEnding.unity",
                "dinner-ending-runtime.png"),
        };

        public static void Capture()
        {
            try
            {
                Directory.CreateDirectory(EvidenceRoot);
                foreach (var capture in Captures)
                {
                    EditorSceneManager.OpenScene(
                        capture.scene,
                        OpenSceneMode.Single);
                    foreach (var captain in UnityEngine.Object.FindObjectsByType<
                                 LayeredCharacterRenderer>(
                                 FindObjectsInactive.Include,
                                 FindObjectsSortMode.None))
                    {
                        var renderers = captain.LayerRenderers.ToArray();
                        captain.Configure(
                            captain.SpriteSet,
                            CaptainSpriteLoadout.CreateLaunchLook(
                                CaptainBodyFamily.Average,
                                renderers.Length),
                            SpriteFacing.Right,
                            renderers);
                        captain.Play(capture.scene.EndsWith(
                            "/DinnerEnding.unity",
                            StringComparison.Ordinal)
                                ? "interact"
                                : "idle");
                    }

                    var camera = UnityEngine.Object.FindObjectsByType<Camera>(
                            FindObjectsInactive.Exclude,
                            FindObjectsSortMode.None)
                        .OrderByDescending(item => item.CompareTag("MainCamera"))
                        .FirstOrDefault() ?? throw new InvalidOperationException(
                            "Scene has no active camera: " + capture.scene);
                    Render(camera, Path.Combine(EvidenceRoot, capture.output));
                }
                Debug.Log("[JSS Task 26] Five chapter evidence captures completed.");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Render(Camera camera, string path)
        {
            const int width = 1616;
            const int height = 720;
            var renderTexture = new RenderTexture(
                width,
                height,
                24,
                RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear,
            };
            var texture = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false,
                false);
            var previous = RenderTexture.active;
            var previousTarget = camera.targetTexture;
            try
            {
                camera.aspect = width / (float)height;
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                texture.Apply(false, false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(texture);
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }
    }
}
