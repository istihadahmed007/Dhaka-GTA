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
}

/**
 * Resolves safe rendering parameters for a chosen quality preset while
 * protecting software WebGL renderers (SwiftShader / llvmpipe) from heavy stalls.
 */
export function resolveGraphicsQualityConfig(
  preset: GraphicsQualityPreset,
  isSoftwareGl: boolean,
  devicePixelRatio = 1,
): GraphicsQualityConfig {
  const safeDpr = Math.max(1, Number.isFinite(devicePixelRatio) ? devicePixelRatio : 1);

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
    };
  }

  if (preset === 'medium') {
    return {
      preset: 'medium',
      pixelRatio: Math.min(safeDpr, 1.5),
      shadowsEnabled: true,
      shadowMapSize: 1024,
      lampHalosEnabled: true,
      rainDensityScale: 0.75,
      envIntensityMultiplier: 0.95,
      npcAnimationMaxDist: 75,
    };
  }

  return {
    preset: 'high',
    pixelRatio: Math.min(safeDpr, 2),
    shadowsEnabled: true,
    shadowMapSize: 2048,
    lampHalosEnabled: true,
    rainDensityScale: 1.0,
    envIntensityMultiplier: 1.0,
    npcAnimationMaxDist: 105,
  };
}
