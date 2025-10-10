using Unity.Entities;
using Unity.Jobs;

/// <summary>
/// Destroys projectiles when hit.
/// </summary>
[UpdateInGroup(typeof(ProjectileDamageSystemGroup), OrderLast = true)]
[RequireMatchingQueriesForUpdate]
public partial struct ProjectileClearSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var a = new Job().Schedule(state.Dependency);
        var b = new HitClearJob().Schedule(state.Dependency);
        state.Dependency = JobHandle.CombineDependencies(a,b);
    }
    
    [WithAll(typeof(ProjectileHit))]
    [WithDisabled(typeof(DestroyFlag))]
    partial struct Job : IJobEntity
    {
        public void Execute(EnabledRefRW<DestroyFlag> destroyFlag)
        {
            destroyFlag.ValueRW = true;
        }
    }
    [WithNone(typeof(ProjectileHit))]
    partial struct HitClearJob : IJobEntity
    {
        public void Execute(ref DynamicBuffer<ProjectileHitEntity> hits)
        {
            hits.Clear();
        }
    }
}