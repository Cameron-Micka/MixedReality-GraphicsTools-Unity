// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#ifndef GT_LIGHTING_UNITY
#define GT_LIGHTING_UNITY

#include "GraphicsToolsLighting.hlsl"

half3 GTContributionDefaultLit(half3 BaseColor,
                               half3 SkySHDiffuse,
                               half Metallic,
                               half Specular,
                               half Roughness,
                               half3 Normal,
                               half AmbientOcclusion,
                               half3 CameraVector,
                               half3 ReflectionVector,
                               half3 DirectionalLightDirection,
                               half4 DirectionalLightColorIntensity)
{
    half RoughnessSq = clamp(Roughness * Roughness, GRAPHICS_TOOLS_MIN_N_DOT_V, half(1));

    half3 Result = half3(0, 0, 0);

#if !GT_FULLY_ROUGH
#if defined(_SPHERICAL_HARMONICS)
    // Indirect (spherical harmonics)
    Result += GTContributionSH(BaseColor,
                               Metallic,
                               Roughness,
                               SkySHDiffuse);
#else

#endif // _SPHERICAL_HARMONICS

    // Indirect (reflection cube).
    Result += GTContributionReflection(BaseColor,
                                       Metallic,
                                       RoughnessSq,
                                       ReflectionVector);
#else
    Result += BaseColor;
#endif // GT_FULLY_ROUGH

    // Direct (directional light).
    Result += GTContributionDirectionalLight(BaseColor,
                                             Metallic,
                                             RoughnessSq,
                                             Specular,
                                             Normal,
                                             CameraVector,
                                             DirectionalLightDirection,
                                             DirectionalLightColorIntensity);

    half EnergyCompensation = 1;//half(1) + (half(1) - (Metallic * half(1.5 * Roughness)));

    return Result * EnergyCompensation * AmbientOcclusion;
}

#endif // GT_LIGHTING_UNITY