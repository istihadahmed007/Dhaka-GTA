using System;
using System.Collections.Generic;
using System.IO;
using DhakaGta.Data;
using UnityEditor;
using UnityEngine;

namespace DhakaGta.Editor
{
    /// <summary>
    /// Parses ../GeographicData/dhaka_world_geo.json into typed LandmarkDefinitionSO and RoadCorridorSO assets
    /// and seeds canonical DistrictDefinitionSO, ProtagonistProfileSO, VehicleSpecSO, WeaponSpecSO, WeatherPresetSO,
    /// MissionDefinitionSO, and WorldStreamingConfigSO assets cleanly.
    /// </summary>
    public static class DhakaGeoJsonImporter
    {
        private const string ScriptableRoot = "Assets/ScriptableObjects";

        [Serializable]
        private sealed class GeoRootJson
        {
            public LandmarkJson[] landmarks;
            public CorridorJson[] corridors;
        }

        [Serializable]
        private sealed class LandmarkJson
        {
            public string osmId;
            public string kind;
            public string name;
            public string banglaName;
            public double lat;
            public double lon;
            public string district;
            public float stylizedX;
            public float stylizedZ;
            public string architecturalStyle;
        }

        [Serializable]
        private sealed class CorridorJson
        {
            public string roadId;
            public string name;
            public string banglaName;
            public float x1;
            public float z1;
            public float x2;
            public float z2;
            public float width;
            public string tier;
        }

        public struct ImportedCatalog
        {
            public DistrictDefinitionSO[] Districts;
            public LandmarkDefinitionSO[] Landmarks;
            public RoadCorridorSO[] Roads;
            public ProtagonistProfileSO[] Protagonists;
            public VehicleSpecSO[] Vehicles;
            public WeaponSpecSO[] Weapons;
            public WeatherPresetSO[] WeatherPresets;
            public MissionDefinitionSO[] Missions;
            public WorldStreamingConfigSO StreamingConfig;
        }

        [MenuItem("Dhaka-GTA/Foundation/Sync GeographicData & Core ScriptableObjects")]
        public static ImportedCatalog SyncAllFoundationAssets()
        {
            EnsureFolderTree();

            var districts = CreateOrUpdateDistricts();
            var landmarks = ImportLandmarksFromGeoJson();
            var roads = ImportRoadsFromGeoJson();
            var protagonists = CreateOrUpdateProtagonists();
            var vehicles = CreateOrUpdateVehicles();
            var weapons = CreateOrUpdateWeapons();
            var weather = CreateOrUpdateWeatherPresets();
            var missions = CreateOrUpdateMissions();
            var streaming = CreateOrUpdateStreamingConfig(districts);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[DhakaGeoJsonImporter] Synced {districts.Length} districts, {landmarks.Length} landmarks, {roads.Length} roads, {protagonists.Length} protagonists, {vehicles.Length} vehicles, {weapons.Length} weapons.");

            return new ImportedCatalog
            {
                Districts = districts,
                Landmarks = landmarks,
                Roads = roads,
                Protagonists = protagonists,
                Vehicles = vehicles,
                Weapons = weapons,
                WeatherPresets = weather,
                Missions = missions,
                StreamingConfig = streaming,
            };
        }

        private static void EnsureFolderTree()
        {
            string[] subfolders =
            {
                "Districts", "Landmarks", "Roads", "Protagonists",
                "Vehicles", "Weapons", "Weather", "Missions"
            };

            foreach (string sub in subfolders)
            {
                string full = $"{ScriptableRoot}/{sub}";
                if (!AssetDatabase.IsValidFolder(full))
                {
                    AssetDatabase.CreateFolder(ScriptableRoot, sub);
                }
            }
        }

        private static T LoadOrCreateAsset<T>(string assetPath) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            return asset;
        }

