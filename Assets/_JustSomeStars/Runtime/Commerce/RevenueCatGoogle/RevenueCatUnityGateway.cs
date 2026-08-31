using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Core;
using UnityEngine;
using UnityEngine.Scripting;

namespace JustSomeStars.Runtime.Commerce.RevenueCatGoogle
{
    internal sealed class RevenueCatUnityGateway : IRevenueCatGateway
    {
        private readonly RevenueCatRuntimeConfiguration m_Configuration;
        private GameObject m_GameObject;
        private Purchases m_Purchases;
        private RedactingLogHandler m_LogHandler;
        private ILogHandler m_PreviousLogHandler;
        private long m_IdentityGeneration;
        private int m_CustomerInfoRefreshActive;
        private bool m_Initialized;

        public RevenueCatUnityGateway(RevenueCatRuntimeConfiguration configuration)
        {
            m_Configuration = configuration ??
                throw new ArgumentNullException(nameof(configuration));
        }

        public bool IsConfigured => true;

        public string AppFingerprint => m_Configuration.Fingerprint;

        public StoreEnvironment Environment => m_Configuration.Environment;

        public string AndroidPackageId => m_Configuration.PackageId;

        public string CurrentAppUserId { get; private set; } = string.Empty;

        public event Action<RevenueCatCustomerInfo> CustomerInfoUpdated;

        public async ValueTask<StartupResult> InitializeAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (m_Initialized)
            {
                return StartupResult.Available();
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            m_PreviousLogHandler = Debug.unityLogger.logHandler;
            m_LogHandler = new RedactingLogHandler(
                m_PreviousLogHandler,
                () => CurrentAppUserId,
                m_Configuration.ApiKey);
            Debug.unityLogger.logHandler = m_LogHandler;

            var ready = new TaskCompletionSource<Exception>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            m_GameObject = new GameObject("JssRevenueCatGoogleProvider");
            m_GameObject.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(m_GameObject);
            m_Purchases = m_GameObject.AddComponent<Purchases>();
            m_Purchases.useRuntimeSetup = true;
            var listener = m_GameObject.AddComponent<RevenueCatCustomerInfoListener>();
            listener.Owner = this;
            m_Purchases.listener = listener;
            var driver = m_GameObject.AddComponent<RevenueCatBridgeDriver>();
            driver.Configure(m_Purchases, ConfigureAfterSdkStart, ready);
            m_GameObject.SetActive(true);
            using var registration = cancellationToken.Register(
                () => ready.TrySetCanceled(cancellationToken));
            var failure = await ready.Task;
            if (failure != null)
            {
                await ShutdownAsync();
                return StartupResult.Unavailable(
                    "RevenueCat could not initialize on this Android build.",
                    failure);
            }

            CurrentAppUserId = m_Purchases.GetAppUserId() ?? string.Empty;
            m_Initialized = !string.IsNullOrWhiteSpace(CurrentAppUserId);
            Interlocked.Increment(ref m_IdentityGeneration);
            return m_Initialized
                ? StartupResult.Available()
                : StartupResult.Unavailable(
                    "RevenueCat did not provide a local anonymous identity.");
#else
            await Task.Yield();
            return StartupResult.Unavailable(
                "RevenueCat runtime commerce is available only in an Android player.");
#endif
        }

        public ValueTask ShutdownAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            m_Initialized = false;
            Interlocked.Increment(ref m_IdentityGeneration);
            Interlocked.Exchange(ref m_CustomerInfoRefreshActive, 0);
            CurrentAppUserId = string.Empty;
            if (ReferenceEquals(Debug.unityLogger.logHandler, m_LogHandler) &&
                m_PreviousLogHandler != null)
            {
                Debug.unityLogger.logHandler = m_PreviousLogHandler;
            }

            m_LogHandler = null;
            m_PreviousLogHandler = null;
            if (m_GameObject != null)
            {
                UnityEngine.Object.Destroy(m_GameObject);
            }

            m_GameObject = null;
            m_Purchases = null;
#endif
            return default;
        }

        public async ValueTask<IReadOnlyList<RevenueCatGatewayProduct>>
            GetProductsAsync(CancellationToken cancellationToken)
        {
            RequireReady();
            var completion = NewCompletion<IReadOnlyList<RevenueCatGatewayProduct>>();
            using var registration = cancellationToken.Register(
                () => completion.TrySetCanceled(cancellationToken));
            m_Purchases.GetOfferings((offerings, error) =>
            {
                if (error != null || offerings?.Current == null)
                {
                    completion.TrySetResult(Array.Empty<RevenueCatGatewayProduct>());
                    return;
                }

                var offeringId = offerings.Current.Identifier ?? string.Empty;
                var products = offerings.Current.AvailablePackages
                    .Where(package => package?.StoreProduct != null)
                    .Select(package => new RevenueCatGatewayProduct(
                        package.StoreProduct.Identifier,
                        offeringId,
                        package.Identifier,
                        package.StoreProduct.Title,
                        package.StoreProduct.Description,
                        package.StoreProduct.PriceString,
                        package.StoreProduct.CurrencyCode))
                    .ToArray();
                completion.TrySetResult(products);
            });
            return await completion.Task;
        }

