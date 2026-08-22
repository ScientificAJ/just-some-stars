using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Xml;
using JustSomeStars.Runtime.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class FrontendSceneAssetTests
    {
        private const string BootScenePath =
            "Assets/_JustSomeStars/Scenes/Core/Boot.unity";
        private const string FrontendScenePath =
            "Assets/_JustSomeStars/Scenes/Core/Frontend.unity";
        private const string FrontendRuntimeFolder =
            "Assets/_JustSomeStars/Runtime/UI";
        private const string FrontendFontAssetPath =
            "Assets/TextMesh Pro/Resources/Fonts & Materials/" +
            "LiberationSans SDF.asset";
        private const string TmpSettingsAssetPath =
            "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const string LiberationLicenseAssetPath =
            "Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt";
        private const string ApacheLicenseAssetPath =
            "Assets/_JustSomeStars/Legal/Apache-2.0.txt";
        private const string CustomMainManifestPath =
            "Assets/Plugins/Android/AndroidManifest.xml";
        private const string LiberationSourceFontPath =
            "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";
        private const string LiberationSourceFontSha256 =
            "e5b0af421ea2bfbc1ac8d251d647268087ae82786234c57f757d1f0b90fa8b49";
        private const string LiberationLicenseSha256 =
            "37f8552e9a874ec10710dc0ede6a9adf168e6609fbd02a507f35629373b85a48";
        private const string ApacheLicenseSha256 =
            "cfc7749b96f63bd31c3c42b5c471bf756814053e847c10f3eb003417bc523d30";
        private const int ApacheLicenseByteLength = 11358;
        private const string LiberationSourceFontGuid =
            "e3265ab4bf004d28a9537516768c1c75";
        private const string EmojiResourceAssetPath =
            "Assets/TextMesh Pro/Resources/Sprite Assets/EmojiOne.asset";
        private const string EmojiResourceFolderPath =
            "Assets/TextMesh Pro/Resources/Sprite Assets";
        private const string EmojiSourceImagePath =
            "Assets/TextMesh Pro/Sprites/EmojiOne.png";
        private const string EmojiSourceJsonPath =
            "Assets/TextMesh Pro/Sprites/EmojiOne.json";
        private const string EmojiAttributionPath =
            "Assets/TextMesh Pro/Sprites/EmojiOne Attribution.txt";
        private const string FrontendInputActionsAssetPath =
            "Assets/_JustSomeStars/Art/UI/Generated/FrontendUIActions.asset";
        private const string RequiredLaunchCopy =
            "Just Some Stars Development Flight Version 1.0 Continue " +
            "Gameplay is not in this flight yet. Settings Credits Privacy Close";
        private const float AndroidDensityBaseline = 160f;
        private const float RequiredTouchTargetDp = 48f;
        private const float RequiredMinimumAuthoredFontSize = 12f;
        private const float RequiredBodyAndControlFontSize = 17f;
        private const float RequiredAuthoredFooterContrast = 7f;
        private const float GeometryTolerance = 0.01f;
        private const string RequiredGameActivityConfigChanges =
            "mcc|mnc|locale|touchscreen|keyboard|keyboardHidden|navigation|" +
            "orientation|screenLayout|uiMode|screenSize|smallestScreenSize|" +
            "fontScale|layoutDirection|density";

        private static readonly MobileProfile[] RequiredMobileProfiles =
        {
            new MobileProfile("Landscape", 1616f, 720f, 280f, 1f),
        };

        private static readonly IReadOnlyDictionary<string, string>
            IntentionalNonScrollLabels = new Dictionary<string, string>
            {
                { "StatusLabel", "Development Flight" },
                {
                    "ContinueExplanation",
                    "Gameplay is not in this flight yet."
                },
                { "PanelTitle", "Settings" },
                { "VersionLabel", "Version 1.0" },
                { "CloseButtonLabel", "Close" },
                { "ContinueState", "Not yet" },
                {
                    "LocalPanelLabel",
                    "LOCAL NOTE // NOTHING LEAVES THIS SCREEN"
                },
                { "SettingsButtonLabel", "Settings" },
                { "ContinueButtonLabel", "Continue" },
                { "TitleSemantic", "Just Some Stars" },
                { "CreditsButtonLabel", "Credits" },
                { "PrivacyButtonLabel", "Privacy" },
            };

        private static readonly HashSet<string> BodyAndControlTextNames =
            new HashSet<string>
            {
                "ContinueExplanation",
                "PanelBody",
                "ContinueButtonLabel",
                "SettingsButtonLabel",
                "CreditsButtonLabel",
                "PrivacyButtonLabel",
                "CloseButtonLabel",
            };

        [Test]
        public void BuildSettingsAndPlayerIdentity_AreCanonical()
        {
            var enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .ToArray();

            Assert.That(enabledScenes, Has.Length.GreaterThanOrEqualTo(2));
            Assert.That(enabledScenes[0].path, Is.EqualTo(BootScenePath));
            Assert.That(enabledScenes[1].path, Is.EqualTo(FrontendScenePath));
            Assert.That(PlayerSettings.companyName, Is.EqualTo("ScientificAJ"));
            Assert.That(PlayerSettings.productName, Is.EqualTo("Just Some Stars"));
            Assert.That(PlayerSettings.bundleVersion, Is.Not.Null.And.Not.Empty);
            Assert.That(PlayerSettings.bundleVersion, Is.EqualTo("1.0"));
            Assert.That(Application.version, Is.EqualTo(PlayerSettings.bundleVersion));
            var inputHandler = FindPlayerSettingsProperty("activeInputHandler");
            Assert.That(inputHandler, Is.Not.Null);
            Assert.That(
                inputHandler.intValue,
                Is.EqualTo(1),
                "Task 5 requires New Input System only; Old/Both can hide " +
                "device touch and Back integration defects.");
            var applicationEntry = FindPlayerSettingsProperty(
                "androidApplicationEntry");
            Assert.That(applicationEntry, Is.Not.Null);
            Assert.That(
                applicationEntry.intValue,
                Is.EqualTo(2),
                "Task 5's custom main manifest is the Unity GameActivity " +
                "template, not the legacy Activity entry point.");
            AssertCustomAndroidMainManifest();
        }

        [Test]
        public void TmpEssentials_AreCanonicalStaticAndLicensed()
        {
            var settings = AssetDatabase.LoadMainAssetAtPath(
                TmpSettingsAssetPath);
            Assert.That(settings, Is.Not.Null);
            var serializedSettings = new SerializedObject(settings);
            Assert.That(
                serializedSettings.FindProperty("assetVersion")?.stringValue,
                Is.EqualTo("2"));

            var fontAsset = AssertStaticFrontendFont();
            Assert.That(
                serializedSettings.FindProperty("m_defaultFontAsset")
                    ?.objectReferenceValue,
                Is.SameAs(fontAsset));
            Assert.That(
                serializedSettings.FindProperty("m_defaultSpriteAsset")
                    ?.objectReferenceValue,
                Is.Null,
                "Task 5 uses no TMP sprites; retaining the default EmojiOne " +
                "reference redistributes an unused attributed payload.");
            Assert.That(
                serializedSettings.FindProperty("m_enableEmojiSupport")
                    ?.boolValue,
                Is.False);
            Assert.That(
                AssetDatabase.AssetPathExists(EmojiResourceAssetPath),
                Is.False,
                "A sprite asset under Resources is included even with no " +
                "serialized scene reference.");
            Assert.That(
                AssetDatabase.AssetPathExists(EmojiSourceImagePath),
                Is.True,
                "Only the unused generated Resources asset is deletion-owned.");
            Assert.That(
                AssetDatabase.AssetPathExists(EmojiSourceJsonPath),
                Is.True,
                "The official EmojiOne source JSON remains outside Resources.");
            Assert.That(
                AssetDatabase.AssetPathExists(EmojiAttributionPath),
                Is.True,
                "The canonical TMP source attribution must remain intact.");

            var license = AssetDatabase.LoadAssetAtPath<TextAsset>(
                LiberationLicenseAssetPath);
            Assert.That(license, Is.Not.Null);
            Assert.That(
                license.text,
                Does.Contain("SIL OPEN FONT LICENSE Version 1.1"));
            AssertFileSha256(
                LiberationLicenseAssetPath,
                LiberationLicenseSha256);
            AssertFontSupportsText(fontAsset, license.text);

            var apacheLicense = AssetDatabase.LoadAssetAtPath<TextAsset>(
                ApacheLicenseAssetPath);
            Assert.That(apacheLicense, Is.Not.Null);
            Assert.That(
                apacheLicense.text,
                Does.StartWith(
                    "\n                                 Apache License"));
            Assert.That(
                apacheLicense.text,
                Does.Contain("Version 2.0, January 2004"));
            Assert.That(
                new FileInfo(AssetFileSystemPath(ApacheLicenseAssetPath)).Length,
                Is.EqualTo(ApacheLicenseByteLength));
            AssertFileSha256(
                ApacheLicenseAssetPath,
                ApacheLicenseSha256);
            AssertFontSupportsText(fontAsset, apacheLicense.text);

            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(
                LiberationSourceFontPath);
            Assert.That(sourceFont, Is.Not.Null);
            Assert.That(
                AssetDatabase.AssetPathExists(LiberationSourceFontPath),
                Is.True);
            Assert.That(
                AssetDatabase.GetAssetPath(sourceFont),
                Is.EqualTo(LiberationSourceFontPath));
            Assert.That(
                AssetDatabase.AssetPathToGUID(LiberationSourceFontPath),
                Is.EqualTo(LiberationSourceFontGuid));
            AssertFileSha256(
                LiberationSourceFontPath,
                LiberationSourceFontSha256);
        }

        [Test]
        public void AssetTree_HasNoEmptyDirectoriesOrOrphanedMetaFiles()
        {
            var assetsRoot = Path.GetFullPath(Application.dataPath);
            Assert.That(Directory.Exists(assetsRoot), Is.True, assetsRoot);
            var violations = new List<string>();

            foreach (var directory in Directory.GetDirectories(
                         assetsRoot,
                         "*",
                         SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.Ordinal))
            {
                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    violations.Add("Empty asset directory: " + directory);
                }

                var folderMeta = directory + ".meta";
                if (!File.Exists(folderMeta))
                {
                    violations.Add(
                        "Asset directory has no sibling folder meta: " +
                        directory + " (expected " + folderMeta + ")");
                }
            }

            foreach (var meta in Directory.GetFiles(
                         assetsRoot,
                         "*.meta",
                         SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.Ordinal))
            {
                var target = meta.Substring(
                    0,
                    meta.Length - ".meta".Length);
                if (!File.Exists(target) && !Directory.Exists(target))
                {
                    violations.Add(
                        "Orphan asset meta: " + meta +
                        " (missing target " + target + ")");
                }
            }

            if (AssetDatabase.IsValidFolder(EmojiResourceFolderPath))
            {
                violations.Add(
                    "Deleted Emoji Resources folder is still registered in " +
                    "AssetDatabase: " + EmojiResourceFolderPath);
            }

            var emojiFolderMeta = Path.Combine(
                assetsRoot,
                "TextMesh Pro",
                "Resources",
                "Sprite Assets.meta");
            if (File.Exists(emojiFolderMeta))
            {
                violations.Add(
                    "Deleted Emoji Resources folder meta still exists: " +
                    emojiFolderMeta);
            }

            Assert.That(
                violations.Count,
                Is.EqualTo(0),
                "Asset-tree integrity violations:\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void FrontendScene_ContainsResponsiveTruthfulLaunchScreen()
        {
            WithFrontendRoot(root =>
            {
                Assert.That(root.name, Is.EqualTo("FrontendVisualRoot"));
                Assert.That(root.GetComponent<FrontendController>(), Is.Not.Null);
                Assert.That(root.GetComponent<UnityFrontendLifecycle>(), Is.Not.Null);

                var canvasObject = RequireCanvasRoot(root);
                var canvas = canvasObject.GetComponent<Canvas>();
                Assert.That(canvas, Is.Not.Null);
                Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                Assert.That(
                    ComponentByFullName(canvasObject, "UnityEngine.UI.GraphicRaycaster"),
                    Is.Not.Null);

                var scaler = ComponentByFullName(
                    canvasObject,
                    "UnityEngine.UI.CanvasScaler");
                Assert.That(
                    Property(scaler, "uiScaleMode").ToString(),
                    Is.EqualTo("ScaleWithScreenSize"));
                Assert.That(
                    Property(scaler, "screenMatchMode").ToString(),
                    Is.EqualTo("MatchWidthOrHeight"));
                var referenceResolution = (Vector2)Property(
                    scaler,
                    "referenceResolution");
                Assert.That(
                    referenceResolution.x,
                    Is.GreaterThan(0f));
                Assert.That(
                    referenceResolution.y,
                    Is.GreaterThan(0f));
                var matchWidthOrHeight = (float)Property(
                    scaler,
                    "matchWidthOrHeight");
                Assert.That(matchWidthOrHeight, Is.GreaterThanOrEqualTo(0f));
                Assert.That(matchWidthOrHeight, Is.LessThanOrEqualTo(1f));

                var safeArea = FindDescendant(root.transform, "SafeArea");
                var safeAreaFitter = safeArea.GetComponent<SafeAreaFitter>();
                Assert.That(safeAreaFitter, Is.Not.Null);
                var serializedSafeArea = new SerializedObject(safeAreaFitter);
                Assert.That(
                    serializedSafeArea.FindProperty("m_ApplyHorizontal")
                        ?.boolValue,
                    Is.False,
                    "The immutable full-bleed target owns horizontal " +
                    "composition; its visible controls already include " +
                    "landscape edge clearance and must not drift inward.");
                Assert.That(
                    serializedSafeArea.FindProperty("m_ApplyVertical")
                        ?.boolValue,
                    Is.True);
                Assert.That(safeArea.transform.parent, Is.SameAs(canvasObject.transform));
                var safeAreaRect = safeArea.GetComponent<RectTransform>();
                Assert.That(safeAreaRect.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(safeAreaRect.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(safeAreaRect.offsetMin, Is.EqualTo(Vector2.zero));
                Assert.That(safeAreaRect.offsetMax, Is.EqualTo(Vector2.zero));

                var activeText = TextValues(root, includeInactive: false);
                var allText = TextValues(root, includeInactive: true);
                Assert.That(allText, Does.Contain("Just Some Stars"));
                Assert.That(activeText, Does.Contain("Development Flight"));
                Assert.That(activeText, Does.Contain($"Version {Application.version}"));
                Assert.That(activeText, Does.Contain("Continue"));
                Assert.That(
                    activeText,
                    Does.Contain("Gameplay is not in this flight yet."));
                Assert.That(activeText, Does.Contain("Settings"));
                Assert.That(activeText, Does.Contain("Credits"));
                Assert.That(activeText, Does.Contain("Privacy"));
                Assert.That(
                    string.Join(" ", allText),
                    Does.Not.Contain("NO NETWORK"));
                Assert.That(
                    FindOptionalDescendant(root.transform, "Footer"),
                    Is.Null,
                    "The approved minimal target has no footer copy.");
                Assert.That(
                    FindOptionalDescendant(root.transform, "BackdropSignalCopy"),
                    Is.Null,
                    "The approved target has no decorative copy competing " +
                    "with functional UI.");
                foreach (var requiredVisual in new[]
                         {
                             "LandscapePlate",
                             "TitleOverlay",
                             "StarGlints",
                             "SignalBeam",
                             "TelescopeLensGlow",
                             "PanelFrame",
                         })
                {
                    Assert.That(
                        FindDescendant(root.transform, requiredVisual),
                        Is.Not.Null,
                        requiredVisual);
                }
                foreach (var textComponent in TextComponents(
                             root,
                             includeInactive: true))
                {
                    var font = Property(textComponent, "font") as UnityEngine.Object;
                    Assert.That(
                        font,
                        Is.Not.Null,
                        $"{textComponent.name} has no TMP font asset.");
                    Assert.That(
                        AssetDatabase.GetAssetPath(font),
                        Is.EqualTo(FrontendFontAssetPath),
                        $"{textComponent.name} does not use the generated font.");
                }

                AssertStaticFrontendFont();

                var continueObject = FindDescendant(root.transform, "ContinueButton");
                var continueButton = ComponentByFullName(
                    continueObject,
                    "UnityEngine.UI.Button");
                Assert.That((bool)Property(continueButton, "interactable"), Is.False);

                var localPanel = FindDescendant(root.transform, "LocalPanel");
                Assert.That(localPanel.activeSelf, Is.False);
                Assert.That(
                    localPanel.transform.GetSiblingIndex(),
                    Is.EqualTo(localPanel.transform.parent.childCount - 1),
                    "The modal must be the last SafeArea sibling so its " +
                    "dimmer and raycast surface cover every launch control.");
            });
        }

        [Test]
        public void FrontendScene_HasValidLocalOnlyControllerAndInputBindings()
        {
            WithFrontendRoot(root =>
            {
                var controller = root.GetComponent<FrontendController>();
                var lifecycle = root.GetComponent<UnityFrontendLifecycle>();
                var view = root.GetComponentInChildren<FrontendView>(true);
                Assert.That(controller, Is.Not.Null);
                Assert.That(lifecycle, Is.Not.Null);
                Assert.That(view, Is.Not.Null);

                var serializedController = new SerializedObject(controller);
                Assert.That(
                    serializedController.FindProperty("m_ViewSource")
                        ?.objectReferenceValue,
                    Is.SameAs(view));
                Assert.That(
                    serializedController.FindProperty("m_LifecycleSource")
                        ?.objectReferenceValue,
                    Is.SameAs(lifecycle));
                var license = AssetDatabase.LoadAssetAtPath<TextAsset>(
                    LiberationLicenseAssetPath);
                Assert.That(license, Is.Not.Null);
                Assert.That(
                    serializedController.FindProperty("m_LiberationSansLicense")
                        ?.objectReferenceValue,
                    Is.SameAs(license));
                var apacheLicense = AssetDatabase.LoadAssetAtPath<TextAsset>(
                    ApacheLicenseAssetPath);
                Assert.That(apacheLicense, Is.Not.Null);
                Assert.That(
                    serializedController.FindProperty("m_ApacheLicense")
                        ?.objectReferenceValue,
                    Is.SameAs(apacheLicense));
                Assert.That(
                    AssetDatabase.GetDependencies(
                            FrontendScenePath,
                            recursive: true)
                        .Select(path => path.Replace('\\', '/')),
                    Does.Contain(LiberationLicenseAssetPath));
                Assert.That(
                    AssetDatabase.GetDependencies(
                            FrontendScenePath,
                            recursive: true)
                        .Select(path => path.Replace('\\', '/')),
                    Does.Contain(ApacheLicenseAssetPath));

                var serializedView = new SerializedObject(view);
                foreach (var propertyName in new[]
                         {
                             "m_VersionLabel",
                             "m_ContinueButton",
                             "m_ContinueExplanation",
                             "m_SettingsButton",
                             "m_CreditsButton",
                             "m_PrivacyButton",
                             "m_PanelRoot",
                             "m_PanelTitle",
                             "m_PanelBody",
                             "m_PanelScrollRect",
                             "m_CloseButton",
                         })
                {
                    var property = serializedView.FindProperty(propertyName);
                    Assert.That(property, Is.Not.Null, propertyName);
                    Assert.That(
                        property.objectReferenceValue,
                        Is.Not.Null,
                        propertyName);
                }

                var eventSystem = FindDescendant(root.transform, "EventSystem");
                Assert.That(
                    ComponentByFullName(
                        eventSystem,
                        "UnityEngine.EventSystems.EventSystem"),
                    Is.Not.Null);
                var inputModule = ComponentByFullName(
                    eventSystem,
                    "UnityEngine.InputSystem.UI.InputSystemUIInputModule");
                Assert.That(inputModule, Is.Not.Null);
                Assert.That(((Behaviour)inputModule).enabled, Is.True);
                var actionsAsset = Property(inputModule, "actionsAsset");
                var pointReference = Property(inputModule, "point");
                var leftClickReference = Property(inputModule, "leftClick");
                Assert.That(actionsAsset, Is.Not.Null);
                Assert.That(
                    AssetDatabase.GetAssetPath((UnityEngine.Object)actionsAsset),
                    Is.EqualTo(FrontendInputActionsAssetPath));
                Assert.That(pointReference, Is.Not.Null);
                Assert.That(leftClickReference, Is.Not.Null);

                var pointAction = Property(pointReference, "action");
                var leftClickAction = Property(leftClickReference, "action");
                Assert.That(pointAction, Is.Not.Null);
                Assert.That(leftClickAction, Is.Not.Null);
                Assert.That(BindingCount(pointAction), Is.GreaterThan(0));
                Assert.That(BindingCount(leftClickAction), Is.GreaterThan(0));
            });
        }

        [Test]
        public void FrontendScene_LicensePanelUsesClippedTopResetVerticalScroll()
        {
            WithFrontendRoot(root =>
            {
                var panelCard = FindDescendant(root.transform, "PanelFrame");
                var scrollObject = FindDescendant(
                    root.transform,
                    "PanelBodyScroll");
                var viewport = FindDescendant(root.transform, "Viewport");
                var panelBody = FindDescendant(root.transform, "PanelBody");

                Assert.That(
                    scrollObject.transform.parent,
                    Is.SameAs(panelCard.transform));
                Assert.That(
                    viewport.transform.parent,
                    Is.SameAs(scrollObject.transform));
                Assert.That(
                    panelBody.transform.parent,
                    Is.SameAs(viewport.transform));

                var scrollRect = ComponentByFullName(
                    scrollObject,
                    "UnityEngine.UI.ScrollRect");
                Assert.That(scrollRect, Is.Not.Null);
                Assert.That((bool)Property(scrollRect, "horizontal"), Is.False);
                Assert.That((bool)Property(scrollRect, "vertical"), Is.True);
                Assert.That(
                    Property(scrollRect, "movementType").ToString(),
                    Is.EqualTo("Clamped"));
                Assert.That(
                    Property(scrollRect, "viewport"),
                    Is.SameAs(viewport.GetComponent<RectTransform>()));
                Assert.That(
                    Property(scrollRect, "content"),
                    Is.SameAs(panelBody.GetComponent<RectTransform>()));
                Assert.That(
                    panelBody.GetComponent<RectTransform>().anchoredPosition,
                    Is.EqualTo(Vector2.zero),
                    "The persisted content transform must be top-origin; the " +
                    "live normalized position is reset when a panel opens.");

                var viewportRect = viewport.GetComponent<RectTransform>();
                Assert.That(viewportRect.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(viewportRect.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(viewportRect.offsetMin, Is.EqualTo(Vector2.zero));
                Assert.That(
                    viewportRect.offsetMax,
                    Is.EqualTo(new Vector2(-18f, 0f)),
                    "The right inset is reserved for the visible scrollbar.");
                Assert.That(
                    ComponentByFullName(
                        viewport,
                        "UnityEngine.UI.RectMask2D"),
                    Is.Not.Null);
                var viewportImage = ComponentByFullName(
                    viewport,
                    "UnityEngine.UI.Image");
                Assert.That(viewportImage, Is.Not.Null);
                Assert.That(
                    (bool)Property(viewportImage, "raycastTarget"),
                    Is.True);

                var bodyRect = panelBody.GetComponent<RectTransform>();
                Assert.That(bodyRect.anchorMin, Is.EqualTo(new Vector2(0f, 1f)));
                Assert.That(bodyRect.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(bodyRect.pivot, Is.EqualTo(new Vector2(0.5f, 1f)));
                var contentFitter = ComponentByFullName(
                    panelBody,
                    "UnityEngine.UI.ContentSizeFitter");
                Assert.That(contentFitter, Is.Not.Null);
                Assert.That(
                    Property(contentFitter, "verticalFit").ToString(),
                    Is.EqualTo("PreferredSize"));

                var view = root.GetComponentInChildren<FrontendView>(true);
                var serializedView = new SerializedObject(view);
                Assert.That(
                    serializedView.FindProperty("m_PanelScrollRect")
                        ?.objectReferenceValue,
                    Is.SameAs(scrollRect));
            });
        }

        [Test]
        public void FrontendScene_InteractiveTargetsMeet48DpAtRequiredMobileProfiles()
        {
            WithFrontendRoot(root =>
            {
                var canvasObject = RequireCanvasRoot(root);
                var canvasRect = canvasObject.GetComponent<RectTransform>();
                var scaler = ComponentByFullName(
                    canvasObject,
                    "UnityEngine.UI.CanvasScaler");
                var buttons = ComponentsByFullName(
                    root,
                    "UnityEngine.UI.Button");
                Assert.That(buttons.Count, Is.EqualTo(5));

                foreach (var profile in RequiredMobileProfiles)
                {
                    var scale = CalculateCanvasScale(scaler, profile);
                    var logicalCanvas = LogicalCanvasRect(profile, scale);
                    foreach (var button in buttons)
                    {
                        var rectTransform = button.GetComponent<RectTransform>();
                        var logicalRect = ResolveSyntheticRect(
                            rectTransform,
                            canvasRect,
                            logicalCanvas);
                        AssertPhysicalAxisAtLeastDp(
                            logicalRect.width,
                            scale,
                            profile,
                            button.name,
                            "width");
                        AssertPhysicalAxisAtLeastDp(
                            logicalRect.height,
                            scale,
                            profile,
                            button.name,
                            "height");
                    }

                    var scrollObject = FindDescendant(
                        root.transform,
                        "PanelBodyScroll");
                    var scrollRect = ComponentByFullName(
                        scrollObject,
                        "UnityEngine.UI.ScrollRect");
                    var viewport = Property(scrollRect, "viewport") as
                        RectTransform;
                    Assert.That(viewport, Is.Not.Null);
                    var viewportRect = ResolveSyntheticRect(
                        viewport,
                        canvasRect,
                        logicalCanvas);
                    AssertPhysicalAxisAtLeastDp(
                        viewportRect.width,
                        scale,
                        profile,
                        "PanelBodyScroll/Viewport",
                        "width");
                    AssertPhysicalAxisAtLeastDp(
                        viewportRect.height,
                        scale,
                        profile,
                        "PanelBodyScroll/Viewport",
                        "height");
                }
            });
        }

        [Test]
        public void FrontendScene_TypographyMatchesReadableApprovedLandscapeScale()
        {
            WithFrontendRoot(root =>
            {
                var canvasObject = RequireCanvasRoot(root);
                var scaler = ComponentByFullName(
                    canvasObject,
                    "UnityEngine.UI.CanvasScaler");
                var textComponents = TextComponents(
                    root,
                    includeInactive: true);
                Assert.That(textComponents, Is.Not.Empty);
                Assert.That(
                    textComponents.Select(component => component.name),
                    Does.Contain("PanelBody"));

                foreach (var textComponent in textComponents)
                {
                    var authoredSize = AuthoredTypographyFloor(textComponent);
                    Assert.That(
                        authoredSize,
                        Is.GreaterThanOrEqualTo(
                            RequiredMinimumAuthoredFontSize),
                        $"{textComponent.name} falls below the approved " +
                        "landscape design's authored type floor.");

                    if (!BodyAndControlTextNames.Contains(textComponent.name))
                    {
                        continue;
                    }

                    Assert.That(
                        authoredSize,
                        Is.GreaterThanOrEqualTo(
                            RequiredBodyAndControlFontSize),
                        $"{textComponent.name} body/control text is smaller " +
                        "than the approved landscape target.");
                }
            });
        }

        [Test]
        public void FrontendScene_AllIntentionalNonScrollCopyFitsAtRequiredMobileProfiles()
        {
            WithFrontendRoot(root =>
            {
                var canvasObject = RequireCanvasRoot(root);
                var canvasRect = canvasObject.GetComponent<RectTransform>();
                var scaler = ComponentByFullName(
                    canvasObject,
                    "UnityEngine.UI.CanvasScaler");
                var textComponents = TextComponents(
                        root,
                        includeInactive: true)
                    .Where(component =>
                        component.name != "PanelBody" &&
                        component.name != "BackdropSignalCopy")
                    .OrderBy(component => component.name)
                    .ToArray();
                Assert.That(
                    textComponents.Select(component => component.name),
                    Is.EqualTo(IntentionalNonScrollLabels.Keys.OrderBy(
                        name => name)));

                foreach (var textComponent in textComponents)
                {
                    var expectedText = IntentionalNonScrollLabels[
                        textComponent.name];
                    Assert.That(
                        Property(textComponent, "text"),
                        Is.EqualTo(expectedText),
                        textComponent.name);

                    foreach (var profile in RequiredMobileProfiles)
                    {
                        AssertCompleteTextFitsInProfile(
                            textComponent,
                            expectedText,
                            profile,
                            scaler,
                            canvasRect);
                    }
                }

                var panelTitle = textComponents.Single(component =>
                    component.name == "PanelTitle");
                foreach (var profile in RequiredMobileProfiles)
                {
                    AssertCompleteTextFitsInProfile(
                        panelTitle,
                        "Credits & Licenses",
                        profile,
                        scaler,
                        canvasRect);
                }
            });
        }

        [Test]
        public void FrontendScene_RequiredRegionsStayContainedAndOrderedAtApprovedLandscapeProfile()
        {
            WithFrontendRoot(root =>
            {
                var canvasObject = RequireCanvasRoot(root);
                var canvasRect = canvasObject.GetComponent<RectTransform>();
                var scaler = ComponentByFullName(
                    canvasObject,
                    "UnityEngine.UI.CanvasScaler");
                var requiredRegions = new[]
                {
                    "BackgroundLayers",
                    "TitleGroup",
                    "StatusGroup",
                    "MenuGroup",
                    "LocalPanel",
                    "PanelFrame",
                };

                foreach (var profile in RequiredMobileProfiles)
                {
                    var scale = CalculateCanvasScale(scaler, profile);
                    var logicalCanvas = LogicalCanvasRect(profile, scale);
                    foreach (var regionName in requiredRegions)
                    {
                        var region = FindDescendant(
                            root.transform,
                            regionName);
                        AssertRectContained(
                            logicalCanvas,
                            ResolveSyntheticRect(
                                region.GetComponent<RectTransform>(),
                                canvasRect,
                                logicalCanvas),
                            $"{profile.Name}/{regionName}");
                    }

                    foreach (var component in TextComponents(
                                 root,
                                 includeInactive: true)
                             .Concat(ComponentsByFullName(
                                 root,
                                 "UnityEngine.UI.Button")))
                    {
                        AssertRectContained(
                            logicalCanvas,
                            ResolveSyntheticRect(
                                component.GetComponent<RectTransform>(),
                                canvasRect,
                                logicalCanvas),
                            $"{profile.Name}/{component.name}");
                    }

                    var panelCardObject = FindDescendant(
                        root.transform,
                        "PanelFrame");
                    var panelCard = ResolveSyntheticRect(
                        panelCardObject.GetComponent<RectTransform>(),
                        canvasRect,
                        logicalCanvas);
                    foreach (Transform child in panelCardObject.transform)
                    {
                        var childRect = child as RectTransform;
                        Assert.That(childRect, Is.Not.Null, child.name);
                        AssertRectContained(
                            panelCard,
                            ResolveSyntheticRect(
                                childRect,
                                canvasRect,
                                logicalCanvas),
                            $"{profile.Name}/PanelFrame/{child.name}");
                    }

                    var close = ResolveNamedRect(
                        root,
                        "CloseButton",
                        canvasRect,
                        logicalCanvas);
                    var panelBodyScroll = ResolveNamedRect(
                        root,
                        "PanelBodyScroll",
                        canvasRect,
                        logicalCanvas);
                    Assert.That(
                        close.yMax,
                        Is.LessThanOrEqualTo(
                            panelBodyScroll.yMin + GeometryTolerance),
                        $"{profile.Name}: Close overlaps the scroll viewport.");
                }
            });
        }

        [Test]
        public void FrontendRuntime_UsesNoLegacyPollingOrExternalNavigation()
        {
            var sources = Directory.GetFiles(
                    FrontendRuntimeFolder,
                    "*.cs",
                    SearchOption.TopDirectoryOnly)
                .Select(File.ReadAllText)
                .ToArray();

            Assert.That(sources, Is.Not.Empty);
            foreach (var source in sources)
            {
                Assert.That(source, Does.Not.Contain("UnityEngine.Input."));
                Assert.That(source, Does.Not.Contain("Input.Get"));
                Assert.That(source, Does.Not.Contain("Application.OpenURL"));
                Assert.That(source, Does.Not.Contain("UnityWebRequest"));
            }
        }

        private static void WithFrontendRoot(Action<GameObject> assertion)
        {
            var previousSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(
                    FrontendScenePath,
                    OpenSceneMode.Single);
                Assert.That(scene.IsValid(), Is.True);
                Assert.That(scene.isLoaded, Is.True);
                var roots = scene.GetRootGameObjects();
                Assert.That(roots, Has.Length.EqualTo(1));
                assertion(roots[0]);
            }
            finally
            {
                if (previousSetup.Length > 0)
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

        private static GameObject FindDescendant(Transform root, string name)
        {
            var matches = root
                .GetComponentsInChildren<Transform>(includeInactive: true)
                .Where(transform => transform.name == name)
                .ToArray();
            Assert.That(matches, Has.Length.EqualTo(1), name);
            return matches[0].gameObject;
        }

        private static GameObject RequireCanvasRoot(GameObject root)
        {
            Assert.That(root, Is.Not.Null);
            Assert.That(root.GetComponent<Canvas>(), Is.Not.Null);
            return root;
        }

        private static GameObject FindOptionalDescendant(
            Transform root,
            string name)
        {
            var matches = root
                .GetComponentsInChildren<Transform>(includeInactive: true)
                .Where(transform => transform.name == name)
                .ToArray();
            Assert.That(matches, Has.Length.LessThanOrEqualTo(1), name);
            return matches.Length == 0 ? null : matches[0].gameObject;
        }

        private static bool RectsOverlap(Rect first, Rect second)
        {
            return first.xMin < second.xMax - GeometryTolerance &&
                   first.xMax > second.xMin + GeometryTolerance &&
                   first.yMin < second.yMax - GeometryTolerance &&
                   first.yMax > second.yMin + GeometryTolerance;
        }

        private static void AssertFooterContrastAgainstCompositedBackdrop(
            Component text,
            Component baseBackground,
            Component overlay,
            bool expectWarmOverlay,
            string label)
        {
            Assert.That(text, Is.Not.Null, label);
            Assert.That(baseBackground, Is.Not.Null, label);
            Assert.That(overlay, Is.Not.Null, label);
            var foreground = (Color)Property(text, "color");
            var backdrop = (Color)Property(baseBackground, "color");
            var overlayColor = (Color)Property(overlay, "color");
            Assert.That(
                foreground.r,
                Is.GreaterThan(foreground.b),
                label + " foreground must retain the warm visual role.");
            Assert.That(
                backdrop.b,
                Is.GreaterThan(backdrop.r),
                label + " background must retain the cool visual role.");
            Assert.That(
                backdrop.a,
                Is.EqualTo(1f).Within(GeometryTolerance),
                label + " contrast requires an opaque compositing backdrop.");
            Assert.That(
                overlayColor.a,
                Is.InRange(0f, 1f),
                label + " overlay alpha must be a valid compositing value.");
            Assert.That(
                expectWarmOverlay
                    ? overlayColor.r > overlayColor.b
                    : overlayColor.b > overlayColor.r,
                Is.True,
                label + " overlay does not retain its expected warm/cool role.");

            var compositedBackdrop = Composite(overlayColor, backdrop);
            var compositedForeground = Composite(
                foreground,
                compositedBackdrop);
            var contrast = ContrastRatio(
                compositedForeground,
                compositedBackdrop);
            Assert.That(
                contrast,
                Is.GreaterThanOrEqualTo(RequiredAuthoredFooterContrast),
                $"{label} text composites to contrast {contrast:F3}:1; " +
                "small semantic footer copy requires at least 7:1 authored " +
                "contrast so glyph rasterization still clears the 4.5:1 " +
                "rendered-device floor on both background halves.");
        }

        private static Color Composite(Color foreground, Color background)
        {
            return new Color(
                foreground.r * foreground.a +
                background.r * (1f - foreground.a),
                foreground.g * foreground.a +
                background.g * (1f - foreground.a),
                foreground.b * foreground.a +
                background.b * (1f - foreground.a),
                1f);
        }

        private static float ContrastRatio(Color first, Color second)
        {
            var firstLuminance = RelativeLuminance(first);
            var secondLuminance = RelativeLuminance(second);
            var lighter = Mathf.Max(firstLuminance, secondLuminance);
            var darker = Mathf.Min(firstLuminance, secondLuminance);
            return (lighter + 0.05f) / (darker + 0.05f);
        }

        private static float RelativeLuminance(Color color)
        {
            return 0.2126f * LinearizeSrgb(color.r) +
                   0.7152f * LinearizeSrgb(color.g) +
                   0.0722f * LinearizeSrgb(color.b);
        }

        private static float LinearizeSrgb(float channel)
        {
            return channel <= 0.04045f
                ? channel / 12.92f
                : Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);
        }

        private static void AssertCustomAndroidMainManifest()
        {
            var customMainManifest = FindPlayerSettingsProperty(
                "useCustomMainManifest");
            Assert.That(customMainManifest, Is.Not.Null);
            Assert.That(
                customMainManifest.boolValue,
                Is.True,
                "Task 5's supported EmojiCompat removal requires Unity's " +
                "custom main manifest flag.");
            Assert.That(
                AssetDatabase.AssetPathExists(CustomMainManifestPath),
                Is.True,
                CustomMainManifestPath);

            var document = new XmlDocument();
            document.Load(AssetFileSystemPath(CustomMainManifestPath));
            var namespaces = new XmlNamespaceManager(document.NameTable);
            namespaces.AddNamespace(
                "android",
                "http://schemas.android.com/apk/res/android");
            namespaces.AddNamespace(
                "tools",
                "http://schemas.android.com/tools");

            var manifest = document.DocumentElement;
            Assert.That(manifest, Is.Not.Null);
            Assert.That(manifest.LocalName, Is.EqualTo("manifest"));
            var application = RequireXmlElement(
                manifest,
                "application",
                namespaces,
                "application");
            AssertAndroidAttribute(application, "appCategory", "game");
            AssertAndroidAttribute(
                application,
                "enableOnBackInvokedCallback",
                "true");
            AssertAndroidAttribute(application, "extractNativeLibs", "true");
            AssertAndroidMetadata(
                application,
                "unity.splash-mode",
                "0",
                namespaces);
            AssertAndroidMetadata(
                application,
                "unity.splash-enable",
                "True",
                namespaces);
            AssertAndroidMetadata(
                application,
                "unity.launch-fullscreen",
                "True",
                namespaces);
            AssertAndroidMetadata(
                application,
                "unity.render-outside-safearea",
                "True",
                namespaces);
            AssertAndroidMetadata(
                application,
                "notch.config",
                "portrait|landscape",
                namespaces);
            AssertAndroidMetadata(
                application,
                "unity.auto-report-fully-drawn",
                "true",
                namespaces);
            AssertAndroidMetadata(
                application,
                "unity.strip-engine-code",
                "true",
                namespaces);
            AssertAndroidMetadata(
                application,
                "unity.run-without-focus",
                "false",
                namespaces);
            AssertAndroidMetadata(
                application,
                "unity.auto-set-game-state",
                "true",
                namespaces);
            var gameActivity = RequireXmlElement(
                application,
                "activity[@android:name='com.unity3d.player." +
                "UnityPlayerGameActivity']",
                namespaces,
                "UnityPlayerGameActivity");
            AssertAndroidAttribute(
                gameActivity,
                "theme",
                "@style/BaseUnityGameActivityTheme");
            AssertAndroidAttribute(
                gameActivity,
                "configChanges",
                RequiredGameActivityConfigChanges);
            AssertAndroidAttribute(gameActivity, "enabled", "true");
            AssertAndroidAttribute(gameActivity, "exported", "true");
            AssertAndroidAttribute(
                gameActivity,
                "hardwareAccelerated",
                "false");
            AssertAndroidAttribute(gameActivity, "launchMode", "singleTask");
            AssertAndroidAttribute(
                gameActivity,
                "resizeableActivity",
                "true");
            AssertAndroidAttribute(
                gameActivity,
                "screenOrientation",
                "fullUser");
            Assert.That(
                application.SelectNodes("activity", namespaces)?.Count,
                Is.EqualTo(1),
                "The custom main manifest must retain only Unity's configured " +
                "GameActivity template entry.");

            var intentFilter = RequireXmlElement(
                gameActivity,
                "intent-filter",
                namespaces,
                "GameActivity intent filter");
            RequireXmlElement(
                intentFilter,
                "action[@android:name='android.intent.action.MAIN']",
                namespaces,
                "MAIN action");
            RequireXmlElement(
                intentFilter,
                "category[@android:name='android.intent.category.LAUNCHER']",
                namespaces,
                "LAUNCHER category");
            var unityActivityMetadata = RequireXmlElement(
                gameActivity,
                "meta-data[@android:name='unityplayer.UnityActivity']",
                namespaces,
                "UnityActivity metadata");
            AssertAndroidAttribute(unityActivityMetadata, "value", "true");
            var gameLibraryMetadata = RequireXmlElement(
                gameActivity,
                "meta-data[@android:name='android.app.lib_name']",
                namespaces,
                "GameActivity native-library metadata");
            AssertAndroidAttribute(gameLibraryMetadata, "value", "game");
            AssertAndroidMetadata(
                gameActivity,
                "WindowManagerPreference:FreeformWindowSize",
                "@string/FreeformWindowSize_maximize",
                namespaces);
            AssertAndroidMetadata(
                gameActivity,
                "WindowManagerPreference:FreeformWindowOrientation",
                "@string/FreeformWindowOrientation_landscape",
                namespaces);
            AssertAndroidMetadata(
                gameActivity,
                "notch_support",
                "true",
                namespaces);
            var activityLayout = RequireXmlElement(
                gameActivity,
                "layout",
                namespaces,
                "GameActivity freeform layout");
            AssertAndroidAttribute(activityLayout, "minHeight", "300px");
            AssertAndroidAttribute(activityLayout, "minWidth", "400px");

            var provider = RequireXmlElement(
                application,
                "provider[@android:name='androidx.startup." +
                "InitializationProvider']",
                namespaces,
                "InitializationProvider");
            AssertAndroidAttribute(
                provider,
                "authorities",
                "${applicationId}.androidx-startup");
            AssertAndroidAttribute(provider, "exported", "false");
            Assert.That(
                provider.GetAttribute(
                    "node",
                    "http://schemas.android.com/tools"),
                Is.EqualTo("merge"));

            var emojiRemoval = RequireXmlElement(
                provider,
                "meta-data[@android:name='androidx.emoji2.text." +
                "EmojiCompatInitializer']",
                namespaces,
                "EmojiCompatInitializer removal marker");
            Assert.That(
                emojiRemoval.GetAttribute(
                    "node",
                    "http://schemas.android.com/tools"),
                Is.EqualTo("remove"));
            Assert.That(
                emojiRemoval.HasAttribute(
                    "value",
                    "http://schemas.android.com/apk/res/android"),
                Is.False,
                "The removal marker must not reintroduce an initializer value.");

            var processLifecycle = RequireXmlElement(
                provider,
                "meta-data[@android:name='androidx.lifecycle." +
                "ProcessLifecycleInitializer']",
                namespaces,
                "ProcessLifecycleInitializer");
            AssertAndroidAttribute(
                processLifecycle,
                "value",
                "androidx.startup");
            Assert.That(
                processLifecycle.GetAttribute(
                    "node",
                    "http://schemas.android.com/tools"),
                Is.EqualTo("merge"));

            var removalNodes = document.SelectNodes(
                "//*[@tools:node='remove']",
                namespaces);
            Assert.That(
                removalNodes?.Count,
                Is.EqualTo(1),
                "The custom main manifest may remove only " +
                "EmojiCompatInitializer.");
            Assert.That(
                provider.SelectNodes("meta-data", namespaces)?.Count,
                Is.EqualTo(2),
                "InitializationProvider must contain only the narrow Emoji " +
                "removal and preserved process-lifecycle metadata.");
        }

        private static XmlElement RequireXmlElement(
            XmlNode owner,
            string xpath,
            XmlNamespaceManager namespaces,
            string label)
        {
            var node = owner.SelectSingleNode(xpath, namespaces) as XmlElement;
            Assert.That(node, Is.Not.Null, label);
            return node;
        }

        private static void AssertAndroidAttribute(
            XmlElement element,
            string localName,
            string expected)
        {
            Assert.That(
                element.GetAttribute(
                    localName,
                    "http://schemas.android.com/apk/res/android"),
                Is.EqualTo(expected),
                element.Name + "/android:" + localName);
        }

        private static void AssertAndroidMetadata(
            XmlNode owner,
            string name,
            string expectedValue,
            XmlNamespaceManager namespaces)
        {
            var metadata = RequireXmlElement(
                owner,
                $"meta-data[@android:name='{name}']",
                namespaces,
                name + " metadata");
            AssertAndroidAttribute(metadata, "value", expectedValue);
        }

        private static Component ComponentByFullName(
            GameObject gameObject,
            string fullName)
        {
            return gameObject
                .GetComponents<Component>()
                .SingleOrDefault(component =>
                    component != null && component.GetType().FullName == fullName);
        }

        private static object Property(object owner, string propertyName)
        {
            Assert.That(owner, Is.Not.Null, propertyName);
            var property = owner.GetType().GetProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            return property.GetValue(owner);
        }

        private static IReadOnlyList<Component> ComponentsByFullName(
            GameObject root,
            string fullName)
        {
            return root
                .GetComponentsInChildren<Component>(includeInactive: true)
                .Where(component =>
                    component != null &&
                    component.GetType().FullName == fullName)
                .ToArray();
        }

        private static float CalculateCanvasScale(
            Component scaler,
            MobileProfile profile)
        {
            Assert.That(scaler, Is.Not.Null);
            Assert.That(
                Property(scaler, "uiScaleMode").ToString(),
                Is.EqualTo("ScaleWithScreenSize"));
            Assert.That(
                Property(scaler, "screenMatchMode").ToString(),
                Is.EqualTo("MatchWidthOrHeight"));
            var referenceResolution = (Vector2)Property(
                scaler,
                "referenceResolution");
            var match = (float)Property(scaler, "matchWidthOrHeight");
            var widthScale = profile.Width / referenceResolution.x;
            var heightScale = profile.Height / referenceResolution.y;
            return Mathf.Pow(
                2f,
                Mathf.Lerp(
                    Mathf.Log(widthScale, 2f),
                    Mathf.Log(heightScale, 2f),
                    match));
        }

        private static Rect LogicalCanvasRect(
            MobileProfile profile,
            float scale)
        {
            Assert.That(scale, Is.GreaterThan(0f));
            return new Rect(
                0f,
                0f,
                profile.Width / scale,
                profile.Height / scale);
        }

        private static Rect ResolveSyntheticRect(
            RectTransform target,
            RectTransform canvas,
            Rect logicalCanvas)
        {
            Assert.That(target, Is.Not.Null);
            Assert.That(canvas, Is.Not.Null);
            if (target == canvas)
            {
                return logicalCanvas;
            }

            var parent = target.parent as RectTransform;
            Assert.That(
                parent,
                Is.Not.Null,
                $"{target.name} is not under the Frontend Canvas.");
            var parentRect = ResolveSyntheticRect(
                parent,
                canvas,
                logicalCanvas);
            Assert.That(
                target.localScale.x,
                Is.EqualTo(1f).Within(GeometryTolerance),
                $"{target.name} uses unsupported synthetic X scale.");
            Assert.That(
                target.localScale.y,
                Is.EqualTo(1f).Within(GeometryTolerance),
                $"{target.name} uses unsupported synthetic Y scale.");
            Assert.That(
                Mathf.DeltaAngle(0f, target.localEulerAngles.z),
                Is.EqualTo(0f).Within(GeometryTolerance),
                $"{target.name} uses unsupported synthetic rotation.");

            var anchorSpan = target.anchorMax - target.anchorMin;
            var size = Vector2.Scale(parentRect.size, anchorSpan) +
                       target.sizeDelta;
            var anchorReference = parentRect.min + Vector2.Scale(
                parentRect.size,
                target.anchorMin + Vector2.Scale(anchorSpan, target.pivot));
            var pivotPosition = anchorReference + target.anchoredPosition;
            return new Rect(
                pivotPosition - Vector2.Scale(size, target.pivot),
                size);
        }

        private static void AssertPhysicalAxisAtLeastDp(
            float logicalUnits,
            float canvasScale,
            MobileProfile profile,
            string objectName,
            string axis)
        {
            var physicalPixels = logicalUnits * canvasScale;
            var dp = physicalPixels * AndroidDensityBaseline / profile.Dpi;
            Assert.That(
                dp,
                Is.GreaterThanOrEqualTo(RequiredTouchTargetDp),
                $"{profile.Name}/{objectName} {axis} is {logicalUnits:F3} " +
                $"logical units, {physicalPixels:F3}px and {dp:F3}dp at " +
                $"{profile.Width:F0}x{profile.Height:F0}@{profile.Dpi:F0}dpi; " +
                "the mobile target floor is 48dp.");
        }

        private static float AuthoredTypographyFloor(Component textComponent)
        {
            var autoSizing = (bool)Property(
                textComponent,
                "enableAutoSizing");
            return autoSizing
                ? (float)Property(textComponent, "fontSizeMin")
                : (float)Property(textComponent, "fontSize");
        }

        private static Vector2 MeasureCompleteTextWithoutEllipsis(
            Component textComponent,
            string fullText,
            float availableWidth,
            out bool wraps)
        {
            var type = textComponent.GetType();
            var overflowProperty = type.GetProperty("overflowMode");
            var autoSizingProperty = type.GetProperty("enableAutoSizing");
            var fontSizeProperty = type.GetProperty("fontSize");
            var wrappingProperty = type.GetProperty("textWrappingMode");
            var textProperty = type.GetProperty("text");
            var preferredValues = type.GetMethod(
                "GetPreferredValues",
                new[] { typeof(string), typeof(float), typeof(float) });
            Assert.That(overflowProperty, Is.Not.Null);
            Assert.That(autoSizingProperty, Is.Not.Null);
            Assert.That(fontSizeProperty, Is.Not.Null);
            Assert.That(wrappingProperty, Is.Not.Null);
            Assert.That(textProperty, Is.Not.Null);
            Assert.That(preferredValues, Is.Not.Null);

            var originalOverflow = overflowProperty.GetValue(textComponent);
            var originalAutoSizing = (bool)autoSizingProperty.GetValue(
                textComponent);
            var originalFontSize = (float)fontSizeProperty.GetValue(
                textComponent);
            var originalWrapping = wrappingProperty.GetValue(textComponent);
            var originalText = (string)textProperty.GetValue(textComponent);
            wraps = !IsNoWrapMode(originalWrapping);
            try
            {
                overflowProperty.SetValue(
                    textComponent,
                    Enum.Parse(overflowProperty.PropertyType, "Overflow"));
                autoSizingProperty.SetValue(textComponent, false);
                fontSizeProperty.SetValue(
                    textComponent,
                    originalAutoSizing
                        ? (float)Property(textComponent, "fontSizeMin")
                        : originalFontSize);
                return (Vector2)preferredValues.Invoke(
                    textComponent,
                    new object[]
                    {
                        fullText,
                        availableWidth,
                        float.PositiveInfinity,
                    });
            }
            finally
            {
                textProperty.SetValue(textComponent, originalText);
                fontSizeProperty.SetValue(textComponent, originalFontSize);
                autoSizingProperty.SetValue(
                    textComponent,
                    originalAutoSizing);
                overflowProperty.SetValue(textComponent, originalOverflow);
                Assert.That(
                    wrappingProperty.GetValue(textComponent),
                    Is.EqualTo(originalWrapping),
                    $"{textComponent.name} wrapping mode changed during " +
                    "the in-memory fit probe.");
                Assert.That(
                    textProperty.GetValue(textComponent),
                    Is.EqualTo(originalText),
                    $"{textComponent.name} text changed during the " +
                    "in-memory fit probe.");
            }
        }

        private static void AssertCompleteTextFitsInProfile(
            Component textComponent,
            string fullText,
            MobileProfile profile,
            Component scaler,
            RectTransform canvas)
        {
            var scale = CalculateCanvasScale(scaler, profile);
            var logicalCanvas = LogicalCanvasRect(profile, scale);
            var available = ResolveSyntheticRect(
                textComponent.GetComponent<RectTransform>(),
                canvas,
                logicalCanvas);
            var preferred = MeasureCompleteTextWithoutEllipsis(
                textComponent,
                fullText,
                available.width,
                out var wraps);

            if (wraps)
            {
                Assert.That(
                    preferred.y,
                    Is.LessThanOrEqualTo(
                        available.height + GeometryTolerance),
                    $"{profile.Name}/{textComponent.name} needs " +
                    $"{preferred.y:F3} logical height for exact copy " +
                    $"'{fullText}' but has {available.height:F3}.");
                return;
            }

            Assert.That(
                preferred.x,
                Is.LessThanOrEqualTo(
                    available.width + GeometryTolerance),
                $"{profile.Name}/{textComponent.name} needs " +
                $"{preferred.x:F3} logical width for exact copy " +
                $"'{fullText}' but has {available.width:F3}.");
            Assert.That(
                preferred.y,
                Is.LessThanOrEqualTo(
                    available.height + GeometryTolerance),
                $"{profile.Name}/{textComponent.name} needs " +
                $"{preferred.y:F3} logical height for exact copy " +
                $"'{fullText}' but has {available.height:F3}.");
        }

        private static bool IsNoWrapMode(object wrappingMode)
        {
            Assert.That(wrappingMode, Is.Not.Null);
            var name = wrappingMode.ToString();
            return name == "NoWrap" || name == "PreserveWhitespaceNoWrap";
        }

        private static Rect ResolveNamedRect(
            GameObject root,
            string name,
            RectTransform canvas,
            Rect logicalCanvas)
        {
            return ResolveSyntheticRect(
                FindDescendant(root.transform, name)
                    .GetComponent<RectTransform>(),
                canvas,
                logicalCanvas);
        }

        private static void AssertRectContained(
            Rect container,
            Rect child,
            string label)
        {
            Assert.That(
                child.width,
                Is.GreaterThanOrEqualTo(0f),
                $"{label} has a negative width.");
            Assert.That(
                child.height,
                Is.GreaterThanOrEqualTo(0f),
                $"{label} has a negative height.");
            Assert.That(
                child.xMin,
                Is.GreaterThanOrEqualTo(
                    container.xMin - GeometryTolerance),
                $"{label} escapes the left bound.");
            Assert.That(
                child.yMin,
                Is.GreaterThanOrEqualTo(
                    container.yMin - GeometryTolerance),
                $"{label} escapes the bottom bound.");
            Assert.That(
                child.xMax,
                Is.LessThanOrEqualTo(
                    container.xMax + GeometryTolerance),
                $"{label} escapes the right bound.");
            Assert.That(
                child.yMax,
                Is.LessThanOrEqualTo(
                    container.yMax + GeometryTolerance),
                $"{label} escapes the top bound.");
        }

        private static int BindingCount(object action)
        {
            var bindings = Property(action, "bindings");
            return (int)Property(bindings, "Count");
        }

        private static UnityEngine.Object AssertStaticFrontendFont()
        {
            var fontAsset = AssetDatabase.LoadMainAssetAtPath(
                FrontendFontAssetPath);
            Assert.That(fontAsset, Is.Not.Null);
            Assert.That(
                Property(fontAsset, "atlasPopulationMode").ToString(),
                Is.EqualTo("Static"));
            Assert.That(
                (int)Property(Property(fontAsset, "glyphTable"), "Count"),
                Is.GreaterThan(0));
            Assert.That(
                (int)Property(Property(fontAsset, "characterTable"), "Count"),
                Is.GreaterThan(0));
            var material = Property(fontAsset, "material") as Material;
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader, Is.Not.Null);
            var atlasTextures = Property(fontAsset, "atlasTextures") as Array;
            Assert.That(atlasTextures, Is.Not.Null);
            Assert.That(atlasTextures.Length, Is.GreaterThan(0));
            foreach (var atlasTexture in atlasTextures)
            {
                Assert.That(atlasTexture, Is.Not.Null);
                var texture = atlasTexture as Texture;
                Assert.That(texture, Is.Not.Null);
                Assert.That(texture.width, Is.GreaterThan(1));
                Assert.That(texture.height, Is.GreaterThan(1));
            }

            var hasCharacter = fontAsset.GetType().GetMethod(
                "HasCharacter",
                new[] { typeof(int) });
            Assert.That(hasCharacter, Is.Not.Null);
            foreach (var character in RequiredLaunchCopy.Distinct())
            {
                Assert.That(
                    (bool)hasCharacter.Invoke(
                        fontAsset,
                        new object[] { (int)character }),
                    Is.True,
                    $"Generated static font is missing '{character}'.");
            }

            var serializedFont = new SerializedObject(fontAsset);
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(
                LiberationSourceFontPath);
            Assert.That(sourceFont, Is.Not.Null);
            var sourceGuid = AssetDatabase.AssetPathToGUID(
                LiberationSourceFontPath);
            Assert.That(
                serializedFont.FindProperty("m_SourceFontFilePath")
                    ?.stringValue,
                Is.Empty,
                "The canonical Static asset uses its project TTF reference, " +
                "not a host-only file path.");
            Assert.That(
                serializedFont.FindProperty("m_SourceFontFileGUID")
                    ?.stringValue,
                Is.EqualTo(sourceGuid));
            Assert.That(
                serializedFont.FindProperty("m_SourceFontFile")
                    ?.objectReferenceValue,
                Is.Null,
                "Static runtime font data must not require the source TTF.");
            var editorReference = fontAsset.GetType().GetProperty(
                "SourceFont_EditorRef",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(editorReference, Is.Not.Null);
            Assert.That(
                editorReference.GetValue(fontAsset),
                Is.SameAs(sourceFont));

            var creationSettings = serializedFont.FindProperty(
                "m_CreationSettings");
            Assert.That(creationSettings, Is.Not.Null);
            Assert.That(
                creationSettings.FindPropertyRelative("sourceFontFileGUID")
                    ?.stringValue,
                Is.EqualTo(sourceGuid));
            Assert.That(
                creationSettings.FindPropertyRelative(
                        "referencedFontAssetGUID")
                    ?.stringValue,
                Is.EqualTo(AssetDatabase.AssetPathToGUID(
                    FrontendFontAssetPath)));
            return fontAsset;
        }

        private static void AssertFontSupportsText(
            UnityEngine.Object fontAsset,
            string text)
        {
            var hasCharacter = fontAsset.GetType().GetMethod(
                "HasCharacter",
                new[] { typeof(int) });
            Assert.That(hasCharacter, Is.Not.Null);
            foreach (var character in text
                         .Where(character => !char.IsControl(character))
                         .Distinct())
            {
                Assert.That(
                    (bool)hasCharacter.Invoke(
                        fontAsset,
                        new object[] { (int)character }),
                    Is.True,
                    $"Static frontend font is missing license character " +
                    $"'{character}' (U+{(int)character:X4}).");
            }
        }

        private static void AssertFileSha256(
            string assetPath,
            string expected)
        {
            var fileSystemPath = AssetFileSystemPath(assetPath);
            Assert.That(File.Exists(fileSystemPath), Is.True, assetPath);
            using var stream = File.OpenRead(fileSystemPath);
            using var sha256 = SHA256.Create();
            var actual = string.Concat(
                sha256.ComputeHash(stream).Select(value =>
                    value.ToString("x2")));
            Assert.That(actual, Is.EqualTo(expected), assetPath);
        }

        private static string AssetFileSystemPath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath);
            Assert.That(projectRoot, Is.Not.Null);
            return Path.Combine(
                projectRoot.FullName,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static SerializedProperty FindPlayerSettingsProperty(string name)
        {
            var buildProfileType = typeof(BuildProfile);
            var globalSettingsField = buildProfileType.GetField(
                "s_GlobalPlayerSettings",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(globalSettingsField, Is.Not.Null);
            var playerSettings = globalSettingsField.GetValue(null) as PlayerSettings;

            var activeProfile = BuildProfile.GetActiveBuildProfile();
            if (activeProfile != null)
            {
                var overrideField = buildProfileType.GetField(
                    "m_PlayerSettings",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(overrideField, Is.Not.Null);
                var profileSettings = overrideField.GetValue(activeProfile) as
                    PlayerSettings;
                if (profileSettings != null)
                {
                    playerSettings = profileSettings;
                }
            }

            Assert.That(playerSettings, Is.Not.Null);
            return new SerializedObject(playerSettings).FindProperty(name);
        }

        private static IReadOnlyList<string> TextValues(
            GameObject root,
            bool includeInactive)
        {
            return TextComponents(root, includeInactive)
                .Select(component => (string)Property(component, "text"))
                .ToArray();
        }

        private static IReadOnlyList<Component> TextComponents(
            GameObject root,
            bool includeInactive)
        {
            return root
                .GetComponentsInChildren<Component>(includeInactive)
                .Where(component =>
                    component != null &&
                    component.GetType().FullName == "TMPro.TextMeshProUGUI")
                .ToArray();
        }

        private readonly struct MobileProfile
        {
            public MobileProfile(
                string name,
                float width,
                float height,
                float dpi,
                float fontScale)
            {
                Name = name;
                Width = width;
                Height = height;
                Dpi = dpi;
                FontScale = fontScale;
            }

            public string Name { get; }

            public float Width { get; }

            public float Height { get; }

            public float Dpi { get; }

            public float FontScale { get; }
        }
    }
}
