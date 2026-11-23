/**
 * Offline build script to generate a lightweight, original (CC0 / project-authored)
 * rigged & animated glTF 2.0 binary character (/public/models/dhaka_character.glb).
 *
 * Skeleton naming follows standard humanoid conventions (Hips, Spine, Chest, Neck, Head,
 * Left/Right UpperArm, Forearm, Hand, Thigh, Shin, Foot) so offline retargeted clips
 * exported from Mesh2Motion / Blender / MoCapAnything can drop straight onto the rig.
 *
 * Usage: npx --yes tsx scripts/buildCharacterGlb.ts
 */
import fs from 'node:fs';
import path from 'node:path';
import * as THREE from 'three';
import { mergeGeometries, mergeVertices } from 'three/addons/utils/BufferGeometryUtils.js';
import { GLTFExporter } from 'three/addons/exporters/GLTFExporter.js';

// Minimal Node FileReader polyfill for Three.js GLTFExporter binary output
if (typeof globalThis.FileReader === 'undefined') {
  class NodeFileReader {
    result: ArrayBuffer | string | null = null;
    onloadend: (() => void) | null = null;
    onerror: ((err: unknown) => void) | null = null;

    readAsArrayBuffer(blob: Blob): void {
      blob
        .arrayBuffer()
        .then((buf) => {
          this.result = buf;
          this.onloadend?.();
        })
        .catch((err) => {
          this.onerror?.(err);
        });
    }

    readAsDataURL(blob: Blob): void {
      blob
        .arrayBuffer()
        .then((buf) => {
          const b64 = Buffer.from(buf).toString('base64');
          this.result = `data:${blob.type || 'application/octet-stream'};base64,${b64}`;
          this.onloadend?.();
        })
        .catch((err) => {
          this.onerror?.(err);
        });
    }
  }
  (globalThis as unknown as { FileReader: typeof NodeFileReader }).FileReader = NodeFileReader;
}

const BONE_NAMES = [
  'Hips', // 0
  'Spine', // 1
  'Chest', // 2
  'Neck', // 3
  'Head', // 4
  'LeftUpperArm', // 5
  'LeftForearm', // 6
  'LeftHand', // 7
  'RightUpperArm', // 8
  'RightForearm', // 9
  'RightHand', // 10
  'LeftThigh', // 11
  'LeftShin', // 12
  'LeftFoot', // 13
  'RightThigh', // 14
  'RightShin', // 15
  'RightFoot', // 16
] as const;

type BoneName = (typeof BONE_NAMES)[number];

const BONE_IDX: Record<BoneName, number> = Object.fromEntries(
  BONE_NAMES.map((n, i) => [n, i]),
) as Record<BoneName, number>;

interface WeightedPart {
  geo: THREE.BufferGeometry;
  boneA: BoneName;
  boneB?: BoneName;
  blendAxis?: 'y';
  blendMin?: number;
  blendMax?: number;
}

function createSkeletonHierarchy(): { rootBone: THREE.Bone; bones: THREE.Bone[]; skeleton: THREE.Skeleton } {
  const map: Record<BoneName, THREE.Bone> = {} as Record<BoneName, THREE.Bone>;
  for (const name of BONE_NAMES) {
    const b = new THREE.Bone();
    b.name = name;
    map[name] = b;
  }

  // Bind-pose positions (character stands at y = 0, facing +Z, ~1.84m tall)
  map.Hips.position.set(0, 0.9, 0);

  map.Spine.position.set(0, 0.16, 0);
  map.Hips.add(map.Spine);

  map.Chest.position.set(0, 0.24, 0);
  map.Spine.add(map.Chest);

  map.Neck.position.set(0, 0.21, 0);
  map.Chest.add(map.Neck);

  map.Head.position.set(0, 0.12, 0.01);
  map.Neck.add(map.Head);

  // Arms (slightly relaxed A-pose shoulders)
  map.LeftUpperArm.position.set(-0.24, 0.15, 0);
  map.Chest.add(map.LeftUpperArm);
  map.LeftForearm.position.set(0, -0.27, 0);
  map.LeftUpperArm.add(map.LeftForearm);
  map.LeftHand.position.set(0, -0.25, 0.02);
  map.LeftForearm.add(map.LeftHand);

  map.RightUpperArm.position.set(0.24, 0.15, 0);
  map.Chest.add(map.RightUpperArm);
  map.RightForearm.position.set(0, -0.27, 0);
  map.RightUpperArm.add(map.RightForearm);
  map.RightHand.position.set(0, -0.25, 0.02);
  map.RightForearm.add(map.RightHand);

  // Legs
  map.LeftThigh.position.set(-0.115, -0.02, 0);
  map.Hips.add(map.LeftThigh);
  map.LeftShin.position.set(0, -0.43, 0);
  map.LeftThigh.add(map.LeftShin);
  map.LeftFoot.position.set(0, -0.39, 0.04);
  map.LeftShin.add(map.LeftFoot);

  map.RightThigh.position.set(0.115, -0.02, 0);
  map.Hips.add(map.RightThigh);
  map.RightShin.position.set(0, -0.43, 0);
  map.RightThigh.add(map.RightShin);
  map.RightFoot.position.set(0, -0.39, 0.04);
  map.RightShin.add(map.RightFoot);

  const bones = BONE_NAMES.map((n) => map[n]);
  map.Hips.updateMatrixWorld(true);
  const skeleton = new THREE.Skeleton(bones);
  return { rootBone: map.Hips, bones, skeleton };
}

