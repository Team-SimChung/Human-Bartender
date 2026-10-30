// 도트 글로우 셰이더 (URP 2D Renderer, 스프라이트·파티클 공용, 가산).
// 쿼드 중심에서 바깥으로 갈수록 어두워지는 둥근 빛을 부드러운 그라디언트 대신 몇 단계(Bands)의 동심원 도트로 끊어 그린다.
// 원 계산은 월드 도트 격자(_PixelSize) 칸 중심에서 하므로 씬의 도트 크기와 같은 칸으로 계단이 진다.
// 밝기와 반경이 펄린 비슷한 노이즈로 일렁이며, 초당 _FlickerSteps 단계로 끊어 불 애니메이션과 박자를 맞춘다.
// 파티클의 Start Color / Color over Lifetime(정점 색)이 색과 세기에 곱해진다.
Shader "Custom/VFX/SH_PixelGlow"
{
    Properties
    {
        [HDR] _Color ("빛 색", Color) = (1, 0.45, 0.12, 1)
        _Intensity ("세기", Range(0, 4)) = 1
        _Bands ("동심원 단계 수", Range(1, 8)) = 3
        _Falloff ("가장자리로 갈수록 줄어드는 정도 (클수록 중심에 모임)", Range(0.3, 4)) = 1.4
        _PixelSize ("도트 크기 (유닛, PPU 100이면 0.01)", Float) = 0.01

        [Header(Flicker)]
        _FlickerAmount ("밝기 일렁임 (0.3 = ±30%)", Range(0, 1)) = 0.25
        _RadiusFlicker ("반경 일렁임 비율", Range(0, 0.5)) = 0.08
        _FlickerSpeed ("일렁임 속도", Range(0, 20)) = 3
        _FlickerSteps ("값을 바꾸는 횟수 (초당, 0이면 매 프레임)", Float) = 16
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
            Name "PixelGlow"
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
                float _Bands;
                float _Falloff;
                float _PixelSize;
                float _FlickerAmount;
                float _RadiusFlicker;
                float _FlickerSpeed;
                float _FlickerSteps;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            // 매끄러운 1차원 값 노이즈 (-1 ~ 1)
            float Noise1(float t)
            {
                float i = floor(t);
                float f = frac(t);
                float u = f * f * (3.0 - 2.0 * f);
                return lerp(Hash11(i), Hash11(i + 1.0), u) * 2.0 - 1.0;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 이 픽셀이 속한 월드 도트 칸의 중심으로 UV를 옮긴다 (쿼드 회전 없이 놓인다고 본다).
                float px = max(_PixelSize, 0.0001);
                float2 ws = input.positionWS.xy;
                float2 cell = (floor(ws / px) + 0.5) * px;
                float2 dUVdx = ddx(input.uv), dUVdy = ddy(input.uv);
                float2 dWdx = ddx(ws), dWdy = ddy(ws);
                float det = dWdx.x * dWdy.y - dWdy.x * dWdx.y;
                float2 uv = input.uv;
                if (abs(det) > 1e-12)
                {
                    float2 d = cell - ws;
                    float2 s = float2(dWdy.y * d.x - dWdy.x * d.y, -dWdx.y * d.x + dWdx.x * d.y) / det;
                    uv += dUVdx * s.x + dUVdy * s.y;
                }

                float t = _Time.y;
                if (_FlickerSteps > 0.0) t = floor(t * _FlickerSteps) / _FlickerSteps;
                float n = Noise1(t * _FlickerSpeed) * 0.65 + Noise1(t * _FlickerSpeed * 3.1 + 17.0) * 0.35;

                float radius = 1.0 + n * _RadiusFlicker;
                float dist = length(uv - 0.5) * 2.0 / radius;
                float glow = pow(saturate(1.0 - dist), _Falloff);
                float bands = max(round(_Bands), 1.0);
                glow = ceil(glow * bands - 0.001) / bands;
                glow *= saturate(1.0 - dist) > 0.0 ? 1.0 : 0.0;

                float strength = glow * _Intensity * (1.0 + n * _FlickerAmount) * input.color.a;
                half3 rgb = _Color.rgb * input.color.rgb * strength;
                return half4(rgb, 0.0);
            }
            ENDHLSL
        }
    }
}
