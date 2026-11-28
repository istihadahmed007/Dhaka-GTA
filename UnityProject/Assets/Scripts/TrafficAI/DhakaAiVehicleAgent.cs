using DhakaGta.Core;
using DhakaGta.Data;
using DhakaGta.Vehicles;
using DhakaGta.WorldGen;
using UnityEngine;

namespace DhakaGta.TrafficAI
{
    public enum AiVehicleBehaviorState
    {
        Cruising = 0,
        YieldingAtSignal = 1,
        CongestionQueue = 2,
        Overtaking = 3,
        YieldingToPedestrian = 4,
        YieldingToSiren = 5,
        ParkedCurbside = 6,
        PolicePursuit = 7
    }

    /// <summary>
    /// Autonomous Dhaka AI Vehicle Agent controlling CNGs, Cycle Rickshaws, City/Intercity Buses,
    /// Sedans, SUVs, Motorcycles, and DMP Police Cruisers over real OSM RoadSplineSegments.
    /// Hands control cleanly to DhakaVehicleController if boarded by the player.
    /// </summary>
    public sealed class DhakaAiVehicleAgent : MonoBehaviour
    {
        private readonly RaycastHit[] _sensorHits = new RaycastHit[12];
        private readonly Collider[] _pedSensorBuffer = new Collider[8];

        private DhakaRoadNetworkRouter _router;
        private DhakaVehicleController _manualController;
        private DhakaVehicleLightsAndHorn _lightsAndHorn;
        private DhakaVehicleAudioController _audioController;
        private DhakaVehicleSeatsAndInteract _seats;
        private Rigidbody _rb;

        private RoadSplineSegment _currentSegment;
        private float _distanceAlongSegment;
        private bool _travelForward = true;
        private int _preferredLane;
        private float _currentSpeedMps;
        private float _laneShiftOffsetMeters;
        private float _targetLaneShiftMeters;
        private float _overtakeTimer;
        private float _congestionWaitTimer;
        private float _honkCooldownTimer;

        private Vector3? _policeInterceptTarget;
        private System.Random _rng;

        public VehicleSpecSO Spec { get; private set; }
        public AiVehicleBehaviorState BehaviorState { get; private set; }
        public float CurrentSpeedMetersPerSec => _currentSpeedMps;
        public bool IsParked => BehaviorState == AiVehicleBehaviorState.ParkedCurbside;
        public bool IsPoliceVehicle => Spec != null && Spec.vehicleClass == DhakaVehicleClass.DmpPoliceCruiser;
        public bool IsPoliceResponding { get; private set; }
        public bool IsAssignedToPlayer => _manualController != null && _manualController.IsOccupied;
        public int HonkCount => _lightsAndHorn != null ? _lightsAndHorn.HornHonkCount : 0;

        public void InitializeAgent(
            VehicleSpecSO spec,
            DhakaRoadNetworkRouter router,
            RoadSplineSegment startSegment,
            float startDistance,
            bool travelForward,
            bool startParked,
            int seed)
        {
            Spec = spec;
            _router = router;
            _currentSegment = startSegment;
            _distanceAlongSegment = startDistance;
            _travelForward = travelForward;
            _rng = new System.Random(seed);

            _manualController = GetComponent<DhakaVehicleController>();
            _lightsAndHorn = GetComponent<DhakaVehicleLightsAndHorn>();
            _audioController = GetComponent<DhakaVehicleAudioController>();
            _seats = GetComponent<DhakaVehicleSeatsAndInteract>();
            _rb = GetComponent<Rigidbody>();

            if (_manualController != null)
            {
                _manualController.enabled = false;
            }
            if (_rb != null)
            {
                _rb.isKinematic = true;
                _rb.useGravity = false;
            }

            SetChildLayersForAiTraffic(PhysicsLayers.TrafficVehicle);

            // Cycle rickshaws hug outer curb; sedans/CNGs pick inner/outer LHT lanes
            _preferredLane = spec != null && spec.vehicleClass == DhakaVehicleClass.CycleRickshaw ? 1 : 0;
            _laneShiftOffsetMeters = startParked ? -1.45f : (spec != null && spec.vehicleClass == DhakaVehicleClass.CycleRickshaw ? -0.95f : 0f);
            _targetLaneShiftMeters = _laneShiftOffsetMeters;
            BehaviorState = startParked ? AiVehicleBehaviorState.ParkedCurbside : AiVehicleBehaviorState.Cruising;
            _currentSpeedMps = startParked ? 0f : ResolveDesiredCruiseSpeedMps() * 0.65f;
            IsPoliceResponding = false;
            _policeInterceptTarget = null;

            SnapToRoadPoseImmediate();
        }

