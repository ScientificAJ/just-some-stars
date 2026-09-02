using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Atlas;
using JustSomeStars.Runtime.Saving;
using UnityEngine;

namespace JustSomeStars.Runtime.UI
{
    [DisallowMultipleComponent]
    public sealed class FrontendController : MonoBehaviour
    {
        private const string MissingBindingsError =
            "[JSS Frontend] FrontendController requires view, lifecycle, and " +
            "settings panel sources.";
        private const string MissingLocalizationError =
            "[JSS Frontend] FrontendController requires the complete English " +
            "localization catalog.";
        private const string MissingLiberationLicenseError =
            "[JSS Frontend] FrontendController requires a non-empty " +
            "Liberation Sans license asset.";
        private const string MissingApacheLicenseError =
            "[JSS Frontend] FrontendController requires a non-empty " +
            "Apache License 2.0 asset.";

        [SerializeField] private MonoBehaviour m_ViewSource;
        [SerializeField] private MonoBehaviour m_LifecycleSource;
        [SerializeField] private MonoBehaviour m_SettingsPanelSource;
        [SerializeField] private LocalizedEnglishCatalog m_English;
        [SerializeField] private TextAsset m_LiberationSansLicense;
        [SerializeField] private TextAsset m_ApacheLicense;
        [SerializeField] private AccessibilityApplier m_AccessibilityApplier;

        private IFrontendView m_View;
        private IFrontendLaunchView m_LaunchView;
        private IFrontendLifecycle m_Lifecycle;
        private IFrontendSettingsPanel m_SettingsPanel;
        private CancellationTokenSource m_Lifetime;
        private GameSave m_ContinueSave;
        private bool m_IsBound;
        private bool m_IsPanelVisible;
        private bool m_LaunchInFlight;

        public bool IsConfigured => Dependencies != null;
        public FrontendDependencies Dependencies { get; private set; }

        private void Awake()
        {
            m_View = m_ViewSource as IFrontendView;
            m_LaunchView = m_ViewSource as IFrontendLaunchView;
            m_Lifecycle = m_LifecycleSource as IFrontendLifecycle;
            m_SettingsPanel = m_SettingsPanelSource as IFrontendSettingsPanel;
            if (m_View == null || m_LaunchView == null || !m_View.IsReady ||
                m_Lifecycle == null || m_SettingsPanel == null ||
                !m_SettingsPanel.IsReady)
            {
                DisableWithError(MissingBindingsError);
                return;
            }

            try
            {
                m_English?.ValidateOrThrow();
            }
            catch (InvalidOperationException)
            {
                DisableWithError(MissingLocalizationError);
                return;
            }
            if (m_English == null)
            {
                DisableWithError(MissingLocalizationError);
                return;
            }
            if (m_LiberationSansLicense == null ||
                string.IsNullOrEmpty(m_LiberationSansLicense.text))
            {
                DisableWithError(MissingLiberationLicenseError);
                return;
            }
            if (m_ApacheLicense == null ||
                string.IsNullOrEmpty(m_ApacheLicense.text))
            {
                DisableWithError(MissingApacheLicenseError);
            }
        }

        public void Configure(FrontendDependencies dependencies)
        {
            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }
            if (ReferenceEquals(Dependencies, dependencies))
            {
                return;
            }
            if (Dependencies != null)
            {
                throw new InvalidOperationException(
                    "FrontendController cannot be rebound to another composition.");
            }
            if (m_View == null || m_LaunchView == null || m_Lifecycle == null ||
                m_SettingsPanel == null || m_English == null)
            {
                throw new InvalidOperationException(
                    "FrontendController cannot be configured with invalid bindings.");
            }

            var settingsConfigured = false;
            var accessibilityConfigured = false;
            try
            {
                m_SettingsPanel.SetLocalization(m_English);
                m_SettingsPanel.Configure(dependencies);
                settingsConfigured = true;
                if (m_AccessibilityApplier != null)
                {
                    m_AccessibilityApplier.Configure(dependencies.Settings);
                    accessibilityConfigured = true;
                }
                Dependencies = dependencies;
                m_Lifetime = new CancellationTokenSource();
                m_LaunchView.PresentLocalizedChrome(m_English);
                m_View.PresentVersion(Task28English.Format(
                    m_English,
                    Task28English.FrontendVersion,
                    Application.version));
                Bind();
                PresentLoadingState();
                _ = RefreshLaunchStateAsync(dependencies, m_Lifetime.Token);
            }
            catch
            {
                Unbind();
                m_Lifetime?.Cancel();
                m_Lifetime?.Dispose();
                m_Lifetime = null;
                Dependencies = null;
                if (accessibilityConfigured)
                {
                    m_AccessibilityApplier.Release();
                }
                if (settingsConfigured)
                {
                    m_SettingsPanel.Release(dependencies);
                }
                throw;
            }
        }

