// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#ifndef GT_LIGHTING_UNITY
#define GT_LIGHTING_UNITY

#include "GraphicsToolsLighting.hlsl"

struct GTMainLight
{
    half3 direction;
    half3 color;
};

GTMainLight GTGetMainLight()
{
    GTMainLight light;
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

#endif // GT_LIGHTING_UNITY
