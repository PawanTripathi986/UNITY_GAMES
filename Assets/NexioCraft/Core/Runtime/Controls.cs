using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NexioCraft.Core
{
    /// <summary>iOS-style on/off switch.</summary>
    public sealed class SwitchControl : MonoBehaviour, IPointerClickHandler
    {
        const float Width = 150f;
        const float Height = 86f;

        Image track;
        RectTransform knob;
        Color onColor;
        Action<bool> changed;
        float position;

        public bool Value { get; private set; }

        public static SwitchControl Create(Transform parent, bool value, Color onColor, Action<bool> changed)
        {
            var track = UIFactory.AddPanel(parent, "Switch", Palette.Inset, Height * 0.5f, true);
            track.rectTransform.sizeDelta = new Vector2(Width, Height);
            var knobImage = UIFactory.AddImage(track.rectTransform, "Knob", Sprites.Circle, Color.white);
            var control = track.gameObject.AddComponent<SwitchControl>();
            control.track = track;
            control.knob = knobImage.rectTransform;
            control.onColor = onColor;
            control.changed = changed;
            control.Value = value;
            control.position = value ? 1f : 0f;
            UIFactory.Place(control.knob, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Height - 16f, Height - 16f));
            control.Apply();
            return control;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Value = !Value;
            if (App.Instance != null) App.Instance.Audio.Play(Sfx.Click, 0.8f);
            Haptics.Play(HapticKind.Selection);
            changed?.Invoke(Value);
        }

        void Update()
        {
            float target = Value ? 1f : 0f;
            if (Mathf.Approximately(position, target)) return;
            position = Mathf.MoveTowards(position, target, Time.unscaledDeltaTime * 7f);
            Apply();
        }

        void Apply()
        {
            float eased = Ease.InOutQuad(position);
            float travel = (Width - Height) * 0.5f;
            knob.anchoredPosition = new Vector2(Mathf.Lerp(-travel, travel, eased), 0f);
            track.color = Color.Lerp(Palette.Inset, onColor, eased);
        }
    }

    /// <summary>Row of mutually exclusive options.</summary>
    public sealed class SegmentedControl : MonoBehaviour
    {
        Image[] backgrounds;
        Text[] labels;
        Color accent;
        Action<int> changed;

        public int Selected { get; private set; }

        public static SegmentedControl Create(Transform parent, string[] options, int selected, Color accent, Vector2 size, Action<int> changed)
        {
            var frame = UIFactory.AddPanel(parent, "Segmented", Palette.Inset, size.y * 0.5f);
            frame.rectTransform.sizeDelta = size;
            var control = frame.gameObject.AddComponent<SegmentedControl>();
            control.accent = accent;
            control.changed = changed;
            control.backgrounds = new Image[options.Length];
            control.labels = new Text[options.Length];

            const float pad = 8f;
            float segment = (size.x - pad * 2f) / options.Length;
            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                var option = UIFactory.AddPanel(frame.rectTransform, options[i], Color.clear, (size.y - pad * 2f) * 0.5f, true);
                var rt = option.rectTransform;
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.offsetMin = new Vector2(pad + segment * i, pad);
                rt.offsetMax = new Vector2(pad + segment * (i + 1), -pad);
                var label = UIFactory.AddLabel(rt, "Label", options[i], Mathf.Min(48f, size.y * 0.42f), Color.white);
                UIFactory.Stretch(label.rectTransform);
                UIFactory.MakeButton(option.gameObject, () => control.Select(index, true));
                control.backgrounds[i] = option;
                control.labels[i] = label;
            }
            control.Select(Mathf.Clamp(selected, 0, options.Length - 1), false);
            return control;
        }

        public void Select(int index, bool notify)
        {
            Selected = index;
            for (int i = 0; i < backgrounds.Length; i++)
            {
                bool on = i == index;
                backgrounds[i].color = on ? accent : Color.clear;
                labels[i].color = on ? Color.white : Palette.TextDim;
            }
            if (notify) changed?.Invoke(index);
        }
    }

    /// <summary>Gentle looping scale pulse (for "tap me" hints).</summary>
    public sealed class Pulse : MonoBehaviour
    {
        public float Amplitude = 0.06f;
        public float Speed = 5f;

        void OnDisable() => transform.localScale = Vector3.one;

        void Update()
        {
            float s = 1f + (Mathf.Sin(Time.unscaledTime * Speed) * 0.5f + 0.5f) * Amplitude;
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
