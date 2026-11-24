import test from 'node:test';
import assert from 'node:assert/strict';
import {
  applyVehicleCustomization,
  createInitialPlayerState,
  purchaseSafehouse,
  refuelVehicle,
  restAtSafehouse,
  stepVehicleKinematics,
  useInventoryItem,
} from './physics';
import { VehicleState } from './types';

function createMockVehicle(overrides: Partial<VehicleState> = {}): VehicleState {
  return {
    id: 'test_sedan',
    kind: 'sedan',
    x: 0,
    y: 0,
    z: 0,
    heading: 0,
    speed: 0,
    steerAngle: 0,
    hp: 165,
    maxHp: 165,
    driver: 'player',
    color: 0xd9e1e8,
    damagedSmoke: false,
    routeIdx: 0,
    waypointIdx: 0,
    honkTimer: 0,
    sirenOn: false,
    fuel: 100,
    maxFuel: 100,
    fuelType: 'petrol',
    isStalled: false,
    ...overrides,
  };
}

test('openWorld: fuel burns during driving and engine stalls when fuel hits 0', () => {
  const veh = createMockVehicle({ fuel: 0.05, speed: 10 });
  const input = { throttle: 1, steer: 0, handbrake: false };

  // Step kinematics with driving throttle
  for (let i = 0; i < 20; i++) {
    stepVehicleKinematics(veh, input, 0.1, []);
  }

  assert.equal(veh.fuel, 0, 'Fuel should hit zero');
  assert.equal(veh.isStalled, true, 'Vehicle should stall when fuel is depleted');

  // Stalled vehicle cannot accelerate
  const speedBefore = veh.speed;
  stepVehicleKinematics(veh, input, 0.1, []);
  assert.ok(veh.speed <= speedBefore, 'Stalled vehicle should not gain speed from throttle');
});

test('openWorld: refueling restores tank and clears stalled engine state', () => {
  const player = createInitialPlayerState();
  player.money = 1000;
  const veh = createMockVehicle({ fuel: 0, isStalled: true });

  const res = refuelVehicle(player, veh);
  assert.equal(res.ok, true);
  assert.equal(veh.fuel, 100);
  assert.equal(veh.isStalled, false);
  assert.ok(player.money < 1000, 'Money should be deducted for fuel');
});

test('openWorld: vehicle performance tuning upgrades boost acceleration and braking', () => {
  const player = createInitialPlayerState();
  player.money = 10000;
  const stockVeh = createMockVehicle();
  const tunedVeh = createMockVehicle();

  // Upgrade tuned vehicle
  const engineRes = applyVehicleCustomization(player, tunedVeh, 'engine');
  assert.equal(engineRes.ok, true);
  assert.equal(tunedVeh.customization?.engineStage, 1);

  const brakeRes = applyVehicleCustomization(player, tunedVeh, 'brakes');
  assert.equal(brakeRes.ok, true);
  assert.equal(tunedVeh.customization?.brakesStage, 1);

  const turboRes = applyVehicleCustomization(player, tunedVeh, 'turbo');
  assert.equal(turboRes.ok, true);
  assert.equal(tunedVeh.customization?.hasTurbo, true);

  const spoilerRes = applyVehicleCustomization(player, tunedVeh, 'spoiler');
  assert.equal(spoilerRes.ok, true);
  assert.equal(tunedVeh.customization?.hasSpoiler, true);

  // Accelerate both for 0.5s
  stepVehicleKinematics(stockVeh, { throttle: 1, steer: 0, handbrake: false }, 0.5, []);
  stepVehicleKinematics(tunedVeh, { throttle: 1, steer: 0, handbrake: false }, 0.5, []);

  assert.ok(
    tunedVeh.speed > stockVeh.speed,
    `Tuned vehicle speed (${tunedVeh.speed}) must exceed stock vehicle speed (${stockVeh.speed})`,
  );
});

test('openWorld: safehouse deeds can be purchased and resting restores full vitals', () => {
  const player = createInitialPlayerState();
  player.money = 100000;
  player.hp = 25;
  player.stamina = 15;
  player.wantedLevel = 1;
  player.wantedHeat = 20;

  // Purchase Gulshan Penthouse
  const buyRes = purchaseSafehouse(player, 'gulshan_penthouse');
  assert.equal(buyRes.ok, true);
  assert.ok(player.unlockedSafehouses.includes('gulshan_penthouse'));

  // Rest at safehouse
  const restRes = restAtSafehouse(player, 6);
  assert.equal(restRes.ok, true);
  assert.equal(player.hp, player.maxHp, 'HP must be fully restored');
  assert.equal(player.stamina, player.maxStamina, 'Stamina must be fully restored');
  assert.equal(player.wantedLevel, 0, 'Resting at safehouse must cool down minor heat');
});

test('openWorld: inventory items can be consumed for authentic gameplay benefits', () => {
  const player = createInitialPlayerState();
  player.hp = 50;
  player.stamina = 30;

  // Drink Cha
  const chaRes = useInventoryItem(player, null, 'item_cha_flask');
  assert.equal(chaRes.ok, true);
  assert.equal(player.hp, 80);
  assert.equal(player.stamina, 70);

  // Add and use emergency jerrycan
  player.inventory.push({
    id: 'item_jerrycan',
    name: 'Emergency Jerrycan',
    banglaName: 'জেরিক্যান',
    kind: 'fuel',
    count: 1,
    value: 200,
    description: 'Emergency fuel',
  });
  const stalledVeh = createMockVehicle({ fuel: 0, isStalled: true });

  const jerryRes = useInventoryItem(player, stalledVeh, 'item_jerrycan');
  assert.equal(jerryRes.ok, true);
  assert.equal(stalledVeh.fuel, 35);
  assert.equal(stalledVeh.isStalled, false);
});
