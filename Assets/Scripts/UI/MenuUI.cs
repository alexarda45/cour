using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChromaBlast
{
    public class MenuUI : MonoBehaviour
    {
        private const string OceanBackgroundPath = "Ocean/MainMenu/BG_MainMenu_New";
        private const string OceanLogoPath = "Ocean/MainMenu/Logo_ChromaBlast_Menu";
        private const string ClassicButtonPath = "Ocean/MainMenu/Buttons/Button_Classic";
        private const string BlitzButtonPath = "Ocean/MainMenu/Buttons/Button_Blitz";
        private const string DailyButtonPath = "Ocean/MainMenu/Buttons/daily_transparent_same_size";
        private const string RewardsButtonPath = "Ocean/MainMenu/Buttons/Button_Rewards";
        private const string ThemeButtonPath = "Ocean/MainMenu/Buttons/Button_Theme";
        private const string ShopButtonPath = "Ocean/MainMenu/Buttons/Button_Shop";
        private const string ThemesBuyButtonPath = "Themes/UI/BuyButton";
        private const string ThemesApplyButtonPath = "Themes/UI/ApplyThemeButton_Final";
        private const string ThemesCoinBalancePath = "Themes/UI/ThemesCoinBalance";
        private const string OceanThemesCloseButtonPath = "Themes/Artwork/Ocean/ThemesCloseButton";
        private const string OceanCardNormalPath = "Themes/Artwork/Ocean/Card_Ocean_Normal";
        private const string OceanCardOwnedPath = "Themes/Artwork/Ocean/Card_Ocean_Owned";
        private const string OceanCardSelectedPath = "Themes/Artwork/Ocean/Card_Ocean_Selected_Final";
        private const string GardenCardPath = "Themes/Artwork/Crystal/Card_Garden_Final";
        private const string GardenCardOwnedPath = "Themes/Artwork/Crystal/Card_Garden_Owned";
        private const string GardenCardSelectedPath = "Themes/Artwork/Crystal/Card_Garden_Selected";
        private const string BlossomCardPath = "Themes/Artwork/Neon/Card_Blossom_Final";
        private const string BlossomCardOwnedPath = "Themes/Artwork/Neon/Card_Blossom_Owned";
        private const string BlossomCardSelectedPath = "Themes/Artwork/Neon/Card_Blossom_Selected";
        private const string DesertCardPath = "Themes/Artwork/Gold/Card_Desert_Final";
        private const string DesertCardOwnedPath = "Themes/Artwork/Gold/Card_Desert_Owned";
        private const string DesertCardSelectedPath = "Themes/Artwork/Gold/Card_Desert_Selected";
        private const string CandyCardPath = "Themes/Artwork/Candy/Card_Candy_Final";
        private const string CandyCardOwnedPath = "Themes/Artwork/Candy/Card_Candy_Owned";
        private const string CandyCardSelectedPath = "Themes/Artwork/Candy/Card_Candy_Selected";
        private const string BeachCardPath = "Themes/Artwork/Aqua/Card_Beach_Final";
        private const string BeachCardOwnedPath = "Themes/Artwork/Aqua/Card_Beach_Owned";
        private const string BeachCardSelectedPath = "Themes/Artwork/Aqua/Card_Beach_Selected";
        private const string RewardsCoinsIconPath = "Ocean/Rewards/icon_coins";
        private const string RewardsClosedChestIconPath = "Ocean/Rewards/icon_chest_closed";
        private const string RewardsGrandIconPath = "Ocean/Rewards/icon_grand_reward";
        private const string RewardsPearlIconPath = "Ocean/Rewards/icon_pearl";
        private const string RewardsPowerupIconPath = "Ocean/Rewards/icon_powerup";
        private const string RewardsClaimActivePath = "Ocean/Rewards/btn_claim_active";
        private const string RewardsClaimDisabledPath = "Ocean/Rewards/btn_claim_disabled";
        private const string DailyRewardPrefabResourcePath = "UI/DailyRewardPanel";
        private const string SelectedLanguageKey = "SelectedLanguage";
        private const string EnglishLanguageCode = "en";
        private const float MainMenuButtonSpacing = 10f;
        private const int MainMenuReferenceButtonCount = 5;
        private const float MainMenuReferenceButtonSpacing = 16f;
        private const float MainMenuReferenceColumnHeight = 0.625f;
        private static readonly Vector2 MainMenuColumnAnchorMin = new Vector2(0.11f, 0.05f);
        private static readonly Vector2 MainMenuColumnAnchorMax = new Vector2(0.89f, 0.5421f);
        private static readonly ThemeType[] VisibleThemeTypes =
        {
            ThemeType.Ocean,
            ThemeType.Crystal,
            ThemeType.Neon,
            ThemeType.Gold,
            ThemeType.Candy,
            ThemeType.Aqua
        };

        [SerializeField] private Button classicButton;
        [SerializeField] private Button newClassicButton;
        [SerializeField] private Button blitzButton;
        [SerializeField] private Button dailyButton;
        [SerializeField] private TMP_Text dailyInfoText;
        [SerializeField] private Button dailyGiftButton;
        [SerializeField] private TMP_Text dailyGiftButtonText;
        [SerializeField] private GameObject settingsRoot;
        [SerializeField] private TMP_Text settingsStatusText;
        [SerializeField] private Button settingsCloseButton;
        [SerializeField] private Button settingsSoundButton;
        [SerializeField] private TMP_Text settingsSoundButtonText;
        [SerializeField] private Button settingsHapticsButton;
        [SerializeField] private TMP_Text settingsHapticsButtonText;
        [SerializeField] private Button settingsPerformanceButton;
        [SerializeField] private TMP_Text settingsPerformanceButtonText;
        private Button settingsLanguageButton;
        private Button settingsPrivacyButton;
        private Button settingsTermsButton;
        private Button settingsAboutButton;
        private TMP_Text settingsVersionText;
        private TMP_Text settingsTitleText;
        private GameObject settingsAboutRoot;
        private TMP_Text settingsAboutBodyText;
        [SerializeField] private Button themeButton;
        [SerializeField] private TMP_Text themeButtonText;
        [SerializeField] private TMP_Text themeHintText;
        [SerializeField] private Image[] themeSwatches;
        private GameObject themesRoot;
        private RectTransform themesPanel;
        private Image themesCoinPill;
        private TMP_Text themesCoinText;
        private TMP_Text themesFeedbackText;
        private Button themesCloseButton;
        private Button themesApplyButton;
        private Coroutine themesTransitionRoutine;
        private readonly List<ThemeCardRuntime> themeCards = new List<ThemeCardRuntime>();
        private ThemeType pendingTheme;
        private bool hasPendingTheme;
        [SerializeField] private Button shopButton;
        [SerializeField] private GameObject shopRoot;
        [SerializeField] private TMP_Text shopStatusText;
        [SerializeField] private Button shopCloseButton;
        [SerializeField] private Button removeAdsButton;
        [SerializeField] private Button shopCosmeticsButton;
        [SerializeField] private Button shopRestoreButton;
        [SerializeField] private Button achievementsButton;
        [SerializeField] private GameObject achievementsRoot;
        [SerializeField] private TMP_Text achievementsListText;
        [SerializeField] private Button achievementsCloseButton;
        [SerializeField] private DailyRewardView dailyRewardPrefab;
        [SerializeField] private DailyRewardView dailyRewardView;
        private TMP_Text rewardsCoinBalanceText;
        private TMP_Text rewardsFeedbackText;
        private TMP_Text rewardsClaimButtonText;
        private RectTransform rewardsCardsRoot;
        private RectTransform rewardsNormalCardsRoot;
        private RectTransform rewardsRowOne;
        private RectTransform rewardsRowTwo;
        private Image[] rewardCardImages;
        private Button[] rewardCardButtons;
        private Outline[] rewardCardOutlines;
        private Image[] rewardCardAccentImages;
        private Image[] rewardCardCoinImages;
        private Image[] rewardCardLockedOverlays;
        private Image[] rewardCardStateBadges;
        private TMP_Text[] rewardCardDayTexts;
        private TMP_Text[] rewardCardAmountTexts;
        private TMP_Text[] rewardCardStateTexts;
        private Sprite rewardCoinsIconSprite;
        private Sprite rewardClosedChestIconSprite;
        private Sprite rewardGrandIconSprite;
        private Sprite rewardPearlIconSprite;
        private Sprite rewardPowerupIconSprite;
        private Sprite rewardClaimActiveSprite;
        private Sprite rewardClaimDisabledSprite;
        private bool rewardSpritesLoaded;
        private bool dailyRewardPrefabResolutionErrorLogged;
        private Coroutine rewardsRefreshRoutine;
        [SerializeField] private Button quitButton;
        [SerializeField] private TMP_Text highScoresText;
        [SerializeField] private RankProgressView rankProgressView;
        private Camera menuCamera;
        private Canvas menuLayerCanvas;
        private Image oceanBackgroundImage;
        private Image oceanLogoImage;
        [SerializeField] private RectTransform mainMenuButtonColumn;
        private bool rewardsLogoStateCaptured;
        private bool rewardsLogoWasActive;
        private readonly List<GameObject> hiddenGameplayObjects = new List<GameObject>();
        private readonly Dictionary<GameObject, bool> settingsModalMenuObjectStates = new Dictionary<GameObject, bool>();
        private bool settingsModalMenuStateCaptured;

        private sealed class ThemeCardRuntime
        {
            public ThemeType Theme;
            public Button Button;
            public Image Artwork;
        }

        private void Awake()
        {
            EnforceEnglishLanguage();
            EnsureMenuCameraAndCanvas();
            DisableCameraWarningTexts();
            EnsureAchievementsUi();
            EnsureMainMenuButtons();
            RemoveMainMenuSettingsEntry();
            EnsureThemesUi();
            if (themesRoot != null)
            {
                themesRoot.SetActive(false);
            }
            EnsureOceanBackground();
            EnsureOceanLogo();
            StylePremiumMainMenu(null);
            HideLegacyMenuSceneArtifacts();
            HideGameplaySceneObjects();

            if (classicButton != null)
            {
                classicButton.onClick.AddListener(() => StartMode(GameMode.Classic));
            }

            if (newClassicButton != null)
            {
                newClassicButton.onClick.RemoveAllListeners();
                newClassicButton.onClick.AddListener(StartFreshClassic);
            }

            if (blitzButton != null)
            {
                blitzButton.onClick.AddListener(() => StartMode(GameMode.Blitz));
            }

            if (dailyButton != null)
            {
                dailyButton.onClick.AddListener(() => StartMode(GameMode.Daily));
            }

            if (dailyGiftButton != null)
            {
                dailyGiftButton.onClick.RemoveAllListeners();
            }

            if (shopButton != null)
            {
                shopButton.onClick.RemoveAllListeners();
                shopButton.onClick.AddListener(OpenShop);
            }

            if (shopCloseButton != null)
            {
                shopCloseButton.onClick.RemoveAllListeners();
                shopCloseButton.onClick.AddListener(CloseShop);
            }

            if (shopCosmeticsButton != null)
            {
                shopCosmeticsButton.onClick.RemoveAllListeners();
            }

            if (shopRestoreButton != null)
            {
                shopRestoreButton.onClick.RemoveAllListeners();
            }

            if (removeAdsButton != null)
            {
                removeAdsButton.onClick.RemoveAllListeners();
            }

            if (achievementsButton != null)
            {
                achievementsButton.onClick.RemoveAllListeners();
                achievementsButton.onClick.AddListener(OpenRewards);
            }

            if (achievementsCloseButton != null)
            {
                achievementsCloseButton.onClick.RemoveAllListeners();
                achievementsCloseButton.onClick.AddListener(CloseAchievements);
            }

            WireThemeButton();

            if (quitButton != null)
            {
                quitButton.onClick.AddListener(QuitGame);
            }

            if (shopRoot != null)
            {
                shopRoot.SetActive(false);
            }

            if (achievementsRoot != null)
            {
                achievementsRoot.SetActive(false);
            }

            if (settingsRoot != null)
            {
                SetSettingsOverlayInputState(false);
                settingsRoot.SetActive(false);
            }

            if (themesRoot != null)
            {
                themesRoot.SetActive(false);
            }
        }

        private void Update()
        {
            if (BackButton.WasPressedThisFrame())
            {
                if (achievementsRoot != null && achievementsRoot.activeSelf)
                {
                    CloseAchievements();
                    return;
                }

                if (shopRoot != null && shopRoot.activeSelf)
                {
                    CloseShop();
                    return;
                }

                if (themesRoot != null && themesRoot.activeSelf)
                {
                    CloseThemes();
                    return;
                }

                QuitGame();
            }
        }

        private void OnEnable()
        {
            ThemeCatalog.ThemeChanged -= HandleThemeChanged;
            ThemeCatalog.ThemeChanged += HandleThemeChanged;
            WireThemeButton();
            HideGameplaySceneObjects();
            Refresh();
        }

        private void OnDisable()
        {
            ThemeCatalog.ThemeChanged -= HandleThemeChanged;
            StopThemesTransition();
            StopRewardsRefreshRoutine();
            RestoreOceanLogoAfterRewards();
            RestoreGameplaySceneObjects();
        }

        private void OnDestroy()
        {
            ThemeCatalog.ThemeChanged -= HandleThemeChanged;
            StopThemesTransition();
            StopRewardsRefreshRoutine();
            RestoreOceanLogoAfterRewards();
            RestoreGameplaySceneObjects();
        }

        public void Refresh()
        {
            SaveManager save = SaveManager.Instance;
            if (save == null)
            {
                return;
            }

            save.EnsureDailyState();
            ThemeType selectedTheme = ChromaPalette.CurrentTheme;

            bool hasClassicRun = save.HasClassicRun();
            SetButtonLabel(classicButton, "PLAY");
            if (newClassicButton != null)
            {
                newClassicButton.gameObject.SetActive(false);
            }

            ApplyClassicButtonLayout(hasClassicRun);
            SetButtonLabel(blitzButton, "BLITZ");
            SetButtonLabel(dailyButton, "DAILY");

            if (highScoresText != null)
            {
                RankInfo rank = RankSystem.GetInfo(save.Data.rankPoints);
                highScoresText.text = $"RANK {rank.name.ToUpperInvariant()}";
            }

            if (rankProgressView != null)
            {
                rankProgressView.Refresh(save.Data.rankPoints);
            }

            if (dailyInfoText != null)
            {
                dailyInfoText.text = string.Empty;
            }

            if (themeButtonText == null && themeButton != null)
            {
                themeButtonText = themeButton.GetComponentInChildren<TMP_Text>();
            }

            if (themeButtonText != null)
            {
                if (save.Data.selectedTheme != (int)selectedTheme)
                {
                    save.SetTheme((int)selectedTheme);
                }

                themeButtonText.text = "THEMES";
            }

            if (themeHintText != null)
            {
                themeHintText.text = string.Empty;
            }

            if (themesRoot != null && themesRoot.activeSelf)
            {
                RefreshThemesOverlay(save);
            }

            RefreshAchievements(save);
            RefreshDailyGift(save);
            DisableCameraWarningTexts();
            StylePremiumMainMenu(save);
        }

        private void OpenThemes()
        {
            AudioManager.Instance?.PlayClick();
            SetSettingsOverlayInputState(false);
            EnsureThemesUi();
            if (themesRoot != null)
            {
                themesRoot.SetActive(true);
                themesRoot.transform.SetAsLastSibling();
            }

            StartThemesOpenTransition();

            SaveManager save = SaveManager.Instance;
            if (save == null)
            {
                Debug.LogWarning("[Themes] Popup opened before SaveManager became available; theme actions remain disabled until data is ready.");
                return;
            }

            // The popup currently exposes only the Ocean card. Keep the active
            // theme as the pending choice until the Ocean card is actually tapped.
            // That leaves the supplied normal card art visible for other active themes.
            pendingTheme = ChromaPalette.CurrentTheme;
            hasPendingTheme = true;
            RefreshThemesOverlay(save);
        }

        private void WireThemeButton()
        {
            if (themeButton == null)
            {
                return;
            }

            themeButton.enabled = true;
            themeButton.interactable = true;
            themeButton.onClick.RemoveListener(OpenThemes);
            themeButton.onClick.AddListener(OpenThemes);
        }

        private void CloseThemes()
        {
            AudioManager.Instance?.PlayClick();
            pendingTheme = ChromaPalette.CurrentTheme;
            hasPendingTheme = false;
            ApplyThemesCoinBalance(GetActiveThemesAssetSet());
            if (themesRoot != null && themesRoot.activeSelf)
            {
                StopThemesTransition();
                themesTransitionRoutine = StartCoroutine(AnimateThemesClosed());
            }
        }

        private void StartThemesOpenTransition()
        {
            if (themesRoot == null || themesPanel == null)
            {
                return;
            }

            StopThemesTransition();
            themesTransitionRoutine = StartCoroutine(AnimateThemesOpened());
        }

        private IEnumerator AnimateThemesOpened()
        {
            CanvasGroup group = themesRoot.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = themesRoot.AddComponent<CanvasGroup>();
            }

            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            themesPanel.localScale = Vector3.one * 0.94f;

            const float duration = 0.12f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = EaseOutCubic(t);
                group.alpha = eased;
                themesPanel.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, eased);
                yield return null;
            }

            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
            themesPanel.localScale = Vector3.one;
            themesTransitionRoutine = null;
        }

        private IEnumerator AnimateThemesClosed()
        {
            CanvasGroup group = themesRoot.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = themesRoot.AddComponent<CanvasGroup>();
            }

            group.interactable = false;
            group.blocksRaycasts = false;
            float startAlpha = group.alpha;
            Vector3 startScale = themesPanel == null ? Vector3.one : themesPanel.localScale;

            const float duration = 0.09f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = EaseOutCubic(t);
                group.alpha = Mathf.Lerp(startAlpha, 0f, eased);
                if (themesPanel != null)
                {
                    themesPanel.localScale = Vector3.Lerp(startScale, Vector3.one * 0.97f, eased);
                }

                yield return null;
            }

            if (themesPanel != null)
            {
                themesPanel.localScale = Vector3.one;
            }

            group.alpha = 1f;
            themesRoot.SetActive(false);
            themesTransitionRoutine = null;
        }

        private void StopThemesTransition()
        {
            if (themesTransitionRoutine == null)
            {
                return;
            }

            StopCoroutine(themesTransitionRoutine);
            themesTransitionRoutine = null;
        }

        private static float EaseOutCubic(float value)
        {
            float inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse;
        }

        private void ChooseTheme(ThemeType theme)
        {
            SaveManager save = SaveManager.Instance;
            if (save == null)
            {
                return;
            }

            pendingTheme = theme;
            hasPendingTheme = true;
            AudioManager.Instance?.PlayClick();

            if (save.IsThemeUnlocked(theme))
            {
                SetThemesFeedback($"{ChromaPalette.GetThemeName(theme)} READY TO APPLY", new Color(0.50f, 1f, 0.92f, 1f));
                RefreshThemesOverlay(save, false);
                return;
            }

            if (theme == ThemeType.Aqua)
            {
                SetThemesFeedback("UNLOCKS WITH DAILY REWARD DAY 7", new Color(1f, 0.82f, 0.34f, 1f));
                RefreshThemesOverlay(save, false);
                return;
            }

            int cost = ChromaPalette.GetThemeCoinCost(theme);
            SetThemesFeedback($"BUY FOR {cost:N0} COINS", new Color(1f, 0.82f, 0.34f, 1f));
            RefreshThemesOverlay(save, false);
        }

        private void ApplyPendingTheme()
        {
            SaveManager save = SaveManager.Instance;
            if (save == null)
            {
                return;
            }

            if (!hasPendingTheme)
            {
                pendingTheme = ChromaPalette.CurrentTheme;
                hasPendingTheme = true;
            }

            if (!save.IsThemeUnlocked(pendingTheme))
            {
                if (pendingTheme == ThemeType.Aqua)
                {
                    AudioManager.Instance?.PlayClick();
                    Haptics.Light();
                    SetThemesFeedback("BEACH UNLOCKS WITH DAILY REWARD DAY 7", new Color(1f, 0.82f, 0.34f, 1f));
                    RefreshThemesOverlay(save, false);
                    return;
                }

                int cost = ChromaPalette.GetThemeCoinCost(pendingTheme);
                if (save.TryBuyTheme(pendingTheme))
                {
                    ThemeType purchasedTheme = pendingTheme;
                    pendingTheme = ChromaPalette.CurrentTheme;
                    hasPendingTheme = true;
                    AudioManager.Instance?.PlayPure();
                    Haptics.Medium();
                    SetThemesFeedback($"{ChromaPalette.GetThemeName(purchasedTheme)} OWNED", new Color(0.50f, 1f, 0.92f, 1f));
                }
                else
                {
                    AudioManager.Instance?.PlayClick();
                    Haptics.Light();
                    SetThemesFeedback($"NOT ENOUGH COINS - NEED {cost:N0}", new Color(1f, 0.48f, 0.50f, 1f));
                }

                RefreshThemesOverlay(save, false);
                return;
            }

            bool changed = save.Data.selectedTheme != (int)pendingTheme;
            if (changed)
            {
                save.SetTheme((int)pendingTheme);
                AudioManager.Instance?.PlayPure();
                Haptics.Medium();
                SetThemesFeedback($"{ChromaPalette.GetThemeName(pendingTheme)} APPLIED", new Color(0.50f, 1f, 0.92f, 1f));
            }
            else
            {
                AudioManager.Instance?.PlayClick();
                SetThemesFeedback("THEME ALREADY APPLIED", new Color(0.50f, 1f, 0.92f, 1f));
            }

            RefreshThemesOverlay(save, false);
        }

        private void OpenShop()
        {
            AudioManager.Instance?.PlayClick();
            ConfigureShopPlaceholder();
            if (shopRoot != null)
            {
                shopRoot.SetActive(true);
                shopRoot.transform.SetAsLastSibling();
            }
        }

        private void CloseShop()
        {
            AudioManager.Instance?.PlayClick();
            if (shopRoot != null)
            {
                shopRoot.SetActive(false);
            }
        }

        private void ConfigureShopPlaceholder()
        {
            if (shopRoot == null)
            {
                return;
            }

            Transform panel = shopRoot.transform.Find("ShopPanel");
            if (panel == null)
            {
                return;
            }

            TMP_Text title = panel.Find("ShopTitle")?.GetComponent<TMP_Text>();
            if (title != null)
            {
                title.text = "SHOP";
            }

            if (shopStatusText == null)
            {
                shopStatusText = panel.Find("ShopStatus")?.GetComponent<TMP_Text>();
            }

            if (shopStatusText != null)
            {
                shopStatusText.text = "COMING SOON\nNew cosmetic content is being prepared.";
                shopStatusText.alignment = TextAlignmentOptions.Center;
            }

            if (removeAdsButton != null)
            {
                removeAdsButton.onClick.RemoveAllListeners();
                removeAdsButton.gameObject.SetActive(false);
            }

            if (shopCosmeticsButton != null)
            {
                shopCosmeticsButton.onClick.RemoveAllListeners();
                shopCosmeticsButton.gameObject.SetActive(false);
            }

            if (shopRestoreButton != null)
            {
                shopRestoreButton.onClick.RemoveAllListeners();
                shopRestoreButton.gameObject.SetActive(false);
            }

            if (shopCloseButton != null)
            {
                shopCloseButton.onClick.RemoveAllListeners();
                shopCloseButton.onClick.AddListener(CloseShop);
                shopCloseButton.gameObject.SetActive(true);
            }
        }

        private void OpenAchievements()
        {
            OpenRewards();
        }

        private void OpenRewards()
        {
            AudioManager.Instance?.PlayClick();
            SetSettingsOverlayInputState(false);
            ResolveDailyRewardPrefab();
            EnsureAchievementsUi();
            if (achievementsRoot == null)
            {
                ShowMissingRewardsError();
                return;
            }

            achievementsRoot.SetActive(true);
            achievementsRoot.transform.SetAsLastSibling();
            HideOceanLogoForRewards();
            dailyRewardView?.SetFeedback(string.Empty);

            BeginRewardsRefresh();
        }

        private void ShowMissingRewardsError()
        {
            if (dailyRewardPrefabResolutionErrorLogged)
            {
                return;
            }

            dailyRewardPrefabResolutionErrorLogged = true;
            Debug.LogError(
                "Daily Reward UI could not resolve a prefab with complete DailyRewardView bindings. "
                + "Assign MenuUI.dailyRewardPrefab or ensure Resources/UI/DailyRewardPanel.prefab exists.");
        }

        private DailyRewardView ResolveDailyRewardPrefab()
        {
            if (dailyRewardPrefab == null)
            {
                dailyRewardPrefab = Resources.Load<DailyRewardView>(DailyRewardPrefabResourcePath);
            }

            if (dailyRewardPrefab == null || !dailyRewardPrefab.HasCompleteBindings)
            {
                ShowMissingRewardsError();
                return null;
            }

            return dailyRewardPrefab;
        }

        private void CloseAchievements()
        {
            AudioManager.Instance?.PlayClick();
            StopRewardsRefreshRoutine();
            if (achievementsRoot != null)
            {
                achievementsRoot.SetActive(false);
            }

            RestoreOceanLogoAfterRewards();
        }

        private void BeginRewardsRefresh()
        {
            StopRewardsRefreshRoutine();
            SaveManager save = SaveManager.Instance;
            AdManager.Instance?.PrepareRewarded();
            RefreshAchievements(save);
            if (achievementsRoot != null && achievementsRoot.activeInHierarchy)
            {
                rewardsRefreshRoutine = StartCoroutine(RefreshRewardsWhenSaveIsReady());
            }
        }

        private IEnumerator RefreshRewardsWhenSaveIsReady()
        {
            bool saveReady = SaveManager.Instance != null;
            WaitForSecondsRealtime refreshDelay = new WaitForSecondsRealtime(0.5f);
            while (achievementsRoot != null && achievementsRoot.activeInHierarchy)
            {
                SaveManager save = SaveManager.Instance;
                if (!saveReady && save != null)
                {
                    RefreshAchievements(save);
                    saveReady = true;
                }
                else
                {
                    // Keep the final Daily Rewards UI bound to its claim flow.
                    // RefreshRewardedAdButton targets the same serialized button and
                    // removes its listeners, which made CLAIM stop responding after
                    // the first half-second refresh tick.
                    RefreshDailyGift(save);
                }

                yield return refreshDelay;
            }

            rewardsRefreshRoutine = null;
        }

        private void StopRewardsRefreshRoutine()
        {
            if (rewardsRefreshRoutine == null)
            {
                return;
            }

            StopCoroutine(rewardsRefreshRoutine);
            rewardsRefreshRoutine = null;
        }

        private void OpenSettings()
        {
            AudioManager.Instance?.PlayClick();
            EnsureSettingsUi();
            WireCompletedSettingsListeners();
            if (settingsRoot != null)
            {
                settingsRoot.SetActive(true);
                settingsRoot.transform.SetAsLastSibling();
                SetSettingsOverlayInputState(true);
                SetMainMenuControlsSuppressedForSettings(true);
            }

            RefreshSettings(SaveManager.Instance);
        }

        private void CloseSettings()
        {
            AudioManager.Instance?.PlayClick();
            CloseSettingsAbout(false);
            if (settingsRoot != null)
            {
                SetSettingsOverlayInputState(false);
                settingsRoot.SetActive(false);
            }

            SetMainMenuControlsSuppressedForSettings(false);
        }

        private void SetSettingsOverlayInputState(bool visible)
        {
            if (settingsRoot == null)
            {
                return;
            }

            CanvasGroup group = settingsRoot.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = settingsRoot.AddComponent<CanvasGroup>();
            }

            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        private void SetMainMenuControlsSuppressedForSettings(bool suppressed)
        {
            if (suppressed)
            {
                if (settingsModalMenuStateCaptured)
                {
                    return;
                }

                settingsModalMenuObjectStates.Clear();
                Button[] menuButtons =
                {
                    classicButton,
                    newClassicButton,
                    blitzButton,
                    dailyButton,
                    dailyGiftButton,
                    achievementsButton,
                    themeButton,
                    shopButton,
                    quitButton
                };

                for (int i = 0; i < menuButtons.Length; i++)
                {
                    GameObject menuObject = menuButtons[i] == null ? null : menuButtons[i].gameObject;
                    if (menuObject == null || settingsModalMenuObjectStates.ContainsKey(menuObject))
                    {
                        continue;
                    }

                    settingsModalMenuObjectStates.Add(menuObject, menuObject.activeSelf);
                    menuObject.SetActive(false);
                }

                settingsModalMenuStateCaptured = true;
                return;
            }

            if (!settingsModalMenuStateCaptured)
            {
                return;
            }

            foreach (KeyValuePair<GameObject, bool> state in settingsModalMenuObjectStates)
            {
                if (state.Key != null)
                {
                    state.Key.SetActive(state.Value);
                }
            }

            settingsModalMenuObjectStates.Clear();
            settingsModalMenuStateCaptured = false;
        }

        private void ClaimDailyGift()
        {
            SaveManager save = SaveManager.Instance;
            if (save == null)
            {
                return;
            }

            int claimedDayIndex = save.GetDailyRewardDayIndex();
            if (save.TryClaimDailyGift(out int coinsClaimed))
            {
                AudioManager.Instance?.PlayPure();
                Haptics.Medium();
                AnalyticsManager.Instance?.RecordDailyGiftClaimed(coinsClaimed, save.GetDailyStreak());
                RefreshDailyGift(save);
                dailyRewardView?.SetFeedback(claimedDayIndex == SaveManager.DailyRewardDayCount - 1
                    ? "Beach Theme Unlocked!"
                    : $"+{coinsClaimed} Coins");
            }
            else
            {
                AudioManager.Instance?.PlayClick();
                RefreshDailyGift(save);
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugShowDailyRewardsDay(int dayNumber)
        {
            SaveManager save = SaveManager.Instance;
            if (save == null)
            {
                Debug.LogError("[Daily Rewards Debug] SaveManager is not available.");
                return;
            }

            save.DebugConfigureDailyRewardDay(dayNumber);
            OpenRewards();
            RefreshDailyGift(save);
        }

        public void DebugRefreshDailyRewards()
        {
            RefreshDailyGift(SaveManager.Instance);
        }
#endif

        private void RequestDailyRewardedAd()
        {
            SaveManager save = SaveManager.Instance;
            AdManager adManager = AdManager.Instance;
            if (save == null
                || adManager == null
                || !save.CanClaimDailyRewardedAd()
                || !adManager.IsRewardedReady)
            {
                RefreshRewardedAdButton(save);
                return;
            }

            dailyGiftButton.onClick.RemoveAllListeners();
            dailyGiftButton.interactable = false;
            adManager.ShowRewarded("daily_reward_coins", CompleteDailyRewardedAd);
            RefreshRewardedAdButton(save);
        }

        private void CompleteDailyRewardedAd()
        {
            SaveManager save = SaveManager.Instance;
            if (save == null || !save.TryClaimDailyRewardedAd(out int coinsClaimed))
            {
                RefreshRewardedAdButton(save);
                return;
            }

            AudioManager.Instance?.PlayPure();
            Haptics.Medium();
            AnalyticsManager.Instance?.RecordRewardedCompleted("daily_reward_coins");
            dailyRewardView?.SetFeedback($"+{coinsClaimed} Coins");

            RefreshDailyGift(save);
        }

        private void BuyRemoveAds()
        {
            AudioManager.Instance?.PlayClick();
            IAPManager.Instance?.BuyRemoveAds();
            Refresh();
        }

        private void BuyCosmeticPack()
        {
            AudioManager.Instance?.PlayClick();
            IAPManager.Instance?.BuyCosmeticPack();
            Refresh();
        }

        private void RestorePurchases()
        {
            AudioManager.Instance?.PlayClick();
            IAPManager.Instance?.RestorePurchases();
            if (shopStatusText != null)
            {
                shopStatusText.text = "RESTORE PORNIT\nVERIFICA STORE-UL";
            }
        }

        private void ToggleMenuSound()
        {
            AudioManager audio = AudioManager.Instance;
            if (audio != null)
            {
                if (audio.Muted)
                {
                    audio.SetSoundEnabled(true);
                    audio.PlayToggle();
                }
                else
                {
                    audio.PlayToggle();
                    audio.SetSoundEnabled(false);
                }
            }
            else
            {
                bool enabled = PlayerPrefs.GetInt("SoundEnabled", 1) != 0;
                PlayerPrefs.SetInt("SoundEnabled", enabled ? 0 : 1);
                PlayerPrefs.Save();
            }

            RefreshSettings(SaveManager.Instance);
        }

        private void ToggleMenuMusic()
        {
            AudioManager audio = AudioManager.Instance;
            if (audio != null)
            {
                audio.PlayToggle();
                audio.ToggleMusic();
            }
            else
            {
                bool enabled = PlayerPrefs.GetInt("MusicEnabled", 1) != 0;
                PlayerPrefs.SetInt("MusicEnabled", enabled ? 0 : 1);
                PlayerPrefs.Save();
            }

            RefreshSettings(SaveManager.Instance);
        }

        private void ToggleMenuHaptics()
        {
            bool enabled = !Haptics.IsEnabled();
            Haptics.SetEnabled(enabled);
            AudioManager.Instance?.PlayToggle();
            if (enabled)
            {
                Haptics.Light();
            }

            RefreshSettings(SaveManager.Instance);
        }

        private void CycleMenuPerformanceMode()
        {
            SaveManager save = SaveManager.Instance;
            if (save == null)
            {
                return;
            }

            save.CyclePerformanceMode();
            AudioManager.Instance?.PlayClick();
            Haptics.Light();
            RefreshSettings(save);
        }

        private void ToggleMenuLanguage()
        {
            EnforceEnglishLanguage();
            RefreshSettings(SaveManager.Instance);
        }

        private void EnforceEnglishLanguage()
        {
            if (PlayerPrefs.HasKey(SelectedLanguageKey)
                && PlayerPrefs.GetString(SelectedLanguageKey, EnglishLanguageCode) == EnglishLanguageCode)
            {
                return;
            }

            PlayerPrefs.SetString(SelectedLanguageKey, EnglishLanguageCode);
            PlayerPrefs.Save();
        }

        private void EnsureThemesUi()
        {
            if (themesRoot == null)
            {
                Transform existing = transform.Find("ThemesOverlay");
                if (existing != null)
                {
                    themesRoot = existing.gameObject;
                }
            }

            if (themesRoot == null)
            {
                RectTransform overlay = CreateRuntimePanel("ThemesOverlay", transform, new Color(0f, 0.025f, 0.09f, 0.78f));
                Stretch(overlay, Vector2.zero, Vector2.zero);
                themesRoot = overlay.gameObject;
            }

            RectTransform overlayRect = themesRoot.transform as RectTransform;
            if (overlayRect == null)
            {
                return;
            }

            Image dim = themesRoot.GetComponent<Image>();
            if (dim == null)
            {
                dim = themesRoot.AddComponent<Image>();
            }

            dim.color = new Color(0f, 0.018f, 0.075f, 0.84f);
            dim.raycastTarget = true;
            Stretch(overlayRect, Vector2.zero, Vector2.zero);

            themesPanel = overlayRect.Find("ThemesPanel") as RectTransform;
            if (themesPanel == null)
            {
                themesPanel = CreateRuntimePanel("ThemesPanel", overlayRect, new Color(0.015f, 0.075f, 0.18f, 0.985f));
            }

            // Every themed panel uses the same portrait layout, so only the sprite
            // changes while this shared 9:16 RectTransform remains untouched.
            SetRect(themesPanel, new Vector2(0.045f, 0.116f), new Vector2(0.955f, 0.884f), Vector2.zero, Vector2.zero);
            Image panelImage = themesPanel.GetComponent<Image>();
            Sprite panelBackground = ResolveThemesPanelBackground(GetActiveThemesAssetSet());
            if (panelBackground != null)
            {
                panelImage.sprite = panelBackground;
                panelImage.type = Image.Type.Simple;
                panelImage.preserveAspect = false;
                panelImage.color = Color.white;
                panelImage.material = null;
            }
            else
            {
                UISpriteFactory.ApplyRounded(panelImage, 0.075f);
                panelImage.color = new Color(0.015f, 0.075f, 0.18f, 0.985f);
            }
            // The panel is visual-only. Its children own all Themes-popup input.
            panelImage.raycastTarget = false;

            Outline panelOutline = themesPanel.GetComponent<Outline>();
            if (panelOutline == null)
            {
                panelOutline = themesPanel.gameObject.AddComponent<Outline>();
            }

            panelOutline.effectColor = new Color(0.20f, 0.88f, 1f, 0.72f);
            panelOutline.effectDistance = new Vector2(3f, -3f);
            panelOutline.useGraphicAlpha = true;
            panelOutline.enabled = panelBackground == null;

            Shadow panelShadow = EnsureStandaloneShadow(themesPanel.gameObject);
            panelShadow.effectColor = new Color(0f, 0.01f, 0.055f, 0.72f);
            panelShadow.effectDistance = new Vector2(0f, -10f);
            panelShadow.useGraphicAlpha = true;
            panelShadow.enabled = panelBackground == null;

            Image panelRim = EnsureThemeImage(themesPanel, "ThemesPanelInnerRim");
            SetRect(panelRim.rectTransform, Vector2.zero, Vector2.one, new Vector2(7f, 7f), new Vector2(-7f, -7f));
            UISpriteFactory.ApplyFrame(panelRim, 0.075f, 0.025f);
            panelRim.color = new Color(0.54f, 0.96f, 1f, 0.30f);
            panelRim.raycastTarget = false;
            panelRim.gameObject.SetActive(panelBackground == null);
            panelRim.transform.SetAsFirstSibling();

            Image panelGloss = EnsureThemeImage(themesPanel, "ThemesPanelGloss");
            SetRect(panelGloss.rectTransform, new Vector2(0.025f, 0.72f), new Vector2(0.975f, 0.985f), Vector2.zero, Vector2.zero);
            UISpriteFactory.ApplyRounded(panelGloss, 0.20f);
            panelGloss.color = new Color(0.38f, 0.90f, 1f, 0.055f);
            panelGloss.raycastTarget = false;
            panelGloss.gameObject.SetActive(panelBackground == null);
            panelGloss.transform.SetSiblingIndex(1);

            TMP_Text title = EnsureThemeText(themesPanel, "ThemesTitle", "THEMES", 58f, TextAlignmentOptions.Center);
            title.gameObject.SetActive(false);
            title.raycastTarget = false;
            Transform legacySubtitle = themesPanel.Find("ThemesSubtitle");
            if (legacySubtitle != null)
            {
                legacySubtitle.gameObject.SetActive(false);
            }

            themesCoinPill = EnsureThemeImage(themesPanel, "ThemesCoinPill");
            // Compact balance bar below the baked heading, leaving more room for
            // the enlarged two-column, three-row card grid.
            SetRect(themesCoinPill.rectTransform, new Vector2(0.365f, 0.782f), new Vector2(0.635f, 0.824f), Vector2.zero, Vector2.zero);
            Sprite coinBalanceSprite = Resources.Load<Sprite>(ThemesCoinBalancePath);
            if (coinBalanceSprite != null)
            {
                themesCoinPill.sprite = coinBalanceSprite;
                themesCoinPill.type = Image.Type.Simple;
                themesCoinPill.preserveAspect = true;
                themesCoinPill.material = null;
                themesCoinPill.color = Color.white;
            }
            else
            {
                UISpriteFactory.ApplyRounded(themesCoinPill, 0.48f);
                themesCoinPill.color = new Color(0.025f, 0.19f, 0.40f, 0.96f);
            }
            themesCoinPill.raycastTarget = false;
            Outline coinOutline = themesCoinPill.GetComponent<Outline>();
            if (coinOutline == null)
            {
                coinOutline = themesCoinPill.gameObject.AddComponent<Outline>();
            }

            coinOutline.effectColor = new Color(1f, 0.84f, 0.26f, 0.48f);
            coinOutline.effectDistance = new Vector2(2f, -2f);
            coinOutline.useGraphicAlpha = true;
            coinOutline.enabled = coinBalanceSprite == null;

            themesCoinText = EnsureThemeText(themesPanel, "ThemesCoinBalance", string.Empty, 32f, TextAlignmentOptions.Center);
            SetRect(themesCoinText.rectTransform, new Vector2(0.445f, 0.782f), new Vector2(0.625f, 0.824f), Vector2.zero, Vector2.zero);
            themesCoinText.color = Color.white;
            themesCoinText.raycastTarget = false;
            EnsureTextShadow(themesCoinText, new Color(0f, 0.02f, 0.08f, 0.70f), new Vector2(0f, -2f));

            themesFeedbackText = EnsureThemeText(themesPanel, "ThemesFeedback", string.Empty, 27f, TextAlignmentOptions.Center);
            SetRect(themesFeedbackText.rectTransform, new Vector2(0.10f, 0.035f), new Vector2(0.90f, 0.085f), Vector2.zero, Vector2.zero);
            themesFeedbackText.raycastTarget = false;
            themesFeedbackText.gameObject.SetActive(false);

            EnsureThemeCards();
            EnsureThemesApplyButton();
            EnsureThemesCloseButton();
            themesCoinPill.transform.SetAsLastSibling();
            themesCoinText.transform.SetAsLastSibling();
            themesApplyButton.transform.SetAsLastSibling();
            themesFeedbackText.transform.SetAsLastSibling();
            themesCloseButton.transform.SetAsLastSibling();
            themesRoot.transform.SetAsLastSibling();
        }

        private void EnsureThemeCards()
        {
            if (themesPanel == null)
            {
                return;
            }

            themeCards.Clear();
            for (int childIndex = themesPanel.childCount - 1; childIndex >= 0; childIndex--)
            {
                Transform child = themesPanel.GetChild(childIndex);
                if (child.name.StartsWith("ThemeCard_", System.StringComparison.Ordinal)
                    && !IsVisibleThemeCardName(child.name))
                {
                    child.gameObject.SetActive(false);
                }
            }

            for (int themeIndex = 0; themeIndex < VisibleThemeTypes.Length; themeIndex++)
            {
                ThemeType theme = VisibleThemeTypes[themeIndex];
                string cardName = $"ThemeCard_{theme}";
                Transform existing = themesPanel.Find(cardName);
                Button cardButton = existing == null ? null : existing.GetComponent<Button>();
                if (cardButton == null)
                {
                    cardButton = CreateRuntimeButton(cardName, themesPanel, string.Empty, Color.white, Color.white);
                }

                cardButton.gameObject.SetActive(true);
                // The supplied card artwork contains its own title, price, lock state,
                // and frame. Disable the previous generated layers so they cannot cover it.
                for (int childIndex = 0; childIndex < cardButton.transform.childCount; childIndex++)
                {
                    cardButton.transform.GetChild(childIndex).gameObject.SetActive(false);
                }

                GetThemesGridSlotRect(theme, out Vector2 cardAnchorMin, out Vector2 cardAnchorMax);
                SetRect((RectTransform)cardButton.transform, cardAnchorMin, cardAnchorMax, Vector2.zero, Vector2.zero);
                Image cardImage = cardButton.image != null ? cardButton.image : cardButton.GetComponent<Image>();
                // The Button root is only a full-slot hit target. Artwork is in its
                // own non-raycasting child, preventing transparent pixels from stealing taps.
                cardImage.sprite = null;
                cardImage.type = Image.Type.Simple;
                cardImage.preserveAspect = false;
                cardImage.material = null;
                cardImage.color = Color.clear;
                cardImage.raycastTarget = true;
                cardButton.targetGraphic = cardImage;
                cardButton.interactable = true;
                // The root Image is a transparent hit target. ColorTint can turn it
                // opaque white on the next Selectable state update and cover an
                // unavailable/dormant artwork child.
                cardButton.transition = Selectable.Transition.None;
                ColorBlock colors = cardButton.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = Color.white;
                colors.pressedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
                colors.selectedColor = Color.white;
                colors.disabledColor = Color.white;
                colors.colorMultiplier = 1f;
                cardButton.colors = colors;

                Outline cardOutline = cardButton.GetComponent<Outline>();
                if (cardOutline != null)
                {
                    cardOutline.enabled = false;
                }

                Shadow cardShadow = cardButton.GetComponent<Shadow>();
                if (cardShadow != null)
                {
                    cardShadow.enabled = false;
                }

                Image cardArtwork = EnsureThemeImage(cardButton.transform, "ThemeCardArtwork");
                // Every card sprite is normalized to the same 720 x 720 canvas and
                // visible-art bounds. Keep one shared RectTransform for all six cards;
                // Ocean must never receive a larger runtime compensation.
                SetRect(cardArtwork.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                // preserveAspect keeps every card identical. The previous 1.2744 X
                // compensation left less than one reference pixel between columns
                // on a real portrait device. Retain the readable approved height,
                // but cap the width so the two columns have a genuine device gap.
                cardArtwork.rectTransform.localScale = new Vector3(1.19f, 1f, 1f);

                cardArtwork.type = Image.Type.Simple;
                cardArtwork.preserveAspect = true;
                cardArtwork.material = null;
                cardArtwork.color = Color.white;
                cardArtwork.raycastTarget = false;
                cardArtwork.gameObject.SetActive(true);
                // Keep the final supplied artwork above any dormant legacy card
                // surfaces. The artwork itself never receives raycasts, so the
                // full-slot Button below remains the interaction target.
                cardArtwork.transform.SetAsLastSibling();

                ThemeType clickedTheme = theme;
                cardButton.onClick.RemoveAllListeners();
                cardButton.onClick.AddListener(() => ChooseTheme(clickedTheme));
                // Keep the hit target local to the card root. The shared menu-button
                // helper also rewrites descendant CanvasGroups/Graphics and must not
                // touch the supplied card artwork hierarchy.
                cardImage.enabled = true;
                cardImage.raycastTarget = true;
                cardButton.targetGraphic = cardImage;
                if (cardButton.GetComponent<UIButtonFeedback>() == null)
                {
                    cardButton.gameObject.AddComponent<UIButtonFeedback>();
                }

                themeCards.Add(new ThemeCardRuntime
                {
                    Theme = theme,
                    Button = cardButton,
                    Artwork = cardArtwork
                });
            }

            EnsureThemesGridPlaceholders();
        }

        private void EnsureThemesGridPlaceholders()
        {
            ThemeType[] futureThemes = { ThemeType.Aqua, ThemeType.Candy, ThemeType.Gold, ThemeType.Crystal, ThemeType.Neon };
            for (int i = 0; i < futureThemes.Length; i++)
            {
                ThemeType theme = futureThemes[i];
                string slotName = $"ThemeCardSlot_{theme}";
                RectTransform slot = themesPanel.Find(slotName) as RectTransform;
                if (slot == null)
                {
                    GameObject slotObject = new GameObject(slotName, typeof(RectTransform));
                    slotObject.transform.SetParent(themesPanel, false);
                    slot = slotObject.GetComponent<RectTransform>();
                }

                GetThemesGridSlotRect(theme, out Vector2 anchorMin, out Vector2 anchorMax);
                SetRect(slot, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
                // The real cards now occupy these exact positions. Keep any prior
                // placeholder objects disabled so no legacy layer can overlap them.
                slot.gameObject.SetActive(false);
            }
        }

        private static void GetThemesGridSlotRect(ThemeType theme, out Vector2 anchorMin, out Vector2 anchorMax)
        {
            int index;
            switch (theme)
            {
                case ThemeType.Ocean: index = 0; break;
                case ThemeType.Crystal: index = 1; break;
                case ThemeType.Neon: index = 2; break;
                case ThemeType.Gold: index = 3; break;
                case ThemeType.Candy: index = 4; break;
                case ThemeType.Aqua: index = 5; break;
                default: index = 0; break;
            }

            int row = index / 2;
            int column = index % 2;
            // These normalized bounds are applied to the resolved panel rect, so
            // padding and gaps scale with the actual device-safe panel dimensions.
            // The slight height reduction creates a real vertical gutter while
            // preserving the approved two-column positions and readable card size.
            float xCenter = column == 0 ? 0.299f : 0.701f;
            float yCenter = row == 0 ? 0.655f : row == 1 ? 0.44f : 0.225f;
            const float halfWidth = 0.188f;
            const float halfHeight = 0.1025f;
            anchorMin = new Vector2(xCenter - halfWidth, yCenter - halfHeight);
            anchorMax = new Vector2(xCenter + halfWidth, yCenter + halfHeight);
        }

        private static bool IsVisibleThemeCardName(string objectName)
        {
            for (int i = 0; i < VisibleThemeTypes.Length; i++)
            {
                if (objectName == $"ThemeCard_{VisibleThemeTypes[i]}")
                {
                    return true;
                }
            }

            return false;
        }

        private static Sprite ResolveThemeCardArtwork(ThemeType theme, bool owned, bool selected)
        {
            string lockedPath;
            string ownedPath;
            string selectedPath;

            switch (theme)
            {
                case ThemeType.Ocean:
                    lockedPath = OceanCardNormalPath;
                    ownedPath = OceanCardOwnedPath;
                    selectedPath = OceanCardSelectedPath;
                    break;
                case ThemeType.Crystal:
                    lockedPath = GardenCardPath;
                    ownedPath = GardenCardOwnedPath;
                    selectedPath = GardenCardSelectedPath;
                    break;
                case ThemeType.Neon:
                    lockedPath = BlossomCardPath;
                    ownedPath = BlossomCardOwnedPath;
                    selectedPath = BlossomCardSelectedPath;
                    break;
                case ThemeType.Gold:
                    lockedPath = DesertCardPath;
                    ownedPath = DesertCardOwnedPath;
                    selectedPath = DesertCardSelectedPath;
                    break;
                case ThemeType.Candy:
                    lockedPath = CandyCardPath;
                    ownedPath = CandyCardOwnedPath;
                    selectedPath = CandyCardSelectedPath;
                    break;
                case ThemeType.Aqua:
                    lockedPath = BeachCardPath;
                    ownedPath = BeachCardOwnedPath;
                    selectedPath = BeachCardSelectedPath;
                    break;
                default:
                    return null;
            }

            string requestedPath = !owned ? lockedPath : selected ? selectedPath : ownedPath;
            Sprite sprite = Resources.Load<Sprite>(requestedPath);
            if (sprite != null)
            {
                return sprite;
            }

            // Keep old saves and partially imported artwork safe: an owned card may
            // fall back to its locked artwork, but a locked card can never borrow a
            // selected/owned visual that would misrepresent ownership.
            return Resources.Load<Sprite>(lockedPath);
        }

        private void EnsureThemesCloseButton()
        {
            if (themesPanel == null)
            {
                return;
            }

            Transform existing = themesPanel.Find("ThemesCloseButton");
            themesCloseButton = existing == null ? null : existing.GetComponent<Button>();
            if (themesCloseButton == null)
            {
                themesCloseButton = CreateRuntimeButton("ThemesCloseButton", themesPanel, string.Empty, Color.clear, Color.white);
            }

            SetRect((RectTransform)themesCloseButton.transform, new Vector2(0.785f, 0.875f), new Vector2(0.890f, 0.945f), Vector2.zero, Vector2.zero);
            // Keep the Button's hit surface separate from its artwork. A Button can
            // legally change its target Image state; the supplied Close Ocean sprite
            // must never be allowed to disappear along with that hit surface.
            Image closeHitImage = themesCloseButton.GetComponent<Image>();
            if (closeHitImage == null)
            {
                closeHitImage = themesCloseButton.gameObject.AddComponent<Image>();
            }

            closeHitImage.sprite = null;
            closeHitImage.color = Color.clear;
            closeHitImage.raycastTarget = true;
            closeHitImage.enabled = true;
            themesCloseButton.targetGraphic = closeHitImage;
            ApplyThemesCloseButton(GetActiveThemesAssetSet());

            themesCloseButton.enabled = true;
            themesCloseButton.interactable = true;
            // CreateRuntimeButton originally gave this button a transparent normal
            // ColorTint state. Unity reapplied that state on the next frame, so the
            // correct close art flashed once and then vanished. The supplied PNG is
            // self-contained artwork, so its visual must never be colour-tinted.
            themesCloseButton.transition = Selectable.Transition.None;
            ColorBlock closeColors = themesCloseButton.colors;
            closeColors.normalColor = Color.white;
            closeColors.highlightedColor = Color.white;
            closeColors.pressedColor = Color.white;
            closeColors.selectedColor = Color.white;
            closeColors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            closeColors.colorMultiplier = 1f;
            themesCloseButton.colors = closeColors;
            EnsureButtonRaycastPath(themesCloseButton);
            themesCloseButton.gameObject.SetActive(true);
            TMP_Text closeLabel = themesCloseButton.GetComponentInChildren<TMP_Text>(true);
            if (closeLabel != null)
            {
                closeLabel.gameObject.SetActive(false);
            }

            themesCloseButton.onClick.RemoveAllListeners();
            themesCloseButton.onClick.AddListener(CloseThemes);
        }

        private void EnsureThemesApplyButton()
        {
            if (themesPanel == null)
            {
                return;
            }

            Transform existing = themesPanel.Find("ThemesApplyButton");
            themesApplyButton = existing == null ? null : existing.GetComponent<Button>();
            if (themesApplyButton == null)
            {
                themesApplyButton = CreateThemesApplyButton();
            }

            SetRect((RectTransform)themesApplyButton.transform, new Vector2(0.21f, 0.023f), new Vector2(0.79f, 0.115f), Vector2.zero, Vector2.zero);

            ApplyThemesApplyButton(GetActiveThemesAssetSet());
            Image applyImage = themesApplyButton.image != null ? themesApplyButton.image : themesApplyButton.GetComponent<Image>();
            if (applyImage != null)
            {
                applyImage.raycastTarget = true;
                themesApplyButton.targetGraphic = applyImage;
            }

            ColorBlock colors = themesApplyButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.82f, 0.92f, 1f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.50f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.06f;
            themesApplyButton.colors = colors;
            themesApplyButton.transition = Selectable.Transition.ColorTint;
            themesApplyButton.enabled = true;
            themesApplyButton.interactable = true;

            RemoveThemesApplyButtonText();

            themesApplyButton.onClick.RemoveAllListeners();
            themesApplyButton.onClick.AddListener(ApplyPendingTheme);
            EnsureButtonRaycastPath(themesApplyButton);
            if (themesApplyButton.GetComponent<UIButtonFeedback>() == null)
            {
                themesApplyButton.gameObject.AddComponent<UIButtonFeedback>();
            }

            themesApplyButton.gameObject.SetActive(true);
        }

        private Button CreateThemesApplyButton()
        {
            GameObject buttonObject = new GameObject("ThemesApplyButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(themesPanel, false);

            Image image = buttonObject.GetComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = true;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            buttonObject.AddComponent<UIButtonFeedback>();
            return button;
        }

        private void RemoveThemesApplyButtonText()
        {
            if (themesApplyButton == null)
            {
                return;
            }

            TMP_Text[] labels = themesApplyButton.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == null)
                {
                    continue;
                }

                labels[i].gameObject.SetActive(false);
                Destroy(labels[i].gameObject);
            }
        }

        private void RefreshThemesOverlay(SaveManager save, bool clearFeedback = true)
        {
            if (save == null)
            {
                return;
            }

            ThemeAssetSet activeTheme = GetActiveThemesAssetSet();
            ApplyThemesPanelBackground(activeTheme);
            ApplyThemesCloseButton(activeTheme);

            if (!hasPendingTheme)
            {
                pendingTheme = ChromaPalette.CurrentTheme;
                hasPendingTheme = true;
            }

            // Coin balance artwork always follows the applied/saved theme.
            // A pending card selection (including a newly purchased-but-not-applied
            // theme) must not preview or replace this bar.
            ApplyThemesCoinBalance(activeTheme);

            bool pendingOwned = save.IsThemeUnlocked(pendingTheme);
            bool showBuy = !pendingOwned && pendingTheme != ThemeType.Aqua;
            ApplyThemesApplyButton(activeTheme, showBuy);

            if (themesCoinText != null)
            {
                themesCoinText.text = $"{save.GetCoins():N0}";
            }

            if (clearFeedback && themesFeedbackText != null)
            {
                themesFeedbackText.gameObject.SetActive(false);
                themesFeedbackText.text = string.Empty;
            }

            for (int i = 0; i < themeCards.Count; i++)
            {
                ThemeCardRuntime card = themeCards[i];
                if (card == null || card.Button == null)
                {
                    continue;
                }

                bool owned = save.IsThemeUnlocked(card.Theme);
                bool selected = owned && pendingTheme == card.Theme;
                Image cardImage = card.Artwork;
                if (cardImage == null)
                {
                    continue;
                }

                cardImage.sprite = ResolveThemeCardArtwork(card.Theme, owned, selected);
                cardImage.type = Image.Type.Simple;
                cardImage.preserveAspect = true;
                cardImage.material = null;
                cardImage.color = Color.white;
                cardImage.enabled = true;
                cardImage.gameObject.SetActive(true);
                cardImage.raycastTarget = false;
                cardImage.transform.SetAsLastSibling();
                cardImage.SetAllDirty();

                ColorBlock colors = card.Button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = Color.white;
                colors.pressedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
                colors.selectedColor = Color.white;
                colors.disabledColor = Color.white;
                colors.colorMultiplier = 1f;
                card.Button.colors = colors;

            }

            if (themesApplyButton != null)
            {
                themesApplyButton.interactable = pendingOwned || pendingTheme != ThemeType.Aqua;
            }
        }

        private void SetThemesFeedback(string message, Color color)
        {
            if (themesFeedbackText == null)
            {
                return;
            }

            themesFeedbackText.text = message;
            themesFeedbackText.color = color;
        }

        private TMP_Text EnsureThemeText(Transform parent, string objectName, string value, float fontSize, TextAlignmentOptions alignment)
        {
            Transform existing = parent.Find(objectName);
            TMP_Text text = existing == null ? null : existing.GetComponent<TMP_Text>();
            if (text == null)
            {
                text = CreateRuntimeText(objectName, parent, value, fontSize, alignment);
            }

            text.text = value;
            text.fontSize = fontSize;
            text.fontSizeMax = fontSize;
            text.fontSizeMin = Mathf.Max(12f, fontSize * 0.62f);
            text.alignment = alignment;
            text.raycastTarget = false;
            TMP_FontAsset premiumFont = Resources.Load<TMP_FontAsset>("Fonts/Fredoka-SemiBold SDF");
            if (premiumFont != null)
            {
                text.font = premiumFont;
            }

            return text;
        }

        private Image EnsureThemeImage(Transform parent, string objectName)
        {
            Transform existing = parent.Find(objectName);
            Image image = existing == null ? null : existing.GetComponent<Image>();
            if (image != null)
            {
                return image;
            }

            GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            return imageObject.GetComponent<Image>();
        }

        private void RefreshShop(SaveManager save)
        {
            if (save == null)
            {
                return;
            }

            if (shopStatusText != null)
            {
                string ads = save.Data.removeAds ? "ACTIV" : "NECUMPARAT";
                string themes = save.Data.cosmeticPackOwned ? "ACTIV" : "NECUMPARAT";
                ThemeType nextLocked = save.GetNextLockedTheme();
                string nextTheme = nextLocked == ThemeType.Neon ? "toate temele sunt deblocate" : $"urmatoarea tema: {ChromaPalette.GetThemeName(nextLocked)}";
                shopStatusText.text = $"PORTOFEL: {save.GetCoins():N0} MONEDE   TEME {ChromaPalette.ThemeCount}   REALIZARI {save.GetAchievementCount()}/{AchievementSystem.Total}\nFARA RECLAME: {ads}   TOATE TEMELE: {themes}   {nextTheme}\nStiluri: Crystal, Aqua, Doodle, Arcade, Candy, Arcane, Cosmic, Zen, Storm, Sunset.";
            }

            if (removeAdsButton != null)
            {
                removeAdsButton.interactable = !save.Data.removeAds;
                SetButtonLabel(removeAdsButton, save.Data.removeAds ? "FARA ADS ACTIV\nmultumim" : "REMOVE ADS\nopreste interstitialele");
            }

            if (shopCosmeticsButton != null)
            {
                shopCosmeticsButton.interactable = !save.Data.cosmeticPackOwned;
                SetButtonLabel(shopCosmeticsButton, save.Data.cosmeticPackOwned ? "TOATE TEMELE ACTIVE\ncolectie completa" : "UNLOCK ALL THEMES\ncolectie completa");
            }

            if (shopRestoreButton != null)
            {
                SetButtonLabel(shopRestoreButton, "RESTORE\nrecupereaza cumparaturile");
            }
        }

        private void RefreshSettings(SaveManager save)
        {
            EnforceEnglishLanguage();
            bool romanian = false;
            bool legacySoundEnabled = save == null || !save.Data.soundMuted;
            AudioManager audio = AudioManager.Instance;
            bool soundEnabled = audio != null
                ? audio.SoundEnabled
                : PlayerPrefs.GetInt("SoundEnabled", legacySoundEnabled ? 1 : 0) != 0;
            bool musicEnabled = audio != null
                ? audio.MusicEnabled
                : PlayerPrefs.GetInt("MusicEnabled", legacySoundEnabled ? 1 : 0) != 0;
            bool vibrationEnabled = Haptics.IsEnabled();

            settingsSoundButtonText = ResolveButtonText(settingsSoundButton, settingsSoundButtonText);
            settingsHapticsButtonText = ResolveButtonText(settingsHapticsButton, settingsHapticsButtonText);
            settingsPerformanceButtonText = ResolveButtonText(settingsPerformanceButton, settingsPerformanceButtonText);

            SetSettingsToggleText(settingsPerformanceButtonText, musicEnabled, romanian);
            SetSettingsToggleText(settingsSoundButtonText, soundEnabled, romanian);
            SetSettingsToggleText(settingsHapticsButtonText, vibrationEnabled, romanian);
            SetButtonLabel(settingsLanguageButton, romanian ? "LIMBA\nRomână" : "LANGUAGE\nEnglish");
            SetButtonLabel(settingsPrivacyButton, romanian ? "POLITICA DE CONFIDENTIALITATE\nÎn curând" : "PRIVACY POLICY\nComing Soon");
            SetButtonLabel(settingsTermsButton, romanian ? "TERMENI SI CONDITII\nÎn curând" : "TERMS OF SERVICE\nComing Soon");
            SetButtonLabel(settingsCloseButton, romanian ? "INCHIDE" : "CLOSE");
            SetButtonLabel(settingsLanguageButton, romanian ? "Rom\u00E2n\u0103" : "English");
            SetButtonLabel(settingsPrivacyButton, romanian ? "\u00CEn cur\u00E2nd" : "Coming Soon");
            SetButtonLabel(settingsTermsButton, romanian ? "\u00CEn cur\u00E2nd" : "Coming Soon");
            SetButtonLabel(settingsAboutButton, romanian ? "DESCHIDE" : "OPEN");
            SetButtonLabel(settingsCloseButton, "X");

            SetSettingsRowLabel("MusicRow", romanian ? "Muzic\u0103" : "Music");
            SetSettingsRowLabel("SoundRow", romanian ? "Sunet" : "Sound");
            SetSettingsRowLabel("VibrationRow", romanian ? "Vibra\u021Bii" : "Vibration");
            SetSettingsRowLabel("LanguageRow", romanian ? "Limb\u0103" : "Language");
            SetSettingsRowLabel("PrivacyRow", romanian ? "Politica de confiden\u021Bialitate" : "Privacy Policy");
            SetSettingsRowLabel("TermsRow", romanian ? "Termeni \u0219i condi\u021Bii" : "Terms and Conditions");
            SetSettingsRowLabel("AboutRow", romanian ? "Despre" : "About");

            if (settingsTitleText != null)
            {
                settingsTitleText.text = romanian ? "SET\u0102RI" : "SETTINGS";
            }

            if (settingsVersionText != null)
            {
                settingsVersionText.text = $"{(romanian ? "Versiune" : "Version")} {Application.version}";
            }

            if (settingsStatusText != null)
            {
                settingsStatusText.gameObject.SetActive(false);
            }

            RefreshSettingsAboutText(romanian);
        }

        private static TMP_Text ResolveButtonText(Button button, TMP_Text current)
        {
            return current != null ? current : button == null ? null : button.GetComponentInChildren<TMP_Text>(true);
        }

        private void SetSettingsToggleText(TMP_Text text, bool enabled, bool romanian)
        {
            if (text == null)
            {
                return;
            }

            text.text = enabled ? (romanian ? "PORNIT" : "ON") : (romanian ? "OPRIT" : "OFF");
            text.color = enabled ? new Color(0.82f, 1f, 1f, 1f) : new Color(0.66f, 0.78f, 0.90f, 1f);

            RectTransform control = text.transform.parent as RectTransform;
            Button button = control == null ? null : control.GetComponent<Button>();
            Image track = button == null ? null : button.image;
            if (track != null)
            {
                track.color = enabled
                    ? new Color(0.02f, 0.48f, 0.78f, 0.99f)
                    : new Color(0.02f, 0.16f, 0.30f, 0.99f);
            }

            if (control == null)
            {
                return;
            }

            RectTransform knob = EnsureSettingsRect(control, "ToggleKnob");
            knob.anchorMin = new Vector2(0.5f, 0.5f);
            knob.anchorMax = knob.anchorMin;
            knob.pivot = new Vector2(0.5f, 0.5f);
            knob.anchoredPosition = new Vector2(enabled ? 58f : -58f, 0f);
            knob.sizeDelta = new Vector2(46f, 46f);
            knob.localScale = Vector3.one;
            Image knobImage = EnsureSettingsImage(knob.gameObject);
            UISpriteFactory.ApplySoftCircle(knobImage);
            knobImage.color = new Color(0.96f, 1f, 1f, 1f);
            knobImage.raycastTarget = false;
            knob.SetAsLastSibling();

            text.rectTransform.offsetMin = enabled ? new Vector2(12f, 4f) : new Vector2(62f, 4f);
            text.rectTransform.offsetMax = enabled ? new Vector2(-62f, -4f) : new Vector2(-12f, -4f);
            text.alignment = TextAlignmentOptions.Center;
        }

        private void RefreshAchievements(SaveManager save)
        {
            EnsureAchievementsUi();
            if (achievementsListText != null)
            {
                achievementsListText.gameObject.SetActive(false);
            }

            RefreshDailyGift(save);
        }

        private void RefreshDailyGift(SaveManager save)
        {
            EnsureDailyGiftUi();
            if (dailyGiftButton == null)
            {
                return;
            }

            bool hasSave = save != null;

            dailyRewardView?.SetBalance(hasSave ? save.GetCoins() : 0);

            RefreshDailyClaimButton(save);
            RefreshRewardCardStates(save);
        }

        private void RefreshDailyClaimButton(SaveManager save)
        {
            if (dailyGiftButton == null) return;
            bool canClaim = save != null && save.CanClaimDailyGift();
            dailyGiftButton.gameObject.name = "DailyRewardClaimButton";
            dailyGiftButton.onClick.RemoveAllListeners();
            dailyGiftButton.interactable = canClaim;
            if (canClaim) dailyGiftButton.onClick.AddListener(ClaimDailyGift);
            dailyRewardView?.ApplyDailyClaimState(canClaim);
            dailyRewardView?.SetClaimAvailability(canClaim);
        }

        private void RefreshRewardCardStates(SaveManager save)
        {
            bool saveReady = save != null;
            bool canClaimToday = !saveReady || save.CanClaimDailyGift();
            bool claimedToday = saveReady && !canClaimToday;
            int currentDayIndex = saveReady ? save.GetDailyRewardDayIndex() : 0;

            RefreshRewardCards(currentDayIndex, canClaimToday, claimedToday, saveReady);
        }

        private void RefreshRewardedAdButton(SaveManager save)
        {
            if (dailyGiftButton == null)
            {
                return;
            }

            AdManager adManager = AdManager.Instance;
            int rewardedAdCount = save == null ? 0 : save.GetDailyRewardedAdCount();
            bool limitReached = rewardedAdCount >= SaveManager.DailyRewardedAdLimit;
            bool adConfigured = adManager != null && adManager.IsRewardedConfigured;
            bool rewardedAdAvailable = save != null
                && !limitReached
                && adConfigured
                && adManager.IsRewardedReady;

            dailyGiftButton.gameObject.name = "RewardedAdButton";
            dailyGiftButton.onClick.RemoveAllListeners();
            dailyGiftButton.interactable = rewardedAdAvailable;
            if (rewardedAdAvailable)
            {
                dailyGiftButton.onClick.AddListener(RequestDailyRewardedAd);
            }

            string buttonLabel = limitReached
                ? $"Ad Rewards Claimed \u00B7 {SaveManager.DailyRewardedAdLimit}/{SaveManager.DailyRewardedAdLimit}"
                : rewardedAdAvailable
                    ? $"Watch Ad \u00B7 +{SaveManager.DailyRewardedAdCoins} Coins \u00B7 {rewardedAdCount}/{SaveManager.DailyRewardedAdLimit}"
                    : "Ad Unavailable";
            dailyRewardView?.ApplyRewardedAdState(buttonLabel, rewardedAdAvailable, limitReached);

            dailyGiftButton.gameObject.SetActive(true);
        }

        private void UpdateRewardsLowerLayout()
        {
            RectTransform daySevenCard = FindRewardCard(SaveManager.DailyRewardDayCount - 1);
            if (daySevenCard == null)
            {
                return;
            }

            SetRect(
                daySevenCard,
                Vector2.zero,
                new Vector2(1f, 0.38f),
                Vector2.zero,
                Vector2.zero);
        }

        private void ApplyClaimButtonSprite(Image target, bool active)
        {
            if (target == null)
            {
                return;
            }

            EnsureRewardSpritesLoaded();
            Sprite stateSprite = active ? rewardClaimActiveSprite : rewardClaimDisabledSprite;
            if (stateSprite != null)
            {
                target.sprite = stateSprite;
                target.type = stateSprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
                target.preserveAspect = false;
                target.color = Color.white;
                return;
            }

            UISpriteFactory.ApplyRounded(target, 0.44f);
            target.color = active
                ? new Color(0f, 0.82f, 1f, 1f)
                : new Color(0.012f, 0.105f, 0.21f, 0.98f);
        }

        private void EnsureMainMenuButtons()
        {
            classicButton = EnsureImageMenuButton(classicButton, "ClassicButton");
            blitzButton = EnsureImageMenuButton(blitzButton, "BlitzButton");
            dailyButton = EnsureImageMenuButton(dailyButton, "DailyButton");
            achievementsButton = EnsureImageMenuButton(achievementsButton, "RewardsButton");
            if (achievementsButton != null)
            {
                achievementsButton.gameObject.name = "RewardsButton";
            }
            themeButton = EnsureImageMenuButton(themeButton, "ThemeButton");
            shopButton = EnsureImageMenuButton(shopButton, "ShopButton");
            EnsureMainMenuButtonColumn();
        }

        private void EnsureMainMenuButtonColumn()
        {
            RectTransform parentRect = transform as RectTransform;
            if (parentRect == null)
            {
                return;
            }

            if (mainMenuButtonColumn == null)
            {
                Transform existing = transform.Find("MainMenuButtonColumn");
                mainMenuButtonColumn = existing as RectTransform;
            }

            if (mainMenuButtonColumn == null)
            {
                GameObject columnObject = new GameObject(
                    "MainMenuButtonColumn",
                    typeof(RectTransform),
                    typeof(VerticalLayoutGroup));
                columnObject.transform.SetParent(transform, false);
                mainMenuButtonColumn = columnObject.GetComponent<RectTransform>();
            }

            mainMenuButtonColumn.anchorMin = MainMenuColumnAnchorMin;
            mainMenuButtonColumn.anchorMax = MainMenuColumnAnchorMax;
            mainMenuButtonColumn.offsetMin = Vector2.zero;
            mainMenuButtonColumn.offsetMax = Vector2.zero;
            mainMenuButtonColumn.pivot = new Vector2(0.5f, 0.5f);
            mainMenuButtonColumn.localScale = Vector3.one;

            VerticalLayoutGroup layout = mainMenuButtonColumn.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = mainMenuButtonColumn.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = MainMenuButtonSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childScaleWidth = false;
            layout.childScaleHeight = false;

            Button[] orderedButtons =
            {
                classicButton,
                blitzButton,
                achievementsButton,
                themeButton
            };

            for (int i = 0; i < orderedButtons.Length; i++)
            {
                Button button = orderedButtons[i];
                if (button == null)
                {
                    continue;
                }

                RectTransform buttonRect = button.transform as RectTransform;
                if (buttonRect != null && buttonRect.parent != mainMenuButtonColumn)
                {
                    buttonRect.SetParent(mainMenuButtonColumn, false);
                }

                button.transform.SetSiblingIndex(i);
            }

            RefreshMainMenuButtonLayout();
        }

        private void RefreshMainMenuButtonLayout()
        {
            if (mainMenuButtonColumn == null)
            {
                return;
            }

            RectTransform parentRect = transform as RectTransform;
            if (parentRect == null)
            {
                return;
            }

            float availableWidth = parentRect.rect.width
                * (MainMenuColumnAnchorMax.x - MainMenuColumnAnchorMin.x);
            float referenceAvailableHeight = parentRect.rect.height * MainMenuReferenceColumnHeight;
            float maximumButtonHeight = Mathf.Max(
                1f,
                (referenceAvailableHeight
                    - (MainMenuReferenceButtonSpacing * (MainMenuReferenceButtonCount - 1f)))
                / MainMenuReferenceButtonCount);

            Button[] orderedButtons =
            {
                classicButton,
                blitzButton,
                achievementsButton,
                themeButton
            };

            for (int i = 0; i < orderedButtons.Length; i++)
            {
                Button button = orderedButtons[i];
                Image image = button == null ? null : button.image;
                Sprite sprite = image == null ? null : image.sprite;
                if (button == null || sprite == null || sprite.rect.height <= 0f)
                {
                    continue;
                }

                float aspect = sprite.rect.width / sprite.rect.height;
                float preferredWidth = Mathf.Min(availableWidth, maximumButtonHeight * aspect);
                float preferredHeight = preferredWidth / aspect;
                LayoutElement layoutElement = button.GetComponent<LayoutElement>();
                if (layoutElement == null)
                {
                    layoutElement = button.gameObject.AddComponent<LayoutElement>();
                }

                layoutElement.ignoreLayout = false;
                layoutElement.minWidth = 0f;
                layoutElement.minHeight = 0f;
                layoutElement.preferredWidth = preferredWidth;
                layoutElement.preferredHeight = preferredHeight;
                layoutElement.flexibleWidth = 0f;
                layoutElement.flexibleHeight = 0f;

                RectTransform buttonRect = button.transform as RectTransform;
                if (buttonRect != null)
                {
                    buttonRect.localScale = Vector3.one;
                    buttonRect.localRotation = Quaternion.identity;
                    buttonRect.pivot = new Vector2(0.5f, 0.5f);
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(mainMenuButtonColumn);
        }

        private void RemoveMainMenuSettingsEntry()
        {
            Transform oldButton = transform.Find("SettingsButton");
            if (oldButton != null)
            {
                Destroy(oldButton.gameObject);
            }

            GameObject oldSettingsRoot = settingsRoot;
            if (oldSettingsRoot == null)
            {
                Transform root = transform.Find("SettingsRoot");
                if (root == null)
                {
                    root = transform.Find("SettingsOverlay");
                }

                oldSettingsRoot = root == null ? null : root.gameObject;
            }

            if (oldSettingsRoot != null)
            {
                Destroy(oldSettingsRoot);
            }

            settingsRoot = null;
            settingsStatusText = null;
            settingsCloseButton = null;
            settingsSoundButton = null;
            settingsSoundButtonText = null;
            settingsHapticsButton = null;
            settingsHapticsButtonText = null;
            settingsPerformanceButton = null;
            settingsPerformanceButtonText = null;
            settingsLanguageButton = null;
            settingsPrivacyButton = null;
            settingsTermsButton = null;
            settingsAboutButton = null;
            settingsVersionText = null;
            settingsTitleText = null;
            settingsAboutRoot = null;
            settingsAboutBodyText = null;
        }

        private Button EnsureImageMenuButton(Button button, string objectName)
        {
            if (button != null)
            {
                return button;
            }

            Transform existing = mainMenuButtonColumn == null
                ? null
                : mainMenuButtonColumn.Find(objectName);
            if (existing == null)
            {
                existing = transform.Find(objectName);
            }
            if (existing != null)
            {
                button = existing.GetComponent<Button>();
                if (button != null)
                {
                    return button;
                }
            }

            GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(transform, false);
            button = buttonObject.GetComponent<Button>();
            if (button.GetComponent<UIButtonFeedback>() == null)
            {
                button.gameObject.AddComponent<UIButtonFeedback>();
            }

            return button;
        }

        private void EnsureOceanBackground()
        {
            RectTransform menuRect = transform as RectTransform;
            Canvas canvas = GetComponentInParent<Canvas>();
            if (menuRect == null)
            {
                return;
            }

            ThemeAssetSet activeTheme = ThemeCatalog.Current;
            Sprite backgroundSprite = activeTheme == null ? null : activeTheme.MenuBackground;
            if (backgroundSprite == null)
            {
                backgroundSprite = Resources.Load<Sprite>(OceanBackgroundPath);
            }
            if (backgroundSprite == null)
            {
                Debug.LogError($"Missing Ocean sprite at Resources path: {OceanBackgroundPath}");
                return;
            }

            RectTransform backgroundParent = canvas == null
                ? menuRect
                : canvas.transform as RectTransform;
            if (backgroundParent == null)
            {
                backgroundParent = menuRect;
            }

            if (oceanBackgroundImage == null)
            {
                Transform existing = menuRect.Find("OceanMenuBackground");
                if (existing == null && canvas != null)
                {
                    existing = canvas.transform.Find("OceanMenuBackground");
                }

                oceanBackgroundImage = existing == null ? null : existing.GetComponent<Image>();
            }

            if (oceanBackgroundImage == null)
            {
                GameObject backgroundObject = new GameObject("OceanMenuBackground", typeof(RectTransform), typeof(Image));
                backgroundObject.transform.SetParent(backgroundParent, false);
                oceanBackgroundImage = backgroundObject.GetComponent<Image>();
            }
            else if (oceanBackgroundImage.transform.parent != backgroundParent)
            {
                oceanBackgroundImage.transform.SetParent(backgroundParent, false);
            }

            oceanBackgroundImage.sprite = backgroundSprite;
            oceanBackgroundImage.color = Color.white;
            oceanBackgroundImage.type = Image.Type.Simple;
            oceanBackgroundImage.preserveAspect = true;
            // Fullscreen menu artwork is decorative. Keeping it as a raycast target
            // lets it intercept modal controls such as Themes cards.
            oceanBackgroundImage.raycastTarget = false;
            RectTransform backgroundRect = oceanBackgroundImage.rectTransform;
            Stretch(backgroundRect, Vector2.zero, Vector2.zero);
            backgroundRect.pivot = new Vector2(0.5f, 0.5f);
            backgroundRect.anchoredPosition = Vector2.zero;
            backgroundRect.localScale = Vector3.one;

            AspectRatioFitter backgroundAspect = oceanBackgroundImage.GetComponent<AspectRatioFitter>();
            if (backgroundAspect == null)
            {
                backgroundAspect = oceanBackgroundImage.gameObject.AddComponent<AspectRatioFitter>();
            }

            backgroundAspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            backgroundAspect.aspectRatio = backgroundSprite.rect.width / backgroundSprite.rect.height;
            oceanBackgroundImage.transform.SetAsFirstSibling();
            DisableLegacyBackground(menuRect);
        }

        private void HandleThemeChanged(ThemeType requestedTheme, ThemeAssetSet resolvedTheme)
        {
            EnsureOceanBackground();
            ThemeAssetSet activeTheme = resolvedTheme ?? GetActiveThemesAssetSet();
            ApplyThemesPanelBackground(activeTheme);
            ApplyThemesCloseButton(activeTheme);
            ApplyThemesApplyButton(activeTheme);
            StyleMenuSpriteButton(themeButton, ThemeButtonPath, new Vector2(0.10f, 0.137f), new Vector2(0.49f, 0.232f));
            EnsureMainMenuButtonColumn();
            if (themesRoot != null && themesRoot.activeSelf && SaveManager.Instance != null)
            {
                RefreshThemesOverlay(SaveManager.Instance, false);
            }
        }

        private void ApplyThemesPanelBackground(ThemeAssetSet themeSet)
        {
            if (themesPanel == null)
            {
                return;
            }

            Image panelImage = themesPanel.GetComponent<Image>();
            Sprite panelBackground = ResolveThemesPanelBackground(themeSet);
            if (panelImage == null || panelBackground == null)
            {
                return;
            }

            panelImage.sprite = panelBackground;
            panelImage.type = Image.Type.Simple;
            panelImage.preserveAspect = false;
            panelImage.color = Color.white;
            panelImage.material = null;
            panelImage.SetAllDirty();
        }

        private void ApplyThemesCoinBalance(ThemeAssetSet themeSet)
        {
            if (themesCoinPill == null)
            {
                return;
            }

            Sprite coinBalance = ResolveThemesCoinBalanceSprite(themeSet);
            if (coinBalance == null)
            {
                return;
            }

            themesCoinPill.sprite = coinBalance;
            themesCoinPill.type = Image.Type.Simple;
            themesCoinPill.preserveAspect = true;
            themesCoinPill.color = Color.white;
            themesCoinPill.material = null;
            themesCoinPill.raycastTarget = false;
            themesCoinPill.SetAllDirty();
        }

        private static Sprite ResolveThemesCoinBalanceSprite(ThemeAssetSet themeSet)
        {
            if (themeSet != null && themeSet.ThemesCoinBalanceSprite != null)
            {
                return themeSet.ThemesCoinBalanceSprite;
            }

            ThemeAssetSet ocean = ThemeCatalog.GetDefinition(ThemeType.Ocean);
            if (ocean != null && ocean.ThemesCoinBalanceSprite != null)
            {
                return ocean.ThemesCoinBalanceSprite;
            }

            return Resources.Load<Sprite>(ThemesCoinBalancePath);
        }

        private static Sprite ResolveThemesPanelBackground(ThemeAssetSet themeSet)
        {
            if (themeSet != null && themeSet.ThemesPanelBackgroundSprite != null)
            {
                return themeSet.ThemesPanelBackgroundSprite;
            }

            ThemeAssetSet ocean = ThemeCatalog.GetDefinition(ThemeType.Ocean);
            return ocean != null ? ocean.ThemesPanelBackgroundSprite : null;
        }

        private static ThemeAssetSet GetOceanThemesAssetSet()
        {
            return ThemeCatalog.GetDefinition(ThemeType.Ocean) ?? ThemeCatalog.Current;
        }

        private static ThemeAssetSet GetActiveThemesAssetSet()
        {
            ThemeType activeTheme = ChromaPalette.CurrentTheme;
            return ThemeCatalog.GetDefinition(activeTheme)
                ?? ThemeCatalog.Current
                ?? ThemeCatalog.GetDefinition(ThemeType.Ocean);
        }

        private void ApplyThemesCloseButton(ThemeAssetSet themeSet)
        {
            if (themesCloseButton == null)
            {
                return;
            }

            Image closeHitImage = themesCloseButton.GetComponent<Image>();
            if (closeHitImage == null)
            {
                closeHitImage = themesCloseButton.gameObject.AddComponent<Image>();
            }

            closeHitImage.sprite = null;
            closeHitImage.color = Color.clear;
            closeHitImage.raycastTarget = true;
            closeHitImage.enabled = true;
            themesCloseButton.targetGraphic = closeHitImage;

            Image closeArtwork = EnsureThemeImage(themesCloseButton.transform, "ThemesCloseArtwork");
            SetRect(closeArtwork.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Sprite closeSprite = ResolveThemesCloseButtonSprite(themeSet);
            if (closeSprite != null)
            {
                closeArtwork.sprite = closeSprite;
                closeArtwork.type = Image.Type.Simple;
                closeArtwork.preserveAspect = true;
                closeArtwork.color = Color.white;
                closeArtwork.material = null;
                closeArtwork.raycastTarget = false;
                closeArtwork.enabled = true;
                closeArtwork.gameObject.SetActive(true);
                closeArtwork.transform.SetAsLastSibling();
                closeArtwork.SetAllDirty();
            }
            else
            {
                closeArtwork.sprite = null;
                closeArtwork.enabled = false;
            }

            TMP_Text closeLabel = themesCloseButton.GetComponentInChildren<TMP_Text>(true);
            if (closeLabel != null)
            {
                closeLabel.gameObject.SetActive(false);
            }
        }

        private static Sprite ResolveThemesCloseButtonSprite(ThemeAssetSet themeSet)
        {
            if (themeSet != null && themeSet.ThemesCloseButtonSprite != null)
            {
                return themeSet.ThemesCloseButtonSprite;
            }

            // A missing themed close asset must fail safely to the known-good Ocean
            // artwork without forcing Ocean over correctly configured themes.
            Sprite exactOceanClose = Resources.Load<Sprite>(OceanThemesCloseButtonPath);
            if (exactOceanClose != null)
            {
                return exactOceanClose;
            }

            ThemeAssetSet ocean = ThemeCatalog.GetDefinition(ThemeType.Ocean);
            return ocean != null ? ocean.ThemesCloseButtonSprite : null;
        }

        private void ApplyThemesApplyButton(ThemeAssetSet themeSet, bool showBuy = false)
        {
            if (themesApplyButton == null || themesApplyButton.image == null)
            {
                return;
            }

            Image applyImage = themesApplyButton.image;
            Sprite applySprite = ResolveThemesApplyButtonSprite(showBuy);
            if (applySprite != null)
            {
                applyImage.sprite = applySprite;
                applyImage.type = Image.Type.Simple;
                applyImage.preserveAspect = true;
                applyImage.material = null;
                applyImage.color = Color.white;
                applyImage.SetAllDirty();
            }
            else
            {
                UISpriteFactory.ApplyRounded(applyImage, 0.46f);
                applyImage.color = new Color(0.04f, 0.48f, 0.90f, 1f);
            }

            RemoveThemesApplyButtonText();
        }

        private static Sprite ResolveThemesApplyButtonSprite(bool showBuy)
        {
            if (showBuy)
            {
                Sprite buySprite = Resources.Load<Sprite>(ThemesBuyButtonPath);
                if (buySprite != null)
                {
                    return buySprite;
                }
            }

            // APPLY is intentionally global: never fall back to a per-theme
            // ThemeAssetSet sprite, so every theme renders the exact same artwork.
            return Resources.Load<Sprite>(ThemesApplyButtonPath);
        }

        private static Color GetThemesGridFrameColor(ThemeAssetSet activeThemeSet)
        {
            ThemeType theme = activeThemeSet != null ? activeThemeSet.ThemeType : ThemeType.Ocean;
            switch (theme)
            {
                case ThemeType.Aqua:
                    return new Color(0.10f, 0.88f, 0.73f, 1f);
                case ThemeType.Candy:
                    return new Color(1f, 0.42f, 0.68f, 1f);
                case ThemeType.Gold:
                    return new Color(1f, 0.64f, 0.16f, 1f);
                case ThemeType.Crystal:
                    return new Color(0.40f, 0.84f, 0.48f, 1f);
                case ThemeType.Neon:
                    return new Color(0.56f, 1f, 0.12f, 1f);
                case ThemeType.Ocean:
                default:
                    return new Color(0.18f, 0.76f, 1f, 1f);
            }
        }

        private void DisableLegacyBackground(RectTransform menuRect)
        {
            Transform legacy = menuRect.Find("Background");
            if (legacy == null || oceanBackgroundImage == null || legacy == oceanBackgroundImage.transform)
            {
                return;
            }

            Image legacyImage = legacy.GetComponent<Image>();
            if (legacyImage != null)
            {
                legacyImage.enabled = false;
            }
        }

        private void EnsureMenuCameraAndCanvas()
        {
            Camera activeCamera = GetActiveDisplayCamera();
            if (activeCamera == null)
            {
                if (menuCamera == null)
                {
                    GameObject cameraObject = new GameObject("MenuCamera");
                    menuCamera = cameraObject.AddComponent<Camera>();
                }

                activeCamera = menuCamera;
            }

            activeCamera.gameObject.SetActive(true);
            activeCamera.enabled = true;
            activeCamera.targetDisplay = 0;
            activeCamera.depth = -100f;
            activeCamera.clearFlags = CameraClearFlags.SolidColor;
            activeCamera.backgroundColor = new Color(0.005f, 0.02f, 0.045f, 1f);
            activeCamera.orthographic = true;
            activeCamera.orthographicSize = 5f;
            activeCamera.cullingMask = 0;

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                // A zero-scaled root canvas makes the complete menu hierarchy
                // active but invisible. Normalize only that invalid state.
                if (canvas.transform.localScale.sqrMagnitude < 0.0001f)
                {
                    canvas.transform.localScale = Vector3.one;
                }

                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
                canvas.targetDisplay = 0;
                canvas.pixelPerfect = false;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 5000;
            }

            EnsureMenuLayerCanvas();
        }

        private void EnsureMenuLayerCanvas()
        {
            if (menuLayerCanvas == null)
            {
                menuLayerCanvas = GetComponent<Canvas>();
            }

            if (menuLayerCanvas == null)
            {
                menuLayerCanvas = gameObject.AddComponent<Canvas>();
            }

            menuLayerCanvas.overrideSorting = true;
            menuLayerCanvas.sortingOrder = 6000;
            menuLayerCanvas.pixelPerfect = false;

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        private Camera GetActiveDisplayCamera()
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera candidate = cameras[i];
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.isActiveAndEnabled && candidate.targetDisplay == 0)
                {
                    return candidate;
                }
            }

            for (int i = 0; i < cameras.Length; i++)
            {
                Camera candidate = cameras[i];
                if (candidate == null || candidate.targetDisplay != 0)
                {
                    continue;
                }

                candidate.gameObject.SetActive(true);
                candidate.enabled = true;
                return candidate;
            }

            return Camera.main != null ? Camera.main : null;
        }

        private void EnsureOceanLogo()
        {
            RectTransform parentRect = transform as RectTransform;
            if (parentRect == null)
            {
                return;
            }

            Sprite logoSprite = Resources.Load<Sprite>(OceanLogoPath);
            if (logoSprite == null)
            {
                Debug.LogError($"Missing Ocean sprite at Resources path: {OceanLogoPath}");
                return;
            }

            DisableLegacyMenuLogo(parentRect);
            DisableLegacyMenuTitle(parentRect);

            if (oceanLogoImage == null)
            {
                Transform existing = parentRect.Find("OceanLogo");
                oceanLogoImage = existing == null ? null : existing.GetComponent<Image>();
            }

            if (oceanLogoImage == null)
            {
                GameObject logoObject = new GameObject("OceanLogo", typeof(RectTransform), typeof(Image));
                logoObject.transform.SetParent(parentRect, false);
                oceanLogoImage = logoObject.GetComponent<Image>();
            }

            oceanLogoImage.sprite = logoSprite;
            oceanLogoImage.color = Color.white;
            oceanLogoImage.type = Image.Type.Simple;
            oceanLogoImage.preserveAspect = true;
            oceanLogoImage.raycastTarget = false;

            RectTransform logoRect = oceanLogoImage.rectTransform;
            logoRect.anchorMin = new Vector2(0.005f, 0.7375f);
            logoRect.anchorMax = new Vector2(0.995f, 0.9625f);
            logoRect.offsetMin = Vector2.zero;
            logoRect.offsetMax = Vector2.zero;
            logoRect.pivot = new Vector2(0.5f, 0.5f);
            oceanLogoImage.transform.SetAsLastSibling();
        }

        private void HideOceanLogoForRewards()
        {
            if (oceanLogoImage == null)
            {
                EnsureOceanLogo();
            }

            if (oceanLogoImage == null)
            {
                return;
            }

            if (!rewardsLogoStateCaptured)
            {
                rewardsLogoWasActive = oceanLogoImage.gameObject.activeSelf;
                rewardsLogoStateCaptured = true;
            }

            oceanLogoImage.gameObject.SetActive(false);
        }

        private void RestoreOceanLogoAfterRewards()
        {
            if (!rewardsLogoStateCaptured)
            {
                return;
            }

            if (oceanLogoImage != null)
            {
                oceanLogoImage.gameObject.SetActive(rewardsLogoWasActive);
            }

            rewardsLogoStateCaptured = false;
        }

        private void StylePremiumMainMenu(SaveManager save)
        {
            EnsureMenuCameraAndCanvas();
            EnsureOceanBackground();
            EnsureOceanLogo();
            DisablePremiumMenuStackFrame();

            StyleMenuSpriteButton(classicButton, ClassicButtonPath, new Vector2(0.10f, 0.458f), new Vector2(0.90f, 0.553f));
            StyleMenuSpriteButton(blitzButton, BlitzButtonPath, new Vector2(0.10f, 0.351f), new Vector2(0.90f, 0.446f));
            StyleMenuSpriteButton(achievementsButton, RewardsButtonPath, new Vector2(0.10f, 0.244f), new Vector2(0.90f, 0.339f));
            StyleMenuSpriteButton(themeButton, ThemeButtonPath, new Vector2(0.10f, 0.137f), new Vector2(0.90f, 0.232f));
            SetButtonVisible(shopButton, false);
            EnsureMainMenuButtonColumn();
            SetButtonVisible(dailyButton, false);

            HideLegacyMainMenuObjects();
            HideLegacyMenuSceneArtifacts();
            HideMenuText(dailyInfoText);
            HideMenuText(themeHintText);
            HideMenuText(highScoresText);

            if (rankProgressView != null)
            {
                rankProgressView.gameObject.SetActive(false);
            }

            HideGameplaySceneObjects();
        }

        private void DisablePremiumMenuStackFrame()
        {
            Transform frame = transform.Find("PremiumMenuStackFrame");
            if (frame != null)
            {
                frame.gameObject.SetActive(false);
            }
        }

        private void HideLegacyMenuSceneArtifacts()
        {
            HideLegacyMainMenuObjects();
            DisableLegacyMenuPanelImages();

            Transform[] children = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child == null || child == transform || IsApprovedMainMenuElement(child) || IsMenuOverlayElement(child))
                {
                    continue;
                }

                if (IsGameplayName(child.name) || IsLegacyMenuArtifactName(child.name))
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private void DisableLegacyMenuPanelImages()
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image == null || IsApprovedMainMenuElement(image.transform) || IsMenuOverlayElement(image.transform))
                {
                    continue;
                }

                if (image.transform == transform || IsLegacyMenuPanelName(image.name) || IsGameplayName(image.name))
                {
                    image.enabled = false;
                    image.raycastTarget = false;
                }
            }
        }

        private bool IsApprovedMainMenuElement(Transform target)
        {
            if (target == null)
            {
                return false;
            }

            return IsTransformOrChildOf(target, oceanBackgroundImage == null ? null : oceanBackgroundImage.transform)
                || IsTransformOrChildOf(target, oceanLogoImage == null ? null : oceanLogoImage.transform)
                || IsTransformOrChildOf(target, classicButton == null ? null : classicButton.transform)
                || IsTransformOrChildOf(target, blitzButton == null ? null : blitzButton.transform)
                || IsTransformOrChildOf(target, dailyButton == null ? null : dailyButton.transform)
                || IsTransformOrChildOf(target, achievementsButton == null ? null : achievementsButton.transform)
                || IsTransformOrChildOf(target, themeButton == null ? null : themeButton.transform)
                || IsTransformOrChildOf(target, shopButton == null ? null : shopButton.transform);
        }

        private bool IsMenuOverlayElement(Transform target)
        {
            return IsTransformOrChildOf(target, settingsRoot == null ? null : settingsRoot.transform)
                || IsTransformOrChildOf(target, achievementsRoot == null ? null : achievementsRoot.transform)
                || IsTransformOrChildOf(target, shopRoot == null ? null : shopRoot.transform)
                || IsTransformOrChildOf(target, themesRoot == null ? null : themesRoot.transform);
        }

        private static bool IsTransformOrChildOf(Transform target, Transform root)
        {
            return target != null && root != null && (target == root || target.IsChildOf(root));
        }

        private static bool IsLegacyMenuArtifactName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return false;
            }

            return objectName.IndexOf("DailyGift", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("HighScores", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("RankProgress", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("Theme", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("Shop", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("NewClassic", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("Quit", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("Mission", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("Pure", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("Chain", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("Pop", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsLegacyMenuPanelName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return false;
            }

            return string.Equals(objectName, "Background", System.StringComparison.OrdinalIgnoreCase)
                || objectName.IndexOf("Panel", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("Progress", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("FillArea", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("Fill", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void HideGameplaySceneObjects()
        {
            HideGameplayComponents<BoardManager>();
            HideGameplayComponents<PieceSpawner>();
            HideGameplayComponents<GameHUD>();
            HideGameplayComponents<TraySlot>();
            HideGameplayCanvasObjects();
            HideGameplayNamedObjects();
        }

        private void HideGameplayComponents<T>() where T : Component
        {
            T[] components = FindObjectsByType<T>(FindObjectsInactive.Exclude);
            for (int i = 0; i < components.Length; i++)
            {
                T component = components[i];
                if (component == null)
                {
                    continue;
                }

                HideGameplayObject(component.gameObject);
            }
        }

        private void HideGameplayCanvasObjects()
        {
            Canvas menuCanvas = GetComponentInParent<Canvas>();
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null || canvas == menuCanvas)
                {
                    continue;
                }

                if (IsMenuRelated(canvas.transform))
                {
                    continue;
                }

                if (IsGameplayName(canvas.name))
                {
                    HideGameplayObject(canvas.gameObject);
                }
            }
        }

        private void HideGameplayNamedObjects()
        {
            Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Exclude);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform target = transforms[i];
                if (target == null || IsMenuRelated(target))
                {
                    continue;
                }

                if (IsGameplayName(target.name))
                {
                    HideGameplayObject(target.gameObject);
                }
            }
        }

        private void HideGameplayObject(GameObject target)
        {
            if (target == null || !target.activeSelf)
            {
                return;
            }

            if (!target.scene.IsValid() || IsMenuRelated(target.transform))
            {
                return;
            }

            if (!hiddenGameplayObjects.Contains(target))
            {
                hiddenGameplayObjects.Add(target);
            }

            target.SetActive(false);
        }

        private void RestoreGameplaySceneObjects()
        {
            for (int i = hiddenGameplayObjects.Count - 1; i >= 0; i--)
            {
                GameObject hidden = hiddenGameplayObjects[i];
                if (hidden != null)
                {
                    hidden.SetActive(true);
                }
            }

            hiddenGameplayObjects.Clear();
        }

        private bool IsMenuRelated(Transform target)
        {
            if (target == null)
            {
                return false;
            }

            return target == transform
                || target.IsChildOf(transform)
                || transform.IsChildOf(target);
        }

        private static bool IsGameplayName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return false;
            }

            return objectName.IndexOf("Board", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("Grid", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("Tray", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("GameHUD", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("Gameplay", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("ScorePanel", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("TopButton", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("OceanBoardFrame", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void StyleMenuSpriteButton(Button button, string spritePath, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.image != null ? button.image : button.GetComponent<Image>();
            if (image == null)
            {
                image = button.gameObject.AddComponent<Image>();
            }

            if (!string.IsNullOrEmpty(spritePath)
                && (image.sprite == null || IsLegacyMenuButtonSprite(image.sprite)))
            {
                image.sprite = LoadMenuSprite(spritePath);
            }

            image.material = null;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
            image.raycastTarget = true;
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.48f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
            button.transition = Selectable.Transition.None;

            if (mainMenuButtonColumn == null || button.transform.parent != mainMenuButtonColumn)
            {
                SetButtonRect(button, anchorMin, anchorMax);
            }
            HideButtonText(button);
            DisableGeneratedButtonDecor(button);
            EnsureButtonRaycastPath(button);
            button.gameObject.SetActive(true);
            button.transform.SetAsLastSibling();

            if (button.GetComponent<UIButtonFeedback>() == null)
            {
                button.gameObject.AddComponent<UIButtonFeedback>();
            }

            RefreshMainMenuButtonLayout();
        }

        private static bool IsLegacyMenuButtonSprite(Sprite sprite)
        {
            if (sprite == null || string.IsNullOrEmpty(sprite.name))
            {
                return true;
            }

            string spriteName = sprite.name;
            return spriteName.IndexOf("transparent_same_size", System.StringComparison.OrdinalIgnoreCase) >= 0
                || spriteName.IndexOf("rounded", System.StringComparison.OrdinalIgnoreCase) >= 0
                || spriteName.IndexOf("generated", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void StyleMenuUtilityButton(Button button, string fallbackLabel, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (button == null)
            {
                return;
            }

            SetButtonRect(button, anchorMin, anchorMax);
            Image image = button.image != null ? button.image : button.GetComponent<Image>();
            if (image == null)
            {
                image = button.gameObject.AddComponent<Image>();
            }

            if (image.sprite == null)
            {
                UISpriteFactory.ApplyRounded(image, 0.46f);
            }

            image.material = null;
            image.color = Color.white;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = true;
            button.targetGraphic = image;
            button.enabled = true;
            button.interactable = true;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
            button.transition = Selectable.Transition.None;

            DisableGeneratedButtonDecor(button);
            DisableNamedButtonDecor(button, "ThemeButtonGloss");
            DisableNamedButtonDecor(button, "ThemeButtonInnerRim");

            TMP_Text label = button == themeButton ? themeButtonText : button.GetComponentInChildren<TMP_Text>(true);
            if (label == null)
            {
                label = CreateRuntimeText($"{button.name}Label", button.transform, fallbackLabel, 40f, TextAlignmentOptions.Center);
            }

            if (button == themeButton)
            {
                themeButtonText = label;
            }

            label.gameObject.SetActive(true);
            label.text = fallbackLabel;
            label.color = Color.white;
            label.fontSize = 40f;
            label.fontSizeMax = 40f;
            label.fontSizeMin = 24f;
            label.fontStyle = FontStyles.Bold;
            label.raycastTarget = false;
            TMP_FontAsset premiumFont = Resources.Load<TMP_FontAsset>("Fonts/Fredoka-SemiBold SDF");
            if (premiumFont != null)
            {
                label.font = premiumFont;
            }

            Stretch(label.rectTransform, new Vector2(14f, 8f), new Vector2(-14f, -8f));
            label.transform.SetAsLastSibling();

            EnsureButtonRaycastPath(button);
            button.gameObject.SetActive(true);
            button.transform.SetAsLastSibling();
            if (button.GetComponent<UIButtonFeedback>() == null)
            {
                button.gameObject.AddComponent<UIButtonFeedback>();
            }

            if (button == themeButton)
            {
                WireThemeButton();
            }
        }

        private static void DisableNamedButtonDecor(Button button, string childName)
        {
            Transform child = button == null ? null : button.transform.Find(childName);
            if (child != null)
            {
                child.gameObject.SetActive(false);
            }
        }

        private static void EnsureButtonRaycastPath(Button button)
        {
            if (button == null)
            {
                return;
            }

            Image rootImage = button.image != null ? button.image : button.GetComponent<Image>();
            if (rootImage != null)
            {
                rootImage.enabled = true;
                rootImage.raycastTarget = true;
                button.targetGraphic = rootImage;
            }

            Graphic[] descendants = button.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                if (descendants[i] != null && descendants[i] != rootImage)
                {
                    descendants[i].raycastTarget = false;
                }
            }

            CanvasGroup[] groups = button.GetComponentsInChildren<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] == null)
                {
                    continue;
                }

                groups[i].alpha = 1f;
                groups[i].interactable = true;
                groups[i].blocksRaycasts = true;
            }
        }

        private void StyleThemeMenuButton()
        {
            if (themeButton == null)
            {
                return;
            }

            RectTransform rect = themeButton.transform as RectTransform;
            SetRect(rect, new Vector2(0.10f, 0.030f), new Vector2(0.90f, 0.125f), Vector2.zero, Vector2.zero);

            Image image = themeButton.image != null ? themeButton.image : themeButton.GetComponent<Image>();
            if (image == null)
            {
                image = themeButton.gameObject.AddComponent<Image>();
            }

            UISpriteFactory.ApplyRounded(image, 0.46f);
            ThemeAssetSet activeTheme = ThemeCatalog.Current;
            Color themeAccent = activeTheme == null ? new Color(0.16f, 0.78f, 1f, 1f) : activeTheme.CapsuleTintColor;
            image.color = Color.Lerp(new Color(0.015f, 0.12f, 0.30f, 1f), themeAccent, 0.46f);
            image.color = new Color(image.color.r, image.color.g, image.color.b, 0.98f);
            image.raycastTarget = true;
            themeButton.targetGraphic = image;
            themeButton.enabled = true;
            themeButton.interactable = true;

            ColorBlock colors = themeButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.94f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.78f, 0.92f, 1f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            colors.colorMultiplier = 1f;
            themeButton.colors = colors;

            Outline outline = themeButton.GetComponent<Outline>();
            if (outline == null)
            {
                outline = themeButton.gameObject.AddComponent<Outline>();
            }

            themeAccent.a = 0.76f;
            outline.effectColor = themeAccent;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;

            Shadow shadow = EnsureStandaloneShadow(themeButton.gameObject);
            shadow.effectColor = new Color(0f, 0.02f, 0.10f, 0.50f);
            shadow.effectDistance = new Vector2(0f, -5f);
            shadow.useGraphicAlpha = true;

            Image buttonGloss = EnsureThemeImage(themeButton.transform, "ThemeButtonGloss");
            SetRect(buttonGloss.rectTransform, new Vector2(0.025f, 0.50f), new Vector2(0.975f, 0.94f), Vector2.zero, Vector2.zero);
            UISpriteFactory.ApplyRounded(buttonGloss, 0.46f);
            buttonGloss.color = new Color(1f, 1f, 1f, 0.14f);
            buttonGloss.raycastTarget = false;
            buttonGloss.transform.SetAsFirstSibling();

            Image buttonRim = EnsureThemeImage(themeButton.transform, "ThemeButtonInnerRim");
            SetRect(buttonRim.rectTransform, Vector2.zero, Vector2.one, new Vector2(5f, 5f), new Vector2(-5f, -5f));
            UISpriteFactory.ApplyFrame(buttonRim, 0.46f, 0.035f);
            Color buttonRimColor = themeAccent;
            buttonRimColor.a = 0.38f;
            buttonRim.color = buttonRimColor;
            buttonRim.raycastTarget = false;
            buttonRim.transform.SetSiblingIndex(1);

            if (themeButtonText == null)
            {
                themeButtonText = themeButton.GetComponentInChildren<TMP_Text>(true);
            }

            if (themeButtonText == null)
            {
                themeButtonText = CreateRuntimeText("ThemeButtonLabel", themeButton.transform, "THEMES", 48f, TextAlignmentOptions.Center);
            }

            themeButtonText.gameObject.SetActive(true);
            themeButtonText.text = "THEMES";
            themeButtonText.color = Color.white;
            themeButtonText.fontSize = 48f;
            themeButtonText.fontSizeMax = 48f;
            themeButtonText.fontSizeMin = 30f;
            themeButtonText.fontStyle = FontStyles.Bold;
            themeButtonText.raycastTarget = false;
            TMP_FontAsset premiumFont = Resources.Load<TMP_FontAsset>("Fonts/Fredoka-SemiBold SDF");
            if (premiumFont != null)
            {
                themeButtonText.font = premiumFont;
            }

            EnsureTextShadow(themeButtonText, new Color(0f, 0.02f, 0.10f, 0.72f), new Vector2(0f, -2f));

            Stretch(themeButtonText.rectTransform, new Vector2(24f, 10f), new Vector2(-24f, -10f));
            themeButtonText.transform.SetAsLastSibling();
            themeButton.gameObject.SetActive(true);
            WireThemeButton();

            if (themeHintText != null)
            {
                themeHintText.gameObject.SetActive(false);
            }

            if (themeButton.GetComponent<UIButtonFeedback>() == null)
            {
                themeButton.gameObject.AddComponent<UIButtonFeedback>();
            }
        }

        private Sprite LoadMenuSprite(string resourcesPath)
        {
            Sprite sprite = Resources.Load<Sprite>(resourcesPath);
            if (sprite == null)
            {
                Debug.LogError($"Missing Ocean main menu sprite at Resources path: {resourcesPath}");
            }

            return sprite;
        }

        private void HideLegacyMainMenuObjects()
        {
            SetButtonVisible(newClassicButton, false);
            SetButtonVisible(themeButton, true);
            SetButtonVisible(shopButton, false);
            SetButtonVisible(quitButton, false);

            if (themeSwatches != null)
            {
                for (int i = 0; i < themeSwatches.Length; i++)
                {
                    if (themeSwatches[i] != null)
                    {
                        themeSwatches[i].gameObject.SetActive(false);
                    }
                }
            }
        }

        private void SetButtonVisible(Button button, bool visible)
        {
            if (button != null)
            {
                button.gameObject.SetActive(visible);
            }
        }

        private void HideButtonText(Button button)
        {
            if (button == null)
            {
                return;
            }

            TMP_Text[] tmpTexts = button.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < tmpTexts.Length; i++)
            {
                if (tmpTexts[i] != null)
                {
                    tmpTexts[i].gameObject.SetActive(false);
                }
            }

            Text[] uiTexts = button.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < uiTexts.Length; i++)
            {
                if (uiTexts[i] != null)
                {
                    uiTexts[i].gameObject.SetActive(false);
                }
            }
        }

        private void DisableGeneratedButtonDecor(Button button)
        {
            if (button == null)
            {
                return;
            }

            Transform gloss = button.transform.Find("OceanMenuButtonGloss");
            if (gloss != null)
            {
                gloss.gameObject.SetActive(false);
            }

            Shadow[] shadows = button.GetComponents<Shadow>();
            for (int i = 0; i < shadows.Length; i++)
            {
                if (shadows[i] != null)
                {
                    shadows[i].enabled = false;
                }
            }

            Outline[] outlines = button.GetComponents<Outline>();
            for (int i = 0; i < outlines.Length; i++)
            {
                if (outlines[i] != null)
                {
                    outlines[i].enabled = false;
                }
            }
        }

        private void HideMenuText(TMP_Text text)
        {
            if (text != null)
            {
                text.gameObject.SetActive(false);
            }
        }

        private void SetButtonRect(Button button, Vector2 anchorMin, Vector2 anchorMax)
        {
            RectTransform rect = button == null ? null : button.transform as RectTransform;
            if (rect != null)
            {
                SetRect(rect, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            }
        }

        private void DisableLegacyMenuLogo(RectTransform parentRect)
        {
            Transform legacyLogo = parentRect.Find("MenuLogo");
            if (legacyLogo != null)
            {
                legacyLogo.gameObject.SetActive(false);
            }

            Transform[] children = parentRect.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child != null && child.name.StartsWith("LogoBlock_", System.StringComparison.OrdinalIgnoreCase))
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private void DisableLegacyMenuTitle(RectTransform parentRect)
        {
            TMP_Text[] menuTexts = parentRect.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < menuTexts.Length; i++)
            {
                TMP_Text menuText = menuTexts[i];
                if (menuText == null)
                {
                    continue;
                }

                string normalizedText = NormalizeMenuTitle(menuText.text);
                bool title = string.Equals(normalizedText, "CHROMA BLAST", System.StringComparison.OrdinalIgnoreCase);
                bool prototypeTagline = normalizedText.IndexOf("PURE", System.StringComparison.OrdinalIgnoreCase) >= 0
                    && normalizedText.IndexOf("CHAIN", System.StringComparison.OrdinalIgnoreCase) >= 0
                    && normalizedText.IndexOf("POP", System.StringComparison.OrdinalIgnoreCase) >= 0;
                bool debugStats = normalizedText.IndexOf("MONEDE", System.StringComparison.OrdinalIgnoreCase) >= 0
                    && normalizedText.IndexOf("REALIZARI", System.StringComparison.OrdinalIgnoreCase) >= 0
                    && normalizedText.IndexOf("PURE", System.StringComparison.OrdinalIgnoreCase) >= 0;

                if (title || prototypeTagline || debugStats)
                {
                    menuText.gameObject.SetActive(false);
                }
            }
        }

        private void DisableCameraWarningTexts()
        {
            TMP_Text[] tmpTexts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
            for (int i = 0; i < tmpTexts.Length; i++)
            {
                DisableIfCameraWarning(tmpTexts[i]);
            }

            Text[] uiTexts = FindObjectsByType<Text>(FindObjectsInactive.Include);
            for (int i = 0; i < uiTexts.Length; i++)
            {
                Text text = uiTexts[i];
                if (text == null)
                {
                    continue;
                }

                string normalizedText = NormalizeMenuTitle(text.text);
                if (IsCameraWarningText(normalizedText))
                {
                    text.gameObject.SetActive(false);
                }
            }
        }

        private void DisableIfCameraWarning(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            string normalizedText = NormalizeMenuTitle(text.text);
            if (IsCameraWarningText(normalizedText))
            {
                text.gameObject.SetActive(false);
            }
        }

        private bool IsCameraWarningText(string normalizedText)
        {
            return normalizedText.IndexOf("Display", System.StringComparison.OrdinalIgnoreCase) >= 0
                && normalizedText.IndexOf("camera", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string NormalizeMenuTitle(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        }

        private void EnsureAchievementsUi()
        {
            if (achievementsButton == null && highScoresText != null && highScoresText.transform.parent != null)
            {
                achievementsButton = highScoresText.transform.parent.GetComponent<Button>();
                if (achievementsButton == null)
                {
                    achievementsButton = highScoresText.transform.parent.gameObject.AddComponent<Button>();
                    ColorBlock colors = achievementsButton.colors;
                    colors.normalColor = Color.white;
                    colors.highlightedColor = new Color(0.85f, 0.95f, 1f, 1f);
                    colors.pressedColor = new Color(0.45f, 0.85f, 1f, 1f);
                    colors.selectedColor = Color.white;
                    achievementsButton.colors = colors;
                }
            }

            if (!BindDailyRewardView())
            {
                return;
            }
        }

        private bool BindDailyRewardView()
        {
            if (dailyRewardView == null)
            {
                dailyRewardView = GetComponentInChildren<DailyRewardView>(true);
            }

            if (dailyRewardView == null)
            {
                DailyRewardView prefab = ResolveDailyRewardPrefab();
                if (prefab == null)
                {
                    achievementsRoot = null;
                    return false;
                }

                dailyRewardView = Instantiate(prefab, transform, false);
                dailyRewardView.gameObject.name = "AchievementsOverlay";

                RectTransform overlayRect = dailyRewardView.transform as RectTransform;
                if (overlayRect != null)
                {
                    overlayRect.anchorMin = Vector2.zero;
                    overlayRect.anchorMax = Vector2.one;
                    overlayRect.pivot = new Vector2(0.5f, 0.5f);
                    overlayRect.offsetMin = Vector2.zero;
                    overlayRect.offsetMax = Vector2.zero;
                    overlayRect.localScale = Vector3.one;
                    overlayRect.localRotation = Quaternion.identity;
                }
            }

            if (dailyRewardView == null || !dailyRewardView.HasCompleteBindings)
            {
                achievementsRoot = null;
                return false;
            }

            achievementsRoot = dailyRewardView.gameObject;
            achievementsCloseButton = dailyRewardView.CloseButton;
            dailyGiftButton = dailyRewardView.RewardedAdButton;
            rewardsCoinBalanceText = dailyRewardView.BalanceText;
            rewardsFeedbackText = dailyRewardView.FeedbackText;
            rewardsClaimButtonText = dailyRewardView.RewardedAdText;
            ApplyActiveDailyRewardVisualPolish();
            return true;
        }

        private void ApplyActiveDailyRewardVisualPolish()
        {
            dailyRewardView?.ApplyFinalVisualLayout();
        }

        private void EnsureDailyGiftUi()
        {
            BindDailyRewardView();
        }

        private void ConfigureRewardsPanel()
        {
            RectTransform panel = achievementsRoot == null
                ? null
                : achievementsRoot.transform.Find("AchievementsPanel") as RectTransform;
            if (panel == null)
            {
                return;
            }

            EnsureRewardSpritesLoaded();

            Image overlayImage = achievementsRoot.GetComponent<Image>();
            if (overlayImage != null)
            {
                overlayImage.color = new Color(0f, 0.025f, 0.09f, 0.58f);
            }

            SetRect(panel, new Vector2(0.04f, 0.065f), new Vector2(0.96f, 0.935f), Vector2.zero, Vector2.zero);

            Image panelImage = panel.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.sprite = null;
                UISpriteFactory.ApplyRounded(panelImage, 0.16f);
                panelImage.type = Image.Type.Sliced;
                panelImage.preserveAspect = false;
                panelImage.color = new Color(0.004f, 0.035f, 0.095f, 0.965f);
            }

            Outline legacyPanelOutline = panel.GetComponent<Outline>();
            if (legacyPanelOutline != null)
            {
                legacyPanelOutline.enabled = false;
            }

            Shadow panelShadow = EnsureStandaloneShadow(panel.gameObject);
            panelShadow.effectColor = new Color(0f, 0.015f, 0.06f, 0.50f);
            panelShadow.effectDistance = new Vector2(0f, -8f);
            panelShadow.useGraphicAlpha = true;

            Image panelBorder = EnsureRewardsImage(panel, "RewardsPanelBorder");
            Stretch(panelBorder.rectTransform, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            UISpriteFactory.ApplyFrame(panelBorder, 0.16f, 0.045f);
            panelBorder.type = Image.Type.Sliced;
            panelBorder.preserveAspect = false;
            panelBorder.color = new Color(0.36f, 0.96f, 1f, 0.49f);
            panelBorder.transform.SetSiblingIndex(1);

            DisableRewardsVisual(panel, "RewardsContentDimmer");
            DisableRewardsVisual(panel, "RewardsContentWell");
            DisableRewardsVisual(panel, "RewardsHeaderSheen");
            DisableRewardsVisual(panel, "RewardsHeaderDivider");

            DisableRewardsVisual(panel, "RewardsPanelTopTint");

            Image headerAreaImage = EnsureRewardsImage(panel, "HeaderArea");
            RectTransform headerArea = headerAreaImage.rectTransform;
            SetRect(headerArea, new Vector2(0.035f, 0.82f), new Vector2(0.965f, 0.975f), Vector2.zero, Vector2.zero);
            UISpriteFactory.ApplyRounded(headerAreaImage, 0.18f);
            headerAreaImage.color = new Color(0.006f, 0.060f, 0.13f, 0.78f);
            Outline headerOutline = headerAreaImage.GetComponent<Outline>();
            if (headerOutline != null)
            {
                headerOutline.enabled = false;
            }

            MoveRewardsChild(panel, headerArea, "AchievementsTitle");
            MoveRewardsChild(panel, headerArea, "RewardsBalancePill");

            TMP_Text title = headerArea.Find("AchievementsTitle")?.GetComponent<TMP_Text>();
            if (title != null)
            {
                title.text = "Daily Reward";
                title.color = new Color(1f, 0.99f, 0.96f, 1f);
                title.fontSize = 62f;
                title.fontSizeMax = 62f;
                title.fontSizeMin = 42f;
                title.fontStyle = FontStyles.Bold;
                title.characterSpacing = 1.5f;
                SetRect(title.rectTransform, new Vector2(0.14f, 0.48f), new Vector2(0.86f, 0.95f), Vector2.zero, Vector2.zero);

                Shadow titleShadow = title.GetComponent<Shadow>();
                if (titleShadow == null)
                {
                    titleShadow = title.gameObject.AddComponent<Shadow>();
                }

                titleShadow.effectColor = new Color(0f, 0.025f, 0.09f, 0.66f);
                titleShadow.effectDistance = new Vector2(0f, -3f);
                titleShadow.useGraphicAlpha = true;

                Outline titleGlow = title.GetComponent<Outline>();
                if (titleGlow == null)
                {
                    titleGlow = title.gameObject.AddComponent<Outline>();
                }

                titleGlow.effectColor = new Color(0.22f, 0.86f, 1f, 0.14f);
                titleGlow.effectDistance = new Vector2(1f, -1f);
                titleGlow.useGraphicAlpha = true;
            }

            if (achievementsListText != null)
            {
                achievementsListText.gameObject.SetActive(false);
            }

            Image balancePill = EnsureRewardsImage(headerArea, "RewardsBalancePill");
            SetRect(balancePill.rectTransform, new Vector2(0.285f, 0.035f), new Vector2(0.715f, 0.415f), Vector2.zero, Vector2.zero);
            UISpriteFactory.ApplyRounded(balancePill, 0.48f);
            balancePill.color = new Color(0.008f, 0.18f, 0.31f, 0.99f);
            Outline balanceOutline = balancePill.GetComponent<Outline>();
            if (balanceOutline == null)
            {
                balanceOutline = balancePill.gameObject.AddComponent<Outline>();
            }

            balanceOutline.enabled = true;
            balanceOutline.effectColor = new Color(0.42f, 0.96f, 1f, 0.56f);
            balanceOutline.effectDistance = new Vector2(1f, -1f);
            balanceOutline.useGraphicAlpha = true;
            Shadow balanceShadow = EnsureStandaloneShadow(balancePill.gameObject);
            balanceShadow.effectColor = new Color(0f, 0.02f, 0.08f, 0.38f);
            balanceShadow.effectDistance = new Vector2(0f, -2f);
            balanceShadow.useGraphicAlpha = true;
            balancePill.transform.SetAsFirstSibling();

            MoveRewardsChild(headerArea, balancePill.rectTransform, "RewardsBalanceGloss");
            Image balanceGloss = EnsureRewardsImage(balancePill.rectTransform, "RewardsBalanceGloss");
            SetRect(balanceGloss.rectTransform, new Vector2(0.03f, 0.56f), new Vector2(0.97f, 0.93f), Vector2.zero, Vector2.zero);
            UISpriteFactory.ApplyRounded(balanceGloss, 0.48f);
            balanceGloss.color = new Color(0.86f, 1f, 1f, 0.16f);
            balanceGloss.raycastTarget = false;
            balanceGloss.transform.SetAsFirstSibling();

            RectTransform balanceContent = EnsureRewardsContainer(balancePill.rectTransform, "RewardsBalanceContent");
            SetRect(balanceContent, new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.94f), Vector2.zero, Vector2.zero);
            HorizontalLayoutGroup balanceLayout = balanceContent.GetComponent<HorizontalLayoutGroup>();
            if (balanceLayout == null)
            {
                balanceLayout = balanceContent.gameObject.AddComponent<HorizontalLayoutGroup>();
            }

            balanceLayout.padding = new RectOffset(10, 10, 6, 6);
            balanceLayout.spacing = 18f;
            balanceLayout.childAlignment = TextAnchor.MiddleCenter;
            balanceLayout.childControlWidth = false;
            balanceLayout.childControlHeight = false;
            balanceLayout.childForceExpandWidth = false;
            balanceLayout.childForceExpandHeight = false;
            balanceContent.SetAsLastSibling();

            MoveRewardsChild(headerArea, balanceContent, "RewardsBalanceCoin");
            MoveRewardsChild(headerArea, balanceContent, "RewardsCoinBalance");
            Image balanceCoin = EnsureRewardsImage(balanceContent, "RewardsBalanceCoin");
            RectTransform balanceCoinRect = balanceCoin.rectTransform;
            balanceCoinRect.anchorMin = new Vector2(0.5f, 0.5f);
            balanceCoinRect.anchorMax = balanceCoinRect.anchorMin;
            balanceCoinRect.pivot = new Vector2(0.5f, 0.5f);
            balanceCoinRect.anchoredPosition = Vector2.zero;
            balanceCoinRect.sizeDelta = new Vector2(74f, 74f);
            if (rewardCoinsIconSprite != null)
            {
                balanceCoin.sprite = rewardCoinsIconSprite;
            }

            balanceCoin.type = Image.Type.Simple;
            balanceCoin.preserveAspect = true;
            balanceCoin.color = Color.white;
            Shadow balanceCoinShadow = EnsureStandaloneShadow(balanceCoin.gameObject);
            balanceCoinShadow.effectColor = new Color(0f, 0.035f, 0.09f, 0.34f);
            balanceCoinShadow.effectDistance = new Vector2(0f, -2f);
            balanceCoinShadow.useGraphicAlpha = true;
            LayoutElement balanceCoinLayout = balanceCoin.GetComponent<LayoutElement>();
            if (balanceCoinLayout == null)
            {
                balanceCoinLayout = balanceCoin.gameObject.AddComponent<LayoutElement>();
            }

            balanceCoinLayout.minWidth = 74f;
            balanceCoinLayout.preferredWidth = 74f;
            balanceCoinLayout.minHeight = 74f;
            balanceCoinLayout.preferredHeight = 74f;
            balanceCoinLayout.flexibleWidth = 0f;
            balanceCoinLayout.flexibleHeight = 0f;

            rewardsCoinBalanceText = EnsureRewardsText(balanceContent, "RewardsCoinBalance", "0", 47f, TextAlignmentOptions.Center);
            rewardsCoinBalanceText.text = SaveManager.Instance == null
                ? "0"
                : SaveManager.Instance.GetCoins().ToString();
            RectTransform balanceTextRect = rewardsCoinBalanceText.rectTransform;
            balanceTextRect.anchorMin = new Vector2(0.5f, 0.5f);
            balanceTextRect.anchorMax = balanceTextRect.anchorMin;
            balanceTextRect.pivot = new Vector2(0.5f, 0.5f);
            balanceTextRect.anchoredPosition = Vector2.zero;
            balanceTextRect.sizeDelta = new Vector2(150f, 82f);
            rewardsCoinBalanceText.fontSize = 47f;
            rewardsCoinBalanceText.fontSizeMax = 47f;
            rewardsCoinBalanceText.fontSizeMin = 35f;
            rewardsCoinBalanceText.color = new Color(1f, 0.88f, 0.48f, 1f);
            LayoutElement balanceTextLayout = rewardsCoinBalanceText.GetComponent<LayoutElement>();
            if (balanceTextLayout == null)
            {
                balanceTextLayout = rewardsCoinBalanceText.gameObject.AddComponent<LayoutElement>();
            }

            balanceTextLayout.minWidth = 96f;
            balanceTextLayout.preferredWidth = 150f;
            balanceTextLayout.minHeight = 72f;
            balanceTextLayout.preferredHeight = 82f;
            balanceTextLayout.flexibleWidth = 0f;
            balanceTextLayout.flexibleHeight = 0f;
            Shadow balanceTextShadow = EnsureStandaloneShadow(rewardsCoinBalanceText.gameObject);
            balanceTextShadow.effectColor = new Color(0f, 0.02f, 0.07f, 0.62f);
            balanceTextShadow.effectDistance = new Vector2(0f, -2f);
            balanceTextShadow.useGraphicAlpha = true;

            rewardsFeedbackText = EnsureRewardsText(panel, "RewardsClaimFeedback", string.Empty, 27f, TextAlignmentOptions.Center);
            SetRect(rewardsFeedbackText.rectTransform, new Vector2(0.18f, 0.132f), new Vector2(0.82f, 0.158f), Vector2.zero, Vector2.zero);
            rewardsFeedbackText.color = new Color(0.42f, 0.95f, 1f, 1f);
            rewardsFeedbackText.gameObject.SetActive(false);

            EnsureRewardCards(panel);
            RefreshRewardCardStates(SaveManager.Instance);
            EnsureDailyGiftUi();
            ConfigureRewardsCloseButton(panel);
        }

        private TMP_Text EnsureRewardsText(RectTransform parent, string objectName, string value, float fontSize, TextAlignmentOptions alignment)
        {
            TMP_Text text = parent.Find(objectName)?.GetComponent<TMP_Text>();
            if (text == null)
            {
                text = CreateRuntimeText(objectName, parent, value, fontSize, alignment);
            }

            text.fontSize = fontSize;
            text.fontSizeMax = fontSize;
            text.fontSizeMin = Mathf.Max(14f, fontSize * 0.68f);
            text.fontStyle = FontStyles.Bold;
            text.alignment = alignment;
            return text;
        }

        private Image EnsureRewardsImage(RectTransform parent, string objectName)
        {
            Image image = parent.Find(objectName)?.GetComponent<Image>();
            if (image == null)
            {
                RectTransform decoration = CreateRuntimePanel(objectName, parent, Color.clear);
                image = decoration.GetComponent<Image>();
            }

            image.gameObject.SetActive(true);
            image.enabled = true;
            image.raycastTarget = false;
            return image;
        }

        private void DisableRewardsVisual(RectTransform panel, string objectName)
        {
            Transform visual = FindRewardsDescendant(panel, objectName);
            if (visual != null)
            {
                visual.gameObject.SetActive(false);
            }
        }

        private void MoveRewardsChild(RectTransform searchRoot, RectTransform destination, string objectName)
        {
            Transform child = FindRewardsDescendant(searchRoot, objectName);
            if (child != null && child != destination && child.parent != destination)
            {
                child.SetParent(destination, false);
            }
        }

        private Transform FindRewardsDescendant(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }

            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                if (descendants[i] != null && descendants[i].name == objectName)
                {
                    return descendants[i];
                }
            }

            return null;
        }

        private void EnsureRewardSpritesLoaded()
        {
            if (rewardSpritesLoaded)
            {
                return;
            }

            rewardSpritesLoaded = true;
            rewardCoinsIconSprite = Resources.Load<Sprite>(RewardsCoinsIconPath);
            rewardClosedChestIconSprite = Resources.Load<Sprite>(RewardsClosedChestIconPath);
            rewardGrandIconSprite = Resources.Load<Sprite>(RewardsGrandIconPath);
            rewardPearlIconSprite = Resources.Load<Sprite>(RewardsPearlIconPath);
            rewardPowerupIconSprite = Resources.Load<Sprite>(RewardsPowerupIconPath);
            rewardClaimActiveSprite = Resources.Load<Sprite>(RewardsClaimActivePath);
            rewardClaimDisabledSprite = Resources.Load<Sprite>(RewardsClaimDisabledPath);

            WarnIfRewardSpriteMissing(rewardCoinsIconSprite, RewardsCoinsIconPath);
            WarnIfRewardSpriteMissing(rewardClosedChestIconSprite, RewardsClosedChestIconPath);
            WarnIfRewardSpriteMissing(rewardGrandIconSprite, RewardsGrandIconPath);
            WarnIfRewardSpriteMissing(rewardPearlIconSprite, RewardsPearlIconPath);
            WarnIfRewardSpriteMissing(rewardPowerupIconSprite, RewardsPowerupIconPath);
        }

        private void WarnIfRewardSpriteMissing(Sprite sprite, string resourcesPath)
        {
            if (sprite == null)
            {
                Debug.LogWarning($"Daily Rewards sprite is missing at Resources path '{resourcesPath}'.");
            }
        }

        private Sprite GetRewardIconSprite(int index)
        {
            EnsureRewardSpritesLoaded();
            if (index < SaveManager.DailyRewardDayCount - 1)
            {
                return rewardCoinsIconSprite;
            }

            return rewardGrandIconSprite;
        }

        private Vector2 GetRewardArtworkSize(int index)
        {
            if (index == SaveManager.DailyRewardDayCount - 1)
            {
                return new Vector2(252f, 252f);
            }

            if (index == 2)
            {
                return new Vector2(124f, 124f);
            }

            if (index == 3)
            {
                return new Vector2(122f, 122f);
            }

            if (index == 4 || index == 5)
            {
                return new Vector2(132f, 132f);
            }

            return new Vector2(110f, 110f);
        }

        private Shadow EnsureStandaloneShadow(GameObject target)
        {
            Shadow[] effects = target.GetComponents<Shadow>();
            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i] != null && effects[i].GetType() == typeof(Shadow))
                {
                    return effects[i];
                }
            }

            return target.AddComponent<Shadow>();
        }

        private void EnsureRewardCards(RectTransform panel)
        {
            rewardsCardsRoot = panel.Find("RewardsCards") as RectTransform;
            if (rewardsCardsRoot == null)
            {
                GameObject cardsObject = new GameObject("RewardsCards", typeof(RectTransform));
                cardsObject.transform.SetParent(panel, false);
                rewardsCardsRoot = cardsObject.transform as RectTransform;
            }

            SetRect(rewardsCardsRoot, new Vector2(0.05f, 0.16f), new Vector2(0.95f, 0.815f), Vector2.zero, Vector2.zero);
            rewardsNormalCardsRoot = EnsureRewardsContainer(rewardsCardsRoot, "NormalCardsContainer");
            SetRect(rewardsNormalCardsRoot, new Vector2(0f, 0.42f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            rewardsRowOne = EnsureRewardsContainer(rewardsNormalCardsRoot, "Row1");
            SetRect(rewardsRowOne, new Vector2(0f, 0.52f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            rewardsRowTwo = EnsureRewardsContainer(rewardsNormalCardsRoot, "Row2");
            SetRect(rewardsRowTwo, new Vector2(0f, 0f), new Vector2(1f, 0.48f), Vector2.zero, Vector2.zero);

            DisableUnexpectedRewardCards();

            rewardCardImages = new Image[SaveManager.DailyRewardDayCount];
            rewardCardButtons = new Button[SaveManager.DailyRewardDayCount];
            rewardCardOutlines = new Outline[SaveManager.DailyRewardDayCount];
            rewardCardAccentImages = new Image[SaveManager.DailyRewardDayCount];
            rewardCardCoinImages = new Image[SaveManager.DailyRewardDayCount];
            rewardCardLockedOverlays = new Image[SaveManager.DailyRewardDayCount];
            rewardCardStateBadges = new Image[SaveManager.DailyRewardDayCount];
            rewardCardDayTexts = new TMP_Text[SaveManager.DailyRewardDayCount];
            rewardCardAmountTexts = new TMP_Text[SaveManager.DailyRewardDayCount];
            rewardCardStateTexts = new TMP_Text[SaveManager.DailyRewardDayCount];

            for (int i = 0; i < SaveManager.DailyRewardDayCount; i++)
            {
                RectTransform card = FindRewardCard(i);
                if (card == null)
                {
                    card = CreateRewardCard(i);
                }

                card.gameObject.SetActive(true);
                PositionRewardCard(card, i);
                DisableObsoleteRewardCardChildren(card);
                rewardCardImages[i] = card.GetComponent<Image>();
                if (rewardCardImages[i] == null)
                {
                    rewardCardImages[i] = card.gameObject.AddComponent<Image>();
                }

                UISpriteFactory.ApplyRounded(rewardCardImages[i], 0.22f);
                rewardCardImages[i].raycastTarget = true;
                rewardCardButtons[i] = card.GetComponent<Button>();
                if (rewardCardButtons[i] == null)
                {
                    rewardCardButtons[i] = card.gameObject.AddComponent<Button>();
                }

                rewardCardButtons[i].targetGraphic = rewardCardImages[i];
                rewardCardButtons[i].transition = Selectable.Transition.ColorTint;
                rewardCardButtons[i].navigation = new Navigation { mode = Navigation.Mode.None };
                ColorBlock cardButtonColors = rewardCardButtons[i].colors;
                cardButtonColors.normalColor = Color.white;
                cardButtonColors.highlightedColor = Color.white;
                cardButtonColors.pressedColor = new Color(0.84f, 0.94f, 1f, 1f);
                cardButtonColors.selectedColor = Color.white;
                cardButtonColors.disabledColor = Color.white;
                cardButtonColors.colorMultiplier = 1f;
                cardButtonColors.fadeDuration = 0.06f;
                rewardCardButtons[i].colors = cardButtonColors;
                if (card.GetComponent<RectMask2D>() == null)
                {
                    card.gameObject.AddComponent<RectMask2D>();
                }

                rewardCardOutlines[i] = card.GetComponent<Outline>();
                if (rewardCardOutlines[i] == null)
                {
                    rewardCardOutlines[i] = card.gameObject.AddComponent<Outline>();
                }

                rewardCardOutlines[i].useGraphicAlpha = true;
                Shadow cardShadow = EnsureStandaloneShadow(card.gameObject);
                cardShadow.effectColor = new Color(0f, 0.02f, 0.075f, 0.40f);
                cardShadow.effectDistance = new Vector2(0f, -3f);
                cardShadow.useGraphicAlpha = true;
                bool finalCard = i == SaveManager.DailyRewardDayCount - 1;
                rewardCardDayTexts[i] = card.Find("Day")?.GetComponent<TMP_Text>();
                if (rewardCardDayTexts[i] == null)
                {
                    rewardCardDayTexts[i] = CreateRuntimeText("Day", card, $"Day {i + 1}", 36f, TextAlignmentOptions.Center);
                }

                rewardCardAmountTexts[i] = card.Find("Amount")?.GetComponent<TMP_Text>();
                if (rewardCardAmountTexts[i] == null && finalCard)
                {
                    rewardCardAmountTexts[i] = card.Find("Day7RewardValue/RewardAmountText")?.GetComponent<TMP_Text>();
                }

                if (rewardCardAmountTexts[i] == null)
                {
                    rewardCardAmountTexts[i] = CreateRuntimeText("Amount", card, SaveManager.GetDailyRewardAmountForDay(i).ToString(), 50f, TextAlignmentOptions.Center);
                }

                rewardCardStateTexts[i] = card.Find("StateArea/State")?.GetComponent<TMP_Text>();
                if (rewardCardStateTexts[i] == null)
                {
                    rewardCardStateTexts[i] = card.Find("State")?.GetComponent<TMP_Text>();
                }

                if (rewardCardStateTexts[i] == null)
                {
                    rewardCardStateTexts[i] = CreateRuntimeText("State", card, "LOCKED", 30f, TextAlignmentOptions.Center);
                }

                ConfigureRewardCardText(rewardCardDayTexts[i], finalCard ? 44f : 38f, finalCard ? 32f : 28f);
                ConfigureRewardCardText(rewardCardAmountTexts[i], finalCard ? 62f : 53f, finalCard ? 45f : 37f);
                ConfigureRewardCardText(rewardCardStateTexts[i], finalCard ? 42f : 29f, finalCard ? 30f : 24f);
                if (rewardCardAmountTexts[i] != null)
                {
                    rewardCardAmountTexts[i].text = SaveManager.GetDailyRewardAmountForDay(i).ToString();
                    if (finalCard)
                    {
                        rewardCardAmountTexts[i].characterSpacing = 0.25f;
                        Shadow amountShadow = EnsureStandaloneShadow(rewardCardAmountTexts[i].gameObject);
                        amountShadow.effectColor = new Color(0.14f, 0.05f, 0f, 0.58f);
                        amountShadow.effectDistance = new Vector2(0f, -1.8f);
                        amountShadow.useGraphicAlpha = true;
                    }
                }

                if (rewardCardDayTexts[i] != null)
                {
                    rewardCardDayTexts[i].text = $"Day {i + 1}";
                }

                Image accent = EnsureRewardsImage(card, "Accent");
                SetRect(accent.rectTransform, new Vector2(0.14f, 0.955f), new Vector2(0.86f, 0.975f), Vector2.zero, Vector2.zero);
                UISpriteFactory.ApplyRounded(accent, 1f);
                accent.transform.SetAsFirstSibling();
                accent.gameObject.SetActive(!finalCard);
                rewardCardAccentImages[i] = accent;

                Image innerHighlight = EnsureRewardsImage(card, "InnerHighlight");
                SetRect(innerHighlight.rectTransform, new Vector2(0.035f, 0.43f), new Vector2(0.965f, 0.96f), Vector2.zero, Vector2.zero);
                UISpriteFactory.ApplyRounded(innerHighlight, 0.22f);
                innerHighlight.color = finalCard
                    ? new Color(0.02f, 0.22f, 0.32f, 0.025f)
                    : new Color(0.48f, 0.94f, 1f, 0.045f);
                innerHighlight.transform.SetSiblingIndex(1);

                Image lockedOverlay = EnsureRewardsImage(card, "LockedOverlay");
                Stretch(lockedOverlay.rectTransform, new Vector2(2f, 2f), new Vector2(-2f, -2f));
                UISpriteFactory.ApplyRounded(lockedOverlay, 0.22f);
                lockedOverlay.color = Color.clear;
                lockedOverlay.transform.SetSiblingIndex(2);
                rewardCardLockedOverlays[i] = lockedOverlay;

                Image stateBadge = card.Find("StateArea")?.GetComponent<Image>();
                if (stateBadge == null)
                {
                    stateBadge = EnsureRewardsImage(card, "StateBadge");
                }

                SetRect(stateBadge.rectTransform, new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.285f), Vector2.zero, Vector2.zero);
                UISpriteFactory.ApplyRounded(stateBadge, 0.48f);
                if (rewardCardStateTexts[i].transform.parent == stateBadge.transform)
                {
                    stateBadge.transform.SetAsLastSibling();
                }
                else
                {
                    stateBadge.transform.SetSiblingIndex(Mathf.Max(0, rewardCardStateTexts[i].transform.GetSiblingIndex()));
                }

                rewardCardStateTexts[i].transform.SetAsLastSibling();
                rewardCardStateBadges[i] = stateBadge;

                RectTransform coin = FindRewardArtwork(card);
                if (coin == null)
                {
                    GameObject coinObject = new GameObject("Coin", typeof(RectTransform), typeof(Image));
                    coinObject.transform.SetParent(card, false);
                    coin = coinObject.transform as RectTransform;
                }

                coin.anchorMin = new Vector2(0.5f, 0.635f);
                coin.anchorMax = coin.anchorMin;
                coin.pivot = new Vector2(0.5f, 0.5f);
                coin.anchoredPosition = Vector2.zero;
                coin.sizeDelta = GetRewardArtworkSize(i);
                Image coinImage = coin.GetComponent<Image>();
                if (coinImage == null)
                {
                    coinImage = coin.gameObject.AddComponent<Image>();
                }

                Sprite rewardIcon = GetRewardIconSprite(i);
                if (rewardIcon != null)
                {
                    coinImage.sprite = rewardIcon;
                }

                coinImage.type = Image.Type.Simple;
                coinImage.preserveAspect = true;
                coinImage.color = Color.white;
                coinImage.raycastTarget = false;
                rewardCardCoinImages[i] = coinImage;
                if (finalCard)
                {
                    EnsureDaySevenArtworkViewport(card, coin);
                }

                RemoveRewardStateSymbol(card, "LockStateIcon");
                RemoveRewardStateSymbol(card, "ClaimedStateIcon");
                rewardCardDayTexts[i].transform.SetAsLastSibling();
                rewardCardAmountTexts[i].transform.SetAsLastSibling();
                rewardCardStateTexts[i].transform.SetAsLastSibling();
                ConfigureRewardCardContentLayout(
                    i,
                    rewardCardDayTexts[i].rectTransform,
                    rewardCardAmountTexts[i].rectTransform,
                    rewardCardStateTexts[i].rectTransform,
                    coin,
                    null,
                    stateBadge.rectTransform);
                if (finalCard)
                {
                    ConfigureDaySevenRewardValue(card, rewardCardAmountTexts[i]);
                }
            }

            ApplyDayOneArtworkTemplate();
        }

        private void DisableUnexpectedRewardCards()
        {
            if (rewardsCardsRoot == null)
            {
                return;
            }

            HashSet<int> activeDays = new HashSet<int>();
            RectTransform[] descendants = rewardsCardsRoot.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                Transform child = descendants[i];
                if (child == null)
                {
                    continue;
                }

                bool rewardCardName = child.name.StartsWith("RewardCard", System.StringComparison.Ordinal);
                if (!rewardCardName)
                {
                    continue;
                }

                string suffix = rewardCardName ? child.name.Substring("RewardCard".Length) : string.Empty;
                bool validDay = rewardCardName
                    && int.TryParse(suffix, out int day)
                    && day >= 1
                    && day <= SaveManager.DailyRewardDayCount
                    && activeDays.Add(day);
                child.gameObject.SetActive(validDay);
            }
        }

        private RectTransform EnsureRewardsContainer(RectTransform parent, string objectName)
        {
            RectTransform container = parent.Find(objectName) as RectTransform;
            if (container == null)
            {
                GameObject containerObject = new GameObject(objectName, typeof(RectTransform));
                containerObject.transform.SetParent(parent, false);
                container = containerObject.transform as RectTransform;
            }

            container.gameObject.SetActive(true);
            return container;
        }

        private RectTransform FindRewardCard(int index)
        {
            if (rewardsCardsRoot == null)
            {
                return null;
            }

            string objectName = $"RewardCard{index + 1}";
            RectTransform[] descendants = rewardsCardsRoot.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                if (descendants[i] != null && descendants[i].name == objectName && descendants[i].gameObject.activeSelf)
                {
                    return descendants[i];
                }
            }

            return null;
        }

        private RectTransform FindRewardArtwork(RectTransform card)
        {
            if (card == null)
            {
                return null;
            }

            RectTransform directArtwork = card.Find("Coin") as RectTransform;
            if (directArtwork != null)
            {
                return directArtwork;
            }

            if (card.name == "RewardCard7")
            {
                RectTransform prefabArtwork = card.Find("ArtworkViewport/Artwork") as RectTransform;
                if (prefabArtwork != null)
                {
                    return prefabArtwork;
                }
            }

            Transform nestedArtwork = FindRewardsDescendant(card, "Coin");
            return nestedArtwork as RectTransform;
        }

        private void EnsureDaySevenArtworkViewport(RectTransform card, RectTransform artwork)
        {
            if (card == null || artwork == null)
            {
                return;
            }

            RectTransform viewport = card.Find("Day7ArtworkViewport") as RectTransform;
            if (viewport == null)
            {
                GameObject viewportObject = new GameObject("Day7ArtworkViewport", typeof(RectTransform), typeof(RectMask2D));
                viewportObject.transform.SetParent(card, false);
                viewport = viewportObject.transform as RectTransform;
            }

            viewport.gameObject.SetActive(true);
            SetRect(viewport, new Vector2(0.20f, 0.33f), new Vector2(0.80f, 0.98f), Vector2.zero, Vector2.zero);
            viewport.SetSiblingIndex(Mathf.Min(3, card.childCount - 1));
            if (artwork.parent != viewport)
            {
                artwork.SetParent(viewport, false);
            }

            artwork.anchorMin = new Vector2(0.5f, 0.5f);
            artwork.anchorMax = artwork.anchorMin;
            artwork.pivot = new Vector2(0.5f, 0.5f);
            artwork.anchoredPosition = Vector2.zero;
            artwork.sizeDelta = GetRewardArtworkSize(SaveManager.DailyRewardDayCount - 1);
            artwork.localScale = Vector3.one;
            artwork.localRotation = Quaternion.identity;
        }

        private void ConfigureDaySevenRewardValue(RectTransform card, TMP_Text amount)
        {
            if (card == null || amount == null)
            {
                return;
            }

            if (amount.transform.parent != card)
            {
                amount.transform.SetParent(card, false);
            }

            Transform obsoleteValueGroup = card.Find("Day7RewardValue");
            if (obsoleteValueGroup != null)
            {
                obsoleteValueGroup.gameObject.SetActive(false);
                Destroy(obsoleteValueGroup.gameObject);
            }

            amount.gameObject.name = "Amount";
            RectTransform amountRect = amount.rectTransform;
            SetRect(amountRect, new Vector2(0.18f, 0.19f), new Vector2(0.82f, 0.485f), Vector2.zero, Vector2.zero);
            amountRect.anchoredPosition = new Vector2(0f, -4f);
            amount.text = SaveManager.GetDailyRewardAmountForDay(SaveManager.DailyRewardDayCount - 1).ToString();
            amount.fontSize = 64f;
            amount.fontSizeMax = 64f;
            amount.fontSizeMin = 64f;
            amount.enableAutoSizing = false;
            amount.fontStyle = FontStyles.Bold;
            amount.characterSpacing = 0.25f;
            amount.alignment = TextAlignmentOptions.Center;
            amount.overflowMode = TextOverflowModes.Overflow;
            amount.color = new Color(1f, 0.81f, 0.35f, 1f);
            amount.raycastTarget = false;
            LayoutElement legacyAmountLayout = amount.GetComponent<LayoutElement>();
            if (legacyAmountLayout != null)
            {
                legacyAmountLayout.ignoreLayout = true;
            }
            Shadow amountShadow = EnsureStandaloneShadow(amount.gameObject);
            amountShadow.effectColor = new Color(0.14f, 0.05f, 0f, 0.58f);
            amountShadow.effectDistance = new Vector2(0f, -1.8f);
            amountShadow.useGraphicAlpha = true;

            amount.transform.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(card);
        }

        private void RemoveRewardStateSymbol(RectTransform card, string objectName)
        {
            Transform symbol = card.Find(objectName);
            if (symbol == null)
            {
                return;
            }

            symbol.gameObject.SetActive(false);
            Destroy(symbol.gameObject);
        }

        private void ApplyDayOneArtworkTemplate()
        {
            if (rewardCardCoinImages == null
                || rewardCardCoinImages.Length < 6
                || rewardCardCoinImages[0] == null)
            {
                return;
            }

            RectTransform template = rewardCardCoinImages[0].rectTransform;
            int templateSiblingIndex = template.GetSiblingIndex();
            for (int i = 1; i < 6; i++)
            {
                Image artwork = rewardCardCoinImages[i];
                if (artwork == null)
                {
                    continue;
                }

                RectTransform target = artwork.rectTransform;
                target.anchorMin = template.anchorMin;
                target.anchorMax = template.anchorMax;
                target.pivot = template.pivot;
                target.anchoredPosition = template.anchoredPosition;
                target.sizeDelta = GetRewardArtworkSize(i);
                target.localScale = template.localScale;
                target.localRotation = template.localRotation;
                target.SetSiblingIndex(Mathf.Min(templateSiblingIndex, target.parent.childCount - 1));
                artwork.preserveAspect = rewardCardCoinImages[0].preserveAspect;
            }
        }

        private void DisableObsoleteRewardCardChildren(RectTransform card)
        {
            for (int i = 0; i < card.childCount; i++)
            {
                Transform child = card.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                bool approved = child.name == "Day"
                    || child.name == "Coin"
                    || child.name == "Amount"
                    || child.name == "State"
                    || child.name == "StateArea"
                    || child.name == "Accent"
                    || child.name == "InnerHighlight"
                    || child.name == "LockedOverlay"
                    || child.name == "StateBadge"
                    || child.name == "Day7ArtworkViewport"
                    || child.name == "Day7RewardValue";
                child.gameObject.SetActive(approved);
            }
        }

        private void ConfigureRewardCardText(TMP_Text text, float size, float minimumSize)
        {
            if (text == null)
            {
                return;
            }

            text.fontSize = size;
            text.fontSizeMax = size;
            text.fontSizeMin = minimumSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            Shadow textShadow = EnsureStandaloneShadow(text.gameObject);
            textShadow.effectColor = new Color(0f, 0.02f, 0.075f, 0.52f);
            textShadow.effectDistance = new Vector2(0f, -1.5f);
            textShadow.useGraphicAlpha = true;
        }

        private void ConfigureRewardCardContentLayout(
            int index,
            RectTransform day,
            RectTransform amount,
            RectTransform state,
            RectTransform artwork,
            RectTransform lockIcon,
            RectTransform stateBadge)
        {
            bool finalCard = index == SaveManager.DailyRewardDayCount - 1;
            RectTransform stateSurface = state.parent is RectTransform stateParent && stateParent.name == "StateArea"
                ? stateParent
                : state;
            bool stateUsesArea = stateSurface != state;
            if (!finalCard)
            {
                SetRect(day, new Vector2(0.06f, 0.81f), new Vector2(0.94f, 0.96f), Vector2.zero, Vector2.zero);
                SetRect(amount, new Vector2(0.06f, 0.25f), new Vector2(0.94f, 0.43f), Vector2.zero, Vector2.zero);
                SetRect(stateSurface, new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.22f), Vector2.zero, Vector2.zero);
                if (stateUsesArea)
                {
                    Stretch(state, Vector2.zero, Vector2.zero);
                }

                if (stateBadge != null && stateBadge != stateSurface)
                {
                    SetRect(stateBadge, new Vector2(0.04f, 0.015f), new Vector2(0.96f, 0.225f), Vector2.zero, Vector2.zero);
                }

                artwork.anchorMin = new Vector2(0.5f, 0.60f);
                artwork.anchorMax = artwork.anchorMin;
                artwork.pivot = new Vector2(0.5f, 0.5f);
                artwork.anchoredPosition = Vector2.zero;
                artwork.sizeDelta = GetRewardArtworkSize(index);
                if (lockIcon != null)
                {
                    lockIcon.anchorMin = new Vector2(0.84f, 0.73f);
                    lockIcon.anchorMax = lockIcon.anchorMin;
                    lockIcon.pivot = new Vector2(0.5f, 0.5f);
                    lockIcon.anchoredPosition = Vector2.zero;
                    lockIcon.sizeDelta = new Vector2(44f, 44f);
                }

                return;
            }

            SetRect(day, new Vector2(0.03f, 0.68f), new Vector2(0.18f, 0.95f), Vector2.zero, Vector2.zero);
            SetRect(amount, new Vector2(0.66f, 0.34f), new Vector2(0.94f, 0.66f), Vector2.zero, Vector2.zero);
            SetRect(stateSurface, new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.21f), Vector2.zero, Vector2.zero);
            if (stateUsesArea)
            {
                Stretch(state, Vector2.zero, Vector2.zero);
            }

            if (stateBadge != null && stateBadge != stateSurface)
            {
                SetRect(stateBadge, new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.22f), Vector2.zero, Vector2.zero);
            }

            bool artworkUsesViewport = artwork.parent != null && artwork.parent.name == "Day7ArtworkViewport";
            artwork.anchorMin = artworkUsesViewport ? new Vector2(0.5f, 0.5f) : new Vector2(0.29f, 0.60f);
            artwork.anchorMax = artwork.anchorMin;
            artwork.pivot = new Vector2(0.5f, 0.5f);
            artwork.anchoredPosition = artworkUsesViewport ? new Vector2(-12f, -4f) : Vector2.zero;
            artwork.sizeDelta = GetRewardArtworkSize(index);
            if (lockIcon != null)
            {
                lockIcon.anchorMin = new Vector2(0.91f, 0.78f);
                lockIcon.anchorMax = lockIcon.anchorMin;
                lockIcon.pivot = new Vector2(0.5f, 0.5f);
                lockIcon.anchoredPosition = Vector2.zero;
                lockIcon.sizeDelta = new Vector2(52f, 52f);
            }
        }

        private RectTransform CreateRewardCard(int index)
        {
            RectTransform card = CreateRuntimePanel($"RewardCard{index + 1}", rewardsCardsRoot, new Color(0.02f, 0.13f, 0.28f, 0.92f));
            UISpriteFactory.ApplyRounded(card.GetComponent<Image>(), 0.22f);
            card.GetComponent<Image>().raycastTarget = true;

            Outline outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.25f, 0.87f, 1f, 0.36f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;

            TMP_Text day = CreateRuntimeText("Day", card, $"Day {index + 1}", 36f, TextAlignmentOptions.Center);
            SetRect(day.rectTransform, new Vector2(0.04f, 0.82f), new Vector2(0.96f, 0.97f), Vector2.zero, Vector2.zero);
            day.fontStyle = FontStyles.Bold;

            GameObject coinObject = new GameObject("Coin", typeof(RectTransform), typeof(Image));
            coinObject.transform.SetParent(card, false);
            RectTransform coinRect = coinObject.transform as RectTransform;
            coinRect.anchorMin = new Vector2(0.5f, 0.635f);
            coinRect.anchorMax = coinRect.anchorMin;
            coinRect.pivot = new Vector2(0.5f, 0.5f);
            coinRect.anchoredPosition = Vector2.zero;
            coinRect.sizeDelta = GetRewardArtworkSize(index);

            Image coinImage = coinObject.GetComponent<Image>();
            Sprite rewardIcon = GetRewardIconSprite(index);
            if (rewardIcon != null)
            {
                coinImage.sprite = rewardIcon;
            }

            coinImage.type = Image.Type.Simple;
            coinImage.preserveAspect = true;
            coinImage.color = Color.white;
            coinImage.raycastTarget = false;
            if (rewardCardCoinImages != null && index >= 0 && index < rewardCardCoinImages.Length)
            {
                rewardCardCoinImages[index] = coinImage;
            }

            TMP_Text amount = CreateRuntimeText("Amount", card, SaveManager.GetDailyRewardAmountForDay(index).ToString(), 50f, TextAlignmentOptions.Center);
            SetRect(amount.rectTransform, new Vector2(0.04f, 0.29f), new Vector2(0.96f, 0.45f), Vector2.zero, Vector2.zero);
            amount.fontStyle = FontStyles.Bold;

            TMP_Text state = CreateRuntimeText("State", card, "LOCKED", 30f, TextAlignmentOptions.Center);
            SetRect(state.rectTransform, new Vector2(0.04f, 0.035f), new Vector2(0.96f, 0.28f), Vector2.zero, Vector2.zero);
            state.fontStyle = FontStyles.Bold;
            ConfigureRewardCardContentLayout(
                index,
                day.rectTransform,
                amount.rectTransform,
                state.rectTransform,
                coinRect,
                null,
                null);

            return card;
        }

        private void PositionRewardCard(RectTransform card, int index)
        {
            if (index < 6)
            {
                RectTransform row = index < 3 ? rewardsRowOne : rewardsRowTwo;
                if (card.parent != row)
                {
                    card.SetParent(row, false);
                }

                int column = index % 3;
                float minX = column * 0.35f;
                SetRect(card, new Vector2(minX, 0f), new Vector2(minX + 0.30f, 1f), Vector2.zero, Vector2.zero);
                return;
            }

            if (card.parent != rewardsCardsRoot)
            {
                card.SetParent(rewardsCardsRoot, false);
            }

            SetRect(card, new Vector2(0f, 0f), new Vector2(1f, 0.38f), Vector2.zero, Vector2.zero);
        }

        private void RefreshRewardCards(int currentDayIndex, bool canClaim, bool claimedToday, bool claimInteractionReady)
        {
            if (dailyRewardView == null
                || dailyRewardView.RewardCards == null
                || dailyRewardView.RewardCards.Length != SaveManager.DailyRewardDayCount)
            {
                return;
            }

            currentDayIndex = Mathf.Clamp(currentDayIndex, 0, SaveManager.DailyRewardDayCount - 1);
            for (int i = 0; i < SaveManager.DailyRewardDayCount; i++)
            {
                DailyRewardView.RewardCard card = dailyRewardView.RewardCards[i];
                if (card == null || card.Button == null)
                {
                    continue;
                }

                bool available = canClaim && i == currentDayIndex;
                bool claimed = i < currentDayIndex || (claimedToday && i == currentDayIndex);
                bool cardCanClaim = available && claimInteractionReady;

                card.Button.onClick.RemoveAllListeners();
                card.Button.interactable = cardCanClaim;
                if (cardCanClaim)
                {
                    card.Button.onClick.AddListener(ClaimDailyGift);
                }

                card.ApplyState(
                    i,
                    SaveManager.GetDailyRewardAmountForDay(i),
                    available,
                    claimed);
            }
        }

        private void RefreshRewardCardsLegacy(int currentDayIndex, bool canClaim, bool claimedToday, bool claimInteractionReady)
        {
            if (rewardCardImages == null || rewardCardImages.Length != SaveManager.DailyRewardDayCount)
            {
                return;
            }

            currentDayIndex = Mathf.Clamp(currentDayIndex, 0, SaveManager.DailyRewardDayCount - 1);
            EnsureRewardSpritesLoaded();
            for (int i = 0; i < SaveManager.DailyRewardDayCount; i++)
            {
                bool available = canClaim && i == currentDayIndex;
                bool claimed = i < currentDayIndex || (claimedToday && i == currentDayIndex);
                bool locked = !available && !claimed;
                bool daySeven = i == SaveManager.DailyRewardDayCount - 1;

                if (rewardCardButtons != null
                    && i < rewardCardButtons.Length
                    && rewardCardButtons[i] != null)
                {
                    rewardCardButtons[i].onClick.RemoveAllListeners();
                    bool cardCanClaim = available && claimInteractionReady;
                    rewardCardButtons[i].interactable = cardCanClaim;
                    if (cardCanClaim)
                    {
                        rewardCardButtons[i].onClick.AddListener(ClaimDailyGift);
                    }
                }

                Color fill = daySeven
                    ? available
                        ? new Color(0.015f, 0.34f, 0.46f, 0.995f)
                        : claimed
                            ? new Color(0.014f, 0.095f, 0.155f, 1f)
                            : new Color(0.012f, 0.075f, 0.135f, 1f)
                    : available
                        ? new Color(0.015f, 0.44f, 0.68f, 0.995f)
                        : claimed
                            ? new Color(0.012f, 0.12f, 0.21f, 0.92f)
                            : new Color(0.012f, 0.105f, 0.18f, 0.995f);
                Color border = available
                    ? (daySeven ? new Color(1f, 0.79f, 0.31f, 0.98f) : new Color(0.34f, 0.96f, 1f, 0.98f))
                    : daySeven
                        ? new Color(1f, 0.76f, 0.27f, 0.90f)
                        : new Color(0.30f, 0.87f, 0.98f, claimed ? 0.42f : 0.44f);
                Color accent = daySeven
                    ? new Color(1f, 0.79f, 0.30f, available ? 1f : 0.82f)
                    : available
                        ? new Color(0.58f, 1f, 1f, 1f)
                        : claimed
                            ? new Color(0.32f, 0.76f, 0.82f, 0.48f)
                            : new Color(0.28f, 0.68f, 0.82f, 0.42f);
                Color stateBadge = daySeven
                    ? available
                        ? new Color(0.04f, 0.60f, 0.62f, 0.96f)
                        : claimed
                            ? new Color(0.045f, 0.25f, 0.28f, 0.96f)
                            : new Color(0.035f, 0.105f, 0.14f, 0.96f)
                    : available
                        ? new Color(0.02f, 0.65f, 0.72f, 0.94f)
                        : claimed
                            ? new Color(0.075f, 0.34f, 0.42f, 0.94f)
                            : new Color(0.02f, 0.10f, 0.17f, 0.96f);

                if (rewardCardImages[i] != null)
                {
                    rewardCardImages[i].color = fill;
                }

                if (rewardCardOutlines[i] != null)
                {
                    rewardCardOutlines[i].effectColor = border;
                    rewardCardOutlines[i].effectDistance = available
                        ? new Vector2(2f, -2f)
                        : new Vector2(1f, -1f);
                }

                if (rewardCardAccentImages[i] != null)
                {
                    rewardCardAccentImages[i].color = accent;
                    rewardCardAccentImages[i].gameObject.SetActive(!daySeven);
                }

                Transform innerHighlightTransform = rewardCardImages[i] == null
                    ? null
                    : rewardCardImages[i].transform.Find("InnerHighlight");
                Image innerHighlight = innerHighlightTransform == null
                    ? null
                    : innerHighlightTransform.GetComponent<Image>();
                if (innerHighlight != null)
                {
                    innerHighlight.color = daySeven
                        ? available
                            ? new Color(0.18f, 0.78f, 0.86f, 0.085f)
                            : claimed
                                ? new Color(0.05f, 0.24f, 0.32f, 0.045f)
                                : new Color(0.03f, 0.22f, 0.30f, 0.045f)
                        : available
                            ? new Color(0.62f, 1f, 1f, 0.105f)
                            : claimed
                                ? new Color(0.34f, 0.78f, 0.88f, 0.085f)
                                : new Color(0.34f, 0.74f, 0.86f, 0.11f);
                }

                if (rewardCardImages[i] != null)
                {
                    Shadow cardShadow = EnsureStandaloneShadow(rewardCardImages[i].gameObject);
                    cardShadow.effectColor = available
                        ? new Color(0f, 0.035f, 0.11f, 0.60f)
                        : daySeven
                            ? new Color(0f, 0.018f, 0.055f, 0.64f)
                            : new Color(0f, 0.02f, 0.075f, 0.42f);
                    cardShadow.effectDistance = available || daySeven
                        ? new Vector2(0f, -4f)
                        : new Vector2(0f, -3f);
                }

                if (rewardCardStateBadges[i] != null)
                {
                    rewardCardStateBadges[i].color = stateBadge;
                }

                if (rewardCardCoinImages[i] != null)
                {
                    Sprite rewardArtwork = GetRewardIconSprite(i);
                    if (rewardArtwork != null)
                    {
                        rewardCardCoinImages[i].sprite = rewardArtwork;
                    }

                    rewardCardCoinImages[i].type = Image.Type.Simple;
                    rewardCardCoinImages[i].preserveAspect = true;
                    rewardCardCoinImages[i].rectTransform.sizeDelta = GetRewardArtworkSize(i);
                    rewardCardCoinImages[i].color = available
                        ? Color.white
                        : claimed
                            ? new Color(1f, 1f, 1f, 0.88f)
                            : daySeven
                                ? new Color(1f, 1f, 1f, 0.88f)
                                : new Color(1f, 1f, 1f, 0.92f);
                }

                if (rewardCardLockedOverlays != null
                    && i < rewardCardLockedOverlays.Length
                    && rewardCardLockedOverlays[i] != null)
                {
                    rewardCardLockedOverlays[i].color = locked
                        ? daySeven
                            ? Color.clear
                            : new Color(0f, 0.018f, 0.075f, 0.08f)
                        : Color.clear;
                }

                rewardCardDayTexts[i].text = $"Day {i + 1}";
                rewardCardDayTexts[i].color = daySeven
                    ? new Color(1f, 0.86f, 0.55f, available ? 1f : 0.94f)
                    : available
                        ? Color.white
                        : new Color(0.80f, 0.93f, 1f, claimed ? 0.94f : 0.88f);
                rewardCardAmountTexts[i].text = SaveManager.GetDailyRewardAmountForDay(i).ToString();
                rewardCardAmountTexts[i].color = daySeven
                    ? new Color(1f, 0.81f, 0.35f, 1f)
                    : available
                        ? Color.white
                        : new Color(0.88f, 0.96f, 1f, claimed ? 0.84f : 0.92f);
                rewardCardStateTexts[i].text = available ? "CLAIM" : claimed ? "CLAIMED" : "LOCKED";
                rewardCardStateTexts[i].color = daySeven
                    ? available
                        ? new Color(1f, 0.96f, 0.78f, 1f)
                        : claimed
                            ? new Color(0.86f, 0.92f, 0.84f, 1f)
                            : new Color(0.90f, 0.80f, 0.58f, 1f)
                    : available
                        ? new Color(0.86f, 1f, 1f, 1f)
                        : claimed
                            ? new Color(0.76f, 0.95f, 0.97f, 1f)
                            : new Color(0.72f, 0.83f, 0.90f, 1f);
                Shadow stateShadow = EnsureStandaloneShadow(rewardCardStateTexts[i].gameObject);
                stateShadow.effectColor = available
                    ? new Color(0f, 0.04f, 0.10f, 0.68f)
                    : new Color(0f, 0.018f, 0.065f, 0.54f);
                stateShadow.effectDistance = new Vector2(0f, -2f);
                stateShadow.useGraphicAlpha = true;
            }
        }

        private void ConfigureRewardsCloseButton(RectTransform panel)
        {
            if (achievementsCloseButton == null)
            {
                Transform close = FindRewardsDescendant(panel, "AchievementsCloseButton");
                achievementsCloseButton = close == null ? null : close.GetComponent<Button>();
            }

            if (achievementsCloseButton == null)
            {
                achievementsCloseButton = CreateRuntimeButton("AchievementsCloseButton", panel, string.Empty, Color.clear, Color.white);
            }

            RectTransform headerArea = panel.Find("HeaderArea") as RectTransform;
            RectTransform closeRect = achievementsCloseButton.transform as RectTransform;
            achievementsCloseButton.gameObject.SetActive(true);
            closeRect.SetParent(headerArea != null ? headerArea : panel, false);
            closeRect.anchorMin = Vector2.one;
            closeRect.anchorMax = Vector2.one;
            closeRect.pivot = new Vector2(0.5f, 0.5f);
            closeRect.anchoredPosition = new Vector2(-98f, -86f);
            closeRect.sizeDelta = new Vector2(79.12f, 79.12f);
            closeRect.SetAsLastSibling();

            Image closeImage = achievementsCloseButton.GetComponent<Image>();
            if (closeImage == null)
            {
                closeImage = achievementsCloseButton.gameObject.AddComponent<Image>();
            }

            closeImage.enabled = true;
            closeImage.gameObject.SetActive(true);
            achievementsCloseButton.targetGraphic = closeImage;
            TMP_Text label = achievementsCloseButton.GetComponentInChildren<TMP_Text>(true);
            UISpriteFactory.ApplyRounded(closeImage, 1f);
            closeImage.color = new Color(0.46f, 1f, 1f, 1f);

            Image closeCyanFill = EnsureRewardsImage(closeRect, "CloseCyanFill");
            SetRect(closeCyanFill.rectTransform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f), Vector2.zero, Vector2.zero);
            UISpriteFactory.ApplyRounded(closeCyanFill, 1f);
            closeCyanFill.color = new Color(0f, 0.58f, 0.96f, 1f);
            closeCyanFill.raycastTarget = false;
            closeCyanFill.transform.SetAsFirstSibling();

            Image closeHighlight = EnsureRewardsImage(closeRect, "CloseTopHighlight");
            SetRect(closeHighlight.rectTransform, new Vector2(0.18f, 0.56f), new Vector2(0.82f, 0.86f), Vector2.zero, Vector2.zero);
            UISpriteFactory.ApplyRounded(closeHighlight, 1f);
            closeHighlight.color = new Color(0.94f, 1f, 1f, 0.20f);
            closeHighlight.raycastTarget = false;
            closeHighlight.transform.SetSiblingIndex(1);
            if (label == null)
            {
                label = CreateRuntimeText("CloseLabel", achievementsCloseButton.transform, "X", 42f, TextAlignmentOptions.Center);
                Stretch(label.rectTransform, Vector2.zero, Vector2.zero);
            }

            label.gameObject.SetActive(true);
            label.text = "X";
            label.color = Color.white;
            label.fontSize = 47f;
            label.fontSizeMax = 47f;
            label.fontSizeMin = 32f;
            label.fontStyle = FontStyles.Bold;
            Shadow closeLabelShadow = EnsureStandaloneShadow(label.gameObject);
            closeLabelShadow.effectColor = new Color(0f, 0.08f, 0.20f, 0.72f);
            closeLabelShadow.effectDistance = new Vector2(0f, -2f);
            closeLabelShadow.useGraphicAlpha = true;

            Outline closeOutline = achievementsCloseButton.GetComponent<Outline>();
            if (closeOutline == null)
            {
                closeOutline = achievementsCloseButton.gameObject.AddComponent<Outline>();
            }

            closeOutline.effectColor = new Color(0.72f, 1f, 1f, 0.94f);
            closeOutline.effectDistance = new Vector2(2f, -2f);
            closeOutline.useGraphicAlpha = true;

            Shadow closeShadow = EnsureStandaloneShadow(achievementsCloseButton.gameObject);
            closeShadow.effectColor = new Color(0f, 0.02f, 0.08f, 0.48f);
            closeShadow.effectDistance = new Vector2(0f, -3f);
            closeShadow.useGraphicAlpha = true;

            closeImage.raycastTarget = true;
            achievementsCloseButton.transition = Selectable.Transition.None;

            if (label != null)
            {
                label.raycastTarget = false;
                label.transform.SetAsLastSibling();
            }

            achievementsCloseButton.onClick.RemoveAllListeners();
            achievementsCloseButton.onClick.AddListener(CloseAchievements);
        }

        private void EnsureNewClassicButton()
        {
            if (newClassicButton == null)
            {
                Transform existing = transform.Find("NewClassicButton");
                if (existing != null)
                {
                    newClassicButton = existing.GetComponent<Button>();
                }
            }

            if (newClassicButton == null)
            {
                newClassicButton = CreateRuntimeButton("NewClassicButton", transform, "JOC NOU", Hex("#10182E"), Hex("#FFD166"));
                SetRect((RectTransform)newClassicButton.transform, new Vector2(0.67f, 0.620f), new Vector2(0.91f, 0.716f), Vector2.zero, Vector2.zero);
            }

            TMP_Text text = newClassicButton.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                text.fontSize = 18f;
                text.fontSizeMax = 18f;
                text.fontSizeMin = 10f;
                text.text = "JOC\nNOU";
            }

            newClassicButton.gameObject.SetActive(false);
        }

        private void EnsureSettingsUi()
        {
            if (settingsRoot == null)
            {
                Transform existing = transform.Find("SettingsRoot");
                if (existing == null)
                {
                    existing = transform.Find("SettingsOverlay");
                }
                if (existing != null)
                {
                    settingsRoot = existing.gameObject;
                }
            }

            if (settingsRoot == null)
            {
                CreateRuntimeSettingsOverlay();
            }

            if (settingsRoot == null)
            {
                return;
            }

            if (settingsStatusText == null)
            {
                Transform status = settingsRoot.transform.Find("SettingsPanel/SettingsStatus");
                if (status != null)
                {
                    settingsStatusText = status.GetComponent<TMP_Text>();
                }
            }

            if (settingsCloseButton == null)
            {
                Transform close = settingsRoot.transform.Find("SettingsPanel/SettingsCloseButton");
                if (close != null)
                {
                    settingsCloseButton = close.GetComponent<Button>();
                }
            }

            if (settingsSoundButton == null)
            {
                Transform sound = settingsRoot.transform.Find("SettingsPanel/SettingsSoundButton");
                if (sound != null)
                {
                    settingsSoundButton = sound.GetComponent<Button>();
                    settingsSoundButtonText = settingsSoundButton.GetComponentInChildren<TMP_Text>();
                }
            }

            if (settingsHapticsButton == null)
            {
                Transform haptics = settingsRoot.transform.Find("SettingsPanel/SettingsHapticsButton");
                if (haptics != null)
                {
                    settingsHapticsButton = haptics.GetComponent<Button>();
                    settingsHapticsButtonText = settingsHapticsButton.GetComponentInChildren<TMP_Text>();
                }
            }

            if (settingsPerformanceButton == null)
            {
                Transform performance = settingsRoot.transform.Find("SettingsPanel/SettingsPerformanceButton");
                if (performance != null)
                {
                    settingsPerformanceButton = performance.GetComponent<Button>();
                    settingsPerformanceButtonText = settingsPerformanceButton.GetComponentInChildren<TMP_Text>();
                }
            }

            ConfigureCompletedSettingsUi();
        }

        private void ConfigureCompletedSettingsUi()
        {
            RectTransform panel = settingsRoot == null
                ? null
                : settingsRoot.transform.Find("SettingsPanel") as RectTransform;
            if (panel == null)
            {
                return;
            }

            settingsRoot.name = "SettingsRoot";
            RectTransform settingsRootRect = settingsRoot.transform as RectTransform;
            if (settingsRootRect != null)
            {
                Stretch(settingsRootRect, Vector2.zero, Vector2.zero);
            }

            Image rootImage = settingsRoot.GetComponent<Image>();
            if (rootImage != null)
            {
                rootImage.color = Color.clear;
                rootImage.raycastTarget = false;
            }

            RectTransform dimOverlay = EnsureSettingsRect(settingsRoot.transform, "DimOverlay");
            Stretch(dimOverlay, Vector2.zero, Vector2.zero);
            Image dimImage = EnsureSettingsImage(dimOverlay.gameObject);
            dimImage.color = new Color(0f, 0.025f, 0.09f, 0.72f);
            dimImage.raycastTarget = true;
            dimOverlay.SetAsFirstSibling();

            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = panel.anchorMin;
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(780f, 944f);
            panel.localScale = Vector3.one;
            panel.SetAsLastSibling();

            Image panelImage = panel.GetComponent<Image>();
            if (panelImage != null)
            {
                UISpriteFactory.ApplyRounded(panelImage, 0.18f);
                panelImage.color = new Color(0.008f, 0.060f, 0.145f, 0.97f);
                panelImage.raycastTarget = true;
            }

            Outline panelOutline = panel.GetComponent<Outline>();
            if (panelOutline == null)
            {
                panelOutline = panel.gameObject.AddComponent<Outline>();
            }

            panelOutline.effectColor = new Color(0.18f, 0.86f, 1f, 0.30f);
            panelOutline.effectDistance = new Vector2(1f, -1f);
            panelOutline.useGraphicAlpha = true;

            Shadow panelShadow = FindPlainShadow(panel.gameObject);
            if (panelShadow == null)
            {
                panelShadow = panel.gameObject.AddComponent<Shadow>();
            }

            panelShadow.effectColor = new Color(0f, 0.01f, 0.055f, 0.68f);
            panelShadow.effectDistance = new Vector2(0f, -8f);
            panelShadow.useGraphicAlpha = true;

            EnsureSettingsHeaderTreatment(panel);

            Transform titleTransform = panel.Find("SettingsTitle");
            settingsTitleText = titleTransform == null ? null : titleTransform.GetComponent<TMP_Text>();
            if (settingsTitleText == null)
            {
                settingsTitleText = CreateRuntimeText("SettingsTitle", panel, "SETTINGS", 54f, TextAlignmentOptions.Center);
            }

            settingsTitleText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            settingsTitleText.rectTransform.anchorMax = settingsTitleText.rectTransform.anchorMin;
            settingsTitleText.rectTransform.pivot = new Vector2(0.5f, 1f);
            settingsTitleText.rectTransform.anchoredPosition = new Vector2(0f, -38f);
            settingsTitleText.rectTransform.sizeDelta = new Vector2(560f, 76f);
            settingsTitleText.rectTransform.localScale = Vector3.one;
            settingsTitleText.fontSize = 60f;
            settingsTitleText.fontSizeMax = 60f;
            settingsTitleText.fontSizeMin = 44f;
            settingsTitleText.fontStyle = FontStyles.Bold;
            settingsTitleText.color = new Color(0.91f, 1f, 1f, 1f);
            settingsTitleText.alignment = TextAlignmentOptions.Center;

            settingsPrivacyButton = EnsureSettingsOptionButton(panel, "SettingsPrivacyButton", "PRIVACY POLICY\nComing Soon");
            settingsTermsButton = EnsureSettingsOptionButton(panel, "SettingsTermsButton", "TERMS OF SERVICE\nComing Soon");
            settingsAboutButton = EnsureSettingsOptionButton(panel, "SettingsAboutButton", "OPEN");

            DisableLegacyTutorialSettings(panel);
            DisableSettingsLanguageUi(panel);
            ConfigureSettingsCloseButton(panel);

            BuildSettingsRow(panel, "MusicRow", "Music", "M", settingsPerformanceButton, true, 0);
            BuildSettingsRow(panel, "SoundRow", "Sound", "S", settingsSoundButton, true, 1);
            BuildSettingsRow(panel, "VibrationRow", "Vibration", "V", settingsHapticsButton, true, 2);
            BuildSettingsRow(panel, "PrivacyRow", "Privacy Policy", "P", settingsPrivacyButton, false, 3);
            BuildSettingsRow(panel, "TermsRow", "Terms and Conditions", "T", settingsTermsButton, false, 4);
            BuildSettingsRow(panel, "AboutRow", "About", "i", settingsAboutButton, true, 5);

            Transform versionTransform = panel.Find("SettingsVersion");
            settingsVersionText = versionTransform == null ? null : versionTransform.GetComponent<TMP_Text>();
            if (settingsVersionText == null)
            {
                settingsVersionText = CreateRuntimeText("SettingsVersion", panel, string.Empty, 20f, TextAlignmentOptions.Center);
            }

            settingsVersionText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            settingsVersionText.rectTransform.anchorMax = settingsVersionText.rectTransform.anchorMin;
            settingsVersionText.rectTransform.pivot = new Vector2(0.5f, 0f);
            settingsVersionText.rectTransform.anchoredPosition = new Vector2(0f, 28f);
            settingsVersionText.rectTransform.sizeDelta = new Vector2(620f, 42f);
            settingsVersionText.rectTransform.localScale = Vector3.one;
            settingsVersionText.fontSize = 22f;
            settingsVersionText.fontSizeMax = 22f;
            settingsVersionText.fontSizeMin = 17f;
            settingsVersionText.color = new Color(0.67f, 0.82f, 0.90f, 0.78f);
            settingsVersionText.raycastTarget = false;

            if (settingsStatusText != null)
            {
                settingsStatusText.gameObject.SetActive(false);
            }

            EnsureSettingsAboutModal();
        }

        private void EnsureSettingsHeaderTreatment(RectTransform panel)
        {
            RectTransform header = EnsureSettingsRect(panel, "SettingsHeaderSurface");
            SetRect(header, new Vector2(0.055f, 0.855f), new Vector2(0.945f, 0.978f), Vector2.zero, Vector2.zero);
            Image headerImage = EnsureSettingsImage(header.gameObject);
            UISpriteFactory.ApplyRounded(headerImage, 0.34f);
            headerImage.color = new Color(0.015f, 0.16f, 0.29f, 0.48f);
            headerImage.raycastTarget = false;

            RectTransform accent = EnsureSettingsRect(header, "HeaderAccent");
            SetRect(accent, new Vector2(0.24f, 0.03f), new Vector2(0.76f, 0.075f), Vector2.zero, Vector2.zero);
            Image accentImage = EnsureSettingsImage(accent.gameObject);
            UISpriteFactory.ApplyRounded(accentImage, 1f);
            accentImage.color = new Color(0.28f, 0.91f, 1f, 0.42f);
            accentImage.raycastTarget = false;

            header.SetAsFirstSibling();
        }

        private void DisableSettingsLanguageUi(RectTransform panel)
        {
            Transform languageRow = panel == null ? null : panel.Find("LanguageRow");
            if (languageRow != null)
            {
                Button[] rowButtons = languageRow.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < rowButtons.Length; i++)
                {
                    rowButtons[i].onClick.RemoveAllListeners();
                }

                languageRow.gameObject.SetActive(false);
            }

            Transform[] panelChildren = panel == null ? null : panel.GetComponentsInChildren<Transform>(true);
            if (panelChildren != null)
            {
                for (int i = 0; i < panelChildren.Length; i++)
                {
                    if (panelChildren[i] == null || panelChildren[i].name != "SettingsLanguageButton")
                    {
                        continue;
                    }

                    Button languageButton = panelChildren[i].GetComponent<Button>();
                    if (languageButton != null)
                    {
                        languageButton.onClick.RemoveAllListeners();
                        settingsLanguageButton = languageButton;
                    }

                    panelChildren[i].gameObject.SetActive(false);
                }
            }
        }

        private void BuildSettingsRow(
            RectTransform panel,
            string rowName,
            string label,
            string iconText,
            Button control,
            bool interactable,
            int rowIndex)
        {
            if (panel == null || control == null)
            {
                return;
            }

            RectTransform row = EnsureSettingsRect(panel, rowName);
            row.anchorMin = new Vector2(0.5f, 0.5f);
            row.anchorMax = row.anchorMin;
            row.pivot = new Vector2(0.5f, 0.5f);
            float informationGroupGap = rowIndex >= 3 ? 28f : 0f;
            row.anchoredPosition = new Vector2(0f, 252f - rowIndex * 96f - informationGroupGap);
            row.sizeDelta = new Vector2(660f, 86f);
            row.localScale = Vector3.one;
            row.SetAsLastSibling();

            LayoutElement layoutElement = row.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = row.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.preferredWidth = 660f;
            layoutElement.preferredHeight = 86f;
            layoutElement.flexibleWidth = 0f;
            layoutElement.flexibleHeight = 0f;

            Image rowImage = EnsureSettingsImage(row.gameObject);
            UISpriteFactory.ApplyRounded(rowImage, 0.30f);
            rowImage.color = new Color(0.015f, 0.12f, 0.24f, 0.56f);
            rowImage.raycastTarget = false;
            Outline rowOutline = row.GetComponent<Outline>();
            if (rowOutline == null)
            {
                rowOutline = row.gameObject.AddComponent<Outline>();
            }

            rowOutline.effectColor = new Color(0.16f, 0.78f, 1f, 0.14f);
            rowOutline.effectDistance = new Vector2(1f, -1f);
            rowOutline.useGraphicAlpha = true;

            Shadow rowShadow = FindPlainShadow(row.gameObject);
            if (rowShadow == null)
            {
                rowShadow = row.gameObject.AddComponent<Shadow>();
            }

            rowShadow.effectColor = new Color(0f, 0.015f, 0.07f, 0.22f);
            rowShadow.effectDistance = new Vector2(0f, -2f);
            rowShadow.useGraphicAlpha = true;

            RectTransform iconBadge = EnsureSettingsRect(row, "IconBadge");
            iconBadge.anchorMin = new Vector2(0f, 0.5f);
            iconBadge.anchorMax = iconBadge.anchorMin;
            iconBadge.pivot = new Vector2(0.5f, 0.5f);
            iconBadge.anchoredPosition = new Vector2(46f, 0f);
            iconBadge.sizeDelta = new Vector2(54f, 54f);
            iconBadge.localScale = Vector3.one;
            Image iconBadgeImage = EnsureSettingsImage(iconBadge.gameObject);
            UISpriteFactory.ApplySoftCircle(iconBadgeImage);
            iconBadgeImage.color = new Color(0.02f, 0.38f, 0.69f, 0.98f);
            iconBadgeImage.raycastTarget = false;

            TMP_Text icon = EnsureSettingsText(iconBadge, "Icon", iconText, 28f, TextAlignmentOptions.Center);
            Stretch(icon.rectTransform, Vector2.zero, Vector2.zero);
            icon.fontStyle = FontStyles.Bold;
            icon.color = new Color(0.94f, 1f, 1f, 1f);

            TMP_Text rowLabel = EnsureSettingsText(row, "Label", label, 30f, TextAlignmentOptions.MidlineLeft);
            rowLabel.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            rowLabel.rectTransform.anchorMax = rowLabel.rectTransform.anchorMin;
            rowLabel.rectTransform.pivot = new Vector2(0f, 0.5f);
            rowLabel.rectTransform.anchoredPosition = new Vector2(86f, 0f);
            rowLabel.rectTransform.sizeDelta = new Vector2(360f, 64f);
            rowLabel.rectTransform.localScale = Vector3.one;
            rowLabel.fontStyle = FontStyles.Bold;
            rowLabel.color = Color.white;
            rowLabel.textWrappingMode = TextWrappingModes.NoWrap;
            rowLabel.overflowMode = TextOverflowModes.Ellipsis;
            EnsureTextShadow(rowLabel, new Color(0f, 0.025f, 0.09f, 0.70f), new Vector2(0f, -1.5f));

            RectTransform controlRect = control.transform as RectTransform;
            controlRect.SetParent(row, false);
            controlRect.anchorMin = new Vector2(1f, 0.5f);
            controlRect.anchorMax = controlRect.anchorMin;
            controlRect.pivot = new Vector2(1f, 0.5f);
            controlRect.anchoredPosition = new Vector2(-18f, 0f);
            controlRect.sizeDelta = new Vector2(190f, 58f);
            controlRect.localScale = Vector3.one;
            StyleSettingsControlButton(control, interactable);
        }

        private void ConfigureSettingsCloseButton(RectTransform panel)
        {
            settingsCloseButton = settingsCloseButton != null
                ? settingsCloseButton
                : EnsureSettingsOptionButton(panel, "SettingsCloseButton", "X");
            RectTransform closeRect = settingsCloseButton.transform as RectTransform;
            closeRect.SetParent(panel, false);
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = closeRect.anchorMin;
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-24f, -24f);
            closeRect.sizeDelta = new Vector2(68f, 68f);
            closeRect.localScale = Vector3.one;

            Image closeImage = settingsCloseButton.image != null
                ? settingsCloseButton.image
                : EnsureSettingsImage(settingsCloseButton.gameObject);
            Sprite closeSprite = Resources.Load<Sprite>("Ocean/Settings/Close_X");
            closeImage.sprite = closeSprite;
            closeImage.color = Color.white;
            closeImage.type = Image.Type.Simple;
            closeImage.preserveAspect = true;
            closeImage.raycastTarget = true;
            settingsCloseButton.transition = Selectable.Transition.None;
            SetButtonLabel(settingsCloseButton, string.Empty);
        }

        private void StyleSettingsControlButton(Button button, bool interactable)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.image != null ? button.image : EnsureSettingsImage(button.gameObject);
            UISpriteFactory.ApplyRounded(image, 0.44f);
            image.color = interactable
                ? new Color(0.025f, 0.40f, 0.72f, 0.98f)
                : new Color(0.02f, 0.17f, 0.31f, 0.96f);
            image.raycastTarget = true;

            button.transition = interactable ? Selectable.Transition.ColorTint : Selectable.Transition.None;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.84f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.66f, 0.92f, 1f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = Color.white;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            button.interactable = interactable;

            TMP_Text[] labels = button.GetComponentsInChildren<TMP_Text>(true);
            TMP_Text label = labels.Length == 0
                ? CreateRuntimeText("Label", button.transform, string.Empty, 23f, TextAlignmentOptions.Center)
                : labels[0];
            for (int i = 1; i < labels.Length; i++)
            {
                labels[i].gameObject.SetActive(false);
            }

            Stretch(label.rectTransform, new Vector2(10f, 4f), new Vector2(-10f, -4f));
            label.fontStyle = FontStyles.Bold;
            label.enableAutoSizing = true;
            label.fontSizeMax = 22f;
            label.fontSizeMin = 15f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = interactable ? Color.white : new Color(0.72f, 0.88f, 0.96f, 0.94f);
            label.raycastTarget = false;
            EnsureTextShadow(label, new Color(0f, 0.025f, 0.09f, 0.64f), new Vector2(0f, -1f));

            Outline outline = button.GetComponent<Outline>();
            if (outline == null)
            {
                outline = button.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = new Color(0.34f, 0.92f, 1f, interactable ? 0.42f : 0.20f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
        }

        private void DisableLegacyTutorialSettings(RectTransform panel)
        {
            Transform oldReset = panel.Find("SettingsResetTutorialButton");
            if (oldReset != null)
            {
                Button button = oldReset.GetComponent<Button>();
                button?.onClick.RemoveAllListeners();
                oldReset.gameObject.SetActive(false);
            }

            Transform oldConfirmation = settingsRoot.transform.Find("ResetTutorialConfirmation");
            if (oldConfirmation != null)
            {
                oldConfirmation.gameObject.SetActive(false);
            }
        }

        private void SetSettingsRowLabel(string rowName, string value)
        {
            Transform label = settingsRoot == null
                ? null
                : settingsRoot.transform.Find($"SettingsPanel/{rowName}/Label");
            TMP_Text text = label == null ? null : label.GetComponent<TMP_Text>();
            if (text != null)
            {
                text.text = value;
            }
        }

        private RectTransform EnsureSettingsRect(Transform parent, string objectName)
        {
            Transform existing = parent == null ? null : parent.Find(objectName);
            if (existing is RectTransform existingRect)
            {
                return existingRect;
            }

            GameObject child = new GameObject(objectName, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
        }

        private Image EnsureSettingsImage(GameObject target)
        {
            Image image = target.GetComponent<Image>();
            return image != null ? image : target.AddComponent<Image>();
        }

        private TMP_Text EnsureSettingsText(
            Transform parent,
            string objectName,
            string value,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            Transform existing = parent == null ? null : parent.Find(objectName);
            TMP_Text text = existing == null ? null : existing.GetComponent<TMP_Text>();
            if (text == null)
            {
                text = CreateRuntimeText(objectName, parent, value, fontSize, alignment);
            }

            text.text = value;
            text.fontSize = fontSize;
            text.fontSizeMax = fontSize;
            text.fontSizeMin = Mathf.Max(14f, fontSize * 0.65f);
            text.enableAutoSizing = true;
            text.alignment = alignment;
            text.characterSpacing = 0f;
            text.raycastTarget = false;
            return text;
        }

        private void EnsureTextShadow(TMP_Text text, Color color, Vector2 distance)
        {
            if (text == null)
            {
                return;
            }

            Shadow shadow = FindPlainShadow(text.gameObject);
            if (shadow == null)
            {
                shadow = text.gameObject.AddComponent<Shadow>();
            }

            shadow.effectColor = color;
            shadow.effectDistance = distance;
            shadow.useGraphicAlpha = true;
        }

        private void EnsureSettingsAboutModal()
        {
            if (settingsRoot == null)
            {
                return;
            }

            RectTransform overlay = EnsureSettingsRect(settingsRoot.transform, "AboutOverlay");
            Stretch(overlay, Vector2.zero, Vector2.zero);
            overlay.localScale = Vector3.one;
            Image overlayImage = EnsureSettingsImage(overlay.gameObject);
            overlayImage.color = new Color(0f, 0.015f, 0.06f, 0.76f);
            overlayImage.raycastTarget = true;

            RectTransform popup = EnsureSettingsRect(overlay, "AboutPanel");
            popup.anchorMin = new Vector2(0.5f, 0.5f);
            popup.anchorMax = popup.anchorMin;
            popup.pivot = new Vector2(0.5f, 0.5f);
            popup.anchoredPosition = Vector2.zero;
            popup.sizeDelta = new Vector2(620f, 620f);
            popup.localScale = Vector3.one;
            Image popupImage = EnsureSettingsImage(popup.gameObject);
            UISpriteFactory.ApplyRounded(popupImage, 0.20f);
            popupImage.color = new Color(0.01f, 0.08f, 0.19f, 0.995f);
            popupImage.raycastTarget = true;

            Outline popupOutline = popup.GetComponent<Outline>();
            if (popupOutline == null)
            {
                popupOutline = popup.gameObject.AddComponent<Outline>();
            }

            popupOutline.effectColor = new Color(0.20f, 0.86f, 1f, 0.50f);
            popupOutline.effectDistance = new Vector2(2f, -2f);
            popupOutline.useGraphicAlpha = true;

            TMP_Text title = EnsureSettingsText(popup, "AboutTitle", "CHROMA BLAST", 46f, TextAlignmentOptions.Center);
            title.rectTransform.anchorMin = new Vector2(0.08f, 0.76f);
            title.rectTransform.anchorMax = new Vector2(0.92f, 0.92f);
            title.rectTransform.offsetMin = Vector2.zero;
            title.rectTransform.offsetMax = Vector2.zero;
            title.fontStyle = FontStyles.Bold;
            title.color = new Color(0.90f, 1f, 1f, 1f);
            EnsureTextShadow(title, new Color(0f, 0.025f, 0.09f, 0.72f), new Vector2(0f, -2f));

            settingsAboutBodyText = EnsureSettingsText(popup, "AboutBody", string.Empty, 27f, TextAlignmentOptions.Center);
            settingsAboutBodyText.rectTransform.anchorMin = new Vector2(0.09f, 0.29f);
            settingsAboutBodyText.rectTransform.anchorMax = new Vector2(0.91f, 0.74f);
            settingsAboutBodyText.rectTransform.offsetMin = Vector2.zero;
            settingsAboutBodyText.rectTransform.offsetMax = Vector2.zero;
            settingsAboutBodyText.fontStyle = FontStyles.Normal;
            settingsAboutBodyText.color = new Color(0.82f, 0.94f, 1f, 1f);

            Button closeButton = EnsureSettingsOptionButton(popup, "AboutCloseButton", "CLOSE");
            RectTransform closeRect = closeButton.transform as RectTransform;
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = closeRect.anchorMin;
            closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.anchoredPosition = new Vector2(0f, 54f);
            closeRect.sizeDelta = new Vector2(260f, 72f);
            closeRect.localScale = Vector3.one;
            StyleSettingsControlButton(closeButton, true);
            WireButton(closeButton, CloseSettingsAbout);

            settingsAboutRoot = overlay.gameObject;
            settingsAboutRoot.SetActive(false);
        }

        private void OpenSettingsAbout()
        {
            EnsureSettingsAboutModal();
            AudioManager.Instance?.PlayClick();
            if (settingsAboutRoot != null)
            {
                RefreshSettingsAboutText(PlayerPrefs.GetString("SelectedLanguage", "en") == "ro");
                settingsAboutRoot.SetActive(true);
                settingsAboutRoot.transform.SetAsLastSibling();
            }
        }

        private void CloseSettingsAbout()
        {
            CloseSettingsAbout(true);
        }

        private void CloseSettingsAbout(bool playClick)
        {
            if (playClick)
            {
                AudioManager.Instance?.PlayClick();
            }

            if (settingsAboutRoot != null)
            {
                settingsAboutRoot.SetActive(false);
            }
        }

        private void RefreshSettingsAboutText(bool romanian)
        {
            if (settingsAboutBodyText == null)
            {
                return;
            }

            string description = romanian
                ? "Un puzzle relaxant cu blocuri, inspirat de ocean, despre linii curate si recorduri noi."
                : "A relaxing ocean-themed block puzzle about clean lines and new high scores.";
            string developer = string.IsNullOrWhiteSpace(Application.companyName)
                ? string.Empty
                : $"\n{(romanian ? "Dezvoltator" : "Developer")}: {Application.companyName}";
            settingsAboutBodyText.text = $"{description}\n\n{(romanian ? "Versiune" : "Version")} {Application.version}{developer}";

            Transform close = settingsAboutRoot == null
                ? null
                : settingsAboutRoot.transform.Find("AboutPanel/AboutCloseButton");
            SetButtonLabel(close == null ? null : close.GetComponent<Button>(), romanian ? "INCHIDE" : "CLOSE");
        }

        private Button EnsureSettingsOptionButton(RectTransform panel, string objectName, string label)
        {
            Transform existing = panel.Find(objectName);
            if (existing == null)
            {
                Button[] descendants = panel.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < descendants.Length; i++)
                {
                    if (descendants[i] != null && descendants[i].name == objectName)
                    {
                        existing = descendants[i].transform;
                        break;
                    }
                }
            }

            Button button = existing == null ? null : existing.GetComponent<Button>();
            return button != null
                ? button
                : CreateRuntimeButton(objectName, panel, label, new Color(0.02f, 0.14f, 0.30f, 0.96f), Color.white);
        }

        private static Shadow FindPlainShadow(GameObject target)
        {
            Shadow[] shadows = target == null ? null : target.GetComponents<Shadow>();
            if (shadows == null)
            {
                return null;
            }

            for (int i = 0; i < shadows.Length; i++)
            {
                if (shadows[i] != null && shadows[i].GetType() == typeof(Shadow))
                {
                    return shadows[i];
                }
            }

            return null;
        }

        private void WireCompletedSettingsListeners()
        {
            WireButton(settingsCloseButton, CloseSettings);
            WireButton(settingsPerformanceButton, ToggleMenuMusic);
            WireButton(settingsSoundButton, ToggleMenuSound);
            WireButton(settingsHapticsButton, ToggleMenuHaptics);
            WireButton(settingsAboutButton, OpenSettingsAbout);

            RectTransform settingsPanel = settingsRoot == null
                ? null
                : settingsRoot.transform.Find("SettingsPanel") as RectTransform;
            DisableSettingsLanguageUi(settingsPanel);

            if (settingsPrivacyButton != null)
            {
                settingsPrivacyButton.onClick.RemoveAllListeners();
                settingsPrivacyButton.interactable = false;
            }

            if (settingsTermsButton != null)
            {
                settingsTermsButton.onClick.RemoveAllListeners();
                settingsTermsButton.interactable = false;
            }
        }

        private static void WireButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private void CreateRuntimeSettingsOverlay()
        {
            RectTransform parentRect = transform as RectTransform;
            if (parentRect == null)
            {
                return;
            }

            RectTransform overlay = CreateRuntimePanel("SettingsRoot", parentRect, Color.clear);
            Stretch(overlay, Vector2.zero, Vector2.zero);
            overlay.SetAsLastSibling();

            RectTransform dimOverlay = CreateRuntimePanel("DimOverlay", overlay, new Color(0f, 0.015f, 0.06f, 0.66f));
            Stretch(dimOverlay, Vector2.zero, Vector2.zero);

            RectTransform panel = CreateRuntimePanel("SettingsPanel", overlay, Hex("#0B1123"));

            TMP_Text title = CreateRuntimeText("SettingsTitle", panel, "SETTINGS", 52, TextAlignmentOptions.Center);
            title.color = Hex("#17E6FF");

            settingsPerformanceButton = CreateRuntimeButton("SettingsPerformanceButton", panel, "ON", Hex("#075A9A"), Color.white);
            settingsSoundButton = CreateRuntimeButton("SettingsSoundButton", panel, "ON", Hex("#075A9A"), Color.white);
            settingsHapticsButton = CreateRuntimeButton("SettingsHapticsButton", panel, "ON", Hex("#075A9A"), Color.white);
            settingsCloseButton = CreateRuntimeButton("SettingsCloseButton", panel, "X", Color.white, Color.white);

            settingsSoundButtonText = settingsSoundButton.GetComponentInChildren<TMP_Text>();
            settingsHapticsButtonText = settingsHapticsButton.GetComponentInChildren<TMP_Text>();
            settingsPerformanceButtonText = settingsPerformanceButton.GetComponentInChildren<TMP_Text>();
            settingsRoot = overlay.gameObject;
            settingsRoot.SetActive(false);
        }

        private void EnsureThemeSwatches()
        {
            if (themeButtonText == null && themeButton != null)
            {
                themeButtonText = themeButton.GetComponentInChildren<TMP_Text>();
            }

            if (themeButtonText != null)
            {
                SetRect(themeButtonText.rectTransform, new Vector2(0f, 0.24f), Vector2.one, Vector2.zero, Vector2.zero);
            }

            if (themeSwatches != null && themeSwatches.Length >= GameConstants.ColorCount)
            {
                return;
            }

            Transform swatchRoot = null;
            if (themeButton != null)
            {
                swatchRoot = themeButton.transform.Find("ThemeSwatches");
            }

            if (swatchRoot == null)
            {
                swatchRoot = transform.Find("ThemeButton/ThemeSwatches");
            }

            if (swatchRoot != null)
            {
                themeSwatches = swatchRoot.GetComponentsInChildren<Image>(true);
            }
        }

        private void RefreshThemeSwatches(ThemeType theme)
        {
            EnsureThemeSwatches();
            if (themeSwatches == null)
            {
                return;
            }

            int count = Mathf.Min(themeSwatches.Length, GameConstants.ColorCount);
            for (int i = 0; i < count; i++)
            {
                if (themeSwatches[i] == null)
                {
                    continue;
                }

                themeSwatches[i].enabled = true;
                themeSwatches[i].color = ChromaPalette.GetColor((ChromaColor)i, theme);
            }
        }

        private void ApplyClassicButtonLayout(bool hasClassicRun)
        {
            RectTransform classicRect = classicButton == null ? null : classicButton.transform as RectTransform;
            if (classicRect != null)
            {
                SetRect(
                    classicRect,
                    new Vector2(0.13f, 0.545f),
                    new Vector2(0.87f, 0.665f),
                    Vector2.zero,
                    Vector2.zero);
            }

            RectTransform newRect = newClassicButton == null ? null : newClassicButton.transform as RectTransform;
            if (newRect != null)
            {
                SetRect(newRect, new Vector2(0.12f, 0.445f), new Vector2(0.34f, 0.515f), Vector2.zero, Vector2.zero);
            }
        }

        private void CreateRuntimeAchievementsOverlay()
        {
            RectTransform parentRect = transform as RectTransform;
            if (parentRect == null)
            {
                return;
            }

            RectTransform overlay = CreateRuntimePanel("AchievementsOverlay", parentRect, new Color(0f, 0.025f, 0.09f, 0.72f));
            Stretch(overlay, Vector2.zero, Vector2.zero);
            overlay.SetAsLastSibling();

            RectTransform panel = CreateRuntimePanel("AchievementsPanel", overlay, Hex("#0B1123"));
            SetRect(panel, new Vector2(0.055f, 0.14f), new Vector2(0.945f, 0.88f), Vector2.zero, Vector2.zero);
            AddFrame(panel.gameObject, new Color(0.25f, 0.88f, 1f, 0.72f));

            TMP_Text title = CreateRuntimeText("AchievementsTitle", panel, "Daily Reward", 54, TextAlignmentOptions.Center);
            SetRect(title.rectTransform, new Vector2(0.13f, 0.86f), new Vector2(0.87f, 0.96f), Vector2.zero, Vector2.zero);
            title.color = new Color(0.92f, 1f, 1f, 1f);

            achievementsRoot = overlay.gameObject;
            ConfigureRewardsPanel();
            achievementsRoot.SetActive(false);
        }

        private RectTransform CreateRuntimePanel(string name, Transform parent, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            Image image = panel.GetComponent<Image>();
            image.color = color;
            return (RectTransform)panel.transform;
        }

        private TMP_Text CreateRuntimeText(string name, Transform parent, string textValue, float size, TextAlignmentOptions alignment)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            TMP_Text text = textObject.GetComponent<TMP_Text>();
            text.text = textValue;
            text.fontSize = size;
            text.fontSizeMax = size;
            text.fontSizeMin = Mathf.Max(11f, size * 0.55f);
            text.enableAutoSizing = true;
            text.fontStyle = FontStyles.Bold;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private Button CreateRuntimeButton(string name, Transform parent, string label, Color backgroundColor, Color accentColor)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            Image image = buttonObject.GetComponent<Image>();
            image.color = backgroundColor;
            Button button = buttonObject.GetComponent<Button>();
            buttonObject.AddComponent<UIButtonFeedback>();

            ColorBlock colors = button.colors;
            colors.normalColor = backgroundColor;
            colors.highlightedColor = Color.Lerp(backgroundColor, accentColor, 0.25f);
            colors.pressedColor = accentColor;
            colors.selectedColor = backgroundColor;
            button.colors = colors;

            TMP_Text text = CreateRuntimeText("Label", buttonObject.transform, label, 24, TextAlignmentOptions.Center);
            Stretch(text.rectTransform, Vector2.zero, Vector2.zero);
            text.color = accentColor;
            return button;
        }

        private void AddFrame(GameObject target, Color color)
        {
            Outline outline = target.AddComponent<Outline>();
            outline.effectColor = new Color(color.r, color.g, color.b, 0.44f);
            outline.effectDistance = new Vector2(2f, -2f);
            Shadow shadow = target.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.42f);
            shadow.effectDistance = new Vector2(0f, -5f);
        }

        private void SetButtonLabel(Button button, string label)
        {
            TMP_Text text = button == null ? null : button.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                text.text = label;
            }
        }

        private void StartMode(GameMode mode)
        {
            AudioManager.Instance?.PlayClick();
            GameSession.SelectedMode = mode;
            AnalyticsManager.Instance?.RecordModeSelected(mode);
            SceneManager.LoadScene("Game");
        }

        private void StartFreshClassic()
        {
            AudioManager.Instance?.PlayClick();
            SaveManager.Instance?.ClearClassicRun();
            GameSession.SelectedMode = GameMode.Classic;
            AnalyticsManager.Instance?.RecordModeSelected(GameMode.Classic);
            SceneManager.LoadScene("Game");
        }

        private void QuitGame()
        {
            AudioManager.Instance?.PlayClick();
            Application.Quit();
        }

        private void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            SetRect(rect, Vector2.zero, Vector2.one, offsetMin, offsetMax);
        }

        private Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString(value, out Color color);
            return color;
        }
    }
}
