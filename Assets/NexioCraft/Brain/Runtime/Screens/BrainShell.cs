using System;
using NexioCraft.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NexioCraft.Brain
{
    /// <summary>Picker for the five brain games, with each game's record.</summary>
    public sealed class BrainMenuScreen : UIScreen
    {
        const float CardHeight = 224f;
        const float CardGap = 22f;

        protected override void Build()
        {
            UIFactory.AddHeader(SafeRoot, "Brain Games", () => App.Instance.GoHome());
            var subtitle = UIFactory.AddLabel(SafeRoot, "Subtitle", "Five quick games for memory, numbers and logic", 40f, Palette.TextDim, FontWeight.Medium);
            subtitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(900f, 90f), new Vector2(0.5f, 1f));

            var list = UIFactory.AddRect("Games", SafeRoot);
            UIFactory.Stretch(list, 40f, 280f, 40f, 40f);

            float y = 0f;
            AddCard(list, y, "Memory Match", "Find every pair", 0, BrainArt.SymbolColor(4), MemoryRecord(), () => UI.Show<MemoryMatchScreen>());
            y += CardHeight + CardGap;
            AddCard(list, y, "2048", "Merge tiles to reach 2048", 1, BrainArt.TileColor(64), ScoreRecord("2048"), () => UI.Show<Game2048Screen>());
            y += CardHeight + CardGap;
            AddCard(list, y, "Quick Maths", "60 seconds of sums", 2, BrainArt.SymbolColor(1), ScoreRecord("maths"), () => UI.Show<QuickMathsScreen>());
            y += CardHeight + CardGap;
            AddCard(list, y, "Sequence", "Repeat the pattern", 3, BrainArt.SymbolColor(2), LevelRecord(), () => UI.Show<SequenceScreen>());
            y += CardHeight + CardGap;
            AddCard(list, y, "Sliding Puzzle", "Put the numbers in order", 5, BrainArt.SymbolColor(8), SlideRecord(), () => UI.Show<SlidingPuzzleScreen>());
        }

        static string ScoreRecord(string game)
        {
            int best = BrainPrefs.BestScore(game);
            return best > 0 ? "Best " + best : "Not played yet";
        }

        static string LevelRecord()
        {
            int best = BrainPrefs.BestScore("sequence");
            return best > 0 ? "Best level " + best : "Not played yet";
        }

        static string MemoryRecord()
        {
            int size = BrainPrefs.GetChoice("memory", 1);
            int best = BrainPrefs.BestLowScore("memory", MemoryMatchScreen.VariantName(size));
            return best > 0 ? $"Best {best} moves ({MemoryMatchScreen.VariantName(size)})" : "Not played yet";
        }

        static string SlideRecord()
        {
            int size = BrainPrefs.GetChoice("slide", 1);
            int best = BrainPrefs.BestLowScore("slide", SlidingPuzzleScreen.VariantName(size));
            return best > 0 ? $"Best {best} moves ({SlidingPuzzleScreen.VariantName(size)})" : "Not played yet";
        }

        void AddCard(RectTransform parent, float y, string title, string subtitle, int symbol, Color color, string record, Action onClick)
        {
            var holder = UIFactory.AddRect(title, parent);
            holder.anchorMin = new Vector2(0f, 1f);
            holder.anchorMax = new Vector2(1f, 1f);
            holder.pivot = new Vector2(0.5f, 1f);
            holder.sizeDelta = new Vector2(0f, CardHeight);
            holder.anchoredPosition = new Vector2(0f, -y);

            var card = UIFactory.AddPanel(holder, "Card", Palette.Card, 44f, true);
            UIFactory.Stretch(card.rectTransform);
            UIFactory.MakeButton(card.gameObject, onClick);
            card.gameObject.AddComponent<PressFeedback>();

            var badge = UIFactory.AddPanel(card.rectTransform, "Badge", Palette.WithAlpha(color, 0.22f), 36f);
            UIFactory.Place(badge.rectTransform, new Vector2(0f, 0.5f), new Vector2(24f + 70f, 0f), new Vector2(140f, 140f));
            var icon = UIFactory.AddImage(badge.rectTransform, "Symbol", BrainArt.Symbol(symbol), Color.white);
            UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84f, 84f));

            var name = UIFactory.AddLabel(card.rectTransform, "Name", title, 56f, Color.white, FontWeight.ExtraBold, TextAnchor.LowerLeft);
            UIFactory.Stretch(name.rectTransform, 190f, 20f, 90f, 138f);
            var detail = UIFactory.AddLabel(card.rectTransform, "Detail", subtitle, 34f, Palette.TextDim, FontWeight.Medium, TextAnchor.MiddleLeft);
            UIFactory.Stretch(detail.rectTransform, 190f, 92f, 90f, 80f);
            var best = UIFactory.AddLabel(card.rectTransform, "Best", record, 32f, Palette.WithAlpha(color, 0.95f), FontWeight.Bold, TextAnchor.MiddleLeft);
            UIFactory.Stretch(best.rectTransform, 190f, 150f, 90f, 22f);

            var chevron = UIFactory.AddImage(card.rectTransform, "Open", Icons.Get(IconKind.Back), Palette.WithAlpha(Color.white, 0.55f));
            chevron.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f);
            UIFactory.Place(chevron.rectTransform, new Vector2(1f, 0.5f), new Vector2(-44f, 0f), new Vector2(46f, 46f));
        }

        public override bool HandleBack()
        {
            App.Instance.GoHome();
            return true;
        }
    }

    /// <summary>Shared scaffolding for a brain game: header, restart, three stat chips and a play area.</summary>
    public abstract class BrainGameScreen : UIScreen
    {
        const float HeaderHeight = 150f;
        const float StatsTop = 176f;
        const float StatsHeight = 118f;

        protected RectTransform Content { get; private set; }
        protected Vector2 ContentSize { get; private set; }

        readonly Text[] statLabels = new Text[3];
        readonly Text[] statValues = new Text[3];
        Vector2 laidOut;

        protected abstract string Title { get; }
        /// <summary>Space reserved under the play area for the game's own controls.</summary>
        protected virtual float BottomInset => 40f;

        protected override void Build()
        {
            UIFactory.AddHeader(SafeRoot, Title, () => UI.Show<BrainMenuScreen>(), HeaderHeight);
            var restart = UIFactory.AddIconButton(SafeRoot, IconKind.Restart, Palette.CardRaised, 104f, Restart);
            UIFactory.Place((RectTransform)restart.transform, new Vector2(1f, 1f), new Vector2(-40f - 52f, -30f - 52f), new Vector2(104f, 104f));

            var stats = UIFactory.AddRect("Stats", SafeRoot);
            stats.anchorMin = new Vector2(0f, 1f);
            stats.anchorMax = new Vector2(1f, 1f);
            stats.pivot = new Vector2(0.5f, 1f);
            stats.offsetMin = new Vector2(40f, 0f);
            stats.offsetMax = new Vector2(-40f, 0f);
            stats.sizeDelta = new Vector2(-80f, StatsHeight);
            stats.anchoredPosition = new Vector2(0f, -StatsTop);
            for (int i = 0; i < 3; i++)
            {
                var chip = UIFactory.AddPanel(stats, "Stat" + i, Palette.Card, 30f);
                chip.rectTransform.anchorMin = new Vector2(i / 3f, 0f);
                chip.rectTransform.anchorMax = new Vector2((i + 1) / 3f, 1f);
                chip.rectTransform.offsetMin = new Vector2(i == 0 ? 0f : 8f, 0f);
                chip.rectTransform.offsetMax = new Vector2(i == 2 ? 0f : -8f, 0f);
                statLabels[i] = UIFactory.AddLabel(chip.rectTransform, "Label", "", 30f, Palette.TextDim, FontWeight.Bold);
                UIFactory.Stretch(statLabels[i].rectTransform, 8f, 14f, 8f, 60f);
                statValues[i] = UIFactory.AddLabel(chip.rectTransform, "Value", "", 46f, Color.white, FontWeight.ExtraBold);
                UIFactory.Stretch(statValues[i].rectTransform, 8f, 52f, 8f, 10f);
            }

            Content = UIFactory.AddRect("Content", SafeRoot);
            UIFactory.Stretch(Content, 30f, StatsTop + StatsHeight + 24f, 30f, BottomInset);
            BuildGame();
        }

        protected abstract void BuildGame();
        protected abstract void LayoutGame(Vector2 size);
        protected abstract void Restart();

        protected void SetStat(int index, string label, string value)
        {
            statLabels[index].text = label.ToUpperInvariant();
            statValues[index].text = value;
        }

        protected void ShowResult(string title, string subtitle, bool celebrate, Action playAgain)
        {
            UI.PushOverlay<BrainResultOverlay>(o =>
            {
                o.Title = title;
                o.Subtitle = subtitle;
                o.Celebrate = celebrate;
                o.PlayAgain = playAgain;
                o.Menu = () => UI.Show<BrainMenuScreen>();
            });
        }

        protected virtual void Update()
        {
            var size = Content.rect.size;
            if (size.x > 0f && size.y > 0f && size != laidOut)
            {
                laidOut = size;
                ContentSize = size;
                LayoutGame(size);
            }
        }

        public override bool HandleBack()
        {
            UI.Show<BrainMenuScreen>();
            return true;
        }
    }

    public sealed class BrainResultOverlay : UIOverlay
    {
        public string Title;
        public string Subtitle;
        public bool Celebrate;
        public Action PlayAgain;
        public Action Menu;

        protected override bool CloseOnBackdropTap => false;

        protected override void Build()
        {
            var card = BuildCard(880f, 620f, Palette.Card);
            var title = UIFactory.AddLabel(card, "Title", Title, 92f, Color.white, FontWeight.ExtraBold);
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
            var menu = UIFactory.AddGameButton(card, "Games", Palette.Neutral, new Vector2(340f, 150f),
                () => Ads.ShowInterstitial(() => Menu?.Invoke()), 54f, IconKind.Home);
            UIFactory.Place((RectTransform)menu.transform, new Vector2(0.5f, 0f), new Vector2(-220f, 120f), new Vector2(340f, 150f));

            App.Instance.Audio.Play(Celebrate ? Sfx.Win : Sfx.Lose, 0.8f);
            Haptics.Play(Celebrate ? HapticKind.Success : HapticKind.Warning);
            if (Celebrate) Effects.Confetti(UI.FxLayer, new[] { BrainArt.Accent, Palette.Gold, Palette.Green, Palette.Blue, Color.white });
        }

        public override bool HandleBack()
        {
            Menu?.Invoke();
            return true;
        }
    }
}