        public async ValueTask<RevenueCatGatewayResult> PurchaseAsync(
            string storeProductId,
            CancellationToken cancellationToken)
        {
            RequireReady();
            cancellationToken.ThrowIfCancellationRequested();
            var expectedAppUserId = CurrentAppUserId;
            var generation = Interlocked.Read(ref m_IdentityGeneration);
            var completion = NewCompletion<RevenueCatGatewayResult>();
            m_Purchases.PurchaseProduct(
                storeProductId,
                result => completion.TrySetResult(
                    IdentityStillMatches(expectedAppUserId, generation)
                        ? Project(result, expectedAppUserId)
                        : Failed(
                            "The game profile changed while the store was open.")),
                type: "inapp");
            return await completion.Task;
        }

        public ValueTask<RevenueCatGatewayResult> RestoreAsync(
            CancellationToken cancellationToken) => CustomerInfoOperation(
                callback => m_Purchases.RestorePurchases(callback),
                cancellationToken);

        public ValueTask<RevenueCatGatewayResult> RefreshAsync(
            CancellationToken cancellationToken) => CustomerInfoOperation(
                callback => m_Purchases.GetCustomerInfo(callback),
                cancellationToken);

        public async ValueTask<RevenueCatGatewayResult> LogInAsync(
            string firebaseUserId,
            CancellationToken cancellationToken)
        {
            RequireReady();
            cancellationToken.ThrowIfCancellationRequested();
            var generation = Interlocked.Increment(ref m_IdentityGeneration);
            var completion = NewCompletion<RevenueCatGatewayResult>();
            m_Purchases.LogIn(firebaseUserId, (info, created, error) =>
            {
                var resolvedAppUserId = m_Purchases.GetAppUserId() ?? string.Empty;
                if (!m_Initialized ||
                    generation != Interlocked.Read(ref m_IdentityGeneration) ||
                    error != null ||
                    info == null ||
                    !string.Equals(
                        resolvedAppUserId,
                        firebaseUserId,
                        StringComparison.Ordinal))
                {
                    completion.TrySetResult(Failed(
                        "RevenueCat did not confirm the requested game profile."));
                    return;
                }

                CurrentAppUserId = resolvedAppUserId;
                completion.TrySetResult(new RevenueCatGatewayResult(
                    RevenueCatGatewayResultStatus.Succeeded,
                    Project(info, firebaseUserId),
                    string.Empty));
            });
            return await completion.Task;
        }

        public async ValueTask<RevenueCatGatewayResult> LogOutAsync(
            CancellationToken cancellationToken)
        {
            RequireReady();
            cancellationToken.ThrowIfCancellationRequested();
            var previousAppUserId = CurrentAppUserId;
            var generation = Interlocked.Increment(ref m_IdentityGeneration);
            var completion = NewCompletion<RevenueCatGatewayResult>();
            m_Purchases.LogOut((info, error) =>
            {
                var resolvedAppUserId = m_Purchases.GetAppUserId() ?? string.Empty;
                if (!m_Initialized ||
                    generation != Interlocked.Read(ref m_IdentityGeneration) ||
                    error != null ||
                    info == null ||
                    string.IsNullOrWhiteSpace(resolvedAppUserId) ||
                    string.Equals(
                        resolvedAppUserId,
                        previousAppUserId,
                        StringComparison.Ordinal) ||
                    !resolvedAppUserId.StartsWith(
                        "$RCAnonymousID:",
                        StringComparison.Ordinal))
                {
                    completion.TrySetResult(Failed(
                        "RevenueCat did not confirm a new anonymous profile."));
                    return;
                }

                CurrentAppUserId = resolvedAppUserId;
                completion.TrySetResult(new RevenueCatGatewayResult(
                    RevenueCatGatewayResultStatus.Succeeded,
                    Project(info, resolvedAppUserId),
                    string.Empty));
            });
            return await completion.Task;
        }

        internal void ReceiveCustomerInfo(Purchases.CustomerInfo info)
        {
            if (!m_Initialized || info == null)
            {
                return;
            }

            RefreshCustomerInfoFromSdk();
        }

