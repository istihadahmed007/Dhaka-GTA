using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using ProjectDhaka;

public static class BuildAnimationLabScene
{
    const string ScenePath = "Assets/Scenes/AnimationLab.unity";
    const string ProtagonistPrefabPath = "Assets/Prefabs/DhakaProtagonist.prefab";
    const string EnvDir = "Assets/Environment/AnimationLab";

    public static string Build()
    {
        Directory.CreateDirectory(EnvDir);

        // Generate crisp calibration Dhaka wet asphalt texture & URP Lit materials (no Default-Material anywhere)
        Material groundMat = CreateLabAsphaltMaterial();
        Material coneOrangeMat = CreateLitMat("M_Lab_ConeOrange", EnvDir + "/M_Lab_ConeOrange.mat", new Color(0.95f, 0.36f, 0.08f), 0.42f, 0.05f);
        Material coneWhiteMat = CreateLitMat("M_Lab_ConeReflect", EnvDir + "/M_Lab_ConeReflect.mat", new Color(0.94f, 0.96f, 0.98f), 0.72f, 0.15f);
        Material coneBaseMat = CreateLitMat("M_Lab_ConeBase", EnvDir + "/M_Lab_ConeBase.mat", new Color(0.11f, 0.12f, 0.14f), 0.25f, 0.05f);
        Material slopeMat = CreateLitMat("M_Lab_Concrete", EnvDir + "/M_Lab_Concrete.mat", new Color(0.32f, 0.35f, 0.39f), 0.34f, 0.04f);
        Material stepEdgeMat = CreateLitMat("M_Lab_StepTrim", EnvDir + "/M_Lab_StepTrim.mat", Palette.Accent, 0.50f, 0.10f);
        Material bumpMat = CreateLitMat("M_Lab_CobbleBump", EnvDir + "/M_Lab_CobbleBump.mat", new Color(0.40f, 0.34f, 0.30f), 0.30f, 0.04f);
        Material crateMat = CreateLitMat("M_Lab_InteractCrate", EnvDir + "/M_Lab_InteractCrate.mat", Palette.WallsTerracotta, 0.38f, 0.08f);
        Material barrierMat = CreateLitMat("M_Lab_Barrier", EnvDir + "/M_Lab_Barrier.mat", new Color(0.24f, 0.27f, 0.31f), 0.36f, 0.06f);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 1. Lighting & Atmosphere (Monsoon Dhaka golden-dusk calibration clarity)
        var lightGo = new GameObject("SunKeyLight");
        var sun = lightGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1.0f, 0.93f, 0.84f);
        sun.intensity = 1.65f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.85f;
        lightGo.transform.rotation = Quaternion.Euler(42f, -35f, 0f);

