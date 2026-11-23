import * as THREE from 'three';
import { OutfitId, OUTFIT_SPECS, VehicleKind, WeaponId } from './types';
import { DhakaTextureSet } from './textures';
import { CharacterAnimator } from './characterAnimator';
import { DhakaCharacterGlbTemplate, instantiateGlbCharacter } from './assetLoader';

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
  animator: CharacterAnimator;
  isGlbBacked: boolean;
  baseScale: number;
  isPlayer: boolean;
  isPolice: boolean;
  isBoss: boolean;
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
    roughnessMap: kind === 'cng' || kind === 'rickshaw' || kind === 'bus' || kind === 'truck' ? textures.metalWearRoughness : null,
    roughness: kind === 'sedan' || kind === 'bike' ? 0.24 : 0.52,
    metalness: kind === 'sedan' || kind === 'bike' ? 0.65 : 0.36,
  });
  bodyMaterials.push(primaryMat);

  let sirenRed: THREE.MeshStandardMaterial | undefined;
  let sirenBlue: THREE.MeshStandardMaterial | undefined;

  if (kind === 'cng') {
    // Iconic Dhaka Green CNG Auto-Rickshaw (sculpted 3-wheeler with arched soft-top & protective cage)
    addContactShadow(group, 1.18, 1.78, textures.contactShadowMap);
    const canvasTopMat = new THREE.MeshStandardMaterial({
      color: 0x1a1e24,
      roughness: 0.9,
      metalness: 0.04,
      normalMap: textures.fabricNormal,
      normalScale: new THREE.Vector2(0.45, 0.45),
    });
    const yellowTrimMat = new THREE.MeshStandardMaterial({ color: 0xfacc15, roughness: 0.38, metalness: 0.22 });
    const seatMat = new THREE.MeshStandardMaterial({ color: 0x262930, roughness: 0.84 });
    const plateMat = new THREE.MeshStandardMaterial({ color: 0x0c6b42, roughness: 0.35, metalness: 0.2 });

    // Lower emerald chassis tub with beveled front apron
    const chassis = new THREE.Mesh(new THREE.BoxGeometry(1.52, 0.56, 2.52), primaryMat);
    chassis.position.set(0, 0.56, -0.06);
    group.add(chassis);

    // Emerald rear engine cowling & passenger backrest bulkhead
    const rearDeck = new THREE.Mesh(new THREE.BoxGeometry(1.48, 0.5, 0.72), primaryMat);
    rearDeck.position.set(0, 1.02, -0.96);
    group.add(rearDeck);

    // Dark rear engine louver grille + Dhaka green number plate + spare tire
    const louver = new THREE.Mesh(new THREE.BoxGeometry(0.92, 0.26, 0.06), matteDarkMat);
    louver.position.set(0, 0.72, -1.34);
    group.add(louver);

    const rearPlate = new THREE.Mesh(new THREE.BoxGeometry(0.42, 0.16, 0.04), plateMat);
    rearPlate.position.set(-0.28, 0.96, -1.34);
    group.add(rearPlate);

    const spareTire = new THREE.Mesh(new THREE.CylinderGeometry(0.24, 0.24, 0.14, 12), tireMat);
    spareTire.rotation.x = Math.PI / 2;
    spareTire.position.set(0.45, 0.98, -1.36);
    group.add(spareTire);

    // Sculpted tapered front nose fairing + front wheel curved fender mudguard + chrome bull-bar
    const nose = new THREE.Mesh(new THREE.CylinderGeometry(0.46, 0.7, 0.96, 12), primaryMat);
    nose.position.set(0, 0.74, 1.18);
    group.add(nose);

    const frontFender = new THREE.Mesh(
      new THREE.CylinderGeometry(0.35, 0.35, 0.36, 10, 1, false, 0, Math.PI),
      primaryMat,
    );
    frontFender.rotation.z = Math.PI / 2;
    frontFender.position.set(0, 0.34, 1.32);
    group.add(frontFender);

    const frontBumper = new THREE.Mesh(new THREE.CylinderGeometry(0.032, 0.032, 1.44, 8), chromeMat);
    frontBumper.rotation.z = Math.PI / 2;
    frontBumper.position.set(0, 0.44, 1.62);
    group.add(frontBumper);

    // Crisp Dhaka yellow identification stripe + fare meter box inside cab
    [-0.77, 0.77].forEach((sx) => {
      const sideStripe = new THREE.Mesh(new THREE.BoxGeometry(0.04, 0.11, 2.38), yellowTrimMat);
      sideStripe.position.set(sx, 0.8, -0.05);
      group.add(sideStripe);
    });
    const meterBox = new THREE.Mesh(new THREE.BoxGeometry(0.22, 0.16, 0.14), matteDarkMat);
    meterBox.position.set(0.36, 1.14, 0.82);
    group.add(meterBox);

    // Interior passenger bench seat & driver saddle
    const bench = new THREE.Mesh(new THREE.BoxGeometry(1.26, 0.24, 0.52), seatMat);
    bench.position.set(0, 0.94, -0.45);
    const driverSeat = new THREE.Mesh(new THREE.BoxGeometry(0.48, 0.18, 0.42), seatMat);
    driverSeat.position.set(0, 0.92, 0.36);
    group.add(bench, driverSeat);

    // Windshield chrome surround & sloped tinted glass
    const wsFrame = new THREE.Mesh(new THREE.BoxGeometry(1.36, 0.62, 0.06), primaryMat);
    wsFrame.position.set(0, 1.38, 0.96);
    wsFrame.rotation.x = -0.18;
    const windshield = new THREE.Mesh(new THREE.BoxGeometry(1.22, 0.5, 0.08), darkGlassMat);
    windshield.position.set(0, 1.4, 0.98);
    windshield.rotation.x = -0.18;
    group.add(wsFrame, windshield);

    // Sculpted Arched Soft-Top Black Canvas Canopy (gently domed roof flush with pillars)
    const canopyGeo = new THREE.CylinderGeometry(0.77, 0.77, 2.38, 12, 1, false, -Math.PI / 2, Math.PI);
    canopyGeo.rotateX(-Math.PI / 2);
    const canopyArch = new THREE.Mesh(canopyGeo, canvasTopMat);
    canopyArch.scale.set(1, 0.28, 1);
    canopyArch.position.set(0, 1.74, -0.12);
    group.add(canopyArch);

    const canopyEdge = new THREE.Mesh(new THREE.BoxGeometry(1.54, 0.12, 2.38), canvasTopMat);
    canopyEdge.position.set(0, 1.7, -0.12);
    group.add(canopyEdge);

    const rearCanvas = new THREE.Mesh(new THREE.BoxGeometry(1.48, 0.54, 0.08), canvasTopMat);
    rearCanvas.position.set(0, 1.44, -1.28);
    group.add(rearCanvas);

    // Side pillars + signature Dhaka protective steel door cage bars
    const pillarGeo = new THREE.CylinderGeometry(0.036, 0.036, 0.94, 6);
    [
      [-0.71, 1.28, 0.85],
      [0.71, 1.28, 0.85],
      [-0.71, 1.28, -0.02],
      [0.71, 1.28, -0.02],
      [-0.71, 1.28, -1.22],
      [0.71, 1.28, -1.22],
    ].forEach(([px, py, pz]) => {
      const p = new THREE.Mesh(pillarGeo, matteDarkMat);
      p.position.set(px, py, pz);
      group.add(p);
    });
    // Horizontal & diagonal Dhaka passenger safety cage bars on sides
    [-0.74, 0.74].forEach((sx) => {
      const rail1 = new THREE.Mesh(new THREE.CylinderGeometry(0.018, 0.018, 0.84, 6), chromeMat);
      rail1.rotation.x = Math.PI / 2;
      rail1.position.set(sx, 1.12, -0.46);
      const rail2 = new THREE.Mesh(new THREE.CylinderGeometry(0.018, 0.018, 0.84, 6), chromeMat);
      rail2.rotation.x = Math.PI / 2;
      rail2.position.set(sx, 1.36, -0.46);
      group.add(rail1, rail2);
    });

    // Twin front headlights in chrome bezels + rear brake lights
    [-0.32, 0.32].forEach((hx) => {
      const bezel = new THREE.Mesh(new THREE.CylinderGeometry(0.13, 0.13, 0.07, 10), chromeMat);
      bezel.rotation.x = Math.PI / 2;
      bezel.position.set(hx, 0.84, 1.54);
      const hl = new THREE.Mesh(new THREE.CylinderGeometry(0.1, 0.1, 0.08, 10), hlMat);
      hl.rotation.x = Math.PI / 2;
      hl.position.set(hx, 0.84, 1.56);
      group.add(bezel, hl);
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
    // Iconic Bangladeshi Cycle Rickshaw with Pleated Curved Folding Hood & Folk-Art Plaque
    addContactShadow(group, 1.02, 1.68, textures.contactShadowMap);
    const goldMat = new THREE.MeshStandardMaterial({ color: 0xf59e0b, roughness: 0.28, metalness: 0.72 });
    const cushionMat = new THREE.MeshStandardMaterial({
      color: 0x991b1b,
      roughness: 0.78,
      normalMap: textures.fabricNormal,
      normalScale: new THREE.Vector2(0.4, 0.4),
    });
    const artPanelMat = new THREE.MeshStandardMaterial({
      map: textures.rickshawArtMap,
      roughness: 0.42,
      metalness: 0.12,
    });

    // Passenger carriage tub with decorative brass footstep
    const seatBox = new THREE.Mesh(new THREE.BoxGeometry(1.16, 0.4, 1.05), primaryMat);
    seatBox.position.set(0, 0.62, -0.52);
    const footboard = new THREE.Mesh(new THREE.BoxGeometry(1.06, 0.08, 0.42), matteDarkMat);
    footboard.position.set(0, 0.42, 0.16);
    group.add(seatBox, footboard);

    // Cushioned passenger seat & backrest with brass side armrests
    const seatCushion = new THREE.Mesh(new THREE.BoxGeometry(1.04, 0.14, 0.68), cushionMat);
    seatCushion.position.set(0, 0.86, -0.52);
    const backrest = new THREE.Mesh(new THREE.BoxGeometry(1.08, 0.56, 0.18), cushionMat);
    backrest.position.set(0, 1.08, -0.96);
    group.add(seatCushion, backrest);

    [-0.58, 0.58].forEach((ax) => {
      const arm = new THREE.Mesh(new THREE.CylinderGeometry(0.03, 0.03, 0.85, 6), goldMat);
      arm.rotation.x = Math.PI / 2;
      arm.position.set(ax, 1.04, -0.52);
      group.add(arm);
    });

    // Curved Pleated Folding Hood Canopy (3 arched ribs + curved roof shell)
    const hoodGeo = new THREE.CylinderGeometry(0.63, 0.63, 1.04, 12, 1, false, -Math.PI / 2, Math.PI);
    hoodGeo.rotateX(-Math.PI / 2);
    const hoodShell = new THREE.Mesh(hoodGeo, primaryMat);
    hoodShell.scale.set(1, 0.46, 1);
    hoodShell.position.set(0, 1.64, -0.56);
    group.add(hoodShell);

    [-0.1, -0.55, -1.0].forEach((rz) => {
      const rib = new THREE.Mesh(new THREE.TorusGeometry(0.62, 0.024, 6, 14, Math.PI), goldMat);
      rib.scale.set(1, 0.48, 1);
      rib.position.set(0, 1.64, rz);
      group.add(rib);
    });

    const hoodBack = new THREE.Mesh(new THREE.BoxGeometry(1.24, 0.82, 0.1), goldMat);
    hoodBack.position.set(0, 1.32, -1.08);
    group.add(hoodBack);

    // Side canopy support struts
    [-0.6, 0.6].forEach((sx) => {
      const strut = new THREE.Mesh(new THREE.CylinderGeometry(0.02, 0.02, 0.82, 6), chromeMat);
      strut.position.set(sx, 1.24, -0.22);
      strut.rotation.x = 0.28;
      group.add(strut);
    });

    // Rear folk-art rickshaw painting plaque + decorative fender arches
    const artBoard = new THREE.Mesh(new THREE.BoxGeometry(1.14, 0.56, 0.05), artPanelMat);
    artBoard.position.set(0, 0.78, -1.12);
    group.add(artBoard);

    [-0.68, 0.68].forEach((fx) => {
      const mudguard = new THREE.Mesh(
        new THREE.CylinderGeometry(0.4, 0.4, 0.12, 10, 1, false, 0, Math.PI),
        goldMat,
      );
      mudguard.rotation.z = Math.PI / 2;
      mudguard.position.set(fx, 0.36, -0.62);
      group.add(mudguard);
    });

    // Bicycle front frame, saddle, chrome handlebars & brass bell
    const frameTube = new THREE.Mesh(new THREE.CylinderGeometry(0.036, 0.036, 1.32, 8), matteDarkMat);
    frameTube.rotation.x = Math.PI / 2 - 0.12;
    frameTube.position.set(0, 0.66, 0.5);
    const pullerSeat = new THREE.Mesh(new THREE.BoxGeometry(0.22, 0.09, 0.28), matteDarkMat);
    pullerSeat.position.set(0, 0.96, 0.48);
    group.add(frameTube, pullerSeat);

    const fork = new THREE.Mesh(new THREE.CylinderGeometry(0.035, 0.035, 0.96, 8), chromeMat);
    fork.position.set(0, 0.75, 1.12);
    fork.rotation.x = -0.2;
    group.add(fork);
    const handlebar = new THREE.Mesh(new THREE.CylinderGeometry(0.026, 0.026, 0.76, 8), chromeMat);
    handlebar.rotation.z = Math.PI / 2;
    handlebar.position.set(0, 1.18, 1.02);
    group.add(handlebar);

    const brassBell = new THREE.Mesh(new THREE.SphereGeometry(0.045, 8, 6), goldMat);
    brassBell.position.set(0.24, 1.22, 1.02);
    const lamp = new THREE.Mesh(new THREE.SphereGeometry(0.08, 8, 8), hlMat);
    lamp.position.set(0, 1.06, 1.18);
    group.add(brassBell, lamp);

    // 3 thin spoked-style wheels
    const wF = makeWheel(0.92);
    wF.scale.x = 0.42;
    wF.position.set(0, 0.34, 1.18);
    const wRL = makeWheel(1.0);
    wRL.scale.x = 0.42;
    wRL.position.set(-0.68, 0.36, -0.62);
    const wRR = makeWheel(1.0);
    wRR.scale.x = 0.42;
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

// Shared sculpted procedural fallback humanoid geometries & materials (zero redundant allocations)
const sharedSkinMat = new THREE.MeshStandardMaterial({ color: 0xb9805a, roughness: 0.74, metalness: 0.02 });
const sharedHairMat = new THREE.MeshStandardMaterial({ color: 0x16161a, roughness: 0.86, metalness: 0.06 });
const sharedShoeMat = new THREE.MeshStandardMaterial({ color: 0x1a1c20, roughness: 0.68, metalness: 0.08 });
const procTorsoGeo = new THREE.CylinderGeometry(0.23, 0.19, 0.62, 10);
procTorsoGeo.scale(1.06, 1, 0.68);
const procPelvisGeo = new THREE.SphereGeometry(0.19, 10, 6);
procPelvisGeo.scale(1.04, 0.78, 0.72);
const procLegGeo = new THREE.CapsuleGeometry(0.086, 0.56, 4, 8);
const procShoeGeo = new THREE.CapsuleGeometry(0.068, 0.14, 4, 6);
procShoeGeo.rotateX(Math.PI / 2);
procShoeGeo.scale(1.0, 0.72, 1.05);
const procArmGeo = new THREE.CapsuleGeometry(0.068, 0.28, 4, 8);
const procHandGeo = new THREE.CapsuleGeometry(0.052, 0.16, 4, 6);
const procHeadGeo = new THREE.SphereGeometry(0.145, 10, 8);
procHeadGeo.scale(0.92, 1.1, 0.98);
const procHairGeo = new THREE.SphereGeometry(0.152, 10, 7, 0, Math.PI * 2, 0, Math.PI * 0.62);
procHairGeo.scale(0.95, 1.08, 1.04);

function attachRoleAccessories(
  target: { chest: THREE.Object3D; head: THREE.Object3D },
  accentMat: THREE.MeshStandardMaterial,
  options: { isPlayer?: boolean; isPolice?: boolean; isBoss?: boolean },
  isGlbBoneSpace: boolean,
): void {
  if (options.isPlayer) {
    // Player street satchel / backpack on back (-Z)
    const pack = new THREE.Mesh(new THREE.BoxGeometry(0.32, 0.4, 0.15), matteDarkMat);
    const packTrim = new THREE.Mesh(new THREE.BoxGeometry(0.33, 0.07, 0.16), accentMat);
    if (isGlbBoneSpace) {
      pack.position.set(0, 0.0, -0.19);
      packTrim.position.set(0, 0.08, -0.19);
    } else {
      pack.position.set(0, 1.24, -0.2);
      packTrim.position.set(0, 1.32, -0.2);
    }
    target.chest.add(pack, packTrim);
  }
  if (options.isPolice) {
    const brim = new THREE.Mesh(new THREE.BoxGeometry(0.28, 0.035, 0.16), matteDarkMat);
    brim.position.set(0, isGlbBoneSpace ? 0.12 : 1.82, 0.16);
    target.head.add(brim);
  }
  if (options.isPlayer || options.isBoss) {
    const shades = new THREE.Mesh(new THREE.BoxGeometry(0.24, 0.058, 0.055), matteDarkMat);
    shades.position.set(0, isGlbBoneSpace ? 0.06 : 1.74, 0.138);
    target.head.add(shades);
  }
}

/**
 * Builds a sculpted articulated humanoid rig (instant fallback while async GLB loads, or when GLB unavailable).
 */
export function createHumanoidRig(
  torsoColor: number,
  pantsColor: number,
  accentColor: number,
  textures: DhakaTextureSet,
  options: { isPolice?: boolean; isBoss?: boolean; isPlayer?: boolean; castShadow?: boolean } = {},
): HumanoidRig {
  const group = new THREE.Group();
  const bodyRoot = new THREE.Group();
  group.add(bodyRoot);

  addContactShadow(group, 0.46, 0.46, textures.contactShadowMap);

  const torsoMat = new THREE.MeshStandardMaterial({
    color: torsoColor,
    roughness: 0.72,
    metalness: 0.06,
    normalMap: textures.fabricNormal,
    normalScale: new THREE.Vector2(0.32, 0.32),
  });
  const pantsMat = new THREE.MeshStandardMaterial({
    color: pantsColor,
    roughness: 0.82,
    metalness: 0.04,
    normalMap: textures.fabricNormal,
    normalScale: new THREE.Vector2(0.25, 0.25),
  });
  const accentMat = new THREE.MeshStandardMaterial({ color: accentColor, roughness: 0.44, metalness: 0.24 });

  const baseScale = options.isBoss ? 1.18 : 1.0;
  bodyRoot.scale.setScalar(baseScale);

  // Legs (pivoted at hip y = 0.88)
  const makeLeg = (xOff: number): THREE.Group => {
    const hip = new THREE.Group();
    hip.position.set(xOff, 0.88, 0);
    const leg = new THREE.Mesh(procLegGeo, pantsMat);
    leg.position.y = -0.39;
    const shoe = new THREE.Mesh(procShoeGeo, sharedShoeMat);
    shoe.position.set(0, -0.82, 0.05);
    hip.add(leg, shoe);
    return hip;
  };
  const leftLeg = makeLeg(-0.12);
  const rightLeg = makeLeg(0.12);
  bodyRoot.add(leftLeg, rightLeg);

  const pelvis = new THREE.Mesh(procPelvisGeo, pantsMat);
  pelvis.position.set(0, 0.89, 0);
  bodyRoot.add(pelvis);

  // Sculpted Torso + front shirt/jacket placket
  const torsoGroup = new THREE.Group();
  const torso = new THREE.Mesh(procTorsoGeo, torsoMat);
  torso.position.set(0, 1.24, 0);
  const chestDetail = new THREE.Mesh(new THREE.BoxGeometry(0.14, 0.48, 0.04), accentMat);
  chestDetail.position.set(0, 1.25, 0.148);
  torsoGroup.add(torso, chestDetail);
  bodyRoot.add(torsoGroup);

  // Head + Hair / Cap + Sunglasses
  const head = new THREE.Mesh(procHeadGeo, sharedSkinMat);
  head.position.set(0, 1.72, 0.01);
  const hair = new THREE.Mesh(procHairGeo, options.isPolice ? torsoMat : sharedHairMat);
  hair.position.set(0, 1.74, -0.01);
  bodyRoot.add(head, hair);

  attachRoleAccessories({ chest: torsoGroup, head: bodyRoot }, accentMat, options, false);

  // Arms (pivoted at shoulder y = 1.46)
  const makeArm = (xOff: number): THREE.Group => {
    const shoulder = new THREE.Group();
    shoulder.position.set(xOff, 1.46, 0);
    const sleeve = new THREE.Mesh(procArmGeo, torsoMat);
    sleeve.position.y = -0.18;
    const hand = new THREE.Mesh(procHandGeo, sharedSkinMat);
    hand.position.y = -0.46;
    shoulder.add(sleeve, hand);
    return shoulder;
  };
  const leftArm = makeArm(-0.29);
  const rightArm = makeArm(0.29);
  bodyRoot.add(leftArm, rightArm);

  // Weapon mount on right hand
  const weaponGroup = new THREE.Group();
  weaponGroup.position.set(0, -0.54, 0.16);
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

  if (options.castShadow) {
    bodyRoot.traverse((obj) => {
      if ((obj as THREE.Mesh).isMesh) {
        obj.castShadow = true;
        obj.receiveShadow = true;
      }
    });
  }

  const animator = new CharacterAnimator({
    leftLeg,
    rightLeg,
    leftArm,
    rightArm,
    torso: torsoGroup,
    head,
    bodyRoot,
  });

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
    animator,
    isGlbBacked: false,
    baseScale,
    isPlayer: Boolean(options.isPlayer),
    isPolice: Boolean(options.isPolice),
    isBoss: Boolean(options.isBoss),
  };
}

/**
 * Upgrades an existing HumanoidRig in-place to a skinned, animated GLB character
 * while preserving its current world transform, colors, equipped weapon, and role accessories.
 */
export function upgradeHumanoidRigToGlb(
  rig: HumanoidRig,
  template: DhakaCharacterGlbTemplate,
  textures: DhakaTextureSet,
  castShadow: boolean,
): void {
  if (rig.isGlbBacked) return;

  const glb = instantiateGlbCharacter(
    template,
    {
      torsoColor: rig.torsoMat.color.getHex(),
      pantsColor: rig.pantsMat.color.getHex(),
      accentColor: rig.accentMat.color.getHex(),
    },
    {
      fabricNormalMap: textures.fabricNormal,
      castShadow,
      isPolice: rig.isPolice,
    },
  );

  // Detach existing weaponGroup so we can reparent it cleanly onto RightHand bone
  rig.weaponGroup.parent?.remove(rig.weaponGroup);

  // Clear old procedural children inside bodyRoot
  while (rig.bodyRoot.children.length > 0) {
    rig.bodyRoot.remove(rig.bodyRoot.children[0]);
  }
  rig.bodyRoot.rotation.set(0, 0, 0);
  rig.bodyRoot.position.set(0, 0, 0);
  rig.bodyRoot.scale.setScalar(rig.baseScale);
  rig.bodyRoot.add(glb.root);

  // Reparent weaponGroup onto RightHand bone (fallback to root if bone missing)
  if (glb.rightHandBone) {
    rig.weaponGroup.position.set(0, -0.04, 0.14);
    rig.weaponGroup.rotation.set(0, 0, 0);
    glb.rightHandBone.add(rig.weaponGroup);
  } else {
    rig.weaponGroup.position.set(0.25, 0.92, 0.18);
    glb.root.add(rig.weaponGroup);
  }

  // Attach iconic Dhaka role accessories (backpack, shades, police cap brim) onto skeleton bones
  if (glb.chestBone && glb.headBone) {
    attachRoleAccessories(
      { chest: glb.chestBone, head: glb.headBone },
      glb.accentMat,
      { isPlayer: rig.isPlayer, isPolice: rig.isPolice, isBoss: rig.isBoss },
      true,
    );
  }

  // Swap live material references so setHumanoidOutfit & hit-flash continue working transparently
  rig.torsoMat.dispose();
  rig.pantsMat.dispose();
  rig.accentMat.dispose();
  rig.torsoMat = glb.torsoMat;
  rig.pantsMat = glb.pantsMat;
  rig.accentMat = glb.accentMat;
  rig.isGlbBacked = true;

  // Bind Three.js AnimationMixer with Idle/Walk/Run/Attack/Hit/KO clips
  rig.animator.bindGlbRig(glb.root, template.animations);
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
 * Iconic Shapla (Water Lily) Monument for Dhaka Crossing Roundabout (11.5m tall sculpted hero landmark).
 */
export function createShaplaMonument(textures?: DhakaTextureSet): THREE.Group {
  const g = new THREE.Group();

  const stoneMat = new THREE.MeshStandardMaterial({
    color: 0xd8d0c3,
    roughness: 0.74,
    metalness: 0.08,
    normalMap: textures?.sidewalkNormal ?? null,
    normalScale: new THREE.Vector2(0.45, 0.45),
  });
  const darkStoneMat = new THREE.MeshStandardMaterial({
    color: 0x9b9284,
    roughness: 0.82,
    metalness: 0.08,
  });
  const waterMat = new THREE.MeshStandardMaterial({
    color: 0x0d9488,
    bumpMap: textures?.waterBumpMap ?? null,
    bumpScale: 0.08,
    roughness: 0.1,
    metalness: 0.42,
    emissive: 0x064e57,
    emissiveIntensity: 0.18,
  });
  const petalMat = new THREE.MeshStandardMaterial({
    color: 0xf8fafc,
    roughness: 0.24,
    metalness: 0.18,
    emissive: 0x29394d,
    emissiveIntensity: 0.22,
  });
  const sepalMat = new THREE.MeshStandardMaterial({
    color: 0x0f766e,
    roughness: 0.36,
    metalness: 0.22,
  });
  const goldMat = new THREE.MeshStandardMaterial({
    color: 0xfbbf24,
    roughness: 0.2,
    metalness: 0.86,
    emissive: 0x92400e,
    emissiveIntensity: 0.28,
  });

  // Circular 2-tier stepped fountain terrace + brass rim band
  const baseStep = new THREE.Mesh(new THREE.CylinderGeometry(4.85, 5.15, 0.32, 28), darkStoneMat);
  baseStep.position.y = 0.16;
  g.add(baseStep);

  const baseOuter = new THREE.Mesh(new THREE.CylinderGeometry(4.35, 4.6, 0.65, 28), stoneMat);
  baseOuter.position.y = 0.52;
  g.add(baseOuter);

  const rimBand = new THREE.Mesh(new THREE.TorusGeometry(4.25, 0.08, 8, 32), goldMat);
  rimBand.rotation.x = Math.PI / 2;
  rimBand.position.y = 0.84;
  g.add(rimBand);

  const pool = new THREE.Mesh(new THREE.CylinderGeometry(3.92, 3.92, 0.48, 28), waterMat);
  pool.position.y = 0.56;
  g.add(pool);

  // Sculpted flared pedestal using LatheGeometry
  const pedPoints = [
    new THREE.Vector2(2.25, 0.5),
    new THREE.Vector2(1.85, 1.15),
    new THREE.Vector2(1.38, 2.2),
    new THREE.Vector2(1.62, 3.05),
    new THREE.Vector2(1.25, 3.35),
  ];
  const pedestal = new THREE.Mesh(new THREE.LatheGeometry(pedPoints, 20), stoneMat);
  g.add(pedestal);

  // Sculpted curved beveled water-lily petal geometry (ExtrudeGeometry)
  const petalShape = new THREE.Shape();
  petalShape.moveTo(0, 0);
  petalShape.quadraticCurveTo(0.86, 1.25, 0.62, 2.85);
  petalShape.quadraticCurveTo(0.28, 3.72, 0, 4.15);
  petalShape.quadraticCurveTo(-0.28, 3.72, -0.62, 2.85);
  petalShape.quadraticCurveTo(-0.86, 1.25, 0, 0);

  const petalGeo = new THREE.ExtrudeGeometry(petalShape, {
    depth: 0.14,
    bevelEnabled: true,
    bevelThickness: 0.07,
    bevelSize: 0.06,
    bevelSegments: 2,
    curveSegments: 8,
  });
  petalGeo.translate(0, 0, -0.07);

  // Lower emerald lotus sepal cradle ring (8 leaves)
  for (let i = 0; i < 8; i++) {
    const angle = ((i + 0.5) / 8) * Math.PI * 2;
    const pivot = new THREE.Group();
    pivot.position.set(0, 3.15, 0);
    pivot.rotation.y = angle;
    const sepal = new THREE.Mesh(petalGeo, sepalMat);
    sepal.scale.set(0.74, 0.68, 0.85);
    sepal.position.set(0, 0.05, 0.82);
    sepal.rotation.x = 1.02;
    pivot.add(sepal);
    g.add(pivot);
  }

  // Outer ring of 8 sculpted white Shapla lotus petals
  for (let i = 0; i < 8; i++) {
    const angle = (i / 8) * Math.PI * 2;
    const pivot = new THREE.Group();
    pivot.position.set(0, 3.3, 0);
    pivot.rotation.y = angle;
    const petal = new THREE.Mesh(petalGeo, petalMat);
    petal.scale.set(0.95, 1.02, 1.0);
    petal.position.set(0, 0.1, 0.72);
    petal.rotation.x = 0.68;
    pivot.add(petal);
    g.add(pivot);
  }

  // Inner ring of 6 taller, more upright Shapla petals
  for (let i = 0; i < 6; i++) {
    const angle = (i / 6) * Math.PI * 2 + 0.28;
    const pivot = new THREE.Group();
    pivot.position.set(0, 3.7, 0);
    pivot.rotation.y = angle;
    const petal = new THREE.Mesh(petalGeo, petalMat);
    petal.scale.set(0.76, 1.12, 0.9);
    petal.position.set(0, 0.15, 0.42);
    petal.rotation.x = 0.32;
    pivot.add(petal);
    g.add(pivot);
  }

  // Sculpted central golden lotus bud & finial spire (LatheGeometry)
  const budPoints = [
    new THREE.Vector2(0.0, 0.0),
    new THREE.Vector2(0.66, 0.55),
    new THREE.Vector2(0.56, 1.55),
    new THREE.Vector2(0.24, 2.7),
    new THREE.Vector2(0.02, 3.65),
  ];
  const crown = new THREE.Mesh(new THREE.LatheGeometry(budPoints, 14), goldMat);
  crown.position.y = 4.55;
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

/**
 * Iconic multi-deck Sadarghat Passenger River Launch (ঢাকা লঞ্চ) moored along Buriganga.
 */
export function createSadarghatLaunch(): THREE.Group {
  const g = new THREE.Group();
  const hullMat = new THREE.MeshStandardMaterial({ color: 0x0f5132, roughness: 0.55, metalness: 0.25 });
  const deckMat = new THREE.MeshStandardMaterial({ color: 0xf1f5f9, roughness: 0.42, metalness: 0.15 });
  const trimMat = new THREE.MeshStandardMaterial({ color: 0xdc2626, roughness: 0.4 });
  const winGlowMat = new THREE.MeshStandardMaterial({
    color: 0xfef08a,
    emissive: 0xf59e0b,
    emissiveIntensity: 0.55,
    roughness: 0.2,
  });

  // Main steel hull
  const hull = new THREE.Mesh(new THREE.BoxGeometry(6.8, 2.2, 22), hullMat);
  hull.position.y = 1.0;
  g.add(hull);

  // Tapered bow
  const bow = new THREE.Mesh(new THREE.ConeGeometry(3.6, 5.4, 4), hullMat);
  bow.rotation.x = Math.PI / 2;
  bow.rotation.y = Math.PI / 4;
  bow.position.set(0, 1.1, 13.4);
  g.add(bow);

  // Lower & upper white passenger cabin decks
  const deck1 = new THREE.Mesh(new THREE.BoxGeometry(6.2, 2.4, 18.5), deckMat);
  deck1.position.set(0, 3.1, -0.4);
  g.add(deck1);

  const deck2 = new THREE.Mesh(new THREE.BoxGeometry(5.6, 2.2, 14.5), deckMat);
  deck2.position.set(0, 5.3, -0.8);
  g.add(deck2);

  // Crimson hull stripe & illuminated cabin window bands
  const stripe = new THREE.Mesh(new THREE.BoxGeometry(6.9, 0.38, 21.6), trimMat);
  stripe.position.y = 1.8;
  g.add(stripe);

  const winBand1 = new THREE.Mesh(new THREE.BoxGeometry(6.3, 0.72, 16.5), winGlowMat);
  winBand1.position.set(0, 3.3, -0.4);
  const winBand2 = new THREE.Mesh(new THREE.BoxGeometry(5.7, 0.68, 12.6), winGlowMat);
  winBand2.position.set(0, 5.4, -0.8);
  g.add(winBand1, winBand2);

  // Bridge wheelhouse + twin exhaust funnels
  const bridge = new THREE.Mesh(new THREE.BoxGeometry(4.2, 1.8, 3.6), deckMat);
  bridge.position.set(0, 7.1, 3.4);
  g.add(bridge);

  [-1.2, 1.2].forEach((fx) => {
    const funnel = new THREE.Mesh(new THREE.CylinderGeometry(0.42, 0.52, 2.4, 10), trimMat);
    funnel.position.set(fx, 7.5, -2.6);
    g.add(funnel);
  });

  return g;
}

/**
 * Lalbagh / Old Dhaka terracotta ceremonial arch gateway (placed cleanly on plaza edges).
 */
export function createOldDhakaGateArch(): THREE.Group {
  const g = new THREE.Group();
  const terraMat = new THREE.MeshStandardMaterial({ color: 0xb04e36, roughness: 0.85, metalness: 0.05 });
  const trimMat = new THREE.MeshStandardMaterial({ color: 0xe9d8c4, roughness: 0.7 });
  const domeMat = new THREE.MeshStandardMaterial({ color: 0xf8fafc, roughness: 0.38, metalness: 0.15 });

  [-4.2, 4.2].forEach((px) => {
    const pier = new THREE.Mesh(new THREE.BoxGeometry(2.2, 7.6, 2.6), terraMat);
    pier.position.set(px, 3.8, 0);
    g.add(pier);

    const turret = new THREE.Mesh(new THREE.CylinderGeometry(0.75, 0.9, 2.2, 10), trimMat);
    turret.position.set(px, 8.7, 0);
    g.add(turret);

    const dome = new THREE.Mesh(new THREE.SphereGeometry(0.78, 10, 8), domeMat);
    dome.position.set(px, 10.0, 0);
    g.add(dome);
  });

  const lintel = new THREE.Mesh(new THREE.BoxGeometry(10.8, 2.2, 2.4), terraMat);
  lintel.position.set(0, 6.8, 0);
  g.add(lintel);

  const frieze = new THREE.Mesh(new THREE.BoxGeometry(11.1, 0.45, 2.6), trimMat);
  frieze.position.set(0, 7.95, 0);
  g.add(frieze);

  return g;
}

/**
 * Covered Dhaka roadside Bus Stop Shelter with backlit route panel.
 */
export function createBusStopShelter(): THREE.Group {
  const g = new THREE.Group();
  const steelMat = new THREE.MeshStandardMaterial({ color: 0x0f766e, roughness: 0.42, metalness: 0.55 });
  const roofMat = new THREE.MeshStandardMaterial({ color: 0xd97706, roughness: 0.48, metalness: 0.35 });
  const benchMat = new THREE.MeshStandardMaterial({ color: 0x334155, roughness: 0.6 });
  const routePanelMat = new THREE.MeshStandardMaterial({
    color: 0xfef3c7,
    emissive: 0xf59e0b,
    emissiveIntensity: 0.35,
    roughness: 0.25,
  });

  const backWall = new THREE.Mesh(new THREE.BoxGeometry(4.4, 2.3, 0.16), steelMat);
  backWall.position.set(0, 1.35, -0.75);
  g.add(backWall);

  const routeBox = new THREE.Mesh(new THREE.BoxGeometry(2.6, 0.9, 0.12), routePanelMat);
  routeBox.position.set(0, 1.65, -0.64);
  g.add(routeBox);

  const roof = new THREE.Mesh(new THREE.BoxGeometry(4.8, 0.16, 1.95), roofMat);
  roof.position.set(0, 2.65, 0);
  roof.rotation.x = 0.08;
  g.add(roof);

  [-2.05, 2.05].forEach((px) => {
    const post = new THREE.Mesh(new THREE.CylinderGeometry(0.07, 0.07, 2.6, 8), steelMat);
    post.position.set(px, 1.3, 0.72);
    g.add(post);
  });

  const bench = new THREE.Mesh(new THREE.BoxGeometry(3.5, 0.14, 0.52), benchMat);
  bench.position.set(0, 0.55, -0.25);
  g.add(bench);

  return g;
}

/**
 * Overhead Dhaka Boulevard steel directional gantry (clear height 6.8m so buses & trucks pass freely).
 */
export function createOverheadGantrySign(
  spanWidth: number,
  signTex: THREE.Texture,
): { group: THREE.Group; signMat: THREE.MeshStandardMaterial; disposables: Array<{ dispose: () => void }> } {
  const group = new THREE.Group();
  const steelMat = new THREE.MeshStandardMaterial({ color: 0x475569, roughness: 0.48, metalness: 0.72 });
  const signMat = new THREE.MeshStandardMaterial({
    map: signTex,
    emissiveMap: signTex,
    emissive: 0xffffff,
    emissiveIntensity: 0.25,
    roughness: 0.32,
    metalness: 0.12,
  });

  const halfSpan = spanWidth / 2;
  const colGeo = new THREE.CylinderGeometry(0.22, 0.28, 7.6, 8);
  [-halfSpan, halfSpan].forEach((cx) => {
    const col = new THREE.Mesh(colGeo, steelMat);
    col.position.set(cx, 3.8, 0);
    group.add(col);
  });

  const beamGeo = new THREE.BoxGeometry(spanWidth + 0.6, 0.36, 0.36);
  const beam = new THREE.Mesh(beamGeo, steelMat);
  beam.position.set(0, 7.4, 0);
  group.add(beam);

  const boardW = Math.min(spanWidth - 2.6, 11.4);
  const boardGeo = new THREE.BoxGeometry(boardW, 2.1, 0.22);
  const board = new THREE.Mesh(boardGeo, signMat);
  board.position.set(0, 7.3, 0);
  group.add(board);

  return {
    group,
    signMat,
    disposables: [steelMat, signMat, colGeo, beamGeo, boardGeo],
  };
}