        public void EngagePoliceDispatch(Vector3 targetWorldPosition)
        {
            if (!IsPoliceVehicle || IsAssignedToPlayer || IsParked)
            {
                return;
            }

            IsPoliceResponding = true;
            _policeInterceptTarget = targetWorldPosition;
            BehaviorState = AiVehicleBehaviorState.PolicePursuit;
            if (_lightsAndHorn != null && !_lightsAndHorn.IsPoliceSirenActive)
            {
                _lightsAndHorn.TogglePoliceSiren();
            }
        }

        public void ClearPoliceDispatch()
        {
            IsPoliceResponding = false;
            _policeInterceptTarget = null;
            if (!IsParked)
            {
                BehaviorState = AiVehicleBehaviorState.Cruising;
            }
            if (_lightsAndHorn != null && _lightsAndHorn.IsPoliceSirenActive)
            {
                _lightsAndHorn.TogglePoliceSiren();
            }
        }

        public void ConvertToPlayerDrivenControl()
        {
            if (_rb != null)
            {
                _rb.isKinematic = false;
                _rb.useGravity = true;
            }
            if (_manualController != null)
            {
                _manualController.enabled = true;
            }
            SetChildLayersForAiTraffic(PhysicsLayers.Vehicle);
            enabled = false;
        }

