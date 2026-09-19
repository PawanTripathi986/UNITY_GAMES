using NexioCraft.Core;
using UnityEngine;

namespace NexioCraft.Sort
{
    /// <summary>Glass tubes, liquid slices and the app icon, all drawn in code.</summary>
    public static class SortArt
    {
        public static readonly Color Accent = Palette.Hex(0x22B8CF);

        /// <summary>Liquid colours, picked to stay apart from each other (index 0 is unused: 0 means empty).</summary>
        public static readonly Color[] Colours =
        {
            Color.clear,
            Palette.Hex(0xE8453C), // red
            Palette.Hex(0x3B82F6), // blue
            Palette.Hex(0x22C55E), // green
            Palette.Hex(0xFACC15), // yellow
            Palette.Hex(0xA855F7), // purple
            Palette.Hex(0xFB923C), // orange
            Palette.Hex(0x22D3EE), // cyan
            Palette.Hex(0xF472B6), // pink
            Palette.Hex(0x84CC16), // lime
            Palette.Hex(0x9A6B4F), // brown
            Palette.Hex(0xCBD5E1)  // silver
        };

        public static Color Of(int colour) => colour > 0 && colour < Colours.Length ? Colours[colour] : Color.clear;

        static Sprite glass, liquidBottom, liquidPlain, liquidTop, addTube;

        /// <summary>Open-topped glass tube: a soft fill with a lighter rim.</summary>
        public static Sprite Glass
        {
            get
            {
                if (glass != null) return glass;
                const int w = 160, h = 520;
                var r = new Raster(w, h, new Color(1f, 1f, 1f, 0f));
                float radius = 68f;
                r.RoundRect(w * 0.5f, h * 0.5f + radius * 0.5f, w * 0.5f - 2f, h * 0.5f + radius * 0.5f - 2f, radius, new Color(1f, 1f, 1f, 0.13f));
                // Rim.
                r.RoundRect(w * 0.5f, h * 0.5f + radius * 0.5f, w * 0.5f - 2f, h * 0.5f + radius * 0.5f - 2f, radius, new Color(1f, 1f, 1f, 0.45f));
                r.EraseMode = true;
                r.RoundRect(w * 0.5f, h * 0.5f + radius * 0.5f, w * 0.5f - 9f, h * 0.5f + radius * 0.5f - 9f, radius - 7f, Color.white);
                r.EraseMode = false;
                r.RoundRect(w * 0.5f, h * 0.5f + radius * 0.5f, w * 0.5f - 9f, h * 0.5f + radius * 0.5f - 9f, radius - 7f, new Color(1f, 1f, 1f, 0.09f));
                // Glass highlight down the left side.
                r.RoundRect(w * 0.28f, h * 0.55f, 5f, h * 0.3f, 5f, new Color(1f, 1f, 1f, 0.22f));
                glass = r.ToSprite("Tube", new Vector2(0.5f, 0.5f));
                return glass;
            }
        }

        /// <summary>Liquid slice with a rounded bottom (the one resting on the base of the tube).</summary>
        public static Sprite LiquidBottom
        {
            get
            {
                if (liquidBottom != null) return liquidBottom;
                const int w = 128, h = 128;
                var r = new Raster(w, h, new Color(1f, 1f, 1f, 0f));
                r.RoundRect(w * 0.5f, h * 0.5f, w * 0.5f, h * 0.5f, 52f, Color.white);
                r.RoundRect(w * 0.5f, h * 0.75f, w * 0.5f, h * 0.25f, 0f, Color.white);
                liquidBottom = r.ToSprite("LiquidBottom", new Vector2(0.5f, 0.5f));
                return liquidBottom;
            }
        }

        /// <summary>Plain middle slice.</summary>
        public static Sprite LiquidPlain
        {
            get
            {
                if (liquidPlain != null) return liquidPlain;
                var r = new Raster(64, 64, Color.white);
                liquidPlain = r.ToSprite("LiquidPlain", new Vector2(0.5f, 0.5f));
                return liquidPlain;
            }
        }

