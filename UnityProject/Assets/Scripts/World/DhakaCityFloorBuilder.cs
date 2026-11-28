using DhakaGta.Core;
using DhakaGta.Data;
using UnityEngine;

namespace DhakaGta.World
{
    /// <summary>
    /// Attaches authored GIS landmark gizmos and surface tags to the vertical slice foundation
    /// so Shapla Crossing, roads, buildings, and Buriganga river zones carry explicit PhysicsLayers.
    /// </summary>
    public sealed class DhakaCityFloorBuilder : MonoBehaviour
    {
        [SerializeField] private LandmarkDefinitionSO[] landmarks = System.Array.Empty<LandmarkDefinitionSO>();
        [SerializeField] private RoadCorridorSO[] roads = System.Array.Empty<RoadCorridorSO>();

        public void SetReferences(LandmarkDefinitionSO[] landmarkAssets, RoadCorridorSO[] roadAssets)
        {
            landmarks = landmarkAssets ?? System.Array.Empty<LandmarkDefinitionSO>();
            roads = roadAssets ?? System.Array.Empty<RoadCorridorSO>();
        }

        private void OnDrawGizmosSelected()
        {
            if (roads != null)
            {
                Gizmos.color = new Color(0.9f, 0.72f, 0.25f, 0.85f);
                foreach (RoadCorridorSO road in roads)
                {
                    if (road == null) continue;
                    Vector3 a = new Vector3(road.startXZ.x, 0.25f, road.startXZ.y);
                    Vector3 b = new Vector3(road.endXZ.x, 0.25f, road.endXZ.y);
                    Gizmos.DrawLine(a, b);
                }
            }

            if (landmarks != null)
            {
                Gizmos.color = new Color(0.18f, 0.82f, 0.62f, 0.9f);
                foreach (LandmarkDefinitionSO lm in landmarks)
                {
                    if (lm == null) continue;
                    Gizmos.DrawWireSphere(lm.stylizedPosition + Vector3.up * 2f, 2.5f);
                }
            }

            Gizmos.color = new Color(0.1f, 0.55f, 0.9f, 0.75f);
            Gizmos.DrawLine(
                new Vector3(-4000f, 0.3f, DhakaGeoProjection.RiverZStartNorthMeters),
                new Vector3(4000f, 0.3f, DhakaGeoProjection.RiverZStartNorthMeters));
        }
    }
}