function prepareWeightedGeometry(parts: WeightedPart[]): THREE.BufferGeometry {
  const normalized: THREE.BufferGeometry[] = [];

  for (const part of parts) {
    const g = part.geo.index ? part.geo.toNonIndexed() : part.geo.clone();
    for (const key of Object.keys(g.attributes)) {
      if (key !== 'position' && key !== 'normal' && key !== 'uv') {
        g.deleteAttribute(key);
      }
    }
    if (!g.attributes.normal) g.computeVertexNormals();
    if (!g.attributes.uv) {
      const count = g.attributes.position.count;
      g.setAttribute('uv', new THREE.Float32BufferAttribute(new Float32Array(count * 2), 2));
    }

    const pos = g.attributes.position;
    const count = pos.count;
    const indices = new Uint16Array(count * 4);
    const weights = new Float32Array(count * 4);
    const idxA = BONE_IDX[part.boneA];
    const idxB = part.boneB ? BONE_IDX[part.boneB] : idxA;

    for (let i = 0; i < count; i++) {
      let wB = 0;
      if (part.boneB && part.blendMin !== undefined && part.blendMax !== undefined) {
        const y = pos.getY(i);
        const t = THREE.MathUtils.clamp((y - part.blendMin) / (part.blendMax - part.blendMin), 0, 1);
        wB = t * t * (3 - 2 * t);
      }
      const wA = 1 - wB;
      indices[i * 4] = idxA;
      indices[i * 4 + 1] = idxB;
      indices[i * 4 + 2] = 0;
      indices[i * 4 + 3] = 0;

      weights[i * 4] = wA;
      weights[i * 4 + 1] = wB;
      weights[i * 4 + 2] = 0;
      weights[i * 4 + 3] = 0;
    }

    g.setAttribute('skinIndex', new THREE.Uint16BufferAttribute(indices, 4));
    g.setAttribute('skinWeight', new THREE.Float32BufferAttribute(weights, 4));
    normalized.push(g);
  }

  const merged = mergeGeometries(normalized, false);
  if (!merged) {
    throw new Error('Failed to merge character part geometries');
  }
  normalized.forEach((g) => g.dispose());
  // Re-index shared vertices for compact GLB binary size (~120KB total)
  const indexed = mergeVertices(merged, 1e-4);
  merged.dispose();
  return indexed;
}

