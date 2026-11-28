using System;
using DhakaGta.Data;

namespace DhakaGta.Missions
{
    [Serializable]
    public struct BilingualDialogueLine
    {
        public string Speaker;
        public string TextBangla;
        public string TextEnglish;
        public float DisplaySeconds;
    }

    [Serializable]
    public struct MissionProgressSnapshot
    {
        public string MissionId;
        public string TitleEnglish;
        public string TitleBangla;
        public MissionCategory Category;
        public int CurrentStepIndex;
        public int TotalSteps;
        public string StepPrompt;
        public int ProgressCount;
        public int RequiredCount;
        public float RemainingTimerSeconds;
        public bool HasTargetPoint;
        public UnityEngine.Vector3 TargetWorldPosition;
        public bool IsCompleted;
        public bool IsFailed;
    }

    public static class MissionObjectiveEvents
    {
        public static event Action<int> OnWalletBdtChanged;
        public static event Action OnInventoryChanged;
        public static event Action<int, string> OnTariqMayaBondChanged;
        public static event Action<string> OnSafehouseUnlocked;
        public static event Action<MissionProgressSnapshot> OnMissionUpdated;
        public static event Action<MissionDefinitionSO> OnMissionCompleted;
        public static event Action<MissionDefinitionSO, string> OnMissionFailed;
        public static event Action<BilingualDialogueLine> OnDialogueShown;
        public static event Action<string> OnSaveStatusMessage;

        public static void RaiseWalletChanged(int newBdt) => OnWalletBdtChanged?.Invoke(newBdt);
        public static void RaiseInventoryChanged() => OnInventoryChanged?.Invoke();
        public static void RaiseBondChanged(int bondXp, string label) => OnTariqMayaBondChanged?.Invoke(bondXp, label);
        public static void RaiseSafehouseUnlocked(string id) => OnSafehouseUnlocked?.Invoke(id);
        public static void RaiseMissionUpdated(MissionProgressSnapshot snap) => OnMissionUpdated?.Invoke(snap);
        public static void RaiseMissionCompleted(MissionDefinitionSO mission) => OnMissionCompleted?.Invoke(mission);
        public static void RaiseMissionFailed(MissionDefinitionSO mission, string reason) => OnMissionFailed?.Invoke(mission, reason);
        public static void RaiseDialogue(BilingualDialogueLine line) => OnDialogueShown?.Invoke(line);
        public static void RaiseSaveStatus(string msg) => OnSaveStatusMessage?.Invoke(msg);
    }
}
