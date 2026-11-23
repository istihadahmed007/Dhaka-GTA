import * as THREE from 'three';
import {
  BUILDINGS,
  BuildingSpec,
  FLYOVER_PILLARS,
  LAKE_ZONES,
  POIS,
  RIVER_Z_START,
  ROAD_SEGMENTS,
} from './worldData';
import {
  createBurigangaBoat,
  createBusStopShelter,
  createOldDhakaGateArch,
  createOverheadGantrySign,
  createSadarghatLaunch,
  createShaplaMonument,
} from './models';
import { createHighwayGantryTexture, createSignboardTexture, DhakaTextureSet } from './textures';

export interface SkyDomeUniforms {
  uZenithColor: { value: THREE.Color };
  uHorizonColor: { value: THREE.Color };
  uSunColor: { value: THREE.Color };
  uSunDir: { value: THREE.Vector3 };
  uSunGlow: { value: number };
}

export interface WorldVisualHandles {
  root: THREE.Group;
  skyDome: THREE.Mesh;
  skyUniforms: SkyDomeUniforms;
  riverMesh: THREE.Mesh;
  riverMat: THREE.MeshStandardMaterial;
  waterMaterials: THREE.MeshStandardMaterial[];
  roadMaterials: THREE.MeshStandardMaterial[];
  nightWindowMaterials: THREE.MeshStandardMaterial[];
  lampHaloMat: THREE.MeshBasicMaterial;
  lampPoolMat: THREE.MeshBasicMaterial;
  haloInst: THREE.InstancedMesh;
  poolInst: THREE.InstancedMesh;
  boats: Array<{ group: THREE.Group; baseX: number; baseZ: number; phase: number; isLaunch?: boolean }>;
  lampGlowMaterials: THREE.MeshStandardMaterial[];
  signMaterials: THREE.MeshStandardMaterial[];
  missionBeacon: THREE.Group;
  fareBeacon: THREE.Group;
  poiRings: Map<string, THREE.Mesh>;
  rainPoints: THREE.Points;
  rainPositions: Float32Array;
  rainMaxCount: number;
  cameraOccluders: THREE.Object3D[];
  disposables: Array<{ dispose: () => void }>;
}

const SKY_VERTEX_SHADER = /* glsl */ `
  varying vec3 vLocalDir;
  void main() {
    vLocalDir = normalize(position);
    gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
  }
`;

