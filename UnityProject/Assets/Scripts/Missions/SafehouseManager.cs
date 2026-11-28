using System;
using System.Collections.Generic;
using DhakaGta.Character;
using DhakaGta.Combat;
using DhakaGta.Core;
using DhakaGta.Data;
using UnityEngine;

namespace DhakaGta.Missions
{
    /// <summary>
    /// Interactive Safehouse portal (Gulshan Apartment, Old Dhaka Rooftop, Dhanmondi Studio, Motijheel Pad).
    /// Supports purchasing, resting to heal/clear minor wanted heat, and saving game progress.
    /// </summary>
    public sealed class SafehouseInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private SafehouseDefinitionSO definition;
        [SerializeField] private PlayerEconomyAndInventory economy;
        [SerializeField] private DhakaSaveSystem saveSystem;

        public SafehouseDefinitionSO Definition => definition;
        public InteractableKind Kind => InteractableKind.Generic;

        public string PromptText
        {
            get
            {
                if (definition == null) return "[E] Safehouse";
                bool owned = IsOwned();
                return owned
                    ? $"[E] Rest & Save ({definition.displayName} · {definition.banglaName})"
                    : $"[E] Buy {definition.displayName} (৳{definition.purchaseCostBdt:N0})";
            }
        }

        public Vector3 WorldPosition => transform.position;
        public float InteractionRadiusMeters => 3.5f;

        public void Configure(SafehouseDefinitionSO def, PlayerEconomyAndInventory econ, DhakaSaveSystem saver)
        {
            definition = def;
            economy = econ;
            saveSystem = saver;
            if (def != null && def.unlockedByDefault && economy != null)
            {
                economy.GrantSafehouseUnlock(def.safehouseId);
            }
        }

        public bool IsOwned()
        {
            if (definition == null) return false;
            if (definition.unlockedByDefault) return true;
            if (economy == null) return false;
            foreach (string id in economy.UnlockedSafehouses)
            {
                if (id == definition.safehouseId) return true;
            }
            return false;
        }

        public bool CanInteract(GameObject interactor)
        {
            return interactor != null && Vector3.Distance(interactor.transform.position, transform.position) <= InteractionRadiusMeters;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor) || definition == null) return;

            if (!IsOwned())
            {
                if (economy != null && economy.TrySpendBdt(definition.purchaseCostBdt, definition.displayName))
                {
                    economy.GrantSafehouseUnlock(definition.safehouseId);
                    InteractionEvents.RaiseFeedback($"Safehouse Purchased: {definition.displayName} ({definition.banglaName})");
                }
                else
                {
                    InteractionEvents.RaiseFeedback($"Need ৳{definition.purchaseCostBdt:N0} to buy {definition.displayName}");
                    return;
                }
            }

            // Rest: restore full HP + stamina + advance clock + cool down low wanted heat + AutoSave
            interactor.GetComponent<HealthComponent>()?.Heal(definition.restHealAmount);
            interactor.GetComponent<ProtagonistController>()?.ResetStatsFromProfile();
            WorldSurfaceState.CurrentTimeOfDayHours = Mathf.Repeat(WorldSurfaceState.CurrentTimeOfDayHours + 6.0f, 24f);
            if (CityDangerBus.CurrentWantedLevel <= definition.clearMaxWantedStars)
            {
                CityDangerBus.ClearAllForReset();
            }

            saveSystem?.SaveToDisk();
            InteractionEvents.RaiseFeedback($"Rested at {definition.displayName}. Progress saved!");
        }
    }

    public sealed class SafehouseManager : MonoBehaviour
    {
        [SerializeField] private SafehouseDefinitionSO[] safehouses = System.Array.Empty<SafehouseDefinitionSO>();
        private readonly List<SafehouseInteractable> _activePortals = new List<SafehouseInteractable>();

        public IReadOnlyList<SafehouseDefinitionSO> Safehouses => safehouses;
        public IReadOnlyList<SafehouseInteractable> ActivePortals => _activePortals;

        public void ConfigureAndSpawnPortals(
            SafehouseDefinitionSO[] defs,
            PlayerEconomyAndInventory economy,
            DhakaSaveSystem saveSystem,
            Transform parentRoot)
        {
            safehouses = defs ?? System.Array.Empty<SafehouseDefinitionSO>();
            _activePortals.Clear();

            for (int i = 0; i < safehouses.Length; i++)
            {
                SafehouseDefinitionSO def = safehouses[i];
                if (def == null) continue;

                var doorGo = new GameObject($"Safehouse_{def.safehouseId}");
                doorGo.layer = PhysicsLayers.TriggerZone;
                if (parentRoot != null) doorGo.transform.SetParent(parentRoot, false);
                doorGo.transform.position = def.fallbackLocalPosition;

                var col = doorGo.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = new Vector3(2.4f, 2.8f, 2.4f);

                var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = "SafehouseBeaconRing";
                DestroyImmediateIfSafe(marker.GetComponent<Collider>());
                marker.transform.SetParent(doorGo.transform, false);
                marker.transform.localScale = new Vector3(1.5f, 0.06f, 1.5f);
                var mr = marker.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    Shader sh = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
                    var mat = new Material(sh);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.15f, 0.85f, 0.45f));
                    if (mat.HasProperty("_EmissiveColor")) mat.SetColor("_EmissiveColor", new Color(0.2f, 1.8f, 0.6f));
                    mr.sharedMaterial = mat;
                }

                var portal = doorGo.AddComponent<SafehouseInteractable>();
                portal.Configure(def, economy, saveSystem);
                _activePortals.Add(portal);
            }
        }

        private static void DestroyImmediateIfSafe(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
