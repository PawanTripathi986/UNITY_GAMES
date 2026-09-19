using System;
using NexioCraft.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NexioCraft.Chess
{
    /// <summary>Choose the piece a pawn promotes to.</summary>
    public sealed class ChessPromotionOverlay : UIOverlay
    {
        public int Side = Chess.Side.White;
        public Action<int> Chosen;
        public Action Cancelled;
        bool picked;

        protected override void Build()
        {
            var card = BuildCard(900f, 480f, Palette.Card);
            var title = UIFactory.AddLabel(card, "Title", "Promote to", 64f, Color.white, FontWeight.ExtraBold);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -85f), new Vector2(800f, 90f));

            int[] types = { Piece.Queen, Piece.Rook, Piece.Bishop, Piece.Knight };
            for (int i = 0; i < types.Length; i++)
            {
                int type = types[i];
                var tile = UIFactory.AddPanel(card, "Option", Side == Chess.Side.White ? Palette.Hex(0x58606E) : Palette.Hex(0xE4E7EE), 32f, true);
                UIFactory.Place(tile.rectTransform, new Vector2(0.5f, 0f), new Vector2((i - 1.5f) * 200f, 170f), new Vector2(170f, 170f));
                var piece = UIFactory.AddImage(tile.rectTransform, "Piece", ChessArt.PieceSprite(type * Side), Color.white);
                piece.preserveAspect = true;
                UIFactory.Stretch(piece.rectTransform, 12f, 12f, 12f, 12f);
                UIFactory.MakeButton(tile.gameObject, () =>
                {
                    picked = true;
                    UI.CloseOverlay(this);
                    Chosen?.Invoke(type);
                });
                tile.gameObject.AddComponent<PressFeedback>();
            }
        }

        protected override void OnClosed()
        {
            if (!picked) Cancelled?.Invoke();
        }
    }

    public sealed class ChessPauseOverlay : UIOverlay
    {
        public ChessGameScreen Game;

        protected override void Build()
        {
            var card = BuildCard(840f, 1180f, Palette.Card);
            var title = UIFactory.AddLabel(card, "Title", "Paused", 90f, Color.white, FontWeight.ExtraBold);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(700f, 120f));

            float y = -290f;
            AddButton(card, "Resume", Palette.Green, IconKind.Play, ref y, () => UI.CloseOverlay(this));
            AddButton(card, "How to Play", Palette.Blue, IconKind.Help, ref y, () => UI.PushOverlay<ChessHowToOverlay>());
            AddButton(card, "New Game", Palette.Orange, IconKind.Restart, ref y, () =>
                UI.PushOverlay<ConfirmOverlay>(o =>
                {
                    o.Title = "Start a new game?";
                    o.Message = "The current game will be lost.";
                    o.ConfirmLabel = "New Game";
                    o.ConfirmColor = Palette.Orange;
                    o.Confirmed = () => Game.NewGame();
                }));
            AddButton(card, "Resign", Palette.Red, IconKind.Flag, ref y, () =>
                UI.PushOverlay<ConfirmOverlay>(o =>
                {
                    o.Title = "Resign this game?";
                    o.Message = "Your opponent wins.";
                    o.ConfirmLabel = "Resign";
                    o.Confirmed = () =>
                    {
                        UI.CloseOverlay(this);
                        Game.Resign();
                    };
                }));
            AddButton(card, "Main Menu", Palette.Neutral, IconKind.Home, ref y, () => Game.ExitToMenu());

            var note = UIFactory.AddLabel(card, "Note", "Your game is saved automatically.", 38f, Palette.TextDim, FontWeight.Medium);
            UIFactory.Place(note.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 62f), new Vector2(760f, 60f));
        }

        static void AddButton(RectTransform card, string label, Color color, IconKind icon, ref float y, Action onClick)
        {
            var button = UIFactory.AddGameButton(card, label, color, new Vector2(640f, 136f), onClick, 54f, icon);
            UIFactory.Place((RectTransform)button.transform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(640f, 136f));
            y -= 162f;
        }

        protected override void OnClosed()
        {
            if (Game != null) Game.Resume();
        }
    }

    public sealed class ChessResultOverlay : UIOverlay
    {
        public string Title;
        public string Reason;
        public bool Celebrate;
        public bool Lost;
        public int WinnerSide;
        public Action PlayAgain;
        public Action Menu;

        protected override bool CloseOnBackdropTap => false;

        protected override void Build()
        {
            var card = BuildCard(900f, 760f, Palette.Card);

            var badge = UIFactory.AddImage(card, "Badge", Sprites.Circle, Lost ? Palette.Neutral : Palette.Gold);
            UIFactory.Place(badge.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(210f, 210f));
            var piece = UIFactory.AddImage(badge.rectTransform, "Piece", ChessArt.PieceSprite(Piece.King * (WinnerSide == 0 ? Side.White : WinnerSide)), Color.white);
            piece.preserveAspect = true;
            UIFactory.Stretch(piece.rectTransform, 30f, 26f, 30f, 34f);

            var title = UIFactory.AddLabel(card, "Title", Title, 100f, Lost ? Palette.TextDim : Color.white, FontWeight.ExtraBold);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(840f, 130f));
            UIFactory.AddTextShadow(title, 6f, 0.35f);
            var reason = UIFactory.AddLabel(card, "Reason", Reason, 50f, Palette.TextDim, FontWeight.Bold);
            UIFactory.Place(reason.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(840f, 70f));

            Ads.GameFinished();
            var again = UIFactory.AddGameButton(card, "Play Again", Palette.Green, new Vector2(400f, 150f),
                () => Ads.ShowInterstitial(() => PlayAgain?.Invoke()), 54f, IconKind.Restart);
            UIFactory.Place((RectTransform)again.transform, new Vector2(0.5f, 0f), new Vector2(210f, 130f), new Vector2(400f, 150f));
            var menu = UIFactory.AddGameButton(card, "Menu", Palette.Neutral, new Vector2(360f, 150f),
                () => Ads.ShowInterstitial(() => Menu?.Invoke()), 54f, IconKind.Home);
            UIFactory.Place((RectTransform)menu.transform, new Vector2(0.5f, 0f), new Vector2(-210f, 130f), new Vector2(360f, 150f));

            App.Instance.Audio.Play(Lost ? Sfx.Lose : Sfx.Win);
            Haptics.Play(Lost ? HapticKind.Warning : HapticKind.Success);
            if (Celebrate) Effects.Confetti(UI.FxLayer, new[] { Palette.Gold, Palette.Hex(0xF0D9B5), Palette.Hex(0xB58863), Color.white, Palette.Blue });
        }

        public override bool HandleBack()
        {
            Menu?.Invoke();
            return true;
        }
    }

    public sealed class ChessHowToOverlay : UIOverlay
    {
        const string Rules =
            "<b><color=#FFFFFF>Goal</color></b>\n" +
            "Checkmate the other king: attack it so that it has no way to escape.\n\n" +
            "<b><color=#FFFFFF>Playing a move</color></b>\n" +
            "Tap one of your pieces to see where it can go, then tap a highlighted square. You can also drag pieces.\n\n" +
            "<b><color=#FFFFFF>How pieces move</color></b>\n" +
            "King: one square in any direction.\n" +
            "Queen: any distance in a straight line or diagonal.\n" +
            "Rook: any distance in a straight line.\n" +
            "Bishop: any distance diagonally.\n" +
            "Knight: an L shape, jumping over pieces.\n" +
            "Pawn: forward one square (two on its first move) and captures diagonally.\n\n" +
            "<b><color=#FFFFFF>Special moves</color></b>\n" +
            "Castling: move the king two squares towards a rook that has not moved; the rook jumps over. Not allowed out of, through, or into check.\n" +
            "En passant: a pawn that moves two squares can be captured by an enemy pawn beside it, as if it had moved one.\n" +
            "Promotion: a pawn reaching the far side becomes a queen, rook, bishop or knight.\n\n" +
            "<b><color=#FFFFFF>Draws</color></b>\n" +
            "Stalemate (no legal move but not in check), the same position three times, fifty moves without a capture or pawn move, or too few pieces to checkmate.\n\n" +
            "<b><color=#FFFFFF>Buttons</color></b>\n" +
            "Undo takes back your last move, Hint shows a strong move, and Flip turns the board around.";

        protected override void Build()
        {
            var card = BuildCard(920f, 1560f, Palette.Card);
            var title = UIFactory.AddLabel(card, "Title", "How to Play", 80f, Color.white, FontWeight.ExtraBold);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(800f, 110f));

            var viewport = UIFactory.AddRect("Viewport", card);
            UIFactory.Stretch(viewport, 60f, 180f, 60f, 240f);
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();

            var content = UIFactory.AddRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(0, 0, 0, 30);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var text = UIFactory.AddLabel(content, "Rules", Rules, 42f, Palette.Lighten(Palette.TextDim, 0.4f), FontWeight.Medium, TextAnchor.UpperLeft);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.lineSpacing = 0.82f;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;

            var close = UIFactory.AddGameButton(card, "Got it", Palette.Green, new Vector2(520f, 150f), () => UI.CloseOverlay(this), 58f, IconKind.Check);
            UIFactory.Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(520f, 150f));
        }
    }
}
