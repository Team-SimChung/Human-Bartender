Shader "Custom/SpriteOutline_Lit"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineThickness ("Outline Thickness (px)", Range(0, 10)) = 2
        _OutlineIntensity ("Outline Intensity", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType"     = "Plane"
            "RenderPipeline"  = "UniversalPipeline"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "Universal2D"
            Tags { "LightMode" = "Universal2D" } 

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_0 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_1 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_2 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_3 __

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/SurfaceData2D.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/InputData2D.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
                // 수정됨: 2D 라이팅은 월드 좌표가 아닌 Screen-space UV를 사용합니다.
                float2 lightingUV  : TEXCOORD1; 
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _MainTex_TexelSize;   

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _OutlineColor;
                float  _OutlineThickness;
                float  _OutlineIntensity;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color * _Color;
                
                // 수정됨: URP 2D 조명 텍스처를 샘플링하기 위한 스크린 좌표 계산
                float4 screenPos = ComputeScreenPos(OUT.positionHCS);
                OUT.lightingUV = screenPos.xy / screenPos.w;
                
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color;

                float2 texel = _MainTex_TexelSize.xy * _OutlineThickness;
                float neighborAlpha = 0;
                neighborAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( texel.x,  0)).a;
                neighborAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-texel.x,  0)).a;
                neighborAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( 0,  texel.y)).a;
                neighborAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( 0, -texel.y)).a;
                neighborAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( texel.x,  texel.y)).a;
                neighborAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-texel.x,  texel.y)).a;
                neighborAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( texel.x, -texel.y)).a;
                neighborAlpha += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-texel.x, -texel.y)).a;

                float outlineMask = saturate(neighborAlpha) * (1.0 - c.a);

                half4 result;
                result.rgb = lerp(_OutlineColor.rgb, c.rgb, c.a);
                result.a   = max(c.a, outlineMask * _OutlineIntensity * _OutlineColor.a);

                // --- 2. URP 2D 라이팅 연산 ---
                SurfaceData2D surfaceData;
                surfaceData.albedo = result.rgb;
                surfaceData.alpha = result.a;
                surfaceData.mask = 1.0; 
                
                // 수정됨: positionWS 대신 스크린 공간의 lightingUV를 매핑해야 합니다.
                InputData2D inputData;
                inputData.uv = IN.uv;
                inputData.lightingUV = IN.lightingUV; 

                // 2D 라이팅 블렌딩
                half4 finalColor = CombinedShapeLightShared(surfaceData, inputData);
                
                return half4(finalColor.rgb, result.a);
            }
            ENDHLSL
        }
    }
}