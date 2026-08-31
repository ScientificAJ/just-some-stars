package com.scientificaj.justsomestars.galaxy;

import android.app.Activity;
import com.samsung.android.sdk.iap.lib.helper.HelperDefine;
import com.samsung.android.sdk.iap.lib.helper.IapHelper;

/**
 * Galaxy-only facade. It returns opaque callback JSON to managed code; managed
 * code must send purchase IDs to the trusted receipt verifier before granting.
 */
public final class JssSamsungIapBridge {
    public interface Callback {
        void onResult(String operation, String payload);
    }

    private static IapHelper helper;

    private JssSamsungIapBridge() { }

    public static void configure(Activity activity, String mode) {
        if (activity == null) {
            throw new IllegalArgumentException("activity");
        }
        helper = IapHelper.getInstance(activity.getApplicationContext());
        if ("PRODUCTION".equals(mode)) {
            helper.setOperationMode(
                HelperDefine.OperationMode.OPERATION_MODE_PRODUCTION);
        } else if ("TEST".equals(mode)) {
            helper.setOperationMode(
                HelperDefine.OperationMode.OPERATION_MODE_TEST);
        } else if ("TEST_FAILURE".equals(mode)) {
            helper.setOperationMode(
                HelperDefine.OperationMode.OPERATION_MODE_TEST_FAILURE);
        } else {
            throw new IllegalArgumentException("Unsupported Samsung IAP mode");
        }
    }

    public static void getProductsDetails(String itemIds, Callback callback) {
        requireHelper();
        boolean sent = helper.getProductsDetails(itemIds, (error, products) ->
            callback.onResult("products", GalaxyJson.products(error, products)));
        if (!sent) {
            callback.onResult("products", GalaxyJson.failed());
        }
    }

    public static void getOwnedList(Callback callback) {
        requireHelper();
        boolean sent = helper.getOwnedList(IapHelper.PRODUCT_TYPE_ALL, (error, products) ->
            callback.onResult("owned", GalaxyJson.owned(error, products)));
        if (!sent) {
            callback.onResult("owned", GalaxyJson.failed());
        }
    }

    public static void startPayment(
        String itemId,
        String obfuscatedAccountId,
        String obfuscatedProfileId,
        Callback callback) {
        requireHelper();
        boolean sent = helper.startPayment(
            itemId,
            obfuscatedAccountId,
            obfuscatedProfileId,
            (error, purchase) -> callback.onResult(
                "payment",
                GalaxyJson.purchase(error, purchase)));
        if (!sent) {
            callback.onResult("payment", GalaxyJson.failed());
        }
    }

    public static void acknowledgePurchases(
        String purchaseIds,
        Callback callback) {
        requireHelper();
        boolean sent = helper.acknowledgePurchases(purchaseIds, (error, results) ->
            callback.onResult(
                "acknowledge",
                GalaxyJson.acknowledgements(error, results)));
        if (!sent) {
            callback.onResult("acknowledge", GalaxyJson.failed());
        }
    }

    public static void dispose() {
        helper = null;
    }

    private static void requireHelper() {
        if (helper == null) {
            throw new IllegalStateException("Samsung IAP is not configured");
        }
    }
}
