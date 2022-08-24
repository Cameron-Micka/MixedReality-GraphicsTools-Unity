Shader "SDF Font"
{
    Properties
    {
        _Font("Font", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM

            //#pragma enable_d3d11_debug_symbols

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                float scaleX = length(mul(UNITY_MATRIX_M, float4(1.0, 0.0, 0.0, 0.0)));
                float scaleY = length(mul(UNITY_MATRIX_M, float4(0.0, 1.0, 0.0, 0.0)));
                o.uv.x *= (scaleX / scaleY);

                return o;
            }

            sampler2D _Font;

            #define CH_A 0
            #define CH_B 1
            #define CH_C 2
            #define CH_D 3
            #define CH_E 4
            #define CH_F 5
            #define CH_G 6
            #define CH_H 7
            #define CH_I 8
            #define CH_J 9
            #define CH_K 10
            #define CH_L 11
            #define CH_M 12
            #define CH_N 13
            #define CH_O 14
            #define CH_P 15
            #define CH_Q 16
            #define CH_R 17
            #define CH_S 18
            #define CH_T 19
            #define CH_U 20
            #define CH_V 21
            #define CH_W 22
            #define CH_X 23
            #define CH_Y 24
            #define CH_Z 25
            #define CH_0 26
            #define CH_1 27
            #define CH_2 28
            #define CH_3 29
            #define CH_4 30
            #define CH_5 31
            #define CH_6 32
            #define CH_7 33
            #define CH_8 34
            #define CH_9 35

            #define C(c) U.x-=.5; output += char(U,65+c)
            #define SPACE U.x-=.5

            float4 char(float2 p, int c)
            {
                if (p.x < 0.0 || p.x > 1.0 || p.y < 0.0 || p.y > 1.0)
                {
                    return float4(0, 0, 0, 1);
                }

                float smoothing = 64.0;
                float temp = 16.0;

                return tex2D(_Font, p / 16. + frac(float2(c, 15 - c / 16) / 16.), ddx(p / smoothing), ddy(p / smoothing));
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float4 output = (float4)0;
                float2 position = float2(0, .25);
                float FontSize = 32.;
                float2 U = (i.uv - position) * 64.0 / FontSize;

                // FRAME:
                C(CH_F); C(CH_R); C(CH_A); C(CH_M); C(CH_E); C(-7); 
                
                // 9.99
                SPACE; C(-8); C(-19); C(-8); C(-8);

                return output.xxxx;
            }
            ENDCG
        }
    }
}
