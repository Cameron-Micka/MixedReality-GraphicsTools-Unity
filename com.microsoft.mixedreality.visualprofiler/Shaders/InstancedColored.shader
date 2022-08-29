// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

Shader "Hidden/Instanced-Colored"
{
    Properties
    {
        _Color("Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _FontTexture("Font", 2D) = "black" {}
    }

    SubShader
    {
        Pass
        {
            Name "Main"
            Tags{ "RenderType" = "Opaque" }
            ZWrite On
            ZTest Always
            Cull Off

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            // Comment in to help with RenderDoc debugging.
            //#pragma enable_d3d11_debug_symbols

            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct appdata_t
            {
                fixed4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                fixed4 vertex : SV_POSITION;
                fixed4 color : COLOR0;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _FontTexture;
            float4x4 _ParentLocalToWorldMatrix;

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            UNITY_DEFINE_INSTANCED_PROP(float4, _UVScaleOffset)
            UNITY_INSTANCING_BUFFER_END(Props)

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // Conver from local to (window) parent to world space.
                // We do this in the vertex shader to avoid having to iterate over all instances each frame.
                o.vertex = mul(UNITY_MATRIX_VP, mul(_ParentLocalToWorldMatrix, mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0))));
                o.color = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);

                // Scale and offset UVs.
                float4 uvScaleOffset = UNITY_ACCESS_INSTANCED_PROP(Props, _UVScaleOffset);
                o.uv = (v.uv * uvScaleOffset.xy) + uvScaleOffset.zw;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return tex2D(_FontTexture, i.uv) * i.color;
            }

            ENDCG
        }
    }
}
