using System;
using System.Collections.Generic;

namespace NexioCraft.Brain
{
    /// <summary>Sliding number puzzle (15-puzzle and friends). The blank is the last cell when solved.</summary>
    public sealed class SlidingPuzzle
    {
        public int Size { get; }
        /// <summary>Tile number in each cell, row-major from the top; 0 is the blank.</summary>
        public int[] Cells { get; }
        public int Blank { get; private set; }
        public int Moves { get; private set; }

        public SlidingPuzzle(int size)
        {
            Size = size;
            Cells = new int[size * size];
            for (int i = 0; i < Cells.Length - 1; i++) Cells[i] = i + 1;
            Blank = Cells.Length - 1;
        }

        public bool IsSolved
        {
            get
            {
                for (int i = 0; i < Cells.Length - 1; i++)
                    if (Cells[i] != i + 1) return false;
                return Cells[Cells.Length - 1] == 0;
            }
        }

        /// <summary>Shuffles with random legal moves, so the result is always solvable.</summary>
        public void Shuffle(Random rng, int steps = 0)
        {
            if (steps <= 0) steps = Size * Size * 25;
            int previous = -1;
            for (int i = 0; i < steps; i++)
            {
                var options = new List<int>(4);
                foreach (int cell in Neighbours(Blank))
                    if (cell != previous) options.Add(cell);
                if (options.Count == 0) continue;
                int pick = options[rng.Next(options.Count)];
                previous = Blank;
                Slide(pick);
            }
            Moves = 0;
            if (IsSolved) Shuffle(rng, steps);
        }

        public bool CanSlide(int cell)
        {
            foreach (int neighbour in Neighbours(Blank))
                if (neighbour == cell) return true;
            return false;
        }

        /// <summary>Moves the tile in <paramref name="cell"/> into the blank, if they are neighbours.</summary>
        public bool Slide(int cell)
        {
            if (!CanSlide(cell)) return false;
            Cells[Blank] = Cells[cell];
            Cells[cell] = 0;
            Blank = cell;
            Moves++;
            return true;
        }

        public IEnumerable<int> Neighbours(int cell)
        {
            int x = cell % Size, y = cell / Size;
            if (x > 0) yield return cell - 1;
            if (x < Size - 1) yield return cell + 1;
            if (y > 0) yield return cell - Size;
            if (y < Size - 1) yield return cell + Size;
        }
    }

    public enum MathOperation { Add, Subtract, Multiply, Divide }

    /// <summary>One arithmetic question with four answers, one of them correct.</summary>
    public sealed class MathQuestion
    {
        public string Text;
        public int Answer;
        public int[] Options;
        public int CorrectIndex;

        /// <summary>Builds a question whose difficulty grows with <paramref name="level"/> (0 upwards).</summary>
        public static MathQuestion Create(Random rng, int level)
        {
            int tier = Math.Min(level / 5, 4);
            var operation = (MathOperation)rng.Next(tier < 1 ? 2 : tier < 2 ? 3 : 4);
            int a, b, answer;
            string symbol;
            switch (operation)
            {
                case MathOperation.Add:
                    a = rng.Next(2, 10 + tier * 12);
                    b = rng.Next(2, 10 + tier * 12);
                    answer = a + b;
                    symbol = "+";
                    break;
                case MathOperation.Subtract:
                    a = rng.Next(4, 12 + tier * 14);
                    b = rng.Next(1, a);
                    answer = a - b;
                    symbol = "-";
                    break;
                case MathOperation.Multiply:
                    a = rng.Next(2, 6 + tier * 3);
                    b = rng.Next(2, 6 + tier * 3);
                    answer = a * b;
                    symbol = "x";
                    break;
                default:
                    b = rng.Next(2, 6 + tier * 2);
                    answer = rng.Next(2, 6 + tier * 3);
                    a = b * answer;
                    symbol = "/";
                    break;
            }

            var question = new MathQuestion
            {
                Text = $"{a} {symbol} {b}",
                Answer = answer,
                Options = new int[4]
            };

            var used = new HashSet<int> { answer };
            var values = new List<int> { answer };
            while (values.Count < 4)
            {
                int spread = Math.Max(2, Math.Abs(answer) / 4 + tier + 1);
                int candidate = answer + rng.Next(-spread, spread + 1);
                if (candidate < 0 || !used.Add(candidate)) continue;
                values.Add(candidate);
            }
            for (int i = values.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
            for (int i = 0; i < 4; i++)
            {
                question.Options[i] = values[i];
                if (values[i] == answer) question.CorrectIndex = i;
            }
            return question;
        }
    }

    /// <summary>Deck for Memory Match: each symbol appears exactly twice, shuffled.</summary>
    public static class MemoryDeck
    {
        public static int[] Create(int pairs, Random rng)
        {
            var cards = new int[pairs * 2];
            for (int i = 0; i < pairs; i++)
            {
                cards[i * 2] = i;
                cards[i * 2 + 1] = i;
            }
            for (int i = cards.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }
            return cards;
        }
    }
}
