using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[UpdateInGroup(typeof(ProjectileDamageSystemGroup))]
[BurstCompile]
public partial struct ProjectileHitSystem_Subdivide : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<NetworkIdMapping>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var delayedEcb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        
        state.Dependency = new Job()
        {
            ecb = delayedEcb.AsParallelWriter(),
            networkIdMapping = SystemAPI.GetSingleton<NetworkIdMapping>()
        }.Schedule(state.Dependency);
    }
    
    [WithAll(typeof(ProjectileHit))]
    partial struct Job : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ecb;
        [ReadOnly] public NetworkIdMapping networkIdMapping;
    
        public void Execute([ChunkIndexInQuery] int Key, in DynamicBuffer<ProjectileHitEntity> hits, in Subdivide Subdivide)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                var e = networkIdMapping[hits[i].Value];
                if (e != Entity.Null)
                {
                    ecb.AppendToBuffer(Key, e, Pending.Subdivide(Subdivide.Value));
                    ecb.SetComponentEnabled<Pending.Dirty>(Key, e, true);
                }
            }
        }
    }
}

[UpdateInGroup(typeof(ProjectileDamageSystemGroup))]
[BurstCompile]
public partial struct SubdivideSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        new Job()
        {
            Time = SystemAPI.Time.ElapsedTime
        }.ScheduleParallel();
    }
    
    partial struct Job : IJobEntity
    {
        [ReadOnly] public double Time;
        public void Execute(ref DynamicBuffer<Pending> pending, ref Subdivide subdivide, ref Subdivide.Timer subdivideTimer, EnabledRefRW<Subdivide> subdivideState)
        {
            if (subdivideTimer.TriggerTime > Time) return;
            
            pending.Add(Pending.Damage(subdivide.Value*10));
            subdivide.Value = 0;
            subdivideState.ValueRW = false;
        }
    }
}