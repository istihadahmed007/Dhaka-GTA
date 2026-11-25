using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectDhaka
{
    /// <summary>
    /// GTA-inspired Cinemachine 3 third-person camera controller for Project Dhaka.
    /// Supports smooth mouse/stick orbit + damping, sphere-cast obstacle collision recovery,
    /// sprint/jump/landing framing dynamics, camera distance presets (V), and cinematic view transitions (C).
    /// Preserves public API used by GameLoop, IntelPickup, PatrolHazard, and Scene wiring.
    /// </summary>
    public class DhakaCameraRig : MonoBehaviour
    {
        public static DhakaCameraRig Instance { get; private set; }

        [Header("Cinemachine Cameras")]
        [SerializeField] CinemachineCamera titleOrbitCam;
        [SerializeField] CinemachineCamera gameplayFollowCam;
        [SerializeField] Transform followProxy;
        [SerializeField] Transform playerTarget;
        [SerializeField] CinemachineImpulseSource impulseSource;

        [Header("Orbit & Damping")]
        [SerializeField] float titleOrbitDegPerSec = 9f;
        [SerializeField] float mouseSensitivity = 0.14f;
        [SerializeField] float stickSensitivityDegPerSec = 135f;
        [SerializeField] float orbitDamping = 14f;
        [SerializeField] float minPitchDeg = -18f;
        [SerializeField] float maxPitchDeg = 58f;
        [SerializeField] float proxyFollowSpeed = 22f;

        [Header("Framing & Obstacle Collision")]
        [SerializeField] float idleFov = 54f;
        [SerializeField] float sprintFov = 63f;
        [SerializeField] float airFovBoost = 3.5f;
        [SerializeField] LayerMask collisionMask = ~0;
        [SerializeField] float collisionProbeRadius = 0.24f;
        [SerializeField] float minCollisionDistance = 1.25f;
        [SerializeField] float collisionPullInSpeed = 24f;
        [SerializeField] float collisionRecoverSpeed = 7f;

        public CinemachineCamera TitleOrbitCam => titleOrbitCam;
        public CinemachineCamera GameplayFollowCam => gameplayFollowCam;
        public float ConfiguredCameraDistance => desiredPresetDistance;

        CinemachineThirdPersonFollow thirdPersonFollow;
        PlayerController playerController;
        InputAction lookAction;

        static readonly float[] DistancePresets = { 4.8f, 6.8f, 3.3f }; // Medium, Wide, Close Shoulder
        static readonly Vector3[] ShoulderPresets =
        {
            new Vector3(0.46f, 1.50f, 0f),
            new Vector3(0.32f, 1.66f, 0f),
            new Vector3(0.52f, 1.46f, 0f)
        };

        int presetIndex;
        float desiredPresetDistance = 4.8f;
        float currentCollisionDistance = 4.8f;
        float targetYaw;
        float targetPitch = 14f;
        float smoothedYaw;
        float smoothedPitch = 14f;
        float timeSinceManualLook = 99f;
        bool cinematicToggle;

        float currentFov;
        float landingDipOffset;
        float landingDipVel;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            CacheComponents();
            var asset = InputSystem.actions;
            if (asset != null) lookAction = asset.FindAction("Player/Look", false);
        }

        void OnEnable()
        {
            lookAction?.Enable();
        }

        public void BindRig(CinemachineCamera titleCam, CinemachineCamera gameplayCam, Transform proxy, Transform player, CinemachineImpulseSource impulse)
        {
            titleOrbitCam = titleCam;
            gameplayFollowCam = gameplayCam;
            followProxy = proxy;
            playerTarget = player;
            impulseSource = impulse;
            CacheComponents();
        }

        void CacheComponents()
        {
            if (gameplayFollowCam != null)
            {
                thirdPersonFollow = gameplayFollowCam.GetComponent<CinemachineThirdPersonFollow>();
                if (thirdPersonFollow != null)
                {
                    desiredPresetDistance = DistancePresets[presetIndex];
                    currentCollisionDistance = desiredPresetDistance;
                    thirdPersonFollow.CameraDistance = desiredPresetDistance;
                    thirdPersonFollow.ShoulderOffset = ShoulderPresets[presetIndex];
                }
            }
            if (playerTarget != null)
            {
                playerController = playerTarget.GetComponent<PlayerController>();
                targetYaw = playerTarget.eulerAngles.y;
                smoothedYaw = targetYaw;
            }
            currentFov = idleFov;
        }

        void LateUpdate()
        {
            if (playerTarget == null || followProxy == null) return;
            if (playerController == null) playerController = playerTarget.GetComponent<PlayerController>();

            float dt = Time.deltaTime;
            if (dt <= 0.00001f) return;

            // Camera preset cycling (V) & cinematic orbit toggle (C)
            if (Keyboard.current != null)
            {
                if (Keyboard.current.vKey.wasPressedThisFrame)
                {
                    presetIndex = (presetIndex + 1) % DistancePresets.Length;
                    desiredPresetDistance = DistancePresets[presetIndex];
                }
                if (Keyboard.current.cKey.wasPressedThisFrame)
                {
                    cinematicToggle = !cinematicToggle;
                }
            }

            GameLoop loop = GameLoop.Instance;
            bool playing = loop == null || loop.CurrentState == GameLoop.PlayState.Playing;
            bool showGameplayCam = playing && !cinematicToggle;

            if (titleOrbitCam != null && gameplayFollowCam != null)
            {
                gameplayFollowCam.Priority = showGameplayCam ? 20 : 5;
                titleOrbitCam.Priority = showGameplayCam ? 5 : 20;
            }

            // Spring-damped vertical landing dip recovery
            landingDipOffset = Mathf.SmoothDamp(landingDipOffset, 0f, ref landingDipVel, 0.16f);

            // Smooth proxy position tracking with subtle jump lag & landing dip
            Vector3 desiredProxyPos = playerTarget.position + Vector3.up * landingDipOffset;
            followProxy.position = Vector3.Lerp(followProxy.position, desiredProxyPos, 1f - Mathf.Exp(-proxyFollowSpeed * dt));

            if (!showGameplayCam)
            {
                // Smooth cinematic orbit around protagonist
                targetYaw += titleOrbitDegPerSec * dt;
                smoothedYaw = Mathf.LerpAngle(smoothedYaw, targetYaw, 1f - Mathf.Exp(-8f * dt));
                followProxy.rotation = Quaternion.Euler(14f, smoothedYaw, 0f);
                return;
            }

            // Mouse & gamepad orbit input
            Vector2 lookInput = lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;
            Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
            bool manualLook = false;

            if (mouseDelta.sqrMagnitude > 0.04f)
            {
                targetYaw += mouseDelta.x * mouseSensitivity;
                targetPitch -= mouseDelta.y * mouseSensitivity;
                manualLook = true;
            }
            else if (Gamepad.current != null && lookInput.sqrMagnitude > 0.02f)
            {
                targetYaw += lookInput.x * stickSensitivityDegPerSec * dt;
                targetPitch -= lookInput.y * stickSensitivityDegPerSec * dt;
                manualLook = true;
            }

            targetPitch = Mathf.Clamp(targetPitch, minPitchDeg, maxPitchDeg);
            timeSinceManualLook = manualLook ? 0f : timeSinceManualLook + dt;

            // Gentle GTA-style auto-follow behind character after manual mouse idle while running
            if (!manualLook && timeSinceManualLook > 1.8f && playerController != null)
            {
                Vector3 vel = playerController.PlanarVelocity;
                if (vel.sqrMagnitude > 2.2f)
                {
                    float moveYaw = Mathf.Atan2(vel.x, vel.z) * Mathf.Rad2Deg;
                    float alignRate = Mathf.Clamp01(vel.magnitude / 7f) * 1.35f;
                    targetYaw = Mathf.LerpAngle(targetYaw, moveYaw, alignRate * dt);
                }
            }

            float kOrbit = 1f - Mathf.Exp(-orbitDamping * dt);
            smoothedYaw = Mathf.LerpAngle(smoothedYaw, targetYaw, kOrbit);
            smoothedPitch = Mathf.Lerp(smoothedPitch, targetPitch, kOrbit);
            followProxy.rotation = Quaternion.Euler(smoothedPitch, smoothedYaw, 0f);

            // Dynamic FOV (sprint + jump/fall responsiveness) & Obstacle-aware camera collision
            float speedNorm = playerController != null
                ? Mathf.Clamp01(playerController.PlanarVelocity.magnitude / Mathf.Max(0.1f, playerController.MaxSpeed))
                : 0f;
            float airBoost = (playerController != null && !playerController.IsGrounded) ? airFovBoost : 0f;
            float targetFov = Mathf.Lerp(idleFov, sprintFov, speedNorm) + airBoost;
            currentFov = Mathf.Lerp(currentFov, targetFov, 1f - Mathf.Exp(-6.5f * dt));

            if (gameplayFollowCam != null)
            {
                var lens = gameplayFollowCam.Lens;
                lens.FieldOfView = currentFov;
                gameplayFollowCam.Lens = lens;
            }

            if (thirdPersonFollow != null)
            {
                Vector3 baseShoulder = ShoulderPresets[presetIndex];
                float crouchDrop = (playerController != null && !playerController.IsGrounded) ? -0.10f : 0f;
                thirdPersonFollow.ShoulderOffset = Vector3.Lerp(
                    thirdPersonFollow.ShoulderOffset,
                    baseShoulder + new Vector3(0f, crouchDrop, 0f),
                    1f - Mathf.Exp(-10f * dt));

                // Safe obstacle-aware SphereCast from shoulder pivot backward along view vector
                Vector3 shoulderWorld = followProxy.position + followProxy.TransformVector(thirdPersonFollow.ShoulderOffset);
                Vector3 backDir = -(followProxy.rotation * Vector3.forward);
                float maxDist = desiredPresetDistance + speedNorm * 0.45f;
                float resolvedDist = maxDist;

                RaycastHit[] hits = Physics.SphereCastAll(
                    shoulderWorld, collisionProbeRadius, backDir, maxDist, collisionMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < hits.Length; i++)
                {
                    if (hits[i].collider == null) continue;
                    if (playerTarget != null && hits[i].collider.transform.IsChildOf(playerTarget)) continue;
                    if (hits[i].distance > 0.05f)
                    {
                        resolvedDist = Mathf.Min(resolvedDist, Mathf.Max(minCollisionDistance, hits[i].distance - 0.12f));
                    }
                }

                float camLerpSpeed = resolvedDist < currentCollisionDistance ? collisionPullInSpeed : collisionRecoverSpeed;
                currentCollisionDistance = Mathf.Lerp(currentCollisionDistance, resolvedDist, 1f - Mathf.Exp(-camLerpSpeed * dt));
                thirdPersonFollow.CameraDistance = currentCollisionDistance;
            }
        }

        public void ResetForRunStart()
        {
            cinematicToggle = false;
            currentFov = idleFov;
            landingDipOffset = 0f;
            landingDipVel = 0f;
            if (followProxy != null && playerTarget != null)
            {
                followProxy.position = playerTarget.position;
                targetYaw = playerTarget.eulerAngles.y;
                smoothedYaw = targetYaw;
                targetPitch = 14f;
                smoothedPitch = 14f;
                followProxy.rotation = Quaternion.Euler(smoothedPitch, smoothedYaw, 0f);
            }
        }

        public void TriggerPickupImpulse()
        {
            if (impulseSource != null)
            {
                impulseSource.GenerateImpulseWithForce(0.32f);
            }
        }

        public void TriggerHitImpulse()
        {
            if (impulseSource != null)
            {
                impulseSource.GenerateImpulseWithForce(0.85f);
            }
        }

        public void SetCameraAngles(float yawDeg, float pitchDeg)
        {
            targetYaw = yawDeg;
            smoothedYaw = yawDeg;
            targetPitch = Mathf.Clamp(pitchDeg, minPitchDeg, maxPitchDeg);
            smoothedPitch = targetPitch;
            timeSinceManualLook = 0f;
            if (followProxy != null)
            {
                followProxy.rotation = Quaternion.Euler(smoothedPitch, smoothedYaw, 0f);
            }
        }

        public void TriggerLandingImpulse(float impactVerticalSpeed)
        {
            float strength = Mathf.Clamp01((impactVerticalSpeed - 3.5f) / 10f);
            landingDipOffset = -Mathf.Lerp(0.05f, 0.22f, strength);
            if (impulseSource != null && strength > 0.1f)
            {
                impulseSource.GenerateImpulseWithForce(0.18f + 0.35f * strength);
            }
        }
    }
}
