using System;
using System.Collections.Generic;
using DhakaGta.Core;
using DhakaGta.GIS;
using UnityEngine;

namespace DhakaGta.WorldGen
{
    public enum JunctionKind
    {
        ThreeWay = 0,
        FourWay = 1,
        MultiLeg = 2,
        Roundabout = 3,
    }

    [Serializable]
    public sealed class JunctionArmInfo
    {
        public RoadSplineSegment segment;
        public bool entersAtEndNode;
        public Vector3 directionOutward;
        public float halfWidthMeters;
    }

    [Serializable]
    public sealed class RoadJunctionNode
    {
        public string junctionId;
        public JunctionKind kind;
        public long osmNodeId;
        public Vector3 centerPosition;
        public float radiusMeters;
        public bool hasTrafficSignal;
        public List<JunctionArmInfo> arms = new List<JunctionArmInfo>();
    }

    [Serializable]
    public sealed class NavGraphEdge
    {
        public string fromJunctionId;
        public string toJunctionId;
        public string roadName;
        public float lengthMeters;
        public float speedLimitKph;
        public bool isOneWay;
        public int lanes;
    }

    /// <summary>
    /// Detects non-grid intersections, roundabouts (Gulshan Circle 1/2), and traffic signal nodes directly
    /// from shared OpenStreetMap node topology and proximity, generating paved junction aprons, crosswalks,
    /// stop lines, roundabout islands, and vehicle navigation graph edges.
    /// </summary>
    public sealed class IntersectionGenerator : MonoBehaviour
    {
        [SerializeField] private float proximityMergeMeters = 1.6f;

        [SerializeField] private List<RoadJunctionNode> _junctions = new List<RoadJunctionNode>();
        [SerializeField] private List<NavGraphEdge> _navEdges = new List<NavGraphEdge>();

        public IReadOnlyList<RoadJunctionNode> Junctions => _junctions;
        public IReadOnlyList<NavGraphEdge> NavEdges => _navEdges;

        public sealed class IntersectionMaterials
        {
            public Material AsphaltApronMaterial;
            public Material MarkingWhiteMaterial;
            public Material CurbConcreteMaterial;
            public Material IslandTurfMaterial;
            public Material SignalSteelMaterial;
            public Material SignalGlowGreenMaterial;
            public Material SignalGlowRedMaterial;
        }

        private sealed class ConnectedArm
        {
            public RoadSplineSegment Segment;
            public int SampleIndex;
            public bool EntersAtEnd;
            public Vector3 Position;
            public Vector3 OutwardDir;
        }

