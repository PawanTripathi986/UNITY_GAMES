using System.Collections.Generic;
using NexioCraft.Core;
using UnityEngine;

namespace NexioCraft.Ludo
{
    /// <summary>Procedural Ludo artwork: board, tokens and dice.</summary>
    public static class LudoArt
    {
        static readonly Dictionary<int, Texture2D> boards = new Dictionary<int, Texture2D>();
        static readonly Sprite[] tokens = new Sprite[4];
        static readonly Sprite[] diceFaces = new Sprite[7];
        static Sprite tokenShadow;

        static readonly Vector2[] ArrowShape =
        {
            new Vector2(-0.30f, 0.09f), new Vector2(0.02f, 0.09f), new Vector2(0.02f, 0.23f),
            new Vector2(0.30f, 0f), new Vector2(0.02f, -0.23f), new Vector2(0.02f, -0.09f), new Vector2(-0.30f, -0.09f)
        };

        /// <summary>Board texture of the given pixel size (rounded to a multiple of 15). Cached per size.</summary>
        public static Texture2D Board(int pixels)
        {
            int size = Mathf.Clamp(Mathf.RoundToInt(pixels / 15f) * 15, 300, 2100);
            if (boards.TryGetValue(size, out var cached) && cached != null) return cached;
            var texture = DrawBoard(size).ToTexture("LudoBoard" + size);
            boards[size] = texture;
            return texture;
        }

        public static Raster DrawBoard(int size)
        {
            float c = size / (float)LudoBoard.Grid;
            var r = new Raster(size, size, Palette.WithAlpha(LudoTheme.BoardBase, 0f));
            r.RoundRect(size * 0.5f, size * 0.5f, size * 0.5f - 1f, size * 0.5f - 1f, c * 0.5f, LudoTheme.BoardBase);

            for (int seat = 0; seat < 4; seat++) DrawYard(r, seat, c);

            for (int i = 0; i < LudoBoard.Track.Length; i++)
            {
                var cell = LudoBoard.Track[i];
                int startSeat = System.Array.IndexOf(LudoEngine.StartIndex, i);
                if (startSeat >= 0)
                {
                    DrawTile(r, cell, c, LudoTheme.Seat(startSeat));
                    DrawArrow(r, cell, c, LudoBoard.StartDirection[startSeat], Color.white);
                }
                else
                {
                    DrawTile(r, cell, c, LudoTheme.Tile);
                    if (LudoEngine.IsSafeSquare(i))
                        r.Polygon(Raster.StarPoints((cell.x + 0.5f) * c, (cell.y + 0.48f) * c, c * 0.33f, c * 0.14f), LudoTheme.Star, 1f, c * 0.02f);
                }
            }

            for (int seat = 0; seat < 4; seat++)
            {
                // Arrow on the square where this colour turns into its home column.
                var entry = LudoBoard.Track[(LudoEngine.StartIndex[seat] + LudoEngine.LastTrack) % LudoEngine.TrackLength];
                DrawArrow(r, entry, c, LudoBoard.StartDirection[seat], LudoTheme.Seat(seat));
                for (int step = 1; step <= 5; step++) DrawTile(r, LudoBoard.HomeColumnCell(seat, step), c, LudoTheme.Seat(seat));
            }

            DrawCenter(r, c);
            return r;
        }

        static void DrawTile(Raster r, Vector2Int cell, float c, Color fill)
        {
            float gap = Mathf.Max(1f, c * 0.04f);
            float half = c * 0.5f - gap;
            float cx = (cell.x + 0.5f) * c, cy = (cell.y + 0.5f) * c;
            var paint = Paint.Vertical(Palette.Darken(fill, 0.05f), Palette.Lighten(fill, 0.08f), cy - half, cy + half);
            r.RoundRect(cx, cy, half, half, c * 0.16f, paint);
        }

        static void DrawArrow(Raster r, Vector2Int cell, float c, Vector2Int direction, Color color)
        {
            float degrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            var points = Raster.Transform(ArrowShape, (cell.x + 0.5f) * c, (cell.y + 0.5f) * c, c, degrees);
            r.Polygon(points, color, 1f, c * 0.025f);
        }

        static void DrawYard(Raster r, int seat, float c)
        {
            var origin = LudoBoard.YardOrigin[seat];
            float cx = (origin.x + 3f) * c, cy = (origin.y + 3f) * c;
            Color color = LudoTheme.Seat(seat);
            float half = 3f * c - c * 0.07f;
            r.RoundRect(cx, cy, half, half, c * 0.55f,
                Paint.Vertical(Palette.Darken(color, 0.1f), Palette.Lighten(color, 0.1f), cy - half, cy + half));
            r.RoundRect(cx, cy - c * 0.04f, 2.12f * c, 2.12f * c, c * 0.5f, new Color(0f, 0f, 0f, 0.18f), c * 0.12f);
            r.RoundRect(cx, cy, 2.1f * c, 2.1f * c, c * 0.48f, Color.white);
            for (int t = 0; t < 4; t++)
            {
                var slot = LudoBoard.YardSlot(seat, t) * c;
                r.Circle(slot.x, slot.y, c * 0.62f, LudoTheme.SeatLight(seat));
                r.Ring(slot.x, slot.y, c * 0.58f, c * 0.1f, Palette.Lighten(color, 0.4f));
            }
        }

