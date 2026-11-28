using System;
using System.IO;
using DhakaGta.GIS;
using NUnit.Framework;
using UnityEngine;

namespace DhakaGta.Tests.EditMode
{
    public sealed class GeographicValidationTests
    {
        // Real verified Dhaka WGS84 anchor coordinates
        private const double ShaplaLat = 23.733015;
        private const double ShaplaLon = 90.417014;
        private const double AhsanManzilLat = 23.708600;
        private const double AhsanManzilLon = 90.406000;
        private const double SadarghatLat = 23.705200;
        private const double SadarghatLon = 90.413500;
        private const double LalbaghLat = 23.719600;
        private const double LalbaghLon = 90.388100;
        private const double UttaraNorthMrtLat = 23.869000;
        private const double UttaraNorthMrtLon = 90.396500;

        [Test]
        public void UtmZone46N_ForwardAndInverseRoundTrip_AreSubMillimeterAccurate()
        {
            Assert.AreEqual(46, GeographicCoordinateConverter.GetUtmZoneNumber(ShaplaLat, ShaplaLon));

            var samples = new[]
            {
                new GeoLatLon(ShaplaLat, ShaplaLon, 0.0),
                new GeoLatLon(AhsanManzilLat, AhsanManzilLon, 0.0),
                new GeoLatLon(LalbaghLat, LalbaghLon, 0.0),
                new GeoLatLon(UttaraNorthMrtLat, UttaraNorthMrtLon, 12.0),
            };

            foreach (GeoLatLon sample in samples)
            {
                UtmCoord utm = GeographicCoordinateConverter.LatLonToUtm(sample.latitude, sample.longitude);
                Assert.AreEqual(46, utm.zone);
                Assert.IsTrue(utm.isNorthernHemisphere);
                Assert.Greater(utm.easting, 200000.0);
                Assert.Less(utm.easting, 300000.0);
                Assert.Greater(utm.northing, 2600000.0);

                GeoLatLon roundTrip = GeographicCoordinateConverter.UtmToLatLon(utm, sample.elevationMeters);
                Assert.That(roundTrip.latitude, Is.EqualTo(sample.latitude).Within(1e-7));
                Assert.That(roundTrip.longitude, Is.EqualTo(sample.longitude).Within(1e-7));
            }
        }

        [Test]
        public void OneToOneUnityScale_PreservesRealWorldDhakaDistances_WithoutCityCompression()
        {
            UtmCoord shaplaUtm = GeographicCoordinateConverter.LatLonToUtm(ShaplaLat, ShaplaLon);
            UtmCoord ahsanUtm = GeographicCoordinateConverter.LatLonToUtm(AhsanManzilLat, AhsanManzilLon);
            UtmCoord sadarghatUtm = GeographicCoordinateConverter.LatLonToUtm(SadarghatLat, SadarghatLon);
            UtmCoord uttaraUtm = GeographicCoordinateConverter.LatLonToUtm(UttaraNorthMrtLat, UttaraNorthMrtLon);

            Vector3 shaplaPos = GeographicCoordinateConverter.UtmToUnityLocal(shaplaUtm, shaplaUtm);
            Vector3 ahsanPos = GeographicCoordinateConverter.UtmToUnityLocal(ahsanUtm, shaplaUtm);
            Vector3 sadarghatPos = GeographicCoordinateConverter.UtmToUnityLocal(sadarghatUtm, shaplaUtm);
            Vector3 uttaraPos = GeographicCoordinateConverter.UtmToUnityLocal(uttaraUtm, shaplaUtm);

            double geodesicAhsan = GeographicCoordinateConverter.ComputeGeodesicDistanceMeters(
                ShaplaLat, ShaplaLon, AhsanManzilLat, AhsanManzilLon);
            float unityDistAhsan = Vector3.Distance(shaplaPos, ahsanPos);
            Assert.That(unityDistAhsan, Is.EqualTo((float)geodesicAhsan).Within(3.0f));
            Assert.Greater(unityDistAhsan, 2500f, "Must never compress Dhaka distances like legacy 0.085 scale.");

            double geodesicSadarghat = GeographicCoordinateConverter.ComputeGeodesicDistanceMeters(
                ShaplaLat, ShaplaLon, SadarghatLat, SadarghatLon);
            float unityDistSadarghat = Vector3.Distance(shaplaPos, sadarghatPos);
            Assert.That(unityDistSadarghat, Is.EqualTo((float)geodesicSadarghat).Within(3.0f));

            double geodesicUttara = GeographicCoordinateConverter.ComputeGeodesicDistanceMeters(
                ShaplaLat, ShaplaLon, UttaraNorthMrtLat, UttaraNorthMrtLon);
            float unityDistUttara = Vector3.Distance(shaplaPos, uttaraPos);
            Assert.That(unityDistUttara, Is.EqualTo((float)geodesicUttara).Within(10.0f));
            Assert.Greater(unityDistUttara, 14500f);

            // Verify East-Up-North Unity conventions (+X = East, +Z = North)
            Assert.Less(ahsanPos.x, -500f, "Ahsan Manzil lies west of Shapla Chottor (-X).");
            Assert.Less(ahsanPos.z, -2000f, "Ahsan Manzil lies south of Shapla Chottor (-Z).");
            Assert.Greater(uttaraPos.z, 14000f, "Uttara North lies ~15km north of Shapla Chottor (+Z).");
        }