        public void Generate(
            RoadSplineSystem splineSystem,
            GeoFeatureDatabase db,
            WorldCoordinateManager coordManager,
            Transform parentRoot,
            IntersectionMaterials materials = null)
        {
            _junctions.Clear();
            _navEdges.Clear();
            if (splineSystem == null || parentRoot == null)
            {
                return;
            }

            materials ??= new IntersectionMaterials();
            IReadOnlyList<RoadSplineSegment> segments = splineSystem.Segments;
            if (segments == null || segments.Count == 0)
            {
                return;
            }

            // Step 1: Index all OSM node attachments (internal AND endpoint vertices) across surface/grade roads
            var nodeBuckets = new Dictionary<long, List<ConnectedArm>>();
            var roundaboutRings = new List<RoadSplineSegment>();

            foreach (RoadSplineSegment seg in segments)
            {
                if (seg == null || seg.sampledPointsLocal.Length < 2) continue;
                if (seg.isRoundabout)
                {
                    roundaboutRings.Add(seg);
                }

                if (seg.controlNodeIds != null && seg.controlNodeIds.Length > 0 && seg.controlPointsUtm != null)
                {
                    int nodeCount = Mathf.Min(seg.controlNodeIds.Length, seg.controlPointsUtm.Length);
                    for (int i = 0; i < nodeCount; i++)
                    {
                        long nid = seg.controlNodeIds[i];
                        if (nid <= 0) continue;

                        if (!nodeBuckets.TryGetValue(nid, out List<ConnectedArm> list))
                        {
                            list = new List<ConnectedArm>();
                            nodeBuckets[nid] = list;
                        }

                        bool atEnd = i == nodeCount - 1;
                        bool atStart = i == 0;
                        int sampleIdx = MapControlIndexToSampleIndex(seg, i, nodeCount);
                        Vector3 pos = seg.sampledPointsLocal[sampleIdx];
                        Vector3 tan = seg.sampledTangents[sampleIdx];

                        if (atStart)
                        {
                            list.Add(new ConnectedArm { Segment = seg, SampleIndex = sampleIdx, EntersAtEnd = false, Position = pos, OutwardDir = tan });
                        }
                        else if (atEnd)
                        {
                            list.Add(new ConnectedArm { Segment = seg, SampleIndex = sampleIdx, EntersAtEnd = true, Position = pos, OutwardDir = -tan });
                        }
                        else
                        {
                            // Mid-polyline split acts as two opposing arms at this OSM node
                            list.Add(new ConnectedArm { Segment = seg, SampleIndex = sampleIdx, EntersAtEnd = true, Position = pos, OutwardDir = -tan });
                            list.Add(new ConnectedArm { Segment = seg, SampleIndex = sampleIdx, EntersAtEnd = false, Position = pos, OutwardDir = tan });
                        }
                    }
                }
            }

            // Convert OSM traffic signal coordinates to local Unity coordinates once
            var signalPositions = new List<Vector3>();
            if (db?.trafficSignals != null && coordManager != null)
            {
                foreach (GeoTrafficSignalFeature sig in db.trafficSignals)
                {
                    signalPositions.Add(coordManager.UtmToUnityPosition(sig.coordinateUtm, 0.02f));
                }
            }

            Transform apronRoot = CreateCleanGroup(parentRoot, "Intersections_Aprons");
            Transform markingsRoot = CreateCleanGroup(parentRoot, "Intersections_Markings");
            Transform signalsRoot = CreateCleanGroup(parentRoot, "Intersections_Signals");
            Transform roundaboutRoot = CreateCleanGroup(parentRoot, "Intersections_Roundabouts");

            var markingVertices = new List<Vector3>();
            var markingTriangles = new List<int>();
            var visitedCenters = new List<Vector3>();

            // Step 2: Generate non-grid Junctions where >= 3 directional arms meet
            int junctionSeq = 0;
            foreach (var kvp in nodeBuckets)
            {
                List<ConnectedArm> arms = kvp.Value;
                if (arms.Count < 3) continue;

                // Ensure all arms belong to roughly the same vertical grade (prevent flyover-over-street false intersections)
                Vector3 center = arms[0].Position;
                var sameGradeArms = new List<ConnectedArm>();
                foreach (ConnectedArm arm in arms)
                {
                    if (Mathf.Abs(arm.Position.y - center.y) <= 2.2f)
                    {
                        sameGradeArms.Add(arm);
                    }
                }
                if (sameGradeArms.Count < 3) continue;
                if (IsNearExisting(visitedCenters, center, 4.5f)) continue;
                visitedCenters.Add(center);

                bool hasSignal = IsNearAnySignal(signalPositions, center, 22f) ||
                                 (sameGradeArms.Count >= 4 && HasPrimaryOrSecondaryArm(sameGradeArms));

                float maxHalfW = 5f;
                foreach (ConnectedArm arm in sameGradeArms)
                {
                    maxHalfW = Mathf.Max(maxHalfW, arm.Segment.widthMeters * 0.52f);
                }

                var node = new RoadJunctionNode
                {
                    junctionId = $"Junc_{++junctionSeq}_{kvp.Key}",
                    osmNodeId = kvp.Key,
                    kind = sameGradeArms.Count == 3 ? JunctionKind.ThreeWay :
                           (sameGradeArms.Count == 4 ? JunctionKind.FourWay : JunctionKind.MultiLeg),
                    centerPosition = center,
                    radiusMeters = Mathf.Clamp(maxHalfW * 1.25f, 6f, 18f),
                    hasTrafficSignal = hasSignal,
                };

                foreach (ConnectedArm a in sameGradeArms)
                {
                    node.arms.Add(new JunctionArmInfo
                    {
                        segment = a.Segment,
                        entersAtEndNode = a.EntersAtEnd,
                        directionOutward = a.OutwardDir,
                        halfWidthMeters = a.Segment.widthMeters * 0.5f,
                    });
                }

                _junctions.Add(node);
                BuildJunctionApronMesh(node, apronRoot, materials.AsphaltApronMaterial);
                AppendStopLinesAndCrosswalks(node, markingVertices, markingTriangles);

                if (hasSignal)
                {
                    SpawnTrafficSignalRig(node, signalsRoot, materials);
                }
            }

            // Step 3: Build Roundabout rings (Gulshan Circle 1, Gulshan Circle 2, etc.)
            BuildRoundabouts(roundaboutRings, roundaboutRoot, materials);

            // Step 4: Bake batched white crosswalk + stop-line markings mesh
            if (markingVertices.Count >= 3 && markingTriangles.Count >= 3)
            {
                var markMesh = new Mesh
                {
                    name = "IntersectionMarkingsBatch",
                    indexFormat = markingVertices.Count > 60000
                        ? UnityEngine.Rendering.IndexFormat.UInt32
                        : UnityEngine.Rendering.IndexFormat.UInt16,
                };
                markMesh.SetVertices(markingVertices);
                markMesh.SetTriangles(markingTriangles, 0);
                markMesh.RecalculateNormals();
                markMesh.RecalculateBounds();

                var go = new GameObject("IntersectionCrosswalksAndStopLines");
                go.layer = PhysicsLayers.Ground;
                go.transform.SetParent(markingsRoot, false);
                go.AddComponent<MeshFilter>().sharedMesh = markMesh;
                var mr = go.AddComponent<MeshRenderer>();
                if (materials.MarkingWhiteMaterial != null) mr.sharedMaterial = materials.MarkingWhiteMaterial;
            }

            // Step 5: Build Vehicle Navigation Graph edges preserving real street topology
            BuildNavigationEdges(segments);
        }