        /// <summary>
        /// Steps road navigation, signal checks, forward headway spacing, pedestrian yield, overtaking,
        /// siren yielding, and horn feedback cleanly. Can be ticked at sliced rates by LOD manager.
        /// </summary>
        public void StepAiTraffic(float deltaTime, bool highDetailSensors = true)
        {
            if (_seats != null && _seats.CurrentDriver != null)
            {
                ConvertToPlayerDrivenControl();
                return;
            }

            if (_currentSegment == null || deltaTime <= 0.0001f)
            {
                return;
            }

            if (BehaviorState == AiVehicleBehaviorState.ParkedCurbside)
            {
                _lightsAndHorn?.UpdateLightStates(braking: false, reversing: false);
                return;
            }

            float dt = Mathf.Min(0.15f, deltaTime);
            _honkCooldownTimer = Mathf.Max(0f, _honkCooldownTimer - dt);

            float cruiseTargetMps = ResolveDesiredCruiseSpeedMps();
            bool needBrake = false;
            float allowedSpeedMps = cruiseTargetMps;

            // 1. Evaluate Traffic Signals ahead (emergency police cruisers push cautiously through unless blocked)
            Vector3 fwd = transform.forward;
            TrafficIntersectionNode aheadSignal = _router != null
                ? _router.FindAheadSignalIntersection(transform.position, fwd, 26f)
                : null;

            if (aheadSignal != null && !IsPoliceResponding)
            {
                float distToCenter = Vector3.Distance(transform.position, aheadSignal.transform.position);
                float stopLineDist = aheadSignal.StopRadiusMeters + 3.2f;
                if (distToCenter > aheadSignal.StopRadiusMeters + 0.5f &&
                    !aheadSignal.IsApproachGreen(fwd, out bool isYellow))
                {
                    if (distToCenter <= stopLineDist + 6.5f || isYellow)
                    {
                        allowedSpeedMps = 0f;
                        needBrake = true;
                        BehaviorState = AiVehicleBehaviorState.YieldingAtSignal;
                    }
                }
            }

            // 2. Forward vehicle & pedestrian hazard detection + overtaking / lane-filtering
            if (highDetailSensors)
            {
                EvaluateHazardsAndOvertake(dt, ref allowedSpeedMps, ref needBrake);
            }

            // 3. Smooth speed integration with realistic braking/acceleration
            float accel = allowedSpeedMps < _currentSpeedMps ? 11.5f : 5.2f;
            _currentSpeedMps = Mathf.MoveTowards(_currentSpeedMps, allowedSpeedMps, accel * dt);

            if (BehaviorState == AiVehicleBehaviorState.CongestionQueue || needBrake)
            {
                _congestionWaitTimer += dt;
                if (_congestionWaitTimer > 3.6f && _honkCooldownTimer <= 0f)
                {
                    TriggerHonk();
                }
            }
            else
            {
                _congestionWaitTimer = 0f;
            }

            // 4. Advance spline distance along LHT route + handle intersection transitions
            float deltaDist = _currentSpeedMps * dt;
            _distanceAlongSegment += _travelForward ? deltaDist : -deltaDist;

            if (_travelForward && _distanceAlongSegment >= _currentSegment.totalLengthMeters)
            {
                TransitionSegment(reachedEndNode: true);
            }
            else if (!_travelForward && _distanceAlongSegment <= 0f)
            {
                TransitionSegment(reachedEndNode: false);
            }

            // 5. Smooth lane shift interpolation & orient toward LHT lookahead point
            float defaultLaneBias = Spec != null && Spec.vehicleClass == DhakaVehicleClass.CycleRickshaw ? -0.95f : 0f;
            if (BehaviorState != AiVehicleBehaviorState.Overtaking && BehaviorState != AiVehicleBehaviorState.YieldingToSiren)
            {
                _targetLaneShiftMeters = defaultLaneBias;
            }
            _laneShiftOffsetMeters = Mathf.MoveTowards(_laneShiftOffsetMeters, _targetLaneShiftMeters, 2.2f * dt);

            float lookAheadDist = Mathf.Clamp(_distanceAlongSegment + (_travelForward ? 5.5f : -5.5f), 0f, _currentSegment.totalLengthMeters);
            DhakaRoadNetworkRouter.ComputeLhtLanePose(
                _currentSegment, _distanceAlongSegment, _preferredLane, _travelForward, _laneShiftOffsetMeters,
                out Vector3 targetPos, out Vector3 travelTan);

            DhakaRoadNetworkRouter.ComputeLhtLanePose(
                _currentSegment, lookAheadDist, _preferredLane, _travelForward, _laneShiftOffsetMeters,
                out Vector3 lookPos, out _);

            // Ground terrain snap cleanly onto asphalt deck / bridge surface
            targetPos.y = ResolveRoadSurfaceY(targetPos);
            Vector3 desiredForward = (lookPos - targetPos);
            desiredForward.y = 0f;
            if (desiredForward.sqrMagnitude < 0.01f) desiredForward = travelTan;

            transform.position = Vector3.Lerp(transform.position, targetPos, Mathf.Clamp01(dt * 12f));
            Quaternion desiredRot = Quaternion.LookRotation(desiredForward.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRot, 165f * dt);

            _lightsAndHorn?.UpdateLightStates(braking: needBrake || _currentSpeedMps < 0.4f, reversing: false);
            _audioController?.UpdateAudio(_currentSpeedMps * 3.6f, needBrake ? 0f : 0.6f, groundedWheelCount: Spec != null ? Spec.wheelCount : 4);
        }

