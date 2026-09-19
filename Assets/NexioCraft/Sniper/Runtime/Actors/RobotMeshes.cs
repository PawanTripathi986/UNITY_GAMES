using System.Collections.Generic;
using NexioCraft.Core;
using UnityEngine;

namespace NexioCraft.Sniper
{
    public sealed class RobotMeshSet
    {
        public Mesh Hips, Leg, Torso, Head, ArmLeft, ArmRight, Visor;
    }

    /// <summary>Robot part meshes, built once per look and shared by every robot wearing it.</summary>
    public static class RobotMeshes
    {
        static readonly Dictionary<int, RobotMeshSet> cache = new Dictionary<int, RobotMeshSet>();
        static Mesh shadowMesh;

        static readonly Color Gunmetal = Palette.Hex(0x59606E);
        static readonly Color GunmetalDark = Palette.Hex(0x2E323B);
        static readonly Color Warning = Palette.Hex(0xE2463C);
        static readonly Color VisorRed = Palette.Hex(0xFF3B30);
        static readonly Color Gold = Palette.Hex(0xE3B341);
        static readonly Color GoldDark = Palette.Hex(0x8A6A1F);
        static readonly Color Crimson = Palette.Hex(0xB3202F);
        static readonly Color VisorViolet = Palette.Hex(0xD04BFF);
        static readonly Color VisorCyan = Palette.Hex(0x5BE3FF);
        static readonly Color[] CivilianColors = { Palette.Hex(0xF2C14E), Palette.Hex(0x3FB8AF), Palette.Hex(0xE9E4DA) };

        public static RobotMeshSet For(RobotRole role, int variant)
        {
            int key = (int)role * 10 + (role == RobotRole.Civilian ? Mathf.Abs(variant) % CivilianColors.Length : 0);
            if (cache.TryGetValue(key, out var set) && set.Hips != null) return set;
            set = role == RobotRole.Civilian ? BuildCivilian(Mathf.Abs(variant) % CivilianColors.Length) : BuildHostile(role == RobotRole.Boss);
            cache[key] = set;
            return set;
        }

        static Color32 T(Color color, float glow = 0f) => MeshBuilder.Tone(color, glow);

        static Mesh Take(MeshBuilder mb, string name)
        {
            var mesh = mb.ToMesh(name);
            mb.Clear();
            return mesh;
        }

