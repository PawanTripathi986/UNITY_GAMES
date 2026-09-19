using System;
using System.Collections.Generic;
using UnityEngine;

namespace NexioCraft.Blocks
{
    /// <summary>A piece: cell offsets with (0,0) at its top-left corner. y grows downwards, like board rows.</summary>
    public sealed class BlockShape
    {
        public readonly Vector2Int[] Cells;
        public readonly int Width;
        public readonly int Height;
        public readonly int Weight;

        public int Count => Cells.Length;

        public BlockShape(int weight, params int[] coords)
        {
            Weight = weight;
            Cells = new Vector2Int[coords.Length / 2];
            int maxX = 0, maxY = 0;
            for (int i = 0; i < Cells.Length; i++)
            {
                Cells[i] = new Vector2Int(coords[i * 2], coords[i * 2 + 1]);
                maxX = Math.Max(maxX, Cells[i].x);
                maxY = Math.Max(maxY, Cells[i].y);
            }
            Width = maxX + 1;
            Height = maxY + 1;
        }
    }

    public static class BlockShapes
    {
        /// <summary>Every piece the game can deal. Weights make small pieces more common.</summary>
        public static readonly BlockShape[] All =
        {
            new BlockShape(6, 0, 0),                                              // single
            new BlockShape(7, 0, 0, 1, 0),                                        // 2 across
            new BlockShape(7, 0, 0, 0, 1),                                        // 2 down
            new BlockShape(7, 0, 0, 1, 0, 2, 0),                                  // 3 across
            new BlockShape(7, 0, 0, 0, 1, 0, 2),                                  // 3 down
            new BlockShape(4, 0, 0, 1, 0, 2, 0, 3, 0),                            // 4 across
            new BlockShape(4, 0, 0, 0, 1, 0, 2, 0, 3),                            // 4 down
            new BlockShape(2, 0, 0, 1, 0, 2, 0, 3, 0, 4, 0),                      // 5 across
            new BlockShape(2, 0, 0, 0, 1, 0, 2, 0, 3, 0, 4),                      // 5 down
            new BlockShape(7, 0, 0, 1, 0, 0, 1, 1, 1),                            // 2x2 square
            new BlockShape(3, 0, 0, 1, 0, 2, 0, 0, 1, 1, 1, 2, 1),                // 3x2
            new BlockShape(3, 0, 0, 1, 0, 0, 1, 1, 1, 0, 2, 1, 2),                // 2x3
            new BlockShape(1, 0, 0, 1, 0, 2, 0, 0, 1, 1, 1, 2, 1, 0, 2, 1, 2, 2, 2), // 3x3 square
            new BlockShape(5, 0, 0, 0, 1, 1, 1),                                  // small corner
            new BlockShape(5, 0, 0, 1, 0, 0, 1),
            new BlockShape(5, 0, 0, 1, 0, 1, 1),
            new BlockShape(5, 1, 0, 0, 1, 1, 1),
            new BlockShape(3, 0, 0, 0, 1, 0, 2, 1, 2, 2, 2),                      // big corner
            new BlockShape(3, 0, 0, 1, 0, 2, 0, 0, 1, 0, 2),
            new BlockShape(3, 0, 0, 1, 0, 2, 0, 2, 1, 2, 2),
            new BlockShape(3, 2, 0, 2, 1, 0, 2, 1, 2, 2, 2),
            new BlockShape(3, 0, 0, 1, 0, 2, 0, 1, 1),                            // T pieces
            new BlockShape(3, 1, 0, 0, 1, 1, 1, 2, 1),
            new BlockShape(3, 0, 0, 0, 1, 1, 1, 0, 2),
            new BlockShape(3, 1, 0, 0, 1, 1, 1, 1, 2),
            new BlockShape(2, 1, 0, 2, 0, 0, 1, 1, 1),                            // S and Z
            new BlockShape(2, 0, 0, 1, 0, 1, 1, 2, 1),
            new BlockShape(2, 0, 0, 0, 1, 1, 1, 1, 2),
            new BlockShape(2, 1, 0, 0, 1, 1, 1, 0, 2)
        };

