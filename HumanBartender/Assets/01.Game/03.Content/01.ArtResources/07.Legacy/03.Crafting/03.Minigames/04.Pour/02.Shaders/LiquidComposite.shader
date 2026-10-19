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
        // _CloudMotion: 상승 속도(월드/초), 분산, 색 혼합 강도 / _FoamShape: 유지 시간(초), 윗면 덩어리
        float4 _CloudMotion, _FoamShape;
        float4x4 _ScreenToWorld, _WorldToScreen, _GlassToWorld, _WorldToGlass;
        float _Threshold, _EdgeSmoothness, _BubbleSize;

        float hash21(float2 p) { return frac(sin(dot(p,float2(127.1,311.7))) * 43758.5453); }
        float valueNoise(float2 p)
        {
            float2 i = floor(p), f = frac(p);
            f = f * f * (3 - 2 * f);
            return lerp(lerp(hash21(i), hash21(i + float2(1,0)), f.x),
                        lerp(hash21(i + float2(0,1)), hash21(i + 1), f.x), f.y);
        }
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
        float2 glassWorldSize()
        {
            return float2(length(mul((float3x3)_GlassToWorld,float3(_GlassSize.x*2,0,0))),
                          length(mul((float3x3)_GlassToWorld,float3(0,_GlassSize.y*2,0))));
        }
        // 최종 픽셀 격자 한 칸이 잔 로컬 좌표에서 차지하는 크기.
        float2 pixelInGlass()
        {
            float pixelWorld = length(mul((float3x3)_ScreenToWorld,float3(1/_PixelGrid.x,0,0)));
            return pixelWorld / max(0.001, glassWorldSize());
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
            float2 px = pixelInGlass();
            // 거품은 서로 연결된 층이다. 열 하나의 수면/유입 변화가 세로 빗살로 튀지 않게
            // 표시 픽셀 폭에서 함께 읽는다. 실제 액체 실루엣(mask)은 그대로 둔다.
            float4 surface = 0;
            float surfaceWeight = 0;
            float surfaceStride = max(px.x, _RawSurface_TexelSize.x);
            [unroll] for (int sx=-3; sx<=3; sx++)
            {
                float4 s = tex2D(_SurfaceTex,float2(saturate(local.x + sx * surfaceStride),0.5));
                float w = (4-abs(sx)) * s.a;
                surface += s * w;
                surfaceWeight += w;
            }
            surface /= max(0.001, surfaceWeight);
            float valid = step(0.5, surfaceWeight) * inGlass;

            // 거품층: 수량(g)×최대 높이. 잔 입구 위로는 1.5칸, 적은 액체 위에서는 깊이에 비례해 제한한다.
            float headroom = 1 + px.y * 1.5 - surface.r;
            float thick = min(surface.g * _Foam.z, min(headroom, surface.r * 0.9 + px.y * 2));
            // 반복되는 동일 폭의 톱니 대신 서로 이어지는 넓고 낮은 거품 덩어리.
            float bump = local.x / max(0.001, px.x * 5);
            float lumpHeight = valueNoise(float2(bump, _Step.y * 0.22 + 5.1));
            float lump = _FoamShape.y * min(px.y * 1.5, thick * 0.3) * lumpHeight;
            float hasFoam = step(px.y * 0.5, thick) * valid;
            float wetEdge = valueNoise(float2(bump * 1.7 + 17.3, _Step.y * 0.16));
            float foamBottom = surface.r - px.y * (0.4 + wetEdge * 0.8);
            float foamTop = surface.r + thick + lump;
            float foam = smoothstep(foamBottom, foamBottom + px.y * 1.4, local.y) * step(local.y,foamTop) * hasFoam;
            // 화면 대부분의 빈 영역에서는 조명/기포 텍스처 계산을 하지 않는다.
            [branch] if (max(mask,foam) < 0.001) discard;
            float2 effects = tex2D(_EffectsTex,saturate(local)).rg * inGlass;
            float radial = sqrt(saturate(1 - pow(local.x * 2 - 1,2)));
            float capDepth = max(0.002,_PixelGrid.w * radial);
            float cap = saturate(1 - abs(local.y - (surface.r - capDepth * 0.5)) / (capDepth * 0.5)) * valid;
            cap *= mask * (1 - hasFoam); // 실제 액체 실루엣 안에서만 수면을 밝힌다.
            float connected = step(local.y,surface.r + 0.005) * valid;
            float thickness = radial * connected;

            float2 e = max(_FieldTex_TexelSize.xy,1 / _PixelGrid.xy);
            float2 gradient = float2(
                occupancy(tex2D(_FieldTex,screen+float2(e.x,0)).r) - occupancy(tex2D(_FieldTex,screen-float2(e.x,0)).r),
                occupancy(tex2D(_FieldTex,screen+float2(0,e.y)).r) - occupancy(tex2D(_FieldTex,screen-float2(0,e.y)).r));
            float3 normal = normalize(float3(-gradient * 1.6 + float2((local.x-0.5)*0.45*connected, cap*0.7), 1));
            float light = saturate(dot(normal,normalize(float3(-0.45,0.65,0.8))));
            float shade = floor((0.74 + light * 0.30) * _PixelGrid.z) / _PixelGrid.z;
            // 투과율을 깊이만큼 거듭제곱한다. 두꺼운 곳은 어두워지면서 색도 진해진다(맥주의 호박색).
            float3 color = pow(max(_Color.rgb,0.0001), 1 + _Optics.x * thickness * 0.9) * shade;
            float highlight = smoothstep(0.91,0.99,light) * _Optics.z;
            // 반사광은 흰색을 더하지 않고 액체 색을 밝게 한다.
            color = lerp(color, saturate(color * 1.6 + 0.1), highlight * 0.7);
            color = lerp(color,lerp(_Color.rgb,_FoamColor.rgb,0.3),cap * 0.25);
            float3 liquid = color;

            // 미세기포 구름: 흰 막 대신 액체 색을 옅게 하는 탁도와 위로 한 칸씩 오르는 기포 점.
            float cloud = _Cloud.w > 0 ? saturate(effects.r) : 0;
            float cloudVis = smoothstep(0.03,0.6,cloud) * _CloudMotion.z * mask;
            float3 haze = lerp(color, _FoamColor.rgb, 0.5);
            color = lerp(color, haze, cloudVis * (1 - _Optics.y * 0.6));
            if (cloudVis > 0.01)
            {
                float pixelWorld = px.y * glassWorldSize().y;
                float2 speckCell = floor(float2(local.x / px.x, local.y / px.y - _Step.y * _CloudMotion.x / max(0.0001,pixelWorld)));
                float speck = step(hash21(speckCell + 0.37), cloudVis * 0.4);
                color = lerp(color, _FoamColor.rgb, speck * 0.5);
            }

            // 고정 seed와 시간으로 기포를 움직인다. CPU 입자는 추가하지 않는다.
            float bubbles = 0;
            if (_Cloud.w > 0 && connected > 0)
            {
                float2 size = glassWorldSize();
                float cellSize = max(0.001,size.x/12);
                float2 p = local * float2(12,size.y/cellSize);
                p.y -= _Step.y * _Cloud.z/cellSize;
                float2 bubbleCell = floor(p);
                float random = hash21(bubbleCell);
                float2 centre = float2(0.25 + hash21(bubbleCell+3)*0.5,0.3 + hash21(bubbleCell+7)*0.4);
                float pixelWorldSize = length(mul((float3x3)_ScreenToWorld,float3(1/_PixelGrid.x,0,0)));
                float radius = min(0.38,max(lerp(0.08,0.16,random)*_BubbleSize,pixelWorldSize*0.5/cellSize));
                float d = length(frac(p)-centre);
                bubbles = step(d,radius) * step(random, _Cloud.w * 0.18);
                color = lerp(color,_FoamColor.rgb,bubbles * 0.45);
            }
            if (_Foam.w > 0 && connected > 0)
            {
                float2 p = local * float2(25,35) - localVelocity(field) * _Step.y * 0.15;
                float fleck = step(0.97,hash21(floor(p))) * step(length(frac(p)-0.5),0.23);
                color *= 1 - fleck * _Foam.w * 0.35;
            }

            // 거품 픽셀: 윗줄 하이라이트, 드문 기공, 액체와 닿는 아랫줄의 전이색.
            float2 foamCell = floor(local / max(0.0001,px));
            float fromTop = (foamTop - local.y) / max(0.0001,px.y);
            float fromBottom = (local.y - foamBottom) / max(0.0001,px.y);
            float3 foamColor = _FoamColor.rgb * (0.94 + 0.06 * step(0.5, hash21(foamCell)));
            float pore = step(0.87, hash21(foamCell)) *
                step(1.5, fromTop) * step(1.5, fromBottom);
            foamColor *= 1 - pore * 0.12;
            foamColor = lerp(foamColor, saturate(_FoamColor.rgb * 1.05), step(fromTop, 1));
            foamColor *= lerp(1, 0.93, step(0.86, local.x));
            foamColor = lerp(foamColor, lerp(_FoamColor.rgb, liquid * 1.15, 0.5), step(fromBottom, 1.2) * mask);
            color = lerp(color,foamColor,foam);

            // 탁도와 흡수가 클수록 두꺼운 곳이 불투명해진다. 재료 데이터의 alpha는 얇은 줄기의 값이다.
            float alpha = 1 - (1 - _Color.a) * exp(-(_Optics.x * 0.9 + _Optics.y * 1.5) * thickness);
            alpha = saturate(alpha + cloudVis * 0.2 + highlight * 0.1);
            alpha = max(alpha * mask,foam * 0.97);
            if (_Optics.w > 0 && mask > 0)
            {
                float2 refractedUV = clamp(screen + normal.xy * _Optics.w * (1-_Optics.y),0,1);
                color = lerp(tex2D(_CameraSortingLayerTexture,refractedUV).rgb,color,alpha);
                alpha = 1;
            }
            clip(alpha - 0.001);
            return float4(color,alpha);
        }

        // 1: 잔 로컬 좌표의 미세기포 농도. 충돌 지점에서 덩어리로 생겨 흐름과 부력으로 오르며 흩어진다.
        float4 evolveCloud(v2f_img i) : SV_Target
        {
            if (_GlassSize.z < 0.5 || _Cloud.w <= 0) return 0;
            float4 field = fieldAt(i.uv);
            float occupied = smoothstep(_Threshold*0.7,_Threshold,field.r);
            float2 size = max(0.001, glassWorldSize());
            float2 velocity = localVelocity(field) * 0.85;
            float2 up = mul((float3x3)_WorldToGlass,float3(0,_CloudMotion.x,0)).xy / max(0.001,_GlassSize.xy*2);
            // 시간에 따라 바뀌는 소용돌이 성분으로 같은 모양이 반복되지 않게 흔든다.
            float2 q = i.uv * size / 0.18;
            float t = _Step.y;
            float2 swirl = float2(valueNoise(q + float2(0, t * 0.6)) - 0.5, valueNoise(q + float2(31.7, -t * 0.6)) - 0.5);
            float dispersion = _CloudMotion.y;
            float2 from = i.uv - (velocity + up + swirl * dispersion * 0.35 / size) * _Step.x;
            float old = tex2D(_PreviousEffects,saturate(from)).r * inside(from);
            // 분산: 이웃으로 퍼지며 봉우리가 낮아진다. dt가 0이면 아무것도 바꾸지 않는다.
            float2 r = dispersion * 0.04 / size * saturate(_Step.x * 60);
            float blur = 0.25 * (
                tex2D(_PreviousEffects,saturate(from + float2(r.x,0))).r + tex2D(_PreviousEffects,saturate(from - float2(r.x,0))).r +
                tex2D(_PreviousEffects,saturate(from + float2(0,r.y))).r + tex2D(_PreviousEffects,saturate(from - float2(0,r.y))).r);
            old = lerp(old, blur * inside(from), saturate(_Step.x * 6 * dispersion));
            float cloud = old * exp(-_Step.x / _Cloud.y);
            // 충돌은 사건마다 한 번 기록된다. 겹친 충돌을 포화시키고 매번 다른 덩어리 모양으로 남긴다.
            float2 seed = float2(hash21(floor(t * 13) + 0.3), hash21(floor(t * 13) + 7.1)) * 50;
            float clump = smoothstep(0.42, 0.78, valueNoise(i.uv * size / 0.09 + seed));
            float hit = 1 - exp(-2 * field.a);
            // 잔 속으로 파고드는 유입 줄기가 기포를 끌고 내려간다. 고인 액체의 느린 흐름에서는 생기지 않는다.
            float plunge = saturate((-field.b / max(field.r,0.05) - 0.8) / 2) * occupied;
            float deposit = saturate(_Cloud.x * (0.45 * hit + 2.5 * plunge * _Step.x) * (0.25 + 0.75 * clump));
            cloud = 1 - (1 - cloud) * (1 - deposit);
            // 탄산 자체는 맑은 액체를 우유처럼 만들지 않는다.
            cloud += _Cloud.w * _Step.x * 0.01 * occupied;
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
                // 빠르게 떨어지는 유입 줄기는 수면이 아니다(두껍게 그린 줄기가 수면을 끌어올리지 않게).
                float fall = -field.b / max(field.r,0.05);
                if (field.r >= _Threshold && fall < 1.1)
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

        // 3: 수면을 안정화하고 거품을 내부 구름과 독립된 시간축으로 키우고 유지한다.
        // r: 수면 높이, g: 거품량(0~1), b: 남은 유지 시간(초), a: 유효
        float4 evolveSurface(v2f_img i) : SV_Target
        {
            float4 centre = tex2D(_RawSurface,i.uv);
            float sum = 0, weight = 0;
            [unroll] for (int x=-2; x<=2; x++)
            {
                float4 sample = tex2D(_RawSurface,i.uv+float2(x*_RawSurface_TexelSize.x,0));
                float w = sample.a * (3-abs(x));
                sum += sample.r*w; weight += w;
            }
            if (centre.a < 0.5)
            {
                // 한 열만 유입 속도 때문에 누락된 경우 양쪽 수면이 있을 때만 잇는다.
                float4 l = tex2D(_RawSurface,i.uv-float2(_RawSurface_TexelSize.x,0));
                float4 r = tex2D(_RawSurface,i.uv+float2(_RawSurface_TexelSize.x,0));
                if (l.a < 0.5 || r.a < 0.5) return 0;
                centre = (l+r)*0.5;
            }
            float height = sum/max(1,weight);
            // 몸통에 이어진 가는 유입 줄기도 수면으로 오인하지 않는다.
            // 입자 간격 범위의 중앙값으로 위로 솟은 좁은 부분만 낮춘다.
            float2 size = glassWorldSize();
            float stride = clamp(_Step.z/max(0.001,size.x),_RawSurface_TexelSize.x,0.1);
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
            // 줄기가 꽂히는 열은 빠른 하강 흐름이 수면에서 빠져 홈이 생긴다. 이웃 중앙값으로 메운다.
            height = clamp(height,heights[3]-0.008,heights[3]+0.008);

            float2 at = float2(saturate(i.uv.x-centre.b*_Step.x),0.5);
            float4 previous = tex2D(_PreviousSurface,at);
            // 거품이 얹히는 기준 높이는 약 0.08초에 걸쳐 따라간다. 입자 실루엣과 판정은 지연하지 않는다.
            if (previous.a > 0.5)
                height = lerp(previous.r, height, 1-exp(-_Step.x*12));
            // 거품은 수면을 따라 옆으로 번져 유입 지점에서 잔 전체로 덮인다.
            float spread = 4 * _RawSurface_TexelSize.x;
            float4 left = tex2D(_PreviousSurface,saturate(at - float2(spread,0)));
            float4 right = tex2D(_PreviousSurface,saturate(at + float2(spread,0)));
            float4 far = 0.5 * (tex2D(_PreviousSurface,saturate(at - float2(spread*2.5,0))) +
                                tex2D(_PreviousSurface,saturate(at + float2(spread*2.5,0))));
            float lateral = 0.35 * (left.g + right.g) + 0.3 * far.g;
            float amount = lerp(previous.g, max(previous.g, lateral), saturate(_Step.x * 6));
            float hold = max(previous.b, max(max(left.b, right.b), far.b) - 0.25);

            // 실제 유입: 수면 바로 위의 아래로 향하는 액체 흐름(밀도×하강 속도)과 수면 충돌.
            float inflow = 0, impact = 0;
            [unroll] for (int k=1; k<=6; k++)
            {
                [unroll] for (int s=-1; s<=1; s++)
                {
                    float4 f = fieldAt(float2(i.uv.x + s * stride, height + k * 0.018));
                    inflow += f.r * max(0, -localVelocity(f).y);
                }
            }
            // 충돌은 수면 아래 유입 지점에서 기록된다.
            [unroll] for (int d=1; d<=4; d++)
                impact = max(impact, fieldAt(float2(i.uv.x, height - d * 0.02)).a);
            // 실제 프리팹에서 최대 유량의 유입 합은 약 30, 약하게 기울이면 10~15였다.
            inflow = saturate(inflow / 34);
            impact = saturate(impact);
            float drive = saturate(inflow * 0.7 + inflow * inflow * 0.3 + impact * 0.5);
            if (drive > 0.05) hold = _FoamShape.x;
            else hold = max(0, hold - _Step.x);
            // 유입이 있는 동안과 유지 시간에는 천천히, 이후 수명에 따라 가라앉는다.
            float tau = _Foam.y * lerp(1, 6, saturate(hold / max(0.05, _FoamShape.x) * 1.5));
            amount *= exp(-_Step.x / max(0.05, tau));
            // 유입 세기가 거품층이 다다를 수 있는 높이를 정하고 foamGain이 그 높이까지 차오르는 빠르기를 정한다.
            // 약하게 따르면 오래 따라도 얇고, 세게 부으면 빨리 두껍게 솟는다.
            float target = saturate(drive * 1.35) * saturate(_Foam.x);
            amount += max(0, target - amount) * (1 - exp(-_Foam.x * 3 * drive * _Step.x));
            amount += _Foam.x * _Cloud.w * 0.03 * _Step.x;
            return float4(height,saturate(amount),hold,1);
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
