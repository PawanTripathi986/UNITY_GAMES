using System;
using NexioCraft.Core;
using UnityEngine;

namespace NexioCraft.Sniper
{
    public enum MissionOutcome { Completed, TargetEscaped, CivilianHit, OutOfTime, Quit }

    /// <summary>What happened in one mission.</summary>
    public struct MissionStats
    {
        public MissionOutcome Outcome;
        public int ShotsFired;
        public int Hits;
        public int Kills;
        public int Headshots;
        public float TimeUsed;

        public bool Completed => Outcome == MissionOutcome.Completed;
        public bool NoMisses => ShotsFired > 0 && Hits >= ShotsFired;
        public bool AllHeadshots => Kills > 0 && Headshots >= Kills;
    }

    public static class MissionScoring
    {
        /// <summary>One star for finishing, one for never missing, one for every kill being a headshot.</summary>
        public static int Stars(MissionStats stats)
        {
            if (!stats.Completed) return 0;
            int stars = 1;
            if (stats.NoMisses) stars++;
            if (stats.AllHeadshots) stars++;
            return stars;
        }

        public static int Coins(MissionDef mission, MissionStats stats)
        {
            if (!stats.Completed) return 0;
            int stars = Stars(stats);
            return Mathf.RoundToInt(mission.Reward * (0.5f + 0.25f * stars)) + stats.Headshots * 10;
        }
    }

    [Serializable]
    public sealed class SniperSave
    {
        public int coins;
        /// <summary>Highest mission index the player may start.</summary>
        public int unlocked;
        public int[] stars;
        public int[] upgrades;
        /// <summary>UTC ticks after which the free-coins video can be watched again.</summary>
        public long freeCoinsReadyAt;
    }

    /// <summary>Coins, unlocked missions, stars and rifle upgrades. Pure logic over a <see cref="SniperSave"/>.</summary>
    public sealed class SniperProgress
    {
        public const int StartingCoins = 150;
        public const int FreeCoinsAmount = 150;
        public static readonly TimeSpan FreeCoinsCooldown = TimeSpan.FromMinutes(5);

        public SniperSave Data { get; }

        public SniperProgress(SniperSave data)
        {
            Data = Sanitize(data);
        }

        public static SniperSave NewSave() => new SniperSave
        {
            coins = StartingCoins,
            unlocked = 0,
            stars = new int[SniperMissions.Count],
            upgrades = new int[Rifle.StatCount]
        };

        /// <summary>Repairs anything missing or out of range, so an old or damaged save still loads.</summary>
        static SniperSave Sanitize(SniperSave data)
        {
            if (data == null) return NewSave();
            data.coins = Mathf.Max(0, data.coins);
            data.unlocked = Mathf.Clamp(data.unlocked, 0, SniperMissions.Count - 1);
            var stars = new int[SniperMissions.Count];
            if (data.stars != null)
                for (int i = 0; i < Mathf.Min(stars.Length, data.stars.Length); i++) stars[i] = Mathf.Clamp(data.stars[i], 0, 3);
            data.stars = stars;
            var upgrades = new int[Rifle.StatCount];
            if (data.upgrades != null)
                for (int i = 0; i < Mathf.Min(upgrades.Length, data.upgrades.Length); i++)
                    upgrades[i] = Mathf.Clamp(data.upgrades[i], 0, Rifle.MaxLevel((RifleStat)i));
            data.upgrades = upgrades;
            return data;
        }

        public int Coins => Data.coins;
        public int Unlocked => Data.unlocked;
        public int StarsFor(int mission) => mission >= 0 && mission < Data.stars.Length ? Data.stars[mission] : 0;
        public int Level(RifleStat stat) => Data.upgrades[(int)stat];
        public RifleStats CurrentRifle => Rifle.For(Data.upgrades);
        public bool IsUnlocked(int mission) => mission <= Data.unlocked;

        public int TotalStars
        {
            get
            {
                int total = 0;
                foreach (int s in Data.stars) total += s;
                return total;
            }
        }

        public void AddCoins(int amount) => Data.coins = Mathf.Max(0, Data.coins + amount);

        /// <summary>Records a finished mission: best stars, coins, and unlocks the next mission on success.</summary>
        public int RecordResult(MissionDef mission, MissionStats stats)
        {
            if (!stats.Completed) return 0;
            int stars = MissionScoring.Stars(stats);
            Data.stars[mission.Index] = Mathf.Max(Data.stars[mission.Index], stars);
            if (mission.Index + 1 < SniperMissions.Count) Data.unlocked = Mathf.Max(Data.unlocked, mission.Index + 1);
            int coins = MissionScoring.Coins(mission, stats);
            AddCoins(coins);
            return coins;
        }

        public int UpgradeCost(RifleStat stat) => Rifle.UpgradeCost(stat, Level(stat));

        public bool TryUpgrade(RifleStat stat)
        {
            int cost = UpgradeCost(stat);
            if (cost < 0 || Data.coins < cost) return false;
            Data.coins -= cost;
            Data.upgrades[(int)stat]++;
            return true;
        }

        public bool FreeCoinsReady(DateTime utcNow) => utcNow.Ticks >= Data.freeCoinsReadyAt;

        public TimeSpan FreeCoinsWait(DateTime utcNow) =>
            FreeCoinsReady(utcNow) ? TimeSpan.Zero : TimeSpan.FromTicks(Data.freeCoinsReadyAt - utcNow.Ticks);

        public void ClaimFreeCoins(DateTime utcNow)
        {
            AddCoins(FreeCoinsAmount);
            Data.freeCoinsReadyAt = (utcNow + FreeCoinsCooldown).Ticks;
        }
    }

    /// <summary>The player's saved progress (PlayerPrefs, JSON).</summary>
    public static class SniperPrefs
    {
        const string Key = "sniper.progress";

        static SniperProgress current;

        public static SniperProgress Progress => current ??= Load();

        static SniperProgress Load()
        {
            string json = PlayerPrefs.GetString(Key, string.Empty);
            SniperSave data = null;
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    data = JsonUtility.FromJson<SniperSave>(json);
                }
                catch (Exception)
                {
                    data = null;
                }
            }
            return new SniperProgress(data);
        }

        public static void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(Progress.Data));
            PlayerPrefs.Save();
        }
    }

    static class SniperEntry
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => App.RegisterGame(new GameInfo
        {
            Id = "sniper",
            Title = "Steel Sniper",
            Tagline = "Scope in and stop the rogue robots.",
            Order = 4,
            Accent = SniperArt.Accent,
            Artwork = () => SniperArt.AppIcon(300).ToTexture("SniperCard"),
            IsNew = true,
            Status = () => SniperPrefs.Progress.TotalStars > 0
                ? $"Mission {SniperPrefs.Progress.Unlocked + 1}, {SniperPrefs.Progress.TotalStars} stars"
                : "30 missions",
            ShowMenu = ui => ui.Show<SniperMenuScreen>()
        });
    }
}
