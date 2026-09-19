using System.Collections.Generic;
using NexioCraft.Core;
using UnityEngine;

namespace NexioCraft.Sniper
{
    public enum SniperIcon { Scope, Breath, Bullet, Coin, Wind, Lock, Upgrade, Target, Clock, Power, Stability, Reload, Computer }

    /// <summary>Procedural 2D art for Steel Sniper: app icon, scope mask, rifle and HUD icons.</summary>
    public static class SniperArt
    {
        public static readonly Color Accent = Palette.Hex(0x2A8C73);
        public static readonly Color Hostile = Palette.Hex(0xFF4B3E);
        public static readonly Color Civilian = Palette.Hex(0x5BE3FF);
        public static readonly Color Boss = Palette.Hex(0xFFC53D);
        public static readonly Color Coin = Palette.Hex(0xFFC53D);

        static readonly Dictionary<SniperIcon, Sprite> icons = new Dictionary<SniperIcon, Sprite>();
        static Texture2D scopeMask;
        static Sprite rifle;

        public static Raster AppIcon(int size)
        {
            float s = size / 256f;
            var r = new Raster(size, size, Color.clear);
            var backBottom = Palette.Hex(0x0C1330);
            var backTop = Palette.Hex(0x22305E);
            var back = Paint.Vertical(backBottom, backTop, 0f, size);
            r.RoundRect(size * 0.5f, size * 0.5f, size * 0.5f, size * 0.5f, 0f, back);

            float cx = size * 0.5f, cy = size * 0.52f, lens = 96f * s;
            r.Circle(cx, cy, lens, Paint.Vertical(Palette.Hex(0xFF9A5C), Palette.Hex(0x3D3F8C), cy - lens * 0.4f, cy + lens));

            // Skyline inside the lens.
            float ground = cy - lens;
            float[] xs = { -100f, -72f, -52f, -24f, 8f, 30f, 58f, 80f };
            float[] ws = { 26f, 18f, 26f, 30f, 20f, 26f, 20f, 30f };
            float[] hs = { 92f, 128f, 84f, 108f, 140f, 96f, 120f, 78f };
            for (int i = 0; i < xs.Length; i++)
            {
                float bx = cx + (xs[i] + ws[i] * 0.5f) * s;
                float h = hs[i] * s;
                r.RoundRect(bx, ground + h * 0.5f, ws[i] * 0.5f * s, h * 0.5f, 0f, Palette.Hex(i % 2 == 0 ? 0x2B2E58 : 0x343866));
                for (int w = 0; w < 5; w++)
                    if ((i * 7 + w * 3) % 4 == 0)
                        r.RoundRect(bx + ((w % 2) * 2 - 1) * 5f * s, ground + h - (14f + w * 16f) * s, 2.6f * s, 3.4f * s, 0.5f, Palette.Hex(0xFFD27A));
            }

            // Robot in the crosshairs.
            var metal = Palette.Hex(0x2B2F3A);
            r.RoundRect(cx, cy - 58f * s, 50f * s, 26f * s, 14f * s, metal);
            r.RoundRect(cx, cy - 20f * s, 11f * s, 12f * s, 3f * s, metal);
            r.RoundRect(cx, cy + 6f * s, 34f * s, 26f * s, 10f * s, Palette.Hex(0x4A505E));
            r.Circle(cx, cy + 6f * s, 30f * s, new Color(1f, 0.25f, 0.2f, 0.45f), 26f * s);
            r.RoundRect(cx, cy + 8f * s, 25f * s, 6.5f * s, 3.5f * s, Hostile);
            r.Segment(cx + 16f * s, cy + 30f * s, cx + 22f * s, cy + 50f * s, 2.2f * s, metal);
            r.Circle(cx + 22f * s, cy + 52f * s, 4.5f * s, Hostile);

            // Mask everything outside the lens, then the metal scope ring.
            r.Ring(cx, cy, lens + 70f * s, 140f * s, back);
            r.Ring(cx, cy, lens + 9f * s, 20f * s, Paint.Vertical(Palette.Hex(0x4E5767), Palette.Hex(0xC4CCD6), cy - lens, cy + lens));
            r.Ring(cx, cy, lens + 0.5f * s, 3f * s, new Color(0f, 0f, 0f, 0.55f));

            // Crosshair: thin centre, thick outer posts.
            var ink = new Color(0.04f, 0.05f, 0.08f, 0.92f);
            r.Segment(cx - lens, cy, cx - 12f * s, cy, 1.6f * s, ink);
            r.Segment(cx + 12f * s, cy, cx + lens, cy, 1.6f * s, ink);
            r.Segment(cx, cy - lens, cx, cy - 12f * s, 1.6f * s, ink);
            r.Segment(cx, cy + 12f * s, cx, cy + lens, 1.6f * s, ink);
            r.Segment(cx - lens, cy, cx - lens * 0.62f, cy, 4.4f * s, ink);
            r.Segment(cx + lens * 0.62f, cy, cx + lens, cy, 4.4f * s, ink);
            r.Segment(cx, cy - lens, cx, cy - lens * 0.62f, 4.4f * s, ink);
            r.Segment(cx, cy + lens * 0.62f, cx, cy + lens, 4.4f * s, ink);
            r.Circle(cx, cy, 3.6f * s, Hostile);
            return r;
        }