        private static DistrictDefinitionSO[] CreateOrUpdateDistricts()
        {
            // 1:1 Real-World Metric Bounds (+X = East, +Z = North from Shapla Chottor UTM 46N Origin)
            var specs = new (string id, string name, string bn, float minX, float minZ, float maxX, float maxZ, int prio, string scene, Color tint)[]
            {
                ("shapla_crossing", "Shapla Crossing", "শাপলা চত্বর", -650f, -650f, 650f, 650f, 30, "District_ShaplaCore", new Color(1f, 0.9f, 0.72f)),
                ("motijheel", "Motijheel C/A", "মতিঝিল বাণিজ্যিক এলাকা", 200f, -900f, 1800f, 800f, 20, "District_Motijheel", new Color(0.95f, 0.9f, 0.8f)),
                ("kawran_flyover", "Kawran Bazar & Express Flyover", "কারওয়ান বাজার", -3200f, 900f, -600f, 3200f, 20, "District_KawranBazar", new Color(0.92f, 0.88f, 0.82f)),
                ("dhanmondi", "Dhanmondi & Rabindra Sarobar", "ধানমন্ডি", -5200f, 400f, -2400f, 3200f, 15, "District_Dhanmondi", new Color(0.84f, 0.95f, 0.86f)),
                ("gulshan", "Gulshan & Hatirjheel", "গুলশান ও হাতিরঝিল", -800f, 2800f, 2600f, 8500f, 15, "District_Gulshan", new Color(0.85f, 0.92f, 1f)),
                ("old_dhaka", "Old Dhaka · Chawkbazar", "পুরান ঢাকা", -4200f, -2950f, -650f, -750f, 15, "District_OldDhaka", new Color(1f, 0.84f, 0.74f)),
                ("sadarghat", "Sadarghat River Terminal", "সদরঘাট", -1600f, -3800f, 600f, -2450f, 25, "District_Sadarghat", new Color(0.75f, 0.88f, 0.96f)),
                ("mirpur_market", "Mirpur 10 Roundabout", "মিরপুর ১০", -6500f, 6500f, -2800f, 11500f, 10, "District_Mirpur", new Color(0.94f, 0.88f, 0.78f)),
                ("uttara_north", "Uttara Metro Sector", "উত্তরা", -3500f, 12000f, 1500f, 18500f, 10, "District_Uttara", new Color(0.88f, 0.93f, 0.98f)),
            };

            var list = new DistrictDefinitionSO[specs.Length];
            for (int i = 0; i < specs.Length; i++)
            {
                var s = specs[i];
                var so = LoadOrCreateAsset<DistrictDefinitionSO>($"{ScriptableRoot}/Districts/District_{s.id}.asset");
                so.districtId = s.id;
                so.displayName = s.name;
                so.banglaName = s.bn;
                so.boundsMinXZ = new Vector2(s.minX, s.minZ);
                so.boundsMaxXZ = new Vector2(s.maxX, s.maxZ);
                so.priority = s.prio;
                so.additiveSceneName = s.scene;
                so.addressableGroupKey = s.scene;
                so.ambientTint = s.tint;
                EditorUtility.SetDirty(so);
                list[i] = so;
            }
            return list;
        }

        private static LandmarkDefinitionSO[] ImportLandmarksFromGeoJson()
        {
            GeoRootJson root = LoadGeoJson();
            if (root?.landmarks == null) return Array.Empty<LandmarkDefinitionSO>();

            var list = new List<LandmarkDefinitionSO>();
            foreach (LandmarkJson lm in root.landmarks)
            {
                var so = LoadOrCreateAsset<LandmarkDefinitionSO>($"{ScriptableRoot}/Landmarks/Landmark_{lm.osmId}.asset");
                so.osmId = lm.osmId;
                so.kind = ParseLandmarkKind(lm.kind);
                so.displayName = lm.name;
                so.banglaName = lm.banglaName;
                so.districtId = lm.district;
                so.latitude = lm.lat;
                so.longitude = lm.lon;
                so.stylizedPosition = new Vector3(lm.stylizedX, 0f, lm.stylizedZ);
                so.architecturalStyle = lm.architecturalStyle;
                EditorUtility.SetDirty(so);
                list.Add(so);
            }
            return list.ToArray();
        }

