using System;
using System.Collections.Generic;
using DhakaGta.GIS;
using UnityEngine;

namespace DhakaGta.WorldGen
{
    public enum RoadGradeKind
    {
        Surface = 0,
        Bridge = 1,
        Flyover = 2,
        Underpass = 3,
    }

    /// <summary>
    /// Smooth C1 Catmull-Rom spline built strictly from real OpenStreetMap polyline vertices
    /// (preserving actual Dhaka curves, diagonal avenues, flyovers, and roundabouts).
    /// Carries vehicle navigation metadata (speed limit, lanes, one-way, left-hand-traffic polarity).
    /// </summary>
    [Serializable]
    public sealed class RoadSplineSegment
    {
        public string osmId;
        public string roadName;
        public string banglaName;
        public string highwayClass;
        public RoadGradeKind gradeKind;
        public bool isRoundabout;
        public bool isOneWay;
        public bool hasSidewalk;
        public int lanes;
        public float widthMeters;
        public float maxSpeedKph;
        public float totalLengthMeters;
        public long startOsmNodeId;
        public long endOsmNodeId;
        public UtmCoord[] controlPointsUtm = Array.Empty<UtmCoord>();
        public long[] controlNodeIds = Array.Empty<long>();
        public Vector3[] sampledPointsLocal = Array.Empty<Vector3>();
        public Vector3[] sampledTangents = Array.Empty<Vector3>();
        public float[] cumulativeDistances = Array.Empty<float>();

        public Vector3 StartPosition => sampledPointsLocal != null && sampledPointsLocal.Length > 0 ? sampledPointsLocal[0] : Vector3.zero;
        public Vector3 EndPosition => sampledPointsLocal != null && sampledPointsLocal.Length > 0 ? sampledPointsLocal[sampledPointsLocal.Length - 1] : Vector3.zero;

        public void SampleAtDistance(float distanceMeters, out Vector3 position, out Vector3 tangent, out Vector3 right)
        {
            if (sampledPointsLocal == null || sampledPointsLocal.Length == 0)
            {
                position = Vector3.zero;
                tangent = Vector3.forward;
                right = Vector3.right;
                return;
            }

            if (sampledPointsLocal.Length == 1)
            {
                position = sampledPointsLocal[0];
                tangent = sampledTangents[0];
                right = Vector3.Cross(Vector3.up, tangent).normalized;
                return;
            }

            float d = Mathf.Clamp(distanceMeters, 0f, totalLengthMeters);
            int hi = 1;
            while (hi < cumulativeDistances.Length - 1 && cumulativeDistances[hi] < d)
            {
                hi++;
            }

            int lo = hi - 1;
            float segLen = Mathf.Max(0.0001f, cumulativeDistances[hi] - cumulativeDistances[lo]);
            float t = Mathf.Clamp01((d - cumulativeDistances[lo]) / segLen);

            position = Vector3.Lerp(sampledPointsLocal[lo], sampledPointsLocal[hi], t);
            tangent = Vector3.Slerp(sampledTangents[lo], sampledTangents[hi], t).normalized;
            if (tangent.sqrMagnitude < 1e-4f) tangent = Vector3.forward;
            right = new Vector3(tangent.z, 0f, -tangent.x).normalized;
        }

        /// <summary>
        /// Evaluates a specific drivable lane centerline at distanceMeters (supports Bangladesh Left-Hand Traffic default).
        /// </summary>
        public Vector3 GetLaneCenterPosition(float distanceMeters, int laneIndex, bool isReverseDirection = false)
        {
            SampleAtDistance(distanceMeters, out Vector3 pos, out _, out Vector3 right);
            float laneWidth = widthMeters / Mathf.Max(1, lanes);
            float halfW = widthMeters * 0.5f;
            int clampedLane = Mathf.Clamp(laneIndex, 0, Mathf.Max(0, lanes - 1));
            // Offset across carriageway width from left edge to right edge
            float lateralOffset = -halfW + laneWidth * (clampedLane + 0.5f);
            if (isReverseDirection && !isOneWay)
            {
                lateralOffset = -Mathf.Abs(lateralOffset);
            }
            return pos + right * lateralOffset;
        }
    }

