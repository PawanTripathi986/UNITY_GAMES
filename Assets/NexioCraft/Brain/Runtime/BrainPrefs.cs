using NexioCraft.Core;
using UnityEngine;

namespace NexioCraft.Brain
{
    /// <summary>Best scores for the brain games. Lower is better for moves and times.</summary>
    public static class BrainPrefs
    {
        public static int BestScore(string game, string variant = "")
        {
            return PlayerPrefs.GetInt(Key(game, variant, "best"), 0);
        }

        /// <summary>Stores a score when it beats the record. Returns true if it was a new best.</summary>
        public static bool SubmitHighScore(string game, int score, string variant = "")
        {
            if (score <= BestScore(game, variant)) return false;
            PlayerPrefs.SetInt(Key(game, variant, "best"), score);
            PlayerPrefs.Save();
            return true;
        }

        public static int BestLowScore(string game, string variant = "")
        {
            return PlayerPrefs.GetInt(Key(game, variant, "low"), 0);
        }

        /// <summary>Stores a "lower is better" record such as moves or seconds.</summary>
        public static bool SubmitLowScore(string game, int score, string variant = "")
        {
            int current = BestLowScore(game, variant);
            if (current > 0 && score >= current) return false;
            PlayerPrefs.SetInt(Key(game, variant, "low"), score);
            PlayerPrefs.Save();
            return true;
        }

        public static int GetChoice(string game, int fallback = 0) => PlayerPrefs.GetInt(Key(game, "", "choice"), fallback);

        public static void SetChoice(string game, int value)
        {
            PlayerPrefs.SetInt(Key(game, "", "choice"), value);
            PlayerPrefs.Save();
        }

        public static string SaveData(string game) => PlayerPrefs.GetString(Key(game, "", "save"), string.Empty);

        public static void SetSaveData(string game, string data)
        {
            if (string.IsNullOrEmpty(data)) PlayerPrefs.DeleteKey(Key(game, "", "save"));
            else PlayerPrefs.SetString(Key(game, "", "save"), data);
            PlayerPrefs.Save();
        }

        static string Key(string game, string variant, string stat) =>
            string.IsNullOrEmpty(variant) ? $"brain.{game}.{stat}" : $"brain.{game}.{variant}.{stat}";
    }

    static class BrainEntry
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => App.RegisterGame(new GameInfo
        {
            Id = "brain",
            Title = "Brain Games",
            Tagline = "Memory, numbers, logic. Five quick games.",
            Order = 2,
            Accent = BrainArt.Accent,
            Artwork = () => BrainArt.AppIcon(300).ToTexture("BrainCard"),
            Status = () => BrainPrefs.BestScore("2048") > 0 ? $"2048 best {BrainPrefs.BestScore("2048"):N0}"
                : BrainPrefs.BestScore("maths") > 0 ? $"Maths best {BrainPrefs.BestScore("maths")}" : "5 mini games",
            ShowMenu = ui => ui.Show<BrainMenuScreen>()
        });
    }
}
