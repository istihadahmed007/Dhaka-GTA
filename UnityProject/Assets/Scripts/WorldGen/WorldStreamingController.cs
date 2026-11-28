using System;
using DhakaGta.Core;
using DhakaGta.GIS;
using UnityEngine;

namespace DhakaGta.WorldGen
{
    /// <summary>
    /// Coordinates real-time 12-district Dhaka region transitions, chunk activation (WorldChunkManager),
    /// and asynchronous additive / Addressables-ready loading (WorldRegionLoader) relative to WorldCoordinateManager.
    /// </summary>
    public sealed class WorldStreamingController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private WorldCoordinateManager coordinateManager;
        [SerializeField] private WorldRegionLoader regionLoader;
        [SerializeField] private WorldChunkManager chunkManager;
        [SerializeField] private Transform focusTarget;

        [Header("Streaming Radii (1:1 Real Meters)")]
        [SerializeField] private float preloadRegionMarginMeters = 900f;
        [SerializeField] private float unloadRegionMarginMeters = 1800f;
        [SerializeField] private float pollIntervalSeconds = 0.5f;

        private float _nextPollTime;
        private string _activeDistrictId = string.Empty;

        public event Action<DhakaDistrictDescriptor> OnActiveDistrictChanged;
        public DhakaDistrictDescriptor ActiveDistrict { get; private set; }

        public void Configure(
            WorldCoordinateManager coordMgr,
            WorldRegionLoader loader,
            WorldChunkManager chunks,
            Transform target)
        {
            coordinateManager = coordMgr;
            regionLoader = loader;
            chunkManager = chunks;
            focusTarget = target;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextPollTime) return;
            _nextPollTime = Time.unscaledTime + Mathf.Max(0.15f, pollIntervalSeconds);
            TickStreaming();
        }

        public void TickStreaming()
        {
            if (focusTarget == null || coordinateManager == null)
            {
                return;
            }

            UtmCoord utm = coordinateManager.UnityPositionToUtm(focusTarget.position);
            DhakaDistrictDescriptor resolved = WorldRegionLoader.ResolveDistrictAtUtm(utm);

            if (resolved != null && !string.Equals(resolved.districtId, _activeDistrictId, StringComparison.Ordinal))
            {
                _activeDistrictId = resolved.districtId;
                ActiveDistrict = resolved;
                GameEvents.RaiseDistrictChanged(resolved.districtId, resolved.displayName);
                OnActiveDistrictChanged?.Invoke(resolved);
            }

            if (chunkManager != null)
            {
                chunkManager.EvaluateStreamingAround(focusTarget.position);
            }

            if (regionLoader != null)
            {
                foreach (DhakaDistrictDescriptor desc in WorldRegionLoader.AllTwelveDistricts)
                {
                    UtmCoord center = GeographicCoordinateConverter.LatLonToUtm(desc.centerLat, desc.centerLon, GeographicCoordinateConverter.DhakaUtmZone);
                    double distToEdge = center.Distance2D(utm) - desc.radiusMeters;
                    RegionLoadStatus status = regionLoader.GetStatus(desc.districtId);

                    if (distToEdge <= preloadRegionMarginMeters && status == RegionLoadStatus.Unloaded)
                    {
                        _ = regionLoader.LoadRegionAsync(desc.districtId);
                    }
                    else if (distToEdge > unloadRegionMarginMeters && status == RegionLoadStatus.Loaded)
                    {
                        _ = regionLoader.UnloadRegionAsync(desc.districtId);
                    }
                }
            }
        }
    }
}
