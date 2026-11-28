using System.Collections.Generic;
using DhakaGta.Character;
using DhakaGta.Combat;
using DhakaGta.Data;
using DhakaGta.Environment;
using DhakaGta.GIS;
using DhakaGta.Missions;
using DhakaGta.TrafficAI;
using DhakaGta.UI;
using DhakaGta.Vehicles;
using UnityEditor;
using UnityEngine;

namespace DhakaGta.Editor
{
    /// <summary>
    /// Part 9 Story, Side Hustles & Safehouse ScriptableObject Factory and Scene Integrator.
    /// Generates an original interconnected Tariq & Maya Bangladesh campaign across real Dhaka landmarks.
    /// </summary>
    public static class DhakaStoryContentBuilder
    {
        public const string MissionsSoDir = "Assets/ScriptableObjects/Missions";
        public const string SafehousesSoDir = "Assets/ScriptableObjects/Safehouses";

        [MenuItem("Dhaka-GTA/Part 9/Build Dhaka Story, Safehouses & Open-World Assets")]
        public static void BuildAllStoryAssetsMenu()
        {
            BuildStoryMissions();
            BuildSafehouses();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DhakaStoryContentBuilder] Generated 6 Story Missions, 2 Side Quests, and 4 Dhaka Safehouses.");
        }

