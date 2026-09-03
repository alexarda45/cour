using System;
using System.Collections;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;

namespace ChromaBlast
{
    public sealed class PrivacyManager : MonoBehaviour
    {
        private const string LegacyPrivacyChoiceKey = "ChromaBlast.PrivacyChoice.v1";

        public const string PrivacyPolicyUrl = "https://ardagamesstudio.github.io/chroma-blast-privacy/";

        private static PrivacyManager instance;

        private bool consentFlowStarted;
        private bool consentFlowCompleted;
        private bool adsCanInitialize;
        private bool privacyOptionsRequired;

#if UNITY_IOS && !UNITY_EDITOR
        private const int IosConsentRetryLimit = 2;
        private const float IosConsentRetryDelaySeconds = 4f;
        private const float IosNativeStartTimeoutSeconds = 3f;

        private Coroutine iosConsentRetryRoutine;
        private Coroutine iosNativeStartWatchdogRoutine;
        private int iosConsentRetryCount;
        private int iosLastConsentStatus;
        private int iosLastPrivacyOptionsStatus;
        private int iosLastAttStatus = -1;
        private int iosLastPrivacyErrorCode;
        private string iosLastPrivacyErrorMessage = "None";
        private string iosLastPrivacyStage = "Managed request pending";

        [DllImport("__Internal", EntryPoint = "ChromaBlastIosPrivacyRequestConsentUpdate")]
        private static extern void RequestIosConsentUpdate(string unityGameObjectName);

        [DllImport("__Internal", EntryPoint = "ChromaBlastIosPrivacyShowPrivacyOptions")]
        private static extern void ShowIosPrivacyOptions(string unityGameObjectName);
#endif

        public static PrivacyManager Instance => instance;
        public bool ConsentFlowCompleted => consentFlowCompleted;
        public bool AdsCanInitialize => adsCanInitialize;
        public bool PrivacyOptionsRequired => privacyOptionsRequired;

        [Serializable]
        private sealed class UmpStatePayload
        {
            public bool canRequestAds = false;
            public bool privacyOptionsRequired = false;
            public int consentStatus = 0;
            public int errorCode = 0;
            public string errorMessage = string.Empty;
        }

        [Serializable]
        private sealed class IosPrivacyStatePayload
        {
            public bool flowCompleted = false;
            public bool canRequestAds = false;
            public bool privacyOptionsRequired = false;
            public int consentStatus = 0;
            public int privacyOptionsRequirementStatus = 0;
            public int privacyOptionsAction = 0;
            public int attAuthorizationStatus = -1;
            public int errorCode = 0;
            public string errorMessage = string.Empty;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            GetOrCreate();
        }

        public static PrivacyManager GetOrCreate()
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindAnyObjectByType<PrivacyManager>();
            if (instance != null)
            {
                return instance;
            }

            GameObject managerObject = new GameObject("PrivacyManager");
            instance = managerObject.AddComponent<PrivacyManager>();
            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            // The former custom AdsDeclined value must never survive as a second,
            // contradictory consent system after Google UMP becomes authoritative.
            if (PlayerPrefs.HasKey(LegacyPrivacyChoiceKey))
            {
                PlayerPrefs.DeleteKey(LegacyPrivacyChoiceKey);
                PlayerPrefs.Save();
            }
        }

        private void Start()
        {
            BeginConsentFlow();
        }

        private void OnDestroy()
        {
#if UNITY_IOS && !UNITY_EDITOR
            StopIosConsentRetry();
            StopIosNativeStartWatchdog();
#endif
            if (instance == this)
            {
                instance = null;
            }
        }

        public void ApplyCurrentPrivacyStateToAds(AdManager adManager)
        {
            adManager?.ApplyPrivacyEligibility(adsCanInitialize);
            BeginConsentFlow();
        }

        public void ShowPrivacyOptions()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using AndroidJavaClass bridge = new AndroidJavaClass(
                    "com.ardagames.chromablast.privacy.ChromaBlastUmpBridge");
                bridge.CallStatic("showPrivacyOptions", gameObject.name);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Privacy] Google UMP privacy options could not open: {exception.Message}");
            }
#elif UNITY_IOS && !UNITY_EDITOR
            try
            {
                LogIosPrivacyDiagnostic("Privacy Options requested from Settings.");
                ShowIosPrivacyOptions(gameObject.name);
            }
            catch (Exception exception)
            {
                LogIosPrivacyWarning(
                    $"iOS UMP privacy options could not open: {exception.Message}");
            }
#else
            Debug.Log("[Privacy] Privacy options are available only on a supported device build.");
