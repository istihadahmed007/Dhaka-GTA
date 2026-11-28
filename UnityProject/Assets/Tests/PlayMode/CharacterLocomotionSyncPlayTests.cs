using System.Collections;
using DhakaGta.Character;
using DhakaGta.Core;
using DhakaGta.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DhakaGta.Tests.PlayMode
{
    public sealed class CharacterLocomotionSyncPlayTests
    {
        [UnityTest]
        public IEnumerator PlayMode_ProtagonistLocomotion_FootLockSwimAndAbilities_SynchronizeCleanly()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "CharTestFloor";
            ground.layer = PhysicsLayers.Ground;
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(80f, 1f, 80f);

            var tariq = ScriptableObject.CreateInstance<ProtagonistProfileSO>();
            tariq.protagonistId = "tariq";
            tariq.fullName = "Tariq Hasan";
            tariq.maxHealth = 135f;
            tariq.maxStamina = 115f;
            tariq.walkSpeedMetersPerSec = 2.4f;
            tariq.jogSpeedMetersPerSec = 5.2f;
            tariq.sprintSpeedMetersPerSec = 7.8f;
            tariq.specialAbility = SpecialAbilityKind.StreetOverdrive;
            tariq.abilityDurationSeconds = 5f;
            tariq.abilityCooldownSeconds = 12f;

            var maya = ScriptableObject.CreateInstance<ProtagonistProfileSO>();
            maya.protagonistId = "maya";
            maya.fullName = "Maya Rahman";
            maya.maxHealth = 110f;
            maya.maxStamina = 140f;
            maya.walkSpeedMetersPerSec = 2.6f;
            maya.jogSpeedMetersPerSec = 5.7f;
            maya.sprintSpeedMetersPerSec = 8.4f;
            maya.specialAbility = SpecialAbilityKind.FocusReflex;
            maya.abilityDurationSeconds = 4f;
            maya.abilityCooldownSeconds = 10f;

            GameObject playerGo = new GameObject("PlayTestProtagonist");
            playerGo.transform.position = new Vector3(0f, 0.12f, 0f);
            var cc = playerGo.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.34f;
            cc.center = new Vector3(0f, 0.9f, 0f);

            var controller = playerGo.AddComponent<ProtagonistController>();
            controller.Configure(tariq, null, null);
            controller.SetSyntheticMoveInput(Vector2.up, sprint: true);

            GameObject waterGo = null;

            try
            {
                float elapsed = 0f;
                while (elapsed < 0.35f)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                // 1. Verify Sprint Trajectory + Codebook Matching + Phase Advance + Foot Lock IK
                Assert.Greater(playerGo.transform.position.z, 0.8f, "Sprinting protagonist should advance rapidly along +Z.");
                Assert.AreEqual(MotionActionTag.Sprint, controller.MotionMatcher.CurrentActionTag);
                Assert.Greater(controller.TrajectoryPredictor.CurrentHorizontalSpeed, 4.5f);
                Assert.IsFalse(float.IsNaN(controller.IkLocker.LeftFootLockWeight));
                Assert.IsFalse(float.IsNaN(controller.IkLocker.RightFootLockWeight));

                // 2. Verify Tariq's Street Overdrive ability boosts speed & state cleanly
                bool activatedTariq = controller.AbilityController.TryActivateAbility();
                Assert.IsTrue(activatedTariq);
                Assert.AreEqual(SpecialAbilityKind.StreetOverdrive, CharacterAbilityEvents.CurrentAbilityState.AbilityKind);
                Assert.Greater(controller.AbilityController.CurrentSpeedMultiplier, 1.05f);

                // 3. Verify Aiming overrides to AimStrafe and elevates IK Aim weight
                controller.SetSyntheticAimState(true, playerGo.transform.position + Vector3.forward * 10f + Vector3.up * 1.4f);
                yield return null;
                yield return null;
                Assert.AreEqual(MotionActionTag.AimStrafe, controller.MotionMatcher.CurrentActionTag);
                Assert.Greater(controller.IkLocker.CurrentAimWeight, 0.05f);
                controller.SetSyntheticAimState(false, Vector3.zero);

                // 4. Verify Swimming transition when entering Hatirjheel water volume (>1.15m depth)
                waterGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                waterGo.name = "TestHatirjheelWater";
                waterGo.layer = PhysicsLayers.Water;
                waterGo.GetComponent<Collider>().isTrigger = true;
                waterGo.transform.position = playerGo.transform.position + new Vector3(0f, 0.25f, 0f);
                waterGo.transform.localScale = new Vector3(20f, 3.0f, 20f);

                float swimElapsed = 0f;
                while (swimElapsed < 0.25f && !controller.IsSwimming)
                {
                    swimElapsed += Time.deltaTime;
                    yield return null;
                }

                Assert.IsTrue(controller.IsSwimming, "Character submerged past threshold must enter Swimming state.");
                Assert.IsTrue(
                    controller.MotionMatcher.CurrentActionTag == MotionActionTag.SwimForward ||
                    controller.MotionMatcher.CurrentActionTag == MotionActionTag.SwimIdle);

                // 5. Verify Maya Switch + FocusReflex TimeScale dilation + clean restore
                controller.Configure(maya, null, null);
                bool activatedMaya = controller.AbilityController.TryActivateAbility();
                Assert.IsTrue(activatedMaya);
                Assert.Less(Time.timeScale, 0.85f, "FocusReflex must dial Time.timeScale cleanly.");

                controller.AbilityController.ForceStopAbility();
                Assert.AreEqual(1f, Time.timeScale, 0.01f, "Ending FocusReflex must restore Time.timeScale cleanly to 1.0.");
            }
            finally
            {
                Time.timeScale = 1f;
                Time.fixedDeltaTime = 0.02f;
                controller.ClearSyntheticInput();
                if (waterGo != null) Object.Destroy(waterGo);
                Object.Destroy(playerGo);
                Object.Destroy(ground);
                Object.Destroy(tariq);
                Object.Destroy(maya);
            }
        }
    }
}
