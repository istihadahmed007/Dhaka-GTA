import * as THREE from 'three';
import { DistrictId } from './types';
import { BuildingSpec, RIVER_Z_START, WORLD_HALF_SIZE } from './worldData';

export const CHUNK_SIZE = 64; // 64m x 64m spatial grid partitioning
export const LOD_NEAR_DIST = 95; // LOD 0: full high detail (props, signs, neon, awnings)
export const LOD_MED_DIST = 210; // LOD 1: simplified architectural volumes
export const LOD_FAR_DIST = 340; // LOD 2: low-poly silhouette proxy; culled beyond 340m

export interface WorldChunkBounds {
  minX: number;
  maxX: number;
  minZ: number;
  maxZ: number;
}

export interface WorldChunk {
  id: string;
  cx: number;
  cz: number;
  bounds: WorldChunkBounds;
  center: THREE.Vector3;
  box: THREE.Box3;
  buildings: BuildingSpec[];
  group: THREE.Group;
  lod0Group: THREE.Group;
  lod1Group: THREE.Group;
  lod2Group: THREE.Group;
  currentLod: 0 | 1 | 2 | -1; // -1 = culled / hidden
}

export interface ChunkManagerStats {
  totalChunks: number;
  activeChunks: number;
  lod0Count: number;
  lod1Count: number;
  lod2Count: number;
  culledCount: number;
}

/**
 * High-performance spatial chunk streaming, frustum culling, and Level-of-Detail (LOD) manager.
 * Divides the expansive Dhaka metropolitan world into a spatial grid, streaming
 * geometry dynamically around the camera to guarantee silky 60 FPS in Three.js.
 */
export class WorldChunkManager {
  private chunks = new Map<string, WorldChunk>();
  private root = new THREE.Group();
  private lastUpdatePos = new THREE.Vector3(-9999, -9999, -9999);
  private lastCamDir = new THREE.Vector3(0, 0, 1);
  private lodNearDist = LOD_NEAR_DIST;
  private lodMedDist = LOD_MED_DIST;
  private lodFarDist = LOD_FAR_DIST;
  private _frustum = new THREE.Frustum();
  private _projScreenMatrix = new THREE.Matrix4();
  private _camDir = new THREE.Vector3();

  private stats: ChunkManagerStats = {
    totalChunks: 0,
    activeChunks: 0,
    lod0Count: 0,
    lod1Count: 0,
    lod2Count: 0,
    culledCount: 0,
  };

  constructor() {
    this.root.name = 'WorldChunkRoot';
  }

  public getRoot(): THREE.Group {
    return this.root;
  }

  public setLodDistances(near: number, med: number, far: number): void {
    this.lodNearDist = near;
    this.lodMedDist = med;
    this.lodFarDist = far;
    this.lastUpdatePos.set(-9999, -9999, -9999);
  }

  public getChunkKey(x: number, z: number): string {
    const cx = Math.floor(x / CHUNK_SIZE);
    const cz = Math.floor(z / CHUNK_SIZE);
    return `${cx}_${cz}`;
  }

  public getOrCreateChunk(cx: number, cz: number): WorldChunk {
    const key = `${cx}_${cz}`;
    let chunk = this.chunks.get(key);
    if (!chunk) {
      const minX = cx * CHUNK_SIZE;
      const maxX = (cx + 1) * CHUNK_SIZE;
      const minZ = cz * CHUNK_SIZE;
      const maxZ = (cz + 1) * CHUNK_SIZE;
      const center = new THREE.Vector3((minX + maxX) * 0.5, 0, (minZ + maxZ) * 0.5);
      const box = new THREE.Box3(
        new THREE.Vector3(minX, -4, minZ),
        new THREE.Vector3(maxX, 55, maxZ),
      );

      const group = new THREE.Group();
      group.name = `Chunk_${key}`;

      const lod0Group = new THREE.Group();
      lod0Group.name = `LOD0_${key}`;
      const lod1Group = new THREE.Group();
      lod1Group.name = `LOD1_${key}`;
      const lod2Group = new THREE.Group();
      lod2Group.name = `LOD2_${key}`;

      group.visible = false;
      lod0Group.visible = false;
      lod1Group.visible = false;
      lod2Group.visible = false;

      group.add(lod0Group, lod1Group, lod2Group);
      this.root.add(group);

      chunk = {
        id: key,
        cx,
        cz,
        bounds: { minX, maxX, minZ, maxZ },
        center,
        box,
        buildings: [],
        group,
        lod0Group,
        lod1Group,
        lod2Group,
        currentLod: -1,
      };

      this.chunks.set(key, chunk);
      this.stats.totalChunks = this.chunks.size;
    }
    return chunk;
  }

