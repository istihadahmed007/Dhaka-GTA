# PART 9 — UNITY OPEN-WORLD GAMEPLAY AND STORY SYSTEM

## Overview
Part 9 implements the connected single-player Bangladesh campaign, bilingual dialogue, open-world side hustles, inventory, BDT economy, safehouses, Smartphone UI, and Save/Load persistence inside **Unity 6 HDRP C#** (`DhakaGta.Data`, `DhakaGta.Missions`, `DhakaGta.UI`, `DhakaGta.Editor`).

---

## Story & Dual-Protagonist Campaign (`Tariq & Maya`)
- **[MissionDefinitionSO.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Scripts/Data/MissionDefinitionSO.cs)** & **[DhakaStoryContentBuilder.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Scripts/Editor/DhakaStoryContentBuilder.cs)**:
  - Authored as inspectable ScriptableObject assets (`Assets/ScriptableObjects/Missions/`), interconnected via prerequisites (`prerequisiteMissionIds`) and real geographic WGS84 Dhaka coordinates resolved through `WorldCoordinateManager` / `DhakaGeoProjection`:
    1. `Mission_01_FirstCupAtTong` (*চায়ের দোকানের বার্তা* — **Tariq**): Meet Rafiq Bhai at Gulshan Tong Tea Stall (`InteractWithNpc`), repel 3 Syndicate enforcers (`EliminateTargets`), earn `৳12,000` + unlock `service_9mm`.
    2. `Mission_02_MayaSignalIntercept` (*মায়ার গোপন সংকেত* — **Maya**): Requires protagonist switch (`SwitchProtagonistStep`), intercept Hatirjheel Promenade transponder (`GoToLocation`), and shake DMP patrols (`EvadePoliceWanted`).
    3. `Mission_03_BurigangaSteelRun` (*বুড়িগঙ্গার চালান* — **Tariq**): Travel to Sadarghat terminal and deliver convoy vehicle back to Gulshan Bay within countdown timer (`StealAndDeliverVehicle`).
    4. `Mission_04_HatirjheelRace` (*হাতিরঝিল রাতের রেস* — **Tariq or Maya**): Complete 4 sequential Midnight Circuit race gates (`CompleteRaceCheckpoints`).
    5. `Mission_05_DawnOverDhaka` (*ঢাকার নতুন ভোর* — **Tariq & Maya Finale**): Dismantle Padma Syndicate High Command at Shapla Monument Plaza, earning `৳50,000` + `gulshan_circle_penthouse` Safehouse unlock + maximum Tariq–Maya Bond boost.
    6. `SideQuest_01_ChawkbazarTeaAid` (*চা-স্টলের জরুরি সরবরাহ*): Timed Dhaka supply aid run.

---

## Reusable Mission Runtime Engine & Side Jobs
- **[DhakaMissionEngine.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Scripts/Missions/DhakaMissionEngine.cs)**:
  - Drives mission initialization, step-by-step objective evaluation (`GoToLocation`, `InteractWithNpc`, `EliminateTargets`, `StealAndDeliverVehicle`, `PassengerFareDropoff`, `DeliverPackageTimed`, `CompleteRaceCheckpoints`, `EvadePoliceWanted`, `SwitchProtagonistStep`), dynamic actor spawning/cleanup, countdown failure, protagonist-death failure, and progression persistence.
  - Launches 4 repeatable open-world hustles (`StartDynamicCngOrTaxiFare`, `StartDynamicDeliveryJob`, `StartDynamicVehicleContractJob`, `StartDynamicStreetRace`) over real city coordinates.
- **[MissionContactNpc.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Scripts/Missions/MissionContactNpc.cs)**, **[MissionTargetActor.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Scripts/Missions/MissionTargetActor.cs)** & **[StreetRaceController.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Scripts/Missions/StreetRaceController.cs)**:
  - Interactive articulated story/shop NPCs with Bengali + English dialogues, hostile combat enforcers integrated with `HealthComponent` and `CityDangerBus`, and live highway race checkpoint beacons.
- **[MissionRegistry.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Scripts/Missions/MissionRegistry.cs)**: Preserved and bridged cleanly into `DhakaMissionEngine`.

---

## Economy, Inventory, Safehouses, Smartphone UI & Persistence
- **[PlayerEconomyAndInventory.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Scripts/Missions/PlayerEconomyAndInventory.cs)**:
  - Bangladeshi Taka (`৳ BDT`) wallet, consumable inventory & Dhaka street shop (`cha_flask`, `kacchi_pack`, `first_aid_kit`, `repair_toolkit`), weapon/outfit/safehouse unlocks, hustle statistics, and **Tariq–Maya Partnership Bond** (`Reluctant Contacts` -> `Street Allies` -> `Trusted Allies` -> `Unbreakable Partners`).
- **[SafehouseManager.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Scripts/Missions/SafehouseManager.cs)** & **[SafehouseAndShopDataSO.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Scripts/Data/SafehouseAndShopDataSO.cs)**:
  - 4 interactive Dhaka Safehouses (`old_dhaka_rooftop`, `gulshan_circle_penthouse`, `dhanmondi_lake_studio`, `motijheel_crash_flat`) supporting purchase, HP/Stamina rest, time advance (`+6h`), minor wanted heat reset, and Save triggers.
- **[DhakaSaveSystem.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Scripts/Missions/DhakaSaveSystem.cs)**:
  - Versioned `JsonUtility` persistence (`dhaka_gta_save_v1.json`) capturing active protagonist, coordinates, health, BDT wallet, bond XP, inventory items, unlocked weapons/outfits/safehouses, garage vehicles (`DhakaGarageManager`), mission completion/progress, and time of day.
- **[DhakaSmartphoneController.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Scripts/UI/DhakaSmartphoneController.cs)** & **[DhakaOpenWorldHudController.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Scripts/UI/DhakaOpenWorldHudController.cs)**:
  - `[P]` toggleable `"Sonar Dhaka OS"` smartphone UI for Story launch, Taxi/CNG Fares, Deliveries, Contracts, Races, Shop purchases, Character Switching (`[Tab]`), Maya Contact calls, Garage calls, and Quick Save/Load alongside bilingual dialogue subtitle banners and mission telemetry.
- **[GulshanHatirjheelWorldBuilder.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Scripts/Editor/GulshanHatirjheelWorldBuilder.cs)**:
  - Integrated complete Part 9 runtime campaign, contacts (`Rafiq Bhai`), Safehouse portals, Smartphone UI, and HUD into `GulshanHatirjheel_Milestone.unity`.

---

## Verification & Offline Blockers
- **Tests Written**: [StoryAndEconomyEditTests.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Tests/EditMode/StoryAndEconomyEditTests.cs) & [OpenWorldGameplayPlayTests.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Tests/PlayMode/OpenWorldGameplayPlayTests.cs).
- **Pending Live Execution**: `Unity.exe` (`6000.6.10f1`) remains unavailable offline due to missing Drive `D:`; ScriptableObject baking, scene rebuild, C# compilation, and NUnit test execution await Unity Editor availability.
