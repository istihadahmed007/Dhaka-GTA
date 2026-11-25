using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;

namespace ProjectDhaka
{
    /// <summary>
    /// Procedural Animation Rigging driver for the Dhaka humanoid character.
    /// Evaluates ground raycasts, foot IK targets & surface normals, pelvis terrain drop,
    /// dynamic upper-body lean/banking, head POI tracking, and interaction hand IK via RigBuilder constraints.
    /// Never overwrites animated bone transforms directly in Update().
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class DhakaProceduralRigDriver : MonoBehaviour
    {
        [Header("Core References")]
        [SerializeField] Animator animator;
        [SerializeField] RigBuilder rigBuilder;
        [SerializeField] Rig lowerBodyRig;
        [SerializeField] Rig upperBodyRig;

        [Header("Rig Constraints")]
        [SerializeField] OverrideTransform pelvisOffsetConstraint;
        [SerializeField] TwoBoneIKConstraint leftLegIK;
        [SerializeField] TwoBoneIKConstraint rightLegIK;
        [SerializeField] OverrideTransform torsoTiltConstraint;
        [SerializeField] MultiAimConstraint headAimConstraint;
        [SerializeField] TwoBoneIKConstraint leftArmIK;
        [SerializeField] TwoBoneIKConstraint rightArmIK;

        [Header("Constraint Target & Hint Proxies")]
        [SerializeField] Transform pelvisOffsetTarget;
        [SerializeField] Transform leftFootIKTarget;
        [SerializeField] Transform rightFootIKTarget;
        [SerializeField] Transform leftKneeHint;
        [SerializeField] Transform rightKneeHint;
        [SerializeField] Transform torsoTiltTarget;
        [SerializeField] Transform headLookTarget;
        [SerializeField] Transform leftHandIKTarget;
        [SerializeField] Transform rightHandIKTarget;
        [SerializeField] Transform leftElbowHint;
        [SerializeField] Transform rightElbowHint;

        [Header("Foot IK & Terrain Adaptation")]
        [SerializeField] LayerMask groundLayerMask = ~0;
        [SerializeField] float ankleSoleHeight = 0.088f;
        [SerializeField] float raycastUpOffset = 0.65f;
        [SerializeField] float raycastDistance = 1.45f;
        [SerializeField] float footSmoothSpeed = 18f;
        [SerializeField] float pelvisSmoothSpeed = 11f;
        [SerializeField] float maxPelvisDrop = 0.28f;
        [SerializeField] float maxFootPitchDeg = 38f;

        [Header("Head Look & Hand Interaction")]
        [SerializeField] float poiScanRadius = 5.5f;
        [SerializeField] float interactReachDistance = 1.05f;
        [SerializeField] float headAimSmoothSpeed = 7.5f;
        [SerializeField] float handIKBlendSpeed = 8.5f;

        // Cached humanoid bones for reading raycast origins (never written directly)
        Transform hipsBone;
        Transform upperChestBone;
        Transform headBone;
        Transform leftUpperLegBone, leftLowerLegBone, leftFootBone;
        Transform rightUpperLegBone, rightLowerLegBone, rightFootBone;
        Transform leftUpperArmBone, leftLowerArmBone, leftHandBone;
        Transform rightUpperArmBone, rightLowerArmBone, rightHandBone;

        static readonly int HashLeftFootIK = Animator.StringToHash("LeftFootIKWeight");
        static readonly int HashRightFootIK = Animator.StringToHash("RightFootIKWeight");

        float smoothedLeftFootY;
        float smoothedRightFootY;
        Vector3 smoothedLeftNormal = Vector3.up;
        Vector3 smoothedRightNormal = Vector3.up;
        float smoothedPelvisOffset;
        float smoothedLeftLegWeight;
        float smoothedRightLegWeight;
        float smoothedLeftArmWeight;
        float smoothedRightArmWeight;
        float smoothedHeadWeight;
        Vector3 smoothedHeadTargetWorld;
        Vector3 smoothedTorsoTiltEuler;

        // Motion & external context fed by PlayerController
        Vector3 currentPlanarVel;
        float currentYawRate;
        float currentObstacleLean;
        bool isGrounded = true;

        // Debug readouts
        public bool LeftFootGrounded { get; private set; }
        public bool RightFootGrounded { get; private set; }
        public RaycastHit LeftFootHit { get; private set; }
        public RaycastHit RightFootHit { get; private set; }
        public float LeftLegWeight => leftLegIK != null ? leftLegIK.weight : 0f;
        public float RightLegWeight => rightLegIK != null ? rightLegIK.weight : 0f;
        public float LeftArmWeight => leftArmIK != null ? leftArmIK.weight : 0f;
        public float RightArmWeight => rightArmIK != null ? rightArmIK.weight : 0f;
        public float HeadAimWeight => headAimConstraint != null ? headAimConstraint.weight : 0f;
        public float PelvisOffsetY => smoothedPelvisOffset;
        public Vector3 LeftFootTargetPos => leftFootIKTarget != null ? leftFootIKTarget.position : Vector3.zero;
        public Vector3 RightFootTargetPos => rightFootIKTarget != null ? rightFootIKTarget.position : Vector3.zero;
        public Vector3 LeftKneeHintPos => leftKneeHint != null ? leftKneeHint.position : Vector3.zero;
        public Vector3 RightKneeHintPos => rightKneeHint != null ? rightKneeHint.position : Vector3.zero;
        public Vector3 LeftElbowHintPos => leftElbowHint != null ? leftElbowHint.position : Vector3.zero;
        public Vector3 RightElbowHintPos => rightElbowHint != null ? rightElbowHint.position : Vector3.zero;
        public Vector3 HeadLookPos => headLookTarget != null ? headLookTarget.position : Vector3.zero;
        public Animator Animator => animator;

        void Awake()
        {
            ResolveReferences();
            CacheBones();
            if (headBone != null)
            {
                smoothedHeadTargetWorld = headBone.position + transform.forward * 3.5f;
            }
        }

        public void ResolveReferences()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (rigBuilder == null) rigBuilder = GetComponentInChildren<RigBuilder>();

            if (rigBuilder != null && rigBuilder.layers != null)
            {
                if (lowerBodyRig == null && rigBuilder.layers.Count > 0) lowerBodyRig = rigBuilder.layers[0].rig;
                if (upperBodyRig == null && rigBuilder.layers.Count > 1) upperBodyRig = rigBuilder.layers[1].rig;
            }

            if (lowerBodyRig != null)
            {
                pelvisOffsetConstraint ??= FindByName<OverrideTransform>(lowerBodyRig.transform, "PelvisOffsetConstraint");
                leftLegIK ??= FindByName<TwoBoneIKConstraint>(lowerBodyRig.transform, "LeftLegIK");
                rightLegIK ??= FindByName<TwoBoneIKConstraint>(lowerBodyRig.transform, "RightLegIK");
                pelvisOffsetTarget ??= pelvisOffsetConstraint != null ? pelvisOffsetConstraint.data.sourceObject : null;
                leftFootIKTarget ??= leftLegIK != null ? leftLegIK.data.target : null;
                rightFootIKTarget ??= rightLegIK != null ? rightLegIK.data.target : null;
                leftKneeHint ??= leftLegIK != null ? leftLegIK.data.hint : null;
                rightKneeHint ??= rightLegIK != null ? rightLegIK.data.hint : null;
            }

            if (upperBodyRig != null)
            {
                torsoTiltConstraint ??= FindByName<OverrideTransform>(upperBodyRig.transform, "TorsoTiltConstraint");
                headAimConstraint ??= FindByName<MultiAimConstraint>(upperBodyRig.transform, "HeadLookAim");
                leftArmIK ??= FindByName<TwoBoneIKConstraint>(upperBodyRig.transform, "LeftArmIK");
                rightArmIK ??= FindByName<TwoBoneIKConstraint>(upperBodyRig.transform, "RightArmIK");
                torsoTiltTarget ??= torsoTiltConstraint != null ? torsoTiltConstraint.data.sourceObject : null;
                if (headLookTarget == null && headAimConstraint != null && headAimConstraint.data.sourceObjects.Count > 0)
                    headLookTarget = headAimConstraint.data.sourceObjects[0].transform;
                leftHandIKTarget ??= leftArmIK != null ? leftArmIK.data.target : null;
                rightHandIKTarget ??= rightArmIK != null ? rightArmIK.data.target : null;
                leftElbowHint ??= leftArmIK != null ? leftArmIK.data.hint : null;
                rightElbowHint ??= rightArmIK != null ? rightArmIK.data.hint : null;
            }
        }

