import test from 'node:test';
import assert from 'node:assert/strict';
import {
  addWantedHeat,
  applyGarageService,
  applyPlayerDamage,
  buyShopItem,
  createInitialPlayerState,
  equipOrBuyOutfit,
  formatDhakaTime,
  resolveCircleCollisions,
  stepVehicleKinematics,
  tickWantedCooldown,
} from './physics';
import {
  BUILDINGS,
  buildStaticColliders,
  FARE_DESTINATIONS,
  getDistrictAt,
  INITIAL_VEHICLES,
  POIS,
  RIVER_Z_START,
  ROAD_SEGMENTS,
  STORY_MISSIONS,
  TRAFFIC_ROUTES,
  WORLD_HALF_SIZE,
} from './worldData';
import { VehicleState, VEHICLE_SPECS, isWatercraftKind } from './types';
import { BURIGANGA_MAX_Z } from './waterSystem';

test('expanded city bounds are ~2.5x original dimensions and contain dense generated blocks', () => {
  assert.ok(WORLD_HALF_SIZE >= 250, `Expected expanded WORLD_HALF_SIZE >= 250, got ${WORLD_HALF_SIZE}`);
  assert.ok(RIVER_Z_START >= 190, `Expected expanded RIVER_Z_START >= 190, got ${RIVER_Z_START}`);
  assert.ok(BUILDINGS.length >= 90, `Expected substantial city density (>= 90 buildings), got ${BUILDINGS.length}`);
});

test('no building overlaps any road segment across the entire expanded Dhaka road network', () => {
  for (const b of BUILDINGS) {
    const bMinX = b.x - b.w / 2;
    const bMaxX = b.x + b.w / 2;
    const bMinZ = b.z - b.d / 2;
    const bMaxZ = b.z + b.d / 2;

    for (const seg of ROAD_SEGMENTS) {
      const rMinX = Math.min(seg.x1, seg.x2) - seg.width / 2;
      const rMaxX = Math.max(seg.x1, seg.x2) + seg.width / 2;
      const rMinZ = Math.min(seg.z1, seg.z2) - seg.width / 2;
      const rMaxZ = Math.max(seg.z1, seg.z2) + seg.width / 2;
      const overlaps = bMaxX > rMinX && bMinX < rMaxX && bMaxZ > rMinZ && bMinZ < rMaxZ;
      assert.equal(
        overlaps,
        false,
        `Building ${b.id} (${b.x},${b.z} ${b.w}x${b.d}) overlaps road ${seg.id}`,
      );
    }
  }
});

test('all POIs, mission targets, fare destinations, vehicle spawns, and traffic routes stay inside playable bounds', () => {
  const colliders = buildStaticColliders();
  const checkInBounds = (label: string, x: number, z: number) => {
    assert.ok(Math.abs(x) < WORLD_HALF_SIZE - 4, `${label} X=${x} out of bounds`);
    assert.ok(z > -WORLD_HALF_SIZE + 4 && z < RIVER_Z_START - 2, `${label} Z=${z} out of bounds`);
  };

  for (const poi of POIS) {
    checkInBounds(`POI ${poi.id}`, poi.x, poi.z);
  }
  for (const m of STORY_MISSIONS) {
    for (const step of m.steps) {
      checkInBounds(`Mission ${m.id}/${step.id}`, step.targetX, step.targetZ);
    }
  }
  for (const f of FARE_DESTINATIONS) {
    checkInBounds(`Fare ${f.label}`, f.x, f.z);
    const res = resolveCircleCollisions(f.x, f.z, 1.0, colliders);
    assert.equal(res.collided, false, `Fare destination ${f.label} collides with static object`);
  }
  for (const v of INITIAL_VEHICLES) {
    if (isWatercraftKind(v.kind)) {
      assert.ok(Math.abs(v.x) < WORLD_HALF_SIZE - 4, `Watercraft ${v.id} X=${v.x} out of bounds`);
      assert.ok(v.z >= RIVER_Z_START && v.z <= BURIGANGA_MAX_Z, `Watercraft ${v.id} Z=${v.z} out of river bounds`);
      const res = resolveCircleCollisions(v.x, v.z, 0.8, colliders, true);
      assert.equal(res.collided, false, `Watercraft spawn ${v.id} collides with static object`);
    } else {
      checkInBounds(`Vehicle ${v.id}`, v.x, v.z);
      const res = resolveCircleCollisions(v.x, v.z, 0.8, colliders);
      assert.equal(res.collided, false, `Vehicle spawn ${v.id} collides with static object`);
    }
  }
  TRAFFIC_ROUTES.forEach((route, ri) => {
    route.forEach((wp, wi) => {
      checkInBounds(`Route ${ri} wp ${wi}`, wp.x, wp.z);
    });
  });
});

