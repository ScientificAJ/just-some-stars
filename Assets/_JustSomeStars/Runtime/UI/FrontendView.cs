using System;
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
        private TMP_Text m_PanelTitle;

        [SerializeField]
        private TMP_Text m_PanelBody;

        [SerializeField]
        private ScrollRect m_PanelScrollRect;

        [SerializeField]
        private Button m_CloseButton;

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
            m_PanelTitle.text = title ?? string.Empty;
            m_PanelBody.text = body ?? string.Empty;
            m_PanelRoot.SetActive(true);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(
                m_PanelBody.rectTransform);
            Canvas.ForceUpdateCanvases();
            m_PanelScrollRect.StopMovement();
            m_PanelScrollRect.verticalNormalizedPosition = 1f;
        }

        public void HidePanel()
        {
            m_PanelRoot.SetActive(false);
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
                   m_PanelTitle != null &&
                   m_PanelBody != null &&
                   m_PanelScrollRect != null &&
                   m_CloseButton != null;
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
