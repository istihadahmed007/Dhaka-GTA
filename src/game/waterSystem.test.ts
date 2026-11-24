import test from 'node:test';
import assert from 'node:assert/strict';
import {
  BURIGANGA_WATER_LEVEL,
  BURIGANGA_MIN_X,
  BURIGANGA_MAX_X,
  BURIGANGA_MIN_Z,
  BURIGANGA_MAX_Z,
  RIVER_DOCKS,
  getRiverWaveSample,
  isPointInBurigangaWater,
  stepWatercraftKinematics,
  getNearestRiverDock,
  dockWatercraftAtBerth,
} from './waterSystem';
import { VehicleState, VEHICLE_SPECS, isWatercraftKind } from './types';
import { buildStaticColliders } from './worldData';

test('waterSystem: Buriganga river geographic boundaries and water level are well-defined', () => {
  assert.equal(BURIGANGA_WATER_LEVEL, -1.05);
  assert.ok(BURIGANGA_MIN_X < -500);
  assert.ok(BURIGANGA_MAX_X > 500);
  assert.ok(BURIGANGA_MIN_Z > 195 && BURIGANGA_MIN_Z < 205);
  assert.ok(BURIGANGA_MAX_Z > 380 && BURIGANGA_MAX_Z < 410);
});

test('waterSystem: all 6 river docks have authentic Sadarghat and Keraniganj configurations', () => {
  assert.equal(RIVER_DOCKS.length, 6);
  for (const dock of RIVER_DOCKS) {
    assert.ok(dock.id.length > 0);
    assert.ok(dock.name.length > 0);
    assert.ok(dock.banglaName.length > 0);
    assert.ok(dock.x >= BURIGANGA_MIN_X && dock.x <= BURIGANGA_MAX_X);
    assert.ok(dock.z >= BURIGANGA_MIN_Z - 1.5 && dock.z <= BURIGANGA_MAX_Z + 1.5);
    assert.ok(dock.allowedKinds.length > 0);
    for (const k of dock.allowedKinds) {
      assert.ok(isWatercraftKind(k), `Allowed kind ${k} must be a watercraft kind`);
    }
  }

  const sadarghatDocks = RIVER_DOCKS.filter((d) => d.district === 'sadarghat');
  const keraniganjDocks = RIVER_DOCKS.filter((d) => d.district === 'keraniganj');
  assert.equal(sadarghatDocks.length, 4, 'Expected 4 Sadarghat berths (Ghat 1–4)');
  assert.equal(keraniganjDocks.length, 2, 'Expected 2 Keraniganj berths (Ferry Pier & Shipyard)');
});

test('waterSystem: getRiverWaveSample generates continuous surface waves and normalized normals', () => {
  const sample1 = getRiverWaveSample(0, 250, 0);
  assert.ok(
    sample1.height >= BURIGANGA_WATER_LEVEL - 0.25 && sample1.height <= BURIGANGA_WATER_LEVEL + 0.25,
    `Wave height ${sample1.height} outside expected range`,
  );
  assert.ok(Math.abs(sample1.normalX) < 0.15, 'Wave normalX slope should be moderate');
  assert.ok(Math.abs(sample1.normalZ) < 0.15, 'Wave normalZ slope should be moderate');

  // Multi-time continuity check
  const sample2 = getRiverWaveSample(0, 250, 1.5);
  assert.ok(Math.abs(sample2.height - sample1.height) < 0.35);

  // Cross-location variation
  const sampleFar = getRiverWaveSample(150, 320, 0);
  assert.notEqual(sample1.height, sampleFar.height);
});

test('waterSystem: isPointInBurigangaWater discriminates open water from dry land and piers', () => {
  // Open Buriganga river channel
  assert.equal(isPointInBurigangaWater(0, 260), true, 'Central river channel should be water');
  assert.equal(isPointInBurigangaWater(-150, 300), true, 'West river channel should be water');

  // Sadarghat city street / launch terminal (dry land)
  assert.equal(isPointInBurigangaWater(0, 180), false, 'Sadarghat street should not be water');
  assert.equal(isPointInBurigangaWater(50, 150), false, 'Old Dhaka street should not be water');

  // Sadarghat concrete jetties (walkway above water)
  assert.equal(isPointInBurigangaWater(-72, 205), false, 'Sadarghat Ghat 1 walkway should be dry');
  assert.equal(isPointInBurigangaWater(-26, 206), false, 'Sadarghat Ghat 2 walkway should be dry');
  assert.equal(isPointInBurigangaWater(26, 206), false, 'Sadarghat Ghat 3 walkway should be dry');
  assert.equal(isPointInBurigangaWater(78, 205), false, 'Sadarghat Ghat 4 walkway should be dry');

  // Keraniganj piers
  assert.equal(isPointInBurigangaWater(-26, 390), false, 'Keraniganj ferry pier should be dry');

  // Beyond Keraniganj southern shoreline
  assert.equal(isPointInBurigangaWater(0, 420), false, 'Beyond southern shore should not be water');
});

const defaultVehicleProps = {
  driver: 'player' as const,
  routeIdx: 0,
  waypointIdx: 0,
  honkTimer: 0,
  sirenOn: false,
  damagedSmoke: false,
};

