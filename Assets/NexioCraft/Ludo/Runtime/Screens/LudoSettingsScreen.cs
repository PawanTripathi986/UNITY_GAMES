using System;
using NexioCraft.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NexioCraft.Ludo
{
    public sealed class LudoSettingsScreen : UIScreen
    {
        LudoRules rules;
        RectTransform content;

        protected override void Build()
        {
            rules = LudoPrefs.LoadRules();
            UIFactory.AddHeader(SafeRoot, "Settings", () => UI.Show<LudoMenuScreen>());

            var viewport = UIFactory.AddRect("Viewport", SafeRoot);
            UIFactory.Stretch(viewport, 40f, 170f, 40f, 30f);
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();

            content = UIFactory.AddRect("Content", viewport);
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
            layout.spacing = 14f;
            layout.padding = new RectOffset(0, 0, 10, 60);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;

            AddSection("General");
            AddToggle("Sound", "Dice, moves and effects", Settings.Sound, Settings.SetSound);
            AddToggle("Vibration", "Haptic taps on rolls and captures", Settings.Vibration, Settings.SetVibration);
            AddToggle("Fast mode", "Quicker animations and computer turns", LudoPrefs.FastMode, on => LudoPrefs.FastMode = on);
            AddToggle("Auto move", "Move for you when there is only one choice", LudoPrefs.AutoMove, on => LudoPrefs.AutoMove = on);

            AddSection("House rules (new games)");
            AddRule("Need a 6 to start", "Off: a 1 or a 6 brings a token out", rules.sixToStart, on => rules.sixToStart = on);
            AddRule("Roll again on a 6", "Rolling a 6 gives another roll", rules.extraTurnOnSix, on => rules.extraTurnOnSix = on);
            AddRule("Three 6s lose the turn", "A third 6 in a row ends your turn", rules.threeSixesForfeit, on => rules.threeSixesForfeit = on);
            AddRule("Roll again on capture", "Sending a token home gives another roll", rules.extraTurnOnCapture, on => rules.extraTurnOnCapture = on);
            AddRule("Roll again on reaching home", "Getting a token home gives another roll", rules.extraTurnOnHome, on => rules.extraTurnOnHome = on);
            AddRule("Safe squares", "Start and star squares cannot be captured on", rules.safeSquares, on => rules.safeSquares = on);
            AddRule("Stop when humans finish", "Rank the computers once every human is done", rules.endWhenHumansFinish, on => rules.endWhenHumansFinish = on);

            var resetRow = AddRow(150f);
            var reset = UIFactory.AddGameButton(resetRow, "Reset rules", Palette.Neutral, new Vector2(520f, 130f), () =>
            {
                LudoPrefs.SaveRules(new LudoRules());
                UI.Show<LudoSettingsScreen>();
            }, 50f, IconKind.Restart);
            UIFactory.Place((RectTransform)reset.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 130f));
        }

        void AddSection(string title)
        {
            var row = AddRow(110f);
            var label = UIFactory.AddLabel(row, "Section", title.ToUpperInvariant(), 40f, Palette.TextDim, FontWeight.ExtraBold, TextAnchor.LowerLeft);
            UIFactory.Stretch(label.rectTransform, 16f, 0f, 16f, 14f);
        }

        void AddRule(string title, string subtitle, bool value, Action<bool> apply)
        {
            AddToggle(title, subtitle, value, on =>
            {
                apply(on);
                LudoPrefs.SaveRules(rules);
            });
        }

        void AddToggle(string title, string subtitle, bool value, Action<bool> changed)
        {
            var row = AddRow(170f);
            var card = UIFactory.AddPanel(row, "Card", Palette.Card, 40f);
            UIFactory.Stretch(card.rectTransform);
            var name = UIFactory.AddLabel(card.rectTransform, "Title", title, 50f, Color.white, FontWeight.ExtraBold, TextAnchor.LowerLeft);
            UIFactory.Stretch(name.rectTransform, 44f, 20f, 220f, 78f);
            var detail = UIFactory.AddLabel(card.rectTransform, "Subtitle", subtitle, 36f, Palette.TextDim, FontWeight.Medium, TextAnchor.UpperLeft);
            UIFactory.Stretch(detail.rectTransform, 44f, 98f, 220f, 14f);
            var toggle = SwitchControl.Create(card.rectTransform, value, Palette.Green, changed);
            UIFactory.Place((RectTransform)toggle.transform, new Vector2(1f, 0.5f), new Vector2(-40f - 75f, 0f), new Vector2(150f, 86f));
        }

        RectTransform AddRow(float height)
        {
            var row = UIFactory.AddRect("Row", content);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return row;
        }

        public override bool HandleBack()
        {
            UI.Show<LudoMenuScreen>();
            return true;
        }
    }
}
