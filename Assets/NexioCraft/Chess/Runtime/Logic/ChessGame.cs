using System;
using System.Collections.Generic;
using System.Text;

namespace NexioCraft.Chess
{
    public enum ChessResult
    {
        None,
        WhiteWinsByCheckmate,
        BlackWinsByCheckmate,
        WhiteWinsByResignation,
        BlackWinsByResignation,
        Stalemate,
        DrawByRepetition,
        DrawByFiftyMoves,
        DrawByMaterial
    }

    /// <summary>A game in progress: position, move history (with SAN), draw detection and undo.</summary>
    public sealed class ChessGame
    {
        public ChessPosition Position { get; private set; }
        public string StartFen { get; private set; }
        public ChessResult Result { get; private set; }
        public IReadOnlyList<ChessMove> Moves => moves;
        public IReadOnlyList<string> San => san;
        public IReadOnlyList<ulong> Hashes => hashes;
        public List<ChessMove> LegalMoves { get; } = new List<ChessMove>();

        readonly List<ChessMove> moves = new List<ChessMove>();
        readonly List<string> san = new List<string>();
        readonly List<sbyte> captured = new List<sbyte>();
        readonly List<ulong> hashes = new List<ulong>();

        public ChessGame(string fen = ChessPosition.StartFen)
        {
            StartFen = fen;
            Position = ChessPosition.FromFen(fen);
            hashes.Add(Position.Hash);
            Refresh();
        }

        public bool IsOver => Result != ChessResult.None;
        public int SideToMove => Position.SideToMove;
        public ChessMove LastMove => moves.Count > 0 ? moves[moves.Count - 1] : default;

        /// <summary>Pieces captured so far, in order (positive white, negative black).</summary>
        public IReadOnlyList<sbyte> Captured => captured;

        public void Play(ChessMove move)
        {
            if (IsOver) throw new InvalidOperationException("Game is over");
            if (!LegalMoves.Contains(move)) throw new ArgumentException("Illegal move " + move);
            san.Add(ToSan(Position, move, LegalMoves));
            int capturedSquare = move.IsEnPassant ? move.To - 8 * Position.SideToMove : move.To;
            captured.Add(move.IsCapture ? Position.Squares[capturedSquare] : (sbyte)0);
            Position.MakeMove(move);
            moves.Add(move);
            hashes.Add(Position.Hash);
            Refresh();
        }

        public bool CanUndo => moves.Count > 0;

        public void Undo()
        {
            if (moves.Count == 0) return;
            Position.UnmakeMove();
            int last = moves.Count - 1;
            moves.RemoveAt(last);
            san.RemoveAt(last);
            captured.RemoveAt(last);
            hashes.RemoveAt(hashes.Count - 1);
            Result = ChessResult.None;
            Refresh();
        }

        public void Resign(int side)
        {
            Result = side == Side.White ? ChessResult.BlackWinsByResignation : ChessResult.WhiteWinsByResignation;
        }

        /// <summary>+1 white won, -1 black won, 0 draw or still playing.</summary>
        public int Winner => Result switch
        {
            ChessResult.WhiteWinsByCheckmate or ChessResult.WhiteWinsByResignation => Side.White,
            ChessResult.BlackWinsByCheckmate or ChessResult.BlackWinsByResignation => Side.Black,
            _ => 0
        };

        public bool IsDraw => Result == ChessResult.Stalemate || Result == ChessResult.DrawByRepetition ||
                              Result == ChessResult.DrawByFiftyMoves || Result == ChessResult.DrawByMaterial;

        /// <summary>How many times the current position has occurred since the last pawn move or capture.</summary>
        public int RepetitionCount()
        {
            int count = 0;
            ulong current = Position.Hash;
            int earliest = Math.Max(0, hashes.Count - 1 - Position.HalfmoveClock);
            for (int i = hashes.Count - 1; i >= earliest; i -= 1)
                if (hashes[i] == current) count++;
            return count;
        }

