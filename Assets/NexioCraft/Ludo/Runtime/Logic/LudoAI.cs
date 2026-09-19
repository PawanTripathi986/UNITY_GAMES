using System;
using System.Collections.Generic;

namespace NexioCraft.Ludo
{
    /// <summary>
    /// Computer opponent. Scores every legal move with Ludo heuristics (capture, reach home, escape danger,
    /// avoid danger, leave the yard) and picks the best; lower levels add randomness.
    /// </summary>
    public static class LudoAI
    {
        public static LudoMove Choose(LudoState state, List<LudoMove> moves, Random rng)
        {
            if (moves.Count == 1) return moves[0];
            var level = state.Level(state.current);
            if (level == BotLevel.Easy && rng.NextDouble() < 0.4) return moves[rng.Next(moves.Count)];

            float noise = level switch { BotLevel.Easy => 35f, BotLevel.Normal => 8f, _ => 0.5f };
            int best = 0;
            float bestScore = float.MinValue;
            for (int i = 0; i < moves.Count; i++)
            {
                float score = Score(state, moves[i], level == BotLevel.Hard) + (float)(rng.NextDouble() * 2.0 - 1.0) * noise;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return moves[best];
        }

        static float Score(LudoState state, LudoMove move, bool careful)
        {
            var rules = state.rules;
            int seat = move.Seat;
            float score = 0f;

            if (move.From == LudoEngine.Yard)
                score += 58f + (LudoState.TokensPerSeat - TokensInPlay(state, seat)) * 4f;

            if (move.To == LudoEngine.Home) score += 90f;
            else if (move.From <= LudoEngine.LastTrack && move.To > LudoEngine.LastTrack) score += 46f;

            int toSquare = LudoEngine.Absolute(seat, move.To);
            if (toSquare >= 0)
            {
                bool safe = LudoEngine.IsProtected(rules, toSquare);
                if (!safe)
                {
                    int victims = 0;
                    float victimProgress = 0f;
                    ForEachOpponentToken(state, seat, (other, progress) =>
                    {
                        if (LudoEngine.Absolute(other, progress) != toSquare) return;
                        victims++;
                        victimProgress += progress;
                    });
                    if (victims > 0) score += 105f + victimProgress * 1.5f;
                }
                else
                {
                    score += 16f;
                }

                float danger = safe ? 0f : Threat(state, seat, toSquare, careful);
                score -= danger * (42f + move.To);
                score += Targets(state, seat, toSquare, move.To) * (careful ? 6f : 3f);
            }

            int fromSquare = LudoEngine.Absolute(seat, move.From);
            if (fromSquare >= 0)
            {
                if (LudoEngine.IsProtected(rules, fromSquare)) score -= 6f;
                else score += Threat(state, seat, fromSquare, careful) * (36f + move.From * 0.9f);
            }

            score += (move.To - Math.Max(move.From, 0)) * 0.4f + move.To * 0.08f;
            return score;
        }

        /// <summary>Rough probability that an opponent can land on <paramref name="square"/> next round.</summary>
        static float Threat(LudoState state, int seat, int square, bool careful)
        {
            float threat = 0f;
            var rules = state.rules;
            for (int other = 0; other < LudoState.SeatCount; other++)
            {
                if (other == seat || !state.IsActive(other) || state.HasFinished(other)) continue;
                for (int t = 0; t < LudoState.TokensPerSeat; t++)
                {
                    int progress = state.Token(other, t);
                    if (progress >= 0 && progress <= LudoEngine.LastTrack)
                    {
                        int distance = (square - LudoEngine.Absolute(other, progress) + LudoEngine.TrackLength) % LudoEngine.TrackLength;
                        if (distance == 0 || progress + distance > LudoEngine.LastTrack) continue;
                        if (distance <= 6) threat += 1f / 6f;
                        else if (careful && distance <= 12 && rules.extraTurnOnSix) threat += 1f / 36f;
                    }
                    else if (careful && progress == LudoEngine.Yard)
                    {
                        int distance = (square - LudoEngine.StartIndex[other] + LudoEngine.TrackLength) % LudoEngine.TrackLength;
                        if (distance >= 1 && distance <= 6) threat += 1f / 36f;
                        else if (distance == 0 && !rules.safeSquares) threat += 1f / 6f;
                    }
                }
            }
            return Math.Min(threat, 1f);
        }

        /// <summary>Opponent tokens this token could capture next turn from <paramref name="square"/>.</summary>
        static int Targets(LudoState state, int seat, int square, int progress)
        {
            int count = 0;
            ForEachOpponentToken(state, seat, (other, otherProgress) =>
            {
                int target = LudoEngine.Absolute(other, otherProgress);
                if (LudoEngine.IsProtected(state.rules, target)) return;
                int distance = (target - square + LudoEngine.TrackLength) % LudoEngine.TrackLength;
                if (distance >= 1 && distance <= 6 && progress + distance <= LudoEngine.LastTrack) count++;
            });
            return count;
        }

        static int TokensInPlay(LudoState state, int seat)
        {
            int count = 0;
            for (int t = 0; t < LudoState.TokensPerSeat; t++)
            {
                int p = state.Token(seat, t);
                if (p != LudoEngine.Yard && p != LudoEngine.Home) count++;
            }
            return count;
        }

        static void ForEachOpponentToken(LudoState state, int seat, Action<int, int> visit)
        {
            for (int other = 0; other < LudoState.SeatCount; other++)
            {
                if (other == seat || !state.IsActive(other)) continue;
                for (int t = 0; t < LudoState.TokensPerSeat; t++)
                {
                    int progress = state.Token(other, t);
                    if (progress >= 0 && progress <= LudoEngine.LastTrack) visit(other, progress);
                }
            }
        }
    }
}
