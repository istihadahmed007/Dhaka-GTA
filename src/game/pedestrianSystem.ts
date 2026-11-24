import * as THREE from 'three';
import { AabbCollider, NpcState, VehicleState } from './types';
import { resolveCircleCollisions } from './physics';
import { CharacterAnimator } from './characterAnimator';

export interface CrosswalkLocation {
  id: string;
  name: string;
  x1: number;
  z1: number;
  x2: number;
  z2: number;
  roadHeading: 'NS' | 'EW';
}

export const DHAKA_CROSSWALKS: CrosswalkLocation[] = [
  // Shapla Crossing
  { id: 'cw_shapla_n', name: 'Shapla North Crosswalk', x1: -7.5, z1: 12, x2: 7.5, z2: 12, roadHeading: 'EW' },
  { id: 'cw_shapla_s', name: 'Shapla South Crosswalk', x1: -7.5, z1: -12, x2: 7.5, z2: -12, roadHeading: 'EW' },
  { id: 'cw_shapla_e', name: 'Shapla East Crosswalk', x1: 12, z1: -7.5, x2: 12, z2: 7.5, roadHeading: 'NS' },
  { id: 'cw_shapla_w', name: 'Shapla West Crosswalk', x1: -12, z1: -7.5, x2: -12, z2: 7.5, roadHeading: 'NS' },

  // Shahbagh & Kawran
  { id: 'cw_shahbagh', name: 'Shahbagh Avenue Crosswalk', x1: -7.5, z1: -58, x2: 7.5, z2: -58, roadHeading: 'EW' },
  { id: 'cw_kawran', name: 'Kawran Bazar Crosswalk', x1: -58, z1: -70, x2: -58, z2: -58, roadHeading: 'NS' },

  // Paltan & Motijheel
  { id: 'cw_paltan', name: 'Paltan GPO Crosswalk', x1: -7.5, z1: 58, x2: 7.5, z2: 58, roadHeading: 'EW' },
  { id: 'cw_motijheel', name: 'Motijheel Commercial Crosswalk', x1: 58, z1: -7.5, x2: 58, z2: 7.5, roadHeading: 'NS' },

  // Sadarghat Riverfront & Old Dhaka
  { id: 'cw_sadarghat', name: 'Sadarghat Launch Crosswalk', x1: -8.0, z1: 180, x2: 8.0, z2: 180, roadHeading: 'EW' },
  { id: 'cw_nilkhet', name: 'Nilkhet Book Market Crosswalk', x1: -58, z1: -7.5, x2: -58, z2: 7.5, roadHeading: 'NS' },
];

export interface PedestrianActivitySpot {
  id: string;
  kind: 'tea_stall' | 'shop' | 'bus_stop' | 'riverfront';
  name: string;
  x: number;
  z: number;
}

export const DHAKA_PEDESTRIAN_SPOTS: PedestrianActivitySpot[] = [
  // Tonger Cha (Tea Stalls)
  { id: 'spot_tea_rafiq', kind: 'tea_stall', name: 'Rafiq Bhai Cha Stall', x: -16, z: 42 },
  { id: 'spot_tea_sadarghat', kind: 'tea_stall', name: 'Buriganga Ghat Cha', x: 24, z: 176 },
  { id: 'spot_tea_gulshan', kind: 'tea_stall', name: 'Gulshan 1 Tea Stall', x: 42, z: -38 },

  // Street Markets & Shopfronts
  { id: 'spot_shop_babu', kind: 'shop', name: "Babu's Hardware & Gear", x: -24, z: 24 },
  { id: 'spot_chawk_spice', kind: 'shop', name: 'Chawk Spice Emporium', x: -46, z: 22 },
  { id: 'spot_mirpur_bazaar', kind: 'shop', name: 'Mirpur 10 Market', x: -124, z: -184 },
  { id: 'spot_banani_plaza', kind: 'shop', name: 'Banani Road 11 Boutique', x: 45, z: -124 },

  // Riverfront Promenade
  { id: 'spot_river_1', kind: 'riverfront', name: 'Sadarghat Pier Promenade West', x: -28, z: 184 },
  { id: 'spot_river_2', kind: 'riverfront', name: 'Sadarghat Launch Plaza', x: 0, z: 184 },
  { id: 'spot_river_3', kind: 'riverfront', name: 'Sadarghat Pier Promenade East', x: 32, z: 184 },

  // Bus Shelters
  { id: 'spot_bus_farmgate', kind: 'bus_stop', name: 'Farmgate Bus Shelter', x: 9.5, z: -140 },
  { id: 'spot_bus_shahbagh', kind: 'bus_stop', name: 'Shahbagh Bus Shelter', x: 9.5, z: -68 },
  { id: 'spot_bus_shapla', kind: 'bus_stop', name: 'Shapla South Shelter', x: 10.0, z: -16 },
];

