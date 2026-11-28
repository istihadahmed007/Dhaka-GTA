using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace DhakaGta.Graphics
{
    /// <summary>
    /// Manages a mobile HDRP Reflection Probe centered around the player/vehicle corridor,
    /// triggering OnDemand updates cleanly as the vehicle moves or time-of-day shifts so glossy buildings,
    /// wet roads, and car paint reflect live surroundings.
    /// </summary>
    public sealed class DhakaReflectionProbeRig : MonoBehaviour
    {
        [SerializeField] private ReflectionProbe probeComponent;
        [SerializeField] private HDAdditionalReflectionData hdProbeData;
        [SerializeField] private Transform followTarget;
        [SerializeField] private float distanceTriggerMeters = 45f;
        [SerializeField] private float minimumIntervalSeconds = 2.5f;
        [SerializeField] private Vector3 influenceBoxSize = new Vector3(260f, 90f, 260f);

        private Vector3 _lastRenderPosition;
        private float _lastRenderTime = -100f;

        public void Configure(ReflectionProbe probe, Transform target)
        {
            probeComponent = probe;
            followTarget = target;
            if (probe != null)
            {
                hdProbeData = probe.GetComponent<HDAdditionalReflectionData>();
                if (hdProbeData == null) hdProbeData = probe.gameObject.AddComponent<HDAdditionalReflectionData>();

                hdProbeData.mode = ProbeSettings.Mode.Realtime;
                hdProbeData.realtimeMode = ProbeSettings.RealtimeMode.OnDemand;
                hdProbeData.influenceVolume.boxSize = influenceBoxSize;
                hdProbeData.weight = 1.0f;
                RequestImmediateRefresh();
            }
        }

        public void RequestImmediateRefresh()
        {
            if (probeComponent != null)
            {
                probeComponent.RequestRenderNextUpdate();
                _lastRenderTime = Time.unscaledTime;
                if (followTarget != null) _lastRenderPosition = followTarget.position;
            }
        }

        private void Update()
        {
            if (probeComponent == null || followTarget == null) return;

            transform.position = followTarget.position + Vector3.up * 3.5f;
            float distSq = (followTarget.position - _lastRenderPosition).sqrMagnitude;
            if (distSq >= distanceTriggerMeters * distanceTriggerMeters &&
                Time.unscaledTime - _lastRenderTime >= minimumIntervalSeconds)
            {
                RequestImmediateRefresh();
            }
        }
    }
}
