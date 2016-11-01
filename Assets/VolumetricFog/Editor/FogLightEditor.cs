using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(FogLight))]
[CanEditMultipleObjects]
public class FogLightEditor : Editor
{
	SerializedProperty m_IntensityMult;
	SerializedProperty m_RangeMult;
	SerializedProperty m_ForceOnForFog;
	SerializedProperty m_Shadows;
	SerializedProperty m_ShadowmapRes;
	SerializedProperty m_BlurIterations;
	SerializedProperty m_BlurSize;
	SerializedProperty m_Bounded;

	void OnEnable()
	{
		m_IntensityMult = serializedObject.FindProperty ("m_IntensityMult");
		m_RangeMult = serializedObject.FindProperty ("m_RangeMult");
		m_ForceOnForFog = serializedObject.FindProperty ("m_ForceOnForFog");
		m_Shadows = serializedObject.FindProperty ("m_Shadows");
		m_ShadowmapRes = serializedObject.FindProperty ("m_ShadowmapRes");
		m_BlurIterations = serializedObject.FindProperty ("m_BlurIterations");
		m_BlurSize = serializedObject.FindProperty ("m_BlurSize");
		m_Bounded = serializedObject.FindProperty("m_Bounded");
	}

	override public void OnInspectorGUI()
	{
		serializedObject.Update();

		EditorGUILayout.PropertyField(m_IntensityMult);
		EditorGUILayout.PropertyField(m_RangeMult);
		EditorGUILayout.PropertyField(m_ForceOnForFog);

		// Section below just for light types with shadow
		bool supportsShadows = false;
		bool isAreaLight = false;
		foreach (FogLight fogLight in targets)
		{
			if (fogLight.type == FogLight.Type.Area)
			{
				supportsShadows = true;
				isAreaLight = true;
				break;	
			}
			else if (fogLight.type == FogLight.Type.Directional)
			{
				supportsShadows = true;
				break;
			}
		}

		if (supportsShadows)
		{
			EditorGUILayout.Space();

			EditorGUILayout.PropertyField(m_Shadows);
			EditorGUILayout.PropertyField(m_ShadowmapRes);
			EditorGUILayout.PropertyField(m_BlurIterations);
			EditorGUILayout.PropertyField(m_BlurSize);
		}

		if (isAreaLight)
		{
			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(m_Bounded);
		}

		serializedObject.ApplyModifiedProperties();
	}
}