test('player spawn position is completely unobstructed by any static building collider', () => {
  const player = createInitialPlayerState();
  const colliders = buildStaticColliders();
  const res = resolveCircleCollisions(player.x, player.z, 0.6, colliders);
  assert.equal(res.collided, false, 'Player spawn must not collide with any building or pillar');
  assert.equal(res.x, player.x);
  assert.equal(res.z, player.z);
});

test('circle-vs-AABB collision resolves penetration and keeps entities north of Buriganga River', () => {
  const colliders = buildStaticColliders();
  // Push into Shapla Monument pedestal ([-4.2..4.2])
  const hitMonument = resolveCircleCollisions(3.5, 0, 1.0, colliders);
  assert.equal(hitMonument.collided, true);
  assert.ok(hitMonument.x >= 5.19, `Expected x >= 5.2 outside monument, got ${hitMonument.x}`);

  // Push south into Buriganga River
  const hitRiver = resolveCircleCollisions(0, RIVER_Z_START + 5, 1.0, colliders);
  assert.equal(hitRiver.collided, true);
  assert.ok(hitRiver.z < RIVER_Z_START, 'Entity must stay on riverfront pier north of water');
});

test('vehicle kinematics accelerate, steer, brake, and take collision damage at high speed', () => {
  const colliders = buildStaticColliders();
  const cng: VehicleState = {
    id: 'test_cng',
    kind: 'cng',
    x: 0,
    y: 0,
    z: 36,
    heading: Math.PI, // Facing North (-Z)
    speed: 0,
    steerAngle: 0,
    hp: VEHICLE_SPECS.cng.maxHp,
    maxHp: VEHICLE_SPECS.cng.maxHp,
    color: VEHICLE_SPECS.cng.defaultColor,
    driver: 'player',
    routeIdx: 0,
    waypointIdx: 0,
    honkTimer: 0,
    sirenOn: false,
    damagedSmoke: false,
  };

  for (let i = 0; i < 20; i++) {
    stepVehicleKinematics(cng, { throttle: 1, steer: 0, handbrake: false }, 0.05, colliders);
  }
  assert.ok(cng.speed > 10, `Expected CNG speed > 10 m/s, got ${cng.speed}`);
  assert.ok(cng.z < 30, `Expected CNG to move north (z < 30), got ${cng.z}`);

  const headingBefore = cng.heading;
  stepVehicleKinematics(cng, { throttle: 1, steer: 1, handbrake: false }, 0.05, colliders);
  assert.notEqual(cng.heading, headingBefore);

  cng.x = 0;
  cng.z = 5.5;
  cng.heading = Math.PI;
  cng.speed = 18;
  const hpBefore = cng.hp;
  const crash = stepVehicleKinematics(cng, { throttle: 1, steer: 0, handbrake: false }, 0.05, colliders);
  assert.equal(crash.collided, true);
  assert.ok(cng.hp < hpBefore, 'High-speed wall crash should reduce vehicle HP');
});

test('combat damage absorbs via armor first and allows player defeat (hp reaches 0)', () => {
  const player = createInitialPlayerState();
  player.hp = 100;
  player.armor = 50;

  applyPlayerDamage(player, 40);
  assert.ok(player.armor < 50, 'Armor should absorb part of incoming damage');
  assert.ok(player.hp < 100 && player.hp > 60, 'HP should drop by reduced post-armor amount');

  applyPlayerDamage(player, 250);
  assert.equal(player.hp, 0);
  assert.equal(player.armor, 0);
});

