using DhakaGta.Data;

namespace DhakaGta.Vehicles
{
    /// <summary>
    /// Contract implemented by land WheelCollider vehicles and Buriganga watercraft in Phase 2+.
    /// </summary>
    public interface IVehicleDrivable
    {
        VehicleSpecSO Spec { get; }
        bool IsOccupied { get; }
        float ForwardSpeedKph { get; }
        void EnterDriverSeat();
        void ExitDriverSeat();
    }
}
