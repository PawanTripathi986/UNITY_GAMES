using NexioCraft.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NexioCraft.Chess
{
    public sealed class ChessMenuScreen : UIScreen
    {
        const float ButtonsBottom = 80f;
        const float PreviewFrame = 16f;
        // A lively Italian Game position for the menu board.
        const string PreviewFen = "r1bqk2r/pppp1ppp/2n2n2/2b1p3/2B1P3/2NP1N2/PPP2PPP/R1BQK2R b KQkq - 0 5";

        RectTransform preview;
        RawImage previewSquares;
        Image[] previewPieces;
        Image previewFrame;
        Vector2 laidOutArea;
        float buttonsHeight;

        protected override void Build()
        {
            var title = UIFactory.AddLabel(SafeRoot, "Title", "CHESS", 210f, Color.white, FontWeight.ExtraBold);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(40f, -215f), new Vector2(900f, 260f));
            var outline = title.gameObject.AddComponent<Outline>();
            outline.effectColor = Palette.Hex(0x3B2716);
            outline.effectDistance = new Vector2(5f, -5f);
            UIFactory.AddTextShadow(title, 14f, 0.35f);
            float titleWidth = title.preferredWidth;
            var knight = UIFactory.AddImage(SafeRoot, "Knight", ChessArt.PieceSprite(Piece.Knight), Color.white);
            knight.preserveAspect = true;
            UIFactory.Place(knight.rectTransform, new Vector2(0.5f, 1f), new Vector2(40f - titleWidth * 0.5f - 95f, -205f), new Vector2(190f, 190f));
            var tagline = UIFactory.AddLabel(SafeRoot, "Tagline", "Think. Plan. Checkmate.", 48f, Palette.TextDim, FontWeight.Bold);
            UIFactory.Place(tagline.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -385f), new Vector2(900f, 70f));

            if (App.IsCollection)
            {
                var home = UIFactory.AddIconButton(SafeRoot, IconKind.Home, Palette.CardRaised, 112f, () => App.Instance.GoHome());
                UIFactory.Place((RectTransform)home.transform, new Vector2(0f, 1f), new Vector2(40f + 56f, -40f - 56f), new Vector2(112f, 112f));
            }

            BuildPreview();

            var buttons = UIFactory.AddRect("Buttons", SafeRoot);
            buttons.anchorMin = buttons.anchorMax = new Vector2(0.5f, 0f);
            buttons.pivot = new Vector2(0.5f, 0f);
            buttons.sizeDelta = new Vector2(820f, 800f);
            buttons.anchoredPosition = new Vector2(0f, ButtonsBottom);

            float y = 70f;
            var rules = UIFactory.AddGameButton(buttons, "Rules", Palette.Neutral, new Vector2(400f, 128f), () => UI.PushOverlay<ChessHowToOverlay>(), 50f, IconKind.Help);
            UIFactory.Place((RectTransform)rules.transform, new Vector2(0.5f, 0f), new Vector2(-210f, y), new Vector2(400f, 128f));
            var settings = UIFactory.AddGameButton(buttons, "Settings", Palette.Neutral, new Vector2(400f, 128f), () => UI.Show<ChessSettingsScreen>(), 50f, IconKind.Gear);
            UIFactory.Place((RectTransform)settings.transform, new Vector2(0.5f, 0f), new Vector2(210f, y), new Vector2(400f, 128f));

            y += 190f;
            AddMainButton(buttons, "Pass & Play", Palette.Blue, y, IconKind.Person, StartPassAndPlay);
            y += 190f;
            AddMainButton(buttons, "Play vs Computer", Palette.Green, y, IconKind.Robot, () => UI.Show<ChessSetupScreen>());

            var saved = ChessPrefs.LoadSave();
            if (saved != null)
            {
                y += 190f;
                AddMainButton(buttons, "Continue Game", Palette.Orange, y, IconKind.Play, () => UI.Show<ChessGameScreen>(g => g.Save = saved));
            }
            buttonsHeight = y + 79f;

            string stats = ChessPrefs.Played > 0
                ? $"vs computer: {ChessPrefs.Wins} won, {ChessPrefs.Losses} lost, {ChessPrefs.Draws} drawn"
                : "Play the computer or a friend";
            var footer = UIFactory.AddLabel(SafeRoot, "Stats", stats, 38f, Palette.TextDim, FontWeight.Medium);
            UIFactory.Place(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(1000f, 60f));
        }

        void AddMainButton(RectTransform parent, string label, Color color, float y, IconKind icon, System.Action onClick)
        {
            var button = UIFactory.AddGameButton(parent, label, color, new Vector2(820f, 158f), onClick, 62f, icon);
            UIFactory.Place((RectTransform)button.transform, new Vector2(0.5f, 0f), new Vector2(0f, y), new Vector2(820f, 158f));
        }

        void StartPassAndPlay()
        {
            var save = new ChessSave { mode = (int)ChessMode.PassAndPlay, humanSide = Side.White };
            ChessPrefs.SaveGame(save);
            UI.Show<ChessGameScreen>(g => g.Save = save);
        }

        void BuildPreview()
        {
            preview = UIFactory.AddRect("Preview", SafeRoot);
            preview.anchorMin = preview.anchorMax = new Vector2(0.5f, 0f);
            UIFactory.AddShadow(preview, "Shadow", 24f, 0.5f, new Vector2(0f, -24f));
            previewFrame = UIFactory.AddPanel(preview, "Frame", ChessArt.Frame(ChessPrefs.Theme), 30f);
            UIFactory.Stretch(previewFrame.rectTransform);
            previewSquares = UIFactory.AddRaw(preview, "Squares", null);
            UIFactory.Stretch(previewSquares.rectTransform, PreviewFrame, PreviewFrame, PreviewFrame, PreviewFrame);

            var position = ChessPosition.FromFen(PreviewFen);
            previewPieces = new Image[64];
            for (int sq = 0; sq < 64; sq++)
            {
                if (position.Squares[sq] == 0) continue;
                var image = UIFactory.AddImage(preview, "Piece", ChessArt.PieceSprite(position.Squares[sq]), Color.white);
                image.preserveAspect = true;
                previewPieces[sq] = image;
            }
        }

        void LayoutPreview(Vector2 area)
        {
            laidOutArea = area;
            const float titleBottom = 430f;
            float buttonsTop = ButtonsBottom + buttonsHeight;
            float band = area.y - titleBottom - buttonsTop;
            float size = Mathf.Min(Mathf.Clamp(band - 170f, 300f, 760f), area.x - 180f);
            preview.sizeDelta = new Vector2(size, size);
            preview.anchoredPosition = new Vector2(0f, buttonsTop + band * 0.5f);

            float inner = size - PreviewFrame * 2f;
            float scale = UI.Canvas.scaleFactor > 0f ? UI.Canvas.scaleFactor : 1f;
            previewSquares.texture = ChessArt.Board(Mathf.RoundToInt(inner * scale), ChessPrefs.Theme);
            float cell = inner / 8f;
            for (int sq = 0; sq < 64; sq++)
            {
                var image = previewPieces[sq];
                if (image == null) continue;
                var center = new Vector2(((sq & 7) + 0.5f) * cell - inner * 0.5f, ((sq >> 3) + 0.5f) * cell - inner * 0.5f);
                UIFactory.Place(image.rectTransform, new Vector2(0.5f, 0.5f), center, new Vector2(cell * 0.95f, cell * 0.95f));
            }
        }

        void Update()
        {
            var area = SafeRoot.rect.size;
            if (area.x > 0f && area.y > 0f && area != laidOutArea) LayoutPreview(area);
            if (preview != null) preview.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(Time.unscaledTime * 0.7f) * 2.5f);
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
                o.Title = "Quit Chess?";
                o.Message = "Your game in progress is saved.";
                o.ConfirmLabel = "Quit";
                o.Confirmed = Application.Quit;
            });
            return true;
        }
    }

    public sealed class ChessSetupScreen : UIScreen
    {
        int sideChoice;
        ChessLevel level;

        protected override void Build()
        {
            sideChoice = ChessPrefs.SetupSide;
            level = ChessPrefs.SetupLevel;
            UIFactory.AddHeader(SafeRoot, "Play vs Computer", () => UI.Show<ChessMenuScreen>());

            var content = UIFactory.AddRect("Content", SafeRoot);
            UIFactory.Stretch(content, 50f, 220f, 50f, 300f);

            AddSection(content, "You play as", 0f, 230f, section =>
            {
                string[] labels = { "White", "Random", "Black" };
                int[] pieces = { Piece.King, 0, -Piece.King };
                for (int i = 0; i < 3; i++)
                {
                    int index = i;
                    var tile = UIFactory.AddPanel(section, labels[i], Palette.Inset, 36f, true);
                    UIFactory.Place(tile.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 290f, 0f), new Vector2(260f, 190f));
                    if (pieces[i] != 0)
                    {
                        var piece = UIFactory.AddImage(tile.rectTransform, "Piece", ChessArt.PieceSprite(pieces[i]), Color.white);
                        piece.preserveAspect = true;
                        UIFactory.Place(piece.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 22f), new Vector2(110f, 110f));
                    }
                    else
                    {
                        var white = UIFactory.AddImage(tile.rectTransform, "White", ChessArt.PieceSprite(Piece.King), Color.white);
                        white.preserveAspect = true;
                        UIFactory.Place(white.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-30f, 22f), new Vector2(96f, 96f));
                        var black = UIFactory.AddImage(tile.rectTransform, "Black", ChessArt.PieceSprite(-Piece.King), Color.white);
                        black.preserveAspect = true;
                        UIFactory.Place(black.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(30f, 22f), new Vector2(96f, 96f));
                    }
                    var label = UIFactory.AddLabel(tile.rectTransform, "Label", labels[i], 40f, Color.white, FontWeight.ExtraBold);
                    UIFactory.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(240f, 50f));
                    UIFactory.MakeButton(tile.gameObject, () =>
                    {
                        sideChoice = index;
                        ChessPrefs.SetupSide = index;
                        RefreshSideTiles(section);
                    });
                }
                RefreshSideTiles(section);
            });

            AddSection(content, "Computer level", 330f, 150f, section =>
            {
                var control = SegmentedControl.Create(section, new[] { "Easy", "Medium", "Hard" }, (int)level, Palette.Green, new Vector2(840f, 116f), i =>
                {
                    level = (ChessLevel)i;
                    ChessPrefs.SetupLevel = level;
                });
                UIFactory.Place((RectTransform)control.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(840f, 116f));
            });

            var hint = UIFactory.AddLabel(content, "Hint", "Easy makes mistakes, Medium plays solid moves, Hard looks several moves ahead.", 40f, Palette.TextDim, FontWeight.Medium, TextAnchor.UpperCenter);
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -580f), new Vector2(900f, 140f), new Vector2(0.5f, 1f));

            var start = UIFactory.AddGameButton(SafeRoot, "Start Game", Palette.Green, new Vector2(820f, 166f), StartGame, 66f, IconKind.Play);
            UIFactory.Place((RectTransform)start.transform, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(820f, 166f));
        }

        void RefreshSideTiles(RectTransform section)
        {
            for (int i = 0; i < section.childCount; i++)
            {
                var tile = section.GetChild(i).GetComponent<Image>();
                if (tile != null) tile.color = i == sideChoice ? Palette.Blue : Palette.Inset;
            }
        }

        static void AddSection(RectTransform content, string title, float y, float height, System.Action<RectTransform> fill)
        {
            var label = UIFactory.AddLabel(content, title + " Label", title.ToUpperInvariant(), 40f, Palette.TextDim, FontWeight.ExtraBold, TextAnchor.MiddleLeft);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.anchoredPosition = new Vector2(10f, -y);
            labelRect.sizeDelta = new Vector2(0f, 60f);

            var card = UIFactory.AddPanel(content, title, Palette.Card, 44f);
            var rt = card.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -(y + 66f));
            rt.sizeDelta = new Vector2(0f, height);
            fill(rt);
        }

        void StartGame()
        {
            int side = sideChoice == 0 ? Side.White : sideChoice == 2 ? Side.Black : (Random.value < 0.5f ? Side.White : Side.Black);
            var save = new ChessSave
            {
                mode = (int)ChessMode.VsComputer,
                humanSide = side,
                level = (int)level,
                flipped = side == Side.Black
            };
            ChessPrefs.SaveGame(save);
            UI.Show<ChessGameScreen>(g => g.Save = save);
        }

        public override bool HandleBack()
        {
            UI.Show<ChessMenuScreen>();
            return true;
        }
    }

    public sealed class ChessSettingsScreen : UIScreen
    {
        protected override void Build()
        {
            UIFactory.AddHeader(SafeRoot, "Settings", () => UI.Show<ChessMenuScreen>());
            var list = UIFactory.AddRect("List", SafeRoot);
            UIFactory.Stretch(list, 40f, 190f, 40f, 40f);

            float y = 0f;
            y = AddToggle(list, y, "Sound", "Piece moves, captures and checks", Settings.Sound, Settings.SetSound);
            y = AddToggle(list, y, "Vibration", "Haptic taps on moves and checks", Settings.Vibration, Settings.SetVibration);
            y = AddToggle(list, y, "Show legal moves", "Dots on the squares a piece can reach", ChessPrefs.ShowLegalMoves, on => ChessPrefs.ShowLegalMoves = on);

            var label = UIFactory.AddLabel(list, "Theme Label", "BOARD COLOURS", 40f, Palette.TextDim, FontWeight.ExtraBold, TextAnchor.LowerLeft);
            Row(label.rectTransform, y + 20f, 70f, 16f);
            y += 100f;
            var themeCard = UIFactory.AddPanel(list, "Theme", Palette.Card, 40f);
            Row(themeCard.rectTransform, y, 300f, 0f);
            var swatches = UIFactory.AddRect("Swatches", themeCard.rectTransform);
            UIFactory.Stretch(swatches);
            string[] names = { "Classic", "Green", "Blue" };
            var tiles = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                var tile = UIFactory.AddPanel(swatches, names[i], Palette.Inset, 30f, true);
                UIFactory.Place(tile.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 300f, 0f), new Vector2(270f, 250f));
                var board = UIFactory.AddRaw(tile.rectTransform, "Board", ChessArt.Board(160, (BoardTheme)i));
                UIFactory.Place(board.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -95f), new Vector2(150f, 150f));
                var text = UIFactory.AddLabel(tile.rectTransform, "Name", names[i], 38f, Color.white, FontWeight.ExtraBold);
                UIFactory.Place(text.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 38f), new Vector2(240f, 50f));
                tiles[i] = tile;
                UIFactory.MakeButton(tile.gameObject, () =>
                {
                    ChessPrefs.Theme = (BoardTheme)index;
                    for (int k = 0; k < tiles.Length; k++) tiles[k].color = k == index ? Palette.Blue : Palette.Inset;
                });
            }
            int current = (int)ChessPrefs.Theme;
            for (int k = 0; k < tiles.Length; k++) tiles[k].color = k == current ? Palette.Blue : Palette.Inset;
        }

        static float AddToggle(RectTransform list, float y, string title, string subtitle, bool value, System.Action<bool> changed)
        {
            var card = UIFactory.AddPanel(list, title, Palette.Card, 40f);
            Row(card.rectTransform, y, 170f, 0f);
            var name = UIFactory.AddLabel(card.rectTransform, "Title", title, 50f, Color.white, FontWeight.ExtraBold, TextAnchor.LowerLeft);
            UIFactory.Stretch(name.rectTransform, 44f, 20f, 220f, 78f);
            var detail = UIFactory.AddLabel(card.rectTransform, "Subtitle", subtitle, 36f, Palette.TextDim, FontWeight.Medium, TextAnchor.UpperLeft);
            UIFactory.Stretch(detail.rectTransform, 44f, 98f, 220f, 14f);
            var toggle = SwitchControl.Create(card.rectTransform, value, Palette.Green, changed);
            UIFactory.Place((RectTransform)toggle.transform, new Vector2(1f, 0.5f), new Vector2(-40f - 75f, 0f), new Vector2(150f, 86f));
            return y + 170f + 14f;
        }

        static void Row(RectTransform rt, float y, float height, float inset)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(inset, 0f);
            rt.offsetMax = new Vector2(-inset, 0f);
            rt.sizeDelta = new Vector2(-inset * 2f, height);
            rt.anchoredPosition = new Vector2(0f, -y);
        }

        public override bool HandleBack()
        {
            UI.Show<ChessMenuScreen>();
            return true;
        }
    }
}
