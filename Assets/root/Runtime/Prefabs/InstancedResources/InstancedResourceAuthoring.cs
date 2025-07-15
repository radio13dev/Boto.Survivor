using System.Linq;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

public class InstancedResourceAuthoring : MonoBehaviour
{
    public int AssetIndex;

    public class Baker : Baker<InstancedResourceAuthoring>
    {
        public override void Bake(InstancedResourceAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddSharedComponent(entity, new InstancedResourceRequest(authoring.AssetIndex));
            AddComponent(entity, new LocalTransformLast());
        }
    }

#if UNITY_EDITOR
    public InstancedResource ToSpawn;
    
    [EditorButton]
    public void NewInstancedResource()
    {
        InstancedResource asset = null;
    
        string key = "NewInstancedResource";
        int attempt = 1;
        while (AssetDatabase.FindAssets(key).Length >= 1)
        {
            key = $"NewInstancedResource_{attempt++}";
        }
    
        Debug.Log("Create new Asset");
        asset = ScriptableObject.CreateInstance<InstancedResource>();
        AssetDatabase.CreateAsset(asset, $@"Assets\root\Runtime\Prefabs\InstancedResources\{key}.asset");
        
        ToSpawn = asset;
    
        EditorUtility.SetDirty(this);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
    
    [UnityEditor.CustomEditor(typeof(InstancedResourceAuthoring))]
    public class MyEditor : EditorButton
    {
        InstancedResource _lastRef;

        private void OnEnable()
        {
            _lastRef = null;
        }

        private void OnDisable()
        {
            _lastRef = null;
        }

        private void OnSceneGUI()
        {
            var specific = (InstancedResourceAuthoring)target;
            if (_lastRef != specific.ToSpawn)
            {
                _lastRef = specific.ToSpawn;
                if (!specific.ToSpawn)
                {
                }
                else
                {
                    string[] result = AssetDatabase.FindAssets("InstancedResourcesDatabaseAsset");
                    InstancedResourcesDatabase database = null;
    
                    if (result.Length > 1)
                    {
                        Debug.LogError($"More than 1 Asset founded: {string.Join(", ", result.Select(r => AssetDatabase.GUIDToAssetPath(r)))}");
                        return;
                    }
    
                    bool shouldSave = false;
                    if(result.Length == 0)
                    {
                        Debug.Log("Create new Asset");
                        database = ScriptableObject.CreateInstance<InstancedResourcesDatabase >();
                        AssetDatabase.CreateAsset(database, @"Assets\root\Runtime\Prefabs\InstancedResources\InstancedResourcesDatabaseAsset.asset");
                        shouldSave = true;
                    }
                    else
                    {
                        string path = AssetDatabase.GUIDToAssetPath(result[0]);
                        database = (InstancedResourcesDatabase)AssetDatabase.LoadAssetAtPath(path, typeof(InstancedResourcesDatabase));
                        Debug.Log("Found Asset File !!!");
                    }
                    
                    var newIndex = database.IndexOf(specific.ToSpawn);
                    if (newIndex == -1)
                    {
                        newIndex = database.Add(specific.ToSpawn);
                        shouldSave = true;
                    }
                    
                    if (specific.AssetIndex != newIndex)
                    {
                        specific.AssetIndex = newIndex;
                        shouldSave = true;
                    }
                    
                    if (shouldSave)
                    {
                        EditorUtility.SetDirty(specific);
                        EditorUtility.SetDirty(database);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                    }
                }
            }
        }
    }

#endif
}