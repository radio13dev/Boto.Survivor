using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial struct EnemySpawner : IComponentData
{
    public Unity.Mathematics.Random random;
    
    public const float SpawnRadius = 10;
    public const float SpawnBlockRadiusSqr = 9*9;
    
    public EnemySpawner(int init)
    {
        random = Random.CreateFromIndex(unchecked((uint)init));
    }
}

[RequireMatchingQueriesForUpdate]
public partial struct EnemySpawnSystem : ISystem
{
    EntityQuery m_PlayerTransforms;
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GameManager.Resources>();
        state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<PlayerControlled>();
        m_PlayerTransforms = SystemAPI.QueryBuilder().WithAll<LocalTransform, PlayerControlled, EnemySpawner>().Build();
        state.RequireForUpdate(m_PlayerTransforms);
    }


    public void OnUpdate(ref SystemState state)
    {
        var playerTransforms = m_PlayerTransforms.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        var delayedEcb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        new Job()
        {
            ecb = delayedEcb.AsParallelWriter(),
            resources = SystemAPI.GetSingleton<GameManager.Resources>(),
            PlayerTransforms = playerTransforms
        }.Schedule();
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
            var rPos = t.Position;
            rPos += math.normalizesafe(spawner.random.NextFloat2(min, max), max).f3() * EnemySpawner.SpawnRadius;
            
            for (int i = 0; i < PlayerTransforms.Length; i++)
                if (math.distancesq(PlayerTransforms[i].Position, rPos) <= EnemySpawner.SpawnBlockRadiusSqr)
                    return;
            
            var enemy = ecb.Instantiate(key, resources.EnemyTemplate);
            ecb.SetComponent(key, enemy, LocalTransform.FromPosition(rPos));
        }
    }
}