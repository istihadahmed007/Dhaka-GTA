import * as THREE from 'three';
import { AabbCollider, VehicleKind, VehicleState, VEHICLE_SPECS } from './types';
import { stepVehicleKinematics } from './physics';
import { TRAFFIC_ROUTES } from './worldData';

export type TrafficLightPhase = 'NS_GREEN' | 'NS_YELLOW' | 'EW_GREEN' | 'EW_YELLOW';

export interface TrafficIntersection {
  id: string;
  name: string;
  banglaName: string;
  x: number;
  z: number;
  radius: number;
  cycleTime: number; // e.g. 28s
  cycleOffset: number; // Staggered phase offset
  nsGreenDuration: number; // e.g. 11s
  nsYellowDuration: number; // e.g. 3s
  ewGreenDuration: number; // e.g. 11s
  ewYellowDuration: number; // e.g. 3s
}

/**
 * 12 Major Dhaka Intersections with traffic light controllers across the road network
 */
export const TRAFFIC_INTERSECTIONS: TrafficIntersection[] = [
  {
    id: 'shapla_crossing',
    name: 'Shapla Crossing (Grand Central)',
    banglaName: 'শাপলা চত্বর ক্রসিং',
    x: 0,
    z: 0,
    radius: 18,
    cycleTime: 28,
    cycleOffset: 0,
    nsGreenDuration: 11,
    nsYellowDuration: 3,
    ewGreenDuration: 11,
    ewYellowDuration: 3,
  },
  {
    id: 'shahbagh_crossing',
    name: 'Shahbagh Central Junction',
    banglaName: 'শাহবাগ মোড়',
    x: 0,
    z: -64,
    radius: 15,
    cycleTime: 28,
    cycleOffset: 7,
    nsGreenDuration: 11,
    nsYellowDuration: 3,
    ewGreenDuration: 11,
    ewYellowDuration: 3,
  },
  {
    id: 'kawran_flyover_crossing',
    name: 'Kawran Bazar Flyover Junction',
    banglaName: 'কারওয়ান বাজার মোড়',
    x: -64,
    z: -64,
    radius: 15,
    cycleTime: 28,
    cycleOffset: 14,
    nsGreenDuration: 11,
    nsYellowDuration: 3,
    ewGreenDuration: 11,
    ewYellowDuration: 3,
  },
  {
    id: 'hatirjheel_link',
    name: 'Gulshan / Hatirjheel Link Junction',
    banglaName: 'হাতিরঝিল লিংক মোড়',
    x: 64,
    z: -64,
    radius: 15,
    cycleTime: 28,
    cycleOffset: 21,
    nsGreenDuration: 11,
    nsYellowDuration: 3,
    ewGreenDuration: 11,
    ewYellowDuration: 3,
  },
  {
    id: 'paltan_crossing',
    name: 'Paltan / GPO Crossing',
    banglaName: 'পল্টন মোড়',
    x: 0,
    z: 64,
    radius: 15,
    cycleTime: 28,
    cycleOffset: 10,
    nsGreenDuration: 11,
    nsYellowDuration: 3,
    ewGreenDuration: 11,
    ewYellowDuration: 3,
  },
  {
    id: 'motijheel_center',
    name: 'Motijheel Commercial Crossing',
    banglaName: 'মতিঝিল বাণিজ্যিক মোড়',
    x: 64,
    z: 0,
    radius: 15,
    cycleTime: 28,
    cycleOffset: 5,
    nsGreenDuration: 11,
    nsYellowDuration: 3,
    ewGreenDuration: 11,
    ewYellowDuration: 3,
  },
  {
    id: 'dhanmondi_nilkhet',
    name: 'Dhanmondi / Nilkhet Junction',
    banglaName: 'নীলক্ষেত মোড়',
    x: -64,
    z: 0,
    radius: 15,
    cycleTime: 28,
    cycleOffset: 18,
    nsGreenDuration: 11,
    nsYellowDuration: 3,
    ewGreenDuration: 11,
    ewYellowDuration: 3,
  },
  {
    id: 'sadarghat_crossing',
    name: 'Sadarghat Riverfront Crossing',
    banglaName: 'সদরঘাট লঞ্চ ঘাট মোড়',
    x: 0,
    z: 186,
    radius: 16,
    cycleTime: 28,
    cycleOffset: 12,
    nsGreenDuration: 11,
    nsYellowDuration: 3,
    ewGreenDuration: 11,
    ewYellowDuration: 3,
  },
  {
    id: 'farmgate_metro',
    name: 'Farmgate Metro Line 6 Crossing',
    banglaName: 'ফার্মগেট মোড়',
    x: 0,
    z: -144,
    radius: 15,
    cycleTime: 28,
    cycleOffset: 2,
    nsGreenDuration: 11,
    nsYellowDuration: 3,
    ewGreenDuration: 11,
    ewYellowDuration: 3,
  },
  {
    id: 'gulshan_1_circle',
    name: 'Gulshan 1 Circle',
    banglaName: 'গুলশান ১ চত্বর',
    x: 64,
    z: -144,
    radius: 15,
    cycleTime: 28,
    cycleOffset: 16,
    nsGreenDuration: 11,
    nsYellowDuration: 3,
    ewGreenDuration: 11,
    ewYellowDuration: 3,
  },
  {
    id: 'dhanmondi_27_crossing',
    name: 'Dhanmondi 27 Avenue Junction',
    banglaName: 'ধানমন্ডি ২৭ মোড়',
    x: -144,
    z: 0,
    radius: 15,
    cycleTime: 28,
    cycleOffset: 9,
    nsGreenDuration: 11,
    nsYellowDuration: 3,
    ewGreenDuration: 11,
    ewYellowDuration: 3,
  },
  {
    id: 'mirpur_10_crossing',
    name: 'Mirpur 10 Roundabout Crossing',
    banglaName: 'মিরপুর ১০ চত্বর',
    x: -144,
    z: -144,
    radius: 15,
    cycleTime: 28,
    cycleOffset: 23,
    nsGreenDuration: 11,
    nsYellowDuration: 3,
    ewGreenDuration: 11,
    ewYellowDuration: 3,
  },
];