        /// <summary>Square black mask with a soft round lens hole, for the scope view.</summary>
        public static Texture2D ScopeMask
        {
            get
            {
                if (scopeMask != null) return scopeMask;
                const int size = 640;
                float c = size * 0.5f;
                var r = new Raster(size, size, Color.black);
                r.EraseMode = true;
                r.Circle(c, c, c - 22f, Color.white, 18f);
                r.EraseMode = false;
                // Darkening towards the rim, like looking down a tube.
                r.Ring(c, c, c - 34f, 40f, new Color(0f, 0f, 0f, 0.45f), 36f);
                r.Ring(c, c, c - 14f, 10f, new Color(0f, 0f, 0f, 1f), 6f);
                scopeMask = r.ToTexture("ScopeMask");
                return scopeMask;
            }
        }

        public static Sprite Icon(SniperIcon kind)
        {
            if (icons.TryGetValue(kind, out var sprite) && sprite != null) return sprite;
            const int size = 128;
            var r = new Raster(size, size, new Color(1f, 1f, 1f, 0f));
            DrawIcon(r, kind);
            sprite = r.ToSprite("SniperIcon" + kind, new Vector2(0.5f, 0.5f));
            icons[kind] = sprite;
            return sprite;
        }

        static void DrawIcon(Raster r, SniperIcon kind)
        {
            var w = Color.white;
            switch (kind)
            {
                case SniperIcon.Scope:
                    r.Ring(64f, 64f, 40f, 9f, w);
                    r.Segment(64f, 10f, 64f, 42f, 5f, w);
                    r.Segment(64f, 86f, 64f, 118f, 5f, w);
                    r.Segment(10f, 64f, 42f, 64f, 5f, w);
                    r.Segment(86f, 64f, 118f, 64f, 5f, w);
                    r.Circle(64f, 64f, 7f, w);
                    break;

                case SniperIcon.Breath:
                    // Lungs.
                    r.Segment(64f, 112f, 64f, 70f, 6f, w);
                    r.Segment(64f, 72f, 50f, 60f, 5f, w);
                    r.Segment(64f, 72f, 78f, 60f, 5f, w);
                    r.RoundRect(38f, 52f, 20f, 36f, 18f, w);
                    r.RoundRect(90f, 52f, 20f, 36f, 18f, w);
                    break;

                case SniperIcon.Bullet:
                    r.RoundRect(64f, 40f, 16f, 30f, 3f, w);
                    r.Polygon(new[] { new Vector2(48f, 70f), new Vector2(64f, 118f), new Vector2(80f, 70f) }, w, 1f, 4f);
                    r.RoundRect(64f, 12f, 19f, 5f, 2f, w);
                    break;

                case SniperIcon.Coin:
                    r.Circle(64f, 64f, 56f, Palette.Hex(0xE8A21F));
                    r.Circle(64f, 68f, 50f, Coin);
                    r.Ring(64f, 66f, 36f, 6f, Palette.Hex(0xE8A21F));
                    r.Polygon(Raster.StarPoints(64f, 64f, 22f, 10f), Palette.Hex(0xE8A21F), 1f, 2f);
                    r.Arc(64f, 66f, 44f, 5f, 110f, 160f, new Color(1f, 1f, 1f, 0.7f));
                    break;

                case SniperIcon.Wind:
                    r.Segment(14f, 84f, 84f, 84f, 7f, w);
                    r.Arc(84f, 98f, 14f, 9f, -90f, 150f, w);
                    r.Segment(14f, 60f, 104f, 60f, 7f, w);
                    r.Arc(104f, 46f, 14f, 9f, -150f, 90f, w);
                    r.Segment(28f, 36f, 70f, 36f, 7f, w);
                    break;

                case SniperIcon.Lock:
                    r.Arc(64f, 70f, 26f, 11f, 0f, 180f, w);
                    r.Segment(38f, 70f, 38f, 58f, 5.5f, w);
                    r.Segment(90f, 70f, 90f, 58f, 5.5f, w);
                    r.RoundRect(64f, 40f, 40f, 32f, 8f, w);
                    r.EraseMode = true;
                    r.Circle(64f, 46f, 8f, w);
                    r.RoundRect(64f, 30f, 3.5f, 10f, 2f, w);
                    r.EraseMode = false;
                    break;

                case SniperIcon.Upgrade:
                    r.Polygon(new[] { new Vector2(64f, 116f), new Vector2(112f, 64f), new Vector2(82f, 64f), new Vector2(82f, 14f), new Vector2(46f, 14f), new Vector2(46f, 64f), new Vector2(16f, 64f) }, w, 1f, 4f);
                    break;

                case SniperIcon.Target:
                    r.Ring(64f, 64f, 48f, 9f, w);
                    r.Ring(64f, 64f, 26f, 9f, w);
                    r.Circle(64f, 64f, 9f, w);
                    break;

                case SniperIcon.Clock:
                    r.Ring(64f, 60f, 44f, 10f, w);
                    r.Segment(64f, 60f, 64f, 88f, 6f, w);
                    r.Segment(64f, 60f, 84f, 48f, 6f, w);
                    r.RoundRect(64f, 114f, 12f, 6f, 3f, w);
                    break;

                case SniperIcon.Power:
                    r.Polygon(new[] { new Vector2(74f, 120f), new Vector2(26f, 56f), new Vector2(60f, 56f), new Vector2(50f, 8f), new Vector2(102f, 76f), new Vector2(66f, 76f) }, w, 1f, 3f);
                    break;

                case SniperIcon.Stability:
                    r.Ring(64f, 64f, 44f, 8f, w);
                    r.Circle(64f, 64f, 12f, w);
                    r.Segment(64f, 4f, 64f, 30f, 5f, w);
                    r.Segment(64f, 98f, 64f, 124f, 5f, w);
                    r.Segment(4f, 64f, 30f, 64f, 5f, w);
                    r.Segment(98f, 64f, 124f, 64f, 5f, w);
                    break;

                case SniperIcon.Reload:
                    r.Arc(64f, 64f, 40f, 12f, 30f, 300f, w);
                    r.Polygon(new[] { new Vector2(98f, 96f), new Vector2(116f, 58f), new Vector2(78f, 64f) }, w, 1f, 3f);
                    break;

                case SniperIcon.Computer:
                    r.RoundRect(64f, 70f, 50f, 38f, 8f, w);
                    r.EraseMode = true;
                    r.RoundRect(64f, 72f, 40f, 28f, 4f, w);
                    r.EraseMode = false;
                    r.Circle(64f, 72f, 7f, w);
                    r.Ring(64f, 72f, 16f, 4f, w);
                    r.RoundRect(64f, 22f, 26f, 6f, 3f, w);
                    r.Segment(64f, 28f, 64f, 34f, 5f, w);
                    break;
            }
        }

