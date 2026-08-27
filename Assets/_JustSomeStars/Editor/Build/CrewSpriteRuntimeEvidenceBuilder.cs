using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using JustSomeStars.Runtime.Animation2D;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JustSomeStars.Editor.Build
{
    public static class CrewSpriteRuntimeEvidenceBuilder
    {
        private const string EvidenceRoot =
            "Builds/Task12Stage4CrewEvidence/RuntimeFrames";
        private const string OwnerFile = ".jss-crew-runtime-evidence-owner";
        private const int Width = 1280;
        private const int Height = 720;
        private const int FramesPerSecond = 12;
        private const int ExpectedFrameCount = 96;
        private static readonly (string Name, string Id)[] Characters =
        {
            ("Mira", "mira"),
            ("Juno", "juno"),
            ("Kai", "kai"),
            ("Bea", "bea"),
            ("Ori", "ori"),
        };
        private static readonly string[] ClipIds =
        {
            "idle", "run", "turn", "jump", "land", "climb", "scan", "interact",
        };
        private static readonly string[] Facings = { "right", "left" };

        public static void Apply()
        {
            var priorScene = SceneManager.GetActiveScene().path;
            var created = new List<UnityEngine.Object>();
            RenderTexture renderTexture = null;
            Texture2D readback = null;
            try
            {
                PrepareOutput();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var cameraObject = new GameObject("Task12Stage4RuntimeEvidenceCamera");
                created.Add(cameraObject);
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 2.35f;
                camera.transform.position = new Vector3(0f, 0.25f, -10f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(5, 15, 27, 255);

                renderTexture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                {
                    filterMode = FilterMode.Bilinear,
                    antiAliasing = 1,
                };
                renderTexture.Create();
                camera.targetTexture = renderTexture;
                readback = new Texture2D(Width, Height, TextureFormat.RGB24, false, false);

                var sets = new List<CharacterSpriteSet>();
                var animators = new List<SpriteAtlasAnimator>();
                for (var index = 0; index < Characters.Length; index++)
                {
                    var character = Characters[index];
                    var path =
                        $"Assets/_JustSomeStars/Content/Characters/{character.Name}SpriteSet.asset";
                    var spriteSet = AssetDatabase.LoadAssetAtPath<CharacterSpriteSet>(path) ??
                        throw new InvalidDataException($"Missing runtime sprite set: {path}.");
                    sets.Add(spriteSet);
                    var target = new GameObject($"RuntimeEvidence-{character.Name}");
                    created.Add(target);
                    target.transform.position = new Vector3(-3.25f + index * 1.625f, -1.55f, 0f);
                    var renderer = target.AddComponent<SpriteRenderer>();
                    renderer.sortingOrder = index;
                    var animator = target.AddComponent<SpriteAtlasAnimator>();
                    animator.Configure(renderer);
                    animators.Add(animator);
                }

                var clipRanges = new List<string>();
                var frameNumber = 0;
                foreach (var facing in Facings)
                {
                    foreach (var clipId in ClipIds)
                    {
                        var clips = sets.Select((set, index) =>
                            set.FindClip(
                                $"{Characters[index].Id}.{clipId}.{facing}"))
                            .ToArray();
                        var frameCount = clips[0].Frames.Count;
                        if (clips.Any(clip => clip.Frames.Count != frameCount))
                        {
                            throw new InvalidDataException(
                                $"Crew frame-count mismatch for {clipId}.{facing}.");
                        }
                        for (var index = 0; index < animators.Count; index++)
                        {
                            animators[index].Play(clips[index]);
                        }

                        var firstFrame = frameNumber;
                        for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
                        {
                            if (frameIndex > 0)
                            {
                                for (var index = 0; index < animators.Count; index++)
                                {
                                    animators[index].Advance(
                                        clips[index].FrameDurations[frameIndex - 1] +
                                        0.00001f);
                                }
                            }
                            var path = Path.Combine(
                                EvidenceRoot,
                                $"frame-{frameNumber:0000}.png");
                            Capture(camera, renderTexture, readback, path);
                            if (clipId == "idle" && frameIndex == 0)
                            {
                                var lineupPath = facing == "right"
                                    ? "Builds/Task12Stage4CrewEvidence/" +
                                      "crew-same-scale-lineup.png"
                                    : "Builds/Task12Stage4CrewEvidence/" +
                                      "crew-same-scale-lineup-left.png";
                                File.Copy(path, lineupPath, true);
                            }
                            frameNumber++;
                        }
                        clipRanges.Add(
                            $"    {{\"id\":\"{clipId}.{facing}\"," +
                            $"\"clipId\":\"{clipId}\"," +
                            $"\"facing\":\"{facing}\"," +
                            $"\"firstFrame\":{firstFrame}," +
                            $"\"frameCount\":{frameCount}}}");
                    }
                }
                if (frameNumber != ExpectedFrameCount)
                {
                    throw new InvalidDataException(
                        $"Runtime evidence captured {frameNumber} frames; " +
                        $"expected {ExpectedFrameCount} across both facings.");
                }

                var assetLines = Characters.Select(character =>
                {
                    var path =
                        $"Assets/_JustSomeStars/Content/Characters/{character.Name}SpriteSet.asset";
                    return
                        $"    {{\"id\":\"{character.Id}\",\"path\":\"{path}\"," +
                        $"\"sha256\":\"{Sha256(path)}\"}}";
                });
                File.WriteAllText(
                    "Builds/Task12Stage4CrewEvidence/runtime-capture-manifest.json",
                    "{\n" +
                    "  \"schemaVersion\": 1,\n" +
                    $"  \"width\": {Width},\n" +
                    $"  \"height\": {Height},\n" +
                    $"  \"framesPerSecond\": {FramesPerSecond},\n" +
                    $"  \"frameCount\": {frameNumber},\n" +
                    "  \"facings\": [\"right\",\"left\"],\n" +
                    "  \"characterOrder\": [\"mira\",\"juno\",\"kai\",\"bea\",\"ori\"],\n" +
                    "  \"assets\": [\n" + string.Join(",\n", assetLines) + "\n  ],\n" +
                    "  \"clips\": [\n" + string.Join(",\n", clipRanges) + "\n  ]\n" +
                    "}\n",
                    new UTF8Encoding(false));
                Debug.Log(
                    $"[JSS Task 12 Stage 4] Captured {frameNumber} Unity runtime evidence frames.");
                Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Exit(1);
            }
            finally
            {
                if (RenderTexture.active == renderTexture)
                {
                    RenderTexture.active = null;
                }
                if (renderTexture != null)
                {
                    renderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }
                if (readback != null)
                {
                    UnityEngine.Object.DestroyImmediate(readback);
                }
                foreach (var target in created)
                {
                    if (target != null)
                    {
                        UnityEngine.Object.DestroyImmediate(target);
                    }
                }
                if (!string.IsNullOrEmpty(priorScene) && File.Exists(priorScene))
                {
                    EditorSceneManager.OpenScene(priorScene, OpenSceneMode.Single);
                }
            }
        }

        private static void Capture(
            Camera camera,
            RenderTexture target,
            Texture2D readback,
            string path)
        {
            camera.Render();
            var prior = RenderTexture.active;
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
            readback.Apply(false, false);
            RenderTexture.active = prior;
            File.WriteAllBytes(path, readback.EncodeToPNG());
        }

        private static void PrepareOutput()
        {
            var parent = Path.GetDirectoryName(EvidenceRoot) ??
                throw new InvalidOperationException("Evidence output has no parent.");
            var owner = Path.Combine(parent, OwnerFile);
            if (Directory.Exists(parent))
            {
                if (!File.Exists(owner))
                {
                    throw new InvalidOperationException(
                        $"Refusing to replace unowned evidence directory: {parent}.");
                }
                Directory.Delete(parent, true);
            }
            Directory.CreateDirectory(EvidenceRoot);
            File.WriteAllText(owner, "Just Some Stars Task 12 Stage 4 runtime evidence\n");
        }

        private static string Sha256(string path)
        {
            using var stream = File.OpenRead(path);
            using var hash = SHA256.Create();
            return string.Concat(hash.ComputeHash(stream).Select(value => value.ToString("x2")));
        }

        private static void Exit(int code)
        {
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(code);
            }
        }
    }
}
