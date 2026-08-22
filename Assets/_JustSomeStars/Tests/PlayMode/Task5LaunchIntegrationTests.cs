using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Development;
using JustSomeStars.Runtime.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class Task5LaunchIntegrationTests
    {
        private const string CreditsPrefix =
            "Just Some Stars is being built by ScientificAJ. This Development " +
            "Flight contains a launch screen, not finished gameplay.\n\n" +
            "Liberation Sans\n\n";
        private const string ApacheCreditsPrefix =
            "\n\nAndroid open-source components\n\n" +
            "This Android build includes AndroidX, Kotlin, Kotlin coroutines, " +
            "JetBrains annotations, and Guava components distributed under " +
            "the Apache License 2.0. The complete license follows.\n\n" +
            "Apache License 2.0\n\n";

        private string m_PreviousSceneName;
        private string m_PreviousScenePath;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var activeScene = SceneManager.GetActiveScene();
            m_PreviousSceneName = activeScene.name;
            m_PreviousScenePath = activeScene.path;
            GameBootstrap.CompositionFactory = null;
            yield return ShutdownAndDestroyAllBootstraps();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return ShutdownAndDestroyAllBootstraps();
            GameBootstrap.CompositionFactory = null;
            yield return RestorePriorTestScene();
            Assert.That(FindAllBootstraps(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator RealBoot_WithDevelopmentInstaller_ActivatesFrontendOnce()
        {
            DevelopmentBootstrapInstaller.Install();
            Assert.That(GameBootstrap.CompositionFactory, Is.Not.Null);

            var bootLoad = SceneManager.LoadSceneAsync("Boot", LoadSceneMode.Single);
            Assert.That(bootLoad, Is.Not.Null);
            yield return bootLoad;
            yield return WaitForFrontendStartup();

            var bootstraps = FindAllBootstraps();
            Assert.That(bootstraps, Has.Length.EqualTo(1));
            var bootstrap = bootstraps[0];
            Assert.That(bootstrap.gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Frontend"));
            Assert.That(bootstrap.LastStartupReport, Is.Not.Null);
            Assert.That(bootstrap.LastStartupReport.IsSuccessful, Is.True);
            Assert.That(bootstrap.LastStartupReport.RoutedToFrontend, Is.True);
            Assert.That(
                bootstrap.LastStartupReport.RequestedDestination,
                Is.EqualTo("Frontend"));
            Assert.That(
                bootstrap.LastStartupReport.Services.Count,
                Is.EqualTo(5));

            var inputModule = UnityEngine.Object.FindFirstObjectByType<
                InputSystemUIInputModule>(FindObjectsInactive.Include);
            Assert.That(inputModule, Is.Not.Null);
            Assert.That(inputModule.enabled, Is.True);
            Assert.That(inputModule.actionsAsset, Is.Not.Null);
            Assert.That(inputModule.actionsAsset.enabled, Is.True);
            Assert.That(inputModule.point, Is.Not.Null);
            Assert.That(inputModule.point.action, Is.Not.Null);
            Assert.That(inputModule.point.action.enabled, Is.True);
            Assert.That(inputModule.point.action.bindings, Is.Not.Empty);
            Assert.That(inputModule.leftClick, Is.Not.Null);
            Assert.That(inputModule.leftClick.action, Is.Not.Null);
            Assert.That(inputModule.leftClick.action.enabled, Is.True);
            Assert.That(inputModule.leftClick.action.bindings, Is.Not.Empty);

            var view = UnityEngine.Object.FindFirstObjectByType<FrontendView>(
                FindObjectsInactive.Include);
            Assert.That(view, Is.Not.Null);
            var localPanel = FindDescendant(view.transform, "LocalPanel");
            var panelTitle = FindDescendant(view.transform, "PanelTitle")
                .GetComponent<TextMeshProUGUI>();
            var panelBody = FindDescendant(view.transform, "PanelBody")
                .GetComponent<TextMeshProUGUI>();
            var panelBodyScroll = FindDescendant(
                    view.transform,
                    "PanelBodyScroll")
                .GetComponent<ScrollRect>();
            var continueButton = FindDescendant(view.transform, "ContinueButton")
                .GetComponent<Button>();
            var settingsButton = FindDescendant(view.transform, "SettingsButton")
                .GetComponent<Button>();
            var creditsButton = FindDescendant(view.transform, "CreditsButton")
                .GetComponent<Button>();
            var privacyButton = FindDescendant(view.transform, "PrivacyButton")
                .GetComponent<Button>();
            var closeButton = FindDescendant(view.transform, "CloseButton")
                .GetComponent<Button>();

            Assert.That(localPanel.activeSelf, Is.False);
            Assert.That(continueButton.interactable, Is.False);
            continueButton.onClick.Invoke();
            Assert.That(localPanel.activeSelf, Is.False);

            settingsButton.onClick.Invoke();
            Assert.That(localPanel.activeSelf, Is.True);
            Assert.That(panelTitle.text, Is.EqualTo("Settings"));
            creditsButton.onClick.Invoke();
            Assert.That(panelTitle.text, Is.EqualTo("Credits & Licenses"));
            var controller = UnityEngine.Object.FindFirstObjectByType<
                FrontendController>(FindObjectsInactive.Include);
            Assert.That(controller, Is.Not.Null);
            var liberationLicense = GetLicense(
                controller,
                "m_LiberationSansLicense");
            var apacheLicense = GetLicense(controller, "m_ApacheLicense");
            Assert.That(liberationLicense, Is.Not.Null);
            Assert.That(apacheLicense, Is.Not.Null);
            Assert.That(
                liberationLicense.text,
                Does.Contain("SIL OPEN FONT LICENSE Version 1.1"));
            Assert.That(
                apacheLicense.text,
                Does.Contain("Version 2.0, January 2004"));
            Assert.That(
                panelBody.text,
                Is.EqualTo(
                    CreditsPrefix +
                    liberationLicense.text +
                    ApacheCreditsPrefix +
                    apacheLicense.text));
            Assert.That(
                panelBody.text.IndexOf(
                    liberationLicense.text,
                    System.StringComparison.Ordinal),
                Is.LessThan(panelBody.text.IndexOf(
                    ApacheCreditsPrefix,
                    System.StringComparison.Ordinal)),
                "The Android dependency notice and Apache license must follow " +
                "the complete Liberation Sans OFL.");
            Assert.That(
                panelBody.text.EndsWith(
                    apacheLicense.text,
                    System.StringComparison.Ordinal),
                Is.True,
                "Credits must end with the complete canonical Apache license.");
            Assert.That(panelBodyScroll, Is.Not.Null);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelBody.rectTransform);
            Canvas.ForceUpdateCanvases();
            Assert.That(
                panelBody.rectTransform.rect.height,
                Is.GreaterThan(panelBodyScroll.viewport.rect.height),
                "The complete license must overflow into a genuinely scrollable " +
                "viewport rather than clip or shrink.");
            Assert.That(
                panelBodyScroll.verticalNormalizedPosition,
                Is.EqualTo(1f).Within(0.001f));
            panelBodyScroll.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
            Assert.That(
                panelBodyScroll.verticalNormalizedPosition,
                Is.EqualTo(0f).Within(0.001f));
            privacyButton.onClick.Invoke();
            Assert.That(panelTitle.text, Is.EqualTo("Privacy"));
            Assert.That(
                panelBody.text,
                Is.EqualTo(
                    "This Development Flight does not ask for an account, " +
                    "collect gameplay progress, open web links, or offer " +
                    "purchases."));
            Assert.That(
                panelBody.text,
                Does.Not.Contain("SIL OPEN FONT LICENSE"),
                "Privacy must replace the prior Credits and license copy.");
            Assert.That(
                panelBody.text,
                Does.Not.Contain("Apache License"),
                "Privacy must replace the prior Apache dependency license copy.");
            panelBodyScroll.verticalNormalizedPosition = 0f;
            creditsButton.onClick.Invoke();
            Assert.That(
                panelBodyScroll.verticalNormalizedPosition,
                Is.EqualTo(1f).Within(0.001f),
                "Reopening Credits must reset the full license to its beginning.");
            closeButton.onClick.Invoke();
            Assert.That(localPanel.activeSelf, Is.False);

            var firstShutdown = bootstrap.ShutdownAsync().AsTask();
            var secondShutdown = bootstrap.ShutdownAsync().AsTask();
            Assert.That(secondShutdown, Is.SameAs(firstShutdown));
            yield return WaitForTask(firstShutdown, "Task 5 bootstrap shutdown");

            UnityEngine.Object.Destroy(bootstrap.gameObject);
            yield return WaitForCondition(
                () => FindAllBootstraps().Length == 0,
                "Task 5 bootstrap destruction");
            GameBootstrap.CompositionFactory = null;
            yield return RestorePriorTestScene();

            Assert.That(FindAllBootstraps(), Is.Empty);
            Assert.That(SceneManager.GetActiveScene().name, Is.Not.EqualTo("Frontend"));
            Assert.That(SceneManager.GetActiveScene().name, Is.Not.EqualTo("Boot"));
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator WaitForFrontendStartup()
        {
            return WaitForCondition(
                () =>
                {
                    var bootstraps = FindAllBootstraps();
                    return SceneManager.GetActiveScene().name == "Frontend" &&
                           bootstraps.Length == 1 &&
                           bootstraps[0].LastStartupReport != null;
                },
                "Boot-to-Frontend startup");
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

        private static TextAsset GetLicense(
            FrontendController controller,
            string fieldName)
        {
            var field = typeof(FrontendController).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return field.GetValue(controller) as TextAsset;
        }

        private static IEnumerator ShutdownAndDestroyAllBootstraps()
        {
            var bootstraps = FindAllBootstraps();
            var shutdownTasks = bootstraps
                .Where(bootstrap => bootstrap != null)
                .Select(bootstrap => bootstrap.ShutdownAsync().AsTask())
                .Distinct()
                .ToArray();
            foreach (var shutdownTask in shutdownTasks)
            {
                yield return WaitForTask(shutdownTask, "bootstrap teardown shutdown");
            }

            DestroyAllBootstraps();
            yield return WaitForCondition(
                () => FindAllBootstraps().Length == 0,
                "bootstrap teardown destruction");
        }

        private IEnumerator RestorePriorTestScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() &&
                activeScene.isLoaded &&
                activeScene.name != "Boot" &&
                activeScene.name != "Frontend")
            {
                yield break;
            }

            var restoreTarget = ResolveRestoreTarget();
            if (!string.IsNullOrEmpty(restoreTarget))
            {
                var restore = SceneManager.LoadSceneAsync(
                    restoreTarget,
                    LoadSceneMode.Single);
                Assert.That(restore, Is.Not.Null);
                yield return restore;
                yield break;
            }

            var recoveryName = "Task5PlayModeRecovery_" + Guid.NewGuid().ToString("N");
            var recoveryScene = SceneManager.CreateScene(recoveryName);
            Assert.That(SceneManager.SetActiveScene(recoveryScene), Is.True);

            foreach (var sceneName in new[] { "Frontend", "Boot" })
            {
                var scene = SceneManager.GetSceneByName(sceneName);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                var unload = SceneManager.UnloadSceneAsync(scene);
                Assert.That(unload, Is.Not.Null);
                yield return unload;
            }
        }

        private string ResolveRestoreTarget()
        {
            if (IsCoreLaunchScene(m_PreviousSceneName, m_PreviousScenePath))
            {
                return null;
            }

            if (!string.IsNullOrEmpty(m_PreviousScenePath) &&
                Application.CanStreamedLevelBeLoaded(m_PreviousScenePath))
            {
                return m_PreviousScenePath;
            }

            if (!string.IsNullOrEmpty(m_PreviousSceneName) &&
                Application.CanStreamedLevelBeLoaded(m_PreviousSceneName))
            {
                return m_PreviousSceneName;
            }

            return null;
        }

        private static bool IsCoreLaunchScene(string sceneName, string scenePath)
        {
            if (string.Equals(sceneName, "Boot", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    sceneName,
                    "Frontend",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var normalizedPath = (scenePath ?? string.Empty)
                .Replace('\\', '/');
            return normalizedPath.EndsWith(
                       "/Boot.unity",
                       StringComparison.OrdinalIgnoreCase) ||
                   normalizedPath.EndsWith(
                       "/Frontend.unity",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerator WaitForTask(Task task, string operation)
        {
            yield return WaitForCondition(() => task.IsCompleted, operation);
            task.GetAwaiter().GetResult();
        }

        private static IEnumerator WaitForCondition(
            Func<bool> condition,
            string operation)
        {
            const int maximumFrames = 600;
            for (var frame = 0; frame < maximumFrames; frame++)
            {
                if (condition())
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail(
                $"{operation} did not complete within {maximumFrames} frames.");
        }

        private static GameBootstrap[] FindAllBootstraps()
        {
            return UnityEngine.Object.FindObjectsByType<GameBootstrap>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        }

        private static void DestroyAllBootstraps()
        {
            foreach (var bootstrap in FindAllBootstraps())
            {
                if (bootstrap != null)
                {
                    UnityEngine.Object.Destroy(bootstrap.gameObject);
                }
            }
        }
    }
}
