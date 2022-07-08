// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#ifndef GRAPHICS_TOOLS_LIGHTING
#define GRAPHICS_TOOLS_LIGHTING

#include "GraphicsToolsCommon.hlsl"

/// <summary>
/// Forked from: https://github.com/microsoft/MixedReality-GraphicsTools-Unreal/blob/public/0.4.x/GraphicsToolsProject/Plugins/GraphicsTools/Shaders/Common/GTLighting.ush
/// </summary>

half GTDistribution(half roughness,
                    half NoH,
                    half3 NxH)
{
    // Walter et al. 2007, "Microfacet Models for Refraction through Rough Surfaces"

    // In mediump, there are two problems computing 1.0 - NoH^2
    // 1) 1.0 - NoH^2 suffers floating point cancellation when NoH^2 is close to 1 (highlights)
    // 2) NoH doesn't have enough precision around 1.0
    // Both problem can be fixed by computing 1-NoH^2 in highp and providing NoH in highp as well

    // However, we can do better using Lagrange's identity:
    //      ||a x b||^2 = ||a||^2 ||b||^2 - (a . b)^2
    // since N and H are unit vectors: ||N x H||^2 = 1.0 - NoH^2
    // This computes 1.0 - NoH^2 directly (which is close to zero in the highlights and has
    // enough precision).
    // Overall this yields better performance, keeping all computations in mediump
    half oneMinusNoHSquared = dot(NxH, NxH);

    half a = NoH * roughness;
    half k = roughness / (oneMinusNoHSquared + a * a);
    half d = k * k * GRAPHICS_TOOLS_INV_PI;

    return min(d, GRAPHICS_TOOLS_MEDIUMP_FLOAT_MAX);
}

// Calculated at full precision to avoid artifacts.
float GTVisibility(float roughness,
                   float NoV,
                   float NoL)
{
    // Hammon 2017, "PBR Diffuse Lighting for GGX+Smith Microsurfaces"
	return 0.5 / lerp(2 * NoL * NoV, NoL + NoV, roughness);
}

half3 GTFresnel(half3 f0,
                half LoH)
{
    // Schlick 1994, "An Inexpensive BRDF Model for Physically-Based Rendering"
    half f = pow(half(1) - LoH, half(5));
    return f + f0 * (1.0 - f); // f90 = 1.0
}

half3 GTSpecularLobe(half roughness,
                     half NoV,
                     half NoL,
                     half NoH,
                     half LoH,
                     half3 NxH,
                     half3 fresnel)
{
    half D = GTDistribution(roughness, NoH, NxH);
    float V = GTVisibility(roughness, NoV, NoL);
    half3 F = GTFresnel(fresnel, LoH);

    return (D * V) * F;
}

half3 GTDiffuseLobe(half3 baseColor)
{
    return baseColor * GRAPHICS_TOOLS_INV_TWO_PI;
}

half3 GTContributionDirectionalLight(half3 baseColor,
                                     half metallic,
                                     half roughnessSq,
                                     half specular,
                                     half3 worldNormal,
                                     half3 cameraVector,
                                     half3 lightDirection,
                                     half4 lightColorIntensity)
{
    half3 h = normalize(cameraVector + lightDirection);
    half NoL = saturate(dot(worldNormal, lightDirection));

#if GRAPHICS_TOOLS_FULLY_ROUGH
    half3 specularLobe = half3(0, 0, 0);
    half3 diffuseLobe = GTDiffuseLobe(baseColor);
#else
    // Neubelt and Pettineo 2013, "Crafting a Next-gen Material Pipeline for The Order: 1886"
    half NoV = max(dot(worldNormal, cameraVector), GRAPHICS_TOOLS_MIN_N_DOT_V);
    half NoH = saturate(dot(worldNormal, h));
    half LoH = saturate(dot(lightDirection, h));
    half dielectric = half(1) - metallic;
    half3 NxH = cross(worldNormal, h);
    half3 fresnel = half(0.16) * specular * specular * dielectric + baseColor * metallic;

    half3 specularLobe = GTSpecularLobe(roughnessSq, NoV, NoL, NoH, LoH, NxH, fresnel);
    half3 diffuseLobe = GTDiffuseLobe(baseColor) * dielectric;
#endif // GRAPHICS_TOOLS_FULLY_ROUGH

    return ((diffuseLobe + specularLobe) * lightColorIntensity.rgb) * lightColorIntensity.a * NoL;
}

