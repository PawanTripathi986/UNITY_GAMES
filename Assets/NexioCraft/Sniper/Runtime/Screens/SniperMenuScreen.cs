using System;
using NexioCraft.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NexioCraft.Sniper
{
    /// <summary>Steel Sniper home: next mission, rifle upgrades, and every mission grouped by district.</summary>
    public sealed class SniperMenuScreen : UIScreen
    {
        const float TileSize = 176f;
        const float TileGap = 18f;

        protected override void Build()
        {
            var progress = SniperPrefs.Progress;
            UIFactory.AddHeader(SafeRoot, "Steel Sniper", App.IsCollection ? (Action)(() => App.Instance.GoHome()) : null);
            SniperUi.CoinChip(SafeRoot, new Vector2(1f, 1f), new Vector2(-40f - 125f, -210f));

            var viewport = UIFactory.AddRect("List", SafeRoot);
            UIFactory.Stretch(viewport, 0f, 270f, 0f, 0f);
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = UIFactory.AddRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            float y = 20f;
            y = BuildHero(content, progress, y);
            for (int district = 0; district < SniperMissions.DistrictCount; district++)
                y = BuildDistrict(content, progress, district, y);
            content.sizeDelta = new Vector2(0f, y + 60f);

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 40f;
        }

        static int NextMission(SniperProgress progress)
        {
            int unlocked = progress.Unlocked;
            if (progress.StarsFor(unlocked) == 0) return unlocked;
            // Everything played: suggest the first mission still missing a star.
            for (int i = 0; i < SniperMissions.Count; i++)
                if (progress.StarsFor(i) < 3) return i;
            return SniperMissions.Count - 1;
        }

        float BuildHero(RectTransform content, SniperProgress progress, float y)
        {
            const float height = 560f;
            var holder = Row(content, y, height);
            UIFactory.AddShadow(holder, "Shadow", 24f, 0.45f, new Vector2(0f, -16f));
            var card = UIFactory.AddPanel(holder, "Hero", Palette.Card, 56f);
            UIFactory.Stretch(card.rectTransform);

            var rifle = UIFactory.AddImage(card.rectTransform, "Rifle", SniperArt.Rifle, Color.white);
            UIFactory.Place(rifle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -115f), new Vector2(720f, 220f));

            var mission = SniperMissions.Get(NextMission(progress));
            var label = UIFactory.AddLabel(card.rectTransform, "Label", $"MISSION {mission.Number}  /  {SniperMissions.DistrictNames[mission.District].ToUpperInvariant()}", 30f, SniperArt.Accent, FontWeight.ExtraBold);
            UIFactory.Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -255f), new Vector2(880f, 40f));
            var title = UIFactory.AddLabel(card.rectTransform, "Title", mission.Title, 70f, Color.white, FontWeight.ExtraBold);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -318f), new Vector2(880f, 90f));
            UIFactory.AddTextShadow(title, 5f, 0.35f);

            var upgrade = UIFactory.AddGameButton(card.rectTransform, "Rifle", Palette.Orange, new Vector2(310f, 140f),
                () => UI.Show<SniperUpgradeScreen>(), 52f, IconKind.Gear);
            UIFactory.Place((RectTransform)upgrade.transform, new Vector2(0.5f, 0f), new Vector2(-255f, 105f), new Vector2(310f, 140f));
            var play = UIFactory.AddGameButton(card.rectTransform, "Play", Palette.Green, new Vector2(450f, 140f),
                () => StartMission(mission.Index), 60f, IconKind.Play);
            UIFactory.Place((RectTransform)play.transform, new Vector2(0.5f, 0f), new Vector2(165f, 105f), new Vector2(450f, 140f));
            play.gameObject.AddComponent<Pulse>().Amplitude = 0.04f;
            return y + height + 50f;
        }

        float BuildDistrict(RectTransform content, SniperProgress progress, int district, float y)
        {
            int first = district * SniperMissions.PerDistrict;
            int stars = 0;
            for (int i = 0; i < SniperMissions.PerDistrict; i++) stars += progress.StarsFor(first + i);

            var header = Row(content, y, 80f);
            var name = UIFactory.AddLabel(header, "District", SniperMissions.DistrictNames[district], 56f, Color.white, FontWeight.ExtraBold, TextAnchor.MiddleLeft);
            UIFactory.Stretch(name.rectTransform, 10f, 0f, 200f, 0f);
            var starIcon = UIFactory.AddImage(header, "Star", Icons.Get(IconKind.Star), Palette.Gold);
            UIFactory.Place(starIcon.rectTransform, new Vector2(1f, 0.5f), new Vector2(-170f, 0f), new Vector2(48f, 48f));
            var count = UIFactory.AddLabel(header, "Stars", $"{stars}/{SniperMissions.PerDistrict * 3}", 42f, Palette.TextDim, FontWeight.ExtraBold, TextAnchor.MiddleLeft);
            UIFactory.Place(count.rectTransform, new Vector2(1f, 0.5f), new Vector2(-70f, 0f), new Vector2(130f, 50f));
            y += 100f;

            const int columns = 5;
            int rows = Mathf.CeilToInt(SniperMissions.PerDistrict / (float)columns);
            var grid = Row(content, y, rows * TileSize + (rows - 1) * TileGap);
            float gridWidth = columns * TileSize + (columns - 1) * TileGap;
            int next = NextMission(progress);
            for (int i = 0; i < SniperMissions.PerDistrict; i++)
            {
                int index = first + i;
                float x = -gridWidth * 0.5f + TileSize * 0.5f + (i % columns) * (TileSize + TileGap);
                float ty = -(TileSize * 0.5f + (i / columns) * (TileSize + TileGap));
                Tile(grid, SniperMissions.Get(index), progress, new Vector2(x, ty), index == next);
            }
            return y + rows * TileSize + (rows - 1) * TileGap + 60f;
        }

        void Tile(RectTransform grid, MissionDef mission, SniperProgress progress, Vector2 position, bool highlight)
        {
            bool unlocked = progress.IsUnlocked(mission.Index);
            var tile = UIFactory.AddPanel(grid, "Mission " + mission.Number, unlocked ? Palette.CardRaised : Palette.Inset, 36f, true);
            UIFactory.Place(tile.rectTransform, new Vector2(0.5f, 1f), position, new Vector2(TileSize, TileSize));
            if (highlight)
            {
                var ring = UIFactory.AddPanel(tile.rectTransform, "Current", SniperArt.Accent, 44f);
                UIFactory.Stretch(ring.rectTransform, -8f, -8f, -8f, -8f);
                ring.transform.SetAsFirstSibling();
                var inner = UIFactory.AddPanel(tile.rectTransform, "Inner", Palette.CardRaised, 36f);
                UIFactory.Stretch(inner.rectTransform);
                inner.transform.SetSiblingIndex(1);
                tile.color = Palette.WithAlpha(Palette.CardRaised, 0f);
            }

            if (!unlocked)
            {
                var lockIcon = UIFactory.AddImage(tile.rectTransform, "Lock", SniperArt.Icon(SniperIcon.Lock), Palette.Neutral);
                UIFactory.Place(lockIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(70f, 70f));
                var number = UIFactory.AddLabel(tile.rectTransform, "Number", mission.Number.ToString(), 34f, Palette.Neutral, FontWeight.ExtraBold);
                UIFactory.Place(number.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(160f, 40f));
            }
            else
            {
                var number = UIFactory.AddLabel(tile.rectTransform, "Number", mission.Number.ToString(), 70f, Color.white, FontWeight.ExtraBold);
                UIFactory.Place(number.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(160f, 90f));
                int earned = progress.StarsFor(mission.Index);
                for (int s = 0; s < 3; s++)
                {
                    var star = UIFactory.AddImage(tile.rectTransform, "Star", Icons.Get(IconKind.Star), s < earned ? Palette.Gold : new Color(1f, 1f, 1f, 0.15f));
                    UIFactory.Place(star.rectTransform, new Vector2(0.5f, 0f), new Vector2((s - 1) * 42f, 34f), new Vector2(38f, 38f));
                }
            }

            if (mission.Goal == MissionGoal.EliminateBoss)
            {
                var crown = UIFactory.AddImage(tile.rectTransform, "Boss", Icons.Get(IconKind.Crown), unlocked ? Palette.Gold : Palette.Neutral);
                UIFactory.Place(crown.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(40f, 40f));
            }

            UIFactory.MakeButton(tile.gameObject, () =>
            {
                if (unlocked) StartMission(mission.Index);
                else UI.Toast($"Finish mission {mission.Index} first", Palette.Neutral, 1.2f);
            });
            tile.gameObject.AddComponent<PressFeedback>();
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

        void StartMission(int index) => UI.Show<SniperGameScreen>(s => s.MissionIndex = index);

        public override bool HandleBack()
        {
            App.Instance.GoHome();
            return true;
        }
    }
}
