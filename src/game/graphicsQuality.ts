export type GraphicsQualityPreset = 'low' | 'medium' | 'high';

export interface GraphicsQualityConfig {
  preset: GraphicsQualityPreset;
  pixelRatio: number;
  shadowsEnabled: boolean;
  shadowMapSize: number;
  lampHalosEnabled: boolean;
  rainDensityScale: number;
  envIntensityMultiplier: number;
  npcAnimationMaxDist: number;
  lodDistances: { near: number; med: number; far: number };
}

export function isMobileDevice(): boolean {
  if (typeof navigator === 'undefined') return false;
  return /Mobi|Android|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent);
}

/**
 * Resolves safe rendering parameters for a chosen quality preset while
 * protecting software WebGL renderers (SwiftShader / llvmpipe) and mobile GPUs from heavy stalls.
 */
export function resolveGraphicsQualityConfig(
  preset: GraphicsQualityPreset,
  isSoftwareGl: boolean,
  devicePixelRatio = 1,
): GraphicsQualityConfig {
  const safeDpr = Math.max(1, Number.isFinite(devicePixelRatio) ? devicePixelRatio : 1);
  const mobile = isMobileDevice();

  if (isSoftwareGl) {
    // Software WebGL fallback always keeps shadows disabled and DPR locked to 1
    // so frame times stay interactive across all presets.
    return {
      preset,
      pixelRatio: 1,
      shadowsEnabled: false,
      shadowMapSize: 512,
      lampHalosEnabled: preset !== 'low',
      rainDensityScale: preset === 'low' ? 0.45 : preset === 'medium' ? 0.72 : 1.0,
      envIntensityMultiplier: preset === 'low' ? 0.85 : 1.0,
      npcAnimationMaxDist: preset === 'low' ? 42 : preset === 'medium' ? 68 : 90,
      lodDistances: { near: 60, med: 130, far: 210 },
    };
  }

  if (preset === 'low') {
    return {
      preset: 'low',
      pixelRatio: 1,
      shadowsEnabled: false,
      shadowMapSize: 512,
      lampHalosEnabled: false,
      rainDensityScale: 0.45,
      envIntensityMultiplier: 0.85,
      npcAnimationMaxDist: 48,
      lodDistances: { near: 65, med: 140, far: 220 },
    };
  }

  if (preset === 'medium') {
    return {
      preset: 'medium',
      pixelRatio: Math.min(safeDpr, mobile ? 1.25 : 1.5),
      shadowsEnabled: true,
      shadowMapSize: mobile ? 512 : 1024,
      lampHalosEnabled: true,
      rainDensityScale: 0.75,
      envIntensityMultiplier: 0.95,
      npcAnimationMaxDist: 75,
      lodDistances: { near: 90, med: 190, far: 300 },
    };
  }

  return {
    preset: 'high',
    pixelRatio: Math.min(safeDpr, mobile ? 1.5 : 2),
    shadowsEnabled: true,
    shadowMapSize: 2048,
    lampHalosEnabled: true,
    rainDensityScale: 1.0,
    envIntensityMultiplier: 1.0,
    npcAnimationMaxDist: 105,
    lodDistances: { near: 115, med: 240, far: 380 },
  };
}
