using System;
using System.Collections.Generic;
using System.Text;

namespace NexioCraft.Chess
{
    /// <summary>Piece codes: positive white, negative black, 0 empty.</summary>
    public static class Piece
    {
        public const int None = 0, Pawn = 1, Knight = 2, Bishop = 3, Rook = 4, Queen = 5, King = 6;

        public static int TypeOf(int piece) => piece < 0 ? -piece : piece;
        public static int SideOf(int piece) => piece > 0 ? Side.White : piece < 0 ? Side.Black : 0;
        public static char Letter(int type) => " PNBRQK"[type];
    }

    public static class Side
    {
        public const int White = 1;
        public const int Black = -1;
    }

    public readonly struct ChessMove : IEquatable<ChessMove>
    {
        public const int FlagCapture = 1, FlagEnPassant = 2, FlagCastle = 4, FlagDoublePush = 8;

        public readonly byte From;
        public readonly byte To;
        /// <summary>Piece type promoted to (2..5), or 0.</summary>
        public readonly byte Promotion;
        public readonly byte Flags;

        public ChessMove(int from, int to, int promotion, int flags)
        {
            From = (byte)from;
            To = (byte)to;
            Promotion = (byte)promotion;
            Flags = (byte)flags;
        }

        public bool IsCapture => (Flags & FlagCapture) != 0;
        public bool IsEnPassant => (Flags & FlagEnPassant) != 0;
        public bool IsCastle => (Flags & FlagCastle) != 0;
        public bool IsPromotion => Promotion != 0;
        public bool IsNull => From == 0 && To == 0;

        public int Packed => From | (To << 6) | (Promotion << 12) | (Flags << 16);
        public static ChessMove FromPacked(int v) => new ChessMove(v & 63, (v >> 6) & 63, (v >> 12) & 15, (v >> 16) & 255);

        public string Uci
        {
            get
            {
                string s = ChessPosition.SquareName(From) + ChessPosition.SquareName(To);
                return Promotion != 0 ? s + char.ToLowerInvariant(Piece.Letter(Promotion)) : s;
            }
        }

        public bool Equals(ChessMove other) => From == other.From && To == other.To && Promotion == other.Promotion;
        public override bool Equals(object obj) => obj is ChessMove other && Equals(other);
        public override int GetHashCode() => From | (To << 6) | (Promotion << 12);
        public override string ToString() => Uci;
    }

    /// <summary>
    /// Chess position with make/unmake and legal move generation. Squares: 0 = a1 … 7 = h1 … 63 = h8.
    /// </summary>
    public sealed class ChessPosition
    {
        public const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
        public const int CastleWhiteKing = 1, CastleWhiteQueen = 2, CastleBlackKing = 4, CastleBlackQueen = 8;

        public readonly sbyte[] Squares = new sbyte[64];
        public int SideToMove = Side.White;
        public int Castling;
        /// <summary>En passant target square (only set when a capture is actually possible), or -1.</summary>
        public int EnPassant = -1;
        public int HalfmoveClock;
        public int FullmoveNumber = 1;
        public ulong Hash;

        int whiteKing = -1;
        int blackKing = -1;
        readonly List<Undo> undoStack = new List<Undo>(256);
        readonly List<ChessMove> scratch = new List<ChessMove>(64);

        struct Undo
        {
            public ChessMove Move;
            public sbyte Captured;
            public int Castling;
            public int EnPassant;
            public int Halfmove;
            public ulong Hash;
        }

        // Direction offsets: N, S, W, E, NW, SE, NE, SW (rooks use 0..3, bishops 4..7).
        static readonly int[] DirectionOffsets = { 8, -8, -1, 1, 7, -7, 9, -9 };
        static readonly int[,] SquaresToEdge = new int[64, 8];
        static readonly int[][] KnightTargets = new int[64][];
        static readonly int[][] KingTargets = new int[64][];
        static readonly int[] CastlingMask = new int[64];