        static T FindByName<T>(Transform parent, string name) where T : Component
        {
            var t = parent.Find(name);
            return t != null ? t.GetComponent<T>() : null;
        }

        void CacheBones()
        {
            if (animator == null || !animator.isHuman) return;
            hipsBone = animator.GetBoneTransform(HumanBodyBones.Hips);
            upperChestBone = animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest);
            headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            leftUpperLegBone = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            leftLowerLegBone = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            leftFootBone = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightUpperLegBone = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            rightLowerLegBone = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            rightFootBone = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            leftUpperArmBone = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftLowerArmBone = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            leftHandBone = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightUpperArmBone = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightLowerArmBone = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rightHandBone = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        public void SetMotionState(Vector3 planarVelocity, float yawRateDeg, float obstacleLean, bool grounded)
        {
            currentPlanarVel = planarVelocity;
            currentYawRate = yawRateDeg;
            currentObstacleLean = obstacleLean;
            isGrounded = grounded;
        }

        void Update()
        {
            if (animator == null || !animator.isHuman || hipsBone == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0.00001f) return;

            UpdateFootAndPelvisIK(dt);
            UpdateTorsoTilt(dt);
            UpdateHeadAndArmTracking(dt);
        }

        void UpdateFootAndPelvisIK(float dt)
        {
            if (leftFootBone == null || rightFootBone == null) return;

            float clipWeightL = animator.GetFloat(HashLeftFootIK);
            float clipWeightR = animator.GetFloat(HashRightFootIK);

            // Fallback contact estimate if using external clip without foot curves
            if (clipWeightL <= 0.001f && clipWeightR <= 0.001f && currentPlanarVel.sqrMagnitude < 0.05f && isGrounded)
            {
                clipWeightL = 1f;
                clipWeightR = 1f;
            }

            // Raycast ground at both foot XZ positions (ignoring own character colliders)
            bool hitL = RaycastGround(leftFootBone.position, out RaycastHit rayHitL);
            bool hitR = RaycastGround(rightFootBone.position, out RaycastHit rayHitR);

            LeftFootGrounded = hitL && isGrounded;
            RightFootGrounded = hitR && isGrounded;
            LeftFootHit = rayHitL;
            RightFootHit = rayHitR;

            float rootY = transform.position.y;
            float deltaL = hitL ? (rayHitL.point.y - rootY) : 0f;
            float deltaR = hitR ? (rayHitR.point.y - rootY) : 0f;

            // Clamp extreme outliers (e.g. stepping onto a sheer wall)
            deltaL = Mathf.Clamp(deltaL, -maxPelvisDrop, 0.42f);
            deltaR = Mathf.Clamp(deltaR, -maxPelvisDrop, 0.42f);

            // Smooth foot heights & normals
            float kFoot = 1f - Mathf.Exp(-footSmoothSpeed * dt);
            smoothedLeftFootY = Mathf.Lerp(smoothedLeftFootY, deltaL, kFoot);
            smoothedRightFootY = Mathf.Lerp(smoothedRightFootY, deltaR, kFoot);

            Vector3 nL = hitL ? ClampSurfaceNormal(rayHitL.normal, maxFootPitchDeg) : Vector3.up;
            Vector3 nR = hitR ? ClampSurfaceNormal(rayHitR.normal, maxFootPitchDeg) : Vector3.up;
            smoothedLeftNormal = Vector3.Slerp(smoothedLeftNormal, nL, kFoot);
            smoothedRightNormal = Vector3.Slerp(smoothedRightNormal, nR, kFoot);

            // Pelvis drops to the lower foot so the trailing/lower leg stays within anatomical reach
            float desiredPelvisOffset = 0f;
            if (isGrounded && (hitL || hitR))
            {
                float minDelta = Mathf.Min(hitL ? smoothedLeftFootY : 0f, hitR ? smoothedRightFootY : 0f);
                desiredPelvisOffset = Mathf.Clamp(minDelta, -maxPelvisDrop, 0.14f);
            }

            float kPelvis = 1f - Mathf.Exp(-pelvisSmoothSpeed * dt);
            smoothedPelvisOffset = Mathf.Lerp(smoothedPelvisOffset, desiredPelvisOffset, kPelvis);

            if (pelvisOffsetTarget != null && pelvisOffsetConstraint != null)
            {
                // OverrideTransform in Pivot space adds local displacement to Hips
                pelvisOffsetTarget.localPosition = new Vector3(0f, smoothedPelvisOffset, 0f);
                float speedNorm = Mathf.Clamp01(currentPlanarVel.magnitude / 7.5f);
                float hipBank = Mathf.Clamp(-currentYawRate * 0.03f, -4f, 4f) * speedNorm;
                pelvisOffsetTarget.localRotation = Quaternion.Euler(0f, 0f, hipBank);
                pelvisOffsetConstraint.weight = Mathf.Lerp(pelvisOffsetConstraint.weight, isGrounded ? 1f : 0f, kPelvis);
            }

            // Update Left/Right Foot IK Targets & Knee Hints
            UpdateSingleFootTarget(
                leftFootBone, leftFootIKTarget, leftKneeHint, leftLegIK,
                hitL, rayHitL.point.y, smoothedLeftNormal,
                isGrounded ? clipWeightL : 0f, ref smoothedLeftLegWeight, -0.08f, dt);

            UpdateSingleFootTarget(
                rightFootBone, rightFootIKTarget, rightKneeHint, rightLegIK,
                hitR, rayHitR.point.y, smoothedRightNormal,
                isGrounded ? clipWeightR : 0f, ref smoothedRightLegWeight, +0.08f, dt);
        }

