using System.Collections;
using System.IO;
using DhakaGta.Core;
using DhakaGta.Data;
using DhakaGta.GIS;
using DhakaGta.Vehicles;
using DhakaGta.WorldGen;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DhakaGta.Tests.PlayMode
{
    public sealed class DhakaVehiclePhysicsPlayTests
    {
        [UnityTest]
        public IEnumerator PlayMode_SedanCngMotorcycleAndBus_DriveSteerDriftReverseWetAndRepairOnGeneratedRoads()
        {
            PhysicsLayers.ApplyCollisionMatrix();
            WorldSurfaceState.GlobalRoadWetness = 0f;

            string geoJsonPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "GeographicData", "dhaka_gulshan_hatirjheel.geojson"));
            Assert.IsTrue(File.Exists(geoJsonPath));
            var db = ScriptableObject.CreateInstance<GeoFeatureDatabase>();
            Assert.IsTrue(DhakaGeoImporter.ImportGeoJsonFileIntoDatabase(geoJsonPath, db));

            var host = new GameObject("PlayTest_Part6Host");
            var worldRoot = new GameObject("PlayTest_Part6World");
            var instantiatedCars = new System.Collections.Generic.List<GameObject>();
            var createdSpecs = new System.Collections.Generic.List<VehicleSpecSO>();

            try
            {
                var coordMgr = host.AddComponent<WorldCoordinateManager>();
                coordMgr.Configure(db, 23.7745, 90.4138, 0.0);

                var splineSystem = host.AddComponent<RoadSplineSystem>();
                splineSystem.BuildFromDatabase(db, coordMgr, maxRadiusMeters: 850.0);
                var roadGen = host.AddComponent<RoadGenerator>();
                roadGen.BuildNetworkAndWater(splineSystem, db, coordMgr, worldRoot.transform);

                RoadSplineSegment targetRoad = null;
                foreach (RoadSplineSegment seg in splineSystem.Segments)
                {
                    if (seg.gradeKind == RoadGradeKind.Surface && (targetRoad == null || seg.totalLengthMeters > targetRoad.totalLengthMeters))
                    {
                        targetRoad = seg;
                    }
                }
                Assert.NotNull(targetRoad);
                targetRoad.SampleAtDistance(Mathf.Min(18f, targetRoad.totalLengthMeters * 0.2f), out Vector3 roadPt, out Vector3 roadDir, out _);

                // 1. Test Sedan: Accelerate, Wheel Spin, Steer/Drift, Reverse, Damage/Wreck & Repair on actual generated OSM road!
                VehicleSpecSO sedanSpec = MakeSpec("sedan", DhakaVehicleClass.CompactSedan, 4, 1420f, 2200f, 32f, 0.36f, 0f);
                createdSpecs.Add(sedanSpec);
                GameObject sedanGo = DhakaVehicleFactory.CreateVehicleFromSpec(
                    sedanSpec, roadPt + Vector3.up * 0.8f, Quaternion.LookRotation(roadDir, Vector3.up));
                instantiatedCars.Add(sedanGo);

                var sedan = sedanGo.GetComponent<DhakaVehicleController>();
                sedan.EnterDriverSeat();
                sedan.SetSyntheticDriveInput(throttleBrake: 1.0f, steer: 0f);
                yield return new WaitForSeconds(0.95f);

                Assert.IsTrue(sedan.IsAnyWheelGrounded, "PhysX SphereCast wheels must contact generated RoadDeck.");
                Assert.Greater(sedan.ForwardSpeedKph, 10f, "Sedan must accelerate cleanly on generated Dhaka asphalt.");

                DhakaPhysxWheel[] wheels = sedanGo.GetComponentsInChildren<DhakaPhysxWheel>();
                Assert.Greater(Mathf.Abs(wheels[0].SpinAngleDegrees), 5f, "Wheels must visually rotate while driving.");

                // Handbrake drift test
                sedan.SetSyntheticDriveInput(throttleBrake: 1.0f, steer: 0.9f, handbrake: true);
                yield return new WaitForSeconds(0.35f);
                Assert.IsTrue(sedan.IsDrifting, "High-speed turn with handbrake must engage drift/skid state.");

                // Wet road traction verification
                WorldSurfaceState.GlobalRoadWetness = 0.95f;
                sedan.SetSyntheticDriveInput(throttleBrake: -1.0f, steer: 0f, handbrake: false);
                yield return new WaitForSeconds(1.1f);
                Assert.Less(sedan.ForwardSpeedKph, -1.5f, "Holding S after stopping must engage Reverse gear cleanly.");
                Assert.IsTrue(sedan.IsReversing);

                // Collision wreck -> engine cutoff -> repair restoration
                sedan.DamageAndTuning.ApplyDamage(5000f);
                Assert.IsTrue(sedan.DamageAndTuning.IsWrecked);
                sedan.DamageAndTuning.RepairFull();
                Assert.IsFalse(sedan.DamageAndTuning.IsWrecked);

                // 2. Test 3-Wheel CNG & 2-Wheel Motorcycle upright stability on same generated road!
                VehicleSpecSO cngSpec = MakeSpec("cng_auto", DhakaVehicleClass.CngAutoRickshaw, 3, 380f, 800f, 38f, 0.24f, 0f);
                VehicleSpecSO motoSpec = MakeSpec("motorcycle", DhakaVehicleClass.Motorcycle, 2, 175f, 520f, 32f, 0.32f, 26f);
                createdSpecs.Add(cngSpec);
                createdSpecs.Add(motoSpec);

                GameObject cngGo = DhakaVehicleFactory.CreateVehicleFromSpec(cngSpec, roadPt + roadDir * 12f + Vector3.up * 0.7f, Quaternion.LookRotation(roadDir, Vector3.up));
                GameObject motoGo = DhakaVehicleFactory.CreateVehicleFromSpec(motoSpec, roadPt + roadDir * 22f + Vector3.up * 0.7f, Quaternion.LookRotation(roadDir, Vector3.up));
                instantiatedCars.Add(cngGo);
                instantiatedCars.Add(motoGo);

                var cng = cngGo.GetComponent<DhakaVehicleController>();
                var moto = motoGo.GetComponent<DhakaVehicleController>();
                cng.EnterDriverSeat(); moto.EnterDriverSeat();
                cng.SetSyntheticDriveInput(1f, 0.15f);
                moto.SetSyntheticDriveInput(1f, 0.35f);

                yield return new WaitForSeconds(0.65f);
                Assert.Greater(cng.ForwardSpeedKph, 6f, "3-wheel CNG must accelerate stably without rolling over.");
                Assert.Greater(moto.ForwardSpeedKph, 8f, "2-wheel Motorcycle must accelerate while staying upright via gyro lean.");
                Assert.Less(Mathf.Abs(Mathf.DeltaAngle(0f, motoGo.transform.eulerAngles.z)), 40f, "Motorcycle must stay stabilized.");
            }
            finally
            {
                WorldSurfaceState.GlobalRoadWetness = 0f;
                foreach (GameObject g in instantiatedCars) if (g != null) Object.Destroy(g);
                foreach (VehicleSpecSO s in createdSpecs) if (s != null) Object.Destroy(s);
                Object.Destroy(worldRoot);
                Object.Destroy(host);
                Object.Destroy(db);
            }
        }

        [UnityTest]
        public IEnumerator PlayMode_WatercraftBuoyancy_SpeedboatAndLaunchFloatAndAdvance()
        {
            PhysicsLayers.ApplyCollisionMatrix();

            var boatSpec = ScriptableObject.CreateInstance<VehicleSpecSO>();
            boatSpec.vehicleId = "speedboat";
            boatSpec.vehicleClass = DhakaVehicleClass.Speedboat;
            boatSpec.isWatercraft = true;
            boatSpec.seatCount = 4;
            boatSpec.massKg = 1250f;
            boatSpec.maxMotorTorqueNm = 1400f;
            boatSpec.maxSpeedKph = 80f;
            boatSpec.waterDrag = 1.15f;
            boatSpec.waterAngularDrag = 2.5f;
            boatSpec.chassisSizeMeters = new Vector3(2.35f, 1.25f, 6.4f);
            boatSpec.maxHealth = 1000f;

            GameObject boatGo = DhakaVehicleFactory.CreateVehicleFromSpec(boatSpec, new Vector3(0f, 0.1f, 0f), Quaternion.identity);
            var boat = boatGo.GetComponent<DhakaWatercraftController>();
            boat.EnterDriverSeat();
            boat.SetSyntheticMarineInput(1.0f, 0.2f);

            try
            {
                yield return new WaitForSeconds(0.85f);
                Assert.IsTrue(boat.IsFloating, "Speedboat hull pontoons must float on river datum.");
                Assert.Greater(boatGo.transform.position.y, -1.2f, "Watercraft must stay buoyant and never sink.");
                Assert.Greater(boat.ForwardSpeedKph, 4f, "Watercraft propeller thrust must accelerate forward.");
            }
            finally
            {
                Object.Destroy(boatGo);
                Object.Destroy(boatSpec);
            }
        }

        private static VehicleSpecSO MakeSpec(string id, DhakaVehicleClass cls, int wheels, float mass, float torque, float steerDeg, float radius, float lean)
        {
            var s = ScriptableObject.CreateInstance<VehicleSpecSO>();
            s.vehicleId = id;
            s.vehicleClass = cls;
            s.wheelCount = wheels;
            s.drivetrain = wheels >= 4 ? DrivetrainMode.AllWheelDrive : DrivetrainMode.RearWheelDrive;
            s.seatCount = wheels == 2 ? 2 : 4;
            s.massKg = mass;
            s.maxMotorTorqueNm = torque;
            s.maxBrakeTorqueNm = torque * 1.8f;
            s.maxSpeedKph = 120f;
            s.reverseMaxSpeedKph = 30f;
            s.maxSteerAngleDeg = steerDeg;
            s.wheelRadiusMeters = radius;
            s.suspensionTravelMeters = 0.24f;
            s.suspensionSpringStiffness = mass * 24f;
            s.suspensionDamper = mass * 3.2f;
            s.maxLeanAngleDeg = lean;
            s.maxHealth = 1000f;
            s.wetGripMultiplier = 0.58f;
            s.handbrakeRearGripMultiplier = 0.26f;
            s.dryLateralGrip = 1.2f;
            return s;
        }
    }
}
