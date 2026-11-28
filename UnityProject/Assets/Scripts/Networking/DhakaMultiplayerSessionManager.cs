using System;
using System.Collections.Generic;
using DhakaGta.Character;
using DhakaGta.Combat;
using DhakaGta.Core;
using DhakaGta.Missions;
using DhakaGta.Vehicles;
using UnityEngine;

namespace DhakaGta.Networking
{
    public enum MultiplayerConnectionState
    {
        Disconnected = 0,
        Connecting = 1,
        Authenticating = 2,
        ConnectedAuthenticated = 3,
        Reconnecting = 4
    }

    public struct PredictedInputFrame
    {
        public int Seq;
        public float Dt;
        public Vector3 SentPosition;
    }

    /// <summary>
    /// Full Unity authoritative multiplayer client coordinator handling WebSocket session lifecycle,
    /// PIN/Token authentication, exponential-backoff reconnects, 20 Hz input upload, s_reconcile checks,
    /// persistent account inventory/progression sync, chat, and anti-cheat feedback.
    /// </summary>
    [RequireComponent(typeof(DhakaWebSocketTransport))]
    public sealed class DhakaMultiplayerSessionManager : MonoBehaviour
    {
        private const string TokenPrefKeyPrefix = "DhakaMpToken_";
        private const int MaxReconnectAttempts = 5;

        [Header("Scene Links (Optional for Headless Tests)")]
        [SerializeField] private ProtagonistController localProtagonist;
        [SerializeField] private HealthComponent localHealth;
        [SerializeField] private PlayerEconomyAndInventory localEconomy;
        [SerializeField] private DhakaMissionEngine localMissionEngine;
        [SerializeField] private RemoteNetworkAvatarController remoteAvatarController;
        [SerializeField] private RemoteNetworkVehicleSync remoteVehicleSync;

        private DhakaWebSocketTransport _transport;
        private readonly Queue<PredictedInputFrame> _pendingInputs = new Queue<PredictedInputFrame>(64);
        private readonly Dictionary<string, int> _serverInventory = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _ownedVehicleIds = new List<string>();
        private readonly List<string> _completedMissionIds = new List<string>();
        private readonly List<string> _unlockedWeapons = new List<string>();
        private readonly List<SChatPacket> _chatHistory = new List<SChatPacket>(64);

        private string _pendingServerUrl = DhakaNetProtocolConstants.DefaultLocalServerUrl;
        private string _pendingCallSign = "Tariq_Dhaka";
        private string _pendingPin;
        private string _pendingProtagonistId = "tariq";
        private bool _userRequestedDisconnect;
        private int _reconnectAttempts;
        private float _reconnectTimer;
        private float _inputSendAccum;
        private float _pingTimer;
        private int _inputSequence;

        public MultiplayerConnectionState ConnectionState { get; private set; } = MultiplayerConnectionState.Disconnected;
        public string LocalPlayerId { get; private set; }
        public string SessionToken { get; private set; }
        public string ActiveCallSign => _pendingCallSign;
        public string LastStatusMessage { get; private set; } = "Offline";
        public string LastAntiCheatAlert { get; private set; } = string.Empty;
        public int CurrentPingMs { get; private set; }
        public int ServerPlayerCount { get; private set; }
        public int ServerTick { get; private set; }
        public int AuthoritativeMoneyBdt { get; private set; }
        public IReadOnlyDictionary<string, int> ServerInventory => _serverInventory;
        public IReadOnlyList<string> OwnedVehicleIds => _ownedVehicleIds;
        public IReadOnlyList<string> CompletedMissionIds => _completedMissionIds;
        public IReadOnlyList<string> UnlockedWeapons => _unlockedWeapons;
        public IReadOnlyList<SChatPacket> ChatHistory => _chatHistory;
        public IMultiplayerSessionTransport Transport => _transport;

        public event Action<SAuthOkPacket> OnAuthenticated;
        public event Action<string> OnAuthFailed;
        public event Action<SChatPacket> OnChatMessageReceived;
        public event Action<SAntiCheatAlertPacket> OnAntiCheatAlertReceived;
        public event Action<SCombatEventPacket> OnCombatEventReceived;