        static ChessPosition()
        {
            for (int sq = 0; sq < 64; sq++)
            {
                int file = sq & 7, rank = sq >> 3;
                int north = 7 - rank, south = rank, west = file, east = 7 - file;
                SquaresToEdge[sq, 0] = north;
                SquaresToEdge[sq, 1] = south;
                SquaresToEdge[sq, 2] = west;
                SquaresToEdge[sq, 3] = east;
                SquaresToEdge[sq, 4] = Math.Min(north, west);
                SquaresToEdge[sq, 5] = Math.Min(south, east);
                SquaresToEdge[sq, 6] = Math.Min(north, east);
                SquaresToEdge[sq, 7] = Math.Min(south, west);

                KnightTargets[sq] = Leaps(file, rank, new[] { 1, 2, 2, 1, 2, -1, 1, -2, -1, -2, -2, -1, -2, 1, -1, 2 });
                KingTargets[sq] = Leaps(file, rank, new[] { 1, 0, 1, 1, 0, 1, -1, 1, -1, 0, -1, -1, 0, -1, 1, -1 });
                CastlingMask[sq] = 15;
            }
            CastlingMask[0] = 15 & ~CastleWhiteQueen;
            CastlingMask[4] = 15 & ~(CastleWhiteKing | CastleWhiteQueen);
            CastlingMask[7] = 15 & ~CastleWhiteKing;
            CastlingMask[56] = 15 & ~CastleBlackQueen;
            CastlingMask[60] = 15 & ~(CastleBlackKing | CastleBlackQueen);
            CastlingMask[63] = 15 & ~CastleBlackKing;
        }

        static int[] Leaps(int file, int rank, int[] deltas)
        {
            var targets = new List<int>(8);
            for (int i = 0; i < deltas.Length; i += 2)
            {
                int f = file + deltas[i], r = rank + deltas[i + 1];
                if (f >= 0 && f < 8 && r >= 0 && r < 8) targets.Add(r * 8 + f);
            }
            return targets.ToArray();
        }

        public static ChessPosition Start() => FromFen(StartFen);

        public static string SquareName(int sq) => ((char)('a' + (sq & 7))).ToString() + (char)('1' + (sq >> 3));

        public static int ParseSquare(string name)
        {
            if (name == null || name.Length != 2) return -1;
            int file = name[0] - 'a', rank = name[1] - '1';
            return file >= 0 && file < 8 && rank >= 0 && rank < 8 ? rank * 8 + file : -1;
        }

        public int KingSquare(int side) => side == Side.White ? whiteKing : blackKing;

        public bool InCheck(int side)
        {
            int king = KingSquare(side);
            return king >= 0 && IsSquareAttacked(king, -side);
        }

        public static ChessPosition FromFen(string fen)
        {
            var parts = fen.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 1) throw new FormatException("Empty FEN");
            var pos = new ChessPosition();
            int rank = 7, file = 0;
            foreach (char c in parts[0])
            {
                if (c == '/')
                {
                    rank--;
                    file = 0;
                    continue;
                }
                if (char.IsDigit(c))
                {
                    file += c - '0';
                    continue;
                }
                int type = "pnbrqk".IndexOf(char.ToLowerInvariant(c)) + 1;
                if (type <= 0 || rank < 0 || file > 7) throw new FormatException("Bad FEN board: " + parts[0]);
                pos.Squares[rank * 8 + file] = (sbyte)(char.IsUpper(c) ? type : -type);
                file++;
            }

            pos.SideToMove = parts.Length > 1 && parts[1] == "b" ? Side.Black : Side.White;
            if (parts.Length > 2 && parts[2] != "-")
            {
                foreach (char c in parts[2])
                {
                    if (c == 'K') pos.Castling |= CastleWhiteKing;
                    else if (c == 'Q') pos.Castling |= CastleWhiteQueen;
                    else if (c == 'k') pos.Castling |= CastleBlackKing;
                    else if (c == 'q') pos.Castling |= CastleBlackQueen;
                }
            }
            if (parts.Length > 3 && parts[3] != "-")
            {
                int ep = ParseSquare(parts[3]);
                if (ep >= 0 && pos.CanCaptureEnPassant(ep - 8 * pos.SideToMove, pos.SideToMove)) pos.EnPassant = ep;
            }
            if (parts.Length > 4) int.TryParse(parts[4], out pos.HalfmoveClock);
            if (parts.Length > 5 && int.TryParse(parts[5], out int fullmove)) pos.FullmoveNumber = Math.Max(1, fullmove);

            for (int sq = 0; sq < 64; sq++)
            {
                if (pos.Squares[sq] == Piece.King) pos.whiteKing = sq;
                else if (pos.Squares[sq] == -Piece.King) pos.blackKing = sq;
            }
            if (pos.whiteKing < 0 || pos.blackKing < 0) throw new FormatException("FEN needs both kings");
            pos.Hash = pos.ComputeHash();
            return pos;
        }