        private void BuildRoundabouts(
            List<RoadSplineSegment> roundaboutRings,
            Transform roundaboutRoot,
            IntersectionMaterials materials)
        {
            if (roundaboutRings.Count == 0) return;

            var grouped = new List<List<RoadSplineSegment>>();
            foreach (RoadSplineSegment ringSeg in roundaboutRings)
            {
                Vector3 mid = ringSeg.sampledPointsLocal[ringSeg.sampledPointsLocal.Length / 2];
                List<RoadSplineSegment> targetGroup = null;
                foreach (List<RoadSplineSegment> grp in grouped)
                {
                    Vector3 grpCenter = ComputeGroupCenter(grp);
                    if (Vector3.Distance(grpCenter, mid) <= 95f)
                    {
                        targetGroup = grp;
                        break;
                    }
                }
                if (targetGroup == null)
                {
                    targetGroup = new List<RoadSplineSegment>();
                    grouped.Add(targetGroup);
                }
                targetGroup.Add(ringSeg);
            }

            int rbIndex = 0;
            foreach (List<RoadSplineSegment> group in grouped)
            {
                Vector3 center = ComputeGroupCenter(group);
                float avgRadius = 0f;
                int count = 0;
                foreach (RoadSplineSegment seg in group)
                {
                    foreach (Vector3 pt in seg.sampledPointsLocal)
                    {
                        avgRadius += Vector3.Distance(new Vector3(pt.x, 0f, pt.z), new Vector3(center.x, 0f, center.z));
                        count++;
                    }
                }
                avgRadius = count > 0 ? avgRadius / count : 18f;
                float innerRadius = Mathf.Clamp(avgRadius - 5.5f, 4f, 45f);

                _junctions.Add(new RoadJunctionNode
                {
                    junctionId = $"Roundabout_{++rbIndex}",
                    kind = JunctionKind.Roundabout,
                    centerPosition = center,
                    radiusMeters = avgRadius,
                    hasTrafficSignal = false,
                });

                // Curbed Landscaped Center Island inside roundabout loop
                GameObject island = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                island.name = $"RoundaboutIsland_{rbIndex}";
                island.layer = PhysicsLayers.Building;
                island.transform.SetParent(roundaboutRoot, false);
                island.transform.position = new Vector3(center.x, center.y + 0.14f, center.z);
                island.transform.localScale = new Vector3(innerRadius * 2f, 0.22f, innerRadius * 2f);
                if (materials.IslandTurfMaterial != null)
                {
                    island.GetComponent<MeshRenderer>().sharedMaterial = materials.IslandTurfMaterial;
                }
            }
        }

