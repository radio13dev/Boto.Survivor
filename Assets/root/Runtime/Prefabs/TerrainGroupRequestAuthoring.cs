using System;
using BovineLabs.Saving;
using BovineLabs.Saving.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class TerrainGroupRequestAuthoring : MonoBehaviour
{
    public TerrainGroupRequest Request = new TerrainGroupRequest(){ Index = -1 };
    

    partial class Baker : Baker<TerrainGroupRequestAuthoring>
    {
        public override void Bake(TerrainGroupRequestAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(entity, authoring.Request);
        }
    }
}

[Save]
[Serializable]
public struct TerrainGroupRequest : IComponentData
{
    public int Index;
    public bool FixedPosition;
}

[UpdateInGroup(typeof(WorldInitSystemGroup))]
public partial struct TerrainGroupInitSystem : ISystem
{
    const int NonRandomGroupCount = 3;
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
        foreach (var (terrainSpawner, e) in SystemAPI.Query<RefRO<TerrainGroupRequest>>().WithEntityAccess())
        {
            LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(e);
            
            // Setup map with initial terrain
            // Position
            var posToroidal = r.NextFloat2(bounds.Min, bounds.Max);
            var pos = TorusMapper.ToroidalToCartesian(posToroidal.x, posToroidal.y);
            if (terrainSpawner.ValueRO.FixedPosition) pos = transform.Position;
            TorusMapper.SnapToSurface(pos, 0, out _, out var normal);
            
            // Rotation
            var randomRot = r.NextFloat3Direction();
            if (terrainSpawner.ValueRO.FixedPosition) randomRot = transform.Forward();
            var rotation = quaternion.LookRotationSafe(math.cross(math.cross(normal, randomRot), normal), normal);
            
            // What to spawn
            var groupE = Entity.Null; //terrainSpawner.ValueRO.SpecificRequest;
            if (terrainSpawner.ValueRO.Index == -1) groupE = options[r.NextInt(NonRandomGroupCount, options.Length)].Entity;
            else groupE = options[terrainSpawner.ValueRO.Index].Entity;
            
            // Spawn parent
            LocalTransform parentT = LocalTransform.FromPositionRotation(pos, rotation);
            SpawnTerrainGroup(state.EntityManager, groupE, parentT);
        }
        
        var destQ = SystemAPI.QueryBuilder().WithAll<TerrainGroupRequest>().Build();
        state.EntityManager.DestroyEntity(destQ);
    }

    public static Entity SpawnTerrainGroup(EntityManager entityManager, Entity groupE, LocalTransform parentT)
    {
        var instGroup = entityManager.Instantiate(groupE);
        entityManager.SetComponentData(instGroup, parentT);
            
        // Spawn children entities
        var normal = parentT.Up();
        var children = entityManager.GetBuffer<SavableLinks>(instGroup);
        for (int i = 0; i < children.Length; i++)
        {
            var childE = children[i].Entity;
            var initT = entityManager.GetComponentData<LocalTransform>(childE);
            var premappedT = parentT.TransformTransform(initT);
            TorusMapper.SnapToSurface(premappedT.Position, initT.Position.y, out var finalPos, out var finalNormal);
            var finalT = premappedT;
            finalT.Position = finalPos;
            finalT.Rotation = Quaternion.FromToRotation(normal, finalNormal) * finalT.Rotation;
            entityManager.SetComponentData(childE, finalT);
        }
        return instGroup;
    }
}