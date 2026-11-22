import * as THREE from 'three';
import { OutfitId, OUTFIT_SPECS, VehicleKind, WeaponId } from './types';
import { DhakaTextureSet } from './textures';

export interface VehicleRig {
  group: THREE.Group;
  wheels: THREE.Object3D[];
  bodyMaterials: THREE.MeshStandardMaterial[];
  headlights: THREE.MeshStandardMaterial[];
  sirenRed?: THREE.MeshStandardMaterial;
  sirenBlue?: THREE.MeshStandardMaterial;
}

export interface HumanoidRig {
  group: THREE.Group;
  bodyRoot: THREE.Group;
  leftArm: THREE.Object3D;
  rightArm: THREE.Object3D;
  leftLeg: THREE.Object3D;
  rightLeg: THREE.Object3D;
  torsoMat: THREE.MeshStandardMaterial;
  pantsMat: THREE.MeshStandardMaterial;
  accentMat: THREE.MeshStandardMaterial;
  weaponGroup: THREE.Group;
  telegraphRing: THREE.Mesh;
}

// Shared reusable geometries for crisp performance
const wheelCylGeo = new THREE.CylinderGeometry(0.36, 0.36, 0.24, 14);
wheelCylGeo.rotateZ(Math.PI / 2);
const rimCylGeo = new THREE.CylinderGeometry(0.2, 0.2, 0.26, 10);
rimCylGeo.rotateZ(Math.PI / 2);

const tireMat = new THREE.MeshStandardMaterial({ color: 0x1a1d22, roughness: 0.9, metalness: 0.05 });
const rimMat = new THREE.MeshStandardMaterial({ color: 0xcbd5e1, roughness: 0.25, metalness: 0.85 });
const darkGlassMat = new THREE.MeshStandardMaterial({ color: 0x162230, roughness: 0.12, metalness: 0.7 });
const chromeMat = new THREE.MeshStandardMaterial({ color: 0xe2e8f0, roughness: 0.18, metalness: 0.9 });
const matteDarkMat = new THREE.MeshStandardMaterial({ color: 0x1e2228, roughness: 0.82, metalness: 0.15 });
const taillightMat = new THREE.MeshStandardMaterial({
  color: 0xdc2626,
  emissive: 0x7f1d1d,
  emissiveIntensity: 0.8,
  roughness: 0.25,
});

function addContactShadow(parent: THREE.Group, rx: number, rz: number, shadowTex: THREE.Texture): void {
  const geo = new THREE.PlaneGeometry(rx * 2, rz * 2);
  geo.rotateX(-Math.PI / 2);
  const mat = new THREE.MeshBasicMaterial({
    map: shadowTex,
    transparent: true,
    opacity: 0.52,
    depthWrite: false,
  });
  const mesh = new THREE.Mesh(geo, mat);
  mesh.position.y = 0.025;
  parent.add(mesh);
}

function makeWheel(scale = 1): THREE.Group {
  const g = new THREE.Group();
  const tire = new THREE.Mesh(wheelCylGeo, tireMat);
  tire.scale.setScalar(scale);
  const rim = new THREE.Mesh(rimCylGeo, rimMat);
  rim.scale.setScalar(scale);
  g.add(tire, rim);
  return g;
}

/**
 * Builds composite 6-12 part procedural 3D vehicles authentic to Dhaka streets.
 * Bottom-center pivot at y = 0, facing +Z in local coordinates.
 */
