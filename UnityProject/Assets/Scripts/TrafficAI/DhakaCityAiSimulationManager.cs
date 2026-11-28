using System.Collections.Generic;
using DhakaGta.Core;
using DhakaGta.Data;
using DhakaGta.Vehicles;
using DhakaGta.WorldGen;
using UnityEngine;

namespace DhakaGta.TrafficAI
{
    /// <summary>
    /// Pooled, distance-budgeted Living Dhaka City Simulation orchestrating AI Vehicles, Traffic Lights,
    /// Bangladeshi Pedestrians (NavMesh + Sidewalk), Street Vendors, Tea Stall Patrons, and DMP Police.
    /// Adjusts traffic/pedestrian counts dynamically by DistrictDefinition and Time of Day while slicing update rates by distance.
    /// </summary>
    public sealed class DhakaCityAiSimulationManager : MonoBehaviour
    {
        [Header("Scene Topology References")]
        [SerializeField] private RoadSplineSystem roadSplineSystem;
        [SerializeField] private IntersectionGenerator intersectionGenerator;
        [SerializeField] private Transform simulationFocusTarget;
        [SerializeField] private RuntimeAnimatorController optionalPedestrianAnimatorController;
        [SerializeField] private VehicleSpecSO[] vehicleCatalog = System.Array.Empty<VehicleSpecSO>();

        [Header("District, Density & Pool Budgets")]
        [SerializeField] private string activeDistrictId = "gulshan";
        [SerializeField, Range(4, 64)] private int maxVehiclePoolSize = 36;
        [SerializeField, Range(0, 16)] private int parkedVehicleCount = 6;
        [SerializeField, Range(6, 96)] private int maxPedestrianPoolSize = 48;

        [Header("Distance-Based Performance LODs")]
        [SerializeField] private float nearDetailRadiusMeters = 48f;
        [SerializeField] private float midDetailRadiusMeters = 135f;
        [SerializeField] private float recycleDistanceMeters = 240f;

        private readonly DhakaRoadNetworkRouter _roadRouter = new DhakaRoadNetworkRouter();
        private readonly List<TrafficIntersectionNode> _signalNodes = new List<TrafficIntersectionNode>();
        private readonly List<DhakaAiVehicleAgent> _vehiclePool = new List<DhakaAiVehicleAgent>();
        private readonly List<DhakaPedestrianAgent> _pedestrianPool = new List<DhakaPedestrianAgent>();
        private readonly Dictionary<DhakaVehicleClass, VehicleSpecSO> _specByClass = new Dictionary<DhakaVehicleClass, VehicleSpecSO>();

        private DhakaNavMeshRuntimeBaker _navBaker;
        private DhakaPoliceDispatchController _policeDispatch;
        private Transform _vehiclesPoolRoot;
        private Transform _pedestriansPoolRoot;
        private int _frameCounter;
        private bool _initialized;

        public DhakaRoadNetworkRouter RoadRouter => _roadRouter;
        public DhakaNavMeshRuntimeBaker NavBaker => _navBaker;
        public DhakaPoliceDispatchController PoliceDispatch => _policeDispatch;
        public IReadOnlyList<TrafficIntersectionNode> SignalNodes => _signalNodes;
        public IReadOnlyList<DhakaAiVehicleAgent> ActiveVehicles => _vehiclePool;
        public IReadOnlyList<DhakaPedestrianAgent> ActivePedestrians => _pedestrianPool;

        public int CountActiveMovingVehicles()
        {
            int c = 0;
            for (int i = 0; i < _vehiclePool.Count; i++)
            {
                if (_vehiclePool[i] != null && _vehiclePool[i].gameObject.activeSelf && !_vehiclePool[i].IsParked) c++;
            }
            return c;
        }

        public int CountActiveParkedVehicles()
        {
            int c = 0;
            for (int i = 0; i < _vehiclePool.Count; i++)
            {
                if (_vehiclePool[i] != null && _vehiclePool[i].gameObject.activeSelf && _vehiclePool[i].IsParked) c++;
            }
            return c;
        }

        public int CountActivePedestrians()
        {
            int c = 0;
            for (int i = 0; i < _pedestrianPool.Count; i++)
            {
                if (_pedestrianPool[i] != null && _pedestrianPool[i].gameObject.activeSelf) c++;
            }
            return c;
        }

