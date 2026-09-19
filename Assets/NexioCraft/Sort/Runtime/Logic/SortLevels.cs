using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace NexioCraft.Sort
{
    public struct SortLevel
    {
        public int Index;
        public int Colours;
        public int EmptyTubes;
        public int Seed;

        public int Number => Index + 1;
        public int TubeCount => Colours + EmptyTubes;
    }

    /// <summary>
    /// Endless levels. Difficulty comes from the number of colours; there are always two spare tubes.
    /// Every level is checked with the solver before it is handed out, so no level can be a dead end.
    /// </summary>
    public static class SortLevels
    {
        public const int EmptyTubes = 2;
        public const int MinColours = 3;
        public const int MaxColours = 11;

        public static SortLevel For(int index)
        {
            index = Mathf.Max(0, index);
            return new SortLevel
            {
                Index = index,
                Colours = Mathf.Clamp(MinColours + index / 3, MinColours, MaxColours),
                EmptyTubes = EmptyTubes,
                Seed = 8191 + index * 5279
            };
        }

        /// <summary>Builds the level's starting board: full tubes, shuffled, and solvable.</summary>
        public static SortBoard Create(SortLevel level)
        {
            var rng = new Random(level.Seed);
            // Random fills look the way players expect (every tube full), so try those first...
            for (int attempt = 0; attempt < 30; attempt++)
            {
                var board = RandomFill(level, rng);
                if (board.Solved) continue;
                if (SortSolver.TrySolve(board, out _, 60000)) return board;
            }
            // ...and fall back to unwinding a solved board, which cannot produce an unsolvable puzzle.
            return ReverseShuffle(level, rng);
        }

        static SortBoard RandomFill(SortLevel level, Random rng)
        {
            var pile = new List<int>(level.Colours * SortBoard.Capacity);
            for (int colour = 1; colour <= level.Colours; colour++)
                for (int i = 0; i < SortBoard.Capacity; i++) pile.Add(colour);
            for (int i = pile.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (pile[i], pile[j]) = (pile[j], pile[i]);
            }
            var board = new SortBoard(level.TubeCount);
            for (int i = 0; i < pile.Count; i++) board[i / SortBoard.Capacity, i % SortBoard.Capacity] = pile[i];
            return board;
        }

        /// <summary>
        /// Starts from the finished puzzle and undoes pours. Each step is the exact reverse of a legal pour,
        /// so replaying them backwards always solves the result.
        /// </summary>
        static SortBoard ReverseShuffle(SortLevel level, Random rng)
        {
            var board = new SortBoard(level.TubeCount);
            for (int colour = 1; colour <= level.Colours; colour++)
                for (int slot = 0; slot < SortBoard.Capacity; slot++) board[colour - 1, slot] = colour;

            int steps = 30 + level.Colours * 8;
            for (int step = 0; step < steps; step++)
            {
                var options = new List<SortMove>();
                for (int from = 0; from < board.TubeCount; from++)
                {
                    int height = board.Height(from);
                    if (height == 0) continue;
                    int run = board.TopRun(from);
                    int colour = board.Top(from);
                    for (int take = 1; take <= run; take++)
                    {
                        // Leave the same colour on top, or empty the tube: anything else could not be poured back.
                        if (take == run && take != height) continue;
                        for (int to = 0; to < board.TubeCount; to++)
                        {
                            if (to == from) continue;
                            if (SortBoard.Capacity - board.Height(to) < take) continue;
                            if (!board.IsEmpty(to) && board.Top(to) == colour) continue;
                            options.Add(new SortMove(from, to, take));
                        }
                    }
                }
                if (options.Count == 0) break;
                var move = options[rng.Next(options.Count)];
                int source = board.Height(move.From);
                int target = board.Height(move.To);
                int moved = board.Top(move.From);
                for (int i = 0; i < move.Count; i++)
                {
                    board[move.From, source - 1 - i] = 0;
                    board[move.To, target + i] = moved;
                }
            }
            board.ClearHistory();
            return board;
        }
    }
}
