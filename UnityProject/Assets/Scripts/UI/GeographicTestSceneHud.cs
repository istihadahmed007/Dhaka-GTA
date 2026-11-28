using System.Text;
using DhakaGta.GIS;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DhakaGta.UI
{
    /// <summary>
    /// Diagnostic HUD for GeographicTestScene displaying active UTM Zone 46N coordinates, WGS84 Lat/Lon,
    /// floating-origin shift delta, and real-world metric distances to iconic Dhaka landmarks.
    /// </summary>
    public sealed class GeographicTestSceneHud : MonoBehaviour
    {
        [SerializeField] private WorldCoordinateManager coordinateManager;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private TextMeshProUGUI telemetryLabel;
        [SerializeField] private float flySpeedMetersPerSec = 140f;

        public void Configure(WorldCoordinateManager manager, Transform camTransform, TextMeshProUGUI label)
        {
            coordinateManager = manager;
            cameraTransform = camTransform;
            telemetryLabel = label;
        }

        private void Update()
        {
            HandleKeyboardFlyPreview();
            UpdateTelemetry();
        }

        private void HandleKeyboardFlyPreview()
        {
            if (cameraTransform == null || Keyboard.current == null)
            {
                return;
            }

            Vector3 move = Vector3.zero;
            if (Keyboard.current.wKey.isPressed) move += cameraTransform.forward;
            if (Keyboard.current.sKey.isPressed) move -= cameraTransform.forward;
            if (Keyboard.current.dKey.isPressed) move += cameraTransform.right;
            if (Keyboard.current.aKey.isPressed) move -= cameraTransform.right;
            if (Keyboard.current.eKey.isPressed) move += Vector3.up;
            if (Keyboard.current.qKey.isPressed) move += Vector3.down;

            if (move.sqrMagnitude > 0.001f)
            {
                float mult = Keyboard.current.leftShiftKey.isPressed ? 4.0f : 1.0f;
                cameraTransform.position += move.normalized * (flySpeedMetersPerSec * mult * Time.deltaTime);
            }
        }

        private void UpdateTelemetry()
        {
            if (telemetryLabel == null || coordinateManager == null || cameraTransform == null)
            {
                return;
            }

            UtmCoord utm = coordinateManager.UnityPositionToUtm(cameraTransform.position);
            GeoLatLon ll = coordinateManager.UnityPositionToLatLon(cameraTransform.position);

            var sb = new StringBuilder();
            sb.AppendLine("DHAKA-GTA — 1:1 REAL-SCALE GIS PIPELINE (EPSG:32646 / UTM ZONE 46N)");
            sb.AppendLine($"WGS84: {ll.latitude:F6} N, {ll.longitude:F6} E | Alt: {cameraTransform.position.y:F1} m");
            sb.AppendLine($"UTM 46N: E {utm.easting:F2} m, N {utm.northing:F2} m");
            sb.AppendLine($"Floating Origin Shift: ({coordinateManager.CumulativeShiftEastingMeters:F1}, {coordinateManager.CumulativeShiftNorthingMeters:F1}) m");
            sb.Append("WASD + Shift/E/Q: Fly 1:1 City | ");

            GeoFeatureDatabase db = coordinateManager.FeatureDatabase;
            if (db != null)
            {
                sb.AppendLine($"Features: {db.roads.Count} roads, {db.buildings.Count} bldgs, {db.waterBodies.Count} water, {db.metroAndRailways.Count} metro/rail, {db.landmarks.Count} landmarks");
                GeoRoadFeature nearestRoad = db.FindNearestRoad(utm, out double roadDist);
                if (nearestRoad != null && !string.IsNullOrEmpty(nearestRoad.name))
                {
                    sb.AppendLine($"Nearest OSM Road: {nearestRoad.name} ({roadDist:F1} m)");
                }
            }

            telemetryLabel.text = sb.ToString();
        }
    }
}
