using UnityEngine;

#if CHROMA_USE_UNITY_ANALYTICS
using Unity.Services.Analytics;
using Unity.Services.Core;
#endif

namespace ChromaBlast
{
    public class AnalyticsManager : MonoBehaviour
    {
        public static AnalyticsManager Instance { get; private set; }

        private bool initialized;

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

#if CHROMA_USE_UNITY_ANALYTICS
        private async void Start()
        {
            await UnityServices.InitializeAsync();
            AnalyticsService.Instance.StartDataCollection();
            initialized = true;
            RecordSessionStarted();
        }
#else
        private void Start()
        {
            initialized = true;
            RecordSessionStarted();
        }
#endif

        public void RecordSessionStarted()
        {
            Record("session_started");
        }

        public void RecordModeSelected(GameMode mode)
        {
            Record("mode_selected", ("mode", mode.ToString()));
        }

        public void RecordGameStarted(GameMode mode)
        {
            Record("game_started", ("mode", mode.ToString()));
        }

        public void RecordMove(GameMode mode, int score, int lines, int pureLines)
        {
            Record("move_played", ("mode", mode.ToString()), ("score", score), ("lines", lines), ("pure_lines", pureLines));
        }

        public void RecordPop(GameMode mode, ChromaColor color, int cells, int scoreAdded)
        {
            Record("pop_used", ("mode", mode.ToString()), ("color", color.ToString()), ("cells", cells), ("score_added", scoreAdded));
        }

        public void RecordGameOver(GameMode mode, int score, int chain, int coinsEarned)
        {
            Record("game_over", ("mode", mode.ToString()), ("score", score), ("chain", chain), ("coins_earned", coinsEarned));
        }

        public void RecordRewardedRequested(string placementContext)
        {
            Record("rewarded_requested", ("placement_context", placementContext));
        }

        public void RecordRewardedCompleted(string placementContext)
        {
            Record("rewarded_completed", ("placement_context", placementContext));
        }

        public void RecordPurchase(string productId)
        {
            Record("purchase_completed", ("product_id", productId));
        }

        public void RecordUndo()
        {
            Record("undo_used");
        }

        public void RecordMissionCompleted(GameMode mode, RoundMissionType missionType, int rewardScore)
        {
            Record("mission_completed", ("mode", mode.ToString()), ("mission", missionType.ToString()), ("reward_score", rewardScore));
        }

        public void RecordDailyAttempt(int streak, int attempts)
        {
            Record("daily_attempt", ("streak", streak), ("attempts", attempts));
        }

        public void RecordDailyGiftClaimed(int coins, int streak)
        {
            Record("daily_gift_claimed", ("coins", coins), ("streak", streak));
        }

        public void RecordDailyMedalClaimed(string medal, int score, int coins)
        {
            Record("daily_medal_claimed", ("medal", medal), ("score", score), ("coins", coins));
        }

        public void RecordDailyQuestClaimed(string quest, int coins)
        {
            Record("daily_quest_claimed", ("quest", quest), ("coins", coins));
        }

        public void RecordAchievementUnlocked(AchievementId achievement, int coins)
        {
            Record("achievement_unlocked", ("achievement", achievement.ToString()), ("coins", coins));
        }

        public void RecordScoreMilestone(GameMode mode, int milestoneScore, int coins)
        {
            Record("score_milestone", ("mode", mode.ToString()), ("milestone_score", milestoneScore), ("coins", coins));
        }

        public void RecordBoardSweep(GameMode mode, int scoreAdded, int emptyCells)
        {
            Record("board_sweep", ("mode", mode.ToString()), ("score_added", scoreAdded), ("empty_cells", emptyCells));
        }

        private void Record(string eventName, params (string key, object value)[] parameters)
        {
            if (!initialized)
            {
                return;
            }

#if CHROMA_USE_UNITY_ANALYTICS
            CustomEvent customEvent = new CustomEvent(eventName);
            for (int i = 0; i < parameters.Length; i++)
            {
                customEvent[parameters[i].key] = parameters[i].value;
            }

            AnalyticsService.Instance.RecordEvent(customEvent);
#else
            Debug.Log($"Analytics: {eventName}");
#endif
        }
    }
}
