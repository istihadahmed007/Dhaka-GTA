using System;
using System.Collections.Generic;
using DhakaGta.Core;
using DhakaGta.Environment;
using DhakaGta.WorldGen;
using UnityEngine;
using UnityEngine.AI;

namespace DhakaGta.TrafficAI
{
    public enum CityPoiKind
    {
        TeaStall = 0,
        ShopStall = 1,
        StreetVendorSpot = 2,
        BenchRest = 3,
        BusShelter = 4
    }

    public struct PedestrianCrosswalkLink
    {
        public Vector3 EndA;
        public Vector3 EndB;
        public TrafficIntersectionNode ParentSignal;
    }

    public struct PedestrianActivityPoi
    {
        public string Name;
        public CityPoiKind Kind;
        public Vector3 WorldPosition;
        public Vector3 LookDirection;
    }

    /// <summary>
    /// Extracts walkable sidewalks, zebra crosswalk links, and interactive Dhaka POIs from generated roads
    /// and intersections, and bakes a runtime UnityEngine.AI NavMeshData volume without requiring external UPM packages.
    /// </summary>
    public sealed class DhakaNavMeshRuntimeBaker : MonoBehaviour
    {
        private readonly List<Vector3> _sidewalkWaypoints = new List<Vector3>();
        private readonly List<PedestrianCrosswalkLink> _crosswalks = new List<PedestrianCrosswalkLink>();
        private readonly List<PedestrianActivityPoi> _activityPois = new List<PedestrianActivityPoi>();

        private NavMeshData _runtimeNavMeshData;
        private NavMeshDataInstance _navMeshInstance;

        public IReadOnlyList<Vector3> SidewalkWaypoints => _sidewalkWaypoints;
        public IReadOnlyList<PedestrianCrosswalkLink> Crosswalks => _crosswalks;
        public IReadOnlyList<PedestrianActivityPoi> ActivityPois => _activityPois;
        public bool HasBakedNavMesh => _navMeshInstance.valid;

        public void ExtractPedestrianGraphAndBakeNavMesh(
            RoadSplineSystem splineSystem,
            IntersectionGenerator intersectionGen,
            IReadOnlyList<TrafficIntersectionNode> signalNodes,
            Vector3 navCenter,
            Vector3 navExtents,
            bool bakeNavMesh = true)
        {
            _sidewalkWaypoints.Clear();
            _crosswalks.Clear();
            _activityPois.Clear();

            ExtractSidewalksAndCrosswalks(splineSystem, intersectionGen, signalNodes);
            DiscoverWorldActivityPois();

            if (bakeNavMesh)
            {
                BakeDynamicNavMeshVolume(navCenter, navExtents);
            }
        }

        private void ExtractSidewalksAndCrosswalks(
            RoadSplineSystem splineSystem,
            IntersectionGenerator intersectionGen,
            IReadOnlyList<TrafficIntersectionNode> signalNodes)
        {
            if (splineSystem?.Segments != null)
            {
                for (int i = 0; i < splineSystem.Segments.Count; i++)
                {
                    RoadSplineSegment seg = splineSystem.Segments[i];
                    if (seg == null || seg.totalLengthMeters < 14f || seg.gradeKind != RoadGradeKind.Surface)
                    {
                        continue;
                    }

                    float curbOffset = seg.widthMeters * 0.5f + 1.55f;
                    float step = Mathf.Clamp(seg.totalLengthMeters / 5f, 12f, 34f);

                    for (float d = 4f; d < seg.totalLengthMeters - 4f; d += step)
                    {
                        seg.SampleAtDistance(d, out Vector3 p, out Vector3 tan, out Vector3 right);
                        _sidewalkWaypoints.Add(p + right * curbOffset + Vector3.up * 0.14f);
                        _sidewalkWaypoints.Add(p - right * curbOffset + Vector3.up * 0.14f);

                        // Seed local vendor spots periodically along wide market roads
                        if (_activityPois.Count < 48 && ((int)(d + i * 7) % 53) < 6)
                        {
                            float sideSign = (i % 2 == 0) ? 1f : -1f;
                            Vector3 stallPos = p + right * (curbOffset + 0.9f) * sideSign + Vector3.up * 0.12f;
                            _activityPois.Add(new PedestrianActivityPoi
                            {
                                Name = $"{seg.roadName}_VendorSpot",
                                Kind = CityPoiKind.StreetVendorSpot,
                                WorldPosition = stallPos,
                                LookDirection = -right * sideSign
                            });
                        }
                    }
                }
            }

            if (intersectionGen?.Junctions != null)
            {
                for (int j = 0; j < intersectionGen.Junctions.Count; j++)
                {
                    RoadJunctionNode junc = intersectionGen.Junctions[j];
                    if (junc?.arms == null) continue;

                    TrafficIntersectionNode matchedSignal = null;
                    if (signalNodes != null)
                    {
                        for (int s = 0; s < signalNodes.Count; s++)
                        {
                            if (signalNodes[s] != null && signalNodes[s].IntersectionId == junc.junctionId)
                            {
                                matchedSignal = signalNodes[s];
                                break;
                            }
                        }
                    }

                    for (int a = 0; a < junc.arms.Count; a++)
                    {
                        JunctionArmInfo arm = junc.arms[a];
                        if (arm == null) continue;
                        Vector3 dir = arm.directionOutward;
                        dir.y = 0f;
                        if (dir.sqrMagnitude < 1e-4f) continue;
                        dir.Normalize();

                        Vector3 right = new Vector3(dir.z, 0f, -dir.x);
                        Vector3 walkMid = junc.centerPosition + dir * (junc.radiusMeters + 1.4f);
                        float halfSpan = Mathf.Max(3.2f, arm.halfWidthMeters + 1.2f);

                        _crosswalks.Add(new PedestrianCrosswalkLink
                        {
                            EndA = walkMid - right * halfSpan + Vector3.up * 0.08f,
                            EndB = walkMid + right * halfSpan + Vector3.up * 0.08f,
                            ParentSignal = matchedSignal
                        });
                    }
                }
            }
        }

