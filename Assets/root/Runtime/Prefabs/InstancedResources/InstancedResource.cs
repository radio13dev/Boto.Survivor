using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/InstancedResourcesDatabase", order = 1)]
public class InstancedResource : ScriptableObject
{
    public Material Material;
    public Mesh Mesh;

    internal RenderParams RenderParams
    {
        get
        {
            if (!m_Setup)
            {
                m_Setup = true;
                m_RenderParams = new RenderParams(Material);
            }
            
            return m_RenderParams;
        }
    }

    [NonSerialized] bool m_Setup;
    [NonSerialized] RenderParams m_RenderParams;
}


#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(InstancedResource))]
public class InlineInstancedResourceDrawer : PropertyDrawer
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
            foldout = EditorGUI.Foldout(foldoutRect, foldout, "Edit InstancedResource", true);
            s_FoldoutStates[key] = foldout;

            // If expanded, display the inline inspector for the InstancedResource.
            if (foldout)
            {
                Editor editor = Editor.CreateEditor(property.objectReferenceValue);
                if (editor != null)
                {
                    Rect inspectorRect = new Rect(position.x, position.y + 2 * EditorGUIUtility.singleLineHeight, position.width, 100f);
                    editor.OnInspectorGUI();
                }
            }
        }

        EditorGUI.EndProperty();
    }
}
#endif