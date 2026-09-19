using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace NexioCraft.Core
{
    /// <summary>Lightweight UI particle effects built from plain Images.</summary>
    public static class Effects
    {
        /// <summary>Ring of dots flying outward from a point in <paramref name="parent"/>'s local space.</summary>
        public static void Burst(RectTransform parent, Vector2 localPosition, Color color, int count = 12, float radius = 110f, float dotSize = 22f)
        {
            if (App.Instance == null || parent == null) return;
            App.Instance.StartCoroutine(BurstRoutine(parent, localPosition, color, count, radius, dotSize));
        }

        static IEnumerator BurstRoutine(RectTransform parent, Vector2 origin, Color color, int count, float radius, float dotSize)
        {
            var container = UIFactory.AddRect("Burst", parent);
            UIFactory.Place(container, new Vector2(0.5f, 0.5f), origin, Vector2.zero);
            var dots = new RectTransform[count];
            var images = new Image[count];
            var directions = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
                directions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(0.65f, 1f);
                var image = UIFactory.AddImage(container, "Dot", Sprites.Circle, i % 3 == 0 ? Color.white : color);
                float size = dotSize * Random.Range(0.7f, 1.2f);
                UIFactory.Place(image.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
                dots[i] = image.rectTransform;
                images[i] = image;
            }

            yield return Tween.Run(0.6f, t =>
            {
                if (container == null) return;
                for (int i = 0; i < count; i++)
                {
                    dots[i].anchoredPosition = directions[i] * (radius * t);
                    dots[i].localScale = Vector3.one * (1f - 0.8f * t);
                    var c = images[i].color;
                    c.a = 1f - t * t;
                    images[i].color = c;
                }
            }, Ease.OutCubic);

            if (container != null) Object.Destroy(container.gameObject);
        }

        /// <summary>Confetti falling across the whole screen.</summary>
        public static void Confetti(RectTransform layer, Color[] colors, int count = 110, float seconds = 3.2f)
        {
            if (layer == null) return;
            var go = UIFactory.AddRect("Confetti", layer);
            UIFactory.Stretch(go);
            go.gameObject.AddComponent<ConfettiRain>().Begin(colors, count, seconds);
        }
    }

    sealed class ConfettiRain : MonoBehaviour
    {
        RectTransform[] pieces;
        Vector2[] velocities;
        float[] spins;
        float[] phases;
        float age;
        float duration;

        public void Begin(Color[] colors, int count, float seconds)
        {
            duration = seconds;
            var area = ((RectTransform)transform).rect.size;
            if (area.x <= 0f) area = new Vector2(UIRoot.ReferenceWidth, UIRoot.ReferenceHeight);
            pieces = new RectTransform[count];
            velocities = new Vector2[count];
            spins = new float[count];
            phases = new float[count];
            for (int i = 0; i < count; i++)
            {
                var image = UIFactory.AddImage(transform, "Piece", Sprites.White, colors[i % colors.Length]);
                var rt = image.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(Random.Range(16f, 26f), Random.Range(26f, 42f));
                rt.anchoredPosition = new Vector2(Random.Range(-area.x * 0.5f, area.x * 0.5f), Random.Range(20f, 600f));
                rt.localEulerAngles = new Vector3(0f, 0f, Random.Range(0f, 360f));
                pieces[i] = rt;
                velocities[i] = new Vector2(Random.Range(-120f, 120f), -Random.Range(450f, 900f));
                spins[i] = Random.Range(-360f, 360f);
                phases[i] = Random.Range(0f, 10f);
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            age += dt;
            float fade = Mathf.Clamp01((duration - age) / 0.6f);
            for (int i = 0; i < pieces.Length; i++)
            {
                var rt = pieces[i];
                var v = velocities[i];
                float sway = Mathf.Sin(age * 4f + phases[i]) * 90f;
                rt.anchoredPosition += new Vector2((v.x + sway) * dt, v.y * dt);
                rt.localEulerAngles += new Vector3(0f, 0f, spins[i] * dt);
                float flip = Mathf.Abs(Mathf.Cos(age * 6f + phases[i]));
                rt.localScale = new Vector3(0.35f + 0.65f * flip, 1f, 1f);
                var image = rt.GetComponent<Image>();
                var c = image.color;
                c.a = fade;
                image.color = c;
            }
            if (age >= duration) Destroy(gameObject);
        }
    }
}
