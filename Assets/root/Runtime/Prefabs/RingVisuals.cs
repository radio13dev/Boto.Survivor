using AYellowpaper.SerializedCollections;
using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/RingVisualsDatabase", order = 1)]
public class RingVisuals : ScriptableObject
{
    public SerializedDictionary<RingPrimaryEffect, InstancedResource> Value = new();
}