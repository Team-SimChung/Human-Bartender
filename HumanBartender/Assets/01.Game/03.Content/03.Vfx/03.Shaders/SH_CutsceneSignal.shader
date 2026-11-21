Shader "HumanBartender/UI/Cutscene Signal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _SignalTime ("Studio time", Float) = 0
        [HideInInspector] _SignalAmount ("Studio strength", Float) = 1
        [Enum(Legacy,0,Dream,1,Documentary,2,Disposal,3,Bar,4,Illustration,5)] _SignalStyle ("Signal style", Float) = 0
        [Toggle] _DialogueAfterSignal ("Draw speech above signal", Float) = 0
        [HideInInspector] _Speckles ("Reception speckles", 2D) = "black" {}
        _Bars ("Letterbox height", Range(0,0.25)) = 0
        _Split ("Channel separation in virtual pixels", Range(0,6)) = 0
        _Sepia ("Sepia grade", Range(0,1)) = 0
        _Grain ("Noise", Range(0,1)) = 0.2
        _Vignette ("Vignette", Range(0,1)) = 0
        _Scan ("Scanlines", Range(0,1)) = 0.12
        _Streak ("Horizontal interference", Range(0,1)) = 0.15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode] Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            float4 _ClipRect;
            struct Input { float4 vertex:POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varying { float4 pos:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float2 local:TEXCOORD1; };
            sampler2D _MainTex;
            float _SignalTime, _SignalAmount, _Bars, _Grain, _Scan, _Streak, _Vignette, _Split, _Sepia;
            Varying vert(Input v) { Varying o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; o.local=v.vertex.xy; return o; }
            float noise(float2 p) { return frac(sin(dot(p,float2(12.9898,78.233)))*43758.5453); }
            float4 _MainTex_TexelSize, _Band, _Reception;
            float _SignalStyle, _DashPositions[110];
            float4 _Tears[9], _Blocks[5], _BlockColors[5];
            sampler2D _Speckles;

            // Reference canvases use a top-left origin and composite colours in sRGB.
            float3 displayColor(float2 uv)
            {
                float3 c = tex2D(_MainTex, saturate(uv)).rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                c = lerp(c*12.92, 1.055*pow(max(c,0),1./2.4)-.055, step(.0031308,c));
                #endif
                return c;
            }
            float3 renderColor(float3 c)
            {
                #ifndef UNITY_COLORSPACE_GAMMA
                c = saturate(c);
                c = lerp(c/12.92, pow((c+.055)/1.055,2.4), step(.04045,c));
                #endif
                return c;
            }
            float2 fromCanvas(float2 p, float2 size) { return float2(p.x/size.x, 1-p.y/size.y); }
            float2 canvasPoint(float2 uv, float2 size) { return float2(uv.x, 1-uv.y)*size; }

            float shiftRow(float x, float y, float top, float height, float shift, float2 size)
            {
                float a = floor(top*size.y/720+.5);
                float b = a+max(1,floor(height*size.y/720+.5));
                float source = x-floor(shift*size.x/1280+.5);
                return y>=a && y<b && source>=0 && source<size.x ? source : x;
            }
            float2 displacedUV(float2 uv)
            {
                if (_Band.z<.5) return uv;
                float beat=_Band.x, p=_Band.y;
                float dx=(_Band.w>0?_Band.w:3+floor(23*p+.5))*(fmod(beat,2)<1?1:-1);
                float2 size=_MainTex_TexelSize.zw, pixel=canvasPoint(uv,size);
                // Canvas draws these copies in sequence. Trace the destination backwards.
                pixel.x=shiftRow(pixel.x,pixel.y,115+fmod(beat*71,420),8+floor(24*p+.5),-dx,size);
                pixel.x=shiftRow(pixel.x,pixel.y,364,3,-sign(dx)*ceil(abs(dx)*.55),size);
                pixel.x=shiftRow(pixel.x,pixel.y,355+fmod(beat,5),3+floor(3*p+.5),dx,size);
                return fromCanvas(pixel,size);
            }

            float rowCoverage(float y,float origin,float height,float pixel)
            {
                return saturate((min(y+pixel*.5,origin+height)-max(y-pixel*.5,origin))/pixel);
            }
            float dreamDash(float2 p, int row, int previous, float2 pixel)
            {
                if (row<0 || row>=720) return 0;
                // 347 is the multiplicative inverse of 83 modulo 720.
                int index=(row*347)%720;
                if (index>=110 || (previous!=0 && (index%2)==0)) return 0;
                return rowCoverage(p.x,_DashPositions[index],1+index%23,pixel.x)*
                    rowCoverage(p.y,row,1+index%2,pixel.y);
            }
            float3 dream(float2 uv)
            {
                uv=displacedUV(uv);
                float2 p=canvasPoint(uv,float2(1280,720)), pixel=float2(1280,720)*abs(_MainTex_TexelSize.xy);
                int row=(int)floor(p.y);
                float dash=max(dreamDash(p,row,0,pixel),dreamDash(p,row-1,1,pixel));
                // Include partial coverage at non-integer window scales.
                dash=max(dash,dreamDash(p,row+1,0,pixel));
                float3 tint=float3(177,214,218)/255.;
                float3 c=lerp(displayColor(uv),tint,dash*_Grain*.23);
                float scan=rowCoverage(p.y,floor((p.y+.5*pixel.y)/4)*4,1,pixel.y);
                return lerp(c,tint,scan*_Grain*.1);
            }
            float3 documentary(float2 uv)
            {
                float y=(1-uv.y)*720, pixel=abs(_MainTex_TexelSize.y)*720;
                float phase=fmod(_SignalTime*30,6);
                float top=floor((y-phase+.5*pixel)/6)*6+phase;
                float scan=top>=0 ? rowCoverage(y,top,2,pixel) : 0;
                return lerp(displayColor(uv),float3(0,16,24)/255.,scan*_Scan*.6);
            }

            float3 disposalFrame(float2 p)
            {
                if (p.y<floor(270*_Bars+.5) || p.y>=270-floor(270*_Bars+.5)) return 0;
                float3 c=displayColor(fromCanvas(p,float2(480,270)));
                c=lerp(c,dot(c,float3(.3,.59,.11)).xxx,.75);
                c=lerp(c,float3(159,216,255)/255.,.07);
                return c*(1-_Vignette*saturate((length(p-float2(240,135))-50)/240));
            }
            float3 disposal(float2 uv)
            {
                float2 p=canvasPoint(displacedUV(uv),float2(480,270));
                float2 channel=float2(_Reception.x,_Reception.y);
                float3 c=disposalFrame(p+float2(channel.x,-channel.y));
                c.r=disposalFrame(p-channel).r;
                // Overlapping strips copy from the same original frame; the last copy wins.
                bool torn = false;
                float sourceX = p.x;
                [unroll] for (int i = 0; i < 9; i++)
                {
                    float3 strip = _Tears[i].xyz;
                    float x = p.x - strip.z;
                    if (p.y >= strip.x && p.y < strip.x + strip.y && x >= 0 && x < 480)
                    {
                        torn = true;
                        sourceX = x;
                    }
                }
                if (torn) c = disposalFrame(float2(sourceX, p.y));
                [unroll] for(int j=0;j<5;j++)
                {
                    float4 block=_Blocks[j];float2 d=p-block.xy;
                    if(all(d>=0) && all(d<block.zw)) c+=(1-c)*_BlockColors[j].rgb;
                }
                float4 dotColor=tex2D(_Speckles,p/float2(480,270));
                c=lerp(c,dotColor.rgb,dotColor.a);
                if(fmod(floor(p.y),2)==_Reception.z) c*=1-(.08+.16*_Reception.w);
                float ry=fmod(_SignalTime*110,350)-40;
                return lerp(c,1,saturate(1-abs(p.y-ry-23)/23)*(.05+.1*_Reception.w));
            }

            float grainCell(float2 position)
            {
                float2 cell=floor(position), f=frac(position);f=f*f*(3-2*f);
                return lerp(lerp(noise(cell),noise(cell+float2(1,0)),f.x),
                            lerp(noise(cell+float2(0,1)),noise(cell+1),f.x),f.y);
            }
            float3 bar(float2 uv)
            {
                float3 c=displayColor(uv);
                float3 warm=float3(dot(c,float3(.393,.769,.189)),dot(c,float3(.349,.686,.168)),dot(c,float3(.272,.534,.131)));
                c=lerp(c,warm,.38*_Sepia);
                c=lerp(dot(c,float3(.213,.715,.072)).xxx,c,.6)*.94+.03;
                float2 p=canvasPoint(uv,float2(1280,720));
                c=lerp(c,float3(39,24,11)/255.,(fmod(720-p.y,4)<1?_Scan:0));
                float vignette=saturate((length((uv-.5)*2)/1.41421356-.35)/.65);
                c=lerp(c,float3(8,6,4)/255.,vignette*_Vignette);
                float2 edge=min(p,float2(1280,720)-p);
                float2 blur=.5*exp(-edge*edge/3700.);
                c=lerp(c,float3(18,10,5)/255.,(1-(1-blur.x)*(1-blur.y))*160./255.);
                float phase=floor(fmod(_SignalTime,.4)*10);
                float2 tile=fmod(p+8-(phase<2?phase:4-phase)*float2(2,-1.5),140)*.7;
                float g=(grainCell(tile)-.5)*.5+(grainCell(tile*2)-.5)*.25+(grainCell(tile*4)-.5)*.125;
                return lerp(c,.5+g,_Grain*.8*.5);
            }
            fixed4 frag(Varying i):SV_Target
            {
                if (_SignalStyle>4.5)
                {
                    // Preserve the web artwork's soft edge when fading onto a black stage.
                    float alpha=tex2D(_MainTex,i.uv).a*i.color.a;
                    #ifdef UNITY_UI_CLIP_RECT
                    alpha*=UnityGet2DClipping(i.local,_ClipRect);
                    #endif
                    return float4(renderColor(displayColor(i.uv)*alpha)*i.color.rgb/max(alpha,1.e-5),alpha);
                }
                if (_SignalStyle>.5)
                {
                    float3 c;
                    if (_SignalStyle<1.5) c=dream(i.uv);
                    else if (_SignalStyle<2.5) c=documentary(i.uv);
                    else if (_SignalStyle<3.5) c=disposal(i.uv);
                    else c=bar(i.uv);
                    return float4(lerp(tex2D(_MainTex,i.uv).rgb,renderColor(c),_SignalAmount),1);
                }
                float2 pix=floor(i.uv*float2(480,270));
                float tick=floor(_SignalTime*18);
                float n=noise(pix+float2(tick,0));
                float strength=_SignalAmount;
                float2 uv=i.uv;
                float band=floor(pix.y/4);
                float jitter=step(.988,noise(float2(band,tick)))*(noise(float2(tick,band))-.5)*_Streak*.12;
                uv.x+=jitter*strength;
                float delta=_Split*strength/480;
                float3 c=tex2D(_MainTex,uv-float2(delta,0)).rgb;
                c.r=tex2D(_MainTex,uv+float2(delta,0)).r;
                if (_Sepia>0)
                {
                    float3 srgb=c;
                    #ifndef UNITY_COLORSPACE_GAMMA
                    srgb=LinearToGammaSpace(c);
                    #endif
                    float3 warm=float3(dot(srgb,float3(.393,.769,.189)),dot(srgb,float3(.349,.686,.168)),dot(srgb,float3(.272,.534,.131)));
                    warm=lerp(srgb,warm,.38);
                    warm=lerp(dot(warm,float3(.2126,.7152,.0722)).xxx,warm,.6)*.94+.03;
                    #ifndef UNITY_COLORSPACE_GAMMA
                    warm=GammaToLinearSpace(warm);
                    #endif
                    c=lerp(c,warm,_Sepia*strength);
                }
                c*=1-strength*(_Scan*step(.5,frac((pix.y+tick)*.5)) +
                    smoothstep(.3,.72,length(uv-.5))*_Vignette);
                float grain=step(.9985,n)*_Grain*(noise(pix.yx-tick)-.35);
                float roll=saturate(1-abs(frac(uv.y-_SignalTime*.30)-.5)*18)*_Streak*.06;
                c+=strength*(grain+roll);
                if(min(i.uv.y,1-i.uv.y)<_Bars) c*=1-strength;
                return fixed4(c,1);
            }
            ENDCG
        }
    }
}
