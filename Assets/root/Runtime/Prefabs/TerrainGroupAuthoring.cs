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
        }
    }
}