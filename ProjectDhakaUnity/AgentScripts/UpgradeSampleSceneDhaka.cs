using System.Collections.Generic;
using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using ProjectDhaka;

public static class UpgradeSampleSceneDhaka
{
    const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    const string AnimationLabPath = "Assets/Scenes/AnimationLab.unity";
    const string ProtagonistPrefabPath = "Assets/Prefabs/DhakaProtagonist.prefab";
    const string EnvDir = "Assets/Environment/DhakaOpenWorld";

    public static string Upgrade()
    {
        Directory.CreateDirectory(EnvDir);

        var scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

        // Clean up empty leftover test stubs if present
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == "DhakaPlayModeVerifier" && root.GetComponents<Component>().Length <= 1 && root.transform.childCount == 0)
            {
                Object.DestroyImmediate(root);
            }
        }

        // 1. Upgrade DhakaCourier -> DhakaProtagonist while preserving GameLoop & DhakaCameraRig
        Vector3 spawnPos = new Vector3(0f, 0.01f, -4.5f);
        Quaternion spawnRot = Quaternion.identity;
        var existingCourier = GameObject.Find("DhakaCourier");
        if (existingCourier != null)
        {
            spawnPos = new Vector3(existingCourier.transform.position.x, 0.01f, existingCourier.transform.position.z);
            spawnRot = existingCourier.transform.rotation;
            Object.DestroyImmediate(existingCourier);
        }

        var oldUpgrade = GameObject.Find("DhakaProtagonist");
        if (oldUpgrade != null) Object.DestroyImmediate(oldUpgrade);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProtagonistPrefabPath);
        if (prefab == null) throw new FileNotFoundException("Missing protagonist prefab", ProtagonistPrefabPath);

        var playerGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        playerGo.name = "DhakaProtagonist";
        playerGo.transform.SetPositionAndRotation(spawnPos, spawnRot);
        var playerCtrl = playerGo.GetComponent<PlayerController>();
        playerCtrl.SetArenaClamp(false);

        var gameLoop = Object.FindAnyObjectByType<GameLoop>();
        if (gameLoop != null)
        {
            gameLoop.BindPlayer(playerCtrl, null);
            EditorUtility.SetDirty(gameLoop);
        }

        var camRig = Object.FindAnyObjectByType<DhakaCameraRig>();
        if (camRig != null)
        {
            CinemachineCamera titleCam = null;
            CinemachineCamera gameplayCam = null;
            Transform proxy = null;
            foreach (Transform child in camRig.transform)
            {
                if (child.name.Contains("Title") || child.name.Contains("Orbit")) titleCam = child.GetComponent<CinemachineCamera>();
                else if (child.name.Contains("Gameplay") || child.name.Contains("FollowCam")) gameplayCam = child.GetComponent<CinemachineCamera>();
                else if (child.name.Contains("Proxy")) proxy = child;
            }
            titleCam ??= camRig.TitleOrbitCam;
            gameplayCam ??= camRig.GameplayFollowCam;
            if (proxy == null) proxy = GameObject.Find("FollowProxy")?.transform;

            var impulse = camRig.GetComponent<CinemachineImpulseSource>();
            if (gameplayCam != null)
            {
                var tpf = gameplayCam.GetComponent<CinemachineThirdPersonFollow>();
                if (tpf != null)
                {
                    tpf.Damping = new Vector3(0.08f, 0.16f, 0.08f);
                    tpf.ShoulderOffset = new Vector3(0.46f, 1.50f, 0f);
                    tpf.CameraDistance = 4.8f;
                }
            }
            if (titleCam != null)
            {
                titleCam.Target.TrackingTarget = proxy;
                titleCam.Target.LookAtTarget = playerGo.transform;
                var orb = titleCam.GetComponent<CinemachineOrbitalFollow>();
                if (orb != null)
                {
                    orb.Radius = 4.6f;
                    orb.TargetOffset = new Vector3(0f, 1.35f, 0f);
                    orb.VerticalAxis.Value = 10f;
                }
                var tpfTitle = titleCam.GetComponent<CinemachineThirdPersonFollow>();
                if (tpfTitle != null)
                {
                    tpfTitle.ShoulderOffset = new Vector3(0.45f, 1.55f, 0f);
                    tpfTitle.CameraDistance = 4.4f;
                }
            }
            if (proxy != null)
            {
                proxy.position = spawnPos;
                proxy.rotation = Quaternion.Euler(10f, 22f, 0f);
            }
            camRig.BindRig(titleCam, gameplayCam, proxy, playerGo.transform, impulse);
            EditorUtility.SetDirty(camRig);
        }

        // 2. Build Dhaka Materials (Wet Road, Puddles, Concrete Flyover, Buildings, Vehicles)
        Material wetRoadMat = CreateWetAsphaltMaterial();
        Material sidewalkMat = CreateLitMat("M_Dhaka_Sidewalk", EnvDir + "/M_Dhaka_Sidewalk.mat", new Color(0.24f, 0.25f, 0.28f), 0.32f, 0.03f);
        Material puddleMat = CreatePuddleMaterial();
        Material stripeWhiteMat = CreateLitMat("M_Dhaka_LaneWhite", EnvDir + "/M_Dhaka_LaneWhite.mat", new Color(0.88f, 0.90f, 0.92f), 0.55f, 0.02f);
        Material stripeGoldMat = CreateLitMat("M_Dhaka_LaneGold", EnvDir + "/M_Dhaka_LaneGold.mat", new Color(0.92f, 0.72f, 0.18f), 0.52f, 0.02f);
        Material flyoverMat = CreateLitMat("M_Dhaka_Flyover", EnvDir + "/M_Dhaka_Flyover.mat", new Color(0.36f, 0.38f, 0.41f), 0.42f, 0.05f);
        Material treeTrunkMat = CreateLitMat("M_Dhaka_Trunk", EnvDir + "/M_Dhaka_Trunk.mat", new Color(0.25f, 0.19f, 0.14f), 0.18f, 0f);
        Material treeFoliageMat = CreateLitMat("M_Dhaka_Foliage", EnvDir + "/M_Dhaka_Foliage.mat", new Color(0.14f, 0.34f, 0.18f), 0.28f, 0f);

        Material bldgTerracotta = CreateLitMat("M_Bldg_Terracotta", EnvDir + "/M_Bldg_Terracotta.mat", new Color(0.62f, 0.31f, 0.22f), 0.24f, 0.02f);
        Material bldgCream = CreateLitMat("M_Bldg_Cream", EnvDir + "/M_Bldg_Cream.mat", new Color(0.76f, 0.72f, 0.64f), 0.28f, 0.02f);
        Material bldgSlate = CreateLitMat("M_Bldg_Slate", EnvDir + "/M_Bldg_Slate.mat", new Color(0.32f, 0.36f, 0.42f), 0.34f, 0.08f);
        Material bldgWindowMat = CreateEmissiveMat("M_Bldg_Windows", EnvDir + "/M_Bldg_Windows.mat", new Color(0.18f, 0.26f, 0.34f), new Color(1.0f, 0.80f, 0.42f) * 0.65f, 0.78f);

        Material cngGreenMat = CreateLitMat("M_Veh_CNG_Green", EnvDir + "/M_Veh_CNG_Green.mat", new Color(0.08f, 0.48f, 0.24f), 0.56f, 0.22f);
        Material cngCanopyMat = CreateLitMat("M_Veh_CNG_Canopy", EnvDir + "/M_Veh_CNG_Canopy.mat", new Color(0.16f, 0.20f, 0.18f), 0.32f, 0.05f);
        Material busRedMat = CreateLitMat("M_Veh_Bus_Red", EnvDir + "/M_Veh_Bus_Red.mat", new Color(0.76f, 0.16f, 0.14f), 0.55f, 0.25f);
        Material sedanMat = CreateLitMat("M_Veh_Sedan_Silver", EnvDir + "/M_Veh_Sedan_Silver.mat", new Color(0.74f, 0.77f, 0.80f), 0.76f, 0.45f);
        Material motoMat = CreateLitMat("M_Veh_Moto_Dark", EnvDir + "/M_Veh_Moto_Dark.mat", new Color(0.18f, 0.22f, 0.28f), 0.65f, 0.38f);
        Material wheelMat = CreateLitMat("M_Veh_Tire", EnvDir + "/M_Veh_Tire.mat", new Color(0.09f, 0.09f, 0.10f), 0.22f, 0.04f);
        Material headlightMat = CreateEmissiveMat("M_Veh_Headlight", EnvDir + "/M_Veh_Headlight.mat", Color.white, new Color(1.0f, 0.92f, 0.75f) * 1.8f, 0.85f);
        Material sodiumBulbMat = CreateEmissiveMat("M_Dhaka_SodiumBulb", EnvDir + "/M_Dhaka_SodiumBulb.mat", new Color(1f, 0.78f, 0.36f), new Color(1.0f, 0.72f, 0.28f) * 1.4f, 0.6f);

        // 3. Build / Refresh DhakaWorld/DhakaOpenWorldExpansion cleanly
        var worldRoot = GameObject.Find("DhakaWorld");
        if (worldRoot == null) worldRoot = new GameObject("DhakaWorld");

        var existingExp = worldRoot.transform.Find("DhakaOpenWorldExpansion");
        if (existingExp != null) Object.DestroyImmediate(existingExp.gameObject);

        var expRoot = new GameObject("DhakaOpenWorldExpansion").transform;
        expRoot.SetParent(worldRoot.transform, false);

        // 3A. Outer Dhaka Boulevards & Sidewalks (just below y=0 at y=-0.025m outside core plaza so zero Z-fighting)
        CreateStaticBox("OuterGround_Base", expRoot, new Vector3(0f, -0.53f, 0f), new Vector3(220f, 1f, 220f), Vector3.zero, sidewalkMat);

        // 4 Outer Wet Monsoon Avenues enclosing core ±22m plaza + extending N/S/E/W to ±95m
        CreateStaticBox("Boulevard_North", expRoot, new Vector3(0f, -0.024f, 58f), new Vector3(220f, 0.04f, 70f), Vector3.zero, wetRoadMat);
        CreateStaticBox("Boulevard_South", expRoot, new Vector3(0f, -0.024f, -58f), new Vector3(220f, 0.04f, 70f), Vector3.zero, wetRoadMat);
        CreateStaticBox("Boulevard_East", expRoot, new Vector3(58f, -0.024f, 0f), new Vector3(70f, 0.04f, 46f), Vector3.zero, wetRoadMat);
        CreateStaticBox("Boulevard_West", expRoot, new Vector3(-58f, -0.024f, 0f), new Vector3(70f, 0.04f, 46f), Vector3.zero, wetRoadMat);

        // Outer ring median lane lines & crosswalks
        for (int z = -84; z <= 84; z += 8)
        {
            if (Mathf.Abs(z) < 25) continue;
            CreateStaticBox($"Line_NS_W_{z}", expRoot, new Vector3(-30f, 0.004f, z), new Vector3(0.24f, 0.01f, 3.6f), Vector3.zero, stripeWhiteMat);
            CreateStaticBox($"Line_NS_E_{z}", expRoot, new Vector3(30f, 0.004f, z), new Vector3(0.24f, 0.01f, 3.6f), Vector3.zero, stripeWhiteMat);
        }
        for (int x = -84; x <= 84; x += 8)
        {
            if (Mathf.Abs(x) < 25) continue;
            CreateStaticBox($"Line_EW_N_{x}", expRoot, new Vector3(x, 0.004f, 30f), new Vector3(3.6f, 0.01f, 0.24f), Vector3.zero, stripeGoldMat);
            CreateStaticBox($"Line_EW_S_{x}", expRoot, new Vector3(x, 0.004f, -30f), new Vector3(3.6f, 0.01f, 0.24f), Vector3.zero, stripeGoldMat);
        }

        // Crosswalks outside existing arena entrances
        for (int k = -3; k <= 3; k++)
        {
            CreateStaticBox($"Zebra_N_{k}", expRoot, new Vector3(k * 1.4f, 0.006f, 24.5f), new Vector3(0.75f, 0.01f, 3.4f), Vector3.zero, stripeWhiteMat);
            CreateStaticBox($"Zebra_S_{k}", expRoot, new Vector3(k * 1.4f, 0.006f, -24.5f), new Vector3(0.75f, 0.01f, 3.4f), Vector3.zero, stripeWhiteMat);
        }

        // 3B. Glossy Monsoon Rain Puddles (no colliders so steps never snag)
        Vector3[] puddlePos =
        {
            new Vector3(-5.2f, 0.009f, 7.5f), new Vector3(6.8f, 0.009f, -8.4f), new Vector3(-11f, 0.009f, -12f),
            new Vector3(12f, 0.009f, 9.5f), new Vector3(0f, 0.009f, 27.5f), new Vector3(-26.5f, 0.009f, 4f),
            new Vector3(26.5f, 0.009f, -6f), new Vector3(8.5f, 0.009f, -28f), new Vector3(-15f, 0.009f, 29f),
            new Vector3(21f, 0.009f, 25f), new Vector3(-25f, 0.009f, -26f), new Vector3(34f, 0.009f, 15f)
        };
        for (int i = 0; i < puddlePos.Length; i++)
        {
            var puddle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            puddle.name = $"MonsoonPuddle_{i + 1:00}";
            puddle.transform.SetParent(expRoot, false);
            puddle.transform.position = puddlePos[i];
            float sx = 2.6f + (i % 3) * 1.1f;
            float sz = 1.9f + ((i + 1) % 3) * 0.9f;
            puddle.transform.localScale = new Vector3(sx, 0.004f, sz);
            puddle.transform.rotation = Quaternion.Euler(0f, i * 28f, 0f);
            Object.DestroyImmediate(puddle.GetComponent<Collider>());
            puddle.GetComponent<MeshRenderer>().sharedMaterial = puddleMat;
        }

        // 3C. Accessible Dhaka Flyover Overpass & Ramp along East Avenue (x = 40..46m)
        var flyRoot = new GameObject("Dhaka_FlyoverSpan").transform;
        flyRoot.SetParent(expRoot, false);
        CreateStaticBox("FlyoverDeck_Main", flyRoot, new Vector3(44f, 4.45f, 14f), new Vector3(7.4f, 0.65f, 78f), Vector3.zero, flyoverMat);
        CreateStaticBox("FlyoverParapet_L", flyRoot, new Vector3(40.5f, 5.15f, 14f), new Vector3(0.35f, 0.85f, 78f), Vector3.zero, stripeGoldMat);
        CreateStaticBox("FlyoverParapet_R", flyRoot, new Vector3(47.5f, 5.15f, 14f), new Vector3(0.35f, 0.85f, 78f), Vector3.zero, stripeGoldMat);
        // Walkable Flyover On-Ramp rising cleanly from ground at z=-51 to deck z=-25 (y=0 -> 4.78)
        float rampLen = 27f;
        float rampRise = 4.75f;
        float rampHyp = Mathf.Sqrt(rampLen * rampLen + rampRise * rampRise);
        float rampDeg = Mathf.Atan2(rampRise, rampLen) * Mathf.Rad2Deg;
        CreateStaticBox("FlyoverRamp_South", flyRoot, new Vector3(44f, rampRise * 0.5f - 0.15f, -38.5f),
            new Vector3(7.4f, 0.45f, rampHyp), new Vector3(-rampDeg, 0f, 0f), flyoverMat);
        for (int pz = -20; pz <= 48; pz += 17)
        {
            CreateStaticBox($"FlyoverPier_{pz}", flyRoot, new Vector3(44f, 2.2f, pz), new Vector3(2.0f, 4.3f, 2.2f), Vector3.zero, flyoverMat);
        }

        // 3D. Authentic Dhaka District Buildings & Raintree Foliage
        var cityRoot = new GameObject("DhakaCityBlocks").transform;
        cityRoot.SetParent(expRoot, false);

        Material[] wallMats = { bldgTerracotta, bldgCream, bldgSlate, cngGreenMat };
        (Vector3 pos, Vector3 size, int matIdx)[] blocks =
        {
            // North Motijheel / Kakrail blocks
            (new Vector3(-42f, 9f, 54f), new Vector3(14f, 18f, 12f), 2),
            (new Vector3(-20f, 7.5f, 56f), new Vector3(16f, 15f, 11f), 1),
            (new Vector3(4f, 11f, 58f), new Vector3(18f, 22f, 13f), 0),
            (new Vector3(26f, 8.5f, 55f), new Vector3(13f, 17f, 12f), 2),
            // South Old Dhaka shophouse & residential blocks
            (new Vector3(-38f, 6.5f, -54f), new Vector3(15f, 13f, 11f), 0),
            (new Vector3(-16f, 5.5f, -52f), new Vector3(14f, 11f, 10f), 1),
            (new Vector3(8f, 7f, -55f), new Vector3(16f, 14f, 12f), 0),
            (new Vector3(28f, 6f, -52f), new Vector3(12f, 12f, 10f), 1),
            // West Dhaka avenue apartments & corner tea shop
            (new Vector3(-56f, 8f, -18f), new Vector3(13f, 16f, 14f), 0),
            (new Vector3(-55f, 6.5f, 2f), new Vector3(12f, 13f, 13f), 1),
            (new Vector3(-57f, 9.5f, 24f), new Vector3(14f, 19f, 14f), 2),
            // East block beyond flyover
            (new Vector3(62f, 8.5f, -12f), new Vector3(13f, 17f, 16f), 1),
            (new Vector3(63f, 10f, 18f), new Vector3(14f, 20f, 15f), 2)
        };
        for (int i = 0; i < blocks.Length; i++)
        {
            var b = blocks[i];
            BuildDhakaBuilding($"DhakaBlock_{i + 1:00}", cityRoot, b.pos, b.size, wallMats[b.matIdx % wallMats.Length], bldgWindowMat, bldgCream, cngGreenMat);
        }

        // Roadside Raintrees & Sodium Bulbs
        var bulbRenderers = new List<Renderer>();
        foreach (var mr in Object.FindObjectsByType<MeshRenderer>())
        {
            if (mr.gameObject.name.Contains("SodiumBulb")) bulbRenderers.Add(mr);
        }

        Vector3[] treePositions =
        {
            new Vector3(-25f, 0f, -18f), new Vector3(-25f, 0f, 18f),
            new Vector3(25f, 0f, -18f), new Vector3(25f, 0f, 18f),
            new Vector3(-12f, 0f, 40f), new Vector3(12f, 0f, 40f),
            new Vector3(-12f, 0f, -40f), new Vector3(12f, 0f, -40f)
        };
        for (int i = 0; i < treePositions.Length; i++)
        {
            BuildDhakaRaintree($"DhakaTree_{i + 1:00}", cityRoot, treePositions[i], treeTrunkMat, treeFoliageMat);
            if (i % 2 == 0)
            {
                var bulb = BuildStreetLamp($"DhakaLamp_{i + 1:00}", cityRoot, treePositions[i] + new Vector3(2.2f, 0f, 0f), flyoverMat, sodiumBulbMat);
                if (bulb != null) bulbRenderers.Add(bulb);
            }
        }

        // 3E. Ambient Dhaka Traffic & Pedestrians (outside core pickups/patrol lanes)
        var oldTraffic = GameObject.Find("DhakaTrafficAndPedestrians");
        if (oldTraffic != null) Object.DestroyImmediate(oldTraffic);
        var trafficGo = new GameObject("DhakaTrafficAndPedestrians");
        trafficGo.transform.SetParent(worldRoot.transform, false);
        var trafficMgr = trafficGo.AddComponent<DhakaTrafficAndPedestrians>();

        // Outer Boulevard Counter-Clockwise Loop (+32m / -32m ring)
        Vector3[] outerRingCCW =
        {
            new Vector3(-31f, 0f, -31f), new Vector3(31f, 0f, -31f),
            new Vector3(31f, 0f, 31f), new Vector3(-31f, 0f, 31f)
        };
        Vector3[] outerRingCW =
        {
            new Vector3(36f, 0f, 36f), new Vector3(36f, 0f, -36f),
            new Vector3(-36f, 0f, -36f), new Vector3(-36f, 0f, 36f)
        };

        // Vehicle 1: Red-Green Dhaka City Bus
        BuildBus("Traffic_DhakaCityBus", trafficGo.transform, outerRingCW[0], busRedMat, cngGreenMat, wheelMat, bldgWindowMat, headlightMat, out var busWheels);
        trafficMgr.RegisterVehicle(GameObject.Find("Traffic_DhakaCityBus").transform, outerRingCW, 7.2f, busWheels);

        // Vehicle 2 & 3: Classic Dhaka Green CNG Auto-Rickshaws
        Vector3[] cngLoop2 = { outerRingCCW[2], outerRingCCW[3], outerRingCCW[0], outerRingCCW[1] };
        var cng1 = BuildCNG("Traffic_CNG_01", trafficGo.transform, outerRingCCW[0], cngGreenMat, cngCanopyMat, wheelMat, headlightMat, out var cng1W);
        trafficMgr.RegisterVehicle(cng1.transform, outerRingCCW, 6.2f, cng1W);
        var cng2 = BuildCNG("Traffic_CNG_02", trafficGo.transform, cngLoop2[0], cngGreenMat, cngCanopyMat, wheelMat, headlightMat, out var cng2W);
        trafficMgr.RegisterVehicle(cng2.transform, cngLoop2, 5.8f, cng2W);

        // Vehicle 4: Commuter Motorcycle
        Vector3[] motoLoop = { new Vector3(-26f, 0f, 27f), new Vector3(26f, 0f, 27f), new Vector3(26f, 0f, -27f), new Vector3(-26f, 0f, -27f) };
        var moto = BuildMotorcycle("Traffic_Motorcycle_01", trafficGo.transform, motoLoop[0], motoMat, busRedMat, wheelMat, headlightMat, out var motoW);
        trafficMgr.RegisterVehicle(moto.transform, motoLoop, 8.2f, motoW);

        // Vehicle 5: Dhaka Silver Sedan
        Vector3[] sedanLoop = { outerRingCW[2], outerRingCW[3], outerRingCW[0], outerRingCW[1] };
        var sedan = BuildSedan("Traffic_Sedan_01", trafficGo.transform, sedanLoop[0], sedanMat, bldgWindowMat, wheelMat, headlightMat, out var sedanW);
        trafficMgr.RegisterVehicle(sedan.transform, sedanLoop, 7.8f, sedanW);

        // Sidewalk Pedestrians (articulated walking & idle commuters around Dhaka outer plaza)
        Material pedSkin = CreateLitMat("M_Ped_Skin", EnvDir + "/M_Ped_Skin.mat", new Color(0.58f, 0.40f, 0.30f), 0.25f, 0f);
        Material pedPants = CreateLitMat("M_Ped_Pants", EnvDir + "/M_Ped_Pants.mat", new Color(0.14f, 0.16f, 0.20f), 0.22f, 0f);
        Material[] pedShirts = { cngGreenMat, bldgCream, bldgTerracotta, bldgSlate };

        Vector3[][] pedPaths =
        {
            new[] { new Vector3(-23.5f, 0f, -15f), new Vector3(-23.5f, 0f, 15f) },
            new[] { new Vector3(23.5f, 0f, 16f), new Vector3(23.5f, 0f, -16f) },
            new[] { new Vector3(-16f, 0f, 23.5f), new Vector3(16f, 0f, 23.5f) },
            new[] { new Vector3(15f, 0f, -23.5f), new Vector3(-15f, 0f, -23.5f) },
            new[] { new Vector3(-39f, 0f, -8f), new Vector3(-39f, 0f, 14f) },
            new[] { new Vector3(37.5f, 0f, -14f), new Vector3(37.5f, 0f, 12f) }
        };
        for (int i = 0; i < pedPaths.Length; i++)
        {
            CreatePedestrian($"DhakaPedestrian_{i + 1:00}", trafficGo.transform, pedPaths[i][0], pedSkin, pedShirts[i % pedShirts.Length], pedPants,
                out var lLeg, out var rLeg, out var lArm, out var rArm);
            var pedRoot = trafficGo.transform.Find($"DhakaPedestrian_{i + 1:00}");
            trafficMgr.RegisterPedestrian(pedRoot, lLeg, rLeg, lArm, rArm, pedPaths[i], 1.45f + (i % 3) * 0.2f, false);
        }

        Vector3[] idleSpotPos = { new Vector3(23.2f, 0f, -4.5f), new Vector3(23.2f, 0f, -3.2f), new Vector3(-23.2f, 0f, 5.2f) };
        for (int i = 0; i < idleSpotPos.Length; i++)
        {
            CreatePedestrian($"DhakaCommuter_Idle_{i + 1:00}", trafficGo.transform, idleSpotPos[i], pedSkin, pedShirts[(i + 1) % pedShirts.Length], pedPants,
                out var lLeg, out var rLeg, out var lArm, out var rArm);
            var pedRoot = trafficGo.transform.Find($"DhakaCommuter_Idle_{i + 1:00}");
            pedRoot.rotation = Quaternion.Euler(0f, i == 0 ? 190f : 35f, 0f);
            trafficMgr.RegisterPedestrian(pedRoot, lLeg, rLeg, lArm, rArm, null, 0f, true);
        }

        // 4. Monsoon Rain + Day/Night Atmosphere Controller under GameSystems
        var systemsGo = GameObject.Find("GameSystems") ?? new GameObject("GameSystems");
        var oldAtm = systemsGo.transform.Find("DhakaAtmosphere");
        if (oldAtm != null) Object.DestroyImmediate(oldAtm.gameObject);

        var atmGo = new GameObject("DhakaAtmosphere");
        atmGo.transform.SetParent(systemsGo.transform, false);
        var atm = atmGo.AddComponent<DhakaAtmosphereController>();
        Light sunLight = null;
        foreach (var l in Object.FindObjectsByType<Light>())
        {
            if (l.type == LightType.Directional) { sunLight = l; break; }
        }
        atm.BindReferences(sunLight, null, wetRoadMat, puddleMat, bulbRenderers.ToArray());

        EditorSceneManager.SaveScene(scene, SampleScenePath);

        // Ensure SampleScene.unity is scene 0 and AnimationLab.unity is scene 1 in Build Settings
        var buildScenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(SampleScenePath, true)
        };
        if (File.Exists(AnimationLabPath))
        {
            buildScenes.Add(new EditorBuildSettingsScene(AnimationLabPath, true));
        }
        EditorBuildSettings.scenes = buildScenes.ToArray();

        return $"Upgraded {SampleScenePath} cleanly: Protagonist={playerGo.name}, Flyover+Blocks+12 Puddles, 5 Traffic Vehicles, 9 Pedestrians, Monsoon Rain + M/N Atmosphere Controller";
    }

    static void CreateStaticBox(string name, Transform parent, Vector3 pos, Vector3 scale, Vector3 euler, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static void BuildDhakaBuilding(string name, Transform parent, Vector3 pos, Vector3 size, Material wallMat, Material winMat, Material trimMat, Material awningMat)
    {
        var root = new GameObject(name).transform;
        root.SetParent(parent, false);
        root.position = new Vector3(pos.x, 0f, pos.z);

        CreateStaticBox("TowerCore", root, new Vector3(0f, size.y * 0.5f, 0f), size, Vector3.zero, wallMat);
        CreateStaticBox("RoofParapet", root, new Vector3(0f, size.y + 0.45f, 0f), new Vector3(size.x + 0.4f, 0.9f, size.z + 0.4f), Vector3.zero, trimMat);
        CreateStaticBox("WaterTank", root, new Vector3(size.x * 0.25f, size.y + 1.55f, -size.z * 0.2f), new Vector3(2.2f, 1.4f, 2.2f), Vector3.zero, wallMat);

        // Multi-floor window strip facing plaza center + ground floor shopfront awning
        int floors = Mathf.Max(2, Mathf.FloorToInt(size.y / 3.4f));
        for (int f = 1; f < floors; f++)
        {
            float fy = 2.6f + f * 3.1f;
            if (fy > size.y - 1.2f) break;
            CreateStaticBox($"WinBand_{f}", root, new Vector3(0f, fy, 0f), new Vector3(size.x + 0.08f, 1.15f, size.z * 0.72f), Vector3.zero, winMat);
            CreateStaticBox($"WinBandZ_{f}", root, new Vector3(0f, fy, 0f), new Vector3(size.x * 0.72f, 1.15f, size.z + 0.08f), Vector3.zero, winMat);
        }
        CreateStaticBox("ShopAwning", root, new Vector3(0f, 3.0f, 0f), new Vector3(size.x + 1.3f, 0.35f, size.z + 1.3f), Vector3.zero, awningMat);
    }

    static void BuildDhakaRaintree(string name, Transform parent, Vector3 pos, Material trunkMat, Material foliageMat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;

        var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.transform.SetParent(go.transform, false);
        trunk.transform.localPosition = new Vector3(0f, 2.2f, 0f);
        trunk.transform.localScale = new Vector3(0.55f, 2.2f, 0.55f);
        trunk.GetComponent<MeshRenderer>().sharedMaterial = trunkMat;

        var canopy1 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        canopy1.transform.SetParent(go.transform, false);
        canopy1.transform.localPosition = new Vector3(0f, 5.2f, 0f);
        canopy1.transform.localScale = new Vector3(5.6f, 2.8f, 5.4f);
        Object.DestroyImmediate(canopy1.GetComponent<Collider>());
        canopy1.GetComponent<MeshRenderer>().sharedMaterial = foliageMat;
    }

    static Renderer BuildStreetLamp(string name, Transform parent, Vector3 pos, Material poleMat, Material bulbMat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        CreateStaticBox("Pole", go.transform, new Vector3(0f, 2.8f, 0f), new Vector3(0.16f, 5.6f, 0.16f), Vector3.zero, poleMat);
        CreateStaticBox("Arm", go.transform, new Vector3(0f, 5.5f, 0.55f), new Vector3(0.14f, 0.14f, 1.25f), Vector3.zero, poleMat);
        var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bulb.name = "SodiumBulb";
        bulb.transform.SetParent(go.transform, false);
        bulb.transform.localPosition = new Vector3(0f, 5.38f, 1.05f);
        bulb.transform.localScale = new Vector3(0.32f, 0.18f, 0.42f);
        Object.DestroyImmediate(bulb.GetComponent<Collider>());
        var mr = bulb.GetComponent<MeshRenderer>();
        mr.sharedMaterial = bulbMat;
        return mr;
    }

    static void BuildBus(string name, Transform parent, Vector3 pos, Material bodyMat, Material stripeMat, Material wheelMat, Material glassMat, Material lightMat, out Transform[] wheels)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = pos;
        CreateStaticBox("Chassis", root.transform, new Vector3(0f, 1.55f, 0f), new Vector3(2.45f, 2.35f, 8.2f), Vector3.zero, bodyMat);
        CreateStaticBox("GreenStripe", root.transform, new Vector3(0f, 1.15f, 0f), new Vector3(2.50f, 0.55f, 8.24f), Vector3.zero, stripeMat);
        CreateStaticBox("WindowBand", root.transform, new Vector3(0f, 2.05f, 0.15f), new Vector3(2.48f, 0.85f, 7.65f), Vector3.zero, glassMat);
        CreateStaticBox("Headlights", root.transform, new Vector3(0f, 0.82f, 4.12f), new Vector3(1.9f, 0.22f, 0.08f), Vector3.zero, lightMat);
        wheels = AddFourWheels(root.transform, 1.15f, 2.55f, 0.44f, wheelMat);
    }

    static GameObject BuildCNG(string name, Transform parent, Vector3 pos, Material greenMat, Material canopyMat, Material wheelMat, Material lightMat, out Transform[] wheels)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = pos;
        CreateStaticBox("LowerHull", root.transform, new Vector3(0f, 0.58f, 0f), new Vector3(1.25f, 0.65f, 2.35f), Vector3.zero, greenMat);
        CreateStaticBox("Canopy", root.transform, new Vector3(0f, 1.28f, -0.15f), new Vector3(1.22f, 0.78f, 1.85f), Vector3.zero, canopyMat);
        CreateStaticBox("FrontLamp", root.transform, new Vector3(0f, 0.68f, 1.19f), new Vector3(0.28f, 0.22f, 0.08f), Vector3.zero, lightMat);
        wheels = new[]
        {
            MakeWheel(root.transform, new Vector3(0f, 0.26f, 0.92f), 0.26f, wheelMat),
            MakeWheel(root.transform, new Vector3(-0.56f, 0.26f, -0.78f), 0.26f, wheelMat),
            MakeWheel(root.transform, new Vector3(0.56f, 0.26f, -0.78f), 0.26f, wheelMat)
        };
        return root;
    }

    static GameObject BuildMotorcycle(string name, Transform parent, Vector3 pos, Material frameMat, Material tankMat, Material wheelMat, Material lightMat, out Transform[] wheels)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = pos;
        CreateStaticBox("Frame", root.transform, new Vector3(0f, 0.58f, 0f), new Vector3(0.32f, 0.42f, 1.55f), Vector3.zero, frameMat);
        CreateStaticBox("FuelTank", root.transform, new Vector3(0f, 0.84f, 0.24f), new Vector3(0.36f, 0.24f, 0.55f), Vector3.zero, tankMat);
        CreateStaticBox("Headlamp", root.transform, new Vector3(0f, 0.88f, 0.78f), new Vector3(0.18f, 0.18f, 0.12f), Vector3.zero, lightMat);
        wheels = new[]
        {
            MakeWheel(root.transform, new Vector3(0f, 0.30f, 0.65f), 0.30f, wheelMat),
            MakeWheel(root.transform, new Vector3(0f, 0.30f, -0.65f), 0.30f, wheelMat)
        };
        return root;
    }

    static GameObject BuildSedan(string name, Transform parent, Vector3 pos, Material bodyMat, Material glassMat, Material wheelMat, Material lightMat, out Transform[] wheels)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = pos;
        CreateStaticBox("Body", root.transform, new Vector3(0f, 0.62f, 0f), new Vector3(1.85f, 0.68f, 4.25f), Vector3.zero, bodyMat);
        CreateStaticBox("Cabin", root.transform, new Vector3(0f, 1.15f, -0.15f), new Vector3(1.62f, 0.56f, 2.25f), Vector3.zero, glassMat);
        CreateStaticBox("Headlights", root.transform, new Vector3(0f, 0.65f, 2.13f), new Vector3(1.52f, 0.16f, 0.06f), Vector3.zero, lightMat);
        wheels = AddFourWheels(root.transform, 0.90f, 1.32f, 0.34f, wheelMat);
        return root;
    }

    static Transform[] AddFourWheels(Transform parent, float halfW, float halfL, float radius, Material mat)
    {
        return new[]
        {
            MakeWheel(parent, new Vector3(-halfW, radius, halfL), radius, mat),
            MakeWheel(parent, new Vector3(halfW, radius, halfL), radius, mat),
            MakeWheel(parent, new Vector3(-halfW, radius, -halfL), radius, mat),
            MakeWheel(parent, new Vector3(halfW, radius, -halfL), radius, mat)
        };
    }

    static Transform MakeWheel(Transform parent, Vector3 pos, float rad, Material mat)
    {
        var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cyl.name = "Wheel";
        cyl.transform.SetParent(parent, false);
        cyl.transform.localPosition = pos;
        cyl.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        cyl.transform.localScale = new Vector3(rad * 2f, 0.14f, rad * 2f);
        Object.DestroyImmediate(cyl.GetComponent<Collider>());
        cyl.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return cyl.transform;
    }

    static void CreatePedestrian(string name, Transform parent, Vector3 pos, Material skinMat, Material shirtMat, Material pantsMat,
        out Transform lLeg, out Transform rLeg, out Transform lArm, out Transform rArm)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = pos;

        var cap = root.AddComponent<CapsuleCollider>();
        cap.center = new Vector3(0f, 0.88f, 0f);
        cap.radius = 0.24f;
        cap.height = 1.74f;

        CreateStaticBox("Torso", root.transform, new Vector3(0f, 1.15f, 0f), new Vector3(0.38f, 0.56f, 0.22f), Vector3.zero, shirtMat);
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(root.transform, false);
        head.transform.localPosition = new Vector3(0f, 1.58f, 0f);
        head.transform.localScale = new Vector3(0.22f, 0.25f, 0.23f);
        Object.DestroyImmediate(head.GetComponent<Collider>());
        head.GetComponent<MeshRenderer>().sharedMaterial = skinMat;

        lLeg = MakeLimbPivot("LeftLeg", root.transform, new Vector3(-0.10f, 0.84f, 0f), new Vector3(0.14f, 0.80f, 0.15f), -0.40f, pantsMat);
        rLeg = MakeLimbPivot("RightLeg", root.transform, new Vector3(0.10f, 0.84f, 0f), new Vector3(0.14f, 0.80f, 0.15f), -0.40f, pantsMat);
        lArm = MakeLimbPivot("LeftArm", root.transform, new Vector3(-0.24f, 1.38f, 0f), new Vector3(0.11f, 0.56f, 0.11f), -0.26f, skinMat);
        rArm = MakeLimbPivot("RightArm", root.transform, new Vector3(0.24f, 1.38f, 0f), new Vector3(0.11f, 0.56f, 0.11f), -0.26f, skinMat);
    }

    static Transform MakeLimbPivot(string name, Transform parent, Vector3 pivotLocal, Vector3 size, float childY, Material mat)
    {
        var pivot = new GameObject(name).transform;
        pivot.SetParent(parent, false);
        pivot.localPosition = pivotLocal;
        var geom = GameObject.CreatePrimitive(PrimitiveType.Cube);
        geom.transform.SetParent(pivot, false);
        geom.transform.localPosition = new Vector3(0f, childY, 0f);
        geom.transform.localScale = size;
        Object.DestroyImmediate(geom.GetComponent<Collider>());
        geom.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return pivot;
    }

    static Material CreateWetAsphaltMaterial()
    {
        string texPath = EnvDir + "/T_DhakaWetAsphalt.png";
        string matPath = EnvDir + "/M_DhakaWetAsphalt.mat";
        int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, false);
        var rng = new System.Random(101);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float c = 0.065f + (float)rng.NextDouble() * 0.035f;
                tex.SetPixel(x, y, new Color(c * 0.94f, c * 0.98f, c * 1.08f, 1f));
            }
        }
        tex.Apply();
        File.WriteAllBytes(texPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(texPath);
        var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        loaded.wrapMode = TextureWrapMode.Repeat;

        var mat = CreateLitMat("M_DhakaWetAsphalt", matPath, Color.white, 0.84f, 0.06f);
        mat.SetTexture("_BaseMap", loaded);
        mat.SetTextureScale("_BaseMap", new Vector2(18f, 18f));
        return mat;
    }

    static Material CreatePuddleMaterial()
    {
        string matPath = EnvDir + "/M_DhakaPuddle.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        var mat = existing != null ? existing : new Material(lit);
        mat.name = "M_DhakaPuddle";
        mat.shader = lit;
        mat.SetFloat("_Surface", 1f); // Transparent
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;
        mat.SetColor("_BaseColor", new Color(0.08f, 0.14f, 0.22f, 0.42f));
        mat.SetFloat("_Smoothness", 0.98f);
        mat.SetFloat("_Metallic", 0.30f);
        if (existing == null) AssetDatabase.CreateAsset(mat, matPath);
        else EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material CreateLitMat(string name, string path, Color color, float smoothness, float metallic)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        var mat = existing != null ? existing : new Material(lit);
        mat.name = name;
        mat.shader = lit;
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", metallic);
        if (existing == null) AssetDatabase.CreateAsset(mat, path);
        else EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material CreateEmissiveMat(string name, string path, Color baseColor, Color emission, float smoothness)
    {
        var mat = CreateLitMat(name, path, baseColor, smoothness, 0.05f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", emission);
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
