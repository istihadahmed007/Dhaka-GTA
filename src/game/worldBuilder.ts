import * as THREE from 'three';
import {
  BUILDINGS,
  BuildingSpec,
  FLYOVER_PILLARS,
  POIS,
  RIVER_Z_START,
  ROAD_SEGMENTS,
} from './worldData';
import { createBurigangaBoat, createShaplaMonument } from './models';
import { createSignboardTexture, DhakaTextureSet } from './textures';

export interface WorldVisualHandles {
  root: THREE.Group;
  riverMesh: THREE.Mesh;
  boats: Array<{ group: THREE.Group; baseX: number; baseZ: number; phase: number }>;
  lampGlowMaterials: THREE.MeshStandardMaterial[];
  signMaterials: THREE.MeshStandardMaterial[];
  missionBeacon: THREE.Group;
  fareBeacon: THREE.Group;
  poiRings: Map<string, THREE.Mesh>;
  rainPoints: THREE.Points;
  rainPositions: Float32Array;
  cameraOccluders: THREE.Object3D[];
  disposables: Array<{ dispose: () => void }>;
}

export function buildDhakaWorld(
  scene: THREE.Scene,
  textures: DhakaTextureSet,
  castShadow: boolean,
): WorldVisualHandles {
  const root = new THREE.Group();
  scene.add(root);

  const disposables: Array<{ dispose: () => void }> = [];
  const lampGlowMaterials: THREE.MeshStandardMaterial[] = [];
  const signMaterials: THREE.MeshStandardMaterial[] = [];
  const cameraOccluders: THREE.Object3D[] = [];
  const poiRings = new Map<string, THREE.Mesh>();

  // 1. Extended Ground & Sidewalk Base (extends far beyond city bounds into horizon fog)
  const farGroundGeo = new THREE.PlaneGeometry(760, 760);
  farGroundGeo.rotateX(-Math.PI / 2);
  const farGroundMat = new THREE.MeshStandardMaterial({
    color: 0x3b4d3b,
    roughness: 0.95,
    metalness: 0.0,
  });
  const farGround = new THREE.Mesh(farGroundGeo, farGroundMat);
  farGround.position.set(0, -0.06, -40);
  farGround.receiveShadow = true;
  root.add(farGround);
  disposables.push(farGroundGeo, farGroundMat);

  // Urban concrete city pad (north of Buriganga river)
  const cityPadGeo = new THREE.PlaneGeometry(236, 200);
  cityPadGeo.rotateX(-Math.PI / 2);
  const cityPadMat = new THREE.MeshStandardMaterial({
    map: textures.sidewalkMap,
    color: 0xffffff,
    roughness: 0.88,
    metalness: 0.04,
  });
  const cityPad = new THREE.Mesh(cityPadGeo, cityPadMat);
  cityPad.position.set(0, 0.0, -20);
  cityPad.receiveShadow = true;
  root.add(cityPad);
  disposables.push(cityPadGeo, cityPadMat);

  // 2. Buriganga River (south of z = 80) + Sadarghat Concrete Pier Wall + Far Bank
  const embankmentGeo = new THREE.BoxGeometry(260, 1.4, 2.8);
  const embankmentMat = new THREE.MeshStandardMaterial({ color: 0x7d766c, roughness: 0.9 });
  const embankment = new THREE.Mesh(embankmentGeo, embankmentMat);
  embankment.position.set(0, -0.45, RIVER_Z_START - 0.8);
  embankment.receiveShadow = true;
  root.add(embankment);
  disposables.push(embankmentGeo, embankmentMat);

  const riverGeo = new THREE.PlaneGeometry(760, 260, 24, 12);
  riverGeo.rotateX(-Math.PI / 2);
  const riverMat = new THREE.MeshStandardMaterial({
    color: 0x164e63,
    roughness: 0.18,
    metalness: 0.45,
  });
  const riverMesh = new THREE.Mesh(riverGeo, riverMat);
  riverMesh.position.set(0, -0.95, RIVER_Z_START + 130);
  root.add(riverMesh);
  disposables.push(riverGeo, riverMat);

  // Far Keraniganj river bank silhouette across the Buriganga
  const farBankGeo = new THREE.BoxGeometry(640, 4.5, 45);
  const farBankMat = new THREE.MeshStandardMaterial({ color: 0x2d4a3e, roughness: 0.95 });
  const farBank = new THREE.Mesh(farBankGeo, farBankMat);
  farBank.position.set(0, 0.8, RIVER_Z_START + 145);
  root.add(farBank);
  disposables.push(farBankGeo, farBankMat);

  // Wooden Buriganga Boats (Noukas) moored and drifting near Sadarghat
  const boats: Array<{ group: THREE.Group; baseX: number; baseZ: number; phase: number }> = [];
  const boatCoords: Array<[number, number, number]> = [
    [-32, 86, 0.4],
    [-14, 88, 1.8],
    [12, 85.5, 3.1],
    [34, 90, 4.5],
    [58, 87, 0.9],
    [-55, 89, 2.4],
  ];
  boatCoords.forEach(([bx, bz, ph]) => {
    const boat = createBurigangaBoat();
    boat.position.set(bx, -0.95, bz);
    boat.rotation.y = Math.PI * 0.45 + (ph % 0.6) - 0.3;
    root.add(boat);
    boats.push({ group: boat, baseX: bx, baseZ: bz, phase: ph });
  });

  // 3. Asphalt Road Network (offset at y = 0.03 to eliminate coplanar Z-fighting)
  ROAD_SEGMENTS.forEach((seg, idx) => {
    const dx = seg.x2 - seg.x1;
    const dz = seg.z2 - seg.z1;
    const len = Math.hypot(dx, dz);
    const roadTex = textures.asphaltMap.clone();
    roadTex.needsUpdate = true;
    roadTex.repeat.set(1, Math.max(1, Math.round(len / 14)));
    disposables.push(roadTex);

    const roadGeo = new THREE.PlaneGeometry(seg.width, len);
    roadGeo.rotateX(-Math.PI / 2);
    const roadMat = new THREE.MeshStandardMaterial({
      map: roadTex,
      bumpMap: textures.asphaltBump,
      bumpScale: 0.04,
      color: 0xffffff,
      roughness: 0.78,
      metalness: 0.06,
      polygonOffset: true,
      polygonOffsetFactor: -1,
      polygonOffsetUnits: -1,
    });
    const mesh = new THREE.Mesh(roadGeo, roadMat);
    mesh.position.set((seg.x1 + seg.x2) / 2, 0.03 + idx * 0.002, (seg.z1 + seg.z2) / 2);
    mesh.rotation.y = Math.atan2(dx, dz);
    mesh.receiveShadow = true;
    root.add(mesh);
    disposables.push(roadGeo, roadMat);
  });

  // Central Roundabout Asphalt Ring around Shapla Monument
  const roundGeo = new THREE.RingGeometry(4.4, 13.5, 36);
  roundGeo.rotateX(-Math.PI / 2);
  const roundMat = new THREE.MeshStandardMaterial({
    color: 0x2f343c,
    roughness: 0.78,
    metalness: 0.05,
  });
  const roundMesh = new THREE.Mesh(roundGeo, roundMat);
  roundMesh.position.y = 0.045;
  roundMesh.receiveShadow = true;
  root.add(roundMesh);
  disposables.push(roundGeo, roundMat);

  // Iconic Shapla Monument Hero Landmark at (0, 0)
  const shapla = createShaplaMonument();
  shapla.position.set(0, 0.05, 0);
  if (castShadow) {
    shapla.traverse((o) => {
      if ((o as THREE.Mesh).isMesh) {
        o.castShadow = true;
        o.receiveShadow = true;
      }
    });
  }
  root.add(shapla);

  // 4. Composite Dhaka Buildings (Old Dhaka, Gulshan Towers, DMP Thana, Garage, Shop)
  const parapetMat = new THREE.MeshStandardMaterial({ color: 0x5c5852, roughness: 0.85 });
  const tankMat = new THREE.MeshStandardMaterial({ color: 0x161b22, roughness: 0.45, metalness: 0.15 });
  const tankBlueMat = new THREE.MeshStandardMaterial({ color: 0x0369a1, roughness: 0.4, metalness: 0.15 });
  disposables.push(parapetMat, tankMat, tankBlueMat);

  const buildArchitecture = (b: BuildingSpec): void => {
    const grp = new THREE.Group();
    grp.position.set(b.x, 0, b.z);

    let mapTex: THREE.Texture | null = null;
    let bumpTex: THREE.Texture | null = null;
    let roughness = 0.78;
    let metalness = 0.08;

    if (b.style === 'old_dhaka' || b.style === 'shop') {
      mapTex = textures.brickFacadeMap;
      bumpTex = textures.brickBump;
      roughness = 0.85;
    } else if (b.style === 'gulshan_tower') {
      mapTex = textures.glassTowerMap;
      roughness = 0.18;
      metalness = 0.62;
    } else if (b.style === 'residential' || b.style === 'police') {
      mapTex = textures.residentialMap;
      roughness = 0.76;
    }

    const wallMat = new THREE.MeshStandardMaterial({
      color: b.style === 'gulshan_tower' ? 0xffffff : b.color,
      map: mapTex,
      bumpMap: bumpTex,
      bumpScale: bumpTex ? 0.08 : 0,
      roughness,
      metalness,
    });
    const accentMat = new THREE.MeshStandardMaterial({
      color: b.accentColor,
      roughness: 0.48,
      metalness: 0.22,
    });
    disposables.push(wallMat, accentMat);

    // Main architectural block
    const bodyGeo = new THREE.BoxGeometry(b.w, b.h, b.d);
    const body = new THREE.Mesh(bodyGeo, wallMat);
    body.position.y = b.h / 2;
    if (castShadow) {
      body.castShadow = true;
      body.receiveShadow = true;
    }
    grp.add(body);
    cameraOccluders.push(body);
    disposables.push(bodyGeo);

    // Ground-floor plinth / shopfront trim band
    const plinthGeo = new THREE.BoxGeometry(b.w + 0.3, 0.55, b.d + 0.3);
    const plinth = new THREE.Mesh(plinthGeo, accentMat);
    plinth.position.y = 3.6;
    grp.add(plinth);
    disposables.push(plinthGeo);

    // Roof cornice parapet
    const roofCapGeo = new THREE.BoxGeometry(b.w + 0.45, 0.6, b.d + 0.45);
    const roofCap = new THREE.Mesh(roofCapGeo, parapetMat);
    roofCap.position.y = b.h + 0.25;
    grp.add(roofCap);
    disposables.push(roofCapGeo);

    // Gulshan skyscraper mechanical penthouse crown
    if (b.style === 'gulshan_tower') {
      const crownGeo = new THREE.BoxGeometry(b.w * 0.55, 3.2, b.d * 0.55);
      const crown = new THREE.Mesh(crownGeo, accentMat);
      crown.position.y = b.h + 1.9;
      grp.add(crown);
      disposables.push(crownGeo);
    }

    // Corrugated tin shop awnings (Old Dhaka & Garage flavor)
    if (b.hasAwning) {
      const awningGeo = new THREE.BoxGeometry(b.w * 0.85, 0.16, 2.2);
      const awning = new THREE.Mesh(awningGeo, accentMat);
      const sideSign = b.z > 0 ? -1 : 1;
      awning.position.set(0, 3.3, sideSign * (b.d / 2 + 1.0));
      awning.rotation.x = sideSign * 0.24;
      grp.add(awning);
      disposables.push(awningGeo);
    }

    // Iconic Dhaka plastic rooftop water tanks (Gazipur black/blue cylinders)
    if (b.hasWaterTank) {
      const tankGeo = new THREE.CylinderGeometry(0.75, 0.8, 1.55, 10);
      const tank = new THREE.Mesh(tankGeo, b.id.length % 2 === 0 ? tankMat : tankBlueMat);
      tank.position.set(b.w * 0.24, b.h + 1.25, -b.d * 0.2);
      grp.add(tank);
      disposables.push(tankGeo);
    }

    // Illuminated bilingual Bengali + English signboard facing nearest street
    if (b.signText && b.signBangla) {
      const signTex = createSignboardTexture(b.signText, b.signBangla, 0x161c24, b.accentColor);
      const signMat = new THREE.MeshStandardMaterial({
        map: signTex,
        emissiveMap: signTex,
        emissive: 0xffffff,
        emissiveIntensity: 0.45,
        roughness: 0.3,
      });
      signMaterials.push(signMat);
      disposables.push(signTex, signMat);

      const signGeo = new THREE.BoxGeometry(Math.min(b.w * 0.78, 8.6), 1.85, 0.24);
      const signMesh = new THREE.Mesh(signGeo, signMat);
      const faceFrontZ = b.z > 0 ? -(b.d / 2 + 0.18) : b.d / 2 + 0.18;
      signMesh.position.set(0, Math.min(b.h - 1.2, 5.2), faceFrontZ);
      if (b.z > 0) signMesh.rotation.y = Math.PI;
      grp.add(signMesh);
      disposables.push(signGeo);
    }

    root.add(grp);
  };

  BUILDINGS.forEach(buildArchitecture);

  // 5. Elevated Dhaka Express Flyover (Kawran Bazar / West Skyline)
  const flyoverConcreteMat = new THREE.MeshStandardMaterial({ color: 0x8a8e94, roughness: 0.82 });
  const flyoverDeckMat = new THREE.MeshStandardMaterial({ color: 0x3a3f47, roughness: 0.75 });
  const flyoverRailMat = new THREE.MeshStandardMaterial({ color: 0xf59e0b, roughness: 0.45, metalness: 0.35 });
  disposables.push(flyoverConcreteMat, flyoverDeckMat, flyoverRailMat);

  const deckGeo = new THREE.BoxGeometry(7.2, 1.35, 148);
  const deck = new THREE.Mesh(deckGeo, flyoverDeckMat);
  deck.position.set(-56.2, 9.6, -18);
  if (castShadow) {
    deck.castShadow = true;
    deck.receiveShadow = true;
  }
  root.add(deck);
  disposables.push(deckGeo);

  [-3.45, 3.45].forEach((rx) => {
    const railGeo = new THREE.BoxGeometry(0.32, 0.85, 148);
    const rail = new THREE.Mesh(railGeo, flyoverRailMat);
    rail.position.set(-56.2 + rx, 10.65, -18);
    root.add(rail);
    disposables.push(railGeo);
  });

  const pillarGeo = new THREE.CylinderGeometry(0.88, 1.05, 9.3, 12);
  const capGeo = new THREE.BoxGeometry(5.8, 0.9, 2.2);
  FLYOVER_PILLARS.forEach((p) => {
    const pillar = new THREE.Mesh(pillarGeo, flyoverConcreteMat);
    pillar.position.set(p.x, 4.65, p.z);
    if (castShadow) pillar.castShadow = true;
    const cap = new THREE.Mesh(capGeo, flyoverConcreteMat);
    cap.position.set(p.x, 8.9, p.z);
    root.add(pillar, cap);
  });
  disposables.push(pillarGeo, capGeo);

  // 6. Instanced Dhaka Trees (Royal Palms & Tropical Canopy Trees along avenues)
  const treePositions: Array<[number, number]> = [
    [-11, 24], [-11, 40], [-11, 54],
    [11, 24], [11, 40], [11, 54],
    [-11, -20], [-11, -40], [-11, -54],
    [11, -20], [11, -40], [11, -54],
    [24, 11], [44, 11], [-24, 11], [-44, 11],
    [24, -11], [44, -11], [-24, -11], [-44, -11],
    [-30, 74], [-12, 74], [14, 74], [32, 74], [52, 74], [-48, 74],
  ];

  const trunkGeo = new THREE.CylinderGeometry(0.2, 0.36, 4.7, 8);
  trunkGeo.translate(0, 2.35, 0);
  const trunkMat = new THREE.MeshStandardMaterial({ color: 0x5c4332, roughness: 0.92 });
  const canopyGeo = new THREE.DodecahedronGeometry(1.55, 1);
  const canopyMat = new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.84 });

  const trunkInst = new THREE.InstancedMesh(trunkGeo, trunkMat, treePositions.length);
  const clustersPerTree = 3;
  const canopyInst = new THREE.InstancedMesh(canopyGeo, canopyMat, treePositions.length * clustersPerTree);
  const dummy = new THREE.Object3D();
  const tint = new THREE.Color();

  const clusterOffsets: Array<[number, number, number, number]> = [
    [0, 5.55, 0, 1.15],
    [-1.05, 4.75, 0.65, 0.85],
    [1.0, 4.85, -0.58, 0.9],
  ];

  treePositions.forEach(([tx, tz], i) => {
    const s = 0.86 + ((i * 37) % 28) / 100;
    const rotY = i * 1.15;
    dummy.position.set(tx, 0, tz);
    dummy.rotation.set(0, rotY, 0);
    dummy.scale.set(s, s * (0.95 + (i % 3) * 0.08), s);
    dummy.updateMatrix();
    trunkInst.setMatrixAt(i, dummy.matrix);

    const cosR = Math.cos(rotY);
    const sinR = Math.sin(rotY);
    clusterOffsets.forEach(([ox, oy, oz, cs], ci) => {
      const idx = i * clustersPerTree + ci;
      const wx = tx + (ox * cosR - oz * sinR) * s;
      const wz = tz + (ox * sinR + oz * cosR) * s;
      dummy.position.set(wx, oy * s, wz);
      dummy.rotation.set(ci * 0.4, rotY + ci * 1.2, 0);
      dummy.scale.set(s * cs * 1.12, s * cs * 0.82, s * cs * 1.05);
      dummy.updateMatrix();
      canopyInst.setMatrixAt(idx, dummy.matrix);
      tint.setHSL(0.35 + ((i + ci) % 5 - 2) * 0.014, 0.46, 0.25 + ci * 0.045 + (i % 2) * 0.03);
      canopyInst.setColorAt(idx, tint);
    });
  });
  trunkInst.instanceMatrix.needsUpdate = true;
  canopyInst.instanceMatrix.needsUpdate = true;
  if (canopyInst.instanceColor) canopyInst.instanceColor.needsUpdate = true;
  trunkInst.computeBoundingSphere();
  canopyInst.computeBoundingSphere();
  if (castShadow) {
    trunkInst.castShadow = true;
    canopyInst.castShadow = true;
  }
  root.add(trunkInst, canopyInst);
  disposables.push(trunkGeo, trunkMat, canopyGeo, canopyMat);

  // 7. Dhaka Sodium Streetlamps (Instanced poles + glowing bulb heads that brighten at night)
  const lampCoords: Array<[number, number, number]> = [
    [-9.2, 18, 0], [9.2, 18, Math.PI],
    [-9.2, 48, 0], [9.2, 48, Math.PI],
    [-9.2, -26, 0], [9.2, -26, Math.PI],
    [-9.2, -52, 0], [9.2, -52, Math.PI],
    [28, -9.2, -Math.PI / 2], [-28, -9.2, -Math.PI / 2],
    [28, 9.2, Math.PI / 2], [-28, 9.2, Math.PI / 2],
  ];
  const poleMat = new THREE.MeshStandardMaterial({ color: 0x334155, roughness: 0.5, metalness: 0.7 });
  const bulbMat = new THREE.MeshStandardMaterial({
    color: 0xffd580,
    emissive: 0xffa726,
    emissiveIntensity: 0.4,
    roughness: 0.2,
  });
  lampGlowMaterials.push(bulbMat);
  const poleGeo = new THREE.CylinderGeometry(0.1, 0.14, 6.8, 8);
  const armGeo = new THREE.BoxGeometry(1.5, 0.12, 0.12);
  const bulbGeo = new THREE.BoxGeometry(0.55, 0.18, 0.32);

  lampCoords.forEach(([lx, lz, rot]) => {
    const lg = new THREE.Group();
    lg.position.set(lx, 0, lz);
    lg.rotation.y = rot;
    const pole = new THREE.Mesh(poleGeo, poleMat);
    pole.position.y = 3.4;
    const arm = new THREE.Mesh(armGeo, poleMat);
    arm.position.set(0.7, 6.75, 0);
    const bulb = new THREE.Mesh(bulbGeo, bulbMat);
    bulb.position.set(1.3, 6.62, 0);
    lg.add(pole, arm, bulb);
    root.add(lg);
  });
  disposables.push(poleMat, bulbMat, poleGeo, armGeo, bulbGeo);

  // 8. Interactive POI Ground Markers (Garage, Babu's Shop, Tea Stall)
  POIS.forEach((poi) => {
    if (poi.id !== 'garage' && poi.id !== 'shop') return;
    const color = poi.id === 'garage' ? 0xf59e0b : 0x10b981;
    const rGeo = new THREE.RingGeometry(2.1, 2.7, 28);
    rGeo.rotateX(-Math.PI / 2);
    const rMat = new THREE.MeshBasicMaterial({
      color,
      transparent: true,
      opacity: 0.78,
      side: THREE.DoubleSide,
      depthWrite: false,
    });
    const ring = new THREE.Mesh(rGeo, rMat);
    ring.position.set(poi.x, 0.08, poi.z);
    root.add(ring);
    poiRings.set(poi.id, ring);
    disposables.push(rGeo, rMat);
  });

  // 9. Active Mission & Fare Beacon Pillars
  const createBeacon = (colorHex: number): THREE.Group => {
    const g = new THREE.Group();
    const ringGeo = new THREE.RingGeometry(1.8, 2.5, 30);
    ringGeo.rotateX(-Math.PI / 2);
    const ringMat = new THREE.MeshBasicMaterial({
      color: colorHex,
      transparent: true,
      opacity: 0.85,
      side: THREE.DoubleSide,
      depthWrite: false,
    });
    const ring = new THREE.Mesh(ringGeo, ringMat);
    ring.position.y = 0.09;

    const beamGeo = new THREE.CylinderGeometry(0.45, 1.15, 22, 12, 1, true);
    const beamMat = new THREE.MeshBasicMaterial({
      color: colorHex,
      transparent: true,
      opacity: 0.22,
      side: THREE.DoubleSide,
      depthWrite: false,
    });
    const beam = new THREE.Mesh(beamGeo, beamMat);
    beam.position.y = 11;
    g.add(ring, beam);
    g.visible = false;
    disposables.push(ringGeo, ringMat, beamGeo, beamMat);
    return g;
  };

  const missionBeacon = createBeacon(0xfbbf24);
  const fareBeacon = createBeacon(0x22d3ee);
  root.add(missionBeacon, fareBeacon);

  // 10. Pooled Monsoon Rain Points (zero-GC buffer around camera)
  const rainCount = 900;
  const rainPositions = new Float32Array(rainCount * 3);
  for (let i = 0; i < rainCount; i++) {
    rainPositions[i * 3] = (Math.random() - 0.5) * 65;
    rainPositions[i * 3 + 1] = Math.random() * 28;
    rainPositions[i * 3 + 2] = (Math.random() - 0.5) * 65;
  }
  const rainGeo = new THREE.BufferGeometry();
  rainGeo.setAttribute('position', new THREE.BufferAttribute(rainPositions, 3));
  const rainMat = new THREE.PointsMaterial({
    color: 0xcbd5e1,
    size: 0.22,
    transparent: true,
    opacity: 0.65,
    depthWrite: false,
  });
  const rainPoints = new THREE.Points(rainGeo, rainMat);
  rainPoints.visible = false;
  rainPoints.frustumCulled = false;
  root.add(rainPoints);
  disposables.push(rainGeo, rainMat);

  return {
    root,
    riverMesh,
    boats,
    lampGlowMaterials,
    signMaterials,
    missionBeacon,
    fareBeacon,
    poiRings,
    rainPoints,
    rainPositions,
    cameraOccluders,
    disposables,
  };
}
