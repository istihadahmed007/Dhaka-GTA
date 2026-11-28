using System;
using System.Collections.Generic;
using System.IO;
using DhakaGta.Character;
using DhakaGta.Core;
using DhakaGta.Data;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DhakaGta.Editor
{
    /// <summary>
    /// Part 7 Editor Asset Pipeline:
    /// 1) Imports HDRP PBR character textures & binds Skinned PBR Materials for Tariq & Maya,
    /// 2) Configures Blender Humanoid FBXs (if present) or builds anatomical rigged multi-part Humanoid SkinnedMeshes + Avatars,
    /// 3) Bakes 20 retargetable Humanoid .anim muscle clips + UpperBodyAim AvatarMask + AC_DhakaHumanoid.controller (with iKPass=true),
    /// 4) Produces PF_Protagonist_Tariq.prefab, PF_Protagonist_Maya.prefab, and MotionCodebook_Default.asset.
    /// </summary>
    public static class DhakaCharacterPipelineBuilder
    {
        public const string TexRoot = "Assets/Textures/Characters";
        public const string MatRoot = "Assets/Materials/Characters";
        public const string ModelRoot = "Assets/Models/Characters";
        public const string GeneratedMeshRoot = "Assets/Models/Characters/GeneratedMeshes";
        public const string AnimRoot = "Assets/Animations/Characters";
        public const string ClipRoot = "Assets/Animations/Characters/Clips";
        public const string PrefabRoot = "Assets/Prefabs/Characters";
        public const string DataRoot = "Assets/ScriptableObjects/Protagonists";

        public const string ControllerPath = AnimRoot + "/AC_DhakaHumanoid.controller";
        public const string UpperMaskPath = AnimRoot + "/UpperBodyAimMask.mask";
        public const string CodebookPath = DataRoot + "/MotionCodebook_Default.asset";
        public const string TariqPrefabPath = PrefabRoot + "/PF_Protagonist_Tariq.prefab";
        public const string MayaPrefabPath = PrefabRoot + "/PF_Protagonist_Maya.prefab";

        private enum BoneId
        {
            Hips = 0, Spine, Chest, UpperChest, Neck, Head, Jaw, LeftEye, RightEye,
            LeftShoulder, LeftUpperArm, LeftLowerArm, LeftHand,
            RightShoulder, RightUpperArm, RightLowerArm, RightHand,
            LeftUpperLeg, LeftLowerLeg, LeftFoot, LeftToes,
            RightUpperLeg, RightLowerLeg, RightFoot, RightToes
        }

        [MenuItem("Dhaka-GTA/Part 7/Build Tariq, Maya & Humanoid Animator Assets")]
        public static void BuildAllCharacterAssetsMenu()
        {
            EnsureCharacterPipelineAssets();
            Debug.Log("[DhakaCharacterPipelineBuilder] Built HDRP character PBR materials, Humanoid AnimatorController, 20 Humanoid clips, and Tariq/Maya prefabs.");
        }

        public static (GameObject tariqPrefab, GameObject mayaPrefab, MotionCodebookSO codebook) EnsureCharacterPipelineAssets()
        {
            EnsureDirectories();
            ConfigureCharacterTextureImporters();
            var codebook = BuildOrUpdateMotionCodebook();
            var mats = CreateOrUpdateCharacterMaterials();
            var clips = BakeHumanoidAnimationClips();
            var upperMask = CreateUpperBodyMask();
            var controller = BuildHumanoidAnimatorController(clips, upperMask);

            var tariqProfile = AssetDatabase.LoadAssetAtPath<ProtagonistProfileSO>($"{DataRoot}/Protagonist_Tariq.asset");
            var mayaProfile = AssetDatabase.LoadAssetAtPath<ProtagonistProfileSO>($"{DataRoot}/Protagonist_Maya.asset");

            GameObject tariqPrefab = BuildOrLoadCharacterPrefab(
                "Tariq",
                $"{ModelRoot}/CHAR_Tariq.fbx",
                TariqPrefabPath,
                controller,
                mats.TariqSkin,
                mats.TariqJacket,
                mats.DenimCargo,
                mats.Hair,
                mats.Footwear,
                mats.EyeIris,
                mats.TariqAccent,
                heightMeters: tariqProfile != null ? tariqProfile.characterHeightMeters : 1.80f,
                feminineBuild: false);

            GameObject mayaPrefab = BuildOrLoadCharacterPrefab(
                "Maya",
                $"{ModelRoot}/CHAR_Maya.fbx",
                MayaPrefabPath,
                controller,
                mats.MayaSkin,
                mats.MayaJacket,
                mats.DenimCargo,
                mats.Hair,
                mats.Footwear,
                mats.EyeIris,
                mats.MayaAccent,
                heightMeters: mayaProfile != null ? mayaProfile.characterHeightMeters : 1.70f,
                feminineBuild: true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return (tariqPrefab, mayaPrefab, codebook);
        }

        /// <summary>
        /// Attaches rigged Humanoid Tariq & Maya visuals, MotionMatching, IK Foot Locking,
        /// Abilities, Swimming/Dialogue, and Tab-switching onto a playable character root.
        /// </summary>
        public static ProtagonistSwitcher AttachProtagonistRigsAndSwitcher(
            GameObject playerRoot,
            ProtagonistController controller,
            InputActionRouter router,
            ProtagonistProfileSO tariqProfile,
            ProtagonistProfileSO mayaProfile)
        {
            var assets = EnsureCharacterPipelineAssets();
            GameObject tariqRig = PrefabUtility.InstantiatePrefab(assets.tariqPrefab, playerRoot.transform) as GameObject;
            tariqRig.name = "Rig_Tariq";
            tariqRig.transform.localPosition = Vector3.zero;
            tariqRig.transform.localRotation = Quaternion.identity;

            GameObject mayaRig = PrefabUtility.InstantiatePrefab(assets.mayaPrefab, playerRoot.transform) as GameObject;
            mayaRig.name = "Rig_Maya";
            mayaRig.transform.localPosition = Vector3.zero;
            mayaRig.transform.localRotation = Quaternion.identity;
            mayaRig.SetActive(false);

            var matcher = playerRoot.GetComponent<MotionMatchingController>() ?? playerRoot.AddComponent<MotionMatchingController>();
            var animator = tariqRig.GetComponentInChildren<Animator>(true);
            matcher.Configure(animator, assets.codebook);

            if (playerRoot.GetComponent<MotionTrajectoryPredictor>() == null) playerRoot.AddComponent<MotionTrajectoryPredictor>();
            var ikLocker = playerRoot.GetComponent<CharacterIkAndFootLocker>() ?? playerRoot.AddComponent<CharacterIkAndFootLocker>();
            ikLocker.Configure(animator, matcher);

            var abilityCtrl = playerRoot.GetComponent<CharacterAbilityController>() ?? playerRoot.AddComponent<CharacterAbilityController>();
            abilityCtrl.Configure(tariqProfile);

            if (playerRoot.GetComponent<CharacterSwimAndDialogueController>() == null) playerRoot.AddComponent<CharacterSwimAndDialogueController>();

            controller.BindActiveVisualRig(animator);

            var switcher = playerRoot.GetComponent<ProtagonistSwitcher>() ?? playerRoot.AddComponent<ProtagonistSwitcher>();
            switcher.Configure(controller, router, tariqProfile, mayaProfile, tariqRig, mayaRig);
            return switcher;
        }

        private static MotionCodebookSO BuildOrUpdateMotionCodebook()
        {
            var cb = AssetDatabase.LoadAssetAtPath<MotionCodebookSO>(CodebookPath);
            if (cb == null)
            {
                cb = ScriptableObject.CreateInstance<MotionCodebookSO>();
                cb.PopulateDefaultAi4AnimationEntries();
                AssetDatabase.CreateAsset(cb, CodebookPath);
            }
            else
            {
                cb.PopulateDefaultAi4AnimationEntries();
                EditorUtility.SetDirty(cb);
            }
            return cb;
        }

        private static void ConfigureCharacterTextureImporters()
        {
            if (!Directory.Exists(TexRoot)) return;
            foreach (string file in Directory.GetFiles(TexRoot, "*.png"))
            {
                string assetPath = file.Replace('\\', '/');
                if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
                {
                    bool dirty = false;
                    if (assetPath.EndsWith("_Normal.png", StringComparison.OrdinalIgnoreCase))
                    {
                        if (importer.textureType != TextureImporterType.NormalMap)
                        {
                            importer.textureType = TextureImporterType.NormalMap;
                            dirty = true;
                        }
                    }
                    else if (assetPath.EndsWith("_MaskMap.png", StringComparison.OrdinalIgnoreCase))
                    {
                        if (importer.sRGBTexture)
                        {
                            importer.sRGBTexture = false;
                            dirty = true;
                        }
                    }

                    if (dirty)
                    {
                        importer.SaveAndReimport();
                    }
                }
            }
        }

        private sealed class CharMatSet
        {
            public Material TariqSkin, MayaSkin, TariqJacket, MayaJacket, DenimCargo, Hair, Footwear, EyeIris, TariqAccent, MayaAccent;
        }

        private static CharMatSet CreateOrUpdateCharacterMaterials()
        {
            Shader litShader = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");

            Material MakePbr(string fileName, Color tint, string prefix, float defaultSmooth, float metallic)
            {
                string path = $"{MatRoot}/{fileName}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(litShader);
                    AssetDatabase.CreateAsset(mat, path);
                }
                else
                {
                    mat.shader = litShader;
                }

                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
                else mat.color = tint;

                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", defaultSmooth);
                if (mat.HasProperty("_EnableGPUInstancing")) mat.enableInstancing = true;

                if (!string.IsNullOrEmpty(prefix))
                {
                    Texture2D alb = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexRoot}/{prefix}_Albedo.png");
                    Texture2D nrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexRoot}/{prefix}_Normal.png");
                    Texture2D msk = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexRoot}/{prefix}_MaskMap.png");

                    if (alb != null && mat.HasProperty("_BaseColorMap")) mat.SetTexture("_BaseColorMap", alb);
                    else if (alb != null) mat.mainTexture = alb;

                    if (nrm != null && mat.HasProperty("_NormalMap"))
                    {
                        mat.SetTexture("_NormalMap", nrm);
                        mat.EnableKeyword("_NORMALMAP");
                    }

                    if (msk != null && mat.HasProperty("_MaskMap"))
                    {
                        mat.SetTexture("_MaskMap", msk);
                        mat.EnableKeyword("_MASKMAP");
                    }
                }

                EditorUtility.SetDirty(mat);
                return mat;
            }

            var set = new CharMatSet
            {
                TariqSkin = MakePbr("MAT_Char_Skin_Tariq", new Color(0.88f, 0.72f, 0.62f, 1f), "T_Char_Skin", 0.42f, 0f),
                MayaSkin = MakePbr("MAT_Char_Skin_Maya", new Color(0.94f, 0.78f, 0.68f, 1f), "T_Char_Skin", 0.46f, 0f),
                TariqJacket = MakePbr("MAT_Char_Tariq_Jacket", Color.white, "T_Char_Tariq_Jacket", 0.45f, 0.05f),
                MayaJacket = MakePbr("MAT_Char_Maya_Jacket", Color.white, "T_Char_Maya_Jacket", 0.48f, 0.05f),
                DenimCargo = MakePbr("MAT_Char_DenimCargo", new Color(0.85f, 0.85f, 0.90f, 1f), "T_Char_DenimCargo", 0.24f, 0.02f),
                Hair = MakePbr("MAT_Char_Hair", new Color(0.22f, 0.20f, 0.19f, 1f), "T_Char_Hair", 0.35f, 0f),
                Footwear = MakePbr("MAT_Char_Footwear", Color.white, "T_Char_Footwear", 0.50f, 0.05f),
                EyeIris = MakePbr("MAT_Char_EyeIris", new Color(0.14f, 0.11f, 0.09f, 1f), null, 0.92f, 0f),
                TariqAccent = MakePbr("MAT_Char_Tariq_Accent", new Color(0.831f, 0.627f, 0.090f, 1f), null, 0.82f, 0.85f),
                MayaAccent = MakePbr("MAT_Char_Maya_Accent", new Color(0.0f, 0.824f, 0.827f, 1f), null, 0.78f, 0.65f)
            };
            return set;
        }

        private static GameObject BuildOrLoadCharacterPrefab(
            string charName,
            string fbxPath,
            string prefabOutPath,
            AnimatorController controller,
            Material skinMat,
            Material jacketMat,
            Material pantsMat,
            Material hairMat,
            Material bootsMat,
            Material eyeMat,
            Material accentMat,
            float heightMeters,
            bool feminineBuild)
        {
            // Prefer exported Blender Humanoid FBX when present on disk
            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxAsset != null)
            {
                if (AssetImporter.GetAtPath(fbxPath) is ModelImporter mi && mi.animationType != ModelImporterAnimationType.Human)
                {
                    mi.animationType = ModelImporterAnimationType.Human;
                    mi.SaveAndReimport();
                    fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                }

                GameObject fbxInst = PrefabUtility.InstantiatePrefab(fbxAsset) as GameObject;
                try
                {
                    fbxInst.name = $"CHAR_{charName}_Humanoid";
                    Animator anim = fbxInst.GetComponentInChildren<Animator>(true) ?? fbxInst.AddComponent<Animator>();
                    anim.runtimeAnimatorController = controller;
                    anim.applyRootMotion = false;
                    anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    return PrefabUtility.SaveAsPrefabAsset(fbxInst, prefabOutPath);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(fbxInst);
                }
            }

            // Otherwise assemble a genuine 25-bone Humanoid SkinnedMeshRenderer anatomical prefab in C#
            GameObject root = new GameObject($"CHAR_{charName}_Humanoid");
            try
            {
                float scale = heightMeters / 1.78f;
                Transform[] bones = CreateHumanoidSkeleton(root.transform, scale, feminineBuild);

                // Build sculpted lofted anatomical SkinnedMeshRenderers (not bare cylinders/spheres)
                BuildSkinnedAnatomicalParts(
                    charName,
                    root.transform,
                    bones,
                    skinMat,
                    jacketMat,
                    pantsMat,
                    hairMat,
                    bootsMat,
                    eyeMat,
                    accentMat,
                    scale,
                    feminineBuild);

                Avatar avatar = BuildHumanoidAvatar(root, bones);
                avatar.name = $"Avatar_{charName}_Humanoid";
                if (AssetDatabase.LoadAssetAtPath<Avatar>($"{ModelRoot}/Avatar_{charName}.asset") != null)
                {
                    AssetDatabase.DeleteAsset($"{ModelRoot}/Avatar_{charName}.asset");
                }
                AssetDatabase.CreateAsset(avatar, $"{ModelRoot}/Avatar_{charName}.asset");

                Animator animator = root.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.updateMode = AnimatorUpdateMode.Normal;

                return PrefabUtility.SaveAsPrefabAsset(root, prefabOutPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Transform[] CreateHumanoidSkeleton(Transform root, float scale, bool feminine)
        {
            Transform[] list = new Transform[25];
            Transform Make(BoneId id, Transform parent, Vector3 localPos)
            {
                var go = new GameObject(id.ToString());
                go.transform.SetParent(parent, false);
                go.transform.localPosition = localPos * scale;
                go.transform.localRotation = Quaternion.identity;
                list[(int)id] = go.transform;
                return go.transform;
            }

            float shoulderX = feminine ? 0.155f : 0.19f;
            float hipX = feminine ? 0.104f : 0.098f;

            var hips = Make(BoneId.Hips, root, new Vector3(0f, 0.96f, 0f));
            var spine = Make(BoneId.Spine, hips, new Vector3(0f, 0.11f, 0f));
            var chest = Make(BoneId.Chest, spine, new Vector3(0f, 0.15f, 0f));
            var upperChest = Make(BoneId.UpperChest, chest, new Vector3(0f, 0.15f, 0f));
            var neck = Make(BoneId.Neck, upperChest, new Vector3(0f, 0.14f, 0f));
            var head = Make(BoneId.Head, neck, new Vector3(0f, 0.08f, 0.004f));
            Make(BoneId.Jaw, head, new Vector3(0f, 0.018f, 0.035f));
            Make(BoneId.LeftEye, head, new Vector3(-0.030f, 0.088f, 0.085f));
            Make(BoneId.RightEye, head, new Vector3(0.030f, 0.088f, 0.085f));

            var lSh = Make(BoneId.LeftShoulder, upperChest, new Vector3(-0.055f, 0.095f, 0f));
            var lUa = Make(BoneId.LeftUpperArm, lSh, new Vector3(-(shoulderX - 0.055f), 0f, 0f));
            var lLa = Make(BoneId.LeftLowerArm, lUa, new Vector3(-0.27f, 0f, 0f));
            Make(BoneId.LeftHand, lLa, new Vector3(-0.24f, 0f, 0f));

            var rSh = Make(BoneId.RightShoulder, upperChest, new Vector3(0.055f, 0.095f, 0f));
            var rUa = Make(BoneId.RightUpperArm, rSh, new Vector3((shoulderX - 0.055f), 0f, 0f));
            var rLa = Make(BoneId.RightLowerArm, rUa, new Vector3(0.27f, 0f, 0f));
            Make(BoneId.RightHand, rLa, new Vector3(0.24f, 0f, 0f));

            var lUl = Make(BoneId.LeftUpperLeg, hips, new Vector3(-hipX, -0.04f, 0f));
            var lLl = Make(BoneId.LeftLowerLeg, lUl, new Vector3(0f, -0.44f, 0f));
            var lFt = Make(BoneId.LeftFoot, lLl, new Vector3(0f, -0.43f, 0f));
            Make(BoneId.LeftToes, lFt, new Vector3(0f, -0.035f, 0.135f));

            var rUl = Make(BoneId.RightUpperLeg, hips, new Vector3(hipX, -0.04f, 0f));
            var rLl = Make(BoneId.RightLowerLeg, rUl, new Vector3(0f, -0.44f, 0f));
            var rFt = Make(BoneId.RightFoot, rLl, new Vector3(0f, -0.43f, 0f));
            Make(BoneId.RightToes, rFt, new Vector3(0f, -0.035f, 0.135f));

            return list;
        }

        private static Avatar BuildHumanoidAvatar(GameObject root, Transform[] bones)
        {
            Transform[] allTransforms = root.GetComponentsInChildren<Transform>();
            var skel = new SkeletonBone[allTransforms.Length];
            for (int i = 0; i < allTransforms.Length; i++)
            {
                skel[i] = new SkeletonBone
                {
                    name = allTransforms[i].name,
                    position = allTransforms[i].localPosition,
                    rotation = allTransforms[i].localRotation,
                    scale = allTransforms[i].localScale
                };
            }

            var human = new HumanBone[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                human[i] = new HumanBone
                {
                    humanName = ((BoneId)i).ToString(),
                    boneName = bones[i].name,
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
                armStretch = 0.04f,
                legStretch = 0.04f,
                feetSpacing = 0f,
                hasTranslationDoF = false
            };

            return AvatarBuilder.BuildHumanAvatar(root, desc);
        }

        private static void BuildSkinnedAnatomicalParts(
            string charName,
            Transform root,
            Transform[] bones,
            Material skinMat,
            Material jacketMat,
            Material pantsMat,
            Material hairMat,
            Material bootsMat,
            Material eyeMat,
            Material accentMat,
            float scale,
            bool feminine)
        {
            Matrix4x4[] bindPoses = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                bindPoses[i] = bones[i].worldToLocalMatrix * root.localToWorldMatrix;
            }

            void AttachSkinnedPart(string partName, Mesh mesh, Material mat)
            {
                mesh.name = $"M_{charName}_{partName}";
                mesh.bindposes = bindPoses;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();

                string meshPath = $"{GeneratedMeshRoot}/{mesh.name}.asset";
                if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) != null) AssetDatabase.DeleteAsset(meshPath);
                AssetDatabase.CreateAsset(mesh, meshPath);

                var go = new GameObject(partName);
                go.transform.SetParent(root, false);
                var smr = go.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                smr.bones = bones;
                smr.rootBone = bones[(int)BoneId.Hips];
                smr.sharedMaterial = mat;
                smr.updateWhenOffscreen = true;
            }

            // 1. Torso Jacket + Collar + Sleeves
            Mesh jacketMesh = CreateLoftedTorsoAndSleevesMesh(bones, scale, feminine);
            AttachSkinnedPart("Outfit_JacketTorso", jacketMesh, jacketMat);

            // 2. Sculpted Head & Neck + Arms/Hands (Skin)
            Mesh headSkinMesh = CreateSculptedHeadAndHandsMesh(bones, scale, feminine);
            AttachSkinnedPart("Body_HeadAndHands", headSkinMesh, skinMat);

            // 3. Trousers
            Mesh pantsMesh = CreateTrousersMesh(bones, scale, feminine);
            AttachSkinnedPart("Outfit_Trousers", pantsMesh, pantsMat);

            // 4. Styled Hair Crown
            Mesh hairMesh = CreateHairMesh(bones, scale, feminine);
            AttachSkinnedPart("Body_Hair", hairMesh, hairMat);

            // 5. Anatomical Boots
            Mesh bootMesh = CreateBootsMesh(bones, scale, feminine);
            AttachSkinnedPart("Outfit_Footwear", bootMesh, bootsMat);

            // 6. Facial Eyes + Accent Collar/Watch Trim
            Mesh detailMesh = CreateEyesAndTrimMesh(bones, scale, feminine);
            AttachSkinnedPart("Detail_EyesAndTrim", detailMesh, eyeMat);

            // Optional Watch/Collar Badge Accent
            Mesh accentMesh = CreateAccentTrimMesh(bones, scale);
            AttachSkinnedPart("Detail_SignatureTrim", accentMesh, accentMat);
        }

        private static void AddTubeSection(
            List<Vector3> verts,
            List<Vector2> uvs,
            List<BoneWeight> weights,
            List<int> tris,
            (Vector3 center, float rx, float rz, int boneA, int boneB, float wA)[] rings,
            int segs = 14)
        {
            int startVert = verts.Count;
            for (int r = 0; r < rings.Length; r++)
            {
                var spec = rings[r];
                float vCoord = (float)r / Mathf.Max(1, rings.Length - 1);
                for (int s = 0; s < segs; s++)
                {
                    float theta = (s / (float)segs) * Mathf.PI * 2f;
                    verts.Add(spec.center + new Vector3(Mathf.Cos(theta) * spec.rx, 0f, Mathf.Sin(theta) * spec.rz));
                    uvs.Add(new Vector2(s / (float)segs, vCoord));
                    weights.Add(new BoneWeight
                    {
                        boneIndex0 = spec.boneA,
                        weight0 = spec.wA,
                        boneIndex1 = spec.boneB,
                        weight1 = 1f - spec.wA
                    });
                }
            }

            for (int r = 0; r < rings.Length - 1; r++)
            {
                int row0 = startVert + r * segs;
                int row1 = startVert + (r + 1) * segs;
                for (int s = 0; s < segs; s++)
                {
                    int sn = (s + 1) % segs;
                    tris.Add(row0 + s);
                    tris.Add(row1 + sn);
                    tris.Add(row0 + sn);

                    tris.Add(row0 + s);
                    tris.Add(row1 + s);
                    tris.Add(row1 + sn);
                }
            }
        }

        private static Mesh PackMesh(List<Vector3> verts, List<Vector2> uvs, List<BoneWeight> weights, List<int> tris)
        {
            Mesh m = new Mesh();
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.boneWeights = weights.ToArray();
            m.SetTriangles(tris, 0);
            return m;
        }

        private static Mesh CreateLoftedTorsoAndSleevesMesh(Transform[] bones, float scale, bool feminine)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var weights = new List<BoneWeight>();
            var tris = new List<int>();

            float waist = feminine ? 0.130f : 0.155f;
            float chest = feminine ? 0.165f : 0.198f;
            AddTubeSection(verts, uvs, weights, tris, new[]
            {
                (bones[(int)BoneId.Hips].position + Vector3.down * (0.04f * scale), waist * 1.05f * scale, 0.118f * scale, (int)BoneId.Hips, (int)BoneId.Spine, 0.85f),
                (bones[(int)BoneId.Spine].position, waist * scale, 0.110f * scale, (int)BoneId.Spine, (int)BoneId.Hips, 0.80f),
                (bones[(int)BoneId.Chest].position, chest * 0.94f * scale, 0.122f * scale, (int)BoneId.Chest, (int)BoneId.Spine, 0.78f),
                (bones[(int)BoneId.UpperChest].position, chest * scale, 0.126f * scale, (int)BoneId.UpperChest, (int)BoneId.Chest, 0.80f),
                (bones[(int)BoneId.Neck].position + Vector3.down * (0.02f * scale), 0.088f * scale, 0.088f * scale, (int)BoneId.UpperChest, (int)BoneId.Neck, 0.70f),
            }, 16);

            // Sleeves from Shoulder -> Elbow
            void AddSleeve(BoneId ua, BoneId la, float sign)
            {
                Vector3 p0 = bones[(int)ua].position;
                Vector3 p1 = Vector3.Lerp(p0, bones[(int)la].position, 0.55f);
                Vector3 p2 = bones[(int)la].position;
                Vector3 p3 = Vector3.Lerp(p2, bones[(int)(ua == BoneId.LeftUpperArm ? BoneId.LeftHand : BoneId.RightHand)].position, 0.72f);

                int sSeg = 10;
                int baseIdx = verts.Count;
                Vector3[] centers = { p0, p1, p2, p3 };
                float[] rads = { 0.068f, 0.056f, 0.049f, 0.042f };
                for (int r = 0; r < centers.Length; r++)
                {
                    float rad = rads[r] * (feminine ? 0.88f : 1.0f) * scale;
                    for (int s = 0; s < sSeg; s++)
                    {
                        float th = (s / (float)sSeg) * Mathf.PI * 2f;
                        verts.Add(centers[r] + new Vector3(0f, Mathf.Cos(th) * rad, Mathf.Sin(th) * rad));
                        uvs.Add(new Vector2(s / (float)sSeg, r / 3f));
                        int bA = r < 2 ? (int)ua : (int)la;
                        int bB = r < 2 ? (int)la : (int)ua;
                        weights.Add(new BoneWeight { boneIndex0 = bA, weight0 = 0.85f, boneIndex1 = bB, weight1 = 0.15f });
                    }
                }
                for (int r = 0; r < centers.Length - 1; r++)
                {
                    int r0 = baseIdx + r * sSeg;
                    int r1 = baseIdx + (r + 1) * sSeg;
                    for (int s = 0; s < sSeg; s++)
                    {
                        int sn = (s + 1) % sSeg;
                        if (sign < 0f)
                        {
                            tris.Add(r0 + s); tris.Add(r0 + sn); tris.Add(r1 + sn);
                            tris.Add(r0 + s); tris.Add(r1 + sn); tris.Add(r1 + s);
                        }
                        else
                        {
                            tris.Add(r0 + s); tris.Add(r1 + sn); tris.Add(r0 + sn);
                            tris.Add(r0 + s); tris.Add(r1 + s); tris.Add(r1 + sn);
                        }
                    }
                }
            }

            AddSleeve(BoneId.LeftUpperArm, BoneId.LeftLowerArm, -1f);
            AddSleeve(BoneId.RightUpperArm, BoneId.RightLowerArm, 1f);
            return PackMesh(verts, uvs, weights, tris);
        }

        private static Mesh CreateSculptedHeadAndHandsMesh(Transform[] bones, float scale, bool feminine)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var weights = new List<BoneWeight>();
            var tris = new List<int>();

            float w = (feminine ? 0.90f : 1.0f) * scale;
            Vector3 headPos = bones[(int)BoneId.Head].position;
            Vector3 neckPos = bones[(int)BoneId.Neck].position;

            int headStart = verts.Count;
            AddTubeSection(verts, uvs, weights, tris, new[]
            {
                (neckPos + Vector3.down * (0.02f * scale), 0.056f * w, 0.058f * w, (int)BoneId.Neck, (int)BoneId.UpperChest, 0.75f),
                (headPos + Vector3.up * (0.01f * scale), 0.058f * w, 0.062f * w, (int)BoneId.Head, (int)BoneId.Neck, 0.80f),
                (headPos + new Vector3(0f, 0.04f * scale, 0.014f * scale), 0.074f * w, 0.088f * w, (int)BoneId.Head, (int)BoneId.Jaw, 0.78f),
                (headPos + new Vector3(0f, 0.095f * scale, 0.016f * scale), 0.083f * w, 0.100f * w, (int)BoneId.Head, (int)BoneId.Head, 1f),
                (headPos + new Vector3(0f, 0.155f * scale, 0.008f * scale), 0.084f * w, 0.102f * w, (int)BoneId.Head, (int)BoneId.Head, 1f),
                (headPos + new Vector3(0f, 0.215f * scale, -0.004f * scale), 0.070f * w, 0.090f * w, (int)BoneId.Head, (int)BoneId.Head, 1f),
                (headPos + new Vector3(0f, 0.248f * scale, -0.008f * scale), 0.026f * w, 0.032f * w, (int)BoneId.Head, (int)BoneId.Head, 1f),
            }, 16);

            // Sculpt prominent nose bridge & brow ridge on anterior +Z vertices
            for (int i = headStart; i < verts.Count; i++)
            {
                Vector3 v = verts[i];
                float dy = v.y - headPos.y;
                if (v.z > headPos.z + 0.075f * w && Mathf.Abs(v.x) < 0.020f * w && dy > 0.068f * scale && dy < 0.135f * scale)
                {
                    v.z += 0.020f * scale;
                    verts[i] = v;
                }
            }

            // Forearms + Hands
            void AddHand(BoneId la, BoneId ha)
            {
                Vector3 elbow = bones[(int)la].position;
                Vector3 wrist = bones[(int)ha].position;
                Vector3 start = Vector3.Lerp(elbow, wrist, 0.68f);
                Vector3 tip = wrist + (wrist - elbow).normalized * (0.13f * scale);
                AddTubeSection(verts, uvs, weights, tris, new[]
                {
                    (start + Vector3.down * (0.02f * scale), 0.034f * w, 0.032f * w, (int)la, (int)ha, 0.5f),
                    (wrist + Vector3.down * (0.01f * scale), 0.036f * w, 0.022f * w, (int)ha, (int)la, 0.85f),
                    (tip + Vector3.down * (0.01f * scale), 0.028f * w, 0.015f * w, (int)ha, (int)ha, 1f),
                }, 10);
            }
            AddHand(BoneId.LeftLowerArm, BoneId.LeftHand);
            AddHand(BoneId.RightLowerArm, BoneId.RightHand);
            return PackMesh(verts, uvs, weights, tris);
        }

        private static Mesh CreateTrousersMesh(Transform[] bones, float scale, bool feminine)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var weights = new List<BoneWeight>();
            var tris = new List<int>();
            float s = (feminine ? 0.91f : 1.0f) * scale;

            // Pelvis / Waistband
            AddTubeSection(verts, uvs, weights, tris, new[]
            {
                (bones[(int)BoneId.Hips].position + Vector3.down * (0.08f * scale), 0.160f * s, 0.120f * scale, (int)BoneId.Hips, (int)BoneId.Spine, 1f),
                (bones[(int)BoneId.Hips].position + Vector3.up * (0.02f * scale), 0.155f * s, 0.118f * scale, (int)BoneId.Hips, (int)BoneId.Spine, 0.9f),
            }, 14);

            void AddLeg(BoneId ul, BoneId ll, BoneId ft)
            {
                Vector3 pHip = bones[(int)ul].position;
                Vector3 pKnee = bones[(int)ll].position;
                Vector3 pAnkle = bones[(int)ft].position + Vector3.up * (0.04f * scale);
                AddTubeSection(verts, uvs, weights, tris, new[]
                {
                    (pAnkle, 0.052f * s, 0.056f * s, (int)ll, (int)ft, 0.85f),
                    (Vector3.Lerp(pAnkle, pKnee, 0.55f), 0.068f * s, 0.072f * s, (int)ll, (int)ul, 0.85f),
                    (pKnee, 0.068f * s, 0.072f * s, (int)ll, (int)ul, 0.55f),
                    (Vector3.Lerp(pKnee, pHip, 0.5f), 0.085f * s, 0.092f * s, (int)ul, (int)ll, 0.85f),
                    (pHip + Vector3.up * (0.04f * scale), 0.096f * s, 0.104f * s, (int)ul, (int)BoneId.Hips, 0.7f),
                }, 12);
            }
            AddLeg(BoneId.LeftUpperLeg, BoneId.LeftLowerLeg, BoneId.LeftFoot);
            AddLeg(BoneId.RightUpperLeg, BoneId.RightLowerLeg, BoneId.RightFoot);
            return PackMesh(verts, uvs, weights, tris);
        }

        private static Mesh CreateHairMesh(Transform[] bones, float scale, bool feminine)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var weights = new List<BoneWeight>();
            var tris = new List<int>();
            Vector3 hp = bones[(int)BoneId.Head].position;
            float w = (feminine ? 0.92f : 1.0f) * scale;

            if (feminine)
            {
                AddTubeSection(verts, uvs, weights, tris, new[]
                {
                    (hp + new Vector3(0f, 0.04f * scale, -0.025f * scale), 0.094f * w, 0.096f * w, (int)BoneId.Head, (int)BoneId.Neck, 0.9f),
                    (hp + new Vector3(0f, 0.145f * scale, -0.012f * scale), 0.096f * w, 0.110f * w, (int)BoneId.Head, (int)BoneId.Head, 1f),
                    (hp + new Vector3(0f, 0.215f * scale, -0.008f * scale), 0.088f * w, 0.105f * w, (int)BoneId.Head, (int)BoneId.Head, 1f),
                    (hp + new Vector3(0f, 0.262f * scale, -0.012f * scale), 0.042f * w, 0.052f * w, (int)BoneId.Head, (int)BoneId.Head, 1f),
                }, 14);
            }
            else
            {
                AddTubeSection(verts, uvs, weights, tris, new[]
                {
                    (hp + new Vector3(0f, 0.135f * scale, -0.015f * scale), 0.088f * w, 0.102f * w, (int)BoneId.Head, (int)BoneId.Head, 1f),
                    (hp + new Vector3(0f, 0.205f * scale, -0.002f * scale), 0.088f * w, 0.108f * w, (int)BoneId.Head, (int)BoneId.Head, 1f),
                    (hp + new Vector3(0f, 0.258f * scale, 0.015f * scale), 0.064f * w, 0.084f * w, (int)BoneId.Head, (int)BoneId.Head, 1f),
                    (hp + new Vector3(0f, 0.278f * scale, 0.022f * scale), 0.024f * w, 0.032f * w, (int)BoneId.Head, (int)BoneId.Head, 1f),
                }, 14);
            }
            return PackMesh(verts, uvs, weights, tris);
        }

        private static Mesh CreateBootsMesh(Transform[] bones, float scale, bool feminine)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var weights = new List<BoneWeight>();
            var tris = new List<int>();
            float w = (feminine ? 0.90f : 1.0f) * scale;

            void AddBoot(BoneId ll, BoneId ft, BoneId to)
            {
                Vector3 ankle = bones[(int)ft].position;
                Vector3 toes = bones[(int)to].position;
                Vector3 center = (ankle + toes) * 0.5f + Vector3.up * (0.012f * scale);
                AddTubeSection(verts, uvs, weights, tris, new[]
                {
                    (center + Vector3.down * (0.048f * scale), 0.056f * w, 0.135f * scale, (int)ft, (int)to, 0.65f),
                    (center + Vector3.up * (0.015f * scale), 0.052f * w, 0.118f * scale, (int)ft, (int)to, 0.72f),
                    (ankle + Vector3.up * (0.055f * scale), 0.050f * w, 0.062f * scale, (int)ft, (int)ll, 0.9f),
                }, 10);
            }
            AddBoot(BoneId.LeftLowerLeg, BoneId.LeftFoot, BoneId.LeftToes);
            AddBoot(BoneId.RightLowerLeg, BoneId.RightFoot, BoneId.RightToes);
            return PackMesh(verts, uvs, weights, tris);
        }

        private static Mesh CreateEyesAndTrimMesh(Transform[] bones, float scale, bool feminine)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var weights = new List<BoneWeight>();
            var tris = new List<int>();

            void AddEye(BoneId eyeId)
            {
                Vector3 p = bones[(int)eyeId].position;
                AddTubeSection(verts, uvs, weights, tris, new[]
                {
                    (p + Vector3.down * (0.011f * scale), 0.004f * scale, 0.004f * scale, (int)eyeId, (int)BoneId.Head, 0.85f),
                    (p, 0.013f * scale, 0.013f * scale, (int)eyeId, (int)BoneId.Head, 0.85f),
                    (p + Vector3.up * (0.011f * scale), 0.004f * scale, 0.004f * scale, (int)eyeId, (int)BoneId.Head, 0.85f),
                }, 8);
            }
            AddEye(BoneId.LeftEye);
            AddEye(BoneId.RightEye);
            return PackMesh(verts, uvs, weights, tris);
        }

        private static Mesh CreateAccentTrimMesh(Transform[] bones, float scale)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var weights = new List<BoneWeight>();
            var tris = new List<int>();
            Vector3 collar = bones[(int)BoneId.Neck].position + Vector3.down * (0.015f * scale);
            AddTubeSection(verts, uvs, weights, tris, new[]
            {
                (collar + Vector3.down * (0.018f * scale), 0.095f * scale, 0.095f * scale, (int)BoneId.UpperChest, (int)BoneId.Neck, 0.8f),
                (collar + Vector3.up * (0.018f * scale), 0.092f * scale, 0.092f * scale, (int)BoneId.UpperChest, (int)BoneId.Neck, 0.8f),
            }, 14);
            return PackMesh(verts, uvs, weights, tris);
        }

        private static Dictionary<string, AnimationClip> BakeHumanoidAnimationClips()
        {
            var clips = new Dictionary<string, AnimationClip>();

            void BakeClip(string clipName, float lengthSec, bool loop, Func<float, Dictionary<string, float>> eval)
            {
                var clip = new AnimationClip { name = clipName, frameRate = 30f };
                int steps = Mathf.Max(8, Mathf.RoundToInt(lengthSec * 30f));
                var curveKeys = new Dictionary<string, List<Keyframe>>();

                for (int i = 0; i <= steps; i++)
                {
                    float u = (float)i / steps;
                    float t = u * lengthSec;
                    Dictionary<string, float> muscles = eval(u);
                    foreach (var kv in muscles)
                    {
                        if (!curveKeys.TryGetValue(kv.Key, out var list))
                        {
                            list = new List<Keyframe>(steps + 1);
                            curveKeys[kv.Key] = list;
                        }
                        list.Add(new Keyframe(t, kv.Value));
                    }
                }

                string[] validMuscles = HumanTrait.MuscleName;
                var validSet = new HashSet<string>(validMuscles);

                foreach (var kv in curveKeys)
                {
                    if (!kv.Key.StartsWith("Root", StringComparison.Ordinal) && !validSet.Contains(kv.Key))
                    {
                        continue;
                    }
                    var curve = new AnimationCurve(kv.Value.ToArray());
                    var binding = EditorCurveBinding.FloatCurve("", typeof(Animator), kv.Key);
                    AnimationUtility.SetEditorCurve(clip, binding, curve);
                }

                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = loop;
                settings.loopBlend = loop;
                settings.keepOriginalOrientation = true;
                settings.keepOriginalPositionY = true;
                settings.keepOriginalPositionXZ = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);

                string assetPath = $"{ClipRoot}/{clipName}.anim";
                if (AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath) != null) AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.CreateAsset(clip, assetPath);
                clips[clipName] = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            }

            // Procedural Humanoid Muscle Curves (verified against HumanTrait.MuscleName)
            BakeClip("Idle_Loop", 2.4f, true, u =>
            {
                float br = Mathf.Sin(u * Mathf.PI * 2f);
                return new Dictionary<string, float>
                {
                    { "RootT.y", 0.96f + br * 0.005f },
                    { "Spine Front-Back", br * 0.04f },
                    { "Chest Front-Back", br * 0.06f },
                    { "Left Arm Down-Up", -0.78f },
                    { "Right Arm Down-Up", -0.78f },
                    { "Left Forearm Stretch", 0.85f },
                    { "Right Forearm Stretch", 0.85f },
                    { "Left Upper Leg Front-Back", 0.04f },
                    { "Right Upper Leg Front-Back", 0.04f },
                    { "Left Lower Leg Stretch", 0.94f },
                    { "Right Lower Leg Stretch", 0.94f }
                };
            });

            Dictionary<string, float> EvalStrafeLocomotion(float u, float hipAmp, float kneeAmp, float armAmp, float rootAmp, float crouchBias)
            {
                float p = u * Mathf.PI * 2f;
                float s = Mathf.Sin(p);
                float c = Mathf.Cos(p);
                return new Dictionary<string, float>
                {
                    { "RootT.y", (0.94f - crouchBias * 0.25f) + Mathf.Abs(Mathf.Sin(p * 2f)) * rootAmp },
                    { "Spine Front-Back", -0.08f - crouchBias * 0.35f },
                    { "Spine Twist Left-Right", -s * 0.14f },
                    { "Chest Twist Left-Right", -s * 0.18f },
                    { "Left Upper Leg Front-Back", s * hipAmp + crouchBias * 0.45f },
                    { "Right Upper Leg Front-Back", -s * hipAmp + crouchBias * 0.45f },
                    { "Left Lower Leg Stretch", 0.88f - Mathf.Max(0f, c) * kneeAmp - crouchBias * 0.4f },
                    { "Right Lower Leg Stretch", 0.88f - Mathf.Max(0f, -c) * kneeAmp - crouchBias * 0.4f },
                    { "Left Foot Up-Down", -c * 0.25f },
                    { "Right Foot Up-Down", c * 0.25f },
                    { "Left Arm Down-Up", -0.75f },
                    { "Right Arm Down-Up", -0.75f },
                    { "Left Arm Front-Back", -s * armAmp },
                    { "Right Arm Front-Back", s * armAmp },
                    { "Left Forearm Stretch", 0.72f - Mathf.Max(0f, -s) * 0.25f },
                    { "Right Forearm Stretch", 0.72f - Mathf.Max(0f, s) * 0.25f }
                };
            }

            BakeClip("Walk_Fwd", 0.96f, true, u => EvalStrafeLocomotion(u, 0.36f, 0.45f, 0.32f, 0.025f, 0f));
            BakeClip("Walk_Back", 0.96f, true, u => EvalStrafeLocomotion(1f - u, 0.28f, 0.35f, 0.24f, 0.020f, 0f));
            BakeClip("Walk_Strafe_L", 0.90f, true, u =>
            {
                var d = EvalStrafeLocomotion(u, 0.22f, 0.30f, 0.18f, 0.02f, 0f);
                d["Left Upper Leg In-Out"] = 0.22f * Mathf.Sin(u * Mathf.PI * 2f);
                d["Right Upper Leg In-Out"] = -0.22f * Mathf.Sin(u * Mathf.PI * 2f);
                return d;
            });
            BakeClip("Walk_Strafe_R", 0.90f, true, u =>
            {
                var d = EvalStrafeLocomotion(u, 0.22f, 0.30f, 0.18f, 0.02f, 0f);
                d["Left Upper Leg In-Out"] = -0.22f * Mathf.Sin(u * Mathf.PI * 2f);
                d["Right Upper Leg In-Out"] = 0.22f * Mathf.Sin(u * Mathf.PI * 2f);
                return d;
            });
            BakeClip("Jog_Fwd", 0.68f, true, u => EvalStrafeLocomotion(u, 0.56f, 0.68f, 0.54f, 0.055f, 0f));
            BakeClip("Sprint_Fwd", 0.52f, true, u => EvalStrafeLocomotion(u, 0.78f, 0.85f, 0.78f, 0.075f, 0f));
            BakeClip("Crouch_Idle", 1.8f, true, u => EvalStrafeLocomotion(u * 0.25f, 0.02f, 0.05f, 0.04f, 0.005f, 1.0f));
            BakeClip("Crouch_Walk", 0.85f, true, u => EvalStrafeLocomotion(u, 0.26f, 0.32f, 0.18f, 0.020f, 1.0f));
            BakeClip("Turn_90", 0.50f, false, u => EvalStrafeLocomotion(u, 0.20f, 0.28f, 0.15f, 0.015f, 0f));
            BakeClip("Turn_180", 0.65f, false, u => EvalStrafeLocomotion(u, 0.25f, 0.34f, 0.22f, 0.020f, 0f));

            BakeClip("Jump_Start", 0.45f, false, u => new Dictionary<string, float>
            {
                { "RootT.y", 0.96f + Mathf.Sin(u * Mathf.PI * 0.5f) * 0.08f },
                { "Left Upper Leg Front-Back", 0.42f * u },
                { "Right Upper Leg Front-Back", -0.18f * u },
                { "Left Lower Leg Stretch", 0.92f - 0.55f * u },
                { "Right Lower Leg Stretch", 0.92f - 0.22f * u },
                { "Left Arm Down-Up", -0.60f + 0.45f * u },
                { "Right Arm Down-Up", -0.60f + 0.45f * u }
            });

            BakeClip("Fall_Loop", 0.80f, true, u => new Dictionary<string, float>
            {
                { "RootT.y", 0.98f },
                { "Left Upper Leg Front-Back", 0.32f },
                { "Right Upper Leg Front-Back", 0.16f },
                { "Left Lower Leg Stretch", 0.48f },
                { "Right Lower Leg Stretch", 0.65f },
                { "Left Arm Down-Up", -0.25f + 0.08f * Mathf.Sin(u * Mathf.PI * 2f) },
                { "Right Arm Down-Up", -0.25f - 0.08f * Mathf.Sin(u * Mathf.PI * 2f) }
            });

            BakeClip("Land_Recovery", 0.38f, false, u =>
            {
                float dip = Mathf.Sin(u * Mathf.PI);
                return new Dictionary<string, float>
                {
                    { "RootT.y", 0.96f - dip * 0.16f },
                    { "Spine Front-Back", -0.22f * dip },
                    { "Left Upper Leg Front-Back", 0.45f * dip },
                    { "Right Upper Leg Front-Back", 0.45f * dip },
                    { "Left Lower Leg Stretch", 0.92f - 0.48f * dip },
                    { "Right Lower Leg Stretch", 0.92f - 0.48f * dip }
                };
            });

            BakeClip("Swim_Idle", 1.6f, true, u =>
            {
                float s = Mathf.Sin(u * Mathf.PI * 2f);
                return new Dictionary<string, float>
                {
                    { "RootT.y", 0.96f + s * 0.03f },
                    { "Left Arm Down-Up", -0.42f + s * 0.16f },
                    { "Right Arm Down-Up", -0.42f - s * 0.16f },
                    { "Left Forearm Stretch", 0.55f },
                    { "Right Forearm Stretch", 0.55f },
                    { "Left Upper Leg Front-Back", s * 0.18f },
                    { "Right Upper Leg Front-Back", -s * 0.18f }
                };
            });

            BakeClip("Swim_Forward", 1.1f, true, u =>
            {
                float p = u * Mathf.PI * 2f;
                float s = Mathf.Sin(p);
                return new Dictionary<string, float>
                {
                    { "RootT.y", 0.96f },
                    { "RootQ.x", 0.52f }, // pitch horizontal forward into water breaststroke
                    { "Spine Front-Back", 0.18f },
                    { "Neck Nod Down-Up", 0.42f },
                    { "Left Arm Down-Up", -0.25f + Mathf.Cos(p) * 0.45f },
                    { "Right Arm Down-Up", -0.25f + Mathf.Cos(p) * 0.45f },
                    { "Left Arm Front-Back", 0.35f + s * 0.42f },
                    { "Right Arm Front-Back", 0.35f + s * 0.42f },
                    { "Left Upper Leg Front-Back", -0.15f + s * 0.35f },
                    { "Right Upper Leg Front-Back", -0.15f - s * 0.35f }
                };
            });

            BakeClip("Aim_UpperBody", 1.2f, true, u => new Dictionary<string, float>
            {
                { "Chest Twist Left-Right", -0.15f },
                { "Right Arm Down-Up", -0.15f },
                { "Right Arm Front-Back", 0.68f },
                { "Right Forearm Stretch", 0.78f },
                { "Left Arm Down-Up", -0.25f },
                { "Left Arm Front-Back", 0.55f },
                { "Left Forearm Stretch", 0.52f }
            });

            BakeClip("Interact_Reach", 0.65f, false, u =>
            {
                float r = Mathf.Sin(u * Mathf.PI);
                return new Dictionary<string, float>
                {
                    { "Chest Front-Back", -0.14f * r },
                    { "Right Arm Down-Up", -0.65f + 0.55f * r },
                    { "Right Arm Front-Back", 0.62f * r },
                    { "Right Forearm Stretch", 0.85f }
                };
            });

            BakeClip("Dialogue_Gesture", 1.4f, false, u =>
            {
                float wave = Mathf.Sin(u * Mathf.PI * 2f) * Mathf.Sin(u * Mathf.PI);
                return new Dictionary<string, float>
                {
                    { "Head Nod Down-Up", 0.12f * Mathf.Sin(u * Mathf.PI * 3f) },
                    { "Right Arm Down-Up", -0.52f + 0.32f * Mathf.Abs(wave) },
                    { "Right Arm Front-Back", 0.38f + 0.22f * wave },
                    { "Right Forearm Stretch", 0.42f },
                    { "Left Arm Down-Up", -0.65f + 0.14f * wave },
                    { "Left Forearm Stretch", 0.58f }
                };
            });

            BakeClip("Vehicle_DriveSeat", 1.5f, true, u => new Dictionary<string, float>
            {
                { "RootT.y", 0.62f },
                { "Spine Front-Back", 0.08f },
                { "Left Upper Leg Front-Back", 0.72f },
                { "Right Upper Leg Front-Back", 0.72f },
                { "Left Lower Leg Stretch", 0.32f },
                { "Right Lower Leg Stretch", 0.32f },
                { "Left Arm Down-Up", -0.22f },
                { "Right Arm Down-Up", -0.22f },
                { "Left Arm Front-Back", 0.45f },
                { "Right Arm Front-Back", 0.45f },
                { "Left Forearm Stretch", 0.52f },
                { "Right Forearm Stretch", 0.52f }
            });

            return clips;
        }

        private static AvatarMask CreateUpperBodyMask()
        {
            var mask = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var part = (AvatarMaskBodyPart)i;
                bool active = part == AvatarMaskBodyPart.Body ||
                              part == AvatarMaskBodyPart.Head ||
                              part == AvatarMaskBodyPart.LeftArm ||
                              part == AvatarMaskBodyPart.RightArm ||
                              part == AvatarMaskBodyPart.LeftFingers ||
                              part == AvatarMaskBodyPart.RightFingers;
                mask.SetHumanoidBodyPartActive(part, active);
            }

            if (AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperMaskPath) != null) AssetDatabase.DeleteAsset(UpperMaskPath);
            AssetDatabase.CreateAsset(mask, UpperMaskPath);
            return AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperMaskPath);
        }

        private static AnimatorController BuildHumanoidAnimatorController(Dictionary<string, AnimationClip> clips, AvatarMask upperMask)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            {
                AssetDatabase.DeleteAsset(ControllerPath);
            }

            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            // Parameters aligned identically with MotionMatchingController hashes
            ctrl.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("TurnDelta", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("GaitPhase", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("GaitPhaseSin", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("GaitPhaseCos", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("VerticalVelocity", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("LeftFootContact", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("RightFootContact", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("IsSprinting", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("IsCrouching", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("IsSwimming", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("IsAiming", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("InVehicle", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("TriggerJump", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("TriggerLand", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("TriggerInteract", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("TriggerGesture", AnimatorControllerParameterType.Trigger);

            // Enable IK Pass on Base Layer cleanly so OnAnimatorIK runs automatically
            var layers = ctrl.layers;
            layers[0].name = "BaseLocomotion";
            layers[0].iKPass = true;
            ctrl.layers = layers;

            var baseSm = ctrl.layers[0].stateMachine;

            var locoTree = new BlendTree
            {
                name = "Locomotion_2D_BlendTree",
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = "MoveX",
                blendParameterY = "MoveZ",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(locoTree, ctrl);
            locoTree.AddChild(clips["Idle_Loop"], new Vector2(0f, 0f));
            locoTree.AddChild(clips["Walk_Fwd"], new Vector2(0f, 2.2f));
            locoTree.AddChild(clips["Walk_Back"], new Vector2(0f, -1.6f));
            locoTree.AddChild(clips["Walk_Strafe_L"], new Vector2(-1.8f, 0f));
            locoTree.AddChild(clips["Walk_Strafe_R"], new Vector2(1.8f, 0f));
            locoTree.AddChild(clips["Jog_Fwd"], new Vector2(0f, 5.2f));
            locoTree.AddChild(clips["Sprint_Fwd"], new Vector2(0f, 7.8f));

            var idleState = baseSm.AddState("Idle", new Vector3(240, 120, 0));
            idleState.motion = clips["Idle_Loop"];
            baseSm.defaultState = idleState;

            var locoState = baseSm.AddState("Locomotion", new Vector3(500, 120, 0));
            locoState.motion = locoTree;

            var crouchIdleState = baseSm.AddState("CrouchIdle", new Vector3(240, 260, 0));
            crouchIdleState.motion = clips["Crouch_Idle"];

            var crouchWalkState = baseSm.AddState("CrouchWalk", new Vector3(500, 260, 0));
            crouchWalkState.motion = clips["Crouch_Walk"];

            var swimIdleState = baseSm.AddState("SwimIdle", new Vector3(240, -100, 0));
            swimIdleState.motion = clips["Swim_Idle"];

            var swimFwdState = baseSm.AddState("SwimForward", new Vector3(500, -100, 0));
            swimFwdState.motion = clips["Swim_Forward"];

            var jumpState = baseSm.AddState("Jump", new Vector3(760, 20, 0));
            jumpState.motion = clips["Jump_Start"];

            var fallState = baseSm.AddState("Fall", new Vector3(760, 110, 0));
            fallState.motion = clips["Fall_Loop"];

            var landState = baseSm.AddState("Land", new Vector3(760, 200, 0));
            landState.motion = clips["Land_Recovery"];

            var vehicleDriveState = baseSm.AddState("VehicleDrive", new Vector3(500, 380, 0));
            vehicleDriveState.motion = clips["Vehicle_DriveSeat"];

            void AddT(AnimatorState from, AnimatorState to, float dur, bool hasExit, float exitTime, Action<AnimatorStateTransition> cfg)
            {
                var tr = from.AddTransition(to);
                tr.duration = dur;
                tr.hasExitTime = hasExit;
                tr.exitTime = exitTime;
                tr.hasFixedDuration = true;
                cfg?.Invoke(tr);
            }

            AddT(idleState, locoState, 0.14f, false, 0f, t => t.AddCondition(AnimatorConditionMode.Greater, 0.20f, "MoveSpeed"));
            AddT(locoState, idleState, 0.18f, false, 0f, t => t.AddCondition(AnimatorConditionMode.Less, 0.15f, "MoveSpeed"));

            AddT(idleState, crouchIdleState, 0.14f, false, 0f, t => t.AddCondition(AnimatorConditionMode.If, 0f, "IsCrouching"));
            AddT(locoState, crouchWalkState, 0.14f, false, 0f, t => t.AddCondition(AnimatorConditionMode.If, 0f, "IsCrouching"));
            AddT(crouchIdleState, crouchWalkState, 0.12f, false, 0f, t => t.AddCondition(AnimatorConditionMode.Greater, 0.2f, "MoveSpeed"));
            AddT(crouchWalkState, crouchIdleState, 0.14f, false, 0f, t => t.AddCondition(AnimatorConditionMode.Less, 0.15f, "MoveSpeed"));
            AddT(crouchIdleState, idleState, 0.14f, false, 0f, t => t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCrouching"));
            AddT(crouchWalkState, locoState, 0.14f, false, 0f, t => t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCrouching"));

            AddT(swimIdleState, swimFwdState, 0.18f, false, 0f, t => t.AddCondition(AnimatorConditionMode.Greater, 0.30f, "MoveSpeed"));
            AddT(swimFwdState, swimIdleState, 0.20f, false, 0f, t => t.AddCondition(AnimatorConditionMode.Less, 0.25f, "MoveSpeed"));
            AddT(swimIdleState, idleState, 0.22f, false, 0f, t => t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsSwimming"));
            AddT(swimFwdState, locoState, 0.22f, false, 0f, t => t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsSwimming"));

            var anySwim = baseSm.AddAnyStateTransition(swimIdleState);
            anySwim.duration = 0.20f;
            anySwim.canTransitionToSelf = false;
            anySwim.AddCondition(AnimatorConditionMode.If, 0f, "IsSwimming");
            anySwim.AddCondition(AnimatorConditionMode.Less, 0.30f, "MoveSpeed");

            var anyJump = baseSm.AddAnyStateTransition(jumpState);
            anyJump.duration = 0.08f;
            anyJump.canTransitionToSelf = false;
            anyJump.AddCondition(AnimatorConditionMode.If, 0f, "TriggerJump");

            var anyVehicle = baseSm.AddAnyStateTransition(vehicleDriveState);
            anyVehicle.duration = 0.16f;
            anyVehicle.canTransitionToSelf = false;
            anyVehicle.AddCondition(AnimatorConditionMode.If, 0f, "InVehicle");
            AddT(vehicleDriveState, idleState, 0.18f, false, 0f, t => t.AddCondition(AnimatorConditionMode.IfNot, 0f, "InVehicle"));

            AddT(jumpState, fallState, 0.16f, true, 0.85f, null);
            AddT(fallState, landState, 0.10f, false, 0f, t => t.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded"));
            AddT(landState, idleState, 0.18f, true, 0.85f, t => t.AddCondition(AnimatorConditionMode.Less, 0.2f, "MoveSpeed"));
            AddT(landState, locoState, 0.16f, true, 0.80f, t => t.AddCondition(AnimatorConditionMode.Greater, 0.2f, "MoveSpeed"));

            // Layer 2: Upper Body Masked Layer (Aim, Interact Reach, Dialogue Gestures)
            var upperSm = new AnimatorStateMachine { name = "UpperBodyActions" };
            AssetDatabase.AddObjectToAsset(upperSm, ctrl);

            var upperEmpty = upperSm.AddState("UpperEmpty", new Vector3(240, 120, 0));
            upperSm.defaultState = upperEmpty;

            var upperAim = upperSm.AddState("AimUpperBody", new Vector3(500, 40, 0));
            upperAim.motion = clips["Aim_UpperBody"];

            var upperInteract = upperSm.AddState("InteractReach", new Vector3(500, 140, 0));
            upperInteract.motion = clips["Interact_Reach"];

            var upperGesture = upperSm.AddState("DialogueGesture", new Vector3(500, 240, 0));
            upperGesture.motion = clips["Dialogue_Gesture"];

            AddT(upperEmpty, upperAim, 0.12f, false, 0f, t => t.AddCondition(AnimatorConditionMode.If, 0f, "IsAiming"));
            AddT(upperAim, upperEmpty, 0.14f, false, 0f, t => t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsAiming"));
            AddT(upperEmpty, upperInteract, 0.10f, false, 0f, t => t.AddCondition(AnimatorConditionMode.If, 0f, "TriggerInteract"));
            AddT(upperInteract, upperEmpty, 0.14f, true, 0.88f, null);
            AddT(upperEmpty, upperGesture, 0.12f, false, 0f, t => t.AddCondition(AnimatorConditionMode.If, 0f, "TriggerGesture"));
            AddT(upperGesture, upperEmpty, 0.18f, true, 0.90f, null);

            ctrl.AddLayer(new AnimatorControllerLayer
            {
                name = "UpperBodyOverlay",
                avatarMask = upperMask,
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Override,
                stateMachine = upperSm,
                iKPass = true
            });

            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        private static void EnsureDirectories()
        {
            void MakePath(string fullPath)
            {
                string[] parts = fullPath.Split('/');
                string cur = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = $"{cur}/{parts[i]}";
                    if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                    cur = next;
                }
            }

            MakePath(TexRoot);
            MakePath(MatRoot);
            MakePath(GeneratedMeshRoot);
            MakePath(ClipRoot);
            MakePath(PrefabRoot);
            MakePath(DataRoot);
        }
    }
}
