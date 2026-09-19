using System;
using System.Collections.Generic;
using System.Text;

namespace NexioCraft.Sort
{
    /// <summary>One pour: <see cref="Count"/> items of one colour moved from one tube to another.</summary>
    public readonly struct SortMove
    {
        public readonly int From;
        public readonly int To;
        public readonly int Count;

        public SortMove(int from, int to, int count)
        {
            From = from;
            To = to;
            Count = count;
        }

        public override string ToString() => $"{From}->{To} x{Count}";
    }

    /// <summary>
    /// Tubes of coloured liquid. A pour moves the whole run of equal colours from the top of one tube onto a
    /// matching colour (or into an empty tube), as far as it fits. The puzzle is done when every tube is either
    /// empty or filled with one colour.
    /// </summary>
    public sealed class SortBoard
    {
        public const int Capacity = 4;

        /// <summary>Colours stack from the bottom: cells[tube * Capacity + slot], 0 means empty.</summary>
        readonly List<int> cells = new List<int>();
        readonly List<SortMove> history = new List<SortMove>();

        public int TubeCount { get; private set; }
        public int Moves => history.Count;
        public IReadOnlyList<SortMove> History => history;

        public SortBoard(int tubes)
        {
            TubeCount = tubes;
            for (int i = 0; i < tubes * Capacity; i++) cells.Add(0);
        }

        public int this[int tube, int slot]
        {
            get => cells[tube * Capacity + slot];
            set => cells[tube * Capacity + slot] = value;
        }

        public int Height(int tube)
        {
            for (int slot = Capacity - 1; slot >= 0; slot--)
                if (this[tube, slot] != 0) return slot + 1;
            return 0;
        }

        public bool IsEmpty(int tube) => Height(tube) == 0;
        public bool IsFull(int tube) => Height(tube) == Capacity;

        /// <summary>Colour on top of the tube, or 0 when empty.</summary>
        public int Top(int tube)
        {
            int height = Height(tube);
            return height == 0 ? 0 : this[tube, height - 1];
        }

        /// <summary>How many equal colours sit on top.</summary>
        public int TopRun(int tube)
        {
            int height = Height(tube);
            if (height == 0) return 0;
            int colour = this[tube, height - 1];
            int run = 1;
            for (int slot = height - 2; slot >= 0 && this[tube, slot] == colour; slot--) run++;
            return run;
        }

        /// <summary>True when the tube holds one colour only (and is not empty).</summary>
        public bool IsUniform(int tube)
        {
            int height = Height(tube);
            return height > 0 && TopRun(tube) == height;
        }

        public bool CanPour(int from, int to)
        {
            if (from == to || from < 0 || to < 0 || from >= TubeCount || to >= TubeCount) return false;
            if (IsEmpty(from) || IsFull(to)) return false;
            int target = Top(to);
            return target == 0 || target == Top(from);
        }

        /// <summary>A pour that actually helps: shifting a whole one-colour tube into an empty one does not.</summary>
        public bool IsUseful(int from, int to)
        {
            if (!CanPour(from, to)) return false;
            if (IsEmpty(to) && IsUniform(from)) return false;
            return true;
        }

        public bool HasUsefulMove()
        {
            for (int from = 0; from < TubeCount; from++)
                for (int to = 0; to < TubeCount; to++)
                    if (IsUseful(from, to)) return true;
            return false;
        }

        /// <summary>Pours and returns how many items moved (0 when the move is not allowed).</summary>
        public int Pour(int from, int to)
        {
            if (!CanPour(from, to)) return 0;
            int count = Math.Min(TopRun(from), Capacity - Height(to));
            int colour = Top(from);
            int fromHeight = Height(from);
            int toHeight = Height(to);
            for (int i = 0; i < count; i++)
            {
                this[from, fromHeight - 1 - i] = 0;
                this[to, toHeight + i] = colour;
            }
            history.Add(new SortMove(from, to, count));
            return count;
        }

        public bool Undo()
        {
            if (history.Count == 0) return false;
            var move = history[history.Count - 1];
            history.RemoveAt(history.Count - 1);
            int colour = Top(move.To);
            int toHeight = Height(move.To);
            int fromHeight = Height(move.From);
            for (int i = 0; i < move.Count; i++)
            {
                this[move.To, toHeight - 1 - i] = 0;
                this[move.From, fromHeight + i] = colour;
            }
            return true;
        }

        public void ClearHistory() => history.Clear();

        /// <summary>Adds an empty tube (the rewarded "extra tube" and the reason undo is blocked afterwards).</summary>
        public void AddTube()
        {
            TubeCount++;
            for (int i = 0; i < Capacity; i++) cells.Add(0);
            history.Clear();
        }

        public bool Solved
        {
            get
            {
                for (int tube = 0; tube < TubeCount; tube++)
                {
                    int height = Height(tube);
                    if (height == 0) continue;
                    if (height != Capacity || !IsUniform(tube)) return false;
                }
                return true;
            }
        }

        public SortBoard Clone()
        {
            var copy = new SortBoard(TubeCount);
            for (int i = 0; i < cells.Count; i++) copy.cells[i] = cells[i];
            return copy;
        }

        /// <summary>Same tubes in any order compare equal, which keeps the solver from re-exploring mirrored states.</summary>
        public string Key()
        {
            var tubes = new string[TubeCount];
            var builder = new StringBuilder(Capacity);
            for (int tube = 0; tube < TubeCount; tube++)
            {
                builder.Clear();
                for (int slot = 0; slot < Capacity; slot++) builder.Append((char)('a' + this[tube, slot]));
                tubes[tube] = builder.ToString();
            }
            Array.Sort(tubes, StringComparer.Ordinal);
            return string.Concat(tubes);
        }

        public int[] ToArray() => cells.ToArray();

        public static SortBoard FromArray(int[] values)
        {
            if (values == null || values.Length == 0 || values.Length % Capacity != 0) return null;
            var board = new SortBoard(values.Length / Capacity);
            for (int i = 0; i < values.Length; i++) board.cells[i] = values[i];
            return board;
        }
    }
}
