using System;
using DhakaGta.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DhakaGta.Graphics
{
    [Serializable]
    public sealed class PcGraphicsPresetConfig
    {
        public HdrpQualityTier qualityTier = HdrpQualityTier.Balanced;
        public bool vSyncEnabled = true;
        public int targetFrameRate = 60;
        public float textureStreamingBudgetMb = 384f;
        public float lodBias = 1.0f;
        public float shadowDistanceMeters = 75f;
        public bool volumetricCloudsEnabled = true;
        public bool volumetricFogEnabled = true;
        public bool screenSpaceReflectionsEnabled = true;
    }

    /// <summary>
    /// Configurable PC Graphics Preset Controller syncing Unity QualitySettings, Texture Mipmap Streaming,
    /// LOD Bias, VSync/FrameCap, and HDRP Volume budgets (Volumetric Clouds/Fog, SSR, VFX).
    /// Press [F8] at runtime to cycle presets or apply custom PcGraphicsPresetConfig via PlayerPrefs/UI.
    /// </summary>
    public sealed class DhakaGraphicsQualityManager : MonoBehaviour
    {
        private const string GraphicsPrefKey = "DhakaPcGraphicsConfig_v1";

        [SerializeField] private HdrpQualityTier currentTier = HdrpQualityTier.Balanced;
        [SerializeField] private DhakaTimeOfDayAndWeatherController weatherController;
        [SerializeField] private DhakaWeatherVfxController vfxController;

        public HdrpQualityTier CurrentTier => currentTier;
        public PcGraphicsPresetConfig ActiveConfig { get; private set; } = new PcGraphicsPresetConfig();

        public void Configure(
            DhakaTimeOfDayAndWeatherController weather,
            DhakaWeatherVfxController vfx,
            HdrpQualityTier initialTier)
        {
            weatherController = weather;
            vfxController = vfx;
            ApplyQualityPreset(initialTier);
        }

        private void Awake()
        {
            string savedJson = PlayerPrefs.GetString(GraphicsPrefKey, string.Empty);
            if (!string.IsNullOrEmpty(savedJson))
            {
                var cfg = JsonUtility.FromJson<PcGraphicsPresetConfig>(savedJson);
                if (cfg != null)
                {
                    ApplyCustomConfig(cfg, saveToPrefs: false);
                    return;
                }
            }

            if (SystemInfo.graphicsMemorySize > 0 && SystemInfo.graphicsMemorySize <= 1024)
            {
                currentTier = HdrpQualityTier.Performant;
            }
            ApplyQualityPreset(currentTier);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
            {
                CycleQualityPreset();
            }
        }

        public void CycleQualityPreset()
        {
            int next = ((int)currentTier + 1) % 3;
            ApplyQualityPreset((HdrpQualityTier)next);
        }

        public void ApplyQualityPreset(HdrpQualityTier tier)
        {
            PcGraphicsPresetConfig preset = tier switch
            {
                HdrpQualityTier.HighFidelity => new PcGraphicsPresetConfig
                {
                    qualityTier = HdrpQualityTier.HighFidelity,
                    vSyncEnabled = true,
                    targetFrameRate = 0,
                    textureStreamingBudgetMb = 512f,
                    lodBias = 1.25f,
                    shadowDistanceMeters = 120f,
                    volumetricCloudsEnabled = true,
                    volumetricFogEnabled = true,
                    screenSpaceReflectionsEnabled = true
                },
                HdrpQualityTier.Performant => new PcGraphicsPresetConfig
                {
                    qualityTier = HdrpQualityTier.Performant,
                    vSyncEnabled = false,
                    targetFrameRate = 60,
                    textureStreamingBudgetMb = 256f,
                    lodBias = 0.70f,
                    shadowDistanceMeters = 40f,
                    volumetricCloudsEnabled = false,
                    volumetricFogEnabled = false,
                    screenSpaceReflectionsEnabled = false
                },
                _ => new PcGraphicsPresetConfig
                {
                    qualityTier = HdrpQualityTier.Balanced,
                    vSyncEnabled = true,
                    targetFrameRate = 60,
                    textureStreamingBudgetMb = 384f,
                    lodBias = 1.0f,
                    shadowDistanceMeters = 75f,
                    volumetricCloudsEnabled = true,
                    volumetricFogEnabled = true,
                    screenSpaceReflectionsEnabled = true
                }
            };

            ApplyCustomConfig(preset, saveToPrefs: true);
        }

        public void ApplyCustomConfig(PcGraphicsPresetConfig cfg, bool saveToPrefs = true)
        {
            if (cfg == null) return;
            ActiveConfig = cfg;
            currentTier = cfg.qualityTier;

            if (QualitySettings.names != null && QualitySettings.names.Length > (int)currentTier)
            {
                QualitySettings.SetQualityLevel((int)currentTier, applyExpensiveChanges: true);
            }

            QualitySettings.streamingMipmapsActive = true;
            QualitySettings.streamingMipmapsAddAllCameras = true;
            QualitySettings.streamingMipmapsMemoryBudget = Mathf.Clamp(cfg.textureStreamingBudgetMb, 128f, 2048f);
            QualitySettings.lodBias = Mathf.Clamp(cfg.lodBias, 0.5f, 2.0f);
            QualitySettings.shadowDistance = Mathf.Clamp(cfg.shadowDistanceMeters, 25f, 200f);
            QualitySettings.vSyncCount = cfg.vSyncEnabled ? 1 : 0;
            Application.targetFrameRate = cfg.vSyncEnabled ? -1 : cfg.targetFrameRate;

            GameBootstrap.ApplyQualityTier(currentTier);

            float particleBudget = currentTier switch
            {
                HdrpQualityTier.HighFidelity => 1.35f,
                HdrpQualityTier.Balanced => 0.90f,
                _ => 0.45f,
            };

            weatherController?.SetFeatureBudgets(
                cfg.volumetricCloudsEnabled,
                cfg.volumetricFogEnabled,
                cfg.screenSpaceReflectionsEnabled);
            vfxController?.SetQualityBudgetMultiplier(particleBudget);

            if (saveToPrefs)
            {
                PlayerPrefs.SetString(GraphicsPrefKey, JsonUtility.ToJson(ActiveConfig));
            }
        }
    }
}
