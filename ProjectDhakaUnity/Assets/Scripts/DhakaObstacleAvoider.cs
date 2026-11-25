using System.Collections.Generic;
using UnityEngine;

namespace ProjectDhaka
{
    /// <summary>
    /// Predictive obstacle detection, smooth steering avoidance, and curved trajectory forecasting
    /// with optional world-space directional arrows matching the AnimationLab visual reference.
    /// </summary>
    public class DhakaObstacleAvoider : MonoBehaviour
    {
        public struct SensorRayInfo
        {
            public Vector3 origin;
            public Vector3 dir;
            public float maxDist;
            public bool hit;
            public Vector3 hitPoint;
            public Vector3 hitNormal;
            public float hitFraction;
        }

        [Header("Obstacle Detection")]
        [SerializeField] LayerMask obstacleLayerMask = ~0;
        [SerializeField] float lookAheadDistance = 2.6f;
        [SerializeField] float probeRadius = 0.28f;
        [SerializeField] float avoidanceStrength = 1.15f;
        [SerializeField] float steerSmoothSpeed = 9f;
        [SerializeField] float maxStepClearanceHeight = 0.34f;

        [Header("Trajectory Prediction")]
        [SerializeField] int trajectoryPointsCount = 12;
        [SerializeField] float trajectoryStepSeconds = 0.09f;
        [SerializeField] bool showWorldTrajectoryArrows = false;

        readonly List<SensorRayInfo> sensorRays = new();
        readonly List<Vector3> predictedTrajectory = new();
        readonly List<GameObject> arrowPool = new();
        Transform arrowsRoot;
        Material arrowMat;
        LineRenderer trajectoryLine;

        float smoothedSteerAngle;
        int lastAvoidSide = 1; // Hysteresis prevents oscillation dead-center in front of cones

        public IReadOnlyList<SensorRayInfo> SensorRays => sensorRays;
        public IReadOnlyList<Vector3> PredictedTrajectory => predictedTrajectory;
        public float CurrentAvoidanceLean { get; private set; }
        public float SpeedScaleFactor { get; private set; } = 1f;
        public bool ShowWorldTrajectoryArrows
        {
            get => showWorldTrajectoryArrows;
            set
            {
                showWorldTrajectoryArrows = value;
                UpdateArrowVisualVisibility();
            }
        }

        static readonly float[] FanAnglesDeg = { 0f, -22f, 22f, -45f, 45f, -68f, 68f };

        void Awake()
        {
            BuildOptionalTrajectoryVisuals();
        }

        void BuildOptionalTrajectoryVisuals()
        {
            arrowsRoot = new GameObject("TrajectoryArrows_Runtime").transform;
            arrowsRoot.SetParent(transform, false);

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) unlit = Shader.Find("Sprites/Default");
            arrowMat = new Material(unlit) { name = "TrajectoryArrowMat" };
            if (arrowMat.HasProperty("_BaseColor")) arrowMat.SetColor("_BaseColor", Palette.AccentBright);
            if (arrowMat.HasProperty("_Color")) arrowMat.SetColor("_Color", Palette.AccentBright);

            var lineGo = new GameObject("PredictedPathLine");
            lineGo.transform.SetParent(arrowsRoot, false);
            trajectoryLine = lineGo.AddComponent<LineRenderer>();
            trajectoryLine.useWorldSpace = true;
            trajectoryLine.startWidth = 0.045f;
            trajectoryLine.endWidth = 0.018f;
            trajectoryLine.sharedMaterial = arrowMat;
            trajectoryLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trajectoryLine.receiveShadows = false;

            for (int i = 0; i < trajectoryPointsCount; i++)
            {
                var arrow = CreateArrowMeshObject($"TrajArrow_{i}", arrowsRoot, arrowMat);
                arrow.SetActive(false);
                arrowPool.Add(arrow);
            }
            UpdateArrowVisualVisibility();
        }

