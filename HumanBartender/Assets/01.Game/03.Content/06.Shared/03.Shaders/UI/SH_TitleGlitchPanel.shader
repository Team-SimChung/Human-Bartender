// 타이틀 로고(uGUI Image)용 글리치 셰이더.
// 원본: kwakyosep의 SH_UI_GlitchPanel.shadergraph(#13, f87dedeb). 값 이름과 계산을 그대로 옮기고,
// 화면 뒤를 일그러뜨리던 부분(_CameraSortingLayerTexture)은 Image 스프라이트(_MainTex)로,
// 판 가장자리 비네트 알파는 스프라이트 알파로 바꿨다. 시간은 셰이더가 _Time으로 혼자 돈다(스크립트 불필요).
// 예전 TitleLogoGlitch.cs(메시를 가로 띠로 나눠 밀던 효과)도 "Band Shift"로 이 셰이더에 옮겼다.
// QA-34: 웹 title-logo.js의 가로 찢김을 "Signal Tear"로 추가했다. 표 텍스처 TX_TitleLogoTear_LUT.png는
// 웹 frames 배열을 10ms 칸으로 펼친 것이다(286칸 × 75줄).
// UI 마스크(Stencil), RectMask2D(Clip Rect), Image 색·CanvasGroup 투명도를 지원한다.
Shader "Custom/TitleGlitchPanel"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Glitch)]
        _GlitchTex ("Glitch Noise", 2D) = "white" {}
        _GlitchAmount ("Glitch Amount (UV)", Range(0, 1)) = 0.017
        _GlitchCutAmountX ("Noise Tiling X", Range(0, 10)) = 0
        _GlitchCutAmountY ("Noise Tiling Y (slices)", Range(0, 10)) = 2.17
        _GlitchFPS ("Noise FPS (intermittent)", Float) = 3.49
        _GlitchSpeed ("Noise Speed (intermittent)", Float) = 5

        [Header(Intermittent)]
        [Toggle] _UseIntermittentGlitch ("Use Intermittent Glitch", Float) = 1
        _GlitchDurationMin ("Burst Duration Min (s)", Float) = 0.15
        _GlitchDurationMax ("Burst Duration Max (s)", Float) = 0.2
        _GlitchIntervalMin ("Burst Interval Min (s)", Float) = 4
        _GlitchIntervalMax ("Burst Interval Max (s)", Float) = 5

        [Header(RGB Split)]
        [Toggle] _UseRGBGlitch ("Use RGB Glitch", Float) = 1
        _GlitchColor1 ("Red Channel Color", Color) = (1,1,1,1)
        _GlitchColor2 ("Green Channel Color", Color) = (1,1,1,1)
        _GlitchColor3 ("Blue Channel Color", Color) = (1,1,1,1)

        [Header(Band Shift)]
        [Toggle] _UseBandShift ("Use Band Shift", Float) = 1
        _BandCount ("Band Count", Range(1, 64)) = 24
        _BandShift ("Max Shift (UV)", Range(0, 0.2)) = 0.031
        _BandPeriod ("Cycle (s)", Float) = 2.86
        _BandStart ("Active From (s)", Float) = 0.87
        _BandEnd ("Active Until (s)", Float) = 2.35
        _BandFlickerA ("Flicker Frequency A", Float) = 47
        _BandFlickerB ("Flicker Frequency B", Float) = 19
        _BandFPS ("Band Pattern FPS", Float) = 30

        [Header(Signal Tear (web title logo))]
        [Toggle] _UseTear ("Use Signal Tear", Float) = 0
        [NoScaleOffset] _TearTex ("Tear Table (x: time, y: row)", 2D) = "gray" {}
        _TearCycle ("Cycle (s)", Float) = 2.86
        _TearRowOrigin ("First Row (UV from top)", Float) = 0.137121
        _TearRowStep ("Row Height (UV)", Float) = 0.0098182
        _TearRows ("Row Count", Float) = 75
        _TearShiftScale ("Shift per Unit (UV)", Float) = 0.004975

        [Header(Layout)]
        _HorizontalPad ("Horizontal Padding (each side, of logo width)", Range(0, 0.3)) = 0

        [Header(UI)]
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _GlitchTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            float _GlitchAmount;
            float _GlitchCutAmountX;
            float _GlitchCutAmountY;
            float _GlitchFPS;
            float _GlitchSpeed;
            float _UseIntermittentGlitch;
            float _GlitchDurationMin;
            float _GlitchDurationMax;
            float _GlitchIntervalMin;
            float _GlitchIntervalMax;
            float _UseRGBGlitch;
            fixed4 _GlitchColor1;
            fixed4 _GlitchColor2;
            fixed4 _GlitchColor3;
            float _UseBandShift;
            float _BandCount;
            float _BandShift;
            float _BandPeriod;
            float _BandStart;
            float _BandEnd;
            float _BandFlickerA;
            float _BandFlickerB;
            float _BandFPS;
            float _UseTear;
            sampler2D _TearTex;
            float4 _TearTex_TexelSize;
            float _TearCycle;
            float _TearRowOrigin;
            float _TearRowStep;
            float _TearRows;
            float _TearShiftScale;
            float _HorizontalPad;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            // Shader Graph Random Range 노드와 같은 식.
            float RandomRange(float2 seed, float minValue, float maxValue)
            {
                float r = frac(sin(dot(seed, float2(12.9898, 78.233))) * 43758.5453);
                return lerp(minValue, maxValue, r);
            }

            // 원본 그래프의 "Intermittent" 묶음: 주기마다 랜덤한 시각에 랜덤한 길이만큼만 켠다.
            float BurstMask(float t)
            {
                if (_UseIntermittentGlitch < 0.5) return 1;
                float period = max((_GlitchIntervalMin + _GlitchIntervalMax) * 0.5, 0.01);
                float cycle = floor(t / period);
                float duration = RandomRange(float2(cycle, 12.34), _GlitchDurationMin, _GlitchDurationMax);
                float local = fmod(t, period) - RandomRange(float2(cycle, 47.2), 0, (_GlitchIntervalMax - _GlitchIntervalMin) * 0.5);
                return (1 - step(duration, local)) * step(0, local);
            }

            // 예전 TitleLogoGlitch: 주기 안 일정 시간대에만 sin 두 개로 깜빡이는 세기로, 가로 띠마다 다르게 민다.
            float BandShift(float2 uv, float t)
            {
                if (_UseBandShift < 0.5) return 0;
                float local = fmod(t, max(_BandPeriod, 0.1));
                float strength = (local < _BandStart || local > _BandEnd)
                    ? 0
                    : abs(sin(local * _BandFlickerA)) * abs(sin(local * _BandFlickerB));
                float band = floor(saturate(uv.y) * _BandCount);
                float tick = floor(t * _BandFPS);
                float pattern = (fmod(band * 17 + tick * 13, 11) - 5) / 5;
                return pattern * _BandShift * strength;
            }

            // 웹 title-logo.js: 2.86초 주기의 프레임 표대로 로고 가로 줄을 좌우로 민다.
            // 표(_TearTex)는 x가 시간(칸마다 같은 길이), y가 위에서부터 줄 번호이고, 값은 128 + 이동 단위 × 4다.
            float TearShift(float2 uv, float t)
            {
                float row = ((1 - uv.y) - _TearRowOrigin) / max(_TearRowStep, 0.0001);
                float inside = step(0, row) * step(row, _TearRows - 0.001) * step(0.5, _UseTear);
                float local = frac(t / max(_TearCycle, 0.01));
                float2 lookup = float2((floor(local * _TearTex_TexelSize.z) + 0.5) * _TearTex_TexelSize.x,
                    1 - (floor(clamp(row, 0, _TearRows - 1)) + 0.5) / max(_TearRows, 1));
                float units = (tex2D(_TearTex, lookup).r * 255 - 128) / 4;
                return units * _TearShiftScale * inside;
            }

            fixed4 Sample(float2 uv)
            {
                // 밀려서 그림 밖(0~1)을 읽으면 가장자리 픽셀이 늘어지지 않게 투명으로 둔다.
                float2 inside = step(0, uv) * step(uv, 1);
                fixed4 c = (tex2D(_MainTex, uv) + _TextureSampleAdd) * (inside.x * inside.y);
                c.rgb *= c.a; // 투명 픽셀의 RGB가 RGB 분리 때 새어 나오지 않게 미리 곱한다.
                return c;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float t = _Time.y;
                // 좌우 여백: 사각형을 넓혀 두고 그림은 가운데에 원래 크기로 그린다. 밀린 줄이 사각형 밖으로 잘리지 않게 한다.
                IN.texcoord.x = 0.5 + (IN.texcoord.x - 0.5) * (1 + 2 * _HorizontalPad);
                // 띠가 오른쪽으로 dx만큼 밀린 것처럼 보이도록 dx만큼 왼쪽을 읽는다.
                IN.texcoord.x -= BandShift(IN.texcoord, t) + TearShift(IN.texcoord, t);
                float scroll = _UseIntermittentGlitch > 0.5
                    ? floor(t * max(_GlitchFPS, 1)) / max(_GlitchFPS, 1) * _GlitchSpeed
                    : t * 100;
                float2 noiseUV = frac(IN.texcoord * float2(_GlitchCutAmountX, _GlitchCutAmountY) + float2(scroll, sin(scroll)));
                float amount = _GlitchAmount * BurstMask(t);
                float shift = tex2D(_GlitchTex, noiseUV).r * amount;

                fixed4 color;
                if (_UseRGBGlitch > 0.5)
                {
                    float weight = saturate(amount * 10);
                    fixed4 r = Sample(IN.texcoord + float2(shift, 0));
                    fixed4 g = Sample(IN.texcoord - float2(shift, 0));
                    fixed4 b = Sample(IN.texcoord + float2(0, shift));
                    color.rgb = r.r * lerp(float3(1, 0, 0), _GlitchColor1.rgb, weight)
                              + g.g * lerp(float3(0, 1, 0), _GlitchColor2.rgb, weight)
                              + b.b * lerp(float3(0, 0, 1), _GlitchColor3.rgb, weight);
                    color.a = max(r.a, max(g.a, b.a));
                }
                else
                {
                    color = Sample(IN.texcoord + float2(shift, 0));
                }

                // Image 색·CanvasGroup 투명도 (premultiplied).
                color *= fixed4(IN.color.rgb * IN.color.a, IN.color.a);

                #ifdef UNITY_UI_CLIP_RECT
                color *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
