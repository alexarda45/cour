using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChromaBlast
{
    public class GameHUD : MonoBehaviour
    {
        private const string OceanBackgroundPath = "Ocean/Backgrounds/BG_Ocean";
        private const string OceanScorePanelPath = "Ocean/UI/UI_ScorePanel_Ocean";
        private const string SettingsPanelPath = "Ocean/Settings/Settings_Panel";
        private const string SettingsClosePath = "Ocean/Settings/Close_X";
        private const string SettingsRestartIconPath = "Ocean/Icons/Icon_Restart";
        private const float SettingsPanelWidth = 690f;
        private const float SettingsPanelHeight = 920f;
        private const float SettingsRowsWidth = 580f;
        private const float SettingsRowHeight = 96f;
        private const float SettingsRowsSpacing = 16f;
        private const float SettingsRowsHeight = SettingsRowHeight * 5f + SettingsRowsSpacing * 4f;
        private const float SettingsControlWidth = 190f;
        private const float SettingsControlHeight = 56f;
        private const float OceanBoardFramePadding = 34f;
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

        public RectTransform DragLayer => dragLayer;

        private GameManager gameManager;
        private Coroutine feedbackRoutine;
        private Coroutine scoreCountRoutine;
        private readonly bool[] announcedPopReady = new bool[GameConstants.ColorCount];
        private int displayedScore;
        private int targetScore;
        private int lastDisplayedBlitzSecond = -1;
        private Image oceanBackgroundImage;
        private Image oceanScorePanelImage;
        private Image oceanBoardFrameImage;
        private RectTransform oceanBoardFrameRoot;
        private Image pauseOverlayImage;
        private RectTransform pausePanelRoot;
        private Image pausePanelImage;
        private TMP_Text pauseTitleText;
        private Button pauseLanguageButton;
        private GameObject languagePopupRoot;
        private TMP_Text languageValueOverlay;
        private RectTransform settingsRowsContainer;
        private Image soundToggleTrackImage;
        private Image soundToggleKnobImage;
        private TMP_Text soundToggleStateText;
        private Image vibrationToggleTrackImage;
        private Image vibrationToggleKnobImage;
        private TMP_Text vibrationToggleStateText;
        private TMP_Text languageButtonText;
        private Sprite soundIconSprite;
        private Sprite vibrationIconSprite;
        private Sprite languageIconSprite;
        private Sprite restartIconSprite;
        private Sprite mainMenuIconSprite;

        public void Initialize(GameManager owner)
        {
            gameManager = owner;

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
            EnsureOceanBranding();
            EnsureOceanScorePanel();
            EnsureOceanBoardFrame();
            StyleGameplayHudText();
            StyleGameplayButtons();
            EnsureOceanPauseMenu();
            HideFeedback();
            ShowPause(false);
        }

        private void OnDestroy()
        {
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

            if (highScoreText != null)
            {
                highScoreText.text = $"Record {highScore:N0}";
            }

            if (chainText != null)
            {
                bool chainActive = scoreManager.Chain > 0;
                chainText.gameObject.SetActive(chainActive);
                if (chainActive)
                {
                    chainText.text = $"CHAIN x{scoreManager.Chain}";
                }
            }

            if (timerText != null)
            {
                timerText.gameObject.SetActive(mode == GameMode.Blitz);
                if (mode == GameMode.Blitz)
                {
                    int seconds = Mathf.CeilToInt(blitzSeconds);
                    timerText.text = seconds.ToString();
                    timerText.color = seconds <= 10
                        ? new Color(1f, 0.18f, 0.28f, 1f)
                        : seconds <= 20
                        ? new Color(1f, 0.82f, 0.35f, 1f)
                        : new Color(0.92f, 0.96f, 1f, 1f);

                    if (seconds <= 10 && seconds != lastDisplayedBlitzSecond)
                    {
                        timerText.transform.DOKill();
                        timerText.transform.DOPunchScale(Vector3.one * 0.16f, 0.16f, 6, 0.7f);
                    }

                    lastDisplayedBlitzSecond = seconds;
                }
                else
                {
                    lastDisplayedBlitzSecond = -1;
                }
            }

            if (undoButton != null)
            {
                undoButton.gameObject.SetActive(mode == GameMode.Classic);
                undoButton.interactable = undoAvailable;
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

        public void PunchScore(bool pure)
        {
            if (scoreText != null)
            {
                scoreText.transform.DOKill();
                scoreText.transform.DOPunchScale(Vector3.one * (pure ? 0.22f : 0.14f), 0.24f, 8, 0.8f);
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
            popupLayer?.ShowClear(result, chain, scoreAdded, styleBonus);
            if (result != null && result.linesCleared > 0)
            {
                Color color = result.pureLines > 0 ? new Color(1f, 0.82f, 0.35f, 1f) : new Color(0.1f, 0.9f, 1f, 1f);
                float alpha = Mathf.Clamp(0.12f + result.linesCleared * 0.04f + result.pureLines * 0.05f + styleBonus * 0.00016f, 0.12f, 0.34f);
                Flash(color, alpha, 0.26f);
                if (chainText != null && chain >= 2)
                {
                    chainText.transform.DOKill();
                    chainText.transform.DOPunchScale(Vector3.one * Mathf.Clamp(0.08f + chain * 0.02f, 0.10f, 0.20f), 0.22f, 8, 0.75f);
                }
            }
        }

        public void ShowPopFeedback(ChromaColor color, int popped, int scoreAdded)
        {
            popupLayer?.ShowPop(color, popped, scoreAdded);
            Flash(ChromaPalette.GetColor(color), popped >= 8 ? 0.30f : 0.22f, 0.28f);
        }

        public void ShowPopReady(ChromaColor color, int targetCount)
        {
            Color popColor = ChromaPalette.GetColor(color);
            string colorName = ChromaPalette.GetRomanianName(color).ToUpperInvariant();
            popupLayer?.Show($"POP GATA\n{colorName} x{targetCount}", popColor, 44, new Vector2(0f, 178f));
            ShowFeedback($"APASA POP {colorName}", popColor, 1.05f);
            Flash(popColor, 0.12f, 0.18f);
        }

        public void ShowRewardedChromaReady(ChromaColor color)
        {
            Color popColor = ChromaPalette.GetColor(color);
            string colorName = ChromaPalette.GetRomanianName(color).ToUpperInvariant();
            popupLayer?.Show($"+CHROMA\nPOP {colorName} GATA", popColor, 46, new Vector2(0f, 205f));
            ShowFeedback($"POP {colorName} GATA", popColor, 1.0f);
            Flash(popColor, 0.16f, 0.20f);
        }

        public void ShowPopUnavailable(ChromaColor color)
        {
            Color popColor = ChromaPalette.GetColor(color);
            ShowFeedback("POP FARA CELULE", popColor, 0.58f);
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
            ShowFeedback($"+{rounded}s TIMP", color, 0.42f);
        }

        public void ShowInvalidMove()
        {
            ShowFeedback("NU INCAPE", new Color(1f, 0.18f, 0.28f, 1f), 0.7f);
        }

        public void ShowNoFitPiece()
        {
            ShowFeedback("NU ARE LOC", new Color(1f, 0.62f, 0.18f, 1f), 0.55f);
        }

        public void ShowUsePopToContinue(ChromaColor color)
        {
            Color popColor = ChromaPalette.GetColor(color);
            popupLayer?.Show($"FOLOSESTE POP\n{ChromaPalette.GetRomanianName(color).ToUpperInvariant()}", popColor, 42, new Vector2(0f, 165f));
            ShowFeedback("POP TE SALVEAZA", popColor, 0.9f);
        }

        public void SetMission(string mission, bool completed)
        {
            EnsureMissionText();
            HideMissionText();
        }

        public void ShowMissionComplete(int rewardScore)
        {
            Color color = new Color(0.65f, 1f, 0.26f, 1f);
            popupLayer?.Show($"MISIUNE COMPLETA\n+{rewardScore}", color, 48, new Vector2(0f, 25f));
            ShowFeedback($"MISIUNE +{rewardScore}", color, 0.65f);
            Flash(color, 0.18f, 0.22f);
            if (missionText != null)
            {
                HideMissionText();
            }
        }

        public void ShowAchievement(AchievementReward reward)
        {
            Color color = new Color(1f, 0.82f, 0.35f, 1f);
            popupLayer?.Show($"REALIZARE\n{reward.title}\n+{reward.coins} MONEDE", color, 44, new Vector2(0f, 165f));
            ShowFeedback($"{reward.title} +{reward.coins} MONEDE", color, 0.8f);
            Flash(color, 0.22f, 0.26f);
        }

        public void ShowScoreMilestone(int milestoneScore, int coins)
        {
            if (coins <= 0)
            {
                return;
            }

            Color color = new Color(1f, 0.82f, 0.35f, 1f);
            popupLayer?.Show($"SCOR {milestoneScore:N0}\n+{coins} MONEDE", color, 44, new Vector2(0f, 220f));
            ShowFeedback($"BONUS +{coins} MONEDE", color, 0.72f);
            Flash(color, 0.16f, 0.22f);
        }

        public void ShowDailyQuestReward(string questName, int coins)
        {
            if (coins <= 0)
            {
                return;
            }

            Color color = new Color(1f, 0.82f, 0.35f, 1f);
            string title = string.IsNullOrEmpty(questName) ? "OBIECTIV ZILNIC" : questName;
            popupLayer?.Show($"{title}\n+{coins} MONEDE", color, 44, new Vector2(0f, 235f));
            ShowFeedback($"ZILNIC +{coins} MONEDE", color, 0.78f);
            Flash(color, 0.18f, 0.24f);
        }

        public void ShowSetupBonus(int scoreAdded)
        {
            if (scoreAdded <= 0)
            {
                return;
            }

            Color color = new Color(1f, 0.82f, 0.35f, 1f);
            popupLayer?.Show($"APROAPE\n+{scoreAdded}", color, 40, new Vector2(0f, 95f));
            ShowFeedback($"APROAPE +{scoreAdded}", color, 0.44f);
            Flash(color, 0.08f, 0.14f);
        }

        public void ShowLargePieceBonus(int cells, int scoreAdded)
        {
            if (scoreAdded <= 0)
            {
                return;
            }

            Color color = new Color(0.1f, 0.9f, 1f, 1f);
            popupLayer?.Show($"PIESA MARE\n{cells} CELULE +{scoreAdded}", color, 38, new Vector2(0f, 80f));
            ShowFeedback($"PIESA MARE +{scoreAdded}", color, 0.40f);
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
            string title = boardEmpty ? "TABLA CURATA" : "SPATIU LIBER";
            popupLayer?.Show($"{title}\n+{scoreAdded}", color, boardEmpty ? 58 : 48, new Vector2(0f, 360f));
            ShowFeedback($"{title} +{scoreAdded}", color, 0.72f);
            Flash(color, boardEmpty ? 0.28f : 0.18f, 0.26f);
        }

        public void ShowPlacedFeedback()
        {
            ShowPlacedFeedback(0);
        }

        public void ShowPlacedFeedback(int scoreAdded)
        {
            string message = scoreAdded > 0 ? $"+{scoreAdded}" : "BUN";
            ShowFeedback(message, new Color(0.1f, 0.9f, 1f, 1f), 0.28f);
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
                message = "LINIE!";
            }
            else if (lineCount == 2)
            {
                message = "DUBLU!";
            }
            else
            {
                message = $"COMBO x{lineCount}!";
            }

            ShowFeedback(message, color, 0.36f);
        }

        public void ShowSmartMoveHint()
        {
            ShowFeedback("INCEARCA AICI", new Color(0.65f, 1f, 0.26f, 1f), 0.58f);
        }

        public void ShowTrayCompleteBonus(int scoreAdded)
        {
            if (scoreAdded <= 0)
            {
                return;
            }

            Color color = new Color(0.65f, 1f, 0.26f, 1f);
            popupLayer?.Show($"TAVA COMPLETA\n+{scoreAdded}", color, 44, new Vector2(0f, 65f));
            ShowFeedback($"TAVA +{scoreAdded}", color, 0.48f);
            Flash(color, 0.10f, 0.16f);
        }

        public void ShowPause(bool visible)
        {
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
        }

        private void ToggleSoundFromSettings()
        {
            bool soundOn;
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.ToggleMute();
                soundOn = !AudioManager.Instance.Muted;
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
            SaveManager save = SaveManager.Instance;
            bool vibrationOn;
            if (save != null)
            {
                bool nextMuted = !save.Data.hapticsMuted;
                save.SetHapticsMuted(nextMuted);
                vibrationOn = !nextMuted;
            }
            else
            {
                bool currentlyOn = PlayerPrefs.GetInt("VibrationEnabled", 1) != 0;
                vibrationOn = !currentlyOn;
                PlayerPrefs.SetInt("VibrationEnabled", vibrationOn ? 1 : 0);
                PlayerPrefs.Save();
            }

            if (vibrationOn)
            {
                Haptics.Light();
            }

            PlayerPrefs.SetInt("VibrationEnabled", vibrationOn ? 1 : 0);
            PlayerPrefs.Save();
            Debug.Log($"Vibration toggled: {(vibrationOn ? "ON" : "OFF")}");
            RefreshPauseLabels();
            UpdateSettingsToggleVisuals(false, true);
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

            Sprite backgroundSprite = LoadOceanSprite(OceanBackgroundPath);
            DisableGameplayLogo(hudRect);

            if (backgroundSprite != null)
            {
                oceanBackgroundImage = EnsureImageLayer(oceanBackgroundImage, "OceanBackground", hudRect);
                if (oceanBackgroundImage != null)
                {
                    oceanBackgroundImage.sprite = backgroundSprite;
                    oceanBackgroundImage.color = Color.white;
                    oceanBackgroundImage.type = Image.Type.Simple;
                    oceanBackgroundImage.preserveAspect = false;
                    oceanBackgroundImage.raycastTarget = false;
                    StretchToFullPortrait(oceanBackgroundImage.rectTransform);
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

            oceanBoardFrameRoot = EnsureRectLayer(oceanBoardFrameRoot, "OceanBoardFrame", boardRoot);
            if (oceanBoardFrameRoot == null)
            {
                return;
            }

            oceanBoardFrameImage = oceanBoardFrameRoot.GetComponent<Image>();
            if (oceanBoardFrameImage != null)
            {
                oceanBoardFrameImage.enabled = false;
                oceanBoardFrameImage.raycastTarget = false;
            }

            oceanBoardFrameRoot.anchorMin = Vector2.zero;
            oceanBoardFrameRoot.anchorMax = Vector2.one;
            oceanBoardFrameRoot.pivot = new Vector2(0.5f, 0.5f);
            oceanBoardFrameRoot.offsetMin = Vector2.one * -OceanBoardFramePadding;
            oceanBoardFrameRoot.offsetMax = Vector2.one * OceanBoardFramePadding;
            oceanBoardFrameRoot.SetAsFirstSibling();

            ConfigureSimpleOceanBoardFrame(oceanBoardFrameRoot);
            DisableDuplicateOceanBoardFrames(boardRoot, oceanBoardFrameRoot);
            DisableLegacyBoardFrameDecorators(boardRoot);
        }

        private void DisableGameplayLogo(RectTransform hudRect)
        {
            Transform existing = hudRect.Find("OceanLogo");
            if (existing != null)
            {
                existing.gameObject.SetActive(false);
            }
        }

        private void EnsureOceanScorePanel()
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

            Sprite scorePanelSprite = LoadOceanSprite(OceanScorePanelPath);
            if (scorePanelSprite == null)
            {
                return;
            }

            oceanScorePanelImage = EnsureImageLayer(oceanScorePanelImage, "OceanScorePanel", parentRect);
            if (oceanScorePanelImage == null)
            {
                return;
            }

            oceanScorePanelImage.sprite = scorePanelSprite;
            oceanScorePanelImage.color = Color.white;
            oceanScorePanelImage.type = scorePanelSprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            oceanScorePanelImage.preserveAspect = false;
            oceanScorePanelImage.raycastTarget = false;

            RectTransform panelRect = oceanScorePanelImage.rectTransform;
            panelRect.anchorMin = scoreRect.anchorMin;
            panelRect.anchorMax = scoreRect.anchorMax;
            panelRect.pivot = scoreRect.pivot;
            panelRect.anchoredPosition = scoreRect.anchoredPosition + new Vector2(0f, -7f);

            Vector2 scoreSize = scoreRect.rect.size;
            if (scoreSize.x <= 1f || scoreSize.y <= 1f)
            {
                scoreSize = scoreRect.sizeDelta;
            }

            float panelWidth = Mathf.Clamp(scoreSize.x + 230f, 360f, 560f);
            float panelHeight = Mathf.Clamp(scoreSize.y + 84f, 96f, 142f);
            panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);

            int textIndex = scoreText.transform.GetSiblingIndex();
            if (highScoreText != null && highScoreText.transform.parent == parentRect)
            {
                textIndex = Mathf.Min(textIndex, highScoreText.transform.GetSiblingIndex());
            }

            if (chainText != null && chainText.transform.parent == parentRect)
            {
                textIndex = Mathf.Min(textIndex, chainText.transform.GetSiblingIndex());
            }

            oceanScorePanelImage.transform.SetSiblingIndex(Mathf.Max(0, textIndex));
        }

        private void ConfigureSimpleOceanBoardFrame(RectTransform frameRoot)
        {
            DisableOrnateOceanFrameChildren(frameRoot);

            ConfigureRoundedFrameLayer(
                EnsureFrameLayer(frameRoot, "OceanBoardFrameShadow"),
                new Color(0f, 0.015f, 0.05f, 0.46f),
                new Vector2(-8f, -12f),
                new Vector2(8f, 6f),
                OceanBoardFrameRadius,
                false,
                0f);

            ConfigureRoundedFrameLayer(
                EnsureFrameLayer(frameRoot, "OceanBoardFramePlate"),
                new Color(0.015f, 0.075f, 0.14f, 0.88f),
                Vector2.zero,
                Vector2.zero,
                OceanBoardFrameRadius,
                false,
                0f);

            ConfigureRoundedFrameLayer(
                EnsureFrameLayer(frameRoot, "OceanBoardFrameOuterBorder"),
                new Color(0.02f, 0.72f, 1f, 0.78f),
                Vector2.zero,
                Vector2.zero,
                OceanBoardFrameRadius,
                true,
                0.11f);

            ConfigureRoundedFrameLayer(
                EnsureFrameLayer(frameRoot, "OceanBoardFrameInnerHighlight"),
                new Color(0.70f, 1f, 1f, 0.24f),
                new Vector2(8f, 8f),
                new Vector2(-8f, -8f),
                0.28f,
                true,
                0.045f);

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
                if (child == null || child.name != "OceanBoardFrame")
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
            if (legacyBoardImage != null && legacyBoardImage != oceanBoardFrameImage)
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
            pausePanelRoot.sizeDelta = new Vector2(SettingsPanelWidth, SettingsPanelHeight);
            pausePanelRoot.localScale = Vector3.one;
            pausePanelRoot.pivot = new Vector2(0.5f, 0.5f);

            ConfigureSettingsPanelSprite();
            ConfigureSettingsTitle();
            DisableGeneratedPauseDecor();
            DisableObsoletePausePanelBackplates();
            DisableLegacyPauseRows();
            ConfigureModernCloseButton();
            BuildModernPauseRows();

            EnsureSettingsRaycastPath();
            UpdateSettingsToggleVisuals(false);
            UpdateLanguageRowVisual();
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

            Sprite panelSprite = LoadSettingsSprite(SettingsPanelPath);
            pausePanelImage.enabled = true;
            pausePanelImage.sprite = panelSprite;
            pausePanelImage.color = Color.white;
            pausePanelImage.raycastTarget = true;
            pausePanelImage.type = Image.Type.Simple;
            pausePanelImage.preserveAspect = true;
            DisableSelectableDecor(pausePanelRoot.gameObject);
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

            RectTransform rect = pauseTitleText.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, 320f);
                rect.sizeDelta = new Vector2(520f, 76f);
                rect.localScale = Vector3.one;
                rect.pivot = new Vector2(0.5f, 0.5f);
            }

            pauseTitleText.text = "PAUZA";

            pauseTitleText.alignment = TextAlignmentOptions.Center;
            pauseTitleText.fontSize = 52f;
            pauseTitleText.characterSpacing = 1.5f;
            pauseTitleText.fontStyle = FontStyles.Bold;
            pauseTitleText.color = new Color(0.92f, 1f, 1f, 1f);
            pauseTitleText.raycastTarget = false;
            pauseTitleText.enabled = true;
            pauseTitleText.gameObject.SetActive(true);
            EnsureTextShadow(pauseTitleText, new Color(0f, 0.07f, 0.18f, 0.82f), new Vector2(0f, -2.5f));
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
            dimImage.color = new Color(0f, 0f, 0f, 0.45f);
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
            RectTransform closeRect = GetOrCreateChildRect(pausePanelRoot, "CloseButton");
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(0.5f, 0.5f);
            closeRect.anchoredPosition = new Vector2(-68f, -68f);
            closeRect.sizeDelta = new Vector2(68f, 68f);
            closeRect.localScale = Vector3.one;

            Image image = GetOrAddImage(closeRect.gameObject);
            image.sprite = LoadSettingsSprite(SettingsClosePath);
            image.color = Color.white;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = true;

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
            settingsRowsContainer = GetOrCreateChildRect(pausePanelRoot, "RowsContainer");
            settingsRowsContainer.anchorMin = new Vector2(0.5f, 0.5f);
            settingsRowsContainer.anchorMax = new Vector2(0.5f, 0.5f);
            settingsRowsContainer.pivot = new Vector2(0.5f, 0.5f);
            settingsRowsContainer.anchoredPosition = new Vector2(0f, -62f);
            settingsRowsContainer.sizeDelta = new Vector2(SettingsRowsWidth, SettingsRowsHeight);
            settingsRowsContainer.localScale = Vector3.one;

            VerticalLayoutGroup layout = settingsRowsContainer.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = settingsRowsContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = SettingsRowsSpacing;
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

            BuildToggleRow("SoundRow", "Sound", "sound", true);
            BuildToggleRow("VibrationRow", "Vibration", "vibration", false);
            BuildLanguageRow();
            BuildActionRow("RestartRow", "Restart", "restart", "Restart", true);
            BuildActionRow("MainMenuRow", "Main Menu", "home", "Menu", false);

            SetRowSiblingOrder("SoundRow", 0);
            SetRowSiblingOrder("VibrationRow", 1);
            SetRowSiblingOrder("LanguageRow", 2);
            SetRowSiblingOrder("RestartRow", 3);
            SetRowSiblingOrder("MainMenuRow", 4);
            settingsRowsContainer.gameObject.SetActive(true);
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
            RectTransform row = GetOrCreateChildRect(settingsRowsContainer, rowName);
            row.localScale = Vector3.one;
            row.sizeDelta = new Vector2(SettingsRowsWidth, SettingsRowHeight);
            row.anchorMin = new Vector2(0.5f, 0.5f);
            row.anchorMax = new Vector2(0.5f, 0.5f);
            row.pivot = new Vector2(0.5f, 0.5f);

            Image rowRootImage = row.GetComponent<Image>();
            if (rowRootImage != null)
            {
                rowRootImage.enabled = false;
                rowRootImage.raycastTarget = false;
            }

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

            layoutElement.preferredWidth = SettingsRowsWidth;
            layoutElement.preferredHeight = SettingsRowHeight;
            layoutElement.minWidth = SettingsRowsWidth;
            layoutElement.minHeight = SettingsRowHeight;
            layoutElement.flexibleWidth = 0f;
            layoutElement.flexibleHeight = 0f;

            RectTransform background = GetOrCreateChildRect(row, "RowBackground");
            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.one;
            background.offsetMin = Vector2.zero;
            background.offsetMax = Vector2.zero;
            background.localScale = Vector3.one;
            Image backgroundImage = GetOrAddImage(background.gameObject);
            UISpriteFactory.ApplyRounded(backgroundImage, 0.18f);
            backgroundImage.color = new Color(0.015f, 0.13f, 0.29f, 0.90f);
            backgroundImage.raycastTarget = false;
            background.SetAsFirstSibling();
            EnsureGraphicOutline(background.gameObject, new Color(0.22f, 0.86f, 1f, 0.30f), new Vector2(1.1f, -1.1f));
            EnsureGraphicShadow(background.gameObject, new Color(0f, 0.02f, 0.07f, 0.42f), new Vector2(0f, -1.8f));

            RectTransform highlight = GetOrCreateChildRect(row, "RowHighlight");
            highlight.anchorMin = new Vector2(0.045f, 0.58f);
            highlight.anchorMax = new Vector2(0.955f, 0.90f);
            highlight.offsetMin = Vector2.zero;
            highlight.offsetMax = Vector2.zero;
            highlight.localScale = Vector3.one;
            Image highlightImage = GetOrAddImage(highlight.gameObject);
            UISpriteFactory.ApplyRounded(highlightImage, 0.25f);
            highlightImage.color = new Color(0.58f, 0.95f, 1f, 0.055f);
            highlightImage.raycastTarget = false;
            highlight.SetSiblingIndex(1);

            RectTransform icon = GetOrCreateChildRect(row, "Icon");
            icon.anchorMin = new Vector2(0f, 0.5f);
            icon.anchorMax = new Vector2(0f, 0.5f);
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.anchoredPosition = new Vector2(52f, 0f);
            icon.sizeDelta = new Vector2(58f, 58f);
            icon.localScale = Vector3.one;
            Image iconImage = GetOrAddImage(icon.gameObject);
            iconImage.sprite = GetSettingsIconSprite(iconKey);
            iconImage.color = Color.white;
            iconImage.type = Image.Type.Simple;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            RectTransform labelRect = GetOrCreateChildRect(row, "Label");
            labelRect.anchorMin = new Vector2(0f, 0.5f);
            labelRect.anchorMax = new Vector2(0f, 0.5f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.anchoredPosition = new Vector2(108f, 0f);
            labelRect.sizeDelta = new Vector2(245f, 64f);
            labelRect.localScale = Vector3.one;

            TMP_Text labelText = GetOrAddText(labelRect.gameObject);
            labelText.text = label;
            labelText.alignment = TextAlignmentOptions.MidlineLeft;
            labelText.fontSize = 31f;
            labelText.characterSpacing = 0f;
            labelText.fontStyle = FontStyles.Bold;
            labelText.color = Color.white;
            labelText.raycastTarget = false;
            EnsureTextShadow(labelText, new Color(0f, 0.04f, 0.10f, 0.66f), new Vector2(0f, -1.2f));

            row.gameObject.SetActive(true);
            return row;
        }

        private void BuildToggleRow(string rowName, string label, string iconKey, bool sound)
        {
            RectTransform row = BuildRowBase(rowName, label, iconKey);
            RectTransform toggle = GetOrCreateChildRect(row, "ToggleControl");
            toggle.anchorMin = new Vector2(1f, 0.5f);
            toggle.anchorMax = new Vector2(1f, 0.5f);
            toggle.pivot = new Vector2(1f, 0.5f);
            toggle.anchoredPosition = new Vector2(-24f, 0f);
            toggle.sizeDelta = new Vector2(SettingsControlWidth, SettingsControlHeight);
            toggle.localScale = Vector3.one;

            Image hitImage = GetOrAddImage(toggle.gameObject);
            hitImage.sprite = null;
            hitImage.color = new Color(1f, 1f, 1f, 0f);
            hitImage.raycastTarget = true;

            Button button = GetOrAddButton(toggle.gameObject, hitImage);
            ConfigureButtonNoTransition(button);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(sound ? ToggleSoundFromSettings : ToggleVibrationFromSettings);

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
            stateText.fontSize = 24f;
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

            if (sound)
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
            ConfigureRightActionButton(buttonRect, buttonLabel, restart ? RestartFromPauseSettings : MainMenuFromPauseSettings);
            if (restart)
            {
                pauseRestartButton = buttonRect.GetComponent<Button>();
            }
            else
            {
                pauseMenuButton = buttonRect.GetComponent<Button>();
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
            image.color = new Color(0.02f, 0.52f, 0.90f, 0.97f);
            image.raycastTarget = true;
            EnsureGraphicOutline(buttonRect.gameObject, new Color(0.64f, 1f, 1f, 0.30f), new Vector2(1f, -1f));
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
            text.fontSize = 24f;
            text.enableAutoSizing = true;
            text.fontSizeMax = 24f;
            text.fontSizeMin = 16f;
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

        private Sprite BuildSettingsIconSprite(string iconKey)
        {
            const int size = 96;
            Color32[] pixels = new Color32[size * size];
            Color32 clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clear;
            }

            Color32 circle = new Color32(4, 108, 178, 245);
            Color32 circleDark = new Color32(1, 45, 96, 210);
            Color32 cyan = new Color32(76, 232, 255, 230);
            Color32 white = new Color32(245, 252, 255, 255);

            DrawFilledCircle(pixels, size, 48, 48, 42, circle);
            DrawFilledCircle(pixels, size, 48, 38, 30, circleDark);
            DrawCircleOutline(pixels, size, 48, 48, 42, 3, cyan);
            DrawCircleOutline(pixels, size, 48, 48, 35, 1, new Color32(255, 255, 255, 80));

            switch (iconKey)
            {
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
            bool soundOn = IsSoundEnabled();
            bool vibrationOn = IsVibrationEnabled();

            ApplyModernToggleVisual(soundToggleTrackImage, soundToggleKnobImage, soundToggleStateText, soundOn);
            ApplyModernToggleVisual(vibrationToggleTrackImage, vibrationToggleKnobImage, vibrationToggleStateText, vibrationOn);

            if (logSoundState)
            {
                Debug.Log($"Sound toggled visual state: {(soundOn ? "ON" : "OFF")}");
            }

            if (logVibrationState)
            {
                Debug.Log($"Vibration toggled visual state: {(vibrationOn ? "ON" : "OFF")}");
            }
        }

        private void ApplyModernToggleVisual(Image trackImage, Image knobImage, TMP_Text stateText, bool enabledState)
        {
            if (trackImage != null)
            {
                trackImage.color = enabledState
                    ? new Color(0.00f, 0.66f, 0.98f, 0.96f)
                    : new Color(0.025f, 0.17f, 0.31f, 0.98f);
                trackImage.raycastTarget = false;
            }

            if (stateText != null)
            {
                RectTransform stateRect = stateText.rectTransform;
                if (stateRect != null)
                {
                    stateRect.offsetMin = enabledState ? new Vector2(10f, 0f) : new Vector2(58f, 0f);
                    stateRect.offsetMax = enabledState ? new Vector2(-58f, 0f) : new Vector2(-10f, 0f);
                    stateRect.localScale = Vector3.one;
                }

                stateText.text = enabledState ? "ON" : "OFF";
                stateText.color = enabledState ? Color.white : new Color(0.78f, 0.93f, 1f, 1f);
                stateText.gameObject.SetActive(true);
            }

            if (knobImage != null)
            {
                RectTransform knobRect = knobImage.rectTransform;
                if (knobRect != null)
                {
                    knobRect.anchoredPosition = new Vector2(enabledState ? 66f : -66f, 0f);
                    knobRect.localScale = Vector3.one;
                }

                knobImage.color = new Color(0.96f, 1f, 1f, 1f);
                knobImage.raycastTarget = false;
            }
        }

        private bool IsSoundEnabled()
        {
            if (AudioManager.Instance != null)
            {
                return !AudioManager.Instance.Muted;
            }

            return PlayerPrefs.GetInt("SoundEnabled", AudioListener.volume > 0.001f ? 1 : 0) != 0;
        }

        private bool IsVibrationEnabled()
        {
            if (SaveManager.Instance != null)
            {
                return !SaveManager.Instance.Data.hapticsMuted;
            }

            return PlayerPrefs.GetInt("VibrationEnabled", 1) != 0;
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
            PlayerPrefs.SetString("SelectedLanguage", languageCode);
            PlayerPrefs.Save();
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

        private void ShowFeedback(string message, Color color, float duration)
        {
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

        private void StyleGameplayButtons()
        {
            StyleOceanControlButton(undoButton);
            StyleOceanControlButton(menuButton);
            StyleOceanControlButton(muteButton);
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

            if (score <= displayedScore || Mathf.Abs(score - displayedScore) <= 8)
            {
                displayedScore = score;
                targetScore = score;
                scoreText.text = displayedScore.ToString("N0");
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
                displayedScore = Mathf.RoundToInt(Mathf.Lerp(displayedScore, targetScore, 0.38f));
                if (Mathf.Abs(targetScore - displayedScore) <= 3)
                {
                    displayedScore = targetScore;
                }

                if (scoreText != null)
                {
                    scoreText.text = displayedScore.ToString("N0");
                }

                yield return null;
            }

            scoreCountRoutine = null;
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
