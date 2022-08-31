// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

Shader "Hidden/Profiler"
{
    Properties
    {
        _Color("Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _BaseColor("Base Color", Color) = (0.0, 0.0, 0.0, 1.0)
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
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR0;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _BaseColor;
            sampler2D _FontTexture;
            float2 _FontScale;
            float4x4 _ParentLocalToWorldMatrix;

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
            UNITY_DEFINE_INSTANCED_PROP(float4, _UVOffsetScaleX)
            UNITY_INSTANCING_BUFFER_END(Props)

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float4 uvOffsetScaleX = UNITY_ACCESS_INSTANCED_PROP(Props, _UVOffsetScaleX);

                // The verticies on the right (UV 1, x) are scaled in the positive X direction for progress bars.
                float3 localVertex = v.vertex.xyz;
                localVertex.x += v.uv.x * uvOffsetScaleX.z;

                // Conver from local to (window) parent to world space.
                // We do this in the vertex shader to avoid having to iterate over all instances each frame.
                o.vertex = mul(UNITY_MATRIX_VP, mul(_ParentLocalToWorldMatrix, mul(unity_ObjectToWorld, float4(localVertex, 1.0))));
                o.color = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);

                // Scale and offset UVs.
                o.uv = (v.uv * _FontScale) + uvOffsetScaleX.xy;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 font = tex2D(_FontTexture, i.uv);
                fixed alpha = font.r;
                return fixed4((_BaseColor.rgb * (1.0 - alpha)) + (font.rgb * i.color.rgb * alpha), 1.0);
            }

            ENDCG
        }
    }
}