        static RobotMeshSet BuildHostile(bool boss)
        {
            var body = boss ? Gold : Gunmetal;
            var dark = boss ? GoldDark : GunmetalDark;
            var accent = boss ? Crimson : Warning;
            var glow = boss ? VisorViolet : VisorRed;
            var set = new RobotMeshSet();
            var mb = new MeshBuilder();

            mb.Box(Vector3.zero, new Vector3(0.44f, 0.2f, 0.28f), T(dark), T(dark));
            mb.Box(new Vector3(0f, -0.02f, 0.15f), new Vector3(0.2f, 0.11f, 0.03f), T(accent), T(accent));
            set.Hips = Take(mb, "Robot Hips");

            mb.Box(new Vector3(0f, -0.24f, 0f), new Vector3(0.2f, 0.46f, 0.24f), T(body), T(body));
            mb.Box(new Vector3(0f, -0.47f, 0.12f), new Vector3(0.16f, 0.14f, 0.06f), T(accent), T(accent));
            mb.Box(new Vector3(0f, -0.7f, 0f), new Vector3(0.17f, 0.42f, 0.2f), T(dark), T(dark));
            mb.Box(new Vector3(0f, -0.92f, 0.06f), new Vector3(0.24f, 0.1f, 0.38f), T(dark), T(body));
            set.Leg = Take(mb, "Robot Leg");

            mb.TaperedBox(new Vector3(0f, 0.3f, 0f), new Vector3(0.44f, 0.6f, 0.28f), new Vector2(1.36f, 1.25f), T(body), T(body));
            mb.Box(new Vector3(0f, 0.36f, 0.19f), new Vector3(0.34f, 0.3f, 0.05f), T(accent), T(accent));
            mb.Box(new Vector3(0f, 0.42f, 0.217f), new Vector3(0.09f, 0.09f, 0.02f), T(glow, 1f), T(glow, 1f));
            mb.Box(new Vector3(0f, 0.34f, -0.22f), new Vector3(0.34f, 0.42f, 0.16f), T(dark), T(dark));
            mb.Box(new Vector3(-0.36f, 0.6f, 0f), new Vector3(0.22f, 0.13f, 0.32f), T(boss ? accent : dark), T(boss ? accent : body));
            mb.Box(new Vector3(0.36f, 0.6f, 0f), new Vector3(0.22f, 0.13f, 0.32f), T(boss ? accent : dark), T(boss ? accent : body));
            if (boss) mb.TaperedBox(new Vector3(0f, 0.12f, -0.33f), new Vector3(0.74f, 1f, 0.05f), new Vector2(0.72f, 1f), T(Crimson), T(Crimson));
            set.Torso = Take(mb, "Robot Torso");

            mb.Cylinder(Vector3.zero, 0.07f, 0.08f, 8, T(dark), T(dark));
            mb.Box(new Vector3(0f, 0.21f, 0f), new Vector3(0.34f, 0.28f, 0.32f), T(body), T(Color.Lerp(body, Color.white, 0.12f)));
            mb.Box(new Vector3(0f, 0.08f, 0.05f), new Vector3(0.28f, 0.08f, 0.26f), T(dark), T(dark));
            mb.Box(new Vector3(0f, 0.22f, 0.165f), new Vector3(0.3f, 0.12f, 0.02f), T(Color.black), T(Color.black));
            mb.Box(new Vector3(-0.185f, 0.2f, 0f), new Vector3(0.04f, 0.12f, 0.12f), T(accent), T(accent));
            mb.Box(new Vector3(0.185f, 0.2f, 0f), new Vector3(0.04f, 0.12f, 0.12f), T(accent), T(accent));
            mb.Tube(new Vector3(0.1f, 0.35f, -0.06f), new Vector3(0.13f, 0.56f, -0.08f), 0.014f, 5, T(dark));
            mb.Sphere(new Vector3(0.13f, 0.58f, -0.08f), Vector3.one * 0.035f, 3, 6, T(glow, 1f));
            if (boss)
            {
                var crown = Color.Lerp(Gold, Color.white, 0.25f);
                mb.Cone(new Vector3(-0.11f, 0.35f, 0f), 0.05f, 0.13f, 6, T(crown), T(crown));
                mb.Cone(new Vector3(0f, 0.35f, 0f), 0.06f, 0.2f, 6, T(crown), T(crown));
                mb.Cone(new Vector3(0.11f, 0.35f, 0f), 0.05f, 0.13f, 6, T(crown), T(crown));
            }
            set.Head = Take(mb, "Robot Head");

            mb.Box(new Vector3(0f, 0.22f, 0.18f), new Vector3(0.26f, 0.07f, 0.025f), T(glow, 1f), T(glow, 1f));
            set.Visor = Take(mb, "Robot Visor");

            Arm(mb, body, dark);
            set.ArmLeft = Take(mb, "Robot Arm L");
            Arm(mb, body, dark);
            if (!boss)
            {
                var blaster = Palette.Hex(0x1C1F26);
                mb.Box(new Vector3(0f, -0.78f, 0.07f), new Vector3(0.09f, 0.46f, 0.12f), T(blaster), T(blaster));
                mb.Box(new Vector3(0f, -0.66f, 0.15f), new Vector3(0.06f, 0.12f, 0.08f), T(accent), T(accent));
                mb.Tube(new Vector3(0f, -1f, 0.07f), new Vector3(0f, -1.16f, 0.07f), 0.03f, 6, T(blaster));
                mb.Box(new Vector3(0f, -1.17f, 0.07f), new Vector3(0.05f, 0.02f, 0.05f), T(glow, 1f), T(glow, 1f));
            }
            set.ArmRight = Take(mb, "Robot Arm R");
            return set;
        }

        static void Arm(MeshBuilder mb, Color body, Color dark)
        {
            mb.Box(new Vector3(0f, -0.17f, 0f), new Vector3(0.14f, 0.34f, 0.16f), T(body), T(body));
            mb.Box(new Vector3(0f, -0.47f, 0f), new Vector3(0.12f, 0.3f, 0.14f), T(dark), T(dark));
            mb.Box(new Vector3(0f, -0.67f, 0.01f), new Vector3(0.13f, 0.12f, 0.14f), T(body), T(body));
        }

