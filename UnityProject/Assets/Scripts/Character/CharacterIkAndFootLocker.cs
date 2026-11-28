using DhakaGta.Core;
using UnityEngine;

namespace DhakaGta.Character
{
    /// <summary>
    /// Handles Humanoid OnAnimatorIK:
    /// 1) Contact-phase Foot Locking & terrain normal alignment (prevents foot sliding on slopes/curbs),
    /// 2) Dynamic Pelvis height compensation on stairs and uneven Dhaka sidewalks,
    /// 3) Upper-body Weapon Aiming IK + LookAt head tracking,
    /// 4) Procedural Right-Hand Interaction Reach IK (Tea Stalls, ATMs, Vehicle Doors).
    /// Compatible with Animation Rigging packages once installed without hard assembly dependency.
    /// </summary>
    public sealed class CharacterIkAndFootLocker : MonoBehaviour
    {
        [Header("Foot Grounding & Locking")]
        [SerializeField] private float footRayStartHeight = 0.45f;
        [SerializeField] private float footRayDistance = 0.95f;
        [SerializeField] private float footSoleOffsetMeters = 0.088f;
        [SerializeField] private float footLockLerpSpeed = 16f;
        [SerializeField] private float pelvisOffsetLerpSpeed = 10f;
        [SerializeField] private float maxPelvisDropMeters = 0.35f;

        [Header("Upper Body Aim & Look IK")]
        [SerializeField] private float aimIkBlendSpeed = 12f;
        [SerializeField] private Vector3 localWeaponGripOffset = new Vector3(0.22f, 1.38f, 0.42f);
        [SerializeField] private Vector3 localLeftSupportOffset = new Vector3(-0.08f, -0.02f, 0.18f);

        private Animator _animator;
        private MotionMatchingController _motionMatcher;

        private bool _leftLocked;
        private bool _rightLocked;
        private Vector3 _leftLockWorldPos;
        private Vector3 _rightLockWorldPos;
        private Quaternion _leftFootIkRot = Quaternion.identity;
        private Quaternion _rightFootIkRot = Quaternion.identity;
        private float _leftLockWeight;
        private float _rightLockWeight;
        private float _pelvisOffsetY;

        private CharacterAimTargetState _aimState;
        private float _currentAimWeight;

        private bool _reachActive;
        private Vector3 _reachWorldTarget;
        private float _reachTimer;
        private const float ReachDurationSeconds = 0.55f;

        public float LeftFootLockWeight => _leftLockWeight;
        public float RightFootLockWeight => _rightLockWeight;
        public float PelvisOffsetY => _pelvisOffsetY;
        public float CurrentAimWeight => _currentAimWeight;

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
            _motionMatcher = GetComponent<MotionMatchingController>();
        }

        public void Configure(Animator animator, MotionMatchingController matcher)
        {
            _animator = animator != null ? animator : GetComponentInChildren<Animator>();
            _motionMatcher = matcher != null ? matcher : GetComponent<MotionMatchingController>();
        }

        public void SetAimTargetState(CharacterAimTargetState state)
        {
            _aimState = state;
        }

        public void TriggerInteractionReach(Vector3 worldTarget)
        {
            _reachActive = true;
            _reachWorldTarget = worldTarget;
            _reachTimer = 0f;
        }

        /// <summary>
        /// Steps foot contact locking weights deterministically from gait phase + terrain probe.
        /// Invoked every frame (and directly callable in PlayMode tests without waiting for Animator callbacks).
        /// </summary>
        public void StepFootLockWeights(bool isGrounded, bool isSwimming, bool inVehicle, float deltaTime)
        {
            float dt = Mathf.Max(0.0001f, deltaTime);

            if (!isGrounded || isSwimming || inVehicle)
            {
                _leftLocked = false;
                _rightLocked = false;
                _leftLockWeight = Mathf.MoveTowards(_leftLockWeight, 0f, footLockLerpSpeed * dt);
                _rightLockWeight = Mathf.MoveTowards(_rightLockWeight, 0f, footLockLerpSpeed * dt);
                _pelvisOffsetY = Mathf.Lerp(_pelvisOffsetY, 0f, pelvisOffsetLerpSpeed * dt);
            }
            else
            {
                bool wantLeftLock = _motionMatcher == null || _motionMatcher.IsLeftFootPlanted;
                bool wantRightLock = _motionMatcher == null || _motionMatcher.IsRightFootPlanted;

                _leftLockWeight = Mathf.MoveTowards(_leftLockWeight, wantLeftLock ? 1f : 0f, footLockLerpSpeed * dt);
                _rightLockWeight = Mathf.MoveTowards(_rightLockWeight, wantRightLock ? 1f : 0f, footLockLerpSpeed * dt);

                if (!wantLeftLock)
                {
                    _leftLocked = false;
                }
                if (!wantRightLock)
                {
                    _rightLocked = false;
                }
            }

            float targetAim = _aimState.IsAiming ? Mathf.Clamp01(_aimState.AimBlendWeight) : 0f;
            _currentAimWeight = Mathf.MoveTowards(_currentAimWeight, targetAim, aimIkBlendSpeed * dt);

            if (_reachActive)
            {
                _reachTimer += dt;
                if (_reachTimer >= ReachDurationSeconds)
                {
                    _reachActive = false;
                }
            }
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (_animator == null || !_animator.isHuman)
            {
                return;
            }

            if (layerIndex == 0)
            {
                ApplyFootAndPelvisIk();
                ApplyLookAtIk();
            }

            ApplyUpperBodyAimAndReachIk();
        }

