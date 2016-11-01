Shader "Hidden/ApplyToOpaque" {
SubShader {
Pass {
	ZTest Always Cull Off ZWrite Off
	Blend Off

CGPROGRAM
	#pragma target 3.0
	#include "UnityCG.cginc"
	#include "VolumetricFog.cginc"
	#pragma vertex vert
	#pragma fragment frag

	sampler2D _CameraDepthTexture;
	sampler2D _MainTex;

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
		half linear01Depth = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv));
		half4 fog = Fog(linear01Depth, i.uv);
		return tex2D(_MainTex, i.uv) * fog.a + fog;
	}

ENDCG
}
}
}