#endif
        }

        public bool OpenPrivacyPolicy()
        {
            Application.OpenURL(PrivacyPolicyUrl);
            return true;
        }

        // Called by the Android UMP bridge through UnitySendMessage.
        public void OnUmpStateUpdated(string json)
        {
            UmpStatePayload state = null;
            try
            {
                state = JsonUtility.FromJson<UmpStatePayload>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Privacy] Google UMP returned an unreadable state: {exception.Message}");
            }

            consentFlowCompleted = true;
            adsCanInitialize = state != null && state.canRequestAds;
            privacyOptionsRequired = state != null && state.privacyOptionsRequired;

            AdManager.Instance?.ApplyPrivacyEligibility(adsCanInitialize);

            if (state != null && state.errorCode != 0)
            {
                Debug.LogWarning(
                    $"[Privacy] Google UMP flow completed with error {state.errorCode}: {state.errorMessage}. "
                    + $"Ads permitted by cached/current UMP state: {adsCanInitialize}.");
            }
        }

#if UNITY_IOS && !UNITY_EDITOR
        // Called by the native iOS UMP/ATT bridge through UnitySendMessage.
        [UnityEngine.Scripting.Preserve]
        public void OnIosPrivacyStateUpdated(string json)
        {
            StopIosNativeStartWatchdog();
            IosPrivacyStatePayload state = null;
            try
            {
                state = JsonUtility.FromJson<IosPrivacyStatePayload>(json);
            }
            catch (Exception exception)
            {
                LogIosPrivacyWarning(
                    $"[Privacy] iOS privacy bridge returned an unreadable state: {exception.Message}");
            }

            if (state != null)
            {
                iosLastConsentStatus = state.consentStatus;
                iosLastPrivacyOptionsStatus = state.privacyOptionsRequirementStatus;
                iosLastAttStatus = state.attAuthorizationStatus;
                iosLastPrivacyErrorCode = state.errorCode;
                iosLastPrivacyErrorMessage = state.errorCode == 0
                    ? "None"
                    : SanitizeIosPrivacyMessage(state.errorMessage);
                LogIosPrivacyDiagnostic(
                    $"iOS UMP status={state.consentStatus}, "
                    + $"CanRequestAds={state.canRequestAds}, "
                    + $"PrivacyOptionsStatus={state.privacyOptionsRequirementStatus}, "
                    + $"PrivacyOptionsRequired={state.privacyOptionsRequired}, "
                    + $"ATT={state.attAuthorizationStatus}.");
            }

            if (state == null || !state.flowCompleted)
            {
                consentFlowCompleted = false;
                consentFlowStarted = false;
                adsCanInitialize = false;
                privacyOptionsRequired = state != null && state.privacyOptionsRequired;
                AdManager.Instance?.ApplyPrivacyEligibility(false);
                LogIosPrivacyWarning(
                    state == null
                        ? "iOS privacy flow returned no readable state; ads remain unavailable."
                        : $"iOS privacy flow is incomplete ({state.errorCode}: "
                            + $"{state.errorMessage}); ads remain unavailable.");
                ScheduleIosConsentRetry();
                return;
            }

            StopIosConsentRetry();
            iosConsentRetryCount = 0;
            consentFlowCompleted = true;
            adsCanInitialize = state.canRequestAds;
            privacyOptionsRequired = state.privacyOptionsRequired;

            if (state.privacyOptionsAction == 1)
            {
                LogIosPrivacyDiagnostic(
                    "Google UMP reports that Privacy Options are not required/available "
                    + "for this user; no form was presented.");
            }
            else if (state.privacyOptionsAction == 2)
            {
                LogIosPrivacyDiagnostic("Google UMP Privacy Options form completed.");
            }

            // Google UMP writes the full TCF and Additional Consent state that LevelPlay 7.7+
            // consumes directly. Do not collapse canRequestAds into SetGDPRConsent(bool):
            // canRequestAds may also represent valid limited/contextual ad serving.
            AdManager.Instance?.ApplyPrivacyEligibility(adsCanInitialize);

            if (state.errorCode != 0)
            {
                LogIosPrivacyWarning(
                    $"iOS privacy flow completed with error {state.errorCode}: {state.errorMessage}. "
                    + $"ATT status: {state.attAuthorizationStatus}. Ads permitted: {adsCanInitialize}.");
            }
        }

        // Called by the native bridge at each lifecycle boundary. Besides making
        // TestFlight diagnostics actionable, the first acknowledgement proves
        // that the exported bridge function was entered successfully.
        [UnityEngine.Scripting.Preserve]
        public void OnIosPrivacyStageUpdated(string stage)
        {
            StopIosNativeStartWatchdog();
            iosLastPrivacyStage = SanitizeIosPrivacyMessage(stage);
            LogIosPrivacyDiagnostic("iOS privacy stage: " + iosLastPrivacyStage + ".");
        }

        public string GetIosPrivacyDiagnosticText()
        {
            return "Privacy flow completed: " + consentFlowCompleted
                + "\nPrivacy stage: " + iosLastPrivacyStage
                + "\nUMP/Options/ATT: " + iosLastConsentStatus
                + "/" + iosLastPrivacyOptionsStatus
                + "/" + iosLastAttStatus
                + " Error: " + iosLastPrivacyErrorCode
                + " " + iosLastPrivacyErrorMessage;
        }

        private void StartIosNativeStartWatchdog()
        {
            StopIosNativeStartWatchdog();
            iosNativeStartWatchdogRoutine = StartCoroutine(WatchForIosNativeStart());
        }

        private IEnumerator WatchForIosNativeStart()
        {
            yield return new WaitForSecondsRealtime(IosNativeStartTimeoutSeconds);
            iosNativeStartWatchdogRoutine = null;
            consentFlowStarted = false;
            adsCanInitialize = false;
            AdManager.Instance?.ApplyPrivacyEligibility(false);
            iosLastPrivacyStage = "Native bridge did not acknowledge start";
            LogIosPrivacyWarning(
                "iOS privacy bridge did not acknowledge startup; ads remain unavailable safely.");
            ScheduleIosConsentRetry();
        }

        private void StopIosNativeStartWatchdog()
        {
            if (iosNativeStartWatchdogRoutine == null)
            {
                return;
            }

            StopCoroutine(iosNativeStartWatchdogRoutine);
            iosNativeStartWatchdogRoutine = null;
        }

        private void ScheduleIosConsentRetry()
        {
            if (iosConsentRetryRoutine != null || iosConsentRetryCount >= IosConsentRetryLimit)
            {
                if (iosConsentRetryCount >= IosConsentRetryLimit)
                {
                    LogIosPrivacyWarning("iOS UMP retry limit reached; ads remain unavailable safely.");
                }

                return;
            }

            iosConsentRetryCount++;
            LogIosPrivacyDiagnostic(
                $"Scheduling iOS UMP retry {iosConsentRetryCount}/{IosConsentRetryLimit}.");
            iosConsentRetryRoutine = StartCoroutine(RetryIosConsentFlow());
        }

        private IEnumerator RetryIosConsentFlow()
        {
            yield return new WaitForSecondsRealtime(IosConsentRetryDelaySeconds);
            iosConsentRetryRoutine = null;
            BeginConsentFlow();
        }

        private void StopIosConsentRetry()
        {
            if (iosConsentRetryRoutine == null)
            {
                return;
            }

            StopCoroutine(iosConsentRetryRoutine);
            iosConsentRetryRoutine = null;
        }

        private static string SanitizeIosPrivacyMessage(string message)
        {
            return string.IsNullOrWhiteSpace(message)
                ? "Unknown"
                : message.Replace('\n', ' ').Replace('\r', ' ');
        }

        private static void LogIosPrivacyWarning(string message)
        {
            Debug.LogWarning("[CB-PRIVACY] " + message);
        }

        private static void LogIosPrivacyDiagnostic(string message)
        {
            Debug.Log("[CB-PRIVACY] " + message);
        }