        void UpdateSingleFootTarget(
            Transform footBone,
            Transform targetTransform,
            Transform hintTransform,
            TwoBoneIKConstraint ikConstraint,
            bool hasHit,
            float hitY,
            Vector3 surfaceNormal,
            float targetWeight,
            ref float smoothedWeight,
            float kneeOutwardX,
            float dt)
        {
            if (ikConstraint == null || targetTransform == null) return;

            float blendRate = targetWeight > smoothedWeight ? 20f : 14f;
            smoothedWeight = Mathf.Lerp(smoothedWeight, Mathf.Clamp01(targetWeight), 1f - Mathf.Exp(-blendRate * dt));
            ikConstraint.weight = smoothedWeight;
            ikConstraint.data.targetPositionWeight = 1f;
            ikConstraint.data.targetRotationWeight = hasHit ? 0.92f : 0f;
            ikConstraint.data.hintWeight = 0.85f;

            Vector3 footPos = footBone.position;
            float targetY = hasHit ? (hitY + ankleSoleHeight) : footPos.y;
            targetTransform.position = new Vector3(footPos.x, targetY, footPos.z);

            // Preserve character-aligned foot heading tilted onto ground normal
            Vector3 fwd = Vector3.ProjectOnPlane(footBone.forward, surfaceNormal);
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.ProjectOnPlane(transform.forward, surfaceNormal);
            if (fwd.sqrMagnitude < 0.001f) fwd = transform.forward;
            targetTransform.rotation = Quaternion.LookRotation(fwd.normalized, surfaceNormal);

            if (hintTransform != null && hipsBone != null)
            {
                // Place knee hint cleanly forward (+Z) and slightly outward so knees always bend naturally
                Vector3 kneeMid = (hipsBone.position + targetTransform.position) * 0.5f;
                hintTransform.position = kneeMid + transform.forward * 0.48f + transform.right * kneeOutwardX;
            }
        }

