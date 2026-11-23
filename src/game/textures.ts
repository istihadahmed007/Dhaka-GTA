import * as THREE from 'three';

export interface DhakaTextureSet {
  asphaltMap: THREE.CanvasTexture;
  asphaltBump: THREE.CanvasTexture;
  asphaltNormal: THREE.CanvasTexture;
  asphaltRoughness: THREE.CanvasTexture;
  sidewalkMap: THREE.CanvasTexture;
  sidewalkBump: THREE.CanvasTexture;
  sidewalkNormal: THREE.CanvasTexture;
  brickFacadeMap: THREE.CanvasTexture;
  brickEmissiveMap: THREE.CanvasTexture;
  brickBump: THREE.CanvasTexture;
  brickNormal: THREE.CanvasTexture;
  fabricNormal: THREE.CanvasTexture;
  metalWearRoughness: THREE.CanvasTexture;
  glassTowerMap: THREE.CanvasTexture;
  glassTowerEmissiveMap: THREE.CanvasTexture;
  glassRoughnessMap: THREE.CanvasTexture;
  motijheelTowerMap: THREE.CanvasTexture;
  motijheelEmissiveMap: THREE.CanvasTexture;
  marketFacadeMap: THREE.CanvasTexture;
  residentialMap: THREE.CanvasTexture;
  residentialEmissiveMap: THREE.CanvasTexture;
  waterBumpMap: THREE.CanvasTexture;
  lampHaloMap: THREE.CanvasTexture;
  curbStripeMap: THREE.CanvasTexture;
  rickshawArtMap: THREE.CanvasTexture;
  contactShadowMap: THREE.CanvasTexture;
  dispose: () => void;
}

function makeCanvas(w: number, h: number): [HTMLCanvasElement, CanvasRenderingContext2D] {
  const c = document.createElement('canvas');
  c.width = w;
  c.height = h;
  const ctx = c.getContext('2d')!;
  return [c, ctx];
}

function finalizeTexture(
  canvas: HTMLCanvasElement,
  isColor: boolean,
  maxAniso: number,
  repeatX = 1,
  repeatY = 1,
): THREE.CanvasTexture {
  const tex = new THREE.CanvasTexture(canvas);
  tex.wrapS = THREE.RepeatWrapping;
  tex.wrapT = THREE.RepeatWrapping;
  tex.repeat.set(repeatX, repeatY);
  tex.anisotropy = Math.min(8, maxAniso);
  tex.colorSpace = isColor ? THREE.SRGBColorSpace : THREE.NoColorSpace;
  tex.needsUpdate = true;
  return tex;
}

/**
 * Converts a grayscale height canvas into a seamless tangent-space RGB Normal Map
 * tagged explicitly as linear (THREE.NoColorSpace).
 */
function heightCanvasToNormalTexture(
  heightCanvas: HTMLCanvasElement,
  strength: number,
  maxAniso: number,
  repeatX = 1,
  repeatY = 1,
): THREE.CanvasTexture {
  const w = heightCanvas.width;
  const h = heightCanvas.height;
  const srcCtx = heightCanvas.getContext('2d')!;
  const src = srcCtx.getImageData(0, 0, w, h).data;

  const [outC, outCtx] = makeCanvas(w, h);
  const outImg = outCtx.createImageData(w, h);
  const dst = outImg.data;

  const sample = (x: number, y: number): number => {
    const wx = ((x % w) + w) % w;
    const wy = ((y % h) + h) % h;
    return src[(wy * w + wx) * 4] / 255;
  };

  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const tl = sample(x - 1, y - 1);
      const t = sample(x, y - 1);
      const tr = sample(x + 1, y - 1);
      const l = sample(x - 1, y);
      const r = sample(x + 1, y);
      const bl = sample(x - 1, y + 1);
      const b = sample(x, y + 1);
      const br = sample(x + 1, y + 1);

      const dX = (tr + 2 * r + br - (tl + 2 * l + bl)) * strength;
      const dY = (bl + 2 * b + br - (tl + 2 * t + tr)) * strength;
      const dZ = 1.0;
      const invLen = 1 / Math.hypot(dX, dY, dZ);

      const idx = (y * w + x) * 4;
      dst[idx] = Math.round((-dX * invLen * 0.5 + 0.5) * 255);
      dst[idx + 1] = Math.round((-dY * invLen * 0.5 + 0.5) * 255);
      dst[idx + 2] = Math.round((dZ * invLen * 0.5 + 0.5) * 255);
      dst[idx + 3] = 255;
    }
  }
  outCtx.putImageData(outImg, 0, 0);
  return finalizeTexture(outC, false, maxAniso, repeatX, repeatY);
}