        static readonly int TotalWeight = ComputeTotalWeight();

        static int ComputeTotalWeight()
        {
            int total = 0;
            foreach (var shape in All) total += shape.Weight;
            return total;
        }

        public static int RandomIndex(System.Random rng)
        {
            int roll = rng.Next(TotalWeight);
            for (int i = 0; i < All.Length; i++)
            {
                roll -= All[i].Weight;
                if (roll < 0) return i;
            }
            return 0;
        }
    }

    public struct BlockPlacement
    {
        public int Rows;
        public int Columns;
        public int CellsCleared;
        public int Points;
        public List<int> ClearedRows;
        public List<int> ClearedColumns;

        public int Lines => Rows + Columns;
    }

    /// <summary>
    /// 8x8 block puzzle board: drop pieces anywhere they fit, and full rows or columns clear.
    /// Clearing several lines at once scores much more.
    /// </summary>
    public sealed class BlockBoard
    {
        public const int Size = 8;

        /// <summary>0 is empty, otherwise the piece colour (1-based).</summary>
        public readonly int[] Cells = new int[Size * Size];
        public int Score { get; private set; }
        public int Lines { get; private set; }

        public int this[int x, int y] => Cells[y * Size + x];

        public void Load(int[] cells, int score, int lines)
        {
            for (int i = 0; i < Cells.Length; i++) Cells[i] = i < cells.Length ? cells[i] : 0;
            Score = score;
            Lines = lines;
        }

        public bool CanPlace(BlockShape shape, int x, int y)
        {
            if (x < 0 || y < 0 || x + shape.Width > Size || y + shape.Height > Size) return false;
            foreach (var cell in shape.Cells)
                if (Cells[(y + cell.y) * Size + x + cell.x] != 0) return false;
            return true;
        }

        public bool HasAnyPlacement(BlockShape shape)
        {
            for (int y = 0; y <= Size - shape.Height; y++)
                for (int x = 0; x <= Size - shape.Width; x++)
                    if (CanPlace(shape, x, y)) return true;
            return false;
        }

        /// <summary>Places a piece and clears any full lines. Check <see cref="CanPlace"/> first.</summary>
        public BlockPlacement Place(BlockShape shape, int x, int y, int colour)
        {
            foreach (var cell in shape.Cells) Cells[(y + cell.y) * Size + x + cell.x] = colour;
            Score += shape.Count;

            var result = new BlockPlacement { ClearedRows = new List<int>(), ClearedColumns = new List<int>() };
            for (int row = 0; row < Size; row++)
            {
                bool full = true;
                for (int column = 0; column < Size && full; column++)
                    if (Cells[row * Size + column] == 0) full = false;
                if (full) result.ClearedRows.Add(row);
            }
            for (int column = 0; column < Size; column++)
            {
                bool full = true;
                for (int row = 0; row < Size && full; row++)
                    if (Cells[row * Size + column] == 0) full = false;
                if (full) result.ClearedColumns.Add(column);
            }

            foreach (int row in result.ClearedRows)
                for (int column = 0; column < Size; column++) Cells[row * Size + column] = 0;
            foreach (int column in result.ClearedColumns)
                for (int row = 0; row < Size; row++) Cells[row * Size + column] = 0;

            result.Rows = result.ClearedRows.Count;
            result.Columns = result.ClearedColumns.Count;
            result.CellsCleared = result.Rows * Size + result.Columns * Size - result.Rows * result.Columns;
            int lines = result.Lines;
            result.Points = shape.Count + lines * lines * 100;
            Score += lines * lines * 100;
            Lines += lines;
            return result;
        }

        public int FilledCells
        {
            get
            {
                int count = 0;
                foreach (int cell in Cells)
                    if (cell != 0) count++;
                return count;
            }
        }
    }
}
