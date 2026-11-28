namespace DhakaGta.Core
{
    /// <summary>
    /// Converts WGS84 geographic coordinates around Shapla Crossing / Motijheel into 1:1 real-world meter
    /// East-Up-North Unity coordinates (+X = East, +Z = North). Never applies artificial city compression.
    /// For full UTM Zone 46N (EPSG:32646) + floating origin support, use DhakaGta.GIS.GeographicCoordinateConverter.
    /// </summary>
    public static class DhakaGeoProjection
    {
        public const double OriginLatitude = 23.733015;
        public const double OriginLongitude = 90.417014;
        public const double MetersPerDegLat = 110750.0;
        public const double MetersPerDegLon = 101850.0;
        public const double UnityMetersPerRealMeter = 1.0;
        public const float RiverZStartNorthMeters = -2850.0f;

        /// <summary>
        /// Projects a WGS84 latitude/longitude pair into flat 1:1 tangent-plane Unity (X=East, Z=North) meters.
        /// </summary>
        public static UnityEngine.Vector2 LatLonToMetersXZ(double latitude, double longitude)
        {
            double xEast = (longitude - OriginLongitude) * MetersPerDegLon * UnityMetersPerRealMeter;
            double zNorth = (latitude - OriginLatitude) * MetersPerDegLat * UnityMetersPerRealMeter;
            return new UnityEngine.Vector2((float)xEast, (float)zNorth);
        }

        /// <summary>
        /// Converts tangent-plane Unity (X=East, Z=North) meters back into approximate WGS84 (Latitude, Longitude).
        /// </summary>
        public static (double latitude, double longitude) MetersXZToLatLon(UnityEngine.Vector2 worldXZ)
        {
            double lon = OriginLongitude + (worldXZ.x / (MetersPerDegLon * UnityMetersPerRealMeter));
            double lat = OriginLatitude + (worldXZ.y / (MetersPerDegLat * UnityMetersPerRealMeter));
            return (lat, lon);
        }

        /// <summary>
        /// Returns true when the given world XZ coordinate falls south of the Buriganga River embankment (~2.85km south of Shapla).
        /// </summary>
        public static bool IsInRiverZone(UnityEngine.Vector3 worldPosition)
        {
            return worldPosition.z <= RiverZStartNorthMeters;
        }
    }
}
