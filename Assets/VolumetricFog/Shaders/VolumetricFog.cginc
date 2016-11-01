sampler3D _VolumeScatter;
float4 _VolumeScatter_TexelSize;
float4 _Screen_TexelSize;
float _CameraFarOverMaxFar;
float _NearOverFarClip;

int ihash(int n)
{
	n = (n<<13)^n;
	return (n*(n*n*15731+789221)+1376312589) & 2147483647;
}

float frand(int n)
{
	return ihash(n) / 2147483647.0;
}

float2 cellNoise(int2 p)
{
	int i = p.y*256 + p.x;
	return float2(frand(i), frand(i + 57)) - 0.5;//*2.0-1.0;
}

half4 Fog(half linear01Depth, half2 screenuv)
{
	half z = linear01Depth * _CameraFarOverMaxFar;
	z = (z - _NearOverFarClip) / (1 - _NearOverFarClip);
	if (z < 0.0)
		return half4(0, 0, 0, 1);

	half3 uvw = half3(screenuv.x, screenuv.y, z);
	uvw.xy += cellNoise(uvw.xy * _Screen_TexelSize.zw) * _VolumeScatter_TexelSize.xy * 0.8;
	return tex3D(_VolumeScatter, uvw);
}
