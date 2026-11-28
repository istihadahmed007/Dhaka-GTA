using System.Text;
using DhakaGta.Environment;
using DhakaGta.GIS;
using DhakaGta.Graphics;
using DhakaGta.Vehicles;
using DhakaGta.WorldGen;
using TMPro;
using UnityEngine;

namespace DhakaGta.UI
{
    /// <summary>
    /// Driving + On-Foot interaction & HDRP weather/camera telemetry overlay for GulshanHatirjheel_Milestone.unity.
    /// </summary>
    public sealed class GulshanHatirjheelHud : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour primaryVehicleBehaviour;
        [SerializeField] private ProtagonistInteractionScanner interactionScanner;
        [SerializeField] private WorldCoordinateManager coordinateManager;
        [SerializeField] private WorldStreamingController streamingController;
        [SerializeField] private RoadSplineSystem roadSplines;
        [SerializeField] private TextMeshProUGUI hudLabel;
        [SerializeField] private DhakaTimeOfDayAndWeatherController weatherController;
        [SerializeField] private DhakaCinemachineCameraRig cameraRig;
        [SerializeField] private WetSurfacePuddleDriver wetDriver;

        public void Configure(
            MonoBehaviour carBehaviour,
            ProtagonistInteractionScanner scanner,
            WorldCoordinateManager coordMgr,
            WorldStreamingController streamCtrl,
            RoadSplineSystem splines,
            TextMeshProUGUI label,
            DhakaTimeOfDayAndWeatherController weather = null,
            DhakaCinemachineCameraRig cam = null,
            WetSurfacePuddleDriver wet = null)
        {
            primaryVehicleBehaviour = carBehaviour;
            interactionScanner = scanner;
            coordinateManager = coordMgr;
            streamingController = streamCtrl;
            roadSplines = splines;
            hudLabel = label;
            weatherController = weather;
            cameraRig = cam;
            wetDriver = wet;
        }

        private void Update()
        {
            if (hudLabel == null || coordinateManager == null)
            {
                return;
            }

            MonoBehaviour activeCarBeh = interactionScanner != null && interactionScanner.ActiveVehicleBehaviour != null
                ? interactionScanner.ActiveVehicleBehaviour
                : primaryVehicleBehaviour;
            IVehicleDrivable activeCar = activeCarBeh as IVehicleDrivable;

            Vector3 activePos = interactionScanner != null
                ? interactionScanner.ActiveSubjectPosition
                : (activeCarBeh != null ? activeCarBeh.transform.position : Vector3.zero);

            UtmCoord utm = coordinateManager.UnityPositionToUtm(activePos);
            GeoLatLon ll = coordinateManager.UnityPositionToLatLon(activePos);
            string district = streamingController != null && streamingController.ActiveDistrict != null
                ? $"{streamingController.ActiveDistrict.displayName} ({streamingController.ActiveDistrict.banglaName})"
                : "Gulshan / Hatirjheel";

            string street = "Hatirjheel / Gulshan Loop";
            string extra = "";
            if (roadSplines != null)
            {
                RoadSplineSegment closest = roadSplines.FindClosestSegment(activePos, out float dist, out _, out _);
                if (closest != null)
                {
                    street = $"{closest.roadName} [{closest.gradeKind}, {closest.lanes}L, {closest.maxSpeedKph:F0} km/h]";
                    extra = $" | Offset: {dist:F1} m";
                }
            }

            string vehicleLine = activeCar != null
                ? $"VEHICLE ({activeCarBeh.gameObject.name}): {activeCar.ForwardSpeedKph:F1} km/h"
                : "ON FOOT (Approach any vehicle & press [F])";
            if (activeCarBeh is DhakaVehicleController rc)
            {
                vehicleLine += $" | Wheels: {rc.GroundedWheelCount} | HP: {(rc.DamageAndTuning != null ? rc.DamageAndTuning.CurrentHealth : 1000f):F0}" +
                               (rc.IsDrifting ? " [DRIFT]" : "") + (rc.IsReversing ? " [REV]" : "");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"DISTRICT: {district}");
            sb.AppendLine($"ROAD: {street}{extra}");
            sb.AppendLine(vehicleLine);
            if (weatherController != null && cameraRig != null)
            {
                int hr = Mathf.FloorToInt(weatherController.TimeOfDayHours);
                int mn = Mathf.FloorToInt((weatherController.TimeOfDayHours - hr) * 60f);
                float wetPct = wetDriver != null ? wetDriver.CurrentWetness * 100f : 0f;
                sb.AppendLine($"HDRP: {weatherController.ActiveCondition} ({hr:00}:{mn:00}) | Wet: {wetPct:F0}% | Cam: {cameraRig.CurrentMode} (FOV {cameraRig.CurrentFieldOfView:F0})");
            }
            sb.AppendLine($"WGS84: {ll.latitude:F5} N, {ll.longitude:F5} E | UTM 46N: ({utm.easting:F1}, {utm.northing:F1})");
            if (interactionScanner != null && !string.IsNullOrEmpty(interactionScanner.ActivePrompt))
            {
                sb.AppendLine($"ACTION: {interactionScanner.ActivePrompt}");
            }
            else
            {
                sb.AppendLine("WASD Move/Drive | E Interact | F Car | V Cam | F6 Weather | F7 +3h | F8 Quality");
            }
            if (interactionScanner != null && !string.IsNullOrEmpty(interactionScanner.LastFeedback))
            {
                sb.Append(interactionScanner.LastFeedback);
            }
            hudLabel.text = sb.ToString();
        }
    }
}
