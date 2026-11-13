Shader "Custom/KuwaharaURP"
{
    Properties
    {
        _Radius ("Radius (at reference height)", Range(1, 24)) = 8
        _ReferenceHeight ("Reference Height (px)", Float) = 1080
        _Saturation ("Saturation", Range(0, 3)) = 1.5
        _PolyEta ("Poly Eta", Range(0, 1)) = 1.0
        _ToneMapping ("Tone Mapping", Range(0, 1)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "KuwaharaPass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Radius;
                float _ReferenceHeight;
                float _Saturation;
                float _PolyEta;
                float _ToneMapping;
            CBUFFER_END

            // Quality / speed knobs.
            // Texture reads per pixel = 8 (edge detection) + SECTORS * RINGS * ANGLES.
            // With the defaults that is 8 + 48 = 56 reads, whatever the radius is.
            // SECTORS: how many wedges are compared (4 = classic Kuwahara, 8 = smoother
            //          direction changes along edges).
            // RINGS:   samples along each direction (raise for smoother large radii).
            // ANGLES:  directions inside each wedge (raise for less grain).
            #define SECTORS 4
            #define RINGS 4
            #define ANGLES 1

            static const float SHARPNESS = 1.0;
            static const float SECTOR_ANGLE = 6.28318530718 / (float)SECTORS;

            float4 SampleInput(float2 p)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, saturate(p), 0);
            }

            float Luminance3(float3 c)
            {
                return dot(c, float3(0.2126, 0.7152, 0.0722));
            }

            float3 AcesToneMap(float3 x)
            {
                float3 a = x * (x * 2.51 + 0.03);
                float3 b = x * (x * 2.43 + 0.59) + 0.14;
                return saturate(a / b);
            }

            float4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float2 uv = i.texcoord;
                float2 texel = _BlitTexture_TexelSize.xy;
                float4 inputColor = SampleInput(uv);

                // Radius scales with the screen height, so the strokes cover the same
                // fraction of the image at any resolution. _Radius is the value at
                // _ReferenceHeight (1080 by default); _BlitTexture_TexelSize.w is the height in pixels.
                float rad = max(_Radius * (_BlitTexture_TexelSize.w / max(_ReferenceHeight, 1.0)), 1.0);
                float ringStep = rad / (float)RINGS; // distance between rings, in pixels

                // ---- Structure tensor from 3x3 Sobel on luminance (center tap has weight 0) ----
                float jxx = 0.0;
                float jyy = 0.0;
                float jxy = 0.0;
                [unroll]
                for (int sy = -1; sy <= 1; sy++)
                {
                    [unroll]
                    for (int sx = -1; sx <= 1; sx++)
                    {
                        if (sx == 0 && sy == 0) continue;
                        float lum = Luminance3(SampleInput(uv + float2(sx, sy) * texel).rgb);
                        float gx_w = sx * (sy == 0 ? 2.0 : 1.0);
                        float gy_w = sy * (sx == 0 ? 2.0 : 1.0);
                        jxx += gx_w * gx_w * lum;
                        jyy += gy_w * gy_w * lum;
                        jxy += gx_w * gy_w * lum;
                    }
                }

                // ---- Eigenvalues and dominant eigenvector ----
                float tr = jxx + jyy;
                float detVal = jxx * jyy - jxy * jxy;
                float disc = sqrt(max(tr * tr * 0.25 - detVal, 0.0));
                float lambda1 = tr * 0.5 + disc;
                float lambda2 = tr * 0.5 - disc;
                float aniso = (lambda1 - lambda2) / (lambda1 + lambda2 + 1e-7);
                float2 evec = float2(1.0, 0.0);
                if (abs(jxy) > 1e-7)
                {
                    evec = normalize(float2(lambda1 - jyy, jxy));
                }

                float scaleX = SHARPNESS / (aniso + SHARPNESS);
                float scaleY = (aniso + SHARPNESS) / SHARPNESS;
                float cosA = evec.x;
                float sinA = evec.y;

                // ---- Ring weights ----
                // The weight only depends on the ring, so it is the same for every
                // sector. That means the weight total is the same for every sector too,
                // and we can compute everything about weights once.
                float wRing[RINGS];
                float wTotal = 0.0;
                [unroll]
                for (int k = 0; k < RINGS; k++)
                {
                    float normX = ((float)(k + 1) * ringStep) / (rad + 1.0);
                    float wArg = normX + _PolyEta;
                    wRing[k] = wArg * wArg;
                    wTotal += wRing[k] * (float)ANGLES;
                }
                float invW = 1.0 / wTotal;

                // ---- Pick the sector with the lowest variance ----
                // Every loop below is fully unrolled, so the GPU can issue all texture
                // reads up front and cos/sin of the (constant) angles are folded away.
                float3 bestColor = 0.0;
                float bestVar = 1e10;
                [unroll]
                for (int s = 0; s < SECTORS; s++)
                {
                    float3 sumCol = 0.0;
                    float3 sumCol2 = 0.0;

                    [unroll]
                    for (int j = 0; j < ANGLES; j++)
                    {
                        // Where this direction sits inside the sector (0..1)
                        float f = 0.375 + ((float)j - 0.5 * (float)(ANGLES - 1)) / (float)ANGLES;
                        float angle = ((float)s + f) * SECTOR_ANGLE;

                        // Direction after anisotropy + rotation, as a per-ring uv step
                        float sx2 = cos(angle) * scaleX;
                        float sy2 = sin(angle) * scaleY;
                        float2 stepUv = float2(cosA * sx2 - sinA * sy2, sinA * sx2 + cosA * sy2) * texel * ringStep;

                        [unroll]
                        for (int r = 0; r < RINGS; r++)
                        {
                            float3 sc = SampleInput(uv + stepUv * (float)(r + 1)).rgb;
                            sumCol += sc * wRing[r];
                            sumCol2 += sc * sc * wRing[r];
                        }
                    }

                    float3 meanCol = sumCol * invW;
                    float3 meanCol2 = sumCol2 * invW;
                    float variance = dot(meanCol2 - meanCol * meanCol, 1.0);
                    bool better = variance < bestVar;
                    bestVar = better ? variance : bestVar;
                    bestColor = better ? meanCol : bestColor;
                }

                // ---- Saturation, then ACES tone mapping ----
                float3 col = bestColor;
                float gray = Luminance3(col);
                col = lerp(gray.xxx, col, _Saturation);
                col = lerp(col, AcesToneMap(col), _ToneMapping);

                return float4(col, inputColor.a);
            }
            ENDHLSL
        }
    }
}
