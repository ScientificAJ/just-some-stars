using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Atlas;
using JustSomeStars.Runtime.Cosmetics;
using JustSomeStars.Runtime.Flight;
using JustSomeStars.Runtime.Dialogue;
using JustSomeStars.Runtime.Cinematics;
using JustSomeStars.Runtime.Player;
using JustSomeStars.Runtime.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace JustSomeStars.Editor.UI
{
    public static class Task28UiBuilder
    {
        private const string FrontendPrefab =
            "Assets/_JustSomeStars/Prefabs/UI/FrontendVisualRoot.prefab";
        private const string FrontendScene =
            "Assets/_JustSomeStars/Scenes/Core/Frontend.unity";
        private const string LocalizationRoot =
            "Assets/_JustSomeStars/Content/Localization/English";
        private const string EnglishAsset = LocalizationRoot +
            "/Task28English.asset";
        private const string TokensAsset = LocalizationRoot +
            "/HomemadeSignalUiTokens.asset";
        private const string AccessibilityRoot =
            "Assets/_JustSomeStars/Content/Accessibility";
        private const string MotionBlurProfileAsset = AccessibilityRoot +
            "/Task28MotionBlur.asset";
        private const string CosmeticCatalogAsset =
            "Assets/_JustSomeStars/Content/Cosmetics/CosmeticCatalog.asset";
        private const string NotoFont = LocalizationRoot +
            "/NotoSans-Regular.ttf";
        private const string NotoTmpFont = LocalizationRoot +
            "/NotoSansReadable SDF.asset";
        private const string SystemNotoFont =
            "/usr/share/fonts/truetype/noto/NotoSans-Regular.ttf";
        private const string StandardFont =
            "Assets/TextMesh Pro/Resources/Fonts & Materials/" +
            "LiberationSans SDF.asset";
        private const string PrimaryPlate =
            "Assets/_JustSomeStars/Art/UI/FrontendRedesign/Textures/" +
            "PrimaryPlate.png";
        private const string SecondaryPlate =
            "Assets/_JustSomeStars/Art/UI/FrontendRedesign/Textures/" +
            "SecondaryPlateSettings.png";
        private const string FrameSprite =
            "Assets/_JustSomeStars/Art/UI/FrontendRedesign/Textures/" +
            "ModalFrame.png";
        private static readonly Vector2 FrontendReference =
            new Vector2(1616f, 720f);
        private static readonly Vector2 GameplayReference =
            new Vector2(1280f, 720f);

        private static readonly string[] GameplayScenes =
        {
            "Assets/_JustSomeStars/Scenes/Destinations/Mirra.unity",
            "Assets/_JustSomeStars/Scenes/Destinations/KoroVesper.unity",
            "Assets/_JustSomeStars/Scenes/Destinations/AsterVeil.unity",
            "Assets/_JustSomeStars/Scenes/Destinations/Task25VesperFlight.unity",
            "Assets/_JustSomeStars/Scenes/Benchmarks/Task17FlightGraybox.unity",
        };

        private static readonly string[] ChapterOneScenes =
        {
            "Assets/_JustSomeStars/Scenes/Cinematics/Opening.unity",
            "Assets/_JustSomeStars/Scenes/Cinematics/SignalReassembly.unity",
            "Assets/_JustSomeStars/Scenes/Core/Clubhouse.unity",
            "Assets/_JustSomeStars/Scenes/Cinematics/DinnerEnding.unity",
        };

        [MenuItem("Just Some Stars/Task 28/Build Player UI Layer")]
        public static void Apply()
        {
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                EnsureFolder(LocalizationRoot);
                EnsureFolder(AccessibilityRoot);
                var english = EnsureEnglishCatalog();
                var tokens = EnsureTokens();
                var motionBlur = EnsureMotionBlurProfile();
                var standard = Require<TMP_FontAsset>(StandardFont);
                var readable = EnsureReadableFont();
                PatchFrontend(english, tokens, standard, readable);
                foreach (var path in GameplayScenes)
                {
                    PatchGameplayScene(
                        path,
                        english,
                        tokens,
                        standard,
                        readable,
                        motionBlur);
                }
                foreach (var path in ChapterOneScenes)
                {
                    PatchChapterOneScene(
                        path,
                        english,
                        tokens,
                        standard,
                        readable,
                        motionBlur);
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                ValidatePersisted(english, tokens);
                Debug.Log(
                    "[JSS Task 28] Localized responsive player UI layer " +
                    "materialized and statically validated.");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static LocalizedEnglishCatalog EnsureEnglishCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LocalizedEnglishCatalog>(
                EnglishAsset);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<LocalizedEnglishCatalog>();
                AssetDatabase.CreateAsset(catalog, EnglishAsset);
            }
            catalog.Configure(Task28English.CreateEntries());
            catalog.ValidateOrThrow();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static HomemadeSignalUiTokens EnsureTokens()
        {
            var tokens = AssetDatabase.LoadAssetAtPath<HomemadeSignalUiTokens>(
                TokensAsset);
            if (tokens == null)
            {
                tokens = ScriptableObject.CreateInstance<HomemadeSignalUiTokens>();
                AssetDatabase.CreateAsset(tokens, TokensAsset);
            }
            tokens.ValidateOrThrow();
            EditorUtility.SetDirty(tokens);
            return tokens;
        }

        private static VolumeProfile EnsureMotionBlurProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(
                MotionBlurProfileAsset);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, MotionBlurProfileAsset);
            }
            if (!profile.TryGet<MotionBlur>(out var motionBlur))
            {
                motionBlur = profile.Add<MotionBlur>(overrides: true);
            }
            motionBlur.active = true;
            motionBlur.intensity.Override(0.12f);
            motionBlur.clamp.Override(0.035f);
            EditorUtility.SetDirty(motionBlur);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static TMP_FontAsset EnsureReadableFont()
        {
            if (!File.Exists(SystemNotoFont))
            {
                throw new FileNotFoundException(
                    "The pinned Noto Sans source font is unavailable.",
                    SystemNotoFont);
            }
            if (!File.Exists(NotoFont))
            {
                File.Copy(SystemNotoFont, NotoFont, overwrite: false);
                AssetDatabase.ImportAsset(
                    NotoFont,
                    ImportAssetOptions.ForceSynchronousImport);
            }

            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NotoTmpFont);
            if (fontAsset != null && fontAsset.material != null &&
                fontAsset.atlasTextures != null &&
                fontAsset.atlasTextures.Length > 0 &&
                fontAsset.atlasTextures[0] != null)
            {
                return fontAsset;
            }
            if (fontAsset != null && !AssetDatabase.DeleteAsset(NotoTmpFont))
            {
                throw new InvalidOperationException(
                    "Task 28 could not replace its incomplete readable font asset.");
            }
            var source = Require<Font>(NotoFont);
            fontAsset = TMP_FontAsset.CreateFontAsset(
                source,
                90,
                9,
                GlyphRenderMode.SDFAA,
                1024,
                1024,
                AtlasPopulationMode.Dynamic,
                true);
            if (fontAsset == null)
            {
                throw new InvalidOperationException(
                    "TMP could not create the Noto Sans readable font asset.");
            }
            fontAsset.name = "NotoSansReadable SDF";
            var material = fontAsset.material;
            var atlasTextures = fontAsset.atlasTextures?
                .Where(texture => texture != null)
                .Distinct()
                .ToArray() ?? Array.Empty<Texture2D>();
            if (material == null || atlasTextures.Length == 0)
            {
                throw new InvalidOperationException(
                    "TMP created an incomplete Noto Sans font asset.");
            }
            AssetDatabase.CreateAsset(fontAsset, NotoTmpFont);
            material.hideFlags = HideFlags.None;
            material.name = "NotoSansReadable Atlas Material";
            AssetDatabase.AddObjectToAsset(material, NotoTmpFont);
            foreach (var texture in atlasTextures)
            {
                texture.hideFlags = HideFlags.None;
                texture.name = "NotoSansReadable Atlas";
                AssetDatabase.AddObjectToAsset(texture, NotoTmpFont);
            }
            AssetDatabase.ImportAsset(
                NotoTmpFont,
                ImportAssetOptions.ForceSynchronousImport);
            fontAsset = Require<TMP_FontAsset>(NotoTmpFont);
            if (fontAsset.material == null || fontAsset.atlasTexture == null)
            {
                throw new InvalidOperationException(
                    "Noto Sans font sub-assets were not persisted.");
            }
            return fontAsset;
        }

        private static void PatchFrontend(
            LocalizedEnglishCatalog english,
            HomemadeSignalUiTokens tokens,
            TMP_FontAsset standard,
            TMP_FontAsset readable)
        {
            var root = PrefabUtility.LoadPrefabContents(FrontendPrefab);
            try
            {
                var safeArea = RequireTransform(root.transform, "SafeArea");
                var menu = RequireTransform(safeArea, "MenuGroup");
                RequireText(root.transform, "TitleSemantic").text =
                    english.Resolve(Task28English.FrontendTitle);
                RequireText(root.transform, "StatusLabel").text =
                    english.Resolve(Task28English.FrontendStatus);
                RequireText(root.transform, "VersionLabel").text =
                    Task28English.Format(english, Task28English.FrontendVersion, "1.0");
                var continueButton = RequireTransform(menu, "ContinueButton")
                    .GetComponent<Button>();
                SetReferenceRect(
                    continueButton.GetComponent<RectTransform>(),
                    new Rect(72f, 466f, 536f, 96f),
                    FrontendReference);
                SetPlateToParent(continueButton.transform);

                var newGame = menu.Find("NewGameButton")?.GetComponent<Button>();
                if (newGame == null)
                {
                    var clone = UnityEngine.Object.Instantiate(
                        continueButton.gameObject,
                        menu,
                        false);
                    clone.name = "NewGameButton";
                    newGame = clone.GetComponent<Button>();
                    RenameDescendant(clone.transform, "ContinueButtonLabel",
                        "NewGameButtonLabel");
                    RenameDescendant(clone.transform, "ContinueState",
                        "NewGameState");
                }
                SetReferenceRect(
                    newGame.GetComponent<RectTransform>(),
                    new Rect(72f, 335f, 536f, 96f),
                    FrontendReference);
                SetPlateToParent(newGame.transform);
                newGame.interactable = true;
                RequireText(newGame.transform, "NewGameButtonLabel").text =
                    english.Resolve(Task28English.FrontendNewGame);
                RequireText(newGame.transform, "NewGameState").text =
                    english.Resolve("frontend.state.new");

                var continueExplanation = RequireTransform(
                    menu,
                    "ContinueExplanation").GetComponent<TMP_Text>();
                SetReferenceRect(
                    continueExplanation.rectTransform,
                    new Rect(116f, 566f, 450f, 28f),
                    FrontendReference);
                continueExplanation.fontSize = 16f;
                continueExplanation.textWrappingMode = TextWrappingModes.Normal;
                continueExplanation.text = english.Resolve(
                    Task28English.FrontendContinueNoSave);
                RequireText(continueButton.transform, "ContinueButtonLabel").text =
                    english.Resolve(Task28English.FrontendContinue);
                RequireText(continueButton.transform, "ContinueState").text =
                    english.Resolve("frontend.state.offline");

                var newExplanation = menu.Find("NewGameExplanation")?
                    .GetComponent<TMP_Text>();
                if (newExplanation == null)
                {
                    var clone = UnityEngine.Object.Instantiate(
                        continueExplanation.gameObject,
                        menu,
                        false);
                    clone.name = "NewGameExplanation";
                    newExplanation = clone.GetComponent<TMP_Text>();
                }
                SetReferenceRect(
                    newExplanation.rectTransform,
                    new Rect(116f, 435f, 450f, 28f),
                    FrontendReference);
                newExplanation.text = english.Resolve(
                    Task28English.FrontendNewGameReady);

                RequireText(root.transform, "SettingsButtonLabel").text =
                    english.Resolve(Task28English.SettingsTitle);
                RequireText(root.transform, "CreditsButtonLabel").text =
                    english.Resolve(Task28English.CreditsButton);
                RequireText(root.transform, "PrivacyButtonLabel").text =
                    english.Resolve(Task28English.PrivacyTitle);
                RequireText(root.transform, "LocalPanelLabel").text =
                    english.Resolve(Task28English.LocalPanelNote);
                RequireText(root.transform, "CloseButtonLabel").text =
                    english.Resolve(Task28English.Close);

                foreach (var name in new[]
                         {
                             "SettingsButton",
                             "CreditsButton",
                             "PrivacyButton",
                         })
                {
                    var rect = RequireTransform(menu, name)
                        .GetComponent<RectTransform>();
                    var min = rect.anchorMin;
                    var max = rect.anchorMax;
                    var height = max.y - min.y;
                    min.y = 1f - 681f / FrontendReference.y;
                    max.y = min.y + height;
                    rect.anchorMin = min;
                    rect.anchorMax = max;
                }

                var info = menu.Find("InfoGlyph")?.GetComponent<RectTransform>();
                if (info != null)
                {
                    SetReferenceRect(
                        info,
                        new Rect(84f, 569f, 21f, 21f),
                        FrontendReference);
                }

                var accessibility = root.GetComponent<AccessibilityApplier>() ??
                    root.AddComponent<AccessibilityApplier>();
                SetObject(accessibility, "m_StandardFont", standard);
                SetObject(accessibility, "m_ReadableFont", readable);
                SetObject(accessibility, "m_ScopeRoot", root.transform);

                var view = RequireComponent<FrontendView>(root);
                SetObject(view, "m_TitleSemantic",
                    RequireText(root.transform, "TitleSemantic"));
                SetObject(view, "m_StatusLabel",
                    RequireText(root.transform, "StatusLabel"));
                SetObject(view, "m_NewGameButton", newGame);
                SetObject(view, "m_NewGameButtonLabel",
                    RequireText(newGame.transform, "NewGameButtonLabel"));
                SetObject(view, "m_NewGameExplanation", newExplanation);
                SetObject(view, "m_ContinueButtonLabel",
                    RequireText(continueButton.transform, "ContinueButtonLabel"));
                SetObject(view, "m_ContinueState",
                    RequireText(continueButton.transform, "ContinueState"));
                SetObject(view, "m_SettingsButtonLabel",
                    RequireText(root.transform, "SettingsButtonLabel"));
                SetObject(view, "m_CreditsButtonLabel",
                    RequireText(root.transform, "CreditsButtonLabel"));
                SetObject(view, "m_PrivacyButtonLabel",
                    RequireText(root.transform, "PrivacyButtonLabel"));
                SetObject(view, "m_LocalPanelLabel",
                    RequireText(root.transform, "LocalPanelLabel"));
                SetObject(view, "m_CloseButtonLabel",
                    RequireText(root.transform, "CloseButtonLabel"));

                var settings = RequireComponent<FrontendSettingsPanel>(root);
                var names = new TMP_Text[FrontendSettingsPanel.ControlCount];
                for (var index = 0; index < names.Length; index++)
                {
                    var row = RequireTransform(
                        root.transform,
                        $"SettingRow{index:00}");
                    names[index] = row.GetComponentsInChildren<TMP_Text>(true)
                        .FirstOrDefault(text => text.transform.parent == row &&
                            !text.name.StartsWith("Value", StringComparison.Ordinal) &&
                            !text.name.StartsWith("Increase", StringComparison.Ordinal) &&
                            !text.name.StartsWith("Decrease", StringComparison.Ordinal));
                    if (names[index] == null)
                    {
                        throw new InvalidOperationException(
                            $"SettingRow{index:00} has no direct name label.");
                    }
                }
                SetObjects(settings, "m_NameLabels", names);

                var panelFrame = RequireTransform(root.transform, "PanelFrame");
                var previousChallenge = panelFrame.Find(
                    "FrontendGrownUpChallenge");
                if (previousChallenge != null)
                {
                    UnityEngine.Object.DestroyImmediate(
                        previousChallenge.gameObject);
                }
                var challengeRoot = NewUi(
                    "FrontendGrownUpChallenge",
                    panelFrame,
                    typeof(Image));
                var challengeRect = challengeRoot.GetComponent<RectTransform>();
                challengeRect.anchorMin = new Vector2(0.04f, 0.08f);
                challengeRect.anchorMax = new Vector2(0.96f, 0.92f);
                challengeRect.offsetMin = Vector2.zero;
                challengeRect.offsetMax = Vector2.zero;
                var challengeImage = challengeRoot.GetComponent<Image>();
                challengeImage.sprite = Require<Sprite>(FrameSprite);
                challengeImage.type = Image.Type.Sliced;
                challengeImage.color = new Color(0.07f, 0.11f, 0.18f, 0.985f);
                challengeImage.raycastTarget = true;

                var challengeReference = new Vector2(392f, 356f);
                var challengePrompt = CreateLocalizedText(
                    "FrontendGrownUpPrompt",
                    challengeRoot.transform,
                    english,
                    "account.grownUpConfirm",
                    standard,
                    18f,
                    tokens.WarmPaper,
                    new Rect(24f, 24f, 344f, 100f),
                    challengeReference);
                challengePrompt.alignment = TextAlignmentOptions.TopLeft;
                var challengeAnswer = CreateLocalizedText(
                    "FrontendGrownUpAnswer",
                    challengeRoot.transform,
                    english,
                    Task28English.NotAvailable,
                    standard,
                    24f,
                    tokens.SignalCyan,
                    new Rect(116f, 132f, 160f, 48f),
                    challengeReference);
                var challengeActions = new (
                    string Name,
                    string Key)[]
                {
                    ("FrontendAnswerDown", "shop.answerDown"),
                    ("FrontendAnswerUp", "shop.answerUp"),
                    ("FrontendConfirmGrownUp", "shop.confirm"),
                    ("FrontendCancelGrownUp", "shop.cancel"),
                };
                var challengeButtons = new Button[challengeActions.Length];
                for (var index = 0; index < challengeActions.Length; index++)
                {
                    challengeButtons[index] = CreateButton(
                        challengeActions[index].Name,
                        challengeRoot.transform,
                        Require<Sprite>(index < 2 ? PrimaryPlate : SecondaryPlate),
                        english,
                        challengeActions[index].Key,
                        standard,
                        new Rect(8f + index * 96f, 224f, 88f, 92f),
                        challengeReference,
                        tokens);
                }
                SetObject(settings, "m_GrownUpChallengeRoot", challengeRoot);
                SetObject(settings, "m_GrownUpPrompt", challengePrompt);
                SetObject(settings, "m_GrownUpAnswerValue", challengeAnswer);
                SetObject(settings, "m_GrownUpAnswerDownButton",
                    challengeButtons[0]);
                SetObject(settings, "m_GrownUpAnswerUpButton",
                    challengeButtons[1]);
                SetObject(settings, "m_GrownUpConfirmButton",
                    challengeButtons[2]);
                SetObject(settings, "m_GrownUpCancelButton",
                    challengeButtons[3]);
                challengeRoot.SetActive(false);

                var controller = RequireComponent<FrontendController>(root);
                SetObject(controller, "m_English", english);
                SetObject(controller, "m_AccessibilityApplier", accessibility);
                SetObject(controller, "m_SettingsPanelSource", settings);

                PrefabUtility.SaveAsPrefabAsset(root, FrontendPrefab, out var success);
                if (!success)
                {
                    throw new InvalidOperationException(
                        "Task 28 could not save the Frontend prefab.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            var scene = EditorSceneManager.OpenScene(FrontendScene, OpenSceneMode.Single);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, FrontendScene))
            {
                throw new InvalidOperationException(
                    "Task 28 could not persist the Frontend scene.");
            }
        }

        private static void PatchGameplayScene(
            string path,
            LocalizedEnglishCatalog english,
            HomemadeSignalUiTokens tokens,
            TMP_FontAsset standard,
            TMP_FontAsset readable,
            VolumeProfile motionBlurProfile)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Gameplay scene is missing.", path);
            }
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var surface = FindInScene<SurfaceGameplayLifecycle2D>(scene)
                .SingleOrDefault();
            var flight = FindInScene<FlightGameplayLifecycle2D>(scene)
                .SingleOrDefault();
            var lifecycle = (Component)surface ?? flight;
            if (lifecycle == null)
            {
                throw new InvalidOperationException(
                    $"{path} has no supported gameplay lifecycle.");
            }

            var root = lifecycle.gameObject;
            var accessibility = EnsureSingleComponent<AccessibilityApplier>(root);
            SetObject(accessibility, "m_StandardFont", standard);
            SetObject(accessibility, "m_ReadableFont", readable);
            SetObject(accessibility, "m_ScopeRoot", null);

            if (surface != null)
            {
                var movement = FindInScene<RectTransform>(scene)
                    .Where(item => item.name == "TouchMove")
                    .ToArray();
                var actions = FindInScene<RectTransform>(scene)
                    .Where(item => item.name == "TouchJump" ||
                        item.name == "TouchInteract" ||
                        item.name == "TouchLens")
                    .Distinct()
                    .ToArray();
                if (movement.Length == 0 || actions.Length < 2)
                {
                    throw new InvalidOperationException(
                        $"{path} is missing responsive surface touch groups.");
                }
                var touchLayout = EnsureSingleComponent<AccessibleTouchLayout>(root);
                SetObjects(touchLayout, "movementGroup", movement);
                SetObjects(touchLayout, "actionGroup", actions);
            }

            var photo = EnsureSingleComponent<PhotoModeController>(root);
            var playerMenu = EnsureSingleComponent<PlayerMenuController>(root);
            var catalog = Require<CosmeticCatalog>(CosmeticCatalogAsset);
            SetObject(playerMenu, "m_English", english);
            SetObject(playerMenu, "m_Catalog", catalog);
            SetObjects(playerMenu, "m_AtlasEntries", LoadAtlasEntries());
            SetObject(photo, "catalog", catalog);
            var camera = surface != null
                ? surface.GetComponentInChildren<CompositionCamera2D>(true)?
                    .ControlledCamera
                : flight.CompositionCamera.ControlledCamera;
            if (camera == null)
            {
                throw new InvalidOperationException(
                    $"{path} has no 2D composition camera.");
            }
            SetObject(photo, "photoCamera", camera);
            var photoUi = BuildPhotoUi(
                scene,
                photo,
                playerMenu,
                english,
                tokens,
                standard);
            BindAccessibilityVisuals(
                scene,
                photoUi.CanvasRoot,
                tokens,
                standard,
                motionBlurProfile);
            SetObject(photo, "panelRoot", photoUi.PanelRoot);
            SetObject(photo, "explorerControlsRoot", photoUi.ExplorerRoot);
            SetObject(photo, "earnedFrameImage", photoUi.FrameImage);
            var photoItems = catalog.Category(CosmeticCategory.Photo)
                .Where(item => item.PresentationSprite != null)
                .ToArray();
            SetObjects(
                photo,
                "earnedFrames",
                photoItems.Select(item => item.PresentationSprite).ToArray());
            SetStrings(
                photo,
                "earnedFrameIds",
                photoItems.Select(item => item.Id).ToArray());
            SetObjects(
                photo,
                "poseActors",
                FindInScene<JustSomeStars.Runtime.Animation2D.LayeredCharacterRenderer>(
                    scene));

            var renderers = FindInScene<SpriteRenderer>(scene)
                .Where(renderer => renderer.gameObject.activeInHierarchy)
                .ToArray();
            SetObjects(photo, "exposureTargets", renderers);
            SetObjects(photo, "depthLayers", renderers
                .OrderBy(renderer => renderer.sortingOrder)
                .Take(Mathf.Min(6, renderers.Length))
                .ToArray());

            foreach (var probe in FindInScene<SurfaceInteractionProbe2D>(scene))
            {
                SetString(probe, "availableText", english.Resolve("hud.interact"));
                SetString(probe, "activatedText", english.Resolve("hud.signalLinked"));
            }
            foreach (var presenter in FindInScene<MirraDialoguePresenter2D>(scene))
            {
                var serialized = new SerializedObject(presenter);
                var panelObject = serialized.FindProperty("panel")?
                    .objectReferenceValue as GameObject;
                var speaker = serialized.FindProperty("speakerLabel")?
                    .objectReferenceValue as TMP_Text;
                var body = serialized.FindProperty("bodyLabel")?
                    .objectReferenceValue as TMP_Text;
                if (panelObject == null || speaker == null || body == null)
                {
                    throw new InvalidOperationException(
                        $"{path} has incomplete dialogue caption bindings.");
                }
                var caption = EnsureSingleComponent<AccessibleCaption>(panelObject);
                SetObject(caption, "root", panelObject);
                SetObject(caption, "speakerLabel", speaker);
                SetObject(caption, "bodyLabel", body);
                SetObject(presenter, "accessibleCaption", caption);
                SetObject(presenter, "accessibility", accessibility);
            }
            var cameraPosition = camera.transform.position;
            SetBounds(
                photo,
                "panBounds",
                new Bounds(
                    new Vector3(cameraPosition.x, cameraPosition.y, 0f),
                    new Vector3(12f, 6.4f, 1f)));

            var hudGroups = FindInScene<Canvas>(scene)
                .Where(canvas => canvas.gameObject != photoUi.CanvasRoot)
                .Select(canvas => canvas.GetComponent<CanvasGroup>() ??
                    canvas.gameObject.AddComponent<CanvasGroup>())
                .Concat(new[] { photoUi.OpenGroup })
                .Concat(new[] { photoUi.MenuOpenGroup })
                .Distinct()
                .ToArray();
            SetObjects(photo, "hudGroups", hudGroups);

            if (surface != null)
            {
                AppendExtension(surface, accessibility);
                AppendExtension(surface, photo);
                AppendExtension(surface, playerMenu);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, path))
            {
                throw new InvalidOperationException($"Could not save {path}.");
            }
        }

        private static PhotoUi BuildPhotoUi(
            Scene scene,
            PhotoModeController controller,
            PlayerMenuController playerMenu,
            LocalizedEnglishCatalog english,
            HomemadeSignalUiTokens tokens,
            TMP_FontAsset font)
        {
            foreach (var existing in scene.GetRootGameObjects()
                         .Where(item => item.name == "Task28PlayerUi"))
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }

            var canvasRoot = NewUi(
                "Task28PlayerUi",
                null,
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));
            SceneManager.MoveGameObjectToScene(canvasRoot, scene);
            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            var scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = GameplayReference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var safe = FullStretch(
                "SafeArea",
                canvasRoot.transform,
                typeof(SafeAreaFitter));
            var menuOpenRoot = FullStretch(
                "PlayerMenuOpen",
                safe.transform,
                typeof(CanvasGroup));
            var menuOpenButton = CreateButton(
                "PlayerMenuOpenButton",
                menuOpenRoot.transform,
                Require<Sprite>(SecondaryPlate),
                english,
                "hud.pause",
                font,
                new Rect(24f, 28f, 154f, 72f),
                GameplayReference,
                tokens);
            UnityEventTools.AddPersistentListener(
                menuOpenButton.onClick,
                playerMenu.ToggleFromUi);
            var openRoot = FullStretch(
                "PhotoModeOpen",
                safe.transform,
                typeof(CanvasGroup));
            var openButton = CreateButton(
                "PhotoModeOpenButton",
                openRoot.transform,
                Require<Sprite>(SecondaryPlate),
                english,
                Task28English.PhotoTitle,
                font,
                new Rect(1090f, 28f, 154f, 72f),
                GameplayReference,
                tokens);
            UnityEventTools.AddPersistentListener(
                openButton.onClick,
                controller.ToggleFromUi);

            var panel = FullStretch("PhotoModePanel", safe.transform);
            var dim = FullStretch("Dim", panel.transform, typeof(Image))
                .GetComponent<Image>();
            dim.color = new Color(0.004f, 0.012f, 0.03f, 0.38f);
            dim.raycastTarget = true;
            var frame = NewUi("InstrumentFrame", panel.transform, typeof(Image));
            SetReferenceRect(
                frame.GetComponent<RectTransform>(),
                new Rect(742f, 72f, 474f, 572f),
                GameplayReference);
            var frameImage = frame.GetComponent<Image>();
            frameImage.sprite = Require<Sprite>(FrameSprite);
            frameImage.type = Image.Type.Sliced;
            frameImage.color = Color.white;

            CreateLocalizedText(
                "PhotoTitle",
                frame.transform,
                english,
                Task28English.PhotoTitle,
                font,
                32f,
                tokens.WarmPaper,
                new Rect(36f, 34f, 402f, 48f),
                new Vector2(474f, 572f));

            var buttons = new List<(
                string name,
                string key,
                UnityAction callback)>
            {
                ("PanLeft", "photo.pan", controller.PanLeft),
                ("PanRight", "photo.pan", controller.PanRight),
                ("PanUp", "photo.pan", controller.PanUp),
                ("PanDown", "photo.pan", controller.PanDown),
                ("ZoomIn", "photo.zoom", controller.ZoomIn),
                ("ZoomOut", "photo.zoom", controller.ZoomOut),
                ("Depth", "photo.depth", controller.NextDepthLayer),
                ("ExposureDown", "photo.exposure", controller.DecreaseExposure),
                ("ExposureUp", "photo.exposure", controller.IncreaseExposure),
                ("CleanHud", "photo.cleanHud", controller.ToggleCleanHud),
                ("Frame", "photo.frame", controller.NextEarnedFrame),
                ("Capture", "photo.capture", controller.CaptureToGallery),
            };
            for (var index = 0; index < buttons.Count; index++)
            {
                var column = index % 3;
                var row = index / 3;
                var button = CreateButton(
                    buttons[index].name,
                    frame.transform,
                    Require<Sprite>(PrimaryPlate),
                    english,
                    buttons[index].key,
                    font,
                    new Rect(
                        24f + column * 142f,
                        92f + row * 95f,
                        128f,
                        84f),
                    new Vector2(474f, 572f),
                    tokens);
                UnityEventTools.AddPersistentListener(
                    button.onClick,
                    buttons[index].callback);
            }

            var explorer = NewUi("ExplorerControls", frame.transform);
            SetReferenceRect(
                explorer.GetComponent<RectTransform>(),
                new Rect(24f, 474f, 426f, 84f),
                new Vector2(474f, 572f));
            var explorerActions = new (
                string Name,
                string Key,
                UnityAction Action)[]
            {
                ("CinematicLens", "photo.lens", controller.NextCinematicLens),
                ("ExpandedPose", "photo.pose", controller.NextExpandedPose),
                ("SavePreset", "photo.preset", controller.SaveExplorerPresetZero),
                ("LoadPreset", "photo.loadPreset", controller.LoadExplorerPresetZero),
            };
            for (var index = 0; index < explorerActions.Length; index++)
            {
                var button = CreateButton(
                    explorerActions[index].Name,
                    explorer.transform,
                    Require<Sprite>(SecondaryPlate),
                    english,
                    explorerActions[index].Key,
                    font,
                    new Rect(index * 106f, 0f, 100f, 84f),
                    new Vector2(426f, 84f),
                    tokens);
                UnityEventTools.AddPersistentListener(
                    button.onClick,
                    explorerActions[index].Action);
            }

            var earnedFrame = NewUi(
                "EarnedFrame",
                panel.transform,
                typeof(Image)).GetComponent<Image>();
            SetReferenceRect(
                earnedFrame.rectTransform,
                new Rect(12f, 12f, 1256f, 696f),
                GameplayReference);
            earnedFrame.sprite = Require<Sprite>(FrameSprite);
            earnedFrame.type = Image.Type.Sliced;
            earnedFrame.raycastTarget = false;
            earnedFrame.enabled = false;

            panel.SetActive(false);
            explorer.SetActive(false);
            var playerPanel = BuildPlayerMenuPanel(
                safe.transform,
                playerMenu,
                english,
                tokens,
                font);
            SetObject(playerMenu, "m_OpenRoot", menuOpenRoot);
            SetObject(playerMenu, "m_PanelRoot", playerPanel);
            return new PhotoUi(
                canvasRoot,
                panel,
                explorer,
                earnedFrame,
                Require<Sprite>(FrameSprite),
                openRoot.GetComponent<CanvasGroup>(),
                menuOpenRoot.GetComponent<CanvasGroup>());
        }

        private static void PatchChapterOneScene(
            string path,
            LocalizedEnglishCatalog english,
            HomemadeSignalUiTokens tokens,
            TMP_FontAsset standard,
            TMP_FontAsset readable,
            VolumeProfile motionBlurProfile)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var sequence = FindInScene<ChapterOneSequenceController2D>(scene)
                .SingleOrDefault() ?? throw new InvalidOperationException(
                    $"{path} requires exactly one Chapter One sequence.");
            var accessibility = EnsureSingleComponent<AccessibilityApplier>(
                sequence.gameObject);
            SetObject(sequence, "english", english);
            SetObject(accessibility, "m_StandardFont", standard);
            SetObject(accessibility, "m_ReadableFont", readable);
            SetObject(accessibility, "m_ScopeRoot", null);
            AppendChapterExtension(sequence, accessibility);

            if (sequence.SequenceKind == ChapterOneSequenceKind.Clubhouse)
            {
                var catalog = Require<CosmeticCatalog>(CosmeticCatalogAsset);
                var photo = EnsureSingleComponent<PhotoModeController>(
                    sequence.gameObject);
                var playerMenu = EnsureSingleComponent<PlayerMenuController>(
                    sequence.gameObject);
                SetObject(photo, "catalog", catalog);
                SetObject(playerMenu, "m_English", english);
                SetObject(playerMenu, "m_Catalog", catalog);
                SetObjects(playerMenu, "m_AtlasEntries", LoadAtlasEntries());
                var camera = FindInScene<Camera>(scene)
                    .FirstOrDefault(candidate => candidate.orthographic) ??
                    throw new InvalidOperationException(
                        "Clubhouse Photo Mode requires an orthographic camera.");
                SetObject(photo, "photoCamera", camera);
                var ui = BuildPhotoUi(
                    scene,
                    photo,
                    playerMenu,
                    english,
                    tokens,
                    standard);
                BindAccessibilityVisuals(
                    scene,
                    ui.CanvasRoot,
                    tokens,
                    standard,
                    motionBlurProfile);
                SetObject(photo, "panelRoot", ui.PanelRoot);
                SetObject(photo, "explorerControlsRoot", ui.ExplorerRoot);
                SetObject(photo, "earnedFrameImage", ui.FrameImage);
                var photoItems = catalog.Category(CosmeticCategory.Photo)
                    .Where(item => item.PresentationSprite != null)
                    .ToArray();
                SetObjects(photo, "earnedFrames", photoItems
                    .Select(item => item.PresentationSprite).ToArray());
                SetStrings(photo, "earnedFrameIds", photoItems
                    .Select(item => item.Id).ToArray());
                SetObjects(
                    photo,
                    "poseActors",
                    FindInScene<JustSomeStars.Runtime.Animation2D.LayeredCharacterRenderer>(
                        scene));
                var renderers = FindInScene<SpriteRenderer>(scene)
                    .Where(renderer => renderer.gameObject.activeInHierarchy)
                    .ToArray();
                SetObjects(photo, "exposureTargets", renderers);
                SetObjects(photo, "depthLayers", renderers
                    .OrderBy(renderer => renderer.sortingOrder)
                    .Take(Mathf.Min(6, renderers.Length))
                    .ToArray());
                SetBounds(
                    photo,
                    "panBounds",
                    new Bounds(
                        camera.transform.position,
                        new Vector3(12f, 6.4f, 1f)));
                var hudGroups = FindInScene<Canvas>(scene)
                    .Where(canvas => canvas.gameObject != ui.CanvasRoot)
                    .Select(canvas => canvas.GetComponent<CanvasGroup>() ??
                        canvas.gameObject.AddComponent<CanvasGroup>())
                    .Concat(new[] { ui.OpenGroup, ui.MenuOpenGroup })
                    .Distinct()
                    .ToArray();
                SetObjects(photo, "hudGroups", hudGroups);
                AppendChapterExtension(sequence, photo);
                AppendChapterExtension(sequence, playerMenu);
            }
            else
            {
                BindMotionBlur(scene, motionBlurProfile);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, path))
            {
                throw new InvalidOperationException($"Could not save {path}.");
            }
        }

        private static AtlasEntry[] LoadAtlasEntries()
        {
            return AssetDatabase.FindAssets(
                    "t:AtlasEntry",
                    new[] { "Assets/_JustSomeStars/Content/Atlas" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<AtlasEntry>)
                .Where(entry => entry != null)
                .OrderBy(entry => entry.StableId.Value, StringComparer.Ordinal)
                .ToArray();
        }

        private static void BindAccessibilityVisuals(
            Scene scene,
            GameObject canvasRoot,
            HomemadeSignalUiTokens tokens,
            TMP_FontAsset font,
            VolumeProfile motionBlurProfile)
        {
            var safe = RequireTransform(canvasRoot.transform, "SafeArea");
            var status = NewUi(
                "AccessibilityStatus",
                safe,
                typeof(Image));
            SetReferenceRect(
                status.GetComponent<RectTransform>(),
                new Rect(536f, 28f, 208f, 64f),
                GameplayReference);
            var statusImage = status.GetComponent<Image>();
            statusImage.sprite = Require<Sprite>(SecondaryPlate);
            statusImage.type = Image.Type.Sliced;
            statusImage.color = new Color(0.2f, 0.28f, 0.36f, 0.86f);
            statusImage.raycastTarget = false;

            var pulseRoot = FullStretch(
                "SignalPulse",
                status.transform,
                typeof(Image),
                typeof(CanvasGroup),
                typeof(AccessibleSignalPulse),
                typeof(AccessibleEffect));
            var pulseImage = pulseRoot.GetComponent<Image>();
            pulseImage.sprite = Require<Sprite>(PrimaryPlate);
            pulseImage.type = Image.Type.Sliced;
            pulseImage.color = tokens.SignalCyan;
            pulseImage.raycastTarget = false;
            var pulse = pulseRoot.GetComponent<AccessibleSignalPulse>();
            SetObject(pulse, "target", pulseRoot.GetComponent<CanvasGroup>());
            var pulseEffect = pulseRoot.GetComponent<AccessibleEffect>();
            SetEnum(
                pulseEffect,
                "kind",
                (int)AccessibilityEffectKind.Flashing);
            SetObject(pulseEffect, "effect", pulse);

            var labelRoot = NewUi(
                "StatusShape",
                status.transform,
                typeof(TextMeshProUGUI),
                typeof(AccessibleStatusSymbol));
            SetReferenceRect(
                labelRoot.GetComponent<RectTransform>(),
                new Rect(22f, 10f, 164f, 44f),
                new Vector2(208f, 64f));
            var label = labelRoot.GetComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = 22f;
            label.color = tokens.WarmPaper;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            var symbol = labelRoot.GetComponent<AccessibleStatusSymbol>();
            SetObject(symbol, "symbolLabel", label);
            SetString(symbol, "standardSymbol", "●");
            SetString(symbol, "alternateSymbol", "◆");

            BindMotionBlur(scene, motionBlurProfile);
        }

        private static void BindMotionBlur(
            Scene scene,
            VolumeProfile motionBlurProfile)
        {
            foreach (var existing in scene.GetRootGameObjects()
                         .Where(item => item.name == "Task28MotionBlur"))
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }
            var root = new GameObject(
                "Task28MotionBlur",
                typeof(Volume),
                typeof(AccessibleEffect));
            SceneManager.MoveGameObjectToScene(root, scene);
            var volume = root.GetComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 900f;
            volume.sharedProfile = motionBlurProfile;
            volume.enabled = false;
            var effect = root.GetComponent<AccessibleEffect>();
            SetEnum(
                effect,
                "kind",
                (int)AccessibilityEffectKind.MotionBlur);
            SetObject(effect, "effect", volume);
        }

        private static GameObject BuildPlayerMenuPanel(
            Transform safe,
            PlayerMenuController controller,
            LocalizedEnglishCatalog english,
            HomemadeSignalUiTokens tokens,
            TMP_FontAsset font)
        {
            var panel = FullStretch("PlayerMenuPanel", safe);
            var dim = FullStretch("MenuDim", panel.transform, typeof(Image))
                .GetComponent<Image>();
            dim.color = new Color(0.004f, 0.012f, 0.03f, 0.55f);
            dim.raycastTarget = true;
            var frame = NewUi("MenuInstrumentFrame", panel.transform, typeof(Image));
            SetReferenceRect(
                frame.GetComponent<RectTransform>(),
                new Rect(64f, 60f, 1152f, 600f),
                GameplayReference);
            var frameImage = frame.GetComponent<Image>();
            frameImage.sprite = Require<Sprite>(FrameSprite);
            frameImage.type = Image.Type.Sliced;

            var title = CreateLocalizedText(
                "MenuTitle",
                frame.transform,
                english,
                "menu.journey",
                font,
                34f,
                tokens.WarmPaper,
                new Rect(48f, 30f, 510f, 58f),
                new Vector2(1152f, 600f));
            title.alignment = TextAlignmentOptions.Left;
            var context = CreateLocalizedText(
                "MenuContext",
                frame.transform,
                english,
                "menu.context",
                font,
                15f,
                tokens.SignalCyan,
                new Rect(570f, 35f, 530f, 48f),
                new Vector2(1152f, 600f));
            context.alignment = TextAlignmentOptions.Right;

            var nav = new (string Name, string Key, UnityAction Action)[]
            {
                ("Journey", "menu.journey", controller.ShowJourney),
                ("Accessibility", "hud.accessibility", controller.ShowAccessibility),
                ("Atlas", "menu.atlas", controller.ShowAtlas),
                ("Captain", "menu.customization", controller.ShowCaptain),
                ("Shop", "menu.shop", controller.ShowShop),
                ("Account", "menu.account", controller.ShowAccount),
                ("Birthday", "birthday.title", controller.ShowBirthday),
            };
            for (var index = 0; index < nav.Length; index++)
            {
                var button = CreateButton(
                    nav[index].Name,
                    frame.transform,
                    Require<Sprite>(SecondaryPlate),
                    english,
                    nav[index].Key,
                    font,
                    new Rect(48f + index * 150f, 100f, 140f, 68f),
                    new Vector2(1152f, 600f),
                    tokens);
                UnityEventTools.AddPersistentListener(
                    button.onClick,
                    nav[index].Action);
            }

            var detail = CreateLocalizedText(
                "MenuDetail",
                frame.transform,
                english,
                "menu.accessibility.detail",
                font,
                22f,
                tokens.BodyText,
                new Rect(56f, 218f, 474f, 230f),
                new Vector2(1152f, 600f));
            detail.alignment = TextAlignmentOptions.TopLeft;

            var accessibilityGroup = NewUi(
                "AccessibilityControls",
                frame.transform);
            SetReferenceRect(
                accessibilityGroup.GetComponent<RectTransform>(),
                new Rect(554f, 214f, 540f, 190f),
                new Vector2(1152f, 600f));
            var accessibilityName = CreateLocalizedText(
                "AccessibilitySettingName",
                accessibilityGroup.transform,
                english,
                "settings.pilotingAssist",
                font,
                18f,
                tokens.WarmPaper,
                new Rect(0f, 0f, 340f, 52f),
                new Vector2(540f, 190f));
            accessibilityName.alignment = TextAlignmentOptions.Left;
            var accessibilityValue = CreateLocalizedText(
                "AccessibilitySettingValue",
                accessibilityGroup.transform,
                english,
                "value.balanced",
                font,
                18f,
                tokens.SignalCyan,
                new Rect(350f, 0f, 190f, 52f),
                new Vector2(540f, 190f));
            accessibilityValue.alignment = TextAlignmentOptions.Right;
            var accessibilityActions = new (
                string Name,
                string Key,
                UnityAction Action)[]
            {
                ("PreviousSetting", "menu.accessibility.previous",
                    controller.PreviousAccessibilitySetting),
                ("NextSetting", "menu.accessibility.next",
                    controller.NextAccessibilitySetting),
                ("DecreaseSetting", "menu.accessibility.decrease",
                    controller.DecreaseAccessibilitySetting),
                ("IncreaseSetting", "menu.accessibility.increase",
                    controller.IncreaseAccessibilitySetting),
            };
            for (var index = 0; index < accessibilityActions.Length; index++)
            {
                var button = CreateButton(
                    accessibilityActions[index].Name,
                    accessibilityGroup.transform,
                    Require<Sprite>(PrimaryPlate),
                    english,
                    accessibilityActions[index].Key,
                    font,
                    new Rect(index * 135f, 78f, 126f, 84f),
                    new Vector2(540f, 190f),
                    tokens);
                UnityEventTools.AddPersistentListener(
                    button.onClick,
                    accessibilityActions[index].Action);
            }

            var atlasGroup = NewUi("AtlasControls", frame.transform);
            SetReferenceRect(
                atlasGroup.GetComponent<RectTransform>(),
                new Rect(554f, 214f, 540f, 190f),
                new Vector2(1152f, 600f));
            var atlasValue = CreateLocalizedText(
                "AtlasValue",
                atlasGroup.transform,
                english,
                "menu.atlas.none",
                font,
                15f,
                tokens.WarmPaper,
                new Rect(0f, 0f, 540f, 72f),
                new Vector2(540f, 190f));
            atlasValue.alignment = TextAlignmentOptions.TopLeft;
            var atlasActions = new (
                string Name,
                string Key,
                UnityAction Action)[]
            {
                ("PreviousAtlasEntry", "menu.atlas.previous",
                    controller.PreviousAtlasEntry),
                ("NextAtlasEntry", "menu.atlas.next",
                    controller.NextAtlasEntry),
                ("NextAtlasDepth", "menu.atlas.depth",
                    controller.NextAtlasDepth),
            };
            for (var index = 0; index < atlasActions.Length; index++)
            {
                var button = CreateButton(
                    atlasActions[index].Name,
                    atlasGroup.transform,
                    Require<Sprite>(PrimaryPlate),
                    english,
                    atlasActions[index].Key,
                    font,
                    new Rect(index * 180f, 92f, 168f, 84f),
                    new Vector2(540f, 190f),
                    tokens);
                UnityEventTools.AddPersistentListener(
                    button.onClick,
                    atlasActions[index].Action);
            }

            var captainGroup = NewUi("CaptainControls", frame.transform);
            SetReferenceRect(
                captainGroup.GetComponent<RectTransform>(),
                new Rect(554f, 214f, 540f, 190f),
                new Vector2(1152f, 600f));
            var captainValue = CreateLocalizedText(
                "CaptainValue",
                captainGroup.transform,
                english,
                "menu.customization.noSave",
                font,
                15f,
                tokens.WarmPaper,
                new Rect(0f, 0f, 540f, 72f),
                new Vector2(540f, 190f));
            captainValue.alignment = TextAlignmentOptions.TopLeft;
            var captainActions = new (
                string Name,
                string Key,
                UnityAction Action)[]
            {
                ("CaptainBody", "menu.customization.body",
                    controller.CycleCaptainFamilyFromUi),
                ("CaptainAppearance", "menu.customization.appearance",
                    controller.CycleCaptainAppearanceFromUi),
                ("CaptainSuit", "menu.customization.suit",
                    controller.CycleCaptainSuitFromUi),
                ("CaptainCosmetic", "menu.customization.cosmetic",
                    controller.CycleOwnedCaptainCosmeticFromUi),
            };
            var captainButtons = new Button[captainActions.Length];
            for (var index = 0; index < captainActions.Length; index++)
            {
                captainButtons[index] = CreateButton(
                    captainActions[index].Name,
                    captainGroup.transform,
                    Require<Sprite>(PrimaryPlate),
                    english,
                    captainActions[index].Key,
                    font,
                    new Rect(index * 135f, 92f, 126f, 84f),
                    new Vector2(540f, 190f),
                    tokens);
                UnityEventTools.AddPersistentListener(
                    captainButtons[index].onClick,
                    captainActions[index].Action);
            }

            var shopGroup = NewUi("ShopControls", frame.transform);
            SetReferenceRect(
                shopGroup.GetComponent<RectTransform>(),
                new Rect(554f, 214f, 540f, 190f),
                new Vector2(1152f, 600f));
            var shopValue = CreateLocalizedText(
                "ShopProductValue",
                shopGroup.transform,
                english,
                "shop.loading",
                font,
                15f,
                tokens.WarmPaper,
                new Rect(0f, 0f, 540f, 72f),
                new Vector2(540f, 190f));
            shopValue.alignment = TextAlignmentOptions.TopLeft;
            var shopActions = new (
                string Name,
                string Key,
                UnityAction Action)[]
            {
                ("PreviousProduct", "shop.previous",
                    controller.PreviousShopProduct),
                ("NextProduct", "shop.next",
                    controller.NextShopProduct),
                ("PurchaseProduct", "shop.purchase",
                    controller.PurchaseSelectedProductFromUi),
                ("RestorePurchases", "shop.restore",
                    controller.RestorePurchasesFromUi),
            };
            var shopButtons = new Button[shopActions.Length];
            for (var index = 0; index < shopActions.Length; index++)
            {
                shopButtons[index] = CreateButton(
                    shopActions[index].Name,
                    shopGroup.transform,
                    Require<Sprite>(PrimaryPlate),
                    english,
                    shopActions[index].Key,
                    font,
                    new Rect(index * 135f, 92f, 126f, 84f),
                    new Vector2(540f, 190f),
                    tokens);
                UnityEventTools.AddPersistentListener(
                    shopButtons[index].onClick,
                    shopActions[index].Action);
            }

            var grownUpGroup = NewUi(
                "GrownUpChallenge",
                frame.transform,
                typeof(Image));
            SetReferenceRect(
                grownUpGroup.GetComponent<RectTransform>(),
                new Rect(554f, 214f, 540f, 190f),
                new Vector2(1152f, 600f));
            var grownUpImage = grownUpGroup.GetComponent<Image>();
            grownUpImage.sprite = Require<Sprite>(FrameSprite);
            grownUpImage.type = Image.Type.Sliced;
            grownUpImage.color = new Color(0.1f, 0.14f, 0.2f, 0.98f);
            var grownUpPrompt = CreateLocalizedText(
                "GrownUpPrompt",
                grownUpGroup.transform,
                english,
                "shop.grownUpConfirm",
                font,
                16f,
                tokens.WarmPaper,
                new Rect(18f, 10f, 504f, 52f),
                new Vector2(540f, 190f));
            grownUpPrompt.alignment = TextAlignmentOptions.TopLeft;
            var grownUpAnswer = CreateLocalizedText(
                "GrownUpAnswer",
                grownUpGroup.transform,
                english,
                Task28English.NotAvailable,
                font,
                18f,
                tokens.SignalCyan,
                new Rect(180f, 62f, 180f, 34f),
                new Vector2(540f, 190f));
            var challengeActions = new (
                string Name,
                string Key,
                UnityAction Action)[]
            {
                ("AnswerDown", "shop.answerDown", controller.DecreaseGrownUpAnswer),
                ("AnswerUp", "shop.answerUp", controller.IncreaseGrownUpAnswer),
                ("ConfirmGrownUp", "shop.confirm", controller.ConfirmGrownUpFromUi),
                ("CancelGrownUp", "shop.cancel", controller.CancelGrownUpFromUi),
            };
            var challengeButtons = new Button[challengeActions.Length];
            for (var index = 0; index < challengeActions.Length; index++)
            {
                challengeButtons[index] = CreateButton(
                    challengeActions[index].Name,
                    grownUpGroup.transform,
                    Require<Sprite>(SecondaryPlate),
                    english,
                    challengeActions[index].Key,
                    font,
                    new Rect(index * 135f, 104f, 126f, 84f),
                    new Vector2(540f, 190f),
                    tokens);
                UnityEventTools.AddPersistentListener(
                    challengeButtons[index].onClick,
                    challengeActions[index].Action);
            }

            var birthdayGroup = NewUi("BirthdayControls", frame.transform);
            SetReferenceRect(
                birthdayGroup.GetComponent<RectTransform>(),
                new Rect(554f, 214f, 540f, 190f),
                new Vector2(1152f, 600f));
            var birthdaySelectors = new (
                string Name,
                string Key,
                UnityAction Action,
                string Field)[]
            {
                ("BirthdayDay", "birthday.day", controller.NextBirthdayDay,
                    "m_BirthdayDayValue"),
                ("BirthdayMonth", "birthday.month", controller.NextBirthdayMonth,
                    "m_BirthdayMonthValue"),
                ("BirthdayYear", "birthday.year", controller.NextBirthdayYear,
                    "m_BirthdayYearValue"),
            };
            for (var index = 0; index < birthdaySelectors.Length; index++)
            {
                var selector = birthdaySelectors[index];
                var button = CreateButton(
                    selector.Name,
                    birthdayGroup.transform,
                    Require<Sprite>(PrimaryPlate),
                    english,
                    selector.Key,
                    font,
                    new Rect(index * 180f, 0f, 168f, 82f),
                    new Vector2(540f, 190f),
                    tokens);
                UnityEventTools.AddPersistentListener(button.onClick, selector.Action);
                var value = CreateLocalizedText(
                    selector.Name + "Value",
                    button.transform,
                    english,
                    Task28English.NotAvailable,
                    font,
                    14f,
                    tokens.SignalCyan,
                    new Rect(10f, 50f, 148f, 24f),
                    new Vector2(168f, 84f));
                SetObject(controller, selector.Field, value);
            }
            var saveBirthday = CreateButton(
                "SaveBirthday",
                birthdayGroup.transform,
                Require<Sprite>(PrimaryPlate),
                english,
                "birthday.save",
                font,
                new Rect(0f, 104f, 258f, 82f),
                new Vector2(540f, 190f),
                tokens);
            UnityEventTools.AddPersistentListener(
                saveBirthday.onClick,
                controller.SaveBirthdayFromUi);
            var confirmBirthday = CreateButton(
                "ConfirmBirthdayCorrection",
                birthdayGroup.transform,
                Require<Sprite>(SecondaryPlate),
                english,
                "birthday.confirmCorrection",
                font,
                new Rect(270f, 104f, 258f, 82f),
                new Vector2(540f, 190f),
                tokens);
            UnityEventTools.AddPersistentListener(
                confirmBirthday.onClick,
                controller.ConfirmBirthdayCorrectionFromUi);

            var link = CreateButton(
                "LinkAccount",
                frame.transform,
                Require<Sprite>(PrimaryPlate),
                english,
                "account.link",
                font,
                new Rect(56f, 458f, 220f, 82f),
                new Vector2(1152f, 600f),
                tokens);
            UnityEventTools.AddPersistentListener(
                link.onClick,
                controller.LinkAccountFromUi);
            var sync = CreateButton(
                "SyncAccount",
                frame.transform,
                Require<Sprite>(PrimaryPlate),
                english,
                "account.sync",
                font,
                new Rect(290f, 458f, 220f, 82f),
                new Vector2(1152f, 600f),
                tokens);
            UnityEventTools.AddPersistentListener(
                sync.onClick,
                controller.SyncAccountFromUi);
            var resume = CreateButton(
                "Resume",
                frame.transform,
                Require<Sprite>(SecondaryPlate),
                english,
                "menu.resume",
                font,
                new Rect(942f, 458f, 154f, 82f),
                new Vector2(1152f, 600f),
                tokens);
            UnityEventTools.AddPersistentListener(resume.onClick, controller.ToggleFromUi);
            SetObject(controller, "m_Title", title);
            SetObject(controller, "m_Context", context);
            SetObject(controller, "m_Detail", detail);
            SetObject(
                controller,
                "m_AccessibilitySettingName",
                accessibilityName);
            SetObject(
                controller,
                "m_AccessibilitySettingValue",
                accessibilityValue);
            SetObject(controller, "m_AtlasValue", atlasValue);
            SetObject(controller, "m_CaptainValue", captainValue);
            SetObject(controller, "m_ShopProductValue", shopValue);
            SetObject(controller, "m_GrownUpPrompt", grownUpPrompt);
            SetObject(controller, "m_GrownUpAnswerValue", grownUpAnswer);
            SetObject(
                controller,
                "m_AccessibilityControlsRoot",
                accessibilityGroup);
            SetObject(controller, "m_AtlasControlsRoot", atlasGroup);
            SetObject(controller, "m_CaptainControlsRoot", captainGroup);
            SetObject(controller, "m_ShopControlsRoot", shopGroup);
            SetObject(controller, "m_GrownUpChallengeRoot", grownUpGroup);
            SetObject(controller, "m_BirthdayControlsRoot", birthdayGroup);
            SetObject(controller, "m_CaptainFamilyButton", captainButtons[0]);
            SetObject(controller, "m_CaptainAppearanceButton", captainButtons[1]);
            SetObject(controller, "m_CaptainSuitButton", captainButtons[2]);
            SetObject(controller, "m_CaptainCosmeticButton", captainButtons[3]);
            SetObject(controller, "m_ShopPreviousButton", shopButtons[0]);
            SetObject(controller, "m_ShopNextButton", shopButtons[1]);
            SetObject(controller, "m_ShopPurchaseButton", shopButtons[2]);
            SetObject(controller, "m_RestoreButton", shopButtons[3]);
            SetObject(controller, "m_GrownUpConfirmButton", challengeButtons[2]);
            SetObject(controller, "m_GrownUpCancelButton", challengeButtons[3]);
            SetObject(controller, "m_LinkButton", link);
            SetObject(controller, "m_SyncButton", sync);
            SetObject(controller, "m_BirthdaySaveButton", saveBirthday);
            SetObject(controller, "m_BirthdayConfirmButton", confirmBirthday);
            accessibilityGroup.SetActive(false);
            atlasGroup.SetActive(false);
            captainGroup.SetActive(false);
            shopGroup.SetActive(false);
            grownUpGroup.SetActive(false);
            birthdayGroup.SetActive(false);
            panel.SetActive(false);
            return panel;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            Sprite plate,
            LocalizedEnglishCatalog english,
            string key,
            TMP_FontAsset font,
            Rect rect,
            Vector2 reference,
            HomemadeSignalUiTokens tokens)
        {
            var target = rect;
            target.width = Mathf.Max(target.width, 84f);
            target.height = Mathf.Max(target.height, 84f);
            var root = NewUi(name, parent, typeof(Image), typeof(Button));
            SetReferenceRect(root.GetComponent<RectTransform>(), target, reference);
            var image = root.GetComponent<Image>();
            image.sprite = plate;
            image.type = Image.Type.Sliced;
            image.color = new Color(0.78f, 0.72f, 0.66f, 0.92f);
            var button = root.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            CreateLocalizedText(
                name + "Label",
                root.transform,
                english,
                key,
                font,
                17f,
                tokens.BodyText,
                new Rect(12f, 10f, target.width - 24f, target.height - 20f),
                new Vector2(target.width, target.height));
            return button;
        }

        private static TMP_Text CreateLocalizedText(
            string name,
            Transform parent,
            LocalizedEnglishCatalog english,
            string key,
            TMP_FontAsset font,
            float size,
            Color color,
            Rect rect,
            Vector2 reference)
        {
            var root = NewUi(name, parent, typeof(TextMeshProUGUI));
            SetReferenceRect(root.GetComponent<RectTransform>(), rect, reference);
            var label = root.GetComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            root.AddComponent<LocalizedUiLabel>().Configure(english, key);
            return label;
        }

        private static void AppendExtension(
            SurfaceGameplayLifecycle2D lifecycle,
            MonoBehaviour extension)
        {
            var serialized = new SerializedObject(lifecycle);
            var property = serialized.FindProperty("gameplayExtensions") ??
                throw new InvalidOperationException(
                    "Surface lifecycle extension property changed.");
            for (var index = 0; index < property.arraySize; index++)
            {
                if (property.GetArrayElementAtIndex(index).objectReferenceValue ==
                    extension)
                {
                    return;
                }
            }
            property.InsertArrayElementAtIndex(property.arraySize);
            property.GetArrayElementAtIndex(property.arraySize - 1)
                .objectReferenceValue = extension;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(lifecycle);
        }

        private static void AppendChapterExtension(
            ChapterOneSequenceController2D sequence,
            MonoBehaviour extension)
        {
            var serialized = new SerializedObject(sequence);
            var property = serialized.FindProperty("playerUiExtensions") ??
                throw new InvalidOperationException(
                    "Chapter One UI extension property changed.");
            for (var index = 0; index < property.arraySize; index++)
            {
                if (property.GetArrayElementAtIndex(index).objectReferenceValue ==
                    extension)
                {
                    return;
                }
            }
            property.InsertArrayElementAtIndex(property.arraySize);
            property.GetArrayElementAtIndex(property.arraySize - 1)
                .objectReferenceValue = extension;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sequence);
        }

        private static void ValidatePersisted(
            LocalizedEnglishCatalog english,
            HomemadeSignalUiTokens tokens)
        {
            english.ValidateOrThrow();
            tokens.ValidateOrThrow();
            var prefab = Require<GameObject>(FrontendPrefab);
            var view = prefab.GetComponent<FrontendView>();
            var controller = prefab.GetComponent<FrontendController>();
            if (view == null || !view.IsReady || controller == null ||
                prefab.transform.Find("SafeArea/MenuGroup/NewGameButton") == null ||
                prefab.GetComponent<AccessibilityApplier>() == null ||
                prefab.GetComponentsInChildren<Transform>(true).Count(item =>
                    item.name == "FrontendGrownUpChallenge") != 1)
            {
                throw new InvalidOperationException(
                    "Task 28 Frontend bindings did not persist.");
            }
            foreach (var path in GameplayScenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var photo = FindInScene<PhotoModeController>(scene).SingleOrDefault();
                var menu = FindInScene<PlayerMenuController>(scene).SingleOrDefault();
                var accessibility = FindInScene<AccessibilityApplier>(scene)
                    .SingleOrDefault();
                var localizedLabelCount = FindInScene<LocalizedUiLabel>(scene)
                    .Length;
                var statusSymbolCount = FindInScene<AccessibleStatusSymbol>(scene)
                    .Length;
                var effectCount = FindInScene<AccessibleEffect>(scene).Length;
                if (photo == null || menu == null || accessibility == null ||
                    localizedLabelCount < 25 || statusSymbolCount != 1 ||
                    effectCount != 2)
                {
                    throw new InvalidOperationException(
                        $"{path} did not persist its player UI layer " +
                        $"(photo={photo != null}, menu={menu != null}, " +
                        $"accessibility={accessibility != null}, " +
                        $"localizedLabels={localizedLabelCount}, " +
                        $"statusSymbols={statusSymbolCount}, " +
                        $"effects={effectCount}).");
                }
                var serializedMenu = new SerializedObject(menu);
                var atlasEntries = serializedMenu.FindProperty("m_AtlasEntries");
                if (atlasEntries == null || atlasEntries.arraySize == 0)
                {
                    throw new InvalidOperationException(
                        $"{path} did not persist Atlas browsing entries.");
                }
                foreach (var effect in FindInScene<AccessibleEffect>(scene))
                {
                    var serialized = new SerializedObject(effect);
                    if (serialized.FindProperty("effect")?.objectReferenceValue ==
                        null)
                    {
                        throw new InvalidOperationException(
                            $"{path} has an unbound accessibility effect.");
                    }
                }
                var isSurface = FindInScene<SurfaceGameplayLifecycle2D>(scene)
                    .Length == 1;
                if (FindInScene<AccessibleTouchLayout>(scene).Length !=
                    (isSurface ? 1 : 0))
                {
                    throw new InvalidOperationException(
                        $"{path} has duplicate or missing touch-layout bindings.");
                }
                var needsCaption = path.EndsWith(
                        "Mirra.unity",
                        StringComparison.Ordinal) ||
                    path.EndsWith("KoroVesper.unity", StringComparison.Ordinal);
                if (FindInScene<AccessibleCaption>(scene).Length !=
                    (needsCaption ? 1 : 0))
                {
                    throw new InvalidOperationException(
                        $"{path} has duplicate or missing caption bindings.");
                }
            }
            foreach (var path in ChapterOneScenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var sequence = FindInScene<ChapterOneSequenceController2D>(scene)
                    .SingleOrDefault();
                if (sequence == null ||
                    FindInScene<AccessibilityApplier>(scene).Length != 1 ||
                    FindInScene<AccessibleEffect>(scene).Count(effect =>
                        new SerializedObject(effect).FindProperty("kind")?
                            .enumValueIndex ==
                        (int)AccessibilityEffectKind.MotionBlur) != 1)
                {
                    throw new InvalidOperationException(
                        $"{path} did not persist accessibility bindings.");
                }
                if (sequence.SequenceKind == ChapterOneSequenceKind.Clubhouse &&
                    (FindInScene<PlayerMenuController>(scene).Length != 1 ||
                     FindInScene<PhotoModeController>(scene).Length != 1))
                {
                    throw new InvalidOperationException(
                        "Clubhouse did not persist its complete player UI.");
                }
            }
        }

        private static void SetPlateToParent(Transform button)
        {
            var plate = RequireTransform(button, "PlateVisual")
                .GetComponent<RectTransform>();
            plate.anchorMin = Vector2.zero;
            plate.anchorMax = Vector2.one;
            plate.offsetMin = Vector2.zero;
            plate.offsetMax = Vector2.zero;
        }

        private static void RenameDescendant(
            Transform root,
            string oldName,
            string newName)
        {
            RequireTransform(root, oldName).name = newName;
        }

        private static T RequireComponent<T>(GameObject root) where T : Component
        {
            var component = root.GetComponent<T>();
            return component != null
                ? component
                : throw new InvalidOperationException(
                    $"{root.name} is missing {typeof(T).Name}.");
        }

        private static T EnsureSingleComponent<T>(GameObject root)
            where T : Component
        {
            var existing = root.GetComponents<T>();
            var retained = existing.FirstOrDefault();
            for (var index = 1; index < existing.Length; index++)
            {
                UnityEngine.Object.DestroyImmediate(existing[index]);
            }
            return retained != null ? retained : root.AddComponent<T>();
        }

        private static TMP_Text RequireText(Transform root, string name)
        {
            var transform = RequireTransform(root, name);
            return transform.GetComponent<TMP_Text>() ??
                throw new InvalidOperationException($"{name} is not TMP text.");
        }

        private static Transform RequireTransform(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == name)
                {
                    return transform;
                }
            }
            throw new InvalidOperationException(
                $"{root.name} is missing descendant {name}.");
        }

        private static T Require<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            return asset != null
                ? asset
                : throw new InvalidOperationException(
                    $"Task 28 requires {typeof(T).Name} at {path}.");
        }

        private static T[] FindInScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }

        private static GameObject NewUi(
            string name,
            Transform parent,
            params Type[] components)
        {
            var types = new List<Type> { typeof(RectTransform) };
            types.AddRange(components.Where(type =>
                type != null && type != typeof(RectTransform)));
            var result = new GameObject(name, types.ToArray());
            if (parent != null)
            {
                result.transform.SetParent(parent, false);
            }
            return result;
        }

        private static GameObject FullStretch(
            string name,
            Transform parent,
            params Type[] components)
        {
            var result = NewUi(name, parent, components);
            var rect = result.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return result;
        }

        private static void SetReferenceRect(
            RectTransform target,
            Rect spec,
            Vector2 reference)
        {
            target.anchorMin = new Vector2(
                spec.x / reference.x,
                1f - (spec.y + spec.height) / reference.y);
            target.anchorMax = new Vector2(
                (spec.x + spec.width) / reference.x,
                1f - spec.y / reference.y);
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
        }

        private static void SetRectInParent(RectTransform target, Rect spec)
        {
            target.anchorMin = new Vector2(0f, 1f);
            target.anchorMax = new Vector2(0f, 1f);
            target.pivot = new Vector2(0f, 1f);
            target.anchoredPosition = new Vector2(spec.x, -spec.y);
            target.sizeDelta = new Vector2(spec.width, spec.height);
        }

        private static void SetObject(
            UnityEngine.Object target,
            string field,
            UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ??
                throw new InvalidOperationException(
                    $"{target.GetType().Name}.{field} is missing.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetObjects<T>(
            UnityEngine.Object target,
            string field,
            IReadOnlyList<T> values) where T : UnityEngine.Object
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ??
                throw new InvalidOperationException(
                    $"{target.GetType().Name}.{field} is missing.");
            property.arraySize = values.Count;
            for (var index = 0; index < values.Count; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue =
                    values[index];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetStrings(
            UnityEngine.Object target,
            string field,
            IReadOnlyList<string> values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ??
                throw new InvalidOperationException(
                    $"{target.GetType().Name}.{field} is missing.");
            property.arraySize = values.Count;
            for (var index = 0; index < values.Count; index++)
            {
                property.GetArrayElementAtIndex(index).stringValue = values[index];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetString(
            UnityEngine.Object target,
            string field,
            string value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ??
                throw new InvalidOperationException(
                    $"{target.GetType().Name}.{field} is missing.");
            property.stringValue = value ?? string.Empty;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetEnum(
            UnityEngine.Object target,
            string field,
            int value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ??
                throw new InvalidOperationException(
                    $"{target.GetType().Name}.{field} is missing.");
            property.enumValueIndex = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetBounds(
            UnityEngine.Object target,
            string field,
            Bounds value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ??
                throw new InvalidOperationException(
                    $"{target.GetType().Name}.{field} is missing.");
            property.boundsValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void EnsureFolder(string path)
        {
            var normalized = path.Replace('\\', '/');
            var pieces = normalized.Split('/');
            var current = pieces[0];
            for (var index = 1; index < pieces.Length; index++)
            {
                var next = current + "/" + pieces[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, pieces[index]);
                }
                current = next;
            }
        }

        private readonly struct PhotoUi
        {
            public PhotoUi(
                GameObject canvasRoot,
                GameObject panelRoot,
                GameObject explorerRoot,
                Image frameImage,
                Sprite frameSprite,
                CanvasGroup openGroup,
                CanvasGroup menuOpenGroup)
            {
                CanvasRoot = canvasRoot;
                PanelRoot = panelRoot;
                ExplorerRoot = explorerRoot;
                FrameImage = frameImage;
                FrameSprite = frameSprite;
                OpenGroup = openGroup;
                MenuOpenGroup = menuOpenGroup;
            }

            public GameObject CanvasRoot { get; }
            public GameObject PanelRoot { get; }
            public GameObject ExplorerRoot { get; }
            public Image FrameImage { get; }
            public Sprite FrameSprite { get; }
            public CanvasGroup OpenGroup { get; }
            public CanvasGroup MenuOpenGroup { get; }
        }
    }
}