export function createVehicleRig(
  kind: VehicleKind,
  colorHex: number,
  textures: DhakaTextureSet,
  castShadow: boolean,
): VehicleRig {
  const group = new THREE.Group();
  const wheels: THREE.Object3D[] = [];
  const bodyMaterials: THREE.MeshStandardMaterial[] = [];
  const headlights: THREE.MeshStandardMaterial[] = [];

  const hlMat = new THREE.MeshStandardMaterial({
    color: 0xfff8dc,
    emissive: 0xffeba0,
    emissiveIntensity: 0.6,
    roughness: 0.15,
  });
  headlights.push(hlMat);

  const primaryMat = new THREE.MeshStandardMaterial({
    color: colorHex,
    roughness: kind === 'sedan' || kind === 'bike' ? 0.24 : 0.42,
    metalness: kind === 'sedan' || kind === 'bike' ? 0.65 : 0.35,
  });
  bodyMaterials.push(primaryMat);

  let sirenRed: THREE.MeshStandardMaterial | undefined;
  let sirenBlue: THREE.MeshStandardMaterial | undefined;

  if (kind === 'cng') {
    // Iconic Dhaka Green CNG Auto-Rickshaw (3-wheeler)
    addContactShadow(group, 1.15, 1.75, textures.contactShadowMap);
    const canvasTopMat = new THREE.MeshStandardMaterial({ color: 0x181c20, roughness: 0.88, metalness: 0.05 });
    const yellowTrimMat = new THREE.MeshStandardMaterial({ color: 0xfacc15, roughness: 0.4, metalness: 0.25 });
    const seatMat = new THREE.MeshStandardMaterial({ color: 0x262930, roughness: 0.82 });

    // Lower emerald chassis tub
    const chassis = new THREE.Mesh(new THREE.BoxGeometry(1.52, 0.58, 2.55), primaryMat);
    chassis.position.set(0, 0.57, -0.05);
    group.add(chassis);

    // Emerald rear engine cowling & passenger backrest bulkhead
    const rearDeck = new THREE.Mesh(new THREE.BoxGeometry(1.48, 0.48, 0.72), primaryMat);
    rearDeck.position.set(0, 1.02, -0.96);
    group.add(rearDeck);

    // Dark rear engine louver grille + spare tire
    const louver = new THREE.Mesh(new THREE.BoxGeometry(0.92, 0.26, 0.06), matteDarkMat);
    louver.position.set(0, 0.72, -1.34);
    group.add(louver);

    const spareTire = new THREE.Mesh(new THREE.CylinderGeometry(0.24, 0.24, 0.14, 12), tireMat);
    spareTire.rotation.x = Math.PI / 2;
    spareTire.position.set(0.45, 0.98, -1.36);
    group.add(spareTire);

    // Tapered front nose fairing
    const nose = new THREE.Mesh(new THREE.CylinderGeometry(0.45, 0.68, 0.94, 8), primaryMat);
    nose.position.set(0, 0.74, 1.2);
    group.add(nose);

    // Crisp Dhaka yellow identification stripe along sides
    [-0.77, 0.77].forEach((sx) => {
      const sideStripe = new THREE.Mesh(new THREE.BoxGeometry(0.04, 0.11, 2.38), yellowTrimMat);
      sideStripe.position.set(sx, 0.8, -0.05);
      group.add(sideStripe);
    });
    // Interior passenger bench seat
    const bench = new THREE.Mesh(new THREE.BoxGeometry(1.26, 0.24, 0.52), seatMat);
    bench.position.set(0, 0.94, -0.45);
    group.add(bench);

    // Windshield frame & glass
    const windshield = new THREE.Mesh(new THREE.BoxGeometry(1.25, 0.56, 0.08), darkGlassMat);
    windshield.position.set(0, 1.4, 0.98);
    windshield.rotation.x = -0.18;
    group.add(windshield);

    // Soft-top black canvas roof canopy with rear canvas drop
    const roof = new THREE.Mesh(new THREE.BoxGeometry(1.54, 0.2, 2.38), canvasTopMat);
    roof.position.set(0, 1.78, -0.12);
    group.add(roof);
    const rearCanvas = new THREE.Mesh(new THREE.BoxGeometry(1.48, 0.56, 0.08), canvasTopMat);
    rearCanvas.position.set(0, 1.45, -1.28);
    group.add(rearCanvas);

    // Side pillars + protective Dhaka wire-mesh door bars
    const pillarGeo = new THREE.BoxGeometry(0.08, 0.92, 0.08);
    [
      [-0.7, 1.3, 0.85],
      [0.7, 1.3, 0.85],
      [-0.7, 1.3, -1.22],
      [0.7, 1.3, -1.22],
    ].forEach(([px, py, pz]) => {
      const p = new THREE.Mesh(pillarGeo, matteDarkMat);
      p.position.set(px, py, pz);
      group.add(p);
    });

    // Twin front headlights + rear brake lights
    [-0.32, 0.32].forEach((hx) => {
      const hl = new THREE.Mesh(new THREE.CylinderGeometry(0.11, 0.11, 0.08, 10), hlMat);
      hl.rotation.x = Math.PI / 2;
      hl.position.set(hx, 0.84, 1.56);
      group.add(hl);
    });
    const tl = new THREE.Mesh(new THREE.BoxGeometry(0.95, 0.11, 0.06), taillightMat);
    tl.position.set(-0.18, 0.52, -1.35);
    group.add(tl);

    // 1 Front wheel + 2 Rear wheels
    const wF = makeWheel(0.76);
    wF.position.set(0, 0.28, 1.28);
    const wRL = makeWheel(0.78);
    wRL.position.set(-0.74, 0.29, -0.92);
    const wRR = makeWheel(0.78);
    wRR.position.set(0.74, 0.29, -0.92);
    wheels.push(wF, wRL, wRR);
    group.add(wF, wRL, wRR);
  } else if (kind === 'rickshaw') {
    // Iconic Bangladeshi Cycle Rickshaw with Folk-Art Canopy
    addContactShadow(group, 1.0, 1.65, textures.contactShadowMap);
    const goldMat = new THREE.MeshStandardMaterial({ color: 0xf59e0b, roughness: 0.3, metalness: 0.7 });
    const artPanelMat = new THREE.MeshStandardMaterial({
      map: textures.rickshawArtMap,
      roughness: 0.45,
      metalness: 0.1,
    });

    // Passenger carriage tub
    const seatBox = new THREE.Mesh(new THREE.BoxGeometry(1.16, 0.42, 1.05), primaryMat);
    seatBox.position.set(0, 0.62, -0.52);
    group.add(seatBox);

    // Plush seat backrest
    const backrest = new THREE.Mesh(new THREE.BoxGeometry(1.12, 0.55, 0.18), primaryMat);
    backrest.position.set(0, 1.04, -0.96);
    group.add(backrest);

    // Vibrant decorative folding hood canopy
    const hoodTop = new THREE.Mesh(new THREE.BoxGeometry(1.26, 0.16, 1.18), primaryMat);
    hoodTop.position.set(0, 1.72, -0.55);
    hoodTop.rotation.x = 0.08;
    group.add(hoodTop);
    const hoodBack = new THREE.Mesh(new THREE.BoxGeometry(1.24, 0.85, 0.12), goldMat);
    hoodBack.position.set(0, 1.32, -1.08);
    group.add(hoodBack);

    // Rear folk-art rickshaw painting plaque
    const artBoard = new THREE.Mesh(new THREE.BoxGeometry(1.14, 0.56, 0.05), artPanelMat);
    artBoard.position.set(0, 0.78, -1.12);
    group.add(artBoard);

    // Bicycle front frame & handlebars
    const frameTube = new THREE.Mesh(new THREE.BoxGeometry(0.1, 0.1, 1.35), matteDarkMat);
    frameTube.position.set(0, 0.58, 0.52);
    group.add(frameTube);
    const fork = new THREE.Mesh(new THREE.CylinderGeometry(0.04, 0.04, 0.95, 8), chromeMat);
    fork.position.set(0, 0.75, 1.12);
    fork.rotation.x = -0.2;
    group.add(fork);
    const handlebar = new THREE.Mesh(new THREE.CylinderGeometry(0.03, 0.03, 0.76, 8), chromeMat);
    handlebar.rotation.z = Math.PI / 2;
    handlebar.position.set(0, 1.18, 1.02);
    group.add(handlebar);

    const lamp = new THREE.Mesh(new THREE.SphereGeometry(0.08, 8, 8), hlMat);
    lamp.position.set(0, 1.06, 1.18);
    group.add(lamp);

    // 3 thin spoked-style wheels
    const wF = makeWheel(0.92);
    wF.scale.x = 0.45;
    wF.position.set(0, 0.34, 1.18);
    const wRL = makeWheel(1.0);
    wRL.scale.x = 0.45;
    wRL.position.set(-0.68, 0.36, -0.62);
    const wRR = makeWheel(1.0);
    wRR.scale.x = 0.45;
    wRR.position.set(0.68, 0.36, -0.62);
    wheels.push(wF, wRL, wRR);
    group.add(wF, wRL, wRR);
  } else if (kind === 'bus') {
    // Dhaka City Bus (BRTC / Local Express style)
    addContactShadow(group, 1.65, 4.4, textures.contactShadowMap);
    const creamMat = new THREE.MeshStandardMaterial({ color: 0xf4ecd8, roughness: 0.45, metalness: 0.15 });

    // Lower hull
    const lower = new THREE.Mesh(new THREE.BoxGeometry(2.52, 1.15, 7.9), primaryMat);
    lower.position.set(0, 0.98, 0);
    group.add(lower);
    // Upper cabin stripe
    const upper = new THREE.Mesh(new THREE.BoxGeometry(2.48, 1.32, 7.75), creamMat);
    upper.position.set(0, 2.2, -0.02);
    group.add(upper);
    // Continuous dark side window band + panoramic front windshield
    const sideWin = new THREE.Mesh(new THREE.BoxGeometry(2.54, 0.82, 6.8), darkGlassMat);
    sideWin.position.set(0, 2.15, -0.15);
    group.add(sideWin);
    const frontWin = new THREE.Mesh(new THREE.BoxGeometry(2.26, 0.95, 0.12), darkGlassMat);
    frontWin.position.set(0, 2.1, 3.86);
    group.add(frontWin);
    // Roof AC pods + luggage rail
    const ac1 = new THREE.Mesh(new THREE.BoxGeometry(1.5, 0.28, 1.8), matteDarkMat);
    ac1.position.set(0, 2.98, 1.1);
    const ac2 = new THREE.Mesh(new THREE.BoxGeometry(1.5, 0.28, 1.8), matteDarkMat);
    ac2.position.set(0, 2.98, -1.6);
    group.add(ac1, ac2);
    // Heavy chrome bumpers
    const fBumper = new THREE.Mesh(new THREE.BoxGeometry(2.6, 0.28, 0.22), chromeMat);
    fBumper.position.set(0, 0.56, 4.0);
    const rBumper = new THREE.Mesh(new THREE.BoxGeometry(2.6, 0.28, 0.22), chromeMat);
    rBumper.position.set(0, 0.56, -4.0);
    group.add(fBumper, rBumper);
    // Headlights & Taillights
    [-0.9, 0.9].forEach((hx) => {
      const hl = new THREE.Mesh(new THREE.BoxGeometry(0.38, 0.2, 0.08), hlMat);
      hl.position.set(hx, 0.92, 3.96);
      group.add(hl);
      const tl = new THREE.Mesh(new THREE.BoxGeometry(0.36, 0.22, 0.08), taillightMat);
      tl.position.set(hx, 0.95, -3.96);
      group.add(tl);
    });
    // 4 heavy bus tires
    [
      [-1.24, 0.48, 2.45],
      [1.24, 0.48, 2.45],
      [-1.24, 0.48, -2.35],
      [1.24, 0.48, -2.35],
    ].forEach(([wx, wy, wz]) => {
      const w = makeWheel(1.32);
      w.position.set(wx, wy, wz);
      wheels.push(w);
      group.add(w);
    });
  } else if (kind === 'truck') {
    // Painted Bangladeshi Cargo Truck (Tata/Bedford style)
    addContactShadow(group, 1.55, 3.45, textures.contactShadowMap);
    const woodBedMat = new THREE.MeshStandardMaterial({ color: 0x0f766e, roughness: 0.65, metalness: 0.15 });
    const stripeMat = new THREE.MeshStandardMaterial({ color: 0xfbbf24, roughness: 0.4, metalness: 0.3 });

    // Steel chassis frame
    const frame = new THREE.Mesh(new THREE.BoxGeometry(2.1, 0.35, 6.0), matteDarkMat);
    frame.position.set(0, 0.65, 0);
    group.add(frame);

    // Ornate cab + hood nose
    const cab = new THREE.Mesh(new THREE.BoxGeometry(2.26, 1.65, 1.75), primaryMat);
    cab.position.set(0, 1.62, 1.85);
    group.add(cab);
    const hood = new THREE.Mesh(new THREE.BoxGeometry(1.72, 0.92, 1.05), primaryMat);
    hood.position.set(0, 1.24, 2.95);
    group.add(hood);
    const cabWin = new THREE.Mesh(new THREE.BoxGeometry(2.05, 0.62, 0.1), darkGlassMat);
    cabWin.position.set(0, 1.92, 2.74);
    group.add(cabWin);
    const grille = new THREE.Mesh(new THREE.BoxGeometry(1.3, 0.55, 0.08), chromeMat);
    grille.position.set(0, 1.18, 3.49);
    group.add(grille);

    // Tall painted wooden cargo box
    const cargo = new THREE.Mesh(new THREE.BoxGeometry(2.38, 1.9, 3.95), woodBedMat);
    cargo.position.set(0, 1.78, -1.08);
    group.add(cargo);
    const stripe = new THREE.Mesh(new THREE.BoxGeometry(2.42, 0.24, 3.98), stripeMat);
    stripe.position.set(0, 1.55, -1.08);
    group.add(stripe);

    [-0.78, 0.78].forEach((hx) => {
      const hl = new THREE.Mesh(new THREE.CylinderGeometry(0.14, 0.14, 0.08, 10), hlMat);
      hl.rotation.x = Math.PI / 2;
      hl.position.set(hx, 1.04, 3.48);
      group.add(hl);
    });

    [
      [-1.14, 0.48, 2.15],
      [1.14, 0.48, 2.15],
      [-1.14, 0.48, -1.85],
      [1.14, 0.48, -1.85],
    ].forEach(([wx, wy, wz]) => {
      const w = makeWheel(1.28);
      w.position.set(wx, wy, wz);
      wheels.push(w);
      group.add(w);
    });
  } else if (kind === 'bike') {
    // Dhaka 150cc Street Motorcycle
    addContactShadow(group, 0.65, 1.35, textures.contactShadowMap);
    const engineBlock = new THREE.Mesh(new THREE.BoxGeometry(0.42, 0.46, 0.72), matteDarkMat);
    engineBlock.position.set(0, 0.52, -0.02);
    group.add(engineBlock);
    const tank = new THREE.Mesh(new THREE.BoxGeometry(0.46, 0.34, 0.82), primaryMat);
    tank.position.set(0, 0.96, 0.18);
    tank.rotation.x = -0.15;
    group.add(tank);
    const seat = new THREE.Mesh(new THREE.BoxGeometry(0.38, 0.16, 0.86), matteDarkMat);
    seat.position.set(0, 0.88, -0.52);
    group.add(seat);
    const cowl = new THREE.Mesh(new THREE.BoxGeometry(0.38, 0.38, 0.34), primaryMat);
    cowl.position.set(0, 1.05, 0.78);
    cowl.rotation.x = 0.25;
    group.add(cowl);
    const bars = new THREE.Mesh(new THREE.CylinderGeometry(0.03, 0.03, 0.82, 8), chromeMat);
    bars.rotation.z = Math.PI / 2;
    bars.position.set(0, 1.15, 0.64);
    group.add(bars);
    const exhaust = new THREE.Mesh(new THREE.CylinderGeometry(0.06, 0.08, 0.95, 8), chromeMat);
    exhaust.rotation.x = Math.PI / 2 - 0.15;
    exhaust.position.set(0.28, 0.48, -0.58);
    group.add(exhaust);
    const hl = new THREE.Mesh(new THREE.BoxGeometry(0.26, 0.16, 0.08), hlMat);
    hl.position.set(0, 1.02, 0.98);
    group.add(hl);

    const wF = makeWheel(0.88);
    wF.scale.x = 0.6;
    wF.position.set(0, 0.32, 0.82);
    const wR = makeWheel(0.9);
    wR.scale.x = 0.72;
    wR.position.set(0, 0.33, -0.78);
    wheels.push(wF, wR);
    group.add(wF, wR);
  } else if (kind === 'police') {
    // DMP (Dhaka Metropolitan Police) Patrol Pickup
    addContactShadow(group, 1.32, 2.65, textures.contactShadowMap);
    const navyMat = new THREE.MeshStandardMaterial({ color: 0x1e3a8a, roughness: 0.32, metalness: 0.45 });
    sirenRed = new THREE.MeshStandardMaterial({
      color: 0xef4444,
      emissive: 0xef4444,
      emissiveIntensity: 0.3,
      roughness: 0.2,
    });
    sirenBlue = new THREE.MeshStandardMaterial({
      color: 0x3b82f6,
      emissive: 0x3b82f6,
      emissiveIntensity: 0.3,
      roughness: 0.2,
    });

    const body = new THREE.Mesh(new THREE.BoxGeometry(2.04, 0.76, 4.65), primaryMat);
    body.position.set(0, 0.78, 0);
    group.add(body);
    const stripe = new THREE.Mesh(new THREE.BoxGeometry(2.08, 0.24, 4.55), navyMat);
    stripe.position.set(0, 0.78, 0);
    group.add(stripe);
    const cab = new THREE.Mesh(new THREE.BoxGeometry(1.88, 0.78, 1.95), primaryMat);
    cab.position.set(0, 1.5, 0.32);
    group.add(cab);
    const glass = new THREE.Mesh(new THREE.BoxGeometry(1.92, 0.54, 1.62), darkGlassMat);
    glass.position.set(0, 1.5, 0.36);
    group.add(glass);
    // Pickup bed rollbar & front pushbar
    const rollbar = new THREE.Mesh(new THREE.BoxGeometry(1.84, 0.72, 0.14), matteDarkMat);
    rollbar.position.set(0, 1.5, -0.74);
    group.add(rollbar);
    const pushbar = new THREE.Mesh(new THREE.BoxGeometry(1.45, 0.5, 0.16), matteDarkMat);
    pushbar.position.set(0, 0.8, 2.4);
    group.add(pushbar);
    // Lightbar
    const lbR = new THREE.Mesh(new THREE.BoxGeometry(0.54, 0.16, 0.24), sirenRed);
    lbR.position.set(-0.32, 1.96, 0.32);
    const lbB = new THREE.Mesh(new THREE.BoxGeometry(0.54, 0.16, 0.24), sirenBlue);
    lbB.position.set(0.32, 1.96, 0.32);
    group.add(lbR, lbB);

    [-0.72, 0.72].forEach((hx) => {
      const hl = new THREE.Mesh(new THREE.BoxGeometry(0.34, 0.16, 0.08), hlMat);
      hl.position.set(hx, 0.88, 2.34);
      group.add(hl);
      const tl = new THREE.Mesh(new THREE.BoxGeometry(0.32, 0.18, 0.08), taillightMat);
      tl.position.set(hx, 0.88, -2.34);
      group.add(tl);
    });

    [
      [-1.02, 0.4, 1.48],
      [1.02, 0.4, 1.48],
      [-1.02, 0.4, -1.42],
      [1.02, 0.4, -1.42],
    ].forEach(([wx, wy, wz]) => {
      const w = makeWheel(1.1);
      w.position.set(wx, wy, wz);
      wheels.push(w);
      group.add(w);
    });
  } else {
    // Gulshan GT Sports Sedan
    addContactShadow(group, 1.25, 2.45, textures.contactShadowMap);
    const body = new THREE.Mesh(new THREE.BoxGeometry(1.96, 0.64, 4.35), primaryMat);
    body.position.set(0, 0.66, 0);
    group.add(body);
    const cabin = new THREE.Mesh(new THREE.BoxGeometry(1.74, 0.62, 2.15), primaryMat);
    cabin.position.set(0, 1.24, -0.18);
    group.add(cabin);
    const windows = new THREE.Mesh(new THREE.BoxGeometry(1.78, 0.46, 1.88), darkGlassMat);
    windows.position.set(0, 1.24, -0.16);
    group.add(windows);
    const spoiler = new THREE.Mesh(new THREE.BoxGeometry(1.76, 0.08, 0.28), matteDarkMat);
    spoiler.position.set(0, 1.08, -2.02);
    group.add(spoiler);
    const grille = new THREE.Mesh(new THREE.BoxGeometry(1.12, 0.24, 0.08), matteDarkMat);
    grille.position.set(0, 0.64, 2.19);
    group.add(grille);

    [-0.68, 0.68].forEach((hx) => {
      const hl = new THREE.Mesh(new THREE.BoxGeometry(0.4, 0.14, 0.08), hlMat);
      hl.position.set(hx, 0.74, 2.18);
      group.add(hl);
      const tl = new THREE.Mesh(new THREE.BoxGeometry(0.46, 0.14, 0.08), taillightMat);
      tl.position.set(hx, 0.76, -2.18);
      group.add(tl);
    });

    [
      [-0.96, 0.36, 1.38],
      [0.96, 0.36, 1.38],
      [-0.96, 0.36, -1.38],
      [0.96, 0.36, -1.38],
    ].forEach(([wx, wy, wz]) => {
      const w = makeWheel(0.98);
      w.position.set(wx, wy, wz);
      wheels.push(w);
      group.add(w);
    });
  }

  if (castShadow) {
    group.traverse((obj) => {
      if ((obj as THREE.Mesh).isMesh) {
        obj.castShadow = true;
        obj.receiveShadow = true;
      }
    });
  }

  return { group, wheels, bodyMaterials, headlights, sirenRed, sirenBlue };
}

