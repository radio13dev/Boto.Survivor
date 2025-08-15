using BovineLabs.Saving;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class TerrainGroupAuthoring : MonoBehaviour
{
    partial class Baker : Baker<TerrainGroupAuthoring>
    {
        public override void Bake(TerrainGroupAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            var children = GetComponentsInChildren<Transform>();
            var links = AddBuffer<LinkedEntityGroup>(entity);
            links.Add(entity);
            foreach (var child in children)
                links.Add(GetEntity(child, TransformUsageFlags.Dynamic));
        }
    }
}