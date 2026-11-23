import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import * as SkeletonUtils from 'three/addons/utils/SkeletonUtils.js';

export interface DhakaCharacterGlbTemplate {
  scene: THREE.Object3D;
  animations: THREE.AnimationClip[];
}

export interface InstantiatedGlbCharacter {
  root: THREE.Object3D;
  torsoMat: THREE.MeshStandardMaterial;
  pantsMat: THREE.MeshStandardMaterial;
  accentMat: THREE.MeshStandardMaterial;
  rightHandBone: THREE.Object3D | null;
  headBone: THREE.Object3D | null;
  chestBone: THREE.Object3D | null;
}

let cachedTemplatePromise: Promise<DhakaCharacterGlbTemplate | null> | null = null;
let cachedTemplate: DhakaCharacterGlbTemplate | null = null;

/**
 * Asynchronously loads and caches the rigged GLB character template via Three.js GLTFLoader.
 * Returns null cleanly on network/parse error so the game seamlessly falls back to procedural rigs.
 */
export function loadDhakaCharacterTemplate(
  url = '/models/dhaka_character.glb',
  timeoutMs = 8000,
): Promise<DhakaCharacterGlbTemplate | null> {
  if (cachedTemplate) return Promise.resolve(cachedTemplate);
  if (cachedTemplatePromise) return cachedTemplatePromise;

  cachedTemplatePromise = new Promise<DhakaCharacterGlbTemplate | null>((resolve) => {
    const loader = new GLTFLoader();
    let settled = false;

    const timer = window.setTimeout(() => {
      if (!settled) {
        settled = true;
        resolve(null);
      }
    }, timeoutMs);

    loader.load(
      url,
      (gltf) => {
        if (settled) return;
        settled = true;
        window.clearTimeout(timer);
        if (!gltf?.scene || !Array.isArray(gltf.animations) || gltf.animations.length === 0) {
          resolve(null);
          return;
        }
        // Tag any embedded albedo textures properly as sRGB
        gltf.scene.traverse((obj) => {
          const mesh = obj as THREE.Mesh;
          if (!mesh.isMesh) return;
          const mats = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
          for (const m of mats) {
            const std = m as THREE.MeshStandardMaterial;
            if (std?.map) std.map.colorSpace = THREE.SRGBColorSpace;
            if (std?.emissiveMap) std.emissiveMap.colorSpace = THREE.SRGBColorSpace;
          }
        });
        cachedTemplate = {
          scene: gltf.scene,
          animations: gltf.animations,
        };
        resolve(cachedTemplate);
      },
      undefined,
      () => {
        if (settled) return;
        settled = true;
        window.clearTimeout(timer);
        resolve(null);
      },
    );
  });

  return cachedTemplatePromise;
}

export function getCachedDhakaCharacterTemplate(): DhakaCharacterGlbTemplate | null {
  return cachedTemplate;
}

/**
 * Instantiates a skinned clone of the cached GLB template using SkeletonUtils.clone,
 * assigning lightweight per-character tinted garment materials (reusing shared geometries & clips).
 */
export function instantiateGlbCharacter(
  template: DhakaCharacterGlbTemplate,
  colors: {
    torsoColor: number;
    pantsColor: number;
    accentColor: number;
  },
  options: {
    fabricNormalMap?: THREE.Texture | null;
    castShadow?: boolean;
    isPolice?: boolean;
  } = {},
): InstantiatedGlbCharacter {
  const root = SkeletonUtils.clone(template.scene);

  const torsoMat = new THREE.MeshStandardMaterial({
    color: colors.torsoColor,
    roughness: 0.72,
    metalness: 0.06,
    normalMap: options.fabricNormalMap ?? null,
    normalScale: new THREE.Vector2(0.35, 0.35),
  });
  const pantsMat = new THREE.MeshStandardMaterial({
    color: colors.pantsColor,
    roughness: 0.82,
    metalness: 0.04,
    normalMap: options.fabricNormalMap ?? null,
    normalScale: new THREE.Vector2(0.28, 0.28),
  });
  const accentMat = new THREE.MeshStandardMaterial({
    color: colors.accentColor,
    roughness: 0.44,
    metalness: 0.24,
  });

  root.traverse((obj) => {
    const mesh = obj as THREE.SkinnedMesh;
    if (!mesh.isMesh) return;
    mesh.frustumCulled = false; // SkinnedMesh bounding spheres can clip when limbs animate
    if (options.castShadow) {
      mesh.castShadow = true;
      mesh.receiveShadow = true;
    }

    const origName = ((mesh.material as THREE.Material)?.name || mesh.name || '').toLowerCase();
    if (origName.includes('torso')) {
      mesh.material = torsoMat;
    } else if (origName.includes('pants')) {
      mesh.material = pantsMat;
    } else if (origName.includes('accent')) {
      mesh.material = accentMat;
    } else if (origName.includes('hair') && options.isPolice) {
      // Police wear navy cap tinted to torso uniform color
      mesh.material = torsoMat;
    }
  });

  const rightHandBone = root.getObjectByName('RightHand') ?? null;
  const headBone = root.getObjectByName('Head') ?? null;
  const chestBone = root.getObjectByName('Chest') ?? null;

  return {
    root,
    torsoMat,
    pantsMat,
    accentMat,
    rightHandBone,
    headBone,
    chestBone,
  };
}
