using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using NexioCraft.Chess;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace NexioCraft.EditorTools
{
    /// <summary>
    /// Chess engine checks: perft move counts on standard positions, draw/mate detection, SAN, save/restore,
    /// hash consistency over random games, and a Hard-vs-Easy match.
    ///   Unity -batchmode -projectPath . -executeMethod NexioCraft.EditorTools.ChessTests.CommandLine
    /// </summary>
    public static class ChessTests
    {
        [MenuItem("NexioCraft/Chess/Run Engine Tests")]
        public static void RunFromMenu()
        {
            bool ok = Run(out string report);
            Debug.Log(report);
            EditorUtility.DisplayDialog(ok ? "Chess tests passed" : "Chess tests FAILED", report, "OK");
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
            var total = Stopwatch.StartNew();

            // Perft reference counts from the Chess Programming Wiki.
            Perft(log, failures, "start", ChessPosition.StartFen, 20, 400, 8902, 197281);
            Perft(log, failures, "kiwipete", "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1", 48, 2039, 97862);
            Perft(log, failures, "position3", "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", 14, 191, 2812, 43238);
            Perft(log, failures, "position4", "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1", 6, 264, 9467);
            Perft(log, failures, "position5", "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8", 44, 1486, 62379);

            // Results.
            ExpectResult(failures, "stalemate", "7k/5Q2/6K1/8/8/8/8/8 b - - 0 1", ChessResult.Stalemate);
            ExpectResult(failures, "bare kings", "8/8/8/4k3/8/8/8/4K3 w - - 0 1", ChessResult.DrawByMaterial);
            ExpectResult(failures, "checkmated", "R5k1/5ppp/8/8/8/8/5PPP/6K1 b - - 0 1", ChessResult.WhiteWinsByCheckmate);

            // Engine finds mates in one.
            ExpectBestMove(failures, "back rank mate", "6k1/5ppp/8/8/8/8/5PPP/3R2K1 w - - 0 1", "d1d8");
            ExpectBestMove(failures, "scholar's mate", "r1bqkb1r/pppp1ppp/2n2n2/4p2Q/2B1P3/8/PPPP1PPP/RNB1K1NR w KQkq - 4 4", "h5f7");

            // SAN and promotion.
            var game = new ChessGame();
            PlaySan(failures, game, "e2e4", "e4");
            PlaySan(failures, game, "e7e5", "e5");
            PlaySan(failures, game, "g1f3", "Nf3");
            PlaySan(failures, game, "b8c6", "Nc6");
            PlaySan(failures, game, "f1c4", "Bc4");
            PlaySan(failures, game, "g8f6", "Nf6");
            PlaySan(failures, game, "e1g1", "O-O");
            var promo = new ChessGame("8/P6k/8/8/8/8/8/K7 w - - 0 1");
            int promotions = 0;
            foreach (var m in promo.LegalMoves) if (m.IsPromotion) promotions++;
            if (promotions != 4) failures.Add($"expected 4 promotion moves, got {promotions}");
            PlaySan(failures, promo, "a7a8q", "a8=Q");

            // Save and restore.
            var restored = ChessGame.FromUci(game.StartFen, game.MovesToUci());
            if (restored.Position.ToFen() != game.Position.ToFen()) failures.Add("UCI save/restore produced a different position");

            // Random games: incremental hash, FEN round trip, and full undo.
            var rng = new Random(15092026);
            int randomPlies = 0;
            for (int g = 0; g < 150 && failures.Count < 20; g++)
            {
                var random = new ChessGame();
                string startFen = random.Position.ToFen();
                for (int ply = 0; ply < 250 && !random.IsOver; ply++)
                {
                    random.Play(random.LegalMoves[rng.Next(random.LegalMoves.Count)]);
                    randomPlies++;
                    var pos = random.Position;
                    if (pos.Hash != pos.ComputeHash()) { failures.Add($"random game {g}: incremental hash drifted at ply {ply}"); break; }
                    if (ChessPosition.FromFen(pos.ToFen()).Hash != pos.Hash) { failures.Add($"random game {g}: FEN round trip changed hash at ply {ply} ({pos.ToFen()})"); break; }
                }
                while (random.CanUndo) random.Undo();
                if (random.Position.ToFen() != startFen) failures.Add($"random game {g}: undo did not restore the start position");
            }
            log.AppendLine($"random games: 150 games, {randomPlies} plies checked");

            // Strength: a short-time Hard engine should clearly beat Easy.
            int hardPoints2 = 0, games = 8;
            for (int g = 0; g < games; g++)
            {
                var match = new ChessGame();
                int hardSide = g % 2 == 0 ? Side.White : Side.Black;
                for (int ply = 0; ply < 220 && !match.IsOver; ply++)
                {
                    var move = match.SideToMove == hardSide
                        ? ChessAI.FindBestMove(match, 4, 60)
                        : ChessAI.FindMove(match, ChessLevel.Easy, rng);
                    match.Play(move);
                }
                int winner = match.Winner;
                if (winner == hardSide) hardPoints2 += 2;
                else if (winner == 0) hardPoints2 += 1;
            }
            log.AppendLine($"Hard vs Easy: Hard scored {hardPoints2 / 2f} / {games}");
            if (hardPoints2 < games * 2 * 0.7f) failures.Add($"Hard engine scored only {hardPoints2 / 2f}/{games} against Easy");

            var sb = new StringBuilder();
            sb.AppendLine(failures.Count == 0 ? "CHESS TESTS PASSED" : $"CHESS TESTS FAILED ({failures.Count})");
            sb.Append(log);
            sb.AppendLine($"total time {total.Elapsed.TotalSeconds:F1}s");
            foreach (var failure in failures) sb.AppendLine(" - " + failure);
            report = sb.ToString();
            return failures.Count == 0;
        }

        static void Perft(StringBuilder log, List<string> failures, string name, string fen, params long[] expected)
        {
            var timer = Stopwatch.StartNew();
            var position = ChessPosition.FromFen(fen);
            for (int depth = 1; depth <= expected.Length; depth++)
            {
                long nodes = position.Perft(depth);
                if (nodes != expected[depth - 1]) failures.Add($"perft {name} depth {depth}: {nodes}, expected {expected[depth - 1]}");
            }
            log.AppendLine($"perft {name}: depth {expected.Length} = {expected[expected.Length - 1]} in {timer.ElapsedMilliseconds} ms");
        }

        static void ExpectResult(List<string> failures, string name, string fen, ChessResult expected)
        {
            var game = new ChessGame(fen);
            if (game.Result != expected) failures.Add($"{name}: result {game.Result}, expected {expected}");
        }

        static void ExpectBestMove(List<string> failures, string name, string fen, string uci)
        {
            var game = new ChessGame(fen);
            var move = ChessAI.FindBestMove(game, 4, 500);
            if (move.Uci != uci) failures.Add($"{name}: engine played {move.Uci}, expected {uci}");
            var hard = ChessAI.FindMove(game, ChessLevel.Hard, new Random(1));
            if (hard.Uci != uci) failures.Add($"{name}: Hard level played {hard.Uci}, expected {uci}");
            game.Play(move);
            if (game.Result != ChessResult.WhiteWinsByCheckmate) failures.Add($"{name}: expected checkmate after {uci}, got {game.Result}");
        }

        static void PlaySan(List<string> failures, ChessGame game, string uci, string expectedSan)
        {
            foreach (var move in game.LegalMoves)
            {
                if (move.Uci != uci) continue;
                game.Play(move);
                string san = game.San[game.San.Count - 1];
                if (san != expectedSan) failures.Add($"SAN for {uci}: {san}, expected {expectedSan}");
                return;
            }
            failures.Add($"move {uci} not legal in {game.Position.ToFen()}");
        }
    }
}
