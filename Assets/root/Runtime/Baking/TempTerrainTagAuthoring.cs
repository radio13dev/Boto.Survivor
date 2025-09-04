using Collisions;
using Unity.Entities;
using UnityEngine;

public class TempTerrainTagAuthoring : MonoBehaviour
{
    partial class Baker : Baker<TempTerrainTagAuthoring>
    {
        public override void Bake(TempTerrainTagAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            AddComponent<TempTerrainTag>(entity);
        }
    }
}