        static RobotMeshSet BuildCivilian(int variant)
        {
            var body = CivilianColors[variant];
            var light = Color.Lerp(body, Color.white, 0.25f);
            var dark = Palette.Hex(0x3A3F4A);
            var set = new RobotMeshSet();
            var mb = new MeshBuilder();

            mb.Sphere(Vector3.zero, new Vector3(0.22f, 0.12f, 0.17f), 3, 8, T(dark));
            set.Hips = Take(mb, "Civilian Hips");

            mb.Box(new Vector3(0f, -0.25f, 0f), new Vector3(0.14f, 0.46f, 0.16f), T(body), T(body));
            mb.Box(new Vector3(0f, -0.68f, 0f), new Vector3(0.12f, 0.42f, 0.14f), T(dark), T(dark));
            mb.Sphere(new Vector3(0f, -0.9f, 0.05f), new Vector3(0.12f, 0.07f, 0.18f), 3, 7, T(dark));
            set.Leg = Take(mb, "Civilian Leg");

            mb.Sphere(new Vector3(0f, 0.32f, 0f), new Vector3(0.29f, 0.35f, 0.24f), 5, 10, T(light), T(body));
            mb.Box(new Vector3(0f, 0.3f, 0.215f), new Vector3(0.2f, 0.17f, 0.04f), T(Color.white), T(Color.white));
            mb.Box(new Vector3(-0.05f, 0.32f, 0.238f), new Vector3(0.04f, 0.04f, 0.01f), T(VisorCyan, 1f), T(VisorCyan, 1f));
            mb.Box(new Vector3(0.05f, 0.32f, 0.238f), new Vector3(0.04f, 0.04f, 0.01f), T(Palette.Hex(0x7CFF8A), 1f), T(Palette.Hex(0x7CFF8A), 1f));
            set.Torso = Take(mb, "Civilian Torso");

            mb.Cylinder(Vector3.zero, 0.06f, 0.08f, 8, T(dark), T(dark));
            mb.Sphere(new Vector3(0f, 0.2f, 0f), new Vector3(0.17f, 0.16f, 0.16f), 4, 10, T(light), T(body));
            if (variant == 0)
            {
                var hat = Palette.Hex(0xFF8A2A);
                mb.Sphere(new Vector3(0f, 0.29f, 0f), new Vector3(0.19f, 0.11f, 0.19f), 3, 10, T(hat));
                mb.Cylinder(new Vector3(0f, 0.26f, 0f), 0.23f, 0.02f, 12, T(hat), T(hat));
            }
            else
            {
                mb.Tube(new Vector3(0f, 0.34f, 0f), new Vector3(0f, 0.48f, 0f), 0.012f, 5, T(dark));
                mb.Sphere(new Vector3(0f, 0.5f, 0f), Vector3.one * 0.03f, 3, 6, T(VisorCyan, 1f));
            }
            set.Head = Take(mb, "Civilian Head");

            mb.Box(new Vector3(0f, 0.21f, 0.15f), new Vector3(0.2f, 0.06f, 0.03f), T(VisorCyan, 1f), T(VisorCyan, 1f));
            set.Visor = Take(mb, "Civilian Visor");

            CivilianArm(mb, body, dark);
            set.ArmLeft = Take(mb, "Civilian Arm L");
            CivilianArm(mb, body, dark);
            if (variant == 1)
            {
                var cardboard = Palette.Hex(0xC08A55);
                mb.Box(new Vector3(0f, -0.72f, 0.16f), new Vector3(0.3f, 0.24f, 0.26f), T(cardboard), T(Color.Lerp(cardboard, Color.white, 0.15f)));
            }
            set.ArmRight = Take(mb, "Civilian Arm R");
            return set;
        }

        static void CivilianArm(MeshBuilder mb, Color body, Color dark)
        {
            mb.Box(new Vector3(0f, -0.18f, 0f), new Vector3(0.1f, 0.34f, 0.12f), T(body), T(body));
            mb.Box(new Vector3(0f, -0.47f, 0f), new Vector3(0.09f, 0.3f, 0.1f), T(dark), T(dark));
            mb.Sphere(new Vector3(0f, -0.66f, 0.01f), Vector3.one * 0.065f, 3, 6, T(body));
        }

        /// <summary>Soft dark blob under a robot's feet.</summary>
        public static GameObject CreateShadow(Transform parent, float size)
        {
            if (shadowMesh == null)
            {
                shadowMesh = new Mesh { name = "Blob Shadow" };
                const float h = 0.5f;
                shadowMesh.vertices = new[] { new Vector3(-h, 0f, -h), new Vector3(-h, 0f, h), new Vector3(h, 0f, h), new Vector3(h, 0f, -h) };
                shadowMesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
                var shade = new Color(0f, 0f, 0f, 0.5f);
                shadowMesh.colors = new[] { shade, shade, shade, shade };
                shadowMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                shadowMesh.RecalculateBounds();
            }
            var go = new GameObject("Shadow");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            go.transform.localScale = new Vector3(size, 1f, size);
            go.AddComponent<MeshFilter>().sharedMesh = shadowMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = SniperMaterials.Decal;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }
    }
}
