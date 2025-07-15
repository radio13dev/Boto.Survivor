using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using System;
using UnityEditor;
#endif

public abstract class Database : ScriptableObject
{
}


public abstract class Database<T> : Database where T : Object
{
    public int Length => Assets?.Count ?? 0;
    public List<T> Assets = new();

    public int IndexOf(T instance)
    {
        for (int i = 0; i < Assets?.Count; i++)
            if (Assets[i] == instance)
                return i;
        return -1;
    }

    public int Add(T instance)
    {
        for (int i = 0; i < Assets.Count; i++)
            if (!Assets[i])
            {
                Assets[i] = instance;
                return i;
            }

        Assets.Add(instance);
        return Assets.Count - 1;
    }
    

    public T this[int index]
    {
        get
        {
            if (index < 0 || index >= Assets.Count) return null;
            return Assets[index];
        }
    }
}

public class DatabaseRef
{
}

public class DatabaseRef<T, D> : DatabaseRef where D : Database<T> where T : Object
{
    public int AssetIndex;
#if UNITY_EDITOR
    public T Asset;

    [CustomPropertyDrawer(typeof(DatabaseRef), true)]
    public class DatbaseRefDrawer : PropertyDrawer
    {
        // Draw the property inside the given rect
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Using BeginProperty / EndProperty on the parent property means that
            // prefab override logic works on the entire property.
            EditorGUI.BeginProperty(position, label, property);

            // Draw label
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            //// Don't make child fields be indented
            //var indent = EditorGUI.indentLevel;
            //EditorGUI.indentLevel = 0;

            var assetProp = property.FindPropertyRelative("Asset");
            var indexProp = property.FindPropertyRelative("AssetIndex");

            var databaseRefType = property.objectReferenceValue.GetType();
            var assetType = databaseRefType.GenericTypeArguments[0];
            var databaseType = databaseRefType.GenericTypeArguments[1];

            var asset = assetProp.objectReferenceValue;
            if (!asset)
            {
            }
            else
            {
                var key = databaseType.Name + "Asset";
                string[] result = AssetDatabase.FindAssets(key);
                Object database = null;
                if (result.Length > 1)
                {
                    Debug.LogError($"More than 1 Asset founded: {string.Join(", ", result.Select(r => AssetDatabase.GUIDToAssetPath(r)))}");
                    return;
                }

                bool shouldSave = false;
                if (result.Length == 0)
                {
                    Debug.Log("Create new Asset");
                    database = ScriptableObject.CreateInstance(databaseType);
                    AssetDatabase.CreateAsset(database, $@"Assets\root\Runtime\Prefabs\{key}.asset");
                    shouldSave = true;
                }
                else
                {
                    string path = AssetDatabase.GUIDToAssetPath(result[0]);
                    database = AssetDatabase.LoadAssetAtPath(path, databaseType);
                    Debug.Log("Found Asset File !!!");
                }

                var indexMethod = databaseType.GetMethod("IndexOf", BindingFlags.Instance | BindingFlags.Public, null, new[] { assetType }, null);
                var addMethod = databaseType.GetMethod("Add", BindingFlags.Instance | BindingFlags.Public, null, new[] { assetType }, null);

                var newIndex = (int)indexMethod.Invoke(database, new object[] { asset });
                if (newIndex == -1)
                {
                    newIndex = (int)addMethod.Invoke(database, new object[] { asset });
                    shouldSave = true;
                }

                if (indexProp.intValue != newIndex)
                {
                    indexProp.intValue = newIndex;
                    shouldSave = true;
                }

                if (shouldSave)
                {
                    EditorUtility.SetDirty(database);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
            }

            // Draw fields - pass GUIContent.none to each so they are drawn without labels
            Rect assetRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(assetRect, assetProp, GUIContent.none, true);
            GUI.enabled = false;
            Rect indexRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + 2, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(indexRect, indexProp, GUIContent.none, true);
            GUI.enabled = true;

            //// Set indent back to what it was
            //EditorGUI.indentLevel = indent;

            EditorGUI.EndProperty();
        }
    }
#endif
}