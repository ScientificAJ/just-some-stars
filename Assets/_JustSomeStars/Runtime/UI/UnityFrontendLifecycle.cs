using System;
using UnityEngine;

namespace JustSomeStars.Runtime.UI
{
    [DisallowMultipleComponent]
    public sealed class UnityFrontendLifecycle : MonoBehaviour, IFrontendLifecycle
    {
        private bool m_IsBound;

        public bool IsConfigured => Dependencies != null;

        public FrontendDependencies Dependencies { get; private set; }

        public event Action BackRequested;

        private void OnEnable()
        {
            Bind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void OnDestroy()
        {
            Unbind();
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
                    "UnityFrontendLifecycle cannot be rebound to another composition.");
            }

            Dependencies = dependencies;
            Bind();
        }

        internal void Release(FrontendDependencies dependencies)
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
                    "UnityFrontendLifecycle can only be released by its owning " +
                    "composition.");
            }

            Unbind();
            Dependencies = null;
        }

        public void RequestExit()
        {
            Application.Quit();
        }

        private void Bind()
        {
            if (m_IsBound || Dependencies == null || !isActiveAndEnabled)
            {
                return;
            }

            Dependencies.Input.BackRequested += HandleBackRequested;
            m_IsBound = true;
        }

        private void Unbind()
        {
            if (!m_IsBound)
            {
                return;
            }

            Dependencies.Input.BackRequested -= HandleBackRequested;
            m_IsBound = false;
        }

        private void HandleBackRequested()
        {
            BackRequested?.Invoke();
        }
    }
}