        private void Awake()
        {
            _transport = GetComponent<DhakaWebSocketTransport>() ?? gameObject.AddComponent<DhakaWebSocketTransport>();
            _transport.OnSocketOpened += HandleSocketOpened;
            _transport.OnSocketClosed += HandleSocketClosed;
            _transport.OnSocketError += HandleSocketError;
            _transport.OnRawJsonMessage += ProcessServerJsonMessage;
        }

        private void OnDestroy()
        {
            if (_transport != null)
            {
                _transport.OnSocketOpened -= HandleSocketOpened;
                _transport.OnSocketClosed -= HandleSocketClosed;
                _transport.OnSocketError -= HandleSocketError;
                _transport.OnRawJsonMessage -= ProcessServerJsonMessage;
            }
        }

        public void BindSceneReferences(
            ProtagonistController protagonist,
            HealthComponent health,
            PlayerEconomyAndInventory economy,
            DhakaMissionEngine missions,
            RemoteNetworkAvatarController remoteAvatars,
            RemoteNetworkVehicleSync remoteVehicles)
        {
            localProtagonist = protagonist;
            localHealth = health;
            localEconomy = economy;
            localMissionEngine = missions;
            remoteAvatarController = remoteAvatars;
            remoteVehicleSync = remoteVehicles;
        }

        public void ConnectAndAuthenticate(
            string serverUrl,
            string callSign,
            string pin = "",
            string protagonistId = "tariq",
            bool useCachedToken = true)
        {
            _userRequestedDisconnect = false;
            _reconnectAttempts = 0;
            _pendingServerUrl = string.IsNullOrWhiteSpace(serverUrl)
                ? DhakaNetProtocolConstants.DefaultLocalServerUrl
                : serverUrl.Trim();
            _pendingCallSign = string.IsNullOrWhiteSpace(callSign) ? "Tariq_Unity" : callSign.Trim();
            _pendingPin = string.IsNullOrEmpty(pin) ? null : pin.Trim();
            _pendingProtagonistId = protagonistId == "maya" ? "maya" : "tariq";

            if (useCachedToken && string.IsNullOrEmpty(SessionToken))
            {
                string cached = PlayerPrefs.GetString(TokenPrefKeyPrefix + _pendingCallSign, string.Empty);
                if (!string.IsNullOrEmpty(cached)) SessionToken = cached;
            }

            ConnectionState = MultiplayerConnectionState.Connecting;
            LastStatusMessage = $"Connecting to {_pendingServerUrl}...";
            _transport.Connect(_pendingServerUrl);
        }

        public void Disconnect()
        {
            _userRequestedDisconnect = true;
            ConnectionState = MultiplayerConnectionState.Disconnected;
            LastStatusMessage = "Disconnected cleanly";
            _transport.CloseImmediate();
            remoteAvatarController?.ClearAllPeers();
            remoteVehicleSync?.ClearAllVehicles();
        }

        public void SendChatMessage(string text)
        {
            if (ConnectionState != MultiplayerConnectionState.ConnectedAuthenticated || string.IsNullOrWhiteSpace(text)) return;
            _transport.SendPacketObject(new CChatPacket { text = text.Trim() });
        }

        public void SendBuyItem(string itemId)
        {
            if (ConnectionState != MultiplayerConnectionState.ConnectedAuthenticated) return;
            _transport.SendPacketObject(new CBuyItemPacket { itemId = itemId });
        }

        public void SendUseItem(string itemId)
        {
            if (ConnectionState != MultiplayerConnectionState.ConnectedAuthenticated) return;
            _transport.SendPacketObject(new CUseItemPacket { itemId = itemId });
        }

        public void SendBuyVehicle(string vehicleKind)
        {
            if (ConnectionState != MultiplayerConnectionState.ConnectedAuthenticated) return;
            _transport.SendPacketObject(new CBuyVehiclePacket { vehicleKind = vehicleKind });
        }

        public void SendVehicleLock(string vehicleId, bool locked)
        {
            if (ConnectionState != MultiplayerConnectionState.ConnectedAuthenticated) return;
            _transport.SendPacketObject(new CVehicleLockPacket { vehicleId = vehicleId, locked = locked });
        }

