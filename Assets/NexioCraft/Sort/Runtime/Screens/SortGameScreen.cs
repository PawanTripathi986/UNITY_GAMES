using System.Collections;
using System.Collections.Generic;
using NexioCraft.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NexioCraft.Sort
{
    /// <summary>
    /// Colour sort: tap a tube to lift its top colours, tap another to pour. A tube is done when it holds four
    /// of one colour. Undo, hints and an extra tube are the ad-supported helpers.
    /// </summary>
    public sealed class SortGameScreen : UIScreen
    {
        const float HeaderHeight = 150f;
        const float StatsTop = 176f;
        const float StatsHeight = 110f;
        const float ButtonsHeight = 210f;
        const int FreeUndos = 3;
        const int FreeHints = 1;
        const int MaxExtraTubes = 2;

        SortBoard board;
        SortLevel level;
        int undosUsed, hintsUsed, extraTubes;
        int selected = -1;
        bool busy;

        RectTransform area;
        readonly List<Image> tubes = new List<Image>();
        readonly List<Image[]> liquids = new List<Image[]>();
        Text levelText, movesText, bestText;
        Text undoLabel, hintLabel, tubeLabel;
        Vector2 laidOut;
        float tubeWidth, tubeHeight, slotHeight, rim;
        int perRow, rows;

        protected override void Build()
        {
            UIFactory.AddHeader(SafeRoot, "Color Sort", () => App.Instance.GoHome(), HeaderHeight);
            var restart = UIFactory.AddIconButton(SafeRoot, IconKind.Restart, Palette.CardRaised, 104f, ConfirmRestart);
            UIFactory.Place((RectTransform)restart.transform, new Vector2(1f, 1f), new Vector2(-40f - 52f, -30f - 52f), new Vector2(104f, 104f));

            var stats = UIFactory.AddRect("Stats", SafeRoot);
            stats.anchorMin = new Vector2(0f, 1f);
            stats.anchorMax = new Vector2(1f, 1f);
            stats.pivot = new Vector2(0.5f, 1f);
            stats.offsetMin = new Vector2(40f, 0f);
            stats.offsetMax = new Vector2(-40f, 0f);
            stats.sizeDelta = new Vector2(-80f, StatsHeight);
            stats.anchoredPosition = new Vector2(0f, -StatsTop);
            levelText = Chip(stats, 0, "LEVEL");
            movesText = Chip(stats, 1, "MOVES");
            bestText = Chip(stats, 2, "BEST");

            area = UIFactory.AddRect("Tubes", SafeRoot);
            UIFactory.Stretch(area, 20f, StatsTop + StatsHeight + 20f, 20f, ButtonsHeight);

            var buttons = UIFactory.AddRect("Buttons", SafeRoot);
            buttons.anchorMin = new Vector2(0f, 0f);
            buttons.anchorMax = new Vector2(1f, 0f);
            buttons.pivot = new Vector2(0.5f, 0f);
            buttons.offsetMin = new Vector2(40f, 40f);
            buttons.offsetMax = new Vector2(-40f, 0f);
            buttons.sizeDelta = new Vector2(-80f, 150f);
            undoLabel = Helper(buttons, -1, "Undo", Palette.Neutral, IconKind.Undo, null, UseUndo);
            hintLabel = Helper(buttons, 0, "Hint", Palette.Blue, IconKind.Bulb, null, UseHint);
            tubeLabel = Helper(buttons, 1, "Tube", SortArt.Accent, IconKind.Play, SortArt.AddTube, UseExtraTube);

            LoadOrStart();
        }

        static Text Chip(RectTransform parent, int index, string caption)
        {
            var chip = UIFactory.AddPanel(parent, caption, Palette.Card, 30f);
            chip.rectTransform.anchorMin = new Vector2(index / 3f, 0f);
            chip.rectTransform.anchorMax = new Vector2((index + 1) / 3f, 1f);
            chip.rectTransform.offsetMin = new Vector2(index == 0 ? 0f : 8f, 0f);
            chip.rectTransform.offsetMax = new Vector2(index == 2 ? 0f : -8f, 0f);
            var label = UIFactory.AddLabel(chip.rectTransform, "Label", caption, 28f, Palette.TextDim, FontWeight.Bold);
            UIFactory.Stretch(label.rectTransform, 8f, 12f, 8f, 56f);
            var value = UIFactory.AddLabel(chip.rectTransform, "Value", "0", 44f, Color.white, FontWeight.ExtraBold);
            UIFactory.Stretch(value.rectTransform, 8f, 48f, 8f, 8f);
            return value;
        }

        /// <summary>One helper button with an icon and a "left" counter; returns the counter label.</summary>
        Text Helper(RectTransform parent, int slot, string name, Color colour, IconKind icon, Sprite customIcon, System.Action action)
        {
            var root = UIFactory.AddRect(name, parent);
            UIFactory.Place(root, new Vector2(0.5f, 0.5f), new Vector2(slot * 330f, 0f), new Vector2(300f, 150f));
            var button = UIFactory.AddGameButton(root, name, colour, new Vector2(300f, 150f), action, 46f, icon);
            UIFactory.Place((RectTransform)button.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 150f));
            if (customIcon != null)
            {
                var image = button.transform.Find("Face/Content/Icon");
                if (image != null) image.GetComponent<Image>().sprite = customIcon;
            }
            var badge = UIFactory.AddPanel(root, "Badge", Palette.Inset, 26f);
            UIFactory.Place(badge.rectTransform, new Vector2(1f, 1f), new Vector2(-6f, 4f), new Vector2(86f, 52f));
            var label = UIFactory.AddLabel(badge.rectTransform, "Left", "", 30f, Color.white, FontWeight.ExtraBold);
            UIFactory.Stretch(label.rectTransform);
            return label;
        }

        // ------------------------------------------------------------ level lifecycle

        void LoadOrStart()
        {
            var save = SortPrefs.Load();
            if (save != null)
            {
                var restored = SortBoard.FromArray(save.cells);
                if (restored != null && !restored.Solved)
                {
                    level = SortLevels.For(save.level);
                    board = restored;
                    undosUsed = save.undosUsed;
                    hintsUsed = save.hintsUsed;
                    extraTubes = save.extraTubes;
                    Rebuild();
                    return;
                }
            }
            StartLevel(SortPrefs.Level);
        }

        void StartLevel(int index)
        {
            level = SortLevels.For(index);
            board = SortLevels.Create(level);
            undosUsed = hintsUsed = extraTubes = 0;
            selected = -1;
            Rebuild();
            Persist();
        }

        void ConfirmRestart()
        {
            if (board.Moves == 0)
            {
                StartLevel(level.Index);
                return;
            }
            UI.PushOverlay<ConfirmOverlay>(o =>
            {
                o.Title = "Restart level?";
                o.Message = "The tubes go back to how they started.";
                o.ConfirmLabel = "Restart";
                o.ConfirmColor = Palette.Orange;
                o.Confirmed = () => StartLevel(level.Index);
            });
        }

        void Persist()
        {
            SortPrefs.Save(new SortSave
            {
                level = level.Index,
                tubes = board.TubeCount,
                cells = board.ToArray(),
                undosUsed = undosUsed,
                hintsUsed = hintsUsed,
                extraTubes = extraTubes
            });
        }

        /// <summary>Recreates the tube and liquid images for the current board.</summary>
        void Rebuild()
        {
            foreach (var tube in tubes)
                if (tube != null) Destroy(tube.gameObject);
            tubes.Clear();
            foreach (var row in liquids)
                foreach (var image in row)
                    if (image != null) Destroy(image.gameObject);
            liquids.Clear();

            for (int t = 0; t < board.TubeCount; t++)
            {
                var glass = UIFactory.AddImage(area, "Tube" + t, SortArt.Glass, Color.white, true);
                int index = t;
                UIFactory.MakeButton(glass.gameObject, () => Tap(index));
                tubes.Add(glass);
            }
            // Liquids live above the glasses so they can be animated from tube to tube.
            for (int t = 0; t < board.TubeCount; t++)
            {
                var slots = new Image[SortBoard.Capacity];
                for (int s = 0; s < SortBoard.Capacity; s++)
                    slots[s] = UIFactory.AddImage(area, $"Liquid{t}_{s}", SortArt.LiquidPlain, Color.white);
                liquids.Add(slots);
            }
            laidOut = Vector2.zero;
            Refresh();
        }

        void Update()
        {
            var size = area.rect.size;
            if (size.x > 0f && size.y > 0f && size != laidOut) Layout(size);
        }

        void Layout(Vector2 size)
        {
            laidOut = size;
            int count = board.TubeCount;
            perRow = count <= 5 ? count : Mathf.CeilToInt(count / 2f);
            perRow = Mathf.Min(perRow, 7);
            rows = Mathf.CeilToInt(count / (float)perRow);

            const float gapX = 22f, gapY = 46f;
            const float aspect = 3.5f;
            // Keep a margin at the sides: a row that fills the area looks like it is falling off the screen.
            float maxWidth = (size.x - 40f - gapX * (perRow - 1)) / perRow;
            float maxHeight = (size.y - gapY * (rows - 1)) / rows;
            // Fill the space: with few tubes the screen width is the limit, with many it is the height.
            tubeWidth = Mathf.Min(maxWidth, maxHeight / aspect, 185f);
            tubeHeight = tubeWidth * aspect;
            rim = tubeWidth * 0.1f;
            slotHeight = (tubeHeight - rim * 2f) / SortBoard.Capacity;
            Refresh();
        }

        Vector2 TubeCentre(int index)
        {
            int row = index / perRow;
            int column = index % perRow;
            int inRow = Mathf.Min(perRow, board.TubeCount - row * perRow);
            const float gapX = 22f, gapY = 46f;
            float rowWidth = inRow * tubeWidth + (inRow - 1) * gapX;
            float x = -rowWidth * 0.5f + tubeWidth * 0.5f + column * (tubeWidth + gapX);
            float totalHeight = rows * tubeHeight + (rows - 1) * gapY;
            float y = totalHeight * 0.5f - tubeHeight * 0.5f - row * (tubeHeight + gapY);
            return new Vector2(x, y);
        }

        Vector2 SlotPosition(int tube, int slot)
        {
            var centre = TubeCentre(tube);
            return new Vector2(centre.x, centre.y - tubeHeight * 0.5f + rim + slotHeight * (slot + 0.5f));
        }

        Vector2 LiftPosition(int tube, int order)
        {
            var centre = TubeCentre(tube);
            return new Vector2(centre.x, centre.y + tubeHeight * 0.5f + slotHeight * (order + 0.8f));
        }

        void Refresh()
        {
            if (tubeWidth <= 0f) return;
            for (int t = 0; t < tubes.Count; t++)
                UIFactory.Place(tubes[t].rectTransform, new Vector2(0.5f, 0.5f), TubeCentre(t), new Vector2(tubeWidth, tubeHeight));

            for (int t = 0; t < liquids.Count; t++)
            {
                int height = board.Height(t);
                int run = board.TopRun(t);
                for (int s = 0; s < SortBoard.Capacity; s++)
                {
                    var image = liquids[t][s];
                    int colour = board[t, s];
                    image.enabled = colour != 0;
                    if (colour == 0) continue;
                    image.sprite = s == 0 ? SortArt.LiquidBottom : s == height - 1 ? SortArt.LiquidTop : SortArt.LiquidPlain;
                    image.color = SortArt.Of(colour);
                    bool lifted = selected == t && s >= height - run;
                    var position = lifted ? LiftPosition(t, s - (height - run)) : SlotPosition(t, s);
                    UIFactory.Place(image.rectTransform, new Vector2(0.5f, 0.5f), position, new Vector2(tubeWidth - rim * 2f, slotHeight + 1f));
                }
            }

            levelText.text = level.Number.ToString();
            movesText.text = board.Moves.ToString();
            bestText.text = SortPrefs.Best.ToString();
            int undosLeft = Mathf.Max(0, FreeUndos - undosUsed);
            undoLabel.text = undosLeft > 0 ? undosLeft.ToString() : "AD";
            int hintsLeft = Mathf.Max(0, FreeHints - hintsUsed);
            hintLabel.text = hintsLeft > 0 ? hintsLeft.ToString() : "AD";
            tubeLabel.text = extraTubes >= MaxExtraTubes ? "0" : "AD";
        }

        // ------------------------------------------------------------ playing

        void Tap(int tube)
        {
            if (busy) return;
            if (selected < 0)
            {
                if (board.IsEmpty(tube)) return;
                Select(tube);
                return;
            }
            if (selected == tube)
            {
                Select(-1);
                return;
            }
            if (board.CanPour(selected, tube))
            {
                StartCoroutine(PourRoutine(selected, tube));
                return;
            }
            App.Instance.Audio.Play(Sfx.NoMove, 0.5f);
            if (board.IsEmpty(tube)) Select(-1);
            else Select(tube);
        }

        void Select(int tube)
        {
            selected = tube;
            if (tube >= 0)
            {
                App.Instance.Audio.Play(Sfx.Pop, 0.6f, 1.2f);
                Haptics.Play(HapticKind.Selection);
            }
            Refresh();
        }

        IEnumerator PourRoutine(int from, int to)
        {
            busy = true;
            int count = Mathf.Min(board.TopRun(from), SortBoard.Capacity - board.Height(to));
            int fromHeight = board.Height(from);
            int toHeight = board.Height(to);

            var movers = new List<RectTransform>();
            var starts = new List<Vector2>();
            var targets = new List<Vector2>();
            for (int i = 0; i < count; i++)
            {
                // Topmost first: it ends up highest in the destination.
                var image = liquids[from][fromHeight - 1 - i];
                movers.Add(image.rectTransform);
                starts.Add(image.rectTransform.anchoredPosition);
                targets.Add(SlotPosition(to, toHeight + count - 1 - i));
            }

            board.Pour(from, to);
            selected = -1;
            App.Instance.Audio.Play(Sfx.Step, 0.7f, 1.1f);
            Haptics.Play(HapticKind.Light);

            float lift = tubeHeight * 0.5f + slotHeight;
            yield return Tween.Run(0.26f, t =>
            {
                for (int i = 0; i < movers.Count; i++)
                {
                    if (movers[i] == null) continue;
                    var point = Vector2.Lerp(starts[i], targets[i], t);
                    // Arc over the rim of both tubes.
                    point.y += Mathf.Sin(t * Mathf.PI) * lift * 0.35f;
                    movers[i].anchoredPosition = point;
                }
            }, Ease.InOutQuad);

            Refresh();
            Persist();
            busy = false;

            if (board.Solved)
            {
                Win();
                yield break;
            }
            if (!board.HasUsefulMove())
                UI.Toast("No moves left - undo or add a tube", Palette.Orange, 1.6f);
        }

        void Win()
        {
            App.Instance.Audio.Play(Sfx.Win, 0.8f);
            Haptics.Play(HapticKind.Success);
            Effects.Confetti(UI.FxLayer, new[] { SortArt.Colours[1], SortArt.Colours[2], SortArt.Colours[3], SortArt.Colours[4], SortArt.Colours[5] });
            int next = level.Index + 1;
            // Remember the new level, but not this finished board.
            SortPrefs.Clear();
            SortPrefs.Level = next;
            UI.PushOverlay<SortWinOverlay>(o =>
            {
                o.Level = level;
                o.Moves = board.Moves;
                o.Next = () => StartLevel(next);
                o.Home = () => App.Instance.GoHome();
            });
        }

        // ------------------------------------------------------------ helpers

        void UseUndo()
        {
            if (busy || board.Moves == 0)
            {
                UI.Toast("Nothing to undo", Palette.Neutral, 0.9f);
                return;
            }
            if (undosUsed < FreeUndos)
            {
                undosUsed++;
                DoUndo();
                return;
            }
            if (!Ads.RewardedReady)
            {
                UI.Toast("No video available right now", Palette.Neutral, 1.1f);
                return;
            }
            Ads.OfferReward(UI, "Out of undos", "Watch a short video for three more undos?", "Watch", earned =>
            {
                if (!earned) return;
                undosUsed = Mathf.Max(0, undosUsed - FreeUndos);
                DoUndo();
            });
        }

        void DoUndo()
        {
            selected = -1;
            board.Undo();
            App.Instance.Audio.Play(Sfx.Pop, 0.6f, 0.8f);
            Refresh();
            Persist();
        }

        void UseHint()
        {
            if (busy) return;
            if (hintsUsed < FreeHints)
            {
                hintsUsed++;
                ShowHint();
                return;
            }
            if (!Ads.RewardedReady)
            {
                UI.Toast("No video available right now", Palette.Neutral, 1.1f);
                return;
            }
            Ads.OfferReward(UI, "Need a hint?", "Watch a short video to be shown the next pour?", "Watch", earned =>
            {
                if (earned) ShowHint();
            });
        }

        void ShowHint()
        {
            Refresh();
            if (!SortSolver.TryHint(board, out var hint))
            {
                UI.Toast("No way through - undo or add a tube", Palette.Orange, 1.6f);
                return;
            }
            Select(hint.From);
            StartCoroutine(FlashHint(hint.To));
            Persist();
        }

        IEnumerator FlashHint(int tube)
        {
            var glass = tubes[tube];
            for (int i = 0; i < 3; i++)
            {
                glass.color = SortArt.Accent;
                yield return Tween.Delay(0.18f);
                glass.color = Color.white;
                yield return Tween.Delay(0.16f);
            }
        }

        void UseExtraTube()
        {
            if (busy) return;
            if (extraTubes >= MaxExtraTubes)
            {
                UI.Toast("No more tubes for this level", Palette.Neutral, 1.1f);
                return;
            }
            if (!Ads.RewardedReady)
            {
                UI.Toast("No video available right now", Palette.Neutral, 1.1f);
                return;
            }
            Ads.OfferReward(UI, "Extra tube", "Watch a short video to add an empty tube?", "Watch", earned =>
            {
                if (!earned) return;
                extraTubes++;
                selected = -1;
                board.AddTube();
                Rebuild();
                Persist();
                App.Instance.Audio.Play(Sfx.Unlock, 0.8f);
                UI.Toast("Extra tube added", SortArt.Accent, 1.1f);
            });
        }

        protected override void OnClosed()
        {
            if (board != null && !board.Solved) Persist();
        }

        public override bool HandleBack()
        {
            App.Instance.GoHome();
            return true;
        }
    }
}
