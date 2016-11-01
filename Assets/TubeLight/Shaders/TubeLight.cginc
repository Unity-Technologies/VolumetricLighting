// Based on 'Real Shading in Unreal Engine 4'
// http://blog.selfshadow.com/publications/s2013-shading-course/#course_content

#include "TubeLightAttenuation.cginc"

#ifndef SHADOW_PLANES
#define SHADOW_PLANES 1
#endif

#if SHADOW_PLANES
#include "TubeLightShadowPlanes.cginc"
#endif

#define SHARP_EDGE_FIX 1

void SphereLightToPointLight(float3 pos, float3 lightPos, float3 eyeVec, half3 normal, float sphereRad, half rangeSqInv, inout UnityLight light, out half lightDist)
{
	half3 viewDir = -eyeVec;
	half3 r = reflect (viewDir, normal);

	float3 L = lightPos - pos;
	float3 centerToRay	= dot (L, r) * r - L;
	float3 closestPoint	= L + centerToRay * saturate(sphereRad / length(centerToRay));

	lightDist = length(closestPoint);
	light.dir = closestPoint / lightDist;

	half distLSq = dot(L, L);
	light.ndotl = saturate(dot(normal, L/sqrt(distLSq)));

	float distNorm = distLSq * rangeSqInv;
	float atten = AttenuationToZero(distNorm);
	light.color *= atten;
}

void TubeLightToPointLight(float3 pos, float3 tubeStart, float3 tubeEnd, float3 normal, float tubeRad, float rangeSqInv, float3 representativeDir, float3 lightColor, out float3 outLightColor, out float3 outLightDir, out float outNdotL, out half outLightDist)
{
	half3 N = normal;
	float3 L0 = tubeStart - pos;
	float3 L1 = tubeEnd - pos;
	float L0dotL0 = dot(L0, L0);
	float distL0 = sqrt(L0dotL0);
	float distL1 = length(L1);
	
	float NdotL0 = dot(L0, N) / (2.0 * distL0);
	float NdotL1 = dot(L1, N) / (2.0 * distL1);
	outNdotL = saturate(NdotL0 + NdotL1);
	
	float3 Ldir = L1 - L0;
	float RepdotL0 = dot(representativeDir, L0);
	float RepdotLdir = dot(representativeDir, Ldir);
	float L0dotLdir	= dot(L0, Ldir);
	float LdirdotLdir = dot(Ldir, Ldir);
	float distLdir = sqrt(LdirdotLdir);
	
#if SHARP_EDGE_FIX
	// There's a very visible discontinuity if we just take the closest distance to ray,
	// as the original paper suggests. This is an attempt to fix it, but it gets slightly more expensive and
	// has its own artifact, although this time at least C0 smooth.

	// Smallest angle to ray
	float t = (L0dotLdir * RepdotL0 - L0dotL0 * RepdotLdir) / (L0dotLdir * RepdotLdir - LdirdotLdir * RepdotL0);
	t = saturate(t);

	// As representativeDir becomes parallel (well, in some plane) to Ldir and then points away, t flips from 0 to 1 (or vv) and a discontinuity shows up.
	// Counteract by detecting that relative angle/position and flip t. The discontinuity in t moves to the back side.
	float3 L0xLdir = cross(L0, Ldir);
	float3 LdirxR = cross(Ldir, representativeDir);
	float RepAtLdir = dot(L0xLdir, LdirxR);

	// RepAtLdir is negative if R points away from Ldir.
	// TODO: check if lerp below is indeed cheaper.
	// if (RepAtLdir < 0)
	// 	t = 1 - t;
	t = lerp(1 - t, t, step(0, RepAtLdir));

#else
	// Original by Karis
	// Closest distance to ray
	float t = (RepdotL0 * RepdotLdir - L0dotLdir) / (distLdir * distLdir - RepdotLdir * RepdotLdir);
	t = saturate(t);

#endif

	float3 closestPoint = L0 + Ldir * t;
	float3 centerToRay = dot(closestPoint, representativeDir) * representativeDir - closestPoint;

	closestPoint = closestPoint + centerToRay * saturate(tubeRad / length(centerToRay));

	outLightDist = length(closestPoint);
	outLightDir = closestPoint / outLightDist;

	float distNorm = 0.5f * (distL0 * distL1 + dot(L0, L1)) * rangeSqInv;
	outLightColor = lightColor * AttenuationToZero(distNorm);
}

