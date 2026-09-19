using System.Collections.Generic;
using NexioCraft.Core;
using UnityEngine;

namespace NexioCraft.Brain
{
    /// <summary>Procedural art for the brain games: memory symbols, card backs, 2048 colours and the icon.</summary>
    public static class BrainArt
    {
        public static readonly Color Accent = Palette.Hex(0x8B5CF6);
        public static readonly Color AccentDark = Palette.Hex(0x5B2FB5);

        static readonly Color[] SymbolColors =
        {
            Palette.Hex(0xEF4444), Palette.Hex(0x22C55E), Palette.Hex(0x3B82F6), Palette.Hex(0xF59E0B),
            Palette.Hex(0xEC4899), Palette.Hex(0x14B8A6), Palette.Hex(0xA855F7), Palette.Hex(0x84CC16),
            Palette.Hex(0xF97316), Palette.Hex(0x06B6D4), Palette.Hex(0xE11D48), Palette.Hex(0x64748B)
        };

        public const int SymbolCount = 12;

        static readonly Dictionary<int, Sprite> symbols = new Dictionary<int, Sprite>();
        static Sprite cardBack;

        public static Color SymbolColor(int index) => SymbolColors[index % SymbolColors.Length];

        /// <summary>White-outlined coloured shape used on the memory cards.</summary>
        public static Sprite Symbol(int index)
        {
            index %= SymbolCount;
            if (symbols.TryGetValue(index, out var sprite) && sprite != null) return sprite;
            const int size = 192;
            const float c = size * 0.5f;
            var color = SymbolColor(index);
            var r = new Raster(size, size, Palette.WithAlpha(color, 0f));
            float radius = size * 0.34f;
            switch (index)
            {
                case 0: r.Circle(c, c, radius, color); break;
                case 1: r.RoundRect(c, c, radius * 0.92f, radius * 0.92f, radius * 0.22f, color); break;
                case 2: r.Polygon(Polar(c, 3, radius * 1.1f, 90f), color, 1f, 6f); break;
                case 3: r.Polygon(Raster.StarPoints(c, c, radius * 1.15f, radius * 0.5f), color, 1f, 5f); break;
                case 4: r.Polygon(Polar(c, 4, radius * 1.08f, 90f), color, 1f, 6f); break;
                case 5: r.Polygon(Polar(c, 6, radius * 1.02f, 90f), color, 1f, 6f); break;
                case 6: r.Polygon(Polar(c, 5, radius * 1.08f, 90f), color, 1f, 6f); break;
                case 7:
                    r.RoundRect(c, c, radius * 0.34f, radius, radius * 0.2f, color);
                    r.RoundRect(c, c, radius, radius * 0.34f, radius * 0.2f, color);
                    break;
                case 8: r.Ring(c, c, radius * 0.82f, radius * 0.5f, color); break;
                case 9:
                    r.Circle(c - radius * 0.42f, c + radius * 0.34f, radius * 0.52f, color);
                    r.Circle(c + radius * 0.42f, c + radius * 0.34f, radius * 0.52f, color);
                    r.Polygon(new[]
                    {
                        new Vector2(c - radius * 0.94f, c + radius * 0.36f),
                        new Vector2(c + radius * 0.94f, c + radius * 0.36f),
                        new Vector2(c, c - radius)
                    }, color, 1f, 4f);
                    break;
                case 10:
                    r.Polygon(new[]
                    {
                        new Vector2(c + radius * 0.35f, c + radius), new Vector2(c - radius * 0.55f, c + radius * 0.05f),
                        new Vector2(c + radius * 0.05f, c + radius * 0.05f), new Vector2(c - radius * 0.3f, c - radius),
                        new Vector2(c + radius * 0.6f, c - radius * 0.1f), new Vector2(c - radius * 0.02f, c - radius * 0.1f)
                    }, color, 1f, 5f);
                    break;
                default:
                    r.Circle(c + radius * 0.2f, c, radius, color);
                    r.EraseMode = true;
                    r.Circle(c + radius * 0.62f, c + radius * 0.22f, radius * 0.86f, color);
                    r.EraseMode = false;
                    break;
            }
            sprite = r.ToSprite("Symbol" + index, new Vector2(0.5f, 0.5f));
            symbols[index] = sprite;
            return sprite;
        }

