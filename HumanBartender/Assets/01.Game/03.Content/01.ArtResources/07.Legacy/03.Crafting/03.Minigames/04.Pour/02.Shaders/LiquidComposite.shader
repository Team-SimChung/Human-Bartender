Shader "Pour/LiquidComposite"
{
    Properties
    {
        _MainTex ("Blit Source", 2D) = "black" {}
        _Color ("Liquid Color", Color) = (1,1,1,1)
        _Threshold ("Threshold", Float) = 0.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        ZWrite Off Cull Off
        HLSLINCLUDE
        #include "UnityCG.cginc"
        sampler2D _FieldTex, _EffectsTex, _PreviousEffects, _RawSurface, _SurfaceTex, _PreviousSurface;
        sampler2D _CameraSortingLayerTexture;
        float4 _FieldTex_TexelSize, _RawSurface_TexelSize;
        float4 _Color, _FoamColor, _GlassSize, _PixelGrid, _Optics, _Cloud, _Foam, _Step, _ContainerVelocity;
        float4x4 _ScreenToWorld, _WorldToScreen, _GlassToWorld, _WorldToGlass;
        float _Threshold, _EdgeSmoothness, _BubbleSize;

        float hash21(float2 p) { return frac(sin(dot(p,float2(127.1,311.7))) * 43758.5453); }
        float inside(float2 uv) { return step(0,uv.x)*step(uv.x,1)*step(0,uv.y)*step(uv.y,1); }
        float occupancy(float density) { return smoothstep(_Threshold-_EdgeSmoothness,_Threshold,density); }
        float2 glassToScreen(float2 uv)
        {
            float2 local = (uv * 2 - 1) * _GlassSize.xy;
            float4 world = mul(_GlassToWorld,float4(local,0,1));
            return mul(_WorldToScreen,world).xy;
        }
        float4 fieldAt(float2 glassUV)
        {
            float2 screen = glassToScreen(glassUV);
            return tex2Dlod(_FieldTex,float4(screen,0,0)) * inside(screen);
        }
        float2 localVelocity(float4 field)
        {
            float2 velocity = field.gb / max(field.r,0.05) - _ContainerVelocity.xy;
            return mul((float3x3)_WorldToGlass,float3(velocity,0)).xy / max(0.001,_GlassSize.xy * 2);
        }

        // 0: 액체와 거품을 같은 픽셀 격자로 합성한다.
        float4 composite(v2f_img i) : SV_Target
        {
            float2 screen = (floor(i.uv * _PixelGrid.xy) + 0.5) / _PixelGrid.xy;
            float4 field = tex2D(_FieldTex,screen);
            float mask = step(_Threshold,field.r);
            float2 world = mul(_ScreenToWorld,float4(screen,0,1)).xy;
            float2 local = mul(_WorldToGlass,float4(world,0,1)).xy / max(0.001,_GlassSize.xy * 2) + 0.5;
            float inGlass = inside(local) * _GlassSize.z;
            float4 surface = tex2D(_SurfaceTex,float2(saturate(local.x),0.5));
            float valid = surface.a * inGlass;
            float foamTop = surface.r + min(surface.g * _Foam.z, surface.r * 0.35);
            float breakup = 0.0025 * sin(floor(local.x*45)*2.3 + _Step.y*0.7);
            float foam = step(surface.r-0.006,local.y) * step(local.y,foamTop+breakup) *
                step(0.005,surface.g) * valid;
            // 화면 대부분의 빈 영역에서는 조명/기포 텍스처 계산을 하지 않는다.
            [branch] if (max(mask,foam) < 0.001) discard;
            float2 effects = tex2D(_EffectsTex,saturate(local)).rg * inGlass;
            float radial = sqrt(saturate(1 - pow(local.x * 2 - 1,2)));
            float capDepth = max(0.002,_PixelGrid.w * radial);
            float cap = saturate(1 - abs(local.y - (surface.r - capDepth * 0.5)) / (capDepth * 0.5)) * valid;
            cap *= mask; // 실제 액체 실루엣 안에서만 수면을 밝힌다.
            float connected = step(local.y,surface.r + 0.005) * valid;
            float thickness = radial * connected;

            float2 e = max(_FieldTex_TexelSize.xy,1 / _PixelGrid.xy);
            float2 gradient = float2(
                occupancy(tex2D(_FieldTex,screen+float2(e.x,0)).r) - occupancy(tex2D(_FieldTex,screen-float2(e.x,0)).r),
                occupancy(tex2D(_FieldTex,screen+float2(0,e.y)).r) - occupancy(tex2D(_FieldTex,screen-float2(0,e.y)).r));
            float3 normal = normalize(float3(-gradient * 1.6 + float2((local.x-0.5)*0.45*connected, cap*0.7), 1));
            float light = saturate(dot(normal,normalize(float3(-0.45,0.65,0.8))));
            float shade = floor((0.70 + light * 0.33) * _PixelGrid.z) / _PixelGrid.z;
            float3 color = _Color.rgb * shade * exp(-_Optics.x * thickness * 0.32);
            float highlight = smoothstep(0.91,0.99,light) * _Optics.z;
            color += highlight * 0.26;
            color = lerp(color,lerp(_Color.rgb,_FoamColor.rgb,0.45),cap * 0.4);
            float cloud = _Cloud.w > 0 ? saturate(effects.r) : 0;
            color = lerp(color,_FoamColor.rgb,cloud * (1 - _Optics.y * 0.6));

            // 고정 seed와 시간으로 기포를 움직인다. CPU 입자는 추가하지 않는다.
            float bubbles = 0;
            if (_Cloud.w > 0 && connected > 0)
            {
                float worldWidth = length(mul((float3x3)_GlassToWorld,float3(_GlassSize.x*2,0,0)));
                float worldHeight = length(mul((float3x3)_GlassToWorld,float3(0,_GlassSize.y*2,0)));
                float cellSize = max(0.001,worldWidth/12);
                float2 p = local * float2(12,worldHeight/cellSize);
                p.y -= _Step.y * _Cloud.z/cellSize;
                float2 cell = floor(p);
                float random = hash21(cell);
                float2 centre = float2(0.25 + hash21(cell+3)*0.5,0.3 + hash21(cell+7)*0.4);
                float pixelWorldSize = length(mul((float3x3)_ScreenToWorld,float3(1/_PixelGrid.x,0,0)));
                float radius = min(0.38,max(lerp(0.08,0.16,random)*_BubbleSize,pixelWorldSize*0.85/cellSize));
                float d = length(frac(p)-centre);
                bubbles = step(d,radius) * step(random, _Cloud.w * 0.65);
                color = lerp(color,_FoamColor.rgb,bubbles * 0.9);
            }
            if (_Foam.w > 0 && connected > 0)
            {
                float2 p = local * float2(25,35) - localVelocity(field) * _Step.y * 0.15;
                float fleck = step(0.97,hash21(floor(p))) * step(length(frac(p)-0.5),0.23);
                color *= 1 - fleck * _Foam.w * 0.35;
            }

            float foamShade = 0.88 + 0.12 * step(0.4,hash21(floor(local*float2(100,150))));
            color = lerp(color,_FoamColor.rgb * foamShade,foam);
            float alpha = saturate(_Color.a + thickness * _Optics.x * 0.12 + cloud * 0.35 + highlight * 0.12);
            alpha = max(alpha * mask,foam * 0.96);
            if (_Optics.w > 0 && mask > 0)
            {
                float2 refractedUV = clamp(screen + normal.xy * _Optics.w * (1-_Optics.y),0,1);
                color = lerp(tex2D(_CameraSortingLayerTexture,refractedUV).rgb,color,alpha);
                alpha = 1;
            }
            clip(alpha - 0.001);
            return float4(color,alpha);
        }

        // 1: 잔 로컬 좌표의 기포 농도. 카메라 이동에도 구름이 화면에 붙지 않는다.
        float4 evolveCloud(v2f_img i) : SV_Target
        {
            if (_GlassSize.z < 0.5 || _Cloud.w <= 0) return 0;
            float4 field = fieldAt(i.uv);
            float occupied = smoothstep(_Threshold*0.7,_Threshold,field.r);
            float2 velocity = localVelocity(field);
            float2 up = mul((float3x3)_WorldToGlass,float3(0,_Cloud.z,0)).xy / max(0.001,_GlassSize.xy*2);
            float2 from = i.uv - (velocity + up) * _Step.x;
            float oldCloud = tex2D(_PreviousEffects,saturate(from)).r * inside(from);
            float cloud = oldCloud * exp(-_Step.x / _Cloud.y);
            cloud += saturate(field.a) * _Cloud.x;
            // 탄산 자체는 맑은 액체를 우유처럼 만들지 않는다.
            cloud += _Cloud.w * _Step.x * 0.015 * occupied;
            return float4(saturate(cloud) * occupied,0,0,0);
        }

        // 2: 바닥에서 이어진 액체만 찾는다. 낙하 중인 줄기는 수면 높이에 포함하지 않는다.
        float4 extractSurface(v2f_img i) : SV_Target
        {
            if (_GlassSize.z < 0.5) return 0;
            float height = 0, vx = 0, found = 0;
            int gap = 0;
            [loop] for (int y=0; y<128; y++)
            {
                float localY = (y+0.5)/128.0;
                float4 field = fieldAt(float2(i.uv.x,localY));
                if (field.r >= _Threshold)
                {
                    found = 1; height = localY; gap = 0;
                    vx = localVelocity(field).x;
                }
                else
                {
                    gap++;
                    if ((found > 0 && gap > 2) || (found == 0 && gap > 8)) break;
                }
            }
            return float4(height,0,vx,found);
        }

        // 3: 수면을 안정화하고 내부 기포와 다른 시간축으로 거품을 유지한다.
        float4 evolveSurface(v2f_img i) : SV_Target
        {
            float4 centre = tex2D(_RawSurface,i.uv);
            if (centre.a < 0.5) return 0;
            float sum = 0, weight = 0;
            [unroll] for (int x=-2; x<=2; x++)
            {
                float4 sample = tex2D(_RawSurface,i.uv+float2(x*_RawSurface_TexelSize.x,0));
                float w = sample.a * (3-abs(x));
                sum += sample.r*w; weight += w;
            }
            float height = sum/max(1,weight);
            // 몸통에 이어진 가는 유입 줄기도 수면으로 오인하지 않는다.
            // 입자 간격 범위의 중앙값으로 위로 솟은 좁은 부분만 낮춘다.
            float worldWidth = length(mul((float3x3)_GlassToWorld,float3(_GlassSize.x*2,0,0)));
            float stride = clamp(_Step.z/max(0.001,worldWidth),_RawSurface_TexelSize.x,0.1);
            float heights[7];
            [unroll] for (int j=0; j<7; j++)
            {
                float4 neighbour = tex2D(_RawSurface,float2(saturate(i.uv.x+(j-3)*stride),0.5));
                heights[j] = neighbour.a > 0.5 ? neighbour.r : centre.r;
            }
            [unroll] for (int a=0; a<6; a++)
                [unroll] for (int b=a+1; b<7; b++)
                {
                    float lower = min(heights[a],heights[b]);
                    heights[b] = max(heights[a],heights[b]);
                    heights[a] = lower;
                }
            height = min(height,heights[3]+0.008);
            float4 previous = tex2D(_PreviousSurface,float2(saturate(i.uv.x-centre.b*_Step.x),0.5));
            float amount = previous.g * exp(-_Step.x/_Foam.y);
            float cloud = tex2D(_EffectsTex,float2(i.uv.x,saturate(height-0.012))).r;
            float impact = fieldAt(float2(i.uv.x,height)).a;
            amount += (cloud * _Foam.x * 2 + _Cloud.w * _Foam.x * 0.06) * _Step.x;
            amount += impact * _Foam.x * 0.2;
            return float4(height,saturate(amount),centre.b,1);
        }
        ENDHLSL
        Pass
        {
            Name "Composite"
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert_img
            #pragma fragment composite
            ENDHLSL
        }
        Pass
        {
            Name "Cloud"
            Blend Off ZTest Always
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert_img
            #pragma fragment evolveCloud
            ENDHLSL
        }
        Pass
        {
            Name "Surface"
            Blend Off ZTest Always
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert_img
            #pragma fragment extractSurface
            ENDHLSL
        }
        Pass
        {
            Name "Foam"
            Blend Off ZTest Always
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert_img
            #pragma fragment evolveSurface
            ENDHLSL
        }
    }
}
