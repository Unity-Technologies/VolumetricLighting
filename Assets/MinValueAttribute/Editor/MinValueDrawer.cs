using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer (typeof (MinValueAttribute))]
public class MinValueDrawer : PropertyDrawer {

	public override void OnGUI (Rect position, SerializedProperty property, GUIContent label)
	{
		if (property.propertyType != SerializedPropertyType.Float && property.propertyType != SerializedPropertyType.Integer)
		{
			EditorGUI.LabelField (position, label.text, "Use MinValue with float or int.");
			return;
		}

		EditorGUI.PropertyField(position, property, label);

		if (GUI.changed)
		{
			MinValueAttribute minValue = attribute as MinValueAttribute;
			property.floatValue = Mathf.Max(property.floatValue, minValue.min);
		}
	}
}
