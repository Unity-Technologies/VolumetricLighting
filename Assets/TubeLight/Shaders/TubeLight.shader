Shader "Hidden/TubeLight" {
SubShader {
	Tags { "Queue"="Geometry-1" }

CGINCLUDE
#include "UnityCG.cginc"
#include "UnityPBSLighting.cginc"
#include "UnityDeferredLibrary.cginc"

#define SHADOW_PLANES 1
#include "TubeLight.cginc"

sampler2D _CameraGBufferTexture0;
sampler2D _CameraGBufferTexture1;
sampler2D _CameraGBufferTexture2;

float _LightRadius;
float _LightLength;
float4 _LightAxis;


void DeferredCalculateLightParams (
	unity_v2f_deferred i,
	out float3 outWorldPos,
	out float2 outUV)
{
	i.ray = i.ray * (_ProjectionParams.z / i.ray.z);
	float2 uv = i.uv.xy / i.uv.w;
	
	// read depth and reconstruct world position
	float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv);
	depth = Linear01Depth (depth);
	float4 vpos = float4(i.ray * depth,1);
	float3 wpos = mul (unity_CameraToWorld, vpos).xyz;

	outWorldPos = wpos;
	outUV = uv;
}

half4 CalculateLightDeferred (unity_v2f_deferred i)
{
	float3 worldPos;
	float2 uv;
	DeferredCalculateLightParams (i, worldPos, uv);

	half4 gbuffer0 = tex2D (_CameraGBufferTexture0, uv);
	half4 gbuffer1 = tex2D (_CameraGBufferTexture1, uv);
	half4 gbuffer2 = tex2D (_CameraGBufferTexture2, uv);

	half3 baseColor = gbuffer0.rgb;
	half3 specColor = gbuffer1.rgb;
	half oneMinusRoughness = gbuffer1.a;
	half3 normalWorld = gbuffer2.rgb * 2 - 1;
	normalWorld = normalize(normalWorld);
	
	return CalculateLight (worldPos, uv, baseColor, specColor, oneMinusRoughness, normalWorld,
		_LightPos.xyz, _LightPos.xyz + _LightAxis.xyz * _LightLength, _LightColor.xyz, _LightRadius, _LightPos.w);
}
ENDCG

Pass {
	Fog { Mode Off }
	ZWrite Off
	Blend One One
	Cull Front
	ZTest Always

	
CGPROGRAM
#pragma target 3.0
#pragma vertex vert_deferred
#pragma fragment frag
#pragma exclude_renderers nomrt

fixed4 frag (unity_v2f_deferred i) : SV_Target
{
	half4 light = CalculateLightDeferred(i);
	// TODO: squash those NaNs at their source
	return isnan(light) ? 0 : light;
}

ENDCG
}

}
Fallback Off
}
