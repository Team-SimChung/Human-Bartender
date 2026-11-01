Shader "Custom/Outside/LightShaft"
{
    Properties
    {
        _Color ("Shaft color (intensity premultiplied)", Color) = (1, 0.7, 0.35, 1)
        // x 길이 감쇠 지수, y 가장자리 부드러움, z 광선 줄무늬 강도, w 줄무늬 개수
        _Shape ("Shape", Vector) = (1.6, 0.6, 0.35, 5)
        // x 줄무늬 흔들림 속도, y 먼지 양, z 먼지 낙하 속도, w 픽셀/유닛
        _Motion ("Motion", Vector) = (0.4, 0.35, 0.6, 100)
        // x 램프 쪽 시작 페이드 길이, y 깜빡임, z 밝기 단계(0이면 부드럽게)
        _Fade ("Fade", Vector) = (0.08, 0.04, 0, 0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _Shape, _Motion, _Fade;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 world : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.world = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.world);
                o.uv = input.uv;
                return o;
            }
            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // uv.x: 원뿔 가로(0~1), uv.y: 램프(0)에서 바닥 쪽(1)까지
                float across = abs(input.uv.x - 0.5) * 2;
                float along = input.uv.y;
                float edge = (1 - smoothstep(1 - _Shape.y, 1, across)) * lerp(1, 1 - across * across, 0.5);
                float body = pow(saturate(1 - along), _Shape.x) * smoothstep(0, max(0.001, _Fade.x), along);
                // 빗속에서 빛이 일렁이도록 가로 방향 줄무늬가 천천히 흐른다.
                float t = _Time.y * _Motion.x;
                float rays = 0.5 + 0.5 * sin(input.uv.x * _Shape.w * 6.2832 + t + sin(input.uv.x * 13.1 - t * 0.7) * 1.3);
                float glow = edge * body * lerp(1, rays, _Shape.z);
                glow *= 1 - _Fade.y * (0.5 + 0.5 * sin(_Time.y * 23) * sin(_Time.y * 7.3));
                if (_Fade.z > 0.5) glow = floor(glow * _Fade.z + 0.5) / _Fade.z;
                // 빛 속에서만 보이는 먼지/빗방울: 픽셀 격자 단위로 떨어지는 점.
                float2 cell = floor(input.world.xy * _Motion.w + float2(0, _Time.y * _Motion.z * _Motion.w));
                float dust = step(1 - _Motion.y * 0.03, Hash(cell)) * edge * sqrt(body);
                return half4(_Color.rgb * (glow + dust * 1.5), 1);
            }
            ENDHLSL
        }
    }
}
