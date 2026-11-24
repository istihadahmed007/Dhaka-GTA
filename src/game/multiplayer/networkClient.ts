import {
  CAttackMsg,
  CAuthMsg,
  CBuyItemMsg,
  CChatMsg,
  CInputMsg,
  ClientMessage,
  CVehicleActionMsg,
  CVehicleInputMsg,
  INTERPOLATION_DELAY_MS,
  NetPlayerSnapshot,
  NetPlayerState,
  NetVehicleSnapshot,
  NetWorldState,
  SCombatEventMsg,
  ServerMessage,
  SWorldSnapshotMsg,
} from './protocol';
import { OutfitId, WeaponId } from '../types';

export interface InterpolatedPlayer {
  id: string;
  x: number;
  y: number;
  z: number;
  vx: number;
  vz: number;
  heading: number;
  hp: number;
  armor: number;
  stamina: number;
  weapon: WeaponId;
  vehicleId: string | null;
  state: 'idle' | 'walk' | 'run' | 'sprint' | 'jump' | 'attack' | 'driving' | 'ko';
  ping: number;
}

export interface InterpolatedVehicle {
  id: string;
  kind: string;
  x: number;
  y: number;
  z: number;
  speed: number;
  steerAngle: number;
  heading: number;
  hp: number;
  fuel: number;
  driverId: string | null;
  sirenOn: boolean;
  honkActive: boolean;
}

interface StoredSnapshot {
  timestamp: number;
  players: Map<string, NetPlayerSnapshot>;
  vehicles: Map<string, NetVehicleSnapshot>;
}

export interface LocalPredictedInput {
  seq: number;
  dt: number;
  moveX: number;
  moveZ: number;
  heading: number;
  sprint: boolean;
  jump: boolean;
  predictedX: number;
  predictedZ: number;
}

export type NetworkClientEventListener = {
  onConnect?: () => void;
  onDisconnect?: (reason: string) => void;
  onAuthOk?: (player: NetPlayerState, isNew: boolean) => void;
  onCombatEvent?: (evt: SCombatEventMsg) => void;
  onChat?: (senderName: string, text: string, isSystem: boolean) => void;
  onPlayerJoined?: (player: NetPlayerState) => void;
  onPlayerLeft?: (playerId: string, name: string) => void;
  onError?: (err: string) => void;
};

function lerp(a: number, b: number, t: number): number {
  return a + (b - a) * t;
}

function lerpAngle(a: number, b: number, t: number): number {
  let diff = (b - a) % (Math.PI * 2);
  if (diff < -Math.PI) diff += Math.PI * 2;
  if (diff > Math.PI) diff -= Math.PI * 2;
  return a + diff * t;
}

export class NetworkClient {
  private ws: WebSocket | null = null;
  private url: string;
  private localPlayerId: string | null = null;
  private localPlayerState: NetPlayerState | null = null;
  private token: string | null = null;
  private inputSeq = 0;
  private pendingInputs: LocalPredictedInput[] = [];
  private snapshots: StoredSnapshot[] = [];
  private listeners: NetworkClientEventListener = {};
  private connected = false;
  private reconnecting = false;
  private reconnectAttempts = 0;
  private reconnectTimer: number | null = null;
  private worldState: NetWorldState = {
    timeOfDay: 14.5,
    weather: 'clear',
    serverUptimeSec: 0,
    connectedCount: 1,
  };
  private serverTimeOffset = 0;
  private currentPing = 0;
  private pingAccum = 0;

  constructor(serverUrl = 'ws://localhost:8080', listener?: NetworkClientEventListener) {
    this.url = serverUrl;
    if (listener) this.listeners = listener;
    // Load cached session token from localStorage if available
    try {
      this.token = localStorage.getItem('dhaka_mp_auth_token');
    } catch {}
  }

  public setListener(listener: NetworkClientEventListener): void {
    this.listeners = listener;
  }

  public isConnected(): boolean {
    return this.connected;
  }