        var fillGo = new GameObject("SkyFillLight");
        var fill = fillGo.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(0.55f, 0.72f, 0.96f);
        fill.intensity = 0.42f;
        fill.shadows = LightShadows.None;
        fillGo.transform.rotation = Quaternion.Euler(140f, 30f, 0f);

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.44f, 0.56f, 0.74f);
        RenderSettings.ambientEquatorColor = new Color(0.34f, 0.38f, 0.44f);
        RenderSettings.ambientGroundColor = new Color(0.16f, 0.17f, 0.19f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.46f, 0.54f, 0.64f);
        RenderSettings.fogDensity = 0.0042f;

        // URP Post Processing Volume
        var volGo = new GameObject("GlobalVolume");
        var vol = volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        var volProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/DhakaVolumeProfile.asset");
        if (volProfile == null) volProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/SampleSceneProfile.asset");
        vol.sharedProfile = volProfile;

        // 2. Large Calibration Ground (120m x 120m) + Low Perimeter Safety Curbs
        var worldRoot = new GameObject("AnimationLab_Environment");
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground_CalibrationAsphalt";
        ground.transform.SetParent(worldRoot.transform, false);
        ground.transform.position = new Vector3(0f, -0.5f, 0f);
        ground.transform.localScale = new Vector3(120f, 1f, 120f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;

        CreateBoxProp("PerimeterCurb_N", worldRoot.transform, new Vector3(0f, 0.55f, 59.5f), new Vector3(120f, 1.1f, 1.0f), Vector3.zero, barrierMat);
        CreateBoxProp("PerimeterCurb_S", worldRoot.transform, new Vector3(0f, 0.55f, -59.5f), new Vector3(120f, 1.1f, 1.0f), Vector3.zero, barrierMat);
        CreateBoxProp("PerimeterCurb_E", worldRoot.transform, new Vector3(59.5f, 0.55f, 0f), new Vector3(1.0f, 1.1f, 120f), Vector3.zero, barrierMat);
        CreateBoxProp("PerimeterCurb_W", worldRoot.transform, new Vector3(-59.5f, 0.55f, 0f), new Vector3(1.0f, 1.1f, 120f), Vector3.zero, barrierMat);

        // 3. Multiple Traffic Cones (single ahead, slalom line, chicane cluster)
        var conesRoot = new GameObject("TrafficCones");
        conesRoot.transform.SetParent(worldRoot.transform, false);

        Vector3[] conePositions =
        {
            // Direct ahead obstacle + slalom corridor along +Z
            new Vector3(0.0f, 0f, 4.5f),
            new Vector3(-1.05f, 0f, 8.2f),
            new Vector3(0.95f, 0f, 11.4f),
            new Vector3(-0.90f, 0f, 14.6f),
            new Vector3(1.05f, 0f, 17.8f),
            // Double gate & offset cluster on right (+X, +Z)
            new Vector3(5.5f, 0f, 5.2f),
            new Vector3(7.3f, 0f, 5.2f),
            new Vector3(6.4f, 0f, 8.0f),
            new Vector3(5.2f, 0f, 10.6f),
            new Vector3(7.6f, 0f, 10.6f),
            new Vector3(6.4f, 0f, 13.4f),
            // Dense cone/barrier lane on left (-X)
            new Vector3(-5.5f, 0f, 6.0f),
            new Vector3(-6.2f, 0f, 8.4f),
            new Vector3(-4.9f, 0f, 10.6f)
        };
        for (int i = 0; i < conePositions.Length; i++)
        {
            CreateTrafficCone($"Cone_{i + 1:00}", conesRoot.transform, conePositions[i], coneBaseMat, coneOrangeMat, coneWhiteMat);
        }

        // Low Dhaka concrete street barriers alongside left test lane
        CreateBoxProp("StreetBarrier_01", worldRoot.transform, new Vector3(-7.6f, 0.42f, 9.5f), new Vector3(0.55f, 0.84f, 4.2f), Vector3.zero, barrierMat);
        CreateBoxProp("StreetBarrier_02", worldRoot.transform, new Vector3(11.2f, 0.42f, 9.0f), new Vector3(0.55f, 0.84f, 6.0f), new Vector3(0f, 18f, 0f), barrierMat);

        // 4. Uneven Terrain Pads (cobblestone / rubble height steps for L/R Foot IK + pelvis drop)
        var unevenRoot = new GameObject("UnevenGroundCourse");
        unevenRoot.transform.SetParent(worldRoot.transform, false);
        for (int row = 0; row < 5; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                float h = 0.08f + ((row * 3 + col) % 4) * 0.055f + (col == 1 ? 0.06f : 0f);
                Vector3 pos = new Vector3(5.2f + col * 1.45f, h * 0.5f, -5.5f + row * 1.45f);
                CreateBoxProp($"UnevenPad_{row}_{col}", unevenRoot.transform, pos, new Vector3(1.25f, h, 1.25f),
                    new Vector3((row % 2 == 0 ? 1 : -1) * 3.5f, 0f, (col % 2 == 0 ? -1 : 1) * 4f), bumpMat);
            }
        }

        // 5. Slopes & Steps Course (Left side -X and behind/side for quick camera view)
        var slopeRoot = new GameObject("SlopesAndSteps");
        slopeRoot.transform.SetParent(worldRoot.transform, false);

        // Gentle 14-deg ramp rising to y=1.35m plateau, plus steeper 23-deg return ramp
        CreateRamp("Slope_14Deg", slopeRoot.transform, new Vector3(-8.5f, 0f, 1.0f), 3.6f, 5.4f, 1.35f, 0f, slopeMat);
        CreateBoxProp("ElevatedPlateau", slopeRoot.transform, new Vector3(-8.5f, 0.675f, 5.4f), new Vector3(3.6f, 1.35f, 3.6f), Vector3.zero, slopeMat);
        CreateRamp("Slope_23Deg", slopeRoot.transform, new Vector3(-8.5f, 0f, 8.9f), 3.6f, 3.2f, 1.35f, 180f, slopeMat);

        // Stepped stairs (9 steps, 0.16m riser, 0.36m tread) at (-4.2, 0, -5.0)
        int stepCount = 9;
        float riser = 0.16f;
        float tread = 0.36f;
        for (int s = 0; s < stepCount; s++)
        {
            float topY = (s + 1) * riser;
            CreateBoxProp($"Step_{s + 1:00}", slopeRoot.transform,
                new Vector3(-4.2f, topY * 0.5f, -3.0f - s * tread),
                new Vector3(3.2f, topY, tread + 0.02f), Vector3.zero, s % 2 == 0 ? slopeMat : bumpMat);
            CreateBoxProp($"StepTrim_{s + 1:00}", slopeRoot.transform,
                new Vector3(-4.2f, topY - 0.015f, -3.0f - s * tread + tread * 0.45f),
                new Vector3(3.22f, 0.03f, 0.04f), Vector3.zero, stepEdgeMat);
        }
        CreateBoxProp("StairLanding", slopeRoot.transform,
            new Vector3(-4.2f, stepCount * riser * 0.5f, -3.0f - stepCount * tread - 1.3f),
            new Vector3(3.4f, stepCount * riser, 2.8f), Vector3.zero, slopeMat);
        CreateRamp("StairSideRamp", slopeRoot.transform,
            new Vector3(-1.3f, 0f, -2.8f), 2.4f, 4.6f, stepCount * riser, 180f, slopeMat);

        // 6. Interactable Props for Head Look & Hand IK Reach
        CreateBoxProp("Interact_DhakaSupplyCrate", worldRoot.transform, new Vector3(2.8f, 0.48f, 2.6f), new Vector3(0.95f, 0.96f, 0.95f), Vector3.zero, crateMat);
        CreateBoxProp("Interact_ControlConsole", worldRoot.transform, new Vector3(-2.8f, 0.55f, 2.4f), new Vector3(0.75f, 1.10f, 0.60f), new Vector3(0f, 25f, 0f), stepEdgeMat);

        // 7. Protagonist Instance + Camera Rig
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProtagonistPrefabPath);
        if (prefab == null) throw new FileNotFoundException("Missing protagonist prefab", ProtagonistPrefabPath);
        var playerGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        playerGo.name = "DhakaProtagonist";
        playerGo.transform.position = new Vector3(0f, 0.01f, 0f);
        playerGo.transform.rotation = Quaternion.identity;

        var followProxy = new GameObject("CameraFollowProxy").transform;
        followProxy.position = playerGo.transform.position;

        var mainCamGo = new GameObject("Main Camera");
        mainCamGo.tag = "MainCamera";
        var cam = mainCamGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.12f;
        cam.farClipPlane = 350f;
        cam.fieldOfView = 54f;
        var urpCam = mainCamGo.AddComponent<UniversalAdditionalCameraData>();
        urpCam.renderPostProcessing = true;
        urpCam.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        mainCamGo.AddComponent<CinemachineBrain>();
        mainCamGo.AddComponent<AudioListener>();
        mainCamGo.transform.position = new Vector3(0.46f, 1.85f, -4.8f);

        var gameplayCamGo = new GameObject("CM_GameplayFollow");
        var gameplayCam = gameplayCamGo.AddComponent<CinemachineCamera>();
        gameplayCam.Priority = 20;
        gameplayCam.Target.TrackingTarget = followProxy;
        var tpf = gameplayCamGo.AddComponent<CinemachineThirdPersonFollow>();
        tpf.Damping = new Vector3(0.08f, 0.16f, 0.08f);
        tpf.ShoulderOffset = new Vector3(0.46f, 1.50f, 0f);
        tpf.VerticalArmLength = 0.16f;
        tpf.CameraSide = 1f;
        tpf.CameraDistance = 4.8f;

        var orbitCamGo = new GameObject("CM_CinematicOrbit");
        var orbitCam = orbitCamGo.AddComponent<CinemachineCamera>();
        orbitCam.Priority = 5;
        orbitCam.Target.TrackingTarget = followProxy;
        var orbitFollow = orbitCamGo.AddComponent<CinemachineThirdPersonFollow>();
        orbitFollow.Damping = new Vector3(0.14f, 0.2f, 0.14f);
        orbitFollow.ShoulderOffset = new Vector3(0.15f, 1.38f, 0f);
        orbitFollow.CameraDistance = 3.2f;

        var camRigGo = new GameObject("DhakaCameraRig");
        var impulse = camRigGo.AddComponent<CinemachineImpulseSource>();
        var camRig = camRigGo.AddComponent<DhakaCameraRig>();
        camRig.BindRig(orbitCam, gameplayCam, followProxy, playerGo.transform, impulse);

        // 8. Clean Corner Control Hint Strip (AnimationLab only)
        BuildLabHintOverlay();

        EditorSceneManager.SaveScene(scene, ScenePath);

        // Ensure SampleScene.unity is index 0 and AnimationLab.unity is in Build Settings
        var buildScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
        if (File.Exists("Assets/Scenes/SampleScene.unity"))
            buildScenes.Add(new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", true));
        buildScenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = buildScenes.ToArray();

        return $"Built {ScenePath} cleanly with 14 Cones, 15 Uneven Pads, 2 Slopes, 9 Stairs, Protagonist & Cinemachine3 Rig";
    }

    static void CreateTrafficCone(string name, Transform parent, Vector3 pos, Material baseMat, Material orangeMat, Material whiteMat)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = pos;

        // Weighted rubber base + tapered orange body + reflective collar
        var baseBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
        baseBox.name = "Base";
        baseBox.transform.SetParent(root.transform, false);
        baseBox.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        baseBox.transform.localScale = new Vector3(0.42f, 0.06f, 0.42f);
        Object.DestroyImmediate(baseBox.GetComponent<Collider>());
        baseBox.GetComponent<MeshRenderer>().sharedMaterial = baseMat;

        // Tapered cone mesh built from 3 stacked frustum rings
        var bodyGo = new GameObject("Body");
        bodyGo.transform.SetParent(root.transform, false);
        var mf = bodyGo.AddComponent<MeshFilter>();
        mf.sharedMesh = BuildConeMesh(0.145f, 0.042f, 0.76f);
        var mr = bodyGo.AddComponent<MeshRenderer>();
        mr.sharedMaterials = new[] { orangeMat, whiteMat };

        // Single clean CapsuleCollider covering full cone height above CC stepOffset (0.35m < 0.78m)
        var col = root.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 0.39f, 0f);
        col.radius = 0.20f;
        col.height = 0.78f;
    }

    static Mesh BuildConeMesh(float rBot, float rTop, float height)
    {
        Mesh m = new Mesh { name = "TrafficConeMesh" };
        int segs = 16;
        // 5 height rings: [0..0.32] orange, [0.32..0.52] reflective white, [0.52..1.0] orange
        float[] hs = { 0.05f, 0.32f, 0.32f, 0.50f, 0.50f, height };
        var verts = new System.Collections.Generic.List<Vector3>();
        var norms = new System.Collections.Generic.List<Vector3>();
        var uvs = new System.Collections.Generic.List<Vector2>();
        var triOrange = new System.Collections.Generic.List<int>();
        var triWhite = new System.Collections.Generic.List<int>();

        for (int r = 0; r < hs.Length; r++)
        {
            float t = hs[r] / height;
            float rad = Mathf.Lerp(rBot, rTop, t);
            for (int s = 0; s <= segs; s++)
            {
                float ang = (s / (float)segs) * Mathf.PI * 2f;
                Vector3 radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                verts.Add(radial * rad + Vector3.up * hs[r]);
                norms.Add((radial + Vector3.up * 0.18f).normalized);
                uvs.Add(new Vector2(s / (float)segs, t));
            }
        }

        int stride = segs + 1;
        AddBand(0, stride, segs, triOrange);
        AddBand(2, stride, segs, triWhite);
        AddBand(4, stride, segs, triOrange);

        // Top cap
        int centerTop = verts.Count;
        verts.Add(new Vector3(0f, height, 0f));
        norms.Add(Vector3.up);
        uvs.Add(new Vector2(0.5f, 1f));
        int ringTop = 5 * stride;
        for (int s = 0; s < segs; s++)
        {
            triOrange.Add(ringTop + s);
            triOrange.Add(centerTop);
            triOrange.Add(ringTop + s + 1);
        }

        m.subMeshCount = 2;
        m.SetVertices(verts);
        m.SetNormals(norms);
        m.SetUVs(0, uvs);
        m.SetTriangles(triOrange, 0);
        m.SetTriangles(triWhite, 1);
        m.RecalculateBounds();
        return m;
    }

    static void AddBand(int baseRing, int stride, int segs, System.Collections.Generic.List<int> tris)
    {
        int r0 = baseRing * stride;
        int r1 = (baseRing + 1) * stride;
        for (int s = 0; s < segs; s++)
        {
            tris.Add(r0 + s);
            tris.Add(r1 + s);
            tris.Add(r0 + s + 1);
            tris.Add(r0 + s + 1);
            tris.Add(r1 + s);
            tris.Add(r1 + s + 1);
        }
    }

    static void CreateBoxProp(string name, Transform parent, Vector3 pos, Vector3 scale, Vector3 euler, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static void CreateRamp(string name, Transform parent, Vector3 baseStartPos, float width, float run, float rise, float yawDeg, Material mat)
    {
        float hyp = Mathf.Sqrt(run * run + rise * rise);
        float pitchDeg = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
        var pivot = new GameObject(name);
        pivot.transform.SetParent(parent, false);
        pivot.transform.position = baseStartPos;
        pivot.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);

        var wedge = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wedge.name = "RampSurface";
        wedge.transform.SetParent(pivot.transform, false);
        wedge.transform.localScale = new Vector3(width, 0.24f, hyp);
        wedge.transform.localRotation = Quaternion.Euler(-pitchDeg, 0f, 0f);
        wedge.transform.localPosition = new Vector3(0f, rise * 0.5f - 0.11f, run * 0.5f);
        wedge.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static Material CreateLabAsphaltMaterial()
    {
        string texPath = EnvDir + "/T_LabAsphalt.png";
        string matPath = EnvDir + "/M_LabAsphalt.mat";

        int size = 512;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, false);
        var rng = new System.Random(42);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float gx = Mathf.Abs(((x % 64) - 32)) <= 1 ? 1f : 0f;
                float gy = Mathf.Abs(((y % 64) - 32)) <= 1 ? 1f : 0f;
                float major = (x < 3 || y < 3) ? 1f : 0f;
                float grain = (float)rng.NextDouble() * 0.045f - 0.022f;
                float c = 0.16f + grain + Mathf.Max(gx, gy) * 0.045f + major * 0.11f;
                tex.SetPixel(x, y, new Color(c * 0.96f, c * 0.98f, c * 1.04f, 1f));
            }
        }
        tex.Apply();
        File.WriteAllBytes(texPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(texPath);

        var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        loaded.wrapMode = TextureWrapMode.Repeat;
        loaded.anisoLevel = 8;

        var mat = CreateLitMat("M_LabAsphalt", matPath, Color.white, 0.52f, 0.02f);
        mat.SetTexture("_BaseMap", loaded);
        mat.SetTextureScale("_BaseMap", new Vector2(22f, 22f));
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    static Material CreateLitMat(string name, string assetPath, Color color, float smoothness, float metallic)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        var mat = existing != null ? existing : new Material(lit);
        mat.name = name;
        mat.shader = lit;
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", metallic);
        if (existing == null) AssetDatabase.CreateAsset(mat, assetPath);
        else EditorUtility.SetDirty(mat);
        return mat;
    }

    static void BuildLabHintOverlay()
    {
        var canvasGo = new GameObject("AnimationLab_HUD");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var panelGo = new GameObject("ControlHintsBar");
        panelGo.transform.SetParent(canvasGo.transform, false);
        var rt = panelGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 16f);
        rt.sizeDelta = new Vector2(1140f, 38f);
        var bg = panelGo.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.07f, 0.11f, 0.74f);

        var textGo = new GameObject("HintText");
        textGo.transform.SetParent(panelGo.transform, false);
        var trt = textGo.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(16f, 4f);
        trt.offsetMax = new Vector2(-16f, -4f);
        var txt = textGo.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 18;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = new Color(0.92f, 0.96f, 1f, 0.95f);
        txt.text = "PROJECT DHAKA ANIMATION LAB   |   WASD Move   |   Alt Walk   |   Shift Sprint   |   Space Jump   |   RMB Strafe   |   T Trajectory Arrows   |   F3 IK Debug   |   V Cam   |   C Orbit";
    }
}