        internal void Release(FrontendDependencies dependencies)
        {
            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }
            if (Dependencies == null)
            {
                return;
            }
            if (!ReferenceEquals(Dependencies, dependencies))
            {
                throw new InvalidOperationException(
                    "FrontendController can only be released by its owning composition.");
            }

            Unbind();
            HidePanel();
            m_SettingsPanel.Release(dependencies);
            m_AccessibilityApplier?.Release();
            m_Lifetime?.Cancel();
            m_Lifetime?.Dispose();
            m_Lifetime = null;
            m_ContinueSave = null;
            Dependencies = null;
        }

        private void OnEnable() => Bind();
        private void OnDisable() => Unbind();

        private void OnDestroy()
        {
            Unbind();
            m_Lifetime?.Cancel();
            m_Lifetime?.Dispose();
            m_Lifetime = null;
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (!isPaused && Dependencies != null && m_Lifetime != null &&
                !m_LaunchInFlight)
            {
                _ = RefreshLaunchStateAsync(Dependencies, m_Lifetime.Token);
            }
        }

        private void Bind()
        {
            if (m_IsBound || Dependencies == null || m_View == null ||
                m_LaunchView == null || m_Lifecycle == null || !isActiveAndEnabled)
            {
                return;
            }
            m_LaunchView.NewGameRequested += HandleNewGameRequested;
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
            m_LaunchView.NewGameRequested -= HandleNewGameRequested;
            m_View.ContinueRequested -= HandleContinueRequested;
            m_View.SettingsRequested -= HandleSettingsRequested;
            m_View.CreditsRequested -= HandleCreditsRequested;
            m_View.PrivacyRequested -= HandlePrivacyRequested;
            m_View.CloseRequested -= HandleCloseRequested;
            m_Lifecycle.BackRequested -= HandleBackRequested;
            m_IsBound = false;
        }

        private void HandleNewGameRequested()
        {
            if (Dependencies?.StartNewGame == null || m_LaunchInFlight ||
                m_Lifetime == null)
            {
                return;
            }
            _ = RunLaunchAsync(
                token => Dependencies.StartNewGame(token),
                m_Lifetime.Token);
        }

        private void HandleContinueRequested()
        {
            if (Dependencies?.ContinueGame == null || m_ContinueSave == null ||
                m_LaunchInFlight || m_Lifetime == null)
            {
                return;
            }
            var checkpoint = m_ContinueSave.Copy();
            _ = RunLaunchAsync(
                token => Dependencies.ContinueGame(checkpoint, token),
                m_Lifetime.Token);
        }

        private async Task RunLaunchAsync(
            Func<CancellationToken, ValueTask> route,
            CancellationToken cancellationToken)
        {
            m_LaunchInFlight = true;
            m_ContinueSave = null;
            m_LaunchView.PresentLaunch(new FrontendLaunchPresentation(
                false,
                m_English.Resolve(Task28English.FrontendNewGameLoading),
                false,
                m_English.Resolve(Task28English.FrontendContinueLoading),
                m_English.Resolve(Task28English.FrontendContinueLoading),
                FrontendContinueState.Loading));
            try
            {
                await route(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                m_LaunchInFlight = false;
                if (Dependencies != null && !cancellationToken.IsCancellationRequested)
                {
                    await RefreshLaunchStateAsync(Dependencies, cancellationToken);
                }
            }
        }

        private async Task RefreshLaunchStateAsync(
            FrontendDependencies dependencies,
            CancellationToken cancellationToken)
        {
            if (dependencies == null || !ReferenceEquals(Dependencies, dependencies) ||
                m_LaunchInFlight)
            {
                return;
            }

            LoadSaveResult loaded;
            try
            {
                loaded = dependencies.Saves == null
                    ? null
                    : await dependencies.Saves.LoadAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is InvalidOperationException)
            {
                Debug.LogWarning(
                    "[JSS Frontend] Local launch state could not be refreshed: " +
                    exception.GetType().Name,
                    this);
                PresentLaunchState(
                    FrontendContinueState.StorageUnavailable,
                    null,
                    dependencies);
                return;
            }

            if (!ReferenceEquals(Dependencies, dependencies) ||
                cancellationToken.IsCancellationRequested)
            {
                return;
            }
            if (loaded == null)
            {
                PresentLaunchState(
                    FrontendContinueState.StorageUnavailable,
                    null,
                    dependencies);
                return;
            }

            var state = loaded.Status switch
            {
                LoadSaveStatus.Missing => FrontendContinueState.NoSave,
                LoadSaveStatus.LoadedPrimary => FrontendContinueState.Ready,
                LoadSaveStatus.RecoveredBackup =>
                    FrontendContinueState.RecoveredBackup,
                LoadSaveStatus.Unreadable => FrontendContinueState.Unreadable,
                LoadSaveStatus.StorageUnavailable =>
                    FrontendContinueState.StorageUnavailable,
                _ => FrontendContinueState.StorageUnavailable,
            };
            var save = loaded.HasSave ? loaded.Save : null;
            if ((state == FrontendContinueState.Ready ||
                 state == FrontendContinueState.RecoveredBackup) &&
                (dependencies.ContinueGame == null ||
                 dependencies.CanContinue?.Invoke(save) != true))
            {
                state = FrontendContinueState.ContentUnavailable;
            }
            PresentLaunchState(state, save, dependencies);
        }

        private void PresentLaunchState(
            FrontendContinueState state,
            GameSave save,
            FrontendDependencies dependencies)
        {
            var canContinue = (state == FrontendContinueState.Ready ||
                state == FrontendContinueState.RecoveredBackup) && save != null;
            m_ContinueSave = canContinue ? save.Copy() : null;
            var checkpoint = save != null
                ? dependencies.DescribeCheckpoint?.Invoke(save)
                : null;
            checkpoint = LocalizeCheckpoint(checkpoint);

            var explanationKey = state switch
            {
                FrontendContinueState.NoSave => Task28English.FrontendContinueNoSave,
                FrontendContinueState.Ready => Task28English.FrontendContinueReady,
                FrontendContinueState.RecoveredBackup =>
                    Task28English.FrontendContinueRecovered,
                FrontendContinueState.Unreadable =>
                    Task28English.FrontendContinueUnreadable,
                FrontendContinueState.StorageUnavailable =>
                    Task28English.FrontendContinueStorageUnavailable,
                FrontendContinueState.ContentUnavailable =>
                    Task28English.FrontendContinueContentUnavailable,
                _ => Task28English.FrontendContinueLoading,
            };
            var stateKey = state switch
            {
                FrontendContinueState.Ready => "frontend.state.ready",
                FrontendContinueState.RecoveredBackup => "frontend.state.recovered",
                FrontendContinueState.Unreadable => "frontend.state.recovery",
                FrontendContinueState.StorageUnavailable => "frontend.state.offline",
                FrontendContinueState.ContentUnavailable => "frontend.state.unavailable",
                _ => "frontend.state.new",
            };
            var explanation = canContinue
                ? Task28English.Format(m_English, explanationKey, checkpoint)
                : m_English.Resolve(explanationKey);
            m_LaunchView.PresentLaunch(new FrontendLaunchPresentation(
                dependencies.StartNewGame != null && dependencies.Saves != null,
                m_English.Resolve(Task28English.FrontendNewGameReady),
                canContinue,
                m_English.Resolve(stateKey),
                explanation,
                state));
        }

        private string LocalizeCheckpoint(string checkpoint)
        {
            if (string.IsNullOrWhiteSpace(checkpoint))
            {
                return m_English.Resolve(Task28English.FrontendCheckpointFallback);
            }
            var normalized = Path.GetFileNameWithoutExtension(checkpoint);
            var key = normalized switch
            {
                "Opening" => "location.opening",
                "Clubhouse" => "location.clubhouse",
                "Mirra" => "location.mirra",
                "KoroVesper" => "location.koro",
                "Task25VesperFlight" => "location.vesper",
                "AsterVeil" => "location.aster",
                _ => Task28English.FrontendCheckpointFallback,
            };
            return m_English.Resolve(key);
        }

        private void PresentLoadingState()
        {
            m_LaunchView.PresentLaunch(new FrontendLaunchPresentation(
                false,
                m_English.Resolve(Task28English.FrontendNewGameLoading),
                false,
                m_English.Resolve(Task28English.FrontendContinueLoading),
                m_English.Resolve(Task28English.FrontendContinueLoading),
                FrontendContinueState.Loading));
        }

        private void HandleSettingsRequested()
        {
            ShowPanel(
                m_English.Resolve(Task28English.SettingsTitle),
                m_English.Resolve(Task28English.SettingsBody));
            m_SettingsPanel.Show();
        }

        private void HandleCreditsRequested()
        {
            ShowPanel(
                m_English.Resolve(Task28English.CreditsTitle),
                m_English.Resolve(Task28English.CreditsWrapper) +
                m_LiberationSansLicense.text +
                m_English.Resolve(Task28English.CreditsApacheWrapper) +
                m_ApacheLicense.text);
        }

        private void HandlePrivacyRequested()
        {
            ShowPanel(
                m_English.Resolve(Task28English.PrivacyTitle),
                m_English.Resolve(Task28English.PrivacyBody));
        }

        private void HandleCloseRequested() => HidePanel();

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
            if (!string.Equals(
                    title,
                    m_English.Resolve(Task28English.SettingsTitle),
                    StringComparison.Ordinal))
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

        private void DisableWithError(string message)
        {
            Debug.LogError(message, this);
            enabled = false;
        }
    }
}
