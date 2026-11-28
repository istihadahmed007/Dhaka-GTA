using DhakaGta.Core;
using DhakaGta.Environment;
using DhakaGta.Vehicles;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DhakaGta.Graphics
{
    public enum DhakaCameraMode
    {
        DrivingChase = 0,
        DrivingClose = 1,
        DrivingWideCinematic = 2,
        FirstPersonCockpit = 3,
        OnFootThirdPerson = 4,
    }

    /// <summary>
    /// 5-Mode HDRP Cinemachine 3 camera controller for Dhaka driving and on-foot exploration:
    /// DrivingChase, DrivingClose, DrivingWideCinematic, FirstPersonCockpit (RHD), and OnFootThirdPerson.
    /// Implements ground/building SphereCast collision avoidance, smooth damping, lateral corner banking roll,
    /// and speed-sensitive dynamic FOV.
    /// </summary>
    public sealed class DhakaCinemachineCameraRig : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private CinemachineCamera cinemachineCamera;
        [SerializeField] private Camera unityMainCamera;
        [SerializeField] private MonoBehaviour targetVehicleBehaviour;
        [SerializeField] private Transform onFootPlayerTransform;
        [SerializeField] private ProtagonistInteractionScanner interactionScanner;

        private IVehicleDrivable _targetDrivable;

        [Header("Camera Behaviour")]
        [SerializeField] private DhakaCameraMode currentMode = DhakaCameraMode.DrivingChase;
        [SerializeField] private float lookSensitivity = 0.18f;
        [SerializeField] private float autoCenterSpeedDegPerSec = 55f;
        [SerializeField] private float sphereCastRadius = 0.24f;
        [SerializeField] private float maxBankDegrees = 4.5f;

        private Transform _internalRigTarget;
        private DhakaCameraMode _lastDrivingMode = DhakaCameraMode.DrivingChase;
        private Vector3 _smoothedFollowPos;
        private float _yawOffsetDeg;
        private float _pitchDeg = 12f;
        private float _currentRollDeg;
        private float _currentFov = 58f;
        private float _lastVehicleYawDeg;
        private float _currentOcclusionFraction = 1f;
        private bool _manualOrbitRecentlyUsed;
        private float _manualOrbitCooldownTimer;

        public DhakaCameraMode CurrentMode => currentMode;
        public float CurrentFieldOfView => _currentFov;
        public float CurrentBankRollDegrees => _currentRollDeg;
        public float LastOcclusionFraction => _currentOcclusionFraction;
        public Vector3 ComputedCameraWorldPosition { get; private set; }

        public void Configure(
            CinemachineCamera vcam,
            Camera mainCam,
            MonoBehaviour vehicleBehaviour,
            Transform playerTransform,
            ProtagonistInteractionScanner scanner,
            DhakaCameraMode initialMode = DhakaCameraMode.OnFootThirdPerson)
        {
            cinemachineCamera = vcam;
            unityMainCamera = mainCam;
            targetVehicleBehaviour = vehicleBehaviour;
            _targetDrivable = vehicleBehaviour as IVehicleDrivable;
            onFootPlayerTransform = playerTransform;
            interactionScanner = scanner;
            SetCameraMode(initialMode);
            EnsureRigTarget();
        }

        public void SetCameraMode(DhakaCameraMode mode)
        {
            currentMode = mode;
            if (mode != DhakaCameraMode.OnFootThirdPerson)
            {
                _lastDrivingMode = mode;
            }

            if (mode == DhakaCameraMode.FirstPersonCockpit)
            {
                _yawOffsetDeg = 0f;
                _pitchDeg = 2f;
            }
            else if (mode == DhakaCameraMode.OnFootThirdPerson)
            {
                _pitchDeg = 14f;
            }
        }

        public DhakaCameraMode CycleDrivingOrFootMode()
        {
            bool isDriving = interactionScanner != null ? interactionScanner.IsDrivingVehicle : (currentMode != DhakaCameraMode.OnFootThirdPerson);
            if (!isDriving)
            {
                SetCameraMode(DhakaCameraMode.OnFootThirdPerson);
                return currentMode;
            }

            int next = ((int)currentMode + 1) % 4; // Cycles 0..3 across the 4 driving camera modes
            SetCameraMode((DhakaCameraMode)next);
            return currentMode;
        }

        private void Awake()
        {
            EnsureRigTarget();
            _targetDrivable = targetVehicleBehaviour as IVehicleDrivable;
            if (targetVehicleBehaviour != null)
            {
                _lastVehicleYawDeg = targetVehicleBehaviour.transform.eulerAngles.y;
            }
        }

        private void LateUpdate()
        {
            bool isDriving = interactionScanner != null && interactionScanner.IsDrivingVehicle;
            if (isDriving && interactionScanner.ActiveVehicleBehaviour != null &&
                interactionScanner.ActiveVehicleBehaviour != targetVehicleBehaviour)
            {
                targetVehicleBehaviour = interactionScanner.ActiveVehicleBehaviour;
                _targetDrivable = interactionScanner.ActiveVehicle;
                _lastVehicleYawDeg = targetVehicleBehaviour.transform.eulerAngles.y;
            }

            if (isDriving && currentMode == DhakaCameraMode.OnFootThirdPerson)
            {
                SetCameraMode(_lastDrivingMode);
            }
            else if (!isDriving && onFootPlayerTransform != null && currentMode != DhakaCameraMode.OnFootThirdPerson)
            {
                SetCameraMode(DhakaCameraMode.OnFootThirdPerson);
            }

            if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
            {
                CycleDrivingOrFootMode();
            }

            UpdateCameraTransform(Time.deltaTime);
        }

        public void UpdateCameraTransform(float dt)
        {
            EnsureRigTarget();
            Transform subject = GetActiveSubjectTransform();
            if (subject == null) return;

            ReadLookInput(dt);

            bool lookBack = Keyboard.current != null && Keyboard.current.bKey.isPressed;
            IVehicleDrivable activeDrivable = _targetDrivable ?? targetVehicleBehaviour as IVehicleDrivable;
            float speedKph = activeDrivable != null && currentMode != DhakaCameraMode.OnFootThirdPerson
                ? Mathf.Abs(activeDrivable.ForwardSpeedKph)
                : 0f;
            float speedNormalized = Mathf.Clamp01(speedKph / 120f);

            // Auto-center orbit smoothly behind moving vehicle
            if (!_manualOrbitRecentlyUsed && speedKph > 10f && currentMode != DhakaCameraMode.FirstPersonCockpit)
            {
                _yawOffsetDeg = Mathf.MoveTowardsAngle(_yawOffsetDeg, 0f, autoCenterSpeedDegPerSec * dt);
            }

            GetPresetMetrics(currentMode, out float baseDist, out float heightOffset, out float shoulderOffset, out float baseFov, out float maxFovBoost, out float posLerpRate);

            Vector3 focusTarget = subject.position + Vector3.up * heightOffset + subject.right * shoulderOffset;
            if (_smoothedFollowPos == Vector3.zero) _smoothedFollowPos = focusTarget;
            _smoothedFollowPos = Vector3.Lerp(_smoothedFollowPos, focusTarget, 1f - Mathf.Exp(-posLerpRate * Mathf.Max(0.001f, dt)));

            // Speed-sensitive FOV
            float targetFov = baseFov + speedNormalized * maxFovBoost;
            _currentFov = Mathf.Lerp(_currentFov, targetFov, 1f - Mathf.Exp(-6f * Mathf.Max(0.001f, dt)));

            // Lateral cornering banking roll when driving
            float targetRoll = 0f;
            if (targetVehicleBehaviour != null && currentMode != DhakaCameraMode.OnFootThirdPerson && dt > 0.0001f)
            {
                float currentYaw = targetVehicleBehaviour.transform.eulerAngles.y;
                float yawRateDeg = Mathf.DeltaAngle(_lastVehicleYawDeg, currentYaw) / dt;
                _lastVehicleYawDeg = currentYaw;
                float bankFactor = currentMode == DhakaCameraMode.FirstPersonCockpit ? 0.35f : 1.0f;
                targetRoll = Mathf.Clamp(-yawRateDeg * 0.06f * speedNormalized, -maxBankDegrees, maxBankDegrees) * bankFactor;
            }
            _currentRollDeg = Mathf.Lerp(_currentRollDeg, targetRoll, 1f - Mathf.Exp(-8f * Mathf.Max(0.001f, dt)));

            Vector3 desiredCamPos;
            Quaternion desiredCamRot;

            if (currentMode == DhakaCameraMode.FirstPersonCockpit)
            {
                // Right-Hand Drive cockpit eye position (uses vehicle seat anchor if configured)
                var seats = subject.GetComponent<DhakaVehicleSeatsAndInteract>();
                desiredCamPos = seats != null && seats.FirstPersonCockpitEye != null
                    ? seats.FirstPersonCockpitEye.position
                    : subject.TransformPoint(new Vector3(0.36f, 1.16f, 0.22f));
                float cockpitYaw = lookBack ? 180f : _yawOffsetDeg;
                desiredCamRot = subject.rotation * Quaternion.Euler(_pitchDeg * 0.5f, cockpitYaw, _currentRollDeg);
                _currentOcclusionFraction = 1f;
            }
            else
            {
                float subjectYaw = subject.eulerAngles.y + (lookBack ? 180f : _yawOffsetDeg);
                Quaternion orbitRot = Quaternion.Euler(_pitchDeg, subjectYaw, 0f);
                Vector3 desiredOffset = orbitRot * new Vector3(0f, 0f, -baseDist);
                desiredCamPos = ResolveCollisionAvoidance(_smoothedFollowPos, desiredOffset, out _currentOcclusionFraction);

                Vector3 lookDir = (_smoothedFollowPos + subject.forward * (speedNormalized * 3.5f) - desiredCamPos);
                if (lookDir.sqrMagnitude < 1e-4f) lookDir = subject.forward;
                Quaternion lookRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                desiredCamRot = lookRot * Quaternion.Euler(0f, 0f, _currentRollDeg);
            }

            ComputedCameraWorldPosition = desiredCamPos;
            _internalRigTarget.SetPositionAndRotation(desiredCamPos, desiredCamRot);

            if (cinemachineCamera != null)
            {
                cinemachineCamera.Lens.FieldOfView = _currentFov;
                cinemachineCamera.Target = new CameraTarget
                {
                    TrackingTarget = _internalRigTarget,
                    LookAtTarget = null,
                };
            }
            if (unityMainCamera != null)
            {
                unityMainCamera.fieldOfView = _currentFov;
                if (cinemachineCamera == null)
                {
                    unityMainCamera.transform.SetPositionAndRotation(desiredCamPos, desiredCamRot);
                }
            }
        }

        private Vector3 ResolveCollisionAvoidance(Vector3 pivot, Vector3 offset, out float fraction)
        {
            float distance = offset.magnitude;
            if (distance < 0.05f)
            {
                fraction = 1f;
                return pivot + offset;
            }

            Vector3 dir = offset / distance;
            if (Physics.SphereCast(pivot, sphereCastRadius, dir, out RaycastHit hit, distance, PhysicsLayers.CameraOcclusionMask, QueryTriggerInteraction.Ignore))
            {
                float clampedDist = Mathf.Max(0.6f, hit.distance - 0.15f);
                fraction = clampedDist / distance;
                return pivot + dir * clampedDist;
            }

            fraction = 1f;
            return pivot + offset;
        }

        private void ReadLookInput(float dt)
        {
            if (Mouse.current != null && Mouse.current.rightButton.isPressed)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                if (delta.sqrMagnitude > 0.01f)
                {
                    _yawOffsetDeg += delta.x * lookSensitivity;
                    _pitchDeg = Mathf.Clamp(_pitchDeg - delta.y * lookSensitivity, -10f, 52f);
                    _manualOrbitRecentlyUsed = true;
                    _manualOrbitCooldownTimer = 2.2f;
                    return;
                }
            }

            if (_manualOrbitRecentlyUsed)
            {
                _manualOrbitCooldownTimer -= dt;
                if (_manualOrbitCooldownTimer <= 0f)
                {
                    _manualOrbitRecentlyUsed = false;
                }
            }
        }

        private Transform GetActiveSubjectTransform()
        {
            if (currentMode == DhakaCameraMode.OnFootThirdPerson && onFootPlayerTransform != null && onFootPlayerTransform.gameObject.activeInHierarchy)
            {
                return onFootPlayerTransform;
            }
            return targetVehicleBehaviour != null ? targetVehicleBehaviour.transform : onFootPlayerTransform;
        }

        private void EnsureRigTarget()
        {
            if (_internalRigTarget == null)
            {
                var go = new GameObject("DhakaCameraComputedPose");
                go.transform.SetParent(transform, false);
                _internalRigTarget = go.transform;
            }
        }

        private static void GetPresetMetrics(
            DhakaCameraMode mode,
            out float dist, out float height, out float shoulder, out float baseFov, out float maxFovBoost, out float lerpRate)
        {
            switch (mode)
            {
                case DhakaCameraMode.DrivingClose:
                    dist = 4.5f; height = 1.48f; shoulder = 0f; baseFov = 60f; maxFovBoost = 14f; lerpRate = 18f;
                    break;
                case DhakaCameraMode.DrivingWideCinematic:
                    dist = 9.2f; height = 2.85f; shoulder = 0f; baseFov = 54f; maxFovBoost = 12f; lerpRate = 9f;
                    break;
                case DhakaCameraMode.FirstPersonCockpit:
                    dist = 0f; height = 1.16f; shoulder = 0.36f; baseFov = 66f; maxFovBoost = 9f; lerpRate = 30f;
                    break;
                case DhakaCameraMode.OnFootThirdPerson:
                    dist = 3.8f; height = 1.62f; shoulder = 0.48f; baseFov = 56f; maxFovBoost = 4f; lerpRate = 14f;
                    break;
                default: // DrivingChase
                    dist = 6.4f; height = 1.92f; shoulder = 0f; baseFov = 58f; maxFovBoost = 13f; lerpRate = 13f;
                    break;
            }
        }
    }
}
