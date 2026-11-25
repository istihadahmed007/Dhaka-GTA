using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectDhaka
{
    /// <summary>
    /// Shared unscaled-time easing curves, UI tween coroutines, and runtime 2D sprite utilities.
    /// </summary>
    public static class Ease
    {
        public static float OutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        public static float OutBack(float t, float overshoot = 1.70158f)
        {
            t = Mathf.Clamp01(t);
            float p = t - 1f;
            return 1f + p * p * ((overshoot + 1f) * p + overshoot);
        }

        public static float InOutSine(float t)
        {
            t = Mathf.Clamp01(t);
            return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
        }

        public static float Punch(float t)
        {
            t = Mathf.Clamp01(t);
            return Mathf.Sin(t * Mathf.PI);
        }

        /// <summary>
        /// Generates a crisp opaque 4x4 sprite so UI Filled images and panels render cleanly without external sprite assets.
        /// </summary>
        public static Sprite CreateSolidSprite()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
            {
                name = "RuntimeSolidUI",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color[16];
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = Palette.TextLight;
            }
            // Wait: Image.color multiplies vertex color by sprite texture pixels, so white pixels preserve exact Palette tinting.
            var neutralPx = new Color32[16];
            for (int i = 0; i < neutralPx.Length; i++)
            {
                neutralPx[i] = new Color32(255, 255, 255, 255);
            }
            tex.SetPixels32(neutralPx);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
        }

        public static Sprite CreateSoftDiscSprite(int size = 32)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "RuntimeSoftDisc",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            float half = (size - 1) * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(1f - dist);
                    alpha = alpha * alpha;
                    byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        public static IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration, Action onComplete = null)
        {
            if (group == null) yield break;
            float elapsed = 0f;
            group.alpha = from;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float u = duration > 0.0001f ? Mathf.Clamp01(elapsed / duration) : 1f;
                group.alpha = Mathf.Lerp(from, to, InOutSine(u));
                yield return null;
            }
            group.alpha = to;
            onComplete?.Invoke();
        }

        public static IEnumerator ScalePunch(Transform target, Vector3 baseScale, float peakMultiplier, float duration)
        {
            if (target == null) yield break;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float u = duration > 0.0001f ? Mathf.Clamp01(elapsed / duration) : 1f;
                float mult = 1f + (peakMultiplier - 1f) * Punch(u);
                target.localScale = baseScale * mult;
                yield return null;
            }
            target.localScale = baseScale;
        }
    }
}
