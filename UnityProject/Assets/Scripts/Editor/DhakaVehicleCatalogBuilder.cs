using System.Collections.Generic;
using System.IO;
using DhakaGta.Data;
using DhakaGta.Vehicles;
using UnityEditor;
using UnityEngine;

namespace DhakaGta.Editor
{
    /// <summary>
    /// Creates all 11 Dhaka VehicleSpecSO configurations and reusable Prefabs (PF_Vehicle_*.prefab),
    /// binding Blender FBX exports automatically when available under Assets/Models/Environment/Vehicles/.
    /// </summary>
    public static class DhakaVehicleCatalogBuilder
    {
        private const string SpecsFolder = "Assets/ScriptableObjects/Vehicles";
        private const string PrefabsFolder = "Assets/Prefabs/Vehicles";
        private const string MatFolder = "Assets/Materials/Vehicles";

        private struct SpecDef
        {
            public string FileName;
            public string Id;
            public string DisplayName;
            public DhakaVehicleClass Class;
            public DrivetrainMode Drive;
            public int Seats;
            public int Wheels;
            public Vector3 Size;
            public float WheelRadius;
            public float Wheelbase;
            public float Track;
            public float Mass;
            public float Torque;
            public float Brake;
            public float TopKph;
            public float RevKph;
            public float SteerDeg;
            public float SuspTravel;
            public float Spring;
            public float Damper;
            public float WetMult;
            public float MaxLean;
            public float MaxHp;
            public bool IsWater;
            public float IdleHz;
            public float MaxHz;
            public float HornHz;
            public Color Color;
            public string FbxFileName;
        }

