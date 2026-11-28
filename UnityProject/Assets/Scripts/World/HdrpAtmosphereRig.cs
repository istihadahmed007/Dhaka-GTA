using DhakaGta.Data;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace DhakaGta.World
{
    /// <summary>
    /// Controls HDRP Directional Light (sun azimuth/elevation, physical Lux/Kelvin) and VolumeProfile
    /// from WeatherPresetSO assets across Golden Hour, Monsoon, and Motijheel Night presets.
    /// </summary>
    public sealed class HdrpAtmosphereRig : MonoBehaviour
    {
        [SerializeField] private Light sunLight;
        [SerializeField] private Volume skyAndFogVolume;
        [SerializeField] private WeatherPresetSO activePreset;

        public WeatherPresetSO ActivePreset => activePreset;

        private void Awake()
        {
            if (activePreset != null)
            {
                ApplyPreset(activePreset);
            }
        }

        public void ConfigureReferences(Light directionalSun, Volume volume, WeatherPresetSO preset)
        {
            sunLight = directionalSun;
            skyAndFogVolume = volume;
            if (preset != null)
            {
                ApplyPreset(preset);
            }
        }

        public void ApplyPreset(WeatherPresetSO preset)
        {
            activePreset = preset;
            if (preset == null)
            {
                return;
            }

            if (sunLight != null)
            {
                sunLight.type = LightType.Directional;
                sunLight.transform.rotation = Quaternion.Euler(preset.sunElevationDeg, preset.sunAzimuthDeg, 0f);
                sunLight.useColorTemperature = true;
                sunLight.colorTemperature = preset.sunColorTemperatureK;
                sunLight.intensity = preset.sunIntensityLux;
                sunLight.shadows = LightShadows.Soft;

                if (sunLight.GetComponent<HDAdditionalLightData>() == null)
                {
                    sunLight.gameObject.AddComponent<HDAdditionalLightData>();
                }
            }

            if (skyAndFogVolume != null && skyAndFogVolume.profile != null)
            {
                if (skyAndFogVolume.profile.TryGet(out Fog fog))
                {
                    fog.meanFreePath.overrideState = true;
                    fog.meanFreePath.value = Mathf.Max(20f, preset.fogAttenuationDistanceMeters);
                    fog.anisotropy.overrideState = true;
                    fog.anisotropy.value = preset.volumetricFogAnisotropy;
                }
            }
        }
    }
}
