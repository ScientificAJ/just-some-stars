using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Crew;
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
using UnityEngine.UI;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class MirraMissionTests
    {
        private const string TargetPath =
            "outputs/just-some-stars-2.5d-gameplay-target-v1.png";
        private const string TargetSha256 =
            "72644970448effd81177222e0aa23ae8a23f9b733077dab6e27e9ca765f5eaed";
        private const string FlightScene = "Task17FlightGraybox";
        private const string MirraScene = "Mirra";
        private const string ContentResource = "Task19MirraChapter";
        private const string ProgressionTypeName =
            "JustSomeStars.Runtime.Missions.MirraProgressionService";
        private const string ControllerTypeName =
            "JustSomeStars.Runtime.Missions.MirraMissionController2D";
        private const string ClimateTypeName =
            "JustSomeStars.Runtime.Discovery.MirraClimateField";
        private const string ParticipantTypeName =
            "JustSomeStars.Runtime.Interaction.MirraInteractionParticipant2D";
        private const string CrewRuntimeTypeName =
            "JustSomeStars.Runtime.Crew.MirraCrewRuntime2D";
        private const string CrewActorRuntimeTypeName =
            "JustSomeStars.Runtime.Crew.MirraCrewActorRuntime2D";

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
            foreach (var root in m_TemporaryRoots)
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }

            m_TemporaryRoots.Clear();
        }

        [Test]
        public async Task ProductionAssets_AreReachableAndPreserveApprovedMirra()
        {
#if UNITY_EDITOR
            var enabledBuildScenes = UnityEditor.EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => Path.GetFileNameWithoutExtension(scene.path))
                .ToArray();
            Assert.That(enabledBuildScenes, Does.Contain(FlightScene),
                "The internal player routes to flight through SceneManager, so " +
                "the flight scene must be enabled in the player build.");
            Assert.That(enabledBuildScenes, Does.Contain(MirraScene),
                "The landing route uses SceneManager, so Mirra must be enabled " +
                "in the player build.");
