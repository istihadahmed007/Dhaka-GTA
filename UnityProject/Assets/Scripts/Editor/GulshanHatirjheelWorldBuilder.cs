using System.IO;
using DhakaGta.Character;
using DhakaGta.Combat;
using DhakaGta.Core;
using DhakaGta.Data;
using DhakaGta.Environment;
using DhakaGta.GIS;
using DhakaGta.Graphics;
using DhakaGta.UI;
using DhakaGta.Vehicles;
using DhakaGta.WorldGen;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DhakaGta.Editor
{
    /// <summary>
    /// Builds First Milestone scene: Assets/Scenes/GulshanHatirjheel_Milestone.unity
    /// with connected OSM roads, intersections, roundabouts, bridges/flyovers, sidewalks/curbs,
    /// markings, traffic signals, Hatirjheel/Gulshan lakes, OSM buildings, streaming chunks, and a driveable WheelCollider car.
    /// </summary>
    public static class GulshanHatirjheelWorldBuilder
    {
        public const string GeoJsonRelPath = "../GeographicData/dhaka_gulshan_hatirjheel.geojson";
        public const string MilestoneDbPath = "Assets/ScriptableObjects/GIS/DhakaGulshanHatirjheelDatabase.asset";
        public const string MilestoneScenePath = "Assets/Scenes/GulshanHatirjheel_Milestone.unity";
        private const string WorldGenMatFolder = "Assets/Materials/WorldGen";

        [MenuItem("Dhaka-GTA/WorldGen/Build Gulshan-Hatirjheel Driveable Milestone Scene")]
        public static void BuildMilestoneScene()
        {
            PhysicsMatrixValidator.ApplyAndValidate();

            string geoPath = Path.GetFullPath(Path.Combine(Application.dataPath, GeoJsonRelPath));
            if (!File.Exists(geoPath))
            {
                Debug.LogError("[GulshanHatirjheelWorldBuilder] Missing GeoJSON: " + geoPath);
                return;
            }

            EnsureFolders();
            GeoFeatureDatabase db = AssetDatabase.LoadAssetAtPath<GeoFeatureDatabase>(MilestoneDbPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<GeoFeatureDatabase>();
                AssetDatabase.CreateAsset(db, MilestoneDbPath);
            }

            if (!DhakaGeoImporter.ImportGeoJsonFileIntoDatabase(geoPath, db))
            {
                return;
            }

            MaterialBundle mats = EnsureMaterials();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // HDRP Atmosphere & Golden-Hour Dhaka Sun
            var envGo = new GameObject("HDRP_DhakaAtmosphere");
            var sunGo = new GameObject("SunLight");
            sunGo.transform.SetParent(envGo.transform, false);
            sunGo.transform.rotation = Quaternion.Euler(28f, 232f, 0f);
            var sunLight = sunGo.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.useColorTemperature = true;
            sunLight.colorTemperature = 5200f;
            sunLight.intensity = 82000f;
            sunLight.shadows = LightShadows.Soft;
            sunGo.AddComponent<HDAdditionalLightData>();

            var volGo = new GameObject("SkyAndFogVolume");
            volGo.transform.SetParent(envGo.transform, false);
            var volume = volGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/SkyandFogSettingsProfile.asset");

            // Base Datum Terrain Plane at real UTM relative offset covering Gulshan-Hatirjheel
            Vector3 regionAnchorLocal = ComputeLocalOffsetFor(23.7745, 90.4138);
            var terrainBase = GameObject.CreatePrimitive(PrimitiveType.Cube);
            terrainBase.name = "DhakaBaseTerrain_GulshanHatirjheel";
            terrainBase.layer = PhysicsLayers.Ground;
            terrainBase.transform.position = new Vector3(regionAnchorLocal.x, -0.55f, regionAnchorLocal.z);
            terrainBase.transform.localScale = new Vector3(6500f, 1f, 6500f);
            terrainBase.GetComponent<MeshRenderer>().sharedMaterial = mats.TerrainEarth;

            // Core GIS & Streaming Controllers
            var systemsGo = new GameObject("DhakaWorldGenSystems");
            var coordManager = systemsGo.AddComponent<WorldCoordinateManager>();
            coordManager.Configure(db, GeographicCoordinateConverter.ShaplaOriginLat, GeographicCoordinateConverter.ShaplaOriginLon, 0.0, null);

            var chunkManager = systemsGo.AddComponent<WorldChunkManager>();
            var regionLoader = systemsGo.AddComponent<WorldRegionLoader>();
            var streamingCtrl = systemsGo.AddComponent<WorldStreamingController>();

            var splineSystem = systemsGo.AddComponent<RoadSplineSystem>();
            var intersectionGen = systemsGo.AddComponent<IntersectionGenerator>();
            var roadGen = systemsGo.AddComponent<RoadGenerator>();
            var buildingSys = systemsGo.AddComponent<BuildingPlacementSystem>();

            // Generated World Hierarchy Root
            var worldRoot = new GameObject("GulshanHatirjheel_1To1World");
            coordManager.RegisterFloatingRoot(worldRoot.transform);

            splineSystem.BuildFromDatabase(db, coordManager, maxRadiusMeters: 0.0);
            intersectionGen.Generate(splineSystem, db, coordManager, worldRoot.transform, mats.IntersectionMats);
            RoadGenerator.RoadGenStats roadStats = roadGen.BuildNetworkAndWater(splineSystem, db, coordManager, worldRoot.transform, mats.RoadMats);
            BuildingPlacementSystem.BuildingGenStats bldgStats = buildingSys.PlaceBuildings(
                db, coordManager, splineSystem, worldRoot.transform, mats.BuildingMats, maxDistanceFromOriginMeters: 0.0, chunkManager);

            // Part 4 AAA HDRP Environment Art: PBR Materials/Palettes, Hero Towers, Tea Stalls, Poles+Wires, GPU Trees
            DhakaHeroArchitectureBuilder.MaterialPack heroPack = EnvironmentPaletteImporter.SyncMaterialsPalettesAndPrefabs();
            var gpuBatcher = systemsGo.AddComponent<GpuInstancedPropBatcher>();
            var envRoot = new GameObject("GulshanHatirjheel_EnvironmentArt");
            envRoot.transform.SetParent(worldRoot.transform, false);

            // Part 6 All-11 Vehicle Specs & Prefabs + Full Dhaka Test Fleet along Gulshan-Hatirjheel Roads & Lake
            VehicleSpecSO[] allElevenSpecs = DhakaVehicleCatalogBuilder.BuildAllSpecsAndPrefabs();
            VehicleSpecSO sedanSpec = FindVehicleSpec(allElevenSpecs, "sedan") ?? AssetDatabase.LoadAssetAtPath<VehicleSpecSO>("Assets/ScriptableObjects/Vehicles/VehicleSpec_Sedan.asset");

            RoadSplineSegment spawnRoad = FindBestDriveSpawnRoad(splineSystem, regionAnchorLocal, out Vector3 spawnPos, out Vector3 spawnForward);
            GameObject carObj = DhakaVehicleFactory.CreateVehicleFromSpec(sedanSpec, spawnPos, Quaternion.LookRotation(spawnForward, Vector3.up), worldRoot.transform);
            MonoBehaviour car = carObj != null ? carObj.GetComponent<DhakaVehicleController>() : null;

            // Spawn companion showcase vehicles spaced cleanly along nearby road points + Boat on Hatirjheel Lake
            var fleetRoot = new GameObject("Dhaka_TestFleet_11Classes");
            fleetRoot.transform.SetParent(worldRoot.transform, false);
            SpawnNearbyTestFleet(splineSystem, allElevenSpecs, regionAnchorLocal, fleetRoot.transform, db, coordManager);

            // Garage Ownership & Repair Service Anchor
            var garageMgr = systemsGo.AddComponent<DhakaGarageManager>();
            garageMgr.ConfigureCatalog(allElevenSpecs, fleetRoot.transform);
            garageMgr.GrantVehicleToGarage("sedan", new Color(0.76f, 0.12f, 0.14f));
            garageMgr.GrantVehicleToGarage("cng_auto", new Color(0.06f, 0.44f, 0.20f));
            garageMgr.GrantVehicleToGarage("motorcycle", new Color(0.14f, 0.24f, 0.72f));

            Vector3 footSpawnPos = spawnPos - (car != null ? car.transform.right * 3.0f : Vector3.right * 3.0f);
            GulshanHatirjheelEnvironmentPopulator.PopulateGulshanHatirjheel(
                splineSystem, intersectionGen, gpuBatcher, heroPack, envRoot.transform, regionAnchorLocal, footSpawnPos);

            ProtagonistProfileSO tariq = AssetDatabase.LoadAssetAtPath<ProtagonistProfileSO>("Assets/ScriptableObjects/Protagonists/Protagonist_Tariq.asset");
            ProtagonistProfileSO maya = AssetDatabase.LoadAssetAtPath<ProtagonistProfileSO>("Assets/ScriptableObjects/Protagonists/Protagonist_Maya.asset");
            var playerGo = new GameObject("Player_Tariq_Gulshan");
            playerGo.layer = PhysicsLayers.Player;
            playerGo.tag = "Player";
            playerGo.transform.position = footSpawnPos;
            var cc = playerGo.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.34f; cc.center = new Vector3(0f, 0.9f, 0f);

            var router = systemsGo.AddComponent<InputActionRouter>();
            InputActionAsset inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            router.BindAsset(inputAsset);
            router.SetMode(InputContextMode.OnFoot);

            var protagonistCtrl = playerGo.AddComponent<ProtagonistController>();
            var playerHealth = playerGo.AddComponent<HealthComponent>();
            playerHealth.InitializeFromProfile(tariq);

            // Part 7 AAA Rigged Humanoid Prefabs, AI4Animation Matcher, Foot Locker IK, Abilities & [Tab] Switcher
            ProtagonistSwitcher switcher = DhakaCharacterPipelineBuilder.AttachProtagonistRigsAndSwitcher(playerGo, protagonistCtrl, router, tariq, maya);

            var followProxy = new GameObject("ActiveCameraFollowProxy").transform;
            followProxy.position = footSpawnPos;

            coordManager.Configure(db, GeographicCoordinateConverter.ShaplaOriginLat, GeographicCoordinateConverter.ShaplaOriginLon, 0.0, followProxy);
            streamingCtrl.Configure(coordManager, regionLoader, chunkManager, followProxy);

            // Main Camera + Cinemachine 3 Chase Rig
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.position = spawnPos - spawnForward * 7f + Vector3.up * 3.2f;
            camGo.transform.LookAt(spawnPos + Vector3.up * 1.2f);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.25f;
            cam.farClipPlane = 8000f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<HDAdditionalCameraData>();
            camGo.AddComponent<CinemachineBrain>();

            protagonistCtrl.Configure(tariq, router, cam.transform);
            // Attach interaction scanner on persistent systemsGo so [F] works even when playerGo hides inside car!
            var scanner = systemsGo.AddComponent<ProtagonistInteractionScanner>();
            scanner.Configure(playerGo, followProxy, router);

            var vcamGo = new GameObject("CM_CarFollowCamera");
            var cmCam = vcamGo.AddComponent<CinemachineCamera>();
            cmCam.Target = new CameraTarget { TrackingTarget = followProxy, LookAtTarget = followProxy };

            // Part 5 HDRP Photorealistic Graphics Pipeline: 5-Mode Camera, Dynamic Day/Night, Wet Shaders, Rain VFX, Probe Rig
            var wetDriver = systemsGo.AddComponent<WetSurfacePuddleDriver>();
            wetDriver.RegisterPavementMaterial(mats.RoadMats.AsphaltMaterial);
            wetDriver.RegisterPavementMaterial(mats.RoadMats.SidewalkTileMaterial);
            wetDriver.RegisterPavementMaterial(mats.RoadMats.BridgeDeckMaterial);
            wetDriver.RegisterVehiclePaintMaterial(mats.CarBody);

            var vfxMat = CreateMat("Mat_DhakaRainVfx", new Color(0.85f, 0.90f, 0.96f), 0.1f);
            var tailGlow = CreateMat("Mat_TaillightGlow", new Color(0.95f, 0.10f, 0.08f), 0.85f);
            if (tailGlow.HasProperty("_EmissiveColor")) tailGlow.SetColor("_EmissiveColor", new Color(3.2f, 0.25f, 0.15f));

            var lampGlow = CreateMat("Mat_StreetlampAmberGlow", new Color(1.0f, 0.78f, 0.35f), 0.7f);
            if (lampGlow.HasProperty("_EmissiveColor")) lampGlow.SetColor("_EmissiveColor", new Color(2.4f, 1.4f, 0.4f));

            var probeGo = new GameObject("HDRP_LocalReflectionProbe");
            probeGo.transform.position = spawnPos + Vector3.up * 3.5f;
            var reflProbe = probeGo.AddComponent<ReflectionProbe>();
            var probeRig = probeGo.AddComponent<DhakaReflectionProbeRig>();
            probeRig.Configure(reflProbe, followProxy);

            var vfxCtrl = DhakaWeatherVfxController.BuildRuntimeVfxRig(systemsGo.transform, cam.transform, car, vfxMat);

            var weatherCtrl = systemsGo.AddComponent<DhakaTimeOfDayAndWeatherController>();
            var streetLampsRoot = new GameObject("GulshanStreetlampLights");
            streetLampsRoot.transform.SetParent(envRoot.transform, false);
            DhakaGraphicsShowcaseRig.PlaceStreetlampLightsAlongSpawnCorridor(splineSystem, regionAnchorLocal, weatherCtrl, streetLampsRoot.transform, lampGlow);
            weatherCtrl.Configure(sunLight, volume, wetDriver, vfxCtrl, probeRig, DhakaWeatherCondition.GoldenHourClear);

            var qualityMgr = systemsGo.AddComponent<DhakaGraphicsQualityManager>();
            qualityMgr.Configure(weatherCtrl, vfxCtrl, HdrpQualityTier.Balanced);

            var camRig = systemsGo.AddComponent<DhakaCinemachineCameraRig>();
            camRig.Configure(cmCam, cam, car, playerGo.transform, scanner, DhakaCameraMode.OnFootThirdPerson);

            // Part 8 Living Dhaka Traffic (CNGs, Rickshaws, Buses, Cars, Bikes, DMP Police), Crosswalk/TeaStall NPCs & NavMesh
            DhakaTrafficAiPipelineBuilder.AttachLivingCitySimulation(
                systemsGo,
                splineSystem,
                intersectionGen,
                followProxy,
                allElevenSpecs,
                "gulshan",
                warmImmediatelyInScene: false);

            // Part 9 Open-World Story Campaign, Side Hustles, BDT Economy, Safehouses, Save/Load & Smartphone UI
            DhakaStoryContentBuilder.AttachOpenWorldGameplaySystems(
                systemsGo,
                worldRoot.transform,
                protagonistCtrl,
                switcher,
                scanner,
                playerHealth,
                garageMgr,
                coordManager,
                footSpawnPos);

            // Part 10 Dedicated Node.js Authoritative WebSocket Client, Remote Avatars/Vehicles & [M] Lobby/Chat
            DhakaMultiplayerPipelineBuilder.AttachMultiplayerSystems(
                systemsGo,
                worldRoot.transform,
                protagonistCtrl,
                playerHealth,
                systemsGo.GetComponent<DhakaGta.Missions.PlayerEconomyAndInventory>(),
                systemsGo.GetComponent<DhakaGta.Missions.DhakaMissionEngine>(),
                allElevenSpecs);

            // Part 11 Live CPU/GPU/Memory ProfilerRecorder
            systemsGo.AddComponent<DhakaPerformanceProfiler>();

            // Driving & Environment Interaction HUD + Graphics Telemetry
            CreateDrivingHud(car, scanner, coordManager, streamingCtrl, splineSystem, weatherCtrl, camRig, wetDriver);

            EditorSceneManager.SaveScene(scene, MilestoneScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[GulshanHatirjheelWorldBuilder] Built {MilestoneScenePath}: " +
                $"Roads={roadStats.RoadMeshCount}, Junctions={intersectionGen.Junctions.Count}, " +
                $"Bridges={roadStats.BridgeCount}, Flyovers={roadStats.FlyoverCount}, Underpasses={roadStats.UnderpassCount}, " +
                $"Water={roadStats.WaterMeshesCount}, Buildings={bldgStats.PlacedCount}, " +
                $"SpawnRoad='{spawnRoad?.roadName ?? "Fallback"}'");
        }

        private static VehicleSpecSO FindVehicleSpec(VehicleSpecSO[] specs, string id)
        {
            if (specs == null) return null;
            foreach (VehicleSpecSO s in specs)
            {
                if (s != null && s.vehicleId == id) return s;
            }
            return null;
        }

        private static void SpawnNearbyTestFleet(
            RoadSplineSystem splineSystem, VehicleSpecSO[] specs, Vector3 anchor, Transform fleetRoot, GeoFeatureDatabase db, WorldCoordinateManager coordMgr)
        {
            if (specs == null || splineSystem == null) return;
            int landOffsetIdx = 0;
            foreach (VehicleSpecSO spec in specs)
            {
                if (spec == null || spec.vehicleId == "sedan") continue;
                if (spec.isWatercraft)
                {
                    Vector3 lakePos = new Vector3(anchor.x - 140f, 0.15f, anchor.z - 220f);
                    if (db?.waterBodies != null)
                    {
                        foreach (GeoWaterFeature w in db.waterBodies)
                        {
                            if (w != null && w.pointsUtm != null && w.pointsUtm.Length > 0 &&
                                w.waterKind != "drain")
                            {
                                lakePos = coordMgr.UtmToUnityPosition(w.pointsUtm[0], 0.2f);
                                break;
                            }
                        }
                    }
                    float xBias = spec.vehicleId == "buriganga_launch" ? 28f : 0f;
                    DhakaVehicleFactory.CreateVehicleFromSpec(spec, lakePos + new Vector3(xBias, 0.2f, 0f), Quaternion.identity, fleetRoot);
                    continue;
                }

                // Place land classes cleanly onto adjacent primary/secondary road splines
                landOffsetIdx++;
                int targetSegIdx = Mathf.Min(splineSystem.Segments.Count - 1, landOffsetIdx * 3);
                if (targetSegIdx >= 0 && splineSystem.Segments.Count > 0)
                {
                    RoadSplineSegment seg = splineSystem.Segments[targetSegIdx % splineSystem.Segments.Count];
                    seg.SampleAtDistance(Mathf.Min(18f, seg.totalLengthMeters * 0.5f), out Vector3 p, out Vector3 t, out _);
                    DhakaVehicleFactory.CreateVehicleFromSpec(spec, p + Vector3.up * 0.7f, Quaternion.LookRotation(t, Vector3.up), fleetRoot);
                }
            }
        }

        private static RoadSplineSegment FindBestDriveSpawnRoad(
            RoadSplineSystem splineSystem, Vector3 regionAnchor, out Vector3 spawnPos, out Vector3 spawnForward)
        {
            RoadSplineSegment best = null;
            float bestScore = float.MinValue;

            foreach (RoadSplineSegment seg in splineSystem.Segments)
            {
                if (seg == null || seg.gradeKind != RoadGradeKind.Surface || seg.totalLengthMeters < 60f) continue;
                float distToAnchor = Vector3.Distance(seg.StartPosition, regionAnchor);
                if (distToAnchor > 1500f) continue;

                float priority = (seg.highwayClass == "primary" || seg.highwayClass == "trunk" ? 500f : 150f)
                                 + Mathf.Min(300f, seg.totalLengthMeters)
                                 - distToAnchor * 0.25f;
                if (priority > bestScore)
                {
                    bestScore = priority;
                    best = seg;
                }
            }

            if (best != null)
            {
                float d = Mathf.Clamp(best.totalLengthMeters * 0.3f, 15f, best.totalLengthMeters - 15f);
                best.SampleAtDistance(d, out Vector3 centerPos, out Vector3 tangent, out _);
                spawnPos = centerPos + Vector3.up * 0.85f;
                spawnForward = tangent;
                return best;
            }

            spawnPos = new Vector3(regionAnchor.x, 0.85f, regionAnchor.z);
            spawnForward = Vector3.forward;
            return null;
        }

        private static DhakaWheelVehicleController SpawnDriveableCar(
            Vector3 position, Vector3 forward, VehicleSpecSO spec, MaterialBundle mats)
        {
            var carGo = new GameObject("GulshanGT_DrivableCar");
            carGo.layer = PhysicsLayers.Vehicle;
            carGo.transform.position = position;
            carGo.transform.rotation = Quaternion.LookRotation(forward.sqrMagnitude > 0.01f ? forward : Vector3.forward, Vector3.up);

            var rb = carGo.AddComponent<Rigidbody>();
            rb.mass = spec != null ? spec.massKg : 1420f;

            var chassisCol = carGo.AddComponent<BoxCollider>();
            chassisCol.center = new Vector3(0f, 0.68f, 0f);
            chassisCol.size = new Vector3(1.85f, 0.95f, 4.35f);

            GameObject bodyVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bodyVisual.name = "ChassisBodyVisual";
            Object.DestroyImmediate(bodyVisual.GetComponent<Collider>());
            bodyVisual.layer = PhysicsLayers.Vehicle;
            bodyVisual.transform.SetParent(carGo.transform, false);
            bodyVisual.transform.localPosition = new Vector3(0f, 0.62f, 0f);
            bodyVisual.transform.localScale = new Vector3(1.85f, 0.65f, 4.35f);
            bodyVisual.GetComponent<MeshRenderer>().sharedMaterial = mats.CarBody;

            GameObject cabinVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cabinVisual.name = "ChassisCabinVisual";
            Object.DestroyImmediate(cabinVisual.GetComponent<Collider>());
            cabinVisual.layer = PhysicsLayers.Vehicle;
            cabinVisual.transform.SetParent(carGo.transform, false);
            cabinVisual.transform.localPosition = new Vector3(0f, 1.15f, -0.15f);
            cabinVisual.transform.localScale = new Vector3(1.55f, 0.52f, 2.25f);
            cabinVisual.GetComponent<MeshRenderer>().sharedMaterial = mats.CommercialGlass;

            WheelCollider fl = CreateWheel(carGo.transform, "Wheel_FL", new Vector3(-0.84f, 0.36f, 1.38f), mats.TireRubber, out Transform flMesh);
            WheelCollider fr = CreateWheel(carGo.transform, "Wheel_FR", new Vector3(0.84f, 0.36f, 1.38f), mats.TireRubber, out Transform frMesh);
            WheelCollider rl = CreateWheel(carGo.transform, "Wheel_RL", new Vector3(-0.84f, 0.36f, -1.38f), mats.TireRubber, out Transform rlMesh);
            WheelCollider rr = CreateWheel(carGo.transform, "Wheel_RR", new Vector3(0.84f, 0.36f, -1.38f), mats.TireRubber, out Transform rrMesh);

            var controller = carGo.AddComponent<DhakaWheelVehicleController>();
            controller.ConfigureWheels(spec, fl, fr, rl, rr, flMesh, frMesh, rlMesh, rrMesh);
            return controller;
        }

        private static WheelCollider CreateWheel(
            Transform parent, string name, Vector3 localPos, Material tireMat, out Transform visualTransform)
        {
            var colGo = new GameObject(name);
            colGo.layer = PhysicsLayers.VehicleWheel;
            colGo.transform.SetParent(parent, false);
            colGo.transform.localPosition = localPos;

            var wc = colGo.AddComponent<WheelCollider>();
            wc.radius = 0.36f;
            wc.suspensionDistance = 0.22f;
            wc.mass = 22f;
            JointSpring spring = wc.suspensionSpring;
            spring.spring = 32000f;
            spring.damper = 4200f;
            spring.targetPosition = 0.45f;
            wc.suspensionSpring = spring;

            var vis = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            vis.name = name + "_Visual";
            Object.DestroyImmediate(vis.GetComponent<Collider>());
            vis.layer = PhysicsLayers.VehicleWheel;
            vis.transform.SetParent(colGo.transform, false);
            vis.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            vis.transform.localScale = new Vector3(0.72f, 0.14f, 0.72f);
            vis.GetComponent<MeshRenderer>().sharedMaterial = tireMat;
            visualTransform = vis.transform;
            return wc;
        }

        private static void CreateDrivingHud(
            MonoBehaviour car,
            ProtagonistInteractionScanner scanner,
            WorldCoordinateManager coordMgr,
            WorldStreamingController streamCtrl,
            RoadSplineSystem splines,
            DhakaTimeOfDayAndWeatherController weatherCtrl,
            DhakaCinemachineCameraRig camRig,
            WetSurfacePuddleDriver wetDriver)
        {
            var canvasGo = new GameObject("GulshanMilestoneHudCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            var panelGo = new GameObject("HudPanel");
            panelGo.transform.SetParent(canvasGo.transform, false);
            var img = panelGo.AddComponent<Image>();
            img.color = new Color(0.05f, 0.06f, 0.09f, 0.78f);
            img.rectTransform.anchorMin = new Vector2(0.02f, 0.72f);
            img.rectTransform.anchorMax = new Vector2(0.60f, 0.97f);
            img.rectTransform.offsetMin = Vector2.zero;
            img.rectTransform.offsetMax = Vector2.zero;

            var txtGo = new GameObject("HudText");
            txtGo.transform.SetParent(panelGo.transform, false);
            var label = txtGo.AddComponent<TextMeshProUGUI>();
            label.fontSize = 17;
            label.color = new Color(0.95f, 0.96f, 0.98f);
            if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
            label.rectTransform.anchorMin = new Vector2(0.03f, 0.06f);
            label.rectTransform.anchorMax = new Vector2(0.97f, 0.94f);
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;

            var hud = canvasGo.AddComponent<GulshanHatirjheelHud>();
            hud.Configure(car, scanner, coordMgr, streamCtrl, splines, label, weatherCtrl, camRig, wetDriver);
        }

        private static Vector3 ComputeLocalOffsetFor(double lat, double lon)
        {
            UtmCoord origin = GeographicCoordinateConverter.LatLonToUtm(
                GeographicCoordinateConverter.ShaplaOriginLat,
                GeographicCoordinateConverter.ShaplaOriginLon);
            UtmCoord pt = GeographicCoordinateConverter.LatLonToUtm(lat, lon);
            return GeographicCoordinateConverter.UtmToUnityLocal(pt, origin, 0.0);
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects")) AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects/GIS")) AssetDatabase.CreateFolder("Assets/ScriptableObjects", "GIS");
            if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder(WorldGenMatFolder)) AssetDatabase.CreateFolder("Assets/Materials", "WorldGen");
        }

        private sealed class MaterialBundle
        {
            public Material TerrainEarth;
            public Material CarBody;
            public Material TireRubber;
            public Material CommercialGlass;
            public RoadGenerator.RoadVisualMaterials RoadMats;
            public IntersectionGenerator.IntersectionMaterials IntersectionMats;
            public BuildingPlacementSystem.BuildingMaterialPalette BuildingMats;
        }

        private static MaterialBundle EnsureMaterials()
        {
            Material asphalt = CreateMat("Mat_RoadAsphalt", new Color(0.55f, 0.55f, 0.58f), 0.38f);
            BindPbrMaps(asphalt, "WetMonsoonAsphalt");
            Material bridgeConcrete = CreateMat("Mat_BridgeDeck", new Color(0.30f, 0.31f, 0.33f), 0.32f);
            Material flyoverConcrete = CreateMat("Mat_FlyoverViaduct", new Color(0.36f, 0.36f, 0.38f), 0.30f);
            Material curbConcrete = CreateMat("Mat_CurbConcrete", new Color(0.55f, 0.54f, 0.52f), 0.25f);
            Material sidewalk = CreateMat("Mat_SidewalkTile", Color.white, 0.28f);
            BindPbrMaps(sidewalk, "DhakaSidewalkPaver");
            Material whiteMark = CreateMat("Mat_RoadMarkingWhite", new Color(0.92f, 0.92f, 0.90f), 0.15f);
            Material yellowMark = CreateMat("Mat_RoadMarkingYellow", new Color(0.95f, 0.72f, 0.14f), 0.20f);
            Material water = CreateMat("Mat_HatirjheelWater", new Color(0.07f, 0.30f, 0.36f), 0.90f);
            Material turf = CreateMat("Mat_RoundaboutTurf", new Color(0.16f, 0.42f, 0.18f), 0.12f);
            Material steel = CreateMat("Mat_SignalPoleSteel", new Color(0.22f, 0.24f, 0.26f), 0.65f);
            Material greenSig = CreateMat("Mat_SignalGlowGreen", new Color(0.15f, 0.92f, 0.35f), 0.5f);
            Material redSig = CreateMat("Mat_SignalGlowRed", new Color(0.95f, 0.18f, 0.15f), 0.5f);

            Material glass = CreateMat("Mat_GulshanGlassTower", new Color(0.14f, 0.27f, 0.35f), 0.90f);
            Material modernConcrete = CreateMat("Mat_ModernFacade", new Color(0.65f, 0.62f, 0.57f), 0.28f);
            Material dhakaBrick = CreateMat("Mat_DhakaBrick", new Color(0.58f, 0.34f, 0.26f), 0.20f);
            Material civicPlaster = CreateMat("Mat_CivicPlaster", new Color(0.84f, 0.81f, 0.76f), 0.24f);

            Material carPaint = CreateMat("Mat_GulshanGtRed", new Color(0.76f, 0.10f, 0.12f), 0.88f);
            if (carPaint.HasProperty("_Metallic")) carPaint.SetFloat("_Metallic", 0.82f);
            if (carPaint.HasProperty("_CoatMask")) carPaint.SetFloat("_CoatMask", 0.85f);
            if (carPaint.HasProperty("_CoatSmoothness")) carPaint.SetFloat("_CoatSmoothness", 0.95f);

            return new MaterialBundle
            {
                TerrainEarth = CreateMat("Mat_DhakaEarthBase", new Color(0.19f, 0.22f, 0.18f), 0.12f),
                CarBody = carPaint,
                TireRubber = CreateMat("Mat_TireRubber", new Color(0.06f, 0.06f, 0.07f), 0.25f),
                CommercialGlass = glass,
                RoadMats = new RoadGenerator.RoadVisualMaterials
                {
                    AsphaltMaterial = asphalt,
                    BridgeDeckMaterial = bridgeConcrete,
                    FlyoverConcreteMaterial = flyoverConcrete,
                    UnderpassWallMaterial = curbConcrete,
                    CurbConcreteMaterial = curbConcrete,
                    SidewalkTileMaterial = sidewalk,
                    WhiteMarkingMaterial = whiteMark,
                    YellowMarkingMaterial = yellowMark,
                    WaterSurfaceMaterial = water,
                },
                IntersectionMats = new IntersectionGenerator.IntersectionMaterials
                {
                    AsphaltApronMaterial = asphalt,
                    MarkingWhiteMaterial = whiteMark,
                    CurbConcreteMaterial = curbConcrete,
                    IslandTurfMaterial = turf,
                    SignalSteelMaterial = steel,
                    SignalGlowGreenMaterial = greenSig,
                    SignalGlowRedMaterial = redSig,
                },
                BuildingMats = new BuildingPlacementSystem.BuildingMaterialPalette
                {
                    CommercialGlassMaterial = glass,
                    ModernConcreteMaterial = modernConcrete,
                    DhakaBrickMaterial = dhakaBrick,
                    CivicPlasterMaterial = civicPlaster,
                },
            };
        }

        private static void BindPbrMaps(Material mat, string texPrefix)
        {
            const string folder = "Assets/Textures/Environment";
            Texture2D baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/T_{texPrefix}_BaseColor.png");
            Texture2D maskMap = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/T_{texPrefix}_MaskMap.png");
            Texture2D normMap = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/T_{texPrefix}_Normal.png");
            if (baseMap != null && mat.HasProperty("_BaseColorMap")) mat.SetTexture("_BaseColorMap", baseMap);
            if (maskMap != null && mat.HasProperty("_MaskMap")) mat.SetTexture("_MaskMap", maskMap);
            if (normMap != null && mat.HasProperty("_NormalMap")) mat.SetTexture("_NormalMap", normMap);
        }

        private static Material CreateMat(string name, Color albedo, float smoothness)
        {
            string path = $"{WorldGenMatFolder}/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("HDRP/Lit");
                mat = new Material(shader != null ? shader : Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, path);
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", albedo);
            else mat.color = albedo;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
