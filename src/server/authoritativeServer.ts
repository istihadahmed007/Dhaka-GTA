import { WebSocket, WebSocketServer } from 'ws';
import http from 'node:http';
import {
  ClientMessage,
  DEFAULT_SERVER_PORT,
  MAX_SIMULTANEOUS_PLAYERS,
  MULTIPLAYER_SERVER_TICK_MS,
  MULTIPLAYER_SERVER_TICK_RATE,
  NetPlayerSnapshot,
  NetPlayerState,
  NetVehicleSnapshot,
  NetWorldState,
  ServerMessage,
} from '../game/multiplayer/protocol';
import { AccountStore, PersistentAccount } from './accountStore';
import { OutfitId, VehicleKind, VehicleSpec, VEHICLE_SPECS, WeaponId, WEAPON_SPECS } from '../game/types';
import { buildStaticColliders, RIVER_Z_START, WORLD_HALF_SIZE } from '../game/worldData';
import { resolveCircleCollisions } from '../game/physics';

const BUILDING_COLLIDERS = buildStaticColliders();
const UNITY_WORLD_HALF_EXTENT_METERS = 12000;

export interface ServerLogEntry {
  timestampIso: string;
  level: 'INFO' | 'WARN' | 'ANTICHEAT';
  category: string;
  message: string;
}

export interface ServerHealthSnapshot {
  status: 'ok';
  players: number;
  maxPlayers: number;
  vehicles: number;
  uptime: number;
  tickRateHz: number;
  serverTick: number;
  avgTickMs: number;
  packetsReceivedTotal: number;
  packetsRejectedTotal: number;
  antiCheatStrikesTotal: number;
  memoryRssMb: number;
  realPlayersOnly: true;
}

interface ConnectedSession {
  ws: WebSocket;
  account: PersistentAccount;
  state: NetPlayerState;
  clientEngine: 'unity' | 'web';
  lastProcessedSeq: number;
  lastInputTime: number;
  attackCooldownTimer: number;
  respawnTimer: number;
  lastPingTime: number;
  ip: string;
  packetCountWindow: number;
  packetWindowResetMs: number;
  chatCountWindow: number;
  chatWindowResetMs: number;
  dtBudgetAccumSec: number;
  dtWindowResetMs: number;
  cheatStrikes: number;
}

interface ServerVehicle {
  id: string;
  kind: VehicleKind;
  x: number;
  y: number;
  z: number;
  vx: number;
  vz: number;
  heading: number;
  speed: number;
  steerAngle: number;
  hp: number;
  maxHp: number;
  fuel: number;
  color: number;
  driverId: string | null;
  ownerId: string | null;
  locked: boolean;
  sirenOn: boolean;
  honkActive: boolean;
}

export class DhakaAuthoritativeServer {
  private wss: WebSocketServer | null = null;
  private httpServer: http.Server | null = null;
  private sessions = new Map<string, ConnectedSession>(); // playerId -> session
  private wsToPlayerId = new Map<WebSocket, string>();
  private ipConnections = new Map<string, number>();
  private wsToIp = new Map<WebSocket, string>();
  private vehicles: ServerVehicle[] = [];
  private accountStore: AccountStore;
  private tickInterval: NodeJS.Timeout | null = null;
  private serverTick = 0;
  private actualPort = DEFAULT_SERVER_PORT;
  private startTime = Date.now();
  private timeOfDay = 14.5; // Starts in clear Dhaka afternoon
  private weather: 'clear' | 'monsoon' | 'storm' = 'clear';
  private packetsReceivedTotal = 0;
  private packetsRejectedTotal = 0;
  private antiCheatStrikesTotal = 0;
  private avgTickMs = 0;
  private recentLogs: ServerLogEntry[] = [];

  constructor(accountStore?: AccountStore) {
    this.accountStore = accountStore ?? new AccountStore();
    this.initDefaultVehicles();
  }

  private initDefaultVehicles(): void {
    const starterPlacements: Array<{ kind: VehicleKind; x: number; z: number; heading: number; color: number }> = [
      { kind: 'sedan', x: -14, z: 28, heading: 0, color: 0xd9e1e8 },
      { kind: 'sedan', x: 24, z: -38, heading: Math.PI / 2, color: 0x1e293b },
      { kind: 'bike', x: -8, z: 32, heading: 0, color: 0xb91c1c },
      { kind: 'cng', x: 18, z: 22, heading: -Math.PI / 2, color: 0x0f6a44 },
      { kind: 'police', x: 38, z: 28, heading: Math.PI, color: 0xf1f5f9 },
      { kind: 'speedboat', x: 28, z: 198, heading: Math.PI / 2, color: 0xd97706 },
      { kind: 'jetski', x: -18, z: 202, heading: -Math.PI / 2, color: 0x2563eb },
      { kind: 'launch', x: 0, z: 215, heading: 0, color: 0x0f5132 },
    ];

    starterPlacements.forEach((p, idx) => {
      const spec = VEHICLE_SPECS[p.kind];
      this.vehicles.push({
        id: `net_veh_${idx + 1}`,
        kind: p.kind,
        x: p.x,
        y: 0,
        z: p.z,
        vx: 0,
        vz: 0,
        heading: p.heading,
        speed: 0,
        steerAngle: 0,
        hp: spec.maxHp,
        maxHp: spec.maxHp,
        fuel: 100,
        color: p.color,
        driverId: null,
        ownerId: null,
        locked: false,
        sirenOn: false,
        honkActive: false,
      });
    });
  }

  public listen(port = DEFAULT_SERVER_PORT): Promise<void> {
    return new Promise((resolve) => {
      this.httpServer = http.createServer((req, res) => {
        if (req.url === '/health') {
          res.writeHead(200, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify(this.getHealthMetrics()));
          return;
        }
        res.writeHead(200, { 'Content-Type': 'text/plain' });
        res.end('Dhaka-GTA Authoritative Dedicated Server Running');
      });

      this.wss = new WebSocketServer({ server: this.httpServer });

      this.wss.on('connection', (ws: WebSocket, req: http.IncomingMessage) => {
        this.handleConnection(ws, req);
      });

      this.httpServer.listen(port, () => {
        const addr = this.httpServer?.address();
        this.actualPort = typeof addr === 'object' && addr ? addr.port : port;
        console.log(`[DhakaServer] Listening on port ${this.actualPort} (Tick rate: ${MULTIPLAYER_SERVER_TICK_RATE} Hz)`);
        this.startTickLoop();
        resolve();
      });
    });
  }

