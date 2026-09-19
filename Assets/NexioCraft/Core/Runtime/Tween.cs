using System;
using System.Collections;
using UnityEngine;

namespace NexioCraft.Core
{
    public static class Ease
    {
        public static float Linear(float t) => t;
        public static float InQuad(float t) => t * t;
        public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);
        public static float InOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - (-2f * t + 2f) * (-2f * t + 2f) * 0.5f;
        public static float OutCubic(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

        public static float OutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        /// <summary>0 → 1 → 0 bump, for hops and pulses.</summary>
        public static float Arc(float t) => 4f * t * (1f - t);
    }

    /// <summary>Coroutine-based tweens on unscaled time, so UI keeps animating while gameplay is paused.</summary>
    public static class Tween
    {
        /// <summary>Calls <paramref name="step"/> with the eased progress every frame, finishing with exactly 1.</summary>
        public static IEnumerator Run(float duration, Action<float> step, Func<float, float> ease = null)
        {
            if (ease == null) ease = Ease.Linear;
            if (duration <= 0f)
            {
                step(1f);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                step(ease(elapsed / duration));
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            step(1f);
        }

        public static IEnumerator Delay(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
        }
    }
}