        private void EvaluateHazardsAndOvertake(float dt, ref float allowedSpeedMps, ref bool needBrake)
        {
            float halfLen = Spec != null ? Spec.chassisSizeMeters.z * 0.5f : 2.1f;
            Vector3 sensorOrigin = transform.position + Vector3.up * 0.85f + transform.forward * (halfLen * 0.4f);

            // A. Pedestrian & Player crossing safety cone
            int pedMask = (1 << PhysicsLayers.Npc) | (1 << PhysicsLayers.Player);
            int pedHits = Physics.OverlapSphereNonAlloc(sensorOrigin + transform.forward * 4.5f, 2.4f, _pedSensorBuffer, pedMask);
            if (pedHits > 0)
            {
                allowedSpeedMps = 0f;
                needBrake = true;
                BehaviorState = AiVehicleBehaviorState.YieldingToPedestrian;
                if (_honkCooldownTimer <= 0f && _currentSpeedMps > 1.2f)
                {
                    TriggerHonk();
                }
                return;
            }

            // B. Forward vehicle headway & overtaking evaluation
            int vehMask = (1 << PhysicsLayers.TrafficVehicle) | (1 << PhysicsLayers.Vehicle) | (1 << PhysicsLayers.Player);
            int hitCount = Physics.SphereCastNonAlloc(sensorOrigin, 1.05f, transform.forward, _sensorHits, 18.5f, vehMask, QueryTriggerInteraction.Ignore);

            float closestGap = float.MaxValue;
            float leadSpeedMps = cruiseMaxFallback();
            bool leadIsPoliceSiren = false;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit h = _sensorHits[i];
                if (h.collider == null || h.collider.transform.IsChildOf(transform)) continue;

                float gap = Mathf.Max(0f, h.distance - halfLen * 0.6f);
                if (gap < closestGap)
                {
                    closestGap = gap;
                    var peerAi = h.collider.GetComponentInParent<DhakaAiVehicleAgent>();
                    if (peerAi != null)
                    {
                        leadSpeedMps = peerAi.CurrentSpeedMetersPerSec;
                        leadIsPoliceSiren = peerAi.IsPoliceResponding;
                    }
                    else
                    {
                        var manualVeh = h.collider.GetComponentInParent<DhakaVehicleController>();
                        leadSpeedMps = manualVeh != null ? Mathf.Max(0f, manualVeh.ForwardSpeedKph / 3.6f) : 0f;
                    }
                }
            }

            if (closestGap < 4.5f)
            {
                // Bumper-to-bumper queue or motorcycle filtering
                if (Spec != null && Spec.vehicleClass == DhakaVehicleClass.Motorcycle && _currentSegment.widthMeters >= 7f)
                {
                    _targetLaneShiftMeters = 0.68f;
                    allowedSpeedMps = Mathf.Min(allowedSpeedMps, 4.2f);
                    BehaviorState = AiVehicleBehaviorState.Overtaking;
                }
                else
                {
                    allowedSpeedMps = 0f;
                    needBrake = true;
                    BehaviorState = AiVehicleBehaviorState.CongestionQueue;
                }
            }
            else if (closestGap < 14.5f)
            {
                // Attempt LHT right-side overtake if lead vehicle is slow (Rickshaw/Parked/Queue) and road allows passing
                bool canPass = _currentSegment.lanes >= 2 &&
                               _currentSegment.widthMeters >= 7.2f &&
                               leadSpeedMps < cruiseMaxFallback() * 0.62f &&
                               !leadIsPoliceSiren &&
                               BehaviorState != AiVehicleBehaviorState.YieldingAtSignal;

                if (canPass && HasClearOvertakeLane())
                {
                    BehaviorState = AiVehicleBehaviorState.Overtaking;
                    _targetLaneShiftMeters = 1.65f; // LHT overtake toward road median/center
                    _overtakeTimer = 2.6f;
                    allowedSpeedMps = Mathf.Min(cruiseMaxFallback() * 1.15f, allowedSpeedMps);
                }
                else
                {
                    allowedSpeedMps = Mathf.Min(allowedSpeedMps, Mathf.Max(1.0f, leadSpeedMps * 0.92f));
                    needBrake = _currentSpeedMps > allowedSpeedMps + 1.0f;
                }
            }
            else if (BehaviorState == AiVehicleBehaviorState.Overtaking)
            {
                _overtakeTimer -= dt;
                if (_overtakeTimer <= 0f)
                {
                    BehaviorState = IsPoliceResponding ? AiVehicleBehaviorState.PolicePursuit : AiVehicleBehaviorState.Cruising;
                    _targetLaneShiftMeters = 0f;
                }
            }
            else if (!IsPoliceResponding)
            {
                BehaviorState = AiVehicleBehaviorState.Cruising;
            }
        }