        private void RefreshCustomerInfoFromSdk()
        {
            if (!m_Initialized ||
                Interlocked.CompareExchange(
                    ref m_CustomerInfoRefreshActive,
                    1,
                    0) != 0)
            {
                return;
            }

            var expectedAppUserId = CurrentAppUserId;
            var generation = Interlocked.Read(ref m_IdentityGeneration);
            try
            {
                m_Purchases.GetCustomerInfo((info, error) =>
                {
                    try
                    {
                        if (!IdentityStillMatches(
                                expectedAppUserId,
                                generation) ||
                            error != null ||
                            info == null)
                        {
                            return;
                        }

                        CustomerInfoUpdated?.Invoke(
                            Project(info, expectedAppUserId));
                    }
                    finally
                    {
                        Interlocked.Exchange(
                            ref m_CustomerInfoRefreshActive,
                            0);
                    }
                });
            }
            catch
            {
                Interlocked.Exchange(ref m_CustomerInfoRefreshActive, 0);
                throw;
            }
        }

        private void ConfigureAfterSdkStart()
        {
            var configuration = Purchases.PurchasesConfiguration.Builder
                .Init(m_Configuration.ApiKey)
                .SetPurchasesAreCompletedBy(
                    Purchases.PurchasesAreCompletedBy.RevenueCat,
                    Purchases.StoreKitVersion.Default)
                .SetDangerousSettings(new Purchases.DangerousSettings(
                    autoSyncPurchases: true))
                .SetShouldShowInAppMessagesAutomatically(true)
                .SetEntitlementVerificationMode(
                    Purchases.EntitlementVerificationMode.Informational)
                .SetPendingTransactionsForPrepaidPlansEnabled(false)
                .SetDiagnosticsEnabled(false)
                .SetAutomaticDeviceIdentifierCollectionEnabled(false)
                .Build();
            m_Purchases.Configure(configuration);
        }

        private async ValueTask<RevenueCatGatewayResult> CustomerInfoOperation(
            Action<Purchases.CustomerInfoFunc> start,
            CancellationToken cancellationToken)
        {
            RequireReady();
            var expectedAppUserId = CurrentAppUserId;
            var generation = Interlocked.Read(ref m_IdentityGeneration);
            var completion = NewCompletion<RevenueCatGatewayResult>();
            using var registration = cancellationToken.Register(
                () => completion.TrySetCanceled(cancellationToken));
            start((info, error) =>
            {
                completion.TrySetResult(
                    IdentityStillMatches(expectedAppUserId, generation)
                        ? Project(info, error, expectedAppUserId)
                        : Failed(
                            "The game profile changed before verification completed."));
            });
            return await completion.Task;
        }

        private RevenueCatGatewayResult Project(
            Purchases.PurchaseResult result,
            string expectedAppUserId)
        {
            if (result == null)
            {
                return Failed("The store returned no purchase result.");
            }

            if (result.UserCancelled)
            {
                return new RevenueCatGatewayResult(
                    RevenueCatGatewayResultStatus.Cancelled,
                    null,
                    "Purchase cancelled.");
            }

            if (result.Error != null)
            {
                var pending = (result.Error.ReadableErrorCode ?? string.Empty)
                    .IndexOf("PaymentPending", StringComparison.OrdinalIgnoreCase) >= 0;
                return new RevenueCatGatewayResult(
                    pending
                        ? RevenueCatGatewayResultStatus.Pending
                        : RevenueCatGatewayResultStatus.Failed,
                    null,
                    pending ? "Purchase pending." : "The store did not confirm purchase.");
            }

            return result.CustomerInfo == null
                ? Failed("The store returned no verified customer information.")
                : new RevenueCatGatewayResult(
                    RevenueCatGatewayResultStatus.Succeeded,
                    Project(result.CustomerInfo, expectedAppUserId),
                    string.Empty);
        }

        private RevenueCatGatewayResult Project(
            Purchases.CustomerInfo info,
            Purchases.Error error,
            string expectedAppUserId)
        {
            if (error != null || info == null)
            {
                return Failed("The store could not verify customer information.");
            }

            return new RevenueCatGatewayResult(
                RevenueCatGatewayResultStatus.Succeeded,
                Project(info, expectedAppUserId),
                string.Empty);
        }

        private RevenueCatCustomerInfo Project(
            Purchases.CustomerInfo info,
            string expectedAppUserId)
        {
            var entitlements = info.Entitlements.All
                .Select(pair => new RevenueCatEntitlement(
                    pair.Key,
                    pair.Value.IsActive,
                    Project(pair.Value.Verification)))
                .ToArray();
            return new RevenueCatCustomerInfo(
                expectedAppUserId,
                Project(info.Entitlements.Verification),
                DateTime.SpecifyKind(info.RequestDate, DateTimeKind.Utc),
                entitlements);
        }

