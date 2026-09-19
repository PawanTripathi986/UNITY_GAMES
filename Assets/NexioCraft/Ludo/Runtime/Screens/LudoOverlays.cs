using System;
using System.Text;
using NexioCraft.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NexioCraft.Ludo
{
    public sealed class LudoPauseOverlay : UIOverlay
    {
        public LudoGameScreen Game;

        protected override void Build()
        {
            var card = BuildCard(840f, 1010f, Palette.Card);
            var title = UIFactory.AddLabel(card, "Title", "Paused", 90f, Color.white, FontWeight.ExtraBold);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(700f, 120f));

            float y = -290f;
            AddButton(card, "Resume", Palette.Green, IconKind.Play, ref y, () => UI.CloseOverlay(this));
            AddButton(card, "How to Play", Palette.Blue, IconKind.Help, ref y, () => UI.PushOverlay<LudoHowToOverlay>());
            AddButton(card, "Restart", Palette.Orange, IconKind.Restart, ref y, () =>
                UI.PushOverlay<ConfirmOverlay>(o =>
                {
                    o.Title = "Restart game?";
                    o.Message = "The current game will be lost.";
                    o.ConfirmLabel = "Restart";
                    o.ConfirmColor = Palette.Orange;
                    o.Confirmed = () => Game.Restart();
                }));
            AddButton(card, "Main Menu", Palette.Neutral, IconKind.Home, ref y, () => Game.ExitToMenu());

            var note = UIFactory.AddLabel(card, "Note", "Your game is saved automatically.", 38f, Palette.TextDim, FontWeight.Medium);
            UIFactory.Place(note.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 62f), new Vector2(760f, 60f));
        }

        static void AddButton(RectTransform card, string label, Color color, IconKind icon, ref float y, Action onClick)
        {
            var button = UIFactory.AddGameButton(card, label, color, new Vector2(640f, 142f), onClick, 56f, icon);
            UIFactory.Place((RectTransform)button.transform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(640f, 142f));
            y -= 172f;
        }

        protected override void OnClosed()
        {
            if (Game != null) Game.Resume();
        }
    }

    public sealed class LudoResultOverlay : UIOverlay
    {
        public LudoState State;
        public string[] Names;
        public Action PlayAgain;
        public Action Menu;

        protected override bool CloseOnBackdropTap => false;

        protected override void Build()
        {
            int ranked = State.finishedCount;
            float height = 560f + ranked * 150f;
            var card = BuildCard(900f, height, Palette.Card);

            int winner = State.finishOrder[0];
            bool vsComputer = State.Mode == LudoMode.VsComputer;
            bool humanWon = State.Kind(winner) == SeatKind.Human;
            string headline = vsComputer ? (humanWon ? "You Win!" : $"{Names[winner]} Wins") : $"{Names[winner]} Wins!";

            var crown = UIFactory.AddImage(card, "Crown", Icons.Get(IconKind.Crown), Palette.Gold);
            UIFactory.Place(crown.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 10f), new Vector2(170f, 170f));
            var title = UIFactory.AddLabel(card, "Title", headline, 92f, LudoTheme.Seat(winner), FontWeight.ExtraBold);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(840f, 130f));
            var outline = title.gameObject.AddComponent<Outline>();
            outline.effectColor = Palette.Darken(LudoTheme.Seat(winner), 0.6f);
            outline.effectDistance = new Vector2(4f, -4f);

            float y = -270f;
            for (int rank = 0; rank < ranked; rank++)
            {
                int seat = State.finishOrder[rank];
                var row = UIFactory.AddPanel(card, "Rank " + (rank + 1), rank == 0 ? Palette.CardRaised : Palette.Inset, 40f);
                UIFactory.Place(row.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, y - 64f), new Vector2(780f, 128f));

                var medal = UIFactory.AddImage(row.rectTransform, "Medal", Sprites.Circle, PlayerPanel.RankColor(rank));
                UIFactory.Place(medal.rectTransform, new Vector2(0f, 0.5f), new Vector2(76f, 0f), new Vector2(92f, 92f));
                var number = UIFactory.AddLabel(medal.rectTransform, "Number", (rank + 1).ToString(), 52f, Palette.Hex(0x2A2240), FontWeight.ExtraBold);
                UIFactory.Stretch(number.rectTransform);

                var dot = UIFactory.AddImage(row.rectTransform, "Colour", Sprites.Circle, LudoTheme.Seat(seat));
                UIFactory.Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(180f, 0f), new Vector2(64f, 64f));
                var icon = UIFactory.AddImage(dot.rectTransform, "Icon", Icons.Get(State.Kind(seat) == SeatKind.Bot ? IconKind.Robot : IconKind.Person), Color.white);
                UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f));

                var name = UIFactory.AddLabel(row.rectTransform, "Name", Names[seat], 52f, Color.white, FontWeight.ExtraBold, TextAnchor.MiddleLeft);
                UIFactory.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(240f, 0f), new Vector2(300f, 100f), new Vector2(0f, 0.5f));

                int home = State.TokensHome(seat);
                string detail = home == LudoState.TokensPerSeat ? "All home" : $"{home}/4 home";
                var info = UIFactory.AddLabel(row.rectTransform, "Detail", detail, 40f, Palette.TextDim, FontWeight.Bold, TextAnchor.MiddleRight);
                UIFactory.Place(info.rectTransform, new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(260f, 100f), new Vector2(1f, 0.5f));
                y -= 150f;
            }

            // A full-screen ad may appear here, paced by Ads so it never interrupts a match.
            Ads.GameFinished();
            var again = UIFactory.AddGameButton(card, "Play Again", Palette.Green, new Vector2(400f, 150f),
                () => Ads.ShowInterstitial(() => PlayAgain?.Invoke()), 54f, IconKind.Restart);
            UIFactory.Place((RectTransform)again.transform, new Vector2(0.5f, 0f), new Vector2(210f, 120f), new Vector2(400f, 150f));
            var menu = UIFactory.AddGameButton(card, "Menu", Palette.Neutral, new Vector2(360f, 150f),
                () => Ads.ShowInterstitial(() => Menu?.Invoke()), 54f, IconKind.Home);
            UIFactory.Place((RectTransform)menu.transform, new Vector2(0.5f, 0f), new Vector2(-210f, 120f), new Vector2(360f, 150f));

            bool celebrate = !vsComputer || humanWon;
            App.Instance.Audio.Play(celebrate ? Sfx.Win : Sfx.Lose);
            Haptics.Play(celebrate ? HapticKind.Success : HapticKind.Warning);
            if (celebrate) Effects.Confetti(UI.FxLayer, LudoTheme.SeatColors);
        }

        public override bool HandleBack()
        {
            Menu?.Invoke();
            return true;
        }
    }

    public sealed class LudoHowToOverlay : UIOverlay
    {
        protected override void Build()
        {
            var card = BuildCard(920f, 1500f, Palette.Card);
            var title = UIFactory.AddLabel(card, "Title", "How to Play", 80f, Color.white, FontWeight.ExtraBold);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(800f, 110f));

            var viewport = UIFactory.AddRect("Viewport", card);
            UIFactory.Stretch(viewport, 60f, 180f, 60f, 230f);
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

            var text = UIFactory.AddLabel(content, "Rules", RulesText(LudoPrefs.LoadRules()), 44f, Palette.Lighten(Palette.TextDim, 0.4f), FontWeight.Medium, TextAnchor.UpperLeft);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            // Baloo 2 has tall line metrics; tighten paragraphs so more rules fit on screen.
            text.lineSpacing = 0.82f;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;

            var close = UIFactory.AddGameButton(card, "Got it", Palette.Green, new Vector2(520f, 150f), () => UI.CloseOverlay(this), 58f, IconKind.Check);
            UIFactory.Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(520f, 150f));
        }

        public static string RulesText(LudoRules rules)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<b><color=#FFFFFF>Goal</color></b>");
            sb.AppendLine("Race all four of your tokens around the board and into your home triangle before everyone else.");
            sb.AppendLine();
            sb.AppendLine("<b><color=#FFFFFF>Your turn</color></b>");
            sb.AppendLine("Tap the dice to roll, then tap a glowing token to move it that many squares, clockwise.");
            sb.AppendLine(rules.sixToStart
                ? "You need a 6 to bring a token out of your yard onto your start square."
                : "Roll a 1 or a 6 to bring a token out of your yard onto your start square.");
            sb.AppendLine();
            sb.AppendLine("<b><color=#FFFFFF>Bonus rolls</color></b>");
            if (rules.extraTurnOnSix) sb.AppendLine("Rolling a 6 gives you another roll.");
            if (rules.threeSixesForfeit) sb.AppendLine("Three 6s in a row and your turn is lost.");
            if (rules.extraTurnOnCapture) sb.AppendLine("Capturing a token gives you another roll.");
            if (rules.extraTurnOnHome) sb.AppendLine("Getting a token home gives you another roll.");
            if (!rules.extraTurnOnSix && !rules.extraTurnOnCapture && !rules.extraTurnOnHome) sb.AppendLine("No bonus rolls with the current house rules.");
            sb.AppendLine();
            sb.AppendLine("<b><color=#FFFFFF>Capturing</color></b>");
            sb.AppendLine("Land exactly on an opponent's token to send it back to their yard.");
            sb.AppendLine(rules.safeSquares
                ? "Tokens on a start square or a star square are safe and cannot be captured."
                : "Safe squares are switched off: any square is fair game.");
            sb.AppendLine();
            sb.AppendLine("<b><color=#FFFFFF>Going home</color></b>");
            sb.AppendLine("After a full lap your token turns into your coloured home column, where no one can touch it. You need the exact number to reach home.");
            sb.AppendLine();
            sb.AppendLine("<b><color=#FFFFFF>Winning</color></b>");
            sb.Append(rules.endWhenHumansFinish
                ? "Players are ranked in the order they finish. The game ends when every human player has finished, or only one player is left."
                : "Players are ranked in the order they finish. The game ends when only one player is left.");
            return sb.ToString();
        }
    }
}
