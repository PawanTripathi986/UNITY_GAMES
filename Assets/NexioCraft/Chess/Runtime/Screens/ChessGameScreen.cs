using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NexioCraft.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NexioCraft.Chess
{
    /// <summary>A chess match against the computer or pass-and-play.</summary>
    public sealed class ChessGameScreen : UIScreen
    {
        const float TopBarHeight = 140f;
        const float ActionBarHeight = 170f;
        const float Gap = 22f;

        public ChessSave Save;

        ChessGame game;
        ChessBoardView board;
        ChessPlayerStrip topStrip;
        ChessPlayerStrip bottomStrip;
        Text status;
        Image soundIcon;
        Vector2 laidOutSize;
        int selected = -1;
        readonly List<ChessMove> selectedMoves = new List<ChessMove>();
        bool animating;
        bool computerThinking;
        bool hintRunning;
        bool paused;
        bool resultShown;
        const int FreeHintsPerGame = 2;
        int hintsUsed;
        CancellationTokenSource workerCancel;
        readonly System.Random rng = new System.Random();

        bool VsComputer => Save.Mode == ChessMode.VsComputer;
        bool HumanToMove => !VsComputer || game.SideToMove == Save.humanSide;

        protected override void Build()
        {
            game = ChessGame.FromUci(Save.startFen, Save.moves);
            BuildTopBar();

            topStrip = ChessPlayerStrip.Create(SafeRoot);
            bottomStrip = ChessPlayerStrip.Create(SafeRoot);
            foreach (var strip in new[] { topStrip, bottomStrip })
                strip.Rect.anchorMin = strip.Rect.anchorMax = new Vector2(0.5f, 1f);

            board = ChessBoardView.Create(SafeRoot, ChessPrefs.Theme, Save.flipped);
            board.Holder.anchorMin = board.Holder.anchorMax = new Vector2(0.5f, 1f);
            board.CanPickUp = CanPickUp;
            board.SquareTapped = OnSquareTapped;
            board.PieceDropped = OnPieceDropped;

            BuildActionBar();
            RefreshAll();

            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            App.PauseChanged += OnAppPause;
        }

        void Start()
        {
            if (game.IsOver) StartCoroutine(ShowResultSoon());
            else if (!HumanToMove) StartCoroutine(ComputerTurn());
        }

        void OnDestroy()
        {
            workerCancel?.Cancel();
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

            status = UIFactory.AddLabel(bar, "Status", "", 52f, Color.white, FontWeight.ExtraBold);
            UIFactory.Stretch(status.rectTransform, 170f, 0f, 170f, 0f);
            status.horizontalOverflow = HorizontalWrapMode.Wrap;
            status.resizeTextForBestFit = true;
            status.resizeTextMinSize = 30;
            status.resizeTextMaxSize = 52;
            UIFactory.AddTextShadow(status, 4f, 0.3f);
        }

        void BuildActionBar()
        {
            var bar = UIFactory.AddRect("Actions", SafeRoot);
            bar.anchorMin = new Vector2(0.5f, 0f);
            bar.anchorMax = new Vector2(0.5f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.sizeDelta = new Vector2(1000f, ActionBarHeight);
            bar.anchoredPosition = new Vector2(0f, 20f);

            AddAction(bar, "Undo", IconKind.Undo, -330f, Undo);
            AddAction(bar, "Hint", IconKind.Bulb, 0f, Hint);
            AddAction(bar, "Flip", IconKind.Flip, 330f, Flip);
        }

        static void AddAction(RectTransform bar, string label, IconKind icon, float x, System.Action onClick)
        {
            var button = UIFactory.AddGameButton(bar, label, Palette.CardRaised, new Vector2(300f, 124f), onClick, 46f, icon);
            UIFactory.Place((RectTransform)button.transform, new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(300f, 124f));
        }

        void Update()
        {
            var size = SafeRoot.rect.size;
            if (size.x > 0f && size.y > 0f && size != laidOutSize) Layout(size);
        }

        void Layout(Vector2 area)
        {
            laidOutSize = area;
            const float frame = 32f;
            const float sidePad = 20f;
            float region = area.y - TopBarHeight - ActionBarHeight - 40f;
            float boardSize = Mathf.Floor(Mathf.Min(area.x - sidePad * 2f - frame, region - ChessPlayerStrip.Height * 2f - Gap * 2f - frame));
            float holder = boardSize + frame;
            float block = ChessPlayerStrip.Height * 2f + Gap * 2f + holder;
            float top = TopBarHeight + Mathf.Max(0f, (region - block) * 0.5f);
            float stripWidth = holder;

            topStrip.Rect.sizeDelta = new Vector2(stripWidth, ChessPlayerStrip.Height);
            topStrip.Rect.anchoredPosition = new Vector2(0f, -(top + ChessPlayerStrip.Height * 0.5f));
            board.Holder.anchoredPosition = new Vector2(0f, -(top + ChessPlayerStrip.Height + Gap + holder * 0.5f));
            bottomStrip.Rect.sizeDelta = new Vector2(stripWidth, ChessPlayerStrip.Height);
            bottomStrip.Rect.anchoredPosition = new Vector2(0f, -(top + ChessPlayerStrip.Height + Gap * 2f + holder + ChessPlayerStrip.Height * 0.5f));
            board.Resize(boardSize);
        }

        bool CanPickUp(int square)
        {
            if (!InputAllowed()) return false;
            int piece = game.Position.Squares[square];
            return piece != 0 && Piece.SideOf(piece) == game.SideToMove;
        }

        bool InputAllowed() => !animating && !computerThinking && !hintRunning && !paused && !game.IsOver && HumanToMove;

        void OnSquareTapped(int square)
        {
            if (!InputAllowed()) return;
            if (selected >= 0 && square != selected)
            {
                foreach (var move in selectedMoves)
                {
                    if (move.To != square) continue;
                    TryMove(selected, square);
                    return;
                }
            }
            int piece = game.Position.Squares[square];
            if (piece != 0 && Piece.SideOf(piece) == game.SideToMove && square != selected) Select(square);
            else Select(-1);
        }

        void OnPieceDropped(int from, int to)
        {
            if (!InputAllowed())
            {
                board.CancelDrag();
                return;
            }
            foreach (var move in game.LegalMoves)
            {
                if (move.From != from || move.To != to) continue;
                TryMove(from, to);
                return;
            }
            board.CancelDrag();
            Select(from);
        }

        void Select(int square)
        {
            selected = square;
            selectedMoves.Clear();
            if (square >= 0)
                foreach (var move in game.LegalMoves)
                    if (move.From == square) selectedMoves.Add(move);
            board.SetSelection(square, selectedMoves, ChessPrefs.ShowLegalMoves);
        }

        void TryMove(int from, int to)
        {
            var options = new List<ChessMove>();
            foreach (var move in game.LegalMoves)
                if (move.From == from && move.To == to) options.Add(move);
            if (options.Count == 0) return;
            if (!options[0].IsPromotion)
            {
                StartCoroutine(PlayMove(options[0]));
                return;
            }
            int side = game.SideToMove;
            UI.PushOverlay<ChessPromotionOverlay>(o =>
            {
                o.Side = side;
                o.Chosen = type =>
                {
                    foreach (var option in options)
                        if (option.Promotion == type) StartCoroutine(PlayMove(option));
                };
                o.Cancelled = () =>
                {
                    board.CancelDrag();
                    board.Sync(game.Position);
                };
            });
        }

        IEnumerator PlayMove(ChessMove move)
        {
            animating = true;
            Select(-1);
            board.SetHint(-1, -1);
            var before = game.Position.Clone();
            game.Play(move);
            yield return board.AnimateMove(move, before);
            board.Sync(game.Position);
            board.SetLastMove(move.From, move.To);
            PlayMoveFeedback(move);
            RefreshAll();
            SaveProgress();
            animating = false;

            if (game.IsOver)
            {
                yield return ShowResultSoon();
                yield break;
            }
            if (!HumanToMove) yield return ComputerTurn();
        }

        void PlayMoveFeedback(ChessMove move)
        {
            var sound = App.Instance.Audio;
            if (move.IsPromotion) sound.Play(Sfx.Promote, 0.8f);
            else if (move.IsCapture) sound.Play(Sfx.PieceCapture);
            else sound.Play(Sfx.PieceMove);
            Haptics.Play(move.IsCapture ? HapticKind.Medium : HapticKind.Light);
            if (!game.IsOver && game.Position.InCheck(game.SideToMove))
            {
                sound.Play(Sfx.Check, 0.7f);
                Haptics.Play(HapticKind.Warning);
            }
        }

        IEnumerator ComputerTurn()
        {
            computerThinking = true;
            RefreshStatus();
            workerCancel?.Cancel();
            workerCancel = new CancellationTokenSource();
            var token = workerCancel.Token;
            var snapshot = ChessGame.FromUci(game.StartFen, game.MovesToUci());
            var level = Save.Level;
            int seed = rng.Next();
            var task = Task.Run(() => ChessAI.FindMove(snapshot, level, new System.Random(seed), token), token);
            float started = Time.unscaledTime;
            while (!task.IsCompleted || Time.unscaledTime - started < 0.5f || paused) yield return null;
            computerThinking = false;
            if (token.IsCancellationRequested || task.IsCanceled || this == null) yield break;

            ChessMove move;
            if (task.IsFaulted)
            {
                Debug.LogException(task.Exception);
                move = game.LegalMoves[rng.Next(game.LegalMoves.Count)];
            }
            else
            {
                move = task.Result;
            }
            if (!game.LegalMoves.Contains(move)) yield break;
            yield return PlayMove(move);
        }

        void Undo()
        {
            if (animating || game.Moves.Count == 0 || game.IsOver && resultShown) return;
            workerCancel?.Cancel();
            computerThinking = false;
            hintRunning = false;
            StopAllCoroutines();
            animating = false;

            if (VsComputer)
            {
                // Take back to the player's previous turn.
                do game.Undo();
                while (game.Moves.Count > 0 && game.SideToMove != Save.humanSide);
            }
            else
            {
                game.Undo();
            }

            Select(-1);
            board.SetHint(-1, -1);
            board.Sync(game.Position);
            var last = game.LastMove;
            if (game.Moves.Count > 0) board.SetLastMove(last.From, last.To);
            else board.SetLastMove(-1, -1);
            RefreshAll();
            SaveProgress();
            App.Instance.Audio.Play(Sfx.PieceMove, 0.6f);
            if (!HumanToMove && !game.IsOver) StartCoroutine(ComputerTurn());
        }

        void Hint()
        {
            if (!InputAllowed()) return;
            // A couple of free hints per game, then an optional video for more.
            if (hintsUsed < FreeHintsPerGame || !Ads.RewardedReady)
            {
                hintsUsed++;
                StartCoroutine(HintRoutine());
                return;
            }
            Ads.OfferReward(UI, "Out of free hints", "Watch a short video for another hint?", "Watch", earned =>
            {
                if (earned) StartCoroutine(HintRoutine());
            });
        }

        IEnumerator HintRoutine()
        {
            hintRunning = true;
            string previous = status.text;
            status.text = "Looking for a good move...";
            workerCancel?.Cancel();
            workerCancel = new CancellationTokenSource();
            var token = workerCancel.Token;
            var snapshot = ChessGame.FromUci(game.StartFen, game.MovesToUci());
            var task = Task.Run(() => ChessAI.FindBestMove(snapshot, 8, 900, token), token);
            while (!task.IsCompleted) yield return null;
            hintRunning = false;
            status.text = previous;
            if (task.IsFaulted || task.IsCanceled) yield break;
            var move = task.Result;
            board.SetHint(move.From, move.To);
            App.Instance.Audio.Play(Sfx.Pop, 0.7f);
        }

        void Flip()
        {
            Save.flipped = !board.Flipped;
            board.SetFlipped(Save.flipped);
            if (selected >= 0) board.SetSelection(selected, selectedMoves, ChessPrefs.ShowLegalMoves);
            RefreshAll();
            SaveProgress();
        }

        void RefreshAll()
        {
            board.Sync(game.Position);
            var last = game.LastMove;
            board.SetLastMove(game.Moves.Count > 0 ? last.From : -1, game.Moves.Count > 0 ? last.To : -1);

            int bottomSide = board.Flipped ? Side.Black : Side.White;
            ConfigureStrip(bottomStrip, bottomSide);
            ConfigureStrip(topStrip, -bottomSide);
            RefreshStatus();
        }

        void ConfigureStrip(ChessPlayerStrip strip, int side)
        {
            strip.SetPlayer(PlayerName(side), side);
            int material = 0;
            foreach (sbyte piece in game.Captured)
            {
                if (piece == 0) continue;
                int value = Piece.TypeOf(piece) switch { Piece.Pawn => 1, Piece.Knight => 3, Piece.Bishop => 3, Piece.Rook => 5, Piece.Queen => 9, _ => 0 };
                material += Piece.SideOf(piece) == -side ? value : -value;
            }
            strip.SetCaptured(game.Captured, material);
            strip.SetActive(!game.IsOver && game.SideToMove == side);
        }

        string PlayerName(int side)
        {
            if (!VsComputer) return side == Side.White ? "White" : "Black";
            if (side == Save.humanSide) return "You";
            return "Computer (" + Save.Level + ")";
        }

        void RefreshStatus()
        {
            if (game.IsOver)
            {
                status.text = ResultTitle();
                status.color = Color.white;
                return;
            }
            bool check = game.Position.InCheck(game.SideToMove);
            string text;
            if (computerThinking) text = "Computer is thinking...";
            else if (VsComputer) text = check ? "Check! Your move" : "Your move";
            else text = (game.SideToMove == Side.White ? "White" : "Black") + (check ? " is in check" : " to move");
            status.text = text;
            status.color = check ? Palette.Hex(0xFF8A80) : Color.white;
        }

        public string ResultTitle()
        {
            int winner = game.Winner;
            if (game.IsDraw) return "Draw";
            if (VsComputer) return winner == Save.humanSide ? "You win!" : "You lost";
            return winner == Side.White ? "White wins" : "Black wins";
        }

        IEnumerator ShowResultSoon()
        {
            yield return Tween.Delay(0.7f);
            ShowResult();
        }

        void ShowResult()
        {
            if (resultShown) return;
            resultShown = true;
            ChessPrefs.ClearGame();
            int outcome = game.IsDraw ? 0 : game.Winner == Save.humanSide ? 1 : -1;
            if (VsComputer) ChessPrefs.RecordComputerGame(outcome);
            RefreshStatus();
            UI.PushOverlay<ChessResultOverlay>(o =>
            {
                o.Title = ResultTitle();
                o.Reason = ResultReason(game.Result);
                o.Celebrate = VsComputer ? outcome > 0 : !game.IsDraw;
                o.Lost = VsComputer && outcome < 0;
                o.WinnerSide = game.Winner;
                o.PlayAgain = NewGame;
                o.Menu = () => UI.Show<ChessMenuScreen>();
            });
        }

        public static string ResultReason(ChessResult result) => result switch
        {
            ChessResult.WhiteWinsByCheckmate or ChessResult.BlackWinsByCheckmate => "by checkmate",
            ChessResult.WhiteWinsByResignation or ChessResult.BlackWinsByResignation => "by resignation",
            ChessResult.Stalemate => "Stalemate",
            ChessResult.DrawByRepetition => "Threefold repetition",
            ChessResult.DrawByFiftyMoves => "Fifty-move rule",
            ChessResult.DrawByMaterial => "Not enough pieces to mate",
            _ => ""
        };

        public void NewGame()
        {
            StopAllCoroutines();
            workerCancel?.Cancel();
            int side = Save.humanSide;
            var fresh = new ChessSave { mode = Save.mode, humanSide = side, level = Save.level, flipped = VsComputer ? side == Side.Black : false };
            ChessPrefs.SaveGame(fresh);
            UI.Show<ChessGameScreen>(g => g.Save = fresh);
        }

        public void Resign()
        {
            if (game.IsOver) return;
            StopAllCoroutines();
            workerCancel?.Cancel();
            computerThinking = false;
            animating = false;
            int resigningSide = VsComputer ? Save.humanSide : game.SideToMove;
            game.Resign(resigningSide);
            RefreshAll();
            ShowResult();
        }

        public void ExitToMenu()
        {
            StopAllCoroutines();
            workerCancel?.Cancel();
            if (!game.IsOver) SaveProgress();
            UI.Show<ChessMenuScreen>();
        }

        public void Resume() => paused = false;

        void SaveProgress()
        {
            if (game.IsOver)
            {
                ChessPrefs.ClearGame();
                return;
            }
            Save.moves = game.MovesToUci();
            ChessPrefs.SaveGame(Save);
        }

        void OpenPause()
        {
            if (paused || resultShown) return;
            paused = true;
            UI.PushOverlay<ChessPauseOverlay>(o => o.Game = this);
        }

        void ToggleSound()
        {
            Settings.SetSound(!Settings.Sound);
            soundIcon.sprite = Icons.Get(Settings.Sound ? IconKind.SoundOn : IconKind.SoundOff);
        }

        void OnAppPause(bool appPaused)
        {
            if (!appPaused) return;
            SaveProgress();
            // A full-screen ad pauses the activity too; that is not the player leaving the game.
            if (!game.IsOver && !Ads.ShowingAd) OpenPause();
        }

        public override bool HandleBack()
        {
            OpenPause();
            return true;
        }
    }
}
