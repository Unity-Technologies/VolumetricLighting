half ShadowPlane(half3 worldPos, half4 plane, half feather)
{
	half x = plane.w - dot(worldPos, plane.xyz);
	// Compiler bug workaround
	x += 0.0001;
	return smoothstep(-feather, feather, x);
}

float4 _ShadowPlane0;
float _ShadowPlaneFeather0;
float4 _ShadowPlane1;
float _ShadowPlaneFeather1;

half ShadowPlanes(half3 worldPos)
{
	half att = 1;
	att *= ShadowPlane(worldPos, _ShadowPlane0, _ShadowPlaneFeather0);
	att *= ShadowPlane(worldPos, _ShadowPlane1, _ShadowPlaneFeather1);
	return att;
}

struct TubeLightShadowPlane
{
	float4 plane0;
	float4 plane1;
	float feather0;
	float feather1;
	float padding0;
	float padding1;
};

half ShadowPlanes(half3 worldPos, TubeLightShadowPlane params)
{
	half att = 1;
	att *= ShadowPlane(worldPos, params.plane0, params.feather0);
	att *= ShadowPlane(worldPos, params.plane1, params.feather1);
	return att;
}