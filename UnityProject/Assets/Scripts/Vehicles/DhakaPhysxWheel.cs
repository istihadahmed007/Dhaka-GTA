using DhakaGta.Core;
using UnityEngine;

namespace DhakaGta.Vehicles
{
    /// <summary>
    /// Custom PhysX SphereCast raycast wheel suspension + Pacejka-style tire friction/slip solver.
    /// Reliable across 2-wheel motorcycles, 3-wheel CNG/rickshaws, 4-wheel sedans/SUVs/pickups, and buses.
    /// Evaluates suspension compression, damping, longitudinal drive/brake/reverse forces, lateral grip,
    /// wet-road traction loss, handbrake lockup, drifting slip, and wheel mesh visual spin/steer rotation.
    /// </summary>
    public sealed class DhakaPhysxWheel : MonoBehaviour
    {
        [Header("Geometry & Setup")]
        [SerializeField] private bool isSteerable;
        [SerializeField] private bool isDriven = true;
        [SerializeField] private bool isRearWheel = true;
        [SerializeField] private float wheelRadiusMeters = 0.36f;
        [SerializeField] private float restLengthMeters = 0.24f;
        [SerializeField] private Transform visualWheelTransform;

        private float _previousCompressionDist;
        private float _currentSteerAngleDeg;
        private float _wheelSpinAngleDeg;

        public bool IsSteerable => isSteerable;
        public bool IsDriven => isDriven;
        public bool IsRearWheel => isRearWheel;
        public float WheelRadius => wheelRadiusMeters;

        public bool IsGrounded { get; private set; }
        public float CompressionRatio { get; private set; }
        public float ForwardSpeedMetersPerSec { get; private set; }
        public float LateralSlipRatio { get; private set; }
        public float SpinAngleDegrees => _wheelSpinAngleDeg;
        public bool IsSkidding { get; private set; }
        public RaycastHit LastGroundHit { get; private set; }

        public void Configure(
            bool steerable, bool driven, bool rear,
            float radiusMeters, float suspensionTravelMeters, Transform visualMesh)
        {
            isSteerable = steerable;
            isDriven = driven;
            isRearWheel = rear;
            wheelRadiusMeters = Mathf.Max(0.14f, radiusMeters);
            restLengthMeters = Mathf.Max(0.08f, suspensionTravelMeters);
            visualWheelTransform = visualMesh;
            _previousCompressionDist = restLengthMeters;
        }

