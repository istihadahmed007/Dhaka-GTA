using DhakaGta.Data;
using DhakaGta.TrafficAI;
using DhakaGta.WorldGen;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DhakaGta.Editor
{
    /// <summary>
    /// Editor integration wiring Living Dhaka Traffic, Bangladeshi Pedestrians, NavMesh,
    /// Traffic Light Controllers, and DMP Police Dispatch cleanly into scenes.
    /// </summary>
    public static class DhakaTrafficAiPipelineBuilder
    {
        [MenuItem("Dhaka-GTA/Part 8/Attach & Bake Living Traffic AI in Active Scene")]
        public static void AttachToActiveSceneMenu()
        {
            RoadSplineSystem splines = Object.FindFirstObjectByType<RoadSplineSystem>();
            IntersectionGenerator intersections = Object.FindFirstObjectByType<IntersectionGenerator>();
            if (splines == null)
            {
                Debug.LogWarning("[DhakaTrafficAiPipelineBuilder] No RoadSplineSystem found in active scene.");
                return;
            }

            VehicleSpecSO[] specs = DhakaVehicleCatalogBuilder.BuildAllSpecsAndPrefabs();
            Transform focus = GameObject.Find("ActiveCameraFollowProxy")?.transform
                              ?? Object.FindFirstObjectByType<Camera>()?.transform
                              ?? splines.transform;

            GameObject systemsRoot = GameObject.Find("GulshanSystems") ?? new GameObject("DhakaAiSimulationSystems");
            AttachLivingCitySimulation(systemsRoot, splines, intersections, focus, specs, "gulshan", warmImmediatelyInScene: true);
            Debug.Log("[DhakaTrafficAiPipelineBuilder] Attached Living Traffic, Bangladeshi Pedestrians, NavMesh & Police AI to active scene.");
        }

        public static DhakaCityAiSimulationManager AttachLivingCitySimulation(
            GameObject hostSystemsGo,
            RoadSplineSystem splines,
            IntersectionGenerator intersections,
            Transform focusTarget,
            VehicleSpecSO[] vehicleCatalog,
            string initialDistrictId,
            bool warmImmediatelyInScene = false)
        {
            RuntimeAnimatorController humanoidCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                DhakaCharacterPipelineBuilder.ControllerPath);

            var aiManager = hostSystemsGo.GetComponent<DhakaCityAiSimulationManager>()
                            ?? hostSystemsGo.AddComponent<DhakaCityAiSimulationManager>();

            aiManager.BindSceneSetup(splines, intersections, focusTarget, vehicleCatalog, initialDistrictId, humanoidCtrl);

            if (warmImmediatelyInScene)
            {
                aiManager.ConfigureAndWarmPools(
                    splines,
                    intersections,
                    focusTarget,
                    vehicleCatalog,
                    initialDistrictId,
                    humanoidCtrl,
                    bakeNavMesh: true);
            }

            EditorUtility.SetDirty(aiManager);
            return aiManager;
        }
    }
}