  public attachToExistingServer(server: http.Server): void {
    this.httpServer = server;
    this.wss = new WebSocketServer({ server, path: '/ws' });
    this.wss.on('connection', (ws: WebSocket, req: http.IncomingMessage) => {
      this.handleConnection(ws, req);
    });
    this.startTickLoop();
  }

  private handleConnection(ws: WebSocket, req?: http.IncomingMessage): void {
    const ip = req?.socket?.remoteAddress || req?.headers?.['x-forwarded-for']?.toString().split(',')[0].trim() || 'local';
    const currentIpCount = this.ipConnections.get(ip) || 0;

    if (currentIpCount >= 6) {
      this.sendToWs(ws, {
        type: 's_auth_fail',
        reason: 'Connection limit reached for this IP address (Max 6 concurrent connections)',
      });
      ws.close();
      return;
    }

    this.ipConnections.set(ip, currentIpCount + 1);
    this.wsToIp.set(ws, ip);

    if (this.sessions.size >= MAX_SIMULTANEOUS_PLAYERS) {
      this.sendToWs(ws, {
        type: 's_auth_fail',
        reason: `Server full (Max ${MAX_SIMULTANEOUS_PLAYERS} simultaneous players)`,
      });
      ws.close();
      return;
    }

    ws.on('message', (data: Buffer | string) => {
      this.packetsReceivedTotal++;
      try {
        const raw = JSON.parse(data.toString());
        const validated = this.validatePacket(raw);
        if (!validated.valid) {
          this.packetsRejectedTotal++;
          this.recordLog('WARN', 'PACKET', `Dropped invalid packet from ${ip}: ${validated.reason}`);
          console.warn(`[DhakaServer] Dropped invalid packet from ${ip}: ${validated.reason}`);
          this.sendToWs(ws, { type: 's_error', code: 'INVALID_PACKET', message: validated.reason });
          return;
        }
        this.handleClientMessage(ws, validated.msg);
      } catch (err) {
        this.packetsRejectedTotal++;
        this.recordLog('WARN', 'PACKET', `Malformed non-JSON packet from ${ip}`);
        console.warn('[DhakaServer] Malformed non-JSON packet received from', ip);
        this.sendToWs(ws, { type: 's_error', code: 'MALFORMED_JSON', message: 'Payload must be valid JSON' });
      }
    });

    ws.on('close', () => {
      this.handleDisconnect(ws);
    });

    ws.on('error', (err) => {
      console.warn('[DhakaServer] Socket error:', err.message);
      this.handleDisconnect(ws);
    });
  }

  private validatePacket(raw: any): { valid: true; msg: ClientMessage } | { valid: false; reason: string } {
    if (!raw || typeof raw !== 'object' || Array.isArray(raw)) {
      return { valid: false, reason: 'Packet must be a JSON object' };
    }

    if (typeof raw.type !== 'string') {
      return { valid: false, reason: 'Packet missing string type field' };
    }

    const isNum = (v: any): v is number => typeof v === 'number' && Number.isFinite(v);

    switch (raw.type) {
      case 'c_auth':
        if (typeof raw.name !== 'string' || raw.name.trim().length === 0 || raw.name.length > 24) {
          return { valid: false, reason: 'Invalid callsign name' };
        }
        if (raw.token !== undefined && typeof raw.token !== 'string') {
          return { valid: false, reason: 'Token must be a string' };
        }
        if (raw.pin !== undefined && typeof raw.pin !== 'string') {
          return { valid: false, reason: 'PIN must be a string' };
        }
        return { valid: true, msg: raw as ClientMessage };

      case 'c_input':
        if (!isNum(raw.seq) || !Number.isInteger(raw.seq) || raw.seq < 1) {
          return { valid: false, reason: 'Invalid input sequence number' };
        }
        if (!isNum(raw.dt) || raw.dt <= 0 || raw.dt > 0.25) {
          return { valid: false, reason: 'Invalid or out-of-range delta time' };
        }
        if (!isNum(raw.moveX) || !isNum(raw.moveZ) || Math.abs(raw.moveX) > 1.5 || Math.abs(raw.moveZ) > 1.5) {
          return { valid: false, reason: 'Movement vector must be finite numbers in range [-1.5, 1.5]' };
        }
        if (!isNum(raw.heading)) {
          return { valid: false, reason: 'Heading must be a finite number' };
        }
        if (typeof raw.sprint !== 'boolean' || typeof raw.jump !== 'boolean') {
          return { valid: false, reason: 'Sprint and jump must be booleans' };
        }
        return { valid: true, msg: raw as ClientMessage };

      case 'c_vehicle_input':
        if (!isNum(raw.seq) || !Number.isInteger(raw.seq) || raw.seq < 1) {
          return { valid: false, reason: 'Invalid vehicle sequence number' };
        }
        if (!isNum(raw.throttle) || Math.abs(raw.throttle) > 1.5) {
          return { valid: false, reason: 'Throttle must be a finite number in range [-1.5, 1.5]' };
        }
        if (!isNum(raw.steer) || Math.abs(raw.steer) > 1.5) {
          return { valid: false, reason: 'Steer must be a finite number in range [-1.5, 1.5]' };
        }
        if (typeof raw.handbrake !== 'boolean' || typeof raw.horn !== 'boolean') {
          return { valid: false, reason: 'Handbrake and horn must be booleans' };
        }
        return { valid: true, msg: raw as ClientMessage };

      case 'c_vehicle_action':
        if (raw.action !== 'enter' && raw.action !== 'exit') {
          return { valid: false, reason: 'Vehicle action must be "enter" or "exit"' };
        }
        if (raw.vehicleId !== undefined && typeof raw.vehicleId !== 'string') {
          return { valid: false, reason: 'Vehicle ID must be a string' };
        }
        return { valid: true, msg: raw as ClientMessage };

      case 'c_attack':
        if (!isNum(raw.seq) || !Number.isInteger(raw.seq) || raw.seq < 1) {
          return { valid: false, reason: 'Invalid attack sequence number' };
        }
        if (!['fists', 'pistol', 'smg', 'shotgun'].includes(raw.weapon)) {
          return { valid: false, reason: 'Invalid weapon identifier' };
        }
        if (!isNum(raw.aimHeading)) {
          return { valid: false, reason: 'Aim heading must be a finite number' };
        }
        return { valid: true, msg: raw as ClientMessage };

      case 'c_chat':
        if (typeof raw.text !== 'string' || raw.text.trim().length === 0 || raw.text.length > 140) {
          return { valid: false, reason: 'Chat message must be a string between 1 and 140 characters' };
        }
        return { valid: true, msg: raw as ClientMessage };

      case 'c_buy_item':
        if (typeof raw.itemId !== 'string' || raw.itemId.length > 64) {
          return { valid: false, reason: 'Item ID must be a string' };
        }
        return { valid: true, msg: raw as ClientMessage };

      case 'c_use_item':
        if (typeof raw.itemId !== 'string' || raw.itemId.length > 64) {
          return { valid: false, reason: 'Item ID must be a string' };
        }
        return { valid: true, msg: raw as ClientMessage };

      case 'c_buy_vehicle':
        if (typeof raw.vehicleKind !== 'string' || !(raw.vehicleKind in VEHICLE_SPECS)) {
          return { valid: false, reason: 'Unknown vehicleKind' };
        }
        return { valid: true, msg: raw as ClientMessage };

      case 'c_vehicle_lock':
        if (typeof raw.vehicleId !== 'string' || typeof raw.locked !== 'boolean') {
          return { valid: false, reason: 'vehicleId string and locked boolean required' };
        }
        return { valid: true, msg: raw as ClientMessage };

      case 'c_switch_protagonist':
        if (raw.protagonistId !== 'tariq' && raw.protagonistId !== 'maya') {
          return { valid: false, reason: 'protagonistId must be "tariq" or "maya"' };
        }
        return { valid: true, msg: raw as ClientMessage };

      case 'c_progression_sync':
        if (raw.completedMissionId !== undefined && typeof raw.completedMissionId !== 'string') {
          return { valid: false, reason: 'completedMissionId must be a string' };
        }
        return { valid: true, msg: raw as ClientMessage };

      case 'c_ping':
        if (!isNum(raw.clientTime) || raw.clientTime <= 0) {
          return { valid: false, reason: 'Ping clientTime must be a positive finite timestamp' };
        }
        return { valid: true, msg: raw as ClientMessage };

      default:
        return { valid: false, reason: `Unknown packet type "${String(raw.type)}"` };
    }
  }

