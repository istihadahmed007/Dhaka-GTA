import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';
import { WorldChunkManager, CHUNK_SIZE, LOD_NEAR_DIST, LOD_MED_DIST, LOD_FAR_DIST } from './worldChunks';
import { BUILDINGS } from './worldData';

test('WorldChunkManager partitions buildings and manages 3-tier LOD visibility', () => {
  const manager = new WorldChunkManager();

  assert.equal(manager.getChunkKey(0, 0), '0_0');
  assert.equal(manager.getChunkKey(CHUNK_SIZE * 2 + 5, -CHUNK_SIZE - 2), '2_-2');

  // Partition all real Dhaka buildings
  manager.partitionBuildings(BUILDINGS);
  const chunks = manager.getChunks();
  assert.ok(chunks.size > 10, 'Expected multiple spatial chunks covering metropolitan Dhaka');

  // Verify center calculation of a chunk
  const chunk0 = manager.getOrCreateChunk(0, 0);
  assert.equal(chunk0.center.x, CHUNK_SIZE * 0.5);
  assert.equal(chunk0.center.z, CHUNK_SIZE * 0.5);

  // Position camera directly at center of chunk0 (distance ~0m -> LOD 0)
  manager.update(new THREE.Vector3(chunk0.center.x, 0, chunk0.center.z), true);
  assert.equal(chunk0.currentLod, 0, 'Chunk centered on camera must be LOD 0');
  assert.equal(chunk0.group.visible, true);
  assert.equal(chunk0.lod0Group.visible, true);
  assert.equal(chunk0.lod1Group.visible, false);
  assert.equal(chunk0.lod2Group.visible, false);

  // Position camera at medium distance (e.g. 150m away -> LOD 1)
  const medDist = (LOD_NEAR_DIST + LOD_MED_DIST) * 0.5; // ~152.5m
  manager.update(new THREE.Vector3(chunk0.center.x + medDist, 0, chunk0.center.z), true);
  assert.equal(chunk0.currentLod, 1, 'Chunk at 150m must switch to LOD 1');
  assert.equal(chunk0.group.visible, true);
  assert.equal(chunk0.lod0Group.visible, false);
  assert.equal(chunk0.lod1Group.visible, true);
  assert.equal(chunk0.lod2Group.visible, false);

  // Position camera at far distance (e.g. 260m away -> LOD 2)
  const farDist = (LOD_MED_DIST + LOD_FAR_DIST) * 0.5; // ~275m
  manager.update(new THREE.Vector3(chunk0.center.x + farDist, 0, chunk0.center.z), true);
  assert.equal(chunk0.currentLod, 2, 'Chunk at 275m must switch to LOD 2');
  assert.equal(chunk0.group.visible, true);
  assert.equal(chunk0.lod0Group.visible, false);
  assert.equal(chunk0.lod1Group.visible, false);
  assert.equal(chunk0.lod2Group.visible, true);

  // Position camera beyond culling distance (>340m -> Culled -1)
  manager.update(new THREE.Vector3(chunk0.center.x + LOD_FAR_DIST + 50, 0, chunk0.center.z), true);
  assert.equal(chunk0.currentLod, -1, 'Chunk beyond 340m must be culled');
  assert.equal(chunk0.group.visible, false);

  // Verify throttling: moving camera by only 1.0m without force should NOT trigger update
  const statsBefore = { ...manager.getStats() };
  manager.update(new THREE.Vector3(chunk0.center.x + LOD_FAR_DIST + 51, 0, chunk0.center.z), false);
  assert.deepEqual(manager.getStats(), statsBefore);

  manager.dispose();
  assert.equal(manager.getChunks().size, 0);
});
