using DhakaGta.Vehicles;
using UnityEngine;

namespace DhakaGta.Graphics
{
    /// <summary>
    /// Controls dynamic 3D Monsoon Rain streaks following the camera dome, ground droplet splashes,
    /// and velocity-scaled rear-wheel road spray behind DhakaWheelVehicleController on wet asphalt.
    /// Zero static screen overlays.
    /// </summary>
    public sealed class DhakaWeatherVfxController : MonoBehaviour
    {
        [SerializeField] private Transform cameraFollowAnchor;
        [SerializeField] private MonoBehaviour trackedVehicleBehaviour;
        [SerializeField] private ParticleSystem rainStreaksSystem;
        [SerializeField] private ParticleSystem groundSplashesSystem;
        [SerializeField] private ParticleSystem wheelSprayLeft;
        [SerializeField] private ParticleSystem wheelSprayRight;

        private IVehicleDrivable _trackedDrivable;
        private float _rainIntensity;
        private float _roadWetness;
        private float _qualityParticleBudgetMultiplier = 1f;

        public void Configure(
            Transform cameraTransform,
            MonoBehaviour vehicleBehaviour,
            ParticleSystem rain,
            ParticleSystem splashes,
            ParticleSystem sprayL,
            ParticleSystem sprayR)
        {
            cameraFollowAnchor = cameraTransform;
            trackedVehicleBehaviour = vehicleBehaviour;
            _trackedDrivable = vehicleBehaviour as IVehicleDrivable;
            rainStreaksSystem = rain;
            groundSplashesSystem = splashes;
            wheelSprayLeft = sprayL;
            wheelSprayRight = sprayR;
        }

        public void SetWeatherParameters(float rainIntensity, float wetness)
        {
            _rainIntensity = Mathf.Clamp01(rainIntensity);
            _roadWetness = Mathf.Clamp01(wetness);
        }

        public void SetQualityBudgetMultiplier(float mult)
        {
            _qualityParticleBudgetMultiplier = Mathf.Clamp(mult, 0.25f, 1.5f);
        }

        private void Update()
        {
            if (cameraFollowAnchor != null)
            {
                Vector3 camPos = cameraFollowAnchor.position;
                if (rainStreaksSystem != null)
                {
                    rainStreaksSystem.transform.position = camPos + cameraFollowAnchor.forward * 10f + Vector3.up * 16f;
                }
                if (groundSplashesSystem != null)
                {
                    groundSplashesSystem.transform.position = new Vector3(camPos.x, 0.1f, camPos.z) + cameraFollowAnchor.forward * 12f;
                }
            }

            UpdateEmissionRate(rainStreaksSystem, _rainIntensity * 950f * _qualityParticleBudgetMultiplier);
            UpdateEmissionRate(groundSplashesSystem, _rainIntensity * 320f * _qualityParticleBudgetMultiplier);

            float sprayFactor = 0f;
            IVehicleDrivable drivable = _trackedDrivable ?? trackedVehicleBehaviour as IVehicleDrivable;
            if (drivable != null && _roadWetness > 0.12f)
            {
                bool grounded = trackedVehicleBehaviour is DhakaVehicleController rayCar ? rayCar.IsAnyWheelGrounded
                              : (trackedVehicleBehaviour is DhakaWheelVehicleController wcCar ? wcCar.IsAnyWheelGrounded : true);
                if (grounded)
                {
                    float speed = Mathf.Abs(drivable.ForwardSpeedKph);
                    sprayFactor = Mathf.Clamp01((speed - 12f) / 65f) * _roadWetness;
                }
            }

            float sprayRate = sprayFactor * 140f * _qualityParticleBudgetMultiplier;
            UpdateEmissionRate(wheelSprayLeft, sprayRate);
            UpdateEmissionRate(wheelSprayRight, sprayRate);
        }

        private static void UpdateEmissionRate(ParticleSystem ps, float ratePerSecond)
        {
            if (ps == null) return;
            ParticleSystem.EmissionModule em = ps.emission;
            em.enabled = ratePerSecond > 1f;
            em.rateOverTime = ratePerSecond;
            if (em.enabled && !ps.isPlaying) ps.Play();
            else if (!em.enabled && ps.isPlaying) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        /// <summary>
        /// Builds clean runtime-configured 3D rain, ground splash, and tire road-spray ParticleSystems.
        /// </summary>
        public static DhakaWeatherVfxController BuildRuntimeVfxRig(
            Transform hostTransform,
            Transform camTransform,
            MonoBehaviour vehicleBehaviour,
            Material particleMat)
        {
            ParticleSystem rain = CreateRainDomeSystem(hostTransform, particleMat);
            ParticleSystem splash = CreateGroundSplashSystem(hostTransform, particleMat);

            ParticleSystem sprayL = null;
            ParticleSystem sprayR = null;
            if (vehicleBehaviour != null)
            {
                sprayL = CreateTireSpraySystem(vehicleBehaviour.transform, "TireSpray_RL", new Vector3(-0.84f, 0.18f, -1.65f), particleMat);
                sprayR = CreateTireSpraySystem(vehicleBehaviour.transform, "TireSpray_RR", new Vector3(0.84f, 0.18f, -1.65f), particleMat);
            }

            var ctrl = hostTransform.gameObject.GetComponent<DhakaWeatherVfxController>();
            if (ctrl == null) ctrl = hostTransform.gameObject.AddComponent<DhakaWeatherVfxController>();
            ctrl.Configure(camTransform, vehicleBehaviour, rain, splash, sprayL, sprayR);
            return ctrl;
        }

        private static ParticleSystem CreateRainDomeSystem(Transform parent, Material mat)
        {
            var go = new GameObject("VFX_MonsoonRainStreaks");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.9f;
            main.startSpeed = 24f;
            main.startSize = 0.06f;
            main.startColor = new Color(0.75f, 0.85f, 0.95f, 0.45f);
            main.maxParticles = 2500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            go.transform.rotation = Quaternion.Euler(96f, 15f, 0f);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(36f, 36f, 2f);

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.lengthScale = 6.5f;
            if (mat != null) rend.sharedMaterial = mat;
            ps.Stop();
            return ps;
        }

        private static ParticleSystem CreateGroundSplashSystem(Transform parent, Material mat)
        {
            var go = new GameObject("VFX_RainGroundSplashes");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.25f;
            main.startSpeed = 2.2f;
            main.gravityModifier = 0.9f;
            main.startSize = 0.14f;
            main.startColor = new Color(0.8f, 0.88f, 0.94f, 0.35f);
            main.maxParticles = 800;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(28f, 28f, 0.2f);

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            if (mat != null) rend.sharedMaterial = mat;
            ps.Stop();
            return ps;
        }

        private static ParticleSystem CreateTireSpraySystem(Transform vehicleTransform, string name, Vector3 localOffset, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(vehicleTransform, false);
            go.transform.localPosition = localOffset;
            go.transform.localRotation = Quaternion.Euler(-18f, 180f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.55f;
            main.startSpeed = 5.5f;
            main.startSize = 0.48f;
            main.gravityModifier = 0.45f;
            main.startColor = new Color(0.80f, 0.84f, 0.88f, 0.28f);
            main.maxParticles = 350;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.16f;

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            if (mat != null) rend.sharedMaterial = mat;
            ps.Stop();
            return ps;
        }
    }
}