        static GameObject CreateArrowMeshObject(string name, Transform parent, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            // Crisp flat forward chevron arrow mesh pointing +Z
            var mesh = new Mesh { name = "ChevronArrow" };
            mesh.vertices = new[]
            {
                new Vector3(0f, 0.02f, 0.18f),
                new Vector3(-0.11f, 0.02f, -0.06f),
                new Vector3(-0.04f, 0.02f, -0.02f),
                new Vector3(0.04f, 0.02f, -0.02f),
                new Vector3(0.11f, 0.02f, -0.06f),
                new Vector3(-0.04f, 0.02f, -0.14f),
                new Vector3(0.04f, 0.02f, -0.14f)
            };
            mesh.triangles = new[]
            {
                0, 4, 3,
                0, 3, 2,
                0, 2, 1,
                2, 3, 6,
                2, 6, 5
            };
            mesh.RecalculateNormals();
            mf.sharedMesh = mesh;
            return go;
        }

        void UpdateArrowVisualVisibility()
        {
            if (arrowsRoot != null) arrowsRoot.gameObject.SetActive(showWorldTrajectoryArrows);
        }

        /// <summary>
        /// Evaluates obstacle clearance rays around desired wish direction, returns adjusted avoidance direction,
        /// speed damping, upper-body lean factor, and future predicted trajectory points.
        /// </summary>
        public Vector3 EvaluateMovementAndPredict(Vector3 wishDir, Vector3 currentPlanarVel, float currentMaxSpeed, float dt)
        {
            sensorRays.Clear();
            predictedTrajectory.Clear();

            Vector3 forwardProbe = wishDir.sqrMagnitude > 0.01f ? wishDir.normalized : transform.forward;
            float speed = currentPlanarVel.magnitude;
            float dynamicLookAhead = Mathf.Lerp(1.35f, lookAheadDistance, Mathf.Clamp01(speed / 7f));

            // Multi-height probe (chest/hip level 0.65m ignores climbable small steps below maxStepClearanceHeight)
            Vector3 probeOrigin = transform.position + Vector3.up * 0.62f;
            float leftClearance = 0f;
            float rightClearance = 0f;
            float centerHitFrac = 1f;
            bool centerBlocked = false;

            for (int i = 0; i < FanAnglesDeg.Length; i++)
            {
                float ang = FanAnglesDeg[i];
                Vector3 dir = Quaternion.AngleAxis(ang, Vector3.up) * forwardProbe;
                bool hitFound = CastIgnoringSelf(probeOrigin, dir, dynamicLookAhead, out RaycastHit bestHit);

                // Ignore walkable gentle ramps or low steps that CharacterController steps over naturally
                if (hitFound && (bestHit.normal.y > 0.52f || IsLowClimbableStep(bestHit)))
                {
                    hitFound = false;
                }

                float frac = hitFound ? Mathf.Clamp01(bestHit.distance / dynamicLookAhead) : 1f;
                sensorRays.Add(new SensorRayInfo
                {
                    origin = probeOrigin,
                    dir = dir,
                    maxDist = dynamicLookAhead,
                    hit = hitFound,
                    hitPoint = hitFound ? bestHit.point : probeOrigin + dir * dynamicLookAhead,
                    hitNormal = hitFound ? bestHit.normal : Vector3.up,
                    hitFraction = frac
                });

                if (i == 0 && hitFound)
                {
                    centerBlocked = true;
                    centerHitFrac = frac;
                }
                else if (ang < 0f)
                {
                    leftClearance += frac;
                }
                else if (ang > 0f)
                {
                    rightClearance += frac;
                }
            }

            float targetSteerDeg = 0f;
            float targetSpeedScale = 1f;
            if (wishDir.sqrMagnitude > 0.01f)
            {
                if (centerBlocked || leftClearance < 2.65f || rightClearance < 2.65f)
                {
                    float sideDiff = rightClearance - leftClearance;
                    int chosenSide = Mathf.Abs(sideDiff) > 0.14f ? (sideDiff > 0f ? 1 : -1) : lastAvoidSide;
                    lastAvoidSide = chosenSide;

                    float urgency = centerBlocked ? (1f - centerHitFrac * 0.65f) : (1f - Mathf.Min(leftClearance, rightClearance) / 3f);
                    urgency = Mathf.Clamp01(urgency);
                    targetSteerDeg = chosenSide * urgency * 58f * avoidanceStrength;
                    targetSpeedScale = centerBlocked ? Mathf.Lerp(0.55f, 0.92f, centerHitFrac) : Mathf.Lerp(0.80f, 1.0f, 1f - urgency);
                }
            }

            smoothedSteerAngle = Mathf.Lerp(smoothedSteerAngle, targetSteerDeg, 1f - Mathf.Exp(-steerSmoothSpeed * dt));
            SpeedScaleFactor = Mathf.Lerp(SpeedScaleFactor, targetSpeedScale, 1f - Mathf.Exp(-8f * dt));
            CurrentAvoidanceLean = Mathf.Clamp(smoothedSteerAngle / 55f, -1f, 1f);

            Vector3 adjustedDir = Quaternion.AngleAxis(smoothedSteerAngle, Vector3.up) * forwardProbe;
            adjustedDir.y = 0f;
            if (adjustedDir.sqrMagnitude > 0.0001f) adjustedDir.Normalize();

            BuildPredictedTrajectory(wishDir.sqrMagnitude > 0.01f ? adjustedDir * currentMaxSpeed : Vector3.zero, currentPlanarVel);
            RefreshArrowVisuals();

            return wishDir.sqrMagnitude > 0.01f ? adjustedDir : Vector3.zero;
        }

