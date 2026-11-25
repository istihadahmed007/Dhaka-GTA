using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Rendering;

public static class BuildDhakaCharacterAssets
{
    const string RootDir = "Assets/Characters/DhakaProtagonist";
    const string TexDir = RootDir + "/Textures";
    const string MatDir = RootDir + "/Materials";
    const string MeshDir = RootDir + "/Meshes";
    const string PrefabPath = "Assets/Prefabs/DhakaProtagonist.prefab";

    enum B
    {
        Hips = 0,
        Spine = 1,
        Chest = 2,
        UpperChest = 3,
        Neck = 4,
        Head = 5,
        Jaw = 6,
        LeftEye = 7,
        RightEye = 8,
        LeftShoulder = 9,
        LeftUpperArm = 10,
        LeftLowerArm = 11,
        LeftHand = 12,
        RightShoulder = 13,
        RightUpperArm = 14,
        RightLowerArm = 15,
        RightHand = 16,
        LeftUpperLeg = 17,
        LeftLowerLeg = 18,
        LeftFoot = 19,
        LeftToes = 20,
        RightUpperLeg = 21,
        RightLowerLeg = 22,
        RightFoot = 23,
        RightToes = 24
    }

    public static string BuildAll()
    {
        EnsureFolders();
        GenerateTextures();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureTextureImporters();

        var mats = CreateMaterials();

        GameObject root = new GameObject("DhakaProtagonist");
        try
        {
            Transform[] bones = CreateSkeleton(root.transform);
            Avatar avatar = CreateHumanoidAvatar(root, bones);
            var existingAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(RootDir + "/DhakaProtagonistAvatar.asset");
            if (existingAvatar != null) AssetDatabase.DeleteAsset(RootDir + "/DhakaProtagonistAvatar.asset");
            AssetDatabase.CreateAsset(avatar, RootDir + "/DhakaProtagonistAvatar.asset");

            Matrix4x4[] bindPoses = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                bindPoses[i] = bones[i].worldToLocalMatrix * root.transform.localToWorldMatrix;
            }

            Mesh headFaceMesh = BuildHeadAndFaceMesh(bindPoses);
            Mesh armsHandsMesh = BuildForearmsAndHandsMesh(bindPoses);
            Mesh hairMesh = BuildHairMesh(bindPoses);
            Mesh jacketMesh = BuildJacketAndTorsoMesh(bindPoses);
            Mesh pantsMesh = BuildTrousersMesh(bindPoses);
            Mesh shoeMesh = BuildShoesMesh(bindPoses);

            SaveMeshAsset(headFaceMesh, MeshDir + "/M_HeadAndFace.asset");
            SaveMeshAsset(armsHandsMesh, MeshDir + "/M_ArmsAndHands.asset");
            SaveMeshAsset(hairMesh, MeshDir + "/M_Hair.asset");
            SaveMeshAsset(jacketMesh, MeshDir + "/M_JacketTorso.asset");
            SaveMeshAsset(pantsMesh, MeshDir + "/M_Trousers.asset");
            SaveMeshAsset(shoeMesh, MeshDir + "/M_Shoes.asset");

            var visuals = new GameObject("Visuals");
            visuals.transform.SetParent(root.transform, false);

            CreateSkinnedPart("Body_HeadAndFace", visuals.transform, headFaceMesh, bones, bones[(int)B.Head],
                new[] { mats.headSkin, mats.eyeSclera, mats.eyeIris, mats.hair });
            CreateSkinnedPart("Body_ArmsAndHands", visuals.transform, armsHandsMesh, bones, bones[(int)B.UpperChest],
                new[] { mats.limbSkin });
            CreateSkinnedPart("Body_Hair", visuals.transform, hairMesh, bones, bones[(int)B.Head],
                new[] { mats.hair });
            CreateSkinnedPart("Outfit_JacketTorso", visuals.transform, jacketMesh, bones, bones[(int)B.Hips],
                new[] { mats.jacket, mats.shirt, mats.sling, mats.metalAccent });
            CreateSkinnedPart("Outfit_Trousers", visuals.transform, pantsMesh, bones, bones[(int)B.Hips],
                new[] { mats.pants });
            CreateSkinnedPart("Outfit_Shoes", visuals.transform, shoeMesh, bones, bones[(int)B.Hips],
                new[] { mats.shoeUpper, mats.shoeSole });

            var animator = root.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var existingCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                RootDir + "/Animations/DhakaHumanoidLocomotion.controller");
            if (existingCtrl != null) animator.runtimeAnimatorController = existingCtrl;

            var rigBuilder = root.AddComponent<RigBuilder>();
            var rigObj = new GameObject("ProceduralRig");
            rigObj.transform.SetParent(root.transform, false);
            var rig = rigObj.AddComponent<Rig>();
            rigBuilder.layers = new List<RigLayer> { new RigLayer(rig, true) };

            AssetDatabase.SaveAssets();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.Refresh();

            return $"Rebuilt DhakaProtagonist at {PrefabPath} | Avatar valid={avatar.isValid} human={avatar.isHuman} | HeadVerts={headFaceMesh.vertexCount} JacketVerts={jacketMesh.vertexCount}";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    static void EnsureFolders()
    {
        string[] dirs = { "Assets/Characters", RootDir, TexDir, MatDir, MeshDir, "Assets/Prefabs" };
        foreach (string d in dirs)
        {
            if (!AssetDatabase.IsValidFolder(d))
            {
                AssetDatabase.CreateFolder(Path.GetDirectoryName(d).Replace('\\', '/'), Path.GetFileName(d));
            }
        }
    }

