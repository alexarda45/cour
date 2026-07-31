using UnityEngine;

namespace ChromaBlast
{
    public static class Haptics
    {
        private const string VibrationEnabledKey = "VibrationEnabled";
        private static float lastVibrateTime;

        public static bool IsEnabled()
        {
            bool savedEnabled = SaveManager.Instance == null || !SaveManager.Instance.Data.hapticsMuted;
            return PlayerPrefs.GetInt(VibrationEnabledKey, savedEnabled ? 1 : 0) != 0
                && savedEnabled;
        }

        public static void SetEnabled(bool enabled)
        {
            PlayerPrefs.SetInt(VibrationEnabledKey, enabled ? 1 : 0);
            PlayerPrefs.Save();

            if (SaveManager.Instance != null && SaveManager.Instance.Data.hapticsMuted == enabled)
            {
                SaveManager.Instance.SetHapticsMuted(!enabled);
            }
        }

        public static void Light()
        {
            VibrateThrottled(0.08f);
        }

        public static void Medium()
        {
            VibrateThrottled(0.14f);
        }

        public static void Heavy()
        {
            VibrateThrottled(0.22f);
        }

        private static void VibrateThrottled(float cooldown)
        {
            if (!IsEnabled())
            {
                return;
            }

            if (Time.unscaledTime - lastVibrateTime < cooldown)
            {
                return;
            }

            lastVibrateTime = Time.unscaledTime;
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }
    }
}
