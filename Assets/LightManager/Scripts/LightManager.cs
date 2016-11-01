using UnityEngine;
using System.Collections.Generic;

public class LightManager<T> : MonoBehaviour
{
	static LightManager<T> s_Instance;
	HashSet<T> m_Container = new HashSet<T>();

	static LightManager<T> Instance
	{
		get
		{
			if (s_Instance != null)
				return s_Instance;

			s_Instance = (LightManager<T>) FindObjectOfType(typeof(LightManager<T>));
			return s_Instance;
		}
	}

	public static HashSet<T> Get()
	{
		LightManager<T> instance = Instance;
		return instance == null ? new HashSet<T>() : instance.m_Container;
	}

	public static bool Add(T t)
	{
		LightManager<T> instance = Instance;
		if (instance == null)
			return false;
	
		instance.m_Container.Add(t);
		return true;
	}

	public static void Remove(T t)
	{
		LightManager<T> instance = Instance;
		if (instance == null)
			return;
	
		instance.m_Container.Remove(t);
	}
}
