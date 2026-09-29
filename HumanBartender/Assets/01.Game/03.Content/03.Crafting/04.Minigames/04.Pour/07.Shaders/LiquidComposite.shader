// 블롭들이 가산 합성된 필드 텍스처에 threshold를 적용해 액체 모양으로 그린다(메타볼 2패스).
// 필드가 threshold를 넘는 곳만 액체가 되므로, 가까운 블롭끼리는 필드가 더해져 한 덩어리로 이어진다.
Shader "Pour/LiquidComposite"
{
    Properties
    {
        _MainTex ("Field", 2D) = "black" {}
        _Color ("Liquid Color", Color) = (1,1,1,1)
        _Threshold ("Threshold", Range(0.01, 3)) = 0.35
        _Smoothness ("Edge Smoothness", Range(0.001, 1)) = 0.12
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            float _Threshold;
            float _Smoothness;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float field = tex2D(_MainTex, i.uv).r;

                float alpha = smoothstep(_Threshold - _Smoothness, _Threshold + _Smoothness, field);
                clip(alpha - 0.001);

                return fixed4(_Color.rgb, alpha * _Color.a);
            }
            ENDCG
        }
    }
}
