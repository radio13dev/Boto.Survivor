using Collisions;
using Unity.Entities;
using UnityEngine;

public class TerrainAuthoring : MonoBehaviour
{
    partial class Baker : Baker<TerrainAuthoring>
    {
        public override void Bake(TerrainAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent<TerrainTag>(entity);
        }
    }
}
