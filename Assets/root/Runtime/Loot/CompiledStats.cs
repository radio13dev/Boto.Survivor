using BovineLabs.Saving;
using Unity.Entities;

[Save]
public struct CompiledStats : IComponentData
{
    public RingStats CombinedRingStats;
}

[Save]
public struct CompiledStatsDirty : IComponentData, IEnableableComponent {}

public partial struct CompiledStatsSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<CompiledStatsDirty>();
    }

    public void OnUpdate(ref SystemState state)
    {
        new Job().Schedule();
    }
    
    [WithAll(typeof(CompiledStatsDirty))]
    partial struct Job : IJobEntity
    {
        public void Execute(Entity entity, ref CompiledStats stats, EnabledRefRW<CompiledStatsDirty> statsDirty, in DynamicBuffer<Ring> rings)
        {
            statsDirty.ValueRW = false;
        }
    }
}