/**
 * Builds an articulated 10-part humanoid rig (player, police, syndicate, civilian).
 */
export function createHumanoidRig(
  torsoColor: number,
  pantsColor: number,
  accentColor: number,
  textures: DhakaTextureSet,
  options: { isPolice?: boolean; isBoss?: boolean; isPlayer?: boolean } = {},
): HumanoidRig {
  const group = new THREE.Group();
  const bodyRoot = new THREE.Group();
  group.add(bodyRoot);

  addContactShadow(group, 0.46, 0.46, textures.contactShadowMap);

  const skinMat = new THREE.MeshStandardMaterial({ color: 0xb9805a, roughness: 0.75, metalness: 0.02 });
  const hairMat = new THREE.MeshStandardMaterial({ color: 0x16161a, roughness: 0.88, metalness: 0.05 });
  const torsoMat = new THREE.MeshStandardMaterial({ color: torsoColor, roughness: 0.72, metalness: 0.08 });
  const pantsMat = new THREE.MeshStandardMaterial({ color: pantsColor, roughness: 0.8, metalness: 0.05 });
  const accentMat = new THREE.MeshStandardMaterial({ color: accentColor, roughness: 0.45, metalness: 0.25 });
  const shoeMat = new THREE.MeshStandardMaterial({ color: 0x1a1c20, roughness: 0.7 });

  const scale = options.isBoss ? 1.18 : 1.0;
  bodyRoot.scale.setScalar(scale);

  // Legs (pivoted at hip y = 0.88)
  const makeLeg = (xOff: number): THREE.Group => {
    const hip = new THREE.Group();
    hip.position.set(xOff, 0.88, 0);
    const leg = new THREE.Mesh(new THREE.BoxGeometry(0.22, 0.78, 0.24), pantsMat);
    leg.position.y = -0.39;
    const shoe = new THREE.Mesh(new THREE.BoxGeometry(0.23, 0.14, 0.34), shoeMat);
    shoe.position.set(0, -0.8, 0.05);
    hip.add(leg, shoe);
    return hip;
  };
  const leftLeg = makeLeg(-0.14);
  const rightLeg = makeLeg(0.14);
  bodyRoot.add(leftLeg, rightLeg);

  // Torso + front shirt/jacket placket (cleanly offset on +Z so zero back Z-fighting)
  const torso = new THREE.Mesh(new THREE.BoxGeometry(0.54, 0.66, 0.3), torsoMat);
  torso.position.set(0, 1.22, 0);
  bodyRoot.add(torso);

  const chestDetail = new THREE.Mesh(new THREE.BoxGeometry(0.22, 0.56, 0.05), accentMat);
  chestDetail.position.set(0, 1.22, 0.155);
  bodyRoot.add(chestDetail);

  // Player street satchel / backpack on back (-Z) for distinct 3rd-person hero silhouette
  if (options.isPlayer) {
    const pack = new THREE.Mesh(new THREE.BoxGeometry(0.36, 0.44, 0.16), matteDarkMat);
    pack.position.set(0, 1.24, -0.21);
    const packTrim = new THREE.Mesh(new THREE.BoxGeometry(0.37, 0.08, 0.17), accentMat);
    packTrim.position.set(0, 1.32, -0.21);
    bodyRoot.add(pack, packTrim);
  }

  // Head + Hair / Cap + Sunglasses
  const head = new THREE.Mesh(new THREE.BoxGeometry(0.32, 0.34, 0.32), skinMat);
  head.position.set(0, 1.74, 0);
  bodyRoot.add(head);

  const hair = new THREE.Mesh(new THREE.BoxGeometry(0.35, 0.14, 0.35), options.isPolice ? torsoMat : hairMat);
  hair.position.set(0, 1.9, -0.01);
  bodyRoot.add(hair);

  if (options.isPolice) {
    const brim = new THREE.Mesh(new THREE.BoxGeometry(0.34, 0.04, 0.18), matteDarkMat);
    brim.position.set(0, 1.84, 0.2);
    bodyRoot.add(brim);
  }
  if (options.isPlayer || options.isBoss) {
    const shades = new THREE.Mesh(new THREE.BoxGeometry(0.28, 0.07, 0.06), matteDarkMat);
    shades.position.set(0, 1.76, 0.16);
    bodyRoot.add(shades);
  }

  // Arms (pivoted at shoulder y = 1.48)
  const makeArm = (xOff: number): THREE.Group => {
    const shoulder = new THREE.Group();
    shoulder.position.set(xOff, 1.48, 0);
    const sleeve = new THREE.Mesh(new THREE.BoxGeometry(0.18, 0.44, 0.2), torsoMat);
    sleeve.position.y = -0.22;
    const hand = new THREE.Mesh(new THREE.BoxGeometry(0.15, 0.22, 0.16), skinMat);
    hand.position.y = -0.52;
    shoulder.add(sleeve, hand);
    return shoulder;
  };
  const leftArm = makeArm(-0.37);
  const rightArm = makeArm(0.37);
  bodyRoot.add(leftArm, rightArm);

  // Weapon mount on right hand
  const weaponGroup = new THREE.Group();
  weaponGroup.position.set(0, -0.56, 0.18);
  rightArm.add(weaponGroup);

  // Ground attack telegraph ring (hidden unless winding up heavy attack)
  const ringGeo = new THREE.RingGeometry(0.55, 0.85, 24);
  ringGeo.rotateX(-Math.PI / 2);
  const ringMat = new THREE.MeshBasicMaterial({
    color: 0xff3b30,
    transparent: true,
    opacity: 0.78,
    side: THREE.DoubleSide,
    depthWrite: false,
  });
  const telegraphRing = new THREE.Mesh(ringGeo, ringMat);
  telegraphRing.position.y = 0.04;
  telegraphRing.visible = false;
  group.add(telegraphRing);

  return {
    group,
    bodyRoot,
    leftArm,
    rightArm,
    leftLeg,
    rightLeg,
    torsoMat,
    pantsMat,
    accentMat,
    weaponGroup,
    telegraphRing,
  };
}

