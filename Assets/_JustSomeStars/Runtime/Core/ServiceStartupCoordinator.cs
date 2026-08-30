using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace JustSomeStars.Runtime.Core
{
    internal static class InitialExperiencePolicy
    {
        private const string FrontendDestination = "Frontend";
        private const string InternalProofDestination = "Mirra2DProof";
        private const string FlightEvidenceDestination = "Task17FlightGraybox";

        internal static string CurrentDestination => ResolveDestinationForInvocation(
            IsDevelopmentVariant,
            IsFlightEvidenceInvocation);

        internal static GameMode CurrentMode => ResolveModeForInvocation(
            IsDevelopmentVariant,
            IsFlightEvidenceInvocation);

        internal static string ResolveDestination(bool isDevelopmentVariant)
        {
            return isDevelopmentVariant
                ? InternalProofDestination
                : FrontendDestination;
        }

        internal static GameMode ResolveMode(bool isDevelopmentVariant)
        {
            return isDevelopmentVariant ? GameMode.Surface : GameMode.Frontend;
        }

        internal static string ResolveDestinationForInvocation(
            bool isDevelopmentVariant,
            bool isFlightEvidenceInvocation)
        {
            return isFlightEvidenceInvocation
                ? FlightEvidenceDestination
                : ResolveDestination(isDevelopmentVariant);
        }

        internal static GameMode ResolveModeForInvocation(
            bool isDevelopmentVariant,
            bool isFlightEvidenceInvocation)
        {
            return isFlightEvidenceInvocation
                ? GameMode.Flight
                : ResolveMode(isDevelopmentVariant);
        }

        private static bool IsDevelopmentVariant
        {
            get
            {
#if JSS_DEVELOPMENT
                return true;
#else
                return false;
#endif
            }
        }

        private static bool IsFlightEvidenceInvocation
        {
            get
            {
#if JSS_TASK17_FLIGHT_EVIDENCE
                return true;
#else
                return false;
#endif
            }
        }
    }

    internal sealed class ServiceStartupCoordinator
    {
        private readonly object m_LifecycleGate = new object();
        private readonly List<IGameService> m_InitializedServices =
            new List<IGameService>();
        private readonly CancellationTokenSource m_ShutdownCancellation =
            new CancellationTokenSource();

        private Task<StartupReport> m_StartupTask;
        private Task m_ShutdownTask;
        private ISceneBindingLifecycle m_SceneBindingLifecycle;
        private bool m_ShutdownCancellationDisposed;
        private bool m_RouteCommitted;

        public StartupReport LastReport { get; private set; }

        public ValueTask<StartupReport> StartupAsync(
            GameBootstrapComposition composition,
            CancellationToken cancellationToken)
        {
            if (composition == null)
            {
                throw new ArgumentNullException(nameof(composition));
            }

            lock (m_LifecycleGate)
            {
                if (m_ShutdownTask != null)
                {
                    throw new InvalidOperationException(
                        "GameBootstrap cannot start after shutdown has begun.");
                }

                if (m_StartupTask == null)
                {
                    m_SceneBindingLifecycle =
                        composition.SceneTransition as ISceneBindingLifecycle;
                    m_StartupTask = RunStartupAsync(
                        composition,
                        cancellationToken);
                }

                return new ValueTask<StartupReport>(m_StartupTask);
            }
        }

        public ValueTask ShutdownAsync()
        {
            TaskCompletionSource<Exception> cancellationIssued = null;
            Task shutdownTask;
            lock (m_LifecycleGate)
            {
                if (m_ShutdownTask == null)
                {
                    cancellationIssued = new TaskCompletionSource<Exception>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    m_ShutdownTask = RunShutdownAfterStartupAsync(
                        cancellationIssued.Task);
                }

                shutdownTask = m_ShutdownTask;
            }

            if (cancellationIssued != null)
            {
                Exception cancellationCallbackFailure = null;
                try
                {
                    m_ShutdownCancellation.Cancel();
                }
                catch (Exception exception)
                {
                    cancellationCallbackFailure = exception;
                }

                cancellationIssued.TrySetResult(cancellationCallbackFailure);
            }

            return new ValueTask(shutdownTask);
        }

        private async Task<StartupReport> RunStartupAsync(
            GameBootstrapComposition composition,
            CancellationToken callerCancellation)
        {
            // Ensure m_StartupTask is published before any service code can re-enter
            // the lifecycle through a shutdown request.
            await Task.Yield();

            var report = new StartupReport();
            var missingRoles = composition.FindMissingRequiredRoles();
            if (missingRoles.Count > 0)
            {
                report.Fail(
                    new InvalidOperationException(
                        $"Missing required service roles: " +
                        $"{string.Join(", ", missingRoles)}."),
                    isCancelled: false);
                return CompleteStartup(report);
            }

            using var linkedCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    callerCancellation,
                    m_ShutdownCancellation.Token);
            var startupCancellation = linkedCancellation.Token;
            var orderedServices = composition.Services
                .OrderBy(registration => (int)registration.Role)
                .ToArray();

            foreach (var registration in orderedServices)
            {
                if (startupCancellation.IsCancellationRequested)
                {
                    return await CancelStartupAsync(
                        report,
                        registration: null,
                        currentService: null,
                        cancellationToken: startupCancellation);
                }

                StartupResult result;
                try
                {
                    result = await registration.Service.InitializeAsync(
                        startupCancellation);
                }
                catch (OperationCanceledException exception)
                {
                    if (startupCancellation.IsCancellationRequested)
                    {
                        return await CancelStartupAsync(
                            report,
                            registration,
                            registration.Service,
                            exception);
                    }

                    if (registration.Requirement == ServiceRequirement.Optional)
                    {
                        report.AddService(
                            registration.Role,
                            registration.Requirement,
                            ServiceStartupState.Unavailable,
                            ResolveExceptionMessage(registration.Role, exception),
                            exception);
                        await ShutdownServiceAsync(registration.Service, report);
                        continue;
                    }

                    report.AddService(
                        registration.Role,
                        registration.Requirement,
                        ServiceStartupState.Failed,
                        ResolveExceptionMessage(registration.Role, exception),
                        exception);
                    report.Fail(exception, isCancelled: false);
                    await ShutdownCurrentThenInitializedAsync(
                        registration.Service,
                        report);
                    return CompleteStartup(report);
                }
                catch (Exception exception)
                {
                    if (registration.Requirement == ServiceRequirement.Optional)
                    {
                        report.AddService(
                            registration.Role,
                            registration.Requirement,
                            ServiceStartupState.Unavailable,
                            ResolveExceptionMessage(registration.Role, exception),
                            exception);
                        await ShutdownServiceAsync(registration.Service, report);
                        continue;
                    }

                    report.AddService(
                        registration.Role,
                        registration.Requirement,
                        ServiceStartupState.Failed,
                        ResolveExceptionMessage(registration.Role, exception),
                        exception);
                    report.Fail(exception, isCancelled: false);
                    await ShutdownCurrentThenInitializedAsync(
                        registration.Service,
                        report);
                    return CompleteStartup(report);
                }

                if (!result.IsAvailable)
                {
                    var resultMessage = ResolveResultMessage(
                        registration.Role,
                        result);
                    if (registration.Requirement == ServiceRequirement.Optional)
                    {
                        report.AddService(
                            registration.Role,
                            registration.Requirement,
                            ServiceStartupState.Unavailable,
                            resultMessage,
                            result.Failure);
                        await ShutdownServiceAsync(registration.Service, report);
                        continue;
                    }

                    var requiredFailure = result.Failure ??
                        new InvalidOperationException(resultMessage);
                    report.AddService(
                        registration.Role,
                        registration.Requirement,
                        ServiceStartupState.Failed,
                        resultMessage,
                        requiredFailure);
                    report.Fail(requiredFailure, isCancelled: false);
                    await ShutdownCurrentThenInitializedAsync(
                        registration.Service,
                        report);
                    return CompleteStartup(report);
                }

                if (startupCancellation.IsCancellationRequested)
                {
                    return await CancelStartupAsync(
                        report,
                        registration,
                        registration.Service,
                        new OperationCanceledException(startupCancellation));
                }

                report.AddService(
                    registration.Role,
                    registration.Requirement,
                    ServiceStartupState.Available,
                    result.Message,
                    null);
                AddInitializedService(registration.Service);
            }

            if (!TryCommitRoute(startupCancellation))
            {
                return await CancelStartupAsync(
                    report,
                    registration: null,
                    currentService: null,
                    cancellationToken: startupCancellation);
            }

            try
            {
                var routeOperation = composition.SceneTransition.RouteAsync(
                    InitialExperiencePolicy.CurrentDestination,
                    CancellationToken.None);
                await routeOperation;
                report.MarkRouted(InitialExperiencePolicy.CurrentDestination);
            }
            catch (Exception exception)
            {
                report.Fail(exception, isCancelled: false);
                await ShutdownInitializedServicesAsync(report);
            }

            return CompleteStartup(report);
        }

        private bool TryCommitRoute(CancellationToken startupCancellation)
        {
            lock (m_LifecycleGate)
            {
                // Shutdown publishes m_ShutdownTask under this same lock. Whichever
                // side enters first owns the boundary: shutdown blocks commitment,
                // or startup commits before invoking external transition code.
                if (m_RouteCommitted)
                {
                    return true;
                }

                if (m_ShutdownTask != null ||
                    startupCancellation.IsCancellationRequested)
                {
                    return false;
                }

                m_RouteCommitted = true;
                return true;
            }
        }

        private async Task<StartupReport> CancelStartupAsync(
            StartupReport report,
            GameServiceRegistration registration,
            IGameService currentService,
            CancellationToken cancellationToken)
        {
            return await CancelStartupAsync(
                report,
                registration,
                currentService,
                new OperationCanceledException(cancellationToken));
        }

        private async Task<StartupReport> CancelStartupAsync(
            StartupReport report,
            GameServiceRegistration registration,
            IGameService currentService,
            OperationCanceledException cancellation)
        {
            if (registration != null)
            {
                report.AddService(
                    registration.Role,
                    registration.Requirement,
                    ServiceStartupState.Cancelled,
                    ResolveExceptionMessage(registration.Role, cancellation),
                    cancellation);
            }

            report.Fail(cancellation, isCancelled: true);
            await ShutdownCurrentThenInitializedAsync(currentService, report);
            return CompleteStartup(report);
        }

        private async Task RunShutdownAfterStartupAsync(
            Task<Exception> cancellationIssued)
        {
            // ShutdownAsync publishes this task before invoking user cancellation
            // callbacks. Awaiting the signal keeps callbacks outside the lifecycle
            // lock while preventing cleanup/disposal from racing cancellation.
            var cancellationCallbackFailure = await cancellationIssued;

            Task<StartupReport> startupTask;
            lock (m_LifecycleGate)
            {
                startupTask = m_StartupTask;
            }

            StartupReport report = null;
            try
            {
                if (startupTask != null)
                {
                    report = await startupTask;
                }
            }
            catch (Exception exception)
            {
                report = LastReport ?? new StartupReport();
                report.Fail(exception, isCancelled: false);
                CompleteStartup(report);
            }

            report ??= LastReport ?? new StartupReport();
            if (cancellationCallbackFailure != null)
            {
                report.AddCleanupFailure(cancellationCallbackFailure);
            }

            try
            {
                await ShutdownInitializedServicesAsync(report);
            }
            finally
            {
                DisposeShutdownCancellation();
            }
        }

        private async Task ShutdownCurrentThenInitializedAsync(
            IGameService currentService,
            StartupReport report)
        {
            if (currentService != null)
            {
                await ShutdownServiceAsync(currentService, report);
            }

            await ShutdownInitializedServicesAsync(report);
        }

        private async Task ShutdownInitializedServicesAsync(StartupReport report)
        {
            ReleaseSceneBindings(report);

            IGameService[] initializedServices;
            lock (m_LifecycleGate)
            {
                initializedServices = m_InitializedServices.ToArray();
                m_InitializedServices.Clear();
            }

            for (var index = initializedServices.Length - 1; index >= 0; index--)
            {
                await ShutdownServiceAsync(initializedServices[index], report);
            }
        }

        private void ReleaseSceneBindings(StartupReport report)
        {
            ISceneBindingLifecycle bindingLifecycle;
            lock (m_LifecycleGate)
            {
                bindingLifecycle = m_SceneBindingLifecycle;
                m_SceneBindingLifecycle = null;
            }

            if (bindingLifecycle == null)
            {
                return;
            }

            try
            {
                bindingLifecycle.ReleaseBindings();
            }
            catch (Exception exception)
            {
                report.AddCleanupFailure(exception);
            }
        }

        private static async Task ShutdownServiceAsync(
            IGameService service,
            StartupReport report)
        {
            try
            {
                await service.ShutdownAsync();
            }
            catch (Exception exception)
            {
                report.AddCleanupFailure(exception);
            }
        }

        private void AddInitializedService(IGameService service)
        {
            lock (m_LifecycleGate)
            {
                m_InitializedServices.Add(service);
            }
        }

        private StartupReport CompleteStartup(StartupReport report)
        {
            lock (m_LifecycleGate)
            {
                LastReport = report;
            }

            return report;
        }

        private void DisposeShutdownCancellation()
        {
            lock (m_LifecycleGate)
            {
                if (m_ShutdownCancellationDisposed)
                {
                    return;
                }

                m_ShutdownCancellationDisposed = true;
            }

            m_ShutdownCancellation.Dispose();
        }

        private static string ResolveResultMessage(
            GameServiceRole role,
            StartupResult result)
        {
            if (!string.IsNullOrWhiteSpace(result.Message))
            {
                return result.Message;
            }

            return $"Service role '{role}' returned an invalid startup result.";
        }

        private static string ResolveExceptionMessage(
            GameServiceRole role,
            Exception exception)
        {
            if (!string.IsNullOrWhiteSpace(exception.Message))
            {
                return exception.Message;
            }

            return $"Service role '{role}' did not complete initialization.";
        }
    }
}
