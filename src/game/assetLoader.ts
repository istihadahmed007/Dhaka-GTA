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

/**
 * Resolves an asset path against Vite's base URL (e.g. for GitHub Pages subpath deployment).
 */
export function getAssetUrl(relativePath: string): string {
  const base = (typeof import.meta !== 'undefined' && import.meta.env?.BASE_URL) || './';
  const cleanBase = base.endsWith('/') ? base : `${base}/`;
  const cleanPath = relativePath.startsWith('/') ? relativePath.slice(1) : relativePath;
  return `${cleanBase}${cleanPath}`;
}

let cachedTemplatePromise: Promise<DhakaCharacterGlbTemplate | null> | null = null;
let cachedTemplate: DhakaCharacterGlbTemplate | null = null;

/**
 * Asynchronously loads and caches the rigged GLB character template via Three.js GLTFLoader.
 * Returns null cleanly on network/parse error so the game seamlessly falls back to procedural rigs.
 */
export function loadDhakaCharacterTemplate(
  url = getAssetUrl('models/dhaka_character.glb'),
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

export interface DhakaSedanGlbTemplate {
  scene: THREE.Object3D;
}

export interface InstantiatedGlbSedan {
  root: THREE.Group;
  chassis: THREE.Object3D;
  wheels: THREE.Object3D[];
  steerNodes: THREE.Object3D[];
  steeringWheel: THREE.Object3D | null;
  bodyMaterials: THREE.MeshStandardMaterial[];
  headlights: THREE.MeshStandardMaterial[];
  brakeLights: THREE.MeshStandardMaterial[];
  reverseLights: THREE.MeshStandardMaterial[];
}

let cachedSedanTemplatePromise: Promise<DhakaSedanGlbTemplate | null> | null = null;
let cachedSedanTemplate: DhakaSedanGlbTemplate | null = null;

/**
 * Asynchronously loads and caches the high-quality luxury sports sedan GLB model.
 */
export function loadDhakaSedanTemplate(
  url = getAssetUrl('models/dhaka_sedan.glb'),
  timeoutMs = 8000,
): Promise<DhakaSedanGlbTemplate | null> {
  if (cachedSedanTemplate) return Promise.resolve(cachedSedanTemplate);
  if (cachedSedanTemplatePromise) return cachedSedanTemplatePromise;

  cachedSedanTemplatePromise = new Promise<DhakaSedanGlbTemplate | null>((resolve) => {
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
        if (!gltf?.scene) {
          resolve(null);
          return;
        }

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

        cachedSedanTemplate = { scene: gltf.scene };
        resolve(cachedSedanTemplate);
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

  return cachedSedanTemplatePromise;
}

export function getCachedDhakaSedanTemplate(): DhakaSedanGlbTemplate | null {
  return cachedSedanTemplate;
}

/**
 * Clones the luxury sedan template, assigning a custom paint color and
 * extracting its functional suspension chassis, steering, and light nodes.
 */
export function instantiateGlbSedan(
  template: DhakaSedanGlbTemplate,
  paintColorHex: number,
  castShadow = true,
): InstantiatedGlbSedan {
  const root = template.scene.clone(true) as THREE.Group;

  const bodyMaterials: THREE.MeshStandardMaterial[] = [];
  const headlights: THREE.MeshStandardMaterial[] = [];
  const brakeLights: THREE.MeshStandardMaterial[] = [];
  const reverseLights: THREE.MeshStandardMaterial[] = [];

  const paintMat = new THREE.MeshStandardMaterial({
    name: 'Sedan_PaintMat_Instance',
    color: paintColorHex,
    roughness: 0.18,
    metalness: 0.82,
  });
  bodyMaterials.push(paintMat);

  root.traverse((obj) => {
    const mesh = obj as THREE.Mesh;
    if (!mesh.isMesh) return;
    if (castShadow) {
      mesh.castShadow = true;
      mesh.receiveShadow = true;
    }

    const mat = mesh.material as THREE.MeshStandardMaterial;
    const matName = (mat?.name || '').toLowerCase();
    const meshName = (mesh.name || '').toLowerCase();

    if (matName.includes('paint') || meshName.includes('paint') || meshName.includes('body_')) {
      mesh.material = paintMat;
    } else if (matName.includes('headlight') || meshName.includes('headlight')) {
      const clonedHl = mat.clone();
      mesh.material = clonedHl;
      headlights.push(clonedHl);
    } else if (matName.includes('brakelight') || meshName.includes('brakelight')) {
      const clonedBrake = mat.clone();
      mesh.material = clonedBrake;
      brakeLights.push(clonedBrake);
    } else if (matName.includes('reverselight') || meshName.includes('reverselight')) {
      const clonedRev = mat.clone();
      mesh.material = clonedRev;
      reverseLights.push(clonedRev);
    }
  });

  const chassis = root.getObjectByName('Chassis') ?? root;

  // Find front steering pivot nodes
  const steerNodes: THREE.Object3D[] = [];
  const steerFL = root.getObjectByName('Steer_FL');
  const steerFR = root.getObjectByName('Steer_FR');
  if (steerFL) steerNodes.push(steerFL);
  if (steerFR) steerNodes.push(steerFR);

  // Find all 4 wheel spin nodes
  const wheels: THREE.Object3D[] = [];
  const wheelFL = root.getObjectByName('Wheel_FL');
  const wheelFR = root.getObjectByName('Wheel_FR');
  const wheelRL = root.getObjectByName('Wheel_RL');
  const wheelRR = root.getObjectByName('Wheel_RR');
  if (wheelFL) wheels.push(wheelFL);
  if (wheelFR) wheels.push(wheelFR);
  if (wheelRL) wheels.push(wheelRL);
  if (wheelRR) wheels.push(wheelRR);

  const steeringWheel = root.getObjectByName('SteeringWheel') ?? null;

  return {
    root,
    chassis,
    wheels,
    steerNodes,
    steeringWheel,
    bodyMaterials,
    headlights,
    brakeLights,
    reverseLights,
  };
}

export interface DhakaLandmarkGlbTemplate {
  scene: THREE.Object3D;
}

let cachedLandmarkTemplatePromise: Promise<DhakaLandmarkGlbTemplate | null> | null = null;
let cachedLandmarkTemplate: DhakaLandmarkGlbTemplate | null = null;

/**
 * Asynchronously loads and caches the high-fidelity Ahsan Manzil GLB landmark model.
 * Returns null gracefully on error so procedural fallback is 100% seamless.
 */
export function loadDhakaLandmarkTemplate(
  url = getAssetUrl('models/dhaka_landmark_ahsan_manzil.glb'),
  timeoutMs = 8000,
): Promise<DhakaLandmarkGlbTemplate | null> {
  if (cachedLandmarkTemplate) return Promise.resolve(cachedLandmarkTemplate);
  if (cachedLandmarkTemplatePromise) return cachedLandmarkTemplatePromise;

  cachedLandmarkTemplatePromise = new Promise<DhakaLandmarkGlbTemplate | null>((resolve) => {
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
        if (!gltf?.scene) {
          resolve(null);
          return;
        }

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

        cachedLandmarkTemplate = { scene: gltf.scene };
        resolve(cachedLandmarkTemplate);
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

  return cachedLandmarkTemplatePromise;
}

export function getCachedDhakaLandmarkTemplate(): DhakaLandmarkGlbTemplate | null {
  return cachedLandmarkTemplate;
}

/**
 * Instantiates a clone of the Ahsan Manzil landmark GLB.
 */
export function instantiateGlbLandmark(
  template: DhakaLandmarkGlbTemplate,
  castShadow = true,
): THREE.Group {
  const root = template.scene.clone(true) as THREE.Group;
  if (castShadow) {
    root.traverse((obj) => {
      const mesh = obj as THREE.Mesh;
      if (mesh.isMesh) {
        mesh.castShadow = true;
        mesh.receiveShadow = true;
      }
    });
  }
  return root;
}
