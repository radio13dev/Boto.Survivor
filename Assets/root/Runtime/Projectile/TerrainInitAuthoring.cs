using System;
using BovineLabs.Saving;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace Collisions
{
    public class TerrainInitAuthoring : MonoBehaviour
    {
        public TerrainInit TerrainInit;
        
        partial class Baker : Baker<TerrainInitAuthoring>
        {
            public override void Bake(TerrainInitAuthoring authoring)
            {
                var entity = GetEntity(authoring, TransformUsageFlags.None);
                AddComponent(entity, authoring.TerrainInit);
            }
        }
    }
    
    [Save]
    [Serializable]
    public struct TerrainInit : IComponentData
    {
        public int TerrainSpawnAttempts;
    }

    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(SurvivorSimulationSystemGroup))]
    public partial struct TerrainInitSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<GameManager.Terrain>();
            state.RequireForUpdate<TerrainInit>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var bounds = TorusMapper.MapBounds;
            var options = SystemAPI.GetSingletonBuffer<GameManager.Terrain>();
            Random r = Random.CreateFromIndex(unchecked((uint)SystemAPI.Time.ElapsedTime));
            var delayedEcb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            foreach ((var terrainSpawner, var terrainSpawnerE) in SystemAPI.Query<RefRO<TerrainInit>>().WithEntityAccess())
            {
                Debug.Log($"Init terrain: {terrainSpawner.ValueRO.TerrainSpawnAttempts}");
                // Setup map with initial terrain
                for (int i = 0; i < terrainSpawner.ValueRO.TerrainSpawnAttempts; i++)
                {
                    var posToroidal = r.NextFloat2(bounds.Min, bounds.Max);
                    var pos = TorusMapper.ToroidalToCartesian(posToroidal.x, posToroidal.y);
                    var template = options[r.NextInt(options.Length)];
                    var newTerrainE = state.EntityManager.Instantiate(template.Entity);
                    state.EntityManager.SetComponentData(newTerrainE, new LocalTransform(){ Position = pos });
                }
                
                delayedEcb.DestroyEntity(terrainSpawnerE);
            }
        }
    }
}