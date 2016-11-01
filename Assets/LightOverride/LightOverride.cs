using UnityEngine;

public abstract class LightOverride : MonoBehaviour
{

	[Header("Overrides")]
	public float m_IntensityMult = 1.0f;
	[MinValue(0.0f)]
	public float m_RangeMult = 1.0f;

	public enum Type{None, Point, Tube, Area, Directional}

	Type m_Type = Type.None;
	bool m_Initialized = false;
	Light m_Light;
	TubeLight m_TubeLight;
	AreaLight m_AreaLight;

	public bool isOn
	{
		get
		{
			if (!isActiveAndEnabled)
				return false;

			Init();

			switch(m_Type)
			{
				case Type.Point: return m_Light.enabled || GetForceOn();
				case Type.Tube: return m_TubeLight.enabled || GetForceOn();
				case Type.Area: return m_AreaLight.enabled || GetForceOn();
				case Type.Directional: return m_Light.enabled || GetForceOn();
			}

			return false;
		}

		private set{}
	}

	new public Light light {get{Init(); return m_Light;} private set{}}
	public TubeLight tubeLight {get{Init(); return m_TubeLight;} private set{}}
	public AreaLight areaLight {get{Init(); return m_AreaLight;} private set{}}

	public Type type {get{Init(); return m_Type;} private set{}}

	// To get the "enabled" state checkbox
	void Update()
	{

	}

	public abstract bool GetForceOn();

	void Init()
	{
		if (m_Initialized)
			return;

		if ((m_Light = GetComponent<Light>()) != null)
		{
			switch(m_Light.type)
			{
				case LightType.Point: m_Type = Type.Point; break;
				case LightType.Directional: m_Type = Type.Directional; break;
				default: m_Type = Type.None; break;
			}
		}
		else if ((m_TubeLight = GetComponent<TubeLight>()) != null)
		{
			m_Type = Type.Tube;
		}
		else if ((m_AreaLight = GetComponent<AreaLight>()) != null)
		{
			m_Type = Type.Area;
		}

		m_Initialized = true;
	}
}
