using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Input;
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
    public sealed class ApplicationLaunchIntegrationTests
    {
        private const string CreditsPrefix =
            "Just Some Stars is created by ScientificAJ.\n\n" +
            "Liberation Sans and Noto Sans\n\n";
        private const string ApacheCreditsPrefix =
            "\n\nAndroid open-source components\n\n" +
            "This Android build includes AndroidX, Kotlin, Kotlin coroutines, " +
            "JetBrains annotations, and Guava components distributed under " +
            "the Apache License 2.0. The complete license follows.\n\n" +
            "Apache License 2.0\n\n";

        private string m_PreviousSceneName;
        private string m_PreviousScenePath;
        private string m_SettingsPath;
        private bool m_SettingsFileExisted;
        private byte[] m_PreviousSettingsBytes;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var activeScene = SceneManager.GetActiveScene();
            m_PreviousSceneName = activeScene.name;
            m_PreviousScenePath = activeScene.path;
            m_SettingsPath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "Library/JustSomeStars/Local/jss-settings-v1.json"));
            m_SettingsFileExisted = File.Exists(m_SettingsPath);
            m_PreviousSettingsBytes = m_SettingsFileExisted
                ? File.ReadAllBytes(m_SettingsPath)
                : null;
            GameBootstrap.CompositionFactory = null;
            yield return ShutdownAndDestroyAllBootstraps();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return ShutdownAndDestroyAllBootstraps();
            GameBootstrap.CompositionFactory = null;
            yield return RestorePriorTestScene();
            RestoreSettingsFile();
            Assert.That(FindAllBootstraps(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator RealBoot_WithApplicationInstaller_ActivatesFrontendOnce()
        {
            ApplicationBootstrapInstaller.Install();
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
                Is.EqualTo(7));

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
            var settingsControls = FindDescendant(
                view.transform,
                "SettingsControls");
            var settingsScroll = settingsControls.GetComponent<ScrollRect>();
            var captionsDecrease = FindDescendant(
                    view.transform,
                    "Decrease03")
                .GetComponent<Button>();
            var captionsValue = FindDescendant(
                    view.transform,
                    "Value03")
                .GetComponent<TextMeshProUGUI>();
            var continueButton = FindDescendant(view.transform, "ContinueButton")
                .GetComponent<Button>();
            var newGameButton = FindDescendant(view.transform, "NewGameButton")
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
            Assert.That(newGameButton.interactable, Is.True);
            Assert.That(continueButton.interactable, Is.False);
            continueButton.onClick.Invoke();
            Assert.That(localPanel.activeSelf, Is.False);

            settingsButton.onClick.Invoke();
            Assert.That(localPanel.activeSelf, Is.True);
            Assert.That(panelTitle.text, Is.EqualTo("Settings"));
            Assert.That(settingsControls.activeSelf, Is.True);
            Assert.That(panelBodyScroll.gameObject.activeSelf, Is.False);
            Assert.That(
                settingsScroll.verticalNormalizedPosition,
                Is.EqualTo(1f).Within(0.001f));
            var controller = UnityEngine.Object.FindFirstObjectByType<
                FrontendController>(FindObjectsInactive.Include);
            var lifecycle = UnityEngine.Object.FindFirstObjectByType<
                UnityFrontendLifecycle>(FindObjectsInactive.Include);
            var settingsPanel = UnityEngine.Object.FindFirstObjectByType<
                FrontendSettingsPanel>(FindObjectsInactive.Include);
            Assert.That(controller, Is.Not.Null);
            Assert.That(lifecycle, Is.Not.Null);
            Assert.That(settingsPanel, Is.Not.Null);
            Assert.That(controller.Dependencies, Is.Not.Null);
            Assert.That(lifecycle.Dependencies,
                Is.SameAs(controller.Dependencies));
            Assert.That(settingsPanel.Dependencies,
                Is.SameAs(controller.Dependencies));
            Assert.That(controller.Dependencies.Input, Is.TypeOf<InputRouter>());
            Assert.That(
                controller.Dependencies.Input.Actions,
                Is.SameAs(inputModule.actionsAsset));

            var captionsBefore =
                controller.Dependencies.Settings.Current.CaptionsEnabled;
            captionsDecrease.onClick.Invoke();
            Assert.That(
                controller.Dependencies.Settings.Current.CaptionsEnabled,
                Is.EqualTo(!captionsBefore));
            Assert.That(
                captionsValue.text,
                Is.EqualTo(captionsBefore ? "Off" : "On"));
            Assert.That(File.Exists(m_SettingsPath), Is.True);

            creditsButton.onClick.Invoke();
            Assert.That(panelTitle.text, Is.EqualTo("Credits & Licenses"));
            Assert.That(settingsControls.activeSelf, Is.False);
            Assert.That(panelBodyScroll.gameObject.activeSelf, Is.True);
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
            var expectedCredits =
                CreditsPrefix +
                liberationLicense.text +
                ApacheCreditsPrefix +
                apacheLicense.text;
            var visibleCredits = NormalizeVisibleText(panelBody.text);
            Assert.That(
                visibleCredits,
                Is.EqualTo(NormalizeVisibleText(expectedCredits)),
                "Presentation-only line wrapping, rich-text headings, and " +
                "license de-indentation must preserve every visible word.");
            Assert.That(
                visibleCredits.IndexOf(
                    NormalizeVisibleText(liberationLicense.text),
                    System.StringComparison.Ordinal),
                Is.LessThan(visibleCredits.IndexOf(
                    NormalizeVisibleText(ApacheCreditsPrefix),
                    System.StringComparison.Ordinal)),
                "The Android dependency notice and Apache license must follow " +
                "the complete Liberation Sans OFL.");
            Assert.That(
                visibleCredits.EndsWith(
                    NormalizeVisibleText(apacheLicense.text),
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
                NormalizeVisibleText(panelBody.text),
                Is.EqualTo(
                    "An account is optional. Progress stays on this device unless a " +
                    "grown-up chooses private Google cloud backup. Photos and device " +
                    "settings always stay local. Cloud data can be exported, signed " +
                    "out, or deleted from Settings. Google sign-in data is never used " +
                    "for advertising. Optional store purchases never sell story, " +
                    "science, or accessibility."));
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
            yield return WaitForCondition(
                () => !localPanel.activeSelf,
                "Frontend panel exit animation");
            Assert.That(localPanel.activeSelf, Is.False);

            var firstShutdown = bootstrap.ShutdownAsync().AsTask();
            var secondShutdown = bootstrap.ShutdownAsync().AsTask();
            Assert.That(secondShutdown, Is.SameAs(firstShutdown));
            yield return WaitForTask(firstShutdown, "Task 5 bootstrap shutdown");

            Assert.That(
                controller.IsConfigured,
                Is.False,
                "Bootstrap shutdown must detach the Frontend controller before " +
                "composition-owned services stop.");
            Assert.That(lifecycle.IsConfigured, Is.False);
            Assert.That(settingsPanel.IsConfigured, Is.False);
            Assert.That(controller.Dependencies, Is.Null);
            Assert.That(lifecycle.Dependencies, Is.Null);
            Assert.That(settingsPanel.Dependencies, Is.Null);

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

        private static string NormalizeVisibleText(string value)
        {
            var withoutRichText = Regex.Replace(
                value ?? string.Empty,
                "<[^>]+>",
                string.Empty);
            return Regex.Replace(withoutRichText, @"\s+", " ").Trim();
        }

        private void RestoreSettingsFile()
        {
            if (m_SettingsFileExisted)
            {
                var parent = Path.GetDirectoryName(m_SettingsPath);
                if (!string.IsNullOrEmpty(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                File.WriteAllBytes(m_SettingsPath, m_PreviousSettingsBytes);
                return;
            }

            if (File.Exists(m_SettingsPath))
            {
                File.Delete(m_SettingsPath);
            }
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
            var timeout = TimeSpan.FromSeconds(10);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                if (condition())
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail(
                $"{operation} did not complete within {timeout.TotalSeconds:0} " +
                "seconds.");
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
