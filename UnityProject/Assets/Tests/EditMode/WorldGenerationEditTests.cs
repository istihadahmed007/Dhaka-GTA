using System.Collections.Generic;
using System.IO;
using DhakaGta.GIS;
using DhakaGta.WorldGen;
using NUnit.Framework;
using UnityEngine;

namespace DhakaGta.Tests.EditMode
{
    public sealed class WorldGenerationEditTests
    {
        [Test]
        public void WorldRegionLoader_ContainsAllTwelveDhakaDistricts_AndResolvesAccurately()
        {
            Assert.AreEqual(12, WorldRegionLoader.AllTwelveDistricts.Length);

            var expectedIds = new HashSet<string>
            {
                "old_dhaka", "chawkbazar", "sadarghat", "motijheel", "shapla_chottor", "dhanmondi",
                "kawran_bazar", "gulshan", "banani", "hatirjheel", "mirpur", "uttara"
            };

            foreach (DhakaDistrictDescriptor desc in WorldRegionLoader.AllTwelveDistricts)
            {
                Assert.IsTrue(expectedIds.Remove(desc.districtId), "Unexpected district ID: " + desc.districtId);
                Assert.IsNotEmpty(desc.additiveSceneName);
                Assert.IsNotEmpty(desc.addressableGroupKey);
            }
            Assert.AreEqual(0, expectedIds.Count);

            UtmCoord gulshanUtm = GeographicCoordinateConverter.LatLonToUtm(23.7855, 90.4158);
            Assert.AreEqual("gulshan", WorldRegionLoader.ResolveDistrictAtUtm(gulshanUtm).districtId);

            UtmCoord hatirjheelUtm = GeographicCoordinateConverter.LatLonToUtm(23.7678, 90.4110);
            Assert.AreEqual("hatirjheel", WorldRegionLoader.ResolveDistrictAtUtm(hatirjheelUtm).districtId);

            UtmCoord chawkbazarUtm = GeographicCoordinateConverter.LatLonToUtm(23.7168, 90.3952);
            Assert.AreEqual("chawkbazar", WorldRegionLoader.ResolveDistrictAtUtm(chawkbazarUtm).districtId);
        }

        [Test]
        public void GulshanHatirjheelOsmExtract_GeneratesConnectedNonGridRoadsJunctionsRoundaboutsBridgesWaterAndBuildings()
        {
            string geoJsonPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "GeographicData", "dhaka_gulshan_hatirjheel.geojson"));
            Assert.IsTrue(File.Exists(geoJsonPath), "Expected Gulshan-Hatirjheel OSM GeoJSON at: " + geoJsonPath);

            var db = ScriptableObject.CreateInstance<GeoFeatureDatabase>();
            bool loaded = DhakaGeoImporter.ImportGeoJsonFileIntoDatabase(geoJsonPath, db);
            Assert.IsTrue(loaded);
            Assert.Greater(db.roads.Count, 400);
            Assert.Greater(db.waterBodies.Count, 15);
            Assert.Greater(db.buildings.Count, 500);
            Assert.Greater(db.trafficSignals.Count, 5);

            var host = new GameObject("TestWorldGenHost");
            var worldRoot = new GameObject("TestWorldRoot");
            try
            {
                var coordMgr = host.AddComponent<WorldCoordinateManager>();
                coordMgr.Configure(db, GeographicCoordinateConverter.ShaplaOriginLat, GeographicCoordinateConverter.ShaplaOriginLon, 0.0);

                var splineSystem = host.AddComponent<RoadSplineSystem>();
                splineSystem.BuildFromDatabase(db, coordMgr, maxRadiusMeters: 0.0);
                Assert.Greater(splineSystem.Segments.Count, 400);

                // Verify real diagonal/curved angles exist (never a synthetic 0/90 rectangular grid)
                bool hasDiagonalHeading = false;
                bool hasBridgeOrFlyover = false;
                bool hasUnderpass = false;
                foreach (RoadSplineSegment seg in splineSystem.Segments)
                {
                    if (seg.gradeKind == RoadGradeKind.Bridge || seg.gradeKind == RoadGradeKind.Flyover) hasBridgeOrFlyover = true;
                    if (seg.gradeKind == RoadGradeKind.Underpass) hasUnderpass = true;
                    if (seg.sampledTangents.Length > 0)
                    {
                        Vector3 t = seg.sampledTangents[0];
                        float dotN = Mathf.Abs(t.z);
                        float dotE = Mathf.Abs(t.x);
                        if (dotN > 0.25f && dotE > 0.25f) hasDiagonalHeading = true;
                    }
                }
                Assert.IsTrue(hasDiagonalHeading, "Road segments must follow real curved/diagonal OSM alignments.");
                Assert.IsTrue(hasBridgeOrFlyover, "Expected Hatirjheel/Banani bridges or flyovers in OSM extract.");
                Assert.IsTrue(hasUnderpass, "Expected OSM underpass/tunnel segment in Gulshan-Hatirjheel extract.");

                var intersectionGen = host.AddComponent<IntersectionGenerator>();
                intersectionGen.Generate(splineSystem, db, coordMgr, worldRoot.transform);
                Assert.Greater(intersectionGen.Junctions.Count, 20, "Expected real multi-arm intersections & roundabouts.");
                Assert.Greater(intersectionGen.NavEdges.Count, 400, "Expected vehicle navigation graph edges.");

                var roadGen = host.AddComponent<RoadGenerator>();
                RoadGenerator.RoadGenStats roadStats = roadGen.BuildNetworkAndWater(splineSystem, db, coordMgr, worldRoot.transform);
                Assert.AreEqual(splineSystem.Segments.Count, roadStats.RoadMeshCount);
                Assert.Greater(roadStats.WaterMeshesCount, 10);
                Assert.Greater(roadStats.SidewalkSegments, 100);

                var chunkMgr = host.AddComponent<WorldChunkManager>();
                var bldgSys = host.AddComponent<BuildingPlacementSystem>();
                BuildingPlacementSystem.BuildingGenStats bldgStats = bldgSys.PlaceBuildings(db, coordMgr, splineSystem, worldRoot.transform, null, 0.0, chunkMgr);
                Assert.Greater(bldgStats.PlacedCount, 300);
                Assert.Greater(chunkMgr.ActiveChunkCount, 4);
            }
            finally
            {
                Object.DestroyImmediate(worldRoot);
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(db);
            }
        }
    }
}
