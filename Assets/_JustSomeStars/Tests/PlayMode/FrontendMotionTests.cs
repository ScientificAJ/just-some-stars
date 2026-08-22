using System.Collections;
using System.Linq;
using JustSomeStars.Runtime.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class FrontendMotionTests
    {
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

            Assert.That(settings, Is.Not.Null);
            Assert.That(credits, Is.Not.Null);
            Assert.That(privacy, Is.Not.Null);
            Assert.That(close, Is.Not.Null);
            Assert.That(panel, Is.Not.Null);
            Assert.That(panelTitle, Is.Not.Null);
            Assert.That(panelBody, Is.Not.Null);
            Assert.That(panelFrame, Is.Not.Null);
            Assert.That(panelScroll, Is.Not.Null);

            settings.onClick.Invoke();
            yield return null;
            Assert.That(panel.activeSelf, Is.True);
            Assert.That(panelTitle.text, Is.EqualTo("Settings"));
            Assert.That(
                panelBody.text,
                Is.EqualTo(
                    "Settings arrive in a later flight.\n" +
                    "This screen does not save or\n" +
                    "change controls yet."));
            Assert.That(panelFrame.sizeDelta.y, Is.EqualTo(424f));
            Assert.That(panelTitle.rectTransform.anchoredPosition.y, Is.EqualTo(-128f));
            Assert.That(
                panelScroll.GetComponent<RectTransform>().anchoredPosition.y,
                Is.EqualTo(-194f));
            Assert.That(
                panelScroll.GetComponent<RectTransform>().sizeDelta.y,
                Is.EqualTo(118f));

            close.onClick.Invoke();
            yield return WaitForPanelState(panel, active: false);
            Assert.That(panel.activeSelf, Is.False);

            credits.onClick.Invoke();
            yield return null;
            Assert.That(panel.activeSelf, Is.True);
            Assert.That(panelTitle.text, Is.EqualTo("Credits & Licenses"));
            Assert.That(panelScroll.verticalNormalizedPosition, Is.EqualTo(1f).Within(0.001f));
            Assert.That(panelFrame.sizeDelta.y, Is.EqualTo(441f));
            Assert.That(panelTitle.rectTransform.anchoredPosition.y, Is.EqualTo(-108f));
            Assert.That(
                panelScroll.GetComponent<RectTransform>().anchoredPosition.y,
                Is.EqualTo(-160f));
            Assert.That(
                panelScroll.GetComponent<RectTransform>().sizeDelta.y,
                Is.EqualTo(180f));
            Assert.That(panelBody.text, Does.Contain("<b><color=#F7D7AB>Liberation Sans"));
            Assert.That(panelBody.text, Does.Contain("<size=14><line-height=150%>"));
            Assert.That(panelBody.text, Does.Not.Match("(?m)^ {2,}\\S"));

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
