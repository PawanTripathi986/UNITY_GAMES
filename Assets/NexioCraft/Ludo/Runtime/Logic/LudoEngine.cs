using System.Collections.Generic;

namespace NexioCraft.Ludo
{
    public readonly struct LudoMove
    {
        public readonly int Seat;
        public readonly int Token;
        public readonly int From;
        public readonly int To;

        public LudoMove(int seat, int token, int from, int to)
        {
            Seat = seat;
            Token = token;
            From = from;
            To = to;
        }

        public override string ToString() => $"seat {Seat} token {Token}: {From} -> {To}";
    }

    public readonly struct LudoCapture
    {
        public readonly int Seat;
        public readonly int Token;
        public readonly int From;

        public LudoCapture(int seat, int token, int from)
        {
            Seat = seat;
            Token = token;
            From = from;
        }
    }

    public sealed class LudoMoveResult
    {
        public LudoMove Move;
        public readonly List<LudoCapture> Captures = new List<LudoCapture>();
        public bool ReachedHome;
        public bool SeatFinished;
        public int FinishRank = -1;
        public bool ExtraTurn;
        public bool GameOver;
    }

    public enum RollOutcome { ChooseMove, NoMoves, ThreeSixes }

    /// <summary>Pure Ludo rules. The view layer only animates what these methods decide.</summary>
    public static class LudoEngine
    {
        public const int Yard = -1;
        public const int LastTrack = 50;
        public const int Home = 56;
        public const int TrackLength = 52;

        /// <summary>Main-track index of each seat's start square.</summary>
        public static readonly int[] StartIndex = { 0, 13, 26, 39 };

        public static LudoState NewGame(SeatKind[] kinds, BotLevel[] levels, LudoRules rules, LudoMode mode, int firstSeat)
        {
            var state = new LudoState { mode = (int)mode, rules = rules.Clone() };
            for (int s = 0; s < LudoState.SeatCount; s++)
            {
                state.seatKinds[s] = (int)kinds[s];
                state.botLevels[s] = (int)levels[s];
            }
            for (int i = 0; i < state.tokens.Length; i++) state.tokens[i] = Yard;
            state.current = state.IsActive(firstSeat) ? firstSeat : NextActiveSeat(state, firstSeat);
            return state;
        }

        /// <summary>Main-track square (0..51) of a token, or -1 when it is not on the shared track.</summary>
        public static int Absolute(int seat, int progress) =>
            progress >= 0 && progress <= LastTrack ? (StartIndex[seat] + progress) % TrackLength : -1;

        /// <summary>Start squares and the star squares eight steps after them.</summary>
        public static bool IsSafeSquare(int absolute) => absolute >= 0 && (absolute % 13 == 0 || absolute % 13 == 8);

        public static bool IsProtected(LudoRules rules, int absolute) => rules.safeSquares && IsSafeSquare(absolute);

        /// <summary>Registers a die roll for the current seat and fills <paramref name="moves"/> with the legal moves.</summary>
        public static RollOutcome Roll(LudoState state, int value, List<LudoMove> moves)
        {
            moves.Clear();
            state.lastRoll = value;
            state.pendingRoll = 0;
            state.consecutiveSixes = value == 6 ? state.consecutiveSixes + 1 : 0;

            if (value == 6 && state.rules.threeSixesForfeit && state.consecutiveSixes >= 3)
            {
                AdvanceTurn(state);
                return RollOutcome.ThreeSixes;
            }

            CollectMoves(state, state.current, value, moves);
            if (moves.Count > 0)
            {
                state.pendingRoll = value;
                return RollOutcome.ChooseMove;
            }

            // A six still earns another roll even when it cannot be used.
            if (!(value == 6 && state.rules.extraTurnOnSix)) AdvanceTurn(state);
            return RollOutcome.NoMoves;
        }

        public static void CollectMoves(LudoState state, int seat, int roll, List<LudoMove> moves)
        {
            for (int t = 0; t < LudoState.TokensPerSeat; t++)
            {
                int from = state.Token(seat, t);
                if (from == Home) continue;
                if (from == Yard)
                {
                    if (roll == 6 || (!state.rules.sixToStart && roll == 1)) moves.Add(new LudoMove(seat, t, Yard, 0));
                    continue;
                }
                int to = from + roll;
                if (to <= Home) moves.Add(new LudoMove(seat, t, from, to));
            }
        }

