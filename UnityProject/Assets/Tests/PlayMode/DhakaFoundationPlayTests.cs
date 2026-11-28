using System.Collections;
using DhakaGta.Character;
using DhakaGta.Combat;
using DhakaGta.Core;
using DhakaGta.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.TestTools;

namespace DhakaGta.Tests.PlayMode
{
    public sealed class DhakaFoundationPlayTests
    {
        [UnityTest]
        public IEnumerator PlayMode_HdrpAndProtagonistLocomotion_OperateWithoutErrors()
        {
            Assert.IsInstanceOf<HDRenderPipelineAsset>(GraphicsSettings.currentRenderPipeline);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "TestGroundFloor";
            ground.layer = PhysicsLayers.Ground;
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(40f, 1f, 40f);

            var profile = ScriptableObject.CreateInstance<ProtagonistProfileSO>();
            profile.protagonistId = "tariq";
            profile.fullName = "Tariq Hasan";
            profile.maxHealth = 135f;
            profile.maxStamina = 115f;
            profile.jogSpeedMetersPerSec = 5.2f;

            GameObject playerGo = new GameObject("TestProtagonist");
            playerGo.transform.position = new Vector3(0f, 0.1f, 0f);
            var cc = playerGo.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.32f;
            cc.center = new Vector3(0f, 0.9f, 0f);

            var controller = playerGo.AddComponent<ProtagonistController>();
            controller.Configure(profile, null, null);
            controller.SetSyntheticMoveInput(Vector2.up);

            var health = playerGo.AddComponent<HealthComponent>();
            health.InitializeFromProfile(profile);

            try
            {
                yield return null;
                float elapsed = 0f;
                while (elapsed < 0.25f)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                Vector3 finalPos = playerGo.transform.position;
                Assert.IsFalse(float.IsNaN(finalPos.x) || float.IsNaN(finalPos.y) || float.IsNaN(finalPos.z));
                Assert.Greater(finalPos.z, 0.35f, "ProtagonistController must move forward cleanly under synthetic input.");
                Assert.AreEqual(PhysicsLayers.Player, playerGo.layer);
                Assert.AreEqual(135f, health.MaxHealth, 0.001f);
            }
            finally
            {
                controller.ClearSyntheticInput();
                Object.Destroy(playerGo);
                Object.Destroy(ground);
                Object.Destroy(profile);
            }
        }
    }
}