        public void StepPhysics(
            Rigidbody rb,
            float springStiffness,
            float damperStrength,
            float motorTorqueNm,
            float brakeTorqueNm,
            float steerAngleDeg,
            float dryLateralGrip,
            float wetRoadMultiplier,
            float handbrakeGripMultiplier,
            bool handbrakeActive,
            float dt)
        {
            if (rb == null || dt <= 0.00001f) return;

            _currentSteerAngleDeg = isSteerable ? steerAngleDeg : 0f;
            float castRadius = Mathf.Clamp(wheelRadiusMeters * 0.42f, 0.05f, 0.28f);
            float maxRayDist = restLengthMeters + wheelRadiusMeters - castRadius + 0.05f;

            Vector3 origin = transform.position;
            Vector3 downDir = -transform.up;

            bool hitGround = Physics.SphereCast(
                origin,
                castRadius,
                downDir,
                out RaycastHit hit,
                maxRayDist,
                PhysicsLayers.DrivableSurfaceMask,
                QueryTriggerInteraction.Ignore);

            if (!hitGround)
            {
                IsGrounded = false;
                CompressionRatio = 0f;
                IsSkidding = false;
                LateralSlipRatio = 0f;
                _previousCompressionDist = restLengthMeters;
                ApplyVisualPose(restLengthMeters, rb.linearVelocity, dt);
                return;
            }

            IsGrounded = true;
            LastGroundHit = hit;

            float groundDistance = hit.distance + castRadius;
            float currentSpringLength = Mathf.Clamp(groundDistance - wheelRadiusMeters, 0.01f, restLengthMeters);
            float springCompression = restLengthMeters - currentSpringLength;
            CompressionRatio = Mathf.Clamp01(springCompression / Mathf.Max(0.05f, restLengthMeters));

            float compressionVel = (_previousCompressionDist - currentSpringLength) / dt;
            _previousCompressionDist = currentSpringLength;

            float suspensionForceMag = Mathf.Max(0f, springStiffness * springCompression + damperStrength * compressionVel);
            Vector3 suspensionForce = transform.up * suspensionForceMag;
            rb.AddForceAtPosition(suspensionForce, origin, ForceMode.Force);

            // Calculate wheel plane directions incorporating steer yaw
            Quaternion steerRot = Quaternion.AngleAxis(_currentSteerAngleDeg, transform.up);
            Vector3 wheelFwd = Vector3.ProjectOnPlane(steerRot * transform.forward, hit.normal).normalized;
            if (wheelFwd.sqrMagnitude < 1e-4f) wheelFwd = steerRot * transform.forward;
            Vector3 wheelRight = Vector3.Cross(hit.normal, wheelFwd).normalized;
            if (wheelRight.sqrMagnitude < 1e-4f) wheelRight = Vector3.Cross(Vector3.up, wheelFwd).normalized;

            Vector3 contactVel = rb.GetPointVelocity(hit.point);
            ForwardSpeedMetersPerSec = Vector3.Dot(contactVel, wheelFwd);
            float latVel = Vector3.Dot(contactVel, wheelRight);

            // Dynamic tire friction scaled cleanly by wet-road traction and handbrake drift
            float wetness = Mathf.Clamp01(WorldSurfaceState.GlobalRoadWetness);
            float surfaceGrip = Mathf.Lerp(1.0f, Mathf.Clamp(wetRoadMultiplier, 0.25f, 1.0f), wetness);
            float rearDriftFactor = (handbrakeActive && isRearWheel) ? handbrakeGripMultiplier : 1.0f;
            float totalGrip = Mathf.Max(0.15f, dryLateralGrip * surfaceGrip * rearDriftFactor);

            // Lateral Tire Force (Pacejka-inspired saturation allowing progressive drifts!)
            float slipRatio = Mathf.Clamp(latVel / Mathf.Max(1.8f, Mathf.Abs(ForwardSpeedMetersPerSec) + 2.0f), -1.2f, 1.2f);
            LateralSlipRatio = Mathf.Abs(slipRatio);

            float maxLateralForce = suspensionForceMag * totalGrip;
            float desiredLateralForce = -latVel * rb.mass * 0.48f * totalGrip;
            float clampedLateralForce = Mathf.Clamp(desiredLateralForce, -maxLateralForce, maxLateralForce);
            rb.AddForceAtPosition(wheelRight * clampedLateralForce, hit.point, ForceMode.Force);

            // Longitudinal Drive / Brake / Handbrake / Rolling Resistance Force
            float driveForce = 0f;
            if (isDriven && !(handbrakeActive && isRearWheel))
            {
                driveForce += motorTorqueNm / wheelRadiusMeters;
            }

            float maxTraction = suspensionForceMag * totalGrip * 1.15f;
            float speedSign = Mathf.Abs(ForwardSpeedMetersPerSec) > 0.08f ? Mathf.Sign(ForwardSpeedMetersPerSec) : 0f;

            if (brakeTorqueNm > 1f && speedSign != 0f)
            {
                float brakeForce = Mathf.Min(brakeTorqueNm / wheelRadiusMeters, Mathf.Abs(ForwardSpeedMetersPerSec) * rb.mass * 0.5f / dt);
                driveForce -= speedSign * brakeForce;
            }
            else if (Mathf.Abs(motorTorqueNm) < 1f && Mathf.Abs(ForwardSpeedMetersPerSec) > 0.05f)
            {
                // Gentle rolling resistance when coasting
                driveForce -= ForwardSpeedMetersPerSec * 12f;
            }

            driveForce = Mathf.Clamp(driveForce, -maxTraction, maxTraction);
            rb.AddForceAtPosition(wheelFwd * driveForce, hit.point, ForceMode.Force);

            IsSkidding = (LateralSlipRatio > 0.34f && Mathf.Abs(ForwardSpeedMetersPerSec) > 4f) ||
                         (handbrakeActive && isRearWheel && Mathf.Abs(ForwardSpeedMetersPerSec) > 3.5f);

            ApplyVisualPose(currentSpringLength, wheelFwd * ForwardSpeedMetersPerSec, dt);
        }

        private void ApplyVisualPose(float springLength, Vector3 worldVelAlongWheel, float dt)
        {
            float speed = Vector3.Dot(worldVelAlongWheel, Quaternion.AngleAxis(_currentSteerAngleDeg, transform.up) * transform.forward);
            float radPerSec = speed / Mathf.Max(0.14f, wheelRadiusMeters);
            _wheelSpinAngleDeg = (_wheelSpinAngleDeg + radPerSec * Mathf.Rad2Deg * dt) % 360f;

            if (visualWheelTransform != null)
            {
                visualWheelTransform.localPosition = new Vector3(0f, -springLength, 0f);
                visualWheelTransform.localRotation = Quaternion.Euler(_wheelSpinAngleDeg, _currentSteerAngleDeg, 0f);
            }
        }
    }
}
