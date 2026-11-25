using System.Text;
using UnityEngine;
using ProjectDhaka;

public static class VerifyAnimationLabPlayMode
{
    static PlayerController FindPlayer() => Object.FindAnyObjectByType<PlayerController>();

    public static string StageIdle() => SetStage("idle");
    public static string StageConesArrows() => SetStage("cones_arrows");
    public static string StageF3Debug() => SetStage("f3_debug_cones");
    public static string StageStairsIK() => SetStage("slope_stairs_ik");
    public static string StageSlopeSprint() => SetStage("slope_sprint");
    public static string StageUnevenClean() => SetStage("uneven_ik_clean");
    public static string StageStrafeInteract() => SetStage("strafe_interact");
    public static string StageSideWalk() => SetStage("side_walk");
    public static string StageJump() => SetStage("jump");
    public static string StageCleanup() => SetStage("stop_cleanup");

    public static string SetStage(string stage)
    {
        var player = FindPlayer();
        if (player == null) return "ERROR: No PlayerController in scene";

        var driver = player.GetComponent<DhakaProceduralRigDriver>();
        var avoider = player.GetComponent<DhakaObstacleAvoider>();
        var dbg = player.GetComponent<DhakaAnimationDebugger>();
        var cc = player.GetComponent<CharacterController>();

        switch (stage)
        {
            case "idle":
                Teleport(cc, player.transform, new Vector3(0f, 0.02f, -0.5f), Quaternion.identity);
                player.ResetVelocity();
                player.SetSimulatedInput(false, Vector2.zero);
                if (avoider != null) avoider.ShowWorldTrajectoryArrows = false;
                if (dbg != null) dbg.SetDebugEnabled(false);
                break;

            case "cones_arrows":
                // Jog directly toward Cone_01 at (0,0,4.5) and through slalom lane with world trajectory arrows ON
                Teleport(cc, player.transform, new Vector3(0f, 0.02f, 0.2f), Quaternion.identity);
                player.ResetVelocity();
                player.SetSimulatedInput(true, new Vector2(0f, 1f), sprint: false, walk: false, durationSeconds: 6.5f);
                if (avoider != null) avoider.ShowWorldTrajectoryArrows = true;
                if (dbg != null) dbg.SetDebugEnabled(false);
                break;

            case "f3_debug_cones":
                // Approach Cone cluster with F3 debug skeleton, Foot IK, hints, red/green rays, predicted curve & HUD ON
                Teleport(cc, player.transform, new Vector3(0.0f, 0.02f, 2.2f), Quaternion.identity);
                player.ResetVelocity();
                if (avoider != null) avoider.ShowWorldTrajectoryArrows = true;
                if (dbg != null) dbg.SetDebugEnabled(true);
                player.SetSimulatedInput(true, new Vector2(0f, 1f), sprint: false, walk: false, durationSeconds: 6.5f);
                break;

            case "slope_stairs_ik":
                // Climb 9-step stairs dynamically with F3 debug ON showing unequal L/R Foot IK & pelvis drop
                Teleport(cc, player.transform, new Vector3(-4.2f, 0.02f, -1.6f), Quaternion.Euler(0f, 180f, 0f));
                player.ResetVelocity();
                if (avoider != null) avoider.ShowWorldTrajectoryArrows = true;
                if (dbg != null) dbg.SetDebugEnabled(true);
                player.SetSimulatedInput(true, new Vector2(0f, 1f), sprint: false, walk: true, durationSeconds: 6.5f);
                break;

            case "slope_sprint":
                // Sprint dynamically up 14-deg sloped ramp from dramatic 3/4 view
                Teleport(cc, player.transform, new Vector3(-8.5f, 0.02f, -0.8f), Quaternion.identity);
                player.ResetVelocity();
                if (avoider != null) avoider.ShowWorldTrajectoryArrows = false;
                if (dbg != null) dbg.SetDebugEnabled(false);
                Object.FindAnyObjectByType<DhakaCameraRig>()?.SetCameraAngles(42f, 12f);
                player.SetSimulatedInput(true, new Vector2(-0.67f, 0.74f), sprint: true, walk: false, durationSeconds: 5.5f);
                break;

            case "uneven_ik_clean":
                // Walk slowly across uneven cobblestone pads to capture Foot IK adaptation cleanly
                Teleport(cc, player.transform, new Vector3(6.6f, 0.05f, -6.5f), Quaternion.identity);
                player.ResetVelocity();
                if (avoider != null) avoider.ShowWorldTrajectoryArrows = false;
                if (dbg != null) dbg.SetDebugEnabled(false);
                player.SetSimulatedInput(true, new Vector2(0f, 1f), sprint: false, walk: true, durationSeconds: 6.5f);
                break;

            case "strafe_interact":
                // Strafe sideways right past Dhaka supply crate to capture BlendTree StrafeLeft & Head/Hand IK
                Teleport(cc, player.transform, new Vector3(1.85f, 0.02f, 2.55f), Quaternion.Euler(0f, 82f, 0f));
                player.ResetVelocity();
                if (avoider != null) avoider.ShowWorldTrajectoryArrows = false;
                if (dbg != null) dbg.SetDebugEnabled(false);
                player.SetSimulatedInput(true, new Vector2(-1f, 0.05f), sprint: false, walk: false, jump: false, strafe: true, durationSeconds: 6.0f);
                break;

            case "side_walk":
                // Pure 90-deg side camera angle while actively walking forward (+Z) with F3 debug ON
                Teleport(cc, player.transform, new Vector3(14f, 0.02f, -2.0f), Quaternion.identity);
                player.ResetVelocity();
                if (avoider != null) avoider.ShowWorldTrajectoryArrows = true;
                if (dbg != null) dbg.SetDebugEnabled(true);
                Object.FindAnyObjectByType<DhakaCameraRig>()?.SetCameraAngles(-85f, 10f);
                player.SetSimulatedInput(true, new Vector2(1f, 0f), sprint: false, walk: true, jump: false, strafe: false, durationSeconds: 6.5f);
                break;

            case "jump":
                // Running periodic jumps along flat lane so capture snaps genuine airborne / takeoff pose
                Teleport(cc, player.transform, new Vector3(2.2f, 0.02f, -4.0f), Quaternion.identity);
                player.ResetVelocity();
                if (avoider != null) avoider.ShowWorldTrajectoryArrows = false;
                if (dbg != null) dbg.SetDebugEnabled(false);
                Object.FindAnyObjectByType<DhakaCameraRig>()?.SetCameraAngles(35f, 11f);
                player.SetSimulatedInput(true, new Vector2(-0.57f, 0.82f), sprint: true, walk: false, jump: true, strafe: false, durationSeconds: 6.0f, repeatJump: true);
                break;

            case "stop_cleanup":
                player.SetSimulatedInput(false, Vector2.zero);
                if (avoider != null) avoider.ShowWorldTrajectoryArrows = false;
                if (dbg != null) dbg.SetDebugEnabled(false);
                break;
        }

        return SampleTelemetry();
    }

