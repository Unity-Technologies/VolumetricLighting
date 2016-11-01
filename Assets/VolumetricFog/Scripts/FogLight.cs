using UnityEngine;

[ExecuteInEditMode]
public partial class FogLight : LightOverride
{
	public bool m_ForceOnForFog = false;
	[Tooltip("Only one shadowed fog AreaLight at a time.")]
	[Header("Shadows")]
	public bool m_Shadows = false;

	public enum TextureSize
	{
		x256 = 256,
		x512 = 512,
		x1024 = 1024,
	}

	[Tooltip("Always at most half the res of the AreaLight's shadowmap.")]
	public TextureSize m_ShadowmapRes = TextureSize.x256;
	[Range(0, 3)]
	public int m_BlurIterations = 0;
	[MinValue(0)]
	public float m_BlurSize = 1.0f;
	[MinValue(0)]
	[Tooltip("Affects shadow softness.")]
	public float m_ESMExponent = 40.0f;

	public bool m_Bounded = true;

	public override bool GetForceOn()
	{
		return m_ForceOnForFog;
	}

	bool m_AddedToLightManager = false;

	void AddToLightManager()
	{
		if (!m_AddedToLightManager)
			m_AddedToLightManager = LightManagerFogLights.Add(this);
	}

	void OnEnable()
	{
		AddToLightManager();
	}

	void Update()
	{
		// LightManager might not have been available during this light's OnEnable(), so keep trying.
		AddToLightManager();
	}

	void OnDisable()
	{
		LightManagerFogLights.Remove(this);
		m_AddedToLightManager = false;
		CleanupDirectionalShadowmap();
	}
}
