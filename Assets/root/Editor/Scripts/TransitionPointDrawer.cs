using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(TransitionPoint))]
public class TransitionPointDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Using BeginProperty / EndProperty on the parent property means that
        // prefab override logic works on the entire property.
        EditorGUI.BeginProperty(position, label, property);

        // Draw label
        position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
        
        // Get current value
        TransitionPoint curVal = (TransitionPoint)property.boxedValue;
        
        // Get the game object related to this property
        var targetObject = property.serializedObject.targetObject;
        GameObject targetGObj = targetObject as GameObject;
        if (targetGObj == null)
            if (targetObject is MonoBehaviour mono) targetGObj = mono.gameObject;
        RectTransform targetRect = null;
        if (targetGObj && targetGObj.transform is RectTransform rectTransform)
            targetRect = rectTransform;
        
        bool wasEnabled = GUI.enabled;
        GUI.enabled = targetRect && !curVal.Equals(targetRect);
        var savePosition = GUILayout.Button($"Save Position");
        if (savePosition)
        {
            curVal.Save(targetRect);
            property.boxedValue = curVal;
        }
        
        var transition = GUILayout.Button($"Transition");
        if (transition)
        {
            EditorCoroutineUtility.StartCoroutine(curVal.Lerp(targetRect, HandUIController.k_AnimTransitionTime, true), this);
        }
        GUI.enabled = wasEnabled;

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }
}