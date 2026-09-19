using System;
using System.Collections.Generic;
using System.Text;
using NexioCraft.Ludo;
using UnityEditor;
using UnityEngine;

namespace NexioCraft.EditorTools
{
    /// <summary>
    /// Plays thousands of computer-vs-computer games with random house rules and checks the rules engine's
    /// invariants after every move, plus the board geometry and a Hard-vs-Easy strength check.
    ///   Unity -batchmode -projectPath . -executeMethod NexioCraft.EditorTools.LudoSimulation.CommandLine
    /// </summary>
    public static class LudoSimulation
    {
        [MenuItem("NexioCraft/Ludo/Run Rules Simulation")]
        public static void RunFromMenu()
        {
            bool ok = Run(500, out string report);
            Debug.Log(report);
            EditorUtility.DisplayDialog(ok ? "Ludo simulation passed" : "Ludo simulation FAILED", report, "OK");
        }

        public static void CommandLine()
        {
            bool ok;
            try
            {
                ok = Run(4000, out string report);
                Debug.Log(report);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                ok = false;
            }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool Run(int games, out string report)
        {
            var failures = new List<string>();
            CheckGeometry(failures);

            var rng = new System.Random(20260915);
            var moves = new List<LudoMove>();
            long rollsTotal = 0, movesTotal = 0, capturesTotal = 0, forfeits = 0, noMoveRolls = 0;
            int longestGame = 0;

            for (int game = 0; game < games && failures.Count < 25; game++)
            {
                var rules = RandomRules(rng, game);
                int players = 2 + rng.Next(3);
                var seats = LudoSetupScreen.SeatsFor(players, rng.Next(4));
                var kinds = new SeatKind[4];
                var levels = new BotLevel[4];
                foreach (int seat in seats)
                {
                    // "Human" seats are played by the AI too; they exercise the end-when-humans-finish rule.
                    kinds[seat] = rng.Next(4) == 0 ? SeatKind.Human : SeatKind.Bot;
                    levels[seat] = (BotLevel)rng.Next(3);
                }
                var state = LudoEngine.NewGame(kinds, levels, rules, LudoMode.PassAndPlay, seats[0]);

                int rolls = 0;
                while (!state.over)
                {
                    if (++rolls > 6000)
                    {
                        failures.Add($"game {game}: did not finish after 6000 rolls");
                        break;
                    }
                    int seatToPlay = state.current;
                    if (!state.IsActive(seatToPlay) || state.HasFinished(seatToPlay))
                    {
                        failures.Add($"game {game}: turn given to seat {seatToPlay} which is inactive or finished");
                        break;
                    }

                    int roll = rng.Next(1, 7);
                    var beforeRoll = state.Clone();
                    var outcome = LudoEngine.Roll(state, roll, moves);

                    if (outcome == RollOutcome.ThreeSixes)
                    {
                        forfeits++;
                        if (!rules.threeSixesForfeit || roll != 6 || beforeRoll.consecutiveSixes != 2)
                            failures.Add($"game {game}: bad three-sixes forfeit (roll {roll}, previous sixes {beforeRoll.consecutiveSixes})");
                        if (state.current == seatToPlay && CountPlaying(state) > 1)
                            failures.Add($"game {game}: turn did not pass after three sixes");
                        continue;
                    }

                    if (outcome == RollOutcome.NoMoves)
                    {
                        noMoveRolls++;
                        for (int t = 0; t < 4; t++)
                        {
                            int p = state.Token(seatToPlay, t);
                            bool couldLeaveYard = p == LudoEngine.Yard && (roll == 6 || (!rules.sixToStart && roll == 1));
                            bool couldAdvance = p >= 0 && p < LudoEngine.Home && p + roll <= LudoEngine.Home;
                            if (couldLeaveYard || couldAdvance) failures.Add($"game {game}: reported no moves but token {t} at {p} could move {roll}");
                        }
                        bool keepsTurn = roll == 6 && rules.extraTurnOnSix;
                        if (keepsTurn != (state.current == seatToPlay) && CountPlaying(state) > 1)
                            failures.Add($"game {game}: wrong turn handling after a roll with no moves");
                        continue;
                    }

                    if (state.pendingRoll != roll) failures.Add($"game {game}: pending roll not recorded");
                    var move = LudoAI.Choose(state, moves, rng);
                    var beforeMove = state.Clone();
                    var result = LudoEngine.Apply(state, move);
                    movesTotal++;
                    capturesTotal += result.Captures.Count;
                    CheckMove(game, beforeMove, state, move, result, failures);
                }

                rollsTotal += rolls;
                longestGame = Math.Max(longestGame, rolls);
                CheckFinished(game, state, failures);
            }

            // Strength: Hard should beat Easy clearly more often than not, heads-up.
            const int duels = 600;
            int hardWins = 0;
            for (int i = 0; i < duels; i++)
            {
                var kinds = new[] { SeatKind.Bot, SeatKind.Off, SeatKind.Bot, SeatKind.Off };
                var levels = new[] { BotLevel.Hard, BotLevel.Easy, BotLevel.Easy, BotLevel.Easy };
                var state = LudoEngine.NewGame(kinds, levels, new LudoRules(), LudoMode.VsComputer, i % 2 == 0 ? 0 : 2);
                int guard = 0;
                while (!state.over && guard++ < 6000)
                {
                    if (LudoEngine.Roll(state, rng.Next(1, 7), moves) != RollOutcome.ChooseMove) continue;
                    LudoEngine.Apply(state, LudoAI.Choose(state, moves, rng));
                }
                if (state.over && state.finishOrder[0] == 0) hardWins++;
            }
            float hardRate = hardWins / (float)duels;
            if (hardRate < 0.55f) failures.Add($"Hard bot won only {hardRate:P0} of {duels} games against Easy");

            var sb = new StringBuilder();
            sb.AppendLine(failures.Count == 0 ? "LUDO SIMULATION PASSED" : $"LUDO SIMULATION FAILED ({failures.Count} problems)");
            sb.AppendLine($"games {games}, rolls {rollsTotal}, moves {movesTotal}, captures {capturesTotal}, three-six forfeits {forfeits}, no-move rolls {noMoveRolls}");
            sb.AppendLine($"average rolls per game {(games > 0 ? rollsTotal / (float)games : 0f):F0}, longest {longestGame}");
            sb.AppendLine($"Hard vs Easy heads-up: Hard won {hardRate:P1}");
            foreach (var failure in failures) sb.AppendLine(" - " + failure);
            report = sb.ToString();
            return failures.Count == 0;
        }

        static LudoRules RandomRules(System.Random rng, int game)
        {
            if (game % 3 == 0) return new LudoRules();
            return new LudoRules
            {
                sixToStart = rng.Next(4) != 0,
                extraTurnOnSix = rng.Next(4) != 0,
                threeSixesForfeit = rng.Next(3) != 0,
                extraTurnOnCapture = rng.Next(3) != 0,
                extraTurnOnHome = rng.Next(3) != 0,
                safeSquares = rng.Next(4) != 0,
                endWhenHumansFinish = rng.Next(2) == 0
            };
        }

        static int CountPlaying(LudoState state)
        {
            int count = 0;
            for (int seat = 0; seat < 4; seat++)
                if (state.IsActive(seat) && !state.HasFinished(seat)) count++;
            return count;
        }

        static void CheckMove(int game, LudoState before, LudoState after, LudoMove move, LudoMoveResult result, List<string> failures)
        {
            string where = $"game {game}, move {after.moves} ({move})";
            foreach (int p in after.tokens)
                if (p < LudoEngine.Yard || p > LudoEngine.Home) failures.Add($"{where}: token progress {p} out of range");

            int expectedTo = move.From == LudoEngine.Yard ? 0 : move.From + before.lastRoll;
            if (move.To != expectedTo) failures.Add($"{where}: move target {move.To}, expected {expectedTo}");
            if (after.Token(move.Seat, move.Token) != move.To) failures.Add($"{where}: token not at its target");
            if (after.pendingRoll != 0) failures.Add($"{where}: pending roll not cleared");

            int square = LudoEngine.Absolute(move.Seat, move.To);
            foreach (var capture in result.Captures)
            {
                if (capture.Seat == move.Seat) failures.Add($"{where}: captured own token");
                if (LudoEngine.Absolute(capture.Seat, capture.From) != square) failures.Add($"{where}: captured a token on a different square");
                if (LudoEngine.IsProtected(after.rules, square)) failures.Add($"{where}: captured on a protected square");
                if (after.Token(capture.Seat, capture.Token) != LudoEngine.Yard) failures.Add($"{where}: captured token not sent to the yard");
            }

            // After any move, two different colours can only share a main-track square if it is protected.
            var owner = new Dictionary<int, int>();
            for (int seat = 0; seat < 4; seat++)
            {
                if (!after.IsActive(seat)) continue;
                for (int t = 0; t < 4; t++)
                {
                    int abs = LudoEngine.Absolute(seat, after.Token(seat, t));
                    if (abs < 0 || LudoEngine.IsProtected(after.rules, abs)) continue;
                    if (owner.TryGetValue(abs, out int other) && other != seat)
                        failures.Add($"{where}: seats {other} and {seat} share unprotected square {abs}");
                    owner[abs] = seat;
                }
            }

            if (result.SeatFinished)
            {
                if (after.TokensHome(move.Seat) != 4) failures.Add($"{where}: seat finished without all tokens home");
                if (after.RankOf(move.Seat) != result.FinishRank) failures.Add($"{where}: finish rank mismatch");
            }
            else if (after.TokensHome(move.Seat) == 4)
            {
                failures.Add($"{where}: all tokens home but seat not marked finished");
            }

            if (!result.GameOver)
            {
                if (result.ExtraTurn && after.current != move.Seat) failures.Add($"{where}: extra turn not given");
                if (!result.ExtraTurn && after.current == move.Seat) failures.Add($"{where}: turn did not pass");
                bool expectedExtra = !result.SeatFinished &&
                                     ((before.lastRoll == 6 && after.rules.extraTurnOnSix) ||
                                      (result.Captures.Count > 0 && after.rules.extraTurnOnCapture) ||
                                      (result.ReachedHome && after.rules.extraTurnOnHome));
                if (expectedExtra != result.ExtraTurn) failures.Add($"{where}: extra turn {result.ExtraTurn}, expected {expectedExtra}");
            }
        }

        static void CheckFinished(int game, LudoState state, List<string> failures)
        {
            if (!state.over) return;
            int active = state.ActiveSeatCount;
            if (state.finishedCount != active) failures.Add($"game {game}: {state.finishedCount} ranked seats, expected {active}");
            var seen = new HashSet<int>();
            for (int i = 0; i < state.finishedCount; i++)
            {
                int seat = state.finishOrder[i];
                if (seat < 0 || !state.IsActive(seat) || !seen.Add(seat)) failures.Add($"game {game}: bad finish order entry {seat}");
            }
        }

        static void CheckGeometry(List<string> failures)
        {
            var track = LudoBoard.Track;
            if (track.Length != LudoEngine.TrackLength) failures.Add($"track has {track.Length} squares");
            var cells = new HashSet<Vector2Int>();
            for (int i = 0; i < track.Length; i++)
            {
                if (!cells.Add(track[i])) failures.Add($"track square {track[i]} repeats");
                var next = track[(i + 1) % track.Length];
                int dx = Math.Abs(next.x - track[i].x), dy = Math.Abs(next.y - track[i].y);
                if (Math.Max(dx, dy) != 1) failures.Add($"track squares {i} {track[i]} and {i + 1} {next} are not neighbours");
                if (InYard(track[i]) || InCenter(track[i])) failures.Add($"track square {track[i]} overlaps a yard or the centre");
            }

            for (int seat = 0; seat < 4; seat++)
            {
                var start = track[LudoEngine.StartIndex[seat]];
                var yard = LudoBoard.YardOrigin[seat];
                bool touchesYard = start.x >= yard.x - 1 && start.x <= yard.x + 6 && start.y >= yard.y - 1 && start.y <= yard.y + 6;
                if (!touchesYard) failures.Add($"seat {seat} start square {start} is not beside its yard");

                var entry = track[(LudoEngine.StartIndex[seat] + LudoEngine.LastTrack) % LudoEngine.TrackLength];
                var previous = entry;
                for (int step = 1; step <= 5; step++)
                {
                    var cell = LudoBoard.HomeColumnCell(seat, step);
                    if (Math.Abs(cell.x - previous.x) + Math.Abs(cell.y - previous.y) != 1)
                        failures.Add($"seat {seat} home column step {step} {cell} does not follow {previous}");
                    if (cells.Contains(cell) || InYard(cell) || InCenter(cell)) failures.Add($"seat {seat} home column {cell} overlaps");
                    previous = cell;
                }
                if (!InCenter(previous + (Vector2Int)LudoBoard.StartDirection[seat]))
                    failures.Add($"seat {seat} home column does not lead into the centre");
            }
        }

        static bool InYard(Vector2Int c) => (c.x <= 5 || c.x >= 9) && (c.y <= 5 || c.y >= 9);
        static bool InCenter(Vector2Int c) => c.x >= 6 && c.x <= 8 && c.y >= 6 && c.y <= 8;
    }
}
