using DhakaGta.Core;
using DhakaGta.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DhakaGta.Vehicles
{
    /// <summary>
    /// Realistic Unity Rigidbody + 4x WheelCollider vehicle controller built for testing generated 1:1 Dhaka roads,
    /// roundabouts, bridges, flyovers, and intersections. Supports live Input System + deterministic PlayMode test input.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DhakaWheelVehicleController : MonoBehaviour, IVehicleDrivable
    {
        [Header("Vehicle Spec & Setup")]
        [SerializeField] private VehicleSpecSO vehicleSpec;
        [SerializeField] private float motorTorqueNm = 1650f;
        [SerializeField] private float brakeTorqueNm = 2800f;
        [SerializeField] private float maxSteerAngleDeg = 32f;
        [SerializeField] private float maxSpeedKph = 120f;
        [SerializeField] private float antiRollStiffness = 5000f;

        [Header("Wheel Colliders (FL, FR, RL, RR)")]
        [SerializeField] private WheelCollider frontLeftCollider;
        [SerializeField] private WheelCollider frontRightCollider;
        [SerializeField] private WheelCollider rearLeftCollider;
        [SerializeField] private WheelCollider rearRightCollider;

        [Header("Optional Wheel Visual Transforms")]
        [SerializeField] private Transform frontLeftMesh;
        [SerializeField] private Transform frontRightMesh;
        [SerializeField] private Transform rearLeftMesh;
        [SerializeField] private Transform rearRightMesh;

        private Rigidbody _rb;
        private Vector3 _lastPosition;
        private bool _hasSyntheticInput;
        private float _syntheticThrottle;
        private float _syntheticSteer;
        private bool _syntheticHandbrake;

        public VehicleSpecSO Spec => vehicleSpec;
        public bool IsOccupied { get; private set; } = true;
        public float ForwardSpeedKph => _rb != null ? Vector3.Dot(_rb.linearVelocity, transform.forward) * 3.6f : 0f;
        public float DistanceDrivenMeters { get; private set; }
        public int GroundedWheelCount { get; private set; }
        public bool IsAnyWheelGrounded => GroundedWheelCount > 0;

        public void EnterDriverSeat() => IsOccupied = true;
        public void ExitDriverSeat() => IsOccupied = false;

        public void ConfigureWheels(
            VehicleSpecSO spec,
            WheelCollider fl, WheelCollider fr, WheelCollider rl, WheelCollider rr,
            Transform flMesh = null, Transform frMesh = null, Transform rlMesh = null, Transform rrMesh = null)
        {
            vehicleSpec = spec;
            frontLeftCollider = fl;
            frontRightCollider = fr;
            rearLeftCollider = rl;
            rearRightCollider = rr;
            frontLeftMesh = flMesh;
            frontRightMesh = frMesh;
            rearLeftMesh = rlMesh;
            rearRightMesh = rrMesh;

            if (spec != null)
            {
                motorTorqueNm = Mathf.Max(900f, spec.maxMotorTorqueNm * 2.5f);
                maxSteerAngleDeg = spec.maxSteerAngleDeg;
                maxSpeedKph = spec.maxSpeedKph;
            }
        }

        public void SetSyntheticDriveInput(float throttleBrake, float steer, bool handbrake = false)
        {
            _hasSyntheticInput = true;
            _syntheticThrottle = Mathf.Clamp(throttleBrake, -1f, 1f);
            _syntheticSteer = Mathf.Clamp(steer, -1f, 1f);
            _syntheticHandbrake = handbrake;
        }

        public void ClearSyntheticDriveInput()
        {
            _hasSyntheticInput = false;
            _syntheticThrottle = 0f;
            _syntheticSteer = 0f;
            _syntheticHandbrake = false;
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.mass = vehicleSpec != null ? Mathf.Max(380f, vehicleSpec.massKg) : 1420f;
            _rb.centerOfMass = new Vector3(0f, -0.35f, 0.1f);
            gameObject.layer = PhysicsLayers.Vehicle;
            _lastPosition = transform.position;
        }

        private void FixedUpdate()
        {
            ReadControls(out float throttle, out float steer, out bool handbrake);
            ApplyDrivetrain(throttle, steer, handbrake);
            ApplyAntiRollBar(frontLeftCollider, frontRightCollider);
            ApplyAntiRollBar(rearLeftCollider, rearRightCollider);
            SyncVisualWheels();

            DistanceDrivenMeters += Vector3.Distance(transform.position, _lastPosition);
            _lastPosition = transform.position;
        }

        private void ReadControls(out float throttle, out float steer, out bool handbrake)
        {
            if (_hasSyntheticInput)
            {
                throttle = _syntheticThrottle;
                steer = _syntheticSteer;
                handbrake = _syntheticHandbrake;
                return;
            }

            throttle = 0f;
            steer = 0f;
            handbrake = false;

            if (!IsOccupied || Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) throttle += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) throttle -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) steer += 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) steer -= 1f;
            handbrake = Keyboard.current.spaceKey.isPressed;
        }

        private void ApplyDrivetrain(float throttle, float steer, bool handbrake)
        {
            float speedKph = Mathf.Abs(ForwardSpeedKph);
            float steerAngle = steer * maxSteerAngleDeg * Mathf.Lerp(1f, 0.45f, Mathf.Clamp01(speedKph / 90f));
            if (frontLeftCollider != null) frontLeftCollider.steerAngle = steerAngle;
            if (frontRightCollider != null) frontRightCollider.steerAngle = steerAngle;

            float torque = speedKph < maxSpeedKph ? throttle * motorTorqueNm : 0f;
            bool braking = handbrake || (Mathf.Sign(throttle) != Mathf.Sign(ForwardSpeedKph) && speedKph > 4f && Mathf.Abs(throttle) > 0.1f);
            float appliedBrake = braking ? brakeTorqueNm : 0f;

            // Balanced AWD torque distribution across all 4 wheels for reliable curb & ramp climbing
            ApplyWheelForce(frontLeftCollider, torque * 0.45f, handbrake ? 0f : appliedBrake);
            ApplyWheelForce(frontRightCollider, torque * 0.45f, handbrake ? 0f : appliedBrake);
            ApplyWheelForce(rearLeftCollider, torque * 0.55f, handbrake ? brakeTorqueNm * 1.4f : appliedBrake);
            ApplyWheelForce(rearRightCollider, torque * 0.55f, handbrake ? brakeTorqueNm * 1.4f : appliedBrake);

            int grounded = 0;
            if (IsGrounded(frontLeftCollider)) grounded++;
            if (IsGrounded(frontRightCollider)) grounded++;
            if (IsGrounded(rearLeftCollider)) grounded++;
            if (IsGrounded(rearRightCollider)) grounded++;
            GroundedWheelCount = grounded;
        }

        private static void ApplyWheelForce(WheelCollider wheel, float motor, float brake)
        {
            if (wheel == null) return;
            wheel.motorTorque = motor;
            wheel.brakeTorque = brake;
        }

        private static bool IsGrounded(WheelCollider wheel)
        {
            return wheel != null && wheel.GetGroundHit(out _);
        }

        private void ApplyAntiRollBar(WheelCollider left, WheelCollider right)
        {
            if (left == null || right == null || _rb == null) return;

            float travelL = 1f;
            float travelR = 1f;
            bool groundedL = left.GetGroundHit(out WheelHit hitL);
            if (groundedL)
            {
                travelL = (-left.transform.InverseTransformPoint(hitL.point).y - left.radius) / Mathf.Max(0.05f, left.suspensionDistance);
            }
            bool groundedR = right.GetGroundHit(out WheelHit hitR);
            if (groundedR)
            {
                travelR = (-right.transform.InverseTransformPoint(hitR.point).y - right.radius) / Mathf.Max(0.05f, right.suspensionDistance);
            }

            float antiRollForce = (travelL - travelR) * antiRollStiffness;
            if (groundedL) _rb.AddForceAtPosition(left.transform.up * -antiRollForce, left.transform.position);
            if (groundedR) _rb.AddForceAtPosition(right.transform.up * antiRollForce, right.transform.position);
        }

        private void SyncVisualWheels()
        {
            SyncSingleWheel(frontLeftCollider, frontLeftMesh);
            SyncSingleWheel(frontRightCollider, frontRightMesh);
            SyncSingleWheel(rearLeftCollider, rearLeftMesh);
            SyncSingleWheel(rearRightCollider, rearRightMesh);
        }

        private static void SyncSingleWheel(WheelCollider col, Transform meshTransform)
        {
            if (col == null || meshTransform == null) return;
            col.GetWorldPose(out Vector3 pos, out Quaternion rot);
            meshTransform.position = pos;
            meshTransform.rotation = rot;
        }
    }
}
