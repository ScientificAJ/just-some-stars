using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Accounts;
using JustSomeStars.Runtime.Atlas;
using JustSomeStars.Runtime.Commerce;
using JustSomeStars.Runtime.Cinematics;
using JustSomeStars.Runtime.Core;
using JustSomeStars.Runtime.Cosmetics;
using JustSomeStars.Runtime.Flight;
using JustSomeStars.Runtime.Input;
using JustSomeStars.Runtime.Missions;
using JustSomeStars.Runtime.Player;
using JustSomeStars.Runtime.Saving;
using JustSomeStars.Runtime.UI.Account;
using JustSomeStars.Runtime.UI.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JustSomeStars.Runtime.UI
{
    public enum PlayerMenuSection
    {
        Journey = 0,
        Accessibility = 1,
        Atlas = 2,
        Captain = 3,
        Shop = 4,
        Account = 5,
        Birthday = 6,
    }

    public sealed class PlayerMenuRuntimeDependencies
    {
        public PlayerMenuRuntimeDependencies(
            SettingsService settings,
            InputRouter input,
            GameModeController modes,
            ISaveService saves,
            IChapterProgression progression,
            IAccountService account,
            IStoreService store,
            LocalizedEnglishCatalog english,
            CosmeticCatalog catalog,
            BirthdayGiftService birthdays = null)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Input = input ?? throw new ArgumentNullException(nameof(input));
            Modes = modes ?? throw new ArgumentNullException(nameof(modes));
            Saves = saves ?? PlayerMenuMissingSaveService.Instance;
            Progression = progression;
            Account = account;
            Store = store ?? new UnavailableStoreService();
            English = english ?? throw new ArgumentNullException(nameof(english));
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Birthdays = birthdays;
            English.ValidateOrThrow();
            Catalog.ValidateOrThrow();
        }

        public SettingsService Settings { get; }
        public InputRouter Input { get; }
        public GameModeController Modes { get; }
        public ISaveService Saves { get; }
        public IChapterProgression Progression { get; }
        public IAccountService Account { get; }
        public IStoreService Store { get; }
        public LocalizedEnglishCatalog English { get; }
        public CosmeticCatalog Catalog { get; }
        public BirthdayGiftService Birthdays { get; }
    }

    internal sealed class PlayerMenuMissingSaveService : ISaveService
    {
        public static readonly PlayerMenuMissingSaveService Instance =
            new PlayerMenuMissingSaveService();

        private PlayerMenuMissingSaveService()
        {
        }

        public ValueTask<StartupResult> InitializeAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<StartupResult>(StartupResult.Unavailable(
                "Local saves are unavailable in this composition."));
        }

        public ValueTask ShutdownAsync() => default;

        public ValueTask<LoadSaveResult> LoadAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<LoadSaveResult>(new LoadSaveResult(
                LoadSaveStatus.StorageUnavailable,
                null,
                "Local saves are unavailable."));
        }

        public ValueTask SaveCheckpointAsync(
            GameSave save,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                "This composition does not own local-save persistence.");

        public ValueTask<LoadSaveResult> RecoverAsync(
            CancellationToken cancellationToken) =>
            LoadAsync(cancellationToken);

        public GameSave Merge(GameSave local, GameSave cloud) =>
            throw new InvalidOperationException(
                "This composition does not own save merging.");
    }

    [DisallowMultipleComponent]
    public sealed class PlayerMenuController :
        MonoBehaviour,
        ISurfaceGameplayExtension,
        IFlightGameplayExtension,
        IChapterOneSequenceExtension,
        IGrownUpChallengePresenter
    {
        private static readonly string[] CaptainFamilies =
        {
            "captain.family.a",
            "captain.family.b",
            "captain.family.c",
        };

        private static readonly string[] CaptainAppearances =
        {
            "captain.face.01",
            "captain.face.02",
            "captain.face.03",
            "captain.face.04",
            "captain.face.05",
            "captain.face.06",
        };

        private static readonly string[] CaptainSuits =
        {
            "suit.clubhouse",
            "suit.signal",
            "suit.flight",
        };

        private enum AccessibilitySetting
        {
            PilotingAssist = 0,
            ExplorationAssist = 1,
            ScienceDepth = 2,
            Captions = 3,
            TextScale = 4,
            ReadableType = 5,
            DialogueSpeed = 6,
            ColorVision = 7,
            CameraShake = 8,
            Flashing = 9,
            Motion = 10,
            MotionBlur = 11,
            Particles = 12,
            Quality = 13,
            Music = 14,
            Dialogue = 15,
            Effects = 16,
            Haptics = 17,
            ControlSide = 18,
            TouchSensitivity = 19,
        }

        [SerializeField] private LocalizedEnglishCatalog m_English;
        [SerializeField] private CosmeticCatalog m_Catalog;
        [SerializeField] private GameObject m_OpenRoot;
        [SerializeField] private GameObject m_PanelRoot;
        [SerializeField] private TMP_Text m_Title;
        [SerializeField] private TMP_Text m_Context;
        [SerializeField] private TMP_Text m_Detail;
        [SerializeField] private TMP_Text m_AccessibilitySettingName;
        [SerializeField] private TMP_Text m_AccessibilitySettingValue;
        [SerializeField] private TMP_Text m_AtlasValue;
        [SerializeField] private TMP_Text m_CaptainValue;
        [SerializeField] private TMP_Text m_ShopProductValue;
        [SerializeField] private TMP_Text m_GrownUpPrompt;
        [SerializeField] private TMP_Text m_GrownUpAnswerValue;
        [SerializeField] private TMP_Text m_BirthdayDayValue;
        [SerializeField] private TMP_Text m_BirthdayMonthValue;
        [SerializeField] private TMP_Text m_BirthdayYearValue;
        [SerializeField] private GameObject m_AccessibilityControlsRoot;
        [SerializeField] private GameObject m_AtlasControlsRoot;
        [SerializeField] private GameObject m_CaptainControlsRoot;
        [SerializeField] private GameObject m_ShopControlsRoot;
        [SerializeField] private GameObject m_GrownUpChallengeRoot;
        [SerializeField] private GameObject m_BirthdayControlsRoot;
        [SerializeField] private AtlasEntry[] m_AtlasEntries =
            Array.Empty<AtlasEntry>();
        [SerializeField] private Button m_CaptainFamilyButton;
        [SerializeField] private Button m_CaptainAppearanceButton;
        [SerializeField] private Button m_CaptainSuitButton;
        [SerializeField] private Button m_CaptainCosmeticButton;
        [SerializeField] private Button m_ShopPreviousButton;
        [SerializeField] private Button m_ShopNextButton;
        [SerializeField] private Button m_ShopPurchaseButton;
        [SerializeField] private Button m_RestoreButton;
        [SerializeField] private Button m_GrownUpConfirmButton;
        [SerializeField] private Button m_GrownUpCancelButton;
        [SerializeField] private Button m_LinkButton;
        [SerializeField] private Button m_SyncButton;
        [SerializeField] private Button m_BirthdaySaveButton;
        [SerializeField] private Button m_BirthdayConfirmButton;

        private PlayerMenuRuntimeDependencies m_Dependencies;
        private CancellationTokenSource m_Lifetime;
        private GameSave m_Save;
        private bool m_IsTransitioning;
        private bool m_InputBound;
        private int m_AccessibilitySettingIndex;
        private int m_AtlasIndex;
        private int m_SelectedProductIndex;
        private int m_GrownUpAnswer;
        private bool m_BirthdayCorrectionArmed;
        private bool m_BirthdayCorrectionConfirmed;
        private int m_BirthdayDay;
        private int m_BirthdayMonth;
        private int m_BirthdayYear;
        private BirthdaySetupController m_BirthdaySetup;
        private ShopController m_Shop;
        private IGrownUpPurchaseGate m_GrownUpGate;
        private TaskCompletionSource<GrownUpChallengeResponse>
            m_GrownUpCompletion;
        private GrownUpChallenge m_GrownUpChallenge;
        private CancellationTokenRegistration m_GrownUpCancellation;

        public bool IsOpen { get; private set; }
        public PlayerMenuSection ActiveSection { get; private set; }
        public string CurrentDetail => m_Detail != null
            ? m_Detail.text
            : string.Empty;

        public void Configure(PlayerMenuRuntimeDependencies dependencies)
        {
            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }
            if (m_Dependencies != null)
            {
                if (ReferenceEquals(m_Dependencies, dependencies))
                {
                    return;
                }
                throw new InvalidOperationException(
                    "PlayerMenuController cannot be rebound without release.");
            }

            m_Dependencies = dependencies;
            m_English = dependencies.English;
            m_Catalog = dependencies.Catalog;
            m_AtlasEntries ??= Array.Empty<AtlasEntry>();
            foreach (var entry in m_AtlasEntries)
            {
                entry?.ValidateOrThrow();
            }
            if (m_AtlasEntries
                .Where(entry => entry != null)
                .GroupBy(entry => entry.StableId.Value, StringComparer.Ordinal)
                .Any(group => group.Count() > 1))
            {
                throw new InvalidOperationException(
                    "Player Atlas entries require unique stable IDs.");
            }
            m_GrownUpGate = new GrownUpPurchaseGate(this);
            m_Shop = new ShopController(dependencies.Store, m_GrownUpGate);
            m_Shop.StateChanged += HandleShopChanged;
            var birthdayService = dependencies.Birthdays;
            if (birthdayService == null && dependencies.Account != null)
            {
                birthdayService = new BirthdayGiftService(
                    dependencies.Saves,
                    dependencies.Account,
                    new UnavailableBirthdayGiftGateway(),
                    () => DateTimeOffset.UtcNow,
                    Array.Empty<BirthdayGiftOffer>());
            }
            m_BirthdaySetup = birthdayService == null
                ? null
                : new BirthdaySetupController(
                    birthdayService,
                    new GrownUpConfirmationController(
                        ConfirmBirthdayCorrectionAsync));
            m_Lifetime = new CancellationTokenSource();
            m_Dependencies.Input.GameplayCommandPerformed += HandleCommand;
            m_InputBound = true;
            m_Dependencies.Settings.SettingsChanged += HandleSettingsChanged;
            if (m_Dependencies.Account != null)
            {
                m_Dependencies.Account.StateChanged += HandleAccountChanged;
            }
            m_PanelRoot?.SetActive(false);
            m_OpenRoot?.SetActive(true);
            SeedBirthdaySelection();
            ApplySettingsValues(m_Dependencies.Settings.Current);
            RenderSection(PlayerMenuSection.Journey);
        }

        public void Configure(SurfaceGameplayDependencies dependencies)
        {
            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }
            Configure(new PlayerMenuRuntimeDependencies(
                dependencies.Settings,
                dependencies.Input,
                dependencies.Modes,
                dependencies.Saves,
                dependencies.ChapterProgression,
                dependencies.Account,
                dependencies.Store,
                m_English,
                m_Catalog));
        }

        public void Release(SurfaceGameplayDependencies dependencies)
        {
            _ = dependencies;
            ReleaseRuntime();
        }

        public void Configure(FlightGameplayDependencies dependencies)
        {
            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }
            Configure(new PlayerMenuRuntimeDependencies(
                dependencies.Settings,
                dependencies.Input,
                dependencies.Modes,
                dependencies.Saves,
                dependencies.Progression,
                dependencies.Account,
                dependencies.Store,
                m_English,
                m_Catalog));
        }

        public void Release(FlightGameplayDependencies dependencies)
        {
            _ = dependencies;
            ReleaseRuntime();
        }

        public void Configure(ChapterOneSequenceDependencies dependencies)
        {
            if (dependencies?.Settings == null)
            {
                throw new InvalidOperationException(
                    "Clubhouse player UI requires settings dependencies.");
            }
            Configure(new PlayerMenuRuntimeDependencies(
                dependencies.Settings,
                dependencies.Input,
                dependencies.Modes,
                dependencies.Saves,
                dependencies.Progression,
                dependencies.Account,
                dependencies.Store,
                m_English,
                m_Catalog));
        }

        public void Release(ChapterOneSequenceDependencies dependencies)
        {
            _ = dependencies;
            ReleaseRuntime();
        }

        public async ValueTask OpenAsync(CancellationToken cancellationToken)
        {
            RequireConfigured();
            if (IsOpen || m_IsTransitioning)
            {
                return;
            }
            m_IsTransitioning = true;
            try
            {
                await m_Dependencies.Modes.OpenOverlayAsync(
                    GameOverlay.Pause,
                    cancellationToken);
                var loaded = await m_Dependencies.Saves.LoadAsync(cancellationToken);
                m_Save = loaded.HasSave ? loaded.Save : null;
                m_SelectedProductIndex = 0;
                m_BirthdayCorrectionArmed = false;
                m_BirthdayCorrectionConfirmed = false;
                SeedBirthdaySelection();
                IsOpen = true;
                m_OpenRoot?.SetActive(false);
                m_PanelRoot?.SetActive(true);
                RenderSection(PlayerMenuSection.Journey);
            }
            finally
            {
                m_IsTransitioning = false;
            }
        }

        public async ValueTask CloseAsync(CancellationToken cancellationToken)
        {
            if (!IsOpen || m_IsTransitioning)
            {
                return;
            }
            m_IsTransitioning = true;
            try
            {
                await m_Dependencies.Modes.CloseOverlayAsync(cancellationToken);
                m_Shop?.Close();
                CloseGrownUpChallenge();
                m_BirthdayCorrectionArmed = false;
                m_BirthdayCorrectionConfirmed = false;
                IsOpen = false;
                m_PanelRoot?.SetActive(false);
                m_OpenRoot?.SetActive(true);
                RenderButtonAvailability();
            }
            finally
            {
                m_IsTransitioning = false;
            }
        }

        public async void ToggleFromUi()
        {
            await RunUiAsync(IsOpen
                ? CloseAsync(destroyCancellationToken)
                : OpenAsync(destroyCancellationToken));
        }

        public void ShowJourney() => RenderSection(PlayerMenuSection.Journey);
        public void ShowAccessibility() =>
            RenderSection(PlayerMenuSection.Accessibility);
        public void ShowAtlas() => RenderSection(PlayerMenuSection.Atlas);
        public void ShowCaptain() => RenderSection(PlayerMenuSection.Captain);
        public async void ShowShop() =>
            await RunUiAsync(OpenShopAsync(m_Lifetime.Token));
        public void ShowAccount() => RenderSection(PlayerMenuSection.Account);
        public void ShowBirthday() => RenderSection(PlayerMenuSection.Birthday);

        public void PreviousAccessibilitySetting()
        {
            m_AccessibilitySettingIndex = Wrap(
                m_AccessibilitySettingIndex - 1,
                Enum.GetValues(typeof(AccessibilitySetting)).Length);
            ApplySettingsValues(m_Dependencies.Settings.Current);
        }

        public void NextAccessibilitySetting()
        {
            m_AccessibilitySettingIndex = Wrap(
                m_AccessibilitySettingIndex + 1,
                Enum.GetValues(typeof(AccessibilitySetting)).Length);
            ApplySettingsValues(m_Dependencies.Settings.Current);
        }

        public void DecreaseAccessibilitySetting() =>
            AdjustAccessibilitySetting(-1);

        public void IncreaseAccessibilitySetting() =>
            AdjustAccessibilitySetting(1);

        public void IncreaseTextScale() => MutateSettings(settings =>
            settings.TextScale = Mathf.Min(1.35f, settings.TextScale + 0.1f));

        public void DecreaseTextScale() => MutateSettings(settings =>
            settings.TextScale = Mathf.Max(0.85f, settings.TextScale - 0.1f));

        public void ToggleCaptions() => MutateSettings(settings =>
            settings.CaptionsEnabled = !settings.CaptionsEnabled);

        public void ToggleReadableType() => MutateSettings(settings =>
            settings.DyslexiaFriendlyFontEnabled =
                !settings.DyslexiaFriendlyFontEnabled);

        public void ToggleReducedMotion() => MutateSettings(settings =>
            settings.ReducedMotion = !settings.ReducedMotion);

        public void ToggleControlSide() => MutateSettings(settings =>
            settings.LeftHandedControls = !settings.LeftHandedControls);

        public void NextBirthdayDay()
        {
            RequireOpenBirthday();
            var days = DateTime.DaysInMonth(m_BirthdayYear, m_BirthdayMonth);
            m_BirthdayDay = m_BirthdayDay >= days ? 1 : m_BirthdayDay + 1;
            ApplyBirthdayValues();
        }

        public void NextBirthdayMonth()
        {
            RequireOpenBirthday();
            m_BirthdayMonth = m_BirthdayMonth >= 12 ? 1 : m_BirthdayMonth + 1;
            m_BirthdayDay = Mathf.Min(
                m_BirthdayDay,
                DateTime.DaysInMonth(m_BirthdayYear, m_BirthdayMonth));
            ApplyBirthdayValues();
        }

        public void NextBirthdayYear()
        {
            RequireOpenBirthday();
            var latest = DateTime.UtcNow.Year - 1;
            var earliest = latest - 99;
            m_BirthdayYear = m_BirthdayYear >= latest
                ? earliest
                : m_BirthdayYear + 1;
            m_BirthdayDay = Mathf.Min(
                m_BirthdayDay,
                DateTime.DaysInMonth(m_BirthdayYear, m_BirthdayMonth));
            ApplyBirthdayValues();
        }

        public async void SaveBirthdayFromUi() =>
            await RunUiAsync(SaveBirthdayAsync(m_Lifetime.Token));

        public async void ConfirmBirthdayCorrectionFromUi()
        {
            if (!m_BirthdayCorrectionArmed)
            {
                return;
            }
            m_BirthdayCorrectionConfirmed = true;
            await RunUiAsync(SaveBirthdayAsync(m_Lifetime.Token));
        }

        public async void CycleCaptainFamilyFromUi()
        {
            if (!CanCustomizeCaptain())
            {
                return;
            }
            var current = Array.IndexOf(
                CaptainFamilies,
                m_Save.Captain.BodyFamilyId);
            m_Save.Captain.BodyFamilyId =
                CaptainFamilies[(current + 1 + CaptainFamilies.Length) %
                    CaptainFamilies.Length];
            m_Save.Captain.LastCustomizedUtcTicks = DateTime.UtcNow.Ticks;
            await RunUiAsync(SaveCaptainAsync(m_Lifetime.Token));
        }

        public async void CycleCaptainAppearanceFromUi()
        {
            if (!CanCustomizeCaptain())
            {
                return;
            }
            var current = Array.IndexOf(
                CaptainAppearances,
                m_Save.Captain.AppearancePresetId);
            m_Save.Captain.AppearancePresetId =
                CaptainAppearances[Wrap(current + 1, CaptainAppearances.Length)];
            m_Save.Captain.LastCustomizedUtcTicks = DateTime.UtcNow.Ticks;
            await RunUiAsync(SaveCaptainAsync(m_Lifetime.Token));
        }

        public async void CycleCaptainSuitFromUi()
        {
            if (!CanCustomizeCaptain())
            {
                return;
            }
            var current = Array.IndexOf(
                CaptainSuits,
                m_Save.Captain.SuitCosmeticId);
            m_Save.Captain.SuitCosmeticId =
                CaptainSuits[Wrap(current + 1, CaptainSuits.Length)];
            m_Save.Captain.LastCustomizedUtcTicks = DateTime.UtcNow.Ticks;
            await RunUiAsync(SaveCaptainAsync(m_Lifetime.Token));
        }

        public async void CycleOwnedCaptainCosmeticFromUi()
        {
            if (!CanCustomizeCaptain())
            {
                return;
            }
            var earned = new HashSet<string>(
                m_Save.EarnedCosmeticIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            var currentId = m_Save.CosmeticLoadout.Captain;
            var owned = m_Catalog.Category(CosmeticCategory.Captain)
                .Where(item => item.OwnershipSource == CosmeticOwnershipSource.Free ||
                    earned.Contains(item.Id) ||
                    string.Equals(item.Id, currentId, StringComparison.Ordinal))
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .ToArray();
            if (owned.Length == 0)
            {
                SetDetail(Resolve("menu.customization.noOwned"));
                return;
            }
            var current = Array.FindIndex(
                owned,
                item => string.Equals(item.Id, currentId, StringComparison.Ordinal));
            var selected = owned[Wrap(current + 1, owned.Length)];
            m_Save.CosmeticLoadout.Set(
                CosmeticCategory.Captain,
                selected.Id,
                DateTime.UtcNow.Ticks);
            m_Save.Captain.LastCustomizedUtcTicks = DateTime.UtcNow.Ticks;
            await RunUiAsync(SaveCaptainAsync(m_Lifetime.Token));
        }

        public void PreviousAtlasEntry()
        {
            m_AtlasIndex = Wrap(m_AtlasIndex - 1, UnlockedAtlasEntries().Length);
            RenderSection(PlayerMenuSection.Atlas);
        }

        public void NextAtlasEntry()
        {
            m_AtlasIndex = Wrap(m_AtlasIndex + 1, UnlockedAtlasEntries().Length);
            RenderSection(PlayerMenuSection.Atlas);
        }

        public void NextAtlasDepth()
        {
            MutateSettings(settings => settings.ScienceDepth =
                NextEnum(settings.ScienceDepth, 1));
            RenderSection(PlayerMenuSection.Atlas);
        }

        public void PreviousShopProduct()
        {
            m_SelectedProductIndex = Wrap(
                m_SelectedProductIndex - 1,
                m_Shop?.Products.Count ?? 0);
            RenderShop();
        }

        public void NextShopProduct()
        {
            m_SelectedProductIndex = Wrap(
                m_SelectedProductIndex + 1,
                m_Shop?.Products.Count ?? 0);
            RenderShop();
        }

        public async void PurchaseSelectedProductFromUi() =>
            await RunUiAsync(PurchaseSelectedProductAsync(m_Lifetime.Token));

        public async void RestorePurchasesFromUi() =>
            await RunUiAsync(RestorePurchasesAsync(m_Lifetime.Token));

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

        public async void LinkAccountFromUi() =>
            await RunUiAsync(LinkAccountAsync(m_Lifetime.Token));

        public async void SyncAccountFromUi() =>
            await RunUiAsync(SyncAccountAsync(m_Lifetime.Token));

        private async ValueTask SaveCaptainAsync(CancellationToken cancellationToken)
        {
            await m_Dependencies.Saves.SaveCheckpointAsync(m_Save, cancellationToken);
            SetDetail(Task28English.Format(
                m_English,
                "menu.customization.saved",
                FriendlyFamily(m_Save.Captain.BodyFamilyId)));
            RenderButtonAvailability();
        }

        private async ValueTask LinkAccountAsync(CancellationToken cancellationToken)
        {
            if (m_Dependencies.Account == null || m_GrownUpGate == null)
            {
                SetDetail(Resolve("account.unavailable"));
                return;
            }

            if (!await AccountLinkAuthorization.TryLinkAsync(
                    m_Dependencies.Account,
                    m_GrownUpGate,
                    CurrentAgeBand(),
                    cancellationToken))
            {
                SetDetail(Resolve("account.linkCancelled"));
                return;
            }
            RenderSection(PlayerMenuSection.Account);
        }

        private async ValueTask OpenShopAsync(CancellationToken cancellationToken)
        {
            if (!IsOpen || m_Shop == null)
            {
                return;
            }
            RenderSection(PlayerMenuSection.Shop);
            await m_Shop.OpenAsync(cancellationToken);
            m_SelectedProductIndex = Wrap(
                m_SelectedProductIndex,
                m_Shop.Products.Count);
            RenderShop();
        }

        private async ValueTask PurchaseSelectedProductAsync(
            CancellationToken cancellationToken)
        {
            if (!IsOpen || ActiveSection != PlayerMenuSection.Shop ||
                m_Shop == null || !m_Shop.IsOpen || m_Shop.Products.Count == 0)
            {
                SetDetail(Resolve("shop.unavailable"));
                return;
            }
            var product = m_Shop.Products[Wrap(
                m_SelectedProductIndex,
                m_Shop.Products.Count)];
            var result = await m_Shop.PurchaseAsync(
                product.Id,
                CurrentAgeBand(),
                cancellationToken);
            SetDetail(string.IsNullOrWhiteSpace(result.Message)
                ? Resolve("shop.purchaseFinished")
                : result.Message);
            RenderShop();
        }

        private async ValueTask RestorePurchasesAsync(
            CancellationToken cancellationToken)
        {
            if (!IsOpen || ActiveSection != PlayerMenuSection.Shop ||
                m_Shop == null || !m_Shop.IsOpen)
            {
                SetDetail(Resolve("shop.unavailable"));
                return;
            }
            await m_Shop.RestoreAsync(CurrentAgeBand(), cancellationToken);
            SetDetail(string.IsNullOrWhiteSpace(m_Shop.StatusMessage)
                ? Resolve("shop.restoreComplete")
                : m_Shop.StatusMessage);
            RenderShop();
        }

        public ValueTask<GrownUpChallengeResponse> PresentAsync(
            GrownUpChallenge challenge,
            CancellationToken cancellationToken)
        {
            if (!IsOpen ||
                (ActiveSection != PlayerMenuSection.Shop &&
                 ActiveSection != PlayerMenuSection.Account))
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
            if (m_GrownUpPrompt != null)
            {
                var isCloudLink = challenge.Action == GrownUpAction.CloudLink;
                m_GrownUpPrompt.text = challenge.RequiresArithmetic
                    ? Task28English.Format(
                        m_English,
                        isCloudLink
                            ? "account.grownUpArithmetic"
                            : "shop.grownUpArithmetic",
                        challenge.LeftOperand,
                        challenge.RightOperand)
                    : Resolve(isCloudLink
                        ? "account.grownUpConfirm"
                        : "shop.grownUpConfirm");
            }
            ApplyGrownUpAnswer();
            m_GrownUpChallengeRoot?.SetActive(true);
            RenderButtonAvailability();
            return new ValueTask<GrownUpChallengeResponse>(
                m_GrownUpCompletion.Task);
        }

        private async ValueTask SyncAccountAsync(CancellationToken cancellationToken)
        {
            if (m_Dependencies.Account == null)
            {
                SetDetail(Resolve("account.unavailable"));
                return;
            }
            await m_Dependencies.Account.SyncAsync(cancellationToken);
            RenderSection(PlayerMenuSection.Account);
        }

        private async ValueTask SaveBirthdayAsync(
            CancellationToken cancellationToken)
        {
            if (!IsOpen || ActiveSection != PlayerMenuSection.Birthday ||
                m_BirthdaySetup == null || m_Save == null)
            {
                SetDetail(Resolve("birthday.unavailable"));
                return;
            }
            var result = await m_BirthdaySetup.SubmitAsync(
                m_BirthdayDay,
                m_BirthdayMonth,
                m_BirthdayYear,
                cancellationToken);
            if (result.Status == BirthdayUpdateStatus.RequiresGrownUp)
            {
                SetDetail(Resolve("birthday.grownUpRequired"));
                RenderButtonAvailability();
                return;
            }
            var loaded = await m_Dependencies.Saves.LoadAsync(cancellationToken);
            m_Save = loaded.HasSave ? loaded.Save : m_Save;
            m_BirthdayCorrectionArmed = false;
            m_BirthdayCorrectionConfirmed = false;
            SetDetail(Resolve("birthday.saved"));
            RenderButtonAvailability();
        }

        private ValueTask<bool> ConfirmBirthdayCorrectionAsync(
            GrownUpPrompt prompt,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (prompt.Action != GrownUpAction.BirthdayCorrection)
            {
                return new ValueTask<bool>(false);
            }
            if (m_BirthdayCorrectionConfirmed)
            {
                m_BirthdayCorrectionConfirmed = false;
                return new ValueTask<bool>(true);
            }
            m_BirthdayCorrectionArmed = true;
            return new ValueTask<bool>(false);
        }

        private void RenderSection(PlayerMenuSection section)
        {
            if (ActiveSection == PlayerMenuSection.Shop &&
                section != PlayerMenuSection.Shop)
            {
                m_Shop?.Close();
                CloseGrownUpChallenge();
            }
            ActiveSection = section;
            if (m_Title != null)
            {
                m_Title.text = Resolve(SectionTitleKey(section));
            }
            if (m_Context != null)
            {
                m_Context.text = Resolve("menu.context");
            }
            SetDetail(section switch
            {
                PlayerMenuSection.Journey => JourneyCopy(),
                PlayerMenuSection.Accessibility => Resolve(
                    "menu.accessibility.detail"),
                PlayerMenuSection.Atlas => AtlasCopy(),
                PlayerMenuSection.Captain => CaptainCopy(),
                PlayerMenuSection.Shop => ShopCopy(),
                PlayerMenuSection.Account => AccountCopy(),
                PlayerMenuSection.Birthday => BirthdayCopy(),
                _ => throw new ArgumentOutOfRangeException(nameof(section)),
            });
            ApplySettingsValues(m_Dependencies?.Settings.Current);
            ApplyBirthdayValues();
            RenderAtlas();
            RenderCaptain();
            RenderShop();
            RenderButtonAvailability();
        }

        private string JourneyCopy()
        {
            if (m_Save == null)
            {
                return Resolve("menu.journey.noSave");
            }
            if (m_Dependencies.Progression == null)
            {
                return Resolve("menu.journey.noSave");
            }
            return Task28English.Format(
                m_English,
                "menu.journey.detail",
                FriendlyScene(m_Dependencies.Progression.ResumeSceneName),
                m_Save.Mission.CheckpointOrdinal);
        }

        private string AtlasCopy()
        {
            var entries = UnlockedAtlasEntries();
            if (entries.Length == 0)
            {
                return Resolve("menu.atlas.none");
            }
            var selected = entries[Wrap(m_AtlasIndex, entries.Length)];
            var bodyKey = m_Dependencies.Settings.Current.ScienceDepth switch
            {
                ScienceDepth.Guided => selected.ShortTextKey,
                ScienceDepth.Balanced => selected.BalancedTextKey,
                ScienceDepth.Deep => selected.DeepTextKey,
                _ => selected.BalancedTextKey,
            };
            return Task28English.Format(
                m_English,
                "menu.atlas.entry",
                AtlasTitle(selected.StableId.Value),
                Resolve(bodyKey),
                FriendlyScienceDepth(m_Dependencies.Settings.Current.ScienceDepth),
                Wrap(m_AtlasIndex, entries.Length) + 1,
                entries.Length);
        }

        private string CaptainCopy()
        {
            if (m_Save == null)
            {
                return Resolve("menu.customization.noSave");
            }
            var selected = m_Catalog.Find(m_Save.CosmeticLoadout.Captain);
            return Task28English.Format(
                m_English,
                "menu.customization.detail",
                FriendlyFamily(m_Save.Captain.BodyFamilyId),
                FriendlyAppearance(m_Save.Captain.AppearancePresetId),
                FriendlySuit(m_Save.Captain.SuitCosmeticId),
                selected?.DisplayName ?? Resolve("common.notAvailable"));
        }

        private string ShopCopy()
        {
            if (m_Shop == null ||
                m_Shop.Availability != StoreAvailability.Available)
            {
                return Task28English.Format(
                    m_English,
                    "shop.unavailable.detail",
                    m_Catalog.Items.Count);
            }
            if (m_Shop.Products.Count == 0)
            {
                return Resolve("shop.loading");
            }
            var product = m_Shop.Products[Wrap(
                m_SelectedProductIndex,
                m_Shop.Products.Count)];
            return Task28English.Format(
                m_English,
                "shop.product",
                product.Title,
                product.Description,
                product.FormattedPrice,
                m_SelectedProductIndex + 1,
                m_Shop.Products.Count);
        }

        private string AccountCopy()
        {
            var state = m_Dependencies.Account?.Current;
            return state == null
                ? Resolve("account.unavailable")
                : Resolve(state.Connection switch
                {
                    AccountConnection.CloudAvailable => "account.available",
                    AccountConnection.Linked => "account.linked",
                    AccountConnection.Pending => "account.pending",
                    AccountConnection.Conflict => "account.conflict",
                    AccountConnection.OfflineGuest => "account.offline",
                    _ => "account.unavailable",
                });
        }

        private string BirthdayCopy()
        {
            if (m_Save?.Birthday?.HasValue != true)
            {
                return Resolve("birthday.notSet");
            }
            return Task28English.Format(
                m_English,
                "birthday.set",
                m_Save.Birthday.Day,
                m_Save.Birthday.Month,
                m_Save.Birthday.Year,
                Math.Max(0, m_Save.Birthday.CorrectionCount));
        }

        private void MutateSettings(Action<GameSettings> mutation)
        {
            RequireConfigured();
            var candidate = m_Dependencies.Settings.Current.Copy();
            mutation(candidate);
            if (!m_Dependencies.Settings.Apply(candidate))
            {
                throw new InvalidOperationException(
                    "The accessibility settings change was rejected.");
            }
            ApplySettingsValues(m_Dependencies.Settings.Current);
            if (ActiveSection == PlayerMenuSection.Accessibility)
            {
                SetDetail(Resolve("menu.accessibility.applied"));
            }
        }

        private void AdjustAccessibilitySetting(int direction)
        {
            MutateSettings(settings =>
            {
                var selected = (AccessibilitySetting)m_AccessibilitySettingIndex;
                switch (selected)
                {
                    case AccessibilitySetting.PilotingAssist:
                        settings.PilotingAssist = NextEnum(
                            settings.PilotingAssist,
                            direction);
                        break;
                    case AccessibilitySetting.ExplorationAssist:
                        settings.ExplorationAssist = NextEnum(
                            settings.ExplorationAssist,
                            direction);
                        break;
                    case AccessibilitySetting.ScienceDepth:
                        settings.ScienceDepth = NextEnum(
                            settings.ScienceDepth,
                            direction);
                        break;
                    case AccessibilitySetting.Captions:
                        settings.CaptionsEnabled = !settings.CaptionsEnabled;
                        break;
                    case AccessibilitySetting.TextScale:
                        settings.TextScale = Mathf.Clamp(
                            settings.TextScale + direction * 0.05f,
                            0.85f,
                            1.35f);
                        break;
                    case AccessibilitySetting.ReadableType:
                        settings.DyslexiaFriendlyFontEnabled =
                            !settings.DyslexiaFriendlyFontEnabled;
                        break;
                    case AccessibilitySetting.DialogueSpeed:
                        settings.DialogueSpeed = Mathf.Clamp(
                            settings.DialogueSpeed + direction * 0.1f,
                            0.5f,
                            2f);
                        break;
                    case AccessibilitySetting.ColorVision:
                        settings.ColorVisionMode = NextEnum(
                            settings.ColorVisionMode,
                            direction);
                        break;
                    case AccessibilitySetting.CameraShake:
                        settings.ReducedCameraShake = !settings.ReducedCameraShake;
                        break;
                    case AccessibilitySetting.Flashing:
                        settings.ReducedFlashing = !settings.ReducedFlashing;
                        break;
                    case AccessibilitySetting.Motion:
                        settings.ReducedMotion = !settings.ReducedMotion;
                        break;
                    case AccessibilitySetting.MotionBlur:
                        settings.MotionBlurEnabled = !settings.MotionBlurEnabled;
                        break;
                    case AccessibilitySetting.Particles:
                        settings.ParticleDensity = Mathf.Clamp01(
                            settings.ParticleDensity + direction * 0.1f);
                        break;
                    case AccessibilitySetting.Quality:
                        settings.PresentationQuality = NextEnum(
                            settings.PresentationQuality,
                            direction);
                        break;
                    case AccessibilitySetting.Music:
                        settings.MusicVolume = Mathf.Clamp01(
                            settings.MusicVolume + direction * 0.1f);
                        break;
                    case AccessibilitySetting.Dialogue:
                        settings.DialogueVolume = Mathf.Clamp01(
                            settings.DialogueVolume + direction * 0.1f);
                        break;
                    case AccessibilitySetting.Effects:
                        settings.EffectsVolume = Mathf.Clamp01(
                            settings.EffectsVolume + direction * 0.1f);
                        break;
                    case AccessibilitySetting.Haptics:
                        settings.HapticsEnabled = !settings.HapticsEnabled;
                        break;
                    case AccessibilitySetting.ControlSide:
                        settings.LeftHandedControls = !settings.LeftHandedControls;
                        break;
                    case AccessibilitySetting.TouchSensitivity:
                        settings.TouchSensitivity = Mathf.Clamp(
                            settings.TouchSensitivity + direction * 0.1f,
                            0.5f,
                            2f);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            });
        }

        private void ApplySettingsValues(GameSettings settings)
        {
            if (settings == null)
            {
                return;
            }
            var selected = (AccessibilitySetting)m_AccessibilitySettingIndex;
            if (m_AccessibilitySettingName != null)
            {
                m_AccessibilitySettingName.text = Resolve(
                    AccessibilitySettingKey(selected));
            }
            if (m_AccessibilitySettingValue != null)
            {
                m_AccessibilitySettingValue.text =
                    AccessibilitySettingValue(selected, settings);
            }
        }

        private void SetValue(TMP_Text label, bool enabled)
        {
            if (label != null)
            {
                label.text = Resolve(enabled ? Task28English.On : Task28English.Off);
            }
        }

        private void RenderButtonAvailability()
        {
            m_AccessibilityControlsRoot?.SetActive(
                ActiveSection == PlayerMenuSection.Accessibility);
            m_AtlasControlsRoot?.SetActive(
                ActiveSection == PlayerMenuSection.Atlas);
            m_CaptainControlsRoot?.SetActive(
                ActiveSection == PlayerMenuSection.Captain);
            m_ShopControlsRoot?.SetActive(
                ActiveSection == PlayerMenuSection.Shop);
            m_BirthdayControlsRoot?.SetActive(
                ActiveSection == PlayerMenuSection.Birthday);
            var canCustomize = m_Save != null &&
                (m_Dependencies.Modes.CurrentMode == GameMode.Clubhouse ||
                 m_Dependencies.Modes.CurrentMode == GameMode.Customization);
            foreach (var button in new[]
                     {
                         m_CaptainFamilyButton,
                         m_CaptainAppearanceButton,
                         m_CaptainSuitButton,
                         m_CaptainCosmeticButton,
                     })
            {
                if (button != null)
                {
                    button.interactable = canCustomize;
                }
            }
            var hasProducts = m_Shop != null && m_Shop.IsOpen &&
                m_Shop.Availability == StoreAvailability.Available &&
                m_Shop.Products.Count > 0;
            if (m_ShopPreviousButton != null)
            {
                m_ShopPreviousButton.interactable = hasProducts;
            }
            if (m_ShopNextButton != null)
            {
                m_ShopNextButton.interactable = hasProducts;
            }
            if (m_ShopPurchaseButton != null)
            {
                m_ShopPurchaseButton.interactable = hasProducts;
            }
            if (m_RestoreButton != null)
            {
                m_RestoreButton.interactable = m_Shop != null &&
                    m_Shop.IsOpen &&
                    m_Shop.Availability == StoreAvailability.Available;
            }
            if (m_GrownUpConfirmButton != null)
            {
                m_GrownUpConfirmButton.interactable =
                    m_GrownUpCompletion != null;
            }
            if (m_GrownUpCancelButton != null)
            {
                m_GrownUpCancelButton.interactable =
                    m_GrownUpCompletion != null;
            }
            if (m_LinkButton != null)
            {
                m_LinkButton.gameObject.SetActive(
                    ActiveSection == PlayerMenuSection.Account);
                m_LinkButton.interactable = m_Dependencies.Account != null &&
                    m_Dependencies.Account.Current.Connection ==
                        AccountConnection.CloudAvailable;
            }
            if (m_SyncButton != null)
            {
                m_SyncButton.gameObject.SetActive(
                    ActiveSection == PlayerMenuSection.Account);
                var connection = m_Dependencies.Account?.Current.Connection;
                m_SyncButton.interactable = connection == AccountConnection.Linked ||
                    connection == AccountConnection.Pending;
            }
            if (m_BirthdaySaveButton != null)
            {
                m_BirthdaySaveButton.interactable = m_Save != null &&
                    m_BirthdaySetup != null;
            }
            if (m_BirthdayConfirmButton != null)
            {
                m_BirthdayConfirmButton.interactable =
                    m_BirthdayCorrectionArmed;
            }
        }

        private void SeedBirthdaySelection()
        {
            if (m_Save?.Birthday?.HasValue == true)
            {
                m_BirthdayDay = m_Save.Birthday.Day;
                m_BirthdayMonth = m_Save.Birthday.Month;
                m_BirthdayYear = m_Save.Birthday.Year;
                return;
            }
            m_BirthdayDay = 1;
            m_BirthdayMonth = 1;
            m_BirthdayYear = DateTime.UtcNow.Year - 13;
        }

        private void ApplyBirthdayValues()
        {
            if (m_BirthdayDayValue != null)
            {
                m_BirthdayDayValue.text = m_BirthdayDay.ToString();
            }
            if (m_BirthdayMonthValue != null)
            {
                m_BirthdayMonthValue.text = m_BirthdayMonth.ToString();
            }
            if (m_BirthdayYearValue != null)
            {
                m_BirthdayYearValue.text = m_BirthdayYear.ToString();
            }
        }

        private void RequireOpenBirthday()
        {
            RequireConfigured();
            if (!IsOpen || ActiveSection != PlayerMenuSection.Birthday)
            {
                throw new InvalidOperationException(
                    "Birthday controls require the open private birthday panel.");
            }
        }

        private async void HandleCommand(
            GameplayInputMode mode,
            SemanticGameplayCommand command)
        {
            _ = mode;
            if (command != SemanticGameplayCommand.Pause || m_IsTransitioning)
            {
                return;
            }
            await RunUiAsync(IsOpen
                ? CloseAsync(m_Lifetime.Token)
                : OpenAsync(m_Lifetime.Token));
        }

        private void HandleSettingsChanged(GameSettings settings) =>
            ApplySettingsValues(settings);

        private void HandleShopChanged()
        {
            if (ActiveSection == PlayerMenuSection.Shop)
            {
                RenderShop();
                RenderButtonAvailability();
            }
        }

        private void HandleAccountChanged(AccountState state)
        {
            _ = state;
            if (ActiveSection == PlayerMenuSection.Account)
            {
                RenderSection(ActiveSection);
            }
        }

        private async ValueTask RunUiAsync(ValueTask operation)
        {
            try
            {
                await operation;
            }
            catch (OperationCanceledException) when (
                m_Lifetime == null || m_Lifetime.IsCancellationRequested ||
                destroyCancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                SetDetail(Resolve("menu.operationFailed"));
                Debug.LogException(exception, this);
            }
        }

        private void ReleaseRuntime()
        {
            if (m_Dependencies == null)
            {
                return;
            }
            if (m_InputBound)
            {
                m_Dependencies.Input.GameplayCommandPerformed -= HandleCommand;
            }
            m_Dependencies.Settings.SettingsChanged -= HandleSettingsChanged;
            if (m_Shop != null)
            {
                m_Shop.StateChanged -= HandleShopChanged;
                m_Shop.Dispose();
                m_Shop = null;
            }
            m_GrownUpGate = null;
            if (m_Dependencies.Account != null)
            {
                m_Dependencies.Account.StateChanged -= HandleAccountChanged;
            }
            m_Lifetime.Cancel();
            m_Lifetime.Dispose();
            m_Lifetime = null;
            m_InputBound = false;
            CloseGrownUpChallenge();
            m_BirthdayCorrectionArmed = false;
            m_BirthdayCorrectionConfirmed = false;
            IsOpen = false;
            m_PanelRoot?.SetActive(false);
            m_Dependencies = null;
            m_Save = null;
            m_BirthdaySetup = null;
        }

        private void OnDestroy() => ReleaseRuntime();

        private string Resolve(string key) => m_English.Resolve(key);

        private void SetDetail(string value)
        {
            if (m_Detail != null)
            {
                m_Detail.text = value ?? string.Empty;
            }
        }

        private bool CanCustomizeCaptain()
        {
            if (!IsOpen || m_Save == null)
            {
                SetDetail(Resolve("menu.customization.noSave"));
                return false;
            }
            if (m_Dependencies.Modes.CurrentMode != GameMode.Clubhouse &&
                m_Dependencies.Modes.CurrentMode != GameMode.Customization)
            {
                SetDetail(Resolve("menu.customization.clubhouseOnly"));
                return false;
            }
            return true;
        }

        private AtlasEntry[] UnlockedAtlasEntries()
        {
            if (m_Save?.AtlasEntryIds == null || m_AtlasEntries == null)
            {
                return Array.Empty<AtlasEntry>();
            }
            var unlocked = new HashSet<string>(
                m_Save.AtlasEntryIds,
                StringComparer.Ordinal);
            return m_AtlasEntries
                .Where(entry => entry != null &&
                    unlocked.Contains(entry.StableId.Value))
                .OrderBy(entry => entry.StableId.Value, StringComparer.Ordinal)
                .ToArray();
        }

        private void RenderAtlas()
        {
            if (m_AtlasValue != null)
            {
                m_AtlasValue.text = AtlasCopy();
            }
        }

        private void RenderCaptain()
        {
            if (m_CaptainValue != null)
            {
                m_CaptainValue.text = CaptainCopy();
            }
        }

        private void RenderShop()
        {
            if (m_ShopProductValue != null)
            {
                m_ShopProductValue.text = ShopCopy();
            }
            if (ActiveSection == PlayerMenuSection.Shop)
            {
                SetDetail(ShopCopy());
            }
        }

        private BirthdayAgeBand CurrentAgeBand()
        {
            return m_Save?.Birthday?.HasValue == true
                ? BirthdayPolicy.AgeBandOn(
                    BirthdayDate.FromState(m_Save.Birthday),
                    DateTimeOffset.UtcNow)
                : BirthdayAgeBand.Unknown;
        }

        private void ApplyGrownUpAnswer()
        {
            if (m_GrownUpAnswerValue != null)
            {
                m_GrownUpAnswerValue.text = m_GrownUpChallenge.RequiresArithmetic
                    ? m_GrownUpAnswer.ToString()
                    : Resolve("common.confirm");
            }
        }

        private void CompleteGrownUpChallenge(bool confirmed)
        {
            var completion = m_GrownUpCompletion;
            if (completion == null)
            {
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
            RenderButtonAvailability();
        }

        private void CloseGrownUpChallenge()
        {
            CompleteGrownUpChallenge(false);
            m_GrownUpChallengeRoot?.SetActive(false);
        }

        private static string AccessibilitySettingKey(
            AccessibilitySetting setting) => setting switch
        {
            AccessibilitySetting.PilotingAssist => "settings.pilotingAssist",
            AccessibilitySetting.ExplorationAssist => "settings.explorationAssist",
            AccessibilitySetting.ScienceDepth => "settings.scienceDepth",
            AccessibilitySetting.Captions => "settings.captions",
            AccessibilitySetting.TextScale => "settings.textScale",
            AccessibilitySetting.ReadableType => "settings.readableType",
            AccessibilitySetting.DialogueSpeed => "settings.dialogueSpeed",
            AccessibilitySetting.ColorVision => "settings.colorVision",
            AccessibilitySetting.CameraShake => "settings.cameraShake",
            AccessibilitySetting.Flashing => "settings.flashing",
            AccessibilitySetting.Motion => "settings.motion",
            AccessibilitySetting.MotionBlur => "settings.motionBlur",
            AccessibilitySetting.Particles => "settings.particles",
            AccessibilitySetting.Quality => "settings.quality",
            AccessibilitySetting.Music => "settings.music",
            AccessibilitySetting.Dialogue => "settings.dialogue",
            AccessibilitySetting.Effects => "settings.effects",
            AccessibilitySetting.Haptics => "settings.haptics",
            AccessibilitySetting.ControlSide => "settings.controlSide",
            AccessibilitySetting.TouchSensitivity => "settings.touchSensitivity",
            _ => throw new ArgumentOutOfRangeException(nameof(setting)),
        };

        private string AccessibilitySettingValue(
            AccessibilitySetting setting,
            GameSettings settings) => setting switch
        {
            AccessibilitySetting.PilotingAssist =>
                FriendlyAssist(settings.PilotingAssist),
            AccessibilitySetting.ExplorationAssist =>
                FriendlyAssist(settings.ExplorationAssist),
            AccessibilitySetting.ScienceDepth =>
                FriendlyScienceDepth(settings.ScienceDepth),
            AccessibilitySetting.Captions => OnOff(settings.CaptionsEnabled),
            AccessibilitySetting.TextScale => Percent(settings.TextScale),
            AccessibilitySetting.ReadableType =>
                OnOff(settings.DyslexiaFriendlyFontEnabled),
            AccessibilitySetting.DialogueSpeed =>
                Multiplier(settings.DialogueSpeed),
            AccessibilitySetting.ColorVision => settings.ColorVisionMode switch
            {
                ColorVisionMode.Standard => Resolve("value.standard"),
                ColorVisionMode.Protanopia => Resolve("value.protanopia"),
                ColorVisionMode.Deuteranopia => Resolve("value.deuteranopia"),
                ColorVisionMode.Tritanopia => Resolve("value.tritanopia"),
                _ => Resolve("value.standard"),
            },
            AccessibilitySetting.CameraShake =>
                ReducedOrFull(settings.ReducedCameraShake),
            AccessibilitySetting.Flashing =>
                ReducedOrFull(settings.ReducedFlashing),
            AccessibilitySetting.Motion =>
                ReducedOrFull(settings.ReducedMotion),
            AccessibilitySetting.MotionBlur => OnOff(settings.MotionBlurEnabled),
            AccessibilitySetting.Particles => Percent(settings.ParticleDensity),
            AccessibilitySetting.Quality =>
                FriendlyQuality(settings.PresentationQuality),
            AccessibilitySetting.Music => Percent(settings.MusicVolume),
            AccessibilitySetting.Dialogue => Percent(settings.DialogueVolume),
            AccessibilitySetting.Effects => Percent(settings.EffectsVolume),
            AccessibilitySetting.Haptics => OnOff(settings.HapticsEnabled),
            AccessibilitySetting.ControlSide => Resolve(
                settings.LeftHandedControls ? Task28English.Left : Task28English.Right),
            AccessibilitySetting.TouchSensitivity =>
                Multiplier(settings.TouchSensitivity),
            _ => throw new ArgumentOutOfRangeException(nameof(setting)),
        };

        private string OnOff(bool value) =>
            Resolve(value ? Task28English.On : Task28English.Off);

        private string ReducedOrFull(bool value) =>
            Resolve(value ? Task28English.Reduced : Task28English.Full);

        private string Percent(float value) => Task28English.Format(
            m_English,
            "common.percent",
            Mathf.RoundToInt(value * 100f));

        private string Multiplier(float value) => Task28English.Format(
            m_English,
            "common.multiplier",
            value);

        private string FriendlyAssist(AssistLevel value) => value switch
        {
            AssistLevel.Guided => Resolve("value.guided"),
            AssistLevel.Balanced => Resolve("value.balanced"),
            AssistLevel.Ace => Resolve("value.ace"),
            _ => Resolve("value.balanced"),
        };

        private string FriendlyScienceDepth(ScienceDepth value) => value switch
        {
            ScienceDepth.Guided => Resolve("value.guided"),
            ScienceDepth.Balanced => Resolve("value.balanced"),
            ScienceDepth.Deep => Resolve("value.deep"),
            _ => Resolve("value.balanced"),
        };

        private string FriendlyQuality(PresentationQuality value) => value switch
        {
            PresentationQuality.Performance => Resolve("value.performance"),
            PresentationQuality.Balanced => Resolve("value.balanced"),
            PresentationQuality.Cinematic => Resolve("value.cinematic"),
            PresentationQuality.HighFrameRate => Resolve("value.highFrameRate"),
            _ => Resolve("value.balanced"),
        };

        private static int Wrap(int index, int count)
        {
            if (count <= 0)
            {
                return 0;
            }
            return ((index % count) + count) % count;
        }

        private static T NextEnum<T>(T value, int direction)
            where T : struct, Enum
        {
            var values = (T[])Enum.GetValues(typeof(T));
            var current = Array.IndexOf(values, value);
            return values[Wrap(current + direction, values.Length)];
        }

        private void RequireConfigured()
        {
            if (m_Dependencies == null)
            {
                throw new InvalidOperationException(
                    "PlayerMenuController must be configured before use.");
            }
        }

        private static string SectionTitleKey(PlayerMenuSection section) =>
            section switch
            {
                PlayerMenuSection.Journey => "menu.journey",
                PlayerMenuSection.Accessibility => "hud.accessibility",
                PlayerMenuSection.Atlas => "menu.atlas",
                PlayerMenuSection.Captain => "menu.customization",
                PlayerMenuSection.Shop => "menu.shop",
                PlayerMenuSection.Account => "menu.account",
                PlayerMenuSection.Birthday => "birthday.title",
                _ => throw new ArgumentOutOfRangeException(nameof(section)),
            };

        private string FriendlyScene(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Resolve("location.clubhouse");
            }
            var normalized = value.ToLowerInvariant();
            if (normalized.Contains("mirra")) return Resolve("location.mirra");
            if (normalized.Contains("koro")) return Resolve("location.koro");
            if (normalized.Contains("vesper") || normalized.Contains("flight"))
                return Resolve("location.vesper");
            if (normalized.Contains("aster")) return Resolve("location.aster");
            return Resolve("location.clubhouse");
        }

        private string FriendlyFamily(string id) => id switch
        {
            "captain.family.a" => Resolve("captain.family.compact"),
            "captain.family.b" => Resolve("captain.family.average"),
            "captain.family.c" => Resolve("captain.family.tallBroad"),
            _ => Resolve("captain.family.custom"),
        };

        private string FriendlyAppearance(string id)
        {
            var index = Array.IndexOf(CaptainAppearances, id);
            return index >= 0
                ? Task28English.Format(
                    m_English,
                    "captain.appearance",
                    index + 1)
                : Resolve("common.notAvailable");
        }

        private string FriendlySuit(string id) => id switch
        {
            "suit.clubhouse" => Resolve("captain.suit.clubhouse"),
            "suit.signal" => Resolve("captain.suit.signal"),
            "suit.flight" => Resolve("captain.suit.flight"),
            _ => Resolve("common.notAvailable"),
        };

        private string AtlasTitle(string id) => id switch
        {
            "atlas.mirra.temperature-gradient" => Resolve("atlas.mirra.title"),
            "atlas.koro.geyser-spectra" => Resolve("atlas.koro.title"),
            _ => Resolve("menu.atlas"),
        };

    }
}
