using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectDhaka;

public static class InspectSampleScene
{
    public static string Dump()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        var sb = new StringBuilder();
        foreach (var root in scene.GetRootGameObjects())
        {
            sb.AppendLine($"ROOT: {root.name} pos={root.transform.position.ToString("F1")} children={root.transform.childCount}");
            foreach (var c in root.GetComponents<Component>())
            {
                if (c != null && !(c is Transform)) sb.AppendLine($"   [{c.GetType().Name}]");
            }
        }
        var p = Object.FindAnyObjectByType<PlayerController>();
        if (p != null) sb.AppendLine($"PLAYER: {p.gameObject.name} pos={p.transform.position.ToString("F2")} children={p.transform.childCount}");
        var rig = Object.FindAnyObjectByType<DhakaCameraRig>();
        if (rig != null) sb.AppendLine($"CAMRIG: {rig.gameObject.name} dist={rig.ConfiguredCameraDistance}");
        var pickups = Object.FindObjectsByType<IntelPickup>(FindObjectsSortMode.None);
        sb.AppendLine($"PICKUPS: {pickups.Length}");
        var patrols = Object.FindObjectsByType<PatrolHazard>(FindObjectsSortMode.None);
        sb.AppendLine($"PATROLS: {patrols.Length}");
        return sb.ToString();
    }
}
