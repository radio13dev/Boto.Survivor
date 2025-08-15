using Unity.Entities;

/// <summary>
/// Destroys projectiles when hit.
/// </summary>
[UpdateInGroup(typeof(ProjectileSystemGroup), OrderLast = true)]
[RequireMatchingQueriesForUpdate]
public partial struct ProjectileClearSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency = new Job().Schedule(state.Dependency);
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
}