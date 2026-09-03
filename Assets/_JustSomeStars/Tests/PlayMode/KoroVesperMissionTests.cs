using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Discovery;
using JustSomeStars.Runtime.Flight;
using JustSomeStars.Runtime.Input;
using JustSomeStars.Runtime.Missions;
using JustSomeStars.Runtime.Player;
using JustSomeStars.Runtime.Rendering2D;
using JustSomeStars.Runtime.Saving;
using JustSomeStars.Runtime.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class KoroVesperMissionTests
    {
        private const string SceneName = "KoroVesper";
        private const string FlightSceneName = "Task25VesperFlight";
        private const string ContentResource = "Task25KoroVesperChapter";
        private const string TargetPath =
            "outputs/task25-koro-vesper-gameplay-target-v1.png";
        private const string TargetSha256 =
            "a76309c8f98a3020363056e6f977ba3965ff0d604b1a4e1538ed5ffbb38ef8b1";
        private const string NaturalPlumePath =
            "Assets/_JustSomeStars/Art/2D/Environments/KoroVesper/Vfx/NaturalGeyserPlume.png";
        private const string SignalPlumePath =
            "Assets/_JustSomeStars/Art/2D/Environments/KoroVesper/Vfx/SignalGeyserPlume.png";
        private const string ProgressionTypeName =
            "JustSomeStars.Runtime.Missions.KoroVesperProgressionService";
        private const string CoordinatorTypeName =
            "JustSomeStars.Runtime.Missions.DestinationProgressionCoordinator";
        private const string ControllerTypeName =
            "JustSomeStars.Runtime.Missions.KoroVesperMissionController2D";
        private const string GeyserTypeName =
            "JustSomeStars.Runtime.Discovery.GeyserController";
        private const string GeyserCycleTypeName =
            "JustSomeStars.Runtime.Discovery.GeyserCycleModel";
        private const string SpectrumSampleTypeName =
            "JustSomeStars.Runtime.Discovery.KoroSpectrumSample";
        private const string SpectrumComparisonTypeName =
            "JustSomeStars.Runtime.Discovery.KoroSpectrumComparison";

        private readonly List<UnityEngine.Object> m_OwnedObjects = new();
        private readonly List<string> m_TemporaryRoots = new();

        [TearDown]
        public void TearDown()
        {
            for (var index = m_OwnedObjects.Count - 1; index >= 0; index--)
            {
                if (m_OwnedObjects[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(m_OwnedObjects[index]);
                }
            }

            m_OwnedObjects.Clear();
            foreach (var path in m_TemporaryRoots)
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }

            m_TemporaryRoots.Clear();
        }

        [Test]
        public async Task ProductionAssets_AreReachableLayeredAndHashLocked()
        {
            Assert.That(Hash(File.ReadAllBytes(Path.GetFullPath(TargetPath))),
                Is.EqualTo(TargetSha256),
                "Task 25 must remain bound to the critic-approved visual target.");
#if UNITY_EDITOR
            var enabledScenes = UnityEditor.EditorBuildSettings.scenes
                .Where(item => item.enabled)
                .Select(item => Path.GetFileNameWithoutExtension(item.path))
                .ToArray();
            Assert.That(enabledScenes, Does.Contain(SceneName),
                "Koro/Vesper must be loadable in a production player.");
#endif
            Assert.That(Application.CanStreamedLevelBeLoaded(SceneName), Is.True);
            Assert.That(RequireType(ControllerTypeName), Is.Not.Null);
            Assert.That(RequireType(GeyserTypeName), Is.Not.Null);

            var content = Resources.Load<ScriptableObject>(ContentResource);
            Assert.That(content, Is.Not.Null,
                "The scene-owned mission needs a runtime-loadable chapter catalog.");
            Invoke(content, "ValidateOrThrow");
            Assert.That(ReadContentId(content, "StableId"),
                Is.EqualTo("mission.koro-vesper.chapter-one"));
            Assert.That(ReadStringList(content, "CheckpointNodeIds"),
                Is.EqualTo(new[]
                {
                    "mission.koro-vesper.approach",
                    "mission.koro-vesper.landed",
                    "mission.koro-vesper.traversal",
                    "mission.koro-vesper.spectra",
                    "mission.koro-vesper.rhythm",
                    "mission.koro-vesper.fragment",
                }));

            var catalogHandle = Addressables.LoadAssetAsync<SceneCatalog>(
                SceneCatalog.AddressablesKey);
            try
            {
                await catalogHandle.Task;
                Assert.That(catalogHandle.Status,
                    Is.EqualTo(AsyncOperationStatus.Succeeded));
                catalogHandle.Result.Validate();
                Assert.That(catalogHandle.Result.TryGetEntry(
                    "destination.koro.surface",
                    out var entry), Is.True);
                Assert.That(entry.Address, Is.EqualTo(SceneName));
                Assert.That(entry.TargetMode, Is.EqualTo(GameMode.Surface));
            }
            finally
            {
                if (catalogHandle.IsValid())
                {
                    Addressables.Release(catalogHandle);
                }
            }

            var scene = await LoadSceneAsync(SceneName);
            try
            {
                var roots = scene.GetRootGameObjects();
                var lifecycle = FindSingleInScene<SurfaceGameplayLifecycle2D>(scene);
                Assert.That(lifecycle, Is.Not.Null);
                Assert.That(FindByTypeName(scene, ControllerTypeName),
                    Has.Length.EqualTo(1));
                Assert.That(FindByTypeName(scene, GeyserTypeName),
                    Has.Length.EqualTo(2));
                foreach (var required in new[]
                         {
                             "Captain", "Mira", "Bea", "Ori", "Vesper",
                             "NaturalGeyser", "SignalGeyser", "SpectrumPanel",
                             "SecondSignalFragment", "KoroObjective",
                         })
                {
                    var match = required == "Captain"
                        ? FindNamedWithComponent<Rigidbody2D>(roots, required)
                        : FindNamed(roots, required);
                    Assert.That(match, Is.Not.Null, required);
                }

                var bands = FindNamed(roots, "Bands");
                Assert.That(bands, Is.Not.Null);
                Assert.That(Enumerable.Range(0, bands.transform.childCount)
                        .Select(index => bands.transform.GetChild(index).name),
                    Is.EquivalentTo(new[]
                    {
                        "Sky", "FarWorld", "Atmosphere", "Midground",
                        "Gameplay", "ActorsAndProps", "Foreground", "Hud",
                    }));
                Assert.That(roots.SelectMany(root =>
                        root.GetComponentsInChildren<ParallaxLayer2D>(true))
                    .ToArray(),
                    Has.Length.EqualTo(7));
                Assert.That(roots.SelectMany(root =>
                        root.GetComponentsInChildren<MeshRenderer>(true))
                    .Where(renderer => renderer.GetComponent<TMP_Text>() == null),
                    Is.Empty, "Koro/Vesper gameplay must remain true layered 2.5D.");
                Assert.That(roots.SelectMany(root =>
                    root.GetComponentsInChildren<SkinnedMeshRenderer>(true)),
                    Is.Empty);

                var layerSprites = new[]
                    {
                        "Sky", "FarWorld", "Atmosphere", "Midground",
                        "Gameplay", "Foreground",
                    }
                    .Select(name => Enumerable.Range(0, bands.transform.childCount)
                        .Select(index => bands.transform.GetChild(index))
                        .Single(child => child.name == name)
                        .GetComponentInChildren<SpriteRenderer>(true))
                    .Where(renderer => renderer != null && renderer.sprite != null)
                    .Select(renderer => renderer.sprite.texture)
                    .Distinct()
                    .ToArray();
                Assert.That(layerSprites.Length, Is.GreaterThanOrEqualTo(6),
                    "Koro cannot be a single flattened concept plate.");
                Assert.That(layerSprites.All(texture => texture.width >= 1024 &&
                    texture.height >= 512), Is.True);

                var camera = FindSingleInScene<Camera>(scene);
                Assert.That(camera, Is.Not.Null);
                Assert.That(camera.orthographic, Is.True);
                var composition = camera.GetComponent<CompositionCamera2D>();
                Assert.That(composition, Is.Not.Null);
                var movementBounds = ReadField<Bounds>(composition, "movementBounds");
                var viewportWidth = camera.orthographicSize * 2f * (1616f / 720f);
                var requiredHeight = camera.orthographicSize * 2f;
                foreach (var backgroundName in new[] { "Sky", "FarWorld" })
                {
                    var backgroundBand = Enumerable.Range(
                            0, bands.transform.childCount)
                        .Select(index => bands.transform.GetChild(index))
                        .Single(child => child.name == backgroundName);
                    var renderer = backgroundBand
                        .GetComponentInChildren<SpriteRenderer>(true);
                    var parallax = backgroundBand.GetComponent<ParallaxLayer2D>();
                    Assert.That(parallax, Is.Not.Null, backgroundName);
                    var travelMargin = 2f * movementBounds.extents.x *
                        (1f - parallax.Factor);
                    var requiredWidth = viewportWidth + travelMargin;
                    Assert.That(renderer.bounds.size.x,
                        Is.GreaterThanOrEqualTo(requiredWidth - 0.01f),
                        $"{backgroundName} must cover the settled 1616x720 viewport " +
                        "through the full camera rail without black side gutters.");
                    Assert.That(renderer.bounds.size.y,
                        Is.GreaterThanOrEqualTo(requiredHeight - 0.01f));
                }

                var plumeRenderers = FindByTypeName(scene, GeyserTypeName)
                    .Select(geyser => ReadField<Transform>(geyser, "plumeVisual"))
                    .Select(plume => plume.GetComponent<SpriteRenderer>())
                    .OrderBy(renderer => renderer.transform.parent.name)
                    .ToArray();
                Assert.That(plumeRenderers, Has.Length.EqualTo(2));
                Assert.That(plumeRenderers.All(renderer =>
                    renderer != null && renderer.sprite != null), Is.True);
                Assert.That(plumeRenderers.Select(renderer => renderer.sprite.texture)
                    .Distinct().Count(), Is.EqualTo(2),
                    "Natural and Signal geysers need distinct authored plume art.");
                Assert.That(plumeRenderers.Select(renderer => renderer.sprite.texture)
                    .Intersect(layerSprites), Is.Empty,
                    "A geyser plume cannot reuse a whole environment layer plate.");
                var gameplayOrder = Enumerable.Range(
                        0, bands.transform.childCount)
                    .Select(index => bands.transform.GetChild(index))
                    .Single(child => child.name == "Gameplay")
                    .GetComponentInChildren<SpriteRenderer>(true)
                    .sortingOrder;
                Assert.That(plumeRenderers.All(renderer =>
                    renderer.sortingOrder > gameplayOrder &&
                    renderer.sortingOrder < 512), Is.True,
                    "Dedicated plumes must render above the terrain plate and " +
                    "behind the crew rather than disappearing behind the world.");
#if UNITY_EDITOR
                var plumePaths = plumeRenderers
                    .Select(renderer => UnityEditor.AssetDatabase.GetAssetPath(
                        renderer.sprite.texture))
                    .ToArray();
                Assert.That(plumePaths,
                    Is.EquivalentTo(new[] { NaturalPlumePath, SignalPlumePath }));
                foreach (var plumePath in plumePaths)
                {
                    var importer = UnityEditor.AssetImporter.GetAtPath(plumePath)
                        as UnityEditor.TextureImporter;
                    Assert.That(importer, Is.Not.Null, plumePath);
                    Assert.That(importer.textureType,
                        Is.EqualTo(UnityEditor.TextureImporterType.Sprite));
                    Assert.That(importer.alphaIsTransparency, Is.True, plumePath);
                }
#endif

                var missionController = FindByTypeName(scene, ControllerTypeName)
                    .Single();
                var framingLayers = ReadField<SpriteRenderer[]>(
                    missionController, "framingLayers");
                Assert.That(framingLayers, Has.Length.EqualTo(6));
                Assert.That(framingLayers.All(renderer =>
                    renderer != null && renderer.sprite != null), Is.True);
                Assert.That(framingLayers.Select(renderer => renderer.name),
                    Is.EquivalentTo(new[]
                    {
                        "KoroSky", "KoroFarWorld", "KoroAtmosphere",
                        "KoroMidground", "KoroGameplay", "KoroForeground",
                    }));
                foreach (var profile in composition.Profiles)
                {
                    Assert.That(profile.CenterRails.extents.x,
                        Is.LessThanOrEqualTo(0.05f),
                        "The approved Koro tableau uses a stable wide composition; " +
                        "camera tracking must not expose finite layer edges.");
                }

                camera.aspect = 1616f / 720f;
                Invoke(missionController, "ApplyResponsiveFraming", true);
                Assert.That(camera.orthographicSize,
                    Is.InRange(3.70f, 4.05f));
                var responsiveWidth = camera.orthographicSize * 2f * camera.aspect;
                var framingGaps = framingLayers
                    .Where(renderer =>
                        renderer.bounds.size.x + 0.01f < responsiveWidth)
                    .Select(renderer =>
                        $"{renderer.name}: {renderer.bounds.size.x:F3} < " +
                        $"{responsiveWidth:F3}")
                    .ToArray();
                Assert.That(framingGaps, Is.Empty,
                    "Every Koro art band must cover the responsive 1616x720 view; " +
                    "a full-bleed sky alone cannot hide hard inner layer edges.\n" +
                    string.Join("\n", framingGaps));
            }
            finally
            {
                await UnloadSceneAsync(scene);
            }
        }

        [Test]
        public void GeyserCycle_IsDeterministicPauseSafeAndReducedMotionPreservesTiming()
        {
            var modelType = RequireType(GeyserCycleTypeName);
            var natural = Activator.CreateInstance(
                modelType,
                8f,
                1.5f,
                2.25f,
                0f,
                false);
            var signal = Activator.CreateInstance(
                modelType,
                8f,
                1.5f,
                2.25f,
                4f,
                true);

            var naturalWarning = Invoke(natural, "Sample", 1.25f, false);
            var naturalEruption = Invoke(natural, "Sample", 2.25f, false);
            var naturalRepeat = Invoke(natural, "Sample", 10.25f, false);
            var reduced = Invoke(natural, "Sample", 2.25f, true);
            var signalAtNaturalPeak = Invoke(signal, "Sample", 2.25f, false);

            Assert.That(Read<bool>(naturalWarning, "WarningActive"), Is.True);
            Assert.That(Read<bool>(naturalEruption, "HazardActive"), Is.True);
            Assert.That(Read<float>(naturalEruption, "BallisticHeight"),
                Is.GreaterThan(0f));
            Assert.That(Read<float>(naturalRepeat, "BallisticHeight"),
                Is.EqualTo(Read<float>(naturalEruption, "BallisticHeight"))
                    .Within(0.0001f));
            Assert.That(Read<bool>(reduced, "HazardActive"),
                Is.EqualTo(Read<bool>(naturalEruption, "HazardActive")),
                "Reduced motion may reduce particles, never alter gameplay timing.");
            Assert.That(Read<float>(reduced, "VisualIntensity"),
                Is.LessThan(Read<float>(naturalEruption, "VisualIntensity")));
            Assert.That(Read<bool>(signalAtNaturalPeak, "HazardActive"), Is.False,
                "The paired geysers need a real alternating comparison rhythm.");

            var paused = Invoke(natural, "Advance", 2f, false, false);
            var stillPaused = Invoke(natural, "Advance", 5f, true, false);
            Assert.That(Read<float>(stillPaused, "CycleTime"),
                Is.EqualTo(Read<float>(paused, "CycleTime")).Within(0.0001f),
                "Paused or unloaded play cannot advance an invisible hazard.");
        }

        [Test]
        public void SpectrumComparison_RequiresTwoDistinctAuthoredUvSamples()
        {
            var sampleType = RequireType(SpectrumSampleTypeName);
            var comparisonType = RequireType(SpectrumComparisonTypeName);
            var natural = Activator.CreateInstance(
                sampleType,
                "spectrum.koro.natural",
                new[] { 121.6f, 130.4f, 135.6f },
                new[] { 1f, 0.42f, 0.68f },
                "nm");
            var signal = Activator.CreateInstance(
                sampleType,
                "spectrum.koro.signal",
                new[] { 121.6f, 130.4f, 135.6f },
                new[] { 1f, 0.42f, 0.91f },
                "nm");

            Assert.Throws<TargetInvocationException>(() =>
                InvokeStatic(comparisonType, "Compare", natural, natural));
            var result = InvokeStatic(comparisonType, "Compare", natural, signal);
            Assert.That(Read<bool>(result, "WaterRelatedSignaturePresent"), Is.True);
            Assert.That(Read<bool>(result, "RepeatingSignalDifferencePresent"), Is.True);
            Assert.That(Read<string>(result, "Unit"), Is.EqualTo("nm"));
            Assert.That(Read<float>(result, "MatchScore"), Is.InRange(0f, 1f));
            Assert.That(Read<string>(result, "Interpretation"),
                Does.Contain("may").IgnoreCase,
                "Spectra may support a plume interpretation; they cannot prove life.");
            Assert.That(Read<string>(result, "Interpretation"),
                Does.Not.Contain("proves life").IgnoreCase);
        }

        [Test]
        public async Task ProductionRoute_CompletesSameStoryAcrossEveryProfile()
        {
            foreach (AssistLevel assist in Enum.GetValues(typeof(AssistLevel)))
            foreach (ScienceDepth depth in Enum.GetValues(typeof(ScienceDepth)))
            {
                var root = CreateTemporaryRoot($"route-{assist}-{depth}");
                var settings = new SettingsService(Path.Combine(root, "settings.json"));
                var saves = new LocalSaveService(Path.Combine(root, "save.json"));
                await settings.InitializeAsync(CancellationToken.None);
                await saves.InitializeAsync(CancellationToken.None);
                var configured = settings.Current;
                configured.PilotingAssist = assist;
                configured.ExplorationAssist = assist;
                configured.ScienceDepth = depth;
                settings.Apply(configured);
                var events = new GameEventBus();
                var progression = CreateProgression(events, saves, settings);
                await ((IGameService)progression).InitializeAsync(
                    CancellationToken.None);
                try
                {
                    await AdvanceCompleteRouteAsync(progression, events);
                    Assert.That(Read<bool>(progression, "IsMissionComplete"), Is.True);
                    Assert.That(Read<int>(progression, "CheckpointOrdinal"),
                        Is.EqualTo(6));
                    Assert.That(ReadContentId(progression, "FragmentId"),
                        Is.EqualTo("fragment.signal.koro.002"));
                    Assert.That(Read<int>(progression, "DuplicateEventCount"), Is.Zero);
                    Assert.That(Read<string>(progression, "ResumeSceneName"),
                        Is.EqualTo(SceneName));
                    Assert.That(settings.Current.PilotingAssist, Is.EqualTo(assist));
                    Assert.That(settings.Current.ExplorationAssist, Is.EqualTo(assist));
                    Assert.That(settings.Current.ScienceDepth, Is.EqualTo(depth));
                }
                finally
                {
                    await ((IGameService)progression).ShutdownAsync();
                    await saves.ShutdownAsync();
                    await settings.ShutdownAsync();
                }
            }
        }

        [Test]
        public void SaveMerge_PreservesBothFragmentsAndOrdersKoroAboveMirra()
        {
            var mirra = GameSave.CreateNew("save.merge", 10);
            mirra.Story.CheckpointId = "mission.mirra.complete";
            mirra.Story.CheckpointOrdinal = 7;
            mirra.Mission = new MissionProgress
            {
                MissionId = "mission.mirra.chapter-one",
                CheckpointNodeId = "mission.mirra.complete",
                CheckpointOrdinal = 7,
                CompletedNodeIds = new[] { "mission.mirra.complete" },
                ActiveNodeIds = Array.Empty<string>(),
            };
            mirra.DiscoveryIds = new[] { "fragment.signal.mirra.001" };

            var koro = mirra.Copy();
            koro.Story.CheckpointId = "mission.koro-vesper.approach";
            koro.Story.CheckpointOrdinal = 8;
            koro.Mission = new MissionProgress
            {
                MissionId = "mission.koro-vesper.chapter-one",
                CheckpointNodeId = "mission.koro-vesper.approach",
                CheckpointOrdinal = 1,
                CompletedNodeIds = new[] { "mission.koro-vesper.approach" },
                ActiveNodeIds = new[] { "mission.koro-vesper.landed" },
            };
            koro.DiscoveryIds = new[]
            {
                "fragment.signal.mirra.001",
                "sample.koro.geyser-natural",
            };

            var merged = SaveMerge.Combine(mirra, koro);
            Assert.That(merged.Mission.MissionId,
                Is.EqualTo("mission.koro-vesper.chapter-one"));
            Assert.That(merged.DiscoveryIds,
                Is.EquivalentTo(new[]
                {
                    "fragment.signal.mirra.001",
                    "sample.koro.geyser-natural",
                }));
        }

        [Test]
        public async Task CompletedMirraSave_SelectsVesperThroughOneCoordinator()
        {
            var root = CreateTemporaryRoot("destination-coordinator");
            var settings = new SettingsService(Path.Combine(root, "settings.json"));
            var saves = new LocalSaveService(Path.Combine(root, "save.json"));
            await settings.InitializeAsync(CancellationToken.None);
            await saves.InitializeAsync(CancellationToken.None);
            var save = GameSave.CreateNew("save.destination", 10);
            save.Story.CheckpointId = "mission.mirra.complete";
            save.Story.CheckpointOrdinal = 7;
            save.Mission = new MissionProgress
            {
                MissionId = "mission.mirra.chapter-one",
                CheckpointNodeId = "mission.mirra.complete",
                CheckpointOrdinal = 7,
                CompletedNodeIds = new[] { "mission.mirra.complete" },
                ActiveNodeIds = Array.Empty<string>(),
            };
            save.DiscoveryIds = Array.Empty<string>();
            await saves.SaveCheckpointAsync(save, CancellationToken.None);

            var coordinator = Activator.CreateInstance(
                RequireType(CoordinatorTypeName),
                new GameEventBus(),
                saves,
                settings);
            Assert.That(coordinator, Is.InstanceOf<IGameService>());
            await ((IGameService)coordinator).InitializeAsync(
                CancellationToken.None);
            try
            {
                Assert.That(Read<string>(coordinator, "ResumeSceneName"),
                    Is.EqualTo("Task25VesperFlight"));
                Assert.That(Read<string>(coordinator, "ActiveChapterId"),
                    Is.EqualTo("mission.koro-vesper.chapter-one"));
                var active = Read<object>(coordinator, "ActiveProgression");
                Assert.That(Invoke(
                        active,
                        "HasDiscovery",
                        "fragment.signal.mirra.001"),
                    Is.True,
                    "A real completed Mirra save must hand its first fragment " +
                    "into Koro without a test-authored discovery seed.");
            }
            finally
            {
                await ((IGameService)coordinator).ShutdownAsync();
                await saves.ShutdownAsync();
                await settings.ShutdownAsync();
            }
        }

        [Test]
        public async Task Checkpoints_RestoreWithoutSkippingOrDuplicateFragmentGrant()
        {
            var root = CreateTemporaryRoot("checkpoint-reload");
            var settings = new SettingsService(Path.Combine(root, "settings.json"));
            var saves = new LocalSaveService(Path.Combine(root, "save.json"));
            await settings.InitializeAsync(CancellationToken.None);
            await saves.InitializeAsync(CancellationToken.None);
            var events = new GameEventBus();
            var progression = CreateProgression(events, saves, settings);
            await ((IGameService)progression).InitializeAsync(CancellationToken.None);
            try
            {
                events.Publish(new ApproachCompleted(
                    new ContentId("approach.vesper.gravity-route")));
                await InvokeTask(progression, "FlushPendingAsync",
                    CancellationToken.None);
                events.Publish(new LandingCompleted(
                    new ContentId("destination.koro.surface")));
                await InvokeTask(progression, "FlushPendingAsync",
                    CancellationToken.None);

                events.Publish(new PhenomenonObserved(
                    new ContentId("phenomenon.koro.geyser-natural")));
                await InvokeTask(progression, "FlushPendingAsync",
                    CancellationToken.None);
                Assert.That(Invoke(
                        progression,
                        "HasDiscovery",
                        "sample.koro.geyser-natural"),
                    Is.False,
                    "An observation before the spectra checkpoint is not valid yet.");

                events.Publish(new TraversalMilestoneReached(
                    new ContentId("route.koro.low-gravity")));
                await InvokeTask(progression, "FlushPendingAsync",
                    CancellationToken.None);
                events.Publish(new PhenomenonObserved(
                    new ContentId("phenomenon.koro.geyser-natural")));
                events.Publish(new PhenomenonObserved(
                    new ContentId("phenomenon.koro.geyser-signal")));
                events.Publish(new EvidenceAccepted(
                    new ContentId("evidence.koro.spectrum-comparison"),
                    new ContentId("prediction.koro.water-related-material")));
                await InvokeTask(progression, "FlushPendingAsync",
                    CancellationToken.None);
                Assert.That(Read<int>(progression, "CheckpointOrdinal"), Is.EqualTo(4));
                Assert.That(Read<int>(progression, "DuplicateEventCount"), Is.Zero,
                    "A rejected early observation must remain retryable later.");
                await ((IGameService)progression).ShutdownAsync();

                progression = CreateProgression(events, saves, settings);
                await ((IGameService)progression).InitializeAsync(
                    CancellationToken.None);
                Assert.That(Read<int>(progression, "CheckpointOrdinal"), Is.EqualTo(4));
                Assert.That(Read<string>(progression, "CurrentObjectiveId"),
                    Is.EqualTo("mission.koro-vesper.rhythm"));

                events.Publish(new InteractionCompleted(
                    new ContentId("interaction.koro.geyser-rhythm")));
                await InvokeTask(progression, "FlushPendingAsync",
                    CancellationToken.None);
                events.Publish(new SignalFragmentRecovered(
                    new ContentId("fragment.signal.koro.002")));
                events.Publish(new SignalFragmentRecovered(
                    new ContentId("fragment.signal.koro.002")));
                await InvokeTask(progression, "FlushPendingAsync",
                    CancellationToken.None);

                Assert.That(Read<bool>(progression, "IsMissionComplete"), Is.True);
                var loaded = await saves.LoadAsync(CancellationToken.None);
                Assert.That(loaded.Save.Mission.CheckpointOrdinal, Is.EqualTo(6));
                Assert.That(loaded.Save.DiscoveryIds.Count(id =>
                    id == "fragment.signal.koro.002"), Is.EqualTo(1));
            }
            finally
            {
                await ((IGameService)progression).ShutdownAsync();
                await saves.ShutdownAsync();
                await settings.ShutdownAsync();
            }
        }

        [Test]
        public void ScienceContent_IsConditionalSourceBackedAndDepthSpecific()
        {
            var content = Resources.Load<ScriptableObject>(ContentResource);
            Assert.That(content, Is.Not.Null);
            Invoke(content, "ValidateOrThrow");
            var sources = ReadEnumerable(content, "ScienceSources").ToArray();
            Assert.That(sources, Has.Length.GreaterThanOrEqualTo(3),
                "Spectroscopy, tidal heating/ocean evidence and habitability " +
                "need claim-specific primary sources.");
            Assert.That(sources.All(source =>
                    Read<string>(source, "Publisher") == "NASA" &&
                    Read<string>(source, "SourceUrl").StartsWith(
                        "https://science.nasa.gov/", StringComparison.Ordinal)),
                Is.True);

            var atlasId = new ContentId("atlas.koro.geyser-spectra");
            var guided = (string)Invoke(content, "ResolveAtlasEnglish",
                atlasId, ScienceDepth.Guided);
            var balanced = (string)Invoke(content, "ResolveAtlasEnglish",
                atlasId, ScienceDepth.Balanced);
            var deep = (string)Invoke(content, "ResolveAtlasEnglish",
                atlasId, ScienceDepth.Deep);
            Assert.That(new[] { guided, balanced, deep }.Distinct().Count(),
                Is.EqualTo(3));
            Assert.That(guided.Length, Is.LessThan(balanced.Length));
            Assert.That(balanced.Length, Is.LessThan(deep.Length));
            Assert.That(string.Join(" ", guided, balanced, deep),
                Does.Contain("may").IgnoreCase);
            Assert.That(string.Join(" ", guided, balanced, deep),
                Does.Not.Contain("life found").IgnoreCase);
        }

        [Test]
        public async Task SceneReload_RebindsOneMissionAndTwoGeysersWithoutLeaks()
        {
            var first = await LoadSceneAsync(SceneName);
            try
            {
                Assert.That(FindByTypeName(first, ControllerTypeName),
                    Has.Length.EqualTo(1));
                Assert.That(FindByTypeName(first, GeyserTypeName),
                    Has.Length.EqualTo(2));
            }
            finally
            {
                await UnloadSceneAsync(first);
            }

            var second = await LoadSceneAsync(SceneName);
            try
            {
                Assert.That(FindByTypeName(second, ControllerTypeName),
                    Has.Length.EqualTo(1));
                Assert.That(FindByTypeName(second, GeyserTypeName),
                    Has.Length.EqualTo(2));
                Assert.That(FindByTypeName(second, ControllerTypeName)
                        .Single().GetComponents<MonoBehaviour>()
                        .Count(component => component.GetType().FullName ==
                            ControllerTypeName), Is.EqualTo(1));
            }
            finally
            {
                await UnloadSceneAsync(second);
            }
        }

        [Test]
        public async Task ProductionFlightLandingAndSurfaceRoute_CompletesRealMission()
        {
            var root = CreateTemporaryRoot("production-scene-bind");
            var settings = new SettingsService(Path.Combine(root, "settings.json"));
            var saves = new LocalSaveService(Path.Combine(root, "save.json"));
            var actions = UnityEngine.Object.Instantiate(InputSystem.actions);
            m_OwnedObjects.Add(actions);
            var input = new InputRouter(actions, settings);
            var modes = GameModeController.CreateForTests(
                GameMode.Flight,
                new InputRouterGameModeRuntimeHooks(input));
            await settings.InitializeAsync(CancellationToken.None);
            await saves.InitializeAsync(CancellationToken.None);
            var configured = settings.Current;
            configured.ScienceDepth = ScienceDepth.Deep;
            Assert.That(settings.Apply(configured), Is.True);
            var completedMirra = GameSave.CreateNew("save.koro.real-route", 10);
            completedMirra.Story.CheckpointId = "mission.mirra.complete";
            completedMirra.Story.CheckpointOrdinal = 7;
            completedMirra.Mission = new MissionProgress
            {
                MissionId = "mission.mirra.chapter-one",
                CheckpointNodeId = "mission.mirra.complete",
                CheckpointOrdinal = 7,
                CompletedNodeIds = new[] { "mission.mirra.complete" },
                ActiveNodeIds = Array.Empty<string>(),
            };
            completedMirra.DiscoveryIds = Array.Empty<string>();
            await saves.SaveCheckpointAsync(completedMirra, CancellationToken.None);
            await input.InitializeAsync(CancellationToken.None);
            await modes.InitializeAsync(CancellationToken.None);
            var events = new GameEventBus();
            var progression = CreateProgression(events, saves, settings);
            await ((IGameService)progression).InitializeAsync(CancellationToken.None);
            var dependencies = new SurfaceGameplayDependencies(
                settings,
                input,
                modes,
                events,
                saves,
                (IChapterProgression)progression);
            var transition = new UnitySceneTransition(
                new FrontendDependencies(settings, input), dependencies);
            dependencies.ConfigureSceneTransition(transition);
            transition.ConfigureFlightDependencies(
                new FlightGameplayDependencies(
                    settings,
                    input,
                    modes,
                    events,
                    transition,
                    (IChapterProgression)progression));
            var flightScene = await LoadSceneAsync(FlightSceneName);
            var scene = flightScene;
            try
            {
                Invoke(transition, "BindActiveScene");
                var flightLifecycle = FindSingleInScene<FlightGameplayLifecycle2D>(
                    flightScene);
                Assert.That(flightLifecycle.IsConfigured, Is.True);
                Assert.That(flightLifecycle.Motor.IsBound, Is.True);
                Assert.That(flightLifecycle.Motor.Model, Is.Not.Null);
                var route = FindSingleInScene<Task17FlightRoute2D>(flightScene);
                Assert.That(route.GravityOpportunity, Is.Not.Null,
                    "The production Vesper flight must retain its gravity assist.");

                var flightState = flightLifecycle.Motor.State;
                var initialFlightState = flightState;
                var thrust = new FlightInputFrame(
                    Vector2.right,
                    primaryHeld: true,
                    secondaryHeld: false);
                for (var step = 0;
                     step < 2000 &&
                     flightState.Position.x < route.RouteFinish.x - 5f;
                     step++)
                {
                    flightState = flightLifecycle.Motor.Model.Step(
                        flightState,
                        thrust,
                        0.1f);
                }

                var brake = new FlightInputFrame(
                    Vector2.zero,
                    primaryHeld: false,
                    secondaryHeld: true);
                for (var step = 0;
                     step < 100 && flightState.Velocity.magnitude > 3.9f;
                     step++)
                {
                    flightState = flightLifecycle.Motor.Model.Step(
                        flightState,
                        brake,
                        0.1f);
                }

                flightLifecycle.Motor.SetStateForTests(flightState);
                Assert.That(flightState.Position.x,
                    Is.GreaterThan(initialFlightState.Position.x + 400f),
                    "The real Vesper model must traverse the production route.");
                var landingGate = FindSingleInScene<FlightLandingGate2D>(
                    flightScene);
                var requiredLane = ReadField<int>(landingGate, "requiredLane");
                var maximumApproachSpeed = ReadField<float>(
                    landingGate,
                    "maximumApproachSpeed");
                var approachIsValid = flightState.Lane == requiredLane &&
                    flightState.Velocity.magnitude <= maximumApproachSpeed;
                Assert.That(approachIsValid, Is.True,
                    "The simulated production flight must reach a valid landing " +
                    "lane and speed, not bypass the landing gate contract.");
                Assert.That(await flightLifecycle.Landing.TryLandAsync(
                    approachIsValid,
                    CancellationToken.None), Is.True);
                scene = SceneManager.GetActiveScene();
                Assert.That(scene.name, Is.EqualTo(SceneName));
                Assert.That(modes.CurrentMode, Is.EqualTo(GameMode.Surface));
                Assert.That(ReadStringList(progression, "EventHistory").Take(2),
                    Is.EqualTo(new[]
                    {
                        "ApproachCompleted:approach.vesper.gravity-route",
                        "LandingCompleted:destination.koro.surface",
                    }),
                    "Only the production LandingSequence may publish the route " +
                    "arrival events in this end-to-end fixture.");

                var lifecycle = FindSingleInScene<SurfaceGameplayLifecycle2D>(scene);
                Assert.That(lifecycle.IsConfigured, Is.True);
                var controller = FindByTypeName(scene, ControllerTypeName).Single();
                Assert.That(Read<bool>(controller, "IsConfigured"), Is.True);
                Assert.That(FindByTypeName(scene, GeyserTypeName)
                    .All(item => Read<bool>(item, "IsConfigured")), Is.True);
                var retainedMirraClimate = FindByTypeName(
                    scene, typeof(MirraClimateField).FullName);
                var lensInstruments = ReadField<InstrumentDefinition[]>(
                    lifecycle,
                    "lensInstruments");
                var captain = FindNamedWithComponent<Rigidbody2D>(
                    scene.GetRootGameObjects(),
                    "Captain");
                var captainBody = captain.GetComponent<Rigidbody2D>();
                var dialoguePresenter = controller.GetComponent<
                    JustSomeStars.Runtime.Dialogue.MirraDialoguePresenter2D>();
                Assert.That(captainBody.gravityScale,
                    Is.EqualTo(0.42f).Within(0.001f));
                Assert.That(dialoguePresenter, Is.Not.Null,
                    "Koro companion observations need the authored HUD presenter.");
                foreach (var name in new[] { "Mira", "Bea", "Ori" })
                {
                    var actor = FindNamed(scene.GetRootGameObjects(), name);
                    Assert.That(actor.GetComponent<Rigidbody2D>().bodyType,
                        Is.EqualTo(RigidbodyType2D.Dynamic));
                }

                Invoke(controller, "Update");
                Assert.That(Read<int>(progression, "CheckpointOrdinal"),
                    Is.EqualTo(2));
                var objectiveAfterLanding = ReadField<TMP_Text>(
                    controller, "objectiveLabel").text;

                await ScanRealTargetAsync(
                    lifecycle,
                    modes,
                    captainBody,
                    "phenomenon.koro.geyser-natural");
                await InvokeTask(
                    progression, "WaitForQuiescenceAsync", CancellationToken.None);
                Assert.That(Read<bool>(controller, "HasNaturalSample"), Is.False,
                    "A real Lens scan before traversal cannot consume or cache " +
                    "the later required spectrum.");
                Assert.That(Read<int>(progression, "CheckpointOrdinal"),
                    Is.EqualTo(2));

                captainBody.position = new Vector2(2.35f, captainBody.position.y);
                Invoke(controller, "Update");
                await InvokeTask(
                    progression, "WaitForQuiescenceAsync", CancellationToken.None);

                var checkpointAfterTraversal = Read<int>(
                    progression, "CheckpointOrdinal");
                var objectiveAfterTraversal = ReadField<TMP_Text>(
                    controller, "objectiveLabel").text;

                await ScanRealTargetAsync(
                    lifecycle,
                    modes,
                    captainBody,
                    "phenomenon.koro.geyser-natural");
                await ScanRealTargetAsync(
                    lifecycle,
                    modes,
                    captainBody,
                    "phenomenon.koro.geyser-signal");
                await InvokeTask(
                    progression, "WaitForQuiescenceAsync", CancellationToken.None);
                Assert.That(Read<int>(progression, "CheckpointOrdinal"),
                    Is.EqualTo(4),
                    "Two real Lens scans must produce the comparison checkpoint.");
                Assert.That(ReadField<TMP_Text>(controller, "spectrumPanel").text,
                    Does.Contain("135.6 nm"),
                    "Deep science settings must drive the production spectrum copy.");

                DriveRealGeyserRhythm(scene, controller);
                await InvokeTask(
                    progression, "WaitForQuiescenceAsync", CancellationToken.None);
                Assert.That(Read<int>(progression, "CheckpointOrdinal"),
                    Is.EqualTo(5),
                    "The real alternating geyser controllers must earn rhythm.");

                var fragmentPoint = ReadField<Transform>(controller, "fragmentPoint");
                captainBody.position = fragmentPoint.position;
                Physics2D.SyncTransforms();
                Assert.That(await (Task<bool>)Invoke(
                        controller,
                        "TryRecoverFragmentAsync",
                        CancellationToken.None),
                    Is.True);
                await InvokeTask(
                    progression, "WaitForQuiescenceAsync", CancellationToken.None);
                Assert.That(Read<bool>(progression, "IsMissionComplete"), Is.True);
                Assert.That(ReadField<TMP_Text>(controller, "objectiveLabel").text,
                    Is.EqualTo("CHAPTER COMPLETE · SECOND SIGNAL RECOVERED"));
                var dialogueTail = ReadField<Task>(controller, "m_DialogueTail");
                await WaitUntilAsync(() => dialogueTail.IsCompleted, 20f);
                Assert.That(dialogueTail.IsCompleted, Is.True,
                    "All three production observations must finish their readable " +
                    "HUD presentation within the bounded route timeout.");
                await dialogueTail;
                Assert.That(dialoguePresenter.PresentationCount,
                    Is.GreaterThanOrEqualTo(3),
                    "Mira, Bea and Ori observations must reach the live HUD.");

                var durable = await saves.LoadAsync(CancellationToken.None);
                Assert.That(durable.HasSave, Is.True);
                Assert.That(durable.Save.Mission.CheckpointOrdinal, Is.EqualTo(6));
                Assert.That(durable.Save.DiscoveryIds,
                    Does.Contain("fragment.signal.mirra.001"));
                Assert.That(durable.Save.DiscoveryIds,
                    Does.Contain("fragment.signal.koro.002"));
                var routeFailures = new List<string>();
                if (retainedMirraClimate.Length != 0)
                {
                    routeFailures.Add(
                        "Koro retains MirraClimateField and its route event.");
                }
                if (lensInstruments.Length != 1 ||
                    lensInstruments[0] == null ||
                    lensInstruments[0].StableId.Value !=
                    "instrument.koro.uv-spectrometer" ||
                    !lensInstruments[0].Supports(LensMode.Spectrum))
                {
                    routeFailures.Add(
                        "Koro does not bind its Spectrum-capable UV spectrometer.");
                }
                if (objectiveAfterLanding != "CROSS THE LOW-GRAVITY SHELVES")
                {
                    routeFailures.Add(
                        $"Landing objective stayed '{objectiveAfterLanding}'.");
                }
                if (checkpointAfterTraversal != 3)
                {
                    routeFailures.Add(
                        $"Real traversal stopped at checkpoint {checkpointAfterTraversal}.");
                }
                if (objectiveAfterTraversal != "COMPARE BOTH GEYSER SPECTRA")
                {
                    routeFailures.Add(
                        $"Traversal objective stayed '{objectiveAfterTraversal}'.");
                }
                Assert.That(routeFailures, Is.Empty,
                    string.Join("\n", routeFailures));
            }
            finally
            {
                transition.ReleaseBindings();
                await UnloadSceneAsync(scene);
                await UnloadSceneAsync(flightScene);
                await ((IGameService)progression).ShutdownAsync();
                await modes.ShutdownAsync();
                await input.ShutdownAsync();
                await saves.ShutdownAsync();
                await settings.ShutdownAsync();
            }
        }

        private static async Task ScanRealTargetAsync(
            SurfaceGameplayLifecycle2D lifecycle,
            GameModeController modes,
            Rigidbody2D captainBody,
            string phenomenonId)
        {
            var target = ReadField<DiscoveryLensTarget2D[]>(
                    lifecycle,
                    "lensTargets")
                .Single(item => item.TargetId == phenomenonId);
            captainBody.position = target.transform.position;
            var camera = lifecycle.LensController.CompositionCamera;
            camera.transform.position = new Vector3(
                target.transform.position.x,
                target.transform.position.y,
                camera.transform.position.z);
            Physics2D.SyncTransforms();

            await modes.EnterAsync(GameMode.Lens, CancellationToken.None);
            lifecycle.LensController.SelectMode(LensMode.Spectrum);
            lifecycle.LensController.Advance(0f, Vector2.zero, scanHeld: false);
            Assert.That(lifecycle.LensController.FocusedTarget, Is.SameAs(target),
                $"The real Lens must focus {phenomenonId} at its authored target.");
            var instrument = ReadField<InstrumentDefinition[]>(
                    lifecycle,
                    "lensInstruments")
                .Single(item => item.Supports(LensMode.Spectrum));
            lifecycle.LensController.Advance(
                instrument.ScanDurationSeconds,
                Vector2.zero,
                scanHeld: true);
            Assert.That(lifecycle.LensController.LastEvidence, Is.Not.Null);
            Assert.That(lifecycle.LensController.LastEvidence.PhenomenonId.Value,
                Is.EqualTo(phenomenonId));
            await modes.EnterAsync(GameMode.Surface, CancellationToken.None);
        }

        private static void DriveRealGeyserRhythm(
            Scene scene,
            object controller)
        {
            var geysers = FindByTypeName(scene, GeyserTypeName);
            var natural = geysers.Single(item =>
                !ReadField<bool>(item, "signalSource"));
            var signal = geysers.Single(item =>
                ReadField<bool>(item, "signalSource"));

            ApplyGeyserSample(natural, 4f);
            ApplyGeyserSample(signal, 4f);
            Invoke(controller, "Update");
            ApplyGeyserSample(natural, 2f);
            ApplyGeyserSample(signal, 4f);
            Invoke(controller, "Update");
            ApplyGeyserSample(natural, 4f);
            ApplyGeyserSample(signal, 5.6f);
            Invoke(controller, "Update");
            ApplyGeyserSample(natural, 10f);
            ApplyGeyserSample(signal, 10f);
            Invoke(controller, "Update");
        }

        private static void ApplyGeyserSample(Component geyser, float time)
        {
            var model = ReadField<object>(geyser, "m_Model");
            var sample = Invoke(model, "Sample", time, false);
            Invoke(geyser, "Apply", sample);
        }

        private static async Task WaitUntilAsync(
            Func<bool> predicate,
            float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            while (!predicate() && Time.realtimeSinceStartupAsDouble < deadline)
            {
                await Task.Yield();
            }
        }

        private static async Task AdvanceToSpectraAsync(
            object progression,
            GameEventBus events)
        {
            events.Publish(new ApproachCompleted(
                new ContentId("approach.vesper.gravity-route")));
            await InvokeTask(progression, "FlushPendingAsync", CancellationToken.None);
            events.Publish(new LandingCompleted(
                new ContentId("destination.koro.surface")));
            await InvokeTask(progression, "FlushPendingAsync", CancellationToken.None);
            events.Publish(new TraversalMilestoneReached(
                new ContentId("route.koro.low-gravity")));
            await InvokeTask(progression, "FlushPendingAsync", CancellationToken.None);
            events.Publish(new PhenomenonObserved(
                new ContentId("phenomenon.koro.geyser-natural")));
            events.Publish(new PhenomenonObserved(
                new ContentId("phenomenon.koro.geyser-signal")));
            events.Publish(new EvidenceAccepted(
                new ContentId("evidence.koro.spectrum-comparison"),
                new ContentId("prediction.koro.water-related-material")));
            await InvokeTask(progression, "FlushPendingAsync", CancellationToken.None);
        }

        private static async Task AdvanceCompleteRouteAsync(
            object progression,
            GameEventBus events)
        {
            await AdvanceToSpectraAsync(progression, events);
            events.Publish(new InteractionCompleted(
                new ContentId("interaction.koro.geyser-rhythm")));
            await InvokeTask(progression, "FlushPendingAsync", CancellationToken.None);
            events.Publish(new SignalFragmentRecovered(
                new ContentId("fragment.signal.koro.002")));
            await InvokeTask(progression, "FlushPendingAsync", CancellationToken.None);
        }

        private object CreateProgression(
            GameEventBus events,
            ISaveService saves,
            SettingsService settings)
        {
            var instance = Activator.CreateInstance(
                RequireType(ProgressionTypeName), events, saves, settings);
            Assert.That(instance, Is.InstanceOf<IGameService>());
            return instance;
        }

        private string CreateTemporaryRoot(string label)
        {
            var path = Path.Combine(
                Path.GetTempPath(),
                "JssTask25KoroVesper",
                label,
                Guid.NewGuid().ToString("N"));
            m_TemporaryRoots.Add(path);
            return path;
        }

        private static Type RequireType(string fullName)
        {
            var type = typeof(SurfaceMotor2D).Assembly.GetType(fullName);
            Assert.That(type, Is.Not.Null, $"Missing production type '{fullName}'.");
            return type;
        }

        private static Component[] FindByTypeName(Scene scene, string fullName)
        {
            var type = RequireType(fullName);
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren(type, true))
                .Cast<Component>()
                .ToArray();
        }

        private static T FindSingleInScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .SingleOrDefault();
        }

        private static GameObject FindNamed(
            IEnumerable<GameObject> roots,
            string name)
        {
            return roots.SelectMany(root =>
                    root.GetComponentsInChildren<Transform>(true))
                .Where(item => string.Equals(item.name, name, StringComparison.Ordinal))
                .Select(item => item.gameObject)
                .SingleOrDefault();
        }

        private static GameObject FindNamedWithComponent<T>(
            IEnumerable<GameObject> roots,
            string name)
            where T : Component
        {
            return roots.SelectMany(root =>
                    root.GetComponentsInChildren<Transform>(true))
                .Where(item => string.Equals(item.name, name, StringComparison.Ordinal))
                .Select(item => item.gameObject)
                .Where(item => item.GetComponent<T>() != null)
                .SingleOrDefault();
        }

        private static async Task<Scene> LoadSceneAsync(string name)
        {
            var operation = SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive);
            Assert.That(operation, Is.Not.Null, name);
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            var scene = SceneManager.GetSceneByName(name);
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True, name);
            Assert.That(SceneManager.SetActiveScene(scene), Is.True);
            return scene;
        }

        private static async Task UnloadSceneAsync(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            var recovery = SceneManager.CreateScene(
                "Task25Recovery_" + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(recovery);
            var operation = SceneManager.UnloadSceneAsync(scene);
            if (operation != null)
            {
                while (!operation.isDone)
                {
                    await Task.Yield();
                }
            }
        }

        private static object Invoke(object target, string method, params object[] args)
        {
            Assert.That(target, Is.Not.Null, method);
            var match = target.GetType().GetMethods(
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic)
                .Where(candidate => candidate.Name == method)
                .Single(candidate => candidate.GetParameters().Length == args.Length);
            return match.Invoke(target, args);
        }

        private static object InvokeStatic(
            Type type,
            string method,
            params object[] args)
        {
            var match = type.GetMethods(
                    BindingFlags.Static | BindingFlags.Public |
                    BindingFlags.NonPublic)
                .Where(candidate => candidate.Name == method)
                .Single(candidate => candidate.GetParameters().Length == args.Length);
            return match.Invoke(null, args);
        }

        private static async Task InvokeTask(
            object target,
            string method,
            params object[] args)
        {
            var result = Invoke(target, method, args);
            Assert.That(result, Is.InstanceOf<Task>());
            await (Task)result;
        }

        private static string ReadContentId(object target, string property)
        {
            return Read<ContentId>(target, property).Value;
        }

        private static IReadOnlyList<string> ReadStringList(
            object target,
            string property)
        {
            var value = target.GetType().GetProperty(property)?.GetValue(target);
            Assert.That(value, Is.InstanceOf<IEnumerable>());
            return ((IEnumerable)value).Cast<object>()
                .Select(item => item is ContentId id ? id.Value : item?.ToString())
                .ToArray();
        }

        private static IEnumerable<object> ReadEnumerable(
            object target,
            string property)
        {
            var value = target.GetType().GetProperty(property)?.GetValue(target);
            Assert.That(value, Is.InstanceOf<IEnumerable>());
            return ((IEnumerable)value).Cast<object>();
        }

        private static T Read<T>(object target, string property)
        {
            var info = target.GetType().GetProperty(
                property,
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null,
                $"{target.GetType().FullName}.{property}");
            return (T)info.GetValue(target);
        }

        private static T ReadField<T>(object target, string field)
        {
            var info = target.GetType().GetField(
                field,
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null,
                $"{target.GetType().FullName}.{field}");
            return (T)info.GetValue(target);
        }

        private static string Hash(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(bytes)
                .Select(value => value.ToString("x2")));
        }
    }
}