/**
 * Calculates current traffic light phase for an intersection based on play time.
 */
export function getIntersectionPhase(intersection: TrafficIntersection, timeSec: number): TrafficLightPhase {
  const t = (timeSec + intersection.cycleOffset) % intersection.cycleTime;
  const nsGreenEnd = intersection.nsGreenDuration;
  const nsYellowEnd = nsGreenEnd + intersection.nsYellowDuration;
  const ewGreenEnd = nsYellowEnd + intersection.ewGreenDuration;

  if (t < nsGreenEnd) return 'NS_GREEN';
  if (t < nsYellowEnd) return 'NS_YELLOW';
  if (t < ewGreenEnd) return 'EW_GREEN';
  return 'EW_YELLOW';
}

/**
 * Checks whether a vehicle approaching an intersection faces a RED or YELLOW light.
 * In Dhaka, vehicles stop before entering the box when light is red/yellow.
 */
export function isVehicleStoppedByTrafficLight(
  v: VehicleState,
  intersection: TrafficIntersection,
  timeSec: number,
): boolean {
  const dx = intersection.x - v.x;
  const dz = intersection.z - v.z;
  const dist = Math.hypot(dx, dz);

  // If outside approach zone (4m to 24m), not affected
  if (dist > 24 || dist < 4.5) return false;

  // Dot product with heading to verify vehicle is heading TOWARDS intersection
  const fx = Math.sin(v.heading);
  const fz = Math.cos(v.heading);
  const toIntersectionDot = (dx * fx + dz * fz) / Math.max(0.1, dist);
  if (toIntersectionDot < 0.6) return false;

  const phase = getIntersectionPhase(intersection, timeSec);
  const isNorthSouth = Math.abs(fz) > Math.abs(fx);

  if (isNorthSouth) {
    // Heading along Z: blocked if EW has green/yellow (NS is RED) or if NS is YELLOW and vehicle can stop safely
    return phase === 'EW_GREEN' || phase === 'EW_YELLOW' || (phase === 'NS_YELLOW' && dist > 7);
  } else {
    // Heading along X: blocked if NS has green/yellow (EW is RED) or if EW is YELLOW and vehicle can stop safely
    return phase === 'NS_GREEN' || phase === 'NS_YELLOW' || (phase === 'EW_YELLOW' && dist > 7);
  }
}

