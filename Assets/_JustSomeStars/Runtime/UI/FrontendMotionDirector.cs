using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace JustSomeStars.Runtime.UI
{
    [DisallowMultipleComponent]
    public sealed class FrontendMotionDirector : MonoBehaviour
    {
        [SerializeField]
        private CanvasGroup m_TitleGroup;

        [SerializeField]
        private CanvasGroup m_StatusGroup;

        [SerializeField]
        private CanvasGroup m_MenuGroup;

        [SerializeField]
        private RectTransform m_TitleTransform;

        [SerializeField]
        private RectTransform m_MenuTransform;

        [SerializeField]
        private CanvasGroup m_PanelGroup;

        [SerializeField]
        private RectTransform m_PanelFrame;

        [SerializeField]
        private Graphic m_StarGlints;

        [SerializeField]
        private Graphic m_SignalBeam;

        [SerializeField]
        private Graphic m_TelescopeLensGlow;

        [SerializeField, Range(0f, 2f)]
        private float m_MotionScale = 1f;

        [SerializeField, Min(0.01f)]
        private float m_EntranceDuration = 1.15f;

        [SerializeField, Min(0.01f)]
        private float m_PanelInDuration = 0.28f;

        [SerializeField, Min(0.01f)]
        private float m_PanelOutDuration = 0.16f;

        private Coroutine m_EntranceRoutine;
        private Coroutine m_PanelRoutine;
        private Vector2 m_TitleRestPosition;
        private Vector2 m_MenuRestPosition;
        private Vector2 m_PanelRestPosition;
        private Vector3 m_PanelRestScale;
        private Color m_StarColor;
        private Color m_SignalColor;
        private Color m_LensColor;

        public bool IsSettled { get; private set; }

        public int ActiveSequenceCount { get; private set; }

        public float MotionScale
        {
            get => m_MotionScale;
            set => m_MotionScale = Mathf.Clamp(value, 0f, 2f);
        }

        private void Awake()
        {
            if (!HasCompleteBindings())
            {
                Debug.LogError(
                    "[JSS Frontend] FrontendMotionDirector has incomplete bindings.",
                    this);
                enabled = false;
                return;
            }

            m_TitleRestPosition = m_TitleTransform.anchoredPosition;
            m_MenuRestPosition = m_MenuTransform.anchoredPosition;
            m_PanelRestPosition = m_PanelFrame.anchoredPosition;
            m_PanelRestScale = m_PanelFrame.localScale;
            m_StarColor = m_StarGlints.color;
            m_SignalColor = m_SignalBeam.color;
            m_LensColor = m_TelescopeLensGlow.color;
        }

        private void OnEnable()
        {
            if (HasCompleteBindings())
            {
                PlayEntrance();
            }
        }

        private void OnDisable()
        {
            StopOwnedCoroutine(ref m_EntranceRoutine);
            StopOwnedCoroutine(ref m_PanelRoutine);
            ActiveSequenceCount = 0;
            ResetEntranceVisuals();
            ResetPanelVisuals();
        }

        private void Update()
        {
            if (!IsSettled || m_MotionScale <= 0f)
            {
                return;
            }

            var time = Time.unscaledTime;
            SetAlpha(m_StarGlints, m_StarColor.a *
                Mathf.Lerp(0.18f, 0.52f, Wave01(time * 0.47f)));
            SetAlpha(m_SignalBeam, m_SignalColor.a *
                Mathf.Lerp(0.06f, 0.23f, Wave01(time * 0.31f + 0.7f)));
            SetAlpha(m_TelescopeLensGlow, m_LensColor.a *
                Mathf.Lerp(0.08f, 0.3f, Wave01(time * 0.72f + 1.4f)));
        }

        public void PlayEntrance()
        {
            StopOwnedCoroutine(ref m_EntranceRoutine);
            if (m_MotionScale <= 0f)
            {
                ResetEntranceVisuals();
                IsSettled = true;
                return;
            }

            m_EntranceRoutine = StartCoroutine(PlayEntranceRoutine());
        }

        public void ShowPanel(GameObject panelRoot)
        {
            if (panelRoot == null)
            {
                return;
            }

            StopOwnedCoroutine(ref m_PanelRoutine);
            panelRoot.SetActive(true);
            if (m_MotionScale <= 0f)
            {
                ResetPanelVisuals();
                return;
            }

            m_PanelRoutine = StartCoroutine(ShowPanelRoutine());
        }

        public void HidePanel(GameObject panelRoot)
        {
            if (panelRoot == null || !panelRoot.activeSelf)
            {
                return;
            }

            StopOwnedCoroutine(ref m_PanelRoutine);
            if (m_MotionScale <= 0f)
            {
                panelRoot.SetActive(false);
                ResetPanelVisuals();
                return;
            }

            m_PanelRoutine = StartCoroutine(HidePanelRoutine(panelRoot));
        }

        private IEnumerator PlayEntranceRoutine()
        {
            IsSettled = false;
            ActiveSequenceCount++;
            m_TitleGroup.alpha = 0f;
            m_StatusGroup.alpha = 0f;
            m_MenuGroup.alpha = 0f;
            m_TitleTransform.anchoredPosition =
                m_TitleRestPosition + new Vector2(-22f, 7f);
            m_MenuTransform.anchoredPosition =
                m_MenuRestPosition + new Vector2(0f, -24f);
            SetAlpha(m_StarGlints, 0f);
            SetAlpha(m_SignalBeam, 0f);
            SetAlpha(m_TelescopeLensGlow, 0f);

            var duration = m_EntranceDuration * m_MotionScale;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                var eased = EaseOutCubic(progress);
                m_TitleGroup.alpha = Mathf.Clamp01(progress / 0.62f);
                m_StatusGroup.alpha = Mathf.Clamp01((progress - 0.18f) / 0.55f);
                m_MenuGroup.alpha = Mathf.Clamp01((progress - 0.28f) / 0.58f);
                m_TitleTransform.anchoredPosition = Vector2.LerpUnclamped(
                    m_TitleRestPosition + new Vector2(-22f, 7f),
                    m_TitleRestPosition,
                    eased);
                m_MenuTransform.anchoredPosition = Vector2.LerpUnclamped(
                    m_MenuRestPosition + new Vector2(0f, -24f),
                    m_MenuRestPosition,
                    EaseOutBack(progress));
                yield return null;
            }

            ResetEntranceVisuals();
            IsSettled = true;
            ActiveSequenceCount--;
            m_EntranceRoutine = null;
        }

        private IEnumerator ShowPanelRoutine()
        {
            ActiveSequenceCount++;
            m_PanelGroup.alpha = 0f;
            m_PanelFrame.anchoredPosition =
                m_PanelRestPosition + new Vector2(0f, -16f);
            m_PanelFrame.localScale = m_PanelRestScale * 0.965f;
            var duration = m_PanelInDuration * m_MotionScale;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                var eased = EaseOutBack(progress);
                m_PanelGroup.alpha = Mathf.Clamp01(progress / 0.72f);
                m_PanelFrame.anchoredPosition = Vector2.LerpUnclamped(
                    m_PanelRestPosition + new Vector2(0f, -16f),
                    m_PanelRestPosition,
                    eased);
                m_PanelFrame.localScale = Vector3.LerpUnclamped(
                    m_PanelRestScale * 0.965f,
                    m_PanelRestScale,
                    eased);
                yield return null;
            }

            ResetPanelVisuals();
            ActiveSequenceCount--;
            m_PanelRoutine = null;
        }

        private IEnumerator HidePanelRoutine(GameObject panelRoot)
        {
            ActiveSequenceCount++;
            var startAlpha = m_PanelGroup.alpha;
            var startPosition = m_PanelFrame.anchoredPosition;
            var startScale = m_PanelFrame.localScale;
            var duration = m_PanelOutDuration * m_MotionScale;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                var eased = SmoothStep(progress);
                m_PanelGroup.alpha = Mathf.Lerp(startAlpha, 0f, eased);
                m_PanelFrame.anchoredPosition = Vector2.Lerp(
                    startPosition,
                    m_PanelRestPosition + new Vector2(0f, -9f),
                    eased);
                m_PanelFrame.localScale = Vector3.Lerp(
                    startScale,
                    m_PanelRestScale * 0.985f,
                    eased);
                yield return null;
            }

            panelRoot.SetActive(false);
            ResetPanelVisuals();
            ActiveSequenceCount--;
            m_PanelRoutine = null;
        }

        private void StopOwnedCoroutine(ref Coroutine routine)
        {
            if (routine == null)
            {
                return;
            }

            StopCoroutine(routine);
            routine = null;
            ActiveSequenceCount = Mathf.Max(0, ActiveSequenceCount - 1);
        }

        private void ResetEntranceVisuals()
        {
            if (!HasCompleteBindings())
            {
                return;
            }

            m_TitleGroup.alpha = 1f;
            m_StatusGroup.alpha = 1f;
            m_MenuGroup.alpha = 1f;
            m_TitleTransform.anchoredPosition = m_TitleRestPosition;
            m_MenuTransform.anchoredPosition = m_MenuRestPosition;
            SetAlpha(m_StarGlints, 0f);
            SetAlpha(m_SignalBeam, 0f);
            SetAlpha(m_TelescopeLensGlow, 0f);
        }

        private void ResetPanelVisuals()
        {
            if (m_PanelGroup == null || m_PanelFrame == null)
            {
                return;
            }

            m_PanelGroup.alpha = 1f;
            m_PanelFrame.anchoredPosition = m_PanelRestPosition;
            m_PanelFrame.localScale = m_PanelRestScale;
        }

        private bool HasCompleteBindings()
        {
            return m_TitleGroup != null &&
                   m_StatusGroup != null &&
                   m_MenuGroup != null &&
                   m_TitleTransform != null &&
                   m_MenuTransform != null &&
                   m_PanelGroup != null &&
                   m_PanelFrame != null &&
                   m_StarGlints != null &&
                   m_SignalBeam != null &&
                   m_TelescopeLensGlow != null;
        }

        private static float Wave01(float value)
        {
            return 0.5f + 0.5f * Mathf.Sin(value * Mathf.PI * 2f);
        }

        private static float EaseOutCubic(float value)
        {
            var inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse;
        }

        private static float EaseOutBack(float value)
        {
            value = Mathf.Clamp01(value);
            const float overshoot = 1.32f;
            var shifted = value - 1f;
            return 1f + (overshoot + 1f) * shifted * shifted * shifted +
                   overshoot * shifted * shifted;
        }

        private static float SmoothStep(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            var color = graphic.color;
            color.a = Mathf.Clamp01(alpha);
            graphic.color = color;
        }
    }
}