export function createDhakaTextures(maxAniso: number): DhakaTextureSet {
  // 1. Weathered Dhaka Asphalt + Double-Yellow/Dashed Centerline + Subtle Patches
  const [asphC, asphCtx] = makeCanvas(512, 512);
  asphCtx.fillStyle = '#252930';
  asphCtx.fillRect(0, 0, 512, 512);
  // Subtle tarmac patch repairs
  asphCtx.fillStyle = 'rgba(20, 23, 28, 0.35)';
  asphCtx.fillRect(64, 90, 128, 86);
  asphCtx.fillRect(300, 290, 140, 112);
  // Aggregate mineral speckle
  for (let i = 0; i < 8500; i++) {
    const x = (i * 73 + (i % 13) * 19) % 512;
    const y = (i * 197 + Math.floor(i / 7) * 31) % 512;
    const shade = 28 + ((i * 29) % 28);
    asphCtx.fillStyle = `rgb(${shade},${shade + 2},${shade + 5})`;
    asphCtx.fillRect(x, y, 2, 2);
  }
  // Darker vehicle wheel wear tracks
  asphCtx.fillStyle = 'rgba(12, 14, 18, 0.34)';
  asphCtx.fillRect(96, 0, 68, 512);
  asphCtx.fillRect(348, 0, 68, 512);
  // Slight roadside dust accumulation along gutters
  const leftDust = asphCtx.createLinearGradient(0, 0, 38, 0);
  leftDust.addColorStop(0, 'rgba(116, 106, 92, 0.32)');
  leftDust.addColorStop(1, 'rgba(116, 106, 92, 0)');
  asphCtx.fillStyle = leftDust;
  asphCtx.fillRect(0, 0, 38, 512);
  const rightDust = asphCtx.createLinearGradient(512, 0, 474, 0);
  rightDust.addColorStop(0, 'rgba(116, 106, 92, 0.32)');
  rightDust.addColorStop(1, 'rgba(116, 106, 92, 0)');
  asphCtx.fillStyle = rightDust;
  asphCtx.fillRect(474, 0, 38, 512);

  // Crisp Dhaka yellow dashed lane center + weathered white shoulder lines
  asphCtx.fillStyle = '#e0a928';
  for (let y = 16; y < 512; y += 96) {
    asphCtx.fillRect(249, y, 14, 52);
  }
  asphCtx.fillStyle = 'rgba(232, 234, 238, 0.62)';
  asphCtx.fillRect(18, 0, 8, 512);
  asphCtx.fillRect(486, 0, 8, 512);
  const asphaltMap = finalizeTexture(asphC, true, maxAniso, 1, 1);

  // Asphalt micro-relief bump map (linear)
  const [abC, abCtx] = makeCanvas(256, 256);
  abCtx.fillStyle = '#808080';
  abCtx.fillRect(0, 0, 256, 256);
  for (let i = 0; i < 4800; i++) {
    const x = (i * 53) % 256;
    const y = (i * 113) % 256;
    const v = 84 + ((i * 37) % 92);
    abCtx.fillStyle = `rgb(${v},${v},${v})`;
    abCtx.fillRect(x, y, 2, 2);
  }
  const asphaltBump = finalizeTexture(abC, false, maxAniso, 1, 1);
  const asphaltNormal = heightCanvasToNormalTexture(abC, 1.35, maxAniso, 1, 1);

  // Asphalt wet-sheen roughness map (used ONLY during monsoon/storm; smooth wheel tracks + subtle wet streaks)
  const [arC, arCtx] = makeCanvas(256, 256);
  arCtx.fillStyle = '#c4c4c4';
  arCtx.fillRect(0, 0, 256, 256);
  // Broad longitudinal tire-track rain sheen channels
  const trackGradL = arCtx.createLinearGradient(28, 0, 102, 0);
  trackGradL.addColorStop(0, 'rgba(196,196,196,0)');
  trackGradL.addColorStop(0.5, 'rgba(92,92,92,0.72)');
  trackGradL.addColorStop(1, 'rgba(196,196,196,0)');
  arCtx.fillStyle = trackGradL;
  arCtx.fillRect(28, 0, 74, 256);

  const trackGradR = arCtx.createLinearGradient(154, 0, 228, 0);
  trackGradR.addColorStop(0, 'rgba(196,196,196,0)');
  trackGradR.addColorStop(0.5, 'rgba(92,92,92,0.72)');
  trackGradR.addColorStop(1, 'rgba(196,196,196,0)');
  arCtx.fillStyle = trackGradR;
  arCtx.fillRect(154, 0, 74, 256);

  // Fine asphalt grain variation (no polka dots)
  for (let i = 0; i < 2400; i++) {
    const x = (i * 67) % 256;
    const y = (i * 139) % 256;
    const v = 115 + ((i * 29) % 75);
    arCtx.fillStyle = `rgba(${v},${v},${v},0.35)`;
    arCtx.fillRect(x, y, 3, 5);
  }
  const asphaltRoughness = finalizeTexture(arC, false, maxAniso, 1, 1);

  // 2. Warm Weathered Dhaka Concrete Sidewalk Pavers + Bump Map
  const [swC, swCtx] = makeCanvas(256, 256);
  swCtx.fillStyle = '#8b8376';
  swCtx.fillRect(0, 0, 256, 256);
  // Subtle tonal tile variation so ground pad never looks flat white
  for (let gx = 0; gx < 4; gx++) {
    for (let gy = 0; gy < 4; gy++) {
      const delta = ((gx * 7 + gy * 13) % 5) * 5 - 10;
      swCtx.fillStyle = `rgb(${136 + delta}, ${129 + delta}, ${118 + delta})`;
      swCtx.fillRect(gx * 64 + 2, gy * 64 + 2, 60, 60);
    }
  }
  swCtx.strokeStyle = 'rgba(46, 42, 36, 0.42)';
  swCtx.lineWidth = 2;
  for (let i = 0; i <= 256; i += 64) {
    swCtx.beginPath();
    swCtx.moveTo(i, 0);
    swCtx.lineTo(i, 256);
    swCtx.stroke();
    swCtx.beginPath();
    swCtx.moveTo(0, i);
    swCtx.lineTo(256, i);
    swCtx.stroke();
  }
  for (let i = 0; i < 1800; i++) {
    const x = (i * 61) % 256;
    const y = (i * 139) % 256;
    swCtx.fillStyle = i % 3 === 0 ? 'rgba(255,246,230,0.08)' : 'rgba(38,34,28,0.11)';
    swCtx.fillRect(x, y, 2, 2);
  }
  const sidewalkMap = finalizeTexture(swC, true, maxAniso, 18, 18);

  const [swbC, swbCtx] = makeCanvas(256, 256);
  swbCtx.fillStyle = '#929292';
  swbCtx.fillRect(0, 0, 256, 256);
  swbCtx.strokeStyle = '#383838';
  swbCtx.lineWidth = 3;
  for (let i = 0; i <= 256; i += 64) {
    swbCtx.beginPath();
    swbCtx.moveTo(i, 0);
    swbCtx.lineTo(i, 256);
    swbCtx.stroke();
    swbCtx.beginPath();
    swbCtx.moveTo(0, i);
    swbCtx.lineTo(256, i);
    swbCtx.stroke();
  }
  const sidewalkBump = finalizeTexture(swbC, false, maxAniso, 18, 18);
  const sidewalkNormal = heightCanvasToNormalTexture(swbC, 1.55, maxAniso, 18, 18);

  // 3. Old Dhaka Terracotta / Weathered Colonial Brick + Arched Windows + Warm Night Emissive
  const [brC, brCtx] = makeCanvas(512, 512);
  const [breC, breCtx] = makeCanvas(512, 512);
  breCtx.fillStyle = '#000000';
  breCtx.fillRect(0, 0, 512, 512);

  brCtx.fillStyle = '#c4835a';
  brCtx.fillRect(0, 0, 512, 512);
  // Subtle brick courses & aged plaster variation
  for (let row = 0; row < 32; row++) {
    const y = row * 16;
    brCtx.fillStyle = 'rgba(68, 30, 16, 0.16)';
    brCtx.fillRect(0, y, 512, 2);
    const offset = (row % 2) * 24;
    for (let x = offset; x < 512; x += 48) {
      brCtx.fillRect(x, y, 2, 16);
      if ((row * 11 + x) % 7 === 0) {
        brCtx.fillStyle = 'rgba(170, 92, 58, 0.16)';
        brCtx.fillRect(x + 2, y + 2, 44, 12);
        brCtx.fillStyle = 'rgba(68, 30, 16, 0.16)';
      }
    }
  }
  // Monsoon plaster weathering streaks near roofline & ledges
  const stainGrad = brCtx.createLinearGradient(0, 0, 0, 96);
  stainGrad.addColorStop(0, 'rgba(36, 30, 26, 0.38)');
  stainGrad.addColorStop(1, 'rgba(36, 30, 26, 0)');
  brCtx.fillStyle = stainGrad;
  brCtx.fillRect(0, 0, 512, 96);

  // Horizontal decorative Dhaka molded cornice bands between floors
  for (let floor = 0; floor < 3; floor++) {
    const bandY = 18 + floor * 158;
    brCtx.fillStyle = '#dfcbb2';
    brCtx.fillRect(0, bandY, 512, 10);
    brCtx.fillStyle = 'rgba(40, 28, 20, 0.28)';
    brCtx.fillRect(0, bandY + 10, 512, 5);
  }

  // Arched Mughal-colonial shuttered windows + warm amber interior lights at night
  for (let floor = 0; floor < 3; floor++) {
    const wy = 46 + floor * 158;
    for (let col = 0; col < 4; col++) {
      const wx = 32 + col * 124;
      const isLit = (floor * 3 + col * 5) % 3 !== 0;

      // Plaster arch molding
      brCtx.fillStyle = '#e7d5be';
      brCtx.beginPath();
      brCtx.arc(wx + 32, wy + 26, 33, Math.PI, 0);
      brCtx.lineTo(wx + 65, wy + 98);
      brCtx.lineTo(wx - 1, wy + 98);
      brCtx.closePath();
      brCtx.fill();

      // Deep interior / teal wooden louver shutters
      brCtx.fillStyle = (floor + col) % 3 === 0 ? '#184b44' : isLit ? '#49392b' : '#20262c';
      brCtx.beginPath();
      brCtx.arc(wx + 32, wy + 28, 25, Math.PI, 0);
      brCtx.lineTo(wx + 57, wy + 90);
      brCtx.lineTo(wx + 7, wy + 90);
      brCtx.closePath();
      brCtx.fill();

      // Warm night glow into emissive map
      if (isLit) {
        const glowG = breCtx.createLinearGradient(wx, wy + 8, wx, wy + 90);
        glowG.addColorStop(0, '#ffcb74');
        glowG.addColorStop(1, '#c97828');
        breCtx.fillStyle = glowG;
        breCtx.beginPath();
        breCtx.arc(wx + 32, wy + 28, 23, Math.PI, 0);
        breCtx.lineTo(wx + 55, wy + 88);
        breCtx.lineTo(wx + 9, wy + 88);
        breCtx.closePath();
        breCtx.fill();
      }

      // Window grille bars & sill
      brCtx.fillStyle = '#6d472c';
      brCtx.fillRect(wx + 30, wy + 6, 4, 84);
      brCtx.fillRect(wx + 7, wy + 48, 50, 4);
      brCtx.fillStyle = '#d4bfa6';
      brCtx.fillRect(wx - 6, wy + 94, 76, 8);
    }
  }
  const brickFacadeMap = finalizeTexture(brC, true, maxAniso, 1, 1);
  const brickEmissiveMap = finalizeTexture(breC, true, maxAniso, 1, 1);

  // Brick bump map
  const [bbC, bbCtx] = makeCanvas(256, 256);
  bbCtx.fillStyle = '#888888';
  bbCtx.fillRect(0, 0, 256, 256);
  bbCtx.fillStyle = '#4c4c4c';
  for (let row = 0; row < 16; row++) {
    bbCtx.fillRect(0, row * 16, 256, 2);
    const off = (row % 2) * 16;
    for (let x = off; x < 256; x += 32) {
      bbCtx.fillRect(x, row * 16, 2, 16);
    }
  }
  const brickBump = finalizeTexture(bbC, false, maxAniso, 2, 2);
  const brickNormal = heightCanvasToNormalTexture(bbC, 1.9, maxAniso, 2, 2);

  // 3b. Subtle Woven Fabric Normal Map for Dhaka Character Garments (linear)
  const [fbC, fbCtx] = makeCanvas(128, 128);
  fbCtx.fillStyle = '#808080';
  fbCtx.fillRect(0, 0, 128, 128);
  for (let y = 0; y < 128; y += 4) {
    for (let x = 0; x < 128; x += 4) {
      const weave = ((x + y) % 8 === 0) ? 148 : 108;
      fbCtx.fillStyle = `rgb(${weave},${weave},${weave})`;
      fbCtx.fillRect(x, y, 2, 4);
      fbCtx.fillRect(x + 2, y + 2, 2, 2);
    }
  }
  const fabricNormal = heightCanvasToNormalTexture(fbC, 0.75, maxAniso, 3, 3);

  // 3c. Painted Metal Street-Wear Roughness Map for Dhaka CNGs, Rickshaws & Vehicles (linear)
  const [mwC, mwCtx] = makeCanvas(256, 256);
  mwCtx.fillStyle = '#686868';
  mwCtx.fillRect(0, 0, 256, 256);
  // Subtle lower-body Dhaka street dust / matte wear band
  const dustG = mwCtx.createLinearGradient(0, 160, 0, 256);
  dustG.addColorStop(0, 'rgba(185,185,185,0)');
  dustG.addColorStop(1, 'rgba(215,215,215,0.85)');
  mwCtx.fillStyle = dustG;
  mwCtx.fillRect(0, 160, 256, 96);
  // Subtle micro-scuffs and painted panel variation
  for (let i = 0; i < 650; i++) {
    const x = (i * 47) % 256;
    const y = (i * 97) % 256;
    const v = 78 + ((i * 31) % 90);
    mwCtx.fillStyle = `rgba(${v},${v},${v},0.4)`;
    mwCtx.fillRect(x, y, 6 + (i % 8), 2);
  }
  const metalWearRoughness = finalizeTexture(mwC, false, maxAniso, 1, 1);

  // 4. Gulshan / Banani Modern Glass Curtain Wall + Night Emissive + Glass Roughness Map
  const [glC, glCtx] = makeCanvas(512, 512);
  const [gleC, gleCtx] = makeCanvas(512, 512);
  const [glrC, glrCtx] = makeCanvas(512, 512);

  glCtx.fillStyle = '#1f344a';
  glCtx.fillRect(0, 0, 512, 512);
  gleCtx.fillStyle = '#000000';
  gleCtx.fillRect(0, 0, 512, 512);
  // Roughness: matte aluminum/concrete spandrels (#d8d8d8) vs glossy reflective glass (#202020)
  glrCtx.fillStyle = '#d4d4d4';
  glrCtx.fillRect(0, 0, 512, 512);

  for (let r = 0; r < 8; r++) {
    const y = r * 64;
    // Metallic curtain-wall spandrel slab
    glCtx.fillStyle = '#3f5a74';
    glCtx.fillRect(0, y, 512, 13);
    glCtx.fillStyle = '#627d98';
    glCtx.fillRect(0, y, 512, 3);

    for (let c = 0; c < 6; c++) {
      const x = c * 85 + 6;
      const hash = (r * 13 + c * 17) % 9;
      const litWarm = hash === 0 || hash === 3 || hash === 6;
      const litCool = hash === 1;

      const grad = glCtx.createLinearGradient(x, y + 14, x + 22, y + 60);
      if (litWarm) {
        grad.addColorStop(0, '#c8d8e6');
        grad.addColorStop(0.55, '#89a3b8');
        grad.addColorStop(1, '#395570');
      } else {
        grad.addColorStop(0, '#5a87ad');
        grad.addColorStop(0.45, '#2b4d6d');
        grad.addColorStop(1, '#14283c');
      }
      glCtx.fillStyle = grad;
      glCtx.fillRect(x, y + 14, 74, 46);

      // Subtle diagonal sky reflection highlight on glass
      glCtx.fillStyle = 'rgba(210, 236, 255, 0.14)';
      glCtx.beginPath();
      glCtx.moveTo(x, y + 14);
      glCtx.lineTo(x + 34, y + 14);
      glCtx.lineTo(x + 12, y + 60);
      glCtx.lineTo(x, y + 60);
      glCtx.closePath();
      glCtx.fill();

      // Glossy glass roughness
      glrCtx.fillStyle = '#1e1e1e';
      glrCtx.fillRect(x, y + 14, 74, 46);

      // Illuminated office interiors at sunset/night
      if (litWarm || litCool) {
        const emG = gleCtx.createLinearGradient(x, y + 14, x, y + 60);
        if (litWarm) {
          emG.addColorStop(0, '#ffdfa0');
          emG.addColorStop(1, '#c88b3a');
        } else {
          emG.addColorStop(0, '#b8e2ff');
          emG.addColorStop(1, '#5d9ec9');
        }
        gleCtx.fillStyle = emG;
        gleCtx.fillRect(x + 2, y + 16, 70, 42);
        // Subtle interior ceiling blind shadow
        gleCtx.fillStyle = 'rgba(0,0,0,0.35)';
        gleCtx.fillRect(x + 2, y + 16, 70, 8);
      }
    }
  }
  const glassTowerMap = finalizeTexture(glC, true, maxAniso, 1, 2);
  const glassTowerEmissiveMap = finalizeTexture(gleC, true, maxAniso, 1, 2);
  const glassRoughnessMap = finalizeTexture(glrC, false, maxAniso, 1, 2);

  // 4b. Motijheel Commercial Precast Concrete Fins & Tinted Office Glazing + Night Emissive
  const [mtC, mtCtx] = makeCanvas(512, 512);
  const [mteC, mteCtx] = makeCanvas(512, 512);
  mtCtx.fillStyle = '#2d3b4d';
  mtCtx.fillRect(0, 0, 512, 512);
  mteCtx.fillStyle = '#000000';
  mteCtx.fillRect(0, 0, 512, 512);

  for (let r = 0; r < 8; r++) {
    const y = r * 64;
    mtCtx.fillStyle = '#9aa6b4';
    mtCtx.fillRect(0, y, 512, 15);
    mtCtx.fillStyle = '#475569';
    mtCtx.fillRect(0, y + 15, 512, 4);
    for (let c = 0; c < 8; c++) {
      const x = c * 64;
      mtCtx.fillStyle = '#c5ced8';
      mtCtx.fillRect(x, y, 10, 64);
      const lit = (r * 5 + c * 7) % 4 === 0 || (r * 3 + c) % 7 === 0;
      const wg = mtCtx.createLinearGradient(x + 10, y + 19, x + 10, y + 61);
      wg.addColorStop(0, '#365874');
      wg.addColorStop(1, '#1a2f44');
      mtCtx.fillStyle = wg;
      mtCtx.fillRect(x + 10, y + 19, 54, 43);

      if (lit) {
        mteCtx.fillStyle = (r + c) % 3 === 0 ? '#f6c774' : '#9ed2f0';
        mteCtx.fillRect(x + 12, y + 21, 50, 39);
      }
    }
  }
  const motijheelTowerMap = finalizeTexture(mtC, true, maxAniso, 1, 2);
  const motijheelEmissiveMap = finalizeTexture(mteC, true, maxAniso, 1, 2);

  // 4c. Dhaka Market & Warehouse Corrugated Metal + Rolling Shopfront Shutters
  const [mkC, mkCtx] = makeCanvas(512, 512);
  mkCtx.fillStyle = '#94887a';
  mkCtx.fillRect(0, 0, 512, 512);
  for (let x = 0; x < 512; x += 12) {
    mkCtx.fillStyle = x % 24 === 0 ? 'rgba(255,255,255,0.08)' : 'rgba(20,20,25,0.16)';
    mkCtx.fillRect(x, 0, 6, 260);
  }
  for (let c = 0; c < 4; c++) {
    mkCtx.fillStyle = '#232a33';
    mkCtx.fillRect(28 + c * 124, 72, 86, 52);
    mkCtx.strokeStyle = '#d4c8b8';
    mkCtx.lineWidth = 4;
    mkCtx.strokeRect(28 + c * 124, 72, 86, 52);
  }
  mkCtx.fillStyle = '#685f56';
  mkCtx.fillRect(0, 260, 512, 252);
  for (let s = 0; s < 4; s++) {
    const sx = 18 + s * 124;
    mkCtx.fillStyle = s % 2 === 0 ? '#165a50' : '#2d4762';
    mkCtx.fillRect(sx, 286, 100, 212);
    mkCtx.fillStyle = 'rgba(0,0,0,0.24)';
    for (let sy = 294; sy < 496; sy += 12) {
      mkCtx.fillRect(sx, sy, 100, 3);
    }
  }
  const marketFacadeMap = finalizeTexture(mkC, true, maxAniso, 1, 1);

  // 5. Dhaka Residential Concrete Apartment Facade with Balconies + Warm Apartment Emissive
  const [resC, resCtx] = makeCanvas(512, 512);
  const [reseC, reseCtx] = makeCanvas(512, 512);
  resCtx.fillStyle = '#cfc3b0';
  resCtx.fillRect(0, 0, 512, 512);
  reseCtx.fillStyle = '#000000';
  reseCtx.fillRect(0, 0, 512, 512);

  for (let r = 0; r < 4; r++) {
    const y = r * 128;
    // Slab ledge + shadow underside
    resCtx.fillStyle = '#9e9587';
    resCtx.fillRect(0, y + 114, 512, 14);
    resCtx.fillStyle = 'rgba(28,24,20,0.22)';
    resCtx.fillRect(0, y, 512, 6);
    for (let c = 0; c < 3; c++) {
      const x = 22 + c * 164;
      resCtx.fillStyle = '#252b33';
      resCtx.fillRect(x, y + 20, 122, 92);
      const isLit = (r * 2 + c * 3) % 3 !== 0;
      if (isLit) {
        resCtx.fillStyle = '#8b6e4a';
        resCtx.fillRect(x + 18, y + 32, 46, 54);
        reseCtx.fillStyle = (r + c) % 2 === 0 ? '#ffd082' : '#d8eeff';
        reseCtx.fillRect(x + 18, y + 32, 46, 54);
      }
      // Balcony grill railing
      resCtx.fillStyle = '#52453b';
      resCtx.fillRect(x, y + 72, 122, 6);
      for (let rx = x + 6; rx < x + 120; rx += 14) {
        resCtx.fillRect(rx, y + 72, 3, 40);
      }
    }
  }
  const residentialMap = finalizeTexture(resC, true, maxAniso, 1, 1);
  const residentialEmissiveMap = finalizeTexture(reseC, true, maxAniso, 1, 1);

  // 5b. Water Ripple Bump Map (animated on Buriganga River, Hatirjheel & Dhanmondi Lakes)
  const [wbC, wbCtx] = makeCanvas(256, 256);
  wbCtx.fillStyle = '#808080';
  wbCtx.fillRect(0, 0, 256, 256);
  for (let y = 0; y < 256; y += 4) {
    for (let x = 0; x < 256; x += 4) {
      const wave =
        Math.sin((x / 256) * Math.PI * 8 + Math.sin((y / 256) * Math.PI * 4) * 1.4) * 0.5 +
        Math.cos((y / 256) * Math.PI * 10 + (x / 256) * Math.PI * 4) * 0.5;
      const v = Math.round(128 + wave * 56);
      wbCtx.fillStyle = `rgb(${v},${v},${v})`;
      wbCtx.fillRect(x, y, 4, 4);
    }
  }
  const waterBumpMap = finalizeTexture(wbC, false, maxAniso, 22, 8);

  // 5c. Soft Radial Streetlamp Glow Halo / Sodium Ground Pool Texture
  const [lhC, lhCtx] = makeCanvas(128, 128);
  const lhGrad = lhCtx.createRadialGradient(64, 64, 4, 64, 64, 62);
  lhGrad.addColorStop(0, 'rgba(255, 226, 150, 1.0)');
  lhGrad.addColorStop(0.28, 'rgba(255, 178, 72, 0.52)');
  lhGrad.addColorStop(0.65, 'rgba(255, 145, 40, 0.14)');
  lhGrad.addColorStop(1, 'rgba(255, 145, 40, 0.0)');
  lhCtx.fillStyle = lhGrad;
  lhCtx.fillRect(0, 0, 128, 128);
  const lampHaloMap = new THREE.CanvasTexture(lhC);
  lampHaloMap.colorSpace = THREE.SRGBColorSpace;
  lampHaloMap.needsUpdate = true;

  // 5d. Iconic Dhaka Black-and-Yellow Painted Road Curb Texture
  const [csStripeC, csStripeCtx] = makeCanvas(256, 64);
  for (let i = 0; i < 4; i++) {
    csStripeCtx.fillStyle = i % 2 === 0 ? '#1f242c' : '#d9a426';
    csStripeCtx.fillRect(i * 64, 0, 64, 64);
  }
  // Subtle weathering along bottom edge
  const curbWear = csStripeCtx.createLinearGradient(0, 0, 0, 64);
  curbWear.addColorStop(0, 'rgba(255,255,255,0.08)');
  curbWear.addColorStop(0.7, 'rgba(0,0,0,0.05)');
  curbWear.addColorStop(1, 'rgba(25,22,18,0.45)');
  csStripeCtx.fillStyle = curbWear;
  csStripeCtx.fillRect(0, 0, 256, 64);
  const curbStripeMap = finalizeTexture(csStripeC, true, maxAniso, 6, 1);

  // 6. Bangladeshi Folk Rickshaw Art Panel (Lotus, Bengal Tiger Stripes, Sunburst)
  const [raC, raCtx] = makeCanvas(256, 128);
  raCtx.fillStyle = '#d62828';
  raCtx.fillRect(0, 0, 256, 128);
  raCtx.strokeStyle = '#fcbf49';
  raCtx.lineWidth = 8;
  raCtx.strokeRect(6, 6, 244, 116);
  raCtx.fillStyle = '#007f5f';
  raCtx.beginPath();
  raCtx.arc(128, 120, 88, Math.PI, 0);
  raCtx.fill();
  for (let i = -2; i <= 2; i++) {
    raCtx.fillStyle = i === 0 ? '#ffd166' : '#ff4d6d';
    raCtx.beginPath();
    raCtx.ellipse(128 + i * 22, 78 + Math.abs(i) * 8, 16, 30, i * 0.28, 0, Math.PI * 2);
    raCtx.fill();
  }
  raCtx.fillStyle = '#fff3b0';
  for (let x = 22; x < 240; x += 22) {
    raCtx.beginPath();
    raCtx.arc(x, 20, 4, 0, Math.PI * 2);
    raCtx.fill();
  }
  const rickshawArtMap = finalizeTexture(raC, true, maxAniso, 1, 1);

  // 7. Soft Radial Contact Shadow Disc
  const [csC, csCtx] = makeCanvas(128, 128);
  const rad = csCtx.createRadialGradient(64, 64, 8, 64, 64, 62);
  rad.addColorStop(0, 'rgba(0, 0, 0, 0.72)');
  rad.addColorStop(0.55, 'rgba(0, 0, 0, 0.34)');
  rad.addColorStop(1, 'rgba(0, 0, 0, 0.0)');
  csCtx.fillStyle = rad;
  csCtx.fillRect(0, 0, 128, 128);
  const contactShadowMap = new THREE.CanvasTexture(csC);
  contactShadowMap.colorSpace = THREE.NoColorSpace;
  contactShadowMap.needsUpdate = true;

  return {
    asphaltMap,
    asphaltBump,
    asphaltNormal,
    asphaltRoughness,
    sidewalkMap,
    sidewalkBump,
    sidewalkNormal,
    brickFacadeMap,
    brickEmissiveMap,
    brickBump,
    brickNormal,
    fabricNormal,
    metalWearRoughness,
    glassTowerMap,
    glassTowerEmissiveMap,
    glassRoughnessMap,
    motijheelTowerMap,
    motijheelEmissiveMap,
    marketFacadeMap,
    residentialMap,
    residentialEmissiveMap,
    waterBumpMap,
    lampHaloMap,
    curbStripeMap,
    rickshawArtMap,
    contactShadowMap,
    dispose: () => {
      asphaltMap.dispose();
      asphaltBump.dispose();
      asphaltNormal.dispose();
      asphaltRoughness.dispose();
      sidewalkMap.dispose();
      sidewalkBump.dispose();
      sidewalkNormal.dispose();
      brickFacadeMap.dispose();
      brickEmissiveMap.dispose();
      brickBump.dispose();
      brickNormal.dispose();
      fabricNormal.dispose();
      metalWearRoughness.dispose();
      glassTowerMap.dispose();
      glassTowerEmissiveMap.dispose();
      glassRoughnessMap.dispose();
      motijheelTowerMap.dispose();
      motijheelEmissiveMap.dispose();
      marketFacadeMap.dispose();
      residentialMap.dispose();
      residentialEmissiveMap.dispose();
      waterBumpMap.dispose();
      lampHaloMap.dispose();
      curbStripeMap.dispose();
      rickshawArtMap.dispose();
      contactShadowMap.dispose();
    },
  };
}

