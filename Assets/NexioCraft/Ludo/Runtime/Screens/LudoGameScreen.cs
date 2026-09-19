using System.Collections;
using System.Collections.Generic;
using NexioCraft.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NexioCraft.Ludo
{
    /// <summary>The match: lays out board and players, then runs the turn loop (roll, choose, animate, save).</summary>
    public sealed class LudoGameScreen : UIScreen
    {
        const float TopBarHeight = 140f;
        const float Gap = 24f;
        const float BottomPad = 28f;

        public LudoState State;

        readonly System.Random rng = new System.Random();
        readonly List<LudoMove> moves = new List<LudoMove>();
        readonly PlayerPanel[] panels = new PlayerPanel[LudoState.SeatCount];
        readonly string[] names = new string[LudoState.SeatCount];

        LudoBoardView board;
        DiceView dice;
        Text turnLabel;
        Image soundIcon;
        Vector2 laidOutSize;
        float messageBandTop;
        float messageBandBottom;
        int diceSeat = -1;
        bool diceMoving;
        bool paused;
        bool rollRequested;
        bool awaitingMove;
        LudoMove? chosenMove;
        int previousSeat = -1;

        float StepTime => LudoPrefs.FastMode ? 0.085f : 0.14f;
        float RollTime => LudoPrefs.FastMode ? 0.38f : 0.6f;
        float BotThink => LudoPrefs.FastMode ? 0.2f : 0.45f;

        protected override void Build()
        {
            int viewSeat = PrimarySeat();
            for (int seat = 0; seat < LudoState.SeatCount; seat++) names[seat] = SeatName(seat);

            BuildTopBar();
            board = LudoBoardView.Create(SafeRoot, State, viewSeat);
            board.Holder.anchorMin = board.Holder.anchorMax = new Vector2(0.5f, 1f);
            board.Tapped += OnBoardTapped;

            for (int seat = 0; seat < LudoState.SeatCount; seat++)
            {
                if (!State.IsActive(seat)) continue;
                int corner = board.ScreenCorner(seat);
                bool leftSide = corner == 0 || corner == 1;
                panels[seat] = PlayerPanel.Create(SafeRoot, seat, State.Kind(seat), names[seat], leftSide);
                var rt = panels[seat].Rect;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            }

            dice = DiceView.Create(SafeRoot, 124f);
            dice.Tapped += () => rollRequested = true;
            dice.gameObject.SetActive(false);

            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            App.PauseChanged += OnAppPause;
        }

        void Start() => StartCoroutine(Play());

        void OnDestroy()
        {
            App.PauseChanged -= OnAppPause;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
        }

        void BuildTopBar()
        {
            var bar = UIFactory.AddRect("TopBar", SafeRoot);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.sizeDelta = new Vector2(0f, TopBarHeight);
            bar.anchoredPosition = Vector2.zero;

            var pause = UIFactory.AddIconButton(bar, IconKind.Pause, Palette.CardRaised, 112f, OpenPause);
            UIFactory.Place((RectTransform)pause.transform, new Vector2(0f, 0.5f), new Vector2(34f + 56f, 0f), new Vector2(112f, 112f));

            var sound = UIFactory.AddIconButton(bar, Settings.Sound ? IconKind.SoundOn : IconKind.SoundOff, Palette.CardRaised, 112f, ToggleSound);
            UIFactory.Place((RectTransform)sound.transform, new Vector2(1f, 0.5f), new Vector2(-34f - 56f, 0f), new Vector2(112f, 112f));
            soundIcon = sound.transform.Find("Face/Icon").GetComponent<Image>();

            turnLabel = UIFactory.AddLabel(bar, "Turn", "", 50f, Color.white, FontWeight.ExtraBold);
            UIFactory.Stretch(turnLabel.rectTransform, 170f, 0f, 170f, 0f);
            turnLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            turnLabel.resizeTextForBestFit = true;
            turnLabel.resizeTextMinSize = 30;
            turnLabel.resizeTextMaxSize = 50;
            UIFactory.AddTextShadow(turnLabel, 4f, 0.3f);
        }

        void Update()
        {
            var size = SafeRoot.rect.size;
            if (size.x > 0f && size.y > 0f && size != laidOutSize) Layout(size);
        }

        void LateUpdate()
        {
            // Keep the die glued to its slot (panels scale slightly when active).
            if (diceSeat >= 0 && !diceMoving && panels[diceSeat] != null) dice.Rect.position = panels[diceSeat].DiceSlot.position;
        }

        void Layout(Vector2 area)
        {
            laidOutSize = area;
            const float sidePad = 24f;
            const float frame = 28f;
            float panelHeight = PlayerPanel.Height;
            float regionHeight = area.y - TopBarHeight - BottomPad;
            float boardSize = Mathf.Floor(Mathf.Min(area.x - sidePad * 2f - frame, regionHeight - panelHeight * 2f - Gap * 2f - frame));
            float holderSize = boardSize + frame;
            float block = panelHeight * 2f + Gap * 2f + holderSize;
            float blockTop = TopBarHeight + Mathf.Max(0f, (regionHeight - block) * 0.5f);
            messageBandTop = TopBarHeight;
            messageBandBottom = blockTop;

            board.Holder.anchoredPosition = new Vector2(0f, -(blockTop + panelHeight + Gap + holderSize * 0.5f));
            board.Resize(boardSize, State);

            float panelWidth = Mathf.Min(540f, holderSize * 0.5f - 8f);
            float topY = -(blockTop + panelHeight * 0.5f);
            float bottomY = -(blockTop + panelHeight + Gap + holderSize + Gap + panelHeight * 0.5f);
            float leftX = -holderSize * 0.5f + panelWidth * 0.5f;
            float rightX = holderSize * 0.5f - panelWidth * 0.5f;
            for (int seat = 0; seat < LudoState.SeatCount; seat++)
            {
                if (panels[seat] == null) continue;
                int corner = board.ScreenCorner(seat);
                float x = corner == 0 || corner == 1 ? leftX : rightX;
                float y = corner == 1 || corner == 2 ? topY : bottomY;
                var rt = panels[seat].Rect;
                rt.sizeDelta = new Vector2(panelWidth, panelHeight);
                rt.anchoredPosition = new Vector2(x, y);
            }
        }

        IEnumerator Play()
        {
            while (laidOutSize == Vector2.zero) yield return null;
            RefreshPanels();
            yield return Wait(0.3f);

            while (!State.over)
            {
                yield return WhilePaused();
                int seat = State.current;
                bool human = State.Kind(seat) == SeatKind.Human;
                SetActivePanel(seat);
                yield return MoveDiceTo(seat);

                int roll;
                RollOutcome outcome;
                if (State.pendingRoll > 0)
                {
                    // Resumed after the die was already rolled: use that roll instead of rolling again.
                    roll = State.pendingRoll;
                    dice.ShowFace(roll);
                    moves.Clear();
                    LudoEngine.CollectMoves(State, seat, roll, moves);
                    outcome = RollOutcome.ChooseMove;
                }
                else
                {
                    if (human)
                    {
                        if (previousSeat != seat) App.Instance.Audio.Play(Sfx.YourTurn, 0.7f);
                        SetTurnText(State.Mode == LudoMode.VsComputer ? "Your turn - tap the dice" : $"{names[seat]}'s turn - tap the dice", seat);
                        rollRequested = false;
                        dice.SetInteractive(true);
                        while (!rollRequested || paused) yield return null;
                        dice.SetInteractive(false);
                    }
                    else
                    {
                        SetTurnText($"{names[seat]} is rolling", seat);
                        yield return Wait(BotThink);
                    }

                    roll = rng.Next(1, 7);
                    App.Instance.Audio.Play(Sfx.DiceRoll);
                    Haptics.Play(HapticKind.Light);
                    yield return dice.Roll(roll, RollTime, rng);
                    if (roll == 6) App.Instance.Audio.Play(Sfx.Six, 0.7f);
                    outcome = LudoEngine.Roll(State, roll, moves);
                }
                previousSeat = seat;
                bool you = human && State.Mode == LudoMode.VsComputer;
                SetTurnText(you ? $"You rolled a {roll}" : $"{names[seat]} rolled a {roll}", seat);

                if (outcome == RollOutcome.ThreeSixes)
                {
                    App.Instance.Audio.Play(Sfx.NoMove);
                    Haptics.Play(HapticKind.Warning);
                    Toast("Three 6s - turn lost!", LudoTheme.Seat(seat), 1.1f);
                    yield return Wait(1.3f);
                    LudoPrefs.SaveGame(State);
                    continue;
                }

                if (outcome == RollOutcome.NoMoves)
                {
                    if (human)
                    {
                        App.Instance.Audio.Play(Sfx.NoMove, 0.6f);
                        Toast(roll == 6 ? "No move - roll again" : "No moves", Palette.Neutral, 0.7f);
                    }
                    yield return Wait(human ? 1.0f : 0.55f);
                    LudoPrefs.SaveGame(State);
                    continue;
                }

                LudoMove move;
                if (human)
                {
                    if (LudoPrefs.AutoMove && LudoEngine.AllEquivalent(moves))
                    {
                        yield return Wait(0.2f);
                        move = moves[0];
                    }
                    else
                    {
                        SetTurnText(State.Mode == LudoMode.VsComputer ? "Tap a token to move" : $"{names[seat]} - tap a token", seat);
                        chosenMove = null;
                        awaitingMove = true;
                        board.SetMovable(moves);
                        while (chosenMove == null || paused) yield return null;
                        awaitingMove = false;
                        board.ClearMovable();
                        move = chosenMove.Value;
                    }
                }
                else
                {
                    yield return Wait(BotThink);
                    move = LudoAI.Choose(State, moves, rng);
                }

                var result = LudoEngine.Apply(State, move);
                yield return board.AnimateMove(State, result, StepTime);
                RefreshPanels();

                if (result.SeatFinished)
                {
                    App.Instance.Audio.Play(Sfx.Win, 0.8f);
                    Haptics.Play(HapticKind.Success);
                    Effects.Burst(UI.FxLayer, Vector2.zero, LudoTheme.Seat(move.Seat), 24, 420f, 36f);
                    string who = State.Kind(move.Seat) == SeatKind.Human && State.Mode == LudoMode.VsComputer ? "You finished" : $"{names[move.Seat]} finished";
                    Toast($"{who} {Ordinal(result.FinishRank)}!", LudoTheme.Seat(move.Seat), 1.3f);
                    yield return Wait(1.6f);
                }
                else if (result.ExtraTurn && result.Captures.Count > 0 && human)
                {
                    Toast("Captured! Roll again", LudoTheme.Seat(seat), 0.7f);
                }
                LudoPrefs.SaveGame(State);
            }

            FinishGame();
        }

        void FinishGame()
        {
            dice.SetInteractive(false);
            SetActivePanel(-1);
            RefreshPanels();
            LudoPrefs.ClearGame();
            int winner = State.finishOrder[0];
            bool humanWon = State.Kind(winner) == SeatKind.Human;
            LudoPrefs.RecordGame(State.Mode == LudoMode.VsComputer && humanWon);
            SetTurnText("Game over", -1);
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
            UI.PushOverlay<LudoResultOverlay>(o =>
            {
                o.State = State;
                o.Names = names;
                o.PlayAgain = Restart;
                o.Menu = () => UI.Show<LudoMenuScreen>();
            });
        }

        public void Restart()
        {
            StopAllCoroutines();
            var kinds = new SeatKind[LudoState.SeatCount];
            var levels = new BotLevel[LudoState.SeatCount];
            for (int seat = 0; seat < LudoState.SeatCount; seat++)
            {
                kinds[seat] = State.Kind(seat);
                levels[seat] = State.Level(seat);
            }
            var fresh = LudoEngine.NewGame(kinds, levels, State.rules, State.Mode, PrimarySeat());
            LudoPrefs.SaveGame(fresh);
            UI.Show<LudoGameScreen>(g => g.State = fresh);
        }

        public void ExitToMenu()
        {
            StopAllCoroutines();
            LudoPrefs.SaveGame(State);
            UI.Show<LudoMenuScreen>();
        }

        public void Resume() => paused = false;

        void OpenPause()
        {
            if (State.over || paused) return;
            paused = true;
            UI.PushOverlay<LudoPauseOverlay>(o => o.Game = this);
        }

        void ToggleSound()
        {
            Settings.SetSound(!Settings.Sound);
            soundIcon.sprite = Icons.Get(Settings.Sound ? IconKind.SoundOn : IconKind.SoundOff);
        }

        void OnAppPause(bool appPaused)
        {
            if (!appPaused) return;
            LudoPrefs.SaveGame(State);
            // A full-screen ad pauses the activity too; that is not the player leaving the game.
            if (!Ads.ShowingAd) OpenPause();
        }

        public override bool HandleBack()
        {
            OpenPause();
            return true;
        }

        void OnBoardTapped(Vector2 local)
        {
            if (!awaitingMove || paused) return;
            var hit = board.HitTest(local, view => FindMove(view.Seat, view.Index).HasValue);
            if (hit == null) return;
            chosenMove = FindMove(hit.Seat, hit.Index);
        }

        LudoMove? FindMove(int seat, int token)
        {
            foreach (var move in moves)
                if (move.Seat == seat && move.Token == token) return move;
            return null;
        }

        IEnumerator MoveDiceTo(int seat)
        {
            if (diceSeat == seat && dice.gameObject.activeSelf) yield break;
            var target = panels[seat].DiceSlot;
            if (!dice.gameObject.activeSelf)
            {
                dice.gameObject.SetActive(true);
                dice.Rect.position = target.position;
                diceSeat = seat;
                yield return Tween.Run(0.25f, t => dice.Rect.localScale = Vector3.one * Mathf.LerpUnclamped(0.3f, 1f, t), Ease.OutBack);
                yield break;
            }

            diceMoving = true;
            Vector3 from = dice.Rect.position;
            yield return Tween.Run(0.28f, t =>
            {
                dice.Rect.position = Vector3.LerpUnclamped(from, target.position, t);
                float s = 1f + Ease.Arc(t) * 0.2f;
                dice.Rect.localScale = new Vector3(s, s, 1f);
            }, Ease.InOutQuad);
            diceSeat = seat;
            diceMoving = false;
        }

        void SetActivePanel(int seat)
        {
            for (int s = 0; s < LudoState.SeatCount; s++)
                if (panels[s] != null) panels[s].SetActive(s == seat);
        }

        void RefreshPanels()
        {
            for (int seat = 0; seat < LudoState.SeatCount; seat++)
            {
                if (panels[seat] == null) continue;
                panels[seat].SetTokensHome(State.TokensHome(seat));
                panels[seat].SetRank(State.RankOf(seat));
            }
        }

        /// <summary>Shows a message in the free band above the board so it never hides tokens.</summary>
        void Toast(string message, Color color, float seconds)
        {
            const float toastHeight = 124f;
            float band = messageBandBottom - messageBandTop;
            Vector3 world;
            if (band >= toastHeight + 8f)
            {
                float fromTop = messageBandTop + band * 0.5f;
                world = SafeRoot.TransformPoint(new Vector3(0f, SafeRoot.rect.yMax - fromTop, 0f));
            }
            else
            {
                world = board.Holder.position;
            }
            UI.Toast(message, color, seconds, UI.FxLayer.InverseTransformPoint(world).y);
        }

        void SetTurnText(string text, int seat)
        {
            turnLabel.text = text;
            turnLabel.color = seat >= 0 ? Palette.Lighten(LudoTheme.Seat(seat), 0.35f) : Color.white;
        }

        IEnumerator Wait(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                if (!paused) elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        IEnumerator WhilePaused()
        {
            while (paused) yield return null;
        }

        /// <summary>The seat shown at the bottom-left: the first human, else Red.</summary>
        int PrimarySeat()
        {
            for (int seat = 0; seat < LudoState.SeatCount; seat++)
                if (State.IsActive(seat) && State.Kind(seat) == SeatKind.Human) return seat;
            for (int seat = 0; seat < LudoState.SeatCount; seat++)
                if (State.IsActive(seat)) return seat;
            return 0;
        }

        string SeatName(int seat)
        {
            if (!State.IsActive(seat)) return string.Empty;
            if (State.Mode == LudoMode.VsComputer && State.Kind(seat) == SeatKind.Human) return "You";
            return LudoTheme.SeatNames[seat];
        }

        public static string Ordinal(int rank)
        {
            switch (rank)
            {
                case 0: return "1st";
                case 1: return "2nd";
                case 2: return "3rd";
                default: return (rank + 1) + "th";
            }
        }
    }
}
