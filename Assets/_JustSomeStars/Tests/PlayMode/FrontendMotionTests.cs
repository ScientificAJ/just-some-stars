using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Input;
using JustSomeStars.Runtime.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class FrontendMotionTests
    {
        private SettingsService m_SettingsService;
        private InputRouter m_InputRouter;
        private string m_SettingsRoot;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (m_InputRouter != null)
            {
                m_InputRouter.ShutdownAsync().GetAwaiter().GetResult();
            }

            if (m_SettingsService != null)
            {
                m_SettingsService.ShutdownAsync().GetAwaiter().GetResult();
            }

            if (!string.IsNullOrEmpty(m_SettingsRoot) &&
                Directory.Exists(m_SettingsRoot))
            {
                Directory.Delete(m_SettingsRoot, recursive: true);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator RedesignedFrontend_SettlesAndKeepsRealControlsWorking()
        {
            yield return SceneManager.LoadSceneAsync("Frontend", LoadSceneMode.Single);
            yield return null;

            var behaviours = Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            var motion = behaviours.SingleOrDefault(component =>
                component != null &&
                component.GetType().Name == "FrontendMotionDirector");
            Assert.That(motion, Is.Not.Null);

            var isSettled = motion.GetType().GetProperty("IsSettled");
            var activeSequenceCount = motion.GetType().GetProperty(
                "ActiveSequenceCount");
            Assert.That(isSettled, Is.Not.Null);
            Assert.That(activeSequenceCount, Is.Not.Null);

            var deadline = Time.realtimeSinceStartup + 3f;
            while (!(bool)isSettled.GetValue(motion) &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That((bool)isSettled.GetValue(motion), Is.True);
            Assert.That((int)activeSequenceCount.GetValue(motion), Is.LessThanOrEqualTo(1));

            var view = Object.FindFirstObjectByType<FrontendView>(
                FindObjectsInactive.Include);
            Assert.That(view, Is.Not.Null);
            var controller = Object.FindFirstObjectByType<FrontendController>(
                FindObjectsInactive.Include);
            var lifecycle = Object.FindFirstObjectByType<UnityFrontendLifecycle>(
                FindObjectsInactive.Include);
            Assert.That(controller, Is.Not.Null);
            Assert.That(lifecycle, Is.Not.Null);
            Assert.That(InputSystem.actions, Is.Not.Null);
            m_SettingsRoot = Path.Combine(
                Path.GetTempPath(),
                "JssTask6FrontendMotionTests",
                System.Guid.NewGuid().ToString("N"));
            m_SettingsService = new SettingsService(Path.Combine(
                m_SettingsRoot,
                "jss-settings-v1.json"));
            Assert.That(
                m_SettingsService.InitializeAsync(CancellationToken.None)
                    .GetAwaiter()
                    .GetResult()
                    .IsAvailable,
                Is.True);
            m_InputRouter = new InputRouter(
                InputSystem.actions,
                m_SettingsService);
            Assert.That(
                m_InputRouter.InitializeAsync(CancellationToken.None)
                    .GetAwaiter()
                    .GetResult()
                    .IsAvailable,
                Is.True);
            var dependencies = new FrontendDependencies(
                m_SettingsService,
                m_InputRouter);
            lifecycle.Configure(dependencies);
            controller.Configure(dependencies);

            var settings = FindDescendant(view.transform, "SettingsButton")
                ?.GetComponent<Button>();
            var credits = FindDescendant(view.transform, "CreditsButton")
                ?.GetComponent<Button>();
            var privacy = FindDescendant(view.transform, "PrivacyButton")
                ?.GetComponent<Button>();
            var close = FindDescendant(view.transform, "CloseButton")
                ?.GetComponent<Button>();
            var panel = FindDescendant(view.transform, "LocalPanel")?.gameObject;
            var panelTitle = FindDescendant(view.transform, "PanelTitle")
                ?.GetComponent<TMP_Text>();
            var panelBody = FindDescendant(view.transform, "PanelBody")
                ?.GetComponent<TMP_Text>();
            var panelFrame = FindDescendant(view.transform, "PanelFrame") as
                RectTransform;
            var panelScroll = FindDescendant(view.transform, "PanelBodyScroll")
                ?.GetComponent<ScrollRect>();
            var settingsControls = FindDescendant(
                view.transform,
                "SettingsControls")?.gameObject;
            var settingsScroll = settingsControls?.GetComponent<ScrollRect>();
            var decreaseCaptions = FindDescendant(
                view.transform,
                "Decrease03")?.GetComponent<Button>();
            var captionsValue = FindDescendant(
                view.transform,
                "Value03")?.GetComponent<TMP_Text>();

            Assert.That(settings, Is.Not.Null);
            Assert.That(credits, Is.Not.Null);
            Assert.That(privacy, Is.Not.Null);
            Assert.That(close, Is.Not.Null);
            Assert.That(panel, Is.Not.Null);
            Assert.That(panelTitle, Is.Not.Null);
            Assert.That(panelBody, Is.Not.Null);
            Assert.That(panelFrame, Is.Not.Null);
            Assert.That(panelScroll, Is.Not.Null);
            Assert.That(settingsControls, Is.Not.Null);
            Assert.That(settingsScroll, Is.Not.Null);
            Assert.That(decreaseCaptions, Is.Not.Null);
            Assert.That(captionsValue, Is.Not.Null);

            settings.onClick.Invoke();
            yield return null;
            Assert.That(panel.activeSelf, Is.True);
            Assert.That(panelTitle.text, Is.EqualTo("Settings"));
            Assert.That(
                panelBody.text,
                Is.EqualTo(
                    "Device settings are saved locally and are never included " +
                    "in cloud backup."));
            Assert.That(settingsControls.activeSelf, Is.True);
            Assert.That(panelScroll.gameObject.activeSelf, Is.False);
            Assert.That(panelFrame.sizeDelta.y, Is.EqualTo(424f));
            Assert.That(panelTitle.rectTransform.anchoredPosition.y, Is.EqualTo(-128f));
            Assert.That(
                panelScroll.GetComponent<RectTransform>().anchoredPosition.y,
                Is.EqualTo(-194f));
            Assert.That(
                panelScroll.GetComponent<RectTransform>().sizeDelta.y,
                Is.EqualTo(118f));
            Assert.That(
                settingsControls.GetComponent<RectTransform>()
                    .anchoredPosition.y,
                Is.EqualTo(-194f));
            Assert.That(
                settingsControls.GetComponent<RectTransform>().sizeDelta.y,
                Is.EqualTo(118f));
            Assert.That(settingsScroll.verticalNormalizedPosition,
                Is.EqualTo(1f).Within(0.001f));

            decreaseCaptions.onClick.Invoke();
            Assert.That(m_SettingsService.Current.CaptionsEnabled, Is.False);
            Assert.That(captionsValue.text, Is.EqualTo("Off"));
            Assert.That(
                File.Exists(Path.Combine(
                    m_SettingsRoot,
                    "jss-settings-v1.json")),
                Is.True);

            close.onClick.Invoke();
            yield return WaitForPanelState(panel, active: false);
            Assert.That(panel.activeSelf, Is.False);

            credits.onClick.Invoke();
            yield return null;
            Assert.That(panel.activeSelf, Is.True);
            Assert.That(panelTitle.text, Is.EqualTo("Credits & Licenses"));
            Assert.That(settingsControls.activeSelf, Is.False);
            Assert.That(panelScroll.gameObject.activeSelf, Is.True);
            Assert.That(panelScroll.verticalNormalizedPosition, Is.EqualTo(1f).Within(0.001f));
            Assert.That(panelFrame.sizeDelta.y, Is.EqualTo(441f));
            Assert.That(panelTitle.rectTransform.anchoredPosition.y, Is.EqualTo(-108f));
            Assert.That(
                panelScroll.GetComponent<RectTransform>().anchoredPosition.y,
                Is.EqualTo(-160f));
            Assert.That(
                panelScroll.GetComponent<RectTransform>().sizeDelta.y,
                Is.EqualTo(180f));
            Assert.That(panelBody.richText, Is.False);
            Assert.That(panelBody.text, Does.Not.Contain("<b>"));
            Assert.That(panelBody.text, Does.Not.Contain("<size="));
            Assert.That(panelBody.text, Does.Contain("SIL OPEN FONT LICENSE"));
            Assert.That(panelBody.text, Does.Contain("Apache License 2.0"));
            Assert.That(
                panelBody.text,
                Does.Contain("Version 2.0, January 2004"));
            Assert.That(
                panelBody.text,
                Does.EndWith("limitations under the License.\n"));

            close.onClick.Invoke();
            yield return WaitForPanelState(panel, active: false);
            privacy.onClick.Invoke();
            yield return null;
            Assert.That(panelTitle.text, Is.EqualTo("Privacy"));
        }

        private static IEnumerator WaitForPanelState(
            GameObject panel,
            bool active)
        {
            var deadline = Time.realtimeSinceStartup + 1f;
            while (panel.activeSelf != active &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(panel.activeSelf, Is.EqualTo(active));
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
    }
}
