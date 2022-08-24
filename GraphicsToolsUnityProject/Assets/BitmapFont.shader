Shader "Bitmap Font"
{
    Properties
    {
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

            // Source: https://www.shadertoy.com/view/4s3fzl
            static int font[53] =
            {
                0x69f99, 0x79797, 0xe111e, 0x79997, 0xf171f, 0xf1711, 0xe1d96, 0x99f99,
                0xf444f, 0x88996, 0x95159, 0x1111f, 0x9f999, 0x9bd99, 0x69996, 0x79971,
                0x69b5a, 0x79759, 0xe1687, 0xf4444, 0x99996, 0x999a4, 0x999f9, 0x99699,
                0x99e8e, 0xf843f, 0x6bd96, 0x46444, 0x6942f, 0x69496, 0x99f88, 0xf1687,
                0x61796, 0xf8421, 0x69696, 0x69e84, 0x66400, 0x0faa9, 0x0000f, 0x00600,
                0x0a500, 0x02720, 0x0f0f0, 0x08421, 0x33303, 0x69404, 0x00032, 0x00002,
                0x55000, 0x00000, 0x00202, 0x42224, 0x24442
            };
            
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
            #define CH_APST 36
            #define CH_PI   37
            #define CH_UNDS 38
            #define CH_HYPH 39
            #define CH_TILD 40
            #define CH_PLUS 41
            #define CH_EQUL 42
            #define CH_SLSH 43
            #define CH_EXCL 44
            #define CH_QUES 45
            #define CH_COMM 46
            #define CH_FSTP 47
            #define CH_QUOT 48 
            #define CH_BLNK 49
            #define CH_COLN 50
            #define CH_LPAR 51
            #define CH_RPAR 52
            
            static int2 MAP_SIZE = int2(4, 5);
            static float2 MAP_SIZE_F = float2(4.0, 5.0);
            
            /*
                Draws a character, given its encoded value, a position, size and
                current [0..1] uv coordinate.
            */
            int drawChar(in int char, in float2 pos, in float2 size, in float2 uv)
            {
                // Subtract our position from the current uv so that we can
                // know if we're inside the bounding box or not.
                uv -= pos;
            
                // Divide the screen space by the size, so our bounding box is 1x1.
                uv /= size;
            
                // Multiply the UV by the bitmap size so we can work in
                // bitmap space coordinates.
                uv *= MAP_SIZE_F;
            
                // Compute bitmap texel coordinates
                int2 iuv = int2(round(uv));
            
                // Bounding box check. With branches, so we avoid the maths and lookups
                if (iuv.x < 0 || iuv.x > (MAP_SIZE.x - 1) || 
                    iuv.y < 0 || iuv.y > (MAP_SIZE.y - 1)) 
                {
                    return 0;
                }
                else
                {
                    // Compute bit index
                    int index = MAP_SIZE.x * iuv.y + iuv.x;

                    // Get the appropriate bit and return it.
                    return (font[char] >> index) & 1;
                }
            }
            
            /*
                Prints a float as an int. Be very careful about overflow.
                This as a side effect will modify the character position,
                so that multiple calls to this can be made without worrying
                much about kerning.
            */
            int drawIntCarriage(in int val, inout float2 pos, in float2 size, in float2 uv, in int places)
            {
                // Create a place to store the current values.
                int res = 0;
                // Surely it won't be more than 10 chars long, will it?
                // (MAX_INT is 10 characters)
                for (int i = 0; i < 10; ++i)
                {
                    // If we've run out of film, cut!
                    if (val == 0 && i >= places)
                    {
                        break;
                    }
                    
                    // The current lsd is the difference between the current
                    // value and the value rounded down one place.
                    int digit = val % 10u;
                    // Draw the character. Since there are no overlaps, we don't
                    // need max().
                    res |= drawChar(CH_0 + digit,pos,size,uv);
                    // Move the carriage.
                    pos.x -= size.x * 1.2;
                    // Truncate away this most recent digit.
                    val /= 10u;
                }
                return res;
            }
            
            /*
                Prints a fixed point fractional value. Be even more careful about overflowing.
            */
            int drawFixed(in float val, in int places, in float2 pos, in float2 size, in float2 uv)
            {
                float fval, ival;
                fval = modf(val, ival);
            
                float2 p = float2(pos);
            
                // Draw the floating point part.
                int res = drawIntCarriage(int(fval * pow(10.0,float(places))), p, size, uv, places);
                // The decimal is tiny, so we back things up a bit before drawing it.
                p.x += size.x * .4;
                res |= drawChar(CH_FSTP,p,size,uv); p.x -= size.x * 1.2;
                // And after as well.
                p.x += size.x * .1;
                // Draw the integer part.
                res |= drawIntCarriage(int(ival),p,size,uv,1);

                return res;
            }

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float ratio : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

                float2 scale;
                scale.x = length(mul(UNITY_MATRIX_M, float4(1.0, 0.0, 0.0, 0.0)));
                scale.y = length(mul(UNITY_MATRIX_M, float4(0.0, 1.0, 0.0, 0.0)));
                o.ratio = scale.x / scale.y;

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float size = 0.01;
                float2 charSize = float2(size * MAP_SIZE_F);
                charSize.y *= i.ratio;
                float spaceSize = size * (MAP_SIZE_F.x + 1.0);

                float text = 0;

                // Frame time.
                float2 charPos = float2(0.05, 0.75);
                text += drawChar(CH_F, charPos, charSize, i.uv); charPos.x += spaceSize;
                text += drawChar(CH_R, charPos, charSize, i.uv); charPos.x += spaceSize;
                text += drawChar(CH_A, charPos, charSize, i.uv); charPos.x += spaceSize;
                text += drawChar(CH_M, charPos, charSize, i.uv); charPos.x += spaceSize;
                text += drawChar(CH_E, charPos, charSize, i.uv); charPos.x += spaceSize;
                text += drawChar(CH_COLN, charPos, charSize, i.uv);
                charPos = float2(0.5, 0.75);
                text += drawFixed(unity_DeltaTime.x, 2, charPos, charSize, i.uv);

                // Draw time.
                charPos = float2(0.05, 0.5);
                text += drawChar(CH_D, charPos, charSize, i.uv); charPos.x += spaceSize;
                text += drawChar(CH_R, charPos, charSize, i.uv); charPos.x += spaceSize;
                text += drawChar(CH_A, charPos, charSize, i.uv); charPos.x += spaceSize;
                text += drawChar(CH_W, charPos, charSize, i.uv); charPos.x += spaceSize;
                text += drawChar(CH_COLN, charPos, charSize, i.uv);
                charPos = float2(0.5, 0.5);
                text += drawFixed(unity_DeltaTime.w, 2, charPos, charSize, i.uv);

                // GPU time.
                charPos = float2(0.05, 0.25);
                text += drawChar(CH_G, charPos, charSize, i.uv); charPos.x += spaceSize;
                text += drawChar(CH_P, charPos, charSize, i.uv); charPos.x += spaceSize;
                text += drawChar(CH_U, charPos, charSize, i.uv); charPos.x += spaceSize;
                text += drawChar(CH_COLN, charPos, charSize, i.uv);
                charPos = float2(0.5, 0.25);
                text += drawFixed(unity_DeltaTime.z, 2, charPos, charSize, i.uv);

                return fixed4(text, text, 0, 1);
            }
            ENDCG
        }
    }
}
