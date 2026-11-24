import * as THREE from 'three';
import { AabbCollider, VehicleKind, VehicleState, VEHICLE_SPECS, isWatercraftKind } from './types';
export { isWatercraftKind };
import { VehicleInput } from './physics';
import { RIVER_Z_START } from './worldData';

export const BURIGANGA_WATER_LEVEL = -1.05;
export const BURIGANGA_MIN_X = -540;
export const BURIGANGA_MAX_X = 540;
export const BURIGANGA_MIN_Z = RIVER_Z_START + 1.2; // Sadarghat embankment edge (z = 201.2)
export const BURIGANGA_MAX_Z = RIVER_Z_START + 195.0; // Keraniganj shoreline (z = 395.0)

export interface RiverDockBerth {
  id: string;
  name: string;
  banglaName: string;
  x: number;
  z: number;
  heading: number;
  district: 'sadarghat' | 'keraniganj';
  allowedKinds: VehicleKind[];
}

/**
 * 6 Designated River Docks along Sadarghat Riverfront and Keraniganj Southern Shore.
 */
export const RIVER_DOCKS: RiverDockBerth[] = [
  {
    id: 'dock_sadarghat_west',
    name: 'Sadarghat Ghat 1 (West Jetty)',
    banglaName: 'সদরঘাট ১ নং ঘাট (পশ্চিম)',
    x: -72,
    z: RIVER_Z_START + 4.8,
    heading: 0,
    district: 'sadarghat',
    allowedKinds: ['launch', 'speedboat', 'jetski'],
  },
  {
    id: 'dock_sadarghat_central_launch',
    name: 'Sadarghat Ghat 2 (Central Launch Terminal)',
    banglaName: 'সদরঘাট ২ নং প্রধান লঞ্চ টার্মিনাল',
    x: -26,
    z: RIVER_Z_START + 5.2,
    heading: 0,
    district: 'sadarghat',
    allowedKinds: ['launch'],
  },
  {
    id: 'dock_sadarghat_east_launch',
    name: 'Sadarghat Ghat 3 (East Launch Berth)',
    banglaName: 'সদরঘাট ৩ নং পূর্ব লঞ্চ ঘাট',
    x: 26,
    z: RIVER_Z_START + 5.2,
    heading: 0,
    district: 'sadarghat',
    allowedKinds: ['launch', 'speedboat'],
  },
  {
    id: 'dock_sadarghat_speedboat',
    name: 'Sadarghat Ghat 4 (Speedboat & Jet Ski Jetty)',
    banglaName: 'সদরঘাট ৪ নং স্পিডবোট ঘাট',
    x: 78,
    z: RIVER_Z_START + 4.5,
    heading: 0,
    district: 'sadarghat',
    allowedKinds: ['speedboat', 'jetski'],
  },
  {
    id: 'dock_keraniganj_ferry',
    name: 'Keraniganj South Ferry Ghat',
    banglaName: 'কেরানীগঞ্জ দক্ষিণ ঘাট',
    x: -26,
    z: RIVER_Z_START + 188.0,
    heading: Math.PI,
    district: 'keraniganj',
    allowedKinds: ['launch', 'speedboat', 'jetski'],
  },
  {
    id: 'dock_keraniganj_shipyard',
    name: 'Keraniganj Shipyard Pier',
    banglaName: 'কেরানীগঞ্জ ডকইয়ার্ড জেটি',
    x: 65,
    z: RIVER_Z_START + 186.5,
    heading: Math.PI,
    district: 'keraniganj',
    allowedKinds: ['launch', 'speedboat', 'jetski'],
  },
];

export interface WaterWaveSample {
  height: number;
  normalX: number;
  normalZ: number;
  currentVx: number;
  currentVz: number;
}

/**
 * Calculates continuous procedural river surface elevation, wave slopes and water currents.
 */
