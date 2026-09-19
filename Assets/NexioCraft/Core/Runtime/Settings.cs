using System;
using UnityEngine;

namespace NexioCraft.Core
{
    /// <summary>Device-wide player preferences shared by every game.</summary>
    public static class Settings
    {
        const string SoundKey = "nx.sound";
        const string VibrationKey = "nx.vibration";

        public static bool Sound { get; private set; } = true;
        public static bool Vibration { get; private set; } = true;

        public static event Action Changed;

        public static void Load()
        {
            Sound = PlayerPrefs.GetInt(SoundKey, 1) == 1;
            Vibration = PlayerPrefs.GetInt(VibrationKey, 1) == 1;
        }

        public static void SetSound(bool on)
        {
            Sound = on;
            PlayerPrefs.SetInt(SoundKey, on ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void SetVibration(bool on)
        {
            Vibration = on;
            PlayerPrefs.SetInt(VibrationKey, on ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
