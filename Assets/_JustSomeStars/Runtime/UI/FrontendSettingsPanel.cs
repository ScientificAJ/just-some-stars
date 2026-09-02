using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accounts;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Atlas;
using JustSomeStars.Runtime.UI.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JustSomeStars.Runtime.UI
{
    [DisallowMultipleComponent]
    public sealed class FrontendSettingsPanel :
        MonoBehaviour,
        IFrontendSettingsPanel,
        IGrownUpChallengePresenter
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
        private TMP_Text[] m_NameLabels = new TMP_Text[ControlCount];

        [SerializeField]
        private LocalizedEnglishCatalog m_English;

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

        [Header("Grown-up cloud-link confirmation")]
        [SerializeField]
        private GameObject m_GrownUpChallengeRoot;

        [SerializeField]
        private TMP_Text m_GrownUpPrompt;

        [SerializeField]
        private TMP_Text m_GrownUpAnswerValue;

        [SerializeField]
        private Button m_GrownUpAnswerDownButton;

        [SerializeField]
        private Button m_GrownUpAnswerUpButton;

        [SerializeField]
        private Button m_GrownUpConfirmButton;

        [SerializeField]
        private Button m_GrownUpCancelButton;

        private readonly UnityEngine.Events.UnityAction[] m_DecreaseCallbacks =
            new UnityEngine.Events.UnityAction[ControlCount];
        private readonly UnityEngine.Events.UnityAction[] m_IncreaseCallbacks =
            new UnityEngine.Events.UnityAction[ControlCount];

        private bool m_IsListening;
        private bool m_DeleteArmed;
        private CancellationTokenSource m_AccountLifetime;
        private IGrownUpPurchaseGate m_GrownUpGate;
        private TaskCompletionSource<GrownUpChallengeResponse>
            m_GrownUpCompletion;
        private GrownUpChallenge m_GrownUpChallenge;
        private CancellationTokenRegistration m_GrownUpCancellation;
        private int m_GrownUpAnswer;

        public bool IsReady =>
            m_Root != null &&
            m_ScrollRect != null &&
            HasCompleteArray(m_DecreaseButtons) &&
            HasCompleteArray(m_IncreaseButtons) &&
            HasCompleteArray(m_ValueLabels) &&
            HasCompleteArray(m_NameLabels) &&
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

        public void SetLocalization(LocalizedEnglishCatalog english)
        {
            if (english == null)
            {
                throw new ArgumentNullException(nameof(english));
            }
            english.ValidateOrThrow();
            if (m_English != null && !ReferenceEquals(m_English, english))
            {
                throw new InvalidOperationException(
                    "FrontendSettingsPanel cannot change localization catalogs.");
            }
            m_English = english;
            ApplyStaticLocalization();
        }

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
            if (m_English == null)
            {
                throw new InvalidOperationException(
                    "Frontend settings require an English localization catalog.");
            }

            Dependencies = dependencies;
            m_GrownUpGate = dependencies.GrownUpGate ??
                new GrownUpPurchaseGate(this);
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
            CloseGrownUpChallenge();
            m_GrownUpGate = null;
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
            CloseGrownUpChallenge();
            m_DeleteArmed = false;
            if (m_CloudDeleteLabel != null)
            {
                m_CloudDeleteLabel.text = Resolve("account.delete");
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
            m_GrownUpAnswerDownButton?.onClick.AddListener(
                DecreaseGrownUpAnswer);
            m_GrownUpAnswerUpButton?.onClick.AddListener(
                IncreaseGrownUpAnswer);
            m_GrownUpConfirmButton?.onClick.AddListener(
                ConfirmGrownUpFromUi);
            m_GrownUpCancelButton?.onClick.AddListener(
                CancelGrownUpFromUi);

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
            m_GrownUpAnswerDownButton?.onClick.RemoveListener(
                DecreaseGrownUpAnswer);
            m_GrownUpAnswerUpButton?.onClick.RemoveListener(
                IncreaseGrownUpAnswer);
            m_GrownUpConfirmButton?.onClick.RemoveListener(
                ConfirmGrownUpFromUi);
            m_GrownUpCancelButton?.onClick.RemoveListener(
                CancelGrownUpFromUi);

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
            m_CloudStatusLabel.text = ResolveAccountStatus(state);
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
            m_CloudLinkLabel.text = available
                ? Resolve("account.link")
                : Resolve(Task28English.NotAvailable);
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
                ? Resolve("account.confirmDelete")
                : Resolve("account.delete");
        }

        private void HandleCloudLink() => RunAccountOperation(LinkCloudAsync);

        private async Task LinkCloudAsync(CancellationToken cancellationToken)
        {
            var ageBand = await ResolveAgeBandAsync(cancellationToken);
            await AccountLinkAuthorization.TryLinkAsync(
                Dependencies.Account,
                m_GrownUpGate,
                ageBand,
                cancellationToken);
        }

        private async ValueTask<BirthdayAgeBand> ResolveAgeBandAsync(
            CancellationToken cancellationToken)
        {
            if (Dependencies?.Saves == null)
            {
                return BirthdayAgeBand.Unknown;
            }

            var loaded = await Dependencies.Saves.LoadAsync(cancellationToken);
            return loaded.HasSave && loaded.Save?.Birthday?.HasValue == true
                ? BirthdayPolicy.AgeBandOn(
                    BirthdayDate.FromState(loaded.Save.Birthday),
                    DateTimeOffset.UtcNow)
                : BirthdayAgeBand.Unknown;
        }

        public ValueTask<GrownUpChallengeResponse> PresentAsync(
            GrownUpChallenge challenge,
            CancellationToken cancellationToken)
        {
            if (!IsConfigured || m_Root == null || !m_Root.activeInHierarchy ||
                m_GrownUpChallengeRoot == null || m_GrownUpPrompt == null ||
                m_GrownUpAnswerValue == null ||
                m_GrownUpAnswerDownButton == null ||
                m_GrownUpAnswerUpButton == null ||
                m_GrownUpConfirmButton == null || m_GrownUpCancelButton == null)
            {
                return new ValueTask<GrownUpChallengeResponse>(
                    new GrownUpChallengeResponse(challenge.Id, false, 0));
            }

            CloseGrownUpChallenge();
            m_GrownUpChallenge = challenge;
            m_GrownUpAnswer = 0;
            m_GrownUpCompletion = new TaskCompletionSource<
                GrownUpChallengeResponse>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            m_GrownUpCancellation = cancellationToken.Register(
                () => CompleteGrownUpChallenge(false));
            m_GrownUpPrompt.text = challenge.RequiresArithmetic
                ? Task28English.Format(
                    m_English,
                    "account.grownUpArithmetic",
                    challenge.LeftOperand,
                    challenge.RightOperand)
                : Resolve("account.grownUpConfirm");
            ApplyGrownUpAnswer();
            m_GrownUpChallengeRoot.SetActive(true);
            return new ValueTask<GrownUpChallengeResponse>(
                m_GrownUpCompletion.Task);
        }

        public void IncreaseGrownUpAnswer()
        {
            m_GrownUpAnswer = Math.Min(99, m_GrownUpAnswer + 1);
            ApplyGrownUpAnswer();
        }

        public void DecreaseGrownUpAnswer()
        {
            m_GrownUpAnswer = Math.Max(0, m_GrownUpAnswer - 1);
            ApplyGrownUpAnswer();
        }

        public void ConfirmGrownUpFromUi() => CompleteGrownUpChallenge(true);

        public void CancelGrownUpFromUi() => CompleteGrownUpChallenge(false);

        private void ApplyGrownUpAnswer()
        {
            if (m_GrownUpAnswerValue == null)
            {
                return;
            }

            m_GrownUpAnswerValue.text = m_GrownUpChallenge.RequiresArithmetic
                ? m_GrownUpAnswer.ToString()
                : Resolve("common.confirm");
        }

        private void CompleteGrownUpChallenge(bool confirmed)
        {
            var completion = m_GrownUpCompletion;
            if (completion == null)
            {
                m_GrownUpChallengeRoot?.SetActive(false);
                return;
            }

            m_GrownUpCompletion = null;
            var registration = m_GrownUpCancellation;
            m_GrownUpCancellation = default;
            m_GrownUpChallengeRoot?.SetActive(false);
            completion.TrySetResult(new GrownUpChallengeResponse(
                m_GrownUpChallenge.Id,
                confirmed,
                m_GrownUpAnswer));
            registration.Dispose();
        }

        private void CloseGrownUpChallenge()
        {
            CompleteGrownUpChallenge(false);
            m_GrownUpChallengeRoot?.SetActive(false);
        }

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

            m_ValueLabels[0].text = ResolveEnum(settings.PilotingAssist);
            m_ValueLabels[1].text = ResolveEnum(settings.ExplorationAssist);
            m_ValueLabels[2].text = ResolveEnum(settings.ScienceDepth);
            m_ValueLabels[3].text = OnOff(settings.CaptionsEnabled);
            m_ValueLabels[4].text = Percent(settings.TextScale);
            m_ValueLabels[5].text = OnOff(settings.DyslexiaFriendlyFontEnabled);
            m_ValueLabels[6].text = Task28English.Format(
                m_English,
                "common.multiplier",
                settings.DialogueSpeed);
            m_ValueLabels[7].text = ResolveEnum(settings.ColorVisionMode);
            m_ValueLabels[8].text = settings.ReducedCameraShake
                ? Resolve(Task28English.Reduced)
                : Resolve(Task28English.Full);
            m_ValueLabels[9].text = settings.ReducedFlashing
                ? Resolve(Task28English.Reduced)
                : Resolve(Task28English.Full);
            m_ValueLabels[10].text = OnOff(settings.ReducedMotion);
            m_ValueLabels[11].text = OnOff(settings.MotionBlurEnabled);
            m_ValueLabels[12].text = Percent(settings.ParticleDensity);
            m_ValueLabels[13].text = ResolveEnum(settings.PresentationQuality);
            m_ValueLabels[14].text = Percent(settings.MusicVolume);
            m_ValueLabels[15].text = Percent(settings.DialogueVolume);
            m_ValueLabels[16].text = Percent(settings.EffectsVolume);
            m_ValueLabels[17].text = OnOff(settings.HapticsEnabled);
            m_ValueLabels[18].text =
                settings.LeftHandedControls
                    ? Resolve(Task28English.Left)
                    : Resolve(Task28English.Right);
            m_ValueLabels[19].text = Task28English.Format(
                m_English,
                "common.multiplier",
                settings.TouchSensitivity);

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

        private string OnOff(bool enabled)
        {
            return enabled
                ? Resolve(Task28English.On)
                : Resolve(Task28English.Off);
        }

        private string Percent(float value)
        {
            return Task28English.Format(
                m_English,
                "common.percent",
                Mathf.RoundToInt(value * 100f));
        }

        private string Resolve(string key)
        {
            if (m_English == null)
            {
                throw new InvalidOperationException(
                    "Frontend settings localization is not configured.");
            }
            return m_English.Resolve(key);
        }

        private string ResolveEnum<T>(T value) where T : struct
        {
            var key = "value." + value.ToString().Replace("HighFrameRate", "highFrameRate")
                .ToLowerInvariant();
            if (value.ToString() == "HighFrameRate")
            {
                key = "value.highFrameRate";
            }
            return Resolve(key);
        }

        private string ResolveAccountStatus(AccountState state)
        {
            if (state == null)
            {
                return Resolve("account.unavailable");
            }
            if (state.Operation != AccountOperation.None)
            {
                return Task28English.Format(
                    m_English,
                    "account.busy",
                    ResolveAccountOperation(state.Operation));
            }
            return state.Connection switch
            {
                AccountConnection.CloudAvailable => Resolve("account.available"),
                AccountConnection.Linked => Resolve("account.linked"),
                AccountConnection.Pending => Resolve("account.pending"),
                AccountConnection.Conflict => Resolve("account.conflict"),
                AccountConnection.OfflineGuest => Resolve("account.offline"),
                _ => Resolve("account.unavailable"),
            };
        }

        private string ResolveAccountOperation(AccountOperation operation)
        {
            var key = operation switch
            {
                AccountOperation.Linking => "account.operation.linking",
                AccountOperation.Syncing => "account.operation.syncing",
                AccountOperation.ResolvingConflict =>
                    "account.operation.resolvingConflict",
                AccountOperation.Exporting => "account.operation.exporting",
                AccountOperation.SigningOut => "account.operation.signingOut",
                AccountOperation.Unlinking => "account.operation.unlinking",
                AccountOperation.Deleting => "account.operation.deleting",
                _ => throw new ArgumentOutOfRangeException(nameof(operation)),
            };
            return Resolve(key);
        }

        private void ApplyStaticLocalization()
        {
            if (m_English == null || m_Root == null)
            {
                return;
            }
            var settingKeys = new[]
            {
                "settings.pilotingAssist",
                "settings.explorationAssist",
                "settings.scienceDepth",
                "settings.captions",
                "settings.textScale",
                "settings.readableType",
                "settings.dialogueSpeed",
                "settings.colorVision",
                "settings.cameraShake",
                "settings.flashing",
                "settings.motion",
                "settings.motionBlur",
                "settings.particles",
                "settings.quality",
                "settings.music",
                "settings.dialogue",
                "settings.effects",
                "settings.haptics",
                "settings.controlSide",
                "settings.touchSensitivity",
            };
            for (var index = 0; index < ControlCount; index++)
            {
                m_NameLabels[index].text = Resolve(settingKeys[index]);
            }
            var named = new System.Collections.Generic.Dictionary<string, string>
            {
                { "CloudBackupSyncLabel", "account.sync" },
                { "CloudBackupExportLabel", "account.export" },
                { "CloudBackupSignOutLabel", "account.signOut" },
                { "CloudBackupUnlinkLabel", "account.unlink" },
                { "CloudBackupUseDeviceLabel", "account.useDevice" },
                { "CloudBackupUseBackupLabel", "account.useBackup" },
            };
            foreach (var label in m_Root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (named.TryGetValue(label.name, out var key))
                {
                    label.text = Resolve(key);
                }
            }
        }
    }
}
