#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ChromaBlast.Editor
{
    public static class DailyRewardsDebugMenu
    {
        private const string MenuRoot = "Chroma Blast/Debug/Daily Rewards/";

        [MenuItem(MenuRoot + "Simulate Day 1", false, 1)]
        private static void SimulateDay1() => SimulateDay(1);

        [MenuItem(MenuRoot + "Simulate Day 2", false, 2)]
        private static void SimulateDay2() => SimulateDay(2);

        [MenuItem(MenuRoot + "Simulate Day 3", false, 3)]
        private static void SimulateDay3() => SimulateDay(3);

        [MenuItem(MenuRoot + "Simulate Day 4", false, 4)]
        private static void SimulateDay4() => SimulateDay(4);

        [MenuItem(MenuRoot + "Simulate Day 5", false, 5)]
        private static void SimulateDay5() => SimulateDay(5);

        [MenuItem(MenuRoot + "Simulate Day 6", false, 6)]
        private static void SimulateDay6() => SimulateDay(6);

        [MenuItem(MenuRoot + "Simulate Day 7", false, 7)]
        private static void SimulateDay7() => SimulateDay(7);

        [MenuItem(MenuRoot + "Run Full 7-Day Validation", false, 20)]
        private static void RunFullValidation()
        {
            if (!TryGetRuntime(out SaveManager save, out MenuUI menu))
            {
                return;
            }

            bool passed = save.DebugRunDailyRewardsValidation(out string report);
            menu.DebugRefreshDailyRewards();
            Debug.Log($"[Daily Rewards Debug] Full validation: {(passed ? "PASS" : "FAIL")}\n{report}");
        }

        // Headless entry point used by local Unity validation. It deliberately
        // starts from an in-memory SaveData instance, so no player save file is
        // read or written by the automated test.
        public static void RunBatchValidation()
        {
            GameObject host = new GameObject("DailyRewardsDebugValidationHost");
            try
            {
                SaveManager save = host.AddComponent<SaveManager>();
                save.DebugPrepareDailyRewardsValidationState();
                bool passed = save.DebugRunDailyRewardsValidation(out string report);
                Debug.Log($"[Daily Rewards Debug] Batch validation: {(passed ? "PASS" : "FAIL")}\n{report}");
                if (!passed)
                {
                    throw new System.InvalidOperationException("Daily Rewards 7-day validation failed. See the Unity log above.");
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [MenuItem(MenuRoot + "Stop Simulation / Restore Runtime State", false, 30)]
        private static void StopSimulation()
        {
            if (!TryGetRuntime(out SaveManager save, out MenuUI menu))
            {
                return;
            }

            save.DebugEndDailyRewardSimulation();
            menu.DebugRefreshDailyRewards();
        }

        [MenuItem(MenuRoot + "Simulate Day 1", true)]
        [MenuItem(MenuRoot + "Simulate Day 2", true)]
        [MenuItem(MenuRoot + "Simulate Day 3", true)]
        [MenuItem(MenuRoot + "Simulate Day 4", true)]
        [MenuItem(MenuRoot + "Simulate Day 5", true)]
        [MenuItem(MenuRoot + "Simulate Day 6", true)]
        [MenuItem(MenuRoot + "Simulate Day 7", true)]
        [MenuItem(MenuRoot + "Run Full 7-Day Validation", true)]
        [MenuItem(MenuRoot + "Stop Simulation / Restore Runtime State", true)]
        private static bool ValidatePlayModeCommands()
        {
            return EditorApplication.isPlaying;
        }

        private static void SimulateDay(int dayNumber)
        {
            if (!TryGetRuntime(out _, out MenuUI menu))
            {
                return;
            }

            menu.DebugShowDailyRewardsDay(dayNumber);
        }

        private static bool TryGetRuntime(out SaveManager save, out MenuUI menu)
        {
            save = null;
            menu = null;

            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Daily Rewards Debug] Enter Play Mode before using these commands.");
                return false;
            }

            save = Object.FindFirstObjectByType<SaveManager>(FindObjectsInactive.Include);
            menu = Object.FindFirstObjectByType<MenuUI>(FindObjectsInactive.Include);
            if (save == null || menu == null)
            {
                Debug.LogError("[Daily Rewards Debug] SaveManager or MenuUI was not found in Play Mode.");
                return false;
            }

            return true;
        }
    }
}
#endif