        private static RoadCorridorSO[] ImportRoadsFromGeoJson()
        {
            GeoRootJson root = LoadGeoJson();
            if (root?.corridors == null) return Array.Empty<RoadCorridorSO>();

            var list = new List<RoadCorridorSO>();
            foreach (CorridorJson r in root.corridors)
            {
                var so = LoadOrCreateAsset<RoadCorridorSO>($"{ScriptableRoot}/Roads/Road_{r.roadId}.asset");
                so.roadId = r.roadId;
                so.roadName = r.name;
                so.banglaName = r.banglaName;
                so.startXZ = new Vector2(r.x1, r.z1);
                so.endXZ = new Vector2(r.x2, r.z2);
                so.widthMeters = r.width;
                so.tier = r.tier == "avenue" ? RoadTier.Avenue : (r.tier == "street" ? RoadTier.Street : RoadTier.Boulevard);
                so.hasOverheadMetro = r.roadId == "ave_metro_line6";
                EditorUtility.SetDirty(so);
                list.Add(so);
            }
            return list.ToArray();
        }

        private static ProtagonistProfileSO[] CreateOrUpdateProtagonists()
        {
            var tariq = LoadOrCreateAsset<ProtagonistProfileSO>($"{ScriptableRoot}/Protagonists/Protagonist_Tariq.asset");
            tariq.protagonistId = "tariq";
            tariq.fullName = "Tariq Hasan";
            tariq.banglaCallsign = "বাঘা তারেক";
            tariq.backgroundSummary = "Former Dhaka street courier & driver balancing heavy syndicate pressure.";
            tariq.maxHealth = 135f;
            tariq.maxArmor = 100f;
            tariq.maxStamina = 115f;
            tariq.walkSpeedMetersPerSec = 2.4f;
            tariq.jogSpeedMetersPerSec = 5.2f;
            tariq.sprintSpeedMetersPerSec = 7.8f;
            tariq.combatDamageMultiplier = 1.25f;
            tariq.meleeDamageMultiplier = 1.45f;
            tariq.aimPrecisionMultiplier = 1.0f;
            tariq.driftHandlingBonus = 0.45f;
            tariq.collisionResistance = 1.35f;
            tariq.stealthMultiplier = 0.95f;
            tariq.specialAbility = DhakaGta.Core.SpecialAbilityKind.StreetOverdrive;
            tariq.abilityName = "Street Overdrive";
            tariq.abilityDescription = "Unleashes Dhaka street brawler momentum: heavy melee force, stamina surge, and sharp drift traction.";
            tariq.abilityDurationSeconds = 9.0f;
            tariq.abilityCooldownSeconds = 20.0f;
            tariq.isFeminineBuild = false;
            tariq.characterHeightMeters = 1.80f;
            tariq.skinToneColor = new Color(0.76f, 0.54f, 0.40f, 1f);
            tariq.hairColor = new Color(0.06f, 0.055f, 0.05f, 1f);
            tariq.primaryGarmentColor = new Color(0.118f, 0.227f, 0.373f, 1f);
            tariq.secondaryGarmentColor = new Color(0.122f, 0.141f, 0.176f, 1f);
            tariq.accentColor = new Color(0.831f, 0.627f, 0.090f, 1f);
            tariq.defaultSpawnPosition = new Vector3(-2.2f, 0.1f, 35.2f);
            tariq.defaultDistrictId = "shapla_crossing";
            EditorUtility.SetDirty(tariq);

            var maya = LoadOrCreateAsset<ProtagonistProfileSO>($"{ScriptableRoot}/Protagonists/Protagonist_Maya.asset");
            maya.protagonistId = "maya";
            maya.fullName = "Maya Rahman";
            maya.banglaCallsign = "মায়া";
            maya.backgroundSummary = "Tech fixer & night scout across Gulshan and Hatirjheel.";
            maya.maxHealth = 110f;
            maya.maxArmor = 120f;
            maya.maxStamina = 140f;
            maya.walkSpeedMetersPerSec = 2.6f;
            maya.jogSpeedMetersPerSec = 5.7f;
            maya.sprintSpeedMetersPerSec = 8.4f;
            maya.combatDamageMultiplier = 1.05f;
            maya.meleeDamageMultiplier = 1.0f;
            maya.aimPrecisionMultiplier = 1.65f;
            maya.driftHandlingBonus = 0.25f;
            maya.collisionResistance = 1.0f;
            maya.stealthMultiplier = 1.35f;
            maya.specialAbility = DhakaGta.Core.SpecialAbilityKind.FocusReflex;
            maya.abilityName = "Focus Reflex";
            maya.abilityDescription = "Slows tactical perception across Dhaka rooftops & alleys while locking upper-body aim precision.";
            maya.abilityDurationSeconds = 7.5f;
            maya.abilityCooldownSeconds = 18.0f;
            maya.isFeminineBuild = true;
            maya.characterHeightMeters = 1.70f;
            maya.skinToneColor = new Color(0.80f, 0.59f, 0.45f, 1f);
            maya.hairColor = new Color(0.08f, 0.06f, 0.055f, 1f);
            maya.primaryGarmentColor = new Color(0.541f, 0.110f, 0.153f, 1f);
            maya.secondaryGarmentColor = new Color(0.078f, 0.094f, 0.122f, 1f);
            maya.accentColor = new Color(0.0f, 0.824f, 0.827f, 1f);
            maya.defaultSpawnPosition = new Vector3(45f, 0.1f, -43f);
            maya.defaultDistrictId = "gulshan";
            EditorUtility.SetDirty(maya);

            return new[] { tariq, maya };
        }