        bool IsLowClimbableStep(RaycastHit hit)
        {
            // Check if obstacle top surface is within CharacterController step height above player feet
            return hit.collider != null && hit.collider.bounds.max.y <= transform.position.y + maxStepClearanceHeight;
        }

        bool CastIgnoringSelf(Vector3 origin, Vector3 dir, float maxDist, out RaycastHit bestHit)
        {
            bestHit = default;
            RaycastHit[] hits = Physics.SphereCastAll(origin, probeRadius, dir, maxDist, obstacleLayerMask, QueryTriggerInteraction.Ignore);
            float closest = float.MaxValue;
            bool found = false;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider == null || hits[i].collider.transform.IsChildOf(transform)) continue;
                if (hits[i].distance <= 0.001f) continue;
                if (hits[i].distance < closest)
                {
                    closest = hits[i].distance;
                    bestHit = hits[i];
                    found = true;
                }
            }
            return found;
        }

        void BuildPredictedTrajectory(Vector3 targetVel, Vector3 currentVel)
        {
            Vector3 simPos = transform.position;
            Vector3 simVel = currentVel;
            predictedTrajectory.Add(simPos + Vector3.up * 0.04f);

            for (int i = 1; i <= trajectoryPointsCount; i++)
            {
                simVel = Vector3.MoveTowards(simVel, targetVel, 22f * trajectoryStepSeconds);
                simPos += simVel * trajectoryStepSeconds;

                // Project trajectory onto ground surface
                Vector3 gOrigin = new Vector3(simPos.x, simPos.y + 0.9f, simPos.z);
                if (Physics.Raycast(gOrigin, Vector3.down, out RaycastHit gHit, 2.2f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!gHit.collider.transform.IsChildOf(transform))
                    {
                        simPos.y = gHit.point.y;
                    }
                }
                predictedTrajectory.Add(simPos + Vector3.up * 0.04f);
            }
        }

        void RefreshArrowVisuals()
        {
            if (!showWorldTrajectoryArrows || trajectoryLine == null) return;

            trajectoryLine.positionCount = predictedTrajectory.Count;
            for (int i = 0; i < predictedTrajectory.Count; i++)
            {
                trajectoryLine.SetPosition(i, predictedTrajectory[i]);
            }

            bool moving = predictedTrajectory.Count > 2 &&
                          (predictedTrajectory[predictedTrajectory.Count - 1] - predictedTrajectory[0]).sqrMagnitude > 0.08f;

            for (int i = 0; i < arrowPool.Count; i++)
            {
                int ptIdx = i + 1;
                if (!moving || ptIdx >= predictedTrajectory.Count)
                {
                    arrowPool[i].SetActive(false);
                    continue;
                }
                Vector3 p0 = predictedTrajectory[ptIdx - 1];
                Vector3 p1 = predictedTrajectory[ptIdx];
                Vector3 seg = p1 - p0;
                seg.y = 0f;
                if (seg.sqrMagnitude < 0.0005f)
                {
                    arrowPool[i].SetActive(false);
                    continue;
                }
                arrowPool[i].SetActive(true);
                arrowPool[i].transform.position = p1;
                arrowPool[i].transform.rotation = Quaternion.LookRotation(seg.normalized, Vector3.up);
                float fadeScale = Mathf.Lerp(1.0f, 0.55f, i / (float)arrowPool.Count);
                arrowPool[i].transform.localScale = Vector3.one * fadeScale;
            }
        }
    }
}
