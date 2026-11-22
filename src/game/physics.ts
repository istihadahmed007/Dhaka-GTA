import {
  AabbCollider,
  OutfitId,
  OUTFIT_SPECS,
  PlayerState,
  VehicleSpec,
  VehicleState,
  VEHICLE_SPECS,
  WeaponId,
  WEAPON_SPECS,
} from './types';
import { RIVER_Z_START, WORLD_HALF_SIZE } from './worldData';

export interface CircleCollisionResult {
  x: number;
  z: number;
  collided: boolean;
  normalX: number;
  normalZ: number;
}

/**
 * Resolves a circle (entity or vehicle) against static AABB colliders and outer city boundaries.
 * Pure, deterministic, zero-allocation hot path for unit testing and 60fps sim.
 */
export function resolveCircleCollisions(
  x: number,
  z: number,
  radius: number,
  colliders: readonly AabbCollider[],
): CircleCollisionResult {
  let outX = x;
  let outZ = z;
  let collided = false;
  let normalX = 0;
  let normalZ = 0;

  // Keep inside playable city bounds (and north of the Buriganga River water drop-off)
  const minX = -WORLD_HALF_SIZE + radius;
  const maxX = WORLD_HALF_SIZE - radius;
  const minZ = -WORLD_HALF_SIZE + radius;
  const maxZ = RIVER_Z_START - 1.2 - radius;

  if (outX < minX) {
    outX = minX;
    collided = true;
    normalX = 1;
  } else if (outX > maxX) {
    outX = maxX;
    collided = true;
    normalX = -1;
  }

  if (outZ < minZ) {
    outZ = minZ;
    collided = true;
    normalZ = 1;
  } else if (outZ > maxZ) {
    outZ = maxZ;
    collided = true;
    normalZ = -1;
  }

  for (let i = 0; i < colliders.length; i++) {
    const c = colliders[i];
    const closestX = Math.max(c.minX, Math.min(outX, c.maxX));
    const closestZ = Math.max(c.minZ, Math.min(outZ, c.maxZ));
    const dx = outX - closestX;
    const dz = outZ - closestZ;
    const distSq = dx * dx + dz * dz;

    if (distSq < radius * radius) {
      collided = true;
      if (distSq > 1e-6) {
        const dist = Math.sqrt(distSq);
        const push = radius - dist;
        const nx = dx / dist;
        const nz = dz / dist;
        outX += nx * push;
        outZ += nz * push;
        normalX = nx;
        normalZ = nz;
      } else {
        // Center is inside AABB: push out along nearest edge
        const leftPen = Math.abs(outX - c.minX);
        const rightPen = Math.abs(c.maxX - outX);
        const topPen = Math.abs(outZ - c.minZ);
        const botPen = Math.abs(c.maxZ - outZ);
        const minPen = Math.min(leftPen, rightPen, topPen, botPen);
        if (minPen === leftPen) {
          outX = c.minX - radius;
          normalX = -1;
        } else if (minPen === rightPen) {
          outX = c.maxX + radius;
          normalX = 1;
        } else if (minPen === topPen) {
          outZ = c.minZ - radius;
          normalZ = -1;
        } else {
          outZ = c.maxZ + radius;
          normalZ = 1;
        }
      }
    }
  }

  return { x: outX, z: outZ, collided, normalX, normalZ };
}

export interface VehicleInput {
  throttle: number; // -1..1
  steer: number; // -1..1 (positive = left, negative = right)
  handbrake: boolean;
}

export interface VehicleStepResult {
  impactSpeed: number;
  collided: boolean;
}

/**
 * Steps vehicle physics with realistic acceleration, reverse, speed-sensitive steering,
 * drift/handbrake friction, and collision damage.
 */