export function getRiverWaveSample(x: number, z: number, timeSec: number): WaterWaveSample {
  // Primary gentle river swell
  const k1 = 0.045;
  const k2 = 0.075;
  const w1 = 1.7;
  const arg1 = timeSec * w1 + x * k1 + z * k2;
  const sin1 = Math.sin(arg1);
  const cos1 = Math.cos(arg1);
  const a1 = 0.07;

  // Secondary cross ripple
  const k3 = -0.06;
  const k4 = 0.055;
  const w2 = 2.4;
  const arg2 = timeSec * w2 + x * k3 + z * k4;
  const sin2 = Math.sin(arg2);
  const cos2 = Math.cos(arg2);
  const a2 = 0.04;

  // Micro chop
  const chop = Math.sin(timeSec * 3.8 + (x + z) * 0.12) * 0.018;

  const height = BURIGANGA_WATER_LEVEL + a1 * sin1 + a2 * sin2 + chop;

  // Analytical partial derivatives for wave normals
  const dhdx = a1 * k1 * cos1 + a2 * k3 * cos2;
  const dhdz = a1 * k2 * cos1 + a2 * k4 * cos2;

  // Downstream Buriganga current (gentle eastward flow toward Meghna)
  const currentVx = 0.32;
  const currentVz = 0.0;

  return {
    height,
    normalX: -dhdx,
    normalZ: -dhdz,
    currentVx,
    currentVz,
  };
}

export interface WatercraftStepResult {
  collided: boolean;
  impactSpeed: number;
  isPlaning: boolean;
  inWater: boolean;
  wakeIntensity: number;
  spawnSpray: boolean;
}

/**
 * Steps realistic watercraft kinematics: Archimedes buoyancy, hydrodynamic drag,
 * planing hull lift, rudder steering, wave pitch/roll, and river boundary collisions.
 */
