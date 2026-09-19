using System.Collections.Generic;
using NexioCraft.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NexioCraft.Chess
{
    /// <summary>Bar above/below the board: who plays this side, captured pieces and material lead.</summary>
    public sealed class ChessPlayerStrip : MonoBehaviour
    {
        public const float Height = 112f;

        public RectTransform Rect { get; private set; }

        Image card;
        Image avatar;
        Image avatarPiece;
        Text nameLabel;
        RectTransform capturedRow;
        Text advantageLabel;
        readonly List<Image> capturedImages = new List<Image>();
        int side;
        bool active;
        float activeAmount;

        public static ChessPlayerStrip Create(Transform parent)
        {
            var rt = UIFactory.AddRect("PlayerStrip", parent);
            var strip = rt.gameObject.AddComponent<ChessPlayerStrip>();
            strip.Rect = rt;
            strip.card = UIFactory.AddPanel(rt, "Card", Palette.Card, 36f);
            UIFactory.Stretch(strip.card.rectTransform);

            strip.avatar = UIFactory.AddPanel(strip.card.rectTransform, "Avatar", Palette.Hex(0xF0D9B5), 22f);
            UIFactory.Place(strip.avatar.rectTransform, new Vector2(0f, 0.5f), new Vector2(16f + 42f, 0f), new Vector2(84f, 84f));
            strip.avatarPiece = UIFactory.AddImage(strip.avatar.rectTransform, "Piece", null, Color.white);
            strip.avatarPiece.preserveAspect = true;
            UIFactory.Stretch(strip.avatarPiece.rectTransform, 6f, 6f, 6f, 6f);

            strip.nameLabel = UIFactory.AddLabel(strip.card.rectTransform, "Name", "", 44f, Color.white, FontWeight.ExtraBold, TextAnchor.MiddleLeft);
            UIFactory.Stretch(strip.nameLabel.rectTransform, 118f, 8f, 20f, 50f);

            strip.capturedRow = UIFactory.AddRect("Captured", strip.card.rectTransform);
            UIFactory.Stretch(strip.capturedRow, 118f, 58f, 20f, 8f);
            strip.advantageLabel = UIFactory.AddLabel(strip.capturedRow, "Advantage", "", 34f, Palette.TextDim, FontWeight.ExtraBold, TextAnchor.MiddleLeft);
            UIFactory.Stretch(strip.advantageLabel.rectTransform);
            return strip;
        }

        public void SetPlayer(string playerName, int playerSide)
        {
            side = playerSide;
            nameLabel.text = playerName;
            avatarPiece.sprite = ChessArt.PieceSprite(Piece.King * playerSide);
            avatar.color = playerSide == Side.White ? Palette.Hex(0x58606E) : Palette.Hex(0xE4E7EE);
        }

        /// <summary>Shows the opponent pieces this side has captured and its material lead, if any.</summary>
        public void SetCaptured(IReadOnlyList<sbyte> capturedPieces, int advantage)
        {
            var mine = new List<int>();
            foreach (sbyte piece in capturedPieces)
                if (piece != 0 && Piece.SideOf(piece) == -side) mine.Add(piece);
            mine.Sort((a, b) => Piece.TypeOf(a).CompareTo(Piece.TypeOf(b)));

            const float size = 40f;
            float x = 0f;
            for (int i = 0; i < mine.Count; i++)
            {
                while (capturedImages.Count <= i)
                {
                    var image = UIFactory.AddImage(capturedRow, "Captured", null, Color.white);
                    image.preserveAspect = true;
                    capturedImages.Add(image);
                }
                var img = capturedImages[i];
                img.enabled = true;
                img.sprite = ChessArt.PieceSprite(mine[i]);
                bool sameAsPrevious = i > 0 && Piece.TypeOf(mine[i]) == Piece.TypeOf(mine[i - 1]);
                x += i == 0 ? 0f : sameAsPrevious ? size * 0.45f : size * 0.9f;
                UIFactory.Place(img.rectTransform, new Vector2(0f, 0.5f), new Vector2(x + size * 0.5f, 0f), new Vector2(size, size));
            }
            for (int i = mine.Count; i < capturedImages.Count; i++) capturedImages[i].enabled = false;

            advantageLabel.text = advantage > 0 ? "+" + advantage : "";
            float labelX = mine.Count > 0 ? x + size + 10f : 0f;
            advantageLabel.rectTransform.offsetMin = new Vector2(labelX, 0f);
        }

        public void SetActive(bool on) => active = on;

        void Update()
        {
            activeAmount = Mathf.MoveTowards(activeAmount, active ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            card.color = Color.Lerp(Palette.Card, Palette.CardRaised, activeAmount);
        }
    }
}
