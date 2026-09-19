using System;
using System.Collections;
using System.Collections.Generic;
using NexioCraft.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NexioCraft.Brain
{
    /// <summary>Swipe to slide and merge tiles. The board survives leaving the screen.</summary>
    public sealed class Game2048Screen : BrainGameScreen
    {
        [Serializable]
        class Save2048
        {
            public int[] cells;
            public int score;
            public bool won;
        }

        class TileView
        {
            public RectTransform Rect;
            public Image Panel;
            public Text Label;
            public int Value;
        }

        protected override string Title => "2048";
        protected override float BottomInset => 150f;

        Grid2048 grid;
        RectTransform board;
        readonly Dictionary<int, TileView> viewsByCell = new Dictionary<int, TileView>();
        RectTransform tileLayer;
        float cellSize;
        float boardSize;
        bool animating;
        bool announcedWin;
        bool usedContinue;
        Button undoButton;
        readonly System.Random rng = new System.Random();

        protected override void BuildGame()
        {
            var swipe = Content.gameObject.AddComponent<Image>();
            swipe.color = new Color(0f, 0f, 0f, 0f);
            Content.gameObject.AddComponent<SwipeArea>().Swiped = OnSwipe;

            board = UIFactory.AddRect("Board", Content);
            var back = UIFactory.AddPanel(board, "Back", Palette.Inset, 34f);
            UIFactory.Stretch(back.rectTransform);
            tileLayer = UIFactory.Stretch(UIFactory.AddRect("Tiles", board));

            undoButton = UIFactory.AddGameButton(SafeRoot, "Undo", Palette.CardRaised, new Vector2(300f, 116f), Undo, 46f, IconKind.Undo);
            UIFactory.Place((RectTransform)undoButton.transform, new Vector2(0.5f, 0f), new Vector2(0f, 54f), new Vector2(300f, 116f));

            Load();
        }

        protected override void Restart() => NewGame();

        void NewGame()
        {
            StopAllCoroutines();
            animating = false;
            announcedWin = false;
            usedContinue = false;
            grid = new Grid2048(rng);
            RebuildTiles();
            Persist();
            UpdateStats();
        }

        void Load()
        {
            string json = BrainPrefs.SaveData("2048");
            grid = new Grid2048(rng, false);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var save = JsonUtility.FromJson<Save2048>(json);
                    if (save?.cells != null && save.cells.Length == Grid2048.Size * Grid2048.Size)
                    {
                        grid.Load(save.cells, save.score, save.won);
                        announcedWin = save.won;
                    }
                }
                catch (Exception)
                {
                    // Corrupt save: start fresh below.
                }
            }
            if (grid.Tiles.Count == 0)
            {
                grid.Spawn();
                grid.Spawn();
            }
            RebuildTiles();
            UpdateStats();
        }

        void Persist()
        {
            BrainPrefs.SetSaveData("2048", JsonUtility.ToJson(new Save2048 { cells = grid.ToArray(), score = grid.Score, won = announcedWin }));
        }

        protected override void LayoutGame(Vector2 size)
        {
            if (size.x <= 0f) return;
            boardSize = Mathf.Min(size.x, size.y);
            UIFactory.Place(board, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(boardSize, boardSize));
            const float padding = 14f;
            cellSize = (boardSize - padding * (Grid2048.Size + 1)) / Grid2048.Size;
            RebuildTiles();
        }

        Vector2 CellPosition(int cell)
        {
            const float padding = 14f;
            int column = cell % Grid2048.Size, row = cell / Grid2048.Size;
            float x = -boardSize * 0.5f + padding + cellSize * 0.5f + column * (cellSize + padding);
            float y = boardSize * 0.5f - padding - cellSize * 0.5f - row * (cellSize + padding);
            return new Vector2(x, y);
        }

        void RebuildTiles()
        {
            if (tileLayer == null || cellSize <= 0f) return;
            for (int i = tileLayer.childCount - 1; i >= 0; i--) Destroy(tileLayer.GetChild(i).gameObject);
            viewsByCell.Clear();

            // Empty slots.
            for (int cell = 0; cell < Grid2048.Size * Grid2048.Size; cell++)
            {
                var slot = UIFactory.AddPanel(tileLayer, "Slot", Palette.WithAlpha(Color.white, 0.06f), 22f);
                UIFactory.Place(slot.rectTransform, new Vector2(0.5f, 0.5f), CellPosition(cell), new Vector2(cellSize, cellSize));
            }
            foreach (var tile in grid.Tiles) viewsByCell[tile.Cell] = CreateTile(tile.Value, tile.Cell);
            if (undoButton != null) SetUndoEnabled(grid.CanUndo);
        }

        TileView CreateTile(int value, int cell)
        {
            var panel = UIFactory.AddPanel(tileLayer, "Tile" + value, BrainArt.TileColor(value), 22f);
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), CellPosition(cell), new Vector2(cellSize, cellSize));
            var label = UIFactory.AddLabel(panel.rectTransform, "Value", value.ToString(), FontSizeFor(value), BrainArt.TileTextColor(value), FontWeight.ExtraBold);
            UIFactory.Stretch(label.rectTransform);
            return new TileView { Rect = panel.rectTransform, Panel = panel, Label = label, Value = value };
        }

        float FontSizeFor(int value) => cellSize * (value < 100 ? 0.46f : value < 1000 ? 0.38f : 0.3f);

        void SetTileValue(TileView view, int value)
        {
            view.Value = value;
            view.Panel.color = BrainArt.TileColor(value);
            view.Label.text = value.ToString();
            view.Label.color = BrainArt.TileTextColor(value);
            view.Label.fontSize = Mathf.RoundToInt(FontSizeFor(value));
        }

        void OnSwipe(SlideDirection direction)
        {
            if (animating || grid == null) return;
            var before = new Dictionary<int, TileView>(viewsByCell);
            if (!grid.Move(direction)) return;
            StartCoroutine(AnimateMove(before));
        }

        IEnumerator AnimateMove(Dictionary<int, TileView> before)
        {
            animating = true;
            App.Instance.Audio.Play(Sfx.PieceMove, 0.5f, 1.3f);
            Haptics.Play(HapticKind.Selection);

            var next = new Dictionary<int, TileView>();
            var moving = new List<(TileView view, Vector2 from, Vector2 to)>();
            var merging = new List<TileView>();
            var popping = new List<(TileView view, int value)>();
            var spawned = new List<TileView>();

            foreach (var tile in grid.Tiles)
            {
                if (tile.FromCell < 0)
                {
                    var fresh = CreateTile(tile.Value, tile.Cell);
                    fresh.Rect.localScale = Vector3.zero;
                    spawned.Add(fresh);
                    next[tile.Cell] = fresh;
                    continue;
                }
                if (!before.TryGetValue(tile.FromCell, out var view)) continue;
                moving.Add((view, view.Rect.anchoredPosition, CellPosition(tile.Cell)));
                if (tile.Merged && tile.MergedFromCell >= 0 && before.TryGetValue(tile.MergedFromCell, out var partner))
                {
                    moving.Add((partner, partner.Rect.anchoredPosition, CellPosition(tile.Cell)));
                    merging.Add(partner);
                    popping.Add((view, tile.Value));
                }
                next[tile.Cell] = view;
            }

            yield return Tween.Run(0.1f, t =>
            {
                foreach (var (view, from, to) in moving)
                    if (view.Rect != null) view.Rect.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
            }, Ease.OutQuad);

            foreach (var view in merging)
                if (view.Rect != null) Destroy(view.Rect.gameObject);
            foreach (var (view, value) in popping)
            {
                SetTileValue(view, value);
                view.Rect.SetAsLastSibling();
            }
            if (popping.Count > 0) App.Instance.Audio.Play(Sfx.Pop, 0.6f);

            viewsByCell.Clear();
            foreach (var pair in next) viewsByCell[pair.Key] = pair.Value;

            yield return Tween.Run(0.14f, t =>
            {
                foreach (var (view, _) in popping)
                    if (view.Rect != null) view.Rect.localScale = Vector3.one * (1f + Ease.Arc(t) * 0.16f);
                foreach (var view in spawned)
                    if (view.Rect != null) view.Rect.localScale = Vector3.one * t;
            }, Ease.OutQuad);
            foreach (var view in spawned)
                if (view.Rect != null) view.Rect.localScale = Vector3.one;

            animating = false;
            UpdateStats();
            Persist();
            SetUndoEnabled(grid.CanUndo);

            if (grid.Won && !announcedWin)
            {
                // Reaching 2048 is a milestone, not the end: celebrate and let the game continue.
                announcedWin = true;
                Persist();
                App.Instance.Audio.Play(Sfx.Win, 0.7f);
                Haptics.Play(HapticKind.Success);
                Effects.Confetti(UI.FxLayer, new[] { BrainArt.TileColor(2048), Palette.Gold, Color.white, BrainArt.Accent }, 70, 2.4f);
                UI.Toast("2048! Keep going", BrainArt.TileColor(2048), 1.6f);
            }
            if (!grid.IsStuck()) yield break;

            // One optional rescue per game: watch a video to take the last move back.
            if (!usedContinue && grid.CanUndo && Ads.RewardedReady)
            {
                usedContinue = true;
                Ads.OfferReward(UI, "No moves left", "Watch a short video to undo your last move and keep playing?", "Watch", earned =>
                {
                    if (!earned)
                    {
                        EndGame();
                        return;
                    }
                    grid.Undo();
                    RebuildTiles();
                    UpdateStats();
                    Persist();
                    App.Instance.Audio.Play(Sfx.Pop, 0.6f);
                    UI.Toast("Move taken back", BrainArt.TileColor(64), 1.1f);
                });
                yield break;
            }
            EndGame();
        }

        void EndGame()
        {
            int best = BrainPrefs.BestScore("2048");
            bool record = BrainPrefs.SubmitHighScore("2048", grid.Score);
            BrainPrefs.SetSaveData("2048", string.Empty);
            ShowResult("No moves left", record ? $"New best score: {grid.Score}" : $"Score {grid.Score} (best {Mathf.Max(best, grid.Score)})", record, NewGame);
        }

        void Undo()
        {
            if (animating || grid == null || !grid.Undo()) return;
            App.Instance.Audio.Play(Sfx.Pop, 0.5f, 0.8f);
            RebuildTiles();
            UpdateStats();
            Persist();
        }

        void SetUndoEnabled(bool enabled)
        {
            var group = undoButton.GetComponent<CanvasGroup>();
            if (group == null) group = undoButton.gameObject.AddComponent<CanvasGroup>();
            group.alpha = enabled ? 1f : 0.45f;
            group.interactable = enabled;
            group.blocksRaycasts = enabled;
        }

        void UpdateStats()
        {
            int best = Mathf.Max(BrainPrefs.BestScore("2048"), grid.Score);
            SetStat(0, "Score", grid.Score.ToString());
            SetStat(1, "Best", best.ToString());
            int highest = 0;
            foreach (var tile in grid.Tiles) highest = Mathf.Max(highest, tile.Value);
            SetStat(2, "Top tile", highest.ToString());
        }

        protected override void OnClosed()
        {
            if (grid != null && !grid.IsStuck())
            {
                BrainPrefs.SubmitHighScore("2048", grid.Score);
                Persist();
            }
        }
    }

    /// <summary>Turns a drag into one of four directions.</summary>
    public sealed class SwipeArea : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Action<SlideDirection> Swiped;
        Vector2 start;
        bool tracking;

        public void OnBeginDrag(PointerEventData eventData)
        {
            start = eventData.position;
            tracking = true;
        }

        public void OnDrag(PointerEventData eventData) { }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!tracking) return;
            tracking = false;
            var delta = eventData.position - start;
            if (delta.magnitude < 40f) return;
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y)) Swiped?.Invoke(delta.x > 0f ? SlideDirection.Right : SlideDirection.Left);
            else Swiped?.Invoke(delta.y > 0f ? SlideDirection.Up : SlideDirection.Down);
        }
    }
}
