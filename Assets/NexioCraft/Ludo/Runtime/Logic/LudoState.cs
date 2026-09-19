using System;

namespace NexioCraft.Ludo
{
    public enum SeatKind { Off = 0, Human = 1, Bot = 2 }

    public enum BotLevel { Easy = 0, Normal = 1, Hard = 2 }

    public enum LudoMode { VsComputer = 0, PassAndPlay = 1 }

    /// <summary>House rules. Serialized with each saved game so a resumed game keeps its rules.</summary>
    [Serializable]
    public sealed class LudoRules
    {
        /// <summary>A 6 is needed to bring a token out; when false a 1 also works.</summary>
        public bool sixToStart = true;
        public bool extraTurnOnSix = true;
        public bool threeSixesForfeit = true;
        public bool extraTurnOnCapture = true;
        public bool extraTurnOnHome = true;
        /// <summary>Start squares and star squares protect tokens from capture.</summary>
        public bool safeSquares = true;
        /// <summary>Stop once every human has finished and rank the remaining computers by progress.</summary>
        public bool endWhenHumansFinish = true;

        public LudoRules Clone() => (LudoRules)MemberwiseClone();
    }

    /// <summary>
    /// Complete game state as plain data (JsonUtility-friendly). Seats are clockwise from the
    /// bottom-left yard: 0 Red, 1 Green, 2 Yellow, 3 Blue.
    /// Token progress: -1 yard, 0..50 main track (0 = own start square), 51..55 home column, 56 home.
    /// </summary>
    [Serializable]
    public sealed class LudoState
    {
        public const int SeatCount = 4;
        public const int TokensPerSeat = 4;

        public int mode;
        public int[] seatKinds = new int[SeatCount];
        public int[] botLevels = new int[SeatCount];
        public int[] tokens = new int[SeatCount * TokensPerSeat];
        public int[] finishOrder = { -1, -1, -1, -1 };
        public int finishedCount;
        public int current;
        public int consecutiveSixes;
        public int lastRoll;
        /// <summary>A roll that has been made but not yet used (so a resumed game cannot re-roll it).</summary>
        public int pendingRoll;
        public int moves;
        public bool over;
        public LudoRules rules = new LudoRules();

        public LudoMode Mode => (LudoMode)mode;
        public SeatKind Kind(int seat) => (SeatKind)seatKinds[seat];
        public BotLevel Level(int seat) => (BotLevel)botLevels[seat];
        public bool IsActive(int seat) => seatKinds[seat] != (int)SeatKind.Off;
        public int Token(int seat, int token) => tokens[seat * TokensPerSeat + token];
        public void SetToken(int seat, int token, int progress) => tokens[seat * TokensPerSeat + token] = progress;

        public bool HasFinished(int seat)
        {
            for (int i = 0; i < finishedCount; i++)
                if (finishOrder[i] == seat) return true;
            return false;
        }

        /// <summary>0-based finishing position, or -1 while still playing.</summary>
        public int RankOf(int seat)
        {
            for (int i = 0; i < finishedCount; i++)
                if (finishOrder[i] == seat) return i;
            return -1;
        }

        public int TokensHome(int seat)
        {
            int count = 0;
            for (int t = 0; t < TokensPerSeat; t++)
                if (Token(seat, t) == LudoEngine.Home) count++;
            return count;
        }

        public int ActiveSeatCount
        {
            get
            {
                int count = 0;
                for (int s = 0; s < SeatCount; s++)
                    if (IsActive(s)) count++;
                return count;
            }
        }

        public LudoState Clone()
        {
            var copy = (LudoState)MemberwiseClone();
            copy.seatKinds = (int[])seatKinds.Clone();
            copy.botLevels = (int[])botLevels.Clone();
            copy.tokens = (int[])tokens.Clone();
            copy.finishOrder = (int[])finishOrder.Clone();
            copy.rules = rules.Clone();
            return copy;
        }

        /// <summary>Guards against corrupt or outdated saves.</summary>
        public bool IsValid()
        {
            if (seatKinds == null || seatKinds.Length != SeatCount) return false;
            if (botLevels == null || botLevels.Length != SeatCount) return false;
            if (tokens == null || tokens.Length != SeatCount * TokensPerSeat) return false;
            if (finishOrder == null || finishOrder.Length != SeatCount) return false;
            if (rules == null || current < 0 || current >= SeatCount || !IsActive(current)) return false;
            if (ActiveSeatCount < 2 || finishedCount < 0 || finishedCount > SeatCount) return false;
            if (pendingRoll < 0 || pendingRoll > 6) return false;
            foreach (int p in tokens)
                if (p < LudoEngine.Yard || p > LudoEngine.Home) return false;
            return true;
        }
    }
}
