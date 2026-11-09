// 시간은 컨트롤러의 _SequenceTime만 사용한다. PPU 100 기준으로 모든 선을 도트 격자에 그린다.
Shader "Custom/VFX/SH_GradeWave"
{
    Properties
    {
        _SequenceTime ("연출 시간 (초, 음수면 숨김)", Float) = -1
        [Enum(Dim,0,Beam,1,Wave,2,Flash,3,Sparks,4)] _Layer ("연출 층 (어둠 / 광선 / 파문 / 섬광 / 파편)", Float) = 1
        [HDR] _Tint ("황금빛 색", Color) = (1, 0.66, 0.22, 1)
        [HDR] _CoreColor ("흰빛 중심 색", Color) = (1, 0.96, 0.79, 1)
        _Intensity ("빛의 세기", Range(0, 5)) = 1
        _PixelSize ("도트 크기 (월드 단위)", Float) = 0.01
        [ToggleUI] _PixelSnap ("도트 격자에 맞추기", Float) = 1
        _Extent ("그리는 영역 크기 (가로, 세로)", Vector) = (9.6, 2, 0, 0)
        _BeamFadeEnd ("빛줄기(Beam)가 완전히 사라지는 시간 (초, 0.3초부터 옅어짐)", Range(0.6, 3)) = 1.9
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent" "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }
        Pass
        {
            Name "GradeWave"
            Tags { "LightMode" = "Universal2D" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float4 _CoreColor;
                float4 _Extent;
                float _SequenceTime;
                float _Layer;
                float _Intensity;
                float _PixelSize;
                float _PixelSnap;
                float _BeamFadeEnd;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                if (_PixelSnap > 0.5)
                    world.xy = round(world.xy / max(_PixelSize, 0.0001)) * max(_PixelSize, 0.0001);
                output.positionCS = TransformWorldToHClip(world);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            float Hash(float n)
            {
                return frac(sin(n * 127.1 + 311.7) * 43758.5453);
            }

            float Window(float t, float start, float rise, float end, float fall)
            {
                return smoothstep(start, start + rise, t) * (1.0 - smoothstep(end, end + fall, t));
            }

            // 두 성분은 황금빛 가장자리와 흰빛 심이다. 가산광의 알파는 항상 0이다.
            float2 Beam(float2 p, float t, float px)
            {
                float launch = smoothstep(0.095, 0.225, t);
                float reach = lerp(0.07, 3.50, launch);
                float along = pow(saturate(1.0 - abs(p.x) / reach), 1.45);
                along = floor(along * 24.0 + 0.5) / 24.0;
                float dy = abs(p.y);
                float core = 1.0 - step(px * 0.51, dy);
                // 번짐의 폭도 양끝으로 갈수록 줄어든다. 중심 10도트만 짧게 반짝인다.
                float taper = 0.24 + 0.76 * sqrt(along);
                float rim = (1.0 - step(0.0151 * taper, dy)) * 0.37;
                rim += (1.0 - step(0.0251 * taper, dy)) * 0.17;
                rim += (1.0 - step(0.0401 * taper, dy)) * 0.065;
                rim += (1.0 - step(0.0551 * taper, dy)) * 0.018;
                // 빛줄기가 다 커진 뒤에는 길이와 테두리 두께를 그대로 두고, 테두리와 심이 함께 천천히 옅어진다.
                // (예전에는 0.27초 뒤 테두리 빛이 급히 꺼지고 심만 남아 줄이 얇고 짧아지는 것처럼 보였다.)
                float peak = smoothstep(0.13, 0.20, t);
                float fadeT = saturate((t - 0.30) / max(_BeamFadeEnd - 0.30, 0.05));
                float fade = (1.0 - fadeT) * (1.0 - fadeT);
                float sustain = Window(t, 0.01, 0.10, 2.48, 0.52);
                float level = sustain * fade;
                float edgeLight = (0.025 + peak * 1.25) * level;
                float gold = along * (rim * edgeLight + core * 0.25 * level);
                float white = along * core * level * (0.42 + peak * 1.10);
                return float2(gold, white);
            }

            float2 Shockwave(float2 p, float t, float px)
            {
                float2 light = float2(0.0, 0.0);
                [unroll]
                for (int j = 0; j < 2; ++j)
                {
                    float i = (float)j;
                    float age = t - (0.185 + i * 0.038);
                    float opening = 1.0 - pow(1.0 - saturate(age / 0.25), 2.7);
                    float rx = lerp(0.055, 0.415 - i * 0.064, opening);
                    float ry = lerp(0.045, 0.310 - i * 0.041, opening);
                    float2 radius = float2(rx, ry);
                    float2 drift = float2(sin(age * 8.0 + i * 2.0), sin(age * 11.0 + i)) * 0.008;
                    float2 q = (p - drift * opening) / radius;
                    float angle = atan2(q.y, q.x);
                    float morph = smoothstep(0.035, 0.135, age);
                    float ellipse = max(length(q), 0.0001);
                    float diamond = abs(q.x) + abs(q.y);
                    // 마름모가 둥근 파면으로 풀리며 빛이 테두리를 따라 흐른다.
                    float wobble = morph * (0.038 * sin(angle * 3.0 + age * 10.0 + i * 1.7)
                                  + 0.019 * sin(angle * 5.0 - age * 16.0 + i));
                    float field = lerp(diamond, ellipse, morph) - 1.0 + wobble;
                    float2 gradient = lerp(sign(q), q / ellipse, morph) / radius;
                    float signedEdge = field / max(length(gradient), 0.001);
                    float rimLine = 1.0 - step(px * 0.65, abs(signedEdge));
                    float inside = floor(saturate(1.0 + signedEdge / 0.038) * 4.0) / 4.0;
                    inside *= 1.0 - step(0.0, signedEdge);
                    float movingArc = sin(angle * 2.0 - age * 17.0 + i * 2.3)
                                    + 0.50 * sin(angle * 5.0 + age * 13.0 + i * 1.4);
                    float broken = smoothstep(-0.16, 0.70, movingArc);
                    float arc = lerp(0.85, 0.035 + broken * 0.965, smoothstep(0.055, 0.19, age));
                    float wispEdge = signedEdge + 0.035 + 0.018 * sin(angle * 2.0 - age * 21.0 + i);
                    float wisp = (1.0 - step(px * 0.55, abs(wispEdge)))
                               * smoothstep(0.35, 0.9, sin(angle * 3.0 - age * 15.0 + i * 2.0));
                    float envelope = Window(age, 0.0, 0.025, 0.12 + i * 0.015, 0.235);
                    float strength = envelope * (1.0 - i * 0.20);
                    light.x += ((rimLine * 0.52 + inside * 0.36) * arc + wisp * 0.15) * strength;
                    light.y += (rimLine * (0.35 + broken * 0.48) * arc + inside * 0.09) * strength;
                }
                // 빠른 측면 충격파: 넓은 타원 전체를 닫지 않고 양쪽 초승달만 스친다.
                float ageWide = t - 0.205;
                float travelWide = 1.0 - pow(1.0 - saturate(ageWide / 0.30), 1.75);
                float2 wideRadius = float2(lerp(0.20, 2.20, travelWide), lerp(0.13, 0.295, travelWide));
                float2 wideQ = p / wideRadius;
                float wideLength = max(length(wideQ), 0.0001);
                float wideAngle = atan2(wideQ.y, wideQ.x);
                float wideField = wideLength - 1.0 + 0.025 * sin(wideAngle * 4.0 - ageWide * 18.0);
                float wideEdge = wideField / max(length(wideQ / wideLength / wideRadius), 0.001);
                float sideArc = smoothstep(0.64, 0.96, abs(cos(wideAngle)));
                float wideRim = 1.0 - step(px * 0.60, abs(wideEdge));
                float wideTrail = floor(saturate(1.0 + wideEdge / 0.03) * 3.0) / 3.0;
                wideTrail *= 1.0 - step(0.0, wideEdge);
                float wideLife = Window(ageWide, 0.0, 0.035, 0.08, 0.235);
                light += float2(wideRim * 0.33 + wideTrail * 0.13, wideRim * 0.16) * sideArc * wideLife;
                return light;
            }

            float2 Flare(float2 p, float t, float px)
            {
                float pulse = Window(t, 0.145, 0.055, 0.255, 0.205);
                float size = lerp(0.12, 1.0, pulse);
                float2 q = abs(p) / (float2(0.34, 0.265) * size);
                // 오목한 네 갈래 별: 큰 흰 사각형 대신 긴 끝과 작은 중심만 남긴다.
                float starField = sqrt(q.x) + sqrt(q.y);
                float star = 1.0 - step(1.0, starField);
                float starRim = 1.0 - step(1.18, starField);
                float diamond = abs(p.x) / max(0.075 * size, px)
                              + abs(p.y) / max(0.070 * size, px);
                float heart = 1.0 - step(1.0, diamond);
                float pin = (1.0 - step(px * 1.01, abs(p.x)))
                          * (1.0 - step(px * 1.01, abs(p.y)));
                float anticipation = Window(t, 0.025, 0.07, 0.125, 0.065) * 0.38;
                float gold = (starRim * 0.19 + star * 0.10) * pulse;
                float white = (star * 0.92 + heart * 0.38) * pulse + pin * anticipation;
                return float2(gold, white);
            }

            float2 Sparks(float2 p, float t, float px)
            {
                float2 light = float2(0.0, 0.0);
                [unroll]
                for (int j = 0; j < 20; ++j)
                {
                    float i = (float)j;
                    float h = Hash(i + 1.0);
                    float age = t - (0.19 + Hash(i + 31.0) * 0.105);
                    float life = 0.24 + Hash(i + 71.0) * 0.27;
                    float u = saturate(age / life);
                    float angle = (i + 0.23 + h * 0.5) * 2.39996323;
                    float2 direction = float2(cos(angle), sin(angle));
                    float travel = 1.0 - pow(1.0 - u, 2.2);
                    float2 reach = float2(0.37 + h * 0.61, 0.12 + h * 0.17);
                    float2 center = direction * reach * (0.18 + travel * 0.82);
                    center.y -= u * u * 0.025;
                    if (_PixelSnap > 0.5) center = round(center / px) * px;
                    float2 d = abs(p - center);
                    float radius = px * (1.05 + step(0.80, h) * 1.0);
                    float diamond = 1.0 - step(radius, d.x + d.y);
                    // 2도트 결정과 드문 4도트 결정, 뒤에는 짧은 잔상만 둔다.
                    float2 tailCenter = center - direction * px * 3.0;
                    if (_PixelSnap > 0.5) tailCenter = round(tailCenter / px) * px;
                    float2 tailDelta = abs(p - tailCenter);
                    float tail = 1.0 - step(px * 0.75, tailDelta.x + tailDelta.y);
                    float envelope = Window(age, 0.0, 0.025, life * 0.45, life * 0.55);
                    float twinkle = 0.72 + 0.28 * sin(u * 11.0 + i * 3.0);
                    light.x += (diamond * 0.42 + tail * 0.18) * envelope;
                    light.y += diamond * (0.64 + h * 0.42) * envelope * twinkle;
                }
                return light;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float t = _SequenceTime;
                clip(t);
                clip(3.05 - t);
                float px = max(_PixelSize, 0.0001);
                float2 p = (input.uv - 0.5) * max(_Extent.xy, float2(px, px));
                if (_PixelSnap > 0.5) p = round(p / px) * px;
                int layer = (int)round(_Layer);
                if (layer == 0)
                {
                    float dim = Window(t, 0.08, 0.20, 2.46, 0.54) * 0.15;
                    return float4(0.0, 0.0, 0.0, saturate(dim * input.color.a * _Tint.a * _Intensity));
                }
                float2 light = float2(0.0, 0.0);
                if (layer == 1) light = Beam(p, t, px);
                else if (layer == 2) light = Shockwave(p, t, px);
                else if (layer == 3) light = Flare(p, t, px);
                else if (layer == 4) light = Sparks(p, t, px);
                float3 color = (_Tint.rgb * light.x + _CoreColor.rgb * light.y) * input.color.rgb;
                color *= _Intensity * input.color.a * _Tint.a;
                return float4(color, 0.0);
            }
            ENDHLSL
        }
    }
}
