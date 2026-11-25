using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ProjectDhaka
{
    /// <summary>
    /// Pooled world-space floating feedback text for intel pickups, hits, and milestones.
    /// </summary>
    public class FloatingPopup : MonoBehaviour
    {
        static readonly Queue<FloatingPopup> Pool = new Queue<FloatingPopup>();
        static int activeCount;

        TextMeshPro label;
        Vector3 startPos;
        Color baseColor;
        float elapsed;
        float lifetime = 0.85f;
        bool active;

        public static int ActivePopupCount => activeCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPoolStatics()
        {
            Pool.Clear();
            activeCount = 0;
        }

        public static FloatingPopup Spawn(Vector3 worldPos, string message, Color color, float scaleMultiplier = 1f)
        {
            FloatingPopup popup = null;
            while (Pool.Count > 0 && popup == null)
            {
                popup = Pool.Dequeue();
            }

            if (popup == null)
            {
                var go = new GameObject("FloatingPopup");
                popup = go.AddComponent<FloatingPopup>();
                popup.BuildLabel();
            }

            popup.Activate(worldPos, message, color, scaleMultiplier);
            return popup;
        }

        void BuildLabel()
        {
            label = gameObject.AddComponent<TextMeshPro>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 4.2f;
            label.fontStyle = FontStyles.Bold;
            label.characterSpacing = 6f;
            label.outlineWidth = 0.24f;
            label.outlineColor = Palette.Shadow;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            if (label.fontSharedMaterial != null)
            {
                var mat = new Material(label.fontSharedMaterial);
                mat.SetColor(ShaderUtilities.ID_OutlineColor, Palette.Shadow);
                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.24f);
                label.fontMaterial = mat;
            }
        }

        void Activate(Vector3 worldPos, string message, Color color, float scaleMultiplier)
        {
            if (label == null) BuildLabel();
            startPos = worldPos;
            transform.position = startPos;
            baseColor = color;
            label.text = message;
            label.color = baseColor;
            transform.localScale = Vector3.one * scaleMultiplier;
            elapsed = 0f;
            active = true;
            activeCount++;
            gameObject.SetActive(true);
        }

        void Update()
        {
            if (!active) return;

            elapsed += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(elapsed / lifetime);

            transform.position = startPos + Vector3.up * (1.45f * Ease.OutCubic(u));

            Camera cam = Camera.main;
            if (cam != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
            }

            float pop = u < 0.25f ? Ease.OutBack(u / 0.25f) : 1f;
            transform.localScale = Vector3.one * Mathf.Lerp(0.75f, 1.05f, pop);

            Color c = baseColor;
            c.a = u < 0.6f ? 1f : Mathf.Lerp(1f, 0f, (u - 0.6f) / 0.4f);
            label.color = c;

            if (u >= 1f)
            {
                active = false;
                activeCount = Mathf.Max(0, activeCount - 1);
                gameObject.SetActive(false);
                Pool.Enqueue(this);
            }
        }
    }
}
