using UnityEngine;
using UnityEngine.UI;

namespace NexioCraft.Core
{
    /// <summary>
    /// Home screen of a multi-game build: a Continue card for the game you played last, then a grid of game
    /// tiles showing where you are in each one.
    /// </summary>
    public sealed class GameHubScreen : UIScreen
    {
        const float SideMargin = 50f;
        const float Gap = 30f;
        const float TileHeight = 430f;
        const float ContinueHeight = 250f;
        const float ListTop = 330f;

        protected override void Build()
        {
            var title = UIFactory.AddLabel(SafeRoot, "Title", "Game Night", 110f, Color.white, FontWeight.ExtraBold, TextAnchor.MiddleLeft);
            UIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(SideMargin, -150f), new Vector2(700f, 130f), new Vector2(0f, 0.5f));
            UIFactory.AddTextShadow(title, 8f, 0.35f);
            var subtitle = UIFactory.AddLabel(SafeRoot, "Subtitle", App.Games.Count + " games, no internet needed", 38f, Palette.TextDim, FontWeight.Bold, TextAnchor.MiddleLeft);
            UIFactory.Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(SideMargin, -238f), new Vector2(700f, 50f), new Vector2(0f, 0.5f));

            var settings = UIFactory.AddIconButton(SafeRoot, IconKind.Gear, Palette.CardRaised, 104f,
                () => UI.PushOverlay<HubSettingsOverlay>());
            UIFactory.Place((RectTransform)settings.transform, new Vector2(1f, 1f), new Vector2(-SideMargin - 52f, -170f), new Vector2(104f, 104f));

            var viewport = UIFactory.AddRect("Games", SafeRoot);
            UIFactory.Stretch(viewport, 0f, ListTop, 0f, 0f);
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = UIFactory.AddRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            float y = 16f;
            var last = App.LastPlayed;
            if (last != null)
            {
                AddContinueCard(content, last, y);
                y += ContinueHeight + Gap + 10f;
            }