test('wanted level escalates up to 5 stars and cools down when evading police', () => {
  const player = createInitialPlayerState();
  addWantedHeat(player, 25);
  assert.equal(player.wantedLevel, 2);

  addWantedHeat(player, 150);
  assert.equal(player.wantedLevel, 5);

  player.wantedCooldown = 1.0;
  tickWantedCooldown(player, false, 1.5);
  assert.equal(player.wantedLevel, 0);
  assert.equal(player.wantedHeat, 0);
});

test('shop, garage, and wardrobe economy validate Taka balance and mutate state', () => {
  const player = createInitialPlayerState();
  player.money = 1000;
  player.hp = 40;

  const heal = buyShopItem(player, 'biriyani');
  assert.equal(heal.ok, true);
  assert.equal(player.hp, player.maxHp);
  assert.equal(player.money, 850);

  const tooExpensive = buyShopItem(player, 'shotgun');
  assert.equal(tooExpensive.ok, false);
  assert.equal(player.unlockedWeapons.includes('shotgun'), false);

  const outfit = equipOrBuyOutfit(player, 'panjabi');
  assert.equal(outfit.ok, true);
  assert.equal(player.outfit, 'panjabi');

  addWantedHeat(player, 40);
  player.money = 500;
  const clear = applyGarageService(player, null, 'clear_heat');
  assert.equal(clear.ok, true);
  assert.equal(player.wantedLevel, 0);
  assert.equal(player.money, 200);
});

test('all 9 Dhaka districts are detected accurately and time formatting works', () => {
  assert.equal(getDistrictAt(0, 0), 'shapla_crossing');
  assert.equal(getDistrictAt(35, -35), 'gulshan');
  assert.equal(getDistrictAt(-35, 35), 'old_dhaka');
  assert.equal(getDistrictAt(-40, -35), 'kawran_flyover');
  assert.equal(getDistrictAt(0, 184), 'sadarghat');
  assert.equal(getDistrictAt(140, 40), 'motijheel');
  assert.equal(getDistrictAt(-180, -20), 'dhanmondi');
  assert.equal(getDistrictAt(40, -190), 'uttara_north');
  assert.equal(getDistrictAt(-120, -190), 'mirpur_market');
  assert.equal(formatDhakaTime(16.5), '4:30 PM');
  assert.equal(formatDhakaTime(0.25), '12:15 AM');
});

test('luxury sedan accelerates, reverses, simulates wheel spin, and drifts with handbrake', () => {
  const colliders = buildStaticColliders();
  const sedan: VehicleState = {
    id: 'test_sedan',
    kind: 'sedan',
    x: 0,
    y: 0,
    z: 50,
    heading: 0, // Facing South (+Z)
    speed: 0,
    steerAngle: 0,
    hp: VEHICLE_SPECS.sedan.maxHp,
    maxHp: VEHICLE_SPECS.sedan.maxHp,
    color: VEHICLE_SPECS.sedan.defaultColor,
    driver: 'player',
    routeIdx: 0,
    waypointIdx: 0,
    honkTimer: 0,
    sirenOn: false,
    damagedSmoke: false,
  };

  // Acceleration test
  for (let i = 0; i < 25; i++) {
    stepVehicleKinematics(sedan, { throttle: 1, steer: 0, handbrake: false }, 0.04, colliders);
  }
  assert.ok(sedan.speed > 12, `Sedan speed should exceed 12 m/s, got ${sedan.speed}`);
  assert.ok(sedan.wheelRotation !== undefined && sedan.wheelRotation > 5, 'Wheel rotation should accumulate');
  assert.ok(sedan.pitch !== undefined, 'Suspension pitch should be calculated');

  // Foot braking to stop
  for (let i = 0; i < 20; i++) {
    stepVehicleKinematics(sedan, { throttle: -1, steer: 0, handbrake: false }, 0.05, colliders);
  }
  assert.ok(sedan.speed <= 0.5, `Sedan should brake to standstill, got ${sedan.speed}`);

  // Reverse gear test from standstill
  for (let i = 0; i < 15; i++) {
    stepVehicleKinematics(sedan, { throttle: -1, steer: 0, handbrake: false }, 0.04, colliders);
  }
  assert.ok(sedan.speed < -2, `Sedan should reverse backwards, got ${sedan.speed}`);
  assert.equal(sedan.isReversing, true, 'isReversing flag must be true during reverse gear');

  // Reset and test handbrake drift breakaway
  sedan.x = 0;
  sedan.z = 50;
  sedan.speed = 18;
  sedan.heading = 0;
  // Initiate sharp turn with handbrake engaged
  for (let i = 0; i < 6; i++) {
    stepVehicleKinematics(sedan, { throttle: 0.8, steer: 1, handbrake: true }, 0.04, colliders);
  }
  assert.equal(sedan.isDrifting, true, 'Sedan should enter drift state during handbrake turn');
  assert.ok(sedan.lateralSlip !== undefined && Math.abs(sedan.lateralSlip) > 0.5, 'Lateral slip should be positive');
});

