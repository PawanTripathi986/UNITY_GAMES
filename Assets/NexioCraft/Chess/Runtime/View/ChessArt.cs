using System.Collections.Generic;
using NexioCraft.Core;
using UnityEngine;

namespace NexioCraft.Chess
{
    public enum BoardTheme { Classic = 0, Green = 1, Blue = 2 }

    /// <summary>Procedural chess artwork: board squares, Staunton-style pieces and the app icon.</summary>
    public static class ChessArt
    {
        const int PieceSize = 256;
        const float Stroke = 7f;

        static readonly Dictionary<int, Sprite> pieces = new Dictionary<int, Sprite>();
        static readonly Dictionary<int, Texture2D> boards = new Dictionary<int, Texture2D>();

        public static Color LightSquare(BoardTheme theme) => theme switch
        {
            BoardTheme.Green => Palette.Hex(0xEEEED2),
            BoardTheme.Blue => Palette.Hex(0xDEE3E6),
            _ => Palette.Hex(0xF0D9B5)
        };

        public static Color DarkSquare(BoardTheme theme) => theme switch
        {
            BoardTheme.Green => Palette.Hex(0x769656),
            BoardTheme.Blue => Palette.Hex(0x8CA2AD),
            _ => Palette.Hex(0xB58863)
        };

        public static Color Frame(BoardTheme theme) => theme switch
        {
            BoardTheme.Green => Palette.Hex(0x4E6B3A),
            BoardTheme.Blue => Palette.Hex(0x5B6F7B),
            _ => Palette.Hex(0x7A5234)
        };

        /// <summary>8x8 board texture at roughly the given pixel size, a1 dark. Cached per size and theme.</summary>
        public static Texture2D Board(int pixels, BoardTheme theme)
        {
            int size = Mathf.Clamp(Mathf.RoundToInt(pixels / 8f) * 8, 64, 2048);
            int key = size * 10 + (int)theme;
            if (boards.TryGetValue(key, out var cached) && cached != null) return cached;

            float c = size / 8f;
            Color light = LightSquare(theme), dark = DarkSquare(theme);
            var r = new Raster(size, size, light);
            for (int rank = 0; rank < 8; rank++)
            {
                for (int file = 0; file < 8; file++)
                {
                    if (((rank + file) & 1) != 0) continue;
                    float cx = (file + 0.5f) * c, cy = (rank + 0.5f) * c;
                    // Slightly oversized so neighbouring dark squares never show seams.
                    r.RoundRect(cx, cy, c * 0.5f + 0.5f, c * 0.5f + 0.5f, 0f, dark, 0.5f);
                }
            }
            var texture = r.ToTexture("ChessBoard" + size);
            texture.filterMode = FilterMode.Point;
            boards[key] = texture;
            return texture;
        }

        /// <summary>Sprite for a piece code (positive white, negative black). Pivot is the centre.</summary>
        public static Sprite PieceSprite(int piece)
        {
            if (pieces.TryGetValue(piece, out var sprite) && sprite != null) return sprite;
            var raster = new Raster(PieceSize, PieceSize, new Color(0f, 0f, 0f, 0f));
            DrawPiece(raster, piece, PieceSize * 0.5f, 0f, 1f, true);
            sprite = raster.ToSprite("Piece" + piece, new Vector2(0.5f, 0.5f));
            pieces[piece] = sprite;
            return sprite;
        }

        /// <summary>Draws a piece designed on a 256px canvas, positioned at (cx, bottom) and scaled.</summary>
        public static void DrawPiece(Raster r, int piece, float cx, float bottom, float scale, bool groundShadow)
        {
            bool white = piece > 0;
            int type = Piece.TypeOf(piece);
            var t = new Transform2(cx - 128f * scale, bottom, scale);
            Color outline = white ? Palette.Hex(0x2B2723) : Palette.Hex(0x0B0B0E);
            Paint fill = white
                ? Paint.Vertical(Palette.Hex(0xD6CEBF), Palette.Hex(0xFFFFFF), t.Y(20f), t.Y(240f))
                : Paint.Vertical(Palette.Hex(0x1A1A20), Palette.Hex(0x50505C), t.Y(20f), t.Y(240f));
            Color detail = white ? outline : Palette.Hex(0x8C8C99);

            if (groundShadow) r.RoundRect(t.X(128f), t.Y(26f), 82f * scale, 12f * scale, 12f * scale, new Color(0f, 0f, 0f, 0.28f), 18f * scale);

            var shapes = Shapes(type);
            foreach (var shape in shapes) shape.Draw(r, t, outline, Stroke);
            foreach (var shape in shapes) shape.Draw(r, t, fill, 0f);

            foreach (var line in Details(type)) line.Draw(r, t, detail, 0f);
        }

