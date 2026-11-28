using System.Collections;
using DhakaGta.Character;
using DhakaGta.Networking;
using DhakaGta.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DhakaGta.Tests.PlayMode
{
    public sealed class MultiplayerClientPlayTests
    {
        [UnityTest]
        public IEnumerator PlayMode_RemoteRealPeersVehiclesReconciliationAndHud_SyncCleanlyWithoutFakes()
        {
            var root = new GameObject("MpPlayTestRoot");
            var playerGo = new GameObject("LocalMpPlayer");
            playerGo.AddComponent<CharacterController>();
            var protagonist = playerGo.AddComponent<ProtagonistController>();
            protagonist.Configure(null, null, null);

            var remoteAvatars = root.AddComponent<RemoteNetworkAvatarController>();
            var remoteVehicles = root.AddComponent<RemoteNetworkVehicleSync>();
            root.AddComponent<DhakaWebSocketTransport>();
            var session = root.AddComponent<DhakaMultiplayerSessionManager>();
            session.BindSceneReferences(protagonist, null, null, null, remoteAvatars, remoteVehicles);

            var hud = root.AddComponent<DhakaMultiplayerHudController>();
            hud.ConfigureAndBuildUi(session);

            try
            {
                // 1. Authenticate local Unity client
                session.ProcessServerJsonMessage(
                    "{\"type\":\"s_auth_ok\",\"playerId\":\"usr_local\",\"token\":\"tok_l\",\"isNewAccount\":true," +
                    "\"player\":{\"id\":\"usr_local\",\"name\":\"Tariq_Local\",\"protagonistId\":\"tariq\",\"money\":1200}," +
                    "\"world\":{\"timeOfDay\":15.0,\"weather\":\"clear\",\"connectedCount\":3},\"vehicles\":[]}");

                // 2. Inject snapshot with local user + 2 real peers + 2 authoritative vehicles (no fake bots!)
                const string snap1 =
                    "{\"type\":\"s_world_snapshot\",\"serverTick\":10,\"timestamp\":1000," +
                    "\"players\":[" +
                    "{\"id\":\"usr_local\",\"name\":\"Tariq_Local\",\"x\":0,\"y\":0,\"z\":0,\"vx\":0,\"vz\":0,\"heading\":0,\"hp\":100,\"state\":\"idle\"}," +
                    "{\"id\":\"usr_peer_1\",\"name\":\"Maya_Gulshan\",\"protagonistId\":\"maya\",\"x\":10,\"y\":0,\"z\":20,\"vx\":4,\"vz\":0,\"heading\":1.5,\"hp\":95,\"state\":\"run\"}," +
                    "{\"id\":\"usr_peer_2\",\"name\":\"Tariq_Moti\",\"protagonistId\":\"tariq\",\"x\":-18,\"y\":0,\"z\":8,\"vx\":0,\"vz\":2,\"heading\":0,\"hp\":100,\"state\":\"walk\"}" +
                    "]," +
                    "\"vehicles\":[" +
                    "{\"id\":\"net_veh_1\",\"kind\":\"sedan\",\"x\":12,\"y\":0,\"z\":24,\"speed\":0,\"heading\":0,\"hp\":1000,\"sirenOn\":false,\"locked\":false}," +
                    "{\"id\":\"net_veh_5\",\"kind\":\"police\",\"x\":-25,\"y\":0,\"z\":14,\"speed\":8,\"heading\":0.5,\"hp\":1200,\"sirenOn\":true,\"locked\":true,\"ownerId\":\"usr_peer_2\"}" +
                    "],\"world\":{\"timeOfDay\":15.1,\"weather\":\"clear\",\"connectedCount\":3}}";

                session.ProcessServerJsonMessage(snap1);
                yield return null;

                // Must spawn ONLY the 2 remote human peers (local player never duplicated)
                Assert.AreEqual(2, remoteAvatars.ActiveRemotePeerCount);
                Assert.AreEqual(2, remoteVehicles.ActiveSyncedVehicleCount);
                Assert.AreEqual(3, session.ServerPlayerCount);

                // 3. Authoritative s_reconcile pulls drifted local transform back
                playerGo.transform.position = new Vector3(100f, 0f, 100f);
                session.ProcessServerJsonMessage(
                    "{\"type\":\"s_reconcile\",\"lastProcessedInput\":1,\"x\":2.5,\"y\":0,\"z\":4.0,\"vx\":0,\"vz\":0,\"heading\":0,\"hp\":100,\"armor\":50,\"money\":1200,\"wantedLevel\":0}");
                yield return null;
                Assert.Less(Vector3.Distance(playerGo.transform.position, new Vector3(2.5f, 0f, 4.0f)), 0.5f, "Server reconciliation must snap excessive drift.");

                // 4. Multiplayer Lobby HUD toggle check
                hud.TogglePanel();
                Assert.IsTrue(hud.IsPanelOpen);
                yield return null;
                hud.TogglePanel();
                Assert.IsFalse(hud.IsPanelOpen);
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(playerGo);
            }
        }
    }
}
