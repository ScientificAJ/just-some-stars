using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JustSomeStars.Runtime.Animation2D;
using JustSomeStars.Runtime.Cinematics;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Cosmetics;
using JustSomeStars.Runtime.Dialogue;
using JustSomeStars.Runtime.Discovery;
using JustSomeStars.Runtime.Flight;
using JustSomeStars.Runtime.Missions;
using JustSomeStars.Runtime.Rendering2D;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace JustSomeStars.Editor
{
    public static class Task26ChapterOneBuilder
    {
        private const string Root = "Assets/_JustSomeStars";
        private const string AsterScene = Root + "/Scenes/Destinations/AsterVeil.unity";
        private const string ReassemblyScene =
            Root + "/Scenes/Cinematics/SignalReassembly.unity";
        private const string ClubhouseScene =
            Root + "/Scenes/Core/Clubhouse.unity";
        private const string LegacyClubhouseScene =
            Root + "/Scenes/Cinematics/Clubhouse.unity";
        private const string OpeningScene =
            Root + "/Scenes/Cinematics/Opening.unity";
        private const string DinnerScene =
            Root + "/Scenes/Cinematics/DinnerEnding.unity";
        private const string AsterContent =
            Root + "/Content/Resources/Task26AsterVeilChapter.asset";
        private const string AsterMission =
            Root + "/Content/Missions/AsterVeil/aster-veil-chapter.asset";

        private static readonly string[] Bands =
        {
            "Sky", "FarWorld", "Atmosphere", "Midground", "Gameplay",
            "ActorsAndProps", "Foreground", "HUD",
        };

        private static readonly string[] Checkpoints =
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

        [MenuItem("Just Some Stars/Task 26/Build Chapter One")]
        public static void Build()
        {
            try
            {
                EnsureFolders();
                ConfigureTextureImporters();
                var content = BuildContent();
                BuildAsterScene();
                BuildSequenceScene(
                    OpeningScene,
                    ChapterOneSequenceKind.Opening,
                    "ClubhouseOpeningBackgroundV2.png",
                    "Mira · Juno · Kai · Bea · Ori\n" +
                    ChapterOneSequenceController2D.OpeningPromise,
                    includeCaptain: true,
                    dinner: false);
                BuildSequenceScene(
                    ReassemblyScene,
                    ChapterOneSequenceKind.SignalReassembly,
                    "SignalReassemblyBackgroundV2.png",
                    "STAR MAP: BEYOND AURELIA\n" +
                    "RECENT PULSE CONFIRMED",
                    includeCaptain: true,
                    dinner: false);
                BuildSequenceScene(
                    ClubhouseScene,
                    ChapterOneSequenceKind.Clubhouse,
                    "ClubhouseOpeningBackgroundV2.png",
                    "The Scout skids into the Clubhouse. Everyone is safe.\n" +
                    "Grab the fragment—race home before the last light.",
                    includeCaptain: true,
                    dinner: false);
                BuildSequenceScene(
                    DinnerScene,
                    ChapterOneSequenceKind.DinnerEnding,
                    "ClubhouseDinnerCleanBackgroundV3.png",
                    ChapterOneSequenceController2D.ParentQuestion + "\n" +
                    ChapterOneSequenceController2D.DinnerAnswer + "\n" +
                    "CHAPTER TWO · SIGNAL BEYOND AURELIA\nCREDITS",
                    includeCaptain: true,
                    dinner: true);
                DeleteIfExists(LegacyClubhouseScene);
                UpdateSceneCatalog();
                UpdateAddressables(content);
                UpdateBuildSettings();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                ValidateProducts();
                Debug.Log("[JSS Task 26] Chapter One content and scenes built.");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder(Root + "/Scenes", "Cinematics");
            EnsureFolder(Root + "/Scenes", "Core");
            EnsureFolder(Root + "/Content/Missions", "AsterVeil");
            EnsureFolder(Root + "/Content", "Resources");
            EnsureFolder(Root + "/Content/Dialogue", "AsterVeil");
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static void ConfigureTextureImporters()
        {
            var paths = new[]
            {
                Root + "/Art/2D/Environments/AsterVeil/Layers/AsterSkyFar.png",
                Root + "/Art/2D/Environments/AsterVeil/Layers/AsterForeground.png",
                Root + "/Art/2D/Environments/AsterVeil/Layers/AsterDebris.png",
                Root + "/Art/2D/Environments/SignalReassembly/Layers/" +
                    "SignalReassemblyEnvironment.png",
                Root + "/Art/2D/Environments/SignalReassembly/Layers/" +
                    "SignalReassemblyBackgroundV2.png",
                Root + "/Art/2D/Environments/SignalReassembly/Layers/" +
                    "SignalHologramV2.png",
                Root + "/Art/2D/Environments/SignalReassembly/Layers/" +
                    "SignalReassemblySkyV4.png",
                Root + "/Art/2D/Environments/SignalReassembly/Layers/" +
                    "SignalReassemblyFarWorldV4.png",
                Root + "/Art/2D/Environments/SignalReassembly/Layers/" +
                    "SignalReassemblyArchitectureV4.png",
                Root + "/Art/2D/Environments/Clubhouse/Layers/" +
                    "ClubhouseOpeningEnvironment.png",
                Root + "/Art/2D/Environments/Clubhouse/Layers/" +
                    "ClubhouseDinnerEnvironment.png",
                Root + "/Art/2D/Environments/Clubhouse/Layers/" +
                    "ClubhouseOpeningBackgroundV2.png",
                Root + "/Art/2D/Environments/Clubhouse/Layers/" +
                    "ClubhouseDinnerBackgroundV2.png",
                Root + "/Art/2D/Environments/Clubhouse/Layers/" +
                    "ClubhouseDinnerCleanBackgroundV3.png",
                Root + "/Art/2D/Environments/Clubhouse/Layers/" +
                    "ClubhouseDinnerTableForegroundV2.png",
                Root + "/Art/2D/Environments/Clubhouse/Layers/" +
                    "ClubhouseDinnerTableForegroundV4.png",
                Root + "/Art/2D/Environments/Clubhouse/Layers/" +
                    "ClubhouseForeground.png",
                Root + "/Art/2D/Environments/Clubhouse/Layers/" +
                    "ClubhouseOpeningSkyV4.png",
                Root + "/Art/2D/Environments/Clubhouse/Layers/" +
                    "ClubhouseOpeningFarWorldV4.png",
                Root + "/Art/2D/Environments/Clubhouse/Layers/" +
                    "ClubhouseOpeningArchitectureV4.png",
                Root + "/Art/2D/Environments/Clubhouse/Layers/" +
                    "ClubhouseDinnerSkyV4.png",
                Root + "/Art/2D/Environments/Clubhouse/Layers/" +
                    "ClubhouseDinnerFarWorldV4.png",
                Root + "/Art/2D/Environments/Clubhouse/Layers/" +
                    "ClubhouseDinnerArchitectureV4.png",
                Root + "/Art/2D/Characters/Parent/ParentStanding.png",
                Root + "/Art/2D/Characters/Parent/ParentSeated.png",
            };
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var path in paths)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter ??
                    throw new InvalidOperationException(
                        "Missing texture importer for " + path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                var textureSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(textureSettings);
                textureSettings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(textureSettings);
                importer.spritePixelsPerUnit = 100f;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
        }

        private static AsterVeilChapterContent BuildContent()
        {
            DeleteIfExists(AsterContent);
            DeleteIfExists(AsterMission);

            var mission = ScriptableObject.CreateInstance<MissionDefinition>();
            mission.name = "Aster Veil Chapter";
            var nodes = new MissionNode[Checkpoints.Length];
            for (var index = 0; index < nodes.Length; index++)
            {
                nodes[index] = new MissionNode(
                    Checkpoints[index],
                    index == 0
                        ? MissionNodeKind.Entry
                        : index == nodes.Length - 1
                            ? MissionNodeKind.Terminal
                            : MissionNodeKind.Checkpoint,
                    Array.Empty<MissionRequirement>(),
                    index + 1 < nodes.Length
                        ? new[] { Checkpoints[index + 1] }
                        : Array.Empty<string>(),
                    Array.Empty<string>(),
                    string.Empty,
                    index == 0 || index == nodes.Length - 1 ? 0 : index);
            }
            mission.Configure(
                AsterVeilProgressionService.MissionId,
                Checkpoints[0],
                nodes);
            mission.ValidateOrThrow();
            AssetDatabase.CreateAsset(mission, AsterMission);

            var dialogue = new[]
            {
                CreateDialogue("mira", "route", "steady", "focused", "point"),
                CreateDialogue("juno", "motion", "curious", "alert", "scan"),
                CreateDialogue("kai", "trust", "brave", "warm", "nod"),
                CreateDialogue("bea", "fragment", "hopeful", "bright", "reach"),
                CreateDialogue("ori", "pulse", "wonder", "flicker", "pulse"),
            };

            var content = ScriptableObject.CreateInstance<AsterVeilChapterContent>();
            content.name = "Task26AsterVeilChapter";
            content.Configure(
                mission,
                LoadRequired<PhenomenonDefinition>(
                    Root + "/Content/Phenomena/AsterMotion.asset"),
                LoadRequired<InstrumentDefinition>(
                    Root + "/Content/Phenomena/Instruments/AsterMotionTracker.asset"),
                LoadRequired<JustSomeStars.Runtime.Atlas.ScienceSourceDefinition>(
                    Root + "/Content/ScienceSources/AsterRelativeMotion.asset"),
                dialogue,
                Checkpoints,
                "seed.aster.debris.260826.v1");
            AssetDatabase.CreateAsset(content, AsterContent);
            EditorUtility.SetDirty(content);
            return content;
        }

        private static DialogueEntry CreateDialogue(
            string speaker,
            string suffix,
            string emotion,
            string expression,
            string gesture)
        {
            var path = Root + "/Content/Dialogue/AsterVeil/" +
                char.ToUpperInvariant(speaker[0]) + speaker.Substring(1) + ".asset";
            DeleteIfExists(path);
            var entry = ScriptableObject.CreateInstance<DialogueEntry>();
            entry.name = "Aster " + speaker;
            entry.Configure(
                "dialogue.aster." + speaker + "." + suffix,
                "dialogue.aster." + speaker + "." + suffix,
                "crew." + speaker,
                "voice.aster." + speaker + "." + suffix,
                emotion,
                expression,
                gesture,
                Array.Empty<string>(),
                DialoguePriority.Story,
                false,
                0d,
                Array.Empty<string>());
            AssetDatabase.CreateAsset(entry, path);
            return entry;
        }

        private static void BuildAsterScene()
        {
            DeleteIfExists(AsterScene);
            var scene = EditorSceneManager.OpenScene(
                Root + "/Scenes/Benchmarks/Task17FlightGraybox.unity",
                OpenSceneMode.Single);
            var roots = EnsureBands(scene);
            DisableTemplateBackgroundRenderers(scene);
            foreach (var band in new[]
                     {
                         "Sky", "FarWorld", "Atmosphere", "Midground",
                         "Gameplay", "Foreground",
                     })
            {
                DisableRenderers(roots[band]);
            }

            var camera = UnityEngine.Object.FindAnyObjectByType<Camera>() ??
                throw new InvalidOperationException("Flight template has no camera.");
            var skyRenderer = AddPlate(
                roots["Sky"].transform,
                "AsterSkyFar",
                LoadSprite(Root +
                    "/Art/2D/Environments/AsterVeil/Layers/AsterSkyFar.png"),
                -1000,
                camera.orthographicSize * 1.02f);
            FitSpriteToCoverCanonicalViewport(skyRenderer, camera, 1.02f);
            skyRenderer.transform.position = new Vector3(
                camera.transform.position.x,
                camera.transform.position.y,
                0f);
            var skyParallax = skyRenderer.gameObject.AddComponent<ParallaxLayer2D>();
            skyParallax.Configure(0.94f, new Vector2(0.9f, 0.72f));
            var farRenderer = AddPlate(
                roots["FarWorld"].transform,
                "AsterFarDebris",
                LoadSprite(Root +
                    "/Art/2D/Environments/AsterVeil/Layers/AsterDebris.png"),
                -620,
                camera.orthographicSize * 0.96f);
            FitSpriteToCoverCanonicalViewport(farRenderer, camera, 1.04f);
            farRenderer.transform.position = new Vector3(
                camera.transform.position.x,
                camera.transform.position.y,
                0f);
            farRenderer.color = new Color(0.7f, 0.78f, 1f, 0.22f);
            var farParallax = farRenderer.gameObject.AddComponent<ParallaxLayer2D>();
            farParallax.Configure(0.72f, new Vector2(0.66f, 0.48f));
            var atmosphereObject = CreateGlow(
                roots["Atmosphere"].transform,
                "AsterSignalHaze",
                new Vector3(
                    camera.transform.position.x + 5.2f,
                    camera.transform.position.y + 1.4f,
                    0f),
                new Color(0.62f, 0.26f, 1f, 0.34f),
                -180);
            atmosphereObject.transform.localScale = Vector3.one * 2.8f;
            var atmosphereParallax = atmosphereObject.AddComponent<ParallaxLayer2D>();
            atmosphereParallax.Configure(0.48f, new Vector2(0.38f, 0.30f));
            var foregroundRenderer = AddPlate(
                roots["Foreground"].transform,
                "AsterForeground",
                LoadSprite(Root +
                    "/Art/2D/Environments/AsterVeil/Layers/AsterForeground.png"),
                900,
                camera.orthographicSize * 1.02f);
            FitSpriteToCoverCanonicalViewport(foregroundRenderer, camera, 1.02f);
            foregroundRenderer.transform.position = new Vector3(
                camera.transform.position.x,
                camera.transform.position.y,
                0f);
            var foregroundParallax = foregroundRenderer.gameObject.AddComponent<
                ParallaxLayer2D>();
            foregroundParallax.Configure(0.14f, new Vector2(0.10f, 0.08f));

            var motor = UnityEngine.Object.FindAnyObjectByType<FlightMotor2D>() ??
                throw new InvalidOperationException("Flight template has no motor.");
            var routeOrigin = motor.Body != null
                ? motor.Body.position
                : (Vector2)motor.transform.position;
            foreach (var shipRenderer in
                     motor.GetComponentsInChildren<SpriteRenderer>(true))
            {
                shipRenderer.transform.localScale *= 1.28f;
            }
            var missionRoot = new GameObject("Aster Veil Mission");
            SceneManager.MoveGameObjectToScene(missionRoot, scene);
            missionRoot.transform.SetParent(roots["Gameplay"].transform, false);
            var parallaxRig = missionRoot.AddComponent<ParallaxRig2D>();
            parallaxRig.Configure(
                camera.transform,
                new[]
                {
                    skyParallax,
                    farParallax,
                    atmosphereParallax,
                    foregroundParallax,
                });

            var debris = missionRoot.AddComponent<DebrisFieldController>();
            var bodies = new Rigidbody2D[DebrisFieldSimulation.BodyCount];
            var renderers = new SpriteRenderer[DebrisFieldSimulation.BodyCount];
            var debrisSprite = LoadSprite(Root +
                "/Art/2D/Environments/AsterVeil/Layers/AsterDebris.png");
            var authoredDebris = new DebrisFieldSimulation(
                DebrisFieldController.AuthoredSeed).Bodies;
            for (var index = 0; index < bodies.Length; index++)
            {
                var item = new GameObject("Debris." + index.ToString("00"));
                item.transform.SetParent(roots["Midground"].transform, false);
                item.transform.position = routeOrigin + authoredDebris[index].Position;
                item.transform.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    index * 37f);
                item.transform.localScale = Vector3.one *
                    Mathf.Lerp(0.055f, 0.13f, (index % 5) / 4f);
                var body = item.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;
                bodies[index] = body;
                var renderer = item.AddComponent<SpriteRenderer>();
                renderer.sprite = debrisSprite;
                renderer.color = new Color(1f, 1f, 1f, 0.86f);
                renderers[index] = renderer;
                var collider = item.AddComponent<CircleCollider2D>();
                collider.radius = 2.1f;
                collider.isTrigger = true;
                item.AddComponent<DebrisHazard2D>().Configure(
                    debris,
                    index,
                    authoredDebris[index].Lane);
            }
            SetObject(debris, "motor", motor);
            SetObjects(debris, "debrisBodies", bodies.Cast<UnityEngine.Object>().ToArray());
            SetObjects(
                debris,
                "debrisRenderers",
                renderers.Cast<UnityEngine.Object>().ToArray());
            SetInt(debris, "authoredSeed", DebrisFieldController.AuthoredSeed);
            SetVector2(debris, "routeOrigin", routeOrigin);
            SetFloat(debris, "routeCheckpointX", 0f);
            SetFloat(debris, "routeExitX", 5.4f);

            var mission = missionRoot.AddComponent<AsterVeilMissionController2D>();
            var canvas = CreateCanvas(roots["HUD"].transform, camera);
            var objective = CreateUiText(
                canvas.transform,
                "AsterObjective",
                "ASTER VEIL · READ THE SHIFTING LANES",
                new Vector2(0.035f, 0.84f),
                new Vector2(0.68f, 0.94f),
                28f,
                TextAlignmentOptions.Left);
            var trust = CreateUiText(
                canvas.transform,
                "CrewTrust",
                "MIRA · JUNO · KAI · BEA\nYOUR CALL, CAPTAIN.",
                new Vector2(0.035f, 0.70f),
                new Vector2(0.58f, 0.84f),
                21f,
                TextAlignmentOptions.Left);
            var fragment = CreateGlow(
                roots["ActorsAndProps"].transform,
                "ThirdSignalFragment",
                new Vector3(routeOrigin.x + 6.45f, routeOrigin.y + 0.7f, 0f),
                new Color(0.72f, 0.35f, 1f, 0.95f),
                620);
            var route = CreateGlow(
                roots["Atmosphere"].transform,
                "GravityRouteHologram",
                new Vector3(routeOrigin.x + 1.8f, routeOrigin.y, 0f),
                new Color(0.28f, 0.78f, 1f, 0.5f),
                210);
            var routeCue = missionRoot.AddComponent<AudioSource>();
            var fragmentCue = missionRoot.AddComponent<AudioSource>();
            SetObject(mission, "debrisField", debris);
            SetObject(mission, "objectiveLabel", objective);
            SetObject(mission, "crewTrustLabel", trust);
            SetObject(mission, "fragmentVisual", fragment);
            SetObject(mission, "routeHologram", route);
            SetObject(mission, "routeCue", routeCue);
            SetObject(mission, "fragmentCue", fragmentCue);

            if (!EditorSceneManager.SaveScene(scene, AsterScene))
            {
                throw new InvalidOperationException("Could not save AsterVeil scene.");
            }
        }

        private static void BuildSequenceScene(
            string path,
            ChapterOneSequenceKind kind,
            string environmentFile,
            string serializedCopy,
            bool includeCaptain,
            bool dinner)
        {
            DeleteIfExists(path);
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            var roots = EnsureBands(scene);
            var cameraObject = new GameObject("ChapterOneCamera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4.705f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.006f, 0.012f, 0.035f, 1f);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var signal = environmentFile.StartsWith(
                "Signal",
                StringComparison.Ordinal);
            var layerRoot = signal
                ? Root + "/Art/2D/Environments/SignalReassembly/Layers/"
                : Root + "/Art/2D/Environments/Clubhouse/Layers/";
            var skyPath = layerRoot + (signal
                ? "SignalReassemblySkyV4.png"
                : dinner
                    ? "ClubhouseDinnerSkyV4.png"
                    : "ClubhouseOpeningSkyV4.png");
            var farWorldPath = layerRoot + (signal
                ? "SignalReassemblyFarWorldV4.png"
                : dinner
                    ? "ClubhouseDinnerFarWorldV4.png"
                    : "ClubhouseOpeningFarWorldV4.png");
            var architecturePath = layerRoot + (signal
                ? "SignalReassemblyArchitectureV4.png"
                : dinner
                    ? "ClubhouseDinnerArchitectureV4.png"
                    : "ClubhouseOpeningArchitectureV4.png");
            var sky = AddPlate(
                roots["Sky"].transform,
                "SkyAuthority",
                LoadSprite(skyPath),
                -1000,
                camera.orthographicSize);
            FitSpriteToCoverCanonicalViewport(sky, camera, 1.02f);
            var far = AddPlate(
                roots["FarWorld"].transform,
                "FarWorldAuthority",
                LoadSprite(farWorldPath),
                -700,
                camera.orthographicSize);
            FitSpriteToCoverCanonicalViewport(far, camera, 1.02f);
            var atmosphere = CreateGlow(
                roots["Atmosphere"].transform,
                "AtmosphereMote",
                new Vector3(2.8f, 1.1f, 0f),
                new Color(0.65f, 0.32f, 1f, 0.34f),
                -200).GetComponent<SpriteRenderer>();
            var architecture = AddPlate(
                roots["Midground"].transform,
                "ArchitectureAuthority",
                LoadSprite(architecturePath),
                -100,
                camera.orthographicSize);
            FitSpriteToCoverCanonicalViewport(architecture, camera, 1.02f);

            var skyParallax = sky.gameObject.AddComponent<ParallaxLayer2D>();
            skyParallax.Configure(0.94f, new Vector2(0.90f, 0.72f));
            var farParallax = far.gameObject.AddComponent<ParallaxLayer2D>();
            farParallax.Configure(0.74f, new Vector2(0.66f, 0.52f));
            var atmosphereParallax = atmosphere.gameObject.AddComponent<
                ParallaxLayer2D>();
            atmosphereParallax.Configure(0.48f, new Vector2(0.38f, 0.30f));
            var architectureParallax = architecture.gameObject.AddComponent<
                ParallaxLayer2D>();
            architectureParallax.Configure(0.32f, new Vector2(0.26f, 0.18f));

            SpriteRenderer foreground;
            if (dinner)
            {
                foreground = AddPlate(
                    roots["Foreground"].transform,
                    "DinnerTableForeground",
                    LoadSprite(Root +
                        "/Art/2D/Environments/Clubhouse/Layers/" +
                    "ClubhouseDinnerTableForegroundV4.png"),
                    950,
                    2.9f);
                foreground.transform.localPosition = new Vector3(2.55f, -2.15f, 0f);
            }
            else if (!environmentFile.StartsWith("Signal", StringComparison.Ordinal))
            {
                foreground = AddPlate(
                    roots["Foreground"].transform,
                    "ClubhouseForeground",
                    LoadSprite(Root +
                        "/Art/2D/Environments/Clubhouse/Layers/" +
                    "ClubhouseForeground.png"),
                    950,
                    camera.orthographicSize);
                FitSpriteToCoverCanonicalViewport(foreground, camera, 1.02f);
            }
            else
            {
                foreground = CreateGlow(
                    roots["Foreground"].transform,
                    "ForegroundLensBloom",
                    new Vector3(0f, -3.6f, 0f),
                    new Color(0.45f, 0.2f, 1f, 0.15f),
                    950).GetComponent<SpriteRenderer>();
            }
            var foregroundParallax = foreground.gameObject.AddComponent<
                ParallaxLayer2D>();
            foregroundParallax.Configure(0.14f, new Vector2(0.10f, 0.08f));
            var parallaxRig = new GameObject("ChapterParallaxRig").AddComponent<
                ParallaxRig2D>();
            parallaxRig.transform.SetParent(roots["Gameplay"].transform, false);
            parallaxRig.Configure(
                camera.transform,
                new[]
                {
                    skyParallax,
                    farParallax,
                    atmosphereParallax,
                    architectureParallax,
                    foregroundParallax,
                });

            Transform signalHologram;
            if (kind == ChapterOneSequenceKind.SignalReassembly)
            {
                var hologram = new GameObject("SignalHologram");
                hologram.transform.SetParent(roots["Midground"].transform, false);
                hologram.transform.localPosition = new Vector3(0.15f, -0.72f, 0f);
                var renderer = hologram.AddComponent<SpriteRenderer>();
                renderer.sprite = LoadSprite(Root +
                    "/Art/2D/Environments/SignalReassembly/Layers/" +
                    "SignalHologramV2.png");
                renderer.sortingOrder = 260;
                FitSpriteHeight(renderer, 6.25f);
                signalHologram = hologram.transform;
            }
            else
            {
                var anchor = new GameObject("SignalHologramAnchor");
                anchor.transform.SetParent(roots["Midground"].transform, false);
                signalHologram = anchor.transform;
            }

            var crew = AddStaticCrew(
                roots["ActorsAndProps"].transform,
                kind,
                out var crewAnimators,
                out var crewIdleClips,
                out var crewActionClips);
            var ship = AddShip(roots["ActorsAndProps"].transform, kind);
            var captain = includeCaptain
                ? AddLayeredCaptain(roots["ActorsAndProps"].transform, kind)
                : null;
            if (kind == ChapterOneSequenceKind.Opening ||
                kind == ChapterOneSequenceKind.DinnerEnding)
            {
                AddParent(roots["ActorsAndProps"].transform, dinner);
            }
            var crewAnchors = CreateCrewAnchors(
                roots["ActorsAndProps"].transform,
                captain != null ? captain.transform : null,
                crew);

            var canvas = CreateCanvas(roots["HUD"].transform, camera);
            var title = CreateUiText(
                canvas.transform,
                "ChapterTitle",
                TitleFor(kind),
                new Vector2(0.055f, 0.835f),
                new Vector2(0.62f, 0.94f),
                31f,
                TextAlignmentOptions.TopLeft);
            var story = CreateUiText(
                canvas.transform,
                "StoryCopy",
                serializedCopy,
                new Vector2(0.055f, 0.63f),
                new Vector2(0.68f, 0.815f),
                20f,
                TextAlignmentOptions.TopLeft);
            var creditsRoot = new GameObject("StoryCredits");
            creditsRoot.transform.SetParent(canvas.transform, false);
            var credits = CreateUiText(
                creditsRoot.transform,
                "CreditsCopy",
                "JUST SOME STARS\nCHAPTER ONE\n\nA story by ScientificAJ\n\n" +
                "CHAPTER TWO · SIGNAL BEYOND AURELIA",
                new Vector2(0.25f, 0.18f),
                new Vector2(0.75f, 0.82f),
                30f,
                TextAlignmentOptions.Center);
            credits.color = new Color(0.92f, 0.94f, 1f, 1f);
            creditsRoot.SetActive(false);

            var oriPulse = CreateGlow(
                roots["ActorsAndProps"].transform,
                "OriEyePulse",
                new Vector3(2.4f, -1.6f, 0f),
                new Color(0.28f, 0.86f, 1f, 0.92f),
                700);
            var fragmentPulse = CreateGlow(
                roots["ActorsAndProps"].transform,
                "PocketFragmentPulse",
                new Vector3(1.7f, -1.2f, 0f),
                new Color(0.75f, 0.32f, 1f, 0.86f),
                710);
            oriPulse.SetActive(false);
            fragmentPulse.SetActive(false);

            var controllerObject = new GameObject("ChapterOneSequence");
            controllerObject.transform.SetParent(roots["Gameplay"].transform, false);
            var controller = controllerObject.AddComponent<
                ChapterOneSequenceController2D>();
            var cue = controllerObject.AddComponent<AudioSource>();
            SetEnum(controller, "sequenceKind", (int)kind);
            SetObject(controller, "captainRenderer", captain);
            SetObject(controller, "chapterTitle", title);
            SetObject(controller, "storyCopy", story);
            SetObject(controller, "creditsRoot", creditsRoot);
            SetObject(controller, "oriPulse", oriPulse);
            SetObject(controller, "fragmentPulse", fragmentPulse);
            SetObjects(
                controller,
                "parallaxBands",
                new UnityEngine.Object[]
                {
                    sky,
                    far,
                    atmosphere,
                    architecture,
                    foreground,
                });
            SetObjects(
                controller,
                "crewAnchors",
                crewAnchors.Cast<UnityEngine.Object>().ToArray());
            SetObjects(
                controller,
                "crewAnimators",
                crewAnimators.Cast<UnityEngine.Object>().ToArray());
            SetObjects(
                controller,
                "crewIdleClips",
                crewIdleClips.Cast<UnityEngine.Object>().ToArray());
            SetObjects(
                controller,
                "crewActionClips",
                crewActionClips.Cast<UnityEngine.Object>().ToArray());
            SetObject(controller, "signalCue", cue);
            SetObject(controller, "scoutShip", ship);
            SetObject(controller, "signalHologram", signalHologram);

            var button = CreateContinueButton(canvas.transform, kind);
            UnityEventTools.AddPersistentListener(
                button.onClick,
                controller.AdvanceFromUi);

            if (!EditorSceneManager.SaveScene(scene, path))
            {
                throw new InvalidOperationException("Could not save " + path);
            }
        }

        private static Dictionary<string, GameObject> EnsureBands(Scene scene)
        {
            var map = scene.GetRootGameObjects()
                .GroupBy(item => item.name)
                .ToDictionary(group => group.Key, group => group.First());
            foreach (var name in Bands)
            {
                if (map.ContainsKey(name)) continue;
                var root = new GameObject(name);
                SceneManager.MoveGameObjectToScene(root, scene);
                map.Add(name, root);
            }
            return map;
        }

        private static void DisableRenderers(GameObject root)
        {
            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                renderer.enabled = false;
            }
        }

        private static void DisableTemplateBackgroundRenderers(Scene scene)
        {
            foreach (var renderer in scene.GetRootGameObjects()
                         .SelectMany(root =>
                             root.GetComponentsInChildren<SpriteRenderer>(true))
                         .Where(renderer => renderer.sortingOrder <= -10))
            {
                renderer.enabled = false;
            }
        }

        private static SpriteRenderer AddPlate(
            Transform parent,
            string name,
            Sprite sprite,
            int sorting,
            float halfHeight)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            var renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sorting;
            var height = Mathf.Max(0.01f, renderer.sprite.bounds.size.y);
            item.transform.localScale = Vector3.one * (halfHeight * 2f / height);
            return renderer;
        }

        private static void FitSpriteToCoverCanonicalViewport(
            SpriteRenderer renderer,
            Camera camera,
            float overscan)
        {
            if (renderer == null || renderer.sprite == null || camera == null ||
                !camera.orthographic || overscan < 1f)
            {
                throw new ArgumentException(
                    "Canonical plate fitting requires a sprite, orthographic camera, " +
                    "and non-shrinking overscan.");
            }

            const float canonicalAspect = 1616f / 720f;
            var source = renderer.sprite.bounds.size;
            var viewportHeight = camera.orthographicSize * 2f;
            var viewportWidth = viewportHeight * canonicalAspect;
            var scale = Mathf.Max(
                viewportWidth / Mathf.Max(0.01f, source.x),
                viewportHeight / Mathf.Max(0.01f, source.y));
            renderer.transform.localScale = Vector3.one * scale * overscan;
        }

        private static GameObject CreateGlow(
            Transform parent,
            string name,
            Vector3 position,
            Color color,
            int sorting)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            var renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = LoadSprite(
                Root + "/Art/2D/Environments/Mirra/VFX/MirraSignalMote.png");
            renderer.color = color;
            renderer.sortingOrder = sorting;
            item.transform.localScale = Vector3.one * 0.65f;
            return item;
        }

        private static Transform[] AddStaticCrew(
            Transform parent,
            ChapterOneSequenceKind kind,
            out SpriteAtlasAnimator[] animators,
            out SpriteAnimationClipDefinition[] idleClips,
            out SpriteAnimationClipDefinition[] actionClips)
        {
            var names = new[] { "Mira", "Juno", "Kai", "Bea", "Ori" };
            var x = kind switch
            {
                ChapterOneSequenceKind.SignalReassembly =>
                    new[] { -3.25f, -1.95f, -0.65f, 2.05f, 0.75f },
                ChapterOneSequenceKind.DinnerEnding =>
                    new[] { 0.15f, 1.15f, 2.15f, 3.15f, 4.15f },
                ChapterOneSequenceKind.Clubhouse =>
                    new[] { -4.15f, -3.3f, -2.45f, -1.6f, -0.75f },
                _ => new[] { -1.55f, -0.35f, 0.85f, 2.05f, -2.75f },
            };
            var baseY = kind switch
            {
                ChapterOneSequenceKind.SignalReassembly => -2.88f,
                ChapterOneSequenceKind.DinnerEnding => -2.35f,
                ChapterOneSequenceKind.Clubhouse => -2.48f,
                _ => -2.08f,
            };
            var childHeight = kind == ChapterOneSequenceKind.SignalReassembly
                ? 3.85f
                : kind == ChapterOneSequenceKind.DinnerEnding
                    ? 2.65f
                    : kind == ChapterOneSequenceKind.Clubhouse ? 3.05f : 3.15f;
            var transforms = new Transform[names.Length];
            animators = new SpriteAtlasAnimator[names.Length];
            idleClips = new SpriteAnimationClipDefinition[names.Length];
            actionClips = new SpriteAnimationClipDefinition[names.Length];
            for (var index = 0; index < names.Length; index++)
            {
                var item = new GameObject(names[index]);
                item.transform.SetParent(parent, false);
                item.transform.localPosition = new Vector3(x[index], baseY, 0f);
                transforms[index] = item.transform;
                var renderer = item.AddComponent<SpriteRenderer>();
                var characterId = names[index].ToLowerInvariant();
                var spriteSet = LoadRequired<CharacterSpriteSet>(
                    Root + "/Content/Characters/" + names[index] +
                    "SpriteSet.asset");
                var facing = kind == ChapterOneSequenceKind.Opening ||
                    kind == ChapterOneSequenceKind.Clubhouse
                    ? "left"
                    : x[index] < 0f ? "right" : "left";
                var motion = kind switch
                {
                    ChapterOneSequenceKind.DinnerEnding => "interact",
                    ChapterOneSequenceKind.Clubhouse => "run",
                    ChapterOneSequenceKind.Opening => "interact",
                    ChapterOneSequenceKind.SignalReassembly => "interact",
                    _ => "idle",
                };
                var clip = spriteSet.FindClip(
                    characterId + "." + motion + "." + facing);
                idleClips[index] = spriteSet.FindClip(
                    characterId + ".idle." + facing);
                actionClips[index] = clip;
                var authoredFrame = kind switch
                {
                    ChapterOneSequenceKind.DinnerEnding =>
                        Mathf.Min(3, clip.Frames.Count - 1),
                    ChapterOneSequenceKind.Clubhouse =>
                        Mathf.Min(4, clip.Frames.Count - 1),
                    ChapterOneSequenceKind.Opening or
                    ChapterOneSequenceKind.SignalReassembly =>
                        Mathf.Min(2, clip.Frames.Count - 1),
                    _ => 0,
                };
                renderer.sprite = clip.Frames[authoredFrame];
                renderer.sortingOrder = 540 + index;
                FitSpriteHeight(
                    renderer,
                    names[index] == "Ori" ? 1.45f : childHeight);
                var animator = item.AddComponent<SpriteAtlasAnimator>();
                animator.Configure(renderer);
                animator.Play(clip);
                animators[index] = animator;
                renderer.sprite = clip.Frames[authoredFrame];
            }
            return transforms;
        }

        private static Transform AddShip(
            Transform parent,
            ChapterOneSequenceKind kind)
        {
            if (kind == ChapterOneSequenceKind.SignalReassembly)
            {
                var hidden = new GameObject("ScoutShipAnchor");
                hidden.transform.SetParent(parent, false);
                return hidden.transform;
            }
            var item = new GameObject("ScoutShip");
            item.transform.SetParent(parent, false);
            item.transform.localPosition = new Vector3(-5.2f, -1.7f, 0f);
            var renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = LoadSprite(
                Root + "/Art/2D/Ship/PlayerShip/PlayerShipMaster.png");
            renderer.sortingOrder = 510;
            FitSpriteHeight(
                renderer,
                kind == ChapterOneSequenceKind.DinnerEnding ? 1.9f : 2.35f);
            if (kind == ChapterOneSequenceKind.Clubhouse)
            {
                item.transform.localPosition = new Vector3(-5.05f, -2.05f, 0f);
                item.transform.localRotation = Quaternion.Euler(0f, 0f, -8f);
            }
            return item.transform;
        }

        private static LayeredCharacterRenderer AddLayeredCaptain(
            Transform parent,
            ChapterOneSequenceKind kind)
        {
            var dinner = kind == ChapterOneSequenceKind.DinnerEnding;
            var root = new GameObject("SavedCaptain");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(
                kind == ChapterOneSequenceKind.SignalReassembly
                    ? 3.35f
                    : dinner ? 5.15f
                    : kind == ChapterOneSequenceKind.Clubhouse ? 0.35f : 3.25f,
                kind == ChapterOneSequenceKind.SignalReassembly
                    ? -2.88f
                    : dinner ? -2.35f
                    : kind == ChapterOneSequenceKind.Clubhouse ? -2.48f : -2.08f,
                0f);
            root.transform.localScale = Vector3.one * 1.15f;
            var renderers = new SpriteRenderer[5];
            for (var index = 0; index < renderers.Length; index++)
            {
                var layer = new GameObject(((CaptainSpriteLayer)index).ToString());
                layer.transform.SetParent(root.transform, false);
                renderers[index] = layer.AddComponent<SpriteRenderer>();
            }
            var character = root.AddComponent<LayeredCharacterRenderer>();
            SetObject(
                character,
                "spriteSet",
                LoadRequired<CaptainSpriteSet>(
                    Root + "/Content/Characters/CaptainSpriteSet.asset"));
            SetEnum(character, "family", (int)CaptainBodyFamily.Average);
            SetEnum(character, "facing", (int)SpriteFacing.Right);
            SetObjects(
                character,
                "layerRenderers",
                renderers.Cast<UnityEngine.Object>().ToArray());
            return character;
        }

        private static void AddParent(Transform parent, bool dinner)
        {
            var item = new GameObject("Parent");
            item.transform.SetParent(parent, false);
            item.transform.localPosition = dinner
                ? new Vector3(6.05f, -1.72f, 0f)
                : new Vector3(4.35f, -0.65f, 0f);
            var renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = LoadSprite(
                Root + "/Art/2D/Characters/Parent/" +
                (dinner ? "ParentSeated.png" : "ParentStanding.png"));
            renderer.sortingOrder = dinner ? 548 : 532;
            FitSpriteHeight(renderer, dinner ? 2.65f : 3.25f);
        }

        private static Transform[] CreateCrewAnchors(
            Transform parent,
            Transform captain,
            IReadOnlyList<Transform> crew)
        {
            if (captain == null || crew == null || crew.Count != 5)
            {
                throw new InvalidOperationException(
                    "Chapter One staging requires the Captain and five crew anchors.");
            }
            var anchors = new Transform[6];
            anchors[0] = captain;
            for (var index = 0; index < crew.Count; index++)
            {
                anchors[index + 1] = crew[index];
            }
            return anchors;
        }

        private static Canvas CreateCanvas(Transform parent, Camera camera)
        {
            var item = new GameObject("StoryCanvas");
            item.transform.SetParent(parent, false);
            var canvas = item.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            var scaler = item.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            item.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static TextMeshProUGUI CreateUiText(
            Transform parent,
            string name,
            string copy,
            Vector2 anchorMin,
            Vector2 anchorMax,
            float size,
            TextAlignmentOptions alignment)
        {
            var item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            var rect = (RectTransform)item.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var text = item.AddComponent<TextMeshProUGUI>();
            text.text = copy;
            text.fontSize = size;
            text.color = new Color(0.93f, 0.96f, 1f, 1f);
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.outlineColor = new Color32(3, 8, 18, 235);
            text.outlineWidth = 0.075f;
            return text;
        }

        private static TMP_Text CreateWorldText(
            Transform parent,
            string name,
            string copy,
            Vector3 position,
            float scale,
            TextAlignmentOptions alignment)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = Vector3.one * scale;
            var text = item.AddComponent<TextMeshPro>();
            text.text = copy;
            text.fontSize = 26f;
            text.color = new Color(0.91f, 0.96f, 1f, 0.98f);
            text.alignment = alignment;
            text.rectTransform.sizeDelta = new Vector2(65f, 14f);
            return text;
        }

        private static Button CreateContinueButton(
            Transform parent,
            ChapterOneSequenceKind kind)
        {
            var item = new GameObject("Continue", typeof(RectTransform));
            item.transform.SetParent(parent, false);
            var rect = (RectTransform)item.transform;
            rect.anchorMin = new Vector2(0.82f, 0.075f);
            rect.anchorMax = new Vector2(0.95f, 0.145f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = item.AddComponent<Image>();
            image.color = new Color(0.025f, 0.07f, 0.16f, 0.76f);
            var button = item.AddComponent<Button>();
            var label = CreateUiText(
                item.transform,
                "Label",
                kind == ChapterOneSequenceKind.DinnerEnding ? "FINISH" : "CONTINUE",
                Vector2.zero,
                Vector2.one,
                18f,
                TextAlignmentOptions.Center);
            label.raycastTarget = false;
            return button;
        }

        private static string TitleFor(ChapterOneSequenceKind kind) => kind switch
        {
            ChapterOneSequenceKind.Opening => "THE CLUBHOUSE · BEFORE DINNER",
            ChapterOneSequenceKind.SignalReassembly =>
                "THREE FRAGMENTS · ONE SIGNAL",
            ChapterOneSequenceKind.Clubhouse => "CLUBHOUSE · SAFE RETURN",
            ChapterOneSequenceKind.DinnerEnding => "HOME BEFORE DINNER",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        private static void FitSpriteHeight(SpriteRenderer renderer, float height)
        {
            var source = Mathf.Max(0.01f, renderer.sprite.bounds.size.y);
            renderer.transform.localScale = Vector3.one * (height / source);
        }

        private static void UpdateSceneCatalog()
        {
            var path = Root + "/Content/SceneCatalog.asset";
            var catalog = LoadRequired<SceneCatalog>(path);
            var serialized = new SerializedObject(catalog);
            serialized.FindProperty("m_SchemaVersion").intValue =
                SceneCatalog.CurrentSchemaVersion;
            serialized.FindProperty("m_FallbackSceneName").stringValue = "Clubhouse";
            serialized.FindProperty("m_FallbackMode").enumValueIndex =
                (int)GameMode.Clubhouse;
            var entries = serialized.FindProperty("m_Entries");
            var values = new[]
            {
                ("destination.mirra.approach", "Task17FlightGraybox", GameMode.Flight),
                ("destination.mirra.surface", "Mirra", GameMode.Surface),
                ("destination.vesper.approach", "Task25VesperFlight", GameMode.Flight),
                ("destination.koro.surface", "KoroVesper", GameMode.Surface),
                ("destination.chapter-one.opening", "Opening", GameMode.Clubhouse),
                ("destination.aster.approach", "AsterVeil", GameMode.Flight),
                ("destination.signal.reassembly", "SignalReassembly", GameMode.Flight),
                ("destination.dinner.ending", "DinnerEnding", GameMode.Clubhouse),
            };
            entries.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
            {
                var entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("m_DestinationId").stringValue =
                    values[index].Item1;
                entry.FindPropertyRelative("m_Address").stringValue =
                    values[index].Item2;
                entry.FindPropertyRelative("m_TargetMode").enumValueIndex =
                    (int)values[index].Item3;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            catalog.Validate();
            EditorUtility.SetDirty(catalog);
        }

        private static void UpdateBuildSettings()
        {
            var addressableScenes = new[]
            {
                Root + "/Scenes/Benchmarks/Task17FlightGraybox.unity",
                Root + "/Scenes/Destinations/Mirra.unity",
                Root + "/Scenes/Destinations/Task25VesperFlight.unity",
                Root + "/Scenes/Destinations/KoroVesper.unity",
                OpeningScene,
                AsterScene,
                ReassemblyScene,
                DinnerScene,
            };
            var existing = EditorBuildSettings.scenes.ToDictionary(
                item => item.path,
                item => item,
                StringComparer.Ordinal);
            foreach (var path in addressableScenes)
            {
                existing.Remove(path);
            }
            existing[ClubhouseScene] = new EditorBuildSettingsScene(
                ClubhouseScene,
                true);
            EditorBuildSettings.scenes = existing.Values
                .OrderBy(item => item.path, StringComparer.Ordinal)
                .ToArray();
        }

        private static void UpdateAddressables(AsterVeilChapterContent content)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true) ??
                throw new InvalidOperationException("Addressables settings are missing.");
            var paths = new (string path, string address, bool task26)[]
            {
                (Root + "/Content/SceneCatalog.asset", SceneCatalog.AddressablesKey, true),
                (AsterContent, AsterVeilProgressionService.ResourceName, true),
                (Root + "/Scenes/Benchmarks/Task17FlightGraybox.unity",
                    "Task17FlightGraybox", false),
                (Root + "/Scenes/Destinations/Mirra.unity", "Mirra", false),
                (Root + "/Scenes/Destinations/Task25VesperFlight.unity",
                    "Task25VesperFlight", false),
                (Root + "/Scenes/Destinations/KoroVesper.unity", "KoroVesper", false),
                (OpeningScene, "Opening", true),
                (AsterScene, "AsterVeil", true),
                (ReassemblyScene, "SignalReassembly", true),
                (DinnerScene, "DinnerEnding", true),
            };
            foreach (var pair in paths)
            {
                var guid = AssetDatabase.AssetPathToGUID(pair.path);
                if (string.IsNullOrEmpty(guid))
                {
                    throw new InvalidOperationException("Missing GUID for " + pair.path);
                }
                var entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
                entry.address = pair.address;
                if (pair.task26)
                {
                    entry.SetLabel("Task26ChapterOne", true, true, false);
                }
            }
            settings.SetDirty(
                AddressableAssetSettings.ModificationEvent.EntryMoved,
                settings.DefaultGroup,
                true,
                true);

        }

        private static void ValidateProducts()
        {
            LoadRequired<AsterVeilChapterContent>(AsterContent).ValidateOrThrow();
            var catalog = LoadRequired<SceneCatalog>(
                Root + "/Content/SceneCatalog.asset");
            catalog.Validate();
            var settings = AddressableAssetSettingsDefaultObject.Settings ??
                throw new InvalidOperationException("Addressables settings are missing.");
            foreach (var entry in catalog.Entries)
            {
                var published = settings.groups
                    .Where(group => group != null)
                    .SelectMany(group => group.entries)
                    .SingleOrDefault(candidate => candidate.address == entry.Address);
                if (published == null)
                {
                    throw new InvalidOperationException(
                        "SceneCatalog address is not published: " + entry.Address);
                }
            }
            if (!EditorBuildSettings.scenes.Any(item => item.enabled &&
                    item.path == ClubhouseScene))
            {
                throw new InvalidOperationException(
                    "The safe Clubhouse fallback must remain in build settings.");
            }
        }

        private static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path) ??
                AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            return sprite ?? throw new InvalidOperationException(
                "Missing imported sprite " + path);
        }

        private static T LoadRequired<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ??
            throw new InvalidOperationException("Missing required asset " + path);

        private static void DeleteIfExists(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        private static void SetObject(
            UnityEngine.Object target,
            string field,
            UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjects(
            UnityEngine.Object target,
            string field,
            UnityEngine.Object[] values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(UnityEngine.Object target, string field, int value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(
            UnityEngine.Object target,
            string field,
            float value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetVector2(
            UnityEngine.Object target,
            string field,
            Vector2 value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).vector2Value = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(UnityEngine.Object target, string field, int value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).enumValueIndex = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
