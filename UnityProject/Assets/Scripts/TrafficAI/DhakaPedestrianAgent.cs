using System.Collections.Generic;
using DhakaGta.Core;
using UnityEngine;
using UnityEngine.AI;

namespace DhakaGta.TrafficAI
{
    public enum NpcBehaviorState
    {
        WalkingSidewalk = 0,
        WaitingAtCrosswalk = 1,
        CrossingRoad = 2,
        ShoppingAtStall = 3,
        TeaStallHangout = 4,
        StreetVending = 5,
        ConversingPair = 6,
        AvoidingVehicle = 7,
        FleeingDanger = 8,
        PoliceRespond = 9
    }

    /// <summary>
    /// Living Dhaka NPC pedestrian state machine supporting Unity NavMeshAgent + sidewalk fallback steering,
    /// crosswalk yielding, tea stall / shop / street vending loops, two-person conversations,
    /// pedestrian avoidance, articulated procedural/Animator gait animation, and police/civilian danger response.
    /// </summary>
    public sealed class DhakaPedestrianAgent : MonoBehaviour
    {
        private static readonly List<DhakaPedestrianAgent> ActiveRegistry = new List<DhakaPedestrianAgent>(64);
        private static readonly Collider[] DangerVehicleBuffer = new Collider[6];

        private DhakaNavMeshRuntimeBaker _navBaker;
        private PedestrianRigJoints _joints;
        private NavMeshAgent _navAgent;
        private Animator _animator;
        private CharacterController _ccFallback;
        private System.Random _rng;

        private Vector3 _targetPos;
        private Vector3 _lookDir = Vector3.forward;
        private Vector3 _fleeSourcePos;
        private DhakaPedestrianAgent _conversationPartner;
        private TrafficIntersectionNode _crossingSignal;

        private float _stateTimer;
        private float _gaitPhase;
        private float _currentSpeedMps;
        private float _cooldownSocial;
        private bool _isStationaryVendor;

        public PedestrianWardrobeArchetype Archetype { get; private set; }
        public NpcBehaviorState BehaviorState { get; private set; }
        public float CurrentSpeedMetersPerSec => _currentSpeedMps;
        public float GaitPhaseRadians => _gaitPhase;
        public bool IsPoliceOfficer => Archetype == PedestrianWardrobeArchetype.DmpPoliceOfficer;
        public DhakaPedestrianAgent ConversationPartner => _conversationPartner;

        private void OnEnable()
        {
            if (!ActiveRegistry.Contains(this))
            {
                ActiveRegistry.Add(this);
            }
        }

        private void OnDisable()
        {
            ClearConversationPartner();
            ActiveRegistry.Remove(this);
        }

