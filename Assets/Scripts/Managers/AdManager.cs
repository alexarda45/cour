using System;
using System.Collections;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace ChromaBlast
{
    public class AdManager : MonoBehaviour
    {
        private const float InitializationRetryInitialDelaySeconds = 5f;
        private const float InitializationRetryMaxDelaySeconds = 60f;
        private const float RewardedLoadRetryDelaySeconds = 15f;
        private const float ClosedRewardCallbackGraceSeconds = 5f;

        public static AdManager Instance { get; private set; }

        [Header("LevelPlay Rewarded Ads")]
        [SerializeField] private string levelPlayAndroidAppKey;
        [SerializeField] private string rewardedAdUnitId;

        private LevelPlayRewardedAd rewardedAd;
        private Action pendingReward;
        private Action pendingRewardFailure;
        private Coroutine initializationRetryRoutine;
        private Coroutine rewardedLoadRetryRoutine;
        private Coroutine closedRewardGraceRoutine;
        private int initializationRetryCount;
        private bool initializationStarted;
        private bool levelPlayInitialized;
        private bool initializationEventsSubscribed;
        private bool applicationPaused;
        private bool rewardedEventsSubscribed;
        private bool rewardedLoadPending;
        private bool rewardedDisplaying;
        private bool rewardDeliveredForCurrentShow;
        private bool initializationFailureLogged;
        private bool loadFailureLogged;

        public bool IsRewardedConfigured => IsValidConfigurationValue(levelPlayAndroidAppKey)
            && IsValidConfigurationValue(rewardedAdUnitId);

        public bool IsRewardedReady
        {
            get
            {
                if (Application.isEditor
                    || Application.platform != RuntimePlatform.Android
                    || !IsRewardedConfigured
                    || !levelPlayInitialized
                    || rewardedAd == null
                    || rewardedDisplaying
                    || pendingReward != null)
                {
                    return false;
                }

                return rewardedAd.IsAdReady();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            InitializeAds();
        }

        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            CancelInitializationRetry();
            UnsubscribeInitializationEvents();
            UnsubscribeRewardedEvents();

            if (rewardedAd != null)
            {
                rewardedAd.DestroyAd();
                rewardedAd = null;
            }

            pendingReward = null;
            pendingRewardFailure = null;
            Instance = null;
        }

        public void ShowRewarded(string placementReason, Action onRewardCompleted)
        {
            TryShowRewarded(placementReason, onRewardCompleted, null);
        }

        public bool TryShowRewarded(
            string placementReason,
            Action onRewardCompleted,
            Action onUnavailableOrFailed)
        {
            if (onRewardCompleted == null
                || !IsRewardedReady
                || pendingReward != null)
            {
                return false;
            }

            CancelClosedRewardGracePeriod();
            pendingReward = onRewardCompleted;
            pendingRewardFailure = onUnavailableOrFailed;
            rewardDeliveredForCurrentShow = false;
            rewardedDisplaying = true;
            AnalyticsManager.Instance?.RecordRewardedRequested(placementReason);

            try
            {
                rewardedAd.ShowAd(placementReason);
                return true;
            }
            catch (Exception exception)
            {
                rewardedDisplaying = false;
                pendingReward = null;
                Action failure = pendingRewardFailure;
                pendingRewardFailure = null;
                Debug.LogWarning($"LevelPlay rewarded ad could not be shown: {exception.Message}");
                ScheduleRewardedLoadRetry();
                failure?.Invoke();
                return true;
            }
        }

        public void PrepareRewarded()
        {
            if (Application.isEditor || Application.platform != RuntimePlatform.Android)
            {
                return;
            }

            if (!initializationStarted)
            {
                if (initializationRetryRoutine == null)
                {
                    InitializeAds();
                }

                return;
            }

            if (!levelPlayInitialized
                || rewardedAd == null
                || rewardedLoadPending
                || rewardedLoadRetryRoutine != null
                || rewardedDisplaying
                || rewardedAd.IsAdReady())
            {
                return;
            }

            LoadRewardedAd();
        }

        public bool ShowInterstitial()
        {
            if (SaveManager.Instance != null && SaveManager.Instance.Data.removeAds)
            {
                return false;
            }

            // Interstitial ads are intentionally unavailable in this LevelPlay rewarded-only adapter.
            return false;
        }

        private void InitializeAds()
        {
            if (initializationStarted
                || levelPlayInitialized
                || initializationRetryRoutine != null
                || !IsRewardedConfigured
                || Application.isEditor
                || Application.platform != RuntimePlatform.Android)
            {
                return;
            }

            initializationStarted = true;
            SubscribeInitializationEvents();

            try
            {
                LevelPlay.Init(levelPlayAndroidAppKey.Trim());
            }
            catch (Exception exception)
            {
                initializationStarted = false;
                ScheduleInitializationRetry();

                if (!initializationFailureLogged)
                {
                    initializationFailureLogged = true;
                    Debug.LogWarning($"LevelPlay initialization could not start; retrying after a delay. {exception.Message}");
                }
            }
        }

        private void OnLevelPlayInitSuccess(LevelPlayConfiguration configuration)
        {
            CancelInitializationRetry();
            initializationRetryCount = 0;
            initializationFailureLogged = false;

            if (levelPlayInitialized)
            {
                return;
            }

            initializationStarted = true;
            levelPlayInitialized = true;
            UnsubscribeInitializationEvents();

            if (rewardedAd == null)
            {
                rewardedAd = new LevelPlayRewardedAd(rewardedAdUnitId.Trim());
                SubscribeRewardedEvents();
            }

            LoadRewardedAd();
        }

        private void OnLevelPlayInitFailed(LevelPlayInitError error)
        {
            if (levelPlayInitialized)
            {
                return;
            }

            initializationStarted = false;
            levelPlayInitialized = false;
            rewardedLoadPending = false;
            rewardedDisplaying = false;
            pendingReward = null;
            Action failure = pendingRewardFailure;
            pendingRewardFailure = null;

            if (!initializationFailureLogged)
            {
                initializationFailureLogged = true;
                Debug.LogWarning($"LevelPlay initialization failed; retrying after a controlled delay. {error}");
            }

            ScheduleInitializationRetry();
            failure?.Invoke();
        }

        private void SubscribeInitializationEvents()
        {
            if (initializationEventsSubscribed)
            {
                return;
            }

            LevelPlay.OnInitSuccess += OnLevelPlayInitSuccess;
            LevelPlay.OnInitFailed += OnLevelPlayInitFailed;
            initializationEventsSubscribed = true;
        }

        private void UnsubscribeInitializationEvents()
        {
            if (!initializationEventsSubscribed)
            {
                return;
            }

            LevelPlay.OnInitSuccess -= OnLevelPlayInitSuccess;
            LevelPlay.OnInitFailed -= OnLevelPlayInitFailed;
            initializationEventsSubscribed = false;
        }

        private void SubscribeRewardedEvents()
        {
            if (rewardedAd == null || rewardedEventsSubscribed)
            {
                return;
            }

            rewardedAd.OnAdLoaded += OnRewardedAdLoaded;
            rewardedAd.OnAdLoadFailed += OnRewardedAdLoadFailed;
            rewardedAd.OnAdDisplayed += OnRewardedAdDisplayed;
            rewardedAd.OnAdDisplayFailed += OnRewardedAdDisplayFailed;
            rewardedAd.OnAdRewarded += OnRewardedAdRewarded;
            rewardedAd.OnAdClosed += OnRewardedAdClosed;
            rewardedEventsSubscribed = true;
        }

        private void UnsubscribeRewardedEvents()
        {
            if (rewardedAd == null || !rewardedEventsSubscribed)
            {
                return;
            }

            rewardedAd.OnAdLoaded -= OnRewardedAdLoaded;
            rewardedAd.OnAdLoadFailed -= OnRewardedAdLoadFailed;
            rewardedAd.OnAdDisplayed -= OnRewardedAdDisplayed;
            rewardedAd.OnAdDisplayFailed -= OnRewardedAdDisplayFailed;
            rewardedAd.OnAdRewarded -= OnRewardedAdRewarded;
            rewardedAd.OnAdClosed -= OnRewardedAdClosed;
            rewardedEventsSubscribed = false;
        }

        private void LoadRewardedAd()
        {
            if (!levelPlayInitialized
                || rewardedAd == null
                || rewardedLoadPending
                || rewardedDisplaying
                || rewardedAd.IsAdReady())
            {
                return;
            }

            rewardedLoadPending = true;
            rewardedAd.LoadAd();
        }

        private void OnRewardedAdLoaded(LevelPlayAdInfo adInfo)
        {
            rewardedLoadPending = false;
            loadFailureLogged = false;
            CancelRewardedLoadRetry();
        }

        private void OnRewardedAdLoadFailed(LevelPlayAdError error)
        {
            rewardedLoadPending = false;

            if (!loadFailureLogged)
            {
                loadFailureLogged = true;
                Debug.LogWarning($"LevelPlay rewarded ad load failed; retrying after a delay. {error}");
            }

            ScheduleRewardedLoadRetry();
        }

        private void OnRewardedAdDisplayed(LevelPlayAdInfo adInfo)
        {
            rewardedDisplaying = true;
        }

        private void OnRewardedAdDisplayFailed(LevelPlayAdInfo adInfo, LevelPlayAdError error)
        {
            rewardedDisplaying = false;
            pendingReward = null;
            Action failure = pendingRewardFailure;
            pendingRewardFailure = null;
            rewardDeliveredForCurrentShow = false;
            CancelClosedRewardGracePeriod();
            Debug.LogWarning($"LevelPlay rewarded ad display failed; no reward was granted. {error}");
            ScheduleRewardedLoadRetry();
            failure?.Invoke();
        }

        private void OnRewardedAdRewarded(LevelPlayAdInfo adInfo, LevelPlayReward reward)
        {
            if (rewardDeliveredForCurrentShow || pendingReward == null)
            {
                return;
            }

            rewardDeliveredForCurrentShow = true;
            CancelClosedRewardGracePeriod();

            Action rewardCompleted = pendingReward;
            pendingReward = null;
            pendingRewardFailure = null;
            rewardCompleted.Invoke();
        }

        private void OnRewardedAdClosed(LevelPlayAdInfo adInfo)
        {
            rewardedDisplaying = false;
            rewardedLoadPending = false;
            LoadRewardedAd();

            if (pendingReward != null && !rewardDeliveredForCurrentShow)
            {
                CancelClosedRewardGracePeriod();
                closedRewardGraceRoutine = StartCoroutine(ClearUnconfirmedRewardAfterGracePeriod());
            }
        }

        private IEnumerator ClearUnconfirmedRewardAfterGracePeriod()
        {
            yield return new WaitForSecondsRealtime(ClosedRewardCallbackGraceSeconds);
            pendingReward = null;
            Action failure = pendingRewardFailure;
            pendingRewardFailure = null;
            rewardDeliveredForCurrentShow = false;
            closedRewardGraceRoutine = null;
            failure?.Invoke();
        }

        private void ScheduleRewardedLoadRetry()
        {
            if (!levelPlayInitialized
                || rewardedAd == null
                || rewardedLoadRetryRoutine != null)
            {
                return;
            }

            rewardedLoadRetryRoutine = StartCoroutine(RetryRewardedLoadAfterDelay());
        }

        private IEnumerator RetryRewardedLoadAfterDelay()
        {
            yield return new WaitForSecondsRealtime(RewardedLoadRetryDelaySeconds);
            rewardedLoadRetryRoutine = null;
            LoadRewardedAd();
        }

        private void CancelRewardedLoadRetry()
        {
            if (rewardedLoadRetryRoutine == null)
            {
                return;
            }

            StopCoroutine(rewardedLoadRetryRoutine);
            rewardedLoadRetryRoutine = null;
        }

        private void ScheduleInitializationRetry()
        {
            if (levelPlayInitialized || initializationRetryRoutine != null)
            {
                return;
            }

            int retryExponent = Mathf.Min(initializationRetryCount, 4);
            float retryDelay = Mathf.Min(
                InitializationRetryInitialDelaySeconds * Mathf.Pow(2f, retryExponent),
                InitializationRetryMaxDelaySeconds);

            initializationRetryCount++;
            initializationRetryRoutine = StartCoroutine(RetryInitializationAfterDelay(retryDelay));
        }

        private IEnumerator RetryInitializationAfterDelay(float retryDelay)
        {
            yield return new WaitForSecondsRealtime(retryDelay);

            while (applicationPaused || Application.internetReachability == NetworkReachability.NotReachable)
            {
                yield return new WaitForSecondsRealtime(1f);
            }

            initializationRetryRoutine = null;
            InitializeAds();
        }

        private void CancelInitializationRetry()
        {
            if (initializationRetryRoutine == null)
            {
                return;
            }

            StopCoroutine(initializationRetryRoutine);
            initializationRetryRoutine = null;
        }

        private void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;

            if (!paused
                && !levelPlayInitialized
                && !initializationStarted
                && initializationRetryRoutine == null
                && initializationRetryCount > 0)
            {
                ScheduleInitializationRetry();
            }
        }

        private void CancelClosedRewardGracePeriod()
        {
            if (closedRewardGraceRoutine == null)
            {
                return;
            }

            StopCoroutine(closedRewardGraceRoutine);
            closedRewardGraceRoutine = null;
        }

        private static bool IsValidConfigurationValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = value.Trim().ToUpperInvariant();
            return !normalized.StartsWith("YOUR_", StringComparison.Ordinal)
                && !normalized.StartsWith("ENTER_", StringComparison.Ordinal)
                && !normalized.Contains("PLACEHOLDER")
                && normalized != "APP_KEY"
                && normalized != "AD_UNIT_ID";
        }
    }
}
