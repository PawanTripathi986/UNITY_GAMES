using NexioCraft.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NexioCraft.Ludo
{
    /// <summary>
    /// One pin on the board. The root lives in (rotating) board space; the visual child is
    /// counter-rotated so the pin always stands upright on screen.
    /// </summary>
    public sealed class TokenView : MonoBehaviour
    {
        public int Seat { get; private set; }
        public int Index { get; private set; }
        public RectTransform Rect { get; private set; }

        /// <summary>Screen-space offset (canvas units) used to fan out tokens sharing a square.</summary>
        public Vector2 StackOffset { get; private set; }
        public float StackScale { get; private set; } = 1f;

        RectTransform visual;
        RectTransform pin;
        RectTransform shadow;
        RectTransform ring;
        Image ringImage;
        float cell;
        float boardRotation;
        bool highlighted;
        float lift;
        float squash = 1f;

        public static TokenView Create(Transform parent, int seat, int index)
        {
            var root = UIFactory.AddRect("Token " + LudoTheme.SeatNames[seat] + " " + index, parent);
            UIFactory.Place(root, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var view = root.gameObject.AddComponent<TokenView>();
            view.Seat = seat;
            view.Index = index;
            view.Rect = root;

            view.visual = UIFactory.AddRect("Visual", root);
            UIFactory.Place(view.visual, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            view.ringImage = UIFactory.AddImage(view.visual, "Ring", Sprites.Ring(0.22f), Color.white);
            view.ring = view.ringImage.rectTransform;
            view.ring.gameObject.SetActive(false);

            view.shadow = UIFactory.AddImage(view.visual, "Shadow", LudoArt.TokenShadow, new Color(0f, 0f, 0f, 0.4f)).rectTransform;
            var pinImage = UIFactory.AddImage(view.visual, "Pin", LudoArt.Token(seat), Color.white);
            view.pin = pinImage.rectTransform;
            view.pin.pivot = pinImage.sprite.pivot / pinImage.sprite.rect.size;
            return view;
        }

        public void SetCellSize(float cellSize)
        {
            cell = cellSize;
            float height = cell * 1.18f;
            var spriteSize = LudoArt.Token(Seat).rect.size;
            pin.sizeDelta = new Vector2(height * spriteSize.x / spriteSize.y, height);
            shadow.sizeDelta = new Vector2(cell * 0.78f, cell * 0.36f);
            ring.sizeDelta = new Vector2(cell * 1.02f, cell * 1.02f);
            ApplyVisual();
        }

        public void SetBoardRotation(float degrees)
        {
            boardRotation = degrees;
            visual.localEulerAngles = new Vector3(0f, 0f, -degrees);
        }

        /// <summary>Places the token at a board-space point, adding its fan-out offset.</summary>
        public void SetPosition(Vector2 boardLocal)
        {
            Rect.anchoredPosition = boardLocal + ScreenToBoard(StackOffset);
        }

        public void SetStack(Vector2 screenOffset, float scale)
        {
            StackOffset = screenOffset;
            StackScale = scale;
            ApplyVisual();
        }

        /// <summary>Hop height (screen space, canvas units) and squash used while animating.</summary>
        public void SetLift(float height, float squashAmount = 1f)
        {
            lift = height;
            squash = squashAmount;
            ApplyVisual();
        }

        public void SetHighlighted(bool on)
        {
            highlighted = on;
            ring.gameObject.SetActive(on);
            if (!on)
            {
                lift = 0f;
                ApplyVisual();
            }
        }

        /// <summary>Screen-space position of the pin head, in board-local coordinates (for tap tests).</summary>
        public Vector2 HeadPointInBoard() => Rect.anchoredPosition + ScreenToBoard(new Vector2(0f, cell * 0.25f * StackScale));

        public Vector2 ScreenToBoard(Vector2 screenOffset)
        {
            float rad = -boardRotation * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            return new Vector2(screenOffset.x * cos - screenOffset.y * sin, screenOffset.x * sin + screenOffset.y * cos);
        }

        void Update()
        {
            if (!highlighted) return;
            float t = Time.unscaledTime;
            lift = Mathf.Abs(Mathf.Sin(t * 5.5f)) * cell * 0.16f;
            float pulse = 1f + Mathf.Sin(t * 5.5f) * 0.08f;
            ring.localScale = new Vector3(pulse, pulse * 0.42f, 1f);
            ringImage.color = new Color(1f, 1f, 1f, 0.75f + Mathf.Sin(t * 5.5f) * 0.25f);
            ApplyVisual();
        }

        void ApplyVisual()
        {
            float scale = StackScale;
            visual.localScale = new Vector3(scale, scale, 1f);
            // The pin's tip sits a little below the square's centre so the head fills the square.
            float baseY = -cell * 0.3f;
            shadow.anchoredPosition = new Vector2(0f, baseY);
            ring.anchoredPosition = new Vector2(0f, baseY);
            shadow.localScale = Vector3.one * Mathf.Clamp(1f - lift / (cell * 1.2f + 0.001f), 0.5f, 1f);
            pin.anchoredPosition = new Vector2(0f, baseY + lift);
            pin.localScale = new Vector3(2f - squash, squash, 1f);
        }
    }
}
