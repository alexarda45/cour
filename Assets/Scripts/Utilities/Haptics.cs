using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ChromaBlast
{
    public static class Haptics
    {
        private const string VibrationEnabledKey = "VibrationEnabled";
        private const float AbsoluteMinimumInterval = 0.025f;

        private static float lastVibrateTime = float.NegativeInfinity;
        private static int lastPriority = -1;

        private enum ImpactStyle
        {
            Light = 0,
            Medium = 1,
            Heavy = 2,
            Soft = 3,
            Rigid = 4
        }

        private readonly struct HapticPattern
        {
            public readonly int DurationMs;
            public readonly int Amplitude;
            public readonly float Cooldown;
            public readonly int Priority;
            public readonly ImpactStyle IosStyle;
            public readonly float IosIntensity;
            public readonly bool HasAccent;
            public readonly int AccentDurationMs;
            public readonly int AccentAmplitude;

            public HapticPattern(
                int durationMs,
                int amplitude,
                float cooldown,
                int priority,
                ImpactStyle iosStyle,
                float iosIntensity,
                bool hasAccent = false,
                int accentDurationMs = 0,
                int accentAmplitude = 0)
            {
                DurationMs = durationMs;
                Amplitude = amplitude;
                Cooldown = cooldown;
                Priority = priority;
                IosStyle = iosStyle;
                IosIntensity = iosIntensity;
                HasAccent = hasAccent;
                AccentDurationMs = accentDurationMs;
                AccentAmplitude = accentAmplitude;
            }
        }

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

            if (!enabled)
            {
                CancelNativeHaptics();
            }

            if (SaveManager.Instance != null && SaveManager.Instance.Data.hapticsMuted == enabled)
            {
                SaveManager.Instance.SetHapticsMuted(!enabled);
            }
        }

        public static void Pickup()
        {
            Play(new HapticPattern(18, 35, 0.045f, 0, ImpactStyle.Light, 0.25f));
        }

        public static void Place()
        {
            Play(new HapticPattern(32, 75, 0.055f, 1, ImpactStyle.Soft, 0.50f));
        }

        public static void Invalid()
        {
            Play(new HapticPattern(18, 90, 0.075f, 2, ImpactStyle.Rigid, 0.45f, true, 12, 55));
        }

        public static void Clear(int lines, int chain = 0)
        {
            bool comboAccent = chain >= 2;
            if (lines >= 2)
            {
                Play(new HapticPattern(
                    58,
                    190,
                    0.105f,
                    4,
                    ImpactStyle.Heavy,
                    0.82f,
                    comboAccent,
                    16,
                    90));
                return;
            }

            Play(new HapticPattern(
                42,
                135,
                0.085f,
                3,
                ImpactStyle.Medium,
                0.68f,
                comboAccent,
                14,
                70));
        }

        public static void Combo(bool strong = false)
        {
            Play(new HapticPattern(
                strong ? 18 : 14,
                strong ? 90 : 65,
                0.10f,
                2,
                ImpactStyle.Light,
                strong ? 0.42f : 0.30f));
        }

        public static void Pure()
        {
            Play(new HapticPattern(66, 225, 0.12f, 5, ImpactStyle.Rigid, 0.90f));
        }

        public static void Pop()
        {
            Play(new HapticPattern(72, 255, 0.14f, 6, ImpactStyle.Heavy, 1.0f));
        }

        // Preserve the established public API for menu, reward, and other non-gameplay feedback.
        public static void Light()
        {
            Play(new HapticPattern(22, 45, 0.08f, 0, ImpactStyle.Light, 0.32f));
        }

        public static void Medium()
        {
            Play(new HapticPattern(42, 125, 0.14f, 2, ImpactStyle.Medium, 0.62f));
        }

        public static void Heavy()
        {
            Play(new HapticPattern(62, 205, 0.22f, 4, ImpactStyle.Heavy, 0.86f));
        }

        private static void Play(HapticPattern pattern)
        {
            if (!IsEnabled())
            {
                return;
            }

            float now = Time.unscaledTime;
            float elapsed = now - lastVibrateTime;
            if (elapsed < AbsoluteMinimumInterval)
            {
                return;
            }

            if (elapsed < pattern.Cooldown && pattern.Priority <= lastPriority)
            {
                return;
            }

            lastVibrateTime = now;
            lastPriority = pattern.Priority;

#if UNITY_ANDROID && !UNITY_EDITOR
            PlayAndroid(pattern);
#elif UNITY_IOS && !UNITY_EDITOR
            PlayIos(pattern);
#endif
        }

#if UNITY_ANDROID
        private static void PlayAndroid(HapticPattern pattern)
        {
            try
            {
                using AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using AndroidJavaObject vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                if (vibrator == null || !vibrator.Call<bool>("hasVibrator"))
                {
                    return;
                }

                using AndroidJavaClass version = new AndroidJavaClass("android.os.Build$VERSION");
                int sdk = version.GetStatic<int>("SDK_INT");
                if (sdk >= 26)
                {
                    using AndroidJavaClass vibrationEffect = new AndroidJavaClass("android.os.VibrationEffect");
                    using AndroidJavaObject effect = pattern.HasAccent
                        ? vibrationEffect.CallStatic<AndroidJavaObject>(
                            "createWaveform",
                            new long[] { 0L, pattern.DurationMs, 28L, pattern.AccentDurationMs },
                            new int[] { 0, pattern.Amplitude, 0, pattern.AccentAmplitude },
                            -1)
                        : vibrationEffect.CallStatic<AndroidJavaObject>(
                            "createOneShot",
                            (long)pattern.DurationMs,
                            pattern.Amplitude);
                    vibrator.Call("vibrate", effect);
                }
                else if (pattern.HasAccent)
                {
                    vibrator.Call("vibrate", new long[] { 0L, pattern.DurationMs, 28L, pattern.AccentDurationMs }, -1);
                }
                else
                {
                    vibrator.Call("vibrate", (long)pattern.DurationMs);
                }
            }
            catch (Exception)
            {
                Handheld.Vibrate();
            }
        }
#endif

        private static void CancelNativeHaptics()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using AndroidJavaObject vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                vibrator?.Call("cancel");
            }
            catch (Exception)
            {
                // Unsupported devices simply have no active vibration to cancel.
            }
#elif UNITY_IOS && !UNITY_EDITOR
            try
            {
                CBHapticsCancel();
            }
            catch (Exception)
            {
                // Older/unsupported runtimes have no queued native accent to cancel.
            }
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void CBHapticsImpact(int style, float intensity, int accent);

        [DllImport("__Internal")]
        private static extern void CBHapticsCancel();

        private static void PlayIos(HapticPattern pattern)
        {
            try
            {
                CBHapticsImpact((int)pattern.IosStyle, pattern.IosIntensity, pattern.HasAccent ? 1 : 0);
            }
            catch (Exception)
            {
                Handheld.Vibrate();
            }
        }
#endif
    }
}
