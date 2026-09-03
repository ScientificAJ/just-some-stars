using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Animation2D;
using JustSomeStars.Runtime.Cinematics;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Input;
using JustSomeStars.Runtime.Missions;
using JustSomeStars.Runtime.Saving;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class ChapterOneCompletionTests
    {
        private static readonly string[] s_ProductionScenes =
        {
            "AsterVeil",
            "SignalReassembly",
            "Clubhouse",
            "Opening",
            "DinnerEnding",
        };

        [Test]
        public void SaveV3_MigratesToExplicitIncompleteChapterOneAndCloudRoundTrips()
        {
            Assert.That(GameSave.CurrentSchemaVersion, Is.EqualTo(5),
                "Task 26 requires an explicit Chapter One durability schema.");

            var current = GameSave.CreateNew("save.task26.schema", 2600);
            var chapter = RequireProperty(current, "ChapterOne");
            AssertChapterState(chapter, "NotStarted", false, false, false);

            var serializer = new JsonSaveSerializer(SaveMigrator.CreateCurrent());
            var currentJson = serializer.Serialize(current);
            var legacyJson = Regex.Replace(
                currentJson,
                "\\s*\\\"chapterOne\\\"\\s*:\\s*\\{[^}]*\\},?",
                string.Empty,
                RegexOptions.CultureInvariant);
            legacyJson = legacyJson.Replace(
                "\"schemaVersion\": 5",
                "\"schemaVersion\": 3");

            Assert.That(serializer.TryDeserialize(legacyJson, out var migrated),
                Is.True, "A real schema-v3 save must migrate through the current chain.");
            Assert.That(migrated.SchemaVersion, Is.EqualTo(5));
            AssertChapterState(
                RequireProperty(migrated, "ChapterOne"),
                "NotStarted",
                false,
                false,
                false);

            var prior = GameSave.CreateNew("save.task26.koro", 2601);
            prior.Mission = new MissionProgress
            {
                MissionId = KoroVesperProgressionService.MissionId,
                CheckpointNodeId = "mission.koro-vesper.fragment",
                CheckpointOrdinal = 6,
                CompletedNodeIds = new[]
                {
                    "mission.koro-vesper.approach",
                    "mission.koro-vesper.landed",
                    "mission.koro-vesper.traversal",
                    "mission.koro-vesper.spectra",
                    "mission.koro-vesper.rhythm",
                    "mission.koro-vesper.fragment",
                },
                ActiveNodeIds = Array.Empty<string>(),
            };
            prior.Story.CheckpointId = "mission.koro-vesper.fragment";
            prior.Story.CheckpointOrdinal = 13;
            prior.DiscoveryIds = new[]
            {
                "fragment.signal.mirra.001",
                KoroVesperProgressionService.FragmentIdValue,
            };
            currentJson = serializer.Serialize(prior);
            legacyJson = Regex.Replace(
                currentJson,
                "\\s*\\\"chapterOne\\\"\\s*:\\s*\\{[^}]*\\},?",
                string.Empty,
                RegexOptions.CultureInvariant).Replace(
                "\"schemaVersion\": 5",
                "\"schemaVersion\": 3");
            Assert.That(serializer.TryDeserialize(legacyJson, out migrated), Is.True);
            AssertChapterState(
                RequireProperty(migrated, "ChapterOne"),
                "KoroComplete",
                false,
                false,
                false);
        }

        [Test]
        public async Task ChapterOneState_MergesMonotonicallyAndSurvivesCloudProjection()
        {
            var local = GameSave.CreateNew("save.task26.merge", 2700);
            var cloud = local.Copy();
            SetChapterState(local, "AsterFragmentRecovered", false, false);
            SetChapterState(cloud, "ReturnedHome", true, false);
            local.Mission = AsterMission(5);
            cloud.Mission = AsterMission(8);
            local.Story.CheckpointId = local.Mission.CheckpointNodeId;
            local.Story.CheckpointOrdinal = 18;
            cloud.Story.CheckpointId = cloud.Mission.CheckpointNodeId;
            cloud.Story.CheckpointOrdinal = 21;

            var merged = SaveMerge.Combine(local, cloud);
            AssertChapterState(
                RequireProperty(merged, "ChapterOne"),
                "ReturnedHome",
                true,
                false,
                false);

            var gateway = new MemoryFirestoreGateway();
            var service = new FirestoreCloudSaveService(gateway);
            await service.UploadAsync("task26-user", merged, CancellationToken.None);
            var downloaded = await service.DownloadAsync(
                "task26-user", CancellationToken.None);
            Assert.That(downloaded.HasValue, Is.True);
            AssertChapterState(
                RequireProperty(downloaded.Value.Save, "ChapterOne"),
                "ReturnedHome",
                true,
                false,
                false);

            var olderChapter = GameSave.CreateNew("save.task26.older", 2701);
            olderChapter.Mission = new MissionProgress
            {
                MissionId = KoroVesperProgressionService.MissionId,
                CheckpointNodeId = "mission.koro-vesper.fragment",
                CheckpointOrdinal = 6,
                CompletedNodeIds = Array.Empty<string>(),
                ActiveNodeIds = Array.Empty<string>(),
            };
            olderChapter.Story.CheckpointId = "mission.koro-vesper.fragment";
            olderChapter.Story.CheckpointOrdinal = 13;
            Assert.That(
                SaveMerge.Combine(olderChapter, local).Mission.MissionId,
                Is.EqualTo("mission.aster-veil.chapter-one"),
                "Aster must be authored after Koro in local/cloud conflict ordering.");
        }

        [Test]
        public void DebrisField_IsDeterministicRecoverableAndCollisionClean()
        {
            var simulationType = RequireType(
                "JustSomeStars.Runtime.Flight.DebrisFieldSimulation");
            var first = Activator.CreateInstance(simulationType, 260826);
            var second = Activator.CreateInstance(simulationType, 260826);
            var variance = Activator.CreateInstance(simulationType, 260827);

            for (var tick = 0; tick < 180; tick++)
            {
                var steering = Mathf.Sin(tick * 0.073f) * 0.7f;
                Invoke(first, "Step", steering);
                Invoke(second, "Step", steering);
                Invoke(variance, "Step", steering);
            }

            var firstToken = RequireProperty(first, "StateToken").ToString();
            Assert.That(RequireProperty(second, "StateToken").ToString(),
                Is.EqualTo(firstToken));
            Assert.That(RequireProperty(variance, "StateToken").ToString(),
                Is.Not.EqualTo(firstToken));

            var checkpoint = Invoke(first, "CaptureCheckpoint");
            var checkpointToken = RequireProperty(first, "StateToken").ToString();
            Invoke(first, "RegisterCollision", 3);
            for (var tick = 0; tick < 40; tick++)
            {
                Invoke(first, "Step", -0.4f);
            }
            Assert.That(RequireProperty(first, "StateToken").ToString(),
                Is.Not.EqualTo(checkpointToken));

            Invoke(first, "RestoreCheckpoint", checkpoint);
            Assert.That(RequireProperty(first, "StateToken").ToString(),
                Is.EqualTo(checkpointToken));
            Assert.That(RequireProperty(first, "ActiveCollisionCount"), Is.EqualTo(0),
                "Recovery must clear stale debris collision ownership.");
        }

        [Test]
        public async Task CleanSave_TravelsOpeningMirraKoroAsterToDurableCredits()
        {
            var saves = new RecordingSaveService(null);
            var events = new GameEventBus();
            var settingsPath = System.IO.Path.Combine(
                Application.temporaryCachePath,
                "task26-settings-" + Guid.NewGuid().ToString("N") + ".json");
            var settings = new SettingsService(settingsPath);
            await settings.InitializeAsync(CancellationToken.None);
            await saves.InitializeAsync(CancellationToken.None);

            var actions = UnityEngine.Object.Instantiate(InputSystem.actions);
            var input = new InputRouter(actions, settings);
            var modes = GameModeController.CreateForTests(GameMode.Clubhouse);
            await input.InitializeAsync(CancellationToken.None);
            await modes.InitializeAsync(CancellationToken.None);
            var routeEvents = new List<string>();
            var scenes = new RecordingSceneTransition(routeEvents);

            var mirra = new MirraProgressionService(events, saves, settings);
            var openingObject = new GameObject("CleanSaveOpeningJourney");
            var opening = openingObject.AddComponent<ChapterOneSequenceController2D>();
            SetPrivateField(opening, "sequenceKind", ChapterOneSequenceKind.Opening);
            SetPrivateField(
                opening,
                "m_Dependencies",
                new ChapterOneSequenceDependencies(
                    saves,
                    input,
                    modes,
                    events,
                    scenes,
                    mirra));
            await opening.CompleteOpeningAsync(CancellationToken.None);
            SetPrivateField<ChapterOneSequenceDependencies>(
                opening,
                "m_Dependencies",
                null);
            UnityEngine.Object.DestroyImmediate(openingObject);
            Assert.That(saves.Current.ChapterOne.Phase,
                Is.EqualTo(ChapterOnePhase.OpeningComplete));
            Assert.That(routeEvents, Does.Contain("route:Task17FlightGraybox"));

            var mirraStartup = await mirra.InitializeAsync(CancellationToken.None);
            Assert.That(mirraStartup.State, Is.EqualTo(StartupResultState.Available));
            await PublishAndFlush(events, mirra,
                new ApproachCompleted(new ContentId("approach.mirra.safe")));
            await PublishAndFlush(events, mirra,
                new LandingCompleted(new ContentId("destination.mirra.surface")));
            events.Publish(new TraversalMilestoneReached(
                new ContentId("route.mirra.twilight")));
            events.Publish(new ClimateSampleObserved(
                new ContentId("climate.mirra.hot-side"),
                112f,
                new Vector2(1.4f, 0.15f)));
            events.Publish(new ClimateSampleObserved(
                new ContentId("climate.mirra.cold-side"),
                -34f,
                new Vector2(-1.1f, 0.2f)));
            events.Publish(new EvidenceAccepted(
                new ContentId("evidence.mirra.day-night-circulation"),
                new ContentId("prediction.mirra.day-night-circulation")));
            await mirra.FlushPendingAsync(CancellationToken.None);
            await PublishAndFlush(events, mirra,
                new InteractionCompleted(
                    new ContentId("interaction.mirra.probe-repair")));
            await PublishAndFlush(events, mirra,
                new SignalFragmentRecovered(
                    new ContentId("fragment.signal.mirra.001")));
            events.Publish(new DepartureRequested(
                new ContentId("departure.mirra.return-to-flight")));
            await PublishAndFlush(events, mirra,
                new DepartureCompleted(
                    new ContentId("departure.mirra.return-to-flight")));
            Assert.That(mirra.IsMissionComplete, Is.True);
            await mirra.ShutdownAsync();

            var koro = new KoroVesperProgressionService(events, saves, settings);
            var koroStartup = await koro.InitializeAsync(CancellationToken.None);
            Assert.That(koroStartup.State, Is.EqualTo(StartupResultState.Available));
            await PublishAndFlush(events, koro,
                new ApproachCompleted(koro.ApproachId));
            await PublishAndFlush(events, koro,
                new LandingCompleted(new ContentId("destination.koro.surface")));
            await PublishAndFlush(events, koro,
                new TraversalMilestoneReached(
                    new ContentId("route.koro.low-gravity")));
            events.Publish(new PhenomenonObserved(
                new ContentId("phenomenon.koro.geyser-natural")));
            events.Publish(new PhenomenonObserved(
                new ContentId("phenomenon.koro.geyser-signal")));
            events.Publish(new EvidenceAccepted(
                new ContentId("evidence.koro.spectrum-comparison"),
                new ContentId("prediction.koro.water-related-material")));
            await koro.FlushPendingAsync(CancellationToken.None);
            await PublishAndFlush(events, koro,
                new InteractionCompleted(
                    new ContentId("interaction.koro.geyser-rhythm")));
            await PublishAndFlush(events, koro,
                new SignalFragmentRecovered(koro.FragmentId));
            Assert.That(koro.IsMissionComplete, Is.True);
            await koro.ShutdownAsync();

            var progressionType = RequireType(
                "JustSomeStars.Runtime.Missions.AsterVeilProgressionService");
            var progression = (IChapterProgression)Activator.CreateInstance(
                progressionType, events, saves, settings);
            var startup = await progression.InitializeAsync(CancellationToken.None);
            Assert.That(startup.State, Is.EqualTo(StartupResultState.Available));
            Assert.That(progression.ResumeSceneName, Is.EqualTo("AsterVeil"));

            await PublishAndFlush(
                events, progression,
                new ApproachCompleted(new ContentId("approach.aster.gravity-route")));
            await PublishAndFlush(
                events, progression,
                new InteractionCompleted(new ContentId(
                    "interaction.aster.route-committed")));
            await PublishAndFlush(
                events, progression,
                new PhenomenonObserved(new ContentId(
                    "phenomenon.aster.relative-motion")));
            await PublishAndFlush(
                events, progression,
                new TraversalMilestoneReached(new ContentId(
                    "route.aster.debris-lane-cleared")));
            await PublishAndFlush(
                events, progression,
                new SignalFragmentRecovered(new ContentId(
                    "fragment.signal.aster.003")));
            await PublishAndFlush(
                events, progression,
                new SignalFragmentRecovered(new ContentId(
                    "fragment.signal.aster.003")));
            await PublishAndFlush(
                events, progression,
                new InteractionCompleted(new ContentId(
                    "interaction.signal.reassemble")));
            await PublishAndFlush(
                events, progression,
                new DepartureCompleted(new ContentId(
                    "departure.aster.escape")));
            await PublishAndFlush(
                events, progression,
                new LandingCompleted(new ContentId(
                    "destination.clubhouse.return")));
            await PublishAndFlush(
                events, progression,
                new ConversationCompleted(new ContentId(
                    "conversation.dinner.just-some-stars")));

            var creditsOrder = -1;
            Action presentCredits = () => creditsOrder = saves.OperationOrder.Count;
            var complete = progressionType.GetMethod(
                "CompleteFinalPulseAndUnlockCreditsAsync",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(complete, Is.Not.Null);
            var completionTask = (Task)complete.Invoke(
                progression,
                new object[] { presentCredits, CancellationToken.None });
            await completionTask;

            var durable = saves.Current;
            Assert.That(durable.DiscoveryIds.Count(id => id ==
                "fragment.signal.aster.003"), Is.EqualTo(1));
            Assert.That(durable.DiscoveryIds,
                Is.SupersetOf(new[]
                {
                    "fragment.signal.mirra.001",
                    KoroVesperProgressionService.FragmentIdValue,
                    "fragment.signal.aster.003",
                    "map.signal.beyond-aurelia",
                    "signal.pulse.recent",
                }));
            AssertChapterState(
                RequireProperty(durable, "ChapterOne"),
                "DinnerComplete",
                true,
                true,
                true);
            Assert.That(creditsOrder, Is.GreaterThan(0));
            Assert.That(saves.OperationOrder[creditsOrder - 1], Is.EqualTo("save"),
                "Final completion must be durable before story credits begin.");

            await progression.ShutdownAsync();
            await modes.ShutdownAsync();
            await input.ShutdownAsync();
            UnityEngine.Object.DestroyImmediate(actions);
            await saves.ShutdownAsync();
            await settings.ShutdownAsync();
        }

        [UnityTest]
        public IEnumerator ProductionScenes_AreReachableLayeredAndUseSavedCaptain()
        {
            foreach (var sceneName in s_ProductionScenes)
            {
                AsyncOperationHandle<SceneInstance>? addressable = null;
                Scene scene;
                if (sceneName == "Clubhouse")
                {
                    Assert.That(Application.CanStreamedLevelBeLoaded(sceneName), Is.True,
                        "The safe Clubhouse fallback must be locally loadable.");
                    var operation = SceneManager.LoadSceneAsync(
                        sceneName,
                        LoadSceneMode.Additive);
                    Assert.That(operation, Is.Not.Null);
                    while (!operation.isDone) yield return null;
                    scene = SceneManager.GetSceneByName(sceneName);
                }
                else
                {
                    var handle = Addressables.LoadSceneAsync(
                        sceneName,
                        LoadSceneMode.Additive,
                        true);
                    addressable = handle;
                    yield return handle;
                    Assert.That(handle.Status, Is.EqualTo(AsyncOperationStatus.Succeeded),
                        $"Addressables could not load production scene '{sceneName}'.");
                    scene = handle.Result.Scene;
                }

                Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
                var roots = scene.GetRootGameObjects();
                Assert.That(roots.Select(root => root.name),
                    Is.SupersetOf(new[]
                    {
                        "Sky",
                        "FarWorld",
                        "Atmosphere",
                        "Midground",
                        "Gameplay",
                        "ActorsAndProps",
                        "Foreground",
                        "HUD",
                    }), $"{sceneName} must publish all eight independent 2.5D bands.");
                Assert.That(
                    roots.SelectMany(root => root.GetComponentsInChildren<MeshRenderer>(true)),
                    Is.Empty, $"{sceneName} must remain a 2D production scene.");
                Assert.That(
                    roots.SelectMany(root =>
                        root.GetComponentsInChildren<SkinnedMeshRenderer>(true)),
                    Is.Empty, $"{sceneName} must not regress to a 3D character rig.");

                if (sceneName == "AsterVeil")
                {
                    var environmentBands = new HashSet<string>(StringComparer.Ordinal)
                    {
                        "Sky", "FarWorld", "Atmosphere", "Midground",
                        "Gameplay", "Foreground",
                    };
                    var enabledSprites = roots
                        .Where(root => environmentBands.Contains(root.name))
                        .SelectMany(root =>
                            root.GetComponentsInChildren<SpriteRenderer>(true))
                        .Where(renderer => renderer.enabled && renderer.sprite != null)
                        .ToArray();
                    Assert.That(enabledSprites.Any(renderer =>
                            renderer.name == "AsterSkyFar"), Is.True);
                    Assert.That(enabledSprites.Any(renderer =>
                            renderer.name == "AsterForeground"), Is.True);
                    var clonedMirraLayers = new HashSet<string>(StringComparer.Ordinal)
                    {
                        "MirraSkyFinal", "MirraFarWorldFinal",
                        "MirraAtmosphereFinal", "MirraMidgroundFinal",
                        "MirraGameplayFinal", "MirraForegroundFinal",
                    };
                    Assert.That(enabledSprites
                            .Select(renderer => renderer.sprite.name)
                            .Where(clonedMirraLayers.Contains),
                        Is.Empty,
                        "Aster must not render the cloned Mirra landscape beneath " +
                        "its authored debris field.");
                    Assert.That(
                        roots.Where(root => !environmentBands.Contains(root.name))
                            .SelectMany(root =>
                                root.GetComponentsInChildren<SpriteRenderer>(true))
                            .Where(renderer => renderer.enabled &&
                                renderer.sprite != null &&
                                renderer.sortingOrder <= -10)
                            .Select(renderer => renderer.name),
                        Is.Empty,
                        "Aster must own the complete rendered background; no " +
                        "template landscape may remain enabled outside the named " +
                        "2.5D band roots.");
                    var camera = roots
                        .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                        .Concat(UnityEngine.Object.FindObjectsByType<Camera>(
                            FindObjectsInactive.Include,
                            FindObjectsSortMode.None))
                        .First(item => item.gameObject.scene == scene &&
                            item.CompareTag("MainCamera"));
                    var sky = enabledSprites.Single(renderer =>
                        renderer.name == "AsterSkyFar");
                    var halfHeight = camera.orthographicSize;
                    var halfWidth = halfHeight * camera.aspect;
                    Assert.That(sky.bounds.min.x,
                        Is.LessThanOrEqualTo(camera.transform.position.x - halfWidth));
                    Assert.That(sky.bounds.max.x,
                        Is.GreaterThanOrEqualTo(camera.transform.position.x + halfWidth));
                    Assert.That(sky.bounds.min.y,
                        Is.LessThanOrEqualTo(camera.transform.position.y - halfHeight));
                    Assert.That(sky.bounds.max.y,
                        Is.GreaterThanOrEqualTo(camera.transform.position.y + halfHeight),
                        "Aster's authored sky must cover the real flight camera frame, " +
                        "not world zero.");
                }
                else
                {
                    var actors = roots.Single(root => root.name == "ActorsAndProps");
                    foreach (var name in new[] { "Mira", "Juno", "Kai", "Bea", "Ori" })
                    {
                        var actor = actors.transform.Find(name);
                        Assert.That(actor, Is.Not.Null,
                            $"{sceneName} is missing crew actor {name}.");
                        var renderer = actor.GetComponent<SpriteRenderer>();
                        Assert.That(renderer, Is.Not.Null);
                        Assert.That(actor.GetComponent<SpriteAtlasAnimator>(), Is.Not.Null,
                            $"{sceneName}/{name} must own its idle animator.");
                        Assert.That(renderer.sprite, Is.Not.Null,
                            $"{sceneName}/{name} must serialize a real idle frame.");
                    }

                    var captain = actors.transform.Find("SavedCaptain");
                    Assert.That(captain, Is.Not.Null,
                        $"{sceneName} must include the saved Captain.");
                    Assert.That(captain.localScale.x, Is.GreaterThanOrEqualTo(0.95f),
                        $"{sceneName} must not miniaturize the Captain beside the crew.");

                    var canvasTexts = roots.Single(root => root.name == "HUD")
                        .GetComponentsInChildren<TMP_Text>(true);
                    var title = canvasTexts.Single(item => item.name == "ChapterTitle");
                    var story = canvasTexts.Single(item => item.name == "StoryCopy");
                    Assert.That(story.rectTransform.anchorMax.y,
                        Is.LessThanOrEqualTo(title.rectTransform.anchorMin.y - 0.01f),
                        $"{sceneName} title and story copy must occupy disjoint regions.");

                    if (sceneName == "Opening" || sceneName == "DinnerEnding")
                    {
                        var parent = actors.transform.Find("Parent");
                        Assert.That(parent, Is.Not.Null,
                            $"{sceneName} must visibly include the parent in the " +
                            "before-dinner promise/payoff.");
                        Assert.That(parent.GetComponent<SpriteRenderer>()?.sprite,
                            Is.Not.Null);
                    }
                }

                if (sceneName != "AsterVeil")
                {
                    Assert.That(
                        roots.SelectMany(root =>
                            root.GetComponentsInChildren<LayeredCharacterRenderer>(true)),
                        Is.Not.Empty,
                        $"{sceneName} must render the saved layered Captain live.");
                }

                if (addressable.HasValue)
                {
                    yield return Addressables.UnloadSceneAsync(addressable.Value);
                }
                else
                {
                    var unload = SceneManager.UnloadSceneAsync(scene);
                    while (unload != null && !unload.isDone) yield return null;
                }
            }
        }

        [UnityTest]
        public IEnumerator OpeningAndDinner_PublishExactStoryBirthdayAndCreditsCopy()
        {
            foreach (var sceneName in new[] { "Opening", "Clubhouse", "DinnerEnding" })
            {
                AsyncOperationHandle<SceneInstance>? addressable = null;
                Scene scene;
                if (sceneName == "Clubhouse")
                {
                    var operation = SceneManager.LoadSceneAsync(
                        sceneName,
                        LoadSceneMode.Additive);
                    Assert.That(operation, Is.Not.Null);
                    while (!operation.isDone) yield return null;
                    scene = SceneManager.GetSceneByName(sceneName);
                }
                else
                {
                    var handle = Addressables.LoadSceneAsync(
                        sceneName,
                        LoadSceneMode.Additive,
                        true);
                    addressable = handle;
                    yield return handle;
                    Assert.That(handle.Status, Is.EqualTo(AsyncOperationStatus.Succeeded));
                    scene = handle.Result.Scene;
                }

                var copy = string.Join(
                    "\n",
                    scene.GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<TMP_Text>(true))
                        .Select(item => item.text));
                if (sceneName == "Opening")
                {
                    Assert.That(copy, Does.Contain(
                        "We’re going exploring! We’ll be back before dinner!"));
                    foreach (var name in new[] { "Mira", "Juno", "Kai", "Bea", "Ori" })
                    {
                        Assert.That(copy, Does.Contain(name));
                    }
                }
                else if (sceneName == "Clubhouse")
                {
                    var sequence = scene.GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<
                            ChapterOneSequenceController2D>(true))
                        .Single();
                    Assert.That(sequence.BirthdayDeliveryStableId,
                        Is.EqualTo("birthday.ori.delivery.2026"));
                    Assert.That(sequence.BirthdayDecorationsStableId,
                        Is.EqualTo("birthday.decorations.homemade.2026"));
                    Assert.That(copy, Does.Contain("race home"));
                    Assert.That(copy, Does.Not.Contain("birthday."),
                        "Internal Task 22 IDs must not leak into player-facing copy.");
                    Assert.That(copy.ToLowerInvariant(), Does.Not.Contain("purchase"));
                    Assert.That(copy, Does.Not.Contain("$"));
                }
                else
                {
                    Assert.That(copy, Does.Contain("So, did you discover anything?"));
                    Assert.That(copy, Does.Contain("Just some stars."));
                    Assert.That(copy, Does.Contain("CHAPTER TWO"));
                    Assert.That(copy, Does.Contain("CREDITS"));
                }

                if (addressable.HasValue)
                {
                    yield return Addressables.UnloadSceneAsync(addressable.Value);
                }
                else
                {
                    var unload = SceneManager.UnloadSceneAsync(scene);
                    while (unload != null && !unload.isDone) yield return null;
                }
            }
        }

        private static async Task PublishAndFlush<T>(
            GameEventBus events,
            IChapterProgression progression,
            T gameEvent)
        {
            events.Publish(gameEvent);
            await progression.FlushPendingAsync(CancellationToken.None);
        }

        private static Type RequireType(string fullName)
        {
            var type = typeof(GameSave).Assembly.GetType(fullName, false);
            Assert.That(type, Is.Not.Null, $"Production type '{fullName}' is missing.");
            return type;
        }

        private static object RequireProperty(object instance, string name)
        {
            Assert.That(instance, Is.Not.Null);
            var property = instance.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null,
                $"{instance.GetType().Name}.{name} is missing.");
            var value = property.GetValue(instance);
            Assert.That(value, Is.Not.Null,
                $"{instance.GetType().Name}.{name} is null.");
            return value;
        }

        private static object Invoke(object instance, string name, params object[] args)
        {
            Assert.That(instance, Is.Not.Null);
            var method = instance.GetType().GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null,
                $"{instance.GetType().Name}.{name} is missing.");
            return method.Invoke(instance, args);
        }

        private static void SetPrivateField<T>(
            object instance,
            string name,
            T value)
        {
            Assert.That(instance, Is.Not.Null);
            var field = instance.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                $"{instance.GetType().Name}.{name} is missing.");
            field.SetValue(instance, value);
        }

        private static void AssertChapterState(
            object chapter,
            string phase,
            bool starMap,
            bool finalPulse,
            bool credits)
        {
            Assert.That(RequireProperty(chapter, "Phase").ToString(), Is.EqualTo(phase));
            Assert.That(RequireProperty(chapter, "StarMapRevealed"), Is.EqualTo(starMap));
            Assert.That(RequireProperty(chapter, "FinalPulseSeen"), Is.EqualTo(finalPulse));
            Assert.That(RequireProperty(chapter, "CreditsUnlocked"), Is.EqualTo(credits));
        }

        private static void SetChapterState(
            GameSave save,
            string phase,
            bool starMap,
            bool finalPulse)
        {
            var chapter = RequireProperty(save, "ChapterOne");
            var phaseProperty = chapter.GetType().GetProperty("Phase");
            var phaseValue = Enum.Parse(phaseProperty.PropertyType, phase);
            phaseProperty.SetValue(chapter, phaseValue);
            chapter.GetType().GetProperty("StarMapRevealed").SetValue(chapter, starMap);
            chapter.GetType().GetProperty("FinalPulseSeen").SetValue(chapter, finalPulse);
        }

        private static MissionProgress AsterMission(int ordinal)
        {
            var ids = new[]
            {
                "mission.aster-veil.approach",
                "mission.aster-veil.route",
                "mission.aster-veil.relative-motion",
                "mission.aster-veil.debris",
                "mission.aster-veil.fragment",
                "mission.aster-veil.reassembly",
                "mission.aster-veil.escape",
                "mission.aster-veil.return",
                "mission.aster-veil.dinner",
            };
            return new MissionProgress
            {
                MissionId = "mission.aster-veil.chapter-one",
                CheckpointNodeId = ordinal == 0 ? ids[0] : ids[ordinal - 1],
                CheckpointOrdinal = ordinal,
                CompletedNodeIds = ids.Take(ordinal).ToArray(),
                ActiveNodeIds = ordinal < ids.Length
                    ? new[] { ids[ordinal] }
                    : Array.Empty<string>(),
            };
        }

        private static GameSave CompletedKoroSave(string id, long ticks)
        {
            var save = GameSave.CreateNew(id, ticks);
            save.Mission = new MissionProgress
            {
                MissionId = KoroVesperProgressionService.MissionId,
                CheckpointNodeId = "mission.koro-vesper.fragment",
                CheckpointOrdinal = 6,
                CompletedNodeIds = new[]
                {
                    "mission.koro-vesper.approach",
                    "mission.koro-vesper.landed",
                    "mission.koro-vesper.traversal",
                    "mission.koro-vesper.spectra",
                    "mission.koro-vesper.rhythm",
                    "mission.koro-vesper.fragment",
                },
                ActiveNodeIds = Array.Empty<string>(),
            };
            save.Story.CheckpointId = "mission.koro-vesper.fragment";
            save.Story.CheckpointOrdinal = 13;
            save.DiscoveryIds = new[]
            {
                "chapter.mirra.complete",
                "fragment.signal.mirra.001",
                "chapter.koro-vesper.complete",
                KoroVesperProgressionService.FragmentIdValue,
            };
            return save;
        }

        private sealed class RecordingSaveService : ISaveService
        {
            public RecordingSaveService(GameSave initial)
            {
                Current = initial?.Copy();
                HasSave = initial != null;
            }

            public GameSave Current { get; private set; }
            public bool HasSave { get; private set; }
            public List<string> OperationOrder { get; } = new();

            public ValueTask<StartupResult> InitializeAsync(
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new ValueTask<StartupResult>(StartupResult.Available());
            }

            public ValueTask ShutdownAsync() => default;

            public ValueTask<LoadSaveResult> LoadAsync(
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new ValueTask<LoadSaveResult>(new LoadSaveResult(
                    HasSave
                        ? LoadSaveStatus.LoadedPrimary
                        : LoadSaveStatus.Missing,
                    HasSave ? Current.Copy() : null,
                    string.Empty));
            }

            public ValueTask SaveCheckpointAsync(
                GameSave save,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                save.ThrowIfInvalid(nameof(save));
                Current = save.Copy();
                HasSave = true;
                OperationOrder.Add("save");
                return default;
            }

            public ValueTask<LoadSaveResult> RecoverAsync(
                CancellationToken cancellationToken) => LoadAsync(cancellationToken);

            public GameSave Merge(GameSave local, GameSave cloud) =>
                SaveMerge.Combine(local, cloud);
        }

        private sealed class MemoryFirestoreGateway : IFirestoreDocumentGateway
        {
            private string m_Document;

            public bool IsConfigured => true;

            public ValueTask<StartupResult> InitializeAsync(
                CancellationToken cancellationToken) =>
                new(StartupResult.Available());

            public ValueTask ShutdownAsync() => default;

            public ValueTask<string> ReadAsync(
                string documentPath,
                CancellationToken cancellationToken) => new(m_Document);

            public ValueTask WriteAsync(
                string documentPath,
                FirestoreDocumentWrite document,
                CancellationToken cancellationToken)
            {
                m_Document = document.PayloadJson;
                return default;
            }

            public ValueTask<CloudCommitResult> WriteIfVersionAsync(
                string documentPath,
                FirestoreDocumentWrite document,
                CloudSaveVersion expectedRemote,
                CancellationToken cancellationToken)
            {
                m_Document = document.PayloadJson;
                return new ValueTask<CloudCommitResult>(new CloudCommitResult(
                    true,
                    false,
                    new CloudSaveVersion(
                        expectedRemote.Revision + 1,
                        "task26-write")));
            }

            public ValueTask DeleteAsync(
                string documentPath,
                CancellationToken cancellationToken)
            {
                m_Document = null;
                return default;
            }
        }
    }
}
