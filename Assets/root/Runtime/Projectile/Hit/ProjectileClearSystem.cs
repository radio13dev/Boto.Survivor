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
        m_CleanupQuery = SystemAPI.QueryBuilder().WithAll<ProjectileHit>().WithDisabled<DestroyFlag>().Build();
        state.RequireForUpdate(m_CleanupQuery);
    }

    public void OnUpdate(ref SystemState state)
    {
        state.EntityManager.SetComponentEnabled<DestroyFlag>(m_CleanupQuery, true);
    }
}