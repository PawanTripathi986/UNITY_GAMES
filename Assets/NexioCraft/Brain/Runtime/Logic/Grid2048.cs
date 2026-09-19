using System;
using System.Collections.Generic;

namespace NexioCraft.Brain
{
    public enum SlideDirection { Left, Right, Up, Down }

    /// <summary>One tile on the 2048 board. Ids let the view animate the same tile across moves.</summary>
    public struct Tile2048
    {
        public int Id;
        public int Value;
        public int Cell;
        /// <summary>Cell this tile came from during the last move (-1 if it was just spawned).</summary>
        public int FromCell;
        /// <summary>Set when this tile is the result of a merge, so the view can pop it.</summary>
        public bool Merged;
        /// <summary>Tile that slid into this one and disappeared (-1 when none).</summary>
        public int MergedFromCell;
    }

    /// <summary>
    /// 2048 rules on a 4x4 grid: slide, merge equal neighbours once per move, then spawn a tile.
    /// Pure logic so it can be unit tested; the view only reads <see cref="Tiles"/>.
    /// </summary>
    public sealed class Grid2048
    {
        public const int Size = 4;
        public const int WinValue = 2048;

        public List<Tile2048> Tiles { get; private set; } = new List<Tile2048>();
        public int Score { get; private set; }
        public bool Won { get; private set; }
        public bool Moved { get; private set; }

        readonly Random rng;
        int nextId = 1;
        List<Tile2048> undoTiles;
        int undoScore;
        bool undoWon;

        public Grid2048(Random random, bool spawnStart = true)
        {
            rng = random;
            if (!spawnStart) return;
            Spawn();
            Spawn();
        }

        public bool CanUndo => undoTiles != null;

        public int[] ToArray()
        {
            var cells = new int[Size * Size];
            foreach (var tile in Tiles) cells[tile.Cell] = tile.Value;
            return cells;
        }

        /// <summary>Restores the board from a flat array (for saves and tests).</summary>
        public void Load(int[] cells, int score, bool won)
        {
            Tiles.Clear();
            for (int i = 0; i < cells.Length && i < Size * Size; i++)
                if (cells[i] > 0) Tiles.Add(new Tile2048 { Id = nextId++, Value = cells[i], Cell = i, FromCell = i, MergedFromCell = -1 });
            Score = score;
            Won = won;
            undoTiles = null;
        }

        public bool Move(SlideDirection direction)
        {
            var before = new List<Tile2048>(Tiles);
            int beforeScore = Score;
            bool beforeWon = Won;

            var board = new Tile2048?[Size * Size];
            foreach (var tile in Tiles) board[tile.Cell] = tile;

            var result = new List<Tile2048>();
            Moved = false;
            for (int line = 0; line < Size; line++)
            {
                var lane = new List<Tile2048>(Size);
                for (int i = 0; i < Size; i++)
                {
                    int cell = CellOf(direction, line, i);
                    if (board[cell].HasValue) lane.Add(board[cell].Value);
                }

                int target = 0;
                for (int i = 0; i < lane.Count; i++)
                {
                    var tile = lane[i];
                    tile.FromCell = tile.Cell;
                    tile.Merged = false;
                    tile.MergedFromCell = -1;
                    if (i + 1 < lane.Count && lane[i + 1].Value == tile.Value)
                    {
                        var partner = lane[i + 1];
                        tile.Value *= 2;
                        tile.Merged = true;
                        tile.MergedFromCell = partner.Cell;
                        Score += tile.Value;
                        if (tile.Value >= WinValue) Won = true;
                        i++;
                    }
                    tile.Cell = CellOf(direction, line, target++);
                    if (tile.Cell != tile.FromCell || tile.Merged) Moved = true;
                    result.Add(tile);
                }
            }

            if (!Moved)
            {
                Score = beforeScore;
                Won = beforeWon;
                return false;
            }

            undoTiles = before;
            undoScore = beforeScore;
            undoWon = beforeWon;
            Tiles = result;
            Spawn();
            return true;
        }

        public bool Undo()
        {
            if (undoTiles == null) return false;
            Tiles = undoTiles;
            Score = undoScore;
            Won = undoWon;
            undoTiles = null;
            return true;
        }

        /// <summary>True when no move in any direction would change the board.</summary>
        public bool IsStuck()
        {
            var cells = ToArray();
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] == 0) return false;
                int x = i % Size, y = i / Size;
                if (x + 1 < Size && cells[i + 1] == cells[i]) return false;
                if (y + 1 < Size && cells[i + Size] == cells[i]) return false;
            }
            return true;
        }

        public Tile2048? Spawn()
        {
            var empty = new List<int>(Size * Size);
            var cells = ToArray();
            for (int i = 0; i < cells.Length; i++)
                if (cells[i] == 0) empty.Add(i);
            if (empty.Count == 0) return null;
            var tile = new Tile2048
            {
                Id = nextId++,
                Value = rng.NextDouble() < 0.9 ? 2 : 4,
                Cell = empty[rng.Next(empty.Count)],
                FromCell = -1,
                MergedFromCell = -1
            };
            tile.FromCell = -1;
            Tiles.Add(tile);
            return tile;
        }

        /// <summary>
        /// Cell index of position <paramref name="index"/> along a lane, counted from the edge the tiles slide
        /// towards. Cells are row-major with row 0 at the top.
        /// </summary>
        static int CellOf(SlideDirection direction, int line, int index)
        {
            switch (direction)
            {
                case SlideDirection.Left: return line * Size + index;
                case SlideDirection.Right: return line * Size + (Size - 1 - index);
                case SlideDirection.Up: return index * Size + line;
                default: return (Size - 1 - index) * Size + line;
            }
        }
    }
}