function buildSculptedParts(): {
  torsoGeo: THREE.BufferGeometry;
  pantsGeo: THREE.BufferGeometry;
  accentGeo: THREE.BufferGeometry;
  skinGeo: THREE.BufferGeometry;
  hairGeo: THREE.BufferGeometry;
  shoeGeo: THREE.BufferGeometry;
} {
  // 1. Torso / Shirt / Jacket + Sleeves + Collar (smooth tapered silhouettes)
  const chestCore = new THREE.CylinderGeometry(0.225, 0.185, 0.36, 10, 2);
  chestCore.scale(1.06, 1, 0.68);
  chestCore.translate(0, 1.33, 0);

  const waistCore = new THREE.CylinderGeometry(0.185, 0.195, 0.28, 10, 2);
  waistCore.scale(1.02, 1, 0.7);
  waistCore.translate(0, 1.04, 0);

  const lShoulderCap = new THREE.SphereGeometry(0.095, 8, 6);
  lShoulderCap.scale(1.05, 0.95, 0.95);
  lShoulderCap.translate(-0.23, 1.44, 0);

  const rShoulderCap = new THREE.SphereGeometry(0.095, 8, 6);
  rShoulderCap.scale(1.05, 0.95, 0.95);
  rShoulderCap.translate(0.23, 1.44, 0);

  const lUpperSleeve = new THREE.CapsuleGeometry(0.072, 0.2, 4, 8);
  lUpperSleeve.translate(-0.25, 1.31, 0);

  const rUpperSleeve = new THREE.CapsuleGeometry(0.072, 0.2, 4, 8);
  rUpperSleeve.translate(0.25, 1.31, 0);

  const collar = new THREE.CylinderGeometry(0.105, 0.135, 0.08, 8);
  collar.translate(0, 1.52, 0.01);

  const torsoGeo = prepareWeightedGeometry([
    { geo: chestCore, boneA: 'Spine', boneB: 'Chest', blendAxis: 'y', blendMin: 1.15, blendMax: 1.42 },
    { geo: waistCore, boneA: 'Hips', boneB: 'Spine', blendAxis: 'y', blendMin: 0.92, blendMax: 1.16 },
    { geo: lShoulderCap, boneA: 'Chest', boneB: 'LeftUpperArm', blendAxis: 'y', blendMin: 1.38, blendMax: 1.48 },
    { geo: rShoulderCap, boneA: 'Chest', boneB: 'RightUpperArm', blendAxis: 'y', blendMin: 1.38, blendMax: 1.48 },
    { geo: lUpperSleeve, boneA: 'LeftUpperArm' },
    { geo: rUpperSleeve, boneA: 'RightUpperArm' },
    { geo: collar, boneA: 'Chest' },
  ]);

  // 2. Pants (Sculpted Pelvis + Tapered Thighs + Shins)
  const pelvis = new THREE.SphereGeometry(0.195, 10, 6);
  pelvis.scale(1.04, 0.78, 0.74);
  pelvis.translate(0, 0.9, 0);

  const lThigh = new THREE.CapsuleGeometry(0.092, 0.28, 4, 8);
  lThigh.translate(-0.115, 0.68, 0);

  const rThigh = new THREE.CapsuleGeometry(0.092, 0.28, 4, 8);
  rThigh.translate(0.115, 0.68, 0);

  const lShin = new THREE.CapsuleGeometry(0.074, 0.28, 4, 8);
  lShin.translate(-0.115, 0.3, 0);

  const rShin = new THREE.CapsuleGeometry(0.074, 0.28, 4, 8);
  rShin.translate(0.115, 0.3, 0);

  const pantsGeo = prepareWeightedGeometry([
    { geo: pelvis, boneA: 'Hips' },
    { geo: lThigh, boneA: 'LeftThigh' },
    { geo: rThigh, boneA: 'RightThigh' },
    { geo: lShin, boneA: 'LeftShin' },
    { geo: rShin, boneA: 'RightShin' },
  ]);

  // 3. Accent (Tailored Panjabi / Jacket Front Placket + Waist Belt + Collar Trim)
  const placket = new THREE.BoxGeometry(0.13, 0.44, 0.045);
  placket.translate(0, 1.28, 0.142);

  const belt = new THREE.CylinderGeometry(0.2, 0.2, 0.055, 10);
  belt.scale(1.03, 1, 0.73);
  belt.translate(0, 0.95, 0);

  const lapelL = new THREE.BoxGeometry(0.055, 0.24, 0.04);
  lapelL.rotateZ(-0.18);
  lapelL.translate(-0.085, 1.38, 0.14);

  const lapelR = new THREE.BoxGeometry(0.055, 0.24, 0.04);
  lapelR.rotateZ(0.18);
  lapelR.translate(0.085, 1.38, 0.14);

  const accentGeo = prepareWeightedGeometry([
    { geo: placket, boneA: 'Spine', boneB: 'Chest', blendAxis: 'y', blendMin: 1.1, blendMax: 1.44 },
    { geo: belt, boneA: 'Hips' },
    { geo: lapelL, boneA: 'Chest' },
    { geo: lapelR, boneA: 'Chest' },
  ]);

  // 4. Skin (Sculpted Head, Neck, Forearms, Hands)
  const neck = new THREE.CylinderGeometry(0.062, 0.072, 0.12, 8);
  neck.translate(0, 1.56, 0);

  const head = new THREE.SphereGeometry(0.138, 10, 8);
  head.scale(0.92, 1.12, 0.98);
  head.translate(0, 1.72, 0.01);

  const nose = new THREE.ConeGeometry(0.022, 0.052, 5);
  nose.rotateX(Math.PI / 2);
  nose.translate(0, 1.71, 0.145);

  const lForearm = new THREE.CapsuleGeometry(0.054, 0.18, 4, 8);
  lForearm.translate(-0.25, 1.07, 0.015);

  const rForearm = new THREE.CapsuleGeometry(0.054, 0.18, 4, 8);
  rForearm.translate(0.25, 1.07, 0.015);

  const lHand = new THREE.SphereGeometry(0.055, 6, 6);
  lHand.scale(0.85, 1.15, 0.85);
  lHand.translate(-0.25, 0.92, 0.025);

  const rHand = new THREE.SphereGeometry(0.055, 6, 6);
  rHand.scale(0.85, 1.15, 0.85);
  rHand.translate(0.25, 0.92, 0.025);

  const skinGeo = prepareWeightedGeometry([
    { geo: neck, boneA: 'Neck' },
    { geo: head, boneA: 'Head' },
    { geo: nose, boneA: 'Head' },
    { geo: lForearm, boneA: 'LeftForearm' },
    { geo: rForearm, boneA: 'RightForearm' },
    { geo: lHand, boneA: 'LeftHand' },
    { geo: rHand, boneA: 'RightHand' },
  ]);

  // 5. Hair (Sculpted Dhaka swept hair cap)
  const hairCrown = new THREE.SphereGeometry(0.145, 10, 7, 0, Math.PI * 2, 0, Math.PI * 0.62);
  hairCrown.scale(0.95, 1.08, 1.04);
  hairCrown.translate(0, 1.74, -0.008);

  const hairBack = new THREE.SphereGeometry(0.125, 8, 6);
  hairBack.scale(0.95, 0.85, 0.68);
  hairBack.translate(0, 1.72, -0.075);

  const hairGeo = prepareWeightedGeometry([
    { geo: hairCrown, boneA: 'Head' },
    { geo: hairBack, boneA: 'Head' },
  ]);

  // 6. Shoes (Sculpted rounded street sneakers)
  const makeShoe = (xOff: number): THREE.BufferGeometry => {
    const sole = new THREE.CapsuleGeometry(0.068, 0.14, 4, 6);
    sole.rotateX(Math.PI / 2);
    sole.scale(1.02, 0.72, 1.05);
    sole.translate(xOff, 0.052, 0.055);
    return sole;
  };
  const shoeGeo = prepareWeightedGeometry([
    { geo: makeShoe(-0.115), boneA: 'LeftFoot' },
    { geo: makeShoe(0.115), boneA: 'RightFoot' },
  ]);

  return { torsoGeo, pantsGeo, accentGeo, skinGeo, hairGeo, shoeGeo };
}

