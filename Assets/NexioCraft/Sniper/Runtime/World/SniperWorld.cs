using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace NexioCraft.Sniper
{
    public enum SpawnArea { Lot, Street, Boulevard, Rooftop }

    /// <summary>A place a robot can stand that the sniper can see, with a patrol route and an escape point.</summary>
    public sealed class SpawnPoint
    {
        public Vector3 Position;
        public SpawnArea Area;
        /// <summary>0 for the ground, otherwise the roof it stands on.</summary>
        public int Surface;
        public bool CanPatrol;
        public Vector3 PatrolTarget;
        public Vector3 Exit;
        /// <summary>Metres from the sniper's eye to the robot's chest.</summary>
        public float Distance;
    }

    /// <summary>
    /// Builds a district from code: the sniper's rooftop nest, a street and parking lot, a long boulevard, rows of
    /// buildings, a skyline and sky. Also finds every spot a robot can stand where the sniper can actually see it.
    /// The nest looks along +z.
    /// </summary>
    public sealed class SniperWorld
    {
        /// <summary>Leaning out over the parapet (top at 33.1 m), so the ledge stays out of the way of the view.</summary>
        public static readonly Vector3 NestEye = new Vector3(0f, 34.2f, -0.35f);
        public const float MaxYaw = 70f;
        public const float MinPitch = -52f;
        public const float MaxPitch = 22f;

        /// <summary>Layers bullets and sight lines ignore (Unity's built-in "Ignore Raycast").</summary>
        public const int IgnoreLayer = 2;
        public static readonly int ShotMask = ~(1 << IgnoreLayer);

        public readonly SniperTheme Theme;
        public readonly GameObject Root;
        public readonly List<SpawnPoint> Spawns = new List<SpawnPoint>();
        public Transform SkyFollower { get; private set; }

        readonly int district;
        readonly Random rng;
        readonly MeshBuilder mb = new MeshBuilder();
        readonly Transform meshRoot;
        readonly Transform solidRoot;
        readonly List<Roof> roofs = new List<Roof>();
        readonly List<Vector3> groundExits = new List<Vector3>();
        int chunk;

        sealed class Roof
        {
            public int Id;
            public float X0, Z0, X1, Z1, Height;
            public Vector3 Hatch;
        }

        SniperWorld(int district, int seed, Transform parent)
        {
            this.district = district;
            rng = new Random(seed);
            Theme = SniperTheme.ForDistrict(district);
            Root = new GameObject("Sniper World");
            if (parent != null) Root.transform.SetParent(parent, false);
            meshRoot = new GameObject("Meshes").transform;
            meshRoot.SetParent(Root.transform, false);
            solidRoot = new GameObject("Solids").transform;
            solidRoot.SetParent(Root.transform, false);
        }

        /// <summary>
        /// The layout depends only on the district, so every mission in a district shares the same city;
        /// missions differ in where robots stand.
        /// </summary>
        public static SniperWorld Build(int district, Transform parent = null)
        {
            var world = new SniperWorld(district, 4200 + district * 131, parent);
            world.Generate();
            return world;
        }

        void Generate()
        {
            Theme.Apply();
            BuildSky();
            BuildGround();
            BuildNest();
            BuildStreetBlock();
            BuildBoulevard();
            BuildSideBlocks();
            if (district == 1) BuildHarbourFront();
            else BuildSkyline();
            Physics.SyncTransforms();
            FindSpawns();
        }

        // ---------------------------------------------------------------- helpers

        float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);
        bool Chance(float probability) => rng.NextDouble() < probability;
        Color Pick(Color[] colors) => colors[rng.Next(colors.Length)];
        static Color32 Tone(Color color, float glow = 0f) => MeshBuilder.Tone(color, glow);
        static Color Shade(Color color, float amount) => Color.Lerp(color, Color.black, amount);

        void Flush(string label)
        {
            if (mb.IsEmpty) return;
            mb.ToObject(label + " " + chunk++, meshRoot, SniperMaterials.Lit);
            mb.Clear();
        }

        void FlushIfBig(string label)
        {
            if (mb.VertexCount > 24000) Flush(label);
        }

        void Solid(Vector3 center, Vector3 size)
        {
            var go = new GameObject("Solid");
            go.transform.SetParent(solidRoot, false);
            go.transform.localPosition = center;
            go.AddComponent<BoxCollider>().size = size;
            go.isStatic = true;
        }

        void SolidBox(Vector3 center, Vector3 size, Color side, Color top)
        {
            mb.Box(center, size, Tone(side), Tone(top));
            Solid(center, size);
        }

        /// <summary>Flat rectangle on the ground (facing up).</summary>
        void Flat(float x0, float z0, float x1, float z1, float y, Color color, float glow = 0f)
        {
            var c = Tone(color, glow);
            mb.Quad(new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0), c);
        }

        /// <summary>Rectangle on a wall: <paramref name="right"/> is the right-hand direction of someone facing the wall.</summary>
        void WallQuad(Vector3 center, Vector3 right, float halfWidth, float halfHeight, Color32 color)
        {
            var up = Vector3.up;
            mb.Quad(center - right * halfWidth - up * halfHeight, center - right * halfWidth + up * halfHeight,
                center + right * halfWidth + up * halfHeight, center + right * halfWidth - up * halfHeight, color);
        }

        static bool ClearLine(Vector3 from, Vector3 to, float stopShort = 0.4f)
        {
            var delta = to - from;
            float length = delta.magnitude;
            if (length <= stopShort) return true;
            return !Physics.Raycast(from, delta / length, length - stopShort, ShotMask, QueryTriggerInteraction.Ignore);
        }

        // ---------------------------------------------------------------- sky

        void BuildSky()
        {
            var sky = new MeshBuilder();
            const float radius = 1500f;
            const int rings = 18, segments = 28;
            for (int i = 0; i < rings; i++)
            {
                float e0 = Mathf.Lerp(-90f, 90f, i / (float)rings);
                float e1 = Mathf.Lerp(-90f, 90f, (i + 1) / (float)rings);
                Color32 c0 = SkyColor(e0), c1 = SkyColor(e1);
                for (int k = 0; k < segments; k++)
                {
                    float a0 = k / (float)segments * 360f, a1 = (k + 1) / (float)segments * 360f;
                    sky.Quad(Direction(e0, a0) * radius, Direction(e1, a0) * radius, Direction(e1, a1) * radius, Direction(e0, a1) * radius, c0, c1, c1, c0);
                }
            }

            if (Theme.Night)
            {
                // Stars: small camera-facing squares, fewer near the hazy horizon.
                for (int i = 0; i < 560; i++)
                {
                    float elevation = Mathf.Lerp(8f, 88f, Mathf.Sqrt((float)rng.NextDouble()));
                    var direction = Direction(elevation, Range(0f, 360f));
                    float size = Range(1.8f, 4.6f);
                    var tint = Color.Lerp(Theme.SkyTop, new Color(0.9f, 0.93f, 1f), Range(0.55f, 1f));
                    Billboard(sky, direction * (radius - 40f), direction, size, tint);
                }
            }
            else
            {
                // Clouds: flattened blobs low over the horizon.
                for (int i = 0; i < 16; i++)
                {
                    float azimuth = Range(15f, 165f);
                    float elevation = Range(4f, 20f);
                    var center = Direction(elevation, azimuth) * 1250f;
                    var cloudTop = Color.Lerp(Color.white, Theme.SkyHorizon, 0.25f);
                    var cloudBottom = Color.Lerp(Theme.SkyHorizon, Theme.SunColor, 0.35f);
                    int puffs = rng.Next(2, 5);
                    for (int p = 0; p < puffs; p++)
                    {
                        var offset = new Vector3(Range(-90f, 90f), Range(-6f, 14f), Range(-40f, 40f));
                        var radii = new Vector3(Range(50f, 120f), Range(14f, 26f), Range(40f, 70f));
                        sky.Sphere(center + offset, radii, 4, 8, cloudTop, cloudBottom);
                    }
                }
            }

            // Sun or moon.
            var sunDirection = Theme.SunDirection.normalized;
            var sunCenter = sunDirection * (radius - 60f);
            Disc(sky, sunCenter, sunDirection, Theme.SunDiscSize, Theme.SunDisc);

            // Far skyline ring, already tinted towards the fog so it reads as distance.
            var far = new List<(float distance, Vector3 center, Vector3 size, Color color)>();
            for (int i = 0; i < 70; i++)
            {
                float azimuth = Range(20f, 160f);
                float distance = district == 1 ? Range(1150f, 1350f) : Range(950f, 1350f);
                var direction = Direction(0f, azimuth);
                float height = district == 1 ? Range(20f, 90f) : Range(60f, 300f);
                var size = new Vector3(Range(40f, 110f), height, Range(40f, 90f));
                var color = Color.Lerp(Pick(Theme.Walls), Theme.FogColor, district == 2 ? 0.7f : 0.8f);
                far.Add((distance, direction * distance + Vector3.up * (height * 0.5f - 34f), size, color));
            }
            far.Sort((a, b) => b.distance.CompareTo(a.distance));
            foreach (var building in far) sky.Box(building.center, building.size, Tone(building.color), Tone(Color.Lerp(building.color, Color.white, 0.05f)), false);

            var go = sky.ToObject("Sky", Root.transform, SniperMaterials.Sky);
            SkyFollower = go.transform;
            go.transform.position = NestEye;
            go.AddComponent<SkyFollow>();

            // Soft glow around the sun, drawn after the city so buildings hide it.
            var glow = GlowQuad("Sun Glow", Theme.SunDiscSize * 5f, WithAlpha(Theme.SunDisc, Theme.Night ? 0.35f : 0.55f));
            glow.transform.SetParent(go.transform, false);
            glow.transform.localPosition = sunCenter * 0.98f;
            glow.transform.localRotation = Quaternion.LookRotation(sunDirection);
        }

        static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        Color32 SkyColor(float elevation)
        {
            if (elevation <= 0f) return Tone(Color.Lerp(Theme.SkyHorizon, Theme.SkyGround, Mathf.Clamp01(-elevation / 10f)));
            return Tone(Color.Lerp(Theme.SkyHorizon, Theme.SkyTop, Mathf.Pow(Mathf.Clamp01(elevation / 75f), 0.55f)));
        }

        /// <summary>Unit direction for an elevation and an azimuth measured from +x towards +z (90° is straight ahead).</summary>
        static Vector3 Direction(float elevation, float azimuth)
        {
            float e = elevation * Mathf.Deg2Rad, a = azimuth * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(e) * Mathf.Cos(a), Mathf.Sin(e), Mathf.Cos(e) * Mathf.Sin(a));
        }

        static void Billboard(MeshBuilder target, Vector3 center, Vector3 facing, float size, Color color)
        {
            var right = Vector3.Cross(Vector3.up, facing).normalized;
            var up = Vector3.Cross(facing, right).normalized;
            var c = MeshBuilder.Tone(color);
            target.Quad(center - right * size - up * size, center - right * size + up * size, center + right * size + up * size, center + right * size - up * size, c);
        }

        static void Disc(MeshBuilder target, Vector3 center, Vector3 facing, float radius, Color color)
        {
            var right = Vector3.Cross(Vector3.up, facing).normalized;
            var up = Vector3.Cross(facing, right).normalized;
            var c = MeshBuilder.Tone(color);
            const int steps = 24;
            for (int i = 0; i < steps; i++)
            {
                float a0 = i / (float)steps * Mathf.PI * 2f, a1 = (i + 1) / (float)steps * Mathf.PI * 2f;
                target.Triangle(center, center + (right * Mathf.Cos(a0) + up * Mathf.Sin(a0)) * radius,
                    center + (right * Mathf.Cos(a1) + up * Mathf.Sin(a1)) * radius, c);
            }
        }

        /// <summary>A textured quad using the additive material (the mesh builder has no UVs).</summary>
        static GameObject GlowQuad(string name, float size, Color color)
        {
            var mesh = new Mesh { name = name };
            float h = size * 0.5f;
            mesh.vertices = new[] { new Vector3(-h, -h, 0f), new Vector3(-h, h, 0f), new Vector3(h, h, 0f), new Vector3(h, -h, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            mesh.colors = new[] { color, color, color, color };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = SniperMaterials.Additive;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        // ---------------------------------------------------------------- ground

        void BuildGround()
        {
            // In the harbour the ground stops at the quay and water takes over (no overlap to z-fight).
            Flat(-1500f, -1500f, 1500f, district == 1 ? 348f : 1500f, 0f, Theme.Ground);
            Solid(new Vector3(0f, -0.5f, 0f), new Vector3(3000f, 1f, 3000f));

            // Street along x, with raised sidewalks.
            Flat(-1500f, 64f, 1500f, 80f, 0.02f, Theme.Road);
            mb.Box(new Vector3(0f, 0.08f, 62f), new Vector3(3000f, 0.16f, 4f), Tone(Shade(Theme.Sidewalk, 0.12f)), Tone(Theme.Sidewalk), false);
            mb.Box(new Vector3(0f, 0.08f, 82f), new Vector3(3000f, 0.16f, 4f), Tone(Shade(Theme.Sidewalk, 0.12f)), Tone(Theme.Sidewalk), false);
            for (float x = -420f; x < 420f; x += 9f)
                if (x < -14f || x > 14f) Flat(x, 71.8f, x + 4.2f, 72.2f, 0.03f, Theme.RoadLine);
            for (float x = -10f; x <= 10f; x += 2f) Flat(x - 0.45f, 65f, x + 0.45f, 79f, 0.03f, Theme.RoadLine);

            // Parking lot below the nest.
            Flat(-74f, 6f, 74f, 60f, 0.025f, Color.Lerp(Theme.Road, Theme.Sidewalk, 0.18f));
            foreach (float row in new[] { 20f, 34f, 48f })
                for (float x = -66f; x <= 66f; x += 3.2f)
                    Flat(x - 0.08f, row - 2.6f, x + 0.08f, row + 2.6f, 0.035f, Theme.RoadLine);

            // Grass either side of the lot, and a strip in front of the nest.
            Flat(-160f, 6f, -74f, 60f, 0.03f, Theme.Grass);
            Flat(74f, 6f, 160f, 60f, 0.03f, Theme.Grass);
            Flat(-74f, 0f, 74f, 6f, 0.03f, Theme.Grass);

            // Boulevard paving and the cross streets between the side blocks.
            Flat(-26f, 84f, 26f, 336f, 0.1f, Color.Lerp(Theme.Sidewalk, Theme.Ground, 0.2f));
            Flat(-9f - 1.3f, 98f, -9f + 1.3f, 330f, 0.11f, Theme.Grass);
            Flat(9f - 1.3f, 98f, 9f + 1.3f, 330f, 0.11f, Theme.Grass);
            // Seen from up to ~500 m away, so kept well clear of the ground plane.
            Flat(-420f, 166f, 420f, 182f, 0.08f, Theme.Road);
            Flat(-420f, 246f, 420f, 262f, 0.08f, Theme.Road);
            Flush("Ground");

            // Parked cars.
            foreach (float row in new[] { 20f, 34f, 48f })
                for (float x = -64f; x <= 64f; x += 3.2f)
                    if (Chance(0.42f)) Car(new Vector3(x, 0f, row), Chance(0.5f) ? 0f : 180f);
            for (float x = -150f; x <= 150f; x += Range(6f, 14f))
                if (Mathf.Abs(x) > 16f && Chance(0.55f)) Car(new Vector3(x, 0f, Chance(0.5f) ? 66.4f : 77.6f), 90f);

            // Trees on the grass, lamps along the sidewalks.
            for (int i = 0; i < 26; i++) Tree(new Vector3(Range(78f, 150f) * (i % 2 == 0 ? -1f : 1f), 0f, Range(10f, 56f)));
            for (float x = -120f; x <= 120f; x += 24f)
            {
                Lamp(new Vector3(x, 0.16f, 60.6f), 0f);
                Lamp(new Vector3(x + 12f, 0.16f, 83.4f), 180f);
            }
            groundExits.Add(new Vector3(-78f, 0f, 30f));
            groundExits.Add(new Vector3(78f, 0f, 30f));
            groundExits.Add(new Vector3(-160f, 0f, 62f));
            groundExits.Add(new Vector3(160f, 0f, 62f));
            Flush("Street Props");
        }

        void Car(Vector3 position, float yaw)
        {
            var saved = mb.Matrix;
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            mb.Matrix = Matrix4x4.TRS(position, rotation, Vector3.one);
            var paint = Pick(Theme.Cars);
            var glass = Color.Lerp(Theme.WindowGlass, Color.black, 0.2f);
            mb.Box(new Vector3(0f, 0.72f, 0f), new Vector3(1.8f, 0.72f, 4.3f), Tone(paint), Tone(Color.Lerp(paint, Color.white, 0.08f)), false);
            mb.TaperedBox(new Vector3(0f, 1.4f, -0.15f), new Vector3(1.6f, 0.64f, 2.3f), new Vector2(0.86f, 0.78f), Tone(glass), Tone(paint));
            var tyre = Tone(new Color(0.08f, 0.08f, 0.09f));
            foreach (var wheel in new[] { new Vector3(0.9f, 0.36f, 1.35f), new Vector3(-0.9f, 0.36f, 1.35f), new Vector3(0.9f, 0.36f, -1.35f), new Vector3(-0.9f, 0.36f, -1.35f) })
                mb.Tube(wheel - Vector3.right * 0.14f * Mathf.Sign(wheel.x), wheel + Vector3.right * 0.14f * Mathf.Sign(wheel.x), 0.36f, 8, tyre);
            float lights = Theme.Night ? 1f : 0f;
            mb.Box(new Vector3(0.58f, 0.82f, 2.16f), new Vector3(0.36f, 0.16f, 0.04f), Tone(new Color(1f, 0.95f, 0.8f), lights), Tone(Color.white));
            mb.Box(new Vector3(-0.58f, 0.82f, 2.16f), new Vector3(0.36f, 0.16f, 0.04f), Tone(new Color(1f, 0.95f, 0.8f), lights), Tone(Color.white));
            mb.Box(new Vector3(0.62f, 0.86f, -2.16f), new Vector3(0.3f, 0.14f, 0.04f), Tone(new Color(0.9f, 0.1f, 0.1f), lights * 0.8f), Tone(Color.red));
            mb.Box(new Vector3(-0.62f, 0.86f, -2.16f), new Vector3(0.3f, 0.14f, 0.04f), Tone(new Color(0.9f, 0.1f, 0.1f), lights * 0.8f), Tone(Color.red));
            mb.Matrix = saved;

            var go = new GameObject("Car");
            go.transform.SetParent(solidRoot, false);
            go.transform.localPosition = position + Vector3.up * 0.95f;
            go.transform.localRotation = rotation;
            go.AddComponent<BoxCollider>().size = new Vector3(1.8f, 1.5f, 4.3f);
        }

        void Tree(Vector3 position, float scale = 1f)
        {
            mb.Cylinder(position, 0.2f * scale, 2.4f * scale, 6, Tone(Shade(new Color(0.45f, 0.33f, 0.22f), 0.1f)), Tone(new Color(0.4f, 0.3f, 0.2f)), false);
            var leaves = Theme.Foliage;
            var top = Tone(Color.Lerp(leaves, Color.white, 0.12f));
            var bottom = Tone(Shade(leaves, 0.25f));
            mb.Sphere(position + Vector3.up * 3.6f * scale, new Vector3(2.1f, 2.2f, 2.1f) * scale, 4, 7, top, bottom);
            mb.Sphere(position + new Vector3(0.6f, 4.9f, -0.3f) * scale, new Vector3(1.3f, 1.4f, 1.3f) * scale, 4, 6, top, bottom);
            Solid(position + Vector3.up * 1.2f * scale, new Vector3(0.4f, 2.4f, 0.4f) * scale);
        }

        void Lamp(Vector3 position, float yaw)
        {
            var pole = Tone(Shade(Theme.Trim, 0.55f));
            mb.Cylinder(position, 0.09f, 6.2f, 6, pole, pole, false);
            var forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            mb.Box(position + Vector3.up * 6.1f + forward * 0.7f, new Vector3(0.12f, 0.12f, 1.5f), Quaternion.Euler(0f, yaw, 0f), pole, pole);
            var head = position + Vector3.up * 5.95f + forward * 1.35f;
            mb.Box(head, new Vector3(0.5f, 0.18f, 0.7f), Quaternion.Euler(0f, yaw, 0f), pole, pole);
            mb.Box(head - Vector3.up * 0.11f, new Vector3(0.4f, 0.04f, 0.56f), Quaternion.Euler(0f, yaw, 0f),
                Tone(new Color(1f, 0.92f, 0.7f), Theme.Night ? 1f : 0.2f), Tone(new Color(1f, 0.92f, 0.7f), Theme.Night ? 1f : 0.2f));
        }

        // ---------------------------------------------------------------- the nest

        void BuildNest()
        {
            var wall = Theme.Walls[0];
            var trim = Theme.Trim;
            mb.Box(new Vector3(0f, 16f, -17f), new Vector3(32f, 32f, 34f), Tone(wall), Tone(Theme.Roof));
            Solid(new Vector3(0f, 16f, -17f), new Vector3(32f, 32f, 34f));
            // Parapet around the roof; the sniper leans over the front one.
            SolidBox(new Vector3(0f, 32.55f, -0.25f), new Vector3(32f, 1.1f, 0.5f), Shade(wall, 0.1f), trim);
            SolidBox(new Vector3(-15.75f, 32.55f, -17f), new Vector3(0.5f, 1.1f, 34f), Shade(wall, 0.1f), trim);
            SolidBox(new Vector3(15.75f, 32.55f, -17f), new Vector3(0.5f, 1.1f, 34f), Shade(wall, 0.1f), trim);
            SolidBox(new Vector3(0f, 32.55f, -33.75f), new Vector3(32f, 1.1f, 0.5f), Shade(wall, 0.1f), trim);

            var metal = new Color(0.62f, 0.64f, 0.66f);
            SolidBox(new Vector3(-9f, 32.8f, -7f), new Vector3(3f, 1.6f, 2.2f), metal, Color.Lerp(metal, Color.white, 0.2f));
            SolidBox(new Vector3(-4.5f, 32.5f, -12f), new Vector3(1.8f, 1f, 1.4f), Shade(metal, 0.1f), metal);
            mb.Tube(new Vector3(6f, 32f, -5f), new Vector3(6f, 34.2f, -5f), 0.25f, 8, Tone(metal));
            mb.Tube(new Vector3(-12f, 32f, -26f), new Vector3(-12f, 42f, -26f), 0.12f, 6, Tone(Shade(metal, 0.3f)));

            // Water tower on stilts.
            var wood = new Color(0.55f, 0.38f, 0.26f);
            foreach (var leg in new[] { new Vector3(8.6f, 0f, -12.4f), new Vector3(11.4f, 0f, -12.4f), new Vector3(8.6f, 0f, -15.6f), new Vector3(11.4f, 0f, -15.6f) })
                mb.Box(new Vector3(leg.x, 33.8f, leg.z), new Vector3(0.25f, 3.6f, 0.25f), Tone(Shade(metal, 0.4f)), Tone(metal));
            mb.Cylinder(new Vector3(10f, 35.6f, -14f), 2.4f, 4f, 12, Tone(wood), Tone(Shade(wood, 0.2f)));
            mb.Cone(new Vector3(10f, 39.6f, -14f), 2.6f, 1.6f, 12, Tone(Shade(Theme.Roof, 0.1f)), Tone(Theme.Roof));

            // Neighbours either side of the nest.
            for (int side = -1; side <= 1; side += 2)
            {
                float cursor = 20f;
                for (int i = 0; i < 4; i++)
                {
                    float width = Range(16f, 28f), height = Range(18f, 44f);
                    float x0 = side > 0 ? cursor : -cursor - width, x1 = side > 0 ? cursor + width : -cursor;
                    Building(x0, -40f, x1, -3f, height, Pick(Theme.Walls), false, false);
                    cursor += width + Range(1f, 6f);
                }
            }
            Flush("Nest");
        }

        // ---------------------------------------------------------------- buildings

        /// <summary>A block with windows on the faces the nest can see, a cornice and (if low enough) a roof to stand on.</summary>
        Roof Building(float x0, float z0, float x1, float z1, float height, Color wall, bool shops, bool bandWindows)
        {
            var size = new Vector3(x1 - x0, height, z1 - z0);
            var center = new Vector3((x0 + x1) * 0.5f, height * 0.5f, (z0 + z1) * 0.5f);
            // Slightly darker at street level, as if in shade.
            mb.Box(center, size, Tone(wall), Tone(Theme.Roof));
            mb.Box(new Vector3(center.x, 0.6f, center.z), new Vector3(size.x + 0.08f, 1.2f, size.z + 0.08f), Tone(Shade(wall, 0.25f)), Tone(Shade(wall, 0.25f)), false);
            mb.Box(new Vector3(center.x, height + 0.12f, center.z), new Vector3(size.x + 0.5f, 0.24f, size.z + 0.5f), Tone(Theme.Trim), Tone(Theme.Trim));
            Solid(center, size);

            float eyeX = NestEye.x, eyeZ = NestEye.z;
            if (z0 > eyeZ) Facade(new Vector3(x0, 0f, z0), Vector3.right, Vector3.back, size.x, height, shops, bandWindows);
            if (x0 > eyeX + 1f) Facade(new Vector3(x0, 0f, z1), Vector3.back, Vector3.left, size.z, height, shops && size.z > 14f, bandWindows);
            if (x1 < eyeX - 1f) Facade(new Vector3(x1, 0f, z0), Vector3.forward, Vector3.right, size.z, height, shops && size.z > 14f, bandWindows);

            if (height > NestEye.y - 2.5f || height < 6f) return null;

            // A roof the sniper looks down onto: parapet, stair hut and clutter.
            const float parapet = 0.8f, thickness = 0.3f;
            var edge = Shade(wall, 0.08f);
            float top = height + parapet * 0.5f;
            SolidBox(new Vector3(center.x, top, z0 + thickness * 0.5f), new Vector3(size.x, parapet, thickness), edge, Theme.Trim);
            SolidBox(new Vector3(center.x, top, z1 - thickness * 0.5f), new Vector3(size.x, parapet, thickness), edge, Theme.Trim);
            SolidBox(new Vector3(x0 + thickness * 0.5f, top, center.z), new Vector3(thickness, parapet, size.z), edge, Theme.Trim);
            SolidBox(new Vector3(x1 - thickness * 0.5f, top, center.z), new Vector3(thickness, parapet, size.z), edge, Theme.Trim);

            var roof = new Roof { Id = roofs.Count + 1, X0 = x0, Z0 = z0, X1 = x1, Z1 = z1, Height = height };
            var hut = new Vector3(Chance(0.5f) ? x0 + 2.4f : x1 - 2.4f, height + 1.35f, z1 - 2.6f);
            SolidBox(hut, new Vector3(2.6f, 2.7f, 2.6f), Shade(wall, 0.15f), Theme.Roof);
            WallQuad(new Vector3(hut.x, height + 1.05f, hut.z - 1.32f), Vector3.right, 0.55f, 1.05f, Tone(Shade(Theme.WindowGlass, 0.3f)));
            roof.Hatch = new Vector3(hut.x, height, hut.z - 2f);
            if (Chance(0.7f))
            {
                var metal = new Color(0.6f, 0.62f, 0.65f);
                float x = Mathf.Lerp(x0 + 3f, x1 - 3f, (float)rng.NextDouble());
                SolidBox(new Vector3(x, height + 0.7f, Mathf.Lerp(z0 + 6f, z1 - 4f, (float)rng.NextDouble())), new Vector3(2.4f, 1.4f, 1.8f), metal, Color.Lerp(metal, Color.white, 0.2f));
            }
            roofs.Add(roof);
            return roof;
        }

        /// <summary>Windows, and optionally a shop front, on one face of a building.</summary>
        void Facade(Vector3 origin, Vector3 right, Vector3 outward, float width, float height, bool shop, bool bands)
        {
            const float floorHeight = 3.3f;
            int firstFloor = shop ? 1 : 0;
            int floors = Mathf.FloorToInt((height - 1f) / floorHeight);
            var glass = Theme.WindowGlass;
            var up = Vector3.up;

            if (bands)
            {
                // Distant towers: one glass band per floor, plus scattered lit windows.
                for (int f = firstFloor; f < floors; f++)
                {
                    var center = origin + right * (width * 0.5f) + up * ((f + 0.55f) * floorHeight) + outward * 0.12f;
                    WallQuad(center, right, width * 0.5f - 1.2f, floorHeight * 0.26f, Tone(glass));
                    if (Theme.LitWindowChance > 0.05f)
                    {
                        int lit = rng.Next(0, Mathf.Max(1, Mathf.RoundToInt(width / 6f * Theme.LitWindowChance * 2f)));
                        for (int i = 0; i < lit; i++)
                        {
                            var spot = center + right * Range(-width * 0.5f + 2f, width * 0.5f - 2f) + outward * 0.05f;
                            WallQuad(spot, right, Range(0.8f, 2.4f), floorHeight * 0.24f, Tone(Pick(Theme.WindowLights), 1f));
                        }
                    }
                }
                return;
            }

            int columns = Mathf.Max(1, Mathf.FloorToInt((width - 2f) / 3.2f));
            float spacing = (width - 2f) / columns;
            float halfWidth = Mathf.Min(0.8f, spacing * 0.3f);
            float halfHeight = floorHeight * 0.27f;
            for (int f = firstFloor; f < floors; f++)
            {
                for (int c = 0; c < columns; c++)
                {
                    var center = origin + right * (1f + (c + 0.5f) * spacing) + up * ((f + 0.55f) * floorHeight) + outward * 0.1f;
                    bool lit = Chance(Theme.LitWindowChance);
                    WallQuad(center, right, halfWidth, halfHeight, lit ? Tone(Pick(Theme.WindowLights), 1f) : Tone(glass));
                }
            }

            if (!shop) return;
            var shopCenter = origin + right * (width * 0.5f) + up * 1.6f + outward * 0.1f;
            WallQuad(shopCenter, right, width * 0.5f - 1.2f, 1.2f, Theme.Night ? Tone(Pick(Theme.WindowLights), 0.85f) : Tone(Shade(glass, 0.2f)));
            var awningColor = Pick(Theme.Cars);
            mb.Box(origin + right * (width * 0.5f) + up * 3.25f + outward * 0.7f, new Vector3(width - 2f, 0.3f, 1.4f), Quaternion.LookRotation(outward), Tone(awningColor), Tone(Color.Lerp(awningColor, Color.white, 0.15f)));
            groundExits.Add(origin + right * (width * 0.5f) + outward * 0.8f);

            if (district == 2 && Chance(0.6f))
            {
                // Neon: a vertical blade sign and a glowing band over the shop.
                var neon = Pick(Theme.Neon);
                float length = Mathf.Min(height - 5f, Range(6f, 12f));
                if (length > 3f)
                {
                    var blade = origin + right * Range(1.5f, Mathf.Max(1.6f, width - 1.5f)) + up * (4.5f + length * 0.5f) + outward * 0.8f;
                    mb.Box(blade, new Vector3(0.3f, length, 1.3f), Quaternion.LookRotation(outward), Tone(neon, 1f), Tone(neon, 1f));
                }
                mb.Box(origin + right * (width * 0.5f) + up * 4.1f + outward * 0.25f, new Vector3(width * 0.6f, 0.5f, 0.2f), Quaternion.LookRotation(outward), Tone(Pick(Theme.Neon), 1f), Tone(neon, 1f));
            }
        }

        void BuildStreetBlock()
        {
            // Shops and low offices across the street, split by the boulevard.
            for (int side = -1; side <= 1; side += 2)
            {
                float cursor = 28f;
                while (cursor < 270f)
                {
                    float width = Range(16f, 30f);
                    float depth = Range(16f, 26f);
                    float height = cursor < 110f ? Range(9f, 24f) : Range(12f, 36f);
                    float x0 = side > 0 ? cursor : -cursor - width;
                    float x1 = side > 0 ? cursor + width : -cursor;
                    Building(x0, 88f, x1, 88f + depth, height, Pick(Theme.Walls), true, false);
                    cursor += width + (Chance(0.3f) ? Range(4f, 7f) : 0.6f);
                    FlushIfBig("Street Block");
                }
            }
            Flush("Street Block");
        }

        void BuildBoulevard()
        {
            // Fountain.
            var stone = Color.Lerp(Theme.Trim, Theme.Ground, 0.3f);
            mb.Cylinder(new Vector3(0f, 0.1f, 150f), 6.5f, 0.9f, 20, Tone(stone), Tone(Theme.Water, 0.25f));
            mb.Cylinder(new Vector3(0f, 1f, 150f), 0.9f, 3.2f, 10, Tone(stone), Tone(stone));
            mb.Cylinder(new Vector3(0f, 4.2f, 150f), 2.3f, 0.45f, 14, Tone(stone), Tone(Theme.Water, 0.3f));
            Solid(new Vector3(0f, 0.55f, 150f), new Vector3(12f, 0.9f, 12f));
            Solid(new Vector3(0f, 2.6f, 150f), new Vector3(1.8f, 3.4f, 1.8f));

            // Monument at the far end.
            mb.TaperedBox(new Vector3(0f, 2f, 326f), new Vector3(8f, 4f, 8f), new Vector2(0.8f, 0.8f), Tone(stone), Tone(stone));
            mb.TaperedBox(new Vector3(0f, 18f, 326f), new Vector3(3f, 28f, 3f), new Vector2(0.45f, 0.45f), Tone(Color.Lerp(stone, Color.white, 0.1f)), Tone(stone));
            Solid(new Vector3(0f, 2f, 326f), new Vector3(8f, 4f, 8f));
            Solid(new Vector3(0f, 18f, 326f), new Vector3(2.6f, 28f, 2.6f));

            for (float z = 104f; z < 326f; z += 14f)
            {
                if (Mathf.Abs(z - 150f) < 12f) continue;
                Tree(new Vector3(-9f, 0.1f, z), 0.9f);
                Tree(new Vector3(9f, 0.1f, z), 0.9f);
            }
            for (float z = 100f; z < 330f; z += 26f)
            {
                Lamp(new Vector3(-24.5f, 0.1f, z), 90f);
                Lamp(new Vector3(24.5f, 0.1f, z + 13f), -90f);
            }

            // Kiosks.
            foreach (var spot in new[] { new Vector3(-16f, 0.1f, 205f), new Vector3(16f, 0.1f, 272f) })
            {
                var colour = Pick(Theme.Cars);
                SolidBox(spot + Vector3.up * 1.3f, new Vector3(2.8f, 2.6f, 2.8f), Color.Lerp(Theme.Trim, colour, 0.25f), colour);
                mb.Box(spot + new Vector3(0f, 2.75f, 0f), new Vector3(3.4f, 0.3f, 3.4f), Tone(colour), Tone(Color.Lerp(colour, Color.white, 0.2f)));
            }
            groundExits.Add(new Vector3(-30f, 0f, 174f));
            groundExits.Add(new Vector3(30f, 0f, 174f));
            groundExits.Add(new Vector3(-30f, 0f, 254f));
            groundExits.Add(new Vector3(30f, 0f, 254f));
            groundExits.Add(new Vector3(0f, 0f, 342f));
            Flush("Boulevard");
        }

        void BuildSideBlocks()
        {
            // Starts behind the street block (which reaches z = 114). The harbour keeps its last block clear
            // for the container yard.
            float[][] blocks = { new[] { 120f, 164f }, new[] { 184f, 244f }, new[] { 264f, 334f } };
            int blockCount = district == 1 ? 2 : 3;
            for (int side = -1; side <= 1; side += 2)
            {
                for (int b = 0; b < blockCount; b++)
                {
                    var block = blocks[b];
                    float z = block[0];
                    while (z < block[1] - 10f)
                    {
                        float depth = Mathf.Min(Range(14f, 26f), block[1] - z);
                        float inner = Range(18f, 30f);
                        bool harbour = district == 1;
                        float height = harbour ? Range(8f, 18f) : Range(13f, 38f);
                        float x0 = side > 0 ? 30f : -30f - inner, x1 = side > 0 ? 30f + inner : -30f;
                        Building(x0, z, x1, z + depth, height, Pick(Theme.Walls), true, false);

                        // Taller buildings behind, mostly seen over the others.
                        float outer = Range(22f, 40f);
                        float ox0 = side > 0 ? x1 + 4f : x0 - 4f - outer, ox1 = side > 0 ? x1 + 4f + outer : x0 - 4f;
                        Building(ox0, z, ox1, z + depth, harbour ? Range(12f, 26f) : Range(24f, 58f), Pick(Theme.Walls), false, false);
                        z += depth + Range(0.5f, 3f);
                        FlushIfBig("Side Block");
                    }
                }
            }
            Flush("Side Block");
        }

        void BuildSkyline()
        {
            for (int i = 0; i < 46; i++)
            {
                float z = Range(350f, 720f);
                float x = Range(-420f, 420f);
                float width = Range(20f, 46f), depth = Range(20f, 40f);
                float height = Range(40f, 170f) * Mathf.Lerp(0.8f, 1.15f, (z - 350f) / 370f);
                Building(x - width * 0.5f, z, x + width * 0.5f, z + depth, height, Pick(Theme.Walls), false, true);
                if (height > 110f)
                {
                    var tip = new Vector3(x, height, z + depth * 0.5f);
                    mb.Tube(tip, tip + Vector3.up * Range(10f, 24f), 0.35f, 6, Tone(Shade(Theme.Trim, 0.4f)));
                    mb.Box(tip + Vector3.up * 24.5f, Vector3.one * 1.2f, Tone(new Color(1f, 0.2f, 0.15f), Theme.Night ? 1f : 0.6f), Tone(new Color(1f, 0.2f, 0.15f), 1f));
                }
                FlushIfBig("Skyline");
            }
            Flush("Skyline");
        }

        void BuildHarbourFront()
        {
            // Quay, water to the horizon, cranes, containers and a moored ship.
            SolidBox(new Vector3(0f, -0.5f, 342f), new Vector3(1600f, 1.4f, 12f), Shade(Theme.Ground, 0.2f), Theme.Sidewalk);
            Flat(-1500f, 348f, 1500f, 1500f, -0.3f, Theme.Water, 0.15f);

            var containerColors = new[] { new Color(0.78f, 0.25f, 0.2f), new Color(0.2f, 0.42f, 0.7f), new Color(0.25f, 0.55f, 0.35f), new Color(0.9f, 0.55f, 0.2f), new Color(0.55f, 0.55f, 0.58f) };
            for (int i = 0; i < 40; i++)
            {
                float x = Range(-240f, 240f);
                if (Mathf.Abs(x) < 36f) continue;
                float z = Range(270f, 334f);
                int stack = rng.Next(1, 4);
                bool along = Chance(0.5f);
                for (int s = 0; s < stack; s++)
                {
                    var colour = Pick(containerColors);
                    var size = along ? new Vector3(12.2f, 2.6f, 2.45f) : new Vector3(2.45f, 2.6f, 12.2f);
                    SolidBox(new Vector3(x, 1.3f + s * 2.6f, z), size, colour, Color.Lerp(colour, Color.white, 0.12f));
                }
                FlushIfBig("Harbour");
            }

            foreach (float x in new[] { -120f, 70f, 190f })
            {
                var yellow = new Color(0.95f, 0.72f, 0.18f);
                foreach (float dx in new[] { -7f, 7f })
                    foreach (float dz in new[] { -5f, 5f })
                        mb.Box(new Vector3(x + dx, 20f, 352f + dz), new Vector3(1.2f, 40f, 1.2f), Tone(yellow), Tone(yellow));
                mb.Box(new Vector3(x, 40.5f, 352f), new Vector3(16f, 2.2f, 12f), Tone(Shade(yellow, 0.1f)), Tone(yellow));
                mb.Box(new Vector3(x, 42f, 380f), new Vector3(3f, 2f, 70f), Tone(yellow), Tone(yellow));
                mb.Box(new Vector3(x, 44f, 348f), new Vector3(5f, 3f, 5f), Tone(new Color(0.85f, 0.2f, 0.18f)), Tone(new Color(0.85f, 0.2f, 0.18f)));
            }

            // Cargo ship.
            var hull = new Color(0.22f, 0.26f, 0.34f);
            mb.TaperedBox(new Vector3(-40f, 4f, 520f), new Vector3(170f, 14f, 30f), new Vector2(1.04f, 1.05f), Tone(hull), Tone(new Color(0.6f, 0.2f, 0.18f)));
            mb.Box(new Vector3(-100f, 17f, 520f), new Vector3(22f, 12f, 24f), Tone(Theme.Trim), Tone(Theme.Trim));
            for (int i = 0; i < 9; i++)
            {
                var colour = Pick(containerColors);
                mb.Box(new Vector3(-70f + i * 14f, 13f + (i % 3) * 1.3f, 520f), new Vector3(12f, 4f + (i % 3) * 2.6f, 26f), Tone(colour), Tone(colour));
            }

            // Lighthouse.
            for (int band = 0; band < 6; band++)
                mb.Cylinder(new Vector3(-260f, band * 6f, 470f), 4.5f - band * 0.35f, 6f, 12, Tone(band % 2 == 0 ? Color.white : new Color(0.85f, 0.2f, 0.2f)), Tone(Color.white), false);
            mb.Cylinder(new Vector3(-260f, 36f, 470f), 2.2f, 3f, 10, Tone(new Color(1f, 0.9f, 0.6f), 1f), Tone(Theme.Roof));
            Flush("Harbour");
        }

        // ---------------------------------------------------------------- where robots can stand

        void FindSpawns()
        {
            // Parking lot aisles (the cars sit on the rows between them).
            foreach (float z in new[] { 27f, 41f, 55f })
                for (float x = -62f; x <= 62f; x += 8f)
                    AddSpawn(new Vector3(x + Range(-1.5f, 1.5f), 0f, z), SpawnArea.Lot, 0, Vector3.right, new Rect(-70f, 8f, 140f, 52f));

            // Sidewalks on both sides of the street.
            for (float x = -110f; x <= 110f; x += 7f)
            {
                AddSpawn(new Vector3(x, 0.16f, 62f), SpawnArea.Street, 0, Vector3.right, new Rect(-130f, 60f, 260f, 4f));
                AddSpawn(new Vector3(x + 3.5f, 0.16f, 82f), SpawnArea.Street, 0, Vector3.right, new Rect(-130f, 80f, 260f, 4f));
            }

            // Boulevard lanes, clear of the fountain and monument.
            foreach (float x in new[] { -20f, -3.5f, 3.5f, 20f })
                for (float z = 92f; z <= 318f; z += 9f)
                {
                    if (Mathf.Abs(z - 150f) < 11f && Mathf.Abs(x) < 10f) continue;
                    AddSpawn(new Vector3(x, 0.1f, z), SpawnArea.Boulevard, 0, Vector3.forward, new Rect(x - 1f, 88f, 2f, 234f));
                }

            // Rooftops.
            foreach (var roof in roofs)
            {
                var bounds = new Rect(roof.X0 + 2f, roof.Z0 + 2f, roof.X1 - roof.X0 - 4f, roof.Z1 - roof.Z0 - 4f);
                if (bounds.width < 4f || bounds.height < 4f) continue;
                float y = roof.Height;
                float midX = (roof.X0 + roof.X1) * 0.5f;
                foreach (var spot in new[]
                         {
                             new Vector3(midX, y, roof.Z0 + 3f), new Vector3(roof.X0 + 3f, y, roof.Z0 + 3.5f),
                             new Vector3(roof.X1 - 3f, y, roof.Z0 + 3.5f), new Vector3(midX, y, (roof.Z0 + roof.Z1) * 0.5f)
                         })
                    AddSpawn(spot, SpawnArea.Rooftop, roof.Id, Vector3.right, bounds);
            }
        }

        void AddSpawn(Vector3 feet, SpawnArea area, int surface, Vector3 patrolAxis, Rect walkable)
        {
            var toFeet = feet - NestEye;
            float yaw = Mathf.Atan2(toFeet.x, toFeet.z) * Mathf.Rad2Deg;
            if (Mathf.Abs(yaw) > MaxYaw - 8f) return;
            // Must be reachable by the scope, not just visible.
            var toChest = feet + Vector3.up * 1.2f - NestEye;
            float pitch = Mathf.Atan2(toChest.y, new Vector2(toChest.x, toChest.z).magnitude) * Mathf.Rad2Deg;
            if (pitch < MinPitch + 4f) return;
            if (!CanSee(feet)) return;

            var spawn = new SpawnPoint
            {
                Position = feet,
                Area = area,
                Surface = surface,
                Distance = Vector3.Distance(NestEye, feet + Vector3.up * 1.2f)
            };

            for (int attempt = 0; attempt < 4; attempt++)
            {
                float length = Range(7f, 18f) * (attempt % 2 == 0 ? 1f : -1f);
                var target = feet + patrolAxis * length;
                if (!walkable.Contains(new Vector2(target.x, target.z))) continue;
                if (!CanSee(target) || !ClearLine(feet + Vector3.up * 0.6f, target + Vector3.up * 0.6f, 0f)) continue;
                spawn.CanPatrol = true;
                spawn.PatrolTarget = target;
                break;
            }

            spawn.Exit = ExitFor(feet, surface);
            Spawns.Add(spawn);
        }

        /// <summary>True when both the head and the chest of a robot standing here are in the sniper's line of sight.</summary>
        public static bool CanSee(Vector3 feet) =>
            ClearLine(NestEye, feet + Vector3.up * 1.75f) && ClearLine(NestEye, feet + Vector3.up * 1.15f);

        Vector3 ExitFor(Vector3 feet, int surface)
        {
            if (surface > 0) return roofs[surface - 1].Hatch;
            Vector3 best = groundExits[0];
            float bestScore = float.MaxValue;
            foreach (var exit in groundExits)
            {
                float distance = Vector3.Distance(new Vector3(feet.x, 0f, feet.z), new Vector3(exit.x, 0f, exit.z));
                // Prefer an exit a short run away: close enough to be a threat, far enough to react to.
                float score = distance < 12f ? 1000f + distance : Mathf.Abs(distance - 28f);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = new Vector3(exit.x, feet.y, exit.z);
                }
            }
            return best;
        }

        /// <summary>Spawn points for a mission, spread apart and inside the range band (widened if the band is too thin).</summary>
        public List<SpawnPoint> Pick(int count, float minRange, float maxRange, ICollection<SpawnPoint> taken, Random random, float spacing = 7f)
        {
            var result = new List<SpawnPoint>();
            for (int widen = 0; widen < 8 && result.Count < count; widen++)
            {
                float min = minRange * (1f - widen * 0.12f), max = maxRange * (1f + widen * 0.1f);
                var candidates = new List<SpawnPoint>();
                foreach (var spawn in Spawns)
                    if (spawn.Distance >= min && spawn.Distance <= max && !taken.Contains(spawn) && !result.Contains(spawn))
                        candidates.Add(spawn);
                // Shuffle, then take the ones far enough from everyone already placed.
                for (int i = candidates.Count - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
                }
                foreach (var candidate in candidates)
                {
                    if (result.Count >= count) break;
                    bool clear = true;
                    foreach (var other in taken)
                        if (Vector3.Distance(other.Position, candidate.Position) < spacing) { clear = false; break; }
                    if (clear)
                        foreach (var other in result)
                            if (Vector3.Distance(other.Position, candidate.Position) < spacing) { clear = false; break; }
                    if (clear) result.Add(candidate);
                }
            }
            return result;
        }

        /// <summary>A visible spot for an explosive barrel next to a robot, on the same surface.</summary>
        public bool BarrelSpot(SpawnPoint near, Random random, out Vector3 spot)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                var candidate = near.Position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (1.6f + (float)random.NextDouble() * 1.4f);
                if (!ClearLine(NestEye, candidate + Vector3.up * 0.7f)) continue;
                if (Physics.CheckBox(candidate + Vector3.up * 0.65f, new Vector3(0.4f, 0.55f, 0.4f), Quaternion.identity, ShotMask, QueryTriggerInteraction.Ignore)) continue;
                if (near.Surface > 0)
                {
                    var roof = roofs[near.Surface - 1];
                    if (candidate.x < roof.X0 + 1f || candidate.x > roof.X1 - 1f || candidate.z < roof.Z0 + 1f || candidate.z > roof.Z1 - 1f) continue;
                }
                spot = candidate;
                return true;
            }
            spot = default;
            return false;
        }

        public void Destroy()
        {
            if (Root != null) Object.Destroy(Root);
        }
    }

    /// <summary>Keeps the sky dome centred on the active camera so it always looks infinitely far away.</summary>
    public sealed class SkyFollow : MonoBehaviour
    {
        public Transform Target;

        void LateUpdate()
        {
            if (Target != null) transform.position = Target.position;
        }
    }
}
