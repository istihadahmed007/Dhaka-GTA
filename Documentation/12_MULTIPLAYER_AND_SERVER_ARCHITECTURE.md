# PART 10 — UNITY MULTIPLAYER CLIENT & AUTHORITATIVE SERVER UPGRADE

## 1. Audit of Existing Server & Migration Decision
Before writing Unity networking code, we audited the existing Node.js multiplayer stack (`src/server/index.ts`, `src/server/authoritativeServer.ts`, `src/server/accountStore.ts`, `src/game/multiplayer/protocol.ts`, and `src/game/multiplayer.test.ts`).

### Existing Strengths Retained
- **20 Hz Authoritative Tick Simulation** (`ws` over Node HTTP, targeting 2–16 simultaneous human players).
- **Salted SHA-256 PIN Authentication & Session Token Reconnects** (`AccountStore`).
- **Rate Limiting & Schema Guardrails**: Rejects malformed JSON, non-finite `NaN/Infinity`, `> 60 packets/sec`, `> 4 chat/sec`, and `> 6` concurrent IP sockets.
- **No Fake Bots**: `sessions` tracks exclusively authenticated human sockets.
- **Ready Deployment Targets**: `Dockerfile`, `render.yaml`, and `/health` JSON monitoring endpoint.

### Decision: Retain Node.js WebSocket Server & Upgrade Incrementally (Zero Breaking Protocol Rewrite)
Rather than discarding a tested Node.js backend to introduce an incompatible transport architecture (e.g., Unity Netcode for GameObjects + Relay requiring a C# Linux headless build refactor), we **retain the Node.js WebSocket authoritative server** and **upgrade both protocol & server validation** cleanly:
- **Migration Plan**:
  - Phase A (Completed): Upgrade `src/game/multiplayer/protocol.ts`, `accountStore.ts`, and `authoritativeServer.ts` with backwards-compatible optional fields (`clientEngine: 'unity'`, `protagonistId`, `inventory`, `ownedVehicleIds`, `completedMissionIds`) so both legacy TypeScript checks AND native Unity 6 `.NET System.Net.WebSockets.ClientWebSocket` clients connect concurrently.
  - Phase B (Completed): Connect native `ClientWebSocket` Unity client + 20 Hz reconciliation + 100 ms buffered remote Humanoid interpolation inside `DhakaGta.Networking`.
  - Phase C (Optional Future Scale >16 Players): Introduce interest-management spatial sector partitioning or switch message serialization from JSON to compact binary framing while keeping the exact same state-authority model.

---

## 2. Node.js Authoritative Server Upgrades Implemented
- **1:1 Unity Coordinate Support (`clientEngine: 'unity'`)**: Expands valid world coordinate envelope from TS's `±270 m` box to `±12,000 m` for Unity HDRP 1:1 Dhaka clients while enforcing strict velocity limits and keeping `±264 m` validation for web clients.
- **Anti-Cheat Extensions**:
  - Cumulative wall-clock `dt` budget monitor (`dtBudgetAccumSec > 1.35 s` per 1s window flags `DT_BUDGET_EXCEEDED` against speed/time-dilation exploits).
  - Weapon unlock ownership verification (`UNOWNED_WEAPON`) checked **prior** to fire-rate cooldowns.
  - Attack aim-cone angular alignment validation (`AIM_MISMATCH` past 2.2 m range) + vehicle damage support.
  - Progressive anti-cheat strike tracking (`s_anticheat_alert`, auto-disconnect at `>= 8` strikes).
- **Vehicle Ownership & Persistent Inventory/Progression**:
  - Added `c_buy_vehicle`, `c_vehicle_lock`, `c_use_item`, `c_switch_protagonist`, `c_progression_sync`, and `s_account_sync`.
  - Private owned/locked vehicles reject unauthorized boarding (`VEHICLE_LOCKED`).
  - Persists `protagonistId`, `inventory` stacks, `ownedVehicleIds`, `completedMissionIds`, BDT wallet, weapons, and coordinates across reconnects in `AccountStore`.
- **Server Health Monitoring & Logging**: Enriched `/health` JSON response (`players`, `maxPlayers`, `vehicles`, `uptime`, `tickRateHz`, `serverTick`, `avgTickMs`, `packetsReceivedTotal`, `packetsRejectedTotal`, `antiCheatStrikesTotal`, `memoryRssMb`, `realPlayersOnly: true`) + 200-entry structured log buffer (`getRecentLogs()`).

---

## 3. Unity 6 Client Implementation (`DhakaGta.Networking` & `DhakaGta.UI`)
- **[DhakaMultiplayerProtocol.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Scripts/Networking/DhakaMultiplayerProtocol.cs)**: Mirrors Node `protocol.ts` packets for Unity `JsonUtility` + `ProtocolJsonMapHelper.ExtractIntDictionary` (inventory maps).
- **[DhakaWebSocketTransport.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Scripts/Networking/DhakaWebSocketTransport.cs)**: Implements `IMultiplayerSessionTransport` over .NET `ClientWebSocket` with background async receive/send loops and thread-safe `ConcurrentQueue<string>` main-thread pumping.
- **[DhakaMultiplayerSessionManager.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Scripts/Networking/DhakaMultiplayerSessionManager.cs)**: Handles CallSign/PIN auth, token caching & automatic exponential-backoff reconnect (`MaxReconnectAttempts = 5`), 20 Hz input emission + prediction queue, `s_reconcile` drift correction, account inventory/vehicle/progression sync, RTT ping, chat, and anti-cheat alerts.
- **[RemoteNetworkAvatarController.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Scripts/Networking/RemoteNetworkAvatarController.cs)**: Spawns and interpolates (`100 ms` buffer) Humanoid **Tariq** & **Maya** avatars exclusively for authenticated peers (zero fake bots), complete with nameplates & vehicle hiding.
- **[RemoteNetworkVehicleSync.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Scripts/Networking/RemoteNetworkVehicleSync.cs)**: Synchronizes multi-class Dhaka vehicles (`DhakaVehicleFactory`), interpolation, DMP sirens, and ownership locks.
- **[DhakaMultiplayerHudController.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Scripts/UI/DhakaMultiplayerHudController.cs)** & **[DhakaMultiplayerPipelineBuilder.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Scripts/Editor/DhakaMultiplayerPipelineBuilder.cs)**: `[M]` Lobby, CallSign/URL config, peer metrics, chat UI, and integration inside `GulshanHatirjheelWorldBuilder.cs`.

---

## 4. Verification Results
- **Live Server & Multi-Client Integration Tests** (`tsc --noEmit` + `src/game/multiplayer.test.ts` + `src/server/unityMultiplayerIntegration.test.ts`):
  - Verifies 4 simultaneous authenticated WebSocket Unity clients operating across >400 m coordinates with zero fake players, live `/health` telemetry, vehicle lock enforcement, persistent account reconnect via disk JSON, and anti-cheat strikes/disconnects.
- **Unity Tests Added**: [MultiplayerClientEditTests.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Tests/EditMode/MultiplayerClientEditTests.cs) & [MultiplayerClientPlayTests.cs](file:///c:/Users/Tonmoy/Documents/Project%20Dhaka%20Game/Dhaka-GTA/UnityProject/Assets/Tests/PlayMode/MultiplayerClientPlayTests.cs).
- **Pending Live Unity Client Run**: Launching multiple live compiled Unity Editor/Player binaries against the local Node server remains pending until Drive `D:` (`Unity 6000.6.10f1`) is restored.
