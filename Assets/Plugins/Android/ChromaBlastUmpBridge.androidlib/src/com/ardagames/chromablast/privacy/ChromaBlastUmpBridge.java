package com.ardagames.chromablast.privacy;

import android.app.Activity;
import android.content.Context;
import android.content.SharedPreferences;
import android.content.pm.ApplicationInfo;
import android.content.pm.PackageManager;
import android.os.Bundle;

import com.google.android.ump.ConsentInformation;
import com.google.android.ump.ConsentRequestParameters;
import com.google.android.ump.FormError;
import com.google.android.ump.UserMessagingPlatform;
import com.unity3d.player.UnityPlayer;

import org.json.JSONObject;

/** Minimal Android bridge for Google UMP. LevelPlay reads UMP's TCF/AC state directly. */
public final class ChromaBlastUmpBridge {
    private static final String ADMOB_APP_ID_KEY = "com.google.android.gms.ads.APPLICATION_ID";
    private static ConsentInformation consentInformation;
    private static boolean requestInFlight;

    private ChromaBlastUmpBridge() {}

    public static void requestConsentUpdate(final String unityGameObjectName) {
        final Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            sendState(unityGameObjectName, -1000, "Android activity is unavailable.");
            return;
        }

        activity.runOnUiThread(() -> {
            if (!hasConfiguredGoogleMobileAdsAppId(activity)) {
                sendState(
                    unityGameObjectName,
                    -1001,
                    "Google UMP requires com.google.android.gms.ads.APPLICATION_ID in the merged Android manifest.");
                return;
            }

            consentInformation = UserMessagingPlatform.getConsentInformation(activity);
            if (requestInFlight) {
                return;
            }

            requestInFlight = true;
            ConsentRequestParameters parameters = new ConsentRequestParameters.Builder().build();
            consentInformation.requestConsentInfoUpdate(
                activity,
                parameters,
                () -> UserMessagingPlatform.loadAndShowConsentFormIfRequired(
                    activity,
                    formError -> {
                        requestInFlight = false;
                        sendState(unityGameObjectName, formError);
                    }),
                requestError -> {
                    requestInFlight = false;
                    sendState(unityGameObjectName, requestError);
                });
        });
    }

    public static void showPrivacyOptions(final String unityGameObjectName) {
        final Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            sendState(unityGameObjectName, -1000, "Android activity is unavailable.");
            return;
        }

        activity.runOnUiThread(() -> {
            if (consentInformation == null) {
                consentInformation = UserMessagingPlatform.getConsentInformation(activity);
            }

            UserMessagingPlatform.showPrivacyOptionsForm(
                activity,
                formError -> sendState(unityGameObjectName, formError));
        });
    }

    private static boolean hasConfiguredGoogleMobileAdsAppId(Activity activity) {
        try {
            ApplicationInfo applicationInfo = activity.getPackageManager().getApplicationInfo(
                activity.getPackageName(),
                PackageManager.GET_META_DATA);
            Bundle metadata = applicationInfo.metaData;
            String appId = metadata == null ? null : metadata.getString(ADMOB_APP_ID_KEY);
            return appId != null
                && appId.startsWith("ca-app-pub-")
                && !appId.contains("YOUR_")
                && !appId.contains("PLACEHOLDER");
        } catch (Exception ignored) {
            return false;
        }
    }

    private static void sendState(String unityGameObjectName, FormError error) {
        sendState(
            unityGameObjectName,
            error == null ? 0 : error.getErrorCode(),
            error == null ? "" : error.getMessage());
    }

    private static void sendState(String unityGameObjectName, int errorCode, String errorMessage) {
        boolean canRequestAds = consentInformation != null
            && consentInformation.canRequestAds()
            && hasPurposeOneConsentWhenGdprApplies(UnityPlayer.currentActivity);
        boolean privacyOptionsRequired = consentInformation != null
            && consentInformation.getPrivacyOptionsRequirementStatus()
                == ConsentInformation.PrivacyOptionsRequirementStatus.REQUIRED;
        int consentStatus = consentInformation == null
            ? ConsentInformation.ConsentStatus.UNKNOWN
            : consentInformation.getConsentStatus();

        try {
            JSONObject state = new JSONObject();
            state.put("canRequestAds", canRequestAds);
            state.put("privacyOptionsRequired", privacyOptionsRequired);
            state.put("consentStatus", consentStatus);
            state.put("errorCode", errorCode);
            state.put("errorMessage", errorMessage == null ? "" : errorMessage);
            UnityPlayer.UnitySendMessage(unityGameObjectName, "OnUmpStateUpdated", state.toString());
        } catch (Exception exception) {
            UnityPlayer.UnitySendMessage(
                unityGameObjectName,
                "OnUmpStateUpdated",
                "{\"canRequestAds\":false,\"privacyOptionsRequired\":false,\"consentStatus\":0,"
                    + "\"errorCode\":-1002,\"errorMessage\":\"UMP state serialization failed\"}");
        }
    }

    private static boolean hasPurposeOneConsentWhenGdprApplies(Activity activity) {
        if (activity == null) {
            return false;
        }

        SharedPreferences preferences = activity.getSharedPreferences(
            activity.getPackageName() + "_preferences",
            Context.MODE_PRIVATE);
        int gdprApplies = 0;
        try {
            gdprApplies = preferences.getInt("IABTCF_gdprApplies", 0);
        } catch (ClassCastException exception) {
            String storedValue = preferences.getString("IABTCF_gdprApplies", "0");
            gdprApplies = "1".equals(storedValue) ? 1 : 0;
        }

        if (gdprApplies != 1) {
            return true;
        }

        String purposeConsents = preferences.getString("IABTCF_PurposeConsents", "");
        return purposeConsents != null
            && !purposeConsents.isEmpty()
            && purposeConsents.charAt(0) == '1';
    }
}