        public string ToFen()
        {
            var sb = new StringBuilder();
            for (int rank = 7; rank >= 0; rank--)
            {
                int empty = 0;
                for (int file = 0; file < 8; file++)
                {
                    int p = Squares[rank * 8 + file];
                    if (p == 0)
                    {
                        empty++;
                        continue;
                    }
                    if (empty > 0)
                    {
                        sb.Append(empty);
                        empty = 0;
                    }
                    char letter = Piece.Letter(Piece.TypeOf(p));
                    sb.Append(p > 0 ? letter : char.ToLowerInvariant(letter));
                }
                if (empty > 0) sb.Append(empty);
                if (rank > 0) sb.Append('/');
            }
            sb.Append(SideToMove == Side.White ? " w " : " b ");
            if (Castling == 0) sb.Append('-');
            else
            {
                if ((Castling & CastleWhiteKing) != 0) sb.Append('K');
                if ((Castling & CastleWhiteQueen) != 0) sb.Append('Q');
                if ((Castling & CastleBlackKing) != 0) sb.Append('k');
                if ((Castling & CastleBlackQueen) != 0) sb.Append('q');
            }
            sb.Append(' ').Append(EnPassant >= 0 ? SquareName(EnPassant) : "-");
            sb.Append(' ').Append(HalfmoveClock).Append(' ').Append(FullmoveNumber);
            return sb.ToString();
        }

        /// <summary>Copy of the board state (without undo history).</summary>
        public ChessPosition Clone()
        {
            var copy = new ChessPosition();
            Array.Copy(Squares, copy.Squares, 64);
            copy.SideToMove = SideToMove;
            copy.Castling = Castling;
            copy.EnPassant = EnPassant;
            copy.HalfmoveClock = HalfmoveClock;
            copy.FullmoveNumber = FullmoveNumber;
            copy.Hash = Hash;
            copy.whiteKing = whiteKing;
            copy.blackKing = blackKing;
            return copy;
        }

        public void MakeMove(ChessMove move)
        {
            int from = move.From, to = move.To;
            int side = SideToMove;
            sbyte piece = Squares[from];
            var undo = new Undo { Move = move, Captured = Squares[to], Castling = Castling, EnPassant = EnPassant, Halfmove = HalfmoveClock, Hash = Hash };

            if (EnPassant >= 0) Hash ^= Zobrist.EnPassantFile[EnPassant & 7];
            Hash ^= Zobrist.Castling[Castling];

            Hash ^= Zobrist.Key(piece, from);
            Squares[from] = 0;
            if (undo.Captured != 0) Hash ^= Zobrist.Key(undo.Captured, to);

            if (move.IsEnPassant)
            {
                int captureSquare = to - 8 * side;
                undo.Captured = Squares[captureSquare];
                Hash ^= Zobrist.Key(undo.Captured, captureSquare);
                Squares[captureSquare] = 0;
            }

            sbyte placed = move.Promotion != 0 ? (sbyte)(move.Promotion * side) : piece;
            Squares[to] = placed;
            Hash ^= Zobrist.Key(placed, to);

            if (move.IsCastle)
            {
                int rookFrom = to > from ? from + 3 : from - 4;
                int rookTo = to > from ? from + 1 : from - 1;
                sbyte rook = Squares[rookFrom];
                Squares[rookFrom] = 0;
                Squares[rookTo] = rook;
                Hash ^= Zobrist.Key(rook, rookFrom) ^ Zobrist.Key(rook, rookTo);
            }

            if (piece == Piece.King) whiteKing = to;
            else if (piece == -Piece.King) blackKing = to;

            Castling &= CastlingMask[from] & CastlingMask[to];
            Hash ^= Zobrist.Castling[Castling];

            EnPassant = -1;
            if ((move.Flags & ChessMove.FlagDoublePush) != 0 && CanCaptureEnPassant(to, -side))
            {
                EnPassant = from + 8 * side;
                Hash ^= Zobrist.EnPassantFile[EnPassant & 7];
            }

            HalfmoveClock = Piece.TypeOf(piece) == Piece.Pawn || undo.Captured != 0 ? 0 : HalfmoveClock + 1;
            if (side == Side.Black) FullmoveNumber++;
            SideToMove = -side;
            Hash ^= Zobrist.BlackToMove;
            undoStack.Add(undo);
        }

