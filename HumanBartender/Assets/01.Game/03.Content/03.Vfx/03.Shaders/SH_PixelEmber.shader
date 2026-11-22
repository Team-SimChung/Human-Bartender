// 도트 불씨 셰이더 (URP 2D Renderer, 파티클용, 가산).
// 꼭짓점을 월드 도트 격자(_PixelSize)에 붙여, 크기 1도트(0.01) 파티클은 정확히 1×1 도트, 2도트(0.02)는 2×2 도트로 그려진다.
// 색과 투명도는 파티클의 Start Color / Color over Lifetime(정점 색)을 그대로 쓰고, 초당 _TwinkleSteps 단계로 반짝인다.
Shader "Custom/VFX/SH_PixelEmber"
{
    Properties
    {
        [HDR] _Color ("색 배율", Color) = (1, 1, 1, 1)
        _Intensity ("세기", Range(0, 4)) = 1.4
        _PixelSize ("도트 크기 (유닛, PPU 100이면 0.01)", Float) = 0.01
        _Twinkle ("반짝임 세기 (0이면 끔)", Range(0, 1)) = 0.35
        _TwinkleSteps ("반짝임 바뀌는 횟수 (초당)", Float) = 12
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "PixelEmber"
            Tags { "LightMode" = "Universal2D" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Intensity;
                float _PixelSize;
                float _Twinkle;
                float _TwinkleSteps;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float seed : TEXCOORD0;
            };

            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float px = max(_PixelSize, 0.0001);
                // 각 꼭짓점을 도트 격자에 붙인다. 크기가 도트 배수인 파티클은 항상 같은 수의 도트를 덮는다.
                positionWS.xy = round(positionWS.xy / px) * px;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.color = input.color;
                // 파티클마다 다른 반짝임 박자를 주기 위한 값 (정점 색이 파티클마다 조금씩 다르다)
                output.seed = dot(input.color.rgb, float3(12.9898, 78.233, 37.719));
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float t = _TwinkleSteps > 0.0 ? floor(_Time.y * _TwinkleSteps) : _Time.y * 60.0;
                float twinkle = 1.0 - _Twinkle * Hash12(float2(t, input.seed));
                half3 rgb = input.color.rgb * _Color.rgb * (_Intensity * twinkle * input.color.a);
                return half4(rgb, 0.0);
            }
            ENDHLSL
        }
    }
}