        /// <summary>Top slice: a slight dome, so a full tube reads as liquid rather than a block.</summary>
        public static Sprite LiquidTop
        {
            get
            {
                if (liquidTop != null) return liquidTop;
                const int w = 128, h = 128;
                var r = new Raster(w, h, new Color(1f, 1f, 1f, 0f));
                r.RoundRect(w * 0.5f, h * 0.45f, w * 0.5f, h * 0.45f, 0f, Color.white);
                r.RoundRect(w * 0.5f, h * 0.82f, w * 0.5f, h * 0.18f, 26f, Color.white);
                liquidTop = r.ToSprite("LiquidTop", new Vector2(0.5f, 0.5f));
                return liquidTop;
            }
        }

        /// <summary>Icon for the "extra tube" button.</summary>
        public static Sprite AddTube
        {
            get
            {
                if (addTube != null) return addTube;
                const int size = 128;
                var r = new Raster(size, size, new Color(1f, 1f, 1f, 0f));
                r.RoundRect(44f, 62f, 22f, 46f, 20f, Color.white);
                r.EraseMode = true;
                r.RoundRect(44f, 66f, 15f, 44f, 13f, Color.white);
                r.EraseMode = false;
                r.RoundRect(44f, 36f, 15f, 18f, 13f, Color.white);
                r.Segment(96f, 46f, 96f, 86f, 9f, Color.white);
                r.Segment(76f, 66f, 116f, 66f, 9f, Color.white);
                addTube = r.ToSprite("AddTube", new Vector2(0.5f, 0.5f));
                return addTube;
            }
        }

        public static Raster AppIcon(int size)
        {
            float s = size / 256f;
            var r = new Raster(size, size, Color.clear);
            r.RoundRect(size * 0.5f, size * 0.5f, size * 0.5f, size * 0.5f, 0f,
                Paint.Vertical(Palette.Hex(0x0E1A38), Palette.Hex(0x1D3A6B), 0f, size));

            // Three tubes, the middle one taller and finished.
            DrawTube(r, s, 52f, new[] { 1, 1, 4, 2 });
            DrawTube(r, s, 128f, new[] { 3, 3, 3, 3 });
            DrawTube(r, s, 204f, new[] { 2, 4, 1, 0 });

            // A pour arc from the right tube into the middle one.
            r.Arc(166f * s, 196f * s, 34f * s, 7f * s, 20f, 160f, Colours[2]);
            r.Circle(132f * s, 206f * s, 6f * s, Colours[2]);
            return r;
        }

        static void DrawTube(Raster r, float s, float cx, int[] colours)
        {
            const float width = 54f, height = 150f, bottom = 44f;
            float half = width * 0.5f * s;
            float centreY = (bottom + height * 0.5f) * s;
            float halfHeight = height * 0.5f * s;
            float slot = height / 4f * s;

            // Rim first, cut hollow, so the liquids drawn afterwards survive.
            r.RoundRect(cx * s, centreY, half, halfHeight, 24f * s, new Color(1f, 1f, 1f, 0.34f));
            r.EraseMode = true;
            r.RoundRect(cx * s, centreY, half - 4f * s, halfHeight - 4f * s, 20f * s, Color.white);
            r.EraseMode = false;
            r.RoundRect(cx * s, centreY, half - 4f * s, halfHeight - 4f * s, 20f * s, new Color(1f, 1f, 1f, 0.14f));

            for (int i = 0; i < colours.Length; i++)
            {
                if (colours[i] == 0) continue;
                float y = (bottom + 5f) * s + slot * (i + 0.5f);
                float radius = i == 0 ? 18f * s : 0f;
                r.RoundRect(cx * s, y, half - 7f * s, slot * 0.5f, radius, Colours[colours[i]]);
            }
        }
    }
}
