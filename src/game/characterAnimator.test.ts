import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { computeLocomotionWeights } from './characterAnimator';
import { resolveGraphicsQualityConfig } from './graphicsQuality';

test('computeLocomotionWeights smoothly blends Idle -> Walk -> Run with normalized weights', () => {
  const idle = computeLocomotionWeights(0, 5.8, 9.6, true);
  assert.equal(idle.idleWeight, 1);
  assert.equal(idle.walkWeight, 0);
  assert.equal(idle.runWeight, 0);

  const slowWalk = computeLocomotionWeights(2.8, 5.8, 9.6, true);
  assert.ok(slowWalk.idleWeight > 0 && slowWalk.idleWeight < 1);
  assert.ok(slowWalk.walkWeight > 0 && slowWalk.walkWeight < 1);
  assert.equal(slowWalk.runWeight, 0);
  assert.ok(Math.abs(slowWalk.idleWeight + slowWalk.walkWeight + slowWalk.runWeight - 1) < 1e-6);

  const fullWalk = computeLocomotionWeights(5.8, 5.8, 9.6, true);
  assert.equal(fullWalk.idleWeight, 0);
  assert.equal(fullWalk.walkWeight, 1);
  assert.equal(fullWalk.runWeight, 0);

  const jog = computeLocomotionWeights(7.7, 5.8, 9.6, true);
  assert.equal(jog.idleWeight, 0);
  assert.ok(jog.walkWeight > 0 && jog.walkWeight < 1);
  assert.ok(jog.runWeight > 0 && jog.runWeight < 1);
  assert.ok(Math.abs(jog.walkWeight + jog.runWeight - 1) < 1e-6);

  const sprint = computeLocomotionWeights(9.8, 5.8, 9.6, true);
  assert.equal(sprint.idleWeight, 0);
  assert.equal(sprint.walkWeight, 0);
  assert.equal(sprint.runWeight, 1);
  assert.ok(sprint.forwardLeanRad > 0.08);
});

test('computeLocomotionWeights slows stride cadence when airborne', () => {
  const air = computeLocomotionWeights(6.5, 5.8, 9.6, false);
  assert.ok(air.walkTimeScale < 0.5);
  assert.ok(air.runTimeScale < 0.5);
  assert.ok(Math.abs(air.idleWeight + air.walkWeight + air.runWeight - 1) < 1e-6);
});

test('resolveGraphicsQualityConfig protects software WebGL renderers across all presets', () => {
  for (const preset of ['low', 'medium', 'high'] as const) {
    const sw = resolveGraphicsQualityConfig(preset, true, 2);
    assert.equal(sw.shadowsEnabled, false);
    assert.equal(sw.pixelRatio, 1);
  }

  const hwLow = resolveGraphicsQualityConfig('low', false, 2);
  assert.equal(hwLow.shadowsEnabled, false);
  assert.equal(hwLow.pixelRatio, 1);
  assert.equal(hwLow.lampHalosEnabled, false);

  const hwMed = resolveGraphicsQualityConfig('medium', false, 2);
  assert.equal(hwMed.shadowsEnabled, true);
  assert.equal(hwMed.shadowMapSize, 1024);
  assert.equal(hwMed.pixelRatio, 1.5);

  const hwHigh = resolveGraphicsQualityConfig('high', false, 2);
  assert.equal(hwHigh.shadowsEnabled, true);
  assert.equal(hwHigh.shadowMapSize, 2048);
  assert.equal(hwHigh.pixelRatio, 2);
});

test('public/models/dhaka_character.glb is a valid compact glTF 2.0 rigged character with 6 animation clips', () => {
  const glbPath = path.resolve(process.cwd(), 'public/models/dhaka_character.glb');
  assert.ok(fs.existsSync(glbPath), 'Expected public/models/dhaka_character.glb to exist');

  const buf = fs.readFileSync(glbPath);
  assert.ok(buf.byteLength > 20_000 && buf.byteLength < 250_000, `Expected compact GLB, got ${buf.byteLength} bytes`);

  // Check GLB magic header ("glTF", version 2)
  assert.equal(buf.toString('ascii', 0, 4), 'glTF');
  assert.equal(buf.readUInt32LE(4), 2);

  // Parse JSON chunk (starts at offset 12: chunkLength u32, chunkType u32 = 0x4E4F534A "JSON")
  const jsonLen = buf.readUInt32LE(12);
  const chunkType = buf.toString('ascii', 16, 20);
  assert.equal(chunkType, 'JSON');
  const gltf = JSON.parse(buf.toString('utf8', 20, 20 + jsonLen)) as {
    skins?: Array<{ joints: number[] }>;
    animations?: Array<{ name: string }>;
    nodes?: Array<{ name?: string }>;
  };

  assert.ok(Array.isArray(gltf.skins) && gltf.skins.length >= 1, 'Expected at least 1 skin');
  assert.equal(gltf.skins[0].joints.length, 17, 'Expected 17 humanoid skeleton joints');

  const nodeNames = new Set((gltf.nodes ?? []).map((n) => n.name));
  for (const requiredBone of ['Hips', 'Spine', 'Chest', 'Head', 'RightHand', 'LeftThigh', 'RightShin']) {
    assert.ok(nodeNames.has(requiredBone), `Missing required bone: ${requiredBone}`);
  }

  const clipNames = new Set((gltf.animations ?? []).map((a) => a.name));
  for (const requiredClip of ['Idle', 'Walk', 'Run', 'Attack', 'Hit', 'KO']) {
    assert.ok(clipNames.has(requiredClip), `Missing required AnimationClip: ${requiredClip}`);
  }
});
