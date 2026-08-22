using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JustSomeStars.Runtime.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button), typeof(RectTransform))]
    public sealed class FrontendButtonVisual : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        ISelectHandler,
        IDeselectHandler
    {
        [SerializeField]
        private CanvasGroup m_EdgeGlow;

        [SerializeField, Range(0f, 2f)]
        private float m_MotionScale = 1f;

        [SerializeField]
        private float m_HoverScale = 1.018f;

        [SerializeField]
        private float m_PressScale = 0.975f;

        [SerializeField]
        private float m_Response = 15f;

        private Button m_Button;
        private RectTransform m_RectTransform;
        private Vector3 m_RestScale;
        private bool m_IsHovered;
        private bool m_IsPressed;
        private bool m_IsSelected;

        private void Awake()
        {
            m_Button = GetComponent<Button>();
            m_RectTransform = GetComponent<RectTransform>();
            m_RestScale = m_RectTransform.localScale;
            if (m_EdgeGlow != null)
            {
                m_EdgeGlow.alpha = 0f;
                m_EdgeGlow.blocksRaycasts = false;
                m_EdgeGlow.interactable = false;
            }
        }

        private void OnDisable()
        {
            m_IsHovered = false;
            m_IsPressed = false;
            m_IsSelected = false;
            if (m_RectTransform != null)
            {
                m_RectTransform.localScale = m_RestScale;
            }

            if (m_EdgeGlow != null)
            {
                m_EdgeGlow.alpha = 0f;
            }
        }

        private void Update()
        {
            if (m_Button == null || m_RectTransform == null)
            {
                return;
            }

            var canRespond = m_Button.interactable && m_MotionScale > 0f;
            var targetScale = 1f;
            if (canRespond && m_IsPressed)
            {
                targetScale = m_PressScale;
            }
            else if (canRespond && (m_IsHovered || m_IsSelected))
            {
                targetScale = m_HoverScale;
            }

            var response = 1f - Mathf.Exp(
                -m_Response * Time.unscaledDeltaTime /
                Mathf.Max(0.05f, m_MotionScale));
            m_RectTransform.localScale = Vector3.Lerp(
                m_RectTransform.localScale,
                m_RestScale * targetScale,
                response);
            if (m_EdgeGlow != null)
            {
                var targetGlow = canRespond &&
                                 (m_IsHovered || m_IsPressed || m_IsSelected)
                    ? 0.34f
                    : 0f;
                m_EdgeGlow.alpha = Mathf.Lerp(
                    m_EdgeGlow.alpha,
                    targetGlow,
                    response);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _ = eventData;
            m_IsHovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _ = eventData;
            m_IsHovered = false;
            m_IsPressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _ = eventData;
            m_IsPressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _ = eventData;
            m_IsPressed = false;
        }

        public void OnSelect(BaseEventData eventData)
        {
            _ = eventData;
            m_IsSelected = true;
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _ = eventData;
            m_IsSelected = false;
        }
    }
}