        static void DrawCenter(Raster r, float c)
        {
            float mid = 7.5f * c;
            r.RoundRect(mid, mid, 1.5f * c, 1.5f * c, c * 0.1f, Color.white);
            var m = new Vector2(mid, mid);
            var bl = new Vector2(6f * c, 6f * c);
            var tl = new Vector2(6f * c, 9f * c);
            var tr = new Vector2(9f * c, 9f * c);
            var br = new Vector2(9f * c, 6f * c);
            float inset = -c * 0.05f;
            r.Polygon(new[] { bl, br, m }, TriPaint(0, c), 1f, inset);
            r.Polygon(new[] { tl, bl, m }, TriPaint(1, c), 1f, inset);
            r.Polygon(new[] { tr, tl, m }, TriPaint(2, c), 1f, inset);
            r.Polygon(new[] { br, tr, m }, TriPaint(3, c), 1f, inset);
            r.Circle(mid, mid, c * 0.2f, Color.white);
        }

        static Paint TriPaint(int seat, float c)
        {
            var color = LudoTheme.Seat(seat);
            return Paint.Vertical(Palette.Darken(color, 0.06f), Palette.Lighten(color, 0.06f), 6f * c, 9f * c);
        }

        /// <summary>Pin-shaped token. The sprite pivot is the pin's tip.</summary>
        public static Sprite Token(int seat)
        {
            if (tokens[seat] != null) return tokens[seat];
            const int width = 160, height = 212;
            const float tipY = 12f;
            var r = new Raster(width, height, Palette.WithAlpha(Palette.Darken(LudoTheme.Seat(seat), 0.5f), 0f));
            DrawPin(r, width * 0.5f, tipY, 62f, LudoTheme.Seat(seat));
            tokens[seat] = r.ToSprite("Token" + seat, new Vector2(0.5f, tipY / height));
            return tokens[seat];
        }

        /// <summary>Draws a pin whose tip is at (cx, tipY) and whose head has the given radius.</summary>
        public static void DrawPin(Raster r, float cx, float tipY, float radius, Color color)
        {
            float headY = tipY + radius * 1.84f;
            float stroke = radius * 0.1f;
            Color outline = Palette.Darken(color, 0.5f);
            var outlinePoints = PinOutline(cx, headY, radius, tipY, 64);
            r.Polygon(outlinePoints, outline, 1f, radius / 62f);
            r.Polygon(outlinePoints, Paint.Vertical(Palette.Darken(color, 0.15f), Palette.Lighten(color, 0.22f), tipY, headY + radius), 1f, -stroke);
            r.Circle(cx, headY, radius * 0.56f, Palette.Darken(color, 0.25f));
            r.Circle(cx, headY, radius * 0.5f, Color.white);
            r.Circle(cx, headY, radius * 0.3f, Paint.Vertical(Palette.Darken(color, 0.1f), Palette.Lighten(color, 0.15f), headY - radius * 0.3f, headY + radius * 0.3f));
            r.Circle(cx - radius * 0.42f, headY + radius * 0.42f, radius * 0.13f, new Color(1f, 1f, 1f, 0.6f), radius * 0.05f);
        }