  public getLocalPlayerId(): string | null {
    return this.localPlayerId;
  }

  public getLocalPlayerState(): NetPlayerState | null {
    return this.localPlayerState;
  }

  public getWorldState(): NetWorldState {
    return this.worldState;
  }

  public getPing(): number {
    return this.currentPing;
  }

  public update(dt: number): void {
    if (!this.connected) return;
    this.pingAccum += dt;
    if (this.pingAccum >= 1.0) {
      this.pingAccum = 0;
      this.send({ type: 'c_ping', clientTime: Date.now() });
    }
  }

  public connect(name: string, outfit: OutfitId = 'street', color = 0x26466d): void {
    if (this.ws && (this.ws.readyState === WebSocket.OPEN || this.ws.readyState === WebSocket.CONNECTING)) {
      return;
    }

    try {
      this.ws = new WebSocket(this.url);
    } catch (err) {
      this.listeners.onError?.(`Failed to open WebSocket: ${String(err)}`);
      return;
    }

    this.ws.onopen = () => {
      this.connected = true;
      this.reconnecting = false;
      this.reconnectAttempts = 0;
      this.listeners.onConnect?.();

      // Send initial authentication message
      const authMsg: CAuthMsg = {
        type: 'c_auth',
        name,
        token: this.token ?? undefined,
        outfit,
        color,
      };
      this.send(authMsg);
    };

    this.ws.onmessage = (event) => {
      try {
        const msg = JSON.parse(event.data) as ServerMessage;
        this.handleServerMessage(msg);
      } catch (err) {
        console.warn('[NetClient] Malformed packet:', err);
      }
    };

    this.ws.onclose = (event) => {
      this.connected = false;
      this.listeners.onDisconnect?.(event.reason || 'Connection lost');
      this.scheduleReconnect(name, outfit, color);
    };

    this.ws.onerror = () => {
      this.listeners.onError?.('WebSocket connection error');
    };
  }

  private scheduleReconnect(name: string, outfit: OutfitId, color: number): void {
    if (this.reconnecting || this.reconnectAttempts >= 5) return;
    this.reconnecting = true;
    this.reconnectAttempts++;
    const delay = Math.min(6000, 1000 * Math.pow(1.5, this.reconnectAttempts));

    if (this.reconnectTimer) window.clearTimeout(this.reconnectTimer);
    this.reconnectTimer = window.setTimeout(() => {
      this.reconnectTimer = null;
      if (!this.connected) {
        this.connect(name, outfit, color);
      }
    }, delay);
  }

  public disconnect(): void {
    if (this.reconnectTimer) {
      window.clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }
    this.reconnecting = false;
    this.reconnectAttempts = 99; // Prevent auto-reconnecting after intentional disconnect
    if (this.ws) {
      this.ws.close();
      this.ws = null;
    }
    this.connected = false;
    this.snapshots = [];
    this.pendingInputs = [];
  }

  private send(msg: ClientMessage): void {
    if (this.ws && this.ws.readyState === WebSocket.OPEN) {
      this.ws.send(JSON.stringify(msg));
    }
  }

  private handleServerMessage(msg: ServerMessage): void {
    switch (msg.type) {
      case 's_auth_ok':
        this.localPlayerId = msg.playerId;
        this.localPlayerState = msg.player;
        this.token = msg.token;
        this.worldState = msg.world;
        try {
          localStorage.setItem('dhaka_mp_auth_token', msg.token);
        } catch {}
        this.listeners.onAuthOk?.(msg.player, msg.isNewAccount);
        break;

      case 's_world_snapshot':
        this.handleWorldSnapshot(msg);
        break;

      case 's_reconcile':
        this.handleReconciliation(msg);
        break;

      case 's_combat_event':
        this.listeners.onCombatEvent?.(msg);
        break;

      case 's_chat':
        this.listeners.onChat?.(msg.senderName, msg.text, Boolean(msg.isSystem));
        break;

      case 's_player_joined':
        this.listeners.onPlayerJoined?.(msg.player);
        break;

      case 's_player_left':
        this.listeners.onPlayerLeft?.(msg.playerId, msg.playerName);
        break;

      case 's_pong':
        this.currentPing = Math.max(1, Math.round(Date.now() - msg.clientTime));
        this.serverTimeOffset = msg.serverTime - (msg.clientTime + Date.now()) * 0.5;
        break;

      case 's_error':
        this.listeners.onError?.(msg.message);
        break;
    }
  }

