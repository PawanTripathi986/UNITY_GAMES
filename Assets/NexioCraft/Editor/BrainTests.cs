using System;
using System.Collections.Generic;
using System.Text;
using NexioCraft.Brain;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace NexioCraft.EditorTools
{
    /// <summary>
    /// Logic checks for the brain games: 2048 slide/merge rules, sliding-puzzle solvability, maths questions
    /// and the memory deck.
    ///   Unity -batchmode -projectPath . -executeMethod NexioCraft.EditorTools.BrainTests.CommandLine
    /// </summary>
    public static class BrainTests
    {
        [MenuItem("NexioCraft/Brain/Run Logic Tests")]
        public static void RunFromMenu()
        {
            bool ok = Run(out string report);
            Debug.Log(report);
            EditorUtility.DisplayDialog(ok ? "Brain tests passed" : "Brain tests FAILED", report, "OK");
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
            var rng = new Random(16092026);

            // 2048 slides and merges.
            CheckMove(failures, "left", SlideDirection.Left,
                new[] { 2, 2, 0, 0, 4, 4, 4, 4, 0, 0, 0, 2, 2, 0, 2, 0 },
                new[] { 4, 0, 0, 0, 8, 8, 0, 0, 2, 0, 0, 0, 4, 0, 0, 0 }, 24);
            CheckMove(failures, "right", SlideDirection.Right,
                new[] { 2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] { 0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 4);
            CheckMove(failures, "up", SlideDirection.Up,
                new[] { 2, 0, 0, 0, 2, 0, 0, 0, 4, 0, 0, 0, 4, 0, 0, 0 },
                new[] { 4, 0, 0, 0, 8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 12);
            CheckMove(failures, "down", SlideDirection.Down,
                new[] { 2, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4, 0, 0, 0 }, 4);
            // A row of three only merges the pair nearest the wall.
            CheckMove(failures, "single merge", SlideDirection.Left,
                new[] { 4, 4, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] { 8, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 8);
            CheckMove(failures, "no double merge", SlideDirection.Left,
                new[] { 4, 4, 8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                new[] { 8, 8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 8);

            var blocked = new Grid2048(rng, false);
            blocked.Load(new[] { 2, 4, 2, 4, 4, 2, 4, 2, 2, 4, 2, 4, 4, 2, 4, 2 }, 0, false);
            if (!blocked.IsStuck()) failures.Add("2048: a checkerboard board should be stuck");
            if (blocked.Move(SlideDirection.Left)) failures.Add("2048: a stuck board reported a move");

            var undoGrid = new Grid2048(rng, false);
            undoGrid.Load(new[] { 2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 0, false);
            undoGrid.Move(SlideDirection.Left);
            if (!undoGrid.Undo()) failures.Add("2048: undo after a move failed");
            var restored = undoGrid.ToArray();
            if (restored[0] != 2 || restored[1] != 2 || undoGrid.Score != 0) failures.Add("2048: undo did not restore the board");

            // Random games: values stay powers of two and games end.
            int finished2048 = 0;
            for (int game = 0; game < 200; game++)
            {
                var grid = new Grid2048(rng);
                for (int move = 0; move < 4000 && !grid.IsStuck(); move++)
                {
                    grid.Move((SlideDirection)rng.Next(4));
                    foreach (var tile in grid.Tiles)
                    {
                        if (tile.Value < 2 || (tile.Value & (tile.Value - 1)) != 0) failures.Add($"2048: odd tile value {tile.Value}");
                        if (tile.Cell < 0 || tile.Cell >= 16) failures.Add($"2048: tile outside the board ({tile.Cell})");
                    }
                    if (grid.Tiles.Count > 16) failures.Add("2048: more tiles than cells");
                }
                if (grid.IsStuck()) finished2048++;
            }
            log.AppendLine($"2048: 200 random games, {finished2048} ended with a full board");

            // Sliding puzzle: every shuffle must be solvable.
            for (int size = 3; size <= 5; size++)
            {
                for (int i = 0; i < 300; i++)
                {
                    var puzzle = new SlidingPuzzle(size);
                    puzzle.Shuffle(rng);
                    if (puzzle.IsSolved) failures.Add($"slide {size}x{size}: shuffle left the puzzle solved");
                    if (puzzle.Moves != 0) failures.Add("slide: shuffle should not count as moves");
                    if (!IsSolvable(puzzle)) failures.Add($"slide {size}x{size}: shuffled into an unsolvable position");
                }
            }
            var slider = new SlidingPuzzle(3);
            if (!slider.IsSolved) failures.Add("slide: a fresh puzzle should be solved");
            if (slider.Slide(0)) failures.Add("slide: moved a tile that is not beside the blank");
            if (!slider.Slide(5) || slider.Moves != 1) failures.Add("slide: legal move rejected");
            log.AppendLine("slide: 900 shuffles across 3x3, 4x4 and 5x5 all solvable");

            // Maths questions.
            for (int level = 0; level < 30; level++)
            {
                for (int i = 0; i < 200; i++)
                {
                    var question = MathQuestion.Create(rng, level);
                    int expected = Evaluate(question.Text, failures);
                    if (expected != question.Answer) failures.Add($"maths: '{question.Text}' answer {question.Answer}, expected {expected}");
                    if (question.Options[question.CorrectIndex] != question.Answer) failures.Add("maths: correct index does not hold the answer");
                    var seen = new HashSet<int>();
                    foreach (int option in question.Options)
                    {
                        if (!seen.Add(option)) failures.Add($"maths: duplicate option {option} in '{question.Text}'");
                        if (option < 0) failures.Add($"maths: negative option {option}");
                    }
                }
            }
            log.AppendLine("maths: 6,000 questions verified (answer correct, 4 distinct options)");

            // Memory deck.
            for (int pairs = 6; pairs <= 10; pairs += 2)
            {
                var deck = MemoryDeck.Create(pairs, rng);
                if (deck.Length != pairs * 2) failures.Add($"memory: deck of {pairs} pairs had {deck.Length} cards");
                var counts = new Dictionary<int, int>();
                foreach (int symbol in deck) counts[symbol] = counts.TryGetValue(symbol, out int c) ? c + 1 : 1;
                if (counts.Count != pairs) failures.Add($"memory: {counts.Count} symbols for {pairs} pairs");
                foreach (var pair in counts)
                    if (pair.Value != 2) failures.Add($"memory: symbol {pair.Key} appears {pair.Value} times");
            }
            log.AppendLine("memory: decks contain exactly two of every symbol");

            var sb = new StringBuilder();
            sb.AppendLine(failures.Count == 0 ? "BRAIN TESTS PASSED" : $"BRAIN TESTS FAILED ({failures.Count})");
            sb.Append(log);
            foreach (var failure in failures) sb.AppendLine(" - " + failure);
            report = sb.ToString();
            return failures.Count == 0;
        }

        static void CheckMove(List<string> failures, string name, SlideDirection direction, int[] before, int[] expected, int expectedScore)
        {
            var grid = new Grid2048(new Random(1), false);
            grid.Load(before, 0, false);
            if (!grid.Move(direction))
            {
                failures.Add($"2048 {name}: move was rejected");
                return;
            }
            // Ignore the tile spawned after the move.
            var cells = new int[16];
            foreach (var tile in grid.Tiles)
                if (tile.FromCell >= 0) cells[tile.Cell] = tile.Value;
            for (int i = 0; i < 16; i++)
                if (cells[i] != expected[i])
                {
                    failures.Add($"2048 {name}: cell {i} is {cells[i]}, expected {expected[i]}");
                    return;
                }
            if (grid.Score != expectedScore) failures.Add($"2048 {name}: score {grid.Score}, expected {expectedScore}");
        }

        /// <summary>Standard inversion test for sliding puzzles.</summary>
        static bool IsSolvable(SlidingPuzzle puzzle)
        {
            var tiles = new List<int>();
            foreach (int value in puzzle.Cells)
                if (value != 0) tiles.Add(value);
            int inversions = 0;
            for (int i = 0; i < tiles.Count; i++)
                for (int j = i + 1; j < tiles.Count; j++)
                    if (tiles[i] > tiles[j]) inversions++;
            if (puzzle.Size % 2 == 1) return inversions % 2 == 0;
            int blankRowFromBottom = puzzle.Size - puzzle.Blank / puzzle.Size;
            return (inversions + blankRowFromBottom) % 2 == 1;
        }

        static int Evaluate(string text, List<string> failures)
        {
            var parts = text.Split(' ');
            if (parts.Length != 3 || !int.TryParse(parts[0], out int a) || !int.TryParse(parts[2], out int b))
            {
                failures.Add("maths: could not parse '" + text + "'");
                return int.MinValue;
            }
            switch (parts[1])
            {
                case "+": return a + b;
                case "-": return a - b;
                case "x": return a * b;
                case "/": return b == 0 ? int.MinValue : a / b;
                default:
                    failures.Add("maths: unknown operator in '" + text + "'");
                    return int.MinValue;
            }
        }
    }
}
