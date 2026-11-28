using DhakaGta.Data;
using UnityEngine;

namespace DhakaGta.Character
{
    /// <summary>
    /// AI4Animation-inspired Local Motion Phase + Codebook Matching controller.
    /// Locks gait phase progression to real-world displacement per stride length so
    /// footfalls remain synchronized with physics velocity across Walk, Jog, Sprint, and Swim.
    /// </summary>
    public sealed class MotionMatchingController : MonoBehaviour
    {
        public static readonly int ParamMoveSpeed = Animator.StringToHash("MoveSpeed");
        public static readonly int ParamMoveX = Animator.StringToHash("MoveX");
        public static readonly int ParamMoveZ = Animator.StringToHash("MoveZ");
        public static readonly int ParamTurnDelta = Animator.StringToHash("TurnDelta");
        public static readonly int ParamGaitPhase = Animator.StringToHash("GaitPhase");
        public static readonly int ParamGaitSin = Animator.StringToHash("GaitPhaseSin");
        public static readonly int ParamGaitCos = Animator.StringToHash("GaitPhaseCos");
        public static readonly int ParamIsGrounded = Animator.StringToHash("IsGrounded");
        public static readonly int ParamIsSprinting = Animator.StringToHash("IsSprinting");
        public static readonly int ParamIsCrouching = Animator.StringToHash("IsCrouching");
        public static readonly int ParamIsSwimming = Animator.StringToHash("IsSwimming");
        public static readonly int ParamIsAiming = Animator.StringToHash("IsAiming");
        public static readonly int ParamInVehicle = Animator.StringToHash("InVehicle");
        public static readonly int ParamVerticalVelocity = Animator.StringToHash("VerticalVelocity");
        public static readonly int ParamLeftFootContact = Animator.StringToHash("LeftFootContact");
        public static readonly int ParamRightFootContact = Animator.StringToHash("RightFootContact");
        public static readonly int TrigJump = Animator.StringToHash("TriggerJump");
        public static readonly int TrigLand = Animator.StringToHash("TriggerLand");
        public static readonly int TrigInteract = Animator.StringToHash("TriggerInteract");
        public static readonly int TrigGesture = Animator.StringToHash("TriggerGesture");

        [SerializeField] private MotionCodebookSO codebook;
        [SerializeField] private Animator targetAnimator;
        [SerializeField] private float parameterDampTime = 0.08f;

        private MotionCodebookEntry _activeEntry;
        private float _gaitPhaseNormalized; // [0, 1)
        private bool _wasGrounded = true;
        private float _animatorSpeedMultiplier = 1f;

        public MotionCodebookEntry ActiveEntry => _activeEntry;
        public MotionActionTag CurrentActionTag => _activeEntry.actionTag;
        public float GaitPhaseNormalized => _gaitPhaseNormalized;
        public float GaitPhaseRadians => _gaitPhaseNormalized * Mathf.PI * 2f;
        public float AnimatorSpeedSyncRatio => _animatorSpeedMultiplier;

        /// <summary>
        /// True when the matched AI4Animation contact window dictates Left Foot is planted on ground.
        /// </summary>
        public bool IsLeftFootPlanted =>
            _activeEntry.allowsFootLocking && IsInNormalizedWindow(_gaitPhaseNormalized, _activeEntry.leftStanceStart, _activeEntry.leftStanceEnd);

        /// <summary>
        /// True when the matched AI4Animation contact window dictates Right Foot is planted on ground.
        /// </summary>
        public bool IsRightFootPlanted =>
            _activeEntry.allowsFootLocking && IsInNormalizedWindow(_gaitPhaseNormalized, _activeEntry.rightStanceStart, _activeEntry.rightStanceEnd);

        private void Awake()
        {
            if (targetAnimator == null)
            {
                targetAnimator = GetComponentInChildren<Animator>();
            }

            EnsureCodebook();
            _activeEntry = codebook.FindByTag(MotionActionTag.Idle);
        }

        public void Configure(Animator animator, MotionCodebookSO customCodebook = null)
        {
            targetAnimator = animator != null ? animator : GetComponentInChildren<Animator>();
            if (customCodebook != null)
            {
                codebook = customCodebook;
            }
            EnsureCodebook();
            _activeEntry = codebook.FindByTag(MotionActionTag.Idle);
        }

        private void EnsureCodebook()
        {
            if (codebook == null)
            {
                codebook = ScriptableObject.CreateInstance<MotionCodebookSO>();
                codebook.PopulateDefaultAi4AnimationEntries();
            }
        }

