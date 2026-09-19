using UnityEngine;

namespace NexioCraft.Core
{
    /// <summary>Fill for raster shapes: a solid colour or a vertical gradient between two heights.</summary>
    public readonly struct Paint
    {
        readonly Color bottom;
        readonly Color top;
        readonly float y0;
        readonly float y1;

        Paint(Color bottom, Color top, float y0, float y1)
        {
            this.bottom = bottom;
            this.top = top;
            this.y0 = y0;
            this.y1 = y1;
        }

        public static Paint Solid(Color color) => new Paint(color, color, 0f, 0f);

        /// <summary>Gradient from <paramref name="bottom"/> at height y0 to <paramref name="top"/> at y1, in pixels.</summary>
        public static Paint Vertical(Color bottom, Color top, float y0, float y1) => new Paint(bottom, top, y0, y1);

        public Color At(float y)
        {
            if (y1 <= y0) return bottom;
            return Color.Lerp(bottom, top, Mathf.Clamp01((y - y0) / (y1 - y0)));
        }

        public static implicit operator Paint(Color color) => Solid(color);
    }

    /// <summary>
    /// CPU rasteriser for procedural art. Every shape is a signed distance function, which gives clean
    /// anti-aliased edges (or soft shadows) at any size. Pixel coordinates start at the bottom-left.
    /// </summary>
    public sealed class Raster
    {
        public readonly int Width;
        public readonly int Height;

        /// <summary>When set, shapes cut alpha out of what is already drawn instead of painting.</summary>
        public bool EraseMode;

        readonly Color32[] pixels;

        public Raster(int width, int height, Color clear)
        {
            Width = width;
            Height = height;
            pixels = new Color32[width * height];
            Color32 c = clear;
            for (int i = 0; i < pixels.Length; i++) pixels[i] = c;
        }

        public void Circle(float cx, float cy, float radius, Paint paint, float soft = 1f)
        {
            float reach = radius + soft + 1f;
            Bounds(cx - reach, cy - reach, cx + reach, cy + reach, out int x0, out int y0, out int x1, out int y1);
            for (int y = y0; y <= y1; y++)
            {
                float py = y + 0.5f - cy;
                Color color = paint.At(y + 0.5f);
                for (int x = x0; x <= x1; x++)
                {
                    float px = x + 0.5f - cx;
                    Put(x, y, color, Mathf.Sqrt(px * px + py * py) - radius, soft);
                }
            }
        }

        public void Ring(float cx, float cy, float radius, float thickness, Paint paint, float soft = 1f)
        {
            float half = thickness * 0.5f;
            float reach = radius + half + soft + 1f;
            Bounds(cx - reach, cy - reach, cx + reach, cy + reach, out int x0, out int y0, out int x1, out int y1);
            for (int y = y0; y <= y1; y++)
            {
                float py = y + 0.5f - cy;
                Color color = paint.At(y + 0.5f);
                for (int x = x0; x <= x1; x++)
                {
                    float px = x + 0.5f - cx;
                    Put(x, y, color, Mathf.Abs(Mathf.Sqrt(px * px + py * py) - radius) - half, soft);
                }
            }
        }

