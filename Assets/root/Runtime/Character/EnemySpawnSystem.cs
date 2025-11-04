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
    Wave_01_Common,
    Wave_02_4SecBurst,
    Wave_03_Common,
    Wave_04_Common,
    Wave_05_Common,
}

public static class EnemySpawnerModeExtensions
{
    public static Color GetColor(this EnemySpawnerMode mode)
    {
        switch (mode)
        {
            case EnemySpawnerMode.None:
                return Color.white*new Color(1,1,1,0.5f);
            case EnemySpawnerMode.Wave_01_Common:
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
            case EnemySpawnerMode.Wave_01_Common:
                return 1;
            case EnemySpawnerMode.Wave_02_4SecBurst:
                return 20;
            case EnemySpawnerMode.Wave_03_Common:
                return 2f;
            case EnemySpawnerMode.Wave_04_Common:
                return 3f;
            case EnemySpawnerMode.Wave_05_Common:
                return 4f;
            default:
                Debug.LogError($"No rate for {mode}");
                return 0;
        }
    }
    public static SpawnRadius GetSpawnRadius(this EnemySpawnerMode mode)
    {
        switch (mode)
        {
            default:
                return new SpawnRadius(SpawnRadius.DEFAULT_MIN_RAD, SpawnRadius.DEFAULT_MAX_RAD);
        }
    }
    
    public static int GetEnemyChance(this EnemySpawnerMode mode, int enemy)
    {
        switch (mode)
        {
            case EnemySpawnerMode.Wave_01_Common:
                return enemy == 0 ? 100 : enemy == 1 ? 4 : 0;
            case EnemySpawnerMode.Wave_02_4SecBurst:
                return enemy == 0 ? 100 : 0;
            case EnemySpawnerMode.Wave_03_Common:
                return enemy == 0 ? 100 : enemy == 1 ? 4 : enemy == 2 ? 2 : enemy == 3 ? 2 : 0;
            case EnemySpawnerMode.Wave_04_Common:
                return enemy == 0 ? 90 : enemy == 1 ? 3 : enemy == 2 ? 2 : enemy == 3 ? 2 : enemy == 4 ? 10 : enemy == 5 ? 1 : 0;
            case EnemySpawnerMode.Wave_05_Common:
                return enemy == 0 ? 50 : enemy == 1 ? 8 : enemy == 2 ? 2 : enemy == 3 ? 2 : enemy == 4 ? 50 : enemy == 5 ? 2 : 0;
                
            default:
                return 0;
        }
    }

    public static EnemySpawnerMode GetWaveAtTime(double time, uint sharedRandomSeed)
    {
        if (time < 60)
            return EnemySpawnerMode.Wave_01_Common;
        if (time < 64)
            return EnemySpawnerMode.Wave_02_4SecBurst;
        if (time < 120)
            return EnemySpawnerMode.Wave_03_Common;
        if (time < 300)
            return EnemySpawnerMode.Wave_04_Common;
        if (time < 360)
            return EnemySpawnerMode.Wave_05_Common;
        return EnemySpawnerMode.None;
    }
}

public readonly struct SpawnRadius
{
    public const float DEFAULT_MIN_RAD = 35;
    public const float DEFAULT_MAX_RAD = 40;

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

public struct EnemySpawner : IComponentData, IEnableableComponent
{
    public EnemySpawnerMode Mode;
}

[RequireMatchingQueriesForUpdate]
[UpdateInGroup(typeof(SurvivorSimulationSystemGroup))]
[GameTypeOnlySystem(1)]
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
        
        state.Enabled = GameDebug.EnemySpawningEnabled;
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
            if (mode == EnemySpawnerMode.Wave_01_Common)
                mode = EnemySpawnerModeExtensions.GetWaveAtTime(time, sharedRandom.Seed);
            
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
                var chance = mode.GetEnemyChance(j);
                if (chance <= 0) 
                    continue;
             
                var enemy = enemies[j];
                if (chance < 100 && chance <= random.NextInt(100))
                    continue;
                        
                var enemyE = ecb.Instantiate(enemy.Entity);
                var t = LocalTransform.FromPosition(rPos + random.NextFloat3(-0.5f, 0.5f));
                ecb.SetComponent(enemyE, t);
                ecb.SetComponent(enemyE, new SpawnAnimation(t));
                ecb.SetComponentEnabled<SpawnAnimation>(enemyE, true);
            }
        }
        
        playerTransforms.Dispose();
        
    }
}

[Save]
public struct SpawnAnimation : IComponentData, IEnableableComponent
{
    public static float ScaleTime => 1;

    public float t;
    public float s;
    
    public SpawnAnimation(LocalTransform zeroT)
    {
        t = 0;
        s = zeroT.Scale;
    }
}

[RequireMatchingQueriesForUpdate]
public partial struct SpawnAnimationSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        new Job()
        {
            dt = SystemAPI.Time.DeltaTime
        }.Schedule();
    }
    
    partial struct Job : IJobEntity
    {
        [ReadOnly] public float dt;
    
        public void Execute(ref LocalTransform transform, ref SpawnAnimation anim, EnabledRefRW<SpawnAnimation> animEnabled)
        {
            anim.t += dt;
            var t = math.clamp(anim.t/SpawnAnimation.ScaleTime, 0, 1);
            transform.Scale = anim.s * ease.elastic_out(t);
            animEnabled.ValueRW = t < 1;
        }
    }
}