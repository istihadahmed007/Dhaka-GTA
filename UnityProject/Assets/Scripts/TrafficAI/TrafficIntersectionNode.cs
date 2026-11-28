using DhakaGta.Core;
using DhakaGta.WorldGen;
using UnityEngine;

namespace DhakaGta.TrafficAI
{
    public enum SignalLightState
    {
        GreenNorthSouth = 0,
        YellowNorthSouth = 1,
        GreenEastWest = 2,
        YellowEastWest = 3,
    }

    /// <summary>
    /// Active Dhaka intersection controller synchronized with RoadJunctionNode topology and signal lamp renderers.
    /// Governs North-South / East-West vehicle stop-lines and crosswalk pedestrian clearance.
    /// </summary>
    public sealed class TrafficIntersectionNode : MonoBehaviour
    {
        [SerializeField] private string intersectionId = "shapla_roundabout";
        [SerializeField] private float cycleSeconds = 26f;
        [SerializeField] private float phaseOffsetSeconds;
        [SerializeField] private float yellowDurationSeconds = 3f;
        [SerializeField] private float junctionStopRadiusMeters = 11f;
        [SerializeField] private bool hasActiveSignal = true;
        [SerializeField] private SignalLightState currentSignal = SignalLightState.GreenNorthSouth;

        private Renderer[] _lampRenderers = System.Array.Empty<Renderer>();
        private float _elapsedTimer;

        public string IntersectionId => intersectionId;
        public SignalLightState CurrentSignal => currentSignal;
        public float CycleSeconds => cycleSeconds;
        public float StopRadiusMeters => junctionStopRadiusMeters;
        public bool HasActiveSignal => hasActiveSignal;

        public void ConfigureFromJunction(RoadJunctionNode node, Renderer[] lampHeads = null, float offsetSeconds = 0f)
        {
            if (node != null)
            {
                intersectionId = node.junctionId;
                junctionStopRadiusMeters = Mathf.Max(6f, node.radiusMeters);
                hasActiveSignal = node.hasTrafficSignal && node.kind != JunctionKind.Roundabout;
                transform.position = node.centerPosition;
            }

            _lampRenderers = lampHeads ?? System.Array.Empty<Renderer>();
            phaseOffsetSeconds = offsetSeconds;
            _elapsedTimer = offsetSeconds;
            EvaluatePhaseImmediate();
        }

        public void ForcePhase(SignalLightState forcedState)
        {
            currentSignal = forcedState;
            UpdateLampVisuals();
        }

        public void StepSignal(float deltaTime)
        {
            if (!hasActiveSignal)
            {
                return;
            }

            _elapsedTimer += Mathf.Max(0f, deltaTime);
            EvaluatePhaseImmediate();
        }

        /// <summary>
        /// Checks whether an approaching vehicle traveling along world direction approachDir has a Green light.
        /// Roundabouts or unsignaled intersections always yield dynamically rather than locking on red.
        /// </summary>
        public bool IsApproachGreen(Vector3 approachDir, out bool isYellow)
        {
            isYellow = false;
            if (!hasActiveSignal)
            {
                return true;
            }

            bool isNorthSouthApproach = Mathf.Abs(approachDir.z) >= Mathf.Abs(approachDir.x);
            if (isNorthSouthApproach)
            {
                isYellow = currentSignal == SignalLightState.YellowNorthSouth;
                return currentSignal == SignalLightState.GreenNorthSouth;
            }

            isYellow = currentSignal == SignalLightState.YellowEastWest;
            return currentSignal == SignalLightState.GreenEastWest;
        }

        /// <summary>
        /// Pedestrians crossing across North-South traffic cross safely while East-West vehicles move, and vice versa.
        /// </summary>
        public bool CanPedestrianCrossAxis(Vector3 walkDirection)
        {
            if (!hasActiveSignal)
            {
                return true;
            }

            // Crossing along X crosses NS roadway -> safe during EW green; crossing along Z crosses EW roadway -> safe during NS green.
            bool walkingAcrossNsRoad = Mathf.Abs(walkDirection.x) >= Mathf.Abs(walkDirection.z);
            return walkingAcrossNsRoad
                ? currentSignal == SignalLightState.GreenEastWest
                : currentSignal == SignalLightState.GreenNorthSouth;
        }

        private void EvaluatePhaseImmediate()
        {
            float cycle = Mathf.Max(8f, cycleSeconds);
            float yellow = Mathf.Clamp(yellowDurationSeconds, 1.5f, cycle * 0.22f);
            float greenEach = (cycle - yellow * 2f) * 0.5f;

            float t = Mathf.Repeat(_elapsedTimer, cycle);
            SignalLightState next;
            if (t < greenEach)
            {
                next = SignalLightState.GreenNorthSouth;
            }
            else if (t < greenEach + yellow)
            {
                next = SignalLightState.YellowNorthSouth;
            }
            else if (t < greenEach * 2f + yellow)
            {
                next = SignalLightState.GreenEastWest;
            }
            else
            {
                next = SignalLightState.YellowEastWest;
            }

            if (next != currentSignal)
            {
                currentSignal = next;
                UpdateLampVisuals();
            }
        }

        private void UpdateLampVisuals()
        {
            if (_lampRenderers == null || _lampRenderers.Length == 0)
            {
                return;
            }

            for (int i = 0; i < _lampRenderers.Length; i++)
            {
                Renderer r = _lampRenderers[i];
                if (r == null || r.sharedMaterial == null) continue;

                // Odd/Even corners roughly alternate NS vs EW approaches
                bool cornerIsNs = (i % 2) == 0;
                Color c;
                if ((cornerIsNs && currentSignal == SignalLightState.GreenNorthSouth) ||
                    (!cornerIsNs && currentSignal == SignalLightState.GreenEastWest))
                {
                    c = new Color(0.25f, 3.2f, 0.55f);
                }
                else if ((cornerIsNs && currentSignal == SignalLightState.YellowNorthSouth) ||
                         (!cornerIsNs && currentSignal == SignalLightState.YellowEastWest))
                {
                    c = new Color(3.4f, 2.2f, 0.15f);
                }
                else
                {
                    c = new Color(3.4f, 0.22f, 0.18f);
                }

                if (r.material.HasProperty("_EmissiveColor"))
                {
                    r.material.SetColor("_EmissiveColor", c);
                }
                else if (r.material.HasProperty("_BaseColor"))
                {
                    r.material.SetColor("_BaseColor", new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b)));
                }
            }
        }
    }
}