#endif

        private void BeginConsentFlow()
        {
            if (consentFlowStarted)
            {
                return;
            }

            consentFlowStarted = true;
            adsCanInitialize = false;
            AdManager.Instance?.ApplyPrivacyEligibility(false);

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using AndroidJavaClass bridge = new AndroidJavaClass(
                    "com.ardagames.chromablast.privacy.ChromaBlastUmpBridge");
                bridge.CallStatic("requestConsentUpdate", gameObject.name);
            }
            catch (Exception exception)
            {
                consentFlowCompleted = true;
                Debug.LogWarning($"[Privacy] Google UMP could not start; ads remain unavailable: {exception.Message}");
            }
#elif UNITY_IOS && !UNITY_EDITOR
            try
            {
                iosLastPrivacyStage = "Managed native request sent";
                StartIosNativeStartWatchdog();
                RequestIosConsentUpdate(gameObject.name);
            }
            catch (Exception exception)
            {
                StopIosNativeStartWatchdog();
                consentFlowCompleted = false;
                consentFlowStarted = false;
                adsCanInitialize = false;
                AdManager.Instance?.ApplyPrivacyEligibility(false);
                LogIosPrivacyWarning(
                    $"iOS UMP/ATT flow could not start; ads remain unavailable: {exception.Message}");
                ScheduleIosConsentRetry();
            }
#else
            consentFlowCompleted = true;
#endif
        }
    }
}
