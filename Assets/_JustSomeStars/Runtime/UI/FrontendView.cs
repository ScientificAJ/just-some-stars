using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JustSomeStars.Runtime.UI
{
    [DisallowMultipleComponent]
    public sealed class FrontendView : MonoBehaviour, IFrontendView
    {
        [SerializeField]
        private TMP_Text m_VersionLabel;

        [SerializeField]
        private Button m_ContinueButton;

        [SerializeField]
        private TMP_Text m_ContinueExplanation;

        [SerializeField]
        private Button m_SettingsButton;

        [SerializeField]
        private Button m_CreditsButton;

        [SerializeField]
        private Button m_PrivacyButton;

        [SerializeField]
        private GameObject m_PanelRoot;

        [SerializeField]
        private RectTransform m_PanelFrame;

        [SerializeField]
        private TMP_Text m_PanelTitle;

        [SerializeField]
        private TMP_Text m_PanelBody;

        [SerializeField]
        private ScrollRect m_PanelScrollRect;

        [SerializeField]
        private GameObject m_SettingsControlsRoot;

        [SerializeField]
        private Button m_CloseButton;

        [SerializeField]
        private FrontendMotionDirector m_MotionDirector;

        private bool m_IsListening;

        public bool IsReady => HasCompleteBindings();

        public event Action ContinueRequested;

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
            m_PanelBody.text = FormatPanelBody(safeTitle, body ?? string.Empty);
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

        private static string FormatPanelBody(string title, string body)
        {
            if (title == "Settings")
            {
                return body;
            }

            if (title == "Privacy")
            {
                return "This Development Flight does\n" +
                       "not ask for an account, collect\n" +
                       "gameplay progress, open web\n" +
                       "links, or offer purchases.";
            }

            if (title != "Credits & Licenses")
            {
                return body;
            }

            var formatted = StripLeadingLineWhitespace(
                body.Replace("\r\n", "\n"));
            formatted = formatted.Replace(
                "Just Some Stars is being built by ScientificAJ. This " +
                "Development Flight contains a launch screen, not finished " +
                "gameplay.",
                "Just Some Stars is being built by\n" +
                "ScientificAJ. This Development Flight\n" +
                "contains a launch screen, not\n" +
                "finished gameplay.");
            formatted = formatted.Replace(
                "Liberation Sans\n\n",
                "<b><color=#F7D7AB>Liberation Sans</color></b>\n");
            formatted = formatted.Replace(
                "Android open-source components\n\n",
                "<b><color=#F7D7AB>Android open-source components" +
                "</color></b>\n");

            const string apacheMarker = "\n\nApache License 2.0\n\n";
            var apacheIndex = formatted.LastIndexOf(
                apacheMarker,
                StringComparison.Ordinal);
            if (apacheIndex < 0)
            {
                return formatted;
            }

            return formatted.Substring(0, apacheIndex) +
                   "\n\n<b><color=#F7D7AB>Apache License 2.0</color></b>\n" +
                   "<size=14><line-height=150%>" +
                   formatted.Substring(apacheIndex + apacheMarker.Length)
                       .TrimStart('\n') +
                   "</line-height></size>";
        }

        private static string StripLeadingLineWhitespace(string value)
        {
            var lines = value.Split('\n');
            var result = new StringBuilder(value.Length);
            for (var index = 0; index < lines.Length; index++)
            {
                if (index > 0)
                {
                    result.Append('\n');
                }

                result.Append(lines[index].TrimStart(' ', '\t'));
            }

            return result.ToString();
        }

        public void HidePanel()
        {
            m_MotionDirector.HidePanel(m_PanelRoot);
        }

        private bool HasCompleteBindings()
        {
            return m_VersionLabel != null &&
                   m_ContinueButton != null &&
                   m_ContinueExplanation != null &&
                   m_SettingsButton != null &&
                   m_CreditsButton != null &&
                   m_PrivacyButton != null &&
                   m_PanelRoot != null &&
                   m_PanelFrame != null &&
                   m_PanelTitle != null &&
                   m_PanelBody != null &&
                   m_PanelScrollRect != null &&
                   m_SettingsControlsRoot != null &&
                   m_CloseButton != null &&
                   m_MotionDirector != null;
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