        private void Awake()
        {
            GameEvents.OnDistrictChanged += HandleDistrictChanged;
        }

        private void OnDestroy()
        {
            GameEvents.OnDistrictChanged -= HandleDistrictChanged;
        }

        private void HandleDistrictChanged(string districtId, string _)
        {
            if (!string.IsNullOrWhiteSpace(districtId))
            {
                activeDistrictId = districtId;
            }
        }

        public void BindSceneSetup(
            RoadSplineSystem splines,
            IntersectionGenerator intersections,
            Transform focusTarget,
            VehicleSpecSO[] specs,
            string initialDistrictId = "gulshan",
            RuntimeAnimatorController humanoidController = null)
        {
            roadSplineSystem = splines;
            intersectionGenerator = intersections;
            simulationFocusTarget = focusTarget;
            vehicleCatalog = specs ?? System.Array.Empty<VehicleSpecSO>();
            activeDistrictId = string.IsNullOrWhiteSpace(initialDistrictId) ? "gulshan" : initialDistrictId;
            optionalPedestrianAnimatorController = humanoidController;
        }

        public void ConfigureAndWarmPools(
            RoadSplineSystem splines,
            IntersectionGenerator intersections,
            Transform focusTarget,
            VehicleSpecSO[] specs,
            string initialDistrictId = "gulshan",
            RuntimeAnimatorController humanoidController = null,
            bool bakeNavMesh = true)
        {
            roadSplineSystem = splines;
            intersectionGenerator = intersections;
            simulationFocusTarget = focusTarget;
            vehicleCatalog = specs ?? System.Array.Empty<VehicleSpecSO>();
            activeDistrictId = string.IsNullOrWhiteSpace(initialDistrictId) ? "gulshan" : initialDistrictId;
            optionalPedestrianAnimatorController = humanoidController;

            IndexVehicleCatalog();
            EnsureSubModules();
            AttachSignalControllersToJunctions();

            _roadRouter.BuildGraph(roadSplineSystem, intersectionGenerator, _signalNodes);

            Vector3 navCenter = simulationFocusTarget != null ? simulationFocusTarget.position : Vector3.zero;
            _navBaker.ExtractPedestrianGraphAndBakeNavMesh(
                roadSplineSystem,
                intersectionGenerator,
                _signalNodes,
                navCenter,
                new Vector3(450f, 36f, 450f),
                bakeNavMesh);

            WarmVehicleAndPedestrianPools();
            _initialized = true;
        }

        private void Start()
        {
            if (!_initialized && roadSplineSystem != null)
            {
                ConfigureAndWarmPools(
                    roadSplineSystem,
                    intersectionGenerator,
                    simulationFocusTarget,
                    vehicleCatalog,
                    activeDistrictId,
                    optionalPedestrianAnimatorController,
                    bakeNavMesh: true);
            }
        }

        private void Update()
        {
            if (!_initialized || GameBootstrap.CurrentState == GameState.Paused)
            {
                return;
            }

            StepSimulation(Time.deltaTime);
        }

