using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Accessibility;
using JustSomeStars.Runtime.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JustSomeStars.Runtime.Input
{
    public enum GameplayInputMode
    {
        None = 0,
        Surface = 1,
        Flight = 2,
        Lens = 3,
    }

    public enum SemanticGameplayCommand
    {
        Primary = 0,
        Secondary = 1,
        Pause = 2,
        Lens = 3,
        PhotoMode = 4,
        Recenter = 5,
    }

    public enum ControlScreenSide
    {
        Left = 0,
        Right = 1,
    }

    public sealed class InputRouter : IGameService
    {
        private static readonly string[] RequiredUiActions =
        {
            "Navigate",
            "Submit",
            "Cancel",
            "Point",
            "Click",
            "RightClick",
            "MiddleClick",
            "ScrollWheel",
            "TrackedDevicePosition",
            "TrackedDeviceOrientation",
        };

        private static readonly string[] RequiredGameplayActions =
        {
            "Move",
            "Look",
            "Primary",
            "Secondary",
            "Pause",
            "Lens",
            "PhotoMode",
            "Recenter",
        };

        private static readonly GameplayInputMode[] GameplayModes =
        {
            GameplayInputMode.Surface,
            GameplayInputMode.Flight,
            GameplayInputMode.Lens,
        };

        private readonly InputActionAsset m_Actions;
        private readonly SettingsService m_Settings;
        private readonly Dictionary<InputAction, Action<InputAction.CallbackContext>>
            m_CommandCallbacks =
                new Dictionary<InputAction, Action<InputAction.CallbackContext>>();

        private InputAction m_CancelAction;
        private bool m_IsInitialized;
        private GameplayInputMode m_ActiveGameplayMode;
        private ControlScreenSide m_MovementScreenSide = ControlScreenSide.Left;
        private ControlScreenSide m_ActionScreenSide = ControlScreenSide.Right;

        public InputRouter(InputActionAsset actions, SettingsService settings)
        {
            m_Actions = actions ?? throw new ArgumentNullException(nameof(actions));
            m_Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public event Action BackRequested;

        public event Action<GameplayInputMode, SemanticGameplayCommand>
            GameplayCommandPerformed;

        public event Action<ControlScreenSide, ControlScreenSide>
            ControlLayoutChanged;

        public InputActionAsset Actions => m_Actions;

        public bool IsInitialized => m_IsInitialized;

        public GameplayInputMode ActiveGameplayMode => m_ActiveGameplayMode;

        public ControlScreenSide MovementScreenSide => m_MovementScreenSide;

        public ControlScreenSide ActionScreenSide => m_ActionScreenSide;

        public ValueTask<StartupResult> InitializeAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (m_IsInitialized)
            {
                return new ValueTask<StartupResult>(StartupResult.Available());
            }

            DisableEveryMap();
            if (!m_Settings.IsInitialized)
            {
                return new ValueTask<StartupResult>(StartupResult.Unavailable(
                    "InputRouter requires an initialized SettingsService."));
            }

            if (!TryResolveCanonicalAsset(out var failure))
            {
                return new ValueTask<StartupResult>(StartupResult.Unavailable(failure));
            }

            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                SubscribeActions();
                m_Settings.SettingsChanged += OnSettingsChanged;
                ApplyControlLayout(m_Settings.Current, publishChange: false);
                m_Actions.FindActionMap("UI", throwIfNotFound: true).Enable();
                m_ActiveGameplayMode = GameplayInputMode.None;
                m_IsInitialized = true;
                return new ValueTask<StartupResult>(StartupResult.Available());
            }
            catch
            {
                m_Settings.SettingsChanged -= OnSettingsChanged;
                UnsubscribeActions();
                DisableEveryMap();
                m_ActiveGameplayMode = GameplayInputMode.None;
                m_IsInitialized = false;
                throw;
            }
        }

        public ValueTask ShutdownAsync()
        {
            m_Settings.SettingsChanged -= OnSettingsChanged;
            UnsubscribeActions();
            DisableEveryMap();
            m_ActiveGameplayMode = GameplayInputMode.None;
            m_IsInitialized = false;
            return default;
        }

        public void SetGameplayMode(GameplayInputMode mode)
        {
            if (!Enum.IsDefined(typeof(GameplayInputMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            if (!m_IsInitialized)
            {
                throw new InvalidOperationException(
                    "InputRouter must be initialized before selecting a gameplay mode.");
            }

            if (mode == m_ActiveGameplayMode)
            {
                return;
            }

            if (m_ActiveGameplayMode != GameplayInputMode.None)
            {
                GetGameplayMap(m_ActiveGameplayMode).Disable();
            }

            m_ActiveGameplayMode = GameplayInputMode.None;
            if (mode != GameplayInputMode.None)
            {
                GetGameplayMap(mode).Enable();
                m_ActiveGameplayMode = mode;
            }
        }

        public Vector2 ReadMove()
        {
            return ReadVector2("Move");
        }

        public Vector2 ReadLook()
        {
            return ReadVector2("Look");
        }

        private bool TryResolveCanonicalAsset(out string failure)
        {
            var ui = m_Actions.FindActionMap("UI", throwIfNotFound: false);
            if (ui == null)
            {
                failure = "JssInputActions is missing the UI action map.";
                return false;
            }

            foreach (var actionName in RequiredUiActions)
            {
                if (ui.FindAction(actionName, throwIfNotFound: false) == null)
                {
                    failure =
                        $"JssInputActions UI map is missing action '{actionName}'.";
                    return false;
                }
            }

            foreach (var mode in GameplayModes)
            {
                var map = m_Actions.FindActionMap(mode.ToString(), throwIfNotFound: false);
                if (map == null)
                {
                    failure =
                        $"JssInputActions is missing the {mode} action map.";
                    return false;
                }

                foreach (var actionName in RequiredGameplayActions)
                {
                    if (map.FindAction(actionName, throwIfNotFound: false) == null)
                    {
                        failure =
                            $"JssInputActions {mode} map is missing action '{actionName}'.";
                        return false;
                    }
                }
            }

            failure = string.Empty;
            return true;
        }

        private void SubscribeActions()
        {
            m_CancelAction = m_Actions.FindAction("UI/Cancel", throwIfNotFound: true);
            m_CancelAction.performed += OnCancelPerformed;

            foreach (var mode in GameplayModes)
            {
                foreach (SemanticGameplayCommand command in
                    Enum.GetValues(typeof(SemanticGameplayCommand)))
                {
                    var action = GetGameplayMap(mode).FindAction(
                        command.ToString(),
                        throwIfNotFound: true);
                    Action<InputAction.CallbackContext> callback = _ =>
                        OnGameplayCommandPerformed(mode, command);
                    action.performed += callback;
                    m_CommandCallbacks.Add(action, callback);
                }
            }
        }

        private void UnsubscribeActions()
        {
            if (m_CancelAction != null)
            {
                m_CancelAction.performed -= OnCancelPerformed;
                m_CancelAction = null;
            }

            foreach (var entry in m_CommandCallbacks)
            {
                entry.Key.performed -= entry.Value;
            }

            m_CommandCallbacks.Clear();
        }

        private void OnCancelPerformed(InputAction.CallbackContext context)
        {
            if (m_IsInitialized)
            {
                BackRequested?.Invoke();
            }
        }

        private void OnGameplayCommandPerformed(
            GameplayInputMode mode,
            SemanticGameplayCommand command)
        {
            if (m_IsInitialized && m_ActiveGameplayMode == mode)
            {
                GameplayCommandPerformed?.Invoke(mode, command);
            }
        }

        private void OnSettingsChanged(GameSettings settings)
        {
            ApplyControlLayout(settings, publishChange: true);
        }

        private void ApplyControlLayout(
            GameSettings settings,
            bool publishChange)
        {
            var movement = settings.LeftHandedControls
                ? ControlScreenSide.Right
                : ControlScreenSide.Left;
            var actions = settings.LeftHandedControls
                ? ControlScreenSide.Left
                : ControlScreenSide.Right;
            if (movement == m_MovementScreenSide && actions == m_ActionScreenSide)
            {
                return;
            }

            m_MovementScreenSide = movement;
            m_ActionScreenSide = actions;
            if (publishChange)
            {
                ControlLayoutChanged?.Invoke(movement, actions);
            }
        }

        private Vector2 ReadVector2(string actionName)
        {
            if (!m_IsInitialized ||
                m_ActiveGameplayMode == GameplayInputMode.None)
            {
                return Vector2.zero;
            }

            return GetGameplayMap(m_ActiveGameplayMode)
                .FindAction(actionName, throwIfNotFound: true)
                .ReadValue<Vector2>();
        }

        private InputActionMap GetGameplayMap(GameplayInputMode mode)
        {
            return m_Actions.FindActionMap(mode.ToString(), throwIfNotFound: true);
        }

        private void DisableEveryMap()
        {
            foreach (var map in m_Actions.actionMaps)
            {
                map.Disable();
            }
        }
    }
}