export interface TrafficLightHandles {
  group: THREE.Group;
  update: (timeSec: number) => void;
  dispose: () => void;
}

/**
 * Creates 3D visual traffic signal gantries placed at all major Dhaka intersections.
 */
export function createTrafficLightVisuals(): TrafficLightHandles {
  const group = new THREE.Group();
  const disposables: Array<{ dispose: () => void }> = [];

  const poleMat = new THREE.MeshStandardMaterial({ color: 0x334155, roughness: 0.7, metalness: 0.6 });
  const housingMat = new THREE.MeshStandardMaterial({ color: 0x0f172a, roughness: 0.85, metalness: 0.1 });
  disposables.push(poleMat, housingMat);

  const poleGeo = new THREE.CylinderGeometry(0.11, 0.13, 5.4, 8);
  const armGeo = new THREE.CylinderGeometry(0.07, 0.07, 3.8, 8);
  armGeo.rotateZ(Math.PI / 2);
  const boxGeo = new THREE.BoxGeometry(0.38, 1.15, 0.32);
  const lensGeo = new THREE.SphereGeometry(0.12, 10, 8);
  lensGeo.scale(1, 1, 0.45);
  disposables.push(poleGeo, armGeo, boxGeo, lensGeo);

  interface IntersectionLightPair {
    intersection: TrafficIntersection;
    nsRedMat: THREE.MeshStandardMaterial;
    nsYellowMat: THREE.MeshStandardMaterial;
    nsGreenMat: THREE.MeshStandardMaterial;
    ewRedMat: THREE.MeshStandardMaterial;
    ewYellowMat: THREE.MeshStandardMaterial;
    ewGreenMat: THREE.MeshStandardMaterial;
  }

  const lightPairs: IntersectionLightPair[] = [];

  for (const inter of TRAFFIC_INTERSECTIONS) {
    const nsRedMat = new THREE.MeshStandardMaterial({ color: 0xff0000, emissive: 0xff0000, emissiveIntensity: 0.1 });
    const nsYellowMat = new THREE.MeshStandardMaterial({ color: 0xffaa00, emissive: 0xffaa00, emissiveIntensity: 0.1 });
    const nsGreenMat = new THREE.MeshStandardMaterial({ color: 0x00ff44, emissive: 0x00ff44, emissiveIntensity: 0.1 });

    const ewRedMat = new THREE.MeshStandardMaterial({ color: 0xff0000, emissive: 0xff0000, emissiveIntensity: 0.1 });
    const ewYellowMat = new THREE.MeshStandardMaterial({ color: 0xffaa00, emissive: 0xffaa00, emissiveIntensity: 0.1 });
    const ewGreenMat = new THREE.MeshStandardMaterial({ color: 0x00ff44, emissive: 0x00ff44, emissiveIntensity: 0.1 });

    disposables.push(nsRedMat, nsYellowMat, nsGreenMat, ewRedMat, ewYellowMat, ewGreenMat);

    // Place 2 diagonal signal posts at intersection curbs
    const corners = [
      { x: inter.x - inter.radius * 0.78, z: inter.z - inter.radius * 0.78, rot: 0 },
      { x: inter.x + inter.radius * 0.78, z: inter.z + inter.radius * 0.78, rot: Math.PI },
    ];

    for (const corner of corners) {
      const post = new THREE.Group();
      post.position.set(corner.x, 0, corner.z);
      post.rotation.y = corner.rot;

      const pole = new THREE.Mesh(poleGeo, poleMat);
      pole.position.y = 2.7;
      const arm = new THREE.Mesh(armGeo, poleMat);
      arm.position.set(1.9, 5.0, 0);
      post.add(pole, arm);

      // NS Signal Head
      const boxNS = new THREE.Mesh(boxGeo, housingMat);
      boxNS.position.set(1.9, 4.8, 0);
      const rLensNS = new THREE.Mesh(lensGeo, nsRedMat);
      rLensNS.position.set(0, 0.36, 0.17);
      const yLensNS = new THREE.Mesh(lensGeo, nsYellowMat);
      yLensNS.position.set(0, 0, 0.17);
      const gLensNS = new THREE.Mesh(lensGeo, nsGreenMat);
      gLensNS.position.set(0, -0.36, 0.17);
      boxNS.add(rLensNS, yLensNS, gLensNS);
      post.add(boxNS);

      // EW Signal Head
      const boxEW = new THREE.Mesh(boxGeo, housingMat);
      boxEW.position.set(1.9, 4.8, 0);
      boxEW.rotation.y = Math.PI / 2;
      const rLensEW = new THREE.Mesh(lensGeo, ewRedMat);
      rLensEW.position.set(0, 0.36, 0.17);
      const yLensEW = new THREE.Mesh(lensGeo, ewYellowMat);
      yLensEW.position.set(0, 0, 0.17);
      const gLensEW = new THREE.Mesh(lensGeo, ewGreenMat);
      gLensEW.position.set(0, -0.36, 0.17);
      boxEW.add(rLensEW, yLensEW, gLensEW);
      post.add(boxEW);

      group.add(post);
    }

    lightPairs.push({
      intersection: inter,
      nsRedMat,
      nsYellowMat,
      nsGreenMat,
      ewRedMat,
      ewYellowMat,
      ewGreenMat,
    });
  }

  const update = (timeSec: number) => {
    for (const pair of lightPairs) {
      const phase = getIntersectionPhase(pair.intersection, timeSec);
      const isNsGreen = phase === 'NS_GREEN';
      const isNsYellow = phase === 'NS_YELLOW';
      const isEwGreen = phase === 'EW_GREEN';
      const isEwYellow = phase === 'EW_YELLOW';

      // NS materials
      pair.nsGreenMat.emissiveIntensity = isNsGreen ? 3.8 : 0.08;
      pair.nsYellowMat.emissiveIntensity = isNsYellow ? 3.8 : 0.08;
      pair.nsRedMat.emissiveIntensity = !isNsGreen && !isNsYellow ? 3.8 : 0.08;

      // EW materials
      pair.ewGreenMat.emissiveIntensity = isEwGreen ? 3.8 : 0.08;
      pair.ewYellowMat.emissiveIntensity = isEwYellow ? 3.8 : 0.08;
      pair.ewRedMat.emissiveIntensity = !isEwGreen && !isEwYellow ? 3.8 : 0.08;
    }
  };

  const dispose = () => {
    disposables.forEach((d) => d.dispose());
  };

  return { group, update, dispose };
}

