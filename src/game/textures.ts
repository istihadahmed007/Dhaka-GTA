import * as THREE from 'three';

export interface DhakaTextureSet {
  asphaltMap: THREE.CanvasTexture;
  asphaltBump: THREE.CanvasTexture;
  sidewalkMap: THREE.CanvasTexture;
  brickFacadeMap: THREE.CanvasTexture;
  brickBump: THREE.CanvasTexture;
  glassTowerMap: THREE.CanvasTexture;
  residentialMap: THREE.CanvasTexture;
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
  if (isColor) {
    tex.colorSpace = THREE.SRGBColorSpace;
  }
  tex.needsUpdate = true;
  return tex;
}

export function createDhakaTextures(maxAniso: number): DhakaTextureSet {
  // 1. Weathered Dhaka Asphalt + Yellow Dashed Centerline
  const [asphC, asphCtx] = makeCanvas(512, 512);
  asphCtx.fillStyle = '#2b2f36';
  asphCtx.fillRect(0, 0, 512, 512);
  for (let i = 0; i < 6500; i++) {
    const x = (i * 73) % 512;
    const y = (i * 197 + Math.floor(i / 7) * 31) % 512;
    const shade = 36 + ((i * 29) % 24);
    asphCtx.fillStyle = `rgb(${shade},${shade + 2},${shade + 5})`;
    asphCtx.fillRect(x, y, 2, 2);
  }
  // Subtle tire wear tracks
  asphCtx.fillStyle = 'rgba(18, 20, 24, 0.28)';
  asphCtx.fillRect(105, 0, 62, 512);
  asphCtx.fillRect(345, 0, 62, 512);
  // Crisp yellow dashed lane center + white edge lines
  asphCtx.fillStyle = '#d9a426';
  for (let y = 16; y < 512; y += 96) {
    asphCtx.fillRect(250, y, 12, 52);
  }
  asphCtx.fillStyle = 'rgba(230, 232, 236, 0.55)';
  asphCtx.fillRect(18, 0, 8, 512);
  asphCtx.fillRect(486, 0, 8, 512);

  const asphaltMap = finalizeTexture(asphC, true, maxAniso, 1, 1);

  // Asphalt micro-relief bump map (linear)
  const [abC, abCtx] = makeCanvas(256, 256);
  abCtx.fillStyle = '#808080';
  abCtx.fillRect(0, 0, 256, 256);
  for (let i = 0; i < 4000; i++) {
    const x = (i * 53) % 256;
    const y = (i * 113) % 256;
    const v = 90 + ((i * 37) % 85);
    abCtx.fillStyle = `rgb(${v},${v},${v})`;
    abCtx.fillRect(x, y, 2, 2);
  }
  const asphaltBump = finalizeTexture(abC, false, maxAniso, 1, 1);

  // 2. Concrete Sidewalk + Iconic Dhaka Black-and-Yellow Curb Accent
  const [swC, swCtx] = makeCanvas(256, 256);
  swCtx.fillStyle = '#9e978b';
  swCtx.fillRect(0, 0, 256, 256);
  swCtx.strokeStyle = 'rgba(60, 56, 50, 0.35)';
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
  for (let i = 0; i < 1200; i++) {
    const x = (i * 61) % 256;
    const y = (i * 139) % 256;
    swCtx.fillStyle = i % 2 === 0 ? 'rgba(255,250,240,0.08)' : 'rgba(40,35,30,0.08)';
    swCtx.fillRect(x, y, 3, 3);
  }
  const sidewalkMap = finalizeTexture(swC, true, maxAniso, 18, 18);

  // 3. Old Dhaka Terracotta / Weathered Colonial Brick & Arched Windows
  const [brC, brCtx] = makeCanvas(512, 512);
  brCtx.fillStyle = '#c88d65';
  brCtx.fillRect(0, 0, 512, 512);
  // Subtle brick courses
  for (let row = 0; row < 32; row++) {
    const y = row * 16;
    brCtx.fillStyle = 'rgba(72, 34, 20, 0.14)';
    brCtx.fillRect(0, y, 512, 2);
    const offset = (row % 2) * 24;
    for (let x = offset; x < 512; x += 48) {
      brCtx.fillRect(x, y, 2, 16);
    }
  }
  // Monsoon plaster weathering stains near cornice
  const stainGrad = brCtx.createLinearGradient(0, 0, 0, 90);
  stainGrad.addColorStop(0, 'rgba(42, 34, 28, 0.32)');
  stainGrad.addColorStop(1, 'rgba(42, 34, 28, 0)');
  brCtx.fillStyle = stainGrad;
  brCtx.fillRect(0, 0, 512, 90);

  // Arched Mughal-colonial shuttered windows
  for (let floor = 0; floor < 3; floor++) {
    const wy = 48 + floor * 158;
    for (let col = 0; col < 4; col++) {
      const wx = 32 + col * 124;
      // Arch frame
      brCtx.fillStyle = '#e7d5be';
      brCtx.beginPath();
      brCtx.arc(wx + 32, wy + 26, 32, Math.PI, 0);
      brCtx.lineTo(wx + 64, wy + 96);
      brCtx.lineTo(wx, wy + 96);
      brCtx.closePath();
      brCtx.fill();
      // Deep interior / teal wooden louver shutters
      brCtx.fillStyle = (floor + col) % 3 === 0 ? '#1b4d47' : '#262b30';
      brCtx.beginPath();
      brCtx.arc(wx + 32, wy + 28, 25, Math.PI, 0);
      brCtx.lineTo(wx + 57, wy + 90);
      brCtx.lineTo(wx + 7, wy + 90);
      brCtx.closePath();
      brCtx.fill();
      // Window bars
      brCtx.fillStyle = '#8b5e3c';
      brCtx.fillRect(wx + 30, wy + 6, 4, 84);
      brCtx.fillRect(wx + 7, wy + 48, 50, 4);
    }
  }
  const brickFacadeMap = finalizeTexture(brC, true, maxAniso, 1, 1);

  // Brick bump map
  const [bbC, bbCtx] = makeCanvas(256, 256);
  bbCtx.fillStyle = '#888888';
  bbCtx.fillRect(0, 0, 256, 256);
  bbCtx.fillStyle = '#505050';
  for (let row = 0; row < 16; row++) {
    bbCtx.fillRect(0, row * 16, 256, 2);
    const off = (row % 2) * 16;
    for (let x = off; x < 256; x += 32) {
      bbCtx.fillRect(x, row * 16, 2, 16);
    }
  }
  const brickBump = finalizeTexture(bbC, false, maxAniso, 2, 2);

  // 4. Gulshan / Banani Modern Glass Curtain Wall
  const [glC, glCtx] = makeCanvas(512, 512);
  glCtx.fillStyle = '#243b53';
  glCtx.fillRect(0, 0, 512, 512);
  for (let r = 0; r < 8; r++) {
    const y = r * 64;
    // Spandrel concrete band
    glCtx.fillStyle = '#486581';
    glCtx.fillRect(0, y, 512, 14);
    for (let c = 0; c < 6; c++) {
      const x = c * 85 + 6;
      const lit = (r * 7 + c * 11) % 5 === 0;
      const grad = glCtx.createLinearGradient(x, y + 14, x, y + 60);
      if (lit) {
        grad.addColorStop(0, '#f6d38b');
        grad.addColorStop(1, '#c59449');
      } else {
        grad.addColorStop(0, '#5a82a6');
        grad.addColorStop(1, '#1d354d');
      }
      glCtx.fillStyle = grad;
      glCtx.fillRect(x, y + 14, 74, 46);
    }
  }
  const glassTowerMap = finalizeTexture(glC, true, maxAniso, 1, 2);

  // 5. Dhaka Residential Concrete Apartment Facade with Balconies
  const [resC, resCtx] = makeCanvas(512, 512);
  resCtx.fillStyle = '#d4c9b8';
  resCtx.fillRect(0, 0, 512, 512);
  for (let r = 0; r < 4; r++) {
    const y = r * 128;
    // Horizontal concrete slab
    resCtx.fillStyle = '#a89f91';
    resCtx.fillRect(0, y + 114, 512, 14);
    for (let c = 0; c < 3; c++) {
      const x = 22 + c * 164;
      // Recessed balcony opening
      resCtx.fillStyle = '#2a3038';
      resCtx.fillRect(x, y + 20, 122, 92);
      // Warm interior window glow on select flats
      if ((r + c * 2) % 3 === 1) {
        resCtx.fillStyle = '#d49d54';
        resCtx.fillRect(x + 18, y + 32, 46, 54);
      }
      // Balcony railing
      resCtx.fillStyle = '#5c4d42';
      resCtx.fillRect(x, y + 72, 122, 6);
      for (let rx = x + 6; rx < x + 120; rx += 14) {
        resCtx.fillRect(rx, y + 72, 3, 40);
      }
    }
  }
  const residentialMap = finalizeTexture(resC, true, maxAniso, 1, 1);

  // 6. Bangladeshi Folk Rickshaw Art Panel (Lotus, Bengal Tiger Stripes, Sunburst)
  const [raC, raCtx] = makeCanvas(256, 128);
  raCtx.fillStyle = '#d62828';
  raCtx.fillRect(0, 0, 256, 128);
  raCtx.strokeStyle = '#fcbf49';
  raCtx.lineWidth = 8;
  raCtx.strokeRect(6, 6, 244, 116);
  // Teal folk arch
  raCtx.fillStyle = '#007f5f';
  raCtx.beginPath();
  raCtx.arc(128, 120, 88, Math.PI, 0);
  raCtx.fill();
  // Pink-gold lotus petals
  for (let i = -2; i <= 2; i++) {
    raCtx.fillStyle = i === 0 ? '#ffd166' : '#ff4d6d';
    raCtx.beginPath();
    raCtx.ellipse(128 + i * 22, 78 + Math.abs(i) * 8, 16, 30, i * 0.28, 0, Math.PI * 2);
    raCtx.fill();
  }
  // Decorative border dots
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
  rad.addColorStop(0, 'rgba(0, 0, 0, 0.65)');
  rad.addColorStop(0.55, 'rgba(0, 0, 0, 0.30)');
  rad.addColorStop(1, 'rgba(0, 0, 0, 0.0)');
  csCtx.fillStyle = rad;
  csCtx.fillRect(0, 0, 128, 128);
  const contactShadowMap = new THREE.CanvasTexture(csC);
  contactShadowMap.needsUpdate = true;

  return {
    asphaltMap,
    asphaltBump,
    sidewalkMap,
    brickFacadeMap,
    brickBump,
    glassTowerMap,
    residentialMap,
    rickshawArtMap,
    contactShadowMap,
    dispose: () => {
      asphaltMap.dispose();
      asphaltBump.dispose();
      sidewalkMap.dispose();
      brickFacadeMap.dispose();
      brickBump.dispose();
      glassTowerMap.dispose();
      residentialMap.dispose();
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