#endif
            Assert.That(Application.CanStreamedLevelBeLoaded(MirraScene), Is.True,
                "The final Mirra destination must be in the production build settings.");
            Assert.That(RequireType(ClimateTypeName), Is.Not.Null);
            Assert.That(RequireType(ControllerTypeName), Is.Not.Null);
            Assert.That(RequireType(ParticipantTypeName), Is.Not.Null);
            Assert.That(RequireType(CrewRuntimeTypeName), Is.Not.Null,
                "Mirra must integrate the Task 15 CrewDirector instead of " +
                "shipping static companion presenters.");
            Assert.That(RequireType(CrewActorRuntimeTypeName), Is.Not.Null);

            var targetBytes = File.ReadAllBytes(Path.GetFullPath(TargetPath));
            Assert.That(Hash(targetBytes), Is.EqualTo(TargetSha256),
                "Task 19 cannot mutate the approved 2.5D visual authority.");

            var content = Resources.Load<ScriptableObject>(ContentResource);
            Assert.That(content, Is.Not.Null,
                "The composition-owned progression service needs a runtime-loadable chapter catalog.");
            Invoke(content, "ValidateOrThrow");
            Assert.That(ReadContentId(content, "StableId"),
                Is.EqualTo("mission.mirra.chapter-one"));
            Assert.That(ReadStringList(content, "CheckpointNodeIds"),
                Is.EqualTo(new[]
                {
                    "mission.mirra.prelanding",
                    "mission.mirra.landed",
                    "mission.mirra.evidence",
                    "mission.mirra.repaired",
                    "mission.mirra.fragment",
                    "mission.mirra.departure-requested",
                    "mission.mirra.departed",
                }));

            var catalogHandle = Addressables.LoadAssetAsync<SceneCatalog>(
                SceneCatalog.AddressablesKey);
            try
            {
                await catalogHandle.Task;
                Assert.That(catalogHandle.Status,
                    Is.EqualTo(AsyncOperationStatus.Succeeded));
                var catalog = catalogHandle.Result;
                catalog.Validate();
                Assert.That(catalog.TryGetEntry(
                    "destination.mirra.approach",
                    out var approach), Is.True);
                Assert.That(approach.Address, Is.EqualTo(FlightScene));
                Assert.That(approach.TargetMode, Is.EqualTo(GameMode.Flight));
                Assert.That(catalog.TryGetEntry(
                    "destination.mirra.surface",
                    out var surface), Is.True);
                Assert.That(surface.Address, Is.EqualTo(MirraScene));
                Assert.That(surface.TargetMode, Is.EqualTo(GameMode.Surface));
            }
            finally
            {
                if (catalogHandle.IsValid())
                {
                    Addressables.Release(catalogHandle);
                }
            }

            var scene = await LoadSceneAsync(MirraScene);
            try
            {
                var roots = scene.GetRootGameObjects();
                var lifecycle = FindSingleInScene<SurfaceGameplayLifecycle2D>(scene);
                Assert.That(lifecycle, Is.Not.Null);
                Assert.That(FindByTypeName(scene, ControllerTypeName), Has.Length.EqualTo(1));
                var crewRuntime = FindByTypeName(scene, CrewRuntimeTypeName).Single();
                var crewActors = FindByTypeName(scene, CrewActorRuntimeTypeName);
                Assert.That(crewActors, Has.Length.EqualTo(3));
                Assert.That(ReadStringList(crewRuntime, "AuthoredActorIds"),
                    Is.EqualTo(new[] { "crew.mira", "crew.juno", "crew.ori" }));
                Assert.That(ReadField<TraversalGraph2D>(crewRuntime, "traversalGraph"),
                    Is.Not.Null,
                    "The production team must use Task 15's authored 2D graph.");
                foreach (var actor in crewActors)
                {
                    var recoveryAnchor = ReadField<Transform>(actor, "recoveryAnchor");
                    Assert.That(recoveryAnchor, Is.Not.Null);
                    Assert.That(recoveryAnchor, Is.Not.SameAs(actor.transform),
                        $"Crew actor '{actor.name}' cannot recover to itself.");
                    Assert.That(recoveryAnchor.IsChildOf(actor.transform), Is.False,
                        $"Crew actor '{actor.name}' needs an independent recovery anchor.");
                }
                var climate = FindByTypeName(scene, ClimateTypeName).Single();
                var participants = FindByTypeName(scene, ParticipantTypeName);
                Assert.That(participants, Has.Length.EqualTo(3),
                    "Probe repair must use Captain, Juno and Ori through the real runner.");
                foreach (var participant in participants.Where(
                             participant => participant.name != "Captain"))
                {
                    var blockingColliders = participant.GetComponents<Collider2D>()
                        .Where(collider => collider.enabled && !collider.isTrigger)
                        .Select(collider => collider.GetType().Name)
                        .ToArray();
                    Assert.That(blockingColliders, Is.Empty,
                        $"Interaction participant '{participant.name}' must not block " +
                        "the Captain's required hot/cold traversal route.");
                }

                Assert.That(
                    FindNamedWithComponent<Rigidbody2D>(roots, "Captain"),
                    Is.Not.Null);
                Assert.That(FindNamed(roots, "Mira"), Is.Not.Null);
                Assert.That(FindNamed(roots, "Juno"), Is.Not.Null);
                Assert.That(FindNamed(roots, "Ori"), Is.Not.Null);
                Assert.That(FindNamed(roots, "ProbeRepairAnchor.Captain"), Is.Not.Null);
                Assert.That(FindNamed(roots, "ProbeRepairAnchor.Juno"), Is.Not.Null);
                Assert.That(FindNamed(roots, "ProbeRepairAnchor.Ori"), Is.Not.Null);

                var bandsRoot = FindNamed(roots, "Bands");
                Assert.That(bandsRoot, Is.Not.Null);
                Assert.That(Enumerable.Range(0, bandsRoot.transform.childCount)
                        .Select(index => bandsRoot.transform.GetChild(index).name),
                    Is.EquivalentTo(new[]
                    {
                        "Sky", "FarWorld", "Atmosphere", "Midground",
                        "Gameplay", "ActorsAndProps", "Foreground", "Hud",
                    }));
                var parallax = roots.SelectMany(root =>
                        root.GetComponentsInChildren<ParallaxLayer2D>(true))
                    .ToArray();
                Assert.That(parallax, Has.Length.EqualTo(7),
                    "HUD is the fixed screen-space band; the other seven bands parallax.");
                Assert.That(CountComponentsByTypeName(scene,
                    "UnityEngine.Rendering.Universal.Light2D"),
                    Is.LessThanOrEqualTo(4));
                Assert.That(CountComponentsByTypeName(scene,
                    "UnityEngine.ParticleSystem"),
                    Is.LessThanOrEqualTo(3));
                var volumes = FindByTypeName(
                    scene,
                    "UnityEngine.Rendering.Volume");
                Assert.That(volumes, Has.Length.EqualTo(2),
                    "Mirra owns one gameplay grade and one dormant Photo Mode " +
                    "motion-blur volume.");
                Assert.That(volumes.Cast<Behaviour>().Count(item => item.enabled),
                    Is.EqualTo(1),
                    "Only the gameplay color grade may be active outside Photo Mode.");

                Canvas.ForceUpdateCanvases();
                var objective = FindNamed(roots, "MirraObjective")
                    .GetComponent<RectTransform>();
                var canvasRect = objective.GetComponentInParent<Canvas>()
                    .GetComponent<RectTransform>();
                var objectiveBounds = RectInCanvas(canvasRect, objective);
                var collisions = objective.parent.Cast<Transform>()
                    .Where(item => item != objective && item.gameObject.activeInHierarchy)
                    .Where(item => item.GetComponent<Graphic>() != null)
                    .Select(item => (item.name,
                        rect: RectInCanvas(canvasRect, (RectTransform)item)))
                    .Where(item => objectiveBounds.Overlaps(item.rect))
                    .Select(item => item.name)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray();
                Assert.That(collisions, Is.Empty,
                    "The mission objective must not overlap the retained route-marker HUD.");
                Assert.That(scene.GetRootGameObjects().SelectMany(root =>
                        root.GetComponentsInChildren<MeshRenderer>(true))
                    .Where(renderer => renderer.GetComponent<TMP_Text>() == null),
                    Is.Empty,
                    "Mirra may use a TMP label mesh, but no 3D gameplay geometry.");
                Assert.That(scene.GetRootGameObjects().SelectMany(root =>
                    root.GetComponentsInChildren<SkinnedMeshRenderer>(true)), Is.Empty);
                Assert.That(scene.GetRootGameObjects().SelectMany(root =>
                        root.GetComponentsInChildren<SurfaceRecovery2D>(true))
                    .Count(), Is.EqualTo(1));

                var hot = Invoke(climate, "Sample", new Vector2(-8f, 0f));
                var cold = Invoke(climate, "Sample", new Vector2(8f, 0f));
                Assert.That(ReadContentId(hot, "ZoneId"),
                    Is.EqualTo("climate.mirra.hot-side"));
                Assert.That(ReadContentId(cold, "ZoneId"),
                    Is.EqualTo("climate.mirra.cold-side"));
                Assert.That(Read<float>(hot, "TemperatureCelsius") -
                    Read<float>(cold, "TemperatureCelsius"),
                    Is.GreaterThanOrEqualTo(80f));
                Assert.That(Read<Vector2>(hot, "WindAcceleration"),
                    Is.Not.EqualTo(Read<Vector2>(cold, "WindAcceleration")));
            }
            finally
            {
                await UnloadSceneAsync(scene);
            }
        }

        [TestCase(AssistLevel.Guided)]
        [TestCase(AssistLevel.Balanced)]
        [TestCase(AssistLevel.Ace)]
        public async Task ProductionRoute_CompletesSameStoryForEveryAssistProfile(
            AssistLevel assist)
        {
            await using var harness = await ProductionHarness.CreateAsync(
                assist,
                CreateTemporaryRoot($"route-{assist}"),
                m_OwnedObjects);

            var landing = FindSingleInScene<LandingSequence>(
                SceneManager.GetActiveScene());
            Assert.That(landing, Is.Not.Null);
            Assert.That(await landing.TryLandAsync(
                approachIsValid: true,
                CancellationToken.None), Is.True);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(MirraScene));
            Assert.That(harness.Modes.CurrentMode, Is.EqualTo(GameMode.Surface));

            var controller = FindByTypeName(
                SceneManager.GetActiveScene(),
                ControllerTypeName).Single();
            var lifecycle = FindSingleInScene<SurfaceGameplayLifecycle2D>(
                SceneManager.GetActiveScene());
            var climate = FindByTypeName(
                SceneManager.GetActiveScene(),
                ClimateTypeName).Single();
            var body = FindNamedWithComponent<Rigidbody2D>(
                SceneManager.GetActiveScene().GetRootGameObjects(),
                "Captain").GetComponent<Rigidbody2D>();
            var objectiveLabel = ReadField<TMP_Text>(controller, "objectiveLabel");
            var crewRuntime = FindByTypeName(
                SceneManager.GetActiveScene(), CrewRuntimeTypeName).Single();

            Assert.That(Read<bool>(crewRuntime, "IsConfigured"), Is.True);
            await WaitUntilAsync(
                () => Read<int>(crewRuntime, "DecisionTickCount") > 0,
                0.5f);
            Assert.That(Read<int>(crewRuntime, "DecisionTickCount"),
                Is.GreaterThan(0),
                "The real scene must tick the Task 15 CrewDirector at runtime.");
            Assert.That(ReadStringList(crewRuntime, "ActiveActorIds"),
                Is.EqualTo(new[] { "crew.mira", "crew.juno", "crew.ori" }));

            Assert.That(objectiveLabel.text, Is.EqualTo(
                "Compare hot-side and cold-side readings with the thermal imager."),
                "The destination HUD must present Mirra's current authored objective, " +
                "not the source flight scene's stale landing instruction.");

            Assert.That(await InvokeTaskBool(controller, "TryRepairAsync",
                CancellationToken.None), Is.False,
                "Repair must fail closed before the authored Lens evidence.");
            Assert.That(await InvokeTaskBool(controller, "TryRepairAsync",
                CancellationToken.None), Is.False);
            var expectedHints = assist switch
            {
                AssistLevel.Guided => 1,
                AssistLevel.Balanced => 1,
                AssistLevel.Ace => 0,
                _ => throw new ArgumentOutOfRangeException(nameof(assist)),
            };
            await WaitForDialogueQuiescenceAsync(harness.Progression, 20f);
            Assert.That(Read<int>(controller, "HintPresentationCount"),
                Is.EqualTo(expectedHints));

            foreach (var x in new[] { 0f, -8f, 8f })
            {
                body.position = new Vector2(x, body.position.y);
                Physics2D.SyncTransforms();
                Invoke(climate, "EvaluateNow");
                await InvokeTask(harness.Progression, "FlushPendingAsync",
                    CancellationToken.None);

            }

            var target = FindSingleInScene<DiscoveryLensTarget2D>(
                SceneManager.GetActiveScene());
            body.position = target.transform.position;
            Physics2D.SyncTransforms();
            await harness.Modes.EnterAsync(GameMode.Lens, CancellationToken.None);
            lifecycle.LensController.SelectMode(LensMode.Temperature);
            lifecycle.LensController.Advance(0f, Vector2.zero, scanHeld: false);
            var instrument = ReadField<InstrumentDefinition[]>(
                lifecycle,
                "lensInstruments").Single(item => item.Supports(LensMode.Temperature));
            lifecycle.LensController.Advance(
                instrument.ScanDurationSeconds,
                Vector2.zero,
                scanHeld: true);
            Assert.That(lifecycle.LensController.LastEvidence, Is.Not.Null);
            Assert.That(lifecycle.LensController.LastEvidence.PredictionWasCorrect,
                Is.True);
            await InvokeTask(harness.Progression, "WaitForQuiescenceAsync",
                CancellationToken.None);
            await harness.Modes.EnterAsync(GameMode.Surface, CancellationToken.None);

            Assert.That(Read<int>(harness.Progression, "CheckpointOrdinal"),
                Is.EqualTo(3),
                "Accepted Lens evidence must persist automatically before " +
                "the probe interaction becomes available.");

            Assert.That(objectiveLabel.text, Is.EqualTo(
                "Repair the silent climate probe with Juno and Ori."),
                "Accepted evidence must update the live mission objective immediately.");

            Assert.That(await InvokeTaskBool(controller, "TryRepairAsync",
                CancellationToken.None), Is.True);
            Assert.That(objectiveLabel.text, Is.EqualTo(
                "Recover the Signal fragment at the violet spire."));
            Assert.That(Read<int>(controller, "ActiveLeaseCount"), Is.Zero);
            Assert.That(await InvokeTaskBool(controller, "TryRecoverFragmentAsync",
                CancellationToken.None), Is.True);
            Assert.That(objectiveLabel.text, Is.EqualTo(
                "Return to the lander and depart Mirra."));
            Assert.That(await InvokeTaskBool(controller, "TryDepartAsync",
                CancellationToken.None), Is.True);
            await InvokeTask(harness.Progression, "WaitForQuiescenceAsync",
                CancellationToken.None);

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(FlightScene));
            Assert.That(harness.Modes.CurrentMode, Is.EqualTo(GameMode.Flight));
            Assert.That(ReadStringList(harness.Progression, "EventHistory"),
                Is.EqualTo(new[]
                {
                    "ApproachCompleted:approach.mirra.safe",
                    "LandingCompleted:destination.mirra.surface",
                    "TraversalMilestoneReached:route.mirra.twilight",
                    "ClimateSampleObserved:climate.mirra.hot-side",
                    "ClimateSampleObserved:climate.mirra.cold-side",
                    "EvidenceAccepted:evidence.mirra.day-night-circulation",
                    "InteractionCompleted:interaction.mirra.probe-repair",
                    "SignalFragmentRecovered:fragment.signal.mirra.001",
                    "DepartureRequested:departure.mirra.return-to-flight",
                    "DepartureCompleted:departure.mirra.return-to-flight",
                }));
            Assert.That(Read<bool>(harness.Progression, "IsMissionComplete"), Is.True);
            Assert.That(ReadContentId(harness.Progression, "FragmentId"),
                Is.EqualTo("fragment.signal.mirra.001"));
            Assert.That(Read<int>(harness.Progression, "DuplicateEventCount"), Is.Zero);
            Assert.That(harness.Settings.Current.ScienceDepth,
                Is.EqualTo(ScienceDepth.Balanced));
        }

        [Test]
        public async Task ProductionLanding_SourceSceneCancellationCannotLoseDurableLanding()
        {
            await using var harness = await ProductionHarness.CreateAsync(
                AssistLevel.Balanced,
                CreateTemporaryRoot("landing-source-teardown"),
                m_OwnedObjects);

            harness.Events.Publish(new ApproachCompleted(
                new ContentId("approach.mirra.safe")));
            await InvokeTask(harness.Progression, "FlushPendingAsync",
                CancellationToken.None);
            Assert.That(Read<int>(harness.Progression, "CheckpointOrdinal"),
                Is.EqualTo(1));

            using var sourceLifetime = new CancellationTokenSource();
            var landingObject = new GameObject("SourceTeardownLanding");
            m_OwnedObjects.Add(landingObject);
            var landing = landingObject.AddComponent<LandingSequence>();
            var transition = new CompletingCancellationTransition(() =>
            {
                sourceLifetime.Cancel();
                landing.Cancel();
            });
            landing.Configure(
                transition,
                harness.Events,
                harness.Modes,
                (MirraProgressionService)harness.Progression);
            Assert.That(ReadField<MirraProgressionService>(landing, "progression"),
                Is.SameAs(harness.Progression));

            Assert.That(await landing.TryLandAsync(
                approachIsValid: true,
                sourceLifetime.Token), Is.True,
                "Once Unity completes the route, source-scene teardown " +
                "cannot cancel landing publication or persistence.");

            Assert.That(sourceLifetime.IsCancellationRequested, Is.True,
                "The test must reproduce the source-scene teardown boundary.");
            Assert.That(transition.ObservedCanceledRouteToken, Is.True,
                "The route must complete after its linked source token cancels.");
            Assert.That(Read<int>(harness.Progression, "CheckpointOrdinal"),
                Is.EqualTo(2));
            var loaded = await harness.Saves.LoadAsync(CancellationToken.None);
            Assert.That(loaded.HasSave, Is.True);
            Assert.That(loaded.Save.Mission.CheckpointOrdinal, Is.EqualTo(2));
            Assert.That(loaded.Save.Mission.CheckpointNodeId,
                Is.EqualTo("mission.mirra.landed"));
        }

        [Test]
        public async Task CheckpointPresentation_WaitsForTheDurableWrite()
        {
            var root = CreateTemporaryRoot("presentation-after-save");
            var settings = new SettingsService(Path.Combine(root, "settings.json"));
            var saves = new BlockingSaveService(Path.Combine(root, "save.json"));
            await settings.InitializeAsync(CancellationToken.None);
            await saves.InitializeAsync(CancellationToken.None);
            var bus = new GameEventBus();
            var progression = CreateProgression(bus, saves, settings);
            await ((IGameService)progression).InitializeAsync(CancellationToken.None);
            var observedObjectives = new List<string>();
            ((MirraProgressionService)progression).ObjectiveChanged +=
                observedObjectives.Add;
            try
            {
                await AdvanceToCheckpointAsync(progression, bus, 3);
                Assert.That(Read<int>(progression, "CheckpointOrdinal"), Is.EqualTo(3));
                observedObjectives.Clear();
                saves.BlockNextCheckpoint();

                Publish(bus,
                    "JustSomeStars.Runtime.Core.InteractionCompleted",
                    new ContentId("interaction.mirra.probe-repair"));

                Assert.That(observedObjectives, Is.Empty,
                    "The fragment objective/visual cannot appear before its " +
                    "checkpoint write completes.");
                await WaitUntilAsync(() => saves.SaveStarted, 0.75f);
                Assert.That(saves.SaveStarted, Is.True,
                    "Checkpoint 4 must be scheduled automatically by production.");
                Assert.That(Read<int>(progression, "CheckpointOrdinal"), Is.EqualTo(3));

                saves.ReleaseCheckpoint();
                await InvokeTask(progression, "WaitForQuiescenceAsync",
                    CancellationToken.None);
                Assert.That(Read<int>(progression, "CheckpointOrdinal"), Is.EqualTo(4));
                Assert.That(observedObjectives,
                    Is.EqualTo(new[] { "mission.mirra.fragment" }));
            }
            finally
            {
                saves.ReleaseCheckpoint();
                await ((IGameService)progression).ShutdownAsync();
                await saves.ShutdownAsync();
                await settings.ShutdownAsync();
            }
        }

        [Test]
        public async Task DepartureRouteFailure_RemainsRetryableThroughTheRealController()
        {
            var root = CreateTemporaryRoot("departure-retry");
            var settings = new SettingsService(Path.Combine(root, "settings.json"));
            var saves = new LocalSaveService(Path.Combine(root, "save.json"));
            var actions = UnityEngine.Object.Instantiate(InputSystem.actions);
            m_OwnedObjects.Add(actions);
            var input = new InputRouter(actions, settings);
            var modes = GameModeController.CreateForTests(
                GameMode.Surface,
                new InputRouterGameModeRuntimeHooks(input));
            await settings.InitializeAsync(CancellationToken.None);
            await saves.InitializeAsync(CancellationToken.None);
            await input.InitializeAsync(CancellationToken.None);
            await modes.InitializeAsync(CancellationToken.None);
            var events = new GameEventBus();
            var progression = CreateProgression(events, saves, settings);
            await ((IGameService)progression).InitializeAsync(CancellationToken.None);
            var retryTransition = new RetryableTransition();
            var dependencies = new SurfaceGameplayDependencies(
                settings,
                input,
                modes,
                events,
                saves,
                (MirraProgressionService)progression);
            dependencies.ConfigureSceneTransition(retryTransition);
            var controllerObject = new GameObject("DepartureControllerUnderTest");
            m_OwnedObjects.Add(controllerObject);
            var controller = controllerObject.AddComponent<MirraMissionController2D>();
            SetField(controller, "m_Dependencies", dependencies);
            try
            {
                await AdvanceToCheckpointAsync(progression, events, 4);
                Publish(events,
                    "JustSomeStars.Runtime.Core.SignalFragmentRecovered",
                    new ContentId("fragment.signal.mirra.001"));
                await InvokeTask(progression, "FlushPendingAsync",
                    CancellationToken.None);
                Assert.That(Read<int>(progression, "CheckpointOrdinal"), Is.EqualTo(5));

                try
                {
                    await InvokeTaskBool(controller, "TryDepartAsync",
                        CancellationToken.None);
                    Assert.Fail("The first route attempt must fail.");
                }
                catch (InvalidOperationException exception)
                {
                    Assert.That(exception.Message,
                        Is.EqualTo("Authentic first-attempt route failure."));
                }
                Assert.That(modes.CurrentMode, Is.EqualTo(GameMode.Surface));
                Assert.That(((MirraProgressionService)progression).IsActiveNode(
                    "mission.mirra.departure-requested"), Is.True,
                    "A failed route must leave departure available for retry.");
                Assert.That(Read<int>(progression, "CheckpointOrdinal"),
                    Is.EqualTo(5));

                Assert.That(await InvokeTaskBool(controller, "TryDepartAsync",
                    CancellationToken.None), Is.True);
                Assert.That(retryTransition.AttemptCount, Is.EqualTo(2));
                Assert.That(Read<bool>(progression, "IsMissionComplete"), Is.True);
                Assert.That(modes.CurrentMode, Is.EqualTo(GameMode.Flight));
            }
            finally
            {
                SetField<SurfaceGameplayDependencies>(
                    controller,
                    "m_Dependencies",
                    null);
                await ((IGameService)progression).ShutdownAsync();
                await modes.ShutdownAsync();
                await input.ShutdownAsync();
                await saves.ShutdownAsync();
                await settings.ShutdownAsync();
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public async Task CheckpointsAndFailures_RestoreWithoutSkippingOrLeaking(
            int checkpointOrdinal)
        {
            var root = CreateTemporaryRoot($"checkpoint-{checkpointOrdinal}");
            var savePath = Path.Combine(root, "save.json");
            var settings = new SettingsService(Path.Combine(root, "settings.json"));
            var saves = new LocalSaveService(savePath);
            await settings.InitializeAsync(CancellationToken.None);
            await saves.InitializeAsync(CancellationToken.None);
            var bus = new GameEventBus();
            var progression = CreateProgression(bus, saves, settings);
            await ((IGameService)progression).InitializeAsync(CancellationToken.None);
            try
            {
                await AdvanceToCheckpointAsync(
                    progression,
                    bus,
                    checkpointOrdinal);
                var durableBefore = Read<GameSave>(progression, "DurableSave");
                Assert.That(durableBefore.Mission.CheckpointOrdinal,
                    Is.EqualTo(checkpointOrdinal));

                await ((IGameService)progression).ShutdownAsync();
                progression = CreateProgression(bus, saves, settings);
                await ((IGameService)progression).InitializeAsync(
                    CancellationToken.None);

                Assert.That(Read<int>(progression, "CheckpointOrdinal"),
                    Is.EqualTo(checkpointOrdinal));
                Assert.That(Read<GameSave>(progression, "DurableSave"),
                    Is.EqualTo(durableBefore));
                Assert.That(Read<int>(progression, "DuplicateEventCount"), Is.Zero);
                Assert.That(Read<string>(progression, "ResumeSceneName"),
                    Is.EqualTo(checkpointOrdinal == 1 ? FlightScene : MirraScene));
                Assert.That(Read<GameMode>(progression, "ResumeMode"),
                    Is.EqualTo(checkpointOrdinal == 1
                        ? GameMode.Flight
                        : GameMode.Surface));

                if (checkpointOrdinal == 3)
                {
                    Publish(bus,
                        "JustSomeStars.Runtime.Core.PlayerBehaviorObserved",
                        new ContentId("interaction.mirra.probe-repair"),
                        PlayerBehaviorOutcome.RecoveryRequested);
                    Assert.That(Read<int>(progression, "CheckpointOrdinal"),
                        Is.EqualTo(3),
                        "A failed repair cannot silently grant its checkpoint.");
                    Publish(bus,
                        "JustSomeStars.Runtime.Core.InteractionCompleted",
                        new ContentId("interaction.mirra.probe-repair"));
                    await InvokeTask(progression, "FlushPendingAsync",
                        CancellationToken.None);
                    Assert.That(Read<int>(progression, "CheckpointOrdinal"),
                        Is.EqualTo(4));
                }

                Publish(bus,
                    "JustSomeStars.Runtime.Core.DepartureRequested",
                    new ContentId("departure.mirra.return-to-flight"));
                await InvokeTask(progression, "FlushPendingAsync",
                    CancellationToken.None);
                Assert.That(Read<bool>(progression, "IsMissionComplete"), Is.False,
                    "A failed scene transition must remain retryable and cannot complete departure.");
                Assert.That(Read<string>(progression, "ResumeSceneName"),
                    Is.EqualTo(checkpointOrdinal == 1 ? FlightScene : MirraScene));
                Assert.That(Read<GameMode>(progression, "ResumeMode"),
                    Is.EqualTo(checkpointOrdinal == 1
                        ? GameMode.Flight
                        : GameMode.Surface));
            }
            finally
            {
                await ((IGameService)progression).ShutdownAsync();
                await saves.ShutdownAsync();
                await settings.ShutdownAsync();
            }
        }

        private static async Task AdvanceToCheckpointAsync(
            object progression,
            GameEventBus bus,
            int ordinal)
        {
            Publish(bus,
                "JustSomeStars.Runtime.Core.ApproachCompleted",
                new ContentId("approach.mirra.safe"));
            await InvokeTask(progression, "FlushPendingAsync", CancellationToken.None);
            if (ordinal == 1)
            {
                return;
            }

            bus.Publish(new LandingCompleted(
                new ContentId("destination.mirra.surface")));
            await InvokeTask(progression, "FlushPendingAsync", CancellationToken.None);
            if (ordinal == 2)
            {
                return;
            }

            Publish(bus,
                "JustSomeStars.Runtime.Core.TraversalMilestoneReached",
                new ContentId("route.mirra.twilight"));
            Publish(bus,
                "JustSomeStars.Runtime.Core.ClimateSampleObserved",
                new ContentId("climate.mirra.hot-side"),
                112f,
                new Vector2(1.4f, 0.15f));
            Publish(bus,
                "JustSomeStars.Runtime.Core.ClimateSampleObserved",
                new ContentId("climate.mirra.cold-side"),
                -34f,
                new Vector2(-1.1f, 0.2f));
            Publish(bus,
                "JustSomeStars.Runtime.Core.EvidenceAccepted",
                new ContentId("evidence.mirra.day-night-circulation"),
                new ContentId("prediction.mirra.day-night-circulation"));
            await InvokeTask(progression, "FlushPendingAsync", CancellationToken.None);
            if (ordinal == 3)
            {
                return;
            }

            Publish(bus,
                "JustSomeStars.Runtime.Core.InteractionCompleted",
                new ContentId("interaction.mirra.probe-repair"));
            await InvokeTask(progression, "FlushPendingAsync", CancellationToken.None);
        }

        private object CreateProgression(
            GameEventBus bus,
            ISaveService saves,
            SettingsService settings)
        {
            var progression = Activator.CreateInstance(
                RequireType(ProgressionTypeName),
                bus,
                saves,
                settings);
            Assert.That(progression, Is.InstanceOf<IGameService>());
            return progression;
        }

        private string CreateTemporaryRoot(string label)
        {
            var path = Path.Combine(
                Path.GetTempPath(),
                "JssTask19Mirra",
                label,
                Guid.NewGuid().ToString("N"));
            m_TemporaryRoots.Add(path);
            return path;
        }

        private static Type RequireType(string fullName)
        {
            var type = typeof(SurfaceMotor2D).Assembly.GetType(fullName) ??
                AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType(fullName))
                    .FirstOrDefault(candidate => candidate != null);
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

        private static T FindSingleInScene<T>(Scene scene)
            where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .SingleOrDefault();
        }

        private static int CountComponentsByTypeName(Scene scene, string fullName)
        {
            var type = Type.GetType(fullName + ", UnityEngine.CoreModule") ??
                AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType(fullName))
                    .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, fullName);
            return scene.GetRootGameObjects()
                .Sum(root => root.GetComponentsInChildren(type, true).Length);
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

        private static Rect RectInCanvas(
            RectTransform canvas,
            RectTransform target)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            var minimum = (Vector2)canvas.InverseTransformPoint(corners[0]);
            var maximum = minimum;
            for (var index = 1; index < corners.Length; index++)
            {
                var point = (Vector2)canvas.InverseTransformPoint(corners[index]);
                minimum = Vector2.Min(minimum, point);
                maximum = Vector2.Max(maximum, point);
            }

            return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
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
                "Task19Recovery_" + Guid.NewGuid().ToString("N"));
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

        private static async Task WaitUntilAsync(
            Func<bool> predicate,
            float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            do
            {
                if (predicate())
                {
                    return;
                }

                await Task.Yield();
            }
            while (Time.realtimeSinceStartup < deadline);
        }

        private static object Invoke(object target, string method, params object[] args)
        {
            Assert.That(target, Is.Not.Null, method);
            var match = target.GetType().GetMethods(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(candidate => candidate.Name == method)
                .Single(candidate => candidate.GetParameters().Length == args.Length);
            return match.Invoke(target, args);
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

        private static async Task<bool> InvokeTaskBool(
            object target,
            string method,
            params object[] args)
        {
            var result = Invoke(target, method, args);
            Assert.That(result, Is.InstanceOf<Task<bool>>());
            return await (Task<bool>)result;
        }

        private static T Read<T>(object target, string property)
        {
            var value = target.GetType().GetProperty(
                property,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(value, Is.Not.Null, property);
            return (T)value.GetValue(target);
        }

        private static T ReadField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(target);
        }

        private static void SetField<T>(
            object target,
            string fieldName,
            T value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static string ReadContentId(object target, string property)
        {
            return ((ContentId)Read<object>(target, property)).Value;
        }

        private static string[] ReadStringList(object target, string property)
        {
            return ((System.Collections.IEnumerable)Read<object>(target, property))
                .Cast<object>()
                .Select(value => value is ContentId id ? id.Value : value.ToString())
                .ToArray();
        }

        private static void Publish(
            GameEventBus bus,
            string eventTypeName,
            params object[] constructorArguments)
        {
            var eventType = RequireType(eventTypeName);
            var gameEvent = Activator.CreateInstance(eventType, constructorArguments);
            var publish = typeof(GameEventBus).GetMethod(nameof(GameEventBus.Publish))
                .MakeGenericMethod(eventType);
            publish.Invoke(bus, new[] { gameEvent });
        }

        private static string Hash(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(bytes)
                .Select(value => value.ToString("x2")));
        }

        private sealed class CompletingCancellationTransition : ISceneTransition
        {
            private readonly Action m_Complete;

            public CompletingCancellationTransition(Action complete)
            {
                m_Complete = complete ?? throw new ArgumentNullException(
                    nameof(complete));
            }

            public bool ObservedCanceledRouteToken { get; private set; }

            public ValueTask RouteAsync(
                string destination,
                CancellationToken cancellationToken)
            {
                m_Complete();
                ObservedCanceledRouteToken = cancellationToken.IsCancellationRequested;
                return default;
            }
        }

        private static async Task WaitForDialogueQuiescenceAsync(
            object progression,
            float timeoutSeconds)
        {
            var director = ReadField<object>(progression, "m_Dialogue");
            var deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                var completion = ReadField<Task>(director, "m_CurrentCompletion");
                var pending = (System.Collections.IEnumerable)Read<object>(
                    director,
                    "Pending");
                if (completion.IsCompleted && !pending.Cast<object>().Any())
                {
                    await Task.Yield();
                    var stableCompletion = ReadField<Task>(
                        director,
                        "m_CurrentCompletion");
                    var stablePending = (System.Collections.IEnumerable)Read<object>(
                        director,
                        "Pending");
                    if (ReferenceEquals(completion, stableCompletion) &&
                        stableCompletion.IsCompleted &&
                        !stablePending.Cast<object>().Any())
                    {
                        await stableCompletion;
                        return;
                    }
                }

                await Task.Yield();
            }

            var active = ReadField<Task>(director, "m_CurrentCompletion");
            var queued = (System.Collections.IEnumerable)Read<object>(
                director,
                "Pending");
            var router = ReadField<object>(progression, "m_DialogueRouter");
            var presenter = ReadField<object>(router, "m_Presenter");
            var dialogueId = presenter == null
                ? "<unbound>"
                : Read<string>(presenter, "CurrentDialogueId");
            var presentationCount = presenter == null
                ? 0
                : Read<int>(presenter, "PresentationCount");
            var actor = presenter == null
                ? null
                : ReadField<Component>(presenter, "m_CurrentActor");
            var animator = actor == null
                ? null
                : ReadField<Component>(actor, "spriteAnimator");
            var clip = animator == null
                ? null
                : Read<object>(animator, "CurrentClip");
            var clipId = clip == null
                ? "<none>"
                : Read<string>(clip, "StableId");
            var frame = animator == null
                ? -1
                : Read<int>(animator, "CurrentFrameIndex");
            var playing = animator != null && Read<bool>(animator, "IsPlaying");
            throw new TimeoutException(
                $"Mirra dialogue did not become quiescent within " +
                $"{timeoutSeconds:0.#} seconds (active={active.Status}, " +
                $"pending={queued.Cast<object>().Count()}, " +
                $"dialogue='{dialogueId}', presentations={presentationCount}, " +
                $"actorActive={actor != null && actor.gameObject.activeInHierarchy}, " +
                $"animatorEnabled={animator is Behaviour behaviour && behaviour.enabled}, " +
                $"clip='{clipId}', frame={frame}, playing={playing}).");
        }

        private sealed class RetryableTransition : ISceneTransition
        {
            public int AttemptCount { get; private set; }

            public ValueTask RouteAsync(
                string destination,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Assert.That(destination, Is.EqualTo(FlightScene));
                AttemptCount++;
                if (AttemptCount == 1)
                {
                    throw new InvalidOperationException(
                        "Authentic first-attempt route failure.");
                }

                return default;
            }
        }

        private sealed class BlockingSaveService : ISaveService
        {
            private readonly LocalSaveService m_Inner;
            private TaskCompletionSource<bool> m_SaveStarted;
            private TaskCompletionSource<bool> m_SaveRelease;
            private bool m_BlockNext;

            public BlockingSaveService(string path)
            {
                m_Inner = new LocalSaveService(path);
            }

            public bool SaveStarted => m_SaveStarted?.Task.IsCompleted == true;

            public ValueTask<StartupResult> InitializeAsync(
                CancellationToken cancellationToken) =>
                m_Inner.InitializeAsync(cancellationToken);

            public ValueTask ShutdownAsync() => m_Inner.ShutdownAsync();

            public ValueTask<LoadSaveResult> LoadAsync(
                CancellationToken cancellationToken) =>
                m_Inner.LoadAsync(cancellationToken);

            public async ValueTask SaveCheckpointAsync(
                GameSave save,
                CancellationToken cancellationToken)
            {
                if (m_BlockNext)
                {
                    m_BlockNext = false;
                    m_SaveStarted.TrySetResult(true);
                    await m_SaveRelease.Task;
                    cancellationToken.ThrowIfCancellationRequested();
                }

                await m_Inner.SaveCheckpointAsync(save, cancellationToken);
            }

            public ValueTask<LoadSaveResult> RecoverAsync(
                CancellationToken cancellationToken) =>
                m_Inner.RecoverAsync(cancellationToken);

            public GameSave Merge(GameSave local, GameSave cloud) =>
                m_Inner.Merge(local, cloud);

            public void BlockNextCheckpoint()
            {
                m_SaveStarted = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                m_SaveRelease = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                m_BlockNext = true;
            }

            public void ReleaseCheckpoint()
            {
                m_SaveRelease?.TrySetResult(true);
            }
        }

        private sealed class ProductionHarness : IAsyncDisposable
        {
            private readonly string m_Root;
            private readonly InputActionAsset m_Actions;
            private readonly LocalSaveService m_Saves;
            private readonly UnitySceneTransition m_Transition;

            private ProductionHarness(
                string root,
                SettingsService settings,
                InputActionAsset actions,
                InputRouter input,
                GameModeController modes,
                LocalSaveService saves,
                GameEventBus events,
                object progression,
                UnitySceneTransition transition)
            {
                m_Root = root;
                Settings = settings;
                m_Actions = actions;
                Input = input;
                Modes = modes;
                m_Saves = saves;
                Events = events;
                Progression = progression;
                m_Transition = transition;
            }

            public SettingsService Settings { get; }
            public InputRouter Input { get; }
            public GameModeController Modes { get; }
            public LocalSaveService Saves => m_Saves;
            public GameEventBus Events { get; }
            public object Progression { get; }

            public static async Task<ProductionHarness> CreateAsync(
                AssistLevel assist,
                string root,
                ICollection<UnityEngine.Object> ownedObjects)
            {
                var settings = new SettingsService(Path.Combine(root, "settings.json"));
                var saves = new LocalSaveService(Path.Combine(root, "save.json"));
                var actions = UnityEngine.Object.Instantiate(InputSystem.actions);
                ownedObjects.Add(actions);
                var input = new InputRouter(actions, settings);
                var modes = GameModeController.CreateForTests(
                    GameMode.Flight,
                    new InputRouterGameModeRuntimeHooks(input));
                await settings.InitializeAsync(CancellationToken.None);
                var configured = settings.Current;
                configured.PilotingAssist = assist;
                configured.ExplorationAssist = assist;
                configured.ScienceDepth = ScienceDepth.Balanced;
                settings.Apply(configured);
                Assert.That(settings.Current.PilotingAssist, Is.EqualTo(assist));
                Assert.That(settings.Current.ExplorationAssist, Is.EqualTo(assist));
                Assert.That(settings.Current.ScienceDepth,
                    Is.EqualTo(ScienceDepth.Balanced));
                await saves.InitializeAsync(CancellationToken.None);
                await input.InitializeAsync(CancellationToken.None);
                await modes.InitializeAsync(CancellationToken.None);

                var bus = new GameEventBus();
                var progression = Activator.CreateInstance(
                    RequireType(ProgressionTypeName),
                    bus,
                    saves,
                    settings);
                await ((IGameService)progression).InitializeAsync(
                    CancellationToken.None);

                var surfaceDependencies = new SurfaceGameplayDependencies(
                    settings,
                    input,
                    modes,
                    bus,
                    saves,
                    (IChapterProgression)progression);
                var transition = new UnitySceneTransition(
                    new FrontendDependencies(settings, input),
                    surfaceDependencies);
                Invoke(surfaceDependencies, "ConfigureSceneTransition", transition);
                var flightDependencies = new FlightGameplayDependencies(
                    settings,
                    input,
                    modes,
                    bus,
                    transition,
                    (IChapterProgression)progression,
                    saves: saves);
                Invoke(transition, "ConfigureFlightDependencies", flightDependencies);

                await LoadSceneAsync(FlightScene);
                Invoke(transition, "BindActiveScene");
                return new ProductionHarness(
                    root,
                    settings,
                    actions,
                    input,
                    modes,
                    saves,
                    bus,
                    progression,
                    transition);
            }

            public async ValueTask DisposeAsync()
            {
                m_Transition.ReleaseBindings();
                await ((IGameService)Progression).ShutdownAsync();
                await Modes.ShutdownAsync();
                await Input.ShutdownAsync();
                await m_Saves.ShutdownAsync();
                await Settings.ShutdownAsync();
                if (m_Actions != null)
                {
                    UnityEngine.Object.DestroyImmediate(m_Actions);
                }

                var active = SceneManager.GetActiveScene();
                if (active.IsValid() && active.isLoaded)
                {
                    await UnloadSceneAsync(active);
                }

                if (Directory.Exists(m_Root))
                {
                    Directory.Delete(m_Root, recursive: true);
                }
            }
        }
    }
}
