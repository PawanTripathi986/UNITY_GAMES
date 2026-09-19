using System.Collections.Generic;
using NexioCraft.Core;
using UnityEngine;

namespace NexioCraft.Blocks
{
    /// <summary>Block sprites: a rounded tile with a light top face and darker base.</summary>
    public static class BlocksArt
    {
        public static readonly Color[] Colors =
        {
            Palette.Hex(0x4CC3F5), Palette.Hex(0x2ECC71), Palette.Hex(0xF5A623), Palette.Hex(0xE8506E),
            Palette.Hex(0x9B6BF2), Palette.Hex(0xF2D024), Palette.Hex(0x22C5B4)
        };

        static readonly Dictionary<int, Sprite> blocks = new Dictionary<int, Sprite>();
        static Sprite empty;

        public static Color ColorOf(int index) => Colors[(index - 1 + Colors.Length) % Colors.Length];

        /// <summary>Filled block for the given 1-based colour.</summary>
        public static Sprite Block(int colour)
        {
            if (blocks.TryGetValue(colour, out var sprite) && sprite != null) return sprite;
            const int size = 128;
            var color = ColorOf(colour);
            var r = new Raster(size, size, new Color(0f, 0f, 0f, 0f));
            r.RoundRect(size * 0.5f, size * 0.5f - 3f, size * 0.46f, size * 0.46f, 18f, Palette.Darken(color, 0.4f));
            r.RoundRect(size * 0.5f, size * 0.5f + 2f, size * 0.46f, size * 0.44f, 18f,
                Paint.Vertical(Palette.Darken(color, 0.12f), Palette.Lighten(color, 0.18f), size * 0.06f, size * 0.94f));
            r.RoundRect(size * 0.5f, size * 0.68f, size * 0.32f, size * 0.16f, 12f, Palette.WithAlpha(Color.white, 0.22f));
            sprite = r.ToSprite("Block" + colour, new Vector2(0.5f, 0.5f));
            blocks[colour] = sprite;
            return sprite;
        }

        /// <summary>Empty board cell.</summary>
        public static Sprite EmptyCell()
        {
            if (empty != null) return empty;
            const int size = 128;
            var r = new Raster(size, size, new Color(0f, 0f, 0f, 0f));
            r.RoundRect(size * 0.5f, size * 0.5f, size * 0.45f, size * 0.45f, 18f, new Color(1f, 1f, 1f, 0.07f));
            empty = r.ToSprite("EmptyCell", new Vector2(0.5f, 0.5f));
            return empty;
        }

        /// <summary>App icon / card artwork: a small board with a piece hovering over it.</summary>
        public static Raster AppIcon(int size)
        {
            float s = size;
            var r = new Raster(size, size, Palette.BackgroundBottom);
            r.RoundRect(s * 0.5f, s * 0.5f, s * 0.5f, s * 0.5f, 0f, Paint.Vertical(Palette.Hex(0x10214A), Palette.Hex(0x2C4C8F), 0f, s));

            float board = s * 0.74f, cell = board / 5f;
            float left = s * 0.5f - board * 0.5f, top = s * 0.5f + board * 0.5f;
            for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                r.RoundRect(left + (x + 0.5f) * cell, top - (y + 0.5f) * cell, cell * 0.44f, cell * 0.44f, cell * 0.16f, new Color(1f, 1f, 1f, 0.08f));

            void Block(int x, int y, Color color)
            {
                float cx = left + (x + 0.5f) * cell, cy = top - (y + 0.5f) * cell;
                r.RoundRect(cx, cy - cell * 0.03f, cell * 0.44f, cell * 0.44f, cell * 0.16f, Palette.Darken(color, 0.4f));
                r.RoundRect(cx, cy + cell * 0.02f, cell * 0.44f, cell * 0.42f, cell * 0.16f,
                    Paint.Vertical(Palette.Darken(color, 0.1f), Palette.Lighten(color, 0.2f), cy - cell * 0.5f, cy + cell * 0.5f));
            }

            // A nearly complete row plus the piece that would clear it.
            for (int x = 0; x < 5; x++)
                if (x != 2) Block(x, 3, Colors[0]);
            Block(1, 4, Colors[1]);
            Block(3, 4, Colors[3]);
            Block(2, 1, Colors[2]);
            Block(2, 0, Colors[2]);
            return r;
        }
    }
}