const SKY_FRAGMENT_SHADER = /* glsl */ `
  uniform vec3 uZenithColor;
  uniform vec3 uHorizonColor;
  uniform vec3 uSunColor;
  uniform vec3 uSunDir;
  uniform float uSunGlow;
  varying vec3 vLocalDir;

  void main() {
    vec3 dir = normalize(vLocalDir);
    float elev = clamp(dir.y, 0.0, 1.0);

    // Tropical Dhaka vertical sky gradient with soft lower-horizon atmospheric haze
    float gradT = pow(elev, 0.52);
    vec3 sky = mix(uHorizonColor, uZenithColor, gradT);

    // Subtle wispy monsoon cloud-band modulation (cheap harmonic, zero texture lookup)
    float band = sin(dir.x * 5.0 + dir.z * 3.5) * cos(dir.z * 6.0 - dir.x * 2.5);
    float cloudMask = smoothstep(0.06, 0.42, elev) * (1.0 - smoothstep(0.55, 0.92, elev));
    sky += uHorizonColor * band * 0.035 * cloudMask;

    // Mie-like sun scatter + crisp solar disc
    float sunDot = max(dot(dir, normalize(uSunDir)), 0.0);
    float broadScatter = pow(sunDot, 7.0) * 0.34 * uSunGlow;
    float tightCorona = pow(sunDot, 48.0) * 0.52 * uSunGlow;
    float sunDisc = smoothstep(0.9968, 0.9993, sunDot) * 1.15 * uSunGlow;
    sky += uSunColor * (broadScatter + tightCorona + sunDisc);

    // Below-horizon bounce blend so distant river/ground transitions seamlessly
    float belowT = smoothstep(-0.18, 0.02, dir.y);
    vec3 finalColor = mix(uHorizonColor * 0.72, sky, belowT);

    gl_FragColor = vec4(finalColor, 1.0);
  }
`;

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
  const roadMaterials: THREE.MeshStandardMaterial[] = [];
  const nightWindowMaterials: THREE.MeshStandardMaterial[] = [];
  const waterMaterials: THREE.MeshStandardMaterial[] = [];
  const cameraOccluders: THREE.Object3D[] = [];
  const poiRings = new Map<string, THREE.Mesh>();

  const dummy = new THREE.Object3D();
  const tintColor = new THREE.Color();

  // 0. Procedural Atmospheric Dhaka Sky Dome (follows camera in engine.ts)
  const skyUniforms: SkyDomeUniforms = {
    uZenithColor: { value: new THREE.Color(0x3a7ec4) },
    uHorizonColor: { value: new THREE.Color(0xc9dceb) },
    uSunColor: { value: new THREE.Color(0xffe1a8) },
    uSunDir: { value: new THREE.Vector3(0.45, 0.65, 0.35).normalize() },
    uSunGlow: { value: 1.0 },
  };
  const skyGeo = new THREE.SphereGeometry(680, 24, 16);
  const skyMat = new THREE.ShaderMaterial({
    vertexShader: SKY_VERTEX_SHADER,
    fragmentShader: SKY_FRAGMENT_SHADER,
    uniforms: skyUniforms as unknown as Record<string, THREE.IUniform>,
    side: THREE.BackSide,
    depthWrite: false,
    fog: false,
  });
  const skyDome = new THREE.Mesh(skyGeo, skyMat);
  skyDome.frustumCulled = false;
  skyDome.renderOrder = -10;
  root.add(skyDome);
  disposables.push(skyGeo, skyMat);

  // 1. Extended Far Horizon Terrain & Expanded 2.5x Dhaka Concrete Urban Pad
  const farGroundGeo = new THREE.PlaneGeometry(1600, 1600);
  farGroundGeo.rotateX(-Math.PI / 2);
  const farGroundMat = new THREE.MeshStandardMaterial({
    color: 0x354735,
    roughness: 0.96,
    metalness: 0.0,
  });
  const farGround = new THREE.Mesh(farGroundGeo, farGroundMat);
  farGround.position.set(0, -0.08, -40);
  farGround.receiveShadow = true;
  root.add(farGround);
  disposables.push(farGroundGeo, farGroundMat);

  const padSidewalkTex = textures.sidewalkMap.clone();
  padSidewalkTex.needsUpdate = true;
  padSidewalkTex.repeat.set(44, 38);
  const padSidewalkNormal = textures.sidewalkNormal.clone();
  padSidewalkNormal.needsUpdate = true;
  padSidewalkNormal.repeat.set(44, 38);
  disposables.push(padSidewalkTex, padSidewalkNormal);

  const cityPadGeo = new THREE.PlaneGeometry(568, 490);
  cityPadGeo.rotateX(-Math.PI / 2);
  const cityPadMat = new THREE.MeshStandardMaterial({
    map: padSidewalkTex,
    normalMap: padSidewalkNormal,
    normalScale: new THREE.Vector2(0.55, 0.55),
    color: 0xeee8dc,
    roughness: 0.9,
    metalness: 0.04,
  });
  const cityPad = new THREE.Mesh(cityPadGeo, cityPadMat);
  cityPad.position.set(0, 0.0, -45);
  cityPad.receiveShadow = true;
  root.add(cityPad);
  disposables.push(cityPadGeo, cityPadMat);

  // 2. Expanded Sadarghat & Buriganga Riverfront (z = 200)
  const embankmentGeo = new THREE.BoxGeometry(590, 1.8, 3.6);
  const embankmentMat = new THREE.MeshStandardMaterial({ color: 0x777066, roughness: 0.9 });
  const embankment = new THREE.Mesh(embankmentGeo, embankmentMat);
  embankment.position.set(0, -0.55, RIVER_Z_START - 1.2);
  embankment.receiveShadow = true;
  root.add(embankment);
  disposables.push(embankmentGeo, embankmentMat);

  // Stepped Sadarghat riverfront ghat stairs & concrete jetties
  const jettyMat = new THREE.MeshStandardMaterial({ color: 0x645d54, roughness: 0.86 });
  const jettyGeo = new THREE.BoxGeometry(6.5, 0.7, 13.5);
  [-72, -26, 26, 78].forEach((jx) => {
    const jetty = new THREE.Mesh(jettyGeo, jettyMat);
    jetty.position.set(jx, -0.42, RIVER_Z_START + 6.5);
    root.add(jetty);
  });
  disposables.push(jettyMat, jettyGeo);

  // Sadarghat Waterfront Bollard Floodlights along river embankment
  const pierPostGeo = new THREE.CylinderGeometry(0.11, 0.15, 3.2, 6);
  pierPostGeo.translate(0, 1.6, 0);
  const pierPostMat = new THREE.MeshStandardMaterial({ color: 0x334155, roughness: 0.5, metalness: 0.65 });
  const pierHeadGeo = new THREE.SphereGeometry(0.26, 8, 6);
  const pierHeadMat = new THREE.MeshStandardMaterial({
    color: 0xffe09c,
    emissive: 0xffa726,
    emissiveIntensity: 0.55,
    roughness: 0.2,
  });
  lampGlowMaterials.push(pierHeadMat);
  const pierLightXs = [-160, -112, -72, -32, -16, 16, 32, 78, 120, 164];
  const pierPostInst = new THREE.InstancedMesh(pierPostGeo, pierPostMat, pierLightXs.length);
  const pierHeadInst = new THREE.InstancedMesh(pierHeadGeo, pierHeadMat, pierLightXs.length);
  pierLightXs.forEach((px, idx) => {
    dummy.position.set(px, 0.25, RIVER_Z_START - 0.5);
    dummy.rotation.set(0, 0, 0);
    dummy.scale.set(1, 1, 1);
    dummy.updateMatrix();
    pierPostInst.setMatrixAt(idx, dummy.matrix);
    dummy.position.set(px, 3.5, RIVER_Z_START - 0.5);
    dummy.updateMatrix();
    pierHeadInst.setMatrixAt(idx, dummy.matrix);
  });
  pierPostInst.instanceMatrix.needsUpdate = true;
  pierHeadInst.instanceMatrix.needsUpdate = true;
  pierPostInst.computeBoundingSphere();
  pierHeadInst.computeBoundingSphere();
  root.add(pierPostInst, pierHeadInst);
  disposables.push(pierPostGeo, pierPostMat, pierHeadGeo, pierHeadMat);

  // Buriganga River Water Surface with animated procedural ripple bump map
  const riverBump = textures.waterBumpMap.clone();
  riverBump.needsUpdate = true;
  riverBump.repeat.set(28, 9);
  disposables.push(riverBump);

  const riverGeo = new THREE.PlaneGeometry(1500, 380, 28, 14);
  riverGeo.rotateX(-Math.PI / 2);
  const riverMat = new THREE.MeshStandardMaterial({
    color: 0x11495f,
    bumpMap: riverBump,
    bumpScale: 0.14,
    roughness: 0.12,
    metalness: 0.54,
    emissive: 0x061f2c,
    emissiveIntensity: 0.08,
  });
  const riverMesh = new THREE.Mesh(riverGeo, riverMat);
  riverMesh.position.set(0, -1.05, RIVER_Z_START + 190);
  root.add(riverMesh);
  waterMaterials.push(riverMat);
  disposables.push(riverGeo, riverMat);

  // Far Keraniganj river bank + distant dockside warehouse skyline across Buriganga
  const farBankGeo = new THREE.BoxGeometry(1200, 5.5, 65);
  const farBankMat = new THREE.MeshStandardMaterial({ color: 0x2a4538, roughness: 0.95 });
  const farBank = new THREE.Mesh(farBankGeo, farBankMat);
  farBank.position.set(0, 1.1, RIVER_Z_START + 205);
  root.add(farBank);
  disposables.push(farBankGeo, farBankMat);

  const keraniCount = 34;
  const keraniBoxGeo = new THREE.BoxGeometry(1, 1, 1);
  keraniBoxGeo.translate(0, 0.5, 0);
  const keraniMat = new THREE.MeshStandardMaterial({
    color: 0x5c6872,
    emissive: 0xffe2a8,
    emissiveMap: textures.brickEmissiveMap,
    emissiveIntensity: 0.12,
    roughness: 0.88,
  });
  nightWindowMaterials.push(keraniMat);
  const keraniInst = new THREE.InstancedMesh(keraniBoxGeo, keraniMat, keraniCount);
  for (let i = 0; i < keraniCount; i++) {
    const kx = -440 + i * 26.5 + ((i * 17) % 9);
    const kw = 14 + ((i * 11) % 14);
    const kh = 9 + ((i * 23) % 20);
    const kd = 12 + ((i * 7) % 10);
    dummy.position.set(kx, 3.2, RIVER_Z_START + 186 + ((i * 13) % 18));
    dummy.rotation.set(0, 0, 0);
    dummy.scale.set(kw, kh, kd);
    dummy.updateMatrix();
    keraniInst.setMatrixAt(i, dummy.matrix);
    tintColor.setHSL(0.56 + ((i % 4) * 0.04), 0.12, 0.42 + (i % 3) * 0.08);
    keraniInst.setColorAt(i, tintColor);
  }
  keraniInst.instanceMatrix.needsUpdate = true;
  if (keraniInst.instanceColor) keraniInst.instanceColor.needsUpdate = true;
  keraniInst.computeBoundingSphere();
  root.add(keraniInst);
  disposables.push(keraniBoxGeo, keraniMat);

  // Iconic Buriganga Bridge extending south from Sadarghat Avenue (with glowing navigation beacons)
  const bridgeDeckGeo = new THREE.BoxGeometry(15, 1.4, 155);
  const bridgeDeckMat = new THREE.MeshStandardMaterial({ color: 0x3b424c, roughness: 0.78 });
  const bridgeArchMat = new THREE.MeshStandardMaterial({ color: 0xb91c1c, roughness: 0.42, metalness: 0.4 });
  const bridgePylonMat = new THREE.MeshStandardMaterial({ color: 0xd6d3cd, roughness: 0.75 });
  const bridgeBeaconMat = new THREE.MeshStandardMaterial({
    color: 0xff6b4a,
    emissive: 0xff3d24,
    emissiveIntensity: 0.6,
    roughness: 0.2,
  });
  lampGlowMaterials.push(bridgeBeaconMat);

  const bridgeDeck = new THREE.Mesh(bridgeDeckGeo, bridgeDeckMat);
  bridgeDeck.position.set(0, 2.2, RIVER_Z_START + 78);
  root.add(bridgeDeck);
  const pylonGeo = new THREE.BoxGeometry(1.4, 19, 2.2);
  const crossBeamGeo = new THREE.BoxGeometry(15.8, 1.5, 1.8);
  const beaconGeo = new THREE.SphereGeometry(0.42, 8, 6);
  [RIVER_Z_START + 26, RIVER_Z_START + 86, RIVER_Z_START + 142].forEach((bz) => {
    [-7.2, 7.2].forEach((bx) => {
      const pylon = new THREE.Mesh(pylonGeo, bridgePylonMat);
      pylon.position.set(bx, 7.5, bz);
      const beacon = new THREE.Mesh(beaconGeo, bridgeBeaconMat);
      beacon.position.set(bx, 17.5, bz);
      root.add(pylon, beacon);
    });
    const crossBeam = new THREE.Mesh(crossBeamGeo, bridgeArchMat);
    crossBeam.position.set(0, 15.8, bz);
    root.add(crossBeam);
  });
  disposables.push(
    bridgeDeckGeo,
    bridgeDeckMat,
    bridgeArchMat,
    bridgePylonMat,
    bridgeBeaconMat,
    pylonGeo,
    crossBeamGeo,
    beaconGeo,
  );

  // Moored Sadarghat Multi-Deck Passenger Launches + 10 Wooden Buriganga Boats (Noukas)
  const boats: Array<{ group: THREE.Group; baseX: number; baseZ: number; phase: number; isLaunch?: boolean }> = [];
  [
    [-36, RIVER_Z_START + 18, 0.5],
    [40, RIVER_Z_START + 20, 2.2],
  ].forEach(([lx, lz, ph]) => {
    const launch = createSadarghatLaunch();
    launch.position.set(lx, -1.0, lz);
    launch.rotation.y = Math.PI * 0.48;
    root.add(launch);
    boats.push({ group: launch, baseX: lx, baseZ: lz, phase: ph, isLaunch: true });
  });

  const boatCoords: Array<[number, number, number]> = [
    [-168, RIVER_Z_START + 12, 0.2],
    [-124, RIVER_Z_START + 16, 1.1],
    [-86, RIVER_Z_START + 10, 2.6],
    [-14, RIVER_Z_START + 14, 1.8],
    [14, RIVER_Z_START + 11, 3.1],
    [68, RIVER_Z_START + 15, 4.5],
    [108, RIVER_Z_START + 12, 0.9],
    [156, RIVER_Z_START + 17, 2.4],
    [-55, RIVER_Z_START + 28, 3.8],
    [92, RIVER_Z_START + 30, 5.1],
  ];
  boatCoords.forEach(([bx, bz, ph]) => {
    const boat = createBurigangaBoat();
    boat.position.set(bx, -1.0, bz);
    boat.rotation.y = Math.PI * 0.45 + (ph % 0.6) - 0.3;
    root.add(boat);
    boats.push({ group: boat, baseX: bx, baseZ: bz, phase: ph });
  });

  // 2b. Dhanmondi Lake (west) & Hatirjheel Lake (east-central with iconic white arch overpass)
  const lakeBump = textures.waterBumpMap.clone();
  lakeBump.needsUpdate = true;
  lakeBump.repeat.set(6, 4);
  disposables.push(lakeBump);

  const lakeRimMat = new THREE.MeshStandardMaterial({ color: 0x8c8478, roughness: 0.85 });
  const lakeWaterMat = new THREE.MeshStandardMaterial({
    color: 0x0d6b86,
    bumpMap: lakeBump,
    bumpScale: 0.1,
    roughness: 0.12,
    metalness: 0.46,
    emissive: 0x052430,
    emissiveIntensity: 0.07,
  });
  waterMaterials.push(lakeWaterMat);

  const bridgeWoodMat = new THREE.MeshStandardMaterial({ color: 0xb45309, roughness: 0.72 });
  const hatirArchMat = new THREE.MeshStandardMaterial({ color: 0xf1f5f9, roughness: 0.32, metalness: 0.38 });
  const hatirDeckMat = new THREE.MeshStandardMaterial({ color: 0x3b424c, roughness: 0.8 });
  LAKE_ZONES.forEach((lake) => {
    const rimGeo = new THREE.BoxGeometry(lake.w + 3.2, 0.45, lake.d + 3.2);
    const rim = new THREE.Mesh(rimGeo, lakeRimMat);
    rim.position.set(lake.x, 0.08, lake.z);
    const waterGeo = new THREE.PlaneGeometry(lake.w, lake.d);
    const water = new THREE.Mesh(waterGeo, lakeWaterMat);
    water.rotation.x = -Math.PI / 2;
    water.position.set(lake.x, 0.32, lake.z);
    root.add(rim, water);
    disposables.push(rimGeo, waterGeo);

    if (lake.id === 'hatirjheel_lake') {
      const deckGeo = new THREE.BoxGeometry(6.4, 0.45, lake.d + 4.5);
      const deck = new THREE.Mesh(deckGeo, hatirDeckMat);
      deck.position.set(lake.x, 1.8, lake.z);
      root.add(deck);
      disposables.push(deckGeo);

      const archGeo = new THREE.TorusGeometry(lake.d * 0.38, 0.42, 8, 20, Math.PI);
      [-2.8, 2.8].forEach((offsetX) => {
        const arch = new THREE.Mesh(archGeo, hatirArchMat);
        arch.rotation.y = Math.PI / 2;
        arch.position.set(lake.x + offsetX, 1.8, lake.z);
        root.add(arch);
      });
      disposables.push(archGeo);
    } else {
      const bridgeGeo = new THREE.BoxGeometry(4.2, 0.35, lake.d + 4);
      const footBridge = new THREE.Mesh(bridgeGeo, bridgeWoodMat);
      footBridge.position.set(lake.x, 1.1, lake.z);
      root.add(footBridge);
      disposables.push(bridgeGeo);
    }
  });
  disposables.push(lakeRimMat, lakeWaterMat, bridgeWoodMat, hatirArchMat, hatirDeckMat);

  // 3. Connected 4-Tier Asphalt Road Network with Wet-Monsoon Roughness Maps + Curbs + Drains + Gantries
  ROAD_SEGMENTS.forEach((seg, idx) => {
    const dx = seg.x2 - seg.x1;
    const dz = seg.z2 - seg.z1;
    const len = Math.hypot(dx, dz);
    const repY = Math.max(1, Math.round(len / 14));

    const roadTex = textures.asphaltMap.clone();
    roadTex.needsUpdate = true;
    roadTex.repeat.set(1, repY);

    const roadRoughTex = textures.asphaltRoughness.clone();
    roadRoughTex.needsUpdate = true;
    roadRoughTex.repeat.set(1, repY);
    const roadNormalTex = textures.asphaltNormal.clone();
    roadNormalTex.needsUpdate = true;
    roadNormalTex.repeat.set(1, repY);
    disposables.push(roadTex, roadRoughTex, roadNormalTex);

    const roadGeo = new THREE.PlaneGeometry(seg.width, len);
    roadGeo.rotateX(-Math.PI / 2);
    const roadMat = new THREE.MeshStandardMaterial({
      map: roadTex,
      normalMap: roadNormalTex,
      normalScale: new THREE.Vector2(0.62, 0.62),
      roughnessMap: null,
      color: seg.tier === 'alley' ? 0xd8d8dc : 0xffffff,
      roughness: 0.88,
      metalness: 0.04,
      polygonOffset: true,
      polygonOffsetFactor: -1,
      polygonOffsetUnits: -1,
    });
    roadMat.userData.wetRoughnessMap = roadRoughTex;
    roadMaterials.push(roadMat);

    const mesh = new THREE.Mesh(roadGeo, roadMat);
    mesh.position.set((seg.x1 + seg.x2) / 2, 0.03 + (idx % 6) * 0.0025, (seg.z1 + seg.z2) / 2);
    mesh.rotation.y = Math.atan2(dx, dz);
    mesh.receiveShadow = true;
    root.add(mesh);
    disposables.push(roadGeo, roadMat);
  });

  // Central Roundabout Asphalt Ring around Shapla Monument
  const roundGeo = new THREE.RingGeometry(4.4, 14.0, 36);
  roundGeo.rotateX(-Math.PI / 2);
  const roundMat = new THREE.MeshStandardMaterial({
    color: 0x2b3038,
    roughnessMap: null,
    roughness: 0.88,
    metalness: 0.04,
  });
  roundMat.userData.wetRoughnessMap = textures.asphaltRoughness;
  roadMaterials.push(roundMat);
  const roundMesh = new THREE.Mesh(roundGeo, roundMat);
  roundMesh.position.y = 0.052;
  roundMesh.receiveShadow = true;
  root.add(roundMesh);
  disposables.push(roundGeo, roundMat);

  // 3b. Instanced Iconic Dhaka Black-and-Yellow Striped Concrete Road Curbs (strictly outside lanes & intersections)
  const curbCoords: Array<[number, number, number]> = [];
  const nsCurbSpans: Array<[number, number]> = [
    [-212, -160], [-128, -80], [-48, -18], [18, 48], [80, 128], [160, 174],
  ];
  nsCurbSpans.forEach(([zStart, zEnd]) => {
    for (let z = zStart + 5; z <= zEnd - 4; z += 10) {
      curbCoords.push([-8.35, z, 0], [8.35, z, 0]);
    }
  });
  const ewCurbSpans: Array<[number, number]> = [
    [-212, -160], [-128, -80], [-48, -18], [18, 48], [80, 128], [160, 212],
  ];
  ewCurbSpans.forEach(([xStart, xEnd]) => {
    for (let x = xStart + 5; x <= xEnd - 4; x += 10) {
      curbCoords.push([x, -8.35, Math.PI / 2], [x, 8.35, Math.PI / 2]);
    }
  });
  const curbGeo = new THREE.BoxGeometry(0.36, 0.22, 9.6);
  curbGeo.translate(0, 0.11, 0);
  const curbTex = textures.curbStripeMap.clone();
  curbTex.needsUpdate = true;
  curbTex.repeat.set(3, 1);
  const curbMat = new THREE.MeshStandardMaterial({ map: curbTex, roughness: 0.78, metalness: 0.05 });
  const curbInst = new THREE.InstancedMesh(curbGeo, curbMat, curbCoords.length);
  curbCoords.forEach(([cx, cz, rot], i) => {
    dummy.position.set(cx, 0, cz);
    dummy.rotation.set(0, rot, 0);
    dummy.scale.set(1, 1, 1);
    dummy.updateMatrix();
    curbInst.setMatrixAt(i, dummy.matrix);
  });
  curbInst.instanceMatrix.needsUpdate = true;
  curbInst.computeBoundingSphere();
  curbInst.receiveShadow = true;
  root.add(curbInst);
  disposables.push(curbGeo, curbTex, curbMat);

  // 3c. Instanced Gutter Storm Drain Grates along Central Dhaka Avenues
  const drainCoords: Array<[number, number, number]> = [];
  [-176, -104, -36, -24, 24, 38, 104, 168].forEach((z) => {
    drainCoords.push([-7.35, z, 0], [7.35, z, 0]);
  });
  [-176, -104, -36, 36, 104, 176].forEach((x) => {
    drainCoords.push([x, -7.35, Math.PI / 2], [x, 7.35, Math.PI / 2]);
  });
  const drainGeo = new THREE.BoxGeometry(0.62, 0.035, 1.05);
  const drainMat = new THREE.MeshStandardMaterial({ color: 0x22272e, roughness: 0.52, metalness: 0.65 });
  const drainInst = new THREE.InstancedMesh(drainGeo, drainMat, drainCoords.length);
  drainCoords.forEach(([dx, dz, rot], i) => {
    dummy.position.set(dx, 0.048, dz);
    dummy.rotation.set(0, rot, 0);
    dummy.scale.set(1, 1, 1);
    dummy.updateMatrix();
    drainInst.setMatrixAt(i, dummy.matrix);
  });
  drainInst.instanceMatrix.needsUpdate = true;
  drainInst.computeBoundingSphere();
  root.add(drainInst);
  disposables.push(drainGeo, drainMat);

  // 3d. Instanced Concrete Traffic Jersey Barriers protecting flyover pillars (never blocking lanes)
  const barrierCoords: Array<[number, number, number, number]> = [];
  FLYOVER_PILLARS.forEach((p, idx) => {
    barrierCoords.push([p.x, p.z + 2.2, 0, idx % 2 === 0 ? 0xdc2626 : 0xf59e0b]);
    barrierCoords.push([p.x, p.z - 2.2, 0, idx % 2 === 0 ? 0xf8fafc : 0x1e293b]);
  });
  const barrierGeo = new THREE.BoxGeometry(1.9, 0.82, 0.56);
  barrierGeo.translate(0, 0.41, 0);
  const barrierMat = new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.72, metalness: 0.06 });
  const barrierInst = new THREE.InstancedMesh(barrierGeo, barrierMat, barrierCoords.length);
  barrierCoords.forEach(([bx, bz, rot, col], i) => {
    dummy.position.set(bx, 0, bz);
    dummy.rotation.set(0, rot, 0);
    dummy.scale.set(1, 1, 1);
    dummy.updateMatrix();
    barrierInst.setMatrixAt(i, dummy.matrix);
    tintColor.setHex(col);
    barrierInst.setColorAt(i, tintColor);
  });
  barrierInst.instanceMatrix.needsUpdate = true;
  if (barrierInst.instanceColor) barrierInst.instanceColor.needsUpdate = true;
  barrierInst.computeBoundingSphere();
  root.add(barrierInst);
  disposables.push(barrierGeo, barrierMat);

  // 3e. 4 Authentic Bilingual Overhead Dhaka Highway Direction Gantries (7.3m bus/truck clearance)
  const gantrySpecs: Array<{
    x: number;
    z: number;
    rotY: number;
    leftBn: string;
    leftEn: string;
    rightBn: string;
    rightEn: string;
  }> = [
    {
      x: 0,
      z: -24,
      rotY: 0,
      leftBn: 'কারওয়ান বাজার ←',
      leftEn: 'KAWRAN BAZAR ←',
      rightBn: 'গুলশান ↑ · বনানী',
      rightEn: 'GULSHAN ↑ · BANANI',
    },
    {
      x: 0,
      z: 56,
      rotY: Math.PI,
      leftBn: 'চকবাজার ←',
      leftEn: 'CHAWKBAZAR ←',
      rightBn: 'সদরঘাট ↓ · বুড়িগঙ্গা',
      rightEn: 'SADARGHAT ↓ · BURIGANGA',
    },
    {
      x: 54,
      z: 0,
      rotY: -Math.PI / 2,
      leftBn: 'হাতিরঝিল ←',
      leftEn: 'HATIRJHEEL ←',
      rightBn: 'মতিঝিল ↑',
      rightEn: 'MOTIJHEEL ↑',
    },
    {
      x: -54,
      z: 0,
      rotY: Math.PI / 2,
      leftBn: 'ধানমন্ডি ↑ · লেকপাড়',
      leftEn: 'DHANMONDI ↑ · LAKE',
      rightBn: 'মিরপুর →',
      rightEn: 'MIRPUR →',
    },
  ];
  gantrySpecs.forEach((g) => {
    const tex = createHighwayGantryTexture(g.leftBn, g.leftEn, g.rightBn, g.rightEn);
    const gantry = createOverheadGantrySign(18.6, tex);
    gantry.group.position.set(g.x, 0, g.z);
    gantry.group.rotation.y = g.rotY;
    root.add(gantry.group);
    signMaterials.push(gantry.signMat);
    disposables.push(tex, ...gantry.disposables);
  });

  // Instanced Zebra Crosswalks at key Dhaka intersections
  const crosswalkIntersections: Array<[number, number]> = [
    [0, 64], [0, -64], [64, 0], [-64, 0],
    [0, 144], [0, -144], [144, 0], [-144, 0],
    [64, 64], [-64, 64], [64, -64], [-64, -64],
    [0, 186], [144, 64], [-144, 64],
  ];
  const stripeGeo = new THREE.PlaneGeometry(0.85, 3.2);
  stripeGeo.rotateX(-Math.PI / 2);
  const stripeMat = new THREE.MeshBasicMaterial({
    color: 0xeae6dc,
    polygonOffset: true,
    polygonOffsetFactor: -2,
    polygonOffsetUnits: -2,
  });
  const stripeInstances: Array<[number, number, number]> = [];
  crosswalkIntersections.forEach(([ix, iz]) => {
    for (let s = -2; s <= 2; s++) {
      stripeInstances.push([ix + s * 2.1, iz + 9.5, 0]);
      stripeInstances.push([ix + s * 2.1, iz - 9.5, 0]);
      stripeInstances.push([ix + 9.5, iz + s * 2.1, Math.PI / 2]);
      stripeInstances.push([ix - 9.5, iz + s * 2.1, Math.PI / 2]);
    }
  });
  const crosswalkInst = new THREE.InstancedMesh(stripeGeo, stripeMat, stripeInstances.length);
  stripeInstances.forEach(([sx, sz, rot], idx) => {
    dummy.position.set(sx, 0.058, sz);
    dummy.rotation.set(0, rot, 0);
    dummy.scale.set(1, 1, 1);
    dummy.updateMatrix();
    crosswalkInst.setMatrixAt(idx, dummy.matrix);
  });
  crosswalkInst.instanceMatrix.needsUpdate = true;
  crosswalkInst.computeBoundingSphere();
  root.add(crosswalkInst);
  disposables.push(stripeGeo, stripeMat);

  // Iconic Shapla Monument Hero Landmark at (0, 0)
  const shapla = createShaplaMonument(textures);
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

  // Old Dhaka Mughal Ceremonial Arch Landmark (flanking the Chawkbazar pedestrian plaza)
  const oldDhakaGate = createOldDhakaGateArch();
  oldDhakaGate.position.set(-34, 0, 148);
  root.add(oldDhakaGate);

  // 4. Composite Hero Buildings + Instanced Expanded District Architecture
  const parapetMat = new THREE.MeshStandardMaterial({ color: 0x5c5852, roughness: 0.85 });
  const tankMat = new THREE.MeshStandardMaterial({ color: 0x161b22, roughness: 0.45, metalness: 0.15 });
  const tankBlueMat = new THREE.MeshStandardMaterial({ color: 0x0369a1, roughness: 0.4, metalness: 0.15 });
  const hedgeMat = new THREE.MeshStandardMaterial({ color: 0x2d6a4f, roughness: 0.88 });
  const crownLedMat = new THREE.MeshStandardMaterial({
    color: 0xbae6fd,
    emissive: 0x38bdf8,
    emissiveIntensity: 0.45,
    roughness: 0.2,
  });
  lampGlowMaterials.push(crownLedMat);
  disposables.push(parapetMat, tankMat, tankBlueMat, hedgeMat, crownLedMat);

  const heroBuildings = BUILDINGS.filter((b) => b.isHero);
  const procBuildings = BUILDINGS.filter((b) => !b.isHero);

  // 4a. Build Hero & Signed Landmarks individually with night window glow, setbacks & greenery
  heroBuildings.forEach((b: BuildingSpec) => {
    const grp = new THREE.Group();
    grp.position.set(b.x, 0, b.z);

    let mapTex: THREE.Texture | null = null;
    let normalTex: THREE.Texture | null = null;
    let emissiveTex: THREE.Texture | null = null;
    let roughTex: THREE.Texture | null = null;
    let roughness = 0.78;
    let metalness = 0.08;

    if (b.style === 'old_dhaka' || b.style === 'shop') {
      mapTex = textures.brickFacadeMap;
      normalTex = textures.brickNormal;
      emissiveTex = textures.brickEmissiveMap;
      roughness = 0.86;
    } else if (b.style === 'gulshan_tower') {
      mapTex = textures.glassTowerMap;
      emissiveTex = textures.glassTowerEmissiveMap;
      roughTex = textures.glassRoughnessMap;
      roughness = 0.22;
      metalness = 0.66;
    } else if (b.style === 'motijheel_tower') {
      mapTex = textures.motijheelTowerMap;
      emissiveTex = textures.motijheelEmissiveMap;
      roughTex = textures.glassRoughnessMap;
      roughness = 0.3;
      metalness = 0.48;
    } else if (b.style === 'market_hall' || b.style === 'warehouse') {
      mapTex = textures.marketFacadeMap;
      emissiveTex = textures.brickEmissiveMap;
      roughness = 0.76;
    } else if (b.style === 'residential' || b.style === 'police') {
      mapTex = textures.residentialMap;
      emissiveTex = textures.residentialEmissiveMap;
      roughness = 0.78;
    }

    const wallMat = new THREE.MeshStandardMaterial({
      color: b.style === 'gulshan_tower' || b.style === 'motijheel_tower' ? 0xffffff : b.color,
      map: mapTex,
      normalMap: normalTex,
      normalScale: normalTex ? new THREE.Vector2(0.7, 0.7) : new THREE.Vector2(1, 1),
      roughnessMap: roughTex,
      emissiveMap: emissiveTex,
      emissive: emissiveTex ? 0xffffff : 0x000000,
      emissiveIntensity: 0.1,
      roughness,
      metalness,
    });
    if (emissiveTex) nightWindowMaterials.push(wallMat);

    const accentMat = new THREE.MeshStandardMaterial({
      color: b.accentColor,
      roughness: 0.48,
      metalness: 0.22,
    });
    disposables.push(wallMat, accentMat);

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

    // Ground-floor shopfront band & cornice parapet
    const plinthGeo = new THREE.BoxGeometry(b.w + 0.32, 0.55, b.d + 0.32);
    const plinth = new THREE.Mesh(plinthGeo, accentMat);
    plinth.position.y = 3.6;
    grp.add(plinth);
    disposables.push(plinthGeo);

    const roofCapGeo = new THREE.BoxGeometry(b.w + 0.48, 0.65, b.d + 0.48);
    const roofCap = new THREE.Mesh(roofCapGeo, parapetMat);
    roofCap.position.y = b.h + 0.28;
    grp.add(roofCap);
    disposables.push(roofCapGeo);

    // High-rise skyline stepped setback crown + glowing LED accent band + landscaped setback hedges
    if (b.style === 'gulshan_tower' || b.style === 'motijheel_tower') {
      const crownGeo = new THREE.BoxGeometry(b.w * 0.62, 4.2, b.d * 0.62);
      const crown = new THREE.Mesh(crownGeo, wallMat);
      crown.position.y = b.h + 2.3;
      grp.add(crown);
      disposables.push(crownGeo);

      const ledGeo = new THREE.BoxGeometry(b.w * 0.64, 0.36, b.d * 0.64);
      const ledBand = new THREE.Mesh(ledGeo, crownLedMat);
      ledBand.position.y = b.h + 4.45;
      grp.add(ledBand);
      disposables.push(ledGeo);

      // Landscaped corporate plaza planter hedges along street frontage
      const hedgeGeo = new THREE.BoxGeometry(b.w * 0.72, 0.65, 0.9);
      const hedgeSide = b.z > 0 ? -(b.d / 2 + 0.55) : b.d / 2 + 0.55;
      const hedge = new THREE.Mesh(hedgeGeo, hedgeMat);
      hedge.position.set(0, 0.325, hedgeSide);
      grp.add(hedge);
      disposables.push(hedgeGeo);

      if (b.h >= 38) {
        const spireGeo = new THREE.CylinderGeometry(0.12, 0.25, 7.8, 6);
        const spire = new THREE.Mesh(spireGeo, parapetMat);
        spire.position.y = b.h + 8.3;
        grp.add(spire);
        disposables.push(spireGeo);
      }
    }

    // Corrugated tin shop awnings
    if (b.hasAwning) {
      const awningGeo = new THREE.BoxGeometry(b.w * 0.85, 0.16, 2.2);
      const awning = new THREE.Mesh(awningGeo, accentMat);
      const sideSign = b.z > 0 ? -1 : 1;
      awning.position.set(0, 3.3, sideSign * (b.d / 2 + 1.0));
      awning.rotation.x = sideSign * 0.24;
      grp.add(awning);
      disposables.push(awningGeo);
    }

    // Iconic Dhaka plastic rooftop water tanks
    if (b.hasWaterTank) {
      const tankGeo = new THREE.CylinderGeometry(0.75, 0.8, 1.55, 10);
      const tank = new THREE.Mesh(tankGeo, b.id.length % 2 === 0 ? tankMat : tankBlueMat);
      tank.position.set(b.w * 0.24, b.h + 1.25, -b.d * 0.2);
      grp.add(tank);
      disposables.push(tankGeo);
    }

    // Illuminated bilingual Bengali + English signboard
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

      const signGeo = new THREE.BoxGeometry(Math.min(b.w * 0.82, 9.2), 1.9, 0.24);
      const signMesh = new THREE.Mesh(signGeo, signMat);
      const faceFrontZ = b.z > 0 ? -(b.d / 2 + 0.18) : b.d / 2 + 0.18;
      signMesh.position.set(0, Math.min(b.h - 1.2, 5.2), faceFrontZ);
      if (b.z > 0) signMesh.rotation.y = Math.PI;
      grp.add(signMesh);
      disposables.push(signGeo);
    }

    root.add(grp);
  });

  // 4b. Batch all ~100+ Expanded Procedural Buildings into 5 InstancedMeshes with Night Window Emissives
  const unitBoxGeo = new THREE.BoxGeometry(1, 1, 1);
  unitBoxGeo.translate(0, 0.5, 0);
  disposables.push(unitBoxGeo);

  const styleGroups: Record<string, BuildingSpec[]> = {
    old_dhaka: [],
    gulshan_tower: [],
    motijheel_tower: [],
    residential: [],
    market: [],
  };
  procBuildings.forEach((b) => {
    if (b.style === 'old_dhaka' || b.style === 'shop') styleGroups.old_dhaka.push(b);
    else if (b.style === 'gulshan_tower') styleGroups.gulshan_tower.push(b);
    else if (b.style === 'motijheel_tower') styleGroups.motijheel_tower.push(b);
    else if (b.style === 'market_hall' || b.style === 'warehouse') styleGroups.market.push(b);
    else styleGroups.residential.push(b);
  });

  const styleMaterials: Record<string, THREE.MeshStandardMaterial> = {
    old_dhaka: new THREE.MeshStandardMaterial({
      color: 0xffffff,
      map: textures.brickFacadeMap,
      normalMap: textures.brickNormal,
      normalScale: new THREE.Vector2(0.65, 0.65),
      emissiveMap: textures.brickEmissiveMap,
      emissive: 0xffffff,
      emissiveIntensity: 0.1,
      roughness: 0.86,
      metalness: 0.04,
    }),
    gulshan_tower: new THREE.MeshStandardMaterial({
      color: 0xffffff,
      map: textures.glassTowerMap,
      roughnessMap: textures.glassRoughnessMap,
      emissiveMap: textures.glassTowerEmissiveMap,
      emissive: 0xffffff,
      emissiveIntensity: 0.12,
      roughness: 0.22,
      metalness: 0.64,
    }),
    motijheel_tower: new THREE.MeshStandardMaterial({
      color: 0xffffff,
      map: textures.motijheelTowerMap,
      roughnessMap: textures.glassRoughnessMap,
      emissiveMap: textures.motijheelEmissiveMap,
      emissive: 0xffffff,
      emissiveIntensity: 0.12,
      roughness: 0.3,
      metalness: 0.46,
    }),
    residential: new THREE.MeshStandardMaterial({
      color: 0xffffff,
      map: textures.residentialMap,
      emissiveMap: textures.residentialEmissiveMap,
      emissive: 0xffffff,
      emissiveIntensity: 0.1,
      roughness: 0.78,
      metalness: 0.05,
    }),
    market: new THREE.MeshStandardMaterial({
      color: 0xffffff,
      map: textures.marketFacadeMap,
      emissiveMap: textures.brickEmissiveMap,
      emissive: 0xffffff,
      emissiveIntensity: 0.08,
      roughness: 0.75,
      metalness: 0.14,
    }),
  };

  Object.entries(styleGroups).forEach(([key, list]) => {
    const mat = styleMaterials[key];
    nightWindowMaterials.push(mat);
    disposables.push(mat);
    if (list.length === 0) return;

    const inst = new THREE.InstancedMesh(unitBoxGeo, mat, list.length);
    list.forEach((b, i) => {
      dummy.position.set(b.x, 0, b.z);
      dummy.rotation.set(0, 0, 0);
      dummy.scale.set(b.w, b.h, b.d);
      dummy.updateMatrix();
      inst.setMatrixAt(i, dummy.matrix);
      if (key === 'gulshan_tower' || key === 'motijheel_tower') {
        tintColor.setHSL(0.56 + (i % 5) * 0.015, 0.16, 0.88 + (i % 3) * 0.04);
      } else {
        tintColor.setHex(b.color).lerp(new THREE.Color(0xffffff), 0.35);
      }
      inst.setColorAt(i, tintColor);
    });
    inst.instanceMatrix.needsUpdate = true;
    if (inst.instanceColor) inst.instanceColor.needsUpdate = true;
    inst.computeBoundingSphere();
    if (castShadow) {
      inst.castShadow = true;
      inst.receiveShadow = true;
    }
    root.add(inst);
    cameraOccluders.push(inst);
  });

  // Instanced Roof Parapets, Stepped Tower Crowns, Balconies, AC Units, Water Tanks & Shopfront Awnings
  if (procBuildings.length > 0) {
    const parapetInst = new THREE.InstancedMesh(unitBoxGeo, parapetMat, procBuildings.length);
    const awningList = procBuildings.filter((b) => b.hasAwning);
    const tankList = procBuildings.filter((b) => b.hasWaterTank);
    const towerList = procBuildings.filter((b) => b.style === 'gulshan_tower' || b.style === 'motijheel_tower');
    const balconyBuildings = procBuildings.filter((b) => b.style === 'old_dhaka' || b.style === 'residential');

    procBuildings.forEach((b, i) => {
      dummy.position.set(b.x, b.h, b.z);
      dummy.rotation.set(0, 0, 0);
      dummy.scale.set(b.w + 0.45, 0.62, b.d + 0.45);
      dummy.updateMatrix();
      parapetInst.setMatrixAt(i, dummy.matrix);
    });
    parapetInst.instanceMatrix.needsUpdate = true;
    parapetInst.computeBoundingSphere();
    root.add(parapetInst);

    // Stepped upper setbacks on procedural Gulshan & Motijheel glass towers for richer skyline silhouettes
    if (towerList.length > 0) {
      const crownInst = new THREE.InstancedMesh(unitBoxGeo, styleMaterials.gulshan_tower, towerList.length);
      towerList.forEach((b, i) => {
        const crownH = 3.2 + (i % 3) * 1.4;
        dummy.position.set(b.x, b.h + 0.4, b.z);
        dummy.rotation.set(0, 0, 0);
        dummy.scale.set(b.w * 0.58, crownH, b.d * 0.58);
        dummy.updateMatrix();
        crownInst.setMatrixAt(i, dummy.matrix);
      });
      crownInst.instanceMatrix.needsUpdate = true;
      crownInst.computeBoundingSphere();
      root.add(crownInst);
    }

    // 3D Protruding Dhaka Balconies & External AC Condenser Units on Old Dhaka & Residential facades
    if (balconyBuildings.length > 0) {
      const balconyPlacements: Array<{ x: number; y: number; z: number; w: number }> = [];
      const acPlacements: Array<{ x: number; y: number; z: number }> = [];
      balconyBuildings.forEach((b) => {
        const sideSign = b.z > 0 ? -1 : 1;
        const faceZ = b.z + sideSign * (b.d / 2 + 0.42);
        const floors = Math.min(3, Math.max(1, Math.floor((b.h - 4) / 4.2)));
        for (let fl = 0; fl < floors; fl++) {
          const fy = 4.6 + fl * 4.1;
          if (fy + 1.4 > b.h) break;
          balconyPlacements.push({ x: b.x - b.w * 0.16, y: fy, z: faceZ, w: Math.min(3.8, b.w * 0.38) });
          acPlacements.push({ x: b.x + b.w * 0.24, y: fy + 0.6, z: b.z + sideSign * (b.d / 2 + 0.24) });
        }
      });

      const balcMat = new THREE.MeshStandardMaterial({ color: 0xbcb5a8, roughness: 0.82 });
      const balcInst = new THREE.InstancedMesh(unitBoxGeo, balcMat, balconyPlacements.length);
      balconyPlacements.forEach((p, i) => {
        dummy.position.set(p.x, p.y, p.z);
        dummy.rotation.set(0, 0, 0);
        dummy.scale.set(p.w, 0.95, 0.82);
        dummy.updateMatrix();
        balcInst.setMatrixAt(i, dummy.matrix);
      });
      balcInst.instanceMatrix.needsUpdate = true;
      balcInst.computeBoundingSphere();
      root.add(balcInst);
      disposables.push(balcMat);

      const acMat = new THREE.MeshStandardMaterial({ color: 0xdbe2e8, roughness: 0.55, metalness: 0.25 });
      const acInst = new THREE.InstancedMesh(unitBoxGeo, acMat, acPlacements.length);
      acPlacements.forEach((p, i) => {
        dummy.position.set(p.x, p.y, p.z);
        dummy.rotation.set(0, 0, 0);
        dummy.scale.set(0.86, 0.58, 0.44);
        dummy.updateMatrix();
        acInst.setMatrixAt(i, dummy.matrix);
      });
      acInst.instanceMatrix.needsUpdate = true;
      acInst.computeBoundingSphere();
      root.add(acInst);
      disposables.push(acMat);
    }

    if (awningList.length > 0) {
      const awningMat = new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.55, metalness: 0.2 });
      const awningInst = new THREE.InstancedMesh(unitBoxGeo, awningMat, awningList.length);
      awningList.forEach((b, i) => {
        const sideSign = b.z > 0 ? -1 : 1;
        dummy.position.set(b.x, 3.25, b.z + sideSign * (b.d / 2 + 1.0));
        dummy.rotation.set(sideSign * 0.22, 0, 0);
        dummy.scale.set(b.w * 0.82, 0.16, 2.1);
        dummy.updateMatrix();
        awningInst.setMatrixAt(i, dummy.matrix);
        tintColor.setHex(b.accentColor);
        awningInst.setColorAt(i, tintColor);
      });
      awningInst.instanceMatrix.needsUpdate = true;
      if (awningInst.instanceColor) awningInst.instanceColor.needsUpdate = true;
      awningInst.computeBoundingSphere();
      root.add(awningInst);
      disposables.push(awningMat);
    }

    if (tankList.length > 0) {
      const tankGeo = new THREE.CylinderGeometry(0.72, 0.78, 1.5, 8);
      tankGeo.translate(0, 0.75, 0);
      const tankInst = new THREE.InstancedMesh(tankGeo, tankMat, tankList.length);
      tankList.forEach((b, i) => {
        dummy.position.set(b.x + b.w * 0.22, b.h + 0.5, b.z - b.d * 0.18);
        dummy.rotation.set(0, 0, 0);
        dummy.scale.set(1, 1, 1);
        dummy.updateMatrix();
        tankInst.setMatrixAt(i, dummy.matrix);
      });
      tankInst.instanceMatrix.needsUpdate = true;
      tankInst.computeBoundingSphere();
      root.add(tankInst);
      disposables.push(tankGeo);
    }
  }

  // 4c. Outer Dhaka Distant Skyline Silhouette Ring (1 draw call blending into horizon haze, open south over Buriganga)
  const skylinePositions: Array<[number, number, number, number, number]> = [];
  const ringCount = 92;
  for (let i = 0; i < ringCount; i++) {
    // Arc from east-southeast around north to west-southwest (keep Buriganga river vista open to the south!)
    const angle = -Math.PI * 0.08 + (i / (ringCount - 1)) * Math.PI * 1.16;
    const rad = 302 + ((i * 47) % 108);
    const sx = Math.cos(angle) * rad;
    const sz = -Math.sin(angle) * rad + 28;
    if (sz > RIVER_Z_START - 24) continue;
    const sw = 18 + ((i * 19) % 24);
    const sd = 16 + ((i * 13) % 22);
    // Taller towers in north/east (Gulshan/Uttara/Motijheel skyline direction)
    const isTowerSector = sx > -40 && sz < 40;
    const sh = (isTowerSector ? 32 : 18) + ((i * 29) % (isTowerSector ? 44 : 24));
    skylinePositions.push([sx, sz, sw, sh, sd]);
  }
  const skylineMat = new THREE.MeshStandardMaterial({
    color: 0x7b8996,
    emissiveMap: textures.glassTowerEmissiveMap,
    emissive: 0xffe4b5,
    emissiveIntensity: 0.1,
    roughness: 0.72,
    metalness: 0.25,
  });
  nightWindowMaterials.push(skylineMat);
  const skylineInst = new THREE.InstancedMesh(unitBoxGeo, skylineMat, skylinePositions.length);
  skylinePositions.forEach(([sx, sz, sw, sh, sd], i) => {
    dummy.position.set(sx, -0.1, sz);
    dummy.rotation.set(0, (i % 4) * 0.12, 0);
    dummy.scale.set(sw, sh, sd);
    dummy.updateMatrix();
    skylineInst.setMatrixAt(i, dummy.matrix);
    tintColor.setHSL(0.57 + ((i % 5) - 2) * 0.02, 0.12, 0.56 + (i % 4) * 0.06);
    skylineInst.setColorAt(i, tintColor);
  });
  skylineInst.instanceMatrix.needsUpdate = true;
  if (skylineInst.instanceColor) skylineInst.instanceColor.needsUpdate = true;
  skylineInst.computeBoundingSphere();
  root.add(skylineInst);
  disposables.push(skylineMat);

  // 5. Extended Elevated Dhaka Express Flyover (318m viaduct spanning Kawran -> Mirpur -> Old Dhaka)
  const flyoverConcreteMat = new THREE.MeshStandardMaterial({ color: 0x8a8e94, roughness: 0.82 });
  const flyoverDeckMat = new THREE.MeshStandardMaterial({ color: 0x3a3f47, roughness: 0.75 });
  const flyoverRailMat = new THREE.MeshStandardMaterial({ color: 0xf59e0b, roughness: 0.45, metalness: 0.35 });
  disposables.push(flyoverConcreteMat, flyoverDeckMat, flyoverRailMat);

  const flyoverLength = 318;
  const flyoverCenterZ = -37;
  const deckGeo = new THREE.BoxGeometry(7.4, 1.35, flyoverLength);
  const deck = new THREE.Mesh(deckGeo, flyoverDeckMat);
  deck.position.set(-56.2, 9.6, flyoverCenterZ);
  if (castShadow) {
    deck.castShadow = true;
    deck.receiveShadow = true;
  }
  root.add(deck);
  disposables.push(deckGeo);

  [-3.55, 3.55].forEach((rx) => {
    const railGeo = new THREE.BoxGeometry(0.32, 0.85, flyoverLength);
    const rail = new THREE.Mesh(railGeo, flyoverRailMat);
    rail.position.set(-56.2 + rx, 10.65, flyoverCenterZ);
    root.add(rail);
    disposables.push(railGeo);
  });

  const pillarGeo = new THREE.CylinderGeometry(0.88, 1.08, 9.3, 12);
  const capGeo = new THREE.BoxGeometry(6.0, 0.9, 2.3);
  FLYOVER_PILLARS.forEach((p) => {
    const pillar = new THREE.Mesh(pillarGeo, flyoverConcreteMat);
    pillar.position.set(p.x, 4.65, p.z);
    if (castShadow) pillar.castShadow = true;
    const cap = new THREE.Mesh(capGeo, flyoverConcreteMat);
    cap.position.set(p.x, 8.9, p.z);
    root.add(pillar, cap);
  });
  disposables.push(pillarGeo, capGeo);

  // 6. Instanced Multi-Cluster Tropical Dhaka Rain-Trees + Concrete Sidewalk Planter Rings
  const treePositions: Array<[number, number]> = [
    // Central North-South Avenue
    [-11, 24], [-11, 40], [-11, 54], [-11, 84], [-11, 114], [-11, 162],
    [11, 24], [11, 40], [11, 54], [11, 84], [11, 114], [11, 162],
    [-11, -20], [-11, -40], [-11, -54], [-11, -84], [-11, -116], [-11, -168], [-11, -204],
    [11, -20], [11, -40], [11, -54], [11, -84], [11, -116], [11, -168], [11, -204],
    // Central East-West Avenue (Shapla -> Motijheel & Dhanmondi)
    [24, 11], [44, 11], [86, 11], [122, 11], [168, 11], [202, 11],
    [24, -11], [44, -11], [86, -11], [122, -11], [168, -11], [202, -11],
    [-24, 11], [-44, 11], [-86, 11], [-122, 11], [-168, 11], [-202, 11],
    [-24, -11], [-44, -11], [-86, -11], [-122, -11], [-168, -11], [-202, -11],
    // Sadarghat Riverfront Promenade Palms/Shade Trees
    [-196, 195], [-162, 195], [-120, 195], [-84, 195], [-44, 195], [-16, 195],
    [16, 195], [48, 195], [88, 195], [128, 195], [168, 195], [204, 195],
    // Dhanmondi Lakefront Promenade Trees
    [-212, -32], [-156, -32], [-196, -54], [-172, -54], [-196, -10], [-172, -10],
    // Motijheel & Banani Boulevard Trees
    [73, 32], [73, 84], [73, -32], [73, -96],
    [153, -32], [153, -96], [153, 84], [153, 124],
    [-73, 84], [-73, 124], [-153, 84], [-153, -96],
  ];

  const trunkGeo = new THREE.CylinderGeometry(0.2, 0.36, 4.7, 8);
  trunkGeo.translate(0, 2.35, 0);
  const trunkMat = new THREE.MeshStandardMaterial({ color: 0x563d2d, roughness: 0.92 });
  const planterGeo = new THREE.CylinderGeometry(0.86, 0.96, 0.34, 10);
  planterGeo.translate(0, 0.17, 0);
  const planterMat = new THREE.MeshStandardMaterial({ color: 0x7c756b, roughness: 0.88 });
  const canopyGeo = new THREE.DodecahedronGeometry(1.58, 1);
  const canopyMat = new THREE.MeshStandardMaterial({ color: 0x3a8452, roughness: 0.85, metalness: 0.0 });

  const trunkInst = new THREE.InstancedMesh(trunkGeo, trunkMat, treePositions.length);
  const planterInst = new THREE.InstancedMesh(planterGeo, planterMat, treePositions.length);
  const clustersPerTree = 4;
  const canopyInst = new THREE.InstancedMesh(canopyGeo, canopyMat, treePositions.length * clustersPerTree);

  const clusterOffsets: Array<[number, number, number, number]> = [
    [0, 5.75, 0, 1.18],
    [-1.12, 4.82, 0.68, 0.88],
    [1.08, 4.9, -0.62, 0.92],
    [0.15, 4.95, 1.05, 0.82],
  ];

  treePositions.forEach(([tx, tz], i) => {
    const s = 0.86 + ((i * 37) % 28) / 100;
    const rotY = i * 1.15;
    dummy.position.set(tx, 0, tz);
    dummy.rotation.set(0, rotY, 0);
    dummy.scale.set(s, s * (0.95 + (i % 3) * 0.08), s);
    dummy.updateMatrix();
    trunkInst.setMatrixAt(i, dummy.matrix);

    dummy.scale.set(1, 1, 1);
    dummy.updateMatrix();
    planterInst.setMatrixAt(i, dummy.matrix);

    const cosR = Math.cos(rotY);
    const sinR = Math.sin(rotY);
    clusterOffsets.forEach(([ox, oy, oz, cs], ci) => {
      const idx = i * clustersPerTree + ci;
      const wx = tx + (ox * cosR - oz * sinR) * s;
      const wz = tz + (ox * sinR + oz * cosR) * s;
      dummy.position.set(wx, oy * s, wz);
      dummy.rotation.set(ci * 0.4, rotY + ci * 1.2, 0);
      dummy.scale.set(s * cs * 1.14, s * cs * 0.8, s * cs * 1.08);
      dummy.updateMatrix();
      canopyInst.setMatrixAt(idx, dummy.matrix);
      tintColor.setHSL(0.35 + (((i + ci) % 5) - 2) * 0.016, 0.38, 0.48 + ci * 0.055);
      canopyInst.setColorAt(idx, tintColor);
    });
  });
  trunkInst.instanceMatrix.needsUpdate = true;
  planterInst.instanceMatrix.needsUpdate = true;
  canopyInst.instanceMatrix.needsUpdate = true;
  if (canopyInst.instanceColor) canopyInst.instanceColor.needsUpdate = true;
  trunkInst.computeBoundingSphere();
  planterInst.computeBoundingSphere();
  canopyInst.computeBoundingSphere();
  if (castShadow) {
    trunkInst.castShadow = true;
    canopyInst.castShadow = true;
  }
  root.add(trunkInst, planterInst, canopyInst);
  disposables.push(trunkGeo, trunkMat, planterGeo, planterMat, canopyGeo, canopyMat);

  // 7. Instanced Dhaka Sodium Streetlamps + Additive Glow Halos & Ground Light Pools (zero extra PointLights!)
  const lampCoords: Array<[number, number, number]> = [];
  [-204, -168, -112, -84, -48, -22, 18, 48, 88, 120, 168].forEach((z) => {
    lampCoords.push([-9.2, z, 0], [9.2, z, Math.PI]);
  });
  [-200, -160, -112, -84, -36, 36, 84, 112, 160, 200].forEach((x) => {
    lampCoords.push([x, -9.2, -Math.PI / 2], [x, 9.2, Math.PI / 2]);
  });
  [-180, -104, -36, 36, 104, 180].forEach((x) => {
    lampCoords.push([x, 178, Math.PI / 2]);
  });
  [28, 86, 126].forEach((z) => {
    lampCoords.push([136.5, z, 0], [151.5, z, Math.PI]);
  });

  const poleMat = new THREE.MeshStandardMaterial({ color: 0x334155, roughness: 0.5, metalness: 0.7 });
  const bulbMat = new THREE.MeshStandardMaterial({
    color: 0xffd580,
    emissive: 0xffa726,
    emissiveIntensity: 0.45,
    roughness: 0.2,
  });
  lampGlowMaterials.push(bulbMat);

  const lampHaloMat = new THREE.MeshBasicMaterial({
    map: textures.lampHaloMap,
    color: 0xffb84d,
    transparent: true,
    opacity: 0.0,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    side: THREE.DoubleSide,
  });
  const lampPoolMat = new THREE.MeshBasicMaterial({
    map: textures.lampHaloMap,
    color: 0xff9e38,
    transparent: true,
    opacity: 0.0,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
  });

  const poleGeo = new THREE.CylinderGeometry(0.1, 0.14, 6.8, 6);
  poleGeo.translate(0, 3.4, 0);
  const bulbGeo = new THREE.BoxGeometry(0.75, 0.2, 0.34);
  const haloGeo = new THREE.PlaneGeometry(2.6, 2.6);
  const poolGeo = new THREE.PlaneGeometry(7.6, 7.6);
  poolGeo.rotateX(-Math.PI / 2);

  const poleInst = new THREE.InstancedMesh(poleGeo, poleMat, lampCoords.length);
  const bulbInst = new THREE.InstancedMesh(bulbGeo, bulbMat, lampCoords.length);
  // 2 crossed vertical halo planes per lamp so glow reads softly from any camera angle
  const haloInst = new THREE.InstancedMesh(haloGeo, lampHaloMat, lampCoords.length * 2);
  const poolInst = new THREE.InstancedMesh(poolGeo, lampPoolMat, lampCoords.length);

  lampCoords.forEach(([lx, lz, rot], idx) => {
    dummy.position.set(lx, 0, lz);
    dummy.rotation.set(0, rot, 0);
    dummy.scale.set(1, 1, 1);
    dummy.updateMatrix();
    poleInst.setMatrixAt(idx, dummy.matrix);

    const armX = lx + Math.cos(rot) * 1.1;
    const armZ = lz - Math.sin(rot) * 1.1;
    dummy.position.set(armX, 6.65, armZ);
    dummy.rotation.set(0, rot, 0);
    dummy.updateMatrix();
    bulbInst.setMatrixAt(idx, dummy.matrix);

    // Crossed sodium lamp glow halos
    dummy.position.set(armX, 6.52, armZ);
    dummy.rotation.set(0, 0, 0);
    dummy.updateMatrix();
    haloInst.setMatrixAt(idx * 2, dummy.matrix);

    dummy.rotation.set(0, Math.PI / 2, 0);
    dummy.updateMatrix();
    haloInst.setMatrixAt(idx * 2 + 1, dummy.matrix);

    // Warm sodium ground light pool onto Dhaka asphalt/sidewalk
    dummy.position.set(armX, 0.068, armZ);
    dummy.rotation.set(0, 0, 0);
    dummy.updateMatrix();
    poolInst.setMatrixAt(idx, dummy.matrix);
  });
  poleInst.instanceMatrix.needsUpdate = true;
  bulbInst.instanceMatrix.needsUpdate = true;
  haloInst.instanceMatrix.needsUpdate = true;
  poolInst.instanceMatrix.needsUpdate = true;
  poleInst.computeBoundingSphere();
  bulbInst.computeBoundingSphere();
  haloInst.computeBoundingSphere();
  poolInst.computeBoundingSphere();
  root.add(poleInst, bulbInst, haloInst, poolInst);
  disposables.push(poleMat, bulbMat, lampHaloMat, lampPoolMat, poleGeo, bulbGeo, haloGeo, poolGeo);

  // 7b. Overhead Dhaka Street Power Wires (single LineSegments draw call in Old Dhaka & Kawran Bazar)
  const wireVerts: number[] = [];
  const addWireSpan = (x1: number, z1: number, x2: number, z2: number, y = 6.2) => {
    const mx = (x1 + x2) * 0.5;
    const mz = (z1 + z2) * 0.5;
    wireVerts.push(x1, y, z1, mx, y - 0.55, mz);
    wireVerts.push(mx, y - 0.55, mz, x2, y, z2);
    wireVerts.push(x1, y - 0.35, z1, mx, y - 0.85, mz);
    wireVerts.push(mx, y - 0.85, mz, x2, y - 0.35, z2);
  };
  [-9.2, 9.2].forEach((x) => {
    addWireSpan(x, 18, x, 48);
    addWireSpan(x, 48, x, 88);
    addWireSpan(x, 88, x, 120);
    addWireSpan(x, 120, x, 168);
  });
  addWireSpan(-9.2, 48, 9.2, 48, 6.4);
  addWireSpan(-9.2, 120, 9.2, 120, 6.4);
  addWireSpan(-36, 9.2, -36, -9.2, 6.4);
  const wireGeo = new THREE.BufferGeometry();
  wireGeo.setAttribute('position', new THREE.Float32BufferAttribute(wireVerts, 3));
  const wireMat = new THREE.LineBasicMaterial({ color: 0x181c22, transparent: true, opacity: 0.72 });
  const wireLines = new THREE.LineSegments(wireGeo, wireMat);
  root.add(wireLines);
  disposables.push(wireGeo, wireMat);

  // 7c. Instanced Roadside Dhaka Market Stalls (Chawkbazar, Kawran Bazar, Sadarghat, Mirpur sidewalks)
  const stallCoords: Array<[number, number, number, number]> = [
    [-16, 36, 0, 0xd97706], [-16, 47, 0, 0x0f766e], [-34, 55, Math.PI / 2, 0xb91c1c],
    [-42, 55, Math.PI / 2, 0x1d4ed8], [-96, 86, 0, 0xd97706], [-96, 118, 0, 0xb91c1c],
    [-112, 94, Math.PI, 0x0f766e], [-76, 112, Math.PI / 2, 0xea580c],
    [-20, -14, Math.PI / 2, 0x0f766e], [-32, -14, Math.PI / 2, 0xb91c1c],
    [-112, -160, 0, 0xd97706], [-124, -160, 0, 0x0f766e],
    [-28, 176, Math.PI / 2, 0xb91c1c], [-18, 176, Math.PI / 2, 0xd97706],
    [16, 176, Math.PI / 2, 0x0f766e], [34, 176, Math.PI / 2, 0x1d4ed8],
    [74, 48, 0, 0x0f766e], [74, 78, 0, 0xd97706],
  ];
  const stallTableGeo = new THREE.BoxGeometry(2.6, 1.0, 1.4);
  stallTableGeo.translate(0, 0.5, 0);
  const stallCanopyGeo = new THREE.BoxGeometry(3.0, 0.16, 1.9);
  stallCanopyGeo.translate(0, 2.35, 0);
  const stallTableMat = new THREE.MeshStandardMaterial({ color: 0x6b4423, roughness: 0.88 });
  const stallCanopyMat = new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.72 });

  const stallTableInst = new THREE.InstancedMesh(stallTableGeo, stallTableMat, stallCoords.length);
  const stallCanopyInst = new THREE.InstancedMesh(stallCanopyGeo, stallCanopyMat, stallCoords.length);
  stallCoords.forEach(([sx, sz, rot, col], i) => {
    dummy.position.set(sx, 0, sz);
    dummy.rotation.set(0, rot, 0);
    dummy.scale.set(1, 1, 1);
    dummy.updateMatrix();
    stallTableInst.setMatrixAt(i, dummy.matrix);
    stallCanopyInst.setMatrixAt(i, dummy.matrix);
    tintColor.setHex(col);
    stallCanopyInst.setColorAt(i, tintColor);
  });
  stallTableInst.instanceMatrix.needsUpdate = true;
  stallCanopyInst.instanceMatrix.needsUpdate = true;
  if (stallCanopyInst.instanceColor) stallCanopyInst.instanceColor.needsUpdate = true;
  stallTableInst.computeBoundingSphere();
  stallCanopyInst.computeBoundingSphere();
  root.add(stallTableInst, stallCanopyInst);
  disposables.push(stallTableGeo, stallCanopyGeo, stallTableMat, stallCanopyMat);

  // 7d. Covered Dhaka Bus Stop Shelters on key boulevard bays
  const busStops: Array<[number, number, number]> = [
    [11, 28, -Math.PI / 2],
    [11, -34, -Math.PI / 2],
    [134, 44, Math.PI / 2],
    [-134, 24, -Math.PI / 2],
    [48, 176, 0],
    [74, -108, -Math.PI / 2],
  ];
  busStops.forEach(([bx, bz, rot]) => {
    const shelter = createBusStopShelter();
    shelter.position.set(bx, 0, bz);
    shelter.rotation.y = rot;
    root.add(shelter);
  });

  // 8. Interactive POI Ground Markers (Garages & Shops across the expanded city)
  POIS.forEach((poi) => {
    if (poi.kind !== 'garage' && poi.kind !== 'shop') return;
    const color = poi.kind === 'garage' ? 0xf59e0b : 0x10b981;
    const rGeo = new THREE.RingGeometry(2.1, 2.75, 28);
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
    const ringGeo = new THREE.RingGeometry(1.9, 2.7, 30);
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

    const beamGeo = new THREE.CylinderGeometry(0.55, 1.3, 34, 12, 1, true);
    const beamMat = new THREE.MeshBasicMaterial({
      color: colorHex,
      transparent: true,
      opacity: 0.22,
      side: THREE.DoubleSide,
      depthWrite: false,
    });
    const beam = new THREE.Mesh(beamGeo, beamMat);
    beam.position.y = 17;
    g.add(ring, beam);
    g.visible = false;
    disposables.push(ringGeo, ringMat, beamGeo, beamMat);
    return g;
  };

  const missionBeacon = createBeacon(0xfbbf24);
  const fareBeacon = createBeacon(0x22d3ee);
  root.add(missionBeacon, fareBeacon);

  // 10. Pooled Monsoon Rain Points (zero-GC buffer around camera)
  const rainCount = 1350;
  const rainPositions = new Float32Array(rainCount * 3);
  for (let i = 0; i < rainCount; i++) {
    rainPositions[i * 3] = (Math.random() - 0.5) * 72;
    rainPositions[i * 3 + 1] = Math.random() * 30;
    rainPositions[i * 3 + 2] = (Math.random() - 0.5) * 72;
  }
  const rainGeo = new THREE.BufferGeometry();
  rainGeo.setAttribute('position', new THREE.BufferAttribute(rainPositions, 3));
  const rainMat = new THREE.PointsMaterial({
    color: 0xcbd5e1,
    size: 0.14,
    transparent: true,
    opacity: 0.52,
    depthWrite: false,
  });
  const rainPoints = new THREE.Points(rainGeo, rainMat);
  rainPoints.visible = false;
  rainPoints.frustumCulled = false;
  root.add(rainPoints);
  disposables.push(rainGeo, rainMat);

  return {
    root,
    skyDome,
    skyUniforms,
    riverMesh,
    riverMat,
    waterMaterials,
    roadMaterials,
    nightWindowMaterials,
    lampHaloMat,
    lampPoolMat,
    haloInst,
    poolInst,
    boats,
    lampGlowMaterials,
    signMaterials,
    missionBeacon,
    fareBeacon,
    poiRings,
    rainPoints,
    rainPositions,
    rainMaxCount: rainCount,
    cameraOccluders,
    disposables,
  };
}
