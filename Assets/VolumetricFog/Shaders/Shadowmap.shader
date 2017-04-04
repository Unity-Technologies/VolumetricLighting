Shader "Hidden/Shadowmap" {
SubShader {
    Tags { "RenderType"="Opaque" }
Pass
{
    Fog { Mode Off }
    Cull Back
    CGPROGRAM
    #pragma vertex vert
    #pragma fragment frag
    #include "UnityCG.cginc"

    struct v2f
    {
        float4 pos : SV_POSITION;
    };

    v2f vert (appdata_base v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos (v.vertex);
        return o;
    }

    float4 frag(v2f i) : COLOR
    {
        return 0;
    }
    ENDCG
}
}
}