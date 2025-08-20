using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

[UpdateInGroup(typeof(ProjectileSystemGroup))]
[BurstCompile]
public partial struct ProjectileHitSystem_Pierce : ISystem
{
    public void OnCreate(ref SystemState state)
    {
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency = new Job()
        {
        }.Schedule(state.Dependency);
    }
    
    [WithAll(typeof(ProjectileHit))]
    partial struct Job : IJobEntity
    {
        public void Execute(in DynamicBuffer<ProjectileHitEntity> hits, ref DynamicBuffer<ProjectileIgnoreEntity> ignore, ref Pierce pierce, EnabledRefRW<ProjectileHit> hitState)
        {
            if (pierce.Value > 0)
            {
                if (!pierce.IsInfinite) pierce.Value--;
                ignore.AddRange(hits.AsNativeArray().Reinterpret<ProjectileIgnoreEntity>());
                hitState.ValueRW = false;
            }
        }
    }
}