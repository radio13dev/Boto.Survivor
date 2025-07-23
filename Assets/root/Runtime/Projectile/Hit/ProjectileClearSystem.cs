using Unity.Entities;

/// <summary>
/// Destroys projectiles when hit.
/// </summary>
[UpdateInGroup(typeof(ProjectileSystemGroup), OrderLast = true)]
public partial struct ProjectileClearSystem : ISystem
{
    EntityQuery m_CleanupQuery;
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        m_CleanupQuery = SystemAPI.QueryBuilder().WithAll<ProjectileHit>().WithAbsent<DestroyFlag>().Build();
        state.RequireForUpdate(m_CleanupQuery);
    }

    public void OnUpdate(ref SystemState state)
    {
        var delayedEcb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        delayedEcb.AddComponent<DestroyFlag>(m_CleanupQuery, EntityQueryCaptureMode.AtPlayback);
    }
}