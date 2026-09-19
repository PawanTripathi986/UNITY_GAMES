using System;
using NexioCraft.Core;
using UnityEngine;

namespace NexioCraft.Ludo
{
    /// <summary>Ludo preferences, last setup choices, the in-progress game, and simple stats (PlayerPrefs).</summary>
    public static class LudoPrefs
    {
        const string RulesKey = "ludo.rules";
        const string SaveKey = "ludo.save";
        const string FastKey = "ludo.fast";
        const string AutoMoveKey = "ludo.autoMove";
        const string PlayedKey = "ludo.played";
        const string WinsKey = "ludo.wins";
        const string PlayersKey = "ludo.setup.players";
        const string ColorKey = "ludo.setup.color";
        const string LevelKey = "ludo.setup.level";
        const string PassPlayersKey = "ludo.setup.passPlayers";

        public static bool FastMode
        {
            get => PlayerPrefs.GetInt(FastKey, 0) == 1;
            set => SetInt(FastKey, value ? 1 : 0);
        }

        /// <summary>Move automatically when the roll allows only one real choice.</summary>
        public static bool AutoMove
        {
            get => PlayerPrefs.GetInt(AutoMoveKey, 1) == 1;
            set => SetInt(AutoMoveKey, value ? 1 : 0);
        }

        public static int SetupPlayers
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(PlayersKey, 4), 2, 4);
            set => SetInt(PlayersKey, value);
        }

        public static int SetupColor
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(ColorKey, 0), 0, 3);
            set => SetInt(ColorKey, value);
        }

        public static BotLevel SetupLevel
        {
            get => (BotLevel)Mathf.Clamp(PlayerPrefs.GetInt(LevelKey, (int)BotLevel.Normal), 0, 2);
            set => SetInt(LevelKey, (int)value);
        }

        public static int PassPlayers
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(PassPlayersKey, 2), 2, 4);
            set => SetInt(PassPlayersKey, value);
        }

        public static LudoRules LoadRules()
        {
            string json = PlayerPrefs.GetString(RulesKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return new LudoRules();
            try
            {
                return JsonUtility.FromJson<LudoRules>(json) ?? new LudoRules();
            }
            catch (Exception)
            {
                return new LudoRules();
            }
        }

        public static void SaveRules(LudoRules rules)
        {
            PlayerPrefs.SetString(RulesKey, JsonUtility.ToJson(rules));
            PlayerPrefs.Save();
        }

        public static bool HasSavedGame => LoadGame() != null;

        public static void SaveGame(LudoState state)
        {
            if (state == null || state.over)
            {
                ClearGame();
                return;
            }
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(state));
            PlayerPrefs.Save();
        }

        public static LudoState LoadGame()
        {
            string json = PlayerPrefs.GetString(SaveKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var state = JsonUtility.FromJson<LudoState>(json);
                return state != null && state.IsValid() && !state.over ? state : null;
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

        public static int GamesPlayed => PlayerPrefs.GetInt(PlayedKey, 0);
        public static int Wins => PlayerPrefs.GetInt(WinsKey, 0);

        public static void RecordGame(bool won)
        {
            PlayerPrefs.SetInt(PlayedKey, GamesPlayed + 1);
            if (won) PlayerPrefs.SetInt(WinsKey, Wins + 1);
            PlayerPrefs.Save();
        }

        static void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
        }
    }

    static class LudoEntry
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => App.RegisterGame(new GameInfo
        {
            Id = "ludo",
            Title = "Ludo",
            Tagline = "Roll, race and capture. 2 to 4 players.",
            Order = 0,
            Accent = LudoTheme.SeatColors[0],
            Artwork = () => LudoArt.Board(270),
            Status = () => LudoPrefs.HasSavedGame ? "Game in progress" : "2 to 4 players",
            ShowMenu = ui => ui.Show<LudoMenuScreen>()
        });
    }
}