        /// <summary>Side view of the sniper rifle, for the menu and upgrade screens.</summary>
        public static Sprite Rifle
        {
            get
            {
                if (rifle != null) return rifle;
                const int width = 720, height = 220;
                var r = new Raster(width, height, new Color(1f, 1f, 1f, 0f));
                var metal = Palette.Hex(0x2C323D);
                var metalLight = Palette.Hex(0x4B5566);
                var accent = Accent;
                var edge = Palette.Hex(0x161A21);

                // Stock.
                r.Polygon(new[] { new Vector2(20f, 60f), new Vector2(20f, 128f), new Vector2(190f, 118f), new Vector2(236f, 100f), new Vector2(236f, 72f), new Vector2(150f, 72f), new Vector2(110f, 40f), new Vector2(60f, 40f) }, accent, 1f, 3f);
                r.Polygon(new[] { new Vector2(34f, 66f), new Vector2(34f, 116f), new Vector2(60f, 114f), new Vector2(60f, 58f) }, Palette.Darken(accent, 0.3f), 1f, 2f);
                // Grip.
                r.Polygon(new[] { new Vector2(214f, 80f), new Vector2(196f, 24f), new Vector2(228f, 20f), new Vector2(254f, 78f) }, metal, 1f, 3f);
                // Receiver and bolt.
                r.RoundRect(318f, 96f, 90f, 22f, 6f, metal);
                r.RoundRect(318f, 104f, 86f, 8f, 3f, metalLight);
                r.Segment(300f, 84f, 282f, 58f, 5f, metalLight);
                r.Circle(282f, 56f, 9f, metalLight);
                // Trigger guard.
                r.Ring(262f, 70f, 16f, 5f, metal);
                // Barrel and suppressor.
                r.RoundRect(520f, 98f, 120f, 7f, 3f, metal);
                r.RoundRect(670f, 98f, 42f, 13f, 5f, edge);
                r.RoundRect(670f, 104f, 40f, 4f, 2f, metalLight);
                // Handguard.
                r.RoundRect(440f, 94f, 50f, 15f, 6f, Palette.Darken(accent, 0.15f));
                // Scope.
                r.RoundRect(330f, 150f, 110f, 17f, 16f, metal);
                r.RoundRect(222f, 150f, 20f, 23f, 8f, edge);
                r.RoundRect(438f, 150f, 22f, 25f, 8f, edge);
                r.RoundRect(330f, 157f, 104f, 5f, 3f, metalLight);
                r.RoundRect(300f, 124f, 8f, 12f, 2f, metal);
                r.RoundRect(360f, 124f, 8f, 12f, 2f, metal);
                r.Circle(330f, 172f, 8f, metalLight);
                // Bipod.
                r.Segment(470f, 84f, 438f, 20f, 5f, metal);
                r.Segment(476f, 84f, 504f, 20f, 5f, metal);
                rifle = r.ToSprite("Rifle", new Vector2(0.5f, 0.5f));
                return rifle;
            }
        }
    }
}
