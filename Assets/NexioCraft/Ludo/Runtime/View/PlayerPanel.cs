using NexioCraft.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NexioCraft.Ludo
{
    /// <summary>Player card beside the board: avatar, name, tokens home, rank, and the dice slot.</summary>
    public sealed class PlayerPanel : MonoBehaviour
    {
        public const float Height = 180f;
        public const float SlotSize = 140f;
        const float AvatarSize = 124f;
        const float Margin = 18f;
        const float Gap = 16f;

        public int Seat { get; private set; }
        public RectTransform Rect { get; private set; }
        public RectTransform DiceSlot { get; private set; }

        Image card;
        Image glow;
        Image[] homeDots;
        Image rankBadge;
        Text rankLabel;
        bool active;
        float activeAmount = -1f;

        /// <param name="diceOnRight">Put the dice slot on the right (panels on the left side of the screen).</param>
        public static PlayerPanel Create(Transform parent, int seat, SeatKind kind, string name, bool diceOnRight)
        {
            var root = UIFactory.AddRect("Panel " + LudoTheme.SeatNames[seat], parent);
            var panel = root.gameObject.AddComponent<PlayerPanel>();
            panel.Seat = seat;
            panel.Rect = root;
            Color color = LudoTheme.Seat(seat);
            float side = diceOnRight ? 1f : -1f;
            var near = new Vector2(diceOnRight ? 0f : 1f, 0.5f);
            var far = new Vector2(diceOnRight ? 1f : 0f, 0.5f);

            panel.glow = UIFactory.AddPanel(root, "Glow", Palette.WithAlpha(color, 0f), 50f);
            UIFactory.Stretch(panel.glow.rectTransform, -9f, -9f, -9f, -9f);
            panel.card = UIFactory.AddPanel(root, "Card", Palette.Card, 42f);
            var cardRect = panel.card.rectTransform;
            UIFactory.Stretch(cardRect);

            var avatar = UIFactory.AddImage(cardRect, "Avatar", Sprites.Circle, color);
            UIFactory.Place(avatar.rectTransform, near, new Vector2(side * (Margin + AvatarSize * 0.5f), 0f), new Vector2(AvatarSize, AvatarSize));
            var ring = UIFactory.AddImage(avatar.rectTransform, "Ring", Sprites.Ring(0.09f), Palette.WithAlpha(Color.white, 0.6f));
            UIFactory.Stretch(ring.rectTransform);
            var icon = UIFactory.AddImage(avatar.rectTransform, "Icon", Icons.Get(kind == SeatKind.Bot ? IconKind.Robot : IconKind.Person), Color.white);
            UIFactory.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72f, 72f));

            panel.rankBadge = UIFactory.AddImage(avatar.rectTransform, "Rank", Sprites.Circle, Palette.Gold);
            UIFactory.Place(panel.rankBadge.rectTransform, new Vector2(0.86f, 0.86f), Vector2.zero, new Vector2(62f, 62f));
            panel.rankLabel = UIFactory.AddLabel(panel.rankBadge.rectTransform, "Label", "1", 40f, Palette.Hex(0x3A2A00), FontWeight.ExtraBold);
            UIFactory.Stretch(panel.rankLabel.rectTransform);
            panel.rankBadge.gameObject.SetActive(false);

            var slot = UIFactory.AddPanel(cardRect, "DiceSlot", Palette.Inset, 32f);
            UIFactory.Place(slot.rectTransform, far, new Vector2(-side * (Margin + SlotSize * 0.5f), 0f), new Vector2(SlotSize, SlotSize));
            panel.DiceSlot = slot.rectTransform;

            float nearInset = Margin + AvatarSize + Gap;
            float farInset = Margin + SlotSize + Gap;
            var info = UIFactory.AddRect("Info", cardRect);
            UIFactory.Stretch(info, diceOnRight ? nearInset : farInset, 20f, diceOnRight ? farInset : nearInset, 20f);

            var nameLabel = UIFactory.AddLabel(info, "Name", name, 44f, Color.white, FontWeight.ExtraBold,
                diceOnRight ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
            var nameRect = nameLabel.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 0.42f);
            nameRect.anchorMax = Vector2.one;
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;
            nameLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            nameLabel.verticalOverflow = VerticalWrapMode.Truncate;
            nameLabel.resizeTextForBestFit = true;
            nameLabel.resizeTextMinSize = 26;
            nameLabel.resizeTextMaxSize = 44;

            panel.homeDots = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var dot = UIFactory.AddImage(info, "HomeDot", Sprites.Circle, Palette.Inset);
                var anchor = new Vector2(diceOnRight ? 0f : 1f, 0.24f);
                UIFactory.Place(dot.rectTransform, anchor, new Vector2(side * (13f + i * 34f), 0f), new Vector2(26f, 26f));
                panel.homeDots[i] = dot;
            }
            return panel;
        }

        public void SetActive(bool on) => active = on;

        public void SetTokensHome(int count)
        {
            for (int i = 0; i < homeDots.Length; i++)
                homeDots[i].color = i < count ? LudoTheme.Seat(Seat) : Palette.Inset;
        }

        /// <summary>Shows the finishing position badge (0-based rank), or hides it for -1.</summary>
        public void SetRank(int rank)
        {
            rankBadge.gameObject.SetActive(rank >= 0);
            if (rank < 0) return;
            rankLabel.text = (rank + 1).ToString();
            rankBadge.color = RankColor(rank);
        }

        public static Color RankColor(int rank) =>
            rank == 0 ? Palette.Gold : rank == 1 ? Palette.Hex(0xD5DAE6) : rank == 2 ? Palette.Hex(0xE3A06A) : Palette.Neutral;

        void Update()
        {
            float target = active ? 1f : 0f;
            activeAmount = activeAmount < 0f ? target : Mathf.MoveTowards(activeAmount, target, Time.unscaledDeltaTime * 5f);
            float pulse = active ? Mathf.Sin(Time.unscaledTime * 4f) * 0.5f + 0.5f : 0f;
            glow.color = Palette.WithAlpha(LudoTheme.Seat(Seat), activeAmount * (0.5f + pulse * 0.5f));
            card.color = Color.Lerp(Palette.Card, Palette.CardRaised, activeAmount);
            float scale = 1f + activeAmount * 0.03f;
            Rect.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
