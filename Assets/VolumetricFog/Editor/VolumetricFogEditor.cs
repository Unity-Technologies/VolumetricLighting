using UnityEditor;

[CustomEditor(typeof(VolumetricFog))]
[CanEditMultipleObjects]
public class VolumetricFogEditor : Editor
{
	override public void OnInspectorGUI()
	{
		if (!VolumetricFog.CheckSupport())
		{
			EditorGUILayout.HelpBox(VolumetricFog.GetUnsupportedErrorMessage(), MessageType.Error);
			return;
		}

		DrawDefaultInspector();
	}
}
