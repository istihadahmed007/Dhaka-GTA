using System.Collections;
using DhakaGta.Character;
using DhakaGta.Combat;
using DhakaGta.Core;
using DhakaGta.Data;
using DhakaGta.Missions;
using DhakaGta.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DhakaGta.Tests.PlayMode
{
    public sealed class OpenWorldGameplayPlayTests
    {
        [UnityTest]
        public IEnumerator PlayMode_StoryMissions_NpcDialogueSwitchTargetsRaceSafehouseAndFailTimer_WorkEndToEnd()
        {
            CityDangerBus.ClearAllForReset();

            var root = new GameObject("PlayTestOpenWorldRoot");
            var playerGo = new GameObject("TestProtagonist");
            playerGo.transform.position = Vector3.zero;
            playerGo.AddComponent<CharacterController>();
            var ctrl = playerGo.AddComponent<ProtagonistController>();
            var hp = playerGo.AddComponent<HealthComponent>();
            var switcher = playerGo.AddComponent<ProtagonistSwitcher>();

            var tariq = ScriptableObject.CreateInstance<ProtagonistProfileSO>();
            tariq.protagonistId = "tariq";
            tariq.fullName = "Tariq Hasan";
            tariq.maxHealth = 135f;

            var maya = ScriptableObject.CreateInstance<ProtagonistProfileSO>();
            maya.protagonistId = "maya";
            maya.fullName = "Maya Rahman";
            maya.maxHealth = 110f;

            switcher.Configure(ctrl, null, tariq, maya, null, null);
            hp.InitializeFromProfile(tariq);

            var econ = root.AddComponent<PlayerEconomyAndInventory>();
            econ.ResetStarterEconomy();
            var saver = root.AddComponent<DhakaSaveSystem>();
            var engine = root.AddComponent<DhakaMissionEngine>();
            var shMgr = root.AddComponent<SafehouseManager>();
            var phone = root.AddComponent<DhakaSmartphoneController>();

            // Connected 3-Step Mission: Talk to Rafiq -> Switch to Maya -> Eliminate 2 Syndicate Targets
            var story = ScriptableObject.CreateInstance<MissionDefinitionSO>();
            story.missionId = "play_story_01";
            story.title = "Gulshan Ledger";
            story.banglaTitle = "গুলশান অভিযান";
            story.rewardBdt = 5000;
            story.relationshipBondXp = 35;
            story.unlockWeaponId = "service_9mm";
            story.structuredSteps = new[]
            {
                new MissionObjectiveStepSpec
                {
                    stepId = "step_npc",
                    objectiveType = MissionObjectiveType.InteractWithNpc,
                    fallbackLocalPosition = new Vector3(1.5f, 0f, 1.5f),
                    dialogueSpeaker = "Rafiq Bhai",
                    dialogueBangla = "সতর্ক থাকো!",
                    dialogueEnglish = "Stay sharp!"
                },
                new MissionObjectiveStepSpec
                {
                    stepId = "step_switch",
                    objectiveType = MissionObjectiveType.SwitchProtagonistStep,
                    requiredProtagonistId = "maya"
                },
                new MissionObjectiveStepSpec
                {
                    stepId = "step_combat",
                    objectiveType = MissionObjectiveType.EliminateTargets,
                    requiredEliminations = 2,
                    fallbackLocalPosition = new Vector3(12f, 0f, 12f)
                }
            };

            bool observedDialogue = false;
            bool observedCompletion = false;
            bool observedFailure = false;
            void OnDlg(BilingualDialogueLine l) { if (!string.IsNullOrEmpty(l.TextBangla)) observedDialogue = true; }
            void OnComp(MissionDefinitionSO m) { if (m != null && m.missionId == "play_story_01") observedCompletion = true; }
            void OnFail(MissionDefinitionSO m, string _) { observedFailure = true; }

            MissionObjectiveEvents.OnDialogueShown += OnDlg;
            MissionObjectiveEvents.OnMissionCompleted += OnComp;
            MissionObjectiveEvents.OnMissionFailed += OnFail;

            engine.BindSystems(new[] { story }, ctrl, switcher, null, hp, econ, null, null, saver);
            saver.BindDependencies(ctrl, switcher, hp, econ, null, engine);
            phone.ConfigureAndBuildUi(engine, econ, saver, ctrl, switcher, null);

            var timedJob = ScriptableObject.CreateInstance<MissionDefinitionSO>();
            timedJob.missionId = "fail_timer_test";
            timedJob.failOnTimerExpired = true;
            timedJob.structuredSteps = new[]
            {
                new MissionObjectiveStepSpec
                {
                    stepId = "impossible_far",
                    objectiveType = MissionObjectiveType.GoToLocation,
                    fallbackLocalPosition = new Vector3(800f, 0f, 800f),
                    timeLimitSeconds = 0.08f
                }
            };

            var shDef = ScriptableObject.CreateInstance<SafehouseDefinitionSO>();
            shDef.safehouseId = "test_penthouse";
            shDef.displayName = "Test Loft";
            shDef.purchaseCostBdt = 1000;
            shDef.fallbackLocalPosition = Vector3.zero;

            try
            {
                Assert.IsTrue(engine.StartMission("play_story_01"));
                yield return null;

                Assert.IsTrue(observedDialogue, "Starting story mission must broadcast bilingual Bengali+English dialogue.");

                // Step 0: Interact with spawned MissionContactNpc
                var spawnedNpc = Object.FindFirstObjectByType<MissionContactNpc>();
                Assert.NotNull(spawnedNpc);
                playerGo.transform.position = spawnedNpc.transform.position;
                spawnedNpc.Interact(playerGo);
                yield return null;

                // Step 1: Switch Tariq -> Maya
                Assert.AreEqual(1, engine.CurrentStepIndex);
                switcher.ToggleProtagonist();
                yield return null;
                yield return null;

                // Step 2: Eliminate spawned targets
                Assert.AreEqual(2, engine.CurrentStepIndex);
                var targets = Object.FindObjectsByType<MissionTargetActor>(FindObjectsSortMode.None);
                Assert.AreEqual(2, targets.Length, "EliminateTargets must dynamically spawn hostile actors.");
                foreach (var t in targets) t.ForceDefeatForTest();
                yield return null;
                yield return null;

                Assert.IsTrue(observedCompletion, "Completing all steps must fire OnMissionCompleted.");
                Assert.AreEqual(7500, econ.CurrentBdt, "Completing mission must reward ৳5,000 BDT.");
                Assert.AreEqual(45, econ.TariqMayaBondXp, "Mission completion must advance Tariq-Maya relationship bond.");
                Assert.Contains("service_9mm", (System.Collections.ICollection)econ.UnlockedWeapons);

                // Safehouse purchase & smartphone toggle test
                shMgr.ConfigureAndSpawnPortals(new[] { shDef }, econ, saver, root.transform);
                shMgr.ActivePortals[0].Interact(playerGo);
                Assert.AreEqual(6500, econ.CurrentBdt, "Buying safehouse must deduct purchase cost and save cleanly.");
                Assert.IsTrue(shMgr.ActivePortals[0].IsOwned());

                phone.TogglePhone();
                Assert.IsTrue(phone.IsPhoneOpen);
                phone.ActionLaunchStreetRace();
                Assert.IsFalse(phone.IsPhoneOpen, "Launching a hustle from Sonar Dhaka OS must dismiss the phone overlay.");
                Assert.IsTrue(engine.IsMissionActive && engine.ActiveMission.category == MissionCategory.StreetRace);

                // Failure Condition test: Timer expiry
                engine.StartMissionAsset(timedJob);
                yield return new WaitForSeconds(0.15f);
                Assert.IsTrue(observedFailure, "Expired mission timer must fail active mission cleanly.");
            }
            finally
            {
                MissionObjectiveEvents.OnDialogueShown -= OnDlg;
                MissionObjectiveEvents.OnMissionCompleted -= OnComp;
                MissionObjectiveEvents.OnMissionFailed -= OnFail;
                CityDangerBus.ClearAllForReset();
                Object.Destroy(root);
                Object.Destroy(playerGo);
                Object.Destroy(tariq);
                Object.Destroy(maya);
                Object.Destroy(story);
                Object.Destroy(timedJob);
                Object.Destroy(shDef);
            }
        }
    }
}
