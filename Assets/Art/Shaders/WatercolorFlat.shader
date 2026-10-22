// =====================================================================
//  Watercolor / Watercolor Unlit   (URP, Unlit, no lighting)
//
//  Layers (applied in this order):
//    1. Gradient x Texture  - vertical color gradient blended with the base map
//    2. Wash                - low-frequency pigment density variation (blotchy)
//    3. Edge Pooling        - pigment collects near the silhouette
//    4. Paper               - screen-space paper grain + granulation
//    5. Depth Fade          - atmospheric fade towards a fog color
//                             (Manual / Camera Depth / World Height)
//  Plus an inverted-hull outline pass with width wobble.
//
//  Works with both perspective and orthographic cameras.
// =====================================================================
Shader "Watercolor/Watercolor Unlit"
{
    Properties
    {
        [Header(Texture)]
        [MainTexture] _MainTex ("Base Map", 2D) = "white" {}
        _TexStrength ("Texture Strength", Range(0, 1)) = 1
        [Enum(Multiply, 0, Overlay, 1)] _TexBlendMode ("Blend Mode", Float) = 0

        [Header(Gradient Color)]
        _TopColor    ("Top Color", Color) = (0.72, 0.80, 0.95, 1)
        _BottomColor ("Bottom Color", Color) = (0.55, 0.60, 0.85, 1)
        [KeywordEnum(Local, World)] _GRADSPACE ("Gradient Space", Float) = 0
        _GradMin ("Gradient Bottom Y", Float) = -0.5
        _GradMax ("Gradient Top Y", Float) = 0.5
        _GradPower ("Gradient Curve", Range(0.2, 5)) = 1

        [Header(Watercolor Wash)]
        _WashStrength ("Wash Strength", Range(0, 1.5)) = 0.6
        _WashScale ("Wash Scale (higher = smaller blotches)", Float) = 3

        [Header(Paper)]
        _Granulation ("Granulation", Range(0, 2)) = 0.8
        _PaperBrightness ("Paper Brightness Variation", Range(0, 0.5)) = 0.12
        _PaperScale ("Paper Grain Size", Range(0.3, 4)) = 1

        [Header(Edge Pooling)]
        _EdgePool ("Edge Pooling Strength", Range(0, 2)) = 0.8
        _EdgeWidth ("Edge Falloff (lower = wider)", Range(0.5, 10)) = 2.5
        _EdgeNoise ("Edge Noise", Range(0, 1)) = 0.5

        [Header(Outline)]
        [Toggle] _OutlineOn ("Outline Enabled", Float) = 1
        _OutlineColor ("Outline Color", Color) = (0.22, 0.18, 0.38, 1)
        _OutlineWidth ("Outline Width (pixels)", Range(0, 10)) = 2
        _OutlineWobble ("Outline Width Wobble", Range(0, 1)) = 0.5
        [Toggle] _OutlineUsePosDir ("Hard Edge Fix (for cube-like meshes)", Float) = 0

        [Header(Depth Fade)]
        [KeywordEnum(Manual, CameraDepth, WorldHeight)] _FADEMODE ("Fade Mode", Float) = 0
        _FadeColor ("Fade Color", Color) = (0.86, 0.89, 0.98, 1)
        _FadeAmount ("Manual Fade (minimum, 0 = none)", Range(0, 1)) = 0
        _FadeMax ("Max Fade", Range(0, 1)) = 0.85
        [Space(4)]
        _FadeNear ("Camera Depth: Fade Start", Float) = 5
        _FadeFar  ("Camera Depth: Fade End", Float) = 40
        [Space(4)]
        _FadeHeightTop    ("World Height: No Fade Above Y", Float) = 0
        _FadeHeightBottom ("World Height: Max Fade Below Y", Float) = -10
        [Space(4)]
        [Toggle(_DEBUG_FADE)] _DebugFade ("Debug: Show Fade (black = none, white = max)", Float) = 0

        [Header(Render)]
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry"
        }

        // ================= Shared code =================
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half   _TexStrength;
            half   _TexBlendMode;

            half4  _TopColor;
            half4  _BottomColor;
            float  _GradMin;
            float  _GradMax;
            half   _GradPower;

            half   _WashStrength;
            float  _WashScale;

            half   _Granulation;
            half   _PaperBrightness;
            half   _PaperScale;

            half   _EdgePool;
            half   _EdgeWidth;
            half   _EdgeNoise;

            half   _OutlineOn;
            half4  _OutlineColor;
            float  _OutlineWidth;
            half   _OutlineWobble;
            half   _OutlineUsePosDir;

            half4  _FadeColor;
            half   _FadeAmount;
            half   _FadeMax;
            float  _FadeNear;
            float  _FadeFar;
            float  _FadeHeightTop;
            float  _FadeHeightBottom;

            half   _Cutoff;
        CBUFFER_END

        // ---------- Noise ----------
        float Hash21(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float ValueNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            float a = Hash21(i);
            float b = Hash21(i + float2(1, 0));
            float c = Hash21(i + float2(0, 1));
            float d = Hash21(i + float2(1, 1));
            return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
        }

        float FBM(float2 p)
        {
            float v = 0.0, amp = 0.5;
            [unroll] for (int k = 0; k < 4; k++)
            {
                v += amp * ValueNoise(p);
                p = p * 2.03 + 17.1;
                amp *= 0.5;
            }
            return v / 0.9375; // normalize to 0..1
        }

        // ---------- Pigment density ----------
        // d > 0: more pigment (darker, same hue)
        // d < 0: pigment pushed away by water (lighter)
        // Affects mid-tones most, which reads more like watercolor than multiplying by grey.
        half3 ApplyDensity(half3 c, half d)
        {
            return saturate(c - (c - c * c) * d);
        }

        // ---------- Paper (in screen pixels, so it stays fixed like a sheet of paper) ----------
        half PaperHeight(float2 pixelPos)
        {
            float2 p = pixelPos / _PaperScale;
            half h  = ValueNoise(p / 2.5) * 0.45;                // fine grain
            h      += ValueNoise(p / 7.0) * 0.35;                // medium grain
            h      += ValueNoise(p / float2(30.0, 9.0)) * 0.20;  // fiber direction
            return h;
        }

        // Granulation (pigment settles in paper valleys) + paper surface brightness
        half3 ApplyPaper(half3 col, float2 pixelPos, half extraDensity)
        {
            half paper = PaperHeight(pixelPos);
            col = ApplyDensity(col, extraDensity + (0.5 - paper) * _Granulation);
            col *= 1.0 + (paper - 0.5) * _PaperBrightness * 2.0;
            return col;
        }

        // ---------- Depth fade ----------
        // viewDepth: distance along the camera's forward axis
        //            (correct for both perspective and orthographic cameras)
        // worldY:    world-space height, for top-down views where "far" means "low"
        half ComputeFade(float viewDepth, float worldY)
        {
            half f = 0;
        #if defined(_FADEMODE_CAMERADEPTH)
            f = saturate((viewDepth - _FadeNear) / max(_FadeFar - _FadeNear, 1e-4)) * _FadeMax;
        #elif defined(_FADEMODE_WORLDHEIGHT)
            f = saturate((_FadeHeightTop - worldY) / max(_FadeHeightTop - _FadeHeightBottom, 1e-4)) * _FadeMax;
        #endif
            return max(f, _FadeAmount);
        }

        half3 ApplyFade(half3 col, half fade)
        {
            col = lerp(col, _FadeColor.rgb, fade);
        #if defined(_DEBUG_FADE)
            col = fade.xxx;
        #endif
            return col;
        }
        ENDHLSL

        // ================= Pass 1: Body =================
        Pass
        {
            Name "Body"
            Tags { "LightMode" = "UniversalForward" }

            Cull   [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   vert
            #pragma fragment frag
            #pragma shader_feature_local _GRADSPACE_LOCAL _GRADSPACE_WORLD
            #pragma shader_feature_local _FADEMODE_MANUAL _FADEMODE_CAMERADEPTH _FADEMODE_WORLDHEIGHT
            #pragma shader_feature_local _DEBUG_FADE
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float  gradY      : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 normalWS   : TEXCOORD3;
                float  viewDepth  : TEXCOORD4;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS   = TransformObjectToWorldNormal(v.normalOS);
                o.viewDepth  = -pos.positionVS.z;
                o.uv         = TRANSFORM_TEX(v.uv, _MainTex);
                o.color      = v.color;

            #if defined(_GRADSPACE_WORLD)
                o.gradY = pos.positionWS.y;
            #else
                o.gradY = v.positionOS.y; // object space: gradient moves with the object
            #endif
                return o;
            }

            // Overlay keeps the texture's value detail while the gradient drives the hue
            half3 Overlay(half3 base, half3 blend)
            {
                half3 lo = 2.0 * base * blend;
                half3 hi = 1.0 - 2.0 * (1.0 - base) * (1.0 - blend);
                return lerp(lo, hi, step(0.5, base));
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                clip(tex.a * i.color.a - _Cutoff);

                // 1. Gradient x texture
                half t = saturate((i.gradY - _GradMin) / max(_GradMax - _GradMin, 1e-4));
                t = pow(t, _GradPower);
                half3 grad  = lerp(_BottomColor.rgb, _TopColor.rgb, t);
                half3 mixed = lerp(grad * tex.rgb, Overlay(grad, tex.rgb), _TexBlendMode);
                half3 col   = lerp(grad, mixed, _TexStrength) * i.color.rgb;

                // 2. Wash: anchored in world space so it doesn't slide when the camera moves
                float2 washUV = (i.positionWS.xy + i.positionWS.zz * 0.5) * _WashScale * 0.2;
                half density = (FBM(washUV) - 0.5) * 2.0 * _WashStrength;

                // 3. Edge pooling: more pigment near the silhouette, broken up by noise
                float3 N = normalize(i.normalWS);
                float3 V = normalize(GetWorldSpaceViewDir(i.positionWS)); // handles ortho too
                half edge = pow(1.0 - saturate(dot(N, V)), _EdgeWidth);
                edge *= 1.0 - _EdgeNoise * ValueNoise(i.positionCS.xy / 12.0);
                density += edge * _EdgePool;

                // 4. Paper grain + granulation (screen space)
                col = ApplyPaper(col, i.positionCS.xy, density);

                // 5. Depth fade
                col = ApplyFade(col, ComputeFade(i.viewDepth, i.positionWS.y));

                return half4(col, 1);
            }
            ENDHLSL
        }

        // ================= Pass 2: Outline (inverted hull) =================
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   vert
            #pragma fragment frag
            #pragma shader_feature_local _FADEMODE_MANUAL _FADEMODE_CAMERADEPTH _FADEMODE_WORLDHEIGHT
            #pragma shader_feature_local _DEBUG_FADE
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float  viewDepth  : TEXCOORD0;
                float  worldY     : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                // Hard-edged meshes (cubes) have split normals at corners, which tears the hull.
                // Extruding along the direction from the pivot closes those gaps.
                float3 dirOS = lerp(v.normalOS, normalize(v.positionOS.xyz), _OutlineUsePosDir);

                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float4 posCS = TransformWorldToHClip(posWS);
                float3 dirWS = TransformObjectToWorldNormal(dirOS);
                float2 dirCS = mul((float3x3)UNITY_MATRIX_VP, dirWS).xy;

                // Width varies along the surface for a hand-drawn look
                half wobble = lerp(1.0, ValueNoise(posWS.xy * 3.0 + posWS.zz) * 1.6, _OutlineWobble);
                float width = _OutlineWidth * _OutlineOn * wobble;

                // Extrude in screen space so the width is in pixels.
                // Multiplying by w keeps it constant for perspective; for ortho w = 1.
                float2 offset = normalize(dirCS + 1e-5) * width * 2.0 / _ScreenParams.xy;
                posCS.xy += offset * posCS.w;

                o.positionCS = posCS;
                o.viewDepth  = -TransformWorldToView(posWS).z;
                o.worldY     = posWS.y;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                clip(_OutlineOn - 0.5);

                // The outline also gets paper grain so it doesn't look like a clean vector line
                half3 col = ApplyPaper(_OutlineColor.rgb, i.positionCS.xy, 0);
                col = ApplyFade(col, ComputeFade(i.viewDepth, i.worldY));
                return half4(col, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}