/**
 * Generates an illuminated bilingual Bangla + English Dhaka shopfront signboard texture.
 */
export function createSignboardTexture(
  titleEn: string,
  titleBn: string,
  bgHex: number,
  accentHex: number,
): THREE.CanvasTexture {
  const [c, ctx] = makeCanvas(512, 128);
  const bg = `#${bgHex.toString(16).padStart(6, '0')}`;
  const acc = `#${accentHex.toString(16).padStart(6, '0')}`;

  ctx.fillStyle = bg;
  ctx.fillRect(0, 0, 512, 128);

  // Subtle top neon trim gradient
  const grad = ctx.createLinearGradient(0, 0, 0, 128);
  grad.addColorStop(0, 'rgba(255,255,255,0.08)');
  grad.addColorStop(1, 'rgba(0,0,0,0.22)');
  ctx.fillStyle = grad;
  ctx.fillRect(0, 0, 512, 128);

  ctx.strokeStyle = acc;
  ctx.lineWidth = 8;
  ctx.strokeRect(6, 6, 500, 116);

  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';

  ctx.fillStyle = '#fff8e7';
  ctx.font = 'bold 40px "Noto Sans Bengali", sans-serif';
  ctx.fillText(titleBn, 256, 46);

  ctx.fillStyle = acc;
  ctx.font = 'bold 28px "Barlow Condensed", sans-serif';
  ctx.fillText(titleEn.toUpperCase(), 256, 94);

  const tex = new THREE.CanvasTexture(c);
  tex.colorSpace = THREE.SRGBColorSpace;
  tex.needsUpdate = true;
  return tex;
}