export function setHumanoidOutfit(rig: HumanoidRig, outfitId: OutfitId): void {
  const spec = OUTFIT_SPECS[outfitId];
  rig.torsoMat.color.setHex(spec.torsoColor);
  rig.pantsMat.color.setHex(spec.pantsColor);
  rig.accentMat.color.setHex(spec.accentColor);
}

export function setHumanoidWeaponMesh(rig: HumanoidRig, weapon: WeaponId): void {
  while (rig.weaponGroup.children.length > 0) {
    rig.weaponGroup.remove(rig.weaponGroup.children[0]);
  }
  if (weapon === 'fists') return;

  const gunMetal = new THREE.MeshStandardMaterial({ color: 0x20242c, roughness: 0.35, metalness: 0.85 });
  const woodGrip = new THREE.MeshStandardMaterial({ color: 0x6b3e26, roughness: 0.75 });

  if (weapon === 'pistol') {
    const barrel = new THREE.Mesh(new THREE.BoxGeometry(0.08, 0.1, 0.34), gunMetal);
    barrel.position.set(0, 0.04, 0.12);
    const grip = new THREE.Mesh(new THREE.BoxGeometry(0.07, 0.16, 0.1), matteDarkMat);
    grip.position.set(0, -0.04, 0);
    rig.weaponGroup.add(barrel, grip);
  } else if (weapon === 'smg') {
    const body = new THREE.Mesh(new THREE.BoxGeometry(0.1, 0.14, 0.56), gunMetal);
    body.position.set(0, 0.05, 0.22);
    const mag = new THREE.Mesh(new THREE.BoxGeometry(0.06, 0.26, 0.08), gunMetal);
    mag.position.set(0, -0.1, 0.16);
    rig.weaponGroup.add(body, mag);
  } else if (weapon === 'shotgun') {
    const barrel = new THREE.Mesh(new THREE.CylinderGeometry(0.045, 0.045, 0.84, 8), gunMetal);
    barrel.rotation.x = Math.PI / 2;
    barrel.position.set(0, 0.06, 0.36);
    const stock = new THREE.Mesh(new THREE.BoxGeometry(0.09, 0.14, 0.32), woodGrip);
    stock.position.set(0, 0.02, -0.05);
    rig.weaponGroup.add(barrel, stock);
  }
}

