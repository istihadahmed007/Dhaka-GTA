using System;
using UnityEngine;

namespace DhakaGta.Core
{
    /// <summary>
    /// Signature protagonist special abilities ported cleanly from legacy Dhaka-GTA specs.
    /// </summary>
    public enum SpecialAbilityKind
    {
        None = 0,
        StreetOverdrive = 1, // Tariq: melee impact surge, vehicle drift grip boost, stamina recovery
        FocusReflex = 2      // Maya: tactical slow-motion time dilation + high aim precision lock
    }

    /// <summary>
    /// Expressive human action states triggered outside base locomotion.
    /// </summary>
    public enum CharacterGestureKind
    {
        None = 0,
        GreetingWave = 1,
        PersuadeHandGesture = 2,
        PointDirection = 3,
        FrustratedShrug = 4,
        InspectItem = 5,
        RadioCheck = 6
    }

    /// <summary>
    /// Shared snapshot published by the active protagonist ability controller so Combat,
    /// Vehicles, Graphics, and UI modules can subscribe without Character depending on them.
    /// </summary>
    [Serializable]
    public struct AbilityActivationState
    {
        public string ProtagonistId;
        public SpecialAbilityKind AbilityKind;
        public bool IsActive;
        public float RemainingDurationSeconds;
        public float CooldownRemainingSeconds;
        public float CooldownNormalized;
        public float CombatDamageBonusMultiplier;
        public float AimPrecisionBonusMultiplier;
        public float DriftControlBonus;
        public float TargetTimeScale;
    }

    /// <summary>
    /// Weapon aiming pose payload forwarded from Combat/Input or synthetic testing into IK.
    /// </summary>
    [Serializable]
    public struct CharacterAimTargetState
    {
        public bool IsAiming;
        public Vector3 WorldAimPoint;
        public float AimBlendWeight;
        public bool HasTwoHandedWeapon;
    }

    /// <summary>
    /// Vehicle boarding/exiting synchronization payload communicated via Core events.
    /// </summary>
    [Serializable]
    public struct VehicleBoardingEventData
    {
        public string ProtagonistId;
        public bool IsEntering;
        public bool UsesLeftDoor;
        public Vector3 SeatWorldPosition;
        public Quaternion SeatWorldRotation;
    }

    /// <summary>
    /// Central event bus in DhakaGta.Core preventing upward assembly cycles from DhakaGta.Character.
    /// </summary>
    public static class CharacterAbilityEvents
    {
        public static event Action<AbilityActivationState> OnAbilityStateChanged;
        public static event Action<string, CharacterGestureKind> OnDialogueGestureTriggered;
        public static event Action<string, Vector3> OnInteractionReachTriggered;
        public static event Action<string, bool> OnSwimmingStateChanged;
        public static event Action<VehicleBoardingEventData> OnVehicleBoardingRequested;
        public static event Action<string> OnActiveProtagonistSwitched;

        public static AbilityActivationState CurrentAbilityState { get; private set; }

        public static void PublishAbilityState(AbilityActivationState state)
        {
            CurrentAbilityState = state;
            OnAbilityStateChanged?.Invoke(state);
        }

        public static void PublishDialogueGesture(string protagonistId, CharacterGestureKind gesture)
        {
            OnDialogueGestureTriggered?.Invoke(protagonistId, gesture);
        }

        public static void PublishInteractionReach(string protagonistId, Vector3 worldTarget)
        {
            OnInteractionReachTriggered?.Invoke(protagonistId, worldTarget);
        }

        public static void PublishSwimmingState(string protagonistId, bool isSwimming)
        {
            OnSwimmingStateChanged?.Invoke(protagonistId, isSwimming);
        }

        public static void PublishVehicleBoarding(VehicleBoardingEventData payload)
        {
            OnVehicleBoardingRequested?.Invoke(payload);
        }

        public static void PublishProtagonistSwitched(string newProtagonistId)
        {
            OnActiveProtagonistSwitched?.Invoke(newProtagonistId);
        }
    }
}
