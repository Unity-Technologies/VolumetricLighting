Shader "Custom/StandardAlphaBlended-VolumetricFog"
{
	Properties
	{
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Metallic ("Metallic", Range(0,1)) = 0.0
	}
	SubShader
	{
		Tags {"Queue" = "Transparent" "RenderType"="Transparent" }
		LOD 200
   
		CGPROGRAM
 
		#pragma surface surf Standard fullforwardshadows alpha finalcolor:ApplyFog
		#pragma target 3.0
		#pragma multi_compile _ VOLUMETRIC_FOG

		#if VOLUMETRIC_FOG
			#include "../../VolumetricFog/Shaders/VolumetricFog.cginc"
		#endif
 
		sampler2D _MainTex;
		sampler2D _CameraDepthTexture;
 
		struct Input {
			float2 uv_MainTex;
			float4 screenPos;
		};
 
		void ApplyFog(Input IN, SurfaceOutputStandard o, inout fixed4 color)
		{
			#if VOLUMETRIC_FOG
				half3 uvscreen = IN.screenPos.xyz/IN.screenPos.w;
				half linear01Depth = Linear01Depth(uvscreen.z);
				fixed4 fog = Fog(linear01Depth, uvscreen.xy);

				// Always apply fog attenuation - also in the forward add pass.
				color.rgb *= fog.a;

				// Alpha premultiply mode (used with alpha and Standard lighting function, or explicitly alpha:premul)
				// uses source blend factor of One instead of SrcAlpha. `color` is compensated for it, so we need to compensate
				// the amount of inscattering too. A note on why this works: below.
				#if _ALPHAPREMULTIPLY_ON
					fog.rgb *= o.Alpha;
				#endif

				// Add inscattering only once, so in forward base, but not forward add.
				#ifndef UNITY_PASS_FORWARDADD
					color.rgb += fog.rgb;
				#endif

				// So why does multiplying the inscattered light by alpha work?
				// In other words: how did fog ever work, if opaque objects add all of the inscattered light
				// between them and the camera, and then the transparencies add even more?
				//
				// This is our scene initially:
				// scene |---is0---------------------------------------> camera
				//
				// And that's with the transparent object added in between the opaque stuff and the camera:
				// scene |---is1---> transparent |---is2---------------> camera
				//
				// When rendering, we start with the opaque part of the scene and add all the light inscattered between that and the camera: is0.
				// Then we add the transparent object. It does two things (let's consider the alpha premultiply version):
				// - Dims whatever was behind it (including is0) by OneMinusSrcAlpha
				// - Adds light inscattered in front of it (is2), multiplied by Alpha
				//
				// So all in all we end up with this much inscattered light:
				// is0 * OneMinusSrcAlpha + is2 * Alpha
				//
				// Judging by the diagram, though, the correct amount should be:
				// is1 * OneMinusSrcAlpha + is2
				//
				// Turns out the two expressions are equal - who would've thunk?
				// is1 = is0 - is2
				// (is0 - is2) * OneMinusSrcAlpha + is2
				// is0 * OneMinusSrcAlpha - is2 * (1 - Alpha) + is2
				// is0 * OneMinusSrcAlpha - is2 + is2 * Alpha + is2
				// is0 * OneMinusSrcAlpha + is2 * Alpha

				// I leave figuring out if the fog attenuation is correct as an exercise to the reader ;)
			#endif
		}

		half _Glossiness;
		half _Metallic;
		fixed4 _Color;
 
		void surf (Input IN, inout SurfaceOutputStandard o)
		{
			fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
			o.Albedo = c.rgb;
			o.Metallic = _Metallic;
			o.Smoothness = _Glossiness;
			o.Alpha = c.a;
		}
		ENDCG
	}
	FallBack "Standard"
}