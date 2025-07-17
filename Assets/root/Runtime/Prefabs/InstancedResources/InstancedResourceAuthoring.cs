using System.Linq;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

public struct InstancedResourceRequest : ISharedComponentData
{
    public readonly int ToSpawn;

    public InstancedResourceRequest(int toSpawn)
    {
        ToSpawn = toSpawn;
    }
}

public class InstancedResourceAuthoring : MonoBehaviour
{
    public DatabaseRef<InstancedResource, InstancedResourcesDatabase> Particle = new();

    public class Baker : Baker<InstancedResourceAuthoring>
    {
        public override void Bake(InstancedResourceAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddSharedComponent(entity, new InstancedResourceRequest(authoring.Particle.AssetIndex));
            AddComponent(entity, new LocalTransformLast());
            AddComponent<SpriteAnimFrame>(entity);
            
            if (authoring.Particle.Asset && authoring.Particle.Asset.Animated)
                AddComponent<SpriteAnimFrameTime>(entity);
        }
    }
}