using Unity.Entities;

public struct DestroyFlag : IComponentData
{
}

[UpdateInGroup(typeof(LateSimulationSystemGroup), OrderLast = true)]
public partial struct DestroySystem : ISystem
{
    EntityQuery m_DestroyQuery;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        m_DestroyQuery = SystemAPI.QueryBuilder().WithAll<DestroyFlag>().Build();
        state.RequireForUpdate(m_DestroyQuery);
    }

    public void OnUpdate(ref SystemState state)
    {
        var delayedEcb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        delayedEcb.DestroyEntity(m_DestroyQuery, EntityQueryCaptureMode.AtPlayback);
    }
}