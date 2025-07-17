using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class InlineScritableEditAttribute : PropertyAttribute
{
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(InlineScritableEditAttribute))]
public class InlineScritableEditDrawer : PropertyDrawer
{
    // Cache foldout state for each property instance.
    private static Dictionary<string, bool> s_FoldoutStates = new Dictionary<string, bool>();

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // Get the initial height (object field).
        float height = EditorGUIUtility.singleLineHeight;

        // Only add extra height if the resource is assigned.
        if (property.objectReferenceValue != null)
        {
            string key = property.propertyPath;
            bool foldout = s_FoldoutStates.ContainsKey(key) && s_FoldoutStates[key];

            // Reserve a line for the foldout control.
            height += EditorGUIUtility.singleLineHeight;

            // If expanded, add additional height.
            if (foldout)
            {
                // Adjust this extra height as needed for your resource.
                //height += 100f;
            }
        }

        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // Draw the object field.
        Rect fieldRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        EditorGUI.PropertyField(fieldRect, property, label, true);

        if (property.objectReferenceValue != null)
        {
            // Get and draw the foldout.
            string key = property.propertyPath;
            bool foldout = s_FoldoutStates.ContainsKey(key) ? s_FoldoutStates[key] : false;
            Rect foldoutRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight, position.width, EditorGUIUtility.singleLineHeight);
            foldout = EditorGUI.Foldout(foldoutRect, foldout, $"Edit {property.objectReferenceValue.GetType().Name}", true);
            s_FoldoutStates[key] = foldout;

            // If expanded, display the inline inspector for the InstancedResource.
            if (foldout)
            {
                Editor editor = Editor.CreateEditor(property.objectReferenceValue);
                if (editor != null)
                {
                    editor.OnInspectorGUI();
                }
            }
        }

        EditorGUI.EndProperty();
    }

    public static Object ObjectField(Object asset, Type assetType, bool b)
    {
        // Draw the object field.
        Object newVal = EditorGUILayout.ObjectField(asset, typeof(ScriptableObject).IsAssignableFrom(assetType) ? assetType : typeof(Object), false);
        
        if (newVal && !assetType.IsAssignableFrom(newVal.GetType()))
        {
            Object adjusted;
            if (newVal is GameObject gObj)
                adjusted = gObj.GetComponent(assetType);
            else
                adjusted = null;
            
            if (!adjusted)
            {
                Debug.LogError($"Couldn't convert {newVal} ({newVal.GetType().Name}) to {assetType.Name}");
            }
            newVal = adjusted;
        }
        
        if (newVal != null && typeof(ScriptableObject).IsAssignableFrom(assetType))
        {
            // Get and draw the foldout.
            string key = assetType.Name;
            bool foldout = s_FoldoutStates.ContainsKey(key) ? s_FoldoutStates[key] : false;
            foldout = EditorGUILayout.Foldout(foldout, $"Edit {key}", true);
            s_FoldoutStates[key] = foldout;

            // If expanded, display the inline inspector for the InstancedResource.
            if (foldout)
            {
                Editor editor = Editor.CreateEditor(asset);
                if (editor != null)
                {
                    editor.OnInspectorGUI();
                }
            }
        }
        
        return newVal;
    }
}
#endif