    /// <summary>
    /// Converts imported GeoRoadFeature polylines into smooth spline segments and exposes spatial road queries
    /// for RoadGenerator, IntersectionGenerator, and vehicle navigation/AI.
    /// </summary>
    public sealed class RoadSplineSystem : MonoBehaviour
    {
        [SerializeField] private float sampleStepMeters = 6f;
        [SerializeField] private float bridgeDeckElevationMeters = 4.2f;
        [SerializeField] private float flyoverDeckElevationMeters = 8.5f;
        [SerializeField] private float underpassDepthMeters = -4.6f;

        [SerializeField] private List<RoadSplineSegment> _segments = new List<RoadSplineSegment>();

        public IReadOnlyList<RoadSplineSegment> Segments => _segments;

        public void BuildFromDatabase(GeoFeatureDatabase db, WorldCoordinateManager coordManager, double maxRadiusMeters = 0.0)
        {
            _segments.Clear();
            if (db == null || coordManager == null)
            {
                return;
            }

            UtmCoord origin = coordManager.BaseOriginUtm;
            foreach (GeoRoadFeature road in db.roads)
            {
                if (road?.pointsUtm == null || road.pointsUtm.Length < 2) continue;
                if (maxRadiusMeters > 0.0 && road.pointsUtm[0].Distance2D(origin) > maxRadiusMeters) continue;

                RoadSplineSegment seg = CreateSplineSegment(road, coordManager);
                if (seg != null && seg.totalLengthMeters >= 1.5f)
                {
                    _segments.Add(seg);
                }
            }
        }

        public RoadSplineSegment FindClosestSegment(Vector3 worldPos, out float distanceMeters, out Vector3 closestPoint, out Vector3 tangent)
        {
            RoadSplineSegment best = null;
            distanceMeters = float.MaxValue;
            closestPoint = worldPos;
            tangent = Vector3.forward;

            foreach (RoadSplineSegment seg in _segments)
            {
                if (seg?.sampledPointsLocal == null || seg.sampledPointsLocal.Length < 2) continue;

                for (int i = 0; i < seg.sampledPointsLocal.Length - 1; i++)
                {
                    Vector3 a = seg.sampledPointsLocal[i];
                    Vector3 b = seg.sampledPointsLocal[i + 1];
                    Vector3 ab = b - a;
                    float lenSq = ab.sqrMagnitude;
                    float t = lenSq > 1e-5f ? Mathf.Clamp01(Vector3.Dot(worldPos - a, ab) / lenSq) : 0f;
                    Vector3 proj = a + ab * t;
                    float dist = Vector3.Distance(worldPos, proj);
                    if (dist < distanceMeters)
                    {
                        distanceMeters = dist;
                        closestPoint = proj;
                        tangent = seg.sampledTangents[i];
                        best = seg;
                    }
                }
            }

            return best;
        }

