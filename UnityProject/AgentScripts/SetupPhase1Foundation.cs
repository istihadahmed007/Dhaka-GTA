using System.IO;
using DhakaGta.Character;
using DhakaGta.Combat;
using DhakaGta.Core;
using DhakaGta.Data;
using DhakaGta.Editor;
using DhakaGta.Missions;
using DhakaGta.Streaming;
using DhakaGta.UI;
using DhakaGta.Vehicles;
using DhakaGta.World;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Headless Phase 1 setup entrypoint executed via unity-pipeline (`unity command script run`).
/// Imports TMP Essentials, configures Physics Layers/Matrix, syncs GIS ScriptableObjects,
/// builds Bootstrap/MainMenu/TestCity HDRP scenes, and registers EditorBuildSettings.
/// </summary>
public static class SetupPhase1Foundation
{
    private const string SkyProfilePath = "Assets/Settings/SkyandFogSettingsProfile.asset";
    private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
    private const string MaterialFolder = "Assets/Materials/Foundation";

    public static void Main()
    {
        Run();
    }

    private static void Run()
    {
        EnsureTmpEssentialsImported();
        PhysicsMatrixValidator.ApplyAndValidate();
        DhakaGeoJsonImporter.ImportedCatalog catalog = DhakaGeoJsonImporter.SyncAllFoundationAssets();

        VolumeProfile skyProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(SkyProfilePath);
        InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);

        MaterialPalette palette = EnsureFoundationMaterials();

