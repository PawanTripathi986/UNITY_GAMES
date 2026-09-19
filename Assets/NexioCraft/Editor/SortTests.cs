using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using NexioCraft.Sort;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace NexioCraft.EditorTools
{
    /// <summary>
    /// Colour Sort: pouring rules, undo, level generation and the solver. Every generated level is solved by the
    /// solver and the solution replayed, so no player can be handed a level that cannot be finished.
    ///   Unity -batchmode -projectPath . -executeMethod NexioCraft.EditorTools.SortTests.CommandLine
    /// </summary>
    public static class SortTests
    {
        [MenuItem("NexioCraft/Sort/Run Logic Tests")]
        public static void RunFromMenu()
        {
            bool ok = Run(out string report);
            Debug.Log(report);
            EditorUtility.DisplayDialog(ok ? "Sort tests passed" : "Sort tests FAILED", report, "OK");
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
            void Check(bool condition, string message)
            {
                if (!condition) failures.Add(message);
            }

            Rules(Check);
            Undo(Check, log);
            Levels(Check, log);
            Saves(Check);

            var summary = new StringBuilder();
            summary.AppendLine(failures.Count == 0 ? "SORT TESTS PASSED" : $"SORT TESTS FAILED ({failures.Count})");
            summary.Append(log);
            foreach (var failure in failures) summary.AppendLine("  FAIL: " + failure);
            report = summary.ToString();
            return failures.Count == 0;
        }

        static SortBoard Board(params int[][] tubes)
        {
            var board = new SortBoard(tubes.Length);
            for (int t = 0; t < tubes.Length; t++)
                for (int s = 0; s < tubes[t].Length; s++)
                    board[t, s] = tubes[t][s];
            return board;
        }

        static void Rules(Action<bool, string> check)
        {
            var board = Board(new[] { 1, 1, 2, 0 }, new[] { 2, 2, 0, 0 }, new int[0]);
            check(board.Height(0) == 3 && board.Height(2) == 0, "height");
            check(board.Top(0) == 2 && board.TopRun(0) == 1, "top of a mixed tube");
            check(board.TopRun(1) == 2 && board.IsUniform(1), "run of a uniform tube");
            check(!board.IsUniform(0), "mixed tube is not uniform");

            check(board.CanPour(0, 1), "colour on top matches");
            check(board.CanPour(1, 2), "anything may go into an empty tube");
            check(!board.CanPour(2, 0), "an empty tube cannot pour");
            check(!board.CanPour(0, 0), "a tube cannot pour into itself");

            // Pouring moves the whole run of equal colours, up to the space available.
            var one = board.Clone();
            check(one.Pour(0, 1) == 1 && one.Height(0) == 2 && one.Height(1) == 3, "single item poured");
            var two = Board(new[] { 3, 1, 1, 0 }, new[] { 1, 0, 0, 0 }, new int[0]);
            check(two.Pour(0, 1) == 2 && two.Top(0) == 3 && two.Height(1) == 3, "the whole run moves");
            var tight = Board(new[] { 1, 1, 1, 0 }, new[] { 2, 1, 0, 0 }, new int[0]);
            check(tight.Pour(0, 1) == 2 && tight.Height(1) == 4 && tight.Height(0) == 1, "only what fits moves");
            check(tight.Pour(0, 1) == 0, "a full tube takes nothing");

            check(!board.IsUseful(1, 2), "moving a whole uniform tube into an empty one achieves nothing");
            check(board.IsUseful(0, 1), "a real pour is useful");

            var solved = Board(new[] { 1, 1, 1, 1 }, new[] { 2, 2, 2, 2 }, new int[0]);
            check(solved.Solved, "finished board");
            check(!Board(new[] { 1, 1, 1, 0 }, new[] { 1, 2, 2, 2 }, new int[0]).Solved, "part-filled tubes are not finished");

            var stuck = Board(new[] { 1, 2, 1, 2 }, new[] { 2, 1, 2, 1 }, new[] { 3, 3, 3, 3 });
            check(!stuck.HasUsefulMove(), "no useful move on a blocked board");
            check(!SortSolver.TrySolve(stuck, out _, 5000), "a blocked board has no solution");
        }

        static void Undo(Action<bool, string> check, StringBuilder log)
        {
            var random = new System.Random(20260918);
            int replays = 0;
            for (int round = 0; round < 40; round++)
            {
                var level = SortLevels.For(random.Next(0, 25));
                var board = SortLevels.Create(level);
                string start = board.Key();
                int poured = 0;
                for (int move = 0; move < 30 && !board.Solved; move++)
                {
                    var options = new List<(int from, int to)>();
                    for (int from = 0; from < board.TubeCount; from++)
                        for (int to = 0; to < board.TubeCount; to++)
                            if (board.IsUseful(from, to)) options.Add((from, to));
                    if (options.Count == 0) break;
                    var pick = options[random.Next(options.Count)];
                    int count = board.Pour(pick.from, pick.to);
                    check(count > 0, "a useful move must move something");
                    poured++;
                }
                check(board.Moves == poured, "history counts every pour");
                while (board.Undo()) { }
                check(board.Key() == start, $"undoing everything must restore the board (level {level.Number})");
                check(board.Moves == 0, "history is empty after undoing everything");
                replays += poured;
            }
            log.AppendLine($"  undo: {replays} random pours undone exactly");
        }

        static void Levels(Action<bool, string> check, StringBuilder log)
        {
            var indices = new List<int>();
            for (int i = 0; i < 40; i++) indices.Add(i);
            indices.AddRange(new[] { 45, 60, 75, 99, 150 });

            int previousColours = 0;
            var timer = Stopwatch.StartNew();
            int solvedCount = 0, longest = 0;
            foreach (int index in indices)
            {
                var level = SortLevels.For(index);
                check(level.Colours >= SortLevels.MinColours && level.Colours <= SortLevels.MaxColours, $"level {level.Number} colour count");
                check(level.Colours >= previousColours, "colours must never go down as levels rise");
                previousColours = level.Colours;
                check(level.TubeCount == level.Colours + SortLevels.EmptyTubes, $"level {level.Number} tube count");

                var board = SortLevels.Create(level);
                check(board.TubeCount == level.TubeCount, $"level {level.Number} board size");
                check(!board.Solved, $"level {level.Number} starts already finished");

                var counts = new Dictionary<int, int>();
                for (int t = 0; t < board.TubeCount; t++)
                    for (int s = 0; s < SortBoard.Capacity; s++)
                    {
                        int colour = board[t, s];
                        if (colour == 0) continue;
                        counts.TryGetValue(colour, out int n);
                        counts[colour] = n + 1;
                    }
                check(counts.Count == level.Colours, $"level {level.Number} has {counts.Count} colours, expected {level.Colours}");
                foreach (var pair in counts)
                    check(pair.Value == SortBoard.Capacity, $"level {level.Number} colour {pair.Key} appears {pair.Value} times");

                // Every level must be finishable, and the solution must really finish it.
                bool solvable = SortSolver.TrySolve(board, out var solution, 200000);
                check(solvable, $"level {level.Number} cannot be solved");
                if (!solvable) continue;
                solvedCount++;
                longest = Math.Max(longest, solution.Count);
                var replay = board.Clone();
                foreach (var move in solution)
                    check(replay.Pour(move.From, move.To) > 0, $"level {level.Number}: solution move {move} is not legal");
                check(replay.Solved, $"level {level.Number}: replaying the solution does not finish it");
            }
            timer.Stop();
            log.AppendLine($"  levels: {solvedCount}/{indices.Count} generated and solved in {timer.ElapsedMilliseconds} ms, longest solution {longest} moves");

            // The same level is the same puzzle for everyone.
            var first = SortLevels.Create(SortLevels.For(7));
            var second = SortLevels.Create(SortLevels.For(7));
            check(first.Key() == second.Key(), "levels must be deterministic");
        }

        static void Saves(Action<bool, string> check)
        {
            var board = SortLevels.Create(SortLevels.For(5));
            board.Pour(0, board.TubeCount - 1);
            var restored = SortBoard.FromArray(board.ToArray());
            check(restored != null && restored.Key() == board.Key(), "board survives a save and load");
            check(restored != null && restored.Moves == 0, "a restored board starts with an empty history");
            check(SortBoard.FromArray(null) == null && SortBoard.FromArray(new int[3]) == null, "bad save data is rejected");

            var extra = board.Clone();
            int before = extra.TubeCount;
            extra.AddTube();
            check(extra.TubeCount == before + 1 && extra.IsEmpty(before), "an extra tube is added empty");
            check(extra.ToArray().Length == (before + 1) * SortBoard.Capacity, "the extra tube is saved too");
        }
    }
}
