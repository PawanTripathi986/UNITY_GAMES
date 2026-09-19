using System;
using System.Collections;
using System.Collections.Generic;
using NexioCraft.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NexioCraft.Blocks
{
    /// <summary>
    /// Block puzzle: drag one of three pieces onto the 8x8 board; full rows and columns clear.
    /// The game ends when none of the remaining pieces fit.
    /// </summary>
    public sealed class BlockPuzzleScreen : UIScreen
    {
        const float HeaderHeight = 150f;
        const float StatsTop = 176f;
        const float StatsHeight = 118f;
        const float TrayHeight = 260f;

        readonly BlockBoard board = new BlockBoard();
        readonly int[] trayShapes = new int[3];
        readonly int[] trayColours = new int[3];
        readonly bool[] trayUsed = new bool[3];
        readonly System.Random rng = new System.Random();

        readonly Image[] cells = new Image[BlockBoard.Size * BlockBoard.Size];
        readonly List<Image> ghosts = new List<Image>();
        readonly RectTransform[] traySlots = new RectTransform[3];
        readonly List<Image>[] trayBlocks = new List<Image>[3];

        Text[] statValues;
        RectTransform boardRect;
        RectTransform dragLayer;
        RectTransform floating;
        List<Image> floatingBlocks = new List<Image>();
        int draggingSlot = -1;
        Vector2Int ghostAnchor = new Vector2Int(-1, -1);
        bool ghostValid;
        float cellSize;
        float boardSize;
        Vector2 laidOut;
        bool gameOver;
        bool busy;
        bool usedContinue;

        protected override void Build()
        {
            UIFactory.AddHeader(SafeRoot, "Block Puzzle", () => App.Instance.GoHome(), HeaderHeight);
            var restart = UIFactory.AddIconButton(SafeRoot, IconKind.Restart, Palette.CardRaised, 104f, ConfirmRestart);
            UIFactory.Place((RectTransform)restart.transform, new Vector2(1f, 1f), new Vector2(-40f - 52f, -30f - 52f), new Vector2(104f, 104f));

            statValues = new Text[3];
            var stats = UIFactory.AddRect("Stats", SafeRoot);
            stats.anchorMin = new Vector2(0f, 1f);
            stats.anchorMax = new Vector2(1f, 1f);
            stats.pivot = new Vector2(0.5f, 1f);
            stats.offsetMin = new Vector2(40f, 0f);
            stats.offsetMax = new Vector2(-40f, 0f);
            stats.sizeDelta = new Vector2(-80f, StatsHeight);
            stats.anchoredPosition = new Vector2(0f, -StatsTop);
            string[] labels = { "Score", "Best", "Lines" };
            for (int i = 0; i < 3; i++)
            {
                var chip = UIFactory.AddPanel(stats, "Stat" + i, Palette.Card, 30f);
                chip.rectTransform.anchorMin = new Vector2(i / 3f, 0f);
                chip.rectTransform.anchorMax = new Vector2((i + 1) / 3f, 1f);
                chip.rectTransform.offsetMin = new Vector2(i == 0 ? 0f : 8f, 0f);
                chip.rectTransform.offsetMax = new Vector2(i == 2 ? 0f : -8f, 0f);
                var label = UIFactory.AddLabel(chip.rectTransform, "Label", labels[i].ToUpperInvariant(), 30f, Palette.TextDim, FontWeight.Bold);
                UIFactory.Stretch(label.rectTransform, 8f, 14f, 8f, 60f);
                statValues[i] = UIFactory.AddLabel(chip.rectTransform, "Value", "0", 46f, Color.white, FontWeight.ExtraBold);
                UIFactory.Stretch(statValues[i].rectTransform, 8f, 52f, 8f, 10f);
            }

            boardRect = UIFactory.AddRect("Board", SafeRoot);
            boardRect.anchorMin = boardRect.anchorMax = new Vector2(0.5f, 1f);
            var boardBack = UIFactory.AddPanel(boardRect, "Back", Palette.Inset, 34f);
            UIFactory.Stretch(boardBack.rectTransform, -14f, -14f, -14f, -14f);
            for (int i = 0; i < cells.Length; i++)
            {
                var image = UIFactory.AddImage(boardRect, "Cell" + i, BlocksArt.EmptyCell(), Color.white);
                cells[i] = image;
            }
            for (int i = 0; i < 9; i++)
            {
                var ghost = UIFactory.AddImage(boardRect, "Ghost" + i, BlocksArt.Block(1), new Color(1f, 1f, 1f, 0.45f));
                ghost.enabled = false;
                ghosts.Add(ghost);
            }

            for (int i = 0; i < 3; i++)
            {
                int slot = i;
                var holder = UIFactory.AddRect("Tray" + i, SafeRoot);
                holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0f);
                var hit = holder.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                var dragger = holder.gameObject.AddComponent<TrayDragger>();
                dragger.Begin = position => BeginDrag(slot, position);
                dragger.Move = MoveDrag;
                dragger.End = EndDrag;
                traySlots[i] = holder;
                trayBlocks[i] = new List<Image>();
            }

            dragLayer = UIFactory.Stretch(UIFactory.AddRect("DragLayer", SafeRoot));
            var dragBlocker = dragLayer.gameObject.AddComponent<CanvasGroup>();
            dragBlocker.blocksRaycasts = false;

            LoadOrStart();
        }

        void LoadOrStart()
        {
            var save = BlocksPrefs.Load();
            if (save != null)
            {
                board.Load(save.cells, save.score, save.lines);
                for (int i = 0; i < 3; i++)
                {
                    trayShapes[i] = save.trayShapes[i];
                    trayColours[i] = save.trayColours != null && save.trayColours.Length == 3 ? save.trayColours[i] : 1 + i;
                    trayUsed[i] = save.trayUsed != null && save.trayUsed.Length == 3 && save.trayUsed[i];
                }
                if (AllTrayUsed()) DealTray();
            }
            else
            {
                NewGame(false);
            }
            RefreshBoard();
            RefreshTray();
            UpdateStats();
            CheckGameOver();
        }

        void NewGame(bool refresh = true)
        {
            gameOver = false;
            busy = false;
            usedContinue = false;
            board.Load(new int[BlockBoard.Size * BlockBoard.Size], 0, 0);
            DealTray();
            Persist();
            if (!refresh) return;
            RefreshBoard();
            RefreshTray();
            UpdateStats();
        }

        void ConfirmRestart()
        {
            if (board.Score == 0)
            {
                NewGame();
                return;
            }
            UI.PushOverlay<ConfirmOverlay>(o =>
            {
                o.Title = "Start again?";
                o.Message = "Your current board and score will be lost.";
                o.ConfirmLabel = "Restart";
                o.ConfirmColor = Palette.Orange;
                o.Confirmed = () => NewGame();
            });
        }

        bool AllTrayUsed() => trayUsed[0] && trayUsed[1] && trayUsed[2];

        void DealTray()
        {
            for (int i = 0; i < 3; i++)
            {
                trayShapes[i] = BlockShapes.RandomIndex(rng);
                trayColours[i] = rng.Next(BlocksArt.Colors.Length) + 1;
                trayUsed[i] = false;
            }
        }

        void Update()
        {
            var size = SafeRoot.rect.size;
            if (size.x > 0f && size.y > 0f && size != laidOut) Layout(size);
        }

        void Layout(Vector2 area)
        {
            laidOut = area;
            float regionTop = StatsTop + StatsHeight + 20f;
            float regionHeight = area.y - regionTop - 20f;
            const float gap = 30f;
            boardSize = Mathf.Floor(Mathf.Min(area.x - 60f, regionHeight - TrayHeight - gap));
            cellSize = boardSize / BlockBoard.Size;
            // Centre the board and tray together in the space under the stats.
            float blockTop = regionTop + Mathf.Max(0f, (regionHeight - boardSize - gap - TrayHeight) * 0.5f);
            UIFactory.Place(boardRect, new Vector2(0.5f, 1f), new Vector2(0f, -(blockTop + boardSize * 0.5f)), new Vector2(boardSize, boardSize));
            float trayCentre = blockTop + boardSize + gap + TrayHeight * 0.5f;

            for (int i = 0; i < cells.Length; i++)
                UIFactory.Place(cells[i].rectTransform, new Vector2(0.5f, 0.5f), CellCenter(i % BlockBoard.Size, i / BlockBoard.Size), new Vector2(cellSize, cellSize));
            foreach (var ghost in ghosts) ghost.rectTransform.sizeDelta = new Vector2(cellSize, cellSize);

            for (int i = 0; i < 3; i++)
                UIFactory.Place(traySlots[i], new Vector2(0.5f, 1f), new Vector2((i - 1) * (area.x / 3.2f), -trayCentre), new Vector2(area.x / 3.4f, TrayHeight - 40f));
            RefreshTray();
        }

        Vector2 CellCenter(int x, int y) =>
            new Vector2(-boardSize * 0.5f + (x + 0.5f) * cellSize, boardSize * 0.5f - (y + 0.5f) * cellSize);

        void RefreshBoard()
        {
            for (int i = 0; i < cells.Length; i++)
            {
                int value = board.Cells[i];
                cells[i].sprite = value == 0 ? BlocksArt.EmptyCell() : BlocksArt.Block(value);
                cells[i].color = Color.white;
                cells[i].rectTransform.localScale = Vector3.one;
            }
        }

        void RefreshTray()
        {
            if (cellSize <= 0f) return;
            for (int slot = 0; slot < 3; slot++)
            {
                foreach (var block in trayBlocks[slot])
                    if (block != null) Destroy(block.gameObject);
                trayBlocks[slot].Clear();
                if (trayUsed[slot]) continue;

                var shape = BlockShapes.All[trayShapes[slot]];
                float trayCell = Mathf.Min(cellSize * 0.62f, (traySlots[slot].rect.width - 10f) / Mathf.Max(shape.Width, 1), (traySlots[slot].rect.height - 10f) / Mathf.Max(shape.Height, 1));
                float width = shape.Width * trayCell, height = shape.Height * trayCell;
                foreach (var cell in shape.Cells)
                {
                    var block = UIFactory.AddImage(traySlots[slot], "Block", BlocksArt.Block(trayColours[slot]), Color.white);
                    var position = new Vector2(-width * 0.5f + (cell.x + 0.5f) * trayCell, height * 0.5f - (cell.y + 0.5f) * trayCell);
                    UIFactory.Place(block.rectTransform, new Vector2(0.5f, 0.5f), position, new Vector2(trayCell, trayCell));
                    trayBlocks[slot].Add(block);
                }
            }
        }

        void UpdateStats()
        {
            statValues[0].text = board.Score.ToString();
            statValues[1].text = Mathf.Max(BlocksPrefs.Best, board.Score).ToString();
            statValues[2].text = board.Lines.ToString();
        }

        void BeginDrag(int slot, Vector2 screenPosition)
        {
            if (busy || gameOver || trayUsed[slot] || cellSize <= 0f) return;
            draggingSlot = slot;
            var shape = BlockShapes.All[trayShapes[slot]];
            floating = UIFactory.AddRect("Floating", dragLayer);
            floating.sizeDelta = new Vector2(shape.Width * cellSize, shape.Height * cellSize);
            floatingBlocks = new List<Image>();
            foreach (var cell in shape.Cells)
            {
                var block = UIFactory.AddImage(floating, "Block", BlocksArt.Block(trayColours[slot]), Color.white);
                var position = new Vector2(-floating.sizeDelta.x * 0.5f + (cell.x + 0.5f) * cellSize,
                    floating.sizeDelta.y * 0.5f - (cell.y + 0.5f) * cellSize);
                UIFactory.Place(block.rectTransform, new Vector2(0.5f, 0.5f), position, new Vector2(cellSize, cellSize));
                floatingBlocks.Add(block);
            }
            foreach (var block in trayBlocks[slot])
                if (block != null) block.color = new Color(1f, 1f, 1f, 0.25f);
            App.Instance.Audio.Play(Sfx.Pop, 0.5f, 1.2f);
            MoveDrag(screenPosition);
        }

        void MoveDrag(Vector2 screenPosition)
        {
            if (floating == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayer, screenPosition, null, out var local)) return;
            // Lift the piece above the finger so it stays visible.
            floating.anchoredPosition = local + new Vector2(0f, cellSize * 1.4f);
            UpdateGhost();
        }

        void UpdateGhost()
        {
            foreach (var ghost in ghosts) ghost.enabled = false;
            ghostAnchor = new Vector2Int(-1, -1);
            ghostValid = false;
            if (floating == null || draggingSlot < 0) return;

            var shape = BlockShapes.All[trayShapes[draggingSlot]];
            // Top-left corner of the floating piece, in board coordinates.
            var world = floating.TransformPoint(new Vector3(-floating.sizeDelta.x * 0.5f, floating.sizeDelta.y * 0.5f, 0f));
            var local = (Vector2)boardRect.InverseTransformPoint(world);
            int x = Mathf.RoundToInt((local.x + boardSize * 0.5f) / cellSize);
            int y = Mathf.RoundToInt((boardSize * 0.5f - local.y) / cellSize);
            ghostAnchor = new Vector2Int(x, y);
            ghostValid = board.CanPlace(shape, x, y);
            if (!ghostValid) return;

            for (int i = 0; i < shape.Cells.Length && i < ghosts.Count; i++)
            {
                var cell = shape.Cells[i];
                var ghost = ghosts[i];
                ghost.enabled = true;
                ghost.sprite = BlocksArt.Block(trayColours[draggingSlot]);
                ghost.rectTransform.anchoredPosition = CellCenter(x + cell.x, y + cell.y);
                ghost.rectTransform.sizeDelta = new Vector2(cellSize, cellSize);
            }
        }

        void EndDrag(Vector2 screenPosition)
        {
            if (floating == null || draggingSlot < 0) return;
            int slot = draggingSlot;
            var shape = BlockShapes.All[trayShapes[slot]];
            bool placed = ghostValid && board.CanPlace(shape, ghostAnchor.x, ghostAnchor.y);
            foreach (var ghost in ghosts) ghost.enabled = false;

            if (!placed)
            {
                foreach (var block in trayBlocks[slot])
                    if (block != null) block.color = Color.white;
                Destroy(floating.gameObject);
                floating = null;
                draggingSlot = -1;
                App.Instance.Audio.Play(Sfx.NoMove, 0.4f);
                return;
            }

            Destroy(floating.gameObject);
            floating = null;
            draggingSlot = -1;
            trayUsed[slot] = true;
            foreach (var block in trayBlocks[slot])
                if (block != null) Destroy(block.gameObject);
            trayBlocks[slot].Clear();
            StartCoroutine(PlacePiece(shape, ghostAnchor, trayColours[slot]));
        }

        IEnumerator PlacePiece(BlockShape shape, Vector2Int anchor, int colour)
        {
            busy = true;
            var placedCells = new List<int>();
            foreach (var cell in shape.Cells) placedCells.Add((anchor.y + cell.y) * BlockBoard.Size + anchor.x + cell.x);
            var result = board.Place(shape, anchor.x, anchor.y, colour);

            // Show the new blocks (the board array already has the clears applied).
            foreach (int index in placedCells)
            {
                cells[index].sprite = BlocksArt.Block(colour);
                cells[index].color = Color.white;
            }
            App.Instance.Audio.Play(Sfx.PieceMove, 0.8f, 1.1f);
            Haptics.Play(HapticKind.Light);
            yield return Tween.Run(0.14f, t =>
            {
                float scale = 0.7f + 0.3f * Ease.OutBack(t);
                foreach (int index in placedCells) cells[index].rectTransform.localScale = new Vector3(scale, scale, 1f);
            });
            foreach (int index in placedCells) cells[index].rectTransform.localScale = Vector3.one;

            if (result.Lines > 0)
            {
                var clearing = new List<int>();
                foreach (int row in result.ClearedRows)
                    for (int column = 0; column < BlockBoard.Size; column++) clearing.Add(row * BlockBoard.Size + column);
                foreach (int column in result.ClearedColumns)
                    for (int row = 0; row < BlockBoard.Size; row++)
                    {
                        int index = row * BlockBoard.Size + column;
                        if (!clearing.Contains(index)) clearing.Add(index);
                    }

                App.Instance.Audio.Play(result.Lines > 1 ? Sfx.Win : Sfx.TokenHome, 0.7f);
                Haptics.Play(result.Lines > 1 ? HapticKind.Success : HapticKind.Medium);
                if (result.Lines > 1)
                    UI.Toast(result.Lines + " lines!  +" + result.Lines * result.Lines * 100, BlocksArt.ColorOf(colour), 1f, boardSize * 0.5f);

                yield return Tween.Run(0.26f, t =>
                {
                    foreach (int index in clearing)
                    {
                        cells[index].color = Color.Lerp(Color.white, new Color(1f, 1f, 1f, 0f), t);
                        float scale = 1f - 0.6f * t;
                        cells[index].rectTransform.localScale = new Vector3(scale, scale, 1f);
                    }
                }, Ease.OutQuad);
            }

            RefreshBoard();
            UpdateStats();
            if (AllTrayUsed())
            {
                DealTray();
                RefreshTray();
            }
            Persist();
            busy = false;
            CheckGameOver();
        }

        void CheckGameOver()
        {
            if (gameOver) return;
            for (int slot = 0; slot < 3; slot++)
            {
                if (trayUsed[slot]) continue;
                if (board.HasAnyPlacement(BlockShapes.All[trayShapes[slot]])) return;
            }

            // One optional rescue per game: watch a video, clear space, keep the run alive.
            if (!usedContinue && Ads.RewardedReady)
            {
                usedContinue = true;
                Ads.OfferReward(UI, "No room left", "Watch a short video to clear the two fullest rows and keep playing?", "Watch",
                    earned =>
                    {
                        if (earned) ContinueAfterReward();
                        else EndGame();
                    });
                return;
            }
            EndGame();
        }

        /// <summary>Clears the two rows holding the most blocks and deals a fresh tray.</summary>
        void ContinueAfterReward()
        {
            for (int cleared = 0; cleared < 2; cleared++)
            {
                int bestRow = -1, bestCount = 0;
                for (int row = 0; row < BlockBoard.Size; row++)
                {
                    int count = 0;
                    for (int column = 0; column < BlockBoard.Size; column++)
                        if (board[column, row] != 0) count++;
                    if (count > bestCount)
                    {
                        bestCount = count;
                        bestRow = row;
                    }
                }
                if (bestRow < 0) break;
                for (int column = 0; column < BlockBoard.Size; column++) board.Cells[bestRow * BlockBoard.Size + column] = 0;
            }
            DealTray();
            RefreshBoard();
            RefreshTray();
            UpdateStats();
            Persist();
            App.Instance.Audio.Play(Sfx.TokenHome, 0.7f);
            UI.Toast("Space cleared. Keep going!", BlocksArt.Colors[1], 1.2f);
            CheckGameOver();
        }

        void EndGame()
        {
            gameOver = true;
            bool record = BlocksPrefs.SubmitScore(board.Score);
            BlocksPrefs.Clear();
            UpdateStats();
            UI.PushOverlay<BlocksResultOverlay>(o =>
            {
                o.Title = "No room left";
                o.Subtitle = record
                    ? $"New best score: {board.Score}"
                    : $"Score {board.Score} with {board.Lines} lines (best {BlocksPrefs.Best})";
                o.Celebrate = record;
                o.PlayAgain = () => NewGame();
                o.Home = () => App.Instance.GoHome();
            });
        }

        void Persist()
        {
            if (gameOver) return;
            BlocksPrefs.Save(new BlocksSave
            {
                cells = (int[])board.Cells.Clone(),
                score = board.Score,
                lines = board.Lines,
                trayShapes = (int[])trayShapes.Clone(),
                trayColours = (int[])trayColours.Clone(),
                trayUsed = (bool[])trayUsed.Clone()
            });
            BlocksPrefs.SubmitScore(board.Score);
        }

        protected override void OnClosed() => Persist();

        public override bool HandleBack()
        {
            App.Instance.GoHome();
            return true;
        }
    }

    /// <summary>Reports drag positions for one tray slot.</summary>
    public sealed class TrayDragger : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Action<Vector2> Begin;
        public Action<Vector2> Move;
        public Action<Vector2> End;

        public void OnBeginDrag(PointerEventData eventData) => Begin?.Invoke(eventData.position);
        public void OnDrag(PointerEventData eventData) => Move?.Invoke(eventData.position);
        public void OnEndDrag(PointerEventData eventData) => End?.Invoke(eventData.position);
    }

    public sealed class BlocksResultOverlay : UIOverlay
    {
        public string Title;
        public string Subtitle;
        public bool Celebrate;
        public Action PlayAgain;
        public Action Home;

        protected override bool CloseOnBackdropTap => false;

        protected override void Build()
        {
            var card = BuildCard(880f, 620f, Palette.Card);
            var title = UIFactory.AddLabel(card, "Title", Title, 88f, Color.white, FontWeight.ExtraBold);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(800f, 120f));
            UIFactory.AddTextShadow(title, 6f, 0.35f);
            var subtitle = UIFactory.AddLabel(card, "Subtitle", Subtitle, 46f, Palette.TextDim, FontWeight.Bold);
            subtitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -270f), new Vector2(760f, 140f));

            Ads.GameFinished();
            var again = UIFactory.AddGameButton(card, "Play Again", Palette.Green, new Vector2(400f, 150f), () =>
            {
                UI.CloseOverlay(this);
                Ads.ShowInterstitial(() => PlayAgain?.Invoke());
            }, 54f, IconKind.Restart);
            UIFactory.Place((RectTransform)again.transform, new Vector2(0.5f, 0f), new Vector2(200f, 120f), new Vector2(400f, 150f));
            var home = UIFactory.AddGameButton(card, "Games", Palette.Neutral, new Vector2(340f, 150f),
                () => Ads.ShowInterstitial(() => Home?.Invoke()), 54f, IconKind.Home);
            UIFactory.Place((RectTransform)home.transform, new Vector2(0.5f, 0f), new Vector2(-220f, 120f), new Vector2(340f, 150f));

            App.Instance.Audio.Play(Celebrate ? Sfx.Win : Sfx.Lose, 0.8f);
            Haptics.Play(Celebrate ? HapticKind.Success : HapticKind.Warning);
            if (Celebrate) Effects.Confetti(UI.FxLayer, BlocksArt.Colors);
        }

        public override bool HandleBack()
        {
            Home?.Invoke();
            return true;
        }
    }
}
