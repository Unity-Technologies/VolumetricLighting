Shader "Hidden/CopyShadowParams"
{
	SubShader
	{
		Pass
		{
			CGPROGRAM
			#pragma target 5.0
			#pragma only_renderers d3d11
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			struct ShadowParams
			{
				float4x4 worldToShadow[4];
				float4 shadowSplitSpheres[4];
				float4 shadowSplitSqRadii;
			};

			// Hmm, we can't be sure u2 doesn't conflict with other effects.
			RWStructuredBuffer<ShadowParams> _ShadowParams : register(u2);

			float4 vert () : SV_POSITION
			{
				for (int i = 0; i < 4; i++)
				{
					_ShadowParams[0].worldToShadow[i] = unity_WorldToShadow[i];
					_ShadowParams[0].shadowSplitSpheres[i] = unity_ShadowSplitSpheres[i];
				}
				_ShadowParams[0].shadowSplitSqRadii = unity_ShadowSplitSqRadii;

				return float4(0, 0, 0, 1);
			}
			
			fixed4 frag () : SV_Target
			{
				return 0;
			}
			ENDCG
		}
	}
}