        public void StepSimulation(float dt)
        {
            if (dt <= 0.0001f) return;

            _frameCounter++;
            CityDangerBus.StepDecay(dt);

            // 1. Update Traffic Light phases across intersections
            for (int i = 0; i < _signalNodes.Count; i++)
            {
                _signalNodes[i]?.StepSignal(dt);
            }

            Vector3 focusPos = simulationFocusTarget != null ? simulationFocusTarget.position : Vector3.zero;
            DhakaTrafficDensityProfile.DistrictAiMix mix =
                DhakaTrafficDensityProfile.GetDistrictProfile(activeDistrictId, WorldSurfaceState.CurrentTimeOfDayHours);

            int desiredMovingVehicles = Mathf.Clamp(
                Mathf.RoundToInt(22f * mix.TrafficDensityMultiplier),
                4,
                Mathf.Max(4, maxVehiclePoolSize - parkedVehicleCount));

            int desiredPedestrians = Mathf.Clamp(
                Mathf.RoundToInt(30f * mix.PedestrianDensityMultiplier),
                6,
                maxPedestrianPoolSize);

            // 2. Step Pooled Vehicles with distance-sliced update budget
            int activeMovingCount = 0;
            for (int i = 0; i < _vehiclePool.Count; i++)
            {
                DhakaAiVehicleAgent agent = _vehiclePool[i];
                if (agent == null || !agent.gameObject.activeSelf || agent.IsAssignedToPlayer) continue;

                float dist = Vector3.Distance(agent.transform.position, focusPos);
                if (!agent.IsParked && dist > recycleDistanceMeters)
                {
                    RespawnVehicleNearFocus(agent, focusPos, mix, parked: false, i);
                    continue;
                }

                if (!agent.IsParked) activeMovingCount++;

                if (dist <= nearDetailRadiusMeters)
                {
                    agent.StepAiTraffic(dt, highDetailSensors: true);
                }
                else if (dist <= midDetailRadiusMeters)
                {
                    if ((_frameCounter + i) % 3 == 0) agent.StepAiTraffic(dt * 3f, highDetailSensors: false);
                }
                else
                {
                    if ((_frameCounter + i) % 6 == 0) agent.StepAiTraffic(dt * 6f, highDetailSensors: false);
                }
            }

            // Enable/Disable extra pooled vehicles cleanly when district or rush-hour changes
            AdjustMovingPoolQuota(desiredMovingVehicles, activeMovingCount, focusPos, mix);

            // 3. Step Pooled Pedestrians & Vendors with distance-sliced update budget
            int activePedCount = 0;
            for (int i = 0; i < _pedestrianPool.Count; i++)
            {
                DhakaPedestrianAgent npc = _pedestrianPool[i];
                if (npc == null || !npc.gameObject.activeSelf) continue;

                float dist = Vector3.Distance(npc.transform.position, focusPos);
                if (dist > recycleDistanceMeters * 0.8f && npc.BehaviorState != NpcBehaviorState.StreetVending)
                {
                    RespawnPedestrianNearFocus(npc, focusPos, i);
                    continue;
                }

                activePedCount++;

                if (dist <= nearDetailRadiusMeters)
                {
                    npc.StepNpc(dt, highDetail: true);
                }
                else if (dist <= midDetailRadiusMeters)
                {
                    if ((_frameCounter + i) % 2 == 0) npc.StepNpc(dt * 2f, highDetail: false);
                }
                else
                {
                    if ((_frameCounter + i) % 5 == 0) npc.StepNpc(dt * 5f, highDetail: false);
                }
            }

            AdjustPedestrianPoolQuota(desiredPedestrians, activePedCount, focusPos);
        }

        public DhakaAiVehicleAgent TryDispatchEmergencyPoliceCruiserNear(Vector3 targetPos)
        {
            if (!_specByClass.TryGetValue(DhakaVehicleClass.DmpPoliceCruiser, out VehicleSpecSO policeSpec))
            {
                return null;
            }

            RoadSplineSegment bestSeg = FindNearestSegment(targetPos, minDistanceFromTarget: 35f);
            if (bestSeg == null) return null;

            // Find an inactive vehicle or create temporary cruiser in pool
            DhakaAiVehicleAgent slot = null;
            for (int i = 0; i < _vehiclePool.Count; i++)
            {
                if (_vehiclePool[i] != null && !_vehiclePool[i].gameObject.activeSelf && _vehiclePool[i].IsPoliceVehicle)
                {
                    slot = _vehiclePool[i];
                    slot.gameObject.SetActive(true);
                    break;
                }
            }

            if (slot == null)
            {
                slot = CreatePooledVehicleAgent(policeSpec, _vehiclePool.Count + 500);
                if (slot != null) _vehiclePool.Add(slot);
            }

            if (slot == null) return null;

            float d = Mathf.Clamp(bestSeg.totalLengthMeters * 0.25f, 4f, bestSeg.totalLengthMeters - 4f);
            slot.InitializeAgent(policeSpec, _roadRouter, bestSeg, d, true, false, _vehiclePool.Count + 77);
            slot.EngagePoliceDispatch(targetPos);
            return slot;
        }

        private void EnsureSubModules()
        {
            _navBaker = GetComponent<DhakaNavMeshRuntimeBaker>() ?? gameObject.AddComponent<DhakaNavMeshRuntimeBaker>();
            _policeDispatch = GetComponent<DhakaPoliceDispatchController>() ?? gameObject.AddComponent<DhakaPoliceDispatchController>();
            _policeDispatch.BindSimulationManager(this);

            if (_vehiclesPoolRoot == null)
            {
                _vehiclesPoolRoot = new GameObject("Pooled_TrafficVehicles").transform;
                _vehiclesPoolRoot.SetParent(transform, false);
            }
            if (_pedestriansPoolRoot == null)
            {
                _pedestriansPoolRoot = new GameObject("Pooled_BangladeshiPedestrians").transform;
                _pedestriansPoolRoot.SetParent(transform, false);
            }
        }

