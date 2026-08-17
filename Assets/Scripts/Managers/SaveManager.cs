using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace ChromaBlast
{
    public enum DailyQuestType
    {
        PlayMoves,
        ClearLines,
        MakePure,
        UsePop,
        ScorePoints
    }

    [Serializable]
    public class DailyQuestState
    {
        public DailyQuestType type;
        public int target;
        public int progress;
        public int rewardCoins;
        public bool claimed;
    }

    [Serializable]
    public class SaveData
    {
        public int schemaVersion = 7;
        public bool soundMuted;
        public bool hapticsMuted;
        public int performanceMode;
        public bool removeAds;
        public int classicHighScore;
        public int blitzHighScore;
        public int dailyHighScore;
        public int rankPoints;
        public int coins;
        public long achievementMask;
        public int gamesPlayed;
        public int totalMoves;
        public int totalLinesCleared;
        public int totalPureLines;
        public int totalPops;
        public int totalPopCells;
        public int bestChain;
        public int bestMoveScore;
        public string dailyDateKey;
        public int dailyBestScore;
        public int dailyAttempts;
        public int dailyStreak;
        public int dailyClaimedMedalIndex;
        public string dailyQuestDateKey;
        public DailyQuestState[] dailyQuests;
        public string lastDailyPlayedDateKey;
        public string lastDailyGiftDateKey;
        public int dailyRewardDayIndex;
        public string lastDailyRewardedAdDate;
        public int dailyRewardedAdCount;
        public int gameOversSinceInterstitial;
        public long lastInterstitialUnix;
        public bool tutorialSeen;
        public int selectedTheme;
        public int unlockedThemeMask = 1 << (int)ThemeType.Ocean;
        public int themeOwnershipVersion;
        public bool cosmeticPackOwned;
        public ClassicRunState classicRun;
    }

    [Serializable]
    public class ClassicRunState
    {
        public bool active;
        public BoardSnapshot board;
        public ScoreSnapshot score;
        public PieceInstance[] trayPieces;
        public bool undoAvailable;
        public UndoSnapshot undoSnapshot;
        public RoundMission roundMission;
        public int roundLinesCleared;
        public int roundPureLines;
        public int roundPops;
        public int roundBestChain;
        public int movesSinceClear;
        public int nextScoreMilestone;
        public bool revivedThisRound;
        public bool oceanRescueConsumedThisRound;
        public long savedUnix;
    }

    public class SaveManager : MonoBehaviour
    {
        public const int DailyRewardDayCount = 7;
        public const int DailyRewardedAdLimit = 3;
        public const int DailyRewardedAdCoins = 25;

        private static readonly int[] DailyRewardCoins =
        {
            50,
            100,
            150,
            175,
            225,
            300,
            0
        };

        public static SaveManager Instance { get; private set; }

        public SaveData Data { get; private set; } = new SaveData();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool dailyRewardsDebugMode;
        private string dailyRewardsProductionSnapshot;
        private string dailyRewardsDebugSerializedState;

        public bool IsDailyRewardsDebugMode => dailyRewardsDebugMode;
#endif

        private const string SaveFileName = "chroma_blast_save.json";
        private const string TemporarySaveFileName = "chroma_blast_save.tmp";
        private const string BackupSaveFileName = "chroma_blast_save.backup.json";
        private const float NormalSaveDebounceSeconds = 0.28f;
        private const float MaximumDirtySeconds = 1.75f;

        private bool pendingSaveDirty;
        private float firstDirtyTime;
        private float latestSaveRequestTime;
        private Coroutine pendingSaveRoutine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public double LastFullSaveMilliseconds { get; private set; }
#endif

        private string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);
        private string TemporarySavePath => Path.Combine(Application.persistentDataPath, TemporarySaveFileName);
        private string BackupSavePath => Path.Combine(Application.persistentDataPath, BackupSaveFileName);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            FlushPendingSaveImmediate();
            Instance = null;
        }

        public void Load()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (dailyRewardsDebugMode && !string.IsNullOrEmpty(dailyRewardsDebugSerializedState))
            {
                SaveData debugData = JsonUtility.FromJson<SaveData>(dailyRewardsDebugSerializedState);
                if (debugData != null)
                {
                    Data = debugData;
                    return;
                }

                Debug.LogError("[Daily Rewards Debug] Could not reload the isolated debug save state.");
                return;
            }
