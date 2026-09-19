using System;
using NexioCraft.Core;
using UnityEngine;

namespace NexioCraft.Chess
{
    public enum ChessMode { VsComputer = 0, PassAndPlay = 1 }

    /// <summary>Everything needed to resume a game: start position, moves, and who is playing.</summary>
    [Serializable]
    public sealed class ChessSave
    {
        public int mode;
        public int humanSide = Side.White;
        public int level = (int)ChessLevel.Medium;
        public string startFen = ChessPosition.StartFen;
        public string moves = "";
        public bool flipped;

        public ChessMode Mode => (ChessMode)mode;
        public ChessLevel Level => (ChessLevel)level;
    }

    public static class ChessPrefs
    {
        const string SaveKey = "chess.save";
        const string ThemeKey = "chess.theme";
        const string ShowMovesKey = "chess.showMoves";
        const string SideKey = "chess.setup.side";
        const string LevelKey = "chess.setup.level";
        const string PlayedKey = "chess.played";
        const string WinsKey = "chess.wins";
        const string LossesKey = "chess.losses";
        const string DrawsKey = "chess.draws";

        public static BoardTheme Theme
        {
            get => (BoardTheme)Mathf.Clamp(PlayerPrefs.GetInt(ThemeKey, 0), 0, 2);
            set => SetInt(ThemeKey, (int)value);
        }

        public static bool ShowLegalMoves
        {
            get => PlayerPrefs.GetInt(ShowMovesKey, 1) == 1;
            set => SetInt(ShowMovesKey, value ? 1 : 0);
        }

        /// <summary>Setup choice: 0 white, 1 random, 2 black.</summary>
        public static int SetupSide
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(SideKey, 0), 0, 2);
            set => SetInt(SideKey, value);
        }

        public static ChessLevel SetupLevel
        {
            get => (ChessLevel)Mathf.Clamp(PlayerPrefs.GetInt(LevelKey, (int)ChessLevel.Medium), 0, 2);
            set => SetInt(LevelKey, (int)value);
        }

        public static void SaveGame(ChessSave save)
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(save));
            PlayerPrefs.Save();
        }

        public static ChessSave LoadSave()
        {
            string json = PlayerPrefs.GetString(SaveKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var save = JsonUtility.FromJson<ChessSave>(json);
                if (save == null) return null;
                // Validate by replaying; a finished game is not resumable.
                var game = ChessGame.FromUci(save.startFen, save.moves);
                return game.IsOver ? null : save;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void ClearGame()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }

        public static int Played => PlayerPrefs.GetInt(PlayedKey, 0);
        public static int Wins => PlayerPrefs.GetInt(WinsKey, 0);
        public static int Losses => PlayerPrefs.GetInt(LossesKey, 0);
        public static int Draws => PlayerPrefs.GetInt(DrawsKey, 0);

        /// <summary>Records a finished game against the computer: +1 win, -1 loss, 0 draw.</summary>
        public static void RecordComputerGame(int outcome)
        {
            PlayerPrefs.SetInt(PlayedKey, Played + 1);
            if (outcome > 0) PlayerPrefs.SetInt(WinsKey, Wins + 1);
            else if (outcome < 0) PlayerPrefs.SetInt(LossesKey, Losses + 1);
            else PlayerPrefs.SetInt(DrawsKey, Draws + 1);
            PlayerPrefs.Save();
        }

        static void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
        }
    }

    static class ChessEntry
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => App.RegisterGame(new GameInfo
        {
            Id = "chess",
            Title = "Chess",
            Tagline = "Beat the computer, or a friend.",
            Order = 1,
            Accent = Palette.Hex(0xB58863),
            Artwork = () => ChessArt.AppIcon(300).ToTexture("ChessCard"),
            Status = () => ChessPrefs.LoadSave() != null ? "Game in progress"
                : ChessPrefs.Played > 0 ? $"{ChessPrefs.Wins}W  {ChessPrefs.Draws}D  {ChessPrefs.Losses}L" : "Beat the computer",
            ShowMenu = ui => ui.Show<ChessMenuScreen>()
        });
    }
}
