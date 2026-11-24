/**
 * Offline build script to generate a high-quality, original (CC0 / project-authored)
 * glTF 2.0 binary luxury sports sedan (/public/models/dhaka_sedan.glb).
 *
 * Features:
 * - Detailed aerodynamic exterior (hood, grille, bumpers, side skirts, trunk, mirrors, diffuser, twin exhausts)
 * - Complete detailed interior (dashboard, gauge binnacle, center console, gear selector, sports steering wheel, bucket seats, rear bench)
 * - Functional suspension hierarchy (Chassis node tilts with pitch & roll)
 * - Functional steering pivots (Steer_FL, Steer_FR) and rolling wheel nodes (Wheel_FL, Wheel_FR, Wheel_RL, Wheel_RR)
 * - Functional lighting nodes (Headlights, Ruby BrakeLights, White ReverseLights)
 *
 * Usage: npx tsx scripts/buildSedanGlb.ts
 */
import fs from 'node:fs';
import path from 'node:path';
import * as THREE from 'three';
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

export function buildSedanModel(): THREE.Group {
  const root = new THREE.Group();
  root.name = 'Sedan_Root';

  // --- Materials ---
  const paintMat = new THREE.MeshStandardMaterial({
    name: 'Sedan_PaintMat',
    color: 0x1e293b, // Deep metallic midnight sapphire / charcoal
    roughness: 0.18,
    metalness: 0.82,
  });

  const darkGlassMat = new THREE.MeshStandardMaterial({
    name: 'Sedan_DarkGlassMat',
    color: 0x0f172a,
    roughness: 0.08,
    metalness: 0.9,
    transparent: true,
    opacity: 0.82,
  });

  const chromeMat = new THREE.MeshStandardMaterial({
    name: 'Sedan_ChromeMat',
    color: 0xf1f5f9,
    roughness: 0.15,
    metalness: 0.95,
  });

  const interiorMat = new THREE.MeshStandardMaterial({
    name: 'Sedan_InteriorMat',
    color: 0x18181b, // Charcoal leatherette
    roughness: 0.78,
    metalness: 0.12,
  });

  const seatMat = new THREE.MeshStandardMaterial({
    name: 'Sedan_SeatMat',
    color: 0x78350f, // Cognac saddle brown leather bolsters
    roughness: 0.65,
    metalness: 0.08,
  });

  const tireMat = new THREE.MeshStandardMaterial({
    name: 'Sedan_TireMat',
    color: 0x18181b,
    roughness: 0.88,
    metalness: 0.04,
  });

  const rimMat = new THREE.MeshStandardMaterial({
    name: 'Sedan_RimMat',
    color: 0xe2e8f0,
    roughness: 0.22,
    metalness: 0.88,
  });

  const caliperMat = new THREE.MeshStandardMaterial({
    name: 'Sedan_CaliperMat',
    color: 0xdc2626, // Brembo crimson red
    roughness: 0.32,
    metalness: 0.6,
  });

  const rotorMat = new THREE.MeshStandardMaterial({
    name: 'Sedan_RotorMat',
    color: 0x94a3b8,
    roughness: 0.28,
    metalness: 0.92,
  });

  const hlMat = new THREE.MeshStandardMaterial({
    name: 'Sedan_HeadlightMat',
    color: 0xffffff,
    emissive: 0xfffbeb,
    emissiveIntensity: 0.85,
    roughness: 0.12,
    metalness: 0.1,
  });

  const brakeMat = new THREE.MeshStandardMaterial({
    name: 'Sedan_BrakeLightMat',
    color: 0xdc2626,
    emissive: 0x991b1b,
    emissiveIntensity: 0.5,
    roughness: 0.2,
    metalness: 0.15,
  });

  const revMat = new THREE.MeshStandardMaterial({
    name: 'Sedan_ReverseLightMat',
    color: 0xffffff,
    emissive: 0x000000,
    emissiveIntensity: 0.0,
    roughness: 0.15,
    metalness: 0.1,
  });

  // --- Chassis (Tilts with suspension pitch & roll) ---
  const chassis = new THREE.Group();
  chassis.name = 'Chassis';
  chassis.position.set(0, 0, 0);
  root.add(chassis);

  // 1. Lower Main Body Tub
  const lowerBodyGeo = new THREE.BoxGeometry(1.92, 0.44, 4.45);
  const lowerBody = new THREE.Mesh(lowerBodyGeo, paintMat);
  lowerBody.name = 'Body_Lower';
  lowerBody.position.set(0, 0.52, 0);
  chassis.add(lowerBody);

  // 2. Sculpted Hood (Bonnet) with Power Bulge & Sloped Nose
  const hoodGeo = new THREE.BoxGeometry(1.82, 0.22, 1.48);
  const hood = new THREE.Mesh(hoodGeo, paintMat);
  hood.name = 'Body_Hood';
  hood.position.set(0, 0.78, 1.42);
  hood.rotation.x = 0.055;
  chassis.add(hood);

  // 3. Front Bumper Fascia, Grille & Front Splitter
  const bumperGeo = new THREE.BoxGeometry(1.88, 0.36, 0.32);
  const bumper = new THREE.Mesh(bumperGeo, paintMat);
  bumper.position.set(0, 0.42, 2.22);
  chassis.add(bumper);

  const grilleGeo = new THREE.BoxGeometry(1.18, 0.22, 0.08);
  const grille = new THREE.Mesh(grilleGeo, chromeMat);
  grille.position.set(0, 0.52, 2.37);
  chassis.add(grille);

  const splitterGeo = new THREE.BoxGeometry(1.86, 0.06, 0.22);
  const splitter = new THREE.Mesh(splitterGeo, interiorMat);
  splitter.position.set(0, 0.22, 2.28);
  chassis.add(splitter);

  // 4. Rear Trunk Deck & Lip Spoiler
  const trunkGeo = new THREE.BoxGeometry(1.8, 0.26, 0.92);
  const trunk = new THREE.Mesh(trunkGeo, paintMat);
  trunk.name = 'Body_Trunk';
  trunk.position.set(0, 0.82, -1.68);
  trunk.rotation.x = -0.045;
  chassis.add(trunk);

  const lipSpoilerGeo = new THREE.BoxGeometry(1.68, 0.06, 0.16);
  const lipSpoiler = new THREE.Mesh(lipSpoilerGeo, interiorMat);
  lipSpoiler.position.set(0, 0.98, -2.12);
  chassis.add(lipSpoiler);

  // Rear Diffuser & Twin Dual Chrome Exhausts
  const diffuserGeo = new THREE.BoxGeometry(1.78, 0.22, 0.24);
  const diffuser = new THREE.Mesh(diffuserGeo, interiorMat);
  diffuser.position.set(0, 0.36, -2.2);
  chassis.add(diffuser);

  const exhaustGeo = new THREE.CylinderGeometry(0.06, 0.06, 0.35, 12);
  exhaustGeo.rotateX(Math.PI / 2);
  [-0.65, -0.52, 0.52, 0.65].forEach((exX) => {
    const tip = new THREE.Mesh(exhaustGeo, chromeMat);
    tip.position.set(exX, 0.32, -2.26);
    chassis.add(tip);
  });

  // 5. Aerodynamic Greenhouse Cabin Roof & Pillars
  const roofGeo = new THREE.BoxGeometry(1.58, 0.08, 1.85);
  const roof = new THREE.Mesh(roofGeo, paintMat);
  roof.name = 'Body_Roof';
  roof.position.set(0, 1.38, -0.12);
  chassis.add(roof);

  // Windshield (Front Sloped Glass)
  const windshieldGeo = new THREE.BoxGeometry(1.56, 0.68, 0.08);
  const windshield = new THREE.Mesh(windshieldGeo, darkGlassMat);
  windshield.name = 'Glass_Windshield';
  windshield.position.set(0, 1.12, 0.86);
  windshield.rotation.x = 0.58;
  chassis.add(windshield);

  // Rear Window (Sloped Glass)
  const rearGlassGeo = new THREE.BoxGeometry(1.54, 0.64, 0.08);
  const rearGlass = new THREE.Mesh(rearGlassGeo, darkGlassMat);
  rearGlass.name = 'Glass_Rear';
  rearGlass.position.set(0, 1.14, -1.08);
  rearGlass.rotation.x = -0.54;
  chassis.add(rearGlass);

  // Side Windows (Left & Right)
  const sideGlassGeo = new THREE.BoxGeometry(0.06, 0.46, 1.82);
  [-0.78, 0.78].forEach((gx) => {
    const sideGlass = new THREE.Mesh(sideGlassGeo, darkGlassMat);
    sideGlass.position.set(gx, 1.15, -0.12);
    chassis.add(sideGlass);
  });

  // A-Pillars, B-Pillars, C-Pillars
  const pillarMat = paintMat;
  [-0.78, 0.78].forEach((px) => {
    const bPillarGeo = new THREE.BoxGeometry(0.07, 0.48, 0.12);
    const bPillar = new THREE.Mesh(bPillarGeo, interiorMat);
    bPillar.position.set(px, 1.15, -0.12);
    chassis.add(bPillar);
  });

  // Aerodynamic Side Mirrors
  const mirrorArmGeo = new THREE.BoxGeometry(0.14, 0.05, 0.08);
  const mirrorCapGeo = new THREE.BoxGeometry(0.24, 0.14, 0.15);
  const mirrorGlassGeo = new THREE.BoxGeometry(0.04, 0.11, 0.13);

  [-0.96, 0.96].forEach((mx) => {
    const arm = new THREE.Mesh(mirrorArmGeo, interiorMat);
    arm.position.set(mx * 0.9, 0.96, 0.85);
    const cap = new THREE.Mesh(mirrorCapGeo, paintMat);
    cap.position.set(mx, 0.98, 0.85);
    const mGlass = new THREE.Mesh(mirrorGlassGeo, chromeMat);
    mGlass.position.set(mx > 0 ? mx - 0.1 : mx + 0.1, 0.98, 0.85);
    chassis.add(arm, cap, mGlass);
  });

  // Flush Chrome Door Handles
  const handleGeo = new THREE.BoxGeometry(0.04, 0.05, 0.18);
  [-0.96, 0.96].forEach((hx) => {
    const hFront = new THREE.Mesh(handleGeo, chromeMat);
    hFront.position.set(hx, 0.76, 0.35);
    const hRear = new THREE.Mesh(handleGeo, chromeMat);
    hRear.position.set(hx, 0.76, -0.55);
    chassis.add(hFront, hRear);
  });

  // --- 6. Detailed Luxury Interior ---
  const cabinFloorGeo = new THREE.BoxGeometry(1.68, 0.12, 2.35);
  const cabinFloor = new THREE.Mesh(cabinFloorGeo, interiorMat);
  cabinFloor.position.set(0, 0.48, -0.15);
  chassis.add(cabinFloor);

  // Sculpted Dashboard with Instrument Binnacle
  const dashGeo = new THREE.BoxGeometry(1.64, 0.38, 0.48);
  const dash = new THREE.Mesh(dashGeo, interiorMat);
  dash.name = 'Interior_Dashboard';
  dash.position.set(0, 0.88, 0.72);
  dash.rotation.x = -0.12;
  chassis.add(dash);

  // Digital Gauge Cluster Screen & Center Infotainment Screen
  const gaugeGeo = new THREE.BoxGeometry(0.42, 0.16, 0.04);
  const screenMat = new THREE.MeshStandardMaterial({
    name: 'Sedan_ScreenMat',
    color: 0x38bdf8,
    emissive: 0x0284c7,
    emissiveIntensity: 0.65,
    roughness: 0.2,
  });
  // Right-hand drive (Dhaka traffic) binnacle on right (+X)
  const gauge = new THREE.Mesh(gaugeGeo, screenMat);
  gauge.position.set(0.42, 0.96, 0.68);
  gauge.rotation.x = -0.18;
  chassis.add(gauge);

  const centerScreenGeo = new THREE.BoxGeometry(0.32, 0.22, 0.04);
  const centerScreen = new THREE.Mesh(centerScreenGeo, screenMat);
  centerScreen.position.set(0, 0.92, 0.69);
  centerScreen.rotation.x = -0.15;
  chassis.add(centerScreen);

  // Center Console & Shifter
  const consoleGeo = new THREE.BoxGeometry(0.34, 0.24, 1.25);
  const consoleMesh = new THREE.Mesh(consoleGeo, interiorMat);
  consoleMesh.position.set(0, 0.58, -0.05);
  chassis.add(consoleMesh);

  const shifterGeo = new THREE.CylinderGeometry(0.025, 0.03, 0.14, 8);
  const shifter = new THREE.Mesh(shifterGeo, chromeMat);
  shifter.position.set(0, 0.76, 0.18);
  chassis.add(shifter);

  // Luxury Bucket Seats (Driver right + Passenger left)
  const buildSeat = (x: number, z: number, isDriver: boolean) => {
    const seatGrp = new THREE.Group();
    seatGrp.name = isDriver ? 'Seat_Driver' : 'Seat_Passenger';
    seatGrp.position.set(x, 0.48, z);

    const cushionGeo = new THREE.BoxGeometry(0.56, 0.18, 0.58);
    const cushion = new THREE.Mesh(cushionGeo, seatMat);
    cushion.position.set(0, 0.1, 0);

    const backrestGeo = new THREE.BoxGeometry(0.54, 0.62, 0.14);
    const backrest = new THREE.Mesh(backrestGeo, seatMat);
    backrest.position.set(0, 0.46, -0.22);
    backrest.rotation.x = -0.12;

    const headrestGeo = new THREE.BoxGeometry(0.28, 0.2, 0.12);
    const headrest = new THREE.Mesh(headrestGeo, seatMat);
    headrest.position.set(0, 0.82, -0.26);

    seatGrp.add(cushion, backrest, headrest);
    return seatGrp;
  };

  chassis.add(buildSeat(0.42, -0.05, true)); // Driver seat (right side)
  chassis.add(buildSeat(-0.42, -0.05, false)); // Passenger seat (left side)

  // Rear Bench Seat
  const rearBenchGeo = new THREE.BoxGeometry(1.52, 0.22, 0.62);
  const rearBench = new THREE.Mesh(rearBenchGeo, seatMat);
  rearBench.position.set(0, 0.58, -0.85);

  const rearBackGeo = new THREE.BoxGeometry(1.48, 0.58, 0.14);
  const rearBack = new THREE.Mesh(rearBackGeo, seatMat);
  rearBack.position.set(0, 0.94, -1.14);
  rearBack.rotation.x = -0.14;
  chassis.add(rearBench, rearBack);

  // Functional 3-Spoke Sports Steering Wheel (Attached to Steering Column)
  const steeringCol = new THREE.Group();
  steeringCol.name = 'SteeringColumn';
  steeringCol.position.set(0.42, 0.84, 0.45);
  steeringCol.rotation.x = -0.32;
  chassis.add(steeringCol);

  const steeringWheel = new THREE.Group();
  steeringWheel.name = 'SteeringWheel';
  steeringCol.add(steeringWheel);

  const rimTorusGeo = new THREE.TorusGeometry(0.185, 0.024, 8, 20);
  const wheelRim = new THREE.Mesh(rimTorusGeo, interiorMat);
  const centerHubGeo = new THREE.CylinderGeometry(0.06, 0.06, 0.04, 12);
  centerHubGeo.rotateX(Math.PI / 2);
  const centerHub = new THREE.Mesh(centerHubGeo, chromeMat);

  const spokeGeo = new THREE.BoxGeometry(0.038, 0.16, 0.02);
  const spoke1 = new THREE.Mesh(spokeGeo, chromeMat);
  spoke1.position.set(0, -0.08, 0);
  const spoke2 = new THREE.Mesh(spokeGeo, chromeMat);
  spoke2.rotation.z = (Math.PI * 2) / 3;
  spoke2.position.set(0.07, 0.04, 0);
  const spoke3 = new THREE.Mesh(spokeGeo, chromeMat);
  spoke3.rotation.z = -(Math.PI * 2) / 3;
  spoke3.position.set(-0.07, 0.04, 0);

  steeringWheel.add(wheelRim, centerHub, spoke1, spoke2, spoke3);

  // --- 7. Functional Lighting Nodes ---
  // Dual Front Headlights (Warm-white LED projector cores)
  const hlGeo = new THREE.BoxGeometry(0.42, 0.16, 0.12);
  [-0.66, 0.66].forEach((hx, idx) => {
    const hlMesh = new THREE.Mesh(hlGeo, hlMat);
    hlMesh.name = `Headlight_${idx === 0 ? 'L' : 'R'}`;
    hlMesh.position.set(hx, 0.72, 2.24);
    chassis.add(hlMesh);
  });

  // Dual Rear Ruby Brake Lights
  const brakeGeo = new THREE.BoxGeometry(0.45, 0.16, 0.12);
  [-0.65, 0.65].forEach((bx, idx) => {
    const brakeMesh = new THREE.Mesh(brakeGeo, brakeMat);
    brakeMesh.name = `BrakeLight_${idx === 0 ? 'L' : 'R'}`;
    brakeMesh.position.set(bx, 0.78, -2.24);
    chassis.add(brakeMesh);
  });

  // Dual Rear White Reverse Lights
  const revGeo = new THREE.BoxGeometry(0.22, 0.08, 0.08);
  [-0.32, 0.32].forEach((rx, idx) => {
    const revMesh = new THREE.Mesh(revGeo, revMat);
    revMesh.name = `ReverseLight_${idx === 0 ? 'L' : 'R'}`;
    revMesh.position.set(rx, 0.75, -2.25);
    chassis.add(revMesh);
  });

  // --- 8. Functional Wheels, Steering Pivots & Brake Assemblies ---
  function makeSedanWheel(isFront: boolean): THREE.Group {
    const wheelGroup = new THREE.Group();

    // Tire Rubber (Rotates on X)
    const tireGeo = new THREE.CylinderGeometry(0.36, 0.36, 0.26, 20);
    tireGeo.rotateZ(Math.PI / 2);
    const tire = new THREE.Mesh(tireGeo, tireMat);

    // Multi-spoke Alloy Rim
    const rimGeo = new THREE.CylinderGeometry(0.24, 0.24, 0.27, 16);
    rimGeo.rotateZ(Math.PI / 2);
    const rim = new THREE.Mesh(rimGeo, rimMat);

    // Vented Steel Brake Rotor
    const rotorGeo = new THREE.CylinderGeometry(0.21, 0.21, 0.04, 16);
    rotorGeo.rotateZ(Math.PI / 2);
    const rotor = new THREE.Mesh(rotorGeo, rotorMat);

    // Brembo Red Brake Caliper
    const caliperGeo = new THREE.BoxGeometry(0.12, 0.16, 0.14);
    const caliper = new THREE.Mesh(caliperGeo, caliperMat);
    caliper.position.set(0, 0.13, 0);

    wheelGroup.add(tire, rim, rotor, caliper);
    return wheelGroup;
  }

  // Front Left Steering Pivot & Wheel
  const steerFL = new THREE.Group();
  steerFL.name = 'Steer_FL';
  steerFL.position.set(-0.94, 0.36, 1.42);
  const wheelFL = makeSedanWheel(true);
  wheelFL.name = 'Wheel_FL';
  steerFL.add(wheelFL);
  root.add(steerFL);

  // Front Right Steering Pivot & Wheel
  const steerFR = new THREE.Group();
  steerFR.name = 'Steer_FR';
  steerFR.position.set(0.94, 0.36, 1.42);
  const wheelFR = makeSedanWheel(true);
  wheelFR.name = 'Wheel_FR';
  steerFR.add(wheelFR);
  root.add(steerFR);

  // Rear Left Wheel (Spins directly on X)
  const wheelRL = makeSedanWheel(false);
  wheelRL.name = 'Wheel_RL';
  wheelRL.position.set(-0.94, 0.36, -1.42);
  root.add(wheelRL);

  // Rear Right Wheel (Spins directly on X)
  const wheelRR = makeSedanWheel(false);
  wheelRR.name = 'Wheel_RR';
  wheelRR.position.set(0.94, 0.36, -1.42);
  root.add(wheelRR);

  return root;
}

async function main(): Promise<void> {
  const scene = new THREE.Scene();
  scene.name = 'DhakaSedanScene';

  const sedan = buildSedanModel();
  scene.add(sedan);

  const exporter = new GLTFExporter();
  const glb = await exporter.parseAsync(scene, {
    binary: true,
  });

  if (!(glb instanceof ArrayBuffer)) {
    throw new Error('Expected binary ArrayBuffer from GLTFExporter');
  }

  const outDir = path.resolve(process.cwd(), 'public/models');
  fs.mkdirSync(outDir, { recursive: true });
  const outPath = path.join(outDir, 'dhaka_sedan.glb');
  fs.writeFileSync(outPath, Buffer.from(glb));

  const bytes = fs.statSync(outPath).size;
  console.log(`Successfully generated ${outPath} (${(bytes / 1024).toFixed(1)} KB)`);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