    public static string SampleTelemetry()
    {
        var player = FindPlayer();
        if (player == null) return "ERROR: No PlayerController";

        var driver = player.GetComponent<DhakaProceduralRigDriver>();
        var avoider = player.GetComponent<DhakaObstacleAvoider>();
        var dbg = player.GetComponent<DhakaAnimationDebugger>();
        var anim = player.GetComponentInChildren<Animator>();

        int rayHits = 0;
        if (avoider != null)
        {
            for (int i = 0; i < avoider.SensorRays.Count; i++)
                if (avoider.SensorRays[i].hit) rayHits++;
        }

        var stInfo = anim != null ? anim.GetCurrentAnimatorStateInfo(0) : default;
        var sb = new StringBuilder();
        sb.Append($"frame={Time.frameCount}");
        sb.Append($" | pos={player.transform.position.ToString("F2")}");
        sb.Append($" | label={player.CurrentMotionLabel}");
        sb.Append($" | spd={player.PlanarVelocity.magnitude:F2}");
        sb.Append($" | grounded={player.IsGrounded}");
        if (driver != null)
        {
            sb.Append($" | legIK=({driver.LeftLegWeight:F2},{driver.RightLegWeight:F2})");
            sb.Append($" | pelvisY={driver.PelvisOffsetY:F3}");
            sb.Append($" | headW={driver.HeadAimWeight:F2}");
        }
        if (avoider != null)
        {
            sb.Append($" | raysHit={rayHits}/{avoider.SensorRays.Count}");
            sb.Append($" | steerLean={avoider.CurrentAvoidanceLean:F2}");
            sb.Append($" | arrows={avoider.ShowWorldTrajectoryArrows}");
        }
        if (dbg != null) sb.Append($" | f3={dbg.DebugEnabled}");
        if (anim != null) sb.Append($" | animNormT={stInfo.normalizedTime:F2}");
        return sb.ToString();
    }

    static void Teleport(CharacterController cc, Transform tr, Vector3 pos, Quaternion rot)
    {
        bool wasEnabled = cc != null && cc.enabled;
        if (wasEnabled) cc.enabled = false;
        tr.SetPositionAndRotation(pos, rot);
        if (wasEnabled) cc.enabled = true;
        var camRig = Object.FindAnyObjectByType<DhakaCameraRig>();
        if (camRig != null) camRig.ResetForRunStart();
    }
}