        private void BuildNavigationEdges(IReadOnlyList<RoadSplineSegment> segments)
        {
            foreach (RoadSplineSegment seg in segments)
            {
                if (seg == null) continue;
                RoadJunctionNode fromNode = FindNearestJunction(seg.StartPosition, 22f);
                RoadJunctionNode toNode = FindNearestJunction(seg.EndPosition, 22f);

                _navEdges.Add(new NavGraphEdge
                {
                    fromJunctionId = fromNode != null ? fromNode.junctionId : $"Start_{seg.osmId}",
                    toJunctionId = toNode != null ? toNode.junctionId : $"End_{seg.osmId}",
                    roadName = seg.roadName,
                    lengthMeters = seg.totalLengthMeters,
                    speedLimitKph = seg.maxSpeedKph,
                    isOneWay = seg.isOneWay,
                    lanes = seg.lanes,
                });
            }
        }

        private RoadJunctionNode FindNearestJunction(Vector3 pos, float maxRadius)
        {
            RoadJunctionNode best = null;
            float bestDist = maxRadius;
            foreach (RoadJunctionNode j in _junctions)
            {
                float d = Vector3.Distance(pos, j.centerPosition);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = j;
                }
            }
            return best;
        }

        private static void BuildJunctionApronMesh(RoadJunctionNode node, Transform parent, Material apronMat)
        {
            const int sectors = 16;
            var verts = new List<Vector3>(sectors + 1) { new Vector3(0f, 0.035f, 0f) };
            var tris = new List<int>(sectors * 3);

            float r = node.radiusMeters * 0.92f;
            for (int i = 0; i < sectors; i++)
            {
                float angle = (float)i / sectors * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(angle) * r, 0.035f, Mathf.Sin(angle) * r));
            }

            for (int i = 0; i < sectors; i++)
            {
                int next = (i + 1) % sectors;
                // Unity CW winding for upward normal (+Y): center(0) -> next+1 -> i+1
                tris.Add(0);
                tris.Add(next + 1);
                tris.Add(i + 1);
            }

            var mesh = new Mesh { name = $"ApronMesh_{node.junctionId}" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject(node.junctionId);
            go.layer = PhysicsLayers.Ground;
            go.transform.SetParent(parent, false);
            go.transform.position = node.centerPosition;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            if (apronMat != null) mr.sharedMaterial = apronMat;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        private static void AppendStopLinesAndCrosswalks(
            RoadJunctionNode node,
            List<Vector3> verts,
            List<int> tris)
        {
            float y = node.centerPosition.y + 0.055f;
            foreach (JunctionArmInfo arm in node.arms)
            {
                Vector3 dir = arm.directionOutward;
                dir.y = 0f;
                if (dir.sqrMagnitude < 1e-4f) continue;
                dir.Normalize();

                Vector3 right = new Vector3(dir.z, 0f, -dir.x);
                float width = Mathf.Clamp(arm.halfWidthMeters * 1.75f, 3.5f, 16f);

                // Stop Line bar
                Vector3 stopCenter = node.centerPosition + dir * (node.radiusMeters + 0.7f);
                stopCenter.y = y;
                AppendWhiteQuad(verts, tris, stopCenter, dir, right, 0.45f, width);

                // Pedestrian Zebra Crosswalk stripes behind stop line
                Vector3 walkCenter = node.centerPosition + dir * (node.radiusMeters + 2.6f);
                walkCenter.y = y;
                int stripes = Mathf.Max(3, Mathf.FloorToInt(width / 1.4f));
                float startLat = -width * 0.45f;
                float step = (width * 0.9f) / Mathf.Max(1, stripes - 1);
                for (int s = 0; s < stripes; s++)
                {
                    Vector3 stripePos = walkCenter + right * (startLat + s * step);
                    AppendWhiteQuad(verts, tris, stripePos, dir, right, 2.2f, 0.55f);
                }
            }
        }

        private static void SpawnTrafficSignalRig(RoadJunctionNode node, Transform parent, IntersectionMaterials materials)
        {
            var rigRoot = new GameObject($"SignalRig_{node.junctionId}");
            rigRoot.transform.SetParent(parent, false);
            rigRoot.transform.position = node.centerPosition;

            int cornerCount = Mathf.Min(4, node.arms.Count);
            for (int i = 0; i < cornerCount; i++)
            {
                JunctionArmInfo arm = node.arms[i];
                Vector3 dir = arm.directionOutward;
                dir.y = 0f;
                if (dir.sqrMagnitude < 1e-4f) continue;
                dir.Normalize();
                Vector3 right = new Vector3(dir.z, 0f, -dir.x);

                Vector3 cornerOffset = dir * (node.radiusMeters + 1.5f) - right * (arm.halfWidthMeters + 1.1f);
                GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = $"SignalPole_{i + 1}";
                pole.layer = PhysicsLayers.Building;
                pole.transform.SetParent(rigRoot.transform, false);
                pole.transform.localPosition = cornerOffset + Vector3.up * 2.4f;
                pole.transform.localScale = new Vector3(0.22f, 2.4f, 0.22f);
                if (materials.SignalSteelMaterial != null)
                {
                    pole.GetComponent<MeshRenderer>().sharedMaterial = materials.SignalSteelMaterial;
                }

                GameObject lampHead = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lampHead.name = $"SignalLamp_{i + 1}";
                DestroyObjectSafe(lampHead.GetComponent<Collider>());
                lampHead.transform.SetParent(pole.transform, false);
                lampHead.transform.localPosition = new Vector3(0f, 0.85f, 0f);
                lampHead.transform.localScale = new Vector3(2.2f, 0.45f, 2.2f);
                Material glow = (i % 2 == 0) ? materials.SignalGlowGreenMaterial : materials.SignalGlowRedMaterial;
                if (glow != null)
                {
                    lampHead.GetComponent<MeshRenderer>().sharedMaterial = glow;
                }
            }
        }

        private static void AppendWhiteQuad(
            List<Vector3> verts, List<int> tris, Vector3 center, Vector3 forward, Vector3 right, float length, float width)
        {
            int b = verts.Count;
            Vector3 hf = forward * (length * 0.5f);
            Vector3 hr = right * (width * 0.5f);

            verts.Add(center - hr - hf);
            verts.Add(center + hr - hf);
            verts.Add(center - hr + hf);
            verts.Add(center + hr + hf);

            tris.Add(b);
            tris.Add(b + 2);
            tris.Add(b + 1);

            tris.Add(b + 1);
            tris.Add(b + 2);
            tris.Add(b + 3);
        }

        private static int MapControlIndexToSampleIndex(RoadSplineSegment seg, int ctrlIndex, int ctrlCount)
        {
            if (ctrlCount <= 1 || seg.sampledPointsLocal.Length <= 1) return 0;
            float ratio = (float)ctrlIndex / (ctrlCount - 1);
            return Mathf.Clamp(Mathf.RoundToInt(ratio * (seg.sampledPointsLocal.Length - 1)), 0, seg.sampledPointsLocal.Length - 1);
        }

        private static bool IsNearExisting(List<Vector3> existing, Vector3 pt, float threshold)
        {
            float sq = threshold * threshold;
            foreach (Vector3 e in existing)
            {
                if ((e - pt).sqrMagnitude <= sq) return true;
            }
            return false;
        }

        private static bool IsNearAnySignal(List<Vector3> signals, Vector3 pt, float radius)
        {
            float sq = radius * radius;
            foreach (Vector3 s in signals)
            {
                if ((s - pt).sqrMagnitude <= sq) return true;
            }
            return false;
        }

        private static bool HasPrimaryOrSecondaryArm(List<ConnectedArm> arms)
        {
            foreach (ConnectedArm a in arms)
            {
                string hc = a.Segment.highwayClass;
                if (hc == "trunk" || hc == "primary" || hc == "secondary") return true;
            }
            return false;
        }

        private static Vector3 ComputeGroupCenter(List<RoadSplineSegment> group)
        {
            Vector3 sum = Vector3.zero;
            int total = 0;
            foreach (RoadSplineSegment s in group)
            {
                foreach (Vector3 p in s.sampledPointsLocal)
                {
                    sum += p;
                    total++;
                }
            }
            return total > 0 ? sum / total : Vector3.zero;
        }

        private static Transform CreateCleanGroup(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
            {
                for (int i = child.childCount - 1; i >= 0; i--)
                {
                    DestroyObjectSafe(child.GetChild(i).gameObject);
                }
                return child;
            }
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void DestroyObjectSafe(UnityEngine.Object obj)
        {
            if (obj == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(obj);
                return;
            }
#endif
            Destroy(obj);
        }
    }
}
