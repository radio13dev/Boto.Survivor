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
public partial struct EnemySpawnSystem : ISystem
{
    EntityQuery m_PlayerTransformsQuery;
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<StepController>();
        state.RequireForUpdate<SharedRandom>();
        state.RequireForUpdate<GameManager.Resources>();
        state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<PlayerControlled>();
        m_PlayerTransformsQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, PlayerControlled>().Build();
        state.RequireForUpdate(m_PlayerTransformsQuery);
    }

    public void OnUpdate(ref SystemState state)
    {
        var playerTransforms = m_PlayerTransformsQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        var delayedEcb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        state.Dependency = new Job()
        {
            ecb = delayedEcb.AsParallelWriter(),
            resources = SystemAPI.GetSingleton<GameManager.Resources>(),
            PlayerTransforms = playerTransforms,
            stepController = SystemAPI.GetSingleton<StepController>(),
            Time = SystemAPI.Time.ElapsedTime,
            dt = SystemAPI.Time.DeltaTime
        }.Schedule(state.Dependency);
        playerTransforms.Dispose(state.Dependency);
        
    }
    
    partial struct Job : IJobEntity
    {
        const int k_MaxSpawnedPerFrame = 10;
    
        static readonly float2 min = new float2(-1,-1);
        static readonly float2 max = new float2(1,1);
    
        public EntityCommandBuffer.ParallelWriter ecb;
        [ReadOnly] public GameManager.Resources resources;
        [ReadOnly] public StepController stepController;
        [ReadOnly] public double Time;
        [ReadOnly] public float dt;
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
            
                // Spawn chance scales with time, so that the longer the game runs, the more enemies spawn.
                double enemiesPerSecond = (8*Time/1000);
                enemiesPerSecond *= enemiesPerSecond;
                enemiesPerSecond += 0.3d;
                spawner.nextSpawnTime += 1/enemiesPerSecond;
                spawner.nextSpawnTime += spawner.random.NextFloat(-0.5f,0.5f); // A little randomness is the spice of life
            
                var rPos = t.Position;
                var rDir = math.normalizesafe(spawner.random.NextFloat2(min, max), max) * spawner.SpawnRadius;
                rPos += t.TransformDirection(rDir.f3z());
            
                for (int i = 0; i < PlayerTransforms.Length; i++)
                    if (math.distancesq(PlayerTransforms[i].Position, rPos) <= spawner.SpawnBlockRadiusSqr)
                        return;
            
                var enemy = ecb.Instantiate(key, resources.EnemyTemplate);
                ecb.SetComponent(key, enemy, LocalTransform.FromPosition(rPos));
                spawned++;
            }
        }
    }
}