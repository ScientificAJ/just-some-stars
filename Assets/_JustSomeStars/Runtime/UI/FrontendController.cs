using UnityEngine;

namespace JustSomeStars.Runtime.UI
{
    [DisallowMultipleComponent]
    public sealed class FrontendController : MonoBehaviour
    {
        private const string ContinueExplanation =
            "Gameplay is not in this flight yet.";
        private const string SettingsTitle = "Settings";
        private const string SettingsBody =
            "Saved locally on this device. Nothing leaves this screen.";
        private const string CreditsTitle = "Credits & Licenses";
        private const string CreditsBodyPrefix =
            "Just Some Stars is being built by ScientificAJ. This Development " +
            "Flight contains a launch screen, not finished gameplay.\n\n" +
            "Liberation Sans\n\n";
        private const string ApacheCreditsPrefix =
            "\n\nAndroid open-source components\n\n" +
            "This Android build includes AndroidX, Kotlin, Kotlin coroutines, " +
            "JetBrains annotations, and Guava components distributed under " +
            "the Apache License 2.0. The complete license follows.\n\n" +
            "Apache License 2.0\n\n";
        private const string PrivacyTitle = "Privacy";
        private const string PrivacyBody =
            "This Development Flight does not ask for an account, collect " +
            "gameplay progress, open web links, or offer purchases.";
        private const string MissingBindingsError =
            "[JSS Frontend] FrontendController requires view, lifecycle, and " +
            "settings panel sources.";
        private const string MissingLiberationLicenseError =
            "[JSS Frontend] FrontendController requires a non-empty " +
            "Liberation Sans license asset.";
        private const string MissingApacheLicenseError =
            "[JSS Frontend] FrontendController requires a non-empty " +
            "Apache License 2.0 asset.";

        [SerializeField]
        private MonoBehaviour m_ViewSource;

        [SerializeField]
        private MonoBehaviour m_LifecycleSource;

        [SerializeField]
        private MonoBehaviour m_SettingsPanelSource;

        [SerializeField]
        private TextAsset m_LiberationSansLicense;

        [SerializeField]
        private TextAsset m_ApacheLicense;

        private IFrontendView m_View;
        private IFrontendLifecycle m_Lifecycle;
        private IFrontendSettingsPanel m_SettingsPanel;
        private bool m_IsBound;
        private bool m_IsPanelVisible;

        public bool IsConfigured => Dependencies != null;

        public FrontendDependencies Dependencies { get; private set; }

        private void Awake()
        {
            m_View = m_ViewSource as IFrontendView;
            m_Lifecycle = m_LifecycleSource as IFrontendLifecycle;
            m_SettingsPanel = m_SettingsPanelSource as IFrontendSettingsPanel;
            if (m_View == null || !m_View.IsReady || m_Lifecycle == null ||
                m_SettingsPanel == null || !m_SettingsPanel.IsReady)
            {
                Debug.LogError(MissingBindingsError, this);
                enabled = false;
                return;
            }

            if (m_LiberationSansLicense == null ||
                string.IsNullOrEmpty(m_LiberationSansLicense.text))
            {
                Debug.LogError(MissingLiberationLicenseError, this);
                enabled = false;
                return;
            }

            if (m_ApacheLicense == null ||
                string.IsNullOrEmpty(m_ApacheLicense.text))
            {
                Debug.LogError(MissingApacheLicenseError, this);
                enabled = false;
                return;
            }

        }

        public void Configure(FrontendDependencies dependencies)
        {
            if (dependencies == null)
            {
                throw new System.ArgumentNullException(nameof(dependencies));
            }

            if (ReferenceEquals(Dependencies, dependencies))
            {
                return;
            }

            if (Dependencies != null)
            {
                throw new System.InvalidOperationException(
                    "FrontendController cannot be rebound to another composition.");
            }

            if (m_View == null || m_Lifecycle == null || m_SettingsPanel == null)
            {
                throw new System.InvalidOperationException(
                    "FrontendController cannot be configured with invalid bindings.");
            }

            m_SettingsPanel.Configure(dependencies);
            Dependencies = dependencies;
            m_View.PresentVersion($"Version {Application.version}");
            m_View.PresentContinue(
                interactable: false,
                ContinueExplanation);
            Bind();
        }

        internal void Release(FrontendDependencies dependencies)
        {
            if (dependencies == null)
            {
                throw new System.ArgumentNullException(nameof(dependencies));
            }

            if (Dependencies == null)
            {
                return;
            }

            if (!ReferenceEquals(Dependencies, dependencies))
            {
                throw new System.InvalidOperationException(
                    "FrontendController can only be released by its owning " +
                    "composition.");
            }

            Unbind();
            HidePanel();
            m_SettingsPanel.Release(dependencies);
            Dependencies = null;
        }

        private void OnEnable()
        {
            Bind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void OnApplicationPause(bool isPaused)
        {
            // The Frontend owns no background work. Its current local panel and
            // Task 4's persistent bootstrap lifecycle remain untouched.
            _ = isPaused;
        }

        private void Bind()
        {
            if (m_IsBound || Dependencies == null || m_View == null ||
                m_Lifecycle == null || !isActiveAndEnabled)
            {
                return;
            }

            m_View.ContinueRequested += HandleContinueRequested;
            m_View.SettingsRequested += HandleSettingsRequested;
            m_View.CreditsRequested += HandleCreditsRequested;
            m_View.PrivacyRequested += HandlePrivacyRequested;
            m_View.CloseRequested += HandleCloseRequested;
            m_Lifecycle.BackRequested += HandleBackRequested;
            m_IsBound = true;
        }

        private void Unbind()
        {
            if (!m_IsBound)
            {
                return;
            }

            m_View.ContinueRequested -= HandleContinueRequested;
            m_View.SettingsRequested -= HandleSettingsRequested;
            m_View.CreditsRequested -= HandleCreditsRequested;
            m_View.PrivacyRequested -= HandlePrivacyRequested;
            m_View.CloseRequested -= HandleCloseRequested;
            m_Lifecycle.BackRequested -= HandleBackRequested;
            m_IsBound = false;
        }

        private void HandleContinueRequested()
        {
            // Continue is intentionally disabled in this truthful skeleton.
        }

        private void HandleSettingsRequested()
        {
            ShowPanel(SettingsTitle, SettingsBody);
            m_SettingsPanel.Show();
        }

        private void HandleCreditsRequested()
        {
            ShowPanel(
                CreditsTitle,
                CreditsBodyPrefix +
                m_LiberationSansLicense.text +
                ApacheCreditsPrefix +
                m_ApacheLicense.text);
        }

        private void HandlePrivacyRequested()
        {
            ShowPanel(PrivacyTitle, PrivacyBody);
        }

        private void HandleCloseRequested()
        {
            HidePanel();
        }

        private void HandleBackRequested()
        {
            if (m_IsPanelVisible)
            {
                HidePanel();
                return;
            }

            m_Lifecycle.RequestExit();
        }

        private void ShowPanel(string title, string body)
        {
            if (!string.Equals(title, SettingsTitle, System.StringComparison.Ordinal))
            {
                m_SettingsPanel.Hide();
            }

            m_View.ShowPanel(title, body);
            m_IsPanelVisible = true;
        }

        private void HidePanel()
        {
            if (!m_IsPanelVisible)
            {
                return;
            }

            m_SettingsPanel.Hide();
            m_View.HidePanel();
            m_IsPanelVisible = false;
        }
    }
}