        public void InitializeNpc(
            PedestrianWardrobeArchetype archetype,
            DhakaNavMeshRuntimeBaker navBaker,
            Vector3 spawnPos,
            int seed,
            bool stationaryVendor = false)
        {
            Archetype = archetype;
            _navBaker = navBaker;
            _rng = new System.Random(seed);
            _isStationaryVendor = stationaryVendor;

            _joints = GetComponent<PedestrianRigJoints>();
            _animator = GetComponentInChildren<Animator>(true);
            _navAgent = GetComponent<NavMeshAgent>();
            _ccFallback = GetComponent<CharacterController>();

            gameObject.layer = PhysicsLayers.Npc;
            transform.position = SnapToGround(spawnPos);
            _lookDir = Quaternion.Euler(0f, (float)(_rng.NextDouble() * 360.0), 0f) * Vector3.forward;
            transform.rotation = Quaternion.LookRotation(_lookDir, Vector3.up);

            if (_navAgent != null && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
            {
                _navAgent.enabled = true;
                _navAgent.Warp(hit.position);
            }
            else if (_navAgent != null)
            {
                _navAgent.enabled = false;
            }

            _cooldownSocial = (float)_rng.NextDouble() * 5f;
            if (stationaryVendor)
            {
                BehaviorState = NpcBehaviorState.StreetVending;
                _stateTimer = 999f;
            }
            else
            {
                BehaviorState = NpcBehaviorState.WalkingSidewalk;
                PickNextSidewalkOrPoiDestination();
            }
        }

        public void ReactToDanger(Vector3 threatWorldPos, float threatRadius)
        {
            float dist = Vector3.Distance(transform.position, threatWorldPos);
            if (dist > threatRadius * 1.45f)
            {
                return;
            }

            ClearConversationPartner();
            _fleeSourcePos = threatWorldPos;

            if (IsPoliceOfficer)
            {
                BehaviorState = NpcBehaviorState.PoliceRespond;
                _targetPos = threatWorldPos + (transform.position - threatWorldPos).normalized * 6f;
                _stateTimer = 14f;
            }
            else
            {
                BehaviorState = NpcBehaviorState.FleeingDanger;
                Vector3 away = (transform.position - threatWorldPos);
                away.y = 0f;
                if (away.sqrMagnitude < 0.01f) away = transform.forward;
                _targetPos = transform.position + away.normalized * 32f;
                _stateTimer = 7.5f;
            }
        }

        public void StepNpc(float deltaTime, bool highDetail)
        {
            float dt = Mathf.Clamp(deltaTime, 0.001f, 0.25f);
            _cooldownSocial = Mathf.Max(0f, _cooldownSocial - dt);

            // 1. Poll active Urban Danger Bus automatically
            IReadOnlyList<CityDangerEvent> dangers = CityDangerBus.ActiveDangerEvents;
            if (dangers != null && dangers.Count > 0 && BehaviorState != NpcBehaviorState.FleeingDanger && BehaviorState != NpcBehaviorState.PoliceRespond)
            {
                for (int i = 0; i < dangers.Count; i++)
                {
                    if (Vector3.Distance(transform.position, dangers[i].WorldPosition) <= dangers[i].RadiusMeters)
                    {
                        ReactToDanger(dangers[i].WorldPosition, dangers[i].RadiusMeters);
                        break;
                    }
                }
            }

            float targetMoveSpeed = 0f;

            switch (BehaviorState)
            {
                case NpcBehaviorState.StreetVending:
                    targetMoveSpeed = 0f;
                    // Hawkers call & wave slightly while tending pushcart/basket
                    break;

                case NpcBehaviorState.TeaStallHangout:
                case NpcBehaviorState.ShoppingAtStall:
                    targetMoveSpeed = 0f;
                    _stateTimer -= dt;
                    FaceDirection(_lookDir, dt);
                    if (_stateTimer <= 0f)
                    {
                        BehaviorState = NpcBehaviorState.WalkingSidewalk;
                        PickNextSidewalkOrPoiDestination();
                    }
                    break;

                case NpcBehaviorState.ConversingPair:
                    targetMoveSpeed = 0f;
                    _stateTimer -= dt;
                    if (_conversationPartner != null)
                    {
                        FaceDirection(_conversationPartner.transform.position - transform.position, dt);
                    }
                    if (_stateTimer <= 0f || _conversationPartner == null || !_conversationPartner.gameObject.activeInHierarchy)
                    {
                        ClearConversationPartner();
                        BehaviorState = NpcBehaviorState.WalkingSidewalk;
                        _cooldownSocial = 8f;
                        PickNextSidewalkOrPoiDestination();
                    }
                    break;

                case NpcBehaviorState.WaitingAtCrosswalk:
                    targetMoveSpeed = 0f;
                    _stateTimer -= dt;
                    FaceDirection(_targetPos - transform.position, dt);
                    bool signalOk = _crossingSignal == null || _crossingSignal.CanPedestrianCrossAxis(_targetPos - transform.position);
                    bool roadClear = !IsFastVehicleApproaching(13.5f);
                    if ((signalOk && roadClear) || _stateTimer <= -3.5f)
                    {
                        BehaviorState = NpcBehaviorState.CrossingRoad;
                        _stateTimer = 10f;
                    }
                    break;

                case NpcBehaviorState.CrossingRoad:
                    bool urgentCar = IsFastVehicleApproaching(11.0f);
                    targetMoveSpeed = urgentCar ? 3.2f : 1.65f;
                    AdvanceTowardTarget(targetMoveSpeed, dt);
                    if (HorizontalDistance(transform.position, _targetPos) < 1.15f || _stateTimer <= 0f)
                    {
                        BehaviorState = NpcBehaviorState.WalkingSidewalk;
                        _crossingSignal = null;
                        PickNextSidewalkOrPoiDestination();
                    }
                    _stateTimer -= dt;
                    break;

                case NpcBehaviorState.FleeingDanger:
                    targetMoveSpeed = 4.95f;
                    _stateTimer -= dt;
                    AdvanceTowardTarget(targetMoveSpeed, dt);
                    if (_stateTimer <= 0f)
                    {
                        BehaviorState = _isStationaryVendor ? NpcBehaviorState.StreetVending : NpcBehaviorState.WalkingSidewalk;
                        if (!_isStationaryVendor) PickNextSidewalkOrPoiDestination();
                    }
                    break;

                case NpcBehaviorState.PoliceRespond:
                    targetMoveSpeed = HorizontalDistance(transform.position, _targetPos) > 4.5f ? 4.4f : 0f;
                    _stateTimer -= dt;
                    if (targetMoveSpeed > 0.1f) AdvanceTowardTarget(targetMoveSpeed, dt);
                    else FaceDirection(_fleeSourcePos - transform.position, dt);

                    if (_stateTimer <= 0f && CityDangerBus.CurrentWantedLevel == 0)
                    {
                        BehaviorState = NpcBehaviorState.WalkingSidewalk;
                        PickNextSidewalkOrPoiDestination();
                    }
                    break;

                case NpcBehaviorState.AvoidingVehicle:
                    targetMoveSpeed = 2.6f;
                    _stateTimer -= dt;
                    AdvanceTowardTarget(targetMoveSpeed, dt);
                    if (_stateTimer <= 0f)
                    {
                        BehaviorState = NpcBehaviorState.WalkingSidewalk;
                        PickNextSidewalkOrPoiDestination();
                    }
                    break;

                case NpcBehaviorState.WalkingSidewalk:
                default:
                    targetMoveSpeed = IsPoliceOfficer ? 1.55f : 1.42f;
                    _stateTimer -= dt;

                    // Immediate sidewalk vehicle dodge check
                    if (highDetail && TryGetImmediateVehicleThreat(out Vector3 carPos))
                    {
                        BehaviorState = NpcBehaviorState.AvoidingVehicle;
                        Vector3 sidestep = (transform.position - carPos);
                        sidestep.y = 0f;
                        _targetPos = transform.position + (sidestep.sqrMagnitude > 0.01f ? sidestep.normalized : -transform.right) * 3.5f;
                        _stateTimer = 1.6f;
                        break;
                    }

                    // Spontaneous conversation with nearby free pedestrian
                    if (highDetail && _cooldownSocial <= 0f && !IsPoliceOfficer && TryPairForConversation())
                    {
                        break;
                    }

                    AdvanceTowardTarget(targetMoveSpeed, dt);

                    if (HorizontalDistance(transform.position, _targetPos) < 1.35f || _stateTimer <= 0f)
                    {
                        DecideNextDestinationOrActivity();
                    }
                    break;
            }

            _currentSpeedMps = Mathf.Lerp(_currentSpeedMps, targetMoveSpeed, Mathf.Clamp01(dt * 10f));
            UpdateArticulatedAnimation(dt, highDetail);
        }

        private bool TryPairForConversation()
        {
            if (_rng == null || _rng.NextDouble() > 0.06)
            {
                return false;
            }

            for (int i = 0; i < ActiveRegistry.Count; i++)
            {
                DhakaPedestrianAgent other = ActiveRegistry[i];
                if (other == null || other == this || other.BehaviorState != NpcBehaviorState.WalkingSidewalk || other.IsPoliceOfficer)
                {
                    continue;
                }

                float d = HorizontalDistance(transform.position, other.transform.position);
                if (d >= 1.1f && d <= 2.7f)
                {
                    float dur = 5.5f + (float)_rng.NextDouble() * 4.5f;
                    BeginConversationWith(other, dur);
                    other.BeginConversationWith(this, dur);
                    return true;
                }
            }
            return false;
        }

        public void BeginConversationWith(DhakaPedestrianAgent partner, float durationSeconds)
        {
            _conversationPartner = partner;
            BehaviorState = NpcBehaviorState.ConversingPair;
            _stateTimer = durationSeconds;
        }

        private void ClearConversationPartner()
        {
            if (_conversationPartner != null)
            {
                var p = _conversationPartner;
                _conversationPartner = null;
                if (p._conversationPartner == this)
                {
                    p._conversationPartner = null;
                }
            }
        }

        private void DecideNextDestinationOrActivity()
        {
            // Check nearby Crosswalks first
            if (_navBaker != null && _navBaker.Crosswalks.Count > 0 && _rng.NextDouble() < 0.34)
            {
                for (int i = 0; i < _navBaker.Crosswalks.Count; i++)
                {
                    PedestrianCrosswalkLink cw = _navBaker.Crosswalks[i];
                    if (HorizontalDistance(transform.position, cw.EndA) < 10f)
                    {
                        _targetPos = cw.EndB;
                        _crossingSignal = cw.ParentSignal;
                        BehaviorState = NpcBehaviorState.WaitingAtCrosswalk;
                        _stateTimer = 3.0f;
                        return;
                    }
                    if (HorizontalDistance(transform.position, cw.EndB) < 10f)
                    {
                        _targetPos = cw.EndA;
                        _crossingSignal = cw.ParentSignal;
                        BehaviorState = NpcBehaviorState.WaitingAtCrosswalk;
                        _stateTimer = 3.0f;
                        return;
                    }
                }
            }

            // Check nearby Tea Stalls & Shops
            if (_navBaker != null && _navBaker.ActivityPois.Count > 0 && _rng.NextDouble() < 0.42)
            {
                for (int i = 0; i < _navBaker.ActivityPois.Count; i++)
                {
                    PedestrianActivityPoi poi = _navBaker.ActivityPois[i];
                    if (HorizontalDistance(transform.position, poi.WorldPosition) < 12f)
                    {
                        _lookDir = poi.LookDirection.sqrMagnitude > 0.01f ? poi.LookDirection : Vector3.forward;
                        BehaviorState = poi.Kind == CityPoiKind.TeaStall
                            ? NpcBehaviorState.TeaStallHangout
                            : NpcBehaviorState.ShoppingAtStall;
                        _stateTimer = 6.5f + (float)_rng.NextDouble() * 6.5f;
                        return;
                    }
                }
            }

            PickNextSidewalkOrPoiDestination();
        }

        private void PickNextSidewalkOrPoiDestination()
        {
            if (_navBaker != null)
            {
                if (_navBaker.ActivityPois.Count > 0 && _rng.NextDouble() < 0.28)
                {
                    var poi = _navBaker.ActivityPois[_rng.Next(_navBaker.ActivityPois.Count)];
                    _targetPos = poi.WorldPosition;
                    _stateTimer = 16f;
                    return;
                }

                if (_navBaker.SidewalkWaypoints.Count > 0)
                {
                    Vector3 candidate = _navBaker.SidewalkWaypoints[_rng.Next(_navBaker.SidewalkWaypoints.Count)];
                    // Prefer nearby reachable waypoint along city streets
                    for (int attempt = 0; attempt < 5; attempt++)
                    {
                        Vector3 test = _navBaker.SidewalkWaypoints[_rng.Next(_navBaker.SidewalkWaypoints.Count)];
                        float d = HorizontalDistance(transform.position, test);
                        if (d > 6f && d < 65f)
                        {
                            candidate = test;
                            break;
                        }
                    }
                    _targetPos = candidate;
                    _stateTimer = 18f;
                    return;
                }
            }

            float angle = (float)(_rng.NextDouble() * Mathf.PI * 2.0);
            float radius = 12f + (float)_rng.NextDouble() * 14f;
            _targetPos = transform.position + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            _stateTimer = 12f;
        }

        private void AdvanceTowardTarget(float speedMps, float dt)
        {
            if (_navAgent != null && _navAgent.enabled && _navAgent.isOnNavMesh)
            {
                _navAgent.speed = speedMps;
                _navAgent.SetDestination(_targetPos);
                if (_navAgent.desiredVelocity.sqrMagnitude > 0.05f)
                {
                    FaceDirection(_navAgent.desiredVelocity, dt);
                }
                return;
            }

            Vector3 toTarget = _targetPos - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.001f) return;

            Vector3 dir = toTarget.normalized;

            // Pedestrian separation avoidance force
            Vector3 separation = Vector3.zero;
            for (int i = 0; i < ActiveRegistry.Count; i++)
            {
                DhakaPedestrianAgent peer = ActiveRegistry[i];
                if (peer == null || peer == this) continue;
                Vector3 diff = transform.position - peer.transform.position;
                diff.y = 0f;
                float dSq = diff.sqrMagnitude;
                if (dSq > 0.001f && dSq < 1.15f * 1.15f)
                {
                    separation += diff / dSq;
                }
            }

            Vector3 steerDir = (dir + Vector3.ClampMagnitude(separation * 0.45f, 0.7f)).normalized;
            FaceDirection(steerDir, dt);

            Vector3 step = steerDir * (speedMps * dt);
            if (_ccFallback != null && _ccFallback.enabled)
            {
                _ccFallback.Move(step + Vector3.down * (9.8f * dt));
            }
            else
            {
                Vector3 nextPos = transform.position + step;
                nextPos.y = SnapToGround(nextPos).y;
                transform.position = nextPos;
            }
        }