        private void ApplyFootAndPelvisIk()
        {
            float leftDrop = ProbeAndApplySingleFoot(
                AvatarIKGoal.LeftFoot,
                _leftLockWeight,
                ref _leftLocked,
                ref _leftLockWorldPos,
                ref _leftFootIkRot);

            float rightDrop = ProbeAndApplySingleFoot(
                AvatarIKGoal.RightFoot,
                _rightLockWeight,
                ref _rightLocked,
                ref _rightLockWorldPos,
                ref _rightFootIkRot);

            float lowestDelta = Mathf.Min(leftDrop, rightDrop);
            float targetPelvisOffset = Mathf.Clamp(lowestDelta, -maxPelvisDropMeters, 0.08f);
            _pelvisOffsetY = Mathf.Lerp(_pelvisOffsetY, targetPelvisOffset, pelvisOffsetLerpSpeed * Time.deltaTime);

            Vector3 bodyPos = _animator.bodyPosition;
            bodyPos.y += _pelvisOffsetY;
            _animator.bodyPosition = bodyPos;
        }

        private float ProbeAndApplySingleFoot(
            AvatarIKGoal goal,
            float lockWeight,
            ref bool isLocked,
            ref Vector3 lockedWorldPos,
            ref Quaternion lockedRot)
        {
            Vector3 ikGoalPos = _animator.GetIKPosition(goal);
            Quaternion ikGoalRot = _animator.GetIKRotation(goal);

            int groundMask = PhysicsLayers.GroundMask | PhysicsLayers.WorldStaticMask;
            Vector3 rayOrigin = ikGoalPos + Vector3.up * footRayStartHeight;

            if (Physics.SphereCast(rayOrigin, 0.055f, Vector3.down, out RaycastHit hit, footRayDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 plantedPos = hit.point + Vector3.up * footSoleOffsetMeters;
                Quaternion align = Quaternion.FromToRotation(Vector3.up, hit.normal) * ikGoalRot;

                if (lockWeight > 0.72f)
                {
                    if (!isLocked)
                    {
                        isLocked = true;
                        lockedWorldPos = plantedPos;
                        lockedRot = align;
                    }
                    else if ((lockedWorldPos - plantedPos).sqrMagnitude > 0.30f * 0.30f)
                    {
                        // Guard against excessive stretch during fast dash or teleport
                        lockedWorldPos = plantedPos;
                    }

                    Vector3 finalPos = Vector3.Lerp(plantedPos, new Vector3(lockedWorldPos.x, plantedPos.y, lockedWorldPos.z), lockWeight);
                    _animator.SetIKPositionWeight(goal, lockWeight);
                    _animator.SetIKRotationWeight(goal, lockWeight);
                    _animator.SetIKPosition(goal, finalPos);
                    _animator.SetIKRotation(goal, align);
                }
                else
                {
                    isLocked = false;
                    _animator.SetIKPositionWeight(goal, lockWeight * 0.5f);
                    _animator.SetIKRotationWeight(goal, lockWeight * 0.5f);
                    _animator.SetIKPosition(goal, plantedPos);
                    _animator.SetIKRotation(goal, align);
                }

                return plantedPos.y - transform.position.y - footSoleOffsetMeters;
            }

            isLocked = false;
            _animator.SetIKPositionWeight(goal, 0f);
            _animator.SetIKRotationWeight(goal, 0f);
            return 0f;
        }

        private void ApplyLookAtIk()
        {
            if (_currentAimWeight > 0.02f)
            {
                _animator.SetLookAtWeight(_currentAimWeight, 0.25f * _currentAimWeight, 0.85f, 0.95f, 0.45f);
                _animator.SetLookAtPosition(_aimState.WorldAimPoint);
            }
            else if (_reachActive)
            {
                float bell = Mathf.Sin((_reachTimer / ReachDurationSeconds) * Mathf.PI);
                _animator.SetLookAtWeight(bell * 0.75f, 0.1f, 0.75f, 0.9f, 0.5f);
                _animator.SetLookAtPosition(_reachWorldTarget);
            }
            else
            {
                _animator.SetLookAtWeight(0f);
            }
        }

        private void ApplyUpperBodyAimAndReachIk()
        {
            if (_reachActive)
            {
                float reachWeight = Mathf.Sin((_reachTimer / ReachDurationSeconds) * Mathf.PI);
                _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, reachWeight);
                _animator.SetIKRotationWeight(AvatarIKGoal.RightHand, reachWeight * 0.6f);
                _animator.SetIKPosition(AvatarIKGoal.RightHand, _reachWorldTarget);
                return;
            }

            if (_currentAimWeight <= 0.01f)
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
                _animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
                _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
                _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
                return;
            }

            Vector3 origin = transform.TransformPoint(localWeaponGripOffset);
            Vector3 aimDir = (_aimState.WorldAimPoint - origin).sqrMagnitude > 0.01f
                ? (_aimState.WorldAimPoint - origin).normalized
                : transform.forward;

            Quaternion weaponRot = Quaternion.LookRotation(aimDir, Vector3.up);
            _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, _currentAimWeight);
            _animator.SetIKRotationWeight(AvatarIKGoal.RightHand, _currentAimWeight);
            _animator.SetIKPosition(AvatarIKGoal.RightHand, origin + aimDir * 0.08f);
            _animator.SetIKRotation(AvatarIKGoal.RightHand, weaponRot);

            if (_aimState.HasTwoHandedWeapon)
            {
                Vector3 leftGrip = origin + weaponRot * localLeftSupportOffset;
                _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, _currentAimWeight * 0.92f);
                _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, _currentAimWeight * 0.92f);
                _animator.SetIKPosition(AvatarIKGoal.LeftHand, leftGrip);
                _animator.SetIKRotation(AvatarIKGoal.LeftHand, weaponRot);
            }
            else
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
                _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
            }
        }
    }
}
