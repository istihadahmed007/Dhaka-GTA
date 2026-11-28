using System.Collections;
using DhakaGta.Core;
using DhakaGta.Data;
using DhakaGta.Environment;
using DhakaGta.GIS;
using DhakaGta.TrafficAI;
using DhakaGta.World;
using DhakaGta.WorldGen;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DhakaGta.Tests.PlayMode
{
    public sealed class TrafficAndNpcAiPlayTests
    {
        [UnityTest]
        public IEnumerator PlayMode_LivingTrafficSignalsPedestriansAndPolice_OperateTogetherCleanly()
        {
            CityDangerBus.ClearAllForReset();
            PhysicsLayers.ApplyCollisionMatrix();

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "CityAiTestFloor";
            ground.layer = PhysicsLayers.Ground;
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(320f, 1f, 320f);

            var systemsGo = new GameObject("AiTestSystems");
            var teaStallGo = new GameObject("Tong_TeaStall");
            teaStallGo.transform.position = new Vector3(9f, 0f, 10f);
            var teaProp = teaStallGo.AddComponent<InteractiveEnvironmentProp>();
            teaProp.Configure(InteractableKind.TeaStall, "Gulshan Tong Cha", "[E] Tea");

            var focusProxy = new GameObject("FocusProxy").transform;
            focusProxy.position = Vector3.zero;

            // Construct real crossed GeoRoadFeature database -> RoadSplineSystem & IntersectionGenerator
            var db = ScriptableObject.CreateInstance<GeoFeatureDatabase>();
            var origins = systemsGo.AddComponent<WorldCoordinateManager>();
            var originUtm = GeographicCoordinateConverter.LatLonToUtm(
                GeographicCoordinateConverter.ShaplaOriginLat,
                GeographicCoordinateConverter.ShaplaOriginLon);
            origins.Configure(db, GeographicCoordinateConverter.ShaplaOriginLat, GeographicCoordinateConverter.ShaplaOriginLon, 0.0, null);

            db.roads.Add(new GeoRoadFeature
            {
                osmId = "test_ave_ns",
                roadName = "Gulshan Avenue",
                highwayClass = "primary",
                lanes = 4,
                widthMeters = 12f,
                hasSidewalk = true,
                maxSpeedKph = 50f,
                nodeIds = new long[] { 101, 102, 103 },
                pointsUtm = new[]
                {
                    new UtmCoord(originUtm.Easting, originUtm.Northing - 110.0),
                    new UtmCoord(originUtm.Easting, originUtm.Northing),
                    new UtmCoord(originUtm.Easting, originUtm.Northing + 110.0)
                }
            });
            db.roads.Add(new GeoRoadFeature
            {
                osmId = "test_rd_ew",
                roadName = "Kemal Ataturk Road",
                highwayClass = "primary",
                lanes = 2,
                widthMeters = 9f,
                hasSidewalk = true,
                maxSpeedKph = 45f,
                nodeIds = new long[] { 201, 102, 203 },
                pointsUtm = new[]
                {
                    new UtmCoord(originUtm.Easting - 110.0, originUtm.Northing),
                    new UtmCoord(originUtm.Easting, originUtm.Northing),
                    new UtmCoord(originUtm.Easting + 110.0, originUtm.Northing)
                }
            });
            db.trafficSignals.Add(new GeoTrafficSignalFeature
            {
                osmId = "sig_center",
                coordinateUtm = originUtm
            });

            var splineSys = systemsGo.AddComponent<RoadSplineSystem>();
            splineSys.BuildFromDatabase(db, origins);

            var worldRoot = new GameObject("TestWorldRoot");
            var intersectionGen = systemsGo.AddComponent<IntersectionGenerator>();
            intersectionGen.Generate(splineSys, db, origins, worldRoot.transform);

            VehicleSpecSO MakeSpec(string id, DhakaVehicleClass vc, float speed)
            {
                var s = ScriptableObject.CreateInstance<VehicleSpecSO>();
                s.vehicleId = id;
                s.vehicleClass = vc;
                s.maxSpeedKph = speed;
                s.massKg = vc == DhakaVehicleClass.CityBus ? 9000f : 1200f;
                s.wheelRadiusMeters = 0.34f;
                s.seatCount = 4;
                s.wheelCount = vc == DhakaVehicleClass.Motorcycle ? 2 : 4;
                s.chassisSizeMeters = new Vector3(1.8f, 1.3f, 4.1f);
                return s;
            }

            VehicleSpecSO[] specs =
            {
                MakeSpec("cng", DhakaVehicleClass.CngAutoRickshaw, 48f),
                MakeSpec("rickshaw", DhakaVehicleClass.CycleRickshaw, 20f),
                MakeSpec("bus", DhakaVehicleClass.CityBus, 55f),
                MakeSpec("sedan", DhakaVehicleClass.CompactSedan, 95f),
                MakeSpec("bike", DhakaVehicleClass.Motorcycle, 105f),
                MakeSpec("police", DhakaVehicleClass.DmpPoliceCruiser, 130f)
            };

            var sim = systemsGo.AddComponent<DhakaCityAiSimulationManager>();

            try
            {
                WorldSurfaceState.CurrentTimeOfDayHours = 18.0f; // Evening Dhaka rush hour
                sim.ConfigureAndWarmPools(splineSys, intersectionGen, focusProxy, specs, "gulshan", null, bakeNavMesh: false);

                Assert.Greater(sim.CountActiveMovingVehicles(), 0, "Moving AI traffic must spawn cleanly.");
                Assert.Greater(sim.CountActiveParkedVehicles(), 0, "Parked curbside vehicles must spawn cleanly.");
                Assert.Greater(sim.CountActivePedestrians(), 0, "Bangladeshi pedestrians and vendors must spawn cleanly.");
                Assert.Greater(sim.SignalNodes.Count, 0, "Intersection signals must attach to real OSM junction.");

                // Record baseline positions & gait phases
                Vector3 initialVehPos = sim.ActiveVehicles[sim.CountActiveParkedVehicles()].transform.position;
                // Pick an active walking pedestrian inside the near-LOD band so gait animation ticks every frame
                DhakaPedestrianAgent sampleWalker = null;
                float bestWalkerDist = 45f;
                foreach (var p in sim.ActivePedestrians)
                {
                    if (p != null && p.gameObject.activeSelf && p.BehaviorState == NpcBehaviorState.WalkingSidewalk)
                    {
                        float d = Vector3.Distance(focusProxy.position, p.transform.position);
                        if (d < bestWalkerDist)
                        {
                            bestWalkerDist = d;
                            sampleWalker = p;
                        }
                    }
                }
                if (sampleWalker == null)
                {
                    foreach (var p in sim.ActivePedestrians)
                    {
                        if (p != null && p.gameObject.activeSelf && p.BehaviorState == NpcBehaviorState.WalkingSidewalk)
                        {
                            p.transform.position = new Vector3(4f, 0f, 4f);
                            sampleWalker = p;
                            break;
                        }
                    }
                }
                Assert.NotNull(sampleWalker, "Expected active Bangladeshi pedestrian near city focus.");
                Vector3 initialPedPos = sampleWalker.transform.position;

                // Simulate 0.45s of city traffic & pedestrian movement
                float elapsed = 0f;
                while (elapsed < 0.45f)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                Vector3 finalVehPos = sim.ActiveVehicles[sim.CountActiveParkedVehicles()].transform.position;
                Assert.Greater(Vector3.Distance(initialVehPos, finalVehPos), 0.1f, "AI traffic vehicle must physically navigate along road spline.");
                Assert.Greater(Vector3.Distance(initialPedPos, sampleWalker.transform.position), 0.08f, "Pedestrian must walk cleanly.");
                Assert.Greater(sampleWalker.GaitPhaseRadians, 0f, "Pedestrian joint gait animation must advance smoothly.");

                // Fire Urban Gunshot Danger Event near center -> Verify Fleeing civilians + DMP Police Pursuit!
                CityDangerBus.RaiseDangerEvent(CityDangerKind.Gunshot, sampleWalker.transform.position + Vector3.forward * 3f, 65f, 1.5f, null, escalateWanted: true);
                yield return null;
                yield return null;

                Assert.AreEqual(NpcBehaviorState.FleeingDanger, sampleWalker.BehaviorState, "Nearby pedestrian must flee on gunshot danger event.");
                Assert.IsTrue(sim.PoliceDispatch.IsPoliceAlertActive, "DMP Police dispatch must activate on escalated gunshot crime.");
                Assert.Greater(sim.PoliceDispatch.ActiveRespondingCruiserCount, 0, "At least one DMP Police Cruiser must respond with sirens.");
            }
            finally
            {
                CityDangerBus.ClearAllForReset();
                Object.Destroy(worldRoot);
                Object.Destroy(systemsGo);
                Object.Destroy(teaStallGo);
                Object.Destroy(focusProxy.gameObject);
                Object.Destroy(ground);
                Object.Destroy(db);
                foreach (var s in specs) Object.Destroy(s);
            }
        }
    }
}
