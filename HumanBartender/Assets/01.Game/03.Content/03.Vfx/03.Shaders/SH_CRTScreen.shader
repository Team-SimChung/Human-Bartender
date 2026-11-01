// CRT 모니터 화면 효과 (URP 2D Renderer 전용).
// 2D 렌더러가 만드는 _CameraSortingLayerTexture(정렬 레이어 ForeFore까지 그린 화면)를 읽어
// 스캔라인, 천천히 내려오는 줄, 색수차, 번짐, 노이즈, 비네트를 입혀 화면 전체에 다시 그린다.
// 사용: 내장 Quad 메시에 이 셰이더의 머티리얼을 넣고 정렬 레이어를 Effect로 둔다.
// 정점 셰이더가 메시 UV로 화면 전체를 덮으므로 오브젝트 위치·크기와 상관없이 전체 화면에 적용된다.
Shader "Custom/VFX/SH_CRTScreen"
{
    Properties
    {
        [Header(Master)]
        _Intensity ("효과 강도 (0 = 원본, 1 = CRT)", Range(0, 1)) = 1

        [Header(Scanline)]
        _ScanlineCount ("스캔라인 개수 (화면 높이당 줄 수)", Range(10, 600)) = 74
        _ScanlineIntensity ("스캔라인 진하기", Range(0, 1)) = 0.14
        _ScanlineSharpness ("스캔라인 경계 선명도 (1 = 부드러운 사인파)", Range(0.5, 8)) = 1
        _ScanlineSpeed ("스캔라인 내려오는 속도 (줄/초, 음수면 위로)", Range(-10, 10)) = 0.5

        [Header(Roll Bar)]
        _RollIntensity ("굴러 내려오는 띠 밝기", Range(0, 0.5)) = 0.05
        _RollWidth ("굴러 내려오는 띠 두께 (화면 비율)", Range(0.01, 0.5)) = 0.08
        _RollSpeed ("굴러 내려오는 띠 속도 (화면/초, 음수면 위로)", Range(-1, 1)) = 0.04

        [Header(Color Bleed)]
        _ChromaticOffset ("색수차 (픽셀)", Range(0, 8)) = 0.5
        _HorizontalBlur ("가로 번짐 (픽셀)", Range(0, 4)) = 0.5

        [Header(Noise and Flicker)]
        _NoiseIntensity ("노이즈 강도", Range(0, 0.3)) = 0.035
        _NoiseFps ("노이즈 갱신 횟수 (초당)", Range(1, 60)) = 24
        _NoiseSize ("노이즈 크기 (픽셀)", Range(1, 16)) = 1
        _FlickerIntensity ("밝기 깜빡임 강도", Range(0, 0.2)) = 0.015
        _FlickerSpeed ("밝기 깜빡임 횟수 (초당)", Range(0, 60)) = 10

        [Header(Line Jitter)]
        _JitterIntensity ("가로 흔들림 (픽셀)", Range(0, 10)) = 0
        _JitterSpeed ("가로 흔들림 횟수 (초당)", Range(0, 60)) = 8

        [Header(Screen Shape)]
        _Curvature ("화면 휘어짐", Range(0, 0.5)) = 0
        _VignetteIntensity ("비네트 강도", Range(0, 1)) = 0.12
        _VignettePower ("비네트 퍼짐 (작을수록 가장자리만)", Range(0.05, 2)) = 0.35

        [Header(Color)]
        _Brightness ("밝기", Range(0, 2)) = 1.035
        _Contrast ("대비", Range(0, 2)) = 1
        _Saturation ("채도", Range(0, 2)) = 0.93
        _Tint ("색조", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "DisableBatching" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "CRTScreen"
            Tags { "LightMode" = "Universal2D" }

            Blend One Zero
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            TEXTURE2D(_CameraSortingLayerTexture);

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _ScanlineCount;
                float _ScanlineIntensity;
                float _ScanlineSharpness;
                float _ScanlineSpeed;
                float _RollIntensity;
                float _RollWidth;
                float _RollSpeed;
                float _ChromaticOffset;
                float _HorizontalBlur;
                float _NoiseIntensity;
                float _NoiseFps;
                float _NoiseSize;
                float _FlickerIntensity;
                float _FlickerSpeed;
                float _JitterIntensity;
                float _JitterSpeed;
                float _Curvature;
                float _VignetteIntensity;
                float _VignettePower;
                float _Brightness;
                float _Contrast;
                float _Saturation;
                half4 _Tint;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 screenUV : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                // 내장 Quad의 UV(0~1)를 클립 공간 -1~1로 펼쳐 화면 전체를 덮는다.
                // 렌더 텍스처에 그릴 때 위아래가 뒤집히는 플랫폼에서는 _ProjectionParams.x가 -1이므로 같이 뒤집는다.
                // 그러면 화면 UV는 메시 UV와 같아진다(아래 0, 위 1).
                // URP의 GetNormalizedScreenSpaceUV는 2D 렌더러가 설정하지 않는 _ScaleBiasRt에 의존해서 쓰지 않는다.
                float2 ndc = input.uv * 2.0 - 1.0;
                output.positionCS = float4(ndc.x, ndc.y * _ProjectionParams.x, 0.5, 1.0);
                output.screenUV = input.uv;
                return output;
            }

            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            half3 SampleScreen(float2 uv, float chromaticPx, float texelX)
            {
                float2 offset = float2(chromaticPx * texelX, 0.0);
                half r = SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_LinearClamp, uv + offset).r;
                half g = SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_LinearClamp, uv).g;
                half b = SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_LinearClamp, uv - offset).b;
                return half3(r, g, b);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 screenSize = _ScreenParams.xy;
                float2 texel = 1.0 / screenSize;
                float2 uv = input.screenUV;
                half3 original = SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_LinearClamp, uv).rgb;

                // 화면 휘어짐(배럴 왜곡). 0이면 그대로.
                float2 centered = uv - 0.5;
                float2 crtUV = 0.5 + centered * (1.0 + _Curvature * dot(centered, centered));
                float inside = step(0.0, crtUV.x) * step(crtUV.x, 1.0) * step(0.0, crtUV.y) * step(crtUV.y, 1.0);

                float time = _Time.y;

                // 줄 단위 가로 흔들림
                float pixelRow = floor(crtUV.y * screenSize.y);
                float jitter = (Hash12(float2(pixelRow, floor(time * _JitterSpeed))) - 0.5) * 2.0 * _JitterIntensity;
                float2 sampleUV = crtUV + float2(jitter * texel.x, 0.0);

                // 색수차 + 가로 번짐(1:2:1 세 탭, 번짐 0이면 원래 샘플과 같다)
                float2 blurOffset = float2(_HorizontalBlur * texel.x, 0.0);
                half3 color = SampleScreen(sampleUV, _ChromaticOffset, texel.x) * 2.0;
                color += SampleScreen(sampleUV - blurOffset, _ChromaticOffset, texel.x);
                color += SampleScreen(sampleUV + blurOffset, _ChromaticOffset, texel.x);
                color *= 0.25;

                // 화면에 보이는 값(sRGB) 기준으로 계산해야 수치가 눈에 보이는 밝기 변화와 맞는다.
                #ifndef UNITY_COLORSPACE_GAMMA
                color = LinearToSRGB(saturate(color));
                #endif

                half luma = dot(color, half3(0.299, 0.587, 0.114));
                color = lerp(luma.xxx, color, _Saturation);
                color = (color - 0.5) * _Contrast + 0.5;
                color *= _Brightness * _Tint.rgb;

                // 스캔라인: 속도가 양수면 줄이 아래로 내려온다.
                float scanPhase = crtUV.y * _ScanlineCount + time * _ScanlineSpeed;
                float scanWave = sin(scanPhase * TWO_PI);
                scanWave = sign(scanWave) * pow(abs(scanWave), 1.0 / _ScanlineSharpness);
                color *= 1.0 + _ScanlineIntensity * scanWave;

                // 화면 위에서 아래로 천천히 굴러가는 밝은 띠
                float rollCenter = 1.0 - frac(time * _RollSpeed);
                float rollDistance = abs(crtUV.y - rollCenter);
                rollDistance = min(rollDistance, 1.0 - rollDistance);
                float rollWidth = max(_RollWidth, 0.0001);
                color *= 1.0 + _RollIntensity * exp(-(rollDistance * rollDistance) / (rollWidth * rollWidth));

                // 밝기 깜빡임
                float flicker = Hash12(float2(floor(time * _FlickerSpeed), 7.31));
                color *= 1.0 + (flicker - 0.5) * 2.0 * _FlickerIntensity;

                // 픽셀 노이즈: _NoiseSize 픽셀 단위 칸마다 한 값(시간을 일정 범위로 묶어 정밀도 손실을 막는다)
                float noiseFrame = floor(fmod(time * _NoiseFps, 4096.0));
                float2 noiseCell = floor(input.positionCS.xy / max(_NoiseSize, 1.0));
                float noise = Hash12(noiseCell + noiseFrame * float2(17.13, 31.71));
                color += (noise - 0.5) * _NoiseIntensity;

                // 비네트
                float2 edge = saturate(crtUV * (1.0 - crtUV));
                float vignette = pow(saturate(16.0 * edge.x * edge.y), _VignettePower);
                color *= lerp(1.0, vignette, _VignetteIntensity);

                color = saturate(color) * inside;

                #ifndef UNITY_COLORSPACE_GAMMA
                color = SRGBToLinear(color);
                #endif

                return half4(lerp(original, color, _Intensity), 1.0);
            }
            ENDHLSL
        }
    }
}
