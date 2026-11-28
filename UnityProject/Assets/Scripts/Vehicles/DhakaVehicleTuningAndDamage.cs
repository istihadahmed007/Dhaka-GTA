using System;
using UnityEngine;

namespace DhakaGta.Vehicles
{
    public enum TireCompoundType
    {
        StreetAllSeason = 0,
        SportDrySlick = 1,
        MonsoonRainTread = 2,
    }

    [Serializable]
    public sealed class VehicleTuningProfile
    {
        public Color primaryPaintColor = new Color(0.76f, 0.12f, 0.14f);
        public Color secondaryPaintColor = new Color(0.14f, 0.16f, 0.19f);
        [Range(1, 3)] public int engineStage = 1;
        public TireCompoundType tireCompound = TireCompoundType.StreetAllSeason;
        [Range(0.75f, 1.45f)] public float suspensionStiffnessMultiplier = 1.0f;

        public float TorqueMultiplier => 1.0f + (engineStage - 1) * 0.24f;
        public float TopSpeedMultiplier => 1.0f + (engineStage - 1) * 0.15f;

        public float DryGripBonus => tireCompound switch
        {
            TireCompoundType.SportDrySlick => 1.22f,
            TireCompoundType.MonsoonRainTread => 1.04f,
            _ => 1.0f,
        };

        public float WetGripBonus => tireCompound switch
        {
            TireCompoundType.MonsoonRainTread => 1.38f,
            TireCompoundType.SportDrySlick => 0.82f,
            _ => 1.0f,
        };
    }

    /// <summary>
    /// Tracks structural vehicle HP, collision impact damage, smoke/wreck state, full mechanical repair,
    /// custom HDRP clear-coat paint colors, and engine/tire/suspension tuning upgrades.
    /// </summary>
    public sealed class DhakaVehicleTuningAndDamage : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 1000f;
        [SerializeField] private float currentHealth = 1000f;
        [SerializeField] private VehicleTuningProfile tuning = new VehicleTuningProfile();
        [SerializeField] private Renderer[] paintBodyRenderers = System.Array.Empty<Renderer>();
        [SerializeField] private GameObject engineSmokeProxy;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int CoatMaskId = Shader.PropertyToID("_CoatMask");
        private MaterialPropertyBlock _mpb;

        public event Action<float, float> OnVehicleHealthChanged;
        public event Action OnVehicleWrecked;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public bool IsWrecked => currentHealth <= 0f;
        public bool IsSmoking => currentHealth > 0f && currentHealth <= maxHealth * 0.30f;
        public VehicleTuningProfile Tuning => tuning;

        public void Initialize(float maxHp, Renderer[] bodyRenderers, Color initialPaint)
        {
            maxHealth = Mathf.Max(100f, maxHp);
            currentHealth = maxHealth;
            paintBodyRenderers = bodyRenderers ?? System.Array.Empty<Renderer>();
            tuning.primaryPaintColor = initialPaint;
            ApplyPaintAndTuning();
        }

        public void ApplyDamage(float amount)
        {
            if (amount <= 0f || IsWrecked) return;
            currentHealth = Mathf.Max(0f, currentHealth - amount);
            UpdateDamageVisuals();
            OnVehicleHealthChanged?.Invoke(currentHealth, maxHealth);
            if (currentHealth <= 0f)
            {
                OnVehicleWrecked?.Invoke();
            }
        }

        public void RepairFull()
        {
            currentHealth = maxHealth;
            UpdateDamageVisuals();
            OnVehicleHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void SetCustomPaint(Color primaryColor)
        {
            tuning.primaryPaintColor = primaryColor;
            ApplyPaintAndTuning();
        }

        public void ApplyTuningUpgrade(int engineStage, TireCompoundType compound, float suspensionScale = 1.0f)
        {
            tuning.engineStage = Mathf.Clamp(engineStage, 1, 3);
            tuning.tireCompound = compound;
            tuning.suspensionStiffnessMultiplier = Mathf.Clamp(suspensionScale, 0.75f, 1.45f);
            ApplyPaintAndTuning();
        }

        public void ApplyPaintAndTuning()
        {
            _mpb ??= new MaterialPropertyBlock();
            float damageDarkening = IsWrecked ? 0.35f : 1.0f;
            Color appliedColor = tuning.primaryPaintColor * damageDarkening;
            appliedColor.a = 1f;

            foreach (Renderer r in paintBodyRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, appliedColor);
                _mpb.SetFloat(CoatMaskId, IsWrecked ? 0.1f : 0.85f);
                r.SetPropertyBlock(_mpb);
            }
            UpdateDamageVisuals();
        }

        private void UpdateDamageVisuals()
        {
            if (engineSmokeProxy != null)
            {
                engineSmokeProxy.SetActive(IsSmoking || IsWrecked);
            }
        }
    }
}
