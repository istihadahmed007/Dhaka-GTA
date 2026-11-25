using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace ProjectDhaka
{
    /// <summary>
    /// Controls Dhaka monsoon weather (rain intensity, wet road & puddle reflections)
    /// and dynamic time-of-day presets (Monsoon Dusk, Golden Sunset, Dhaka Night) toggled via M and N.
    /// Defaults to Monsoon Dusk to preserve Project Dhaka's signature lighting palette.
    /// </summary>
    public class DhakaAtmosphereController : MonoBehaviour
    {
        public enum TimeOfDayPreset
        {
            MonsoonDusk = 0,
            GoldenSunset = 1,
            DhakaNight = 2
        }

        [Header("Scene Lighting & Environment")]
        [SerializeField] Light sunLight;
        [SerializeField] ParticleSystem rainParticles;
        [SerializeField] Material wetRoadMaterial;
        [SerializeField] Material puddleMaterial;
        [SerializeField] Renderer[] sodiumBulbRenderers;

        [Header("Initial States")]
        [SerializeField] TimeOfDayPreset currentTimePreset = TimeOfDayPreset.MonsoonDusk;
        [SerializeField] int rainLevel = 1; // 0 = Clear, 1 = Monsoon Drizzle, 2 = Heavy Downpour

        Transform followCam;
        float rainTargetRate = 420f;
        float currentRoadSmoothness = 0.76f;

        void Start()
        {
            if (sunLight == null)
            {
                var lights = FindObjectsByType<Light>();
                for (int i = 0; i < lights.Length; i++)
                {
                    if (lights[i].type == LightType.Directional)
                    {
                        sunLight = lights[i];
                        break;
                    }
                }
            }
            if (Camera.main != null) followCam = Camera.main.transform;
            if (rainParticles == null) CreateDefaultRainSystem();
            ApplyAtmosphereImmediate();
        }

        public void SetPreset(TimeOfDayPreset preset, int rain)
        {
            currentTimePreset = preset;
            rainLevel = Mathf.Clamp(rain, 0, 2);
            ApplyAtmosphereImmediate();
        }

        MaterialPropertyBlock sodiumPropBlock;

        public void BindReferences(Light sun, ParticleSystem rain, Material roadMat, Material puddleMat, Renderer[] bulbs)
        {
            sunLight = sun;
            rainParticles = rain;
            wetRoadMaterial = roadMat;
            puddleMaterial = puddleMat;
            sodiumBulbRenderers = bulbs;
            if (Application.isPlaying) ApplyAtmosphereImmediate();
        }

        void Update()
        {
            if (followCam == null && Camera.main != null) followCam = Camera.main.transform;
            if (followCam != null && rainParticles != null)
            {
                Vector3 camPos = followCam.position;
                rainParticles.transform.position = new Vector3(camPos.x, Mathf.Max(10f, camPos.y + 8f), camPos.z);
            }

            GameLoop loop = GameLoop.Instance;
            if (loop == null || loop.CurrentState == GameLoop.PlayState.Playing)
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.mKey.wasPressedThisFrame)
                    {
                        rainLevel = (rainLevel + 1) % 3;
                        ApplyRainSettings();
                    }
                    if (kb.nKey.wasPressedThisFrame)
                    {
                        currentTimePreset = (TimeOfDayPreset)(((int)currentTimePreset + 1) % 3);
                        ApplyLightingPreset();
                    }
                }
            }

            // Smooth road wet sheen adaptation
            float targetSmooth = rainLevel == 0 ? 0.42f : (rainLevel == 1 ? 0.78f : 0.90f);
            currentRoadSmoothness = Mathf.Lerp(currentRoadSmoothness, targetSmooth, Time.deltaTime * 1.5f);
            if (wetRoadMaterial != null && wetRoadMaterial.HasProperty("_Smoothness"))
            {
                wetRoadMaterial.SetFloat("_Smoothness", currentRoadSmoothness);
            }
            if (puddleMaterial != null && puddleMaterial.HasProperty("_BaseColor"))
            {
                Color c = puddleMaterial.GetColor("_BaseColor");
                c.a = rainLevel == 0 ? 0.12f : (rainLevel == 1 ? 0.65f : 0.86f);
                puddleMaterial.SetColor("_BaseColor", c);
            }
        }

        void ApplyAtmosphereImmediate()
        {
            ApplyLightingPreset();
            ApplyRainSettings();
        }

        void ApplyRainSettings()
        {
            if (rainParticles == null) return;
            var emission = rainParticles.emission;
            rainTargetRate = rainLevel == 0 ? 0f : (rainLevel == 1 ? 380f : 950f);
            emission.rateOverTime = rainTargetRate;
            if (rainLevel == 0 && rainParticles.isPlaying) rainParticles.Stop();
            else if (rainLevel > 0 && !rainParticles.isPlaying) rainParticles.Play();
        }

        void ApplyLightingPreset()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            switch (currentTimePreset)
            {
                case TimeOfDayPreset.GoldenSunset:
                    if (sunLight != null)
                    {
                        sunLight.color = new Color(1.0f, 0.78f, 0.54f);
                        sunLight.intensity = 2.0f;
                        sunLight.transform.rotation = Quaternion.Euler(22f, -48f, 0f);
                    }
                    RenderSettings.ambientSkyColor = new Color(0.54f, 0.46f, 0.58f);
                    RenderSettings.ambientEquatorColor = new Color(0.62f, 0.46f, 0.36f);
                    RenderSettings.ambientGroundColor = new Color(0.20f, 0.17f, 0.16f);
                    RenderSettings.fogColor = new Color(0.58f, 0.48f, 0.44f);
                    RenderSettings.fogDensity = 0.0045f;
                    SetSodiumEmission(0.55f);
                    break;

                case TimeOfDayPreset.DhakaNight:
                    if (sunLight != null)
                    {
                        sunLight.color = new Color(0.36f, 0.48f, 0.76f);
                        sunLight.intensity = 0.55f;
                        sunLight.transform.rotation = Quaternion.Euler(52f, 24f, 0f);
                    }
                    RenderSettings.ambientSkyColor = new Color(0.11f, 0.16f, 0.28f);
                    RenderSettings.ambientEquatorColor = new Color(0.14f, 0.18f, 0.24f);
                    RenderSettings.ambientGroundColor = new Color(0.06f, 0.07f, 0.09f);
                    RenderSettings.fogColor = new Color(0.08f, 0.11f, 0.17f);
                    RenderSettings.fogDensity = 0.0068f;
                    SetSodiumEmission(2.4f);
                    break;

                default: // MonsoonDusk
                    if (sunLight != null)
                    {
                        sunLight.color = Palette.KeyLight;
                        sunLight.intensity = 1.45f;
                        sunLight.transform.rotation = Quaternion.Euler(34f, -32f, 0f);
                    }
                    RenderSettings.ambientSkyColor = Palette.AmbientSky;
                    RenderSettings.ambientEquatorColor = Palette.AmbientEquator;
                    RenderSettings.ambientGroundColor = Palette.AmbientGround;
                    RenderSettings.fogColor = Palette.Fog;
                    RenderSettings.fogDensity = 0.0052f;
                    SetSodiumEmission(1.35f);
                    break;
            }
        }

        void SetSodiumEmission(float multiplier)
        {
            if (sodiumBulbRenderers == null) return;
            sodiumPropBlock ??= new MaterialPropertyBlock();
            Color sodiumGlow = new Color(1.0f, 0.72f, 0.28f) * multiplier;
            for (int i = 0; i < sodiumBulbRenderers.Length; i++)
            {
                var r = sodiumBulbRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(sodiumPropBlock);
                sodiumPropBlock.SetColor("_EmissionColor", sodiumGlow);
                r.SetPropertyBlock(sodiumPropBlock);
            }
        }

        void CreateDefaultRainSystem()
        {
            var go = new GameObject("MonsoonRain_Particles");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0f, 14f, 0f);
            go.transform.rotation = Quaternion.Euler(8f, 14f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 0.85f;
            main.startSpeed = 24f;
            main.startSize = 0.06f;
            main.maxParticles = 1400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.76f, 0.85f, 0.96f, 0.42f);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(38f, 1f, 38f);

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Stretch;
            psr.velocityScale = 0.045f;
            psr.lengthScale = 3.2f;
            Shader unlit = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit");
            var mat = new Material(unlit) { name = "Mat_MonsoonRain" };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.76f, 0.86f, 0.96f, 0.38f));
            psr.sharedMaterial = mat;

            rainParticles = ps;
        }
    }
}
