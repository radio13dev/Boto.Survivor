using System;
using BovineLabs.Saving;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[Serializable]
[Save]
public partial struct EnemySpawner : IComponentData
{
    [HideInInspector] public Unity.Mathematics.Random random;
    public float SpawnRadius;
    public float SpawnBlockRadiusSqr;
    public int SpawnChancePerFrame;
}

[RequireMatchingQueriesForUpdate]
public partial struct EnemySpawnSystem : ISystem
{
    EntityQuery m_PlayerTransformsQuery;
    public void OnCreate(ref SystemState state)
    {
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
            PlayerTransforms = playerTransforms
        }.Schedule(state.Dependency);
        playerTransforms.Dispose(state.Dependency);
        
    }
    
    partial struct Job : IJobEntity
    {
        static readonly float2 min = new float2(-1,-1);
        static readonly float2 max = new float2(1,1);
    
        public EntityCommandBuffer.ParallelWriter ecb;
        public GameManager.Resources resources;
        [ReadOnly] public NativeArray<LocalTransform> PlayerTransforms;
    
        public void Execute([ChunkIndexInQuery] int key, in LocalTransform t, ref EnemySpawner spawner)
        {
            if (spawner.random.NextInt(100) > spawner.SpawnChancePerFrame)
                return;
        
            var rPos = t.Position;
            var rDir = math.normalizesafe(spawner.random.NextFloat2(min, max), max) * spawner.SpawnRadius;
            rPos += t.TransformDirection(rDir.f3z());
            
            for (int i = 0; i < PlayerTransforms.Length; i++)
                if (math.distancesq(PlayerTransforms[i].Position, rPos) <= spawner.SpawnBlockRadiusSqr)
                    return;
            
            var enemy = ecb.Instantiate(key, resources.EnemyTemplate);
            ecb.SetComponent(key, enemy, LocalTransform.FromPosition(rPos));
        }
    }
}