/**
 * Designated Dhaka Bus Stop Locations where City Buses pause for boarding.
 */
export const DHAKA_BUS_STOPS = [
  { id: 'stop_farmgate', x: 7.5, z: -140, name: 'Farmgate Bus Stand' },
  { id: 'stop_shapla', x: 8.0, z: -16, name: 'Shapla South Stand' },
  { id: 'stop_sadarghat', x: 9.5, z: 180, name: 'Sadarghat Launch Stand' },
  { id: 'stop_shahbagh', x: 7.5, z: -68, name: 'Shahbagh Stand' },
  { id: 'stop_gulshan', x: 69, z: -140, name: 'Gulshan 1 Bus Stand' },
  { id: 'stop_motijheel', x: 68, z: 12, name: 'Motijheel Commercial Stand' },
];

/**
 * Checks if a vehicle is approaching a designated bus stop.
 */
export function isNearBusStop(v: VehicleState): boolean {
  for (const stop of DHAKA_BUS_STOPS) {
    if (Math.hypot(v.x - stop.x, v.z - stop.z) < 12) return true;
  }
  return false;
}

export interface ObstacleDetection {
  blocked: boolean;
  leadDistance: number;
  leadSpeed: number;
  isLeadSlow: boolean;
  pedestrianAhead: boolean;
}

/**
 * Forward detection cone checking for other vehicles, crossing pedestrians, or obstacles.
 */
