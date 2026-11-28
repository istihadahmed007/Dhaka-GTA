using System;
using System.Collections.Generic;
using UnityEngine;

namespace DhakaGta.GIS
{
    public enum GeoFeatureCategory
    {
        Road = 0,
        Building = 1,
        Water = 2,
        Landmark = 3,
        MetroRailway = 4,
    }

    [Serializable]
    public struct GeoTagPair
    {
        public string key;
        public string value;
    }

    [Serializable]
    public sealed class GeoRoadFeature
    {
        public string osmId;
        public string name;
        public string banglaName;
        public string highwayClass;
        public string junctionType;
        public string surface;
        public bool isBridge;
        public bool isFlyover;
        public bool isUnderpass;
        public bool isOneWay;
        public bool hasSidewalk = true;
        public int osmLayer;
        public int lanes = 2;
        public float widthMeters = 8f;
        public float maxSpeedKph = 50f;
        public long[] osmNodeIds = Array.Empty<long>();
        public UtmCoord[] pointsUtm = Array.Empty<UtmCoord>();
        public GeoLatLon[] pointsWgs84 = Array.Empty<GeoLatLon>();
        public GeoTagPair[] metadata = Array.Empty<GeoTagPair>();
    }

    [Serializable]
    public sealed class GeoTrafficSignalFeature
    {
        public string osmId;
        public string name;
        public long osmNodeId;
        public GeoLatLon coordinateWgs84;
        public UtmCoord coordinateUtm;
        public GeoTagPair[] metadata = Array.Empty<GeoTagPair>();
    }

    [Serializable]
    public sealed class GeoBuildingFeature
    {
        public string osmId;
        public string name;
        public string banglaName;
        public string buildingType;
        public int levels = 3;
        public float heightMeters = 10.5f;
        public UtmCoord centroidUtm;
        public UtmCoord[] footprintUtm = Array.Empty<UtmCoord>();
        public GeoLatLon[] footprintWgs84 = Array.Empty<GeoLatLon>();
        public GeoTagPair[] metadata = Array.Empty<GeoTagPair>();
    }

    [Serializable]
    public sealed class GeoWaterFeature
    {
        public string osmId;
        public string name;
        public string banglaName;
        public string waterKind;
        public bool isPolygon;
        public float channelWidthMeters = 35f;
        public UtmCoord[] pointsUtm = Array.Empty<UtmCoord>();
        public GeoLatLon[] pointsWgs84 = Array.Empty<GeoLatLon>();
        public GeoTagPair[] metadata = Array.Empty<GeoTagPair>();
    }

    [Serializable]
    public sealed class GeoLandmarkFeature
    {
        public string osmId;
        public string name;
        public string banglaName;
        public string kind;
        public string architecturalStyle;
        public GeoLatLon coordinateWgs84;
        public UtmCoord coordinateUtm;
        public GeoTagPair[] metadata = Array.Empty<GeoTagPair>();
    }

    [Serializable]
    public sealed class GeoMetroRailFeature
    {
        public string osmId;
        public string name;
        public string banglaName;
        public string railwayType;
        public bool isStationPoint;
        public float elevationMeters = 11.5f;
        public float trackWidthMeters = 7.5f;
        public UtmCoord[] pointsUtm = Array.Empty<UtmCoord>();
        public GeoLatLon[] pointsWgs84 = Array.Empty<GeoLatLon>();
        public GeoTagPair[] metadata = Array.Empty<GeoTagPair>();
    }

    /// <summary>
    /// Stores real-scale OpenStreetMap / GeoJSON urban geometry, landmarks, waterways, transit lines,
    /// and metadata in double-precision UTM Zone 46N (EPSG:32646).
    /// </summary>
    [CreateAssetMenu(fileName = "DhakaGeoFeatureDatabase", menuName = "Dhaka-GTA/GIS/Geo Feature Database")]
    public sealed class GeoFeatureDatabase : ScriptableObject
    {
        [Header("Projection & Attribution")]
        public string sourceAttribution = "Map data (c) OpenStreetMap contributors (ODbL).";
        public string projectionEpsg = "EPSG:32646 (WGS 84 / UTM Zone 46N)";
        public int utmZone = GeographicCoordinateConverter.DhakaUtmZone;
        public GeoLatLon worldOriginWgs84 = new GeoLatLon(
            GeographicCoordinateConverter.ShaplaOriginLat,
            GeographicCoordinateConverter.ShaplaOriginLon,
            0.0);
        public UtmCoord worldOriginUtm;

