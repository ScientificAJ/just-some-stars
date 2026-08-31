using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JustSomeStars.Runtime.Core;
using UnityEngine;

namespace JustSomeStars.Runtime.Commerce.Galaxy
{
    public sealed class GalaxyAndroidJavaGateway : IGalaxyIapGateway
    {
        private const string BridgeClass =
            "com.scientificaj.justsomestars.galaxy.JssSamsungIapBridge";
        private const int NativeTimeoutMilliseconds = 120000;

#if JSS_GALAXY && UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaClass m_Bridge;
        private AndroidJavaObject m_Activity;
#endif

        public bool IsSupported
        {
            get
            {
#if JSS_GALAXY && UNITY_ANDROID && !UNITY_EDITOR
                return m_Bridge != null && m_Activity != null;
#else
                return false;
#endif
            }
        }

        public ValueTask<StartupResult> InitializeAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
#if JSS_GALAXY && UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                m_Bridge = new AndroidJavaClass(BridgeClass);
                using var player = new AndroidJavaClass(
                    "com.unity3d.player.UnityPlayer");
                m_Activity = player.GetStatic<AndroidJavaObject>(
                    "currentActivity");
                if (m_Activity == null)
                {
                    ShutdownNative();
                    return new ValueTask<StartupResult>(
                        StartupResult.Unavailable(
                            "Samsung IAP activity is unavailable."));
                }

                m_Bridge.CallStatic(
                    "configure",
                    m_Activity,
                    "PRODUCTION");
                return new ValueTask<StartupResult>(StartupResult.Available());
            }
            catch (AndroidJavaException)
            {
                ShutdownNative();
                return new ValueTask<StartupResult>(
                    StartupResult.Unavailable(
                        "Samsung IAP is unavailable on this device."));
            }
#else
            return new ValueTask<StartupResult>(StartupResult.Unavailable(
                "Samsung IAP is only available in a Galaxy build on Android."));
#endif
        }

        public ValueTask ShutdownAsync()
        {
#if JSS_GALAXY && UNITY_ANDROID && !UNITY_EDITOR
            ShutdownNative();
#endif
            return default;
        }

        public async ValueTask<IReadOnlyList<GalaxyNativeProduct>>
            GetProductsDetailsAsync(
                IReadOnlyList<string> itemIds,
                CancellationToken cancellationToken)
        {
            RequireSupported();
            var requested = (itemIds ?? Array.Empty<string>())
                .Where(GalaxyProductMap.IsAllowedItem)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (requested.Length == 0)
            {
                return Array.Empty<GalaxyNativeProduct>();
            }

            var json = await InvokeAsync(
                "products",
                callback => CallBridge(
                    "getProductsDetails",
                    string.Join(",", requested),
                    callback),
                cancellationToken);
            var envelope = Parse<ProductEnvelope>(json);
            if (envelope.errorCode != 0)
            {
                return Array.Empty<GalaxyNativeProduct>();
            }

            return (envelope.values ?? Array.Empty<ProductRecord>())
                .Where(value => value != null &&
                    GalaxyProductMap.IsAllowedItem(value.itemId))
                .Select(value => new GalaxyNativeProduct(
                    value.itemId,
                    value.title,
                    value.description,
                    value.formattedPrice,
                    value.currencyCode))
                .ToArray();
        }

        public async ValueTask<IReadOnlyList<GalaxyNativePurchase>>
            GetOwnedListAsync(CancellationToken cancellationToken)
        {
            RequireSupported();
            var json = await InvokeAsync(
                "owned",
                callback => CallBridge("getOwnedList", callback),
                cancellationToken);
            var envelope = Parse<PurchaseListEnvelope>(json);
            if (envelope.errorCode != 0)
            {
                return Array.Empty<GalaxyNativePurchase>();
            }

            return (envelope.values ?? Array.Empty<PurchaseRecord>())
                .Where(IsUsablePurchase)
                .Select(value => ToNativePurchase(
                    GalaxyNativeStatus.Succeeded,
                    value))
                .ToArray();
        }

        public async ValueTask<GalaxyNativePurchase> StartPaymentAsync(
            string itemId,
            string obfuscatedAccountId,
            string obfuscatedProfileId,
            CancellationToken cancellationToken)
        {
            RequireSupported();
            if (!GalaxyProductMap.IsAllowedItem(itemId) ||
                string.IsNullOrWhiteSpace(obfuscatedAccountId) ||
                string.IsNullOrWhiteSpace(obfuscatedProfileId))
            {
                return EmptyPurchase(GalaxyNativeStatus.Failed);
            }

            var json = await InvokeAsync(
                "payment",
                callback => CallBridge(
                    "startPayment",
                    itemId,
                    obfuscatedAccountId,
                    obfuscatedProfileId,
                    callback),
                cancellationToken,
                honourCancellationAfterDispatch: false);
            var envelope = Parse<PurchaseEnvelope>(json);
            if (envelope.errorCode != 0 || !IsUsablePurchase(envelope.value))
            {
                return EmptyPurchase(GalaxyNativeStatus.Failed);
            }

            return ToNativePurchase(GalaxyNativeStatus.Succeeded, envelope.value);
        }

        public async ValueTask<bool> AcknowledgePurchasesAsync(
            string purchaseId,
            CancellationToken cancellationToken)
        {
            RequireSupported();
            if (string.IsNullOrWhiteSpace(purchaseId))
            {
                return false;
            }

            var json = await InvokeAsync(
                "acknowledge",
                callback => CallBridge(
                    "acknowledgePurchases",
                    purchaseId,
                    callback),
                cancellationToken);
            var envelope = Parse<AcknowledgementEnvelope>(json);
            if (envelope.errorCode != 0)
            {
                return false;
            }

            var results = envelope.values ??
                Array.Empty<AcknowledgementRecord>();
            return results.Length == 1 &&
                   string.Equals(
                       results[0].purchaseId,
                       purchaseId,
                       StringComparison.Ordinal) &&
                   (results[0].statusCode == 0 ||
                    results[0].statusCode == 4);
        }

        private void RequireSupported()
        {
            if (!IsSupported)
            {
                throw new InvalidOperationException(
                    "Samsung IAP is not initialized for this build/device.");
            }
        }

        private static T Parse<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException(
                    "Samsung IAP returned an empty response.");
            }

            var result = JsonUtility.FromJson<T>(json);
            return result ?? throw new InvalidOperationException(
                "Samsung IAP returned a malformed response.");
        }

        private static bool IsUsablePurchase(PurchaseRecord value) =>
            value != null &&
            !string.IsNullOrWhiteSpace(value.purchaseId) &&
            GalaxyProductMap.IsAllowedItem(value.itemId) &&
            !string.IsNullOrWhiteSpace(value.obfuscatedAccountId) &&
            !string.IsNullOrWhiteSpace(value.obfuscatedProfileId);

        private static GalaxyNativePurchase ToNativePurchase(
            GalaxyNativeStatus status,
            PurchaseRecord value) => new GalaxyNativePurchase(
                status,
                value.purchaseId,
                value.itemId,
                value.obfuscatedAccountId,
                value.obfuscatedProfileId);

        private static GalaxyNativePurchase EmptyPurchase(
            GalaxyNativeStatus status) => new GalaxyNativePurchase(
                status,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);

