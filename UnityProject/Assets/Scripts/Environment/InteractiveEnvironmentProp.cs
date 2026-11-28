using DhakaGta.Character;
using DhakaGta.Combat;
using DhakaGta.Core;
using UnityEngine;

namespace DhakaGta.Environment
{
    /// <summary>
    /// Interactive environmental prop (Dhaka Tong Tea Stall, Hatirjheel Promenade Bench, Metro Gate, ATM Kiosk)
    /// delivering concrete gameplay effects cleanly through IInteractable.
    /// </summary>
    public sealed class InteractiveEnvironmentProp : MonoBehaviour, IInteractable
    {
        [SerializeField] private InteractableKind kind = InteractableKind.TeaStall;
        [SerializeField] private string propDisplayName = "Gulshan Tong Tea Stall";
        [SerializeField] private string actionPrompt = "[E] Drink Spiced Cha (Restore HP & Stamina)";
        [SerializeField] private float interactionRadius = 3.6f;
        [SerializeField] private float healthRestoreAmount = 20f;
        [SerializeField] private float cooldownSeconds = 3.0f;

        private float _nextInteractAllowedTime;

        public InteractableKind Kind => kind;
        public string PromptText => actionPrompt;
        public Vector3 WorldPosition => transform.position;
        public float InteractionRadiusMeters => interactionRadius;
        public int UsageCount { get; private set; }

        public void Configure(InteractableKind propKind, string displayName, string prompt, float hpHeal = 20f)
        {
            kind = propKind;
            propDisplayName = displayName;
            actionPrompt = prompt;
            healthRestoreAmount = hpHeal;
        }

        public bool CanInteract(GameObject interactor)
        {
            if (interactor == null) return false;
            if (Time.unscaledTime < _nextInteractAllowedTime) return false;
            return Vector3.Distance(interactor.transform.position, transform.position) <= interactionRadius;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            _nextInteractAllowedTime = Time.unscaledTime + cooldownSeconds;
            UsageCount++;

            var health = interactor.GetComponent<HealthComponent>();
            if (health != null && healthRestoreAmount > 0f)
            {
                health.Heal(healthRestoreAmount);
            }

            var controller = interactor.GetComponent<ProtagonistController>();
            if (controller != null && kind == InteractableKind.TeaStall)
            {
                controller.ResetStatsFromProfile();
            }

            string feedback = kind switch
            {
                InteractableKind.TeaStall => $"{propDisplayName}: Had hot doodh-cha (+{healthRestoreAmount:F0} HP, Stamina restored)",
                InteractableKind.BenchRest => $"{propDisplayName}: Rested overlooking Hatirjheel promenade (+{healthRestoreAmount:F0} HP)",
                InteractableKind.AtmKiosk => $"{propDisplayName}: Balance verified at Dhaka Terminal",
                InteractableKind.MetroGate => $"{propDisplayName}: MRT-6 Rapid Pass scanned",
                _ => $"{propDisplayName}: Interacted",
            };

            InteractionEvents.RaiseFeedback(feedback);
            Debug.Log("[DhakaGta.Interaction] " + feedback);
        }
    }
}
