using System;
using BovineLabs.Saving;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public enum EnemySpawnerMode
{
    None,
    Common,
}

public static class EnemySpawnerModeExtensions
{
    public static Color GetColor(this EnemySpawnerMode mode)
    {
        switch (mode)
        {
            case EnemySpawnerMode.None:
                return Color.white*new Color(1,1,1,0.5f);
            case EnemySpawnerMode.Common:
            default:
                return Color.green;
        }
    }
    public static float GetEnemiesPerSecond(this EnemySpawnerMode mode, double elapsed)
    {
        switch (mode)
        {
            case EnemySpawnerMode.None:
                return 0;
            case EnemySpawnerMode.Common:
                // Spawn chance scales with time, so that the longer the game runs, the more enemies spawn.
                double enemiesPerSecond = (8*elapsed/1000);
                enemiesPerSecond *= enemiesPerSecond;
                enemiesPerSecond += 0.3d;
                return (float)enemiesPerSecond;
            default:
                Debug.LogError($"");
                return 0;
        }
    }
    public static SpawnRadius GetSpawnRadius(this EnemySpawnerMode mode)
    {
        switch (mode)
        {
            case EnemySpawnerMode.Common:
                return new SpawnRadius(29, 30);
            default:
                Debug.LogError($"No radius for {mode}");
                return new SpawnRadius(29, 30);
        }
    }
    public static bool IsValidEnemy(this EnemySpawnerMode mode, int enemy)
    {
        switch (mode)
        {
            case EnemySpawnerMode.Common:
                return enemy == 0 || enemy == 1;
            default:
                return false;
        }
    }
}

public readonly struct SpawnRadius
{
    public readonly float Min;
    public readonly float MinSqr;
    public readonly float Max;
    public readonly float MaxSqr;
    
    public SpawnRadius(float min, float max)
    {
        Min = min;
        MinSqr = min*min;
        Max = max;
        MaxSqr = max*max;
    }
}

public struct EnemySpawner : IComponentData
{
    public EnemySpawnerMode Mode;
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
        state.RequireForUpdate<GameManager.Enemies>();
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<PlayerControlled>();
        m_PlayerTransformsQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, EnemySpawner>().Build();
        state.RequireForUpdate(m_PlayerTransformsQuery);
        
        state.Enabled = false;
    }

    public void OnUpdate(ref SystemState state)
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        var sharedRandom = SystemAPI.GetSingleton<SharedRandom>();
        
        var time = SystemAPI.Time.ElapsedTime;
        var dt = SystemAPI.Time.DeltaTime;
        var playerTransforms = m_PlayerTransformsQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        var playerSpawners = m_PlayerTransformsQuery.ToComponentDataArray<EnemySpawner>(Allocator.Temp);
        var enemies = SystemAPI.GetSingletonBuffer<GameManager.Enemies>(true);
        for (int i = 0; i < playerTransforms.Length; i++)
        {
            // Detect the spawner mode required for each player and do different actions for each
            var random = sharedRandom.Random;
            EnemySpawnerMode mode = playerSpawners[i].Mode;
            float enemiesPerSecond = mode.GetEnemiesPerSecond(time);
            if (random.NextFloat() > enemiesPerSecond*dt)
                continue;
            
            var zero = playerTransforms[i];
            SpawnRadius spawnRadius = mode.GetSpawnRadius();
            var rDir = random.NextFloat2Direction() * spawnRadius.Max;
            var rPos = zero.Position + zero.TransformDirection(rDir.f3z());
                
            bool failed = false;
            for (int j = 0; j < playerTransforms.Length; j++)
                if (math.distancesq(playerTransforms[j].Position, rPos) <= spawnRadius.MinSqr)
                {
                    failed = true;
                    break;
                }
            if (failed) continue;
                
            for (int j = 0; j < enemies.Length; j++)
            {
                if (!mode.IsValidEnemy(j)) 
                    continue;
             
                var enemy = enemies[j];   
                if (enemy.Chance <= random.NextInt(100))
                    continue;
                        
                var enemyE = ecb.Instantiate(enemy.Entity);
                ecb.SetComponent(enemyE, LocalTransform.FromPosition(rPos + random.NextFloat3(-0.5f, 0.5f)));
            }
        }
        
        playerTransforms.Dispose();
        
    }
}