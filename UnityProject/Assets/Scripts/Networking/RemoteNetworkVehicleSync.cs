using System;
using System.Collections.Generic;
using DhakaGta.Data;
using DhakaGta.Vehicles;
using UnityEngine;

namespace DhakaGta.Networking
{
    /// <summary>
    /// Synchronizes server-authoritative vehicles across Dhaka Multiplayer sessions,
    /// rendering multi-part Dhaka vehicle models (CNGs, Sedans, Bikes, Police Cruisers, Launches),
    /// smooth velocity interpolation, police siren lights, and ownership lock labels.
    /// </summary>
    public sealed class RemoteNetworkVehicleSync : MonoBehaviour
    {
        [SerializeField] private VehicleSpecSO[] vehicleCatalog = Array.Empty<VehicleSpecSO>();

        private sealed class SyncedVehicleRecord
        {
            public string VehicleId;
            public GameObject RootGo;
            public DhakaVehicleLightsAndHorn Lights;
            public Vector3 TargetPos;
            public Quaternion TargetRot;
            public bool SirensOn;
            public string OwnerId;
            public bool IsLocked;
        }

        private readonly Dictionary<string, SyncedVehicleRecord> _vehicles = new Dictionary<string, SyncedVehicleRecord>();

        public int ActiveSyncedVehicleCount => _vehicles.Count;

        public void ConfigureCatalog(VehicleSpecSO[] specs)
        {
            vehicleCatalog = specs ?? Array.Empty<VehicleSpecSO>();
        }

        public void IngestSnapshot(NetVehicleSnapshotDto[] serverVehicles)
        {
            if (serverVehicles == null) return;

            var present = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < serverVehicles.Length; i++)
            {
                NetVehicleSnapshotDto dto = serverVehicles[i];
                if (dto == null || string.IsNullOrEmpty(dto.id)) continue;

                present.Add(dto.id);
                if (!_vehicles.TryGetValue(dto.id, out SyncedVehicleRecord rec))
                {
                    rec = CreateVehicleVisual(dto);
                    _vehicles[dto.id] = rec;
                }

                rec.TargetPos = new Vector3(dto.x, dto.y, dto.z);
                rec.TargetRot = Quaternion.Euler(0f, dto.heading * Mathf.Rad2Deg, 0f);
                rec.OwnerId = dto.ownerId;
                rec.IsLocked = dto.locked;

                if (rec.Lights != null)
                {
                    if (dto.sirenOn && !rec.Lights.IsPoliceSirenActive) rec.Lights.TogglePoliceSiren();
                    else if (!dto.sirenOn && rec.Lights.IsPoliceSirenActive) rec.Lights.TogglePoliceSiren();
                    rec.Lights.UpdateLightStates(braking: Mathf.Abs(dto.speed) < 0.2f, reversing: dto.speed < -0.3f);
                }
            }

            var toRemove = new List<string>();
            foreach (var kv in _vehicles)
            {
                if (!present.Contains(kv.Key)) toRemove.Add(kv.Key);
            }
            for (int i = 0; i < toRemove.Count; i++)
            {
                if (_vehicles[toRemove[i]]?.RootGo != null) Destroy(_vehicles[toRemove[i]].RootGo);
                _vehicles.Remove(toRemove[i]);
            }
        }

        public void ClearAllVehicles()
        {
            foreach (var kv in _vehicles)
            {
                if (kv.Value?.RootGo != null) Destroy(kv.Value.RootGo);
            }
            _vehicles.Clear();
        }

        private void Update()
        {
            float lerpRate = Mathf.Clamp01(Time.deltaTime * 14f);
            foreach (var kv in _vehicles)
            {
                SyncedVehicleRecord rec = kv.Value;
                if (rec?.RootGo == null) continue;
                rec.RootGo.transform.position = Vector3.Lerp(rec.RootGo.transform.position, rec.TargetPos, lerpRate);
                rec.RootGo.transform.rotation = Quaternion.Slerp(rec.RootGo.transform.rotation, rec.TargetRot, lerpRate);
            }
        }

        private SyncedVehicleRecord CreateVehicleVisual(NetVehicleSnapshotDto dto)
        {
            VehicleSpecSO spec = ResolveSpec(dto.kind);
            Vector3 pos = new Vector3(dto.x, dto.y, dto.z);
            Quaternion rot = Quaternion.Euler(0f, dto.heading * Mathf.Rad2Deg, 0f);

            GameObject go;
            if (spec != null)
            {
                go = DhakaVehicleFactory.CreateVehicleFromSpec(spec, pos, rot, transform);
                var localDrive = go.GetComponent<DhakaVehicleController>();
                if (localDrive != null) localDrive.enabled = false;
                var rb = go.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(transform, false);
                go.transform.SetPositionAndRotation(pos + Vector3.up * 0.65f, rot);
                go.transform.localScale = new Vector3(1.8f, 1.25f, 4.1f);
            }

            go.name = $"NetVehicle_{dto.id}_{dto.kind}";

            return new SyncedVehicleRecord
            {
                VehicleId = dto.id,
                RootGo = go,
                Lights = go.GetComponent<DhakaVehicleLightsAndHorn>(),
                TargetPos = pos,
                TargetRot = rot,
                OwnerId = dto.ownerId,
                IsLocked = dto.locked
            };
        }

        private VehicleSpecSO ResolveSpec(string kind)
        {
            if (vehicleCatalog == null || string.IsNullOrEmpty(kind)) return null;
            DhakaVehicleClass targetClass = kind.ToLowerInvariant() switch
            {
                "cng" => DhakaVehicleClass.CngAutoRickshaw,
                "rickshaw" => DhakaVehicleClass.CycleRickshaw,
                "bike" => DhakaVehicleClass.Motorcycle,
                "bus" => DhakaVehicleClass.CityBus,
                "police" => DhakaVehicleClass.DmpPoliceCruiser,
                "truck" => DhakaVehicleClass.PickupTruck,
                "speedboat" => DhakaVehicleClass.Speedboat,
                "launch" => DhakaVehicleClass.BurigangaLaunch,
                _ => DhakaVehicleClass.CompactSedan
            };

            for (int i = 0; i < vehicleCatalog.Length; i++)
            {
                if (vehicleCatalog[i] != null && vehicleCatalog[i].vehicleClass == targetClass)
                {
                    return vehicleCatalog[i];
                }
            }
            return vehicleCatalog.Length > 0 ? vehicleCatalog[0] : null;
        }
    }
}