        [Header("Double-Precision Dataset Bounds (UTM Meters)")]
        public double minEasting;
        public double maxEasting;
        public double minNorthing;
        public double maxNorthing;

        [Header("Extracted Geo Features")]
        public List<GeoRoadFeature> roads = new List<GeoRoadFeature>();
        public List<GeoBuildingFeature> buildings = new List<GeoBuildingFeature>();
        public List<GeoWaterFeature> waterBodies = new List<GeoWaterFeature>();
        public List<GeoLandmarkFeature> landmarks = new List<GeoLandmarkFeature>();
        public List<GeoMetroRailFeature> metroAndRailways = new List<GeoMetroRailFeature>();
        public List<GeoTrafficSignalFeature> trafficSignals = new List<GeoTrafficSignalFeature>();

        public int TotalFeatureCount =>
            roads.Count + buildings.Count + waterBodies.Count + landmarks.Count + metroAndRailways.Count + trafficSignals.Count;

        public void ClearAll()
        {
            roads.Clear();
            buildings.Clear();
            waterBodies.Clear();
            landmarks.Clear();
            metroAndRailways.Clear();
            trafficSignals.Clear();
            minEasting = double.MaxValue;
            maxEasting = double.MinValue;
            minNorthing = double.MaxValue;
            maxNorthing = double.MinValue;
        }

        public void ExpandBounds(in UtmCoord pt)
        {
            if (pt.easting < minEasting) minEasting = pt.easting;
            if (pt.easting > maxEasting) maxEasting = pt.easting;
            if (pt.northing < minNorthing) minNorthing = pt.northing;
            if (pt.northing > maxNorthing) maxNorthing = pt.northing;
        }

        public GeoLandmarkFeature FindLandmarkByName(string nameSubstring)
        {
            if (string.IsNullOrWhiteSpace(nameSubstring))
            {
                return null;
            }

            foreach (GeoLandmarkFeature lm in landmarks)
            {
                if (lm != null && !string.IsNullOrEmpty(lm.name) &&
                    lm.name.IndexOf(nameSubstring, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return lm;
                }
            }

            return null;
        }

        public List<GeoLandmarkFeature> FindLandmarksInRadius(in UtmCoord centerUtm, double radiusMeters)
        {
            var results = new List<GeoLandmarkFeature>();
            foreach (GeoLandmarkFeature lm in landmarks)
            {
                if (lm != null && lm.coordinateUtm.Distance2D(centerUtm) <= radiusMeters)
                {
                    results.Add(lm);
                }
            }
            return results;
        }

        public GeoRoadFeature FindNearestRoad(in UtmCoord queryUtm, out double minDistanceMeters)
        {
            GeoRoadFeature best = null;
            minDistanceMeters = double.MaxValue;

            foreach (GeoRoadFeature road in roads)
            {
                if (road?.pointsUtm == null || road.pointsUtm.Length < 2)
                {
                    continue;
                }

                for (int i = 0; i < road.pointsUtm.Length - 1; i++)
                {
                    double dist = DistancePointToSegment2D(queryUtm, road.pointsUtm[i], road.pointsUtm[i + 1]);
                    if (dist < minDistanceMeters)
                    {
                        minDistanceMeters = dist;
                        best = road;
                    }
                }
            }

            return best;
        }

        private static double DistancePointToSegment2D(in UtmCoord p, in UtmCoord a, in UtmCoord b)
        {
            double abx = b.easting - a.easting;
            double aby = b.northing - a.northing;
            double lenSq = abx * abx + aby * aby;
            if (lenSq <= 1e-8)
            {
                return p.Distance2D(a);
            }

            double t = ((p.easting - a.easting) * abx + (p.northing - a.northing) * aby) / lenSq;
            t = Math.Max(0.0, Math.Min(1.0, t));
            var proj = new UtmCoord(a.easting + abx * t, a.northing + aby * t, a.zone, a.isNorthernHemisphere);
            return p.Distance2D(proj);
        }
    }
}