export function stepWatercraftKinematics(
  v: VehicleState,
  input: VehicleInput,
  dt: number,
  colliders: AabbCollider[],
  timeSec = 0,
): WatercraftStepResult {
  const spec = VEHICLE_SPECS[v.kind];
  v.isWatercraft = true;

  // 1. Steering & Hydrodynamic Rudder
  const steerSpeed = v.kind === 'jetski' ? 7.2 : v.kind === 'speedboat' ? 5.5 : 2.4;
  const targetSteerAngle = input.steer * (v.kind === 'jetski' ? 0.58 : v.kind === 'speedboat' ? 0.48 : 0.32);
  v.steerAngle += (targetSteerAngle - v.steerAngle) * Math.min(1, dt * steerSpeed);

  // In watercraft, steering efficacy depends on water flow over rudder / jet nozzle
  const speedMag = Math.abs(v.speed);
  const flowFactor = v.kind === 'jetski'
    ? (Math.abs(input.throttle) > 0.1 ? 1.0 : Math.max(0.35, speedMag / 8.0))
    : Math.max(0.25, Math.min(1.0, speedMag / 6.0));

  const turnDelta = v.steerAngle * spec.turnRate * flowFactor * (v.speed >= 0 ? 1 : -0.75) * dt;
  v.heading += turnDelta;

  // 2. Longitudinal Thrust & Hydrodynamic Resistance
  let accelForce = 0;
  if (input.throttle > 0) {
    accelForce = spec.accel * input.throttle;
  } else if (input.throttle < 0) {
    // Astern thrust / reverse propeller
    accelForce = -spec.brake * 0.7 * Math.abs(input.throttle);
  }

  // Water brake / reverse thrust
  if (input.handbrake) {
    accelForce -= Math.sign(v.speed) * spec.brake * 1.4;
  }

  // Quadratic hydrodynamic water resistance + skin friction
  const quadDrag = v.kind === 'jetski' ? 0.024 : v.kind === 'speedboat' ? 0.018 : 0.038;
  const skinDrag = spec.drag * 0.45;
  const totalWaterDrag = (quadDrag * v.speed * v.speed + skinDrag) * Math.sign(v.speed);

  v.speed += (accelForce - totalWaterDrag) * dt;
  v.speed = THREE.MathUtils.clamp(v.speed, -spec.reverseSpeed, spec.maxSpeed);

  // Come to complete rest if throttle released and moving very slowly
  if (Math.abs(input.throttle) < 0.05 && Math.abs(v.speed) < 0.15) {
    v.speed = 0;
  }

  // 3. Planing Hull Dynamics
  // At speed > 6.5 m/s (jetski) or > 8.0 m/s (speedboat), hull planes up onto water surface
  const planingThreshold = v.kind === 'jetski' ? 6.5 : 8.0;
  const isPlaning = (v.kind === 'speedboat' || v.kind === 'jetski') && speedMag > planingThreshold;
  v.isPlaning = isPlaning;
  const planingLift = isPlaning
    ? Math.min(0.24, (speedMag - planingThreshold) * 0.015)
    : 0;

  // 4. World Movement with gentle Buriganga River Current drift
  const wave = getRiverWaveSample(v.x, v.z, timeSec);
  const fx = Math.sin(v.heading);
  const fz = Math.cos(v.heading);

  const prevX = v.x;
  const prevZ = v.z;

  const currentDriftScale = v.speed === 0 ? 0.25 : 0.05;
  v.x += (fx * v.speed + wave.currentVx * currentDriftScale) * dt;
  v.z += (fz * v.speed + wave.currentVz * currentDriftScale) * dt;

  // 5. Archimedes Buoyancy & Dynamic Pitch / Roll
  const targetY = wave.height + (v.kind === 'launch' ? 0.42 : v.kind === 'speedboat' ? 0.22 : 0.14) + planingLift;
  const buoyK = v.kind === 'launch' ? 8.0 : 16.0;
  const buoyDamp = v.kind === 'launch' ? 3.8 : 5.2;

  v.buoyancyVy = v.buoyancyVy ?? 0;
  const deltaY = targetY - v.y;
  v.buoyancyVy += (deltaY * buoyK - v.buoyancyVy * buoyDamp) * dt;
  v.y += v.buoyancyVy * dt;

  // Pitch from wave slope + acceleration squat/planing bow rise
  const bowRise = isPlaning ? 0.075 : (input.throttle > 0 ? 0.035 : 0);
  const wavePitch = wave.normalZ * Math.cos(v.heading) + wave.normalX * Math.sin(v.heading);
  const targetPitch = wavePitch * 0.4 + bowRise - (input.handbrake ? 0.03 : 0);
  v.pitch = (v.pitch ?? 0) + (targetPitch - (v.pitch ?? 0)) * Math.min(1, dt * 6);

  // Roll from wave slope + centrifugal banking lean into turns
  // Speedboats and Jet Skis bank deeply INWARD into turns; Launch lists gently OUTWARD
  const bankingFactor = v.kind === 'jetski' ? 0.42 : v.kind === 'speedboat' ? 0.32 : -0.045;
  const turnRateRad = (v.speed / Math.max(1, spec.maxSpeed)) * v.steerAngle;
  const waveRoll = -wave.normalX * Math.cos(v.heading) + wave.normalZ * Math.sin(v.heading);
  const targetRoll = waveRoll * 0.3 + turnRateRad * bankingFactor;
  v.roll = (v.roll ?? 0) + (targetRoll - (v.roll ?? 0)) * Math.min(1, dt * 6.5);

  // 6. Collision Resolution: River Shorelines, Embankment, and Jetties
  let collided = false;
  let impactSpeed = 0;

  // Keep craft inside playable Buriganga channel
  if (v.x < BURIGANGA_MIN_X + spec.halfWidth) {
    v.x = BURIGANGA_MIN_X + spec.halfWidth;
    collided = true;
    impactSpeed = Math.abs(v.speed);
    v.speed *= -0.3;
  } else if (v.x > BURIGANGA_MAX_X - spec.halfWidth) {
    v.x = BURIGANGA_MAX_X - spec.halfWidth;
    collided = true;
    impactSpeed = Math.abs(v.speed);
    v.speed *= -0.3;
  }

  // Southern Keraniganj shore limit
  if (v.z > BURIGANGA_MAX_Z - spec.halfLength) {
    v.z = BURIGANGA_MAX_Z - spec.halfLength;
    collided = true;
    impactSpeed = Math.abs(v.speed);
    v.speed *= -0.3;
  }

  // Northern Sadarghat embankment: watercraft cannot climb onto dry land
  const minAllowedZ = BURIGANGA_MIN_Z + spec.halfLength;
  if (v.z < minAllowedZ) {
    // Check if watercraft is nicely docked at an authorized jetty berth
    let nearDock = false;
    for (const dock of RIVER_DOCKS) {
      if (Math.hypot(v.x - dock.x, v.z - dock.z) < 8.5) {
        nearDock = true;
        break;
      }
    }
    if (!nearDock) {
      v.z = minAllowedZ;
      collided = true;
      impactSpeed = Math.abs(v.speed);
      v.speed *= -0.3;
    }
  }

  // Resolve collisions against static bridge pylons and waterfront piers
  for (const c of colliders) {
    if (c.kind !== 'pillar' && c.kind !== 'wall') continue;
    // Check bounding box intersection
    const minX = c.minX - spec.halfWidth;
    const maxX = c.maxX + spec.halfWidth;
    const minZ = c.minZ - spec.halfLength;
    const maxZ = c.maxZ + spec.halfLength;
    if (v.x >= minX && v.x <= maxX && v.z >= minZ && v.z <= maxZ) {
      collided = true;
      impactSpeed = Math.abs(v.speed);
      v.x = prevX;
      v.z = prevZ;
      v.speed *= -0.4;
      break;
    }
  }

  if (collided && impactSpeed > 6.0) {
    const damage = Math.round(impactSpeed * 3.5);
    v.hp = Math.max(0, v.hp - damage);
  }

  // 7. Wakes and Sprays
  v.wakeTimer = (v.wakeTimer ?? 0) + dt;
  const wakeIntensity = Math.min(1.0, speedMag / (spec.maxSpeed * 0.75));
  const spawnSpray = speedMag > 5.5 && (v.kind === 'jetski' || v.kind === 'speedboat');

  return {
    collided,
    impactSpeed,
    isPlaning,
    inWater: true,
    wakeIntensity,
    spawnSpray,
  };
}

