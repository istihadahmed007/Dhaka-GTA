import {
  AabbCollider,
  isWatercraftKind,
  OutfitId,
  OUTFIT_SPECS,
  PlayerState,
  SafehouseId,
  VehicleSpec,
  VehicleState,
  VEHICLE_SPECS,
  WeaponId,
  WEAPON_SPECS,
} from './types';
import { RIVER_Z_START, SAFEHOUSE_SPECS, WORLD_HALF_SIZE } from './worldData';
import { BURIGANGA_MAX_Z, stepWatercraftKinematics } from './waterSystem';

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
  allowRiverWater = false,
): CircleCollisionResult {
  let outX = x;
  let outZ = z;
  let collided = false;
  let normalX = 0;
  let normalZ = 0;

  // Check if standing/walking on one of the 4 Sadarghat jetties
  const onSadarghatJetty = [-72, -26, 26, 78].some(
    (jx) => Math.abs(outX - jx) < 3.25 && outZ >= RIVER_Z_START - 2.5 && outZ <= RIVER_Z_START + 11.5,
  );
  // Check if on Keraniganj south pier
  const onKeraniganjPier = [-26, 65].some(
    (kx) => Math.abs(outX - kx) < 4.0 && outZ >= RIVER_Z_START + 184.0 && outZ <= RIVER_Z_START + 196.0,
  );

  const canCrossEmbankment = allowRiverWater || onSadarghatJetty || onKeraniganjPier;

  // Keep inside playable city / river bounds
  const minX = -WORLD_HALF_SIZE + radius;
  const maxX = WORLD_HALF_SIZE - radius;
  const minZ = -WORLD_HALF_SIZE + radius;
  const maxZ = canCrossEmbankment ? (BURIGANGA_MAX_Z - radius) : (RIVER_Z_START - 1.2 - radius);

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

function clamp(val: number, min: number, max: number): number {
  return Math.max(min, Math.min(max, val));
}

/**
 * Steps vehicle physics with realistic 2D velocity vector dynamics, acceleration curves,
 * progressive tire grip & handbrake drifting, speed-sensitive steering, reverse gear,
 * suspension weight-transfer (pitch & roll), and impulse collision response.
 */
export function stepVehicleKinematics(
  v: VehicleState,
  input: VehicleInput,
  dt: number,
  colliders: readonly AabbCollider[],
): VehicleStepResult {
  const safeDt = Math.min(Math.max(dt, 0.0001), 0.1);

  if (isWatercraftKind(v.kind)) {
    let effectiveInput = input;
    if (v.driver === 'player') {
      if (v.fuel === undefined || v.fuel === null) {
        v.fuel = 100;
        v.maxFuel = 100;
      }
      if (v.isStalled) {
        effectiveInput = { ...input, throttle: 0 };
      } else {
        const burn = (0.015 + Math.abs(input.throttle) * 0.05) * safeDt;
        v.fuel = Math.max(0, v.fuel - burn);
        if (v.fuel <= 0) {
          v.isStalled = true;
          effectiveInput = { ...input, throttle: 0 };
        }
      }
    }
    const waterRes = stepWatercraftKinematics(v, effectiveInput, dt, colliders as AabbCollider[]);
    return {
      collided: waterRes.collided,
      impactSpeed: waterRes.impactSpeed,
    };
  }

  const spec: VehicleSpec = VEHICLE_SPECS[v.kind];

  // Fuel consumption and stalling for road vehicles
  if (v.driver === 'player') {
    if (v.fuel === undefined || v.fuel === null) {
      v.fuel = 100;
      v.maxFuel = 100;
    }
    if (!v.isStalled) {
      const burn = (0.02 + (Math.abs(v.speed ?? 0) / spec.maxSpeed) * 0.05 + (Math.abs(input.throttle) > 0.05 ? 0.03 : 0)) * safeDt;
      v.fuel = Math.max(0, v.fuel - burn);
      if (v.fuel <= 0) {
        v.isStalled = true;
      }
    }
  }

  const effectiveThrottle = v.isStalled ? 0 : input.throttle;

  // Customization multipliers
  const engineStage = v.customization?.engineStage ?? 0;
  const brakesStage = v.customization?.brakesStage ?? 0;
  const hasTurbo = Boolean(v.customization?.hasTurbo);
  const hasSpoiler = Boolean(v.customization?.hasSpoiler);

  const torqueMult = (1 + engineStage * 0.12) * (hasTurbo ? 1.15 : 1.0);
  const maxSpeedMult = 1 + engineStage * 0.05;
  const effMaxSpeed = spec.maxSpeed * maxSpeedMult;
  const brakeMult = 1 + brakesStage * 0.25;

  // Initialize velocity vector and dynamics if not yet set
  const fX = Math.sin(v.heading);
  const fZ = Math.cos(v.heading);
  const rX = Math.cos(v.heading);
  const rZ = -Math.sin(v.heading);

  // Initialize or re-sync velocity vector if externally modified (e.g. tests or teleport)
  let vLong = (v.vx ?? 0) * fX + (v.vz ?? 0) * fZ;
  let vLat = (v.vx ?? 0) * rX + (v.vz ?? 0) * rZ;

  if (v.vx === undefined || v.vz === undefined || Math.abs(v.speed - vLong) > 0.8) {
    v.vx = fX * v.speed;
    v.vz = fZ * v.speed;
    v.lateralSlip = 0;
    v.pitch = 0;
    v.roll = 0;
    v.wheelRotation = v.wheelRotation || 0;
    vLong = v.speed;
    vLat = 0;
  }

  // Steering: speed-sensitive steering angle with smooth response
  const speedRatio = Math.min(1, Math.abs(vLong) / 4.5);
  // High-speed steering damping: prevent high-speed instaturns while preserving agility
  const highSpeedDamp = Math.max(0.48, 1 - Math.max(0, Math.abs(vLong) - 15) / 35);
  const maxSteerLock = 0.58 * highSpeedDamp;
  const targetSteer = input.steer * maxSteerLock;
  v.steerAngle += (targetSteer - v.steerAngle) * Math.min(1, safeDt * 12);

  // Determine braking vs reverse states
  let isBraking = false;
  let isReversing = false;

  // Longitudinal Drive & Brake Forces
  const prevVLong = vLong;
  if (effectiveThrottle > 0.05) {
    if (vLong < -0.3) {
      // Moving backward: pressing throttle acts as front brake
      isBraking = true;
      vLong = Math.min(0, vLong + spec.brake * brakeMult * effectiveThrottle * safeDt);
    } else {
      // Forward acceleration with realistic torque curve
      const powerCurve = 1 - 0.45 * Math.min(1, Math.abs(vLong) / effMaxSpeed);
      vLong = Math.min(effMaxSpeed, vLong + spec.accel * torqueMult * effectiveThrottle * powerCurve * safeDt);
    }
  } else if (effectiveThrottle < -0.05) {
    const revInput = Math.abs(effectiveThrottle);
    if (vLong > 0.3) {
      // Moving forward: pressing S / Down acts as foot brake
      isBraking = true;
      vLong = Math.max(0, vLong - spec.brake * brakeMult * revInput * safeDt);
    } else {
      // Stationary or already in reverse: accelerate backward
      isReversing = true;
      vLong = Math.max(-spec.reverseSpeed, vLong - spec.accel * 0.72 * revInput * safeDt);
    }
  } else {
    // Natural rolling resistance and aerodynamic drag
    const aeroDrag = (spec.drag * 2.8 + Math.abs(vLong) * 0.04) * safeDt;
    if (Math.abs(vLong) <= aeroDrag) {
      vLong = 0;
    } else {
      vLong -= Math.sign(vLong) * aeroDrag;
    }
  }

  // Handbrake Deceleration
  if (input.handbrake) {
    isBraking = true;
    const hbDecel = spec.brake * brakeMult * 1.15 * safeDt;
    if (Math.abs(vLong) <= hbDecel) vLong = 0;
    else vLong -= Math.sign(vLong) * hbDecel;
  }

  // --- Lateral Tire Grip & Drift Dynamics ---
  // Normal static tire grip restores lateral velocity quickly
  const normalGrip = spec.kind === 'supercar' ? 24 : spec.kind === 'bike' ? 22 : spec.kind === 'sedan' ? 16 : 14;
  let isDrifting = false;

  if (input.handbrake && spec.canDrift && Math.abs(vLong) > 3.0) {
    // Handbrake drift: lateral grip breaks on rear tires!
    isDrifting = true;
    // Induce lateral kick in direction of steering
    const kickMult = spec.kind === 'supercar' ? 5.8 : 4.5;
    vLat += -Math.sign(input.steer || v.steerAngle || 1) * Math.min(6.5, Math.abs(vLong) * 0.35) * safeDt * kickMult;
    // Sliding friction damping
    vLat *= Math.max(0, 1 - 2.8 * safeDt);
  } else {
    // Check for power-slide / oversteer due to high lateral G
    const lateralG = Math.abs(vLong * (v.steerAngle / 0.55) * spec.turnRate);
    const gripLimit = (spec.kind === 'supercar' ? 14.5 : spec.canDrift ? 11.5 : 8.5) * (hasSpoiler ? 1.25 : 1.0);

    if (lateralG > gripLimit && Math.abs(vLong) > 9.0) {
      isDrifting = true;
      vLat *= Math.max(0, 1 - 4.5 * safeDt);
    } else {
      // Full tire adhesion: rapid restoration of lateral slip to 0
      const gripDamp = Math.min(1, normalGrip * safeDt);
      vLat *= (1 - gripDamp);
      if (Math.abs(vLat) < 0.02) vLat = 0;
    }
  }

  // Heading Rotation (Yaw)
  const dirSign = vLong >= -0.05 ? 1 : -1;
  const driftYawBoost = isDrifting ? 1.45 : 1.0;
  // Natural steering rotation proportional to speedRatio
  v.heading += v.steerAngle * spec.turnRate * speedRatio * driftYawBoost * dirSign * safeDt;
  // Additional oversteer yaw rotation from lateral sliding
  if (isDrifting && Math.abs(vLat) > 0.5) {
    v.heading += (vLat / Math.max(1, Math.abs(vLong))) * 0.35 * safeDt;
  }
  // Keep heading normalized in [-PI, PI]
  while (v.heading > Math.PI) v.heading -= Math.PI * 2;
  while (v.heading < -Math.PI) v.heading += Math.PI * 2;

  // Re-orient forward and right vectors with updated heading
  const newFX = Math.sin(v.heading);
  const newFZ = Math.cos(v.heading);
  const newRX = Math.cos(v.heading);
  const newRZ = -Math.sin(v.heading);

  // Reconstruct world velocities
  v.vx = newFX * vLong + newRX * vLat;
  v.vz = newFZ * vLong + newRZ * vLat;
  v.speed = vLong;
  v.lateralSlip = vLat;
  v.isBraking = isBraking;
  v.isReversing = isReversing;
  v.isDrifting = isDrifting;

  // Cumulative wheel rotation
  const wheelRadius = spec.kind === 'supercar' ? 0.38 : spec.kind === 'bike' ? 0.32 : spec.kind === 'bus' || spec.kind === 'truck' ? 0.52 : 0.36;
  v.wheelRotation = (v.wheelRotation || 0) + (vLong * safeDt) / wheelRadius;

  // --- Suspension Pitch & Roll Dynamics ---
  const longAccel = (vLong - prevVLong) / safeDt;
  // Longitudinal pitch: nose-dive on brake (+X tilt), squat on accel (-X tilt)
  const targetPitch = clamp(-longAccel * 0.009, -0.09, 0.09);
  v.pitch = (v.pitch || 0) + (targetPitch - (v.pitch || 0)) * (1 - Math.exp(-12 * safeDt));

  // Lateral roll:
  if (spec.kind === 'bike') {
    // 2-wheelers bank INTO the turn (positive roll towards inside of curve)
    const targetRoll = clamp(input.steer * speedRatio * 0.48, -0.48, 0.48);
    v.roll = (v.roll || 0) + (targetRoll - (v.roll || 0)) * (1 - Math.exp(-14 * safeDt));
  } else {
    // 4-wheelers roll AWAY from turn (outward centrifugal lean)
    const turnCentrifugal = (v.steerAngle / 0.55) * speedRatio;
    const rollScale = spec.kind === 'supercar' ? 0.028 : spec.kind === 'cng' ? 0.08 : spec.kind === 'bus' ? 0.065 : 0.045;
    const targetRoll = clamp(-turnCentrifugal * rollScale, -0.1, 0.1);
    v.roll = (v.roll || 0) + (targetRoll - (v.roll || 0)) * (1 - Math.exp(-12 * safeDt));
  }

  // --- World Movement & Collision Resolution ---
  const nextX = v.x + v.vx * safeDt;
  const nextZ = v.z + v.vz * safeDt;
  const collisionRadius = (spec.halfWidth + spec.halfLength) * 0.48;
  const res = resolveCircleCollisions(nextX, nextZ, collisionRadius, colliders);

  let impactSpeed = 0;
  if (res.collided) {
    v.x = res.x;
    v.z = res.z;
    // Calculate velocity component along collision normal
    const vNormal = (v.vx * res.normalX + v.vz * res.normalZ);
    if (vNormal < 0) {
      impactSpeed = Math.abs(vNormal);
      // Rebound normal velocity with restitution
      v.vx -= (1 + 0.22) * vNormal * res.normalX;
      v.vz -= (1 + 0.22) * vNormal * res.normalZ;
      // Scrape friction on tangential velocity
      v.vx *= 0.75;
      v.vz *= 0.75;
      v.speed = v.vx * newFX + v.vz * newFZ;
      v.lateralSlip = v.vx * newRX + v.vz * newRZ;

      if (impactSpeed > 5.0) {
        const dmg = Math.round((impactSpeed - 4.5) * 2.5 * Math.min(2.0, spec.mass));
        v.hp = Math.max(0, v.hp - dmg);
        v.damagedSmoke = v.hp < v.maxHp * 0.45;
      }
    }
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
    unlockedSafehouses: ['old_dhaka_rooftop'],
    inventory: [
      {
        id: 'item_cha_flask',
        name: 'Bengal Thermal Cha Flask',
        banglaName: 'চায়ের ফ্লাস্ক',
        kind: 'food',
        count: 2,
        value: 40,
        description: 'Piping hot Old Dhaka cardamom tea. Restores 30 HP & 40 Stamina.',
      },
    ],
    kills: 0,
    missionsCompleted: 0,
    faresCompleted: 0,
    bountiesCompleted: 0,
    carTheftsCompleted: 0,
    distanceDriven: 0,
    protagonist: 'tariq',
    specialAbilityActive: false,
    specialAbilityTimer: 0,
    specialAbilityCooldown: 0,
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

export function applyVehicleCustomization(
  player: PlayerState,
  vehicle: VehicleState | null,
  option: 'engine' | 'brakes' | 'turbo' | 'spoiler' | 'tint' | 'neon' | 'jammer',
  value?: any,
): { ok: boolean; message: string } {
  if (!vehicle) return { ok: false, message: 'Bring a vehicle to Dhaka Customs Garage first' };
  if (!vehicle.customization) {
    vehicle.customization = {
      engineStage: 0,
      brakesStage: 0,
      hasTurbo: false,
      hasSpoiler: false,
      tintLevel: 0,
      neonColor: null,
      plateJammer: false,
    };
  }
  const cust = vehicle.customization!;

  if (option === 'engine') {
    const nextStage = Math.min(3, (cust.engineStage ?? 0) + 1);
    if (nextStage === (cust.engineStage ?? 0)) return { ok: false, message: 'Engine is already at Max Stage 3 Tuning!' };
    const costs = [0, 800, 1500, 2500];
    const cost = costs[nextStage];
    if (player.money < cost) return { ok: false, message: `Need ৳${cost} for Stage ${nextStage} Engine ECU Tuning` };
    player.money -= cost;
    cust.engineStage = nextStage;
    return { ok: true, message: `Upgraded to Stage ${nextStage} Engine ECU (+${nextStage * 12}% Torque)!` };
  }

  if (option === 'brakes') {
    const nextStage = Math.min(2, (cust.brakesStage ?? 0) + 1);
    if (nextStage === (cust.brakesStage ?? 0)) return { ok: false, message: 'Brakes are already at Race Spec Stage 2!' };
    const costs = [0, 450, 900];
    const cost = costs[nextStage];
    if (player.money < cost) return { ok: false, message: `Need ৳${cost} for Stage ${nextStage} Ceramic Brakes` };
    player.money -= cost;
    cust.brakesStage = nextStage;
    return { ok: true, message: `Installed Stage ${nextStage} Ventilated Brakes (+${nextStage * 25}% Stopping Power)!` };
  }

  if (option === 'turbo') {
    if (cust.hasTurbo) return { ok: false, message: 'Garrett Twin-Scroll Turbo is already installed!' };
    const cost = 1800;
    if (player.money < cost) return { ok: false, message: `Need ৳${cost} for Garrett Twin-Scroll Turbocharger` };
    player.money -= cost;
    cust.hasTurbo = true;
    return { ok: true, message: 'Garrett Twin-Scroll Turbocharger installed (+15% Power Boost)!' };
  }

  if (option === 'spoiler') {
    if (cust.hasSpoiler) return { ok: false, message: 'Carbon Aero Wing is already mounted!' };
    const cost = 700;
    if (player.money < cost) return { ok: false, message: `Need ৳${cost} for High-Downforce Carbon Wing` };
    player.money -= cost;
    cust.hasSpoiler = true;
    return { ok: true, message: 'High-Downforce Carbon Wing mounted (High-Speed Stability enhanced)!' };
  }

  if (option === 'tint') {
    const nextTint = Math.min(2, (cust.tintLevel ?? 0) + 1);
    if (nextTint === (cust.tintLevel ?? 0)) return { ok: false, message: 'Limo Tint (5%) is already applied!' };
    const cost = 350;
    if (player.money < cost) return { ok: false, message: `Need ৳${cost} for Ceramic Privacy Tint` };
    player.money -= cost;
    cust.tintLevel = nextTint;
    return { ok: true, message: `Applied Tier ${nextTint} Ceramic Privacy Window Tint!` };
  }

  if (option === 'neon') {
    const hexColor = typeof value === 'number' ? value : 0x00f3ff;
    const cost = 500;
    if (player.money < cost) return { ok: false, message: `Need ৳${cost} for Dhaka Cyber Neon Underglow` };
    player.money -= cost;
    cust.neonColor = hexColor;
    return { ok: true, message: 'Dhaka Cyber Neon Underglow installed!' };
  }

  if (option === 'jammer') {
    if (cust.plateJammer) return { ok: false, message: 'DMP Digital Plate Jammer is already active!' };
    const cost = 1200;
    if (player.money < cost) return { ok: false, message: `Need ৳${cost} for DMP Digital Plate Jammer` };
    player.money -= cost;
    cust.plateJammer = true;
    return { ok: true, message: 'DMP Digital Plate Jammer activated (Reduces police spot chance)!' };
  }

  return { ok: false, message: 'Unknown customization option' };
}

export function refuelVehicle(
  player: PlayerState,
  vehicle: VehicleState | null,
  amount?: number,
): { ok: boolean; message: string } {
  if (!vehicle) return { ok: false, message: 'Drive a vehicle or boat up to the fuel pump first' };
  const currentFuel = vehicle.fuel ?? 100;
  const maxFuel = vehicle.maxFuel ?? 100;
  if (currentFuel >= maxFuel - 0.5) {
    return { ok: false, message: 'Fuel tank is already completely full (100%)' };
  }
  const toFill = amount !== undefined ? Math.min(amount, maxFuel - currentFuel) : (maxFuel - currentFuel);
  const cost = Math.max(15, Math.round(toFill * 1.5));
  if (player.money < cost) {
    return {
      ok: false,
      message: `Need ৳${cost} to refuel ${Math.round(toFill)}% (${vehicle.fuelType === 'petrol' ? 'Petrol' : vehicle.fuelType === 'cng' ? 'CNG' : 'Diesel'})`,
    };
  }
  player.money -= cost;
  vehicle.fuel = Math.min(maxFuel, currentFuel + toFill);
  vehicle.isStalled = false;
  return { ok: true, message: `Refueled tank (+${Math.round(toFill)}%) for ৳${cost}. Ready to roll!` };
}

export function purchaseSafehouse(
  player: PlayerState,
  safehouseId: SafehouseId,
): { ok: boolean; message: string } {
  const spec = SAFEHOUSE_SPECS.find((s) => s.id === safehouseId);
  if (!spec) return { ok: false, message: 'Invalid safehouse location' };
  if (player.unlockedSafehouses.includes(safehouseId)) {
    return { ok: false, message: `You already own ${spec.name} (${spec.banglaName})` };
  }
  if (player.money < spec.price) {
    return { ok: false, message: `Need ৳${spec.price} to purchase ${spec.name}` };
  }
  player.money -= spec.price;
  player.unlockedSafehouses.push(safehouseId);
  return { ok: true, message: `Purchased property deed for ${spec.name}! Safehouse unlocked.` };
}

export function restAtSafehouse(
  player: PlayerState,
  hours = 6,
): { ok: boolean; message: string } {
  player.hp = player.maxHp;
  player.stamina = player.maxStamina;
  if (player.wantedLevel > 0) {
    player.wantedHeat = Math.max(0, player.wantedHeat - 30);
    player.wantedLevel = Math.min(5, Math.ceil(player.wantedHeat / 20));
    player.wantedCooldown = player.wantedLevel > 0 ? 14 + player.wantedLevel * 4 : 0;
  }
  return { ok: true, message: `Rested for ${hours} hours. Health and stamina fully restored!` };
}

export function useInventoryItem(
  player: PlayerState,
  vehicle: VehicleState | null,
  itemId: string,
): { ok: boolean; message: string } {
  const itemIndex = player.inventory.findIndex((i) => i.id === itemId);
  if (itemIndex < 0) return { ok: false, message: 'Item not found in inventory' };
  const item = player.inventory[itemIndex];
  if (item.count <= 0) return { ok: false, message: 'Item out of stock' };

  if (itemId === 'item_cha_flask') {
    if (player.hp >= player.maxHp && player.stamina >= player.maxStamina) {
      return { ok: false, message: 'Health and stamina already at maximum' };
    }
    player.hp = Math.min(player.maxHp, player.hp + 30);
    player.stamina = Math.min(player.maxStamina, player.stamina + 40);
    item.count--;
    if (item.count <= 0) player.inventory.splice(itemIndex, 1);
    return { ok: true, message: 'Sipped hot cardamom cha (+30 HP, +40 Stamina)!' };
  }

  if (itemId === 'item_first_aid') {
    if (player.hp >= player.maxHp) return { ok: false, message: 'Health already full' };
    player.hp = Math.min(player.maxHp, player.hp + 65);
    item.count--;
    if (item.count <= 0) player.inventory.splice(itemIndex, 1);
    return { ok: true, message: 'Applied tactical field dressing (+65 HP)!' };
  }

  if (itemId === 'item_jerrycan') {
    if (!vehicle) return { ok: false, message: 'Enter or stand close to a vehicle to use emergency fuel' };
    if ((vehicle.fuel ?? 100) >= (vehicle.maxFuel ?? 100) - 2) {
      return { ok: false, message: 'Vehicle fuel tank is already full' };
    }
    vehicle.fuel = Math.min(vehicle.maxFuel ?? 100, (vehicle.fuel ?? 0) + 35);
    vehicle.isStalled = false;
    item.count--;
    if (item.count <= 0) player.inventory.splice(itemIndex, 1);
    return { ok: true, message: 'Poured emergency octane jerrycan (+35% Fuel)! Engine cleared.' };
  }

  return { ok: false, message: `Used ${item.name}` };
}