        void UpdateTorsoTilt(float dt)
        {
            if (torsoTiltConstraint == null || torsoTiltTarget == null) return;

            float speed = currentPlanarVel.magnitude;
            float speedNorm = Mathf.Clamp01(speed / 7.5f);
            float fwdDot = Vector3.Dot(currentPlanarVel, transform.forward) / Mathf.Max(0.1f, speed);

            float accelPitch = speedNorm * 4.5f * Mathf.Clamp(fwdDot, -0.5f, 1f);
            float turnBank = Mathf.Clamp(-currentYawRate * 0.045f, -9f, 9f) * Mathf.Clamp01(speed / 2.2f);
            float avoidRoll = Mathf.Clamp(currentObstacleLean * 8f, -7f, 7f);

            Vector3 desiredEuler = new Vector3(accelPitch, 0f, turnBank + avoidRoll);
            smoothedTorsoTiltEuler = Vector3.Lerp(smoothedTorsoTiltEuler, desiredEuler, 1f - Mathf.Exp(-9f * dt));

            torsoTiltTarget.localPosition = Vector3.zero;
            torsoTiltTarget.localRotation = Quaternion.Euler(smoothedTorsoTiltEuler);
            torsoTiltConstraint.weight = isGrounded ? 0.72f : 0.25f;
        }

