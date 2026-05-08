#ifndef WATER_FOG_INCLUDED
#define WATER_FOG_INCLUDED

// Volumetric fog applied to a transparent surface (water).
// Matches the box volume fog shader's distance + height falloff so
// water blends seamlessly with the rest of the scene's fog.

void ApplyVolumetricFog_float(
    float3 BaseColor,
    float3 PositionWS,
    float3 CameraPositionWS,
    float  Visibility,
    float3 FogColor,
    float  HeightStart,
    float  HeightEnd,
    float  HeightFalloff,
    out float3 OutColor)
{
    // Default-init guarantees full assignment on every code path
    OutColor = BaseColor;

    // Distance-based extinction (Beer-Lambert)
    float dist = distance(PositionWS, CameraPositionWS);
    float extinction = exp(-dist / max(0.001, Visibility));
    float fogAmount = 1.0 - extinction;

    // Height falloff — fog is denser near HeightStart, gone above HeightEnd
    float h = saturate((PositionWS.y - HeightStart) / max(0.001, HeightEnd - HeightStart));
    float heightFactor = pow(1.0 - h, HeightFalloff);

    float blend = saturate(fogAmount * heightFactor);

    OutColor = lerp(BaseColor, FogColor, blend);
}

#endif