        private void AttachSignalControllersToJunctions()
        {
            _signalNodes.Clear();
            if (intersectionGenerator?.Junctions == null) return;

            for (int i = 0; i < intersectionGenerator.Junctions.Count; i++)
            {
                RoadJunctionNode junc = intersectionGenerator.Junctions[i];
                if (junc == null) continue;

                GameObject sigObj = GameObject.Find($"SignalRig_{junc.junctionId}");
                Renderer[] lamps = null;
                if (sigObj != null)
                {
                    var lampList = new List<Renderer>();
                    foreach (Transform t in sigObj.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name.StartsWith("SignalLamp_") && t.TryGetComponent<Renderer>(out Renderer r))
                        {
                            lampList.Add(r);
                        }
                    }
                    lamps = lampList.ToArray();
                }

                GameObject host = sigObj != null ? sigObj : new GameObject($"SignalNode_{junc.junctionId}");
                if (sigObj == null) host.transform.SetParent(transform, false);

                var node = host.GetComponent<TrafficIntersectionNode>() ?? host.AddComponent<TrafficIntersectionNode>();
                node.ConfigureFromJunction(junc, lamps, offsetSeconds: (i * 6.5f) % 26f);
                _signalNodes.Add(node);
            }
        }

        private void IndexVehicleCatalog()
        {
            _specByClass.Clear();
            if (vehicleCatalog == null) return;
            for (int i = 0; i < vehicleCatalog.Length; i++)
            {
                VehicleSpecSO s = vehicleCatalog[i];
                if (s != null && !s.isWatercraft)
                {
                    _specByClass[s.vehicleClass] = s;
                }
            }
        }

        private void WarmVehicleAndPedestrianPools()
        {
            Vector3 focus = simulationFocusTarget != null ? simulationFocusTarget.position : Vector3.zero;
            DhakaTrafficDensityProfile.DistrictAiMix mix =
                DhakaTrafficDensityProfile.GetDistrictProfile(activeDistrictId, WorldSurfaceState.CurrentTimeOfDayHours);

            int totalVehicles = Mathf.Max(4, maxVehiclePoolSize);
            int targetInitialVehicles = Mathf.Clamp(
                Mathf.RoundToInt(22f * mix.TrafficDensityMultiplier) + parkedVehicleCount,
                4,
                totalVehicles);

            if (_roadRouter.DrivableSegments.Count > 0)
            {
                for (int i = 0; i < totalVehicles; i++)
                {
                    bool isParked = i < parkedVehicleCount;
                    DhakaVehicleClass vc = isParked
                        ? (i % 3 == 0 ? DhakaVehicleClass.CngAutoRickshaw : DhakaVehicleClass.CompactSedan)
                        : DhakaTrafficDensityProfile.SampleVehicleClass(mix, (i * 0.137f) % 1.0f);

                    VehicleSpecSO spec = ResolveSpecOrFallback(vc);
                    if (spec == null) continue;

                    DhakaAiVehicleAgent agent = CreatePooledVehicleAgent(spec, i);
                    if (agent == null) continue;

                    RoadSplineSegment seg = PickSegmentNear(focus, i, 220f);
                    float dist = seg != null ? Mathf.Clamp(((i * 23.7f) % Mathf.Max(10f, seg.totalLengthMeters - 12f)) + 6f, 4f, seg.totalLengthMeters - 4f) : 0f;
                    agent.InitializeAgent(spec, _roadRouter, seg, dist, travelForward: (i % 2 == 0) || seg.isOneWay, startParked: isParked, seed: 1000 + i);
                    agent.gameObject.SetActive(i < targetInitialVehicles);
                    _vehiclePool.Add(agent);
                }
            }

            // Warm Bangladeshi Pedestrians + Vendors + DMP Constables
            int targetPeds = Mathf.Clamp(Mathf.RoundToInt(28f * mix.PedestrianDensityMultiplier), 6, maxPedestrianPoolSize);
            var poiList = _navBaker.ActivityPois;

            for (int i = 0; i < maxPedestrianPoolSize; i++)
            {
                bool makeVendor = poiList != null && i < Mathf.Min(6, poiList.Count);
                PedestrianWardrobeArchetype arch;
                if (makeVendor)
                {
                    arch = PedestrianWardrobeArchetype.StreetVendorChaiwala;
                }
                else if (i % 14 == 13)
                {
                    arch = PedestrianWardrobeArchetype.DmpPoliceOfficer;
                }
                else
                {
                    arch = (PedestrianWardrobeArchetype)(i % 5);
                }

                Vector3 spawnPos = focus + new Vector3(Mathf.Cos(i) * 18f, 0.1f, Mathf.Sin(i) * 18f);
                if (makeVendor && poiList != null && i < poiList.Count)
                {
                    spawnPos = poiList[i].WorldPosition;
                }
                else if (_navBaker.SidewalkWaypoints.Count > 0)
                {
                    spawnPos = PickSidewalkNear(focus, i, 180f);
                }

                DhakaPedestrianAgent ped = CreatePooledPedestrian(arch, 2000 + i);
                ped.InitializeNpc(arch, _navBaker, spawnPos, 3000 + i, stationaryVendor: makeVendor);
                ped.gameObject.SetActive(i < targetPeds);
                _pedestrianPool.Add(ped);
            }
        }

