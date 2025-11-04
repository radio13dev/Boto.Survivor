using Collisions;
using Unity.Entities;
using UnityEngine;

public class TerrainAuthoring : MonoBehaviour
{
    public TerrainCollisionSystem.Mask Mask = TerrainCollisionSystem.Mask.All;
    partial class Baker : Baker<TerrainAuthoring>
    {
        public override void Bake(TerrainAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.WorldSpace);
            AddComponent(entity, new TerrainTag()
            {
                Mask = authoring.Mask
            });
        }
    }
}
