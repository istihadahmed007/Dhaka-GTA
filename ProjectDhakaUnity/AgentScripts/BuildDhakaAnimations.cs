using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class BuildDhakaAnimations
{
    const string AnimDir = "Assets/Characters/DhakaProtagonist/Animations";
    const string PrefabPath = "Assets/Prefabs/DhakaProtagonist.prefab";
    const string ControllerPath = AnimDir + "/DhakaHumanoidLocomotion.controller";
    const string UpperMaskPath = AnimDir + "/DhakaUpperBodyMask.mask";

    // Skeleton dimensions matching BuildDhakaCharacterAssets
    const float HipDefaultY = 0.96f;
    const float ThighLen = 0.44f;
    const float ShinLen = 0.43f;
    const float AnkleSoleHeight = 0.088f; // Distance from Foot bone down to shoe sole at y=0
    const float HipHalfWidth = 0.102f;

    struct PoseFrame
    {
        public Vector3 hipOffset;      // Relative to (0, HipDefaultY, 0)
        public Vector3 hipEuler;       // Pelvis orientation
        public Vector3 spineEuler;
        public Vector3 chestEuler;
        public Vector3 upperChestEuler;
        public Vector3 neckEuler;
        public Vector3 headEuler;

        public Vector3 leftAnkleTarget;   // Character-local ankle target position
        public float leftFootPitch;       // Degrees (+ = toes up heel strike, - = toes down push-off)
        public float leftToePitch;        // Counter-flex toes
        public float leftFootYaw;

        public Vector3 rightAnkleTarget;
        public float rightFootPitch;
        public float rightToePitch;
        public float rightFootYaw;

        public Vector3 leftArmEuler;      // Relative to dropped-arm neutral (0,0,75) -> pitch X, yaw Y, roll Z
        public float leftElbowFlex;       // Degrees (>=0 flexes forearm forward/up)
        public Vector3 rightArmEuler;
        public float rightElbowFlex;

        public float leftFootIKWeight;    // 1 during ground stance, 0 during swing/airborne
        public float rightFootIKWeight;
    }

    struct BoneRefs
    {
        public Transform root, hips, spine, chest, upperChest, neck, head;
        public Transform lSh, lUa, lLa, lHa;
        public Transform rSh, rUa, rLa, rHa;
        public Transform lUl, lLl, lFt, lTo;
        public Transform rUl, rLl, rFt, rTo;
        public Dictionary<Transform, (Vector3 pos, Quaternion rot)> bindPose;
    }

    public static string BuildAll()
    {
        if (!AssetDatabase.IsValidFolder(AnimDir))
        {
            AssetDatabase.CreateFolder("Assets/Characters/DhakaProtagonist", "Animations");
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) throw new Exception($"Missing prefab at {PrefabPath}");

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        try
        {
            var anim = instance.GetComponent<Animator>();
            Avatar avatar = anim.avatar;
            var bones = CollectBones(instance.transform);
            var handler = new HumanPoseHandler(avatar, instance.transform);

            var clips = new Dictionary<string, AnimationClip>();

            // 1. Idle (3.2s smooth breathing & weight shift)
            clips["Idle_Loop"] = BakeClip("Idle_Loop", 3.2f, 30, true, bones, handler, u => EvalIdle(u));

            // 2. Walk Directional Suite (1.65 m/s fwd, -1.25 back, ±1.45 strafe)
            clips["Walk_Fwd"] = BakeClip("Walk_Fwd", 0.96f, 30, true, bones, handler, u => EvalGait(u, 0f, 1.65f, 0.78f, 0.085f, 2.5f, 28f, 24f));
            clips["Walk_Back"] = BakeClip("Walk_Back", 0.96f, 30, true, bones, handler, u => EvalGait(u, Mathf.PI, 1.25f, 0.58f, 0.075f, -3.5f, 22f, 20f));
            clips["Walk_Strafe_L"] = BakeClip("Walk_Strafe_L", 0.88f, 30, true, bones, handler, u => EvalGait(u, -Mathf.PI * 0.5f, 1.45f, 0.54f, 0.08f, 1.5f, 24f, 18f));
            clips["Walk_Strafe_R"] = BakeClip("Walk_Strafe_R", 0.88f, 30, true, bones, handler, u => EvalGait(u, Mathf.PI * 0.5f, 1.45f, 0.54f, 0.08f, 1.5f, 24f, 18f));

            // 3. Jog Directional Suite (4.2 m/s fwd, -2.8 back, ±3.5 strafe)
            clips["Jog_Fwd"] = BakeClip("Jog_Fwd", 0.68f, 32, true, bones, handler, u => EvalGait(u, 0f, 4.2f, 1.38f, 0.16f, 10.5f, 68f, 44f));
            clips["Jog_Back"] = BakeClip("Jog_Back", 0.72f, 32, true, bones, handler, u => EvalGait(u, Mathf.PI, 2.8f, 0.92f, 0.13f, -5.5f, 52f, 32f));
            clips["Jog_Strafe_L"] = BakeClip("Jog_Strafe_L", 0.68f, 32, true, bones, handler, u => EvalGait(u, -Mathf.PI * 0.5f, 3.5f, 0.96f, 0.14f, 5.0f, 58f, 34f));
            clips["Jog_Strafe_R"] = BakeClip("Jog_Strafe_R", 0.68f, 32, true, bones, handler, u => EvalGait(u, Mathf.PI * 0.5f, 3.5f, 0.96f, 0.14f, 5.0f, 58f, 34f));

            // 4. Sprint Forward (7.4 m/s athletic sprint)
            clips["Sprint_Fwd"] = BakeClip("Sprint_Fwd", 0.54f, 36, true, bones, handler, u => EvalGait(u, 0f, 7.4f, 1.98f, 0.24f, 17.0f, 94f, 64f));

            // 5. Inertial Start & Stop
            clips["Start_Move"] = BakeClip("Start_Move", 0.42f, 30, false, bones, handler, u => EvalStart(u));
            clips["Stop_Move"] = BakeClip("Stop_Move", 0.46f, 30, false, bones, handler, u => EvalStop(u));

            // 6. 90° and 180° Direction Turns
            clips["Turn_90_L"] = BakeClip("Turn_90_L", 0.46f, 30, false, bones, handler, u => EvalTurn(u, -90f));
            clips["Turn_90_R"] = BakeClip("Turn_90_R", 0.46f, 30, false, bones, handler, u => EvalTurn(u, +90f));
            clips["Turn_180_L"] = BakeClip("Turn_180_L", 0.64f, 30, false, bones, handler, u => EvalTurn(u, -180f));
            clips["Turn_180_R"] = BakeClip("Turn_180_R", 0.64f, 30, false, bones, handler, u => EvalTurn(u, +180f));

            // 7. Jump, Fall, Land & Recovery
            clips["Jump_Start"] = BakeClip("Jump_Start", 0.36f, 30, false, bones, handler, u => EvalJump(u));
            clips["Fall_Loop"] = BakeClip("Fall_Loop", 0.60f, 30, true, bones, handler, u => EvalFall(u));
            clips["Land_Recovery"] = BakeClip("Land_Recovery", 0.44f, 30, false, bones, handler, u => EvalLand(u));

            // Build Upper Body AvatarMask & Animator Controller
            var upperMask = CreateUpperBodyMask();
            var controller = BuildAnimatorController(clips, upperMask);

            // Assign controller onto prefab
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            prefabAsset.GetComponent<Animator>().runtimeAnimatorController = controller;
            EditorUtility.SetDirty(prefabAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Verify sample on Idle_Loop
            clips["Idle_Loop"].SampleAnimation(instance, 0f);
            float hipsY = bones.hips.position.y;
            float lFootY = bones.lFt.position.y;
            return $"Generated {clips.Count} Humanoid .anim clips & Controller at {ControllerPath} | Idle isHuman={clips["Idle_Loop"].isHumanMotion} Walk isHuman={clips["Walk_Fwd"].isHumanMotion} | Sample HipsY={hipsY:F3} LFootY={lFootY:F3}";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    static BoneRefs CollectBones(Transform root)
    {
        Transform Find(string n)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == n) return t;
            throw new Exception($"Bone {n} not found");
        }

        var refs = new BoneRefs
        {
            root = root,
            hips = Find("Hips"),
            spine = Find("Spine"),
            chest = Find("Chest"),
            upperChest = Find("UpperChest"),
            neck = Find("Neck"),
            head = Find("Head"),
            lSh = Find("LeftShoulder"),
            lUa = Find("LeftUpperArm"),
            lLa = Find("LeftLowerArm"),
            lHa = Find("LeftHand"),
            rSh = Find("RightShoulder"),
            rUa = Find("RightUpperArm"),
            rLa = Find("RightLowerArm"),
            rHa = Find("RightHand"),
            lUl = Find("LeftUpperLeg"),
            lLl = Find("LeftLowerLeg"),
            lFt = Find("LeftFoot"),
            lTo = Find("LeftToes"),
            rUl = Find("RightUpperLeg"),
            rLl = Find("RightLowerLeg"),
            rFt = Find("RightFoot"),
            rTo = Find("RightToes"),
            bindPose = new Dictionary<Transform, (Vector3, Quaternion)>()
        };

        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            refs.bindPose[t] = (t.localPosition, t.localRotation);
        }
        return refs;
    }

    static void ResetBindPose(BoneRefs b)
    {
        foreach (var kv in b.bindPose)
        {
            kv.Key.localPosition = kv.Value.pos;
            kv.Key.localRotation = kv.Value.rot;
        }
    }

    #region Biomechanical Pose Evaluators

    static PoseFrame BaseStandingPose()
    {
        return new PoseFrame
        {
            hipOffset = Vector3.zero,
            leftAnkleTarget = new Vector3(-HipHalfWidth, AnkleSoleHeight, 0f),
            rightAnkleTarget = new Vector3(+HipHalfWidth, AnkleSoleHeight, 0f),
            leftArmEuler = new Vector3(4f, 0f, -5f),   // Slight natural relaxed hang away from body
            leftElbowFlex = 14f,
            rightArmEuler = new Vector3(4f, 0f, 5f),
            rightElbowFlex = 14f,
            leftFootIKWeight = 1f,
            rightFootIKWeight = 1f
        };
    }

    static PoseFrame EvalIdle(float u)
    {
        float p = u * Mathf.PI * 2f;
        float breathe = Mathf.Sin(p);
        float slowShift = Mathf.Sin(p * 0.5f + 0.4f);
        float f = Mathf.Sin(p * 2f);

        var pose = BaseStandingPose();
        pose.hipOffset = new Vector3(slowShift * 0.012f, breathe * 0.004f - 0.012f, 0f);
        pose.hipEuler = new Vector3(0.5f, slowShift * 2.5f, slowShift * -1.4f);
        pose.spineEuler = new Vector3(-breathe * 1.4f, -slowShift * 1.2f, slowShift * 0.8f);
        pose.chestEuler = new Vector3(-breathe * 1.8f, -slowShift * 1.0f, 0f);
        pose.upperChestEuler = new Vector3(-breathe * 1.2f, 0f, 0f);
        pose.neckEuler = new Vector3(breathe * 0.8f, slowShift * 2.5f, 0f);
        pose.headEuler = new Vector3(breathe * 1.0f, Mathf.Sin(p) * 3.5f, Mathf.Cos(p) * 1.0f);

        // Relaxed stance with right foot slightly set for realistic GTA street stance
        pose.leftAnkleTarget = new Vector3(-HipHalfWidth - 0.014f, AnkleSoleHeight, 0.03f);
        pose.rightAnkleTarget = new Vector3(+HipHalfWidth + 0.014f, AnkleSoleHeight, -0.04f);
        pose.leftFootYaw = -4f;
        pose.rightFootYaw = 7f;

        pose.leftArmEuler = new Vector3(3f + breathe * 1.4f, 0f, -1.5f - breathe * 0.6f);
        pose.leftElbowFlex = 14f + f * 1.8f;
        pose.rightArmEuler = new Vector3(4f - breathe * 1.2f, 0f, 1.5f + breathe * 0.6f);
        pose.rightElbowFlex = 16f - f * 1.8f;
        return pose;
    }

    /// <summary>
    /// Evaluates directional human gait (Walk, Jog, Sprint, Backward, Strafe) with anti-slide flat-foot stance.
    /// moveAngleRad = 0 (fwd), PI (back), -PI/2 (left strafe), +PI/2 (right strafe).
    /// </summary>
    static PoseFrame EvalGait(float u, float moveAngleRad, float speedMps, float strideLen, float swingClearance, float forwardLeanDeg, float elbowBaseDeg, float armSwingDeg)
    {
        float phi = u * Mathf.PI * 2f;
        float speedNorm = Mathf.Clamp01(speedMps / 7.5f);
        Vector3 moveDir = new Vector3(Mathf.Sin(moveAngleRad), 0f, Mathf.Cos(moveAngleRad));
        float fwdFactor = moveDir.z;
        float rightFactor = moveDir.x;

        var pose = BaseStandingPose();

        // Double-frequency vertical COM bob (lowest during double support/mid-stance drop, highest at passing/flight)
        float bobAmp = Mathf.Lerp(0.022f, 0.048f, speedNorm);
        float dropBias = Mathf.Lerp(0.025f, 0.068f, speedNorm);
        float hipBob = -Mathf.Cos(phi * 2f) * bobAmp - dropBias;

        // Lateral weight transfer toward stance leg!
        float latSway = Mathf.Sin(phi) * Mathf.Lerp(0.028f, 0.018f, speedNorm);
        pose.hipOffset = new Vector3(latSway, hipBob, 0f);

        // Pelvis yaw/roll/pitch
        float pelvisYaw = Mathf.Sin(phi) * Mathf.Lerp(6.5f, 11f, speedNorm) * (Mathf.Abs(fwdFactor) > 0.3f ? Mathf.Sign(fwdFactor) : 0.4f);
        float pelvisRoll = -Mathf.Sin(phi) * Mathf.Lerp(3.2f, 4.5f, speedNorm);
        pose.hipEuler = new Vector3(forwardLeanDeg * 0.45f * fwdFactor, pelvisYaw, pelvisRoll + rightFactor * 3f);

        // Spine & UpperBody counter-rotation (natural human torque cancellation!)
        pose.spineEuler = new Vector3(forwardLeanDeg * 0.30f * fwdFactor, -pelvisYaw * 0.45f, -pelvisRoll * 0.4f + rightFactor * 2f);
        pose.chestEuler = new Vector3(forwardLeanDeg * 0.25f * fwdFactor, -pelvisYaw * 0.55f, -pelvisRoll * 0.35f);
        pose.upperChestEuler = new Vector3(forwardLeanDeg * 0.15f * fwdFactor, -pelvisYaw * 0.35f, 0f);

        // Head counter-stabilizes pitch/yaw to keep horizon steady
        pose.neckEuler = new Vector3(-forwardLeanDeg * 0.45f * fwdFactor, pelvisYaw * 0.35f, 0f);
        pose.headEuler = new Vector3(-forwardLeanDeg * 0.45f * fwdFactor, pelvisYaw * 0.15f + rightFactor * 6f, -pelvisRoll * 0.25f);

        // Compute Left (phase = phi) and Right (phase = phi + PI) foot targets in character-local space
        ComputeFootPhase(u, moveDir, strideLen, swingClearance, speedNorm, -HipHalfWidth,
            out pose.leftAnkleTarget, out pose.leftFootPitch, out pose.leftToePitch, out pose.leftFootIKWeight);
        ComputeFootPhase((u + 0.5f) % 1f, moveDir, strideLen, swingClearance, speedNorm, +HipHalfWidth,
            out pose.rightAnkleTarget, out pose.rightFootPitch, out pose.rightToePitch, out pose.rightFootIKWeight);

        // Counter-swinging arms (Left arm advances with Right leg -> opposite phase!)
        float armPhaseL = Mathf.Cos(phi);  // +1 when right leg advances
        float armPhaseR = -armPhaseL;
        float armFwdBias = Mathf.Lerp(2f, 10f, speedNorm);

        pose.leftArmEuler = new Vector3(armPhaseL * armSwingDeg + armFwdBias, armPhaseL * 6f, -6f - speedNorm * 6f);
        pose.leftElbowFlex = elbowBaseDeg + Mathf.Max(0f, armPhaseL) * 16f;

        pose.rightArmEuler = new Vector3(armPhaseR * armSwingDeg + armFwdBias, -armPhaseR * 6f, 6f + speedNorm * 6f);
        pose.rightElbowFlex = elbowBaseDeg + Mathf.Max(0f, armPhaseR) * 16f;

        return pose;
    }

    static void ComputeFootPhase(
        float cycleU,
        Vector3 moveDir,
        float strideLen,
        float swingClearance,
        float speedNorm,
        float baseX,
        out Vector3 anklePos,
        out float footPitchDeg,
        out float toePitchDeg,
        out float ikWeight)
    {
        // Stance occupies [0, 0.54] for walk, tapering to [0, 0.44] for sprint
        float stanceFrac = Mathf.Lerp(0.56f, 0.44f, speedNorm);
        float halfStride = strideLen * 0.5f;

        // Lateral widening slightly for strafes to avoid self-intersection
        float lateralBase = baseX * (1f + Mathf.Abs(moveDir.x) * 0.15f);

        if (cycleU < stanceFrac)
        {
            // STANCE PHASE: Foot sweeps linearly backward at exact ground speed while locked to y = AnkleSoleHeight!
            float s = cycleU / stanceFrac;
            float along = Mathf.Lerp(+halfStride, -halfStride, s);
            anklePos = new Vector3(lateralBase, AnkleSoleHeight, 0f) + moveDir * along;
            ikWeight = s < 0.1f ? Mathf.SmoothStep(0.3f, 1f, s / 0.1f) : (s > 0.85f ? Mathf.SmoothStep(1f, 0.2f, (s - 0.85f) / 0.15f) : 1f);

            if (s < 0.16f && moveDir.z >= -0.2f)
            {
                // Heel strike roll down
                float hs = s / 0.16f;
                footPitchDeg = Mathf.Lerp(16f, 0f, hs);
                toePitchDeg = 0f;
            }
            else if (s > 0.72f && moveDir.z >= -0.2f)
            {
                // Heel lift & toe push-off!
                float push = (s - 0.72f) / 0.28f;
                footPitchDeg = Mathf.Lerp(0f, -30f * (0.6f + 0.4f * speedNorm), push);
                toePitchDeg = -footPitchDeg * 0.9f; // Toes stay planted flat on ground!
                anklePos.y += Mathf.Sin(push * Mathf.PI * 0.5f) * 0.045f;
            }
            else
            {
                footPitchDeg = 0f;
                toePitchDeg = 0f;
            }
        }
        else
        {
            // SWING PHASE: Smooth ground clearance arc forward
            float w = (cycleU - stanceFrac) / (1f - stanceFrac);
            float smoothW = w * w * (3f - 2f * w);
            float along = Mathf.Lerp(-halfStride, +halfStride, smoothW);
            float lift = Mathf.Sin(w * Mathf.PI) * swingClearance;
            anklePos = new Vector3(lateralBase, AnkleSoleHeight + lift, 0f) + moveDir * along;
            ikWeight = w > 0.88f ? Mathf.SmoothStep(0f, 0.3f, (w - 0.88f) / 0.12f) : 0f;

            // Slight trailing ankle plantarflexion -> dorsiflexion before heel strike
            footPitchDeg = Mathf.Lerp(-24f, 15f, smoothW) * (moveDir.z >= -0.2f ? 1f : -0.5f);
            toePitchDeg = Mathf.Lerp(14f, 0f, smoothW);
        }
    }

    static PoseFrame EvalStart(float u)
    {
        float s = u * u * (3f - 2f * u);
        var pose = BaseStandingPose();
        // Dip hips & pitch chest forward into athletic acceleration, push off left leg, drive right knee forward
        pose.hipOffset = new Vector3(Mathf.Sin(u * Mathf.PI) * 0.02f, -Mathf.Sin(u * Mathf.PI) * 0.055f - 0.02f * s, 0f);
        pose.hipEuler = new Vector3(12f * Mathf.Sin(u * Mathf.PI * 0.85f), -6f * s, 0f);
        pose.spineEuler = new Vector3(10f * Mathf.Sin(u * Mathf.PI * 0.8f), 4f * s, 0f);
        pose.chestEuler = new Vector3(6f * Mathf.Sin(u * Mathf.PI * 0.8f), 4f * s, 0f);
        pose.headEuler = new Vector3(-12f * Mathf.Sin(u * Mathf.PI * 0.8f), 0f, 0f);

        pose.leftAnkleTarget = new Vector3(-HipHalfWidth, AnkleSoleHeight + 0.03f * s, Mathf.Lerp(0.02f, -0.34f, s));
        pose.leftFootPitch = -24f * s;
        pose.leftToePitch = 20f * s;

        pose.rightAnkleTarget = new Vector3(+HipHalfWidth, AnkleSoleHeight + Mathf.Sin(u * Mathf.PI) * 0.14f, Mathf.Lerp(-0.02f, 0.36f, s));
        pose.rightFootPitch = Mathf.Lerp(0f, 12f, s);

        pose.leftArmEuler = new Vector3(Mathf.Lerp(4f, 38f, s), 0f, -8f);
        pose.leftElbowFlex = Mathf.Lerp(14f, 52f, s);
        pose.rightArmEuler = new Vector3(Mathf.Lerp(4f, -34f, s), 0f, 8f);
        pose.rightElbowFlex = Mathf.Lerp(14f, 44f, s);
        return pose;
    }

    static PoseFrame EvalStop(float u)
    {
        float s = u * u * (3f - 2f * u);
        float brake = Mathf.Sin(u * Mathf.PI);
        var pose = BaseStandingPose();
        // Plant lead foot ahead, absorb momentum by dipping hips & pitching torso back slightly, then settle to neutral
        pose.hipOffset = new Vector3(0f, -brake * 0.065f, 0f);
        pose.hipEuler = new Vector3(-8f * brake, 0f, 0f);
        pose.spineEuler = new Vector3(-7f * brake, 0f, 0f);
        pose.chestEuler = new Vector3(-4f * brake, 0f, 0f);
        pose.headEuler = new Vector3(9f * brake, 0f, 0f);

        pose.rightAnkleTarget = new Vector3(+HipHalfWidth, AnkleSoleHeight, Mathf.Lerp(0.32f, -0.03f, s));
        pose.rightFootPitch = Mathf.Lerp(18f, 0f, s);

        pose.leftAnkleTarget = new Vector3(-HipHalfWidth, AnkleSoleHeight + brake * 0.07f, Mathf.Lerp(-0.26f, 0.03f, s));
        pose.leftFootPitch = Mathf.Lerp(-16f, 0f, s);

        pose.leftArmEuler = new Vector3(Mathf.Lerp(-26f, 4f, s), 0f, -6f - brake * 8f);
        pose.leftElbowFlex = Mathf.Lerp(42f, 15f, s);
        pose.rightArmEuler = new Vector3(Mathf.Lerp(28f, 4f, s), 0f, 6f + brake * 8f);
        pose.rightElbowFlex = Mathf.Lerp(38f, 16f, s);
        return pose;
    }

    static PoseFrame EvalTurn(float u, float angleDeg)
    {
        // Step-pivot turn: head & upper torso anticipate target turn direction, feet lift and plant cleanly
        float dir = Mathf.Sign(angleDeg);
        float mag = Mathf.Abs(angleDeg) / 180f; // 0.5 for 90°, 1.0 for 180°
        float bell = Mathf.Sin(u * Mathf.PI);
        float step1 = Mathf.Clamp01(u * 2f);
        float step2 = Mathf.Clamp01((u - 0.35f) / 0.65f);

        var pose = BaseStandingPose();
        pose.hipOffset = new Vector3(dir * bell * 0.03f, -bell * (0.045f + 0.025f * mag), 0f);
        pose.hipEuler = new Vector3(4f * bell, dir * bell * 16f * mag, -dir * bell * 5f);
        pose.spineEuler = new Vector3(3f * bell, dir * bell * 20f * mag, 0f);
        pose.chestEuler = new Vector3(2f * bell, dir * bell * 22f * mag, 0f);
        pose.headEuler = new Vector3(0f, dir * bell * 28f * mag, 0f);

        // Stepping feet alternate lift
        float liftL = Mathf.Sin(step1 * Mathf.PI) * (0.10f + 0.04f * mag);
        float liftR = Mathf.Sin(step2 * Mathf.PI) * (0.10f + 0.04f * mag);
        pose.leftAnkleTarget = new Vector3(-HipHalfWidth - 0.03f * bell, AnkleSoleHeight + liftL, dir * -0.10f * Mathf.Sin(step1 * Mathf.PI));
        pose.rightAnkleTarget = new Vector3(+HipHalfWidth + 0.03f * bell, AnkleSoleHeight + liftR, dir * 0.10f * Mathf.Sin(step2 * Mathf.PI));
        pose.leftFootYaw = dir * bell * 22f;
        pose.rightFootYaw = dir * bell * 22f;

        pose.leftArmEuler = new Vector3(8f + dir * bell * 18f, 0f, -10f - bell * 8f);
        pose.leftElbowFlex = 24f + bell * 18f;
        pose.rightArmEuler = new Vector3(8f - dir * bell * 18f, 0f, 10f + bell * 8f);
        pose.rightElbowFlex = 24f + bell * 18f;
        return pose;
    }

    static PoseFrame EvalJump(float u)
    {
        var pose = BaseStandingPose();
        if (u < 0.32f)
        {
            // Quick anticipation crouch
            float c = Mathf.Sin((u / 0.32f) * Mathf.PI * 0.5f);
            pose.hipOffset = new Vector3(0f, -0.13f * c, 0f);
            pose.hipEuler = new Vector3(14f * c, 0f, 0f);
            pose.spineEuler = new Vector3(16f * c, 0f, 0f);
            pose.headEuler = new Vector3(-18f * c, 0f, 0f);
            pose.leftAnkleTarget = new Vector3(-HipHalfWidth, AnkleSoleHeight, 0.04f);
            pose.rightAnkleTarget = new Vector3(+HipHalfWidth, AnkleSoleHeight, -0.04f);
            pose.leftArmEuler = new Vector3(-28f * c, 0f, -10f);
            pose.rightArmEuler = new Vector3(-28f * c, 0f, 10f);
            pose.leftElbowFlex = 28f;
            pose.rightElbowFlex = 28f;
        }
        else
        {
            // Explosive launch extension & athletic knee tuck
            float e = (u - 0.32f) / 0.68f;
            pose.leftFootIKWeight = 0f;
            pose.rightFootIKWeight = 0f;
            pose.hipOffset = new Vector3(0f, Mathf.Lerp(-0.10f, 0.05f, e), 0f);
            pose.hipEuler = new Vector3(Mathf.Lerp(10f, -4f, e), 0f, 0f);
            pose.spineEuler = new Vector3(Mathf.Lerp(12f, 4f, e), 0f, 0f);
            pose.leftAnkleTarget = new Vector3(-HipHalfWidth, AnkleSoleHeight + Mathf.Lerp(0.02f, 0.28f, e), Mathf.Lerp(0f, 0.16f, e));
            pose.rightAnkleTarget = new Vector3(+HipHalfWidth, AnkleSoleHeight + Mathf.Lerp(0.02f, 0.12f, e), Mathf.Lerp(0f, -0.18f, e));
            pose.leftFootPitch = -25f * e;
            pose.rightFootPitch = -28f * e;
            pose.leftArmEuler = new Vector3(Mathf.Lerp(-24f, 62f, e), 0f, -18f * e - 6f);
            pose.rightArmEuler = new Vector3(Mathf.Lerp(-24f, 54f, e), 0f, 18f * e + 6f);
            pose.leftElbowFlex = Mathf.Lerp(28f, 65f, e);
            pose.rightElbowFlex = Mathf.Lerp(28f, 58f, e);
        }
        return pose;
    }

    static PoseFrame EvalFall(float u)
    {
        float p = u * Mathf.PI * 2f;
        float sway = Mathf.Sin(p);
        var pose = BaseStandingPose();
        pose.leftFootIKWeight = 0f;
        pose.rightFootIKWeight = 0f;
        pose.hipOffset = new Vector3(0f, -0.02f + sway * 0.015f, 0f);
        pose.hipEuler = new Vector3(6f, sway * 3f, 0f);
        pose.spineEuler = new Vector3(-4f, -sway * 3f, 0f);
        pose.headEuler = new Vector3(8f, 0f, 0f);

        pose.leftAnkleTarget = new Vector3(-HipHalfWidth - 0.02f, AnkleSoleHeight + 0.22f + sway * 0.02f, 0.14f);
        pose.rightAnkleTarget = new Vector3(+HipHalfWidth + 0.02f, AnkleSoleHeight + 0.14f - sway * 0.02f, -0.08f);
        pose.leftFootPitch = -18f;
        pose.rightFootPitch = -22f;

        pose.leftArmEuler = new Vector3(26f + sway * 6f, 0f, -34f - sway * 4f);
        pose.rightArmEuler = new Vector3(22f - sway * 6f, 0f, 34f + sway * 4f);
        pose.leftElbowFlex = 54f;
        pose.rightElbowFlex = 54f;
        return pose;
    }

    static PoseFrame EvalLand(float u)
    {
        // Rapid shock-absorbing knee/hip bend -> smooth elastic recovery to standing
        float compress = u < 0.28f ? Mathf.Sin((u / 0.28f) * Mathf.PI * 0.5f) : Mathf.Cos(((u - 0.28f) / 0.72f) * Mathf.PI * 0.5f);
        compress = Mathf.Max(0f, compress);
        var pose = BaseStandingPose();
        pose.hipOffset = new Vector3(0f, -0.22f * compress, 0f);
        pose.hipEuler = new Vector3(14f * compress, 0f, 0f);
        pose.spineEuler = new Vector3(18f * compress, 0f, 0f);
        pose.chestEuler = new Vector3(10f * compress, 0f, 0f);
        pose.headEuler = new Vector3(-22f * compress, 0f, 0f);

        pose.leftAnkleTarget = new Vector3(-HipHalfWidth - 0.02f * compress, AnkleSoleHeight, 0.05f);
        pose.rightAnkleTarget = new Vector3(+HipHalfWidth + 0.02f * compress, AnkleSoleHeight, -0.04f);

        pose.leftArmEuler = new Vector3(-24f * compress + 4f, 0f, -18f * compress - 5f);
        pose.rightArmEuler = new Vector3(-24f * compress + 4f, 0f, 18f * compress + 5f);
        pose.leftElbowFlex = 15f + 42f * compress;
        pose.rightElbowFlex = 15f + 42f * compress;
        return pose;
    }

    #endregion

    #region 2-Bone Analytical Leg IK & HumanPose Baking

    static void ApplyPoseFrame(BoneRefs b, PoseFrame f)
    {
        ResetBindPose(b);

        b.hips.position = b.root.TransformPoint(new Vector3(0f, HipDefaultY, 0f) + f.hipOffset);
        b.hips.rotation = b.root.rotation * Quaternion.Euler(f.hipEuler);
        b.spine.localRotation = Quaternion.Euler(f.spineEuler);
        b.chest.localRotation = Quaternion.Euler(f.chestEuler);
        b.upperChest.localRotation = Quaternion.Euler(f.upperChestEuler);
        b.neck.localRotation = Quaternion.Euler(f.neckEuler);
        b.head.localRotation = Quaternion.Euler(f.headEuler);

        // Arms: start from T-pose (-X left, +X right), drop naturally down (+80/-80 Z),
        // then swing forward/backward around Y (+Y forward on left, -Y forward on right) and abduct around Z.
        b.lUa.localRotation = Quaternion.Euler(0f, 0f, 80f)
            * Quaternion.Euler(0f, +f.leftArmEuler.x, f.leftArmEuler.z);
        b.lLa.localRotation = Quaternion.Euler(0f, +Mathf.Max(0f, f.leftElbowFlex), 0f);

        b.rUa.localRotation = Quaternion.Euler(0f, 0f, -80f)
            * Quaternion.Euler(0f, -f.rightArmEuler.x, f.rightArmEuler.z);
        b.rLa.localRotation = Quaternion.Euler(0f, -Mathf.Max(0f, f.rightElbowFlex), 0f);

        // Legs: Analytical 2-Bone IK from hip socket to ankle target with forward knee pole
        SolveLeg(b.lUl, b.lLl, b.lFt, b.lTo, b.root.TransformPoint(f.leftAnkleTarget), f.leftFootPitch, f.leftToePitch, f.leftFootYaw, b.root);
        SolveLeg(b.rUl, b.rLl, b.rFt, b.rTo, b.root.TransformPoint(f.rightAnkleTarget), f.rightFootPitch, f.rightToePitch, f.rightFootYaw, b.root);
    }

    static void SolveLeg(
        Transform thigh, Transform shin, Transform foot, Transform toes,
        Vector3 targetAnkleWorld, float footPitchDeg, float toePitchDeg, float footYawDeg, Transform root)
    {
        Vector3 hipPos = thigh.position;
        Vector3 toTarget = targetAnkleWorld - hipPos;
        float dist = Mathf.Clamp(toTarget.magnitude, 0.12f, (ThighLen + ShinLen) * 0.996f);
        Vector3 dir = toTarget.normalized;

        // Knee pole direction (+Z forward with slight outward natural tracking)
        Vector3 poleDir = (root.forward + root.right * (foot.position.x >= root.position.x ? 0.08f : -0.08f)).normalized;
        Vector3 bendPlaneRight = Vector3.Cross(poleDir, dir);
        if (bendPlaneRight.sqrMagnitude < 0.0001f) bendPlaneRight = root.right;
        else bendPlaneRight.Normalize();
        Vector3 kneeDir = Vector3.Cross(dir, bendPlaneRight).normalized;

        // Law of cosines
        float cosHip = Mathf.Clamp((ThighLen * ThighLen + dist * dist - ShinLen * ShinLen) / (2f * ThighLen * dist), -1f, 1f);
        float hipAngle = Mathf.Acos(cosHip);

        Vector3 thighDir = (dir * Mathf.Cos(hipAngle) + kneeDir * Mathf.Sin(hipAngle)).normalized;
        Vector3 kneePos = hipPos + thighDir * ThighLen;
        Vector3 shinDir = (targetAnkleWorld - kneePos).normalized;

        // Align bone -Y vector to thighDir / shinDir while keeping knee bend axis aligned to bendPlaneRight
        thigh.rotation = Quaternion.LookRotation(Vector3.Cross(bendPlaneRight, -thighDir).normalized, -thighDir);
        shin.rotation = Quaternion.LookRotation(Vector3.Cross(bendPlaneRight, -shinDir).normalized, -shinDir);

        // Foot orientation relative to character root
        Quaternion footWorld = root.rotation * Quaternion.Euler(footPitchDeg, footYawDeg, 0f);
        foot.rotation = footWorld;
        toes.localRotation = Quaternion.Euler(toePitchDeg, 0f, 0f);
    }

    static AnimationClip BakeClip(
        string clipName,
        float duration,
        int fps,
        bool loop,
        BoneRefs bones,
        HumanPoseHandler handler,
        Func<float, PoseFrame> eval)
    {
        int sampleCount = Mathf.Max(2, Mathf.RoundToInt(duration * fps));
        int muscleCount = HumanTrait.MuscleCount;

        var rootTx = new List<Keyframe>();
        var rootTy = new List<Keyframe>();
        var rootTz = new List<Keyframe>();
        var rootQx = new List<Keyframe>();
        var rootQy = new List<Keyframe>();
        var rootQz = new List<Keyframe>();
        var rootQw = new List<Keyframe>();
        var lIkKeys = new List<Keyframe>();
        var rIkKeys = new List<Keyframe>();

        var muscleCurves = new List<Keyframe>[muscleCount];
        for (int m = 0; m < muscleCount; m++) muscleCurves[m] = new List<Keyframe>();

        var pose = new HumanPose();
        for (int i = 0; i <= sampleCount; i++)
        {
            float u = i / (float)sampleCount;
            float t = u * duration;
            if (loop && i == sampleCount) u = 0f; // Guarantee seamless wrap at loop end!

            var pf = eval(u);
            ApplyPoseFrame(bones, pf);
            handler.GetHumanPose(ref pose);

            rootTx.Add(new Keyframe(t, pose.bodyPosition.x));
            rootTy.Add(new Keyframe(t, pose.bodyPosition.y));
            rootTz.Add(new Keyframe(t, pose.bodyPosition.z));
            rootQx.Add(new Keyframe(t, pose.bodyRotation.x));
            rootQy.Add(new Keyframe(t, pose.bodyRotation.y));
            rootQz.Add(new Keyframe(t, pose.bodyRotation.z));
            rootQw.Add(new Keyframe(t, pose.bodyRotation.w));
            lIkKeys.Add(new Keyframe(t, pf.leftFootIKWeight));
            rIkKeys.Add(new Keyframe(t, pf.rightFootIKWeight));

            for (int m = 0; m < muscleCount; m++)
            {
                muscleCurves[m].Add(new Keyframe(t, pose.muscles[m]));
            }
        }

        var clip = new AnimationClip { name = clipName, frameRate = fps };

        void BindCurve(string prop, List<Keyframe> keys)
        {
            var curve = new AnimationCurve(keys.ToArray());
            for (int k = 0; k < curve.length; k++)
                AnimationUtility.SetKeyBroken(curve, k, false);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), prop), curve);
        }

        BindCurve("RootT.x", rootTx);
        BindCurve("RootT.y", rootTy);
        BindCurve("RootT.z", rootTz);
        BindCurve("RootQ.x", rootQx);
        BindCurve("RootQ.y", rootQy);
        BindCurve("RootQ.z", rootQz);
        BindCurve("RootQ.w", rootQw);
        BindCurve("LeftFootIKWeight", lIkKeys);
        BindCurve("RightFootIKWeight", rIkKeys);

        for (int m = 0; m < muscleCount; m++)
        {
            // Keep all core body/limb muscles (skip fingers/eyes/jaw that are zero)
            bool nonZero = false;
            for (int k = 0; k < muscleCurves[m].Count; k++)
            {
                if (Mathf.Abs(muscleCurves[m][k].value) > 0.0005f) { nonZero = true; break; }
            }
            if (nonZero)
            {
                BindCurve(HumanTrait.MuscleName[m], muscleCurves[m]);
            }
        }

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.loopBlend = loop;
        settings.loopBlendOrientation = true;
        settings.loopBlendPositionY = true;
        settings.loopBlendPositionXZ = true;
        settings.keepOriginalOrientation = true;
        settings.keepOriginalPositionY = true;
        settings.keepOriginalPositionXZ = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        string path = AnimDir + "/" + clipName + ".anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(clip, path);
        return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
    }

    #endregion

    #region Animator Controller & Blend Trees

    static AvatarMask CreateUpperBodyMask()
    {
        var mask = new AvatarMask();
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
        {
            var part = (AvatarMaskBodyPart)i;
            bool upper = part == AvatarMaskBodyPart.Body ||
                         part == AvatarMaskBodyPart.Head ||
                         part == AvatarMaskBodyPart.LeftArm ||
                         part == AvatarMaskBodyPart.RightArm;
            mask.SetHumanoidBodyPartActive(part, upper);
        }
        var existing = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperMaskPath);
        if (existing != null) AssetDatabase.DeleteAsset(UpperMaskPath);
        AssetDatabase.CreateAsset(mask, UpperMaskPath);
        return AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperMaskPath);
    }

    static AnimatorController BuildAnimatorController(Dictionary<string, AnimationClip> clips, AvatarMask upperMask)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            AssetDatabase.DeleteAsset(ControllerPath);

        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        ctrl.AddParameter("MoveX", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("SpeedNorm", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("TurnAngle", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("StartTrigger", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("StopTrigger", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Turn90L", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Turn90R", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Turn180L", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Turn180R", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("JumpTrigger", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("LandTrigger", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("LeftFootIKWeight", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("RightFootIKWeight", AnimatorControllerParameterType.Float);

        var baseSm = ctrl.layers[0].stateMachine;

        // 1. Idle state
        var idleState = baseSm.AddState("Idle", new Vector3(260, 120, 0));
        idleState.motion = clips["Idle_Loop"];
        baseSm.defaultState = idleState;

        // 2. Start Move state
        var startState = baseSm.AddState("StartMove", new Vector3(480, 40, 0));
        startState.motion = clips["Start_Move"];

        // 3. 2D Freeform Directional Locomotion Blend Tree (Idle -> Walk -> Jog -> Sprint & Fwd/Back/Left/Right)
        var locoTree = new BlendTree
        {
            name = "Locomotion2D",
            blendType = BlendTreeType.FreeformDirectional2D,
            blendParameter = "MoveX",
            blendParameterY = "MoveZ",
            useAutomaticThresholds = false
        };
        AssetDatabase.AddObjectToAsset(locoTree, ctrl);

        locoTree.AddChild(clips["Idle_Loop"], new Vector2(0f, 0f));
        locoTree.AddChild(clips["Walk_Fwd"], new Vector2(0f, 1.65f));
        locoTree.AddChild(clips["Walk_Back"], new Vector2(0f, -1.25f));
        locoTree.AddChild(clips["Walk_Strafe_L"], new Vector2(-1.45f, 0f));
        locoTree.AddChild(clips["Walk_Strafe_R"], new Vector2(1.45f, 0f));
        locoTree.AddChild(clips["Jog_Fwd"], new Vector2(0f, 4.2f));
        locoTree.AddChild(clips["Jog_Back"], new Vector2(0f, -2.8f));
        locoTree.AddChild(clips["Jog_Strafe_L"], new Vector2(-3.5f, 0f));
        locoTree.AddChild(clips["Jog_Strafe_R"], new Vector2(3.5f, 0f));
        locoTree.AddChild(clips["Sprint_Fwd"], new Vector2(0f, 7.4f));

        var locoState = baseSm.AddState("Locomotion", new Vector3(560, 150, 0));
        locoState.motion = locoTree;

        // 4. Stop Move state
        var stopState = baseSm.AddState("StopMove", new Vector3(320, 240, 0));
        stopState.motion = clips["Stop_Move"];

        // 5. Turn 90 / 180 Pivot states
        var turn90L = baseSm.AddState("Turn90L", new Vector3(760, 40, 0)); turn90L.motion = clips["Turn_90_L"];
        var turn90R = baseSm.AddState("Turn90R", new Vector3(760, 110, 0)); turn90R.motion = clips["Turn_90_R"];
        var turn180L = baseSm.AddState("Turn180L", new Vector3(760, 190, 0)); turn180L.motion = clips["Turn_180_L"];
        var turn180R = baseSm.AddState("Turn180R", new Vector3(760, 260, 0)); turn180R.motion = clips["Turn_180_R"];

        // 6. Airborne Jump / Fall / Land states
        var jumpState = baseSm.AddState("Jump", new Vector3(260, -80, 0)); jumpState.motion = clips["Jump_Start"];
        var fallState = baseSm.AddState("Fall", new Vector3(480, -80, 0)); fallState.motion = clips["Fall_Loop"];
        var landState = baseSm.AddState("Land", new Vector3(680, -80, 0)); landState.motion = clips["Land_Recovery"];

        // Transitions
        void AddT(AnimatorState from, AnimatorState to, float dur, bool hasExit, float exitTime, Action<AnimatorStateTransition> cfg = null)
        {
            var t = from.AddTransition(to);
            t.duration = dur;
            t.hasExitTime = hasExit;
            t.exitTime = exitTime;
            t.hasFixedDuration = true;
            cfg?.Invoke(t);
        }

        // Idle -> StartMove (on StartTrigger) or directly to Locomotion if moving
        AddT(idleState, startState, 0.08f, false, 0f, t => { t.AddCondition(AnimatorConditionMode.If, 0f, "StartTrigger"); });
        AddT(idleState, locoState, 0.14f, false, 0f, t => { t.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving"); });

        // StartMove -> Locomotion or back to Stop/Idle
        AddT(startState, locoState, 0.14f, true, 0.72f);
        AddT(startState, idleState, 0.12f, false, 0f, t => { t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving"); });

        // Locomotion -> StopMove or Idle
        AddT(locoState, stopState, 0.10f, false, 0f, t => { t.AddCondition(AnimatorConditionMode.If, 0f, "StopTrigger"); });
        AddT(locoState, idleState, 0.22f, false, 0f, t =>
        {
            t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");
            t.AddCondition(AnimatorConditionMode.Less, 0.25f, "Speed");
        });

        // StopMove -> Idle cleanly, or interrupt immediately into Locomotion if moving again!
        AddT(stopState, idleState, 0.15f, true, 0.85f);
        AddT(stopState, locoState, 0.10f, false, 0f, t => { t.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving"); });

        // Turns from Idle or Locomotion
        foreach (var src in new[] { idleState, locoState })
        {
            AddT(src, turn90L, 0.10f, false, 0f, t => t.AddCondition(AnimatorConditionMode.If, 0f, "Turn90L"));
            AddT(src, turn90R, 0.10f, false, 0f, t => t.AddCondition(AnimatorConditionMode.If, 0f, "Turn90R"));
            AddT(src, turn180L, 0.10f, false, 0f, t => t.AddCondition(AnimatorConditionMode.If, 0f, "Turn180L"));
            AddT(src, turn180R, 0.10f, false, 0f, t => t.AddCondition(AnimatorConditionMode.If, 0f, "Turn180R"));
        }

        foreach (var turnSt in new[] { turn90L, turn90R, turn180L, turn180R })
        {
            AddT(turnSt, locoState, 0.16f, true, 0.78f, t => t.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving"));
            AddT(turnSt, idleState, 0.16f, true, 0.86f, t => t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving"));
        }

        // AnyState -> Jump / Fall
        var anyToJump = baseSm.AddAnyStateTransition(jumpState);
        anyToJump.duration = 0.08f;
        anyToJump.hasExitTime = false;
        anyToJump.canTransitionToSelf = false;
        anyToJump.AddCondition(AnimatorConditionMode.If, 0f, "JumpTrigger");

        var anyToFall = baseSm.AddAnyStateTransition(fallState);
        anyToFall.duration = 0.18f;
        anyToFall.hasExitTime = false;
        anyToFall.canTransitionToSelf = false;
        anyToFall.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded");
        anyToFall.AddCondition(AnimatorConditionMode.Less, -1.6f, "VerticalSpeed");

        AddT(jumpState, fallState, 0.14f, true, 0.82f);
        AddT(jumpState, landState, 0.08f, false, 0f, t =>
        {
            t.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
            t.AddCondition(AnimatorConditionMode.Less, 0.5f, "VerticalSpeed");
        });

        AddT(fallState, landState, 0.07f, false, 0f, t => t.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded"));

        // Land Recovery -> smooth blend back into locomotion or idle (interruptible early if running!)
        AddT(landState, locoState, 0.14f, true, 0.55f, t => t.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving"));
        AddT(landState, idleState, 0.18f, true, 0.88f, t => t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving"));

        // Add subtle Upper-Body breathing / poise layer
        ctrl.AddLayer("UpperBody");
        var layers = ctrl.layers;
        layers[1].avatarMask = upperMask;
        layers[1].defaultWeight = 0.20f;
        layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
        var ubIdle = layers[1].stateMachine.AddState("UpperIdle", new Vector3(260, 120, 0));
        ubIdle.motion = clips["Idle_Loop"];
        layers[1].stateMachine.defaultState = ubIdle;
        ctrl.layers = layers;

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    #endregion
}
