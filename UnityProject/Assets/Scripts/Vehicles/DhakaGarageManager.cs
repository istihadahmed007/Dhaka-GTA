using System;
using System.Collections.Generic;
using System.IO;
using DhakaGta.Data;
using UnityEngine;

namespace DhakaGta.Vehicles
{
    [Serializable]
    public sealed class OwnedVehicleRecord
    {
        public string vehicleId;
        public string licensePlate;
        public Color customPaintColor;
        public int engineStage = 1;
        public TireCompoundType tireCompound = TireCompoundType.StreetAllSeason;
        public float savedHealth = 1000f;
    }

    [Serializable]
    public sealed class GarageSavePayload
    {
        public List<OwnedVehicleRecord> ownedVehicles = new List<OwnedVehicleRecord>();
    }

    /// <summary>
    /// Manages player vehicle ownership, license plate registration, garage storage/retrieval,
    /// full repair services, and persistent custom paint + tuning upgrades across play sessions.
    /// </summary>
    public sealed class DhakaGarageManager : MonoBehaviour
    {
        [SerializeField] private VehicleSpecSO[] catalog = System.Array.Empty<VehicleSpecSO>();
        [SerializeField] private Transform spawnBayAnchor;

        private readonly List<OwnedVehicleRecord> _ownedFleet = new List<OwnedVehicleRecord>();

        public IReadOnlyList<OwnedVehicleRecord> OwnedFleet => _ownedFleet;

        public void ConfigureCatalog(VehicleSpecSO[] specs, Transform bayAnchor)
        {
            catalog = specs ?? System.Array.Empty<VehicleSpecSO>();
            spawnBayAnchor = bayAnchor;
        }

        public OwnedVehicleRecord GrantVehicleToGarage(string vehicleId, Color? customPaint = null)
        {
            VehicleSpecSO spec = FindSpec(vehicleId);
            Color paint = customPaint ?? (spec != null && spec.typicalLiveries != null && spec.typicalLiveries.Length > 0
                ? spec.typicalLiveries[0]
                : Color.red);

            var entry = new OwnedVehicleRecord
            {
                vehicleId = vehicleId,
                licensePlate = $"DHAKA-METRO-GA-{11 + _ownedFleet.Count * 7:00}-{1024 + _ownedFleet.Count * 113}",
                customPaintColor = paint,
                engineStage = 1,
                tireCompound = TireCompoundType.StreetAllSeason,
                savedHealth = spec != null ? spec.maxHealth : 1000f,
            };
            _ownedFleet.Add(entry);
            return entry;
        }

        public bool TuneAndRepaintOwnedVehicle(int index, Color paintColor, int engineStage, TireCompoundType tires)
        {
            if (index < 0 || index >= _ownedFleet.Count) return false;
            OwnedVehicleRecord rec = _ownedFleet[index];
            rec.customPaintColor = paintColor;
            rec.engineStage = Mathf.Clamp(engineStage, 1, 3);
            rec.tireCompound = tires;
            return true;
        }

        public bool RepairOwnedVehicle(int index)
        {
            if (index < 0 || index >= _ownedFleet.Count) return false;
            OwnedVehicleRecord rec = _ownedFleet[index];
            VehicleSpecSO spec = FindSpec(rec.vehicleId);
            rec.savedHealth = spec != null ? spec.maxHealth : 1000f;
            return true;
        }

        public void ApplyGarageRecordToActiveVehicle(int index, DhakaVehicleController activeVehicle)
        {
            if (index < 0 || index >= _ownedFleet.Count || activeVehicle == null) return;
            OwnedVehicleRecord rec = _ownedFleet[index];
            if (activeVehicle.DamageAndTuning != null)
            {
                activeVehicle.DamageAndTuning.RepairFull();
                activeVehicle.DamageAndTuning.SetCustomPaint(rec.customPaintColor);
                activeVehicle.DamageAndTuning.ApplyTuningUpgrade(rec.engineStage, rec.tireCompound);
            }
        }

        public string SerializeToJson()
        {
            var payload = new GarageSavePayload { ownedVehicles = new List<OwnedVehicleRecord>(_ownedFleet) };
            return JsonUtility.ToJson(payload, true);
        }

        public void DeserializeFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            var payload = JsonUtility.FromJson<GarageSavePayload>(json);
            if (payload?.ownedVehicles != null)
            {
                _ownedFleet.Clear();
                _ownedFleet.AddRange(payload.ownedVehicles);
            }
        }

        public void SaveToDisk(string fileName = "dhaka_garage_save.json")
        {
            string path = Path.Combine(Application.persistentDataPath, fileName);
            File.WriteAllText(path, SerializeToJson());
        }

        public void LoadFromDisk(string fileName = "dhaka_garage_save.json")
        {
            string path = Path.Combine(Application.persistentDataPath, fileName);
            if (File.Exists(path))
            {
                DeserializeFromJson(File.ReadAllText(path));
            }
        }

        private VehicleSpecSO FindSpec(string vehicleId)
        {
            foreach (VehicleSpecSO s in catalog)
            {
                if (s != null && s.vehicleId == vehicleId) return s;
            }
            return null;
        }
    }
}