        private bool IsFastVehicleApproaching(float radiusMeters)
        {
            int vehMask = (1 << PhysicsLayers.TrafficVehicle) | (1 << PhysicsLayers.Vehicle);
            int hits = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * 0.8f, radiusMeters, DangerVehicleBuffer, vehMask);
            for (int i = 0; i < hits; i++)
            {
                Collider c = DangerVehicleBuffer[i];
                if (c == null) continue;
                var aiVeh = c.GetComponentInParent<DhakaAiVehicleAgent>();
                if (aiVeh != null && aiVeh.CurrentSpeedMetersPerSec > 2.2f)
                {
                    return true;
                }
                var rb = c.GetComponentInParent<Rigidbody>();
                if (rb != null && rb.linearVelocity.magnitude > 2.2f)
                {
                    return true;
                }
            }
            return false;
        }

        private bool TryGetImmediateVehicleThreat(out Vector3 vehicleWorldPos)
        {
            vehicleWorldPos = Vector3.zero;
            int vehMask = (1 << PhysicsLayers.TrafficVehicle) | (1 << PhysicsLayers.Vehicle);
            int hits = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * 0.8f, 4.2f, DangerVehicleBuffer, vehMask);
            for (int i = 0; i < hits; i++)
            {
                Collider c = DangerVehicleBuffer[i];
                if (c == null) continue;
                var aiVeh = c.GetComponentInParent<DhakaAiVehicleAgent>();
                if (aiVeh != null && aiVeh.CurrentSpeedMetersPerSec > 2.8f)
                {
                    vehicleWorldPos = c.transform.position;
                    return true;
                }
            }
            return false;
        }

        private void UpdateArticulatedAnimation(float dt, bool highDetail)
        {
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                _animator.SetFloat("MoveSpeed", _currentSpeedMps);
                _animator.SetFloat("MoveZ", _currentSpeedMps);
                _animator.SetBool("IsGrounded", true);
            }

            if (_joints == null || !highDetail)
            {
                return;
            }

            if (_currentSpeedMps > 0.2f)
            {
                float strideFreq = _currentSpeedMps * 2.15f;
                _gaitPhase += strideFreq * dt;
                float swing = Mathf.Sin(_gaitPhase);
                float armAmp = Mathf.Clamp(_currentSpeedMps * 9.5f, 14f, 44f);
                float legAmp = Mathf.Clamp(_currentSpeedMps * 11f, 16f, 46f);

                if (_joints.LeftUpperLeg != null) _joints.LeftUpperLeg.localRotation = Quaternion.Euler(swing * legAmp, 0f, 0f);
                if (_joints.RightUpperLeg != null) _joints.RightUpperLeg.localRotation = Quaternion.Euler(-swing * legAmp, 0f, 0f);
                if (_joints.LeftLowerLeg != null) _joints.LeftLowerLeg.localRotation = Quaternion.Euler(Mathf.Max(0f, -swing) * 28f, 0f, 0f);
                if (_joints.RightLowerLeg != null) _joints.RightLowerLeg.localRotation = Quaternion.Euler(Mathf.Max(0f, swing) * 28f, 0f, 0f);
                if (_joints.LeftUpperArm != null) _joints.LeftUpperArm.localRotation = Quaternion.Euler(-swing * armAmp, 0f, 0f);
                if (_joints.RightUpperArm != null) _joints.RightUpperArm.localRotation = Quaternion.Euler(swing * armAmp, 0f, 0f);
                if (_joints.Hips != null) _joints.Hips.localPosition = new Vector3(0f, 0.92f + Mathf.Abs(Mathf.Cos(_gaitPhase)) * 0.025f, 0f);
            }
            else if (BehaviorState == NpcBehaviorState.ConversingPair ||
                     BehaviorState == NpcBehaviorState.StreetVending ||
                     BehaviorState == NpcBehaviorState.TeaStallHangout)
            {
                _gaitPhase += dt * 3.2f;
                float gesture = Mathf.Sin(_gaitPhase) * 22f;
                if (_joints.LeftUpperLeg != null) _joints.LeftUpperLeg.localRotation = Quaternion.identity;
                if (_joints.RightUpperLeg != null) _joints.RightUpperLeg.localRotation = Quaternion.identity;
                if (_joints.RightUpperArm != null)
                {
                    float lift = BehaviorState == NpcBehaviorState.TeaStallHangout ? -58f : -36f;
                    _joints.RightUpperArm.localRotation = Quaternion.Euler(lift + gesture * 0.45f, 0f, -8f);
                }
                if (_joints.RightForearm != null)
                {
                    _joints.RightForearm.localRotation = Quaternion.Euler(-48f + gesture * 0.35f, 0f, 0f);
                }
                if (_joints.Head != null)
                {
                    _joints.Head.localRotation = Quaternion.Euler(Mathf.Sin(_gaitPhase * 0.7f) * 6f, Mathf.Cos(_gaitPhase * 0.4f) * 10f, 0f);
                }
            }
            else
            {
                // Relaxed standing idle
                if (_joints.LeftUpperLeg != null) _joints.LeftUpperLeg.localRotation = Quaternion.identity;
                if (_joints.RightUpperLeg != null) _joints.RightUpperLeg.localRotation = Quaternion.identity;
                if (_joints.LeftUpperArm != null) _joints.LeftUpperArm.localRotation = Quaternion.identity;
                if (_joints.RightUpperArm != null) _joints.RightUpperArm.localRotation = Quaternion.identity;
            }
        }

        private void FaceDirection(Vector3 dir, float dt)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return;
            Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, 360f * dt);
        }

        private Vector3 SnapToGround(Vector3 worldPos)
        {
            if (Physics.Raycast(worldPos + Vector3.up * 2.5f, Vector3.down, out RaycastHit hit, 8f, PhysicsLayers.GroundMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }
            return worldPos;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
