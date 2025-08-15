using Unity.Entities;
using UnityEngine;

public class TerrainGroupRequestAuthoring : MonoBehaviour
{
    public TerrainGroupAuthoring SpecificGroup;

    partial class Baker : Baker<TerrainGroupRequestAuthoring>
    {
        public override void Bake(TerrainGroupRequestAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            TerrainGroupRequest request = new();
            if (authoring.SpecificGroup)
                request.SpecificRequest = GetEntity(authoring.SpecificGroup, TransformUsageFlags.None);
            else
                request.SpecificRequest = Entity.Null;
            AddComponent<TerrainGroupRequest>(entity, request);
        }
    }
}

public struct TerrainGroupRequest : IComponentData
{
    public Entity SpecificRequest;
}