        [MenuItem("Dhaka-GTA/Vehicles/Build All 11 Vehicle Specs & Prefabs")]
        public static VehicleSpecSO[] BuildAllSpecsAndPrefabs()
        {
            EnsureFolders();
            DhakaVehicleFactory.VehicleVisualMaterials mats = CreateVehicleMaterials();
            SpecDef[] defs = GetAllElevenVehicleDefinitions();
            var specs = new VehicleSpecSO[defs.Length];

            for (int i = 0; i < defs.Length; i++)
            {
                SpecDef d = defs[i];
                string assetPath = $"{SpecsFolder}/VehicleSpec_{d.FileName}.asset";
                var so = AssetDatabase.LoadAssetAtPath<VehicleSpecSO>(assetPath);
                if (so == null)
                {
                    so = ScriptableObject.CreateInstance<VehicleSpecSO>();
                    AssetDatabase.CreateAsset(so, assetPath);
                }

                so.vehicleId = d.Id;
                so.displayName = d.DisplayName;
                so.vehicleClass = d.Class;
                so.drivetrain = d.Drive;
                so.seatCount = d.Seats;
                so.wheelCount = d.Wheels;
                so.chassisSizeMeters = d.Size;
                so.wheelRadiusMeters = d.WheelRadius;
                so.wheelbaseMeters = d.Wheelbase;
                so.trackWidthMeters = d.Track;
                so.massKg = d.Mass;
                so.maxMotorTorqueNm = d.Torque;
                so.maxBrakeTorqueNm = d.Brake;
                so.maxSpeedKph = d.TopKph;
                so.reverseMaxSpeedKph = d.RevKph;
                so.maxSteerAngleDeg = d.SteerDeg;
                so.suspensionTravelMeters = d.SuspTravel;
                so.suspensionSpringStiffness = d.Spring;
                so.suspensionDamper = d.Damper;
                so.wetGripMultiplier = d.WetMult;
                so.maxLeanAngleDeg = d.MaxLean;
                so.maxHealth = d.MaxHp;
                so.isRightHandDrive = true;
                so.isWatercraft = d.IsWater;
                so.idleEngineFreqHz = d.IdleHz;
                so.maxEngineFreqHz = d.MaxHz;
                so.hornFreqHz = d.HornHz;
                so.typicalLiveries = new[] { d.Color };
                EditorUtility.SetDirty(so);
                specs[i] = so;

                GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Models/Environment/Vehicles/{d.FbxFileName}");
                GameObject instance = DhakaVehicleFactory.CreateVehicleFromSpec(so, Vector3.zero, Quaternion.identity, null, mats, fbx);
                PrefabUtility.SaveAsPrefabAsset(instance, $"{PrefabsFolder}/PF_Vehicle_{d.FileName}.prefab");
                Object.DestroyImmediate(instance);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[DhakaVehicleCatalogBuilder] Built all {specs.Length} Dhaka vehicle specs & prefabs.");
            return specs;
        }

        private static SpecDef[] GetAllElevenVehicleDefinitions()
        {
            return new[]
            {
                new SpecDef { FileName = "CngAuto", Id = "cng_auto", DisplayName = "Dhaka CNG Auto-Rickshaw", Class = DhakaVehicleClass.CngAutoRickshaw, Drive = DrivetrainMode.RearWheelDrive, Seats = 3, Wheels = 3, Size = new Vector3(1.30f, 1.55f, 2.55f), WheelRadius = 0.24f, Wheelbase = 1.95f, Track = 1.16f, Mass = 380f, Torque = 650f, Brake = 1100f, TopKph = 60f, RevKph = 18f, SteerDeg = 38f, SuspTravel = 0.18f, Spring = 15000f, Damper = 2100f, WetMult = 0.56f, MaxLean = 0f, MaxHp = 550f, IdleHz = 72f, MaxHz = 230f, HornHz = 490f, Color = new Color(0.06f, 0.44f, 0.20f), FbxFileName = "Vehicle_CNG_Auto.fbx" },
                new SpecDef { FileName = "CycleRickshaw", Id = "cycle_rickshaw", DisplayName = "Dhaka Cycle Rickshaw", Class = DhakaVehicleClass.CycleRickshaw, Drive = DrivetrainMode.HumanPedalRear, Seats = 3, Wheels = 3, Size = new Vector3(1.12f, 1.65f, 2.25f), WheelRadius = 0.34f, Wheelbase = 1.65f, Track = 1.08f, Mass = 140f, Torque = 240f, Brake = 550f, TopKph = 22f, RevKph = 8f, SteerDeg = 42f, SuspTravel = 0.12f, Spring = 7500f, Damper = 1100f, WetMult = 0.65f, MaxLean = 0f, MaxHp = 280f, IdleHz = 25f, MaxHz = 40f, HornHz = 820f, Color = new Color(0.76f, 0.16f, 0.24f), FbxFileName = "Vehicle_CycleRickshaw.fbx" },
                new SpecDef { FileName = "Motorcycle", Id = "motorcycle", DisplayName = "Dhaka Commuter Motorcycle", Class = DhakaVehicleClass.Motorcycle, Drive = DrivetrainMode.RearWheelDrive, Seats = 2, Wheels = 2, Size = new Vector3(0.68f, 1.10f, 2.05f), WheelRadius = 0.32f, Wheelbase = 1.38f, Track = 0.0f, Mass = 175f, Torque = 480f, Brake = 900f, TopKph = 132f, RevKph = 8f, SteerDeg = 32f, SuspTravel = 0.18f, Spring = 11500f, Damper = 1600f, WetMult = 0.52f, MaxLean = 26f, MaxHp = 350f, IdleHz = 85f, MaxHz = 290f, HornHz = 460f, Color = new Color(0.14f, 0.24f, 0.72f), FbxFileName = "Vehicle_Motorcycle.fbx" },
                new SpecDef { FileName = "CityBus", Id = "city_bus", DisplayName = "Dhaka Metro City Bus", Class = DhakaVehicleClass.CityBus, Drive = DrivetrainMode.RearWheelDrive, Seats = 8, Wheels = 4, Size = new Vector3(2.52f, 3.05f, 10.4f), WheelRadius = 0.52f, Wheelbase = 5.70f, Track = 2.20f, Mass = 9800f, Torque = 9500f, Brake = 18000f, TopKph = 78f, RevKph = 20f, SteerDeg = 34f, SuspTravel = 0.28f, Spring = 135000f, Damper = 16000f, WetMult = 0.62f, MaxLean = 0f, MaxHp = 2800f, IdleHz = 36f, MaxHz = 115f, HornHz = 220f, Color = new Color(0.08f, 0.44f, 0.22f), FbxFileName = "Vehicle_CityBus.fbx" },
                new SpecDef { FileName = "IntercityBus", Id = "intercity_bus", DisplayName = "Dhaka Intercity Highway Coach", Class = DhakaVehicleClass.IntercityBus, Drive = DrivetrainMode.RearWheelDrive, Seats = 8, Wheels = 4, Size = new Vector3(2.58f, 3.45f, 12.2f), WheelRadius = 0.54f, Wheelbase = 6.70f, Track = 2.26f, Mass = 13500f, Torque = 12800f, Brake = 22000f, TopKph = 108f, RevKph = 22f, SteerDeg = 32f, SuspTravel = 0.30f, Spring = 175000f, Damper = 21000f, WetMult = 0.62f, MaxLean = 0f, MaxHp = 3400f, IdleHz = 32f, MaxHz = 105f, HornHz = 195f, Color = new Color(0.84f, 0.32f, 0.12f), FbxFileName = "Vehicle_IntercityBus.fbx" },
                new SpecDef { FileName = "Sedan", Id = "sedan", DisplayName = "Gulshan Executive Sedan", Class = DhakaVehicleClass.CompactSedan, Drive = DrivetrainMode.AllWheelDrive, Seats = 4, Wheels = 4, Size = new Vector3(1.85f, 1.38f, 4.45f), WheelRadius = 0.36f, Wheelbase = 2.75f, Track = 1.60f, Mass = 1420f, Torque = 2100f, Brake = 3400f, TopKph = 148f, RevKph = 36f, SteerDeg = 32f, SuspTravel = 0.22f, Spring = 32000f, Damper = 4200f, WetMult = 0.58f, MaxLean = 0f, MaxHp = 1000f, IdleHz = 55f, MaxHz = 215f, HornHz = 410f, Color = new Color(0.76f, 0.12f, 0.14f), FbxFileName = "Vehicle_Sedan.fbx" },
                new SpecDef { FileName = "SUV", Id = "suv", DisplayName = "Gulshan Diplomat SUV", Class = DhakaVehicleClass.Suv, Drive = DrivetrainMode.AllWheelDrive, Seats = 4, Wheels = 4, Size = new Vector3(1.98f, 1.76f, 4.75f), WheelRadius = 0.42f, Wheelbase = 3.05f, Track = 1.72f, Mass = 2150f, Torque = 3100f, Brake = 4600f, TopKph = 136f, RevKph = 34f, SteerDeg = 30f, SuspTravel = 0.32f, Spring = 46000f, Damper = 6200f, WetMult = 0.64f, MaxLean = 0f, MaxHp = 1450f, IdleHz = 48f, MaxHz = 185f, HornHz = 365f, Color = new Color(0.16f, 0.19f, 0.22f), FbxFileName = "Vehicle_SUV.fbx" },
                new SpecDef { FileName = "PoliceCruiser", Id = "police_cruiser", DisplayName = "DMP Patrol Cruiser", Class = DhakaVehicleClass.DmpPoliceCruiser, Drive = DrivetrainMode.AllWheelDrive, Seats = 4, Wheels = 4, Size = new Vector3(1.96f, 1.72f, 4.85f), WheelRadius = 0.41f, Wheelbase = 3.02f, Track = 1.70f, Mass = 1980f, Torque = 3250f, Brake = 4800f, TopKph = 152f, RevKph = 38f, SteerDeg = 33f, SuspTravel = 0.28f, Spring = 44000f, Damper = 5800f, WetMult = 0.65f, MaxLean = 0f, MaxHp = 1600f, IdleHz = 52f, MaxHz = 205f, HornHz = 440f, Color = new Color(0.10f, 0.22f, 0.52f), FbxFileName = "Vehicle_PoliceCruiser.fbx" },
                new SpecDef { FileName = "Pickup", Id = "pickup", DisplayName = "Dhaka Utility Pickup", Class = DhakaVehicleClass.PickupTruck, Drive = DrivetrainMode.AllWheelDrive, Seats = 4, Wheels = 4, Size = new Vector3(1.98f, 1.74f, 5.10f), WheelRadius = 0.42f, Wheelbase = 3.22f, Track = 1.74f, Mass = 2300f, Torque = 3400f, Brake = 4900f, TopKph = 128f, RevKph = 32f, SteerDeg = 30f, SuspTravel = 0.30f, Spring = 48000f, Damper = 6400f, WetMult = 0.62f, MaxLean = 0f, MaxHp = 1650f, IdleHz = 45f, MaxHz = 175f, HornHz = 340f, Color = new Color(0.84f, 0.80f, 0.72f), FbxFileName = "Vehicle_Pickup.fbx" },
                new SpecDef { FileName = "Speedboat", Id = "speedboat", DisplayName = "Hatirjheel Interceptor Speedboat", Class = DhakaVehicleClass.Speedboat, Drive = DrivetrainMode.MarinePropeller, Seats = 4, Wheels = 0, Size = new Vector3(2.35f, 1.25f, 6.40f), WheelRadius = 0f, Mass = 1250f, Torque = 1400f, TopKph = 82f, RevKph = 24f, MaxHp = 1100f, IsWater = true, IdleHz = 62f, MaxHz = 225f, HornHz = 310f, Color = new Color(0.92f, 0.94f, 0.96f), FbxFileName = "Vehicle_Speedboat.fbx" },
                new SpecDef { FileName = "BurigangaLaunch", Id = "buriganga_launch", DisplayName = "Sadarghat Passenger Launch", Class = DhakaVehicleClass.BurigangaLaunch, Drive = DrivetrainMode.MarinePropeller, Seats = 8, Wheels = 0, Size = new Vector3(7.40f, 5.20f, 24.0f), WheelRadius = 0f, Mass = 42000f, Torque = 18000f, TopKph = 36f, RevKph = 14f, MaxHp = 6500f, IsWater = true, IdleHz = 28f, MaxHz = 78f, HornHz = 145f, Color = new Color(0.90f, 0.90f, 0.88f), FbxFileName = "Vehicle_BurigangaLaunch.fbx" },
            };
        }

        private static DhakaVehicleFactory.VehicleVisualMaterials CreateVehicleMaterials()
        {
            Shader s = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
            Material Make(string name, Color col, float metal, float smooth, float coat = 0f)
            {
                string path = $"{MatFolder}/{name}.mat";
                Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(s);
                    AssetDatabase.CreateAsset(m, path);
                }
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
                else m.color = col;
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
                if (coat > 0f && m.HasProperty("_CoatMask")) m.SetFloat("_CoatMask", coat);
                EditorUtility.SetDirty(m);
                return m;
            }

            return new DhakaVehicleFactory.VehicleVisualMaterials
            {
                PaintBodyMat = Make("Mat_VehicleBodyClearCoat", Color.white, 0.78f, 0.88f, 0.85f),
                CabinGlassMat = Make("Mat_VehicleGlass", new Color(0.10f, 0.20f, 0.28f), 0.15f, 0.95f, 0.5f),
                TrimDarkMat = Make("Mat_VehicleDarkTrim", new Color(0.11f, 0.12f, 0.13f), 0.35f, 0.42f),
                TireRubberMat = Make("Mat_VehicleTireRubber", new Color(0.06f, 0.06f, 0.07f), 0f, 0.22f),
                AlloyRimMat = Make("Mat_VehicleAlloyRim", new Color(0.74f, 0.76f, 0.78f), 0.92f, 0.85f),
                BrakeLightMat = Make("Mat_VehicleBrakeLight", new Color(0.92f, 0.12f, 0.10f), 0f, 0.9f),
                ReverseLightMat = Make("Mat_VehicleReverseLight", new Color(0.95f, 0.95f, 0.95f), 0f, 0.9f),
            };
        }

        private static void EnsureFolders()
        {
            string[] folders = { "Assets/ScriptableObjects", SpecsFolder, "Assets/Prefabs", PrefabsFolder, "Assets/Materials", MatFolder };
            foreach (string f in folders)
            {
                string parent = Path.GetDirectoryName(f)?.Replace("\\", "/");
                string leaf = Path.GetFileName(f);
                if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(f))
                {
                    AssetDatabase.CreateFolder(parent, leaf);
                }
            }
        }
    }
}