        private DhakaAiVehicleAgent CreatePooledVehicleAgent(VehicleSpecSO spec, int index)
        {
            GameObject go = DhakaVehicleFactory.CreateVehicleFromSpec(spec, Vector3.zero, Quaternion.identity, _vehiclesPoolRoot);
            if (go == null) return null;

            go.name = $"AI_Pool_{index:00}_{spec.vehicleClass}";
            if (spec.typicalLiveries != null && spec.typicalLiveries.Length > 0)
            {
                Color c = spec.typicalLiveries[index % spec.typicalLiveries.Length];
                foreach (MeshRenderer mr in go.GetComponentsInChildren<MeshRenderer>())
                {
                    if (mr.gameObject.name.Contains("Chassis") || mr.gameObject.name.Contains("Body") || mr.gameObject.name.Contains("Canopy"))
                    {
                        if (mr.material.HasProperty("_BaseColor")) mr.material.SetColor("_BaseColor", c);
                    }
                }
            }

            return go.AddComponent<DhakaAiVehicleAgent>();
        }

        private DhakaPedestrianAgent CreatePooledPedestrian(PedestrianWardrobeArchetype arch, int seed)
        {
            var go = new GameObject($"NPC_Pool_{seed}_{arch}");
            go.layer = PhysicsLayers.Npc;
            go.transform.SetParent(_pedestriansPoolRoot, false);

            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.75f;
            cc.radius = 0.28f;
            cc.center = new Vector3(0f, 0.875f, 0f);

            var navAgent = go.AddComponent<UnityEngine.AI.NavMeshAgent>();
            navAgent.radius = 0.30f;
            navAgent.height = 1.75f;
            navAgent.speed = 1.45f;
            navAgent.acceleration = 12f;
            navAgent.enabled = false;

            BangladeshiPedestrianVisualBuilder.BuildArticulatedBangladeshiNpcVisual(
                go.transform, arch, seed, optionalPedestrianAnimatorController);

            return go.AddComponent<DhakaPedestrianAgent>();
        }

        private void AdjustMovingPoolQuota(int target, int current, Vector3 focus, DhakaTrafficDensityProfile.DistrictAiMix mix)
        {
            if (current < target)
            {
                for (int i = 0; i < _vehiclePool.Count && current < target; i++)
                {
                    if (_vehiclePool[i] != null && !_vehiclePool[i].gameObject.activeSelf && !_vehiclePool[i].IsParked)
                    {
                        _vehiclePool[i].gameObject.SetActive(true);
                        RespawnVehicleNearFocus(_vehiclePool[i], focus, mix, parked: false, i);
                        current++;
                    }
                }
            }
            else if (current > target)
            {
                for (int i = _vehiclePool.Count - 1; i >= 0 && current > target; i--)
                {
                    DhakaAiVehicleAgent v = _vehiclePool[i];
                    if (v != null && v.gameObject.activeSelf && !v.IsParked && !v.IsPoliceResponding &&
                        Vector3.Distance(v.transform.position, focus) > 65f)
                    {
                        v.gameObject.SetActive(false);
                        current--;
                    }
                }
            }
        }

