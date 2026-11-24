import { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import {
  PROTAGONIST_PROFILES,
  createInitialProtagonistState,
  createInitialCharacterSwitchState,
} from './protagonists';

describe('protagonists system', () => {
  it('defines valid distinct profiles for Tariq and Maya with unique attributes', () => {
    const tariq = PROTAGONIST_PROFILES.tariq;
    const maya = PROTAGONIST_PROFILES.maya;

    assert.equal(tariq.id, 'tariq');
    assert.equal(maya.id, 'maya');
    assert.ok(tariq.banglaName.length > 0);
    assert.ok(maya.banglaName.length > 0);

    // Tariq has superior drift and melee characteristics
    assert.ok(tariq.attributes.driftHandlingBonus > maya.attributes.driftHandlingBonus);
    assert.ok(tariq.attributes.meleeDamageMultiplier > maya.attributes.meleeDamageMultiplier);
    assert.equal(tariq.specialAbility, 'street_overdrive');

    // Maya has superior aim precision and sprint agility
    assert.ok(maya.attributes.aimPrecisionMultiplier > tariq.attributes.aimPrecisionMultiplier);
    assert.ok(maya.attributes.runSpeed > tariq.attributes.runSpeed);
    assert.equal(maya.specialAbility, 'focus_reflex');
  });

  it('initializes protagonist instance state accurately', () => {
    const state = createInitialProtagonistState('maya', 120, -80);
    assert.equal(state.id, 'maya');
    assert.equal(state.x, 120);
    assert.equal(state.z, -80);
    assert.equal(state.hp, PROTAGONIST_PROFILES.maya.attributes.maxHp);
    assert.equal(state.abilityActive, false);
    assert.equal(state.abilityTimer, 0);
  });

  it('creates clean character switch state transition structure', () => {
    const sw = createInitialCharacterSwitchState();
    assert.equal(sw.active, false);
    assert.equal(sw.phase, 'none');
    assert.ok(sw.skyAltitude >= 40);
  });
});
