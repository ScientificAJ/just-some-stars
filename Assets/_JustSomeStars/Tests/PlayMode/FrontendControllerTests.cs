using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Atlas;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Input;
using JustSomeStars.Runtime.Saving;
using JustSomeStars.Runtime.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace JustSomeStars.Tests.PlayMode
{
    public sealed class FrontendControllerTests
    {
        private const string TestLiberationLicenseText =
            "SIL OPEN FONT LICENSE test fixture.\n" +
            "Every line must remain visible.\n";
        private const string TestApacheLicenseText =
            "Apache License Version 2.0 test fixture.\n" +
            "Every Android dependency line must remain visible.\n";
        private const string CreditsPrefix =
            "Just Some Stars is created by ScientificAJ.\n\n" +
            "Liberation Sans and Noto Sans\n\n";
        private const string ApacheCreditsPrefix =
            "\n\nAndroid open-source components\n\n" +
            "This Android build includes AndroidX, Kotlin, Kotlin coroutines, " +
            "JetBrains annotations, and Guava components distributed under " +
            "the Apache License 2.0. The complete license follows.\n\n" +
            "Apache License 2.0\n\n";
        private const string MissingLiberationLicenseError =
            "[JSS Frontend] FrontendController requires a non-empty " +
            "Liberation Sans license asset.";
        private const string MissingApacheLicenseError =
            "[JSS Frontend] FrontendController requires a non-empty " +
            "Apache License 2.0 asset.";

        private GameObject m_TestRoot;
        private readonly List<TextAsset> m_TestLicenses =
            new List<TextAsset>();
        private readonly List<UnityEngine.Object> m_OwnedObjects =
            new List<UnityEngine.Object>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameBootstrap.CompositionFactory = null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameBootstrap.CompositionFactory = null;
            if (m_TestRoot != null)
            {
                UnityEngine.Object.Destroy(m_TestRoot);
            }

            foreach (var license in m_TestLicenses)
            {
                if (license != null)
                {
                    UnityEngine.Object.Destroy(license);
                }
            }

            m_TestLicenses.Clear();
            foreach (var owned in m_OwnedObjects)
            {
                if (owned != null)
                {
                    UnityEngine.Object.Destroy(owned);
                }
            }

            m_OwnedObjects.Clear();

            yield return null;
        }

        [Test]
        public void Awake_PresentsLocalizedVersionAndTruthfulNoSaveLaunchState()
        {
            var fixture = CreateController();

            Assert.That(
                fixture.View.VersionText,
                Is.EqualTo($"Version {Application.version}"));
            Assert.That(fixture.View.VersionPresentationCount, Is.EqualTo(1));
            Assert.That(fixture.View.NewGameInteractable, Is.True);
            Assert.That(
                fixture.View.NewGameExplanation,
                Is.EqualTo("Begin at the observatory"));
            Assert.That(fixture.View.ContinueInteractable, Is.False);
            Assert.That(
                fixture.View.ContinueExplanation,
                Is.EqualTo("No journey saved yet"));
            Assert.That(fixture.View.LaunchState, Is.EqualTo(
                FrontendContinueState.NoSave));

            fixture.View.RaiseContinue();

            Assert.That(fixture.View.ShowPanelCount, Is.EqualTo(0));
            Assert.That(fixture.Lifecycle.ExitRequestCount, Is.EqualTo(0));
        }

        [Test]
        public void LocalControls_OpenOnlyTheirTruthfulInAppPanels()
        {
            var fixture = CreateController();

            fixture.View.RaiseSettings();
            Assert.That(fixture.View.PanelTitle, Is.EqualTo("Settings"));
            Assert.That(
                fixture.View.PanelBody,
                Is.EqualTo(
                    "Device settings are saved locally and are never included " +
                    "in cloud backup."));
            Assert.That(fixture.SettingsPanel.ShowCount, Is.EqualTo(1));

            fixture.View.RaiseCredits();
            Assert.That(
                fixture.View.PanelTitle,
                Is.EqualTo("Credits & Licenses"));
            Assert.That(fixture.SettingsPanel.HideCount, Is.EqualTo(1));
            Assert.That(
                fixture.View.PanelBody,
                Is.EqualTo(
                    CreditsPrefix +
                    TestLiberationLicenseText +
                    ApacheCreditsPrefix +
                    TestApacheLicenseText));
            Assert.That(
                fixture.View.PanelBody.EndsWith(
                    TestApacheLicenseText,
                    StringComparison.Ordinal),
                Is.True,
                "Credits must end with the complete Apache license verbatim, " +
                "including its final newline.");
            Assert.That(
                fixture.View.PanelBody.IndexOf(
                    TestLiberationLicenseText,
                    StringComparison.Ordinal),
                Is.LessThan(fixture.View.PanelBody.IndexOf(
                    ApacheCreditsPrefix,
                    StringComparison.Ordinal)),
                "The Apache dependency notice must remain user-readable after " +
                "the complete Liberation Sans OFL.");

            fixture.View.RaisePrivacy();
            Assert.That(fixture.View.PanelTitle, Is.EqualTo("Privacy"));
            Assert.That(
                fixture.View.PanelBody,
                Is.EqualTo(
                    "An account is optional. Progress stays on this device unless a " +
                    "grown-up chooses private Google cloud backup. Photos and device " +
                    "settings always stay local. Cloud data can be exported, signed " +
                    "out, or deleted from Settings. Google sign-in data is never used " +
                    "for advertising. Optional store purchases never sell story, " +
                    "science, or accessibility."));
            Assert.That(fixture.View.ShowPanelCount, Is.EqualTo(3));
            Assert.That(fixture.Lifecycle.ExitRequestCount, Is.EqualTo(0));
        }

        [Test]
        public void Back_ClosesAnOpenPanelThenRequestsNormalExitAtRoot()
        {
            var fixture = CreateController();
            fixture.View.RaisePrivacy();

            fixture.Lifecycle.RaiseBack();

            Assert.That(fixture.View.HidePanelCount, Is.EqualTo(1));
            Assert.That(fixture.View.IsPanelVisible, Is.False);
            Assert.That(fixture.Lifecycle.ExitRequestCount, Is.EqualTo(0));

            fixture.Lifecycle.RaiseBack();

            Assert.That(fixture.View.HidePanelCount, Is.EqualTo(1));
            Assert.That(fixture.Lifecycle.ExitRequestCount, Is.EqualTo(1));
        }

        [Test]
        public void CloseControl_HidesOpenPanelWithoutRequestingExit()
        {
            var fixture = CreateController();
            fixture.View.RaiseCredits();

            fixture.View.RaiseClose();

            Assert.That(fixture.View.HidePanelCount, Is.EqualTo(1));
            Assert.That(fixture.View.IsPanelVisible, Is.False);
            Assert.That(fixture.Lifecycle.ExitRequestCount, Is.EqualTo(0));
        }

        [Test]
        public void Reenable_DoesNotDuplicateViewOrBackBindings()
        {
            var fixture = CreateController();

            fixture.Controller.enabled = false;
            fixture.Controller.enabled = true;
            fixture.Controller.enabled = false;
            fixture.Controller.enabled = true;

            Assert.That(fixture.View.ContinueSubscriberCount, Is.EqualTo(1));
            Assert.That(fixture.View.NewGameSubscriberCount, Is.EqualTo(1));
            Assert.That(fixture.View.SettingsSubscriberCount, Is.EqualTo(1));
            Assert.That(fixture.View.CreditsSubscriberCount, Is.EqualTo(1));
            Assert.That(fixture.View.PrivacySubscriberCount, Is.EqualTo(1));
            Assert.That(fixture.View.CloseSubscriberCount, Is.EqualTo(1));
            Assert.That(fixture.Lifecycle.BackSubscriberCount, Is.EqualTo(1));

            fixture.View.RaiseSettings();
            fixture.Lifecycle.RaiseBack();

            Assert.That(fixture.View.ShowPanelCount, Is.EqualTo(1));
            Assert.That(fixture.View.HidePanelCount, Is.EqualTo(1));
            Assert.That(fixture.Lifecycle.ExitRequestCount, Is.EqualTo(0));
        }

        [Test]
        public void LaunchPresentation_CoversValidRecoveredCorruptAndUnavailableStates()
        {
            var save = GameSave.CreateNew("task28-launch", 28L);
            AssertLaunchState(
                new LoadSaveResult(LoadSaveStatus.LoadedPrimary, save, string.Empty),
                canContinue: true,
                FrontendContinueState.Ready,
                expectedInteractable: true,
                "Return to Mirra");
            AssertLaunchState(
                new LoadSaveResult(
                    LoadSaveStatus.RecoveredBackup,
                    save,
                    string.Empty),
                canContinue: true,
                FrontendContinueState.RecoveredBackup,
                expectedInteractable: true,
                "Recovered backup · Return to Mirra");
            AssertLaunchState(
                new LoadSaveResult(
                    LoadSaveStatus.Unreadable,
                    null,
                    string.Empty),
                canContinue: true,
                FrontendContinueState.Unreadable,
                expectedInteractable: false,
                "Save needs recovery before continuing");
            AssertLaunchState(
                new LoadSaveResult(
                    LoadSaveStatus.StorageUnavailable,
                    null,
                    string.Empty),
                canContinue: true,
                FrontendContinueState.StorageUnavailable,
                expectedInteractable: false,
                "Local saves are temporarily unavailable");
            AssertLaunchState(
                new LoadSaveResult(LoadSaveStatus.LoadedPrimary, save, string.Empty),
                canContinue: false,
                FrontendContinueState.ContentUnavailable,
                expectedInteractable: false,
                "That checkpoint is not installed");
        }

        private void AssertLaunchState(
            LoadSaveResult result,
            bool canContinue,
            FrontendContinueState expectedState,
            bool expectedInteractable,
            string expectedExplanation)
        {
            var fixture = CreateController(result, canContinue);
            Assert.That(fixture.View.LaunchState, Is.EqualTo(expectedState));
            Assert.That(
                fixture.View.ContinueInteractable,
                Is.EqualTo(expectedInteractable));
            Assert.That(
                fixture.View.ContinueExplanation,
                Is.EqualTo(expectedExplanation));
            DestroyCurrentRootImmediately();
        }

        [Test]
        public void ApplicationPauseCallback_PreservesOnlyLocalPanelAndBindings()
        {
            var fixture = CreateController();
            fixture.View.RaisePrivacy();

            InvokeApplicationPause(fixture.Controller, isPaused: true);
            InvokeApplicationPause(fixture.Controller, isPaused: false);

            Assert.That(fixture.View.IsPanelVisible, Is.True);
            Assert.That(fixture.View.PanelTitle, Is.EqualTo("Privacy"));
            Assert.That(fixture.View.ShowPanelCount, Is.EqualTo(1));
            Assert.That(fixture.View.HidePanelCount, Is.EqualTo(0));
            Assert.That(fixture.View.VersionPresentationCount, Is.EqualTo(1));
            Assert.That(fixture.View.PrivacySubscriberCount, Is.EqualTo(1));
            Assert.That(fixture.Lifecycle.BackSubscriberCount, Is.EqualTo(1));
            Assert.That(fixture.Lifecycle.ExitRequestCount, Is.EqualTo(0));
        }

        [Test]
        public void Awake_WithMissingBindings_DisablesControllerAndReportsError()
        {
            const string expectedError =
                "[JSS Frontend] FrontendController requires view, lifecycle, and " +
                "settings panel sources.";
            m_TestRoot = new GameObject("UnboundFrontendController");
            m_TestRoot.SetActive(false);
            var controller = m_TestRoot.AddComponent<FrontendController>();
            LogAssert.Expect(LogType.Error, expectedError);

            m_TestRoot.SetActive(true);

            Assert.That(controller.enabled, Is.False);
        }

        [Test]
        public void Awake_WithoutEitherRequiredLicense_FailsClosedIndependently()
        {
            var validLiberation = CreateLicense(TestLiberationLicenseText);
            var validApache = CreateLicense(TestApacheLicenseText);

            var fixture = CreateControllerWithLicenses(
                liberationLicense: null,
                apacheLicense: validApache);
            AssertLicenseFailure(fixture, MissingLiberationLicenseError);
            DestroyCurrentRootImmediately();

            fixture = CreateControllerWithLicenses(
                validLiberation,
                apacheLicense: null);
            AssertLicenseFailure(fixture, MissingApacheLicenseError);
        }

        [Test]
        public void Awake_WithEitherEmptyRequiredLicense_FailsClosedIndependently()
        {
            var emptyLiberation = CreateLicense(string.Empty);
            var validLiberation = CreateLicense(TestLiberationLicenseText);
            var emptyApache = CreateLicense(string.Empty);
            var validApache = CreateLicense(TestApacheLicenseText);

            var fixture = CreateControllerWithLicenses(
                emptyLiberation,
                validApache);
            AssertLicenseFailure(fixture, MissingLiberationLicenseError);
            DestroyCurrentRootImmediately();

            fixture = CreateControllerWithLicenses(
                validLiberation,
                emptyApache);
            AssertLicenseFailure(fixture, MissingApacheLicenseError);
        }

        [UnityTest]
        public IEnumerator Awake_WithMalformedRealView_FailsClosedWithoutThrowing()
        {
            const string controllerError =
                "[JSS Frontend] FrontendController requires view, lifecycle, and " +
                "settings panel sources.";
            const string viewError =
                "[JSS Frontend] FrontendView has incomplete scene bindings.";
            m_TestRoot = new GameObject("MalformedFrontendFixture");
            m_TestRoot.SetActive(false);
            var controller = m_TestRoot.AddComponent<FrontendController>();
            var lifecycle = m_TestRoot.AddComponent<UnityFrontendLifecycle>();
            var viewObject = new GameObject("MalformedView");
            viewObject.transform.SetParent(m_TestRoot.transform, false);
            var view = viewObject.AddComponent<FrontendView>();
            SetPrivateField(controller, "m_ViewSource", view);
            SetPrivateField(controller, "m_LifecycleSource", lifecycle);
            LogAssert.Expect(LogType.Error, controllerError);
            LogAssert.Expect(LogType.Error, viewError);

            m_TestRoot.SetActive(true);
            yield return null;

            Assert.That(controller.enabled, Is.False);
            Assert.That(view.enabled, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void UnityLifecycle_InjectedInputBackIsIdempotentAcrossReenable()
        {
            Assert.That(
                typeof(UnityFrontendLifecycle).GetMethod(
                    "Update",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic),
                Is.Null,
                "Android Back must be event-driven, not polled each frame.");

            m_TestRoot = new GameObject("FrontendLifecycleFixture");
            m_TestRoot.SetActive(false);
            var lifecycle = m_TestRoot.AddComponent<UnityFrontendLifecycle>();
            var settings = CreateSettingsService();
            settings.InitializeAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            var actions = CreateCanonicalInputAsset();
            var input = new InputRouter(actions, settings);
            var inputResult = input.InitializeAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            Assert.That(inputResult.IsAvailable, Is.True);
            var dependencies = new FrontendDependencies(settings, input);
            var backRequestCount = 0;
            lifecycle.BackRequested += () => backRequestCount++;
            Keyboard keyboard = null;
            var previousBackgroundBehavior =
                InputSystem.settings.backgroundBehavior;
            var previousEditorInputBehavior =
                InputSystem.settings.editorInputBehaviorInPlayMode;

            try
            {
                // Batch PlayMode does not guarantee application focus. Match
                // the Input System package's own integration-test fixture so
                // its synthetic keyboard cannot be disabled by focus policy.
                InputSystem.settings.backgroundBehavior =
                    InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode =
                    InputSettings.EditorInputBehaviorInPlayMode
                        .AllDeviceInputAlwaysGoesToGameView;
                keyboard = InputSystem.AddDevice<Keyboard>();
                m_TestRoot.SetActive(true);
                lifecycle.Configure(dependencies);

                var backAction = actions.FindAction(
                    "UI/Cancel",
                    throwIfNotFound: true);
                Assert.That(keyboard.enabled, Is.True);
                Assert.That(backAction, Is.Not.Null);
                Assert.That(backAction.enabled, Is.True);
                Assert.That(backAction.controls, Does.Contain(keyboard.escapeKey));

                m_TestRoot.SetActive(false);
                m_TestRoot.SetActive(true);
                m_TestRoot.SetActive(false);
                m_TestRoot.SetActive(true);
                Assert.That(
                    actions.FindAction("UI/Cancel", throwIfNotFound: true),
                    Is.SameAs(backAction));
                Assert.That(backAction.enabled, Is.True);
                Assert.That(backAction.controls, Does.Contain(keyboard.escapeKey));

                InputSystem.QueueStateEvent(
                    keyboard,
                    new KeyboardState(Key.Escape));
                InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();

                Assert.That(backRequestCount, Is.EqualTo(1));

                InputSystem.QueueStateEvent(
                    keyboard,
                    new KeyboardState(Key.Escape));
                InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();

                Assert.That(backRequestCount, Is.EqualTo(2));
            }
            finally
            {
                try
                {
                    m_TestRoot.SetActive(false);
                    if (keyboard != null && keyboard.added)
                    {
                        InputSystem.RemoveDevice(keyboard);
                    }

                    input.ShutdownAsync().GetAwaiter().GetResult();
                    settings.ShutdownAsync().GetAwaiter().GetResult();
                }
                finally
                {
                    try
                    {
                        InputSystem.settings.editorInputBehaviorInPlayMode =
                            previousEditorInputBehavior;
                    }
                    finally
                    {
                        InputSystem.settings.backgroundBehavior =
                            previousBackgroundBehavior;
                    }
                }
            }
        }

        private ControllerFixture CreateController(
            LoadSaveResult loadResult = null,
            bool canContinue = true)
        {
            var fixture = CreateControllerWithLicenses(
                CreateLicense(TestLiberationLicenseText),
                CreateLicense(TestApacheLicenseText),
                loadResult,
                canContinue);
            m_TestRoot.SetActive(true);
            fixture.Controller.Configure(fixture.Dependencies);
            return fixture;
        }

        private ControllerFixture CreateControllerWithLicenses(
            TextAsset liberationLicense,
            TextAsset apacheLicense,
            LoadSaveResult loadResult = null,
            bool canContinue = true)
        {
            m_TestRoot = new GameObject("FrontendControllerFixture");
            m_TestRoot.SetActive(false);
            var view = m_TestRoot.AddComponent<FakeFrontendView>();
            var lifecycle = m_TestRoot.AddComponent<FakeFrontendLifecycle>();
            var settingsPanel =
                m_TestRoot.AddComponent<FakeFrontendSettingsPanel>();
            var controller = m_TestRoot.AddComponent<FrontendController>();
            SetPrivateField(controller, "m_ViewSource", view);
            SetPrivateField(controller, "m_LifecycleSource", lifecycle);
            SetPrivateField(controller, "m_SettingsPanelSource", settingsPanel);
            SetPrivateFieldIfPresent(
                controller,
                "m_LiberationSansLicense",
                liberationLicense);
            SetPrivateFieldIfPresent(
                controller,
                "m_ApacheLicense",
                apacheLicense);
            var english = Own(ScriptableObject.CreateInstance<
                LocalizedEnglishCatalog>());
            english.Configure(Task28English.CreateEntries());
            SetPrivateField(controller, "m_English", english);

            return new ControllerFixture(
                controller,
                view,
                lifecycle,
                settingsPanel,
                CreateDependencies(loadResult, canContinue));
        }

        private FrontendDependencies CreateDependencies(
            LoadSaveResult loadResult = null,
            bool canContinue = true)
        {
            var settings = CreateSettingsService();
            var actions = Own(ScriptableObject.CreateInstance<InputActionAsset>());
            return new FrontendDependencies(
                settings,
                new InputRouter(actions, settings),
                saves: new FakeSaveService(loadResult ?? new LoadSaveResult(
                    LoadSaveStatus.Missing,
                    null,
                    string.Empty)),
                startNewGame: token => default,
                continueGame: (save, token) => default,
                canContinue: save => canContinue,
                describeCheckpoint: save => "Mirra");
        }

        private SettingsService CreateSettingsService()
        {
            return new SettingsService(Path.Combine(
                Path.GetTempPath(),
                "JssTask6FrontendControllerTests",
                Guid.NewGuid().ToString("N"),
                "jss-settings-v1.json"));
        }

        private InputActionAsset CreateCanonicalInputAsset()
        {
            var asset = Own(ScriptableObject.CreateInstance<InputActionAsset>());
            var ui = asset.AddActionMap("UI");
            foreach (var actionName in new[]
                     {
                         "Navigate",
                         "Submit",
                         "Cancel",
                         "Point",
                         "Click",
                         "RightClick",
                         "MiddleClick",
                         "ScrollWheel",
                         "TrackedDevicePosition",
                         "TrackedDeviceOrientation",
                     })
            {
                ui.AddAction(actionName, InputActionType.Button);
            }

            ui.FindAction("Cancel", throwIfNotFound: true)
                .AddBinding("<Keyboard>/escape");
            foreach (var mapName in new[] { "Surface", "Flight", "Lens" })
            {
                var map = asset.AddActionMap(mapName);
                foreach (var actionName in new[]
                         {
                             "Move",
                             "Look",
                             "Primary",
                             "Secondary",
                             "Pause",
                             "Lens",
                             "PhotoMode",
                             "Recenter",
                         })
                {
                    map.AddAction(actionName, InputActionType.Button);
                }
            }

            return asset;
        }

        private T Own<T>(T instance) where T : UnityEngine.Object
        {
            m_OwnedObjects.Add(instance);
            return instance;
        }

        private TextAsset CreateLicense(string text)
        {
            var license = new TextAsset(text);
            m_TestLicenses.Add(license);
            return license;
        }

        private void AssertLicenseFailure(
            ControllerFixture fixture,
            string expectedError)
        {
            LogAssert.Expect(LogType.Error, expectedError);
            m_TestRoot.SetActive(true);

            Assert.That(fixture.Controller.enabled, Is.False);
            Assert.That(fixture.View.VersionPresentationCount, Is.EqualTo(0));
            Assert.That(fixture.View.SettingsSubscriberCount, Is.EqualTo(0));
            Assert.That(fixture.View.CreditsSubscriberCount, Is.EqualTo(0));
            Assert.That(fixture.Lifecycle.BackSubscriberCount, Is.EqualTo(0));
        }

        private void DestroyCurrentRootImmediately()
        {
            Assert.That(m_TestRoot, Is.Not.Null);
            UnityEngine.Object.DestroyImmediate(m_TestRoot);
            m_TestRoot = null;
        }

        private static void SetPrivateField(
            FrontendController controller,
            string fieldName,
            UnityEngine.Object value)
        {
            var field = typeof(FrontendController).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing serialized field {fieldName}.");
            field.SetValue(controller, value);
        }

        private static void SetPrivateFieldIfPresent(
            FrontendController controller,
            string fieldName,
            UnityEngine.Object value)
        {
            var field = typeof(FrontendController).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(controller, value);
        }

        private static void InvokeApplicationPause(
            FrontendController controller,
            bool isPaused)
        {
            var method = typeof(FrontendController).GetMethod(
                "OnApplicationPause",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(controller, new object[] { isPaused });
        }

        private sealed class ControllerFixture
        {
            public ControllerFixture(
                FrontendController controller,
                FakeFrontendView view,
                FakeFrontendLifecycle lifecycle,
                FakeFrontendSettingsPanel settingsPanel,
                FrontendDependencies dependencies)
            {
                Controller = controller;
                View = view;
                Lifecycle = lifecycle;
                SettingsPanel = settingsPanel;
                Dependencies = dependencies;
            }

            public FrontendController Controller { get; }

            public FakeFrontendView View { get; }

            public FakeFrontendLifecycle Lifecycle { get; }

            public FakeFrontendSettingsPanel SettingsPanel { get; }

            public FrontendDependencies Dependencies { get; }
        }

        public sealed class FakeFrontendView :
            MonoBehaviour,
            IFrontendView,
            IFrontendLaunchView
        {
            private Action m_NewGameRequested;
            private Action m_ContinueRequested;
            private Action m_SettingsRequested;
            private Action m_CreditsRequested;
            private Action m_PrivacyRequested;
            private Action m_CloseRequested;

            public bool IsReady => true;

            public event Action ContinueRequested
            {
                add => m_ContinueRequested += value;
                remove => m_ContinueRequested -= value;
            }

            public event Action NewGameRequested
            {
                add => m_NewGameRequested += value;
                remove => m_NewGameRequested -= value;
            }

            public event Action SettingsRequested
            {
                add => m_SettingsRequested += value;
                remove => m_SettingsRequested -= value;
            }

            public event Action CreditsRequested
            {
                add => m_CreditsRequested += value;
                remove => m_CreditsRequested -= value;
            }

            public event Action PrivacyRequested
            {
                add => m_PrivacyRequested += value;
                remove => m_PrivacyRequested -= value;
            }

            public event Action CloseRequested
            {
                add => m_CloseRequested += value;
                remove => m_CloseRequested -= value;
            }

            public string VersionText { get; private set; }

            public int VersionPresentationCount { get; private set; }

            public bool ContinueInteractable { get; private set; }

            public bool NewGameInteractable { get; private set; }

            public string NewGameExplanation { get; private set; }

            public string ContinueState { get; private set; }

            public FrontendContinueState LaunchState { get; private set; }

            public string ContinueExplanation { get; private set; }

            public bool IsPanelVisible { get; private set; }

            public string PanelTitle { get; private set; }

            public string PanelBody { get; private set; }

            public int ShowPanelCount { get; private set; }

            public int HidePanelCount { get; private set; }

            public int SettingsSubscriberCount => SubscriberCount(m_SettingsRequested);

            public int ContinueSubscriberCount => SubscriberCount(m_ContinueRequested);

            public int NewGameSubscriberCount => SubscriberCount(m_NewGameRequested);

            public int CreditsSubscriberCount => SubscriberCount(m_CreditsRequested);

            public int PrivacySubscriberCount => SubscriberCount(m_PrivacyRequested);

            public int CloseSubscriberCount => SubscriberCount(m_CloseRequested);

            public void PresentVersion(string versionText)
            {
                VersionText = versionText;
                VersionPresentationCount++;
            }

            public void PresentContinue(bool interactable, string explanation)
            {
                ContinueInteractable = interactable;
                ContinueExplanation = explanation;
            }

            public void PresentLocalizedChrome(LocalizedEnglishCatalog english)
            {
                Assert.That(english, Is.Not.Null);
            }

            public void PresentLaunch(FrontendLaunchPresentation presentation)
            {
                NewGameInteractable = presentation.NewGameInteractable;
                NewGameExplanation = presentation.NewGameExplanation;
                ContinueInteractable = presentation.ContinueInteractable;
                ContinueState = presentation.ContinueState;
                ContinueExplanation = presentation.ContinueExplanation;
                LaunchState = presentation.State;
            }

            public void ShowPanel(string title, string body)
            {
                IsPanelVisible = true;
                PanelTitle = title;
                PanelBody = body;
                ShowPanelCount++;
            }

            public void HidePanel()
            {
                IsPanelVisible = false;
                HidePanelCount++;
            }

            public void RaiseContinue()
            {
                m_ContinueRequested?.Invoke();
            }

            public void RaiseNewGame()
            {
                m_NewGameRequested?.Invoke();
            }

            public void RaiseSettings()
            {
                m_SettingsRequested?.Invoke();
            }

            public void RaiseCredits()
            {
                m_CreditsRequested?.Invoke();
            }

            public void RaisePrivacy()
            {
                m_PrivacyRequested?.Invoke();
            }

            public void RaiseClose()
            {
                m_CloseRequested?.Invoke();
            }

            private static int SubscriberCount(Action action)
            {
                return action?.GetInvocationList().Length ?? 0;
            }
        }

        public sealed class FakeFrontendLifecycle :
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

            public int ExitRequestCount { get; private set; }

            public void RequestExit()
            {
                ExitRequestCount++;
            }

            public void RaiseBack()
            {
                m_BackRequested?.Invoke();
            }
        }

        public sealed class FakeFrontendSettingsPanel :
            MonoBehaviour,
            IFrontendSettingsPanel
        {
            public bool IsReady => true;

            public bool IsConfigured => Dependencies != null;

            public FrontendDependencies Dependencies { get; private set; }

            public void SetLocalization(LocalizedEnglishCatalog english)
            {
                Assert.That(english, Is.Not.Null);
            }

            public int ShowCount { get; private set; }

            public int HideCount { get; private set; }

            public void Configure(FrontendDependencies dependencies)
            {
                Dependencies = dependencies;
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

        private sealed class FakeSaveService : ISaveService
        {
            private readonly LoadSaveResult m_Result;

            public FakeSaveService(LoadSaveResult result)
            {
                m_Result = result ?? throw new ArgumentNullException(nameof(result));
            }

            public ValueTask<StartupResult> InitializeAsync(
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new ValueTask<StartupResult>(StartupResult.Available());
            }

            public ValueTask ShutdownAsync() => default;

            public ValueTask<LoadSaveResult> LoadAsync(
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new ValueTask<LoadSaveResult>(m_Result.Copy());
            }

            public ValueTask SaveCheckpointAsync(
                GameSave save,
                CancellationToken cancellationToken) => default;

            public ValueTask<LoadSaveResult> RecoverAsync(
                CancellationToken cancellationToken) =>
                LoadAsync(cancellationToken);

            public GameSave Merge(GameSave local, GameSave cloud) =>
                local?.Copy() ?? cloud?.Copy();
        }
    }
}
