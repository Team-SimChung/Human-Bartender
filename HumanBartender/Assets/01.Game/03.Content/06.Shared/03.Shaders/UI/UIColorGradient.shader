Shader "Custom/UIColorGradient"
{
	Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // 1. 3가지 색상
        _LeftColor("Left Color (Green)", Color) = (0, 1, 0, 1)
        _CenterColor("Center Color (White)", Color) = (1, 1, 1, 1)
        _RightColor("Right Color (Red)", Color) = (1, 0, 0, 1)

        // 3. 색상의 경계점 (C#에서 비율을 계산해 넘겨줌)
        _LeftBoundary("Left Boundary", Range(0, 1)) = 0.33
        _RightBoundary("Right Boundary", Range(0, 1)) = 0.66
        
        // 2. 경계 뚜렷함 정도 (0에 가까울수록 칼같이 끊어지고, 클수록 자연스럽게 섞임)
        _Smoothness("Smoothness", Range(0.0001, 0.5)) = 0.1

        // UI Masking Properties
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
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
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _Color;

            float4 _LeftColor;
            float4 _CenterColor;
            float4 _RightColor;

            float _LeftBoundary;
            float _RightBoundary;
            float _Smoothness;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // URP 좌표계 변환
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 texColor = tex2D(_MainTex, IN.uv);

                // smoothstep을 이용해 경계 영역 계산
                // UV의 X좌표를 기준으로 부드러운 혼합 영역(Smoothness) 생성
                float blend1 = smoothstep(_LeftBoundary - _Smoothness, _LeftBoundary + _Smoothness, IN.uv.x);
                float blend2 = smoothstep(_RightBoundary - _Smoothness, _RightBoundary + _Smoothness, IN.uv.x);

                // 색상 보간 (좌측 -> 중앙 -> 우측)
                half4 gradientColor = lerp(lerp(_LeftColor, _CenterColor, blend1), _RightColor, blend2);

                // UI 기본 텍스쳐 및 이미지 컴포넌트의 Tint Color 적용
                return gradientColor * texColor * IN.color;
            }
            ENDHLSL
        }
    }
}
