using DhakaGta.Networking;
using NUnit.Framework;
using UnityEngine;

namespace DhakaGta.Tests.EditMode
{
    public sealed class MultiplayerClientEditTests
    {
        [Test]
        public void ProtocolJsonMapHelper_ParsesInventoryMapAccurately()
        {
            const string json =
                "{\"type\":\"s_account_sync\",\"money\":4200,\"inventory\":{\"cha_flask\":3,\"first_aid_kit\":2},\"ownedVehicleIds\":[\"owned_cng_1\"]}";

            var inv = ProtocolJsonMapHelper.ExtractIntDictionary(json, "inventory");
            Assert.AreEqual(3, inv["cha_flask"]);
            Assert.AreEqual(2, inv["first_aid_kit"]);
        }

        [Test]
        public void SessionManager_ProcessesAuthSyncChatAndAntiCheatPacketsDeterministically()
        {
            var go = new GameObject("MpSessionUnit");
            go.AddComponent<DhakaWebSocketTransport>();
            var mgr = go.AddComponent<DhakaMultiplayerSessionManager>();

            bool authed = false;
            bool gotChat = false;
            bool gotAlert = false;
            mgr.OnAuthenticated += _ => authed = true;
            mgr.OnChatMessageReceived += _ => gotChat = true;
            mgr.OnAntiCheatAlertReceived += _ => gotAlert = true;

            try
            {
                const string authJson =
                    "{\"type\":\"s_auth_ok\",\"playerId\":\"usr_101\",\"token\":\"tok_abc\",\"isNewAccount\":false," +
                    "\"player\":{\"id\":\"usr_101\",\"name\":\"Tariq_Test\",\"protagonistId\":\"tariq\",\"money\":3400," +
                    "\"inventory\":{\"cha_flask\":4},\"ownedVehicleIds\":[\"v1\"],\"completedMissionIds\":[\"story_01_tong\"]}," +
                    "\"world\":{\"timeOfDay\":18.2,\"weather\":\"clear\",\"serverUptimeSec\":99,\"connectedCount\":3},\"vehicles\":[]}";

                mgr.ProcessServerJsonMessage(authJson);

                Assert.IsTrue(authed);
                Assert.AreEqual(MultiplayerConnectionState.ConnectedAuthenticated, mgr.ConnectionState);
                Assert.AreEqual("usr_101", mgr.LocalPlayerId);
                Assert.AreEqual(3400, mgr.AuthoritativeMoneyBdt);
                Assert.AreEqual(4, mgr.ServerInventory["cha_flask"]);
                Assert.Contains("v1", (System.Collections.ICollection)mgr.OwnedVehicleIds);
                Assert.Contains("story_01_tong", (System.Collections.ICollection)mgr.CompletedMissionIds);

                mgr.ProcessServerJsonMessage("{\"type\":\"s_chat\",\"senderId\":\"usr_2\",\"senderName\":\"Maya\",\"text\":\"Ready at Circle 1\",\"timestamp\":1000,\"isSystem\":false}");
                Assert.IsTrue(gotChat);
                Assert.AreEqual(1, mgr.ChatHistory.Count);

                mgr.ProcessServerJsonMessage("{\"type\":\"s_anticheat_alert\",\"strikes\":2,\"code\":\"AIM_MISMATCH\",\"message\":\"Invalid angle\"}");
                Assert.IsTrue(gotAlert);
                Assert.IsTrue(mgr.LastAntiCheatAlert.Contains("AIM_MISMATCH"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