        public static MissionDefinitionSO[] BuildStoryMissions()
        {
            EnsureDir(MissionsSoDir);
            var list = new List<MissionDefinitionSO>();

            // 1. First Cup at Tong (Tariq)
            var m1 = LoadOrCreate<MissionDefinitionSO>($"{MissionsSoDir}/Mission_01_FirstCupAtTong.asset");
            m1.missionId = "story_01_tong";
            m1.title = "First Cup at Tong";
            m1.banglaTitle = "চায়ের দোকানের বার্তা";
            m1.category = MissionCategory.Story;
            m1.protagonistFilter = "tariq";
            m1.startDistrictId = "gulshan";
            m1.rewardBdt = 12000;
            m1.relationshipBondXp = 20;
            m1.unlockWeaponId = "service_9mm";
            m1.wantedRiskStars = 1;
            m1.objectiveSummary = "Meet Rafiq Bhai at Gulshan Tong Cha, recover the extortion ledger, and repel Padma Syndicate enforcers.";
            m1.structuredSteps = new[]
            {
                new MissionObjectiveStepSpec
                {
                    stepId = "m1_talk_rafiq", titleEnglish = "Talk to Rafiq Bhai", titleBangla = "রফিক ভাইয়ের সাথে কথা বলুন",
                    objectivePrompt = "Speak with Rafiq Bhai beside the Gulshan Tong Tea Stall [E]",
                    objectiveType = MissionObjectiveType.InteractWithNpc,
                    targetDistrictId = "gulshan", targetLatitude = 23.7805, targetLongitude = 90.4167,
                    fallbackLocalPosition = new Vector3(4.5f, 0.1f, 2.5f), triggerRadiusMeters = 5f,
                    dialogueSpeaker = "Rafiq Bhai (রফিক ভাই)",
                    dialogueBangla = "তারেক! পদ্মা সিন্ডিকেটের লোকেরা গুলশানের দোকানদারদের ভয় দেখাচ্ছে। মায়া তোমাকে সাহায্য করতে চায়।",
                    dialogueEnglish = "Tariq! Padma Syndicate enforcers are threatening Gulshan stalls. Maya says she has telemetry ready."
                },
                new MissionObjectiveStepSpec
                {
                    stepId = "m1_clear_enforcers", titleEnglish = "Drive Off Syndicate Enforcers", titleBangla = "সিন্ডিকেট গুন্ডাদের হটান",
                    objectivePrompt = "Eliminate the 3 Padma Syndicate enforcers nearby",
                    objectiveType = MissionObjectiveType.EliminateTargets,
                    requiredEliminations = 3,
                    targetDistrictId = "gulshan", targetLatitude = 23.7809, targetLongitude = 90.4171,
                    fallbackLocalPosition = new Vector3(18f, 0.1f, -12f), triggerRadiusMeters = 12f
                }
            };
            EditorUtility.SetDirty(m1);
            list.Add(m1);

            // 2. Night Signal at Hatirjheel (Maya Switch + Stealth/Evade)
            var m2 = LoadOrCreate<MissionDefinitionSO>($"{MissionsSoDir}/Mission_02_MayaSignalIntercept.asset");
            m2.missionId = "story_02_maya_signal";
            m2.title = "Night Signal at Hatirjheel";
            m2.banglaTitle = "মায়ার গোপন সংকেত";
            m2.category = MissionCategory.Story;
            m2.protagonistFilter = "maya";
            m2.startDistrictId = "hatirjheel";
            m2.prerequisiteMissionIds = new[] { "story_01_tong" };
            m2.rewardBdt = 16500;
            m2.relationshipBondXp = 30;
            m2.wantedRiskStars = 2;
            m2.objectiveSummary = "Switch to Maya Rahman [Tab], intercept Syndicate ledger traffic at Hatirjheel Promenade, and shake DMP sweeps.";
            m2.structuredSteps = new[]
            {
                new MissionObjectiveStepSpec
                {
                    stepId = "m2_switch_maya", titleEnglish = "Switch to Maya Rahman", titleBangla = "মায়ার ভূমিকায় আসুন",
                    objectivePrompt = "Press [Tab] to switch control to Maya Rahman",
                    objectiveType = MissionObjectiveType.SwitchProtagonistStep, requiredProtagonistId = "maya",
                    dialogueSpeaker = "Maya Rahman (মায়া)",
                    dialogueBangla = "তারেক, এই এনক্রিপ্টেড সিগন্যালটা আমি ডিকোড করছি—আমাকে হাতিরঝিল ব্রিজে কভার দাও।",
                    dialogueEnglish = "Tariq, I'm intercepting their courier transponder near Hatirjheel Bridge—watch my back."
                },
                new MissionObjectiveStepSpec
                {
                    stepId = "m2_promenade_node", titleEnglish = "Hack Hatirjheel Relay", titleBangla = "হাতিরঝিল রিলে স্ক্যান",
                    objectivePrompt = "Reach Hatirjheel Overpass Promenade",
                    objectiveType = MissionObjectiveType.GoToLocation,
                    targetDistrictId = "hatirjheel", targetLatitude = 23.7582, targetLongitude = 90.4095,
                    fallbackLocalPosition = new Vector3(-40f, 0.1f, -120f), triggerRadiusMeters = 12f
                },
                new MissionObjectiveStepSpec
                {
                    stepId = "m2_evade_dmp", titleEnglish = "Evade Patrol Sweep", titleBangla = "পুলিশের তল্লাশি এড়িয়ে যান",
                    objectivePrompt = "Shake DMP Police pursuit until area is CLEAR",
                    objectiveType = MissionObjectiveType.EvadePoliceWanted
                }
            };
            EditorUtility.SetDirty(m2);
            list.Add(m2);

            // 3. Buriganga Cargo Heist
            var m3 = LoadOrCreate<MissionDefinitionSO>($"{MissionsSoDir}/Mission_03_BurigangaSteelRun.asset");
            m3.missionId = "story_03_buriganga_run";
            m3.title = "Buriganga Steel Run";
            m3.banglaTitle = "বুড়িগঙ্গার চালান";
            m3.category = MissionCategory.Story;
            m3.protagonistFilter = "tariq";
            m3.startDistrictId = "sadarghat";
            m3.prerequisiteMissionIds = new[] { "story_02_maya_signal" };
            m3.rewardBdt = 24000;
            m3.relationshipBondXp = 25;
            m3.unlockWeaponId = "micro_smg";
            m3.failOnTimerExpired = true;
            m3.objectiveSummary = "Seize Syndicate armored truck down at Sadarghat Launch Ghat and bring proof back to Gulshan Garage.";
            m3.structuredSteps = new[]
            {
                new MissionObjectiveStepSpec
                {
                    stepId = "m3_sadarghat", titleEnglish = "Reach Sadarghat Pier", titleBangla = "সদরঘাট টার্মিনালে যান",
                    objectivePrompt = "Travel south toward Buriganga Launch Terminal",
                    objectiveType = MissionObjectiveType.GoToLocation,
                    targetDistrictId = "sadarghat", targetLatitude = 23.7073, targetLongitude = 90.4071,
                    fallbackLocalPosition = new Vector3(0f, 0.1f, 180f), triggerRadiusMeters = 14f
                },
                new MissionObjectiveStepSpec
                {
                    stepId = "m3_deliver", titleEnglish = "Deliver Vehicle to Gulshan", titleBangla = "গাড়িটি গুলশান গ্যারেজে আনুন",
                    objectivePrompt = "Drive a vehicle safely to Gulshan Customs Bay before deadline",
                    objectiveType = MissionObjectiveType.StealAndDeliverVehicle, timeLimitSeconds = 180f,
                    targetDistrictId = "gulshan", targetLatitude = 23.7805, targetLongitude = 90.4167,
                    fallbackLocalPosition = new Vector3(24f, 0.1f, -26f), triggerRadiusMeters = 12f
                }
            };
            EditorUtility.SetDirty(m3);
            list.Add(m3);

            // 4. Hatirjheel Midnight Dash
            var m4 = LoadOrCreate<MissionDefinitionSO>($"{MissionsSoDir}/Mission_04_HatirjheelRace.asset");
            m4.missionId = "story_04_hatirjheel_race";
            m4.title = "Hatirjheel Midnight Dash";
            m4.banglaTitle = "হাতিরঝিল রাতের রেস";
            m4.category = MissionCategory.Story;
            m4.protagonistFilter = "";
            m4.startDistrictId = "hatirjheel";
            m4.prerequisiteMissionIds = new[] { "story_03_buriganga_run" };
            m4.rewardBdt = 28000;
            m4.relationshipBondXp = 30;
            m4.failOnTimerExpired = true;
            m4.objectiveSummary = "Beat Syndicate street racers along Hatirjheel & Gulshan Circle-1 checkpoints.";
            m4.structuredSteps = new[]
            {
                new MissionObjectiveStepSpec
                {
                    stepId = "m4_race_loop", titleEnglish = "Win Hatirjheel Sprint", titleBangla = "চেকপয়েন্ট রেস জয় করুন",
                    objectivePrompt = "Clear all 4 high-speed checkpoints around Gulshan-Hatirjheel",
                    objectiveType = MissionObjectiveType.CompleteRaceCheckpoints, timeLimitSeconds = 150f,
                    raceCheckpointsLocal = new[]
                    {
                        new Vector3(0f, 0.1f, 45f), new Vector3(65f, 0.1f, 55f),
                        new Vector3(85f, 0.1f, -30f), new Vector3(15f, 0.1f, -45f)
                    },
                    dialogueSpeaker = "Tariq Hasan (তারেক)",
                    dialogueBangla = "মায়া, স্ট্রিট ওভারড্রাইভ চালু করছি—আজ ওদের সব রুট আমাদের দখলে নেবো!",
                    dialogueEnglish = "Maya, kicking Street Overdrive—we're taking every smuggling corridor tonight!"
                }
            };
            EditorUtility.SetDirty(m4);
            list.Add(m4);

            // 5. Dawn Over Dhaka Finale
            var m5 = LoadOrCreate<MissionDefinitionSO>($"{MissionsSoDir}/Mission_05_DawnOverDhaka.asset");
            m5.missionId = "story_05_dawn_dhaka";
            m5.title = "Dawn Over Dhaka";
            m5.banglaTitle = "ঢাকার নতুন ভোর";
            m5.category = MissionCategory.Story;
            m5.protagonistFilter = "";
            m5.startDistrictId = "shapla_crossing";
            m5.prerequisiteMissionIds = new[] { "story_04_hatirjheel_race" };
            m5.rewardBdt = 50000;
            m5.relationshipBondXp = 40;
            m5.unlockSafehouseId = "gulshan_circle_penthouse";
            m5.wantedRiskStars = 3;
            m5.objectiveSummary = "Tariq & Maya unite at Motijheel / Shapla Crossing to dismantle Padma Syndicate command permanently.";
            m5.structuredSteps = new[]
            {
                new MissionObjectiveStepSpec
                {
                    stepId = "m5_clear_boss", titleEnglish = "Dismantle Syndicate High Command", titleBangla = "সিন্ডিকেট কমান্ড ধ্বংস করুন",
                    objectivePrompt = "Eliminate the 5 Syndicate leaders guarding Shapla Monument Plaza",
                    objectiveType = MissionObjectiveType.EliminateTargets, requiredEliminations = 5,
                    targetDistrictId = "shapla_crossing", targetLatitude = 23.733015, targetLongitude = 90.417014,
                    fallbackLocalPosition = new Vector3(0f, 0.1f, 0f), triggerRadiusMeters = 18f,
                    dialogueSpeaker = "Tariq & Maya",
                    dialogueBangla = "আজ থেকে ঢাকার রাস্তায় আর কোনো সিন্ডিকেটের চাঁদাবাজি চলবে না!",
                    dialogueEnglish = "Starting today, nobody taxes the streets of Dhaka ever again!"
                }
            };
            EditorUtility.SetDirty(m5);
            list.Add(m5);

            // 6. Side Quest: Chawkbazar Spice Escort
            var sq1 = LoadOrCreate<MissionDefinitionSO>($"{MissionsSoDir}/SideQuest_01_ChawkbazarTeaAid.asset");
            sq1.missionId = "side_01_tea_supply";
            sq1.title = "Tong Cha Morning Supply";
            sq1.banglaTitle = "চা-স্টলের জরুরি সরবরাহ";
            sq1.category = MissionCategory.SideQuest;
            sq1.startDistrictId = "old_dhaka";
            sq1.rewardBdt = 3200;
            sq1.relationshipBondXp = 10;
            sq1.failOnTimerExpired = true;
            sq1.objectiveSummary = "Help local Dhaka tea stall vendors deliver cardamom tea sacks before morning rush.";
            sq1.structuredSteps = new[]
            {
                new MissionObjectiveStepSpec
                {
                    stepId = "sq1_drop", titleEnglish = "Deliver Supply Crate", titleBangla = "মসলার বস্তা পৌঁছে দিন",
                    objectivePrompt = "Deliver crate to Gulshan Mama Tea Stall",
                    objectiveType = MissionObjectiveType.DeliverPackageTimed, timeLimitSeconds = 120f,
                    fallbackLocalPosition = new Vector3(4.5f, 0.1f, 2.5f), triggerRadiusMeters = 7f
                }
            };
            EditorUtility.SetDirty(sq1);
            list.Add(sq1);

            return list.ToArray();
        }