        private static VehicleSpecSO[] CreateOrUpdateVehicles()
        {
            var cng = LoadOrCreateAsset<VehicleSpecSO>($"{ScriptableRoot}/Vehicles/VehicleSpec_CngAuto.asset");
            cng.vehicleId = "cng_auto";
            cng.displayName = "Dhaka CNG Auto-Rickshaw";
            cng.vehicleClass = DhakaVehicleClass.CngAutoRickshaw;
            cng.seatCount = 3;
            cng.massKg = 380f;
            cng.maxMotorTorqueNm = 260f;
            cng.maxSpeedKph = 58f;
            cng.maxSteerAngleDeg = 38f;
            cng.isRightHandDrive = true;
            cng.typicalLiveries = new[] { new Color(0.06f, 0.42f, 0.2f), new Color(0.08f, 0.36f, 0.18f) };
            EditorUtility.SetDirty(cng);

            var gt = LoadOrCreateAsset<VehicleSpecSO>($"{ScriptableRoot}/Vehicles/VehicleSpec_GulshanGT.asset");
            gt.vehicleId = "gulshan_gt";
            gt.displayName = "Gulshan GT Coupe";
            gt.vehicleClass = DhakaVehicleClass.SportCoupe;
            gt.seatCount = 2;
            gt.massKg = 1420f;
            gt.maxMotorTorqueNm = 680f;
            gt.maxSpeedKph = 165f;
            gt.maxSteerAngleDeg = 30f;
            gt.isRightHandDrive = true;
            gt.typicalLiveries = new[] { new Color(0.75f, 0.12f, 0.12f), new Color(0.14f, 0.18f, 0.25f) };
            EditorUtility.SetDirty(gt);

            var launch = LoadOrCreateAsset<VehicleSpecSO>($"{ScriptableRoot}/Vehicles/VehicleSpec_BurigangaLaunch.asset");
            launch.vehicleId = "buriganga_launch";
            launch.displayName = "Buriganga River Launch";
            launch.vehicleClass = DhakaVehicleClass.BurigangaLaunch;
            launch.seatCount = 6;
            launch.massKg = 4200f;
            launch.maxMotorTorqueNm = 1200f;
            launch.maxSpeedKph = 44f;
            launch.isWatercraft = true;
            EditorUtility.SetDirty(launch);

            return new[] { cng, gt, launch };
        }

        private static WeaponSpecSO[] CreateOrUpdateWeapons()
        {
            var fists = LoadOrCreateAsset<WeaponSpecSO>($"{ScriptableRoot}/Weapons/WeaponSpec_Unarmed.asset");
            fists.weaponId = "unarmed";
            fists.displayName = "Unarmed Strike";
            fists.slot = WeaponSlot.Unarmed;
            fists.baseDamage = 14f;
            fists.rangeMeters = 2.0f;
            fists.isMelee = true;
            EditorUtility.SetDirty(fists);

            var pistol = LoadOrCreateAsset<WeaponSpecSO>($"{ScriptableRoot}/Weapons/WeaponSpec_Service9mm.asset");
            pistol.weaponId = "service_9mm";
            pistol.displayName = "Service 9mm";
            pistol.slot = WeaponSlot.Sidearm;
            pistol.baseDamage = 28f;
            pistol.fireRateRpm = 260f;
            pistol.rangeMeters = 45f;
            pistol.magazineSize = 15;
            EditorUtility.SetDirty(pistol);

            return new[] { fists, pistol };
        }