export function detectForwardObstacles(
  v: VehicleState,
  allVehicles: VehicleState[],
  playerX: number,
  playerZ: number,
  playerVehicleId: string | null,
  crossingPedestrians?: Array<{ x: number; z: number; isCrossing: boolean }>,
): ObstacleDetection {
  const fx = Math.sin(v.heading);
  const fz = Math.cos(v.heading);

  let leadDistance = 999;
  let leadSpeed = 999;
  let isLeadSlow = false;
  let pedestrianAhead = false;

  // Check player on foot or in vehicle
  if (playerVehicleId !== v.id) {
    const pDx = playerX - v.x;
    const pDz = playerZ - v.z;
    const pDist = Math.hypot(pDx, pDz);
    if (pDist < 16) {
      const forwardProj = (pDx * fx + pDz * fz);
      const lateralOffset = Math.abs(-pDx * fz + pDz * fx);
      if (forwardProj > 0.5 && forwardProj < 14 && lateralOffset < 2.0) {
        leadDistance = Math.min(leadDistance, forwardProj);
        leadSpeed = 0;
      }
    }
  }

  // Check other vehicles ahead
  for (const other of allVehicles) {
    if (other.id === v.id) continue;
    const oDx = other.x - v.x;
    const oDz = other.z - v.z;
    const oDist = Math.hypot(oDx, oDz);
    if (oDist > 24) continue;

    const forwardProj = oDx * fx + oDz * fz;
    const lateralOffset = Math.abs(-oDx * fz + oDz * fx);
    // Forward sector check
    if (forwardProj > 0.8 && forwardProj < 22 && lateralOffset < 2.3) {
      if (forwardProj < leadDistance) {
        leadDistance = forwardProj;
        leadSpeed = Math.abs(other.speed);
        isLeadSlow = other.kind === 'rickshaw' || other.speed < 5.5;
      }
    }
  }

  // Check crossing pedestrians ahead
  if (crossingPedestrians) {
    for (const ped of crossingPedestrians) {
      if (!ped.isCrossing) continue;
      const dx = ped.x - v.x;
      const dz = ped.z - v.z;
      const dist = Math.hypot(dx, dz);
      if (dist < 14) {
        const forwardProj = dx * fx + dz * fz;
        const lateralOffset = Math.abs(-dx * fz + dz * fx);
        if (forwardProj > 0.8 && forwardProj < 11 && lateralOffset < 2.5) {
          pedestrianAhead = true;
          leadDistance = Math.min(leadDistance, forwardProj);
          leadSpeed = 0;
          break;
        }
      }
    }
  }

  const blocked = leadDistance < 8.5 || pedestrianAhead;
  return { blocked, leadDistance, leadSpeed, isLeadSlow, pedestrianAhead };
}

/**
 * Lane-Aware Traffic Steering & Speed controller.
 * In Bangladesh, traffic drives on the LEFT side of the road.
 * - Rickshaws & slow CNGs use the far-left shoulder (laneOffset = -1)
 * - Cars, buses, motorbikes use the main travel lane (laneOffset = 0)
 * - Overtaking vehicles temporarily shift right into passing lane (targetLaneOffset = 1)
 */