/**
 * Checks if a vehicle is approaching a crosswalk.
 */
export function isVehicleApproachingCrosswalk(
  v: VehicleState,
  crosswalk: CrosswalkLocation,
  lookaheadDist = 22,
): boolean {
  const cwCenterX = (crosswalk.x1 + crosswalk.x2) / 2;
  const cwCenterZ = (crosswalk.z1 + crosswalk.z2) / 2;
  const dx = cwCenterX - v.x;
  const dz = cwCenterZ - v.z;
  const dist = Math.hypot(dx, dz);

  if (dist > lookaheadDist || dist < 2.0) return false;

  const fx = Math.sin(v.heading);
  const fz = Math.cos(v.heading);
  const dot = (dx * fx + dz * fz) / Math.max(0.1, dist);
  return dot > 0.65 && Math.abs(v.speed) > 1.5;
}

/**
 * Updates pedestrian state machine, road crossing, social chatting, shop browsing,
 * traffic avoidance, and danger fleeing with AI4Animation motion blending.
 */
export function updateIntelligentPedestrian(
  npc: NpcState,
  dt: number,
  allNpcs: NpcState[],
  vehicles: VehicleState[],
  playerX: number,
  playerZ: number,
  colliders: AabbCollider[],
): void {
  // Police, syndicate, boss, ally have their own specialized combat routines
  if (npc.role !== 'civilian') return;
  if (npc.state === 'ko') return;

  // 1. DANGER REACTION: Gunfire, explosions, or high-speed car crashes trigger panic flee
  if (npc.dangerSourceX !== undefined && npc.dangerSourceZ !== undefined) {
    npc.state = 'flee';
    npc.stateTimer = 6.0;
    const dx = npc.x - npc.dangerSourceX;
    const dz = npc.z - npc.dangerSourceZ;
    const dist = Math.hypot(dx, dz) || 1;
    npc.vx = (dx / dist) * 5.2; // Full panic sprint
    npc.vz = (dz / dist) * 5.2;
    npc.heading = Math.atan2(npc.vx, npc.vz);

    // Fade danger source after reaction initiated
    npc.dangerSourceX = undefined;
    npc.dangerSourceZ = undefined;
  }

  // 2. VEHICLE DODGE: Jump back or sidestep if a vehicle barrels within 4.5m
  for (const v of vehicles) {
    if (Math.abs(v.speed) < 2.5) continue;
    const dx = npc.x - v.x;
    const dz = npc.z - v.z;
    const dist = Math.hypot(dx, dz);
    if (dist < 5.0) {
      const fx = Math.sin(v.heading);
      const fz = Math.cos(v.heading);
      const forwardProj = dx * fx + dz * fz;
      const lateral = -dx * fz + dz * fx;

      if (forwardProj > 0.2 && forwardProj < 4.8 && Math.abs(lateral) < 2.2) {
        // Sidestep away from vehicle path
        const dodgeSign = lateral >= 0 ? 1 : -1;
        npc.vx = -fz * dodgeSign * 4.2;
        npc.vz = fx * dodgeSign * 4.2;
        npc.heading = Math.atan2(npc.vx, npc.vz);
        npc.state = 'walk';
        npc.stateTimer = 1.2;
        break;
      }
    }
  }

  // 3. PEDESTRIAN STATE MACHINE
  switch (npc.state) {
    case 'flee': {
      npc.stateTimer -= dt;
      if (npc.stateTimer <= 0) {
        npc.state = 'walk';
        npc.stateTimer = 4.0;
      }
      break;
    }

    case 'waiting_to_cross': {
      npc.stateTimer -= dt;
      npc.crossLookTimer = (npc.crossLookTimer ?? 0) + dt * 2.5;
      npc.vx = 0;
      npc.vz = 0;

      // Look left and right at curb
      npc.heading += Math.sin(npc.crossLookTimer) * 0.08;

      // Check crosswalk traffic clearance
      let carApproaching = false;
      for (const cw of DHAKA_CROSSWALKS) {
        const cwDist = Math.hypot(npc.x - (cw.x1 + cw.x2) / 2, npc.z - (cw.z1 + cw.z2) / 2);
        if (cwDist < 8.0) {
          for (const v of vehicles) {
            if (isVehicleApproachingCrosswalk(v, cw, 24)) {
              carApproaching = true;
              break;
            }
          }
          break;
        }
      }

      // Safe to step out if no car approaching or waited for > 3.5s
      if (!carApproaching || npc.stateTimer <= 0) {
        npc.state = 'crossing';
        npc.stateTimer = 7.0;
        npc.hurrying = false;
        // Step across perpendicular to road heading
        const headingToTarget = Math.atan2(npc.targetX - npc.x, npc.targetZ - npc.z);
        npc.heading = headingToTarget;
        npc.vx = Math.sin(headingToTarget) * 1.5;
        npc.vz = Math.cos(headingToTarget) * 1.5;
      }
      break;
    }

    case 'crossing': {
      npc.stateTimer -= dt;
      // Check if a vehicle is approaching crosswalk while pedestrian is in the street
      let dangerCarNearby = false;
      for (const v of vehicles) {
        if (Math.abs(v.speed) > 2.0 && Math.hypot(npc.x - v.x, npc.z - v.z) < 11.0) {
          dangerCarNearby = true;
          break;
        }
      }

      // Hurry/jog across street if vehicle nears
      npc.hurrying = dangerCarNearby;
      const crossSpeed = npc.hurrying ? 2.9 : 1.45;
      const dx = npc.targetX - npc.x;
      const dz = npc.targetZ - npc.z;
      const dist = Math.hypot(dx, dz);

      if (dist < 1.2 || npc.stateTimer <= 0) {
        // Reached opposite curb!
        npc.state = 'walk';
        npc.hurrying = false;
        npc.stateTimer = 6.0;
      } else {
        npc.vx = (dx / dist) * crossSpeed;
        npc.vz = (dz / dist) * crossSpeed;
        npc.heading = Math.atan2(npc.vx, npc.vz);
      }
      break;
    }

    case 'chatting': {
      npc.stateTimer -= dt;
      npc.vx = 0;
      npc.vz = 0;
      if (npc.stateTimer <= 0) {
        npc.state = 'walk';
        npc.socialPartnerId = undefined;
        npc.stateTimer = 5.0;
      }
      break;
    }

    case 'browsing_shop': {
      npc.stateTimer -= dt;
      npc.vx = 0;
      npc.vz = 0;
      if (npc.stateTimer <= 0) {
        npc.state = 'walk';
        npc.stateTimer = 6.0;
      }
      break;
    }

    case 'waiting_bus': {
      npc.stateTimer -= dt;
      npc.vx = 0;
      npc.vz = 0;
      if (npc.stateTimer <= 0) {
        npc.state = 'walk';
        npc.stateTimer = 8.0;
      }
      break;
    }

    case 'idle': {
      npc.stateTimer -= dt;
      npc.vx = 0;
      npc.vz = 0;
      if (npc.stateTimer <= 0) {
        npc.state = 'walk';
        npc.stateTimer = 4.0 + Math.random() * 4.0;
      }
      break;
    }

    case 'walk':
    default: {
      npc.stateTimer -= dt;
      const tx = npc.targetX - npc.x;
      const tz = npc.targetZ - npc.z;
      const td = Math.hypot(tx, tz);

      // Random chance to stop at crosswalk, shop, tea stall, or chat with another pedestrian
      if (td < 1.4 || npc.stateTimer <= 0) {
        // Check if near a crosswalk
        let foundCrosswalk = false;
        for (const cw of DHAKA_CROSSWALKS) {
          const d1 = Math.hypot(npc.x - cw.x1, npc.z - cw.z1);
          const d2 = Math.hypot(npc.x - cw.x2, npc.z - cw.z2);
          if (d1 < 4.5 && Math.random() < 0.45) {
            npc.state = 'waiting_to_cross';
            npc.stateTimer = 3.0;
            npc.targetX = cw.x2;
            npc.targetZ = cw.z2;
            foundCrosswalk = true;
            break;
          } else if (d2 < 4.5 && Math.random() < 0.45) {
            npc.state = 'waiting_to_cross';
            npc.stateTimer = 3.0;
            npc.targetX = cw.x1;
            npc.targetZ = cw.z1;
            foundCrosswalk = true;
            break;
          }
        }

        if (!foundCrosswalk) {
          // Check if near an activity spot (tea stall, shop, bus stop)
          let foundSpot = false;
          for (const spot of DHAKA_PEDESTRIAN_SPOTS) {
            if (Math.hypot(npc.x - spot.x, npc.z - spot.z) < 6.5 && Math.random() < 0.35) {
              if (spot.kind === 'tea_stall' || spot.kind === 'shop') {
                npc.state = 'browsing_shop';
                npc.stateTimer = 6.0 + Math.random() * 6.0;
                npc.heading = Math.atan2(spot.x - npc.x, spot.z - npc.z);
              } else if (spot.kind === 'bus_stop') {
                npc.state = 'waiting_bus';
                npc.stateTimer = 8.0 + Math.random() * 8.0;
              }
              foundSpot = true;
              break;
            }
          }

          if (!foundSpot) {
            // Check for chatting with a nearby pedestrian
            let foundChat = false;
            for (const other of allNpcs) {
              if (other.id === npc.id || other.role !== 'civilian') continue;
              if (other.state === 'walk' && Math.hypot(npc.x - other.x, npc.z - other.z) < 3.2 && Math.random() < 0.22) {
                npc.state = 'chatting';
                npc.stateTimer = 7.0 + Math.random() * 5.0;
                npc.socialPartnerId = other.id;
                other.state = 'chatting';
                other.stateTimer = npc.stateTimer;
                other.socialPartnerId = npc.id;

                // Face each other
                npc.heading = Math.atan2(other.x - npc.x, other.z - npc.z);
                other.heading = Math.atan2(npc.x - other.x, npc.z - other.z);
                foundChat = true;
                break;
              }
            }

            if (!foundChat) {
              // Stroll to a new sidewalk spot
              const angle = Math.random() * Math.PI * 2;
              const wanderDist = 12 + Math.random() * 20;
              npc.targetX = npc.x + Math.sin(angle) * wanderDist;
              npc.targetZ = npc.z + Math.cos(angle) * wanderDist;
              npc.stateTimer = 4.0 + Math.random() * 4.0;
            }
          }
        }
      } else {
        const strollSpeed = 1.35;
        npc.vx = (tx / td) * strollSpeed;
        npc.vz = (tz / td) * strollSpeed;
        npc.heading = Math.atan2(npc.vx, npc.vz);
      }
      break;
    }
  }

  // 4. Resolve static collider penetration (walls, barriers, riverfront)
  const resolved = resolveCircleCollisions(npc.x + npc.vx * dt, npc.z + npc.vz * dt, 0.45, colliders);
  npc.x = resolved.x;
  npc.z = resolved.z;
}

