using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace JustSomeStars.Runtime.Core
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        private static readonly object s_OwnershipGate = new object();

        private static GameBootstrap s_Instance;
        private static Func<GameBootstrapComposition> s_CompositionFactory;
        private static Task s_PredecessorShutdown = Task.CompletedTask;

        private readonly ServiceStartupCoordinator m_StartupCoordinator =
            new ServiceStartupCoordinator();
        private readonly CancellationTokenSource m_LifetimeCancellation =
            new CancellationTokenSource();

        private bool m_IsPrimaryInstance;
        private Task m_PredecessorShutdown = Task.CompletedTask;
        private Task<StartupReport> m_StartTask;
        private Task m_ShutdownTask;

        public static Func<GameBootstrapComposition> CompositionFactory
        {
            get
            {
                lock (s_OwnershipGate)
                {
                    return s_CompositionFactory;
                }
            }
            set
            {
                lock (s_OwnershipGate)
                {
                    s_CompositionFactory = value;
                }
            }
        }

        public StartupReport LastStartupReport => m_StartupCoordinator.LastReport;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            lock (s_OwnershipGate)
            {
                // With domain reload disabled, SubsystemRegistration can run
                // before Unity destroys the live DontDestroyOnLoad owner. Keep
                // that owner authoritative so its OnDestroy can publish cleanup.
                if (s_Instance == null)
                {
                    s_Instance = null;
                }

                s_CompositionFactory = null;
                if (s_PredecessorShutdown == null ||
                    s_PredecessorShutdown.IsCompleted)
                {
                    s_PredecessorShutdown = Task.CompletedTask;
                }
            }
        }

        public ValueTask<StartupReport> StartupAsync(
            GameBootstrapComposition composition,
            CancellationToken cancellationToken)
        {
            if (composition == null)
            {
                throw new ArgumentNullException(nameof(composition));
            }

            Task<StartupReport> startTask;
            lock (s_OwnershipGate)
            {
                if (!IsCurrentPrimaryLocked())
                {
                    throw new InvalidOperationException(
                        "Only the current primary GameBootstrap can start services.");
                }

                if (m_ShutdownTask != null)
                {
                    throw new InvalidOperationException(
                        "GameBootstrap cannot start after shutdown has begun.");
                }

                m_StartTask ??= RunOwnedStartupAsync(
                    () => composition,
                    cancellationToken);
                startTask = m_StartTask;
            }

            return new ValueTask<StartupReport>(startTask);
        }

        public ValueTask ShutdownAsync()
        {
            TaskCompletionSource<object> cancellationIssued = null;
            Task shutdownTask;
            lock (s_OwnershipGate)
            {
                if (m_ShutdownTask == null)
                {
                    cancellationIssued = new TaskCompletionSource<object>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    m_ShutdownTask = RunShutdownAsync(
                        m_StartTask,
                        cancellationIssued.Task);
                }

                shutdownTask = m_ShutdownTask;
            }

            if (cancellationIssued != null)
            {
                try
                {
                    CancelLifetime();
                }
                finally
                {
                    cancellationIssued.TrySetResult(null);
                }
            }

            return new ValueTask(shutdownTask);
        }

        private void Awake()
        {
            var isDuplicate = false;
            lock (s_OwnershipGate)
            {
                if (s_Instance != null && s_Instance != this)
                {
                    isDuplicate = true;
                }
                else
                {
                    s_Instance = this;
                    m_IsPrimaryInstance = true;
                    m_PredecessorShutdown =
                        s_PredecessorShutdown ?? Task.CompletedTask;
                }
            }

            if (isDuplicate)
            {
                enabled = false;
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            Task<StartupReport> startTask;
            lock (s_OwnershipGate)
            {
                if (!IsCurrentPrimaryLocked() || m_ShutdownTask != null)
                {
                    return;
                }

                m_StartTask ??= RunOwnedStartupAsync(
                    CreateConfiguredComposition,
                    CancellationToken.None);
                startTask = m_StartTask;
            }

            _ = ObserveAutomaticStartupAsync(startTask);
        }

        private async Task<StartupReport> RunOwnedStartupAsync(
            Func<GameBootstrapComposition> compositionProvider,
            CancellationToken callerCancellation)
        {
            // Publish m_StartTask before composition providers or service code can
            // re-enter this lifecycle through a shutdown request.
            await Task.Yield();

            using var startupCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    callerCancellation,
                    m_LifetimeCancellation.Token);
            var cancellationToken = startupCancellation.Token;

            await AwaitWithCancellationAsync(
                m_PredecessorShutdown,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            lock (s_OwnershipGate)
            {
                ThrowIfOwnedStartupCannotContinueLocked(cancellationToken);
            }

            var composition = compositionProvider();
            if (composition == null)
            {
                throw new InvalidOperationException(
                    "GameBootstrap composition provider returned null.");
            }

            Task<StartupReport> coordinatorStartup;
            lock (s_OwnershipGate)
            {
                // This is the last ownership decision before coordinator entry.
                // RunStartupAsync itself yields before invoking any service code,
                // so no external callback executes under the ownership lock.
                ThrowIfOwnedStartupCannotContinueLocked(cancellationToken);
                coordinatorStartup = m_StartupCoordinator.StartupAsync(
                    composition,
                    cancellationToken).AsTask();
            }

            return await coordinatorStartup;
        }

        private async Task ObserveAutomaticStartupAsync(
            Task<StartupReport> startTask)
        {
            try
            {
                await startTask;
            }
            catch (OperationCanceledException)
            {
                // Cancellation of either the owned lifetime or the first manual
                // caller is an expected terminal result for the shared wrapper.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private async Task RunShutdownAsync(
            Task<StartupReport> startTask,
            Task cancellationIssued)
        {
            await cancellationIssued;
            await m_StartupCoordinator.ShutdownAsync();

            if (startTask == null)
            {
                return;
            }

            try
            {
                await startTask;
            }
            catch (OperationCanceledException)
                when (m_LifetimeCancellation.IsCancellationRequested)
            {
                // Bootstrap shutdown owns this cancellation and has already
                // quiesced any coordinator work.
            }
        }

        private void OnDestroy()
        {
            Task predecessor = null;
            TaskCompletionSource<object> shutdownBarrier = null;

            lock (s_OwnershipGate)
            {
                var wasCurrentPrimary = IsCurrentPrimaryLocked();
                m_IsPrimaryInstance = false;

                if (wasCurrentPrimary)
                {
                    predecessor = s_PredecessorShutdown ?? Task.CompletedTask;
                    shutdownBarrier = new TaskCompletionSource<object>(
                        TaskCreationOptions.RunContinuationsAsynchronously);

                    // Publish the pending cleanup before releasing ownership so
                    // every accepted replacement captures the full chain.
                    s_PredecessorShutdown = shutdownBarrier.Task;
                    s_Instance = null;
                }
            }

            CancelLifetime();

            if (shutdownBarrier == null)
            {
                m_LifetimeCancellation.Dispose();
                return;
            }

            _ = CompleteShutdownBarrierAsync(predecessor, shutdownBarrier);
        }

        private async Task CompleteShutdownBarrierAsync(
            Task predecessor,
            TaskCompletionSource<object> shutdownBarrier)
        {
            try
            {
                await predecessor;
                await ShutdownAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                m_LifetimeCancellation.Dispose();
                shutdownBarrier.TrySetResult(null);
            }
        }

        private void CancelLifetime()
        {
            try
            {
                m_LifetimeCancellation.Cancel();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private bool IsCurrentPrimaryLocked()
        {
            return m_IsPrimaryInstance && ReferenceEquals(s_Instance, this);
        }

        private void ThrowIfOwnedStartupCannotContinueLocked(
            CancellationToken cancellationToken)
        {
            if (!IsCurrentPrimaryLocked() || m_ShutdownTask != null)
            {
                throw new OperationCanceledException(
                    "The owning GameBootstrap lifecycle has ended.",
                    cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        private static async Task AwaitWithCancellationAsync(
            Task operation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (operation.IsCompleted)
            {
                await operation;
                cancellationToken.ThrowIfCancellationRequested();
                return;
            }

            var cancellationSignal = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(
                       () => cancellationSignal.TrySetCanceled(
                           cancellationToken)))
            {
                var completed = await Task.WhenAny(
                    operation,
                    cancellationSignal.Task);
                await completed;
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        private static GameBootstrapComposition CreateConfiguredComposition()
        {
            var factory = CompositionFactory;
            return factory == null
                ? CreateDefaultComposition()
                : factory();
        }

        private static GameBootstrapComposition CreateDefaultComposition()
        {
            return new GameBootstrapComposition(
                Array.Empty<GameServiceRegistration>(),
                new UnitySceneTransition());
        }
    }
}
