using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using JustSomeStars.Runtime.Animation2D;
using JustSomeStars.Runtime.Cosmetics;
using JustSomeStars.Runtime.Player;
using JustSomeStars.Runtime.Rendering2D;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class Mirra2DAssetValidationTests
    {
        private const string ScenePath =
            "Assets/_JustSomeStars/Scenes/Benchmarks/Mirra2DProof.unity";
        private const string DefinitionPath =
            "Assets/_JustSomeStars/Content/Scenes/Mirra2DProof.asset";
        private const string FinalArtRoot =
            "Assets/_JustSomeStars/Art/2D/Environments/Mirra/";
        private const string ApprovedTargetPath =
            "outputs/just-some-stars-2.5d-gameplay-target-v1.png";
        private const string ApprovedTargetSha256 =
            "72644970448effd81177222e0aa23ae8a23f9b733077dab6e27e9ca765f5eaed";

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
        public void ApprovedTargetAndFinalMirraAssets_AreOwnedAndHashLocked()
        {
            Assert.That(File.Exists(ApprovedTargetPath), Is.True);
            Assert.That(Sha256(ApprovedTargetPath), Is.EqualTo(ApprovedTargetSha256));
            Assert.That(
                EditorBuildSettings.scenes.Any(scene =>
                    scene.enabled && scene.path == ScenePath),
                Is.True,
                "The final internal APK must contain the playable Mirra proof scene.");

            var requiredAssets = new[]
            {
                "Layers/MirraSkyFinal.png",
                "Layers/MirraFarWorldFinal.png",
                "Layers/MirraAtmosphereFinal.png",
                "Layers/MirraMidgroundFinal.png",
                "Layers/MirraGameplayFinal.png",
                "Layers/MirraForegroundFinal.png",
                "Masks/MirraGameplayNormal.png",
                "Masks/MirraSignalEmission.png",
                "VFX/MirraSignalMote.png",
            };
            foreach (var relativePath in requiredAssets)
            {
                var path = FinalArtRoot + relativePath;
                Assert.That(AssetDatabase.LoadMainAssetAtPath(path), Is.Not.Null, path);
            }
        }

        [Test]
        public void MirraDefinition_ValidatesEightBandsActorsAndRouteWithoutModels()
        {
            var definition = AssetDatabase.LoadMainAssetAtPath(DefinitionPath);
            Assert.That(definition, Is.Not.Null, DefinitionPath);
            Assert.That(
                definition.GetType().FullName,
                Is.EqualTo(
                    "JustSomeStars.Runtime.Rendering2D.Mirra2DProofDefinition"));

            var validation = Invoke(definition, "Validate");
            Assert.That(ReadStrings(validation, "Errors"), Is.Empty);
            Assert.That(
                ReadEnumerable(definition, "Bands")
                    .Select(item => ReadProperty(item, "Band").ToString()),
                Is.EquivalentTo(RequiredBands));
            Assert.That(ReadProperty(definition, "CaptainSpriteSet"), Is.Not.Null);
            Assert.That(ReadProperty(definition, "CompanionSpriteSet"), Is.Not.Null);
            Assert.That(ReadProperty(definition, "OriSpriteSet"), Is.Not.Null);
            Assert.That(
                ReadEnumerable(definition, "CharacterModelReferences"),
                Is.Empty);
            Assert.That(
                ReadProperty(definition, "InteractionId")?.ToString(),
                Is.EqualTo("mirra.signal-console"));
            Assert.That(
                ReadProperty(definition, "LensTargetId")?.ToString(),
                Is.EqualTo("mirra.signal-spire"));
            Assert.That(
                (float)ReadProperty(definition, "RecoveryThreshold"),
                Is.LessThan(-4f));
        }

        [Test]
        public void MirraScene_UsesFinalOwnedLayersAnimatedActorsAndBoundedRecovery()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath),
                Is.Not.Null);
            var priorSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var root = scene.GetRootGameObjects()
                    .Single(item => item.name == "Mirra2DProof");
                var bandRoot = FindDescendant(root.transform, "Bands");
                Assert.That(
                    Enumerable.Range(0, bandRoot.childCount)
                        .Select(index => bandRoot.GetChild(index).name),
                    Is.EquivalentTo(RequiredBands));

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
                    var renderers = band.GetComponentsInChildren<SpriteRenderer>(true)
                        .Where(renderer => renderer.sprite != null)
                        .ToArray();
                    Assert.That(renderers, Is.Not.Empty, bandName);
                    Assert.That(
                        renderers.Select(renderer =>
                            AssetDatabase.GetAssetPath(renderer.sprite)),
                        Has.All.StartsWith(FinalArtRoot),
                        bandName + " still uses temporary Stage 1 art.");
                }

                var allSpritePaths = root.GetComponentsInChildren<SpriteRenderer>(true)
                    .Where(renderer => renderer.sprite != null)
                    .Select(renderer => AssetDatabase.GetAssetPath(renderer.sprite))
                    .ToArray();
                Assert.That(
                    allSpritePaths,
                    Has.None.Contains("/Environment2D/Stage1/"));
                Assert.That(
                    allSpritePaths,
                    Has.None.Contains("/Characters2D/Stage1/"));
                AssertActor(root.transform, "Captain", "LayeredCharacterRenderer");
                AssertActor(root.transform, "Mira", "MirraProofActorPresenter");
                AssertActor(root.transform, "Ori", "MirraProofActorPresenter");
                var layeredDefinition = root.GetComponent<LayeredSceneDefinition>();
                Assert.That(layeredDefinition, Is.Not.Null);
                foreach (var binding in layeredDefinition.Bindings)
                {
                    if (binding.Band == LayerBand.Hud)
                    {
                        continue;
                    }
                    var band = FindDescendant(bandRoot, binding.Band.ToString());
                    var parallax = band.GetComponent<ParallaxLayer2D>();
                    Assert.That(parallax, Is.Not.Null, binding.Band.ToString());
                    Assert.That(
                        parallax.Factor,
                        Is.EqualTo(binding.ParallaxFactor).Within(0.0001f),
                        binding.Band + " runtime parallax drifted from its declaration.");
                }
                foreach (var actorName in new[] { "Mira", "Ori" })
                {
                    var actor = FindDescendant(root.transform, actorName);
                    var actorRenderer = actor.GetComponent<SpriteRenderer>();
                    Assert.That(actorRenderer.sharedMaterial, Is.Not.Null, actorName);
                    Assert.That(
                        actorRenderer.sharedMaterial.shader.name,
                        Is.Not.EqualTo("Hidden/InternalErrorShader"),
                        actorName + " must use a URP-compatible sprite material.");
                    Assert.That(
                        FindDescendant(actor, "ContactShadow"),
                        Is.Not.Null,
                        actorName + " must be grounded by an owned 2D contact shadow.");
                }
                Assert.That(
                    FindDescendant(
                        FindDescendant(root.transform, "Captain"),
                        "ContactShadow"),
                    Is.Not.Null,
                    "Captain must be grounded by an owned 2D contact shadow.");
                var lens = FindDescendant(root.transform, "TouchLens") as RectTransform;
                Assert.That(lens, Is.Not.Null);
                Assert.That(lens.anchorMin, Is.EqualTo(Vector2.right));
                Assert.That(lens.anchorMax, Is.EqualTo(Vector2.right));
                Assert.That(lens.anchoredPosition.y, Is.InRange(220f, 285f),
                    "Lens belongs in the target's lower-right control cluster.");
                Assert.That(
                    CountComponents(root, "DiscoveryLensTarget2D"),
                    Is.EqualTo(1));
                Assert.That(
                    CountComponents(root, "SurfaceRecovery2D"),
                    Is.EqualTo(1));
                Assert.That(
                    CountComponents(root, "SurfaceInteractionProbe2D"),
                    Is.EqualTo(1));
                Assert.That(root.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(root.GetComponentsInChildren<SkinnedMeshRenderer>(true),
                    Is.Empty);
            }
            finally
            {
                RestoreSetup(priorSetup);
            }
        }

        [Test]
        public void MirraScene_UsesBoundedLightsParticlesMasksAndColorGrading()
        {
            var priorSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var root = scene.GetRootGameObjects()
                    .Single(item => item.name == "Mirra2DProof");
                Assert.That(
                    CountComponents(root, "UnityEngine.Rendering.Universal.Light2D"),
                    Is.InRange(2, 4));
                Assert.That(
                    root.GetComponentsInChildren<ParticleSystem>(true).Length,
                    Is.InRange(1, 3));
                Assert.That(
                    CountComponents(root, "UnityEngine.Rendering.Volume"),
                    Is.EqualTo(1));

                var gameplay = FindDescendant(root.transform, "Gameplay")
                    .GetComponentsInChildren<SpriteRenderer>(true)
                    .Single(renderer => renderer.sprite != null);
                var importer = AssetImporter.GetAtPath(
                    AssetDatabase.GetAssetPath(gameplay.sprite)) as TextureImporter;
                Assert.That(importer, Is.Not.Null);
                Assert.That(
                    importer.secondarySpriteTextures
                        .Select(texture => texture.name),
                    Does.Contain("_NormalMap"));
                Assert.That(
                    importer.secondarySpriteTextures
                        .Select(texture => texture.name),
                    Does.Contain("_MaskTex"));
            }
            finally
            {
                RestoreSetup(priorSetup);
            }
        }

        [Test]
        public void MirraScene_CaptainStartsGroundedAndTraversesWithoutRecovery()
        {
            var priorSetup = EditorSceneManager.GetSceneManagerSetup();
            var priorSimulationMode = Physics2D.simulationMode;
            try
            {
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var root = scene.GetRootGameObjects()
                    .Single(item => item.name == "Mirra2DProof");
                var captain = FindDescendant(root.transform, "Captain");
                var route = FindDescendant(root.transform, "PlayableRoute")
                    .GetComponent<EdgeCollider2D>();
                var body = captain.GetComponent<Rigidbody2D>();
                var capsule = captain.GetComponent<CapsuleCollider2D>();
                var motor = captain.GetComponent<SurfaceMotor2D>();
                var recovery = captain.GetComponent<SurfaceRecovery2D>();

                Assert.That(route, Is.Not.Null);
                Assert.That(body, Is.Not.Null);
                Assert.That(capsule, Is.Not.Null);
                Assert.That(motor, Is.Not.Null);
                Assert.That(recovery, Is.Not.Null);

                Physics2D.simulationMode = SimulationMode2D.Script;
                Physics2D.SyncTransforms();
                var authoredStart = body.position;
                var routeY = SampleRouteHeight(route, authoredStart.x);
                Assert.That(
                    capsule.bounds.min.y,
                    Is.EqualTo(routeY).Within(0.08f),
                    "Captain's authored capsule must begin on, not through, " +
                    "the painted route.");
                var captainRenderer =
                    captain.GetComponent<LayeredCharacterRenderer>();

                var sourceTextures = new Dictionary<string, LoadedTexture>();
                try
                {
                    var renderedOffsets = new List<float>();
                    for (var frameIndex = 0; frameIndex < 8; frameIndex++)
                    {
                        for (var layerIndex = 0;
                             layerIndex < captainRenderer.LayerRenderers.Count;
                             layerIndex++)
                        {
                            captainRenderer.LayerRenderers[layerIndex].sprite =
                                captainRenderer.SpriteSet.FindClip(
                                    CaptainBodyFamily.Average,
                                    SpriteFacing.Right,
                                    (CaptainSpriteLayer)layerIndex,
                                    "run").Frames[frameIndex];
                        }
                        Physics2D.SyncTransforms();
                        renderedOffsets.Add(
                            OpaqueWorldMinY(
                                captainRenderer.LayerRenderers[
                                    (int)CaptainSpriteLayer.BodyBase],
                                sourceTextures) -
                            routeY);
                    }
                    Assert.That(
                        renderedOffsets,
                        Has.All.InRange(-0.08f, 0.08f),
                        "Every authored run frame needs one rendered boot sole " +
                        "on the collision/painted route. Offsets: " +
                        string.Join(", ", renderedOffsets.Select(offset =>
                            offset.ToString("0.000"))));
                }
                finally
                {
                    foreach (var texture in sourceTextures.Values)
                    {
                        UnityEngine.Object.DestroyImmediate(texture.Texture);
                    }
                    foreach (var layerRenderer in captainRenderer.LayerRenderers)
                    {
                        layerRenderer.sprite = null;
                    }
                }

                var contactShadow = FindDescendant(captain, "ContactShadow")
                    .GetComponent<SpriteRenderer>();
                Assert.That(contactShadow, Is.Not.Null);
                Assert.That(
                    contactShadow.transform.position.y,
                    Is.EqualTo(routeY).Within(0.06f),
                    "Captain's contact shadow must sit on the same painted " +
                    "route as the grounded body.");

                body.linearVelocity = Vector2.zero;
                motor.SetMoveInput(Vector2.right);
                var groundedSamples = 0;
                const float step = 1f / 60f;
                for (var frame = 0; frame < 72; frame++)
                {
                    motor.Simulate(step);
                    Assert.That(Physics2D.Simulate(step), Is.True);
                    recovery.EvaluateNow();
                    if (motor.IsGrounded)
                    {
                        groundedSamples++;
                    }
                }

                Assert.That(recovery.RecoveryCount, Is.Zero,
                    "A normal route traversal must not use fall recovery.");
                Assert.That(body.position.x,
                    Is.GreaterThan(authoredStart.x + 1.5f),
                    "Captain did not traverse the real route.");
                Assert.That(body.position.y,
                    Is.GreaterThan(recovery.FallThreshold + 2f));
                Assert.That(groundedSamples, Is.GreaterThanOrEqualTo(60),
                    "Captain lost ground contact during ordinary traversal.");
            }
            finally
            {
                Physics2D.simulationMode = priorSimulationMode;
                RestoreSetup(priorSetup);
            }
        }

        private static float SampleRouteHeight(EdgeCollider2D route, float worldX)
        {
            var points = route.points
                .Select(point => (Vector2)route.transform.TransformPoint(point))
                .ToArray();
            for (var index = 1; index < points.Length; index++)
            {
                var left = points[index - 1];
                var right = points[index];
                if (worldX < Mathf.Min(left.x, right.x) ||
                    worldX > Mathf.Max(left.x, right.x))
                {
                    continue;
                }

                var amount = Mathf.InverseLerp(left.x, right.x, worldX);
                return Mathf.Lerp(left.y, right.y, amount);
            }

            Assert.Fail("Captain starts outside the authored playable route.");
            return float.NaN;
        }

        private static float OpaqueWorldMinY(
            SpriteRenderer renderer,
            IDictionary<string, LoadedTexture> sourceTextures)
        {
            var sprite = renderer.sprite;
            Assert.That(sprite, Is.Not.Null, renderer.name);
            var path = AssetDatabase.GetAssetPath(sprite);
            Assert.That(File.Exists(path), Is.True, path);
            if (!sourceTextures.TryGetValue(path, out var source))
            {
                var texture = new Texture2D(
                    2,
                    2,
                    TextureFormat.RGBA32,
                    false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
                Assert.That(
                    texture.LoadImage(File.ReadAllBytes(path), false),
                    Is.True,
                    path);
                source = new LoadedTexture(
                    texture,
                    texture.width,
                    texture.height,
                    texture.GetPixels32());
                sourceTextures.Add(path, source);
            }

            var rect = sprite.rect;
            var minimumY = float.PositiveInfinity;
            var opaquePixels = 0;
            var minimumX = Mathf.FloorToInt(rect.xMin);
            var maximumX = Mathf.CeilToInt(rect.xMax);
            var minimumPixelY = Mathf.FloorToInt(rect.yMin);
            var maximumPixelY = Mathf.CeilToInt(rect.yMax);
            for (var pixelY = minimumPixelY;
                 pixelY < maximumPixelY;
                 pixelY++)
            {
                for (var pixelX = minimumX; pixelX < maximumX; pixelX++)
                {
                    var sourcePixelY = source.Height - 1 - pixelY;
                    var pixel = source.Pixels[
                        sourcePixelY * source.Width + pixelX];
                    if (pixel.a <= 16)
                    {
                        continue;
                    }
                    opaquePixels++;
                    var local = new Vector3(
                        (pixelX + 0.5f - rect.xMin - sprite.pivot.x) /
                        sprite.pixelsPerUnit,
                        (pixelY + 0.5f - rect.yMin - sprite.pivot.y) /
                        sprite.pixelsPerUnit,
                        0f);
                    if (renderer.flipX)
                    {
                        local.x = -local.x;
                    }
                    if (renderer.flipY)
                    {
                        local.y = -local.y;
                    }
                    minimumY = Mathf.Min(
                        minimumY,
                        renderer.transform.TransformPoint(local).y);
                }
            }
            Assert.That(opaquePixels, Is.GreaterThan(0), path);
            return minimumY;
        }

        private static void RestoreSetup(SceneSetup[] priorSetup)
        {
            if (priorSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(priorSetup);
                return;
            }

            EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
        }

        private static void AssertActor(
            Transform root,
            string name,
            string requiredComponentName)
        {
            var actor = FindDescendant(root, name);
            Assert.That(actor, Is.Not.Null, name);
            Assert.That(
                actor.GetComponents<Component>().Any(component =>
                    component != null &&
                    component.GetType().Name == requiredComponentName),
                Is.True,
                name + " is missing " + requiredComponentName + ".");
        }

        private static int CountComponents(GameObject root, string typeName)
        {
            return root.GetComponentsInChildren<Component>(true).Count(component =>
                component != null &&
                (component.GetType().Name == typeName ||
                 component.GetType().FullName == typeName));
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

        private static object Invoke(object target, string methodName)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, target.GetType().FullName + "." + methodName);
            return method.Invoke(target, Array.Empty<object>());
        }

        private static object ReadProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, target.GetType().FullName + "." + propertyName);
            return property.GetValue(target);
        }

        private static IEnumerable<object> ReadEnumerable(
            object target,
            string propertyName)
        {
            return ((IEnumerable)ReadProperty(target, propertyName)).Cast<object>();
        }

        private static string[] ReadStrings(object target, string propertyName)
        {
            return ReadEnumerable(target, propertyName)
                .Select(value => value?.ToString() ?? string.Empty)
                .ToArray();
        }

        private static string Sha256(string path)
        {
            using var stream = File.OpenRead(path);
            using var hash = SHA256.Create();
            return string.Concat(hash.ComputeHash(stream)
                .Select(value => value.ToString("x2")));
        }

        private sealed class LoadedTexture
        {
            public LoadedTexture(
                Texture2D texture,
                int width,
                int height,
                Color32[] pixels)
            {
                Texture = texture;
                Width = width;
                Height = height;
                Pixels = pixels;
            }

            public Texture2D Texture { get; }
            public int Width { get; }
            public int Height { get; }
            public Color32[] Pixels { get; }
        }
    }
}
