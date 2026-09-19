using System;
using System.Collections.Generic;
using System.Text;
using NexioCraft.Sniper;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace NexioCraft.EditorTools
{
    /// <summary>
    /// Steel Sniper: ballistics, missions, rifle economy, save repair, scoring, and (with real physics) that every
    /// district has visible robot spots and that aimed shots land where the scope says they will.
    ///   Unity -batchmode -projectPath . -executeMethod NexioCraft.EditorTools.SniperTests.CommandLine
    /// </summary>
    public static class SniperTests
    {
        [MenuItem("NexioCraft/Sniper/Run Tests")]
        public static void RunFromMenu()
        {
            bool ok = Run(out string report);
            Debug.Log(report);
            EditorUtility.DisplayDialog(ok ? "Sniper tests passed" : "Sniper tests FAILED", report, "OK");
        }

        public static void CommandLine()
        {
            bool ok;
            try
            {
                ok = Run(out string report);
                Debug.Log(report);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                ok = false;
            }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool Run(out string report)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            void Check(bool condition, string message)
            {
                if (!condition) failures.Add(message);
            }

            Ballistic(Check, log);
            Missions(Check, log);
            Economy(Check, log);
            Saves(Check, log);
            Scoring(Check, log);
            Worlds(Check, log);

            var summary = new StringBuilder();
            summary.AppendLine(failures.Count == 0 ? "SNIPER TESTS PASSED" : $"SNIPER TESTS FAILED ({failures.Count})");
            summary.Append(log);
            foreach (var failure in failures) summary.AppendLine("  FAIL: " + failure);
            report = summary.ToString();
            return failures.Count == 0;
        }

        // ------------------------------------------------------------------ ballistics

        static void Ballistic(Action<bool, string> check, StringBuilder log)
        {
            var p = Ballistics.Rifle;
            float zero = Ballistics.ZeroAngle(p);
            check(zero > 0f && zero < 0.01f, $"zero angle out of range: {zero}");

            var launch = Ballistics.LaunchVelocity(Vector3.forward, p);
            check(Mathf.Abs(launch.magnitude - p.MuzzleSpeed) < 0.01f, "launch speed must equal muzzle speed");
            check(Mathf.Abs(Mathf.Atan2(launch.y, launch.z) - zero) < 1e-5f, "barrel must tip up by the zero angle");

            // A level shot crosses the sight line at the zero distance and matches the exact trajectory.
            var path = new List<Vector3>();
            Ballistics.Trace(Vector3.zero, launch, Vector3.zero, p, null, path, out _, out _, Ballistics.ShotStep, 320f);
            float at100 = HeightAt(path, 100f), at250 = HeightAt(path, 250f);
            check(Mathf.Abs(at100) < 0.03f, $"bullet should cross the sight line at 100 m (was {at100:F3} m)");
            check(Mathf.Abs(at250 - Ballistics.OffsetAt(250f, p)) < 0.03f, $"stepped trajectory drifts from the exact one at 250 m ({at250:F3} vs {Ballistics.OffsetAt(250f, p):F3})");
            check(at250 < -0.3f, $"bullet should drop noticeably by 250 m (was {at250:F3} m)");

            float h50 = Ballistics.HoldoverDegrees(50f, p), h150 = Ballistics.HoldoverDegrees(150f, p);
            float h200 = Ballistics.HoldoverDegrees(200f, p), h300 = Ballistics.HoldoverDegrees(300f, p);
            check(h50 < 0f, "inside the zero the bullet is above the sight line (negative holdover)");
            check(h150 > 0f && h150 < h200 && h200 < h300, "holdover must grow with distance past the zero");

            // Wind pushes the shot the way it blows, in proportion to its speed.
            foreach (float wind in new[] { -4f, 2f, 5f })
            {
                Ballistics.Trace(Vector3.zero, launch, new Vector3(wind, 0f, 0f), p, null, path, out _, out _, Ballistics.ShotStep, 220f);
                float drift = SideAt(path, 200f);
                float expected = Ballistics.DriftAt(200f, wind, p);
                check(Mathf.Sign(drift) == Mathf.Sign(wind), $"wind {wind} pushed the bullet the wrong way");
                check(Mathf.Abs(drift - expected) < 0.05f, $"wind {wind}: drift {drift:F3} m, expected about {expected:F3} m");
            }

            // A wall at 150 m stops the bullet on it.
            bool hit = Ballistics.Trace(new Vector3(0f, 1f, 0f), launch, Vector3.zero, p, (Vector3 a, Vector3 b, out float f) =>
            {
                f = 0f;
                if (a.z > 150f || b.z < 150f) return false;
                f = (150f - a.z) / (b.z - a.z);
                return true;
            }, null, out var impact, out float flight);
            check(hit && Mathf.Abs(impact.z - 150f) < 0.01f, "trace must stop exactly on a wall at 150 m");
            check(Mathf.Abs(flight - 150f / p.MuzzleSpeed) < 0.01f, $"flight time to 150 m wrong ({flight:F3} s)");
            log.AppendLine($"  ballistics: drop at 250 m {at250:F2} m, holdover 150/200/300 m = {h150:F3}/{h200:F3}/{h300:F3} deg");
        }

        static float HeightAt(List<Vector3> path, float distance)
        {
            for (int i = 1; i < path.Count; i++)
                if (path[i].z >= distance)
                    return Mathf.Lerp(path[i - 1].y, path[i].y, Mathf.InverseLerp(path[i - 1].z, path[i].z, distance));
            return float.NaN;
        }

        static float SideAt(List<Vector3> path, float distance)
        {
            for (int i = 1; i < path.Count; i++)
                if (path[i].z >= distance)
                    return Mathf.Lerp(path[i - 1].x, path[i].x, Mathf.InverseLerp(path[i - 1].z, path[i].z, distance));
            return float.NaN;
        }

        // ------------------------------------------------------------------ missions

        static void Missions(Action<bool, string> check, StringBuilder log)
        {
            var all = SniperMissions.All;
            check(all.Count == SniperMissions.DistrictCount * SniperMissions.PerDistrict, "30 missions expected");
            var titles = new HashSet<string>();
            int bosses = 0;
            for (int i = 0; i < all.Count; i++)
            {
                var m = all[i];
                check(m.Index == i, $"mission {i} has index {m.Index}");
                check(m.District == i / SniperMissions.PerDistrict, $"mission {i} in the wrong district");
                check(titles.Add(m.Title), $"duplicate title {m.Title}");
                check(m.MinRange > 20f && m.MaxRange > m.MinRange + 30f, $"mission {i} range band too thin");
                check(m.Hostiles >= 1 && m.Hostiles <= 7, $"mission {i} hostile count {m.Hostiles}");
                check(m.WindSpeed >= 0f && m.TimeLimit >= 60f && m.Reward > 0, $"mission {i} numbers out of range");
                check(!string.IsNullOrEmpty(m.Briefing), $"mission {i} has no briefing");
                if (i > 0) check(m.MinRange > all[i - 1].MinRange && m.Reward > all[i - 1].Reward, $"mission {i} should be further and pay more than the last");
                if (m.Goal == MissionGoal.EliminateBoss)
                {
                    bosses++;
                    check(m.Hostiles >= 3, $"boss mission {i} needs guards");
                }
            }
            check(bosses == 4, $"expected 4 boss missions, found {bosses}");
            check(SniperMissions.Get(-5).Index == 0 && SniperMissions.Get(999).Index == all.Count - 1, "Get must clamp");
            log.AppendLine($"  missions: {all.Count}, bosses {bosses}, ranges {all[0].MinRange}-{all[all.Count - 1].MaxRange} m");
        }

        // ------------------------------------------------------------------ rifle and economy

        static void Economy(Action<bool, string> check, StringBuilder log)
        {
            for (int s = 0; s < Rifle.StatCount; s++)
            {
                var stat = (RifleStat)s;
                int previous = 0;
                for (int level = 0; level < Rifle.MaxLevel(stat); level++)
                {
                    int cost = Rifle.UpgradeCost(stat, level);
                    check(cost > previous, $"{stat} level {level} should cost more than the last");
                    previous = cost;
                }
                check(Rifle.UpgradeCost(stat, Rifle.MaxLevel(stat)) == -1, $"{stat} maxed must cost -1");
            }
            for (int level = 1; level <= 5; level++)
            {
                check(Rifle.Damage(level) > Rifle.Damage(level - 1), "damage must grow");
                check(Rifle.Sway(level) < Rifle.Sway(level - 1), "sway must shrink");
                check(Rifle.MaxZoom(level) > Rifle.MaxZoom(level - 1), "zoom must grow");
                check(Rifle.BoltTime(level) < Rifle.BoltTime(level - 1), "bolt must speed up");
            }
            var basic = Rifle.For(null);
            check(basic.Damage == Rifle.Damage(0) && !basic.BallisticComputer, "no upgrades means level 0");
            var clamped = Rifle.For(new[] { 99, -4, 2 });
            check(clamped.Damage == Rifle.Damage(5) && clamped.Sway == Rifle.Sway(0) && clamped.MaxZoom == Rifle.MaxZoom(2), "levels must clamp");
            check(Rifle.Damage(0) * Rifle.HeadMultiplier >= 100f, "a headshot must always destroy a basic robot");
            check(Rifle.Damage(0) < 100f && Rifle.Damage(5) >= 100f, "body shots: two hits at first, one when fully upgraded");

            int total = 0;
            for (int s = 0; s < Rifle.StatCount; s++)
                for (int level = 0; level < Rifle.MaxLevel((RifleStat)s); level++)
                    total += Rifle.UpgradeCost((RifleStat)s, level);
            int earnable = 0;
            foreach (var m in SniperMissions.All) earnable += m.Reward;
            check(total > earnable * 0.6f && total < earnable * 2.5f, $"upgrades ({total}) out of proportion to campaign rewards ({earnable})");
            log.AppendLine($"  economy: all upgrades {total} coins, campaign base rewards {earnable} coins");
        }

        static void Saves(Action<bool, string> check, StringBuilder log)
        {
            var progress = new SniperProgress(null);
            check(progress.Coins == SniperProgress.StartingCoins && progress.Unlocked == 0, "a new save starts with coins and mission 1");

            var first = SniperMissions.Get(0);
            int coins = progress.RecordResult(first, new MissionStats { Outcome = MissionOutcome.OutOfTime, ShotsFired = 3 });
            check(coins == 0 && progress.Unlocked == 0 && progress.StarsFor(0) == 0, "a failed mission must change nothing");

            var perfect = new MissionStats { Outcome = MissionOutcome.Completed, ShotsFired = 2, Hits = 2, Kills = 2, Headshots = 2 };
            coins = progress.RecordResult(first, perfect);
            check(coins == MissionScoring.Coins(first, perfect) && coins > first.Reward, "3-star coins");
            check(progress.StarsFor(0) == 3 && progress.Unlocked == 1, "stars and unlock after a win");
            progress.RecordResult(first, new MissionStats { Outcome = MissionOutcome.Completed, ShotsFired = 5, Hits = 2, Kills = 1 });
            check(progress.StarsFor(0) == 3, "a worse replay must keep the best stars");

            int before = progress.Coins;
            var saved = new SniperProgress(new SniperSave { coins = 10 });
            check(!saved.TryUpgrade(RifleStat.Power) && saved.Coins == 10, "cannot buy without coins");
            saved.AddCoins(1000);
            int cost = saved.UpgradeCost(RifleStat.Power);
            check(saved.TryUpgrade(RifleStat.Power) && saved.Coins == 1010 - cost && saved.Level(RifleStat.Power) == 1, "buying deducts coins and raises the level");
            check(before > 0, "coins after two wins");

            // Damaged or old saves still load.
            var broken = new SniperProgress(new SniperSave { coins = -50, unlocked = 400, stars = new[] { 7, -1 }, upgrades = new[] { 99, 2, -3, 1, 8, 4 } });
            check(broken.Coins == 0 && broken.Unlocked == SniperMissions.Count - 1, "coins and unlock clamp");
            check(broken.StarsFor(0) == 3 && broken.StarsFor(1) == 0 && broken.Data.stars.Length == SniperMissions.Count, "stars repaired");
            check(broken.Level(RifleStat.Power) == 5 && broken.Level(RifleStat.Zoom) == 0 && broken.Level(RifleStat.Computer) == 1 && broken.Data.upgrades.Length == Rifle.StatCount, "upgrades repaired");

            var json = JsonUtility.ToJson(progress.Data);
            var reloaded = new SniperProgress(JsonUtility.FromJson<SniperSave>(json));
            check(reloaded.Coins == progress.Coins && reloaded.StarsFor(0) == 3 && reloaded.Unlocked == 1, "save round trip");

            var now = new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
            check(reloaded.FreeCoinsReady(now), "free coins ready on a new save");
            int wallet = reloaded.Coins;
            reloaded.ClaimFreeCoins(now);
            check(reloaded.Coins == wallet + SniperProgress.FreeCoinsAmount, "free coins paid");
            check(!reloaded.FreeCoinsReady(now.AddMinutes(4)) && reloaded.FreeCoinsReady(now.AddMinutes(5)), "free coins cooldown is five minutes");
            log.AppendLine("  saves: progress, upgrades, repair and cooldown checked");
        }

        static void Scoring(Action<bool, string> check, StringBuilder log)
        {
            check(MissionScoring.Stars(new MissionStats { Outcome = MissionOutcome.TargetEscaped, ShotsFired = 1, Hits = 1, Kills = 1, Headshots = 1 }) == 0, "failed: no stars");
            check(MissionScoring.Stars(new MissionStats { Outcome = MissionOutcome.Completed, ShotsFired = 4, Hits = 3, Kills = 2, Headshots = 1 }) == 1, "complete only: 1 star");
            check(MissionScoring.Stars(new MissionStats { Outcome = MissionOutcome.Completed, ShotsFired = 3, Hits = 3, Kills = 2, Headshots = 1 }) == 2, "no misses: 2 stars");
            check(MissionScoring.Stars(new MissionStats { Outcome = MissionOutcome.Completed, ShotsFired = 3, Hits = 2, Kills = 2, Headshots = 2 }) == 2, "headshots with a miss: 2 stars");
            check(MissionScoring.Stars(new MissionStats { Outcome = MissionOutcome.Completed, ShotsFired = 2, Hits = 2, Kills = 2, Headshots = 2 }) == 3, "perfect: 3 stars");
            var m = SniperMissions.Get(3);
            check(MissionScoring.Coins(m, new MissionStats { Outcome = MissionOutcome.Completed, ShotsFired = 9, Hits = 1, Kills = 1 }) < MissionScoring.Coins(m, new MissionStats { Outcome = MissionOutcome.Completed, ShotsFired = 1, Hits = 1, Kills = 1, Headshots = 1 }), "better play pays more");
            log.AppendLine("  scoring: star rules checked");
        }

        // ------------------------------------------------------------------ worlds (real physics)

        static void Worlds(Action<bool, string> check, StringBuilder log)
        {
            for (int district = 0; district < SniperMissions.DistrictCount; district++)
            {
                var world = SniperWorld.Build(district);
                try
                {
                    var areas = new Dictionary<SpawnArea, int>();
                    foreach (var spawn in world.Spawns)
                    {
                        areas.TryGetValue(spawn.Area, out int n);
                        areas[spawn.Area] = n + 1;
                        check(SniperWorld.CanSee(spawn.Position), $"district {district}: spawn at {spawn.Position} is not visible");
                        if (spawn.CanPatrol) check(SniperWorld.CanSee(spawn.PatrolTarget), $"district {district}: patrol target hidden");
                        check(Vector3.Distance(spawn.Position, spawn.Exit) > 1f, $"district {district}: spawn with no escape route");
                    }
                    check(world.Spawns.Count >= 80, $"district {district}: only {world.Spawns.Count} spawn spots");
                    // The parking lot is mostly hidden by the nest's own parapet, so it is not required.
                    check(areas.ContainsKey(SpawnArea.Rooftop) && areas.ContainsKey(SpawnArea.Boulevard) && areas.ContainsKey(SpawnArea.Street), $"district {district}: missing spawn areas");

                    for (int i = 0; i < SniperMissions.PerDistrict; i++)
                    {
                        var m = SniperMissions.Get(district * SniperMissions.PerDistrict + i);
                        var taken = new List<SpawnPoint>();
                        var hostiles = world.Pick(m.Hostiles, m.MinRange, m.MaxRange, taken, new System.Random(m.Seed));
                        check(hostiles.Count == m.Hostiles, $"mission {m.Number}: placed {hostiles.Count} of {m.Hostiles} hostiles");
                        taken.AddRange(hostiles);
                        var civilians = world.Pick(m.Civilians, m.MinRange * 0.75f, m.MaxRange * 1.1f, taken, new System.Random(m.Seed + 1), 5f);
                        check(civilians.Count == m.Civilians, $"mission {m.Number}: placed {civilians.Count} of {m.Civilians} civilians");
                    }

                    if (district == 0) ShotTests(world, check, log);
                    log.AppendLine($"  district {district}: {world.Spawns.Count} spots ({string.Join(", ", AreaCounts(areas))})");
                }
                finally
                {
                    Object.DestroyImmediate(world.Root);
                }
            }
        }

        static IEnumerable<string> AreaCounts(Dictionary<SpawnArea, int> areas)
        {
            foreach (var pair in areas) yield return $"{pair.Key} {pair.Value}";
        }

        /// <summary>Real robots and ray casts: aiming with the scope's holdover lands a headshot; ignoring it at long range does not.</summary>
        static void ShotTests(SniperWorld world, Action<bool, string> check, StringBuilder log)
        {
            SpawnPoint far = null, near = null;
            foreach (var spawn in world.Spawns)
            {
                if (far == null && spawn.Distance > 240f && spawn.Distance < 300f && spawn.Area == SpawnArea.Boulevard) far = spawn;
                if (near == null && spawn.Distance > 70f && spawn.Distance < 110f) near = spawn;
            }
            check(far != null && near != null, "need a near and a far spot for the shot tests");
            if (far == null || near == null) return;

            var p = Ballistics.Rifle;
            var root = new GameObject("Shot Test").transform;
            try
            {
                var farRobot = Robot.Create(RobotRole.Hostile, far, false, 0, root, new System.Random(1));
                var nearRobot = Robot.Create(RobotRole.Civilian, near, false, 0, root, new System.Random(2));
                // Face the nest: a robot turned sideways rightly shields its chest with an arm.
                var toNest = SniperWorld.NestEye - nearRobot.transform.position;
                nearRobot.transform.rotation = Quaternion.LookRotation(new Vector3(toNest.x, 0f, toNest.z));
                Physics.SyncTransforms();

                HitZone? Shoot(Vector3 target, float holdover)
                {
                    var eye = SniperWorld.NestEye;
                    var aimPoint = target + Vector3.up * holdover;
                    var aim = (aimPoint - eye).normalized;
                    RaycastHit hitInfo = default;
                    bool hit = Ballistics.Trace(eye + aim * 0.6f, Ballistics.LaunchVelocity(aim, p), Vector3.zero, p, (Vector3 a, Vector3 b, out float f) =>
                    {
                        var d = b - a;
                        float length = d.magnitude;
                        if (Physics.Raycast(a, d / length, out hitInfo, length, SniperWorld.ShotMask, QueryTriggerInteraction.Ignore))
                        {
                            f = hitInfo.distance / length;
                            return true;
                        }
                        f = 1f;
                        return false;
                    }, null, out _, out _);
                    if (!hit) return null;
                    var part = hitInfo.collider.GetComponent<RobotPart>();
                    return part != null ? part.Zone : (HitZone?)null;
                }

                var head = farRobot.HeadPosition;
                float distance = Vector3.Distance(SniperWorld.NestEye, head);
                float drop = Ballistics.OffsetAt(distance, p);
                var aimed = Shoot(head, -drop);
                var unaimed = Shoot(head, 0f);
                check(aimed == HitZone.Head, $"holdover shot at {distance:F0} m should be a headshot (got {aimed?.ToString() ?? "miss"})");
                check(unaimed != HitZone.Head, $"at {distance:F0} m a shot with no holdover should fall below the head (got {unaimed?.ToString() ?? "miss"})");

                var civilianChest = nearRobot.ChestPosition;
                var close = Shoot(civilianChest, -Ballistics.OffsetAt(Vector3.Distance(SniperWorld.NestEye, civilianChest), p));
                check(close == HitZone.Body, $"a chest shot at ~{near.Distance:F0} m should hit the body (got {close?.ToString() ?? "miss"})");

                bool destroyed = farRobot.WouldDestroy(HitZone.Head, Rifle.Damage(0)) && !farRobot.WouldDestroy(HitZone.Body, Rifle.Damage(0));
                check(destroyed, "basic rifle: headshot destroys, body shot does not");
                check(nearRobot.WouldDestroy(HitZone.Limb, 1f), "any hit destroys a civilian");
                log.AppendLine($"  shots: {distance:F0} m drop {drop:F2} m, holdover -> {aimed}, none -> {unaimed?.ToString() ?? "miss"}; {near.Distance:F0} m chest -> {close}");
            }
            finally
            {
                Object.DestroyImmediate(root.gameObject);
            }
        }
    }
}
