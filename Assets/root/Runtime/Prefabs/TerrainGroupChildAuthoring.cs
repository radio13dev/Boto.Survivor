using Unity.Entities;
using UnityEngine;

public class TerrainGroupChildAuthoring : MonoBehaviour
{
    partial class Baker : Baker<TerrainGroupChildAuthoring>
    {
        public override void Bake(TerrainGroupChildAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
        }
    }
}