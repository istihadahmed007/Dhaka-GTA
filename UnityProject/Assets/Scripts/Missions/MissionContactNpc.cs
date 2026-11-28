using System;
using DhakaGta.Core;
using DhakaGta.Data;
using DhakaGta.TrafficAI;
using UnityEngine;

namespace DhakaGta.Missions
{
    /// <summary>
    /// Interactive story, shop, and side-contract NPC delivering bilingual Bengali + English dialogue trees
    /// and advancing InteractWithNpc mission objectives cleanly.
    /// </summary>
    public sealed class MissionContactNpc : MonoBehaviour, IInteractable
    {
        [SerializeField] private string contactId = "rafiq_bhai";
        [SerializeField] private string contactDisplayName = "Rafiq Bhai (রফিক ভাই)";
        [SerializeField] private MissionDefinitionSO offeredMission;
        [SerializeField] private BilingualDialogueLine[] dialogueLines = Array.Empty<BilingualDialogueLine>();

        private int _lineCursor;
        private DhakaMissionEngine _missionEngine;

        public string ContactId => contactId;
        public InteractableKind Kind => InteractableKind.Generic;
        public string PromptText => $"[E] Talk to {contactDisplayName}";
        public Vector3 WorldPosition => transform.position;
        public float InteractionRadiusMeters => 3.5f;

        public void ConfigureContact(
            string id,
            string displayName,
            PedestrianWardrobeArchetype visualArchetype,
            BilingualDialogueLine[] lines,
            DhakaMissionEngine engine,
            MissionDefinitionSO optionalOffer = null)
        {
            contactId = id;
            contactDisplayName = displayName;
            dialogueLines = lines ?? Array.Empty<BilingualDialogueLine>();
            _missionEngine = engine;
            offeredMission = optionalOffer;

            if (GetComponent<PedestrianRigJoints>() == null)
            {
                BangladeshiPedestrianVisualBuilder.BuildArticulatedBangladeshiNpcVisual(
                    transform, visualArchetype, id.GetHashCode());
            }

            if (GetComponent<Collider>() == null)
            {
                var cap = gameObject.AddComponent<CapsuleCollider>();
                cap.height = 1.8f;
                cap.radius = 0.45f;
                cap.center = new Vector3(0f, 0.9f, 0f);
                cap.isTrigger = true;
            }
            gameObject.layer = PhysicsLayers.TriggerZone;
        }

        public bool CanInteract(GameObject interactor)
        {
            return interactor != null && Vector3.Distance(interactor.transform.position, transform.position) <= InteractionRadiusMeters;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;

            if (dialogueLines != null && dialogueLines.Length > 0)
            {
                BilingualDialogueLine line = dialogueLines[_lineCursor % dialogueLines.Length];
                _lineCursor++;
                MissionObjectiveEvents.RaiseDialogue(line);
            }
            else
            {
                MissionObjectiveEvents.RaiseDialogue(new BilingualDialogueLine
                {
                    Speaker = contactDisplayName,
                    TextBangla = "ঢাকার রাস্তায় চোখ-কান খোলা রেখো।",
                    TextEnglish = "Keep your eyes open on the streets of Dhaka.",
                    DisplaySeconds = 4.5f
                });
            }

            _missionEngine?.NotifyNpcInteracted(contactId);

            if (offeredMission != null && _missionEngine != null && _missionEngine.ActiveMission == null)
            {
                _missionEngine.StartMission(offeredMission.missionId);
            }
        }
    }
}