function quatTrack(bone: BoneName, times: number[], eulers: Array<[number, number, number]>): THREE.QuaternionKeyframeTrack {
  const values: number[] = [];
  const q = new THREE.Quaternion();
  const e = new THREE.Euler(0, 0, 0, 'XYZ');
  for (const [rx, ry, rz] of eulers) {
    e.set(rx, ry, rz);
    q.setFromEuler(e);
    values.push(q.x, q.y, q.z, q.w);
  }
  return new THREE.QuaternionKeyframeTrack(`${bone}.quaternion`, times, values);
}

function posTrack(bone: BoneName, times: number[], positions: Array<[number, number, number]>): THREE.VectorKeyframeTrack {
  const values: number[] = [];
  for (const [x, y, z] of positions) {
    values.push(x, y, z);
  }
  return new THREE.VectorKeyframeTrack(`${bone}.position`, times, values);
}

function buildAnimationClips(): THREE.AnimationClip[] {
  // 1. IDLE (2.4s loop — calm Dhaka street stance with breathing & subtle weight shift)
  const idleT = [0, 0.6, 1.2, 1.8, 2.4];
  const idleClip = new THREE.AnimationClip('Idle', 2.4, [
    posTrack('Hips', idleT, [
      [0, 0.9, 0],
      [0.008, 0.908, 0],
      [0, 0.9, 0],
      [-0.008, 0.906, 0],
      [0, 0.9, 0],
    ]),
    quatTrack('Spine', idleT, [
      [0.02, 0, 0],
      [-0.015, 0.02, 0.01],
      [0.02, 0, 0],
      [-0.015, -0.02, -0.01],
      [0.02, 0, 0],
    ]),
    quatTrack('Chest', idleT, [
      [0.01, 0, 0],
      [-0.03, 0.015, 0],
      [0.01, 0, 0],
      [-0.025, -0.015, 0],
      [0.01, 0, 0],
    ]),
    quatTrack('Head', idleT, [
      [0, 0, 0],
      [-0.02, 0.05, 0],
      [0.01, 0, 0],
      [-0.02, -0.05, 0],
      [0, 0, 0],
    ]),
    quatTrack('LeftUpperArm', idleT, [
      [0.04, 0, -0.09],
      [0.07, 0, -0.11],
      [0.04, 0, -0.09],
      [0.02, 0, -0.08],
      [0.04, 0, -0.09],
    ]),
    quatTrack('LeftForearm', idleT, [
      [-0.16, 0, 0],
      [-0.2, 0, 0],
      [-0.16, 0, 0],
      [-0.14, 0, 0],
      [-0.16, 0, 0],
    ]),
    quatTrack('RightUpperArm', idleT, [
      [0.04, 0, 0.09],
      [0.02, 0, 0.08],
      [0.04, 0, 0.09],
      [0.07, 0, 0.11],
      [0.04, 0, 0.09],
    ]),
    quatTrack('RightForearm', idleT, [
      [-0.16, 0, 0],
      [-0.14, 0, 0],
      [-0.16, 0, 0],
      [-0.2, 0, 0],
      [-0.16, 0, 0],
    ]),
  ]);

  // 2. WALK (0.96s loop — realistic heel-strike, passing knee bend, counter-rotated shoulders)
  const walkT = [0, 0.24, 0.48, 0.72, 0.96];
  const walkClip = new THREE.AnimationClip('Walk', 0.96, [
    posTrack('Hips', walkT, [
      [0, 0.875, 0],
      [0.018, 0.915, 0],
      [0, 0.875, 0],
      [-0.018, 0.915, 0],
      [0, 0.875, 0],
    ]),
    quatTrack('Hips', walkT, [
      [0.04, 0.08, 0.02],
      [0.02, 0, -0.02],
      [0.04, -0.08, -0.02],
      [0.02, 0, 0.02],
      [0.04, 0.08, 0.02],
    ]),
    quatTrack('Spine', walkT, [
      [0.04, -0.06, 0],
      [0.02, 0, 0],
      [0.04, 0.06, 0],
      [0.02, 0, 0],
      [0.04, -0.06, 0],
    ]),
    quatTrack('Chest', walkT, [
      [0.02, -0.08, 0],
      [0.01, 0, 0],
      [0.02, 0.08, 0],
      [0.01, 0, 0],
      [0.02, -0.08, 0],
    ]),
    quatTrack('LeftThigh', walkT, [
      [-0.48, 0, 0],
      [0.04, 0, 0],
      [0.42, 0, 0],
      [-0.16, 0, 0],
      [-0.48, 0, 0],
    ]),
    quatTrack('LeftShin', walkT, [
      [0.12, 0, 0],
      [0.08, 0, 0],
      [0.24, 0, 0],
      [0.68, 0, 0],
      [0.12, 0, 0],
    ]),
    quatTrack('LeftFoot', walkT, [
      [-0.14, 0, 0],
      [0.0, 0, 0],
      [0.22, 0, 0],
      [-0.05, 0, 0],
      [-0.14, 0, 0],
    ]),
    quatTrack('RightThigh', walkT, [
      [0.42, 0, 0],
      [-0.16, 0, 0],
      [-0.48, 0, 0],
      [0.04, 0, 0],
      [0.42, 0, 0],
    ]),
    quatTrack('RightShin', walkT, [
      [0.24, 0, 0],
      [0.68, 0, 0],
      [0.12, 0, 0],
      [0.08, 0, 0],
      [0.24, 0, 0],
    ]),
    quatTrack('RightFoot', walkT, [
      [0.22, 0, 0],
      [-0.05, 0, 0],
      [-0.14, 0, 0],
      [0.0, 0, 0],
      [0.22, 0, 0],
    ]),
    quatTrack('LeftUpperArm', walkT, [
      [0.36, 0, -0.08],
      [0.02, 0, -0.08],
      [-0.4, 0, -0.08],
      [-0.02, 0, -0.08],
      [0.36, 0, -0.08],
    ]),
    quatTrack('LeftForearm', walkT, [
      [-0.22, 0, 0],
      [-0.36, 0, 0],
      [-0.48, 0, 0],
      [-0.28, 0, 0],
      [-0.22, 0, 0],
    ]),
    quatTrack('RightUpperArm', walkT, [
      [-0.4, 0, 0.08],
      [-0.02, 0, 0.08],
      [0.36, 0, 0.08],
      [0.02, 0, 0.08],
      [-0.4, 0, 0.08],
    ]),
    quatTrack('RightForearm', walkT, [
      [-0.48, 0, 0],
      [-0.28, 0, 0],
      [-0.22, 0, 0],
      [-0.36, 0, 0],
      [-0.48, 0, 0],
    ]),
  ]);

  // 3. RUN (0.64s loop — athletic Dhaka sprint with forward lean & high knee drive)
  const runT = [0, 0.16, 0.32, 0.48, 0.64];
  const runClip = new THREE.AnimationClip('Run', 0.64, [
    posTrack('Hips', runT, [
      [0, 0.84, 0],
      [0.022, 0.94, 0],
      [0, 0.84, 0],
      [-0.022, 0.94, 0],
      [0, 0.84, 0],
    ]),
    quatTrack('Hips', runT, [
      [0.14, 0.13, 0.03],
      [0.12, 0, -0.03],
      [0.14, -0.13, -0.03],
      [0.12, 0, 0.03],
      [0.14, 0.13, 0.03],
    ]),
    quatTrack('Spine', runT, [
      [0.12, -0.1, 0],
      [0.1, 0, 0],
      [0.12, 0.1, 0],
      [0.1, 0, 0],
      [0.12, -0.1, 0],
    ]),
    quatTrack('Chest', runT, [
      [0.08, -0.14, 0],
      [0.06, 0, 0],
      [0.08, 0.14, 0],
      [0.06, 0, 0],
      [0.08, -0.14, 0],
    ]),
    quatTrack('Head', runT, [
      [-0.18, 0, 0],
      [-0.14, 0, 0],
      [-0.18, 0, 0],
      [-0.14, 0, 0],
      [-0.18, 0, 0],
    ]),
    quatTrack('LeftThigh', runT, [
      [-0.88, 0, 0],
      [-0.12, 0, 0],
      [0.68, 0, 0],
      [-0.36, 0, 0],
      [-0.88, 0, 0],
    ]),
    quatTrack('LeftShin', runT, [
      [0.24, 0, 0],
      [0.34, 0, 0],
      [0.48, 0, 0],
      [1.24, 0, 0],
      [0.24, 0, 0],
    ]),
    quatTrack('RightThigh', runT, [
      [0.68, 0, 0],
      [-0.36, 0, 0],
      [-0.88, 0, 0],
      [-0.12, 0, 0],
      [0.68, 0, 0],
    ]),
    quatTrack('RightShin', runT, [
      [0.48, 0, 0],
      [1.24, 0, 0],
      [0.24, 0, 0],
      [0.34, 0, 0],
      [0.48, 0, 0],
    ]),
    quatTrack('LeftUpperArm', runT, [
      [0.68, 0, -0.12],
      [0.04, 0, -0.12],
      [-0.82, 0, -0.12],
      [-0.08, 0, -0.12],
      [0.68, 0, -0.12],
    ]),
    quatTrack('LeftForearm', runT, [
      [-1.05, 0, 0],
      [-1.18, 0, 0],
      [-1.26, 0, 0],
      [-1.1, 0, 0],
      [-1.05, 0, 0],
    ]),
    quatTrack('RightUpperArm', runT, [
      [-0.82, 0, 0.12],
      [-0.08, 0, 0.12],
      [0.68, 0, 0.12],
      [0.04, 0, 0.12],
      [-0.82, 0, 0.12],
    ]),
    quatTrack('RightForearm', runT, [
      [-1.26, 0, 0],
      [-1.1, 0, 0],
      [-1.05, 0, 0],
      [-1.18, 0, 0],
      [-1.26, 0, 0],
    ]),
  ]);

  // 4. ATTACK (0.42s one-shot — snappy forward punch / weapon strike + torso snap)
  const atkT = [0, 0.08, 0.18, 0.3, 0.42];
  const attackClip = new THREE.AnimationClip('Attack', 0.42, [
    quatTrack('Spine', atkT, [
      [0.04, -0.22, 0],
      [0.12, -0.32, 0],
      [0.14, 0.38, 0],
      [0.08, 0.18, 0],
      [0.02, 0, 0],
    ]),
    quatTrack('Chest', atkT, [
      [0.04, -0.24, 0],
      [0.1, -0.35, 0],
      [0.12, 0.44, 0],
      [0.06, 0.2, 0],
      [0.01, 0, 0],
    ]),
    quatTrack('RightUpperArm', atkT, [
      [-0.35, 0, 0.25],
      [0.32, 0.25, 0.36],
      [-1.48, -0.18, 0.12],
      [-1.15, -0.1, 0.12],
      [-0.25, 0, 0.1],
    ]),
    quatTrack('RightForearm', atkT, [
      [-0.75, 0, 0],
      [-1.28, 0, 0],
      [-0.14, 0, 0],
      [-0.45, 0, 0],
      [-0.2, 0, 0],
    ]),
    quatTrack('LeftUpperArm', atkT, [
      [-0.45, 0, -0.22],
      [-0.75, 0.2, -0.28],
      [0.24, 0, -0.24],
      [-0.22, 0, -0.16],
      [0.04, 0, -0.09],
    ]),
    quatTrack('LeftForearm', atkT, [
      [-0.9, 0, 0],
      [-1.2, 0, 0],
      [-0.85, 0, 0],
      [-0.5, 0, 0],
      [-0.18, 0, 0],
    ]),
  ]);

  // 5. HIT (0.36s one-shot — crisp impact recoil flinch)
  const hitT = [0, 0.09, 0.22, 0.36];
  const hitClip = new THREE.AnimationClip('Hit', 0.36, [
    posTrack('Hips', hitT, [
      [0, 0.9, 0],
      [0, 0.86, -0.1],
      [0, 0.88, -0.04],
      [0, 0.9, 0],
    ]),
    quatTrack('Spine', hitT, [
      [0, 0, 0],
      [-0.32, 0.14, -0.08],
      [-0.14, 0.06, -0.03],
      [0, 0, 0],
    ]),
    quatTrack('Head', hitT, [
      [0, 0, 0],
      [-0.36, -0.16, 0.08],
      [-0.12, -0.05, 0.02],
      [0, 0, 0],
    ]),
    quatTrack('LeftUpperArm', hitT, [
      [0.04, 0, -0.09],
      [-0.48, 0, -0.45],
      [-0.18, 0, -0.22],
      [0.04, 0, -0.09],
    ]),
    quatTrack('RightUpperArm', hitT, [
      [0.04, 0, 0.09],
      [-0.48, 0, 0.45],
      [-0.18, 0, 0.22],
      [0.04, 0, 0.09],
    ]),
  ]);

  // 6. KO (0.52s clamp — clean backward fall onto Dhaka pavement)
  const koT = [0, 0.18, 0.36, 0.52];
  const koClip = new THREE.AnimationClip('KO', 0.52, [
    posTrack('Hips', koT, [
      [0, 0.9, 0],
      [0, 0.72, -0.28],
      [0, 0.32, -0.62],
      [0, 0.16, -0.76],
    ]),
    quatTrack('Hips', koT, [
      [0, 0, 0],
      [-0.55, 0.08, 0.05],
      [-1.28, 0.1, 0.08],
      [-1.52, 0.1, 0.08],
    ]),
    quatTrack('LeftUpperArm', koT, [
      [0.04, 0, -0.09],
      [-0.6, 0, -0.55],
      [-0.2, 0, -0.95],
      [-0.08, 0, -1.12],
    ]),
    quatTrack('RightUpperArm', koT, [
      [0.04, 0, 0.09],
      [-0.5, 0, 0.55],
      [-0.15, 0, 0.9],
      [-0.05, 0, 1.05],
    ]),
    quatTrack('LeftThigh', koT, [
      [0, 0, 0],
      [-0.25, 0, -0.1],
      [-0.1, 0, -0.18],
      [0.05, 0, -0.2],
    ]),
    quatTrack('RightThigh', koT, [
      [0, 0, 0],
      [-0.15, 0, 0.08],
      [0.05, 0, 0.15],
      [0.08, 0, 0.18],
    ]),
  ]);

  return [idleClip, walkClip, runClip, attackClip, hitClip, koClip];
}