        private bool HasClearOvertakeLane()
        {
            Vector3 probeOrigin = transform.position + transform.right * 2.1f + Vector3.up * 0.9f;
            int mask = (1 << PhysicsLayers.TrafficVehicle) | (1 << PhysicsLayers.Vehicle) | (1 << PhysicsLayers.Building);
            return !Physics.CheckSphere(probeOrigin + transform.forward * 5.5f, 1.35f, mask, QueryTriggerInteraction.Ignore);
        }

        private float ResolveRoadSurfaceY(Vector3 xzPos)
        {
            Vector3 rayStart = new Vector3(xzPos.x, xzPos.y + 1.8f, xzPos.z);
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 5f, PhysicsLayers.GroundMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y + 0.04f;
            }
            return xzPos.y + 0.04f;
        }

        private void TransitionSegment(bool reachedEndNode)
        {
            if (_router == null) return;
            RoadTransitionChoice next = _router.PickNextSegment(
                _currentSegment,
                reachedEndNode,
                transform.forward,
                _rng,
                _policeInterceptTarget);

            if (next.NextSegment != null)
            {
                _currentSegment = next.NextSegment;
                _distanceAlongSegment = next.StartDistanceMeters;
                _travelForward = next.TravelForward;
            }
        }

        private void SnapToRoadPoseImmediate()
        {
            if (_currentSegment == null) return;
            DhakaRoadNetworkRouter.ComputeLhtLanePose(
                _currentSegment, _distanceAlongSegment, _preferredLane, _travelForward, _laneShiftOffsetMeters,
                out Vector3 pos, out Vector3 tan);
            pos.y = ResolveRoadSurfaceY(pos);
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(tan, Vector3.up));
        }

        private void TriggerHonk()
        {
            _honkCooldownTimer = 4.2f;
            _lightsAndHorn?.SoundHorn();
        }

        private float ResolveDesiredCruiseSpeedMps()
        {
            if (IsPoliceResponding) return 18.5f;
            float maxKph = Spec != null ? Spec.maxSpeedKph : 55f;
            float roadCapKph = _currentSegment != null && _currentSegment.maxSpeedKph > 10f ? _currentSegment.maxSpeedKph : 50f;
            float ratio = Spec != null ? Spec.vehicleClass switch
            {
                DhakaVehicleClass.CycleRickshaw => 0.24f,
                DhakaVehicleClass.CngAutoRickshaw => 0.62f,
                DhakaVehicleClass.CityBus => 0.56f,
                DhakaVehicleClass.IntercityBus => 0.64f,
                DhakaVehicleClass.Motorcycle => 0.78f,
                _ => 0.65f
            } : 0.6f;

            return Mathf.Min(maxKph, roadCapKph) / 3.6f * ratio;
        }

        private float cruiseMaxFallback() => ResolveDesiredCruiseSpeedMps();

        private void SetChildLayersForAiTraffic(int targetLayer)
        {
            gameObject.layer = targetLayer;
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.gameObject.layer != PhysicsLayers.VehicleWheel)
                {
                    t.gameObject.layer = targetLayer;
                }
            }
        }
    }
}
