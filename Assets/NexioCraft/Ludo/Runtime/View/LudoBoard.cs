using NexioCraft.Core;
using UnityEngine;

namespace NexioCraft.Ludo
{
    /// <summary>
    /// Geometry of the classic 15x15 board in "board units" (0..15, origin bottom-left, y up).
    /// Yards: Red bottom-left, Green top-left, Yellow top-right, Blue bottom-right; play runs clockwise.
    /// </summary>
    public static class LudoBoard
    {
        public const int Grid = 15;

        /// <summary>The 52 shared squares, clockwise, starting at Red's start square.</summary>
        public static readonly Vector2Int[] Track =
        {
            new Vector2Int(6, 1), new Vector2Int(6, 2), new Vector2Int(6, 3), new Vector2Int(6, 4), new Vector2Int(6, 5),
            new Vector2Int(5, 6), new Vector2Int(4, 6), new Vector2Int(3, 6), new Vector2Int(2, 6), new Vector2Int(1, 6), new Vector2Int(0, 6),
            new Vector2Int(0, 7),
            new Vector2Int(0, 8), new Vector2Int(1, 8), new Vector2Int(2, 8), new Vector2Int(3, 8), new Vector2Int(4, 8), new Vector2Int(5, 8),
            new Vector2Int(6, 9), new Vector2Int(6, 10), new Vector2Int(6, 11), new Vector2Int(6, 12), new Vector2Int(6, 13), new Vector2Int(6, 14),
            new Vector2Int(7, 14),
            new Vector2Int(8, 14), new Vector2Int(8, 13), new Vector2Int(8, 12), new Vector2Int(8, 11), new Vector2Int(8, 10), new Vector2Int(8, 9),
            new Vector2Int(9, 8), new Vector2Int(10, 8), new Vector2Int(11, 8), new Vector2Int(12, 8), new Vector2Int(13, 8), new Vector2Int(14, 8),
            new Vector2Int(14, 7),
            new Vector2Int(14, 6), new Vector2Int(13, 6), new Vector2Int(12, 6), new Vector2Int(11, 6), new Vector2Int(10, 6), new Vector2Int(9, 6),
            new Vector2Int(8, 5), new Vector2Int(8, 4), new Vector2Int(8, 3), new Vector2Int(8, 2), new Vector2Int(8, 1), new Vector2Int(8, 0),
            new Vector2Int(7, 0),
            new Vector2Int(6, 0)
        };

        /// <summary>Bottom-left corner of each seat's 6x6 yard.</summary>
        public static readonly Vector2Int[] YardOrigin = { new Vector2Int(0, 0), new Vector2Int(0, 9), new Vector2Int(9, 9), new Vector2Int(9, 0) };

        /// <summary>Direction of travel on each seat's start square (also the direction into its home column).</summary>
        public static readonly Vector2Int[] StartDirection = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        static readonly Vector2[] SlotOffsets = { new Vector2(2f, 2f), new Vector2(4f, 2f), new Vector2(2f, 4f), new Vector2(4f, 4f) };

        /// <summary>Home column square for steps 1..5 (progress 51..55).</summary>
        public static Vector2Int HomeColumnCell(int seat, int step)
        {
            switch (seat)
            {
                case 0: return new Vector2Int(7, step);
                case 1: return new Vector2Int(step, 7);
                case 2: return new Vector2Int(7, Grid - 1 - step);
                default: return new Vector2Int(Grid - 1 - step, 7);
            }
        }

        public static Vector2 CellCenter(Vector2Int cell) => new Vector2(cell.x + 0.5f, cell.y + 0.5f);

        public static Vector2 YardSlot(int seat, int token)
        {
            var origin = YardOrigin[seat];
            return new Vector2(origin.x, origin.y) + SlotOffsets[token];
        }

        /// <summary>Centre of each seat's triangle in the middle of the board.</summary>
        public static Vector2 HomeSpot(int seat)
        {
            switch (seat)
            {
                case 0: return new Vector2(7.5f, 6.55f);
                case 1: return new Vector2(6.55f, 7.5f);
                case 2: return new Vector2(7.5f, 8.45f);
                default: return new Vector2(8.45f, 7.5f);
            }
        }

        /// <summary>Where a token with the given progress sits, in board units.</summary>
        public static Vector2 TokenPoint(int seat, int token, int progress)
        {
            if (progress == LudoEngine.Yard) return YardSlot(seat, token);
            if (progress == LudoEngine.Home) return HomeSpot(seat);
            if (progress > LudoEngine.LastTrack) return CellCenter(HomeColumnCell(seat, progress - LudoEngine.LastTrack));
            return CellCenter(Track[LudoEngine.Absolute(seat, progress)]);
        }

        /// <summary>
        /// Key shared by tokens that are drawn on the same spot, so they can be fanned out.
        /// Yard slots are unique per token.
        /// </summary>
        public static int SpotKey(int seat, int token, int progress)
        {
            if (progress == LudoEngine.Yard) return 1000 + seat * 10 + token;
            if (progress == LudoEngine.Home) return 2000 + seat;
            if (progress > LudoEngine.LastTrack) return 3000 + seat * 10 + progress - LudoEngine.LastTrack;
            return LudoEngine.Absolute(seat, progress);
        }
    }

    public static class LudoTheme
    {
        public static readonly Color[] SeatColors =
        {
            Palette.Hex(0xE84A43), Palette.Hex(0x23A55A), Palette.Hex(0xF4B400), Palette.Hex(0x2F7BF5)
        };

        public static readonly string[] SeatNames = { "Red", "Green", "Yellow", "Blue" };

        public static readonly Color BoardBase = Palette.Hex(0xD6DDE9);
        public static readonly Color Tile = Palette.Hex(0xFFFFFF);
        public static readonly Color Star = Palette.Hex(0x9AA6BC);
        public static readonly Color Frame = Palette.Hex(0xF4F6FB);

        public static Color Seat(int seat) => SeatColors[seat];
        public static Color SeatDark(int seat) => Palette.Darken(SeatColors[seat], 0.35f);
        public static Color SeatLight(int seat) => Palette.Lighten(SeatColors[seat], 0.75f);
    }
}
