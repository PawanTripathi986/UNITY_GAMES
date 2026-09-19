using System;
using NexioCraft.Core;
using UnityEngine;

namespace NexioCraft.Blocks
{
    /// <summary>Saved board, tray and record for Block Puzzle.</summary>
    [Serializable]
    public sealed class BlocksSave
    {
        public int[] cells;
        public int score;
        public int lines;
        public int[] trayShapes;
        public int[] trayColours;
        public bool[] trayUsed;
    }

    public static class BlocksPrefs
    {
        const string SaveKey = "blocks.save";
        const string BestKey = "blocks.best";

        public static int Best => PlayerPrefs.GetInt(BestKey, 0);

        public static bool SubmitScore(int score)
        {
            if (score <= Best) return false;
            PlayerPrefs.SetInt(BestKey, score);
            PlayerPrefs.Save();
            return true;
        }

        public static BlocksSave Load()
        {
            string json = PlayerPrefs.GetString(SaveKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var save = JsonUtility.FromJson<BlocksSave>(json);
                if (save?.cells == null || save.cells.Length != BlockBoard.Size * BlockBoard.Size) return null;
                if (save.trayShapes == null || save.trayShapes.Length != 3) return null;
                foreach (int shape in save.trayShapes)
                    if (shape < 0 || shape >= BlockShapes.All.Length) return null;
                return save;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void Save(BlocksSave save)
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(save));
            PlayerPrefs.Save();
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }
    }

    static class BlocksEntry
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => App.RegisterGame(new GameInfo
        {
            Id = "blocks",
            Title = "Block Puzzle",
            Tagline = "Drop blocks, clear rows and columns.",
            Order = 3,
            Accent = BlocksArt.Colors[2],
            Artwork = () => BlocksArt.AppIcon(300).ToTexture("BlocksCard"),
            Status = () => BlocksPrefs.Best > 0 ? $"Best {BlocksPrefs.Best:N0}" : "8x8 board",
            ShowMenu = ui => ui.Show<BlockPuzzleScreen>()
        });
    }
}