  public partitionBuildings(buildings: readonly BuildingSpec[]): void {
    for (const b of buildings) {
      const cx = Math.floor(b.x / CHUNK_SIZE);
      const cz = Math.floor(b.z / CHUNK_SIZE);
      const chunk = this.getOrCreateChunk(cx, cz);
      chunk.buildings.push(b);
      if (b.h + 8 > chunk.box.max.y) {
        chunk.box.max.y = b.h + 8;
      }
    }
  }

  public getChunks(): Map<string, WorldChunk> {
    return this.chunks;
  }

  public getStats(): ChunkManagerStats {
    return this.stats;
  }

  /**
   * Updates chunk visibility, frustum culling, and LODs based on current camera position and orientation.
   * Throttles updates when camera has moved less than 3m and rotated less than 0.08 rad to minimize CPU overhead.
   */
  public update(
    cameraPosition: THREE.Vector3,
    cameraOrFrustum?: THREE.Camera | THREE.Frustum | boolean,
    force = false,
  ): void {
    const isExplicitForce = typeof cameraOrFrustum === 'boolean' ? cameraOrFrustum : force;
    let frustum: THREE.Frustum | null = null;
    let camDirRotated = false;

    if (cameraOrFrustum && typeof cameraOrFrustum !== 'boolean') {
      if (cameraOrFrustum instanceof THREE.Camera) {
        const cam = cameraOrFrustum;
        this._projScreenMatrix.multiplyMatrices(cam.projectionMatrix, cam.matrixWorldInverse);
        this._frustum.setFromProjectionMatrix(this._projScreenMatrix);
        frustum = this._frustum;
        cam.getWorldDirection(this._camDir);
        if (this.lastCamDir.angleTo(this._camDir) > 0.08) {
          camDirRotated = true;
          this.lastCamDir.copy(this._camDir);
        }
      } else {
        frustum = cameraOrFrustum;
      }
    }

    if (!isExplicitForce && !camDirRotated && this.lastUpdatePos.distanceToSquared(cameraPosition) < 9.0) {
      return;
    }
    this.lastUpdatePos.copy(cameraPosition);

    let lod0 = 0;
    let lod1 = 0;
    let lod2 = 0;
    let culled = 0;

    const camX = cameraPosition.x;
    const camZ = cameraPosition.z;

    for (const chunk of this.chunks.values()) {
      // 2D horizontal distance to chunk center
      const dx = chunk.center.x - camX;
      const dz = chunk.center.z - camZ;
      const dist = Math.hypot(dx, dz);

      let targetLod: 0 | 1 | 2 | -1 = -1;

      if (dist < this.lodNearDist) {
        targetLod = 0;
      } else if (dist < this.lodMedDist) {
        targetLod = 1;
      } else if (dist < this.lodFarDist) {
        targetLod = 2;
      } else {
        targetLod = -1;
      }

      // Frustum culling: chunks beyond 45m distance that fall outside camera frustum are culled early
      if (targetLod !== -1 && frustum && dist > 45) {
        if (!frustum.intersectsBox(chunk.box)) {
          targetLod = -1;
        }
      }

      if (targetLod === 0) lod0++;
      else if (targetLod === 1) lod1++;
      else if (targetLod === 2) lod2++;
      else culled++;

      if (chunk.currentLod !== targetLod) {
        chunk.currentLod = targetLod;
        chunk.group.visible = targetLod !== -1;
        chunk.lod0Group.visible = targetLod === 0;
        chunk.lod1Group.visible = targetLod === 1;
        chunk.lod2Group.visible = targetLod === 2;
      }
    }

    this.stats.activeChunks = lod0 + lod1 + lod2;
    this.stats.lod0Count = lod0;
    this.stats.lod1Count = lod1;
    this.stats.lod2Count = lod2;
    this.stats.culledCount = culled;
  }

  public dispose(): void {
    for (const chunk of this.chunks.values()) {
      chunk.group.traverse((obj) => {
        const mesh = obj as THREE.Mesh;
        if (mesh.isMesh) {
          mesh.geometry?.dispose();
          if (Array.isArray(mesh.material)) {
            mesh.material.forEach((m) => m.dispose());
          } else {
            mesh.material?.dispose();
          }
        }
      });
      this.root.remove(chunk.group);
    }
    this.chunks.clear();
    this.stats = {
      totalChunks: 0,
      activeChunks: 0,
      lod0Count: 0,
      lod1Count: 0,
      lod2Count: 0,
      culledCount: 0,
    };
  }
}
