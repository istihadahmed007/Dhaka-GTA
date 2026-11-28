using System.Collections.Generic;
using DhakaGta.Core;
using UnityEngine;

namespace DhakaGta.Graphics
{
    /// <summary>
    /// Dynamically drives HDRP Wet Surface shaders, road puddle smoothness/albedo darkening,
    /// Clear-Coat vehicle film sheen, and global HLSL shader uniforms (_DhakaGlobalWetness, _DhakaRainTime).
    /// </summary>
    public sealed class WetSurfacePuddleDriver : MonoBehaviour
    {
        [Range(0f, 1f)] [SerializeField] private float currentWetness = 0.25f;
        [SerializeField] private float wetUpSpeedPerSec = 0.18f;
        [SerializeField] private float dryOutSpeedPerSec = 0.04f;

        private readonly List<Material> _pavementMaterials = new List<Material>();
        private readonly List<Material> _carPaintMaterials = new List<Material>();
        private readonly Dictionary<Material, (Color dryColor, float drySmooth)> _dryCache =
            new Dictionary<Material, (Color, float)>();

        private float _targetRainIntensity;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int CoatMaskId = Shader.PropertyToID("_CoatMask");
        private static readonly int CoatSmoothnessId = Shader.PropertyToID("_CoatSmoothness");

        private static readonly int GlobalWetnessId = Shader.PropertyToID("_DhakaGlobalWetness");
        private static readonly int GlobalRainTimeId = Shader.PropertyToID("_DhakaRainTime");
        private static readonly int GlobalRainIntensityId = Shader.PropertyToID("_DhakaRainIntensity");

        public float CurrentWetness => currentWetness;

        public void RegisterPavementMaterial(Material mat)
        {
            if (mat == null || _pavementMaterials.Contains(mat)) return;
            CacheDryBaseline(mat);
            _pavementMaterials.Add(mat);
        }

        public void RegisterVehiclePaintMaterial(Material mat)
        {
            if (mat == null || _carPaintMaterials.Contains(mat)) return;
            CacheDryBaseline(mat);
            _carPaintMaterials.Add(mat);
        }

        public void SetWeatherState(float rainIntensity, float wetnessImmediate = -1f)
        {
            _targetRainIntensity = Mathf.Clamp01(rainIntensity);
            if (wetnessImmediate >= 0f)
            {
                currentWetness = Mathf.Clamp01(wetnessImmediate);
                ApplyWetnessToShadersAndMaterials();
            }
        }

        private void Start()
        {
            ApplyWetnessToShadersAndMaterials();
        }

        private void Update()
        {
            float targetWet = _targetRainIntensity > 0.05f ? Mathf.Clamp01(0.45f + _targetRainIntensity * 0.55f) : 0f;
            float rate = targetWet > currentWetness ? wetUpSpeedPerSec : dryOutSpeedPerSec;
            float nextWet = Mathf.MoveTowards(currentWetness, targetWet, rate * Time.deltaTime);

            Shader.SetGlobalFloat(GlobalRainTimeId, Time.time);
            Shader.SetGlobalFloat(GlobalRainIntensityId, _targetRainIntensity);

            if (Mathf.Abs(nextWet - currentWetness) > 0.002f)
            {
                currentWetness = nextWet;
                ApplyWetnessToShadersAndMaterials();
            }
        }

        public void ApplyWetnessToShadersAndMaterials()
        {
            WorldSurfaceState.GlobalRoadWetness = currentWetness;
            WorldSurfaceState.GlobalRainIntensity = _targetRainIntensity;
            Shader.SetGlobalFloat(GlobalWetnessId, currentWetness);
            Shader.SetGlobalFloat(GlobalRainIntensityId, _targetRainIntensity);

            foreach (Material mat in _pavementMaterials)
            {
                if (mat == null) continue;
                (Color dryColor, float drySmooth) = _dryCache[mat];

                if (mat.HasProperty(BaseColorId))
                {
                    mat.SetColor(BaseColorId, Color.Lerp(dryColor, dryColor * 0.48f, currentWetness));
                }
                if (mat.HasProperty(SmoothnessId))
                {
                    mat.SetFloat(SmoothnessId, Mathf.Lerp(drySmooth, 0.90f, currentWetness));
                }
                if (mat.HasProperty(CoatMaskId))
                {
                    mat.SetFloat(CoatMaskId, currentWetness * 0.85f);
                }
                if (mat.HasProperty(CoatSmoothnessId))
                {
                    mat.SetFloat(CoatSmoothnessId, Mathf.Lerp(0.5f, 0.96f, currentWetness));
                }
            }

            foreach (Material mat in _carPaintMaterials)
            {
                if (mat == null) continue;
                (Color _, float drySmooth) = _dryCache[mat];
                if (mat.HasProperty(SmoothnessId))
                {
                    mat.SetFloat(SmoothnessId, Mathf.Lerp(drySmooth, 0.94f, currentWetness));
                }
                if (mat.HasProperty(CoatMaskId))
                {
                    mat.SetFloat(CoatMaskId, Mathf.Lerp(0.65f, 1.0f, currentWetness));
                }
                if (mat.HasProperty(CoatSmoothnessId))
                {
                    mat.SetFloat(CoatSmoothnessId, 0.96f);
                }
            }
        }

        private void CacheDryBaseline(Material mat)
        {
            if (_dryCache.ContainsKey(mat)) return;
            Color c = mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId) : Color.gray;
            float s = mat.HasProperty(SmoothnessId) ? mat.GetFloat(SmoothnessId) : 0.3f;
            _dryCache[mat] = (c, s);
        }
    }
}
