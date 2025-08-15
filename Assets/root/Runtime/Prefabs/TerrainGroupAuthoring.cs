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

[UpdateInGroup(typeof(SurvivorSimulationSystemGroup))]
public partial struct TerrainGroupInitSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<SharedRandom>();
        state.RequireForUpdate<GameManager.TerrainGroup>();
        state.RequireForUpdate<TerrainGroupRequest>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var bounds = TorusMapper.MapBounds;
        var random = SystemAPI.GetSingleton<SharedRandom>();
        var options = SystemAPI.GetSingletonBuffer<GameManager.TerrainGroup>(true);
        var delayedEcb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        var r = random.Random;
        foreach ((var terrainSpawner, var terrainSpawnerE) in SystemAPI.Query<RefRO<TerrainGroupRequest>>().WithEntityAccess())
        {
            // Setup map with initial terrain
            var posToroidal = r.NextFloat2(bounds.Min, bounds.Max);
            var pos = TorusMapper.ToroidalToCartesian(posToroidal.x, posToroidal.y);
            TorusMapper.SnapToSurface(pos, 0, out _, out var normal);
            var randomRot = r.NextFloat3Direction();
            var rotation = quaternion.LookRotationSafe(math.cross(math.cross(normal, randomRot), normal), normal);
            
            var groupE = terrainSpawner.ValueRO.SpecificRequest;
            if (groupE == Entity.Null) groupE = options[r.NextInt(options.Length)].Entity;
            
            LocalTransform parentT = LocalTransform.FromPositionRotation(pos, rotation);
            
            var children = SystemAPI.GetBuffer<LinkedEntityGroup>(groupE);
            for (int i = 1; i < children.Length; i++)
            {
                var childE = children[i].Value;
                var newTerrainE = state.EntityManager.Instantiate(childE);
                var initT = SystemAPI.GetComponent<LocalTransform>(childE);
                var premappedT = parentT.TransformTransform(initT);
                TorusMapper.SnapToSurface(premappedT.Position, initT.Position.y, out var finalPos, out var finalNormal);
                var finalT = premappedT;
                finalT.Position = finalPos;
                finalT.Rotation = Quaternion.FromToRotation(normal, finalNormal) * finalT.Rotation;
                state.EntityManager.SetComponentData(newTerrainE, finalT);
            }
            
            delayedEcb.DestroyEntity(terrainSpawnerE);
        }
    }
}