        static List<Shape> Shapes(int type)
        {
            var list = new List<Shape>
            {
                new Box(128f, 40f, 80f, 15f, 12f),
                new Box(128f, 62f, 62f, 9f, 8f)
            };
            switch (type)
            {
                case Piece.Pawn:
                    list.Add(new Poly(84, 68, 98, 104, 106, 134, 108, 150, 148, 150, 150, 134, 158, 104, 172, 68));
                    list.Add(new Box(128f, 150f, 38f, 8f, 7f));
                    list.Add(new Circle(128f, 192f, 34f));
                    break;
                case Piece.Rook:
                    list.Add(new Poly(82, 68, 90, 110, 94, 160, 162, 160, 166, 110, 174, 68));
                    list.Add(new Box(128f, 164f, 50f, 9f, 6f));
                    list.Add(new Poly(74, 172, 74, 232, 96, 232, 96, 214, 116, 214, 116, 232, 140, 232, 140, 214, 160, 214, 160, 232, 182, 232, 182, 172));
                    break;
                case Piece.Knight:
                    list.Add(new Poly(174, 68, 186, 112, 182, 152, 168, 190, 148, 218, 128, 240, 118, 218, 98, 208, 72, 186, 48, 152, 46, 130, 60, 118, 82, 124, 102, 138, 114, 122, 94, 68));
                    break;
                case Piece.Bishop:
                    list.Add(new Poly(88, 68, 102, 108, 112, 140, 144, 140, 154, 108, 168, 68));
                    list.Add(new Box(128f, 144f, 36f, 8f, 7f));
                    list.Add(new Poly(Teardrop(128f, 182f, 32f, 228f)));
                    list.Add(new Circle(128f, 234f, 10f));
                    break;
                case Piece.Queen:
                    list.Add(new Poly(86, 68, 100, 112, 110, 150, 146, 150, 156, 112, 170, 68));
                    list.Add(new Box(128f, 154f, 38f, 8f, 7f));
                    list.Add(new Poly(108, 160, 80, 214, 96, 198, 104, 222, 116, 198, 128, 226, 140, 198, 152, 222, 160, 198, 176, 214, 148, 160));
                    list.Add(new Circle(80f, 218f, 9f));
                    list.Add(new Circle(104f, 228f, 9f));
                    list.Add(new Circle(128f, 234f, 10f));
                    list.Add(new Circle(152f, 228f, 9f));
                    list.Add(new Circle(176f, 218f, 9f));
                    break;
                default:
                    list.Add(new Poly(84, 68, 98, 112, 108, 154, 148, 154, 158, 112, 172, 68));
                    list.Add(new Box(128f, 158f, 40f, 8f, 7f));
                    list.Add(new Poly(106, 164, 86, 206, 104, 214, 128, 218, 152, 214, 170, 206, 150, 164));
                    list.Add(new Capsule(128f, 208f, 128f, 236f, 7f));
                    list.Add(new Capsule(115f, 224f, 141f, 224f, 7f));
                    break;
            }
            return list;
        }

        static List<Shape> Details(int type)
        {
            var list = new List<Shape> { new Capsule(62f, 54f, 194f, 54f, 2.5f) };
            switch (type)
            {
                case Piece.Pawn:
                    list.Add(new Capsule(94f, 143f, 162f, 143f, 2.5f));
                    break;
                case Piece.Rook:
                    list.Add(new Capsule(82f, 156f, 174f, 156f, 2.5f));
                    list.Add(new Capsule(78f, 173f, 178f, 173f, 2.5f));
                    break;
                case Piece.Knight:
                    list.Add(new Circle(104f, 184f, 7f));
                    list.Add(new Circle(57f, 138f, 4f));
                    list.Add(new Capsule(162f, 200f, 176f, 150f, 2.5f));
                    break;
                case Piece.Bishop:
                    list.Add(new Capsule(96f, 137f, 160f, 137f, 2.5f));
                    list.Add(new Capsule(115f, 172f, 137f, 200f, 3.5f));
                    break;
                case Piece.Queen:
                    list.Add(new Capsule(94f, 147f, 162f, 147f, 2.5f));
                    break;
                default:
                    list.Add(new Capsule(92f, 151f, 164f, 151f, 2.5f));
                    list.Add(new Capsule(94f, 170f, 162f, 170f, 2.5f));
                    break;
            }
            return list;
        }

        /// <summary>Outline of a circle joined by tangents to a point above it (a bishop's mitre).</summary>
        static float[] Teardrop(float cx, float cy, float radius, float tipY)
        {
            const int segments = 40;
            float distance = tipY - cy;
            float beta = Mathf.Acos(radius / distance) * Mathf.Rad2Deg;
            var coords = new float[(segments + 2) * 2];
            coords[0] = cx;
            coords[1] = tipY;
            float start = 90f - beta, sweep = 360f - 2f * beta;
            for (int i = 0; i <= segments; i++)
            {
                float a = (start - sweep * i / segments) * Mathf.Deg2Rad;
                coords[(i + 1) * 2] = cx + Mathf.Cos(a) * radius;
                coords[(i + 1) * 2 + 1] = cy + Mathf.Sin(a) * radius;
            }
            return coords;
        }