void TubeLightToPointLight(float3 pos, float3 tubeStart, float3 tubeEnd, float3 eyeVec, float3 normal, float tubeRad, float rangeSqInv, float3 lightColor, out float3 outLightColor, out float3 outLightDir, out float outNdotL, out half outLightDist)
{
	half3 viewDir = -eyeVec;
	half3 representativeDir = reflect (viewDir, normal);

	TubeLightToPointLight(pos, tubeStart, tubeEnd, normal, tubeRad, rangeSqInv, representativeDir, lightColor, outLightColor, outLightDir, outNdotL, outLightDist);
}

inline half GGXTerm_Area (half NdotH, half roughness, half lightDist, half lightRadius)
{
	half a = roughness * roughness;
	half a2 = a * a;
	half d = NdotH * NdotH * (a2 - 1.f) + 1.f;
	d = max(d, 0.000001);

	half aP = saturate( lightRadius / (lightDist*2.0) + a);
	half aP2 = aP * aP;

	return a2 * a2 / (UNITY_PI * d * d * aP2);
}

half4 BRDF1_Unity_PBS_Area (half3 diffColor, half3 specColor, half oneMinusReflectivity, half oneMinusRoughness,
	half3 normal, half3 viewDir,
	UnityLight light, UnityIndirect gi, half lightDist, half lightRadius)
{
	half roughness = 1-oneMinusRoughness;
	half3 halfDir = Unity_SafeNormalize (light.dir + viewDir);

	half nl = light.ndotl;
	half nh = BlinnTerm (normal, halfDir);
	half nv = DotClamped (normal, viewDir);
	half lv = DotClamped (light.dir, viewDir);
	half lh = DotClamped (light.dir, halfDir);

	half V = SmithGGXVisibilityTerm (nl, nv, roughness);
	half D = GGXTerm_Area (nh, roughness, lightDist, lightRadius);

	half nlPow5 = Pow5 (1-nl);
	half nvPow5 = Pow5 (1-nv);
	half Fd90 = 0.5 + 2 * lh * lh * roughness;
	half disneyDiffuse = (1 + (Fd90-1) * nlPow5) * (1 + (Fd90-1) * nvPow5);
	
	// HACK: theoretically we should divide by Pi diffuseTerm and not multiply specularTerm!
	// BUT 1) that will make shader look significantly darker than Legacy ones
	// and 2) on engine side "Non-important" lights have to be divided by Pi to in cases when they are injected into ambient SH
	// NOTE: multiplication by Pi is part of single constant together with 1/4 now

	half specularTerm = (V * D) * (UNITY_PI/4); // Torrance-Sparrow model, Fresnel is applied later (for optimization reasons)
	if (IsGammaSpace())
		specularTerm = sqrt(max(1e-4h, specularTerm));
	specularTerm = max(0, specularTerm * nl);
	half diffuseTerm = disneyDiffuse * nl;
	
	half grazingTerm = saturate(oneMinusRoughness + (1-oneMinusReflectivity));
    half3 color =	diffColor * (gi.diffuse + light.color * diffuseTerm)
                    + specularTerm * light.color * FresnelTerm (specColor, lh)
					+ gi.specular * FresnelLerp (specColor, grazingTerm, nv);

	return half4(color, 1);
}

half4 CalculateLight (float3 worldPos, float2 uv, half3 baseColor, half3 specColor, half oneMinusRoughness, half3 normalWorld,
	half3 lightStart, half3 lightEnd, half3 lightColor, half lightRadius, half lightRangeSqInv)
{
	UnityLight light = (UnityLight)0;

	float3 eyeVec = normalize(worldPos - _WorldSpaceCameraPos);
	half lightDist = 0;

#if 0
	// Can't use a keyword, because no keywords in material property blocks.
	// TODO: is it worth the dynamic branch?
	if (sphereLight)
		SphereLightToPointLight (worldPos, lightStart, eyeVec, normalWorld, lightRadius, lightRangeSqInv, light, lightDist);
	else
#else
		TubeLightToPointLight (worldPos, lightStart, lightEnd, eyeVec, normalWorld, lightRadius, lightRangeSqInv, lightColor, light.color, light.dir, light.ndotl, lightDist);
#endif

#if SHADOW_PLANES
	light.color *= ShadowPlanes(worldPos);
#endif

	half oneMinusReflectivity = 1 - SpecularStrength(specColor.rgb);
	
	UnityIndirect ind;
	UNITY_INITIALIZE_OUTPUT(UnityIndirect, ind);
	ind.diffuse = 0;
	ind.specular = 0;

    half4 res = BRDF1_Unity_PBS_Area (baseColor, specColor, oneMinusReflectivity, oneMinusRoughness, normalWorld, -eyeVec, light, ind, lightDist, lightRadius);
	return res;
}
