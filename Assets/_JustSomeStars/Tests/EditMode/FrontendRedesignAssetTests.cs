using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;
using UnityEngine.UI;

namespace JustSomeStars.Tests.EditMode
{
    public sealed class FrontendRedesignAssetTests
    {
        private const string PrefabPath =
            "Assets/_JustSomeStars/Prefabs/UI/FrontendVisualRoot.prefab";
        private const string TextureRoot =
            "Assets/_JustSomeStars/Art/UI/FrontendRedesign/Textures/";

        [Test]
        public void ApprovedTargetsAndAnimationReadyPrefab_AreCanonical()
        {
            AssertSha256(
                "outputs/frontend-redesign-targets/main-landscape.png",
                "4c70a107b5206d976b3febcb3d41b0d6408cac084002a697f3625374bd59796d");
            AssertSha256(
                "outputs/frontend-redesign-targets/settings-landscape.png",
                "27edc232b6c8901430c2712811951e643dc53ed1c3900e5df4164c16ff8d50e1");
            AssertSha256(
                "outputs/frontend-redesign-targets/credits-top-landscape.png",
                "c509a7e69c913b8d793738bd1749dfc10248fd5a8196043389c5243eebcebe7c");
            AssertSha256(
                "outputs/frontend-redesign-targets/credits-tail-landscape.png",
                "51d6a7d34f109ff8dbf14259067936676adc929d16af08d27c316d83ecb99e89");
            AssertSha256(
                "outputs/frontend-redesign-targets/privacy-landscape.png",
                "a7cdc5e2a38bd4732dad4f4a00b4311473baae263cb4d5872fbbde90cdc54881");

            var requiredTextures = new[]
            {
                "LandscapePlate.png",
                "TitleOverlay.png",
                "StarGlints.png",
                "SignalBeam.png",
                "TelescopeLensGlow.png",
                "PrimaryPlate.png",
                "SecondaryPlateSettings.png",
                "SecondaryPlateCredits.png",
                "SecondaryPlatePrivacy.png",
                "ModalFrame.png",
                "GlyphSettings.png",
                "GlyphCredits.png",
                "GlyphPrivacy.png",
            };
            foreach (var textureName in requiredTextures)
            {
                var path = TextureRoot + textureName;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.That(texture, Is.Not.Null, path);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, PrefabPath);
            Assert.That(prefab.name, Is.EqualTo("FrontendVisualRoot"));
            var playerSettings = GetEffectivePlayerSettings();
            var serializedSettings = new SerializedObject(playerSettings);
            Assert.That(
                serializedSettings.FindProperty("defaultScreenOrientation")
                    ?.intValue,
                Is.EqualTo((int)UIOrientation.AutoRotation));
            Assert.That(
                serializedSettings.FindProperty("allowedAutorotateToPortrait")
                    ?.boolValue,
                Is.False);
            Assert.That(
                serializedSettings.FindProperty(
                    "allowedAutorotateToPortraitUpsideDown")?.boolValue,
                Is.False);
            Assert.That(
                serializedSettings.FindProperty("allowedAutorotateToLandscapeLeft")
                    ?.boolValue,
                Is.True);
            Assert.That(
                serializedSettings.FindProperty("allowedAutorotateToLandscapeRight")
                    ?.boolValue,
                Is.True);
            Assert.That(
                prefab.transform.Find("BackgroundLayers/LandscapePlate"),
                Is.Not.Null);
            Assert.That(
                prefab.transform.Find("SafeArea/TitleGroup/TitleOverlay"),
                Is.Not.Null);
            Assert.That(
                prefab.transform.Find("SafeArea/MenuGroup/ContinueButton")
                    ?.GetComponent<Button>(),
                Is.Not.Null);
            Assert.That(
                prefab.transform.Find("SafeArea/LocalPanel/PanelFrame/CloseButton")
                    ?.GetComponent<Button>(),
                Is.Not.Null);
            var panelFrame = prefab.transform.Find(
                "SafeArea/LocalPanel/PanelFrame") as RectTransform;
            var panelTitle = FindTextComponent(
                prefab.transform.Find(
                    "SafeArea/LocalPanel/PanelFrame/PanelTitle"));
            var panelBody = FindTextComponent(
                prefab.transform.Find(
                    "SafeArea/LocalPanel/PanelFrame/PanelBodyScroll/Viewport/PanelBody"));
            var panelDim = prefab.transform.Find(
                    "SafeArea/LocalPanel/PanelDim")
                ?.GetComponent<Image>();
            Assert.That(panelFrame, Is.Not.Null);
            Assert.That(panelFrame.sizeDelta, Is.EqualTo(new Vector2(426f, 424f)));
            Assert.That(panelTitle, Is.Not.Null);
            var serializedTitle = new SerializedObject(panelTitle);
            Assert.That(
                serializedTitle.FindProperty("m_fontStyle")?.intValue,
                Is.EqualTo(1));
            Assert.That(
                serializedTitle.FindProperty("m_fontSize")?.floatValue,
                Is.EqualTo(32f));
            var titleColor = serializedTitle.FindProperty("m_fontColor")?.colorValue;
            Assert.That(titleColor, Is.Not.Null);
            Assert.That(titleColor.Value.r, Is.EqualTo(247f / 255f).Within(0.001f));
            Assert.That(titleColor.Value.g, Is.EqualTo(215f / 255f).Within(0.001f));
            Assert.That(titleColor.Value.b, Is.EqualTo(171f / 255f).Within(0.001f));
            Assert.That(panelBody, Is.Not.Null);
            var serializedBody = new SerializedObject(panelBody);
            Assert.That(
                serializedBody.FindProperty("m_fontSize")?.floatValue,
                Is.EqualTo(18f));
            Assert.That(
                serializedBody.FindProperty("m_HorizontalAlignment")?.intValue,
                Is.EqualTo(1));
            Assert.That(
                serializedBody.FindProperty("m_VerticalAlignment")?.intValue,
                Is.EqualTo(256));
            Assert.That(
                serializedBody.FindProperty("m_lineSpacing")?.floatValue,
                Is.EqualTo(3f));
            Assert.That(panelDim, Is.Not.Null);
            Assert.That(panelDim.color.a, Is.EqualTo(0.68f).Within(0.001f));
            Assert.That(
                prefab.transform.Find(
                    "SafeArea/LocalPanel/PanelFrame/PanelTitleRule"),
                Is.Null,
                "The immutable modal targets do not contain a title divider.");
            Assert.That(
                prefab.GetComponentsInChildren<MonoBehaviour>(true),
                Has.Some.Matches<MonoBehaviour>(component =>
                    component != null &&
                    component.GetType().Name == "FrontendMotionDirector"));
        }

