// SEWAGE 등급 연출 셰이더 (URP 2D Renderer, 파티클 전용, 도트 격자).
// Zone : 글자 주변 타원 영역. 가장자리는 3단계 계단으로 끊기고 영역 밖은 건드리지 않는다.
//        혼합을 곱하기(Src DstColor, Dst Zero)로 두면 영역 안 화면을 빨갛게 물들이고,
//        미리 곱한 알파(Src One, Dst OneMinusSrcAlpha)로 두면 영역 안을 진한 자주색으로 덮는다.
// Hex  : 영역 안에 빨간 네온 육각 패널 선. 가운데일수록 밝고 월드 도트 격자에 고정된다.
// Shard: 쿼드 위에서 아래로 지그재그로 꺾이는 검은 찢김 한 줄 (파티클 회전으로 비스듬히).
// 파티클 정점 색이 색에 곱해지고, 정점 알파가 세기에 곱해진다.
Shader "Custom/VFX/SH_GradeSewage"
{
    Properties
    {
        [Enum(Zone,0,Hex,1,Shard,2)] _Mode ("모양 (Zone 글자 주변 영역, Hex 육각 패널, Shard 검은 찢김)", Float) = 0
        _Color ("색 (일반 색, HDR 아님: 고른 색 그대로 보인다)", Color) = (0.33, 0.14, 0.24, 1)
        _Strength ("세기", Range(0, 2)) = 1
        _StepRadii ("영역 계단 반지름 (안쪽, 가운데, 바깥 / 쿼드 대비)", Vector) = (0.62, 0.82, 1, 0)
        _StepLevels ("계단별 세기 (안쪽, 가운데, 바깥)", Vector) = (1, 0.6, 0.28, 0)

        [Header(Hex)]
        _HexSize ("육각 크기 (도트)", Range(4, 40)) = 11
        _HexLine ("육각 선 두께 (도트, 0.6이면 1도트)", Range(0.5, 4)) = 0.6

        [Header(Shard)]
        _ShardWidth ("찢김 두께 (도트)", Range(1, 12)) = 4
        _ShardJag ("찢김 꺾임 폭 (쿼드 너비 대비)", Range(0, 0.5)) = 0.18
        _Seed ("꺾임 모양 씨앗", Float) = 0

        [Header(Pixel Art)]
        _PixelSize ("도트 크기 (유닛, PPU 100이면 0.01)", Float) = 0.01
        [ToggleUI] _PixelSnap ("도트 격자에 맞추기", Float) = 1

        [Header(Blend)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("혼합 Src (곱하기는 DstColor, 덮기는 One)", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("혼합 Dst (곱하기는 Zero, 덮기는 OneMinusSrcAlpha)", Float) = 10
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
            Name "GradeSewage"
            Tags { "LightMode" = "Universal2D" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _StepRadii;
                float4 _StepLevels;
                float _Mode;
                float _Strength;
                float _HexSize;
                float _HexLine;
                float _ShardWidth;
                float _ShardJag;
                float _Seed;
                float _PixelSize;
                float _PixelSnap;
                float _SrcBlend;
                float _DstBlend;
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
                float2 positionWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                if (_PixelSnap > 0.5)
                {
                    float px = max(_PixelSize, 0.0001);
                    positionWS.xy = round(positionWS.xy / px) * px;
                }
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS.xy;
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            // 타원 영역의 계단식 세기: 안쪽 / 가운데 / 바깥 / 밖(0).
            float ZoneLevel(float2 q)
            {
                float d = length(q);
                if (d < _StepRadii.x) return _StepLevels.x;
                if (d < _StepRadii.y) return _StepLevels.y;
                if (d < _StepRadii.z) return _StepLevels.z;
                return 0.0;
            }

            // 육각 칸 가장자리까지의 거리 (육각 크기 단위, 가장자리 0 / 가운데 0.5).
            float HexEdge(float2 p)
            {
                const float2 r = float2(1.0, 1.7320508);
                const float2 h = r * 0.5;
                float2 a = p - r * floor(p / r) - h;
                float2 b = (p - h) - r * floor((p - h) / r) - h;
                float2 gv = dot(a, a) < dot(b, b) ? a : b;
                gv = abs(gv);
                float c = max(dot(gv, normalize(r)), gv.x);
                return 0.5 - c;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float px = max(_PixelSize, 0.0001);
                float2 uv = input.uv;
                float2 ws = input.positionWS;

                // 월드 1유닛당 UV 변화량 (도트 칸 중심으로 옮기고, 찢김 두께를 도트로 재기 위해).
                float2 dUVdx = ddx(uv);
                float2 dUVdy = ddy(uv);
                float2 dWdx = ddx(ws);
                float2 dWdy = ddy(ws);
                float det = dWdx.x * dWdy.y - dWdy.x * dWdx.y;
                float2 dUVdWx = float2(0.0, 0.0);
                float2 dUVdWy = float2(0.0, 0.0);
                if (abs(det) > 1e-12)
                {
                    float2 sx = float2(dWdy.y, -dWdx.y) / det;
                    float2 sy = float2(-dWdy.x, dWdx.x) / det;
                    dUVdWx = dUVdx * sx.x + dUVdy * sx.y;
                    dUVdWy = dUVdx * sy.x + dUVdy * sy.y;
                    if (_PixelSnap > 0.5)
                    {
                        float2 d = (floor(ws / px) + 0.5) * px - ws;
                        uv += dUVdWx * d.x + dUVdWy * d.y;
                        ws += d;
                    }
                }

                float2 q = (uv - 0.5) * 2.0;
                float inside = (abs(q.x) <= 1.0 && abs(q.y) <= 1.0) ? 1.0 : 0.0;
                int mode = (int)round(_Mode);
                half3 tint = _Color.rgb * input.color.rgb;

                if (mode == 0)
                {
                    float level = saturate(ZoneLevel(q) * inside * _Strength * input.color.a);
                    // Dst가 Zero면 곱하기: 흰색(변화 없음)에서 색으로. 아니면 미리 곱한 알파로 덮기.
                    if (_DstBlend < 0.5)
                        return half4(lerp(half3(1.0, 1.0, 1.0), tint, level), 1.0);
                    return half4(tint * level, level);
                }

                if (mode == 1)
                {
                    float level = ZoneLevel(q) * inside;
                    float hexSize = max(_HexSize, 1.0);
                    float edgeDots = HexEdge((ws / px) / hexSize) * hexSize;
                    float lineMask = edgeDots < _HexLine ? 1.0 : 0.0;
                    float centre = saturate(1.0 - length(q));
                    float a = saturate(lineMask * level * (0.45 + 0.55 * centre) * _Strength * input.color.a);
                    return half4(tint * a, a);
                }

                // Shard: 위에서 아래로 6마디 지그재그. 두께는 도트 단위, 양끝으로 가늘어진다.
                float seg = uv.y * 6.0;
                float i0 = floor(seg);
                float f = seg - i0;
                float x0 = (Hash11(i0 + _Seed * 13.1) - 0.5) * 2.0 * _ShardJag;
                float x1 = (Hash11(i0 + 1.0 + _Seed * 13.1) - 0.5) * 2.0 * _ShardJag;
                float centreU = 0.5 + lerp(x0, x1, f) * 0.5;
                float uPerWorld = max(length(float2(dUVdWx.x, dUVdWy.x)), 1e-5);
                float halfW = 0.5 * _ShardWidth * px * uPerWorld * (0.35 + 0.65 * sin(saturate(uv.y) * 3.14159265));
                float shard = (abs(uv.x - centreU) <= halfW ? 1.0 : 0.0) * inside;
                float sa = saturate(shard * _Strength * input.color.a);
                return half4(tint * sa, sa);
            }
            ENDHLSL
        }
    }
}