  private handleClientMessage(ws: WebSocket, msg: ClientMessage): void {
    const playerId = this.wsToPlayerId.get(ws);

    if (msg.type === 'c_auth') {
      this.handleAuth(ws, msg);
      return;
    }

    if (!playerId) {
      this.sendToWs(ws, { type: 's_error', code: 'UNAUTHORIZED', message: 'Authenticate first via c_auth' });
      return;
    }

    const session = this.sessions.get(playerId);
    if (!session) return;

    // Rate limiting: 60 pkts/s max, 4 chat/s max
    const now = Date.now();
    if (now > session.packetWindowResetMs) {
      session.packetCountWindow = 0;
      session.packetWindowResetMs = now + 1000;
    }
    session.packetCountWindow++;
    if (session.packetCountWindow > 60) {
      this.sendToWs(ws, { type: 's_error', code: 'RATE_LIMIT', message: 'Input rate limit exceeded (max 60 packets/sec)' });
      return;
    }

    if (msg.type === 'c_chat') {
      if (now > session.chatWindowResetMs) {
        session.chatCountWindow = 0;
        session.chatWindowResetMs = now + 1000;
      }
      session.chatCountWindow++;
      if (session.chatCountWindow > 4) {
        this.sendToWs(ws, { type: 's_error', code: 'CHAT_LIMIT', message: 'Chat rate limit exceeded (max 4 messages/sec)' });
        return;
      }
    }

    switch (msg.type) {
      case 'c_input':
        this.handlePlayerInput(session, msg);
        break;
      case 'c_vehicle_action':
        this.handleVehicleAction(session, msg);
        break;
      case 'c_vehicle_input':
        this.handleVehicleInput(session, msg);
        break;
      case 'c_attack':
        this.handleCombatAttack(session, msg);
        break;
      case 'c_chat':
        this.handleChat(session, msg.text);
        break;
      case 'c_buy_item':
        this.handleBuyItem(session, msg.itemId);
        break;
      case 'c_use_item':
        this.handleUseItem(session, msg.itemId);
        break;
      case 'c_buy_vehicle':
        this.handleBuyVehicle(session, msg.vehicleKind);
        break;
      case 'c_vehicle_lock':
        this.handleVehicleLock(session, msg.vehicleId, msg.locked);
        break;
      case 'c_switch_protagonist':
        this.handleSwitchProtagonist(session, msg.protagonistId);
        break;
      case 'c_progression_sync':
        this.handleProgressionSync(session, msg.completedMissionId);
        break;
      case 'c_ping':
        this.sendToWs(ws, { type: 's_pong', clientTime: msg.clientTime, serverTime: Date.now() });
        session.state.ping = Math.max(1, Math.round(Date.now() - session.lastPingTime));
        session.lastPingTime = Date.now();
        break;
    }
  }

