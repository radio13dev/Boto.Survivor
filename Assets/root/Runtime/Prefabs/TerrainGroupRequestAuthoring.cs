using BovineLabs.Saving;
using BovineLabs.Saving.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class TerrainGroupRequestAuthoring : MonoBehaviour
{
    public int SpecificGroup = -1;

    partial class Baker : Baker<TerrainGroupRequestAuthoring>
    {
        public override void Bake(TerrainGroupRequestAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.None);
            TerrainGroupRequest request = new() { Index = authoring.SpecificGroup };
            AddComponent<TerrainGroupRequest>(entity, request);
        }
    }
}

[Save]
public struct TerrainGroupRequest : IComponentData
{
    public int Index;
}

[UpdateInGroup(typeof(WorldInitSystemGroup))]
public partial struct TerrainGroupInitSystem : ISystem
{
    const int NonRandomGroupCount = 2;
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
        var r = random.Random;
        foreach (var terrainSpawner in SystemAPI.Query<RefRO<TerrainGroupRequest>>())
        {
            // Setup map with initial terrain
            var posToroidal = r.NextFloat2(bounds.Min, bounds.Max);
            var pos = TorusMapper.ToroidalToCartesian(posToroidal.x, posToroidal.y);
            TorusMapper.SnapToSurface(pos, 0, out _, out var normal);
            var randomRot = r.NextFloat3Direction();
            var rotation = quaternion.LookRotationSafe(math.cross(math.cross(normal, randomRot), normal), normal);
            
            var groupE = Entity.Null; //terrainSpawner.ValueRO.SpecificRequest;
            if (terrainSpawner.ValueRO.Index == -1) groupE = options[r.NextInt(NonRandomGroupCount, options.Length)].Entity;
            else groupE = options[terrainSpawner.ValueRO.Index].Entity;
            
            var instGroup = state.EntityManager.Instantiate(groupE);
            LocalTransform parentT = LocalTransform.FromPositionRotation(pos, rotation);
            
            var children = SystemAPI.GetBuffer<SavableLinks>(instGroup);
            for (int i = 0; i < children.Length; i++)
            {
                var childE = children[i].Entity;
                var initT = SystemAPI.GetComponent<LocalTransform>(childE);
                var premappedT = parentT.TransformTransform(initT);
                TorusMapper.SnapToSurface(premappedT.Position, initT.Position.y, out var finalPos, out var finalNormal);
                var finalT = premappedT;
                finalT.Position = finalPos;
                finalT.Rotation = Quaternion.FromToRotation(normal, finalNormal) * finalT.Rotation;
                state.EntityManager.SetComponentData(childE, finalT);
            }
            
        }
        
        var destQ = SystemAPI.QueryBuilder().WithAll<TerrainGroupRequest>().Build();
        state.EntityManager.DestroyEntity(destQ);
    }
}