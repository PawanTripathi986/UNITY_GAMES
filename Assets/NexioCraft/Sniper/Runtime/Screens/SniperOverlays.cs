using System;
using System.Collections;
using NexioCraft.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NexioCraft.Sniper
{
    /// <summary>Small shared UI pieces for the sniper screens.</summary>
    static class SniperUi
    {
        /// <summary>Coin balance pill; returns the label so the caller can update it.</summary>
        public static Text CoinChip(Transform parent, Vector2 anchor, Vector2 position)
        {
            var chip = UIFactory.AddPanel(parent, "Coins", Palette.Inset, 40f);
            UIFactory.Place(chip.rectTransform, anchor, position, new Vector2(250f, 90f));
            var icon = UIFactory.AddImage(chip.rectTransform, "Coin", SniperArt.Icon(SniperIcon.Coin), Color.white);
            UIFactory.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(48f, 0f), new Vector2(62f, 62f));
            var label = UIFactory.AddLabel(chip.rectTransform, "Amount", SniperPrefs.Progress.Coins.ToString(), 48f, Color.white, FontWeight.ExtraBold, TextAnchor.MiddleLeft);
            UIFactory.Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(160f, 2f), new Vector2(160f, 60f));
            return label;
        }

        public static string DurationText(float seconds)
        {
            int s = Mathf.CeilToInt(seconds);
            return $"{s / 60}:{s % 60:00}";
        }

        /// <summary>Icon plus a two-line value/caption, for mission intel.</summary>
        public static void Intel(Transform parent, Vector2 position, Sprite icon, Color iconColour, string value, string caption)
        {
            var cell = UIFactory.AddRect("Intel", parent);
            UIFactory.Place(cell, new Vector2(0.5f, 1f), position, new Vector2(390f, 110f));
            var back = UIFactory.AddPanel(cell, "Back", Palette.Inset, 30f);
            UIFactory.Stretch(back.rectTransform);
            var image = UIFactory.AddImage(cell, "Icon", icon, iconColour);
            UIFactory.Place(image.rectTransform, new Vector2(0f, 0.5f), new Vector2(58f, 0f), new Vector2(58f, 58f));
            var valueText = UIFactory.AddLabel(cell, "Value", value, 42f, Color.white, FontWeight.ExtraBold, TextAnchor.MiddleLeft);
            UIFactory.Place(valueText.rectTransform, new Vector2(0f, 0.5f), new Vector2(250f, 16f), new Vector2(280f, 50f));
            var captionText = UIFactory.AddLabel(cell, "Caption", caption, 26f, Palette.TextDim, FontWeight.Bold, TextAnchor.MiddleLeft);
            UIFactory.Place(captionText.rectTransform, new Vector2(0f, 0.5f), new Vector2(250f, -26f), new Vector2(280f, 34f));
        }
    }

    public sealed class SniperBriefingOverlay : UIOverlay
    {
        public MissionDef Mission;
        public Action Start;
        public Action Back;

        protected override bool CloseOnBackdropTap => false;

        protected override void Build()
        {
            var card = BuildCard(920f, 1240f, Palette.Card);
            var m = Mission;

            var header = UIFactory.AddLabel(card, "Header", $"MISSION {m.Number}  /  {SniperMissions.DistrictNames[m.District].ToUpperInvariant()}", 32f, SniperArt.Accent, FontWeight.ExtraBold);
            UIFactory.Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(840f, 44f));
            var title = UIFactory.AddLabel(card, "Title", m.Title, 84f, Color.white, FontWeight.ExtraBold);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(840f, 110f));
            UIFactory.AddTextShadow(title, 6f, 0.35f);

            var briefing = UIFactory.AddLabel(card, "Briefing", m.Briefing, 38f, Palette.TextDim, FontWeight.Medium, TextAnchor.UpperCenter);
            briefing.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place(briefing.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -330f), new Vector2(800f, 180f));

            bool boss = m.Goal == MissionGoal.EliminateBoss;
            float row0 = -490f, row1 = -610f, row2 = -730f;
            SniperUi.Intel(card, new Vector2(-205f, row0), Icons.Get(IconKind.Robot), boss ? SniperArt.Boss : SniperArt.Hostile,
                boss ? "Boss" : m.Hostiles.ToString(), boss ? $"+ {m.Hostiles - 1} guards" : m.Hostiles == 1 ? "hostile" : "hostiles");
            SniperUi.Intel(card, new Vector2(205f, row0), Icons.Get(IconKind.Robot), SniperArt.Civilian,
                m.Civilians.ToString(), m.Civilians == 1 ? "civilian" : "civilians");
            SniperUi.Intel(card, new Vector2(-205f, row1), SniperArt.Icon(SniperIcon.Target), Color.white,
                $"{Mathf.RoundToInt(m.MinRange)}-{Mathf.RoundToInt(m.MaxRange)}", "metres");
            SniperUi.Intel(card, new Vector2(205f, row1), SniperArt.Icon(SniperIcon.Wind), new Color(0.7f, 0.9f, 1f),
                m.WindSpeed.ToString("0.0"), "m/s wind");
            SniperUi.Intel(card, new Vector2(-205f, row2), SniperArt.Icon(SniperIcon.Clock), Color.white,
                SniperUi.DurationText(m.TimeLimit), "time limit");
            SniperUi.Intel(card, new Vector2(205f, row2), SniperArt.Icon(SniperIcon.Coin), Color.white,
                m.Reward.ToString(), "coins");

            var stars = UIFactory.AddLabel(card, "Stars", "Stars:  finish  /  no misses  /  all headshots", 32f, Palette.TextDim, FontWeight.Bold);
            UIFactory.Place(stars.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -830f), new Vector2(840f, 44f));
            for (int i = 0; i < 3; i++)
            {
                var star = UIFactory.AddImage(card, "Star", Icons.Get(IconKind.Star), Palette.Gold);
                UIFactory.Place(star.rectTransform, new Vector2(0.5f, 1f), new Vector2((i - 1) * 70f, -900f), new Vector2(56f, 56f));
            }

            var back = UIFactory.AddGameButton(card, "Back", Palette.Neutral, new Vector2(300f, 150f), () =>
            {
                Back?.Invoke();
            }, 52f, IconKind.Back);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(-250f, 120f), new Vector2(300f, 150f));
            var start = UIFactory.AddGameButton(card, "Start", Palette.Green, new Vector2(460f, 150f), () =>
            {
                UI.CloseOverlay(this);
                Start?.Invoke();
            }, 60f, IconKind.Play);
            UIFactory.Place((RectTransform)start.transform, new Vector2(0.5f, 0f), new Vector2(160f, 120f), new Vector2(460f, 150f));
        }

        public override bool HandleBack()
        {
            Back?.Invoke();
            return true;
        }
    }

    public sealed class SniperPauseOverlay : UIOverlay
    {
        public Action Resume;
        public Action Restart;
        public Action Quit;

        protected override bool CloseOnBackdropTap => false;

        protected override void Build()
        {
            var card = BuildCard(800f, 860f, Palette.Card);
            var title = UIFactory.AddLabel(card, "Title", "Paused", 92f, Color.white, FontWeight.ExtraBold);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(700f, 120f));
            UIFactory.AddTextShadow(title, 6f, 0.35f);

            AddButton(card, "Resume", Palette.Green, IconKind.Play, -270f, () =>
            {
                UI.CloseOverlay(this);
                Resume?.Invoke();
            });
            AddButton(card, "Restart", Palette.Orange, IconKind.Restart, -440f, () => Restart?.Invoke());
            AddButton(card, "Quit Mission", Palette.Red, IconKind.Home, -610f, () => Quit?.Invoke());
        }

        void AddButton(RectTransform card, string label, Color colour, IconKind icon, float y, Action action)
        {
            var button = UIFactory.AddGameButton(card, label, colour, new Vector2(600f, 140f), action, 54f, icon);
            UIFactory.Place((RectTransform)button.transform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(600f, 140f));
        }

        public override bool HandleBack()
        {
            UI.CloseOverlay(this);
            Resume?.Invoke();
            return true;
        }
    }

    public sealed class SniperResultOverlay : UIOverlay
    {
        public MissionDef Mission;
        public MissionStats Stats;
        public int Coins;
        public Action Next;
        public Action Retry;
        public Action Menu;

        protected override bool CloseOnBackdropTap => false;

        readonly Image[] stars = new Image[3];
        Text coinsText;
        Button doubleButton;

        protected override void Build()
        {
            bool won = Stats.Completed;
            int starCount = MissionScoring.Stars(Stats);
            // No coins section on a failed mission, so the card is shorter.
            var card = BuildCard(920f, won ? 1250f : 1000f, Palette.Card);

            var title = UIFactory.AddLabel(card, "Title", won ? "Mission Complete" : "Mission Failed", 80f, won ? Color.white : Palette.Red, FontWeight.ExtraBold);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(860f, 110f));
            UIFactory.AddTextShadow(title, 6f, 0.35f);
            var subtitle = UIFactory.AddLabel(card, "Reason", ReasonText(), 42f, Palette.TextDim, FontWeight.Bold);
            UIFactory.Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(840f, 60f));

            for (int i = 0; i < 3; i++)
            {
                var star = UIFactory.AddImage(card, "Star", Icons.Get(IconKind.Star), new Color(1f, 1f, 1f, 0.12f));
                float lift = i == 1 ? 26f : 0f;
                UIFactory.Place(star.rectTransform, new Vector2(0.5f, 1f), new Vector2((i - 1) * 190f, -350f + lift), new Vector2(170f, 170f));
                stars[i] = star;
            }

            int missed = Mathf.Max(0, Stats.ShotsFired - Stats.Hits);
            Criterion(card, -510f, won, "Mission complete");
            Criterion(card, -590f, won && Stats.NoMisses, Stats.ShotsFired == 0 ? "No missed shots" : missed == 0 ? $"No missed shots ({Stats.Hits}/{Stats.ShotsFired})" : $"{missed} missed shot{(missed == 1 ? "" : "s")}");
            Criterion(card, -670f, won && Stats.AllHeadshots, Stats.Kills > 0 ? $"Every kill a headshot ({Stats.Headshots}/{Stats.Kills})" : "Every kill a headshot");

            if (won)
            {
                var coin = UIFactory.AddImage(card, "Coin", SniperArt.Icon(SniperIcon.Coin), Color.white);
                UIFactory.Place(coin.rectTransform, new Vector2(0.5f, 1f), new Vector2(-120f, -790f), new Vector2(90f, 90f));
                coinsText = UIFactory.AddLabel(card, "Coins", "+" + Coins, 72f, Palette.Gold, FontWeight.ExtraBold, TextAnchor.MiddleLeft);
                UIFactory.Place(coinsText.rectTransform, new Vector2(0.5f, 1f), new Vector2(60f, -790f), new Vector2(260f, 90f));

                if (Coins > 0 && Ads.RewardedReady)
                {
                    doubleButton = UIFactory.AddGameButton(card, "Double coins", Palette.Blue, new Vector2(520f, 120f), DoubleCoins, 46f, IconKind.Play);
                    UIFactory.Place((RectTransform)doubleButton.transform, new Vector2(0.5f, 1f), new Vector2(0f, -910f), new Vector2(520f, 120f));
                }
            }

            Ads.GameFinished();
            bool last = Mission.Index >= SniperMissions.Count - 1;
            var menu = UIFactory.AddGameButton(card, "Menu", Palette.Neutral, new Vector2(300f, 150f),
                () => Ads.ShowInterstitial(() => Menu?.Invoke()), 52f, IconKind.Home);
            UIFactory.Place((RectTransform)menu.transform, new Vector2(0.5f, 0f), new Vector2(-250f, 110f), new Vector2(300f, 150f));
            var primary = won && !last
                ? UIFactory.AddGameButton(card, "Next Mission", Palette.Green, new Vector2(460f, 150f), () => Ads.ShowInterstitial(() => Next?.Invoke()), 52f, IconKind.Play)
                : UIFactory.AddGameButton(card, won ? "Replay" : "Retry", Palette.Orange, new Vector2(460f, 150f), () => Ads.ShowInterstitial(() => Retry?.Invoke()), 52f, IconKind.Restart);
            UIFactory.Place((RectTransform)primary.transform, new Vector2(0.5f, 0f), new Vector2(160f, 110f), new Vector2(460f, 150f));

            App.Instance.Audio.Play(won ? Sfx.Win : Sfx.Lose, 0.85f);
            Haptics.Play(won ? HapticKind.Success : HapticKind.Warning);
            if (won) StartCoroutine(RevealStars(starCount));
        }

        string ReasonText()
        {
            switch (Stats.Outcome)
            {
                case MissionOutcome.Completed: return Mission.Title;
                case MissionOutcome.TargetEscaped: return Mission.Goal == MissionGoal.EliminateBoss ? "The boss got away" : "A target escaped";
                case MissionOutcome.CivilianHit: return "You hit a civilian";
                case MissionOutcome.OutOfTime: return "Out of time";
                default: return "Mission abandoned";
            }
        }

        void Criterion(RectTransform card, float y, bool met, string text)
        {
            var icon = UIFactory.AddImage(card, "Mark", Icons.Get(met ? IconKind.Check : IconKind.Close), met ? Palette.Green : Palette.Neutral);
            UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(-330f, y), new Vector2(50f, 50f));
            var label = UIFactory.AddLabel(card, "Criterion", text, 40f, met ? Color.white : Palette.TextDim, FontWeight.Bold, TextAnchor.MiddleLeft);
            UIFactory.Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(60f, y), new Vector2(700f, 60f));
        }

        IEnumerator RevealStars(int count)
        {
            yield return Tween.Delay(0.35f);
            for (int i = 0; i < count; i++)
            {
                var star = stars[i];
                star.color = Palette.Gold;
                App.Instance.Audio.Play(Sfx.Pop, 0.9f, 1f + i * 0.15f);
                Haptics.Play(HapticKind.Light);
                yield return Tween.Run(0.3f, t => star.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(0.3f, 1f, t), Ease.OutBack);
                Effects.Burst((RectTransform)star.transform.parent, star.rectTransform.anchoredPosition + new Vector2(0f, ((RectTransform)star.transform.parent).rect.height * 0.5f), Palette.Gold, 10, 120f, 18f);
            }
            if (count == 3) Effects.Confetti(UI.FxLayer, new[] { Palette.Gold, SniperArt.Accent, Palette.Green, Color.white });
        }

        void DoubleCoins()
        {
            if (doubleButton == null) return;
            Ads.OfferReward(UI, "Double coins", $"Watch a short video to get another {Coins} coins?", "Watch", earned =>
            {
                if (!earned || doubleButton == null) return;
                SniperPrefs.Progress.AddCoins(Coins);
                SniperPrefs.Save();
                coinsText.text = "+" + Coins * 2;
                App.Instance.Audio.Play(Sfx.Coin, 0.9f);
                Destroy(doubleButton.gameObject);
                doubleButton = null;
            });
        }

        public override bool HandleBack()
        {
            Menu?.Invoke();
            return true;
        }
    }
}