        /// <summary>Ring segment with round caps, running counter-clockwise from startDeg to endDeg.</summary>
        public void Arc(float cx, float cy, float radius, float thickness, float startDeg, float endDeg, Paint paint, float soft = 1f)
        {
            float span = endDeg - startDeg;
            float half = thickness * 0.5f;
            float ax = cx + Mathf.Cos(startDeg * Mathf.Deg2Rad) * radius;
            float ay = cy + Mathf.Sin(startDeg * Mathf.Deg2Rad) * radius;
            float bx = cx + Mathf.Cos(endDeg * Mathf.Deg2Rad) * radius;
            float by = cy + Mathf.Sin(endDeg * Mathf.Deg2Rad) * radius;
            float reach = radius + half + soft + 1f;
            Bounds(cx - reach, cy - reach, cx + reach, cy + reach, out int x0, out int y0, out int x1, out int y1);
            for (int y = y0; y <= y1; y++)
            {
                float py = y + 0.5f;
                Color color = paint.At(py);
                for (int x = x0; x <= x1; x++)
                {
                    float px = x + 0.5f;
                    float dx = px - cx, dy = py - cy;
                    float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    float d;
                    if (Mathf.Repeat(angle - startDeg, 360f) <= span)
                    {
                        d = Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - radius) - half;
                    }
                    else
                    {
                        float da = Mathf.Sqrt((px - ax) * (px - ax) + (py - ay) * (py - ay));
                        float db = Mathf.Sqrt((px - bx) * (px - bx) + (py - by) * (py - by));
                        d = Mathf.Min(da, db) - half;
                    }
                    Put(x, y, color, d, soft);
                }
            }
        }

        public void RoundRect(float cx, float cy, float halfWidth, float halfHeight, float radius, Paint paint, float soft = 1f)
        {
            radius = Mathf.Clamp(radius, 0f, Mathf.Min(halfWidth, halfHeight));
            float rx = halfWidth + soft + 1f, ry = halfHeight + soft + 1f;
            Bounds(cx - rx, cy - ry, cx + rx, cy + ry, out int x0, out int y0, out int x1, out int y1);
            for (int y = y0; y <= y1; y++)
            {
                float qy = Mathf.Abs(y + 0.5f - cy) - halfHeight + radius;
                Color color = paint.At(y + 0.5f);
                for (int x = x0; x <= x1; x++)
                {
                    float qx = Mathf.Abs(x + 0.5f - cx) - halfWidth + radius;
                    float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
                    float d = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    Put(x, y, color, d, soft);
                }
            }
        }

        /// <summary>Capsule: a line segment with rounded ends.</summary>
        public void Segment(float ax, float ay, float bx, float by, float radius, Paint paint, float soft = 1f)
        {
            float reach = radius + soft + 1f;
            Bounds(Mathf.Min(ax, bx) - reach, Mathf.Min(ay, by) - reach, Mathf.Max(ax, bx) + reach, Mathf.Max(ay, by) + reach,
                out int x0, out int y0, out int x1, out int y1);
            float ex = bx - ax, ey = by - ay;
            float ee = ex * ex + ey * ey;
            for (int y = y0; y <= y1; y++)
            {
                float py = y + 0.5f;
                Color color = paint.At(py);
                for (int x = x0; x <= x1; x++)
                {
                    float wx = x + 0.5f - ax, wy = py - ay;
                    float t = ee > 0f ? Mathf.Clamp01((wx * ex + wy * ey) / ee) : 0f;
                    float dx = wx - ex * t, dy = wy - ey * t;
                    Put(x, y, color, Mathf.Sqrt(dx * dx + dy * dy) - radius, soft);
                }
            }
        }

