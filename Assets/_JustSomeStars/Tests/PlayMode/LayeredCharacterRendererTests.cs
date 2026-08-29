using System;
using System.Collections;
using System.Collections.Generic;
using JustSomeStars.Runtime.Animation2D;
using JustSomeStars.Runtime.Cosmetics;
using JustSomeStars.Runtime.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class LayeredCharacterRendererTests
    {
        private readonly List<UnityEngine.Object> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created)
            {
                UnityEngine.Object.DestroyImmediate(item);
            }
            created.Clear();
        }

        [UnityTest]
        public IEnumerator OneThroughFiveLayers_StaySynchronizedAndEmitEventsOnce()
        {
            for (var layerCount = 1; layerCount <= 5; layerCount++)
            {
                var fixture = CreateFixture(layerCount);
                var observed = new List<string>();
                fixture.Renderer.FrameEventEmitted += frameEvent =>
                    observed.Add(frameEvent.Id);

                fixture.Renderer.Play("run");
                fixture.Renderer.Advance(0.18f);

                Assert.That(fixture.Renderer.CurrentFrameIndex, Is.EqualTo(2));
                Assert.That(observed, Is.EqualTo(new[] { "step-left", "step-right" }));
                foreach (var layerRenderer in fixture.LayerRenderers)
                {
                    Assert.That(layerRenderer.sprite.name, Does.EndWith("-2"));
                }
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator FamilyFacingAndClipSwitch_AreAtomicWithoutMixedFrames()
        {
            var fixture = CreateFixture(5);
            fixture.Renderer.Play("idle");
            fixture.Renderer.Advance(0.09f);
            fixture.Renderer.ApplyLoadout(
                CaptainSpriteLoadout.CreateLaunchLook(CaptainBodyFamily.TallBroad),
                SpriteFacing.Left,
                "scan");

            Assert.That(
                fixture.Renderer.CurrentFamily,
                Is.EqualTo(CaptainBodyFamily.TallBroad));
            Assert.That(fixture.Renderer.CurrentFacing, Is.EqualTo(SpriteFacing.Left));
            Assert.That(fixture.Renderer.CurrentFrameIndex, Is.Zero);
            foreach (var layerRenderer in fixture.LayerRenderers)
            {
                StringAssert.Contains("tallbroad-left", layerRenderer.sprite.name);
                StringAssert.EndsWith("scan-0", layerRenderer.sprite.name);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ShippedCaptainAsset_AllFamiliesShareRouteAndLoadoutChangesPixels()
        {
            var handle = Addressables.LoadAssetAsync<CaptainSpriteSet>(
                "characters/captain/sprite-set");
            yield return handle;
            Assert.That(handle.Status, Is.EqualTo(AsyncOperationStatus.Succeeded));
            var realSet = handle.Result;
            Assert.That(realSet, Is.Not.Null);
            realSet.ValidateOrThrow();
            try
            {
                List<string> canonicalEvents = null;
                float? canonicalRouteDistance = null;
                float? canonicalRouteDuration = null;
                foreach (CaptainBodyFamily family in Enum.GetValues(
                             typeof(CaptainBodyFamily)))
                {
                    var root = new GameObject("RealCaptain-" + family);
                    created.Add(root);
                    var renderers = new SpriteRenderer[5];
                    for (var index = 0; index < renderers.Length; index++)
                    {
                        var child = new GameObject("Layer-" + index);
                        child.transform.SetParent(root.transform, false);
                        renderers[index] = child.AddComponent<SpriteRenderer>();
                    }
                    var renderer = root.AddComponent<LayeredCharacterRenderer>();
                    var loadout = CaptainSpriteLoadout.CreateLaunchLook(family);
                    renderer.Configure(
                        realSet,
                        loadout,
                        SpriteFacing.Right,
                        renderers);
                    renderer.enabled = false;
                    var observed = new List<string>();
                    renderer.FrameEventEmitted += frameEvent =>
                        observed.Add(frameEvent.Id);

                    renderer.Play("run");
                    renderer.Advance(0.68f);
                    renderer.Play("jump");
                    renderer.Advance(0.18f);
                    renderer.Play("interact");
                    renderer.Advance(0.34f);
                    Assert.That(
                        observed,
                        Is.EqualTo(new[]
                        {
                            "step-left", "step-right", "step-left", "step-right",
                            "step-left", "jump-audio", "jump-vfx",
                            "interact-commit",
                        }),
                        family.ToString());
                    if (canonicalEvents == null)
                    {
                        canonicalEvents = new List<string>(observed);
                    }
                    else
                    {
                        Assert.That(observed, Is.EqualTo(canonicalEvents));
                    }

                    renderer.Play("idle");
                    var supportsPixelEvidence =
                        SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
                    Color32[] sourcePixels = null;
                    Color32[] launchPixels = null;
                    if (supportsPixelEvidence)
                    {
                        sourcePixels = CaptureSourcePixels(renderers);
                        launchPixels = CapturePixels(renderers);
                        AssertPreservesSourceFidelity(
                            sourcePixels,
                            launchPixels,
                            family + " launch");
                    }
                    var launchBlock = new MaterialPropertyBlock();
                    renderers[(int)CaptainSpriteLayer.HeadHair]
                        .GetPropertyBlock(launchBlock);
                    var launchSkinColor = launchBlock.GetColor("_SkinColor");
                    var launchHairColor = launchBlock.GetColor("_HairColor");
                    var launchSuitColor = launchBlock.GetColor("_SuitColor");
                    var launchSignalColor = launchBlock.GetColor("_SignalColor");
                    var launchHairUv = launchBlock.GetVector("_HairShapesUv");
                    var launchHairPage = launchBlock.GetTexture(
                        "_HairShapesModule");
                    var customized = loadout
                        .WithPaletteSelections(
                            "skin-6",
                            "blue-black",
                            "deep-teal",
                            "active-cyan")
                        .WithOption(
                            CaptainCustomizationCategory.FacePresets,
                            "face-5")
                        .WithOption(
                            CaptainCustomizationCategory.EyeShapes,
                            "eye-shape-4")
                        .WithOption(
                            CaptainCustomizationCategory.IrisColors,
                            "deep-blue")
                        .WithOption(
                            CaptainCustomizationCategory.HairShapes,
                            "hair-shape-8")
                        .WithOption(
                            CaptainCustomizationCategory.SuitComponents,
                            "utility-belt")
                        .WithOption(
                            CaptainCustomizationCategory.Patches,
                            "patch-6")
                        .WithOption(
                            CaptainCustomizationCategory.Accessories,
                            "goggles")
                        .WithOption(
                            CaptainCustomizationCategory.Gloves,
                            "tactile-grip")
                        .WithOption(
                            CaptainCustomizationCategory.Boots,
                            "strap-utility")
                        .WithOption(
                            CaptainCustomizationCategory.Helmets,
                            "surveyor")
                        .WithOption(
                            CaptainCustomizationCategory.Backpacks,
                            "expedition-pack");
                    renderer.ApplyLoadout(
                        customized,
                        SpriteFacing.Right,
                        "idle");
                    if (supportsPixelEvidence)
                    {
                        var customizedPixels = CapturePixels(renderers);
                        AssertPreservesSourceFidelity(
                            sourcePixels,
                            customizedPixels,
                            family + " alternate");
                        var changedPixels = 0;
                        var launchVisible = 0;
                        var customizedVisible = 0;
                        var launchColored = 0;
                        var customizedColored = 0;
                        var launchNonBackground = 0;
                        var customizedNonBackground = 0;
                        var launchBackground = launchPixels[0];
                        var customizedBackground = customizedPixels[0];
                        for (var pixel = 0; pixel < launchPixels.Length; pixel++)
                        {
                            if (launchPixels[pixel].a > 16)
                            {
                                launchVisible++;
                            }
                            if (customizedPixels[pixel].a > 16)
                            {
                                customizedVisible++;
                            }
                            if (launchPixels[pixel].r + launchPixels[pixel].g +
                                launchPixels[pixel].b > 24)
                            {
                                launchColored++;
                            }
                            if (customizedPixels[pixel].r +
                                customizedPixels[pixel].g +
                                customizedPixels[pixel].b > 24)
                            {
                                customizedColored++;
                            }
                            if (ColorDistance(
                                    launchPixels[pixel], launchBackground) > 8)
                            {
                                launchNonBackground++;
                            }
                            if (ColorDistance(
                                    customizedPixels[pixel],
                                    customizedBackground) > 8)
                            {
                                customizedNonBackground++;
                            }
                            if (ColorDistance(
                                    launchPixels[pixel],
                                    customizedPixels[pixel]) > 24)
                            {
                                changedPixels++;
                            }
                        }
                        Assert.That(
                            changedPixels,
                            Is.GreaterThan(300),
                            $"{family}: launchVisible={launchVisible}, " +
                            $"customizedVisible={customizedVisible}, " +
                            $"launchColored={launchColored}, " +
                            $"customizedColored={customizedColored}, " +
                            $"launchNonBackground={launchNonBackground}, " +
                            $"customizedNonBackground={customizedNonBackground}, " +
                            $"background={launchBackground}");
                        Assert.That(
                            customizedVisible,
                            Is.GreaterThan(launchVisible * 0.85f));
                        Assert.That(
                            customizedVisible,
                            Is.LessThan(launchVisible * 1.35f));
                    }
                    var customizedBlock = new MaterialPropertyBlock();
                    renderers[(int)CaptainSpriteLayer.HeadHair]
                        .GetPropertyBlock(customizedBlock);
                    Assert.That(
                        customizedBlock.GetColor("_SkinColor"),
                        Is.Not.EqualTo(launchSkinColor));
                    Assert.That(
                        customizedBlock.GetColor("_HairColor"),
                        Is.Not.EqualTo(launchHairColor));
                    Assert.That(
                        customizedBlock.GetColor("_SuitColor"),
                        Is.Not.EqualTo(launchSuitColor));
                    Assert.That(
                        customizedBlock.GetColor("_SignalColor"),
                        Is.EqualTo(launchSignalColor));
                    Assert.That(
                        customizedBlock.GetTexture("_HairShapesModule"),
                        Is.SameAs(launchHairPage));
                    Assert.That(
                        customizedBlock.GetVector("_HairShapesUv"),
                        Is.Not.EqualTo(launchHairUv));
                    Assert.That(
                        customizedBlock.GetVector("_HairShapesUv"),
                        Is.EqualTo(new Vector4(0.25f, 0.5f, 0.75f, 0f)));
                    var rootAnchor = renderer.ResolveAnchorLocal("Root");
                    Assert.That(float.IsFinite(rootAnchor.x), Is.True);
                    Assert.That(float.IsFinite(rootAnchor.y), Is.True);

                    observed.Clear();
                    var body = root.AddComponent<Rigidbody2D>();
                    body.gravityScale = 0f;
                    body.freezeRotation = true;
                    var collider = root.AddComponent<CapsuleCollider2D>();
                    collider.size = new Vector2(0.63f, 1.395f);
                    var config = ScriptableObject.CreateInstance<SurfaceMotor2DConfig>();
                    created.Add(config);
                    var motor = root.AddComponent<SurfaceMotor2D>();
                    motor.Configure(body, collider, config);
                    renderer.Play("run");
                    const float fixedStep = 0.02f;
                    const int routeSteps = 75;
                    for (var step = 0; step < routeSteps; step++)
                    {
                        motor.SetMoveInput(Vector2.right);
                        motor.Simulate(fixedStep);
                        body.position += body.linearVelocity * fixedStep;
                        renderer.Advance(fixedStep);
                    }
                    renderer.Play("interact");
                    renderer.Advance(0.34f);
                    Assert.That(
                        observed.FindAll(value => value == "interact-commit").Count,
                        Is.EqualTo(1),
                        family.ToString());
                    var routeDistance = body.position.x;
                    var routeDuration = routeSteps * fixedStep;
                    Assert.That(routeDistance, Is.GreaterThan(6f));
                    if (canonicalRouteDistance.HasValue)
                    {
                        Assert.That(
                            routeDistance,
                            Is.EqualTo(canonicalRouteDistance.Value).Within(0.0001f),
                            family.ToString());
                        Assert.That(
                            routeDuration,
                            Is.EqualTo(canonicalRouteDuration.Value).Within(0.0001f),
                            family.ToString());
                    }
                    else
                    {
                        canonicalRouteDistance = routeDistance;
                        canonicalRouteDuration = routeDuration;
                    }
                    root.SetActive(false);
                    yield return null;
                }
            }
            finally
            {
                Addressables.Release(handle);
            }
        }

        private static Color32[] CapturePixels(SpriteRenderer[] renderers)
        {
            var cameraObject = new GameObject("CaptainPixelEvidenceCamera");
            var camera = cameraObject.AddComponent<Camera>();
            var target = new RenderTexture(256, 384, 24, RenderTextureFormat.ARGB32);
            var readback = new Texture2D(256, 384, TextureFormat.RGBA32, false);
            var prior = RenderTexture.active;
            try
            {
                target.Create();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.orthographic = true;
                camera.orthographicSize = 2.1f;
                camera.transform.position = new Vector3(0f, 0.85f, -10f);
                camera.targetTexture = target;
                foreach (var renderer in renderers)
                {
                    renderer.enabled = true;
                }
                RenderTexture.active = target;
                GL.Clear(true, true, Color.clear);
                camera.Render();
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0f, 0f, 256f, 384f), 0, 0);
                readback.Apply(false, false);
                return readback.GetPixels32();
            }
            finally
            {
                RenderTexture.active = prior;
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.DestroyImmediate(readback);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private Color32[] CaptureSourcePixels(SpriteRenderer[] renderers)
        {
            var sourceShader = Shader.Find("Sprites/Default");
            Assert.That(sourceShader, Is.Not.Null);
            var sourceMaterial = new Material(sourceShader);
            created.Add(sourceMaterial);
            var priorMaterials = new Material[renderers.Length];
            try
            {
                for (var index = 0; index < renderers.Length; index++)
                {
                    priorMaterials[index] = renderers[index].sharedMaterial;
                    renderers[index].sharedMaterial = sourceMaterial;
                }
                return CapturePixels(renderers);
            }
            finally
            {
                for (var index = 0; index < renderers.Length; index++)
                {
                    renderers[index].sharedMaterial = priorMaterials[index];
                }
            }
        }

        private static void AssertPreservesSourceFidelity(
            Color32[] source,
            Color32[] output,
            string context)
        {
            Assert.That(output.Length, Is.EqualTo(source.Length));
            var sourceVisible = 0;
            var overlap = 0;
            var sourceEdges = 0;
            var outputEdges = 0;
            long colorDistance = 0;
            var sourceColors = new HashSet<int>();
            var outputColors = new HashSet<int>();
            const int width = 256;
            for (var index = 0; index < source.Length; index++)
            {
                var sourcePixel = source[index];
                var outputPixel = output[index];
                if (sourcePixel.a <= 16)
                {
                    continue;
                }
                sourceVisible++;
                sourceColors.Add(QuantizedColor(sourcePixel));
                if (outputPixel.a > 16)
                {
                    overlap++;
                    outputColors.Add(QuantizedColor(outputPixel));
                    colorDistance += RgbDistance(sourcePixel, outputPixel);
                }
                if (index % width == width - 1 || index + width >= source.Length)
                {
                    continue;
                }
                var sourceRight = source[index + 1];
                var sourceUp = source[index + width];
                var outputRight = output[index + 1];
                var outputUp = output[index + width];
                if (sourceRight.a > 16 &&
                    RgbDistance(sourcePixel, sourceRight) > 20)
                {
                    sourceEdges++;
                }
                if (sourceUp.a > 16 &&
                    RgbDistance(sourcePixel, sourceUp) > 20)
                {
                    sourceEdges++;
                }
                if (outputPixel.a > 16 && outputRight.a > 16 &&
                    RgbDistance(outputPixel, outputRight) > 20)
                {
                    outputEdges++;
                }
                if (outputPixel.a > 16 && outputUp.a > 16 &&
                    RgbDistance(outputPixel, outputUp) > 20)
                {
                    outputEdges++;
                }
            }
            var meanDistance = overlap == 0
                ? float.PositiveInfinity
                : colorDistance / (float)overlap;
            Assert.That(
                overlap,
                Is.GreaterThanOrEqualTo(sourceVisible * 0.90f),
                context + " must retain the source silhouette.");
            Assert.That(
                meanDistance,
                Is.LessThanOrEqualTo(60f),
                context + " must retain source material detail; mean RGB delta=" +
                meanDistance.ToString("0.###"));
            Assert.That(
                outputEdges,
                Is.GreaterThanOrEqualTo(sourceEdges * 0.72f),
                context + " must retain source edge detail.");
            Assert.That(
                outputColors.Count,
                Is.GreaterThanOrEqualTo(sourceColors.Count * 0.60f),
                context + " must retain source color variation.");
        }

        private static int QuantizedColor(Color32 value)
        {
            return (value.r >> 4) << 8 |
                   (value.g >> 4) << 4 |
                   (value.b >> 4);
        }

        private static int RgbDistance(Color32 left, Color32 right)
        {
            return Math.Abs(left.r - right.r) +
                   Math.Abs(left.g - right.g) +
                   Math.Abs(left.b - right.b);
        }

        private static int ColorDistance(Color32 left, Color32 right)
        {
            return Math.Abs(left.r - right.r) +
                   Math.Abs(left.g - right.g) +
                   Math.Abs(left.b - right.b) +
                   Math.Abs(left.a - right.a);
        }

        private Fixture CreateFixture(int layerCount)
        {
            var spriteSet = ScriptableObject.CreateInstance<CaptainSpriteSet>();
            created.Add(spriteSet);
            var entries = new List<CaptainSpriteSetEntry>();
            foreach (CaptainBodyFamily family in Enum.GetValues(
                         typeof(CaptainBodyFamily)))
            {
                foreach (var facing in new[] { SpriteFacing.Right, SpriteFacing.Left })
                {
                    foreach (CaptainSpriteLayer layer in Enum.GetValues(
                                 typeof(CaptainSpriteLayer)))
                    {
                        entries.Add(new CaptainSpriteSetEntry(
                            family,
                            facing,
                            layer,
                            CreateLayerSet(family, facing, layer)));
                    }
                }
            }
            spriteSet.Configure(entries.ToArray());

            var root = new GameObject("LayeredCharacterRendererTests");
            created.Add(root);
            var layerRenderers = new SpriteRenderer[layerCount];
            for (var index = 0; index < layerCount; index++)
            {
                var child = new GameObject($"Layer-{index}");
                child.transform.SetParent(root.transform, false);
                layerRenderers[index] = child.AddComponent<SpriteRenderer>();
            }
            var renderer = root.AddComponent<LayeredCharacterRenderer>();
            renderer.Configure(
                spriteSet,
                CaptainSpriteLoadout.CreateLaunchLook(
                    CaptainBodyFamily.Average,
                    layerCount),
                SpriteFacing.Right,
                layerRenderers);
            return new Fixture(renderer, layerRenderers);
        }

        private CharacterSpriteSet CreateLayerSet(
            CaptainBodyFamily family,
            SpriteFacing facing,
            CaptainSpriteLayer layer)
        {
            var clips = new[]
            {
                CreateClip(family, facing, layer, "idle", 4, false),
                CreateClip(family, facing, layer, "run", 8, true),
                CreateClip(family, facing, layer, "turn", 4, false),
                CreateClip(family, facing, layer, "jump", 6, false),
                CreateClip(family, facing, layer, "land", 4, false),
                CreateClip(family, facing, layer, "climb", 8, false),
                CreateClip(family, facing, layer, "scan", 8, false),
                CreateClip(family, facing, layer, "interact", 6, false),
            };
            var set = ScriptableObject.CreateInstance<CharacterSpriteSet>();
            created.Add(set);
            set.Configure(
                $"captain-{family.ToString().ToLowerInvariant()}-" +
                $"{facing.ToString().ToLowerInvariant()}-{LayerId(layer)}",
                clips);
            return set;
        }

        private SpriteAnimationClipDefinition CreateClip(
            CaptainBodyFamily family,
            SpriteFacing facing,
            CaptainSpriteLayer layer,
            string motion,
            int frameCount,
            bool runEvents)
        {
            var frames = new Sprite[frameCount];
            var durations = new float[frameCount];
            for (var index = 0; index < frameCount; index++)
            {
                var texture = new Texture2D(8, 12, TextureFormat.RGBA32, false);
                created.Add(texture);
                var sprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, 8, 12),
                    new Vector2(0.5f, 0.09375f),
                    100f);
                sprite.name =
                    $"captain-{family.ToString().ToLowerInvariant()}-" +
                    $"{facing.ToString().ToLowerInvariant()}-{LayerId(layer)}-" +
                    $"{motion}-{index}";
                created.Add(sprite);
                frames[index] = sprite;
                durations[index] = 1f / 12f;
            }
            var events = runEvents
                ? new[]
                {
                    new SpriteFrameEvent(0, SpriteFrameEventKind.FootContact, "step-left"),
                    new SpriteFrameEvent(2, SpriteFrameEventKind.FootContact, "step-right"),
                    new SpriteFrameEvent(4, SpriteFrameEventKind.FootContact, "step-left"),
                    new SpriteFrameEvent(6, SpriteFrameEventKind.FootContact, "step-right"),
                }
                : Array.Empty<SpriteFrameEvent>();
            var clip = ScriptableObject.CreateInstance<SpriteAnimationClipDefinition>();
            created.Add(clip);
            clip.Configure(
                $"captain.{family.ToString().ToLowerInvariant()}." +
                $"{LayerId(layer)}.{motion}." +
                facing.ToString().ToLowerInvariant(),
                facing,
                motion == "idle" || motion == "run"
                    ? SpriteAnimationLoopMode.Loop
                    : SpriteAnimationLoopMode.Once,
                frames,
                durations,
                events);
            return clip;
        }

        private static string LayerId(CaptainSpriteLayer layer)
        {
            return layer switch
            {
                CaptainSpriteLayer.BodyBase => "body-base",
                CaptainSpriteLayer.HeadHair => "head-hair",
                CaptainSpriteLayer.SilhouetteCostume => "silhouette-costume",
                CaptainSpriteLayer.BackpackEquipment => "backpack-equipment",
                CaptainSpriteLayer.ForegroundHandTool => "foreground-hand-tool",
                _ => throw new ArgumentOutOfRangeException(nameof(layer)),
            };
        }

        private readonly struct Fixture
        {
            public Fixture(
                LayeredCharacterRenderer renderer,
                SpriteRenderer[] layerRenderers)
            {
                Renderer = renderer;
                LayerRenderers = layerRenderers;
            }

            public LayeredCharacterRenderer Renderer { get; }
            public SpriteRenderer[] LayerRenderers { get; }
        }
    }
}
