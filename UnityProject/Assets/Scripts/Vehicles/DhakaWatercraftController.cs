using DhakaGta.Core;
using DhakaGta.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DhakaGta.Vehicles
{
    /// <summary>
    /// Archimedes multi-point hull buoyancy & marine hydrodynamics controller for Speedboat & Buriganga Launch.
    /// Operates across Water trigger layers and gentle river wave surfaces.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DhakaWatercraftController : MonoBehaviour, IVehicleDrivable
    {
        [SerializeField] private VehicleSpecSO watercraftSpec;
        [SerializeField] private float waterSurfaceLevelY = -0.35f;
        [SerializeField] private float waveAmplitudeMeters = 0.08f;
        [SerializeField] private float forwardThrustNewtons = 22000f;
        [SerializeField] private float rudderTorque = 18000f;
        [SerializeField] private Transform[] hullBuoyancyPoints = System.Array.Empty<Transform>();

        private Rigidbody _rb;
        private bool _hasSyntheticInput;
        private float _synThrottle;
        private float _synSteer;

        public VehicleSpecSO Spec => watercraftSpec;
        public bool IsOccupied { get; private set; }
        public float ForwardSpeedKph => _rb != null ? Vector3.Dot(_rb.linearVelocity, transform.forward) * 3.6f : 0f;
        public int SubmergedPontoonCount { get; private set; }
        public bool IsFloating => SubmergedPontoonCount > 0;

        public void EnterDriverSeat() => IsOccupied = true;
        public void ExitDriverSeat() => IsOccupied = false;

        public void ConfigureWatercraft(VehicleSpecSO spec, Transform[] pontoons, float waterLevelY = -0.35f)
        {
            watercraftSpec = spec;
            hullBuoyancyPoints = pontoons ?? System.Array.Empty<Transform>();
            waterSurfaceLevelY = waterLevelY;

            if (_rb == null) _rb = GetComponent<Rigidbody>();
            if (spec != null && _rb != null)
            {
                _rb.mass = Mathf.Max(300f, spec.massKg);
                _rb.linearDamping = spec.waterDrag;
                _rb.angularDamping = spec.waterAngularDrag;
                forwardThrustNewtons = spec.maxMotorTorqueNm * 18f;
                rudderTorque = _rb.mass * 3.5f;
            }
        }

        public void SetSyntheticMarineInput(float throttle, float steer)
        {
            _hasSyntheticInput = true;
            _synThrottle = Mathf.Clamp(throttle, -1f, 1f);
            _synSteer = Mathf.Clamp(steer, -1f, 1f);
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            gameObject.layer = PhysicsLayers.Vehicle;
            _rb.centerOfMass = new Vector3(0f, -0.45f, 0f);
        }

        private void FixedUpdate()
        {
            ApplyBuoyancy();
            ReadInput(out float throttle, out float steer);
            ApplyMarinePropulsion(throttle, steer);
        }

        private void ApplyBuoyancy()
        {
            if (_rb == null) return;

            Vector3[] fallbackOffsets =
            {
                new Vector3(-1f, 0f, 1.5f),
                new Vector3(1f, 0f, 1.5f),
                new Vector3(-1f, 0f, -1.5f),
                new Vector3(1f, 0f, -1.5f),
            };

            int pointCount = hullBuoyancyPoints != null && hullBuoyancyPoints.Length > 0 ? hullBuoyancyPoints.Length : fallbackOffsets.Length;
            float gravityPerPoint = _rb.mass * Physics.gravity.magnitude / pointCount;
            int submerged = 0;

            for (int i = 0; i < pointCount; i++)
            {
                Vector3 ptWorld = (hullBuoyancyPoints != null && hullBuoyancyPoints.Length > 0)
                    ? hullBuoyancyPoints[i].position
                    : transform.TransformPoint(fallbackOffsets[i]);

                float waveY = waterSurfaceLevelY + Mathf.Sin(Time.time * 1.4f + ptWorld.x * 0.25f) * waveAmplitudeMeters;
                float depth = waveY - ptWorld.y;
                if (depth > 0f)
                {
                    submerged++;
                    float displaceMult = Mathf.Clamp(depth * 2.2f, 0f, 3.2f);
                    Vector3 buoyForce = Vector3.up * (gravityPerPoint * displaceMult);
                    Vector3 pointVel = _rb.GetPointVelocity(ptWorld);
                    Vector3 damping = -pointVel * (gravityPerPoint * 0.14f);
                    _rb.AddForceAtPosition(buoyForce + new Vector3(0f, damping.y, 0f), ptWorld, ForceMode.Force);
                }
            }

            SubmergedPontoonCount = submerged;
        }

        private void ApplyMarinePropulsion(float throttle, float steer)
        {
            if (_rb == null || !IsFloating) return;

            float maxKph = watercraftSpec != null ? watercraftSpec.maxSpeedKph : 68f;
            float spd = ForwardSpeedKph;
            if (Mathf.Abs(throttle) > 0.05f && Mathf.Abs(spd) < maxKph)
            {
                float dirScale = throttle >= 0f ? throttle : throttle * 0.5f;
                Vector3 fwdHoriz = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
                _rb.AddForce(fwdHoriz * (forwardThrustNewtons * dirScale), ForceMode.Force);
            }

            float steerSpeedFactor = Mathf.Clamp01(0.35f + Mathf.Abs(spd) / 25f);
            _rb.AddTorque(Vector3.up * (steer * rudderTorque * steerSpeedFactor), ForceMode.Force);
        }

        private void ReadInput(out float throttle, out float steer)
        {
            if (_hasSyntheticInput)
            {
                throttle = _synThrottle;
                steer = _synSteer;
                return;
            }

            throttle = 0f;
            steer = 0f;
            if (!IsOccupied || Keyboard.current == null) return;
            if (Keyboard.current.wKey.isPressed) throttle += 1f;
            if (Keyboard.current.sKey.isPressed) throttle -= 1f;
            if (Keyboard.current.dKey.isPressed) steer += 1f;
            if (Keyboard.current.aKey.isPressed) steer -= 1f;
        }
    }
}
