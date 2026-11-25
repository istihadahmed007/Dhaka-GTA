using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using ProjectDhaka;

public static class WireDhakaRigPrefab
{
    const string PrefabPath = "Assets/Prefabs/DhakaProtagonist.prefab";
    const string ControllerPath = "Assets/Characters/DhakaProtagonist/Animations/DhakaHumanoidLocomotion.controller";
    const string AvatarPath = "Assets/Characters/DhakaProtagonist/DhakaProtagonistAvatar.asset";

    public static string Wire()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var animator = root.GetComponent<Animator>();
            if (animator == null) animator = root.AddComponent<Animator>();

            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(AvatarPath);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Humanoid physical CharacterController matching sculpted height ~1.78m
            var cc = root.GetComponent<CharacterController>();
            if (cc == null) cc = root.AddComponent<CharacterController>();
            cc.height = 1.78f;
            cc.radius = 0.28f;
            cc.center = new Vector3(0f, 0.90f, 0f);
            cc.stepOffset = 0.35f;
            cc.slopeLimit = 48f;
            cc.skinWidth = 0.03f;

            // Remove old ProceduralRig container if present so hierarchy stays clean & idempotent
            var oldRig = root.transform.Find("ProceduralRig");
            if (oldRig != null) Object.DestroyImmediate(oldRig.gameObject);

            var rigBuilder = root.GetComponent<RigBuilder>();
            if (rigBuilder == null) rigBuilder = root.AddComponent<RigBuilder>();

            var proceduralRigGo = new GameObject("ProceduralRig");
            proceduralRigGo.transform.SetParent(root.transform, false);

