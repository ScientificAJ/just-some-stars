using UnityEngine;

namespace JustSomeStars.Runtime.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        [SerializeField]
        private bool m_ApplyHorizontal = true;

        [SerializeField]
        private bool m_ApplyVertical = true;

        private RectTransform m_RectTransform;
        private Rect m_LastSafeArea;
        private int m_LastScreenWidth;
        private int m_LastScreenHeight;

        private void OnEnable()
        {
            m_RectTransform = GetComponent<RectTransform>();
            ApplyIfChanged(force: true);
        }

        private void LateUpdate()
        {
            ApplyIfChanged(force: false);
        }

        private void ApplyIfChanged(bool force)
        {
            var screenWidth = Screen.width;
            var screenHeight = Screen.height;
            var safeArea = Screen.safeArea;
            if (!force &&
                safeArea == m_LastSafeArea &&
                screenWidth == m_LastScreenWidth &&
                screenHeight == m_LastScreenHeight)
            {
                return;
            }

            m_LastSafeArea = safeArea;
            m_LastScreenWidth = screenWidth;
            m_LastScreenHeight = screenHeight;
            if (screenWidth <= 0 || screenHeight <= 0)
            {
                return;
            }

            m_RectTransform.anchorMin = new Vector2(
                m_ApplyHorizontal ? safeArea.xMin / screenWidth : 0f,
                m_ApplyVertical ? safeArea.yMin / screenHeight : 0f);
            m_RectTransform.anchorMax = new Vector2(
                m_ApplyHorizontal ? safeArea.xMax / screenWidth : 1f,
                m_ApplyVertical ? safeArea.yMax / screenHeight : 1f);
            m_RectTransform.offsetMin = Vector2.zero;
            m_RectTransform.offsetMax = Vector2.zero;
        }
    }
}
