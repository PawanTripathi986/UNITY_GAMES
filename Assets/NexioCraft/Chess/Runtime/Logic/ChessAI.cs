using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace NexioCraft.Chess
{
    public enum ChessLevel { Easy = 0, Medium = 1, Hard = 2 }

    /// <summary>
    /// Alpha-beta chess engine: iterative deepening, quiescence search, transposition table, MVV-LVA and
    /// killer move ordering, and a tapered piece-square evaluation. Runs happily on a worker thread.
    /// </summary>
    public sealed class ChessAI
    {
        const int Infinity = 1_000_000;
        const int MateScore = 100_000;
        const int MaxPly = 64;
        const int TableBits = 18;

        static readonly int[] PieceValue = { 0, 100, 320, 330, 500, 900, 0 };
        static readonly int[] PhaseWeight = { 0, 0, 1, 1, 2, 4, 0 };

        readonly ChessPosition position;
        readonly HashSet<ulong> gameHistory;
        readonly List<ulong> searchPath = new List<ulong>(MaxPly);
        readonly List<ChessMove>[] moveLists = new List<ChessMove>[MaxPly + 1];
        readonly int[][] moveScores = new int[MaxPly + 1][];
        readonly int[,] killers = new int[MaxPly + 1, 2];
        readonly TableEntry[] table = new TableEntry[1 << TableBits];
        readonly CancellationToken cancel;
        readonly Stopwatch clock = new Stopwatch();
        long timeLimitMs;
        bool stopped;
        ChessMove rootBest;

        public long Nodes { get; private set; }
        public int CompletedDepth { get; private set; }
        public int LastScore { get; private set; }

        struct TableEntry
        {
            public ulong Key;
            public int Move;
            public int Score;
            public short Depth;
            public byte Flag;
        }

        const byte FlagExact = 1, FlagLower = 2, FlagUpper = 3;

        ChessAI(ChessPosition root, IReadOnlyList<ulong> historyHashes, CancellationToken token)
        {
            position = root.Clone();
            gameHistory = new HashSet<ulong>();
            // Positions since the last irreversible move count as repetitions (excluding the current one).
            int earliest = Math.Max(0, historyHashes.Count - 1 - root.HalfmoveClock);
            for (int i = earliest; i < historyHashes.Count - 1; i++) gameHistory.Add(historyHashes[i]);
            // The root itself heads the search path so lines that return to it count as repetitions.
            searchPath.Add(position.Hash);
            cancel = token;
            for (int i = 0; i <= MaxPly; i++)
            {
                moveLists[i] = new List<ChessMove>(64);
                moveScores[i] = new int[256];
            }
        }

        /// <summary>Picks a move for the side to move. Lower levels search shallower and add deliberate mistakes.</summary>
        public static ChessMove FindMove(ChessGame game, ChessLevel level, Random rng, CancellationToken token = default)
        {
            if (game.LegalMoves.Count == 1) return game.LegalMoves[0];
            var ai = new ChessAI(game.Position, game.Hashes, token);
            switch (level)
            {
                case ChessLevel.Easy:
                    return ai.PickWithNoise(game.LegalMoves, 1, 140, rng);
                case ChessLevel.Medium:
                    return ai.PickWithNoise(game.LegalMoves, 2, 35, rng);
                default:
                    return ai.Search(12, 1400);
            }
        }

        /// <summary>Full-strength search with custom limits (used by hints and tests).</summary>
        public static ChessMove FindBestMove(ChessGame game, int maxDepth, long timeMs, CancellationToken token = default)
        {
            if (game.LegalMoves.Count == 1) return game.LegalMoves[0];
            return new ChessAI(game.Position, game.Hashes, token).Search(maxDepth, timeMs);
        }

        /// <summary>Best move with iterative deepening, limited by depth and time.</summary>
        ChessMove Search(int maxDepth, long timeMs)
        {
            timeLimitMs = timeMs;
            clock.Restart();
            ChessMove best = default;
            for (int depth = 1; depth <= maxDepth; depth++)
            {
                stopped = false;
                rootBest = default;
                int score = Negamax(depth, 0, -Infinity, Infinity);
                if (stopped && depth > 1) break;
                if (!rootBest.IsNull || depth == 1)
                {
                    best = rootBest;
                    LastScore = score;
                    CompletedDepth = depth;
                }
                if (Math.Abs(score) > MateScore - MaxPly) break;
                // Don't start a depth we are unlikely to finish.
                if (clock.ElapsedMilliseconds > timeLimitMs / 2) break;
            }
            if (best.IsNull)
            {
                var legal = new List<ChessMove>();
                position.GenerateLegalMoves(legal);
                if (legal.Count > 0) best = legal[0];
            }
            return best;
        }

        /// <summary>Scores every root move with a shallow search, adds noise, and picks the best noisy score.</summary>
        ChessMove PickWithNoise(List<ChessMove> legalMoves, int depth, int noise, Random rng)
        {
            timeLimitMs = 2000;
            clock.Restart();
            ChessMove best = legalMoves[0];
            int bestScore = int.MinValue;
            foreach (var move in legalMoves)
            {
                position.MakeMove(move);
                searchPath.Add(position.Hash);
                int score = -Negamax(depth - 1, 1, -Infinity, Infinity);
                searchPath.RemoveAt(searchPath.Count - 1);
                position.UnmakeMove();
                if (cancel.IsCancellationRequested) break;
                // Never throw away a forced mate, but otherwise blur the evaluation.
                int noisy = Math.Abs(score) > MateScore - MaxPly ? score : score + rng.Next(-noise, noise + 1);
                if (noisy > bestScore)
                {
                    bestScore = noisy;
                    best = move;
                }
            }
            return best;
        }

        int Negamax(int depth, int ply, int alpha, int beta)
        {
            if ((++Nodes & 1023) == 0 && (clock.ElapsedMilliseconds > timeLimitMs || cancel.IsCancellationRequested)) stopped = true;
            if (stopped) return 0;

            if (ply > 0)
            {
                if (position.HalfmoveClock >= 100 || IsRepetition()) return 0;
                // Mate distance pruning.
                alpha = Math.Max(alpha, -MateScore + ply);
                beta = Math.Min(beta, MateScore - ply - 1);
                if (alpha >= beta) return alpha;
            }

            int side = position.SideToMove;
            bool inCheck = position.InCheck(side);
            if (inCheck && ply < MaxPly - 1) depth++;
            if (depth <= 0 || ply >= MaxPly) return Quiesce(alpha, beta, ply);

            ulong key = position.Hash;
            ref var entry = ref table[key & ((1UL << TableBits) - 1)];
            int ttMove = 0;
            if (entry.Key == key)
            {
                ttMove = entry.Move;
                if (ply > 0 && entry.Depth >= depth)
                {
                    int ttScore = FromTableScore(entry.Score, ply);
                    if (entry.Flag == FlagExact) return ttScore;
                    if (entry.Flag == FlagLower && ttScore >= beta) return ttScore;
                    if (entry.Flag == FlagUpper && ttScore <= alpha) return ttScore;
                }
            }

            var moves = moveLists[ply];
            moves.Clear();
            position.GenerateMoves(moves);
            ScoreMoves(moves, moveScores[ply], ttMove, ply);

            int originalAlpha = alpha;
            int bestScore = -Infinity;
            int bestMove = 0;
            int legal = 0;
            for (int i = 0; i < moves.Count; i++)
            {
                var move = PickNext(moves, moveScores[ply], i);
                position.MakeMove(move);
                if (position.IsSquareAttacked(position.KingSquare(side), -side))
                {
                    position.UnmakeMove();
                    continue;
                }
                legal++;
                searchPath.Add(position.Hash);
                int score = -Negamax(depth - 1, ply + 1, -beta, -alpha);
                searchPath.RemoveAt(searchPath.Count - 1);
                position.UnmakeMove();
                if (stopped) return 0;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = move.Packed;
                    if (ply == 0) rootBest = move;
                }
                if (score > alpha) alpha = score;
                if (alpha >= beta)
                {
                    if (!move.IsCapture && killers[ply, 0] != move.Packed)
                    {
                        killers[ply, 1] = killers[ply, 0];
                        killers[ply, 0] = move.Packed;
                    }
                    break;
                }
            }

            if (legal == 0) return inCheck ? -MateScore + ply : 0;

            entry.Key = key;
            entry.Move = bestMove;
            entry.Depth = (short)depth;
            entry.Score = ToTableScore(bestScore, ply);
            entry.Flag = bestScore <= originalAlpha ? FlagUpper : bestScore >= beta ? FlagLower : FlagExact;
            return bestScore;
        }

        int Quiesce(int alpha, int beta, int ply)
        {
            if ((++Nodes & 1023) == 0 && (clock.ElapsedMilliseconds > timeLimitMs || cancel.IsCancellationRequested)) stopped = true;
            if (stopped) return 0;

            int standPat = Evaluate();
            if (standPat >= beta) return standPat;
            if (standPat > alpha) alpha = standPat;
            if (ply >= MaxPly) return standPat;

            var moves = moveLists[ply];
            moves.Clear();
            position.GenerateMoves(moves, true);
            ScoreMoves(moves, moveScores[ply], 0, ply);
            int side = position.SideToMove;
            for (int i = 0; i < moves.Count; i++)
            {
                var move = PickNext(moves, moveScores[ply], i);
                position.MakeMove(move);
                if (position.IsSquareAttacked(position.KingSquare(side), -side))
                {
                    position.UnmakeMove();
                    continue;
                }
                int score = -Quiesce(-beta, -alpha, ply + 1);
                position.UnmakeMove();
                if (stopped) return 0;
                if (score >= beta) return score;
                if (score > alpha) alpha = score;
            }
            return alpha;
        }

        bool IsRepetition()
        {
            ulong hash = position.Hash;
            for (int i = searchPath.Count - 2; i >= 0; i--)
                if (searchPath[i] == hash) return true;
            return gameHistory.Contains(hash);
        }

        void ScoreMoves(List<ChessMove> moves, int[] scores, int ttMove, int ply)
        {
            for (int i = 0; i < moves.Count; i++)
            {
                var move = moves[i];
                int packed = move.Packed;
                int score;
                if (packed == ttMove) score = 10_000_000;
                else if (move.IsCapture)
                {
                    int victim = move.IsEnPassant ? Piece.Pawn : Piece.TypeOf(position.Squares[move.To]);
                    int attacker = Piece.TypeOf(position.Squares[move.From]);
                    score = 1_000_000 + PieceValue[victim] * 10 - PieceValue[attacker] / 10;
                }
                else if (move.IsPromotion) score = 900_000 + move.Promotion;
                else if (packed == killers[ply, 0]) score = 800_000;
                else if (packed == killers[ply, 1]) score = 790_000;
                else score = 0;
                if (move.IsPromotion && move.Promotion != Piece.Queen) score -= 950_000;
                scores[i] = score;
            }
        }

        static ChessMove PickNext(List<ChessMove> moves, int[] scores, int start)
        {
            int best = start;
            for (int i = start + 1; i < moves.Count; i++)
                if (scores[i] > scores[best]) best = i;
            if (best != start)
            {
                (moves[start], moves[best]) = (moves[best], moves[start]);
                (scores[start], scores[best]) = (scores[best], scores[start]);
            }
            return moves[start];
        }

        static int ToTableScore(int score, int ply) => score > MateScore - MaxPly ? score + ply : score < -MateScore + MaxPly ? score - ply : score;
        static int FromTableScore(int score, int ply) => score > MateScore - MaxPly ? score - ply : score < -MateScore + MaxPly ? score + ply : score;

        /// <summary>Static evaluation from the side to move's point of view.</summary>
        int Evaluate()
        {
            int middle = 0, end = 0, phase = 0;
            int whiteBishops = 0, blackBishops = 0, whiteMaterial = 0, blackMaterial = 0;
            var squares = position.Squares;
            for (int sq = 0; sq < 64; sq++)
            {
                int p = squares[sq];
                if (p == 0) continue;
                int type = Piece.TypeOf(p);
                bool white = p > 0;
                int index = white ? (7 - (sq >> 3)) * 8 + (sq & 7) : sq;
                int mg, eg;
                switch (type)
                {
                    case Piece.Pawn: mg = PawnTable[index]; eg = mg + PawnEndBonus[index]; break;
                    case Piece.Knight: mg = eg = KnightTable[index]; break;
                    case Piece.Bishop: mg = eg = BishopTable[index]; break;
                    case Piece.Rook: mg = eg = RookTable[index]; break;
                    case Piece.Queen: mg = eg = QueenTable[index]; break;
                    default: mg = KingMiddleTable[index]; eg = KingEndTable[index]; break;
                }
                int value = PieceValue[type];
                phase += PhaseWeight[type];
                if (white)
                {
                    middle += value + mg;
                    end += value + eg;
                    whiteMaterial += value;
                    if (type == Piece.Bishop) whiteBishops++;
                }
                else
                {
                    middle -= value + mg;
                    end -= value + eg;
                    blackMaterial += value;
                    if (type == Piece.Bishop) blackBishops++;
                }
            }

            if (whiteBishops >= 2) { middle += 30; end += 40; }
            if (blackBishops >= 2) { middle -= 30; end -= 40; }

            phase = Math.Min(phase, 24);
            int score = (middle * phase + end * (24 - phase)) / 24;

            // Mop-up: when clearly winning an endgame, push the losing king to the edge and bring ours closer.
            int diff = whiteMaterial - blackMaterial;
            if (phase <= 8 && Math.Abs(diff) >= 400)
            {
                int winner = diff > 0 ? Side.White : Side.Black;
                int loserKing = position.KingSquare(-winner);
                int winnerKing = position.KingSquare(winner);
                int lf = loserKing & 7, lr = loserKing >> 3;
                int centerDistance = Math.Max(3 - lf, lf - 4) + Math.Max(3 - lr, lr - 4);
                int kingDistance = Math.Abs(lf - (winnerKing & 7)) + Math.Abs(lr - (winnerKing >> 3));
                int bonus = centerDistance * 12 + (14 - kingDistance) * 5;
                score += winner == Side.White ? bonus : -bonus;
            }

            return position.SideToMove == Side.White ? score : -score;
        }

        // Piece-square tables from white's point of view, a8 first (Tomasz Michniewski's simplified evaluation).
        static readonly int[] PawnTable =
        {
             0,  0,  0,  0,  0,  0,  0,  0,
            50, 50, 50, 50, 50, 50, 50, 50,
            10, 10, 20, 30, 30, 20, 10, 10,
             5,  5, 10, 25, 25, 10,  5,  5,
             0,  0,  0, 20, 20,  0,  0,  0,
             5, -5,-10,  0,  0,-10, -5,  5,
             5, 10, 10,-20,-20, 10, 10,  5,
             0,  0,  0,  0,  0,  0,  0,  0
        };

        static readonly int[] PawnEndBonus =
        {
             0,  0,  0,  0,  0,  0,  0,  0,
            80, 80, 80, 80, 80, 80, 80, 80,
            50, 50, 40, 30, 30, 40, 50, 50,
            30, 30, 20, 10, 10, 20, 30, 30,
            15, 15, 10,  0,  0, 10, 15, 15,
             5,  5,  5,  0,  0,  5,  5,  5,
             0,  0,  0, 20, 20,  0,  0,  0,
             0,  0,  0,  0,  0,  0,  0,  0
        };

        static readonly int[] KnightTable =
        {
            -50,-40,-30,-30,-30,-30,-40,-50,
            -40,-20,  0,  0,  0,  0,-20,-40,
            -30,  0, 10, 15, 15, 10,  0,-30,
            -30,  5, 15, 20, 20, 15,  5,-30,
            -30,  0, 15, 20, 20, 15,  0,-30,
            -30,  5, 10, 15, 15, 10,  5,-30,
            -40,-20,  0,  5,  5,  0,-20,-40,
            -50,-40,-30,-30,-30,-30,-40,-50
        };

        static readonly int[] BishopTable =
        {
            -20,-10,-10,-10,-10,-10,-10,-20,
            -10,  0,  0,  0,  0,  0,  0,-10,
            -10,  0,  5, 10, 10,  5,  0,-10,
            -10,  5,  5, 10, 10,  5,  5,-10,
            -10,  0, 10, 10, 10, 10,  0,-10,
            -10, 10, 10, 10, 10, 10, 10,-10,
            -10,  5,  0,  0,  0,  0,  5,-10,
            -20,-10,-10,-10,-10,-10,-10,-20
        };

        static readonly int[] RookTable =
        {
              0,  0,  0,  0,  0,  0,  0,  0,
              5, 10, 10, 10, 10, 10, 10,  5,
             -5,  0,  0,  0,  0,  0,  0, -5,
             -5,  0,  0,  0,  0,  0,  0, -5,
             -5,  0,  0,  0,  0,  0,  0, -5,
             -5,  0,  0,  0,  0,  0,  0, -5,
             -5,  0,  0,  0,  0,  0,  0, -5,
              0,  0,  0,  5,  5,  0,  0,  0
        };

        static readonly int[] QueenTable =
        {
            -20,-10,-10, -5, -5,-10,-10,-20,
            -10,  0,  0,  0,  0,  0,  0,-10,
            -10,  0,  5,  5,  5,  5,  0,-10,
             -5,  0,  5,  5,  5,  5,  0, -5,
              0,  0,  5,  5,  5,  5,  0, -5,
            -10,  5,  5,  5,  5,  5,  0,-10,
            -10,  0,  5,  0,  0,  0,  0,-10,
            -20,-10,-10, -5, -5,-10,-10,-20
        };

        static readonly int[] KingMiddleTable =
        {
            -30,-40,-40,-50,-50,-40,-40,-30,
            -30,-40,-40,-50,-50,-40,-40,-30,
            -30,-40,-40,-50,-50,-40,-40,-30,
            -30,-40,-40,-50,-50,-40,-40,-30,
            -20,-30,-30,-40,-40,-30,-30,-20,
            -10,-20,-20,-20,-20,-20,-20,-10,
             20, 20,  0,  0,  0,  0, 20, 20,
             20, 30, 10,  0,  0, 10, 30, 20
        };

        static readonly int[] KingEndTable =
        {
            -50,-40,-30,-20,-20,-30,-40,-50,
            -30,-20,-10,  0,  0,-10,-20,-30,
            -30,-10, 20, 30, 30, 20,-10,-30,
            -30,-10, 30, 40, 40, 30,-10,-30,
            -30,-10, 30, 40, 40, 30,-10,-30,
            -30,-10, 20, 30, 30, 20,-10,-30,
            -30,-30,  0,  0,  0,  0,-30,-30,
            -50,-30,-30,-30,-30,-30,-30,-50
        };
    }
}
