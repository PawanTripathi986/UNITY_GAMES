using System;
using NexioCraft.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NexioCraft.Sniper
{
    /// <summary>Spend coins on the rifle, or watch a video for free coins.</summary>
    public sealed class SniperUpgradeScreen : UIScreen
    {
        const float RowHeight = 250f;

        static readonly SniperIcon[] statIcons = { SniperIcon.Power, SniperIcon.Stability, SniperIcon.Scope, SniperIcon.Reload, SniperIcon.Computer };
        static readonly Color[] statColours = { Palette.Red, Palette.Blue, SniperArt.Accent, Palette.Orange, Palette.Hex(0x9B6BFF) };

        Text coinsLabel;
        Text freeLabel;
        Button freeButton;
        readonly Action[] refreshers = new Action[Rifle.StatCount];

        protected override void Build()
        {
            UIFactory.AddHeader(SafeRoot, "Rifle Upgrades", () => UI.Show<SniperMenuScreen>());
            coinsLabel = SniperUi.CoinChip(SafeRoot, new Vector2(1f, 1f), new Vector2(-40f - 125f, -210f));

            var viewport = UIFactory.AddRect("List", SafeRoot);
            UIFactory.Stretch(viewport, 0f, 270f, 0f, 0f);
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = UIFactory.AddRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            float y = 10f;
            var rifleRow = Row(content, y, 250f);
            var rifle = UIFactory.AddImage(rifleRow, "Rifle", SniperArt.Rifle, Color.white);
            UIFactory.Place(rifle.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 232f));
            y += 270f;

            for (int i = 0; i < Rifle.StatCount; i++)
            {
                BuildStat(content, (RifleStat)i, y);
                y += RowHeight + 30f;
            }
            BuildFreeCoins(content, y);
            y += 220f;
            content.sizeDelta = new Vector2(0f, y + 60f);

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 40f;
            RefreshAll();
        }

        void BuildStat(RectTransform content, RifleStat stat, float y)
        {
            int index = (int)stat;
            var row = Row(content, y, RowHeight);
            UIFactory.AddShadow(row, "Shadow", 20f, 0.4f, new Vector2(0f, -12f));
            var card = UIFactory.AddPanel(row, "Card", Palette.Card, 44f);
            UIFactory.Stretch(card.rectTransform);

            var badge = UIFactory.AddPanel(card.rectTransform, "Badge", statColours[index], 40f);
            UIFactory.Place(badge.rectTransform, new Vector2(0f, 0.5f), new Vector2(40f + 60f, 20f), new Vector2(120f, 120f));
            var icon = UIFactory.AddImage(badge.rectTransform, "Icon", SniperArt.Icon(statIcons[index]), Color.white);
            UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(74f, 74f));

            var name = UIFactory.AddLabel(card.rectTransform, "Name", Rifle.Names[index], 50f, Color.white, FontWeight.ExtraBold, TextAnchor.MiddleLeft);
            UIFactory.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(190f + 260f, -52f), new Vector2(520f, 60f));
            var description = UIFactory.AddLabel(card.rectTransform, "Description", Rifle.Descriptions[index], 30f, Palette.TextDim, FontWeight.Medium, TextAnchor.UpperLeft);
            description.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(description.rectTransform, new Vector2(0f, 1f), new Vector2(190f + 260f, -118f), new Vector2(520f, 76f));

            int maxLevel = Rifle.MaxLevel(stat);
            var pips = new Image[maxLevel];
            for (int p = 0; p < maxLevel; p++)
            {
                var pip = UIFactory.AddPanel(card.rectTransform, "Pip", Palette.Inset, 8f);
                UIFactory.Place(pip.rectTransform, new Vector2(0f, 0f), new Vector2(40f + 22f + p * 50f, 42f), new Vector2(42f, 18f));
                pips[p] = pip;
            }
            var value = UIFactory.AddLabel(card.rectTransform, "Value", "", 32f, Color.white, FontWeight.Bold, TextAnchor.MiddleLeft);
            UIFactory.Place(value.rectTransform, new Vector2(0f, 0f), new Vector2(40f + maxLevel * 50f + 150f, 42f), new Vector2(300f, 44f));

            var button = UIFactory.AddGameButton(card.rectTransform, "0", Palette.Green, new Vector2(230f, 110f), () => Buy(stat), 44f);
            UIFactory.Place((RectTransform)button.transform, new Vector2(1f, 0f), new Vector2(-30f - 115f, 30f + 55f), new Vector2(230f, 110f));
            var buttonLabel = button.transform.Find("Face/Content/Label").GetComponent<Text>();
            var buttonFace = button.transform.Find("Face").GetComponent<Image>();
            var buttonEdge = button.transform.Find("Edge").GetComponent<Image>();

            refreshers[index] = () =>
            {
                var progress = SniperPrefs.Progress;
                int level = progress.Level(stat);
                for (int p = 0; p < pips.Length; p++) pips[p].color = p < level ? statColours[index] : Palette.Inset;
                int cost = progress.UpgradeCost(stat);
                value.text = cost < 0 ? Rifle.ValueText(stat, level) : $"{Rifle.ValueText(stat, level)}  >  {Rifle.ValueText(stat, level + 1)}";
                bool affordable = cost >= 0 && progress.Coins >= cost;
                buttonLabel.text = cost < 0 ? "MAX" : cost.ToString();
                var colour = cost < 0 ? Palette.Neutral : affordable ? Palette.Green : Palette.Darken(Palette.Neutral, 0.2f);
                buttonFace.color = colour;
                buttonEdge.color = Palette.Darken(colour, 0.32f);
            };
        }

        void BuildFreeCoins(RectTransform content, float y)
        {
            var row = Row(content, y, 190f);
            var card = UIFactory.AddPanel(row, "Free Coins", Palette.CardRaised, 44f);
            UIFactory.Stretch(card.rectTransform);
            var coin = UIFactory.AddImage(card.rectTransform, "Coin", SniperArt.Icon(SniperIcon.Coin), Color.white);
            UIFactory.Place(coin.rectTransform, new Vector2(0f, 0.5f), new Vector2(40f + 55f, 0f), new Vector2(110f, 110f));
            freeLabel = UIFactory.AddLabel(card.rectTransform, "Label", "", 40f, Color.white, FontWeight.ExtraBold, TextAnchor.MiddleLeft);
            UIFactory.Place(freeLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(190f + 180f, 0f), new Vector2(360f, 120f));
            freeLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            freeButton = UIFactory.AddGameButton(card.rectTransform, "Watch", Palette.Blue, new Vector2(260f, 120f), ClaimFreeCoins, 46f, IconKind.Play);
            UIFactory.Place((RectTransform)freeButton.transform, new Vector2(1f, 0.5f), new Vector2(-30f - 130f, 0f), new Vector2(260f, 120f));
        }

        void Update()
        {
            if (freeLabel == null) return;
            var progress = SniperPrefs.Progress;
            var now = DateTime.UtcNow;
            bool ready = progress.FreeCoinsReady(now) && Ads.RewardedReady;
            freeButton.gameObject.SetActive(ready);
            if (ready) freeLabel.text = $"+{SniperProgress.FreeCoinsAmount} free coins";
            else if (!progress.FreeCoinsReady(now)) freeLabel.text = "More free coins in " + SniperUi.DurationText((float)progress.FreeCoinsWait(now).TotalSeconds);
            else freeLabel.text = "Free coins coming soon";
        }

        void Buy(RifleStat stat)
        {
            var progress = SniperPrefs.Progress;
            int cost = progress.UpgradeCost(stat);
            if (cost < 0)
            {
                UI.Toast("Already maxed out", Palette.Neutral, 1f);
                return;
            }
            if (!progress.TryUpgrade(stat))
            {
                UI.Toast($"Need {cost - progress.Coins} more coins", Palette.Red, 1.2f);
                App.Instance.Audio.Play(Sfx.NoMove, 0.6f);
                return;
            }
            SniperPrefs.Save();
            App.Instance.Audio.Play(Sfx.Unlock, 0.9f);
            Haptics.Play(HapticKind.Success);
            UI.Toast(Rifle.Names[(int)stat] + " upgraded!", SniperArt.Accent, 1f);
            RefreshAll();
        }

        void ClaimFreeCoins()
        {
            Ads.OfferReward(UI, "Free coins", $"Watch a short video for {SniperProgress.FreeCoinsAmount} coins?", "Watch", earned =>
            {
                if (!earned) return;
                SniperPrefs.Progress.ClaimFreeCoins(DateTime.UtcNow);
                SniperPrefs.Save();
                App.Instance.Audio.Play(Sfx.Coin, 0.9f);
                RefreshAll();
            });
        }

        void RefreshAll()
        {
            coinsLabel.text = SniperPrefs.Progress.Coins.ToString();
            foreach (var refresh in refreshers) refresh?.Invoke();
        }

        static RectTransform Row(RectTransform content, float y, float height)
        {
            var row = UIFactory.AddRect("Row", content);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(50f, 0f);
            row.offsetMax = new Vector2(-50f, 0f);
            row.sizeDelta = new Vector2(-100f, height);
            row.anchoredPosition = new Vector2(0f, -y);
            return row;
        }

        public override bool HandleBack()
        {
            UI.Show<SniperMenuScreen>();
            return true;
        }
    }
}
