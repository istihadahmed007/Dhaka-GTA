using UnityEngine;

namespace DhakaGta.Combat
{
    public interface IHealthTarget
    {
        float CurrentHealth { get; }
        float MaxHealth { get; }
        bool IsAlive { get; }
        void ApplyDamage(float amount, Vector3 hitPoint);
    }
}