/**
 * Checks whether a given world coordinate is in the Buriganga River water channel.
 */
export function isPointInBurigangaWater(x: number, z: number): boolean {
  if (x < BURIGANGA_MIN_X || x > BURIGANGA_MAX_X) return false;
  if (z < BURIGANGA_MIN_Z || z > BURIGANGA_MAX_Z) return false;

  // The 4 Sadarghat jetties are dry walking surfaces extending into water:
  // Jetties at x = -72, -26, 26, 78 extending from z = 197.5 to 211.5
  const onSadarghatJetty = [-72, -26, 26, 78].some(
    (jx) => Math.abs(x - jx) < 3.4 && z >= RIVER_Z_START - 2.5 && z <= RIVER_Z_START + 11.5,
  );
  if (onSadarghatJetty) return false;

  // Keraniganj south piers are dry walking surfaces
  const onKeraniganjPier = [-26, 65].some(
    (kx) => Math.abs(x - kx) < 4.0 && z >= RIVER_Z_START + 184.0 && z <= RIVER_Z_START + 196.0,
  );
  if (onKeraniganjPier) return false;

  return true;
}

/**
 * Finds the nearest dock berth within range.
 */
export function getNearestRiverDock(x: number, z: number, maxDist = 9.0): RiverDockBerth | null {
  let closest: RiverDockBerth | null = null;
  let closestDist = maxDist;
  for (const dock of RIVER_DOCKS) {
    const d = Math.hypot(x - dock.x, z - dock.z);
    if (d < closestDist) {
      closestDist = d;
      closest = dock;
    }
  }
  return closest;
}

/**
 * Neatly docks and secures a watercraft alongside a pier berth.
 */
export function dockWatercraftAtBerth(v: VehicleState, dock: RiverDockBerth): void {
  v.x = dock.x;
  v.z = dock.z;
  v.heading = dock.heading;
  v.speed = 0;
  v.steerAngle = 0;
  v.dockedAt = dock.id;
}