export function stepVehicleKinematics(
  v: VehicleState,
  input: VehicleInput,
  dt: number,
  colliders: readonly AabbCollider[],
): VehicleStepResult {
  const spec: VehicleSpec = VEHICLE_SPECS[v.kind];

  // Longitudinal acceleration & braking
  if (input.throttle > 0.05) {
    if (v.speed < 0) {
      v.speed = Math.min(0, v.speed + spec.brake * dt);
    } else {
      v.speed = Math.min(spec.maxSpeed, v.speed + spec.accel * input.throttle * dt);
    }
  } else if (input.throttle < -0.05) {
    if (v.speed > 0.2) {
      v.speed = Math.max(0, v.speed - spec.brake * dt);
    } else {
      v.speed = Math.max(-spec.reverseSpeed, v.speed + spec.accel * 0.65 * input.throttle * dt);
    }
  } else {
    // Natural rolling drag
    const dragDecel = spec.drag * dt * 3.2;
    if (Math.abs(v.speed) <= dragDecel) {
      v.speed = 0;
    } else {
      v.speed -= Math.sign(v.speed) * dragDecel;
    }
  }

  if (input.handbrake) {
    const hbDecel = spec.brake * 1.1 * dt;
    if (Math.abs(v.speed) <= hbDecel) v.speed = 0;
    else v.speed -= Math.sign(v.speed) * hbDecel;
  }

  // Smooth front wheel steering angle
  const targetSteer = input.steer * 0.55;
  v.steerAngle += (targetSteer - v.steerAngle) * Math.min(1, dt * 10);

  // Heading rotation proportional to speed (reverses steering when backing up)
  const speedRatio = Math.min(1, Math.abs(v.speed) / 4.5);
  const driftBoost = input.handbrake && spec.canDrift ? 1.35 : 1.0;
  const dirSign = v.speed >= -0.05 ? 1 : -1;
  v.heading += input.steer * spec.turnRate * speedRatio * driftBoost * dirSign * dt;

  // Forward direction in Three.js: heading 0 = +Z, heading PI = -Z
  const forwardX = Math.sin(v.heading);
  const forwardZ = Math.cos(v.heading);

  const nextX = v.x + forwardX * v.speed * dt;
  const nextZ = v.z + forwardZ * v.speed * dt;

  const collisionRadius = (spec.halfWidth + spec.halfLength) * 0.48;
  const res = resolveCircleCollisions(nextX, nextZ, collisionRadius, colliders);

  let impactSpeed = 0;
  if (res.collided) {
    impactSpeed = Math.abs(v.speed);
    v.x = res.x;
    v.z = res.z;
    if (impactSpeed > 6) {
      const dmg = Math.round((impactSpeed - 5) * 2.2);
      v.hp = Math.max(0, v.hp - dmg);
      v.damagedSmoke = v.hp < v.maxHp * 0.4;
    }
    // Bounce back gently and kill excess forward momentum
    v.speed = -v.speed * 0.25;
  } else {
    v.x = res.x;
    v.z = res.z;
  }

  return { impactSpeed, collided: res.collided };
}

/**
 * Applies damage to player, absorbing part of it with armor first.
 * Never clamps HP at 1 — defeat (hp === 0) is genuinely reachable.
 */
export function applyPlayerDamage(player: PlayerState, rawDamage: number): number {
  if (rawDamage <= 0 || player.hp <= 0) return 0;
  let remaining = rawDamage;
  if (player.armor > 0) {
    const absorbed = Math.min(player.armor, Math.ceil(rawDamage * 0.65));
    player.armor = Math.max(0, player.armor - absorbed);
    remaining = Math.max(1, rawDamage - absorbed);
  }
  const applied = Math.min(player.hp, remaining);
  player.hp = Math.max(0, player.hp - remaining);
  return applied;
}

/**
 * Escalates player's Wanted Level (0..5 stars) when crimes occur.
 */
export function addWantedHeat(player: PlayerState, heatDelta: number): number {
  player.wantedHeat = Math.min(100, player.wantedHeat + heatDelta);
  const computedStars = Math.min(5, Math.ceil(player.wantedHeat / 20));
  if (computedStars > player.wantedLevel) {
    player.wantedLevel = computedStars;
  }
  if (player.wantedLevel > 0) {
    player.wantedCooldown = 14 + player.wantedLevel * 4;
  }
  return player.wantedLevel;
}

/**
 * Ticks wanted cooldown when no police officer is close to the player.
 */
export function tickWantedCooldown(player: PlayerState, policeNearby: boolean, dt: number): void {
  if (player.wantedLevel <= 0) return;
  if (policeNearby) {
    player.wantedCooldown = Math.min(14 + player.wantedLevel * 4, player.wantedCooldown + dt * 2.5);
    return;
  }
  player.wantedCooldown = Math.max(0, player.wantedCooldown - dt);
  if (player.wantedCooldown <= 0) {
    player.wantedLevel = 0;
    player.wantedHeat = 0;
  }
}

/**
 * Economy actions for Shop, Garage, and Wardrobe (pure testable state transitions).
 */
export function buyShopItem(
  player: PlayerState,
  item: 'biriyani' | 'armor' | WeaponId,
): { ok: boolean; message: string } {
  if (item === 'biriyani') {
    const cost = 150;
    if (player.money < cost) return { ok: false, message: 'Not enough Taka (৳150 needed)' };
    if (player.hp >= player.maxHp) return { ok: false, message: 'Health is already full' };
    player.money -= cost;
    player.hp = player.maxHp;
    return { ok: true, message: 'Kacchi Biriyani restored full health!' };
  }
  if (item === 'armor') {
    const cost = 400;
    if (player.money < cost) return { ok: false, message: 'Not enough Taka (৳400 needed)' };
    if (player.armor >= player.maxArmor) return { ok: false, message: 'Body armor is already full' };
    player.money -= cost;
    player.armor = player.maxArmor;
    return { ok: true, message: 'Tactical Street Vest equipped!' };
  }
  const spec = WEAPON_SPECS[item];
  const owned = player.unlockedWeapons.includes(item);
  if (!owned) {
    if (player.money < spec.price) {
      return { ok: false, message: `Need ৳${spec.price} for ${spec.name}` };
    }
    player.money -= spec.price;
    player.unlockedWeapons.push(item);
    player.weapon = item;
    player.magAmmo[item] = spec.magSize;
    player.ammo[item] = spec.magSize * 3;
    return { ok: true, message: `Purchased ${spec.name}!` };
  }
  if (spec.ammoPrice <= 0) return { ok: false, message: 'No ammo needed' };
  if (player.money < spec.ammoPrice) {
    return { ok: false, message: `Need ৳${spec.ammoPrice} for ammo` };
  }
  player.money -= spec.ammoPrice;
  player.ammo[item] = (player.ammo[item] ?? 0) + spec.magSize * 2;
  return { ok: true, message: `Bought ammo for ${spec.name}` };
}

