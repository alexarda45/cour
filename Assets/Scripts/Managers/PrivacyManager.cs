using System;
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
                LogIosPrivacyDiagnostic("[Privacy] iOS Privacy Options requested from Settings.");
                ShowIosPrivacyOptions(gameObject.name);
            }
            catch (Exception exception)
            {
                LogIosPrivacyWarning(
                    $"[Privacy] iOS UMP privacy options could not open: {exception.Message}");
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
        public void OnIosPrivacyStateUpdated(string json)
        {
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
                LogIosPrivacyDiagnostic(
                    $"[Privacy] iOS UMP status={state.consentStatus}, "
                    + $"CanRequestAds={state.canRequestAds}, "
                    + $"PrivacyOptionsStatus={state.privacyOptionsRequirementStatus}, "
                    + $"PrivacyOptionsRequired={state.privacyOptionsRequired}, "
                    + $"ATT={state.attAuthorizationStatus}.");
            }

            if (state == null || !state.flowCompleted)
            {
                consentFlowCompleted = false;
                adsCanInitialize = false;
                privacyOptionsRequired = state != null && state.privacyOptionsRequired;
                AdManager.Instance?.ApplyPrivacyEligibility(false);
                LogIosPrivacyWarning(
                    state == null
                        ? "[Privacy] iOS privacy flow returned no readable state; ads remain unavailable."
                        : $"[Privacy] iOS privacy flow is incomplete ({state.errorCode}: "
                            + $"{state.errorMessage}); ads remain unavailable.");
                return;
            }

            consentFlowCompleted = true;
            adsCanInitialize = state.canRequestAds;
            privacyOptionsRequired = state.privacyOptionsRequired;

            if (state.privacyOptionsAction == 1)
            {
                LogIosPrivacyDiagnostic(
                    "[Privacy] Google UMP reports that Privacy Options are not required/available "
                    + "for this user; no form was presented.");
            }
            else if (state.privacyOptionsAction == 2)
            {
                LogIosPrivacyDiagnostic("[Privacy] Google UMP Privacy Options form completed.");
            }

            // Google UMP writes the full TCF and Additional Consent state that LevelPlay 7.7+
            // consumes directly. Do not collapse canRequestAds into SetGDPRConsent(bool):
            // canRequestAds may also represent valid limited/contextual ad serving.
            AdManager.Instance?.ApplyPrivacyEligibility(adsCanInitialize);

            if (state.errorCode != 0)
            {
                LogIosPrivacyWarning(
                    $"[Privacy] iOS privacy flow completed with error {state.errorCode}: {state.errorMessage}. "
                    + $"ATT status: {state.attAuthorizationStatus}. Ads permitted: {adsCanInitialize}.");
            }
        }

        private static void LogIosPrivacyWarning(string message)
        {
#if DEVELOPMENT_BUILD
            Debug.LogWarning(message);
#endif
        }

        private static void LogIosPrivacyDiagnostic(string message)
        {
#if DEVELOPMENT_BUILD
            Debug.Log(message);
#endif
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
                RequestIosConsentUpdate(gameObject.name);
            }
            catch (Exception exception)
            {
                consentFlowCompleted = false;
                adsCanInitialize = false;
                AdManager.Instance?.ApplyPrivacyEligibility(false);
                LogIosPrivacyWarning(
                    $"[Privacy] iOS UMP/ATT flow could not start; ads remain unavailable: {exception.Message}");
            }
#else
            consentFlowCompleted = true;
#endif
        }
    }
}