        [Test]
        public void WorldCoordinateManager_FloatingOriginShift_KeepsGeographicCoordinatesInvariant()
        {
            var host = new GameObject("TestCoordManager");
            var worldRoot = new GameObject("TestWorldRoot");
            var landmarkDummy = new GameObject("UttaraAnchor");

            try
            {
                var mgr = host.AddComponent<WorldCoordinateManager>();
                mgr.Configure(null, ShaplaLat, ShaplaLon, 0.0, null);
                mgr.RegisterFloatingRoot(worldRoot.transform);

                Vector3 uttaraBefore = mgr.LatLonToUnityPosition(UttaraNorthMrtLat, UttaraNorthMrtLon, 11.5);
                landmarkDummy.transform.SetParent(worldRoot.transform, false);
                landmarkDummy.transform.position = uttaraBefore;

                // Shift local origin 15km north toward Uttara so local transforms regain full float32 precision
                mgr.ShiftOriginByLocalDelta(uttaraBefore.x, uttaraBefore.z);

                Assert.That(landmarkDummy.transform.position.x, Is.EqualTo(0f).Within(0.01f));
                Assert.That(landmarkDummy.transform.position.z, Is.EqualTo(0f).Within(0.01f));
                Assert.That(landmarkDummy.transform.position.y, Is.EqualTo(11.5f).Within(0.001f));

                GeoLatLon recovered = mgr.UnityPositionToLatLon(landmarkDummy.transform.position);
                Assert.That(recovered.latitude, Is.EqualTo(UttaraNorthMrtLat).Within(1e-5));
                Assert.That(recovered.longitude, Is.EqualTo(UttaraNorthMrtLon).Within(1e-5));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(worldRoot);
            }
        }

        [Test]
        public void DhakaGeoImporter_ImportsRealOpenStreetMapExtract_WithAllRequiredFeatureLayers()
        {
            string geoJsonPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "GeographicData", "dhaka_osm_extract.geojson"));
            Assert.IsTrue(File.Exists(geoJsonPath), "Expected authentic OSM GeoJSON extract at: " + geoJsonPath);

            var db = ScriptableObject.CreateInstance<GeoFeatureDatabase>();
            try
            {
                bool ok = DhakaGeoImporter.ImportGeoJsonFileIntoDatabase(geoJsonPath, db);
                Assert.IsTrue(ok, "DhakaGeoImporter failed to import dhaka_osm_extract.geojson.");

                Assert.AreEqual(46, db.utmZone);
                Assert.Greater(db.roads.Count, 100, "Expected >100 real Dhaka OSM road ways.");
                Assert.Greater(db.buildings.Count, 100, "Expected >100 real Dhaka building footprints.");
                Assert.Greater(db.waterBodies.Count, 10, "Expected real Dhaka river/lake water features.");
                Assert.Greater(db.landmarks.Count, 10, "Expected real Dhaka landmarks.");
                Assert.Greater(db.metroAndRailways.Count, 10, "Expected real Dhaka MRT-6 and railway features.");

                GeoLandmarkFeature ahsan = db.FindLandmarkByName("Ahsan Manzil");
                Assert.NotNull(ahsan, "Ahsan Manzil must be present in imported OSM landmarks.");
                Assert.IsNotEmpty(ahsan.osmId);
                Assert.Greater(ahsan.metadata.Length, 0, "OSM metadata tags must be preserved.");

                double distMeters = db.worldOriginUtm.Distance2D(ahsan.coordinateUtm);
                Assert.Greater(distMeters, 2400.0);
                Assert.Less(distMeters, 3400.0);

                // Verify elevated MRT-6 track elevation preservation
                bool foundElevatedMrt = false;
                foreach (GeoMetroRailFeature rail in db.metroAndRailways)
                {
                    if (!rail.isStationPoint && rail.elevationMeters > 5f)
                    {
                        foundElevatedMrt = true;
                        break;
                    }
                }
                Assert.IsTrue(foundElevatedMrt, "Expected elevated MRT-6 viaduct tracks at >5m elevation.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(db);
            }
        }
    }
}
