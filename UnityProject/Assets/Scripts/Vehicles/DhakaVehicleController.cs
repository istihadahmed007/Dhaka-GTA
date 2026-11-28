using DhakaGta.Core;
using DhakaGta.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DhakaGta.Vehicles
{
    /// <summary>
    /// Full modular PhysX SphereCast raycast vehicle controller handling 2-wheel motorcycles (active lean & gyro),
    /// 3-wheel CNGs/Rickshaws, 4-wheel Sedans/SUVs/Pickups/Police cruisers, and Dhaka Buses.
    /// Implements acceleration, braking, reverse, steering, suspension, tire friction/slip, drifting, handbrake,
    /// wet-road traction, impact damage, lights, horn, and dynamic RPM audio.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DhakaVehicleController : MonoBehaviour, IVehicleDrivable
    {
        [Header("Vehicle Config & Components")]
        [SerializeField] private VehicleSpecSO vehicleSpec;
        [SerializeField] private DhakaPhysxWheel[] wheels = System.Array.Empty<DhakaPhysxWheel>();
        [SerializeField] private Transform steeringWheelMesh;
        [SerializeField] private DhakaVehicleTuningAndDamage damageAndTuning;
        [SerializeField] private DhakaVehicleLightsAndHorn lightsAndHorn;
        [SerializeField] private DhakaVehicleAudioController audioController;

        private Rigidbody _rb;
        private Vector3 _lastPosition;
        private float _smoothedSteerAngle;
        private float _currentThrottle;
        private float _currentSteer;
        private bool _currentHandbrake;
        private bool _hasSyntheticInput;
        private float _synThrottle, _synSteer;
        private bool _synHandbrake;

        public VehicleSpecSO Spec => vehicleSpec;
        public bool IsOccupied { get; private set; }
        public float ForwardSpeedKph => _rb != null ? Vector3.Dot(_rb.linearVelocity, transform.forward) * 3.6f : 0f;
        public float DistanceDrivenMeters { get; private set; }
        public int GroundedWheelCount { get; private set; }
        public bool IsAnyWheelGrounded => GroundedWheelCount > 0;
        public bool IsReversing { get; private set; }
        public bool IsBraking { get; private set; }
        public bool IsDrifting { get; private set; }
        public DhakaVehicleTuningAndDamage DamageAndTuning => damageAndTuning;
        public DhakaVehicleLightsAndHorn LightsAndHorn => lightsAndHorn;

        public void EnterDriverSeat() => IsOccupied = true;
        public void ExitDriverSeat() => IsOccupied = false;

        public void Configure(
            VehicleSpecSO spec,
            DhakaPhysxWheel[] physxWheels,
            Transform steeringWheel = null,
            DhakaVehicleTuningAndDamage dmgTuning = null,
            DhakaVehicleLightsAndHorn lights = null,
            DhakaVehicleAudioController audio = null)
        {
            vehicleSpec = spec;
            wheels = physxWheels ?? System.Array.Empty<DhakaPhysxWheel>();
            steeringWheelMesh = steeringWheel;
            damageAndTuning = dmgTuning ?? GetComponent<DhakaVehicleTuningAndDamage>();
            lightsAndHorn = lights ?? GetComponent<DhakaVehicleLightsAndHorn>();
            audioController = audio ?? GetComponent<DhakaVehicleAudioController>();

            ApplySpecToRigidbody();
            audioController?.Initialize(spec);
        }

        public void SetSyntheticDriveInput(float throttleBrake, float steer, bool handbrake = false)
        {
            _hasSyntheticInput = true;
            _synThrottle = Mathf.Clamp(throttleBrake, -1f, 1f);
            _synSteer = Mathf.Clamp(steer, -1f, 1f);
            _synHandbrake = handbrake;
        }

        public void ClearSyntheticDriveInput()
        {
            _hasSyntheticInput = false;
            _synThrottle = 0f;
            _synSteer = 0f;
            _synHandbrake = false;
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            gameObject.layer = PhysicsLayers.Vehicle;
            damageAndTuning ??= GetComponent<DhakaVehicleTuningAndDamage>();
            lightsAndHorn ??= GetComponent<DhakaVehicleLightsAndHorn>();
            audioController ??= GetComponent<DhakaVehicleAudioController>();
            ApplySpecToRigidbody();
            _lastPosition = transform.position;
        }

        private void ApplySpecToRigidbody()
        {
            if (_rb == null || vehicleSpec == null) return;
            _rb.mass = Mathf.Max(90f, vehicleSpec.massKg);
            _rb.centerOfMass = new Vector3(0f, vehicleSpec.centerOfMassYOffset, 0.08f);
            _rb.linearDamping = 0.05f;
            _rb.angularDamping = 0.25f;
        }

        private void Update()
        {
            if (IsOccupied && Keyboard.current != null)
            {
                if (Keyboard.current.lKey.wasPressedThisFrame) lightsAndHorn?.ToggleHeadlights();
                if (Keyboard.current.hKey.wasPressedThisFrame)
                {
                    lightsAndHorn?.SoundHorn();
                    audioController?.PlayHorn();
                }
                if (Keyboard.current.yKey.wasPressedThisFrame) lightsAndHorn?.TogglePoliceSiren();
            }

            lightsAndHorn?.UpdateLightStates(IsBraking, IsReversing);
            audioController?.UpdateEngineAndSkidAudio(ForwardSpeedKph, _currentThrottle, IsDrifting, damageAndTuning != null && damageAndTuning.IsWrecked);
        }

        private void FixedUpdate()
        {
            ReadControls(out _currentThrottle, out _currentSteer, out _currentHandbrake);

            bool wrecked = damageAndTuning != null && damageAndTuning.IsWrecked;
            if (wrecked)
            {
                _currentThrottle = 0f;
                _currentHandbrake = true;
            }

            float maxSteer = vehicleSpec != null ? vehicleSpec.maxSteerAngleDeg : 32f;
            float speedKph = ForwardSpeedKph;
            float steerSpeedFactor = Mathf.Lerp(1f, 0.42f, Mathf.Clamp01(Mathf.Abs(speedKph) / 100f));
            _smoothedSteerAngle = Mathf.MoveTowards(_smoothedSteerAngle, _currentSteer * maxSteer * steerSpeedFactor, 160f * Time.fixedDeltaTime);

            if (steeringWheelMesh != null)
            {
                steeringWheelMesh.localRotation = Quaternion.Euler(22f, 0f, -_smoothedSteerAngle * 3.4f);
            }

            ComputeDriveAndBrakeTorques(_currentThrottle, speedKph, out float motorTorque, out float brakeTorque);

            float springStiff = (vehicleSpec != null ? vehicleSpec.suspensionSpringStiffness : 32000f)
                              * (damageAndTuning != null ? damageAndTuning.Tuning.suspensionStiffnessMultiplier : 1f);
            float damper = vehicleSpec != null ? vehicleSpec.suspensionDamper : 4200f;
            float dryGrip = (vehicleSpec != null ? vehicleSpec.dryLateralGrip : 1.15f)
                          * (damageAndTuning != null ? damageAndTuning.Tuning.DryGripBonus : 1f);
            float wetMult = (vehicleSpec != null ? vehicleSpec.wetGripMultiplier : 0.58f)
                          * (damageAndTuning != null ? damageAndTuning.Tuning.WetGripBonus : 1f);
            float hbMult = vehicleSpec != null ? vehicleSpec.handbrakeRearGripMultiplier : 0.28f;

            int grounded = 0;
            bool anySkid = false;
            int wheelTotal = Mathf.Max(1, wheels.Length);
            float perWheelMotor = motorTorque / Mathf.Max(1, CountDrivenWheels());

            foreach (DhakaPhysxWheel wheel in wheels)
            {
                if (wheel == null) continue;
                wheel.StepPhysics(
                    _rb,
                    springStiff,
                    damper,
                    wheel.IsDriven ? perWheelMotor : 0f,
                    brakeTorque,
                    _smoothedSteerAngle,
                    dryGrip,
                    wetMult,
                    hbMult,
                    _currentHandbrake,
                    Time.fixedDeltaTime);

                if (wheel.IsGrounded) grounded++;
                if (wheel.IsSkidding) anySkid = true;
            }

            GroundedWheelCount = grounded;
            IsDrifting = anySkid && grounded > 0;

            ApplyChassisStabilization(speedKph);
            ApplyDownforce(speedKph);

            DistanceDrivenMeters += Vector3.Distance(transform.position, _lastPosition);
            _lastPosition = transform.position;
        }

        private void OnCollisionEnter(Collision collision)
        {
            float impactSpeed = collision.relativeVelocity.magnitude;
            if (impactSpeed < 4.5f) return;

            float dmg = (impactSpeed - 4.5f) * 14f;
            damageAndTuning?.ApplyDamage(dmg);
            audioController?.PlayCollisionCrash(impactSpeed);
        }

        private void ComputeDriveAndBrakeTorques(float throttle, float speedKph, out float motorTorque, out float brakeTorque)
        {
            float maxMotor = (vehicleSpec != null ? vehicleSpec.maxMotorTorqueNm : 1650f)
                           * (damageAndTuning != null ? damageAndTuning.Tuning.TorqueMultiplier : 1f);
            float maxBrake = vehicleSpec != null ? vehicleSpec.maxBrakeTorqueNm : 2800f;
            float maxFwdKph = (vehicleSpec != null ? vehicleSpec.maxSpeedKph : 120f)
                            * (damageAndTuning != null ? damageAndTuning.Tuning.TopSpeedMultiplier : 1f);
            float maxRevKph = vehicleSpec != null ? vehicleSpec.reverseMaxSpeedKph : 32f;

            motorTorque = 0f;
            brakeTorque = 0f;
            IsBraking = _currentHandbrake;
            IsReversing = false;

            if (_currentHandbrake)
            {
                brakeTorque = maxBrake * 1.15f;
                return;
            }

            if (throttle > 0.05f)
            {
                if (speedKph < -1.5f)
                {
                    brakeTorque = maxBrake;
                    IsBraking = true;
                }
                else if (speedKph < maxFwdKph)
                {
                    motorTorque = throttle * maxMotor;
                }
            }
            else if (throttle < -0.05f)
            {
                if (speedKph > 1.8f)
                {
                    brakeTorque = maxBrake * Mathf.Abs(throttle);
                    IsBraking = true;
                }
                else if (speedKph > -maxRevKph)
                {
                    motorTorque = throttle * (maxMotor * 0.72f);
                    IsReversing = true;
                }
            }
        }

        private void ApplyChassisStabilization(float speedKph)
        {
            if (_rb == null) return;

            float targetRollDeg = 0f;
            float maxLean = vehicleSpec != null ? vehicleSpec.maxLeanAngleDeg : 0f;

            // 2-Wheel Motorcycle dynamic cornering lean + upright gyro stabilizer
            if (maxLean > 1f)
            {
                float speedRatio = Mathf.Clamp01(Mathf.Abs(speedKph) / 55f);
                targetRollDeg = -_currentSteer * maxLean * speedRatio;
            }

            float currentRoll = Mathf.DeltaAngle(0f, transform.localEulerAngles.z);
            float rollErr = Mathf.DeltaAngle(currentRoll, targetRollDeg);
            float angularVelRoll = Vector3.Dot(_rb.angularVelocity, transform.forward);

            float rollStiffness = maxLean > 1f ? 95f : (vehicleSpec != null && vehicleSpec.wheelCount <= 3 ? 65f : 28f);
            float rollDamping = maxLean > 1f ? 18f : 12f;

            Vector3 stabilizeTorque = transform.forward * (rollErr * rollStiffness - angularVelRoll * rollDamping);
            _rb.AddTorque(stabilizeTorque, ForceMode.Acceleration);
        }

        private void ApplyDownforce(float speedKph)
        {
            if (_rb == null || !IsAnyWheelGrounded) return;
            float df = Mathf.Abs(speedKph) * 8.5f;
            _rb.AddForce(-transform.up * df, ForceMode.Force);
        }

        private int CountDrivenWheels()
        {
            int count = 0;
            foreach (DhakaPhysxWheel w in wheels)
            {
                if (w != null && w.IsDriven) count++;
            }
            return count;
        }

        private void ReadControls(out float throttle, out float steer, out bool handbrake)
        {
            if (_hasSyntheticInput)
            {
                throttle = _synThrottle;
                steer = _synSteer;
                handbrake = _synHandbrake;
                return;
            }

            throttle = 0f;
            steer = 0f;
            handbrake = false;

            if (!IsOccupied || Keyboard.current == null) return;

            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) throttle += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) throttle -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) steer += 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) steer -= 1f;
            handbrake = Keyboard.current.spaceKey.isPressed;
        }
    }
}
