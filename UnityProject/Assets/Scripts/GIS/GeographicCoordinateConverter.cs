using System;
using UnityEngine;

namespace DhakaGta.GIS
{
    /// <summary>
    /// Double-precision WGS84 geographic coordinates.
    /// </summary>
    [Serializable]
    public struct GeoLatLon
    {
        public double latitude;
        public double longitude;
        public double elevationMeters;

        public GeoLatLon(double lat, double lon, double elevation = 0.0)
        {
            latitude = lat;
            longitude = lon;
            elevationMeters = elevation;
        }
    }

    /// <summary>
    /// Double-precision UTM coordinate (default EPSG:32646 — WGS 84 / UTM Zone 46N for Dhaka).
    /// </summary>
    [Serializable]
    public struct UtmCoord
    {
        public double easting;
        public double northing;
        public int zone;
        public bool isNorthernHemisphere;

        public UtmCoord(double eastingMeters, double northingMeters, int utmZone = 46, bool northern = true)
        {
            easting = eastingMeters;
            northing = northingMeters;
            zone = utmZone;
            isNorthernHemisphere = northern;
        }

        public double Distance2D(in UtmCoord other)
        {
            double de = easting - other.easting;
            double dn = northing - other.northing;
            return Math.Sqrt(de * de + dn * dn);
        }
    }

    /// <summary>
    /// Exact WGS84 ellipsoid <-> UTM Zone 46N (EPSG:32646) <-> 1:1 meter Unity world coordinate converter.
    /// Unity axes follow geographic East-Up-North convention: +X = East, +Y = Elevation, +Z = North.
    /// Zero artificial city scaling: 1 Unity unit = 1.0 real-world meter.
    /// </summary>
    public static class GeographicCoordinateConverter
    {
        // WGS84 Ellipsoid Constants
        public const double Wgs84SemiMajorAxisMeters = 6378137.0;
        public const double Wgs84InvFlattening = 298.257223563;
        public const double Wgs84Flattening = 1.0 / Wgs84InvFlattening;
        public const double Wgs84EccentricitySq = Wgs84Flattening * (2.0 - Wgs84Flattening);
        public const double Wgs84SecondEccentricitySq = Wgs84EccentricitySq / (1.0 - Wgs84EccentricitySq);

        // UTM Zone 46N Constants (covers 90.0 E to 96.0 E; Central Meridian = 93.0 E)
        public const int DhakaUtmZone = 46;
        public const double UtmScaleFactor = 0.9996;
        public const double UtmFalseEastingMeters = 500000.0;
        public const double UtmSouthernFalseNorthingMeters = 10000000.0;

        // Canonical Dhaka Datum Anchor: Shapla Chottor Roundabout (Motijheel, Dhaka)
        public const double ShaplaOriginLat = 23.733015;
        public const double ShaplaOriginLon = 90.417014;

        private const double DegToRad = Math.PI / 180.0;
        private const double RadToDeg = 180.0 / Math.PI;

        /// <summary>
        /// Computes standard UTM zone number [1..60] for a longitude in [-180..180].
        /// Returns 46 for Dhaka (~90.4 E).
        /// </summary>
        public static int GetUtmZoneNumber(double latitudeDeg, double longitudeDeg)
        {
            double normalizedLon = ((longitudeDeg + 180.0) % 360.0 + 360.0) % 360.0 - 180.0;
            int zone = (int)Math.Floor((normalizedLon + 180.0) / 6.0) + 1;

            if (latitudeDeg >= 56.0 && latitudeDeg < 64.0 && normalizedLon >= 3.0 && normalizedLon < 12.0)
            {
                return 32;
            }
            if (latitudeDeg >= 72.0 && latitudeDeg < 84.0)
            {
                if (normalizedLon >= 0.0 && normalizedLon < 9.0) return 31;
                if (normalizedLon >= 9.0 && normalizedLon < 21.0) return 33;
                if (normalizedLon >= 21.0 && normalizedLon < 33.0) return 35;
                if (normalizedLon >= 33.0 && normalizedLon < 42.0) return 37;
            }

            return Mathf.Clamp(zone, 1, 60);
        }

