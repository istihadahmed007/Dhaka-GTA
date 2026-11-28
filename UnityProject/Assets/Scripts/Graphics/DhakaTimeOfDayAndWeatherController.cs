using System.Collections.Generic;
using DhakaGta.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace DhakaGta.Graphics
{
    public enum DhakaWeatherCondition
    {
        GoldenHourClear = 0,
        MiddayHumidSun = 1,
        MonsoonDownpour = 2,
        WetNightReflections = 3,
    }

    /// <summary>
    /// Full HDRP 17.6 dynamic day/night, Volumetric Clouds, Volumetric Fog, Physically Based Sky,
    /// ACES Tonemapping, Anamorphic Bloom, Auto Exposure, and Screen-Space Reflections controller for Dhaka.
    /// Drives smooth real-time lighting transitions across Golden Dusk, Noon, Monsoon Rain, and Wet Night.
    /// </summary>
    public sealed class DhakaTimeOfDayAndWeatherController : MonoBehaviour
    {
        [Header("Scene Lighting & Volume References")]
        [SerializeField] private Light sunLight;
        [SerializeField] private Volume postAndSkyVolume;
        [SerializeField] private WetSurfacePuddleDriver wetSurfaceDriver;
        [SerializeField] private DhakaWeatherVfxController vfxController;
        [SerializeField] private DhakaReflectionProbeRig reflectionProbeRig;

        [Header("Simulation State")]
        [Range(0f, 24f)] [SerializeField] private float timeOfDayHours = 17.2f;
        [SerializeField] private float cycleHoursPerRealMinute;
        [SerializeField] private DhakaWeatherCondition activeCondition = DhakaWeatherCondition.GoldenHourClear;
        [Range(0f, 1f)] [SerializeField] private float currentRainIntensity;
        [SerializeField] private float cloudDriftSpeed = 0.004f;

        private readonly List<Light> _nightStreetLamps = new List<Light>();
        private readonly List<Light> _vehicleHeadlights = new List<Light>();

        private bool _allowVolumetricClouds = true;
        private bool _allowVolumetricFog = true;
        private bool _allowScreenSpaceReflections = true;
        private bool _nightLightsActive;
        private Vector2 _cloudOffsetAccum;

        public float TimeOfDayHours => timeOfDayHours;
        public DhakaWeatherCondition ActiveCondition => activeCondition;
        public float RainIntensity => currentRainIntensity;
        public bool AreNightLightsActive => _nightLightsActive;

        public void Configure(
            Light directionalSun,
            Volume volume,
            WetSurfacePuddleDriver wetDriver,
            DhakaWeatherVfxController vfx,
            DhakaReflectionProbeRig probeRig,
            DhakaWeatherCondition initialCondition = DhakaWeatherCondition.GoldenHourClear)
        {
            sunLight = directionalSun;
            postAndSkyVolume = volume;
            wetSurfaceDriver = wetDriver;
            vfxController = vfx;
            reflectionProbeRig = probeRig;
            ApplyConditionImmediate(initialCondition);
        }

        public void RegisterStreetLamp(Light light)
        {
            if (light != null && !_nightStreetLamps.Contains(light))
            {
                _nightStreetLamps.Add(light);
                light.enabled = _nightLightsActive;
            }
        }

        public void RegisterVehicleHeadlight(Light light)
        {
            if (light != null && !_vehicleHeadlights.Contains(light))
            {
                _vehicleHeadlights.Add(light);
                light.enabled = _nightLightsActive;
            }
        }

        public void SetFeatureBudgets(bool enableClouds, bool enableVolumetricFog, bool enableSsr)
        {
            _allowVolumetricClouds = enableClouds;
            _allowVolumetricFog = enableVolumetricFog;
            _allowScreenSpaceReflections = enableSsr;
            RefreshVolumeOverrides();
        }

        public void AdvanceTimeHours(float deltaHours)
        {
            timeOfDayHours = (timeOfDayHours + deltaHours + 24f) % 24f;
            RefreshLightingAndSky();
        }

        public DhakaWeatherCondition CycleWeatherCondition()
        {
            int next = ((int)activeCondition + 1) % 4;
            ApplyConditionImmediate((DhakaWeatherCondition)next);
            return activeCondition;
        }

        public void ApplyConditionImmediate(DhakaWeatherCondition condition)
        {
            activeCondition = condition;
            float immediateWetness = -1f;

            switch (condition)
            {
                case DhakaWeatherCondition.GoldenHourClear:
                    timeOfDayHours = 17.35f;
                    currentRainIntensity = 0f;
                    immediateWetness = 0.05f;
                    break;
                case DhakaWeatherCondition.MiddayHumidSun:
                    timeOfDayHours = 13.0f;
                    currentRainIntensity = 0f;
                    immediateWetness = 0f;
                    break;
                case DhakaWeatherCondition.MonsoonDownpour:
                    timeOfDayHours = 15.5f;
                    currentRainIntensity = 0.92f;
                    immediateWetness = 0.90f;
                    break;
                case DhakaWeatherCondition.WetNightReflections:
                    timeOfDayHours = 21.5f;
                    currentRainIntensity = 0.35f;
                    immediateWetness = 0.95f;
                    break;
            }

            if (wetSurfaceDriver != null)
            {
                wetSurfaceDriver.SetWeatherState(currentRainIntensity, immediateWetness);
            }
            if (vfxController != null)
            {
                vfxController.SetWeatherParameters(currentRainIntensity, Mathf.Max(0f, immediateWetness));
            }

            RefreshLightingAndSky();
            reflectionProbeRig?.RequestImmediateRefresh();
        }

        private void Start()
        {
            RefreshLightingAndSky();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f6Key.wasPressedThisFrame)
            {
                CycleWeatherCondition();
            }
            if (Keyboard.current != null && Keyboard.current.f7Key.wasPressedThisFrame)
            {
                AdvanceTimeHours(3.0f);
            }

            if (cycleHoursPerRealMinute > 0.001f)
            {
                AdvanceTimeHours((cycleHoursPerRealMinute / 60f) * Time.deltaTime);
            }

            if (postAndSkyVolume != null && postAndSkyVolume.profile != null &&
                postAndSkyVolume.profile.TryGet(out VolumetricClouds clouds) && clouds.enable.value)
            {
                _cloudOffsetAccum += new Vector2(1f, 0.35f) * (cloudDriftSpeed * Time.deltaTime);
                clouds.cloudOffset.overrideState = true;
                clouds.cloudOffset.value = _cloudOffsetAccum;
            }

            if (vfxController != null && wetSurfaceDriver != null)
            {
                vfxController.SetWeatherParameters(currentRainIntensity, wetSurfaceDriver.CurrentWetness);
            }
        }

        public static void ComputeSolarTrajectory(float hour24, out float elevationDeg, out float azimuthDeg, out float daylightFactor)
        {
            // Solar noon ~12.2h reaches ~76 deg at Dhaka (23.7N); dawn ~6:00h, sunset ~18:10h
            float normalizedDay = (hour24 - 6f) / 12.16f; // 0 at 6am, 1 at ~6:10pm
            float rawSin = Mathf.Sin(normalizedDay * Mathf.PI);
            daylightFactor = Mathf.Clamp01(rawSin);
            elevationDeg = rawSin >= 0f ? rawSin * 76f : rawSin * 45f;
            azimuthDeg = Mathf.Lerp(82f, 278f, Mathf.Clamp01(normalizedDay));
        }

        public void RefreshLightingAndSky()
        {
            ComputeSolarTrajectory(timeOfDayHours, out float elevation, out float azimuth, out float daylight);
            WorldSurfaceState.CurrentTimeOfDayHours = timeOfDayHours;

            bool shouldLightNight = elevation < 10f || currentRainIntensity > 0.55f;
            WorldSurfaceState.IsNightOrLowVisibility = shouldLightNight;
            if (shouldLightNight != _nightLightsActive)
            {
                _nightLightsActive = shouldLightNight;
                ApplyNightLightsState(_nightLightsActive);
            }

            if (sunLight != null)
            {
                bool isMoonlight = elevation < 1f;
                float clampedElev = isMoonlight ? Mathf.Max(22f, -elevation) : elevation;
                float effectiveAzimuth = isMoonlight ? (azimuth + 160f) % 360f : azimuth;
                sunLight.transform.rotation = Quaternion.Euler(clampedElev, effectiveAzimuth, 0f);
                sunLight.useColorTemperature = true;

                if (isMoonlight)
                {
                    sunLight.colorTemperature = 8800f;
                    sunLight.intensity = 1200f;
                }
                else
                {
                    float duskFactor = 1f - Mathf.Clamp01(elevation / 26f);
                    sunLight.colorTemperature = Mathf.Lerp(5750f, 3100f, duskFactor);
                    float weatherAtten = Mathf.Lerp(1f, 0.24f, currentRainIntensity);
                    sunLight.intensity = Mathf.Lerp(9000f, 94000f, daylight) * weatherAtten;
                }
            }

            RefreshVolumeOverrides();
        }

        private void RefreshVolumeOverrides()
        {
            if (postAndSkyVolume == null) return;
            VolumeProfile profile = postAndSkyVolume.profile;
            if (profile == null) return;

            // 1. Physically Based Sky (humid tropical Dhaka aerosol haze)
            if (!profile.TryGet(out PhysicallyBasedSky sky)) sky = profile.Add<PhysicallyBasedSky>(true);
            sky.aerosolDensity.overrideState = true;
            sky.aerosolDensity.value = Mathf.Lerp(0.18f, 0.52f, currentRainIntensity);
            sky.aerosolTint.overrideState = true;
            sky.aerosolTint.value = activeCondition == DhakaWeatherCondition.GoldenHourClear
                ? new Color(1.0f, 0.88f, 0.72f)
                : new Color(0.80f, 0.86f, 0.92f);

            // 2. Volumetric Clouds
            if (!profile.TryGet(out VolumetricClouds clouds)) clouds = profile.Add<VolumetricClouds>(true);
            clouds.enable.overrideState = true;
            clouds.enable.value = _allowVolumetricClouds;
            clouds.cloudPreset = currentRainIntensity > 0.65f
                ? VolumetricClouds.CloudPresets.Stormy
                : (currentRainIntensity > 0.15f ? VolumetricClouds.CloudPresets.Overcast : VolumetricClouds.CloudPresets.Cloudy);

            // 3. Atmospheric & Volumetric Fog
            if (!profile.TryGet(out Fog fog)) fog = profile.Add<Fog>(true);
            fog.enabled.overrideState = true;
            fog.enabled.value = true;
            fog.enableVolumetricFog.overrideState = true;
            fog.enableVolumetricFog.value = _allowVolumetricFog;
            fog.colorMode.overrideState = true;
            fog.colorMode.value = FogColorMode.SkyColor;
            fog.meanFreePath.overrideState = true;
            fog.meanFreePath.value = Mathf.Lerp(320f, 85f, currentRainIntensity);
            fog.maxFogDistance.overrideState = true;
            fog.maxFogDistance.value = 3200f;

            // 4. Screen Space Reflections (wet asphalt road & vehicle clear-coat reflections)
            if (!profile.TryGet(out ScreenSpaceReflection ssr)) ssr = profile.Add<ScreenSpaceReflection>(true);
            ssr.enabled.overrideState = true;
            ssr.enabled.value = _allowScreenSpaceReflections;
            ssr.enabledTransparent.overrideState = true;
            ssr.enabledTransparent.value = _allowScreenSpaceReflections;
            ssr.reflectSky.overrideState = true;
            ssr.reflectSky.value = true;
            ssr.usedAlgorithm.overrideState = true;
            ssr.usedAlgorithm.value = ScreenSpaceReflectionAlgorithm.Approximation;
            ssr.accumulationFactor.overrideState = true;
            ssr.accumulationFactor.value = 0.82f;

            // 5. Auto Exposure
            if (!profile.TryGet(out Exposure exp)) exp = profile.Add<Exposure>(true);
            exp.mode.overrideState = true;
            exp.mode.value = ExposureMode.Automatic;
            exp.limitMin.overrideState = true;
            exp.limitMin.value = _nightLightsActive ? -0.5f : 1.5f;
            exp.limitMax.overrideState = true;
            exp.limitMax.value = 13.5f;
            exp.compensation.overrideState = true;
            exp.compensation.value = _nightLightsActive ? 0.45f : 0.10f;

            // 6. ACES Tonemapping & Cinematic Color Grading
            if (!profile.TryGet(out Tonemapping tone)) tone = profile.Add<Tonemapping>(true);
            tone.mode.overrideState = true;
            tone.mode.value = TonemappingMode.ACES;

            if (!profile.TryGet(out ColorAdjustments grading)) grading = profile.Add<ColorAdjustments>(true);
            grading.contrast.overrideState = true;
            grading.contrast.value = activeCondition == DhakaWeatherCondition.WetNightReflections ? 16f : 11f;
            grading.saturation.overrideState = true;
            grading.saturation.value = activeCondition == DhakaWeatherCondition.MonsoonDownpour ? -6f : 9f;
            grading.colorFilter.overrideState = true;
            grading.colorFilter.value = activeCondition switch
            {
                DhakaWeatherCondition.GoldenHourClear => new Color(1.03f, 0.97f, 0.90f),
                DhakaWeatherCondition.MonsoonDownpour => new Color(0.90f, 0.96f, 1.02f),
                DhakaWeatherCondition.WetNightReflections => new Color(0.92f, 0.97f, 1.04f),
                _ => Color.white,
            };

            // 7. Anamorphic Bloom for Headlights & Streetlamps
            if (!profile.TryGet(out Bloom bloom)) bloom = profile.Add<Bloom>(true);
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.15f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = _nightLightsActive ? 0.36f : 0.18f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.68f;
            bloom.anamorphic.overrideState = true;
            bloom.anamorphic.value = true;
        }

        private void ApplyNightLightsState(bool enabledState)
        {
            for (int i = _nightStreetLamps.Count - 1; i >= 0; i--)
            {
                if (_nightStreetLamps[i] != null) _nightStreetLamps[i].enabled = enabledState;
                else _nightStreetLamps.RemoveAt(i);
            }
            for (int i = _vehicleHeadlights.Count - 1; i >= 0; i--)
            {
                if (_vehicleHeadlights[i] != null) _vehicleHeadlights[i].enabled = enabledState;
                else _vehicleHeadlights.RemoveAt(i);
            }
        }
    }
}