export function updateLaneAwareTrafficVehicle(
  v: VehicleState,
  dt: number,
  allVehicles: VehicleState[],
  playerX: number,
  playerZ: number,
  playerVehicleId: string | null,
  timeSec: number,
  colliders: AabbCollider[],
  crossingPedestrians?: Array<{ x: number; z: number; isCrossing: boolean }>,
): void {
  if (v.driver !== 'traffic' && v.driver !== 'police') return;

  const spec = VEHICLE_SPECS[v.kind];
  v.laneOffset = v.laneOffset ?? (v.kind === 'rickshaw' ? -1.0 : 0.0);
  v.targetLaneOffset = v.targetLaneOffset ?? v.laneOffset;

  // Smooth lane offset interpolation (lane changing / overtaking)
  v.laneOffset += (v.targetLaneOffset - v.laneOffset) * Math.min(1, dt * 2.2);

  // 1. Route waypoint following
  const route = TRAFFIC_ROUTES[v.routeIdx % TRAFFIC_ROUTES.length];
  const rawWp = route[v.waypointIdx % route.length];

  // Apply lateral lane offset perpendicular to route corridor heading
  const nextIdx = (v.waypointIdx + 1) % route.length;
  const nextWp = route[nextIdx];
  const legDx = nextWp.x - rawWp.x;
  const legDz = nextWp.z - rawWp.z;
  const legLen = Math.hypot(legDx, legDz) || 1;
  const normX = -legDz / legLen;
  const normZ = legDx / legLen;

  // Left-hand traffic offset: negative shift is to the left shoulder
  const baseOffsetMeters = v.kind === 'rickshaw' ? -2.8 : v.kind === 'bike' ? -1.2 : -1.8;
  const laneShift = baseOffsetMeters + v.laneOffset * 2.4;

  const targetX = rawWp.x + normX * laneShift;
  const targetZ = rawWp.z + normZ * laneShift;

  const dx = targetX - v.x;
  const dz = targetZ - v.z;
  const dist = Math.hypot(dx, dz);
  if (dist < 5.2) {
    v.waypointIdx = (v.waypointIdx + 1) % route.length;
  }

  const desiredHeading = Math.atan2(dx, dz);
  let diff = desiredHeading - v.heading;
  while (diff > Math.PI) diff -= Math.PI * 2;
  while (diff < -Math.PI) diff += Math.PI * 2;
  const steer = THREE.MathUtils.clamp(diff * 1.9, -1, 1);

  // 2. Traffic Light check
  let stoppedAtLight = false;
  for (const inter of TRAFFIC_INTERSECTIONS) {
    if (isVehicleStoppedByTrafficLight(v, inter, timeSec)) {
      stoppedAtLight = true;
      break;
    }
  }
  v.stoppedAtRed = stoppedAtLight;

  // 3. Forward obstacle & headway distance detection
  const detection = detectForwardObstacles(
    v,
    allVehicles,
    playerX,
    playerZ,
    playerVehicleId,
    crossingPedestrians,
  );

  // 4. Bus stop dwelling behavior for City Buses
  if (v.kind === 'bus' && (v.busStopTimer ?? 0) > 0) {
    v.busStopTimer = Math.max(0, (v.busStopTimer ?? 0) - dt);
  } else if (v.kind === 'bus' && isNearBusStop(v) && Math.random() < 0.008 && !v.stoppedAtRed) {
    v.busStopTimer = 6.0; // Dwell at bus stop for 6 seconds
  }
  const isBusBoarding = (v.busStopTimer ?? 0) > 0;

  // 5. Overtaking logic: fast vehicles pass slow rickshaws when lane is clear
  if (spec.canDrift || v.kind === 'sedan' || v.kind === 'bike' || v.kind === 'bus') {
    if (v.isOvertaking) {
      v.overtakeTimer = (v.overtakeTimer ?? 0) + dt;
      // Complete overtake after passing lead or 5.5 seconds
      if (detection.leadDistance > 12.0 || (v.overtakeTimer ?? 0) > 5.5) {
        v.isOvertaking = false;
        v.targetLaneOffset = v.kind === 'bike' ? -0.5 : 0.0;
        v.overtakeTimer = 0;
      }
    } else if (detection.isLeadSlow && detection.leadDistance < 10.0 && !stoppedAtLight && !isBusBoarding) {
      // Check if adjacent passing lane is clear
      v.isOvertaking = true;
      v.targetLaneOffset = 1.1; // Shift right into passing lane
      v.overtakeTimer = 0;
    }
  }

  // 6. Motorcycle lane filtering through stopped congestion
  if (v.kind === 'bike' && detection.leadDistance < 7.0 && detection.leadSpeed < 2.0 && !stoppedAtLight) {
    // Narrow bike filters between lanes
    v.targetLaneOffset = 0.55;
  }

  // 7. Congestion queueing & Speed Target Calculation
  let targetSpeed = 0;
  let handbrake = false;

  if (stoppedAtLight) {
    targetSpeed = 0;
    handbrake = v.speed < 1.0;
  } else if (isBusBoarding) {
    targetSpeed = 0;
    handbrake = true;
  } else if (detection.pedestrianAhead) {
    // Yield to crossing pedestrian
    targetSpeed = 0;
    handbrake = v.speed < 2.5;
  } else if (detection.leadDistance < 4.2) {
    // Bumper-to-bumper congestion queueing
    targetSpeed = 0;
    handbrake = v.speed < 1.2;
    v.waitTimer = (v.waitTimer ?? 0) + dt;
    // Occasional Dhaka congestion honk when stalled for > 3.5s
    if ((v.waitTimer ?? 0) > 3.5 && (v.honkTimer ?? 0) <= 0 && Math.random() < 0.015) {
      v.honkTimer = 4.5;
    }
  } else if (detection.leadDistance < 9.5) {
    // Headway speed matching
    targetSpeed = Math.min(detection.leadSpeed * 0.9, spec.maxSpeed * 0.35);
    v.waitTimer = 0;
  } else {
    // Free road cruising
    const cruiseRatio = v.kind === 'rickshaw' ? 0.42 : v.kind === 'cng' ? 0.65 : v.isOvertaking ? 0.88 : 0.58;
    targetSpeed = spec.maxSpeed * cruiseRatio;
    v.waitTimer = 0;
  }

  if (v.honkTimer && v.honkTimer > 0) v.honkTimer -= dt;

  // 8. Longitudinal throttle & brake actuation
  let throttle = 0;
  if (v.speed < targetSpeed - 0.5) {
    throttle = 0.65;
  } else if (v.speed > targetSpeed + 0.8) {
    throttle = -0.85;
  }

  stepVehicleKinematics(v, { throttle, steer, handbrake }, dt, colliders);
}

