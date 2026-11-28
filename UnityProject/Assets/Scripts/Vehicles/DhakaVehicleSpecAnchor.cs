using DhakaGta.Core;
using DhakaGta.Data;
using UnityEngine;

namespace DhakaGta.Vehicles
{
    /// <summary>
    /// Phase 1 foundation anchor binding a Unity Rigidbody vehicle chassis to its VehicleSpecSO and physics layer.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DhakaVehicleSpecAnchor : MonoBehaviour, IVehicleDrivable
    {
        [SerializeField] private VehicleSpecSO vehicleSpec;
        [SerializeField] private bool isTrafficOwned;

        private Rigidbody _rb;

        public VehicleSpecSO Spec => vehicleSpec;
        public bool IsOccupied { get; private set; }
        public float ForwardSpeedKph => _rb != null ? Vector3.Dot(_rb.linearVelocity, transform.forward) * 3.6f : 0f;

        public void Configure(VehicleSpecSO spec, bool trafficOwned = false)
        {
            vehicleSpec = spec;
            isTrafficOwned = trafficOwned;
            ApplyPhysicsLayerAndMass();
        }

        public void EnterDriverSeat() => IsOccupied = true;
        public void ExitDriverSeat() => IsOccupied = false;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            ApplyPhysicsLayerAndMass();
        }

        private void ApplyPhysicsLayerAndMass()
        {
            gameObject.layer = isTrafficOwned ? PhysicsLayers.TrafficVehicle : PhysicsLayers.Vehicle;
            if (_rb != null && vehicleSpec != null)
            {
                _rb.mass = Mathf.Max(50f, vehicleSpec.massKg);
            }
        }
    }
}
