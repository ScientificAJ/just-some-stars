using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accounts;
using JustSomeStars.Runtime.Accessibility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JustSomeStars.Runtime.UI
{
    [DisallowMultipleComponent]
    public sealed class FrontendSettingsPanel :
        MonoBehaviour,
        IFrontendSettingsPanel
    {
        public const int ControlCount = 20;

        [SerializeField]
        private GameObject m_Root;

        [SerializeField]
        private ScrollRect m_ScrollRect;

        [SerializeField]
        private Button[] m_DecreaseButtons = new Button[ControlCount];

        [SerializeField]
        private Button[] m_IncreaseButtons = new Button[ControlCount];

        [SerializeField]
        private TMP_Text[] m_ValueLabels = new TMP_Text[ControlCount];

        [SerializeField]
        private FrontendMotionDirector m_MotionDirector;

        [Header("Cloud backup")]
        [SerializeField]
        private TMP_Text m_CloudStatusLabel;

        [SerializeField]
        private Button m_CloudLinkButton;

        [SerializeField]
        private Button m_CloudSyncButton;

        [SerializeField]
        private Button m_CloudExportButton;

        [SerializeField]
        private Button m_CloudSignOutButton;

        [SerializeField]
        private Button m_CloudUnlinkButton;

        [SerializeField]
        private Button m_CloudDeleteButton;

        [SerializeField]
        private Button m_CloudUseDeviceButton;

        [SerializeField]
        private Button m_CloudUseBackupButton;

        [SerializeField]
        private TMP_Text m_CloudLinkLabel;

        [SerializeField]
        private TMP_Text m_CloudDeleteLabel;

        private readonly UnityEngine.Events.UnityAction[] m_DecreaseCallbacks =
            new UnityEngine.Events.UnityAction[ControlCount];
        private readonly UnityEngine.Events.UnityAction[] m_IncreaseCallbacks =
            new UnityEngine.Events.UnityAction[ControlCount];

        private bool m_IsListening;
        private bool m_DeleteArmed;
        private CancellationTokenSource m_AccountLifetime;

        public bool IsReady =>
            m_Root != null &&
            m_ScrollRect != null &&
            HasCompleteArray(m_DecreaseButtons) &&
            HasCompleteArray(m_IncreaseButtons) &&
            HasCompleteArray(m_ValueLabels) &&
            m_CloudStatusLabel != null &&
            m_CloudLinkButton != null &&
            m_CloudSyncButton != null &&
            m_CloudExportButton != null &&
            m_CloudSignOutButton != null &&
            m_CloudUnlinkButton != null &&
            m_CloudDeleteButton != null &&
            m_CloudUseDeviceButton != null &&
            m_CloudUseBackupButton != null &&
            m_CloudLinkLabel != null &&
            m_CloudDeleteLabel != null;

        public bool IsConfigured => Dependencies != null;

        public FrontendDependencies Dependencies { get; private set; }

        private void Awake()
        {
            if (!IsReady)
            {
                Debug.LogError(
                    "[JSS Frontend] FrontendSettingsPanel has incomplete bindings.",
                    this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            BindControls();
        }

        private void OnDisable()
        {
            UnbindControls();
        }

        private void OnDestroy()
        {
            UnbindControls();
            CancelAccountLifetime();
            if (Dependencies != null)
            {
                Dependencies.Settings.SettingsChanged -= OnSettingsChanged;
                if (Dependencies.Account != null)
                {
                    Dependencies.Account.StateChanged -= OnAccountStateChanged;
                }
                Dependencies = null;
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
                    "FrontendSettingsPanel cannot be rebound to another composition.");
            }

            if (!dependencies.Settings.IsInitialized)
            {
                throw new InvalidOperationException(
                    "Frontend settings require an initialized SettingsService.");
            }

            Dependencies = dependencies;
            Dependencies.Settings.SettingsChanged += OnSettingsChanged;
            if (Dependencies.Account != null)
            {
                Dependencies.Account.StateChanged += OnAccountStateChanged;
            }
            m_AccountLifetime = new CancellationTokenSource();
            Render(dependencies.Settings.Current);
            RenderAccount(dependencies.Account?.Current);
            BindControls();
        }

        public void Release(FrontendDependencies dependencies)
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
                    "FrontendSettingsPanel can only be released by its owning " +
                    "composition.");
            }

            UnbindControls();
            Dependencies.Settings.SettingsChanged -= OnSettingsChanged;
            if (Dependencies.Account != null)
            {
                Dependencies.Account.StateChanged -= OnAccountStateChanged;
            }
            CancelAccountLifetime();
            Dependencies = null;
            Hide();
        }

        public void Show()
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException(
                    "FrontendSettingsPanel must be configured before it is shown.");
            }

            m_Root.SetActive(true);
            Render(Dependencies.Settings.Current);
            RenderAccount(Dependencies.Account?.Current);
            Canvas.ForceUpdateCanvases();
            m_ScrollRect.StopMovement();
            m_ScrollRect.verticalNormalizedPosition = 1f;
        }

        public void Hide()
        {
            m_DeleteArmed = false;
            if (m_CloudDeleteLabel != null)
            {
                m_CloudDeleteLabel.text = "Delete cloud account";
            }

            if (m_Root != null)
            {
                m_Root.SetActive(false);
            }
        }

        private void BindControls()
        {
            if (m_IsListening || !IsConfigured || !IsReady || !isActiveAndEnabled)
            {
                return;
            }

            for (var index = 0; index < ControlCount; index++)
            {
                var capturedIndex = index;
                m_DecreaseCallbacks[index] = () => Change(capturedIndex, -1);
                m_IncreaseCallbacks[index] = () => Change(capturedIndex, 1);
                m_DecreaseButtons[index].onClick.AddListener(
                    m_DecreaseCallbacks[index]);
                m_IncreaseButtons[index].onClick.AddListener(
                    m_IncreaseCallbacks[index]);
            }

            m_CloudLinkButton.onClick.AddListener(HandleCloudLink);
            m_CloudSyncButton.onClick.AddListener(HandleCloudSync);
            m_CloudExportButton.onClick.AddListener(HandleCloudExport);
            m_CloudSignOutButton.onClick.AddListener(HandleCloudSignOut);
            m_CloudUnlinkButton.onClick.AddListener(HandleCloudUnlink);
            m_CloudDeleteButton.onClick.AddListener(HandleCloudDelete);
            m_CloudUseDeviceButton.onClick.AddListener(HandleUseDevice);
            m_CloudUseBackupButton.onClick.AddListener(HandleUseBackup);

            m_IsListening = true;
        }

        private void UnbindControls()
        {
            if (!m_IsListening)
            {
                return;
            }

            for (var index = 0; index < ControlCount; index++)
            {
                m_DecreaseButtons[index].onClick.RemoveListener(
                    m_DecreaseCallbacks[index]);
                m_IncreaseButtons[index].onClick.RemoveListener(
                    m_IncreaseCallbacks[index]);
                m_DecreaseCallbacks[index] = null;
                m_IncreaseCallbacks[index] = null;
            }

            m_CloudLinkButton.onClick.RemoveListener(HandleCloudLink);
            m_CloudSyncButton.onClick.RemoveListener(HandleCloudSync);
            m_CloudExportButton.onClick.RemoveListener(HandleCloudExport);
            m_CloudSignOutButton.onClick.RemoveListener(HandleCloudSignOut);
            m_CloudUnlinkButton.onClick.RemoveListener(HandleCloudUnlink);
            m_CloudDeleteButton.onClick.RemoveListener(HandleCloudDelete);
            m_CloudUseDeviceButton.onClick.RemoveListener(HandleUseDevice);
            m_CloudUseBackupButton.onClick.RemoveListener(HandleUseBackup);

            m_IsListening = false;
        }

        private void Change(int index, int direction)
        {
            var settings = Dependencies.Settings.Current;
            switch (index)
            {
                case 0:
                    settings.PilotingAssist = StepEnum(
                        settings.PilotingAssist,
                        direction);
                    break;
                case 1:
                    settings.ExplorationAssist = StepEnum(
                        settings.ExplorationAssist,
                        direction);
                    break;
                case 2:
                    settings.ScienceDepth = StepEnum(
                        settings.ScienceDepth,
                        direction);
                    break;
                case 3:
                    settings.CaptionsEnabled = !settings.CaptionsEnabled;
                    break;
                case 4:
                    settings.TextScale = StepFloat(
                        settings.TextScale,
                        direction,
                        0.05f,
                        0.85f,
                        1.35f);
                    break;
                case 5:
                    settings.DyslexiaFriendlyFontEnabled =
                        !settings.DyslexiaFriendlyFontEnabled;
                    break;
                case 6:
                    settings.DialogueSpeed = StepFloat(
                        settings.DialogueSpeed,
                        direction,
                        0.25f,
                        0.5f,
                        2f);
                    break;
                case 7:
                    settings.ColorVisionMode = StepEnum(
                        settings.ColorVisionMode,
                        direction);
                    break;
                case 8:
                    settings.ReducedCameraShake = !settings.ReducedCameraShake;
                    break;
                case 9:
                    settings.ReducedFlashing = !settings.ReducedFlashing;
                    break;
                case 10:
                    settings.ReducedMotion = !settings.ReducedMotion;
                    break;
                case 11:
                    settings.MotionBlurEnabled = !settings.MotionBlurEnabled;
                    break;
                case 12:
                    settings.ParticleDensity = StepFloat(
                        settings.ParticleDensity,
                        direction,
                        0.25f,
                        0f,
                        1f);
                    break;
                case 13:
                    settings.PresentationQuality = StepEnum(
                        settings.PresentationQuality,
                        direction);
                    break;
                case 14:
                    settings.MusicVolume = StepFloat(
                        settings.MusicVolume,
                        direction,
                        0.1f,
                        0f,
                        1f);
                    break;
                case 15:
                    settings.DialogueVolume = StepFloat(
                        settings.DialogueVolume,
                        direction,
                        0.1f,
                        0f,
                        1f);
                    break;
                case 16:
                    settings.EffectsVolume = StepFloat(
                        settings.EffectsVolume,
                        direction,
                        0.1f,
                        0f,
                        1f);
                    break;
                case 17:
                    settings.HapticsEnabled = !settings.HapticsEnabled;
                    break;
                case 18:
                    settings.LeftHandedControls = !settings.LeftHandedControls;
                    break;
                case 19:
                    settings.TouchSensitivity = StepFloat(
                        settings.TouchSensitivity,
                        direction,
                        0.25f,
                        0.5f,
                        2f);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index));
            }

            try
            {
                if (!Dependencies.Settings.Apply(settings))
                {
                    Render(Dependencies.Settings.Current);
                }
            }
            catch (IOException exception)
            {
                Debug.LogError(
                    "[JSS Frontend] Could not save local settings: " +
                    exception.Message,
                    this);
                Render(Dependencies.Settings.Current);
            }
            catch (UnauthorizedAccessException exception)
            {
                Debug.LogError(
                    "[JSS Frontend] Could not save local settings: " +
                    exception.Message,
                    this);
                Render(Dependencies.Settings.Current);
            }
        }

        private void OnSettingsChanged(GameSettings settings)
        {
            Render(settings);
        }

        private void OnAccountStateChanged(AccountState state)
        {
            RenderAccount(state);
        }

        private void RenderAccount(AccountState state)
        {
            if (!IsReady)
            {
                return;
            }

            var hasAccount = state != null;
            m_CloudStatusLabel.text = hasAccount
                ? state.StatusMessage
                : "Google backup isn’t available in this build. " +
                  "Offline progress still works.";
            var busy = hasAccount && state.Operation != AccountOperation.None;
            var available = hasAccount &&
                state.Connection == AccountConnection.CloudAvailable;
            var linked = hasAccount && state.Connection == AccountConnection.Linked;
            var pending = hasAccount && state.Connection == AccountConnection.Pending;
            var conflict = hasAccount &&
                state.Connection == AccountConnection.Conflict;
            var authenticated = hasAccount &&
                !string.IsNullOrWhiteSpace(state.FirebaseUserId) &&
                (linked || pending || conflict);

            m_CloudLinkButton.gameObject.SetActive(!authenticated);
            m_CloudLinkButton.interactable = available && !busy;
            m_CloudLinkLabel.text = available ? "Back up with Google" : "Not available";
            m_CloudSyncButton.gameObject.SetActive(linked || pending);
            m_CloudSyncButton.interactable = (linked || pending) && !busy;
            m_CloudExportButton.gameObject.SetActive(linked);
            m_CloudExportButton.interactable = linked && !busy &&
                state.Sync == AccountSyncState.Synced;
            m_CloudSignOutButton.gameObject.SetActive(authenticated);
            m_CloudSignOutButton.interactable = authenticated && !busy;
            m_CloudUnlinkButton.gameObject.SetActive(authenticated);
            m_CloudUnlinkButton.interactable = authenticated && !busy;
            m_CloudDeleteButton.gameObject.SetActive(authenticated);
            m_CloudDeleteButton.interactable = authenticated && !busy;
            m_CloudUseDeviceButton.gameObject.SetActive(conflict);
            m_CloudUseDeviceButton.interactable = conflict && !busy;
            m_CloudUseBackupButton.gameObject.SetActive(conflict);
            m_CloudUseBackupButton.interactable = conflict && !busy;
            if (!authenticated)
            {
                m_DeleteArmed = false;
            }

            m_CloudDeleteLabel.text = m_DeleteArmed
                ? "Confirm delete"
                : "Delete cloud account";
        }

        private void HandleCloudLink() => RunAccountOperation(
            token => Dependencies.Account.LinkGoogleAsync(token).AsTask());

        private void HandleCloudSync() => RunAccountOperation(
            token => Dependencies.Account.SyncAsync(token).AsTask());

        private void HandleCloudExport() => RunAccountOperation(
            async token =>
            {
                var result = await Dependencies.Account.ExportDataAsync(token);
                if (result.Succeeded)
                {
                    WriteCloudExport(result.Document);
                }
            });

        private void HandleCloudSignOut() => RunAccountOperation(
            token => Dependencies.Account.SignOutAsync(token).AsTask());

        private void HandleCloudUnlink() => RunAccountOperation(
            token => Dependencies.Account.UnlinkGoogleAsync(token).AsTask());

        private void HandleCloudDelete()
        {
            if (!m_DeleteArmed)
            {
                m_DeleteArmed = true;
                RenderAccount(Dependencies.Account.Current);
                return;
            }

            m_DeleteArmed = false;
            RunAccountOperation(
                token => Dependencies.Account.DeleteAccountAsync(token).AsTask());
        }

        private void HandleUseDevice() => RunAccountOperation(
            token => Dependencies.Account.ResolveConflictAsync(
                AccountConflictChoice.UseThisDevice,
                token).AsTask());

        private void HandleUseBackup() => RunAccountOperation(
            token => Dependencies.Account.ResolveConflictAsync(
                AccountConflictChoice.UseCloudBackup,
                token).AsTask());

        private async void RunAccountOperation(
            Func<CancellationToken, Task> operation)
        {
            if (Dependencies?.Account == null || operation == null ||
                m_AccountLifetime == null)
            {
                return;
            }

            try
            {
                await operation(m_AccountLifetime.Token);
            }
            catch (OperationCanceledException)
            {
                // Scene teardown owns cancellation; no UI survives it.
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is InvalidOperationException)
            {
                Debug.LogWarning(
                    "[JSS Frontend] Cloud backup operation did not complete: " +
                    exception.GetType().Name,
                    this);
            }
            finally
            {
                if (Dependencies?.Account != null)
                {
                    RenderAccount(Dependencies.Account.Current);
                }
            }
        }

        private static void WriteCloudExport(string document)
        {
            var directory = Path.Combine(
                Application.persistentDataPath,
                "JustSomeStars");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "cloud-save-export.json");
            var temporary = path + ".tmp";
            File.WriteAllText(
                temporary,
                document ?? string.Empty,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            if (File.Exists(path))
            {
                File.Replace(temporary, path, null);
            }
            else
            {
                File.Move(temporary, path);
            }
        }

        private void CancelAccountLifetime()
        {
            if (m_AccountLifetime == null)
            {
                return;
            }

            m_AccountLifetime.Cancel();
            m_AccountLifetime.Dispose();
            m_AccountLifetime = null;
        }

        private void Render(GameSettings settings)
        {
            if (!IsReady || settings == null)
            {
                return;
            }

            m_ValueLabels[0].text = settings.PilotingAssist.ToString();
            m_ValueLabels[1].text = settings.ExplorationAssist.ToString();
            m_ValueLabels[2].text = settings.ScienceDepth.ToString();
            m_ValueLabels[3].text = OnOff(settings.CaptionsEnabled);
            m_ValueLabels[4].text = Percent(settings.TextScale);
            m_ValueLabels[5].text = OnOff(settings.DyslexiaFriendlyFontEnabled);
            m_ValueLabels[6].text = $"{settings.DialogueSpeed:0.00}x";
            m_ValueLabels[7].text = settings.ColorVisionMode.ToString();
            m_ValueLabels[8].text = settings.ReducedCameraShake ? "Reduced" : "Full";
            m_ValueLabels[9].text = settings.ReducedFlashing ? "Reduced" : "Full";
            m_ValueLabels[10].text = OnOff(settings.ReducedMotion);
            m_ValueLabels[11].text = OnOff(settings.MotionBlurEnabled);
            m_ValueLabels[12].text = Percent(settings.ParticleDensity);
            m_ValueLabels[13].text = settings.PresentationQuality.ToString();
            m_ValueLabels[14].text = Percent(settings.MusicVolume);
            m_ValueLabels[15].text = Percent(settings.DialogueVolume);
            m_ValueLabels[16].text = Percent(settings.EffectsVolume);
            m_ValueLabels[17].text = OnOff(settings.HapticsEnabled);
            m_ValueLabels[18].text =
                settings.LeftHandedControls ? "Left" : "Right";
            m_ValueLabels[19].text = $"{settings.TouchSensitivity:0.00}x";

            if (m_MotionDirector != null)
            {
                m_MotionDirector.MotionScale = settings.ReducedMotion ? 0f : 1f;
            }
        }

        private static bool HasCompleteArray<T>(T[] values)
            where T : UnityEngine.Object
        {
            if (values == null || values.Length != ControlCount)
            {
                return false;
            }

            foreach (var value in values)
            {
                if (value == null)
                {
                    return false;
                }
            }

            return true;
        }

        private static T StepEnum<T>(T value, int direction)
            where T : struct
        {
            var values = (T[])Enum.GetValues(typeof(T));
            var index = Array.IndexOf(values, value);
            index = Mathf.Clamp(index + Math.Sign(direction), 0, values.Length - 1);
            return values[index];
        }

        private static float StepFloat(
            float value,
            int direction,
            float step,
            float minimum,
            float maximum)
        {
            var stepped = value + (Math.Sign(direction) * step);
            return Mathf.Round(Mathf.Clamp(stepped, minimum, maximum) * 100f) /
                100f;
        }

        private static string OnOff(bool enabled)
        {
            return enabled ? "On" : "Off";
        }

        private static string Percent(float value)
        {
            return $"{Mathf.RoundToInt(value * 100f)}%";
        }
    }
}
