using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NexioCraft.Core
{
    public enum FontWeight { Medium, Bold, ExtraBold }

    /// <summary>Baloo 2 (SIL OFL) from Resources/Fonts, falling back to Unity's built-in font.</summary>
    public static class Fonts
    {
        static Font medium, bold, extraBold, fallback;

        public static Font Get(FontWeight weight)
        {
            switch (weight)
            {
                case FontWeight.Medium: return Load(ref medium, "Fonts/Baloo2-Medium");
                case FontWeight.ExtraBold: return Load(ref extraBold, "Fonts/Baloo2-ExtraBold");
                default: return Load(ref bold, "Fonts/Baloo2-Bold");
            }
        }

        static Font Load(ref Font cache, string path)
        {
            if (cache != null) return cache;
            cache = Resources.Load<Font>(path);
            if (cache == null)
            {
                if (fallback == null) fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                cache = fallback;
            }
            return cache;
        }
    }

    /// <summary>Helpers for building uGUI hierarchies from code.</summary>
    public static class UIFactory
    {
        public static RectTransform AddRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Fills the parent, inset by the given margins.</summary>
        public static RectTransform Stretch(RectTransform rt, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Anchors to a single point of the parent (0..1) with a fixed size.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image AddImage(Transform parent, string name, Sprite sprite, Color color, bool raycast = false)
        {
            var rt = AddRect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        /// <summary>Rounded rectangle panel with the given corner radius (canvas units).</summary>
        public static Image AddPanel(Transform parent, string name, Color color, float radius, bool raycast = false)
        {
            var image = AddImage(parent, name, Sprites.RoundRect, color, raycast);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = Sprites.CornerMultiplier(radius);
            return image;
        }

        /// <summary>Soft drop shadow sized to a sibling card: call before creating the card so it renders behind.</summary>
        public static Image AddShadow(Transform parent, string name, float spread, float alpha, Vector2 offset)
        {
            var image = AddImage(parent, name, Sprites.SoftShadow, new Color(0f, 0f, 0f, alpha));
            image.type = Image.Type.Sliced;
            Stretch(image.rectTransform, -spread + offset.x, -spread - offset.y, -spread - offset.x, -spread + offset.y);
            return image;
        }

        public static RawImage AddRaw(Transform parent, string name, Texture texture)
        {
            var rt = AddRect(name, parent);
            var raw = rt.gameObject.AddComponent<RawImage>();
            raw.texture = texture;
            raw.raycastTarget = false;
            return raw;
        }

        public static Text AddLabel(Transform parent, string name, string text, float size, Color color,
            FontWeight weight = FontWeight.Bold, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var rt = AddRect(name, parent);
            var label = rt.gameObject.AddComponent<Text>();
            label.font = Fonts.Get(weight);
            label.fontSize = Mathf.RoundToInt(size);
            label.color = color;
            label.alignment = align;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.supportRichText = true;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        public static Shadow AddTextShadow(Text label, float distance = 4f, float alpha = 0.35f)
        {
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, alpha);
            shadow.effectDistance = new Vector2(0f, -distance);
            return shadow;
        }

        /// <summary>Chunky "3D" game button: a darker edge under a raised face that sinks when pressed.</summary>
        public static Button AddGameButton(Transform parent, string label, Color color, Vector2 size, Action onClick,
            float fontSize = 58f, IconKind? icon = null)
        {
            var root = AddRect(label + " Button", parent);
            root.sizeDelta = size;
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);

            float depth = Mathf.Round(Mathf.Clamp(size.y * 0.075f, 6f, 14f));
            float radius = Mathf.Min(size.y * 0.3f, 48f);
            var edge = AddPanel(root, "Edge", Palette.Darken(color, 0.32f), radius);
            Stretch(edge.rectTransform, 0f, depth, 0f, 0f);
            var face = AddPanel(root, "Face", color, radius);
            Stretch(face.rectTransform, 0f, 0f, 0f, depth);
            var gloss = AddPanel(face.rectTransform, "Gloss", new Color(1f, 1f, 1f, 0.13f), radius * 0.8f);
            gloss.rectTransform.anchorMin = new Vector2(0f, 0.52f);
            gloss.rectTransform.anchorMax = Vector2.one;
            gloss.rectTransform.offsetMin = new Vector2(10f, 0f);
            gloss.rectTransform.offsetMax = new Vector2(-10f, -8f);

            var content = AddRect("Content", face.rectTransform);
            Stretch(content);
            var text = AddLabel(content, "Label", label, fontSize, Color.white, FontWeight.ExtraBold);
            Stretch(text.rectTransform);
            AddTextShadow(text, 3f, 0.25f);
            if (icon.HasValue)
            {
                // Centre the icon + label pair as one group.
                const float gap = 18f;
                float iconSize = fontSize * 1.05f;
                float textWidth = text.preferredWidth;
                float total = iconSize + gap + textWidth;
                var iconImage = AddImage(content, "Icon", Icons.Get(icon.Value), Color.white);
                Place(iconImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-total * 0.5f + iconSize * 0.5f, 0f), new Vector2(iconSize, iconSize));
                Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(total * 0.5f - textWidth * 0.5f, 0f), new Vector2(textWidth + 4f, size.y));
            }

            var button = MakeButton(root.gameObject, onClick);
            var press = root.gameObject.AddComponent<PressFeedback>();
            press.Configure(face.rectTransform, depth);
            return button;
        }

        /// <summary>Round 3D icon button.</summary>
        public static Button AddIconButton(Transform parent, IconKind icon, Color color, float size, Action onClick, float iconScale = 0.5f)
        {
            var root = AddRect(icon + " Button", parent);
            root.sizeDelta = new Vector2(size, size);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);

            float depth = Mathf.Round(Mathf.Clamp(size * 0.07f, 5f, 10f));
            var edge = AddPanel(root, "Edge", Palette.Darken(color, 0.35f), size * 0.5f);
            Stretch(edge.rectTransform, 0f, depth, 0f, 0f);
            var face = AddPanel(root, "Face", color, size * 0.5f);
            Stretch(face.rectTransform, 0f, 0f, 0f, depth);
            var image = AddImage(face.rectTransform, "Icon", Icons.Get(icon), Color.white);
            Place(image.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (size * iconScale));

            var button = MakeButton(root.gameObject, onClick);
            root.gameObject.AddComponent<PressFeedback>().Configure(face.rectTransform, depth);
            return button;
        }

        /// <summary>Flat pill button (for secondary actions and segmented choices).</summary>
        public static Button AddFlatButton(Transform parent, string label, Color color, Vector2 size, Action onClick, float fontSize = 46f)
        {
            var panel = AddPanel(parent, label + " Button", color, size.y * 0.5f, true);
            panel.rectTransform.sizeDelta = size;
            var text = AddLabel(panel.rectTransform, "Label", label, fontSize, Color.white, FontWeight.Bold);
            Stretch(text.rectTransform);
            var button = MakeButton(panel.gameObject, onClick);
            panel.gameObject.AddComponent<PressFeedback>();
            return button;
        }

        public static Button MakeButton(GameObject target, Action onClick)
        {
            var button = target.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() =>
            {
                if (App.Instance != null) App.Instance.Audio.Play(Sfx.Click, 0.8f);
                Haptics.Play(HapticKind.Selection);
                onClick?.Invoke();
            });
            return button;
        }

        /// <summary>Screen header: back button on the left and a centred title.</summary>
        public static RectTransform AddHeader(Transform parent, string title, Action onBack, float height = 150f)
        {
            var header = AddRect("Header", parent);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.anchoredPosition = Vector2.zero;
            header.sizeDelta = new Vector2(0f, height);

            var label = AddLabel(header, "Title", title, 72f, Color.white, FontWeight.ExtraBold);
            Stretch(label.rectTransform);
            AddTextShadow(label, 5f, 0.3f);

            if (onBack != null)
            {
                var back = AddIconButton(header, IconKind.Back, Palette.CardRaised, 112f, onBack);
                Place((RectTransform)back.transform, new Vector2(0f, 0.5f), new Vector2(40f + 56f, 0f), new Vector2(112f, 112f));
            }
            return header;
        }
    }

    /// <summary>Press animation: sinks a 3D button's face, or scales flat buttons down slightly.</summary>
    public sealed class PressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        RectTransform face;
        float depth;
        float target;
        float current;

        public void Configure(RectTransform buttonFace, float faceDepth)
        {
            face = buttonFace;
            depth = faceDepth;
        }

        public void OnPointerDown(PointerEventData eventData) => target = 1f;
        public void OnPointerUp(PointerEventData eventData) => target = 0f;
        public void OnPointerExit(PointerEventData eventData) => target = 0f;

        void OnDisable()
        {
            target = current = 0f;
            Apply();
        }

        void Update()
        {
            if (Mathf.Approximately(current, target)) return;
            current = Mathf.MoveTowards(current, target, Time.unscaledDeltaTime * 14f);
            Apply();
        }

        void Apply()
        {
            if (face != null)
            {
                float sink = depth * 0.75f * current;
                face.offsetMin = new Vector2(0f, depth - sink);
                face.offsetMax = new Vector2(0f, -sink);
            }
            else
            {
                transform.localScale = Vector3.one * (1f - 0.05f * current);
            }
        }
    }
}
