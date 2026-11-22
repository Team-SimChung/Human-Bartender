// 등급 연출 이펙트 셰이더 (URP 2D Renderer, 파티클 전용, 도트 아트, 가산·알파 겸용).
// 텍스처 없이 모양을 계산해서 그린다: 가로 광선 / 십자 별빛 / 둥근 빛 / 타원 고리 / 마름모 고리 / 방사 광선 / 스프라이트(텍스처, 없으면 단색 사각형).
// 꼭짓점과 모양 계산을 모두 월드 도트 격자(_PixelSize, PPU 100이면 0.01유닛)에 맞춰 씬 도트와 같은 칸으로 그려지고 서브픽셀로 미끄러지지 않는다.
// 광선 심·별빛 팔·고리 두께는 파티클 크기와 상관없이 항상 같은 도트 수(_Thickness)로 그린다. 그래서 Size over Lifetime으로 고리를 키워도 선은 1도트 그대로다.
// 밝기를 _Bands 단계로 끊어 도트처럼 계단지게 하고, _CorePoint보다 밝은 부분은 _CoreColor(흰빛)로 바꾼다.
// 파티클의 Start Color / Color over Lifetime(정점 색)이 색과 세기에 곱해진다.
// 알파 처리 방식은 _AlphaShrinks로 고른다. 켜면(기본) 알파를 모양에 먼저 곱해 바깥 단계부터 사라지는 도트식 페이드가 되고(알파를 낮추면 크기가 줄어 보임),
// 끄면 모양과 단계는 그대로 두고 알파는 투명도로만 쓴다. 머티리얼 _Color의 알파는 언제나 투명도로만 곱해진다.
// 혼합은 미리 곱한 알파(One, OneMinusSrcAlpha)다. _BlendAlpha 0이면 가산(빛), 1이면 보통 알파(화면을 어둡게 덮기)로 그린다.
// _Prism을 올리면 가운데는 흰빛 그대로 두고 가장자리로 갈수록 롤 프리즘 색(연보라·분홍·하늘·민트)이 섞인다. 색상은 쿼드 가로를 따라 흐르고 밝기는 그대로다. 0이면 예전과 똑같다.
Shader "Custom/VFX/SH_GradeReveal"
{
    Properties
    {
        [Enum(Streak,0,Star,1,Glow,2,Ring,3,Diamond,4,Rays,5,Sprite,6)] _Shape ("모양 (Streak 가로 광선, Star 십자 별빛, Glow 둥근 빛, Ring 타원 고리, Diamond 마름모 고리, Rays 방사 광선, Sprite 텍스처)", Float) = 0
        _MainTex ("텍스처 (모양이 Sprite일 때만, 비우면 단색 사각형)", 2D) = "white" {}
        [HDR] _Color ("색", Color) = (1, 0.8, 0.42, 1)
        [HDR] _CoreColor ("가장 밝은 부분 색", Color) = (1, 0.97, 0.86, 1)
        _CorePoint ("이 밝기부터 가장 밝은 부분 색으로 바뀜 (1이면 끔)", Range(0, 1)) = 0.75
        _Intensity ("세기", Range(0, 8)) = 1.5
        _Bands ("밝기 단계 수 (0이면 부드럽게)", Range(0, 8)) = 4
        _BlendAlpha ("덮는 정도 (0 = 가산 빛, 1 = 보통 알파로 덮기)", Range(0, 1)) = 0
        [ToggleUI] _AlphaShrinks ("알파가 줄면 바깥 단계부터 사라짐 (끄면 알파 = 투명도, 크기 그대로)", Float) = 1

        [Header(Shape)]
        _Thickness ("선 두께 (도트) - 광선 심, 별빛 팔 중심, 고리 (광선은 0이면 심 없음)", Range(0, 8)) = 1
        _Falloff ("끝으로 갈수록 줄어드는 정도 (클수록 중심에 모임)", Range(0.2, 6)) = 1.5
        _Halo ("번짐 세기 (광선 위아래, 별빛·방사 광선 중심, 고리 안쪽)", Range(0, 1)) = 0.5
        _HaloSize ("고리 안쪽 번짐 길이 (도트)", Range(0, 32)) = 6
        _RayCount ("방사 광선 개수", Range(2, 32)) = 12
        _RaySharpness ("방사 광선 날카로움 (클수록 가늘다)", Range(1, 32)) = 6

        [Header(Pixel Art)]
        _PixelSize ("도트 크기 (유닛, PPU 100이면 0.01)", Float) = 0.01
        [ToggleUI] _PixelSnap ("도트 격자에 맞추기", Float) = 1

        [Header(Prism)]
        _Prism ("프리즘 양 (0이면 끔, 가운데는 흰빛 그대로 두고 가장자리에 섞임)", Range(0, 1)) = 0
        _PrismHue ("프리즘 팔레트 시작 위치 (롤 프리즘 순환: 0 연보라, 0.25 분홍, 0.5 하늘, 0.75 민트)", Range(0, 1)) = 0.75
        _PrismSpread ("쿼드 왼쪽 끝에서 오른쪽 끝까지 바뀌는 색상 폭", Range(0, 2)) = 0.7
        _PrismCore ("가운데에도 섞이는 프리즘 양 (0이면 가운데는 흰빛, 1이면 가운데까지 다 섞임)", Range(0, 1)) = 0
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
            Name "GradeReveal"
            Tags { "LightMode" = "Universal2D" }

            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half4 _CoreColor;
                float _Shape;
                float _CorePoint;
                float _Intensity;
                float _Bands;
                float _BlendAlpha;
                float _AlphaShrinks;
                float _Thickness;
                float _Falloff;
                float _Halo;
                float _HaloSize;
                float _RayCount;
                float _RaySharpness;
                float _PixelSize;
                float _PixelSnap;
                float _Prism;
                float _PrismHue;
                float _PrismSpread;
                float _PrismCore;
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

            // 흰빛에 무지개 색을 amount만큼 섞고 휘도를 1로 맞춘다 (곱해도 밝기는 그대로, 색만 바뀜).
            float3 PrismTint(float hue, float amount)
            {
                // 롤 프리즘 팔레트 (선형값, 노란 끼 없음): 연보라 -> 분홍 -> 하늘 -> 민트 -> 다시 연보라. hue는 이 순환 위의 위치다.
                const float3 keys[4] =
                {
                    float3(0.552, 0.402, 1.000), float3(1.000, 0.342, 0.672), float3(0.305, 0.617, 1.000), float3(0.352, 0.922, 0.672)
                };
                float x = frac(hue) * 4.0;
                int i0 = min((int)floor(x), 3);
                int i1 = i0 == 3 ? 0 : i0 + 1;
                float f = smoothstep(0.0, 1.0, x - floor(x));
                float3 prism = lerp(keys[i0], keys[i1], f);
                float3 mixColor = lerp(float3(1.0, 1.0, 1.0), prism, saturate(amount));
                return mixColor / max(dot(mixColor, float3(0.2126, 0.7152, 0.0722)), 0.001);
            }

            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            // 모양 함수 f(q)가 Δf만큼 변할 때 월드에서 몇 도트를 움직이는지 구한다.
            // gradQ: q 공간에서의 f 기울기, dqdWx / dqdWy: 월드 x, y로 1유닛 움직일 때 q의 변화량
            float CellDistance(float deltaF, float2 gradQ, float2 dqdWx, float2 dqdWy, float px)
            {
                float2 gradW = float2(dot(gradQ, dqdWx), dot(gradQ, dqdWy));
                return deltaF / max(length(gradW) * px, 1e-6);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                if (_PixelSnap > 0.5)
                {
                    // 쿼드 꼭짓점을 도트 격자에 붙여, 크기가 도트 배수인 파티클은 항상 같은 칸 수를 덮는다.
                    float px = max(_PixelSize, 0.0001);
                    positionWS.xy = round(positionWS.xy / px) * px;
                }
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS.xy;
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            // 가로 광선: 가운데가 밝고 양끝으로 갈수록 어두워지는 가는 심 + 위아래 번짐.
            // 심 두께는 가운데에서 _Thickness 도트, 끝으로 갈수록 가늘어진다 (최소 1도트).
            float ShapeStreak(float2 q, float2 dqdWx, float2 dqdWy, float px)
            {
                float ax = abs(q.x);
                float along = pow(saturate(1.0 - ax), _Falloff);
                float dyCells = CellDistance(abs(q.y), float2(0.0, 1.0), dqdWx, dqdWy, px);
                float halfCore = max(_Thickness * 0.5 * (1.0 - 0.6 * ax), 0.5);
                float core = (_Thickness > 0.0 && dyCells <= halfCore) ? 1.0 : 0.0;
                float halo = pow(saturate(1.0 - abs(q.y)), 2.0) * _Halo;
                return along * max(core, halo);
            }

            // 십자 별빛: 가로·세로 팔이 중심에서 _Thickness 도트로 시작해 끝에서 1도트로 가늘어진다 + 중심 둥근 빛.
            float ShapeStar(float2 q, float2 dqdWx, float2 dqdWy, float px)
            {
                float ax = abs(q.x);
                float ay = abs(q.y);
                float dyCells = CellDistance(ay, float2(0.0, 1.0), dqdWx, dqdWy, px);
                float dxCells = CellDistance(ax, float2(1.0, 0.0), dqdWx, dqdWy, px);
                float halfH = max(lerp(max(_Thickness, 1.0), 1.0, ax) * 0.5, 0.5);
                float halfV = max(lerp(max(_Thickness, 1.0), 1.0, ay) * 0.5, 0.5);
                float armH = dyCells <= halfH ? pow(saturate(1.0 - ax), _Falloff) : 0.0;
                float armV = dxCells <= halfV ? pow(saturate(1.0 - ay), _Falloff) : 0.0;
                float glow = pow(saturate(1.0 - length(q) * 2.5), 2.0) * _Halo;
                return max(max(armH, armV), glow);
            }

            // 둥근 빛 (쿼드 비율대로 타원).
            float ShapeGlow(float2 q)
            {
                return pow(saturate(1.0 - length(q)), _Falloff);
            }

            // 타원 고리 / 마름모 고리: 쿼드 가장자리에서 1도트 안쪽에 _Thickness 도트 두께 선 + 안쪽으로 _HaloSize 도트 번짐.
            float ShapeRing(float2 q, float2 dqdWx, float2 dqdWy, float px, bool diamond)
            {
                float r;
                float2 g;
                if (diamond)
                {
                    r = abs(q.x) + abs(q.y);
                    g = float2(q.x >= 0.0 ? 1.0 : -1.0, q.y >= 0.0 ? 1.0 : -1.0);
                }
                else
                {
                    r = length(q);
                    g = r > 1e-5 ? q / r : float2(1.0, 0.0);
                }
                float inside = CellDistance(1.0 - r, g, dqdWx, dqdWy, px);
                float thickness = max(_Thickness, 0.5);
                float ring = (inside >= 1.0 && inside < 1.0 + thickness) ? 1.0 : 0.0;
                float haloT = (inside - 1.0 - thickness) / max(_HaloSize, 0.001);
                float halo = (_HaloSize > 0.0 && inside >= 1.0 + thickness) ? pow(saturate(1.0 - haloT), 2.0) * _Halo : 0.0;
                return max(ring, halo);
            }

            // 방사 광선: 중심에서 사방으로 뻗는 광선. 광선마다 길이가 조금씩 다르고, 사이사이 짧고 가는 광선이 있다.
            // 회전은 파티클의 Rotation over Lifetime으로 준다.
            float ShapeRays(float2 q)
            {
                float r = length(q);
                float a = atan2(q.y, q.x);
                float n = max(round(_RayCount), 2.0);
                float sector = a / (2.0 * PI) * n;
                float index = floor(sector + 0.5);
                index -= n * floor(index / n);
                float rayLength = 0.65 + 0.35 * Hash11(index * 7.31 + 1.7);
                float mainRay = pow(0.5 + 0.5 * cos(a * n), _RaySharpness) * pow(saturate(1.0 - r / rayLength), _Falloff);
                float subRay = pow(0.5 - 0.5 * cos(a * n), _RaySharpness * 2.0) * pow(saturate(1.0 - r / (rayLength * 0.6)), _Falloff) * 0.6;
                float center = pow(saturate(1.0 - r * 3.0), 2.0) * _Halo;
                return max(max(mainRay, subRay), center);
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 화면 미분으로 "월드 1유닛당 UV 변화량"을 구한다 (분기 전에 계산해야 미분이 정확하다).
                float2 uv = input.uv;
                float2 ws = input.positionWS;
                float2 dUVdx = ddx(uv);
                float2 dUVdy = ddy(uv);
                float2 dWdx = ddx(ws);
                float2 dWdy = ddy(ws);
                float det = dWdx.x * dWdy.y - dWdy.x * dWdx.y;
                float px = max(_PixelSize, 0.0001);

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
                        // 이 화면 픽셀이 속한 도트 칸 중심의 UV로 옮긴다. 모든 모양이 도트 칸 단위로 계산된다.
                        float2 d = (floor(ws / px) + 0.5) * px - ws;
                        uv += dUVdWx * d.x + dUVdWy * d.y;
                    }
                }

                float2 q = (uv - 0.5) * 2.0;
                float2 dqdWx = dUVdWx * 2.0;
                float2 dqdWy = dUVdWy * 2.0;
                int shape = (int)round(_Shape);
                half3 tint = _Color.rgb * input.color.rgb;

                float s;
                if (shape == 0)      s = ShapeStreak(q, dqdWx, dqdWy, px);
                else if (shape == 1) s = ShapeStar(q, dqdWx, dqdWy, px);
                else if (shape == 2) s = ShapeGlow(q);
                else if (shape == 3) s = ShapeRing(q, dqdWx, dqdWy, px, false);
                else if (shape == 4) s = ShapeRing(q, dqdWx, dqdWy, px, true);
                else if (shape == 5) s = ShapeRays(q);
                else
                {
                    half4 tex = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv * _MainTex_ST.xy + _MainTex_ST.zw, 0);
                    s = tex.a;
                    tint *= tex.rgb;
                }

                // 쿼드 밖 칸은 버린다 (도트 칸 중심이 쿼드 바깥에 걸리는 가장자리).
                s *= (abs(q.x) <= 1.0 && abs(q.y) <= 1.0) ? 1.0 : 0.0;
                s = saturate(s);

                // _AlphaShrinks 켬: 알파를 단계로 끊기 전에 곱한다 (바깥 단계부터 사라짐).
                // _AlphaShrinks 끔: 모양·단계는 알파와 상관없이 그대로, 알파는 아래에서 투명도로만 곱한다.
                bool alphaShrinks = _AlphaShrinks > 0.5;
                if (alphaShrinks) s *= input.color.a;

                if (_Bands >= 1.0)
                {
                    float bands = round(_Bands);
                    s = floor(s * bands + 0.5) / bands;
                }

                float core = saturate((s - _CorePoint) / max(1.0 - _CorePoint, 0.001));
                float opacity = _Color.a * (alphaShrinks ? 1.0 : input.color.a);
                float strength = s * opacity;
                half3 rgb = lerp(tint, _CoreColor.rgb * input.color.rgb, core) * (strength * _Intensity);
                if (_Prism > 0.0)
                {
                    // 가운데(q=0)는 흰빛 그대로, 쿼드 가장자리로 갈수록 프리즘 색이 섞이고 색상은 가로를 따라 흐른다.
                    rgb *= (half3)PrismTint(_PrismHue + q.x * 0.5 * _PrismSpread, _Prism * lerp(_PrismCore, 1.0, saturate(length(q))));
                }
                return half4(rgb, strength * _BlendAlpha);
            }
            ENDHLSL
        }
    }
}