  private handleWorldSnapshot(msg: SWorldSnapshotMsg): void {
    this.worldState = msg.world;

    const playersMap = new Map<string, NetPlayerSnapshot>();
    msg.players.forEach((p) => playersMap.set(p.id, p));

    const vehiclesMap = new Map<string, NetVehicleSnapshot>();
    msg.vehicles.forEach((v) => vehiclesMap.set(v.id, v));

    this.snapshots.push({
      timestamp: msg.timestamp,
      players: playersMap,
      vehicles: vehiclesMap,
    });

    // Prune old snapshots (keep last 1.2s)
    const oldestAllowed = Date.now() - 1200;
    while (this.snapshots.length > 2 && this.snapshots[0].timestamp < oldestAllowed) {
      this.snapshots.shift();
    }
  }

  /**
   * Server Reconciliation: compares server authoritative position to predicted history.
   * If divergence exceeds 0.45m, snaps position and re-simulates remaining unacknowledged inputs.
   */
  private handleReconciliation(msg: ServerMessage & { type: 's_reconcile' }): void {
    if (!this.localPlayerState) return;

    this.localPlayerState.hp = msg.hp;
    this.localPlayerState.armor = msg.armor;
    this.localPlayerState.money = msg.money;
    this.localPlayerState.wantedLevel = msg.wantedLevel;
    this.localPlayerState.vehicleId = msg.vehicleId;

    // Prune acknowledged inputs
    this.pendingInputs = this.pendingInputs.filter((i) => i.seq > msg.lastProcessedInput);

    const p = this.localPlayerState.transform;
    const err = Math.hypot(p.x - msg.x, p.z - msg.z);

    // If client prediction drifted noticeably, correct toward authoritative server position
    if (err > 0.45) {
      p.x = msg.x;
      p.y = msg.y;
      p.z = msg.z;
      p.vx = msg.vx;
      p.vz = msg.vz;
      p.heading = msg.heading;

      // Re-simulate pending unacknowledged inputs
      for (const input of this.pendingInputs) {
        p.x += p.vx * input.dt;
        p.z += p.vz * input.dt;
      }
    }
  }

  /**
   * Sends raw movement input to server and queues in local prediction buffer.
   */
  public sendMovementInput(
    dt: number,
    moveX: number,
    moveZ: number,
    heading: number,
    sprint: boolean,
    jump: boolean,
    predictedX = 0,
    predictedZ = 0,
  ): void {
    this.inputSeq++;
    const msg: CInputMsg = {
      type: 'c_input',
      seq: this.inputSeq,
      dt,
      moveX,
      moveZ,
      heading,
      sprint,
      jump,
    };

    this.pendingInputs.push({
      seq: this.inputSeq,
      dt,
      moveX,
      moveZ,
      heading,
      sprint,
      jump,
      predictedX,
      predictedZ,
    });

    if (this.pendingInputs.length > 60) this.pendingInputs.shift();
    this.send(msg);
  }

  public sendVehicleAction(action: 'enter' | 'exit', vehicleId?: string): void {
    const msg: CVehicleActionMsg = {
      type: 'c_vehicle_action',
      action,
      vehicleId,
    };
    this.send(msg);
  }

  public sendVehicleInput(throttle: number, steer: number, handbrake: boolean, horn: boolean): void {
    this.inputSeq++;
    const msg: CVehicleInputMsg = {
      type: 'c_vehicle_input',
      seq: this.inputSeq,
      throttle,
      steer,
      handbrake,
      horn,
    };
    this.send(msg);
  }