        public static SafehouseDefinitionSO[] BuildSafehouses()
        {
            EnsureDir(SafehousesSoDir);

            SafehouseDefinitionSO MakeSh(string assetName, string id, string en, string bn, string dist, double lat, double lon, Vector3 local, int cost, bool defOwned)
            {
                var sh = LoadOrCreate<SafehouseDefinitionSO>($"{SafehousesSoDir}/{assetName}.asset");
                sh.safehouseId = id;
                sh.displayName = en;
                sh.banglaName = bn;
                sh.districtId = dist;
                sh.latitude = lat;
                sh.longitude = lon;
                sh.fallbackLocalPosition = local;
                sh.purchaseCostBdt = cost;
                sh.unlockedByDefault = defOwned;
                EditorUtility.SetDirty(sh);
                return sh;
            }

            return new[]
            {
                MakeSh("Safehouse_OldDhakaRooftop", "old_dhaka_rooftop", "Chawkbazar Rooftop Pad", "চকবাজার ছাদের বাসা", "old_dhaka", 23.7198, 90.3982, new Vector3(-28f, 0.1f, 36f), 0, true),
                MakeSh("Safehouse_GulshanPenthouse", "gulshan_circle_penthouse", "Gulshan Circle-2 Loft", "গুলশান পেন্টহাউস", "gulshan", 23.7945, 90.4143, new Vector3(-12f, 0.1f, -24f), 25000, false),
                MakeSh("Safehouse_DhanmondiStudio", "dhanmondi_lake_studio", "Dhanmondi Lake Studio", "ধানমন্ডি লেকভিউ", "dhanmondi", 23.7465, 90.3760, new Vector3(-140f, 0.1f, -16f), 18000, false),
                MakeSh("Safehouse_MotijheelFlat", "motijheel_crash_flat", "Motijheel Corner Flat", "মতিঝিল কর্নার ফ্ল্যাট", "motijheel", 23.7332, 90.4192, new Vector3(110f, 0.1f, 18f), 14000, false)
            };
        }

