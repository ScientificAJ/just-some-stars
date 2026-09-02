using System;
using JustSomeStars.Runtime.Atlas;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JustSomeStars.Runtime.UI
{
    [DisallowMultipleComponent]
    public sealed class FrontendView :
        MonoBehaviour,
        IFrontendView,
        IFrontendLaunchView
    {
        [SerializeField]
        private TMP_Text m_TitleSemantic;

        [SerializeField]
        private TMP_Text m_StatusLabel;

        [SerializeField]
        private TMP_Text m_VersionLabel;

        [SerializeField]
        private Button m_NewGameButton;

        [SerializeField]
        private TMP_Text m_NewGameButtonLabel;

        [SerializeField]
        private TMP_Text m_NewGameExplanation;

        [SerializeField]
        private Button m_ContinueButton;

        [SerializeField]
        private TMP_Text m_ContinueButtonLabel;

        [SerializeField]
        private TMP_Text m_ContinueState;

        [SerializeField]
        private TMP_Text m_ContinueExplanation;

        [SerializeField]
        private Button m_SettingsButton;

        [SerializeField]
        private TMP_Text m_SettingsButtonLabel;

        [SerializeField]
        private Button m_CreditsButton;

        [SerializeField]
        private TMP_Text m_CreditsButtonLabel;

        [SerializeField]
        private Button m_PrivacyButton;

        [SerializeField]
        private TMP_Text m_PrivacyButtonLabel;

        [SerializeField]
        private GameObject m_PanelRoot;

        [SerializeField]
        private RectTransform m_PanelFrame;

        [SerializeField]
        private TMP_Text m_PanelTitle;

        [SerializeField]
        private TMP_Text m_LocalPanelLabel;

        [SerializeField]
        private TMP_Text m_PanelBody;

        [SerializeField]
        private ScrollRect m_PanelScrollRect;

        [SerializeField]
        private GameObject m_SettingsControlsRoot;

        [SerializeField]
        private Button m_CloseButton;

        [SerializeField]
        private TMP_Text m_CloseButtonLabel;

        [SerializeField]
        private FrontendMotionDirector m_MotionDirector;

        private bool m_IsListening;

        public bool IsReady => HasCompleteBindings();

        public event Action ContinueRequested;

        public event Action NewGameRequested;

        public event Action SettingsRequested;

        public event Action CreditsRequested;

        public event Action PrivacyRequested;

        public event Action CloseRequested;

        private void Awake()
        {
            if (!HasCompleteBindings())
            {
                Debug.LogError(
                    "[JSS Frontend] FrontendView has incomplete scene bindings.",
                    this);
                enabled = false;
                return;
            }

            m_PanelRoot.SetActive(false);
            m_SettingsControlsRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (m_IsListening || !HasCompleteBindings())
            {
                return;
            }

            m_ContinueButton.onClick.AddListener(HandleContinueClicked);
            m_NewGameButton.onClick.AddListener(HandleNewGameClicked);
            m_SettingsButton.onClick.AddListener(HandleSettingsClicked);
            m_CreditsButton.onClick.AddListener(HandleCreditsClicked);
            m_PrivacyButton.onClick.AddListener(HandlePrivacyClicked);
            m_CloseButton.onClick.AddListener(HandleCloseClicked);
            m_IsListening = true;
        }

        private void OnDisable()
        {
            if (!m_IsListening)
            {
                return;
            }

            m_ContinueButton.onClick.RemoveListener(HandleContinueClicked);
            m_NewGameButton.onClick.RemoveListener(HandleNewGameClicked);
            m_SettingsButton.onClick.RemoveListener(HandleSettingsClicked);
            m_CreditsButton.onClick.RemoveListener(HandleCreditsClicked);
            m_PrivacyButton.onClick.RemoveListener(HandlePrivacyClicked);
            m_CloseButton.onClick.RemoveListener(HandleCloseClicked);
            m_IsListening = false;
        }

        public void PresentVersion(string versionText)
        {
            m_VersionLabel.text = versionText ?? string.Empty;
        }

        public void PresentContinue(bool interactable, string explanation)
        {
            m_ContinueButton.interactable = interactable;
            m_ContinueExplanation.text = explanation ?? string.Empty;
        }

        public void PresentLocalizedChrome(LocalizedEnglishCatalog english)
        {
            if (english == null)
            {
                throw new ArgumentNullException(nameof(english));
            }
            m_TitleSemantic.text = english.Resolve(Task28English.FrontendTitle);
            m_StatusLabel.text = english.Resolve(Task28English.FrontendStatus);
            m_NewGameButtonLabel.text = english.Resolve(Task28English.FrontendNewGame);
            m_ContinueButtonLabel.text = english.Resolve(Task28English.FrontendContinue);
            m_SettingsButtonLabel.text = english.Resolve(Task28English.SettingsTitle);
            m_CreditsButtonLabel.text = english.Resolve(Task28English.CreditsTitle)
                .Replace(" & Licenses", string.Empty);
            m_PrivacyButtonLabel.text = english.Resolve(Task28English.PrivacyTitle);
            m_LocalPanelLabel.text = english.Resolve(Task28English.LocalPanelNote);
            m_CloseButtonLabel.text = english.Resolve(Task28English.Close);
        }

        public void PresentLaunch(FrontendLaunchPresentation presentation)
        {
            m_NewGameButton.interactable = presentation.NewGameInteractable;
            m_NewGameExplanation.text = presentation.NewGameExplanation;
            m_ContinueButton.interactable = presentation.ContinueInteractable;
            m_ContinueState.text = presentation.ContinueState;
            m_ContinueExplanation.text = presentation.ContinueExplanation;
        }

        public void ShowPanel(string title, string body)
        {
            var safeTitle = title ?? string.Empty;
            var isSettings = safeTitle == "Settings";
            ApplyPanelLayout(
                isCredits: safeTitle == "Credits & Licenses",
                isSettings);
            m_SettingsControlsRoot.SetActive(false);
            m_PanelScrollRect.gameObject.SetActive(!isSettings);
            m_PanelTitle.text = safeTitle;
            m_PanelBody.richText = safeTitle != "Credits & Licenses";
            m_PanelBody.text = body ?? string.Empty;
            m_PanelRoot.SetActive(true);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(
                m_PanelBody.rectTransform);
            Canvas.ForceUpdateCanvases();
            m_PanelScrollRect.StopMovement();
            m_PanelScrollRect.verticalNormalizedPosition = 1f;
            m_MotionDirector.ShowPanel(m_PanelRoot);
        }

        private void ApplyPanelLayout(bool isCredits, bool isSettings)
        {
            var frameSize = m_PanelFrame.sizeDelta;
            frameSize.y = isCredits ? 441f : 424f;
            m_PanelFrame.sizeDelta = frameSize;

            var titlePosition = m_PanelTitle.rectTransform.anchoredPosition;
            titlePosition.y = isCredits ? -108f : -128f;
            m_PanelTitle.rectTransform.anchoredPosition = titlePosition;

            var scrollRect = m_PanelScrollRect.GetComponent<RectTransform>();
            var scrollPosition = scrollRect.anchoredPosition;
            scrollPosition.y = isCredits ? -160f : -194f;
            scrollRect.anchoredPosition = scrollPosition;
            var scrollSize = scrollRect.sizeDelta;
            scrollSize.y = isCredits ? 180f : 118f;
            scrollRect.sizeDelta = scrollSize;

            if (isSettings)
            {
                var settingsRect =
                    m_SettingsControlsRoot.GetComponent<RectTransform>();
                var settingsPosition = settingsRect.anchoredPosition;
                settingsPosition.y = -194f;
                settingsRect.anchoredPosition = settingsPosition;
                var settingsSize = settingsRect.sizeDelta;
                settingsSize.y = 118f;
                settingsRect.sizeDelta = settingsSize;
            }

            var closeRect = m_CloseButton.GetComponent<RectTransform>();
            var closePosition = closeRect.anchoredPosition;
            closePosition.y = isCredits ? -343f : -313f;
            closeRect.anchoredPosition = closePosition;
        }

        public void HidePanel()
        {
            m_MotionDirector.HidePanel(m_PanelRoot);
        }

        private bool HasCompleteBindings()
        {
            return m_VersionLabel != null &&
                   m_TitleSemantic != null &&
                   m_StatusLabel != null &&
                   m_NewGameButton != null &&
                   m_NewGameButtonLabel != null &&
                   m_NewGameExplanation != null &&
                   m_ContinueButton != null &&
                   m_ContinueButtonLabel != null &&
                   m_ContinueState != null &&
                   m_ContinueExplanation != null &&
                   m_SettingsButton != null &&
                   m_SettingsButtonLabel != null &&
                   m_CreditsButton != null &&
                   m_CreditsButtonLabel != null &&
                   m_PrivacyButton != null &&
                   m_PrivacyButtonLabel != null &&
                   m_PanelRoot != null &&
                   m_PanelFrame != null &&
                   m_PanelTitle != null &&
                   m_LocalPanelLabel != null &&
                   m_PanelBody != null &&
                   m_PanelScrollRect != null &&
                   m_SettingsControlsRoot != null &&
                   m_CloseButton != null &&
                   m_CloseButtonLabel != null &&
                   m_MotionDirector != null;
        }

        private void HandleNewGameClicked()
        {
            NewGameRequested?.Invoke();
        }

        private void HandleContinueClicked()
        {
            ContinueRequested?.Invoke();
        }

        private void HandleSettingsClicked()
        {
            SettingsRequested?.Invoke();
        }

        private void HandleCreditsClicked()
        {
            CreditsRequested?.Invoke();
        }

        private void HandlePrivacyClicked()
        {
            PrivacyRequested?.Invoke();
        }

        private void HandleCloseClicked()
        {
            CloseRequested?.Invoke();
        }
    }
}
