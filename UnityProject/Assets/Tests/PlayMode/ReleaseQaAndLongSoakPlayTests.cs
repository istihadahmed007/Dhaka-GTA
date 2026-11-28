using System.Collections;
using DhakaGta.Character;
using DhakaGta.Combat;
using DhakaGta.Core;
using DhakaGta.Data;
using DhakaGta.GIS;
using DhakaGta.Graphics;
using DhakaGta.Missions;
using DhakaGta.Networking;
using DhakaGta.WorldGen;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DhakaGta.Tests.PlayMode
{
    public sealed class ReleaseQaAndLongSoakPlayTests
    {
        [UnityTest]
        public IEnumerator PlayMode_LongDurationSoak_WorldStreamingPresetsSaveLoadAndMpRemainStable()
        {
            CityDangerBus.ClearAllForReset();
            Time.timeScale = 1f;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.layer = PhysicsLayers.Ground;
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(120f, 1f, 120f);

            var root = new GameObject("SoakTestRoot");
            var profiler = root.AddComponent<DhakaPerformanceProfiler>();
            var qualityMgr = root.AddComponent<DhakaGraphicsQualityManager>();
            var coordMgr = root.AddComponent<WorldCoordinateManager>();
            var regionLoader = root.AddComponent<WorldRegionLoader>();
            var chunkMgr = root.AddComponent<WorldChunkManager>();
            var streamCtrl = root.AddComponent<WorldStreamingController>();

            var db = ScriptableObject.CreateInstance<GeoFeatureDatabase>();
            var playerGo = new GameObject("SoakProtagonist");
            playerGo.transform.position = new Vector3(0f, 0.1f, 0f);
            var cc = playerGo.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.34f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            var controller = playerGo.AddComponent<ProtagonistController>();
            var hp = playerGo.AddComponent<HealthComponent>();

            var tariq = ScriptableObject.CreateInstance<ProtagonistProfileSO>();
            tariq.protagonistId = "tariq";
            tariq.fullName = "Tariq Hasan";
            tariq.maxHealth = 135f;
            tariq.maxStamina = 115f;
            tariq.jogSpeedMetersPerSec = 5.2f;

            controller.Configure(tariq, null, null);
            hp.InitializeFromProfile(tariq);

            coordMgr.Configure(db, GeographicCoordinateConverter.ShaplaOriginLat, GeographicCoordinateConverter.ShaplaOriginLon, 0.0, playerGo.transform);
            streamCtrl.Configure(coordMgr, regionLoader, chunkMgr, playerGo.transform);

            var econ = root.AddComponent<PlayerEconomyAndInventory>();
            econ.ResetStarterEconomy();
            var saver = root.AddComponent<DhakaSaveSystem>();
            saver.BindDependencies(controller, null, hp, econ, null, null);

            root.AddComponent<DhakaWebSocketTransport>();
            var mpSession = root.AddComponent<DhakaMultiplayerSessionManager>();

            try
            {
                profiler.StartRecorders();
                controller.SetSyntheticMoveInput(Vector2.up);

                // Accelerated 180-tick soak cycle exercising movement, preset swaps, world streaming, save/load, and MP snapshots
                for (int frame = 0; frame < 180; frame++)
                {
                    if (frame % 60 == 0)
                    {
                        qualityMgr.CycleQualityPreset();
                        string json = saver.SerializeToJson();
                        Assert.IsTrue(saver.LoadFromJson(json));
                    }

                    if (frame == 90)
                    {
                        // Simulate high-speed travel transition northward into Gulshan district UTM coords
                        cc.enabled = false;
                        playerGo.transform.position = new Vector3(-20f, 0.1f, 5200f);
                        cc.enabled = true;
                        streamCtrl.TickStreaming();
                        Assert.NotNull(streamCtrl.ActiveDistrict, "World transition must resolve Gulshan/Hatirjheel district cleanly.");
                    }

                    mpSession.ProcessServerJsonMessage(
                        "{\"type\":\"s_world_snapshot\",\"serverTick\":" + frame + ",\"timestamp\":1000," +
                        "\"players\":[],\"vehicles\":[],\"world\":{\"timeOfDay\":17.5,\"weather\":\"clear\",\"connectedCount\":0}}");

                    yield return null;
                }

                PerformanceTelemetrySample finalSample = profiler.SampleNow(Time.unscaledDeltaTime);
                Assert.Greater(finalSample.SampleFrames, 150);
                Assert.IsFalse(float.IsNaN(finalSample.RollingAvgFps));
                Assert.IsFalse(float.IsNaN(playerGo.transform.position.z));
                Assert.AreEqual(1f, Time.timeScale, 0.001f);
            }
            finally
            {
                profiler.StopRecorders();
                controller.ClearSyntheticInput();
                Object.Destroy(ground);
                Object.Destroy(root);
                Object.Destroy(playerGo);
                Object.Destroy(db);
                Object.Destroy(tariq);
            }
        }
    }
}
