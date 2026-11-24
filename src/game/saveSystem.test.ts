import { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import {
  createDefaultSaveGameData,
  validateAndSanitizeSaveData,
} from './saveSystem';

describe('saveSystem', () => {
  it('generates valid default starter save data', () => {
    const save = createDefaultSaveGameData();
    assert.equal(save.version, 1);
    assert.equal(save.player.money, 800);
    assert.equal(save.player.hp, 100);
    assert.ok(save.player.unlockedSafehouses.includes('old_dhaka_rooftop'));
    assert.ok(save.player.unlockedWeapons.includes('fists'));
    assert.ok(save.player.inventory.length > 0);
  });

  it('validates and sanitizes corrupt or partial payload', () => {
    const sanitized = validateAndSanitizeSaveData({
      version: 1,
      player: {
        x: 'not-a-number',
        money: -500,
        hp: 999,
        weapon: 'invalid_rocket_launcher',
      },
    });

    assert.ok(sanitized);
    assert.equal(typeof sanitized.player.x, 'number');
    assert.equal(sanitized.player.money, 0); // clamped to >= 0
    assert.equal(sanitized.player.hp, 100); // clamped to <= 100
    assert.equal(sanitized.player.weapon, 'fists'); // defaulted to fists
    assert.ok(sanitized.player.unlockedSafehouses.includes('old_dhaka_rooftop'));
  });

  it('rejects future unsupported save versions', () => {
    const rejected = validateAndSanitizeSaveData({
      version: 999,
      player: {},
    });
    assert.equal(rejected, null);
  });

  it('preserves valid custom inventory and stats', () => {
    const custom = createDefaultSaveGameData();
    custom.player.money = 25400;
    custom.player.unlockedSafehouses.push('gulshan_penthouse');
    custom.missions.missionsCompleted = 4;
    custom.missions.bountiesCompleted = 6;

    const validated = validateAndSanitizeSaveData(custom);
    assert.ok(validated);
    assert.equal(validated.player.money, 25400);
    assert.ok(validated.player.unlockedSafehouses.includes('gulshan_penthouse'));
    assert.equal(validated.missions.missionsCompleted, 4);
    assert.equal(validated.missions.bountiesCompleted, 6);
  });
});
