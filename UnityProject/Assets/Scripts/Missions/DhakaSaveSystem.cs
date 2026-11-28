using System;
using System.Collections.Generic;
using System.IO;
using DhakaGta.Character;
using DhakaGta.Combat;
using DhakaGta.Core;
using DhakaGta.Vehicles;
using UnityEngine;

namespace DhakaGta.Missions
{
    [Serializable]
    public sealed class DhakaFullSavePayload
    {
        public int saveVersion = 1;
        public string savedUtcTimestamp;
        public string activeProtagonistId = "tariq";
        public Vector3 playerWorldPos;
        public float playerYawDeg;
        public float playerHealth = 135f;
        public int walletBdt = 2500;
        public int totalEarnedBdt;
        public int totalSpentBdt;
        public int tariqMayaBondXp = 10;
        public int faresCompleted;
        public int deliveriesCompleted;
        public int contractsCompleted;
        public int racesWon;
        public float timeOfDayHours = 17.2f;
        public List<InventorySlotRecord> inventory = new List<InventorySlotRecord>();
        public List<string> unlockedWeapons = new List<string>();
        public List<string> unlockedSafehouses = new List<string>();
        public List<string> unlockedOutfits = new List<string>();
        public List<string> completedMissionIds = new List<string>();
        public string activeMissionId;
        public int activeMissionStepIndex;
        public string garageJson;
    }

    /// <summary>
    /// Versioned persistent Save/Load manager serializing protagonist selection, coordinates, stats,
    /// BDT economy, Tariq-Maya relationship bond, inventory, safehouses, garage fleet, and mission progression.
    /// </summary>
    public sealed class DhakaSaveSystem : MonoBehaviour
    {
        public const string DefaultSaveFileName = "dhaka_gta_save_v1.json";

        [SerializeField] private ProtagonistController protagonistController;
        [SerializeField] private ProtagonistSwitcher protagonistSwitcher;
        [SerializeField] private HealthComponent playerHealth;
        [SerializeField] private PlayerEconomyAndInventory economy;
        [SerializeField] private DhakaGarageManager garageManager;
        [SerializeField] private DhakaMissionEngine missionEngine;

        public string SaveFilePath => Path.Combine(Application.persistentDataPath, DefaultSaveFileName);
        public string LastSavedJson { get; private set; }

        public void BindDependencies(
            ProtagonistController ctrl,
            ProtagonistSwitcher switcher,
            HealthComponent hp,
            PlayerEconomyAndInventory econ,
            DhakaGarageManager garage,
            DhakaMissionEngine missions)
        {
            protagonistController = ctrl;
            protagonistSwitcher = switcher;
            playerHealth = hp;
            economy = econ;
            garageManager = garage;
            missionEngine = missions;
        }

        public DhakaFullSavePayload CaptureSnapshot()
        {
            var payload = new DhakaFullSavePayload
            {
                saveVersion = 1,
                savedUtcTimestamp = DateTime.UtcNow.ToString("o"),
                activeProtagonistId = protagonistController != null && protagonistController.Profile != null
                    ? protagonistController.Profile.protagonistId
                    : "tariq",
                playerWorldPos = protagonistController != null ? protagonistController.transform.position : Vector3.zero,
                playerYawDeg = protagonistController != null ? protagonistController.transform.eulerAngles.y : 0f,
                playerHealth = playerHealth != null ? playerHealth.CurrentHealth : 120f,
                timeOfDayHours = WorldSurfaceState.CurrentTimeOfDayHours
            };

            if (economy != null)
            {
                payload.walletBdt = economy.CurrentBdt;
                payload.totalEarnedBdt = economy.TotalEarnedBdt;
                payload.totalSpentBdt = economy.TotalSpentBdt;
                payload.tariqMayaBondXp = economy.TariqMayaBondXp;
                payload.faresCompleted = economy.FaresCompletedCount;
                payload.deliveriesCompleted = economy.DeliveriesCompletedCount;
                payload.contractsCompleted = economy.ContractsCompletedCount;
                payload.racesWon = economy.RacesWonCount;
                payload.inventory = new List<InventorySlotRecord>(economy.Items);
                payload.unlockedWeapons = new List<string>(economy.UnlockedWeapons);
                payload.unlockedSafehouses = new List<string>(economy.UnlockedSafehouses);
                payload.unlockedOutfits = new List<string>(economy.UnlockedOutfits);
            }

            if (garageManager != null)
            {
                payload.garageJson = garageManager.SerializeToJson();
            }

            if (missionEngine != null)
            {
                payload.completedMissionIds = new List<string>(missionEngine.CompletedMissionIds);
                if (missionEngine.ActiveMission != null)
                {
                    payload.activeMissionId = missionEngine.ActiveMission.missionId;
                    payload.activeMissionStepIndex = missionEngine.CurrentStepIndex;
                }
            }

            return payload;
        }

        public string SerializeToJson()
        {
            DhakaFullSavePayload payload = CaptureSnapshot();
            LastSavedJson = JsonUtility.ToJson(payload, true);
            return LastSavedJson;
        }

        public bool SaveToDisk()
        {
            try
            {
                string json = SerializeToJson();
                string dir = Path.GetDirectoryName(SaveFilePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(SaveFilePath, json);
                MissionObjectiveEvents.RaiseSaveStatus("Game Saved (সেভ সফল)");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DhakaSaveSystem] Save failed: {ex.Message}");
                return false;
            }
        }

        public bool LoadFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            DhakaFullSavePayload payload = JsonUtility.FromJson<DhakaFullSavePayload>(json);
            if (payload == null || payload.saveVersion <= 0) return false;

            if (protagonistSwitcher != null && !string.IsNullOrEmpty(payload.activeProtagonistId))
            {
                protagonistSwitcher.SelectById(payload.activeProtagonistId);
            }

            if (protagonistController != null)
            {
                var cc = protagonistController.GetComponent<CharacterController>();
                bool prevCc = cc != null && cc.enabled;
                if (cc != null) cc.enabled = false;
                protagonistController.transform.SetPositionAndRotation(
                    payload.playerWorldPos,
                    Quaternion.Euler(0f, payload.playerYawDeg, 0f));
                if (cc != null) cc.enabled = prevCc;
            }

            WorldSurfaceState.CurrentTimeOfDayHours = payload.timeOfDayHours;

            if (economy != null)
            {
                economy.ApplyLoadedState(
                    payload.walletBdt,
                    payload.totalEarnedBdt,
                    payload.totalSpentBdt,
                    payload.tariqMayaBondXp,
                    payload.faresCompleted,
                    payload.deliveriesCompleted,
                    payload.contractsCompleted,
                    payload.racesWon,
                    payload.inventory,
                    payload.unlockedWeapons,
                    payload.unlockedSafehouses,
                    payload.unlockedOutfits);
            }

            if (garageManager != null && !string.IsNullOrWhiteSpace(payload.garageJson))
            {
                garageManager.DeserializeFromJson(payload.garageJson);
            }

            if (missionEngine != null)
            {
                missionEngine.RestoreProgressionState(
                    payload.completedMissionIds,
                    payload.activeMissionId,
                    payload.activeMissionStepIndex);
            }

            MissionObjectiveEvents.RaiseSaveStatus("Save Restored (লোড সফল)");
            return true;
        }

        public bool LoadFromDisk()
        {
            if (!File.Exists(SaveFilePath)) return false;
            string json = File.ReadAllText(SaveFilePath);
            return LoadFromJson(json);
        }
    }
}
