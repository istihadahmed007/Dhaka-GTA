using System;
using System.Collections.Generic;
using DhakaGta.WorldGen;
using UnityEngine;

namespace DhakaGta.TrafficAI
{
    public struct RoadTransitionChoice
    {
        public RoadSplineSegment NextSegment;
        public float StartDistanceMeters;
        public bool TravelForward;
    }

    /// <summary>
    /// Builds a real-world connected road spline graph directly from RoadSplineSystem & IntersectionGenerator,
    /// handling Bangladesh Left-Hand Traffic (LHT) lane alignment, junction turning, and dead-end reversals.
    /// </summary>
    public sealed class DhakaRoadNetworkRouter
    {
        private readonly List<RoadSplineSegment> _drivableSegments = new List<RoadSplineSegment>();
        private readonly Dictionary<RoadSplineSegment, List<RoadJunctionNode>> _segmentEndJunctions = new Dictionary<RoadSplineSegment, List<RoadJunctionNode>>();
        private readonly Dictionary<RoadSplineSegment, List<RoadJunctionNode>> _segmentStartJunctions = new Dictionary<RoadSplineSegment, List<RoadJunctionNode>>();
        private readonly List<TrafficIntersectionNode> _signalNodes = new List<TrafficIntersectionNode>();

        public IReadOnlyList<RoadSplineSegment> DrivableSegments => _drivableSegments;
        public IReadOnlyList<TrafficIntersectionNode> SignalNodes => _signalNodes;

        public void BuildGraph(
            RoadSplineSystem splineSystem,
            IntersectionGenerator intersectionGen,
            IReadOnlyList<TrafficIntersectionNode> liveSignalNodes = null)
        {
            _drivableSegments.Clear();
            _segmentEndJunctions.Clear();
            _segmentStartJunctions.Clear();
            _signalNodes.Clear();

            if (liveSignalNodes != null)
            {
                _signalNodes.AddRange(liveSignalNodes);
            }

            if (splineSystem?.Segments == null)
            {
                return;
            }

            for (int i = 0; i < splineSystem.Segments.Count; i++)
            {
                RoadSplineSegment seg = splineSystem.Segments[i];
                if (seg == null || seg.sampledPointsLocal == null || seg.sampledPointsLocal.Length < 2 || seg.totalLengthMeters < 12f)
                {
                    continue;
                }
                _drivableSegments.Add(seg);
            }

            if (intersectionGen?.Junctions == null)
            {
                return;
            }

            for (int j = 0; j < intersectionGen.Junctions.Count; j++)
            {
                RoadJunctionNode junc = intersectionGen.Junctions[j];
                if (junc?.arms == null) continue;

                for (int a = 0; a < junc.arms.Count; a++)
                {
                    JunctionArmInfo arm = junc.arms[a];
                    if (arm?.segment == null) continue;

                    Dictionary<RoadSplineSegment, List<RoadJunctionNode>> map =
                        arm.entersAtEndNode ? _segmentEndJunctions : _segmentStartJunctions;

                    if (!map.TryGetValue(arm.segment, out List<RoadJunctionNode> list))
                    {
                        list = new List<RoadJunctionNode>();
                        map[arm.segment] = list;
                    }
                    if (!list.Contains(junc))
                    {
                        list.Add(junc);
                    }
                }
            }
        }

        /// <summary>
        /// Computes Bangladesh Left-Hand Traffic (LHT) world position & travel tangent at distance along segment.
        /// Vehicles drive along the LEFT half relative to travel tangent; extraOvertakeShiftMeters > 0 shifts RIGHT toward center to pass.
        /// </summary>
        public static void ComputeLhtLanePose(
            RoadSplineSegment segment,
            float distanceMeters,
            int preferredLane,
            bool travelForward,
            float extraOvertakeShiftMeters,
            out Vector3 worldPos,
            out Vector3 travelTangent)
        {
            if (segment == null)
            {
                worldPos = Vector3.zero;
                travelTangent = Vector3.forward;
                return;
            }

            segment.SampleAtDistance(distanceMeters, out Vector3 centerPos, out Vector3 segTangent, out _);
            travelTangent = (travelForward || segment.isOneWay) ? segTangent : -segTangent;
            travelTangent.y = 0f;
            if (travelTangent.sqrMagnitude < 1e-4f)
            {
                travelTangent = Vector3.forward;
            }
            travelTangent.Normalize();

            Vector3 travelRight = new Vector3(travelTangent.z, 0f, -travelTangent.x).normalized;

            int laneCount = Mathf.Max(1, segment.lanes);
            float halfRoad = Mathf.Max(2.8f, segment.widthMeters * 0.5f);
            float usableHalf = Mathf.Max(1.6f, halfRoad - 0.65f);

            float lateralFromCenter;
            if (segment.isOneWay)
            {
                float laneW = (usableHalf * 2f) / laneCount;
                int laneIdx = Mathf.Clamp(preferredLane, 0, laneCount - 1);
                lateralFromCenter = -usableHalf + laneW * (laneIdx + 0.5f);
            }
            else
            {
                int dirLanes = Mathf.Max(1, laneCount / 2);
                float laneW = usableHalf / dirLanes;
                int laneIdx = Mathf.Clamp(preferredLane, 0, dirLanes - 1);
                // LHT: stay LEFT (-travelRight) of centerline; inner passing lane = closest to center (-laneW*0.5), slow curb lane = outer left!
                lateralFromCenter = -(laneW * (laneIdx + 0.5f));
            }

            lateralFromCenter += extraOvertakeShiftMeters;
            lateralFromCenter = Mathf.Clamp(lateralFromCenter, -halfRoad + 0.45f, halfRoad - 0.45f);

            worldPos = centerPos + travelRight * lateralFromCenter;
        }

