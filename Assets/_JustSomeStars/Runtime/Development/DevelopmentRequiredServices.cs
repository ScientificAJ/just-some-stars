using System;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Core;

namespace JustSomeStars.Runtime.Development
{
    internal interface IDevelopmentServiceLifecycleObserver
    {
        void OnInitialized(IGameService service);

        void OnShutdown(IGameService service);
    }

    internal sealed class NoOpDevelopmentServiceLifecycleObserver :
        IDevelopmentServiceLifecycleObserver
    {
        public static readonly NoOpDevelopmentServiceLifecycleObserver Instance =
            new NoOpDevelopmentServiceLifecycleObserver();

        private NoOpDevelopmentServiceLifecycleObserver()
        {
        }

        public void OnInitialized(IGameService service)
        {
        }

        public void OnShutdown(IGameService service)
        {
        }
    }

    internal abstract class DevelopmentRequiredService : IGameService
    {
        private readonly IDevelopmentServiceLifecycleObserver m_LifecycleObserver;

        private int m_InitializationObserved;
        private int m_ShutdownObserved;

        protected DevelopmentRequiredService(
            IDevelopmentServiceLifecycleObserver lifecycleObserver)
        {
            m_LifecycleObserver = lifecycleObserver ??
                throw new ArgumentNullException(nameof(lifecycleObserver));
        }

        public ValueTask<StartupResult> InitializeAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Interlocked.CompareExchange(
                    ref m_InitializationObserved,
                    1,
                    0) == 0)
            {
                m_LifecycleObserver.OnInitialized(this);
            }

            return new ValueTask<StartupResult>(StartupResult.Available());
        }

        public ValueTask ShutdownAsync()
        {
            if (Interlocked.CompareExchange(ref m_ShutdownObserved, 1, 0) == 0)
            {
                m_LifecycleObserver.OnShutdown(this);
            }

            return default;
        }
    }

    internal sealed class DevelopmentSettingsService :
        DevelopmentRequiredService
    {
        public DevelopmentSettingsService(
            IDevelopmentServiceLifecycleObserver lifecycleObserver)
            : base(lifecycleObserver)
        {
        }
    }

    internal sealed class DevelopmentLocalSaveService :
        DevelopmentRequiredService
    {
        public DevelopmentLocalSaveService(
            IDevelopmentServiceLifecycleObserver lifecycleObserver)
            : base(lifecycleObserver)
        {
        }
    }

    internal sealed class DevelopmentInputService :
        DevelopmentRequiredService
    {
        public DevelopmentInputService(
            IDevelopmentServiceLifecycleObserver lifecycleObserver)
            : base(lifecycleObserver)
        {
        }
    }

    internal sealed class DevelopmentContentCatalogueService :
        DevelopmentRequiredService
    {
        public DevelopmentContentCatalogueService(
            IDevelopmentServiceLifecycleObserver lifecycleObserver)
            : base(lifecycleObserver)
        {
        }
    }

    internal sealed class DevelopmentModeControllerService :
        DevelopmentRequiredService
    {
        public DevelopmentModeControllerService(
            IDevelopmentServiceLifecycleObserver lifecycleObserver)
            : base(lifecycleObserver)
        {
        }
    }
}
