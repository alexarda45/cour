using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChromaBlast
{
    public class GameHUD : MonoBehaviour
    {
        private const string OceanBackgroundPath = "Ocean/Backgrounds/BG_Gameplay_New";
        private const string BestScoreCrownPath = "Ocean/UI/CrownIcon";
        private const string BestScoreCapsulePath = "Ocean/UI/BestScoreCapsule";
        private const string SettingsGearPath = "Ocean/UI/SettingsIcon";
        private const string SettingsPanelPath = "Ocean/Settings/Settings_Panel";
        private const string SettingsClosePath = "Ocean/Settings/Close_X";
        private const string OceanSettingsRoot = "Ocean/Settings";
        private const string GardenSettingsRoot = "Themes/Settings/Garden";
        private const string DesertCleanSettingsRoot = "Themes/Settings/DesertClean";
        private const string BlossomSettingsRoot = "Themes/Settings/Blossom";
        private const string BeachSettingsRoot = "Themes/Settings/Beach";
        private const string SettingsRestartIconPath = "Ocean/Icons/Icon_Restart";
        private const string FinalBoardVisualPath = "Ocean/Board/Board_LightBlue_Final_Square";
        private const string ScoreFontPath = "Fonts/Fredoka-SemiBold SDF";
        private const string SelectedLanguageKey = "SelectedLanguage";
        private const string EnglishLanguageCode = "en";
        private const float SettingsPanelWidth = 690f;
        private const float SettingsPanelHeight = 1060f;
        private const float SettingsRowsWidth = 580f;
        private const float SettingsRowHeight = 84f;
        private const float SettingsRowsSpacing = 8f;
        private const float SettingsRowsHeight = SettingsRowHeight * 8f + SettingsRowsSpacing * 7f;
        private const float SettingsControlWidth = 190f;
        private const float SettingsControlHeight = 60f;
        private const float CandySettingsPanelWidth = 900f;
        private const float CandySettingsPanelHeight = 1350f;
        private const float CandySettingsRowsWidth = 640f;
        private const float CandySettingsRowHeight = 108f;
        private const float CandySettingsRowsSpacing = 8f;
        private const float CandySettingsRowsHeight = CandySettingsRowHeight * 8f + CandySettingsRowsSpacing * 7f;
        private const float CandySettingsControlWidth = 150f;
        private const float CandySettingsControlHeight = 72f;
        private const float GardenSettingsPanelWidth = 720f;
        private const float GardenSettingsPanelHeight = 1250f;
        private const float GardenSettingsRowsWidth = 620f;
        private const float GardenSettingsRowHeight = 86f;
        private const float GardenSettingsRowsSpacing = 10f;
        private const float GardenSettingsRowsHeight = GardenSettingsRowHeight * 8f + GardenSettingsRowsSpacing * 7f;
        private const float GardenSettingsControlWidth = 146f;
        private const float GardenSettingsControlHeight = 68f;
        private const float DesertSettingsPanelWidth = 800f;
        private const float DesertSettingsPanelHeight = 1360f;
        private const float DesertSettingsRowsWidth = 650f;
        private const float DesertSettingsRowHeight = 92f;
        private const float DesertSettingsRowsSpacing = 8f;
        private const float DesertSettingsRowsHeight = DesertSettingsRowHeight * 8f + DesertSettingsRowsSpacing * 7f;
        private const float DesertSettingsControlWidth = 166f;
        private const float DesertSettingsControlHeight = 76f;
        private const float BlossomSettingsPanelWidth = 840f;
        private const float BlossomSettingsPanelHeight = 1330f;
        private const float BlossomSettingsRowsWidth = 690f;
        private const float BlossomSettingsRowHeight = 105f;
        private const float BlossomSettingsRowsSpacing = 10f;
        private const float BlossomSettingsRowsHeight = BlossomSettingsRowHeight * 8f + BlossomSettingsRowsSpacing * 7f;
        private const float BlossomSettingsControlWidth = 238f;
        private const float BlossomSettingsControlHeight = 86f;
        private const float BeachSettingsPanelWidth = 800f;
        private const float BeachSettingsPanelHeight = 1280f;
        private const float BeachSettingsRowsWidth = 690f;
        private const float BeachSettingsRowHeight = 100f;
        private const float BeachSettingsRowsSpacing = 6f;
        private const float BeachSettingsRowsHeight = BeachSettingsRowHeight * 8f + BeachSettingsRowsSpacing * 7f;
        private const float BeachSettingsControlWidth = 220f;
        private const float BeachSettingsControlHeight = 82f;
        private const float OceanBoardFrameRadius = 0.30f;

        [Header("Text")]
        [SerializeField] private TMP_Text modeText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text highScoreText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text chainText;
        [SerializeField] private TMP_Text missionText;
        [SerializeField] private TMP_Text feedbackText;

        [Header("Controls")]
        [SerializeField] private Button undoButton;
        [SerializeField] private Button menuButton;
        [SerializeField] private Button muteButton;
        [SerializeField] private TMP_Text muteButtonText;
        [SerializeField] private Button rewardedChromaButton;

        [Header("Pause")]
        [SerializeField] private GameObject pauseRoot;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button pauseRestartButton;
        [SerializeField] private Button pauseMenuButton;
        [SerializeField] private Button pauseMuteButton;
        [SerializeField] private TMP_Text pauseMuteButtonText;
        [SerializeField] private Button pauseHapticsButton;
        [SerializeField] private TMP_Text pauseHapticsButtonText;
        [SerializeField] private Button pauseResetTutorialButton;

        [Header("Chroma")]
        [SerializeField] private ChromaBarView[] chromaBars;
        [SerializeField] private RankProgressView rankProgressView;

        [Header("Drag")]
        [SerializeField] private RectTransform dragLayer;

        [Header("Juice")]
        [SerializeField] private JuicePopupLayer popupLayer;
        [SerializeField] private Image screenFlashImage;
        private PremiumLineClearFx premiumLineClearFx;

        public RectTransform DragLayer => dragLayer;

        private GameManager gameManager;
        private Coroutine feedbackRoutine;
        private Coroutine scoreCountRoutine;
        private Coroutine newBestRoutine;
        private readonly bool[] announcedPopReady = new bool[GameConstants.ColorCount];
        private int displayedScore;
        private int targetScore;
        private int displayedBestScore;
        private int targetBestScore;
        private bool bestScoreInitialized;
        private bool suppressNextScoreAutoPunch;
        private int lastDisplayedBlitzSecond = -1;
        private Coroutine blitzUrgencyRoutine;
        private Image oceanBackgroundImage;
        private Image blitzTimerCapsuleImage;
        private TMP_Text scoreShadowText;
        private bool blossomScoreWasActive;
        private bool blossomScoreShadowWasActive;
        private bool blossomScoreVisibilityCaptured;
        private bool beachScoreWasActive;
        private bool beachScoreShadowWasActive;
        private bool beachScoreVisibilityCaptured;
        private TMP_Text newBestText;
        private RectTransform bestScoreHudRoot;
        private Image bestScoreCapsuleImage;
        private Image bestScoreCrownImage;
        private Image bestScoreCrownGlowImage;
        private Image finalBoardVisualImage;
        private Image finalBoardShadowImage;
        private Image pauseOverlayImage;
        private RectTransform pausePanelRoot;
        private Image pausePanelImage;
        private TMP_Text pauseTitleText;
        private Button pauseLanguageButton;
        private GameObject languagePopupRoot;
        private TMP_Text languageValueOverlay;
        private RectTransform settingsRowsContainer;
        private Image settingsBottomDecorationImage;
        private Image musicToggleTrackImage;
        private Image musicToggleKnobImage;
        private TMP_Text musicToggleStateText;
        private Image soundToggleTrackImage;
        private Image soundToggleKnobImage;
        private TMP_Text soundToggleStateText;
        private Image vibrationToggleTrackImage;
        private Image vibrationToggleKnobImage;
        private TMP_Text vibrationToggleStateText;
        private TMP_Text languageButtonText;
        private Sprite musicIconSprite;
        private Sprite soundIconSprite;
        private Sprite vibrationIconSprite;
        private Sprite languageIconSprite;
        private Sprite restartIconSprite;
        private Sprite mainMenuIconSprite;

        public void Initialize(GameManager owner)
        {
            EnforceEnglishLanguage();
            gameManager = owner;
            ThemeCatalog.ThemeChanged -= HandleThemeChanged;
            ThemeCatalog.ThemeChanged += HandleThemeChanged;

            if (undoButton != null)
            {
                undoButton.onClick.RemoveAllListeners();
                undoButton.onClick.AddListener(() => gameManager.UndoLastMove());
            }

            if (menuButton != null)
            {
                menuButton.onClick.RemoveAllListeners();
                menuButton.onClick.AddListener(() => gameManager.OpenPauseMenu());
            }

            if (muteButton != null)
            {
                muteButton.onClick.RemoveAllListeners();
                muteButton.onClick.AddListener(() =>
                {
                    AudioManager.Instance?.ToggleMute();
                    RefreshMuteLabel();
                });
            }

            if (rewardedChromaButton != null)
            {
                rewardedChromaButton.onClick.RemoveAllListeners();
                rewardedChromaButton.onClick.AddListener(() => gameManager.RequestRewardedChromaFill());
            }

            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveAllListeners();
                resumeButton.onClick.AddListener(() => gameManager.ResumeGame());
            }

            if (pauseRestartButton != null)
            {
                pauseRestartButton.onClick.RemoveAllListeners();
                pauseRestartButton.onClick.AddListener(() => gameManager.RestartCurrentMode());
            }

            if (pauseMenuButton != null)
            {
                pauseMenuButton.onClick.RemoveAllListeners();
                pauseMenuButton.onClick.AddListener(() => gameManager.GoToMenu());
            }

            if (pauseMuteButton != null)
            {
                pauseMuteButton.onClick.RemoveAllListeners();
                pauseMuteButton.onClick.AddListener(ToggleSoundFromSettings);
            }

            if (pauseHapticsButton != null)
            {
                pauseHapticsButton.onClick.RemoveAllListeners();
                pauseHapticsButton.onClick.AddListener(ToggleVibrationFromSettings);
            }

            if (pauseResetTutorialButton != null)
            {
                pauseResetTutorialButton.onClick.RemoveAllListeners();
                pauseResetTutorialButton.gameObject.SetActive(false);
            }

            if (chromaBars == null)
            {
                chromaBars = new ChromaBarView[0];
            }

            for (int i = 0; i < chromaBars.Length && i < GameConstants.ColorCount; i++)
            {
                if (chromaBars[i] != null)
                {
                    chromaBars[i].Initialize((ChromaColor)i, gameManager.RequestPop);
                }
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.MuteChanged += OnMuteChanged;
            }

            RefreshMuteLabel();
            RefreshPauseLabels();
            EnsureMissionText();
            HideMissionText();
            EnsureScreenFlash();
            EnsurePremiumLineClearFx();
            EnsureOceanBranding();
            ConfigureMinimalScoreDisplay();
            EnsureOceanBoardFrame();
            StyleGameplayHudText();
            StylePremiumScoreText();
            StylePremiumBlitzTimer();
            EnsureBestScoreDisplay();
            EnsureNewBestFeedback();
            ResetNewBestFeedback();
            StyleGameplayButtons();
            EnsureOceanPauseMenu();
            DisableLegacyChainText();
            HideFeedback();
            ShowPause(false);
        }

        private void OnDestroy()
        {
            ThemeCatalog.ThemeChanged -= HandleThemeChanged;

            if (blitzUrgencyRoutine != null)
            {
                StopCoroutine(blitzUrgencyRoutine);
                blitzUrgencyRoutine = null;
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.MuteChanged -= OnMuteChanged;
            }
        }

        private void OnMuteChanged(bool muted)
        {
            RefreshMuteLabel();
            RefreshPauseLabels();
        }

        private void HandleThemeChanged(ThemeType requestedTheme, ThemeAssetSet resolvedTheme)
        {
            EnsureOceanBranding();
            EnsureOceanBoardFrame();
            ApplyBestScoreTheme(resolvedTheme);
            ApplySettingsTheme(resolvedTheme);
        }

        public void Refresh(GameMode mode, ScoreManager scoreManager, int highScore, float blitzSeconds, bool undoAvailable, int nextScoreMilestone)
        {
            Refresh(mode, scoreManager, highScore, blitzSeconds, undoAvailable, nextScoreMilestone, null);
        }

        public void Refresh(GameMode mode, ScoreManager scoreManager, int highScore, float blitzSeconds, bool undoAvailable, int nextScoreMilestone, int[] popTargetCounts)
        {
            if (modeText != null)
            {
                modeText.text = ModeName(mode);
            }

            if (scoreText != null)
            {
                SetScoreText(scoreManager.Score);
            }

            SetBestScoreTarget(highScore);

            DisableLegacyChainText();

            RefreshBlitzTimer(mode, blitzSeconds);

            if (undoButton != null)
            {
                undoButton.gameObject.SetActive(false);
            }

            if (muteButton != null)
            {
                muteButton.gameObject.SetActive(false);
            }

            if (menuButton != null)
            {
                menuButton.gameObject.SetActive(true);
            }

            if (scoreManager == null)
            {
                return;
            }

            if (chromaBars == null)
            {
                chromaBars = new ChromaBarView[0];
            }

            for (int i = 0; i < chromaBars.Length && i < GameConstants.ColorCount; i++)
            {
                ChromaColor color = (ChromaColor)i;
                bool ready = scoreManager.IsPopReady(color);
                int popTargetCount = popTargetCounts != null && i < popTargetCounts.Length ? Mathf.Max(0, popTargetCounts[i]) : 0;
                if (chromaBars[i] != null)
                {
                    chromaBars[i].Refresh(scoreManager.GetChroma(color), scoreManager.GetChroma01(color), ready, popTargetCount);
                }

                if (ready && popTargetCount > 0 && !announcedPopReady[i])
                {
                    announcedPopReady[i] = true;
                    ShowPopReady(color, popTargetCount);
                    PunchChroma(color);
                    Haptics.Light();
                }
                else if (!ready || popTargetCount <= 0)
                {
                    announcedPopReady[i] = false;
                }
            }

            if (rankProgressView != null && SaveManager.Instance != null)
            {
                rankProgressView.Refresh(SaveManager.Instance.Data.rankPoints);
            }
        }

        public void RefreshBlitzTimer(GameMode mode, float blitzSeconds)
        {
            bool visible = mode == GameMode.Blitz;
            if (blitzTimerCapsuleImage != null)
            {
                blitzTimerCapsuleImage.gameObject.SetActive(visible);
            }

            if (timerText == null)
            {
                return;
            }

            timerText.gameObject.SetActive(visible);
            if (!visible)
            {
                lastDisplayedBlitzSecond = -1;
                SetBlitzTimerUrgency(false);
                return;
            }

            int seconds = Mathf.Max(0, Mathf.CeilToInt(blitzSeconds));
            timerText.text = seconds.ToString();
            if (seconds == lastDisplayedBlitzSecond)
            {
                return;
            }

            ApplyBlitzTimerPalette(seconds);
            SetBlitzTimerUrgency(seconds <= 10);
            lastDisplayedBlitzSecond = seconds;
        }

        public void PunchScore(bool pure)
        {
            ApplyScorePunch(pure ? 0.10f : 0.06f, pure ? 0.22f : 0.19f);
        }

        public void PunchScore(ClearResult result, int chain)
        {
            int linesCleared = result == null ? 0 : result.linesCleared;
            bool premiumClear = linesCleared >= 3
                || (result != null && result.pureLines > 0)
                || chain >= 4;
            float amplitude = premiumClear ? 0.10f : linesCleared >= 2 ? 0.08f : 0.06f;
            float duration = premiumClear ? 0.22f : linesCleared >= 2 ? 0.20f : 0.18f;
            ApplyScorePunch(amplitude, duration);
        }

        private void ApplyScorePunch(float amplitude, float duration)
        {
            if (scoreText != null)
            {
                suppressNextScoreAutoPunch = true;
                scoreText.transform.DOKill();
                scoreText.transform.localScale = Vector3.one;
                scoreText.transform.DOPunchScale(Vector3.one * amplitude, duration, 6, 0.72f);
            }
        }

        public void ShowNewBestFeedback()
        {
            EnsureNewBestFeedback();
            if (newBestText == null)
            {
                return;
            }

            if (newBestRoutine != null)
            {
                StopCoroutine(newBestRoutine);
            }

            newBestRoutine = StartCoroutine(AnimateNewBestFeedback());
        }

        public void ResetNewBestFeedback()
        {
            if (newBestRoutine != null)
            {
                StopCoroutine(newBestRoutine);
                newBestRoutine = null;
            }

            if (newBestText != null)
            {
                newBestText.gameObject.SetActive(false);
                newBestText.transform.localScale = Vector3.one;
            }
        }

        public void PunchChroma(ChromaColor color)
        {
            int index = (int)color;
            if (chromaBars != null && index >= 0 && index < chromaBars.Length && chromaBars[index] != null)
            {
                chromaBars[index].transform.DOKill();
                chromaBars[index].transform.DOPunchScale(Vector3.one * 0.1f, 0.2f, 8, 0.75f);
            }
        }

        public void ShowClearFeedback(ClearResult result, int chain, int scoreAdded)
        {
            ShowClearFeedback(result, chain, scoreAdded, 0);
        }

        public void ShowClearFeedback(ClearResult result, int chain, int scoreAdded, int styleBonus)
        {
            ShowClearFeedback(result, chain, scoreAdded, styleBonus, Vector2.zero);
        }

        public void ShowClearFeedback(ClearResult result, int chain, int scoreAdded, int styleBonus, Vector2 clearScreenPosition)
        {
            ShowClearFeedback(result, chain, scoreAdded, styleBonus, clearScreenPosition, ChromaColor.Cyan);
        }

        public void ShowClearFeedback(
            ClearResult result,
            int chain,
            int scoreAdded,
            int styleBonus,
            Vector2 clearScreenPosition,
            ChromaColor placedPieceColor)
        {
            Color accentColor = ChromaPalette.GetColor(placedPieceColor);
            popupLayer?.ShowClear(result, chain, scoreAdded, styleBonus, clearScreenPosition, accentColor);
            if (result != null && result.linesCleared > 0)
            {
                EnsurePremiumLineClearFx();
                RectTransform boardRect = gameManager != null && gameManager.Board != null
                    ? gameManager.Board.transform as RectTransform
                    : null;
                premiumLineClearFx?.Play(boardRect, result, clearScreenPosition, accentColor, chain);
                float alpha = Mathf.Clamp(0.12f + result.linesCleared * 0.04f + result.pureLines * 0.05f + styleBonus * 0.00016f, 0.12f, 0.34f);
                Flash(Color.Lerp(accentColor, Color.white, 0.22f), alpha, 0.26f);
            }
        }

        private void DisableLegacyChainText()
        {
            if (chainText == null)
            {
                return;
            }

            chainText.transform.DOKill();
            chainText.transform.localScale = Vector3.one;
            chainText.gameObject.SetActive(false);
        }

        public void ShowPopFeedback(ChromaColor color, int popped, int scoreAdded)
        {
            popupLayer?.ShowPop(color, popped, scoreAdded);
            Color popFlash = Color.Lerp(ChromaPalette.GetColor(color), Color.white, 0.62f);
            Flash(popFlash, popped >= 8 ? 0.38f : 0.30f, 0.24f);
        }

        public void ShowPopReady(ChromaColor color, int targetCount)
        {
            Color popColor = ChromaPalette.GetColor(color);
            string colorName = GetEnglishColorName(color);
            popupLayer?.Show($"POP READY\n{colorName} x{targetCount}", popColor, 44, new Vector2(0f, 178f));
            ShowFeedback($"PRESS POP {colorName}", popColor, 1.05f);
            Flash(popColor, 0.12f, 0.18f);
        }

        public void ShowRewardedChromaReady(ChromaColor color)
        {
            Color popColor = ChromaPalette.GetColor(color);
            string colorName = GetEnglishColorName(color);
            popupLayer?.Show($"+CHROMA\n{colorName} POP READY", popColor, 46, new Vector2(0f, 205f));
            ShowFeedback($"{colorName} POP READY", popColor, 1.0f);
            Flash(popColor, 0.16f, 0.20f);
        }

        public void ShowPopUnavailable(ChromaColor color)
        {
            Color popColor = ChromaPalette.GetColor(color);
            ShowFeedback("POP HAS NO TILES", popColor, 0.58f);
        }

        public void ShowTimeBonus(float seconds)
        {
            int rounded = Mathf.RoundToInt(seconds);
            if (rounded <= 0)
            {
                return;
            }

            Color color = new Color(0.65f, 1f, 0.26f, 1f);
            popupLayer?.Show($"+{rounded}s", color, 46, new Vector2(0f, 90f));
            ShowFeedback($"+{rounded}s TIME", color, 0.42f);
        }

        public void ShowInvalidMove()
        {
            ShowFeedback("DOESN'T FIT", new Color(1f, 0.18f, 0.28f, 1f), 0.7f);
        }

        public void ShowNoFitPiece()
        {
            ShowFeedback("NO SPACE", new Color(1f, 0.62f, 0.18f, 1f), 0.55f);
        }

        public void ShowUsePopToContinue(ChromaColor color)
        {
            Color popColor = ChromaPalette.GetColor(color);
            popupLayer?.Show($"USE POP\n{GetEnglishColorName(color)}", popColor, 42, new Vector2(0f, 165f));
            ShowFeedback("POP CAN SAVE YOU", popColor, 0.9f);
        }

        public void SetMission(string mission, bool completed)
        {
            EnsureMissionText();
            HideMissionText();
        }

        public void ShowMissionComplete(int rewardScore)
        {
            Color color = new Color(0.65f, 1f, 0.26f, 1f);
            popupLayer?.Show($"MISSION COMPLETE\n+{rewardScore}", color, 48, new Vector2(0f, 25f));
            ShowFeedback($"MISSION +{rewardScore}", color, 0.65f);
            Flash(color, 0.18f, 0.22f);
            if (missionText != null)
            {
                HideMissionText();
            }
        }

        public void ShowAchievement(AchievementReward reward)
        {
            Color color = new Color(1f, 0.82f, 0.35f, 1f);
            string title = GetEnglishAchievementTitle(reward.id);
            popupLayer?.Show($"ACHIEVEMENT\n{title}\n+{reward.coins} COINS", color, 44, new Vector2(0f, 165f));
            ShowFeedback($"{title} +{reward.coins} COINS", color, 0.8f);
            Flash(color, 0.22f, 0.26f);
        }

        public void ShowScoreMilestone(int milestoneScore, int coins)
        {
            if (coins <= 0)
            {
                return;
            }

            Color color = new Color(1f, 0.82f, 0.35f, 1f);
            popupLayer?.Show($"SCORE {milestoneScore}\n+{coins} COINS", color, 44, new Vector2(0f, 220f));
            ShowFeedback($"BONUS +{coins} COINS", color, 0.72f);
            Flash(color, 0.16f, 0.22f);
        }

        public void ShowDailyQuestReward(string questName, int coins)
        {
            if (coins <= 0)
            {
                return;
            }

            Color color = new Color(1f, 0.82f, 0.35f, 1f);
            string title = GetEnglishDailyQuestTitle(questName);
            popupLayer?.Show($"{title}\n+{coins} COINS", color, 44, new Vector2(0f, 235f));
            ShowFeedback($"DAILY +{coins} COINS", color, 0.78f);
            Flash(color, 0.18f, 0.24f);
        }

        public void ShowSetupBonus(int scoreAdded)
        {
            if (scoreAdded <= 0)
            {
                return;
            }

            Color color = new Color(1f, 0.82f, 0.35f, 1f);
            Flash(color, 0.08f, 0.14f);
        }

        public void ShowLargePieceBonus(int cells, int scoreAdded)
        {
            if (scoreAdded <= 0)
            {
                return;
            }

            Color color = new Color(0.1f, 0.9f, 1f, 1f);
            Flash(color, 0.06f, 0.12f);
        }

        public void ShowBoardSweepBonus(int scoreAdded, bool boardEmpty)
        {
            if (scoreAdded <= 0)
            {
                return;
            }

            Color color = boardEmpty
                ? new Color(1f, 0.82f, 0.35f, 1f)
                : new Color(0.65f, 1f, 0.26f, 1f);
            Flash(color, boardEmpty ? 0.28f : 0.18f, 0.26f);
        }

        public void ShowPlacedFeedback()
        {
            ShowPlacedFeedback(0);
        }

        public void ShowPlacedFeedback(int scoreAdded)
        {
            if (scoreAdded > 0)
            {
                ShowFeedback($"+{scoreAdded}", new Color(0.1f, 0.9f, 1f, 1f), 0.28f);
            }
        }

        public void ShowPlacementPreview(int lineCount, int pureLineCount)
        {
            if (lineCount <= 0)
            {
                return;
            }

            Color color = pureLineCount > 0
                ? new Color(1f, 0.82f, 0.35f, 1f)
                : lineCount >= 3
                ? new Color(1f, 0.31f, 0.85f, 1f)
                : new Color(0.1f, 0.9f, 1f, 1f);

            string message;
            if (pureLineCount > 0)
            {
                message = pureLineCount == 1 ? "PURE!" : $"PURE x{pureLineCount}!";
            }
            else if (lineCount == 1)
            {
                message = "LINE!";
            }
            else if (lineCount == 2)
            {
                message = "DOUBLE!";
            }
            else
            {
                message = $"COMBO x{lineCount}!";
            }

            ShowFeedback(message, color, 0.36f);
        }

        public void ShowSmartMoveHint()
        {
            ShowFeedback("TRY HERE", new Color(0.65f, 1f, 0.26f, 1f), 0.58f);
        }

        public void ShowTrayCompleteBonus(int scoreAdded)
        {
            if (scoreAdded <= 0)
            {
                return;
            }

            Color color = new Color(0.65f, 1f, 0.26f, 1f);
            Flash(color, 0.10f, 0.16f);
        }

        public void ShowPause(bool visible)
        {
            bool blossomPause = IsBlossomSettingsActive();
            if (blossomPause && visible && !blossomScoreVisibilityCaptured)
            {
                blossomScoreWasActive = scoreText != null && scoreText.gameObject.activeSelf;
                blossomScoreShadowWasActive = scoreShadowText != null && scoreShadowText.gameObject.activeSelf;
                blossomScoreVisibilityCaptured = true;
                if (scoreText != null) scoreText.gameObject.SetActive(false);
                if (scoreShadowText != null) scoreShadowText.gameObject.SetActive(false);
            }

            bool beachPause = IsBeachSettingsActive();
            if (beachPause && visible && !beachScoreVisibilityCaptured)
            {
                beachScoreWasActive = scoreText != null && scoreText.gameObject.activeSelf;
                beachScoreShadowWasActive = scoreShadowText != null && scoreShadowText.gameObject.activeSelf;
                beachScoreVisibilityCaptured = true;
                if (scoreText != null) scoreText.gameObject.SetActive(false);
                if (scoreShadowText != null) scoreShadowText.gameObject.SetActive(false);
            }

            if (pauseRoot != null)
            {
                EnsureOceanPauseMenu();
                pauseRoot.SetActive(visible);
                if (visible)
                {
                    pauseRoot.transform.SetAsLastSibling();
                    AnimatePausePanelIn();
                }
            }

            if (visible)
            {
                RefreshPauseLabels();
            }
            else if (blossomScoreVisibilityCaptured)
            {
                if (scoreText != null) scoreText.gameObject.SetActive(blossomScoreWasActive);
                if (scoreShadowText != null) scoreShadowText.gameObject.SetActive(blossomScoreShadowWasActive);
                blossomScoreVisibilityCaptured = false;
            }
            else if (beachScoreVisibilityCaptured)
            {
                if (scoreText != null) scoreText.gameObject.SetActive(beachScoreWasActive);
                if (scoreShadowText != null) scoreShadowText.gameObject.SetActive(beachScoreShadowWasActive);
                beachScoreVisibilityCaptured = false;
            }
        }

        private void ToggleMusicFromSettings()
        {
            AudioManager audio = AudioManager.Instance;
            if (audio != null)
            {
                audio.PlayToggle();
                audio.ToggleMusic();
            }
            else
            {
                bool currentlyOn = PlayerPrefs.GetInt("MusicEnabled", 1) != 0;
                PlayerPrefs.SetInt("MusicEnabled", currentlyOn ? 0 : 1);
                PlayerPrefs.Save();
            }

            RefreshPauseLabels();
            UpdateSettingsToggleVisuals(false, false);
        }

        private void ToggleSoundFromSettings()
        {
            bool soundOn;
            if (AudioManager.Instance != null)
            {
                AudioManager audio = AudioManager.Instance;
                if (audio.Muted)
                {
                    audio.ToggleMute();
                    audio.PlayToggle();
                }
                else
                {
                    audio.PlayToggle();
                    audio.ToggleMute();
                }

                soundOn = !audio.Muted;
            }
            else
            {
                bool currentlyOn = PlayerPrefs.GetInt("SoundEnabled", AudioListener.volume > 0.001f ? 1 : 0) != 0;
                soundOn = !currentlyOn;
                AudioListener.volume = soundOn ? 1f : 0f;
                PlayerPrefs.SetInt("SoundEnabled", soundOn ? 1 : 0);
                PlayerPrefs.Save();
            }

            Debug.Log($"Sound toggled: {(soundOn ? "ON" : "OFF")}");
            PlayerPrefs.SetInt("SoundEnabled", soundOn ? 1 : 0);
            PlayerPrefs.Save();
            RefreshMuteLabel();
            RefreshPauseLabels();
            UpdateSettingsToggleVisuals(true, false);
        }

        private void ToggleVibrationFromSettings()
        {
            bool vibrationOn = !Haptics.IsEnabled();
            Haptics.SetEnabled(vibrationOn);
            AudioManager.Instance?.PlayToggle();

            if (vibrationOn)
            {
                Haptics.Light();
            }

            RefreshPauseLabels();
            UpdateSettingsToggleVisuals(false, true);
        }

        private void EnsurePremiumLineClearFx()
        {
            if (premiumLineClearFx != null)
            {
                return;
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            RectTransform parentRect = canvas != null
                ? canvas.transform as RectTransform
                : transform as RectTransform;
            if (parentRect == null)
            {
                return;
            }

            Transform existing = parentRect.Find("PremiumLineClearFx");
            if (existing != null)
            {
                premiumLineClearFx = existing.GetComponent<PremiumLineClearFx>();
            }

            if (premiumLineClearFx == null)
            {
                GameObject layerObject = new GameObject("PremiumLineClearFx", typeof(RectTransform), typeof(PremiumLineClearFx));
                layerObject.transform.SetParent(parentRect, false);
                RectTransform layerRect = (RectTransform)layerObject.transform;
                layerRect.anchorMin = Vector2.zero;
                layerRect.anchorMax = Vector2.one;
                layerRect.pivot = new Vector2(0.5f, 0.5f);
                layerRect.offsetMin = Vector2.zero;
                layerRect.offsetMax = Vector2.zero;
                premiumLineClearFx = layerObject.GetComponent<PremiumLineClearFx>();
            }

            RectTransform effectRect = premiumLineClearFx.transform as RectTransform;
            premiumLineClearFx.Initialize(effectRect);
        }

        private void Flash(Color color, float alpha, float duration)
        {
            if (MobilePerformance.LowEndMode)
            {
                return;
            }

            EnsureScreenFlash();
            if (screenFlashImage == null)
            {
                return;
            }

            screenFlashImage.gameObject.SetActive(true);
            screenFlashImage.transform.DOKill();
            color.a = Mathf.Clamp01(alpha);
            screenFlashImage.color = color;
            screenFlashImage.DOFade(0f, Mathf.Max(0.05f, duration)).SetEase(Ease.OutQuad).OnComplete(() =>
            {
                if (screenFlashImage != null)
                {
                    screenFlashImage.gameObject.SetActive(false);
                }
            });
        }

        private void EnsureScreenFlash()
        {
            if (screenFlashImage == null)
            {
                Transform existing = transform.Find("ScreenFlash");
                if (existing != null)
                {
                    screenFlashImage = existing.GetComponent<Image>();
                }
            }

            if (screenFlashImage == null)
            {
                RectTransform parentRect = transform as RectTransform;
                if (parentRect == null)
                {
                    return;
                }

                GameObject flashObject = new GameObject("ScreenFlash", typeof(RectTransform), typeof(Image));
                flashObject.transform.SetParent(parentRect, false);
                RectTransform rect = (RectTransform)flashObject.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                screenFlashImage = flashObject.GetComponent<Image>();
            }

            screenFlashImage.raycastTarget = false;
            screenFlashImage.color = new Color(1f, 1f, 1f, 0f);
            screenFlashImage.transform.SetAsLastSibling();
            screenFlashImage.gameObject.SetActive(false);
        }

        private void EnsureOceanBranding()
        {
            RectTransform hudRect = transform as RectTransform;
            if (hudRect == null)
            {
                return;
            }

            ThemeAssetSet activeTheme = ThemeCatalog.Current;
            Sprite backgroundSprite = activeTheme == null ? null : activeTheme.GameplayBackground;
            if (backgroundSprite == null)
            {
                backgroundSprite = LoadOceanSprite(OceanBackgroundPath);
            }
            DisableGameplayLogo(hudRect);

            if (backgroundSprite != null)
            {
                oceanBackgroundImage = EnsureImageLayer(oceanBackgroundImage, "OceanBackground", hudRect);
                if (oceanBackgroundImage != null)
                {
                    oceanBackgroundImage.sprite = backgroundSprite;
                    oceanBackgroundImage.color = Color.white;
                    oceanBackgroundImage.type = Image.Type.Simple;
                    oceanBackgroundImage.preserveAspect = true;
                    oceanBackgroundImage.raycastTarget = false;
                    StretchToFullPortrait(oceanBackgroundImage.rectTransform);
                    ConfigureAspectCover(oceanBackgroundImage, backgroundSprite);
                    oceanBackgroundImage.transform.SetAsFirstSibling();
                }
            }

        }

        private void EnsureOceanBoardFrame()
        {
            RectTransform boardRoot = gameManager == null || gameManager.Board == null ? null : gameManager.Board.BoardRoot;
            if (boardRoot == null)
            {
                return;
            }

            boardRoot.anchorMin = new Vector2(0.5f, 0.50f);
            boardRoot.anchorMax = boardRoot.anchorMin;
            boardRoot.pivot = new Vector2(0.5f, 0.5f);
            boardRoot.anchoredPosition = Vector2.zero;
            boardRoot.sizeDelta = new Vector2(960f, 960f);
            boardRoot.localScale = Vector3.one;

            finalBoardVisualImage = EnsureImageLayer(finalBoardVisualImage, "FinalBoardVisual", boardRoot);
            if (finalBoardVisualImage == null)
            {
                return;
            }

            ThemeAssetSet activeTheme = ThemeCatalog.Current;
            finalBoardVisualImage.sprite = activeTheme == null ? null : activeTheme.BoardSurfaceSprite;
            if (finalBoardVisualImage.sprite == null)
            {
                finalBoardVisualImage.sprite = LoadOceanSprite(FinalBoardVisualPath);
            }
            finalBoardVisualImage.color = Color.white;
            finalBoardVisualImage.type = Image.Type.Simple;
            finalBoardVisualImage.preserveAspect = true;
            finalBoardVisualImage.fillCenter = true;
            finalBoardVisualImage.enabled = finalBoardVisualImage.sprite != null;
            finalBoardVisualImage.raycastTarget = false;

            RectTransform visualRect = finalBoardVisualImage.rectTransform;
            visualRect.anchorMin = Vector2.zero;
            visualRect.anchorMax = Vector2.one;
            visualRect.pivot = new Vector2(0.5f, 0.5f);
            visualRect.offsetMin = Vector2.zero;
            visualRect.offsetMax = Vector2.zero;
            visualRect.anchoredPosition = Vector2.zero;
            visualRect.localScale = Vector3.one;
            visualRect.SetAsFirstSibling();

            // Soft drop shadow behind the board panel so it reads as floating
            // above the ocean background, like the reference. Drawn first so
            // every other board child renders on top of it.
            finalBoardShadowImage = EnsureImageLayer(finalBoardShadowImage, "FinalBoardDropShadow", boardRoot);
            if (finalBoardShadowImage != null)
            {
                UISpriteFactory.ApplySoftShadow(finalBoardShadowImage, 0.16f);
                finalBoardShadowImage.color = new Color(0f, 0.06f, 0.20f, 0.38f);
                finalBoardShadowImage.raycastTarget = false;
                finalBoardShadowImage.enabled = true;

                RectTransform shadowRect = finalBoardShadowImage.rectTransform;
                shadowRect.anchorMin = Vector2.zero;
                shadowRect.anchorMax = Vector2.one;
                shadowRect.pivot = new Vector2(0.5f, 0.5f);
                shadowRect.offsetMin = new Vector2(-18f, -36f);
                shadowRect.offsetMax = new Vector2(18f, 0f);
                shadowRect.localScale = Vector3.one;
                shadowRect.SetAsFirstSibling();
            }

            DisableLegacyFinalBoardLayers(boardRoot, visualRect);
            DisableLegacyBoardFrameDecorators(boardRoot);
        }

        private static void DisableLegacyFinalBoardLayers(RectTransform boardRoot, RectTransform activeVisual)
        {
            for (int i = 0; i < boardRoot.childCount; i++)
            {
                Transform child = boardRoot.GetChild(i);
                if (child == null || child == activeVisual)
                {
                    continue;
                }

                if (child.name == "FinalBoardBacking" ||
                    child.name == "FinalBoardOutline" ||
                    child.name == "OceanBoardFrame")
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private void DisableProceduralBoardFrameChildren(RectTransform frameRoot)
        {
            DisableFrameChild(frameRoot, "OceanBoardFrameShadow");
            DisableFrameChild(frameRoot, "OceanBoardFramePlate");
            DisableFrameChild(frameRoot, "OceanBoardFrameOuterBorder");
            DisableFrameChild(frameRoot, "OceanBoardFrameInnerHighlight");
            DisableFrameChild(frameRoot, "OceanBoardFrameTopGloss");
            DisableOrnateOceanFrameChildren(frameRoot);
        }

        private void DisableGameplayLogo(RectTransform hudRect)
        {
            Transform existing = hudRect.Find("OceanLogo");
            if (existing != null)
            {
                existing.gameObject.SetActive(false);
            }
        }

        private void ConfigureMinimalScoreDisplay()
        {
            if (scoreText == null)
            {
                return;
            }

            RectTransform scoreRect = scoreText.transform as RectTransform;
            RectTransform parentRect = scoreRect == null ? null : scoreRect.parent as RectTransform;
            if (parentRect == null)
            {
                return;
            }

            string[] obsoleteLayerNames =
            {
                "OceanScorePanel",
                "OceanScorePulseGlow",
                "GameplayScoreLabel"
            };
            for (int i = 0; i < obsoleteLayerNames.Length; i++)
            {
                Transform obsoleteLayer = parentRect.Find(obsoleteLayerNames[i]);
                if (obsoleteLayer != null)
                {
                    obsoleteLayer.gameObject.SetActive(false);
                }
            }

            scoreRect.anchorMin = new Vector2(0.5f, 0.835f);
            scoreRect.anchorMax = scoreRect.anchorMin;
            scoreRect.pivot = new Vector2(0.5f, 0.5f);
            scoreRect.anchoredPosition = Vector2.zero;
            scoreRect.sizeDelta = new Vector2(820f, 205f);
            scoreRect.localScale = Vector3.one;

            RectTransform shadowRect = GetOrCreateChildRect(parentRect, "ScoreShadowText");
            shadowRect.anchorMin = scoreRect.anchorMin;
            shadowRect.anchorMax = scoreRect.anchorMax;
            shadowRect.pivot = scoreRect.pivot;
            shadowRect.anchoredPosition = new Vector2(5f, -8f);
            shadowRect.sizeDelta = scoreRect.sizeDelta;
            shadowRect.localScale = Vector3.one;
            scoreShadowText = shadowRect.GetComponent<TMP_Text>();
            if (scoreShadowText == null)
            {
                scoreShadowText = shadowRect.gameObject.AddComponent<TextMeshProUGUI>();
            }

            scoreShadowText.raycastTarget = false;
            shadowRect.SetSiblingIndex(Mathf.Max(0, scoreRect.GetSiblingIndex()));
            scoreText.transform.SetAsLastSibling();
        }

        private void EnsureNewBestFeedback()
        {
            if (newBestText != null || scoreText == null)
            {
                return;
            }

            RectTransform scoreRect = scoreText.transform as RectTransform;
            RectTransform parentRect = scoreRect == null ? null : scoreRect.parent as RectTransform;
            if (parentRect == null)
            {
                return;
            }

            RectTransform textRect = GetOrCreateChildRect(parentRect, "GameplayNewBestText");
            textRect.anchorMin = scoreRect.anchorMin;
            textRect.anchorMax = scoreRect.anchorMax;
            textRect.pivot = scoreRect.pivot;
            textRect.anchoredPosition = scoreRect.anchoredPosition + new Vector2(0f, -78f);
            textRect.sizeDelta = new Vector2(440f, 54f);
            textRect.localScale = Vector3.one;

            newBestText = GetOrAddText(textRect.gameObject);
            newBestText.text = "NEW BEST!";
            newBestText.alignment = TextAlignmentOptions.Center;
            newBestText.fontStyle = FontStyles.Bold;
            newBestText.fontSize = 32f;
            newBestText.fontSizeMax = 32f;
            newBestText.fontSizeMin = 22f;
            newBestText.enableAutoSizing = true;
            newBestText.characterSpacing = 0f;
            newBestText.color = new Color(1f, 0.88f, 0.42f, 0f);
            newBestText.raycastTarget = false;
            EnsureTextShadow(newBestText, new Color(0f, 0.03f, 0.10f, 0.78f), new Vector2(0f, -2f));
            newBestText.gameObject.SetActive(false);
        }

        private IEnumerator AnimateNewBestFeedback()
        {
            newBestText.gameObject.SetActive(true);
            RectTransform textRect = newBestText.transform as RectTransform;
            Image crownGlow = bestScoreHudRoot == null
                ? null
                : bestScoreHudRoot.Find("CrownGlow")?.GetComponent<Image>();
            Color crownGlowBase = crownGlow == null ? Color.clear : crownGlow.color;
            Color textColor = new Color(1f, 0.88f, 0.42f, 1f);
            const float duration = 0.92f;
            float elapsed = 0f;
            while (elapsed < duration && newBestText != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float intro = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.20f));
                float fade = 1f - Mathf.SmoothStep(0.62f, 1f, t);
                float scale = t < 0.18f
                    ? Mathf.Lerp(0.88f, 1.08f, t / 0.18f)
                    : Mathf.Lerp(1.08f, 1f, Mathf.Clamp01((t - 0.18f) / 0.24f));

                textColor.a = intro * fade;
                newBestText.color = textColor;
                if (textRect != null)
                {
                    textRect.localScale = Vector3.one * scale;
                }

                if (crownGlow != null)
                {
                    Color glowColor = crownGlowBase;
                    glowColor.a = crownGlowBase.a + Mathf.Sin(t * Mathf.PI) * 0.22f;
                    crownGlow.color = glowColor;
                }

                yield return null;
            }

            if (crownGlow != null)
            {
                crownGlow.color = crownGlowBase;
            }

            if (newBestText != null)
            {
                newBestText.gameObject.SetActive(false);
                newBestText.transform.localScale = Vector3.one;
            }

            newBestRoutine = null;
        }

        private void EnsureBestScoreDisplay()
        {
            if (highScoreText == null)
            {
                return;
            }

            RectTransform hudRect = transform as RectTransform;
            if (hudRect == null)
            {
                return;
            }

            bestScoreHudRoot = GetOrCreateChildRect(hudRect, "BestScoreHud");
            bestScoreHudRoot.anchorMin = new Vector2(0f, 1f);
            bestScoreHudRoot.anchorMax = bestScoreHudRoot.anchorMin;
            bestScoreHudRoot.pivot = new Vector2(0f, 1f);
            bestScoreHudRoot.anchoredPosition = new Vector2(34f, -50f);
            bestScoreHudRoot.sizeDelta = new Vector2(350f, 132f);
            bestScoreHudRoot.localScale = Vector3.one;
            bestScoreHudRoot.gameObject.SetActive(true);
            bestScoreHudRoot.SetAsLastSibling();

            CanvasGroup bestScoreCanvasGroup = bestScoreHudRoot.GetComponent<CanvasGroup>();
            if (bestScoreCanvasGroup == null)
            {
                bestScoreCanvasGroup = bestScoreHudRoot.gameObject.AddComponent<CanvasGroup>();
            }

            bestScoreCanvasGroup.alpha = 1f;
            bestScoreCanvasGroup.interactable = false;
            bestScoreCanvasGroup.blocksRaycasts = false;

            RectTransform glowRect = GetOrCreateChildRect(bestScoreHudRoot, "CapsuleGlow");
            ConfigureHudLayerRect(glowRect, Vector2.zero, Vector2.one, new Vector2(-7f, -7f), new Vector2(7f, 7f));
            Image glow = GetOrAddImage(glowRect.gameObject);
            glow.enabled = false;

            RectTransform shadowRect = GetOrCreateChildRect(bestScoreHudRoot, "CapsuleShadow");
            ConfigureHudLayerRect(shadowRect, Vector2.zero, Vector2.one, new Vector2(-4f, -14f), new Vector2(4f, -2f));
            Image capsuleShadow = GetOrAddImage(shadowRect.gameObject);
            UISpriteFactory.ApplySoftShadow(capsuleShadow, 0.50f);
            capsuleShadow.color = new Color(0.01f, 0.08f, 0.26f, 0.45f);
            capsuleShadow.raycastTarget = false;
            capsuleShadow.enabled = true;

            // Real art replaces the procedural glass-pill assembly below: border
            // rim, flat backdrop fill and gloss sheen are all baked into
            // BestScoreCapsule.png now, so those layers stay off to avoid a
            // duplicated look. CapsuleShadow above is kept -- it's a drop shadow
            // under the pill, not part of the pill face, so it still adds to it.
            RectTransform borderRect = GetOrCreateChildRect(bestScoreHudRoot, "CapsuleBorder");
            Image border = GetOrAddImage(borderRect.gameObject);
            border.enabled = false;

            RectTransform backdropRect = GetOrCreateChildRect(bestScoreHudRoot, "Backdrop");
            Image backdrop = GetOrAddImage(backdropRect.gameObject);
            backdrop.enabled = false;

            RectTransform glossRect = GetOrCreateChildRect(bestScoreHudRoot, "CapsuleGloss");
            Image capsuleGloss = GetOrAddImage(glossRect.gameObject);
            capsuleGloss.enabled = false;

            RectTransform capsuleArtRect = GetOrCreateChildRect(bestScoreHudRoot, "CapsuleArt");
            // The supplied capsule uses a 1536x1024 canvas while its visible pill
            // occupies only the middle band. Oversize the image rect so that the
            // baked transparent padding stays outside the 320x108 HUD footprint
            // and the original artwork itself is actually visible at the intended
            // size. No source pixels are cropped or recoloured.
            capsuleArtRect.anchorMin = new Vector2(0.5f, 0.5f);
            capsuleArtRect.anchorMax = capsuleArtRect.anchorMin;
            capsuleArtRect.pivot = new Vector2(0.5f, 0.5f);
            capsuleArtRect.anchoredPosition = Vector2.zero;
            capsuleArtRect.sizeDelta = new Vector2(358f, 238f);
            capsuleArtRect.localScale = Vector3.one;
            bestScoreCapsuleImage = GetOrAddImage(capsuleArtRect.gameObject);
            Sprite capsuleSprite = LoadOceanSprite(BestScoreCapsulePath);
            bestScoreCapsuleImage.sprite = capsuleSprite;
            bestScoreCapsuleImage.enabled = capsuleSprite != null;
            bestScoreCapsuleImage.type = Image.Type.Simple;
            bestScoreCapsuleImage.preserveAspect = true;
            bestScoreCapsuleImage.raycastTarget = false;

            RectTransform crownGlowRect = GetOrCreateChildRect(bestScoreHudRoot, "CrownGlow");
            crownGlowRect.anchorMin = new Vector2(0f, 0.5f);
            crownGlowRect.anchorMax = crownGlowRect.anchorMin;
            crownGlowRect.pivot = new Vector2(0.5f, 0.5f);
            crownGlowRect.anchoredPosition = new Vector2(62f, -6.82f);
            crownGlowRect.sizeDelta = new Vector2(90f, 90f);
            crownGlowRect.localScale = Vector3.one;
            bestScoreCrownGlowImage = GetOrAddImage(crownGlowRect.gameObject);
            if (bestScoreCrownGlowImage != null)
            {
                UISpriteFactory.ApplySoftCircle(bestScoreCrownGlowImage);
                bestScoreCrownGlowImage.raycastTarget = false;
            }

            RectTransform crownRect = GetOrCreateChildRect(bestScoreHudRoot, "CrownIcon");
            crownRect.anchorMin = new Vector2(0f, 0.5f);
            crownRect.anchorMax = crownRect.anchorMin;
            crownRect.pivot = new Vector2(0.5f, 0.5f);
            crownRect.anchoredPosition = new Vector2(62f, -6.82f);
            // BestScoreHud local bounds are x [0, 350], y [-132, 0]. The 110 px crown
            // rect is centered at (62, -72.82), giving bounds x [7, 117] and
            // y [-127.82, -17.82], with at least 4.18 px of capsule margin.
            // CrownIcon's opaque artwork is centered 6.82 UI px above its texture
            // center at this size, so the compensation puts its visible center at
            // y=-66, exactly matching BestScoreText's visual/RectTransform center.
            crownRect.sizeDelta = new Vector2(110f, 110f);
            crownRect.localScale = Vector3.one;
            bestScoreCrownImage = GetOrAddImage(crownRect.gameObject);
            if (bestScoreCrownImage != null)
            {
                Sprite crownSprite = LoadOceanSprite(BestScoreCrownPath);
                bestScoreCrownImage.sprite = crownSprite;
                bestScoreCrownImage.enabled = crownSprite != null;
                bestScoreCrownImage.preserveAspect = true;
                bestScoreCrownImage.raycastTarget = false;
            }

            RectTransform valueRect = highScoreText.transform as RectTransform;
            valueRect.SetParent(bestScoreHudRoot, false);
            valueRect.anchorMin = new Vector2(0f, 0f);
            valueRect.anchorMax = new Vector2(1f, 1f);
            valueRect.pivot = new Vector2(0.5f, 0.5f);
            valueRect.offsetMin = new Vector2(120f, 3f);
            valueRect.offsetMax = new Vector2(-10f, -3f);
            valueRect.localScale = Vector3.one;
            highScoreText.alignment = TextAlignmentOptions.Center;
            highScoreText.fontStyle = FontStyles.Bold;
            TMP_FontAsset bestScoreFont = Resources.Load<TMP_FontAsset>(ScoreFontPath);
            if (bestScoreFont != null)
            {
                highScoreText.font = bestScoreFont;
            }
            highScoreText.enableAutoSizing = true;
            highScoreText.fontSize = 56f;
            highScoreText.fontSizeMax = 56f;
            highScoreText.fontSizeMin = 34f;
            highScoreText.enableVertexGradient = false;
            highScoreText.color = Color.white;
            highScoreText.enabled = true;
            highScoreText.gameObject.SetActive(true);
            highScoreText.raycastTarget = false;
            EnsureTextShadow(highScoreText, new Color(0f, 0.025f, 0.09f, 0.82f), new Vector2(0f, -2f));

            glowRect.SetAsFirstSibling();
            shadowRect.SetSiblingIndex(1);
            borderRect.SetSiblingIndex(2);
            backdropRect.SetSiblingIndex(3);
            glossRect.SetSiblingIndex(4);
            capsuleArtRect.SetSiblingIndex(5);
            crownGlowRect.SetSiblingIndex(6);
            crownRect.SetSiblingIndex(7);
            valueRect.SetAsLastSibling();
            ApplyBestScoreTheme(ThemeCatalog.Current);
        }

        private void ApplyBestScoreTheme(ThemeAssetSet theme)
        {
            Sprite capsuleSprite = theme == null ? null : theme.CapsuleSprite;

            if (bestScoreCapsuleImage != null)
            {
                bestScoreCapsuleImage.material = null;
                bestScoreCapsuleImage.color = Color.white;
                bestScoreCapsuleImage.preserveAspect = true;
                bestScoreCapsuleImage.sprite = capsuleSprite != null
                    ? capsuleSprite
                    : LoadOceanSprite(BestScoreCapsulePath);
                bestScoreCapsuleImage.enabled = bestScoreCapsuleImage.sprite != null;
            }

            if (bestScoreCrownImage != null)
            {
                bestScoreCrownImage.material = null;
                bestScoreCrownImage.color = Color.white;
                bestScoreCrownImage.preserveAspect = true;
                bestScoreCrownImage.sprite = LoadOceanSprite(BestScoreCrownPath);
                bestScoreCrownImage.enabled = bestScoreCrownImage.sprite != null;
            }

            if (bestScoreCrownGlowImage != null)
            {
                bestScoreCrownGlowImage.color = new Color(1f, 0.82f, 0.24f, 0.10f);
            }

            if (blitzTimerCapsuleImage != null)
            {
                blitzTimerCapsuleImage.material = null;
                blitzTimerCapsuleImage.color = Color.white;
                blitzTimerCapsuleImage.preserveAspect = true;
                blitzTimerCapsuleImage.sprite = capsuleSprite != null
                    ? capsuleSprite
                    : LoadOceanSprite(BestScoreCapsulePath);
                blitzTimerCapsuleImage.enabled = blitzTimerCapsuleImage.sprite != null;
            }
        }

        private static void ConfigureHudLayerRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void StyleRoundedHudImage(Image image, Color color)
        {
            if (image == null)
            {
                return;
            }

            UISpriteFactory.ApplyRounded(image, 0.50f);
            image.color = color;
            image.raycastTarget = false;
        }

        private void ConfigureSimpleOceanBoardFrame(RectTransform frameRoot)
        {
            DisableOrnateOceanFrameChildren(frameRoot);

            ConfigureRoundedFrameLayer(
                EnsureFrameLayer(frameRoot, "OceanBoardFrameShadow"),
                new Color(0f, 0.055f, 0.16f, 0.38f),
                new Vector2(-7f, -11f),
                new Vector2(7f, 5f),
                OceanBoardFrameRadius,
                false,
                0f);

            ConfigureRoundedFrameLayer(
                EnsureFrameLayer(frameRoot, "OceanBoardFramePlate"),
                new Color(0.49f, 0.84f, 0.96f, 0.98f),
                Vector2.zero,
                Vector2.zero,
                OceanBoardFrameRadius,
                false,
                0f);

            ConfigureRoundedFrameLayer(
                EnsureFrameLayer(frameRoot, "OceanBoardFrameOuterBorder"),
                new Color(0.17f, 0.83f, 1f, 0.98f),
                Vector2.zero,
                Vector2.zero,
                OceanBoardFrameRadius,
                true,
                0.075f);

            ConfigureRoundedFrameLayer(
                EnsureFrameLayer(frameRoot, "OceanBoardFrameInnerHighlight"),
                new Color(0.92f, 1f, 1f, 0.62f),
                new Vector2(7f, 7f),
                new Vector2(-7f, -7f),
                0.28f,
                true,
                0.028f);

            DisableFrameChild(frameRoot, "OceanBoardFrameTopGloss");
        }

        private void DisableOrnateOceanFrameChildren(RectTransform frameRoot)
        {
            DisableFrameChild(frameRoot, "OceanBoardFrameTop");
            DisableFrameChild(frameRoot, "OceanBoardFrameBottom");
            DisableFrameChild(frameRoot, "OceanBoardFrameLeft");
            DisableFrameChild(frameRoot, "OceanBoardFrameRight");
        }

        private void DisableFrameChild(RectTransform frameRoot, string childName)
        {
            Transform child = frameRoot.Find(childName);
            if (child != null)
            {
                child.gameObject.SetActive(false);
            }
        }

        private Image EnsureFrameLayer(RectTransform frameRoot, string layerName)
        {
            Transform existing = frameRoot.Find(layerName);
            Image image = existing == null ? null : existing.GetComponent<Image>();
            if (image != null)
            {
                image.gameObject.SetActive(true);
                return image;
            }

            GameObject layer = new GameObject(layerName, typeof(RectTransform), typeof(Image));
            layer.transform.SetParent(frameRoot, false);
            return layer.GetComponent<Image>();
        }

        private void ConfigureRoundedFrameLayer(Image image, Color color, Vector2 offsetMin, Vector2 offsetMax, float radius, bool frameOnly, float thickness)
        {
            if (image == null)
            {
                return;
            }

            image.enabled = true;
            image.color = color;
            image.raycastTarget = false;

            if (frameOnly)
            {
                UISpriteFactory.ApplyFrame(image, radius, thickness);
                image.fillCenter = false;
            }
            else
            {
                UISpriteFactory.ApplyRounded(image, radius);
                image.fillCenter = true;
            }

            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private void DisableDuplicateOceanBoardFrames(RectTransform boardRoot, RectTransform activeFrame)
        {
            bool keptActiveFrame = false;
            for (int i = 0; i < boardRoot.childCount; i++)
            {
                Transform child = boardRoot.GetChild(i);
                if (child == null || (child.name != "OceanBoardFrame" && child.name != "FinalBoardOutline"))
                {
                    continue;
                }

                bool keep = child == activeFrame && !keptActiveFrame;
                child.gameObject.SetActive(keep);
                keptActiveFrame |= keep;
            }
        }

        private void DisableLegacyBoardFrameDecorators(RectTransform boardRoot)
        {
            Image legacyBoardImage = boardRoot.GetComponent<Image>();
            if (legacyBoardImage != null && legacyBoardImage != finalBoardVisualImage)
            {
                legacyBoardImage.enabled = false;
            }

            Shadow[] frameEffects = boardRoot.GetComponents<Shadow>();
            for (int i = 0; i < frameEffects.Length; i++)
            {
                if (frameEffects[i] != null)
                {
                    frameEffects[i].enabled = false;
                }
            }

            Outline[] frameOutlines = boardRoot.GetComponents<Outline>();
            for (int i = 0; i < frameOutlines.Length; i++)
            {
                if (frameOutlines[i] != null)
                {
                    frameOutlines[i].enabled = false;
                }
            }
        }

        private Sprite LoadOceanSprite(string resourcesPath)
        {
            Sprite sprite = Resources.Load<Sprite>(resourcesPath);
            if (sprite == null)
            {
                Debug.LogError($"Missing Ocean sprite at Resources path: {resourcesPath}");
            }

            return sprite;
        }

        private Image EnsureImageLayer(Image image, string layerName, RectTransform parent)
        {
            if (image == null)
            {
                Transform existing = parent.Find(layerName);
                image = existing == null ? null : existing.GetComponent<Image>();
            }

            if (image != null)
            {
                return image;
            }

            GameObject layer = new GameObject(layerName, typeof(RectTransform), typeof(Image));
            layer.transform.SetParent(parent, false);
            return layer.GetComponent<Image>();
        }

        private RectTransform EnsureRectLayer(RectTransform rect, string layerName, RectTransform parent)
        {
            if (rect == null)
            {
                Transform existing = parent.Find(layerName);
                rect = existing == null ? null : existing as RectTransform;
            }

            if (rect != null)
            {
                rect.gameObject.SetActive(true);
                return rect;
            }

            GameObject layer = new GameObject(layerName, typeof(RectTransform));
            layer.transform.SetParent(parent, false);
            return (RectTransform)layer.transform;
        }

        private void StretchToFullPortrait(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);

            if (Screen.width <= 0 || Screen.height <= 0)
            {
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                return;
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            float scaleFactor = canvas == null ? 1f : Mathf.Max(0.01f, canvas.scaleFactor);
            Rect safeArea = Screen.safeArea;
            rect.offsetMin = new Vector2(-safeArea.xMin / scaleFactor, -safeArea.yMin / scaleFactor);
            rect.offsetMax = new Vector2((Screen.width - safeArea.xMax) / scaleFactor, (Screen.height - safeArea.yMax) / scaleFactor);
        }

        private void RefreshPauseLabels()
        {
            if (pauseMuteButtonText == null && pauseMuteButton != null)
            {
                pauseMuteButtonText = pauseMuteButton.GetComponentInChildren<TMP_Text>();
            }

            if (pauseHapticsButtonText == null && pauseHapticsButton != null)
            {
                pauseHapticsButtonText = pauseHapticsButton.GetComponentInChildren<TMP_Text>();
            }

            if (pauseMuteButtonText != null)
            {
                pauseMuteButtonText.gameObject.SetActive(IsSettingsToggleStateLabel(pauseMuteButtonText));
            }

            if (pauseHapticsButtonText != null)
            {
                pauseHapticsButtonText.gameObject.SetActive(IsSettingsToggleStateLabel(pauseHapticsButtonText));
            }

            UpdateSettingsToggleVisuals(false);
        }

        private void EnsureOceanPauseMenu()
        {
            try
            {
                EnsureOceanPauseMenuSafe();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Settings popup setup skipped to keep gameplay running: {ex.Message}");
            }
        }

        private void EnsureOceanPauseMenuSafe()
        {
            RectTransform pauseRect = pauseRoot == null ? null : pauseRoot.transform as RectTransform;
            if (pauseRect == null)
            {
                return;
            }

            pauseOverlayImage = pauseRoot.GetComponent<Image>();
            if (pauseOverlayImage != null)
            {
                pauseOverlayImage.enabled = true;
                pauseOverlayImage.sprite = null;
                pauseOverlayImage.color = new Color(0f, 0f, 0f, 0f);
                pauseOverlayImage.raycastTarget = false;
            }

            StretchToFullPortrait(pauseRect);
            EnsurePauseDimOverlay(pauseRect);
            DisableObsoletePauseBackplates(pauseRect);

            pausePanelRoot = FindPausePanel(pauseRect) ?? GetOrCreateChildRect(pauseRect, "PausePanel");

            pausePanelRoot.gameObject.SetActive(true);
            pausePanelRoot.anchorMin = new Vector2(0.5f, 0.5f);
            pausePanelRoot.anchorMax = new Vector2(0.5f, 0.5f);
            pausePanelRoot.anchoredPosition = Vector2.zero;
            ApplySettingsPanelGeometry();
            pausePanelRoot.localScale = Vector3.one;
            pausePanelRoot.pivot = new Vector2(0.5f, 0.5f);

            ConfigureSettingsPanelSprite();
            ConfigureCandySettingsBottomDecoration();
            ConfigureSettingsTitle();
            DisableGeneratedPauseDecor();
            DisableObsoletePausePanelBackplates();
            ConfigureSettingsHeaderAccent();
            DisableLegacyPauseRows();
            ConfigureModernCloseButton();
            BuildModernPauseRows();
            ConfigureDesertVisuals();
            ConfigureBlossomBakedVisuals();
            ConfigureBeachVisuals();

            EnsureSettingsRaycastPath();
            UpdateSettingsToggleVisuals(false);
            DisableLanguageSelectionUi();
            RefreshPauseLabels();
            pausePanelRoot.SetAsLastSibling();
            pauseRoot.transform.SetAsLastSibling();
        }

        private RectTransform FindPausePanel(RectTransform pauseRect)
        {
            if (pausePanelRoot != null)
            {
                return pausePanelRoot;
            }

            Transform existing = pauseRect.Find("PausePanel");
            pausePanelRoot = existing == null ? null : existing as RectTransform;
            return pausePanelRoot;
        }

        private void ConfigureSettingsPanelSprite()
        {
            pausePanelImage = GetOrAddImage(pausePanelRoot.gameObject);
            if (pausePanelImage == null)
            {
                return;
            }

            ThemeAssetSet activeTheme = ThemeCatalog.Current;
            bool desert = IsDesertSettingsTheme(activeTheme);
            bool blossom = IsBlossomSettingsTheme(activeTheme);
            bool beach = IsBeachSettingsTheme(activeTheme);

            if (desert || blossom || beach)
            {
                pausePanelImage.sprite = blossom
                    ? LoadBlossomSettingsSprite("01_blossom_main_panel")
                    : beach ? LoadBeachSettingsSprite("01_beach_main_panel")
                    : LoadDesertCleanSettingsSprite("desert_main_panel");
                pausePanelImage.enabled = true;
                pausePanelImage.color = Color.white;
                pausePanelImage.material = null;
                pausePanelImage.raycastTarget = false;
                pausePanelImage.type = Image.Type.Simple;
                pausePanelImage.preserveAspect = true;
                DisableSelectableDecor(pausePanelRoot.gameObject);
                return;
            }

            Sprite panelSprite = IsOceanSettingsTheme(activeTheme)
                ? LoadOceanSettingsSprite("ocean_panel")
                : IsGardenSettingsTheme(activeTheme)
                    ? LoadGardenSettingsSprite("garden_panel_medium")
                    : null;
            panelSprite ??= activeTheme != null && activeTheme.SettingsPanelSprite != null
                ? activeTheme.SettingsPanelSprite
                : LoadSettingsSprite(SettingsPanelPath);
            pausePanelImage.enabled = true;
            pausePanelImage.sprite = panelSprite;
            pausePanelImage.color = Color.white;
            pausePanelImage.material = null;
            pausePanelImage.raycastTarget = false;
            pausePanelImage.type = Image.Type.Simple;
            pausePanelImage.preserveAspect = true;
            Outline panelOutline = pausePanelRoot.GetComponent<Outline>();
            if (panelOutline != null)
            {
                panelOutline.enabled = false;
            }
            DisableSelectableDecor(pausePanelRoot.gameObject);
        }

        private void ApplySettingsTheme(ThemeAssetSet ignoredTheme)
        {
            if (pausePanelImage == null)
            {
                return;
            }

            // Settings follows only the applied/saved theme. The argument is
            // intentionally ignored so a temporary Themes-card selection can
            // never reskin this screen before APPLY is pressed.
            ThemeAssetSet activeTheme = ThemeCatalog.Current;
            bool desert = IsDesertSettingsTheme(activeTheme);
            bool blossom = IsBlossomSettingsTheme(activeTheme);
            bool beach = IsBeachSettingsTheme(activeTheme);

            if (desert || blossom || beach)
            {
                pausePanelImage.sprite = blossom
                    ? LoadBlossomSettingsSprite("01_blossom_main_panel")
                    : beach ? LoadBeachSettingsSprite("01_beach_main_panel")
                    : LoadDesertCleanSettingsSprite("desert_main_panel");
                pausePanelImage.color = Color.white;
                pausePanelImage.material = null;
                pausePanelImage.type = Image.Type.Simple;
                pausePanelImage.preserveAspect = true;
                pausePanelImage.raycastTarget = false;
                pausePanelImage.enabled = true;
                Outline panelOutline = pausePanelRoot.GetComponent<Outline>();
                if (panelOutline != null)
                {
                    panelOutline.enabled = false;
                }
            }

            Sprite settingsSprite = IsOceanSettingsTheme(activeTheme)
                ? LoadOceanSettingsSprite("ocean_panel")
                : IsGardenSettingsTheme(activeTheme)
                    ? LoadGardenSettingsSprite("garden_panel_medium")
                    : activeTheme != null ? activeTheme.SettingsPanelSprite : null;
            if (settingsSprite == null)
            {
                settingsSprite = LoadSettingsSprite(SettingsPanelPath);
            }

            if (!desert && !blossom && !beach)
            {
                pausePanelImage.sprite = settingsSprite;
                pausePanelImage.color = Color.white;
                pausePanelImage.material = null;
                pausePanelImage.type = Image.Type.Simple;
                pausePanelImage.preserveAspect = true;
                pausePanelImage.raycastTarget = false;
                pausePanelImage.enabled = settingsSprite != null;
                Outline panelOutline = pausePanelRoot.GetComponent<Outline>();
                if (panelOutline != null)
                {
                    panelOutline.enabled = false;
                }
            }

            ApplySettingsPanelGeometry();
            ConfigureCandySettingsBottomDecoration();
            ConfigureSettingsTitle();
            ConfigureModernCloseButton();
            BuildModernPauseRows();
            ConfigureDesertVisuals();
            UpdateSettingsToggleVisuals(false);
            ConfigureBlossomBakedVisuals();
            ConfigureBeachVisuals();
        }

        private static bool IsCandySettingsTheme(ThemeAssetSet theme)
        {
            return theme != null && theme.ThemeType == ThemeType.Candy;
        }

        private static bool IsOceanSettingsTheme(ThemeAssetSet theme)
        {
            return theme == null || theme.ThemeType == ThemeType.Ocean;
        }

        private static bool IsGardenSettingsTheme(ThemeAssetSet theme)
        {
            return theme != null && theme.ThemeType == ThemeType.Crystal;
        }

        private static bool IsDesertSettingsTheme(ThemeAssetSet theme)
        {
            return theme != null && theme.ThemeType == ThemeType.Gold;
        }

        private static bool IsBlossomSettingsTheme(ThemeAssetSet theme)
        {
            return theme != null && theme.ThemeType == ThemeType.Neon;
        }

        private static bool IsBeachSettingsTheme(ThemeAssetSet theme)
        {
            return theme != null && theme.ThemeType == ThemeType.Aqua;
        }

        private bool IsDesertSettingsActive()
        {
            return IsDesertSettingsTheme(ThemeCatalog.Current);
        }

        private bool IsBlossomSettingsActive()
        {
            return IsBlossomSettingsTheme(ThemeCatalog.Current);
        }

        private bool IsBeachSettingsActive()
        {
            return IsBeachSettingsTheme(ThemeCatalog.Current);
        }

        private static Sprite LoadOceanSettingsSprite(string fileName)
        {
            return Resources.Load<Sprite>($"{OceanSettingsRoot}/{fileName}");
        }

        private static Sprite LoadGardenSettingsSprite(string fileName)
        {
            string resourcePath = $"{GardenSettingsRoot}/{fileName}";
            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            // The supplied Garden controls can be imported as multiple sprites.
            // Always pick the largest artwork rect, even when Unity returns a tiny
            // alpha fragment as the path's implicit Sprite.
            Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
            Sprite largest = sprite;
            float largestArea = sprite != null ? sprite.rect.width * sprite.rect.height : 0f;
            for (int i = 0; i < sprites.Length; i++)
            {
                Sprite candidate = sprites[i];
                if (candidate == null)
                {
                    continue;
                }

                float area = candidate.rect.width * candidate.rect.height;
                if (largest == null || area > largestArea)
                {
                    largest = candidate;
                    largestArea = area;
                }
            }

            return largest;
        }

        private static Sprite LoadDesertCleanSettingsSprite(string fileName)
        {
            return Resources.Load<Sprite>($"{DesertCleanSettingsRoot}/{fileName}");
        }

        private static Sprite LoadBlossomSettingsSprite(string fileName)
        {
            return Resources.Load<Sprite>($"{BlossomSettingsRoot}/{fileName}");
        }

        private static Sprite LoadBeachSettingsSprite(string fileName)
        {
            return Resources.Load<Sprite>($"{BeachSettingsRoot}/{fileName}");
        }

        private bool IsCandySettingsActive()
        {
            return IsCandySettingsTheme(ThemeCatalog.Current);
        }

        private bool IsGardenSettingsActive()
        {
            return IsGardenSettingsTheme(ThemeCatalog.Current);
        }

        private void ApplySettingsPanelGeometry()
        {
            if (pausePanelRoot == null)
            {
                return;
            }

            pausePanelRoot.sizeDelta = IsCandySettingsActive()
                ? new Vector2(CandySettingsPanelWidth, CandySettingsPanelHeight)
                : IsGardenSettingsActive()
                    ? new Vector2(GardenSettingsPanelWidth, GardenSettingsPanelHeight)
                    : IsDesertSettingsActive()
                        ? new Vector2(DesertSettingsPanelWidth, DesertSettingsPanelHeight)
                        : IsBlossomSettingsActive()
                            ? new Vector2(BlossomSettingsPanelWidth, BlossomSettingsPanelHeight)
                            : IsBeachSettingsActive()
                                ? new Vector2(BeachSettingsPanelWidth, BeachSettingsPanelHeight)
                        : new Vector2(SettingsPanelWidth, SettingsPanelHeight);
        }

        private void ConfigureCandySettingsBottomDecoration()
        {
            if (pausePanelRoot == null)
            {
                return;
            }

            RectTransform decorationRect = GetOrCreateChildRect(pausePanelRoot, "CandyBottomDecoration");
            ThemeAssetSet activeTheme = ThemeCatalog.Current;
            Sprite decorationSprite = activeTheme != null ? activeTheme.SettingsBottomDecorationSprite : null;
            // The enlarged separated Candy rows use the full lower panel area.
            // Keep the optional footer hidden so it cannot compress or cover them.
            bool showDecoration = false;

            decorationRect.anchorMin = new Vector2(0.5f, 0f);
            decorationRect.anchorMax = new Vector2(0.5f, 0f);
            decorationRect.pivot = new Vector2(0.5f, 0f);
            decorationRect.anchoredPosition = new Vector2(0f, 0f);
            decorationRect.sizeDelta = new Vector2(570f, 194f);
            decorationRect.localScale = Vector3.one;

            settingsBottomDecorationImage = GetOrAddImage(decorationRect.gameObject);
            settingsBottomDecorationImage.sprite = decorationSprite;
            settingsBottomDecorationImage.color = Color.white;
            settingsBottomDecorationImage.material = null;
            settingsBottomDecorationImage.type = Image.Type.Simple;
            settingsBottomDecorationImage.preserveAspect = true;
            settingsBottomDecorationImage.raycastTarget = false;
            settingsBottomDecorationImage.enabled = showDecoration;
            decorationRect.gameObject.SetActive(showDecoration);
            if (showDecoration)
            {
                decorationRect.SetAsFirstSibling();
            }

            // The final Blossom panel already contains its border flowers and
            // petals. Keep the optional loose pieces inactive so they cannot
            // duplicate the baked decoration or cover the bottom rows.
            DisableChild(pausePanelRoot, "BlossomFlowerLarge");
            DisableChild(pausePanelRoot, "BlossomFlowerSmall");
            DisableChild(pausePanelRoot, "BlossomPetal");
            DisableChild(pausePanelRoot, "BlossomLeaf");
        }

        private void ConfigureSettingsTitle()
        {
            if (pauseTitleText == null && pausePanelRoot != null)
            {
                Transform titleTransform = pausePanelRoot.Find("PauseTitle");
                if (titleTransform != null)
                {
                    pauseTitleText = titleTransform.GetComponent<TMP_Text>();
                }
            }

            if (pauseTitleText == null)
            {
                RectTransform titleRect = GetOrCreateChildRect(pausePanelRoot, "PauseTitle");
                pauseTitleText = titleRect.gameObject.AddComponent<TextMeshProUGUI>();
            }

            Image titleImage = pauseTitleText.GetComponent<Image>();
            if (titleImage != null)
            {
                titleImage.enabled = false;
                titleImage.raycastTarget = false;
            }

            Button titleButton = pauseTitleText.GetComponent<Button>();
            if (titleButton != null)
            {
                titleButton.enabled = false;
                titleButton.interactable = false;
            }

            bool candy = IsCandySettingsActive();
            bool ocean = IsOceanSettingsTheme(ThemeCatalog.Current);
            bool garden = IsGardenSettingsActive();
            bool desert = IsDesertSettingsActive();
            bool blossom = IsBlossomSettingsActive();
            bool beach = IsBeachSettingsActive();
            // ocean_panel already contains the final Settings lettering. Keep this
            // legacy standalone title permanently inactive so Ocean renders it once.
            RectTransform oceanTitleRect = GetOrCreateChildRect(pausePanelRoot, "OceanSettingsTitle");
            Image oceanTitleImage = GetOrAddImage(oceanTitleRect.gameObject);
            oceanTitleImage.sprite = null;
            oceanTitleImage.enabled = false;
            oceanTitleImage.raycastTarget = false;
            oceanTitleRect.gameObject.SetActive(false);

            RectTransform coverRect = GetOrCreateChildRect(pausePanelRoot, "CandyTitleCover");
            coverRect.anchorMin = new Vector2(0.5f, 0.5f);
            coverRect.anchorMax = new Vector2(0.5f, 0.5f);
            coverRect.pivot = new Vector2(0.5f, 0.5f);
            coverRect.anchoredPosition = new Vector2(0f, 360f);
            coverRect.sizeDelta = new Vector2(490f, 82f);
            coverRect.localScale = Vector3.one;
            Image cover = GetOrAddImage(coverRect.gameObject);
            UISpriteFactory.ApplyRounded(cover, 0.42f);
            cover.color = new Color(1f, 0.91f, 0.95f, 1f);
            cover.raycastTarget = false;
            // The final Candy panel has a clean header area. The legacy cover was
            // only needed to hide the title baked into the previous artwork.
            cover.enabled = false;
            coverRect.gameObject.SetActive(false);

            RectTransform rect = pauseTitleText.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = candy
                    ? new Vector2(0f, 548f)
                    : garden ? new Vector2(0f, 486f) : blossom ? new Vector2(0f, 492f) : desert ? new Vector2(0f, 430f) : new Vector2(0f, 292f);
                rect.sizeDelta = candy
                    ? new Vector2(520f, 86f)
                    : garden ? new Vector2(500f, 80f) : blossom ? new Vector2(500f, 92f) : desert ? new Vector2(500f, 82f) : new Vector2(520f, 76f);
                rect.localScale = Vector3.one;
                rect.pivot = new Vector2(0.5f, 0.5f);
            }

            // OceanSettingsTitle uses the supplied artwork, which already contains
            // the Settings lettering. Keep the legacy TMP title hidden there so
            // the header is rendered exactly once.
            // Garden and Ocean panels already contain the final Settings heading.
            // Candy keeps its runtime title. Desert uses the supplied standalone
            // title sprite below so no TMP lettering is duplicated over it.
            pauseTitleText.text = candy ? "Settings" : string.Empty;
            pauseTitleText.font = Resources.Load<TMP_FontAsset>(ScoreFontPath);
            pauseTitleText.alignment = TextAlignmentOptions.Center;
            pauseTitleText.enableAutoSizing = candy || desert;
            pauseTitleText.fontSizeMin = candy ? 50f : desert ? 44f : garden ? 42f : 44f;
            pauseTitleText.fontSizeMax = candy ? 66f : desert ? 58f : garden ? 56f : 58f;
            pauseTitleText.fontStyle = FontStyles.Bold;
            pauseTitleText.color = garden
                ? new Color(0.035f, 0.23f, 0.09f, 1f)
                : desert ? new Color(0.30f, 0.13f, 0.055f, 1f) : new Color(0.48f, 0.015f, 0.18f, 1f);
            pauseTitleText.raycastTarget = false;
            pauseTitleText.enabled = candy;
            pauseTitleText.gameObject.SetActive(candy);

            RectTransform desertTitleRect = GetOrCreateChildRect(pausePanelRoot, "DesertSettingsTitle");
            desertTitleRect.anchorMin = desertTitleRect.anchorMax = new Vector2(0.5f, 1f);
            desertTitleRect.pivot = new Vector2(0.5f, 1f);
            desertTitleRect.anchoredPosition = new Vector2(0f, -72f);
            desertTitleRect.sizeDelta = new Vector2(360f, 118f);
            desertTitleRect.localScale = Vector3.one;
            Image desertTitle = GetOrAddImage(desertTitleRect.gameObject);
            desertTitle.sprite = desert ? LoadDesertCleanSettingsSprite("desert_settings_title") : null;
            desertTitle.color = Color.white;
            desertTitle.material = null;
            desertTitle.type = Image.Type.Simple;
            desertTitle.preserveAspect = true;
            desertTitle.raycastTarget = false;
            desertTitle.enabled = desert && desertTitle.sprite != null;
            desertTitleRect.gameObject.SetActive(desertTitle.enabled);
            if (desertTitle.enabled)
            {
                desertTitleRect.SetAsLastSibling();
            }
            RectTransform blossomBannerRect = GetOrCreateChildRect(pausePanelRoot, "BlossomSettingsBanner");
            blossomBannerRect.anchorMin = blossomBannerRect.anchorMax = new Vector2(0.5f, 1f);
            blossomBannerRect.pivot = new Vector2(0.5f, 1f);
            blossomBannerRect.anchoredPosition = new Vector2(32f, -116f);
            blossomBannerRect.sizeDelta = new Vector2(300f, 100f);
            blossomBannerRect.localScale = Vector3.one;
            Image blossomBanner = GetOrAddImage(blossomBannerRect.gameObject);
            blossomBanner.sprite = blossom ? LoadBlossomSettingsSprite("23_blossom_settings_title") : null;
            blossomBanner.color = Color.white;
            blossomBanner.preserveAspect = true;
            blossomBanner.raycastTarget = false;
            blossomBanner.enabled = blossom && blossomBanner.sprite != null;
            blossomBannerRect.gameObject.SetActive(blossomBanner.enabled);
            if (blossomBanner.enabled)
            {
                blossomBannerRect.SetAsLastSibling();
            }

            RectTransform beachTitleRect = GetOrCreateChildRect(pausePanelRoot, "BeachSettingsTitle");
            beachTitleRect.anchorMin = beachTitleRect.anchorMax = new Vector2(0.5f, 1f);
            beachTitleRect.pivot = new Vector2(0.5f, 1f);
            beachTitleRect.anchoredPosition = new Vector2(0f, -50f);
            beachTitleRect.sizeDelta = new Vector2(520f, 154f);
            beachTitleRect.localScale = Vector3.one;
            Image beachTitle = GetOrAddImage(beachTitleRect.gameObject);
            beachTitle.sprite = beach ? LoadBeachSettingsSprite("02_beach_settings_title") : null;
            beachTitle.color = Color.white;
            beachTitle.material = null;
            beachTitle.type = Image.Type.Simple;
            beachTitle.preserveAspect = true;
            beachTitle.raycastTarget = false;
            beachTitle.enabled = beach && beachTitle.sprite != null;
            beachTitleRect.gameObject.SetActive(beachTitle.enabled);
            if (beachTitle.enabled)
            {
                beachTitleRect.SetAsLastSibling();
            }
            if (candy || ocean)
            {
                (ocean ? oceanTitleRect : rect).SetAsLastSibling();
            }
        }

        private void ConfigureSettingsHeaderAccent()
        {
            RectTransform accentRect = GetOrCreateChildRect(pausePanelRoot, "SettingsHeaderAccent");
            accentRect.anchorMin = new Vector2(0.5f, 0.5f);
            accentRect.anchorMax = accentRect.anchorMin;
            accentRect.pivot = new Vector2(0.5f, 0.5f);
            accentRect.anchoredPosition = new Vector2(0f, 251f);
            accentRect.sizeDelta = new Vector2(210f, 6f);
            accentRect.localScale = Vector3.one;

            Image accent = GetOrAddImage(accentRect.gameObject);
            accent.enabled = false;
            accent.raycastTarget = false;
            accentRect.SetSiblingIndex(Mathf.Min(1, pausePanelRoot.childCount - 1));
        }

        private void EnsurePauseDimOverlay(RectTransform pauseRect)
        {
            RectTransform dimRect = GetOrCreateChildRect(pauseRect, "DimOverlay");
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            dimRect.localScale = Vector3.one;
            dimRect.pivot = new Vector2(0.5f, 0.5f);
            dimRect.SetAsFirstSibling();

            Image dimImage = GetOrAddImage(dimRect.gameObject);
            dimImage.sprite = null;
            dimImage.type = Image.Type.Simple;
            dimImage.color = new Color(0f, 0.018f, 0.07f, 0.56f);
            dimImage.raycastTarget = true;
        }

        private void DisableObsoletePauseBackplates(RectTransform pauseRect)
        {
            if (pauseRect == null)
            {
                return;
            }

            for (int i = 0; i < pauseRect.childCount; i++)
            {
                Transform child = pauseRect.GetChild(i);
                if (child == null || child.name == "DimOverlay" || child.name == "PausePanel")
                {
                    continue;
                }

                string childName = child.name.ToLowerInvariant();
                bool looksLikeBackplate = childName.Contains("background")
                    || childName.Contains("backdrop")
                    || childName.Contains("backplate")
                    || childName.Contains("frame")
                    || childName.Contains("shadow")
                    || childName.Contains("scrim")
                    || childName.Contains("panel");

                if (looksLikeBackplate)
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private void DisableObsoletePausePanelBackplates()
        {
            if (pausePanelRoot == null)
            {
                return;
            }

            DisableChild(pausePanelRoot, "LanguageSelectionPopup");

            for (int i = 0; i < pausePanelRoot.childCount; i++)
            {
                Transform child = pausePanelRoot.GetChild(i);
                if (child == null
                    || child.name == "PauseTitle"
                    || child.name == "CloseButton"
                    || child.name == "RowsContainer")
                {
                    continue;
                }

                string childName = child.name.ToLowerInvariant();
                bool looksLikeBackplate = childName.Contains("background")
                    || childName.Contains("backdrop")
                    || childName.Contains("backplate")
                    || childName.Contains("frame")
                    || childName.Contains("shadow")
                    || childName.Contains("scrim");

                if (looksLikeBackplate)
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private void ConfigureModernCloseButton()
        {
            bool candy = IsCandySettingsActive();
            bool ocean = IsOceanSettingsTheme(ThemeCatalog.Current);
            bool garden = IsGardenSettingsActive();
            bool desert = IsDesertSettingsActive();
            bool blossom = IsBlossomSettingsActive();
            bool beach = IsBeachSettingsActive();
            RectTransform closeRect = GetOrCreateChildRect(pausePanelRoot, "CloseButton");
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            // The Candy close glyph is baked into the 1024x1536 panel at approximately
            // (838, 140). Keep this transparent Button centered over that artwork after
            // the panel is fitted to 900x1350; no second close sprite is rendered.
            closeRect.anchoredPosition = ocean ? new Vector2(-34f, -34f) : candy ? new Vector2(-105f, -64f) : garden ? new Vector2(-48f, -50f) : blossom ? new Vector2(-24f, -26f) : beach ? new Vector2(-28f, -28f) : desert ? new Vector2(-38f, -32f) : new Vector2(-28f, -28f);
            closeRect.sizeDelta = ocean ? new Vector2(92f, 92f) : candy ? new Vector2(118f, 118f) : garden ? new Vector2(86f, 86f) : blossom ? new Vector2(92f, 115f) : beach ? new Vector2(88f, 88f) : desert ? new Vector2(86f, 86f) : new Vector2(68f, 68f);
            closeRect.localScale = Vector3.one;

            Image image = GetOrAddImage(closeRect.gameObject);
            // Candy and Ocean panels already contain their final X artwork. The
            // retained Image is fully transparent and serves only as the Button's
            // raycastable hitbox; no second close graphic is drawn over either panel.
            bool usesBakedCloseArtwork = ocean || candy;
            image.sprite = blossom ? LoadBlossomSettingsSprite("03_blossom_close_x") : beach ? LoadBeachSettingsSprite("03_beach_close_x") : garden ? LoadGardenSettingsSprite("garden_x") : usesBakedCloseArtwork ? null : LoadSettingsSprite(SettingsClosePath);
            image.color = usesBakedCloseArtwork ? new Color(1f, 1f, 1f, 0f) : Color.white;
            image.type = Image.Type.Simple;
            image.preserveAspect = blossom || beach || garden || !usesBakedCloseArtwork;
            image.raycastTarget = true;

            RectTransform desertGlyphRect = GetOrCreateChildRect(closeRect, "DesertCloseGlyph");
            desertGlyphRect.anchorMin = Vector2.zero;
            desertGlyphRect.anchorMax = Vector2.one;
            desertGlyphRect.offsetMin = Vector2.zero;
            desertGlyphRect.offsetMax = Vector2.zero;
            TMP_Text desertGlyph = GetOrAddText(desertGlyphRect.gameObject);
            desertGlyph.text = "X";
            desertGlyph.font = Resources.Load<TMP_FontAsset>(ScoreFontPath);
            desertGlyph.alignment = TextAlignmentOptions.Center;
            desertGlyph.fontSize = 48f;
            desertGlyph.fontStyle = FontStyles.Bold;
            desertGlyph.color = new Color(0.94f, 0.42f, 0.04f, 1f);
            desertGlyph.raycastTarget = false;
            desertGlyph.gameObject.SetActive(desert);

            Outline closeOutline = closeRect.GetComponent<Outline>();
            if (desert)
            {
                image.sprite = LoadDesertCleanSettingsSprite("desert_close_x");
                image.color = Color.white;
                image.preserveAspect = true;
                desertGlyph.gameObject.SetActive(false);
                if (closeOutline != null)
                {
                    closeOutline.enabled = false;
                }
            }
            else if (closeOutline != null)
            {
                closeOutline.enabled = false;
            }

            resumeButton = GetOrAddButton(closeRect.gameObject, image);
            ConfigureButtonNoTransition(resumeButton);
            resumeButton.onClick.RemoveAllListeners();
            resumeButton.onClick.AddListener(() => gameManager.ResumeGame());
            closeRect.gameObject.SetActive(true);
            closeRect.SetAsLastSibling();
        }

        private void DisableLegacyPauseRows()
        {
            DisableLegacyPauseChild("ResumeButton");
            DisableLegacyPauseChild("PauseMuteButton");
            DisableLegacyPauseChild("PauseHapticsButton");
            DisableLegacyPauseChild("PauseResetTutorialButton");
            DisableLegacyPauseChild("PauseLanguageButton");
            DisableLegacyPauseChild("PauseRestartButton");
            DisableLegacyPauseChild("PauseMenuButton");
            DisableRemovedPauseTutorialRow();
        }

        private void DisableLegacyPauseChild(string childName)
        {
            if (pausePanelRoot == null)
            {
                return;
            }

            Transform child = pausePanelRoot.Find(childName);
            if (child != null)
            {
                child.gameObject.SetActive(false);
            }
        }

        private void BuildModernPauseRows()
        {
            bool candy = IsCandySettingsActive();
            bool garden = IsGardenSettingsActive();
            bool desert = IsDesertSettingsActive();
            bool blossom = IsBlossomSettingsActive();
            bool beach = IsBeachSettingsActive();
            settingsRowsContainer = GetOrCreateChildRect(pausePanelRoot, "RowsContainer");
            settingsRowsContainer.anchorMin = new Vector2(0.5f, 0.5f);
            settingsRowsContainer.anchorMax = new Vector2(0.5f, 0.5f);
            settingsRowsContainer.pivot = new Vector2(0.5f, 0.5f);
            settingsRowsContainer.anchoredPosition = candy ? new Vector2(0f, -78f) : garden ? new Vector2(0f, -70f) : blossom ? new Vector2(0f, -80f) : beach ? new Vector2(0f, -72f) : desert ? new Vector2(0f, 18f) : new Vector2(0f, -62f);
            settingsRowsContainer.sizeDelta = candy
                ? new Vector2(CandySettingsRowsWidth, CandySettingsRowsHeight)
                : garden
                    ? new Vector2(GardenSettingsRowsWidth, GardenSettingsRowsHeight)
                    : blossom
                        ? new Vector2(BlossomSettingsRowsWidth, BlossomSettingsRowsHeight)
                    : beach
                        ? new Vector2(BeachSettingsRowsWidth, BeachSettingsRowsHeight)
                    : desert
                        ? new Vector2(DesertSettingsRowsWidth, DesertSettingsRowsHeight)
                        : new Vector2(SettingsRowsWidth, SettingsRowsHeight);
            settingsRowsContainer.localScale = Vector3.one;

            VerticalLayoutGroup layout = settingsRowsContainer.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = settingsRowsContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            // Blossom positions its supplied rows explicitly over its own panel.
            // Re-enable the shared layout immediately when any other theme opens.
            layout.enabled = !blossom && !beach;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = candy ? CandySettingsRowsSpacing : garden ? GardenSettingsRowsSpacing : blossom ? BlossomSettingsRowsSpacing : beach ? BeachSettingsRowsSpacing : desert ? DesertSettingsRowsSpacing : SettingsRowsSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = settingsRowsContainer.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                fitter.enabled = false;
            }

            BuildToggleRow("MusicRow", "Music", "music", false, true);
            BuildToggleRow("SoundRow", "Sound", "sound", true, false);
            BuildToggleRow("VibrationRow", "Vibration", "vibration", false, false);
            BuildStatusRow("PrivacyRow", "Privacy Policy", "privacy", string.Empty, HandleSettingsPrivacy);
            BuildStatusRow("TermsRow", "Terms & Conditions", "terms", string.Empty, HandleSettingsTerms);
            BuildStatusRow("AboutRow", "About / Version", "about", $"v{Application.version}", HandleSettingsAbout);
            bool ocean = IsOceanSettingsTheme(ThemeCatalog.Current);
            BuildActionRow("RestartRow", candy || garden || ocean || desert || blossom || beach ? "Restart Game" : "Restart", "restart", "RESTART", true);
            BuildActionRow("MainMenuRow", "Main Menu", "home", "MENU", false);

            SetRowSiblingOrder("MusicRow", 0);
            SetRowSiblingOrder("SoundRow", 1);
            SetRowSiblingOrder("VibrationRow", 2);
            SetRowSiblingOrder("PrivacyRow", 3);
            SetRowSiblingOrder("TermsRow", 4);
            SetRowSiblingOrder("AboutRow", 5);
            SetRowSiblingOrder("RestartRow", 6);
            SetRowSiblingOrder("MainMenuRow", 7);
            DisableLanguageSelectionUi();
            settingsRowsContainer.gameObject.SetActive(true);
        }

        private void ConfigureDesertVisuals()
        {
            if (!IsDesertSettingsActive() || pausePanelRoot == null || settingsRowsContainer == null)
            {
                return;
            }

            string[] rowNames =
            {
                "MusicRow", "SoundRow", "VibrationRow", "PrivacyRow",
                "TermsRow", "AboutRow", "RestartRow", "MainMenuRow"
            };
            for (int i = 0; i < rowNames.Length; i++)
            {
                Transform label = settingsRowsContainer.Find(rowNames[i] + "/Label");
                TMP_Text labelText = label == null ? null : label.GetComponent<TMP_Text>();
                if (labelText == null)
                {
                    continue;
                }

                labelText.enableAutoSizing = false;
                labelText.fontSize = 32f;
                labelText.fontSizeMin = 32f;
                labelText.fontSizeMax = 32f;
                labelText.fontStyle = FontStyles.Bold;
                labelText.color = new Color(0.21f, 0.095f, 0.035f, 1f);
                labelText.alignment = TextAlignmentOptions.MidlineLeft;
            }

            Transform about = settingsRowsContainer.Find("AboutRow");
            RectTransform aboutStatus = about == null ? null : about.Find("Status") as RectTransform;
            RectTransform versionRect = aboutStatus == null ? null : aboutStatus.Find("Text") as RectTransform;
            if (versionRect != null)
            {
                versionRect.anchorMin = versionRect.anchorMax = new Vector2(1f, 0.5f);
                versionRect.pivot = new Vector2(1f, 0.5f);
                versionRect.anchoredPosition = new Vector2(-104f, 0f);
                versionRect.sizeDelta = new Vector2(144f, 42f);

                TMP_Text versionText = versionRect.GetComponent<TMP_Text>();
                if (versionText != null)
                {
                    versionText.text = Application.version;
                    versionText.alignment = TextAlignmentOptions.MidlineRight;
                    versionText.enableAutoSizing = false;
                    versionText.fontSize = 22f;
                    versionText.fontStyle = FontStyles.Bold;
                    versionText.color = new Color(0.27f, 0.12f, 0.04f, 0.92f);
                    versionText.raycastTarget = false;
                    versionText.enabled = true;
                    versionText.gameObject.SetActive(true);
                }
            }

            // The supplied main panel already contains its cactus, skull and sand
            // border artwork. Keep loose decorations inactive to avoid duplicates.
            DisableChild(pausePanelRoot, "DesertBottomDecor");
            DisableChild(pausePanelRoot, "DesertCactusCluster");
            DisableChild(pausePanelRoot, "DesertSkullCluster");
            DisableChild(pausePanelRoot, "DesertFlowerCactus");

            UpdateSettingsToggleVisuals(false);
            Transform title = pausePanelRoot.Find("DesertSettingsTitle");
            if (title != null) title.SetAsLastSibling();
            Transform close = pausePanelRoot.Find("CloseButton");
            if (close != null) close.SetAsLastSibling();
        }

        private void ConfigureBlossomBakedVisuals()
        {
            if (!IsBlossomSettingsActive() || pausePanelRoot == null || settingsRowsContainer == null)
            {
                return;
            }

            VerticalLayoutGroup layout = settingsRowsContainer.GetComponent<VerticalLayoutGroup>();
            if (layout != null) layout.enabled = false;
            settingsRowsContainer.anchoredPosition = Vector2.zero;
            settingsRowsContainer.sizeDelta = pausePanelRoot.sizeDelta;

            string[] rowNames =
            {
                "MusicRow", "SoundRow", "VibrationRow", "PrivacyRow",
                "TermsRow", "AboutRow", "RestartRow", "MainMenuRow"
            };

            // Blossom uses the supplied blank panel as its only backplate. Keep
            // the eight functional rows absolute so no shared Layout Group can
            // alter another theme, and render each supplied Blossom element once.
            for (int i = 0; i < rowNames.Length; i++)
            {
                RectTransform row = settingsRowsContainer.Find(rowNames[i]) as RectTransform;
                if (row == null) continue;
                row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
                row.pivot = new Vector2(0.5f, 1f);
                row.anchoredPosition = new Vector2(0f, -(242f + i * 106f));
                row.sizeDelta = new Vector2(690f, 96f);

                LayoutElement element = row.GetComponent<LayoutElement>();
                if (element != null) element.enabled = false;
                Image rootImage = row.GetComponent<Image>();
                if (rootImage != null) { rootImage.enabled = false; rootImage.raycastTarget = false; }

                SetChildActive(row, "RowBackground", true);
                SetChildActive(row, "RowHighlight", false);
                SetChildActive(row, "CandyContentCover", false);
                SetChildActive(row, "Icon", true);
                SetChildActive(row, "Label", true);
                SetChildActive(row, "CandyChevron", false);
                SetChildActive(row, "DesertChevronGlyph", false);
                SetChildActive(row, "BlossomChevron", false);
                SetChildActive(row, "BeachChevron", false);
                SetChildActive(row, "BeachChevron", false);

                RectTransform toggle = row.Find("ToggleControl") as RectTransform;
                if (toggle != null)
                {
                    toggle.anchorMin = toggle.anchorMax = new Vector2(1f, 0.5f);
                    toggle.pivot = new Vector2(1f, 0.5f);
                    toggle.anchoredPosition = new Vector2(-14f, 0f);
                    toggle.sizeDelta = new Vector2(238f, 86f);
                }

                RectTransform status = row.Find("Status") as RectTransform;
                if (status != null)
                {
                    status.anchorMin = Vector2.zero;
                    status.anchorMax = Vector2.one;
                    status.offsetMin = Vector2.zero;
                    status.offsetMax = Vector2.zero;
                }

                RectTransform action = row.Find("ActionButton") as RectTransform;
                if (action != null)
                {
                    action.anchorMin = Vector2.zero;
                    action.anchorMax = Vector2.one;
                    action.offsetMin = Vector2.zero;
                    action.offsetMax = Vector2.zero;
                }
            }

            ConfigureBlossomVersionOverlay();
            UpdateSettingsToggleVisuals(false);
            Transform banner = pausePanelRoot.Find("BlossomSettingsBanner");
            if (banner != null) banner.SetAsLastSibling();
            Transform close = pausePanelRoot.Find("CloseButton");
            if (close != null) close.SetAsLastSibling();
        }

        private void ConfigureBlossomVersionOverlay()
        {
            Transform about = settingsRowsContainer.Find("AboutRow");
            RectTransform status = about == null ? null : about.Find("Status") as RectTransform;
            RectTransform textRect = status == null ? null : status.Find("Text") as RectTransform;
            if (textRect == null) return;
            textRect.anchorMin = textRect.anchorMax = new Vector2(1f, 0.5f);
            textRect.pivot = new Vector2(1f, 0.5f);
            textRect.anchoredPosition = new Vector2(-112f, 12f);
            textRect.sizeDelta = new Vector2(116f, 42f);
            TMP_Text text = textRect.GetComponent<TMP_Text>();
            if (text == null) return;
            text.text = Application.version;
            text.alignment = TextAlignmentOptions.MidlineRight;
            text.fontSizeMin = 12f;
            text.fontSizeMax = 18f;
            text.color = Color.black;
            text.enabled = true;
            text.gameObject.SetActive(true);
        }

        private void ConfigureBeachVisuals()
        {
            if (!IsBeachSettingsActive() || pausePanelRoot == null || settingsRowsContainer == null)
            {
                return;
            }

            VerticalLayoutGroup layout = settingsRowsContainer.GetComponent<VerticalLayoutGroup>();
            if (layout != null) layout.enabled = false;
            settingsRowsContainer.anchoredPosition = Vector2.zero;
            settingsRowsContainer.sizeDelta = pausePanelRoot.sizeDelta;

            string[] rowNames =
            {
                "MusicRow", "SoundRow", "VibrationRow", "PrivacyRow",
                "TermsRow", "AboutRow", "RestartRow", "MainMenuRow"
            };

            for (int i = 0; i < rowNames.Length; i++)
            {
                RectTransform row = settingsRowsContainer.Find(rowNames[i]) as RectTransform;
                if (row == null) continue;
                row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
                row.pivot = new Vector2(0.5f, 1f);
                row.anchoredPosition = new Vector2(0f, -(210f + i * 104f));
                row.sizeDelta = new Vector2(690f, 96f);

                LayoutElement element = row.GetComponent<LayoutElement>();
                if (element != null) element.enabled = false;
                Image rootImage = row.GetComponent<Image>();
                if (rootImage != null) { rootImage.enabled = false; rootImage.raycastTarget = false; }

                SetChildActive(row, "RowBackground", true);
                SetChildActive(row, "RowHighlight", false);
                SetChildActive(row, "CandyContentCover", false);
                SetChildActive(row, "Icon", true);
                SetChildActive(row, "Label", true);
                SetChildActive(row, "CandyChevron", false);
                SetChildActive(row, "DesertChevronGlyph", false);
                SetChildActive(row, "BlossomChevron", false);

                RectTransform toggle = row.Find("ToggleControl") as RectTransform;
                if (toggle != null)
                {
                    toggle.anchorMin = toggle.anchorMax = new Vector2(1f, 0.5f);
                    toggle.pivot = new Vector2(1f, 0.5f);
                    toggle.anchoredPosition = new Vector2(-18f, 0f);
                    toggle.sizeDelta = new Vector2(220f, 82f);
                }

                RectTransform status = row.Find("Status") as RectTransform;
                if (status != null)
                {
                    status.anchorMin = Vector2.zero;
                    status.anchorMax = Vector2.one;
                    status.offsetMin = Vector2.zero;
                    status.offsetMax = Vector2.zero;
                }

                RectTransform action = row.Find("ActionButton") as RectTransform;
                if (action != null)
                {
                    action.anchorMin = Vector2.zero;
                    action.anchorMax = Vector2.one;
                    action.offsetMin = Vector2.zero;
                    action.offsetMax = Vector2.zero;
                }
            }

            Transform about = settingsRowsContainer.Find("AboutRow");
            RectTransform aboutStatus = about == null ? null : about.Find("Status") as RectTransform;
            RectTransform versionRect = aboutStatus == null ? null : aboutStatus.Find("Text") as RectTransform;
            if (versionRect != null)
            {
                versionRect.anchorMin = versionRect.anchorMax = new Vector2(1f, 0.5f);
                versionRect.pivot = new Vector2(1f, 0.5f);
                versionRect.anchoredPosition = new Vector2(-92f, 0f);
                versionRect.sizeDelta = new Vector2(116f, 42f);
            }

            UpdateSettingsToggleVisuals(false);
            Transform title = pausePanelRoot.Find("BeachSettingsTitle");
            if (title != null) title.SetAsLastSibling();
            Transform close = pausePanelRoot.Find("CloseButton");
            if (close != null) close.SetAsLastSibling();
        }

        private static void SetChildActive(Transform root, string childName, bool active)
        {
            Transform child = root.Find(childName);
            if (child != null) child.gameObject.SetActive(active);
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

        private void DisableLanguageSelectionUi()
        {
            if (pauseLanguageButton != null)
            {
                pauseLanguageButton.onClick.RemoveAllListeners();
                pauseLanguageButton.gameObject.SetActive(false);
            }

            Transform languageRow = settingsRowsContainer == null ? null : settingsRowsContainer.Find("LanguageRow");
            if (languageRow != null)
            {
                Button[] rowButtons = languageRow.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < rowButtons.Length; i++)
                {
                    rowButtons[i].onClick.RemoveAllListeners();
                }

                languageRow.gameObject.SetActive(false);
            }

            if (languagePopupRoot != null)
            {
                languagePopupRoot.SetActive(false);
            }

            Transform languageOverlay = pauseRoot == null ? null : pauseRoot.transform.Find("LanguageOverlay");
            if (languageOverlay != null)
            {
                languageOverlay.gameObject.SetActive(false);
            }

            DisableChild(pausePanelRoot, "LanguageSelectionPopup");
        }

        private void SetRowSiblingOrder(string rowName, int index)
        {
            Transform row = settingsRowsContainer == null ? null : settingsRowsContainer.Find(rowName);
            if (row != null)
            {
                row.SetSiblingIndex(index);
            }
        }

        private RectTransform BuildRowBase(string rowName, string label, string iconKey)
        {
            bool candy = IsCandySettingsActive();
            bool ocean = IsOceanSettingsTheme(ThemeCatalog.Current);
            bool garden = IsGardenSettingsActive();
            bool desert = IsDesertSettingsActive();
            bool blossom = IsBlossomSettingsActive();
            bool beach = IsBeachSettingsActive();
            bool separatedArtwork = candy || ocean || garden || desert || blossom || beach;
            float rowWidth = candy ? CandySettingsRowsWidth : garden ? GardenSettingsRowsWidth : blossom ? BlossomSettingsRowsWidth : beach ? BeachSettingsRowsWidth : desert ? DesertSettingsRowsWidth : SettingsRowsWidth;
            float rowHeight = candy ? CandySettingsRowHeight : garden ? GardenSettingsRowHeight : blossom ? BlossomSettingsRowHeight : beach ? BeachSettingsRowHeight : desert ? DesertSettingsRowHeight : SettingsRowHeight;
            RectTransform row = GetOrCreateChildRect(settingsRowsContainer, rowName);
            row.localScale = Vector3.one;
            row.sizeDelta = new Vector2(rowWidth, rowHeight);
            row.anchorMin = new Vector2(0.5f, 0.5f);
            row.anchorMax = new Vector2(0.5f, 0.5f);
            row.pivot = new Vector2(0.5f, 0.5f);

            Image rowRootImage = GetOrAddImage(row.gameObject);
            ApplySettingsRowArtwork(rowName, rowRootImage);

            Button rowButton = row.GetComponent<Button>();
            if (rowButton != null)
            {
                rowButton.enabled = false;
                rowButton.interactable = false;
            }

            LayoutElement layoutElement = row.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = row.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.preferredWidth = rowWidth;
            layoutElement.preferredHeight = rowHeight;
            layoutElement.minWidth = rowWidth;
            layoutElement.minHeight = rowHeight;
            layoutElement.flexibleWidth = 0f;
            layoutElement.flexibleHeight = 0f;

            RectTransform background = GetOrCreateChildRect(row, "RowBackground");
            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.one;
            background.offsetMin = Vector2.zero;
            background.offsetMax = Vector2.zero;
            background.localScale = Vector3.one;
            Image backgroundImage = GetOrAddImage(background.gameObject);
            if (blossom)
            {
                bool toggleRow = rowName == "MusicRow" || rowName == "SoundRow" || rowName == "VibrationRow";
                backgroundImage.sprite = LoadBlossomSettingsSprite(toggleRow ? "22_blossom_row_toggle_clean" : "05_blossom_row_arrow");
                backgroundImage.color = Color.white;
                backgroundImage.material = null;
                backgroundImage.type = Image.Type.Simple;
                // Rows are authored as complete wide elements. Fit them to the
                // stable Settings row rect instead of shrinking them by aspect.
                backgroundImage.preserveAspect = false;
            }
            else if (beach)
            {
                backgroundImage.sprite = LoadBeachSettingsSprite("21_beach_row_clean");
                backgroundImage.color = Color.white;
                backgroundImage.material = null;
                backgroundImage.type = Image.Type.Simple;
                backgroundImage.preserveAspect = false;
            }
            else if (desert)
            {
                UISpriteFactory.ApplyRounded(backgroundImage, 0.18f);
                backgroundImage.color = new Color(1f, 0.94f, 0.80f, 0.96f);
            }
            backgroundImage.enabled = desert || blossom || beach;
            backgroundImage.raycastTarget = false;
            background.SetAsFirstSibling();
            EnsureGraphicOutline(background.gameObject,
                desert ? new Color(0.92f, 0.50f, 0.12f, 0.55f) : new Color(0.22f, 0.86f, 1f, 0.30f),
                new Vector2(1.1f, -1.1f));
            EnsureGraphicShadow(background.gameObject,
                desert ? new Color(0.30f, 0.14f, 0.04f, 0.20f) : new Color(0f, 0.02f, 0.07f, 0.42f),
                new Vector2(0f, -1.8f));
            Outline blossomOutline = background.GetComponent<Outline>();
            Shadow blossomShadow = background.GetComponent<Shadow>();
            if (blossom && blossomOutline != null) blossomOutline.enabled = false;
            if (blossom && blossomShadow != null) blossomShadow.enabled = false;

            RectTransform highlight = GetOrCreateChildRect(row, "RowHighlight");
            highlight.anchorMin = new Vector2(0.045f, 0.58f);
            highlight.anchorMax = new Vector2(0.955f, 0.90f);
            highlight.offsetMin = Vector2.zero;
            highlight.offsetMax = Vector2.zero;
            highlight.localScale = Vector3.one;
            Image highlightImage = GetOrAddImage(highlight.gameObject);
            highlightImage.enabled = false;
            highlightImage.raycastTarget = false;
            highlight.SetSiblingIndex(1);

            RectTransform contentCover = GetOrCreateChildRect(row, "CandyContentCover");
            contentCover.anchorMin = Vector2.zero;
            contentCover.anchorMax = Vector2.one;
            contentCover.offsetMin = new Vector2(8f, 7f);
            contentCover.offsetMax = new Vector2(-8f, -7f);
            contentCover.localScale = Vector3.one;
            Image contentCoverImage = GetOrAddImage(contentCover.gameObject);
            UISpriteFactory.ApplyRounded(contentCoverImage, 0.24f);
            contentCoverImage.color = new Color(1f, 0.965f, 0.978f, 0.995f);
            contentCoverImage.raycastTarget = false;
            contentCoverImage.enabled = candy;
            contentCover.gameObject.SetActive(candy);
            if (candy)
            {
                contentCover.SetSiblingIndex(2);
            }

            RectTransform icon = GetOrCreateChildRect(row, "Icon");
            icon.anchorMin = new Vector2(0f, 0.5f);
            icon.anchorMax = new Vector2(0f, 0.5f);
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.anchoredPosition = candy ? new Vector2(58f, 0f) : blossom ? new Vector2(62f, 0f) : beach ? new Vector2(64f, 0f) : desert ? new Vector2(60f, 0f) : new Vector2(52f, 0f);
            icon.sizeDelta = candy ? new Vector2(70f, 70f) : blossom ? new Vector2(75f, 75f) : beach ? new Vector2(76f, 76f) : desert ? new Vector2(70f, 70f) : new Vector2(62f, 62f);
            icon.localScale = Vector3.one;
            Image iconImage = GetOrAddImage(icon.gameObject);
            iconImage.sprite = blossom
                ? GetBlossomSettingsIconSprite(iconKey)
                : beach ? GetBeachSettingsIconSprite(iconKey)
                : ocean
                ? GetOceanSettingsIconSprite(iconKey)
                : garden ? GetGardenSettingsIconSprite(iconKey)
                    : desert ? GetDesertSettingsIconSprite(iconKey)
                        : candy ? GetCandySettingsIconSprite(ThemeCatalog.Current, iconKey) : null;
            iconImage.color = Color.white;
            iconImage.material = null;
            iconImage.type = Image.Type.Simple;
            iconImage.preserveAspect = true;
            iconImage.enabled = separatedArtwork && iconImage.sprite != null;
            iconImage.raycastTarget = false;

            RectTransform labelRect = GetOrCreateChildRect(row, "Label");
            labelRect.anchorMin = new Vector2(0f, 0.5f);
            labelRect.anchorMax = new Vector2(0f, 0.5f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.anchoredPosition = candy ? new Vector2(105f, 0f) : garden ? new Vector2(104f, 0f) : blossom ? new Vector2(116f, 0f) : beach ? new Vector2(120f, 0f) : desert ? new Vector2(112f, 0f) : new Vector2(108f, 0f);
            labelRect.sizeDelta = candy ? new Vector2(280f, 74f) : garden ? new Vector2(310f, 70f) : blossom ? new Vector2(350f, 82f) : beach ? new Vector2(350f, 78f) : desert ? new Vector2(330f, 76f) : new Vector2(245f, 70f);
            labelRect.localScale = Vector3.one;

            TMP_Text labelText = GetOrAddText(labelRect.gameObject);
            labelText.text = separatedArtwork ? label : string.Empty;
            labelText.font = Resources.Load<TMP_FontAsset>(ScoreFontPath);
            labelText.alignment = TextAlignmentOptions.MidlineLeft;
            labelText.enableAutoSizing = separatedArtwork && !blossom && !beach;
            labelText.textWrappingMode = garden || desert || blossom || beach ? TextWrappingModes.NoWrap : TextWrappingModes.Normal;
            labelText.fontSize = blossom || beach ? 32f : labelText.fontSize;
            labelText.fontSizeMin = garden ? 17f : blossom || beach ? 32f : 22f;
            labelText.fontSizeMax = garden ? 28f : blossom || beach ? 32f : 31f;
            labelText.fontStyle = FontStyles.Bold;
            labelText.color = ocean
                ? new Color(0.03f, 0.16f, 0.38f, 1f)
                : garden ? new Color(0.035f, 0.23f, 0.09f, 1f)
                    : blossom ? Color.black
                    : beach ? Color.black
                    : desert ? new Color(0.33f, 0.15f, 0.055f, 1f)
                        : new Color(0.27f, 0.025f, 0.13f, 1f);
            if (beach)
            {
                labelText.enableAutoSizing = false;
                labelText.fontSize = 32f;
                labelText.fontSizeMin = 32f;
                labelText.fontSizeMax = 32f;
                labelText.fontStyle = FontStyles.Bold;
                labelText.color = Color.black;
                labelText.alignment = TextAlignmentOptions.MidlineLeft;
                labelText.characterSpacing = 0f;
            }
            else if (desert)
            {
                labelText.enableAutoSizing = false;
                labelText.fontSize = 32f;
                labelText.fontSizeMin = 32f;
                labelText.fontSizeMax = 32f;
                labelText.fontStyle = FontStyles.Bold;
                labelText.color = new Color(0.21f, 0.095f, 0.035f, 1f);
                labelText.alignment = TextAlignmentOptions.MidlineLeft;
                labelText.characterSpacing = 0f;
            }
            labelText.enabled = separatedArtwork;
            labelText.raycastTarget = false;

            row.gameObject.SetActive(true);
            return row;
        }

        private static Sprite GetCandySettingsIconSprite(ThemeAssetSet theme, string iconKey)
        {
            if (!IsCandySettingsTheme(theme))
            {
                return null;
            }

            return iconKey switch
            {
                "music" => theme.SettingsMusicIconSprite,
                "sound" => theme.SettingsSoundIconSprite,
                "vibration" => theme.SettingsVibrationIconSprite,
                "privacy" => theme.SettingsPrivacyIconSprite,
                "terms" => theme.SettingsTermsIconSprite,
                "about" => theme.SettingsAboutIconSprite,
                "restart" => theme.SettingsRestartIconSprite,
                "home" => theme.SettingsMainMenuIconSprite,
                _ => null
            };
        }

        private static Sprite GetOceanSettingsIconSprite(string iconKey)
        {
            return iconKey switch
            {
                "music" => LoadOceanSettingsSprite("ocean_icon_music"),
                "sound" => LoadOceanSettingsSprite("ocean_icon_sound"),
                "vibration" => LoadOceanSettingsSprite("ocean_icon_vibration"),
                "privacy" => LoadOceanSettingsSprite("ocean_icon_privacy"),
                "terms" => LoadOceanSettingsSprite("ocean_icon_terms"),
                "about" => LoadOceanSettingsSprite("ocean_icon_about"),
                "restart" => LoadOceanSettingsSprite("ocean_icon_restart"),
                "home" => LoadOceanSettingsSprite("ocean_icon_home"),
                _ => null
            };
        }

        private static Sprite GetGardenSettingsIconSprite(string iconKey)
        {
            return iconKey switch
            {
                "music" => LoadGardenSettingsSprite("garden_icon_music"),
                "sound" => LoadGardenSettingsSprite("garden_icon_sound"),
                "vibration" => LoadGardenSettingsSprite("garden_icon_vibration"),
                "privacy" => LoadGardenSettingsSprite("garden_icon_privacy"),
                "terms" => LoadGardenSettingsSprite("garden_icon_terms"),
                "about" => LoadGardenSettingsSprite("garden_icon_about"),
                "restart" => LoadGardenSettingsSprite("garden_icon_restart"),
                "home" => LoadGardenSettingsSprite("garden_icon_home"),
                _ => null
            };
        }

        private static Sprite GetDesertSettingsIconSprite(string iconKey)
        {
            string fileName = iconKey switch
            {
                "music" => "desert_icon_music",
                "sound" => "desert_icon_sound",
                "vibration" => "desert_icon_vibration",
                "privacy" => "desert_icon_privacy",
                "terms" => "desert_icon_terms",
                "about" => "desert_icon_about",
                "restart" => "desert_icon_restart",
                "home" => "desert_icon_home",
                _ => null
            };
            return string.IsNullOrEmpty(fileName) ? null : LoadDesertCleanSettingsSprite(fileName);
        }

        private static Sprite GetBlossomSettingsIconSprite(string iconKey)
        {
            string fileName = iconKey switch
            {
                "music" => "09_blossom_icon_music",
                "sound" => "10_blossom_icon_sound",
                "vibration" => "11_blossom_icon_vibration",
                "privacy" => "12_blossom_icon_privacy",
                "terms" => "13_blossom_icon_terms",
                "about" => "14_blossom_icon_about",
                "restart" => "15_blossom_icon_restart",
                "home" => "16_blossom_icon_home",
                _ => null
            };
            return string.IsNullOrEmpty(fileName) ? null : LoadBlossomSettingsSprite(fileName);
        }

        private static Sprite GetBeachSettingsIconSprite(string iconKey)
        {
            string fileName = iconKey switch
            {
                "music" => "09_beach_icon_music",
                "sound" => "10_beach_icon_sound",
                "vibration" => "11_beach_icon_vibration",
                "privacy" => "12_beach_icon_privacy",
                "terms" => "13_beach_icon_terms",
                "about" => "14_beach_icon_about",
                "restart" => "15_beach_icon_restart",
                "home" => "16_beach_icon_home",
                _ => null
            };
            return string.IsNullOrEmpty(fileName) ? null : LoadBeachSettingsSprite(fileName);
        }

        private void ConfigureCandyRowChevron(RectTransform row)
        {
            RectTransform chevron = GetOrCreateChildRect(row, "CandyChevron");
            bool candy = IsCandySettingsActive();
            bool ocean = IsOceanSettingsTheme(ThemeCatalog.Current);
            bool garden = IsGardenSettingsActive();
            bool desert = IsDesertSettingsActive();
            bool blossom = IsBlossomSettingsActive();
            bool beach = IsBeachSettingsActive();
            ThemeAssetSet theme = ThemeCatalog.Current;
            chevron.anchorMin = new Vector2(1f, 0.5f);
            chevron.anchorMax = new Vector2(1f, 0.5f);
            chevron.pivot = new Vector2(1f, 0.5f);
            chevron.anchoredPosition = new Vector2(-24f, 0f);
            chevron.sizeDelta = new Vector2(34f, 48f);
            chevron.localScale = Vector3.one;
            Image image = GetOrAddImage(chevron.gameObject);
            image.sprite = ocean
                ? LoadOceanSettingsSprite("ocean_chevron_light")
                : garden ? LoadGardenSettingsSprite("garden_chevron")
                    : candy && theme != null ? theme.SettingsChevronSprite : null;
            image.color = Color.white;
            image.material = null;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = (ocean || candy || garden) && image.sprite != null;
            chevron.gameObject.SetActive(image.enabled);
            if (image.enabled)
            {
                chevron.SetAsLastSibling();
            }

            RectTransform desertChevronRect = GetOrCreateChildRect(row, "DesertChevronGlyph");
            desertChevronRect.anchorMin = new Vector2(1f, 0.5f);
            desertChevronRect.anchorMax = new Vector2(1f, 0.5f);
            desertChevronRect.pivot = new Vector2(1f, 0.5f);
            desertChevronRect.anchoredPosition = new Vector2(-22f, 0f);
            desertChevronRect.sizeDelta = new Vector2(38f, 58f);
            if (desert)
            {
                Image desertChevronImage = GetOrAddImage(desertChevronRect.gameObject);
                desertChevronImage.sprite = LoadDesertCleanSettingsSprite("desert_arrow_right");
                desertChevronImage.color = Color.white;
                desertChevronImage.material = null;
                desertChevronImage.type = Image.Type.Simple;
                desertChevronImage.preserveAspect = true;
                desertChevronImage.raycastTarget = false;
                desertChevronRect.sizeDelta = new Vector2(34f, 42f);
                desertChevronRect.SetAsLastSibling();
            }
            desertChevronRect.gameObject.SetActive(desert);

            // Blossom arrow rows already contain their final arrow artwork.
            // Never add the standalone arrow on top of that baked arrow.
            Transform blossomChevron = row.Find("BlossomChevron");
            if (blossomChevron != null) blossomChevron.gameObject.SetActive(false);

            RectTransform beachChevron = GetOrCreateChildRect(row, "BeachChevron");
            beachChevron.anchorMin = beachChevron.anchorMax = new Vector2(1f, 0.5f);
            beachChevron.pivot = new Vector2(1f, 0.5f);
            beachChevron.anchoredPosition = new Vector2(-26f, 0f);
            beachChevron.sizeDelta = new Vector2(38f, 54f);
            Image beachChevronImage = GetOrAddImage(beachChevron.gameObject);
            beachChevronImage.sprite = beach ? LoadBeachSettingsSprite("08_beach_arrow_right") : null;
            beachChevronImage.color = Color.white;
            beachChevronImage.material = null;
            beachChevronImage.type = Image.Type.Simple;
            beachChevronImage.preserveAspect = true;
            beachChevronImage.raycastTarget = false;
            beachChevronImage.enabled = beach && beachChevronImage.sprite != null;
            beachChevron.gameObject.SetActive(beachChevronImage.enabled);
            if (beachChevronImage.enabled) beachChevron.SetAsLastSibling();
        }

        private void RefreshSettingsRowArtwork()
        {
            if (settingsRowsContainer == null)
            {
                return;
            }

            string[] rowNames =
            {
                "MusicRow", "SoundRow", "VibrationRow", "PrivacyRow",
                "TermsRow", "AboutRow", "RestartRow", "MainMenuRow"
            };

            foreach (string rowName in rowNames)
            {
                Transform row = settingsRowsContainer.Find(rowName);
                if (row != null)
                {
                    ApplySettingsRowArtwork(rowName, GetOrAddImage(row.gameObject));
                }
            }
        }

        private void ApplySettingsRowArtwork(string rowName, Image rowImage)
        {
            if (rowImage == null)
            {
                return;
            }

            ThemeAssetSet activeTheme = ThemeCatalog.Current;
            Sprite rowSprite = GetSettingsRowSprite(activeTheme, rowName);
            bool useSeparatedCandyArtwork = activeTheme != null
                && activeTheme.ThemeType == ThemeType.Candy
                && rowSprite != null;

            rowImage.sprite = rowSprite;
            rowImage.color = Color.white;
            rowImage.material = null;
            rowImage.type = Image.Type.Simple;
            rowImage.preserveAspect = true;
            rowImage.raycastTarget = false;
            rowImage.enabled = useSeparatedCandyArtwork;
        }

        private static Sprite GetSettingsRowSprite(ThemeAssetSet theme, string rowName)
        {
            if (theme == null || theme.ThemeType != ThemeType.Candy)
            {
                return null;
            }

            return rowName switch
            {
                "MusicRow" => theme.SettingsMusicRowSprite,
                "SoundRow" => theme.SettingsSoundRowSprite,
                "VibrationRow" => theme.SettingsVibrationRowSprite,
                "PrivacyRow" => theme.SettingsPrivacyRowSprite,
                "TermsRow" => theme.SettingsTermsRowSprite,
                "AboutRow" => theme.SettingsAboutRowSprite,
                "RestartRow" => theme.SettingsRestartRowSprite,
                "MainMenuRow" => theme.SettingsMainMenuRowSprite,
                _ => null
            };
        }

        private void BuildToggleRow(string rowName, string label, string iconKey, bool sound, bool music)
        {
            bool candy = IsCandySettingsActive();
            bool garden = IsGardenSettingsActive();
            bool desert = IsDesertSettingsActive();
            bool blossom = IsBlossomSettingsActive();
            bool beach = IsBeachSettingsActive();
            RectTransform row = BuildRowBase(rowName, label, iconKey);

            // Keep the three Garden control rows proportionate to the action rows.
            // This is intentionally local to Music/Sound/Vibration.
            if (garden)
            {
                RectTransform labelRect = row.Find("Label") as RectTransform;
                TMP_Text labelText = labelRect == null ? null : labelRect.GetComponent<TMP_Text>();
                if (labelText != null)
                {
                    labelText.enableAutoSizing = true;
                    labelText.fontSizeMin = 19f;
                    labelText.fontSizeMax = 27f;
                    labelText.fontSize = 27f;
                }

                RectTransform iconRect = row.Find("Icon") as RectTransform;
                if (iconRect != null)
                {
                    iconRect.sizeDelta = new Vector2(61f, 61f);
                }
            }

            RectTransform toggle = GetOrCreateChildRect(row, "ToggleControl");
            toggle.anchorMin = new Vector2(1f, 0.5f);
            toggle.anchorMax = new Vector2(1f, 0.5f);
            toggle.pivot = new Vector2(1f, 0.5f);
            toggle.anchoredPosition = candy ? new Vector2(-20f, 0f) : garden ? new Vector2(-22f, 0f) : blossom ? new Vector2(-20f, 0f) : beach ? new Vector2(-18f, 0f) : desert ? new Vector2(-20f, 0f) : new Vector2(-24f, 0f);
            toggle.sizeDelta = candy
                ? new Vector2(CandySettingsControlWidth, CandySettingsControlHeight)
                : garden
                    ? new Vector2(GardenSettingsControlWidth, GardenSettingsControlHeight)
                    : blossom
                        ? new Vector2(BlossomSettingsControlWidth, BlossomSettingsControlHeight)
                    : beach
                        ? new Vector2(BeachSettingsControlWidth, BeachSettingsControlHeight)
                    : desert
                        ? new Vector2(DesertSettingsControlWidth, DesertSettingsControlHeight)
                        : new Vector2(SettingsControlWidth, SettingsControlHeight);
            toggle.localScale = Vector3.one;
            if (garden)
            {
                toggle.SetAsLastSibling();
            }

            Image hitImage = GetOrAddImage(toggle.gameObject);
            hitImage.sprite = null;
            hitImage.color = new Color(1f, 1f, 1f, 0f);
            hitImage.raycastTarget = true;

            Button button = GetOrAddButton(toggle.gameObject, hitImage);
            ConfigureButtonNoTransition(button);
            button.onClick.RemoveAllListeners();
            if (music)
            {
                button.onClick.AddListener(ToggleMusicFromSettings);
            }
            else if (sound)
            {
                button.onClick.AddListener(ToggleSoundFromSettings);
            }
            else
            {
                button.onClick.AddListener(ToggleVibrationFromSettings);
            }

            RectTransform track = GetOrCreateChildRect(toggle, "Track");
            track.anchorMin = Vector2.zero;
            track.anchorMax = Vector2.one;
            track.offsetMin = Vector2.zero;
            track.offsetMax = Vector2.zero;
            track.localScale = Vector3.one;
            Image trackImage = GetOrAddImage(track.gameObject);
            UISpriteFactory.ApplyRounded(trackImage, 0.50f);
            trackImage.raycastTarget = false;
            EnsureGraphicOutline(track.gameObject, new Color(0.65f, 1f, 1f, 0.28f), new Vector2(1f, -1f));

            RectTransform state = GetOrCreateChildRect(toggle, "StateText");
            state.anchorMin = Vector2.zero;
            state.anchorMax = Vector2.one;
            state.offsetMin = new Vector2(10f, 0f);
            state.offsetMax = new Vector2(-58f, 0f);
            state.localScale = Vector3.one;
            TMP_Text stateText = GetOrAddText(state.gameObject);
            stateText.alignment = TextAlignmentOptions.Center;
            stateText.fontSize = 25f;
            stateText.characterSpacing = 0f;
            stateText.fontStyle = FontStyles.Bold;
            stateText.color = Color.white;
            stateText.raycastTarget = false;
            EnsureTextShadow(stateText, new Color(0f, 0.04f, 0.10f, 0.62f), new Vector2(0f, -1.2f));

            RectTransform knob = GetOrCreateChildRect(toggle, "Knob");
            knob.anchorMin = new Vector2(0.5f, 0.5f);
            knob.anchorMax = new Vector2(0.5f, 0.5f);
            knob.pivot = new Vector2(0.5f, 0.5f);
            knob.sizeDelta = new Vector2(48f, 48f);
            knob.localScale = Vector3.one;
            Image knobImage = GetOrAddImage(knob.gameObject);
            UISpriteFactory.ApplySoftCircle(knobImage);
            knobImage.color = new Color(0.96f, 1f, 1f, 1f);
            knobImage.raycastTarget = false;
            EnsureGraphicShadow(knob.gameObject, new Color(0f, 0.05f, 0.14f, 0.45f), new Vector2(0f, -2f));

            if (music)
            {
                musicToggleTrackImage = trackImage;
                musicToggleKnobImage = knobImage;
                musicToggleStateText = stateText;
            }
            else if (sound)
            {
                pauseMuteButton = button;
                soundToggleTrackImage = trackImage;
                soundToggleKnobImage = knobImage;
                soundToggleStateText = stateText;
                pauseMuteButtonText = stateText;
            }
            else
            {
                pauseHapticsButton = button;
                vibrationToggleTrackImage = trackImage;
                vibrationToggleKnobImage = knobImage;
                vibrationToggleStateText = stateText;
                pauseHapticsButtonText = stateText;
            }
        }

        private void BuildLanguageRow()
        {
            RectTransform row = BuildRowBase("LanguageRow", "Language", "language");
            RectTransform buttonRect = GetOrCreateChildRect(row, "LanguageButton");
            ConfigureRightActionButton(buttonRect, "English", OpenLanguageSelection);
            pauseLanguageButton = buttonRect.GetComponent<Button>();
            languageButtonText = buttonRect.Find("Text") == null ? null : buttonRect.Find("Text").GetComponent<TMP_Text>();
            UpdateLanguageRowVisual();
        }

        private void BuildActionRow(string rowName, string label, string iconKey, string buttonLabel, bool restart)
        {
            RectTransform row = BuildRowBase(rowName, label, iconKey);
            RectTransform buttonRect = GetOrCreateChildRect(row, "ActionButton");
            ConfigureTransparentRowButton(buttonRect, restart ? RestartFromPauseSettings : MainMenuFromPauseSettings);
            if (restart)
            {
                pauseRestartButton = buttonRect.GetComponent<Button>();
            }
            else
            {
                pauseMenuButton = buttonRect.GetComponent<Button>();
            }

            ConfigureCandyRowChevron(row);
        }

        private void ConfigureTransparentRowButton(RectTransform buttonRect, UnityEngine.Events.UnityAction action)
        {
            buttonRect.anchorMin = Vector2.zero;
            buttonRect.anchorMax = Vector2.one;
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;
            buttonRect.localScale = Vector3.one;

            Image image = GetOrAddImage(buttonRect.gameObject);
            image.sprite = null;
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;

            Button button = GetOrAddButton(buttonRect.gameObject, image);
            ConfigureButtonNoTransition(button);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);

            Transform text = buttonRect.Find("Text");
            if (text != null)
            {
                text.gameObject.SetActive(false);
            }
        }

        private void ConfigureRightActionButton(RectTransform buttonRect, string label, UnityEngine.Events.UnityAction action)
        {
            buttonRect.anchorMin = new Vector2(1f, 0.5f);
            buttonRect.anchorMax = new Vector2(1f, 0.5f);
            buttonRect.pivot = new Vector2(1f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(-24f, 0f);
            buttonRect.sizeDelta = new Vector2(SettingsControlWidth, SettingsControlHeight);
            buttonRect.localScale = Vector3.one;

            Image image = GetOrAddImage(buttonRect.gameObject);
            UISpriteFactory.ApplyRounded(image, 0.26f);
            image.color = new Color(0.015f, 0.30f, 0.54f, 0.98f);
            image.raycastTarget = true;
            EnsureGraphicOutline(buttonRect.gameObject, new Color(0.48f, 0.94f, 1f, 0.38f), new Vector2(1f, -1f));
            EnsureGraphicShadow(buttonRect.gameObject, new Color(0f, 0.04f, 0.12f, 0.44f), new Vector2(0f, -1.8f));

            Button button = GetOrAddButton(buttonRect.gameObject, image);
            ConfigureButtonNoTransition(button);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);

            RectTransform textRect = GetOrCreateChildRect(buttonRect, "Text");
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            textRect.localScale = Vector3.one;

            TMP_Text text = GetOrAddText(textRect.gameObject);
            text.text = label;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 25f;
            text.enableAutoSizing = true;
            text.fontSizeMax = 25f;
            text.fontSizeMin = 18f;
            text.characterSpacing = 0f;
            text.fontStyle = FontStyles.Bold;
            text.color = Color.white;
            text.raycastTarget = false;
            EnsureTextShadow(text, new Color(0f, 0.04f, 0.10f, 0.65f), new Vector2(0f, -1.2f));
        }

        private RectTransform GetOrCreateChildRect(Transform parent, string childName)
        {
            if (parent == null)
            {
                return null;
            }

            Transform existing = parent.Find(childName);
            RectTransform rect = existing as RectTransform;
            if (rect != null)
            {
                rect.gameObject.SetActive(true);
                return rect;
            }

            GameObject child = new GameObject(childName, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
        }

        private Image GetOrAddImage(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            Image image = target.GetComponent<Image>();
            if (image != null)
            {
                return image;
            }

            if (target.GetComponent<TMP_Text>() != null || target.GetComponent<Text>() != null)
            {
                return null;
            }

            return target.AddComponent<Image>();
        }

        private TMP_Text GetOrAddText(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            TMP_Text text = target.GetComponent<TMP_Text>();
            if (text != null)
            {
                return text;
            }

            return target.AddComponent<TextMeshProUGUI>();
        }

        private Button GetOrAddButton(GameObject target, Graphic targetGraphic)
        {
            if (target == null)
            {
                return null;
            }

            Button button = target.GetComponent<Button>();
            if (button == null)
            {
                button = target.AddComponent<Button>();
            }

            button.targetGraphic = targetGraphic;
            return button;
        }

        private void ConfigureButtonNoTransition(Button button)
        {
            if (button == null)
            {
                return;
            }

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
            button.interactable = true;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        private void EnsureGraphicShadow(GameObject target, Color color, Vector2 distance)
        {
            if (target == null)
            {
                return;
            }

            Shadow shadow = null;
            Shadow[] shadows = target.GetComponents<Shadow>();
            for (int i = 0; i < shadows.Length; i++)
            {
                if (shadows[i] == null || shadows[i].GetType() != typeof(Shadow))
                {
                    continue;
                }

                if (shadow == null)
                {
                    shadow = shadows[i];
                }
                else
                {
                    shadows[i].enabled = false;
                }
            }

            if (shadow == null)
            {
                shadow = target.AddComponent<Shadow>();
            }

            shadow.enabled = true;
            shadow.effectColor = color;
            shadow.effectDistance = distance;
            shadow.useGraphicAlpha = true;
        }

        private void EnsureGraphicOutline(GameObject target, Color color, Vector2 distance)
        {
            if (target == null)
            {
                return;
            }

            Outline outline = null;
            Outline[] outlines = target.GetComponents<Outline>();
            for (int i = 0; i < outlines.Length; i++)
            {
                if (outlines[i] == null)
                {
                    continue;
                }

                if (outline == null)
                {
                    outline = outlines[i];
                }
                else
                {
                    outlines[i].enabled = false;
                }
            }

            if (outline == null)
            {
                outline = target.AddComponent<Outline>();
            }

            outline.enabled = true;
            outline.effectColor = color;
            outline.effectDistance = distance;
            outline.useGraphicAlpha = true;
        }

        private Sprite GetSettingsIconSprite(string iconKey)
        {
            switch (iconKey)
            {
                case "music":
                    return musicIconSprite ??= BuildSettingsIconSprite(iconKey);
                case "sound":
                    return soundIconSprite ??= BuildSettingsIconSprite(iconKey);
                case "vibration":
                    return vibrationIconSprite ??= BuildSettingsIconSprite(iconKey);
                case "language":
                    return languageIconSprite ??= BuildSettingsIconSprite(iconKey);
                case "restart":
                    restartIconSprite ??= Resources.Load<Sprite>(SettingsRestartIconPath);
                    return restartIconSprite ??= BuildSettingsIconSprite(iconKey);
                case "home":
                    return mainMenuIconSprite ??= BuildSettingsIconSprite(iconKey);
                default:
                    return BuildSettingsIconSprite("default");
            }
        }

        private Sprite BuildSettingsIconSprite(string iconKey, bool desert = false)
        {
            const int size = 96;
            Color32[] pixels = new Color32[size * size];
            Color32 clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            Color32 circle = desert ? new Color32(238, 126, 24, 255) : new Color32(4, 108, 178, 245);
            Color32 circleDark = desert ? new Color32(178, 76, 12, 235) : new Color32(1, 45, 96, 210);
            Color32 cyan = desert ? new Color32(255, 190, 80, 245) : new Color32(76, 232, 255, 230);
            Color32 white = desert ? new Color32(255, 244, 218, 255) : new Color32(245, 252, 255, 255);

            DrawFilledCircle(pixels, size, 48, 48, 42, circle);
            DrawFilledCircle(pixels, size, 48, 38, 30, circleDark);
            DrawCircleOutline(pixels, size, 48, 48, 42, 3, cyan);
            DrawCircleOutline(pixels, size, 48, 48, 35, 1, new Color32(255, 255, 255, 80));

            switch (iconKey)
            {
                case "music":
                    DrawFilledRect(pixels, size, 44, 27, 6, 40, white);
                    DrawFilledRect(pixels, size, 68, 22, 6, 39, white);
                    DrawFilledRect(pixels, size, 47, 24, 27, 7, white);
                    DrawFilledCircle(pixels, size, 37, 68, 10, white);
                    DrawFilledCircle(pixels, size, 61, 62, 10, white);
                    break;
                case "sound":
                    DrawFilledRect(pixels, size, 26, 41, 11, 20, white);
                    DrawFilledTriangle(pixels, size, new Vector2Int(37, 39), new Vector2Int(58, 27), new Vector2Int(58, 73), white);
                    DrawArc(pixels, size, 57, 50, 16, -45f, 45f, 3, white);
                    DrawArc(pixels, size, 58, 50, 24, -42f, 42f, 3, white);
                    break;
                case "vibration":
                    DrawRectOutline(pixels, size, 35, 24, 27, 48, 4, white);
                    DrawFilledRect(pixels, size, 44, 29, 9, 3, white);
                    DrawFilledCircle(pixels, size, 48, 65, 2, white);
                    DrawZigZag(pixels, size, 26, 32, 26, 64, 6, 3, white);
                    DrawZigZag(pixels, size, 70, 32, 70, 64, 6, 3, white);
                    break;
                case "language":
                    DrawCircleOutline(pixels, size, 48, 48, 23, 4, white);
                    DrawArc(pixels, size, 48, 48, 14, 90f, 270f, 3, white);
                    DrawArc(pixels, size, 48, 48, 14, -90f, 90f, 3, white);
                    DrawLine(pixels, size, 26, 48, 70, 48, 3, white);
                    DrawLine(pixels, size, 31, 37, 65, 37, 2, white);
                    DrawLine(pixels, size, 31, 59, 65, 59, 2, white);
                    break;
                case "restart":
                    DrawArc(pixels, size, 48, 49, 24, 35f, 325f, 5, white);
                    DrawFilledTriangle(pixels, size, new Vector2Int(61, 25), new Vector2Int(74, 24), new Vector2Int(68, 38), white);
                    break;
                case "home":
                    DrawFilledTriangle(pixels, size, new Vector2Int(22, 49), new Vector2Int(48, 27), new Vector2Int(74, 49), white);
                    DrawFilledRect(pixels, size, 30, 48, 36, 25, white);
                    DrawFilledRect(pixels, size, 44, 58, 9, 15, circle);
                    break;
                default:
                    DrawFilledCircle(pixels, size, 48, 48, 17, white);
                    break;
            }

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private void DrawFilledCircle(Color32[] pixels, int size, int centerX, int centerY, int radius, Color32 color)
        {
            int radiusSquared = radius * radius;
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    int dx = x - centerX;
                    int dy = y - centerY;
                    if (dx * dx + dy * dy <= radiusSquared)
                    {
                        SetPixel(pixels, size, x, y, color);
                    }
                }
            }
        }

        private void DrawCircleOutline(Color32[] pixels, int size, int centerX, int centerY, int radius, int thickness, Color32 color)
        {
            int outer = radius * radius;
            int innerRadius = Mathf.Max(0, radius - thickness);
            int inner = innerRadius * innerRadius;
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    int dx = x - centerX;
                    int dy = y - centerY;
                    int distance = dx * dx + dy * dy;
                    if (distance <= outer && distance >= inner)
                    {
                        SetPixel(pixels, size, x, y, color);
                    }
                }
            }
        }

        private void DrawArc(Color32[] pixels, int size, int centerX, int centerY, int radius, float startAngle, float endAngle, int thickness, Color32 color)
        {
            int steps = Mathf.Max(16, Mathf.RoundToInt(Mathf.Abs(endAngle - startAngle) * 0.55f));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float angle = Mathf.Lerp(startAngle, endAngle, t) * Mathf.Deg2Rad;
                int x = centerX + Mathf.RoundToInt(Mathf.Cos(angle) * radius);
                int y = centerY + Mathf.RoundToInt(Mathf.Sin(angle) * radius);
                DrawFilledCircle(pixels, size, x, y, thickness, color);
            }
        }

        private void DrawLine(Color32[] pixels, int size, int x0, int y0, int x1, int y1, int thickness, Color32 color)
        {
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int steps = Mathf.Max(dx, dy);
            if (steps == 0)
            {
                DrawFilledCircle(pixels, size, x0, y0, thickness, color);
                return;
            }

            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
                int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                DrawFilledCircle(pixels, size, x, y, thickness, color);
            }
        }

        private void DrawZigZag(Color32[] pixels, int size, int x0, int y0, int x1, int y1, int amplitude, int thickness, Color32 color)
        {
            int segments = 5;
            Vector2 previous = new Vector2(x0, y0);
            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                float x = Mathf.Lerp(x0, x1, t) + (i % 2 == 0 ? -amplitude : amplitude);
                float y = Mathf.Lerp(y0, y1, t);
                DrawLine(pixels, size, Mathf.RoundToInt(previous.x), Mathf.RoundToInt(previous.y), Mathf.RoundToInt(x), Mathf.RoundToInt(y), thickness, color);
                previous = new Vector2(x, y);
            }
        }

        private void DrawFilledRect(Color32[] pixels, int size, int x, int y, int width, int height, Color32 color)
        {
            for (int yy = y; yy < y + height; yy++)
            {
                for (int xx = x; xx < x + width; xx++)
                {
                    SetPixel(pixels, size, xx, yy, color);
                }
            }
        }

        private void DrawRectOutline(Color32[] pixels, int size, int x, int y, int width, int height, int thickness, Color32 color)
        {
            DrawFilledRect(pixels, size, x, y, width, thickness, color);
            DrawFilledRect(pixels, size, x, y + height - thickness, width, thickness, color);
            DrawFilledRect(pixels, size, x, y, thickness, height, color);
            DrawFilledRect(pixels, size, x + width - thickness, y, thickness, height, color);
        }

        private void DrawFilledTriangle(Color32[] pixels, int size, Vector2Int a, Vector2Int b, Vector2Int c, Color32 color)
        {
            int minX = Mathf.Min(a.x, Mathf.Min(b.x, c.x));
            int maxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
            int minY = Mathf.Min(a.y, Mathf.Min(b.y, c.y));
            int maxY = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
            float area = Edge(a, b, c);
            if (Mathf.Approximately(area, 0f))
            {
                return;
            }

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2Int p = new Vector2Int(x, y);
                    float w0 = Edge(b, c, p);
                    float w1 = Edge(c, a, p);
                    float w2 = Edge(a, b, p);
                    if ((w0 >= 0f && w1 >= 0f && w2 >= 0f) || (w0 <= 0f && w1 <= 0f && w2 <= 0f))
                    {
                        SetPixel(pixels, size, x, y, color);
                    }
                }
            }
        }

        private float Edge(Vector2Int a, Vector2Int b, Vector2Int c)
        {
            return (c.x - a.x) * (b.y - a.y) - (c.y - a.y) * (b.x - a.x);
        }

        private void SetPixel(Color32[] pixels, int size, int x, int y, Color32 color)
        {
            if (x < 0 || x >= size || y < 0 || y >= size)
            {
                return;
            }

            pixels[y * size + x] = color;
        }

        private void RestartFromPauseSettings()
        {
            AudioManager.Instance?.PlayClick();
            if (gameManager == null)
            {
                Debug.LogWarning("Pause Restart button cannot run because GameManager is missing.");
                return;
            }

            gameManager.RestartCurrentMode();
        }

        private void MainMenuFromPauseSettings()
        {
            AudioManager.Instance?.PlayClick();
            if (gameManager == null)
            {
                Debug.LogWarning("Pause Main Menu button cannot run because GameManager is missing.");
                return;
            }

            gameManager.GoToMenu();
        }

        private void DisableRemovedPauseTutorialRow()
        {
            if (pauseResetTutorialButton != null)
            {
                pauseResetTutorialButton.onClick.RemoveAllListeners();
                pauseResetTutorialButton.gameObject.SetActive(false);
            }
        }

        private bool IsSettingsToggleStateLabel(TMP_Text text)
        {
            return text != null && (text.gameObject.name == "SettingsToggleStateLabel" || text.gameObject.name == "StateText");
        }

        private void EnsureSettingsRaycastPath()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                Debug.LogWarning("Settings popup click input is missing an EventSystem.");
            }

            if (pausePanelRoot != null)
            {
                pausePanelRoot.SetAsLastSibling();
            }
        }

        private Sprite LoadSettingsSprite(string resourcesPath)
        {
            Sprite sprite = Resources.Load<Sprite>(resourcesPath);
            if (sprite == null)
            {
                Debug.LogWarning($"Missing Ocean Settings sprite at Resources path: {resourcesPath}");
            }

            return sprite;
        }

        private void UpdateLanguageRowVisual()
        {
            if (pauseLanguageButton == null)
            {
                return;
            }

            string selectedLanguage = PlayerPrefs.GetString("SelectedLanguage", "en");
            string longValue = selectedLanguage == "ro" ? "Română" : "English";

            if (languageButtonText != null)
            {
                languageButtonText.text = longValue;
                languageButtonText.alignment = TextAlignmentOptions.Center;
                languageButtonText.fontSize = 24f;
                languageButtonText.enableAutoSizing = true;
                languageButtonText.fontSizeMax = 24f;
                languageButtonText.fontSizeMin = 15f;
                languageButtonText.characterSpacing = 0f;
                languageButtonText.fontStyle = FontStyles.Bold;
                languageButtonText.color = Color.white;
                languageButtonText.raycastTarget = false;

                if (languageValueOverlay != null)
                {
                    languageValueOverlay.gameObject.SetActive(false);
                }

                Transform legacyOverlay = pauseLanguageButton.transform.Find("LanguageValueOverlay");
                if (legacyOverlay != null)
                {
                    legacyOverlay.gameObject.SetActive(false);
                }

                return;
            }

            if (languageValueOverlay == null)
            {
                Transform existing = pauseLanguageButton.transform.Find("LanguageValueOverlay");
                if (existing != null)
                {
                    languageValueOverlay = existing.GetComponent<TMP_Text>();
                }
            }

            if (languageValueOverlay == null)
            {
                GameObject valueObject = new GameObject("LanguageValueOverlay", typeof(RectTransform), typeof(TextMeshProUGUI));
                valueObject.transform.SetParent(pauseLanguageButton.transform, false);
                languageValueOverlay = valueObject.GetComponent<TMP_Text>();
            }

            RectTransform rect = languageValueOverlay.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.650f, 0.18f);
                rect.anchorMax = new Vector2(0.910f, 0.82f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
                rect.pivot = new Vector2(0.5f, 0.5f);
            }

            languageValueOverlay.text = longValue;
            languageValueOverlay.alignment = TextAlignmentOptions.Center;
            languageValueOverlay.fontSize = 22f;
            languageValueOverlay.fontStyle = FontStyles.Bold;
            languageValueOverlay.color = Color.white;
            languageValueOverlay.raycastTarget = false;
            languageValueOverlay.enabled = true;
            languageValueOverlay.gameObject.SetActive(true);

            Transform fallbackValue = pauseLanguageButton.transform.Find("LanguageFallbackValue");
            TMP_Text fallbackText = fallbackValue == null ? null : fallbackValue.GetComponent<TMP_Text>();
            if (fallbackText != null)
            {
                fallbackText.text = longValue;
            }
        }

        private void UpdateSettingsToggleVisuals(bool logSoundState, bool logVibrationState = false)
        {
            bool musicOn = IsMusicEnabled();
            bool soundOn = IsSoundEnabled();
            bool vibrationOn = IsVibrationEnabled();

            ApplyModernToggleVisual(musicToggleTrackImage, musicToggleKnobImage, musicToggleStateText, musicOn);
            ApplyModernToggleVisual(soundToggleTrackImage, soundToggleKnobImage, soundToggleStateText, soundOn);
            ApplyModernToggleVisual(vibrationToggleTrackImage, vibrationToggleKnobImage, vibrationToggleStateText, vibrationOn);

            if (logSoundState)
            {
                Debug.Log($"Sound toggled visual state: {(soundOn ? "ON" : "OFF")}");
            }

        }

        private void ApplyModernToggleVisual(Image trackImage, Image knobImage, TMP_Text stateText, bool enabledState)
        {
            ThemeAssetSet activeTheme = ThemeCatalog.Current;
            bool ocean = IsOceanSettingsTheme(activeTheme);
            bool garden = IsGardenSettingsTheme(activeTheme);
            bool desert = IsDesertSettingsTheme(activeTheme);
            bool blossom = IsBlossomSettingsTheme(activeTheme);
            bool beach = IsBeachSettingsTheme(activeTheme);
            Sprite candyToggleSprite = activeTheme != null && activeTheme.ThemeType == ThemeType.Candy
                ? (enabledState ? activeTheme.SettingsToggleOnSprite : activeTheme.SettingsToggleOffSprite)
                : ocean
                    ? LoadOceanSettingsSprite(enabledState ? "ocean_toggle_on_light" : "ocean_toggle_off_light")
                    : garden
                        ? LoadGardenSettingsSprite(enabledState ? "garden_toggle_on" : "garden_toggle_off")
                        : null;

            if (blossom)
            {
                if (trackImage != null)
                {
                    // Cover the baked ON region with the corresponding final
                    // ON/OFF control so exactly one complete toggle is visible.
                    trackImage.gameObject.SetActive(true);
                    trackImage.enabled = true;
                    trackImage.sprite = LoadBlossomSettingsSprite(enabledState ? "06_blossom_toggle_on" : "24_blossom_toggle_off_clean");
                    trackImage.color = Color.white;
                    trackImage.material = null;
                    trackImage.type = Image.Type.Simple;
                    trackImage.preserveAspect = true;
                    trackImage.raycastTarget = false;
                    Outline outline = trackImage.GetComponent<Outline>();
                    Shadow shadow = trackImage.GetComponent<Shadow>();
                    if (outline != null) outline.enabled = false;
                    if (shadow != null) shadow.enabled = false;
                }
                if (stateText != null)
                {
                    stateText.text = string.Empty;
                    stateText.enabled = false;
                    stateText.gameObject.SetActive(false);
                }
                if (knobImage != null)
                {
                    knobImage.enabled = false;
                    knobImage.gameObject.SetActive(false);
                    knobImage.raycastTarget = false;
                }
                return;
            }

            if (beach)
            {
                if (trackImage != null)
                {
                    trackImage.gameObject.SetActive(true);
                    trackImage.enabled = true;
                    trackImage.sprite = LoadBeachSettingsSprite(enabledState ? "06_beach_toggle_on" : "07_beach_toggle_off");
                    trackImage.color = Color.white;
                    trackImage.material = null;
                    trackImage.type = Image.Type.Simple;
                    trackImage.preserveAspect = true;
                    trackImage.raycastTarget = false;
                    Outline outline = trackImage.GetComponent<Outline>();
                    Shadow shadow = trackImage.GetComponent<Shadow>();
                    if (outline != null) outline.enabled = false;
                    if (shadow != null) shadow.enabled = false;
                }
                if (stateText != null)
                {
                    stateText.text = string.Empty;
                    stateText.enabled = false;
                    stateText.gameObject.SetActive(false);
                }
                if (knobImage != null)
                {
                    knobImage.enabled = false;
                    knobImage.gameObject.SetActive(false);
                    knobImage.raycastTarget = false;
                }
                return;
            }

            if (desert)
            {
                if (trackImage != null)
                {
                    trackImage.gameObject.SetActive(true);
                    trackImage.enabled = true;
                    trackImage.sprite = LoadDesertCleanSettingsSprite(enabledState ? "desert_toggle_on_final" : "desert_toggle_off_final");
                    trackImage.color = Color.white;
                    trackImage.material = null;
                    trackImage.type = Image.Type.Simple;
                    trackImage.preserveAspect = true;
                    trackImage.raycastTarget = false;
                    Outline outline = trackImage.GetComponent<Outline>();
                    if (outline != null)
                    {
                        outline.enabled = false;
                    }
                }

                if (stateText != null)
                {
                    RectTransform stateRect = stateText.rectTransform;
                    stateRect.offsetMin = enabledState ? new Vector2(10f, 0f) : new Vector2(58f, 0f);
                    stateRect.offsetMax = enabledState ? new Vector2(-58f, 0f) : new Vector2(-10f, 0f);
                    stateText.text = string.Empty;
                    stateText.enabled = false;
                    stateText.gameObject.SetActive(false);
                }

                if (knobImage != null)
                {
                    knobImage.gameObject.SetActive(false);
                    knobImage.enabled = false;
                    knobImage.raycastTarget = false;
                }

                return;
            }

            if (candyToggleSprite != null)
            {
                if (trackImage != null)
                {
                    trackImage.gameObject.SetActive(true);
                    trackImage.enabled = true;
                    trackImage.sprite = candyToggleSprite;
                    trackImage.color = Color.white;
                    trackImage.material = null;
                    trackImage.type = Image.Type.Simple;
                    trackImage.preserveAspect = true;
                    trackImage.raycastTarget = false;
                    if (garden)
                    {
                        trackImage.transform.SetAsLastSibling();
                        trackImage.canvasRenderer.SetAlpha(1f);
                        trackImage.SetAllDirty();
                    }

                    Outline outline = trackImage.GetComponent<Outline>();
                    if (outline != null)
                    {
                        outline.enabled = false;
                    }

                    Shadow shadow = trackImage.GetComponent<Shadow>();
                    if (shadow != null)
                    {
                        shadow.enabled = false;
                    }
                }

                if (stateText != null)
                {
                    stateText.text = string.Empty;
                    stateText.enabled = false;
                    stateText.gameObject.SetActive(false);
                }

                if (knobImage != null)
                {
                    knobImage.enabled = false;
                    knobImage.gameObject.SetActive(false);
                }

                return;
            }

            if (trackImage != null)
            {
                // The themed Settings artwork already contains the complete ON
                // toggle.  Keep that artwork unobstructed while enabled, and
                // only draw a rounded, theme-coloured replacement for OFF.
                trackImage.gameObject.SetActive(!enabledState);
                trackImage.enabled = !enabledState;
                trackImage.color = GetInactiveSettingsToggleColor();
                trackImage.raycastTarget = false;
            }

            if (stateText != null)
            {
                RectTransform stateRect = stateText.rectTransform;
                if (stateRect != null)
                {
                    stateRect.offsetMin = new Vector2(58f, 0f);
                    stateRect.offsetMax = new Vector2(-10f, 0f);
                    stateRect.localScale = Vector3.one;
                }

                stateText.text = "OFF";
                stateText.color = Color.white;
                stateText.gameObject.SetActive(!enabledState);
            }

            if (knobImage != null)
            {
                RectTransform knobRect = knobImage.rectTransform;
                if (knobRect != null)
                {
                    knobRect.anchoredPosition = new Vector2(-66f, 0f);
                    knobRect.localScale = Vector3.one;
                }

                knobImage.color = new Color(0.96f, 1f, 1f, 1f);
                knobImage.raycastTarget = false;
                knobImage.gameObject.SetActive(!enabledState);
            }
        }

        private static Color GetInactiveSettingsToggleColor()
        {
            ThemeAssetSet activeTheme = ThemeCatalog.Current;
            ThemeType themeType = activeTheme != null ? activeTheme.ThemeType : ThemeType.Ocean;
            return themeType switch
            {
                ThemeType.Crystal => new Color(0.23f, 0.48f, 0.08f, 0.98f),
                ThemeType.Neon => new Color(0.63f, 0.10f, 0.30f, 0.98f),
                ThemeType.Candy => new Color(0.70f, 0.16f, 0.38f, 0.98f),
                ThemeType.Aqua => new Color(0.72f, 0.30f, 0.04f, 0.98f),
                _ => new Color(0.05f, 0.30f, 0.60f, 0.98f)
            };
        }

        private static Color GetActiveSettingsToggleColor()
        {
            ThemeAssetSet activeTheme = ThemeCatalog.Current;
            ThemeType themeType = activeTheme != null ? activeTheme.ThemeType : ThemeType.Ocean;
            return themeType switch
            {
                ThemeType.Crystal => new Color(0.27f, 0.65f, 0.05f, 0.96f),
                ThemeType.Neon => new Color(0.95f, 0.16f, 0.47f, 0.96f),
                ThemeType.Candy => new Color(0.96f, 0.16f, 0.46f, 0.96f),
                ThemeType.Aqua => new Color(1.00f, 0.43f, 0.00f, 0.96f),
                _ => new Color(0.00f, 0.52f, 0.96f, 0.96f)
            };
        }

        private bool IsSoundEnabled()
        {
            if (AudioManager.Instance != null)
            {
                return !AudioManager.Instance.Muted;
            }

            return PlayerPrefs.GetInt("SoundEnabled", AudioListener.volume > 0.001f ? 1 : 0) != 0;
        }

        private bool IsMusicEnabled()
        {
            AudioManager audio = AudioManager.Instance;
            return audio != null
                ? audio.MusicEnabled
                : PlayerPrefs.GetInt("MusicEnabled", 1) != 0;
        }

        private bool IsVibrationEnabled()
        {
            return Haptics.IsEnabled();
        }

        private void DisableGeneratedPauseDecor()
        {
            if (pauseRoot != null)
            {
                DisableChild(pauseRoot.transform, "OceanPauseTopScrim");
            }

            if (pausePanelRoot == null)
            {
                return;
            }

            DisableChild(pausePanelRoot, "OceanPausePanelOuterGlow");
            DisableChild(pausePanelRoot, "OceanPausePanelInnerGlow");
            DisableChild(pausePanelRoot, "OceanPausePanelTopGloss");
            DisableChild(pausePanelRoot, "OceanPausePanelBottomDepth");
        }

        private void DisableChild(Transform parent, string childName)
        {
            Transform child = parent == null ? null : parent.Find(childName);
            if (child != null)
            {
                child.gameObject.SetActive(false);
            }
        }

        private void DisableSelectableDecor(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            Shadow[] shadows = target.GetComponents<Shadow>();
            for (int i = 0; i < shadows.Length; i++)
            {
                if (shadows[i] != null)
                {
                    shadows[i].enabled = false;
                }
            }

            Outline[] outlines = target.GetComponents<Outline>();
            for (int i = 0; i < outlines.Length; i++)
            {
                if (outlines[i] != null)
                {
                    outlines[i].enabled = false;
                }
            }
        }

        private void OpenLanguageSelection()
        {
            AudioManager.Instance?.PlayClick();
            EnsureLanguageSelectionPopup();
            if (languagePopupRoot != null)
            {
                languagePopupRoot.SetActive(true);
                languagePopupRoot.transform.SetAsLastSibling();
                RefreshLanguagePopupSelectionVisuals();
            }
        }

        private void EnsureLanguageSelectionPopup()
        {
            RectTransform pauseRect = pauseRoot == null ? null : pauseRoot.transform as RectTransform;
            if (pauseRect == null)
            {
                return;
            }

            if (pausePanelRoot != null)
            {
                DisableChild(pausePanelRoot, "LanguageSelectionPopup");
            }

            RectTransform overlayRect = GetOrCreateChildRect(pauseRect, "LanguageOverlay");
            languagePopupRoot = overlayRect.gameObject;
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            overlayRect.localScale = Vector3.one;
            overlayRect.pivot = new Vector2(0.5f, 0.5f);
            overlayRect.SetAsLastSibling();

            Image overlayImage = GetOrAddImage(overlayRect.gameObject);
            if (overlayImage != null)
            {
                overlayImage.sprite = null;
                overlayImage.type = Image.Type.Simple;
                overlayImage.color = new Color(0f, 0f, 0f, 0.35f);
                overlayImage.raycastTarget = true;
            }

            Button overlayButton = GetOrAddButton(overlayRect.gameObject, overlayImage);
            ConfigureButtonNoTransition(overlayButton);
            overlayButton.onClick.RemoveAllListeners();
            overlayButton.onClick.AddListener(CloseLanguageSelectionPopup);

            RectTransform popupRect = GetOrCreateChildRect(overlayRect, "LanguagePopup");
            popupRect.anchorMin = new Vector2(0.5f, 0.5f);
            popupRect.anchorMax = new Vector2(0.5f, 0.5f);
            popupRect.anchoredPosition = Vector2.zero;
            popupRect.sizeDelta = new Vector2(430f, 500f);
            popupRect.localScale = Vector3.one;
            popupRect.pivot = new Vector2(0.5f, 0.5f);
            popupRect.SetAsLastSibling();

            Image popupImage = GetOrAddImage(popupRect.gameObject);
            UISpriteFactory.ApplyRounded(popupImage, 0.24f);
            popupImage.color = new Color(0.015f, 0.13f, 0.30f, 0.97f);
            popupImage.raycastTarget = true;
            EnsureGraphicOutline(popupRect.gameObject, new Color(0.34f, 0.93f, 1f, 0.38f), new Vector2(1.5f, -1.5f));
            EnsureGraphicShadow(popupRect.gameObject, new Color(0f, 0.05f, 0.13f, 0.58f), new Vector2(0f, -4f));

            ConfigureLanguagePopupTitle(popupRect);
            ConfigureLanguageChoiceButton(popupRect, "EnglishButton", "English", new Vector2(0f, 84f), () => SelectLanguage("en"));
            ConfigureLanguageChoiceButton(popupRect, "RomanianButton", "Română", new Vector2(0f, -20f), () => SelectLanguage("ro"));
            ConfigureLanguageChoiceButton(popupRect, "CloseButton", "CLOSE", new Vector2(0f, -150f), CloseLanguageSelectionPopup);
            RefreshLanguagePopupSelectionVisuals();

            languagePopupRoot.SetActive(false);
        }

        private void ConfigureLanguagePopupTitle(RectTransform parent)
        {
            RectTransform rect = GetOrCreateChildRect(parent, "LanguageTitle");
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 178f);
            rect.sizeDelta = new Vector2(360f, 70f);
            rect.localScale = Vector3.one;
            rect.pivot = new Vector2(0.5f, 0.5f);

            Image image = rect.GetComponent<Image>();
            if (image != null)
            {
                image.enabled = false;
                image.raycastTarget = false;
            }

            TMP_Text label = GetOrAddText(rect.gameObject);
            label.text = "LANGUAGE";
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 38f;
            label.characterSpacing = 1.2f;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(0.90f, 1f, 1f, 1f);
            label.raycastTarget = false;
            EnsureTextShadow(label, new Color(0f, 0.05f, 0.12f, 0.72f), new Vector2(0f, -1.8f));
        }

        private void ConfigureLanguageChoiceButton(RectTransform parent, string objectName, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = GetOrCreateChildRect(parent, objectName);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(330f, 80f);
            rect.localScale = Vector3.one;
            rect.pivot = new Vector2(0.5f, 0.5f);

            Image image = GetOrAddImage(rect.gameObject);
            UISpriteFactory.ApplyRounded(image, 0.28f);
            image.color = new Color(0.02f, 0.50f, 0.88f, 0.96f);
            image.raycastTarget = true;
            EnsureGraphicOutline(rect.gameObject, new Color(0.64f, 1f, 1f, 0.30f), new Vector2(1f, -1f));
            EnsureGraphicShadow(rect.gameObject, new Color(0f, 0.04f, 0.12f, 0.42f), new Vector2(0f, -1.8f));

            Button button = GetOrAddButton(rect.gameObject, image);
            ConfigureButtonNoTransition(button);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);

            RectTransform textRect = GetOrCreateChildRect(rect, "Label");
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 0f);
            textRect.offsetMax = new Vector2(-16f, 0f);
            textRect.localScale = Vector3.one;

            TMP_Text text = GetOrAddText(textRect.gameObject);
            text.text = label;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 30f;
            text.fontStyle = FontStyles.Bold;
            text.color = Color.white;
            text.raycastTarget = false;
            EnsureTextShadow(text, new Color(0f, 0.04f, 0.10f, 0.64f), new Vector2(0f, -1.2f));
        }

        private void SelectLanguage(string languageCode)
        {
            EnforceEnglishLanguage();
            AudioManager.Instance?.PlayClick();
            UpdateLanguageRowVisual();
            RefreshLanguagePopupSelectionVisuals();
            CloseLanguageSelectionPopup(false);
        }

        private void CloseLanguageSelectionPopup()
        {
            CloseLanguageSelectionPopup(true);
        }

        private void CloseLanguageSelectionPopup(bool playClick)
        {
            if (playClick)
            {
                AudioManager.Instance?.PlayClick();
            }

            if (languagePopupRoot != null)
            {
                languagePopupRoot.SetActive(false);
            }
        }

        private void RefreshLanguagePopupSelectionVisuals()
        {
            if (languagePopupRoot == null)
            {
                return;
            }

            RectTransform overlayRect = languagePopupRoot.transform as RectTransform;
            RectTransform popupRect = overlayRect == null ? null : overlayRect.Find("LanguagePopup") as RectTransform;
            if (popupRect == null)
            {
                return;
            }

            string selectedLanguage = PlayerPrefs.GetString("SelectedLanguage", "en");
            ApplyLanguageButtonSelection(popupRect, "EnglishButton", selectedLanguage == "en");
            ApplyLanguageButtonSelection(popupRect, "RomanianButton", selectedLanguage == "ro");
        }

        private void ApplyLanguageButtonSelection(RectTransform popupRect, string buttonName, bool selected)
        {
            Transform buttonTransform = popupRect == null ? null : popupRect.Find(buttonName);
            Image image = buttonTransform == null ? null : buttonTransform.GetComponent<Image>();
            if (image != null)
            {
                image.color = selected
                    ? new Color(0.02f, 0.68f, 0.98f, 1f)
                    : new Color(0.02f, 0.50f, 0.88f, 0.96f);
            }

            TMP_Text text = buttonTransform == null ? null : buttonTransform.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                string baseLabel = buttonName == "RomanianButton" ? "Română" : "English";
                text.text = selected ? baseLabel + "  ✓" : baseLabel;
            }
        }

        private void AnimatePausePanelIn()
        {
            if (pausePanelRoot == null || MobilePerformance.LowEndMode)
            {
                return;
            }

            pausePanelRoot.DOKill();
            pausePanelRoot.localScale = Vector3.one * 0.965f;
            pausePanelRoot.DOScale(1f, 0.14f).SetEase(Ease.OutBack);
        }

        private static string GetEnglishColorName(ChromaColor color)
        {
            switch (color)
            {
                case ChromaColor.Cyan:
                    return "CYAN";
                case ChromaColor.Magenta:
                    return "MAGENTA";
                case ChromaColor.Lime:
                    return "LIME";
                case ChromaColor.Amber:
                    return "GOLD";
                default:
                    return "COLOR";
            }
        }

        private static string GetEnglishAchievementTitle(AchievementId id)
        {
            switch (id)
            {
                case AchievementId.FirstClear:
                    return "FIRST LINE";
                case AchievementId.FirstPure:
                    return "PURE!";
                case AchievementId.FirstPop:
                    return "FIRST POP";
                case AchievementId.ChainThree:
                    return "CHAIN x3";
                case AchievementId.ScoreThousand:
                    return "SCORE 1000";
                case AchievementId.TripleClear:
                    return "TRIPLE CLEAR";
                case AchievementId.FirstDaily:
                    return "DAILY START";
                case AchievementId.BoardSweep:
                    return "BOARD CLEARED";
                default:
                    return "FIRST MOVE";
            }
        }

        private static string GetEnglishDailyQuestTitle(string questName)
        {
            switch (questName)
            {
                case "MUTARI ZILNICE":
                    return "DAILY MOVES";
                case "LINII ZILNICE":
                    return "DAILY LINES";
                case "PURE ZILNIC":
                    return "DAILY PURE";
                case "POP ZILNIC":
                    return "DAILY POP";
                case "SCOR ZILNIC":
                    return "DAILY SCORE";
                case "OBIECTIVE":
                    return "DAILY OBJECTIVES";
                default:
                    return "DAILY OBJECTIVE";
            }
        }

        private void ShowFeedback(string message, Color color, float duration)
        {
            bool functionalHint = message == "DOESN'T FIT"
                || message == "NO SPACE"
                || message == "POP HAS NO TILES"
                || message == "POP CAN SAVE YOU"
                || message == "TRY HERE";
            if (!functionalHint)
            {
                return;
            }

            if (feedbackText == null)
            {
                return;
            }

            if (feedbackRoutine != null)
            {
                StopCoroutine(feedbackRoutine);
            }

            feedbackText.text = message;
            feedbackText.color = color;
            feedbackText.gameObject.SetActive(true);
            feedbackText.transform.DOKill();
            feedbackText.transform.localScale = Vector3.one;
            feedbackText.transform.DOPunchScale(Vector3.one * 0.18f, 0.16f, 6, 0.7f);
            feedbackRoutine = StartCoroutine(HideFeedbackAfter(duration));
        }

        private IEnumerator HideFeedbackAfter(float duration)
        {
            yield return new WaitForSeconds(duration);
            HideFeedback();
            feedbackRoutine = null;
        }

        private void HideFeedback()
        {
            if (feedbackText != null)
            {
                feedbackText.gameObject.SetActive(false);
            }
        }

        private void EnsureMissionText()
        {
            if (missionText != null)
            {
                return;
            }

            GameObject textObject = new GameObject("MissionText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(transform, false);
            textObject.transform.SetAsFirstSibling();
            RectTransform rect = (RectTransform)textObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0.315f);
            rect.anchorMax = new Vector2(0.5f, 0.315f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(700f, 38f);

            missionText = textObject.GetComponent<TMP_Text>();
            missionText.text = "";
            missionText.fontSize = 22f;
            missionText.fontSizeMax = 22f;
            missionText.fontSizeMin = 12f;
            missionText.enableAutoSizing = true;
            missionText.fontStyle = FontStyles.Bold;
            missionText.alignment = TextAlignmentOptions.Center;
            missionText.raycastTarget = false;
            missionText.color = new Color(0.92f, 0.96f, 1f, 1f);
            EnsureTextShadow(missionText, new Color(0f, 0.04f, 0.10f, 0.72f), new Vector2(0f, -2f));
        }

        private void HideMissionText()
        {
            if (missionText != null)
            {
                missionText.gameObject.SetActive(false);
            }
        }

        private void StyleGameplayHudText()
        {
            StyleHudText(scoreText, new Color(0.96f, 1f, 1f, 1f), 66f, 32f, new Color(0f, 0.07f, 0.15f, 0.9f), new Vector2(0f, -3f));
            StyleHudText(modeText, new Color(0.62f, 0.94f, 1f, 0.74f), 24f, 13f, new Color(0f, 0.04f, 0.10f, 0.62f), new Vector2(0f, -1.5f));
            StyleHudText(highScoreText, new Color(0.74f, 0.94f, 1f, 0.70f), 22f, 12f, new Color(0f, 0.03f, 0.08f, 0.56f), new Vector2(0f, -1.5f));
            StyleHudText(timerText, new Color(0.94f, 1f, 1f, 1f), 44f, 20f, new Color(0f, 0.04f, 0.10f, 0.82f), new Vector2(0f, -2f));
            StyleHudText(chainText, new Color(0.72f, 0.96f, 1f, 0.78f), 28f, 14f, new Color(0f, 0.04f, 0.10f, 0.72f), new Vector2(0f, -1.5f));
            StyleHudText(feedbackText, new Color(0.86f, 1f, 1f, 1f), 32f, 16f, new Color(0f, 0.04f, 0.10f, 0.80f), new Vector2(0f, -2f));
            if (missionText != null)
            {
                EnsureTextShadow(missionText, new Color(0f, 0.04f, 0.10f, 0.72f), new Vector2(0f, -2f));
            }

            ApplyScoreTextAlignment();
            ConfigureReferenceAuxiliaryHud();
        }

        private void ConfigureReferenceAuxiliaryHud()
        {
            if (modeText != null)
            {
                modeText.gameObject.SetActive(false);
            }

            RectTransform timerRect = timerText == null ? null : timerText.transform as RectTransform;
            if (timerRect != null)
            {
                timerRect.anchorMin = new Vector2(0.5f, 0.945f);
                timerRect.anchorMax = timerRect.anchorMin;
                timerRect.pivot = new Vector2(0.5f, 0.5f);
                timerRect.anchoredPosition = Vector2.zero;
                timerRect.sizeDelta = new Vector2(170f, 64f);
            }

            RectTransform chainRect = chainText == null ? null : chainText.transform as RectTransform;
            if (chainRect != null)
            {
                chainRect.anchorMin = new Vector2(0.5f, 0.786f);
                chainRect.anchorMax = chainRect.anchorMin;
                chainRect.pivot = new Vector2(0.5f, 0.5f);
                chainRect.anchoredPosition = Vector2.zero;
                chainRect.sizeDelta = new Vector2(420f, 38f);
            }

            if (chromaBars == null)
            {
                return;
            }

            float[] barAnchors = { 0.185f, 0.395f, 0.605f, 0.815f };
            for (int i = 0; i < chromaBars.Length && i < barAnchors.Length; i++)
            {
                RectTransform barRect = chromaBars[i] == null ? null : chromaBars[i].transform as RectTransform;
                if (barRect == null)
                {
                    continue;
                }

                barRect.anchorMin = new Vector2(barAnchors[i], 0.778f);
                barRect.anchorMax = barRect.anchorMin;
                barRect.pivot = new Vector2(0.5f, 0.5f);
                barRect.anchoredPosition = new Vector2(0f, -18f);
                barRect.sizeDelta = new Vector2(185f, 36f);
            }
        }

        private void StylePremiumScoreText()
        {
            if (scoreText == null)
            {
                return;
            }

            scoreText.color = Color.white;
            scoreText.fontStyle |= FontStyles.Bold;
            scoreText.enableAutoSizing = true;
            scoreText.fontSize = 178f;
            scoreText.fontSizeMax = 178f;
            scoreText.fontSizeMin = 108f;
            scoreText.characterSpacing = -4f;
            scoreText.alignment = TextAlignmentOptions.Center;

            TMP_FontAsset scoreFont = Resources.Load<TMP_FontAsset>(ScoreFontPath);
            if (scoreFont != null)
            {
                scoreText.font = scoreFont;
            }
            else
            {
                Debug.LogWarning(
                    $"Missing TMP font asset at Resources path: {ScoreFontPath}. Run Window > TextMeshPro > "
                    + "Font Asset Creator on Fredoka-SemiBold.ttf and save the result there. Falling back to the current font.");
            }

            // Bubbly reference look: white glyphs cooling to pale ice blue at the
            // bottom, with a thin light-blue rim instead of a hard dark outline.
            scoreText.enableVertexGradient = true;
            scoreText.colorGradient = new VertexGradient(
                Color.white,
                Color.white,
                new Color(0.80f, 0.91f, 1f),
                new Color(0.80f, 0.91f, 1f));
            scoreText.outlineWidth = 0.05f;
            scoreText.outlineColor = new Color32(140, 190, 235, 210);

            Shadow legacyShadow = scoreText.GetComponent<Shadow>();
            if (legacyShadow != null)
            {
                legacyShadow.enabled = false;
            }

            if (scoreShadowText != null)
            {
                scoreShadowText.font = scoreText.font;
                scoreShadowText.fontStyle = FontStyles.Bold;
                scoreShadowText.enableAutoSizing = true;
                scoreShadowText.fontSize = scoreText.fontSize;
                scoreShadowText.fontSizeMax = scoreText.fontSizeMax;
                scoreShadowText.fontSizeMin = scoreText.fontSizeMin;
                scoreShadowText.characterSpacing = scoreText.characterSpacing;
                scoreShadowText.alignment = TextAlignmentOptions.Center;
                scoreShadowText.enableVertexGradient = false;
                scoreShadowText.outlineWidth = 0f;
                scoreShadowText.color = new Color(0f, 0.025f, 0.08f, 0.48f);
                scoreShadowText.text = scoreText.text;
                scoreShadowText.enabled = true;
                scoreShadowText.gameObject.SetActive(true);
            }

            Outline outline = scoreText.GetComponent<Outline>();
            if (outline == null)
            {
                outline = scoreText.gameObject.AddComponent<Outline>();
            }

            outline.enabled = false;
        }

        private void ApplyScoreTextAlignment()
        {
            if (scoreText != null)
            {
                scoreText.alignment = TextAlignmentOptions.Center;
            }

            if (highScoreText != null)
            {
                highScoreText.alignment = TextAlignmentOptions.Center;
            }

            if (chainText != null)
            {
                chainText.alignment = TextAlignmentOptions.Center;
            }
        }

        private void BuildStatusRow(
            string rowName,
            string label,
            string iconKey,
            string status,
            UnityEngine.Events.UnityAction action)
        {
            bool candy = IsCandySettingsActive();
            bool garden = IsGardenSettingsActive();
            bool desert = IsDesertSettingsActive();
            bool blossom = IsBlossomSettingsActive();
            bool beach = IsBeachSettingsActive();
            RectTransform row = BuildRowBase(rowName, label, iconKey);
            RectTransform statusRect = GetOrCreateChildRect(row, "Status");
            ConfigureTransparentRowButton(statusRect, action);

            RectTransform textRect = GetOrCreateChildRect(statusRect, "Text");
            // Keep the runtime version between the baked About label and its
            // right-hand arrow.  This avoids covering either part of the art.
            textRect.anchorMin = candy ? new Vector2(0.68f, 0.12f) : garden ? new Vector2(0.70f, 0.12f) : new Vector2(0.66f, 0.12f);
            textRect.anchorMax = candy ? new Vector2(0.85f, 0.88f) : garden ? new Vector2(0.84f, 0.88f) : new Vector2(0.82f, 0.88f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            textRect.localScale = Vector3.one;

            TMP_Text text = GetOrAddText(textRect.gameObject);
            text.text = (candy || garden || desert || beach) && rowName == "AboutRow" ? Application.version : status;
            text.font = Resources.Load<TMP_FontAsset>(ScoreFontPath);
            text.alignment = TextAlignmentOptions.MidlineRight;
            text.enableAutoSizing = true;
            text.fontSizeMax = candy ? 20f : garden ? 17f : 16f;
            text.fontSizeMin = candy ? 12f : garden ? 10f : 10f;
            text.fontStyle = FontStyles.Bold;
            text.color = candy
                ? new Color(0.43f, 0.04f, 0.20f, 0.90f)
                : garden ? new Color(0.035f, 0.23f, 0.09f, 0.88f)
                    : blossom ? new Color(0.48f, 0.07f, 0.25f, 0.92f)
                    : beach ? new Color(0.34f, 0.17f, 0.06f, 0.90f)
                    : desert ? new Color(0.55f, 0.25f, 0.07f, 0.92f)
                        : new Color(0.16f, 0.10f, 0.07f, 0.78f);
            text.raycastTarget = false;
            text.enabled = !string.IsNullOrEmpty(text.text);
            ConfigureCandyRowChevron(row);
        }

        private void HandleSettingsPrivacy()
        {
            Debug.Log("[Settings] Privacy Policy selected. No production URL is configured yet.");
        }

        private void HandleSettingsTerms()
        {
            Debug.Log("[Settings] Terms & Conditions selected. No production URL is configured yet.");
        }

        private void HandleSettingsAbout()
        {
            Debug.Log($"[Settings] Chroma Blast version {Application.version}");
        }

        private static void ConfigureAspectCover(Image image, Sprite sprite)
        {
            if (image == null || sprite == null)
            {
                return;
            }

            AspectRatioFitter fitter = image.GetComponent<AspectRatioFitter>();
            if (fitter == null)
            {
                fitter = image.gameObject.AddComponent<AspectRatioFitter>();
            }

            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
        }

        private void StylePremiumBlitzTimer()
        {
            if (timerText == null)
            {
                return;
            }

            TMP_FontAsset scoreFont = Resources.Load<TMP_FontAsset>(ScoreFontPath);
            if (scoreFont != null)
            {
                timerText.font = scoreFont;
            }

            timerText.fontStyle |= FontStyles.Bold;
            timerText.enableAutoSizing = true;
            timerText.fontSize = 78f;
            timerText.fontSizeMax = 78f;
            timerText.fontSizeMin = 48f;
            timerText.characterSpacing = -1f;
            timerText.alignment = TextAlignmentOptions.Center;
            timerText.color = Color.white;
            timerText.enableVertexGradient = true;
            timerText.outlineWidth = 0.055f;
            timerText.outlineColor = new Color32(70, 145, 218, 230);
            timerText.raycastTarget = false;
            EnsureTextShadow(timerText, new Color(0f, 0.025f, 0.08f, 0.52f), new Vector2(4f, -6f));

            RectTransform timerRect = timerText.transform as RectTransform;
            if (timerRect != null)
            {
                timerRect.anchorMin = new Vector2(0.5f, 0.93f);
                timerRect.anchorMax = timerRect.anchorMin;
                timerRect.pivot = new Vector2(0.5f, 0.5f);
                timerRect.anchoredPosition = Vector2.zero;
                timerRect.sizeDelta = new Vector2(360f, 110f);
                timerRect.localScale = Vector3.one;

                RectTransform parentRect = timerRect.parent as RectTransform;
                if (parentRect != null)
                {
                    RectTransform capsuleRect = GetOrCreateChildRect(parentRect, "BlitzTimerCapsule");
                    capsuleRect.anchorMin = timerRect.anchorMin;
                    capsuleRect.anchorMax = timerRect.anchorMax;
                    capsuleRect.pivot = timerRect.pivot;
                    capsuleRect.anchoredPosition = timerRect.anchoredPosition;
                    capsuleRect.sizeDelta = new Vector2(287f, 190f);
                    capsuleRect.localScale = Vector3.one;

                    blitzTimerCapsuleImage = GetOrAddImage(capsuleRect.gameObject);
                    Sprite capsuleSprite = LoadOceanSprite(BestScoreCapsulePath);
                    blitzTimerCapsuleImage.sprite = capsuleSprite;
                    blitzTimerCapsuleImage.color = Color.white;
                    blitzTimerCapsuleImage.type = Image.Type.Simple;
                    blitzTimerCapsuleImage.preserveAspect = true;
                    blitzTimerCapsuleImage.raycastTarget = false;
                    blitzTimerCapsuleImage.enabled = capsuleSprite != null;
                    blitzTimerCapsuleImage.material = null;
                    ThemeAssetSet activeTheme = ThemeCatalog.Current;
                    if (activeTheme != null && activeTheme.CapsuleSprite != null)
                    {
                        blitzTimerCapsuleImage.sprite = activeTheme.CapsuleSprite;
                    }
                    EnsureGraphicShadow(
                        capsuleRect.gameObject,
                        new Color(0f, 0.025f, 0.10f, 0.46f),
                        new Vector2(0f, -6f));

                    int timerSiblingIndex = timerRect.GetSiblingIndex();
                    capsuleRect.SetSiblingIndex(timerSiblingIndex);
                    timerRect.SetSiblingIndex(timerSiblingIndex + 1);
                }
            }

            ApplyBlitzTimerPalette(int.MaxValue);
        }

        private void ApplyBlitzTimerPalette(int seconds)
        {
            if (timerText == null)
            {
                return;
            }

            timerText.color = Color.white;
            timerText.enableVertexGradient = true;
            if (seconds <= 10)
            {
                timerText.colorGradient = new VertexGradient(
                    new Color32(255, 244, 244, 255),
                    new Color32(255, 244, 244, 255),
                    new Color32(255, 45, 70, 255),
                    new Color32(255, 45, 70, 255));
                timerText.outlineColor = new Color32(126, 8, 28, 242);
                return;
            }

            if (seconds <= 20)
            {
                timerText.colorGradient = new VertexGradient(
                    new Color32(255, 255, 244, 255),
                    new Color32(255, 255, 244, 255),
                    new Color32(255, 187, 48, 255),
                    new Color32(255, 187, 48, 255));
                timerText.outlineColor = new Color32(150, 78, 5, 235);
                return;
            }

            timerText.colorGradient = new VertexGradient(
                new Color32(255, 255, 255, 255),
                new Color32(255, 255, 255, 255),
                new Color32(214, 241, 255, 255),
                new Color32(214, 241, 255, 255));
            timerText.outlineColor = new Color32(70, 145, 218, 230);
        }

        private void SetBlitzTimerUrgency(bool urgent)
        {
            if (timerText == null)
            {
                return;
            }

            if (urgent)
            {
                if (blitzUrgencyRoutine != null)
                {
                    return;
                }

                timerText.transform.DOKill();
                timerText.transform.localScale = Vector3.one;
                blitzUrgencyRoutine = StartCoroutine(BlitzUrgencyPulseRoutine());
                return;
            }

            if (blitzUrgencyRoutine != null)
            {
                StopCoroutine(blitzUrgencyRoutine);
                blitzUrgencyRoutine = null;
            }

            timerText.transform.localScale = Vector3.one;
        }

        private IEnumerator BlitzUrgencyPulseRoutine()
        {
            const float cycleDuration = 0.68f;
            const float pulseScale = 0.10f;
            float elapsed = 0f;

            while (timerText != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float phase = Mathf.Repeat(elapsed, cycleDuration) / cycleDuration;
                float strength = 0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f);
                timerText.transform.localScale = Vector3.one * (1f + pulseScale * strength);
                yield return null;
            }

            blitzUrgencyRoutine = null;
        }

        private void StyleGameplayButtons()
        {
            if (undoButton != null)
            {
                undoButton.gameObject.SetActive(false);
            }

            if (muteButton != null)
            {
                muteButton.gameObject.SetActive(false);
            }

            if (rewardedChromaButton != null)
            {
                rewardedChromaButton.gameObject.SetActive(false);
            }

            ConfigureSettingsGearButton();
        }

        private void ConfigureSettingsGearButton()
        {
            if (menuButton == null)
            {
                return;
            }

            menuButton.gameObject.SetActive(true);
            menuButton.transition = Selectable.Transition.None;
            RectTransform rect = menuButton.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = rect.anchorMin;
                rect.pivot = new Vector2(0.5f, 0.5f);
                // BestScoreHud uses top-left pivot at Y=-50 with height 132, so its
                // vertical centre is -50 - 66 = -116. Match that exact screen-space
                // centre with this top-anchored, centre-pivot Settings button.
                rect.anchoredPosition = new Vector2(-78f, -116f);
                rect.sizeDelta = new Vector2(100f, 100f);
                rect.localScale = Vector3.one;
            }

            Image image = menuButton.image != null ? menuButton.image : menuButton.GetComponent<Image>();
            if (image != null)
            {
                UISpriteFactory.ApplyRounded(image, 0.50f);
                image.enabled = true;
                image.color = Color.clear;
                image.raycastTarget = true;
            }

            for (int i = 0; i < menuButton.transform.childCount; i++)
            {
                menuButton.transform.GetChild(i).gameObject.SetActive(false);
            }

            RectTransform surfaceRect = GetOrCreateChildRect(menuButton.transform, "SettingsSurface");
            ConfigureHudLayerRect(surfaceRect, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            Image surface = GetOrAddImage(surfaceRect.gameObject);
            StyleRoundedHudImage(surface, new Color(0.01f, 0.075f, 0.16f, 0.76f));
            surface.enabled = false;
            surface.raycastTarget = false;

            RectTransform glowRect = GetOrCreateChildRect(menuButton.transform, "SettingsGlow");
            ConfigureHudLayerRect(glowRect, Vector2.zero, Vector2.one, new Vector2(-8f, -8f), new Vector2(8f, 8f));
            Image glow = GetOrAddImage(glowRect.gameObject);
            UISpriteFactory.ApplyFrame(glow, 0.50f, 0.055f);
            glow.color = new Color(0.20f, 0.88f, 1f, 0.56f);
            glow.fillCenter = false;
            glow.raycastTarget = false;
            glow.enabled = false;

            RectTransform glossRect = GetOrCreateChildRect(menuButton.transform, "SettingsGloss");
            ConfigureHudLayerRect(glossRect, new Vector2(0.12f, 0.56f), new Vector2(0.88f, 0.90f), Vector2.zero, Vector2.zero);
            Image gloss = GetOrAddImage(glossRect.gameObject);
            StyleRoundedHudImage(gloss, new Color(0.76f, 1f, 1f, 0.07f));
            gloss.enabled = false;
            gloss.raycastTarget = false;

            RectTransform gearRect = GetOrCreateChildRect(menuButton.transform, "GearIcon");
            gearRect.anchorMin = new Vector2(0.5f, 0.5f);
            gearRect.anchorMax = gearRect.anchorMin;
            gearRect.pivot = new Vector2(0.5f, 0.5f);
            gearRect.anchoredPosition = Vector2.zero;
            // SettingsIcon.png is a portrait 1024x1536 canvas around a square
            // gear. Matching the canvas to a square rect made the actual gear
            // tiny. Preserve the source aspect in a compensated portrait rect so
            // the visible gear fills the existing 100x100 button hit area.
            gearRect.sizeDelta = new Vector2(140f, 210f);
            gearRect.localScale = Vector3.one;
            Image gear = GetOrAddImage(gearRect.gameObject);
            if (gear != null)
            {
                // SettingsIcon.png is full-color art, not a silhouette -- leave it
                // white so its own baked-in blue rim/glow isn't tinted further.
                Sprite settingsSprite = LoadOceanSprite(SettingsGearPath);
                gear.sprite = settingsSprite;
                gear.enabled = settingsSprite != null;
                gear.color = Color.white;
                gear.type = Image.Type.Simple;
                gear.preserveAspect = true;
                gear.raycastTarget = false;
            }

            glowRect.SetAsFirstSibling();
            surfaceRect.SetSiblingIndex(1);
            glossRect.SetSiblingIndex(2);
            gearRect.SetAsLastSibling();

            Shadow settingsShadow = menuButton.GetComponent<Shadow>();
            if (settingsShadow != null)
            {
                settingsShadow.enabled = false;
            }
            UIButtonFeedback feedback = menuButton.GetComponent<UIButtonFeedback>();
            if (feedback == null)
            {
                feedback = menuButton.gameObject.AddComponent<UIButtonFeedback>();
            }

            // Keep the existing press-scale feedback, but do not pulse a backing
            // layer: the Settings button must display only the supplied gear art.
            feedback.Configure(0.95f, 0.06f, null);
        }

        private void StyleOceanControlButton(Button button)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.image != null ? button.image : button.GetComponent<Image>();
            Color baseColor = new Color(0.02f, 0.22f, 0.38f, 0.94f);
            Color highlightedColor = new Color(0.04f, 0.44f, 0.64f, 0.98f);
            Color pressedColor = new Color(0.02f, 0.66f, 0.88f, 1f);
            if (image != null)
            {
                UISpriteFactory.ApplyRounded(image, 0.44f);
                image.color = baseColor;
                image.raycastTarget = true;
            }

            ColorBlock colors = button.colors;
            colors.normalColor = baseColor;
            colors.highlightedColor = highlightedColor;
            colors.pressedColor = pressedColor;
            colors.selectedColor = highlightedColor;
            colors.disabledColor = new Color(0.02f, 0.10f, 0.16f, 0.58f);
            colors.colorMultiplier = 1f;
            button.colors = colors;

            StyleOceanButtonText(button);
            EnsureButtonShadow(button);
            EnsureButtonGloss(button);

            if (button.GetComponent<UIButtonFeedback>() == null)
            {
                button.gameObject.AddComponent<UIButtonFeedback>();
            }
        }

        private void StyleOceanButtonText(Button button)
        {
            TMP_Text label = button == null ? null : button.GetComponentInChildren<TMP_Text>(true);
            if (label == null)
            {
                return;
            }

            label.color = new Color(0.95f, 1f, 1f, 1f);
            label.fontStyle |= FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMax = 20f;
            label.fontSizeMin = 10f;
            label.raycastTarget = false;
            EnsureTextShadow(label, new Color(0f, 0.04f, 0.10f, 0.72f), new Vector2(0f, -1.5f));
        }

        private void EnsureButtonShadow(Button button)
        {
            if (button == null)
            {
                return;
            }

            Shadow shadow = null;
            Shadow[] shadows = button.GetComponents<Shadow>();
            for (int i = 0; i < shadows.Length; i++)
            {
                if (shadows[i] != null && shadows[i].GetType() == typeof(Shadow))
                {
                    shadow = shadows[i];
                    break;
                }
            }

            if (shadow == null)
            {
                shadow = button.gameObject.AddComponent<Shadow>();
            }

            shadow.effectColor = new Color(0f, 0.025f, 0.08f, 0.60f);
            shadow.effectDistance = new Vector2(0f, -3f);
            shadow.useGraphicAlpha = true;

            Outline outline = button.GetComponent<Outline>();
            if (outline == null)
            {
                outline = button.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = new Color(0.42f, 0.94f, 1f, 0.24f);
            outline.effectDistance = new Vector2(1.4f, -1.4f);
            outline.useGraphicAlpha = true;
        }

        private void EnsureButtonGloss(Button button)
        {
            RectTransform parent = button == null ? null : button.transform as RectTransform;
            if (parent == null)
            {
                return;
            }

            Transform existing = parent.Find("OceanButtonGloss");
            Image gloss = existing == null ? null : existing.GetComponent<Image>();
            if (gloss == null)
            {
                GameObject glossObject = new GameObject("OceanButtonGloss", typeof(RectTransform), typeof(Image));
                glossObject.transform.SetParent(parent, false);
                gloss = glossObject.GetComponent<Image>();
                UISkin.IgnoreLayout(glossObject);
            }

            gloss.gameObject.SetActive(true);
            gloss.raycastTarget = false;
            gloss.color = new Color(1f, 1f, 1f, 0.12f);
            UISpriteFactory.ApplyRounded(gloss, 0.50f);

            RectTransform rect = gloss.rectTransform;
            rect.anchorMin = new Vector2(0.10f, 0.58f);
            rect.anchorMax = new Vector2(0.90f, 0.92f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.SetAsFirstSibling();
        }

        private void StyleHudText(TMP_Text text, Color color, float maxSize, float minSize, Color shadowColor, Vector2 shadowDistance)
        {
            if (text == null)
            {
                return;
            }

            text.color = color;
            text.fontStyle |= FontStyles.Bold;
            text.enableAutoSizing = true;
            text.fontSizeMax = maxSize;
            text.fontSizeMin = minSize;
            text.fontSize = Mathf.Clamp(text.fontSize, minSize, maxSize);
            text.raycastTarget = false;
            EnsureTextShadow(text, shadowColor, shadowDistance);
        }

        private void EnsureTextShadow(TMP_Text text, Color color, Vector2 distance)
        {
            if (text == null)
            {
                return;
            }

            Shadow shadow = text.GetComponent<Shadow>();
            if (shadow == null)
            {
                shadow = text.gameObject.AddComponent<Shadow>();
            }

            shadow.effectColor = color;
            shadow.effectDistance = distance;
            shadow.useGraphicAlpha = true;
        }

        private void SetScoreText(int score)
        {
            if (scoreText == null)
            {
                return;
            }

            bool scoreChanged = score != targetScore;
            if (scoreChanged && scoreText.gameObject.activeInHierarchy)
            {
                if (suppressNextScoreAutoPunch)
                {
                    suppressNextScoreAutoPunch = false;
                }
                else
                {
                    scoreText.transform.DOKill();
                    scoreText.transform.localScale = Vector3.one;
                    scoreText.transform.DOPunchScale(Vector3.one * 0.035f, 0.16f, 4, 0.58f);
                }
            }
            else if (suppressNextScoreAutoPunch)
            {
                suppressNextScoreAutoPunch = false;
            }

            if (score <= displayedScore)
            {
                displayedScore = score;
                targetScore = score;
                SetDisplayedScoreText(displayedScore);
                UpdateDisplayedBestScore(displayedScore);
                return;
            }

            targetScore = score;
            if (scoreCountRoutine == null)
            {
                scoreCountRoutine = StartCoroutine(AnimateScoreCount());
            }
        }

        private IEnumerator AnimateScoreCount()
        {
            while (displayedScore != targetScore)
            {
                int animationStart = displayedScore;
                int animationTarget = targetScore;
                float elapsed = 0f;
                float duration = Mathf.Clamp(0.15f + Mathf.Abs(animationTarget - animationStart) / 2200f, 0.15f, 0.34f);
                while (elapsed < duration && displayedScore != animationTarget)
                {
                    if (animationTarget != targetScore)
                    {
                        animationStart = displayedScore;
                        animationTarget = targetScore;
                        elapsed = 0f;
                        duration = Mathf.Clamp(0.15f + Mathf.Abs(animationTarget - animationStart) / 2200f, 0.15f, 0.34f);
                    }

                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float eased = 1f - (1f - t) * (1f - t) * (1f - t);
                    displayedScore = Mathf.RoundToInt(Mathf.Lerp(animationStart, animationTarget, eased));
                    SetDisplayedScoreText(displayedScore);
                    UpdateDisplayedBestScore(displayedScore);

                    yield return null;
                }

                displayedScore = animationTarget;
                SetDisplayedScoreText(displayedScore);
                UpdateDisplayedBestScore(displayedScore);
            }

            scoreCountRoutine = null;
        }

        private void SetDisplayedScoreText(int value)
        {
            string valueText = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (scoreText != null)
            {
                scoreText.text = valueText;
            }

            if (scoreShadowText != null)
            {
                scoreShadowText.text = valueText;
            }
        }

        private void SetBestScoreTarget(int bestScore)
        {
            targetBestScore = Mathf.Max(0, bestScore);
            if (!bestScoreInitialized)
            {
                displayedBestScore = targetBestScore;
                bestScoreInitialized = true;
                WriteDisplayedBestScore();
            }
        }

        private void UpdateDisplayedBestScore(int currentDisplayedScore)
        {
            if (!bestScoreInitialized || highScoreText == null)
            {
                return;
            }

            int nextBest = Mathf.Max(displayedBestScore, Mathf.Min(currentDisplayedScore, targetBestScore));
            if (nextBest == displayedBestScore)
            {
                return;
            }

            displayedBestScore = nextBest;
            WriteDisplayedBestScore();
        }

        private void WriteDisplayedBestScore()
        {
            if (highScoreText == null)
            {
                return;
            }

            highScoreText.gameObject.SetActive(true);
            highScoreText.enabled = true;
            highScoreText.color = Color.white;
            highScoreText.text = displayedBestScore.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private string ModeName(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.Blitz:
                    return "Blitz 90s";
                case GameMode.Daily:
                    return "Provocarea Zilei";
                default:
                    return "Classic";
            }
        }

        private void RefreshMuteLabel()
        {
            if (muteButtonText == null && muteButton != null)
            {
                muteButtonText = muteButton.GetComponentInChildren<TMP_Text>();
            }

            if (muteButtonText != null)
            {
                bool muted = AudioManager.Instance != null && AudioManager.Instance.Muted;
                muteButtonText.text = muted ? "MUT" : "SUNET";
                StyleOceanButtonText(muteButton);
            }
        }
    }

}
