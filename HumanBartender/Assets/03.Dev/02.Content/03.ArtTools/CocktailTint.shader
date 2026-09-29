Shader "Custom/CocktailTint"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        // Sprite Editor의 Secondary Textures 에 "_MaskTex" 이름으로 등록하면
        // 스프라이트 UV(아틀라스 포함)를 그대로 따라옵니다.
        _MaskTex ("Mask (R=액체, G=잔)", 2D) = "black" {}

        _LiquidColor ("Liquid Color", Color) = (1, 0.25, 0.5, 1)
        _GlassColor  ("Glass Color",  Color) = (0.75, 0.9, 1, 1)

        // 각 부위 틴트 강도 (0=원본, 1=완전 교체)
        _LiquidAmount ("Liquid Amount", Range(0,1)) = 1
        _GlassAmount  ("Glass Amount",  Range(0,1)) = 0.6

        _Color ("Tint (SpriteRenderer.color)", Color) = (1,1,1,1)
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

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha   // Sprites-Default 와 동일 (premultiplied)

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            sampler2D _MainTex;
            sampler2D _MaskTex;
            fixed4 _LiquidColor;
            fixed4 _GlassColor;
            fixed  _LiquidAmount;
            fixed  _GlassAmount;
            fixed4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv     = v.uv;
                o.color  = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                fixed4 m = tex2D(_MaskTex, i.uv);

                // 원본의 명암(luminance)은 유지하면서 색만 바꿉니다.
                // → 원본이 이미 분홍색이어도 다른 색으로 깨끗하게 교체됨.
                float lum = dot(c.rgb, float3(0.299, 0.587, 0.114));

                fixed3 outRgb = c.rgb;
                outRgb = lerp(outRgb, lum * _LiquidColor.rgb, m.r * _LiquidAmount);
                outRgb = lerp(outRgb, lum * _GlassColor.rgb,  m.g * _GlassAmount);

                c.rgb = outRgb;
                c    *= i.color;
                c.rgb *= c.a;   // premultiply
                return c;
            }
            ENDCG
        }
    }
}
