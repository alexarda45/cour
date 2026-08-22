using System;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

namespace ChromaBlast
{
    public class GameManager : MonoBehaviour
    {
        private static readonly ProfilerMarker TryPlaceMarker = new ProfilerMarker("ChromaBlast.Gameplay.TryPlacePiece");
        private static readonly ProfilerMarker ResolveClearsMarker = new ProfilerMarker("ChromaBlast.Gameplay.ResolveClears");
        private static readonly ProfilerMarker ScoreProcessingMarker = new ProfilerMarker("ChromaBlast.Gameplay.ScoreProcessing");
        private static readonly ProfilerMarker FlowStateMarker = new ProfilerMarker("ChromaBlast.Gameplay.FlowStateUpdate");
        private static readonly ProfilerMarker GenerateNextTrayMarker = new ProfilerMarker("ChromaBlast.Gameplay.GenerateNextTray");
        private static readonly ProfilerMarker HudRefreshMarker = new ProfilerMarker("ChromaBlast.UI.HudRefresh");

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static double LastTryPlaceMilliseconds { get; private set; }
        public static double LastResolveClearsMilliseconds { get; private set; }
        public static double LastScoreProcessingMilliseconds { get; private set; }
        public static double LastFlowStateMilliseconds { get; private set; }
        public static double LastGenerateNextTrayMilliseconds { get; private set; }
        public static double LastHudRefreshMilliseconds { get; private set; }
        public static double LastThirdPieceToNextTrayMilliseconds { get; private set; }
#endif
        [Header("Managers")]
        [SerializeField] private BoardManager board;
        [SerializeField] private PieceSpawner pieceSpawner;
        [SerializeField] private ScoreManager scoreManager;

        [Header("UI")]
        [SerializeField] private GameHUD hud;
        [SerializeField] private GameOverUI gameOverUI;
        [SerializeField] private TutorialOverlay tutorialOverlay;
        [SerializeField] private OceanRescueController oceanRescueController;

        [Header("Ads")]
        [SerializeField] private float interstitialCooldownSeconds = 180f;
        [SerializeField] private int interstitialEveryGameOvers = 3;

        [Header("Assist")]
        [SerializeField] private float moveHintDelaySeconds = 5.5f;

        private GameMode currentMode;
        private System.Random random;
        private UndoSnapshot undoSnapshot;
        private bool undoAvailable;
        private bool active;
        private bool paused;
        private bool applicationPaused;
        private bool applicationFocused = true;
        private bool lifecycleSavePerformedWhileInactive;
        private bool revivedThisRound;
        private float blitzTimeRemaining;
        private float noMoveCheckTimer;
        private float moveHintTimer;
        private float nextPopEscapeHintTime;
        private RoundMission roundMission;
        private int roundLinesCleared;
        private int roundPureLines;
        private int roundPops;
        private int roundBestChain;
        private int movesSinceClear;
        private int traysGeneratedThisRun;
        private int consecutiveReliefBiasedTrays;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Development-only balance telemetry. This stays out of player-facing UI
        // and makes it possible to inspect a live Classic run beside the tray
        // metrics exposed by PieceSpawner.
        public int DebugClassicTrayNumber => currentMode == GameMode.Classic ? traysGeneratedThisRun : 0;
        public int DebugPopUsesThisRun => currentMode == GameMode.Classic ? roundPops : 0;
        public float DebugPopRechargeMultiplier => GetPopRechargeMultiplier();
        public int DebugConsecutiveReliefBiasedTrays => currentMode == GameMode.Classic
            ? consecutiveReliefBiasedTrays
            : 0;
#endif
        private int nextScoreMilestone;
        private int roundStartingBestScore;
        private int liveBestScore;
        private bool achievedNewBestThisRun;
        private bool newBestFeedbackShownThisRun;

        public BoardManager Board => board;
        public bool CanInteract => active
            && !paused
            && (oceanRescueController == null || !oceanRescueController.IsBlockingInput);

        private void Awake()
        {
            MobilePerformance.ApplyDefaults();

            if (board == null)
            {
                board = FindSceneObject<BoardManager>();
            }

            if (pieceSpawner == null)
            {
                pieceSpawner = FindSceneObject<PieceSpawner>();
            }

            if (scoreManager == null)
            {
                scoreManager = FindSceneObject<ScoreManager>();
            }

            if (hud == null)
            {
                hud = FindSceneObject<GameHUD>();
            }

            if (gameOverUI == null)
            {
                gameOverUI = FindSceneObject<GameOverUI>();
            }

            if (tutorialOverlay == null)
            {
                tutorialOverlay = FindSceneObject<TutorialOverlay>();
            }

            if (oceanRescueController == null)
            {
                oceanRescueController = FindSceneObject<OceanRescueController>();
            }
        }

        private void Start()
        {
            gameOverUI?.Hide();

            if (board == null || pieceSpawner == null || scoreManager == null || hud == null || gameOverUI == null)
            {
                Debug.LogError("ChromaBlast: lipsesc referinte in scena Game. Ruleaza Chroma Blast > Genereaza scene si prefab-uri.");
                return;
            }

            gameOverUI.Initialize(this);
            tutorialOverlay?.Initialize(this);
            pieceSpawner.Initialize(this);
            oceanRescueController?.Initialize(this, board, pieceSpawner);
            hud.Initialize(this);
            gameOverUI.Hide();
            scoreManager.Changed += RefreshHud;
            StartGame(GameSession.SelectedMode);
        }