        private void AdjustPedestrianPoolQuota(int target, int current, Vector3 focus)
        {
            if (current < target)
            {
                for (int i = 0; i < _pedestrianPool.Count && current < target; i++)
                {
                    if (_pedestrianPool[i] != null && !_pedestrianPool[i].gameObject.activeSelf)
                    {
                        _pedestrianPool[i].gameObject.SetActive(true);
                        RespawnPedestrianNearFocus(_pedestrianPool[i], focus, i);
                        current++;
                    }
                }
            }
            else if (current > target)
            {
                for (int i = _pedestrianPool.Count - 1; i >= 0 && current > target; i--)
                {
                    DhakaPedestrianAgent p = _pedestrianPool[i];
                    if (p != null && p.gameObject.activeSelf && p.BehaviorState == NpcBehaviorState.WalkingSidewalk &&
                        Vector3.Distance(p.transform.position, focus) > 55f)
                    {
                        p.gameObject.SetActive(false);
                        current--;
                    }
                }
            }
        }

        private void RespawnVehicleNearFocus(DhakaAiVehicleAgent agent, Vector3 focus, DhakaTrafficDensityProfile.DistrictAiMix _, bool parked, int seed)
        {
            RoadSplineSegment seg = PickSegmentNear(focus, _frameCounter + seed, 175f, minRange: 45f);
            if (seg == null) return;
            float dist = Mathf.Clamp(seg.totalLengthMeters * 0.35f, 4f, seg.totalLengthMeters - 4f);
            agent.InitializeAgent(agent.Spec, _roadRouter, seg, dist, travelForward: (seed % 2 == 0) || seg.isOneWay, startParked: parked, seed: 5000 + _frameCounter + seed);
        }

        private void RespawnPedestrianNearFocus(DhakaPedestrianAgent npc, Vector3 focus, int seed)
        {
            Vector3 pos = _navBaker.SidewalkWaypoints.Count > 0
                ? PickSidewalkNear(focus, _frameCounter + seed, 130f)
                : focus + new Vector3(Mathf.Cos(seed) * 35f, 0.1f, Mathf.Sin(seed) * 35f);
            npc.InitializeNpc(npc.Archetype, _navBaker, pos, 7000 + seed);
        }

        private RoadSplineSegment PickSegmentNear(Vector3 focus, int indexSeed, float maxRange, float minRange = 0f)
        {
            var segs = _roadRouter.DrivableSegments;
            if (segs.Count == 0) return null;

            for (int attempt = 0; attempt < 24; attempt++)
            {
                RoadSplineSegment cand = segs[(Mathf.Abs(indexSeed) + attempt * 7) % segs.Count];
                float d = Vector3.Distance(cand.StartPosition, focus);
                if (d >= minRange && d <= maxRange)
                {
                    return cand;
                }
            }
            return FindNearestSegment(focus, 0f);
        }

        private RoadSplineSegment FindNearestSegment(Vector3 target, float minDistanceFromTarget)
        {
            var segs = _roadRouter.DrivableSegments;
            RoadSplineSegment best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < segs.Count; i++)
            {
                float d = Vector3.Distance(segs[i].StartPosition, target);
                if (d >= minDistanceFromTarget && d < bestDist)
                {
                    bestDist = d;
                    best = segs[i];
                }
            }
            return best ?? (segs.Count > 0 ? segs[0] : null);
        }

        private Vector3 PickSidewalkNear(Vector3 focus, int seed, float maxRange)
        {
            var wps = _navBaker.SidewalkWaypoints;
            for (int attempt = 0; attempt < 16; attempt++)
            {
                Vector3 wp = wps[(Mathf.Abs(seed) + attempt * 11) % wps.Count];
                if (Vector3.Distance(wp, focus) <= maxRange)
                {
                    return wp;
                }
            }
            return wps[Mathf.Abs(seed) % wps.Count];
        }

        private VehicleSpecSO ResolveSpecOrFallback(DhakaVehicleClass desired)
        {
            if (_specByClass.TryGetValue(desired, out VehicleSpecSO spec)) return spec;
            foreach (VehicleSpecSO fallback in _specByClass.Values)
            {
                return fallback;
            }
            return null;
        }
    }
}
