using BovineLabs.Saving;
using Unity.Collections;
using Unity.Entities;

[Save]
public struct DestroyFlag : IComponentData, IEnableableComponent
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
    EntityQuery m_DestroyLinkedQuery;

    public void OnCreate(ref SystemState state)
    {
        m_DestroyQuery = SystemAPI.QueryBuilder().WithAll<DestroyFlag>().WithNone<LinkedEntityGroup>().Build();
        m_DestroyLinkedQuery = SystemAPI.QueryBuilder().WithAll<DestroyFlag>().WithAll<LinkedEntityGroup>().Build();
        state.RequireAnyForUpdate(m_DestroyQuery, m_DestroyLinkedQuery);
    }

    public void OnUpdate(ref SystemState state)
    {
        state.EntityManager.DestroyEntity(m_DestroyQuery);
        
        var linksToDestroy = m_DestroyLinkedQuery.ToEntityArray(Allocator.Temp);
        for (int i = 0; i < linksToDestroy.Length; i++)
            state.EntityManager.DestroyEntity(SystemAPI.GetBuffer<LinkedEntityGroup>(linksToDestroy[i]).AsNativeArray().Reinterpret<Entity>());
    }
}