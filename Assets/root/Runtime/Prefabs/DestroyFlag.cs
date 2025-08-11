using Unity.Entities;

public struct DestroyFlag : IComponentData
{
}

[UpdateInGroup(typeof(SurvivorSimulationSystemGroup), OrderFirst = true)]
public partial class DestroySystemGroup : ComponentSystemGroup
{
        
}

[UpdateInGroup(typeof(DestroySystemGroup), OrderLast = true)]
public partial struct DestroySystem : ISystem
{
    EntityQuery m_DestroyQuery;

    public void OnCreate(ref SystemState state)
    {
        m_DestroyQuery = SystemAPI.QueryBuilder().WithAll<DestroyFlag>().Build();
        state.RequireForUpdate(m_DestroyQuery);
    }

    public void OnUpdate(ref SystemState state)
    {
        state.EntityManager.DestroyEntity(m_DestroyQuery);
    }
}