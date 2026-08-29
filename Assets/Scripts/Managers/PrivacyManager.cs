using System;
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
            Debug.LogWarning(
                "[Privacy] iOS privacy options are not available until the native CMP/ATT bridge is configured.");
#else
            Debug.Log("[Privacy] Google UMP privacy options are available only on an Android device build.");
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
        // Future native iOS CMP/ATT bridge entry point. Phase 1 deliberately leaves
        // ads gated unless the bridge supplies an explicit completed, eligible state.
        public void OnIosPrivacyStateUpdated(string json)
        {
            IosPrivacyStatePayload state = null;
            try
            {
                state = JsonUtility.FromJson<IosPrivacyStatePayload>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Privacy] iOS privacy bridge returned an unreadable state: {exception.Message}");
            }

            if (state == null || !state.flowCompleted)
            {
                adsCanInitialize = false;
                AdManager.Instance?.ApplyPrivacyEligibility(false);
                Debug.LogWarning("[Privacy] iOS privacy flow is incomplete; ads remain unavailable.");
                return;
            }

            consentFlowCompleted = true;
            adsCanInitialize = state.canRequestAds;
            privacyOptionsRequired = state.privacyOptionsRequired;
            AdManager.Instance?.ApplyPrivacyEligibility(adsCanInitialize);

            if (state.errorCode != 0)
            {
                Debug.LogWarning(
                    $"[Privacy] iOS privacy flow completed with error {state.errorCode}: {state.errorMessage}. "
                    + $"ATT status: {state.attAuthorizationStatus}. Ads permitted: {adsCanInitialize}.");
            }
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
            // The native iOS CMP/ATT bridge is intentionally deferred. Keep the flow
            // incomplete and LevelPlay gated rather than inventing a consent result.
            Debug.LogWarning(
                "[Privacy] iOS CMP/ATT bridge is not configured; ads remain unavailable.");
#else
            consentFlowCompleted = true;
#endif
        }
    }
}