        /// <summary>Patterned back of a memory card.</summary>
        public static Sprite CardBack()
        {
            if (cardBack != null) return cardBack;
            const int size = 256;
            var r = new Raster(size, size, Palette.WithAlpha(Accent, 0f));
            r.RoundRect(size * 0.5f, size * 0.5f, size * 0.5f - 2f, size * 0.5f - 2f, 40f, Paint.Vertical(AccentDark, Accent, 0f, size));
            for (int i = -4; i <= 8; i++)
            {
                float offset = i * 46f;
                r.Segment(offset, -20f, offset + 220f, size + 20f, 7f, Palette.WithAlpha(Color.white, 0.12f));
            }
            r.Circle(size * 0.5f, size * 0.5f, 44f, Palette.WithAlpha(Color.white, 0.22f));
            r.Ring(size * 0.5f, size * 0.5f, 62f, 8f, Palette.WithAlpha(Color.white, 0.25f));
            cardBack = r.ToSprite("CardBack", new Vector2(0.5f, 0.5f));
            return cardBack;
        }

        /// <summary>Classic 2048 tile colours.</summary>
        public static Color TileColor(int value)
        {
            switch (value)
            {
                case 2: return Palette.Hex(0xEEE4DA);
                case 4: return Palette.Hex(0xEDE0C8);
                case 8: return Palette.Hex(0xF2B179);
                case 16: return Palette.Hex(0xF59563);
                case 32: return Palette.Hex(0xF67C5F);
                case 64: return Palette.Hex(0xF65E3B);
                case 128: return Palette.Hex(0xEDCF72);
                case 256: return Palette.Hex(0xEDCC61);
                case 512: return Palette.Hex(0xEDC850);
                case 1024: return Palette.Hex(0xEDC53F);
                case 2048: return Palette.Hex(0xEDC22E);
                default: return Palette.Hex(0x3C3A32);
            }
        }

        public static Color TileTextColor(int value) => value <= 4 ? Palette.Hex(0x776E65) : Color.white;

        public static readonly Color[] PadColors =
        {
            Palette.Hex(0xE5484D), Palette.Hex(0x30A46C), Palette.Hex(0x3E63DD), Palette.Hex(0xF5B800)
        };

        /// <summary>App icon / card artwork: a bulb on a violet ground with a few floating shapes.</summary>
        public static Raster AppIcon(int size)
        {
            float s = size;
            var r = new Raster(size, size, AccentDark);
            r.RoundRect(s * 0.5f, s * 0.5f, s * 0.5f, s * 0.5f, 0f, Paint.Vertical(Palette.Hex(0x2A1C63), Accent, 0f, s));
            r.Circle(s * 0.5f, s * 0.56f, s * 0.3f, Palette.WithAlpha(Color.white, 0.18f), s * 0.35f);

            // Bulb.
            float cx = s * 0.5f, cy = s * 0.58f, radius = s * 0.24f;
            r.Circle(cx, cy, radius, Palette.Hex(0xFFF3C4));
            r.RoundRect(cx, cy - radius * 1.12f, radius * 0.44f, radius * 0.3f, radius * 0.12f, Palette.Hex(0xFFF3C4));
            r.RoundRect(cx, cy - radius * 1.5f, radius * 0.5f, radius * 0.14f, radius * 0.07f, Palette.Hex(0xE2C36B));
            r.RoundRect(cx, cy - radius * 1.78f, radius * 0.4f, radius * 0.12f, radius * 0.06f, Palette.Hex(0xE2C36B));
            r.EraseMode = true;
            r.Arc(cx, cy + radius * 0.1f, radius * 0.5f, radius * 0.14f, 200f, 340f, Color.white);
            r.EraseMode = false;

            // Floating shapes.
            r.Polygon(Raster.StarPoints(s * 0.2f, s * 0.82f, s * 0.07f, s * 0.03f), Palette.WithAlpha(Color.white, 0.85f), 1f, 3f);
            r.Circle(s * 0.82f, s * 0.8f, s * 0.05f, Palette.WithAlpha(Color.white, 0.75f));
            r.RoundRect(s * 0.78f, s * 0.3f, s * 0.05f, s * 0.05f, s * 0.015f, Palette.WithAlpha(Color.white, 0.7f));
            r.Polygon(Polar(new Vector2(s * 0.2f, s * 0.28f), 3, s * 0.07f, 90f), Palette.WithAlpha(Color.white, 0.7f), 1f, 3f);
            return r;
        }

        static Vector2[] Polar(float center, int corners, float radius, float rotation) => Polar(new Vector2(center, center), corners, radius, rotation);

        static Vector2[] Polar(Vector2 center, int corners, float radius, float rotation)
        {
            var points = new Vector2[corners];
            for (int i = 0; i < corners; i++)
            {
                float angle = (rotation + i * 360f / corners) * Mathf.Deg2Rad;
                points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            return points;
        }
    }
}
