using System;
using System.Collections.Generic;
using DhakaGta.Character;
using DhakaGta.Combat;
using DhakaGta.Core;
using DhakaGta.Data;
using DhakaGta.Vehicles;
using UnityEngine;

namespace DhakaGta.Missions
{
    [Serializable]
    public sealed class InventorySlotRecord
    {
        public string itemId;
        public string nameEnglish;
        public string nameBangla;
        public InventoryItemCategory category;
        public int count;
        public int unitPriceBdt;
        public float healAmount;
        public float staminaRestore;
    }

    /// <summary>
    /// Manages Bangladeshi Taka (BDT ৳) wallet economy, street shop catalog, consumable & mission inventory,
    /// Tariq-Maya relationship bond progression, and unlocked weapons/safehouses.
    /// </summary>
    public sealed class PlayerEconomyAndInventory : MonoBehaviour
    {
        [SerializeField] private int startingBdt = 2500;

        private readonly List<InventorySlotRecord> _items = new List<InventorySlotRecord>();
        private readonly List<string> _unlockedWeapons = new List<string> { "unarmed" };
        private readonly List<string> _unlockedSafehouses = new List<string> { "old_dhaka_rooftop" };
        private readonly List<string> _unlockedOutfits = new List<string> { "street_default" };

        private static readonly Dictionary<string, InventoryItemSpec> DefaultCatalog =
            new Dictionary<string, InventoryItemSpec>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "cha_flask",
                    new InventoryItemSpec
                    {
                        itemId = "cha_flask",
                        nameEnglish = "Spiced Tong Cha Flask",
                        nameBangla = "মসলা চায়ের ফ্লাস্ক",
                        category = InventoryItemCategory.FoodAndCha,
                        priceBdt = 60,
                        healAmount = 25f,
                        staminaRestore = 45f,
                        description = "Fresh Dhaka cardamom milk tea restoring HP and sprinting stamina."
                    }
                },
                {
                    "kacchi_pack",
                    new InventoryItemSpec
                    {
                        itemId = "kacchi_pack",
                        nameEnglish = "Old Dhaka Kacchi Box",
                        nameBangla = "পুরান ঢাকার কাচ্চি",
                        category = InventoryItemCategory.FoodAndCha,
                        priceBdt = 280,
                        healAmount = 60f,
                        staminaRestore = 60f,
                        description = "Hearty mutton kacchi recovery meal."
                    }
                },
                {
                    "first_aid_kit",
                    new InventoryItemSpec
                    {
                        itemId = "first_aid_kit",
                        nameEnglish = "Trauma Medkit",
                        nameBangla = "প্রাথমিক চিকিৎসা কিট",
                        category = InventoryItemCategory.Medkit,
                        priceBdt = 450,
                        healAmount = 100f,
                        staminaRestore = 20f,
                        description = "Full medical dressing for gunfights."
                    }
                },
                {
                    "repair_toolkit",
                    new InventoryItemSpec
                    {
                        itemId = "repair_toolkit",
                        nameEnglish = "Dholaikhal Wrench Kit",
                        nameBangla = "ধোলাইখাল মেরামত কিট",
                        category = InventoryItemCategory.VehicleRepairKit,
                        priceBdt = 650,
                        healAmount = 0f,
                        staminaRestore = 0f,
                        description = "Field-repairs active vehicle engine and chassis."
                    }
                }
            };

        public int CurrentBdt { get; private set; }
        public int TotalEarnedBdt { get; private set; }
        public int TotalSpentBdt { get; private set; }
        public int TariqMayaBondXp { get; private set; }

        public int FaresCompletedCount { get; set; }
        public int DeliveriesCompletedCount { get; set; }
        public int ContractsCompletedCount { get; set; }
        public int RacesWonCount { get; set; }

        public IReadOnlyList<InventorySlotRecord> Items => _items;
        public IReadOnlyList<string> UnlockedWeapons => _unlockedWeapons;
        public IReadOnlyList<string> UnlockedSafehouses => _unlockedSafehouses;
        public IReadOnlyList<string> UnlockedOutfits => _unlockedOutfits;

        public string TariqMayaBondTierLabel => TariqMayaBondXp switch
        {
            >= 120 => "Unbreakable Partners (অটুট জুটি)",
            >= 60 => "Trusted Allies (বিশ্বস্ত সঙ্গী)",
            >= 25 => "Street Allies (ঢাকার সহযোদ্ধা)",
            _ => "Reluctant Contacts (নতুন পরিচয়)"
        };

        private void Awake()
        {
            if (CurrentBdt == 0 && _items.Count == 0)
            {
                ResetStarterEconomy();
            }
        }

        public void ResetStarterEconomy()
        {
            CurrentBdt = startingBdt;
            TotalEarnedBdt = 0;
            TotalSpentBdt = 0;
            TariqMayaBondXp = 10;
            FaresCompletedCount = 0;
            DeliveriesCompletedCount = 0;
            ContractsCompletedCount = 0;
            RacesWonCount = 0;

            _items.Clear();
            GrantCatalogItem("cha_flask", 2);
            GrantCatalogItem("first_aid_kit", 1);

            _unlockedWeapons.Clear();
            _unlockedWeapons.Add("unarmed");
            _unlockedSafehouses.Clear();
            _unlockedSafehouses.Add("old_dhaka_rooftop");
            _unlockedOutfits.Clear();
            _unlockedOutfits.Add("street_default");

            MissionObjectiveEvents.RaiseWalletChanged(CurrentBdt);
            MissionObjectiveEvents.RaiseInventoryChanged();
            MissionObjectiveEvents.RaiseBondChanged(TariqMayaBondXp, TariqMayaBondTierLabel);
        }

        public bool CanAfford(int amountBdt) => CurrentBdt >= Mathf.Max(0, amountBdt);

        public void AddBdt(int amountBdt, string reason = null)
        {
            if (amountBdt <= 0) return;
            CurrentBdt += amountBdt;
            TotalEarnedBdt += amountBdt;
            MissionObjectiveEvents.RaiseWalletChanged(CurrentBdt);
            if (!string.IsNullOrEmpty(reason))
            {
                InteractionEvents.RaiseFeedback($"+৳{amountBdt:N0} BDT ({reason})");
            }
        }

        public bool TrySpendBdt(int amountBdt, string reason = null)
        {
            if (amountBdt <= 0) return true;
            if (CurrentBdt < amountBdt) return false;
            CurrentBdt -= amountBdt;
            TotalSpentBdt += amountBdt;
            MissionObjectiveEvents.RaiseWalletChanged(CurrentBdt);
            if (!string.IsNullOrEmpty(reason))
            {
                InteractionEvents.RaiseFeedback($"-৳{amountBdt:N0} BDT ({reason})");
            }
            return true;
        }

        public void AddRelationshipBondXp(int delta)
        {
            if (delta == 0) return;
            TariqMayaBondXp = Mathf.Max(0, TariqMayaBondXp + delta);
            MissionObjectiveEvents.RaiseBondChanged(TariqMayaBondXp, TariqMayaBondTierLabel);
        }

        public void GrantWeaponUnlock(string weaponId)
        {
            if (!string.IsNullOrWhiteSpace(weaponId) && !_unlockedWeapons.Contains(weaponId))
            {
                _unlockedWeapons.Add(weaponId);
                InteractionEvents.RaiseFeedback($"Weapon Unlocked: {weaponId}");
            }
        }

        public void GrantSafehouseUnlock(string safehouseId)
        {
            if (!string.IsNullOrWhiteSpace(safehouseId) && !_unlockedSafehouses.Contains(safehouseId))
            {
                _unlockedSafehouses.Add(safehouseId);
                MissionObjectiveEvents.RaiseSafehouseUnlocked(safehouseId);
            }
        }

        public void GrantOutfitUnlock(string outfitId)
        {
            if (!string.IsNullOrWhiteSpace(outfitId) && !_unlockedOutfits.Contains(outfitId))
            {
                _unlockedOutfits.Add(outfitId);
            }
        }

        public bool PurchaseCatalogItem(string itemId, int quantity = 1)
        {
            if (quantity <= 0 || !DefaultCatalog.TryGetValue(itemId, out InventoryItemSpec spec))
            {
                return false;
            }

            int totalCost = spec.priceBdt * quantity;
            if (!TrySpendBdt(totalCost, spec.nameEnglish))
            {
                return false;
            }

            GrantCatalogItem(itemId, quantity);
            return true;
        }

        public void GrantCatalogItem(string itemId, int quantity)
        {
            if (quantity <= 0 || !DefaultCatalog.TryGetValue(itemId, out InventoryItemSpec spec))
            {
                return;
            }

            InventorySlotRecord existing = _items.Find(s => s.itemId == spec.itemId);
            if (existing != null)
            {
                existing.count += quantity;
            }
            else
            {
                _items.Add(new InventorySlotRecord
                {
                    itemId = spec.itemId,
                    nameEnglish = spec.nameEnglish,
                    nameBangla = spec.nameBangla,
                    category = spec.category,
                    count = quantity,
                    unitPriceBdt = spec.priceBdt,
                    healAmount = spec.healAmount,
                    staminaRestore = spec.staminaRestore
                });
            }

            MissionObjectiveEvents.RaiseInventoryChanged();
        }

        public bool UseInventoryItem(string itemId, GameObject playerGo, DhakaVehicleController optionalVehicle = null)
        {
            InventorySlotRecord slot = _items.Find(s => s.itemId == itemId && s.count > 0);
            if (slot == null) return false;

            if (slot.category == InventoryItemCategory.VehicleRepairKit)
            {
                if (optionalVehicle == null || optionalVehicle.DamageAndTuning == null) return false;
                optionalVehicle.DamageAndTuning.RepairFull();
            }
            else if (playerGo != null)
            {
                if (slot.healAmount > 0f)
                {
                    playerGo.GetComponent<HealthComponent>()?.Heal(slot.healAmount);
                }
                if (slot.staminaRestore > 0f)
                {
                    playerGo.GetComponent<ProtagonistController>()?.ResetStatsFromProfile();
                }
            }

            slot.count--;
            if (slot.count <= 0)
            {
                _items.Remove(slot);
            }

            MissionObjectiveEvents.RaiseInventoryChanged();
            InteractionEvents.RaiseFeedback($"Used {slot.nameEnglish} ({slot.nameBangla})");
            return true;
        }

        public IReadOnlyDictionary<string, InventoryItemSpec> GetShopCatalog() => DefaultCatalog;

        public void ApplyLoadedState(
            int bdt,
            int earned,
            int spent,
            int bondXp,
            int fares,
            int deliveries,
            int contracts,
            int races,
            List<InventorySlotRecord> loadedItems,
            List<string> weapons,
            List<string> safehouses,
            List<string> outfits)
        {
            CurrentBdt = Mathf.Max(0, bdt);
            TotalEarnedBdt = Mathf.Max(0, earned);
            TotalSpentBdt = Mathf.Max(0, spent);
            TariqMayaBondXp = Mathf.Max(0, bondXp);
            FaresCompletedCount = fares;
            DeliveriesCompletedCount = deliveries;
            ContractsCompletedCount = contracts;
            RacesWonCount = races;

            _items.Clear();
            if (loadedItems != null) _items.AddRange(loadedItems);

            _unlockedWeapons.Clear();
            if (weapons != null && weapons.Count > 0) _unlockedWeapons.AddRange(weapons);
            else _unlockedWeapons.Add("unarmed");

            _unlockedSafehouses.Clear();
            if (safehouses != null && safehouses.Count > 0) _unlockedSafehouses.AddRange(safehouses);
            else _unlockedSafehouses.Add("old_dhaka_rooftop");

            _unlockedOutfits.Clear();
            if (outfits != null && outfits.Count > 0) _unlockedOutfits.AddRange(outfits);
            else _unlockedOutfits.Add("street_default");

            MissionObjectiveEvents.RaiseWalletChanged(CurrentBdt);
            MissionObjectiveEvents.RaiseInventoryChanged();
            MissionObjectiveEvents.RaiseBondChanged(TariqMayaBondXp, TariqMayaBondTierLabel);
        }
    }
}