async function main(): Promise<void> {
  const scene = new THREE.Scene();
  scene.name = 'DhakaCharacterScene';

  const characterRoot = new THREE.Group();
  characterRoot.name = 'DhakaCharacterRoot';
  scene.add(characterRoot);

  const { rootBone, skeleton } = createSkeletonHierarchy();
  characterRoot.add(rootBone);

  const parts = buildSculptedParts();

  // Named PBR materials so runtime assetLoader can cleanly tint garments per character/outfit
  const torsoMat = new THREE.MeshStandardMaterial({ name: 'TorsoMat', color: 0x26466d, roughness: 0.72, metalness: 0.06 });
  const pantsMat = new THREE.MeshStandardMaterial({ name: 'PantsMat', color: 0x1e2530, roughness: 0.82, metalness: 0.04 });
  const accentMat = new THREE.MeshStandardMaterial({ name: 'AccentMat', color: 0xe6a135, roughness: 0.46, metalness: 0.22 });
  const skinMat = new THREE.MeshStandardMaterial({ name: 'SkinMat', color: 0xb9805a, roughness: 0.74, metalness: 0.02 });
  const hairMat = new THREE.MeshStandardMaterial({ name: 'HairMat', color: 0x16161a, roughness: 0.86, metalness: 0.06 });
  const shoeMat = new THREE.MeshStandardMaterial({ name: 'ShoeMat', color: 0x1a1c20, roughness: 0.68, metalness: 0.08 });

  const meshDefs: Array<[string, THREE.BufferGeometry, THREE.MeshStandardMaterial]> = [
    ['TorsoMesh', parts.torsoGeo, torsoMat],
    ['PantsMesh', parts.pantsGeo, pantsMat],
    ['AccentMesh', parts.accentGeo, accentMat],
    ['SkinMesh', parts.skinGeo, skinMat],
    ['HairMesh', parts.hairGeo, hairMat],
    ['ShoeMesh', parts.shoeGeo, shoeMat],
  ];

  for (const [name, geo, mat] of meshDefs) {
    const sm = new THREE.SkinnedMesh(geo, mat);
    sm.name = name;
    sm.bind(skeleton);
    characterRoot.add(sm);
  }

  const animations = buildAnimationClips();
  const exporter = new GLTFExporter();
  const glb = await exporter.parseAsync(scene, {
    binary: true,
    animations,
  });

  if (!(glb instanceof ArrayBuffer)) {
    throw new Error('Expected binary ArrayBuffer from GLTFExporter');
  }

  const outDir = path.resolve(process.cwd(), 'public/models');
  fs.mkdirSync(outDir, { recursive: true });
  const outPath = path.join(outDir, 'dhaka_character.glb');
  fs.writeFileSync(outPath, Buffer.from(glb));

  const bytes = fs.statSync(outPath).size;
  console.log(
    `Wrote ${outPath} (${(bytes / 1024).toFixed(1)} KB) with ${animations.length} clips: ${animations.map((a) => a.name).join(', ')}`,
  );
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
