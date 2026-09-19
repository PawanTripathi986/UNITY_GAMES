using System;
using System.Collections;
using System.Collections.Generic;
using NexioCraft.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace NexioCraft.Sniper
{
    /// <summary>What one shot did, for the HUD.</summary>
    public struct ShotReport
    {
        public bool RobotHit;
        public bool Kill;
        public bool Headshot;
        public bool Civilian;
        public bool Barrel;
        public bool Boss;
        public int BarrelKills;
        public float Distance;
    }

    /// <summary>
    /// One mission in progress: the 3D world, robots, the rifle (aim, sway, breath, recoil, bolt) and the rules.
    /// The game screen drives it with input and reads its state for the HUD.
    /// </summary>
    public sealed class SniperSession : MonoBehaviour
    {
        public const float BaseFov = 60f;
        const float MinPitch = SniperWorld.MinPitch;
        const float MaxPitch = SniperWorld.MaxPitch;
        const float FixedStep = 0.02f;

        /// <summary>Where the first-person rifle rests in front of the camera.</summary>
        static readonly Vector3 RifleRest = new Vector3(0.14f, -0.29f, 0.9f);
        static readonly Quaternion RifleAngle = Quaternion.Euler(-2f, -7f, 0f);

        public MissionDef Mission { get; private set; }
        public RifleStats Rifle { get; private set; }
        public SniperWorld World { get; private set; }
        public Camera Camera { get; private set; }

        public bool Running { get; private set; }
        public bool Finished { get; private set; }
        public bool Paused { get; private set; }
        public bool BulletCamActive { get; private set; }

        public bool Scoped { get; private set; }
        public float Zoom { get; private set; }
        public float ZoomTarget { get; private set; }
        public float Breath { get; private set; } = 1f;
        public bool HoldingBreath { get; private set; }
        public bool Gasping => gasp > 0f;
        public int Ammo { get; private set; }
        public bool Reloading => cycleTimer > 0f && reloadingMagazine;
        /// <summary>0 right after a shot, 1 when the rifle is ready again.</summary>
        public float CycleProgress => cycleDuration <= 0f ? 1f : 1f - Mathf.Clamp01(cycleTimer / cycleDuration);

        public float TimeLeft => Mathf.Max(0f, Mission.TimeLimit - elapsed);
        public int RequiredTotal { get; private set; }
        public int RequiredDown { get; private set; }
        public Vector3 Wind { get; private set; }

        /// <summary>Distance to whatever the crosshair is on, or -1.</summary>
        public float AimDistance { get; private set; } = -1f;
        /// <summary>The robot under the crosshair, if any.</summary>
        public Robot AimRobot { get; private set; }
        public bool HasImpactPreview { get; private set; }
        public Vector3 ImpactPreview { get; private set; }

        public MissionStats Stats => stats;
        public IReadOnlyList<Robot> Robots => robots;

        public event Action<ShotReport> ShotLanded;
        public event Action<MissionStats> Ended;
        public event Action TargetsAlerted;
        public event Action<bool> BulletCamChanged;
        public event Action Fired;

        readonly List<Robot> robots = new List<Robot>();
        readonly List<ExplosiveBarrel> barrels = new List<ExplosiveBarrel>();
        readonly List<Vector3> previewPath = new List<Vector3>(64);
        SniperFx fx;
        Transform actors;
        System.Random random;
        MissionStats stats;

        float yaw, pitch;
        float swayTime, swayYaw, swayPitch;
        float recoilPitch, recoilYaw, shake;
        float breathBlend;
        float gasp;
        float cycleTimer, cycleDuration;
        bool reloadingMagazine;
        float elapsed;
        bool alertAnnounced;
        RaycastHit castHit;
        Transform viewRifle;
        float rifleKick;

        // ------------------------------------------------------------ setup

        public static SniperSession Create(MissionDef mission, RifleStats rifle)
        {
            var go = new GameObject("Sniper Session");
            var session = go.AddComponent<SniperSession>();
            session.Setup(mission, rifle);
            return session;
        }

        void Setup(MissionDef mission, RifleStats rifle)
        {
            Mission = mission;
            Rifle = rifle;
            random = new System.Random(mission.Seed);
            Wind = mission.Wind;
            Ammo = rifle.Magazine;
            Zoom = ZoomTarget = Mathf.Min(4f, rifle.MaxZoom);

            World = SniperWorld.Build(mission.District, transform);
            actors = new GameObject("Actors").transform;
            actors.SetParent(transform, false);
            fx = SniperFx.Create(transform);
            CreateCamera();
            SpawnActors();
            AimAtTargets();
            ApplyCamera();
        }

        void CreateCamera()
        {
            var go = new GameObject("Sniper Camera");
            go.transform.SetParent(transform, false);
            Camera = go.AddComponent<Camera>();
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = World.Theme.FogColor;
            Camera.nearClipPlane = 0.5f;
            Camera.farClipPlane = 2600f;
            Camera.fieldOfView = BaseFov;
            Camera.depth = 5f;
            Camera.cullingMask = ~(1 << 5);
            Camera.allowHDR = false;
            World.SkyFollower.GetComponent<SkyFollow>().Target = go.transform;
            CreateViewRifle();
        }

        /// <summary>The rifle seen in first person when not scoped: it leads the eye to the crosshair.</summary>
        void CreateViewRifle()
        {
            Color32 T(int hex) => MeshBuilder.Tone(Palette.Hex(hex));
            Color32 metal = T(0x3A404C), dark = T(0x1E2229), light = T(0x5A6272), accent = T(0x1F6B58);
            var mb = new MeshBuilder();
            mb.Box(new Vector3(0f, -0.03f, -0.44f), new Vector3(0.07f, 0.17f, 0.34f), accent, accent);
            mb.Box(new Vector3(0f, 0.065f, -0.4f), new Vector3(0.055f, 0.035f, 0.22f), dark, dark);
            mb.Box(new Vector3(0f, 0.02f, -0.1f), new Vector3(0.075f, 0.09f, 0.34f), metal, light);
            mb.Box(new Vector3(0f, -0.1f, -0.2f), new Vector3(0.048f, 0.14f, 0.06f), Quaternion.Euler(18f, 0f, 0f), dark, dark);
            mb.Box(new Vector3(0f, -0.065f, -0.03f), new Vector3(0.05f, 0.1f, 0.09f), dark, dark);
            mb.Box(new Vector3(0f, 0.005f, 0.26f), new Vector3(0.082f, 0.085f, 0.4f), accent, accent);
            mb.Box(new Vector3(0f, 0.052f, 0.26f), new Vector3(0.03f, 0.012f, 0.38f), dark, dark);
            mb.Tube(new Vector3(0f, 0.02f, 0.45f), new Vector3(0f, 0.02f, 1.02f), 0.013f, 8, metal);
            mb.Tube(new Vector3(0f, 0.02f, 1.02f), new Vector3(0f, 0.02f, 1.3f), 0.03f, 10, dark);
            mb.Tube(new Vector3(0f, 0.12f, -0.2f), new Vector3(0f, 0.12f, 0.2f), 0.028f, 12, metal);
            mb.Tube(new Vector3(0f, 0.12f, 0.2f), new Vector3(0f, 0.12f, 0.31f), 0.04f, 12, dark);
            mb.Tube(new Vector3(0f, 0.12f, -0.3f), new Vector3(0f, 0.12f, -0.2f), 0.034f, 12, dark);
            mb.Box(new Vector3(0f, 0.075f, -0.1f), new Vector3(0.03f, 0.05f, 0.035f), dark, dark);
            mb.Box(new Vector3(0f, 0.075f, 0.1f), new Vector3(0.03f, 0.05f, 0.035f), dark, dark);
            mb.Tube(new Vector3(0f, 0.12f, 0f), new Vector3(0f, 0.165f, 0f), 0.015f, 8, light);
            mb.Tube(new Vector3(0.026f, 0.12f, 0f), new Vector3(0.052f, 0.12f, 0f), 0.014f, 8, light);
            mb.Box(new Vector3(0f, 0.12f, 0.312f), new Vector3(0.05f, 0.05f, 0.002f), MeshBuilder.Tone(Palette.Hex(0x7FE7FF), 0.55f), dark);
            mb.Tube(new Vector3(0.035f, 0.035f, -0.15f), new Vector3(0.095f, -0.005f, -0.17f), 0.007f, 6, light);
            mb.Sphere(new Vector3(0.1f, -0.01f, -0.17f), Vector3.one * 0.016f, 3, 6, dark);
            viewRifle = mb.ToObject("View Rifle", Camera.transform, SniperMaterials.Lit).transform;
            viewRifle.localPosition = RifleRest;
            viewRifle.localRotation = RifleAngle;
            viewRifle.localScale = Vector3.one * 0.85f;
        }

        void UpdateViewRifle(float dt)
        {
            if (viewRifle == null) return;
            bool show = !Scoped && !BulletCamActive && Camera.fieldOfView > BaseFov * 0.8f;
            if (viewRifle.gameObject.activeSelf != show) viewRifle.gameObject.SetActive(show);
            if (!show) return;
            rifleKick = Mathf.MoveTowards(rifleKick, 0f, dt * 3.2f);
            float kick = Ease.OutQuad(rifleKick);
            float bob = Mathf.Sin(swayTime * 1.7f) * 0.004f;
            viewRifle.localPosition = RifleRest + new Vector3(0f, bob + kick * 0.02f, -kick * 0.11f);
            viewRifle.localRotation = RifleAngle * Quaternion.Euler(-kick * 7f, 0f, kick * 2f);
        }

        void SpawnActors()
        {
            var taken = new List<SpawnPoint>();
            var hostileSpots = World.Pick(Mission.Hostiles, Mission.MinRange, Mission.MaxRange, taken, random);
            // The boss takes the farthest spot.
            hostileSpots.Sort((a, b) => b.Distance.CompareTo(a.Distance));
            taken.AddRange(hostileSpots);
            bool bossMission = Mission.Goal == MissionGoal.EliminateBoss;
            for (int i = 0; i < hostileSpots.Count; i++)
            {
                var role = bossMission && i == 0 ? RobotRole.Boss : RobotRole.Hostile;
                bool patrols = random.NextDouble() < Mission.PatrolShare;
                AddRobot(Robot.Create(role, hostileSpots[i], patrols, i, actors, random));
            }

            var civilianSpots = World.Pick(Mission.Civilians, Mission.MinRange * 0.75f, Mission.MaxRange * 1.1f, taken, random, 5f);
            taken.AddRange(civilianSpots);
            for (int i = 0; i < civilianSpots.Count; i++)
                AddRobot(Robot.Create(RobotRole.Civilian, civilianSpots[i], random.NextDouble() < 0.65, i, actors, random));

            for (int i = 0; i < Mission.Barrels && hostileSpots.Count > 0; i++)
            {
                var near = hostileSpots[hostileSpots.Count - 1 - i % hostileSpots.Count];
                if (World.BarrelSpot(near, random, out var spot)) barrels.Add(ExplosiveBarrel.Create(spot, actors));
            }

            foreach (var robot in robots)
                if (IsRequired(robot)) RequiredTotal++;
            if (hostileSpots.Count < Mission.Hostiles)
                Debug.LogWarning($"Mission {Mission.Number}: only {hostileSpots.Count} of {Mission.Hostiles} hostile spots available.");
        }

        void AddRobot(Robot robot)
        {
            robot.Escaped = OnEscaped;
            robots.Add(robot);
        }

        bool IsRequired(Robot robot) =>
            Mission.Goal == MissionGoal.EliminateBoss ? robot.Role == RobotRole.Boss : robot.Hostile;

        void AimAtTargets()
        {
            var sum = Vector3.zero;
            int count = 0;
            foreach (var robot in robots)
            {
                if (!robot.Hostile) continue;
                sum += robot.ChestPosition;
                count++;
            }
            var focus = count > 0 ? sum / count : new Vector3(0f, 0f, 150f);
            var delta = focus - SniperWorld.NestEye;
            // Start near the action, not on it: finding the target is part of the job.
            float offset = (random.Next(2) == 0 ? -1f : 1f) * (6f + (float)random.NextDouble() * 6f);
            yaw = Mathf.Clamp(Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg + offset, -SniperWorld.MaxYaw, SniperWorld.MaxYaw);
            pitch = Mathf.Clamp(Mathf.Atan2(delta.y, new Vector2(delta.x, delta.z).magnitude) * Mathf.Rad2Deg + 3f, MinPitch, MaxPitch);
        }

        // ------------------------------------------------------------ input

        /// <summary>Starts the clock once the briefing is dismissed.</summary>
        public void Begin() => Running = true;

        bool CanControl => Running && !Finished && !Paused && !BulletCamActive;

        /// <summary>Turns the view by a screen-space drag (pixels): drag right to look right.</summary>
        public void Look(Vector2 pixels)
        {
            if (!CanControl) return;
            float degreesPerPixel = Camera.fieldOfView / Mathf.Max(1f, Screen.height);
            yaw = Mathf.Clamp(yaw + pixels.x * degreesPerPixel, -SniperWorld.MaxYaw, SniperWorld.MaxYaw);
            pitch = Mathf.Clamp(pitch + pixels.y * degreesPerPixel, MinPitch, MaxPitch);
        }

        public void SetScoped(bool scoped)
        {
            if (scoped && !CanControl) return;
            if (Scoped == scoped) return;
            Scoped = scoped;
            if (!scoped) HoldingBreath = false;
            App.Instance.Audio.Play(Sfx.ScopeIn, 0.7f, scoped ? 1f : 0.85f);
            Haptics.Play(HapticKind.Light);
        }

        public float Zoom01 => Mathf.InverseLerp(Sniper.Rifle.MinZoom, Rifle.MaxZoom, ZoomTarget);

        public void SetZoom01(float t) => ZoomTarget = Mathf.Lerp(Sniper.Rifle.MinZoom, Rifle.MaxZoom, Mathf.Clamp01(t));

        public void SetBreath(bool hold)
        {
            HoldingBreath = hold && Scoped && CanControl && gasp <= 0f && Breath > 0.05f;
        }

        public void SetPaused(bool paused)
        {
            if (BulletCamActive) return;
            Paused = paused;
            Time.timeScale = paused ? 0f : 1f;
            if (paused) HoldingBreath = false;
        }

        public bool CanFire => CanControl && cycleTimer <= 0f && Ammo > 0;

        // ------------------------------------------------------------ frame

        void Update()
        {
            if (BulletCamActive) return;
            float dt = Time.deltaTime;

            if (Running && !Finished && dt > 0f)
            {
                elapsed += dt;
                if (elapsed >= Mission.TimeLimit) End(MissionOutcome.OutOfTime, 0.2f);
            }

            UpdateBreath(dt);
            UpdateSway(dt);
            UpdateCycle(dt);

            float settle = 1f - Mathf.Exp(-dt * 7f);
            recoilPitch = Mathf.Lerp(recoilPitch, 0f, settle);
            recoilYaw = Mathf.Lerp(recoilYaw, 0f, settle);
            shake = Mathf.MoveTowards(shake, 0f, dt * 5f);

            Zoom = Mathf.Lerp(Zoom, ZoomTarget, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 12f));
            float targetFov = Scoped ? BaseFov / Zoom : BaseFov;
            Camera.fieldOfView = Mathf.Lerp(Camera.fieldOfView, targetFov, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 16f));

            ApplyCamera();
            UpdateViewRifle(dt);
            UpdateAim();
        }

        void UpdateBreath(float dt)
        {
            if (gasp > 0f) gasp = Mathf.Max(0f, gasp - dt / 1.8f);
            if (HoldingBreath)
            {
                Breath -= dt / 4.5f;
                if (Breath <= 0f)
                {
                    // Held too long: a gasp that makes the rifle shake for a moment.
                    Breath = 0f;
                    HoldingBreath = false;
                    gasp = 1f;
                }
            }
            else
            {
                Breath = Mathf.Min(1f, Breath + dt / 3.2f);
            }
            breathBlend = Mathf.MoveTowards(breathBlend, HoldingBreath ? 1f : 0f, dt / 0.35f);
        }

        void UpdateSway(float dt)
        {
            swayTime += dt;
            float steady = Mathf.Lerp(1f, Sniper.Rifle.BreathSteadiness, breathBlend);
            float amplitude = Rifle.Sway * (Scoped ? 1f : 0.25f) * steady * (1f + gasp * 1.5f);
            float nx = Mathf.PerlinNoise(swayTime * 0.45f, 3.7f) * 2f - 1f;
            float ny = Mathf.PerlinNoise(11.3f, swayTime * 0.38f) * 2f - 1f;
            float breathing = Mathf.Sin(swayTime * 1.7f) * 0.45f * (1f - breathBlend);
            swayYaw = nx * amplitude * 1.2f;
            swayPitch = (ny + breathing) * amplitude;
        }

        void UpdateCycle(float dt)
        {
            if (cycleTimer <= 0f) return;
            float before = cycleTimer;
            cycleTimer -= dt;
            // The bolt sound lands a little after the shot's echo.
            float cue = cycleDuration - Mathf.Min(0.45f, cycleDuration * 0.4f);
            if (before > cue && cycleTimer <= cue) App.Instance.Audio.Play(Sfx.BoltCycle, 0.8f, reloadingMagazine ? 0.9f : 1f);
            if (cycleTimer <= 0f && reloadingMagazine)
            {
                reloadingMagazine = false;
                Ammo = Rifle.Magazine;
            }
        }

        void ApplyCamera()
        {
            var jitter = shake > 0f ? Random.insideUnitSphere * 0.03f * shake : Vector3.zero;
            float p = pitch + swayPitch + recoilPitch;
            float y = yaw + swayYaw + recoilYaw;
            Camera.transform.SetPositionAndRotation(SniperWorld.NestEye + jitter, Quaternion.Euler(-p, y, 0f));
        }

        void UpdateAim()
        {
            var origin = Camera.transform.position;
            var forward = Camera.transform.forward;
            if (Physics.Raycast(origin, forward, out var hit, 1500f, SniperWorld.ShotMask, QueryTriggerInteraction.Ignore))
            {
                AimDistance = hit.distance;
                var part = hit.collider.GetComponent<RobotPart>();
                AimRobot = part != null && part.Owner.Alive ? part.Owner : null;
            }
            else
            {
                AimDistance = -1f;
                AimRobot = null;
            }

            HasImpactPreview = false;
            if (Rifle.BallisticComputer && Scoped && Running && !Finished)
            {
                var velocity = Ballistics.LaunchVelocity(forward, Ballistics.Rifle);
                HasImpactPreview = Ballistics.Trace(origin + forward * 0.6f, velocity, Wind, Ballistics.Rifle, Cast, previewPath,
                    out var impact, out _, Ballistics.PreviewStep, 650f);
                ImpactPreview = impact;
            }
        }

        bool Cast(Vector3 from, Vector3 to, out float fraction)
        {
            var delta = to - from;
            float length = delta.magnitude;
            if (length > 1e-5f && Physics.Raycast(from, delta / length, out castHit, length, SniperWorld.ShotMask, QueryTriggerInteraction.Ignore))
            {
                fraction = castHit.distance / length;
                return true;
            }
            fraction = 1f;
            return false;
        }

        // ------------------------------------------------------------ shooting

        public void Fire()
        {
            if (!CanFire) return;
            Ammo--;
            stats.ShotsFired++;
            Fired?.Invoke();

            var aim = Camera.transform.forward;
            var origin = Camera.transform.position + aim * 0.6f;
            var velocity = Ballistics.LaunchVelocity(aim, Ballistics.Rifle);
            var path = new List<Vector3>(256);

            // Move every walking robot to where it will be when the bullet gets there, trace, then put them back.
            var moved = new List<(Robot robot, Vector3 position)>();
            foreach (var robot in robots)
            {
                if (!robot.Alive) continue;
                float travel = Vector3.Distance(origin, robot.ChestPosition) / Ballistics.Rifle.MuzzleSpeed;
                var predicted = robot.PredictPosition(travel);
                if ((predicted - robot.transform.position).sqrMagnitude < 1e-6f) continue;
                moved.Add((robot, robot.transform.position));
                robot.transform.position = predicted;
            }
            if (moved.Count > 0) Physics.SyncTransforms();
            bool hit = Ballistics.Trace(origin, velocity, Wind, Ballistics.Rifle, Cast, path, out var impact, out float flight);
            var hitInfo = castHit;
            foreach (var (robot, position) in moved) robot.transform.position = position;
            if (moved.Count > 0) Physics.SyncTransforms();

            App.Instance.Audio.Play(Sfx.Gunshot, 1f, Random.Range(0.96f, 1.04f));
            Haptics.Play(HapticKind.Heavy);
            recoilPitch += Scoped ? 1.9f : 2.6f;
            recoilYaw += Random.Range(-0.4f, 0.4f);
            shake = 1f;
            rifleKick = 1f;
            HoldingBreath = false;
            reloadingMagazine = Ammo == 0;
            cycleDuration = cycleTimer = reloadingMagazine ? Rifle.MagazineReloadTime : Rifle.BoltTime;

            var direction = velocity.normalized;
            var part = hit ? hitInfo.collider.GetComponent<RobotPart>() : null;
            var barrel = hit ? hitInfo.collider.GetComponent<ExplosiveBarrel>() : null;
            var normal = hit ? hitInfo.normal : -direction;
            float distance = Vector3.Distance(origin, impact);

            if (part != null && part.Owner.Alive)
            {
                var robot = part.Owner;
                var zone = part.Zone;
                bool finale = IsRequired(robot) && robot.WouldDestroy(zone, Rifle.Damage) && RequiredDown + 1 >= RequiredTotal;
                if (finale)
                {
                    StartCoroutine(BulletCam(path, () => HitRobot(robot, zone, impact, normal, direction, distance)));
                    return;
                }
                StartCoroutine(After(flight, () => HitRobot(robot, zone, impact, normal, direction, distance)));
            }
            else if (barrel != null)
            {
                StartCoroutine(After(flight, () => HitBarrel(barrel, distance)));
            }
            else if (hit)
            {
                StartCoroutine(After(flight, () => HitScenery(impact, normal)));
            }
            fx.Tracer(path);
        }

        static IEnumerator After(float seconds, Action action)
        {
            if (seconds > 0f) yield return new WaitForSeconds(seconds);
            action();
        }

        void HitRobot(Robot robot, HitZone zone, Vector3 point, Vector3 normal, Vector3 direction, float distance)
        {
            if (Finished) return;
            var report = new ShotReport { RobotHit = true, Distance = distance, Civilian = robot.Role == RobotRole.Civilian, Boss = robot.Role == RobotRole.Boss };
            if (!robot.Alive)
            {
                HitScenery(point, normal);
                return;
            }
            stats.Hits++;
            bool destroyed = robot.TakeHit(zone, Rifle.Damage, out bool headshot);
            report.Headshot = headshot;
            fx.Sparks(point, normal, new Color(1f, 0.75f, 0.35f), destroyed ? 36 : 16, destroyed ? 11f : 7f);
            App.Instance.Audio.Play(Sfx.RobotHit, 0.9f, Random.Range(0.92f, 1.08f));

            if (destroyed)
            {
                Destroyed(robot, point, direction, headshot, 1f);
                report.Kill = true;
            }
            else
            {
                AnnounceAlert();
            }
            ShotLanded?.Invoke(report);
            if (robot.Role == RobotRole.Civilian) End(MissionOutcome.CivilianHit, 1.3f);
            else CheckComplete();
        }

        void Destroyed(Robot robot, Vector3 point, Vector3 direction, bool headshot, float force)
        {
            robot.Break(point, direction, headshot, force);
            fx.Flash(robot.ChestPosition, 1.4f, new Color(1f, 0.7f, 0.35f));
            fx.Smoke(robot.ChestPosition, 6, new Color(0.25f, 0.25f, 0.27f, 0.8f));
            App.Instance.Audio.Play(Sfx.RobotDown, 0.85f, Random.Range(0.95f, 1.05f));
            if (robot.Role == RobotRole.Civilian) return;
            stats.Kills++;
            if (headshot) stats.Headshots++;
            if (IsRequired(robot)) RequiredDown++;
            AlertNear(robot.transform.position, 34f);
        }

        void HitBarrel(ExplosiveBarrel barrel, float distance)
        {
            if (Finished || barrel.Exploded) return;
            stats.Hits++;
            var report = new ShotReport { Barrel = true, Distance = distance };
            Explode(barrel, ref report);
            ShotLanded?.Invoke(report);
            if (report.Civilian) End(MissionOutcome.CivilianHit, 1.3f);
            else CheckComplete();
        }

        void Explode(ExplosiveBarrel barrel, ref ShotReport report)
        {
            if (barrel.Exploded) return;
            var center = barrel.transform.position;
            barrel.Explode();
            fx.Explosion(center);
            App.Instance.Audio.Play(Sfx.Explosion, 1f);
            Haptics.Play(HapticKind.Heavy);
            foreach (var robot in robots)
            {
                if (!robot.Alive) continue;
                var offset = robot.ChestPosition - center;
                if (offset.magnitude > ExplosiveBarrel.BlastRadius) continue;
                if (robot.Role == RobotRole.Civilian) report.Civilian = true;
                else report.BarrelKills++;
                Destroyed(robot, center, offset + Vector3.up, false, 1.6f);
            }
            AlertNear(center, 40f);
            // Chain reaction.
            foreach (var other in barrels)
                if (!other.Exploded && Vector3.Distance(other.transform.position, center) < 5f)
                    Explode(other, ref report);
        }

        void HitScenery(Vector3 point, Vector3 normal)
        {
            if (Finished) return;
            fx.Dust(point, normal, Color.Lerp(World.Theme.Ground, Color.white, 0.25f));
            fx.Sparks(point, normal, new Color(1f, 0.85f, 0.6f), 5, 4f);
            fx.BulletHole(point, normal);
            // Robots that hear a bullet smack in next to them bolt.
            AlertNear(point, 14f);
        }

        void AlertNear(Vector3 point, float radius)
        {
            bool any = false;
            foreach (var robot in robots)
            {
                if (!robot.Alive || robot.State == RobotState.Fleeing || robot.State == RobotState.Alerted) continue;
                if (Vector3.Distance(robot.transform.position, point) > radius) continue;
                robot.Alert(Random.Range(0.5f, 1.3f));
                if (robot.Hostile) any = true;
            }
            if (any) AnnounceAlert();
        }

        void AnnounceAlert()
        {
            if (alertAnnounced || Finished) return;
            alertAnnounced = true;
            App.Instance.Audio.Play(Sfx.Alarm, 0.6f);
            TargetsAlerted?.Invoke();
        }

        void OnEscaped(Robot robot)
        {
            if (Finished) return;
            if (IsRequired(robot)) End(MissionOutcome.TargetEscaped, 0.4f);
        }

        void CheckComplete()
        {
            if (!Finished && RequiredTotal > 0 && RequiredDown >= RequiredTotal) End(MissionOutcome.Completed, 1.4f);
        }

        void End(MissionOutcome outcome, float delay)
        {
            if (Finished) return;
            Finished = true;
            Scoped = Scoped && outcome != MissionOutcome.Completed;
            HoldingBreath = false;
            stats.Outcome = outcome;
            stats.TimeUsed = elapsed;
            StartCoroutine(EndRoutine(delay));
        }

        IEnumerator EndRoutine(float delay)
        {
            yield return Tween.Delay(delay);
            while (BulletCamActive) yield return null;
            Ended?.Invoke(stats);
        }

        public void Quit()
        {
            if (Finished) return;
            Finished = true;
            stats.Outcome = MissionOutcome.Quit;
            stats.TimeUsed = elapsed;
        }

        // ------------------------------------------------------------ bullet cam

        IEnumerator BulletCam(List<Vector3> path, Action impact)
        {
            BulletCamActive = true;
            BulletCamChanged?.Invoke(true);
            Scoped = false;
            if (viewRifle != null) viewRifle.gameObject.SetActive(false);
            SetTimeScale(0.06f);

            var bullet = fx.CreateBullet();
            float length = 0f;
            for (int i = 1; i < path.Count; i++) length += Vector3.Distance(path[i - 1], path[i]);
            float duration = Mathf.Clamp(length / 95f, 1.4f, 2.8f);
            var end = path[path.Count - 1];
            var side = Vector3.Cross(Vector3.up, (end - path[0]).normalized).normalized;
            Camera.fieldOfView = 46f;

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                // Fast out of the barrel, easing off as it closes in.
                float s = Mathf.Lerp(u, Ease.OutCubic(u), 0.55f);
                Sample(path, length, s, out var point, out var direction);
                bullet.SetPositionAndRotation(point, Quaternion.LookRotation(direction));
                var cameraPosition = point - direction * 1.1f + Vector3.up * 0.2f + side * Mathf.Lerp(-0.35f, 0.55f, u);
                Camera.transform.SetPositionAndRotation(cameraPosition, Quaternion.LookRotation(point + direction * 0.8f - cameraPosition));
                yield return null;
            }

            Destroy(bullet.gameObject);
            impact();
            Haptics.Play(HapticKind.Success);

            // Swing round the target while time ramps back up.
            var from = Camera.transform.position - end;
            const float hold = 1.9f;
            t = 0f;
            while (t < hold)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / hold);
                SetTimeScale(Mathf.Lerp(0.06f, 0.55f, u * u));
                var offset = Quaternion.Euler(0f, Mathf.Lerp(0f, 55f, Ease.OutQuad(u)), 0f) * from;
                offset = offset.normalized * Mathf.Lerp(from.magnitude, 3.6f, Ease.OutQuad(u)) + Vector3.up * (0.7f * u);
                Camera.transform.position = end + offset;
                Camera.transform.LookAt(end + Vector3.up * 0.2f);
                yield return null;
            }

            SetTimeScale(1f);
            BulletCamActive = false;
            BulletCamChanged?.Invoke(false);
        }

        static void Sample(List<Vector3> path, float length, float s, out Vector3 point, out Vector3 direction)
        {
            float target = Mathf.Clamp01(s) * length;
            float walked = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                float segment = Vector3.Distance(path[i - 1], path[i]);
                if (walked + segment >= target || i == path.Count - 1)
                {
                    float f = segment > 1e-5f ? Mathf.Clamp01((target - walked) / segment) : 1f;
                    point = Vector3.Lerp(path[i - 1], path[i], f);
                    direction = (path[i] - path[i - 1]).normalized;
                    if (direction.sqrMagnitude < 0.5f) direction = Vector3.forward;
                    return;
                }
                walked += segment;
            }
            point = path[path.Count - 1];
            direction = Vector3.forward;
        }

        static void SetTimeScale(float scale)
        {
            Time.timeScale = scale;
            Time.fixedDeltaTime = FixedStep * Mathf.Max(0.05f, scale);
        }

        void OnDestroy()
        {
            // Time scale is global: never leave another screen in slow motion.
            Time.timeScale = 1f;
            Time.fixedDeltaTime = FixedStep;
        }
    }
}