/**
 * Spawns an authentic Dhaka vehicle with appropriate route, heading, and initial velocity.
 */
export function spawnTrafficVehicle(
  id: string,
  kind: VehicleKind,
  routeIdx: number,
  waypointIdx: number,
  driver: 'traffic' | 'none' = 'traffic',
): VehicleState {
  const spec = VEHICLE_SPECS[kind];
  const route = TRAFFIC_ROUTES[routeIdx % TRAFFIC_ROUTES.length];
  const wp = route[waypointIdx % route.length];
  const nextWp = route[(waypointIdx + 1) % route.length];
  const heading = Math.atan2(nextWp.x - wp.x, nextWp.z - wp.z);

  const colors: Record<VehicleKind, number[]> = {
    cng: [0x0f6a44, 0x0c5e3c, 0x147d52],
    rickshaw: [0xc62828, 0xd97706, 0x15803d, 0x1d4ed8],
    sedan: [0xe2e8f0, 0x1e293b, 0x0f766e, 0x1d4ed8, 0x991b1b],
    bike: [0xdc2626, 0x1e293b, 0x2563eb, 0x059669],
    bus: [0xb81d24, 0x1d4ed8, 0x15803d],
    truck: [0xd97706, 0x0f766e, 0x7c2d12],
    police: [0xf1f5f9],
    launch: [0x0f5132, 0x1e3a8a, 0x831843],
    speedboat: [0xd97706, 0xdc2626, 0x0284c7, 0xf1f5f9],
    jetski: [0x2563eb, 0x16a34a, 0xe11d48, 0xca8a04],
  };

  const palette = colors[kind];
  const color = palette[Math.floor(Math.random() * palette.length)];

  return {
    id,
    kind,
    x: wp.x,
    y: 0,
    z: wp.z,
    heading,
    speed: driver === 'traffic' ? spec.maxSpeed * 0.45 : 0,
    steerAngle: 0,
    hp: spec.maxHp,
    maxHp: spec.maxHp,
    color,
    driver,
    routeIdx,
    waypointIdx,
    honkTimer: 0,
    sirenOn: false,
    damagedSmoke: false,
    laneOffset: kind === 'rickshaw' ? -1.0 : 0.0,
    targetLaneOffset: kind === 'rickshaw' ? -1.0 : 0.0,
    fuel: 100,
    maxFuel: 100,
    fuelType: kind === 'cng' ? 'cng' : kind === 'truck' || kind === 'bus' || kind === 'launch' ? 'diesel' : 'petrol',
  };
}
