using UnityEngine;

namespace NexioCraft.Core
{
    /// <summary>App-wide colours plus small colour helpers.</summary>
    public static class Palette
    {
        public static readonly Color BackgroundTop = Hex(0x2A3468);
        public static readonly Color BackgroundBottom = Hex(0x0E1230);
        public static readonly Color Card = Hex(0x222B55);
        public static readonly Color CardRaised = Hex(0x2E3970);
        public static readonly Color Inset = Hex(0x161D40);
        public static readonly Color Text = Color.white;
        public static readonly Color TextDim = Hex(0xA9B3DA);
        public static readonly Color Gold = Hex(0xFFC53D);
        public static readonly Color Green = Hex(0x2BB673);
        public static readonly Color Blue = Hex(0x3F7CF7);
        public static readonly Color Orange = Hex(0xFF8A3D);
        public static readonly Color Red = Hex(0xE8484D);
        public static readonly Color Neutral = Hex(0x48548C);
        public static readonly Color Dim = new Color(0.02f, 0.03f, 0.1f, 0.72f);

        public static Color Hex(int rgb, float alpha = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);

        public static Color Lighten(Color c, float amount)
        {
            var result = Color.Lerp(c, Color.white, amount);
            result.a = c.a;
            return result;
        }

        public static Color Darken(Color c, float amount)
        {
            var result = Color.Lerp(c, Color.black, amount);
            result.a = c.a;
            return result;
        }

        public static Color WithAlpha(Color c, float alpha)
        {
            c.a = alpha;
            return c;
        }
    }
}