export function applyGarageService(
  player: PlayerState,
  vehicle: VehicleState | null,
  service: 'repair' | 'clear_heat' | 'paint',
  paintColor?: number,
): { ok: boolean; message: string } {
  if (service === 'clear_heat') {
    const cost = 300;
    if (player.wantedLevel <= 0) return { ok: false, message: 'No active DMP Wanted Level' };
    if (player.money < cost) return { ok: false, message: 'Need ৳300 to bribe & respray plates' };
    player.money -= cost;
    player.wantedLevel = 0;
    player.wantedHeat = 0;
    player.wantedCooldown = 0;
    return { ok: true, message: 'Plates swapped — DMP Wanted Level cleared!' };
  }
  if (!vehicle) return { ok: false, message: 'Bring a vehicle near Dhaka Customs first' };
  if (service === 'repair') {
    const cost = 200;
    if (vehicle.hp >= vehicle.maxHp) return { ok: false, message: 'Vehicle is already in mint condition' };
    if (player.money < cost) return { ok: false, message: 'Need ৳200 for full repair' };
    player.money -= cost;
    vehicle.hp = vehicle.maxHp;
    vehicle.damagedSmoke = false;
    return { ok: true, message: `${VEHICLE_SPECS[vehicle.kind].name} fully repaired!` };
  }
  if (service === 'paint' && paintColor !== undefined) {
    const cost = 150;
    if (player.money < cost) return { ok: false, message: 'Need ৳150 for Dhaka Custom Respray' };
    player.money -= cost;
    vehicle.color = paintColor;
    return { ok: true, message: 'Custom paint job applied!' };
  }
  return { ok: false, message: 'Unknown garage service' };
}

export function equipOrBuyOutfit(
  player: PlayerState,
  outfitId: OutfitId,
): { ok: boolean; message: string } {
  const spec = OUTFIT_SPECS[outfitId];
  if (player.unlockedOutfits.includes(outfitId)) {
    player.outfit = outfitId;
    return { ok: true, message: `Equipped ${spec.name}` };
  }
  if (player.money < spec.price) {
    return { ok: false, message: `Need ৳${spec.price} to unlock ${spec.name}` };
  }
  player.money -= spec.price;
  player.unlockedOutfits.push(outfitId);
  player.outfit = outfitId;
  return { ok: true, message: `Unlocked & equipped ${spec.name}!` };
}

export function createInitialPlayerState(): PlayerState {
  return {
    x: -2.2,
    y: 0,
    z: 35.2,
    vx: 0,
    vy: 0,
    vz: 0,
    heading: Math.PI, // Facing North (-Z) toward Shapla Monument & Gulshan skyline
    grounded: true,
    coyoteTimer: 0,
    jumpBufferTimer: 0,
    hp: 100,
    maxHp: 100,
    armor: 50,
    maxArmor: 100,
    stamina: 100,
    maxStamina: 100,
    money: 800,
    wantedLevel: 0,
    wantedHeat: 0,
    wantedCooldown: 0,
    vehicleId: null,
    weapon: 'fists',
    unlockedWeapons: ['fists', 'pistol'],
    ammo: { fists: 0, pistol: 48, smg: 0, shotgun: 0 },
    magAmmo: { fists: 0, pistol: 12, smg: 0, shotgun: 0 },
    reloadTimer: 0,
    attackCooldown: 0,
    outfit: 'street',
    unlockedOutfits: ['street'],
    kills: 0,
    missionsCompleted: 0,
    faresCompleted: 0,
    distanceDriven: 0,
  };
}

export function formatDhakaTime(hours: number): string {
  const hNorm = ((hours % 24) + 24) % 24;
  const hh = Math.floor(hNorm);
  const mm = Math.floor((hNorm - hh) * 60);
  const suffix = hh >= 12 ? 'PM' : 'AM';
  const h12 = hh % 12 === 0 ? 12 : hh % 12;
  return `${h12}:${mm.toString().padStart(2, '0')} ${suffix}`;
}
