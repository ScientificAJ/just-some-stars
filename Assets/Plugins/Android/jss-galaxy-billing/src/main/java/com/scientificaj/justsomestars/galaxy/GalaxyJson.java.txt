package com.scientificaj.justsomestars.galaxy;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.lang.reflect.Method;
import java.util.List;

final class GalaxyJson {
    private GalaxyJson() { }

    static String failed() {
        return "{\"errorCode\":-1}";
    }

    static String products(Object error, List<?> products) {
        return envelope(error, products, null, GalaxyJson::product);
    }

    static String owned(Object error, List<?> products) {
        return envelope(error, products, null, GalaxyJson::purchase);
    }

    static String purchase(Object error, Object purchase) {
        return envelope(error, null, purchase, GalaxyJson::purchase);
    }

    static String acknowledgements(Object error, List<?> results) {
        return envelope(error, results, null, GalaxyJson::acknowledgement);
    }

    private static String envelope(
        Object error,
        List<?> values,
        Object value,
        Encoder encoder) {
        try {
            JSONObject result = new JSONObject();
            result.put("errorCode", integer(error, "getErrorCode", -1));
            if (values != null) {
                JSONArray array = new JSONArray();
                for (Object item : values) {
                    array.put(item == null ? JSONObject.NULL : encoder.encode(item));
                }
                result.put("values", array);
            }
            if (value != null) {
                result.put("value", encoder.encode(value));
            }
            return result.toString();
        } catch (JSONException exception) {
            return "{\"errorCode\":-1}";
        }
    }

    private static JSONObject product(Object value) throws JSONException {
        JSONObject result = new JSONObject();
        result.put("itemId", string(value, "getItemId"));
        result.put("title", string(value, "getItemName"));
        result.put("description", string(value, "getItemDesc"));
        result.put("formattedPrice", string(value, "getItemPriceString"));
        result.put("currencyCode", string(value, "getCurrencyCode"));
        return result;
    }

    private static JSONObject purchase(Object value) throws JSONException {
        JSONObject result = new JSONObject();
        result.put("purchaseId", string(value, "getPurchaseId"));
        result.put("itemId", string(value, "getItemId"));
        result.put(
            "obfuscatedAccountId",
            string(value, "getObfuscatedAccountId"));
        result.put(
            "obfuscatedProfileId",
            string(value, "getObfuscatedProfileId"));
        return result;
    }

    private static JSONObject acknowledgement(Object value) throws JSONException {
        JSONObject result = new JSONObject();
        result.put("purchaseId", string(value, "getPurchaseId"));
        result.put("statusCode", integer(value, "getStatusCode", -1));
        return result;
    }

    private static String string(Object value, String getter) {
        Object result = invoke(value, getter);
        return result instanceof String ? (String)result : "";
    }

    private static int integer(Object value, String getter, int fallback) {
        if (value == null) {
            return fallback;
        }
        Object result = invoke(value, getter);
        return result instanceof Number ? ((Number)result).intValue() : fallback;
    }

    private static Object invoke(Object value, String getter) {
        if (value == null) {
            return null;
        }
        try {
            Method method = value.getClass().getMethod(getter);
            return method.invoke(value);
        } catch (ReflectiveOperationException exception) {
            return null;
        }
    }

    private interface Encoder {
        JSONObject encode(Object value) throws JSONException;
    }
}