half3 GTContributionSH(half3 baseColor,
                       half metallic,
                       half roughness,
                       half3 skySHDiffuse)
{
	return (skySHDiffuse * baseColor) * max(half(0.3), min(half(1) - metallic, half(1) - roughness));
}

#define GRAPHICS_TOOLS_REFLECTION_CUBE_MAX_MIP UNITY_SPECCUBE_LOD_STEPS

half3 GTContributionReflection(half3 baseColor,
                               half metallic,
                               half roughnessSq,
                               half3 reflectionVector)
{
#if defined(_REFLECTIONS)
    half lod = (GRAPHICS_TOOLS_REFLECTION_CUBE_MAX_MIP - half(1)) - (half(1) - log2(roughnessSq));
#if defined(_URP)
    half4 data = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectionVector, lod);
    return DecodeHDREnvironment(data, unity_SpecCube0_HDR) * baseColor * max(metallic, half(0.1));
#else
    half4 data = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, reflectionVector, lod);
    return DecodeHDR(data, unity_SpecCube0_HDR) * baseColor * max(metallic, half(0.1));
#endif // _URP
#else
    return unity_IndirectSpecColor.rgb;
#endif // _REFLECTIONS
}

/// <summary>
/// Custom lighting methods.
/// </summary>

inline float GTNearLightDistance(float4 light, float3 worldPosition)
{
    return distance(worldPosition, light.xyz) + ((1.0 - light.w) * GRAPHICS_TOOLS_MAX_NEAR_LIGHT_DIST);
}

inline float GTHoverLight(float4 hoverLight, float inverseRadius, float3 worldPosition)
{
    return (1.0 - saturate(length(hoverLight.xyz - worldPosition) * inverseRadius)) * hoverLight.w;
}

inline float GTProximityLight(float4 proximityLight, float4 proximityLightParams, float4 proximityLightPulseParams, float3 worldPosition, float3 worldNormal, out half colorValue)
{
    float proximityLightDistance = dot(proximityLight.xyz - worldPosition, worldNormal);
#if defined(_PROXIMITY_LIGHT_TWO_SIDED)
    worldNormal = proximityLightDistance < 0.0 ? -worldNormal : worldNormal;
    proximityLightDistance = abs(proximityLightDistance);
#endif
    float normalizedProximityLightDistance = saturate(proximityLightDistance * proximityLightParams.y);
    float3 projectedProximityLight = proximityLight.xyz - (worldNormal * abs(proximityLightDistance));
    float projectedProximityLightDistance = length(projectedProximityLight - worldPosition);
    float attenuation = (1.0 - normalizedProximityLightDistance) * proximityLight.w;
    colorValue = saturate(projectedProximityLightDistance * proximityLightParams.z);
    float pulse = step(proximityLightPulseParams.x, projectedProximityLightDistance) * proximityLightPulseParams.y;

    return smoothstep(1.0, 0.0, projectedProximityLightDistance / (proximityLightParams.x * max(pow(normalizedProximityLightDistance, 0.25), proximityLightParams.w))) * pulse * attenuation;
}

inline half3 GTMixProximityLightColor(half4 centerColor, half4 middleColor, half4 outerColor, half t)
{
    half3 color = lerp(centerColor.rgb, middleColor.rgb, smoothstep(centerColor.a, middleColor.a, t));
    return lerp(color, outerColor.rgb, smoothstep(middleColor.a, outerColor.a, t));
}

#endif // GRAPHICS_TOOLS_LIGHTING