using UnityEngine;

[ExecuteInEditMode]
public class FogEllipsoid : MonoBehaviour
{
	public enum Blend
	{
		Additive,
		Multiplicative
	}

	public Blend m_Blend = Blend.Additive;
	public float m_Density = 1.0f;
	[MinValue(0)]
	public float m_Radius = 1.0f;
	[MinValue(0)]
	public float m_Stretch = 2.0f;
	[Range(0, 1)]
	public float m_Feather = 0.7f;
	[Range(0, 1)]
	public float m_NoiseAmount = 0.0f;
	public float m_NoiseSpeed = 1.0f;
	[MinValue(0)]
	public float m_NoiseScale = 1.0f;

	bool m_AddedToLightManager = false;

	void AddToLightManager()
	{
		if (!m_AddedToLightManager)
			m_AddedToLightManager = LightManagerFogEllipsoids.Add(this);
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
		LightManagerFogEllipsoids.Remove(this);
		m_AddedToLightManager = false;
	}	

	void OnDrawGizmosSelected()
	{
		Matrix4x4 m = Matrix4x4.identity;
		Transform t = transform;
		m.SetTRS(t.position, t.rotation, new Vector3(1.0f, m_Stretch, 1.0f));
		Gizmos.matrix =  m;
		Gizmos.DrawWireSphere(Vector3.zero, m_Radius);
	}
}
