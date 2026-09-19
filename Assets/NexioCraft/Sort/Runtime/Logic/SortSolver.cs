using System.Collections.Generic;

namespace NexioCraft.Sort
{
    /// <summary>
    /// Solves a tube puzzle. Used to guarantee every generated level can be finished, and to give hints.
    /// Depth-first with a visited set and move ordering that tries the obviously good pours first.
    /// </summary>
    public static class SortSolver
    {
        public const int DefaultBudget = 120000;
        const int MaxDepth = 400;

        /// <summary>Finds a sequence of pours that finishes the puzzle.</summary>
        public static bool TrySolve(SortBoard start, out List<SortMove> solution, int nodeBudget = DefaultBudget)
        {
            solution = new List<SortMove>();
            var visited = new HashSet<string> { start.Key() };
            int nodes = 0;
            return Search(start.Clone(), visited, solution, ref nodes, nodeBudget, 0);
        }

        /// <summary>The next move of a solution from here, for the hint button.</summary>
        public static bool TryHint(SortBoard board, out SortMove hint, int nodeBudget = DefaultBudget)
        {
            if (TrySolve(board, out var solution, nodeBudget) && solution.Count > 0)
            {
                hint = solution[0];
                return true;
            }
            hint = default;
            return false;
        }

        static bool Search(SortBoard board, HashSet<string> visited, List<SortMove> path, ref int nodes, int budget, int depth)
        {
            if (board.Solved) return true;
            if (depth >= MaxDepth || nodes >= budget) return false;
            nodes++;

            foreach (var move in Candidates(board))
            {
                var next = board.Clone();
                if (next.Pour(move.From, move.To) == 0) continue;
                if (!visited.Add(next.Key())) continue;
                path.Add(move);
                if (Search(next, visited, path, ref nodes, budget, depth + 1)) return true;
                path.RemoveAt(path.Count - 1);
            }
            return false;
        }

        /// <summary>Useful pours, best first: finish a tube, empty a tube, keep runs whole, and only then use an empty tube.</summary>
        static List<SortMove> Candidates(SortBoard board)
        {
            var moves = new List<SortMove>();
            var scores = new List<int>();
            for (int from = 0; from < board.TubeCount; from++)
            {
                if (board.IsEmpty(from)) continue;
                int run = board.TopRun(from);
                for (int to = 0; to < board.TubeCount; to++)
                {
                    if (!board.IsUseful(from, to)) continue;
                    int space = SortBoard.Capacity - board.Height(to);
                    int moved = run < space ? run : space;
                    int score = 0;
                    if (board.Height(to) + moved == SortBoard.Capacity && board.Top(to) == board.Top(from)) score += 100;
                    if (moved == board.Height(from)) score += 60;
                    if (moved == run) score += 25;
                    if (board.IsEmpty(to)) score -= 30;
                    score += moved;
                    moves.Add(new SortMove(from, to, moved));
                    scores.Add(score);
                }
            }
            // Simple insertion sort: the list is short (a few dozen at most).
            for (int i = 1; i < moves.Count; i++)
            {
                var move = moves[i];
                int score = scores[i];
                int j = i - 1;
                while (j >= 0 && scores[j] < score)
                {
                    moves[j + 1] = moves[j];
                    scores[j + 1] = scores[j];
                    j--;
                }
                moves[j + 1] = move;
                scores[j + 1] = score;
            }
            return moves;
        }
    }
}
