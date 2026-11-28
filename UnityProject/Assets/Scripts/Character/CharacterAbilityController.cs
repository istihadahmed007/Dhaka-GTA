using DhakaGta.Core;
using DhakaGta.Data;
using UnityEngine;

namespace DhakaGta.Character
{
    /// <summary>
    /// Executes Tariq's Street Overdrive and Maya's Focus Reflex signature abilities,
    /// safely managing Time.timeScale restoration on exit or object disable and publishing
    /// multipliers to Combat, Vehicles, and UI via DhakaGta.Core.CharacterAbilityEvents.
    /// </summary>
    public sealed class CharacterAbilityController : MonoBehaviour
    {
        [SerializeField] private ProtagonistProfileSO profile;
        [SerializeField, Range(0.25f, 1f)] private float focusReflexTimeScale = 0.58f;
        [SerializeField, Min(1f)] private float overdriveSpeedBonus = 1.14f;

        private const float DefaultFixedDeltaTime = 0.02f;

        private bool _isActive;
        private float _activeRemaining;
        private float _cooldownRemaining;

        public bool IsAbilityActive => _isActive;
        public float ActiveRemainingSeconds => _activeRemaining;
        public float CooldownRemainingSeconds => _cooldownRemaining;
        public SpecialAbilityKind CurrentAbilityKind =>
            profile != null ? profile.specialAbility : SpecialAbilityKind.None;

        public float CurrentSpeedMultiplier =>
            _isActive && CurrentAbilityKind == SpecialAbilityKind.StreetOverdrive ? overdriveSpeedBonus : 1f;

        public float CurrentStaminaRegenBonus =>
            _isActive && CurrentAbilityKind == SpecialAbilityKind.StreetOverdrive ? 26f : 0f;

        public void Configure(ProtagonistProfileSO profileAsset)
        {
            if (_isActive)
            {
                EndAbility();
            }
            profile = profileAsset;
            _cooldownRemaining = 0f;
            PublishSnapshot();
        }

        public bool TryActivateAbility()
        {
            if (profile == null || profile.specialAbility == SpecialAbilityKind.None)
            {
                return false;
            }

            if (_isActive || _cooldownRemaining > 0.01f)
            {
                return false;
            }

            _isActive = true;
            _activeRemaining = Mathf.Max(1.5f, profile.abilityDurationSeconds);
            _cooldownRemaining = 0f;

            if (profile.specialAbility == SpecialAbilityKind.FocusReflex)
            {
                Time.timeScale = focusReflexTimeScale;
                Time.fixedDeltaTime = DefaultFixedDeltaTime * focusReflexTimeScale;
            }

            PublishSnapshot();
            return true;
        }

        public void ForceStopAbility()
        {
            if (_isActive)
            {
                EndAbility();
            }
        }

        /// <summary>
        /// Steps active duration and cooldown timers using unscaled time so slow-motion reflex
        /// duration feels consistent in wall-clock seconds.
        /// </summary>
        public void StepAbilityTimers(float unscaledDeltaTime)
        {
            float dt = Mathf.Max(0f, unscaledDeltaTime);
            bool changed = false;

            if (_isActive)
            {
                _activeRemaining -= dt;
                if (_activeRemaining <= 0f)
                {
                    EndAbility();
                    return;
                }
                changed = true;
            }
            else if (_cooldownRemaining > 0f)
            {
                _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - dt);
                changed = true;
            }

            if (changed)
            {
                PublishSnapshot();
            }
        }

        private void EndAbility()
        {
            _isActive = false;
            _activeRemaining = 0f;
            _cooldownRemaining = profile != null ? Mathf.Max(2f, profile.abilityCooldownSeconds) : 15f;

            if (Mathf.Abs(Time.timeScale - 1f) > 0.001f && GameBootstrap.CurrentState != GameState.Paused)
            {
                Time.timeScale = 1f;
                Time.fixedDeltaTime = DefaultFixedDeltaTime;
            }

            PublishSnapshot();
        }

        private void OnDisable()
        {
            if (Mathf.Abs(Time.timeScale - 1f) > 0.001f)
            {
                Time.timeScale = 1f;
                Time.fixedDeltaTime = DefaultFixedDeltaTime;
            }
            _isActive = false;
        }

        private void PublishSnapshot()
        {
            string id = profile != null ? profile.protagonistId : "unknown";
            SpecialAbilityKind kind = profile != null ? profile.specialAbility : SpecialAbilityKind.None;
            float maxCd = profile != null ? Mathf.Max(1f, profile.abilityCooldownSeconds) : 20f;

            float dmgMult = profile != null ? profile.combatDamageMultiplier : 1f;
            float aimMult = profile != null ? profile.aimPrecisionMultiplier : 1f;
            float drift = profile != null ? profile.driftHandlingBonus : 0f;

            if (_isActive && kind == SpecialAbilityKind.StreetOverdrive)
            {
                dmgMult *= Mathf.Max(1.35f, profile.meleeDamageMultiplier);
                drift += 0.35f;
            }
            else if (_isActive && kind == SpecialAbilityKind.FocusReflex)
            {
                aimMult *= 1.65f;
            }

            CharacterAbilityEvents.PublishAbilityState(new AbilityActivationState
            {
                ProtagonistId = id,
                AbilityKind = kind,
                IsActive = _isActive,
                RemainingDurationSeconds = _activeRemaining,
                CooldownRemainingSeconds = _cooldownRemaining,
                CooldownNormalized = _isActive ? 1f : 1f - Mathf.Clamp01(_cooldownRemaining / maxCd),
                CombatDamageBonusMultiplier = dmgMult,
                AimPrecisionBonusMultiplier = aimMult,
                DriftControlBonus = drift,
                TargetTimeScale = _isActive && kind == SpecialAbilityKind.FocusReflex ? focusReflexTimeScale : 1f
            });
        }
    }
}
