using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectDhaka
{
    /// <summary>
    /// Fluid, obstacle-aware third-person Humanoid character controller for Project Dhaka.
    /// Drives Animator 2D Blend Trees (Idle, Walk, Jog, Sprint, Strafe/Backpedal), Start/Stop transitions,
    /// 90/180-degree turns, Jump/Fall/Land recovery, obstacle avoidance, and procedural RigBuilder drivers.
    /// Preserves public API used by GameLoop, DhakaCameraRig, IntelPickup, and PatrolHazard.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Locomotion Speeds (Matched to Humanoid Clips)")]
        [SerializeField] float walkSpeed = 1.65f;
        [SerializeField] float runSpeed = 4.25f;
        [SerializeField] float sprintSpeed = 7.40f;
        [SerializeField] float backwardSpeed = 2.60f;
        [SerializeField] float strafeSpeed = 3.40f;

        [Header("Acceleration, Turning & Physics")]
        [SerializeField] float acceleration = 18f;
        [SerializeField] float deceleration = 14f;
        [SerializeField] float airAcceleration = 6f;
        [SerializeField] float turnSpeedDeg = 540f;
        [SerializeField] float strafeTurnSpeedDeg = 720f;
        [SerializeField] float gravity = -22f;
        [SerializeField] float jumpSpeed = 7.6f;
        [SerializeField] float groundStickVelocity = -3.2f;

        [Header("Arena Bounds (Optional for SampleScene)")]
        [SerializeField] bool clampToArena;
        [SerializeField] float arenaHalfExtent = 20.5f;

        [Header("Rig & Visual References")]
        [SerializeField] Animator animator;
        [SerializeField] DhakaProceduralRigDriver rigDriver;
        [SerializeField] DhakaObstacleAvoider obstacleAvoider;
        [SerializeField] DhakaCourierVisual visualRig;
        [SerializeField] Transform cameraTransform;

        CharacterController controller;
        InputAction moveAction;
        InputAction sprintAction;
        InputAction jumpAction;

        Vector3 planarVelocity;
        Vector3 desiredMoveDirection;
        float verticalVelocity;
        float groundedMemoryTimer;
        float jumpBufferTimer;
        float airTime;
        float landRecoveryTimer;
        float prevYaw;
        float currentYawRateDeg;
        bool wasMoving;
        bool wasGrounded = true;

        // Optional deterministic test input override (never active unless explicitly enabled)
        bool simulatedInputActive;
        Vector2 simulatedMove;
        bool simulatedSprint;
        bool simulatedWalk;
        bool simulatedStrafe;
        bool simulatedJumpQueued;
        bool simulatedJumpLoop;
        float simulatedJumpCooldown;
        float simulatedRemainingTime = -1f;

        // Animator parameter hashes
        static readonly int HashMoveX = Animator.StringToHash("MoveX");
        static readonly int HashMoveZ = Animator.StringToHash("MoveZ");
        static readonly int HashSpeed = Animator.StringToHash("Speed");
        static readonly int HashSpeedNorm = Animator.StringToHash("SpeedNorm");
        static readonly int HashTurnAngle = Animator.StringToHash("TurnAngle");
        static readonly int HashVerticalSpeed = Animator.StringToHash("VerticalSpeed");
        static readonly int HashIsMoving = Animator.StringToHash("IsMoving");
        static readonly int HashIsGrounded = Animator.StringToHash("IsGrounded");
        static readonly int HashStartTrigger = Animator.StringToHash("StartTrigger");
        static readonly int HashStopTrigger = Animator.StringToHash("StopTrigger");
        static readonly int HashTurn90L = Animator.StringToHash("Turn90L");
        static readonly int HashTurn90R = Animator.StringToHash("Turn90R");
        static readonly int HashTurn180L = Animator.StringToHash("Turn180L");
        static readonly int HashTurn180R = Animator.StringToHash("Turn180R");
        static readonly int HashJumpTrigger = Animator.StringToHash("JumpTrigger");
        static readonly int HashLandTrigger = Animator.StringToHash("LandTrigger");

        public Vector3 PlanarVelocity => planarVelocity;
        public Vector3 DesiredMoveDirection => desiredMoveDirection;
        public float MaxSpeed => sprintSpeed;
        public bool IsSprinting { get; private set; }
        public bool IsGrounded => groundedMemoryTimer > 0f;
        public string CurrentMotionLabel { get; private set; } = "Idle";

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            ResolveReferences();
            ResolveInputActions();
            prevYaw = transform.eulerAngles.y;
        }

        public void ResolveReferences()
        {
            animator ??= GetComponentInChildren<Animator>();
            rigDriver ??= GetComponent<DhakaProceduralRigDriver>();
            obstacleAvoider ??= GetComponent<DhakaObstacleAvoider>();
            visualRig ??= GetComponentInChildren<DhakaCourierVisual>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        }

        void ResolveInputActions()
        {
            var asset = InputSystem.actions;
            if (asset != null)
            {
                moveAction = asset.FindAction("Player/Move", false);
                sprintAction = asset.FindAction("Player/Sprint", false);
                jumpAction = asset.FindAction("Player/Jump", false);
            }
        }

        void OnEnable()
        {
            moveAction?.Enable();
            sprintAction?.Enable();
            jumpAction?.Enable();
        }

        public void SetVisualRig(DhakaCourierVisual rig) => visualRig = rig;

        public void SetArenaClamp(bool clamp, float halfExtent = 20.5f)
        {
            clampToArena = clamp;
            arenaHalfExtent = halfExtent;
        }

        /// <summary>
        /// Allows Play Mode verification scripts to drive scripted locomotion tests cleanly without overriding release input.
        /// </summary>
        public void SetSimulatedInput(bool active, Vector2 move, bool sprint = false, bool walk = false, bool jump = false, bool strafe = false, float durationSeconds = -1f, bool repeatJump = false)
        {
            simulatedInputActive = active;
            simulatedMove = Vector2.ClampMagnitude(move, 1f);
            simulatedSprint = sprint;
            simulatedWalk = walk;
            simulatedStrafe = strafe;
            simulatedJumpLoop = repeatJump;
            simulatedJumpCooldown = 0f;
            simulatedRemainingTime = durationSeconds;
            if (jump) simulatedJumpQueued = true;
        }

        public void ResetVelocity()
        {
            planarVelocity = Vector3.zero;
            desiredMoveDirection = Vector3.zero;
            verticalVelocity = 0f;
            currentYawRateDeg = 0f;
            landRecoveryTimer = 0f;
            jumpBufferTimer = 0f;
            wasMoving = false;
            wasGrounded = true;
            groundedMemoryTimer = 0.12f;
            airTime = 0f;
            if (animator != null && animator.isActiveAndEnabled)
            {
                animator.SetFloat(HashMoveX, 0f);
                animator.SetFloat(HashMoveZ, 0f);
                animator.SetFloat(HashSpeed, 0f);
                animator.SetFloat(HashSpeedNorm, 0f);
                animator.SetBool(HashIsMoving, false);
                animator.SetBool(HashIsGrounded, true);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0.00001f) return;

            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;

            GameLoop loop = GameLoop.Instance;
            if (loop != null && loop.CurrentState != GameLoop.PlayState.Playing)
            {
                planarVelocity = Vector3.Lerp(planarVelocity, Vector3.zero, 1f - Mathf.Exp(-10f * dt));
                verticalVelocity = Mathf.Max(groundStickVelocity, verticalVelocity + gravity * dt);
                UpdateAnimatorParameters(dt, true, false, 0f);
                rigDriver?.SetMotionState(planarVelocity, 0f, 0f, true);
                return;
            }

            // Optional world trajectory arrow toggle via T
            if (obstacleAvoider != null && Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            {
                obstacleAvoider.ShowWorldTrajectoryArrows = !obstacleAvoider.ShowWorldTrajectoryArrows;
            }

            ReadInputs(out Vector2 moveInput, out bool sprintHeld, out bool walkHeld, out bool strafeHeld, out bool jumpPressed);

            // Grounded coyote + jump buffer
            bool ccGrounded = controller != null && controller.isGrounded;
            if (ccGrounded) groundedMemoryTimer = 0.11f;
            else groundedMemoryTimer = Mathf.Max(0f, groundedMemoryTimer - dt);

            bool isGroundedNow = groundedMemoryTimer > 0f;
            if (jumpPressed) jumpBufferTimer = 0.14f;
            else jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - dt);

            if (landRecoveryTimer > 0f) landRecoveryTimer = Mathf.Max(0f, landRecoveryTimer - dt);

            // Landing detection
            if (ccGrounded && !wasGrounded && airTime > 0.16f)
            {
                if (animator != null && animator.isActiveAndEnabled)
                {
                    animator.SetTrigger(HashLandTrigger);
                }
                landRecoveryTimer = 0.14f;
                DhakaCameraRig.Instance?.TriggerLandingImpulse(Mathf.Abs(verticalVelocity));
                visualRig?.TriggerLandingSquash();
                AudioBank.Instance?.PlayJump();
            }
            wasGrounded = ccGrounded;
            airTime = ccGrounded ? 0f : airTime + dt;

            // Camera-relative planar wish direction
            Vector3 camFwd = cameraTransform != null ? cameraTransform.forward : transform.forward;
            Vector3 camRight = cameraTransform != null ? cameraTransform.right : transform.right;
            camFwd.y = 0f;
            camRight.y = 0f;
            if (camFwd.sqrMagnitude < 0.0001f) camFwd = Vector3.forward;
            if (camRight.sqrMagnitude < 0.0001f) camRight = Vector3.right;
            camFwd.Normalize();
            camRight.Normalize();

            Vector3 rawWish = camFwd * moveInput.y + camRight * moveInput.x;
            float inputMag = Mathf.Clamp01(moveInput.magnitude);
            Vector3 wishDir = inputMag > 0.05f ? rawWish.normalized : Vector3.zero;
            desiredMoveDirection = wishDir;

            // Compute desired target speed
            bool hasInput = inputMag > 0.05f;
            IsSprinting = hasInput && sprintHeld && !walkHeld && !strafeHeld;

            float baseTargetSpeed = 0f;
            if (hasInput)
            {
                if (walkHeld) baseTargetSpeed = walkSpeed * inputMag;
                else if (strafeHeld)
                {
                    float fwdDot = Vector3.Dot(wishDir, camFwd);
                    baseTargetSpeed = (fwdDot < -0.25f ? backwardSpeed : strafeSpeed) * inputMag;
                }
                else if (IsSprinting) baseTargetSpeed = sprintSpeed * inputMag;
                else baseTargetSpeed = runSpeed * inputMag;
            }

            if (landRecoveryTimer > 0f) baseTargetSpeed *= 0.65f;

            // Obstacle-aware predictive steering
            Vector3 steeredDir = wishDir;
            float avoidLean = 0f;
            if (obstacleAvoider != null)
            {
                steeredDir = obstacleAvoider.EvaluateMovementAndPredict(
                    wishDir, planarVelocity, baseTargetSpeed, dt);
                baseTargetSpeed *= obstacleAvoider.SpeedScaleFactor;
                avoidLean = obstacleAvoider.CurrentAvoidanceLean;
            }

            Vector3 targetVel = hasInput ? steeredDir * baseTargetSpeed : Vector3.zero;

            // Natural start/stop triggers & 90/180 pivot turn detection
            bool isMovingNow = hasInput && baseTargetSpeed > 0.25f;
            if (isMovingNow && !wasMoving && ccGrounded && animator != null && animator.isActiveAndEnabled)
            {
                animator.SetTrigger(HashStartTrigger);
            }
            else if (!isMovingNow && wasMoving && planarVelocity.magnitude > 1.35f && ccGrounded && animator != null && animator.isActiveAndEnabled)
            {
                animator.SetTrigger(HashStopTrigger);
            }
            wasMoving = isMovingNow;

            float turnDeltaDeg = 0f;
            if (hasInput && !strafeHeld)
            {
                turnDeltaDeg = Vector3.SignedAngle(transform.forward, steeredDir, Vector3.up);
                if (ccGrounded && planarVelocity.magnitude > 1.2f && animator != null && animator.isActiveAndEnabled)
                {
                    if (turnDeltaDeg > 132f) animator.SetTrigger(HashTurn180R);
                    else if (turnDeltaDeg < -132f) animator.SetTrigger(HashTurn180L);
                    else if (turnDeltaDeg > 72f && Mathf.Abs(currentYawRateDeg) < 220f) animator.SetTrigger(HashTurn90R);
                    else if (turnDeltaDeg < -72f && Mathf.Abs(currentYawRateDeg) < 220f) animator.SetTrigger(HashTurn90L);
                }
            }

            // Momentum damping on sharp reversal + smooth acceleration/deceleration
            float accelRate = hasInput ? (isGroundedNow ? acceleration : airAcceleration) : deceleration;
            if (hasInput && Vector3.Dot(planarVelocity.normalized, targetVel.normalized) < -0.35f && planarVelocity.magnitude > 1.5f)
            {
                accelRate *= 0.85f; // Natural weight shift pause before reversing
            }
            planarVelocity = Vector3.MoveTowards(planarVelocity, targetVel, accelRate * dt);

            // Smooth yaw orientation (face movement in standard mode, face camera in strafe mode)
            if (strafeHeld)
            {
                Quaternion goalRot = Quaternion.LookRotation(camFwd, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, goalRot, strafeTurnSpeedDeg * dt);
            }
            else
            {
                Vector3 faceDir = planarVelocity.sqrMagnitude > 0.16f ? planarVelocity.normalized : steeredDir;
                faceDir.y = 0f;
                if (faceDir.sqrMagnitude > 0.01f)
                {
                    Quaternion goalRot = Quaternion.LookRotation(faceDir.normalized, Vector3.up);
                    float turnRate = Mathf.Lerp(turnSpeedDeg * 0.72f, turnSpeedDeg, Mathf.Clamp01(planarVelocity.magnitude / 3.5f));
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, goalRot, turnRate * dt);
                }
            }

            float curYaw = transform.eulerAngles.y;
            currentYawRateDeg = Mathf.DeltaAngle(prevYaw, curYaw) / dt;
            prevYaw = curYaw;

            // Jump & gravity
            if (jumpBufferTimer > 0f && groundedMemoryTimer > 0f)
            {
                verticalVelocity = jumpSpeed;
                jumpBufferTimer = 0f;
                groundedMemoryTimer = 0f;
                ccGrounded = false;
                if (animator != null && animator.isActiveAndEnabled)
                {
                    animator.SetTrigger(HashJumpTrigger);
                }
                AudioBank.Instance?.PlayJump();
            }
            else if (ccGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = groundStickVelocity;
            }
            else
            {
                verticalVelocity += gravity * dt;
            }

            // Apply motion via CharacterController
            if (controller != null)
            {
                Vector3 totalMove = (planarVelocity + Vector3.up * verticalVelocity) * dt;
                controller.Move(totalMove);

                if (clampToArena && arenaHalfExtent > 0f)
                {
                    Vector3 pos = transform.position;
                    pos.x = Mathf.Clamp(pos.x, -arenaHalfExtent, arenaHalfExtent);
                    pos.z = Mathf.Clamp(pos.z, -arenaHalfExtent, arenaHalfExtent);
                    transform.position = pos;
                }
            }

            // Update Animator, Rig Driver & legacy fallback visual rig
            UpdateAnimatorParameters(dt, isGroundedNow, strafeHeld, turnDeltaDeg);
            rigDriver?.SetMotionState(planarVelocity, currentYawRateDeg, avoidLean, isGroundedNow);
            if (visualRig != null) visualRig.UpdateMotion(planarVelocity, sprintSpeed, currentYawRateDeg, ccGrounded, dt);
        }

        void ReadInputs(out Vector2 moveInput, out bool sprintHeld, out bool walkHeld, out bool strafeHeld, out bool jumpPressed)
        {
            if (simulatedInputActive)
            {
                if (simulatedRemainingTime > 0f)
                {
                    simulatedRemainingTime -= Time.deltaTime;
                    if (simulatedRemainingTime <= 0f)
                    {
                        simulatedInputActive = false;
                        simulatedJumpLoop = false;
                        simulatedMove = Vector2.zero;
                        simulatedSprint = false;
                        simulatedWalk = false;
                        simulatedStrafe = false;
                    }
                }
                if (simulatedJumpLoop)
                {
                    simulatedJumpCooldown -= Time.deltaTime;
                    if (simulatedJumpCooldown <= 0f && controller != null && controller.isGrounded)
                    {
                        simulatedJumpQueued = true;
                        simulatedJumpCooldown = 0.92f;
                    }
                }
                moveInput = simulatedMove;
                sprintHeld = simulatedSprint;
                walkHeld = simulatedWalk;
                strafeHeld = simulatedStrafe;
                jumpPressed = simulatedJumpQueued;
                simulatedJumpQueued = false;
                return;
            }

            moveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
            sprintHeld = sprintAction != null && sprintAction.IsPressed();
            walkHeld = false;
            strafeHeld = false;
            jumpPressed = jumpAction != null && jumpAction.WasPressedThisFrame();

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (moveInput.sqrMagnitude < 0.01f)
                {
                    if (kb.wKey.isPressed || kb.upArrowKey.isPressed) moveInput.y += 1f;
                    if (kb.sKey.isPressed || kb.downArrowKey.isPressed) moveInput.y -= 1f;
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) moveInput.x += 1f;
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) moveInput.x -= 1f;
                }
                if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed) sprintHeld = true;
                if (kb.leftAltKey.isPressed || kb.capsLockKey.isPressed) walkHeld = true;
                if (kb.leftCtrlKey.isPressed) strafeHeld = true;
                if (kb.spaceKey.wasPressedThisFrame) jumpPressed = true;
            }

            if (Mouse.current != null && Mouse.current.rightButton.isPressed)
            {
                strafeHeld = true;
            }

            moveInput = Vector2.ClampMagnitude(moveInput, 1f);
        }

        void UpdateAnimatorParameters(float dt, bool grounded, bool strafing, float turnDeltaDeg)
        {
            float speed = planarVelocity.magnitude;
            Vector3 localVel = transform.InverseTransformDirection(planarVelocity);

            if (animator != null && animator.isActiveAndEnabled)
            {
                float k = 1f - Mathf.Exp(-14f * dt);
                animator.SetFloat(HashMoveX, Mathf.Lerp(animator.GetFloat(HashMoveX), localVel.x, k));
                animator.SetFloat(HashMoveZ, Mathf.Lerp(animator.GetFloat(HashMoveZ), localVel.z, k));
                animator.SetFloat(HashSpeed, speed);
                animator.SetFloat(HashSpeedNorm, Mathf.Clamp01(speed / sprintSpeed));
                animator.SetFloat(HashTurnAngle, turnDeltaDeg);
                animator.SetFloat(HashVerticalSpeed, verticalVelocity);
                animator.SetBool(HashIsMoving, speed > 0.18f);
                animator.SetBool(HashIsGrounded, grounded);
            }

            if (!grounded) CurrentMotionLabel = verticalVelocity > 0.5f ? "Jump" : "Fall";
            else if (landRecoveryTimer > 0f) CurrentMotionLabel = "Land";
            else if (speed < 0.22f) CurrentMotionLabel = "Idle";
            else if (strafing && Mathf.Abs(localVel.x) > Mathf.Abs(localVel.z)) CurrentMotionLabel = localVel.x < 0f ? "StrafeLeft" : "StrafeRight";
            else if (strafing && localVel.z < -0.25f) CurrentMotionLabel = "WalkBackward";
            else if (speed < 2.35f) CurrentMotionLabel = "Walk";
            else if (speed < 5.65f) CurrentMotionLabel = "Jog";
            else CurrentMotionLabel = "Sprint";
        }
    }
}