        public static double GetCentralMeridianDegrees(int zone)
        {
            return (zone - 1) * 6.0 - 180.0 + 3.0;
        }

        /// <summary>
        /// Converts WGS84 latitude/longitude into double-precision UTM coordinates.
        /// Defaults to UTM Zone 46N so continuous datasets spanning the 90.0 E meridian remain seamless.
        /// </summary>
        public static UtmCoord LatLonToUtm(double latitudeDeg, double longitudeDeg, int forcedZone = DhakaUtmZone)
        {
            int zone = forcedZone > 0 ? forcedZone : GetUtmZoneNumber(latitudeDeg, longitudeDeg);
            bool northern = latitudeDeg >= 0.0;

            double latRad = latitudeDeg * DegToRad;
            double lonRad = longitudeDeg * DegToRad;
            double lonOriginRad = GetCentralMeridianDegrees(zone) * DegToRad;

            double sinLat = Math.Sin(latRad);
            double cosLat = Math.Cos(latRad);
            double tanLat = Math.Tan(latRad);

            double e2 = Wgs84EccentricitySq;
            double ep2 = Wgs84SecondEccentricitySq;

            double n = Wgs84SemiMajorAxisMeters / Math.Sqrt(1.0 - e2 * sinLat * sinLat);
            double t = tanLat * tanLat;
            double c = ep2 * cosLat * cosLat;
            double a = cosLat * (lonRad - lonOriginRad);

            double m = ComputeMeridionalArc(latRad, e2);

            double easting = UtmFalseEastingMeters + UtmScaleFactor * n * (
                a
                + ((1.0 - t + c) * Math.Pow(a, 3)) / 6.0
                + ((5.0 - 18.0 * t + t * t + 72.0 * c - 58.0 * ep2) * Math.Pow(a, 5)) / 120.0
            );

            double northing = UtmScaleFactor * (
                m + n * tanLat * (
                    (a * a) / 2.0
                    + ((5.0 - t + 9.0 * c + 4.0 * c * c) * Math.Pow(a, 4)) / 24.0
                    + ((61.0 - 58.0 * t + t * t + 600.0 * c - 330.0 * ep2) * Math.Pow(a, 6)) / 720.0
                )
            );

            if (!northern)
            {
                northing += UtmSouthernFalseNorthingMeters;
            }

            return new UtmCoord(easting, northing, zone, northern);
        }