            for (int i = 0; i < App.Games.Count; i++)
            {
                AddTile(content, App.Games[i], i, y);
                if (i % 2 == 1) y += TileHeight + Gap;
            }
            if (App.Games.Count % 2 == 1) y += TileHeight + Gap;
            content.sizeDelta = new Vector2(0f, y + 40f);

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 40f;
        }

        /// <summary>Wide card that jumps straight back into the last game played.</summary>
        void AddContinueCard(RectTransform content, GameInfo game, float y)
        {
            var holder = Row(content, y, ContinueHeight);
            UIFactory.AddShadow(holder, "Shadow", 22f, 0.4f, new Vector2(0f, -14f));
            var card = UIFactory.AddPanel(holder, "Continue", Palette.Card, 48f, true);
            UIFactory.Stretch(card.rectTransform);
            Open(card, game);

            var art = UIFactory.AddPanel(card.rectTransform, "Art", game.Accent, 40f);
            UIFactory.Place(art.rectTransform, new Vector2(0f, 0.5f), new Vector2(30f + 85f, 0f), new Vector2(170f, 170f));
            if (game.Artwork != null)
            {
                var image = UIFactory.AddRaw(art.rectTransform, "Icon", game.Artwork());
                UIFactory.Place(image.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, 140f));
            }

            var label = UIFactory.AddLabel(card.rectTransform, "Label", "CONTINUE", 30f, game.Accent, FontWeight.ExtraBold, TextAnchor.MiddleLeft);
            UIFactory.Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(230f, 52f), new Vector2(420f, 40f), new Vector2(0f, 0.5f));
            var name = UIFactory.AddLabel(card.rectTransform, "Name", game.Title, 62f, Color.white, FontWeight.ExtraBold, TextAnchor.MiddleLeft);
            UIFactory.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(230f, -4f), new Vector2(520f, 70f), new Vector2(0f, 0.5f));
            var status = UIFactory.AddLabel(card.rectTransform, "Status", StatusOf(game), 34f, Palette.TextDim, FontWeight.Bold, TextAnchor.MiddleLeft);
            UIFactory.Place(status.rectTransform, new Vector2(0f, 0.5f), new Vector2(230f, -62f), new Vector2(520f, 44f), new Vector2(0f, 0.5f));

            var play = UIFactory.AddImage(card.rectTransform, "Play", Icons.Get(IconKind.Play), Palette.WithAlpha(Color.white, 0.85f));
            UIFactory.Place(play.rectTransform, new Vector2(1f, 0.5f), new Vector2(-70f, 0f), new Vector2(60f, 60f));
        }

        /// <summary>One game tile in the two-column grid.</summary>
        void AddTile(RectTransform content, GameInfo game, int index, float y)
        {
            float tileWidth = (UIRoot.ReferenceWidth - SideMargin * 2f - Gap) * 0.5f;
            var holder = UIFactory.AddRect(game.Title, content);
            holder.anchorMin = new Vector2(0.5f, 1f);
            holder.anchorMax = new Vector2(0.5f, 1f);
            holder.pivot = new Vector2(0.5f, 1f);
            holder.sizeDelta = new Vector2(tileWidth, TileHeight);
            holder.anchoredPosition = new Vector2((index % 2 == 0 ? -1f : 1f) * (tileWidth + Gap) * 0.5f, -y);

            UIFactory.AddShadow(holder, "Shadow", 20f, 0.42f, new Vector2(0f, -12f));
            var card = UIFactory.AddPanel(holder, "Card", Palette.Card, 48f, true);
            UIFactory.Stretch(card.rectTransform);
            Open(card, game);

            // Artwork on a panel in the game's colour.
            var art = UIFactory.AddPanel(card.rectTransform, "Art", game.Accent, 40f);
            UIFactory.Place(art.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -24f - 100f), new Vector2(200f, 200f));
            if (game.Artwork != null)
            {
                var image = UIFactory.AddRaw(art.rectTransform, "Icon", game.Artwork());
                UIFactory.Place(image.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(168f, 168f));
            }

            var name = UIFactory.AddLabel(card.rectTransform, "Name", game.Title, 46f, Color.white, FontWeight.ExtraBold);
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -258f), new Vector2(tileWidth - 30f, 56f));

            var chip = UIFactory.AddPanel(card.rectTransform, "Status", Palette.Inset, 26f);
            UIFactory.Place(chip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(tileWidth - 50f, 62f));
            var status = UIFactory.AddLabel(chip.rectTransform, "Text", StatusOf(game), 30f, Palette.TextDim, FontWeight.Bold);
            UIFactory.Stretch(status.rectTransform, 10f, 0f, 10f, 0f);

            if (!game.IsNew) return;
            var badge = UIFactory.AddPanel(card.rectTransform, "New", Palette.Red, 20f);
            UIFactory.Place(badge.rectTransform, new Vector2(1f, 1f), new Vector2(-54f, -34f), new Vector2(92f, 44f));
            var badgeText = UIFactory.AddLabel(badge.rectTransform, "Text", "NEW", 26f, Color.white, FontWeight.ExtraBold);
            UIFactory.Stretch(badgeText.rectTransform);
        }

        static string StatusOf(GameInfo game)
        {
            string status = null;
            if (game.Status != null)
            {
                try
                {
                    status = game.Status();
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Status for {game.Id} failed: {e.Message}");
                }
            }
            return string.IsNullOrEmpty(status) ? "Tap to play" : status;
        }

        void Open(Image card, GameInfo game)
        {
            UIFactory.MakeButton(card.gameObject, () =>
            {
                App.NoteLastPlayed(game.Id);
                game.ShowMenu(UI);
            });
            card.gameObject.AddComponent<PressFeedback>();
        }

        static RectTransform Row(RectTransform content, float y, float height)
        {
            var row = UIFactory.AddRect("Row", content);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(SideMargin, 0f);
            row.offsetMax = new Vector2(-SideMargin, 0f);
            row.sizeDelta = new Vector2(-SideMargin * 2f, height);
            row.anchoredPosition = new Vector2(0f, -y);
            return row;
        }

        public override bool HandleBack()
        {
            UI.PushOverlay<ConfirmOverlay>(o =>
            {
                o.Title = "Quit?";
                o.Message = "Games in progress are saved.";
                o.ConfirmLabel = "Quit";
                o.Confirmed = Application.Quit;
            });
            return true;
        }
    }

    /// <summary>Sound and vibration for every game, from the home screen.</summary>
    public sealed class HubSettingsOverlay : UIOverlay
    {
        protected override void Build()
        {
            var card = BuildCard(820f, 560f, Palette.Card);
            var title = UIFactory.AddLabel(card, "Title", "Settings", 76f, Color.white, FontWeight.ExtraBold);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(700f, 100f));
            UIFactory.AddTextShadow(title, 5f, 0.3f);

            Toggle(card, -250f, IconKind.SoundOn, "Sound", Settings.Sound, Palette.Green, Settings.SetSound);
            Toggle(card, -370f, IconKind.Vibrate, "Vibration", Settings.Vibration, Palette.Blue, Settings.SetVibration);

            var close = UIFactory.AddGameButton(card, "Done", Palette.Neutral, new Vector2(340f, 130f), () => UI.CloseOverlay(this), 50f, IconKind.Check);
            UIFactory.Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0f, 100f), new Vector2(340f, 130f));
        }

        void Toggle(RectTransform card, float y, IconKind icon, string label, bool value, Color colour, System.Action<bool> changed)
        {
            var row = UIFactory.AddPanel(card, label, Palette.Inset, 32f);
            UIFactory.Place(row.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(700f, 100f));
            var image = UIFactory.AddImage(row.rectTransform, "Icon", Icons.Get(icon), colour);
            UIFactory.Place(image.rectTransform, new Vector2(0f, 0.5f), new Vector2(54f, 0f), new Vector2(52f, 52f));
            var text = UIFactory.AddLabel(row.rectTransform, "Label", label, 44f, Color.white, FontWeight.Bold, TextAnchor.MiddleLeft);
            UIFactory.Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(110f, 0f), new Vector2(380f, 60f), new Vector2(0f, 0.5f));
            var toggle = SwitchControl.Create(row.rectTransform, value, colour, changed);
            UIFactory.Place((RectTransform)toggle.transform, new Vector2(1f, 0.5f), new Vector2(-105f, 0f), new Vector2(150f, 86f));
        }
    }
}