        public static void AttachOpenWorldGameplaySystems(
            GameObject systemsGo,
            Transform worldRoot,
            ProtagonistController protagonistCtrl,
            ProtagonistSwitcher switcher,
            ProtagonistInteractionScanner scanner,
            HealthComponent playerHealth,
            DhakaGarageManager garageMgr,
            WorldCoordinateManager coordMgr,
            Vector3 playerSpawnPos)
        {
            MissionDefinitionSO[] stories = BuildStoryMissions();
            SafehouseDefinitionSO[] safehouses = BuildSafehouses();

            var econ = systemsGo.GetComponent<PlayerEconomyAndInventory>() ?? systemsGo.AddComponent<PlayerEconomyAndInventory>();
            econ.ResetStarterEconomy();

            var saver = systemsGo.GetComponent<DhakaSaveSystem>() ?? systemsGo.AddComponent<DhakaSaveSystem>();
            var missionEngine = systemsGo.GetComponent<DhakaMissionEngine>() ?? systemsGo.AddComponent<DhakaMissionEngine>();
            var registry = systemsGo.GetComponent<MissionRegistry>() ?? systemsGo.AddComponent<MissionRegistry>();
            registry.SetMissions(stories);

            missionEngine.BindSystems(stories, protagonistCtrl, switcher, scanner, playerHealth, econ, garageMgr, coordMgr, saver);
            saver.BindDependencies(protagonistCtrl, switcher, playerHealth, econ, garageMgr, missionEngine);

            var safehouseRoot = new GameObject("Dhaka_Safehouses");
            safehouseRoot.transform.SetParent(worldRoot, false);
            var safehouseMgr = systemsGo.GetComponent<SafehouseManager>() ?? systemsGo.AddComponent<SafehouseManager>();

            // Position first safehouse conveniently near Gulshan spawn for immediate interaction
            if (safehouses.Length > 0 && safehouses[0] != null)
            {
                safehouses[0].fallbackLocalPosition = playerSpawnPos + new Vector3(-8f, 0.1f, -4f);
            }
            safehouseMgr.ConfigureAndSpawnPortals(safehouses, econ, saver, safehouseRoot.transform);

            // Interactive Story Contact NPC ("Rafiq Bhai") beside player spawn / Tea Stall
            var contactGo = new GameObject("StoryNpc_RafiqBhai");
            contactGo.transform.SetParent(worldRoot, false);
            contactGo.transform.position = playerSpawnPos + new Vector3(3.2f, 0.08f, 3.8f);
            var contactNpc = contactGo.AddComponent<MissionContactNpc>();
            contactNpc.ConfigureContact(
                "rafiq_bhai",
                "Rafiq Bhai (রফিক ভাই)",
                PedestrianWardrobeArchetype.PanjabiAndPajama,
                new[]
                {
                    new BilingualDialogueLine
                    {
                        Speaker = "Rafiq Bhai (রফিক ভাই)",
                        TextBangla = "আয় তারেক, এক কাপ গরম চা খেয়ে নে! সিন্ডিকেটের খবর আছে।",
                        TextEnglish = "Come Tariq, grab hot cha! I have fresh word on the Padma Syndicate.",
                        DisplaySeconds = 5f
                    }
                },
                missionEngine,
                stories.Length > 0 ? stories[0] : null);

            // Runtime Smartphone & Story HUD overlays
            var phoneCtrl = systemsGo.GetComponent<DhakaSmartphoneController>() ?? systemsGo.AddComponent<DhakaSmartphoneController>();
            phoneCtrl.ConfigureAndBuildUi(missionEngine, econ, saver, protagonistCtrl, switcher, garageMgr);

            var storyHud = systemsGo.GetComponent<DhakaOpenWorldHudController>() ?? systemsGo.AddComponent<DhakaOpenWorldHudController>();
            storyHud.ConfigureAndBuildHud(protagonistCtrl, playerHealth, econ, missionEngine);
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        private static void EnsureDir(string dirPath)
        {
            string[] parts = dirPath.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{cur}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
