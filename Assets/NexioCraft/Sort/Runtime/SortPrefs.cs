using System;
using NexioCraft.Core;
using UnityEngine;

namespace NexioCraft.Sort
{
    [Serializable]
    public sealed class SortSave
    {
        public int level;
        public int tubes;
        public int[] cells;
        public int undosUsed;
        public int hintsUsed;
        public int extraTubes;
    }

    /// <summary>Current level and the board in progress.</summary>
    public static class SortPrefs
    {
        const string Key = "sort.save";
        const string BestKey = "sort.best";
        const string LevelKey = "sort.level";

        /// <summary>Highest level number the player has reached.</summary>
        public static int Best => PlayerPrefs.GetInt(BestKey, 1);

        /// <summary>The level to play next, kept even when no board is saved.</summary>
        public static int Level
        {
            get => PlayerPrefs.GetInt(LevelKey, 0);
            set
            {
                PlayerPrefs.SetInt(LevelKey, Mathf.Max(0, value));
                PlayerPrefs.SetInt(BestKey, Mathf.Max(Best, value + 1));
                PlayerPrefs.Save();
            }
        }

        public static SortSave Load()
        {
            string json = PlayerPrefs.GetString(Key, string.Empty);
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var save = JsonUtility.FromJson<SortSave>(json);
                if (save?.cells == null || save.tubes <= 0) return null;
                if (save.cells.Length != save.tubes * SortBoard.Capacity) return null;
                save.level = Mathf.Max(0, save.level);
                return save;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void Save(SortSave save)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(save));
            Level = save.level;
        }

        /// <summary>Forgets the board in progress (after a win); the level number stays.</summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }

    static class SortEntry
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => App.RegisterGame(new GameInfo
        {
            Id = "sort",
            Title = "Color Sort",
            Tagline = "Pour the colours until every tube matches.",
            Order = 5,
            Accent = SortArt.Accent,
            Artwork = () => SortArt.AppIcon(300).ToTexture("SortCard"),
            IsNew = true,
            Status = () => SortPrefs.Best > 1 ? $"Level {SortPrefs.Best}" : "Endless levels",
            ShowMenu = ui => ui.Show<SortGameScreen>()
        });
    }
}