        /// <summary>Chess app icon: board corner with a large white knight.</summary>
        public static Raster AppIcon(int size)
        {
            float s = size;
            var r = new Raster(size, size, Palette.BackgroundBottom);
            r.RoundRect(s * 0.5f, s * 0.5f, s * 0.5f, s * 0.5f, 0f, Paint.Vertical(Palette.BackgroundBottom, Palette.BackgroundTop, 0f, s));

            float half = s * 0.34f, cx = s * 0.5f, cy = s * 0.46f;
            r.RoundRect(cx, cy - s * 0.02f, half + s * 0.03f, half + s * 0.03f, s * 0.08f, new Color(0f, 0f, 0f, 0.45f), s * 0.06f);
            r.RoundRect(cx, cy, half + s * 0.025f, half + s * 0.025f, s * 0.07f, Frame(BoardTheme.Classic));
            float cell = half * 2f / 4f;
            float radius = s * 0.05f;
            // Dark rounded base, then light squares; light corner squares keep only their outer corner rounded.
            r.RoundRect(cx, cy, half, half, radius, DarkSquare(BoardTheme.Classic));
            Color light = LightSquare(BoardTheme.Classic);
            for (int rank = 0; rank < 4; rank++)
            {
                for (int file = 0; file < 4; file++)
                {
                    if (((rank + file) & 1) == 0) continue;
                    float x = cx - half + (file + 0.5f) * cell, y = cy - half + (rank + 0.5f) * cell;
                    float h = cell * 0.5f + 0.5f;
                    bool corner = (rank == 0 || rank == 3) && (file == 0 || file == 3);
                    if (!corner)
                    {
                        r.RoundRect(x, y, h, h, 0f, light, 0.5f);
                        continue;
                    }
                    float inwardX = file == 0 ? 1f : -1f, inwardY = rank == 0 ? 1f : -1f;
                    r.RoundRect(x, y, h, h, radius, light);
                    r.RoundRect(x + inwardX * h * 0.5f, y, h * 0.5f, h, 0f, light, 0.5f);
                    r.RoundRect(x, y + inwardY * h * 0.5f, h, h * 0.5f, 0f, light, 0.5f);
                }
            }
            DrawPiece(r, Piece.Knight, cx + s * 0.02f, cy - half * 0.95f, s * 0.00255f, true);
            return r;
        }

        readonly struct Transform2
        {
            readonly float offsetX;
            readonly float offsetY;
            public readonly float Scale;

            public Transform2(float offsetX, float bottom, float scale)
            {
                this.offsetX = offsetX;
                offsetY = bottom;
                Scale = scale;
            }

            public float X(float x) => offsetX + x * Scale;
            public float Y(float y) => offsetY + y * Scale;
        }

        abstract class Shape
        {
            public abstract void Draw(Raster r, Transform2 t, Paint paint, float grow);
        }

        sealed class Box : Shape
        {
            readonly float cx, cy, halfWidth, halfHeight, radius;

            public Box(float cx, float cy, float halfWidth, float halfHeight, float radius)
            {
                this.cx = cx;
                this.cy = cy;
                this.halfWidth = halfWidth;
                this.halfHeight = halfHeight;
                this.radius = radius;
            }

            public override void Draw(Raster r, Transform2 t, Paint paint, float grow) =>
                r.RoundRect(t.X(cx), t.Y(cy), (halfWidth + grow) * t.Scale, (halfHeight + grow) * t.Scale, (radius + grow) * t.Scale, paint);
        }

        sealed class Circle : Shape
        {
            readonly float cx, cy, radius;

            public Circle(float cx, float cy, float radius)
            {
                this.cx = cx;
                this.cy = cy;
                this.radius = radius;
            }

            public override void Draw(Raster r, Transform2 t, Paint paint, float grow) =>
                r.Circle(t.X(cx), t.Y(cy), (radius + grow) * t.Scale, paint);
        }

        sealed class Capsule : Shape
        {
            readonly float ax, ay, bx, by, radius;

            public Capsule(float ax, float ay, float bx, float by, float radius)
            {
                this.ax = ax;
                this.ay = ay;
                this.bx = bx;
                this.by = by;
                this.radius = radius;
            }

            public override void Draw(Raster r, Transform2 t, Paint paint, float grow) =>
                r.Segment(t.X(ax), t.Y(ay), t.X(bx), t.Y(by), (radius + grow) * t.Scale, paint);
        }

        sealed class Poly : Shape
        {
            readonly float[] coords;

            public Poly(params float[] coords) => this.coords = coords;

            public override void Draw(Raster r, Transform2 t, Paint paint, float grow)
            {
                var points = new Vector2[coords.Length / 2];
                for (int i = 0; i < points.Length; i++) points[i] = new Vector2(t.X(coords[i * 2]), t.Y(coords[i * 2 + 1]));
                r.Polygon(points, paint, 1f, (grow + 2f) * t.Scale);
            }
        }
    }
}
