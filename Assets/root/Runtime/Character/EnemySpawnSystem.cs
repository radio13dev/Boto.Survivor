using System;
using BovineLabs.Saving;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[Serializable]
[Save]
public partial struct EnemySpawner : IComponentData
{
    [HideInInspector] public Unity.Mathematics.Random random;
    [HideInInspector] public double nextSpawnTime;
    public float SpawnRadius;
    public float SpawnBlockRadiusSqr;
}

[RequireMatchingQueriesForUpdate]
[UpdateInGroup(typeof(SurvivorSimulationSystemGroup))]
public partial struct EnemySpawnSystem : ISystem
{
    EntityQuery m_PlayerTransformsQuery;
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EnemySpawningEnabled>();
        state.RequireForUpdate<StepController>();
        state.RequireForUpdate<SharedRandom>();
        state.RequireForUpdate<GameManager.Resources>();
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<PlayerControlled>();
        m_PlayerTransformsQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, PlayerControlled>().Build();
        state.RequireForUpdate(m_PlayerTransformsQuery);
    }

    public void OnUpdate(ref SystemState state)
    {
        var playerTransforms = m_PlayerTransformsQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        var delayedEcb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        state.Dependency = new Job()
        {
            ecb = delayedEcb.AsParallelWriter(),
            enemies = SystemAPI.GetSingletonBuffer<GameManager.Enemies>(true),
            PlayerTransforms = playerTransforms,
            Time = SystemAPI.Time.ElapsedTime,
        }.Schedule(state.Dependency);
        playerTransforms.Dispose(state.Dependency);
        
    }
    
    partial struct Job : IJobEntity
    {
        const int k_MaxSpawnedPerFrame = 10;
    
        static readonly float2 min = new float2(-1,-1);
        static readonly float2 max = new float2(1,1);
    
        public EntityCommandBuffer.ParallelWriter ecb;
        [ReadOnly] public DynamicBuffer<GameManager.Enemies> enemies;
        [ReadOnly] public double Time;
        [ReadOnly] public NativeArray<LocalTransform> PlayerTransforms;
    
        public void Execute([ChunkIndexInQuery] int key, in LocalTransform t, ref EnemySpawner spawner)
        {
            int spawned = 0;
            while (spawner.nextSpawnTime < Time)
            {
                if (spawned >= k_MaxSpawnedPerFrame)
                {
                    spawner.nextSpawnTime = Time;
                    Debug.Log($"Hit max spawn count in frame: {spawned}");
                    break;
                }
                
                var r = spawner.random;
            
                // Spawn chance scales with time, so that the longer the game runs, the more enemies spawn.
                double enemiesPerSecond = (8*Time/1000);
                enemiesPerSecond *= enemiesPerSecond;
                enemiesPerSecond += 0.3d;
                spawner.nextSpawnTime += 1/enemiesPerSecond;
                spawner.nextSpawnTime += r.NextFloat(-0.5f,0.5f); // A little randomness is the spice of life
            
                var rPos = t.Position;
                var rDir = math.normalizesafe(r.NextFloat2(min, max), max) * spawner.SpawnRadius;
                rPos += t.TransformDirection(rDir.f3z());
            
                for (int i = 0; i < PlayerTransforms.Length; i++)
                    if (math.distancesq(PlayerTransforms[i].Position, rPos) <= spawner.SpawnBlockRadiusSqr)
                    {
                        spawner.random = r;
                        return;
                    }
            
                for (int i = 0; i < enemies.Length; i++)
                {
                    if (enemies[i].Chance <= r.NextInt(100))
                        continue;
                        
                    var enemy = ecb.Instantiate(key, enemies[i].Entity);
                    ecb.SetComponent(key, enemy, LocalTransform.FromPosition(rPos + r.NextFloat3(-0.5f, 0.5f)));
                    spawned++;
                }
                
                spawner.random = r;
            }
        }
    }
}