/**
 * Offline build script to generate a high-quality, authentic glTF 2.0 binary landmark
 * (/public/models/dhaka_landmark_ahsan_manzil.glb) for Dhaka-GTA.
 *
 * Ahsan Manzil (The Pink Palace / আহসান মঞ্জিল) is Old Dhaka's iconic Mughal-Victorian
 * riverside landmark overlooking the Buriganga river at Sadarghat.
 *
 * Features:
 * - Raised flood plinth podium with dressed stone moulding
 * - Neoclassical two-storey main block with symmetrical east & west wings
 * - Grand riverside portico with fluted Tuscan/Corinthian columns, entablature, and pediment
 * - Sweeping ceremonial riverfront flight of stairs with stone balustrade flanks
 * - Central octagonal drum with Mughal arched clerestory window niches
 * - High fluted hemispherical cupola dome with 16 raised ribs
 * - Decorative lotus crest and three-tiered golden finial spire
 * - Four corner octagonal Victorian roof kiosks (chhatris) with cupolas
 * - Arched window bays with colonial dark green louvered shutters and decorative trims
 * - High-detail PBR materials (palace terracotta pink, cream trims, dome metal, gold finials)
 *
 * Usage: npx tsx scripts/buildLandmarkGlb.ts
 */
import fs from 'node:fs';
import path from 'node:path';
import * as THREE from 'three';
import { GLTFExporter } from 'three/addons/exporters/GLTFExporter.js';

// Node FileReader polyfill for Three.js GLTFExporter binary output
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

