// Dhaka-GTA HDRP Shader Graph Custom Function HLSL Library
// Computes procedural rainwater puddle accumulation, porosity albedo darkening,
// mirror-like specular smoothness boost, and animated monsoon raindrop ripple normals.

#ifndef DHAKA_WET_SURFACE_INCLUDED
#define DHAKA_WET_SURFACE_INCLUDED

float DhakaHash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float DhakaValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float a = DhakaHash21(i);
    float b = DhakaHash21(i + float2(1.0, 0.0));
    float c = DhakaHash21(i + float2(0.0, 1.0));
    float d = DhakaHash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

void ApplyDhakaWetPuddles_float(
    float3 WorldPos,
    float3 DryAlbedo,
    float DrySmoothness,
    float3 DryNormalTS,
    float Wetness,
    float RainTime,
    out float3 OutAlbedo,
    out float OutSmoothness,
    out float OutCoatMask,
    out float3 OutNormalTS)
{
    float macroNoise = DhakaValueNoise(WorldPos.xz * 0.085) * 0.65 + DhakaValueNoise(WorldPos.xz * 0.24) * 0.35;
    float puddleMask = saturate((Wetness * 1.35 - (1.0 - macroNoise)) * 3.0);

    // Porous asphalt darkens when damp; puddles flatten micro-normal and reflect sky/streetlamps
    float dampTint = lerp(1.0, 0.46, saturate(Wetness * 0.8 + puddleMask * 0.4));
    OutAlbedo = DryAlbedo * dampTint;
    OutSmoothness = lerp(DrySmoothness, lerp(0.68, 0.96, puddleMask), saturate(Wetness));
    OutCoatMask = saturate(Wetness * 0.65 + puddleMask * 0.35);

    // Procedural raindrop ring ripple in puddles
    float2 cellUV = frac(WorldPos.xz * 1.45) - 0.5;
    float2 cellID = floor(WorldPos.xz * 1.45);
    float seed = DhakaHash21(cellID);
    float age = frac(RainTime * 1.2 + seed);
    float dist = length(cellUV);
    float ring = sin(saturate(1.0 - abs(dist - age * 0.45) * 14.0) * 3.14159) * (1.0 - age) * puddleMask * Wetness;

    float2 rippleGrad = normalize(cellUV + 1e-4) * ring * 0.28;
    float3 flatNormal = float3(rippleGrad.x, rippleGrad.y, 1.0);
    OutNormalTS = normalize(lerp(DryNormalTS, flatNormal, puddleMask * 0.85));
}

#endif