#endif

            bool saveMigratedData = false;
            bool mainSaveExists = File.Exists(SavePath);
            bool backupSaveExists = File.Exists(BackupSavePath);

            if (TryReadSaveFile(SavePath, out SaveData mainData, out string mainFailure))
            {
                Data = mainData;
            }
            else if (TryReadSaveFile(BackupSavePath, out SaveData backupData, out string backupFailure))
            {
                Data = backupData;
                saveMigratedData = true;
                Debug.LogWarning(
                    $"[SaveManager] Main save unavailable or invalid ({mainFailure}). " +
                    "Player progress was recovered from the backup.");
            }
            else
            {
                Data = new SaveData();
                saveMigratedData = true;

                if (mainSaveExists || backupSaveExists)
                {
                    Debug.LogError(
                        $"[SaveManager] Main and backup saves are unavailable or invalid " +
                        $"(main: {mainFailure}; backup: {backupFailure}). A new default save will be created.");
                }
            }

            TryDeleteTemporarySave();

            if (Data.schemaVersion < 7)
            {
                // Only a legacy gift claimed today counts as Day 1 claimed.
                // Empty or older legacy dates begin the new sequence at Day 1 without granting coins.
                Data.dailyRewardDayIndex = Data.lastDailyGiftDateKey == GetDailyDateKey() ? 1 : 0;
                Data.schemaVersion = 7;
                saveMigratedData = true;
            }

            int clampedRewardDay = Mathf.Clamp(Data.dailyRewardDayIndex, 0, DailyRewardDayCount - 1);
            if (Data.dailyRewardDayIndex != clampedRewardDay)
            {
                Data.dailyRewardDayIndex = clampedRewardDay;
                saveMigratedData = true;
            }

            if (Data.themeOwnershipVersion < 1)
            {
                // The legacy default mask owned bit zero (Neon) implicitly. Under
                // the explicit shop rules Ocean is the sole starter theme, so the
                // old implicit bit is removed while every other purchased bit is
                // preserved.
                Data.unlockedThemeMask &= ~(1 << (int)ThemeType.Neon);
                Data.unlockedThemeMask |= 1 << (int)ThemeType.Ocean;
                Data.themeOwnershipVersion = 1;
                saveMigratedData = true;
            }
            else
            {
                int maskWithOcean = Data.unlockedThemeMask | (1 << (int)ThemeType.Ocean);
                if (Data.unlockedThemeMask != maskWithOcean)
                {
                    Data.unlockedThemeMask = maskWithOcean;
                    saveMigratedData = true;
                }
            }

            ThemeType savedTheme = (ThemeType)Mathf.Clamp(Data.selectedTheme, 0, ChromaPalette.ThemeCount - 1);
            if (!IsThemeUnlocked(savedTheme))
            {
                Data.selectedTheme = (int)ThemeType.Ocean;
                saveMigratedData = true;
            }

            string currentDailyAdDate = GetDailyDateKey();
            if (Data.lastDailyRewardedAdDate != currentDailyAdDate)
            {
                Data.lastDailyRewardedAdDate = currentDailyAdDate;
                Data.dailyRewardedAdCount = 0;
                saveMigratedData = true;
            }
            else
            {
                int clampedDailyAdCount = Mathf.Clamp(Data.dailyRewardedAdCount, 0, DailyRewardedAdLimit);
                if (Data.dailyRewardedAdCount != clampedDailyAdCount)
                {
                    Data.dailyRewardedAdCount = clampedDailyAdCount;
                    saveMigratedData = true;
                }
            }

            if (PlayerPrefs.HasKey("VibrationEnabled"))
            {
                Data.hapticsMuted = PlayerPrefs.GetInt("VibrationEnabled", 1) == 0;
            }
            else
            {
                PlayerPrefs.SetInt("VibrationEnabled", Data.hapticsMuted ? 0 : 1);
                PlayerPrefs.Save();
            }

            if (saveMigratedData)
            {
                Save();
            }
        }

        public void Save()
        {
            MarkSaveDirty();
            FlushPendingSaveImmediate();
        }

        public void RequestSave()
        {
            MarkSaveDirty();
            if (pendingSaveRoutine == null && isActiveAndEnabled)
            {
                pendingSaveRoutine = StartCoroutine(FlushPendingSaveRoutine());
            }
        }

        public void FlushPendingSaveImmediate()
        {
            if (pendingSaveRoutine != null)
            {
                StopCoroutine(pendingSaveRoutine);
                pendingSaveRoutine = null;
            }

            if (!pendingSaveDirty)
            {
                return;
            }

            pendingSaveDirty = false;
            WriteSaveToDisk();

            if (pendingSaveDirty && pendingSaveRoutine == null && isActiveAndEnabled)
            {
                pendingSaveRoutine = StartCoroutine(FlushPendingSaveRoutine());
            }
        }

        private void MarkSaveDirty()
        {
            float now = Time.realtimeSinceStartup;
            if (!pendingSaveDirty)
            {
                firstDirtyTime = now;
            }

            pendingSaveDirty = true;
            latestSaveRequestTime = now;
        }

        private IEnumerator FlushPendingSaveRoutine()
        {
            while (pendingSaveDirty)
            {
                float now = Time.realtimeSinceStartup;
                bool debounceElapsed = now - latestSaveRequestTime >= NormalSaveDebounceSeconds;
                bool maximumWindowElapsed = now - firstDirtyTime >= MaximumDirtySeconds;
                if (debounceElapsed || maximumWindowElapsed)
                {
                    break;
                }

                yield return null;
            }

            pendingSaveRoutine = null;
            FlushPendingSaveImmediate();
        }

        private void WriteSaveToDisk()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (dailyRewardsDebugMode)
            {
                dailyRewardsDebugSerializedState = JsonUtility.ToJson(Data, true);
                return;
            }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
