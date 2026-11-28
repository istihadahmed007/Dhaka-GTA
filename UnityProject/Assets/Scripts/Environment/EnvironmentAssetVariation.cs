using System.Collections.Generic;
using UnityEngine;

namespace DhakaGta.Environment
{
    /// <summary>
    /// Applies deterministic MaterialPropertyBlock color/smoothness variation without breaking GPU instancing,
    /// plus configures clean 3-level LODGroups for repeated buildings and environmental props.
    /// </summary>
    public sealed class EnvironmentAssetVariation : MonoBehaviour
    {
        [SerializeField] private int variationSeed;
        [SerializeField] private Color tintColor = Color.white;
        [SerializeField] private float smoothnessOffset;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

        private MaterialPropertyBlock _mpb;

        public int VariationSeed => variationSeed;

        public void ApplyVariation(int seed, Color tint, float smoothDelta = 0f)
        {
            variationSeed = seed;
            tintColor = tint;
            smoothnessOffset = smoothDelta;
            PushPropertyBlock();
        }

        private void Awake()
        {
            PushPropertyBlock();
        }

        private void OnValidate()
        {
            PushPropertyBlock();
        }

        public void PushPropertyBlock()
        {
            _mpb ??= new MaterialPropertyBlock();
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, tintColor);
                if (Mathf.Abs(smoothnessOffset) > 0.001f)
                {
                    _mpb.SetFloat(SmoothnessId, Mathf.Clamp01(0.45f + smoothnessOffset));
                }
                r.SetPropertyBlock(_mpb);
            }
        }

        /// <summary>
        /// Configures standard 3-tier HDRP LODGroup transitions cleanly for open-world draw budgets.
        /// </summary>
        public static LODGroup ConfigureThreeLevelLod(
            GameObject target,
            IList<Renderer> lod0Renderers,
            IList<Renderer> lod1Renderers,
            IList<Renderer> lod2Renderers)
        {
            if (target == null) return null;

            LODGroup group = target.GetComponent<LODGroup>();
            if (group == null) group = target.AddComponent<LODGroup>();

            var lods = new List<LOD>(3);
            if (lod0Renderers != null && lod0Renderers.Count > 0)
            {
                lods.Add(new LOD(0.42f, ToArray(lod0Renderers)));
            }
            if (lod1Renderers != null && lod1Renderers.Count > 0)
            {
                lods.Add(new LOD(0.14f, ToArray(lod1Renderers)));
            }
            if (lod2Renderers != null && lod2Renderers.Count > 0)
            {
                lods.Add(new LOD(0.02f, ToArray(lod2Renderers)));
            }

            if (lods.Count > 0)
            {
                group.SetLODs(lods.ToArray());
                group.RecalculateBounds();
            }
            return group;
        }

        private static Renderer[] ToArray(IList<Renderer> items)
        {
            var arr = new Renderer[items.Count];
            for (int i = 0; i < items.Count; i++) arr[i] = items[i];
            return arr;
        }
    }
}
