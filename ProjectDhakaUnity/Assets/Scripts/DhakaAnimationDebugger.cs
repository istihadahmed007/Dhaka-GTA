using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace ProjectDhaka
{
    /// <summary>
    /// Toggleable F3 runtime animation & IK debug visualizer (URP compatible).
    /// Hidden by default in normal play and release builds.
    /// Renders skeleton bones & joints, Foot IK targets & surface normals, knee/elbow hints,
    /// character move/wish directions, predicted obstacle-aware trajectory, sensor rays,
    /// and ground-contact/IK telemetry.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class DhakaAnimationDebugger : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] DhakaProceduralRigDriver rigDriver;
        [SerializeField] DhakaObstacleAvoider obstacleAvoider;
        [SerializeField] PlayerController playerController;
        [SerializeField] Animator animator;

        [Header("Debug Settings")]
        [SerializeField] bool debugEnabled; // Always false by default in game/builds

        public bool DebugEnabled => debugEnabled;

        Transform debugRoot;
        readonly List<LineRenderer> linePool = new();
        readonly List<Transform> markerPool = new();
        Mesh sphereMarkerMesh;
        Material matBone, matJoint, matFootIK, matHint, matMoveDir, matWishDir, matTrajectory, matRayClear, matRayHit, matNormal;
        GUIStyle hudBgStyle, hudTextStyle;

        static readonly (HumanBodyBones a, HumanBodyBones b)[] BoneConnections =
        {
            (HumanBodyBones.Hips, HumanBodyBones.Spine),
            (HumanBodyBones.Spine, HumanBodyBones.Chest),
            (HumanBodyBones.Chest, HumanBodyBones.UpperChest),
            (HumanBodyBones.UpperChest, HumanBodyBones.Neck),
            (HumanBodyBones.Neck, HumanBodyBones.Head),

            (HumanBodyBones.UpperChest, HumanBodyBones.LeftShoulder),
            (HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm),
            (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm),
            (HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand),

            (HumanBodyBones.UpperChest, HumanBodyBones.RightShoulder),
            (HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm),
            (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm),
            (HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand),

            (HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg),
            (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg),
            (HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot),
            (HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes),

            (HumanBodyBones.Hips, HumanBodyBones.RightUpperLeg),
            (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg),
            (HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot),
            (HumanBodyBones.RightFoot, HumanBodyBones.RightToes)
        };

        void Awake()
        {
            ResolveReferences();
            debugEnabled = false;
        }

        public void ResolveReferences()
        {
            rigDriver ??= GetComponent<DhakaProceduralRigDriver>();
            obstacleAvoider ??= GetComponent<DhakaObstacleAvoider>();
            playerController ??= GetComponent<PlayerController>();
            animator ??= GetComponentInChildren<Animator>();
        }

        public void SetDebugEnabled(bool enabled)
        {
            debugEnabled = enabled;
            if (debugRoot != null)
            {
                debugRoot.gameObject.SetActive(debugEnabled);
            }
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
            {
                SetDebugEnabled(!debugEnabled);
            }
        }

        void LateUpdate()
        {
            if (!debugEnabled)
            {
                if (debugRoot != null && debugRoot.gameObject.activeSelf)
                {
                    debugRoot.gameObject.SetActive(false);
                }
                return;
            }

            EnsureVisualPool();
            if (!debugRoot.gameObject.activeSelf) debugRoot.gameObject.SetActive(true);

            int lineIdx = 0;
            int markerIdx = 0;

            // 1. Skeleton Bones & Joints
            if (animator != null && animator.isHuman)
            {
                for (int i = 0; i < BoneConnections.Length; i++)
                {
                    var ta = animator.GetBoneTransform(BoneConnections[i].a);
                    var tb = animator.GetBoneTransform(BoneConnections[i].b);
                    if (ta != null && tb != null)
                    {
                        DrawLine(ref lineIdx, ta.position, tb.position, 0.016f, matBone);
                    }
                }

                var joints = rigDriver != null ? rigDriver.GetSkeletonBones() : System.Array.Empty<Transform>();
                for (int i = 0; i < joints.Length; i++)
                {
                    DrawMarker(ref markerIdx, joints[i].position, 0.030f, matJoint);
                }
            }

            // 2. Foot IK Targets, Ground Contact Normals, Knee & Elbow Hints, Head Look
            if (rigDriver != null)
            {
                DrawFootDebug(ref lineIdx, ref markerIdx, rigDriver.LeftFootTargetPos, rigDriver.LeftFootGrounded, rigDriver.LeftFootHit);
                DrawFootDebug(ref lineIdx, ref markerIdx, rigDriver.RightFootTargetPos, rigDriver.RightFootGrounded, rigDriver.RightFootHit);

                if (animator != null && animator.isHuman)
                {
                    DrawHintDebug(ref lineIdx, ref markerIdx, animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg), rigDriver.LeftKneeHintPos);
                    DrawHintDebug(ref lineIdx, ref markerIdx, animator.GetBoneTransform(HumanBodyBones.RightLowerLeg), rigDriver.RightKneeHintPos);
                    DrawHintDebug(ref lineIdx, ref markerIdx, animator.GetBoneTransform(HumanBodyBones.LeftLowerArm), rigDriver.LeftElbowHintPos);
                    DrawHintDebug(ref lineIdx, ref markerIdx, animator.GetBoneTransform(HumanBodyBones.RightLowerArm), rigDriver.RightElbowHintPos);

                    var headBone = animator.GetBoneTransform(HumanBodyBones.Head);
                    if (headBone != null && rigDriver.HeadAimWeight > 0.02f)
                    {
                        DrawLine(ref lineIdx, headBone.position, rigDriver.HeadLookPos, 0.012f, matHint);
                        DrawMarker(ref markerIdx, rigDriver.HeadLookPos, 0.055f, matHint);
                    }
                }
            }

            // 3. Movement Direction & Wish Direction Arrows
            Vector3 rootBase = transform.position + Vector3.up * 0.08f;
            if (playerController != null)
            {
                Vector3 vel = playerController.PlanarVelocity;
                if (vel.sqrMagnitude > 0.02f)
                {
                    Vector3 tip = rootBase + vel.normalized * Mathf.Clamp(0.45f + vel.magnitude * 0.22f, 0.45f, 2.2f);
                    DrawArrow(ref lineIdx, rootBase, tip, 0.032f, matMoveDir);
                }

                Vector3 wish = playerController.DesiredMoveDirection;
                if (wish.sqrMagnitude > 0.02f)
                {
                    Vector3 wishTip = rootBase + Vector3.up * 0.03f + wish.normalized * 1.45f;
                    DrawArrow(ref lineIdx, rootBase + Vector3.up * 0.03f, wishTip, 0.024f, matWishDir);
                }
            }

            // 4. Obstacle Detection Fan & Predicted Trajectory
            if (obstacleAvoider != null)
            {
                var rays = obstacleAvoider.SensorRays;
                for (int i = 0; i < rays.Count; i++)
                {
                    var r = rays[i];
                    DrawLine(ref lineIdx, r.origin, r.hitPoint, r.hit ? 0.022f : 0.012f, r.hit ? matRayHit : matRayClear);
                    if (r.hit)
                    {
                        DrawMarker(ref markerIdx, r.hitPoint, 0.065f, matRayHit);
                        DrawLine(ref lineIdx, r.hitPoint, r.hitPoint + r.hitNormal * 0.28f, 0.016f, matNormal);
                    }
                }

                var traj = obstacleAvoider.PredictedTrajectory;
                for (int i = 0; i < traj.Count - 1; i++)
                {
                    DrawLine(ref lineIdx, traj[i], traj[i + 1], 0.026f, matTrajectory);
                    if (i % 2 == 1) DrawMarker(ref markerIdx, traj[i], 0.042f, matTrajectory);
                }
                if (traj.Count > 1)
                {
                    Vector3 pPrev = traj[traj.Count - 2];
                    Vector3 pLast = traj[traj.Count - 1];
                    DrawArrow(ref lineIdx, pPrev, pLast, 0.028f, matTrajectory);
                }
            }

            // Disable unused pooled elements
            for (int i = lineIdx; i < linePool.Count; i++) linePool[i].enabled = false;
            for (int i = markerIdx; i < markerPool.Count; i++) markerPool[i].gameObject.SetActive(false);
        }

        void DrawFootDebug(ref int lineIdx, ref int markerIdx, Vector3 targetPos, bool grounded, RaycastHit hit)
        {
            if (targetPos == Vector3.zero) return;
            DrawMarker(ref markerIdx, targetPos, 0.055f, matFootIK);
            if (grounded && hit.collider != null)
            {
                DrawMarker(ref markerIdx, hit.point + Vector3.up * 0.01f, 0.045f, matNormal);
                DrawLine(ref lineIdx, hit.point, hit.point + hit.normal * 0.35f, 0.018f, matNormal);
            }
        }

        void DrawHintDebug(ref int lineIdx, ref int markerIdx, Transform midBone, Vector3 hintPos)
        {
            if (hintPos == Vector3.zero) return;
            DrawMarker(ref markerIdx, hintPos, 0.052f, matHint);
            if (midBone != null) DrawLine(ref lineIdx, midBone.position, hintPos, 0.010f, matHint);
        }

        void DrawArrow(ref int lineIdx, Vector3 from, Vector3 to, float width, Material mat)
        {
            DrawLine(ref lineIdx, from, to, width, mat);
            Vector3 dir = to - from;
            float len = dir.magnitude;
            if (len < 0.08f) return;
            dir /= len;
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            right.Normalize();
            float headLen = Mathf.Min(0.22f, len * 0.32f);
            DrawLine(ref lineIdx, to, to - dir * headLen + right * (headLen * 0.55f), width * 0.85f, mat);
            DrawLine(ref lineIdx, to, to - dir * headLen - right * (headLen * 0.55f), width * 0.85f, mat);
        }

        void DrawLine(ref int lineIdx, Vector3 a, Vector3 b, float width, Material mat)
        {
            while (lineIdx >= linePool.Count)
            {
                var go = new GameObject($"DbgLine_{linePool.Count}");
                go.transform.SetParent(debugRoot, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.shadowCastingMode = ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.numCapVertices = 2;
                linePool.Add(lr);
            }
            var r = linePool[lineIdx++];
            r.enabled = true;
            r.sharedMaterial = mat;
            r.startWidth = width;
            r.endWidth = width;
            r.SetPosition(0, a);
            r.SetPosition(1, b);
        }

        void DrawMarker(ref int markerIdx, Vector3 pos, float size, Material mat)
        {
            while (markerIdx >= markerPool.Count)
            {
                var go = new GameObject($"DbgJoint_{markerPool.Count}");
                go.transform.SetParent(debugRoot, false);
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = sphereMarkerMesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                markerPool.Add(go.transform);
            }
            var tr = markerPool[markerIdx++];
            tr.gameObject.SetActive(true);
            tr.position = pos;
            tr.localScale = Vector3.one * size;
            tr.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        void EnsureVisualPool()
        {
            if (debugRoot != null) return;
            debugRoot = new GameObject("AnimationDebugVisuals_F3").transform;
            debugRoot.SetParent(transform, false);

            var tmpSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereMarkerMesh = Instantiate(tmpSphere.GetComponent<MeshFilter>().sharedMesh);
            Destroy(tmpSphere);

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) unlit = Shader.Find("Sprites/Default");

            matBone = MakeMat("Dbg_Bone", unlit, new Color(0.95f, 0.97f, 1f, 1f));
            matJoint = MakeMat("Dbg_Joint", unlit, new Color(0.20f, 0.85f, 1f, 1f));
            matFootIK = MakeMat("Dbg_FootIK", unlit, new Color(1f, 0.72f, 0.14f, 1f));
            matHint = MakeMat("Dbg_Hint", unlit, new Color(0.75f, 0.45f, 1f, 1f));
            matMoveDir = MakeMat("Dbg_MoveDir", unlit, new Color(0.18f, 0.92f, 0.95f, 1f));
            matWishDir = MakeMat("Dbg_WishDir", unlit, new Color(1f, 0.92f, 0.28f, 1f));
            matTrajectory = MakeMat("Dbg_Traj", unlit, new Color(0.28f, 1f, 0.56f, 1f));
            matRayClear = MakeMat("Dbg_RayClear", unlit, new Color(0.25f, 0.88f, 0.45f, 1f));
            matRayHit = MakeMat("Dbg_RayHit", unlit, new Color(1f, 0.28f, 0.25f, 1f));
            matNormal = MakeMat("Dbg_Normal", unlit, new Color(0.22f, 1f, 0.42f, 1f));
        }

        static Material MakeMat(string name, Shader shader, Color color)
        {
            var m = new Material(shader) { name = name };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            return m;
        }

        void OnGUI()
        {
            if (!debugEnabled) return;

            if (hudBgStyle == null)
            {
                Texture2D bgTex = new Texture2D(1, 1);
                bgTex.SetPixel(0, 0, new Color(0.06f, 0.08f, 0.12f, 0.82f));
                bgTex.Apply();
                hudBgStyle = new GUIStyle(GUI.skin.box);
                hudBgStyle.normal.background = bgTex;

                hudTextStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    richText = true
                };
                hudTextStyle.normal.textColor = new Color(0.93f, 0.96f, 1f, 1f);
            }

            float spd = playerController != null ? playerController.PlanarVelocity.magnitude : 0f;
            bool grounded = playerController != null ? playerController.IsGrounded : true;
            string locState = playerController != null ? playerController.CurrentMotionLabel : "N/A";
            float lW = rigDriver != null ? rigDriver.LeftLegWeight : 0f;
            float rW = rigDriver != null ? rigDriver.RightLegWeight : 0f;
            float pelvisY = rigDriver != null ? rigDriver.PelvisOffsetY : 0f;
            float headW = rigDriver != null ? rigDriver.HeadAimWeight : 0f;
            float armW = rigDriver != null ? Mathf.Max(rigDriver.LeftArmWeight, rigDriver.RightArmWeight) : 0f;
            float steerLean = obstacleAvoider != null ? obstacleAvoider.CurrentAvoidanceLean : 0f;
            string groundL = rigDriver != null && rigDriver.LeftFootGrounded ? rigDriver.LeftFootHit.collider.name : "Air";
            string groundR = rigDriver != null && rigDriver.RightFootGrounded ? rigDriver.RightFootHit.collider.name : "Air";

            // Keep left side cleanly below SampleScene's INTEL HUD card and off the center protagonist silhouette
            GUILayout.BeginArea(new Rect(16f, 118f, 294f, 142f), hudBgStyle);
            GUILayout.Label(
                "<b><color=#48E5FF>[F3] ANIMATION & IK DEBUG</color></b>\n" +
                $"State: <b>{locState}</b> | Grounded: <b>{grounded}</b> | Spd: <b>{spd:0.00} m/s</b>\n" +
                $"Foot IK L: <b>{lW:0.00}</b>  R: <b>{rW:0.00}</b> | PelvisY: <b>{pelvisY:+0.000;-0.000;0.000}m</b>\n" +
                $"Steer: <b>{steerLean:+0.00;-0.00;0.00}</b> | HeadW: <b>{headW:0.00}</b> | HandW: <b>{armW:0.00}</b>\n" +
                $"Hit L: <color=#8EFFA2>{groundL}</color>  R: <color=#8EFFA2>{groundR}</color>\n" +
                "<color=#A8B5C8>White=Bones | Orange=FootIK | Purple=Hints | Green/Red=Avoid</color>",
                hudTextStyle);
            GUILayout.EndArea();
        }
    }
}
