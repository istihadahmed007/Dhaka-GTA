using System;
using DhakaGta.Data;
using UnityEngine;

namespace DhakaGta.Combat
{
    /// <summary>
    /// Health container honoring ProtagonistProfileSO dynamic caps rather than hardcoded 100 HP.
    /// </summary>
    public sealed class HealthComponent : MonoBehaviour, IHealthTarget
    {
        [SerializeField] private ProtagonistProfileSO optionalProfile;
        [SerializeField] private float fallbackMaxHealth = 100f;

        public event Action<float, float> OnHealthChanged;
        public event Action OnDied;

        public float MaxHealth { get; private set; }
        public float CurrentHealth { get; private set; }
        public bool IsAlive => CurrentHealth > 0f;

        private void Awake()
        {
            InitializeFromProfile(optionalProfile);
        }

        public void InitializeFromProfile(ProtagonistProfileSO profile)
        {
            optionalProfile = profile;
            MaxHealth = profile != null ? Mathf.Max(1f, profile.maxHealth) : Mathf.Max(1f, fallbackMaxHealth);
            CurrentHealth = MaxHealth;
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        public void ApplyDamage(float amount, Vector3 hitPoint)
        {
            if (!IsAlive || amount <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
            if (CurrentHealth <= 0f)
            {
                OnDied?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }
    }
}