#endif
            try
            {
                string json = JsonUtility.ToJson(Data, true);
                if (string.IsNullOrWhiteSpace(json))
                {
                    throw new InvalidDataException("Serialized save data was empty.");
                }

                File.WriteAllText(TemporarySavePath, json);

                if (!TryReadSaveFile(TemporarySavePath, out _, out string temporaryFailure))
                {
                    throw new InvalidDataException($"Temporary save validation failed: {temporaryFailure}");
                }

                bool mainSaveIsValid = TryReadSaveFile(SavePath, out _, out _);
                ReplaceMainSave(mainSaveIsValid);
                TryDeleteTemporarySave();
            }
            catch (Exception exception)
            {
                TryDeleteTemporarySave();
                Debug.LogError($"[SaveManager] Could not save player progress safely: {exception.Message}");
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            finally
            {
                stopwatch.Stop();
                LastFullSaveMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
            }
#endif
        }

        private void ReplaceMainSave(bool mainSaveIsValid)
        {
            if (!File.Exists(SavePath))
            {
                File.Move(TemporarySavePath, SavePath);
                return;
            }

            ReplaceFileAtomically(
                TemporarySavePath,
                SavePath,
                mainSaveIsValid ? BackupSavePath : null);
        }

        private void ReplaceFileAtomically(string sourcePath, string destinationPath, string backupPath)
        {
            try
            {
                File.Replace(sourcePath, destinationPath, backupPath);
            }
            catch (PlatformNotSupportedException)
            {
                ReplaceFileWithMoveFallback(sourcePath, destinationPath, backupPath);
            }
            catch (NotSupportedException)
            {
                ReplaceFileWithMoveFallback(sourcePath, destinationPath, backupPath);
            }
        }

        private void ReplaceFileWithMoveFallback(string sourcePath, string destinationPath, string backupPath)
        {
            bool validBackupCreated = false;
            if (!string.IsNullOrEmpty(backupPath))
            {
                File.Copy(destinationPath, backupPath, true);
                if (!TryReadSaveFile(backupPath, out _, out string backupFailure))
                {
                    throw new InvalidDataException($"Backup validation failed: {backupFailure}");
                }

                validBackupCreated = true;
            }

            try
            {
                File.Delete(destinationPath);
                File.Move(sourcePath, destinationPath);
            }
            catch
            {
                if (!File.Exists(destinationPath) && validBackupCreated)
                {
                    File.Copy(backupPath, destinationPath);
                }

                throw;
            }
        }

        private bool TryReadSaveFile(string path, out SaveData saveData, out string failure)
        {
            saveData = null;
            failure = null;

            if (!File.Exists(path))
            {
                failure = "file does not exist";
                return false;
            }

            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    failure = "file is empty";
                    return false;
                }

                string trimmedJson = json.Trim();
                if (trimmedJson.Length < 2 || trimmedJson[0] != '{' || trimmedJson[trimmedJson.Length - 1] != '}')
                {
                    failure = "JSON object is incomplete";
                    return false;
                }

                SaveData parsedData = JsonUtility.FromJson<SaveData>(json);
                if (parsedData == null)
                {
                    failure = "JSON did not contain save data";
                    return false;
                }

                saveData = parsedData;
                return true;
            }
            catch (Exception exception)
            {
                failure = exception.Message;
                return false;
            }
        }

        private void TryDeleteTemporarySave()
        {
            try
            {
                if (File.Exists(TemporarySavePath))
                {
                    File.Delete(TemporarySavePath);
                }
            }
            catch
            {
                // A stale temporary file is ignored and will be overwritten by the next save.
            }
        }

        public int GetHighScore(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.Blitz:
                    return Data.blitzHighScore;
                case GameMode.Daily:
                    return Data.dailyHighScore;
                default:
                    return Data.classicHighScore;
            }
        }

        public bool TrySetHighScore(GameMode mode, int score)
        {
            if (score <= GetHighScore(mode))
            {
                return false;
            }

            switch (mode)
            {
                case GameMode.Blitz:
                    Data.blitzHighScore = score;
                    break;
                case GameMode.Daily:
                    Data.dailyHighScore = score;
                    break;
                default:
                    Data.classicHighScore = score;
                    break;
            }

            return true;
        }

        public int RegisterGameOver(GameMode mode, int score)
        {
            bool newRecord = score > GetHighScore(mode) && score > 0;
            return RegisterGameOver(mode, score, newRecord);
        }

        public int RegisterGameOver(GameMode mode, int score, bool newRecordThisRun)
        {
            bool newRecord = newRecordThisRun && score > 0;
            TrySetHighScore(mode, score);
            Data.gamesPlayed++;
            Data.rankPoints += Mathf.Max(12, score / 8);
            int coinsEarned = CalculateGameOverCoins(mode, score, newRecord);
            Data.coins = Mathf.Max(0, Data.coins) + coinsEarned;
            Data.gameOversSinceInterstitial++;

            if (mode == GameMode.Daily)
            {
                EnsureDailyState();
                Data.dailyBestScore = Mathf.Max(Data.dailyBestScore, score);
            }

            RequestSave();
            return coinsEarned;
        }

        public void RegisterMoveStats(ClearResult result, int chain, int scoreAdded)
        {
            Data.totalMoves++;
            Data.bestChain = Mathf.Max(Data.bestChain, chain);
            Data.bestMoveScore = Mathf.Max(Data.bestMoveScore, Mathf.Max(0, scoreAdded));

            if (result != null)
            {
                Data.totalLinesCleared += Mathf.Max(0, result.linesCleared);
                Data.totalPureLines += Mathf.Max(0, result.pureLines);
            }
        }

        public void RegisterPopStats(int poppedCells)
        {
            if (poppedCells <= 0)
            {
                return;
            }

            Data.totalPops++;
            Data.totalPopCells += poppedCells;
        }

        public int RegisterDailyQuestMove(ClearResult result, int score, out string completedQuestName)
        {
            completedQuestName = string.Empty;
            EnsureDailyState();

            int totalCoins = 0;
            bool changed = false;
            for (int i = 0; i < Data.dailyQuests.Length; i++)
            {
                DailyQuestState quest = Data.dailyQuests[i];
                int before = quest.progress;
                switch (quest.type)
                {
                    case DailyQuestType.PlayMoves:
                        quest.progress++;
                        break;
                    case DailyQuestType.ClearLines:
                        quest.progress += result == null ? 0 : Mathf.Max(0, result.linesCleared);
                        break;
                    case DailyQuestType.MakePure:
                        quest.progress += result == null ? 0 : Mathf.Max(0, result.pureLines);
                        break;
                    case DailyQuestType.ScorePoints:
                        quest.progress = Mathf.Max(quest.progress, score);
                        break;
                }

                quest.progress = Mathf.Clamp(quest.progress, 0, quest.target);
                changed |= before != quest.progress;
                totalCoins += TryClaimCompletedDailyQuest(quest, ref completedQuestName);
            }

            if (changed || totalCoins > 0)
            {
                RequestSave();
            }

            return totalCoins;
        }

        public int RegisterDailyQuestPop(int poppedCells, int score, out string completedQuestName)
        {
            completedQuestName = string.Empty;
            EnsureDailyState();

            int totalCoins = 0;
            bool changed = false;
            for (int i = 0; i < Data.dailyQuests.Length; i++)
            {
                DailyQuestState quest = Data.dailyQuests[i];
                int before = quest.progress;
                if (quest.type == DailyQuestType.UsePop && poppedCells > 0)
                {
                    quest.progress++;
                }
                else if (quest.type == DailyQuestType.ScorePoints)
                {
                    quest.progress = Mathf.Max(quest.progress, score);
                }

                quest.progress = Mathf.Clamp(quest.progress, 0, quest.target);
                changed |= before != quest.progress;
                totalCoins += TryClaimCompletedDailyQuest(quest, ref completedQuestName);
            }

            if (changed || totalCoins > 0)
            {
                RequestSave();
            }

            return totalCoins;
        }

        public string GetDailyQuestSummary()
        {
            EnsureDailyState();
            if (Data.dailyQuests == null || Data.dailyQuests.Length == 0)
            {
                return "OBIECTIVE: se pregatesc";
            }

            string[] parts = new string[Data.dailyQuests.Length];
            for (int i = 0; i < Data.dailyQuests.Length; i++)
            {
                DailyQuestState quest = Data.dailyQuests[i];
                string status = quest.claimed ? "OK" : $"{Mathf.Min(quest.progress, quest.target)}/{quest.target}";
                parts[i] = $"{GetDailyQuestShortLabel(quest.type)} {status}";
            }

            return "OBIECTIVE: " + string.Join("   ", parts);
        }

        public void AddCoins(int amount)
        {
            int coinsToAdd = Mathf.Max(0, amount);
            if (coinsToAdd <= 0)
            {
                return;
            }

            AddCoinsToBalance(coinsToAdd);
            Save();
        }

        private void AddCoinsToBalance(int amount)
        {
            Data.coins = Mathf.Max(0, Data.coins) + Mathf.Max(0, amount);
        }

        public bool HasClassicRun()
        {
            ClassicRunState run = Data.classicRun;
            return run != null
                && run.active
                && run.board != null
                && run.score != null
                && run.trayPieces != null;
        }

        public ClassicRunState GetClassicRun()
        {
            return HasClassicRun() ? Data.classicRun : null;
        }

        public void SaveClassicRun(ClassicRunState state)
        {
            if (state == null)
            {
                return;
            }

            state.active = true;
            state.savedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Data.classicRun = state;
            RequestSave();
        }

        public void ClearClassicRun()
        {
            ClearClassicRunInternal(false);
        }

        public void ClearClassicRunDeferred()
        {
            ClearClassicRunInternal(true);
        }

        private void ClearClassicRunInternal(bool deferDiskWrite)
        {
            if (Data.classicRun == null)
            {
                return;
            }

            Data.classicRun = null;
            if (deferDiskWrite)
            {
                RequestSave();
            }
            else
            {
                Save();
            }
        }

        public void SetMuted(bool muted)
        {
            Data.soundMuted = muted;
            Save();
        }

        public void SetHapticsMuted(bool muted)
        {
            Data.hapticsMuted = muted;
            PlayerPrefs.SetInt("VibrationEnabled", muted ? 0 : 1);
            PlayerPrefs.Save();
            Save();
        }

        public void SetPerformanceMode(int mode)
        {
            Data.performanceMode = Mathf.Clamp(mode, MobilePerformance.PerformanceAuto, MobilePerformance.PerformanceEco);
            Save();
            MobilePerformance.ApplyDefaults();
        }

        public int CyclePerformanceMode()
        {
            int next = Data.performanceMode + 1;
            if (next > MobilePerformance.PerformanceEco)
            {
                next = MobilePerformance.PerformanceAuto;
            }

            SetPerformanceMode(next);
            return next;
        }

        public void SetRemoveAds(bool removeAds)
        {
            Data.removeAds = removeAds;
            Save();
        }

        public void SetCosmeticPackOwned(bool owned)
        {
            Data.cosmeticPackOwned = owned;
            Save();
        }

        public bool CanShowInterstitial(float cooldownSeconds, int gameOverInterval)
        {
            if (Data.removeAds || Data.gameOversSinceInterstitial < gameOverInterval)
            {
                return false;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return now - Data.lastInterstitialUnix >= cooldownSeconds;
        }

        public void MarkInterstitialShown()
        {
            Data.gameOversSinceInterstitial = 0;
            Data.lastInterstitialUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Save();
        }

        public void MarkTutorialSeen()
        {
            Data.tutorialSeen = true;
            Save();
        }

        public void ResetTutorial()
        {
            Data.tutorialSeen = false;
            Save();
        }

        public void SetTheme(int themeIndex)
        {
            if (themeIndex < 0 || themeIndex >= ChromaPalette.ThemeCount)
            {
                return;
            }

            ThemeType theme = (ThemeType)themeIndex;
            if (!IsThemeUnlocked(theme))
            {
                Debug.LogWarning($"[Themes] Refused to apply unowned theme {theme}.");
                return;
            }

            Data.selectedTheme = themeIndex;
            Save();
            ThemeCatalog.NotifyThemeChanged();
            RefreshThemeBackdrops();
        }

        public bool IsThemeUnlocked(ThemeType theme)
        {
            int themeIndex = Mathf.Clamp((int)theme, 0, ChromaPalette.ThemeCount - 1);
            int mask = Data.unlockedThemeMask | (1 << (int)ThemeType.Ocean);
            bool explicitlyOwned = (mask & (1 << themeIndex)) != 0;

            switch (theme)
            {
                case ThemeType.Ocean:
                case ThemeType.Crystal:
                case ThemeType.Neon:
                case ThemeType.Gold:
                case ThemeType.Candy:
                case ThemeType.Aqua:
                    return explicitlyOwned;
                default:
                    return explicitlyOwned || ChromaPalette.IsThemeUnlocked(theme, Data.rankPoints, Data.cosmeticPackOwned);
            }
        }

        public ThemeType GetNextLockedTheme()
        {
            for (int i = 0; i < ChromaPalette.ThemeCount; i++)
            {
                ThemeType theme = (ThemeType)i;
                if (!IsThemeUnlocked(theme))
                {
                    return theme;
                }
            }

            return ThemeType.Neon;
        }

        public bool CanBuyTheme(ThemeType theme)
        {
            if (theme == ThemeType.Ocean || theme == ThemeType.Aqua || IsThemeUnlocked(theme))
            {
                return false;
            }

            int cost = ChromaPalette.GetThemeCoinCost(theme);
            return cost > 0 && GetCoins() >= cost;
        }

        public bool TryBuyTheme(ThemeType theme)
        {
            if (!CanBuyTheme(theme))
            {
                return false;
            }

            int themeIndex = Mathf.Clamp((int)theme, 0, ChromaPalette.ThemeCount - 1);
            Data.coins = Mathf.Max(0, Data.coins - ChromaPalette.GetThemeCoinCost(theme));
            int existingMask = Data.unlockedThemeMask | (1 << (int)ThemeType.Ocean);
            Data.unlockedThemeMask = existingMask | (1 << themeIndex);
            Save();
            return true;
        }

        private void UnlockThemeWithoutSaving(ThemeType theme)
        {
            int themeIndex = Mathf.Clamp((int)theme, 0, ChromaPalette.ThemeCount - 1);
            Data.unlockedThemeMask |= 1 << themeIndex;
        }

        private void RefreshThemeBackdrops()
        {
            NeonBackdrop[] backdrops = FindObjectsByType<NeonBackdrop>(FindObjectsInactive.Include);
            for (int i = 0; i < backdrops.Length; i++)
            {
                if (backdrops[i] != null)
                {
                    backdrops[i].Apply();
                }
            }
        }

        public bool IsAchievementUnlocked(AchievementId achievement)
        {
            long bit = 1L << (int)achievement;
            return (Data.achievementMask & bit) != 0;
        }

        public int GetAchievementCount()
        {
            return AchievementSystem.CountUnlocked(Data.achievementMask);
        }

        public bool TryUnlockAchievement(AchievementId achievement, out AchievementReward reward)
        {
            reward = AchievementSystem.Get(achievement);
            if (IsAchievementUnlocked(achievement))
            {
                return false;
            }

            Data.achievementMask |= 1L << (int)achievement;
            Data.coins = Mathf.Max(0, Data.coins) + Mathf.Max(0, reward.coins);
            RequestSave();
            return true;
        }

        public void RegisterDailyAttempt()
        {
            EnsureDailyState();

            string today = GetDailyDateKey();
            if (Data.lastDailyPlayedDateKey != today)
            {
                string yesterday = DateTime.Now.AddDays(-1).ToString("yyyyMMdd");
                Data.dailyStreak = Data.lastDailyPlayedDateKey == yesterday
                    ? Mathf.Max(0, Data.dailyStreak) + 1
                    : 1;
                Data.lastDailyPlayedDateKey = today;
            }

            Data.dailyAttempts++;
            Save();
        }

        public bool CanClaimDailyGift()
        {
            return Data.lastDailyGiftDateKey != GetDailyDateKey();
        }

        public int GetDailyGiftAmount()
        {
            return GetDailyRewardAmount(GetDailyRewardDayIndex());
        }

        public int GetDailyRewardAmount(int dayIndex)
        {
            return GetDailyRewardAmountForDay(dayIndex);
        }

        public static int GetDailyRewardAmountForDay(int dayIndex)
        {
            int index = Mathf.Clamp(dayIndex, 0, DailyRewardDayCount - 1);
            return DailyRewardCoins[index];
        }

        public int GetDailyRewardDayIndex()
        {
            string today = GetDailyDateKey();
            int nextDayIndex = Mathf.Clamp(Data.dailyRewardDayIndex, 0, DailyRewardDayCount - 1);

            if (Data.lastDailyGiftDateKey == today)
            {
                return (nextDayIndex + DailyRewardDayCount - 1) % DailyRewardDayCount;
            }

            return IsPreviousCalendarDay(Data.lastDailyGiftDateKey, today) ? nextDayIndex : 0;
        }

        public bool TryClaimDailyGift(out int coinsClaimed)
        {
            coinsClaimed = 0;
            string today = GetDailyDateKey();
            if (Data.lastDailyGiftDateKey == today)
            {
                return false;
            }

            int claimedDayIndex = GetDailyRewardDayIndex();
            coinsClaimed = GetDailyRewardAmount(claimedDayIndex);
            Data.lastDailyGiftDateKey = today;
            Data.dailyRewardDayIndex = (claimedDayIndex + 1) % DailyRewardDayCount;
            AddCoinsToBalance(coinsClaimed);
            if (claimedDayIndex == DailyRewardDayCount - 1)
            {
                UnlockThemeWithoutSaving(ThemeType.Aqua);
            }
            Save();
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugPrepareDailyRewardsValidationState()
        {
            dailyRewardsDebugMode = false;
            dailyRewardsProductionSnapshot = null;
            dailyRewardsDebugSerializedState = null;
            Data = new SaveData
            {
                schemaVersion = 7,
                selectedTheme = (int)ThemeType.Ocean,
                unlockedThemeMask = 1 << (int)ThemeType.Ocean,
                themeOwnershipVersion = 1
            };
        }

        public void DebugConfigureDailyRewardDay(int dayNumber)
        {
            int dayIndex = Mathf.Clamp(dayNumber - 1, 0, DailyRewardDayCount - 1);
            if (!dailyRewardsDebugMode)
            {
                dailyRewardsProductionSnapshot = JsonUtility.ToJson(Data, true);
                dailyRewardsDebugMode = true;
            }

            SaveData cleanDebugData = JsonUtility.FromJson<SaveData>(dailyRewardsProductionSnapshot);
            Data = cleanDebugData ?? new SaveData();

            // A valid consecutive previous day makes the requested day claimable
            // without changing the production calendar or reward table.
            Data.lastDailyGiftDateKey = DateTime.Now.AddDays(-1).ToString("yyyyMMdd");
            Data.dailyRewardDayIndex = dayIndex;

            // Make the Day 7 unlock assertion deterministic inside the isolated
            // test copy, even when the real player already owns Beach.
            Data.unlockedThemeMask |= 1 << (int)ThemeType.Ocean;
            Data.unlockedThemeMask &= ~(1 << (int)ThemeType.Aqua);
            Data.selectedTheme = (int)ThemeType.Ocean;
            Save();

            Debug.Log($"[Daily Rewards Debug] Simulating Day {dayNumber}. Production save is untouched.");
        }

        public void DebugReloadDailyRewardSimulation()
        {
            if (!dailyRewardsDebugMode)
            {
                Debug.LogWarning("[Daily Rewards Debug] No simulation is active.");
                return;
            }

            Load();
        }

        public void DebugEndDailyRewardSimulation()
        {
            if (!dailyRewardsDebugMode)
            {
                return;
            }

            SaveData productionData = JsonUtility.FromJson<SaveData>(dailyRewardsProductionSnapshot);
            if (productionData != null)
            {
                Data = productionData;
            }

            dailyRewardsDebugMode = false;
            dailyRewardsProductionSnapshot = null;
            dailyRewardsDebugSerializedState = null;
            Debug.Log("[Daily Rewards Debug] Simulation stopped. Production save restored in memory.");
        }

        public bool DebugRunDailyRewardsValidation(out string report)
        {
            System.Text.StringBuilder results = new System.Text.StringBuilder();
            bool allPassed = true;

            try
            {
                for (int dayNumber = 1; dayNumber <= DailyRewardDayCount; dayNumber++)
                {
                    DebugConfigureDailyRewardDay(dayNumber);
                    int dayIndex = dayNumber - 1;
                    int startingCoins = GetCoins();
                    int expectedReward = GetDailyRewardAmountForDay(dayIndex);
                    ThemeType startingTheme = (ThemeType)Data.selectedTheme;

                    bool statePatternValid = CanClaimDailyGift() && GetDailyRewardDayIndex() == dayIndex;
                    for (int cardIndex = 0; cardIndex < DailyRewardDayCount; cardIndex++)
                    {
                        bool shouldBeClaimed = cardIndex < dayIndex;
                        bool shouldBeClaim = cardIndex == dayIndex;
                        bool shouldBeLocked = cardIndex > dayIndex;
                        int stateCount = (shouldBeClaimed ? 1 : 0) + (shouldBeClaim ? 1 : 0) + (shouldBeLocked ? 1 : 0);
                        statePatternValid &= stateCount == 1;
                    }

                    bool firstClaim = TryClaimDailyGift(out int claimedCoins);
                    int coinsAfterClaim = GetCoins();
                    bool beachUnlocked = IsThemeUnlocked(ThemeType.Aqua);
                    bool activeThemeUnchanged = (ThemeType)Data.selectedTheme == startingTheme;
                    bool duplicateClaim = TryClaimDailyGift(out int duplicateCoins);
                    bool duplicateBlocked = !duplicateClaim && duplicateCoins == 0 && GetCoins() == coinsAfterClaim;

                    DebugReloadDailyRewardSimulation();
                    bool persisted = GetCoins() == coinsAfterClaim
                        && !CanClaimDailyGift()
                        && IsThemeUnlocked(ThemeType.Aqua) == beachUnlocked
                        && (ThemeType)Data.selectedTheme == startingTheme;

                    bool rewardValid = firstClaim
                        && claimedCoins == expectedReward
                        && coinsAfterClaim == startingCoins + expectedReward;
                    bool beachValid = dayNumber == DailyRewardDayCount ? beachUnlocked : !beachUnlocked;
                    bool dayPassed = statePatternValid
                        && rewardValid
                        && beachValid
                        && activeThemeUnchanged
                        && duplicateBlocked
                        && persisted;

                    allPassed &= dayPassed;
                    results.AppendLine(
                        $"Day {dayNumber}: {(dayPassed ? "PASS" : "FAIL")} | "
                        + $"states={(statePatternValid ? "ok" : "bad")}, reward={claimedCoins}/{expectedReward}, "
                        + $"duplicate={(duplicateBlocked ? "blocked" : "FAILED")}, "
                        + $"Beach={(beachUnlocked ? "owned" : "locked")}, autoApply={!activeThemeUnchanged}, "
                        + $"reload={(persisted ? "persisted" : "FAILED")}");
                }
            }
            finally
            {
                DebugEndDailyRewardSimulation();
            }

            report = results.ToString().TrimEnd();
            return allPassed;
        }
#endif

        public int GetDailyRewardedAdCount()
        {
            EnsureDailyRewardedAdState();
            return Data.dailyRewardedAdCount;
        }

        public bool CanClaimDailyRewardedAd()
        {
            return GetDailyRewardedAdCount() < DailyRewardedAdLimit;
        }

        public bool TryClaimDailyRewardedAd(out int coinsClaimed)
        {
            coinsClaimed = 0;
            EnsureDailyRewardedAdState();
            if (Data.dailyRewardedAdCount >= DailyRewardedAdLimit)
            {
                return false;
            }

            Data.dailyRewardedAdCount++;
            coinsClaimed = DailyRewardedAdCoins;
            AddCoinsToBalance(coinsClaimed);
            Save();
            return true;
        }

        private void EnsureDailyRewardedAdState()
        {
            string today = GetDailyDateKey();
            int clampedCount = Mathf.Clamp(Data.dailyRewardedAdCount, 0, DailyRewardedAdLimit);
            bool changed = Data.dailyRewardedAdCount != clampedCount;
            Data.dailyRewardedAdCount = clampedCount;

            if (Data.lastDailyRewardedAdDate != today)
            {
                Data.lastDailyRewardedAdDate = today;
                Data.dailyRewardedAdCount = 0;
                changed = true;
            }

            if (changed)
            {
                Save();
            }
        }

        private bool IsPreviousCalendarDay(string candidateDateKey, string currentDateKey)
        {
            if (!DateTime.TryParseExact(
                    candidateDateKey,
                    "yyyyMMdd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime candidateDate)
                || !DateTime.TryParseExact(
                    currentDateKey,
                    "yyyyMMdd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime currentDate))
            {
                return false;
            }

            return candidateDate.Date == currentDate.Date.AddDays(-1);
        }

        public bool TryClaimDailyMedalReward(int score, out DailyMedalInfo medal, out int coinsClaimed)
        {
            EnsureDailyState();
            medal = DailyMedalSystem.GetInfo(score);
            coinsClaimed = 0;

            if (medal.index <= 0 || medal.index <= Data.dailyClaimedMedalIndex)
            {
                return false;
            }

            int previousReward = DailyMedalSystem.GetRewardCoins(Data.dailyClaimedMedalIndex);
            int currentReward = DailyMedalSystem.GetRewardCoins(medal.index);
            coinsClaimed = Mathf.Max(0, currentReward - previousReward);
            Data.dailyClaimedMedalIndex = medal.index;
            Data.coins = Mathf.Max(0, Data.coins) + coinsClaimed;
            RequestSave();
            return coinsClaimed > 0;
        }

        public void EnsureDailyState()
        {
            string today = GetDailyDateKey();
            if (Data.dailyDateKey == today)
            {
                EnsureDailyQuests(today);
                return;
            }

            Data.dailyDateKey = today;
            Data.dailyBestScore = 0;
            Data.dailyAttempts = 0;
            Data.dailyClaimedMedalIndex = 0;
            GenerateDailyQuests(today);
            Save();
        }

        public string GetDailyDisplayDate()
        {
            return DateTime.Now.ToString("dd.MM");
        }

        public int GetDailyStreak()
        {
            return Mathf.Max(0, Data.dailyStreak);
        }

        public int GetCoins()
        {
            return Mathf.Max(0, Data.coins);
        }

        private int CalculateGameOverCoins(GameMode mode, int score, bool newRecord)
        {
            int coinsEarned = Mathf.Max(2, score / 260);
            if (newRecord)
            {
                coinsEarned += 12;
            }

            if (mode == GameMode.Daily)
            {
                coinsEarned += Mathf.Min(20, Mathf.Max(0, Data.dailyStreak) * 2);
            }
            else if (mode == GameMode.Blitz)
            {
                coinsEarned += 3;
            }

            return Mathf.Clamp(coinsEarned, 2, 140);
        }

        public string GetDailyDateKey()
        {
            return DateTime.Now.ToString("yyyyMMdd");
        }

        public int GetDailySeed()
        {
            return int.Parse(GetDailyDateKey()) ^ 0x5C0A;
        }

        private void EnsureDailyQuests(string today)
        {
            bool questsReady = Data.dailyQuestDateKey == today && Data.dailyQuests != null && Data.dailyQuests.Length == 3;
            if (questsReady)
            {
                for (int i = 0; i < Data.dailyQuests.Length; i++)
                {
                    if (Data.dailyQuests[i] == null)
                    {
                        questsReady = false;
                        break;
                    }
                }
            }

            if (questsReady)
            {
                return;
            }

            GenerateDailyQuests(today);
            Save();
        }

        private void GenerateDailyQuests(string today)
        {
            System.Random questRandom = new System.Random((int.Parse(today) ^ 0x24D7) + 37);
            DailyQuestType[] pool = new[]
            {
                DailyQuestType.ClearLines,
                DailyQuestType.PlayMoves,
                DailyQuestType.MakePure,
                DailyQuestType.UsePop,
                DailyQuestType.ScorePoints
            };

            for (int i = 0; i < pool.Length; i++)
            {
                int swap = questRandom.Next(i, pool.Length);
                DailyQuestType temp = pool[i];
                pool[i] = pool[swap];
                pool[swap] = temp;
            }

            Data.dailyQuestDateKey = today;
            Data.dailyQuests = new DailyQuestState[3];
            for (int i = 0; i < Data.dailyQuests.Length; i++)
            {
                Data.dailyQuests[i] = CreateDailyQuest(pool[i], i, questRandom);
            }
        }

        private DailyQuestState CreateDailyQuest(DailyQuestType type, int slot, System.Random questRandom)
        {
            DailyQuestState quest = new DailyQuestState
            {
                type = type,
                progress = 0,
                claimed = false
            };

            switch (type)
            {
                case DailyQuestType.PlayMoves:
                    quest.target = 18 + slot * 4;
                    quest.rewardCoins = 35 + slot * 8;
                    break;
                case DailyQuestType.ClearLines:
                    quest.target = 7 + slot * 2;
                    quest.rewardCoins = 45 + slot * 10;
                    break;
                case DailyQuestType.MakePure:
                    quest.target = 1 + (slot == 2 ? 1 : 0);
                    quest.rewardCoins = 55 + slot * 12;
                    break;
                case DailyQuestType.UsePop:
                    quest.target = 1 + slot;
                    quest.rewardCoins = 45 + slot * 12;
                    break;
                case DailyQuestType.ScorePoints:
                    quest.target = 2200 + slot * 900 + questRandom.Next(0, 3) * 250;
                    quest.rewardCoins = 45 + slot * 10;
                    break;
                default:
                    quest.target = 1;
                    quest.rewardCoins = 30;
                    break;
            }

            return quest;
        }

        private int TryClaimCompletedDailyQuest(DailyQuestState quest, ref string completedQuestName)
        {
            if (quest == null || quest.claimed || quest.progress < quest.target)
            {
                return 0;
            }

            quest.claimed = true;
            int reward = Mathf.Max(0, quest.rewardCoins);
            Data.coins = Mathf.Max(0, Data.coins) + reward;
            completedQuestName = string.IsNullOrEmpty(completedQuestName)
                ? GetDailyQuestLabel(quest.type)
                : "OBIECTIVE";
            return reward;
        }

        private string GetDailyQuestShortLabel(DailyQuestType type)
        {
            switch (type)
            {
                case DailyQuestType.PlayMoves:
                    return "MUTARI";
                case DailyQuestType.ClearLines:
                    return "LINII";
                case DailyQuestType.MakePure:
                    return "PURE";
                case DailyQuestType.UsePop:
                    return "POP";
                case DailyQuestType.ScorePoints:
                    return "SCOR";
                default:
                    return "TASK";
            }
        }

        private string GetDailyQuestLabel(DailyQuestType type)
        {
            switch (type)
            {
                case DailyQuestType.PlayMoves:
                    return "MUTARI ZILNICE";
                case DailyQuestType.ClearLines:
                    return "LINII ZILNICE";
                case DailyQuestType.MakePure:
                    return "PURE ZILNIC";
                case DailyQuestType.UsePop:
                    return "POP ZILNIC";
                case DailyQuestType.ScorePoints:
                    return "SCOR ZILNIC";
                default:
                    return "OBIECTIV ZILNIC";
            }
        }
    }
}
