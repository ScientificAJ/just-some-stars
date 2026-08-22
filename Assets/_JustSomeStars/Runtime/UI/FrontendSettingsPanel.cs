using System;
using System.IO;
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

        private readonly UnityEngine.Events.UnityAction[] m_DecreaseCallbacks =
            new UnityEngine.Events.UnityAction[ControlCount];
        private readonly UnityEngine.Events.UnityAction[] m_IncreaseCallbacks =
            new UnityEngine.Events.UnityAction[ControlCount];

        private bool m_IsListening;

        public bool IsReady =>
            m_Root != null &&
            m_ScrollRect != null &&
            HasCompleteArray(m_DecreaseButtons) &&
            HasCompleteArray(m_IncreaseButtons) &&
            HasCompleteArray(m_ValueLabels);

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
            if (Dependencies != null)
            {
                Dependencies.Settings.SettingsChanged -= OnSettingsChanged;
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
            Render(dependencies.Settings.Current);
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
            Canvas.ForceUpdateCanvases();
            m_ScrollRect.StopMovement();
            m_ScrollRect.verticalNormalizedPosition = 1f;
        }

        public void Hide()
        {
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