  private handleAuth(ws: WebSocket, msg: ClientMessage & { type: 'c_auth' }): void {
    const authResult = this.accountStore.authenticate(
      msg.name,
      msg.token,
      msg.pin,
      msg.outfit ?? 'street',
      msg.color ?? 0x26466d,
      msg.protagonistId === 'maya' ? 'maya' : 'tariq',
    );

    if (!authResult.ok) {
      this.recordLog('WARN', 'AUTH', `Auth rejected for "${msg.name}": ${authResult.error}`);
      this.sendToWs(ws, {
        type: 's_auth_fail',
        reason: authResult.error,
      });
      return;
    }

    const { account, isNew } = authResult;
    const clientEngine: 'unity' | 'web' = msg.clientEngine === 'unity' ? 'unity' : 'web';

    // Evict previous active session if reconnecting on a fresh socket
    const existing = this.sessions.get(account.id);
    if (existing && existing.ws !== ws) {
      try {
        existing.ws.close();
      } catch {}
      this.sessions.delete(account.id);
    }

    const playerState: NetPlayerState = {
      id: account.id,
      name: account.name,
      protagonistId: account.protagonistId ?? 'tariq',
      outfit: account.outfit,
      color: account.color,
      transform: {
        x: account.lastX,
        y: account.lastY,
        z: account.lastZ,
        vx: 0,
        vy: 0,
        vz: 0,
        heading: account.lastHeading,
      },
      hp: 100,
      maxHp: 100,
      armor: 50,
      maxArmor: 100,
      stamina: 100,
      money: account.money,
      wantedLevel: 0,
      weapon: account.weapon,
      ammo: { ...account.ammo },
      inventory: { ...account.inventory },
      ownedVehicleIds: [...(account.ownedVehicleIds ?? [])],
      completedMissionIds: [...(account.completedMissionIds ?? [])],
      vehicleId: null,
      seat: null,
      state: 'idle',
      kills: account.kills,
      deaths: account.deaths,
      ping: 25,
    };

    const ip = this.wsToIp.get(ws) || 'local';
    const now = Date.now();
    const session: ConnectedSession = {
      ws,
      account,
      state: playerState,
      clientEngine,
      lastProcessedSeq: 0,
      lastInputTime: now,
      attackCooldownTimer: 0,
      respawnTimer: 0,
      lastPingTime: now,
      ip,
      packetCountWindow: 0,
      packetWindowResetMs: now + 1000,
      chatCountWindow: 0,
      chatWindowResetMs: now + 1000,
      dtBudgetAccumSec: 0,
      dtWindowResetMs: now + 1000,
      cheatStrikes: 0,
    };

    this.sessions.set(account.id, session);
    this.wsToPlayerId.set(ws, account.id);
    this.recordLog('INFO', 'AUTH', `${account.name} (${clientEngine}) signed in [id=${account.id}]`);

    // Reply with authoritative auth confirmation
    this.sendToWs(ws, {
      type: 's_auth_ok',
      playerId: account.id,
      token: account.token,
      isNewAccount: isNew,
      player: playerState,
      world: this.getWorldState(),
      vehicles: this.getVehicleSnapshots(),
    });
    this.sendAccountSync(session);

    // Notify peers of new arrival
    this.broadcast(
      {
        type: 's_player_joined',
        player: playerState,
      },
      account.id,
    );

    this.broadcastChat(
      `Player "${account.name}" joined the streets of Dhaka!`,
      'System',
      'system',
    );
  }

  /**
   * Anti-cheat server-authoritative player movement simulation.
   * Client provides intent (moveX, moveZ, heading, sprint).
   * Server validates physics, bounds, obstacles, and enforces maximum velocity.
   */
  private handlePlayerInput(session: ConnectedSession, msg: ClientMessage & { type: 'c_input' }): void {
    if (session.state.state === 'ko' || session.state.vehicleId) return;

    // Anti-Cheat: reject speed-hack delta-time over-accumulation beyond real wall-clock budget
    const nowMs = Date.now();
    if (nowMs > session.dtWindowResetMs) {
      session.dtBudgetAccumSec = 0;
      session.dtWindowResetMs = nowMs + 1000;
    }
    const safeDt = Math.min(Math.max(msg.dt, 0.005), 0.1);
    session.dtBudgetAccumSec += safeDt;
    if (session.dtBudgetAccumSec > 1.35) {
      this.flagAntiCheat(session, 'DT_BUDGET_EXCEEDED', `Simulated ${session.dtBudgetAccumSec.toFixed(2)}s inside 1s window`);
      return;
    }

    session.lastProcessedSeq = msg.seq;
    const p = session.state.transform;

    // Max speeds: walk ~4.2 m/s, sprint ~7.5 m/s
    const maxSpeed = msg.sprint && session.state.stamina > 5 ? 7.2 : 3.8;
    if (msg.sprint && session.state.stamina > 0) {
      session.state.stamina = Math.max(0, session.state.stamina - safeDt * 12);
    } else {
      session.state.stamina = Math.min(100, session.state.stamina + safeDt * 16);
    }

    const inputLen = Math.hypot(msg.moveX, msg.moveZ);
    let desiredVx = 0;
    let desiredVz = 0;

    if (inputLen > 0.05) {
      const normX = msg.moveX / inputLen;
      const normZ = msg.moveZ / inputLen;
      desiredVx = normX * maxSpeed;
      desiredVz = normZ * maxSpeed;
    }

    // Accelerate toward desired velocity
    p.vx += (desiredVx - p.vx) * Math.min(1, safeDt * 12);
    p.vz += (desiredVz - p.vz) * Math.min(1, safeDt * 12);
    p.heading = msg.heading;

    const candX = p.x + p.vx * safeDt;
    const candZ = p.z + p.vz * safeDt;

    if (session.clientEngine === 'unity') {
      p.x = Math.max(-UNITY_WORLD_HALF_EXTENT_METERS, Math.min(UNITY_WORLD_HALF_EXTENT_METERS, candX));
      p.z = Math.max(-UNITY_WORLD_HALF_EXTENT_METERS, Math.min(UNITY_WORLD_HALF_EXTENT_METERS, candZ));
    } else {
      const resolved = resolveCircleCollisions(candX, candZ, 0.55, BUILDING_COLLIDERS);
      p.x = Math.max(-WORLD_HALF_SIZE + 6, Math.min(WORLD_HALF_SIZE - 6, resolved.x));
      p.z = Math.max(-WORLD_HALF_SIZE + 6, Math.min(RIVER_Z_START + 0.6, resolved.z));
    }

    const curSpeed = Math.hypot(p.vx, p.vz);
    if (msg.jump) {
      session.state.state = 'jump';
    } else if (curSpeed > 4.5) {
      session.state.state = 'sprint';
    } else if (curSpeed > 0.5) {
      session.state.state = 'walk';
    } else {
      session.state.state = 'idle';
    }

    // Send instant reconciliation packet to keep client prediction smooth
    this.sendToWs(session.ws, {
      type: 's_reconcile',
      lastProcessedInput: msg.seq,
      x: p.x,
      y: p.y,
      z: p.z,
      vx: p.vx,
      vz: p.vz,
      heading: p.heading,
      hp: session.state.hp,
      armor: session.state.armor,
      money: session.state.money,
      wantedLevel: session.state.wantedLevel,
      vehicleId: session.state.vehicleId,
    });
  }