        private static Component FindTextComponent(Transform transform)
        {
            return transform == null
                ? null
                : transform.GetComponents<Component>().SingleOrDefault(component =>
                    component != null &&
                    component.GetType().FullName == "TMPro.TextMeshProUGUI");
        }

        private static void AssertSha256(string relativePath, string expected)
        {
            var absolutePath = Path.GetFullPath(relativePath);
            Assert.That(File.Exists(absolutePath), Is.True, absolutePath);
            using var stream = File.OpenRead(absolutePath);
            using var hash = SHA256.Create();
            var actual = string.Concat(
                hash.ComputeHash(stream).Select(value =>
                    value.ToString("x2")));
            Assert.That(actual, Is.EqualTo(expected), relativePath);
        }

        private static PlayerSettings GetEffectivePlayerSettings()
        {
            var buildProfileType = typeof(BuildProfile);
            var globalField = buildProfileType.GetField(
                "s_GlobalPlayerSettings",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(globalField, Is.Not.Null);
            var settings = globalField.GetValue(null) as PlayerSettings;
            var activeProfile = BuildProfile.GetActiveBuildProfile();
            if (activeProfile != null)
            {
                var overrideField = buildProfileType.GetField(
                    "m_PlayerSettings",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(overrideField, Is.Not.Null);
                settings = overrideField.GetValue(activeProfile) as PlayerSettings ??
                           settings;
            }

            Assert.That(settings, Is.Not.Null);
            return settings;
        }
    }
}
