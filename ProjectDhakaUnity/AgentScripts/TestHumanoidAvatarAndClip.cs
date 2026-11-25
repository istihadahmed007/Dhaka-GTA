using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public static class TestHumanoidAvatarAndClip
{
    public static string Run()
    {
        var root = new GameObject("TempHumanoidTest");
        try
        {
            var hips = MakeBone("Hips", root.transform, new Vector3(0, 0.96f, 0));
            var spine = MakeBone("Spine", hips, new Vector3(0, 0.11f, 0));
            var chest = MakeBone("Chest", spine, new Vector3(0, 0.16f, 0));
            var upperChest = MakeBone("UpperChest", chest, new Vector3(0, 0.16f, 0));
            var neck = MakeBone("Neck", upperChest, new Vector3(0, 0.15f, 0));
            var head = MakeBone("Head", neck, new Vector3(0, 0.09f, 0));

            var lSh = MakeBone("LeftShoulder", upperChest, new Vector3(-0.05f, 0.10f, 0));
            var lUa = MakeBone("LeftUpperArm", lSh, new Vector3(-0.14f, 0, 0));
            var lLa = MakeBone("LeftLowerArm", lUa, new Vector3(-0.28f, 0, 0));
            var lHa = MakeBone("LeftHand", lLa, new Vector3(-0.25f, 0, 0));

            var rSh = MakeBone("RightShoulder", upperChest, new Vector3(0.05f, 0.10f, 0));
            var rUa = MakeBone("RightUpperArm", rSh, new Vector3(0.14f, 0, 0));
            var rLa = MakeBone("RightLowerArm", rUa, new Vector3(0.28f, 0, 0));
            var rHa = MakeBone("RightHand", rLa, new Vector3(0.25f, 0, 0));

            var lUl = MakeBone("LeftUpperLeg", hips, new Vector3(-0.105f, -0.04f, 0));
            var lLl = MakeBone("LeftLowerLeg", lUl, new Vector3(0, -0.44f, 0));
            var lFt = MakeBone("LeftFoot", lLl, new Vector3(0, -0.43f, 0));
            var lTo = MakeBone("LeftToes", lFt, new Vector3(0, -0.04f, 0.14f));

            var rUl = MakeBone("RightUpperLeg", hips, new Vector3(0.105f, -0.04f, 0));
            var rLl = MakeBone("RightLowerLeg", rUl, new Vector3(0, -0.44f, 0));
            var rFt = MakeBone("RightFoot", rLl, new Vector3(0, -0.43f, 0));
            var rTo = MakeBone("RightToes", rFt, new Vector3(0, -0.04f, 0.14f));

            var bones = root.GetComponentsInChildren<Transform>();
            var skel = new SkeletonBone[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                skel[i] = new SkeletonBone
                {
                    name = bones[i].name,
                    position = bones[i].localPosition,
                    rotation = bones[i].localRotation,
                    scale = bones[i].localScale
                };
            }

            string[] mapPairs = new[]
            {
                "Hips", "Hips",
                "Spine", "Spine",
                "Chest", "Chest",
                "UpperChest", "UpperChest",
                "Neck", "Neck",
                "Head", "Head",
                "LeftShoulder", "LeftShoulder",
                "LeftUpperArm", "LeftUpperArm",
                "LeftLowerArm", "LeftLowerArm",
                "LeftHand", "LeftHand",
                "RightShoulder", "RightShoulder",
                "RightUpperArm", "RightUpperArm",
                "RightLowerArm", "RightLowerArm",
                "RightHand", "RightHand",
                "LeftUpperLeg", "LeftUpperLeg",
                "LeftLowerLeg", "LeftLowerLeg",
                "LeftFoot", "LeftFoot",
                "LeftToes", "LeftToes",
                "RightUpperLeg", "RightUpperLeg",
                "RightLowerLeg", "RightLowerLeg",
                "RightFoot", "RightFoot",
                "RightToes", "RightToes"
            };
            var human = new HumanBone[mapPairs.Length / 2];
            for (int i = 0; i < human.Length; i++)
            {
                human[i] = new HumanBone
                {
                    humanName = mapPairs[i * 2],
                    boneName = mapPairs[i * 2 + 1],
                    limit = new HumanLimit { useDefaultValues = true }
                };
            }

            var desc = new HumanDescription
            {
                skeleton = skel,
                human = human,
                upperArmTwist = 0.5f,
                lowerArmTwist = 0.5f,
                upperLegTwist = 0.5f,
                lowerLegTwist = 0.5f,
                armStretch = 0.05f,
                legStretch = 0.05f,
                feetSpacing = 0f,
                hasTranslationDoF = false
            };

            Avatar avatar = AvatarBuilder.BuildHumanAvatar(root, desc);
            var handler = new HumanPoseHandler(avatar, root.transform);
            var pose = new HumanPose();
            handler.GetHumanPose(ref pose);

            // Test applying a relaxed arm pose
            int idxL = Array.IndexOf(HumanTrait.MuscleName, "Left Arm Down-Up");
            int idxR = Array.IndexOf(HumanTrait.MuscleName, "Right Arm Down-Up");
            pose.muscles[idxL] = -0.75f;
            pose.muscles[idxR] = -0.75f;
            handler.SetHumanPose(ref pose);

            var clip = new AnimationClip { name = "ProbeIdle" };
            var curve = AnimationCurve.Constant(0f, 1f, -0.75f);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "Left Arm Down-Up"), curve);

            return $"isValid={avatar.isValid} isHuman={avatar.isHuman} bodyPos={pose.bodyPosition} lArmLocalEuler={lUa.localEulerAngles} rArmLocalEuler={rUa.localEulerAngles} clipHuman={clip.isHumanMotion}";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    static Transform MakeBone(string name, Transform parent, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        return go.transform;
    }
}
