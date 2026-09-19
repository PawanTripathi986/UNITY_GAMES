using NexioCraft.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NexioCraft.Ludo
{
    public sealed class LudoMenuScreen : UIScreen
    {
        const float ButtonsBottom = 80f;
        const float PreviewFrame = 12f;

        static readonly Vector2[] PreviewPinSpots =
        {
            new Vector2(6.5f, 3.5f), new Vector2(3.5f, 8.5f), new Vector2(8.5f, 12.5f), new Vector2(12.5f, 6.5f)
        };

        RectTransform boardPreview;
        RawImage previewBoard;
        Image[] previewPins;
        Vector2 laidOutArea;
        float buttonsHeight;
        RectTransform[] letters;
        Vector2[] letterBase;

        protected override void Build()
        {
            BuildTitle();
            BuildBoardPreview();
            if (App.IsCollection)
            {
                var home = UIFactory.AddIconButton(SafeRoot, IconKind.Home, Palette.CardRaised, 112f, () => App.Instance.GoHome());
                UIFactory.Place((RectTransform)home.transform, new Vector2(0f, 1f), new Vector2(40f + 56f, -40f - 56f), new Vector2(112f, 112f));
            }

            var buttons = UIFactory.AddRect("Buttons", SafeRoot);
            buttons.anchorMin = new Vector2(0.5f, 0f);
            buttons.anchorMax = new Vector2(0.5f, 0f);
            buttons.pivot = new Vector2(0.5f, 0f);
            buttons.sizeDelta = new Vector2(820f, 800f);
            buttons.anchoredPosition = new Vector2(0f, ButtonsBottom);

            float y = 70f;
            var help = UIFactory.AddGameButton(buttons, "Rules", Palette.Neutral, new Vector2(400f, 128f),
                () => UI.PushOverlay<LudoHowToOverlay>(), 50f, IconKind.Help);
            UIFactory.Place((RectTransform)help.transform, new Vector2(0.5f, 0f), new Vector2(-210f, y), new Vector2(400f, 128f));
            var settings = UIFactory.AddGameButton(buttons, "Settings", Palette.Neutral, new Vector2(400f, 128f),
                () => UI.Show<LudoSettingsScreen>(), 50f, IconKind.Gear);
            UIFactory.Place((RectTransform)settings.transform, new Vector2(0.5f, 0f), new Vector2(210f, y), new Vector2(400f, 128f));

            y += 190f;
            AddMainButton(buttons, "Pass & Play", Palette.Blue, y, IconKind.Person, () => UI.Show<LudoSetupScreen>(s => s.Mode = LudoMode.PassAndPlay));
            y += 190f;
            AddMainButton(buttons, "Play vs Computer", Palette.Green, y, IconKind.Robot, () => UI.Show<LudoSetupScreen>(s => s.Mode = LudoMode.VsComputer));

            var saved = LudoPrefs.LoadGame();
            if (saved != null)
            {
                y += 190f;
                AddMainButton(buttons, "Continue Game", Palette.Orange, y, IconKind.Play, () => UI.Show<LudoGameScreen>(g => g.State = saved));
            }
            buttonsHeight = y + 79f;

            string stats = LudoPrefs.GamesPlayed > 0
                ? $"Games played {LudoPrefs.GamesPlayed}     Wins vs computer {LudoPrefs.Wins}"
                : "Classic Ludo for 2 to 4 players";
            var footer = UIFactory.AddLabel(SafeRoot, "Stats", stats, 38f, Palette.TextDim, FontWeight.Medium);
            UIFactory.Place(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(1000f, 60f));
        }

        void AddMainButton(RectTransform parent, string label, Color color, float y, IconKind icon, System.Action onClick)
        {
            var button = UIFactory.AddGameButton(parent, label, color, new Vector2(820f, 158f), onClick, 62f, icon);
            UIFactory.Place((RectTransform)button.transform, new Vector2(0.5f, 0f), new Vector2(0f, y), new Vector2(820f, 158f));
        }

        void BuildTitle()
        {
            var title = UIFactory.AddRect("Title", SafeRoot);
            UIFactory.Place(title, new Vector2(0.5f, 1f), new Vector2(0f, -215f), new Vector2(900f, 260f));
            const string word = "LUDO";
            letters = new RectTransform[word.Length];
            letterBase = new Vector2[word.Length];
            var labels = new Text[word.Length];
            float totalWidth = 0f;
            for (int i = 0; i < word.Length; i++)
            {
                var label = UIFactory.AddLabel(title, word[i].ToString(), word[i].ToString(), 250f, LudoTheme.Seat(i), FontWeight.ExtraBold);
                var outline = label.gameObject.AddComponent<Outline>();
                outline.effectColor = Palette.Darken(LudoTheme.Seat(i), 0.55f);
                outline.effectDistance = new Vector2(6f, -6f);
                UIFactory.AddTextShadow(label, 16f, 0.35f);
                labels[i] = label;
                totalWidth += label.preferredWidth;
            }
            const float spacing = 10f;
            totalWidth += spacing * (word.Length - 1);
            float x = -totalWidth * 0.5f;
            for (int i = 0; i < word.Length; i++)
            {
                float w = labels[i].preferredWidth;
                letterBase[i] = new Vector2(x + w * 0.5f, 0f);
                UIFactory.Place(labels[i].rectTransform, new Vector2(0.5f, 0.5f), letterBase[i], new Vector2(w + 20f, 300f));
                letters[i] = labels[i].rectTransform;
                x += w + spacing;
            }

            var tagline = UIFactory.AddLabel(SafeRoot, "Tagline", "Roll. Race. Capture. Win.", 48f, Palette.TextDim, FontWeight.Bold);
            UIFactory.Place(tagline.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -385f), new Vector2(900f, 70f));
        }

        void BuildBoardPreview()
        {
            boardPreview = UIFactory.AddRect("BoardPreview", SafeRoot);
            boardPreview.anchorMin = boardPreview.anchorMax = new Vector2(0.5f, 0f);
            UIFactory.AddShadow(boardPreview, "Shadow", 24f, 0.5f, new Vector2(0f, -24f));
            var frame = UIFactory.AddPanel(boardPreview, "Frame", LudoTheme.Frame, 34f);
            UIFactory.Stretch(frame.rectTransform);
            previewBoard = UIFactory.AddRaw(boardPreview, "Board", null);
            UIFactory.Stretch(previewBoard.rectTransform, PreviewFrame, PreviewFrame, PreviewFrame, PreviewFrame);

            // A few decorative pins, placed once the preview has a size.
            previewPins = new Image[PreviewPinSpots.Length];
            for (int seat = 0; seat < previewPins.Length; seat++)
            {
                var sprite = LudoArt.Token(seat);
                previewPins[seat] = UIFactory.AddImage(boardPreview, "Pin", sprite, Color.white);
                previewPins[seat].rectTransform.pivot = sprite.pivot / sprite.rect.size;
            }
        }

        /// <summary>Sizes the board preview to fill the space between the title and the buttons.</summary>
        void LayoutPreview(Vector2 area)
        {
            laidOutArea = area;
            const float titleBottom = 430f;
            float buttonsTop = ButtonsBottom + buttonsHeight;
            float band = area.y - titleBottom - buttonsTop;
            float size = Mathf.Min(Mathf.Clamp(band - 170f, 300f, 760f), area.x - 180f);
            boardPreview.sizeDelta = new Vector2(size, size);
            boardPreview.anchoredPosition = new Vector2(0f, buttonsTop + band * 0.5f);

            float inner = size - PreviewFrame * 2f;
            float scale = UI.Canvas.scaleFactor > 0f ? UI.Canvas.scaleFactor : 1f;
            previewBoard.texture = LudoArt.Board(Mathf.RoundToInt(inner * scale));

            float cell = inner / LudoBoard.Grid;
            for (int seat = 0; seat < previewPins.Length; seat++)
            {
                var sprite = previewPins[seat].sprite;
                var units = PreviewPinSpots[seat];
                float height = cell * 1.2f;
                var rt = previewPins[seat].rectTransform;
                UIFactory.Place(rt, new Vector2(0.5f, 0.5f),
                    new Vector2((units.x / LudoBoard.Grid - 0.5f) * inner, (units.y / LudoBoard.Grid - 0.5f) * inner - cell * 0.3f),
                    new Vector2(height * sprite.rect.width / sprite.rect.height, height), rt.pivot);
            }
        }

        void Update()
        {
            var area = SafeRoot.rect.size;
            if (area.x > 0f && area.y > 0f && area != laidOutArea) LayoutPreview(area);

            float t = Time.unscaledTime;
            if (boardPreview != null) boardPreview.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(t * 0.8f) * 3f);
            if (letters == null) return;
            for (int i = 0; i < letters.Length; i++)
                letters[i].anchoredPosition = letterBase[i] + new Vector2(0f, Mathf.Max(0f, Mathf.Sin(t * 3f - i * 0.6f)) * 16f);
        }

        public override bool HandleBack()
        {
            if (App.IsCollection)
            {
                App.Instance.GoHome();
                return true;
            }
            UI.PushOverlay<ConfirmOverlay>(o =>
            {
                o.Title = "Quit Ludo?";
                o.Message = "Your game in progress is saved.";
                o.ConfirmLabel = "Quit";
                o.Confirmed = Application.Quit;
            });
            return true;
        }
    }
}