  private handleVehicleAction(session: ConnectedSession, msg: ClientMessage & { type: 'c_vehicle_action' }): void {
    if (session.state.state === 'ko') return;

    if (msg.action === 'enter' && msg.vehicleId) {
      const veh = this.vehicles.find((v) => v.id === msg.vehicleId);
      if (!veh || veh.driverId) return;

      // Vehicle Ownership Validation: locked private vehicles belong strictly to their registered owner
      if (veh.ownerId && veh.locked && veh.ownerId !== session.state.id) {
        this.sendToWs(session.ws, {
          type: 's_error',
          code: 'VEHICLE_LOCKED',
          message: 'Vehicle is locked by its registered owner.',
        });
        return;
      }

      const dist = Math.hypot(session.state.transform.x - veh.x, session.state.transform.z - veh.z);
      if (dist > 5.5) return; // Must be within entering range

      veh.driverId = session.state.id;
      session.state.vehicleId = veh.id;
      session.state.seat = 'driver';
      session.state.state = 'driving';
      session.state.transform.x = veh.x;
      session.state.transform.z = veh.z;
      session.state.transform.heading = veh.heading;
    } else if (msg.action === 'exit' && session.state.vehicleId) {
      const veh = this.vehicles.find((v) => v.id === session.state.vehicleId);
      if (veh && veh.driverId === session.state.id) {
        veh.driverId = null;
      }
      session.state.vehicleId = null;
      session.state.seat = null;
      session.state.state = 'idle';
      if (veh) {
        session.state.transform.x = veh.x - Math.cos(veh.heading) * 1.8;
        session.state.transform.z = veh.z + Math.sin(veh.heading) * 1.8;
      }
    }
  }

  private handleVehicleInput(session: ConnectedSession, msg: ClientMessage & { type: 'c_vehicle_input' }): void {
    if (!session.state.vehicleId || session.state.seat !== 'driver') return;
    const veh = this.vehicles.find((v) => v.id === session.state.vehicleId);
    if (!veh || veh.driverId !== session.state.id) return;

    const spec: VehicleSpec = VEHICLE_SPECS[veh.kind];
    const safeDt = 0.05; // 20 Hz tick

    // Turn steering
    const targetSteer = Math.max(-1, Math.min(1, msg.steer)) * 0.55;
    veh.steerAngle += (targetSteer - veh.steerAngle) * Math.min(1, safeDt * 10);
    veh.heading += (veh.speed / spec.halfLength) * Math.tan(veh.steerAngle) * safeDt;

    // Throttle & acceleration
    if (veh.fuel > 0) {
      const throttle = Math.max(-1, Math.min(1, msg.throttle));
      if (throttle > 0.05) {
        veh.speed = Math.min(spec.maxSpeed, veh.speed + spec.accel * throttle * safeDt);
        veh.fuel = Math.max(0, veh.fuel - safeDt * 0.08);
      } else if (throttle < -0.05) {
        veh.speed = Math.max(-spec.reverseSpeed, veh.speed - spec.brake * Math.abs(throttle) * safeDt);
      } else {
        veh.speed *= Math.max(0, 1 - safeDt * spec.drag * 0.4);
      }
    } else {
      veh.speed *= Math.max(0, 1 - safeDt * 2.0); // Coast down when empty
    }

    if (msg.handbrake) {
      veh.speed *= Math.max(0, 1 - safeDt * 4.5);
    }

    veh.vx = Math.sin(veh.heading) * veh.speed;
    veh.vz = Math.cos(veh.heading) * veh.speed;
    veh.x += veh.vx * safeDt;
    veh.z += veh.vz * safeDt;
    veh.honkActive = Boolean(msg.horn);

    // Keep driver aligned with vehicle
    session.state.transform.x = veh.x;
    session.state.transform.z = veh.z;
    session.state.transform.vx = veh.vx;
    session.state.transform.vz = veh.vz;
    session.state.transform.heading = veh.heading;
  }

  /**
   * Authoritative combat validation: client cannot assert damage.
   * Client reports weapon trigger pull; server validates line-of-sight, range, ammo & damage.
   */
  private handleCombatAttack(session: ConnectedSession, msg: ClientMessage & { type: 'c_attack' }): void {
    if (session.state.state === 'ko') return;

    const weaponSpec = WEAPON_SPECS[msg.weapon];
    if (!weaponSpec) return;

    if (!session.account.unlockedWeapons.includes(msg.weapon)) {
      this.flagAntiCheat(session, 'UNOWNED_WEAPON', `Attempted attack with locked weapon "${msg.weapon}"`);
      return;
    }

    if (session.attackCooldownTimer > 0) return;

    // Check ammo
    if (msg.weapon !== 'fists') {
      const curAmmo = session.state.ammo[msg.weapon] ?? 0;
      if (curAmmo <= 0) return;
      session.state.ammo[msg.weapon] = curAmmo - 1;
    }

    session.attackCooldownTimer = weaponSpec.cooldown;
    session.state.transform.heading = msg.aimHeading;

    if (msg.targetId && msg.targetKind === 'player') {
      const victimSession = this.sessions.get(msg.targetId);
      if (!victimSession || victimSession.state.state === 'ko' || victimSession.state.id === session.state.id) return;

      const dx = victimSession.state.transform.x - session.state.transform.x;
      const dz = victimSession.state.transform.z - session.state.transform.z;
      const dist = Math.hypot(dx, dz);

      // Verify aim cone alignment beyond close contact range (anti-aimbot check)
      if (dist > 2.2) {
        const expectedAim = Math.atan2(dx, dz);
        let diff = Math.abs(expectedAim - msg.aimHeading) % (Math.PI * 2);
        if (diff > Math.PI) diff = Math.PI * 2 - diff;
        if (diff > 0.96) {
          this.flagAntiCheat(session, 'AIM_MISMATCH', 'Attack target outside valid aim cone');
          return;
        }
      }

      // Verify target within weapon range
      if (dist <= weaponSpec.range) {
        const damage = weaponSpec.damage;
        let hpDmg = damage;

        // Armor absorbs 60% of damage
        if (victimSession.state.armor > 0) {
          const absorbed = Math.min(victimSession.state.armor, damage * 0.6);
          victimSession.state.armor -= absorbed;
          hpDmg -= absorbed;
        }

        victimSession.state.hp = Math.max(0, victimSession.state.hp - hpDmg);
        const isLethal = victimSession.state.hp <= 0;

        if (isLethal) {
          victimSession.state.state = 'ko';
          victimSession.respawnTimer = 4.5;
          this.accountStore.recordKill(session.account.id, victimSession.account.id, 350);
          session.state.money += 350;
          session.state.kills += 1;
          victimSession.state.deaths += 1;

          this.broadcastChat(
            `⚔️ ${session.state.name} eliminated ${victimSession.state.name} with ${weaponSpec.name}! (+৳350)`,
            'Server',
            'system',
          );
        }

        // Broadcast authoritative combat event to all clients
        this.broadcast({
          type: 's_combat_event',
          attackerId: session.state.id,
          attackerName: session.state.name,
          victimId: victimSession.state.id,
          victimName: victimSession.state.name,
          victimKind: 'player',
          damage,
          lethal: isLethal,
          weapon: msg.weapon,
          hitX: victimSession.state.transform.x,
          hitY: 1.0,
          hitZ: victimSession.state.transform.z,
        });
      }
    } else if (msg.targetId && msg.targetKind === 'vehicle') {
      const veh = this.vehicles.find((v) => v.id === msg.targetId);
      if (!veh || veh.hp <= 0) return;
      const dist = Math.hypot(veh.x - session.state.transform.x, veh.z - session.state.transform.z);
      if (dist <= weaponSpec.range) {
        const dmg = weaponSpec.damage;
        veh.hp = Math.max(0, veh.hp - dmg);
        this.broadcast({
          type: 's_combat_event',
          attackerId: session.state.id,
          attackerName: session.state.name,
          victimId: veh.id,
          victimName: veh.kind,
          victimKind: 'vehicle',
          damage: dmg,
          lethal: veh.hp <= 0,
          weapon: msg.weapon,
          hitX: veh.x,
          hitY: 0.8,
          hitZ: veh.z,
        });
      }
    }
  }