/**
 * Updates CharacterAnimator with AI4Animation phase synchronization,
 * start/stop inertia, banking lean, and distinct gaits for the pedestrian.
 */
export function updatePedestrianAnimator(
  animator: CharacterAnimator,
  npc: NpcState,
  dt: number,
): void {
  const speed = Math.hypot(npc.vx, npc.vz);
  const isGrounded = true;

  if (npc.state === 'ko') {
    animator.update(dt, { speed: 0, isKo: true, isGrounded });
    return;
  }

  if (npc.state === 'flee') {
    // High-speed panic flee sprint with dynamic banking
    animator.update(dt, {
      speed,
      maxWalkSpeed: 3.2,
      maxRunSpeed: 5.6,
      isGrounded,
    });
  } else if (npc.state === 'crossing' && npc.hurrying) {
    // Hurrying/jogging across street
    animator.update(dt, {
      speed,
      maxWalkSpeed: 2.2,
      maxRunSpeed: 3.8,
      isGrounded,
    });
  } else if (npc.state === 'waiting_to_cross' || npc.state === 'chatting' || npc.state === 'browsing_shop' || npc.state === 'waiting_bus') {
    // Subtle idle breathing and social presence
    animator.update(dt, {
      speed: 0,
      isGrounded,
    });
  } else {
    // Calm sidewalk stroll
    animator.update(dt, {
      speed,
      maxWalkSpeed: 3.0,
      maxRunSpeed: 5.5,
      isGrounded,
    });
  }
}
