using System.Linq;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

public class SpecificPrefabAuthoring : MonoBehaviour
{
#if UNITY_EDITOR
    public GameObject ToSpawn;
#endif

    public int ToSpawnId;
    public bool Dynamic;

    public class Baker : Baker<SpecificPrefabAuthoring>
    {
        public override void Bake(SpecificPrefabAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(entity, new SpecificPrefabRequest(authoring.ToSpawnId));
            if (authoring.Dynamic)
                AddComponent(entity, new SpecificPrefabRequest.DynamicTag());
        }
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(SpecificPrefabAuthoring))]
    public class Editor : UnityEditor.Editor
    {
        GameObject _lastRef;

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
            var specific = (SpecificPrefabAuthoring)target;
            if (_lastRef != specific.ToSpawn)
            {
                _lastRef = specific.ToSpawn;
                if (!specific.ToSpawn)
                {
                    specific.ToSpawnId = -1;
                }
                else
                {
                    string[] result = AssetDatabase.FindAssets("SpecificPrefabDatabaseAsset");
                    SpecificPrefabDatabase database = null;
    
                    if (result.Length > 1)
                    {
                        Debug.LogError($"More than 1 Asset founded: {string.Join(", ", result.Select(r => AssetDatabase.GUIDToAssetPath(r)))}");
                        return;
                    }
    
                    if(result.Length == 0)
                    {
                        Debug.Log("Create new Asset");
                        database = ScriptableObject.CreateInstance<SpecificPrefabDatabase >();
                        AssetDatabase.CreateAsset(database, @"Assets\root\Runtime\Prefabs\SpecificPrefabDatabaseAsset.asset");
                    }
                    else
                    {
                        string path = AssetDatabase.GUIDToAssetPath(result[0]);
                        database = (SpecificPrefabDatabase)AssetDatabase.LoadAssetAtPath(path, typeof(SpecificPrefabDatabase));
                        Debug.Log("Found Asset File !!!");
                    }
    
                    var index = database.IndexOf(specific.ToSpawn);
                    if (index == -1)
                        index = database.Add(specific.ToSpawn);
                    specific.ToSpawnId = index;
    
                    EditorUtility.SetDirty(specific);
                    EditorUtility.SetDirty(database);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
            }
        }
    }

#endif
}