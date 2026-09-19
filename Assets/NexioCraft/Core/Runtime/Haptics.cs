using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace NexioCraft.Core
{
    public enum HapticKind { Selection = 0, Light = 1, Medium = 2, Heavy = 3, Success = 4, Warning = 5 }

    /// <summary>
    /// Short haptic taps. iOS uses UIFeedbackGenerator (Plugins/iOS/NativeHaptics.mm), Android uses
    /// VibrationEffect; the editor and other platforms do nothing.
    /// </summary>
    public static class Haptics
    {
        public static void Play(HapticKind kind)
        {
            if (!Settings.Vibration) return;
#if UNITY_IOS && !UNITY_EDITOR
            nx_haptic((int)kind);
#elif UNITY_ANDROID && !UNITY_EDITOR
            PlayAndroid(kind);
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void nx_haptic(int kind);
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject vibrator;
        static int sdkInt;
        static bool unavailable;

        static void PlayAndroid(HapticKind kind)
        {
            if (unavailable) return;
            try
            {
                if (vibrator == null)
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                        sdkInt = version.GetStatic<int>("SDK_INT");
                    if (vibrator == null || !vibrator.Call<bool>("hasVibrator"))
                    {
                        unavailable = true;
                        return;
                    }
                }

                long millis;
                int amplitude;
                switch (kind)
                {
                    case HapticKind.Selection: millis = 8; amplitude = 50; break;
                    case HapticKind.Light: millis = 12; amplitude = 90; break;
                    case HapticKind.Medium: millis = 22; amplitude = 150; break;
                    case HapticKind.Heavy: millis = 40; amplitude = 255; break;
                    case HapticKind.Success: millis = 30; amplitude = 180; break;
                    default: millis = 50; amplitude = 220; break;
                }

                if (sdkInt >= 26)
                {
                    using (var effects = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var effect = effects.CallStatic<AndroidJavaObject>("createOneShot", millis, amplitude))
                        vibrator.Call("vibrate", effect);
                }
                else if (kind == HapticKind.Heavy || kind == HapticKind.Warning)
                {
                    // Old devices have no amplitude control; only the strong cues are worth a buzz.
                    // This call is also what makes Unity add the VIBRATE permission to the manifest.
                    Handheld.Vibrate();
                }
            }
            catch (System.Exception e)
            {
                unavailable = true;
                Debug.LogWarning("Haptics unavailable: " + e.Message);
            }
        }
#endif
    }
}