        public void UnmakeMove()
        {
            int last = undoStack.Count - 1;
            var undo = undoStack[last];
            undoStack.RemoveAt(last);
            var move = undo.Move;
            SideToMove = -SideToMove;
            int side = SideToMove;
            if (side == Side.Black) FullmoveNumber--;

            sbyte placed = Squares[move.To];
            sbyte piece = move.Promotion != 0 ? (sbyte)(Piece.Pawn * side) : placed;
            Squares[move.From] = piece;
            if (move.IsEnPassant)
            {
                Squares[move.To] = 0;
                Squares[move.To - 8 * side] = undo.Captured;
            }
            else
            {
                Squares[move.To] = undo.Captured;
            }

            if (move.IsCastle)
            {
                int rookFrom = move.To > move.From ? move.From + 3 : move.From - 4;
                int rookTo = move.To > move.From ? move.From + 1 : move.From - 1;
                Squares[rookFrom] = Squares[rookTo];
                Squares[rookTo] = 0;
            }

            if (piece == Piece.King) whiteKing = move.From;
            else if (piece == -Piece.King) blackKing = move.From;

            Castling = undo.Castling;
            EnPassant = undo.EnPassant;
            HalfmoveClock = undo.Halfmove;
            Hash = undo.Hash;
        }

        public int UndoDepth => undoStack.Count;

        /// <summary>Pseudo-legal moves (may leave the king in check). Captures-only also keeps queen promotions.</summary>
        public void GenerateMoves(List<ChessMove> moves, bool capturesOnly = false)
        {
            int side = SideToMove;
            for (int sq = 0; sq < 64; sq++)
            {
                int p = Squares[sq];
                if (p == 0 || (p > 0) != (side > 0)) continue;
                switch (Piece.TypeOf(p))
                {
                    case Piece.Pawn: GeneratePawnMoves(sq, side, moves, capturesOnly); break;
                    case Piece.Knight: GenerateLeaps(sq, KnightTargets[sq], side, moves, capturesOnly); break;
                    case Piece.Bishop: GenerateSlides(sq, 4, 8, side, moves, capturesOnly); break;
                    case Piece.Rook: GenerateSlides(sq, 0, 4, side, moves, capturesOnly); break;
                    case Piece.Queen: GenerateSlides(sq, 0, 8, side, moves, capturesOnly); break;
                    case Piece.King:
                        GenerateLeaps(sq, KingTargets[sq], side, moves, capturesOnly);
                        if (!capturesOnly) GenerateCastles(sq, side, moves);
                        break;
                }
            }
        }

        public void GenerateLegalMoves(List<ChessMove> moves)
        {
            moves.Clear();
            scratch.Clear();
            GenerateMoves(scratch);
            int side = SideToMove;
            foreach (var move in scratch)
            {
                MakeMove(move);
                if (!IsSquareAttacked(KingSquare(side), -side)) moves.Add(move);
                UnmakeMove();
            }
        }

        /// <summary>Returns the legal move matching from/to/promotion, if any.</summary>
        public bool TryFindLegalMove(int from, int to, int promotion, out ChessMove found)
        {
            var legal = new List<ChessMove>();
            GenerateLegalMoves(legal);
            foreach (var move in legal)
            {
                if (move.From == from && move.To == to && (move.Promotion == promotion || (promotion == 0 && move.Promotion == Piece.Queen)))
                {
                    found = move;
                    return true;
                }
            }
            found = default;
            return false;
        }

        public bool IsSquareAttacked(int sq, int bySide)
        {
            int file = sq & 7;
            if (bySide == Side.White)
            {
                if (file > 0 && sq >= 9 && Squares[sq - 9] == Piece.Pawn) return true;
                if (file < 7 && sq >= 7 && Squares[sq - 7] == Piece.Pawn) return true;
            }
            else
            {
                if (file > 0 && sq + 7 < 64 && Squares[sq + 7] == -Piece.Pawn) return true;
                if (file < 7 && sq + 9 < 64 && Squares[sq + 9] == -Piece.Pawn) return true;
            }

            int knight = Piece.Knight * bySide;
            foreach (int t in KnightTargets[sq])
                if (Squares[t] == knight) return true;
            int king = Piece.King * bySide;
            foreach (int t in KingTargets[sq])
                if (Squares[t] == king) return true;

            int rook = Piece.Rook * bySide, bishop = Piece.Bishop * bySide, queen = Piece.Queen * bySide;
            for (int d = 0; d < 8; d++)
            {
                int offset = DirectionOffsets[d];
                int reach = SquaresToEdge[sq, d];
                for (int n = 1; n <= reach; n++)
                {
                    int p = Squares[sq + offset * n];
                    if (p == 0) continue;
                    if (p == queen || (d < 4 ? p == rook : p == bishop)) return true;
                    break;
                }
            }
            return false;
        }

