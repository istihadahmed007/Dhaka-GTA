using UnityEngine;
using ProjectDhaka;

public static class VerifySampleScenePlayMode
{
    public static string StageTitle()
    {
        var loop = Object.FindAnyObjectByType<GameLoop>();
        var p = Object.FindAnyObjectByType<PlayerController>();
        return $"state={loop?.CurrentState} | player={p?.gameObject.name} | pos={p?.transform.position.ToString("F2")} | frame={Time.frameCount}";
    }

    public static string StageStartGameplay()
    {
        var loop = Object.FindAnyObjectByType<GameLoop>();
        loop?.StartRun();
        var p = Object.FindAnyObjectByType<PlayerController>();
        var atm = Object.FindAnyObjectByType<DhakaAtmosphereController>();
        atm?.SetPreset(DhakaAtmosphereController.TimeOfDayPreset.MonsoonDusk, 1);
        p?.SetSimulatedInput(true, new Vector2(0.15f, 1f), sprint: false, walk: false, durationSeconds: 5.5f);
        return Sample();
    }

    public static string StageOpenWorldFlyoverF3()
    {
        var p = Object.FindAnyObjectByType<PlayerController>();
        var cc = p?.GetComponent<CharacterController>();
        if (p != null)
        {
            bool was = cc != null && cc.enabled;
            if (was) cc.enabled = false;
            p.transform.SetPositionAndRotation(new Vector3(44f, 0.85f, -43f), Quaternion.Euler(0f, 0f, 0f));
            if (was) cc.enabled = true;
            p.ResetVelocity();
            Object.FindAnyObjectByType<DhakaCameraRig>()?.SetCameraAngles(28f, 12f);
            p.GetComponent<DhakaAnimationDebugger>()?.SetDebugEnabled(true);
            var av = p.GetComponent<DhakaObstacleAvoider>();
            if (av != null) av.ShowWorldTrajectoryArrows = true;
            p.SetSimulatedInput(true, new Vector2(-0.47f, 0.88f), sprint: true, walk: false, durationSeconds: 5.5f);
        }
        return Sample();
    }

    public static string StageNightMonsoonAndPickup()
    {
        var atm = Object.FindAnyObjectByType<DhakaAtmosphereController>();
        atm?.SetPreset(DhakaAtmosphereController.TimeOfDayPreset.DhakaNight, 2);

        var p = Object.FindAnyObjectByType<PlayerController>();
        var cc = p?.GetComponent<CharacterController>();
        var pickup = Object.FindAnyObjectByType<IntelPickup>();
        if (p != null)
        {
            p.GetComponent<DhakaAnimationDebugger>()?.SetDebugEnabled(false);
            var av = p.GetComponent<DhakaObstacleAvoider>();
            if (av != null) av.ShowWorldTrajectoryArrows = false;

            Vector3 target = pickup != null ? pickup.transform.position - Vector3.forward * 1.4f : new Vector3(0f, 0.02f, 5f);
            bool was = cc != null && cc.enabled;
            if (was) cc.enabled = false;
            p.transform.SetPositionAndRotation(new Vector3(target.x, 0.02f, target.z), Quaternion.identity);
            if (was) cc.enabled = true;
            p.ResetVelocity();
            Object.FindAnyObjectByType<DhakaCameraRig>()?.ResetForRunStart();
            p.SetSimulatedInput(true, new Vector2(0f, 1f), sprint: false, walk: false, durationSeconds: 5.0f);
        }
        return Sample();
    }

    public static string CleanupAndReset()
    {
        var p = Object.FindAnyObjectByType<PlayerController>();
        if (p != null)
        {
            p.SetSimulatedInput(false, Vector2.zero);
            p.GetComponent<DhakaAnimationDebugger>()?.SetDebugEnabled(false);
            var av = p.GetComponent<DhakaObstacleAvoider>();
            if (av != null) av.ShowWorldTrajectoryArrows = false;
        }
        var atm = Object.FindAnyObjectByType<DhakaAtmosphereController>();
        atm?.SetPreset(DhakaAtmosphereController.TimeOfDayPreset.MonsoonDusk, 1);
        return Sample();
    }

    public static string Sample()
    {
        var loop = Object.FindAnyObjectByType<GameLoop>();
        var p = Object.FindAnyObjectByType<PlayerController>();
        var bus = GameObject.Find("Traffic_DhakaCityBus");
        return $"state={loop?.CurrentState} | score={loop?.Score} | intel={loop?.IntelCollected}/{loop?.TargetIntel} | playerPos={p?.transform.position.ToString("F2")} | busPos={(bus != null ? bus.transform.position.ToString("F2") : "NONE")} | frame={Time.frameCount}";
    }
}