        public void SendVehicleEnterExit(bool entering, string vehicleId = null)
        {
            if (ConnectionState != MultiplayerConnectionState.ConnectedAuthenticated) return;
            _transport.SendPacketObject(new CVehicleActionPacket { action = entering ? "enter" : "exit", vehicleId = vehicleId });
        }

        public void SendCombatAttack(string weaponId, float aimHeadingRad, string targetId = null, string targetKind = "player")
        {
            if (ConnectionState != MultiplayerConnectionState.ConnectedAuthenticated) return;
            _transport.SendPacketObject(new CAttackPacket
            {
                seq = ++_inputSequence,
                weapon = weaponId,
                aimHeading = aimHeadingRad,
                targetId = targetId,
                targetKind = targetKind
            });
        }

        public void SendProtagonistSwitch(string protagonistId)
        {
            _pendingProtagonistId = protagonistId == "maya" ? "maya" : "tariq";
            if (ConnectionState != MultiplayerConnectionState.ConnectedAuthenticated) return;
            _transport.SendPacketObject(new CSwitchProtagonistPacket { protagonistId = _pendingProtagonistId });
        }

        public void SendMissionCompletedSync(string missionId)
        {
            if (ConnectionState != MultiplayerConnectionState.ConnectedAuthenticated || string.IsNullOrEmpty(missionId)) return;
            _transport.SendPacketObject(new CProgressionSyncPacket { completedMissionId = missionId });
        }

