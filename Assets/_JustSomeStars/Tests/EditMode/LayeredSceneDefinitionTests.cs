using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class LayeredSceneDefinitionTests
    {
        private const string RuntimeAssembly = "JustSomeStars.Runtime";
        private const string ScenePath =
            "Assets/_JustSomeStars/Scenes/Benchmarks/Mirra2DProof.unity";

        private static readonly string[] RequiredBands =
        {
            "Sky",
            "FarWorld",
            "Atmosphere",
            "Midground",
            "Gameplay",
            "ActorsAndProps",
            "Foreground",
            "Hud",
        };

        [Test]
        public void LayerBand_DeclaresTheExactOwnedCompositionBands()
        {
            var bandType = RequireRuntimeType(
                "JustSomeStars.Runtime.Rendering2D.LayerBand");

            Assert.That(Enum.GetNames(bandType), Is.EqualTo(RequiredBands));
            Assert.That(
                Enum.GetValues(bandType)
                    .Cast<object>()
                    .Select(Convert.ToInt32),
                Is.EqualTo(Enumerable.Range(0, RequiredBands.Length)));
        }

        [Test]
        public void Definition_RequiresEveryBindingLayerExactlyOnce()
        {
            var fixture = CreateDefinitionFixture();
            try
            {
                ConfigureDefinition(fixture, RequiredBands);
                var result = Invoke(fixture.Definition, "Validate");

                Assert.That(ReadNames(result, "MissingBands"), Is.Empty);
                Assert.That(ReadNames(result, "DuplicateBands"), Is.Empty);
                Assert.That(ReadStrings(result, "Errors"), Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fixture.Root);
            }
        }

        [Test]
        public void Definition_RejectsMissingDuplicateAndOverlappingSortingRanges()
        {
            var fixture = CreateDefinitionFixture();
            try
            {
                var invalidBands = RequiredBands
                    .Where(name => name != "Sky")
                    .Concat(new[] { "FarWorld" })
                    .ToArray();
                ConfigureDefinition(fixture, invalidBands, overlapSortingRanges: true);
                var result = Invoke(fixture.Definition, "Validate");

                Assert.That(ReadNames(result, "MissingBands"),
                    Is.EqualTo(new[] { "Sky" }));
                Assert.That(ReadNames(result, "DuplicateBands"),
                    Is.EqualTo(new[] { "FarWorld" }));
                Assert.That(
                    ReadStrings(result, "Errors"),
                    Has.Some.Contains("sorting"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fixture.Root);
            }
        }

        [Test]
        public void Definition_RejectsMissingLightingAndAddressablesOwnership()
        {
            var fixture = CreateDefinitionFixture();
            try
            {
                var constructor = fixture.BindingType.GetConstructor(new[]
                {
                    fixture.BandType,
                    typeof(int),
                    typeof(int),
                    typeof(float),
                    typeof(Bounds),
                    typeof(int),
                    typeof(uint),
                    typeof(string),
                    typeof(string),
                });
                Assert.That(constructor, Is.Not.Null,
                    "LayerBinding2D must declare lighting and Addressables " +
                    "group ownership in addition to collision and key data.");
                var binding = constructor.Invoke(new object[]
                {
                    Enum.Parse(fixture.BandType, "Sky"),
                    0,
                    99,
                    0f,
                    new Bounds(Vector3.zero, new Vector3(40f, 18f, 1f)),
                    0,
                    0u,
                    string.Empty,
                    "jss.invalid.sky",
                });
                var bindings = Array.CreateInstance(fixture.BindingType, 1);
                bindings.SetValue(binding, 0);
                Invoke(fixture.Definition, "Configure", bindings);

                var errors = ReadStrings(
                    Invoke(fixture.Definition, "Validate"),
                    "Errors");
                Assert.That(errors, Has.Some.Contains("lighting"));
                Assert.That(errors, Has.Some.Contains("Addressables group"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fixture.Root);
            }
        }

        [Test]
        public void Project_UsesTheUrp2DRenderer()
        {
            var pipeline = GraphicsSettings.defaultRenderPipeline;
            Assert.That(pipeline, Is.Not.Null);
            var serializedPipeline = new SerializedObject(pipeline);
            var rendererDataList = serializedPipeline.FindProperty(
                "m_RendererDataList");
            Assert.That(rendererDataList, Is.Not.Null);
            Assert.That(rendererDataList.arraySize, Is.GreaterThan(0));

            var renderer = rendererDataList.GetArrayElementAtIndex(0)
                .objectReferenceValue;
            Assert.That(renderer, Is.Not.Null);
            Assert.That(
                renderer.GetType().FullName,
                Is.EqualTo("UnityEngine.Rendering.Universal.Renderer2DData"),
                "Gameplay scenes require URP's 2D Renderer, not the 3D " +
                "Universal Renderer.");
        }

        [Test]
        public void MirraProofScene_OwnsTheCompleteFinalProductionRoute()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath),
                Is.Not.Null,
                ScenePath);
            var previousSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(
                    ScenePath,
                    OpenSceneMode.Single);
                var root = scene.GetRootGameObjects()
                    .SingleOrDefault(item => item.name == "Mirra2DProof");
                Assert.That(root, Is.Not.Null);

                var bandRoot = FindDescendant(root.transform, "Bands");
                Assert.That(bandRoot, Is.Not.Null);
                Assert.That(
                    Enumerable.Range(0, bandRoot.childCount)
                        .Select(index => bandRoot.GetChild(index).name),
                    Is.EquivalentTo(RequiredBands));

                var definitionType = RequireRuntimeType(
                    "JustSomeStars.Runtime.Rendering2D.LayeredSceneDefinition");
                var definition = root.GetComponent(definitionType);
                Assert.That(definition, Is.Not.Null);
                var validation = Invoke(definition, "Validate");
                Assert.That(ReadStrings(validation, "Errors"), Is.Empty);

                var serializedDefinition = new SerializedObject(definition);
                var serializedBindings = serializedDefinition.FindProperty(
                    "bindings");
                Assert.That(serializedBindings, Is.Not.Null);
                Assert.That(serializedBindings.arraySize,
                    Is.EqualTo(RequiredBands.Length));
                var addressableSettings =
                    AddressableAssetSettingsDefaultObject.GetSettings(false);
                Assert.That(addressableSettings, Is.Not.Null);
                var declaredKeys = new HashSet<string>(StringComparer.Ordinal);
                for (var index = 0; index < serializedBindings.arraySize; index++)
                {
                    var binding = serializedBindings.GetArrayElementAtIndex(index);
                    var lightingMask = binding.FindPropertyRelative("lightingMask");
                    var addressGroup = binding.FindPropertyRelative(
                        "addressablesGroup");
                    var addressKey = binding.FindPropertyRelative("addressKey");
                    Assert.That(lightingMask, Is.Not.Null);
                    var bandName = binding.FindPropertyRelative("band")
                        .enumNames[binding.FindPropertyRelative("band").enumValueIndex];
                    if (bandName == "Hud")
                    {
                        Assert.That(lightingMask.uintValue, Is.Zero);
                    }
                    else
                    {
                        Assert.That(lightingMask.uintValue, Is.Not.Zero);
                    }
                    Assert.That(addressGroup, Is.Not.Null);
                    Assert.That(addressGroup.stringValue, Is.Not.Empty);
                    Assert.That(addressKey, Is.Not.Null);
                    Assert.That(declaredKeys.Add(addressKey.stringValue), Is.True,
                        "Every band requires one stable unique address key.");
                    var entry = addressableSettings.groups
                        .Where(group => group != null)
                        .SelectMany(group => group.entries)
                        .SingleOrDefault(candidate =>
                            string.Equals(
                                candidate.address,
                                addressKey.stringValue,
                                StringComparison.Ordinal));
                    Assert.That(entry, Is.Not.Null, addressKey.stringValue);
                    Assert.That(entry.parentGroup.Name,
                        Is.EqualTo(addressGroup.stringValue));
                }

                var parallaxType = RequireRuntimeType(
                    "JustSomeStars.Runtime.Rendering2D.ParallaxLayer2D");
                var rigType = RequireRuntimeType(
                    "JustSomeStars.Runtime.Rendering2D.ParallaxRig2D");
                foreach (var bandName in RequiredBands.Take(7))
                {
                    var band = FindDescendant(bandRoot, bandName);
                    Assert.That(band, Is.Not.Null, bandName);
                    Assert.That(band.GetComponent(parallaxType), Is.Not.Null,
                        bandName + " must own its parallax declaration.");
                }

                var camera = root.GetComponentInChildren<Camera>(true);
                Assert.That(camera, Is.Not.Null);
                Assert.That(camera.orthographic, Is.True);
                var compositionCameraType = RequireRuntimeType(
                    "JustSomeStars.Runtime.Player.CompositionCamera2D");
                var compositionCamera = camera.GetComponent(compositionCameraType);
                Assert.That(compositionCamera, Is.Not.Null);
                var serializedCamera = new SerializedObject(compositionCamera);
                var movementBounds = serializedCamera.FindProperty(
                    "movementBounds").boundsValue;
                Assert.That(movementBounds.extents.x,
                    Is.LessThanOrEqualTo(2.25f));
                Assert.That(movementBounds.extents.y,
                    Is.LessThanOrEqualTo(0.001f));
                const float targetAspect = 1280f / 720f;
                var cameraHalfWidth = camera.orthographicSize * targetAspect;

                var visualPaths = new HashSet<string>(StringComparer.Ordinal);
                var coveredRenderers = new List<SpriteRenderer>();
                foreach (var bandName in new[]
                {
                    "Sky",
                    "FarWorld",
                    "Atmosphere",
                    "Midground",
                    "Gameplay",
                    "Foreground",
                })
                {
                    var band = FindDescendant(bandRoot, bandName);
                    var parallax = band.GetComponent(parallaxType);
                    Assert.That(parallax, Is.Not.Null, bandName);
                    var serializedParallax = new SerializedObject(parallax);
                    if (serializedParallax.FindProperty("factor").floatValue >
                        0.0001f)
                    {
                        Assert.That(
                            serializedParallax.FindProperty("axisScale")
                                .vector2Value.y,
                            Is.EqualTo(0f).Within(0.0001f),
                            bandName + " must not move vertically away from " +
                            "its shared full-frame alpha registration.");
                    }
                    var renderers = band.GetComponentsInChildren<SpriteRenderer>(
                            includeInactive: true)
                        .Where(renderer => renderer.sprite != null)
                        .ToArray();
                    Assert.That(renderers, Is.Not.Empty,
                        bandName + " must own visible independent content.");
                    foreach (var renderer in renderers)
                    {
                        var path = AssetDatabase.GetAssetPath(renderer.sprite);
                        Assert.That(path, Is.Not.Empty, bandName);
                        Assert.That(visualPaths.Add(path), Is.True,
                            "The proof cannot alias one flattened image across " +
                            "multiple visual bands: " + path);
                        var importer = AssetImporter.GetAtPath(path) as
                            TextureImporter;
                        Assert.That(importer, Is.Not.Null, path);
                        var textureSettings = new TextureImporterSettings();
                        importer.ReadTextureSettings(textureSettings);
                        Assert.That(
                            textureSettings.spriteMeshType,
                            Is.EqualTo(SpriteMeshType.FullRect),
                            bandName + " needs full-rect overscan geometry so " +
                            "parallax cannot expose seams between alpha bands.");
                        var factor = serializedParallax.FindProperty(
                            "factor").floatValue;
                        var requiredHalfWidth = cameraHalfWidth +
                            movementBounds.extents.x * (1f - factor);
                        Assert.That(
                            renderer.sprite.bounds.extents.x *
                            Mathf.Abs(renderer.transform.lossyScale.x),
                            Is.GreaterThanOrEqualTo(requiredHalfWidth + 0.1f),
                            bandName + " has no horizontal travel overscan.");
                        if (bandName == "Sky")
                        {
                            AssertSkyOnlyCoverage(path);
                        }
                        else
                        {
                            AssertSemanticCutout(path, bandName);
                            if (bandName == "Gameplay" ||
                                bandName == "Foreground")
                            {
                                AssertPaintedSemanticOverscan(path, bandName);
                            }
                        }

                        coveredRenderers.Add(renderer);
                    }
                }

                var gameplay = FindDescendant(bandRoot, "Gameplay");
                var gameplayParallax = gameplay.GetComponent(parallaxType);
                Assert.That(
                    new SerializedObject(gameplayParallax)
                        .FindProperty("factor").floatValue,
                    Is.EqualTo(0f).Within(0.0001f));
                Assert.That(
                    gameplay.GetComponentsInChildren<SpriteRenderer>(true)
                        .Single().sprite.name,
                    Is.EqualTo("MirraGameplayFinal"));

                var rig = root.GetComponent(rigType);
                Assert.That(rig, Is.Not.Null);
                Invoke(rig, "CaptureOrigins");
                camera.transform.position = new Vector3(
                    movementBounds.max.x,
                    movementBounds.center.y,
                    camera.transform.position.z);
                Invoke(rig, "ApplyNow");
                var cameraLeft = camera.transform.position.x - cameraHalfWidth;
                var cameraRight = camera.transform.position.x + cameraHalfWidth;
                foreach (var renderer in coveredRenderers)
                {
                    Assert.That(renderer.bounds.min.x,
                        Is.LessThanOrEqualTo(cameraLeft + 0.001f),
                        renderer.name + " exposes the left camera edge.");
                    Assert.That(renderer.bounds.max.x,
                        Is.GreaterThanOrEqualTo(cameraRight - 0.001f),
                        renderer.name + " exposes the right camera edge.");
                }

                Assert.That(gameplay.transform.position, Is.EqualTo(Vector3.zero),
                    "Rendered walkable terrain and collision must remain in the " +
                    "factor-zero Gameplay band during camera travel.");

                AssertFinalActor(
                    root.transform,
                    "Captain",
                    "JustSomeStars.Runtime.Animation2D.LayeredCharacterRenderer");
                AssertFinalActor(
                    root.transform,
                    "Mira",
                    "JustSomeStars.Runtime.Animation2D.MirraProofActorPresenter");
                AssertFinalActor(
                    root.transform,
                    "Ori",
                    "JustSomeStars.Runtime.Animation2D.MirraProofActorPresenter");
                Assert.That(FindDescendant(root.transform, "ClimbObstacle"),
                    Is.Not.Null);
                Assert.That(FindDescendant(root.transform, "SignalConsole"),
                    Is.Not.Null);
                Assert.That(FindDescendant(root.transform, "TouchMove"),
                    Is.Not.Null);
                Assert.That(FindDescendant(root.transform, "TouchJump"),
                    Is.Not.Null);
                Assert.That(FindDescendant(root.transform, "TouchInteract"),
                    Is.Not.Null);
                AssertInputSemantics(root.transform);

                var interaction = FindDescendant(
                    root.transform,
                    "SignalConsole");
                var obstacle = FindDescendant(
                    root.transform,
                    "ClimbObstacle");
                var interactionType = RequireRuntimeType(
                    "JustSomeStars.Runtime.Player.SurfaceInteractionProbe2D");
                Assert.That(interaction.GetComponent(interactionType), Is.Not.Null,
                    "The visible interaction target must have an observable " +
                    "Primary-action response in the Stage 1 demo.");
                var interactionTrigger = interaction.GetComponent<CircleCollider2D>();
                var obstacleCollider = obstacle.GetComponent<BoxCollider2D>();
                Assert.That(interactionTrigger, Is.Not.Null);
                Assert.That(obstacleCollider, Is.Not.Null);
                Assert.That(
                    interactionTrigger.bounds.max.x,
                    Is.LessThan(obstacleCollider.bounds.min.x),
                    "The contextual interaction must be reachable from spawn " +
                    "before the solid climb obstacle blocks the approach route.");

                Assert.That(
                    root.GetComponentsInChildren<Rigidbody>(true),
                    Is.Empty,
                    "The proof must not restore 3D character physics.");
                Assert.That(
                    root.GetComponentsInChildren<Collider>(true),
                    Is.Empty,
                    "Painted silhouettes never own 3D collision.");
                Assert.That(
                    root.GetComponentsInChildren<Rigidbody2D>(true),
                    Is.Not.Empty);
                Assert.That(
                    root.GetComponentsInChildren<Collider2D>(true),
                    Is.Not.Empty);
                Assert.That(
                    root.GetComponentsInChildren<ParticleSystem>(true),
                    Has.Length.EqualTo(1),
                    "The final proof owns one bounded signal-mote system.");

                var lifecycleType = RequireRuntimeType(
                    "JustSomeStars.Runtime.Player.SurfaceGameplayLifecycle2D");
                var lifecycles = root.GetComponentsInChildren(lifecycleType, true);
                Assert.That(lifecycles, Has.Length.EqualTo(1));
                var lifecycle = lifecycles[0];
                var serializedLifecycle = new SerializedObject(lifecycle);
                Assert.That(
                    serializedLifecycle.FindProperty("motor")?.objectReferenceValue,
                    Is.SameAs(FindDescendant(root.transform, "Captain")
                        .GetComponent(RequireRuntimeType(
                            "JustSomeStars.Runtime.Player.SurfaceMotor2D"))));
                Assert.That(
                    serializedLifecycle.FindProperty("compositionCamera")
                        ?.objectReferenceValue,
                    Is.SameAs(compositionCamera));
                Assert.That(
                    serializedLifecycle.FindProperty("targetBody")
                        ?.objectReferenceValue,
                    Is.SameAs(FindDescendant(root.transform, "Captain")
                        .GetComponent<Rigidbody2D>()));
            }
            finally
            {
                if (previousSetup.Length > 0 && previousSetup.All(item =>
                    !string.IsNullOrEmpty(item.path) &&
                    File.Exists(Path.GetFullPath(item.path))))
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                }
                else
                {
                    EditorSceneManager.NewScene(
                        NewSceneSetup.EmptyScene,
                        NewSceneMode.Single);
                }
            }
        }

        [Test]
        public async Task LocalDemoLauncher_BindsRealSurfaceServicesAndFinalProofShips()
        {
            Assert.That(
                EditorBuildSettings.scenes.Count(scene =>
                    scene.enabled && scene.path == ScenePath),
                Is.EqualTo(1),
                "The accepted Stage 5 proof must ship exactly once for device QA.");

            var launcherType = Type.GetType(
                "JustSomeStars.Editor.Stage1LocalDemoLauncher, " +
                "JustSomeStars.Editor");
            Assert.That(launcherType, Is.Not.Null,
                "The Editor-only Stage 1 launcher is missing.");
            Assert.That(
                launcherType.Assembly.GetName().Name,
                Is.EqualTo("JustSomeStars.Editor"));

            var runnerType = Type.GetType(
                "JustSomeStars.Editor.Stage1LocalDemoRunner, " +
                "JustSomeStars.Editor");
            Assert.That(runnerType, Is.Not.Null,
                "The Editor-only Stage 1 service runner is missing.");
            Assert.That(
                runnerType.GetField(
                    "RuntimeCaptureWidth",
                    BindingFlags.Static | BindingFlags.NonPublic)
                    ?.GetRawConstantValue(),
                Is.EqualTo(1280));
            Assert.That(
                runnerType.GetField(
                    "RuntimeCaptureHeight",
                    BindingFlags.Static | BindingFlags.NonPublic)
                    ?.GetRawConstantValue(),
                Is.EqualTo(720));

            var openScene = launcherType.GetMethod(
                "OpenProofSceneForLocalPlay",
                BindingFlags.Static | BindingFlags.NonPublic);
            var initialize = runnerType.GetMethod(
                "InitializeAsync",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var shutdown = runnerType.GetMethod(
                "ShutdownAsync",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var isReady = runnerType.GetProperty(
                "IsReady",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(openScene, Is.Not.Null);
            Assert.That(initialize, Is.Not.Null);
            Assert.That(shutdown, Is.Not.Null);
            Assert.That(isReady, Is.Not.Null);

            var sceneBytes = File.ReadAllBytes(Path.GetFullPath(ScenePath));
            var previousSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                openScene.Invoke(null, null);
                Assert.That(
                    EditorSceneManager.GetActiveScene().path,
                    Is.EqualTo(ScenePath));
                Assert.That(EditorSceneManager.GetActiveScene().isDirty, Is.False);

                var runner = Activator.CreateInstance(runnerType);
                await (Task)initialize.Invoke(runner, null);

                Assert.That((bool)isReady.GetValue(runner), Is.True);
                var lifecycleType = RequireRuntimeType(
                    "JustSomeStars.Runtime.Player.SurfaceGameplayLifecycle2D");
                var lifecycle = UnityEngine.Object.FindFirstObjectByType(
                    lifecycleType,
                    FindObjectsInactive.Include);
                Assert.That(lifecycle, Is.Not.Null);
                Assert.That(
                    lifecycleType.GetProperty("IsConfigured")?.GetValue(lifecycle),
                    Is.True);

                var dependencies = lifecycleType.GetProperty("Dependencies")
                    ?.GetValue(lifecycle);
                Assert.That(dependencies, Is.Not.Null);
                Assert.That(
                    dependencies.GetType().GetProperty("Modes")?.GetValue(dependencies)
                        ?.GetType().GetProperty("CurrentMode")?.GetValue(
                            dependencies.GetType().GetProperty("Modes")
                                ?.GetValue(dependencies))?.ToString(),
                    Is.EqualTo("Surface"));
                Assert.That(
                    dependencies.GetType().GetProperty("Input")?.GetValue(dependencies)
                        ?.GetType().GetProperty("ActiveGameplayMode")?.GetValue(
                            dependencies.GetType().GetProperty("Input")
                                ?.GetValue(dependencies))?.ToString(),
                    Is.EqualTo("Surface"));

                await (Task)shutdown.Invoke(runner, null);
                Assert.That((bool)isReady.GetValue(runner), Is.False);
                Assert.That(
                    lifecycleType.GetProperty("IsConfigured")?.GetValue(lifecycle),
                    Is.False);
                Assert.That(EditorSceneManager.GetActiveScene().isDirty, Is.False);
            }
            finally
            {
                if (previousSetup.Length > 0 && previousSetup.All(item =>
                    !string.IsNullOrEmpty(item.path) &&
                    File.Exists(Path.GetFullPath(item.path))))
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                }
                else
                {
                    EditorSceneManager.NewScene(
                        NewSceneSetup.EmptyScene,
                        NewSceneMode.Single);
                }

                CollectionAssert.AreEqual(
                    sceneBytes,
                    File.ReadAllBytes(Path.GetFullPath(ScenePath)),
                    "The Editor-only launcher must never rewrite the proof scene.");
            }
        }

        private static DefinitionFixture CreateDefinitionFixture()
        {
            var root = new GameObject("LayerDefinitionFixture");
            var definitionType = RequireRuntimeType(
                "JustSomeStars.Runtime.Rendering2D.LayeredSceneDefinition");
            var bindingType = RequireRuntimeType(
                "JustSomeStars.Runtime.Rendering2D.LayerBinding2D");
            var bandType = RequireRuntimeType(
                "JustSomeStars.Runtime.Rendering2D.LayerBand");
            return new DefinitionFixture(
                root,
                root.AddComponent(definitionType),
                bindingType,
                bandType);
        }

        private static void ConfigureDefinition(
            DefinitionFixture fixture,
            IReadOnlyList<string> bandNames,
            bool overlapSortingRanges = false)
        {
            var bindings = Array.CreateInstance(fixture.BindingType, bandNames.Count);
            for (var index = 0; index < bandNames.Count; index++)
            {
                var band = Enum.Parse(fixture.BandType, bandNames[index]);
                var minimum = overlapSortingRanges ? 0 : index * 100;
                var maximum = minimum + 99;
                var factor = index / 10f;
                var binding = Activator.CreateInstance(
                    fixture.BindingType,
                    band,
                    minimum,
                    maximum,
                    factor,
                    new Bounds(Vector3.zero, new Vector3(40f, 18f, 1f)),
                    1 << (index % 4),
                    1u << (index % 31),
                    "JSS Mirra Stage 1",
                    "jss.mirra.stage1." + bandNames[index].ToLowerInvariant());
                bindings.SetValue(binding, index);
            }

            Invoke(fixture.Definition, "Configure", bindings);
        }

        private static Type RequireRuntimeType(string fullName)
        {
            var type = Type.GetType(fullName + ", " + RuntimeAssembly);
            Assert.That(type, Is.Not.Null,
                $"Stage 1 runtime type is missing: {fullName}");
            return type;
        }

        private static object Invoke(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethods(
                    BindingFlags.Public | BindingFlags.Instance)
                .SingleOrDefault(candidate =>
                    candidate.Name == methodName &&
                    candidate.GetParameters().Length == args.Length);
            Assert.That(method, Is.Not.Null,
                target.GetType().FullName + "." + methodName);
            return method.Invoke(target, args);
        }

        private static string[] ReadNames(object target, string propertyName)
        {
            return ReadEnumerable(target, propertyName)
                .Select(value => value.ToString())
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static string[] ReadStrings(object target, string propertyName)
        {
            return ReadEnumerable(target, propertyName)
                .Select(value => value?.ToString() ?? string.Empty)
                .ToArray();
        }

        private static IEnumerable<object> ReadEnumerable(
            object target,
            string propertyName)
        {
            Assert.That(target, Is.Not.Null);
            var property = target.GetType().GetProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            var values = property.GetValue(target) as IEnumerable;
            Assert.That(values, Is.Not.Null, propertyName);
            return values.Cast<object>();
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (var index = 0; index < root.childCount; index++)
            {
                var match = FindDescendant(root.GetChild(index), name);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static void AssertSkyOnlyCoverage(string assetPath)
        {
            var texture = LoadSourceTexture(assetPath);
            try
            {
                var pixels = texture.GetPixels32();
                Assert.That(
                    pixels.Min(pixel => pixel.a),
                    Is.EqualTo(byte.MaxValue),
                    assetPath + " must be an opaque semantic sky card so no " +
                    "camera displacement can reveal the clear color.");

                const int comparisonSpan = 32;
                var landmarkEdges = 0;
                var comparisons = 0;
                for (var row = 0; row < texture.height; row++)
                {
                    var rowOffset = row * texture.width;
                    for (var column = comparisonSpan;
                        column < texture.width;
                        column++)
                    {
                        var current = pixels[rowOffset + column];
                        var previous = pixels[
                            rowOffset + column - comparisonSpan];
                        var maximumChannelDelta = Mathf.Max(
                            Mathf.Abs(current.r - previous.r),
                            Mathf.Abs(current.g - previous.g),
                            Mathf.Abs(current.b - previous.b));
                        if (maximumChannelDelta > 60)
                        {
                            landmarkEdges++;
                        }

                        comparisons++;
                    }
                }

                Assert.That(landmarkEdges,
                    Is.LessThan(comparisons / 200),
                    assetPath + " contains blurred whole-scene landmarks; " +
                    "Sky must be a low-frequency sky-only painting.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void AssertPaintedSemanticOverscan(
            string assetPath,
            string bandName)
        {
            const int authoredSourceWidth = 1672;
            var texture = LoadSourceTexture(assetPath);
            try
            {
                var pixels = texture.GetPixels32();
                var overscan = (texture.width - authoredSourceWidth) / 2;
                Assert.That(overscan, Is.GreaterThan(0), assetPath);
                foreach (var limits in new[]
                {
                    (Minimum: 0, Maximum: overscan),
                    (Minimum: texture.width - overscan,
                        Maximum: texture.width),
                })
                {
                    var visiblePairs = 0;
                    var paintedPairs = 0;
                    var visibleRowDeltas = new List<float>();
                    for (var row = 0; row < texture.height; row++)
                    {
                        var rowOffset = row * texture.width;
                        for (var column = limits.Minimum + 1;
                            column < limits.Maximum;
                            column++)
                        {
                            var current = pixels[rowOffset + column];
                            var previous = pixels[rowOffset + column - 1];
                            if (current.a <= 8 || previous.a <= 8)
                            {
                                continue;
                            }

                            visiblePairs++;
                            if (Mathf.Max(
                                    Mathf.Abs(current.r - previous.r),
                                    Mathf.Abs(current.g - previous.g),
                                    Mathf.Abs(current.b - previous.b)) >= 1)
                            {
                                paintedPairs++;
                            }
                        }

                        if (row == 0)
                        {
                            continue;
                        }

                        var previousRowOffset = rowOffset - texture.width;
                        var visibleRowPixels = 0;
                        var rowDelta = 0f;
                        for (var column = limits.Minimum;
                            column < limits.Maximum;
                            column++)
                        {
                            var current = pixels[rowOffset + column];
                            var previous = pixels[previousRowOffset + column];
                            if (current.a <= 8 || previous.a <= 8)
                            {
                                continue;
                            }

                            visibleRowPixels++;
                            rowDelta += Mathf.Max(
                                Mathf.Abs(current.r - previous.r),
                                Mathf.Abs(current.g - previous.g),
                                Mathf.Abs(current.b - previous.b));
                        }

                        if (visibleRowPixels > 50)
                        {
                            visibleRowDeltas.Add(rowDelta / visibleRowPixels);
                        }
                    }

                    Assert.That(visiblePairs,
                        Is.GreaterThan(texture.height * 10),
                        bandName + " has no visible travel overscan.");
                    Assert.That(paintedPairs,
                        Is.GreaterThan(visiblePairs / 100),
                        bandName + " travel overscan is a row-stretched edge, " +
                        "not semantic painted terrain.");
                    Assert.That(visibleRowDeltas.Count,
                        Is.GreaterThan(texture.height / 5),
                        bandName + " has too little visible overscan for " +
                        "painterly edge-quality analysis.");
                    visibleRowDeltas.Sort();
                    Assert.That(
                        visibleRowDeltas[visibleRowDeltas.Count / 2],
                        Is.GreaterThanOrEqualTo(1.5f),
                        bandName + " travel overscan is dominated by smooth " +
                        "scanline bands instead of painterly terrain detail.");
                }

                foreach (var boundary in new[]
                {
                    (OverscanColumn: overscan - 1,
                        AuthoredColumn: overscan),
                    (OverscanColumn: texture.width - overscan,
                        AuthoredColumn: texture.width - overscan - 1),
                })
                {
                    var discontinuities = 0;
                    for (var row = 0; row < texture.height; row++)
                    {
                        var offset = row * texture.width;
                        var overscanPixel = pixels[
                            offset + boundary.OverscanColumn];
                        var authoredPixel = pixels[
                            offset + boundary.AuthoredColumn];
                        if (Mathf.Max(
                                Mathf.Abs(
                                    overscanPixel.r - authoredPixel.r),
                                Mathf.Abs(
                                    overscanPixel.g - authoredPixel.g),
                                Mathf.Abs(
                                    overscanPixel.b - authoredPixel.b),
                                Mathf.Abs(
                                    overscanPixel.a - authoredPixel.a)) > 24)
                        {
                            discontinuities++;
                        }
                    }

                    Assert.That(discontinuities,
                        Is.LessThan(texture.height / 10),
                        bandName + " has an abrupt vertical join between its " +
                        "authored frame and travel overscan.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void AssertSemanticCutout(
            string assetPath,
            string bandName)
        {
            var texture = LoadSourceTexture(assetPath);
            try
            {
                var pixels = texture.GetPixels32();
                var visible = pixels.Count(pixel => pixel.a > 8);
                var transparent = pixels.Length - visible;
                var feathered = pixels.Count(pixel =>
                    pixel.a > 8 && pixel.a < 247);
                Assert.That(visible,
                    Is.GreaterThan(pixels.Length / 100),
                    bandName + " has no visible semantic content.");
                Assert.That(transparent,
                    Is.GreaterThan(pixels.Length / 100),
                    bandName + " is a flattened opaque composition.");
                Assert.That(feathered,
                    Is.GreaterThan(pixels.Length / 500),
                    bandName + " needs a painterly feathered silhouette, not " +
                    "a hard polygon cut.");

                if (bandName == "FarWorld" || bandName == "Midground")
                {
                    const int authoredSourceWidth = 1672;
                    const int minimumBlendWidth = 256;
                    var overscan = (texture.width - authoredSourceWidth) / 2;
                    var outerVisible = 0;
                    for (var row = 0; row < texture.height; row++)
                    {
                        var rowOffset = row * texture.width;
                        for (var column = 0; column < overscan; column++)
                        {
                            if (pixels[rowOffset + column].a > 8 ||
                                pixels[rowOffset + texture.width - 1 - column].a >
                                8)
                            {
                                outerVisible++;
                            }
                        }
                    }

                    Assert.That(outerVisible, Is.Zero,
                        bandName + " cannot mirror landmark silhouettes into " +
                        "its geometric travel overscan.");

                    for (var distance = 0;
                        distance <= minimumBlendWidth;
                        distance++)
                    {
                        var normalized = distance /
                            (float)minimumBlendWidth;
                        var maximumAllowedAlpha = Mathf.CeilToInt(
                            byte.MaxValue * normalized * normalized) + 8;
                        var leftColumn = overscan + distance;
                        var rightColumn = texture.width - overscan - 1 -
                            distance;
                        var leftMaximum = 0;
                        var rightMaximum = 0;
                        for (var row = 0; row < texture.height; row++)
                        {
                            var rowOffset = row * texture.width;
                            leftMaximum = Mathf.Max(
                                leftMaximum,
                                pixels[rowOffset + leftColumn].a);
                            rightMaximum = Mathf.Max(
                                rightMaximum,
                                pixels[rowOffset + rightColumn].a);
                        }

                        Assert.That(leftMaximum,
                            Is.LessThanOrEqualTo(maximumAllowedAlpha),
                            bandName + " has an abrupt left overscan seam at " +
                            "source column " + leftColumn + ".");
                        Assert.That(rightMaximum,
                            Is.LessThanOrEqualTo(maximumAllowedAlpha),
                            bandName + " has an abrupt right overscan seam at " +
                            "source column " + rightColumn + ".");
                    }
                }

                var mixedRows = 0;
                for (var row = 0; row < texture.height; row++)
                {
                    var hasVisible = false;
                    var hasTransparent = false;
                    var offset = row * texture.width;
                    for (var column = 0; column < texture.width; column++)
                    {
                        if (pixels[offset + column].a > 8)
                        {
                            hasVisible = true;
                        }
                        else
                        {
                            hasTransparent = true;
                        }

                        if (hasVisible && hasTransparent)
                        {
                            mixedRows++;
                            break;
                        }
                    }
                }

                Assert.That(mixedRows,
                    Is.GreaterThan(texture.height / 20),
                    bandName + " is only a horizontal slice, not a semantic " +
                    "depth cutout.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static Texture2D LoadSourceTexture(string assetPath)
        {
            var absolutePath = Path.GetFullPath(assetPath);
            Assert.That(File.Exists(absolutePath), Is.True, assetPath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.That(
                texture.LoadImage(File.ReadAllBytes(absolutePath), false),
                Is.True,
                assetPath);
            return texture;
        }

        private static void AssertProofActor(
            Transform root,
            string objectName,
            string expectedSpritePath)
        {
            var actor = FindDescendant(root, objectName);
            Assert.That(actor, Is.Not.Null, objectName);
            var renderer = actor.GetComponent<SpriteRenderer>();
            Assert.That(renderer, Is.Not.Null, objectName);
            Assert.That(renderer.sprite, Is.Not.Null, objectName);
            Assert.That(
                AssetDatabase.GetAssetPath(renderer.sprite),
                Is.EqualTo(expectedSpritePath),
                objectName + " must use its canonical 2.5D cutout.");
            Assert.That(renderer.sharedMaterial, Is.Not.Null, objectName);
            Assert.That(
                renderer.sharedMaterial.shader.name,
                Is.EqualTo("Universal Render Pipeline/2D/Sprite-Unlit-Default"),
                objectName + " must remain independent of scene-light setup.");
            var expectedMaterialPath =
                "Assets/_JustSomeStars/Art/Characters2D/Stage1/Materials/" +
                Path.GetFileNameWithoutExtension(expectedSpritePath) +
                "-Unlit.mat";
            Assert.That(
                AssetDatabase.GetAssetPath(renderer.sharedMaterial),
                Is.EqualTo(expectedMaterialPath),
                objectName + " requires a texture-pinned material so cutouts " +
                "cannot alias while the 2D renderer batches them.");
            Assert.That(
                AssetDatabase.GetAssetPath(renderer.sharedMaterial.mainTexture),
                Is.EqualTo(expectedSpritePath),
                objectName + " material must pin its own canonical texture.");
        }

        private static void AssertFinalActor(
            Transform root,
            string objectName,
            string requiredComponentName)
        {
            var actor = FindDescendant(root, objectName);
            Assert.That(actor, Is.Not.Null, objectName);
            Assert.That(
                actor.GetComponents<Component>().Any(component =>
                    component != null &&
                    component.GetType().FullName == requiredComponentName),
                Is.True,
                objectName + " is missing " + requiredComponentName + ".");
            Assert.That(
                actor.GetComponentsInChildren<SpriteRenderer>(true)
                    .Any(renderer => renderer.sprite != null),
                Is.True,
                objectName + " must resolve real sprite art.");
        }

        private static void AssertInputSemantics(Transform root)
        {
            var actions = InputSystem.actions;
            Assert.That(actions, Is.Not.Null);
            var surface = actions.FindActionMap("Surface", throwIfNotFound: true);
            Assert.That(
                surface.FindAction("Primary", throwIfNotFound: true).bindings
                    .Select(binding => binding.path),
                Is.EquivalentTo(new[]
                {
                    "<Keyboard>/e",
                    "<Gamepad>/buttonSouth",
                }),
                "Primary is the contextual interact action.");
            Assert.That(
                surface.FindAction("Secondary", throwIfNotFound: true).bindings
                    .Select(binding => binding.path),
                Is.EquivalentTo(new[]
                {
                    "<Keyboard>/space",
                    "<Keyboard>/leftShift",
                    "<Gamepad>/buttonWest",
                }),
                "Secondary owns jump and held jet assistance.");
            Assert.That(
                surface.FindAction("Lens", throwIfNotFound: true).bindings
                    .Select(binding => binding.path),
                Is.EquivalentTo(new[]
                {
                    "<Keyboard>/l",
                    "<Gamepad>/buttonNorth",
                }),
                "Lens must not share URP's development-player left-shoulder " +
                "debug-menu control.");

            Assert.That(
                ReadControlPath(FindDescendant(root, "TouchJump")),
                Is.EqualTo("<Gamepad>/buttonWest"));
            Assert.That(
                ReadControlPath(FindDescendant(root, "TouchInteract")),
                Is.EqualTo("<Gamepad>/buttonSouth"));
            Assert.That(
                ReadControlPath(FindDescendant(root, "TouchLens")),
                Is.EqualTo("<Gamepad>/buttonNorth"));
        }

        private static string ReadControlPath(Transform control)
        {
            Assert.That(control, Is.Not.Null);
            var type = Type.GetType(
                "UnityEngine.InputSystem.OnScreen.OnScreenButton, Unity.InputSystem");
            Assert.That(type, Is.Not.Null);
            var button = control.GetComponent(type);
            Assert.That(button, Is.Not.Null, control.name);
            var property = type.GetProperty("controlPath");
            Assert.That(property, Is.Not.Null);
            return property.GetValue(button)?.ToString();
        }

        private readonly struct DefinitionFixture
        {
            public DefinitionFixture(
                GameObject root,
                Component definition,
                Type bindingType,
                Type bandType)
            {
                Root = root;
                Definition = definition;
                BindingType = bindingType;
                BandType = bandType;
            }

            public GameObject Root { get; }
            public Component Definition { get; }
            public Type BindingType { get; }
            public Type BandType { get; }
        }
    }
}