        private bool IdentityStillMatches(
            string expectedAppUserId,
            long generation)
        {
            if (!m_Initialized ||
                generation != Interlocked.Read(ref m_IdentityGeneration) ||
                !string.Equals(
                    CurrentAppUserId,
                    expectedAppUserId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                return string.Equals(
                    m_Purchases.GetAppUserId(),
                    expectedAppUserId,
                    StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        private static EntitlementVerification Project(
            Purchases.VerificationResult verification)
        {
            switch (verification)
            {
                case Purchases.VerificationResult.Verified:
                    return EntitlementVerification.Verified;
                case Purchases.VerificationResult.VerifiedOnDevice:
                    return EntitlementVerification.VerifiedOnDevice;
                case Purchases.VerificationResult.Failed:
                    return EntitlementVerification.Failed;
                default:
                    return EntitlementVerification.NotRequested;
            }
        }

        private static RevenueCatGatewayResult Failed(string message) =>
            new RevenueCatGatewayResult(
                RevenueCatGatewayResultStatus.Failed,
                null,
                message);

        private void RequireReady()
        {
            if (!m_Initialized || m_Purchases == null)
            {
                throw new InvalidOperationException(
                    "RevenueCat Android gateway is not initialized.");
            }
        }

        private static TaskCompletionSource<T> NewCompletion<T>() =>
            new TaskCompletionSource<T>(
                TaskCreationOptions.RunContinuationsAsynchronously);
    }

    [Preserve]
    internal sealed class RevenueCatCustomerInfoListener :
        Purchases.UpdatedCustomerInfoListener
    {
        internal RevenueCatUnityGateway Owner { get; set; }

        public override void CustomerInfoReceived(Purchases.CustomerInfo customerInfo)
        {
            Owner?.ReceiveCustomerInfo(customerInfo);
        }
    }

    [Preserve]
    internal sealed class RevenueCatBridgeDriver : MonoBehaviour
    {
        private Action m_Configure;
        private TaskCompletionSource<Exception> m_Ready;

        internal void Configure(
            Purchases purchases,
            Action configure,
            TaskCompletionSource<Exception> ready)
        {
            _ = purchases ?? throw new ArgumentNullException(nameof(purchases));
            m_Configure = configure ?? throw new ArgumentNullException(nameof(configure));
            m_Ready = ready ?? throw new ArgumentNullException(nameof(ready));
        }

        private IEnumerator Start()
        {
            yield return null;
            try
            {
                m_Configure();
                m_Ready.TrySetResult(null);
            }
            catch (Exception exception)
            {
                m_Ready.TrySetResult(exception);
            }
        }
    }

    internal sealed class RedactingLogHandler : ILogHandler
    {
        private readonly ILogHandler m_Inner;
        private readonly Func<string> m_CurrentUserId;
        private readonly string m_ApiKey;

        public RedactingLogHandler(
            ILogHandler inner,
            Func<string> currentUserId,
            string apiKey)
        {
            m_Inner = inner ?? throw new ArgumentNullException(nameof(inner));
            m_CurrentUserId = currentUserId ??
                throw new ArgumentNullException(nameof(currentUserId));
            m_ApiKey = apiKey ?? string.Empty;
        }

        public void LogFormat(
            LogType logType,
            UnityEngine.Object context,
            string format,
            params object[] args)
        {
            var rendered = args == null || args.Length == 0
                ? format ?? string.Empty
                : string.Format(format ?? string.Empty, args);
            if (rendered.StartsWith("_getCustomerInfo ", StringComparison.Ordinal) ||
                rendered.StartsWith("_receiveCustomerInfo ", StringComparison.Ordinal) ||
                rendered.StartsWith("_makePurchase ", StringComparison.Ordinal) ||
                rendered.StartsWith("_restorePurchases ", StringComparison.Ordinal) ||
                rendered.StartsWith("_logIn ", StringComparison.Ordinal) ||
                rendered.StartsWith("_logOut ", StringComparison.Ordinal))
            {
                m_Inner.LogFormat(
                    logType,
                    context,
                    "[RevenueCat callback payload redacted]");
                return;
            }

            var safe = Redact(rendered, m_ApiKey);
            safe = Redact(safe, m_CurrentUserId());
            m_Inner.LogFormat(logType, context, "{0}", safe);
        }

        public void LogException(Exception exception, UnityEngine.Object context)
        {
            if (exception == null)
            {
                return;
            }

            var message = exception.Message ?? string.Empty;
            var safe = Redact(Redact(message, m_ApiKey), m_CurrentUserId());
            if (string.Equals(message, safe, StringComparison.Ordinal))
            {
                m_Inner.LogException(exception, context);
                return;
            }

            m_Inner.LogFormat(
                LogType.Exception,
                context,
                "{0}: {1} [sensitive values redacted]",
                exception.GetType().FullName,
                safe);
        }

        private static string Redact(string source, string secret) =>
            string.IsNullOrEmpty(secret)
                ? source
                : source.Replace(secret, "[redacted]");
    }
}