        /// <param name="grow">Positive values grow the shape outward (rounding its corners); negative values shrink it.</param>
        public void Polygon(Vector2[] points, Paint paint, float soft = 1f, float grow = 0f)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var p in points)
            {
                minX = Mathf.Min(minX, p.x);
                minY = Mathf.Min(minY, p.y);
                maxX = Mathf.Max(maxX, p.x);
                maxY = Mathf.Max(maxY, p.y);
            }
            float reach = Mathf.Max(0f, grow) + soft + 1f;
            Bounds(minX - reach, minY - reach, maxX + reach, maxY + reach, out int x0, out int y0, out int x1, out int y1);
            for (int y = y0; y <= y1; y++)
            {
                float py = y + 0.5f;
                Color color = paint.At(py);
                for (int x = x0; x <= x1; x++)
                    Put(x, y, color, PolygonDistance(points, x + 0.5f, py) - grow, soft);
            }
        }

        public Texture2D ToTexture(string name, bool keepReadable = false)
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, !keepReadable);
            return texture;
        }

        public Sprite ToSprite(string name, Vector2 pivot, Vector4 border = default)
        {
            var sprite = Sprite.Create(ToTexture(name), new Rect(0f, 0f, Width, Height), pivot, 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = name;
            return sprite;
        }

        /// <summary>Points of a star polygon centred on (cx, cy) with the first tip pointing up.</summary>
        public static Vector2[] StarPoints(float cx, float cy, float outer, float inner, int tips = 5)
        {
            var points = new Vector2[tips * 2];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = (90f + i * 180f / tips) * Mathf.Deg2Rad;
                float r = i % 2 == 0 ? outer : inner;
                points[i] = new Vector2(cx + Mathf.Cos(angle) * r, cy + Mathf.Sin(angle) * r);
            }
            return points;
        }

        /// <summary>Rotates and scales unit-space points (around the origin), then moves them to (cx, cy).</summary>
        public static Vector2[] Transform(Vector2[] unitPoints, float cx, float cy, float scale, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            var result = new Vector2[unitPoints.Length];
            for (int i = 0; i < unitPoints.Length; i++)
            {
                var p = unitPoints[i];
                result[i] = new Vector2(cx + (p.x * cos - p.y * sin) * scale, cy + (p.x * sin + p.y * cos) * scale);
            }
            return result;
        }

        // Signed distance to a closed polygon; negative inside. After Inigo Quilez's sdPolygon.
        static float PolygonDistance(Vector2[] v, float px, float py)
        {
            int n = v.Length;
            float d = (px - v[0].x) * (px - v[0].x) + (py - v[0].y) * (py - v[0].y);
            float sign = 1f;
            for (int i = 0, j = n - 1; i < n; j = i, i++)
            {
                float ex = v[j].x - v[i].x, ey = v[j].y - v[i].y;
                float wx = px - v[i].x, wy = py - v[i].y;
                float ee = ex * ex + ey * ey;
                float t = ee > 0f ? Mathf.Clamp01((wx * ex + wy * ey) / ee) : 0f;
                float bx = wx - ex * t, by = wy - ey * t;
                d = Mathf.Min(d, bx * bx + by * by);
                bool c1 = py >= v[i].y, c2 = py < v[j].y, c3 = ex * wy > ey * wx;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) sign = -sign;
            }
            return sign * Mathf.Sqrt(d);
        }

        void Bounds(float minX, float minY, float maxX, float maxY, out int x0, out int y0, out int x1, out int y1)
        {
            x0 = Mathf.Max(0, Mathf.FloorToInt(minX));
            y0 = Mathf.Max(0, Mathf.FloorToInt(minY));
            x1 = Mathf.Min(Width - 1, Mathf.CeilToInt(maxX));
            y1 = Mathf.Min(Height - 1, Mathf.CeilToInt(maxY));
        }

        void Put(int x, int y, Color color, float distance, float soft)
        {
            float coverage = Mathf.Clamp01(0.5f - distance / soft);
            if (coverage <= 0f) return;
            if (soft > 1.01f) coverage = coverage * coverage * (3f - 2f * coverage);
            int i = y * Width + x;
            if (EraseMode)
            {
                Color32 e = pixels[i];
                e.a = ToByte(e.a / 255f * (1f - coverage * color.a));
                pixels[i] = e;
                return;
            }

            float sa = color.a * coverage;
            if (sa <= 0f) return;
            Color32 d = pixels[i];
            float da = d.a / 255f;
            float outA = sa + da * (1f - sa);
            float keep = da * (1f - sa);
            pixels[i] = new Color32(
                ToByte((color.r * sa + d.r / 255f * keep) / outA),
                ToByte((color.g * sa + d.g / 255f * keep) / outA),
                ToByte((color.b * sa + d.b / 255f * keep) / outA),
                ToByte(outA));
        }

        static byte ToByte(float v)
        {
            int i = (int)(v * 255f + 0.5f);
            return (byte)(i < 0 ? 0 : i > 255 ? 255 : i);
        }
    }
}
