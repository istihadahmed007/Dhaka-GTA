using System.Collections.Generic;
using DhakaGta.Core;
using DhakaGta.Data;
using UnityEngine;

namespace DhakaGta.Streaming
{
    /// <summary>
    /// Streams additive district scenes in and out around the active player transform using IWorldStreamingService.
    /// </summary>
    public sealed class DistrictStreamingController : MonoBehaviour
    {
        [SerializeField] private WorldStreamingConfigSO streamingConfig;
        [SerializeField] private Transform trackedTarget;
        [SerializeField] private float checkIntervalSeconds = 0.75f;

        private readonly Dictionary<string, float> _pendingUnloadTimers = new Dictionary<string, float>();
        private float _nextCheckTime;

        public void Configure(WorldStreamingConfigSO config, Transform target)
        {
            streamingConfig = config;
            trackedTarget = target;
        }

        private void Update()
        {
            if (streamingConfig == null || streamingConfig.districts == null || trackedTarget == null)
            {
                return;
            }

            if (Time.unscaledTime < _nextCheckTime)
            {
                return;
            }

            _nextCheckTime = Time.unscaledTime + Mathf.Max(0.2f, checkIntervalSeconds);
            EvaluateDistricts(trackedTarget.position);
        }

        public void EvaluateDistricts(Vector3 position)
        {
            IWorldStreamingService service = GameBootstrap.StreamingService;
            if (service == null || streamingConfig == null || streamingConfig.districts == null)
            {
                return;
            }

            float loadRadius = streamingConfig.streamingRadiusMeters;
            float unloadRadius = loadRadius + Mathf.Max(10f, streamingConfig.unloadHysteresisMeters);

            foreach (DistrictDefinitionSO district in streamingConfig.districts)
            {
                if (district == null || string.IsNullOrWhiteSpace(district.additiveSceneName))
                {
                    continue;
                }

                float distToBounds = DistanceToBoundsXZ(position, district.boundsMinXZ, district.boundsMaxXZ);
                bool loaded = service.IsSceneLoaded(district.additiveSceneName);

                if (distToBounds <= loadRadius)
                {
                    _pendingUnloadTimers.Remove(district.additiveSceneName);
                    if (!loaded)
                    {
                        _ = service.LoadAdditiveDistrictAsync(district.additiveSceneName);
                    }
                }
                else if (loaded && distToBounds > unloadRadius)
                {
                    if (!_pendingUnloadTimers.TryGetValue(district.additiveSceneName, out float startExit))
                    {
                        _pendingUnloadTimers[district.additiveSceneName] = Time.unscaledTime;
                    }
                    else if (Time.unscaledTime - startExit >= streamingConfig.unloadDelaySeconds)
                    {
                        _pendingUnloadTimers.Remove(district.additiveSceneName);
                        _ = service.UnloadAdditiveDistrictAsync(district.additiveSceneName);
                    }
                }
            }
        }

        public static float DistanceToBoundsXZ(Vector3 point, Vector2 minXZ, Vector2 maxXZ)
        {
            float clampedX = Mathf.Clamp(point.x, minXZ.x, maxXZ.x);
            float clampedZ = Mathf.Clamp(point.z, minXZ.y, maxXZ.y);
            float dx = point.x - clampedX;
            float dz = point.z - clampedZ;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