        /// <summary>True when every move ends in the same place (e.g. several tokens waiting in the yard).</summary>
        public static bool AllEquivalent(List<LudoMove> moves)
        {
            for (int i = 1; i < moves.Count; i++)
                if (moves[i].From != moves[0].From) return false;
            return moves.Count > 0;
        }

        /// <summary>Applies a move produced by <see cref="Roll"/> and advances the turn as the rules require.</summary>
        public static LudoMoveResult Apply(LudoState state, LudoMove move)
        {
            var result = new LudoMoveResult { Move = move };
            state.SetToken(move.Seat, move.Token, move.To);
            state.pendingRoll = 0;
            state.moves++;

            int square = Absolute(move.Seat, move.To);
            if (square >= 0 && !IsProtected(state.rules, square))
            {
                for (int other = 0; other < LudoState.SeatCount; other++)
                {
                    if (other == move.Seat || !state.IsActive(other)) continue;
                    for (int t = 0; t < LudoState.TokensPerSeat; t++)
                    {
                        int progress = state.Token(other, t);
                        if (Absolute(other, progress) != square) continue;
                        result.Captures.Add(new LudoCapture(other, t, progress));
                        state.SetToken(other, t, Yard);
                    }
                }
            }

            result.ReachedHome = move.To == Home;
            bool extra = (state.lastRoll == 6 && state.rules.extraTurnOnSix)
                         || (result.Captures.Count > 0 && state.rules.extraTurnOnCapture)
                         || (result.ReachedHome && state.rules.extraTurnOnHome);

            if (result.ReachedHome && state.TokensHome(move.Seat) == LudoState.TokensPerSeat)
            {
                result.SeatFinished = true;
                result.FinishRank = state.finishedCount;
                state.finishOrder[state.finishedCount++] = move.Seat;
                extra = false;
            }

            result.GameOver = CheckGameOver(state);
            if (!result.GameOver && !extra) AdvanceTurn(state);
            result.ExtraTurn = extra && !result.GameOver;
            return result;
        }

        public static void AdvanceTurn(LudoState state)
        {
            state.consecutiveSixes = 0;
            state.current = NextActiveSeat(state, state.current);
        }

        /// <summary>Total steps travelled by a seat's tokens; used to rank unfinished players.</summary>
        public static int Progress(LudoState state, int seat)
        {
            int total = 0;
            for (int t = 0; t < LudoState.TokensPerSeat; t++) total += state.Token(seat, t) + 1;
            return total;
        }

        static int NextActiveSeat(LudoState state, int from)
        {
            for (int i = 1; i <= LudoState.SeatCount; i++)
            {
                int seat = (from + i) % LudoState.SeatCount;
                if (state.IsActive(seat) && !state.HasFinished(seat)) return seat;
            }
            return from;
        }

        static bool CheckGameOver(LudoState state)
        {
            if (state.over) return true;
            int remaining = 0, remainingHumans = 0, humans = 0;
            for (int seat = 0; seat < LudoState.SeatCount; seat++)
            {
                if (!state.IsActive(seat)) continue;
                bool human = state.Kind(seat) == SeatKind.Human;
                if (human) humans++;
                if (state.HasFinished(seat)) continue;
                remaining++;
                if (human) remainingHumans++;
            }

            bool over = remaining <= 1 || (state.rules.endWhenHumansFinish && humans > 0 && remainingHumans == 0);
            if (!over) return false;

            var left = new List<int>();
            for (int seat = 0; seat < LudoState.SeatCount; seat++)
                if (state.IsActive(seat) && !state.HasFinished(seat)) left.Add(seat);
            left.Sort((a, b) =>
            {
                int byProgress = Progress(state, b).CompareTo(Progress(state, a));
                return byProgress != 0 ? byProgress : a.CompareTo(b);
            });
            foreach (int seat in left) state.finishOrder[state.finishedCount++] = seat;
            state.over = true;
            return true;
        }
    }
}