export function buildAhsanManzilModel(): THREE.Group {
  const root = new THREE.Group();
  root.name = 'AhsanManzil_Root';

  // --- High-Fidelity PBR Materials ---
  const pinkWallMat = new THREE.MeshStandardMaterial({
    name: 'Ahsan_PinkWallMat',
    color: 0xd66878,
    roughness: 0.74,
    metalness: 0.06,
  });

  const creamTrimMat = new THREE.MeshStandardMaterial({
    name: 'Ahsan_CreamTrimMat',
    color: 0xfdf4e2,
    roughness: 0.55,
    metalness: 0.12,
  });

  const darkTrimMat = new THREE.MeshStandardMaterial({
    name: 'Ahsan_DarkTrimMat',
    color: 0x9b3b4a,
    roughness: 0.62,
    metalness: 0.1,
  });

  const domeMat = new THREE.MeshStandardMaterial({
    name: 'Ahsan_DomeMat',
    color: 0xb54354,
    roughness: 0.42,
    metalness: 0.28,
  });

  const goldSpireMat = new THREE.MeshStandardMaterial({
    name: 'Ahsan_GoldSpireMat',
    color: 0xf59e0b,
    roughness: 0.24,
    metalness: 0.88,
  });

  const stairMat = new THREE.MeshStandardMaterial({
    name: 'Ahsan_StairStoneMat',
    color: 0xded4c5,
    roughness: 0.84,
    metalness: 0.04,
  });

  const shutterMat = new THREE.MeshStandardMaterial({
    name: 'Ahsan_ShutterMat',
    color: 0x16382b,
    roughness: 0.68,
    metalness: 0.08,
  });

  const glassMat = new THREE.MeshStandardMaterial({
    name: 'Ahsan_GlassMat',
    color: 0x182430,
    roughness: 0.14,
    metalness: 0.75,
  });

  // --- 1. Elevated Flood Plinth Podium ---
  const plinthW = 44.0;
  const plinthD = 24.0;
  const plinthH = 1.35;
  const plinthGeo = new THREE.BoxGeometry(plinthW, plinthH, plinthD);
  const plinth = new THREE.Mesh(plinthGeo, stairMat);
  plinth.name = 'Plinth';
  plinth.position.y = plinthH / 2;
  root.add(plinth);

  // Plinth moulding ledge
  const plinthCorniceGeo = new THREE.BoxGeometry(plinthW + 0.6, 0.25, plinthD + 0.6);
  const plinthCornice = new THREE.Mesh(plinthCorniceGeo, creamTrimMat);
  plinthCornice.position.y = plinthH;
  root.add(plinthCornice);

  // --- 2. Central Grand Block & East/West Wings ---
  const centerW = 18.0;
  const centerD = 20.0;
  const centerH = 10.8;
  const centerBlock = new THREE.Mesh(new THREE.BoxGeometry(centerW, centerH, centerD), pinkWallMat);
  centerBlock.position.set(0, plinthH + centerH / 2, 0);
  root.add(centerBlock);

  // Symmetrical flanking wings (East & West)
  const wingW = 12.5;
  const wingD = 18.2;
  const wingH = 9.4;
  [-15.2, 15.2].forEach((wx, wi) => {
    const wing = new THREE.Mesh(new THREE.BoxGeometry(wingW, wingH, wingD), pinkWallMat);
    wing.position.set(wx, plinthH + wingH / 2, 0);
    wing.name = `Wing_${wi === 0 ? 'West' : 'East'}`;
    root.add(wing);

    // Wing string course (horizontal decorative band between 1st & 2nd floors)
    const bandGeo = new THREE.BoxGeometry(wingW + 0.35, 0.32, wingD + 0.35);
    const band = new THREE.Mesh(bandGeo, creamTrimMat);
    band.position.set(wx, plinthH + wingH * 0.52, 0);
    root.add(band);

    // Wing cornice moulding below parapet
    const corniceGeo = new THREE.BoxGeometry(wingW + 0.5, 0.45, wingD + 0.5);
    const cornice = new THREE.Mesh(corniceGeo, creamTrimMat);
    cornice.position.set(wx, plinthH + wingH, 0);
    root.add(cornice);

    // Wing parapet wall with balustrade cap
    const parapetGeo = new THREE.BoxGeometry(wingW + 0.2, 0.85, wingD + 0.2);
    const parapet = new THREE.Mesh(parapetGeo, darkTrimMat);
    parapet.position.set(wx, plinthH + wingH + 0.42, 0);
    root.add(parapet);
  });

  // Center Block Cornice & Parapet
  const centerCornice = new THREE.Mesh(new THREE.BoxGeometry(centerW + 0.55, 0.5, centerD + 0.55), creamTrimMat);
  centerCornice.position.set(0, plinthH + centerH, 0);
  const centerParapet = new THREE.Mesh(new THREE.BoxGeometry(centerW + 0.25, 0.95, centerD + 0.25), darkTrimMat);
  centerParapet.position.set(0, plinthH + centerH + 0.47, 0);
  root.add(centerCornice, centerParapet);

  // --- 3. Riverfront Portico with Corinthian Columns & Classical Pediment ---
  const porticoW = 15.6;
  const porticoD = 4.4;
  const colHeight = 7.6;
  const colRadius = 0.32;
  const colCount = 6;
  const colSpan = porticoW - 1.8;

  // Portico floor platform (first floor terrace)
  const porticoFloorGeo = new THREE.BoxGeometry(porticoW, 0.5, porticoD);
  const porticoFloor = new THREE.Mesh(porticoFloorGeo, stairMat);
  porticoFloor.position.set(0, plinthH + 3.8, centerD / 2 + porticoD / 2);
  root.add(porticoFloor);

  // 6 grand classical columns supporting the portico
  const colGeo = new THREE.CylinderGeometry(colRadius * 0.9, colRadius, colHeight, 14);
  colGeo.translate(0, colHeight / 2, 0);
  for (let i = 0; i < colCount; i++) {
    const colX = -colSpan / 2 + (i / (colCount - 1)) * colSpan;
    const col = new THREE.Mesh(colGeo, creamTrimMat);
    col.position.set(colX, plinthH + 3.8 + 0.25, centerD / 2 + porticoD - 0.5);
    root.add(col);

    // Base plinth per column
    const baseGeo = new THREE.BoxGeometry(0.85, 0.3, 0.85);
    const baseMesh = new THREE.Mesh(baseGeo, creamTrimMat);
    baseMesh.position.set(colX, plinthH + 3.8 + 0.15, centerD / 2 + porticoD - 0.5);
    root.add(baseMesh);

    // Capital per column
    const capGeo = new THREE.BoxGeometry(0.88, 0.35, 0.88);
    const capMesh = new THREE.Mesh(capGeo, creamTrimMat);
    capMesh.position.set(colX, plinthH + 3.8 + colHeight + 0.18, centerD / 2 + porticoD - 0.5);
    root.add(capMesh);
  }

  // Classical Entablature (Architrave + Frieze + Cornice)
  const entablatureGeo = new THREE.BoxGeometry(porticoW + 0.4, 0.85, porticoD + 0.3);
  const entablature = new THREE.Mesh(entablatureGeo, creamTrimMat);
  entablature.position.set(0, plinthH + 3.8 + colHeight + 0.42, centerD / 2 + porticoD / 2);
  root.add(entablature);

  // Triangular Pediment facing Buriganga (+Z)
  const pedimentShape = new THREE.Shape();
  pedimentShape.moveTo(-porticoW / 2 - 0.2, 0);
  pedimentShape.lineTo(porticoW / 2 + 0.2, 0);
  pedimentShape.lineTo(0, 2.4);
  pedimentShape.closePath();

  const pedimentGeo = new THREE.ExtrudeGeometry(pedimentShape, {
    depth: 0.6,
    bevelEnabled: true,
    bevelThickness: 0.15,
    bevelSize: 0.15,
    bevelSegments: 2,
  });
  pedimentGeo.translate(0, plinthH + 3.8 + colHeight + 0.85, centerD / 2 + porticoD - 0.3);
  const pedimentMesh = new THREE.Mesh(pedimentGeo, creamTrimMat);
  root.add(pedimentMesh);

  // Tympanum inset with palace coat-of-arms terracotta medallion
  const medallionGeo = new THREE.CylinderGeometry(0.72, 0.72, 0.15, 16);
  medallionGeo.rotateX(Math.PI / 2);
  const medallion = new THREE.Mesh(medallionGeo, darkTrimMat);
  medallion.position.set(0, plinthH + 3.8 + colHeight + 1.65, centerD / 2 + porticoD + 0.38);
  root.add(medallion);

  // --- 4. Grand Riverside Flight of Stairs ---
  const stairTiers = 9;
  const baseStairW = 15.0;
  const baseStairD = 0.95;
  const stairStepH = (plinthH + 3.8) / stairTiers;

  for (let s = 0; s < stairTiers; s++) {
    const sw = baseStairW - s * 0.45;
    const stepMesh = new THREE.Mesh(new THREE.BoxGeometry(sw, stairStepH, baseStairD), stairMat);
    stepMesh.position.set(0, (s + 0.5) * stairStepH, centerD / 2 + porticoD + (stairTiers - s) * baseStairD);
    root.add(stepMesh);
  }

  // Flanking stair balustrades with baluster urns
  [-baseStairW / 2 - 0.45, baseStairW / 2 + 0.45].forEach((bx) => {
    const balustradeLen = stairTiers * baseStairD;
    const balustradeGeo = new THREE.BoxGeometry(0.48, 1.0, balustradeLen);
    const balustrade = new THREE.Mesh(balustradeGeo, creamTrimMat);
    balustrade.position.set(bx, (plinthH + 3.8) * 0.5, centerD / 2 + porticoD + balustradeLen / 2 + 0.5);
    balustrade.rotation.x = -Math.atan2(plinthH + 3.8, balustradeLen);
    root.add(balustrade);

    // Decorative urns at bottom of stairs
    const urnBase = new THREE.Mesh(new THREE.BoxGeometry(0.8, 0.8, 0.8), creamTrimMat);
    urnBase.position.set(bx, 0.4, centerD / 2 + porticoD + balustradeLen + 0.5);
    const urn = new THREE.Mesh(new THREE.SphereGeometry(0.38, 10, 8), darkTrimMat);
    urn.position.set(bx, 1.15, centerD / 2 + porticoD + balustradeLen + 0.5);
    root.add(urnBase, urn);
  });

  // --- 5. Central Octagonal Drum with Arched Window Niches ---
  const drumR = 4.2;
  const drumH = 3.6;
  const drumGeo = new THREE.CylinderGeometry(drumR, drumR * 1.04, drumH, 8);
  const drumMesh = new THREE.Mesh(drumGeo, pinkWallMat);
  drumMesh.name = 'Dome_Drum';
  drumMesh.position.set(0, plinthH + centerH + drumH / 2, 0);
  root.add(drumMesh);

  // Drum base & crown mouldings
  const drumBaseMoulding = new THREE.Mesh(new THREE.CylinderGeometry(drumR * 1.08, drumR * 1.08, 0.35, 8), creamTrimMat);
  drumBaseMoulding.position.set(0, plinthH + centerH + 0.17, 0);
  const drumCrownCornice = new THREE.Mesh(new THREE.CylinderGeometry(drumR * 1.06, drumR * 1.06, 0.42, 8), creamTrimMat);
  drumCrownCornice.position.set(0, plinthH + centerH + drumH + 0.21, 0);
  root.add(drumBaseMoulding, drumCrownCornice);

  // 8 Arched window niches around octagonal drum faces
  const nicheW = 1.35;
  const nicheH = 2.1;
  const nicheGeo = new THREE.BoxGeometry(nicheW, nicheH, 0.25);
  for (let a = 0; a < 8; a++) {
    const ang = (a / 8) * Math.PI * 2 + Math.PI / 8;
    const nx = Math.sin(ang) * (drumR * 0.98);
    const nz = Math.cos(ang) * (drumR * 0.98);
    const niche = new THREE.Mesh(nicheGeo, glassMat);
    niche.position.set(nx, plinthH + centerH + drumH * 0.55, nz);
    niche.rotation.y = ang;
    root.add(niche);

    // Arch framing around niche
    const archFrame = new THREE.Mesh(new THREE.BoxGeometry(nicheW + 0.3, 0.25, 0.3), creamTrimMat);
    archFrame.position.set(nx, plinthH + centerH + drumH * 0.55 + nicheH / 2 + 0.12, nz);
    archFrame.rotation.y = ang;
    root.add(archFrame);
  }

  // --- 6. Fluted Hemispherical Cupola Dome with 16 Raised Ribs ---
  const domeBaseY = plinthH + centerH + drumH + 0.42;
  const domeRadius = 4.15;
  const domeGeo = new THREE.SphereGeometry(domeRadius, 24, 16, 0, Math.PI * 2, 0, Math.PI / 2);
  const domeMesh = new THREE.Mesh(domeGeo, domeMat);
  domeMesh.name = 'Ahsan_Dome';
  domeMesh.position.set(0, domeBaseY, 0);
  root.add(domeMesh);

  // 16 Raised Fluted Ribs extending from drum crown to dome apex
  const ribCount = 16;
  const ribCurveRadius = domeRadius + 0.08;
  const ribPoints: THREE.Vector3[] = [];
  for (let step = 0; step <= 12; step++) {
    const phi = (step / 12) * (Math.PI / 2);
    ribPoints.push(new THREE.Vector3(0, Math.cos(phi) * ribCurveRadius, Math.sin(phi) * ribCurveRadius));
  }
  const ribCurve = new THREE.CatmullRomCurve3(ribPoints);
  const ribGeo = new THREE.TubeGeometry(ribCurve, 12, 0.1, 6, false);

  for (let r = 0; r < ribCount; r++) {
    const ribAng = (r / ribCount) * Math.PI * 2;
    const ribMesh = new THREE.Mesh(ribGeo, darkTrimMat);
    ribMesh.position.set(0, domeBaseY, 0);
    ribMesh.rotation.y = ribAng;
    root.add(ribMesh);
  }

  // --- 7. Lotus Bud Crest & Three-Tier Golden Finial Spire ---
  const spireBaseY = domeBaseY + domeRadius;

  // Sculpted lotus pedestal ring
  const lotusRingGeo = new THREE.CylinderGeometry(0.95, 1.25, 0.5, 12);
  const lotusRing = new THREE.Mesh(lotusRingGeo, darkTrimMat);
  lotusRing.position.set(0, spireBaseY + 0.25, 0);
  root.add(lotusRing);

  // Golden finial spire spheres and needle
  const spireGeo1 = new THREE.SphereGeometry(0.55, 12, 8);
  const spireMesh1 = new THREE.Mesh(spireGeo1, goldSpireMat);
  spireMesh1.position.set(0, spireBaseY + 0.85, 0);

  const spireGeo2 = new THREE.SphereGeometry(0.38, 10, 8);
  const spireMesh2 = new THREE.Mesh(spireGeo2, goldSpireMat);
  spireMesh2.position.set(0, spireBaseY + 1.55, 0);

  const spireNeedle = new THREE.Mesh(new THREE.ConeGeometry(0.18, 2.4, 8), goldSpireMat);
  spireNeedle.position.set(0, spireBaseY + 2.85, 0);
  root.add(spireMesh1, spireMesh2, spireNeedle);

  // --- 8. Four Corner Victorian Roof Kiosks (Chhatris) ---
  const kioskCoords: Array<[number, number]> = [
    [-centerW / 2 - wingW + 2.2, -wingD / 2 + 2.2],
    [centerW / 2 + wingW - 2.2, -wingD / 2 + 2.2],
    [-centerW / 2 - wingW + 2.2, wingD / 2 - 2.2],
    [centerW / 2 + wingW - 2.2, wingD / 2 - 2.2],
  ];

  const kioskPillarGeo = new THREE.CylinderGeometry(0.14, 0.16, 1.9, 6);
  const kioskRoofGeo = new THREE.ConeGeometry(1.25, 1.5, 8);
  const kioskFinialGeo = new THREE.ConeGeometry(0.1, 0.8, 6);

  kioskCoords.forEach(([kx, kz], ki) => {
    const kGroup = new THREE.Group();
    kGroup.name = `Kiosk_${ki}`;
    kGroup.position.set(kx, plinthH + wingH + 0.85, kz);

    // 4 miniature corner pillars per kiosk
    [-0.6, 0.6].forEach((px) => {
      [-0.6, 0.6].forEach((pz) => {
        const p = new THREE.Mesh(kioskPillarGeo, creamTrimMat);
        p.position.set(px, 0.95, pz);
        kGroup.add(p);
      });
    });

    // Kiosk cupola roof + finial
    const roof = new THREE.Mesh(kioskRoofGeo, domeMat);
    roof.position.y = 2.65;
    const finial = new THREE.Mesh(kioskFinialGeo, goldSpireMat);
    finial.position.y = 3.8;
    kGroup.add(roof, finial);

    root.add(kGroup);
  });

  // --- 9. Arched Windows with Louvered Colonial Shutters along Wings ---
  const windowW = 1.25;
  const windowH = 2.2;
  const winGeo = new THREE.BoxGeometry(windowW, windowH, 0.18);
  const shutterGeo = new THREE.BoxGeometry(windowW * 0.44, windowH, 0.12);

  // South-facing windows (facing Buriganga) along west and east wings
  const winXs = [-19.2, -15.2, -11.2, 11.2, 15.2, 19.2];
  const winYs = [plinthH + 2.4, plinthH + wingH * 0.68]; // Ground floor & 1st floor tiers

  winYs.forEach((wy) => {
    winXs.forEach((wx) => {
      // Dark glass windowpane
      const win = new THREE.Mesh(winGeo, glassMat);
      win.position.set(wx, wy, wingD / 2 + 0.08);
      root.add(win);

      // Louvered shutters flanking left & right
      const shutterL = new THREE.Mesh(shutterGeo, shutterMat);
      shutterL.position.set(wx - windowW * 0.42, wy, wingD / 2 + 0.12);
      shutterL.rotation.y = 0.28; // slightly open
      const shutterR = new THREE.Mesh(shutterGeo, shutterMat);
      shutterR.position.set(wx + windowW * 0.42, wy, wingD / 2 + 0.12);
      shutterR.rotation.y = -0.28;
      root.add(shutterL, shutterR);

      // Decorative cream stucco window hood moulding
      const hood = new THREE.Mesh(new THREE.BoxGeometry(windowW + 0.35, 0.22, 0.28), creamTrimMat);
      hood.position.set(wx, wy + windowH / 2 + 0.12, wingD / 2 + 0.12);
      root.add(hood);
    });
  });

  return root;
}

async function main(): Promise<void> {
  const scene = new THREE.Scene();
  scene.name = 'DhakaLandmarkScene';

  const landmark = buildAhsanManzilModel();
  scene.add(landmark);

  const exporter = new GLTFExporter();
  const glb = await exporter.parseAsync(scene, {
    binary: true,
  });

  if (!(glb instanceof ArrayBuffer)) {
    throw new Error('Expected binary ArrayBuffer from GLTFExporter');
  }

  const outDir = path.resolve(process.cwd(), 'public/models');
  fs.mkdirSync(outDir, { recursive: true });
  const outPath = path.join(outDir, 'dhaka_landmark_ahsan_manzil.glb');
  fs.writeFileSync(outPath, Buffer.from(glb));

  const bytes = fs.statSync(outPath).size;
  console.log(`Successfully generated ${outPath} (${(bytes / 1024).toFixed(1)} KB)`);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