        /// <summary>1024-style app icon: a stylised board with a big pin, on the app background.</summary>
        public static Raster AppIcon(int size)
        {
            var r = new Raster(size, size, Palette.BackgroundBottom);
            float s = size;
            r.RoundRect(s * 0.5f, s * 0.5f, s * 0.5f, s * 0.5f, 0f, Paint.Vertical(Palette.BackgroundBottom, Palette.BackgroundTop, 0f, s));

            float boardHalf = s * 0.36f;
            float cx = s * 0.5f, cy = s * 0.47f;
            r.RoundRect(cx, cy - s * 0.02f, boardHalf + s * 0.02f, boardHalf + s * 0.02f, s * 0.09f, new Color(0f, 0f, 0f, 0.45f), s * 0.06f);
            r.RoundRect(cx, cy, boardHalf + s * 0.015f, boardHalf + s * 0.015f, s * 0.08f, LudoTheme.Frame);
            r.RoundRect(cx, cy, boardHalf, boardHalf, s * 0.07f, LudoTheme.BoardBase);

            float cell = boardHalf * 2f / LudoBoard.Grid;
            float left = cx - boardHalf, bottom = cy - boardHalf;
            for (int seat = 0; seat < 4; seat++)
            {
                var origin = LudoBoard.YardOrigin[seat];
                float yx = left + (origin.x + 3f) * cell, yy = bottom + (origin.y + 3f) * cell;
                r.RoundRect(yx, yy, cell * 2.9f, cell * 2.9f, cell * 0.8f, LudoTheme.Seat(seat));
                r.RoundRect(yx, yy, cell * 1.9f, cell * 1.9f, cell * 0.6f, Color.white);
                r.Circle(yx, yy, cell * 1.05f, LudoTheme.SeatLight(seat));
            }
            // Arms of the cross in white, then the coloured home columns.
            r.RoundRect(cx, cy, cell * 1.45f, boardHalf - cell * 0.05f, cell * 0.2f, Color.white);
            r.RoundRect(cx, cy, boardHalf - cell * 0.05f, cell * 1.45f, cell * 0.2f, Color.white);
            for (int seat = 0; seat < 4; seat++)
            {
                var a = LudoBoard.CellCenter(LudoBoard.HomeColumnCell(seat, 1));
                var b = LudoBoard.CellCenter(LudoBoard.HomeColumnCell(seat, 5));
                r.Segment(left + a.x * cell, bottom + a.y * cell, left + b.x * cell, bottom + b.y * cell, cell * 0.42f, LudoTheme.Seat(seat));
            }
            var m = new Vector2(cx, cy);
            r.Polygon(new[] { new Vector2(left + 6f * cell, bottom + 6f * cell), new Vector2(left + 9f * cell, bottom + 6f * cell), m }, LudoTheme.Seat(0));
            r.Polygon(new[] { new Vector2(left + 6f * cell, bottom + 9f * cell), new Vector2(left + 6f * cell, bottom + 6f * cell), m }, LudoTheme.Seat(1));
            r.Polygon(new[] { new Vector2(left + 9f * cell, bottom + 9f * cell), new Vector2(left + 6f * cell, bottom + 9f * cell), m }, LudoTheme.Seat(2));
            r.Polygon(new[] { new Vector2(left + 9f * cell, bottom + 6f * cell), new Vector2(left + 9f * cell, bottom + 9f * cell), m }, LudoTheme.Seat(3));

            float pinRadius = s * 0.13f;
            float tip = cy - s * 0.03f;
            r.Circle(cx, tip, pinRadius * 0.55f, new Color(0f, 0f, 0f, 0.35f), pinRadius * 0.5f);
            DrawPin(r, cx, tip, pinRadius, LudoTheme.Seat(0));
            return r;
        }

        /// <summary>Soft ellipse drawn under a token.</summary>
        public static Sprite TokenShadow
        {
            get
            {
                if (tokenShadow != null) return tokenShadow;
                var r = new Raster(128, 64, new Color(0f, 0f, 0f, 0f));
                r.Circle(64f, 32f, 20f, new Color(0f, 0f, 0f, 1f), 28f);
                tokenShadow = r.ToSprite("TokenShadow", new Vector2(0.5f, 0.5f));
                return tokenShadow;
            }
        }

        public static Sprite DiceFace(int value)
        {
            value = Mathf.Clamp(value, 1, 6);
            if (diceFaces[value] != null) return diceFaces[value];
            const int size = 180;
            var r = new Raster(size, size, new Color(1f, 1f, 1f, 0f));
            const float center = size * 0.5f;
            r.RoundRect(center, center - 5f, 76f, 76f, 30f, new Color(0f, 0f, 0f, 0.3f), 10f);
            r.RoundRect(center, center - 2f, 76f, 76f, 28f, Palette.Hex(0xC9D0DE));
            r.RoundRect(center, center + 4f, 72f, 72f, 26f, Paint.Vertical(Palette.Hex(0xE8ECF4), Color.white, center - 68f, center + 76f));

            const float s = 38f;
            Color pip = value == 1 ? Palette.Hex(0xE84A43) : Palette.Hex(0x1D2346);
            foreach (var p in PipLayout(value))
            {
                float px = center + p.x * s, py = center + 4f + p.y * s;
                r.Circle(px, py - 1.5f, value == 1 ? 20f : 14f, Palette.WithAlpha(Color.black, 0.15f), 3f);
                r.Circle(px, py, value == 1 ? 19f : 13f, pip);
            }
            diceFaces[value] = r.ToSprite("Dice" + value, new Vector2(0.5f, 0.5f));
            return diceFaces[value];
        }

        static IEnumerable<Vector2> PipLayout(int value)
        {
            if (value % 2 == 1) yield return Vector2.zero;
            if (value >= 2)
            {
                yield return new Vector2(-1f, 1f);
                yield return new Vector2(1f, -1f);
            }
            if (value >= 4)
            {
                yield return new Vector2(1f, 1f);
                yield return new Vector2(-1f, -1f);
            }
            if (value == 6)
            {
                yield return new Vector2(-1f, 0f);
                yield return new Vector2(1f, 0f);
            }
        }

        static Vector2[] PinOutline(float cx, float headY, float radius, float tipY, int arcSegments)
        {
            // Tangent lines from the tip to the head circle, joined by the arc over the top.
            float distance = headY - tipY;
            float alpha = Mathf.Asin(radius / distance) * Mathf.Rad2Deg;
            var points = new Vector2[arcSegments + 2];
            points[0] = new Vector2(cx, tipY);
            float start = -alpha, sweep = 180f + 2f * alpha;
            for (int i = 0; i <= arcSegments; i++)
            {
                float a = (start + sweep * i / arcSegments) * Mathf.Deg2Rad;
                points[i + 1] = new Vector2(cx + Mathf.Cos(a) * radius, headY + Mathf.Sin(a) * radius);
            }
            return points;
        }
    }
}
