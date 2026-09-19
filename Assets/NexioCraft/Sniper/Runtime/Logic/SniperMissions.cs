using System;
using System.Collections.Generic;
using UnityEngine;

namespace NexioCraft.Sniper
{
    public enum MissionGoal
    {
        /// <summary>Destroy every hostile robot.</summary>
        EliminateAll,
        /// <summary>Destroy the gold boss robot; its guards are optional.</summary>
        EliminateBoss
    }

    public sealed class MissionDef
    {
        public int Index;
        public int District;
        public string Title;
        public string Briefing;
        public MissionGoal Goal;
        /// <summary>Hostile robots, including the boss on a boss mission.</summary>
        public int Hostiles;
        public int Civilians;
        public float MinRange;
        public float MaxRange;
        public float WindSpeed;
        /// <summary>Compass direction the wind blows towards, in degrees (0 = +x, to the right of the nest).</summary>
        public float WindDirection;
        /// <summary>Fraction of robots that walk a patrol route instead of standing still.</summary>
        public float PatrolShare;
        public int Barrels;
        public float TimeLimit;
        public int Reward;
        public int Seed;

        public int Number => Index + 1;

        public Vector3 Wind
        {
            get
            {
                float radians = WindDirection * Mathf.Deg2Rad;
                return new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)) * WindSpeed;
            }
        }
    }

    /// <summary>The campaign: ten missions in each of three districts, getting longer, windier and busier.</summary>
    public static class SniperMissions
    {
        public const int PerDistrict = 10;
        public const int DistrictCount = 3;

        public static readonly string[] DistrictNames = { "Downtown", "Harbour", "Neon City" };

        static readonly string[] titles =
        {
            "First Contact", "Rooftop Watch", "Crossing Guard", "Lunch Break", "Parking Lot",
            "Double Trouble", "The Courier", "Street Sweep", "Bank Job", "The Foreman",
            "Dock Patrol", "Crane Operator", "Cargo Check", "Fish Market", "Low Tide",
            "Container Maze", "Lighthouse", "Smugglers", "Sunset Run", "Harbour Master",
            "Neon Nights", "Club Door", "Night Market", "Signal Jam", "The Broker",
            "Rain Check", "Alley Cats", "Blackout", "Last Train", "The Overlord"
        };

        static MissionDef[] all;

        public static IReadOnlyList<MissionDef> All => all ??= Build();

        public static int Count => All.Count;

        public static MissionDef Get(int index) => All[Mathf.Clamp(index, 0, Count - 1)];

        static MissionDef[] Build()
        {
            var missions = new MissionDef[DistrictCount * PerDistrict];
            for (int i = 0; i < missions.Length; i++) missions[i] = Create(i);
            return missions;
        }

        static MissionDef Create(int index)
        {
            int district = index / PerDistrict;
            int local = index % PerDistrict;
            var rng = new System.Random(1000 + index * 7919);
            bool boss = local == PerDistrict - 1 || (district == 2 && local == 4);

            int hostiles = Math.Min(7, 1 + (local + 1) / 3 + district);
            if (boss) hostiles = Math.Max(hostiles, 3);
            int civilians = Math.Min(5, district + local / 3);

            float minRange = 45f + index * 4f;
            float maxRange = 105f + index * 6.2f;
            float wind = district == 0 ? local * 0.2f : district == 1 ? 1f + local * 0.3f : 2f + local * 0.4f;
            // Mostly crosswinds: those are the ones that push the bullet sideways.
            float direction = (rng.Next(2) == 0 ? 0f : 180f) + (float)(rng.NextDouble() * 50.0 - 25.0);

            var mission = new MissionDef
            {
                Index = index,
                District = district,
                Title = titles[index],
                Goal = boss ? MissionGoal.EliminateBoss : MissionGoal.EliminateAll,
                Hostiles = hostiles,
                Civilians = civilians,
                MinRange = minRange,
                MaxRange = maxRange,
                WindSpeed = Mathf.Round(wind * 10f) / 10f,
                WindDirection = Mathf.Repeat(direction, 360f),
                PatrolShare = Mathf.Min(0.8f, 0.1f * local + 0.2f * district),
                Barrels = district == 0 ? (local >= 6 ? 1 : 0) : 1 + local / 4,
                TimeLimit = 75f + hostiles * 12f + district * 10f,
                Reward = 80 + index * 15,
                Seed = 1000 + index * 7919
            };
            mission.Briefing = BriefingFor(mission);
            return mission;
        }

        static string BriefingFor(MissionDef m)
        {
            string civilians = m.Civilians == 0 ? "" : m.Civilians == 1 ? " One civilian bot is nearby: don't hit it." : $" {m.Civilians} civilian bots are nearby: don't hit them.";
            if (m.Goal == MissionGoal.EliminateBoss)
                return $"A gold-plated boss robot is on site with {m.Hostiles - 1} guards. Take the boss down before it gets away.{civilians}";
            if (m.Hostiles == 1)
                return $"Intel spotted a hostile robot with a red visor. Destroy it before it escapes.{civilians}";
            return $"Intel spotted {m.Hostiles} hostile robots with red visors. Destroy them all before they escape.{civilians}";
        }
    }
}
