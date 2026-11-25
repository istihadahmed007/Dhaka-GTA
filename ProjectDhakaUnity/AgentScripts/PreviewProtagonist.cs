using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class PreviewProtagonist
{
    public static string Capture()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Key light + cool sky bounce matching realistic overcast/late-afternoon Dhaka lighting
        var sunGo = new GameObject("SunLight");
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1.0f, 0.94f, 0.85f);
        sun.intensity = 1.65f;
        sun.shadows = LightShadows.Soft;
        sunGo.transform.rotation = Quaternion.Euler(38f, 145f, 0f);

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.45f, 0.56f, 0.72f);
        RenderSettings.ambientEquatorColor = new Color(0.42f, 0.44f, 0.48f);
        RenderSettings.ambientGroundColor = new Color(0.18f, 0.16f, 0.15f);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "PreviewFloor";
        ground.transform.localScale = new Vector3(4f, 1f, 4f);
        var floorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        floorMat.SetColor("_BaseColor", new Color(0.24f, 0.26f, 0.29f));
        floorMat.SetFloat("_Smoothness", 0.35f);
        ground.GetComponent<Renderer>().sharedMaterial = floorMat;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/DhakaProtagonist.prefab");
        var charGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        charGo.transform.position = Vector3.zero;

        // Pose arms naturally down using HumanPoseHandler just for preview readability (not T-pose only)
        var anim = charGo.GetComponent<Animator>();
        var handler = new HumanPoseHandler(anim.avatar, charGo.transform);
        var pose = new HumanPose();
        handler.GetHumanPose(ref pose);
        int la = System.Array.IndexOf(HumanTrait.MuscleName, "Left Arm Down-Up");
        int ra = System.Array.IndexOf(HumanTrait.MuscleName, "Right Arm Down-Up");
        int lfa = System.Array.IndexOf(HumanTrait.MuscleName, "Left Forearm Stretch");
        int rfa = System.Array.IndexOf(HumanTrait.MuscleName, "Right Forearm Stretch");
        pose.muscles[la] = -0.68f;
        pose.muscles[ra] = -0.68f;
        pose.muscles[lfa] = 0.65f;
        pose.muscles[rfa] = 0.65f;
        handler.SetHumanPose(ref pose);

        var camGo = new GameObject("PreviewCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.18f, 0.22f, 0.28f);
        cam.nearClipPlane = 0.05f;
        var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;

        // 1. 3/4 Full Body shot
        camGo.transform.position = new Vector3(1.15f, 1.25f, 2.15f);
        camGo.transform.LookAt(new Vector3(0f, 0.95f, 0f));
        RenderToPng(cam, 1280, 720, "Screenshots/char_preview_34.png");

        // 2. Face & Upper Jacket close-up
        camGo.transform.position = new Vector3(0.24f, 1.66f, 0.72f);
        camGo.transform.LookAt(new Vector3(0f, 1.63f, 0.02f));
        RenderToPng(cam, 1280, 720, "Screenshots/char_preview_face.png");

        // Re-open SampleScene cleanly without saving temp preview scene
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        return "Captured Screenshots/char_preview_34.png and Screenshots/char_preview_face.png";
    }

    static void RenderToPng(Camera cam, int w, int h, string relPath)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(relPath)));
        System.IO.File.WriteAllBytes(System.IO.Path.GetFullPath(relPath), tex.EncodeToPNG());
        cam.targetTexture = null;
        RenderTexture.active = null;
        UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(tex);
    }
}
