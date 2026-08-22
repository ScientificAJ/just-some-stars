using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JustSomeStars.Runtime.UI
{
    [DisallowMultipleComponent]
    public sealed class UnityFrontendLifecycle : MonoBehaviour, IFrontendLifecycle
    {
        private InputAction m_BackAction;

        public event Action BackRequested;

        private void OnEnable()
        {
            EnsureBackAction();
            m_BackAction.performed += HandleBackPerformed;
            m_BackAction.Enable();
        }

        private void OnDisable()
        {
            if (m_BackAction == null)
            {
                return;
            }

            m_BackAction.performed -= HandleBackPerformed;
            m_BackAction.Disable();
        }

        private void OnDestroy()
        {
            m_BackAction?.Dispose();
            m_BackAction = null;
        }

        public void RequestExit()
        {
            Application.Quit();
        }

        private void EnsureBackAction()
        {
            if (m_BackAction != null)
            {
                return;
            }

            m_BackAction = new InputAction(
                "Frontend Back",
                InputActionType.Button,
                "<Keyboard>/escape");
        }

        private void HandleBackPerformed(InputAction.CallbackContext context)
        {
            _ = context;
            BackRequested?.Invoke();
        }
    }
}
