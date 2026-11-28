using System;
using UnityEngine;

namespace DhakaGta.Core
{
    public enum InteractableKind
    {
        Generic = 0,
        TeaStall = 1,
        VehicleEntry = 2,
        BenchRest = 3,
        MetroGate = 4,
        AtmKiosk = 5,
    }

    /// <summary>
    /// Uniform contract for world props and vehicles that the player can interact with (e.g., [E] Drink Cha, [F] Enter Car).
    /// </summary>
    public interface IInteractable
    {
        InteractableKind Kind { get; }
        string PromptText { get; }
        Vector3 WorldPosition { get; }
        float InteractionRadiusMeters { get; }
        bool CanInteract(GameObject interactor);
        void Interact(GameObject interactor);
    }

    public static class InteractionEvents
    {
        public static event Action<string> OnInteractionFeedback;

        public static void RaiseFeedback(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                OnInteractionFeedback?.Invoke(message);
            }
        }
    }
}
