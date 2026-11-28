using System.IO;
using DhakaGta.Core;
using DhakaGta.GIS;
using DhakaGta.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DhakaGta.Editor
{
    /// <summary>
    /// Editor tooling for importing OpenStreetMap GeoJSON extracts into GeoFeatureDatabase assets
    /// and generating GeographicTestScene.unity at strict 1 Unity unit = 1 real-world meter scale.
    /// </summary>
    public static class DhakaGeoEditorTools
    {
        public const string DefaultGeoJsonRelativePath = "../GeographicData/dhaka_osm_extract.geojson";
        public const string DatabaseAssetPath = "Assets/ScriptableObjects/GIS/DhakaOsmFeatureDatabase.asset";
        public const string GeoTestScenePath = "Assets/Scenes/GeographicTestScene.unity";
        private const string GisMaterialsFolder = "Assets/Materials/GIS";

        [MenuItem("Dhaka-GTA/GIS/Import Default OSM GeoJSON to Feature Database")]
        public static GeoFeatureDatabase ImportDefaultGeoDatabase()
        {
            string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, DefaultGeoJsonRelativePath));
            return ImportGeoJsonAsset(fullPath);
        }

        [MenuItem("Dhaka-GTA/GIS/Import Custom GeoJSON File...")]
        public static void ImportCustomGeoJsonDialog()
        {
            string selected = EditorUtility.OpenFilePanel("Select Dhaka GeoJSON Extract", Application.dataPath, "geojson,json");
            if (!string.IsNullOrWhiteSpace(selected))
            {
                ImportGeoJsonAsset(selected);
            }
        }

        public static GeoFeatureDatabase ImportGeoJsonAsset(string geoJsonFullPath)
        {
            EnsureGisFolders();

            GeoFeatureDatabase db = AssetDatabase.LoadAssetAtPath<GeoFeatureDatabase>(DatabaseAssetPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<GeoFeatureDatabase>();
                AssetDatabase.CreateAsset(db, DatabaseAssetPath);
            }

            if (!DhakaGeoImporter.ImportGeoJsonFileIntoDatabase(geoJsonFullPath, db))
            {
                return null;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[DhakaGeoEditorTools] Imported {db.TotalFeatureCount} UTM Zone 46N features into {DatabaseAssetPath}.");
            return db;
        }

        [MenuItem("Dhaka-GTA/GIS/Build 1:1 Scale GeographicTestScene")]
        public static void BuildGeographicTestScene()
        {
            GeoFeatureDatabase db = ImportDefaultGeoDatabase();
            if (db == null)
            {
                Debug.LogError("[DhakaGeoEditorTools] Cannot build GeographicTestScene without GeoFeatureDatabase.");
                return;
            }

            DhakaGeoImporter.SceneBuildMaterials mats = EnsureGisHdrpMaterials();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // HDRP Sun & Sky/Fog Volume
            var env = new GameObject("HDRP_GeoEnvironment");
            var sunGo = new GameObject("SunLight");
            sunGo.transform.SetParent(env.transform, false);
            sunGo.transform.rotation = Quaternion.Euler(34f, 225f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.useColorTemperature = true;
            sun.colorTemperature = 5400f;
            sun.intensity = 82000f;
            sun.shadows = LightShadows.Soft;
            sunGo.AddComponent<HDAdditionalLightData>();

            var volGo = new GameObject("SkyAndFogVolume");
            volGo.transform.SetParent(env.transform, false);
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/SkyandFogSettingsProfile.asset");

            // Datum / Ground Reference Plane around Shapla Chottor (no compression)
            var datumFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            datumFloor.name = "DhakaDatumPlane_1To1Meters";
            datumFloor.layer = PhysicsLayers.Ground;
            datumFloor.transform.position = new Vector3(0f, -0.5f, 0f);
            datumFloor.transform.localScale = new Vector3(12000f, 1f, 12000f);
            datumFloor.GetComponent<MeshRenderer>().sharedMaterial = mats.DatumGroundMaterial;

            // Camera elevated over Motijheel / Shapla Chottor looking Southwest toward Old Dhaka & Buriganga
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(320f, 380f, -420f);
            camGo.transform.LookAt(new Vector3(0f, 20f, 0f), Vector3.up);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 25000f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<HDAdditionalCameraData>();

            // WorldCoordinateManager at UTM Zone 46N Origin (Shapla Chottor)
            var coordGo = new GameObject("WorldCoordinateManager");
            var coordManager = coordGo.AddComponent<WorldCoordinateManager>();
            coordManager.Configure(
                db,
                GeographicCoordinateConverter.ShaplaOriginLat,
                GeographicCoordinateConverter.ShaplaOriginLon,
                0.0,
                camGo.transform);

            // Instantiate Full 1:1 Geographic Layers (Roads, Buildings, Water, Metro/Rail, Landmarks)
            var worldRoot = new GameObject("DhakaRealWorld_UTM46N");
            DhakaGeoImporter.SceneBuildSummary summary = DhakaGeoImporter.InstantiateDatabaseInScene(
                db, coordManager, worldRoot.transform, maxDistanceFromOriginMeters: 0.0, mats);

            // Overlay HUD showing WGS84 + UTM46N + Floating-Origin readout
            var canvasGo = new GameObject("GeoTelemetryCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            var panelGo = new GameObject("TelemetryPanel");
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panelImg = panelGo.AddComponent<Image>();
            panelImg.color = new Color(0.05f, 0.07f, 0.10f, 0.78f);
            panelImg.rectTransform.anchorMin = new Vector2(0.02f, 0.78f);
            panelImg.rectTransform.anchorMax = new Vector2(0.58f, 0.97f);
            panelImg.rectTransform.offsetMin = Vector2.zero;
            panelImg.rectTransform.offsetMax = Vector2.zero;

            var textGo = new GameObject("TelemetryText");
            textGo.transform.SetParent(panelGo.transform, false);
            var label = textGo.AddComponent<TextMeshProUGUI>();
            label.fontSize = 18;
            label.color = new Color(0.92f, 0.96f, 0.98f);
            if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
            label.rectTransform.anchorMin = new Vector2(0.02f, 0.05f);
            label.rectTransform.anchorMax = new Vector2(0.98f, 0.95f);
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;

            var hud = canvasGo.AddComponent<GeographicTestSceneHud>();
            hud.Configure(coordManager, camGo.transform, label);

            EditorSceneManager.SaveScene(scene, GeoTestScenePath);
            Debug.Log(
                $"[DhakaGeoEditorTools] Built {GeoTestScenePath}: {summary.RoadMeshesCreated} roads, {summary.BuildingMeshesCreated} buildings, " +
                $"{summary.WaterMeshesCreated} water, {summary.MetroRailMeshesCreated} metro/rail, {summary.LandmarksSpawned} landmarks.");
        }

        private static void EnsureGisFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            {
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            }
            if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects/GIS"))
            {
                AssetDatabase.CreateFolder("Assets/ScriptableObjects", "GIS");
            }
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            {
                AssetDatabase.CreateFolder("Assets", "Materials");
            }
            if (!AssetDatabase.IsValidFolder(GisMaterialsFolder))
            {
                AssetDatabase.CreateFolder("Assets/Materials", "GIS");
            }
        }

        private sealed class GisMaterialBundle : DhakaGeoImporter.SceneBuildMaterials
        {
            public Material DatumGroundMaterial;
        }

        private static GisMaterialBundle EnsureGisHdrpMaterials()
        {
            EnsureGisFolders();
            return new GisMaterialBundle
            {
                DatumGroundMaterial = CreateMat("Mat_GisDatumTerrain", new Color(0.22f, 0.23f, 0.24f), 0.15f),
                RoadMaterial = CreateMat("Mat_GisAsphaltRoad", new Color(0.11f, 0.11f, 0.13f), 0.35f),
                BuildingMaterial = CreateMat("Mat_GisBuildingConcrete", new Color(0.62f, 0.57f, 0.51f), 0.25f),
                WaterMaterial = CreateMat("Mat_GisDhakaWater", new Color(0.08f, 0.32f, 0.38f), 0.85f),
                MetroMaterial = CreateMat("Mat_GisMrt6Viaduct", new Color(0.15f, 0.58f, 0.42f), 0.55f),
                RailMaterial = CreateMat("Mat_GisRailwaySteel", new Color(0.36f, 0.28f, 0.22f), 0.45f),
                LandmarkMaterial = CreateMat("Mat_GisLandmarkBeacon", new Color(0.94f, 0.72f, 0.22f), 0.65f),
            };
        }

        private static Material CreateMat(string matName, Color albedo, float smoothness)
        {
            string path = $"{GisMaterialsFolder}/{matName}.mat";
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
