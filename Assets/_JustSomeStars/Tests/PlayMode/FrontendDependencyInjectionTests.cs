using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Input;
using JustSomeStars.Runtime.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class FrontendDependencyInjectionTests
    {
        private GameObject m_Root;
        private string m_TestRoot;
        private readonly List<UnityEngine.Object> m_OwnedObjects =
            new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            m_TestRoot = Path.Combine(
                Path.GetTempPath(),
                "JssTask6FrontendDependencyTests",
                Guid.NewGuid().ToString("N"));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (m_Root != null)
            {
                UnityEngine.Object.DestroyImmediate(m_Root);
            }

            foreach (var owned in m_OwnedObjects)
            {
                if (owned != null)
                {
                    UnityEngine.Object.DestroyImmediate(owned);
                }
            }

            m_OwnedObjects.Clear();
            if (Directory.Exists(m_TestRoot))
            {
                Directory.Delete(m_TestRoot, recursive: true);
            }

            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() &&
                activeScene.isLoaded &&
                (activeScene.name == "Frontend" || activeScene.name == "Boot"))
            {
                var recovery = SceneManager.CreateScene(
                    "Task6FrontendDependencyRecovery_" +
                    Guid.NewGuid().ToString("N"));
                Assert.That(SceneManager.SetActiveScene(recovery), Is.True);
                var unload = SceneManager.UnloadSceneAsync(activeScene);
                Assert.That(unload, Is.Not.Null);
                yield return unload;
            }
        }

        [Test]
        public async Task Controller_RemainsNonInteractiveUntilExactDependenciesArePushed()
        {
            var settings = await CreateSettings();
            var actions = Own(ScriptableObject.CreateInstance<InputActionAsset>());
            var input = new InputRouter(actions, settings);
            var dependencies = new FrontendDependencies(settings, input);
            var otherSettings = await CreateSettings();
            var otherActions = Own(ScriptableObject.CreateInstance<InputActionAsset>());
            var otherDependencies = new FrontendDependencies(
                otherSettings,
                new InputRouter(otherActions, otherSettings));

            m_Root = new GameObject("Task6FrontendControllerFixture");
            m_Root.SetActive(false);
            var view = m_Root.AddComponent<FakeFrontendView>();
            var lifecycle = m_Root.AddComponent<FakeFrontendLifecycle>();
            var settingsPanel = m_Root.AddComponent<FakeFrontendSettingsPanel>();
            var controller = m_Root.AddComponent<FrontendController>();
            SetField(controller, "m_ViewSource", view);
            SetField(controller, "m_LifecycleSource", lifecycle);
            SetField(controller, "m_SettingsPanelSource", settingsPanel);
            SetField(controller, "m_LiberationSansLicense", Own(new TextAsset("OFL")));
            SetField(controller, "m_ApacheLicense", Own(new TextAsset("Apache")));

            m_Root.SetActive(true);
            Assert.That(controller.IsConfigured, Is.False);
            Assert.That(view.SettingsSubscriberCount, Is.EqualTo(0));
            Assert.That(view.VersionPresentationCount, Is.EqualTo(0));

            controller.Configure(dependencies);

            Assert.That(controller.IsConfigured, Is.True);
            Assert.That(controller.Dependencies, Is.SameAs(dependencies));
            Assert.That(settingsPanel.Dependencies, Is.SameAs(dependencies));
            Assert.That(settingsPanel.ConfigureCount, Is.EqualTo(1));
            Assert.That(view.SettingsSubscriberCount, Is.EqualTo(1));
            Assert.That(lifecycle.BackSubscriberCount, Is.EqualTo(1));
            Assert.That(view.VersionPresentationCount, Is.EqualTo(1));

            controller.Configure(dependencies);
            Assert.That(settingsPanel.ConfigureCount, Is.EqualTo(1));
            Assert.That(view.SettingsSubscriberCount, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() =>
                controller.Configure(otherDependencies));

            view.RaiseSettings();
            Assert.That(view.PanelTitle, Is.EqualTo("Settings"));
            Assert.That(
                view.PanelBody,
                Is.EqualTo(
                    "Saved locally on this device. Nothing leaves this screen."));
            Assert.That(settingsPanel.ShowCount, Is.EqualTo(1));

            view.RaiseCredits();
            Assert.That(settingsPanel.HideCount, Is.EqualTo(1));
            Assert.That(view.PanelTitle, Is.EqualTo("Credits & Licenses"));

            controller.Release(dependencies);
            Assert.That(controller.IsConfigured, Is.False);
            Assert.That(settingsPanel.IsConfigured, Is.False);
            Assert.That(view.SettingsSubscriberCount, Is.EqualTo(0));
            Assert.That(lifecycle.BackSubscriberCount, Is.EqualTo(0));

            await input.ShutdownAsync();
            await settings.ShutdownAsync();
            await otherSettings.ShutdownAsync();
        }

        [Test]
        public async Task SettingsPanel_ChangesPersistAndExternalChangesRenderOnce()
        {
            var settings = await CreateSettings();
            var actions = Own(ScriptableObject.CreateInstance<InputActionAsset>());
            var input = new InputRouter(actions, settings);
            var dependencies = new FrontendDependencies(settings, input);

            m_Root = new GameObject("Task6SettingsPanelFixture");
            m_Root.SetActive(false);
            var panel = m_Root.AddComponent<FrontendSettingsPanel>();
            var scrollObject = new GameObject(
                "SettingsScroll",
                typeof(RectTransform),
                typeof(ScrollRect));
            scrollObject.transform.SetParent(m_Root.transform, false);
            var scroll = scrollObject.GetComponent<ScrollRect>();
            var viewport = new GameObject(
                "Viewport",
                typeof(RectTransform)).GetComponent<RectTransform>();
            viewport.SetParent(scrollObject.transform, false);
            var content = new GameObject(
                "Content",
                typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            scroll.viewport = viewport;
            scroll.content = content;
            var decrease = new Button[FrontendSettingsPanel.ControlCount];
            var increase = new Button[FrontendSettingsPanel.ControlCount];
            var values = new TMP_Text[FrontendSettingsPanel.ControlCount];
            for (var index = 0; index < FrontendSettingsPanel.ControlCount; index++)
            {
                decrease[index] = CreateButton($"Decrease{index}");
                increase[index] = CreateButton($"Increase{index}");
                values[index] = CreateText($"Value{index}");
            }

            SetField(panel, "m_Root", m_Root);
            SetField(panel, "m_ScrollRect", scroll);
            SetField(panel, "m_DecreaseButtons", decrease);
            SetField(panel, "m_IncreaseButtons", increase);
            SetField(panel, "m_ValueLabels", values);

            Assert.That(panel.IsReady, Is.True);
            panel.Configure(dependencies);
            panel.Show();

            increase[0].onClick.Invoke();
            decrease[3].onClick.Invoke();
            Assert.That(settings.Current.PilotingAssist, Is.EqualTo(AssistLevel.Ace));
            Assert.That(settings.Current.CaptionsEnabled, Is.False);
            Assert.That(values[0].text, Is.EqualTo("Ace"));
            Assert.That(values[3].text, Is.EqualTo("Off"));
            Assert.That(File.Exists(Path.Combine(
                m_TestRoot,
                "jss-settings-v1.json")), Is.True);

            var external = settings.Current;
            external.LeftHandedControls = true;
            Assert.That(settings.Apply(external), Is.True);
            Assert.That(values[18].text, Is.EqualTo("Left"));

            m_Root.SetActive(false);
            m_Root.SetActive(true);
            decrease[0].onClick.Invoke();
            Assert.That(
                settings.Current.PilotingAssist,
                Is.EqualTo(AssistLevel.Balanced),
                "Re-enabling the panel must not duplicate button callbacks.");

            panel.Release(dependencies);
            await input.ShutdownAsync();
            await settings.ShutdownAsync();
        }

        [Test]
        public void UnityLifecycle_HasNoOwnedInputActionOrPollingLoop()
        {
            Assert.That(
                typeof(UnityFrontendLifecycle).GetMethod(
                    "Update",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic),
                Is.Null);
            Assert.That(
                typeof(UnityFrontendLifecycle).GetFields(
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic)
                    .Select(field => field.FieldType),
                Has.None.EqualTo(typeof(InputAction)));
            Assert.That(
                typeof(UnityFrontendLifecycle).GetMethod(
                    "Configure",
                    BindingFlags.Instance | BindingFlags.Public),
                Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator CompositionTransition_ReleasesBeforeShutdownAndRebindsOnceAfterReload()
        {
            var settings = new SettingsService(Path.Combine(
                m_TestRoot,
                "jss-settings-v1.json"));
            var projectActions = InputSystem.actions;
            Assert.That(projectActions, Is.Not.Null);
            var actions = Own(UnityEngine.Object.Instantiate(projectActions));
            var input = new InputRouter(actions, settings);
            var dependencies = new FrontendDependencies(settings, input);
            var transition = new UnitySceneTransition(dependencies);
            var coordinator = new ServiceStartupCoordinator();
            var composition = new GameBootstrapComposition(
                new[]
                {
                    new GameServiceRegistration(GameServiceRole.Settings, settings),
                    new GameServiceRegistration(
                        GameServiceRole.LocalSave,
                        new AvailableService()),
                    new GameServiceRegistration(GameServiceRole.Input, input),
                    new GameServiceRegistration(
                        GameServiceRole.ContentCatalogue,
                        new AvailableService()),
                    new GameServiceRegistration(
                        GameServiceRole.ModeController,
                        new AvailableService()),
                },
                transition);

            var startup = coordinator.StartupAsync(
                composition,
                CancellationToken.None).AsTask();
            yield return WaitForTask(startup, "initial Frontend route");
            Assert.That(startup.Result.IsSuccessful, Is.True);

            var firstController = FindOnly<FrontendController>();
            var firstLifecycle = FindOnly<UnityFrontendLifecycle>();
            var firstSettingsPanel = FindOnly<FrontendSettingsPanel>();
            Assert.That(firstController.Dependencies, Is.SameAs(dependencies));
            Assert.That(firstLifecycle.Dependencies, Is.SameAs(dependencies));
            Assert.That(firstSettingsPanel.Dependencies, Is.SameAs(dependencies));
            Assert.That(GetSubscriberCount(input, "BackRequested"), Is.EqualTo(1));
            Assert.That(
                GetSubscriberCount(settings, "SettingsChanged"),
                Is.EqualTo(2),
                "Only InputRouter and the current Frontend settings panel may " +
                "observe settings.");

            var reload = transition.RouteAsync(
                "Frontend",
                CancellationToken.None).AsTask();
            yield return WaitForTask(reload, "Frontend reload");

            var currentController = FindOnly<FrontendController>();
            var currentLifecycle = FindOnly<UnityFrontendLifecycle>();
            var currentSettingsPanel = FindOnly<FrontendSettingsPanel>();
            Assert.That(ReferenceEquals(currentController, firstController), Is.False);
            Assert.That(ReferenceEquals(currentLifecycle, firstLifecycle), Is.False);
            Assert.That(
                ReferenceEquals(currentSettingsPanel, firstSettingsPanel),
                Is.False);
            Assert.That(currentController.Dependencies, Is.SameAs(dependencies));
            Assert.That(currentLifecycle.Dependencies, Is.SameAs(dependencies));
            Assert.That(currentSettingsPanel.Dependencies, Is.SameAs(dependencies));
            Assert.That(GetSubscriberCount(input, "BackRequested"), Is.EqualTo(1));
            Assert.That(GetSubscriberCount(settings, "SettingsChanged"), Is.EqualTo(2));

            var shutdown = coordinator.ShutdownAsync().AsTask();
            yield return WaitForTask(shutdown, "composition shutdown");

            Assert.That(currentController.IsConfigured, Is.False);
            Assert.That(currentLifecycle.IsConfigured, Is.False);
            Assert.That(currentSettingsPanel.IsConfigured, Is.False);
            Assert.That(currentController.Dependencies, Is.Null);
            Assert.That(currentLifecycle.Dependencies, Is.Null);
            Assert.That(currentSettingsPanel.Dependencies, Is.Null);
            Assert.That(GetSubscriberCount(input, "BackRequested"), Is.EqualTo(0));
            Assert.That(GetSubscriberCount(settings, "SettingsChanged"), Is.EqualTo(0));
            Assert.That(input.IsInitialized, Is.False);
            Assert.That(settings.IsInitialized, Is.False);
        }

        private async Task<SettingsService> CreateSettings()
        {
            var settings = new SettingsService(Path.Combine(
                m_TestRoot,
                "jss-settings-v1.json"));
            var result = await settings.InitializeAsync(CancellationToken.None);
            Assert.That(result.IsAvailable, Is.True);
            return settings;
        }

        private static T FindOnly<T>() where T : UnityEngine.Object
        {
            var matches = UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(matches, Has.Length.EqualTo(1), typeof(T).Name);
            return matches[0];
        }

        private static int GetSubscriberCount(object source, string eventName)
        {
            var eventField = source.GetType().GetField(
                eventName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(eventField, Is.Not.Null, eventName);
            return (eventField.GetValue(source) as Delegate)?
                .GetInvocationList().Length ?? 0;
        }

        private static IEnumerator WaitForTask(Task task, string operation)
        {
            var deadline = Time.realtimeSinceStartup + 20f;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(task.IsCompleted, Is.True, operation);
            if (task.IsFaulted)
            {
                throw task.Exception?.InnerException ?? task.Exception;
            }

            Assert.That(task.IsCanceled, Is.False, operation);
        }

        private T Own<T>(T instance) where T : UnityEngine.Object
        {
            m_OwnedObjects.Add(instance);
            return instance;
        }

        private Button CreateButton(string name)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(Button));
            child.transform.SetParent(m_Root.transform, false);
            return child.GetComponent<Button>();
        }

        private TMP_Text CreateText(string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(m_Root.transform, false);
            return child.AddComponent<TextMeshProUGUI>();
        }

        private static void SetField(
            object target,
            string fieldName,
            object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private sealed class FakeFrontendView : MonoBehaviour, IFrontendView
        {
            private Action m_SettingsRequested;

            public bool IsReady => true;

            public int VersionPresentationCount { get; private set; }

            public int SettingsSubscriberCount =>
                m_SettingsRequested?.GetInvocationList().Length ?? 0;

            public string PanelTitle { get; private set; }

            public string PanelBody { get; private set; }

            public event Action ContinueRequested;

            public event Action SettingsRequested
            {
                add => m_SettingsRequested += value;
                remove => m_SettingsRequested -= value;
            }

            public event Action CreditsRequested;

            public event Action PrivacyRequested;

            public event Action CloseRequested;

            public void PresentVersion(string versionText)
            {
                _ = versionText;
                VersionPresentationCount++;
            }

            public void PresentContinue(bool interactable, string explanation)
            {
                _ = interactable;
                _ = explanation;
            }

            public void ShowPanel(string title, string body)
            {
                PanelTitle = title;
                PanelBody = body;
            }

            public void HidePanel()
            {
            }

            public void RaiseSettings()
            {
                m_SettingsRequested?.Invoke();
            }

            public void RaiseCredits()
            {
                CreditsRequested?.Invoke();
            }
        }

        private sealed class FakeFrontendLifecycle :
            MonoBehaviour,
            IFrontendLifecycle
        {
            private Action m_BackRequested;

            public event Action BackRequested
            {
                add => m_BackRequested += value;
                remove => m_BackRequested -= value;
            }

            public int BackSubscriberCount =>
                m_BackRequested?.GetInvocationList().Length ?? 0;

            public void RequestExit()
            {
            }
        }

        private sealed class FakeFrontendSettingsPanel :
            MonoBehaviour,
            IFrontendSettingsPanel
        {
            public bool IsReady => true;

            public bool IsConfigured => Dependencies != null;

            public FrontendDependencies Dependencies { get; private set; }

            public int ConfigureCount { get; private set; }

            public int ShowCount { get; private set; }

            public int HideCount { get; private set; }

            public void Configure(FrontendDependencies dependencies)
            {
                if (ReferenceEquals(Dependencies, dependencies))
                {
                    return;
                }

                Dependencies = dependencies;
                ConfigureCount++;
            }

            public void Release(FrontendDependencies dependencies)
            {
                if (ReferenceEquals(Dependencies, dependencies))
                {
                    Dependencies = null;
                }
            }

            public void Show()
            {
                ShowCount++;
            }

            public void Hide()
            {
                HideCount++;
            }
        }

        private sealed class AvailableService : IGameService
        {
            public ValueTask<StartupResult> InitializeAsync(
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new ValueTask<StartupResult>(StartupResult.Available());
            }

            public ValueTask ShutdownAsync()
            {
                return default;
            }
        }
    }
}
