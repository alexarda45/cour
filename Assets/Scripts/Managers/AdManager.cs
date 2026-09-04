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
        private const long InterstitialCooldownSeconds = 180L;
        private const long RewardedProtectionSeconds = 120L;
        private const int FirstRunProtectionCount = 2;
        private const int MinimumInterstitialRunThreshold = 2;
        private const int MaximumInterstitialRunThreshold = 3;
        private const string InterstitialPlacementName = "between_runs";
        private const string EligibleRunsTotalKey = "ChromaBlast.Ads.EligibleRunsTotal.v1";
        private const string RunsSinceInterstitialKey = "ChromaBlast.Ads.RunsSinceInterstitial.v1";
        private const string NextInterstitialThresholdKey = "ChromaBlast.Ads.NextInterstitialThreshold.v1";
        private const string LastInterstitialUnixKey = "ChromaBlast.Ads.LastInterstitialUnix.v1";
        private const string LastRewardedUnixKey = "ChromaBlast.Ads.LastRewardedUnix.v1";
#if UNITY_IOS && !UNITY_EDITOR
        private const float IosInterstitialCadenceChangeSeconds = 30f * 60f;
        private const int IosEarlyInterstitialRunThreshold = 3;
        private const int IosLateInterstitialRunThreshold = 2;
        private const string IosActivePlaytimeSecondsKey = "ChromaBlast.Ads.IosActivePlaytimeSeconds.v1";
#endif

        public static AdManager Instance { get; private set; }

        [Header("LevelPlay Android Ads")]
        [SerializeField] private string levelPlayAndroidAppKey;
        [SerializeField] private string rewardedAdUnitId;
        [SerializeField] private string interstitialAdUnitId;

        [Header("LevelPlay iOS Ads")]
        [SerializeField] private string levelPlayIosAppKey;
        [SerializeField] private string rewardedIosAdUnitId;
        [SerializeField] private string interstitialIosAdUnitId;

        private LevelPlayRewardedAd rewardedAd;
        private LevelPlayInterstitialAd interstitialAd;
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
        private bool privacyAllowsAds;
        private bool interstitialEventsSubscribed;
        private bool interstitialLoadPending;
        private bool interstitialDisplaying;
        private bool interstitialShowPending;
        private int eligibleCompletedRunsTotal;
        private int completedRunsSinceInterstitial;
        private int nextInterstitialThreshold;
        private long lastSuccessfulInterstitialUnix;
        private long lastSuccessfulRewardedUnix;

#if UNITY_IOS && !UNITY_EDITOR
        private float iosActivePlaytimeSeconds;
        private int iosGameOverRestartPressCount;
        private Action iosRestartAfterInterstitial;
