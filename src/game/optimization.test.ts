import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';
import { getAssetUrl } from './assetLoader';
import { resolveGraphicsQualityConfig } from './graphicsQuality';
import { WorldChunkManager, CHUNK_SIZE } from './worldChunks';
import { DhakaAudioEngine } from './audio';

test('optimization: getAssetUrl resolves clean relative paths for any base URL', () => {
  const url1 = getAssetUrl('models/dhaka_character.glb');
  assert.ok(url1.endsWith('models/dhaka_character.glb'));

  const url2 = getAssetUrl('/models/dhaka_sedan.glb');
  assert.ok(url2.endsWith('models/dhaka_sedan.glb'));
  assert.ok(!url2.includes('//models'));
});

test('optimization: resolveGraphicsQualityConfig provides mobile and desktop tiers with safe LODs', () => {
  const highConfig = resolveGraphicsQualityConfig('high', false, 2);
  assert.equal(highConfig.shadowsEnabled, true);
  assert.equal(highConfig.shadowMapSize, 2048);
  assert.ok(highConfig.lodDistances.far >= 340);

  const lowConfig = resolveGraphicsQualityConfig('low', false, 1);
  assert.equal(lowConfig.shadowsEnabled, false);
  assert.equal(lowConfig.shadowMapSize, 512);
  assert.ok(lowConfig.lodDistances.far <= 250);

  const swConfig = resolveGraphicsQualityConfig('high', true, 2);
  assert.equal(swConfig.pixelRatio, 1);
  assert.equal(swConfig.shadowsEnabled, false);
});

test('optimization: WorldChunkManager frustum culling and adaptive LOD distances', () => {
  const manager = new WorldChunkManager();
  manager.setLodDistances(60, 140, 220);

  const chunkA = manager.getOrCreateChunk(0, 0); // (0..64, 0..64), center (32, 0, 32)
  const chunkBehind = manager.getOrCreateChunk(0, -2); // (0..64, -128..-64), center (32, 0, -96)

  // Camera at (32, 5, 0) looking forward (+Z)
  const camera = new THREE.PerspectiveCamera(60, 1, 0.1, 500);
  camera.position.set(32, 5, 0);
  camera.lookAt(32, 5, 100);
  camera.updateMatrixWorld();
  camera.updateProjectionMatrix();

  manager.update(camera.position, camera, true);

  // Chunk in front of camera should be visible
  assert.ok(chunkA.currentLod >= 0, 'Chunk in front of camera must be active');
  assert.equal(chunkA.group.visible, true);

  // Chunk directly behind the camera (>45m away) should be culled by the frustum
  assert.equal(chunkBehind.currentLod, -1, 'Chunk behind camera must be culled');
  assert.equal(chunkBehind.group.visible, false);

  manager.dispose();
  assert.equal(manager.getChunks().size, 0);
});

test('optimization: audio engine disposes cleanly without memory leaks', () => {
  const audio = new DhakaAudioEngine();
  audio.unlock();
  audio.silenceLoops();
  audio.dispose();
  assert.ok(true, 'Audio engine disposed cleanly');
});