        private void Update()
        {
            if (ConnectionState == MultiplayerConnectionState.Reconnecting)
            {
                _reconnectTimer -= Time.unscaledDeltaTime;
                if (_reconnectTimer <= 0f)
                {
                    ConnectionState = MultiplayerConnectionState.Connecting;
                    _transport.Connect(_pendingServerUrl);
                }
                return;
            }

            if (ConnectionState != MultiplayerConnectionState.ConnectedAuthenticated) return;

            // Periodic 1 Hz RTT ping
            _pingTimer += Time.unscaledDeltaTime;
            if (_pingTimer >= 1.0f)
            {
                _pingTimer = 0f;
                _transport.SendPacketObject(new CPingPacket { clientTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
            }

            // 20 Hz Client Input Stream + Prediction Log
            _inputSendAccum += Time.deltaTime;
            if (_inputSendAccum >= 0.05f && localProtagonist != null)
            {
                float dt = Mathf.Clamp(_inputSendAccum, 0.01f, 0.1f);
                _inputSendAccum = 0f;
                SendLocalTransformIntent(dt);
            }
        }

        private void SendLocalTransformIntent(float dt)
        {
            Vector3 vel = localProtagonist.TrajectoryPredictor != null
                ? localProtagonist.TrajectoryPredictor.CurrentWorldVelocity
                : Vector3.zero;
            Vector2 moveVec = new Vector2(vel.x, vel.z);
            float maxS = localProtagonist.IsSprinting ? 7.5f : 5.0f;
            Vector2 norm = Vector2.ClampMagnitude(moveVec / Mathf.Max(1f, maxS), 1f);

            int seq = ++_inputSequence;
            _pendingInputs.Enqueue(new PredictedInputFrame
            {
                Seq = seq,
                Dt = dt,
                SentPosition = localProtagonist.transform.position
            });
            while (_pendingInputs.Count > 40) _pendingInputs.Dequeue();

            Vector3 fwd = localProtagonist.transform.forward;
            float headingRad = Mathf.Atan2(fwd.x, fwd.z);

            _transport.SendPacketObject(new CInputPacket
            {
                seq = seq,
                dt = dt,
                moveX = norm.x,
                moveZ = norm.y,
                heading = headingRad,
                sprint = localProtagonist.IsSprinting,
                jump = !localProtagonist.IsGrounded
            });
        }

        private void HandleSocketOpened()
        {
            ConnectionState = MultiplayerConnectionState.Authenticating;
            LastStatusMessage = "Authenticating session...";
            _transport.SendPacketObject(new CAuthPacket
            {
                name = _pendingCallSign,
                token = string.IsNullOrEmpty(SessionToken) ? null : SessionToken,
                pin = _pendingPin,
                protagonistId = _pendingProtagonistId,
                clientEngine = "unity"
            });
        }

        private void HandleSocketClosed(string reason)
        {
            if (_userRequestedDisconnect) return;

            if (_reconnectAttempts < MaxReconnectAttempts && !string.IsNullOrEmpty(SessionToken))
            {
                _reconnectAttempts++;
                ConnectionState = MultiplayerConnectionState.Reconnecting;
                _reconnectTimer = Mathf.Min(10f, Mathf.Pow(1.8f, _reconnectAttempts));
                LastStatusMessage = $"Connection lost ({reason}). Reconnecting attempt {_reconnectAttempts}/{MaxReconnectAttempts}...";
            }
            else
            {
                ConnectionState = MultiplayerConnectionState.Disconnected;
                LastStatusMessage = $"Disconnected: {reason}";
            }
        }

        private void HandleSocketError(string err)
        {
            LastStatusMessage = $"Socket Error: {err}";
            if (ConnectionState == MultiplayerConnectionState.Connecting)
            {
                HandleSocketClosed(err);
            }
        }

        /// <summary>
        /// Decodes incoming server JSON payloads into Unity state cleanly.
        /// Marked public so EditMode & PlayMode tests can inject and verify exact server packets.
        /// </summary>
        public void ProcessServerJsonMessage(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            BasePacketEnvelope env = JsonUtility.FromJson<BasePacketEnvelope>(json);
            if (env == null || string.IsNullOrEmpty(env.type)) return;

            switch (env.type)
            {
                case "s_auth_ok":
                    var authOk = JsonUtility.FromJson<SAuthOkPacket>(json);
                    if (authOk == null) return;
                    LocalPlayerId = authOk.playerId;
                    SessionToken = authOk.token;
                    if (!string.IsNullOrEmpty(SessionToken))
                    {
                        PlayerPrefs.SetString(TokenPrefKeyPrefix + _pendingCallSign, SessionToken);
                    }
                    _reconnectAttempts = 0;
                    ConnectionState = MultiplayerConnectionState.ConnectedAuthenticated;
                    LastStatusMessage = $"Online as {_pendingCallSign} ({(authOk.isNewAccount ? "New Account" : "Restored")})";
                    if (authOk.player != null)
                    {
                        AuthoritativeMoneyBdt = authOk.player.money;
                        ApplyAccountInventoryAndLists(
                            ProtocolJsonMapHelper.ExtractIntDictionary(json, "inventory"),
                            authOk.player.ownedVehicleIds,
                            authOk.player.completedMissionIds,
                            null);
                    }
                    remoteVehicleSync?.IngestSnapshot(authOk.vehicles);
                    OnAuthenticated?.Invoke(authOk);
                    break;

                case "s_auth_fail":
                    var fail = JsonUtility.FromJson<SAuthFailPacket>(json);
                    SessionToken = null; // Clear rejected token so manual credentials can re-auth
                    _userRequestedDisconnect = true;
                    ConnectionState = MultiplayerConnectionState.Disconnected;
                    LastStatusMessage = $"Auth Failed: {fail?.reason}";
                    OnAuthFailed?.Invoke(fail?.reason);
                    break;

                case "s_world_snapshot":
                    var snap = JsonUtility.FromJson<SWorldSnapshotPacket>(json);
                    if (snap == null) return;
                    ServerTick = snap.serverTick;
                    if (snap.world != null)
                    {
                        ServerPlayerCount = snap.world.connectedCount;
                        WorldSurfaceState.CurrentTimeOfDayHours = snap.world.timeOfDay;
                    }
                    remoteAvatarController?.IngestSnapshot(snap.players, LocalPlayerId, snap.timestamp);
                    remoteVehicleSync?.IngestSnapshot(snap.vehicles);
                    break;

                case "s_reconcile":
                    var rec = JsonUtility.FromJson<SReconcilePacket>(json);
                    if (rec == null) return;
                    ApplyServerReconciliation(rec);
                    break;

                case "s_account_sync":
                    var sync = JsonUtility.FromJson<SAccountSyncPacket>(json);
                    if (sync == null) return;
                    AuthoritativeMoneyBdt = sync.money;
                    ApplyAccountInventoryAndLists(
                        ProtocolJsonMapHelper.ExtractIntDictionary(json, "inventory"),
                        sync.ownedVehicleIds,
                        sync.completedMissionIds,
                        sync.unlockedWeapons);
                    break;

                case "s_transaction_result":
                    var tx = JsonUtility.FromJson<STransactionResultPacket>(json);
                    if (tx != null)
                    {
                        AuthoritativeMoneyBdt = tx.money;
                        LastStatusMessage = tx.message;
                    }
                    break;

                case "s_chat":
                    var chat = JsonUtility.FromJson<SChatPacket>(json);
                    if (chat != null)
                    {
                        if (_chatHistory.Count >= 64) _chatHistory.RemoveAt(0);
                        _chatHistory.Add(chat);
                        OnChatMessageReceived?.Invoke(chat);
                    }
                    break;

                case "s_combat_event":
                    var cmb = JsonUtility.FromJson<SCombatEventPacket>(json);
                    if (cmb != null) OnCombatEventReceived?.Invoke(cmb);
                    break;

                case "s_pong":
                    var pong = JsonUtility.FromJson<SPongPacket>(json);
                    if (pong != null)
                    {
                        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        CurrentPingMs = Mathf.Clamp((int)(nowMs - pong.clientTime), 1, 999);
                    }
                    break;

                case "s_anticheat_alert":
                    var ac = JsonUtility.FromJson<SAntiCheatAlertPacket>(json);
                    if (ac != null)
                    {
                        LastAntiCheatAlert = $"[{ac.code} #{ac.strikes}] {ac.message}";
                        OnAntiCheatAlertReceived?.Invoke(ac);
                    }
                    break;

                case "s_error":
                    var err = JsonUtility.FromJson<SErrorPacket>(json);
                    if (err != null) LastStatusMessage = $"Server [{err.code}]: {err.message}";
                    break;
            }
        }

        private void ApplyServerReconciliation(SReconcilePacket rec)
        {
            AuthoritativeMoneyBdt = rec.money;

            while (_pendingInputs.Count > 0 && _pendingInputs.Peek().Seq <= rec.lastProcessedInput)
            {
                _pendingInputs.Dequeue();
            }

            if (localProtagonist == null) return;

            Vector3 serverPos = new Vector3(rec.x, localProtagonist.transform.position.y, rec.z);
            float drift = Vector3.Distance(
                new Vector3(localProtagonist.transform.position.x, 0f, localProtagonist.transform.position.z),
                new Vector3(serverPos.x, 0f, serverPos.z));

            // Only pull local transform when authoritative drift exceeds tolerance
            if (drift > 2.5f)
            {
                var cc = localProtagonist.GetComponent<CharacterController>();
                bool prev = cc != null && cc.enabled;
                if (cc != null) cc.enabled = false;
                localProtagonist.transform.position = serverPos;
                if (cc != null) cc.enabled = prev;
            }
            else if (drift > 0.25f)
            {
                localProtagonist.transform.position = Vector3.Lerp(localProtagonist.transform.position, serverPos, 0.22f);
            }
        }

        private void ApplyAccountInventoryAndLists(
            Dictionary<string, int> inv,
            string[] ownedVehs,
            string[] completedMissions,
            string[] weapons)
        {
            if (inv != null && inv.Count > 0)
            {
                _serverInventory.Clear();
                foreach (var kv in inv) _serverInventory[kv.Key] = kv.Value;
            }
            if (ownedVehs != null)
            {
                _ownedVehicleIds.Clear();
                _ownedVehicleIds.AddRange(ownedVehs);
            }
            if (completedMissions != null)
            {
                _completedMissionIds.Clear();
                _completedMissionIds.AddRange(completedMissions);
            }
            if (weapons != null)
            {
                _unlockedWeapons.Clear();
                _unlockedWeapons.AddRange(weapons);
            }
        }
    }
}