    static void CreateSkinnedPart(string name, Transform parent, Mesh mesh, Transform[] bones, Transform rootBone, Material[] materials)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var smr = go.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = mesh;
        smr.bones = bones;
        smr.rootBone = rootBone;
        smr.sharedMaterials = materials;
        smr.shadowCastingMode = ShadowCastingMode.On;
        smr.receiveShadows = true;
        smr.updateWhenOffscreen = true;
        smr.quality = SkinQuality.Bone4;
    }

    static void SaveMeshAsset(Mesh mesh, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
    }

    #region Skeleton & Avatar

    static Transform[] CreateSkeleton(Transform root)
    {
        var list = new Transform[25];
        Transform Make(B id, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(id.ToString());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            list[(int)id] = go.transform;
            return go.transform;
        }

        // Exact T-pose (+Z forward, +Y up, -X left, +X right), total height ~1.79m
        var hips = Make(B.Hips, root, new Vector3(0f, 0.96f, 0f));
        var spine = Make(B.Spine, hips, new Vector3(0f, 0.11f, 0f));
        var chest = Make(B.Chest, spine, new Vector3(0f, 0.15f, 0f));
        var upperChest = Make(B.UpperChest, chest, new Vector3(0f, 0.15f, 0f));
        var neck = Make(B.Neck, upperChest, new Vector3(0f, 0.14f, 0f));
        var head = Make(B.Head, neck, new Vector3(0f, 0.08f, 0.005f));
        Make(B.Jaw, head, new Vector3(0f, 0.02f, 0.035f));
        Make(B.LeftEye, head, new Vector3(-0.031f, 0.088f, 0.088f));
        Make(B.RightEye, head, new Vector3(0.031f, 0.088f, 0.088f));

        var lSh = Make(B.LeftShoulder, upperChest, new Vector3(-0.055f, 0.095f, 0f));
        var lUa = Make(B.LeftUpperArm, lSh, new Vector3(-0.135f, 0f, 0f));
        var lLa = Make(B.LeftLowerArm, lUa, new Vector3(-0.275f, 0f, 0f));
        Make(B.LeftHand, lLa, new Vector3(-0.245f, 0f, 0f));

        var rSh = Make(B.RightShoulder, upperChest, new Vector3(0.055f, 0.095f, 0f));
        var rUa = Make(B.RightUpperArm, rSh, new Vector3(0.135f, 0f, 0f));
        var rLa = Make(B.RightLowerArm, rUa, new Vector3(0.275f, 0f, 0f));
        Make(B.RightHand, rLa, new Vector3(0.245f, 0f, 0f));

        var lUl = Make(B.LeftUpperLeg, hips, new Vector3(-0.102f, -0.04f, 0f));
        var lLl = Make(B.LeftLowerLeg, lUl, new Vector3(0f, -0.44f, 0f));
        var lFt = Make(B.LeftFoot, lLl, new Vector3(0f, -0.43f, 0f));
        Make(B.LeftToes, lFt, new Vector3(0f, -0.035f, 0.135f));

        var rUl = Make(B.RightUpperLeg, hips, new Vector3(0.102f, -0.04f, 0f));
        var rLl = Make(B.RightLowerLeg, rUl, new Vector3(0f, -0.44f, 0f));
        var rFt = Make(B.RightFoot, rLl, new Vector3(0f, -0.43f, 0f));
        Make(B.RightToes, rFt, new Vector3(0f, -0.035f, 0.135f));

        return list;
    }

    static Avatar CreateHumanoidAvatar(GameObject root, Transform[] bones)
    {
        var allTransforms = root.GetComponentsInChildren<Transform>();
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
                humanName = ((B)i).ToString(),
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

    #endregion

    #region Procedural High-Res PBR Textures & Materials

    struct CharacterMaterials
    {
        public Material headSkin, limbSkin, eyeSclera, eyeIris, hair, jacket, shirt, pants, sling, metalAccent, shoeUpper, shoeSole;
    }

    static void GenerateTextures()
    {
        // Head Skin Albedo (u=0.5 is center front of face, v=0.0 is neck, v=1.0 is crown)
        WriteTexture(TexDir + "/T_HeadSkin_Albedo.png", 512, 512, true, (u, v) =>
        {
            float n = Fbm(u * 20f, v * 20f, 3);
            Color baseSkin = new Color(0.68f, 0.45f, 0.33f);
            Color warmCheek = new Color(0.73f, 0.43f, 0.32f);
            Color shadeTone = new Color(0.51f, 0.32f, 0.23f);
            Color c = Color.Lerp(shadeTone, baseSkin, 0.72f + 0.28f * n);

            float dx = u - 0.5f; // front center
            // Subtle cheek warmth
            float cheekL = Mathf.Exp(-(Mathf.Pow((dx + 0.065f) / 0.045f, 2f) + Mathf.Pow((v - 0.50f) / 0.06f, 2f)));
            float cheekR = Mathf.Exp(-(Mathf.Pow((dx - 0.065f) / 0.045f, 2f) + Mathf.Pow((v - 0.50f) / 0.06f, 2f)));
            c = Color.Lerp(c, warmCheek, Mathf.Clamp01(cheekL + cheekR) * 0.32f);

            // Natural under-eye / orbital socket subtle ambient shadow
            float orbL = Mathf.Exp(-(Mathf.Pow((dx + 0.052f) / 0.028f, 2f) + Mathf.Pow((v - 0.605f) / 0.028f, 2f)));
            float orbR = Mathf.Exp(-(Mathf.Pow((dx - 0.052f) / 0.028f, 2f) + Mathf.Pow((v - 0.605f) / 0.028f, 2f)));
            c = Color.Lerp(c, shadeTone * 0.85f, Mathf.Clamp01(orbL + orbR) * 0.34f);

            // Subtle masculine jawline & upper lip stubble warmth
            float jawShadow = Mathf.Clamp01(1f - Mathf.Abs(dx) / 0.16f) * Mathf.Exp(-Mathf.Pow((v - 0.36f) / 0.08f, 2f));
            c = Color.Lerp(c, shadeTone * 0.76f, jawShadow * 0.34f);

            // Nose bridge highlight & subtle underside/nostril shading (v ~ 0.47..0.60)
            float noseUnder = Mathf.Exp(-(Mathf.Pow(dx / 0.022f, 2f) + Mathf.Pow((v - 0.485f) / 0.015f, 2f)));
            c = Color.Lerp(c, shadeTone * 0.72f, noseUnder * 0.45f);
            float noseBridgeHi = Mathf.Exp(-(Mathf.Pow(dx / 0.011f, 2f) + Mathf.Pow((v - 0.545f) / 0.045f, 2f)));
            c = Color.Lerp(c, baseSkin * 1.12f, noseBridgeHi * 0.35f);

            // Natural lips tint + subtle mouth line (v ~ 0.425)
            float lipMask = Mathf.Exp(-(Mathf.Pow(dx / 0.030f, 2f) + Mathf.Pow((v - 0.422f) / 0.016f, 2f)));
            c = Color.Lerp(c, new Color(0.52f, 0.27f, 0.22f), lipMask * 0.78f);
            float mouthLine = Mathf.Exp(-(Mathf.Pow(dx / 0.024f, 2f) + Mathf.Pow((v - 0.422f) / 0.004f, 2f)));
            c = Color.Lerp(c, new Color(0.28f, 0.14f, 0.12f), mouthLine * 0.65f);

            // Neck subtle clavicle/throat shadow
            float neckShadow = Mathf.Exp(-Mathf.Pow((v - 0.16f) / 0.10f, 2f)) * 0.18f;
            c = Color.Lerp(c, shadeTone, neckShadow);

            return c;
        });

        WriteTexture(TexDir + "/T_LimbSkin_Albedo.png", 512, 512, true, (u, v) =>
        {
            float n = Fbm(u * 22f, v * 22f, 3);
            Color baseSkin = new Color(0.68f, 0.45f, 0.33f);
            Color shadeTone = new Color(0.54f, 0.34f, 0.25f);
            return Color.Lerp(shadeTone, baseSkin, 0.72f + 0.28f * n);
        });

        WriteNormalFromHeight(TexDir + "/T_Skin_Normal.png", 512, 512, 0.85f, (u, v) =>
        {
            return Fbm(u * 60f, v * 60f, 3) * 0.6f + Fbm(u * 120f, v * 120f, 2) * 0.4f;
        });

        WriteTexture(TexDir + "/T_Jacket_Albedo.png", 1024, 1024, true, (u, v) =>
        {
            float weave = (Mathf.Sin((u + v) * 900f) * 0.5f + 0.5f) * 0.25f;
            float grain = Fbm(u * 16f, v * 16f, 4);
            Color darkIndigo = new Color(0.11f, 0.17f, 0.25f);
            Color midIndigo = new Color(0.16f, 0.25f, 0.35f);
            Color c = Color.Lerp(darkIndigo, midIndigo, 0.42f + 0.40f * grain + 0.18f * weave);
            // Tailored stitched seam details
            float seamV = Mathf.Min(Mathf.Abs(u - 0.24f), Mathf.Abs(u - 0.76f));
            if (seamV < 0.003f) c *= 0.72f;
            float pocket = (Mathf.Abs(v - 0.58f) < 0.004f && (u > 0.15f && u < 0.36f || u > 0.64f && u < 0.85f)) ? 1f : 0f;
            c = Color.Lerp(c, c * 0.68f, pocket);
            float damp = Mathf.Clamp01(1f - v * 4.2f) * 0.20f;
            return c * (1f - damp);
        });

        WriteNormalFromHeight(TexDir + "/T_Jacket_Normal.png", 1024, 1024, 1.8f, (u, v) =>
        {
            float twill = Mathf.Sin((u * 2f + v) * 800f) * 0.25f;
            float folds = Fbm(u * 10f, v * 8f, 4) * 0.75f;
            return twill * 0.25f + folds;
        });

        WriteTexture(TexDir + "/T_Shirt_Albedo.png", 512, 512, true, (u, v) =>
        {
            float n = Fbm(u * 12f, v * 12f, 3) * 0.07f;
            Color warmCotton = new Color(0.83f, 0.79f, 0.73f);
            return warmCotton * (0.95f + n);
        });

        WriteNormalFromHeight(TexDir + "/T_Shirt_Normal.png", 512, 512, 0.65f, (u, v) =>
        {
            // Fine soft cotton weave + gentle natural folds (zero moiré)
            return Fbm(u * 12f, v * 12f, 3) * 0.85f + Fbm(u * 90f, v * 90f, 2) * 0.15f;
        });

        WriteTexture(TexDir + "/T_Pants_Albedo.png", 512, 512, true, (u, v) =>
        {
            float wear = Fbm(u * 10f, v * 10f, 3) * 0.08f;
            Color slateDenim = new Color(0.11f, 0.13f, 0.17f);
            Color c = slateDenim * (0.93f + wear);
            float damp = Mathf.Clamp01(1f - v * 3.2f) * 0.24f;
            return c * (1f - damp);
        });

        WriteNormalFromHeight(TexDir + "/T_Pants_Normal.png", 512, 512, 1.4f, (u, v) =>
        {
            return Fbm(u * 14f, v * 14f, 3) * 0.75f + Fbm(u * 110f, v * 110f, 2) * 0.25f;
        });

        WriteTexture(TexDir + "/T_Hair_Albedo.png", 512, 512, true, (u, v) =>
        {
            float strand = Fbm(u * 96f, v * 12f, 3);
            Color dark = new Color(0.055f, 0.05f, 0.05f);
            Color sheen = new Color(0.14f, 0.12f, 0.11f);
            return Color.Lerp(dark, sheen, strand * 0.7f);
        });

        WriteNormalFromHeight(TexDir + "/T_Hair_Normal.png", 512, 512, 2.2f, (u, v) =>
        {
            return Fbm(u * 96f, v * 12f, 3);
        });

        WriteTexture(TexDir + "/T_Sling_Albedo.png", 512, 512, true, (u, v) =>
        {
            Color darkCanvas = new Color(0.13f, 0.25f, 0.22f);
            Color c = darkCanvas * (0.93f + Fbm(u * 16f, v * 16f, 3) * 0.12f);
            if (Mathf.Abs(v - 0.5f) < 0.035f) c = new Color(0.84f, 0.65f, 0.24f);
            return c;
        });

        WriteTexture(TexDir + "/T_Shoe_Albedo.png", 512, 512, true, (u, v) =>
        {
            Color charcoal = new Color(0.12f, 0.13f, 0.15f);
            return charcoal * (0.92f + Fbm(u * 24f, v * 24f, 2) * 0.10f);
        });

        WriteNormalFromHeight(TexDir + "/T_Shoe_Normal.png", 512, 512, 1.3f, (u, v) =>
        {
            return Fbm(u * 32f, v * 32f, 3);
        });
    }

    static void ConfigureTextureImporters()
    {
        string[] normals =
        {
            TexDir + "/T_Skin_Normal.png",
            TexDir + "/T_Jacket_Normal.png",
            TexDir + "/T_Shirt_Normal.png",
            TexDir + "/T_Pants_Normal.png",
            TexDir + "/T_Hair_Normal.png",
            TexDir + "/T_Shoe_Normal.png"
        };
        foreach (string p in normals)
        {
            var imp = AssetImporter.GetAtPath(p) as TextureImporter;
            if (imp != null && imp.textureType != TextureImporterType.NormalMap)
            {
                imp.textureType = TextureImporterType.NormalMap;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.SaveAndReimport();
            }
        }
    }

    static CharacterMaterials CreateMaterials()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        Material MakeLit(string name, string albedoPath, string normalPath, Color tint, float smoothness, float metallic, float bumpScale = 0.6f)
        {
            string matPath = MatDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(urpLit);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.shader = urpLit;
            mat.SetColor("_BaseColor", tint);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            if (!string.IsNullOrEmpty(albedoPath))
            {
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath));
            }
            if (!string.IsNullOrEmpty(normalPath))
            {
                mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
                mat.SetFloat("_BumpScale", bumpScale);
                mat.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        return new CharacterMaterials
        {
            headSkin = MakeLit("Mat_Char_HeadSkin", TexDir + "/T_HeadSkin_Albedo.png", TexDir + "/T_Skin_Normal.png", Color.white, 0.34f, 0f, 0.35f),
            limbSkin = MakeLit("Mat_Char_Skin", TexDir + "/T_LimbSkin_Albedo.png", TexDir + "/T_Skin_Normal.png", Color.white, 0.34f, 0f, 0.35f),
            eyeSclera = MakeLit("Mat_Char_EyeSclera", null, null, new Color(0.88f, 0.87f, 0.85f), 0.72f, 0f),
            eyeIris = MakeLit("Mat_Char_EyeIris", null, null, new Color(0.10f, 0.07f, 0.05f), 0.88f, 0.05f),
            hair = MakeLit("Mat_Char_Hair", TexDir + "/T_Hair_Albedo.png", TexDir + "/T_Hair_Normal.png", Color.white, 0.30f, 0.03f, 0.75f),
            jacket = MakeLit("Mat_Char_Jacket", TexDir + "/T_Jacket_Albedo.png", TexDir + "/T_Jacket_Normal.png", Color.white, 0.28f, 0.04f, 0.75f),
            shirt = MakeLit("Mat_Char_Shirt", TexDir + "/T_Shirt_Albedo.png", TexDir + "/T_Shirt_Normal.png", Color.white, 0.16f, 0f, 0.45f),
            pants = MakeLit("Mat_Char_Pants", TexDir + "/T_Pants_Albedo.png", TexDir + "/T_Pants_Normal.png", Color.white, 0.24f, 0.02f, 0.65f),
            sling = MakeLit("Mat_Char_Sling", TexDir + "/T_Sling_Albedo.png", TexDir + "/T_Jacket_Normal.png", Color.white, 0.32f, 0.06f, 0.55f),
            metalAccent = MakeLit("Mat_Char_MetalAccent", null, null, new Color(0.28f, 0.30f, 0.34f), 0.72f, 0.78f),
            shoeUpper = MakeLit("Mat_Char_ShoeUpper", TexDir + "/T_Shoe_Albedo.png", TexDir + "/T_Shoe_Normal.png", Color.white, 0.34f, 0.04f, 0.6f),
            shoeSole = MakeLit("Mat_Char_ShoeSole", null, null, new Color(0.74f, 0.73f, 0.70f), 0.35f, 0.02f)
        };
    }

    static void WriteTexture(string path, int w, int h, bool srgb, Func<float, float, Color> eval)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, !srgb);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float v = y / (float)(h - 1);
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)(w - 1);
                Color c = eval(u, v);
                c.a = 1f;
                px[y * w + x] = (Color32)c;
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        File.WriteAllBytes(Path.GetFullPath(path), tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
    }

    static void WriteNormalFromHeight(string path, int w, int h, float strength, Func<float, float, float> heightFn)
    {
        float[,] H = new float[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                H[x, y] = heightFn(x / (float)w, y / (float)h);

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            int ym = (y - 1 + h) % h, yp = (y + 1) % h;
            for (int x = 0; x < w; x++)
            {
                int xm = (x - 1 + w) % w, xp = (x + 1) % w;
                float dx = (H[xp, y] - H[xm, y]) * strength;
                float dy = (H[x, yp] - H[x, ym]) * strength;
                Vector3 n = new Vector3(-dx, -dy, 1f).normalized;
                px[y * w + x] = new Color32(
                    (byte)Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f),
                    (byte)Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f),
                    (byte)Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f),
                    255);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        File.WriteAllBytes(Path.GetFullPath(path), tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
    }

    static float Hash2(int x, int y)
    {
        uint h = (uint)(x * 374761393 + y * 668265263);
        h = (h ^ (h >> 13)) * 1274126177u;
        return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
    }

    static float ValueNoise(float x, float y)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float fx = x - xi, fy = y - yi;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        return Mathf.Lerp(
            Mathf.Lerp(Hash2(xi, yi), Hash2(xi + 1, yi), fx),
            Mathf.Lerp(Hash2(xi, yi + 1), Hash2(xi + 1, yi + 1), fx),
            fy);
    }

    static float Fbm(float x, float y, int octaves)
    {
        float val = 0f, amp = 0.5f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            val += amp * ValueNoise(x, y);
            norm += amp;
            x *= 2.03f; y *= 2.03f; amp *= 0.5f;
        }
        return val / norm;
    }

    #endregion

    #region Sculpted Skinned Mesh Builder

    class SkinnedMeshBuilder
    {
        readonly List<Vector3> verts = new();
        readonly List<Vector2> uvs = new();
        readonly List<BoneWeight> weights = new();
        readonly List<List<int>> submeshTris = new();

        int EnsureSubmesh(int submesh)
        {
            while (submeshTris.Count <= submesh) submeshTris.Add(new List<int>());
            return submesh;
        }

        public static BoneWeight MakeBW(B a, float wa, B b = B.Hips, float wb = 0f, B c = B.Hips, float wc = 0f)
        {
            float sum = wa + wb + wc;
            if (sum <= 0.0001f) { wa = 1f; sum = 1f; }
            return new BoneWeight
            {
                boneIndex0 = (int)a, weight0 = wa / sum,
                boneIndex1 = (int)b, weight1 = wb / sum,
                boneIndex2 = (int)c, weight2 = wc / sum
            };
        }

        public struct RingSpec
        {
            public Vector3 center;
            public float rx;
            public float rz;
            public float vCoord;
            public BoneWeight bw;
            public Func<float, Vector3, Vector3> deform;
        }

        public void AddLoft(RingSpec[] rings, int radialSegs, int submesh, bool capBottom = true, bool capTop = true, Func<Vector3, BoneWeight> perVertWeight = null)
        {
            AddArcLoft(rings, radialSegs, -Mathf.PI, Mathf.PI, submesh, capBottom, capTop, perVertWeight);
        }

        /// <summary>
        /// Builds an open or closed loft between angleStart and angleEnd (where angle=0 is +Z front, -PI/+PI is -Z back).
        /// </summary>
        public void AddArcLoft(RingSpec[] rings, int radialSegs, float angleStart, float angleEnd, int submesh, bool capBottom, bool capTop, Func<Vector3, BoneWeight> perVertWeight = null, bool doubleSidedEdges = false)
        {
            EnsureSubmesh(submesh);
            var tris = submeshTris[submesh];
            int stride = radialSegs + 1;
            int startIdx = verts.Count;

            for (int r = 0; r < rings.Length; r++)
            {
                var ring = rings[r];
                for (int s = 0; s <= radialSegs; s++)
                {
                    float t = s / (float)radialSegs;
                    float ang = Mathf.Lerp(angleStart, angleEnd, t);
                    // ang=0 -> +Z front, ang=+PI/2 -> +X right, ang=-PI/2 -> -X left, ang=±PI -> -Z back
                    float lx = Mathf.Sin(ang) * ring.rx;
                    float lz = Mathf.Cos(ang) * ring.rz;
                    Vector3 p = ring.center + new Vector3(lx, 0f, lz);
                    if (ring.deform != null) p = ring.deform(ang, p);

                    // u=0.5 corresponds to ang=0 (+Z front)
                    float u = 0.5f + ang / (Mathf.PI * 2f);
                    verts.Add(p);
                    uvs.Add(new Vector2(u, ring.vCoord));
                    weights.Add(perVertWeight != null ? perVertWeight(p) : ring.bw);
                }
            }

            for (int r = 0; r < rings.Length - 1; r++)
            {
                int row0 = startIdx + r * stride;
                int row1 = row0 + stride;
                for (int s = 0; s < radialSegs; s++)
                {
                    int a = row0 + s;
                    int b = row0 + s + 1;
                    int c = row1 + s;
                    int d = row1 + s + 1;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }
            }

            if (capBottom && rings.Length > 0)
            {
                int centerIdx = verts.Count;
                Vector3 cp = rings[0].center;
                verts.Add(cp); uvs.Add(new Vector2(0.5f, rings[0].vCoord));
                weights.Add(perVertWeight != null ? perVertWeight(cp) : rings[0].bw);
                for (int s = 0; s < radialSegs; s++)
                {
                    tris.Add(centerIdx); tris.Add(startIdx + s + 1); tris.Add(startIdx + s);
                }
            }

            if (capTop && rings.Length > 0)
            {
                int last = rings.Length - 1;
                int centerIdx = verts.Count;
                Vector3 cp = rings[last].center;
                verts.Add(cp); uvs.Add(new Vector2(0.5f, rings[last].vCoord));
                weights.Add(perVertWeight != null ? perVertWeight(cp) : rings[last].bw);
                int rowTop = startIdx + last * stride;
                for (int s = 0; s < radialSegs; s++)
                {
                    tris.Add(centerIdx); tris.Add(rowTop + s); tris.Add(rowTop + s + 1);
                }
            }
        }

        public void AddHorizontalLimbLoft(
            float[] xCoords, float[] radY, float[] radZ, float[] centerY, float[] centerZ,
            int radialSegs, int submesh, bool capStart, bool capEnd,
            Func<Vector3, BoneWeight> weightFn)
        {
            EnsureSubmesh(submesh);
            var tris = submeshTris[submesh];
            int stride = radialSegs + 1;
            int startIdx = verts.Count;
            bool flipWinding = xCoords[xCoords.Length - 1] < xCoords[0];

            for (int r = 0; r < xCoords.Length; r++)
            {
                float v = r / (float)Mathf.Max(1, xCoords.Length - 1);
                Vector3 c = new Vector3(xCoords[r], centerY[r], centerZ[r]);
                for (int s = 0; s <= radialSegs; s++)
                {
                    float u = s / (float)radialSegs;
                    float ang = u * Mathf.PI * 2f;
                    Vector3 p = c + new Vector3(0f, Mathf.Sin(ang) * radY[r], Mathf.Cos(ang) * radZ[r]);
                    verts.Add(p);
                    uvs.Add(new Vector2(u, v));
                    weights.Add(weightFn(p));
                }
            }

            for (int r = 0; r < xCoords.Length - 1; r++)
            {
                int row0 = startIdx + r * stride;
                int row1 = row0 + stride;
                for (int s = 0; s < radialSegs; s++)
                {
                    int a = row0 + s; int b = row0 + s + 1;
                    int c = row1 + s; int d = row1 + s + 1;
                    if (!flipWinding) { tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(b); tris.Add(d); tris.Add(c); }
                    else { tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d); }
                }
            }

            if (capStart)
            {
                int cIdx = verts.Count;
                Vector3 cp = new Vector3(xCoords[0], centerY[0], centerZ[0]);
                verts.Add(cp); uvs.Add(new Vector2(0.5f, 0f)); weights.Add(weightFn(cp));
                for (int s = 0; s < radialSegs; s++)
                {
                    if (!flipWinding) { tris.Add(cIdx); tris.Add(startIdx + s + 1); tris.Add(startIdx + s); }
                    else { tris.Add(cIdx); tris.Add(startIdx + s); tris.Add(startIdx + s + 1); }
                }
            }
            if (capEnd)
            {
                int last = xCoords.Length - 1;
                int cIdx = verts.Count;
                Vector3 cp = new Vector3(xCoords[last], centerY[last], centerZ[last]);
                verts.Add(cp); uvs.Add(new Vector2(0.5f, 1f)); weights.Add(weightFn(cp));
                int rowTop = startIdx + last * stride;
                for (int s = 0; s < radialSegs; s++)
                {
                    if (!flipWinding) { tris.Add(cIdx); tris.Add(rowTop + s); tris.Add(rowTop + s + 1); }
                    else { tris.Add(cIdx); tris.Add(rowTop + s + 1); tris.Add(rowTop + s); }
                }
            }
        }

        public void AddEllipsoid(Vector3 center, Vector3 radii, int uSegs, int vSegs, int submesh, Func<Vector3, BoneWeight> weightFn, Func<Vector3, Vector3> deform = null)
        {
            EnsureSubmesh(submesh);
            var tris = submeshTris[submesh];
            int stride = uSegs + 1;
            int startIdx = verts.Count;

            for (int iv = 0; iv <= vSegs; iv++)
            {
                float vf = iv / (float)vSegs;
                float phi = vf * Mathf.PI;
                float sp = Mathf.Sin(phi);
                float cp = Mathf.Cos(phi);
                for (int iu = 0; iu <= uSegs; iu++)
                {
                    float uf = iu / (float)uSegs;
                    float theta = uf * Mathf.PI * 2f;
                    Vector3 unit = new Vector3(Mathf.Sin(theta) * sp, -cp, Mathf.Cos(theta) * sp);
                    Vector3 p = center + Vector3.Scale(unit, radii);
                    if (deform != null) p = deform(p);
                    verts.Add(p);
                    uvs.Add(new Vector2(uf, vf));
                    weights.Add(weightFn(p));
                }
            }

            for (int iv = 0; iv < vSegs; iv++)
            {
                int row0 = startIdx + iv * stride;
                int row1 = row0 + stride;
                for (int iu = 0; iu < uSegs; iu++)
                {
                    int a = row0 + iu; int b = row0 + iu + 1;
                    int c = row1 + iu; int d = row1 + iu + 1;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }
            }
        }

        public void AddContouredBox(Vector3 center, Vector3 halfExtents, Quaternion rot, int submesh, BoneWeight bw, float bevel = 0.2f)
        {
            AddEllipsoid(center, halfExtents, 10, 6, submesh, _ => bw, p =>
            {
                Vector3 local = p - center;
                Vector3 norm = new Vector3(
                    local.x / Mathf.Max(0.001f, halfExtents.x),
                    local.y / Mathf.Max(0.001f, halfExtents.y),
                    local.z / Mathf.Max(0.001f, halfExtents.z));
                float pow = 2.6f;
                Vector3 sq = new Vector3(
                    Mathf.Sign(norm.x) * Mathf.Pow(Mathf.Abs(norm.x), 2f / pow),
                    Mathf.Sign(norm.y) * Mathf.Pow(Mathf.Abs(norm.y), 2f / pow),
                    Mathf.Sign(norm.z) * Mathf.Pow(Mathf.Abs(norm.z), 2f / pow));
                Vector3 blended = Vector3.Lerp(norm, sq, 1f - bevel);
                return center + rot * Vector3.Scale(blended, halfExtents);
            });
        }

        public Mesh ToMesh(string name, Matrix4x4[] bindPoses)
        {
            var mesh = new Mesh { name = name };
            if (verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = submeshTris.Count;
            for (int i = 0; i < submeshTris.Count; i++) mesh.SetTriangles(submeshTris[i], i);
            mesh.boneWeights = weights.ToArray();
            mesh.bindposes = bindPoses;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    static float SmoothStep(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    static Mesh BuildHeadAndFaceMesh(Matrix4x4[] bindPoses)
    {
        var mb = new SkinnedMeshBuilder();

        Func<Vector3, BoneWeight> headWeight = p =>
        {
            if (p.y < 1.54f)
            {
                float t = SmoothStep(1.44f, 1.54f, p.y);
                return SkinnedMeshBuilder.MakeBW(B.Neck, 1f - t * 0.4f, B.UpperChest, (1f - t) * 0.4f, B.Head, t * 0.4f);
            }
            float ht = SmoothStep(1.54f, 1.60f, p.y);
            return SkinnedMeshBuilder.MakeBW(B.Neck, 1f - ht, B.Head, ht);
        };

        // Compact natural head & neck (neck base tucked cleanly inside shirt/jacket collar at y=1.45)
        var headRings = new[]
        {
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 1.445f, 0.000f), rx = 0.082f, rz = 0.075f, vCoord = 0.00f },
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 1.500f, 0.004f), rx = 0.062f, rz = 0.062f, vCoord = 0.14f },
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 1.545f, 0.008f), rx = 0.058f, rz = 0.060f, vCoord = 0.24f },
            // Chin & jawline
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 1.580f, 0.026f), rx = 0.052f, rz = 0.064f, vCoord = 0.33f },
            // Lips & lower jaw
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 1.615f, 0.018f), rx = 0.072f, rz = 0.086f, vCoord = 0.42f },
            // Nose tip & nostrils
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 1.646f, 0.015f), rx = 0.080f, rz = 0.094f, vCoord = 0.50f },
            // Cheekbones & mid-nose bridge
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 1.664f, 0.014f), rx = 0.082f, rz = 0.095f, vCoord = 0.56f },
            // Eyes / orbital sockets & upper nasal bridge
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 1.682f, 0.012f), rx = 0.083f, rz = 0.096f, vCoord = 0.62f },
            // Brow ridge
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 1.708f, 0.010f), rx = 0.084f, rz = 0.099f, vCoord = 0.72f },
            // Forehead
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 1.742f, 0.002f), rx = 0.081f, rz = 0.098f, vCoord = 0.84f },
            // Crown
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 1.775f, -0.006f), rx = 0.056f, rz = 0.072f, vCoord = 0.94f },
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 1.790f, -0.010f), rx = 0.014f, rz = 0.020f, vCoord = 1.00f }
        };

        for (int i = 0; i < headRings.Length; i++)
        {
            headRings[i].deform = (ang, p) =>
            {
                float absX = Mathf.Abs(p.x);
                float front = Mathf.Clamp01((p.z - 0.02f) / 0.09f);
                // Taper lower jaw into clean chin
                if (p.y < 1.61f && p.z > 0.03f) p.x *= 0.84f;

                // Pronounced sculpted nose bridge + nose tip + nostril flare
                if (absX < 0.022f && p.z > 0.04f && p.y > 1.628f && p.y < 1.712f)
                {
                    float noseArch = Mathf.Sin(Mathf.Clamp01((p.y - 1.628f) / 0.084f) * Mathf.PI);
                    float noseTip = Mathf.Exp(-Mathf.Pow((p.y - 1.646f) / 0.013f, 2f));
                    float wMask = Mathf.Cos((absX / 0.022f) * Mathf.PI * 0.5f);
                    p.z += (0.022f * noseArch + 0.028f * noseTip) * wMask;
                }

                // Orbital sockets recessed neatly so eyes sit inside sockets
                float eyeDx = absX - 0.031f;
                float eyeDy = p.y - 1.682f;
                float eyeDist = Mathf.Sqrt(eyeDx * eyeDx + eyeDy * eyeDy);
                if (p.z > 0.04f && eyeDist < 0.022f)
                {
                    float socket = Mathf.Cos((eyeDist / 0.022f) * Mathf.PI * 0.5f);
                    p.z -= 0.016f * socket;
                }

                // Brow ridge overhang above eyes
                if (p.z > 0.03f && p.y > 1.698f && p.y < 1.722f && absX < 0.058f)
                {
                    p.z += 0.010f * front;
                }

                // Cheekbone contour
                if (p.z > 0.02f && p.y > 1.640f && p.y < 1.668f && absX > 0.026f && absX < 0.068f)
                {
                    p.z += 0.008f;
                }

                // Sculpted upper & lower lips + chin projection
                if (absX < 0.028f && p.z > 0.04f && p.y > 1.596f && p.y < 1.628f)
                {
                    float wMask = Mathf.Cos((absX / 0.028f) * Mathf.PI * 0.5f);
                    float crease = Mathf.Exp(-Mathf.Pow((p.y - 1.613f) / 0.003f, 2f));
                    float puff = Mathf.Exp(-Mathf.Pow((p.y - 1.618f) / 0.0045f, 2f)) + Mathf.Exp(-Mathf.Pow((p.y - 1.608f) / 0.0045f, 2f));
                    p.z += (puff * 0.015f - crease * 0.009f) * wMask;
                }
                if (absX < 0.034f && p.z > 0.04f && p.y > 1.572f && p.y < 1.596f)
                {
                    p.z += 0.012f * Mathf.Cos((absX / 0.034f) * Mathf.PI * 0.5f);
                }
                return p;
            };
        }
        mb.AddLoft(headRings, 32, 0, true, true, headWeight);

        // Skin-toned sculpted Ears (submesh 0 = headSkin!)
        for (int side = -1; side <= 1; side += 2)
        {
            mb.AddEllipsoid(
                new Vector3(side * 0.084f, 1.665f, -0.006f),
                new Vector3(0.013f, 0.029f, 0.019f),
                10, 8, 0,
                _ => SkinnedMeshBuilder.MakeBW(B.Head, 1f));
        }

        // Recessed Eyes (submesh 1 = Sclera, submesh 2 = Iris) + Eyebrows & Upper Eyelids (submesh 3 = Hair)
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 eyeCenter = new Vector3(side * 0.031f, 1.682f, 0.091f);
            mb.AddEllipsoid(eyeCenter, new Vector3(0.0115f, 0.0085f, 0.009f), 10, 8, 1,
                _ => SkinnedMeshBuilder.MakeBW(B.Head, 1f));
            mb.AddEllipsoid(eyeCenter + new Vector3(0f, 0f, 0.0058f), new Vector3(0.0056f, 0.0056f, 0.0048f), 10, 6, 2,
                _ => SkinnedMeshBuilder.MakeBW(B.Head, 1f));

            // Sculpted eyebrow arch sitting cleanly on brow surface (submesh 3 = Hair)
            mb.AddContouredBox(
                new Vector3(side * 0.032f, 1.703f, 0.107f),
                new Vector3(0.018f, 0.0036f, 0.0045f),
                Quaternion.Euler(0f, side * -14f, side * -6f),
                3,
                SkinnedMeshBuilder.MakeBW(B.Head, 1f),
                0.35f);

            // Subtle upper eyelid rim (submesh 0 = headSkin)
            mb.AddContouredBox(
                new Vector3(side * 0.031f, 1.689f, 0.096f),
                new Vector3(0.013f, 0.0032f, 0.0065f),
                Quaternion.Euler(14f, side * -10f, 0f),
                0,
                SkinnedMeshBuilder.MakeBW(B.Head, 1f),
                0.3f);
        }

        return mb.ToMesh("M_HeadAndFace", bindPoses);
    }

    static Mesh BuildForearmsAndHandsMesh(Matrix4x4[] bindPoses)
    {
        var mb = new SkinnedMeshBuilder();
        BuildForearmAndHand(mb, -1f, B.LeftUpperArm, B.LeftLowerArm, B.LeftHand);
        BuildForearmAndHand(mb, +1f, B.RightUpperArm, B.RightLowerArm, B.RightHand);
        return mb.ToMesh("M_ArmsAndHands", bindPoses);
    }

    static void BuildForearmAndHand(SkinnedMeshBuilder mb, float dirX, B upperArm, B lowerArm, B hand)
    {
        float yArm = 1.450f;
        float[] xs = { dirX * 0.285f, dirX * 0.355f, dirX * 0.455f, dirX * 0.55f, dirX * 0.645f, dirX * 0.715f, dirX * 0.745f };
        float[] ry = { 0.043f, 0.045f, 0.044f, 0.042f, 0.033f, 0.027f, 0.023f };
        float[] rz = { 0.044f, 0.047f, 0.046f, 0.044f, 0.036f, 0.032f, 0.035f };
        float[] cy = { yArm, yArm, yArm, yArm, yArm, yArm, yArm };
        float[] cz = { 0f, 0f, 0f, 0f, 0f, 0f, 0.002f };

        Func<Vector3, BoneWeight> armWeight = p =>
        {
            float ax = Mathf.Abs(p.x);
            if (ax < 0.41f)
            {
                return SkinnedMeshBuilder.MakeBW(upperArm, 1f);
            }
            if (ax < 0.50f)
            {
                float t = SmoothStep(0.41f, 0.50f, ax);
                return SkinnedMeshBuilder.MakeBW(upperArm, 1f - t, lowerArm, t);
            }
            if (ax < 0.70f) return SkinnedMeshBuilder.MakeBW(lowerArm, 1f);
            float ht = SmoothStep(0.69f, 0.74f, ax);
            return SkinnedMeshBuilder.MakeBW(lowerArm, 1f - ht, hand, ht);
        };

        mb.AddHorizontalLimbLoft(xs, ry, rz, cy, cz, 14, 0, true, false, armWeight);

        mb.AddHorizontalLimbLoft(
            new[] { dirX * 0.72f, dirX * 0.77f, dirX * 0.815f },
            new[] { 0.020f, 0.019f, 0.016f },
            new[] { 0.035f, 0.044f, 0.043f },
            new[] { yArm, yArm - 0.003f, yArm - 0.005f },
            new[] { 0.002f, 0.004f, 0.003f },
            12, 0, false, true, _ => SkinnedMeshBuilder.MakeBW(hand, 1f));

        float[] fingerZ = { 0.029f, 0.010f, -0.010f, -0.028f };
        float[] fingerLen = { 0.070f, 0.078f, 0.072f, 0.056f };
        float[] fingerRad = { 0.0090f, 0.0092f, 0.0088f, 0.0076f };
        for (int f = 0; f < 4; f++)
        {
            float fx0 = dirX * 0.805f;
            float fx1 = dirX * (0.805f + fingerLen[f] * 0.55f);
            float fx2 = dirX * (0.805f + fingerLen[f]);
            mb.AddHorizontalLimbLoft(
                new[] { fx0, fx1, fx2 },
                new[] { fingerRad[f], fingerRad[f] * 0.9f, fingerRad[f] * 0.72f },
                new[] { fingerRad[f], fingerRad[f] * 0.9f, fingerRad[f] * 0.72f },
                new[] { yArm - 0.005f, yArm - 0.012f, yArm - 0.026f },
                new[] { fingerZ[f], fingerZ[f], fingerZ[f] },
                8, 0, true, true, _ => SkinnedMeshBuilder.MakeBW(hand, 1f));
        }

        mb.AddHorizontalLimbLoft(
            new[] { dirX * 0.735f, dirX * 0.775f, dirX * 0.808f },
            new[] { 0.0115f, 0.010f, 0.008f },
            new[] { 0.0115f, 0.010f, 0.008f },
            new[] { yArm - 0.012f, yArm - 0.020f, yArm - 0.027f },
            new[] { 0.035f, 0.055f, 0.070f },
            8, 0, true, true, _ => SkinnedMeshBuilder.MakeBW(hand, 1f));
    }

    static Mesh BuildHairMesh(Matrix4x4[] bindPoses)
    {
        var mb = new SkinnedMeshBuilder();
        Func<Vector3, BoneWeight> bw = _ => SkinnedMeshBuilder.MakeBW(B.Head, 1f);

        // Full natural sculpted cranium hair cap sitting cleanly over forehead hairline down to nape
        var hairRings = new[]
        {
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 1.595f, -0.044f), rx = 0.072f, rz = 0.058f, vCoord = 0.0f },
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 1.655f, -0.032f), rx = 0.088f, rz = 0.082f, vCoord = 0.25f },
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 1.715f, -0.012f), rx = 0.094f, rz = 0.104f, vCoord = 0.50f },
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 1.755f, 0.002f), rx = 0.094f, rz = 0.114f, vCoord = 0.72f },
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 1.792f, -0.002f), rx = 0.084f, rz = 0.106f, vCoord = 0.88f },
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 1.816f, -0.008f), rx = 0.048f, rz = 0.068f, vCoord = 0.96f },
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 1.825f, -0.012f), rx = 0.014f, rz = 0.020f, vCoord = 1.00f }
        };

        for (int i = 0; i < hairRings.Length; i++)
        {
            hairRings[i].deform = (ang, p) =>
            {
                // Keep lower face open while letting top hairline reach neatly to forehead (y=1.725+)
                if (p.y < 1.725f && p.z > -0.005f)
                {
                    p.z = Mathf.Min(p.z, -0.005f + SmoothStep(1.60f, 1.725f, p.y) * 0.065f);
                }
                if (p.y > 1.73f)
                {
                    float wave = Mathf.Sin(ang * 8f + p.y * 30f) * 0.004f;
                    p += new Vector3(wave * 0.5f, Mathf.Abs(wave), wave * 0.4f);
                }
                return p;
            };
        }
        mb.AddLoft(hairRings, 26, 0, true, true, bw);

        // Front swept fringe / textured top hair silhouette
        mb.AddEllipsoid(new Vector3(-0.008f, 1.768f, 0.036f), new Vector3(0.076f, 0.036f, 0.068f), 14, 10, 0, bw, p =>
        {
            p.y += Mathf.Sin(p.x * 42f) * 0.0045f;
            if (p.y < 1.736f) p.y = 1.736f;
            return p;
        });

        // Natural tapered sideburns
        for (int side = -1; side <= 1; side += 2)
        {
            mb.AddContouredBox(
                new Vector3(side * 0.080f, 1.682f, 0.016f),
                new Vector3(0.010f, 0.032f, 0.016f),
                Quaternion.Euler(8f, 0f, 0f),
                0,
                SkinnedMeshBuilder.MakeBW(B.Head, 1f),
                0.35f);
        }

        return mb.ToMesh("M_Hair", bindPoses);
    }

    static Mesh BuildJacketAndTorsoMesh(Matrix4x4[] bindPoses)
    {
        var mb = new SkinnedMeshBuilder();

        Func<Vector3, BoneWeight> torsoWeight = p =>
        {
            float y = p.y;
            float ax = Mathf.Abs(p.x);
            if (ax > 0.16f && y > 1.32f)
            {
                B sh = p.x < 0 ? B.LeftShoulder : B.RightShoulder;
                B ua = p.x < 0 ? B.LeftUpperArm : B.RightUpperArm;
                float armT = SmoothStep(0.16f, 0.27f, ax);
                return SkinnedMeshBuilder.MakeBW(B.UpperChest, 1f - armT, sh, armT * 0.45f, ua, armT * 0.55f);
            }
            if (y < 1.01f)
            {
                float t = SmoothStep(0.90f, 1.01f, y);
                return SkinnedMeshBuilder.MakeBW(B.Hips, 1f - t, B.Spine, t);
            }
            if (y < 1.19f)
            {
                float t = SmoothStep(1.01f, 1.19f, y);
                return SkinnedMeshBuilder.MakeBW(B.Spine, 1f - t, B.Chest, t);
            }
            if (y < 1.36f)
            {
                float t = SmoothStep(1.19f, 1.36f, y);
                return SkinnedMeshBuilder.MakeBW(B.Chest, 1f - t, B.UpperChest, t);
            }
            float nt = SmoothStep(1.44f, 1.54f, y);
            return SkinnedMeshBuilder.MakeBW(B.UpperChest, 1f - nt * 0.45f, B.Neck, nt * 0.45f);
        };

        // 1. Submesh 1: Inner Henley Tee (cleanly tucked inside trousers at y=0.95, snug crew collar at y=1.48)
        var shirtRings = new[]
        {
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 0.94f, 0.004f), rx = 0.156f, rz = 0.110f, vCoord = 0.00f },
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 1.04f, 0.008f), rx = 0.158f, rz = 0.112f, vCoord = 0.22f },
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 1.22f, 0.012f), rx = 0.184f, rz = 0.128f, vCoord = 0.55f },
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 1.39f, 0.008f), rx = 0.194f, rz = 0.122f, vCoord = 0.80f },
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 1.465f, 0.004f), rx = 0.095f, rz = 0.086f, vCoord = 0.94f },
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 1.480f, 0.004f), rx = 0.078f, rz = 0.072f, vCoord = 1.00f }
        };
        mb.AddLoft(shirtRings, 24, 1, true, true, torsoWeight);

        // 2. Submesh 0: Tailored Open Field Jacket built as an OPEN ARC (-162 deg to +162 deg? Wait: front is ang=0!)
        // Around back (-PI to +PI), leaving a clean V-shaped gap around front center ang=0!
        // Specifically: ang runs from +frontGap to (2*PI - frontGap)!
        float openGapRad = 0.24f; // ~14 degrees half-opening on front chest
        var jacketRings = new[]
        {
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 0.89f, -0.002f), rx = 0.184f, rz = 0.130f, vCoord = 0.00f },
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 0.98f, 0.002f), rx = 0.178f, rz = 0.126f, vCoord = 0.16f },
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 1.12f, 0.006f), rx = 0.180f, rz = 0.128f, vCoord = 0.38f },
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 1.27f, 0.010f), rx = 0.206f, rz = 0.140f, vCoord = 0.62f },
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 1.39f, 0.006f), rx = 0.222f, rz = 0.136f, vCoord = 0.80f },
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 1.465f, 0.000f), rx = 0.188f, rz = 0.118f, vCoord = 0.92f },
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 1.515f, -0.004f), rx = 0.098f, rz = 0.088f, vCoord = 1.00f }
        };
        // Wrap cleanly around back: from +openGapRad (right front lapel) through +PI (back) to (2*PI - openGapRad) (left front lapel)
        mb.AddArcLoft(jacketRings, 26, openGapRad, Mathf.PI * 2f - openGapRad, 0, false, false, torsoWeight);

        // Standing folded jacket collar around neck back & sides
        var collarRings = new[]
        {
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 1.47f, -0.005f), rx = 0.096f, rz = 0.086f, vCoord = 0.88f },
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 1.525f, -0.008f), rx = 0.092f, rz = 0.084f, vCoord = 0.98f }
        };
        mb.AddArcLoft(collarRings, 16, 0.48f, Mathf.PI * 2f - 0.48f, 0, false, false,
            _ => SkinnedMeshBuilder.MakeBW(B.UpperChest, 0.65f, B.Neck, 0.35f));

        // Short tailored field-jacket sleeves with anatomically rounded deltoid bend & snug closed cuff at mid-bicep (|x| = 0.355)
        for (int side = -1; side <= 1; side += 2)
        {
            B sh = side < 0 ? B.LeftShoulder : B.RightShoulder;
            B ua = side < 0 ? B.LeftUpperArm : B.RightUpperArm;

            float[] xs = { side * 0.145f, side * 0.190f, side * 0.255f, side * 0.320f, side * 0.350f, side * 0.358f };
            float[] ry = { 0.056f, 0.055f, 0.053f, 0.051f, 0.049f, 0.042f };
            float[] rz = { 0.058f, 0.057f, 0.055f, 0.053f, 0.051f, 0.044f };
            float[] cy = { 1.425f, 1.442f, 1.450f, 1.450f, 1.450f, 1.450f };
            float[] cz = { 0.002f, 0f, 0f, 0f, 0f, 0f };

            mb.AddHorizontalLimbLoft(xs, ry, rz, cy, cz, 16, 0, true, false, p =>
            {
                float ax = Mathf.Abs(p.x);
                if (ax < 0.20f)
                {
                    float t = SmoothStep(0.145f, 0.20f, ax);
                    return SkinnedMeshBuilder.MakeBW(B.UpperChest, 1f - t * 0.85f, sh, t * 0.55f, ua, t * 0.30f);
                }
                if (ax < 0.27f)
                {
                    float t = SmoothStep(0.20f, 0.27f, ax);
                    return SkinnedMeshBuilder.MakeBW(sh, (1f - t) * 0.4f, ua, 0.6f + t * 0.4f);
                }
                return SkinnedMeshBuilder.MakeBW(ua, 1f);
            });
        }

        // 3. Submesh 2: Fitted Dhaka Courier Backpack (cleanly contoured on back, flush top shoulder tabs)
        mb.AddContouredBox(
            new Vector3(0.0f, 1.21f, -0.168f),
            new Vector3(0.132f, 0.175f, 0.046f),
            Quaternion.Euler(3f, 0f, 0f),
            2,
            SkinnedMeshBuilder.MakeBW(B.Chest, 0.55f, B.UpperChest, 0.30f, B.Spine, 0.15f),
            0.25f);

        // 4. Submesh 3: Waist belt buckle & courier pack rear clasp
        mb.AddContouredBox(
            new Vector3(0f, 0.965f, 0.120f),
            new Vector3(0.026f, 0.018f, 0.010f),
            Quaternion.identity,
            3,
            SkinnedMeshBuilder.MakeBW(B.Hips, 0.8f, B.Spine, 0.2f),
            0.2f);
        mb.AddContouredBox(
            new Vector3(0f, 1.24f, -0.214f),
            new Vector3(0.055f, 0.014f, 0.007f),
            Quaternion.identity,
            3,
            SkinnedMeshBuilder.MakeBW(B.Chest, 0.6f, B.UpperChest, 0.4f),
            0.2f);

        return mb.ToMesh("M_JacketTorso", bindPoses);
    }

    static Mesh BuildTrousersMesh(Matrix4x4[] bindPoses)
    {
        var mb = new SkinnedMeshBuilder();

        var pelvisRings = new[]
        {
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 0.80f, 0f), rx = 0.088f, rz = 0.072f, vCoord = 0.62f },
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 0.845f, 0f), rx = 0.158f, rz = 0.106f, vCoord = 0.72f },
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 0.91f, 0f), rx = 0.168f, rz = 0.114f, vCoord = 0.84f },
            new SkinnedMeshBuilder.RingSpec { center = new Vector3(0, 0.96f, 0.002f), rx = 0.160f, rz = 0.110f, vCoord = 0.98f }
        };
        mb.AddLoft(pelvisRings, 22, 0, true, true, p =>
        {
            float t = SmoothStep(0.84f, 0.96f, p.y);
            if (p.y < 0.88f)
            {
                B leg = p.x < 0 ? B.LeftUpperLeg : B.RightUpperLeg;
                return SkinnedMeshBuilder.MakeBW(B.Hips, 0.7f, leg, 0.3f);
            }
            return SkinnedMeshBuilder.MakeBW(B.Hips, 1f - t * 0.2f, B.Spine, t * 0.2f);
        });

        for (int side = -1; side <= 1; side += 2)
        {
            B ul = side < 0 ? B.LeftUpperLeg : B.RightUpperLeg;
            B ll = side < 0 ? B.LeftLowerLeg : B.RightLowerLeg;
            float cx = side * 0.102f;

            var legRings = new[]
            {
                // Ankle cuff overlapping sneaker collar cleanly (y=0.075, rx=0.058 > shoe collar 0.048)
                new SkinnedMeshBuilder.RingSpec { center = new Vector3(cx, 0.075f, 0.00f), rx = 0.058f, rz = 0.062f, vCoord = 0.00f },
                new SkinnedMeshBuilder.RingSpec { center = new Vector3(cx, 0.120f, 0.00f), rx = 0.062f, rz = 0.066f, vCoord = 0.06f },
                new SkinnedMeshBuilder.RingSpec { center = new Vector3(cx, 0.28f, -0.008f), rx = 0.072f, rz = 0.076f, vCoord = 0.26f },
                new SkinnedMeshBuilder.RingSpec { center = new Vector3(cx, 0.44f, 0.002f), rx = 0.070f, rz = 0.074f, vCoord = 0.44f },
                new SkinnedMeshBuilder.RingSpec { center = new Vector3(cx, 0.48f, 0.006f), rx = 0.073f, rz = 0.078f, vCoord = 0.50f },
                new SkinnedMeshBuilder.RingSpec { center = new Vector3(cx, 0.52f, 0.004f), rx = 0.076f, rz = 0.080f, vCoord = 0.55f },
                new SkinnedMeshBuilder.RingSpec { center = new Vector3(cx, 0.68f, 0.004f), rx = 0.092f, rz = 0.094f, vCoord = 0.72f },
                new SkinnedMeshBuilder.RingSpec { center = new Vector3(cx * 0.92f, 0.83f, 0.002f), rx = 0.104f, rz = 0.106f, vCoord = 0.90f },
                new SkinnedMeshBuilder.RingSpec { center = new Vector3(cx * 0.78f, 0.92f, 0.000f), rx = 0.106f, rz = 0.110f, vCoord = 1.00f }
            };

            mb.AddLoft(legRings, 18, 0, true, true, p =>
            {
                if (p.y > 0.82f)
                {
                    float t = SmoothStep(0.80f, 0.92f, p.y);
                    return SkinnedMeshBuilder.MakeBW(ul, 1f - t * 0.75f, B.Hips, t * 0.75f);
                }
                if (p.y > 0.42f && p.y < 0.54f)
                {
                    float kt = SmoothStep(0.42f, 0.54f, p.y);
                    return SkinnedMeshBuilder.MakeBW(ll, 1f - kt, ul, kt);
                }
                if (p.y >= 0.54f) return SkinnedMeshBuilder.MakeBW(ul, 1f);
                return SkinnedMeshBuilder.MakeBW(ll, 1f);
            });
        }

        return mb.ToMesh("M_Trousers", bindPoses);
    }

    static Mesh BuildShoesMesh(Matrix4x4[] bindPoses)
    {
        var mb = new SkinnedMeshBuilder();

        for (int side = -1; side <= 1; side += 2)
        {
            B ft = side < 0 ? B.LeftFoot : B.RightFoot;
            B to = side < 0 ? B.LeftToes : B.RightToes;
            B ll = side < 0 ? B.LeftLowerLeg : B.RightLowerLeg;
            float cx = side * 0.102f;

            Func<Vector3, BoneWeight> shoeWeight = p =>
            {
                if (p.y > 0.085f)
                {
                    float t = SmoothStep(0.085f, 0.125f, p.y);
                    return SkinnedMeshBuilder.MakeBW(ft, 1f - t * 0.45f, ll, t * 0.45f);
                }
                float toeT = SmoothStep(0.11f, 0.18f, p.z);
                return SkinnedMeshBuilder.MakeBW(ft, 1f - toeT, to, toeT);
            };

            var collarRings = new[]
            {
                new SkinnedMeshBuilder.RingSpec { center = new Vector3(cx, 0.025f, 0.00f), rx = 0.048f, rz = 0.054f, vCoord = 0.0f },
                new SkinnedMeshBuilder.RingSpec { center = new Vector3(cx, 0.068f, 0.00f), rx = 0.048f, rz = 0.054f, vCoord = 0.5f },
                new SkinnedMeshBuilder.RingSpec { center = new Vector3(cx, 0.102f, -0.002f), rx = 0.046f, rz = 0.052f, vCoord = 1.0f }
            };
            mb.AddLoft(collarRings, 14, 0, true, true, shoeWeight);

            mb.AddEllipsoid(
                new Vector3(cx, 0.048f, 0.075f),
                new Vector3(0.055f, 0.034f, 0.135f),
                14, 10, 0,
                shoeWeight,
                p =>
                {
                    if (p.y < 0.026f) p.y = 0.026f;
                    if (p.z > 0.12f)
                    {
                        float t = SmoothStep(0.12f, 0.21f, p.z);
                        p.y = Mathf.Lerp(p.y, 0.036f, t * 0.5f);
                    }
                    return p;
                });

            mb.AddEllipsoid(
                new Vector3(cx, 0.015f, 0.075f),
                new Vector3(0.058f, 0.015f, 0.140f),
                14, 6, 1,
                shoeWeight,
                p =>
                {
                    p.y = Mathf.Clamp(p.y, 0.000f, 0.028f);
                    return p;
                });
        }

        return mb.ToMesh("M_Shoes", bindPoses);
    }

    #endregion
}
