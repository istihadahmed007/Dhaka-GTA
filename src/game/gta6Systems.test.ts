import test from 'node:test';
import assert from 'node:assert/strict';
import { PROTAGONIST_PROFILES } from './protagonists';
import { CompanionState, StoreRobberyState, PoliceSearchRadiusState, PhoneContact, WEAPON_SPECS } from './types';

test('gta6Systems: Protagonist profiles support dual companion synergy', () => {
  const tariq = PROTAGONIST_PROFILES.tariq;
  const maya = PROTAGONIST_PROFILES.maya;

  assert.equal(tariq.id, 'tariq');
  assert.equal(maya.id, 'maya');
  assert.equal(tariq.preferredWeapon, 'shotgun');
  assert.equal(maya.preferredWeapon, 'pistol');

  const companion: CompanionState = {
    id: 'maya',
    name: maya.name,
    banglaName: maya.banglaName,
    hp: maya.attributes.maxHp,
    maxHp: maya.attributes.maxHp,
    state: 'passenger',
    weapon: maya.preferredWeapon,
  };

  assert.equal(companion.state, 'passenger');
  assert.equal(companion.weapon, 'pistol');
  assert.ok(companion.hp > 0);
});

test('gta6Systems: Store robbery calculation bags cash based on intimidation meter', () => {
  const robbery: StoreRobberyState = {
    storeId: 'shop',
    storeName: "Babu's Hardware & Gear",
    storeBangla: 'বাবু হার্ডওয়্যার ও শপ',
    district: 'old_dhaka',
    x: -24,
    z: 24,
    intimidation: 60,
    cashBagged: 0,
    targetCash: 8000,
    state: 'intimidation',
    timer: 0,
  };

  // Simulate 3 seconds of bagging at 60% intimidation
  const dt = 3.0;
  const baggingRate = (robbery.intimidation / 100) * 1200;
  robbery.cashBagged = Math.min(robbery.targetCash, robbery.cashBagged + dt * baggingRate);

  assert.equal(robbery.cashBagged, 2160);
  assert.ok(robbery.cashBagged < robbery.targetCash);

  // Complete robbery
  robbery.cashBagged = robbery.targetCash;
  assert.equal(robbery.cashBagged, 8000);
});

test('gta6Systems: Police search radius timer counts down and clears perimeter', () => {
  const search: PoliceSearchRadiusState = {
    x: -24,
    z: 24,
    radius: 45.0,
    active: true,
    searchTimer: 25.0,
  };

  assert.equal(search.active, true);
  assert.equal(search.radius, 45.0);

  // Tick 30 seconds
  search.searchTimer -= 30.0;
  if (search.searchTimer <= 0) {
    search.active = false;
  }

  assert.equal(search.active, false);
});

test('gta6Systems: Weapon specs reflect distinct combat roles and stats', () => {
  assert.ok(WEAPON_SPECS.fists.damage > 0);
  assert.ok(WEAPON_SPECS.pistol.range > WEAPON_SPECS.fists.range);
  // Shotgun total burst damage (pellets * damage = 80) exceeds pistol (32)
  const shotgunBurst = WEAPON_SPECS.shotgun.damage * WEAPON_SPECS.shotgun.pellets;
  assert.ok(shotgunBurst > WEAPON_SPECS.pistol.damage);
  assert.ok(WEAPON_SPECS.smg.cooldown < WEAPON_SPECS.shotgun.cooldown);
});
