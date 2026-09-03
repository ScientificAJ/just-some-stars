using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JustSomeStars.Runtime.Animation2D;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Crew;
using JustSomeStars.Runtime.Rendering2D;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace JustSomeStars.Editor.Validation
{
    public sealed class PerformanceBudgetSample
    {
        public string Owner { get; set; }
        public long TextureResidencyBytes { get; set; }
        public int AtlasCount { get; set; }
        public int TransparentLayerPeak { get; set; }
        public int ActiveCharacters { get; set; }
        public int TwoDLights { get; set; }
        public int ParticleSystems { get; set; }
        public int ProcessMemoryMb { get; set; }
        public int MaxTextureDimension { get; set; }
        public string MaxTextureDimensionAsset { get; set; }
        public int AuthoredCameraCount { get; set; }
        public int DynamicResolutionCameraCount { get; set; }
    }

    public static class PerformanceBudgetValidator
    {
        public const int MaxEnvironmentLayers = 8;
        public const int MaxActiveCharacters = 6;
        public const int MaxTwoDLights = 4;
        public const int MaxParticleSystems = 3;
        public const int MaxAndroidTextureDimension = 2048;
        public const int MaxDestinationAtlases = 24;
        public const int MaxTransparentLayerPeak = 8;
        public const int PerformanceProcessMemoryBudgetMb = 896;
        public const long MaxDestinationTextureResidencyBytes =
            256L * 1024L * 1024L;

        private const string SceneRoot = "Assets/_JustSomeStars/Scenes";

        public static IReadOnlyList<string> CollectFindings(
            PerformanceBudgetSample sample)
        {
            if (sample == null)
            {
                throw new ArgumentNullException(nameof(sample));
            }
            if (string.IsNullOrWhiteSpace(sample.Owner))
            {
                throw new ArgumentException("A measured owner is required.", nameof(sample));
            }

            var findings = new List<string>();
            AddIfOver(
                findings,
                sample.Owner,
                "texture residency bytes",
                sample.TextureResidencyBytes,
                MaxDestinationTextureResidencyBytes);
            AddIfOver(findings, sample.Owner, "atlas count",
                sample.AtlasCount, MaxDestinationAtlases);
            AddIfOver(findings, sample.Owner, "transparent layer peak",
                sample.TransparentLayerPeak, MaxTransparentLayerPeak);
            AddIfOver(findings, sample.Owner, "active characters",
                sample.ActiveCharacters, MaxActiveCharacters);
            AddIfOver(findings, sample.Owner, "2D lights",
                sample.TwoDLights, MaxTwoDLights);
            AddIfOver(findings, sample.Owner, "particle systems",
                sample.ParticleSystems, MaxParticleSystems);
            AddIfOver(findings, sample.Owner, "process memory MB",
                sample.ProcessMemoryMb, PerformanceProcessMemoryBudgetMb);
            if (sample.MaxTextureDimension > MaxAndroidTextureDimension)
            {
                findings.Add(
                    $"{sample.Owner}: texture dimension measured " +
                    $"{sample.MaxTextureDimension}px; limit " +
                    $"{MaxAndroidTextureDimension}px; asset " +
                    $"{sample.MaxTextureDimensionAsset ?? "<unknown>"}.");
            }
            if (sample.DynamicResolutionCameraCount < sample.AuthoredCameraCount)
            {
                findings.Add(
                    $"{sample.Owner}: dynamic-resolution cameras measured " +
                    $"{sample.DynamicResolutionCameraCount}; required " +
                    $"{sample.AuthoredCameraCount} authored cameras.");
            }
            return findings;
        }

        public static IReadOnlyList<string> CollectProjectFindings()
        {
            var findings = new List<string>();
            IReadOnlyList<PerformanceBudgetSample> samples;
            try
            {
                samples = CollectProjectSamples();
            }
            catch (Exception exception)
            {
                return new[]
                {
                    $"{SceneRoot}: budget measurement failed: {exception.Message}",
                };
            }

            foreach (var sample in samples)
            {
                findings.AddRange(CollectFindings(sample));
            }

            return findings;
        }

        public static IReadOnlyList<PerformanceBudgetSample> CollectProjectSamples()
        {
            var samples = new List<PerformanceBudgetSample>();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scenes = EditorBuildSettings.scenes
                    .Where(scene => scene.enabled)
                    .Select(scene => scene.path)
                    .Where(path => path.StartsWith(
                        SceneRoot + "/",
                        StringComparison.Ordinal))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();
                if (scenes.Length == 0)
                {
                    throw new InvalidOperationException(
                        "No enabled player scenes were available for measurement.");
                }

                foreach (var scenePath in scenes)
                {
                    samples.Add(MeasureScene(scenePath));
                }
            }
            finally
            {
                if (setup.Any(item => item.isLoaded))
                {
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                }
                else
                {
                    EditorSceneManager.NewScene(
                        NewSceneSetup.EmptyScene,
                        NewSceneMode.Single);
                }
            }

            return samples;
        }

        private static PerformanceBudgetSample MeasureScene(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var behaviours = roots
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(component => component != null)
                .ToArray();
            var dependencies = AssetDatabase.GetDependencies(scenePath, recursive: true);
            var authoredCameras = roots
                .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .Where(camera => camera != null)
                .Distinct()
                .ToArray();
            var textures = dependencies
                .Select(path => new
                {
                    Path = path,
                    Texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path),
                })
                .Where(item => item.Texture != null)
                .GroupBy(item => item.Path, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();
            var largestTexture = textures
                .OrderByDescending(item => Math.Max(
                    item.Texture.width,
                    item.Texture.height))
                .FirstOrDefault();
            var activeSpriteRenderers = roots
                .SelectMany(root => root.GetComponentsInChildren<SpriteRenderer>(true))
                .Where(renderer => renderer != null && renderer.enabled &&
                    renderer.gameObject.activeInHierarchy && renderer.sprite != null)
                .ToArray();
            var activeActors = MeasureActiveCharacters(behaviours);
            var sample = new PerformanceBudgetSample
            {
                Owner = scenePath,
                TextureResidencyBytes = textures.Sum(item =>
                    Math.Max(0L, Profiler.GetRuntimeMemorySizeLong(item.Texture))),
                AtlasCount = activeSpriteRenderers
                    .Select(renderer => AssetDatabase.GetAssetPath(
                        renderer.sprite.texture))
                    .Where(path => !string.IsNullOrWhiteSpace(path) &&
                        IsShippingSpriteAtlas(path))
                    .Distinct(StringComparer.Ordinal)
                    .Count(),
                TransparentLayerPeak = MeasureTransparentOverlapPeak(
                    activeSpriteRenderers),
                ActiveCharacters = activeActors,
                TwoDLights = behaviours.Count(component =>
                    component.gameObject.activeInHierarchy &&
                    component.GetType().FullName ==
                        "UnityEngine.Rendering.Universal.Light2D"),
                ParticleSystems = roots
                    .SelectMany(root => root.GetComponentsInChildren<ParticleSystem>(true))
                    .Count(particle => particle.gameObject.activeInHierarchy),
                ProcessMemoryMb = 0,
                MaxTextureDimension = largestTexture == null
                    ? 0
                    : Math.Max(
                        largestTexture.Texture.width,
                        largestTexture.Texture.height),
                MaxTextureDimensionAsset = largestTexture?.Path,
                AuthoredCameraCount = authoredCameras.Length,
                DynamicResolutionCameraCount = authoredCameras.Count(camera =>
                    ReadsDynamicResolutionOptIn(camera)),
            };
            return sample;
        }

        private static bool IsShippingSpriteAtlas(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                return false;
            }

            return importer.textureType == TextureImporterType.Sprite &&
                importer.spriteImportMode == SpriteImportMode.Multiple;
        }

        private static bool ReadsDynamicResolutionOptIn(Camera camera)
        {
            var serializedCamera = new SerializedObject(camera);
            var property = serializedCamera.FindProperty(
                "m_AllowDynamicResolution");
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"Camera {camera.name} exposes no dynamic-resolution property.");
            }

            return property.boolValue;
        }

        private static int MeasureActiveCharacters(
            IEnumerable<MonoBehaviour> behaviours)
        {
            var active = behaviours.Where(component =>
                component.gameObject.activeInHierarchy).ToArray();
            var cinematicActors = active
                .Where(component => component is CinematicActor2D)
                .Select(component => component.gameObject)
                .Distinct()
                .ToArray();
            if (cinematicActors.Length > 0)
            {
                return cinematicActors.Length;
            }

            var actors = new HashSet<GameObject>();
            foreach (var component in active)
            {
                if (component is SpriteAtlasAnimator animator)
                {
                    var layeredOwner = animator.GetComponentInParent<
                        LayeredCharacterRenderer>();
                    var cinematicOwner = animator.GetComponentInParent<
                        CinematicActor2D>();
                    var mirraOwner = animator.GetComponentInParent<
                        MirraCrewActorRuntime2D>();
                    var koroOwner = animator.GetComponentInParent<
                        KoroCrewActorRuntime2D>();
                    actors.Add(layeredOwner != null
                        ? layeredOwner.gameObject
                        : cinematicOwner != null
                            ? cinematicOwner.gameObject
                            : mirraOwner != null
                                ? mirraOwner.gameObject
                                : koroOwner != null
                                    ? koroOwner.gameObject
                                    : animator.gameObject);
                }
                else if (component is LayeredCharacterRenderer ||
                    component is CinematicActor2D ||
                    component is MirraCrewActorRuntime2D ||
                    component is KoroCrewActorRuntime2D)
                {
                    actors.Add(component.gameObject);
                }
            }

            return actors.Count;
        }

        private static int MeasureTransparentOverlapPeak(
            IEnumerable<SpriteRenderer> renderers)
        {
            var bounds = renderers
                .Where(renderer => renderer.color.a > 0.001f)
                .Select(renderer => new
                {
                    Layer = renderer.GetComponentInParent<ParallaxLayer2D>(),
                    renderer.bounds,
                })
                .Where(item => item.Layer != null)
                .GroupBy(item => item.Layer)
                .Select(group => CombineBounds(group.Select(item => item.bounds)))
                .Where(item => item.size.x > 0f && item.size.y > 0f)
                .ToArray();
            if (bounds.Length == 0)
            {
                return 0;
            }

            var xEdges = bounds
                .SelectMany(item => new[] { item.min.x, item.max.x })
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
            var peak = 0;
            for (var index = 0; index + 1 < xEdges.Length; index++)
            {
                var x = (xEdges[index] + xEdges[index + 1]) * 0.5f;
                var events = new List<(float Y, int Delta)>();
                foreach (var item in bounds)
                {
                    if (x <= item.min.x || x >= item.max.x)
                    {
                        continue;
                    }

                    events.Add((item.min.y, 1));
                    events.Add((item.max.y, -1));
                }

                var overlap = 0;
                foreach (var item in events
                    .OrderBy(item => item.Y)
                    .ThenByDescending(item => item.Delta))
                {
                    overlap += item.Delta;
                    peak = Math.Max(peak, overlap);
                }
            }

            return peak;
        }

        private static Bounds CombineBounds(IEnumerable<Bounds> values)
        {
            using var enumerator = values.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                return default;
            }

            var combined = enumerator.Current;
            while (enumerator.MoveNext())
            {
                combined.Encapsulate(enumerator.Current);
            }

            return combined;
        }

        private static void AddIfOver(
            ICollection<string> findings,
            string owner,
            string metric,
            long measured,
            long limit)
        {
            if (measured > limit)
            {
                findings.Add(
                    $"{owner}: {metric} measured {measured}; limit {limit}.");
            }
        }
    }
}