  public sendAttack(weapon: WeaponId, aimHeading: number, targetId?: string, targetKind?: 'player' | 'vehicle' | 'npc'): void {
    this.inputSeq++;
    const msg: CAttackMsg = {
      type: 'c_attack',
      seq: this.inputSeq,
      weapon,
      aimHeading,
      targetId,
      targetKind,
    };
    this.send(msg);
  }

  public sendChat(text: string): void {
    const msg: CChatMsg = {
      type: 'c_chat',
      text,
    };
    this.send(msg);
  }

  public sendBuyItem(itemId: string): void {
    const msg: CBuyItemMsg = {
      type: 'c_buy_item',
      itemId,
    };
    this.send(msg);
  }

  /**
   * Hermite / Linear Snapshot Interpolation for remote entities.
   * Renders at (now - INTERPOLATION_DELAY_MS) for perfectly smooth, jitter-free 60 FPS motion.
   */
  public getInterpolatedEntities(): {
    players: InterpolatedPlayer[];
    vehicles: InterpolatedVehicle[];
  } {
    if (this.snapshots.length === 0) {
      return { players: [], vehicles: [] };
    }

    const renderTime = Date.now() - INTERPOLATION_DELAY_MS;

    // Find the two snapshots surrounding renderTime
    let s0: StoredSnapshot = this.snapshots[0];
    let s1: StoredSnapshot = this.snapshots[0];

    for (let i = 0; i < this.snapshots.length - 1; i++) {
      if (renderTime >= this.snapshots[i].timestamp && renderTime <= this.snapshots[i + 1].timestamp) {
        s0 = this.snapshots[i];
        s1 = this.snapshots[i + 1];
        break;
      }
    }

    // Extrapolate to latest if renderTime is past all snapshots
    if (s0 === s1 && this.snapshots.length > 1) {
      s0 = this.snapshots[this.snapshots.length - 2];
      s1 = this.snapshots[this.snapshots.length - 1];
    }

    const span = s1.timestamp - s0.timestamp;
    const alpha = span > 0 ? Math.max(0, Math.min(1.2, (renderTime - s0.timestamp) / span)) : 0;

    const interpolatedPlayers: InterpolatedPlayer[] = [];
    for (const [id, p0] of s0.players.entries()) {
      if (id === this.localPlayerId) continue; // Exclude local player (handled by client prediction)
      const p1 = s1.players.get(id) ?? p0;

      interpolatedPlayers.push({
        id,
        x: lerp(p0.x, p1.x, alpha),
        y: lerp(p0.y, p1.y, alpha),
        z: lerp(p0.z, p1.z, alpha),
        vx: lerp(p0.vx, p1.vx, alpha),
        vz: lerp(p0.vz, p1.vz, alpha),
        heading: lerpAngle(p0.heading, p1.heading, alpha),
        hp: p1.hp,
        armor: p1.armor,
        stamina: p1.stamina,
        weapon: p1.weapon,
        vehicleId: p1.vehicleId,
        state: p1.state,
        ping: p1.ping,
      });
    }

    const interpolatedVehicles: InterpolatedVehicle[] = [];
    for (const [id, v0] of s0.vehicles.entries()) {
      const v1 = s1.vehicles.get(id) ?? v0;

      interpolatedVehicles.push({
        id,
        kind: v1.kind,
        x: lerp(v0.x, v1.x, alpha),
        y: lerp(v0.y, v1.y, alpha),
        z: lerp(v0.z, v1.z, alpha),
        speed: lerp(v0.speed, v1.speed, alpha),
        steerAngle: lerp(v0.steerAngle, v1.steerAngle, alpha),
        heading: lerpAngle(v0.heading, v1.heading, alpha),
        hp: v1.hp,
        fuel: v1.fuel,
        driverId: v1.driverId,
        sirenOn: v1.sirenOn,
        honkActive: v1.honkActive,
      });
    }

    return {
      players: interpolatedPlayers,
      vehicles: interpolatedVehicles,
    };
  }
}
