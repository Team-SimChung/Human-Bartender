// TextMeshPro SDF 글자용 글리치 셰이더. TMP_SDF-Mobile을 바탕으로 RGB 분리·가로 찢김·스캔라인을 더했다.
// - 평소에는 쓰지 않는다. UIChoiceGlitch가 홀드/포커스 동안만 글자 머티리얼을 이 셰이더의 사본으로 바꾼다.
// - 타이밍은 셰이더가 정하지 않는다. 스크립트가 ChoiceGlitchSettings 타이밍으로 매 프레임
//   _GlitchStrength(쉬는 프레임은 0)와 _GlitchFrame(패턴 번호)을 넣는다. 0이면 TMP 기본 글자와 똑같이 그린다.
// - 나머지 _Glitch* 값은 템플릿 머티리얼(ChoiceGlitchSettings의 Text Glitch Material)에서 조절하고,
//   스크립트가 _Glitch 로 시작하는 속성을 전부 사본에 복사한다. 새 값도 _Glitch 접두사만 붙이면 된다.
// - 언더레이(그림자)는 지원하지 않는다. 아웃라인은 지원한다.
Shader "Custom/TMPGlitch" {

Properties {
	[Header(Glitch)]
	_GlitchStrength     ("Strength (script)", Range(0,1)) = 0
	_GlitchFrame        ("Pattern Frame (script)", Float) = 0
	_GlitchRGBSplit     ("RGB Split (atlas px)", Range(0,5)) = 2.5
	_GlitchSliceHeight  ("Slice Height (screen px)", Range(1,32)) = 3
	_GlitchSliceShift   ("Slice Shift (atlas px)", Range(0,5)) = 3
	_GlitchSliceDensity ("Slice Ratio", Range(0,1)) = 0.35
	_GlitchScanline     ("Scanline Strength", Range(0,1)) = 0.25

	[Header(TextMeshPro)]
	_FaceColor          ("Face Color", Color) = (1,1,1,1)
	_FaceDilate         ("Face Dilate", Range(-1,1)) = 0

	_OutlineColor       ("Outline Color", Color) = (0,0,0,1)
	_OutlineWidth       ("Outline Thickness", Range(0,1)) = 0
	_OutlineSoftness    ("Outline Softness", Range(0,1)) = 0

	_WeightNormal       ("Weight Normal", float) = 0
	_WeightBold         ("Weight Bold", float) = .5

	_ShaderFlags        ("Flags", float) = 0
	_ScaleRatioA        ("Scale RatioA", float) = 1
	_ScaleRatioB        ("Scale RatioB", float) = 1
	_ScaleRatioC        ("Scale RatioC", float) = 1

	_MainTex            ("Font Atlas", 2D) = "white" {}
	_TextureWidth       ("Texture Width", float) = 512
	_TextureHeight      ("Texture Height", float) = 512
	_GradientScale      ("Gradient Scale", float) = 5
	_ScaleX             ("Scale X", float) = 1
	_ScaleY             ("Scale Y", float) = 1
	_PerspectiveFilter  ("Perspective Correction", Range(0, 1)) = 0.875
	_Sharpness          ("Sharpness", Range(-1,1)) = 0

	_VertexOffsetX      ("Vertex OffsetX", float) = 0
	_VertexOffsetY      ("Vertex OffsetY", float) = 0

	_ClipRect           ("Clip Rect", vector) = (-32767, -32767, 32767, 32767)
	_MaskSoftnessX      ("Mask SoftnessX", float) = 0
	_MaskSoftnessY      ("Mask SoftnessY", float) = 0

	_StencilComp        ("Stencil Comparison", Float) = 8
	_Stencil            ("Stencil ID", Float) = 0
	_StencilOp          ("Stencil Operation", Float) = 0
	_StencilWriteMask   ("Stencil Write Mask", Float) = 255
	_StencilReadMask    ("Stencil Read Mask", Float) = 255

	_CullMode           ("Cull Mode", Float) = 0
	_ColorMask          ("Color Mask", Float) = 15
}

SubShader {
	Tags
	{
		"Queue"="Transparent"
		"IgnoreProjector"="True"
		"RenderType"="Transparent"
	}

	Stencil
	{
		Ref [_Stencil]
		Comp [_StencilComp]
		Pass [_StencilOp]
		ReadMask [_StencilReadMask]
		WriteMask [_StencilWriteMask]
	}

	Cull [_CullMode]
	ZWrite Off
	Lighting Off
	Fog { Mode Off }
	ZTest [unity_GUIZTestMode]
	Blend One OneMinusSrcAlpha
	ColorMask [_ColorMask]

	Pass {
		CGPROGRAM
		#pragma vertex VertShader
		#pragma fragment PixShader
		#pragma shader_feature __ OUTLINE_ON

		#pragma multi_compile __ UNITY_UI_CLIP_RECT
		#pragma multi_compile __ UNITY_UI_ALPHACLIP

		#include "UnityCG.cginc"
		#include "UnityUI.cginc"
		#include "Assets/TextMesh Pro/Shaders/TMPro_Properties.cginc"

		float _GlitchStrength;
		float _GlitchFrame;
		float _GlitchRGBSplit;
		float _GlitchSliceHeight;
		float _GlitchSliceShift;
		float _GlitchSliceDensity;
		float _GlitchScanline;

		struct vertex_t {
			UNITY_VERTEX_INPUT_INSTANCE_ID
			float4	vertex			: POSITION;
			float3	normal			: NORMAL;
			fixed4	color			: COLOR;
			float4	texcoord0		: TEXCOORD0;
			float2	texcoord1		: TEXCOORD1;
		};

		struct pixel_t {
			UNITY_VERTEX_INPUT_INSTANCE_ID
			UNITY_VERTEX_OUTPUT_STEREO
			float4	vertex			: SV_POSITION;
			fixed4	faceColor		: COLOR;
			fixed4	outlineColor	: COLOR1;
			float4	texcoord0		: TEXCOORD0;			// Texture UV, Mask UV
			half4	param			: TEXCOORD1;			// Scale(x), BiasIn(y), BiasOut(z), Bias(w)
			half4	mask			: TEXCOORD2;			// Position in clip space(xy), Softness(zw)
		};

		float _UIMaskSoftnessX;
		float _UIMaskSoftnessY;
		int _UIVertexColorAlwaysGammaSpace;

		pixel_t VertShader(vertex_t input)
		{
			pixel_t output;

			UNITY_INITIALIZE_OUTPUT(pixel_t, output);
			UNITY_SETUP_INSTANCE_ID(input);
			UNITY_TRANSFER_INSTANCE_ID(input, output);
			UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

			float bold = step(input.texcoord0.w, 0);

			float4 vert = input.vertex;
			vert.x += _VertexOffsetX;
			vert.y += _VertexOffsetY;
			float4 vPosition = UnityObjectToClipPos(vert);

			float2 pixelSize = vPosition.w;
			pixelSize /= float2(_ScaleX, _ScaleY) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

			float scale = rsqrt(dot(pixelSize, pixelSize));
			scale *= abs(input.texcoord0.w) * _GradientScale * (_Sharpness + 1);
			if(UNITY_MATRIX_P[3][3] == 0) scale = lerp(abs(scale) * (1 - _PerspectiveFilter), scale, abs(dot(UnityObjectToWorldNormal(input.normal.xyz), normalize(WorldSpaceViewDir(vert)))));

			float weight = lerp(_WeightNormal, _WeightBold, bold) / 4.0;
			weight = (weight + _FaceDilate) * _ScaleRatioA * 0.5;

			scale /= 1 + (_OutlineSoftness * _ScaleRatioA * scale);
			float bias = (0.5 - weight) * scale - 0.5;
			float outline = _OutlineWidth * _ScaleRatioA * 0.5 * scale;

			if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
			{
				input.color.rgb = UIGammaToLinear(input.color.rgb);
			}
			float opacity = input.color.a;

			fixed4 faceColor = fixed4(input.color.rgb, opacity) * _FaceColor;
			faceColor.rgb *= faceColor.a;

			fixed4 outlineColor = _OutlineColor;
			outlineColor.a *= opacity;
			outlineColor.rgb *= outlineColor.a;
			outlineColor = lerp(faceColor, outlineColor, sqrt(min(1.0, (outline * 2))));

			float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
			float2 maskUV = (vert.xy - clampedRect.xy) / (clampedRect.zw - clampedRect.xy);

			output.vertex = vPosition;
			output.faceColor = faceColor;
			output.outlineColor = outlineColor;
			output.texcoord0 = float4(input.texcoord0.x, input.texcoord0.y, maskUV.x, maskUV.y);
			output.param = half4(scale, bias - outline, bias + outline, bias);

			const half2 maskSoftness = half2(max(_UIMaskSoftnessX, _MaskSoftnessX), max(_UIMaskSoftnessY, _MaskSoftnessY));
			output.mask = half4(vert.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * maskSoftness + pixelSize.xy));

			return output;
		}

		float Hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

		// TMP_SDF-Mobile과 같은 방식으로 거리값 하나를 (premultiplied) 색으로 바꾼다.
		half4 Shade(float2 uv, pixel_t input)
		{
			half d = tex2D(_MainTex, uv).a * input.param.x;
			half4 c = input.faceColor * saturate(d - input.param.w);
			#ifdef OUTLINE_ON
			c = lerp(input.outlineColor, input.faceColor, saturate(d - input.param.z));
			c *= saturate(d - input.param.y);
			#endif
			return c;
		}

		fixed4 PixShader(pixel_t input) : SV_Target
		{
			UNITY_SETUP_INSTANCE_ID(input);

			// 켜짐/쉼과 패턴 번호는 스크립트가 정한다(글자 흔들기와 같은 박자).
			float frame = _GlitchFrame;
			float on = _GlitchStrength;

			// 화면 가로 띠마다 일부만 좌우로 찢는다. 단위는 아틀라스 픽셀이라 패딩(보통 5) 안에서 움직인다.
			float band = floor(input.vertex.y / max(_GlitchSliceHeight, 1));
			float sliced = step(Hash(float2(band, frame + 31.7)), _GlitchSliceDensity);
			float shift = (Hash(float2(band, frame)) * 2 - 1) * _GlitchSliceShift * sliced * on;
			float texel = 1.0 / _TextureWidth;
			float2 uv = input.texcoord0.xy + float2(shift * texel, 0);
			float2 split = float2(_GlitchRGBSplit * on * texel, 0);

			half4 r = Shade(uv + split, input);
			half4 g = Shade(uv, input);
			half4 b = Shade(uv - split, input);
			half4 c = half4(r.r, g.g, b.b, max(g.a, max(r.a, b.a)));

			c *= 1 - _GlitchScanline * on * fmod(floor(input.vertex.y), 2);

			#if UNITY_UI_CLIP_RECT
			half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy)) * input.mask.zw);
			c *= m.x * m.y;
			#endif

			#if UNITY_UI_ALPHACLIP
			clip(c.a - 0.001);
			#endif

			return c;
		}
		ENDCG
	}
}
}