        private static WeatherPresetSO[] CreateOrUpdateWeatherPresets()
        {
            var golden = LoadOrCreateAsset<WeatherPresetSO>($"{ScriptableRoot}/Weather/Weather_GoldenHourDhaka.asset");
            golden.presetId = "golden_hour";
            golden.displayName = "Dhaka Golden Hour";
            golden.timeOfDayHours = 16.8f;
            golden.sunElevationDeg = 22f;
            golden.sunAzimuthDeg = 238f;
            golden.sunIntensityLux = 82000f;
            golden.sunColorTemperatureK = 4900f;
            golden.fogAttenuationDistanceMeters = 210f;
            golden.wetAsphaltSmoothness = 0.42f;
            EditorUtility.SetDirty(golden);

            var monsoon = LoadOrCreateAsset<WeatherPresetSO>($"{ScriptableRoot}/Weather/Weather_MonsoonOvercast.asset");
            monsoon.presetId = "monsoon_overcast";
            monsoon.displayName = "Monsoon Overcast";
            monsoon.timeOfDayHours = 14.0f;
            monsoon.sunElevationDeg = 46f;
            monsoon.sunIntensityLux = 28000f;
            monsoon.sunColorTemperatureK = 6800f;
            monsoon.fogAttenuationDistanceMeters = 105f;
            monsoon.monsoonRainIntensity = 0.85f;
            monsoon.wetAsphaltSmoothness = 0.88f;
            EditorUtility.SetDirty(monsoon);

            return new[] { golden, monsoon };
        }

        private static MissionDefinitionSO[] CreateOrUpdateMissions()
        {
            var m1 = LoadOrCreateAsset<MissionDefinitionSO>($"{ScriptableRoot}/Missions/Mission_ShaplaDispatch.asset");
            m1.missionId = "shapla_dispatch";
            m1.title = "Shapla Dispatch";
            m1.category = MissionCategory.Story;
            m1.protagonistFilter = "tariq";
            m1.startDistrictId = "shapla_crossing";
            m1.objectiveSummary = "Meet the Motijheel runner outside Bangladesh Bank Plaza without alerting DMP patrols.";
            m1.rewardBdt = 12000;
            m1.wantedRiskStars = 1;
            m1.objectiveSteps = new[] { "Survey Shapla Plaza", "Drive a CNG to Motijheel Tower", "Evade patrol sweep" };
            EditorUtility.SetDirty(m1);

            return new[] { m1 };
        }

        private static WorldStreamingConfigSO CreateOrUpdateStreamingConfig(DistrictDefinitionSO[] districts)
        {
            var cfg = LoadOrCreateAsset<WorldStreamingConfigSO>($"{ScriptableRoot}/WorldStreamingConfig.asset");
            cfg.verticalSliceCenter = Vector3.zero;
            cfg.streamingRadiusMeters = 140f;
            cfg.unloadHysteresisMeters = 30f;
            cfg.unloadDelaySeconds = 4f;
            cfg.districts = districts;
            EditorUtility.SetDirty(cfg);
            return cfg;
        }

        private static GeoRootJson LoadGeoJson()
        {
            string geoPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "GeographicData", "dhaka_world_geo.json"));
            if (!File.Exists(geoPath))
            {
                Debug.LogWarning("[DhakaGeoJsonImporter] Missing GIS JSON at: " + geoPath);
                return null;
            }

            string json = File.ReadAllText(geoPath);
            return JsonUtility.FromJson<GeoRootJson>(json);
        }

        private static LandmarkKind ParseLandmarkKind(string kind)
        {
            return kind switch
            {
                "monument" => LandmarkKind.Monument,
                "mosque" => LandmarkKind.Mosque,
                "market" => LandmarkKind.Market,
                "flyover" => LandmarkKind.Flyover,
                "terminal" => LandmarkKind.Terminal,
                "palace" => LandmarkKind.Palace,
                "fort" => LandmarkKind.Fort,
                "lake" => LandmarkKind.Lake,
                "metro" => LandmarkKind.Metro,
                _ => LandmarkKind.Monument,
            };
        }
    }
}
