cbuffer POISSON_DISKS {
	static half2 poisson[40] = {
		half2(0.02971195f, 0.8905211f),
		half2(0.2495298f, 0.732075f),
		half2(-0.3469206f, 0.6437836f),
		half2(-0.01878909f, 0.4827394f),
		half2(-0.2725213f, 0.896188f),
		half2(-0.6814336f, 0.6480481f),
		half2(0.4152045f, 0.2794172f),
		half2(0.1310554f, 0.2675925f),
		half2(0.5344744f, 0.5624411f),
		half2(0.8385689f, 0.5137348f),
		half2(0.6045052f, 0.08393857f),
		half2(0.4643163f, 0.8684642f),
		half2(0.335507f, -0.110113f),
		half2(0.03007669f, -0.0007075319f),
		half2(0.8077537f, 0.2551664f),
		half2(-0.1521498f, 0.2429521f),
		half2(-0.2997617f, 0.0234927f),
		half2(0.2587779f, -0.4226915f),
		half2(-0.01448214f, -0.2720358f),
		half2(-0.3937779f, -0.228529f),
		half2(-0.7833176f, 0.1737299f),
		half2(-0.4447537f, 0.2582748f),
		half2(-0.9030743f, 0.406874f),
		half2(-0.729588f, -0.2115215f),
		half2(-0.5383645f, -0.6681151f),
		half2(-0.07709587f, -0.5395499f),
		half2(-0.3402214f, -0.4782109f),
		half2(-0.5580465f, 0.01399586f),
		half2(-0.105644f, -0.9191031f),
		half2(-0.8343651f, -0.4750755f),
		half2(-0.9959937f, -0.0540134f),
		half2(0.1747736f, -0.936202f),
		half2(-0.3642297f, -0.926432f),
		half2(0.1719682f, -0.6798802f),
		half2(0.4424475f, -0.7744268f),
		half2(0.6849481f, -0.3031401f),
		half2(0.5453879f, -0.5152272f),
		half2(0.9634013f, -0.2050581f),
		half2(0.9907925f, 0.08320642f),
		half2(0.8386722f, -0.5428791f)
	};
};

Texture2D _Shadowmap;
SamplerComparisonState sampler_Shadowmap;

// To get a sampler, which doesn't do comparison
Texture2D _ShadowmapDummy;
SamplerState sampler_ShadowmapDummy;

half _ShadowReceiverWidth;
half _ShadowReceiverDistanceScale;
half2 _ShadowLightWidth;
half _ShadowBias;
float4x4 _ShadowProjectionMatrix;

half EdgeSmooth(half2 xy)
{
	// Magic tweaks to the shape
	float corner = 0.4;
	float outset = 1.0;
	float smooth = 0.5;

	float d = length(max(abs(xy) - 1 + corner*outset, 0.0)) - corner;
	return saturate(1 - smoothstep(-smooth, 0, d));	
}

half ReverseZ(half z)
{
#if UNITY_REVERSED_Z
	return 1.0 - z;
#endif
	return z;
}

half Shadow(half3 position)
{
	half4 pClip = mul(_ShadowProjectionMatrix, half4(position, 1));
	half3 p = pClip.xyz/pClip.w;
	if (any(step(1.0, abs(p.xy))))
		return 0;

	// The texture contains just 0. But we need to sample it somewhere for Unity to initialize the corresponding sampler.
	float dist = _ShadowmapDummy.Sample(sampler_ShadowmapDummy, 0).a;

	half edgeSmooth = EdgeSmooth(p.xy);
	p = p * 0.5 + 0.5;

	for(int j = 0; j < 10; ++j)
	{
		half2 offset = poisson[j + 24] * _ShadowReceiverWidth;
		float depth = ReverseZ(_Shadowmap.SampleLevel(sampler_ShadowmapDummy, p.xy + offset, 0).r);

		dist += max(0.0, p.z - depth);
	}

	dist *= _ShadowReceiverDistanceScale;

	p.z -= _ShadowBias/pClip.w;
	p.z = ReverseZ(p.z);
	half shadow = 0;
	for(int i = 0; i < 32; ++i)
	{
		half lightWidth = lerp(_ShadowLightWidth.x, _ShadowLightWidth.y, min(1.0, dist));
		const half2 offset = poisson[i] * lightWidth;
		shadow += _Shadowmap.SampleCmpLevelZero(sampler_Shadowmap, p.xy + offset, p.z);
	} 

	return shadow * edgeSmooth / 32.0;
}
