using System.Collections.Generic;
using DhakaGta.Data;
using UnityEngine;

namespace DhakaGta.Missions
{
    /// <summary>
    /// Holds authored MissionDefinitionSO catalog and active mission state for Phase 5.
    /// </summary>
    public sealed class MissionRegistry : MonoBehaviour
    {
        [SerializeField] private MissionDefinitionSO[] missions = System.Array.Empty<MissionDefinitionSO>();

        public IReadOnlyList<MissionDefinitionSO> Missions => missions;
        public MissionDefinitionSO ActiveMission => _missionEngine != null && _missionEngine.ActiveMission != null ? _missionEngine.ActiveMission : _legacyActiveMission;
        public int CompletedCount => _missionEngine != null ? _missionEngine.CompletedMissionIds.Count : _legacyCompletedCount;

        private DhakaMissionEngine _missionEngine;
        private MissionDefinitionSO _legacyActiveMission;
        private int _legacyCompletedCount;

        private void Awake()
        {
            _missionEngine = GetComponent<DhakaMissionEngine>();
        }

        public void SetMissions(MissionDefinitionSO[] missionAssets)
        {
            missions = missionAssets ?? System.Array.Empty<MissionDefinitionSO>();
            _missionEngine ??= GetComponent<DhakaMissionEngine>();
        }

        public bool StartMission(string missionId)
        {
            if (_missionEngine != null && _missionEngine.StartMission(missionId))
            {
                return true;
            }

            if (missions == null)
            {
                return false;
            }

            foreach (MissionDefinitionSO mission in missions)
            {
                if (mission != null && mission.missionId == missionId)
                {
                    _legacyActiveMission = mission;
                    return true;
                }
            }

            return false;
        }
    }
}