            // Cache skeleton bones from root/Skeleton
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform upperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);

            Transform lUpLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            Transform lLoLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            Transform lFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);

            Transform rUpLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            Transform rLoLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            Transform rFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);

            Transform lUpArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform lLoArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            Transform lHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);

            Transform rUpArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform rLoArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            Transform rHand = animator.GetBoneTransform(HumanBodyBones.RightHand);

            // ---- LAYER 0: Lower Body IK & Pelvis Adaptation ----
            var lowerGo = new GameObject("Rig_LowerBodyIK");
            lowerGo.transform.SetParent(proceduralRigGo.transform, false);
            var lowerRig = lowerGo.AddComponent<Rig>();
            lowerRig.weight = 1f;

            // Targets for Pivot-space OverrideTransform constraints MUST bind at Vector3.zero
            // so sourceInvLocalBindTx is identity and runtime localPosition/localRotation act as pure deltas.
            var pelvisTarget = CreateProxy("PelvisOffsetTarget", lowerGo.transform, Vector3.zero);
            var pelvisOverrideGo = new GameObject("PelvisOffsetConstraint");
            pelvisOverrideGo.transform.SetParent(lowerGo.transform, false);
            var pelvisConstraint = pelvisOverrideGo.AddComponent<OverrideTransform>();
            var pelvisData = pelvisConstraint.data;
            pelvisData.constrainedObject = hips;
            pelvisData.sourceObject = pelvisTarget;
            pelvisData.space = OverrideTransformData.Space.Pivot;
            pelvisData.positionWeight = 1f;
            pelvisData.rotationWeight = 1f;
            pelvisConstraint.data = pelvisData;
            pelvisConstraint.weight = 1f;

            var lFootTarget = CreateProxy("LeftFootIKTarget", lowerGo.transform, lFoot != null ? lFoot.position : new Vector3(-0.102f, 0.088f, 0f));
            var lKneeHint = CreateProxy("LeftKneeHint", lowerGo.transform, new Vector3(-0.14f, 0.50f, 0.48f));
            CreateTwoBoneIK("LeftLegIK", lowerGo.transform, lUpLeg, lLoLeg, lFoot, lFootTarget, lKneeHint, 1f);

            var rFootTarget = CreateProxy("RightFootIKTarget", lowerGo.transform, rFoot != null ? rFoot.position : new Vector3(0.102f, 0.088f, 0f));
            var rKneeHint = CreateProxy("RightKneeHint", lowerGo.transform, new Vector3(0.14f, 0.50f, 0.48f));
            CreateTwoBoneIK("RightLegIK", lowerGo.transform, rUpLeg, rLoLeg, rFoot, rFootTarget, rKneeHint, 1f);

            // ---- LAYER 1: Upper Body Tilt, Head Look & Arm Interaction IK ----
            var upperGo = new GameObject("Rig_UpperBodyProcedural");
            upperGo.transform.SetParent(proceduralRigGo.transform, false);
            var upperRig = upperGo.AddComponent<Rig>();
            upperRig.weight = 1f;

            var torsoTiltTarget = CreateProxy("TorsoTiltTarget", upperGo.transform, Vector3.zero);
            var torsoOverrideGo = new GameObject("TorsoTiltConstraint");
            torsoOverrideGo.transform.SetParent(upperGo.transform, false);
            var torsoConstraint = torsoOverrideGo.AddComponent<OverrideTransform>();
            var torsoData = torsoConstraint.data;
            torsoData.constrainedObject = upperChest;
            torsoData.sourceObject = torsoTiltTarget;
            torsoData.space = OverrideTransformData.Space.Pivot;
            torsoData.positionWeight = 0f;
            torsoData.rotationWeight = 1f;
            torsoConstraint.data = torsoData;
            torsoConstraint.weight = 1f;

            var headLookTarget = CreateProxy("HeadLookTarget", upperGo.transform, new Vector3(0f, 1.62f, 3.5f));
            var headAimGo = new GameObject("HeadLookAim");
            headAimGo.transform.SetParent(upperGo.transform, false);
            var headAim = headAimGo.AddComponent<MultiAimConstraint>();
            var aimData = headAim.data;
            aimData.constrainedObject = head;
            aimData.aimAxis = MultiAimConstraintData.Axis.Z;
            aimData.upAxis = MultiAimConstraintData.Axis.Y;
            aimData.worldUpType = MultiAimConstraintData.WorldUpType.SceneUp;
            aimData.limits = new Vector2(-60f, 60f);
            aimData.constrainedXAxis = true;
            aimData.constrainedYAxis = true;
            aimData.constrainedZAxis = true;
            aimData.maintainOffset = false;
            var sources = new WeightedTransformArray();
            sources.Add(new WeightedTransform(headLookTarget, 1f));
            aimData.sourceObjects = sources;
            headAim.data = aimData;
            headAim.weight = 0.35f;

            var lHandTarget = CreateProxy("LeftHandIKTarget", upperGo.transform, lHand != null ? lHand.position : new Vector3(-0.35f, 0.90f, 0.10f));
            var lElbowHint = CreateProxy("LeftElbowHint", upperGo.transform, new Vector3(-0.32f, 1.25f, -0.35f));
            CreateTwoBoneIK("LeftArmIK", upperGo.transform, lUpArm, lLoArm, lHand, lHandTarget, lElbowHint, 0f);

            var rHandTarget = CreateProxy("RightHandIKTarget", upperGo.transform, rHand != null ? rHand.position : new Vector3(0.35f, 0.90f, 0.10f));
            var rElbowHint = CreateProxy("RightElbowHint", upperGo.transform, new Vector3(0.32f, 1.25f, -0.35f));
            CreateTwoBoneIK("RightArmIK", upperGo.transform, rUpArm, rLoArm, rHand, rHandTarget, rElbowHint, 0f);

            rigBuilder.layers = new List<RigLayer>
            {
                new RigLayer(lowerRig, true),
                new RigLayer(upperRig, true)
            };

            // Attach gameplay, avoidance, procedural rig driver & F3 debugger (hidden by default)
            var rigDriver = root.GetComponent<DhakaProceduralRigDriver>() ?? root.AddComponent<DhakaProceduralRigDriver>();
            rigDriver.ResolveReferences();

            var avoider = root.GetComponent<DhakaObstacleAvoider>() ?? root.AddComponent<DhakaObstacleAvoider>();
            avoider.ShowWorldTrajectoryArrows = false;

            var playerCtrl = root.GetComponent<PlayerController>() ?? root.AddComponent<PlayerController>();
            playerCtrl.ResolveReferences();
            playerCtrl.SetArenaClamp(false);

            var debugger = root.GetComponent<DhakaAnimationDebugger>() ?? root.AddComponent<DhakaAnimationDebugger>();
            debugger.ResolveReferences();
            debugger.SetDebugEnabled(false);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();

            return $"Wired DhakaProtagonist.prefab | Controller={(controller != null ? controller.name : "NONE")} | RigLayers={rigBuilder.layers.Count} | Hips={(hips != null)} | LegL={(lUpLeg != null && lLoLeg != null && lFoot != null)} | ArmR={(rUpArm != null && rLoArm != null && rHand != null)}";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static Transform CreateProxy(string name, Transform parent, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        return go.transform;
    }

    static void CreateTwoBoneIK(string name, Transform parent, Transform rootBone, Transform midBone, Transform tipBone, Transform target, Transform hint, float initialWeight)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ik = go.AddComponent<TwoBoneIKConstraint>();
        var data = ik.data;
        data.root = rootBone;
        data.mid = midBone;
        data.tip = tipBone;
        data.target = target;
        data.hint = hint;
        data.targetPositionWeight = 1f;
        data.targetRotationWeight = 1f;
        data.hintWeight = 1f;
        data.maintainTargetPositionOffset = false;
        data.maintainTargetRotationOffset = false;
        ik.data = data;
        ik.weight = initialWeight;
    }
}