        BuildBootstrapScene(skyProfile, inputActions, palette);
        BuildMainMenuScene(skyProfile, inputActions, catalog, palette);
        BuildTestCityScene(skyProfile, inputActions, catalog, palette);
        DhakaGeoEditorTools.BuildGeographicTestScene();
        GulshanHatirjheelWorldBuilder.BuildMilestoneScene();

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/Bootstrap.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/TestCity.unity", true),
            new EditorBuildSettingsScene(DhakaGeoEditorTools.GeoTestScenePath, true),
            new EditorBuildSettingsScene(GulshanHatirjheelWorldBuilder.MilestoneScenePath, true),
        };

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SetupPhase1Foundation] Completed Phase 1 Unity 6 HDRP Foundation setup.");
    }

    private struct MaterialPalette
    {
        public Material Asphalt;
        public Material Sidewalk;
        public Material LandmarkStone;
        public Material BuildingConcrete;
        public Material GlassTint;
        public Material HeroGarment;
        public Material CngGreen;
        public Material RiverWater;
    }

    private static void EnsureTmpEssentialsImported()
    {
        if (File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
        {
            return;
        }

        string uguiPath = Path.GetFullPath("Packages/com.unity.ugui");
        string packagePath = Path.Combine(uguiPath, "Package Resources", "TMP Essential Resources.unitypackage");
        if (File.Exists(packagePath))
        {
            AssetDatabase.ImportPackage(packagePath, interactive: false);
            AssetDatabase.Refresh();
        }
    }

    private static MaterialPalette EnsureFoundationMaterials()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
        {
            AssetDatabase.CreateFolder("Assets/Materials", "Foundation");
        }

        return new MaterialPalette
        {
            Asphalt = CreateHdrpLitMaterial("Mat_AsphaltRoad", new Color(0.14f, 0.14f, 0.16f), 0.35f),
            Sidewalk = CreateHdrpLitMaterial("Mat_ConcreteSidewalk", new Color(0.45f, 0.44f, 0.42f), 0.25f),
            LandmarkStone = CreateHdrpLitMaterial("Mat_ShaplaStone", new Color(0.86f, 0.84f, 0.80f), 0.45f),
            BuildingConcrete = CreateHdrpLitMaterial("Mat_DhakaFacade", new Color(0.56f, 0.50f, 0.44f), 0.3f),
            GlassTint = CreateHdrpLitMaterial("Mat_CorporateGlass", new Color(0.16f, 0.28f, 0.34f), 0.85f),
            HeroGarment = CreateHdrpLitMaterial("Mat_HeroJacket", new Color(0.18f, 0.24f, 0.32f), 0.35f),
            CngGreen = CreateHdrpLitMaterial("Mat_CngGreen", new Color(0.08f, 0.44f, 0.22f), 0.6f),
            RiverWater = CreateHdrpLitMaterial("Mat_BurigangaWater", new Color(0.10f, 0.26f, 0.28f), 0.82f),
        };
    }

    private static Material CreateHdrpLitMaterial(string name, Color color, float smoothness)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("HDRP/Lit");
            mat = new Material(shader != null ? shader : Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, path);
        }

        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }
        else
        {
            mat.color = color;
        }

        if (mat.HasProperty("_Smoothness"))
        {
            mat.SetFloat("_Smoothness", smoothness);
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void BuildBootstrapScene(VolumeProfile skyProfile, InputActionAsset inputActions, MaterialPalette palette)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateHdrpEnvironment("BootstrapEnvironment", skyProfile, null);
        Camera mainCam = CreateHdrpCamera("Main Camera", new Vector3(0f, 4f, -12f), Vector3.zero);
        mainCam.backgroundColor = new Color(0.05f, 0.06f, 0.09f);

        GameObject bootstrapGo = new GameObject("GameBootstrap");
        var bootstrap = bootstrapGo.AddComponent<GameBootstrap>();
        var router = bootstrapGo.AddComponent<InputActionRouter>();
        router.BindAsset(inputActions);

        CreateEventSystem(inputActions);

        Canvas canvas = CreateScreenCanvas("BootstrapCanvas");
        GameObject backdrop = CreatePanel(canvas.transform, "Backdrop", Vector2.zero, Vector2.one, new Color(0.04f, 0.05f, 0.07f, 0.94f));
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;

        TextMeshProUGUI title = CreateTmpLabel(backdrop.transform, "Title", "DHAKA-GTA", 54, font, new Color(0.96f, 0.78f, 0.30f));
        SetAnchors(title.rectTransform, new Vector2(0.5f, 0.58f), new Vector2(0.5f, 0.58f), new Vector2(680f, 80f));
        title.alignment = TextAlignmentOptions.Center;

        TextMeshProUGUI status = CreateTmpLabel(backdrop.transform, "StatusText", "Initializing HDRP Engine...", 22, font, Color.white);
        SetAnchors(status.rectTransform, new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.45f), new Vector2(720f, 44f));
        status.alignment = TextAlignmentOptions.Center;

        Slider progress = CreateProgressSlider(backdrop.transform, "BootProgress", new Vector2(440f, 16f), new Color(0.95f, 0.70f, 0.20f));
        SetAnchors((RectTransform)progress.transform, new Vector2(0.5f, 0.36f), new Vector2(0.5f, 0.36f), new Vector2(440f, 16f));

        var uiCtrl = canvas.gameObject.AddComponent<BootstrapScreenController>();
        uiCtrl.BindWidgets(status, progress);

        _ = palette;
        _ = bootstrap;
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Bootstrap.unity");
    }

    private static void BuildMainMenuScene(
        VolumeProfile skyProfile,
        InputActionAsset inputActions,
        DhakaGeoJsonImporter.ImportedCatalog catalog,
        MaterialPalette palette)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        WeatherPresetSO golden = catalog.WeatherPresets != null && catalog.WeatherPresets.Length > 0 ? catalog.WeatherPresets[0] : null;
        CreateHdrpEnvironment("MainMenuAtmosphere", skyProfile, golden);

        BuildShaplaSkylineStage(palette);

        var pivotGo = new GameObject("MenuCameraPivot");
        pivotGo.transform.position = Vector3.zero;
        Camera cam = CreateHdrpCamera("Main Camera", new Vector3(0f, 16f, -38f), new Vector3(0f, 5f, 0f));
        cam.transform.SetParent(pivotGo.transform, true);

        CreateEventSystem(inputActions);

        Canvas canvas = CreateScreenCanvas("MainMenuCanvas");
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;

        GameObject leftDock = CreatePanel(canvas.transform, "MenuPanel", new Vector2(0.04f, 0.14f), new Vector2(0.36f, 0.86f), new Color(0.05f, 0.06f, 0.09f, 0.80f));

        TextMeshProUGUI title = CreateTmpLabel(leftDock.transform, "GameTitle", "DHAKA-GTA", 46, font, new Color(0.96f, 0.78f, 0.30f));
        SetAnchors(title.rectTransform, new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.94f), Vector2.zero);

        TextMeshProUGUI subtitle = CreateTmpLabel(leftDock.transform, "Subtitle", "UNITY 6 HDRP · SHAPLA PLAZA VERTICAL SLICE", 16, font, new Color(0.80f, 0.84f, 0.90f));
        SetAnchors(subtitle.rectTransform, new Vector2(0.08f, 0.68f), new Vector2(0.92f, 0.77f), Vector2.zero);

        Button playBtn = CreateUiButton(leftDock.transform, "PlayButton", "ENTER DHAKA (TEST CITY)", font, out _);
        SetAnchors((RectTransform)playBtn.transform, new Vector2(0.08f, 0.50f), new Vector2(0.92f, 0.61f), Vector2.zero);

        Button qualityBtn = CreateUiButton(leftDock.transform, "QualityButton", "HDRP Quality: Balanced", font, out TextMeshProUGUI qualityLabel);
        SetAnchors((RectTransform)qualityBtn.transform, new Vector2(0.08f, 0.35f), new Vector2(0.92f, 0.46f), Vector2.zero);

        Button quitBtn = CreateUiButton(leftDock.transform, "QuitButton", "EXIT TO DESKTOP", font, out _);
        SetAnchors((RectTransform)quitBtn.transform, new Vector2(0.08f, 0.20f), new Vector2(0.92f, 0.31f), Vector2.zero);

        TextMeshProUGUI buildInfo = CreateTmpLabel(leftDock.transform, "BuildInfo", "Unity 6 HDRP Foundation", 13, font, new Color(0.65f, 0.70f, 0.78f));
        SetAnchors(buildInfo.rectTransform, new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.14f), Vector2.zero);

        var menuController = canvas.gameObject.AddComponent<MainMenuController>();
        menuController.BindWidgets(pivotGo.transform, playBtn, qualityBtn, quitBtn, qualityLabel, buildInfo);

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/MainMenu.unity");
    }

    private static void BuildTestCityScene(
        VolumeProfile skyProfile,
        InputActionAsset inputActions,
        DhakaGeoJsonImporter.ImportedCatalog catalog,
        MaterialPalette palette)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        WeatherPresetSO golden = catalog.WeatherPresets != null && catalog.WeatherPresets.Length > 0 ? catalog.WeatherPresets[0] : null;
        CreateHdrpEnvironment("TestCityAtmosphere", skyProfile, golden);

        BuildShaplaSkylineStage(palette);

        // GIS + Floor Builder Anchor
        var worldRoot = new GameObject("DhakaWorldRoot");
        var floorBuilder = worldRoot.AddComponent<DhakaCityFloorBuilder>();
        floorBuilder.SetReferences(catalog.Landmarks, catalog.Roads);

        // Input Router
        var inputGo = new GameObject("InputActionRouter");
        var router = inputGo.AddComponent<InputActionRouter>();
        router.BindAsset(inputActions);

        // Protagonist (Tariq Hasan at Shapla Crossing spawn)
        ProtagonistProfileSO tariq = catalog.Protagonists != null && catalog.Protagonists.Length > 0 ? catalog.Protagonists[0] : null;
        Vector3 spawnPos = tariq != null ? tariq.defaultSpawnPosition : new Vector3(-2.2f, 0.1f, 35.2f);

        GameObject player = new GameObject("Player_TariqHasan");
        player.layer = PhysicsLayers.Player;
        player.tag = "Player";
        player.transform.position = spawnPos;

        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.34f;
        cc.center = new Vector3(0f, 0.9f, 0f);

        GameObject bodyProxy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        bodyProxy.name = "BodyProxy";
        Object.DestroyImmediate(bodyProxy.GetComponent<Collider>());
        bodyProxy.layer = PhysicsLayers.Player;
        bodyProxy.transform.SetParent(player.transform, false);
        bodyProxy.transform.localPosition = new Vector3(0f, 0.95f, 0f);
        bodyProxy.transform.localScale = new Vector3(0.65f, 0.9f, 0.55f);
        bodyProxy.GetComponent<MeshRenderer>().sharedMaterial = palette.HeroGarment;

        GameObject headProxy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        headProxy.name = "HeadProxy";
        Object.DestroyImmediate(headProxy.GetComponent<Collider>());
        headProxy.layer = PhysicsLayers.Player;
        headProxy.transform.SetParent(player.transform, false);
        headProxy.transform.localPosition = new Vector3(0f, 1.72f, 0f);
        headProxy.transform.localScale = Vector3.one * 0.32f;

        GameObject camPivot = new GameObject("CameraFollowTarget");
        camPivot.transform.SetParent(player.transform, false);
        camPivot.transform.localPosition = new Vector3(0f, 1.45f, 0f);

        // Main Camera + Cinemachine 3 Brain & Follow Rig
        Camera mainCam = CreateHdrpCamera("Main Camera", spawnPos + new Vector3(0f, 2.5f, -5.5f), spawnPos + Vector3.up * 1.4f);
        mainCam.gameObject.AddComponent<CinemachineBrain>();

        var vcamGo = new GameObject("CM_ThirdPersonCamera");
        var cmCam = vcamGo.AddComponent<CinemachineCamera>();
        cmCam.Target = new CameraTarget { TrackingTarget = camPivot.transform };
        var follow = vcamGo.AddComponent<CinemachineThirdPersonFollow>();
        follow.ShoulderOffset = new Vector3(0.45f, 0.25f, 0f);
        follow.VerticalArmLength = 0.3f;
        follow.CameraSide = 0.75f;
        follow.CameraDistance = 4.6f;
        follow.Damping = new Vector3(0.15f, 0.25f, 0.25f);
