using System.Collections.Generic;
using DhakaGta.Character;
using DhakaGta.Combat;
using DhakaGta.Core;
using DhakaGta.Data;
using DhakaGta.Missions;
using DhakaGta.Vehicles;
using NUnit.Framework;
using UnityEngine;

namespace DhakaGta.Tests.EditMode
{
    public sealed class StoryAndEconomyEditTests
    {
        [Test]
        public void EconomyAndInventory_BuyUseAndRelationshipBond_ProgressDeterministically()
        {
            var go = new GameObject("EconomyTest");
            var econ = go.AddComponent<PlayerEconomyAndInventory>();
            var hp = go.AddComponent<HealthComponent>();

            try
            {
                hp.InitializeFromProfile(null);
                econ.ResetStarterEconomy();
                Assert.AreEqual(2500, econ.CurrentBdt);

                bool bought = econ.PurchaseCatalogItem("cha_flask", 2);
                Assert.IsTrue(bought);
                Assert.AreEqual(2500 - 120, econ.CurrentBdt);

                hp.ApplyDamage(40f, Vector3.zero);
                float damagedHp = hp.CurrentHealth;
                bool used = econ.UseInventoryItem("cha_flask", go);
                Assert.IsTrue(used);
                Assert.Greater(hp.CurrentHealth, damagedHp, "Consuming cha_flask should restore health.");

                econ.AddRelationshipBondXp(65);
                Assert.GreaterOrEqual(econ.TariqMayaBondXp, 75);
                Assert.IsTrue(econ.TariqMayaBondTierLabel.Contains("Trusted Allies"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void MissionEngine_RespectsPrerequisitesAndGeoCoordinates()
        {
            var m1 = ScriptableObject.CreateInstance<MissionDefinitionSO>();
            m1.missionId = "story_01";
            m1.title = "Step One";
            m1.structuredSteps = new[]
            {
                new MissionObjectiveStepSpec { stepId = "s1", objectiveType = MissionObjectiveType.GoToLocation, fallbackLocalPosition = new Vector3(10f, 0f, 10f) }
            };

            var m2 = ScriptableObject.CreateInstance<MissionDefinitionSO>();
            m2.missionId = "story_02";
            m2.prerequisiteMissionIds = new[] { "story_01" };

            var go = new GameObject("EnginePreReqTest");
            var engine = go.AddComponent<DhakaMissionEngine>();
            engine.BindSystems(new[] { m1, m2 }, null, null, null, null, null, null, null, null);

            try
            {
                Assert.IsTrue(engine.IsMissionUnlocked(m1));
                Assert.IsFalse(engine.IsMissionUnlocked(m2), "Mission 2 must remain locked before Mission 1 completion.");

                engine.RestoreProgressionState(new List<string> { "story_01" }, null, 0);
                Assert.IsTrue(engine.IsMissionUnlocked(m2), "Completing Story 01 must unlock Story 02.");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(m1);
                Object.DestroyImmediate(m2);
            }
        }

        [Test]
        public void SaveSystem_JsonRoundTrip_PreservesWalletBondGarageAndMissions()
        {
            var go = new GameObject("SaveRoundTripTest");
            var ctrlGo = new GameObject("Protagonist");
            ctrlGo.AddComponent<CharacterController>();
            var ctrl = ctrlGo.AddComponent<ProtagonistController>();
            var hp = ctrlGo.AddComponent<HealthComponent>();
            var econ = go.AddComponent<PlayerEconomyAndInventory>();
            var garage = go.AddComponent<DhakaGarageManager>();
            var missions = go.AddComponent<DhakaMissionEngine>();
            var saver = go.AddComponent<DhakaSaveSystem>();

            try
            {
                hp.InitializeFromProfile(null);
                econ.ResetStarterEconomy();
                econ.AddBdt(8500, "Race Purse");
                econ.AddRelationshipBondXp(55);
                econ.GrantSafehouseUnlock("gulshan_circle_penthouse");
                econ.RacesWonCount = 2;

                var cngSpec = ScriptableObject.CreateInstance<VehicleSpecSO>();
                cngSpec.vehicleId = "cng_auto";
                garage.ConfigureCatalog(new[] { cngSpec }, null);
                garage.GrantVehicleToGarage("cng_auto", Color.green);

                missions.RestoreProgressionState(new List<string> { "story_01_tong" }, null, 0);
                saver.BindDependencies(ctrl, null, hp, econ, garage, missions);

                string json = saver.SerializeToJson();
                Assert.IsFalse(string.IsNullOrEmpty(json));

                // Mutate runtime state completely, then reload from saved JSON
                econ.ResetStarterEconomy();
                missions.RestoreProgressionState(new List<string>(), null, 0);
                Assert.AreEqual(2500, econ.CurrentBdt);

                bool loaded = saver.LoadFromJson(json);
                Assert.IsTrue(loaded);
                Assert.AreEqual(11000, econ.CurrentBdt);
                Assert.AreEqual(65, econ.TariqMayaBondXp);
                Assert.AreEqual(2, econ.RacesWonCount);
                Assert.Contains("gulshan_circle_penthouse", (System.Collections.ICollection)econ.UnlockedSafehouses);
                Assert.AreEqual(1, garage.OwnedFleet.Count);
                Assert.Contains("story_01_tong", new List<string>(missions.CompletedMissionIds));

                Object.DestroyImmediate(cngSpec);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(ctrlGo);
            }
        }
    }
}
