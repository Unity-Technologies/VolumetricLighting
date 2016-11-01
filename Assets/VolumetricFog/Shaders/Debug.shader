Shader "Hidden/Debug" {
SubShader {
Pass {
	ZTest Always Cull Off ZWrite Off
	Blend Off

CGPROGRAM
	#pragma target 3.0
	#include "UnityCG.cginc"
	#pragma vertex vert
	#pragma fragment frag

	sampler2D _CameraDepthTexture;
	sampler3D _VolumeInject;
	sampler3D _VolumeScatter;
	sampler2D _Shadowmap;
	sampler2D _ShadowmapBlurred;
	sampler2D _MainTex;
	sampler2D _BoxLightShadowmap;
	float _Z;

	struct v2f
	{
		float4 pos : SV_POSITION;
		float2 uv : TEXCOORD0;
	};

	v2f vert (appdata_img v)
	{
		v2f o;
		o.pos = v.vertex;
		o.pos.xy = o.pos.xy * 2 - 1;
		o.uv = v.texcoord.xy;
		
		#if UNITY_UV_STARTS_AT_TOP
		if (_ProjectionParams.x < 0)
			o.uv.y = 1-o.uv.y;
		#endif					
		
		return o;
	}
		
	half4 frag (v2f i) : SV_Target
	{
		half depth = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv));
		
		// return i.uv.xyyy;
		return tex2D(_BoxLightShadowmap, i.uv);
		//return log(tex2D(_ShadowmapBlurred, i.uv))/80.0;
		return tex3D(_VolumeInject, half3(i.uv.x, i.uv.y, _Z)).a;// * tex2D(_MainTex, float2(i.uv.x, i.uv.y));
	}

ENDCG
}
}
}
