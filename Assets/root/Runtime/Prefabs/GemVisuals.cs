using AYellowpaper.SerializedCollections;
using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/GemVisualsDatabase", order = 1)]
public class GemVisuals : ScriptableObject
{
    public SerializedDictionary<Gem.Type, InstancedResource> Value = new();
}