using System.Collections;
using DhakaGta.Character;
using DhakaGta.Combat;
using DhakaGta.Core;
using DhakaGta.Data;
using DhakaGta.Environment;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;

namespace DhakaGta.Tests.PlayMode
{
    public sealed class EnvironmentInteractionPlayTests
    {
        [UnityTest]
        public IEnumerator PlayMode_ProtagonistInteractsWithDhakaTeaStall_RestoresHealthAndFiresFeedback()
        {
            var mats = new DhakaHeroArchitectureBuilder.MaterialPack
            {
                CorrugatedTinMat = new Material(Shader.Find("Standard")),
                WoodMat = new Material(Shader.Find("Standard")),
                DarkSteelMat = new Material(Shader.Find("Standard")),
            };

            var root = new GameObject("PlayTest_InteractionRoot");
            var profile = ScriptableObject.CreateInstance<ProtagonistProfileSO>();
            profile.protagonistId = "tariq";
            profile.maxHealth = 135f;
            profile.maxStamina = 115f;

            GameObject player = new GameObject("TestTariq");
            player.transform.position = new Vector3(0f, 0f, 0f);
            player.AddComponent<CharacterController>();
            var ctrl = player.AddComponent<ProtagonistController>();
            ctrl.Configure(profile, null, null);
            var health = player.AddComponent<HealthComponent>();
            health.InitializeFromProfile(profile);
            health.ApplyDamage(40f, Vector3.zero); // 95 HP

            var scanner = player.AddComponent<ProtagonistInteractionScanner>();
            scanner.Configure(player, null, null);

            GameObject stall = DhakaHeroArchitectureBuilder.CreateTeaStall(
                new Vector3(1.8f, 0f, 0f), 0f, "Test_Hatirjheel_Cha_Stall", mats, root.transform);
            var teaProp = stall.GetComponent<InteractiveEnvironmentProp>();

            string observedFeedback = string.Empty;
            InteractionEvents.OnInteractionFeedback += msg => observedFeedback = msg;

            try
            {
                yield return null;
                bool triggered = scanner.TryTriggerNearestInteractable();
                Assert.IsTrue(triggered, "Scanner must detect nearby Tong tea stall inside 3.6m radius.");
                Assert.AreEqual(1, teaProp.UsageCount);
                Assert.AreEqual(115f, health.CurrentHealth, 0.01f, "Drinking spiced cha should heal +20 HP (95 -> 115).");
                Assert.IsTrue(observedFeedback.Contains("doodh-cha"), "Expected tea stall feedback toast event.");
            }
            finally
            {
                Object.Destroy(player);
                Object.Destroy(root);
                Object.Destroy(profile);
            }
        }
    }
}