        /// <summary>
        /// Converts double-precision UTM coordinates back into WGS84 latitude/longitude degrees.
        /// </summary>
        public static GeoLatLon UtmToLatLon(in UtmCoord utm, double elevationMeters = 0.0)
        {
            double e2 = Wgs84EccentricitySq;
            double ep2 = Wgs84SecondEccentricitySq;
            double e1 = (1.0 - Math.Sqrt(1.0 - e2)) / (1.0 + Math.Sqrt(1.0 - e2));

            double x = utm.easting - UtmFalseEastingMeters;
            double y = utm.isNorthernHemisphere ? utm.northing : utm.northing - UtmSouthernFalseNorthingMeters;

            double m = y / UtmScaleFactor;
            double mu = m / (Wgs84SemiMajorAxisMeters * (1.0 - e2 / 4.0 - 3.0 * e2 * e2 / 64.0 - 5.0 * e2 * e2 * e2 / 256.0));

            double phi1Rad = mu
                + (3.0 * e1 / 2.0 - 27.0 * Math.Pow(e1, 3) / 32.0) * Math.Sin(2.0 * mu)
                + (21.0 * e1 * e1 / 16.0 - 55.0 * Math.Pow(e1, 4) / 32.0) * Math.Sin(4.0 * mu)
                + (151.0 * Math.Pow(e1, 3) / 96.0) * Math.Sin(6.0 * mu)
                + (1097.0 * Math.Pow(e1, 4) / 512.0) * Math.Sin(8.0 * mu);

            double sinPhi1 = Math.Sin(phi1Rad);
            double cosPhi1 = Math.Cos(phi1Rad);
            double tanPhi1 = Math.Tan(phi1Rad);

            double n1 = Wgs84SemiMajorAxisMeters / Math.Sqrt(1.0 - e2 * sinPhi1 * sinPhi1);
            double t1 = tanPhi1 * tanPhi1;
            double c1 = ep2 * cosPhi1 * cosPhi1;
            double r1 = Wgs84SemiMajorAxisMeters * (1.0 - e2) / Math.Pow(1.0 - e2 * sinPhi1 * sinPhi1, 1.5);
            double d = x / (n1 * UtmScaleFactor);

            double latRad = phi1Rad - (n1 * tanPhi1 / r1) * (
                (d * d) / 2.0
                - (5.0 + 3.0 * t1 + 10.0 * c1 - 4.0 * c1 * c1 - 9.0 * ep2) * Math.Pow(d, 4) / 24.0
                + (61.0 + 90.0 * t1 + 298.0 * c1 + 45.0 * t1 * t1 - 252.0 * ep2 - 3.0 * c1 * c1) * Math.Pow(d, 6) / 720.0
            );

            double lonDeltaRad = (
                d
                - (1.0 + 2.0 * t1 + c1) * Math.Pow(d, 3) / 6.0
                + (5.0 - 2.0 * c1 + 28.0 * t1 - 3.0 * c1 * c1 + 8.0 * ep2 + 24.0 * t1 * t1) * Math.Pow(d, 5) / 120.0
            ) / cosPhi1;

            double lonOriginDeg = GetCentralMeridianDegrees(utm.zone);
            return new GeoLatLon(latRad * RadToDeg, lonOriginDeg + lonDeltaRad * RadToDeg, elevationMeters);
        }

        /// <summary>
        /// Projects a UTM coordinate into 1:1 Unity local world space relative to a double-precision origin anchor.
        /// +X = East (meters), +Y = Elevation (meters), +Z = North (meters).
        /// </summary>
        public static Vector3 UtmToUnityLocal(
            in UtmCoord pointUtm,
            in UtmCoord originUtm,
            double elevationMeters = 0.0,
            double originElevationMeters = 0.0)
        {
            double dx = pointUtm.easting - originUtm.easting;
            double dy = elevationMeters - originElevationMeters;
            double dz = pointUtm.northing - originUtm.northing;
            return new Vector3((float)dx, (float)dy, (float)dz);
        }

        /// <summary>
        /// Converts a 1:1 Unity local world position back into double-precision UTM coordinates.
        /// </summary>
        public static UtmCoord UnityLocalToUtm(Vector3 unityPosition, in UtmCoord originUtm)
        {
            return new UtmCoord(
                originUtm.easting + unityPosition.x,
                originUtm.northing + unityPosition.z,
                originUtm.zone,
                originUtm.isNorthernHemisphere);
        }

