Shader "Custom/GroundFieldPattern"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.0, 0.0, 0.0, 1.0)
        _HighlightEnabled ("Highlight Enabled", Float) = 0
        _HighlightStartTime ("Highlight Start Time", Float) = 0
        _HighlightDirection ("Highlight Direction", Float) = 1
        _N ("n", Float) = 3
        _M ("m", Float) = 2
        _A ("A", Float) = 1
        _B ("B", Float) = 1
        _RangeMin ("Range Min", Float) = -0.3
        _RangeMax ("Range Max", Float) = 0.3
        _NodeLineWidth ("Node Line Width", Range(0.001, 0.25)) = 0.03
        _EdgeFadeWidth ("Edge Fade Width", Range(0.001, 0.5)) = 0.1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "GroundFieldPattern"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _HighlightEnabled;
                float _HighlightStartTime;
                float _HighlightDirection;
                float _N;
                float _M;
                float _A;
                float _B;
                float _RangeMin;
                float _RangeMax;
                float _NodeLineWidth;
                float _EdgeFadeWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float Chladni(float2 coordinates)
            {
                const float pi = 3.14159265359;

                return _A * cos(_N * pi * coordinates.x)
                    * cos(_M * pi * coordinates.y)
                    + _B * cos(_M * pi * coordinates.x)
                    * cos(_N * pi * coordinates.y);
            }

            float NodeMask(float functionValue)
            {
                return 1.0 - smoothstep(
                    _NodeLineWidth,
                    _NodeLineWidth + 0.02,
                    abs(functionValue));
            }

            bool IsInRange(float value, float rangeMin, float rangeMax)
            {
                if (rangeMin <= rangeMax)
                {
                    return value >= rangeMin && value <= rangeMax;
                }

                return value <= rangeMax || value >= rangeMin;
            }

            half4 PatternColor(float edgeFade)
            {
                return half4(_BaseColor.rgb, _BaseColor.a * edgeFade);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                clip(input.normalWS.y - 0.5);

                float2 coordinates = saturate(input.positionOS.xz + 0.5);
                float functionValue = Chladni(coordinates);
                float nodeMask = NodeMask(functionValue);
                float edgeDistance = min(
                    min(coordinates.x, 1.0 - coordinates.x),
                    min(coordinates.y, 1.0 - coordinates.y));
                float edgeFade = smoothstep(
                    0.0,
                    _EdgeFadeWidth,
                    edgeDistance);

                if (_HighlightEnabled > 0.5)
                {
                    float elapsed = saturate(
                        (_Time.y - _HighlightStartTime) / 0.3);
                    float animationProgress = _HighlightDirection > 0.0
                        ? elapsed
                        : 1.0 - elapsed;
                    float nodeRange = _NodeLineWidth + 0.02;
                    float animatedRangeMin = lerp(
                        -nodeRange,
                        _RangeMin,
                        animationProgress);
                    float animatedRangeMax = lerp(
                        nodeRange,
                        _RangeMax,
                        animationProgress);

                    if (!(_HighlightDirection < 0.0
                        && animationProgress <= 0.0))
                    {
                        bool isActiveArea = IsInRange(
                            functionValue,
                            animatedRangeMin,
                            animatedRangeMax);
                        clip(isActiveArea ? 1.0 : -1.0);
                        return PatternColor(edgeFade);
                    }
                }

                clip(nodeMask - 0.01);
                return PatternColor(edgeFade);
            }
            ENDHLSL
        }
    }
}
