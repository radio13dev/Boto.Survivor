using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[Serializable]
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
        m_PlayerTransformsQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform2D, PlayerControlled>().Build();
        state.RequireForUpdate(m_PlayerTransformsQuery);
    }

    NativeArray<LocalTransform2D> m_PlayerTransformsQueryResults;
    public void OnUpdate(ref SystemState state)
    {
        m_PlayerTransformsQueryResults.Dispose();
        var delayedEcb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        new Job()
        {
            ecb = delayedEcb.AsParallelWriter(),
            resources = SystemAPI.GetSingleton<GameManager.Resources>(),
            PlayerTransforms = m_PlayerTransformsQueryResults = m_PlayerTransformsQuery.ToComponentDataArray<LocalTransform2D>(Allocator.TempJob)
        }.Schedule();
    }
    
    partial struct Job : IJobEntity
    {
        static readonly float2 min = new float2(-1,-1);
        static readonly float2 max = new float2(1,1);
    
        public EntityCommandBuffer.ParallelWriter ecb;
        public GameManager.Resources resources;
        [ReadOnly] public NativeArray<LocalTransform2D> PlayerTransforms;
    
        public void Execute([ChunkIndexInQuery] int key, in LocalTransform2D t, ref EnemySpawner spawner)
        {
            if (spawner.random.NextInt(100) > spawner.SpawnChancePerFrame)
                return;
        
            var rPos = t.Position;
            rPos += math.normalizesafe(spawner.random.NextFloat2(min, max), max) * spawner.SpawnRadius;
            
            for (int i = 0; i < PlayerTransforms.Length; i++)
                if (math.distancesq(PlayerTransforms[i].Position, rPos) <= spawner.SpawnBlockRadiusSqr)
                    return;
            
            var enemy = ecb.Instantiate(key, resources.EnemyTemplate);
            ecb.SetComponent(key, enemy, LocalTransform2D.FromPosition(rPos));
        }
    }
}