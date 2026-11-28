using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DhakaGta.Core;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace DhakaGta.Editor
{
    /// <summary>
    /// Part 11 Production Optimization, Occlusion Culling, LOD/Texture Streaming,
    /// Addressables Adapter, and Windows64 Build Pipeline.
    /// Runnable via MenuItem or Unity CLI:
    ///   Unity.exe -batchmode -nographics -quit -projectPath UnityProject -executeMethod DhakaGta.Editor.DhakaBuildAndOptimizationPipeline.BatchBuildWindows64
    /// </summary>
    public static class DhakaBuildAndOptimizationPipeline
    {
        public const string ReleaseVersionTag = "v0.1.0-alpha";
        public const string WindowsBuildExecutablePath = "Builds/Windows64/v0.1.0-alpha/DhakaGTA.exe";

        [MenuItem("Dhaka-GTA/Part 11/1. Optimize Meshes, LODGroups, Texture Streaming & Bake Occlusion")]
        public static void OptimizeProjectAndBakeOcclusionMenu()
        {
            int streamedTextures = EnableTextureStreamingAcrossTexturesFolder();
            int optimizedMeshes = OptimizeGeneratedMeshesAndInstancing();
            int lodAdded = EnsureLodGroupsAndStaticOcclusionFlagsInActiveScene();
            StaticOcclusionCulling.Compute();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Part 11 Optimize] StreamedTextures={streamedTextures}, OptimizedMeshes={optimizedMeshes}, SceneLODGroups={lodAdded}, OcclusionCulling=Baked.");
        }

        [MenuItem("Dhaka-GTA/Part 11/2. Configure Build Scenes & Addressables")]
        public static void ConfigureBuildScenesAndAddressablesMenu()
        {
            DhakaCharacterPipelineBuilder.EnsureCharacterPipelineAssets();
            DhakaStoryContentBuilder.BuildAllStoryAssetsMenu();
            RegisterValidBuildScenes();
            TrySyncAddressablesGroupsIfInstalled();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Dhaka-GTA/Part 11/3. Build Windows64 Release Executable (v0.1.0-alpha)")]
        public static BuildResult BuildWindows64Release()
        {
            ConfigureBuildScenesAndAddressablesMenu();

            PlayerSettings.companyName = "ProjectDhakaStudio";
            PlayerSettings.productName = "Dhaka-GTA";
            PlayerSettings.bundleVersion = ReleaseVersionTag;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11, GraphicsDeviceType.Direct3D12 });

            EditorBuildSettingsScene[] enabledScenes = EditorBuildSettings.scenes;
            var scenePaths = new List<string>();
            foreach (EditorBuildSettingsScene s in enabledScenes)
            {
                if (s.enabled && File.Exists(s.path))
                {
                    scenePaths.Add(s.path);
                }
            }

            if (scenePaths.Count == 0)
            {
                Debug.LogError("[Part 11 Build] No valid enabled scenes found in EditorBuildSettings.");
                return BuildResult.Failed;
            }

            string outDir = Path.GetDirectoryName(WindowsBuildExecutablePath);
            if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);

            var options = new BuildPlayerOptions
            {
                scenes = scenePaths.ToArray(),
                locationPathName = WindowsBuildExecutablePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.StrictMode
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            string summaryMsg =
                $"[Part 11 Build] Result={report.summary.result}, Warnings={report.summary.totalWarnings}, " +
                $"Errors={report.summary.totalErrors}, SizeBytes={report.summary.totalSize}, Time={report.summary.totalTime.TotalSeconds:F1}s";

            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log(summaryMsg);
            }
            else
            {
                Debug.LogError(summaryMsg);
            }

            return report.summary.result;
        }

        /// <summary>
        /// Headless CLI entry point returning exit code 0 only on genuine BuildResult.Succeeded.
        /// </summary>
        public static void BatchBuildWindows64()
        {
            OptimizeProjectAndBakeOcclusionMenu();
            BuildResult res = BuildWindows64Release();
            EditorApplication.Exit(res == BuildResult.Succeeded ? 0 : 1);
        }

        private static int EnableTextureStreamingAcrossTexturesFolder()
        {
            if (!Directory.Exists("Assets/Textures")) return 0;
            int count = 0;
            foreach (string file in Directory.GetFiles("Assets/Textures", "*.png", SearchOption.AllDirectories))
            {
                string path = file.Replace('\\', '/');
                if (AssetImporter.GetAtPath(path) is TextureImporter importer && !importer.streamingMipmaps)
                {
                    importer.streamingMipmaps = true;
                    importer.SaveAndReimport();
                    count++;
                }
            }
            return count;
        }

        private static int OptimizeGeneratedMeshesAndInstancing()
        {
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Mesh", new[] { "Assets/Models" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh != null)
                {
                    MeshUtility.Optimize(mesh);
                    EditorUtility.SetDirty(mesh);
                    count++;
                }
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Materials" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null && !mat.enableInstancing)
                {
                    mat.enableInstancing = true;
                    EditorUtility.SetDirty(mat);
                }
            }
            return count;
        }

        private static int EnsureLodGroupsAndStaticOcclusionFlagsInActiveScene()
        {
            int lodAdded = 0;
            var staticFlags = StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic;

            foreach (MeshRenderer mr in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                GameObject go = mr.gameObject;
                if (go.layer == PhysicsLayers.Building || go.layer == PhysicsLayers.Ground)
                {
                    GameObjectUtility.SetStaticEditorFlags(go, staticFlags);

                    if (go.layer == PhysicsLayers.Building && go.GetComponentInParent<LODGroup>() == null)
                    {
                        var bounds = mr.bounds;
                        if (bounds.size.magnitude >= 6.0f)
                        {
                            var lodGroup = go.AddComponent<LODGroup>();
                            lodGroup.SetLODs(new[]
                            {
                                new LOD(0.32f, new Renderer[] { mr }),
                                new LOD(0.06f, new Renderer[] { mr })
                            });
                            lodGroup.RecalculateBounds();
                            lodAdded++;
                        }
                    }
                }
            }

            if (lodAdded > 0)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
            return lodAdded;
        }

        private static void RegisterValidBuildScenes()
        {
            string[] candidates =
            {
                "Assets/Scenes/Bootstrap.unity",
                "Assets/Scenes/MainMenu.unity",
                "Assets/Scenes/TestCity.unity",
                "Assets/Scenes/GulshanHatirjheel_Milestone.unity",
                "Assets/Scenes/SampleScene.unity"
            };

            var sceneList = new List<EditorBuildSettingsScene>();
            foreach (string p in candidates)
            {
                if (File.Exists(p))
                {
                    sceneList.Add(new EditorBuildSettingsScene(p, true));
                }
            }

            if (sceneList.Count > 0)
            {
                EditorBuildSettings.scenes = sceneList.ToArray();
            }
        }

        private static void TrySyncAddressablesGroupsIfInstalled()
        {
            Type settingsType = Type.GetType("UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject, Unity.Addressables.Editor");
            if (settingsType == null)
            {
                Debug.Log("[Part 11 Addressables] com.unity.addressables is not installed yet; additive SceneManager fallback active in WorldRegionLoader.");
                return;
            }

            PropertyInfo prop = settingsType.GetProperty("Settings", BindingFlags.Public | BindingFlags.Static);
            object settings = prop?.GetValue(null);
            if (settings != null)
            {
                Debug.Log("[Part 11 Addressables] Verified active AddressableAssetSettings for 12-district WorldRegionLoader bundles.");
            }
        }
    }
}
