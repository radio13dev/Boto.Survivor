using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/SpecificPrefabDatabase", order = 1)]
public class SpecificPrefabDatabase : ScriptableObject
{
    public List<GameObject> Prefabs;

    public int IndexOf(GameObject prefab)
    {
        for (int i = 0; i < Prefabs.Count; i++)
            if (Prefabs[i] == prefab) return i;
        return -1;
    }
    
    public int Add(GameObject prefab)
    {
        Prefabs.Add(prefab);
        return Prefabs.Count - 1;
    }
}