test('motorcycle banks into turns while bus experiences outward centrifugal roll', () => {
  const colliders = buildStaticColliders();
  const bike: VehicleState = {
    id: 'test_bike',
    kind: 'bike',
    x: 0,
    y: 0,
    z: 50,
    heading: 0,
    speed: 15,
    steerAngle: 0,
    hp: VEHICLE_SPECS.bike.maxHp,
    maxHp: VEHICLE_SPECS.bike.maxHp,
    color: VEHICLE_SPECS.bike.defaultColor,
    driver: 'player',
    routeIdx: 0,
    waypointIdx: 0,
    honkTimer: 0,
    sirenOn: false,
    damagedSmoke: false,
  };

  const bus: VehicleState = {
    id: 'test_bus',
    kind: 'bus',
    x: 20,
    y: 0,
    z: 50,
    heading: 0,
    speed: 12,
    steerAngle: 0,
    hp: VEHICLE_SPECS.bus.maxHp,
    maxHp: VEHICLE_SPECS.bus.maxHp,
    color: VEHICLE_SPECS.bus.defaultColor,
    driver: 'player',
    routeIdx: 0,
    waypointIdx: 0,
    honkTimer: 0,
    sirenOn: false,
    damagedSmoke: false,
  };

  // Turn right (steer = 1)
  for (let i = 0; i < 8; i++) {
    stepVehicleKinematics(bike, { throttle: 1, steer: 1, handbrake: false }, 0.04, colliders);
    stepVehicleKinematics(bus, { throttle: 1, steer: 1, handbrake: false }, 0.04, colliders);
  }

  // 2-wheeler bike banks into turn (positive roll angle)
  assert.ok(bike.roll !== undefined && bike.roll > 0.05, `Bike should bank into turn, got roll ${bike.roll}`);
  // 4-wheeler bus rolls outward from centrifugal force (negative roll angle)
  assert.ok(bus.roll !== undefined && bus.roll < -0.01, `Bus should lean outward, got roll ${bus.roll}`);
});

test('every vehicle kind in VEHICLE_SPECS has authentic Dhaka handling parameters', () => {
  const kinds = ['sedan', 'bike', 'cng', 'bus', 'truck', 'rickshaw', 'police'] as const;
  for (const k of kinds) {
    const spec = VEHICLE_SPECS[k];
    assert.ok(spec, `Vehicle spec for ${k} must exist`);
    assert.ok(spec.maxSpeed > 0, `${k} maxSpeed must be positive`);
    assert.ok(spec.accel > 0, `${k} accel must be positive`);
    assert.ok(spec.brake > 0, `${k} brake must be positive`);
    assert.ok(spec.turnRate > 0, `${k} turnRate must be positive`);
    assert.ok(spec.mass > 0, `${k} mass must be positive`);
    assert.ok(spec.banglaName.length > 0, `${k} must have authentic Bangla name`);
  }
  // Sedan has high speed and drift capability
  assert.equal(VEHICLE_SPECS.sedan.canDrift, true);
  assert.ok(VEHICLE_SPECS.sedan.maxSpeed >= 28);
  // Bus has highest mass
  assert.ok(VEHICLE_SPECS.bus.mass >= VEHICLE_SPECS.sedan.mass * 2);
  // Rickshaw is low speed authentic Dhaka pedaled rickshaw
  assert.ok(VEHICLE_SPECS.rickshaw.maxSpeed <= 15);
});