        private void OnDestroy()
        {
            if (scoreManager != null)
            {
                scoreManager.Changed -= RefreshHud;
            }

            if (!lifecycleSavePerformedWhileInactive)
            {
                SaveCurrentClassicRun();
                SaveManager.Instance?.FlushPendingSaveImmediate();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            applicationPaused = pauseStatus;
            if (pauseStatus)
            {
                SaveForLifecycleTransition();
            }
            else
            {
                ResetLifecycleSaveGuardIfActive();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            applicationFocused = hasFocus;
            if (!hasFocus)
            {
                SaveForLifecycleTransition();
            }
            else
            {
                ResetLifecycleSaveGuardIfActive();
            }
        }

        private void OnApplicationQuit()
        {
            SaveForLifecycleTransition();
        }

        private void SaveForLifecycleTransition()
        {
            if (lifecycleSavePerformedWhileInactive)
            {
                return;
            }

            lifecycleSavePerformedWhileInactive = true;

            if (currentMode == GameMode.Classic && active)
            {
                SaveCurrentClassicRun();
                SaveManager.Instance?.FlushPendingSaveImmediate();
                return;
            }

            SaveManager.Instance?.Save();
        }

        private void ResetLifecycleSaveGuardIfActive()
        {
            if (!applicationPaused && applicationFocused)
            {
                lifecycleSavePerformedWhileInactive = false;
            }
        }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
            {
                ForceGameOverDebug();
                return;
            }
#endif

            if (BackButton.WasPressedThisFrame())
            {
                if (oceanRescueController != null && oceanRescueController.IsBlockingInput)
                {
                    oceanRescueController.DeclineRescue();
                    return;
                }

                if (paused)
                {
                    ResumeGame();
                }
                else
                {
                    OpenPauseMenu();
                }

                return;
            }

            if (!active || paused || currentMode != GameMode.Blitz)
            {
                if (active && !paused)
                {
                    TickNoMoveWatchdog();
                    if (active)
                    {
                        TickMoveHint();
                    }
                }

                return;
            }

            TickNoMoveWatchdog();
            if (!active)
            {
                return;
            }

            TickMoveHint();
            if (oceanRescueController == null || !oceanRescueController.IsBlockingInput)
            {
                blitzTimeRemaining -= Time.deltaTime;
                if (blitzTimeRemaining <= 0f)
                {
                    blitzTimeRemaining = 0f;
                    EndGame();
                    return;
                }
            }

            hud?.RefreshBlitzTimer(currentMode, blitzTimeRemaining);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void ForceGameOverDebug()
        {
            if (!active)
            {
                return;
            }

            Debug.Log("[DEBUG] Forced Game Over with F8");
            EndGame();
        }
#endif

        public void StartGame(GameMode mode)
        {
            hud?.ClearActiveComboPresentation();
            AudioManager.Instance?.StopComboVoice();
            currentMode = mode;
            roundStartingBestScore = SaveManager.Instance == null ? 0 : SaveManager.Instance.GetHighScore(mode);
            liveBestScore = roundStartingBestScore;
            achievedNewBestThisRun = false;
            newBestFeedbackShownThisRun = false;
            oceanRescueController?.ResetForNewRound();
            active = true;
            paused = false;
            revivedThisRound = false;
            undoSnapshot = null;
            undoAvailable = false;
            blitzTimeRemaining = GameConstants.BlitzStartSeconds;
            noMoveCheckTimer = 0.35f;
            moveHintTimer = moveHintDelaySeconds;
            nextPopEscapeHintTime = 0f;
            roundLinesCleared = 0;
            roundPureLines = 0;
            roundPops = 0;
            roundBestChain = 0;
            movesSinceClear = 0;
            traysGeneratedThisRun = 0;
            consecutiveReliefBiasedTrays = 0;
            pieceSpawner?.ResetFlowState();
            nextScoreMilestone = GetInitialScoreMilestone();
            hud?.ResetNewBestFeedback();

            int seed = mode == GameMode.Daily && SaveManager.Instance != null
                ? SaveManager.Instance.GetDailySeed()
                : Environment.TickCount ^ Guid.NewGuid().GetHashCode();

            random = new System.Random(seed);
            roundMission = RoundMission.Create(mode, random);
            board.ClearBoard(false);
            scoreManager.ResetScore();
            UpdatePopRechargeRequirement();
            gameOverUI.Hide();
            hud.ShowPause(false);

            bool restoredClassicRun = false;
            if (mode == GameMode.Daily)
            {
                SaveManager.Instance?.RegisterDailyAttempt();
                if (SaveManager.Instance != null)
                {
                    AnalyticsManager.Instance?.RecordDailyAttempt(SaveManager.Instance.GetDailyStreak(), SaveManager.Instance.Data.dailyAttempts);
                }

                TryUnlockAchievement(AchievementId.FirstDaily);
                board.ApplyDailyPrefill(seed + 1337);
            }
            else if (mode == GameMode.Classic)
            {
                restoredClassicRun = TryRestoreClassicRun();
            }

            if (!restoredClassicRun)
            {
                pieceSpawner.SpawnOpeningSatisfyingSet(board, random, GetPieceDifficulty());
                if (pieceSpawner.HasActivePieces())
                {
                    RegisterOpeningTray();
                    ResetMoveHint();
                }
                else
                {
                    SpawnNextSet();
                }
            }

            AnalyticsManager.Instance?.RecordGameStarted(mode);
            RefreshHud();
            RefreshMissionHud();
            ShowTutorialIfNeeded();
            SaveCurrentClassicRun();
        }

        public bool TryPlacePiece(PieceView pieceView, Vector2Int origin)
        {
            if (!CanInteract)
            {
                return false;
            }

            if (!active || pieceView == null || pieceView.Instance == null)
            {
                return false;
            }

            if (!board.CanPlace(pieceView.Instance, origin))
            {
                board.ClearPreview();
                ResetMoveHint(false);
                hud.ShowInvalidMove();
                AudioManager.Instance?.PlayInvalid();
                Haptics.Invalid();
                return false;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long tryPlaceTimingStart = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            TryPlaceMarker.Begin();
            try
            {

            if (currentMode == GameMode.Classic)
            {
                CaptureUndo();
            }

            PieceInstance placedPiece = pieceView.Instance.Clone();
            int setupScoreBeforePlacement = board.ScorePlacementSetup(placedPiece, origin);
            board.PlacePiece(placedPiece, origin);
            pieceView.Consume();
            ResetMoveHint(false);

            AudioManager.Instance?.PlayPlace();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            pieceSpawner?.RecordFlowTargetClearIfCompleted(board);
#endif
            ResolveClearsMarker.Begin();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long resolveTimingStart = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            ClearResult clearResult = board.ResolveClears(placedPiece.color);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LastResolveClearsMilliseconds = ElapsedMilliseconds(resolveTimingStart);
#endif
            ResolveClearsMarker.End();
            if (clearResult.linesCleared == 0)
            {
                Haptics.Place();
            }

            ScoreProcessingMarker.Begin();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long scoreTimingStart = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            ScoreDelta delta = scoreManager.ApplyMove(clearResult, placedPiece.Data.cells.Length);
            bool trayCompleted = pieceSpawner.AllSlotsEmpty();
            int trayBonus = trayCompleted ? CalculateTrayCompleteBonus(clearResult) : 0;
            if (trayBonus > 0)
            {
                scoreManager.AddBonusScore(trayBonus);
            }

            int styleBonus = CalculateSatisfyingClearBonus(clearResult, scoreManager.Chain);
            if (styleBonus > 0)
            {
                scoreManager.AddBonusScore(styleBonus);
            }

            int boardSweepBonus = CalculateBoardSweepBonus(clearResult);
            if (boardSweepBonus > 0)
            {
                scoreManager.AddBonusScore(boardSweepBonus);
            }

            int largePieceBonus = CalculateLargePieceBonus(placedPiece, clearResult);
            if (largePieceBonus > 0)
            {
                scoreManager.AddBonusScore(largePieceBonus);
            }

            int setupBonus = CalculateSetupMoveBonus(placedPiece, clearResult, setupScoreBeforePlacement);
            if (setupBonus > 0)
            {
                scoreManager.AddBonusScore(setupBonus);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LastScoreProcessingMilliseconds = ElapsedMilliseconds(scoreTimingStart);
#endif
            ScoreProcessingMarker.End();

            int totalMoveScore = delta.totalAdded + trayBonus + styleBonus + boardSweepBonus + largePieceBonus + setupBonus;
            SaveManager.Instance?.RegisterMoveStats(clearResult, scoreManager.Chain, totalMoveScore);
            TryAwardDailyQuestMove(clearResult);
            roundLinesCleared += clearResult.linesCleared;
            roundPureLines += clearResult.pureLines;
            roundBestChain = Mathf.Max(roundBestChain, scoreManager.Chain);
            movesSinceClear = clearResult.linesCleared > 0 ? 0 : movesSinceClear + 1;
            TryUnlockAchievement(AchievementId.FirstMove);
            if (clearResult.linesCleared > 0)
            {
                TryUnlockAchievement(AchievementId.FirstClear);
                AudioManager.Instance?.PlayClear(clearResult.linesCleared);
                if (clearResult.pureLines > 0)
                {
                    Haptics.Pure();
                }
                else
                {
                    Haptics.Clear(clearResult.linesCleared, scoreManager.Chain);
                }

                board.PlayComboShake(scoreManager.Chain);
                hud.PunchScore(clearResult, scoreManager.Chain);
                hud.ShowClearFeedback(
                    clearResult,
                    scoreManager.Chain,
                    totalMoveScore,
                    styleBonus,
                    board.LastClearScreenPosition,
                    placedPiece.color);
                if (boardSweepBonus > 0)
                {
                    hud.ShowBoardSweepBonus(boardSweepBonus, board.CountEmptyCells() >= GameConstants.BoardSize * GameConstants.BoardSize);
                    TryUnlockAchievement(AchievementId.BoardSweep);
                    AnalyticsManager.Instance?.RecordBoardSweep(currentMode, boardSweepBonus, board.CountEmptyCells());
                }

                AddBlitzTime(clearResult.linesCleared * GameConstants.BlitzClearTimeBonus);

                if (clearResult.pureLines > 0)
                {
                    TryUnlockAchievement(AchievementId.FirstPure);
                    AudioManager.Instance?.PlayPure();
                    AddBlitzTime(clearResult.pureLines * GameConstants.BlitzPureTimeBonus);
                }

                if (clearResult.linesCleared >= 3)
                {
                    TryUnlockAchievement(AchievementId.TripleClear);
                }

                if (scoreManager.Chain >= 3)
                {
                    TryUnlockAchievement(AchievementId.ChainThree);
                }

            }
            else if (trayBonus > 0)
            {
                hud.PunchScore(false);
                hud.ShowTrayCompleteBonus(trayBonus);
            }
            else if (setupBonus > 0)
            {
                hud.PunchScore(false);
                hud.ShowSetupBonus(setupBonus);
            }
            else if (largePieceBonus > 0)
            {
                hud.PunchScore(false);
                hud.ShowLargePieceBonus(placedPiece.Data.cells.Length, largePieceBonus);
            }
            else if (delta.totalAdded > 0)
            {
                hud.ShowPlacedFeedback(delta.totalAdded);
            }

            if (trayBonus > 0 && clearResult.linesCleared > 0)
            {
                hud.ShowTrayCompleteBonus(trayBonus);
            }

            if (scoreManager.Score >= 1000)
            {
                TryUnlockAchievement(AchievementId.ScoreThousand);
            }

            UpdateMissionAfterMove(clearResult);
            TryAwardScoreMilestones();

            if (trayCompleted)
            {
                // Capture only the board the player actually produced. The
                // resulting FlowState is transient and is used softly by the
                // next tray; no move prediction or save data is involved.
                FlowStateMarker.Begin();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                long flowTimingStart = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
                pieceSpawner.CaptureFlowState(board);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LastFlowStateMilliseconds = ElapsedMilliseconds(flowTimingStart);
#endif
                FlowStateMarker.End();
                SpawnNextSet();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LastThirdPieceToNextTrayMilliseconds = ElapsedMilliseconds(tryPlaceTimingStart);
#endif
            }

            AnalyticsManager.Instance?.RecordMove(currentMode, scoreManager.Score, clearResult.linesCleared, clearResult.pureLines);
            RefreshHud();
            EvaluateGameOver();
            SaveCurrentClassicRun();
            return true;
            }
            finally
            {
                TryPlaceMarker.End();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LastTryPlaceMilliseconds = ElapsedMilliseconds(tryPlaceTimingStart);
#endif
            }
        }

        public void RequestPop(ChromaColor color)
        {
            if (!active || !scoreManager.IsPopReady(color))
            {
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            TracePopState("BEFORE", color);
#endif

            int targetCount = board == null ? 0 : board.CountCellsOfColor(color);
            if (targetCount <= 0)
            {
                hud?.ShowPopUnavailable(color);
                hud?.PunchChroma(color);
                Haptics.Light();
                return;
            }

            if (currentMode == GameMode.Classic)
            {
                CaptureUndo();
            }

            int popped = board.PopColor(color);
            // Commit charge consumption and the new fatigue requirement as one
            // state transition. This prevents ScoreManager.Changed from causing
            // an intermediate HUD refresh before sibling latches are observed.
            ScoreDelta delta = scoreManager.ApplyPop(color, popped, notifyChanged: false);
            ResetMoveHint(false);
            nextPopEscapeHintTime = 0f;
            if (popped > 0)
            {
                roundPops++;
                UpdatePopRechargeRequirement(notifyChanged: false);
                movesSinceClear = 0;
                SaveManager.Instance?.RegisterPopStats(popped);
                TryAwardDailyQuestPop(popped);
                TryUnlockAchievement(AchievementId.FirstPop);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            TracePopState("AFTER GAMEPLAY STATE", color);
#endif

            AudioManager.Instance?.PlayPop();
            Haptics.Pop();
            hud.PunchScore(true);
            hud.PunchChroma(color);
            hud.ShowPopFeedback(color, popped, delta.totalAdded);
            AddBlitzTime(GameConstants.BlitzPopTimeBonus);
            UpdateMissionAfterPop(popped);
            TryAwardScoreMilestones();
            AnalyticsManager.Instance?.RecordPop(currentMode, color, popped, delta.totalAdded);
            RefreshHud();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            TracePopState("AFTER HUD", color);
#endif
            EvaluateGameOver();
            SaveCurrentClassicRun();
        }

        public void RequestRewardedChromaFill()
        {
            if (!active)
            {
                return;
            }

            ChromaColor target = FindLowestChromaColor();
            AdManager.Instance?.ShowRewarded("chroma_fill", () =>
            {
                scoreManager.AddRewardedChroma(target);
                hud.PunchChroma(target);
                hud.ShowRewardedChromaReady(target);
                AnalyticsManager.Instance?.RecordRewardedCompleted("chroma_fill");
                RefreshHud();

                if (!pieceSpawner.AnyActivePieceFits(board))
                {
                    SpawnNextSet(0.65f);
                    RefreshHud();
                }

                SaveCurrentClassicRun();
            });
        }

        public void RequestRewardedContinue()
        {
            if (active || revivedThisRound || currentMode == GameMode.Daily)
            {
                return;
            }

            AdManager.Instance?.ShowRewarded("revive", ReviveAfterRewardedAd);
        }

        public void UndoLastMove()
        {
            if (currentMode != GameMode.Classic || !undoAvailable || undoSnapshot == null)
            {
                return;
            }

            board.Restore(undoSnapshot.board);
            scoreManager.Restore(undoSnapshot.score);
            pieceSpawner.RestoreTray(undoSnapshot.trayPieces);
            blitzTimeRemaining = undoSnapshot.blitzTimeRemaining;
            movesSinceClear = undoSnapshot.movesSinceClear;
            ResetMoveHint();
            undoSnapshot = null;
            undoAvailable = false;
            active = true;
            gameOverUI.Hide();
            AnalyticsManager.Instance?.RecordUndo();
            RefreshHud();
            SaveCurrentClassicRun();
        }

        public void RestartCurrentMode()
        {
            AudioManager.Instance?.PlayClick();
            paused = false;
            hud.ShowPause(false);
            if (currentMode == GameMode.Classic)
            {
                SaveManager.Instance?.ClearClassicRun();
            }

            StartGame(currentMode);
            SaveManager.Instance?.FlushPendingSaveImmediate();
        }

        public void GoToMenu()
        {
            AudioManager.Instance?.PlayClick();
            hud?.ClearActiveComboPresentation();
            AudioManager.Instance?.StopComboVoice();
            pieceSpawner?.ResetFlowState();
            SaveCurrentClassicRun();
            SaveManager.Instance?.FlushPendingSaveImmediate();
            paused = false;
            SceneManager.LoadScene("Menu");
        }

        public void OpenPauseMenu()
        {
            if (!active)
            {
                GoToMenu();
                return;
            }

            AudioManager.Instance?.PlayClick();
            paused = true;
            board.ClearPreview();
            ResetMoveHint(false);
            hud.ShowPause(true);
        }

        public void ResumeGame()
        {
            if (!active)
            {
                return;
            }

            AudioManager.Instance?.PlayClick();
            paused = false;
            ResetMoveHint();
            hud.ShowPause(false);
        }

        public void CloseTutorial()
        {
            AudioManager.Instance?.PlayClick();
            tutorialOverlay?.Hide();
            SaveManager.Instance?.MarkTutorialSeen();
        }

        public void ShowNoFitPieceHint()
        {
            ResetMoveHint();
            hud?.ShowNoFitPiece();
        }

        public void ClearMoveHint()
        {
            ResetMoveHint();
        }

        public void ShowPlacementPreview(int lineCount, int pureLineCount)
        {
            if (!CanInteract)
            {
                return;
            }

            hud?.ShowPlacementPreview(lineCount, pureLineCount);
        }

        public void ShowSmartMoveHint()
        {
            if (!CanInteract)
            {
                return;
            }

            hud?.ShowSmartMoveHint();
        }

        private void UpdateMissionAfterMove(ClearResult clearResult)
        {
            if (roundMission == null)
            {
                return;
            }

            bool completedNow = roundMission.RegisterMove(clearResult, scoreManager.Chain, scoreManager.Score);
            RefreshMissionHud();
            if (completedNow)
            {
                CompleteRoundMission();
            }
        }

        private void UpdateMissionAfterPop(int popped)
        {
            if (roundMission == null)
            {
                return;
            }

            bool completedNow = roundMission.RegisterPop(popped, scoreManager.Score);
            RefreshMissionHud();
            if (completedNow)
            {
                CompleteRoundMission();
            }
        }

        private void CompleteRoundMission()
        {
            scoreManager.AddBonusScore(roundMission.rewardScore);
            hud?.ShowMissionComplete(roundMission.rewardScore);
            if (scoreManager.Score >= 1000)
            {
                TryUnlockAchievement(AchievementId.ScoreThousand);
            }

            AudioManager.Instance?.PlayPure();
            Haptics.Medium();
            AnalyticsManager.Instance?.RecordMissionCompleted(currentMode, roundMission.type, roundMission.rewardScore);
        }

        private void TryAwardDailyQuestMove(ClearResult clearResult)
        {
            SaveManager save = SaveManager.Instance;
            if (save == null)
            {
                return;
            }

            int coins = save.RegisterDailyQuestMove(clearResult, scoreManager.Score, out string questName);
            if (coins <= 0)
            {
                return;
            }

            hud?.ShowDailyQuestReward(questName, coins);
            AudioManager.Instance?.PlayPure();
            Haptics.Medium();
            AnalyticsManager.Instance?.RecordDailyQuestClaimed(questName, coins);
        }

        private void TryAwardDailyQuestPop(int popped)
        {
            SaveManager save = SaveManager.Instance;
            if (save == null)
            {
                return;
            }

            int coins = save.RegisterDailyQuestPop(popped, scoreManager.Score, out string questName);
            if (coins <= 0)
            {
                return;
            }

            hud?.ShowDailyQuestReward(questName, coins);
            AudioManager.Instance?.PlayPure();
            Haptics.Medium();
            AnalyticsManager.Instance?.RecordDailyQuestClaimed(questName, coins);
        }

        private void RefreshMissionHud()
        {
            hud?.SetMission(roundMission == null ? string.Empty : roundMission.GetHudText(), roundMission != null && roundMission.completed);
        }

        private void TryUnlockAchievement(AchievementId achievement)
        {
            SaveManager save = SaveManager.Instance;
            if (save == null || !save.TryUnlockAchievement(achievement, out AchievementReward reward))
            {
                return;
            }

            hud?.ShowAchievement(reward);
            AudioManager.Instance?.PlayPure();
            Haptics.Medium();
            AnalyticsManager.Instance?.RecordAchievementUnlocked(achievement, reward.coins);
        }

        public void ResetTutorialFromPause()
        {
            AudioManager.Instance?.PlayClick();
            SaveManager.Instance?.ResetTutorial();
            tutorialOverlay?.Initialize(this);
            tutorialOverlay?.Show();
        }

        public void ShowHowToPlayFromPause()
        {
            AudioManager.Instance?.PlayClick();
            if (tutorialOverlay == null)
            {
                Debug.LogWarning("How To Play popup is not available in this scene.");
                return;
            }

            tutorialOverlay.Initialize(this);
            tutorialOverlay.Show();
        }

        private bool TryRestoreClassicRun()
        {
            SaveManager save = SaveManager.Instance;
            ClassicRunState run = save == null ? null : save.GetClassicRun();
            if (run == null)
            {
                return false;
            }

            if (run.board == null || run.score == null || run.trayPieces == null)
            {
                save.ClearClassicRun();
                return false;
            }

            board.Restore(run.board);
            scoreManager.Restore(run.score);
            pieceSpawner.RestoreTray(run.trayPieces);
            undoSnapshot = run.undoSnapshot;
            undoAvailable = run.undoAvailable && undoSnapshot != null;
            roundMission = run.roundMission ?? RoundMission.Create(GameMode.Classic, random);
            roundLinesCleared = Mathf.Max(0, run.roundLinesCleared);
            roundPureLines = Mathf.Max(0, run.roundPureLines);
            roundPops = Mathf.Max(0, run.roundPops);
            roundBestChain = Mathf.Max(0, run.roundBestChain);
            movesSinceClear = Mathf.Max(0, run.movesSinceClear);
            traysGeneratedThisRun = Mathf.Max(0, run.traysGeneratedThisRun);
            consecutiveReliefBiasedTrays = Mathf.Max(0, run.consecutiveReliefBiasedTrays);
            UpdatePopRechargeRequirement();
            nextScoreMilestone = run.nextScoreMilestone > 0
                ? run.nextScoreMilestone
                : GetNextScoreMilestoneAbove(scoreManager.Score);
            revivedThisRound = run.revivedThisRound;
            oceanRescueController?.RestoreConsumedState(
                run.oceanRescueConsumedThisRound);
            active = true;
            paused = false;
            gameOverUI.Hide();
            ResetMoveHint();

            if (!pieceSpawner.HasActivePieces())
            {
                SpawnNextSet(0.35f);
            }

            return true;
        }

        private void SaveCurrentClassicRun()
        {
            if (currentMode != GameMode.Classic || !active || SaveManager.Instance == null)
            {
                return;
            }

            if (board == null || scoreManager == null || pieceSpawner == null)
            {
                return;
            }

            SaveManager.Instance.SaveClassicRun(new ClassicRunState
            {
                active = true,
                board = board.CreateSnapshot(),
                score = scoreManager.CreateSnapshot(),
                trayPieces = pieceSpawner.GetActivePieceSnapshots(),
                undoAvailable = undoAvailable,
                undoSnapshot = undoAvailable ? undoSnapshot : null,
                roundMission = roundMission,
                roundLinesCleared = roundLinesCleared,
                roundPureLines = roundPureLines,
                roundPops = roundPops,
                roundBestChain = roundBestChain,
                movesSinceClear = movesSinceClear,
                traysGeneratedThisRun = traysGeneratedThisRun,
                consecutiveReliefBiasedTrays = consecutiveReliefBiasedTrays,
                nextScoreMilestone = nextScoreMilestone,
                revivedThisRound = revivedThisRound,
                oceanRescueConsumedThisRound =
                    oceanRescueController != null
                    && oceanRescueController.ConsumedThisRound
            });
        }

        private void EndGame()
        {
            if (!active)
            {
                return;
            }

            oceanRescueController?.HideForGameOver();
            int finalScore = scoreManager.Score;
            active = false;
            paused = false;
            board.ClearPreview();
            hud.ShowPause(false);
            hud.ClearActiveComboPresentation();
            AudioManager.Instance?.StopComboVoice();
            if (currentMode == GameMode.Classic)
            {
                SaveManager.Instance?.ClearClassicRunDeferred();
            }

            AudioManager.Instance?.PlayGameOver();
            Haptics.Heavy();

            SaveManager save = SaveManager.Instance;
            int previousRankPoints = save == null ? 0 : save.Data.rankPoints;
            int previousHighScore = roundStartingBestScore;
            bool newBest = achievedNewBestThisRun && finalScore > roundStartingBestScore && finalScore > 0;
            int coinsEarned = 0;
            int dailyMedalCoins = 0;
            string dailyMedalName = string.Empty;
            if (save != null)
            {
                coinsEarned = save.RegisterGameOver(currentMode, finalScore, newBest);
                if (currentMode == GameMode.Daily && save.TryClaimDailyMedalReward(finalScore, out DailyMedalInfo medal, out dailyMedalCoins))
                {
                    dailyMedalName = medal.name;
                    coinsEarned += dailyMedalCoins;
                    AnalyticsManager.Instance?.RecordDailyMedalClaimed(medal.name, finalScore, dailyMedalCoins);
                }
            }

            AnalyticsManager.Instance?.RecordGameOver(currentMode, finalScore, scoreManager.Chain, coinsEarned);
            int highScore = save == null ? finalScore : save.GetHighScore(currentMode);
            int rankPoints = save == null ? previousRankPoints : save.Data.rankPoints;
            int xpGained = Mathf.Max(0, rankPoints - previousRankPoints);
            int totalCoins = save == null ? coinsEarned : save.GetCoins();
            bool canRevive = !revivedThisRound && currentMode != GameMode.Daily;
            gameOverUI.Show(currentMode, finalScore, highScore, canRevive, xpGained, rankPoints, coinsEarned, totalCoins, roundLinesCleared, roundPureLines, roundPops, roundBestChain, previousRankPoints, dailyMedalCoins, dailyMedalName, newBest);
            RefreshHud();

            if (save != null && save.CanShowInterstitial(interstitialCooldownSeconds, interstitialEveryGameOvers))
            {
                bool interstitialShown = AdManager.Instance != null
                    && AdManager.Instance.ShowInterstitial();
                if (interstitialShown)
                {
                    save.MarkInterstitialShown();
                }
            }

            save?.FlushPendingSaveImmediate();
        }

        private void EvaluateGameOver()
        {
            if (!active)
            {
                return;
            }

            if (!pieceSpawner.HasActivePieces())
            {
                SpawnNextSet();
                RefreshHud();
                return;
            }

            if (!pieceSpawner.AnyActivePieceFits(board))
            {
                if (TryHoldGameOverForReadyPop())
                {
                    return;
                }

                if (oceanRescueController != null
                    && oceanRescueController.TryInterceptBlockedRound())
                {
                    return;
                }

                EndGame();
            }
        }

        internal void ContinueToGameOverAfterOceanRescue()
        {
            if (active)
            {
                EndGame();
            }
        }

        internal void ResumeAfterOceanRescue()
        {
            if (!active)
            {
                return;
            }

            ResetMoveHint();
            RefreshHud();
            EvaluateGameOver();
            SaveCurrentClassicRun();
        }

        internal void PersistOceanRescueConsumedState()
        {
            SaveCurrentClassicRun();
        }

        private bool TryHoldGameOverForReadyPop()
        {
            if (scoreManager == null || board == null)
            {
                return false;
            }

            for (int i = 0; i < GameConstants.ColorCount; i++)
            {
                ChromaColor color = (ChromaColor)i;
                if (!scoreManager.IsPopReady(color) || board.CountCellsOfColor(color) <= 0)
                {
                    continue;
                }

                if (Time.unscaledTime >= nextPopEscapeHintTime)
                {
                    hud?.ShowUsePopToContinue(color);
                    hud?.PunchChroma(color);
                    Haptics.Light();
                    nextPopEscapeHintTime = Time.unscaledTime + 2.4f;
                }

                return true;
            }

            return false;
        }

        private void TickNoMoveWatchdog()
        {
            noMoveCheckTimer -= Time.deltaTime;
            if (noMoveCheckTimer > 0f)
            {
                return;
            }

            noMoveCheckTimer = 0.35f;
            EvaluateGameOver();
        }

        private void TickMoveHint()
        {
            if (pieceSpawner == null || board == null || (tutorialOverlay != null && tutorialOverlay.IsVisible))
            {
                return;
            }

            moveHintTimer -= Time.deltaTime;
            if (moveHintTimer > 0f)
            {
                return;
            }

            if (pieceSpawner.ShowBestMoveHint(board))
            {
                moveHintTimer = Mathf.Max(2.6f, GetCurrentMoveHintDelay() * 1.35f);
            }
            else
            {
                moveHintTimer = 1f;
            }
        }

        private void ResetMoveHint(bool clearPreview = true)
        {
            moveHintTimer = GetCurrentMoveHintDelay();
            if (clearPreview)
            {
                board?.ClearPreview();
            }
        }

        private float GetCurrentMoveHintDelay()
        {
            float noClearPressure = movesSinceClear <= 1 ? 0f : Mathf.Clamp01((movesSinceClear - 1) / 4f);
            float tightBoardPressure = board == null ? 0f : Mathf.Clamp01((24f - board.CountEmptyCells()) / 18f);
            float pressure = Mathf.Clamp01(noClearPressure * 0.72f + tightBoardPressure * 0.28f);
            return Mathf.Lerp(moveHintDelaySeconds, 2.25f, pressure);
        }

        private void CaptureUndo()
        {
            undoSnapshot = new UndoSnapshot
            {
                board = board.CreateSnapshot(),
                score = scoreManager.CreateSnapshot(),
                trayPieces = pieceSpawner.GetActivePieceSnapshots(),
                blitzTimeRemaining = blitzTimeRemaining,
                movesSinceClear = movesSinceClear
            };
            undoAvailable = true;
        }

        private void ReviveAfterRewardedAd()
        {
            revivedThisRound = true;
            active = true;
            gameOverUI.Hide();
            board.ClearRandomCells(16, random);
            movesSinceClear = 0;
            SpawnNextSet(0.85f);
            int safetyClears = 0;
            while (!pieceSpawner.AnyActivePieceFits(board) && safetyClears < 3)
            {
                board.ClearRandomCells(4, random);
                SpawnNextSet(1f);
                safetyClears++;
            }

            if (currentMode == GameMode.Blitz)
            {
                blitzTimeRemaining = Mathf.Max(blitzTimeRemaining, 15f);
            }

            AnalyticsManager.Instance?.RecordRewardedCompleted("revive");
            RefreshHud();
            EvaluateGameOver();
            SaveCurrentClassicRun();
        }

        private void AddBlitzTime(float amount)
        {
            if (currentMode == GameMode.Blitz && amount > 0f)
            {
                blitzTimeRemaining += amount;
                hud?.ShowTimeBonus(amount);
            }
        }

        private ChromaColor FindLowestChromaColor()
        {
            ChromaColor target = ChromaColor.Cyan;
            int lowest = int.MaxValue;
            for (int i = 0; i < GameConstants.ColorCount; i++)
            {
                ChromaColor color = (ChromaColor)i;
                int amount = scoreManager.GetChroma(color);
                if (amount < lowest)
                {
                    lowest = amount;
                    target = color;
                }
            }

            return target;
        }

        private int CalculateTrayCompleteBonus(ClearResult clearResult)
        {
            int lineBonus = clearResult == null ? 0 : clearResult.linesCleared * 24;
            int pureBonus = clearResult == null ? 0 : clearResult.pureLines * 32;
            int chainBonus = Mathf.Max(0, scoreManager.Chain - 1) * 12;
            int emptySpaceBonus = board == null ? 0 : Mathf.Clamp(board.CountEmptyCells() / 5, 0, 12);
            return Mathf.Clamp(42 + lineBonus + pureBonus + chainBonus + emptySpaceBonus, 42, 180);
        }

        private int CalculateLargePieceBonus(PieceInstance piece, ClearResult clearResult)
        {
            if (piece == null)
            {
                return 0;
            }

            int cells = piece.Data.cells.Length;
            if (cells < 5)
            {
                return 0;
            }

            int bonus = (cells - 4) * 16;
            if (clearResult == null || clearResult.linesCleared <= 0)
            {
                bonus += 12;
            }

            return Mathf.Clamp(bonus, 0, 64);
        }

        private int CalculateSetupMoveBonus(PieceInstance piece, ClearResult clearResult, int setupScore)
        {
            if (piece == null || clearResult == null || clearResult.linesCleared > 0 || setupScore < 54)
            {
                return 0;
            }

            int cells = piece.Data.cells.Length;
            int bonus = 18 + setupScore / 3 + cells * 2;
            return Mathf.Clamp(bonus, 24, 110);
        }

        private int CalculateBoardSweepBonus(ClearResult clearResult)
        {
            if (clearResult == null || clearResult.linesCleared <= 0 || board == null)
            {
                return 0;
            }

            int emptyCells = board.CountEmptyCells();
            if (emptyCells >= GameConstants.BoardSize * GameConstants.BoardSize)
            {
                return 420;
            }

            if (emptyCells >= 58 && clearResult.cellsCleared >= 12)
            {
                return 240;
            }

            if (emptyCells >= 54 && clearResult.linesCleared >= 3)
            {
                return 170;
            }

            return 0;
        }

        private int CalculateSatisfyingClearBonus(ClearResult clearResult, int chain)
        {
            if (clearResult == null || clearResult.linesCleared <= 0)
            {
                return 0;
            }

            int bonus = 0;
            if (clearResult.linesCleared >= 2)
            {
                bonus += (clearResult.linesCleared - 1) * 90;
            }

            if (clearResult.linesCleared >= 4)
            {
                bonus += 180;
            }

            if (clearResult.pureLines > 0)
            {
                bonus += clearResult.pureLines * 130;
            }

            if (chain >= 2)
            {
                float chainMultiplier = ScoreManager.GetChainScoreMultiplier(chain);
                bonus += Mathf.RoundToInt(chainMultiplier * 100f);
            }

            if (clearResult.cellsCleared >= 16)
            {
                bonus += 120;
            }

            return Mathf.Max(0, bonus);
        }

        private void TryAwardScoreMilestones()
        {
            if (scoreManager == null || SaveManager.Instance == null)
            {
                return;
            }

            if (nextScoreMilestone <= 0)
            {
                nextScoreMilestone = GetNextScoreMilestoneAbove(scoreManager.Score);
            }

            int awardedCoins = 0;
            int highestMilestone = 0;
            int guard = 0;
            while (scoreManager.Score >= nextScoreMilestone && guard < 12)
            {
                highestMilestone = nextScoreMilestone;
                awardedCoins += CalculateScoreMilestoneCoins(nextScoreMilestone);
                nextScoreMilestone = GetNextScoreMilestone(nextScoreMilestone);
                guard++;
            }

            if (awardedCoins <= 0)
            {
                return;
            }

            SaveManager.Instance.AddCoins(awardedCoins);
            hud?.ShowScoreMilestone(highestMilestone, awardedCoins);
            AudioManager.Instance?.PlayPure();
            Haptics.Light();
            AnalyticsManager.Instance?.RecordScoreMilestone(currentMode, highestMilestone, awardedCoins);
        }

        private int GetInitialScoreMilestone()
        {
            return 1000;
        }

        private int GetNextScoreMilestoneAbove(int score)
        {
            int milestone = GetInitialScoreMilestone();
            int guard = 0;
            while (score >= milestone && guard < 24)
            {
                milestone = GetNextScoreMilestone(milestone);
                guard++;
            }

            return milestone;
        }

        private int GetNextScoreMilestone(int currentMilestone)
        {
            if (currentMilestone < 5000)
            {
                return currentMilestone + 1000;
            }

            if (currentMilestone < 10000)
            {
                return currentMilestone + 2500;
            }

            return currentMilestone + 5000;
        }

        private int CalculateScoreMilestoneCoins(int milestoneScore)
        {
            return Mathf.Clamp(6 + milestoneScore / 500, 8, 60);
        }

        private float GetPieceDifficulty()
        {
            if (scoreManager == null)
            {
                return 0.18f;
            }

            float scoreDifficulty = Mathf.Clamp01(scoreManager.Score / 14000f);
            switch (currentMode)
            {
                case GameMode.Blitz:
                    return Mathf.Clamp01(0.22f + scoreDifficulty * 0.68f);
                case GameMode.Daily:
                    return Mathf.Clamp01(0.18f + scoreDifficulty * 0.58f);
                default:
                    return Mathf.Clamp01(0.10f + scoreDifficulty * 0.62f);
            }
        }

        private float GetPieceAssist()
        {
            float noClearAssist = movesSinceClear <= 1 ? 0f : Mathf.Clamp01((movesSinceClear - 1) / 3.5f);
            float tightBoardAssist = board == null ? 0f : Mathf.Clamp01((24f - board.CountEmptyCells()) / 18f);
            return Mathf.Clamp01(noClearAssist * 0.80f + tightBoardAssist * 0.24f);
        }

        private void RegisterOpeningTray()
        {
            if (currentMode == GameMode.Classic)
            {
                traysGeneratedThisRun = 1;
                consecutiveReliefBiasedTrays = 0;
            }
        }

        private float GetClassicRunPressure(int trayNumber)
        {
            if (currentMode != GameMode.Classic)
            {
                return 0f;
            }

            float trayPressure;
            if (trayNumber <= 5)
            {
                trayPressure = 0f;
            }
            else if (trayNumber <= 10)
            {
                trayPressure = Mathf.Lerp(0.03f, 0.18f, (trayNumber - 6) / 4f);
            }
            else if (trayNumber <= 18)
            {
                trayPressure = Mathf.Lerp(0.22f, 0.58f, (trayNumber - 11) / 7f);
            }
            else
            {
                trayPressure = Mathf.Min(0.75f, 0.62f + (trayNumber - 19) * 0.025f);
            }

            // POP remains a powerful rescue. Its use contributes only a small
            // extra planning demand and never creates a sudden pressure jump.
            float popPressure = Mathf.Min(0.12f, roundPops * 0.03f);
            return Mathf.Clamp01(trayPressure + popPressure);
        }

        private float GetPopRechargeMultiplier()
        {
            if (currentMode != GameMode.Classic)
            {
                return 1f;
            }

            switch (roundPops)
            {
                case 0:
                    return 1f;
                case 1:
                    return 1.35f;
                case 2:
                    return 1.70f;
                case 3:
                    return 2.10f;
                default:
                    return 2.50f;
            }
        }

        private void UpdatePopRechargeRequirement(bool notifyChanged = true)
        {
            scoreManager?.SetPopRechargeMultiplier(GetPopRechargeMultiplier(), notifyChanged);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void TracePopState(string stage, ChromaColor selectedColor)
        {
            if (scoreManager == null)
            {
                return;
            }

            for (int i = 0; i < GameConstants.ColorCount; i++)
            {
                ChromaColor tracedColor = (ChromaColor)i;
                int targetCount = board == null ? 0 : board.CountCellsOfColor(tracedColor);
                bool visible = false;
                bool interactable = false;
                bool hasViewState = hud != null && hud.DebugTryGetPopViewState(
                    tracedColor,
                    out visible,
                    out interactable);
                Debug.Log(
                    $"[POP STATE] {stage}; used={selectedColor}; color={tracedColor}; "
                    + $"charge={scoreManager.GetChroma(tracedColor)}; "
                    + $"required={scoreManager.CurrentPopRequirement}; "
                    + $"ready={scoreManager.IsPopReady(tracedColor)}; "
                    + $"latched={scoreManager.DebugGetPopReadyLatch(tracedColor)}; "
                    + $"targets={targetCount}; visible={(hasViewState && visible)}; "
                    + $"interactable={(hasViewState && interactable)}");
            }
        }
#endif

        private void SpawnNextSet(float assistOverride = -1f)
        {
            GenerateNextTrayMarker.Begin();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long timingStart = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            try
            {
            float assist = assistOverride >= 0f ? Mathf.Clamp01(assistOverride) : GetPieceAssist();
            int nextTrayNumber = currentMode == GameMode.Classic
                ? traysGeneratedThisRun + 1
                : 0;
            float runPressure = GetClassicRunPressure(nextTrayNumber);
            pieceSpawner.SpawnGuaranteedSet(
                board,
                random,
                GetPieceDifficulty(),
                assist,
                runPressure,
                currentMode == GameMode.Classic ? consecutiveReliefBiasedTrays : 0,
                nextTrayNumber);
            if (currentMode == GameMode.Classic)
            {
                traysGeneratedThisRun = nextTrayNumber;
                consecutiveReliefBiasedTrays = pieceSpawner.LastGenerationReliefBiased
                    ? consecutiveReliefBiasedTrays + 1
                    : 0;
            }
            ResetMoveHint();
            }
            finally
            {
                GenerateNextTrayMarker.End();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LastGenerateNextTrayMilliseconds = ElapsedMilliseconds(timingStart);
#endif
            }
        }

        private void RefreshHud()
        {
            HudRefreshMarker.Begin();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long timingStart = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            try
            {
            if (hud == null || scoreManager == null)
            {
                return;
            }

            SaveManager save = SaveManager.Instance;
            if (save != null)
            {
                liveBestScore = Mathf.Max(liveBestScore, save.GetHighScore(currentMode));
            }

            int currentScore = scoreManager.Score;
            if (active && currentScore > liveBestScore)
            {
                liveBestScore = currentScore;
                if (save != null && save.TrySetHighScore(currentMode, currentScore))
                {
                    save.RequestSave();
                }

                achievedNewBestThisRun = currentScore > roundStartingBestScore;
                if (achievedNewBestThisRun && !newBestFeedbackShownThisRun)
                {
                    newBestFeedbackShownThisRun = true;
                    hud.ShowNewBestFeedback();
                }
            }

            hud.Refresh(currentMode, scoreManager, liveBestScore, blitzTimeRemaining, undoAvailable, nextScoreMilestone, GetPopTargetCounts());
            RefreshMissionHud();
            pieceSpawner?.RefreshFitHints(board);
            }
            finally
            {
                HudRefreshMarker.End();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LastHudRefreshMilliseconds = ElapsedMilliseconds(timingStart);
#endif
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static double ElapsedMilliseconds(long startTimestamp)
        {
            return (System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp)
                * 1000.0
                / System.Diagnostics.Stopwatch.Frequency;
        }
#endif

        private int[] GetPopTargetCounts()
        {
            int[] counts = new int[GameConstants.ColorCount];
            if (board == null)
            {
                return counts;
            }

            for (int i = 0; i < counts.Length; i++)
            {
                counts[i] = board.CountCellsOfColor((ChromaColor)i);
            }

            return counts;
        }

        private void ShowTutorialIfNeeded()
        {
            if (tutorialOverlay == null)
            {
                return;
            }

            if (SaveManager.Instance == null || SaveManager.Instance.Data.tutorialSeen)
            {
                return;
            }

            tutorialOverlay.Show();
        }

        private static T FindSceneObject<T>() where T : Component
        {
            T activeObject = FindAnyObjectByType<T>();
            if (activeObject != null)
            {
                return activeObject;
            }

            T[] objects = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < objects.Length; i++)
            {
                T candidate = objects[i];
                if (candidate != null && candidate.gameObject.scene.IsValid())
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