test('waterSystem: stepWatercraftKinematics simulates launch displacement buoyancy and steering', () => {
  const colliders = buildStaticColliders();
  const launch: VehicleState = {
    id: 'test_launch',
    kind: 'launch',
    x: 0,
    y: BURIGANGA_WATER_LEVEL,
    z: 250,
    heading: 0,
    speed: 0,
    steerAngle: 0,
    hp: VEHICLE_SPECS.launch.maxHp,
    maxHp: VEHICLE_SPECS.launch.maxHp,
    color: VEHICLE_SPECS.launch.defaultColor,
    isWatercraft: true,
    ...defaultVehicleProps,
  };

  // Accelerate launch forward
  for (let i = 0; i < 40; i++) {
    stepWatercraftKinematics(launch, { throttle: 1, steer: 0, handbrake: false }, 0.05, colliders);
  }
  assert.ok(launch.speed > 3.0, `Launch should accelerate forward, got ${launch.speed} m/s`);
  assert.ok(launch.speed <= VEHICLE_SPECS.launch.maxSpeed, 'Speed must not exceed max speed');
  assert.ok(launch.y >= BURIGANGA_WATER_LEVEL - 0.2 && launch.y <= BURIGANGA_WATER_LEVEL + 0.65);

  // Rudder steering under forward propulsion
  const prevHeading = launch.heading;
  for (let i = 0; i < 30; i++) {
    stepWatercraftKinematics(launch, { throttle: 1, steer: 1, handbrake: false }, 0.05, colliders);
  }
  assert.notEqual(launch.heading, prevHeading, 'Launch should steer when moving with rudder deflected');
});

test('waterSystem: speedboat accelerates past planing threshold and banks inwards during turns', () => {
  const colliders = buildStaticColliders();
  const boat: VehicleState = {
    id: 'test_speedboat',
    kind: 'speedboat',
    x: 50,
    y: BURIGANGA_WATER_LEVEL,
    z: 270,
    heading: 0,
    speed: 0,
    steerAngle: 0,
    hp: VEHICLE_SPECS.speedboat.maxHp,
    maxHp: VEHICLE_SPECS.speedboat.maxHp,
    color: VEHICLE_SPECS.speedboat.defaultColor,
    isWatercraft: true,
    ...defaultVehicleProps,
  };

  // Accelerate to high speed
  for (let i = 0; i < 60; i++) {
    stepWatercraftKinematics(boat, { throttle: 1, steer: 0, handbrake: false }, 0.05, colliders);
  }
  assert.ok(boat.speed > 8.5, `Speedboat should achieve planing speed (> 8 m/s), got ${boat.speed}`);
  assert.equal(boat.isPlaning, true, 'Speedboat should be in planing regime');
  assert.ok((boat.pitch ?? 0) > 0.02, 'Bow should rise during planing (positive pitch)');

  // Inward banking during hard turn
  for (let i = 0; i < 15; i++) {
    stepWatercraftKinematics(boat, { throttle: 1, steer: 1, handbrake: false }, 0.05, colliders);
  }
  assert.ok((boat.roll ?? 0) > 0.05, 'Speedboat should bank inward during turn');
});

test('waterSystem: jet ski demonstrates high agility and waterjet braking', () => {
  const colliders = buildStaticColliders();
  const ski: VehicleState = {
    id: 'test_jetski',
    kind: 'jetski',
    x: -40,
    y: BURIGANGA_WATER_LEVEL,
    z: 260,
    heading: 0,
    speed: 15,
    steerAngle: 0,
    hp: VEHICLE_SPECS.jetski.maxHp,
    maxHp: VEHICLE_SPECS.jetski.maxHp,
    color: VEHICLE_SPECS.jetski.defaultColor,
    isWatercraft: true,
    ...defaultVehicleProps,
  };

  // Reverse waterjet brake
  stepWatercraftKinematics(ski, { throttle: -1, steer: 0, handbrake: true }, 0.1, colliders);
  assert.ok(ski.speed < 15, 'Waterjet reverse brake should rapidly decelerate jet ski');
});

test('waterSystem: boundary clamping keeps watercraft inside Buriganga river channel', () => {
  const colliders = buildStaticColliders();
  const ski: VehicleState = {
    id: 'test_border_ski',
    kind: 'jetski',
    x: 0,
    y: BURIGANGA_WATER_LEVEL,
    z: BURIGANGA_MIN_Z - 5, // Attempting to cross embankment north
    heading: Math.PI, // Facing north
    speed: 10,
    steerAngle: 0,
    hp: 100,
    maxHp: 100,
    color: 0xffffff,
    isWatercraft: true,
    ...defaultVehicleProps,
  };

  stepWatercraftKinematics(ski, { throttle: 1, steer: 0, handbrake: false }, 0.05, colliders);
  assert.ok(ski.z >= BURIGANGA_MIN_Z, `Craft must not breach northern river bank: z=${ski.z}`);
});

test('waterSystem: getNearestRiverDock and dockWatercraftAtBerth cleanly moor craft', () => {
  const dock = getNearestRiverDock(-26, 206, 12);
  assert.ok(dock !== null);
  assert.equal(dock?.id, 'dock_sadarghat_central_launch');

  const launch: VehicleState = {
    id: 'test_dock_launch',
    kind: 'launch',
    x: -28,
    y: BURIGANGA_WATER_LEVEL,
    z: 207,
    heading: 1.2,
    speed: 3.5,
    steerAngle: 0.2,
    hp: 1000,
    maxHp: 1000,
    color: 0x1e3a5f,
    isWatercraft: true,
    ...defaultVehicleProps,
  };

  dockWatercraftAtBerth(launch, dock!);
  assert.equal(launch.speed, 0, 'Docked craft must have speed 0');
  assert.equal(launch.dockedAt, dock!.id);
  assert.equal(launch.heading, dock!.heading);
  assert.equal(launch.x, dock!.x);
  assert.equal(launch.z, dock!.z);
});