#if JSS_GALAXY && UNITY_ANDROID && !UNITY_EDITOR
        private async Task<string> InvokeAsync(
            string operation,
            Action<ResultCallback> invoke,
            CancellationToken cancellationToken,
            bool honourCancellationAfterDispatch = true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var completion = new TaskCompletionSource<string>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var callback = new ResultCallback(operation, completion);
            invoke(callback);

            var timeout = Task.Delay(NativeTimeoutMilliseconds);
            Task cancellation = honourCancellationAfterDispatch
                ? Task.Delay(Timeout.Infinite, cancellationToken)
                : Task.Delay(Timeout.Infinite);
            var winner = await Task.WhenAny(completion.Task, timeout, cancellation);
            if (winner == cancellation)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
            if (winner != completion.Task)
            {
                throw new TimeoutException(
                    "Samsung IAP did not complete the requested operation.");
            }

            return await completion.Task;
        }

        private void CallBridge(string method, params object[] arguments) =>
            m_Bridge.CallStatic(method, arguments);

        private void ShutdownNative()
        {
            try
            {
                m_Bridge?.CallStatic("dispose");
            }
            catch (AndroidJavaException)
            {
                // Teardown is best-effort and never grants an entitlement.
            }
            finally
            {
                m_Activity?.Dispose();
                m_Activity = null;
                m_Bridge?.Dispose();
                m_Bridge = null;
            }
        }

        private sealed class ResultCallback : AndroidJavaProxy
        {
            private readonly string m_ExpectedOperation;
            private readonly TaskCompletionSource<string> m_Completion;

            public ResultCallback(
                string expectedOperation,
                TaskCompletionSource<string> completion)
                : base(BridgeClass + "$Callback")
            {
                m_ExpectedOperation = expectedOperation;
                m_Completion = completion;
            }

            public void onResult(string operation, string payload)
            {
                if (!string.Equals(
                    operation,
                    m_ExpectedOperation,
                    StringComparison.Ordinal))
                {
                    m_Completion.TrySetException(new InvalidOperationException(
                        "Samsung IAP returned the wrong operation response."));
                    return;
                }

                m_Completion.TrySetResult(payload);
            }
        }
#else
        private Task<string> InvokeAsync(
            string operation,
            Action<object> invoke,
            CancellationToken cancellationToken,
            bool honourCancellationAfterDispatch = true) =>
            throw new PlatformNotSupportedException();

        private void CallBridge(string method, params object[] arguments) =>
            throw new PlatformNotSupportedException();
#endif

        [Serializable]
        private class BasicEnvelope
        {
            public int errorCode;
        }

        [Serializable]
        private sealed class ProductEnvelope : BasicEnvelope
        {
            public ProductRecord[] values;
        }

        [Serializable]
        private sealed class PurchaseListEnvelope : BasicEnvelope
        {
            public PurchaseRecord[] values;
        }

        [Serializable]
        private sealed class PurchaseEnvelope : BasicEnvelope
        {
            public PurchaseRecord value;
        }

        [Serializable]
        private sealed class AcknowledgementEnvelope : BasicEnvelope
        {
            public AcknowledgementRecord[] values;
        }

        [Serializable]
        private sealed class ProductRecord
        {
            public string itemId;
            public string title;
            public string description;
            public string formattedPrice;
            public string currencyCode;
        }

        [Serializable]
        private sealed class PurchaseRecord
        {
            public string purchaseId;
            public string itemId;
            public string obfuscatedAccountId;
            public string obfuscatedProfileId;
        }

        [Serializable]
        private sealed class AcknowledgementRecord
        {
            public string purchaseId;
            public int statusCode;
        }

    }
}
