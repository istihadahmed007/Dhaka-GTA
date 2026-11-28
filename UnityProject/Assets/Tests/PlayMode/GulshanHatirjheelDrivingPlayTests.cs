using System.Collections;
using System.IO;
using DhakaGta.Core;
using DhakaGta.GIS;
using DhakaGta.Vehicles;
using DhakaGta.WorldGen;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DhakaGta.Tests.PlayMode
{
    public sealed class GulshanHatirjheelDrivingPlayTests
    {
        [UnityTest]
        public IEnumerator PlayMode_DriveVehicleAcrossGeneratedGulshanHatirjheelRoads_MaintainsGroundContactAndMoves()
        {
            PhysicsLayers.ApplyCollisionMatrix();

            string geoJsonPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "GeographicData", "dhaka_gulshan_hatirjheel.geojson"));
            Assert.IsTrue(File.Exists(geoJsonPath), "Expected Gulshan-Hatirjheel GeoJSON at: " + geoJsonPath);

            var db = ScriptableObject.CreateInstance<GeoFeatureDatabase>();
            Assert.IsTrue(DhakaGeoImporter.ImportGeoJsonFileIntoDatabase(geoJsonPath, db));

            var host = new GameObject("PlayTest_WorldGenHost");
            var worldRoot = new GameObject("PlayTest_GulshanWorldRoot");
            GameObject carGo = null;

            try
            {
                var coordMgr = host.AddComponent<WorldCoordinateManager>();
                coordMgr.Configure(db, GeographicCoordinateConverter.ShaplaOriginLat, GeographicCoordinateConverter.ShaplaOriginLon, 0.0, null);

                var splineSystem = host.AddComponent<RoadSplineSystem>();
                // Focus around Hatirjheel / Gulshan 1 corridor (within 950m radius of 23.7745, 90.4138)
                UtmCoord gulshanOrigin = GeographicCoordinateConverter.LatLonToUtm(23.7745, 90.4138);
                coordMgr.Configure(db, 23.7745, 90.4138, 0.0, null);
                splineSystem.BuildFromDatabase(db, coordMgr, maxRadiusMeters: 950.0);
                Assert.Greater(splineSystem.Segments.Count, 10);

                var intersectionGen = host.AddComponent<IntersectionGenerator>();
                intersectionGen.Generate(splineSystem, db, coordMgr, worldRoot.transform);

                var roadGen = host.AddComponent<RoadGenerator>();
                RoadGenerator.RoadGenStats roadStats = roadGen.BuildNetworkAndWater(splineSystem, db, coordMgr, worldRoot.transform);
                Assert.Greater(roadStats.RoadMeshCount, 10);

                // Pick longest surface primary/secondary road near origin for clean driving trajectory
                RoadSplineSegment targetRoad = null;
                foreach (RoadSplineSegment seg in splineSystem.Segments)
                {
                    if (seg.gradeKind == RoadGradeKind.Surface && (targetRoad == null || seg.totalLengthMeters > targetRoad.totalLengthMeters))
                    {
                        targetRoad = seg;
                    }
                }
                Assert.NotNull(targetRoad);

                targetRoad.SampleAtDistance(Mathf.Min(15f, targetRoad.totalLengthMeters * 0.25f), out Vector3 roadPoint, out Vector3 roadTangent, out _);

                carGo = CreatePlayTestCar(roadPoint + Vector3.up * 0.7f, roadTangent);
                var car = carGo.GetComponent<DhakaWheelVehicleController>();
                car.SetSyntheticDriveInput(throttleBrake: 1.0f, steer: 0.08f, handbrake: false);

                Vector3 startPos = carGo.transform.position;
                yield return new WaitForSeconds(0.9f);

                Assert.IsTrue(car.IsAnyWheelGrounded, "At least one WheelCollider must contact the generated RoadDeck MeshCollider.");
                Assert.Greater(car.ForwardSpeedKph, 8.0f, "Car must accelerate along the generated OSM road surface.");
                Assert.Greater(Vector3.Distance(startPos, carGo.transform.position), 2.5f, "Car must displace cleanly across generated road segments.");
                Assert.Greater(carGo.transform.position.y, -2.0f, "Car must not fall through generated road MeshCollider seams.");
                Assert.IsFalse(float.IsNaN(carGo.transform.position.x));
                _ = gulshanOrigin;
            }
            finally
            {
                if (carGo != null) Object.Destroy(carGo);
                Object.Destroy(worldRoot);
                Object.Destroy(host);
                Object.Destroy(db);
            }
        }

        private static GameObject CreatePlayTestCar(Vector3 pos, Vector3 forward)
        {
            var carGo = new GameObject("TestCar");
            carGo.layer = PhysicsLayers.Vehicle;
            carGo.transform.position = pos;
            carGo.transform.rotation = Quaternion.LookRotation(forward.sqrMagnitude > 0.01f ? forward : Vector3.forward, Vector3.up);

            var rb = carGo.AddComponent<Rigidbody>();
            rb.mass = 1420f;
            var box = carGo.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.65f, 0f);
            box.size = new Vector3(1.8f, 0.9f, 4.2f);

            WheelCollider fl = AddWheel(carGo.transform, new Vector3(-0.82f, 0.36f, 1.35f));
            WheelCollider fr = AddWheel(carGo.transform, new Vector3(0.82f, 0.36f, 1.35f));
            WheelCollider rl = AddWheel(carGo.transform, new Vector3(-0.82f, 0.36f, -1.35f));
            WheelCollider rr = AddWheel(carGo.transform, new Vector3(0.82f, 0.36f, -1.35f));

            var ctrl = carGo.AddComponent<DhakaWheelVehicleController>();
            ctrl.ConfigureWheels(null, fl, fr, rl, rr);
            return carGo;
        }

        private static WheelCollider AddWheel(Transform parent, Vector3 localPos)
        {
            var go = new GameObject("Wheel");
            go.layer = PhysicsLayers.VehicleWheel;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var wc = go.AddComponent<WheelCollider>();
            wc.radius = 0.36f;
            wc.suspensionDistance = 0.22f;
            return wc;
        }
    }
}
