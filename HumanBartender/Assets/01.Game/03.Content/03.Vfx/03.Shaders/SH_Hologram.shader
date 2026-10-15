// 도트 아트용 홀로그램 셰이더 (URP 2D Renderer, 스프라이트·파티클 공용).
// 텍스처의 밝기만 읽어 홀로그램 색(어두운 청록 → 청록 → 흰빛)으로 다시 칠하므로 어떤 초상화를 넣어도 같은 홀로그램처럼 보인다.
// 정점을 아트 픽셀 격자(_PixelSize 유닛)에 맞추고, 스캔라인·글리치도 아트 픽셀 단위로 계산해 캐릭터 도트와 크기가 맞는다.
// 파티클의 Start Color / Color over Lifetime(정점 색)이 색과 알파에 곱해진다.
Shader "Custom/VFX/SH_Hologram"
{
    Properties
    {
        _MainTex ("텍스처 (밝기만 사용)", 2D) = "white" {}

        [Header(Hologram Color)]
        _DarkColor ("어두운 부분 색", Color) = (0.02, 0.43, 0.45, 1)
        _BrightColor ("밝은 부분 색", Color) = (0, 0.98, 0.99, 1)
        _HighlightColor ("가장 밝은 부분 색", Color) = (0.86, 1, 1, 1)
        _BrightPoint ("밝은 색이 되는 밝기 (그 위는 흰빛)", Range(0.05, 1)) = 0.85
        _DarkAlpha ("어두운 부분 불투명도", Range(0, 1)) = 1
        _Alpha ("전체 불투명도", Range(0, 1)) = 1

        [Header(Pixel Art)]
        _PixelSize ("아트 픽셀 크기 (유닛, PPU 100이면 0.01)", Float) = 0.01
        [Toggle] _PixelSnap ("픽셀 격자에 맞추기", Float) = 1
        _PixelateCells ("픽셀화 칸 수 (가로, 세로, 0이면 끔)", Vector) = (0, 0, 0, 0)

        [Header(Scanline)]
        _ScanlineIntensity ("스캔라인 진하기", Range(0, 1)) = 0
        _ScanlineSpacing ("스캔라인 간격 (아트 픽셀)", Range(2, 8)) = 2
        _ScanlineSpeed ("스캔라인 내려오는 속도 (줄/초, 음수면 위로)", Range(-30, 30)) = 3

        [Header(Flicker)]
        _FlickerIntensity ("깜빡임 강도", Range(0, 1)) = 0
        _FlickerSpeed ("깜빡임 횟수 (초당)", Range(0, 60)) = 12

        [Header(Glitch)]
        _GlitchChance ("줄 밀림 확률", Range(0, 0.5)) = 0
        _GlitchShift ("줄 밀림 거리 (텍셀)", Range(0, 4)) = 1
        _GlitchSpeed ("줄 밀림 갱신 횟수 (초당)", Range(0, 60)) = 10

        [Header(Blend)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend (OneMinusSrcAlpha 보통, One 가산)", Float) = 10
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
            "CanUseSpriteAtlas" = "True"
        }

        Pass
        {
            Name "Hologram"
            Tags { "LightMode" = "Universal2D" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                half4 _DarkColor;
                half4 _BrightColor;
                half4 _HighlightColor;
                float _BrightPoint;
                float _DarkAlpha;
                float _Alpha;
                float _PixelSize;
                float _PixelSnap;
                float4 _PixelateCells;
                float _ScanlineIntensity;
                float _ScanlineSpacing;
                float _ScanlineSpeed;
                float _FlickerIntensity;
                float _FlickerSpeed;
                float _GlitchChance;
                float _GlitchShift;
                float _GlitchSpeed;
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
                // 꼭짓점을 아트 픽셀 격자에 붙여 캐릭터 도트와 같은 칸에 그려지게 한다.
                float pixel = max(_PixelSize, 0.0001);
                float2 snapped = round(positionWS.xy / pixel) * pixel;
                positionWS.xy = lerp(positionWS.xy, snapped, step(0.5, _PixelSnap));
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float pixel = max(_PixelSize, 0.0001);
                float time = _Time.y;
                float2 uv = input.uv;

                // 줄 단위 글리치: 아트 픽셀 한 줄씩 가끔 옆으로 밀린다.
                float pixelRow = floor(input.positionWS.y / pixel);
                float glitchFrame = floor(fmod(time * _GlitchSpeed, 4096.0));
                float glitchHit = step(Hash12(float2(pixelRow, glitchFrame)), _GlitchChance);
                float glitchDir = Hash12(float2(glitchFrame, pixelRow * 1.7)) > 0.5 ? 1.0 : -1.0;
                float2 cells = _PixelateCells.xy;
                float texelX = cells.x > 0.0 ? 1.0 / cells.x : _MainTex_TexelSize.x;
                uv.x += glitchHit * glitchDir * _GlitchShift * texelX;

                // 고해상도 초상화도 정해진 칸 수의 도트로 보이게 픽셀화
                if (cells.x > 0.0 && cells.y > 0.0)
                    uv = (floor(uv * cells) + 0.5) / cells;

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

                // 밝기를 화면 기준(sRGB)으로 읽어 3단 색으로 다시 칠한다.
                half luma = dot(tex.rgb, half3(0.299, 0.587, 0.114));
                #ifndef UNITY_COLORSPACE_GAMMA
                luma = LinearToSRGB(luma);
                #endif
                half brightT = saturate(luma / max(_BrightPoint, 0.001));
                half highT = saturate((luma - _BrightPoint) / max(1.0 - _BrightPoint, 0.001));
                half3 color = lerp(_DarkColor.rgb, _BrightColor.rgb, brightT);
                color = lerp(color, _HighlightColor.rgb, highT);
                color *= input.color.rgb;

                half alpha = tex.a * lerp(_DarkAlpha, 1.0, brightT) * input.color.a * _Alpha;

                // 아트 픽셀 단위 스캔라인 (_ScanlineSpacing 줄마다 한 줄 어둡게, 속도가 양수면 아래로)
                float scanRow = floor(input.positionWS.y / pixel + time * _ScanlineSpeed);
                float isScanline = step(fmod(abs(scanRow), max(round(_ScanlineSpacing), 1.0)), 0.5);
                color *= 1.0 - _ScanlineIntensity * isScanline;
                alpha *= 1.0 - _ScanlineIntensity * 0.5 * isScanline;

                // 전체 깜빡임
                float flicker = Hash12(float2(floor(time * _FlickerSpeed), 3.17));
                alpha *= 1.0 - _FlickerIntensity * flicker;

                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
