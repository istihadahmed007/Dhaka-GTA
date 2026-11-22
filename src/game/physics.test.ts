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
import { buildStaticColliders, getDistrictAt, RIVER_Z_START } from './worldData';
import { VehicleState, VEHICLE_SPECS } from './types';

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

  // Accelerate north for 1 second
  for (let i = 0; i < 20; i++) {
    stepVehicleKinematics(cng, { throttle: 1, steer: 0, handbrake: false }, 0.05, colliders);
  }
  assert.ok(cng.speed > 10, `Expected CNG speed > 10 m/s, got ${cng.speed}`);
  assert.ok(cng.z < 30, `Expected CNG to move north (z < 30), got ${cng.z}`);

  // Steer left while moving
  const headingBefore = cng.heading;
  stepVehicleKinematics(cng, { throttle: 1, steer: 1, handbrake: false }, 0.05, colliders);
  assert.notEqual(cng.heading, headingBefore);

  // Crash into monument at high speed
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

  // Heavy lethal damage must drop HP to exactly 0 (never clamped to 1)
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

  // When police are far away, cooldown expires and clears stars
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

  // Try buying expensive shotgun without enough funds
  const tooExpensive = buyShopItem(player, 'shotgun');
  assert.equal(tooExpensive.ok, false);
  assert.equal(player.unlockedWeapons.includes('shotgun'), false);

  // Buy outfit
  const outfit = equipOrBuyOutfit(player, 'panjabi');
  assert.equal(outfit.ok, true);
  assert.equal(player.outfit, 'panjabi');

  // Garage clear wanted heat
  addWantedHeat(player, 40);
  player.money = 500;
  const clear = applyGarageService(player, null, 'clear_heat');
  assert.equal(clear.ok, true);
  assert.equal(player.wantedLevel, 0);
  assert.equal(player.money, 200);
});

test('district lookup and time formatting work accurately', () => {
  assert.equal(getDistrictAt(0, 0), 'shapla_crossing');
  assert.equal(getDistrictAt(35, -35), 'gulshan');
  assert.equal(getDistrictAt(-35, 35), 'old_dhaka');
  assert.equal(getDistrictAt(0, 72), 'sadarghat');
  assert.equal(formatDhakaTime(16.5), '4:30 PM');
  assert.equal(formatDhakaTime(0.25), '12:15 AM');
});
