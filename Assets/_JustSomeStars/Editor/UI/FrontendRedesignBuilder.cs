using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JustSomeStars.Runtime.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace JustSomeStars.Editor.UI
{
    public static class FrontendRedesignBuilder
    {
        private const string ScenePath =
            "Assets/_JustSomeStars/Scenes/Core/Frontend.unity";
        private const string PrefabPath =
            "Assets/_JustSomeStars/Prefabs/UI/FrontendVisualRoot.prefab";
        private const string TextureRoot =
            "Assets/_JustSomeStars/Art/UI/FrontendRedesign/Textures/";
        private const string FontPath =
            "Assets/TextMesh Pro/Resources/Fonts & Materials/" +
            "LiberationSans SDF.asset";
        private const string LiberationLicensePath =
            "Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt";
        private const string ApacheLicensePath =
            "Assets/_JustSomeStars/Legal/Apache-2.0.txt";

        private static readonly Color WarmWhite =
            new Color32(245, 238, 228, 255);
        private static readonly Color MutedBlue =
            new Color32(157, 177, 197, 255);
        private static readonly Color Cyan =
            new Color32(91, 224, 239, 255);
        private static readonly Color Amber =
            new Color32(245, 173, 62, 255);
        private static readonly Vector2 ReferenceResolution =
            new Vector2(1616f, 720f);

        [MenuItem("Just Some Stars/Frontend/Build Approved Redesign")]
        public static void BuildAndPromote()
        {
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                ConfigureTextureImporters();
                var bindings = LoadRequiredAssets();
                EnsureAssetFolder("Assets/_JustSomeStars/Prefabs");
                EnsureAssetFolder("Assets/_JustSomeStars/Prefabs/UI");

                BuildPrefab(bindings);
                PromotePrefabIntoScene();
                ConfigureLandscapeOnly();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                ValidatePersistedResult();
                Debug.Log(
                    "[JSS Frontend Redesign] Approved landscape prefab and " +
                    "Frontend scene built and validated.");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void ConfigureTextureImporters()
        {
            var borders = new Dictionary<string, Vector4>
            {
                { "PrimaryPlate.png", new Vector4(60f, 54f, 60f, 54f) },
                { "SecondaryPlateSettings.png", new Vector4(45f, 44f, 45f, 44f) },
                { "SecondaryPlateCredits.png", new Vector4(45f, 44f, 45f, 44f) },
                { "SecondaryPlatePrivacy.png", new Vector4(45f, 44f, 45f, 44f) },
                { "ModalFrame.png", new Vector4(68f, 68f, 68f, 68f) },
            };

            foreach (var path in Directory.GetFiles(
                         TextureRoot,
                         "*.png",
                         SearchOption.TopDirectoryOnly)
                     .Select(path => path.Replace('\\', '/')))
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    throw new InvalidOperationException(
                        $"Texture importer was not available for {path}.");
                }

                var fileName = Path.GetFileName(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.sRGBTexture = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.crunchedCompression = false;
                importer.maxTextureSize = 2048;
                importer.spriteBorder = borders.TryGetValue(fileName, out var border)
                    ? border
                    : Vector4.zero;
                importer.SaveAndReimport();
            }
        }

        private static VisualAssets LoadRequiredAssets()
        {
            return new VisualAssets
            {
                Font = RequireAsset<TMP_FontAsset>(FontPath),
                LiberationLicense = RequireAsset<TextAsset>(
                    LiberationLicensePath),
                ApacheLicense = RequireAsset<TextAsset>(ApacheLicensePath),
                LandscapePlate = RequireSprite("LandscapePlate.png"),
                TitleOverlay = RequireSprite("TitleOverlay.png"),
                StarGlints = RequireSprite("StarGlints.png"),
                SignalBeam = RequireSprite("SignalBeam.png"),
                TelescopeLensGlow = RequireSprite("TelescopeLensGlow.png"),
                PrimaryPlate = RequireSprite("PrimaryPlate.png"),
                SettingsPlate = RequireSprite("SecondaryPlateSettings.png"),
                CreditsPlate = RequireSprite("SecondaryPlateCredits.png"),
                PrivacyPlate = RequireSprite("SecondaryPlatePrivacy.png"),
                ModalFrame = RequireSprite("ModalFrame.png"),
                SettingsGlyph = RequireSprite("GlyphSettings.png"),
                CreditsGlyph = RequireSprite("GlyphCredits.png"),
                PrivacyGlyph = RequireSprite("GlyphPrivacy.png"),
                InfoGlyph = RequireSprite("InfoGlyph.png"),
                StatusSparkle = RequireSprite("StatusSparkle.png"),
            };
        }

        private static void BuildPrefab(VisualAssets assets)
        {
            var root = CreateUiObject(
                "FrontendVisualRoot",
                parent: null,
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(FrontendView),
                typeof(FrontendController),
                typeof(UnityFrontendLifecycle),
                typeof(FrontendMotionDirector));
            try
            {
                ConfigureCanvas(root);
                var background = CreateFullStretch("BackgroundLayers", root.transform);
                var landscape = CreateFullScreenImage(
                    "LandscapePlate",
                    background.transform,
                    assets.LandscapePlate,
                    Color.white);
                var starGlints = CreateFullScreenImage(
                    "StarGlints",
                    background.transform,
                    assets.StarGlints,
                    Color.white);
                var signalBeam = CreateFullScreenImage(
                    "SignalBeam",
                    background.transform,
                    assets.SignalBeam,
                    Color.white);
                var lensGlow = CreateFullScreenImage(
                    "TelescopeLensGlow",
                    background.transform,
                    assets.TelescopeLensGlow,
                    Color.white);
                starGlints.color = WithAlpha(Color.white, 0.22f);
                signalBeam.color = WithAlpha(Color.white, 0f);
                lensGlow.color = WithAlpha(Color.white, 0.3f);
                _ = landscape;

                var safeArea = CreateFullStretch(
                    "SafeArea",
                    root.transform,
                    typeof(SafeAreaFitter));
                SetSerializedBool(
                    safeArea.GetComponent<SafeAreaFitter>(),
                    "m_ApplyHorizontal",
                    false);
                var titleGroup = CreateFullStretch(
                    "TitleGroup",
                    safeArea.transform,
                    typeof(CanvasGroup));
                var titleOverlay = CreateFullScreenImage(
                    "TitleOverlay",
                    titleGroup.transform,
                    assets.TitleOverlay,
                    new Color(1f, 0.83f, 0.62f, 0.82f));
                _ = titleOverlay;
                var semanticTitle = CreateText(
                    "TitleSemantic",
                    titleGroup.transform,
                    assets.Font,
                    "Just Some Stars",
                    56f,
                    Color.clear,
                    TextAlignmentOptions.TopLeft,
                    TextWrappingModes.NoWrap);
                SetReferenceRect(
                    semanticTitle.rectTransform,
                    new RectSpec(82f, 62f, 560f, 250f));
                semanticTitle.gameObject.SetActive(false);

                var statusGroup = CreateFullStretch(
                    "StatusGroup",
                    safeArea.transform,
                    typeof(CanvasGroup));
                CreateImage(
                    "StatusSparkle",
                    statusGroup.transform,
                    assets.StatusSparkle,
                    Cyan,
                    new RectSpec(1388f, 42f, 18f, 18f));
                CreateTextInRect(
                    "StatusLabel",
                    statusGroup.transform,
                    assets.Font,
                    "Development Flight",
                    18f,
                    Cyan,
                    TextAlignmentOptions.MidlineRight,
                    TextWrappingModes.NoWrap,
                    new RectSpec(1403f, 35f, 165f, 30f));
                CreateImage(
                    "StatusRule",
                    statusGroup.transform,
                    null,
                    WithAlpha(Cyan, 0.42f),
                    new RectSpec(1405f, 68f, 162f, 1.5f));
                var versionLabel = CreateTextInRect(
                    "VersionLabel",
                    statusGroup.transform,
                    assets.Font,
                    "Version 1.0",
                    15f,
                    WarmWhite,
                    TextAlignmentOptions.MidlineRight,
                    TextWrappingModes.NoWrap,
                    new RectSpec(1430f, 77f, 138f, 28f));

                var menuGroup = CreateFullStretch(
                    "MenuGroup",
                    safeArea.transform,
                    typeof(CanvasGroup));
                var continueButton = CreateInstrumentButton(
                    "ContinueButton",
                    menuGroup.transform,
                    assets.PrimaryPlate,
                    new RectSpec(72f, 425f, 536f, 128f),
                    interactable: false);
                CreateTextInRect(
                    "ContinueButtonLabel",
                    ButtonVisual(continueButton),
                    assets.Font,
                    "Continue",
                    40f,
                    new Color32(91, 102, 120, 255),
                    TextAlignmentOptions.Center,
                    TextWrappingModes.NoWrap,
                    new RectSpec(38f, 24f, 365f, 76f),
                    localCoordinates: true);
                CreateTextInRect(
                    "ContinueState",
                    ButtonVisual(continueButton),
                    assets.Font,
                    "Not yet",
                    20f,
                    Amber,
                    TextAlignmentOptions.Center,
                    TextWrappingModes.NoWrap,
                    new RectSpec(401f, 29f, 115f, 66f),
                    localCoordinates: true);
                CreateImage(
                    "InfoGlyph",
                    menuGroup.transform,
                    assets.InfoGlyph,
                    MutedBlue,
                    new RectSpec(84f, 558f, 21f, 21f));
                var continueExplanation = CreateTextInRect(
                    "ContinueExplanation",
                    menuGroup.transform,
                    assets.Font,
                    "Gameplay is not in this flight yet.",
                    18f,
                    MutedBlue,
                    TextAlignmentOptions.MidlineLeft,
                    TextWrappingModes.NoWrap,
                    new RectSpec(116f, 551f, 360f, 34f));

                var settingsButton = CreateSecondaryButton(
                    "SettingsButton",
                    menuGroup.transform,
                    assets.SettingsPlate,
                    assets.SettingsGlyph,
                    assets.Font,
                    "Settings",
                    new RectSpec(72f, 593f, 188f, 76f));
                var creditsButton = CreateSecondaryButton(
                    "CreditsButton",
                    menuGroup.transform,
                    assets.CreditsPlate,
                    assets.CreditsGlyph,
                    assets.Font,
                    "Credits",
                    new RectSpec(274f, 593f, 176f, 76f));
                var privacyButton = CreateSecondaryButton(
                    "PrivacyButton",
                    menuGroup.transform,
                    assets.PrivacyPlate,
                    assets.PrivacyGlyph,
                    assets.Font,
                    "Privacy",
                    new RectSpec(462f, 593f, 150f, 76f));

                var panel = CreateFullStretch(
                    "LocalPanel",
                    safeArea.transform,
                    typeof(CanvasGroup));
                var panelGroup = panel.GetComponent<CanvasGroup>();
                panelGroup.blocksRaycasts = true;
                panelGroup.interactable = true;
                CreateImage(
                    "PanelDim",
                    panel.transform,
                    null,
                    new Color(0.005f, 0.014f, 0.033f, 0.68f),
                    new RectSpec(0f, 0f, ReferenceResolution.x, ReferenceResolution.y));
                var panelFrame = CreateImage(
                    "PanelFrame",
                    panel.transform,
                    assets.ModalFrame,
                    Color.white,
                    new RectSpec(318f, 202f, 426f, 424f),
                    Image.Type.Sliced,
                    localCoordinates: true);
                CreateTextInRect(
                    "LocalPanelLabel",
                    panelFrame.transform,
                    assets.Font,
                    "LOCAL NOTE // NOTHING LEAVES THIS SCREEN",
                    12f,
                    Cyan,
                    TextAlignmentOptions.MidlineLeft,
                    TextWrappingModes.NoWrap,
                    new RectSpec(55f, 80f, 318f, 24f),
                    localCoordinates: true);
                var panelTitle = CreateTextInRect(
                    "PanelTitle",
                    panelFrame.transform,
                    assets.Font,
                    "Settings",
                    32f,
                    new Color32(247, 215, 171, 255),
                    TextAlignmentOptions.MidlineLeft,
                    TextWrappingModes.NoWrap,
                    new RectSpec(55f, 128f, 318f, 42f),
                    localCoordinates: true);
                panelTitle.fontStyle = FontStyles.Bold;

                var scroll = CreatePanelScroll(
                    panelFrame.transform,
                    assets.Font,
                    out var panelBody);
                var closeButton = CreateInstrumentButton(
                    "CloseButton",
                    panelFrame.transform,
                    assets.PrivacyPlate,
                    new RectSpec(54f, 320f, 318f, 70f),
                    interactable: true,
                    localCoordinates: true);
                CreateTextInRect(
                    "CloseButtonLabel",
                    ButtonVisual(closeButton),
                    assets.Font,
                    "Close",
                    19f,
                    Amber,
                    TextAlignmentOptions.Center,
                    TextWrappingModes.NoWrap,
                    new RectSpec(20f, 14f, 278f, 42f),
                    localCoordinates: true);

                var motion = root.GetComponent<FrontendMotionDirector>();
                SetReference(motion, "m_TitleGroup", titleGroup.GetComponent<CanvasGroup>());
                SetReference(motion, "m_StatusGroup", statusGroup.GetComponent<CanvasGroup>());
                SetReference(motion, "m_MenuGroup", menuGroup.GetComponent<CanvasGroup>());
                SetReference(motion, "m_TitleTransform", titleGroup.GetComponent<RectTransform>());
                SetReference(motion, "m_MenuTransform", menuGroup.GetComponent<RectTransform>());
                SetReference(motion, "m_PanelGroup", panelGroup);
                SetReference(motion, "m_PanelFrame", panelFrame.rectTransform);
                SetReference(motion, "m_StarGlints", starGlints);
                SetReference(motion, "m_SignalBeam", signalBeam);
                SetReference(motion, "m_TelescopeLensGlow", lensGlow);

                var view = root.GetComponent<FrontendView>();
                SetReference(view, "m_VersionLabel", versionLabel);
                SetReference(view, "m_ContinueButton", continueButton);
                SetReference(view, "m_ContinueExplanation", continueExplanation);
                SetReference(view, "m_SettingsButton", settingsButton);
                SetReference(view, "m_CreditsButton", creditsButton);
                SetReference(view, "m_PrivacyButton", privacyButton);
                SetReference(view, "m_PanelRoot", panel);
                SetReference(view, "m_PanelFrame", panelFrame.rectTransform);
                SetReference(view, "m_PanelTitle", panelTitle);
                SetReference(view, "m_PanelBody", panelBody);
                SetReference(view, "m_PanelScrollRect", scroll);
                SetReference(view, "m_CloseButton", closeButton);
                SetReference(view, "m_MotionDirector", motion);

                var lifecycle = root.GetComponent<UnityFrontendLifecycle>();
                var controller = root.GetComponent<FrontendController>();
                SetReference(controller, "m_ViewSource", view);
                SetReference(controller, "m_LifecycleSource", lifecycle);
                SetReference(
                    controller,
                    "m_LiberationSansLicense",
                    assets.LiberationLicense);
                SetReference(controller, "m_ApacheLicense", assets.ApacheLicense);
                panel.SetActive(false);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out var success);
                if (!success)
                {
                    throw new InvalidOperationException(
                        $"Failed to save {PrefabPath}.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void PromotePrefabIntoScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var eventSystem = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<EventSystem>(true))
                .SingleOrDefault();
            if (eventSystem == null)
            {
                throw new InvalidOperationException(
                    "Frontend scene must contain exactly one EventSystem before promotion.");
            }

            eventSystem.transform.SetParent(null, true);
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<FrontendView>(true) != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }

            if (scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<EventSystem>(true))
                .Count() != 1)
            {
                throw new InvalidOperationException(
                    "Frontend scene must retain exactly one EventSystem.");
            }

            var prefab = RequireAsset<GameObject>(PrefabPath);
            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException(
                    "Could not instantiate the redesigned Frontend prefab.");
            }

            instance.name = "FrontendVisualRoot";
            eventSystem.transform.SetParent(instance.transform, false);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException(
                    $"Could not save {ScenePath}.");
            }
        }

        private static void ConfigureLandscapeOnly()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            // Unity 6000.3 stores the effective Android values in the active
            // BuildProfile PlayerSettings override. Public setters target the
            // global object only, so persist the same values in the pinned
            // effective object used by this project's Android build.
            var settings = GetEffectivePlayerSettings();
            var serialized = new SerializedObject(settings);
            SetInt(serialized, "defaultScreenOrientation", (int)UIOrientation.AutoRotation);
            SetBool(serialized, "allowedAutorotateToPortrait", false);
            SetBool(serialized, "allowedAutorotateToPortraitUpsideDown", false);
            SetBool(serialized, "allowedAutorotateToLandscapeLeft", true);
            SetBool(serialized, "allowedAutorotateToLandscapeRight", true);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        private static PlayerSettings GetEffectivePlayerSettings()
        {
            var buildProfileType = typeof(BuildProfile);
            var globalField = buildProfileType.GetField(
                "s_GlobalPlayerSettings",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (globalField == null ||
                globalField.GetValue(null) is not PlayerSettings settings)
            {
                throw new InvalidOperationException(
                    "Unity 6000.3 global PlayerSettings reflection contract changed.");
            }

            var activeProfile = BuildProfile.GetActiveBuildProfile();
            if (activeProfile == null)
            {
                return settings;
            }

            var overrideField = buildProfileType.GetField(
                "m_PlayerSettings",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (overrideField == null)
            {
                throw new InvalidOperationException(
                    "Unity 6000.3 BuildProfile PlayerSettings contract changed.");
            }

            return overrideField.GetValue(activeProfile) as PlayerSettings ?? settings;
        }

        private static void SetBool(
            SerializedObject serialized,
            string name,
            bool value)
        {
            var property = serialized.FindProperty(name);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"Effective PlayerSettings is missing {name}.");
            }

            property.boolValue = value;
        }

        private static void SetInt(
            SerializedObject serialized,
            string name,
            int value)
        {
            var property = serialized.FindProperty(name);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"Effective PlayerSettings is missing {name}.");
            }

            property.intValue = value;
        }

        private static void ValidatePersistedResult()
        {
            var prefab = RequireAsset<GameObject>(PrefabPath);
            if (prefab.transform.Find("BackgroundLayers/LandscapePlate") == null ||
                prefab.transform.Find("SafeArea/TitleGroup/TitleOverlay") == null ||
                prefab.transform.Find("SafeArea/MenuGroup/ContinueButton") == null ||
                prefab.transform.Find(
                    "SafeArea/LocalPanel/PanelFrame/CloseButton") == null)
            {
                throw new InvalidOperationException(
                    "Redesigned prefab hierarchy did not persist.");
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var view = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<FrontendView>(true))
                .SingleOrDefault();
            if (view == null ||
                view.GetComponent<FrontendMotionDirector>() == null ||
                view.transform.Find("SafeArea/LocalPanel/PanelFrame/PanelBodyScroll") == null)
            {
                throw new InvalidOperationException(
                    "Redesigned Frontend scene did not reload with complete bindings.");
            }
        }

        private static ScrollRect CreatePanelScroll(
            Transform parent,
            TMP_FontAsset font,
            out TMP_Text panelBody)
        {
            var scrollObject = CreateUiObject(
                "PanelBodyScroll",
                parent,
                typeof(Image),
                typeof(ScrollRect));
            SetRectInParent(
                scrollObject.GetComponent<RectTransform>(),
                new RectSpec(55f, 194f, 318f, 118f));
            var scrollImage = scrollObject.GetComponent<Image>();
            scrollImage.color = new Color(0.01f, 0.03f, 0.065f, 0.16f);
            scrollImage.raycastTarget = true;

            var viewport = CreateFullStretch(
                "Viewport",
                scrollObject.transform,
                typeof(Image),
                typeof(RectMask2D));
            var viewportImage = viewport.GetComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.001f);
            viewportImage.raycastTarget = true;
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.offsetMin = new Vector2(0f, 0f);
            viewportRect.offsetMax = new Vector2(-18f, 0f);

            panelBody = CreateText(
                "PanelBody",
                viewport.transform,
                font,
                string.Empty,
                18f,
                new Color32(208, 207, 204, 255),
                TextAlignmentOptions.TopLeft,
                TextWrappingModes.Normal);
            panelBody.lineSpacing = 3f;
            var bodyRect = panelBody.rectTransform;
            bodyRect.anchorMin = new Vector2(0f, 1f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.pivot = new Vector2(0.5f, 1f);
            bodyRect.anchoredPosition = Vector2.zero;
            bodyRect.sizeDelta = new Vector2(-12f, 0f);
            var fitter = panelBody.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollbarObject = CreateUiObject(
                "VerticalScrollbar",
                scrollObject.transform,
                typeof(Image),
                typeof(Scrollbar));
            var scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
            scrollbarRect.anchorMin = new Vector2(1f, 0f);
            scrollbarRect.anchorMax = new Vector2(1f, 1f);
            scrollbarRect.pivot = new Vector2(1f, 0.5f);
            scrollbarRect.anchoredPosition = Vector2.zero;
            scrollbarRect.sizeDelta = new Vector2(8f, 0f);
            var scrollbarBackground = scrollbarObject.GetComponent<Image>();
            scrollbarBackground.color = new Color(0.06f, 0.16f, 0.24f, 0.72f);

            var slidingArea = CreateFullStretch(
                "SlidingArea",
                scrollbarObject.transform);
            var handle = CreateFullStretch(
                "Handle",
                slidingArea.transform,
                typeof(Image));
            var handleImage = handle.GetComponent<Image>();
            handleImage.color = WithAlpha(Cyan, 0.9f);
            var scrollbar = scrollbarObject.GetComponent<Scrollbar>();
            scrollbar.targetGraphic = handleImage;
            scrollbar.handleRect = handle.GetComponent<RectTransform>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            var scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.content = bodyRect;
            scroll.viewport = viewportRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 32f;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility =
                ScrollRect.ScrollbarVisibility.AutoHide;
            return scroll;
        }

        private static Button CreateSecondaryButton(
            string name,
            Transform parent,
            Sprite plate,
            Sprite glyph,
            TMP_FontAsset font,
            string label,
            RectSpec rect)
        {
            var button = CreateInstrumentButton(
                name,
                parent,
                plate,
                rect,
                interactable: true);
            CreateImage(
                name.Replace("Button", "Glyph"),
                ButtonVisual(button),
                glyph,
                WithAlpha(Cyan, 0.78f),
                new RectSpec(15f, 19f, 38f, 38f),
                Image.Type.Simple,
                localCoordinates: true);
            CreateTextInRect(
                name + "Label",
                ButtonVisual(button),
                font,
                label,
                21f,
                WarmWhite,
                TextAlignmentOptions.Center,
                TextWrappingModes.NoWrap,
                new RectSpec(48f, 14f, rect.Width - 58f, 48f),
                localCoordinates: true);
            return button;
        }

        private static Button CreateInstrumentButton(
            string name,
            Transform parent,
            Sprite sprite,
            RectSpec rect,
            bool interactable,
            bool localCoordinates = false)
        {
            const float minimumTouchTarget = 84f;
            var hitWidth = Mathf.Max(rect.Width, minimumTouchTarget);
            var hitHeight = Mathf.Max(rect.Height, minimumTouchTarget);
            var hitRect = new RectSpec(
                rect.X - (hitWidth - rect.Width) * 0.5f,
                rect.Y - (hitHeight - rect.Height) * 0.5f,
                hitWidth,
                hitHeight);
            var buttonObject = CreateUiObject(
                name,
                parent,
                typeof(Button),
                typeof(FrontendButtonVisual));
            if (localCoordinates)
            {
                SetRectInParent(
                    buttonObject.GetComponent<RectTransform>(),
                    hitRect);
            }
            else
            {
                SetReferenceRect(
                    buttonObject.GetComponent<RectTransform>(),
                    hitRect);
            }

            var plateObject = CreateUiObject(
                "PlateVisual",
                buttonObject.transform,
                typeof(Image));
            SetRectInParent(
                plateObject.GetComponent<RectTransform>(),
                new RectSpec(
                    (hitWidth - rect.Width) * 0.5f,
                    (hitHeight - rect.Height) * 0.5f,
                    rect.Width,
                    rect.Height));
            var image = plateObject.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = new Color(0.78f, 0.72f, 0.66f, 0.82f);
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.interactable = interactable;

            var edgeGlow = CreateFullStretch(
                "EdgeGlow",
                plateObject.transform,
                typeof(Image),
                typeof(CanvasGroup));
            var edgeImage = edgeGlow.GetComponent<Image>();
            edgeImage.sprite = sprite;
            edgeImage.type = Image.Type.Sliced;
            edgeImage.color = WithAlpha(Cyan, 0.48f);
            edgeImage.raycastTarget = false;
            var edgeGroup = edgeGlow.GetComponent<CanvasGroup>();
            edgeGroup.alpha = 0f;
            edgeGroup.interactable = false;
            edgeGroup.blocksRaycasts = false;
            SetReference(
                buttonObject.GetComponent<FrontendButtonVisual>(),
                "m_EdgeGlow",
                edgeGroup);
            return button;
        }

        private static Transform ButtonVisual(Button button)
        {
            var visual = button.transform.Find("PlateVisual");
            if (visual == null)
            {
                throw new InvalidOperationException(
                    $"{button.name} is missing its PlateVisual child.");
            }

            return visual;
        }

        private static void ConfigureCanvas(GameObject root)
        {
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            canvas.sortingOrder = 0;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        private static GameObject CreateFullStretch(
            string name,
            Transform parent,
            params Type[] components)
        {
            var result = CreateUiObject(name, parent, components);
            var rect = result.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return result;
        }

        private static Image CreateFullScreenImage(
            string name,
            Transform parent,
            Sprite sprite,
            Color color)
        {
            var imageObject = CreateFullStretch(name, parent, typeof(Image));
            var image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.raycastTarget = false;
            return image;
        }

        private static Image CreateImage(
            string name,
            Transform parent,
            Sprite sprite,
            Color color,
            RectSpec rect,
            Image.Type type = Image.Type.Simple,
            bool localCoordinates = false)
        {
            var imageObject = CreateUiObject(name, parent, typeof(Image));
            var image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = type;
            image.preserveAspect = type == Image.Type.Simple && sprite != null;
            image.raycastTarget = false;
            if (localCoordinates)
            {
                SetRectInParent(image.rectTransform, rect);
            }
            else
            {
                SetReferenceRect(image.rectTransform, rect);
            }

            return image;
        }

        private static void CreateImageInAnchors(
            string name,
            Transform parent,
            Sprite sprite,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            var imageObject = CreateUiObject(name, parent, typeof(Image));
            var rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
        }

        private static TMP_Text CreateTextInRect(
            string name,
            Transform parent,
            TMP_FontAsset font,
            string value,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment,
            TextWrappingModes wrapping,
            RectSpec rect,
            bool localCoordinates = false)
        {
            var text = CreateText(
                name,
                parent,
                font,
                value,
                fontSize,
                color,
                alignment,
                wrapping);
            if (localCoordinates)
            {
                SetRectInParent(text.rectTransform, rect);
            }
            else
            {
                SetReferenceRect(text.rectTransform, rect);
            }

            return text;
        }

        private static TMP_Text CreateTextInAnchors(
            string name,
            Transform parent,
            TMP_FontAsset font,
            string value,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment,
            TextWrappingModes wrapping,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            var text = CreateText(
                name,
                parent,
                font,
                value,
                fontSize,
                color,
                alignment,
                wrapping);
            text.rectTransform.anchorMin = anchorMin;
            text.rectTransform.anchorMax = anchorMax;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return text;
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            TMP_FontAsset font,
            string value,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment,
            TextWrappingModes wrapping)
        {
            var textObject = CreateUiObject(
                name,
                parent,
                typeof(TextMeshProUGUI));
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = wrapping;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject CreateUiObject(
            string name,
            Transform parent,
            params Type[] components)
        {
            var allComponents = new List<Type> { typeof(RectTransform) };
            allComponents.AddRange(components.Where(type =>
                type != null && type != typeof(RectTransform)));
            var result = new GameObject(name, allComponents.ToArray());
            if (parent != null)
            {
                result.transform.SetParent(parent, false);
            }

            return result;
        }

        private static void SetReferenceRect(RectTransform rect, RectSpec spec)
        {
            rect.anchorMin = new Vector2(
                spec.X / ReferenceResolution.x,
                1f - (spec.Y + spec.Height) / ReferenceResolution.y);
            rect.anchorMax = new Vector2(
                (spec.X + spec.Width) / ReferenceResolution.x,
                1f - spec.Y / ReferenceResolution.y);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetRectInParent(RectTransform rect, RectSpec spec)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(spec.X, -spec.Y);
            rect.sizeDelta = new Vector2(spec.Width, spec.Height);
        }

        private static void SetReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"Missing serialized property {target.GetType().Name}.{propertyName}.");
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetSerializedBool(
            UnityEngine.Object target,
            string propertyName,
            bool value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"Missing serialized property {target.GetType().Name}." +
                    propertyName + ".");
            }

            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static T RequireAsset<T>(string path)
            where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"Required asset was missing at {path}.");
            }

            return asset;
        }

        private static Sprite RequireSprite(string fileName)
        {
            return RequireAsset<Sprite>(TextureRoot + fileName);
        }

        private static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var folderName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folderName))
            {
                throw new InvalidOperationException(
                    $"Invalid asset folder path {path}.");
            }

            EnsureAssetFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, folderName)))
            {
                throw new InvalidOperationException(
                    $"Could not create asset folder {path}.");
            }
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private readonly struct RectSpec
        {
            public RectSpec(float x, float y, float width, float height)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }

            public float X { get; }
            public float Y { get; }
            public float Width { get; }
            public float Height { get; }
        }

        private sealed class VisualAssets
        {
            public TMP_FontAsset Font;
            public TextAsset LiberationLicense;
            public TextAsset ApacheLicense;
            public Sprite LandscapePlate;
            public Sprite TitleOverlay;
            public Sprite StarGlints;
            public Sprite SignalBeam;
            public Sprite TelescopeLensGlow;
            public Sprite PrimaryPlate;
            public Sprite SettingsPlate;
            public Sprite CreditsPlate;
            public Sprite PrivacyPlate;
            public Sprite ModalFrame;
            public Sprite SettingsGlyph;
            public Sprite CreditsGlyph;
            public Sprite PrivacyGlyph;
            public Sprite InfoGlyph;
            public Sprite StatusSparkle;
        }
    }
}
