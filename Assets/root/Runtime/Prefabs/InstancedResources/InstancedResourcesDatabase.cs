using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/InstancedResourcesDatabase", order = 1)]
public class InstancedResourcesDatabase : ScriptableObject
{
    public List<InstancedResource> Instances = new();

    public int IndexOf(InstancedResource instance)
    {
        for (int i = 0; i < Instances?.Count; i++)
            if (Instances[i] == instance) return i;
        return -1;
    }
    
    public int Add(InstancedResource instance)
    {
        for (int i = 0; i < Instances.Count; i++)
            if (!Instances[i])
            {
                Instances[i] = instance;
                return i;
            }
        Instances.Add(instance);
        return Instances.Count - 1;
    }
}