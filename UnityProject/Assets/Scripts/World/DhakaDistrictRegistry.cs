using System.Collections.Generic;
using DhakaGta.Core;
using DhakaGta.Data;
using UnityEngine;

namespace DhakaGta.World
{
    /// <summary>
    /// Spatial resolver for Dhaka districts and road corridors.
    /// Fixes the legacy Three.js bug where Shapla spawn (-2.2, 35.2) reported Old Dhaka / wrong road.
    /// </summary>
    public sealed class DhakaDistrictRegistry : MonoBehaviour
    {
        [SerializeField] private DistrictDefinitionSO[] districts = System.Array.Empty<DistrictDefinitionSO>();
        [SerializeField] private LandmarkDefinitionSO[] landmarks = System.Array.Empty<LandmarkDefinitionSO>();
        [SerializeField] private RoadCorridorSO[] roads = System.Array.Empty<RoadCorridorSO>();
        [SerializeField] private Transform trackedTarget;

        private string _lastDistrictId = string.Empty;
        private string _lastRoadName = string.Empty;

        public IReadOnlyList<DistrictDefinitionSO> Districts => districts;
        public IReadOnlyList<LandmarkDefinitionSO> Landmarks => landmarks;
        public IReadOnlyList<RoadCorridorSO> Roads => roads;

        public string CurrentDistrictId { get; private set; } = "shapla_crossing";
        public string CurrentDistrictName { get; private set; } = "Shapla Crossing";
        public string CurrentRoadName { get; private set; } = "Kazi Nazrul Islam Ave";

        public void SetCatalog(
            DistrictDefinitionSO[] districtAssets,
            LandmarkDefinitionSO[] landmarkAssets,
            RoadCorridorSO[] roadAssets,
            Transform target = null)
        {
            districts = districtAssets ?? System.Array.Empty<DistrictDefinitionSO>();
            landmarks = landmarkAssets ?? System.Array.Empty<LandmarkDefinitionSO>();
            roads = roadAssets ?? System.Array.Empty<RoadCorridorSO>();
            if (target != null)
            {
                trackedTarget = target;
            }
        }

        public void SetTrackedTarget(Transform target)
        {
            trackedTarget = target;
        }

        private void Update()
        {
            if (trackedTarget != null)
            {
                UpdateForPosition(trackedTarget.position);
            }
        }

        public void UpdateForPosition(Vector3 worldPosition)
        {
            DistrictDefinitionSO resolved = ResolveDistrict(worldPosition);
            string districtId = resolved != null ? resolved.districtId : "dhaka_metro";
            string districtName = resolved != null ? resolved.displayName : "Dhaka Metropolitan";

            if (!string.Equals(districtId, _lastDistrictId, System.StringComparison.Ordinal))
            {
                _lastDistrictId = districtId;
                CurrentDistrictId = districtId;
                CurrentDistrictName = districtName;
                GameEvents.RaiseDistrictChanged(districtId, districtName);
            }

            RoadCorridorSO nearestRoad = ResolveNearestRoad(worldPosition, out float distMeters);
            string roadName = nearestRoad != null && distMeters <= 32f
                ? nearestRoad.roadName
                : $"{districtName} Inner Lane";

            if (!string.Equals(roadName, _lastRoadName, System.StringComparison.Ordinal))
            {
                _lastRoadName = roadName;
                CurrentRoadName = roadName;
                GameEvents.RaiseNearestRoadChanged(roadName);
            }
        }

        public DistrictDefinitionSO ResolveDistrict(Vector3 worldPosition)
        {
            DistrictDefinitionSO bestMatch = null;
            int highestPriority = int.MinValue;

            if (districts != null)
            {
                foreach (DistrictDefinitionSO district in districts)
                {
                    if (district != null && district.Contains(worldPosition) && district.priority > highestPriority)
                    {
                        highestPriority = district.priority;
                        bestMatch = district;
                    }
                }
            }

            return bestMatch;
        }

        public RoadCorridorSO ResolveNearestRoad(Vector3 worldPosition, out float minDistance)
        {
            RoadCorridorSO closest = null;
            minDistance = float.MaxValue;

            if (roads != null)
            {
                foreach (RoadCorridorSO road in roads)
                {
                    if (road == null)
                    {
                        continue;
                    }

                    float d = road.DistanceToPointXZ(worldPosition);
                    if (d < minDistance)
                    {
                        minDistance = d;
                        closest = road;
                    }
                }
            }

            return closest;
        }
    }
}