/**
 * Iconic Shapla (Water Lily) Monument for Dhaka Crossing Roundabout (11m tall hero landmark).
 */
export function createShaplaMonument(): THREE.Group {
  const g = new THREE.Group();

  const stoneMat = new THREE.MeshStandardMaterial({ color: 0xd6cfc2, roughness: 0.72, metalness: 0.08 });
  const waterMat = new THREE.MeshStandardMaterial({
    color: 0x0d9488,
    roughness: 0.12,
    metalness: 0.35,
  });
  const petalMat = new THREE.MeshStandardMaterial({
    color: 0xf8fafc,
    roughness: 0.28,
    metalness: 0.18,
    emissive: 0x1e293b,
    emissiveIntensity: 0.15,
  });
  const goldMat = new THREE.MeshStandardMaterial({
    color: 0xfbbf24,
    roughness: 0.22,
    metalness: 0.85,
    emissive: 0x92400e,
    emissiveIntensity: 0.25,
  });

  // Circular stepped fountain basin
  const baseOuter = new THREE.Mesh(new THREE.CylinderGeometry(4.4, 4.7, 0.65, 24), stoneMat);
  baseOuter.position.y = 0.325;
  g.add(baseOuter);

  const pool = new THREE.Mesh(new THREE.CylinderGeometry(3.85, 3.85, 0.52, 24), waterMat);
  pool.position.y = 0.42;
  g.add(pool);

  const pedestal = new THREE.Mesh(new THREE.CylinderGeometry(1.4, 2.0, 2.8, 16), stoneMat);
  pedestal.position.y = 1.8;
  g.add(pedestal);

  // Outer ring of 8 sculpted lily petals
  const petalGeo = new THREE.ConeGeometry(0.85, 3.8, 6);
  for (let i = 0; i < 8; i++) {
    const angle = (i / 8) * Math.PI * 2;
    const petalPivot = new THREE.Group();
    petalPivot.position.set(0, 3.2, 0);
    petalPivot.rotation.y = angle;
    const petal = new THREE.Mesh(petalGeo, petalMat);
    petal.position.set(0, 1.5, 1.3);
    petal.rotation.x = 0.58;
    petalPivot.add(petal);
    g.add(petalPivot);
  }

  // Inner ring of 6 steeper lily petals
  for (let i = 0; i < 6; i++) {
    const angle = (i / 6) * Math.PI * 2 + 0.3;
    const petalPivot = new THREE.Group();
    petalPivot.position.set(0, 3.8, 0);
    petalPivot.rotation.y = angle;
    const petal = new THREE.Mesh(petalGeo, petalMat);
    petal.scale.set(0.75, 1.15, 0.75);
    petal.position.set(0, 2.0, 0.65);
    petal.rotation.x = 0.24;
    petalPivot.add(petal);
    g.add(petalPivot);
  }

  // Central golden spire crown
  const crown = new THREE.Mesh(new THREE.ConeGeometry(0.55, 3.2, 8), goldMat);
  crown.position.y = 7.4;
  g.add(crown);

  return g;
}

