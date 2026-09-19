using System;
using System.Collections.Generic;
using System.Text;
using NexioCraft.Blocks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace NexioCraft.EditorTools
{
    /// <summary>
    /// Block puzzle rules: piece shapes, placement, line clearing, scoring and game over.
    ///   Unity -batchmode -projectPath . -executeMethod NexioCraft.EditorTools.BlockTests.CommandLine
    /// </summary>
    public static class BlockTests
    {
        [MenuItem("NexioCraft/Blocks/Run Logic Tests")]
        public static void RunFromMenu()
        {
            bool ok = Run(out string report);
            Debug.Log(report);
            EditorUtility.DisplayDialog(ok ? "Block tests passed" : "Block tests FAILED", report, "OK");
        }

        public static void CommandLine()
        {
            bool ok;
            try
            {
                ok = Run(out string report);
                Debug.Log(report);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                ok = false;
            }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool Run(out string report)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            var rng = new System.Random(160920261);

            // Shapes are well formed: unique cells, normalised to the top-left corner.
            foreach (var shape in BlockShapes.All)
            {
                var seen = new HashSet<Vector2Int>();
                int minX = int.MaxValue, minY = int.MaxValue;
                foreach (var cell in shape.Cells)
                {
                    if (!seen.Add(cell)) failures.Add($"shape with {shape.Count} cells repeats {cell}");
                    if (cell.x < 0 || cell.y < 0) failures.Add($"shape has negative cell {cell}");
                    minX = Math.Min(minX, cell.x);
                    minY = Math.Min(minY, cell.y);
                }
                if (minX != 0 || minY != 0) failures.Add($"shape not aligned to the corner (min {minX},{minY})");
                if (shape.Width > BlockBoard.Size || shape.Height > BlockBoard.Size) failures.Add("shape larger than the board");
                if (shape.Weight <= 0) failures.Add("shape has no weight");
            }
            log.AppendLine($"shapes: {BlockShapes.All.Length} definitions checked");

            var single = Find(1, 1, 1);
            var row4 = Find(4, 4, 1);
            var square = Find(4, 2, 2);

            // Placement rules.
            var board = new BlockBoard();
            if (!board.CanPlace(row4, 4, 0)) failures.Add("4-wide piece should fit at x=4");
            if (board.CanPlace(row4, 5, 0)) failures.Add("4-wide piece should not fit at x=5");
            if (board.CanPlace(single, -1, 0) || board.CanPlace(single, 0, 8)) failures.Add("placement outside the board was allowed");
            board.Place(single, 0, 0, 1);
            if (board.CanPlace(single, 0, 0)) failures.Add("piece placed on an occupied cell");
            if (board[0, 0] != 1) failures.Add("placed cell not filled");
            if (board.Score != 1) failures.Add($"score after one block is {board.Score}, expected 1");

            // Filling a row clears it and scores 100.
            var rowBoard = new BlockBoard();
            var cells = new int[64];
            for (int x = 0; x < 7; x++) cells[3 * 8 + x] = 2;
            rowBoard.Load(cells, 0, 0);
            var placement = rowBoard.Place(single, 7, 3, 3);
            if (placement.Rows != 1 || placement.Columns != 0) failures.Add($"row clear reported {placement.Rows} rows, {placement.Columns} columns");
            if (placement.Points != 1 + 100) failures.Add($"row clear scored {placement.Points}, expected 101");
            for (int x = 0; x < 8; x++)
                if (rowBoard[x, 3] != 0) failures.Add("cleared row still has blocks");
            if (rowBoard.Lines != 1) failures.Add("line counter did not move");

            // A row and a column clearing together score as two lines.
            var crossBoard = new BlockBoard();
            cells = new int[64];
            for (int x = 0; x < 8; x++) cells[5 * 8 + x] = 4;
            for (int y = 0; y < 8; y++) cells[y * 8 + 2] = 4;
            cells[5 * 8 + 2] = 0;
            crossBoard.Load(cells, 0, 0);
            placement = crossBoard.Place(single, 2, 5, 5);
            if (placement.Rows != 1 || placement.Columns != 1) failures.Add("cross clear did not report a row and a column");
            if (placement.Points != 1 + 400) failures.Add($"double clear scored {placement.Points}, expected 401");
            if (crossBoard.FilledCells != 0) failures.Add("cross clear left blocks behind");

            // A full board has no placements.
            var fullBoard = new BlockBoard();
            var full = new int[64];
            for (int i = 0; i < full.Length; i++) full[i] = 1;
            fullBoard.Load(full, 0, 0);
            foreach (var shape in BlockShapes.All)
                if (fullBoard.HasAnyPlacement(shape)) failures.Add("full board reported a placement");
            var emptyBoard = new BlockBoard();
            foreach (var shape in BlockShapes.All)
                if (!emptyBoard.HasAnyPlacement(shape)) failures.Add($"empty board rejected a {shape.Width}x{shape.Height} piece");

            // Random games: the board stays consistent and every game ends.
            int games = 300, ended = 0;
            long totalScore = 0, totalLines = 0;
            for (int game = 0; game < games; game++)
            {
                var play = new BlockBoard();
                var tray = new int[3];
                var used = new bool[3];
                for (int i = 0; i < 3; i++) tray[i] = BlockShapes.RandomIndex(rng);
                for (int move = 0; move < 2000; move++)
                {
                    var options = new List<(int slot, int x, int y)>();
                    for (int slot = 0; slot < 3; slot++)
                    {
                        if (used[slot]) continue;
                        var shape = BlockShapes.All[tray[slot]];
                        for (int y = 0; y <= BlockBoard.Size - shape.Height; y++)
                            for (int x = 0; x <= BlockBoard.Size - shape.Width; x++)
                                if (play.CanPlace(shape, x, y)) options.Add((slot, x, y));
                    }
                    if (options.Count == 0)
                    {
                        ended++;
                        break;
                    }
                    var choice = options[rng.Next(options.Count)];
                    int before = play.Score;
                    play.Place(BlockShapes.All[tray[choice.slot]], choice.x, choice.y, rng.Next(6) + 1);
                    used[choice.slot] = true;
                    if (play.Score < before) failures.Add("score went down");
                    if (play.FilledCells > 64) failures.Add("more blocks than cells");
                    foreach (int cell in play.Cells)
                        if (cell < 0 || cell > 8) failures.Add($"strange cell value {cell}");
                    if (used[0] && used[1] && used[2])
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            tray[i] = BlockShapes.RandomIndex(rng);
                            used[i] = false;
                        }
                    }
                }
                totalScore += play.Score;
                totalLines += play.Lines;
            }
            if (ended != games) failures.Add($"{games - ended} of {games} random games did not end");
            log.AppendLine($"random games: {games} played, average score {totalScore / (float)games:F0}, average lines {totalLines / (float)games:F1}");

            var sb = new StringBuilder();
            sb.AppendLine(failures.Count == 0 ? "BLOCK TESTS PASSED" : $"BLOCK TESTS FAILED ({failures.Count})");
            sb.Append(log);
            foreach (var failure in failures) sb.AppendLine(" - " + failure);
            report = sb.ToString();
            return failures.Count == 0;
        }

        static BlockShape Find(int count, int width, int height)
        {
            foreach (var shape in BlockShapes.All)
                if (shape.Count == count && shape.Width == width && shape.Height == height) return shape;
            throw new Exception($"no shape with {count} cells in {width}x{height}");
        }
    }
}
