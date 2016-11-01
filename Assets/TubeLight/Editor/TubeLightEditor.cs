using UnityEngine;
using UnityEditor;
using UnityEditor.AnimatedValues;

[CustomEditor(typeof(TubeLight))]
[CanEditMultipleObjects]
public class TubeLightEditor : Editor {

	AnimFloat m_ShowInfo;

	void OnEnable()
	{
		m_ShowInfo = new AnimFloat(0);
		m_ShowInfo.valueChanged.AddListener(Repaint);
		m_ShowInfo.speed = 0.5f;
	}

	override public void OnInspectorGUI()
	{
		DrawDefaultInspector();

		if (targets.Length > 1)
			return;

		EditorGUILayout.Space();

		if(GUILayout.Button("Add a shadow plane"))
			m_ShowInfo.value = AddShadowPlane() ? 0 : 100;

		foreach (TubeLightShadowPlane shadowPlane in ((TubeLight)target).m_ShadowPlanes)
			if (shadowPlane != null)
				EditorGUILayout.ObjectField("Shadow Plane", shadowPlane, typeof(TubeLightShadowPlane), !EditorUtility.IsPersistent(target));

		m_ShowInfo.target = 0;
		if (EditorGUILayout.BeginFadeGroup(Mathf.Min(1.0f, m_ShowInfo.value)))
			EditorGUILayout.HelpBox("Limit of " + TubeLight.maxPlanes + " planes reached. Delete an existing one.", MessageType.Info);
		EditorGUILayout.EndFadeGroup();
	}

	bool AddShadowPlane()
	{
		TubeLight tubeLight = (TubeLight)target;

		int i = 0;
		for (; i < TubeLight.maxPlanes; i++)
		{
			if (tubeLight.m_ShadowPlanes[i] != null)
				continue;
			
			GameObject go = new GameObject("Shadow Plane");
			TubeLightShadowPlane shadowPlane = go.AddComponent<TubeLightShadowPlane>();

			go.transform.position = tubeLight.transform.position + go.transform.forward;
			go.transform.parent = tubeLight.transform;
			tubeLight.m_ShadowPlanes[i] = shadowPlane;
			EditorUtility.SetDirty (tubeLight);
			break;
		}

		return i < TubeLight.maxPlanes;
	}
}
