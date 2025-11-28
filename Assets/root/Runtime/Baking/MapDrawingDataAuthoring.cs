using BovineLabs.Saving;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class MapDrawingDataAuthoring : MonoBehaviour
{
    partial class Baker : Baker<MapDrawingDataAuthoring>
    {
        public override void Bake(MapDrawingDataAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddBuffer<MapDrawingData>(entity);
        }
    }
}

[Save]
public struct MapDrawingData : IBufferElementData { public float3 DrawPoint; }