using System;
using System.Collections;
using NexioCraft.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NexioCraft.Ludo
{
    /// <summary>The die: shows a face, tumbles when rolled, and pulses when it is waiting for a tap.</summary>
    public sealed class DiceView : MonoBehaviour, IPointerClickHandler
    {
        public event Action Tapped;

        public RectTransform Rect { get; private set; }

        Image face;
        RectTransform faceRect;
        Image glow;
        bool interactive;
        float glowPhase;

        public static DiceView Create(Transform parent, float size)
        {
            var root = UIFactory.AddRect("Dice", parent);
            UIFactory.Place(root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            var view = root.gameObject.AddComponent<DiceView>();
            view.Rect = root;

            view.glow = UIFactory.AddImage(root, "Glow", Sprites.Glow, new Color(1f, 0.85f, 0.3f, 0f));
            UIFactory.Place(view.glow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 1.9f, size * 1.9f));
            view.face = UIFactory.AddImage(root, "Face", LudoArt.DiceFace(6), Color.white);
            view.faceRect = view.face.rectTransform;
            UIFactory.Stretch(view.faceRect);
            return view;
        }

        public void SetInteractive(bool on)
        {
            interactive = on;
            if (!on)
            {
                faceRect.localScale = Vector3.one;
                glow.color = new Color(1f, 0.85f, 0.3f, 0f);
            }
        }

        public void ShowFace(int value) => face.sprite = LudoArt.DiceFace(value);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (interactive) Tapped?.Invoke();
        }

        /// <summary>Tumble animation that settles on <paramref name="value"/>.</summary>
        public IEnumerator Roll(int value, float duration, System.Random rng)
        {
            SetInteractive(false);
            float elapsed = 0f;
            float nextSwap = 0f;
            float spin = rng.Next(2) == 0 ? 1f : -1f;
            while (elapsed < duration)
            {
                float t = elapsed / duration;
                if (elapsed >= nextSwap)
                {
                    ShowFace(rng.Next(1, 7));
                    nextSwap = elapsed + Mathf.Lerp(0.045f, 0.1f, t);
                }
                faceRect.localEulerAngles = new Vector3(0f, 0f, spin * Mathf.Sin(t * Mathf.PI * 3f) * 28f * (1f - t));
                float bounce = 1f + Ease.Arc(Mathf.Repeat(t * 2.5f, 1f)) * 0.14f * (1f - t);
                faceRect.localScale = new Vector3(bounce, bounce, 1f);
                faceRect.anchoredPosition = new Vector2(0f, Ease.Arc(Mathf.Repeat(t * 2.5f, 1f)) * 18f * (1f - t));
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            ShowFace(value);
            faceRect.localEulerAngles = Vector3.zero;
            faceRect.anchoredPosition = Vector2.zero;
            yield return Tween.Run(0.18f, k =>
            {
                float s = 1f + Ease.Arc(k) * 0.12f;
                faceRect.localScale = new Vector3(s, s, 1f);
            });
        }

        void Update()
        {
            if (!interactive) return;
            glowPhase += Time.unscaledDeltaTime * 4.5f;
            float wave = Mathf.Sin(glowPhase) * 0.5f + 0.5f;
            float scale = 1f + wave * 0.08f;
            faceRect.localScale = new Vector3(scale, scale, 1f);
            glow.color = new Color(1f, 0.85f, 0.3f, 0.18f + wave * 0.22f);
        }
    }
}
