using UnityEngine;

namespace DhakaGta.GIS
{
    /// <summary>
    /// Scene component attached to each imported real-scale Dhaka landmark GameObject so geographic metadata
    /// (OSM ID, English/Bangla names, WGS84 Lat/Lon, UTM Zone 46N coordinate) persists in Inspector and at runtime.
    /// </summary>
    public sealed class GeoLandmarkAnchor : MonoBehaviour
    {
        public string osmId;
        public string displayName;
        public string banglaName;
        public string kind;
        public double latitude;
        public double longitude;
        public UtmCoord utmCoordinate;
        public GeoTagPair[] metadata = System.Array.Empty<GeoTagPair>();

        public void Initialize(GeoLandmarkFeature feature)
        {
            if (feature == null)
            {
                return;
            }

            osmId = feature.osmId;
            displayName = feature.name;
            banglaName = feature.banglaName;
            kind = feature.kind;
            latitude = feature.coordinateWgs84.latitude;
            longitude = feature.coordinateWgs84.longitude;
            utmCoordinate = feature.coordinateUtm;
            metadata = feature.metadata ?? System.Array.Empty<GeoTagPair>();
        }
    }
}
