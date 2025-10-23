using System;
using BovineLabs.Core.Extensions;
using BovineLabs.Saving;
using BovineLabs.Saving.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Object = UnityEngine.Object;

public class TerrainGroupRequestAuthoring : MonoBehaviour
{
    public DatabaseRef<GameObject, GenericDatabase> Request = new();

    partial class Baker : Baker<TerrainGroupRequestAuthoring>
    {
        public override void Bake(TerrainGroupRequestAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(entity, new TerrainGroupRequest()
            {
                Index = authoring.Request.GetAssetIndex()
            });
        }
    }
}

[Save]
[Serializable]
public struct TerrainGroupRequest : IComponentData
{
    public int Index;
}

[UpdateInGroup(typeof(SurvivorWorldInitSystemGroup))]
public partial struct TerrainGroupInitSystem : ISystem
{
    const int NonRandomGroupCount = 5;
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<SharedRandom>();
        state.RequireForUpdate<GameManager.Prefabs>();
        state.RequireForUpdate<TerrainGroupRequest>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var bounds = TorusMapper.MapBounds;
        var random = SystemAPI.GetSingleton<SharedRandom>();
        var options = SystemAPI.GetSingletonBuffer<GameManager.Prefabs>(true);
        var r = random.Random;
        foreach (var (terrainSpawner, e) in SystemAPI.Query<RefRO<TerrainGroupRequest>>().WithEntityAccess())
        {
            LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(e);
            
            // What to spawn
            var groupE = Entity.Null; //terrainSpawner.ValueRO.SpecificRequest;
            if (terrainSpawner.ValueRO.Index == -1) groupE = options[r.NextInt(NonRandomGroupCount, options.Length)].Entity;
            else groupE = options[terrainSpawner.ValueRO.Index].Entity;
            
            // Setup map with initial terrain
            // Position
            float3 pos = default;
            float3 normal = default;
            if (SystemAPI.HasComponent<TerrainGroup>(groupE))
            {
                var exclusionRadius = SystemAPI.GetComponent<TerrainGroup>(groupE).ExclusionRadius;
                
                const int MAX_ATTEMPTS = 10;
                bool success = false;
                for (int attemptNum = 0; attemptNum < MAX_ATTEMPTS; attemptNum++)
                {
                    var posToroidal = r.NextFloat2(bounds.Min, bounds.Max);
                    pos = TorusMapper.ToroidalToCartesian(posToroidal.x, posToroidal.y);
                    TorusMapper.SnapToSurface(pos, 0, out pos, out normal);
                    
                    bool hitOther = false;
                    foreach (var other in SystemAPI.Query<RefRO<TerrainGroup>, RefRO<LocalTransform>>())
                    {
                        var d = math.distance(other.Item2.ValueRO.Position, pos);
                        if (d < exclusionRadius + other.Item1.ValueRO.ExclusionRadius)
                        {
                            hitOther = true;
                            break;
                        }
                    }
                    
                    if (!hitOther)
                    {
                        success = true;
                        break;
                    }
                }
                
                if (!success)
                {
                    Debug.Log($"Didn't spawn terrain, hit max attempts.");
                    continue;
                }
                
            }
            else
            {
                var posToroidal = r.NextFloat2(bounds.Min, bounds.Max);
                pos = TorusMapper.ToroidalToCartesian(posToroidal.x, posToroidal.y);
                TorusMapper.SnapToSurface(pos, 0, out pos, out normal);
            }
            
            
            // Rotation
            var randomRot = r.NextFloat3Direction();
            var rotation = quaternion.LookRotationSafe(math.cross(math.cross(normal, randomRot), normal), normal);
            
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