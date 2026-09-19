using System.Collections;
using NexioCraft.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NexioCraft.Brain
{
    /// <summary>Slide the numbered tiles into order. Shuffled with legal moves, so it is always solvable.</summary>
    public sealed class SlidingPuzzleScreen : BrainGameScreen
    {
        static readonly int[] Sizes = { 3, 4, 5 };

        protected override string Title => "Sliding Puzzle";
        protected override float BottomInset => 150f;

        int variant;
        SlidingPuzzle puzzle;
        RectTransform[] tiles;
        Text[] labels;
        float elapsed;
        bool finished;
        bool animating;
        float cellSize;
        Vector2 boardOrigin;
        readonly System.Random rng = new System.Random();

        public static string VariantName(int variant)
        {
            int size = Sizes[Mathf.Clamp(variant, 0, 2)];
            return size + "x" + size;
        }

        protected override void BuildGame()
        {
            variant = Mathf.Clamp(BrainPrefs.GetChoice("slide", 1), 0, 2);
            var picker = SegmentedControl.Create(SafeRoot, new[] { VariantName(0), VariantName(1), VariantName(2) }, variant, BrainArt.Accent,
                new Vector2(600f, 104f), index =>
                {
                    variant = index;
                    BrainPrefs.SetChoice("slide", index);
                    NewGame();
                });
            UIFactory.Place((RectTransform)picker.transform, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(600f, 104f));
            NewGame();
        }

        protected override void Restart() => NewGame();

        void NewGame()
        {
            StopAllCoroutines();
            animating = false;
            finished = false;
            elapsed = 0f;
            int size = Sizes[variant];
            puzzle = new SlidingPuzzle(size);
            puzzle.Shuffle(rng);

            for (int i = Content.childCount - 1; i >= 0; i--) Destroy(Content.GetChild(i).gameObject);
            var board = UIFactory.AddPanel(Content, "Board", Palette.Inset, 36f);
            UIFactory.Stretch(board.rectTransform);

            int count = size * size;
            tiles = new RectTransform[count];
            labels = new Text[count];
            for (int number = 1; number < count; number++)
            {
                int value = number;
                var tile = UIFactory.AddPanel(Content, "Tile" + number, BrainArt.Accent, 26f, true);
                labels[number] = UIFactory.AddLabel(tile.rectTransform, "Number", number.ToString(), 70f, Color.white, FontWeight.ExtraBold);
                UIFactory.Stretch(labels[number].rectTransform);
                UIFactory.MakeButton(tile.gameObject, () => TapTile(value));
                tiles[number] = tile.rectTransform;
            }
            LayoutGame(ContentSize);
            UpdateStats();
        }

        protected override void LayoutGame(Vector2 size)
        {
            if (puzzle == null || size.x <= 0f) return;
            int grid = puzzle.Size;
            float board = Mathf.Min(size.x, size.y);
            const float padding = 16f;
            cellSize = (board - padding * (grid + 1)) / grid;
            boardOrigin = new Vector2(-board * 0.5f + padding + cellSize * 0.5f, board * 0.5f - padding - cellSize * 0.5f);
            for (int number = 1; number < tiles.Length; number++)
            {
                if (tiles[number] == null) continue;
                tiles[number].sizeDelta = new Vector2(cellSize, cellSize);
                labels[number].fontSize = Mathf.RoundToInt(cellSize * 0.42f);
                UIFactory.Place(tiles[number], new Vector2(0.5f, 0.5f), CellPosition(CellOf(number)), new Vector2(cellSize, cellSize));
            }
        }

        int CellOf(int number)
        {
            for (int i = 0; i < puzzle.Cells.Length; i++)
                if (puzzle.Cells[i] == number) return i;
            return -1;
        }

        Vector2 CellPosition(int cell)
        {
            int grid = puzzle.Size;
            int column = cell % grid, row = cell / grid;
            const float padding = 16f;
            return boardOrigin + new Vector2(column * (cellSize + padding), -row * (cellSize + padding));
        }

        void TapTile(int number)
        {
            if (animating || finished) return;
            int cell = CellOf(number);
            if (cell < 0 || !puzzle.CanSlide(cell)) return;
            int target = puzzle.Blank;
            puzzle.Slide(cell);
            StartCoroutine(SlideTile(number, target));
        }

        IEnumerator SlideTile(int number, int targetCell)
        {
            animating = true;
            App.Instance.Audio.Play(Sfx.PieceMove, 0.7f, 1.2f);
            Haptics.Play(HapticKind.Selection);
            var rt = tiles[number];
            Vector2 from = rt.anchoredPosition;
            Vector2 to = CellPosition(targetCell);
            yield return Tween.Run(0.11f, t => rt.anchoredPosition = Vector2.LerpUnclamped(from, to, t), Ease.OutQuad);
            animating = false;
            UpdateStats();
            if (!puzzle.IsSolved) yield break;

            finished = true;
            string variantName = VariantName(variant);
            bool best = BrainPrefs.SubmitLowScore("slide", puzzle.Moves, variantName);
            UpdateStats();
            ShowResult("Solved!", best
                ? $"New record: {puzzle.Moves} moves in {Mathf.RoundToInt(elapsed)}s"
                : $"{puzzle.Moves} moves in {Mathf.RoundToInt(elapsed)}s (best {BrainPrefs.BestLowScore("slide", variantName)})", true, NewGame);
        }

        void UpdateStats()
        {
            int best = BrainPrefs.BestLowScore("slide", VariantName(variant));
            SetStat(0, "Moves", puzzle.Moves.ToString());
            SetStat(1, "Time", Mathf.RoundToInt(elapsed) + "s");
            SetStat(2, "Best", best > 0 ? best + " moves" : "-");
        }

        protected override void Update()
        {
            base.Update();
            if (finished || puzzle == null) return;
            float previous = elapsed;
            elapsed += Time.unscaledDeltaTime;
            if (Mathf.FloorToInt(previous) != Mathf.FloorToInt(elapsed)) UpdateStats();
        }
    }
}