/**
 * Traditional Bangladeshi wooden Buriganga River boat (Nouka) with bamboo canopy (Chhoi).
 */
export function createBurigangaBoat(): THREE.Group {
  const g = new THREE.Group();
  const hullMat = new THREE.MeshStandardMaterial({ color: 0x4a2c1d, roughness: 0.85 });
  const trimMat = new THREE.MeshStandardMaterial({ color: 0xd97706, roughness: 0.55 });
  const canopyMat = new THREE.MeshStandardMaterial({ color: 0xa16207, roughness: 0.9 });

  const hull = new THREE.Mesh(new THREE.BoxGeometry(1.75, 0.55, 5.4), hullMat);
  hull.position.y = 0.25;
  g.add(hull);

  const bow = new THREE.Mesh(new THREE.ConeGeometry(0.95, 1.7, 4), hullMat);
  bow.rotation.x = Math.PI / 2;
  bow.rotation.y = Math.PI / 4;
  bow.position.set(0, 0.35, 3.4);
  g.add(bow);

  const stern = new THREE.Mesh(new THREE.ConeGeometry(0.95, 1.5, 4), hullMat);
  stern.rotation.x = -Math.PI / 2;
  stern.rotation.y = Math.PI / 4;
  stern.position.set(0, 0.35, -3.3);
  g.add(stern);

  const canopy = new THREE.Mesh(new THREE.CylinderGeometry(0.88, 0.88, 2.6, 10, 1, false, 0, Math.PI), canopyMat);
  canopy.rotation.z = Math.PI / 2;
  canopy.rotation.y = Math.PI / 2;
  canopy.position.set(0, 0.68, -0.2);
  g.add(canopy);

  const stripe = new THREE.Mesh(new THREE.BoxGeometry(1.8, 0.14, 5.1), trimMat);
  stripe.position.y = 0.48;
  g.add(stripe);

  return g;
}
