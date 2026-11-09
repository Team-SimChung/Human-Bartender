// 등급 연출 뒷빛 셰이더 (URP 2D Renderer, 파티클 전용, 부드러운 빛).
// SH_GradeReveal(도트 셰이더)과 달리 도트 격자·밝기 단계를 쓰지 않고, 외곽선 없이 매끈하게 퍼지는 빛을 그린다.
// 모양: SoftGlow = 둥근(쿼드 비율대로 타원) 블룸 / LightBar = 글자 뒤에 까는 가로로 긴 빛줄기 (양끝이 길게 사라지고 가운데에 가는 심).
// 쿼드 가장자리에서 밝기가 매끈하게 0이 되도록 만들어 쿼드 경계나 계단이 보이지 않는다.
// 파티클의 Start Color / Color over Lifetime(정점 색)이 색에 곱해지고, 정점 알파는 마지막에 밝기에만 곱해진다 (사라질 때 색이 변하지 않음).
// 혼합은 미리 곱한 알파(One, OneMinusSrcAlpha)다. _BlendAlpha 0이면 가산(빛)으로 그린다.
Shader "Custom/VFX/SH_GradeRevealGlow"
{
    Properties
    {
        [Enum(SoftGlow,0,LightBar,1)] _Shape ("모양 (SoftGlow 둥근 블룸, LightBar 가로 빛줄기)", Float) = 1
        [HDR] _Color ("빛 색", Color) = (1, 0.78, 0.4, 1)
        [HDR] _CoreColor ("가운데 밝은 부분 색", Color) = (1, 0.95, 0.82, 1)
        _CorePoint ("이 밝기부터 가운데 색으로 바뀜 (1이면 끔)", Range(0, 1)) = 0.6
        _Intensity ("세기", Range(0, 8)) = 1
        _BlendAlpha ("덮는 정도 (0 = 가산 빛, 1 = 보통 알파)", Range(0, 1)) = 0

        [Header(Shape)]
        _Falloff ("퍼짐 (빛줄기는 위아래, 블룸은 바깥쪽 / 클수록 가운데에 모임)", Range(0.5, 12)) = 3
        _EndFade ("빛줄기 양끝이 사라지는 길이 (쿼드 길이 대비 0~1)", Range(0.05, 1)) = 0.5
        _CoreWidth ("가운데 심 두께 (쿼드 높이 대비, 0이면 심 없음)", Range(0, 0.5)) = 0.08
        _CoreBoost ("가운데 심 밝기", Range(0, 4)) = 0.6
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
            Name "GradeRevealGlow"
            Tags { "LightMode" = "Universal2D" }

            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _CoreColor;
                float _Shape;
                float _CorePoint;
                float _Intensity;
                float _BlendAlpha;
                float _Falloff;
                float _EndFade;
                float _CoreWidth;
                float _CoreBoost;
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
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            // 가로 빛줄기: 가운데 구간은 고르게 밝고 양끝 _EndFade 구간에서 매끈하게 사라진다.
            // 위아래는 (1 - y²)^_Falloff 로 가운데가 둥글게 밝고 쿼드 위아래 끝에서 0이 된다 (가운데 접힌 선 없음).
            float LightBar(float2 q)
            {
                float ax = abs(q.x);
                float ends = smoothstep(1.0, 1.0 - _EndFade, ax);
                ends *= 0.8 + 0.2 * (1.0 - ax * ax);
                float body = pow(saturate(1.0 - q.y * q.y), _Falloff);
                float core = 0.0;
                if (_CoreWidth > 0.0)
                    core = exp(-(q.y * q.y) / (_CoreWidth * _CoreWidth)) * _CoreBoost * smoothstep(1.0, 0.25, ax);
                return ends * (body + core);
            }

            // 둥근 블룸: (1 - r²)^_Falloff 로 가운데가 둥글고 쿼드 가장자리에서 0이 된다. 심은 같은 모양을 더 좁게 겹친다.
            float SoftGlow(float2 q)
            {
                float k = saturate(1.0 - dot(q, q));
                return pow(k, _Falloff) + _CoreBoost * pow(k, _Falloff * 4.0);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 q = input.uv * 2.0 - 1.0;
                float s = (_Shape < 0.5) ? SoftGlow(q) : LightBar(q);
                s = max(s, 0.0);

                half3 tint = _Color.rgb * input.color.rgb;
                half3 coreRgb = _CoreColor.rgb * input.color.rgb;
                float core = saturate((s - _CorePoint) / max(1.0 - _CorePoint, 0.001));
                float strength = s * _Intensity * input.color.a;
                half3 rgb = lerp(tint, coreRgb, core) * strength;
                return half4(rgb, saturate(strength) * _BlendAlpha);
            }
            ENDHLSL
        }
    }
}
