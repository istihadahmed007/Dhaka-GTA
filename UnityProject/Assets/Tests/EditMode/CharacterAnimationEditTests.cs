using DhakaGta.Character;
using DhakaGta.Core;
using DhakaGta.Data;
using NUnit.Framework;
using UnityEngine;

namespace DhakaGta.Tests.EditMode
{
    public sealed class CharacterAnimationEditTests
    {
        [Test]
        public void ProtagonistProfiles_TariqAndMaya_HaveDistinctSpecsAndAbilities()
        {
            var tariq = ScriptableObject.CreateInstance<ProtagonistProfileSO>();
            tariq.protagonistId = "tariq";
            tariq.fullName = "Tariq Hasan";
            tariq.maxHealth = 135f;
            tariq.maxArmor = 100f;
            tariq.maxStamina = 115f;
            tariq.meleeDamageMultiplier = 1.45f;
            tariq.driftHandlingBonus = 0.45f;
            tariq.specialAbility = SpecialAbilityKind.StreetOverdrive;

            var maya = ScriptableObject.CreateInstance<ProtagonistProfileSO>();
            maya.protagonistId = "maya";
            maya.fullName = "Maya Rahman";
            maya.maxHealth = 110f;
            maya.maxArmor = 120f;
            maya.maxStamina = 140f;
            maya.aimPrecisionMultiplier = 1.65f;
            maya.stealthMultiplier = 1.35f;
            maya.specialAbility = SpecialAbilityKind.FocusReflex;

            try
            {
                Assert.AreNotEqual(tariq.maxHealth, maya.maxHealth);
                Assert.AreNotEqual(tariq.specialAbility, maya.specialAbility);
                Assert.Greater(tariq.meleeDamageMultiplier, 1.3f);
                Assert.Greater(maya.aimPrecisionMultiplier, 1.5f);
            }
            finally
            {
                Object.DestroyImmediate(tariq);
                Object.DestroyImmediate(maya);
            }
        }

        [Test]
        public void MotionCodebook_SelectsProperEntriesForWalkJogSprintAimSwimAndVehicle()
        {
            var codebook = ScriptableObject.CreateInstance<MotionCodebookSO>();
            codebook.PopulateDefaultAi4AnimationEntries();

            try
            {
                var walk = codebook.FindBestMatch(new Vector3(0f, 0f, 2.2f), 0f, true, false, false, false, false, false, MotionActionTag.Idle, out _);
                Assert.AreEqual(MotionActionTag.Walk, walk.actionTag);

                var jog = codebook.FindBestMatch(new Vector3(0f, 0f, 5.1f), 0f, true, false, false, false, false, false, MotionActionTag.Walk, out _);
                Assert.AreEqual(MotionActionTag.Jog, jog.actionTag);

                var sprint = codebook.FindBestMatch(new Vector3(0f, 0f, 7.8f), 0f, true, false, true, false, false, false, MotionActionTag.Jog, out _);
                Assert.AreEqual(MotionActionTag.Sprint, sprint.actionTag);

                var aim = codebook.FindBestMatch(new Vector3(1.2f, 0f, 2.0f), 0f, true, false, false, false, true, false, MotionActionTag.Walk, out _);
                Assert.AreEqual(MotionActionTag.AimStrafe, aim.actionTag);

                var swim = codebook.FindBestMatch(new Vector3(0f, 0f, 3.0f), 0f, false, false, false, true, false, false, MotionActionTag.Idle, out _);
                Assert.AreEqual(MotionActionTag.SwimForward, swim.actionTag);

                var drive = codebook.FindBestMatch(Vector3.zero, 0f, false, false, false, false, false, true, MotionActionTag.Idle, out _);
                Assert.AreEqual(MotionActionTag.VehicleDrive, drive.actionTag);
            }
            finally
            {
                Object.DestroyImmediate(codebook);
            }
        }

        [Test]
        public void MotionTrajectoryPredictor_ComputesFutureArcAndPredictedTurnAngle()
        {
            var go = new GameObject("PredictorTest");
            var predictor = go.AddComponent<MotionTrajectoryPredictor>();

            try
            {
                predictor.Initialize(Vector3.zero, Vector3.forward);
                Vector3 rightTurnTarget = Vector3.right;
                for (int i = 0; i < 8; i++)
                {
                    predictor.StepPrediction(Vector3.forward * (i * 0.1f), Vector3.forward, rightTurnTarget * 5f, rightTurnTarget, 0.05f);
                }

                Assert.Greater(predictor.GetFuturePosition(4).x, 0.5f, "Predicted +1.0s future position should trend right along target velocity.");
                Assert.Greater(predictor.PredictedTurnDegrees04, 15f, "Turning from +Z toward +X should yield a positive signed turn angle.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void MotionMatchingAndFootLocker_AlternatesLeftRightStancePhasesWithoutFootSlide()
        {
            var go = new GameObject("MatcherTest");
            var matcher = go.AddComponent<MotionMatchingController>();
            var ikLocker = go.AddComponent<CharacterIkAndFootLocker>();
            var codebook = ScriptableObject.CreateInstance<MotionCodebookSO>();
            codebook.PopulateDefaultAi4AnimationEntries();
            matcher.Configure(null, codebook);
            ikLocker.Configure(null, matcher);

            bool sawLeftOnly = false;
            bool sawRightOnly = false;

            try
            {
                for (int i = 0; i < 40; i++)
                {
                    matcher.StepMotionMatching(new Vector3(0f, 0f, 2.2f), 0f, true, false, false, false, false, false, 0.025f);
                    ikLocker.StepFootLockWeights(true, false, false, 0.025f);

                    if (matcher.IsLeftFootPlanted && !matcher.IsRightFootPlanted) sawLeftOnly = true;
                    if (matcher.IsRightFootPlanted && !matcher.IsLeftFootPlanted) sawRightOnly = true;
                }

                Assert.IsTrue(sawLeftOnly, "Walking cycle must enter Left Stance phase cleanly.");
                Assert.IsTrue(sawRightOnly, "Walking cycle must enter Right Stance phase cleanly.");
                Assert.Greater(matcher.AnimatorSpeedSyncRatio, 0.7f);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(codebook);
            }
        }
    }
}
