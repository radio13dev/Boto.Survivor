using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using System.Threading;
using UnityEditor;
#endif

[Serializable]
public sealed class DatabaseRef<T, D> : DatabaseRef where D : Database<T> where T : Object
{
    public T Asset => _Asset as T;

    public int GetAssetIndex()
    {
#if UNITY_EDITOR
        if (!Asset)
        {
            return -1;
        }

        var database = GetCreateDatabase(out bool shouldSave);

        var newIndex = database.IndexOf(Asset);
        if (newIndex == -1)
        {
            newIndex = database.Add(Asset);
            shouldSave = true;
        }

        if (shouldSave)
        {
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        return newIndex;
#else
        Debug.LogError($"This is an editor only method.");
        return -1;
#endif
    }

#if UNITY_EDITOR
    static D _database;
    static SemaphoreSlim _databaseCreateLock = new SemaphoreSlim(1,1);
#endif

    public D GetCreateDatabase(out bool created)
    {
#if UNITY_EDITOR
        if (_database)
        {
            created = false;
            return _database;
        }

        string assetPath = @$"Assets\root\Runtime\Prefabs\{typeof(D).Name}";
        var databaseKey = typeof(D).Name + "Asset";
        string[] result = AssetDatabase.FindAssets(databaseKey);
        Object database = null;
        if (result.Length > 1)
            Debug.LogError($"More than 1 Asset founded: {string.Join(", ", result.Select(r => AssetDatabase.GUIDToAssetPath(r)))}");

        if (result.Length == 0)
        {
            _databaseCreateLock.Wait();
            // Double check it hasn't been created...
            try
            {
                result = AssetDatabase.FindAssets(databaseKey);
                if (result.Length == 0)
                {
                    Debug.Log("Create new Asset");
                    database = ScriptableObject.CreateInstance(typeof(D));
                    if (!Directory.Exists(assetPath))
                        Directory.CreateDirectory(assetPath);
                    AssetDatabase.CreateAsset(database, $@"{assetPath}\{databaseKey}.asset");
                }
                created = false;
            }
            finally
            {
                _databaseCreateLock.Release();
            }
            
        }
        else
        {
            string path = AssetDatabase.GUIDToAssetPath(result[0]);
            database = AssetDatabase.LoadAssetAtPath(path, typeof(D));
            //Debug.Log($"Got asset file for {nameof(DatabaseRef)}<{typeof(T).Name},{typeof(D).Name}>: {path}");
            created = false;
        }

        return database as D;
#else
        created = false;
        Debug.LogError($"This is an editor only method.");
        return null;
#endif
    }
}

[Serializable]
public class DatabaseRef
{
    [SerializeField] public Object _Asset;
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

public abstract class Database : ScriptableObject
{
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(DatabaseRef), true)]
public class DatbaseRefDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Using BeginProperty / EndProperty on the parent property means that
        // prefab override logic works on the entire property.
        EditorGUI.BeginProperty(position, label, property);

        // Draw label
        position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

        var assetProp = property.FindPropertyRelative("_Asset");

        var databaseRefType = property.boxedValue.GetType();
        var assetType = databaseRefType.GenericTypeArguments[0];
        var databaseType = databaseRefType.GenericTypeArguments[1];

        string assetPath = @$"Assets\root\Runtime\Prefabs\{databaseType.Name}";

        var asset = assetProp.objectReferenceValue;

        var adjusted = InlineScritableEditDrawer.ObjectField(asset, assetType, false);

        if (typeof(ScriptableObject).IsAssignableFrom(assetType))
        {
            var spawnNewAsset = GUILayout.Button($"New {assetType.Name}...");
            if (spawnNewAsset)
            {
                string assetKey = $"New{assetType.Name}";
                int attempt = 1;
                while (AssetDatabase.FindAssets(assetKey).Length >= 1)
                {
                    assetKey = $"New{assetType.Name}_{attempt++}";
                }

                Debug.Log("Create new Asset");
                adjusted = ScriptableObject.CreateInstance(assetType);
                if (!Directory.Exists(assetPath))
                    Directory.CreateDirectory(assetPath);
                AssetDatabase.CreateAsset(adjusted, $@"{assetPath}\{assetKey}.asset");

                EditorUtility.SetDirty(adjusted);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        bool changed = false;
        if (adjusted != asset)
        {
            assetProp.boxedValue = adjusted;
            asset = adjusted;
            changed = true;
        }

        if (changed)
        {
            bool shouldSave = false;
            var databaseKey = databaseType.Name + "Asset";
            string[] result = AssetDatabase.FindAssets(databaseKey);
            Object database = null;
            if (result.Length > 1)
                Debug.LogError($"More than 1 Asset founded: {string.Join(", ", result.Select(r => AssetDatabase.GUIDToAssetPath(r)))}");

            if (result.Length == 0)
            {
                Debug.Log("Create new Asset");
                database = ScriptableObject.CreateInstance(databaseType);
                if (!Directory.Exists(assetPath))
                    Directory.CreateDirectory(assetPath);
                AssetDatabase.CreateAsset(database, $@"{assetPath}\{databaseKey}.asset");
                shouldSave = true;
            }
            else
            {
                string path = AssetDatabase.GUIDToAssetPath(result[0]);
                database = AssetDatabase.LoadAssetAtPath(path, databaseType);
                Debug.Log("Found Asset File !!!");
            }

            if (!asset)
            {
            }
            else
            {
                var indexMethod = databaseType.GetMethod("IndexOf", BindingFlags.Instance | BindingFlags.Public, null, new[] { assetType }, null);
                var addMethod = databaseType.GetMethod("Add", BindingFlags.Instance | BindingFlags.Public, null, new[] { assetType }, null);

                var newIndex = (int)indexMethod.Invoke(database, new object[] { asset });
                if (newIndex == -1)
                {
                    newIndex = (int)addMethod.Invoke(database, new object[] { asset });
                    shouldSave = true;
                }
            }

            if (shouldSave)
            {
                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }
}
#endif