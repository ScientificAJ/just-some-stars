using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace JustSomeStars.Runtime.Core
{
    public enum SceneStreamStatus
    {
        Loaded = 0,
        AlreadyLoaded = 1,
        Unloaded = 2,
        NothingLoaded = 3,
        Failed = 4,
    }

    public enum SceneStreamStage
    {
        Resolving = 0,
        Loading = 1,
        Activating = 2,
        CommittingMode = 3,
        CleaningUp = 4,
        Completed = 5,
        Failed = 6,
    }

    public readonly struct SceneStreamProgress
    {
        public SceneStreamProgress(
            string destinationId,
            SceneStreamStage stage,
            float normalizedProgress)
        {
            DestinationId = destinationId;
            Stage = stage;
            NormalizedProgress = normalizedProgress;
        }

        public string DestinationId { get; }

        public SceneStreamStage Stage { get; }

        public float NormalizedProgress { get; }
    }

    public sealed class SceneStreamDiagnostic
    {
        internal SceneStreamDiagnostic(
            string destinationId,
            SceneStreamStage stage,
            string message,
            Exception failure)
        {
            DestinationId = destinationId;
            Stage = stage;
            Message = message;
            Failure = failure;
        }

        public string DestinationId { get; }

        public SceneStreamStage Stage { get; }

        public string Message { get; }

        public Exception Failure { get; }
    }

    public readonly struct SceneStreamResult
    {
        internal SceneStreamResult(
            SceneStreamStatus status,
            string destinationId,
            string diagnostic)
        {
            Status = status;
            DestinationId = destinationId;
            Diagnostic = diagnostic;
        }

        public SceneStreamStatus Status { get; }

        public string DestinationId { get; }

        public string Diagnostic { get; }
    }

    internal interface ISceneCatalogSource
    {
        ValueTask<SceneCatalog> LoadAsync(CancellationToken cancellationToken);

        ValueTask ReleaseAsync();
    }

    internal interface ISceneStreamBackend
    {
        object CaptureActiveScene();

        ISceneLoadHandle BeginLoad(string address, string destinationId);

        void RestoreActiveScene(object sceneToken);
    }

    internal interface ISceneLoadHandle
    {
        Task LoadTask { get; }

        float PercentComplete { get; }

        Exception LoadFailure { get; }

        bool HasLoadedScene { get; }

        Task ActivateAsync();

        Task UnloadAsync();

        void Release();
    }

    internal sealed class AddressablesSceneCatalogSource : ISceneCatalogSource
    {
        private readonly string m_Address;

        private AsyncOperationHandle<SceneCatalog> m_Handle;
        private bool m_HandleIssued;
        private bool m_Released;

        public AddressablesSceneCatalogSource(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException(
                    "A SceneCatalog Addressables key is required.",
                    nameof(address));
            }

            m_Address = address;
        }

        public async ValueTask<SceneCatalog> LoadAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (m_HandleIssued)
            {
                throw new InvalidOperationException(
                    "The Addressables scene catalog source can load only once.");
            }

            m_Handle = Addressables.LoadAssetAsync<SceneCatalog>(m_Address);
            m_HandleIssued = true;
            await m_Handle.Task;
            cancellationToken.ThrowIfCancellationRequested();
            if (m_Handle.Status != AsyncOperationStatus.Succeeded ||
                m_Handle.Result == null)
            {
                throw m_Handle.OperationException ?? new InvalidOperationException(
                    $"Addressables could not load scene catalog '{m_Address}'.");
            }

            return m_Handle.Result;
        }

        public ValueTask ReleaseAsync()
        {
            if (!m_Released && m_HandleIssued)
            {
                m_Released = true;
                if (m_Handle.IsValid())
                {
                    Addressables.Release(m_Handle);
                }
            }

            return default;
        }
    }

    internal sealed class AddressablesSceneStreamBackend : ISceneStreamBackend
    {
        public object CaptureActiveScene()
        {
            return SceneManager.GetActiveScene();
        }

        public ISceneLoadHandle BeginLoad(string address, string destinationId)
        {
            return new AddressablesSceneLoadHandle(address);
        }

        public void RestoreActiveScene(object sceneToken)
        {
            if (sceneToken is Scene scene && scene.IsValid() && scene.isLoaded)
            {
                SceneManager.SetActiveScene(scene);
            }
        }
    }

    internal sealed class AddressablesSceneLoadHandle : ISceneLoadHandle
    {
        private readonly AsyncOperationHandle<SceneInstance> m_Handle;
        private readonly Task m_LoadTask;

        private Exception m_LoadFailure;
        private bool m_ActivationAttempted;
        private bool m_Activated;
        private bool m_Unloaded;
        private bool m_Released;

        public AddressablesSceneLoadHandle(string address)
        {
            m_Handle = Addressables.LoadSceneAsync(
                address,
                LoadSceneMode.Additive,
                activateOnLoad: false);
            m_LoadTask = ObserveLoadAsync();
        }

        public Task LoadTask => m_LoadTask;

        public float PercentComplete => m_Handle.IsValid()
            ? m_Handle.PercentComplete
            : 1f;

        public Exception LoadFailure => m_LoadFailure;

        public bool HasLoadedScene =>
            m_Handle.IsValid() &&
            m_Handle.Status == AsyncOperationStatus.Succeeded &&
            m_Handle.Result.Scene.IsValid();

        public async Task ActivateAsync()
        {
            if (m_LoadFailure != null)
            {
                throw new InvalidOperationException(
                    "A failed Addressables scene cannot be activated.",
                    m_LoadFailure);
            }

            if (m_Activated)
            {
                return;
            }

            m_ActivationAttempted = true;
            var activation = m_Handle.Result.ActivateAsync();
            while (!activation.isDone)
            {
                await Task.Yield();
            }

            var scene = m_Handle.Result.Scene;
            if (!scene.IsValid() || !scene.isLoaded ||
                !SceneManager.SetActiveScene(scene))
            {
                throw new InvalidOperationException(
                    "The Addressables destination scene did not activate.");
            }

            m_Activated = true;
        }

        public async Task UnloadAsync()
        {
            if (m_Unloaded || m_LoadFailure != null || !m_Handle.IsValid())
            {
                return;
            }

            if (!m_Activated && !m_ActivationAttempted)
            {
                await ActivateAsync();
            }

            var unload = Addressables.UnloadSceneAsync(
                m_Handle,
                autoReleaseHandle: false);
            await unload.Task;
            var failure = unload.Status == AsyncOperationStatus.Succeeded
                ? null
                : unload.OperationException ?? new InvalidOperationException(
                    "Addressables destination scene did not unload.");
            if (unload.IsValid())
            {
                Addressables.Release(unload);
            }

            if (failure != null)
            {
                throw failure;
            }

            m_Unloaded = true;
        }

        public void Release()
        {
            if (m_Released)
            {
                return;
            }

            m_Released = true;
            if (!m_Unloaded && m_Handle.IsValid())
            {
                Addressables.Release(m_Handle);
            }
        }

        private async Task ObserveLoadAsync()
        {
            try
            {
                await m_Handle.Task;
                if (m_Handle.Status != AsyncOperationStatus.Succeeded)
                {
                    m_LoadFailure = m_Handle.OperationException ??
                        new InvalidOperationException(
                            "Addressables destination scene did not load.");
                }
            }
            catch (Exception exception)
            {
                m_LoadFailure = exception;
            }
        }
    }

    public sealed class SceneStreamService : IGameService
    {
        private readonly object m_Gate = new object();
        private readonly ISceneCatalogSource m_CatalogSource;
        private readonly ISceneStreamBackend m_Backend;
        private readonly ISceneTransition m_FallbackTransition;
        private readonly GameModeController m_ModeController;

        private SceneCatalog m_Catalog;
        private bool m_IsInitialized;
        private bool m_CatalogReleased;
        private Task<SceneStreamResult> m_ActiveOperation;
        private CancellationTokenSource m_ActiveCancellation;
        private Task m_ShutdownTask;
        private ISceneLoadHandle m_LoadedHandle;
        private string m_LoadedDestinationId;
        private object m_PreviousActiveScene;
        private GameMode m_PreviousMode;

        internal SceneStreamService(
            ISceneCatalogSource catalogSource,
            ISceneStreamBackend backend,
            ISceneTransition fallbackTransition,
            GameModeController modeController)
        {
            m_CatalogSource = catalogSource ??
                throw new ArgumentNullException(nameof(catalogSource));
            m_Backend = backend ?? throw new ArgumentNullException(nameof(backend));
            m_FallbackTransition = fallbackTransition ??
                throw new ArgumentNullException(nameof(fallbackTransition));
            m_ModeController = modeController ??
                throw new ArgumentNullException(nameof(modeController));
        }

        public event Action<SceneStreamProgress> TransitionProgressed;

        public event Action<SceneStreamDiagnostic> DiagnosticRecorded;

        public bool IsInitialized
        {
            get
            {
                lock (m_Gate)
                {
                    return m_IsInitialized;
                }
            }
        }

        public string LoadedDestinationId
        {
            get
            {
                lock (m_Gate)
                {
                    return m_LoadedDestinationId;
                }
            }
        }

        internal GameModeController ModeController => m_ModeController;

        internal ISceneTransition FallbackTransition => m_FallbackTransition;

        internal string FallbackSceneName
        {
            get
            {
                lock (m_Gate)
                {
                    EnsureOperationalLocked();
                    return m_Catalog.FallbackSceneName;
                }
            }
        }

        internal GameMode FallbackMode
        {
            get
            {
                lock (m_Gate)
                {
                    EnsureOperationalLocked();
                    return m_Catalog.FallbackMode;
                }
            }
        }

        internal bool TryResolveDestination(
            string destinationIdOrAddress,
            out string destinationId)
        {
            RequireDestinationId(destinationIdOrAddress);
            lock (m_Gate)
            {
                EnsureOperationalLocked();
                if (m_Catalog.TryResolveEntry(destinationIdOrAddress, out var entry))
                {
                    destinationId = entry.DestinationId;
                    return true;
                }
            }
            destinationId = null;
            return false;
        }

        public async ValueTask<StartupResult> InitializeAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (m_Gate)
            {
                if (m_IsInitialized)
                {
                    return StartupResult.Available();
                }

                if (m_ShutdownTask != null)
                {
                    throw new InvalidOperationException(
                        "SceneStreamService cannot initialize after shutdown begins.");
                }
            }

            try
            {
                var catalog = await m_CatalogSource.LoadAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (catalog == null)
                {
                    throw new InvalidOperationException(
                        "The Addressables scene catalog resolved to null.");
                }

                catalog.Validate();
                lock (m_Gate)
                {
                    m_Catalog = catalog;
                    m_IsInitialized = true;
                }

                return StartupResult.Available();
            }
            catch (OperationCanceledException)
            {
                await ReleaseCatalogAsync();
                throw;
            }
            catch (Exception exception)
            {
                await ReleaseCatalogAsync();
                return StartupResult.Unavailable(
                    "Scene catalog initialization failed: " + exception.Message,
                    exception);
            }
        }

        public ValueTask<SceneStreamResult> LoadDestinationAsync(
            string destinationIdOrAddress,
            CancellationToken cancellationToken)
        {
            RequireDestinationId(destinationIdOrAddress);
            lock (m_Gate)
            {
                EnsureOperationalLocked();
                var destinationId = m_Catalog.TryResolveEntry(
                        destinationIdOrAddress,
                        out var resolved)
                    ? resolved.DestinationId
                    : destinationIdOrAddress;
                if (m_LoadedDestinationId != null)
                {
                    if (string.Equals(
                            m_LoadedDestinationId,
                            destinationId,
                            StringComparison.Ordinal))
                    {
                        return new ValueTask<SceneStreamResult>(
                            new SceneStreamResult(
                                SceneStreamStatus.AlreadyLoaded,
                                destinationId,
                                null));
                    }

                    throw new InvalidOperationException(
                        $"Destination '{m_LoadedDestinationId}' must unload before " +
                        $"'{destinationId}' can load.");
                }

                return BeginOperationLocked(
                    token => RunLoadAsync(destinationId, token),
                    cancellationToken);
            }
        }

        public ValueTask<SceneStreamResult> UnloadDestinationAsync(
            string destinationId,
            CancellationToken cancellationToken)
        {
            RequireDestinationId(destinationId);
            lock (m_Gate)
            {
                EnsureOperationalLocked();
                if (m_LoadedDestinationId == null)
                {
                    return new ValueTask<SceneStreamResult>(
                        new SceneStreamResult(
                            SceneStreamStatus.NothingLoaded,
                            destinationId,
                            null));
                }

                if (!string.Equals(
                        m_LoadedDestinationId,
                        destinationId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Destination '{destinationId}' is not owned; " +
                        $"'{m_LoadedDestinationId}' is currently loaded.");
                }

                return BeginOperationLocked(
                    token => RunUnloadAsync(destinationId, token),
                    cancellationToken);
            }
        }

        public ValueTask ShutdownAsync()
        {
            CancellationTokenSource cancellation = null;
            Task shutdown;
            lock (m_Gate)
            {
                if (m_ShutdownTask == null)
                {
                    m_ShutdownTask = RunShutdownAsync(m_ActiveOperation);
                    cancellation = m_ActiveCancellation;
                }

                shutdown = m_ShutdownTask;
            }

            cancellation?.Cancel();
            return new ValueTask(shutdown);
        }

        private ValueTask<SceneStreamResult> BeginOperationLocked(
            Func<CancellationToken, Task<SceneStreamResult>> operation,
            CancellationToken callerCancellation)
        {
            if (m_ActiveOperation != null && !m_ActiveOperation.IsCompleted)
            {
                throw new InvalidOperationException(
                    "Another scene stream operation is already in flight.");
            }

            var ownedCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    callerCancellation);
            var task = ObserveOperationAsync(
                operation(ownedCancellation.Token),
                ownedCancellation);
            m_ActiveCancellation = ownedCancellation;
            m_ActiveOperation = task;
            return new ValueTask<SceneStreamResult>(task);
        }

        private async Task<SceneStreamResult> ObserveOperationAsync(
            Task<SceneStreamResult> operation,
            CancellationTokenSource ownedCancellation)
        {
            try
            {
                return await operation;
            }
            finally
            {
                lock (m_Gate)
                {
                    if (ReferenceEquals(m_ActiveCancellation, ownedCancellation))
                    {
                        m_ActiveCancellation = null;
                        m_ActiveOperation = null;
                    }
                }

                ownedCancellation.Dispose();
            }
        }

        private async Task<SceneStreamResult> RunLoadAsync(
            string destinationId,
            CancellationToken cancellationToken)
        {
            await Task.Yield();
            var progress = new ProgressPublisher(this, destinationId);
            progress.Publish(SceneStreamStage.Resolving, 0f);
            cancellationToken.ThrowIfCancellationRequested();
            if (!m_Catalog.TryGetEntry(destinationId, out var entry))
            {
                return await FailAndFallbackAsync(
                    destinationId,
                    SceneStreamStage.Resolving,
                    new InvalidOperationException(
                        $"Destination '{destinationId}' is absent from SceneCatalog."),
                    progress);
            }

            var previousScene = m_Backend.CaptureActiveScene();
            var previousMode = m_ModeController.CurrentMode;
            ISceneLoadHandle handle = null;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                handle = m_Backend.BeginLoad(entry.Address, destinationId);
                if (handle == null)
                {
                    throw new InvalidOperationException(
                        "The scene stream backend returned no load handle.");
                }

                while (!handle.LoadTask.IsCompleted)
                {
                    progress.Publish(
                        SceneStreamStage.Loading,
                        0.05f + (Mathf.Clamp01(handle.PercentComplete) * 0.7f));
                    await Task.Yield();
                }

                await handle.LoadTask;
                if (handle.LoadFailure != null)
                {
                    throw handle.LoadFailure;
                }

                cancellationToken.ThrowIfCancellationRequested();
                progress.Publish(SceneStreamStage.Activating, 0.8f);
                await handle.ActivateAsync();
                cancellationToken.ThrowIfCancellationRequested();
                (m_FallbackTransition as ISceneBindingLifecycle)?
                    .BindActiveScene();
                progress.Publish(SceneStreamStage.CommittingMode, 0.95f);
                await m_ModeController.EnterAsync(
                    entry.TargetMode,
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                lock (m_Gate)
                {
                    m_LoadedHandle = handle;
                    m_LoadedDestinationId = destinationId;
                    m_PreviousActiveScene = previousScene;
                    m_PreviousMode = previousMode;
                }

                progress.Publish(SceneStreamStage.Completed, 1f);
                return new SceneStreamResult(
                    SceneStreamStatus.Loaded,
                    destinationId,
                    null);
            }
            catch (OperationCanceledException)
            {
                var cancellation = new OperationCanceledException(
                    cancellationToken);
                try
                {
                    await CleanupHandleAsync(handle, previousScene, progress);
                }
                catch (Exception cleanupFailure)
                {
                    throw CombineFailures(
                        "Cancelled scene load also failed to clean up.",
                        cancellation,
                        cleanupFailure);
                }

                throw cancellation;
            }
            catch (Exception exception)
            {
                var failure = exception;
                try
                {
                    await CleanupHandleAsync(handle, previousScene, progress);
                }
                catch (Exception cleanupFailure)
                {
                    failure = CombineFailures(
                        "Scene load failure also failed to clean up.",
                        exception,
                        cleanupFailure);
                }

                return await FailAndFallbackAsync(
                    destinationId,
                    ResolveFailureStage(handle),
                    failure,
                    progress);
            }
        }

        private async Task<SceneStreamResult> RunUnloadAsync(
            string destinationId,
            CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            var progress = new ProgressPublisher(this, destinationId);
            ISceneLoadHandle handle;
            object previousScene;
            GameMode previousMode;
            lock (m_Gate)
            {
                handle = m_LoadedHandle;
                previousScene = m_PreviousActiveScene;
                previousMode = m_PreviousMode;
                m_LoadedHandle = null;
                m_LoadedDestinationId = null;
                m_PreviousActiveScene = null;
            }

            progress.Publish(SceneStreamStage.CleaningUp, 0f);
            try
            {
                await CleanupHandleAsync(handle, previousScene, progress);
            }
            catch (Exception cleanupFailure)
            {
                return await FailAndFallbackAsync(
                    destinationId,
                    SceneStreamStage.CleaningUp,
                    cleanupFailure,
                    progress);
            }

            cancellationToken.ThrowIfCancellationRequested();
            await m_ModeController.RecoverAsync(
                previousMode,
                cancellationToken);
            progress.Publish(SceneStreamStage.Completed, 1f);
            return new SceneStreamResult(
                SceneStreamStatus.Unloaded,
                destinationId,
                null);
        }

        private async Task<SceneStreamResult> FailAndFallbackAsync(
            string destinationId,
            SceneStreamStage stage,
            Exception failure,
            ProgressPublisher progress)
        {
            var message =
                $"Destination '{destinationId}' failed during {stage}: " +
                failure.Message;
            var diagnostic = new SceneStreamDiagnostic(
                destinationId,
                stage,
                message,
                failure);
            PublishDiagnostic(diagnostic);
            progress.Publish(SceneStreamStage.Failed, 1f);
            try
            {
                await m_FallbackTransition.RouteAsync(
                    m_Catalog.FallbackSceneName,
                    CancellationToken.None);
                await m_ModeController.RecoverAsync(
                    m_Catalog.FallbackMode,
                    CancellationToken.None);
            }
            catch (Exception fallbackFailure)
            {
                message += " Safe fallback also failed: " + fallbackFailure.Message;
                Debug.LogException(fallbackFailure);
            }

            return new SceneStreamResult(
                SceneStreamStatus.Failed,
                destinationId,
                message);
        }

        private async Task CleanupHandleAsync(
            ISceneLoadHandle handle,
            object previousScene,
            ProgressPublisher progress)
        {
            progress.Publish(SceneStreamStage.CleaningUp, 0.98f);
            List<Exception> failures = null;
            try
            {
                (m_FallbackTransition as ISceneBindingLifecycle)?
                    .ReleaseBindings();
            }
            catch (Exception exception)
            {
                failures = new List<Exception> { exception };
            }
            if (handle != null)
            {
                try
                {
                    if (handle.LoadTask.IsCompleted && handle.LoadFailure == null)
                    {
                        await handle.UnloadAsync();
                    }
                }
                catch (Exception exception)
                {
                    AddFailure(ref failures, exception);
                }

                try
                {
                    handle.Release();
                }
                catch (Exception exception)
                {
                    AddFailure(ref failures, exception);
                }
            }

            try
            {
                m_Backend.RestoreActiveScene(previousScene);
            }
            catch (Exception exception)
            {
                AddFailure(ref failures, exception);
            }

            if (failures != null)
            {
                throw new AggregateException(
                    "Scene stream cleanup failed.",
                    failures);
            }
        }

        private async Task RunShutdownAsync(Task<SceneStreamResult> activeOperation)
        {
            await Task.Yield();
            List<Exception> failures = null;
            if (activeOperation != null)
            {
                try
                {
                    await activeOperation;
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception exception)
                {
                    AddFailure(ref failures, exception);
                }
            }

            ISceneLoadHandle loadedHandle;
            object previousScene;
            lock (m_Gate)
            {
                loadedHandle = m_LoadedHandle;
                previousScene = m_PreviousActiveScene;
                m_LoadedHandle = null;
                m_LoadedDestinationId = null;
                m_PreviousActiveScene = null;
            }

            if (loadedHandle != null)
            {
                var progress = new ProgressPublisher(this, "shutdown");
                try
                {
                    await CleanupHandleAsync(loadedHandle, previousScene, progress);
                }
                catch (Exception exception)
                {
                    AddFailure(ref failures, exception);
                }
            }

            try
            {
                await ReleaseCatalogAsync();
            }
            catch (Exception exception)
            {
                AddFailure(ref failures, exception);
            }
            finally
            {
                lock (m_Gate)
                {
                    m_Catalog = null;
                    m_IsInitialized = false;
                }
            }

            if (failures != null)
            {
                throw new AggregateException(
                    "Scene stream shutdown encountered cleanup failures.",
                    failures);
            }
        }

        private static void AddFailure(
            ref List<Exception> failures,
            Exception failure)
        {
            failures ??= new List<Exception>();
            failures.Add(failure);
        }

        private static AggregateException CombineFailures(
            string message,
            Exception first,
            Exception second)
        {
            return new AggregateException(message, first, second);
        }

        private async ValueTask ReleaseCatalogAsync()
        {
            lock (m_Gate)
            {
                if (m_CatalogReleased)
                {
                    return;
                }

                m_CatalogReleased = true;
            }

            await m_CatalogSource.ReleaseAsync();
        }

        private void EnsureOperationalLocked()
        {
            if (!m_IsInitialized)
            {
                throw new InvalidOperationException(
                    "SceneStreamService must be initialized before streaming.");
            }

            if (m_ShutdownTask != null)
            {
                throw new InvalidOperationException(
                    "SceneStreamService cannot stream after shutdown begins.");
            }

            if (m_ActiveOperation != null && !m_ActiveOperation.IsCompleted)
            {
                throw new InvalidOperationException(
                    "Another scene stream operation is already in flight.");
            }
        }

        private void PublishProgress(SceneStreamProgress progress)
        {
            InvokeSubscribersSafely(TransitionProgressed, progress);
        }

        private void PublishDiagnostic(SceneStreamDiagnostic diagnostic)
        {
            InvokeSubscribersSafely(DiagnosticRecorded, diagnostic);
        }

        private static void InvokeSubscribersSafely<T>(Action<T> handlers, T value)
        {
            if (handlers == null)
            {
                return;
            }

            foreach (Action<T> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(value);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private static SceneStreamStage ResolveFailureStage(
            ISceneLoadHandle handle)
        {
            if (handle == null || handle.LoadFailure != null)
            {
                return SceneStreamStage.Loading;
            }

            return SceneStreamStage.Activating;
        }

        private static void RequireDestinationId(string destinationId)
        {
            if (string.IsNullOrWhiteSpace(destinationId) ||
                !string.Equals(
                    destinationId,
                    destinationId.Trim(),
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Destination ID must be non-empty and already trimmed.",
                    nameof(destinationId));
            }
        }

        private sealed class ProgressPublisher
        {
            private readonly SceneStreamService m_Owner;
            private readonly string m_DestinationId;

            private float m_LastProgress;

            public ProgressPublisher(
                SceneStreamService owner,
                string destinationId)
            {
                m_Owner = owner;
                m_DestinationId = destinationId;
            }

            public void Publish(SceneStreamStage stage, float normalizedProgress)
            {
                var monotonic = Mathf.Max(
                    m_LastProgress,
                    Mathf.Clamp01(normalizedProgress));
                m_LastProgress = monotonic;
                m_Owner.PublishProgress(new SceneStreamProgress(
                    m_DestinationId,
                    stage,
                    monotonic));
            }
        }
    }

    internal sealed class SceneRoutingTransition :
        ISceneTransition,
        ISceneBindingLifecycle
    {
        private SceneStreamService m_Stream;
        private ISceneTransition m_Fallback;

        public void Configure(
            SceneStreamService stream,
            ISceneTransition fallback)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (fallback == null) throw new ArgumentNullException(nameof(fallback));
            if (m_Stream != null)
            {
                if (ReferenceEquals(m_Stream, stream) &&
                    ReferenceEquals(m_Fallback, fallback))
                {
                    return;
                }
                throw new InvalidOperationException(
                    "Scene routing transition is already composition-owned.");
            }
            m_Stream = stream;
            m_Fallback = fallback;
        }

        public async ValueTask RouteAsync(
            string destination,
            CancellationToken cancellationToken)
        {
            if (m_Stream == null || m_Fallback == null)
            {
                throw new InvalidOperationException(
                    "Scene routing transition is not configured.");
            }

            if (!m_Stream.TryResolveDestination(destination, out var destinationId))
            {
                await UnloadCurrentAsync(cancellationToken);
                await m_Fallback.RouteAsync(destination, cancellationToken);
                if (string.Equals(
                        destination,
                        m_Stream.FallbackSceneName,
                        StringComparison.Ordinal))
                {
                    await m_Stream.ModeController.RecoverAsync(
                        m_Stream.FallbackMode,
                        cancellationToken);
                }
                return;
            }

            var current = m_Stream.LoadedDestinationId;
            if (string.Equals(current, destinationId, StringComparison.Ordinal))
            {
                return;
            }
            await UnloadCurrentAsync(cancellationToken);
            var result = await m_Stream.LoadDestinationAsync(
                destinationId,
                cancellationToken);
            if (result.Status == SceneStreamStatus.Failed)
            {
                throw new InvalidOperationException(result.Diagnostic);
            }
        }

        public void BindActiveScene()
        {
            RequireFallbackLifecycle().BindActiveScene();
        }

        public void ReleaseBindings()
        {
            RequireFallbackLifecycle().ReleaseBindings();
        }

        private async ValueTask UnloadCurrentAsync(
            CancellationToken cancellationToken)
        {
            var current = m_Stream.LoadedDestinationId;
            if (string.IsNullOrEmpty(current)) return;
            var result = await m_Stream.UnloadDestinationAsync(
                current,
                cancellationToken);
            if (result.Status == SceneStreamStatus.Failed)
            {
                throw new InvalidOperationException(result.Diagnostic);
            }
        }

        private ISceneBindingLifecycle RequireFallbackLifecycle()
        {
            if (m_Fallback is ISceneBindingLifecycle lifecycle)
            {
                return lifecycle;
            }
            throw new InvalidOperationException(
                "Scene routing requires a binding-aware fallback transition.");
        }
    }
}