#endif

        public bool IsRewardedConfigured => IsValidConfigurationValue(PlatformAppKey)
            && IsValidConfigurationValue(PlatformRewardedAdUnitId);

        public bool IsInterstitialConfigured => IsValidConfigurationValue(PlatformAppKey)
            && IsValidConfigurationValue(PlatformInterstitialAdUnitId);

        public bool IsRewardedReady
        {
            get
            {
                if (Application.isEditor
                    || !IsProductionAdsPlatform
                    || !privacyAllowsAds
                    || !IsRewardedConfigured
                    || !levelPlayInitialized
                    || rewardedAd == null
                    || rewardedDisplaying
                    || interstitialDisplaying
                    || interstitialShowPending
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
            LoadInterstitialCadence();
        }

        private void Start()
        {
            PrivacyManager.GetOrCreate().ApplyCurrentPrivacyStateToAds(this);
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
            UnsubscribeInterstitialEvents();

            if (rewardedAd != null)
            {
                rewardedAd.DestroyAd();
                rewardedAd = null;
            }

            if (interstitialAd != null)
            {
                interstitialAd.DestroyAd();
                interstitialAd = null;
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
#if UNITY_IOS && !UNITY_EDITOR
            bool rewardedAvailable = IsRewardedReady;
            LogIosAdsDiagnostic($"Privacy CanRequestAds = {privacyAllowsAds}");
            LogIosAdsDiagnostic($"Rewarded available = {rewardedAvailable}");
            if (onRewardCompleted == null
                || !privacyAllowsAds
                || !rewardedAvailable
                || interstitialDisplaying
                || interstitialShowPending
                || pendingReward != null)
            {
#if UNITY_IOS && !UNITY_EDITOR
                LogIosAdsDiagnostic(
                    $"Rewarded show unavailable: "
                    + $"privacy={privacyAllowsAds}, configured={IsRewardedConfigured}, "
                    + $"initialized={levelPlayInitialized}, loaded={rewardedAd != null && rewardedAd.IsAdReady()}.");
#endif
                return false;
            }
#else
            if (onRewardCompleted == null
                || !privacyAllowsAds
                || !IsRewardedReady
                || interstitialDisplaying
                || interstitialShowPending
                || pendingReward != null)
            {
                return false;
            }
#endif

            CancelClosedRewardGracePeriod();
            pendingReward = onRewardCompleted;
            pendingRewardFailure = onUnavailableOrFailed;
            rewardDeliveredForCurrentShow = false;
            rewardedDisplaying = true;
            AnalyticsManager.Instance?.RecordRewardedRequested(placementReason);

            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                LogIosAdsDiagnostic("Rewarded show requested");
#endif
                rewardedAd.ShowAd(placementReason);
                return true;
            }
            catch (Exception exception)
            {
                rewardedDisplaying = false;
                pendingReward = null;
                Action failure = pendingRewardFailure;
                pendingRewardFailure = null;
#if UNITY_IOS && !UNITY_EDITOR
                string safeError = FormatIosAdsError("Show exception", exception.Message);
                LogIosAdsDiagnostic($"Rewarded show failed: {safeError}");
                Debug.LogWarning($"LevelPlay rewarded ad could not be shown: {safeError}");
#else
                Debug.LogWarning($"LevelPlay rewarded ad could not be shown: {exception.Message}");
#endif
                ScheduleRewardedLoadRetry();
                failure?.Invoke();
                return true;
            }
        }

        public void PrepareRewarded()
        {
            if (Application.isEditor || !IsProductionAdsPlatform)
            {
                return;
            }

            if (!privacyAllowsAds)
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

        public void RecordEligibleRunCompleted(GameMode mode)
        {
            if (mode != GameMode.Classic && mode != GameMode.Blitz)
            {
                return;
            }

            eligibleCompletedRunsTotal = Mathf.Max(0, eligibleCompletedRunsTotal) + 1;

#if UNITY_IOS && !UNITY_EDITOR
            completedRunsSinceInterstitial = Mathf.Max(0, completedRunsSinceInterstitial) + 1;
            SaveInterstitialCadence();
            return;
#else
            if (eligibleCompletedRunsTotal <= FirstRunProtectionCount)
            {
                completedRunsSinceInterstitial = 0;
                if (eligibleCompletedRunsTotal == FirstRunProtectionCount)
                {
                    nextInterstitialThreshold = ChooseNextInterstitialThreshold();
                }

                SaveInterstitialCadence();
                return;
            }

            completedRunsSinceInterstitial = Mathf.Max(0, completedRunsSinceInterstitial) + 1;
            EnsureValidInterstitialThreshold();
            SaveInterstitialCadence();
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        public void RecordActiveGameplayTime(float unscaledDeltaTime)
        {
            if (unscaledDeltaTime > 0f && unscaledDeltaTime < 1f)
            {
                iosActivePlaytimeSeconds = Mathf.Max(0f, iosActivePlaytimeSeconds) + unscaledDeltaTime;
            }
        }
#endif

        public bool TryShowInterstitialAfterGameOverPresentation()
        {
            return TryShowInterstitialBetweenRuns();
        }

        public bool TryHandleIosGameOverRestart(Action restartAfterInterstitial)
        {
#if UNITY_IOS && !UNITY_EDITOR
            iosGameOverRestartPressCount++;

            // If the existing post-Game-Over path already started an ad, attach
            // this restart to that presentation instead of starting a second one.
            if (interstitialShowPending || interstitialDisplaying)
            {
                iosRestartAfterInterstitial ??= restartAfterInterstitial;
                return true;
            }

            if ((iosGameOverRestartPressCount & 1) != 0 || !CanAttemptInterstitialNow())
            {
                EnsureInterstitialCreatedAndLoaded();
                return false;
            }

            iosRestartAfterInterstitial = restartAfterInterstitial;
            interstitialShowPending = true;
            try
            {
                interstitialAd.ShowAd(InterstitialPlacementName);
                return true;
            }
            catch (Exception)
            {
                interstitialShowPending = false;
                iosRestartAfterInterstitial = null;
                LoadInterstitialAd();
                return false;
            }
#else
            return false;
#endif
        }

        public void ApplyPrivacyEligibility(bool allowAds)
        {
            privacyAllowsAds = allowAds;
#if UNITY_IOS && !UNITY_EDITOR
            LogIosAdsDiagnostic($"Privacy CanRequestAds = {allowAds}");
#endif

            if (!allowAds)
            {
                CancelInitializationRetry();
                CancelRewardedLoadRetry();
                interstitialShowPending = false;

                if (!rewardedDisplaying)
                {
                    pendingReward = null;
                    Action failure = pendingRewardFailure;
                    pendingRewardFailure = null;
                    failure?.Invoke();
                }

                return;
            }

            if (levelPlayInitialized)
            {
                LoadRewardedAd();
                EnsureInterstitialCreatedAndLoaded();
            }
            else
            {
                InitializeAds();
            }
        }

        private void InitializeAds()
        {
            if (initializationStarted
                || levelPlayInitialized
                || initializationRetryRoutine != null
                || !privacyAllowsAds
                || !IsPlatformInitializationConfigured
                || Application.isEditor
                || !IsProductionAdsPlatform)
            {
                return;
            }

            initializationStarted = true;
            SubscribeInitializationEvents();

            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                LogIosAdsDiagnostic("LevelPlay init requested");
#endif
                LevelPlay.Init(PlatformAppKey.Trim());
            }
            catch (Exception exception)
            {
                initializationStarted = false;
                ScheduleInitializationRetry();

#if UNITY_IOS && !UNITY_EDITOR
                LogIosAdsDiagnostic($"LevelPlay init exception: {FormatIosAdsError("Init exception", exception.Message)}");
#endif

                if (!initializationFailureLogged)
                {
                    initializationFailureLogged = true;
#if UNITY_IOS && !UNITY_EDITOR
                    Debug.LogWarning(
                        $"LevelPlay initialization could not start; retrying after a delay. "
                        + FormatIosAdsError("Init exception", exception.Message));
#else
                    Debug.LogWarning($"LevelPlay initialization could not start; retrying after a delay. {exception.Message}");
#endif
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
#if UNITY_IOS && !UNITY_EDITOR
            LogIosAdsDiagnostic("LevelPlay init success");
#endif

            if (rewardedAd == null)
            {
                rewardedAd = new LevelPlayRewardedAd(PlatformRewardedAdUnitId.Trim());
                SubscribeRewardedEvents();
#if UNITY_IOS && !UNITY_EDITOR
                LogIosAdsDiagnostic("iOS rewarded configured");
#endif
            }

            LoadRewardedAd();
            EnsureInterstitialCreatedAndLoaded();
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
#if UNITY_IOS && !UNITY_EDITOR
            string safeError = FormatIosAdsError("Init", error.ErrorCode, error.ErrorMessage);
            LogIosAdsDiagnostic($"LevelPlay init failure: {safeError}");
#endif

            if (!initializationFailureLogged)
            {
                initializationFailureLogged = true;
#if UNITY_IOS && !UNITY_EDITOR
                Debug.LogWarning($"LevelPlay initialization failed; retrying after a controlled delay. {safeError}");
#else
                Debug.LogWarning($"LevelPlay initialization failed; retrying after a controlled delay. {error}");
#endif
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
                || !privacyAllowsAds
                || rewardedAd == null
                || rewardedLoadPending
                || rewardedDisplaying
                || rewardedAd.IsAdReady())
            {
                return;
            }

            rewardedLoadPending = true;
#if UNITY_IOS && !UNITY_EDITOR
            LogIosAdsDiagnostic("Rewarded load requested");
#endif
            rewardedAd.LoadAd();
        }

        private void OnRewardedAdLoaded(LevelPlayAdInfo adInfo)
        {
            rewardedLoadPending = false;
            loadFailureLogged = false;
            CancelRewardedLoadRetry();
#if UNITY_IOS && !UNITY_EDITOR
            LogIosAdsDiagnostic("Rewarded loaded");
#endif
        }

        private void OnRewardedAdLoadFailed(LevelPlayAdError error)
        {
            rewardedLoadPending = false;
#if UNITY_IOS && !UNITY_EDITOR
            string safeError = FormatIosAdsError("Load", error.ErrorCode, error.ErrorMessage);
            LogIosAdsDiagnostic($"Rewarded load failed: {safeError}");
#endif

            if (!loadFailureLogged)
            {
                loadFailureLogged = true;
#if UNITY_IOS && !UNITY_EDITOR
                Debug.LogWarning($"LevelPlay rewarded ad load failed; retrying after a delay. {safeError}");
#else
                Debug.LogWarning($"LevelPlay rewarded ad load failed; retrying after a delay. {error}");
#endif
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
#if UNITY_IOS && !UNITY_EDITOR
            string safeError = FormatIosAdsError("Show", error.ErrorCode, error.ErrorMessage);
            LogIosAdsDiagnostic($"Rewarded show failed: {safeError}");
#endif
#if UNITY_IOS && !UNITY_EDITOR
            Debug.LogWarning($"LevelPlay rewarded ad display failed; no reward was granted. {safeError}");
#else
            Debug.LogWarning($"LevelPlay rewarded ad display failed; no reward was granted. {error}");
#endif
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
            lastSuccessfulRewardedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            PlayerPrefs.SetString(LastRewardedUnixKey, lastSuccessfulRewardedUnix.ToString());
            PlayerPrefs.Save();
            CancelClosedRewardGracePeriod();

            Action rewardCompleted = pendingReward;
            pendingReward = null;
            pendingRewardFailure = null;
#if UNITY_IOS && !UNITY_EDITOR
            LogIosAdsDiagnostic("Reward granted");
#endif
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

        private void EnsureInterstitialCreatedAndLoaded()
        {
            if (!privacyAllowsAds
                || !levelPlayInitialized
                || !IsInterstitialConfigured
                || InterstitialsRemoved())
            {
                return;
            }

            if (interstitialAd == null)
            {
                interstitialAd = new LevelPlayInterstitialAd(PlatformInterstitialAdUnitId.Trim());
                SubscribeInterstitialEvents();
            }

            LoadInterstitialAd();
        }

        private void SubscribeInterstitialEvents()
        {
            if (interstitialAd == null || interstitialEventsSubscribed)
            {
                return;
            }

            interstitialAd.OnAdLoaded += OnInterstitialAdLoaded;
            interstitialAd.OnAdLoadFailed += OnInterstitialAdLoadFailed;
            interstitialAd.OnAdDisplayed += OnInterstitialAdDisplayed;
            interstitialAd.OnAdDisplayFailed += OnInterstitialAdDisplayFailed;
            interstitialAd.OnAdClosed += OnInterstitialAdClosed;
            interstitialEventsSubscribed = true;
        }

        private void UnsubscribeInterstitialEvents()
        {
            if (interstitialAd == null || !interstitialEventsSubscribed)
            {
                return;
            }

            interstitialAd.OnAdLoaded -= OnInterstitialAdLoaded;
            interstitialAd.OnAdLoadFailed -= OnInterstitialAdLoadFailed;
            interstitialAd.OnAdDisplayed -= OnInterstitialAdDisplayed;
            interstitialAd.OnAdDisplayFailed -= OnInterstitialAdDisplayFailed;
            interstitialAd.OnAdClosed -= OnInterstitialAdClosed;
            interstitialEventsSubscribed = false;
        }

        private void LoadInterstitialAd()
        {
            if (!privacyAllowsAds
                || !levelPlayInitialized
                || interstitialAd == null
                || interstitialLoadPending
                || interstitialDisplaying
                || interstitialShowPending
                || interstitialAd.IsAdReady()
                || InterstitialsRemoved())
            {
                return;
            }

            interstitialLoadPending = true;
            try
            {
                interstitialAd.LoadAd();
            }
            catch (Exception)
            {
                interstitialLoadPending = false;
            }
        }

        private bool TryShowInterstitialBetweenRuns()
        {
#if UNITY_IOS && !UNITY_EDITOR
            int cadenceThreshold = GetIosInterstitialRunThreshold();
            if (completedRunsSinceInterstitial < cadenceThreshold)
            {
                EnsureInterstitialCreatedAndLoaded();
                return false;
            }

            // Consume this natural transition even when inventory is unavailable or
            // another safety gate blocks the ad. This prevents aggressive catch-up.
            completedRunsSinceInterstitial = 0;
            SaveInterstitialCadence();
#endif
            if (!CanAttemptInterstitialNow())
            {
                EnsureInterstitialCreatedAndLoaded();
                return false;
            }

            interstitialShowPending = true;
            try
            {
                interstitialAd.ShowAd(InterstitialPlacementName);
                return true;
            }
            catch (Exception)
            {
                interstitialShowPending = false;
                LoadInterstitialAd();
                return false;
            }
        }

        private bool CanAttemptInterstitialNow()
        {
            if (!privacyAllowsAds
                || !levelPlayInitialized
                || !IsInterstitialConfigured
                || interstitialAd == null
                || interstitialLoadPending
                || interstitialDisplaying
                || interstitialShowPending
                || rewardedDisplaying
                || pendingReward != null
                || InterstitialsRemoved()
                || !interstitialAd.IsAdReady()
#if !(UNITY_IOS && !UNITY_EDITOR)
                || eligibleCompletedRunsTotal <= FirstRunProtectionCount
                || completedRunsSinceInterstitial < nextInterstitialThreshold
#endif
                )
            {
                return false;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (lastSuccessfulInterstitialUnix > 0L
                && now - lastSuccessfulInterstitialUnix < InterstitialCooldownSeconds)
            {
                return false;
            }

            return lastSuccessfulRewardedUnix <= 0L
                || now - lastSuccessfulRewardedUnix >= RewardedProtectionSeconds;
        }

        private void OnInterstitialAdLoaded(LevelPlayAdInfo adInfo)
        {
            interstitialLoadPending = false;
        }

        private void OnInterstitialAdLoadFailed(LevelPlayAdError error)
        {
            // Do not loop. The next eligible between-run attempt may request one new load.
            interstitialLoadPending = false;
        }

        private void OnInterstitialAdDisplayed(LevelPlayAdInfo adInfo)
        {
            interstitialShowPending = false;
            interstitialDisplaying = true;
            completedRunsSinceInterstitial = 0;
#if !(UNITY_IOS && !UNITY_EDITOR)
            nextInterstitialThreshold = ChooseNextInterstitialThreshold();
#endif
            lastSuccessfulInterstitialUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            SaveInterstitialCadence();
        }

        private void OnInterstitialAdDisplayFailed(LevelPlayAdInfo adInfo, LevelPlayAdError error)
        {
            interstitialShowPending = false;
            interstitialDisplaying = false;
#if UNITY_IOS && !UNITY_EDITOR
            CompleteIosRestartAfterInterstitial();
#endif
            LoadInterstitialAd();
        }

        private void OnInterstitialAdClosed(LevelPlayAdInfo adInfo)
        {
            interstitialShowPending = false;
            interstitialDisplaying = false;
#if UNITY_IOS && !UNITY_EDITOR
            CompleteIosRestartAfterInterstitial();
#endif
            LoadInterstitialAd();
        }

#if UNITY_IOS && !UNITY_EDITOR
        private void CompleteIosRestartAfterInterstitial()
        {
            Action continuation = iosRestartAfterInterstitial;
            iosRestartAfterInterstitial = null;
            continuation?.Invoke();
        }
#endif

        private void LoadInterstitialCadence()
        {
            eligibleCompletedRunsTotal = Mathf.Max(0, PlayerPrefs.GetInt(EligibleRunsTotalKey, 0));
            completedRunsSinceInterstitial = Mathf.Max(0, PlayerPrefs.GetInt(RunsSinceInterstitialKey, 0));
            nextInterstitialThreshold = PlayerPrefs.GetInt(NextInterstitialThresholdKey, 0);
            lastSuccessfulInterstitialUnix = ReadUnixTimestamp(LastInterstitialUnixKey);
            lastSuccessfulRewardedUnix = ReadUnixTimestamp(LastRewardedUnixKey);
#if UNITY_IOS && !UNITY_EDITOR
            iosActivePlaytimeSeconds = Mathf.Max(0f, PlayerPrefs.GetFloat(IosActivePlaytimeSecondsKey, 0f));
#endif

#if !(UNITY_IOS && !UNITY_EDITOR)
            if (eligibleCompletedRunsTotal >= FirstRunProtectionCount)
            {
                EnsureValidInterstitialThreshold();
                SaveInterstitialCadence();
            }
#endif
        }

        private void SaveInterstitialCadence()
        {
            PlayerPrefs.SetInt(EligibleRunsTotalKey, eligibleCompletedRunsTotal);
            PlayerPrefs.SetInt(RunsSinceInterstitialKey, completedRunsSinceInterstitial);
            PlayerPrefs.SetInt(NextInterstitialThresholdKey, nextInterstitialThreshold);
            PlayerPrefs.SetString(LastInterstitialUnixKey, lastSuccessfulInterstitialUnix.ToString());
#if UNITY_IOS && !UNITY_EDITOR
            PlayerPrefs.SetFloat(IosActivePlaytimeSecondsKey, iosActivePlaytimeSeconds);
#endif
            PlayerPrefs.Save();
        }

#if UNITY_IOS && !UNITY_EDITOR
        private int GetIosInterstitialRunThreshold()
        {
            return iosActivePlaytimeSeconds < IosInterstitialCadenceChangeSeconds
                ? IosEarlyInterstitialRunThreshold
                : IosLateInterstitialRunThreshold;
        }
#endif

        private void EnsureValidInterstitialThreshold()
        {
            if (nextInterstitialThreshold < MinimumInterstitialRunThreshold
                || nextInterstitialThreshold > MaximumInterstitialRunThreshold)
            {
                nextInterstitialThreshold = ChooseNextInterstitialThreshold();
            }
        }

        private static int ChooseNextInterstitialThreshold()
        {
            return UnityEngine.Random.Range(
                MinimumInterstitialRunThreshold,
                MaximumInterstitialRunThreshold + 1);
        }

        private static long ReadUnixTimestamp(string key)
        {
            return long.TryParse(PlayerPrefs.GetString(key, "0"), out long value)
                ? Math.Max(0L, value)
                : 0L;
        }

        private static bool InterstitialsRemoved()
        {
            return SaveManager.Instance != null && SaveManager.Instance.Data.removeAds;
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
                || !privacyAllowsAds
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
            if (!privacyAllowsAds || levelPlayInitialized || initializationRetryRoutine != null)
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

        private static void LogIosAdsDiagnostic(string message)
        {
#if UNITY_IOS && !UNITY_EDITOR
            Debug.Log($"[CB-ADS] {message}");
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        private string FormatIosAdsError(string stage, int errorCode, string message)
        {
            return stage + " " + errorCode + ": " + SanitizeIosLogMessage(message);
        }

        private string FormatIosAdsError(string stage, string message)
        {
            return stage + ": " + SanitizeIosLogMessage(message);
        }

        private string SanitizeIosLogMessage(string message)
        {
            string sanitized = string.IsNullOrWhiteSpace(message)
                ? "Unknown error"
                : message.Replace('\r', ' ').Replace('\n', ' ').Trim();

            RedactIosLogConfiguration(ref sanitized, PlatformAppKey);
            RedactIosLogConfiguration(ref sanitized, PlatformRewardedAdUnitId);
            RedactIosLogConfiguration(ref sanitized, PlatformInterstitialAdUnitId);

            const int maximumLength = 120;
            return sanitized.Length <= maximumLength
                ? sanitized
                : sanitized.Substring(0, maximumLength - 3) + "...";
        }

        private static void RedactIosLogConfiguration(ref string message, string configuredValue)
        {
            if (!string.IsNullOrEmpty(configuredValue))
            {
                message = message.Replace(configuredValue.Trim(), "[redacted]");
            }
        }
#endif

        private void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
#if UNITY_IOS && !UNITY_EDITOR
            if (paused)
            {
                SaveInterstitialCadence();
            }
#endif

            if (!paused
                && privacyAllowsAds
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

        private static bool IsProductionAdsPlatform
        {
            get
            {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        private string PlatformAppKey
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return levelPlayAndroidAppKey;
#elif UNITY_IOS && !UNITY_EDITOR
                return levelPlayIosAppKey;
#else
                return null;
#endif
            }
        }

        private string PlatformRewardedAdUnitId
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return rewardedAdUnitId;
#elif UNITY_IOS && !UNITY_EDITOR
                return rewardedIosAdUnitId;
#else
                return null;
#endif
            }
        }

        private string PlatformInterstitialAdUnitId
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return interstitialAdUnitId;
#elif UNITY_IOS && !UNITY_EDITOR
                return interstitialIosAdUnitId;
#else
                return null;
#endif
            }
        }

        private bool IsPlatformInitializationConfigured
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return IsRewardedConfigured && IsInterstitialConfigured;
#elif UNITY_ANDROID && !UNITY_EDITOR
                // Preserve the established Android initialization gate exactly:
                // Android can initialize when the app key and rewarded unit are valid.
                return IsRewardedConfigured;
#else
                return false;
#endif
            }
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
