using System.Collections.Generic;
using DhakaGta.Core;
using DhakaGta.GIS;
using DhakaGta.TrafficAI;
using DhakaGta.WorldGen;
using NUnit.Framework;
using UnityEngine;

namespace DhakaGta.Tests.EditMode
{
    public sealed class TrafficAndNpcAiEditTests
    {
        [Test]
        public void DensityProfile_ScalesByDistrictAndTimeOfDay()
        {
            var eveningOldDhaka = DhakaTrafficDensityProfile.GetDistrictProfile("old_dhaka", 18.5f);
            var nightOldDhaka = DhakaTrafficDensityProfile.GetDistrictProfile("old_dhaka", 3.0f);
            var middayGulshan = DhakaTrafficDensityProfile.GetDistrictProfile("gulshan", 13.0f);

            Assert.Greater(eveningOldDhaka.TrafficDensityMultiplier, nightOldDhaka.TrafficDensityMultiplier * 2.5f);
            Assert.Greater(eveningOldDhaka.CycleRickshawShare, middayGulshan.CycleRickshawShare);
            Assert.Greater(middayGulshan.SedanSuvShare, eveningOldDhaka.SedanSuvShare);
        }

        [Test]
        public void TrafficIntersectionNode_CyclesPhasesAndProtectsOpposingCrosswalks()
        {
            var go = new GameObject("SignalTest");
            var signal = go.AddComponent<TrafficIntersectionNode>();

            try
            {
                var node = new RoadJunctionNode
                {
                    junctionId = "Junc_Test",
                    kind = JunctionKind.FourWay,
                    radiusMeters = 10f,
                    hasTrafficSignal = true,
                    centerPosition = Vector3.zero
                };
                signal.ConfigureFromJunction(node, null, 0f);
                signal.ForcePhase(SignalLightState.GreenNorthSouth);

                Assert.IsTrue(signal.IsApproachGreen(Vector3.forward, out _), "NS green must allow Northbound traffic.");
                Assert.IsFalse(signal.IsApproachGreen(Vector3.right, out _), "NS green must block Eastbound traffic.");
                Assert.IsTrue(signal.CanPedestrianCrossAxis(Vector3.forward), "Crossing EW road along Z is safe during NS green.");
                Assert.IsFalse(signal.CanPedestrianCrossAxis(Vector3.right), "Crossing NS road along X is unsafe during NS green.");

                signal.StepSignal(14f);
                Assert.AreNotEqual(SignalLightState.GreenNorthSouth, signal.CurrentSignal);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RoadRouter_EnforcesBangladeshLeftHandTrafficAndFollowsJunctionLinks()
        {
            var segNs = new RoadSplineSegment
            {
                osmId = "road_ns",
                roadName = "Kemal Ataturk Ave",
                highwayClass = "primary",
                lanes = 4,
                widthMeters = 12f,
                totalLengthMeters = 100f,
                isOneWay = false,
                sampledPointsLocal = new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 50f), new Vector3(0f, 0f, 100f) },
                sampledTangents = new[] { Vector3.forward, Vector3.forward, Vector3.forward },
                cumulativeDistances = new[] { 0f, 50f, 100f }
            };

            // LHT check: Northbound (+Z) drives West of centerline (-X), Southbound (-Z) drives East (+X)
            DhakaRoadNetworkRouter.ComputeLhtLanePose(segNs, 50f, 0, true, 0f, out Vector3 nbPos, out Vector3 nbTan);
            DhakaRoadNetworkRouter.ComputeLhtLanePose(segNs, 50f, 0, false, 0f, out Vector3 sbPos, out Vector3 sbTan);

            Assert.Less(nbPos.x, 0f, "LHT Northbound vehicle must drive on LEFT (-X) side.");
            Assert.Greater(sbPos.x, 0f, "LHT Southbound vehicle must drive on LEFT (+X relative to South = East side).");
            Assert.Greater(Vector3.Dot(nbTan, Vector3.forward), 0.99f);
            Assert.Greater(Vector3.Dot(sbTan, Vector3.back), 0.99f);

            // Overtaking shifts rightward (+X for northbound) toward center line
            DhakaRoadNetworkRouter.ComputeLhtLanePose(segNs, 50f, 0, true, 1.8f, out Vector3 passPos, out _);
            Assert.Greater(passPos.x, nbPos.x);
        }

        [Test]
        public void CityDangerBus_RaisesWantedHeatAndDecaysOverTime()
        {
            CityDangerBus.ClearAllForReset();
            try
            {
                CityDangerBus.RaiseDangerEvent(CityDangerKind.Gunshot, new Vector3(12f, 0f, 8f), 40f, 1.2f, null, escalateWanted: true);
                Assert.AreEqual(1, CityDangerBus.ActiveDangerEvents.Count);
                Assert.GreaterOrEqual(CityDangerBus.CurrentWantedLevel, 1);

                CityDangerBus.StepDecay(35f, maxLifetimeSeconds: 5f);
                Assert.AreEqual(0f, CityDangerBus.WantedHeatScore, 0.01f);
                Assert.AreEqual(0, CityDangerBus.CurrentWantedLevel);
            }
            finally
            {
                CityDangerBus.ClearAllForReset();
            }
        }
    }
}
