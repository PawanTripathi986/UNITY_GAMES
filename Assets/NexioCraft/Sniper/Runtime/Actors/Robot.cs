using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace NexioCraft.Sniper
{
    public enum RobotRole { Hostile, Boss, Civilian }
    public enum HitZone { Head, Body, Limb }
    public enum RobotState { Idle, Patrol, Alerted, Fleeing, Escaped, Destroyed }

    /// <summary>Marks a collider as part of a robot, and which part.</summary>
    public sealed class RobotPart : MonoBehaviour
    {
        public Robot Owner;
        public HitZone Zone;
    }

    /// <summary>
    /// A robot built from low-poly parts. Hostiles are gunmetal with red visors and a blaster, the boss is gold with
    /// a crown and cape, civilians are rounded, colourful and unarmed. Each part is a separate hit zone, and when a
    /// robot is destroyed its parts come apart under physics.
    /// </summary>
    public sealed class Robot : MonoBehaviour
    {
        public const float WalkSpeed = 1.25f;
        public const float RunSpeed = 4f;

        public RobotRole Role { get; private set; }
        public RobotState State { get; private set; }
        public float Health { get; private set; }
        public SpawnPoint Spawn { get; private set; }

        public bool Alive => State != RobotState.Destroyed && State != RobotState.Escaped;
        public bool Hostile => Role != RobotRole.Civilian;
        public bool Moving => State == RobotState.Patrol || State == RobotState.Fleeing;

        /// <summary>Raised when the robot reaches its exit while fleeing.</summary>
        public Action<Robot> Escaped;

        Transform hips, torso, head, armLeft, armRight, legLeft, legRight;
        MeshRenderer visor;
        GameObject shadow;
        readonly List<Transform> parts = new List<Transform>();

        Vector3 patrolA, patrolB, destination;
        float waitTimer;
        float reactTimer;
        float phase;
        float seed;
        float hipHeight;
        float stagger;
        float yawVelocity;

        // ------------------------------------------------------------ creation

        public static Robot Create(RobotRole role, SpawnPoint spawn, bool patrols, int variant, Transform parent, Random random)
        {
            var go = new GameObject(role + " Robot");
            go.transform.SetParent(parent, false);
            go.transform.position = spawn.Position;
            float facing = 180f + (float)(random.NextDouble() * 120.0 - 60.0);
            go.transform.rotation = Quaternion.Euler(0f, facing, 0f);

            var robot = go.AddComponent<Robot>();
            robot.Role = role;
            robot.Spawn = spawn;
            robot.seed = (float)random.NextDouble() * 100f;
            robot.phase = robot.seed;
            robot.Health = role == RobotRole.Boss ? 160f : 100f;
            robot.BuildBody(variant);
            if (patrols && spawn.CanPatrol)
            {
                robot.patrolA = spawn.Position;
                robot.patrolB = spawn.PatrolTarget;
                robot.destination = robot.patrolB;
                robot.State = RobotState.Patrol;
                robot.waitTimer = (float)random.NextDouble() * 2f;
            }
            else
            {
                robot.State = RobotState.Idle;
            }
            return robot;
        }

        void BuildBody(int variant)
        {
            float scale = Role == RobotRole.Boss ? 1.18f : Role == RobotRole.Civilian ? 0.92f : 1f;
            transform.localScale = Vector3.one * scale;
            var set = RobotMeshes.For(Role, variant);
            hipHeight = 0.95f;

            hips = Part("Hips", transform, new Vector3(0f, hipHeight, 0f), set.Hips, HitZone.Body, new Vector3(0f, 0f, 0f), new Vector3(0.48f, 0.26f, 0.32f));
            legLeft = Part("Leg L", hips, new Vector3(-0.14f, -0.02f, 0f), set.Leg, HitZone.Limb, new Vector3(0f, -0.47f, 0.03f), new Vector3(0.26f, 0.96f, 0.36f));
            legRight = Part("Leg R", hips, new Vector3(0.14f, -0.02f, 0f), set.Leg, HitZone.Limb, new Vector3(0f, -0.47f, 0.03f), new Vector3(0.26f, 0.96f, 0.36f));
            torso = Part("Torso", hips, new Vector3(0f, 0.1f, 0f), set.Torso, HitZone.Body, new Vector3(0f, 0.33f, 0f), new Vector3(0.62f, 0.7f, 0.44f));
            head = Part("Head", torso, new Vector3(0f, 0.66f, 0f), set.Head, HitZone.Head, new Vector3(0f, 0.2f, 0.01f), new Vector3(0.42f, 0.42f, 0.4f));
            armLeft = Part("Arm L", torso, new Vector3(-0.37f, 0.54f, 0f), set.ArmLeft, HitZone.Limb, new Vector3(0f, -0.34f, 0f), new Vector3(0.18f, 0.72f, 0.2f));
            armRight = Part("Arm R", torso, new Vector3(0.37f, 0.54f, 0f), set.ArmRight, HitZone.Limb, new Vector3(0f, -0.4f, 0.03f), new Vector3(0.18f, 0.86f, 0.22f));

            var visorGo = new GameObject("Visor");
            visorGo.transform.SetParent(head, false);
            visorGo.AddComponent<MeshFilter>().sharedMesh = set.Visor;
            visor = visorGo.AddComponent<MeshRenderer>();
            Configure(visor);

            shadow = RobotMeshes.CreateShadow(transform, Role == RobotRole.Boss ? 1.5f : 1.2f);
        }

        Transform Part(string name, Transform parent, Vector3 localPosition, Mesh mesh, HitZone zone, Vector3 colliderCenter, Vector3 colliderSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            Configure(go.AddComponent<MeshRenderer>());
            var box = go.AddComponent<BoxCollider>();
            box.center = colliderCenter;
            box.size = colliderSize;
            var part = go.AddComponent<RobotPart>();
            part.Owner = this;
            part.Zone = zone;
            parts.Add(go.transform);
            return go.transform;
        }

        static void Configure(MeshRenderer renderer)
        {
            renderer.sharedMaterial = SniperMaterials.Lit;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        // ------------------------------------------------------------ behaviour

        /// <summary>Hostiles run for their exit after a short reaction; civilians panic and run too.</summary>
        public void Alert(float reaction)
        {
            if (!Alive || State == RobotState.Alerted || State == RobotState.Fleeing) return;
            State = RobotState.Alerted;
            reactTimer = reaction;
        }

        public Vector3 ChestPosition => transform.position + Vector3.up * (1.35f * transform.localScale.y);
        public Vector3 HeadPosition => head != null ? head.position + Vector3.up * 0.2f * transform.localScale.y : transform.position + Vector3.up * 1.8f;

        void Update()
        {
            if (!Alive) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            float speed = 0f;

            switch (State)
            {
                case RobotState.Idle:
                    break;

                case RobotState.Patrol:
                    if (waitTimer > 0f)
                    {
                        waitTimer -= dt;
                        break;
                    }
                    if (MoveTowards(destination, WalkSpeed, dt))
                    {
                        destination = destination == patrolA ? patrolB : patrolA;
                        waitTimer = 1f + Mathf.Repeat(seed * 7.3f, 2.5f);
                    }
                    else speed = WalkSpeed;
                    break;

                case RobotState.Alerted:
                    TurnTowards(SniperWorld.NestEye, dt, 400f);
                    reactTimer -= dt;
                    if (reactTimer <= 0f) State = RobotState.Fleeing;
                    break;

                case RobotState.Fleeing:
                    float run = Role == RobotRole.Boss ? RunSpeed * 0.85f : RunSpeed;
                    if (MoveTowards(Spawn.Exit, run, dt))
                    {
                        State = RobotState.Escaped;
                        gameObject.SetActive(false);
                        Escaped?.Invoke(this);
                        return;
                    }
                    speed = run;
                    break;
            }

            Animate(speed, dt);
        }

        /// <summary>Walks on the flat towards a point; returns true on arrival.</summary>
        bool MoveTowards(Vector3 target, float speed, float dt)
        {
            var position = transform.position;
            var delta = new Vector3(target.x - position.x, 0f, target.z - position.z);
            float distance = delta.magnitude;
            if (distance < 0.25f) return true;
            var direction = delta / distance;
            TurnTowards(position + direction, dt, 360f);
            transform.position = position + direction * Mathf.Min(distance, speed * dt);
            return false;
        }

        void TurnTowards(Vector3 point, float dt, float degreesPerSecond)
        {
            var flat = new Vector3(point.x - transform.position.x, 0f, point.z - transform.position.z);
            if (flat.sqrMagnitude < 1e-4f) return;
            float targetYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref yawVelocity, 0.18f, degreesPerSecond, dt);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        void Animate(float speed, float dt)
        {
            float moving = Mathf.Clamp01(speed / WalkSpeed);
            float running = Mathf.Clamp01((speed - WalkSpeed) / (RunSpeed - WalkSpeed));
            phase += dt * (speed > 0f ? Mathf.Lerp(5.5f, 11f, running) : 0f);
            float t = Time.time + seed;

            float swing = moving * Mathf.Lerp(26f, 48f, running);
            float s = Mathf.Sin(phase);
            legLeft.localRotation = Quaternion.Euler(s * swing, 0f, 0f);
            legRight.localRotation = Quaternion.Euler(-s * swing, 0f, 0f);

            bool armed = Role == RobotRole.Hostile && State != RobotState.Fleeing;
            armLeft.localRotation = Quaternion.Euler(-s * swing * 0.8f - running * 20f, 0f, 5f);
            armRight.localRotation = armed
                ? Quaternion.Euler(-38f + s * swing * 0.15f, 0f, -6f)
                : Quaternion.Euler(s * swing * 0.8f - running * 20f, 0f, -5f);

            float bob = moving * Mathf.Abs(Mathf.Cos(phase)) * Mathf.Lerp(0.04f, 0.09f, running);
            float breathe = (1f - moving) * Mathf.Sin(t * 1.8f) * 0.012f;
            hips.localPosition = new Vector3(0f, hipHeight + bob + breathe, 0f);

            stagger = Mathf.MoveTowards(stagger, 0f, dt * 2.5f);
            torso.localRotation = Quaternion.Euler(Mathf.Lerp(3f, 16f, running) - stagger * 25f, Mathf.Sin(phase) * 4f * moving, 0f);

            float scan = State == RobotState.Idle ? Mathf.Sin(t * 0.6f) * 42f : State == RobotState.Patrol && speed == 0f ? Mathf.Sin(t * 1.1f) * 30f : 0f;
            head.localRotation = Quaternion.Euler(State == RobotState.Alerted ? -6f : 2f, scan, 0f);
        }

        /// <summary>
        /// Where the robot will be in <paramref name="seconds"/> if it keeps doing what it is doing. Shots use this, so a
        /// running robot has to be led like a real moving target.
        /// </summary>
        public Vector3 PredictPosition(float seconds)
        {
            var position = transform.position;
            float run = Role == RobotRole.Boss ? RunSpeed * 0.85f : RunSpeed;
            switch (State)
            {
                case RobotState.Patrol:
                    return waitTimer >= seconds ? position : Step(position, destination, WalkSpeed, seconds - Mathf.Max(0f, waitTimer));
                case RobotState.Alerted:
                    return reactTimer >= seconds ? position : Step(position, Spawn.Exit, run, seconds - Mathf.Max(0f, reactTimer));
                case RobotState.Fleeing:
                    return Step(position, Spawn.Exit, run, seconds);
                default:
                    return position;
            }
        }

        static Vector3 Step(Vector3 from, Vector3 to, float speed, float seconds)
        {
            var delta = new Vector3(to.x - from.x, 0f, to.z - from.z);
            float distance = delta.magnitude;
            if (distance < 1e-3f) return from;
            return from + delta / distance * Mathf.Min(distance, speed * seconds);
        }

        // ------------------------------------------------------------ damage

        static float Multiplier(HitZone zone) => zone == HitZone.Head ? Rifle.HeadMultiplier : zone == HitZone.Limb ? Rifle.LimbMultiplier : 1f;

        /// <summary>True if a bullet here would destroy the robot (any hit destroys a civilian).</summary>
        public bool WouldDestroy(HitZone zone, float damage) =>
            Alive && (Role == RobotRole.Civilian || Health - damage * Multiplier(zone) <= 0f);

        /// <summary>Applies a bullet. Returns true if the robot was destroyed by it.</summary>
        public bool TakeHit(HitZone zone, float damage, out bool headshot)
        {
            headshot = zone == HitZone.Head;
            if (!Alive) return false;
            Health -= Role == RobotRole.Civilian ? 1000f : damage * Multiplier(zone);
            if (Health <= 0f) return true;
            stagger = 1f;
            Alert(0.25f);
            return false;
        }

        /// <summary>Destroys the robot: the visor goes dark and every part flies off under physics.</summary>
        public void Break(Vector3 point, Vector3 direction, bool headshot, float force = 1f)
        {
            if (State == RobotState.Destroyed) return;
            State = RobotState.Destroyed;
            if (shadow != null) shadow.SetActive(false);
            if (visor != null)
            {
                var block = new MaterialPropertyBlock();
                block.SetColor("_Tint", new Color(0.12f, 0.12f, 0.14f));
                visor.SetPropertyBlock(block);
            }

            var root = transform.parent;
            direction = direction.normalized;
            foreach (var part in parts)
            {
                if (part == null) continue;
                part.SetParent(root, true);
                part.gameObject.layer = SniperWorld.IgnoreLayer;
                var body = part.gameObject.AddComponent<Rigidbody>();
                body.mass = part == torso ? 6f : part == hips ? 4f : 2f;
                body.linearDamping = 0.15f;
                body.angularDamping = 0.4f;
                body.interpolation = RigidbodyInterpolation.Interpolate;

                float distance = Vector3.Distance(part.position, point);
                float push = Mathf.Lerp(7f, 2.5f, Mathf.Clamp01(distance / 1.2f)) * force;
                var velocity = direction * push + Vector3.up * UnityEngine.Random.Range(1.5f, 4f) * force + UnityEngine.Random.insideUnitSphere * 1.6f * force;
                if (part == head && headshot) velocity = direction * 11f * force + Vector3.up * 5f * force;
                body.linearVelocity = velocity;
                body.angularVelocity = UnityEngine.Random.insideUnitSphere * 9f * force;
            }
            StartCoroutine(SettleDebris());
        }

        IEnumerator SettleDebris()
        {
            // Let the parts land, then freeze them so wreckage costs nothing.
            yield return new WaitForSeconds(7f);
            foreach (var part in parts)
            {
                if (part == null) continue;
                var body = part.GetComponent<Rigidbody>();
                if (body != null) Destroy(body);
            }
        }
    }
}