        private void DiscoverWorldActivityPois()
        {
            InteractiveEnvironmentProp[] props = FindObjectsByType<InteractiveEnvironmentProp>(FindObjectsSortMode.None);
            for (int i = 0; i < props.Length; i++)
            {
                InteractiveEnvironmentProp prop = props[i];
                if (prop == null) continue;

                CityPoiKind poiKind = prop.Kind switch
                {
                    InteractableKind.TeaStall => CityPoiKind.TeaStall,
                    InteractableKind.BenchRest => CityPoiKind.BenchRest,
                    InteractableKind.MetroGate => CityPoiKind.BusShelter,
                    _ => CityPoiKind.ShopStall
                };

                _activityPois.Add(new PedestrianActivityPoi
                {
                    Name = prop.name,
                    Kind = poiKind,
                    WorldPosition = prop.WorldPosition + prop.transform.forward * 1.4f,
                    LookDirection = -prop.transform.forward
                });
            }
        }

        public void BakeDynamicNavMeshVolume(Vector3 center, Vector3 extents)
        {
            if (_navMeshInstance.valid)
            {
                _navMeshInstance.Remove();
            }

            var sources = new List<NavMeshBuildSource>(256);
            int walkableMask = PhysicsLayers.GroundMask | (1 << PhysicsLayers.Building);

            MeshFilter[] filters = FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
            Bounds bakeBounds = new Bounds(center, extents);

            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter mf = filters[i];
                if (mf == null || mf.sharedMesh == null) continue;
                int layer = mf.gameObject.layer;
                if (((1 << layer) & walkableMask) == 0) continue;

                Renderer r = mf.GetComponent<Renderer>();
                if (r != null && !bakeBounds.Intersects(r.bounds)) continue;

                sources.Add(new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Mesh,
                    sourceObject = mf.sharedMesh,
                    transform = mf.transform.localToWorldMatrix,
                    area = layer == PhysicsLayers.Ground ? 0 : 1 // 0 = Walkable, 1 = Not Walkable obstacle
                });
            }

            if (sources.Count == 0)
            {
                return;
            }

            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.34f;
            settings.agentHeight = 1.75f;
            settings.agentSlope = 38f;
            settings.agentClimb = 0.42f;
            settings.voxelSize = 0.35f;

            _runtimeNavMeshData = NavMeshBuilder.BuildNavMeshData(
                settings,
                sources,
                bakeBounds,
                center,
                Quaternion.identity);

            if (_runtimeNavMeshData != null)
            {
                _navMeshInstance = NavMesh.AddNavMeshData(_runtimeNavMeshData);
            }
        }

        private void OnDestroy()
        {
            if (_navMeshInstance.valid)
            {
                _navMeshInstance.Remove();
            }
        }
    }
}
