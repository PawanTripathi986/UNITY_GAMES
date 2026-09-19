using System;
using UnityEngine;

namespace NexioCraft.Sniper
{
    public enum RifleStat { Power = 0, Stability = 1, Zoom = 2, Reload = 3, Computer = 4 }

    /// <summary>The rifle's numbers for a set of upgrade levels.</summary>
    public readonly struct RifleStats
    {
        public readonly float Damage;
        /// <summary>Peak aim wobble in degrees while scoped, before holding breath.</summary>
        public readonly float Sway;
        public readonly float MaxZoom;
        /// <summary>Seconds to work the bolt between shots.</summary>
        public readonly float BoltTime;
        public readonly float MagazineReloadTime;
        public readonly int Magazine;
        /// <summary>Shows where the bullet will land, wind and drop included.</summary>
        public readonly bool BallisticComputer;

        public RifleStats(float damage, float sway, float maxZoom, float boltTime, float magazineReloadTime, int magazine, bool computer)
        {
            Damage = damage;
            Sway = sway;
            MaxZoom = maxZoom;
            BoltTime = boltTime;
            MagazineReloadTime = magazineReloadTime;
            Magazine = magazine;
            BallisticComputer = computer;
        }
    }

    public static class Rifle
    {
        public const int MagazineSize = 5;
        public const float MinZoom = 2f;

        /// <summary>Holding breath multiplies the sway by this.</summary>
        public const float BreathSteadiness = 0.14f;

        /// <summary>Damage multipliers by where the bullet lands.</summary>
        public const float HeadMultiplier = 3f;
        public const float LimbMultiplier = 0.6f;

        public static readonly int StatCount = Enum.GetValues(typeof(RifleStat)).Length;

        static readonly int[] maxLevels = { 5, 5, 5, 5, 1 };
        static readonly int[] baseCosts = { 150, 120, 140, 100, 1500 };

        public static readonly string[] Names = { "Power", "Stability", "Scope", "Bolt Action", "Ballistic Computer" };

        public static readonly string[] Descriptions =
        {
            "More damage: body shots drop robots in one hit",
            "Less scope wobble while you aim",
            "Stronger zoom for long shots",
            "Work the bolt and reload faster",
            "Shows where the bullet will land, wind included"
        };

        public static int MaxLevel(RifleStat stat) => maxLevels[(int)stat];

        /// <summary>Coins to go from <paramref name="level"/> to the next level, or -1 when maxed.</summary>
        public static int UpgradeCost(RifleStat stat, int level)
        {
            if (level < 0 || level >= MaxLevel(stat)) return -1;
            float cost = baseCosts[(int)stat] * Mathf.Pow(1.75f, level);
            return Mathf.RoundToInt(cost / 10f) * 10;
        }

        public static float Damage(int level) => 60f + level * 12f;
        // Tested on device: 0.42 made a steady-looking chest shot at 95 m miss about half the time on mission 1.
        public static float Sway(int level) => Mathf.Lerp(0.3f, 0.1f, level / 5f);
        public static float MaxZoom(int level) => 4f + level * 1.6f;
        public static float BoltTime(int level) => Mathf.Lerp(1.5f, 0.65f, level / 5f);
        public static float MagazineReloadTime(int level) => Mathf.Lerp(2.4f, 1.2f, level / 5f);

        public static RifleStats For(int[] levels)
        {
            int Level(RifleStat stat) => levels != null && levels.Length > (int)stat ? Mathf.Clamp(levels[(int)stat], 0, MaxLevel(stat)) : 0;
            int reload = Level(RifleStat.Reload);
            return new RifleStats(
                Damage(Level(RifleStat.Power)),
                Sway(Level(RifleStat.Stability)),
                MaxZoom(Level(RifleStat.Zoom)),
                BoltTime(reload),
                MagazineReloadTime(reload),
                MagazineSize,
                Level(RifleStat.Computer) > 0);
        }

        /// <summary>Short description of a stat's value at a level, for the upgrade screen.</summary>
        public static string ValueText(RifleStat stat, int level)
        {
            switch (stat)
            {
                case RifleStat.Power: return Mathf.RoundToInt(Damage(level)) + " dmg";
                case RifleStat.Stability: return Mathf.RoundToInt((1f - Sway(level) / Sway(0)) * 100f) + "% steadier";
                case RifleStat.Zoom: return MaxZoom(level).ToString("0.#") + "x";
                case RifleStat.Reload: return BoltTime(level).ToString("0.00") + " s";
                default: return level > 0 ? "Installed" : "Not installed";
            }
        }
    }
}
