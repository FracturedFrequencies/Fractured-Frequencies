// =====================================================================
//  Watercolor / Watercolor Lit   (URP)
//
//  Same watercolor layers as "Watercolor Unlit", plus a painterly lighting model
//  aimed at soft, back-lit, hazy scenes:
//
//    1. Gradient x Texture   - albedo
//    2. Main light           - wrapped, softly stepped diffuse; shadows are a tinted
//                              glaze (Shadow Tint), never black
//    3. Received shadows     - from the URP main light shadow map, same tint
//    4. Wash / Edge pooling / Paper   - pigment density, as in the unlit version
//    5. Back-light rim       - glowing silhouettes when the light is behind objects
//    6. Atmosphere           - depth fade + height fog; both glow towards the sun
//                              (light scattering), so far objects dissolve into light
//
//  Also casts shadows (ShadowCaster pass) and writes depth (DepthOnly pass).
//  God rays and bloom are screen-space effects and are NOT part of this material.
// =====================================================================
Shader "Watercolor/Watercolor Lit"
{
    Properties
    {
        [Header(Texture)]
        [MainTexture] _MainTex ("Base Map", 2D) = "white" {}
        _TexStrength ("Texture Strength", Range(0, 1)) = 1
        [Enum(Multiply, 0, Overlay, 1)] _TexBlendMode ("Blend Mode", Float) = 0

        [Header(Gradient Color)]
        _TopColor    ("Top Color", Color) = (0.95, 0.80, 0.70, 1)
        _BottomColor ("Bottom Color", Color) = (0.65, 0.40, 0.40, 1)
        [KeywordEnum(Local, World)] _GRADSPACE ("Gradient Space", Float) = 0
        _GradMin ("Gradient Bottom Y", Float) = -0.5
        _GradMax ("Gradient Top Y", Float) = 0.5
        _GradPower ("Gradient Curve", Range(0.2, 5)) = 1

        [Header(Lighting)]
        _ShadowColor ("Shadow Tint", Color) = (0.62, 0.48, 0.66, 1)
        _ShadowThreshold ("Light Shadow Threshold", Range(0, 1)) = 0.45
        _ShadowSoftness ("Terminator Softness", Range(0.001, 0.5)) = 0.2
        _LightWrap ("Light Wrap", Range(0, 1)) = 0.3
        _LightColorInfluence ("Light Color Influence", Range(0, 1)) = 1
        _ReceiveShadows ("Received Shadow Strength", Range(0, 1)) = 1
        _ShadowDensity ("Shadow Pigment Density", Range(0, 1)) = 0.3

        [Header(Back Light Rim)]
        _RimColor ("Rim Color", Color) = (1.0, 0.86, 0.70, 1)
        _RimStrength ("Rim Strength", Range(0, 3)) = 1
        _RimPower ("Rim Width (lower = wider)", Range(0.5, 10)) = 4
        _RimBacklightOnly ("Rim Only When Back-Lit", Range(0, 1)) = 1

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
        [Toggle] _OutlineOn ("Outline Enabled", Float) = 0
        _OutlineColor ("Outline Color", Color) = (0.35, 0.22, 0.28, 1)
        _OutlineWidth ("Outline Width (pixels)", Range(0, 10)) = 2
        _OutlineWobble ("Outline Width Wobble", Range(0, 1)) = 0.5
        [Toggle] _OutlineUsePosDir ("Hard Edge Fix (for cube-like meshes)", Float) = 0

        [Header(Atmosphere Light Scattering)]
        _ScatterStrength ("Haze Glow Strength", Range(0, 3)) = 1
        _ScatterPower ("Haze Glow Focus (higher = tighter)", Range(1, 64)) = 6

        [Header(Depth Fade)]
        [KeywordEnum(Manual, CameraDepth, WorldHeight)] _FADEMODE ("Fade Mode", Float) = 0
        _FadeColor ("Fade Color", Color) = (0.98, 0.90, 0.85, 1)
        _FadeAmount ("Manual Fade (minimum, 0 = none)", Range(0, 1)) = 0
        _FadeMax ("Max Fade", Range(0, 1)) = 0.85
        [Space(4)]
        _FadeNear ("Camera Depth: Fade Start", Float) = 5
        _FadeFar  ("Camera Depth: Fade End", Float) = 40
        [Space(4)]
        _FadeHeightTop    ("World Height: No Fade Above Y", Float) = 0
        _FadeHeightBottom ("World Height: Max Fade Below Y", Float) = -10

        [Header(Height Fog)]
        [Toggle(_HEIGHT_FOG)] _HeightFog ("Height Fog Enabled", Float) = 0
        _HeightFogColor ("Height Fog Color", Color) = (0.93, 0.55, 0.60, 1)
        _HeightFogTop ("No Fog Above Y", Float) = 2
        _HeightFogBottom ("Max Fog Below Y", Float) = -1
        _HeightFogMax ("Max Height Fog", Range(0, 1)) = 0.8

        [Header(Debug)]
        [Toggle(_DEBUG_FADE)] _DebugFade ("Show Atmosphere Amount (black = none, white = max)", Float) = 0

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
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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

            half4  _ShadowColor;
            half   _ShadowThreshold;
            half   _ShadowSoftness;
            half   _LightWrap;
            half   _LightColorInfluence;
            half   _ReceiveShadows;
            half   _ShadowDensity;

            half4  _RimColor;
            half   _RimStrength;
            half   _RimPower;
            half   _RimBacklightOnly;

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

            half   _ScatterStrength;
            half   _ScatterPower;

            half4  _FadeColor;
            half   _FadeAmount;
            half   _FadeMax;
            float  _FadeNear;
            float  _FadeFar;
            float  _FadeHeightTop;
            float  _FadeHeightBottom;

            half4  _HeightFogColor;
            float  _HeightFogTop;
            float  _HeightFogBottom;
            half   _HeightFogMax;

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
            return v / 0.9375;
        }

        // ---------- Pigment density ----------
        // d > 0: more pigment (darker, same hue); d < 0: lighter
        half3 ApplyDensity(half3 c, half d)
        {
            return saturate(c - (c - c * c) * d);
        }

        // ---------- Paper (screen pixels) ----------
        half PaperHeight(float2 pixelPos)
        {
            float2 p = pixelPos / _PaperScale;
            half h  = ValueNoise(p / 2.5) * 0.45;
            h      += ValueNoise(p / 7.0) * 0.35;
            h      += ValueNoise(p / float2(30.0, 9.0)) * 0.20;
            return h;
        }

        half3 ApplyPaper(half3 col, float2 pixelPos, half extraDensity)
        {
            half paper = PaperHeight(pixelPos);
            col = ApplyDensity(col, extraDensity + (0.5 - paper) * _Granulation);
            col *= 1.0 + (paper - 0.5) * _PaperBrightness * 2.0;
            return col;
        }

        // ---------- Atmosphere ----------
        // Fog color brightened towards the main light: looking into the sun,
        // the haze glows with the light color (cheap single-scattering approximation).
        // Note: with an orthographic camera all view rays are parallel,
        // so the glow is uniform across the screen instead of a sun spot.
        half3 HazeColor(half3 baseFog, float3 posWS)
        {
            Light mainLight = GetMainLight();
            float3 rayDir = -GetWorldSpaceNormalizeViewDir(posWS); // camera -> pixel
            half s = pow(saturate(dot(rayDir, mainLight.direction)), _ScatterPower) * _ScatterStrength;
            return baseFog + mainLight.color * s;
        }

        half ComputeDepthFade(float viewDepth, float worldY)
        {
            half f = 0;
        #if defined(_FADEMODE_CAMERADEPTH)
            f = saturate((viewDepth - _FadeNear) / max(_FadeFar - _FadeNear, 1e-4)) * _FadeMax;
        #elif defined(_FADEMODE_WORLDHEIGHT)
            f = saturate((_FadeHeightTop - worldY) / max(_FadeHeightTop - _FadeHeightBottom, 1e-4)) * _FadeMax;
        #endif
            return max(f, _FadeAmount);
        }

        half3 ApplyAtmosphere(half3 col, float viewDepth, float3 posWS)
        {
            half depthFade = ComputeDepthFade(viewDepth, posWS.y);
            col = lerp(col, HazeColor(_FadeColor.rgb, posWS), depthFade);

            half heightFog = 0;
        #if defined(_HEIGHT_FOG)
            heightFog = saturate((_HeightFogTop - posWS.y) / max(_HeightFogTop - _HeightFogBottom, 1e-4)) * _HeightFogMax;
            col = lerp(col, HazeColor(_HeightFogColor.rgb, posWS), heightFog);
        #endif

        #if defined(_DEBUG_FADE)
            col = (1.0 - (1.0 - depthFade) * (1.0 - heightFog)).xxx;
        #endif
            return col;
        }
        ENDHLSL

        // ================= Pass 1: Body (lit) =================
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
            #pragma shader_feature_local _HEIGHT_FOG
            #pragma shader_feature_local _DEBUG_FADE

            // URP main light shadows
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
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
                o.gradY = v.positionOS.y;
            #endif
                return o;
            }

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

                // ---- 1. Albedo: gradient x texture ----
                half t = saturate((i.gradY - _GradMin) / max(_GradMax - _GradMin, 1e-4));
                t = pow(t, _GradPower);
                half3 grad   = lerp(_BottomColor.rgb, _TopColor.rgb, t);
                half3 mixed  = lerp(grad * tex.rgb, Overlay(grad, tex.rgb), _TexBlendMode);
                half3 albedo = lerp(grad, mixed, _TexStrength) * i.color.rgb;

                float3 N = normalize(i.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);

                // ---- 2. Main light ----
            #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(i.positionWS));
            #else
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
            #endif
                Light mainLight = GetMainLight(shadowCoord);

                // Wrapped diffuse, then a soft step: few broad value zones like a painting
                half NdotL   = dot(N, mainLight.direction);
                half wrapped = (NdotL + _LightWrap) / (1.0 + _LightWrap);
                half lightAmt = smoothstep(_ShadowThreshold - _ShadowSoftness,
                                           _ShadowThreshold + _ShadowSoftness, wrapped);

                // ---- 3. Received shadows ----
                half shadowAtten = lerp(1.0, mainLight.shadowAttenuation, _ReceiveShadows);
                lightAmt *= shadowAtten;

                // Lit side takes the light color, shadow side is a tinted glaze (never black)
                half3 lightCol  = lerp(half3(1, 1, 1), mainLight.color, _LightColorInfluence);
                half3 litCol    = albedo * lightCol;
                half3 shadowCol = albedo * _ShadowColor.rgb;
                half3 col = lerp(shadowCol, litCol, lightAmt);

                // ---- 4. Watercolor pigment density ----
                float2 washUV = (i.positionWS.xy + i.positionWS.zz * 0.5) * _WashScale * 0.2;
                half density = (FBM(washUV) - 0.5) * 2.0 * _WashStrength;

                half edge = pow(1.0 - saturate(dot(N, V)), _EdgeWidth);
                edge *= 1.0 - _EdgeNoise * ValueNoise(i.positionCS.xy / 12.0);
                density += edge * _EdgePool;

                // Shadow areas read as an extra layer of glaze
                density += (1.0 - lightAmt) * _ShadowDensity;

                col = ApplyPaper(col, i.positionCS.xy, density);

                // ---- 5. Back-light rim ----
                // Strongest when the light is behind the object (camera looks towards the light)
                half rim  = pow(1.0 - saturate(dot(N, V)), _RimPower);
                half back = saturate(dot(-V, mainLight.direction));
                rim *= lerp(1.0, back, _RimBacklightOnly);
                col += _RimColor.rgb * mainLight.color * rim * _RimStrength;

                // ---- 6. Atmosphere ----
                col = ApplyAtmosphere(col, i.viewDepth, i.positionWS);

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
            #pragma shader_feature_local _HEIGHT_FOG
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
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float3 dirOS = lerp(v.normalOS, normalize(v.positionOS.xyz), _OutlineUsePosDir);

                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float4 posCS = TransformWorldToHClip(posWS);
                float3 dirWS = TransformObjectToWorldNormal(dirOS);
                float2 dirCS = mul((float3x3)UNITY_MATRIX_VP, dirWS).xy;

                half wobble = lerp(1.0, ValueNoise(posWS.xy * 3.0 + posWS.zz) * 1.6, _OutlineWobble);
                float width = _OutlineWidth * _OutlineOn * wobble;

                float2 offset = normalize(dirCS + 1e-5) * width * 2.0 / _ScreenParams.xy;
                posCS.xy += offset * posCS.w;

                o.positionCS = posCS;
                o.viewDepth  = -TransformWorldToView(posWS).z;
                o.positionWS = posWS;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                clip(_OutlineOn - 0.5);

                half3 col = ApplyPaper(_OutlineColor.rgb, i.positionCS.xy, 0);
                col = ApplyAtmosphere(col, i.viewDepth, i.positionWS);
                return half4(col, 1);
            }
            ENDHLSL
        }

        // ================= Pass 3: Shadow caster =================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            // Set by URP while rendering the shadow map
            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings ShadowVert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);

                float3 posWS    = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);

            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDir = normalize(_LightPosition - posWS);
            #else
                float3 lightDir = _LightDirection;
            #endif

                float4 posCS = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, lightDir));
            #if UNITY_REVERSED_Z
                posCS.z = min(posCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                posCS.z = max(posCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif

                o.positionCS = posCS;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 ShadowFrag(Varyings i) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        // ================= Pass 4: Depth only =================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings DepthVert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 DepthFrag(Varyings i) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
