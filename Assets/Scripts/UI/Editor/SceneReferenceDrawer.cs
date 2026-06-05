using UnityEditor;
using UnityEngine;

// Renders SceneReference as a single inline SceneAsset object field.
[CustomPropertyDrawer(typeof(SceneReference))]
public class SceneReferenceDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty sceneAsset = property.FindPropertyRelative("sceneAsset");
        EditorGUI.PropertyField(position, sceneAsset, label);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }
}