        public RoadTransitionChoice PickNextSegment(
            RoadSplineSegment currentSegment,
            bool reachedEndNode,
            Vector3 incomingTravelDir,
            System.Random rng,
            Vector3? attractTargetWorldPos = null)
        {
            if (currentSegment == null)
            {
                return default;
            }

            var lookupMap = reachedEndNode ? _segmentEndJunctions : _segmentStartJunctions;
            Vector3 exitPoint = reachedEndNode ? currentSegment.EndPosition : currentSegment.StartPosition;

            List<RoadTransitionChoice> candidates = new List<RoadTransitionChoice>(8);

            if (lookupMap.TryGetValue(currentSegment, out List<RoadJunctionNode> junctions))
            {
                for (int j = 0; j < junctions.Count; j++)
                {
                    RoadJunctionNode junc = junctions[j];
                    if (junc?.arms == null) continue;
                    for (int a = 0; a < junc.arms.Count; a++)
                    {
                        JunctionArmInfo arm = junc.arms[a];
                        if (arm?.segment == null || arm.segment == currentSegment) continue;
                        bool canEnterEnd = !arm.segment.isOneWay;
                        if (arm.entersAtEndNode && !canEnterEnd) continue;

                        Vector3 outDir = arm.directionOutward;
                        float align = Vector3.Dot(incomingTravelDir, outDir);
                        // Avoid sharp immediate U-turns when forward/cross turns exist
                        if (align < -0.75f) continue;

                        candidates.Add(new RoadTransitionChoice
                        {
                            NextSegment = arm.segment,
                            StartDistanceMeters = arm.entersAtEndNode ? Mathf.Max(0f, arm.segment.totalLengthMeters - 1f) : 1f,
                            TravelForward = !arm.entersAtEndNode
                        });
                    }
                }
            }

            // Proximity fallback in case OSM junction node didn't connect two adjacent segment endpoints directly
            if (candidates.Count == 0)
            {
                for (int i = 0; i < _drivableSegments.Count; i++)
                {
                    RoadSplineSegment seg = _drivableSegments[i];
                    if (seg == currentSegment) continue;

                    if (Vector3.Distance(exitPoint, seg.StartPosition) < 16f)
                    {
                        candidates.Add(new RoadTransitionChoice { NextSegment = seg, StartDistanceMeters = 1f, TravelForward = true });
                    }
                    else if (!seg.isOneWay && Vector3.Distance(exitPoint, seg.EndPosition) < 16f)
                    {
                        candidates.Add(new RoadTransitionChoice { NextSegment = seg, StartDistanceMeters = seg.totalLengthMeters - 1f, TravelForward = false });
                    }
                }
            }

            if (candidates.Count > 0)
            {
                if (attractTargetWorldPos.HasValue)
                {
                    Vector3 target = attractTargetWorldPos.Value;
                    RoadTransitionChoice bestGoal = candidates[0];
                    float bestScore = float.MinValue;
                    for (int i = 0; i < candidates.Count; i++)
                    {
                        RoadTransitionChoice ch = candidates[i];
                        Vector3 farEnd = ch.TravelForward ? ch.NextSegment.EndPosition : ch.NextSegment.StartPosition;
                        Vector3 dir = (farEnd - exitPoint).normalized;
                        float goalDot = Vector3.Dot(dir, (target - exitPoint).normalized);
                        if (goalDot > bestScore)
                        {
                            bestScore = goalDot;
                            bestGoal = ch;
                        }
                    }
                    return bestGoal;
                }

                int pick = rng != null ? rng.Next(candidates.Count) : UnityEngine.Random.Range(0, candidates.Count);
                return candidates[pick];
            }

            // Dead end: smoothly reverse on non-one-way roads, or loop back cleanly on one-way avenues
            bool canReverse = !currentSegment.isOneWay;
            return new RoadTransitionChoice
            {
                NextSegment = currentSegment,
                StartDistanceMeters = canReverse
                    ? (reachedEndNode ? Mathf.Max(0f, currentSegment.totalLengthMeters - 2f) : 2f)
                    : 1f,
                TravelForward = canReverse ? !reachedEndNode : true
            };
        }

        public TrafficIntersectionNode FindAheadSignalIntersection(Vector3 vehiclePos, Vector3 vehicleForward, float maxLookaheadMeters = 28f)
        {
            TrafficIntersectionNode best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < _signalNodes.Count; i++)
            {
                TrafficIntersectionNode node = _signalNodes[i];
                if (node == null || !node.HasActiveSignal) continue;

                Vector3 toNode = node.transform.position - vehiclePos;
                toNode.y = 0f;
                float dist = toNode.magnitude;
                if (dist > maxLookaheadMeters || dist < 1.5f) continue;

                if (Vector3.Dot(vehicleForward, toNode / dist) > 0.72f && dist < bestDist)
                {
                    bestDist = dist;
                    best = node;
                }
            }

            return best;
        }
    }
}
