using System.Collections.Generic;
using UnityEngine;

namespace NexioCraft.Core
{
    /// <summary>Shared procedural sprites, generated once on first use.</summary>
    public static class Sprites
    {
        /// <summary>Corner radius (in sprite pixels) of <see cref="RoundRect"/>; see <see cref="CornerMultiplier"/>.</summary>
        public const float RoundRectRadius = 64f;

        static Sprite roundRect, circle, softShadow, white, glow;
        static readonly Dictionary<int, Sprite> rings = new Dictionary<int, Sprite>();
        static readonly Dictionary<long, Texture2D> gradients = new Dictionary<long, Texture2D>();

        /// <summary>9-sliced white rounded rectangle. Use with Image.Type.Sliced and <see cref="CornerMultiplier"/>.</summary>
        public static Sprite RoundRect
        {
            get
            {
                if (roundRect == null)
                {
                    const int size = 160;
                    var raster = new Raster(size, size, new Color(1f, 1f, 1f, 0f));
                    raster.RoundRect(size * 0.5f, size * 0.5f, size * 0.5f - 1f, size * 0.5f - 1f, RoundRectRadius, Color.white);
                    float border = RoundRectRadius + 2f;
                    roundRect = raster.ToSprite("RoundRect", new Vector2(0.5f, 0.5f), new Vector4(border, border, border, border));
                }
                return roundRect;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (circle == null)
                {
                    const int size = 256;
                    var raster = new Raster(size, size, new Color(1f, 1f, 1f, 0f));
                    raster.Circle(size * 0.5f, size * 0.5f, size * 0.5f - 2f, Color.white);
                    circle = raster.ToSprite("Circle", new Vector2(0.5f, 0.5f));
                }
                return circle;
            }
        }

        /// <summary>Blurred rounded rectangle for drop shadows; 9-sliced so it stretches to any card.</summary>
        public static Sprite SoftShadow
        {
            get
            {
                if (softShadow == null)
                {
                    const int size = 160;
                    var raster = new Raster(size, size, new Color(0f, 0f, 0f, 0f));
                    raster.RoundRect(size * 0.5f, size * 0.5f, 44f, 44f, 30f, Color.black, 44f);
                    softShadow = raster.ToSprite("SoftShadow", new Vector2(0.5f, 0.5f), new Vector4(78f, 78f, 78f, 78f));
                }
                return softShadow;
            }
        }

        /// <summary>Radial glow that fades to transparent at the edge.</summary>
        public static Sprite Glow
        {
            get
            {
                if (glow == null)
                {
                    const int size = 192;
                    var raster = new Raster(size, size, new Color(1f, 1f, 1f, 0f));
                    raster.Circle(size * 0.5f, size * 0.5f, size * 0.22f, Color.white, size * 0.5f);
                    glow = raster.ToSprite("Glow", new Vector2(0.5f, 0.5f));
                }
                return glow;
            }
        }

        public static Sprite White
        {
            get
            {
                if (white == null)
                {
                    var raster = new Raster(4, 4, Color.white);
                    white = raster.ToSprite("White", new Vector2(0.5f, 0.5f));
                }
                return white;
            }
        }

        /// <summary>Image.pixelsPerUnitMultiplier that gives <see cref="RoundRect"/> the requested corner radius.</summary>
        public static float CornerMultiplier(float radius) => RoundRectRadius / Mathf.Max(0.5f, radius);

        /// <summary>White ring whose stroke is <paramref name="thickness"/> of the sprite's radius (0..1).</summary>
        public static Sprite Ring(float thickness)
        {
            int key = Mathf.RoundToInt(Mathf.Clamp01(thickness) * 100f);
            if (rings.TryGetValue(key, out var sprite) && sprite != null) return sprite;
            const int size = 256;
            float outer = size * 0.5f - 2f;
            float stroke = outer * key / 100f;
            var raster = new Raster(size, size, new Color(1f, 1f, 1f, 0f));
            raster.Ring(size * 0.5f, size * 0.5f, outer - stroke * 0.5f, stroke, Color.white);
            sprite = raster.ToSprite("Ring" + key, new Vector2(0.5f, 0.5f));
            rings[key] = sprite;
            return sprite;
        }

        /// <summary>Tall dithered gradient texture for full-screen backgrounds (use with RawImage).</summary>
        public static Texture2D VerticalGradient(Color top, Color bottom)
        {
            long key = ((long)Pack(top) << 32) | (uint)Pack(bottom);
            if (gradients.TryGetValue(key, out var texture) && texture != null) return texture;

            const int width = 32, height = 512;
            var pixels = new Color32[width * height];
            var rng = new System.Random(7);
            for (int y = 0; y < height; y++)
            {
                Color c = Color.Lerp(bottom, top, y / (height - 1f));
                for (int x = 0; x < width; x++)
                {
                    float dither = ((float)rng.NextDouble() - 0.5f) / 255f;
                    pixels[y * width + x] = new Color(c.r + dither, c.g + dither, c.b + dither, 1f);
                }
            }
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Gradient",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            gradients[key] = texture;
            return texture;
        }

        static int Pack(Color c)
        {
            Color32 b = c;
            return (b.r << 24) | (b.g << 16) | (b.b << 8) | b.a;
        }
    }
}
