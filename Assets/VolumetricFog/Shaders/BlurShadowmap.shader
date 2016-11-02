
Shader "Hidden/BlurShadowmap" {
	Properties
	{
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_Bloom ("Bloom (RGB)", 2D) = "black" {}
	}
	
	CGINCLUDE
	#include "UnityCG.cginc"

	half4 _TexelSize;

	struct v2f_tap
	{
		float4 pos : SV_POSITION;
		half2 uv20 : TEXCOORD0;
		half2 uv21 : TEXCOORD1;
		half2 uv22 : TEXCOORD2;
		half2 uv23 : TEXCOORD3;
	};			

	v2f_tap vert4Tap ( appdata_img v )
	{
		v2f_tap o;
		o.pos = v.vertex;

		o.uv20 = v.texcoord + _TexelSize.xy;				
		o.uv21 = v.texcoord + _TexelSize.xy * half2(-0.5, -0.5);	
		o.uv22 = v.texcoord + _TexelSize.xy * half2( 0.5, -0.5);		
		o.uv23 = v.texcoord + _TexelSize.xy * half2(-0.5,  0.5);		

		return o; 
	}

	// TODO: consolidate with the above, but make sure both area and dir shadows work
	v2f_tap vert4TapDir ( appdata_img v )
	{
		v2f_tap o;
		o.pos = UnityObjectToClipPos(v.vertex);

		o.uv20 = v.texcoord + _TexelSize.xy;				
		o.uv21 = v.texcoord + _TexelSize.xy * half2(-0.5, -0.5);	
		o.uv22 = v.texcoord + _TexelSize.xy * half2( 0.5, -0.5);		
		o.uv23 = v.texcoord + _TexelSize.xy * half2(-0.5,  0.5);		

		return o; 
	}

	float4 _ZParams;
	float _ESMExponent;
	Texture2D _Shadowmap;
	SamplerComparisonState sampler_Shadowmap;

	// To get a sampler, which doesn't do comparison
	Texture2D _ShadowmapDummy;
	SamplerState sampler_ShadowmapDummy;

	#define VSM 1

	float4 fragDownsampleFromShadowmapFormat ( v2f_tap i ) : SV_Target
	{
		float4 z;
		z.r = _Shadowmap.Sample(sampler_ShadowmapDummy, i.uv20).r;
		z.g = _Shadowmap.Sample(sampler_ShadowmapDummy, i.uv21).r;
		z.b = _Shadowmap.Sample(sampler_ShadowmapDummy, i.uv22).r;
		z.a = _Shadowmap.Sample(sampler_ShadowmapDummy, i.uv23).r;

		// The texture contains just 0. But we need to sample it somewhere for Unity to initialize the corresponding sampler.
		z.r += _ShadowmapDummy.Sample(sampler_ShadowmapDummy, 0).a;

		#if UNITY_REVERSED_Z
			z = 1.0 - z;
		#endif

		// Transform to linear z, 0 at near, 1 at far
		z = z * 2 - 1;
		z = _ZParams.x * (z + 1.0) / (z + _ZParams.y);	

	#if VSM
		// TODO: this is wrong. We can't average/blur z values before converting to VSM.
		// This doesn't affect m, but affects m * m, so I should swap those two lines.
		float m = dot(z, 0.25);
		return float4(m, m * m, 0, 0);
	#else
		z = exp(_ESMExponent * z);
		return dot(z, 0.25);
	#endif
	}

	sampler2D _DirShadowmap;

	float4 fragDownsampleFromShadowmapFormatDir ( v2f_tap i ) : SV_Target
	{
		float4 z;
		z.r = tex2D (_DirShadowmap, i.uv20).r;
		z.g = tex2D (_DirShadowmap, i.uv21).r;
		z.b = tex2D (_DirShadowmap, i.uv22).r;
		z.a = tex2D (_DirShadowmap, i.uv23).r;

		return z.r;

		// Transform to linear z, 0 at near, 1 at far
		// z = z * 2 - 1;
		// z = _ZParams.x * (z + 1.0) / (z + _ZParams.y);	

	#if 1
		// float m = dot(z, 0.25);
		// return float4(m, m * m, 0, 0);
		float4 z2 = z * z;
		return float4(dot(z, 0.25), dot(z2, 0.25), 0, 0);
	#else
		//z = exp(_ESMExponent * z);
		z = exp(40.0 * z);
		return dot(z, 0.25);
	#endif
	}

	sampler2D _MainTex;

	float4 fragDownsample ( v2f_tap i ) : SV_Target
	{		
		float4 color = tex2D (_MainTex, i.uv20);
		color += tex2D (_MainTex, i.uv21);
		color += tex2D (_MainTex, i.uv22);
		color += tex2D (_MainTex, i.uv23);
		return color * 0.25;
	}

	struct v2f
	{
		float4 pos : SV_POSITION;
		half4 uv : TEXCOORD0;
		half2 offs : TEXCOORD1;
	};

	float _BlurSize;

	v2f vertBlurHorizontal (appdata_img v)
	{
		v2f o;
		o.pos = v.vertex;
		
		o.uv = half4(v.texcoord.xy,1,1);
		o.offs = _TexelSize.xy * half2(1.0, 0.0) * _BlurSize;

		return o; 
	}
	
	v2f vertBlurVertical (appdata_img v)
	{
		v2f o;
		o.pos = v.vertex;
		
		o.uv = half4(v.texcoord.xy, 1, 1);
		o.offs = _TexelSize.xy * half2(0.0, 1.0) * _BlurSize;
		 
		return o; 
	}	

	float4 fragBlur8 (v2f i) : SV_Target
	{
		half2 coords = i.uv.xy - i.offs * 5.0;  
		
		float4 color = 0;
		for(int k = 0; k < 11; k++)  
		{
			color += tex2D(_MainTex, coords);
			coords += i.offs;
		}
		return color/11.0;
	}	
					
	ENDCG
	
	SubShader {
	  ZTest Off Cull Off ZWrite Off Blend Off

	// 0
	Pass 
	{ 
		CGPROGRAM
		#pragma vertex vert4Tap
		#pragma fragment fragDownsampleFromShadowmapFormat
		ENDCG	 
	}

	// 1
	Pass 
	{ 
		CGPROGRAM
		#pragma vertex vert4Tap
		#pragma fragment fragDownsample
		ENDCG	 
	}

	// 2
	Pass {
		ZTest Always
		Cull Off
		
		CGPROGRAM 
		
		#pragma vertex vertBlurVertical
		#pragma fragment fragBlur8
		
		ENDCG 
		}	
		
	// 3
	Pass {		
		ZTest Always
		Cull Off
				
		CGPROGRAM
		
		#pragma vertex vertBlurHorizontal
		#pragma fragment fragBlur8
		
		ENDCG
		}

	// 4
	Pass 
	{ 
		CGPROGRAM
		#pragma vertex vert4TapDir
		#pragma fragment fragDownsampleFromShadowmapFormatDir
		ENDCG	 
	}

	// 5
	Pass 
	{ 
		CGPROGRAM
		#pragma vertex vert4TapDir
		#pragma fragment fragDownsample
		ENDCG	 
	}

	}	

	FallBack Off
}