        void UpdateHeadAndArmTracking(float dt)
        {
            // Keep elbow hints consistently behind/outward from chest so arm IK bends naturally
            if (leftElbowHint != null && upperChestBone != null)
                leftElbowHint.position = upperChestBone.position - transform.forward * 0.42f - transform.right * 0.48f - Vector3.up * 0.14f;
            if (rightElbowHint != null && upperChestBone != null)
                rightElbowHint.position = upperChestBone.position - transform.forward * 0.42f + transform.right * 0.48f - Vector3.up * 0.14f;

            Vector3 origin = headBone != null ? headBone.position : (transform.position + Vector3.up * 1.62f);

            // Scan for nearby interactables / pickups / cones within front cone
            Transform bestPoi = FindBestPointOfInterest(origin, out float bestDist);

            Vector3 desiredLookWorld;
            float desiredHeadWeight;
            if (bestPoi != null)
            {
                desiredLookWorld = bestPoi.position + Vector3.up * 0.35f;
                desiredHeadWeight = Mathf.Lerp(0.88f, 0.35f, Mathf.Clamp01(bestDist / poiScanRadius));
            }
            else if (currentPlanarVel.sqrMagnitude > 0.25f)
            {
                // Look naturally ahead into movement direction
                desiredLookWorld = origin + currentPlanarVel.normalized * 4.0f + Vector3.up * 0.05f;
                desiredHeadWeight = 0.55f;
            }
            else if (Camera.main != null)
            {
                Vector3 camFwd = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up);
                float dot = Vector3.Dot(transform.forward, camFwd.normalized);
                if (dot > 0.2f)
                {
                    desiredLookWorld = origin + camFwd.normalized * 4.0f;
                    desiredHeadWeight = 0.45f;
                }
                else
                {
                    desiredLookWorld = origin + transform.forward * 4.0f;
                    desiredHeadWeight = 0.2f;
                }
            }
            else
            {
                desiredLookWorld = origin + transform.forward * 4.0f;
                desiredHeadWeight = 0.2f;
            }

            smoothedHeadTargetWorld = Vector3.Lerp(smoothedHeadTargetWorld, desiredLookWorld, 1f - Mathf.Exp(-headAimSmoothSpeed * dt));
            smoothedHeadWeight = Mathf.Lerp(smoothedHeadWeight, desiredHeadWeight, 1f - Mathf.Exp(-headAimSmoothSpeed * dt));

            if (headLookTarget != null && headAimConstraint != null)
            {
                headLookTarget.position = smoothedHeadTargetWorld;
                headAimConstraint.weight = smoothedHeadWeight;
            }

            // Natural hand positioning during interactions (reaching toward nearby pickup / interactable when close & slow)
            bool interactHeld = Keyboard.current != null && Keyboard.current.eKey.isPressed;
            bool closeForReach = bestPoi != null && bestDist <= interactReachDistance && currentPlanarVel.magnitude < 3.6f && isGrounded;
            bool reachLeft = false;
            bool reachRight = false;

            if (closeForReach)
            {
                Vector3 localTarget = transform.InverseTransformPoint(bestPoi.position);
                if (localTarget.z > -0.1f)
                {
                    if (localTarget.x < 0f) reachLeft = true;
                    else reachRight = true;
                }
            }
            else if (interactHeld && isGrounded)
            {
                reachRight = true;
            }

            float targetW_L = reachLeft ? 0.85f : 0f;
            float targetW_R = reachRight ? 0.85f : 0f;

            smoothedLeftArmWeight = Mathf.Lerp(smoothedLeftArmWeight, targetW_L, 1f - Mathf.Exp(-handIKBlendSpeed * dt));
            smoothedRightArmWeight = Mathf.Lerp(smoothedRightArmWeight, targetW_R, 1f - Mathf.Exp(-handIKBlendSpeed * dt));

