import { OutfitId, VehicleKind, WeaponId } from '../types';

export const MULTIPLAYER_SERVER_TICK_RATE = 20; // 20 Hz fixed authoritative tick
export const MULTIPLAYER_SERVER_TICK_MS = 1000 / MULTIPLAYER_SERVER_TICK_RATE; // 50 ms
export const MAX_SIMULTANEOUS_PLAYERS = 16;
export const DEFAULT_SERVER_PORT = 8080;
export const INTERPOLATION_DELAY_MS = 100; // 100 ms buffer for silky smooth snapshot interpolation

/**
 * Public production WSS endpoint hosted independently from GitHub Pages.
 * Can be overridden via VITE_MULTIPLAYER_SERVER_URL environment variable or in-game lobby.
 */
export const DEFAULT_PRODUCTION_WSS_URL = 'wss://dhaka-gta-server.onrender.com';

/**
 * Resolves appropriate WebSocket URL depending on runtime environment.
 * In production (HTTPS / GitHub Pages), uses secure WSS production backend.
 * In local development (HTTP / localhost), defaults to local dev server.
 */
export function resolveServerUrl(customUrl?: string): string {
  if (customUrl && customUrl.trim().length > 0) {
    return customUrl.trim();
  }

  // Runtime Vite environment variable override
  if (typeof import.meta !== 'undefined' && import.meta.env?.VITE_MULTIPLAYER_SERVER_URL) {
    return String(import.meta.env.VITE_MULTIPLAYER_SERVER_URL).trim();
  }

  // If running in browser on production (e.g. GitHub Pages https://istihadahmed007.github.io)
  if (typeof window !== 'undefined' && window.location) {
    const isHttps = window.location.protocol === 'https:';
    const isLocalhost =
      window.location.hostname === 'localhost' ||
      window.location.hostname === '127.0.0.1' ||
      window.location.hostname.startsWith('192.168.');

    if (isHttps || !isLocalhost) {
      return DEFAULT_PRODUCTION_WSS_URL;
    }
  }

  return `ws://localhost:${DEFAULT_SERVER_PORT}`;
}

export interface NetTransform {
  x: number;
  y: number;
  z: number;
  vx: number;
  vy: number;
  vz: number;
  heading: number;
}

export interface NetPlayerState {
  id: string;
  name: string;
  outfit: OutfitId;
  color: number;
  transform: NetTransform;
  hp: number;
  maxHp: number;
  armor: number;
  maxArmor: number;
  stamina: number;
  money: number;
  wantedLevel: number;
  weapon: WeaponId;
  ammo: Record<WeaponId, number>;
  vehicleId: string | null;
  seat: 'driver' | 'passenger' | null;
  state: 'idle' | 'walk' | 'run' | 'sprint' | 'jump' | 'attack' | 'driving' | 'ko';
  kills: number;
  deaths: number;
  ping: number;
}

export interface NetPlayerSnapshot {
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

export interface NetVehicleSnapshot {
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
  fuel: number;
  color: number;
  driverId: string | null;
  sirenOn: boolean;
  honkActive: boolean;
}

export interface NetWorldState {
  timeOfDay: number;
  weather: 'clear' | 'monsoon' | 'storm';
  serverUptimeSec: number;
  connectedCount: number;
}

// === CLIENT-TO-SERVER MESSAGES ===

export interface CAuthMsg {
  type: 'c_auth';
  name: string;
  token?: string; // Reconnect session token
  pin?: string; // Account security PIN
  outfit?: OutfitId;
  color?: number;
}

export interface CInputMsg {
  type: 'c_input';
  seq: number;
  dt: number;
  moveX: number; // -1..1
  moveZ: number; // -1..1
  heading: number;
  sprint: boolean;
  jump: boolean;
}

export interface CVehicleActionMsg {
  type: 'c_vehicle_action';
  action: 'enter' | 'exit';
  vehicleId?: string;
}

export interface CVehicleInputMsg {
  type: 'c_vehicle_input';
  seq: number;
  throttle: number; // -1..1
  steer: number; // -1..1
  handbrake: boolean;
  horn: boolean;
}

export interface CAttackMsg {
  type: 'c_attack';
  seq: number;
  weapon: WeaponId;
  aimHeading: number;
  targetId?: string;
  targetKind?: 'player' | 'vehicle' | 'npc';
}

export interface CChatMsg {
  type: 'c_chat';
  text: string;
}

export interface CBuyItemMsg {
  type: 'c_buy_item';
  itemId: string;
}

export interface CPingMsg {
  type: 'c_ping';
  clientTime: number;
}

export type ClientMessage =
  | CAuthMsg
  | CInputMsg
  | CVehicleActionMsg
  | CVehicleInputMsg
  | CAttackMsg
  | CChatMsg
  | CBuyItemMsg
  | CPingMsg;

// === SERVER-TO-CLIENT MESSAGES ===

export interface SAuthOkMsg {
  type: 's_auth_ok';
  playerId: string;
  token: string;
  isNewAccount: boolean;
  player: NetPlayerState;
  world: NetWorldState;
  vehicles: NetVehicleSnapshot[];
}

export interface SAuthFailMsg {
  type: 's_auth_fail';
  reason: string;
}

export interface SWorldSnapshotMsg {
  type: 's_world_snapshot';
  serverTick: number;
  timestamp: number;
  players: NetPlayerSnapshot[];
  vehicles: NetVehicleSnapshot[];
  world: NetWorldState;
}

export interface SReconcileMsg {
  type: 's_reconcile';
  lastProcessedInput: number;
  x: number;
  y: number;
  z: number;
  vx: number;
  vz: number;
  heading: number;
  hp: number;
  armor: number;
  money: number;
  wantedLevel: number;
  vehicleId: string | null;
}

export interface SCombatEventMsg {
  type: 's_combat_event';
  attackerId: string;
  attackerName: string;
  victimId: string;
  victimName: string;
  victimKind: 'player' | 'vehicle' | 'npc';
  damage: number;
  lethal: boolean;
  weapon: WeaponId;
  hitX: number;
  hitY: number;
  hitZ: number;
}

export interface SChatMsg {
  type: 's_chat';
  senderId: string;
  senderName: string;
  text: string;
  timestamp: number;
  isSystem?: boolean;
}

export interface SPongMsg {
  type: 's_pong';
  clientTime: number;
  serverTime: number;
}

export interface SPlayerJoinedMsg {
  type: 's_player_joined';
  player: NetPlayerState;
}

export interface SPlayerLeftMsg {
  type: 's_player_left';
  playerId: string;
  playerName: string;
  reason: string;
}

export interface STransactionResultMsg {
  type: 's_transaction_result';
  ok: boolean;
  money: number;
  itemId: string;
  message: string;
}

export interface SErrorMsg {
  type: 's_error';
  code: string;
  message: string;
}

export type ServerMessage =
  | SAuthOkMsg
  | SAuthFailMsg
  | SWorldSnapshotMsg
  | SReconcileMsg
  | SCombatEventMsg
  | SChatMsg
  | SPongMsg
  | SPlayerJoinedMsg
  | SPlayerLeftMsg
  | STransactionResultMsg
  | SErrorMsg;