        void Refresh()
        {
            Position.GenerateLegalMoves(LegalMoves);
            if (LegalMoves.Count == 0)
            {
                Result = Position.InCheck(Position.SideToMove)
                    ? (Position.SideToMove == Side.White ? ChessResult.BlackWinsByCheckmate : ChessResult.WhiteWinsByCheckmate)
                    : ChessResult.Stalemate;
            }
            else if (Position.HalfmoveClock >= 100) Result = ChessResult.DrawByFiftyMoves;
            else if (RepetitionCount() >= 3) Result = ChessResult.DrawByRepetition;
            else if (Position.IsInsufficientMaterial()) Result = ChessResult.DrawByMaterial;
            else Result = ChessResult.None;
        }

        /// <summary>Standard algebraic notation for a legal move in the given position.</summary>
        public static string ToSan(ChessPosition position, ChessMove move, List<ChessMove> legalMoves)
        {
            var sb = new StringBuilder(8);
            int piece = position.Squares[move.From];
            int type = Piece.TypeOf(piece);
            if (move.IsCastle)
            {
                sb.Append(move.To > move.From ? "O-O" : "O-O-O");
            }
            else if (type == Piece.Pawn)
            {
                if (move.IsCapture) sb.Append((char)('a' + (move.From & 7))).Append('x');
                sb.Append(ChessPosition.SquareName(move.To));
                if (move.IsPromotion) sb.Append('=').Append(Piece.Letter(move.Promotion));
            }
            else
            {
                sb.Append(Piece.Letter(type));
                bool ambiguous = false, sameFile = false, sameRank = false;
                foreach (var other in legalMoves)
                {
                    if (other.To != move.To || other.From == move.From || position.Squares[other.From] != piece) continue;
                    ambiguous = true;
                    if ((other.From & 7) == (move.From & 7)) sameFile = true;
                    if ((other.From >> 3) == (move.From >> 3)) sameRank = true;
                }
                if (ambiguous)
                {
                    if (!sameFile) sb.Append((char)('a' + (move.From & 7)));
                    else if (!sameRank) sb.Append((char)('1' + (move.From >> 3)));
                    else sb.Append(ChessPosition.SquareName(move.From));
                }
                if (move.IsCapture) sb.Append('x');
                sb.Append(ChessPosition.SquareName(move.To));
            }

            position.MakeMove(move);
            if (position.InCheck(position.SideToMove))
            {
                var replies = new List<ChessMove>();
                position.GenerateLegalMoves(replies);
                sb.Append(replies.Count == 0 ? '#' : '+');
            }
            position.UnmakeMove();
            return sb.ToString();
        }

        /// <summary>Space-separated UCI moves, for saving.</summary>
        public string MovesToUci()
        {
            var sb = new StringBuilder();
            foreach (var move in moves)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(move.Uci);
            }
            return sb.ToString();
        }

        /// <summary>Rebuilds a game from a start FEN and UCI moves; stops at the first move that is not legal.</summary>
        public static ChessGame FromUci(string startFen, string uciMoves)
        {
            var game = new ChessGame(string.IsNullOrEmpty(startFen) ? ChessPosition.StartFen : startFen);
            if (string.IsNullOrEmpty(uciMoves)) return game;
            foreach (string token in uciMoves.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (token.Length < 4 || game.IsOver) break;
                int from = ChessPosition.ParseSquare(token.Substring(0, 2));
                int to = ChessPosition.ParseSquare(token.Substring(2, 2));
                int promotion = token.Length > 4 ? "nbrq".IndexOf(token[4]) + 2 : 0;
                if (from < 0 || to < 0 || promotion == 1) break;
                ChessMove match = default;
                bool found = false;
                foreach (var legal in game.LegalMoves)
                {
                    if (legal.From == from && legal.To == to && legal.Promotion == promotion)
                    {
                        match = legal;
                        found = true;
                        break;
                    }
                }
                if (!found) break;
                game.Play(match);
            }
            return game;
        }
    }
}