#if CINEMACHINE_PHYSICS
        follow.AvoidObstacles.Enabled = true;
        follow.AvoidObstacles.CollisionFilter = PhysicsLayers.CameraOcclusionMask;
#endif

        var protagonistCtrl = player.AddComponent<ProtagonistController>();
        protagonistCtrl.Configure(tariq, router, mainCam.transform);

        var healthComp = player.AddComponent<HealthComponent>();
        healthComp.InitializeFromProfile(tariq);

        // Nearby CNG Auto-Rickshaw Spec Anchor
        if (catalog.Vehicles != null && catalog.Vehicles.Length > 0)
        {
            GameObject cng = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cng.name = "Vehicle_CngAuto_Proxy";
            cng.layer = PhysicsLayers.Vehicle;
            cng.transform.position = new Vector3(8.5f, 0.7f, 29f);
            cng.transform.localScale = new Vector3(1.35f, 1.4f, 2.45f);
            cng.GetComponent<MeshRenderer>().sharedMaterial = palette.CngGreen;
            var rb = cng.AddComponent<Rigidbody>();
            rb.mass = catalog.Vehicles[0].massKg;
            var anchor = cng.AddComponent<DhakaVehicleSpecAnchor>();
            anchor.Configure(catalog.Vehicles[0], trafficOwned: false);
        }

        // District Registry, Streaming, Missions
        var systemsGo = new GameObject("DhakaCitySystems");
        var districtRegistry = systemsGo.AddComponent<DhakaDistrictRegistry>();
        districtRegistry.SetCatalog(catalog.Districts, catalog.Landmarks, catalog.Roads, player.transform);

        var streamingCtrl = systemsGo.AddComponent<DistrictStreamingController>();
        streamingCtrl.Configure(catalog.StreamingConfig, player.transform);

        var missionRegistry = systemsGo.AddComponent<MissionRegistry>();
        missionRegistry.SetMissions(catalog.Missions);

        // HUD Canvas
        CreateEventSystem(inputActions);
        Canvas hudCanvas = CreateScreenCanvas("TestCityHudCanvas");
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;

        GameObject topRight = CreatePanel(hudCanvas.transform, "TopRightPanel", new Vector2(0.68f, 0.84f), new Vector2(0.98f, 0.96f), new Color(0.05f, 0.06f, 0.09f, 0.72f));
        TextMeshProUGUI districtTxt = CreateTmpLabel(topRight.transform, "DistrictLabel", "SHAPLA CROSSING", 24, font, new Color(0.96f, 0.78f, 0.30f));
        districtTxt.alignment = TextAlignmentOptions.Right;
        SetAnchors(districtTxt.rectTransform, new Vector2(0.06f, 0.48f), new Vector2(0.94f, 0.92f), Vector2.zero);

        TextMeshProUGUI roadTxt = CreateTmpLabel(topRight.transform, "RoadLabel", "Kazi Nazrul Islam Ave", 16, font, Color.white);
        roadTxt.alignment = TextAlignmentOptions.Right;
        SetAnchors(roadTxt.rectTransform, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.45f), Vector2.zero);

        TextMeshProUGUI badgeTxt = CreateTmpLabel(hudCanvas.transform, "StatusBadge", "HDRP Ready", 15, font, new Color(0.75f, 0.92f, 0.85f));
        SetAnchors(badgeTxt.rectTransform, new Vector2(0.02f, 0.92f), new Vector2(0.45f, 0.97f), Vector2.zero);

        GameObject bottomLeft = CreatePanel(hudCanvas.transform, "VitalsPanel", new Vector2(0.02f, 0.03f), new Vector2(0.24f, 0.13f), new Color(0.05f, 0.06f, 0.09f, 0.72f));
        Slider hpBar = CreateProgressSlider(bottomLeft.transform, "HealthBar", new Vector2(220f, 16f), new Color(0.20f, 0.78f, 0.42f));
        SetAnchors((RectTransform)hpBar.transform, new Vector2(0.06f, 0.56f), new Vector2(0.94f, 0.82f), Vector2.zero);

        Slider stBar = CreateProgressSlider(bottomLeft.transform, "StaminaBar", new Vector2(220f, 16f), new Color(0.24f, 0.65f, 0.94f));
        SetAnchors((RectTransform)stBar.transform, new Vector2(0.06f, 0.18f), new Vector2(0.94f, 0.44f), Vector2.zero);

        // Pause Overlay
        GameObject pausePanel = CreatePanel(hudCanvas.transform, "PauseModal", new Vector2(0.34f, 0.30f), new Vector2(0.66f, 0.70f), new Color(0.05f, 0.06f, 0.09f, 0.92f));
        TextMeshProUGUI pauseTitle = CreateTmpLabel(pausePanel.transform, "PauseTitle", "PAUSED", 34, font, new Color(0.96f, 0.78f, 0.30f));
        pauseTitle.alignment = TextAlignmentOptions.Center;
        SetAnchors(pauseTitle.rectTransform, new Vector2(0.10f, 0.72f), new Vector2(0.90f, 0.92f), Vector2.zero);

        Button resumeBtn = CreateUiButton(pausePanel.transform, "ResumeBtn", "RESUME CITY", font, out _);
        SetAnchors((RectTransform)resumeBtn.transform, new Vector2(0.14f, 0.42f), new Vector2(0.86f, 0.62f), Vector2.zero);

        Button mainMenuBtn = CreateUiButton(pausePanel.transform, "MainMenuBtn", "RETURN TO MAIN MENU", font, out _);
        SetAnchors((RectTransform)mainMenuBtn.transform, new Vector2(0.14f, 0.14f), new Vector2(0.86f, 0.34f), Vector2.zero);

        var hudController = hudCanvas.gameObject.AddComponent<TestCityHudController>();
        hudController.BindWidgets(
            router, districtRegistry, protagonistCtrl, healthComp,
            districtTxt, roadTxt, badgeTxt, hpBar, stBar, pausePanel, resumeBtn, mainMenuBtn);

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/TestCity.unity");
    }

    private static void BuildShaplaSkylineStage(MaterialPalette palette)
    {
        var stage = new GameObject("ShaplaPlazaFoundationStage");

        // Primary asphalt floor slab
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground_AsphaltBase";
        ground.layer = PhysicsLayers.Ground;
        ground.transform.SetParent(stage.transform, false);
        ground.transform.position = new Vector3(0f, -0.5f, 0f);
        ground.transform.localScale = new Vector3(540f, 1f, 540f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = palette.Asphalt;

        // Shapla Roundabout Island
        GameObject plaza = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        plaza.name = "ShaplaRoundaboutIsland";
        plaza.layer = PhysicsLayers.Ground;
        plaza.transform.SetParent(stage.transform, false);
        plaza.transform.position = new Vector3(0f, 0.1f, 0f);
        plaza.transform.localScale = new Vector3(18f, 0.2f, 18f);
        plaza.GetComponent<MeshRenderer>().sharedMaterial = palette.Sidewalk;

        // Shapla Monument Centerpiece Blockout
        GameObject monument = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        monument.name = "ShaplaWaterLilyMonument";
        monument.layer = PhysicsLayers.Building;
        monument.transform.SetParent(stage.transform, false);
        monument.transform.position = new Vector3(0f, 3.6f, 0f);
        monument.transform.localScale = new Vector3(4.5f, 3.6f, 4.5f);
        monument.GetComponent<MeshRenderer>().sharedMaterial = palette.LandmarkStone;

        // Motijheel Skyline Silhouette Massing Blocks
        Vector3[] blockPositions =
        {
            new Vector3(42f, 18f, 24f),
            new Vector3(62f, 26f, -18f),
            new Vector3(-38f, 14f, -28f),
            new Vector3(-44f, 12f, 36f),
            new Vector3(28f, 16f, -44f),
            new Vector3(126f, 32f, 32f), // Bangladesh Bank HQ zone
        };
        Vector3[] blockScales =
        {
            new Vector3(18f, 36f, 18f),
            new Vector3(20f, 52f, 20f),
            new Vector3(22f, 28f, 16f),
            new Vector3(16f, 24f, 20f),
            new Vector3(18f, 32f, 18f),
            new Vector3(24f, 64f, 24f),
        };

        for (int i = 0; i < blockPositions.Length; i++)
        {
            GameObject bldg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bldg.name = $"MotijheelBlock_{i + 1}";
            bldg.layer = PhysicsLayers.Building;
            bldg.transform.SetParent(stage.transform, false);
            bldg.transform.position = blockPositions[i];
            bldg.transform.localScale = blockScales[i];
            bldg.GetComponent<MeshRenderer>().sharedMaterial = (i % 2 == 0) ? palette.BuildingConcrete : palette.GlassTint;
        }

        // Buriganga River Zone South Trigger Plane
        GameObject river = GameObject.CreatePrimitive(PrimitiveType.Cube);
        river.name = "BurigangaRiverSurface";
        river.layer = PhysicsLayers.Water;
        river.transform.SetParent(stage.transform, false);
        river.transform.position = new Vector3(0f, -0.35f, 235f);
        river.transform.localScale = new Vector3(540f, 0.3f, 70f);
        river.GetComponent<MeshRenderer>().sharedMaterial = palette.RiverWater;
        river.GetComponent<BoxCollider>().isTrigger = true;
    }

    private static void CreateHdrpEnvironment(string name, VolumeProfile skyProfile, WeatherPresetSO weatherPreset)
    {
        var env = new GameObject(name);

        var sunGo = new GameObject("SunDirectionalLight");
        sunGo.transform.SetParent(env.transform, false);
        var light = sunGo.AddComponent<Light>();
        light.type = LightType.Directional;
        sunGo.AddComponent<HDAdditionalLightData>();

        var volGo = new GameObject("SkyAndFogVolume");
        volGo.transform.SetParent(env.transform, false);
        var volume = volGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = skyProfile;

        var rig = env.AddComponent<HdrpAtmosphereRig>();
        if (weatherPreset != null)
        {
            rig.ConfigureReferences(light, volume, weatherPreset);
        }
        else
        {
            sunGo.transform.rotation = Quaternion.Euler(24f, 235f, 0f);
            light.useColorTemperature = true;
            light.colorTemperature = 5200f;
            light.intensity = 75000f;
            light.shadows = LightShadows.Soft;
        }
    }

    private static Camera CreateHdrpCamera(string name, Vector3 position, Vector3 lookTarget)
    {
        var camGo = new GameObject(name);
        camGo.tag = "MainCamera";
        camGo.transform.position = position;
        camGo.transform.LookAt(lookTarget, Vector3.up);
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.25f;
        cam.farClipPlane = 1200f;
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<HDAdditionalCameraData>();
        return cam;
    }

    private static void CreateEventSystem(InputActionAsset inputActions)
    {
        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>();
        var uiModule = esGo.AddComponent<InputSystemUIInputModule>();
        if (inputActions != null)
        {
            uiModule.actionsAsset = inputActions;
        }
    }

    private static Canvas CreateScreenCanvas(string name)
    {
        var go = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static GameObject CreatePanel(Transform parent, string name, Vector2 minAnchor, Vector2 maxAnchor, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        SetAnchors(img.rectTransform, minAnchor, maxAnchor, Vector2.zero);
        return go;
    }

    private static TextMeshProUGUI CreateTmpLabel(Transform parent, string name, string text, int fontSize, TMP_FontAsset font, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        if (font != null)
        {
            label.font = font;
        }
        return label;
    }

    private static Button CreateUiButton(Transform parent, string name, string caption, TMP_FontAsset font, out TextMeshProUGUI labelOut)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.14f, 0.18f, 0.25f, 0.95f);
        var btn = go.AddComponent<Button>();

        var colors = btn.colors;
        colors.highlightedColor = new Color(0.95f, 0.72f, 0.24f, 1f);
        colors.pressedColor = new Color(0.76f, 0.55f, 0.14f, 1f);
        btn.colors = colors;

        labelOut = CreateTmpLabel(go.transform, "Text", caption, 22, font, Color.white);
        labelOut.alignment = TextAlignmentOptions.Center;
        SetAnchors(labelOut.rectTransform, Vector2.zero, Vector2.one, Vector2.zero);
        return btn;
    }

    private static Slider CreateProgressSlider(Transform parent, string name, Vector2 size, Color fillColor)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        var rt = root.AddComponent<RectTransform>();
        rt.sizeDelta = size;

        var bg = root.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.17f, 0.22f, 0.95f);

        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(root.transform, false);
        SetAnchors(fillArea.AddComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero);

        var fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = fillColor;
        SetAnchors(fillImg.rectTransform, Vector2.zero, Vector2.one, Vector2.zero);

        var slider = root.AddComponent<Slider>();
        slider.interactable = false;
        slider.fillRect = fillImg.rectTransform;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        return slider;
    }

    private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max, Vector2 sizeDelta)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        if (sizeDelta != Vector2.zero)
        {
            rt.sizeDelta = sizeDelta;
        }
    }
}