        /// <summary>True if <paramref name="capturingSide"/> has a pawn beside the pawn that just double-pushed to <paramref name="pawnSquare"/>.</summary>
        bool CanCaptureEnPassant(int pawnSquare, int capturingSide)
        {
            if (pawnSquare < 0 || pawnSquare > 63) return false;
            int file = pawnSquare & 7;
            int pawn = Piece.Pawn * capturingSide;
            return (file > 0 && Squares[pawnSquare - 1] == pawn) || (file < 7 && Squares[pawnSquare + 1] == pawn);
        }

        void GeneratePawnMoves(int sq, int side, List<ChessMove> moves, bool capturesOnly)
        {
            int rank = sq >> 3, file = sq & 7;
            int forward = 8 * side;
            bool promotes = rank == (side == Side.White ? 6 : 1);
            int one = sq + forward;

            if (Squares[one] == 0 && (!capturesOnly || promotes))
            {
                AddPawnMove(sq, one, 0, promotes, moves, capturesOnly);
                if (!capturesOnly && rank == (side == Side.White ? 1 : 6) && Squares[one + forward] == 0)
                    moves.Add(new ChessMove(sq, one + forward, 0, ChessMove.FlagDoublePush));
            }

            for (int df = -1; df <= 1; df += 2)
            {
                int f = file + df;
                if (f < 0 || f > 7) continue;
                int target = one + df;
                int victim = Squares[target];
                if (victim != 0 && (victim > 0) != (side > 0)) AddPawnMove(sq, target, ChessMove.FlagCapture, promotes, moves, capturesOnly);
                else if (victim == 0 && target == EnPassant) moves.Add(new ChessMove(sq, target, 0, ChessMove.FlagCapture | ChessMove.FlagEnPassant));
            }
        }

        static void AddPawnMove(int from, int to, int flags, bool promotes, List<ChessMove> moves, bool queenOnly)
        {
            if (!promotes)
            {
                moves.Add(new ChessMove(from, to, 0, flags));
                return;
            }
            moves.Add(new ChessMove(from, to, Piece.Queen, flags));
            if (queenOnly) return;
            moves.Add(new ChessMove(from, to, Piece.Knight, flags));
            moves.Add(new ChessMove(from, to, Piece.Rook, flags));
            moves.Add(new ChessMove(from, to, Piece.Bishop, flags));
        }

        void GenerateLeaps(int sq, int[] targets, int side, List<ChessMove> moves, bool capturesOnly)
        {
            foreach (int target in targets)
            {
                int p = Squares[target];
                if (p == 0)
                {
                    if (!capturesOnly) moves.Add(new ChessMove(sq, target, 0, 0));
                }
                else if ((p > 0) != (side > 0))
                {
                    moves.Add(new ChessMove(sq, target, 0, ChessMove.FlagCapture));
                }
            }
        }

        void GenerateSlides(int sq, int firstDirection, int endDirection, int side, List<ChessMove> moves, bool capturesOnly)
        {
            for (int d = firstDirection; d < endDirection; d++)
            {
                int offset = DirectionOffsets[d];
                int reach = SquaresToEdge[sq, d];
                for (int n = 1; n <= reach; n++)
                {
                    int target = sq + offset * n;
                    int p = Squares[target];
                    if (p == 0)
                    {
                        if (!capturesOnly) moves.Add(new ChessMove(sq, target, 0, 0));
                        continue;
                    }
                    if ((p > 0) != (side > 0)) moves.Add(new ChessMove(sq, target, 0, ChessMove.FlagCapture));
                    break;
                }
            }
        }

        void GenerateCastles(int kingSquare, int side, List<ChessMove> moves)
        {
            int home = side == Side.White ? 4 : 60;
            if (kingSquare != home) return;
            int kingSideRight = side == Side.White ? CastleWhiteKing : CastleBlackKing;
            int queenSideRight = side == Side.White ? CastleWhiteQueen : CastleBlackQueen;
            int rook = Piece.Rook * side;
            int enemy = -side;

            if ((Castling & kingSideRight) != 0 && Squares[home + 1] == 0 && Squares[home + 2] == 0 && Squares[home + 3] == rook &&
                !IsSquareAttacked(home, enemy) && !IsSquareAttacked(home + 1, enemy) && !IsSquareAttacked(home + 2, enemy))
                moves.Add(new ChessMove(home, home + 2, 0, ChessMove.FlagCastle));

            if ((Castling & queenSideRight) != 0 && Squares[home - 1] == 0 && Squares[home - 2] == 0 && Squares[home - 3] == 0 && Squares[home - 4] == rook &&
                !IsSquareAttacked(home, enemy) && !IsSquareAttacked(home - 1, enemy) && !IsSquareAttacked(home - 2, enemy))
                moves.Add(new ChessMove(home, home - 2, 0, ChessMove.FlagCastle));
        }