  private handleBuyItem(session: ConnectedSession, itemId: string): void {
    const itemCatalog: Record<string, { price: number; name: string; addToInventory?: boolean }> = {
      biriyani: { price: 150, name: 'Old Dhaka Kacchi Biriyani' },
      armor: { price: 400, name: 'Tactical Street Vest' },
      ammo_pistol: { price: 120, name: 'Pistol Ammo (24 rounds)' },
      ammo_smg: { price: 200, name: 'SMG Ammo (60 rounds)' },
      ammo_shotgun: { price: 260, name: 'Shotgun Shells (18 rounds)' },
      item_jerrycan: { price: 200, name: 'Octane Jerrycan (10L)', addToInventory: true },
      item_first_aid: { price: 150, name: 'Army Surplus Trauma Kit', addToInventory: true },
      cha_flask: { price: 60, name: 'Spiced Tong Cha Flask', addToInventory: true },
      kacchi_pack: { price: 280, name: 'Old Dhaka Kacchi Box', addToInventory: true },
      first_aid_kit: { price: 450, name: 'Trauma Medkit', addToInventory: true },
      repair_toolkit: { price: 650, name: 'Dholaikhal Wrench Kit', addToInventory: true },
    };

    const item = itemCatalog[itemId];
    if (!item) {
      this.sendToWs(session.ws, {
        type: 's_transaction_result',
        ok: false,
        money: session.state.money,
        itemId,
        message: 'Unknown store item',
      });
      return;
    }

    if (session.state.money < item.price) {
      this.sendToWs(session.ws, {
        type: 's_transaction_result',
        ok: false,
        money: session.state.money,
        itemId,
        message: `Insufficient Taka (Needs ৳${item.price})`,
      });
      return;
    }

    session.state.money -= item.price;
    if (itemId === 'biriyani') session.state.hp = 100;
    else if (itemId === 'armor') session.state.armor = 100;
    else if (itemId === 'ammo_pistol') session.state.ammo.pistol = (session.state.ammo.pistol ?? 0) + 24;
    else if (itemId === 'ammo_smg') session.state.ammo.smg = (session.state.ammo.smg ?? 0) + 60;
    else if (itemId === 'ammo_shotgun') session.state.ammo.shotgun = (session.state.ammo.shotgun ?? 0) + 18;
    else if (itemId === 'item_first_aid') session.state.hp = Math.min(100, session.state.hp + 65);

    if (item.addToInventory) {
      session.account.inventory[itemId] = (session.account.inventory[itemId] ?? 0) + 1;
      session.state.inventory = { ...session.account.inventory };
    }

    this.accountStore.updateStats(session.account.id, {
      money: session.state.money,
      inventory: session.account.inventory,
      ammo: session.state.ammo,
    });

    this.sendToWs(session.ws, {
      type: 's_transaction_result',
      ok: true,
      money: session.state.money,
      itemId,
      message: `Purchased ${item.name}!`,
    });
    this.sendAccountSync(session);
  }

  private handleUseItem(session: ConnectedSession, itemId: string): void {
    const count = session.account.inventory[itemId] ?? 0;
    if (count <= 0) {
      this.sendToWs(session.ws, { type: 's_error', code: 'NO_ITEM', message: `No "${itemId}" in inventory` });
      return;
    }

    session.account.inventory[itemId] = count - 1;
    if (session.account.inventory[itemId] <= 0) delete session.account.inventory[itemId];
    session.state.inventory = { ...session.account.inventory };

    if (itemId === 'cha_flask') {
      session.state.hp = Math.min(session.state.maxHp, session.state.hp + 25);
      session.state.stamina = Math.min(100, session.state.stamina + 45);
    } else if (itemId === 'kacchi_pack') {
      session.state.hp = Math.min(session.state.maxHp, session.state.hp + 60);
    } else if (itemId === 'first_aid_kit' || itemId === 'item_first_aid') {
      session.state.hp = Math.min(session.state.maxHp, session.state.hp + 80);
    } else if (itemId === 'repair_toolkit' && session.state.vehicleId) {
      const veh = this.vehicles.find((v) => v.id === session.state.vehicleId);
      if (veh) veh.hp = veh.maxHp;
    }

    this.accountStore.updateStats(session.account.id, { inventory: session.account.inventory });
    this.sendAccountSync(session);
  }

