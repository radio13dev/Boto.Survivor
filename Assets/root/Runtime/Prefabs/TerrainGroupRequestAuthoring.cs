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
            AddComponent<TerrainGroupRequest>(entity);
        }
    }
}

public struct TerrainGroupRequest : IComponentData
{
}