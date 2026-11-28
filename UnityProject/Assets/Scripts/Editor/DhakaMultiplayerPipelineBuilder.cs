using DhakaGta.Character;
using DhakaGta.Combat;
using DhakaGta.Data;
using DhakaGta.Missions;
using DhakaGta.Networking;
using DhakaGta.UI;
using UnityEditor;
using UnityEngine;

namespace DhakaGta.Editor
{
    /// <summary>
    /// Wires Unity WebSocket Multiplayer Client, Remote Character Avatars (Tariq/Maya),
    /// Remote Vehicle Sync, and [M] Multiplayer Lobby/Chat HUD into scenes.
    /// </summary>
    public static class DhakaMultiplayerPipelineBuilder
    {
        [MenuItem("Dhaka-GTA/Part 10/Attach Multiplayer Client Stack to Active Scene")]
        public static void AttachToActiveSceneMenu()
        {
            GameObject host = GameObject.Find("GulshanSystems") ?? new GameObject("DhakaMultiplayerSystems");
            ProtagonistController protagonist = Object.FindFirstObjectByType<ProtagonistController>();
            HealthComponent hp = protagonist != null ? protagonist.GetComponent<HealthComponent>() : null;
            PlayerEconomyAndInventory econ = Object.FindFirstObjectByType<PlayerEconomyAndInventory>();
            DhakaMissionEngine missions = Object.FindFirstObjectByType<DhakaMissionEngine>();
            VehicleSpecSO[] specs = DhakaVehicleCatalogBuilder.BuildAllSpecsAndPrefabs();

            AttachMultiplayerSystems(host, null, protagonist, hp, econ, missions, specs);
            Debug.Log("[DhakaMultiplayerPipelineBuilder] Attached WebSocket Multiplayer client, remote avatar/vehicle sync, and [M] Lobby UI.");
        }

        public static DhakaMultiplayerSessionManager AttachMultiplayerSystems(
            GameObject systemsGo,
            Transform worldRoot,
            ProtagonistController localProtagonist,
            HealthComponent localHealth,
            PlayerEconomyAndInventory localEconomy,
            DhakaMissionEngine localMissions,
            VehicleSpecSO[] vehicleSpecs)
        {
            Transform netParent = worldRoot != null ? worldRoot : systemsGo.transform;

            var remoteRoot = new GameObject("DhakaMultiplayer_RemoteEntities");
            remoteRoot.transform.SetParent(netParent, false);

            GameObject tariqPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DhakaCharacterPipelineBuilder.TariqPrefabPath);
            GameObject mayaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DhakaCharacterPipelineBuilder.MayaPrefabPath);

            var remoteAvatars = remoteRoot.AddComponent<RemoteNetworkAvatarController>();
            remoteAvatars.ConfigurePrefabs(tariqPrefab, mayaPrefab);

            var remoteVehicles = remoteRoot.AddComponent<RemoteNetworkVehicleSync>();
            remoteVehicles.ConfigureCatalog(vehicleSpecs);

            if (systemsGo.GetComponent<DhakaWebSocketTransport>() == null) systemsGo.AddComponent<DhakaWebSocketTransport>();
            var sessionMgr = systemsGo.GetComponent<DhakaMultiplayerSessionManager>()
                             ?? systemsGo.AddComponent<DhakaMultiplayerSessionManager>();
            sessionMgr.BindSceneReferences(localProtagonist, localHealth, localEconomy, localMissions, remoteAvatars, remoteVehicles);

            var mpHud = systemsGo.GetComponent<DhakaMultiplayerHudController>() ?? systemsGo.AddComponent<DhakaMultiplayerHudController>();
            mpHud.ConfigureAndBuildUi(sessionMgr);

            EditorUtility.SetDirty(sessionMgr);
            return sessionMgr;
        }
    }
}