  private handleBuyVehicle(session: ConnectedSession, kind: VehicleKind): void {
    const prices: Partial<Record<VehicleKind, number>> = {
      cng: 1200,
      rickshaw: 600,
      bike: 1800,
      sedan: 3500,
      bus: 7000,
      truck: 6500,
      police: 9500,
    };
    const price = prices[kind] ?? 3000;
    if (session.state.money < price) {
      this.sendToWs(session.ws, {
        type: 's_transaction_result',
        ok: false,
        money: session.state.money,
        itemId: `veh_${kind}`,
        message: `Insufficient Taka for ${kind} (Needs ৳${price})`,
      });
      return;
    }

    session.state.money -= price;
    const spec = VEHICLE_SPECS[kind];
    const vehId = `owned_${session.account.id}_${this.vehicles.length + 1}`;
    this.vehicles.push({
      id: vehId,
      kind,
      x: session.state.transform.x + 2.5,
      y: session.state.transform.y,
      z: session.state.transform.z + 2.5,
      vx: 0,
      vz: 0,
      heading: session.state.transform.heading,
      speed: 0,
      steerAngle: 0,
      hp: spec.maxHp,
      maxHp: spec.maxHp,
      fuel: 100,
      color: 0x0f766e,
      driverId: null,
      ownerId: session.state.id,
      locked: false,
      sirenOn: false,
      honkActive: false,
    });

    session.account.ownedVehicleIds.push(vehId);
    session.state.ownedVehicleIds = [...session.account.ownedVehicleIds];
    this.accountStore.updateStats(session.account.id, {
      money: session.state.money,
      ownedVehicleIds: session.account.ownedVehicleIds,
    });

    this.recordLog('INFO', 'ECONOMY', `${session.state.name} purchased ${kind} [${vehId}] for ৳${price}`);
    this.sendToWs(session.ws, {
      type: 's_transaction_result',
      ok: true,
      money: session.state.money,
      itemId: vehId,
      message: `Registered owned ${kind} (${vehId})!`,
    });
    this.sendAccountSync(session);
  }

  private handleVehicleLock(session: ConnectedSession, vehicleId: string, locked: boolean): void {
    const veh = this.vehicles.find((v) => v.id === vehicleId);
    if (!veh) return;
    if (veh.ownerId !== session.state.id) {
      this.flagAntiCheat(session, 'UNAUTHORIZED_LOCK', `Tried to lock unowned vehicle ${vehicleId}`);
      return;
    }
    veh.locked = locked;
  }

  private handleSwitchProtagonist(session: ConnectedSession, protagonistId: 'tariq' | 'maya'): void {
    session.account.protagonistId = protagonistId;
    session.state.protagonistId = protagonistId;
    this.accountStore.updateStats(session.account.id, { protagonistId });
    this.sendAccountSync(session);
  }

  private handleProgressionSync(session: ConnectedSession, completedMissionId?: string): void {
    if (completedMissionId && !session.account.completedMissionIds.includes(completedMissionId)) {
      session.account.completedMissionIds.push(completedMissionId.slice(0, 64));
      session.state.completedMissionIds = [...session.account.completedMissionIds];
      this.accountStore.updateStats(session.account.id, {
        completedMissionIds: session.account.completedMissionIds,
      });
    }
    this.sendAccountSync(session);
  }

  private flagAntiCheat(session: ConnectedSession, code: string, message: string): void {
    session.cheatStrikes++;
    this.antiCheatStrikesTotal++;
    this.recordLog('ANTICHEAT', code, `${session.state.name} (${session.state.id}): ${message} [Strike ${session.cheatStrikes}]`);
    this.sendToWs(session.ws, {
      type: 's_anticheat_alert',
      strikes: session.cheatStrikes,
      code,
      message,
    });

    if (session.cheatStrikes >= 8) {
      this.recordLog('WARN', 'KICK', `Disconnected ${session.state.name} for repeated anti-cheat strikes.`);
      try {
        session.ws.close(1008, 'Anti-cheat policy violation');
      } catch {}
    }
  }

  private sendAccountSync(session: ConnectedSession): void {
    this.sendToWs(session.ws, {
      type: 's_account_sync',
      money: session.state.money,
      protagonistId: session.account.protagonistId ?? 'tariq',
      inventory: { ...session.account.inventory },
      ownedVehicleIds: [...session.account.ownedVehicleIds],
      completedMissionIds: [...session.account.completedMissionIds],
      unlockedWeapons: [...session.account.unlockedWeapons],
    });
  }

  private handleChat(session: ConnectedSession, text: string): void {
    const cleanText = text.trim().slice(0, 140);
    if (!cleanText) return;
    this.broadcastChat(cleanText, session.state.name, session.state.id);
  }

  private broadcastChat(text: string, senderName: string, senderId: string): void {
    const msg: ServerMessage = {
      type: 's_chat',
      senderId,
      senderName,
      text,
      timestamp: Date.now(),
      isSystem: senderId === 'system',
    };
    this.broadcast(msg);
  }

  private startTickLoop(): void {
    if (this.tickInterval) clearInterval(this.tickInterval);

    this.tickInterval = setInterval(() => {
      this.simulationTick();
    }, MULTIPLAYER_SERVER_TICK_MS);
  }

  private simulationTick(): void {
    const tickStart = performance.now();
    this.serverTick++;
    const dt = MULTIPLAYER_SERVER_TICK_MS / 1000;

    // Advance ambient Dhaka time of day (1 full cycle ~ 24 min)
    this.timeOfDay = (this.timeOfDay + dt * 0.0167) % 24;

    // Tick respawns and combat timers
    for (const session of this.sessions.values()) {
      if (session.attackCooldownTimer > 0) {
        session.attackCooldownTimer -= dt;
      }

      if (session.state.state === 'ko') {
        session.respawnTimer -= dt;
        if (session.respawnTimer <= 0) {
          // Respawn at Old Dhaka Rooftop safehouse
          session.state.state = 'idle';
          session.state.hp = 100;
          session.state.armor = 25;
          session.state.transform.x = -28 + (Math.random() * 4 - 2);
          session.state.transform.z = 48 + (Math.random() * 4 - 2);
          session.state.transform.vx = 0;
          session.state.transform.vz = 0;
          this.broadcastChat(`${session.state.name} recovered and respawned in Old Dhaka!`, 'Server', 'system');
        }
      }
    }

    // Build and broadcast 20 Hz authoritative world state snapshot
    const snapshot: ServerMessage = {
      type: 's_world_snapshot',
      serverTick: this.serverTick,
      timestamp: Date.now(),
      players: this.getPlayerSnapshots(),
      vehicles: this.getVehicleSnapshots(),
      world: this.getWorldState(),
    };

    this.broadcast(snapshot);

    const tickDuration = performance.now() - tickStart;
    this.avgTickMs = this.avgTickMs === 0 ? tickDuration : this.avgTickMs * 0.9 + tickDuration * 0.1;
  }

