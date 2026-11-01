Shader "Custom/CraftPrepSpriteOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (0.384314,0.92549,1,1)
        _OutlineThickness ("Hover Width (screen px)", Range(0,4)) = 1
        _OutlineIntensity ("Outline Intensity", Range(0,1)) = 0
        _HighlightStrength ("Highlight Strength", Range(0,2)) = 1
        _SelectionGlow ("Selection Glow", Range(0,1)) = 0
        _GlowRadius ("Selection Softness (screen px)", Range(1,6)) = 3
        _AlphaThreshold ("Silhouette Alpha Threshold", Range(0,1)) = 0.01
        [HideInInspector] _SpriteBounds ("Sprite Bounds", Vector) = (0,0,0.5,0.5)
        [HideInInspector] _SpriteUVRect ("Sprite UV Rect", Vector) = (0,0,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _OutlineColor;
                float _OutlineThickness;
                float _OutlineIntensity;
                float _HighlightStrength;
                float _SelectionGlow;
                float _GlowRadius;
                float _AlphaThreshold;
                float4 _SpriteBounds;
                float4 _SpriteUVRect;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float4 positionCS = TransformObjectToHClip(input.positionOS);
                float4 unitX = TransformObjectToHClip(input.positionOS + float3(1,0,0));
                float4 unitY = TransformObjectToHClip(input.positionOS + float3(0,1,0));
                float2 screenScale = _ScreenParams.xy * 0.5;
                float pixelsX = length((unitX.xy / unitX.w - positionCS.xy / positionCS.w) * screenScale);
                float pixelsY = length((unitY.xy / unitY.w - positionCS.xy / positionCS.w) * screenScale);
                // Expand only the private quad, leaving the original pixels at their original positions.
                float padding = _OutlineIntensity > 0 ? max(_GlowRadius * 2.5, _OutlineThickness + 1) : 0;
                float2 extra = padding / max(float2(pixelsX, pixelsY), 0.001);
                float2 extent = max(_SpriteBounds.zw, 0.0001);
                float2 direction = (input.positionOS.xy - _SpriteBounds.xy) / extent;
                float3 expanded = input.positionOS + float3(direction * extra, 0);
                output.positionCS = TransformObjectToHClip(expanded);
                float2 uvCenter = (_SpriteUVRect.xy + _SpriteUVRect.zw) * 0.5;
                output.uv = uvCenter + (input.uv - uvCenter) * (1 + extra / extent);
                output.color = input.color * _Color;
                return output;
            }

            float InSprite(float2 uv)
            {
                return step(_SpriteUVRect.x, uv.x) * step(uv.x, _SpriteUVRect.z)
                    * step(_SpriteUVRect.y, uv.y) * step(uv.y, _SpriteUVRect.w);
            }

            float Alpha(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv).a * InSprite(uv);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * InSprite(input.uv);
                half4 color = sprite * input.color;
                if (_OutlineIntensity <= 0) return color;
                float2 pixelX = ddx(input.uv);
                float2 pixelY = ddy(input.uv);
                float maxAlpha = 0;
                const float2 directions[8] = { float2(1,0), float2(-1,0), float2(0,1), float2(0,-1),
                    float2(.707,.707), float2(-.707,.707), float2(.707,-.707), float2(-.707,-.707) };
                [unroll] for (int i = 0; i < 8; ++i)
                {
                    float2 offset = (pixelX * directions[i].x + pixelY * directions[i].y) * _OutlineThickness;
                    maxAlpha = max(maxAlpha, Alpha(input.uv + offset));
                }
                float blurred = 0;
                if (_SelectionGlow > 0)
                {
                    // Separable Gaussian weights: a soft shadow rather than a solid yellow stroke.
                    const float weights[5] = { 1, 4, 6, 4, 1 };
                    [unroll] for (int y = 0; y < 5; ++y)
                        [unroll] for (int x = 0; x < 5; ++x)
                        {
                            float2 offset = (pixelX * (x - 2) + pixelY * (y - 2)) * _GlowRadius;
                            blurred += Alpha(input.uv + offset) * weights[x] * weights[y] / 256;
                        }
                }
                float outside = 1 - step(_AlphaThreshold, sprite.a);
                float halo = saturate(lerp(maxAlpha, blurred, _SelectionGlow) * outside
                    * saturate(_OutlineIntensity) * _OutlineColor.a * _HighlightStrength) * input.color.a;
                // Composite behind the sprite; its black contour and translucent interior stay intact.
                float alpha = color.a + halo * (1 - color.a);
                float3 rgb = (color.rgb * color.a + _OutlineColor.rgb * halo * (1 - color.a)) / max(alpha, 0.00001);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