        /// <summary>
        /// Computes geodesic surface distance on Earth using exact WGS84 Vincenty inverse ellipsoidal equations
        /// for sub-millimeter distance checks against UTM Zone 46N planar distances.
        /// </summary>
        public static double ComputeGeodesicDistanceMeters(double lat1Deg, double lon1Deg, double lat2Deg, double lon2Deg)
        {
            if (Math.Abs(lat1Deg - lat2Deg) < 1e-12 && Math.Abs(lon1Deg - lon2Deg) < 1e-12)
            {
                return 0.0;
            }

            const double a = Wgs84SemiMajorAxisMeters;
            const double f = Wgs84Flattening;
            double b = a * (1.0 - f);

            double u1 = Math.Atan((1.0 - f) * Math.Tan(lat1Deg * DegToRad));
            double u2 = Math.Atan((1.0 - f) * Math.Tan(lat2Deg * DegToRad));
            double l = (lon2Deg - lon1Deg) * DegToRad;

            double sinU1 = Math.Sin(u1), cosU1 = Math.Cos(u1);
            double sinU2 = Math.Sin(u2), cosU2 = Math.Cos(u2);

            double lambda = l;
            double sinSigma = 0.0, cosSigma = 0.0, sigma = 0.0, cosSqAlpha = 0.0, cos2SigmaM = 0.0;

            for (int iter = 0; iter < 100; iter++)
            {
                double sinLambda = Math.Sin(lambda);
                double cosLambda = Math.Cos(lambda);
                double t1 = cosU2 * sinLambda;
                double t2 = cosU1 * sinU2 - sinU1 * cosU2 * cosLambda;
                sinSigma = Math.Sqrt(t1 * t1 + t2 * t2);
                if (sinSigma == 0.0)
                {
                    return 0.0;
                }

                cosSigma = sinU1 * sinU2 + cosU1 * cosU2 * cosLambda;
                sigma = Math.Atan2(sinSigma, cosSigma);
                double sinAlpha = cosU1 * cosU2 * sinLambda / sinSigma;
                cosSqAlpha = 1.0 - sinAlpha * sinAlpha;
                cos2SigmaM = cosSqAlpha != 0.0 ? cosSigma - 2.0 * sinU1 * sinU2 / cosSqAlpha : 0.0;
                double c = (f / 16.0) * cosSqAlpha * (4.0 + f * (4.0 - 3.0 * cosSqAlpha));
                double lambdaPrev = lambda;
                lambda = l + (1.0 - c) * f * sinAlpha * (
                    sigma + c * sinSigma * (cos2SigmaM + c * cosSigma * (-1.0 + 2.0 * cos2SigmaM * cos2SigmaM)));
                if (Math.Abs(lambda - lambdaPrev) < 1e-12)
                {
                    break;
                }
            }

            double uSq = cosSqAlpha * (a * a - b * b) / (b * b);
            double av = 1.0 + uSq / 16384.0 * (4096.0 + uSq * (-768.0 + uSq * (320.0 - 175.0 * uSq)));
            double bv = uSq / 1024.0 * (256.0 + uSq * (-128.0 + uSq * (74.0 - 47.0 * uSq)));
            double deltaSigma = bv * sinSigma * (
                cos2SigmaM + (bv / 4.0) * (
                    cosSigma * (-1.0 + 2.0 * cos2SigmaM * cos2SigmaM)
                    - (bv / 6.0) * cos2SigmaM * (-3.0 + 4.0 * sinSigma * sinSigma) * (-3.0 + 4.0 * cos2SigmaM * cos2SigmaM)));

            return b * av * (sigma - deltaSigma);
        }

        /// <summary>
        /// Returns approximate UTM point scale factor at a given WGS84 location inside its zone.
        /// For Dhaka (~90.417 E, ~2.583 deg west of 93 E), k is ~1.00043 (<0.05% scale deviation).
        /// </summary>
        public static double ComputeUtmPointScaleFactor(double latitudeDeg, double longitudeDeg, int zone = DhakaUtmZone)
        {
            double latRad = latitudeDeg * DegToRad;
            double deltaLonRad = (longitudeDeg - GetCentralMeridianDegrees(zone)) * DegToRad;
            double cosLat = Math.Cos(latRad);
            double c = Wgs84SecondEccentricitySq * cosLat * cosLat;
            double a = cosLat * deltaLonRad;
            return UtmScaleFactor * (1.0 + (1.0 + c) * a * a * 0.5);
        }

        private static double ComputeMeridionalArc(double latRad, double e2)
        {
            double e4 = e2 * e2;
            double e6 = e4 * e2;

            return Wgs84SemiMajorAxisMeters * (
                (1.0 - e2 / 4.0 - 3.0 * e4 / 64.0 - 5.0 * e6 / 256.0) * latRad
                - (3.0 * e2 / 8.0 + 3.0 * e4 / 32.0 + 45.0 * e6 / 1024.0) * Math.Sin(2.0 * latRad)
                + (15.0 * e4 / 256.0 + 45.0 * e6 / 1024.0) * Math.Sin(4.0 * latRad)
                - (35.0 * e6 / 3072.0) * Math.Sin(6.0 * latRad)
            );
        }
    }
}