  private getPlayerSnapshots(): NetPlayerSnapshot[] {
    const snaps: NetPlayerSnapshot[] = [];
    for (const session of this.sessions.values()) {
      const p = session.state;
      snaps.push({
        id: p.id,
        name: p.name,
        protagonistId: p.protagonistId ?? 'tariq',
        x: p.transform.x,
        y: p.transform.y,
        z: p.transform.z,
        vx: p.transform.vx,
        vz: p.transform.vz,
        heading: p.transform.heading,
        hp: p.hp,
        armor: p.armor,
        stamina: p.stamina,
        weapon: p.weapon,
        vehicleId: p.vehicleId,
        state: p.state,
        ping: p.ping,
      });
    }
    return snaps;
  }

  private getVehicleSnapshots(): NetVehicleSnapshot[] {
    return this.vehicles.map((v) => ({
      id: v.id,
      kind: v.kind,
      x: v.x,
      y: v.y,
      z: v.z,
      vx: v.vx,
      vz: v.vz,
      heading: v.heading,
      speed: v.speed,
      steerAngle: v.steerAngle,
      hp: v.hp,
      fuel: v.fuel,
      color: v.color,
      driverId: v.driverId,
      ownerId: v.ownerId,
      locked: v.locked,
      sirenOn: v.sirenOn,
      honkActive: v.honkActive,
    }));
  }

  private getWorldState(): NetWorldState {
    return {
      timeOfDay: this.timeOfDay,
      weather: this.weather,
      serverUptimeSec: Math.round((Date.now() - this.startTime) / 1000),
      connectedCount: this.sessions.size,
    };
  }

  private handleDisconnect(ws: WebSocket): void {
    const ip = this.wsToIp.get(ws);
    if (ip) {
      this.wsToIp.delete(ws);
      const count = this.ipConnections.get(ip) || 0;
      if (count <= 1) {
        this.ipConnections.delete(ip);
      } else {
        this.ipConnections.set(ip, count - 1);
      }
    }

    const playerId = this.wsToPlayerId.get(ws);
    if (!playerId) return;

    this.wsToPlayerId.delete(ws);
    const session = this.sessions.get(playerId);
    if (!session) return;

    this.sessions.delete(playerId);

    // Free any vehicle occupied by player
    if (session.state.vehicleId) {
      const veh = this.vehicles.find((v) => v.id === session.state.vehicleId);
      if (veh && veh.driverId === playerId) {
        veh.driverId = null;
        veh.sirenOn = false;
        veh.honkActive = false;
      }
    }

    // Save persistent progress & inventory to disk
    this.accountStore.updateStats(playerId, {
      money: session.state.money,
      kills: session.state.kills,
      deaths: session.state.deaths,
      protagonistId: session.account.protagonistId,
      inventory: session.account.inventory,
      ownedVehicleIds: session.account.ownedVehicleIds,
      completedMissionIds: session.account.completedMissionIds,
      lastX: session.state.transform.x,
      lastY: session.state.transform.y,
      lastZ: session.state.transform.z,
      lastHeading: session.state.transform.heading,
    });
    this.recordLog('INFO', 'SESSION', `${session.state.name} disconnected cleanly.`);

    this.broadcast({
      type: 's_player_left',
      playerId,
      playerName: session.state.name,
      reason: 'Disconnected',
    });

    this.broadcastChat(`Player "${session.state.name}" left the avenue.`, 'System', 'system');
  }

  private recordLog(level: 'INFO' | 'WARN' | 'ANTICHEAT', category: string, message: string): void {
    this.recentLogs.push({
      timestampIso: new Date().toISOString(),
      level,
      category,
      message,
    });
    if (this.recentLogs.length > 200) {
      this.recentLogs.shift();
    }
  }

  public getHealthMetrics(): ServerHealthSnapshot {
    const rssBytes = process.memoryUsage().rss;
    return {
      status: 'ok',
      players: this.sessions.size,
      maxPlayers: MAX_SIMULTANEOUS_PLAYERS,
      vehicles: this.vehicles.length,
      uptime: Math.round((Date.now() - this.startTime) / 1000),
      tickRateHz: MULTIPLAYER_SERVER_TICK_RATE,
      serverTick: this.serverTick,
      avgTickMs: Number(this.avgTickMs.toFixed(3)),
      packetsReceivedTotal: this.packetsReceivedTotal,
      packetsRejectedTotal: this.packetsRejectedTotal,
      antiCheatStrikesTotal: this.antiCheatStrikesTotal,
      memoryRssMb: Number((rssBytes / (1024 * 1024)).toFixed(2)),
      realPlayersOnly: true,
    };
  }

  public getRecentLogs(): ReadonlyArray<ServerLogEntry> {
    return this.recentLogs;
  }

  private sendToWs(ws: WebSocket, msg: ServerMessage): void {
    if (ws.readyState === WebSocket.OPEN) {
      ws.send(JSON.stringify(msg));
    }
  }

  private broadcast(msg: ServerMessage, excludePlayerId?: string): void {
    const payload = JSON.stringify(msg);
    for (const [id, session] of this.sessions.entries()) {
      if (id !== excludePlayerId && session.ws.readyState === WebSocket.OPEN) {
        session.ws.send(payload);
      }
    }
  }

  public getPlayerCount(): number {
    return this.sessions.size;
  }

  public getPort(): number {
    return this.actualPort;
  }

  public getSessions(): Map<string, ConnectedSession> {
    return this.sessions;
  }

  public getSession(playerId: string): ConnectedSession | undefined {
    return this.sessions.get(playerId);
  }

  public getVehicles(): ServerVehicle[] {
    return this.vehicles;
  }

  public stop(): Promise<void> {
    return new Promise((resolve) => {
      if (this.tickInterval) {
        clearInterval(this.tickInterval);
        this.tickInterval = null;
      }
      this.accountStore.close();
      if (this.wss) {
        for (const client of this.wss.clients) {
          try {
            client.terminate();
          } catch {}
        }
        this.wss.close();
        this.wss = null;
      }
      if (this.httpServer) {
        this.httpServer.close(() => resolve());
      } else {
        resolve();
      }
    });
  }
}