        private RoadSplineSegment CreateSplineSegment(GeoRoadFeature road, WorldCoordinateManager coordManager)
        {
            RoadGradeKind grade = ResolveGradeKind(road);
            float targetY = grade switch
            {
                RoadGradeKind.Bridge => bridgeDeckElevationMeters,
                RoadGradeKind.Flyover => flyoverDeckElevationMeters,
                RoadGradeKind.Underpass => underpassDepthMeters,
                _ => 0.02f,
            };

            int ctrlCount = road.pointsUtm.Length;
            var ctrlLocal = new Vector3[ctrlCount];
            for (int i = 0; i < ctrlCount; i++)
            {
                float progress = ctrlCount > 1 ? (float)i / (ctrlCount - 1) : 0.5f;
                float elev = EvaluateSmoothElevationEnvelope(grade, progress, targetY);
                ctrlLocal[i] = coordManager.UtmToUnityPosition(road.pointsUtm[i], elev);
            }

            List<Vector3> samples = SampleCatmullRomChain(ctrlLocal, Mathf.Max(2.5f, sampleStepMeters));
            if (samples.Count < 2) return null;

            var tangents = new Vector3[samples.Count];
            var cumDist = new float[samples.Count];
            float totalLen = 0f;
            cumDist[0] = 0f;

            for (int i = 0; i < samples.Count; i++)
            {
                if (i > 0)
                {
                    totalLen += Vector3.Distance(samples[i - 1], samples[i]);
                    cumDist[i] = totalLen;
                }

                Vector3 prev = i > 0 ? samples[i - 1] : samples[i];
                Vector3 next = i < samples.Count - 1 ? samples[i + 1] : samples[i];
                Vector3 tan = next - prev;
                if (tan.sqrMagnitude < 1e-6f) tan = Vector3.forward;
                tangents[i] = tan.normalized;
            }

            long startNode = road.osmNodeIds != null && road.osmNodeIds.Length > 0 ? road.osmNodeIds[0] : 0;
            long endNode = road.osmNodeIds != null && road.osmNodeIds.Length > 0 ? road.osmNodeIds[road.osmNodeIds.Length - 1] : 0;

            return new RoadSplineSegment
            {
                osmId = road.osmId,
                roadName = string.IsNullOrEmpty(road.name) ? road.highwayClass : road.name,
                banglaName = road.banglaName,
                highwayClass = road.highwayClass,
                gradeKind = grade,
                isRoundabout = road.junctionType == "roundabout" || road.junctionType == "circular",
                isOneWay = road.isOneWay || road.junctionType == "roundabout" || road.junctionType == "circular",
                hasSidewalk = road.hasSidewalk && grade == RoadGradeKind.Surface,
                lanes = Mathf.Max(1, road.lanes),
                widthMeters = Mathf.Max(4.5f, road.widthMeters),
                maxSpeedKph = road.maxSpeedKph > 1f ? road.maxSpeedKph : 50f,
                totalLengthMeters = totalLen,
                startOsmNodeId = startNode,
                endOsmNodeId = endNode,
                controlPointsUtm = road.pointsUtm,
                controlNodeIds = road.osmNodeIds ?? Array.Empty<long>(),
                sampledPointsLocal = samples.ToArray(),
                sampledTangents = tangents,
                cumulativeDistances = cumDist,
            };
        }

        private static RoadGradeKind ResolveGradeKind(GeoRoadFeature road)
        {
            if (road.isUnderpass || road.osmLayer < 0) return RoadGradeKind.Underpass;
            if (road.isFlyover || road.osmLayer >= 2) return RoadGradeKind.Flyover;
            if (road.isBridge || road.osmLayer == 1) return RoadGradeKind.Bridge;
            return RoadGradeKind.Surface;
        }

        private static float EvaluateSmoothElevationEnvelope(RoadGradeKind grade, float t, float peakY)
        {
            if (grade == RoadGradeKind.Surface) return peakY;
            // Smooth ramp onto and off bridge/flyover/underpass approaches
            float edge = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
            float plateau = Mathf.Clamp01(edge * 1.45f);
            return Mathf.Lerp(0.02f, peakY, plateau);
        }

        private static List<Vector3> SampleCatmullRomChain(Vector3[] pts, float stepMeters)
        {
            var output = new List<Vector3>(pts.Length * 2);
            if (pts.Length < 2) return output;

            output.Add(pts[0]);
            for (int i = 0; i < pts.Length - 1; i++)
            {
                Vector3 p0 = i > 0 ? pts[i - 1] : pts[0];
                Vector3 p1 = pts[i];
                Vector3 p2 = pts[i + 1];
                Vector3 p3 = i + 2 < pts.Length ? pts[i + 2] : pts[pts.Length - 1];

                float chord = Vector3.Distance(p1, p2);
                if (chord < 0.2f) continue;

                int steps = Mathf.Clamp(Mathf.CeilToInt(chord / stepMeters), 1, 16);
                for (int s = 1; s <= steps; s++)
                {
                    float t = (float)s / steps;
                    output.Add(EvaluateCatmullRom(p0, p1, p2, p3, t));
                }
            }

            return output;
        }

        private static Vector3 EvaluateCatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                (2f * p1)
                + (-p0 + p2) * t
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }
    }
}