        /// <summary>Hash computed from scratch (the incremental <see cref="Hash"/> must always equal this).</summary>
        public ulong ComputeHash()
        {
            ulong hash = 0;
            for (int sq = 0; sq < 64; sq++)
                if (Squares[sq] != 0) hash ^= Zobrist.Key(Squares[sq], sq);
            hash ^= Zobrist.Castling[Castling];
            if (EnPassant >= 0) hash ^= Zobrist.EnPassantFile[EnPassant & 7];
            if (SideToMove == Side.Black) hash ^= Zobrist.BlackToMove;
            return hash;
        }

        /// <summary>Counts leaf nodes of the legal move tree (move generator self-test).</summary>
        public long Perft(int depth)
        {
            if (depth == 0) return 1;
            var moves = new List<ChessMove>(64);
            GenerateMoves(moves);
            long nodes = 0;
            int side = SideToMove;
            foreach (var move in moves)
            {
                MakeMove(move);
                if (!IsSquareAttacked(KingSquare(side), -side)) nodes += Perft(depth - 1);
                UnmakeMove();
            }
            return nodes;
        }

        /// <summary>Material check for dead positions: K v K, K+minor v K, K+B v K+B with same-coloured bishops.</summary>
        public bool IsInsufficientMaterial()
        {
            int whiteMinor = 0, blackMinor = 0, whiteBishopColor = -1, blackBishopColor = -1;
            bool bishopsOnMixedColors = false;
            for (int sq = 0; sq < 64; sq++)
            {
                int p = Squares[sq];
                int type = Piece.TypeOf(p);
                if (type == Piece.None || type == Piece.King) continue;
                if (type == Piece.Pawn || type == Piece.Rook || type == Piece.Queen) return false;
                int color = ((sq >> 3) + (sq & 7)) & 1;
                if (p > 0)
                {
                    whiteMinor++;
                    if (type == Piece.Bishop)
                    {
                        if (whiteBishopColor >= 0 && whiteBishopColor != color) bishopsOnMixedColors = true;
                        whiteBishopColor = color;
                    }
                    else whiteBishopColor = -2;
                }
                else
                {
                    blackMinor++;
                    if (type == Piece.Bishop)
                    {
                        if (blackBishopColor >= 0 && blackBishopColor != color) bishopsOnMixedColors = true;
                        blackBishopColor = color;
                    }
                    else blackBishopColor = -2;
                }
            }
            if (whiteMinor == 0 && blackMinor == 0) return true;
            if (whiteMinor + blackMinor == 1) return true;
            if (whiteMinor == 1 && blackMinor == 1 && !bishopsOnMixedColors && whiteBishopColor >= 0 && blackBishopColor >= 0)
                return whiteBishopColor == blackBishopColor;
            return false;
        }
    }

    static class Zobrist
    {
        static readonly ulong[] PieceSquare = new ulong[13 * 64];
        public static readonly ulong[] Castling = new ulong[16];
        public static readonly ulong[] EnPassantFile = new ulong[8];
        public static readonly ulong BlackToMove;

        static Zobrist()
        {
            ulong state = 0x9E3779B97F4A7C15UL;
            for (int i = 0; i < PieceSquare.Length; i++) PieceSquare[i] = Next(ref state);
            for (int i = 0; i < Castling.Length; i++) Castling[i] = Next(ref state);
            for (int i = 0; i < EnPassantFile.Length; i++) EnPassantFile[i] = Next(ref state);
            BlackToMove = Next(ref state);
        }

        public static ulong Key(int piece, int square) => PieceSquare[(piece + 6) * 64 + square];

        static ulong Next(ref ulong state)
        {
            // xorshift64*
            state ^= state >> 12;
            state ^= state << 25;
            state ^= state >> 27;
            return state * 0x2545F4914F6CDD1DUL;
        }
    }
}
