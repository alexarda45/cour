using UnityEngine;

namespace ChromaBlast
{
    public static class MobilePerformance
    {
        public const int PerformanceAuto = 0;
        public const int PerformanceMax = 1;
        public const int PerformanceEco = 2;

        public static bool LowEndMode { get; private set; }

        public static void ApplyDefaults()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            int memory = SystemInfo.systemMemorySize;
            int cores = SystemInfo.processorCount;
#if UNITY_ANDROID || UNITY_IOS
            bool autoLowEnd = (memory > 0 && memory <= 3072) || (cores > 0 && cores <= 4);
#else
            bool autoLowEnd = (memory > 0 && memory <= 4096) || (cores > 0 && cores <= 6);
#endif

            int mode = SaveManager.Instance == null
                ? PerformanceAuto
                : Mathf.Clamp(SaveManager.Instance.Data.performanceMode, PerformanceAuto, PerformanceEco);

            LowEndMode = mode == PerformanceEco || (mode == PerformanceAuto && autoLowEnd);
        }

        public static string GetModeName(int mode)
        {
            switch (Mathf.Clamp(mode, PerformanceAuto, PerformanceEco))
            {
                case PerformanceMax:
                    return "MAX";
                case PerformanceEco:
                    return "ECO";
                default:
                    return "AUTO";
            }
        }

        public static bool UseFullJuice()
        {
            return !LowEndMode;
        }
    }
}