/**
 * Generates an authentic green Bangladeshi overhead highway/avenue gantry direction board
 * with accurate Bangla and English district names.
 */
export function createHighwayGantryTexture(
  leftBn: string,
  leftEn: string,
  rightBn: string,
  rightEn: string,
): THREE.CanvasTexture {
  const [c, ctx] = makeCanvas(1024, 192);

  // Authentic Dhaka green highway sign board
  ctx.fillStyle = '#0c5c40';
  ctx.fillRect(0, 0, 1024, 192);
  ctx.strokeStyle = '#f8fafc';
  ctx.lineWidth = 10;
  ctx.strokeRect(10, 10, 1004, 172);

  // Center vertical divider
  ctx.fillStyle = 'rgba(248, 250, 252, 0.75)';
  ctx.fillRect(509, 24, 6, 144);

  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';

  // Left destination
  ctx.fillStyle = '#fef3c7';
  ctx.font = 'bold 46px "Noto Sans Bengali", sans-serif';
  ctx.fillText(leftBn, 260, 68);
  ctx.fillStyle = '#ffffff';
  ctx.font = 'bold 34px "Barlow Condensed", sans-serif';
  ctx.fillText(leftEn.toUpperCase(), 260, 134);

  // Right destination
  ctx.fillStyle = '#fef3c7';
  ctx.font = 'bold 46px "Noto Sans Bengali", sans-serif';
  ctx.fillText(rightBn, 764, 68);
  ctx.fillStyle = '#ffffff';
  ctx.font = 'bold 34px "Barlow Condensed", sans-serif';
  ctx.fillText(rightEn.toUpperCase(), 764, 134);

  const tex = new THREE.CanvasTexture(c);
  tex.colorSpace = THREE.SRGBColorSpace;
  tex.needsUpdate = true;
  return tex;
}
