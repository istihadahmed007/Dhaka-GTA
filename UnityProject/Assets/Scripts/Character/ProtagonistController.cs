using DhakaGta.Core;
using DhakaGta.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DhakaGta.Character
{
    /// <summary>
    /// Grounded, swimming, and IK/Motion-Matched third-person protagonist controller built on Unity CharacterController,
    /// MotionTrajectoryPredictor, MotionMatchingController, CharacterIkAndFootLocker, CharacterAbilityController,
    /// and CharacterSwimAndDialogueController. Preserves full backward-compatibility for synthetic PlayMode tests.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class ProtagonistController : MonoBehaviour
    {
        [SerializeField] private ProtagonistProfileSO profile;
        [SerializeField] private InputActionRouter inputRouter;
        [SerializeField] private Transform cameraReference;
        [SerializeField] private float rotationSpeedDegPerSec = 540f;
        [SerializeField] private float aimRotationSpeedDegPerSec = 780f;
        [SerializeField] private float gravityMetersPerSecSq = -22f;

        private CharacterController _controller;
        private MotionTrajectoryPredictor _trajectoryPredictor;
        private MotionMatchingController _motionMatcher;
        private CharacterIkAndFootLocker _ikLocker;
        private CharacterAbilityController _abilityController;
        private CharacterSwimAndDialogueController _swimDialogueController;

        private float _verticalVelocity;
        private float _currentStamina = 100f;
        private Vector2 _syntheticMoveInput;
        private bool _hasSyntheticInput;
        private bool _syntheticSprint;
        private bool _syntheticAim;
        private Vector3 _syntheticAimWorldPoint;

        public ProtagonistProfileSO Profile => profile;
        public float CurrentStamina => _currentStamina;
        public float MaxStamina => profile != null ? profile.maxStamina : 100f;
        public bool IsGrounded => _controller != null && _controller.isGrounded;
        public bool IsCrouching { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsSwimming => _swimDialogueController != null && _swimDialogueController.IsSwimming;
        public bool IsAiming { get; private set; }
        public bool InVehicle { get; private set; }

        public MotionTrajectoryPredictor TrajectoryPredictor => _trajectoryPredictor;
        public MotionMatchingController MotionMatcher => _motionMatcher;
        public CharacterIkAndFootLocker IkLocker => _ikLocker;
        public CharacterAbilityController AbilityController => _abilityController;
        public CharacterSwimAndDialogueController SwimAndDialogueController => _swimDialogueController;

        public void Configure(ProtagonistProfileSO profileAsset, InputActionRouter router, Transform cameraTransform)
        {
            profile = profileAsset;
            inputRouter = router;
            cameraReference = cameraTransform;
            EnsureSubSystems();
            ResetStatsFromProfile();
        }

        public void BindActiveVisualRig(Animator activeAnimator)
        {
            EnsureSubSystems();
            _motionMatcher?.Configure(activeAnimator);
            _ikLocker?.Configure(activeAnimator, _motionMatcher);
        }

        public void SetSyntheticMoveInput(Vector2 moveInput, bool sprint = false)
        {
            _syntheticMoveInput = Vector2.ClampMagnitude(moveInput, 1f);
            _syntheticSprint = sprint;
            _hasSyntheticInput = true;
        }

        public void SetSyntheticAimState(bool aiming, Vector3 worldAimPoint)
        {
            _syntheticAim = aiming;
            _syntheticAimWorldPoint = worldAimPoint;
        }

        public void SetVehicleOccupancy(bool inVehicle)
        {
            InVehicle = inVehicle;
        }

        public void ClearSyntheticInput()
        {
            _syntheticMoveInput = Vector2.zero;
            _syntheticSprint = false;
            _syntheticAim = false;
            _hasSyntheticInput = false;
        }

        public void ResetStatsFromProfile()
        {
            _currentStamina = profile != null ? profile.maxStamina : 100f;
            _abilityController?.Configure(profile);
            if (profile != null)
            {
                _swimDialogueController?.SetProtagonistId(profile.protagonistId);
                GameEvents.RaiseProtagonistChanged(profile.protagonistId);
            }
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            gameObject.layer = PhysicsLayers.Player;
            EnsureSubSystems();
            ResetStatsFromProfile();
        }

        private void EnsureSubSystems()
        {
            _trajectoryPredictor = GetComponent<MotionTrajectoryPredictor>() ?? gameObject.AddComponent<MotionTrajectoryPredictor>();
            _motionMatcher = GetComponent<MotionMatchingController>() ?? gameObject.AddComponent<MotionMatchingController>();
            _ikLocker = GetComponent<CharacterIkAndFootLocker>() ?? gameObject.AddComponent<CharacterIkAndFootLocker>();
            _abilityController = GetComponent<CharacterAbilityController>() ?? gameObject.AddComponent<CharacterAbilityController>();
            _swimDialogueController = GetComponent<CharacterSwimAndDialogueController>() ?? gameObject.AddComponent<CharacterSwimAndDialogueController>();
        }

        private void Update()
        {
            if (GameBootstrap.CurrentState == GameState.Paused)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0.00001f)
            {
                return;
            }

            _abilityController?.StepAbilityTimers(Time.unscaledDeltaTime);

            // Detect seat-mounted vehicle driving automatically when VehicleSeats disables CharacterController
            bool seatedInVehicle = InVehicle || !_controller.enabled;
            if (seatedInVehicle)
            {
                IsAiming = false;
                IsSprinting = false;
                _trajectoryPredictor.StepPrediction(transform.position, transform.forward, Vector3.zero, transform.forward, dt);
                _motionMatcher.StepMotionMatching(Vector3.zero, 0f, false, false, false, false, false, true, dt);
                _ikLocker.StepFootLockWeights(false, false, true, dt);
                return;
            }

            Vector2 move = ReadMoveInput();
            bool sprintHeld = _hasSyntheticInput
                ? _syntheticSprint
                : (inputRouter != null && inputRouter.SprintAction != null && inputRouter.SprintAction.IsPressed()) ||
                  (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed);
            bool jumpPressed = !_hasSyntheticInput &&
                               ((inputRouter != null && inputRouter.JumpAction != null && inputRouter.JumpAction.WasPressedThisFrame()) ||
                                (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame));
            bool crouchPressed = !_hasSyntheticInput &&
                                 ((inputRouter != null && inputRouter.CrouchAction != null && inputRouter.CrouchAction.WasPressedThisFrame()) ||
                                  (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame));
            bool abilityPressed = !_hasSyntheticInput &&
                                  ((inputRouter != null && inputRouter.AbilityAction != null && inputRouter.AbilityAction.WasPressedThisFrame()) ||
                                   (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame));
            bool gesturePressed = !_hasSyntheticInput &&
                                  ((inputRouter != null && inputRouter.GestureAction != null && inputRouter.GestureAction.WasPressedThisFrame()) ||
                                   (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame));
            bool interactPressed = !_hasSyntheticInput &&
                                   ((inputRouter != null && inputRouter.InteractAction != null && inputRouter.InteractAction.WasPressedThisFrame()) ||
                                    (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame));

            IsAiming = _hasSyntheticInput
                ? _syntheticAim
                : (inputRouter != null && inputRouter.AimAction != null && inputRouter.AimAction.IsPressed()) ||
                  (Mouse.current != null && Mouse.current.rightButton.isPressed);

            if (abilityPressed)
            {
                _abilityController?.TryActivateAbility();
            }

            if (gesturePressed)
            {
                _swimDialogueController?.TriggerNextDialogueGesture(_motionMatcher);
            }

            if (interactPressed)
            {
                _swimDialogueController?.TryInteractNearest(_ikLocker, _motionMatcher);
            }

            bool isSwimming = _swimDialogueController != null &&
                              _swimDialogueController.EvaluateWaterState(transform.position, _verticalVelocity, dt, out float swimVerticalVel);

            if (crouchPressed && !isSwimming)
            {
                IsCrouching = !IsCrouching;
                _controller.height = IsCrouching ? 1.2f : 1.8f;
                _controller.center = new Vector3(0f, _controller.height * 0.5f, 0f);
            }
            else if (isSwimming && IsCrouching)
            {
                IsCrouching = false;
                _controller.height = 1.8f;
                _controller.center = new Vector3(0f, 0.9f, 0f);
            }

            IsSprinting = sprintHeld && !IsCrouching && !IsAiming && move.sqrMagnitude > 0.04f && _currentStamina > 2f;
            UpdateStamina(dt);

            float targetSpeed = GetCurrentSpeed(move.magnitude, isSwimming);
            Vector3 moveDirection = ComputeCameraRelativeDirection(move);
            Vector3 desiredWorldVel = moveDirection * targetSpeed;

            Vector3 desiredFacing = ResolveFacingDirection(moveDirection);
            if (desiredFacing.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(desiredFacing, Vector3.up);
                float rotSpeed = IsAiming ? aimRotationSpeedDegPerSec : rotationSpeedDegPerSec;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotSpeed * dt);
            }

            if (isSwimming)
            {
                _verticalVelocity = swimVerticalVel;
            }
            else
            {
                if (_controller.isGrounded && _verticalVelocity < 0f)
                {
                    _verticalVelocity = -2f;
                }

                if (_controller.isGrounded && jumpPressed && !IsCrouching)
                {
                    float jumpHeight = profile != null ? profile.jumpHeightMeters : 1.2f;
                    _verticalVelocity = Mathf.Sqrt(Mathf.Max(0.2f, jumpHeight) * -2f * gravityMetersPerSecSq);
                    _motionMatcher?.TriggerJump();
                }

                _verticalVelocity += gravityMetersPerSecSq * dt;
            }

            // Predict smooth root trajectory arc (AI4Animation) and synchronize motion matching + foot locking IK
            _trajectoryPredictor.StepPrediction(transform.position, transform.forward, desiredWorldVel, desiredFacing, dt);

            Vector3 activeHorizVel = _trajectoryPredictor.CurrentWorldVelocity;
            Vector3 finalVelocity = new Vector3(activeHorizVel.x, _verticalVelocity, activeHorizVel.z);
            _controller.Move(finalVelocity * dt);

            Vector3 localVel = _trajectoryPredictor.CurrentLocalVelocity;
            localVel.y = _verticalVelocity;

            _motionMatcher.StepMotionMatching(
                localVel,
                _trajectoryPredictor.PredictedTurnDegrees04,
                _controller.isGrounded,
                IsCrouching,
                IsSprinting,
                isSwimming,
                IsAiming,
                false,
                dt);

            if (IsAiming)
            {
                Vector3 aimPt = _hasSyntheticInput
                    ? _syntheticAimWorldPoint
                    : ResolveAimWorldPoint();
                _ikLocker.SetAimTargetState(new CharacterAimTargetState
                {
                    IsAiming = true,
                    WorldAimPoint = aimPt,
                    AimBlendWeight = 1f,
                    HasTwoHandedWeapon = true
                });
            }
            else
            {
                _ikLocker.SetAimTargetState(default);
            }

            _ikLocker.StepFootLockWeights(_controller.isGrounded, isSwimming, false, dt);
        }

        private Vector3 ResolveFacingDirection(Vector3 moveDirection)
        {
            if (IsAiming)
            {
                Vector3 aimPt = _hasSyntheticInput ? _syntheticAimWorldPoint : ResolveAimWorldPoint();
                Vector3 toAim = aimPt - transform.position;
                toAim.y = 0f;
                if (toAim.sqrMagnitude > 0.001f)
                {
                    return toAim.normalized;
                }
            }

            return moveDirection.sqrMagnitude > 0.001f ? moveDirection : transform.forward;
        }

        private Vector3 ResolveAimWorldPoint()
        {
            Transform cam = cameraReference != null ? cameraReference : (Camera.main != null ? Camera.main.transform : null);
            if (cam != null)
            {
                return cam.position + cam.forward * 24f;
            }

            return transform.position + transform.forward * 12f + Vector3.up * 1.4f;
        }

        private Vector2 ReadMoveInput()
        {
            if (_hasSyntheticInput)
            {
                return _syntheticMoveInput;
            }

            if (inputRouter != null && inputRouter.MoveAction != null && inputRouter.MoveAction.enabled)
            {
                Vector2 boundVal = inputRouter.MoveAction.ReadValue<Vector2>();
                if (boundVal.sqrMagnitude > 0.0001f)
                {
                    return Vector2.ClampMagnitude(boundVal, 1f);
                }
            }

            if (Keyboard.current != null)
            {
                float x = (Keyboard.current.dKey.isPressed ? 1f : 0f) - (Keyboard.current.aKey.isPressed ? 1f : 0f);
                float y = (Keyboard.current.wKey.isPressed ? 1f : 0f) - (Keyboard.current.sKey.isPressed ? 1f : 0f);
                if (x != 0f || y != 0f)
                {
                    return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
                }
            }

            return Vector2.zero;
        }

        private float GetCurrentSpeed(float inputMagnitude, bool isSwimming)
        {
            if (inputMagnitude <= 0.01f)
            {
                return 0f;
            }

            float abilityMult = _abilityController != null ? _abilityController.CurrentSpeedMultiplier : 1f;

            if (isSwimming && _swimDialogueController != null)
            {
                float baseSwim = IsSprinting ? _swimDialogueController.SwimSprintSpeed : _swimDialogueController.SwimSpeed;
                return baseSwim * inputMagnitude * abilityMult;
            }

            float walk = profile != null ? profile.walkSpeedMetersPerSec : 2.4f;
            float jog = profile != null ? profile.jogSpeedMetersPerSec : 5.1f;
            float sprint = profile != null ? profile.sprintSpeedMetersPerSec : 7.6f;

            if (IsCrouching)
            {
                return walk * 0.85f * inputMagnitude;
            }

            if (IsAiming)
            {
                return walk * 1.08f * inputMagnitude;
            }

            if (IsSprinting)
            {
                return sprint * inputMagnitude * abilityMult;
            }

            return jog * inputMagnitude * abilityMult;
        }

        private void UpdateStamina(float dt)
        {
            float cap = MaxStamina;
            float bonusRegen = _abilityController != null ? _abilityController.CurrentStaminaRegenBonus : 0f;
            if (IsSprinting)
            {
                _currentStamina = Mathf.Max(0f, _currentStamina - (18f - Mathf.Min(12f, bonusRegen * 0.45f)) * dt);
            }
            else
            {
                _currentStamina = Mathf.Min(cap, _currentStamina + (14f + bonusRegen) * dt);
            }
        }

        private Vector3 ComputeCameraRelativeDirection(Vector2 moveInput)
        {
            if (moveInput.sqrMagnitude < 0.0001f)
            {
                return Vector3.zero;
            }

            Transform cam = cameraReference != null ? cameraReference : (Camera.main != null ? Camera.main.transform : null);
            if (cam == null)
            {
                return new Vector3(moveInput.x, 0f, moveInput.y).normalized;
            }

            Vector3 forward = cam.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();

            Vector3 right = cam.right;
            right.y = 0f;
            right.Normalize();

            return (forward * moveInput.y + right * moveInput.x).normalized;
        }
    }
}