            if (leftArmIK != null && leftHandIKTarget != null)
            {
                leftArmIK.weight = smoothedLeftArmWeight;
                if (bestPoi != null && reachLeft)
                {
                    leftHandIKTarget.position = bestPoi.position + Vector3.up * 0.25f;
                    leftHandIKTarget.rotation = Quaternion.LookRotation((bestPoi.position - origin).normalized, Vector3.up);
                }
                else if (leftHandBone != null)
                {
                    leftHandIKTarget.position = leftHandBone.position;
                }
            }

            if (rightArmIK != null && rightHandIKTarget != null)
            {
                rightArmIK.weight = smoothedRightArmWeight;
                if (bestPoi != null && reachRight)
                {
                    rightHandIKTarget.position = bestPoi.position + Vector3.up * 0.25f;
                    rightHandIKTarget.rotation = Quaternion.LookRotation((bestPoi.position - origin).normalized, Vector3.up);
                }
                else if (interactHeld)
                {
                    rightHandIKTarget.position = transform.TransformPoint(new Vector3(0.24f, 1.15f, 0.48f));
                    rightHandIKTarget.rotation = transform.rotation;
                }
                else if (rightHandBone != null)
                {
                    rightHandIKTarget.position = rightHandBone.position;
                }
            }
        }

        Transform FindBestPointOfInterest(Vector3 headOrigin, out float bestDistance)
        {
            bestDistance = float.MaxValue;
            Transform best = null;
            Collider[] hits = Physics.OverlapSphere(transform.position + Vector3.up * 0.9f, poiScanRadius, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i];
                if (col == null || col.transform.IsChildOf(transform)) continue;

                // Prioritize IntelPickups, tagged Interactables, or named Cones/Obstacles
                string n = col.name;
                bool isPoi = col.GetComponent<IntelPickup>() != null ||
                             n.Contains("Interact") ||
                             n.Contains("Cone") ||
                             n.Contains("Stall") ||
                             n.Contains("CNG");
                if (!isPoi) continue;

                Vector3 toObj = col.bounds.center - headOrigin;
                toObj.y = 0f;
                float dist = toObj.magnitude;
                if (dist < 0.2f || dist > poiScanRadius) continue;

                float dot = Vector3.Dot(transform.forward, toObj / dist);
                if (dot < 0.1f) continue; // Only track things roughly in front hemisphere

                float scoreDist = dist * (col.GetComponent<IntelPickup>() != null ? 0.55f : 1.0f);
                if (scoreDist < bestDistance)
                {
                    bestDistance = dist;
                    best = col.transform;
                }
            }
            return best;
        }

        bool RaycastGround(Vector3 footWorldPos, out RaycastHit bestHit)
        {
            bestHit = default;
            Vector3 origin = new Vector3(footWorldPos.x, transform.position.y + raycastUpOffset, footWorldPos.z);
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, raycastDistance, groundLayerMask, QueryTriggerInteraction.Ignore);
            float closest = float.MaxValue;
            bool found = false;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider == null || hits[i].collider.transform.IsChildOf(transform)) continue;
                if (hits[i].distance < closest)
                {
                    closest = hits[i].distance;
                    bestHit = hits[i];
                    found = true;
                }
            }
            return found;
        }

        static Vector3 ClampSurfaceNormal(Vector3 normal, float maxDeg)
        {
            float angle = Vector3.Angle(Vector3.up, normal);
            if (angle <= maxDeg) return normal;
            return Vector3.Slerp(Vector3.up, normal, maxDeg / Mathf.Max(0.01f, angle)).normalized;
        }

        /// <summary>
        /// Collects all core humanoid bone transforms for F3 skeleton visualization.
        /// </summary>
        public Transform[] GetSkeletonBones()
        {
            if (animator == null) return System.Array.Empty<Transform>();
            var list = new System.Collections.Generic.List<Transform>(24);
            HumanBodyBones[] ids =
            {
                HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest,
                HumanBodyBones.Neck, HumanBodyBones.Head,
                HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
                HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
                HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes,
                HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightToes
            };
            for (int i = 0; i < ids.Length; i++)
            {
                var t = animator.GetBoneTransform(ids[i]);
                if (t != null) list.Add(t);
            }
            return list.ToArray();
        }
    }
}