        /// <summary>
        /// Updates codebook selection, stride-locked gait phase clock, and Humanoid Animator parameters.
        /// </summary>
        public void StepMotionMatching(
            Vector3 localVelocity,
            float predictedTurnDegrees04,
            bool isGrounded,
            bool isCrouching,
            bool isSprinting,
            bool isSwimming,
            bool isAiming,
            bool inVehicle,
            float deltaTime)
        {
            EnsureCodebook();
            float dt = Mathf.Max(0.0001f, deltaTime);

            MotionCodebookEntry next = codebook.FindBestMatch(
                localVelocity,
                predictedTurnDegrees04,
                isGrounded,
                isCrouching,
                isSprinting,
                isSwimming,
                isAiming,
                inVehicle,
                _activeEntry.actionTag,
                out _);

            _activeEntry = next;

            float horizSpeed = new Vector2(localVelocity.x, localVelocity.z).magnitude;
            float stride = Mathf.Max(0.45f, _activeEntry.strideLengthMeters);
            float cycleRateHz = horizSpeed > 0.15f
                ? horizSpeed / stride
                : _activeEntry.basePhaseFrequencyHz;

            _gaitPhaseNormalized = Mathf.Repeat(_gaitPhaseNormalized + cycleRateHz * dt, 1f);

            // Sync Animator playback multiplier to prevent stride slippage when speed diverges slightly
            _animatorSpeedMultiplier = _activeEntry.targetSpeedMetersPerSec > 0.25f && horizSpeed > 0.25f
                ? Mathf.Clamp(horizSpeed / _activeEntry.targetSpeedMetersPerSec, 0.72f, 1.38f)
                : 1f;

            if (targetAnimator == null)
            {
                targetAnimator = GetComponentInChildren<Animator>();
            }

            if (targetAnimator != null && targetAnimator.runtimeAnimatorController != null)
            {
                ApplyToAnimator(localVelocity, horizSpeed, predictedTurnDegrees04, isGrounded, isCrouching, isSprinting, isSwimming, isAiming, inVehicle, dt);
            }

            _wasGrounded = isGrounded;
        }

        public void TriggerJump()
        {
            if (targetAnimator != null && targetAnimator.runtimeAnimatorController != null)
            {
                targetAnimator.SetTrigger(TrigJump);
            }
        }

        public void TriggerInteract()
        {
            if (targetAnimator != null && targetAnimator.runtimeAnimatorController != null)
            {
                targetAnimator.SetTrigger(TrigInteract);
            }
        }

        public void TriggerDialogueGesture()
        {
            if (targetAnimator != null && targetAnimator.runtimeAnimatorController != null)
            {
                targetAnimator.SetTrigger(TrigGesture);
            }
        }

        private void ApplyToAnimator(
            Vector3 localVelocity,
            float horizSpeed,
            float turnDelta,
            bool isGrounded,
            bool isCrouching,
            bool isSprinting,
            bool isSwimming,
            bool isAiming,
            bool inVehicle,
            float dt)
        {
            targetAnimator.speed = _animatorSpeedMultiplier;

            SetFloatSafe(ParamMoveSpeed, horizSpeed, parameterDampTime, dt);
            SetFloatSafe(ParamMoveX, localVelocity.x, parameterDampTime, dt);
            SetFloatSafe(ParamMoveZ, localVelocity.z, parameterDampTime, dt);
            SetFloatSafe(ParamTurnDelta, Mathf.Clamp(turnDelta / 90f, -1f, 1f), parameterDampTime, dt);
            SetFloatSafe(ParamGaitPhase, _gaitPhaseNormalized, 0f, dt);

            float rad = _gaitPhaseNormalized * Mathf.PI * 2f;
            SetFloatSafe(ParamGaitSin, Mathf.Sin(rad), 0f, dt);
            SetFloatSafe(ParamGaitCos, Mathf.Cos(rad), 0f, dt);
            SetFloatSafe(ParamVerticalVelocity, localVelocity.y, 0.05f, dt);
            SetFloatSafe(ParamLeftFootContact, IsLeftFootPlanted ? 1f : 0f, 0.04f, dt);
            SetFloatSafe(ParamRightFootContact, IsRightFootPlanted ? 1f : 0f, 0.04f, dt);

            SetBoolSafe(ParamIsGrounded, isGrounded);
            SetBoolSafe(ParamIsSprinting, isSprinting);
            SetBoolSafe(ParamIsCrouching, isCrouching);
            SetBoolSafe(ParamIsSwimming, isSwimming);
            SetBoolSafe(ParamIsAiming, isAiming);
            SetBoolSafe(ParamInVehicle, inVehicle);

            if (!_wasGrounded && isGrounded)
            {
                targetAnimator.SetTrigger(TrigLand);
            }
        }

        private void SetFloatSafe(int hash, float value, float dampTime, float dt)
        {
            if (dampTime > 0f)
            {
                targetAnimator.SetFloat(hash, value, dampTime, dt);
            }
            else
            {
                targetAnimator.SetFloat(hash, value);
            }
        }

        private void SetBoolSafe(int hash, bool value)
        {
            targetAnimator.SetBool(hash, value);
        }

        private static bool IsInNormalizedWindow(float phase01, float start01, float end01)
        {
            if (end01 <= start01)
            {
                return false;
            }
            return phase01 >= start01 && phase01 <= end01;
        }
    }
}
