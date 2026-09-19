using System;
using NexioCraft.Core;
using UnityEngine;

namespace NexioCraft.Sort
{
    public sealed class SortWinOverlay : UIOverlay
    {
        public SortLevel Level;
        public int Moves;
        public Action Next;
        public Action Home;

        protected override bool CloseOnBackdropTap => false;

        protected override void Build()
        {
            var card = BuildCard(880f, 640f, Palette.Card);
            var title = UIFactory.AddLabel(card, "Title", "Level " + Level.Number + " done!", 84f, Color.white, FontWeight.ExtraBold);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(800f, 110f));
            UIFactory.AddTextShadow(title, 6f, 0.35f);

            string subtitle = $"Sorted {Level.Colours} colours in {Moves} moves";
            var text = UIFactory.AddLabel(card, "Subtitle", subtitle, 44f, Palette.TextDim, FontWeight.Bold);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -270f), new Vector2(760f, 120f));

            Ads.GameFinished();
            var home = UIFactory.AddGameButton(card, "Games", Palette.Neutral, new Vector2(320f, 150f),
                () => Ads.ShowInterstitial(() => Home?.Invoke()), 52f, IconKind.Home);
            UIFactory.Place((RectTransform)home.transform, new Vector2(0.5f, 0f), new Vector2(-230f, 120f), new Vector2(320f, 150f));
            var next = UIFactory.AddGameButton(card, "Next Level", Palette.Green, new Vector2(440f, 150f), () =>
            {
                UI.CloseOverlay(this);
                Ads.ShowInterstitial(() => Next?.Invoke());
            }, 52f, IconKind.Play);
            UIFactory.Place((RectTransform)next.transform, new Vector2(0.5f, 0f), new Vector2(170f, 120f), new Vector2(440f, 150f));

            App.Instance.Audio.Play(Sfx.TokenHome, 0.7f);
        }

        public override bool HandleBack()
        {
            UI.CloseOverlay(this);
            Next?.Invoke();
            return true;
        }
    }
}
