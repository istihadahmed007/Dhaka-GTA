using DhakaGta.WorldGen;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace DhakaGta.Graphics
{
    /// <summary>
    /// Attaches physical SpotLight headlights and glowing taillight housings to vehicles
    /// and places amber streetlamp lights along the showcase corridor registered with TimeOfDayController.
    /// </summary>
    public static class DhakaGraphicsShowcaseRig
    {
        public static void AttachVehicleHeadlightsAndTaillights(
            Transform vehicleTransform,
            DhakaTimeOfDayAndWeatherController weatherCtrl,
            Material tailGlowMat)
        {
            if (vehicleTransform == null) return;

            float[] xOffsets = { -0.62f, 0.62f };
            foreach (float x in xOffsets)
            {
                var headGo = new GameObject(x < 0 ? "Headlight_L" : "Headlight_R");
                headGo.transform.SetParent(vehicleTransform, false);
                headGo.transform.localPosition = new Vector3(x, 0.68f, 2.15f);
                headGo.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);

                var light = headGo.AddComponent<Light>();
                light.type = LightType.Spot;
                light.range = 65f;
                light.spotAngle = 52f;
                light.useColorTemperature = true;
                light.colorTemperature = 5600f;
                light.intensity = 1400f;
                light.shadows = LightShadows.None;
                headGo.AddComponent<HDAdditionalLightData>();
                weatherCtrl?.RegisterVehicleHeadlight(light);

                var tailGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tailGo.name = x < 0 ? "Taillight_L" : "Taillight_R";
                Object.DestroyImmediate(tailGo.GetComponent<Collider>());
                tailGo.transform.SetParent(vehicleTransform, false);
                tailGo.transform.localPosition = new Vector3(x, 0.72f, -2.18f);
                tailGo.transform.localScale = new Vector3(0.38f, 0.14f, 0.06f);
                if (tailGlowMat != null) tailGo.GetComponent<MeshRenderer>().sharedMaterial = tailGlowMat;
            }
        }

        public static void PlaceStreetlampLightsAlongSpawnCorridor(
            RoadSplineSystem splineSystem,
            Vector3 centerAnchor,
            DhakaTimeOfDayAndWeatherController weatherCtrl,
            Transform lightsRoot,
            Material bulbGlowMat)
        {
            if (splineSystem == null || lightsRoot == null) return;

            int lampsPlaced = 0;
            foreach (RoadSplineSegment seg in splineSystem.Segments)
            {
                if (seg == null || seg.gradeKind != RoadGradeKind.Surface) continue;
                if (Vector3.Distance(seg.StartPosition, centerAnchor) > 450f) continue;

                float lateral = seg.widthMeters * 0.5f + 1.8f;
                for (float d = 15f; d < seg.totalLengthMeters - 10f && lampsPlaced < 18; d += 42f)
                {
                    seg.SampleAtDistance(d, out Vector3 p, out _, out Vector3 right);
                    Vector3 lampPos = p + right * lateral + Vector3.up * 6.8f;

                    var lampGo = new GameObject($"StreetlampLight_{lampsPlaced + 1}");
                    lampGo.transform.SetParent(lightsRoot, false);
                    lampGo.transform.position = lampPos;
                    lampGo.transform.rotation = Quaternion.Euler(78f, 0f, 0f);

                    var spot = lampGo.AddComponent<Light>();
                    spot.type = LightType.Spot;
                    spot.range = 32f;
                    spot.spotAngle = 86f;
                    spot.useColorTemperature = true;
                    spot.colorTemperature = 3200f;
                    spot.intensity = 900f;
                    spot.shadows = LightShadows.None;
                    lampGo.AddComponent<HDAdditionalLightData>();

                    var fixture = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Object.DestroyImmediate(fixture.GetComponent<Collider>());
                    fixture.transform.SetParent(lampGo.transform, false);
                    fixture.transform.localScale = new Vector3(0.5f, 0.25f, 0.9f);
                    if (bulbGlowMat != null) fixture.GetComponent<MeshRenderer>().sharedMaterial = bulbGlowMat;

                    weatherCtrl?.RegisterStreetLamp(spot);
                    lampsPlaced++;
                }
            }
        }
    }
}
