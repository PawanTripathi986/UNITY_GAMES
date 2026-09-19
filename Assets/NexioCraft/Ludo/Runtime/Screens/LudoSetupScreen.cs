using System.Collections.Generic;
using NexioCraft.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NexioCraft.Ludo
{
    /// <summary>Choose players, colour and difficulty (vs Computer) or who is human (Pass &amp; Play).</summary>
    public sealed class LudoSetupScreen : UIScreen
    {
        public LudoMode Mode = LudoMode.VsComputer;

        int players;
        int color;
        BotLevel level;
        readonly SeatKind[] passKinds = { SeatKind.Human, SeatKind.Human, SeatKind.Human, SeatKind.Human };

        RectTransform content;
        readonly List<Image> swatchRings = new List<Image>();
        RectTransform seatList;

        protected override void Build()
        {
            players = Mode == LudoMode.VsComputer ? LudoPrefs.SetupPlayers : LudoPrefs.PassPlayers;
            color = LudoPrefs.SetupColor;
            level = LudoPrefs.SetupLevel;

            UIFactory.AddHeader(SafeRoot, Mode == LudoMode.VsComputer ? "Play vs Computer" : "Pass & Play", () => UI.Show<LudoMenuScreen>());

            content = UIFactory.AddRect("Content", SafeRoot);
            UIFactory.Stretch(content, 50f, 200f, 50f, 260f);

            float y = 0f;
            y = AddSection("Players", y, 150f, section =>
            {
                var control = SegmentedControl.Create(section, new[] { "2", "3", "4" }, players - 2, Palette.Blue, new Vector2(620f, 116f), i =>
                {
                    players = i + 2;
                    if (Mode == LudoMode.VsComputer) LudoPrefs.SetupPlayers = players;
                    else LudoPrefs.PassPlayers = players;
                    RefreshSeats();
                });
                UIFactory.Place((RectTransform)control.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 116f));
            });

            if (Mode == LudoMode.VsComputer)
            {
                y = AddSection("Your colour", y, 190f, BuildColorPicker);
                y = AddSection("Computer skill", y, 150f, section =>
                {
                    var control = SegmentedControl.Create(section, new[] { "Easy", "Normal", "Hard" }, (int)level, Palette.Green, new Vector2(820f, 116f), i =>
                    {
                        level = (BotLevel)i;
                        LudoPrefs.SetupLevel = level;
                    });
                    UIFactory.Place((RectTransform)control.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 116f));
                });
                AddSection("Seats", y, 190f, section => seatList = section);
            }
            else
            {
                AddSection("Who is playing?", y, 560f, section => seatList = section);
            }
            RefreshSeats();

            var start = UIFactory.AddGameButton(SafeRoot, "Start Game", Palette.Green, new Vector2(820f, 166f), StartGame, 66f, IconKind.Play);
            UIFactory.Place((RectTransform)start.transform, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(820f, 166f));
        }

        float AddSection(string title, float y, float height, System.Action<RectTransform> fill)
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
            return y + 66f + height + 34f;
        }

        void BuildColorPicker(RectTransform section)
        {
            for (int seat = 0; seat < 4; seat++)
            {
                int index = seat;
                float x = (seat - 1.5f) * 190f;
                var ring = UIFactory.AddImage(section, "Selected", Sprites.Ring(0.12f), Color.white);
                UIFactory.Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(150f, 150f));
                swatchRings.Add(ring);
                var swatch = UIFactory.AddImage(section, LudoTheme.SeatNames[seat], Sprites.Circle, LudoTheme.Seat(seat), true);
                UIFactory.Place(swatch.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(118f, 118f));
                UIFactory.MakeButton(swatch.gameObject, () =>
                {
                    color = index;
                    LudoPrefs.SetupColor = color;
                    RefreshSeats();
                });
            }
        }

        void RefreshSeats()
        {
            for (int i = 0; i < swatchRings.Count; i++) swatchRings[i].enabled = i == color;
            if (seatList == null) return;
            for (int i = seatList.childCount - 1; i >= 0; i--) Destroy(seatList.GetChild(i).gameObject);

            var seats = SeatsFor(players, Mode == LudoMode.VsComputer ? color : 0);
            if (Mode == LudoMode.VsComputer)
            {
                float spacing = 820f / seats.Count;
                for (int i = 0; i < seats.Count; i++)
                {
                    int seat = seats[i];
                    bool human = seat == color;
                    float x = (i - (seats.Count - 1) * 0.5f) * spacing;
                    AddSeatChip(seatList, seat, human ? SeatKind.Human : SeatKind.Bot, human ? "You" : LudoTheme.SeatNames[seat], new Vector2(x, 0f));
                }
                return;
            }

            float rowHeight = 125f;
            float top = (seatList.rect.height > 0f ? seatList.rect.height : 560f) * 0.5f - rowHeight * 0.5f - 18f;
            for (int i = 0; i < 4; i++)
            {
                int seat = i;
                bool inGame = seats.Contains(seat);
                var row = UIFactory.AddRect(LudoTheme.SeatNames[seat] + " Row", seatList);
                row.anchorMin = new Vector2(0f, 0.5f);
                row.anchorMax = new Vector2(1f, 0.5f);
                row.sizeDelta = new Vector2(-60f, rowHeight);
                row.anchoredPosition = new Vector2(0f, top - i * (rowHeight + 6f));
                var group = row.gameObject.AddComponent<CanvasGroup>();
                group.alpha = inGame ? 1f : 0.35f;
                group.interactable = inGame;
                group.blocksRaycasts = inGame;

                var dot = UIFactory.AddImage(row, "Colour", Sprites.Circle, LudoTheme.Seat(seat));
                UIFactory.Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(50f, 0f), new Vector2(84f, 84f));
                var name = UIFactory.AddLabel(row, "Name", LudoTheme.SeatNames[seat], 50f, Color.white, FontWeight.ExtraBold, TextAnchor.MiddleLeft);
                UIFactory.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(120f, 0f), new Vector2(260f, 90f), new Vector2(0f, 0.5f));

                if (!inGame) continue;
                var toggle = SegmentedControl.Create(row, new[] { "Human", "Computer" }, passKinds[seat] == SeatKind.Human ? 0 : 1, Palette.Blue,
                    new Vector2(470f, 100f), choice => passKinds[seat] = choice == 0 ? SeatKind.Human : SeatKind.Bot);
                UIFactory.Place((RectTransform)toggle.transform, new Vector2(1f, 0.5f), new Vector2(-235f, 0f), new Vector2(470f, 100f));
            }
        }

        static void AddSeatChip(RectTransform parent, int seat, SeatKind kind, string label, Vector2 position)
        {
            var avatar = UIFactory.AddImage(parent, "Seat", Sprites.Circle, LudoTheme.Seat(seat));
            UIFactory.Place(avatar.rectTransform, new Vector2(0.5f, 0.5f), position + new Vector2(0f, 22f), new Vector2(100f, 100f));
            var icon = UIFactory.AddImage(avatar.rectTransform, "Icon", Icons.Get(kind == SeatKind.Human ? IconKind.Person : IconKind.Robot), Color.white);
            UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f));
            var name = UIFactory.AddLabel(parent, "Name", label, 36f, Color.white, FontWeight.Bold);
            UIFactory.Place(name.rectTransform, new Vector2(0.5f, 0.5f), position + new Vector2(0f, -62f), new Vector2(200f, 50f));
        }

        /// <summary>Seats used for a player count; two players sit opposite each other.</summary>
        public static List<int> SeatsFor(int count, int first)
        {
            switch (count)
            {
                case 2: return new List<int> { first, (first + 2) % 4 };
                case 3: return new List<int> { first, (first + 1) % 4, (first + 2) % 4 };
                default: return new List<int> { 0, 1, 2, 3 };
            }
        }

        void StartGame()
        {
            var kinds = new SeatKind[4];
            var levels = new BotLevel[4];
            int first;
            var seats = SeatsFor(players, Mode == LudoMode.VsComputer ? color : 0);
            if (Mode == LudoMode.VsComputer)
            {
                foreach (int seat in seats)
                {
                    kinds[seat] = seat == color ? SeatKind.Human : SeatKind.Bot;
                    levels[seat] = level;
                }
                first = color;
            }
            else
            {
                bool anyHuman = false;
                foreach (int seat in seats)
                {
                    kinds[seat] = passKinds[seat];
                    levels[seat] = BotLevel.Normal;
                    anyHuman |= passKinds[seat] == SeatKind.Human;
                }
                if (!anyHuman)
                {
                    UI.Toast("Make at least one player human", Palette.Red);
                    return;
                }
                first = seats[0];
            }

            var state = LudoEngine.NewGame(kinds, levels, LudoPrefs.LoadRules(), Mode, first);
            LudoPrefs.SaveGame(state);
            UI.Show<LudoGameScreen>(g => g.State = state);
        }

        public override bool HandleBack()
        {
            UI.Show<LudoMenuScreen>();
            return true;
        }
    }
}
