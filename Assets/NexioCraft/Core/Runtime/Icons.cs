using System;
using System.Collections.Generic;
using UnityEngine;

namespace NexioCraft.Core
{
    public enum IconKind { Pause, Back, Close, SoundOn, SoundOff, Home, Restart, Gear, Person, Robot, Crown, Star, Check, Play, Vibrate, Help, Undo, Bulb, Flip, Flag }

    /// <summary>White 128px vector-style icons drawn procedurally (tint them with Image.color).</summary>
    public static class Icons
    {
        const int Size = 128;
        static readonly Dictionary<IconKind, Sprite> cache = new Dictionary<IconKind, Sprite>();

        public static Sprite Get(IconKind kind)
        {
            if (cache.TryGetValue(kind, out var sprite) && sprite != null) return sprite;
            var r = new Raster(Size, Size, new Color(1f, 1f, 1f, 0f));
            Draw(r, kind);
            sprite = r.ToSprite("Icon" + kind, new Vector2(0.5f, 0.5f));
            cache[kind] = sprite;
            return sprite;
        }

        static void Draw(Raster r, IconKind kind)
        {
            Color w = Color.white;
            switch (kind)
            {
                case IconKind.Pause:
                    r.RoundRect(44f, 64f, 12f, 38f, 8f, w);
                    r.RoundRect(84f, 64f, 12f, 38f, 8f, w);
                    break;

                case IconKind.Back:
                    r.Segment(78f, 100f, 42f, 64f, 11f, w);
                    r.Segment(42f, 64f, 78f, 28f, 11f, w);
                    break;

                case IconKind.Close:
                    r.Segment(36f, 36f, 92f, 92f, 11f, w);
                    r.Segment(36f, 92f, 92f, 36f, 11f, w);
                    break;

                case IconKind.SoundOn:
                    Speaker(r, 0f);
                    r.Arc(66f, 64f, 24f, 9f, -50f, 50f, w);
                    r.Arc(66f, 64f, 42f, 9f, -50f, 50f, w);
                    break;

                case IconKind.SoundOff:
                    Speaker(r, 0f);
                    r.Segment(86f, 48f, 112f, 80f, 7f, w);
                    r.Segment(86f, 80f, 112f, 48f, 7f, w);
                    break;

                case IconKind.Home:
                    r.Polygon(new[]
                    {
                        new Vector2(16f, 62f), new Vector2(64f, 108f), new Vector2(112f, 62f), new Vector2(98f, 62f),
                        new Vector2(98f, 20f), new Vector2(30f, 20f), new Vector2(30f, 62f)
                    }, w, 1f, 3f);
                    r.EraseMode = true;
                    r.RoundRect(64f, 34f, 12f, 16f, 4f, w);
                    r.EraseMode = false;
                    break;

                case IconKind.Restart:
                {
                    r.Arc(64f, 62f, 36f, 13f, 110f, 400f, w);
                    float a = 110f * Mathf.Deg2Rad;
                    var tip = new Vector2(64f + Mathf.Cos(a) * 36f, 62f + Mathf.Sin(a) * 36f);
                    r.Polygon(new[] { tip + new Vector2(-4f, 24f), tip + new Vector2(-4f, -24f), tip + new Vector2(-30f, 0f) }, w, 1f, 3f);
                    break;
                }

                case IconKind.Gear:
                {
                    var points = new List<Vector2>();
                    const int teeth = 8;
                    for (int i = 0; i < teeth; i++)
                    {
                        float baseAngle = i * 360f / teeth;
                        AddPolar(points, baseAngle - 22.5f + 5f, 40f);
                        AddPolar(points, baseAngle - 11f, 54f);
                        AddPolar(points, baseAngle + 11f, 54f);
                        AddPolar(points, baseAngle + 22.5f - 5f, 40f);
                    }
                    r.Polygon(points.ToArray(), w, 1f, 2f);
                    r.EraseMode = true;
                    r.Circle(64f, 64f, 17f, w);
                    r.EraseMode = false;
                    break;
                }

                case IconKind.Person:
                    r.Circle(64f, 84f, 23f, w);
                    r.Circle(64f, 10f, 46f, w);
                    r.EraseMode = true;
                    r.Ring(64f, 84f, 29f, 6f, w);
                    r.EraseMode = false;
                    break;

                case IconKind.Robot:
                    r.Segment(64f, 88f, 64f, 104f, 5f, w);
                    r.Circle(64f, 108f, 9f, w);
                    r.RoundRect(64f, 58f, 42f, 32f, 14f, w);
                    r.RoundRect(16f, 58f, 6f, 14f, 4f, w);
                    r.RoundRect(112f, 58f, 6f, 14f, 4f, w);
                    r.EraseMode = true;
                    r.Circle(47f, 64f, 10f, w);
                    r.Circle(81f, 64f, 10f, w);
                    r.RoundRect(64f, 42f, 16f, 4f, 4f, w);
                    r.EraseMode = false;
                    break;

                case IconKind.Crown:
                    r.Polygon(new[]
                    {
                        new Vector2(16f, 34f), new Vector2(14f, 86f), new Vector2(42f, 60f), new Vector2(64f, 100f),
                        new Vector2(86f, 60f), new Vector2(114f, 86f), new Vector2(112f, 34f)
                    }, w, 1f, 3f);
                    r.RoundRect(64f, 22f, 48f, 8f, 5f, w);
                    r.Circle(14f, 90f, 8f, w);
                    r.Circle(64f, 104f, 8f, w);
                    r.Circle(114f, 90f, 8f, w);
                    break;

                case IconKind.Star:
                    r.Polygon(Raster.StarPoints(64f, 60f, 56f, 24f), w, 1f, 4f);
                    break;

                case IconKind.Check:
                    r.Segment(28f, 66f, 52f, 40f, 11f, w);
                    r.Segment(52f, 40f, 100f, 90f, 11f, w);
                    break;

                case IconKind.Play:
                    r.Polygon(new[] { new Vector2(42f, 26f), new Vector2(42f, 102f), new Vector2(104f, 64f) }, w, 1f, 6f);
                    break;

                case IconKind.Vibrate:
                    r.RoundRect(64f, 64f, 22f, 40f, 9f, w);
                    r.EraseMode = true;
                    r.RoundRect(64f, 66f, 13f, 28f, 4f, w);
                    r.EraseMode = false;
                    r.Segment(22f, 44f, 22f, 84f, 5f, w);
                    r.Segment(106f, 44f, 106f, 84f, 5f, w);
                    r.Segment(8f, 54f, 8f, 74f, 5f, w);
                    r.Segment(120f, 54f, 120f, 74f, 5f, w);
                    break;

                case IconKind.Help:
                    r.Arc(64f, 80f, 24f, 14f, -60f, 180f, w);
                    r.Segment(76f, 60f, 64f, 50f, 7f, w);
                    r.Segment(64f, 50f, 64f, 40f, 7f, w);
                    r.Circle(64f, 20f, 9f, w);
                    break;

                case IconKind.Undo:
                    r.Polygon(new[] { new Vector2(16f, 84f), new Vector2(48f, 110f), new Vector2(48f, 58f) }, w, 1f, 3f);
                    r.Segment(46f, 84f, 74f, 84f, 7f, w);
                    r.Arc(74f, 56f, 28f, 14f, -90f, 90f, w);
                    r.Segment(74f, 28f, 36f, 28f, 7f, w);
                    break;

                case IconKind.Bulb:
                    r.Circle(64f, 78f, 34f, w);
                    r.RoundRect(64f, 42f, 18f, 12f, 4f, w);
                    r.Segment(48f, 26f, 80f, 26f, 5f, w);
                    r.Segment(52f, 14f, 76f, 14f, 5f, w);
                    r.EraseMode = true;
                    r.Arc(64f, 78f, 20f, 6f, 100f, 170f, w);
                    r.EraseMode = false;
                    break;

                case IconKind.Flip:
                    r.Segment(42f, 20f, 42f, 88f, 8f, w);
                    r.Polygon(new[] { new Vector2(42f, 114f), new Vector2(18f, 82f), new Vector2(66f, 82f) }, w, 1f, 3f);
                    r.Segment(86f, 108f, 86f, 40f, 8f, w);
                    r.Polygon(new[] { new Vector2(86f, 14f), new Vector2(62f, 46f), new Vector2(110f, 46f) }, w, 1f, 3f);
                    break;

                case IconKind.Flag:
                    r.Segment(32f, 14f, 32f, 112f, 6f, w);
                    r.Polygon(new[] { new Vector2(38f, 110f), new Vector2(108f, 90f), new Vector2(38f, 58f) }, w, 1f, 3f);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        static void Speaker(Raster r, float dx)
        {
            r.Polygon(new[]
            {
                new Vector2(14f + dx, 46f), new Vector2(38f + dx, 46f), new Vector2(66f + dx, 20f),
                new Vector2(66f + dx, 108f), new Vector2(38f + dx, 82f), new Vector2(14f + dx, 82f)
            }, Color.white, 1f, 3f);
        }

        static void AddPolar(List<Vector2> points, float degrees, float radius)
        {
            float a = degrees * Mathf.Deg2Rad;
            points.Add(new Vector2(64f + Mathf.Cos(a) * radius, 64f + Mathf.Sin(a) * radius));
        }
    }
}
