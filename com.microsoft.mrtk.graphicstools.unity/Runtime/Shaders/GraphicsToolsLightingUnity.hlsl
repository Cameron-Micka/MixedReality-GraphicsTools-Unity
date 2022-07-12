// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#ifndef GT_LIGHTING_UNITY
#define GT_LIGHTING_UNITY

#include "GraphicsToolsLighting.hlsl"

struct GTLight
{
    half3 direction;
    half3 color;
};

GTLight GTGetMainLight()
{
    GTLight light;
#if defined(_DIRECTIONAL_LIGHT) || defined(_DISTANT_LIGHT)
#if defined(_DISTANT_LIGHT)
    light.direction = _DistantLightData[0].xyz;
    light.color = _DistantLightData[1].xyz;
#else
#if defined(_URP)
    Light directionalLight = GetMainLight();
    light.direction = directionalLight.direction;
    light.color = directionalLight.color;
#else
    light.direction = _WorldSpaceLightPos0.xyz;
    light.color = _LightColor0.rgb;
#endif
#endif
#endif
    return light;
}

half3 GTContributionDefaultLit(half3 BaseColor,
                               half Metallic,
                               half Smoothness,
                               half3 WorldNormal,
                               half3 CameraVector,
                               half3 DirectionalLightDirection,
                               half4 DirectionalLightColorIntensity,
                               half3 SkySHDiffuse)
{
    // 100% smooth surfaces do not exist.
    half SmoothnessClamp = clamp(Smoothness, half(0), half(0.9));
    half Roughness = half(1) - SmoothnessClamp;
    half RoughnessSq = clamp(Roughness * Roughness, GRAPHICS_TOOLS_MIN_N_DOT_V, half(1));

    half3 Result = half3(0, 0, 0);

#if !GT_FULLY_ROUGH
    // Indirect (spherical harmonics)
    half EnergyCompensation = 1.25 + (2.75 * min(SmoothnessClamp, RoughnessSq));
    Result += GTContributionSH(BaseColor,
                               Metallic,
                               RoughnessSq,
                               SkySHDiffuse) * EnergyCompensation;

#if defined(_REFLECTIONS)
    // Indirect (reflection cube).
    half3 WorldReflection = reflect(-CameraVector, WorldNormal);
    Result += GTContributionReflection(BaseColor,
                                       Metallic,
                                       RoughnessSq,
                                       WorldReflection) * SmoothnessClamp;
#endif // _REFLECTIONS
#else
    Result += BaseColor * 0.1;
#endif // GT_FULLY_ROUGH

    // Direct (directional light).
    Result += GTContributionDirectionalLight(BaseColor,
                                             Metallic,
                                             RoughnessSq,
                                             half(1.0),
                                             WorldNormal,
                                             CameraVector,
                                             DirectionalLightDirection,
                                             DirectionalLightColorIntensity) * 4;

    return Result;
}

#endif // GT_LIGHTING_UNITY