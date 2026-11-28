using System.Collections.Generic;
using DhakaGta.Core;
using UnityEngine;

namespace DhakaGta.TrafficAI
{
    /// <summary>
    /// Coordinates Dhaka Metropolitan Police (DMP) vehicle and foot constable response to
    /// CityDangerBus hazards and player/NPC crime heat (1-5 stars), activating sirens and vectoring interceptors.
    /// </summary>
    public sealed class DhakaPoliceDispatchController : MonoBehaviour
    {
        [SerializeField] private float alertRadiusMeters = 180f;

        private DhakaCityAiSimulationManager _simManager;
        private readonly List<DhakaAiVehicleAgent> _respondingCruisers = new List<DhakaAiVehicleAgent>();

        public int ActiveRespondingCruiserCount => _respondingCruisers.Count;
        public bool IsPoliceAlertActive { get; private set; }
        public Vector3 LastDispatchTarget { get; private set; }

        public void BindSimulationManager(DhakaCityAiSimulationManager simManager)
        {
            _simManager = simManager;
        }

        private void OnEnable()
        {
            CityDangerBus.OnDangerReported += HandleDangerReported;
            CityDangerBus.OnWantedLevelChanged += HandleWantedChanged;
        }

        private void OnDisable()
        {
            CityDangerBus.OnDangerReported -= HandleDangerReported;
            CityDangerBus.OnWantedLevelChanged -= HandleWantedChanged;
        }

        private void HandleWantedChanged(int newStars, Vector3 crimePos)
        {
            if (newStars > 0)
            {
                DispatchUnitsTo(crimePos, Mathf.Clamp(newStars, 1, 4));
            }
            else
            {
                StandDownAllUnits();
            }
        }

        private void HandleDangerReported(CityDangerEvent evt)
        {
            if (evt.Kind == CityDangerKind.Gunshot ||
                evt.Kind == CityDangerKind.Explosion ||
                evt.Kind == CityDangerKind.Assault ||
                (evt.Kind == CityDangerKind.VehicleCollision && evt.Severity >= 0.85f))
            {
                int desiredUnits = Mathf.Clamp(1 + CityDangerBus.CurrentWantedLevel, 1, 3);
                DispatchUnitsTo(evt.WorldPosition, desiredUnits);
            }
        }

        public void DispatchUnitsTo(Vector3 targetWorldPos, int desiredCruisers)
        {
            IsPoliceAlertActive = true;
            LastDispatchTarget = targetWorldPos;
            _respondingCruisers.Clear();

            if (_simManager == null)
            {
                return;
            }

            IReadOnlyList<DhakaAiVehicleAgent> vehicles = _simManager.ActiveVehicles;
            int assigned = 0;

            for (int i = 0; i < vehicles.Count && assigned < desiredCruisers; i++)
            {
                DhakaAiVehicleAgent v = vehicles[i];
                if (v == null || !v.gameObject.activeInHierarchy || !v.IsPoliceVehicle || v.IsParked || v.IsAssignedToPlayer)
                {
                    continue;
                }

                if (Vector3.Distance(v.transform.position, targetWorldPos) <= alertRadiusMeters * 1.5f)
                {
                    v.EngagePoliceDispatch(targetWorldPos);
                    _respondingCruisers.Add(v);
                    assigned++;
                }
            }

            // If not enough nearby patrol cruisers exist in active pool, summon interceptor onto incoming spline
            while (assigned < desiredCruisers)
            {
                DhakaAiVehicleAgent spawned = _simManager.TryDispatchEmergencyPoliceCruiserNear(targetWorldPos);
                if (spawned == null)
                {
                    break;
                }
                spawned.EngagePoliceDispatch(targetWorldPos);
                _respondingCruisers.Add(spawned);
                assigned++;
            }

            // Alert foot constables nearby
            IReadOnlyList<DhakaPedestrianAgent> peds = _simManager.ActivePedestrians;
            for (int i = 0; i < peds.Count; i++)
            {
                DhakaPedestrianAgent p = peds[i];
                if (p != null && p.gameObject.activeInHierarchy && p.IsPoliceOfficer)
                {
                    p.ReactToDanger(targetWorldPos, alertRadiusMeters);
                }
            }
        }

        public void StandDownAllUnits()
        {
            IsPoliceAlertActive = false;
            for (int i = _respondingCruisers.Count - 1; i >= 0; i--)
            {
                if (_respondingCruisers[i] != null)
                {
                    _respondingCruisers[i].ClearPoliceDispatch();
                }
            }
            _respondingCruisers.Clear();
        }
    }
}
